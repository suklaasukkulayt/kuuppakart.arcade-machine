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

void OnCollisionEnter(Collision collision) {
    float impactSpeed = collision.relativeVelocity.magnitude;

    if (impactSpeed < stunImpactThreshold)
        return;

    if (!isStunned)
    {
        wasAutoCorrectorEnabled =
            autoCorrector != null && autoCorrector.enabled;

        if (autoCorrector != null)
            autoCorrector.enabled = false;

        rb.constraints = RigidbodyConstraints.None;
    }

    isStunned = true;
    stunTimer = stunDuration;

    Vector3 launchDirection =
        collision.relativeVelocity.sqrMagnitude > 0.01f
        ? -collision.relativeVelocity.normalized
        : transform.forward;

    Vector3 launchForce =
        launchDirection * impactSpeed * crashLaunchMultiplier
        + Vector3.up * impactSpeed * crashUpwardMultiplier;

    rb.AddForce(launchForce, ForceMode.Impulse);

    rb.AddTorque(
        Random.onUnitSphere * extraSpinTorque * impactSpeed,
        ForceMode.Impulse
    );
}

void FixedUpdate()
{
    if (GameManager.Instance.Phase != RacePhase.Racing)
        return;

    if (isStunned)
    {
        stunTimer -= Time.fixedDeltaTime;

        if (stunTimer <= 0f)
        {
            isStunned = false;
            rb.constraints = normalConstraints;

            if (autoCorrector != null)
                autoCorrector.enabled = wasAutoCorrectorEnabled;
        }

        return;
    }

    if (autoCorrector != null && autoCorrector.IsAutoCorrecting)
        return;

    float move =
        Input.GetAxis("Vertical") * currentSpeed * Time.fixedDeltaTime;

    float turn =
        Input.GetAxis("Horizontal") * turnSpeed * Time.fixedDeltaTime;

    rb.MovePosition(rb.position + transform.forward * move);
    rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
}

}