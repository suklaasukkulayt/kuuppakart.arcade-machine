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
    private bool isStunned = false;
    private float stunTimer = 0f;
    private bool writeBoostText = true;
    private float forwardSpeed = 0f;
    private PlayerUprightAndShortcuts_AutoFallReset autoCorrector;
    private Vector3 intendedVelocity;
    private float crashCooldownTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        normalConstraints = rb.constraints;
        autoCorrector = GetComponent<PlayerUprightAndShortcuts_AutoFallReset>();

        if (DifficultyManager.Instance != null && DifficultyManager.Instance.SelectedDifficulty == Difficulty.Hard)
        {
            baseSpeed *= hardModeSpeedMultiplier;
        }

        currentMaxSpeed = baseSpeed;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

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

        if (isStunned || crashCooldownTimer > 0f)
            return;

        ContactPoint contact = collision.GetContact(0);

        if (contact.normal.y > groundNormalY)
            return;

        Vector3 away = contact.normal;
        if (Vector3.Dot(away, rb.position - contact.point) < 0f)
            away = -away;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

        Vector3 otherVel = collision.rigidbody != null ? collision.rigidbody.linearVelocity : Vector3.zero;
        float intoWall = Mathf.Max(0f, Vector3.Dot(intendedVelocity - otherVel, -away));
        float impactSpeed = Mathf.Max(intoWall, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, away)));

        if (impactSpeed < crashMinSpeed)
            return;

        float severity = Mathf.InverseLerp(crashMinSpeed, crashFullSpeed, impactSpeed);

        if (autoCorrector != null)
            autoCorrector.Suspended = true;

        rb.constraints = RigidbodyConstraints.None;

        isStunned = true;
        stunTimer = Mathf.Lerp(minStunTime, maxStunTime, severity);
        crashCooldownTimer = stunTimer + crashCooldown;
        forwardSpeed = 0f;

        rb.linearVelocity = away * impactSpeed * bounceFactor + Vector3.up * impactSpeed * upFactor;
        rb.angularVelocity = Random.onUnitSphere * Mathf.Lerp(minSpin, maxSpin, severity);
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
            return;
        }

        float actual = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (forwardSpeed > 0f && actual < forwardSpeed - 2f)
            forwardSpeed = Mathf.Max(actual, 0f);
        else if (forwardSpeed < 0f && actual > forwardSpeed + 2f)
            forwardSpeed = Mathf.Min(actual, 0f);

        float input = Input.GetAxisRaw("Vertical");
        float turn = Input.GetAxis("Horizontal") * turnSpeed * dt;

        UpdateForwardSpeed(input, dt);

        Vector3 desired = transform.forward * forwardSpeed;
        Vector3 vel = rb.linearVelocity;
        vel.x = desired.x;
        vel.z = desired.z;
        if (desired.y > 0f)
            vel.y = desired.y;
        rb.linearVelocity = vel;
        intendedVelocity = desired;

        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }

    void UpdateForwardSpeed(float input, float dt)
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

        if (forwardSpeed > currentMaxSpeed)
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, currentMaxSpeed, overspeedDeceleration * dt);
    }
}