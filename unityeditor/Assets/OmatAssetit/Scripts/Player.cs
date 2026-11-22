using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 10f;
    public float turnSpeed = 130f;

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
    [Range(0f, 0.5f)] public float slopeGravityFactor = 0.4f;
    [FormerlySerializedAs("slopeHoldAcceleration")]
    [Min(0f)] public float slopeRollResistance = 0.2f;
    public float maxRollBackSpeed = 8f;
    [Range(0f, 1f)] public float boostSlopeFactor = 0.1f;

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

    private float EffectiveMaxSlopeAngle => Mathf.Clamp(Mathf.Max(maxSlopeAngle, 68f), 0f, 89f);

    public bool IsGrounded { get; private set; }

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
        blockedTimer = 0f;

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

        if (!foundLowSideContact)
            return false;

        if (foundStepEdge)
        {
            lowEdgeContactNormal = selectedNormal;
            lowEdgeStepTargetY = selectedStepY;
            lowEdgeStepUntil = Time.time + 0.45f;
            ignoreBlockingUntil = Mathf.Max(ignoreBlockingUntil, Time.time + Mathf.Max(0.1f, blockedTimeLimit));
        }

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
            return;
        }

        bool hasGroundContact = groundContactCount > 0;
        Vector3 contactGroundNormal = hasGroundContact ? groundContactNormalSum.normalized : Vector3.up;
        groundContactCount = 0;
        groundContactNormalSum = Vector3.zero;

        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
        float dt = Time.fixedDeltaTime;
        if (crashCooldownTimer > 0f)
            crashCooldownTimer -= dt;

        if (isStunned)
        {
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
            blockedTimer = 0f;
            return;
        }

        float input = controls != null ? controls.MoveVertical : 0f;
        float turn = (controls != null ? controls.MoveHorizontal : 0f) * turnSpeed * dt;
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;

        RaycastHit groundHit = default;
        bool grounded = hasGroundContact && ProbeGroundSurface(flatForward, out groundHit);
        if (grounded)
        {
            groundHit.normal = contactGroundNormal;
            if (Vector3.Dot(rb.linearVelocity, groundHit.normal) > 0.2f)
                grounded = false;
        }

        IsGrounded = grounded;
        bool blockedByWall = IsPushingIntoWall(input, flatForward, out Vector3 blockingWallNormal);
        if (blockedByWall)
        {
            forwardSpeed = 0f;
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
            surfaceForward = Vector3.ProjectOnPlane(flatForward, groundHit.normal);
            if (surfaceForward.sqrMagnitude < 0.0001f)
                surfaceForward = flatForward;
            surfaceForward.Normalize();

            float gravityFactor = Mathf.Clamp(slopeGravityFactor, 0f, 0.25f);
            slopeAccel = Vector3.Dot(Physics.gravity, surfaceForward) * gravityFactor;
            if (isBoosted)
                slopeAccel *= boostSlopeFactor;
        }

        if (blockedByWall)
            forwardSpeed = 0f;
        else
            UpdateForwardSpeed(input, dt, slopeAccel, grounded);

        Vector3 desired = surfaceForward * forwardSpeed;
        if (grounded)
        {
            rb.linearVelocity = desired;
        }
        else
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = desired.x;
            velocity.z = desired.z;
            rb.linearVelocity = velocity;
        }
        ApplyStepAssist(input, flatForward);

        intendedVelocity = desired;
        commandedXZ = new Vector3(desired.x, 0f, desired.z);

        Quaternion rot = rb.rotation * Quaternion.Euler(0f, turn, 0f);
        if (grounded)
        {
            Quaternion aligned = Quaternion.FromToRotation(rot * Vector3.up, groundHit.normal) * rot;
            rot = Quaternion.Slerp(rot, aligned, 1f - Mathf.Exp(-alignSharpness * dt));
            rb.angularVelocity = Vector3.zero;
        }
        rb.MoveRotation(rot);
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

    private void UpdateForwardSpeed(float input, float dt, float slopeAccel, bool grounded)
    {
        float maxReverse = currentMaxSpeed * reverseSpeedFactor;
        float accel = isBoosted ? boostAcceleration : acceleration;
        float uphillMultiplier = grounded && input > 0.1f && slopeAccel < -0.05f ? 1.25f : 1f;
        accel *= uphillMultiplier;
        float reverseAccel = isBoosted ? boostAcceleration : reverseAcceleration;

        if (input > 0.1f)
        {
            if (forwardSpeed < 0f)
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * dt);
            else if (forwardSpeed < currentMaxSpeed)
            {
                float t = Mathf.Clamp01(forwardSpeed / Mathf.Max(0.01f, currentMaxSpeed));
                float a = accel * Mathf.Lerp(1f, topSpeedAccelerationFactor, t * t);
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, currentMaxSpeed, a * dt);
            }
        }
        else if (input < -0.1f)
        {
            if (forwardSpeed > 0f)
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * dt);
            else
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, -maxReverse, reverseAccel * dt);
        }
        else
        {
            float coast = grounded && Mathf.Abs(slopeAccel) > 0.05f ? Mathf.Max(0f, slopeRollResistance) : coastDeceleration;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coast * dt);
        }

        if (grounded)
            forwardSpeed += slopeAccel * dt;

        forwardSpeed = Mathf.Clamp(forwardSpeed, -Mathf.Min(maxReverse, maxRollBackSpeed), currentMaxSpeed);
    }
}