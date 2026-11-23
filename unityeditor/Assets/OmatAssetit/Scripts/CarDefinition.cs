using UnityEngine;

[CreateAssetMenu(fileName = "NewCar", menuName = "KuuppaKart/Cars/Car Definition")]
public class CarDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "new-car";
    public string displayName = "New Car";
    [TextArea(2, 5)] public string description;

    [Header("Visual model")]
    public GameObject visualPrefab;
    public Vector3 visualLocalPosition = Vector3.zero;
    public Vector3 visualLocalEulerAngles = Vector3.zero;
    public Vector3 visualLocalScale = Vector3.one;

    [Header("Player settings")]
    public bool applyStatsToPlayer = true;

    [Header("Performance")]
    [Min(0.1f)] public float topSpeed = 10f;
    [Min(0.1f)] public float acceleration = 7f;
    [Min(0.1f)] public float boostMaxSpeed = 15f;
    [Min(0.1f)] public float boostAcceleration = 14f;
    [Min(0.1f)] public float boostDuration = 1.5f;
    [Min(0f)] public float boostCooldown = 4f;
    [Min(0.1f)] public float turnSpeed = 130f;
    [Min(0.1f)] public float brakePower = 18f;

    [Header("Drifting")]
    [Min(0f)] public float driftMinSpeed = 4f;
    [Range(0f, 1f)] public float driftTurnThreshold = 0.12f;
    [Min(1f)] public float driftYawMultiplier = 1.25f;
    [Min(0f)] public float driftLateralAcceleration = 16f;
    [Min(0f)] public float driftLateralSpeed = 4.5f;
    [Min(0f)] public float driftRecoveryGrip = 9f;
    [Range(0f, 35f)] public float driftBodyAngle = 16f;
    [Min(0f)] public float driftBodyYawSpeed = 120f;

    [Header("Physics")]
    [Min(0.01f)] public float rigidbodyMass = 1f;

    public string StableId
    {
        get
        {
            if (!string.IsNullOrEmpty(id))
                return id;
            return name;
        }
    }
}
