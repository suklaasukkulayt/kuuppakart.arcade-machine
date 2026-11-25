using UnityEngine;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerCarStatsApplier : MonoBehaviour
{
    [Header("Car database")]
    [SerializeField] private CarDatabase database;
    [Header("Model replacement")]
    [SerializeField] private Transform visualMount;
    [SerializeField] private GameObject originalVisualRoot;
    [Header("Camera fitting")]
    [SerializeField, Min(0f)] private float cameraRoofClearance = 0.08f;
    [SerializeField, Min(0f)] private float cameraRearClearance = 0.1f;
    [SerializeField] private Vector3 colliderPadding = new Vector3(0.08f, 0.05f, 0.12f);

    private Player player;
    private Rigidbody body;
    private GameObject activeVisual;
    private Bounds originalBounds;
    private Camera[] playerCameras;
    private Vector3[] originalCameraPositions;

    private void Awake()
    {
        player = GetComponent<Player>();
        body = GetComponent<Rigidbody>();

        Transform original = originalVisualRoot != null ? originalVisualRoot.transform : transform;
        bool hasOriginalBounds = GetLocalBounds(original, out originalBounds);

        playerCameras = GetComponentsInChildren<Camera>(true);
        originalCameraPositions = new Vector3[playerCameras.Length];

        for (int i = 0; i < playerCameras.Length; i++)
            originalCameraPositions[i] = transform.InverseTransformPoint(playerCameras[i].transform.position);

        if (database == null || database.Count == 0)
        {
            Debug.LogWarning("PlayerCarStatsApplier: No CarDatabase assigned.", this);
            return;
        }

        string id = PlayerPrefs.GetString(CarSelectionKeys.SelectedCarIdKey, string.Empty);
        CarDefinition car = database.FindById(id);
        if (car == null) car = database.GetCar(0);
        if (car == null) return;

        if (car.applyStatsToPlayer) ApplyStats(car);
        ApplyVisual(car);

        if (hasOriginalBounds && activeVisual != null && GetLocalBounds(activeVisual.transform, out Bounds newBounds))
        {
            FitCameras(newBounds);
            UpdatePlayerCollider(newBounds);
            PlayerCarEffects effects = GetComponent<PlayerCarEffects>();

            if (effects == null)
                effects = gameObject.AddComponent<PlayerCarEffects>();

            effects.Initialize(player, newBounds);
        }
    }

    private void UpdatePlayerCollider(Bounds bounds)
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
        {
            Debug.LogWarning("PlayerCarStatsApplier: Player is missing a BoxCollider.", this);
            return;
        }

        Vector3 padding = Vector3.Max(Vector3.zero, colliderPadding);
        Vector3 size = bounds.size + padding;
        box.center = bounds.center;
        box.size = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), Mathf.Max(0.05f, size.z));
    }

    private void ApplyStats(CarDefinition car)
    {
        player.baseSpeed = car.topSpeed;
        player.acceleration = car.acceleration;
        player.boostSpeed = car.boostMaxSpeed;
        player.boostAcceleration = car.boostAcceleration;
        player.boostDuration = car.boostDuration;
        player.boostCooldown = car.boostCooldown;
        player.turnSpeed = car.turnSpeed;
        player.brakeDeceleration = car.brakePower;
        player.driftMinSpeed = car.driftMinSpeed;
        player.driftTurnThreshold = car.driftTurnThreshold;
        player.driftYawMultiplier = car.driftYawMultiplier;
        player.driftLateralAcceleration = car.driftLateralAcceleration;
        player.driftLateralSpeed = car.driftLateralSpeed;
        player.driftRecoveryGrip = car.driftRecoveryGrip;
        player.driftBodyAngle = car.driftBodyAngle;
        player.driftBodyYawSpeed = car.driftBodyYawSpeed;
        body.mass = car.rigidbodyMass;
    }

    private void ApplyVisual(CarDefinition car)
    {
        if (car.visualPrefab == null) return;
        if (visualMount == null) visualMount = transform;

        HideOriginalVisual();

        activeVisual = Instantiate(car.visualPrefab, visualMount, false);
        activeVisual.name = car.displayName + " Player Model";
        activeVisual.transform.localPosition = car.visualLocalPosition;
        activeVisual.transform.localRotation = Quaternion.Euler(car.visualLocalEulerAngles);
        activeVisual.transform.localScale = car.visualLocalScale * database.GetVisualScaleMultiplier(car);
        MakeVisualOnly(activeVisual);
    }

    private void HideOriginalVisual()
    {
        if (originalVisualRoot == null) return;

        if (originalVisualRoot == gameObject || transform.IsChildOf(originalVisualRoot.transform))
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
        }
        else originalVisualRoot.SetActive(false);
    }

    private bool GetLocalBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds();
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;

            Bounds local = renderer.localBounds;
            if (local.size.sqrMagnitude < 0.00001f) continue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 point = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z
                );

                point = transform.InverseTransformPoint(renderer.transform.TransformPoint(point));

                if (!found)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else bounds.Encapsulate(point);
            }
        }

        return found;
    }

    private void FitCameras(Bounds newBounds)
    {
        Vector3 ratio = new Vector3(
            newBounds.size.x / Mathf.Max(0.01f, originalBounds.size.x),
            newBounds.size.y / Mathf.Max(0.01f, originalBounds.size.y),
            newBounds.size.z / Mathf.Max(0.01f, originalBounds.size.z)
        );

        for (int i = 0; i < playerCameras.Length; i++)
        {
            if (playerCameras[i] == null) continue;

            Vector3 oldPosition = originalCameraPositions[i];
            Vector3 newPosition = ScaleCameraPosition(oldPosition, newBounds, ratio);

            if (oldPosition.y >= originalBounds.center.y)
                newPosition.y = Mathf.Max(newPosition.y, newBounds.max.y + newBounds.size.y * cameraRoofClearance);

            if (oldPosition.z <= originalBounds.center.z)
                newPosition.z = Mathf.Min(newPosition.z, newBounds.min.z - newBounds.size.z * cameraRearClearance);

            playerCameras[i].transform.position = transform.TransformPoint(newPosition);
        }

        LookBackCamera[] lookBacks = GetComponentsInChildren<LookBackCamera>(true);

        foreach (LookBackCamera lookBack in lookBacks)
        {
            Transform cameraParent = lookBack.carCamera != null ? lookBack.carCamera.parent : transform;
            Vector3 oldPosition = transform.InverseTransformPoint(cameraParent.TransformPoint(lookBack.firstPersonLocalPosition));
            Vector3 newPosition = ScaleCameraPosition(oldPosition, newBounds, ratio);
            lookBack.firstPersonLocalPosition = cameraParent.InverseTransformPoint(transform.TransformPoint(newPosition));
        }
    }

    private Vector3 ScaleCameraPosition(Vector3 position, Bounds newBounds, Vector3 ratio)
    {
        return newBounds.center + Vector3.Scale(position - originalBounds.center, ratio);
    }

    private void MakeVisualOnly(GameObject carObject)
    {
        Collider[] colliders = carObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

        Rigidbody[] bodies = carObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }

        MonoBehaviour[] scripts = carObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scripts.Length; i++) scripts[i].enabled = false;

        Camera[] cameras = carObject.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++) cameras[i].enabled = false;

        AudioListener[] listeners = carObject.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++) listeners[i].enabled = false;
    }
}