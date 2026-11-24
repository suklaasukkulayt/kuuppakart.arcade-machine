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

    private Rigidbody rb;
    private bool isStunned;
    private float stunTimer;
    private float currentSpeed;

    public Vector3 CurrentVelocity => isStunned ? Vector3.zero : transform.forward * currentSpeed;
    public bool LapArmed { get; private set; }

    public void ConsumeLap()
    {
        LapArmed = false;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (GetComponent<PelaajanKierrosTarkastus>() == null)
            gameObject.AddComponent<PelaajanKierrosTarkastus>();
    }

    private void Start()
    {
        if (DifficultyManager.Instance == null)
            return;

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

        Vector3 randomAxis = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

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

        Vector3 randomAxis = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

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

        float t = speed > 0.01f ? Mathf.Clamp01(currentSpeed / speed) : 1f;
        float a = acceleration * Mathf.Lerp(1f, topSpeedAccelerationFactor, t * t);
        currentSpeed = Mathf.MoveTowards(currentSpeed, speed, a * Time.fixedDeltaTime);

        Transform target = waypoints[currentWaypointIndex];

        if (target == null)
            return;

        Vector3 targetXZ = new Vector3(target.position.x, rb.position.y, target.position.z);
        Vector3 direction = (targetXZ - rb.position).normalized;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, lookRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        rb.MovePosition(rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime);

        if (Vector3.Distance(rb.position, targetXZ) < 2f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;

            if (currentWaypointIndex == 0)
                LapArmed = true;
        }
    }
}