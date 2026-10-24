using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerUprightAndShortcuts_AutoFallReset : MonoBehaviour
{
    [Header("Teleport & shortcuts")]
    public Vector3 teleportPosition = new Vector3(13f, 0f, 47f);

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

    Rigidbody rb;
    Camera mainCamera;

    bool isAutoCorrecting = false;
    Quaternion autoTargetRotation = Quaternion.identity;
    float tiltTimer = 0f;
    float correctionCooldownTimer = 0f;
    float correctionElapsedTime = 0f;
    public bool IsAutoCorrecting => isAutoCorrecting;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        mainCamera = GetComponentInChildren<Camera>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogWarning("[PlayerUprightAndShortcuts_AutoFallReset] Kameraa ei löytynyt Playerin lapsista eikä Camera.main:sta. useCameraYaw ei toimi.");
        }
    }

    void FixedUpdate()
    {
        if (correctionCooldownTimer > 0f)
        {
            correctionCooldownTimer -= Time.fixedDeltaTime;
        }

        if (!isAutoCorrecting && correctionCooldownTimer <= 0f && IsTiltedBeyondThreshold())
        {
            StartAutoCorrection();
        }

        if (isAutoCorrecting)
        {
            PerformAutoCorrectionStep();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartAutoCorrection(GetLookDirectionRotation());
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            DoTeleportAsT();
        }

        if (transform.position.y < fallYThreshold)
        {
            DoFallSafetyTeleport();
        }
    }

    void DoFallSafetyTeleport()
    {
        Vector3 current = rb.position;
        Vector3 fixedPos = new Vector3(current.x, 0f, current.z);

        rb.position = fixedPos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        tiltTimer = 0f;

        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        Quaternion uprightKeepHeading = flatForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
            : Quaternion.identity;

        StartAutoCorrection(uprightKeepHeading);
    }

    void DoTeleportAsT()
    {
        rb.position = teleportPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        tiltTimer = 0f;

        Quaternion target90 = Quaternion.Euler(0f, 90f, 0f);

        if (mainCamera != null && !mainCamera.transform.IsChildOf(transform))
        {
            Vector3 camEuler = mainCamera.transform.eulerAngles;
            mainCamera.transform.rotation = Quaternion.Euler(camEuler.x, 90f, camEuler.z);
        }

        StartAutoCorrection(target90);
    }

    bool IsTiltedBeyondThreshold()
    {
        float angleFromUp = Vector3.Angle(transform.up, Vector3.up);
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

    void StartAutoCorrection(Quaternion? optionalTarget = null)
    {
        Quaternion target = optionalTarget ?? GetLookDirectionRotation();

        if (correctOnlyYaw)
        {
            float yaw = target.eulerAngles.y;
            target = Quaternion.Euler(0f, yaw, 0f);
        }

        autoTargetRotation = target;
        isAutoCorrecting = true;
        tiltTimer = 0f;
        correctionElapsedTime = 0f;

        rb.angularVelocity = Vector3.zero;
    }

    void PerformAutoCorrectionStep()
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

        Quaternion next = Quaternion.RotateTowards(rb.rotation, autoTargetRotation, maxDelta);

        rb.MoveRotation(next);

        rb.angularVelocity = Vector3.zero;
    }

    Quaternion GetLookDirectionRotation()
    {
        if (useCameraYaw && mainCamera != null)
        {
            float cameraYaw = mainCamera.transform.eulerAngles.y;
            return Quaternion.Euler(0f, cameraYaw, 0f);
        }
        else if (transform.forward != Vector3.zero)
        {
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flatForward.sqrMagnitude < 0.0001f)
                return rb.rotation;
            return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }
        else
        {
            return rb.rotation;
        }
    }
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        if (isAutoCorrecting)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + autoTargetRotation * Vector3.forward * 2f);
        }

        Gizmos.color = Color.yellow;
        Vector3 from = new Vector3(transform.position.x - 2f, fallYThreshold, transform.position.z - 2f);
        Vector3 to   = new Vector3(transform.position.x + 2f, fallYThreshold, transform.position.z + 2f);
        Gizmos.DrawLine(from, to);
    }
}