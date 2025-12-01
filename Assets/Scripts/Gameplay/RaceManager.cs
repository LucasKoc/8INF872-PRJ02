using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RaceManager : NetworkBehaviour
{
    [Header("Références")]
    public LapCounter lapCounter;
    public SimpleCarController playerCar;

    // Joeurs prêts ?
    private HashSet<ulong> readyClients = new HashSet<ulong>();
    private bool countdownRunning = false;

    // État de la course côté réseau
    public NetworkVariable<bool> raceStarted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Si déjà prêt, on ignore
        if (!readyClients.Add(clientId))
        {
            Debug.Log($"Client {clientId} était déjà ready.");
            return;
        }

        int connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"Client {clientId} ready ({readyClients.Count}/{connectedCount})");

        // Quand tout le monde est prêt → on lance le countdown
        if (!countdownRunning && readyClients.Count >= connectedCount)
        {
            StartCoroutine(StartRaceCountdown());
        }
    }

    private IEnumerator StartRaceCountdown()
    {
        countdownRunning = true;

        float timer = 3f;

        while (timer > 0f)
        {
            Debug.Log($"Course dans {Mathf.CeilToInt(timer)}...");
            // Ici plus tard : mettre à jour un UI de compte à rebours via NetworkVariable ou RPC
            timer -= Time.deltaTime;
            yield return null;
        }

        Debug.Log("GO !");
        raceStarted.Value = true;

        // Autoriser toutes les voitures à rouler
        foreach (var car in FindObjectsOfType<SimpleCarController>())
        {
            car.canDrive.Value = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        readyClients.Clear();
        countdownRunning = false;
        raceStarted.Value = false;

        if (lapCounter != null)
        {
            lapCounter.currentLap = 0;
            lapCounter.raceFinished = false;
        }
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