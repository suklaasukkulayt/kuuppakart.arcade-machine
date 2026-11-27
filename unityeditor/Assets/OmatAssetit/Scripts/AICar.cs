using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICar : MonoBehaviour
{
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;
    public float speed = 10f;
    public float rotationSpeed = 5f;

    [Header("Acceleration")]
    public float acceleration = 6f;
    [Range(0.05f, 1f)] public float topSpeedAccelerationFactor = 0.35f;

    [Header("Driving variation")]
    [Min(0f)] public float maxLaneOffset = 1.1f;
    [Min(0f)] public float laneWanderAmount = 0.3f;
    [Min(0f)] public float laneWanderSpeed = 0.18f;
    [Min(0f)] public float laneChangeSpeed = 2.2f;
    [Range(0f, 0.2f)] public float speedVariation = 0.05f;

    [Header("Overtaking")]
    public bool enableOvertaking = true;
    [Min(1f)] public float overtakeDetectionDistance = 11f;
    [Min(0f)] public float overtakeCorridorWidth = 4f;
    [Min(0f)] public float overtakeOffset = 1.7f;

    [Header("Difficulty")]
    public float easyModeSpeedMultiplier = 0.85f;
    public float hardModeSpeedMultiplier = 1.15f;
    public float easyModeStartOffsetBack = 6f;

    [Header("Crashing")]
    public float stunImpactThreshold = 6f;
    public float stunDuration = 1.5f;
    public float extraSpinTorque = 8f;
    [Range(0f, 1f)] public float knockbackUpFactor = 0.28f;
    [Range(0f, 1f)] public float spinImpactFactor = 0.12f;

    private static readonly List<AICar> activeCars = new List<AICar>();
    private Rigidbody rb;
    private bool isStunned;
    private float stunTimer;
    private float currentSpeed;
    private float personalLaneOffset;
    private float currentLaneOffset;
    private float laneNoiseSeed;

    public Vector3 CurrentVelocity => isStunned ? Vector3.zero : transform.forward * currentSpeed;
    public bool LapArmed { get; private set; }

    public void ConsumeLap()
    {
        LapArmed = false;
    }

    private void OnEnable()
    {
        if (!activeCars.Contains(this))
            activeCars.Add(this);
    }

    private void OnDisable()
    {
        activeCars.Remove(this);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (GetComponent<PelaajanKierrosTarkastus>() == null)
            gameObject.AddComponent<PelaajanKierrosTarkastus>();
    }

    private void Start()
    {
        AssignPersonalLane();
        laneNoiseSeed = Random.Range(0f, 1000f);
        currentLaneOffset = personalLaneOffset;

        if (DifficultyManager.Instance != null)
        {
            switch (DifficultyManager.Instance.SelectedDifficulty)
            {
                case Difficulty.Easy:
                    speed *= easyModeSpeedMultiplier;
                    Vector3 backPos = rb.position - transform.forward * easyModeStartOffsetBack;
                    rb.position = backPos;
                    transform.position = backPos;
                    break;

                case Difficulty.Hard:
                    speed *= hardModeSpeedMultiplier;
                    break;
            }
        }

        speed *= Random.Range(1f - speedVariation, 1f + speedVariation);
    }

    private void AssignPersonalLane()
    {
        activeCars.RemoveAll(car => car == null);
        activeCars.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

        int count = activeCars.Count;
        int index = activeCars.IndexOf(this);
        personalLaneOffset = count <= 1 || index < 0 ? 0f : Mathf.Lerp(-maxLaneOffset, maxLaneOffset, index / (float)(count - 1));
    }

    public bool ReceivePlayerImpact(Rigidbody sourceBody, Vector3 direction, float impactSpeed)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null || rb.isKinematic || sourceBody == null || sourceBody.isKinematic)
            return false;

        float totalMass = Mathf.Max(0.01f, sourceBody.mass + rb.mass);
        float transferredSpeed = impactSpeed * (2f * sourceBody.mass / totalMass);

        if (transferredSpeed < stunImpactThreshold)
            return false;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = -transform.forward;

        direction.Normalize();

        isStunned = true;
        stunTimer = stunDuration;
        currentSpeed = 0f;

        Vector3 velocity = direction * transferredSpeed;
        velocity.y = Mathf.Max(rb.linearVelocity.y, transferredSpeed * knockbackUpFactor);
        rb.linearVelocity = velocity;
        Vector3 randomAxis = new Vector3( Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        rb.AddTorque(randomAxis * extraSpinTorque * transferredSpeed * spinImpactFactor, ForceMode.Impulse);
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<Player>() != null)
            return;

        if (collision.collider.GetComponentInParent<CarIdentify>() == null)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < stunImpactThreshold)
            return;

        isStunned = true;
        stunTimer = stunDuration;
        currentSpeed = 0f;

        Vector3 randomAxis = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        rb.AddTorque(randomAxis * extraSpinTorque * impactSpeed, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer <= 0f)
                isStunned = false;

            return;
        }

        if (waypoints == null || waypoints.Length == 0)
            return;

        currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Length - 1);
        Transform target = waypoints[currentWaypointIndex];

        if (target == null)
            return;

        float t = speed > 0.01f ? Mathf.Clamp01(currentSpeed / speed) : 1f;
        float a = acceleration * Mathf.Lerp(1f, topSpeedAccelerationFactor, t * t);
        currentSpeed = Mathf.MoveTowards(currentSpeed, speed, a * Time.fixedDeltaTime);

        float desiredLaneOffset = GetDesiredLaneOffset();
        currentLaneOffset = Mathf.MoveTowards(currentLaneOffset, desiredLaneOffset, laneChangeSpeed * Time.fixedDeltaTime);

        Vector3 laneTarget = target.position + GetWaypointRight(currentWaypointIndex) * currentLaneOffset;
        Vector3 targetXZ = new Vector3(laneTarget.x, rb.position.y, laneTarget.z);
        Vector3 direction = targetXZ - rb.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, lookRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        rb.MovePosition(rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime);

        Vector3 waypointCenter = new Vector3(target.position.x, rb.position.y, target.position.z);
        if (Vector3.Distance(rb.position, waypointCenter) < 2f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;

            if (currentWaypointIndex == 0)
                LapArmed = true;
        }
    }

    private float GetDesiredLaneOffset()
    {
        float noise = (Mathf.PerlinNoise(laneNoiseSeed, Time.time * laneWanderSpeed) * 2f - 1f) * laneWanderAmount;
        float normalOffset = personalLaneOffset + noise;

        if (!enableOvertaking || activeCars.Count < 2)
            return normalOffset;

        AICar nearestCar = null;
        Vector3 nearestDelta = Vector3.zero;
        float nearestForwardDistance = overtakeDetectionDistance;

        for (int i = 0; i < activeCars.Count; i++)
        {
            AICar other = activeCars[i];
            if (other == null || other == this || !other.isActiveAndEnabled || other.rb == null)
                continue;

            Vector3 delta = other.rb.position - rb.position;
            delta.y = 0f;

            float forwardDistance = Vector3.Dot(delta, transform.forward);
            if (forwardDistance <= 0.5f || forwardDistance >= nearestForwardDistance)
                continue;

            float lateralDistance = Mathf.Abs(Vector3.Dot(delta, transform.right));
            if (lateralDistance > overtakeCorridorWidth)
                continue;

            nearestCar = other;
            nearestDelta = delta;
            nearestForwardDistance = forwardDistance;
        }

        if (nearestCar == null)
            return normalOffset;

        float otherSide = Vector3.Dot(nearestDelta, transform.right);
        float passSide = otherSide > 0.15f ? -1f : otherSide < -0.15f ? 1f : personalLaneOffset >= 0f ? -1f : 1f;
        float passTarget = passSide * overtakeOffset;
        float passStrength = Mathf.InverseLerp(overtakeDetectionDistance, 1.5f, nearestForwardDistance);

        return Mathf.Lerp(normalOffset, passTarget, passStrength);
    }

    private Vector3 GetWaypointRight(int waypointIndex)
    {
        if (waypoints == null || waypoints.Length < 2)
            return transform.right;

        int previousIndex = (waypointIndex - 1 + waypoints.Length) % waypoints.Length;
        int nextIndex = (waypointIndex + 1) % waypoints.Length;
        Transform previous = waypoints[previousIndex];
        Transform next = waypoints[nextIndex];
        Vector3 tangent = previous != null && next != null ? next.position - previous.position : transform.forward;
        tangent.y = 0f;

        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = waypoints[waypointIndex] != null ? waypoints[waypointIndex].forward : transform.forward;
            tangent.y = 0f;
        }

        if (tangent.sqrMagnitude < 0.0001f)
            return transform.right;

        return Vector3.Cross(Vector3.up, tangent.normalized).normalized;
    }
}