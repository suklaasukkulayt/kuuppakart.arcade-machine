using UnityEngine;
using TMPro;

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
    private float boostTimer = 0f;
    private float cooldownTimer = 0f;
    private bool isBoosted = false;
    private Rigidbody rb;
    private Collider groundCollider;
    private bool isStunned = false;
    private float stunTimer = 0f;
    private bool writeBoostText = true;

    private float forwardSpeed = 0f;
    private PlayerUprightAndShortcuts_AutoFallReset autoCorrector;

    private Vector3 intendedVelocity;
    private Vector3 commandedXZ;
    [Range(0.2f, 0.95f)] public float blockedRatio = 0.7f;
    private float crashCooldownTimer = 0f;

    [Header("Ground following")]
    public LayerMask groundMask = ~0;
    public float groundCheckHeight = 1f;
    public float groundSnapDistance = 0.4f;
    public float lookAhead = 1.4f;
    public float maxStepHeight = 0.5f;
    public float maxSlopeAngle = 55f;
    public float alignSharpness = 12f;
    public float maxDepenetrationVelocity = 3f;

    [Header("Collision filtering")]
    public float lightObjectMassRatio = 0.25f;
    public float lowObstacleHeight = 0.35f;
    public float blockedTimeLimit = 0.15f;

    [Header("Slopes")]
    public float slopeGravityFactor = 1f;
    public float slopeHoldAcceleration = 2f;
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

    private float blockedTimer = 0f;
    private float ignoreBlockingUntil = 0f;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        groundCollider = GetComponent<Collider>();
        normalConstraints = rb.constraints;
        autoCorrector = GetComponent<PlayerUprightAndShortcuts_AutoFallReset>();

        if (DifficultyManager.Instance != null && DifficultyManager.Instance.SelectedDifficulty == Difficulty.Hard)
        {
            baseSpeed *= hardModeSpeedMultiplier;
        }

        currentMaxSpeed = baseSpeed;

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxDepenetrationVelocity = maxDepenetrationVelocity;

        if (FindFirstObjectByType<Speedometer>() != null)
        {
            writeBoostText = false;
            if (uiText != null)
                uiText.text = "";
        }
    }

    void Update()
    {
        if (GameManager.Instance.Phase != RacePhase.Racing)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.B) && !isBoosted && cooldownTimer <= 0f)
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
            {
                uiText.text = "<mark>Boosted!</mark>";
            }
            else if (cooldownTimer > 0f)
            {
                uiText.text = $"<mark>{cooldownTimer:F1}s to the next boost!</mark>";
            }
            else
            {
                uiText.text = "<mark>Boost ready!</mark>";
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        if (!isStunned)
            SuppressLowEdgeBounce(collision);

        if (isStunned || crashCooldownTimer > 0f)
            return;

        CarIdentify otherCar = collision.collider.GetComponentInParent<CarIdentify>();
        bool hitCar = otherCar != null;

        Rigidbody otherBody = collision.rigidbody;
        if (otherBody != null
            && !otherBody.isKinematic
            && otherBody.mass < rb.mass * lightObjectMassRatio
            && !hitCar)
        {
            return;
        }

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            return;
        flatForward.Normalize();

        ContactPoint contact = default;
        Vector3 away = Vector3.zero;
        float bestFrontAlignment = -1f;
        bool foundFrontalContact = false;
        float obstacleHeightLimit = Mathf.Max(0f, lowObstacleHeight);
        float minimumContactHeight = groundCollider != null ? groundCollider.bounds.min.y + obstacleHeightLimit : rb.position.y + obstacleHeightLimit;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint candidate = collision.GetContact(i);
            Vector3 candidateAway = candidate.normal;

            Vector3 towardPlayer = rb.worldCenterOfMass - candidate.point;
            if (Vector3.Dot(candidateAway, towardPlayer) < 0f)
                candidateAway = -candidateAway;

            if (candidateAway.y > groundNormalY)
                continue;

            if (candidate.point.y < minimumContactHeight)
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
                contact = candidate;
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
        {
            spin *= carSpinMultiplier;
        }
        else if (impactSpeed >= wallHardHitSpeed)
        {
            bounce += wallExtraBounce * severity;
            spin += wallExtraSpin * severity;
        }

        rb.linearVelocity = away * bounce + Vector3.up * up;
        rb.angularVelocity = Random.onUnitSphere * spin;
    }

    void OnCollisionStay(Collision collision)
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing || isStunned)
            return;

        SuppressLowEdgeBounce(collision);
    }

    void SuppressLowEdgeBounce(Collision collision)
    {
        if (rb == null || groundCollider == null)
            return;

        if (collision.collider.GetComponentInParent<CarIdentify>() != null)
            return;

        Rigidbody otherBody = collision.rigidbody;
        if (otherBody != null && !otherBody.isKinematic
            && otherBody.mass < rb.mass * lightObjectMassRatio)
            return;

        float lowerBodyTop = groundCollider.bounds.min.y + Mathf.Max(0f, lowObstacleHeight);

        bool lowSideContact = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.point.y > lowerBodyTop)
                continue;

            Vector3 normalTowardPlayer = contact.normal;
            if (Vector3.Dot(normalTowardPlayer, rb.worldCenterOfMass - contact.point) < 0f)
                normalTowardPlayer = -normalTowardPlayer;

            if (normalTowardPlayer.y > groundNormalY)
                continue;

            lowSideContact = true;
            break;
        }

        if (!lowSideContact)
            return;
        
        ignoreBlockingUntil = Mathf.Max(ignoreBlockingUntil, Time.time + Mathf.Max(0f, blockedTimeLimit));
        blockedTimer = 0f;
        Vector3 velocity = rb.linearVelocity;
        if (velocity.y > 0f)
        {
            velocity.y = 0f;
            rb.linearVelocity = velocity;
        }

        Vector3 angular = rb.angularVelocity;
        angular.x = 0f;
        angular.z = 0f;
        rb.angularVelocity = angular;
    }

    void FixedUpdate()
    {
        if (GameManager.Instance.Phase != RacePhase.Racing)
            return;

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

        float input = Input.GetAxisRaw("Vertical");
        float turn = Input.GetAxis("Horizontal") * turnSpeed * dt;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;

        RaycastHit hitCenter;
        bool grounded = ProbeGround(rb.position + Vector3.up * groundCheckHeight, groundCheckHeight + groundSnapDistance, out hitCenter) && hitCenter.normal.y > Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad) && IsGroundWithinSnapDistance(hitCenter);
        UpdateBlockedSpeed(dt, grounded);

        float slopeAccel = 0f;
        if (grounded)
        {
            Vector3 gn = hitCenter.normal;
            float slopeForward = -(gn.x * flatForward.x + gn.z * flatForward.z) / Mathf.Max(gn.y, 0.01f);
            float sinSlope = slopeForward / Mathf.Sqrt(1f + slopeForward * slopeForward);
            slopeAccel = -Physics.gravity.magnitude * sinSlope * slopeGravityFactor;

            if (isBoosted)
                slopeAccel *= boostSlopeFactor;
        }

        UpdateForwardSpeed(input, dt, slopeAccel);

        float dirSign = forwardSpeed >= 0f ? 1f : -1f;
        Vector3 moveFlat = flatForward * dirSign;
        float speedAbs = Mathf.Abs(forwardSpeed);

        Vector3 vel = rb.linearVelocity;
        Vector3 desired;

        if (grounded)
        {
            float maxTan = Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad);
            Vector3 n = hitCenter.normal;
            float slopeNormal = -(n.x * moveFlat.x + n.z * moveFlat.z) / Mathf.Max(n.y, 0.01f);
            float slope = slopeNormal;

            RaycastHit hitAhead;
            if (ProbeGround(rb.position + moveFlat * lookAhead + Vector3.up * groundCheckHeight,
                            groundCheckHeight + 1f, out hitAhead))
            {
                float dy = hitAhead.point.y - hitCenter.point.y;
                if (Mathf.Abs(dy) <= maxStepHeight)
                    slope = dy / Mathf.Max(lookAhead, 0.01f);
            }

            slope = Mathf.Clamp(slope, -maxTan, maxTan);

            Vector3 moveDir = (moveFlat + Vector3.up * slope).normalized;
            desired = moveDir * speedAbs;
            vel.x = desired.x;
            vel.z = desired.z;
        }
        else
        {
            desired = moveFlat * speedAbs;
            vel.x = desired.x;
            vel.z = desired.z;
        }

        rb.linearVelocity = vel;
        intendedVelocity = desired;
        commandedXZ = new Vector3(desired.x, 0f, desired.z);

        Quaternion rot = rb.rotation * Quaternion.Euler(0f, turn, 0f);
        if (grounded)
        {
            Quaternion aligned = Quaternion.FromToRotation(rot * Vector3.up, hitCenter.normal) * rot;
            rot = Quaternion.Slerp(rot, aligned, 1f - Mathf.Exp(-alignSharpness * dt));
            rb.angularVelocity = Vector3.zero;
        }
        rb.MoveRotation(rot);
    }

    bool IsGroundWithinSnapDistance(RaycastHit hit)
    {
        if (groundCollider == null)
            return true;

        float colliderBottomY = groundCollider.bounds.min.y;
        return hit.point.y >= colliderBottomY - Mathf.Max(0f, groundSnapDistance);
    }

    void UpdateBlockedSpeed(float dt, bool grounded)
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
        float blockingThreshold = blockedRatio;
        if (ratio < blockingThreshold)
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

    bool ProbeGround(Vector3 origin, float distance, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, groundMask, QueryTriggerInteraction.Ignore);
        best = default;
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = groundHits[i];
            if (h.collider.attachedRigidbody != null)
                continue;

            if (h.distance < bestDistance)
            {
                bestDistance = h.distance;
                best = h;
                found = true;
            }
        }

        return found;
    }

    void UpdateForwardSpeed(float input, float dt, float slopeAccel)
    {
        float maxReverse = baseSpeed * reverseSpeedFactor;
        float accel = isBoosted ? boostAcceleration : acceleration;

        if (input > 0.1f)
        {
            if (forwardSpeed < 0f)
            {
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * dt);
            }
            else if (forwardSpeed < currentMaxSpeed)
            {
                float t = Mathf.Clamp01(forwardSpeed / currentMaxSpeed);
                float a = accel * Mathf.Lerp(1f, topSpeedAccelerationFactor, t * t);
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, currentMaxSpeed, a * dt);
            }
        }
        else if (input < -0.1f)
        {
            if (forwardSpeed > 0f)
            {
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * dt);
            }
            else
            {
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, -maxReverse, reverseAcceleration * dt);
            }
        }
        else
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * dt);
        }

        if (slopeAccel != 0f)
        {
            bool idle = Mathf.Abs(input) <= 0.1f;
            if (idle && Mathf.Abs(forwardSpeed) < 0.2f && Mathf.Abs(slopeAccel) < slopeHoldAcceleration)
                forwardSpeed = 0f;
            else
                forwardSpeed += slopeAccel * dt;
        }

        forwardSpeed = Mathf.Max(forwardSpeed, -maxRollBackSpeed);

        if (forwardSpeed > currentMaxSpeed)
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, currentMaxSpeed, overspeedDeceleration * dt);
    }
}