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

    private Player player;
    private Rigidbody body;
    private GameObject activeVisual;

    private void Awake()
    {
        player = GetComponent<Player>();
        body = GetComponent<Rigidbody>();

        if (database == null || database.Count == 0)
        {
            Debug.LogWarning("PlayerCarStatsApplier: No CarDatabase is assigned. The original car settings will be kept.", this);
            return;
        }

        string selectedId = PlayerPrefs.GetString(CarSelectionKeys.SelectedCarIdKey, string.Empty);
        CarDefinition selectedCar = database.FindById(selectedId);
        if (selectedCar == null)
            selectedCar = database.GetCar(0);

        if (selectedCar == null)
            return;

        if (selectedCar.applyStatsToPlayer)
            ApplyStats(selectedCar);

        ApplyVisual(selectedCar);
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
        if (car.visualPrefab == null)
            return;

        if (visualMount == null)
            visualMount = transform;

        if (originalVisualRoot != null)
            originalVisualRoot.SetActive(false);

        activeVisual = Instantiate(car.visualPrefab, visualMount, false);
        activeVisual.name = car.displayName + " Player Model";
        activeVisual.transform.localPosition = car.visualLocalPosition;
        activeVisual.transform.localRotation = Quaternion.Euler(car.visualLocalEulerAngles);
        activeVisual.transform.localScale = car.visualLocalScale;
        MakeVisualOnly(activeVisual);
    }

    private void MakeVisualOnly(GameObject carObject)
    {
        Collider[] colliders = carObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] bodies = carObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }

        MonoBehaviour[] scripts = carObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scripts.Length; i++)
            scripts[i].enabled = false;

        Camera[] cameras = carObject.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = false;

        AudioListener[] listeners = carObject.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            listeners[i].enabled = false;
    }
}
