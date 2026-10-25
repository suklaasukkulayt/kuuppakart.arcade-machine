using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICar : MonoBehaviour
{
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;
    public float speed = 10f;
    public float rotationSpeed = 5f;

    [Header("Difficulty")]
    public float easyModeSpeedMultiplier = 0.85f;
    public float hardModeSpeedMultiplier = 1.15f;
    public float easyModeStartOffsetBack = 6f;

    [Header("Crashing")]
    public float stunImpactThreshold = 6f;
    public float stunDuration = 1.5f;
    public float extraSpinTorque = 8f;

    private Rigidbody rb;
    private bool isStunned = false;
    private float stunTimer = 0f;
    public bool LapArmed { get; private set; } = false;

    public void ConsumeLap()
    {
        LapArmed = false;
    }

    void Awake()
    {
        if (GetComponent<PelaajanKierrosTarkastus>() == null)
            gameObject.AddComponent<PelaajanKierrosTarkastus>();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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
                case Difficulty.Normal:
                default:
                    break;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<CarIdentify>() == null)
        {
            return;
        }

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed >= stunImpactThreshold)
        {
            isStunned = true;
            stunTimer = stunDuration;

            Vector3 randomAxis = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;

            rb.AddTorque(randomAxis * extraSpinTorque * impactSpeed, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        if (GameManager.Instance.Phase != RacePhase.Racing)
        {
            return;
        }

        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
            }
            return;
        }

        Transform target = waypoints[currentWaypointIndex];
        Vector3 targetXZ = new Vector3(target.position.x, rb.position.y, target.position.z);
        Vector3 direction = (targetXZ - rb.position).normalized;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, lookRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        rb.MovePosition(rb.position + transform.forward * speed * Time.fixedDeltaTime);

        if (Vector3.Distance(rb.position, targetXZ) < 2f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;

            if (currentWaypointIndex == 0)
                LapArmed = true;
        }
    }
}