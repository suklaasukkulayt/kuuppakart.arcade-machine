using UnityEngine;

public class LookBackCamera : MonoBehaviour
{
    public Transform carCamera;

    [Header("Look back camera settings")]
    public bool autoMirrorPosition = true;
    public Vector3 rearViewLocalPosition;
    public float rearViewPitchOffset = 10f;
    public Vector3 firstPersonLocalPosition = new Vector3(0f, 0.9f, 0.15f);
    public Vector3 firstPersonLocalEulerAngles = Vector3.zero;
    public float viewTransitionSpeed = 8f;

    [Header("Driving camera motion")]
    public bool enableDrivingCameraMotion = true;
    public float lateralAccelerationInfluence = 0.012f;
    public float longitudinalAccelerationInfluence = 0.008f;
    public float maximumPositionOffset = 0.12f;
    public float turningRollMultiplier = 2f;
    public float maximumRollAngle = 3f;
    public float accelerationPitchMultiplier = 0.45f;
    public float maximumPitchAngle = 2f;
    public float speedBobAmplitude = 0.012f;
    public float speedBobFrequency = 7f;
    public float speedBobStartSpeed = 5f;

    private Vector3 forwardPosition;
    private Vector3 computedRearPosition;
    private Vector3 forwardOffsetInPlayer;
    private Vector3 rearOffsetInPlayer;
    private Quaternion forwardRotationInPlayer;
    private Quaternion rearRotationInPlayer;
    private bool lookingBack;
    private Transform playerTransform;
    private Rigidbody playerBody;
    private Player playerController;
    private Vector3 lastLocalVelocity;
    private Vector3 cameraPositionVelocity;
    private Vector3 currentMotionOffset;
    private Quaternion currentMotionRotation = Quaternion.identity;
    private KuuppaKartControls controls;

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerBody = player.GetComponent<Rigidbody>();
            playerController = player.GetComponent<Player>();
        }

        if (carCamera == null && player != null)
        {
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam != null)
                carCamera = cam.transform;
        }

        if (carCamera == null)
        {
            Debug.LogWarning("LookBackCamera: Car Camera reference is missing. Assign it in the Inspector.");
            enabled = false;
            return;
        }

        controls = KuuppaKartControls.EnsureInstance();
        if (playerTransform == null)
            playerTransform = carCamera.parent != null ? carCamera.parent : transform;
        if (playerBody == null)
            playerBody = playerTransform.GetComponentInParent<Rigidbody>();
        if (playerController == null)
            playerController = playerTransform.GetComponentInParent<Player>();

        if (DifficultyManager.Instance != null)
        {
            Camera cam = carCamera.GetComponent<Camera>();
            if (cam != null)
                cam.fieldOfView = DifficultyManager.Instance.SelectedFov;
            if (DifficultyManager.Instance.FirstPerson)
            {
                carCamera.localPosition = firstPersonLocalPosition;
                carCamera.localEulerAngles = firstPersonLocalEulerAngles;
            }
        }

        forwardPosition = carCamera.localPosition;
        Vector3 basePos = autoMirrorPosition
            ? new Vector3(forwardPosition.x, forwardPosition.y, -forwardPosition.z)
            : rearViewLocalPosition;
        computedRearPosition = basePos;

        Transform cameraParent = carCamera.parent;
        Vector3 forwardWorldPosition = cameraParent != null
            ? cameraParent.TransformPoint(forwardPosition)
            : forwardPosition;
        Vector3 rearWorldPosition = cameraParent != null
            ? cameraParent.TransformPoint(computedRearPosition)
            : computedRearPosition;

        forwardOffsetInPlayer = playerTransform.InverseTransformPoint(forwardWorldPosition);
        rearOffsetInPlayer = playerTransform.InverseTransformPoint(rearWorldPosition);
        forwardRotationInPlayer = Quaternion.Inverse(playerTransform.rotation) * carCamera.rotation;
        rearRotationInPlayer = forwardRotationInPlayer * Quaternion.Euler(rearViewPitchOffset, 180f, 0f);

        if (playerBody != null)
            lastLocalVelocity = playerTransform.InverseTransformDirection(playerBody.linearVelocity);
    }

    private void Update()
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
        lookingBack = controls.LookBackHeld;
    }

    private void LateUpdate()
    {
        if (carCamera == null || playerTransform == null)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 localAcceleration = Vector3.zero;

        if (playerBody != null)
        {
            Vector3 localVelocity = playerTransform.InverseTransformDirection(playerBody.linearVelocity);
            localAcceleration = (localVelocity - lastLocalVelocity) / dt;
            lastLocalVelocity = localVelocity;
        }

        if (!lookingBack && enableDrivingCameraMotion && playerBody != null)
        {
            float lateralOffset = Mathf.Clamp(-localAcceleration.x * lateralAccelerationInfluence, -maximumPositionOffset, maximumPositionOffset);
            float longitudinalOffset = Mathf.Clamp(-localAcceleration.z * longitudinalAccelerationInfluence, -maximumPositionOffset, maximumPositionOffset);
            float bobBlend = Mathf.InverseLerp(speedBobStartSpeed, speedBobStartSpeed * 2f, playerBody.linearVelocity.magnitude);
            float bob = Mathf.Sin(Time.time * speedBobFrequency) * speedBobAmplitude * bobBlend;
            currentMotionOffset = new Vector3(lateralOffset, bob, longitudinalOffset);
            float roll = Mathf.Clamp(-playerBody.angularVelocity.y * turningRollMultiplier, -maximumRollAngle, maximumRollAngle);
            float pitch = Mathf.Clamp(-localAcceleration.z * accelerationPitchMultiplier, -maximumPitchAngle, maximumPitchAngle);
            currentMotionRotation = Quaternion.Euler(pitch, 0f, roll);
        }
        else
        {
            currentMotionOffset = Vector3.zero;
            currentMotionRotation = Quaternion.identity;
        }

        Quaternion cameraHeading = playerTransform.rotation;
        if (!lookingBack && playerController != null && playerController.IsDrifting && playerBody != null)
        {
            Vector3 movementDirection = Vector3.ProjectOnPlane(playerBody.linearVelocity, playerTransform.up);
            if (movementDirection.sqrMagnitude > 0.25f)
                cameraHeading = Quaternion.LookRotation(movementDirection.normalized, playerTransform.up);
        }

        Vector3 playerLocalOffset = lookingBack ? rearOffsetInPlayer : forwardOffsetInPlayer;
        Quaternion playerRelativeRotation = lookingBack ? rearRotationInPlayer : forwardRotationInPlayer;
        Quaternion headingDelta = cameraHeading * Quaternion.Inverse(playerTransform.rotation);
        Vector3 worldOffset = headingDelta * playerTransform.TransformVector(playerLocalOffset);
        Vector3 targetWorldPosition = playerTransform.position + worldOffset + cameraHeading * currentMotionOffset;
        Quaternion targetWorldRotation = cameraHeading * playerRelativeRotation * currentMotionRotation;

        float smoothTime = 1f / Mathf.Max(1f, viewTransitionSpeed);
        carCamera.position = Vector3.SmoothDamp(carCamera.position, targetWorldPosition, ref cameraPositionVelocity, smoothTime, Mathf.Infinity, dt);
        float rotationBlend = 1f - Mathf.Exp(-Mathf.Max(1f, viewTransitionSpeed) * dt);
        carCamera.rotation = Quaternion.Slerp(carCamera.rotation, targetWorldRotation, rotationBlend);
    }
}
