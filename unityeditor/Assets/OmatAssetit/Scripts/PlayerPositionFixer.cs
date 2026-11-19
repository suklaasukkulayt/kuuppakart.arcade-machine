using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerUprightAndShortcuts_AutoFallReset : MonoBehaviour
{
    [Header("Auto-fall reset")]
    public float fallYThreshold = -5f;

    [Header("Tilt detection")]
    public float angleThreshold = 45f;
    public float angularVelocityMin = 0.5f;
    public float stuckTimeThreshold = 1.5f;
    public float correctionRetriggerCooldown = 1.0f;
    public float maxCorrectionDuration = 2.0f;

    [Header("Smooth auto-correction")]
    public float autoCorrectionDegreesPerSecond = 360f;
    public float finishAngleEpsilon = 0.5f;
    public bool correctOnlyYaw = true;

    [Header("Camera settings")]
    public bool useCameraYaw = true;

    private Rigidbody rb;
    private Camera mainCamera;
    private Player player;
    private bool isAutoCorrecting;
    private Quaternion autoTargetRotation = Quaternion.identity;
    private float tiltTimer;
    private float correctionCooldownTimer;
    private float correctionElapsedTime;
    private bool suspended;

    public bool IsAutoCorrecting => isAutoCorrecting;
    public bool Suspended
    {
        get => suspended;
        set
        {
            suspended = value;
            if (value)
            {
                isAutoCorrecting = false;
                tiltTimer = 0f;
            }
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<Player>();
        mainCamera = GetComponentInChildren<Camera>();
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogWarning("PlayerUprightAndShortcuts_AutoFallReset: No camera was found. Camera-based yaw correction is unavailable.");
    }

    private void Update()
    {
        if (transform.position.y < fallYThreshold)
            DoFallSafetyTeleport();
    }

    private void FixedUpdate()
    {
        if (suspended)
        {
            tiltTimer = 0f;
            return;
        }

        if (correctionCooldownTimer > 0f)
            correctionCooldownTimer -= Time.fixedDeltaTime;

        if (!isAutoCorrecting && correctionCooldownTimer <= 0f && IsTiltedBeyondThreshold())
            StartAutoCorrection();

        if (isAutoCorrecting)
            PerformAutoCorrectionStep();
    }

    private void DoFallSafetyTeleport()
    {
        Vector3 current = rb.position;
        rb.position = new Vector3(current.x, 0f, current.z);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        tiltTimer = 0f;

        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        Quaternion uprightKeepHeading = flatForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
            : Quaternion.identity;
        StartAutoCorrection(uprightKeepHeading);
    }

    private bool IsTiltedBeyondThreshold()
    {
        float angleFromUp = Vector3.Angle(transform.up, Vector3.up);
        if (player != null && player.IsGrounded && angleFromUp < 85f)
        {
            tiltTimer = 0f;
            return false;
        }

        if (angleFromUp <= angleThreshold)
        {
            tiltTimer = 0f;
            return false;
        }

        if (angularVelocityMin <= 0f || rb.angularVelocity.magnitude >= angularVelocityMin)
        {
            tiltTimer = 0f;
            return true;
        }

        tiltTimer += Time.fixedDeltaTime;
        return tiltTimer >= stuckTimeThreshold;
    }

    private void StartAutoCorrection(Quaternion? optionalTarget = null)
    {
        Quaternion target = optionalTarget ?? GetLookDirectionRotation();
        if (correctOnlyYaw)
            target = Quaternion.Euler(0f, target.eulerAngles.y, 0f);

        autoTargetRotation = target;
        isAutoCorrecting = true;
        tiltTimer = 0f;
        correctionElapsedTime = 0f;
        rb.angularVelocity = Vector3.zero;
    }

    private void PerformAutoCorrectionStep()
    {
        correctionElapsedTime += Time.fixedDeltaTime;
        if (correctionElapsedTime >= maxCorrectionDuration)
        {
            rb.angularVelocity = Vector3.zero;
            isAutoCorrecting = false;
            correctionCooldownTimer = correctionRetriggerCooldown;
            return;
        }

        float angleToTarget = Quaternion.Angle(rb.rotation, autoTargetRotation);
        if (angleToTarget <= finishAngleEpsilon)
        {
            rb.MoveRotation(autoTargetRotation);
            rb.angularVelocity = Vector3.zero;
            isAutoCorrecting = false;
            correctionCooldownTimer = correctionRetriggerCooldown;
            return;
        }

        float maxDelta = autoCorrectionDegreesPerSecond * Time.fixedDeltaTime;
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, autoTargetRotation, maxDelta));
        rb.angularVelocity = Vector3.zero;
    }

    private Quaternion GetLookDirectionRotation()
    {
        if (useCameraYaw && mainCamera != null)
            return Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y, 0f);

        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatForward.sqrMagnitude < 0.0001f)
            return rb.rotation;
        return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        if (isAutoCorrecting)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + autoTargetRotation * Vector3.forward * 2f);
        }
        Gizmos.color = Color.yellow;
        Vector3 from = new Vector3(transform.position.x - 2f, fallYThreshold, transform.position.z - 2f);
        Vector3 to = new Vector3(transform.position.x + 2f, fallYThreshold, transform.position.z + 2f);
        Gizmos.DrawLine(from, to);
    }
}