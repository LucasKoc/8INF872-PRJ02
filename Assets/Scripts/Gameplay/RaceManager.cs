using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class RaceManager : NetworkBehaviour
{
    [Header("Références")]
    public LapCounter lapCounter;

    [Header("Voitures")]
    public GameObject carPrefab;
    private Transform[] carSpawnPoints;
    private Dictionary<ulong, SimpleCarController> playerCars =
        new Dictionary<ulong, SimpleCarController>();

    // circuit de référence côté host
    private Transform circuitRef;
    private float trackScale = 1f;

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

    // État de la course côté réseau
    public NetworkVariable<bool> raceStarted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Appelé par ARPlacementController côté host
    public void RegisterTrack(LapCounter lc, Transform[] spawnPoints, float scale)
    {
        lapCounter     = lc;
        carSpawnPoints = spawnPoints;
        trackScale     = scale;

        // circuitRef = root du circuit du host
        if (lc != null)
            circuitRef = lc.transform.root;   // ou spawnedCircuit.transform côté host

        Debug.Log($"[RaceManager] RegisterTrack -> LapCounter={lapCounter != null}, SpawnPoints={carSpawnPoints?.Length}, trackScale={trackScale}");

        if (IsServer && lapCounter != null)
        {
            lapCounter.currentLap = 0;
            lapCounter.raceFinished = false;
        }
    }



    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[RaceManager] SetReadyServerRpc reçu de {clientId}. IsServer={IsServer}");

        if (raceStarted.Value)
        {
            Debug.Log("[RaceManager] Course déjà lancée, on ignore.");
            return;
        }

        // 1) Si ce client n’a pas encore de voiture, on la crée
        if (!playerCars.ContainsKey(clientId))
        {
            SpawnCarForClient(clientId);
        }

        // 2) Gestion du ready
        if (!readyClients.Add(clientId))
        {
            Debug.Log($"Client {clientId} était déjà ready.");
            return;
        }

        int connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"Client {clientId} ready ({readyClients.Count}/{connectedCount})");

        if (countdownRunning) return;

        // SOLO
        if (connectedCount == 1)
        {
            Debug.Log("[RaceManager] Un seul joueur connecté : on lance le compte à rebours.");
            StartCoroutine(StartRaceCountdown());
            return;
        }

        // MULTI
        if (readyClients.Count >= connectedCount)
        {
            Debug.Log("[RaceManager] Tous les joueurs connectés sont prêts, on lance le compte à rebours.");
            StartCoroutine(StartRaceCountdown());
        }
    }

    private void SpawnCarForClient(ulong clientId)
    {
        if (carPrefab == null)
        {
            Debug.LogError("[RaceManager] carPrefab n’est pas assigné !");
            return;
        }
        if (carSpawnPoints == null || carSpawnPoints.Length == 0)
        {
            Debug.LogError("[RaceManager] Aucun CarSpawnPoint enregistré !");
            return;
        }

        int index = playerCars.Count; // 0,1,2,...
        if (index >= carSpawnPoints.Length)
            index = carSpawnPoints.Length - 1;

        Transform spawn = carSpawnPoints[index];
        Debug.Log($"[RaceManager] Spawn car pour client {clientId} au spawn index={index}, name={spawn.name}, pos={spawn.position}");

        GameObject carObj = Instantiate(carPrefab, spawn.position, spawn.rotation);
        Debug.Log($"[RaceManager] Car {clientId} world pos après instantiate = {carObj.transform.position}");
        carObj.transform.localScale *= trackScale;

        // Récupérer le controller
        var carCtrl = carObj.GetComponent<SimpleCarController>();
        if (carCtrl == null)
        {
            Debug.LogError("[RaceManager] carPrefab n’a pas de SimpleCarController !");
        }
        else
        {
            // On donne au contrôleur le circuit de référence du host
            if (circuitRef != null)
            {
                carCtrl.InitServerTrack(circuitRef);
            }
            playerCars[clientId] = carCtrl;
        }

        var no = carObj.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[RaceManager] carPrefab n’a pas de NetworkObject !");
            Destroy(carObj);
            return;
        }

        // L’owner est le client
        no.SpawnWithOwnership(clientId);
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
        foreach (var kvp in playerCars)
        {
            if (kvp.Value != null)
            {
                kvp.Value.canDrive.Value = true;
            }
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

            // On écoute les nouveaux clients qui se connectent - NE DEVRAIS PAS ARRIVER PCQ READY AVANT
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // si la piste n'est pas encore posée, on ne fait rien
        if (carSpawnPoints == null || carSpawnPoints.Length == 0)
            return;

        if (!playerCars.ContainsKey(clientId))
        {
            SpawnCarForClient(clientId);
        }
    }

    private void OnDestroy()
    {
        countdownValue.OnValueChanged -= OnCountdownChanged;
    }

    private void OnCountdownChanged(int oldValue, int newValue)
    {
        if (countdownText == null) return;

        if (newValue < 0)
        {
            countdownText.gameObject.SetActive(false);
            return;
        }

        countdownText.gameObject.SetActive(true);

        if (newValue > 0)
            countdownText.text = newValue.ToString();
        else
            countdownText.text = "GO!";
    }

    private void Update()
    {
        if (!IsServer) return;

        if (lapCounter == null)
            return;

        if (lapCounter.raceFinished)
        {
            Debug.Log("RaceManager : la course est terminée.");
            enabled = false;
        }
    }
}