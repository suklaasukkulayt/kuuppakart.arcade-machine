using UnityEngine;

public class CheckpointTarkistus : MonoBehaviour
{
    public int orderIndex = 0;
    private void OnTriggerEnter(Collider other)
    {
        PelaajanKierrosTarkastus validator = other.GetComponent<PelaajanKierrosTarkastus>();
        if(validator != null) 
        {
            validator.MarkVisited(orderIndex);
        }
    }
}
