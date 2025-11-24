using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Ordre du checkpoint (0, 1, 2, ...)")]
    public int index;

    [Header("Référence au LapCounter (ligne de départ)")]
    public LapCounter lapCounter;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (lapCounter == null)
            return;

        if (lapCounter.raceFinished)
            return;

        lapCounter.NotifyCheckpointReached(this);
    }
}