using UnityEngine;

public class TriggerBlueCarRotation : MonoBehaviour
{
    [Header("AI car script:")]
    public AICar blueCarScript;

    [Header("Crash settings:")]
    public string requiredTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            if (blueCarScript != null)
            {
                blueCarScript.enabled = true;
                
                blueCarScript.rotationSpeed = 0.1f;
            }
            else
            {
                Debug.LogWarning("AI car script isn't here :<");
            }
        }
    }
}