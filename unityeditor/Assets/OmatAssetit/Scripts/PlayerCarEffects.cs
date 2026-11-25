using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Player))]
public class PlayerCarEffects : MonoBehaviour
{
    [Header("Tire marks")]
    [SerializeField] private float normalTrailWidth = 0.035f;
    [SerializeField] private float driftTrailWidth = 0.10f;
    [SerializeField] private float trailLifetime = 3.2f;
    [SerializeField] private float minimumTrailSpeed = 2.5f;

    [Header("Exhaust")]
    [SerializeField] private float exhaustRate = 3.5f;
    [SerializeField] private float boostedExhaustRate = 7f;

    private Player player;
    private Rigidbody body;
    private Bounds carBounds;
    private TrailRenderer leftTrail;
    private TrailRenderer rightTrail;
    private Vector3 leftWheelPosition;
    private Vector3 rightWheelPosition;
    private ParticleSystem exhaust;
    private Material trailMaterial;
    private Material exhaustMaterial;

    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private bool initialized;

    private void Awake()
    {
        player = GetComponent<Player>();
        body = GetComponent<Rigidbody>();
    }

    public void Initialize(Player targetPlayer, Bounds bounds)
    {
        if (targetPlayer == null || bounds.size.sqrMagnitude < 0.001f)
            return;

        player = targetPlayer;
        if (body == null)
            body = GetComponent<Rigidbody>();

        carBounds = bounds;

        CreateMaterials();

        float tireOffset = Mathf.Max(bounds.extents.x * 0.68f, bounds.size.x * 0.28f);
        float tireZ = bounds.min.z + bounds.size.z * 0.24f;
        float tireY = bounds.min.y + Mathf.Max(0.08f, bounds.size.y * 0.12f);

        leftWheelPosition = new Vector3(bounds.center.x - tireOffset, tireY, tireZ);
        rightWheelPosition = new Vector3(bounds.center.x + tireOffset, tireY, tireZ);

        leftTrail = CreateTrail("Left Tire Marks", leftWheelPosition);
        rightTrail = CreateTrail("Right Tire Marks", rightWheelPosition);

        CreateExhaust();

        initialized = true;
    }

    private void CreateMaterials()
    {
        Shader trailShader = Shader.Find("Sprites/Default");

        if (trailShader == null)
            trailShader = Shader.Find("Unlit/Transparent");

        if (trailShader != null)
            trailMaterial = new Material(trailShader);

        Shader exhaustShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (exhaustShader == null)
            exhaustShader = Shader.Find("Particles/Standard Unlit");

        if (exhaustShader == null)
            exhaustShader = Shader.Find("Sprites/Default");

        if (exhaustShader != null)
            exhaustMaterial = new Material(exhaustShader);
    }

    private TrailRenderer CreateTrail(string trailName, Vector3 localPosition)
    {
        GameObject obj = new GameObject(trailName);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = localPosition;

        TrailRenderer trail = obj.AddComponent<TrailRenderer>();

        trail.time = trailLifetime;
        trail.startWidth = normalTrailWidth;
        trail.endWidth = normalTrailWidth;
        trail.minVertexDistance = 0.045f;
        trail.alignment = LineAlignment.TransformZ;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.textureMode = LineTextureMode.Stretch;

        if (trailMaterial != null)
            trail.sharedMaterial = trailMaterial;

        return trail;
    }

    private void CreateExhaust()
    {
        GameObject obj = new GameObject("Exhaust Smoke");
        obj.transform.SetParent(transform, false);

        obj.transform.localPosition = new Vector3(carBounds.center.x, carBounds.min.y + Mathf.Max(0.08f, carBounds.size.y * 0.17f), carBounds.min.z - carBounds.size.z * 0.025f);

        obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        exhaust = obj.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = exhaust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = Color.white;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = exhaust.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = exhaust.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.025f;

        ParticleSystem.VelocityOverLifetimeModule velocity = exhaust.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = 0.12f;
        velocity.z = 0.15f;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.18f, 0.18f, 0.18f), 0f),
                new GradientColorKey(new Color(0.30f, 0.30f, 0.30f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.24f, 0f),
                new GradientAlphaKey(0.10f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        ParticleSystem.ColorOverLifetimeModule color = exhaust.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
        ParticleSystem.SizeOverLifetimeModule size = exhaust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.75f, 1f, 1.6f));
        ParticleSystemRenderer particleRenderer = obj.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (exhaustMaterial != null)
            particleRenderer.sharedMaterial = exhaustMaterial;
    }

    private void LateUpdate()
    {
        if (!initialized || player == null)
            return;

        bool racing = GameManager.Instance != null && GameManager.Instance.Phase == RacePhase.Racing;
        bool moving = Mathf.Abs(player.ForwardSpeed) >= minimumTrailSpeed;
        bool canMark = racing && player.IsGrounded && moving;
        bool drifting = player.IsDrifting;
        UpdateTrail(leftTrail, leftWheelPosition, canMark, drifting);
        UpdateTrail(rightTrail, rightWheelPosition, canMark, drifting);

        ParticleSystem.EmissionModule emission = exhaust.emission;
        emission.rateOverTime = racing ? (player.IsBoosted ? boostedExhaustRate : exhaustRate) : 0f;

        if (racing && !exhaust.isPlaying)
            exhaust.Play();
    }

    private void UpdateTrail(TrailRenderer trail, Vector3 localPosition, bool shouldEmit, bool drifting){
        if (trail == null)
            return;

        if (!shouldEmit)
        {
            SetTrailActive(trail, false);
            return;
        }

        Vector3 wheelPosition = transform.TransformPoint(localPosition);
        Vector3 rayOrigin = wheelPosition + Vector3.up * Mathf.Max(1f, carBounds.size.y + 0.8f);
        float rayDistance = Mathf.Max(2f, carBounds.size.y + 2f);
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, groundHits, rayDistance, player.groundMask, QueryTriggerInteraction.Ignore);
        bool foundGround = false;
        float nearestDistance = float.MaxValue;
        RaycastHit bestHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.attachedRigidbody == body ||
                hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y < 0.35f)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                bestHit = hit;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            SetTrailActive(trail, false);
            return;
        }

        trail.transform.position = bestHit.point + bestHit.normal * 0.018f;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, bestHit.normal);

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(Vector3.forward, bestHit.normal);

        trail.transform.rotation = Quaternion.LookRotation(bestHit.normal, forward.normalized);
        float width = drifting ? driftTrailWidth : normalTrailWidth;
        trail.startWidth = width;
        trail.endWidth = width;
        Color markColor = drifting ? new Color(0.01f, 0.01f, 0.01f, 0.95f) : new Color(0.035f, 0.035f, 0.035f, 0.70f);
        trail.startColor = new Color(markColor.r, markColor.g, markColor.b, 0f);
        trail.endColor = markColor;

        SetTrailActive(trail, true);
    }

    private void SetTrailActive(TrailRenderer trail, bool active)
    {
        if (trail.emitting == active)
            return;

        trail.emitting = active;

        if (active)
            trail.Clear();
    }

    private void OnDestroy()
    {
        if (trailMaterial != null)
            Destroy(trailMaterial);

        if (exhaustMaterial != null)
            Destroy(exhaustMaterial);
    }
}