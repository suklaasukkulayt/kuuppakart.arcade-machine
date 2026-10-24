using UnityEngine;

public class LookBackCamera : MonoBehaviour
{
    public Transform carCamera;

    public KeyCode lookBackKey = KeyCode.V;

    [Header("Look back camera settings")]
    public bool autoMirrorPosition = true;

    public Vector3 rearViewLocalPosition;

    public float rearViewPitchOffset = 10f;

    public Vector3 firstPersonLocalPosition = new Vector3(0f, 0.9f, 0.6f);

    public Vector3 firstPersonLocalEulerAngles = Vector3.zero;

    private Vector3 forwardPosition;
    private Quaternion forwardRotation;
    private Vector3 computedRearPosition;
    private Quaternion computedRearRotation;
    private bool lookingBack = false;

    void Start()
    {
        if (carCamera == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    carCamera = cam.transform;
                }
            }
        }

        if (carCamera == null)
        {
            Debug.LogWarning("[LookBackCamera] Car Camera -viittaus puuttuu. Aseta se Inspectorissa.");
            return;
        }

        if (DifficultyManager.Instance != null)
        {
            Camera cam = carCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = DifficultyManager.Instance.SelectedFov;
            }

            if (DifficultyManager.Instance.FirstPerson)
            {
                carCamera.localPosition = firstPersonLocalPosition;
                carCamera.localEulerAngles = firstPersonLocalEulerAngles;
            }
        }

        forwardPosition = carCamera.localPosition;
        forwardRotation = carCamera.localRotation;
        
        Vector3 basePos = autoMirrorPosition
            ? new Vector3(forwardPosition.x, forwardPosition.y, -forwardPosition.z)
            : rearViewLocalPosition;

        computedRearPosition = basePos;
        computedRearRotation = forwardRotation * Quaternion.Euler(rearViewPitchOffset, 180f, 0f);
    }

    void Update()
    {
        if (carCamera == null) return;

        if (Input.GetKeyDown(lookBackKey)) LookBack();
        if (Input.GetKeyUp(lookBackKey)) LookForward();
    }

    private void LookBack()
    {
        if (lookingBack) return;
        lookingBack = true;
        carCamera.localPosition = computedRearPosition;
        carCamera.localRotation = computedRearRotation;
    }

    private void LookForward()
    {
        if (!lookingBack) return;
        lookingBack = false;
        carCamera.localPosition = forwardPosition;
        carCamera.localRotation = forwardRotation;
    }
}