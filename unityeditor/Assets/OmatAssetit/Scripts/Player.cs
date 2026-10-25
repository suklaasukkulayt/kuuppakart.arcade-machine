using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 10f;
    public float turnSpeed = 130f;

    [Header("Difficulty")]
    public float hardModeSpeedMultiplier = 0.9f;

    
    [Header("Crashing")]
    public float stunImpactThreshold = 3f;
    public float stunDuration = 1f;
    public float minStunDuration = 0.8f;
    public float crashCooldown = 0.5f;
    public float groundNormalY = 0.7f;
    public float crashBounceFactor = 0.5f;
    public float crashUpFactor = 0.35f;
    public float crashSpinPerImpact = 0.8f;
    public float maxCrashSpin = 12f;
    private RigidbodyConstraints normalConstraints;

    [Header("Boost settings")]
    public float boostSpeed = 15f;
    public float boostDuration = 1.5f;
    public float boostCooldown = 4.0f;
    public TextMeshProUGUI uiText;

    private float currentSpeed;
    private float boostTimer = 0f;
    private float cooldownTimer = 0f;
    private bool isBoosted = false;
    private Rigidbody rb;
    private bool isStunned = false;
    private float stunTimer = 0f;

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

        currentSpeed = baseSpeed;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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
            currentSpeed = boostSpeed;
            boostTimer = boostDuration;
        }

        if (isBoosted)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0f)
            {
                isBoosted = false;
                currentSpeed = baseSpeed;
                cooldownTimer = boostCooldown;
            }
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (uiText != null)
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

    if (impactSpeed < stunImpactThreshold)
        return;

    if (autoCorrector != null)
        autoCorrector.Suspended = true; 

    rb.constraints = RigidbodyConstraints.None;

    isStunned = true;
    stunTimer = Mathf.Max(stunDuration, minStunDuration);
    crashCooldownTimer = stunTimer + crashCooldown;

    rb.linearVelocity = away * impactSpeed * crashBounceFactor + Vector3.up * impactSpeed * crashUpFactor;
    float spin = Mathf.Min(impactSpeed * crashSpinPerImpact, maxCrashSpin);
    rb.angularVelocity = Random.onUnitSphere * spin;
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
        intendedVelocity = Vector3.zero;
        return;
    }

    float input = Input.GetAxis("Vertical");
    float turn = Input.GetAxis("Horizontal") * turnSpeed * dt;

    Vector3 desired = transform.forward * (input * currentSpeed);
    Vector3 vel = rb.linearVelocity;
    vel.x = desired.x;
    vel.z = desired.z;
    if (desired.y > 0f)
        vel.y = desired.y;
    rb.linearVelocity = vel;
    intendedVelocity = desired;

    rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
}

}