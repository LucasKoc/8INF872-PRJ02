using UnityEngine;

public class RaceManager : MonoBehaviour
{
    [Header("Références")]
    public LapCounter lapCounter;
    public SimpleCarController playerCar;

    private void Start()
    {
        if (lapCounter == null)
        {
            // On essaie de le retrouver dans la scène
            lapCounter = FindObjectOfType<LapCounter>();
            Debug.Log("RaceManager : LapCounter automatiquement assigné.");
        }

        if (playerCar == null)
        {
            playerCar = FindObjectOfType<SimpleCarController>();
            Debug.Log("RaceManager : SimpleCarController automatiquement assigné.");
        }

        if (lapCounter == null)
        {
            Debug.LogWarning("RaceManager : aucun LapCounter trouvé !");
        }

        if (playerCar == null)
        {
            Debug.LogWarning("RaceManager : aucune voiture (SimpleCarController) trouvée !");
        }
    }

    private void Update()
    {
        if (lapCounter == null)
            return;

        if (lapCounter.raceFinished)
        {
            // Ici plus tard : prévenir l'UI, le réseau, etc.
            // Pour l’instant, on log juste une fois.
            Debug.Log("RaceManager : la course est terminée.");
            // On peut désactiver ce script si on veut éviter les logs répétés
            enabled = false;
        }
    }
}