using Unity.Netcode;
using UnityEngine;

public class RaceManager : NetworkBehaviour
{
    [Header("Références")]
    public LapCounter lapCounter;
    public SimpleCarController playerCar;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Exemple : démarrer un timer, mettre à zéro les tours, etc.
        lapCounter.currentLap=0; 
    }
    
    private void Start()
    {
        if (lapCounter == null)
        {
            // On essaie de le retrouver dans la scène
            lapCounter = FindObjectOfType<LapCounter>();
            Debug.Log("RaceManager : LapCounter automatiquement assigné.");
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
        if (!IsServer) return;
        
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