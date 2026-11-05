using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CheckpointTrigger : MonoBehaviour
{
    public int index;

    private void OnTriggerEnter(Collider other)
    {
        string carName = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject.name : other.gameObject.name;
        if (carName.Contains("Player") || carName.Contains("AI"))
        {
            LapManager.Instance?.OnCarHitCheckpoint(carName, index);
        }
    }
}
