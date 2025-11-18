using UnityEngine;

public class LapCounter : MonoBehaviour
{
    [Header("Réglages")] public int totalLaps = 3;
    [Header("Debug (lecture seule)")] public int currentLap = 0;
    public bool raceFinished = false;

    private bool hasStarted = false;

    private void OnTriggerEnter(Collider other)
    {
        // On ne réagit qu'à la voiture du joueur
        if (!raceFinished && other.CompareTag("Player"))
        {
            if (!hasStarted)
            {
                // Premier passage : on démarre la course
                hasStarted = true;
                currentLap = 1;
                Debug.Log("Départ ! Tour 1 / " + totalLaps);
            }
            else
            {
                currentLap++;
                Debug.Log("Passage ligne. Tour " + currentLap + " / " + totalLaps);
            }

            // Vérifier si on a fini
            if (currentLap >= totalLaps)
            {
                raceFinished = true;
                Debug.Log("Course terminée !");
                // Ici plus tard : appeler un écran de fin de course / retour lobby
            }
        }
    }
}