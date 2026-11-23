using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 10f;
    public float turnSpeed = 130f;

    [Header("Drifting")]
    [Min(0f)] public float driftMinSpeed = 4f;
    [Range(0f, 1f)] public float driftTurnThreshold = 0.12f;
    [Min(1f)] public float driftYawMultiplier = 1.25f;
    [Min(0f)] public float driftLateralAcceleration = 16f;
    [Min(0f)] public float driftLateralSpeed = 4.5f;
    [Min(0f)] public float driftRecoveryGrip = 9f;
    [Range(0f, 35f)] public float driftBodyAngle = 16f;
    [Min(0f)] public float driftBodyYawSpeed = 120f;

    [Header("Drift exit boost")]
    [Min(0f)] public float driftBoostMinDuration = 0.45f;
    [Min(0f)] public float driftBoostInstantSpeed = 2.5f;
    [Min(0f)] public float driftBoostSpeedBonus = 3f;
    [Min(0f)] public float driftBoostAcceleration = 18f;
    [Min(0f)] public float driftBoostDuration = 1.25f;
    [Min(0f)] public float driftBoostCooldown = 0.65f;

    [Header("Acceleration")]
    public float acceleration = 7f;
    [Range(0.05f, 1f)] public float topSpeedAccelerationFactor = 0.35f;
    public float coastDeceleration = 3.5f;
    public float brakeDeceleration = 18f;
    public float reverseSpeedFactor = 0.4f;
    public float reverseAcceleration = 5f;
    public float overspeedDeceleration = 5f;

    [Header("Difficulty")]
    public float hardModeSpeedMultiplier = 0.9f;

    [Header("Crashing")]
    public float crashMinSpeed = 7f;
    public float crashFullSpeed = 16f;
    public float minStunTime = 0.4f;
    public float maxStunTime = 1.0f;
    public float crashCooldown = 0.5f;
    public float groundNormalY = 0.7f;
    public float bounceFactor = 0.25f;
    public float upFactor = 0.15f;
    public float minSpin = 1.5f;
    public float maxSpin = 6f;
    private RigidbodyConstraints normalConstraints;

    [Header("Boost settings")]
    public float boostSpeed = 15f;
    public float boostAcceleration = 14f;
    public float boostDuration = 1.5f;
    public float boostCooldown = 4.0f;
    public TextMeshProUGUI uiText;
    public float ForwardSpeed => forwardSpeed;
    public float BaseSpeed => baseSpeed;
    public float BoostSpeed => boostSpeed;
    public bool IsBoosted => isBoosted;
    public bool BoostReady => !isBoosted && cooldownTimer <= 0f;
    public float BoostRemaining01 => isBoosted ? Mathf.Clamp01(boostTimer / boostDuration) : 0f;
    public float BoostCooldownProgress01 => BoostReady ? 1f : (isBoosted ? 0f : 1f - Mathf.Clamp01(cooldownTimer / boostCooldown));
    public float BoostCooldownLeft => Mathf.Max(0f, cooldownTimer);
    public bool IsDriftBoosted => driftBoostTimer > 0f;
    public float DriftBoostRemaining01 => EffectiveDriftBoostDuration > 0f ? Mathf.Clamp01(driftBoostTimer / EffectiveDriftBoostDuration) : 0f;

    private float currentMaxSpeed;
    private float boostTimer;
    private float cooldownTimer;
    private bool isBoosted;
    private Rigidbody rb;
    private Collider groundCollider;
    private bool isStunned;
    private float stunTimer;
    private bool writeBoostText = true;
    private float forwardSpeed;
    private PlayerUprightAndShortcuts_AutoFallReset autoCorrector;
    private KuuppaKartControls controls;
    private Vector3 intendedVelocity;
    private Vector3 commandedXZ;
    private Vector3 driftVelocity;
    private float driftYawOffset;
    private float driftRunTimer;
    private float driftBoostTimer;
    private float driftBoostCooldownTimer;
    private bool wasDrifting;
    [Range(0.2f, 0.95f)] public float blockedRatio = 0.7f;
    private float crashCooldownTimer;

    [Header("Ground following")]
    public LayerMask groundMask = ~0;
    public float groundCheckHeight = 1f;
    public float groundSnapDistance = 0.4f;
    public float lookAhead = 1.4f;
    public float maxStepHeight = 0.5f;
    public float maxSlopeAngle = 55f;
    public float alignSharpness = 12f;
    public float maxDepenetrationVelocity = 2f;
    public float groundProbeSideOffset = 0.3f;
    public float stepClimbSpeed = 1.8f;

    [Header("Collision filtering")]
    public float lightObjectMassRatio = 0.25f;
    public float lowObstacleHeight = 0.35f;
    public float blockedTimeLimit = 0.15f;

    [Header("Slopes")]
    [Range(0f, 1.5f)] public float slopeGravityMultiplier = 1f;
    [FormerlySerializedAs("slopeHoldAcceleration")]
    [Min(0f)] public float slopeRollResistance = 0.2f;
    public float maxRollBackSpeed = 8f;
    [Range(0f, 1f)] public float boostSlopeFactor = 0.1f;
    [Min(0f)] public float slopeRestThreshold = 0.5f;
    [Range(0f, 5f)] public float flatSlopeDeadzone = 1.5f;
    [Range(0f, 1f)] public float downhillOverspeedFactor = 0.25f;
    public float overspeedSlopeHold = 2f;
    public float climbAssistReserve = 1.5f;
    [Range(0.05f, 1f)] public float climbAssistSpeedFraction = 0.35f;

    [Header("Ground contact")]
    public float groundGraceTime = 0.08f;
    public float graceSnapDistance = 0.1f;
    public bool frictionlessCollider = true;
    public bool interpolateBody = true;

    [Header("Step up (low obstacles)")]
    public float stepLookAheadTime = 0.15f;
    public float stepUpMaxClimbSpeed = 5f;
    [Range(0.3f, 0.95f)] public float stepFaceMaxNormalY = 0.75f;

    [Header("Car and wall hits")]
    [Range(0.5f, 0.98f)] public float frontalCrashDot = 0.8f;
    public float carHitMinSpeed = 4f;
    public float carBounceFactor = 0.6f;
    public float carUpFactor = 0.4f;
    public float carSpinMultiplier = 1.8f;
    public float wallHardHitSpeed = 10f;
    public float wallExtraBounce = 3f;
    public float wallExtraSpin = 4f;

    private float blockedTimer;
    private float ignoreBlockingUntil;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private readonly RaycastHit[] groundSampleHits = new RaycastHit[5];
    private int groundContactCount;
    private Vector3 groundContactNormalSum;
    private Vector3 wallContactNormal;
    private float wallContactUntil;
    private Vector3 lowEdgeContactNormal;
    private float lowEdgeStepTargetY;
    private float lowEdgeStepUntil;
    private float groundGraceTimer;
    private readonly RaycastHit[] stepHits = new RaycastHit[8];

    private float EffectiveMaxSlopeAngle => Mathf.Clamp(Mathf.Max(maxSlopeAngle, 68f), 0f, 89f);
    private float EffectiveDriftBoostMinDuration => Mathf.Min(driftBoostMinDuration, 0.45f);
    private float EffectiveDriftBoostInstantSpeed => Mathf.Max(driftBoostInstantSpeed, 2.5f);
    private float EffectiveDriftBoostBonus => Mathf.Max(driftBoostSpeedBonus, 3f);
    private float EffectiveDriftBoostAcceleration => Mathf.Max(driftBoostAcceleration, 18f);
    private float EffectiveDriftBoostDuration => Mathf.Max(driftBoostDuration, 1.25f);
    private float DriftBoostStrength => EffectiveDriftBoostDuration > 0f ? Mathf.Clamp01(driftBoostTimer / EffectiveDriftBoostDuration) : 0f;
    private float EffectiveMaxSpeed => currentMaxSpeed + EffectiveDriftBoostBonus * DriftBoostStrength;

    public bool IsGrounded { get; private set; }
    public bool IsDrifting { get; private set; }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        groundCollider = GetComponent<Collider>();
        normalConstraints = rb.constraints;
        autoCorrector = GetComponent<PlayerUprightAndShortcuts_AutoFallReset>();
        controls = KuuppaKartControls.EnsureInstance();

        if (DifficultyManager.Instance != null && DifficultyManager.Instance.SelectedDifficulty == Difficulty.Hard)
            baseSpeed *= hardModeSpeedMultiplier;

        currentMaxSpeed = baseSpeed;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxDepenetrationVelocity = Mathf.Clamp(maxDepenetrationVelocity, 1f, 2f);

        rb.sleepThreshold = 0f;
        if (interpolateBody)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (frictionlessCollider && groundCollider != null)
        {
            groundCollider.sharedMaterial = new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        if (FindFirstObjectByType<Speedometer>() != null)
        {
            writeBoostText = false;
            if (uiText != null)
                uiText.text = string.Empty;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        if (controls.BoostPressed && !isBoosted && cooldownTimer <= 0f)
        {
            isBoosted = true;
            currentMaxSpeed = boostSpeed;
            boostTimer = boostDuration;
        }

        if (isBoosted)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0f)
            {
                isBoosted = false;
                currentMaxSpeed = baseSpeed;
                cooldownTimer = boostCooldown;
            }
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (uiText != null && writeBoostText)
        {
            if (isBoosted)
                uiText.text = "<mark>Boosted!</mark>";
            else if (cooldownTimer > 0f)
                uiText.text = $"<mark>{cooldownTimer:F1}s to the next boost!</mark>";
            else
                uiText.text = "<mark>Boost ready!</mark>";
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        RegisterGroundContact(collision);
        bool stepEdge = !isStunned && SuppressLowEdgeBounce(collision);
        if (stepEdge)
            return;
        if (isStunned || crashCooldownTimer > 0f)
            return;
        RegisterWallContact(collision);

        CarIdentify otherCar = collision.collider.GetComponentInParent<CarIdentify>();
        bool hitCar = otherCar != null;
        Rigidbody otherBody = collision.rigidbody;
        if (otherBody != null && !otherBody.isKinematic && otherBody.mass < rb.mass * lightObjectMassRatio && !hitCar)
            return;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            return;
        flatForward.Normalize();

        Vector3 away = Vector3.zero;
        float bestFrontAlignment = -1f;
        bool foundFrontalContact = false;
        float obstacleHeightLimit = Mathf.Max(0f, lowObstacleHeight);
        float minimumContactHeight = groundCollider != null
            ? groundCollider.bounds.min.y + Mathf.Max(obstacleHeightLimit + 0.2f, 0.5f)
            : rb.position.y + Mathf.Max(obstacleHeightLimit + 0.2f, 0.5f);
        float walkableNormalY = Mathf.Min(groundNormalY, Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad)) - 0.05f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint candidate = collision.GetContact(i);
            Vector3 candidateAway = candidate.normal;
            Vector3 towardPlayer = rb.worldCenterOfMass - candidate.point;
            if (Vector3.Dot(candidateAway, towardPlayer) < 0f)
                candidateAway = -candidateAway;
            if (candidateAway.y >= walkableNormalY || candidate.point.y < minimumContactHeight)
                continue;

            Vector3 candidateAwayFlat = new Vector3(candidateAway.x, 0f, candidateAway.z);
            if (candidateAwayFlat.sqrMagnitude < 0.001f)
                continue;
            candidateAwayFlat.Normalize();
            float frontAlignment = Vector3.Dot(candidateAwayFlat, -flatForward);
            if (frontAlignment < frontalCrashDot)
                continue;
            if (frontAlignment > bestFrontAlignment)
            {
                bestFrontAlignment = frontAlignment;
                away = candidateAwayFlat;
                foundFrontalContact = true;
            }
        }

        if (!foundFrontalContact)
            return;
        if (!hitCar && collision.rigidbody == null && !ConfirmWallAhead(away))
            return;

        Vector3 otherVel = Vector3.zero;
        if (hitCar)
        {
            AICar ai = otherCar.GetComponent<AICar>();
            if (ai != null)
                otherVel = ai.CurrentVelocity;
            else if (otherBody != null)
                otherVel = otherBody.linearVelocity;
        }
        else if (otherBody != null)
        {
            otherVel = otherBody.linearVelocity;
        }

        float intoWall = Mathf.Max(0f, Vector3.Dot(intendedVelocity - otherVel, -away));
        float impactSpeed = Mathf.Max(intoWall, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, away)));
        float minSpeed = hitCar ? carHitMinSpeed : crashMinSpeed;
        if (impactSpeed < minSpeed)
            return;

        float severity = Mathf.InverseLerp(minSpeed, crashFullSpeed, impactSpeed);
        if (autoCorrector != null)
            autoCorrector.Suspended = true;

        rb.constraints = RigidbodyConstraints.None;
        isStunned = true;
        stunTimer = Mathf.Lerp(minStunTime, maxStunTime, severity);
        crashCooldownTimer = stunTimer + crashCooldown;
        forwardSpeed = 0f;
        commandedXZ = Vector3.zero;
        driftVelocity = Vector3.zero;
        driftYawOffset = 0f;
        IsDrifting = false;
        ResetDriftTracking(true);
        blockedTimer = 0f;
        groundGraceTimer = 0f;

        float bounce = impactSpeed * (hitCar ? carBounceFactor : bounceFactor);
        float up = impactSpeed * (hitCar ? carUpFactor : upFactor);
        float spin = Mathf.Lerp(minSpin, maxSpin, severity);
        if (hitCar)
            spin *= carSpinMultiplier;
        else if (impactSpeed >= wallHardHitSpeed)
        {
            bounce += wallExtraBounce * severity;
            spin += wallExtraSpin * severity;
        }

        rb.linearVelocity = away * bounce + Vector3.up * up;
        rb.angularVelocity = Random.onUnitSphere * spin;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        RegisterGroundContact(collision);
        if (isStunned)
            return;

        bool stepEdge = SuppressLowEdgeBounce(collision);
        if (!stepEdge)
            RegisterWallContact(collision);
    }

    private void RegisterGroundContact(Collision collision)
    {
        if (rb == null || groundCollider == null)
            return;
        if (collision.collider.GetComponentInParent<CarIdentify>() != null)
            return;
        if (collision.rigidbody != null)
            return;

        Vector3 downFromCenter = rb.worldCenterOfMass - transform.up * 100f;
        Vector3 lowestPoint = groundCollider.ClosestPoint(downFromCenter);
        float lowestPointHeight = Vector3.Dot(lowestPoint - rb.worldCenterOfMass, transform.up);
        float supportHeight = Mathf.Max(0.15f, Mathf.Min(Mathf.Max(0f, lowObstacleHeight), 0.35f));
        float minGroundNormalY = Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad) - 0.02f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            Vector3 towardPlayer = rb.worldCenterOfMass - contact.point;
            if (Vector3.Dot(normal, towardPlayer) < 0f)
                normal = -normal;

            if (normal.y < minGroundNormalY || Vector3.Dot(transform.up, normal) < 0.2f)
                continue;

            float contactHeight = Vector3.Dot(contact.point - rb.worldCenterOfMass, transform.up);
            if (contactHeight > lowestPointHeight + supportHeight)
                continue;

            groundContactNormalSum += normal;
            groundContactCount++;
        }
    }

    private void RegisterWallContact(Collision collision)
    {
        if (rb == null || collision.collider == null)
            return;
        if (collision.collider.GetComponentInParent<CarIdentify>() != null)
            return;

        Rigidbody otherBody = collision.rigidbody;
        if (otherBody != null && !otherBody.isKinematic && otherBody.mass < rb.mass * lightObjectMassRatio)
            return;

        float walkableNormalY = Mathf.Min(groundNormalY, Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad)) - 0.05f;
        float wallContactMinHeight = groundCollider != null
            ? groundCollider.bounds.min.y + Mathf.Max(0.15f, lowObstacleHeight)
            : rb.position.y;
        Vector3 preferredMotion = new Vector3(intendedVelocity.x, 0f, intendedVelocity.z);
        if (preferredMotion.sqrMagnitude < 0.04f)
            preferredMotion = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        float bestScore = -1f;
        Vector3 bestNormal = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.point.y < wallContactMinHeight)
                continue;

            Vector3 normal = contact.normal;
            if (Vector3.Dot(normal, rb.worldCenterOfMass - contact.point) < 0f)
                normal = -normal;
            if (normal.y >= walkableNormalY)
                continue;

            Vector3 flatNormal = new Vector3(normal.x, 0f, normal.z);
            if (flatNormal.sqrMagnitude < 0.01f)
                continue;
            flatNormal.Normalize();

            float score = preferredMotion.sqrMagnitude > 0.04f
                ? Vector3.Dot(preferredMotion.normalized, -flatNormal)
                : 0f;
            if (score > bestScore)
            {
                bestScore = score;
                bestNormal = flatNormal;
            }
        }

        if (bestNormal.sqrMagnitude < 0.5f)
            return;
        if (TryGetStepSurface(-bestNormal, out _, out _))
            return;

        wallContactNormal = bestNormal;
        wallContactUntil = Time.time + Mathf.Max(0.08f, Time.fixedDeltaTime * 3f);
    }

    private bool IsPushingIntoWall(float input, Vector3 flatForward, out Vector3 wallNormal)
    {
        wallNormal = wallContactNormal;
        if (Time.time > wallContactUntil || wallContactNormal.sqrMagnitude < 0.5f)
            return false;

        Vector3 intoWall = -wallContactNormal;
        bool inputIntoWall = Mathf.Abs(input) > 0.1f &&
            Vector3.Dot(flatForward * Mathf.Sign(input), intoWall) > 0.35f;
        bool speedIntoWall = Mathf.Abs(forwardSpeed) > 0.2f &&
            Vector3.Dot(flatForward * Mathf.Sign(forwardSpeed), intoWall) > 0.35f;

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        bool velocityIntoWall = Vector3.Dot(horizontalVelocity, intoWall) > 0.15f;
        return inputIntoWall || speedIntoWall || velocityIntoWall;
    }

    private bool SuppressLowEdgeBounce(Collision collision)
    {
        if (rb == null || groundCollider == null)
            return false;
        if (collision.collider.GetComponentInParent<CarIdentify>() != null)
            return false;

        Rigidbody otherBody = collision.rigidbody;
        if (otherBody != null && !otherBody.isKinematic && otherBody.mass < rb.mass * lightObjectMassRatio)
            return false;

        Vector3 downFromCenter = rb.worldCenterOfMass - transform.up * 100f;
        Vector3 lowestPoint = groundCollider.ClosestPoint(downFromCenter);
        float lowestPointHeight = Vector3.Dot(lowestPoint - rb.worldCenterOfMass, transform.up);
        float contactBand = Mathf.Clamp(Mathf.Max(lowObstacleHeight + 0.2f, 0.45f), 0.45f, 0.7f);
        Vector3 motion = new Vector3(intendedVelocity.x, 0f, intendedVelocity.z);
        if (motion.sqrMagnitude < 0.04f)
            motion = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (motion.sqrMagnitude < 0.04f)
            motion = new Vector3(transform.forward.x, 0f, transform.forward.z);

        bool foundLowSideContact = false;
        bool foundStepEdge = false;
        Vector3 selectedNormal = Vector3.zero;
        float selectedStepY = 0f;
        float bestMotionScore = -1f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            float contactHeight = Vector3.Dot(contact.point - rb.worldCenterOfMass, transform.up);
            if (contactHeight > lowestPointHeight + contactBand)
                continue;

            Vector3 normal = contact.normal;
            if (Vector3.Dot(normal, rb.worldCenterOfMass - contact.point) < 0f)
                normal = -normal;
            Vector3 flatNormal = Vector3.ProjectOnPlane(normal, transform.up);
            if (flatNormal.sqrMagnitude < 0.12f)
                continue;
            flatNormal.Normalize();

            Vector3 directionIntoEdge = -flatNormal;
            float motionScore = Vector3.Dot(motion.normalized, directionIntoEdge);
            if (motionScore < 0.1f)
                continue;

            foundLowSideContact = true;
            if (motionScore <= bestMotionScore)
                continue;

            if (TryGetStepSurface(directionIntoEdge, out RaycastHit stepHit, out _))
            {
                foundStepEdge = true;
                bestMotionScore = motionScore;
                selectedNormal = flatNormal;
                selectedStepY = stepHit.point.y;
            }
        }

        if (!foundLowSideContact || !foundStepEdge)
            return false;

        lowEdgeContactNormal = selectedNormal;
        lowEdgeStepTargetY = selectedStepY;
        lowEdgeStepUntil = Time.time + 0.45f;
        ignoreBlockingUntil = Mathf.Max(ignoreBlockingUntil, Time.time + Mathf.Max(0.1f, blockedTimeLimit));

        blockedTimer = 0f;
        Vector3 velocity = rb.linearVelocity;
        if (velocity.y > 0f)
            velocity.y = 0f;
        rb.linearVelocity = velocity;

        Vector3 angular = rb.angularVelocity;
        angular.x = 0f;
        angular.z = 0f;
        rb.angularVelocity = angular;
        return foundStepEdge;
    }

    private bool ConfirmWallAhead(Vector3 away)
    {
        if (groundCollider == null)
            return true;

        Bounds bounds = groundCollider.bounds;
        Vector3 center = rb.worldCenterOfMass;
        Vector3 toward = -away;
        Vector3 side = Vector3.Cross(Vector3.up, away);
        float reach = Mathf.Abs(away.x) * bounds.extents.x + Mathf.Abs(away.z) * bounds.extents.z + 0.6f;
        float walkableNormalY = Mathf.Min(groundNormalY, Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad)) - 0.05f;
        float highY = bounds.min.y + Mathf.Max(lowObstacleHeight, 0.2f) + 0.2f;

        for (int h = 0; h < 2; h++)
        {
            float y = h == 0 ? highY : center.y;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 origin = new Vector3(center.x, y, center.z) + side * (i * 0.4f) + away * 0.5f;
                if (!CastSolid(origin, toward, reach + 0.5f, out RaycastHit hit))
                    continue;
                if (hit.normal.y >= walkableNormalY)
                    continue;
                Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
                if (flatNormal.sqrMagnitude < 0.01f)
                    continue;
                if (Vector3.Dot(flatNormal.normalized, away) > 0.3f)
                    return true;
            }
        }
        return false;
    }

    private bool TryGetStepSurface(Vector3 direction, out RaycastHit hit, out float stepRise)
    {
        hit = default;
        stepRise = 0f;
        if (groundCollider == null || direction.sqrMagnitude < 0.001f)
            return false;

        direction.y = 0f;
        direction.Normalize();
        Bounds bounds = groundCollider.bounds;
        float frontExtent = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
        Vector3 origin = rb.position + direction * (frontExtent + 0.12f)
            + Vector3.up * (groundCheckHeight + Mathf.Max(0.1f, maxStepHeight));
        float rayDistance = groundCheckHeight + Mathf.Max(0.1f, maxStepHeight) + groundSnapDistance + 0.35f;

        if (!ProbeGround(origin, rayDistance, out hit))
            return false;
        if (hit.normal.y < Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad) - 0.02f)
            return false;

        stepRise = hit.point.y - bounds.min.y;
        return stepRise > 0.025f && stepRise <= Mathf.Max(0.1f, maxStepHeight) + 0.05f;
    }

    private bool ApplyStepAssist(float input, Vector3 flatForward)
    {
        if (Time.time > lowEdgeStepUntil)
            return false;
        if (lowEdgeContactNormal.sqrMagnitude < 0.5f)
            return false;

        float directionSign = Mathf.Abs(input) > 0.1f ? Mathf.Sign(input) : Mathf.Sign(forwardSpeed);
        if (Mathf.Abs(directionSign) < 0.5f)
            return false;

        Vector3 driveDirection = flatForward * directionSign;
        if (Vector3.Dot(driveDirection, -lowEdgeContactNormal) < 0.1f)
            return false;

        float remainingRise = lowEdgeStepTargetY - groundCollider.bounds.min.y;
        if (remainingRise <= 0.035f || remainingRise > Mathf.Max(0.1f, maxStepHeight) + 0.05f)
            return false;

        float climbVelocity = Mathf.Clamp(remainingRise * 5f, 0.55f, Mathf.Max(0.6f, stepClimbSpeed));
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, climbVelocity);
        rb.linearVelocity = velocity;
        return true;
    }

    private void FixedUpdate()
    {
        IsGrounded = false;
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
        {
            groundContactCount = 0;
            groundContactNormalSum = Vector3.zero;
            driftVelocity = Vector3.zero;
            IsDrifting = false;
            ResetDriftTracking(true);
            return;
        }

        bool hasGroundContact = groundContactCount > 0;
        Vector3 contactGroundNormal = hasGroundContact ? groundContactNormalSum.normalized : Vector3.up;
        groundContactCount = 0;
        groundContactNormalSum = Vector3.zero;

        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
        float dt = Time.fixedDeltaTime;
        driftBoostTimer = Mathf.Max(0f, driftBoostTimer - dt);
        driftBoostCooldownTimer = Mathf.Max(0f, driftBoostCooldownTimer - dt);
        if (crashCooldownTimer > 0f)
            crashCooldownTimer -= dt;

        if (isStunned)
        {
            ResetDriftTracking(true);
            stunTimer -= dt;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                rb.constraints = normalConstraints;
                if (autoCorrector != null)
                    autoCorrector.Suspended = false;
            }
            return;
        }

        if (autoCorrector != null && autoCorrector.IsAutoCorrecting)
        {
            forwardSpeed = 0f;
            intendedVelocity = Vector3.zero;
            commandedXZ = Vector3.zero;
            driftVelocity = Vector3.zero;
            driftYawOffset = 0f;
            IsDrifting = false;
            ResetDriftTracking(true);
            blockedTimer = 0f;
            return;
        }

        float input = controls != null ? controls.MoveVertical : 0f;
        float steeringInput = controls != null ? controls.MoveHorizontal : 0f;
        float turn = steeringInput * turnSpeed * dt;
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;

        RaycastHit groundHit = default;
        bool probeOk = ProbeGroundSurface(flatForward, out groundHit);
        bool grounded = false;
        Vector3 groundNormal = Vector3.up;
        if (probeOk && hasGroundContact)
        {
            grounded = true;
            groundNormal = contactGroundNormal;
        }
        else if (probeOk && groundGraceTimer > 0f && IsGroundWithinDistance(groundHit, graceSnapDistance))
        {
            grounded = true;
            groundNormal = groundHit.normal;
        }

        Vector3 driveNormal = ApplySlopeDeadzone(groundNormal);
        if (grounded && Vector3.Dot(rb.linearVelocity, driveNormal) > 0.2f)
            grounded = false;

        if (grounded && hasGroundContact)
            groundGraceTimer = groundGraceTime;
        else
            groundGraceTimer = Mathf.Max(0f, groundGraceTimer - dt);

        IsGrounded = grounded;
        bool blockedByWall = IsPushingIntoWall(input, flatForward, out Vector3 blockingWallNormal);
        if (blockedByWall)
        {
            forwardSpeed = 0f;
            driftVelocity = Vector3.zero;
            IsDrifting = false;
            blockedTimer = 0f;
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 intoWall = -blockingWallNormal;
            float intoWallSpeed = Vector3.Dot(horizontalVelocity, intoWall);
            if (intoWallSpeed > 0f)
                horizontalVelocity -= intoWall * intoWallSpeed;
            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;
            rb.linearVelocity = velocity;
        }
        else
        {
            UpdateBlockedSpeed(dt, grounded);
        }

        Vector3 surfaceForward = flatForward;
        float slopeAccel = 0f;
        if (grounded)
        {
            surfaceForward = Vector3.ProjectOnPlane(flatForward, driveNormal);
            if (surfaceForward.sqrMagnitude < 0.0001f)
                surfaceForward = flatForward;
            surfaceForward.Normalize();

            slopeAccel = Vector3.Dot(Physics.gravity, surfaceForward) * Mathf.Max(0f, slopeGravityMultiplier);
            if (isBoosted)
                slopeAccel *= boostSlopeFactor;
        }

        if (blockedByWall)
            forwardSpeed = 0f;
        else
            UpdateForwardSpeed(input, dt, slopeAccel, grounded);

        IsDrifting = !blockedByWall
            && controls != null
            && controls.DriftHeld
            && grounded
            && forwardSpeed >= driftMinSpeed
            && Mathf.Abs(steeringInput) >= driftTurnThreshold;

        if (IsDrifting)
        {
            driftRunTimer += dt;
            wasDrifting = true;
        }
        else if (wasDrifting)
        {
            if (!blockedByWall
                && driftRunTimer >= EffectiveDriftBoostMinDuration
                && driftBoostCooldownTimer <= 0f)
            {
                driftBoostTimer = EffectiveDriftBoostDuration;
                driftBoostCooldownTimer = driftBoostCooldown;
                forwardSpeed = Mathf.Min(
                    forwardSpeed + EffectiveDriftBoostInstantSpeed,
                    currentMaxSpeed + EffectiveDriftBoostBonus
                );
            }

            driftRunTimer = 0f;
            wasDrifting = false;
        }

        if (driftBoostTimer > 0f && !isBoosted && !blockedByWall && input >= -0.1f && forwardSpeed > 0f)
        {
            float driftBoostTarget = currentMaxSpeed + EffectiveDriftBoostBonus * DriftBoostStrength;
            if (forwardSpeed < driftBoostTarget)
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, driftBoostTarget, EffectiveDriftBoostAcceleration * dt);
        }

        Vector3 surfaceRight = Vector3.Cross(driveNormal, surfaceForward);
        if (surfaceRight.sqrMagnitude < 0.0001f)
            surfaceRight = Vector3.Cross(Vector3.up, flatForward);
        surfaceRight.Normalize();

        if (IsDrifting)
        {
            float speedFactor = Mathf.InverseLerp(
                driftMinSpeed,
                Mathf.Max(driftMinSpeed + 0.01f, EffectiveMaxSpeed),
                forwardSpeed
            );
            Vector3 targetDriftVelocity = -surfaceRight
                * (Mathf.Sign(steeringInput) * driftLateralSpeed * speedFactor);
            driftVelocity = Vector3.MoveTowards(
                driftVelocity,
                targetDriftVelocity,
                driftLateralAcceleration * dt
            );
        }
        else
        {
            float recoveryGrip = grounded ? driftRecoveryGrip : driftRecoveryGrip * 0.25f;
            driftVelocity = Vector3.MoveTowards(
                driftVelocity,
                Vector3.zero,
                recoveryGrip * dt
            );
        }

        if (grounded)
            driftVelocity = Vector3.ProjectOnPlane(driftVelocity, driveNormal);

        Vector3 desired = surfaceForward * forwardSpeed + driftVelocity;
        if (grounded)
        {
            float intoGround = Mathf.Min(0f, Vector3.Dot(rb.linearVelocity, driveNormal));
            rb.linearVelocity = desired + driveNormal * intoGround;
        }
        else
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = desired.x;
            velocity.z = desired.z;
            rb.linearVelocity = velocity;
        }
        ApplyStepAssist(input, flatForward);
        ApplyStepUp(input, dt, grounded ? desired.y : 0f);

        intendedVelocity = desired;
        commandedXZ = new Vector3(desired.x, 0f, desired.z);

        float targetDriftYawOffset = IsDrifting ? steeringInput * driftBodyAngle : 0f;
        float previousDriftYawOffset = driftYawOffset;
        driftYawOffset = Mathf.MoveTowards(
            driftYawOffset,
            targetDriftYawOffset,
            driftBodyYawSpeed * dt
        );

        float turnMultiplier = IsDrifting ? driftYawMultiplier : 1f;
        float driftYawDelta = driftYawOffset - previousDriftYawOffset;
        Quaternion rot = rb.rotation * Quaternion.Euler(0f, turn * turnMultiplier + driftYawDelta, 0f);
        if (grounded)
        {
            Quaternion aligned = Quaternion.FromToRotation(rot * Vector3.up, groundNormal) * rot;
            rot = Quaternion.Slerp(rot, aligned, 1f - Mathf.Exp(-alignSharpness * dt));
            rb.angularVelocity = Vector3.zero;
        }
        rb.MoveRotation(rot);
    }

    private Vector3 ApplySlopeDeadzone(Vector3 normal)
    {
        if (flatSlopeDeadzone <= 0f)
            return normal;
        float angle = Vector3.Angle(Vector3.up, normal);
        if (angle <= flatSlopeDeadzone)
            return Vector3.up;
        float effective = (angle - flatSlopeDeadzone) * Mathf.Deg2Rad;
        return Vector3.RotateTowards(Vector3.up, normal, effective, 0f).normalized;
    }

    private bool IsGroundWithinDistance(RaycastHit hit, float maxGap)
    {
        if (groundCollider == null)
            return false;
        Vector3 closestPoint = groundCollider.ClosestPoint(hit.point);
        return Vector3.Distance(hit.point, closestPoint) <= Mathf.Max(0f, maxGap);
    }

    private void ApplyStepUp(float input, float dt, float baseVerticalVelocity)
    {
        float moveSign = Mathf.Abs(forwardSpeed) > 0.2f
            ? Mathf.Sign(forwardSpeed)
            : (Mathf.Abs(input) > 0.1f ? Mathf.Sign(input) : 0f);
        if (moveSign == 0f)
            return;

        if (!DetectStepAhead(moveSign, Mathf.Abs(forwardSpeed), out float rise, out float timeToReach))
            return;

        float climb = Mathf.Clamp(rise / Mathf.Max(timeToReach, 0.06f), 0.4f, Mathf.Max(0.5f, stepUpMaxClimbSpeed));
        climb = Mathf.Min(climb, rise / dt);

        Vector3 velocity = rb.linearVelocity;
        float target = baseVerticalVelocity + climb;
        if (velocity.y < target)
        {
            velocity.y = target;
            rb.linearVelocity = velocity;
        }
        ignoreBlockingUntil = Mathf.Max(ignoreBlockingUntil, Time.time + 0.1f);
    }

    private bool CastSolid(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
    {
        best = default;
        if (distance <= 0.001f)
            return false;
        int count = Physics.RaycastNonAlloc(origin, direction, stepHits, distance, groundMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = stepHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.collider.attachedRigidbody != null)
                continue;
            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit;
                found = true;
            }
        }
        return found;
    }

    private bool DetectStepAhead(float moveSign, float speedAbs, out float rise, out float timeToReach)
    {
        rise = 0f;
        timeToReach = 0f;
        if (groundCollider == null)
            return false;

        Transform t = transform;
        Vector3 dir = t.forward * moveSign;
        Vector3 up = t.up;
        Vector3 right = t.right;

        float halfLength;
        float halfWidth;
        float halfHeight;
        Vector3 center;
        if (groundCollider is BoxCollider box)
        {
            Vector3 scale = t.lossyScale;
            halfLength = box.size.z * 0.5f * Mathf.Abs(scale.z);
            halfWidth = box.size.x * 0.5f * Mathf.Abs(scale.x);
            halfHeight = box.size.y * 0.5f * Mathf.Abs(scale.y);
            center = t.TransformPoint(box.center);
        }
        else
        {
            Bounds b = groundCollider.bounds;
            halfLength = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
            halfWidth = Mathf.Abs(right.x) * b.extents.x + Mathf.Abs(right.z) * b.extents.z;
            halfHeight = b.extents.y;
            center = b.center;
        }

        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude < 0.0001f)
            return false;
        flatDir.Normalize();

        float maxRise = Mathf.Max(0.1f, maxStepHeight);
        float lowHeight = 0.05f;
        float highHeight = maxRise + 0.08f;
        float lead = Mathf.Clamp(speedAbs * Mathf.Max(0.02f, stepLookAheadTime), 0.3f, 2.5f);
        float rayLength = halfLength + lead;
        Vector3 bottomCenter = center - up * halfHeight;

        bool found = false;
        float bestRise = 0f;
        float bestGap = 0f;
        for (int i = 0; i < 3; i++)
        {
            float lateral = (i == 0 ? 0f : (i == 1 ? 1f : -1f)) * halfWidth * 0.85f;
            Vector3 basePoint = bottomCenter + right * lateral;

            if (!CastSolid(basePoint + up * lowHeight, dir, rayLength, out RaycastHit low))
                continue;
            if (low.normal.y > stepFaceMaxNormalY)
                continue;

            Vector3 highOrigin = basePoint + up * highHeight;
            if (CastSolid(highOrigin, dir, low.distance + 0.3f, out _))
                continue;

            float topStartY = (highOrigin + dir * low.distance).y + 0.1f;
            Vector3 topOrigin = low.point + flatDir * 0.12f;
            topOrigin.y = topStartY;
            if (!CastSolid(topOrigin, Vector3.down, topStartY - low.point.y + 0.05f, out RaycastHit top))
                continue;
            if (top.normal.y < 0.5f)
                continue;

            float bottomAtHit = (basePoint + dir * low.distance).y;
            float needed = top.point.y + 0.02f - bottomAtHit;
            if (needed <= 0.02f || needed > maxRise + 0.05f)
                continue;

            if (!found || needed > bestRise)
            {
                found = true;
                bestRise = needed;
                bestGap = Mathf.Max(0f, low.distance - halfLength);
            }
        }

        if (!found)
            return false;

        rise = bestRise;
        timeToReach = bestGap / Mathf.Max(speedAbs, 0.5f);
        return true;
    }

    private bool ProbeGroundSurface(Vector3 flatForward, out RaycastHit bestHit)
    {
        bestHit = default;
        int count = 0;
        float forwardOffset = Mathf.Clamp(lookAhead * 0.45f, 0.25f, 0.9f);
        Vector3 flatRight = new Vector3(transform.right.x, 0f, transform.right.z);
        flatRight = flatRight.sqrMagnitude > 0.0001f ? flatRight.normalized : Vector3.right;
        float rayDistance = groundCheckHeight + groundSnapDistance + Mathf.Max(0f, maxStepHeight) + 0.35f;

        CollectGroundSample(Vector3.zero, rayDistance, ref count);
        CollectGroundSample(flatForward * forwardOffset, rayDistance, ref count);
        CollectGroundSample(-flatForward * forwardOffset, rayDistance, ref count);
        CollectGroundSample(flatRight * groundProbeSideOffset, rayDistance, ref count);
        CollectGroundSample(-flatRight * groundProbeSideOffset, rayDistance, ref count);

        if (count == 0)
            return false;

        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (groundSampleHits[i].distance < nearestDistance)
            {
                nearestDistance = groundSampleHits[i].distance;
                bestHit = groundSampleHits[i];
            }
        }

        Vector3 normalSum = Vector3.zero;
        int normalCount = 0;
        float normalRange = Mathf.Max(0.15f, Mathf.Max(0f, maxStepHeight) * 0.5f);
        for (int i = 0; i < count; i++)
        {
            RaycastHit sample = groundSampleHits[i];
            if (sample.distance > nearestDistance + normalRange)
                continue;
            if (Mathf.Abs(sample.point.y - bestHit.point.y) > Mathf.Max(0.2f, maxStepHeight))
                continue;
            normalSum += sample.normal;
            normalCount++;
        }

        if (normalCount > 0 && normalSum.sqrMagnitude > 0.0001f)
            bestHit.normal = normalSum.normalized;

        return bestHit.normal.y >= Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad) - 0.02f;
    }

    private void CollectGroundSample(Vector3 offset, float distance, ref int count)
    {
        if (count >= groundSampleHits.Length)
            return;

        Vector3 origin = rb.position + offset + Vector3.up * groundCheckHeight;
        if (!ProbeGround(origin, distance, out RaycastHit hit))
            return;
        if (!IsGroundWithinSnapDistance(hit))
            return;
        if (hit.normal.y < Mathf.Cos(EffectiveMaxSlopeAngle * Mathf.Deg2Rad) - 0.02f)
            return;

        groundSampleHits[count++] = hit;
    }

    private bool IsGroundWithinSnapDistance(RaycastHit hit)
    {
        if (groundCollider == null)
            return true;
        Vector3 closestPoint = groundCollider.ClosestPoint(hit.point);
        float separation = Vector3.Distance(hit.point, closestPoint);
        return separation <= Mathf.Max(0f, groundSnapDistance);
    }

    private void UpdateBlockedSpeed(float dt, bool grounded)
    {
        if (!grounded || Time.time < ignoreBlockingUntil || commandedXZ.sqrMagnitude < 1f)
        {
            blockedTimer = 0f;
            return;
        }

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float commandedMagnitude = commandedXZ.magnitude;
        float actualAlongCommand = Vector3.Dot(horizontalVelocity, commandedXZ / commandedMagnitude);
        float ratio = actualAlongCommand / commandedMagnitude;
        if (ratio < blockedRatio)
        {
            blockedTimer += dt;
            if (blockedTimer >= blockedTimeLimit)
            {
                forwardSpeed *= Mathf.Clamp01(ratio);
                blockedTimer = 0f;
            }
        }
        else
        {
            blockedTimer = 0f;
        }
    }

    private bool ProbeGround(Vector3 origin, float distance, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, groundMask, QueryTriggerInteraction.Ignore);
        best = default;
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.collider.attachedRigidbody != null)
                continue;
            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit;
                found = true;
            }
        }
        return found;
    }

    private void ResetDriftTracking(bool cancelBoost)
    {
        driftRunTimer = 0f;
        wasDrifting = false;
        if (cancelBoost)
            driftBoostTimer = 0f;
    }

    private float ThrottleAcceleration(float speed, float baseAccel, float slopeAccel, bool grounded)
    {
        float maxSpeed = EffectiveMaxSpeed;
        float t = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed));
        float a = baseAccel * Mathf.Lerp(1f, topSpeedAccelerationFactor, t * t);

        if (grounded && slopeAccel < 0f)
        {
            float lowSpeed = Mathf.Clamp01(1f - speed / Mathf.Max(0.01f, maxSpeed * climbAssistSpeedFraction));
            float needed = -slopeAccel + climbAssistReserve;
            a = Mathf.Lerp(a, Mathf.Max(a, needed), lowSpeed);
        }
        return a;
    }

    private void UpdateForwardSpeed(float input, float dt, float slopeAccel, bool grounded)
    {
        float maxSpeed = EffectiveMaxSpeed;
        float maxReverse = maxSpeed * reverseSpeedFactor;
        float accel = isBoosted ? boostAcceleration : driftBoostTimer > 0f ? EffectiveDriftBoostAcceleration : acceleration;
        float reverseAccel = isBoosted ? boostAcceleration : driftBoostTimer > 0f ? EffectiveDriftBoostAcceleration : reverseAcceleration;
        float brake = Mathf.Max(0.01f, brakeDeceleration);
        float v = forwardSpeed;

        if (input > 0.1f)
        {
            if (v < 0f)
            {
                float brakeStep = brake * dt;
                if (v + brakeStep < 0f)
                    v += brakeStep;
                else
                {
                    float leftover = Mathf.Max(0f, dt - (-v / brake));
                    v = ThrottleAcceleration(0f, accel, slopeAccel, grounded) * leftover;
                }
            }
            else if (v < maxSpeed)
            {
                v = Mathf.Min(v + ThrottleAcceleration(v, accel, slopeAccel, grounded) * dt, maxSpeed);
            }
        }
        else if (input < -0.1f)
        {
            if (v > 0f)
            {
                float brakeStep = brake * dt;
                if (v - brakeStep > 0f)
                    v -= brakeStep;
                else
                {
                    float leftover = Mathf.Max(0f, dt - (v / brake));
                    v = -reverseAccel * leftover;
                }
            }
            else if (v > -maxReverse)
            {
                v = Mathf.Max(v - reverseAccel * dt, -maxReverse);
            }
        }
        else
        {
            float slopeBlend = grounded ? Mathf.Clamp01(Mathf.Abs(slopeAccel) / 1.5f) : 0f;
            float coast = Mathf.Lerp(coastDeceleration, Mathf.Max(0f, slopeRollResistance), slopeBlend);
            v = Mathf.MoveTowards(v, 0f, coast * dt);
        }

        if (grounded)
        {
            bool idle = Mathf.Abs(input) <= 0.1f;
            if (idle && Mathf.Abs(v) < 0.15f && Mathf.Abs(slopeAccel) < slopeRestThreshold)
                v = 0f;
            else
                v += slopeAccel * dt;
        }

        if (v > maxSpeed)
        {
            bool downhillPull = grounded && slopeAccel > overspeedSlopeHold;
            if (!downhillPull)
                v = Mathf.MoveTowards(v, maxSpeed, overspeedDeceleration * dt);
            v = Mathf.Min(v, maxSpeed * (1f + Mathf.Max(0f, downhillOverspeedFactor)));
        }

        v = Mathf.Max(v, -Mathf.Max(maxRollBackSpeed, maxReverse));
        forwardSpeed = v;
    }
}