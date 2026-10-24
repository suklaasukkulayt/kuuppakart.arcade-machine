using UnityEngine;

public class CarTeleporter : MonoBehaviour
{
    [Header("Teleport settings")]
    public float fallThreshold = -10f;
    public Vector3 respawnPosition = new Vector3(18.8f, 0.101f, 43.359f);
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (transform.position.y < fallThreshold)
        {
            TeleportCar();
        }
    }

    private void TeleportCar()
    {
        transform.position = respawnPosition;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}