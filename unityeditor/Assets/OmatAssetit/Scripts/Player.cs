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
    public float stunImpactThreshold = 6f;
    public float stunDuration = 1.5f;
    public float extraSpinTorque = 8f;
    public float crashLaunchMultiplier = 1.5f;
    public float crashUpwardMultiplier = 0.8f;
    private RigidbodyConstraints normalConstraints;
    private bool wasAutoCorrectorEnabled;

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

    private Vector3 lastPosition;
    private Vector3 measuredVelocity;
    private float crashCooldownTimer = 0f;

    [Header("Crash tuning")]
    public float minStunDuration = 0.8f;
    public float crashCooldown = 0.5f;
    public float groundNormalY = 0.7f;

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
        lastPosition = rb.position;
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
    if (Vector3.Dot(away, rb.position - collision.transform.position) < 0f)
        away = -away;
    away.y = 0f;
    away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

    Vector3 otherVel = collision.rigidbody != null ? collision.rigidbody.linearVelocity : Vector3.zero;
    float impactSpeed = Mathf.Max(
        collision.relativeVelocity.magnitude,
        (measuredVelocity - otherVel).magnitude
    );

    if (impactSpeed < stunImpactThreshold)
        return;

    wasAutoCorrectorEnabled = autoCorrector != null && autoCorrector.enabled;
    if (autoCorrector != null)
        autoCorrector.enabled = false;

    rb.constraints = RigidbodyConstraints.None;

    isStunned = true;
    stunTimer = Mathf.Max(stunDuration, minStunDuration);
    crashCooldownTimer = stunTimer + crashCooldown;

    Vector3 launch = (away + Vector3.up * crashUpwardMultiplier) * crashLaunchMultiplier * impactSpeed;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    rb.AddForce(launch, ForceMode.Impulse);
    rb.AddTorque(Random.onUnitSphere * extraSpinTorque * impactSpeed, ForceMode.Impulse);
}

void FixedUpdate()
{
    if (GameManager.Instance.Phase != RacePhase.Racing)
        return;

    float dt = Time.fixedDeltaTime;
    measuredVelocity = (rb.position - lastPosition) / dt;
    lastPosition = rb.position;

    if (crashCooldownTimer > 0f)
        crashCooldownTimer -= dt;

    if (isStunned)
    {
        stunTimer -= dt;

        if (stunTimer <= 0f)
        {
            isStunned = false;
            rb.constraints = normalConstraints;
            rb.angularVelocity = Vector3.zero;

            if (autoCorrector != null)
                autoCorrector.enabled = wasAutoCorrectorEnabled;
        }

        return;
    }

    if (autoCorrector != null && autoCorrector.IsAutoCorrecting)
        return;

    float move = Input.GetAxis("Vertical") * currentSpeed * dt;
    float turn = Input.GetAxis("Horizontal") * turnSpeed * dt;

    rb.MovePosition(rb.position + transform.forward * move);
    rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
}

}