using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class RaceManager : NetworkBehaviour
{
    [Header("Références")]
    public LapCounter lapCounter;
    public SimpleCarController playerCar;

    [Header("UI")]
    public TMP_Text countdownText;

    // Joeurs prêts ?
    private HashSet<ulong> readyClients = new HashSet<ulong>();
    private bool countdownRunning = false;

    // Valeur du compte à rebours (-1 = caché)
    public NetworkVariable<int> countdownValue = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private Coroutine hideCountdownCoroutine;

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
        Debug.Log($"[RaceManager] SetReadyServerRpc reçu de {clientId}. IsServer={IsServer}");

        // Si déjà prêt, on ignore
        if (!readyClients.Add(clientId))
        {
            Debug.Log($"Client {clientId} était déjà ready.");
            return;
        }

        int connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"Client {clientId} ready ({readyClients.Count}/{connectedCount})");

        if (!countdownRunning && readyClients.Count >= connectedCount)
        {
            Debug.Log("[RaceManager] Tous les joueurs sont prêts, on lance le compte à rebours.");
            StartCoroutine(StartRaceCountdown());
        }
    }

    public void RegisterTrackAndCar(LapCounter lc, SimpleCarController car)
    {
        lapCounter = lc;
        playerCar = car;

        Debug.Log($"[RaceManager] RegisterTrackAndCar -> LapCounter={lapCounter != null}, Car={playerCar != null}");

        // Reset l’état de course côté serveur
        if (IsServer && lapCounter != null)
        {
            lapCounter.currentLap = 0;
            lapCounter.raceFinished = false;
        }
    }

    private IEnumerator StartRaceCountdown()
    {
        countdownRunning = true;

        // On part de 3 → 2 → 1 → GO
        for (int t = 3; t > 0; t--)
        {
            // Update côté serveur, propagé à tous
            countdownValue.Value = t;
            Debug.Log($"Course dans {t}...");
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("GO !");
        countdownValue.Value = 0;  // 0 = GO!
        raceStarted.Value = true;

        // Autoriser toutes les voitures à rouler
        foreach (var car in FindObjectsOfType<SimpleCarController>())
        {
            car.canDrive.Value = true;
        }

        // On cache le texte après 1 seconde
        yield return new WaitForSeconds(1f);
        countdownValue.Value = -1; // -1 = caché
        countdownRunning = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"[RaceManager] OnNetworkSpawn. IsServer={IsServer}, IsClient={IsClient}");

        // Abonner le callback pour le timer
        countdownValue.OnValueChanged += OnCountdownChanged;

        if (IsServer)
        {
            readyClients.Clear();
            countdownRunning = false;
            raceStarted.Value = false;
            countdownValue.Value = -1;

            if (lapCounter != null)
            {
                lapCounter.currentLap = 0;
                lapCounter.raceFinished = false;
            }
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

    private void OnDestroy()
    {
        countdownValue.OnValueChanged -= OnCountdownChanged;
    }

    private void OnCountdownChanged(int oldValue, int newValue)
    {
        if (countdownText == null) return;

        // -1 → caché
        if (newValue < 0)
        {
            countdownText.gameObject.SetActive(false);
            return;
        }

        // On affiche le texte
        countdownText.gameObject.SetActive(true);

        if (newValue > 0)
        {
            // 3, 2, 1
            countdownText.text = newValue.ToString();
        }
        else // newValue == 0 ➜ GO!
        {
            countdownText.text = "GO!";
        }
    }
}