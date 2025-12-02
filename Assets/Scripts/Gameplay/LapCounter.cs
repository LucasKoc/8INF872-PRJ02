using UnityEngine;
using Unity.Netcode;

public class LapCounter : MonoBehaviour
{
    [Header("Réglages")]
    public int totalLaps = 3;

    [Header("Debug (lecture seule)")]
    public int currentLap = 0;
    public bool raceFinished = false;

    [Header("Race manager (optionnel)")]
    public RaceManager raceManager;

    // est-ce qu'on a déjà franchi la ligne une première fois
    private bool hasStarted = false;

    [Header("Checkpoints dans l'ordre")]
    public Checkpoint[] checkpoints;  // 0 -> 1 -> 2 ...

    // index du prochain checkpoint attendu
    private int nextCheckpointIndex = 0;

    void Start()
    {
        // Si LobbyUI.GameLaps a été défini dans le lobby
        if (LobbyUI.GameLaps > 0)
        {
            totalLaps = LobbyUI.GameLaps;
        }
    }

    // Appelée par un checkpoint quand le joueur le traverse
    public void NotifyCheckpointReached(Checkpoint cp)
    {
        // Si ce n'est pas le bon checkpoint dans l'ordre, on ignore
        if (cp.index != nextCheckpointIndex)
        {
            Debug.Log($"Checkpoint {cp.index} ignoré (on attendait {nextCheckpointIndex})");
            return;
        }

        Debug.Log($"Checkpoint {cp.index} validé");

        nextCheckpointIndex++;

        // Si on veut, on peut log quand tous les checkpoints sont faits
        if (nextCheckpointIndex >= checkpoints.Length)
        {
            Debug.Log("Tous les checkpoints de ce tour sont validés, tu peux boucler la ligne.");
        }
    }

    private void ResetCheckpointsForNextLap()
    {
        nextCheckpointIndex = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        // On ne réagit qu'à la voiture du joueur
        if (!other.CompareTag("Player"))
            return;

        if (raceFinished)
            return;

        if (!hasStarted)
        {
            // Premier passage : on démarre la course
            hasStarted = true;
            currentLap = 1;
            ResetCheckpointsForNextLap();
            Debug.Log("Départ ! Tour 1 / " + totalLaps);
        }
        else
        {
            // On ne compte le tour que si tous les checkpoints ont été faits dans l'ordre
            if (nextCheckpointIndex < checkpoints.Length)
            {
                Debug.Log("Ligne franchie, mais tous les checkpoints dans l'ordre n'ont pas été validés -> tour NON compté");
                return;
            }

            currentLap++;
            Debug.Log("Passage ligne. Tour " + currentLap + " / " + totalLaps);

            // Préparer le tour suivant
            ResetCheckpointsForNextLap();

            if (currentLap >= totalLaps)
            {
                raceFinished = true;
                Debug.Log("Course terminée !");

                if (raceManager != null &&
                    NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsServer)
                {
                    var no = other.GetComponentInParent<NetworkObject>();
                    if (no != null)
                    {
                        raceManager.NotifyRaceFinished(no.OwnerClientId);
                    }
                }
            }
        }
    }
}
