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
    [Tooltip("Liste des prefabs de voitures dans le même ordre que le menu de sélection.")]
    public GameObject[] carPrefabs;

    // choix de voiture par client : clientId -> index de voiture
    private Dictionary<ulong, int> playerCarChoices = new Dictionary<ulong, int>();

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

        if (lc != null)
            circuitRef = lc.transform.root;

        Debug.Log($"[RaceManager] RegisterTrack -> LapCounter={lapCounter != null}, SpawnPoints={carSpawnPoints?.Length}, trackScale={trackScale}");

        if (IsServer && lapCounter != null)
        {
            lapCounter.currentLap = 0;
            lapCounter.raceFinished = false;

            // Si la piste vient juste d'être posée alors que tout le monde
            // était déjà ready, on peut tenter de lancer la course.
            TryStartRace();
        }
    }

    private void TryStartRace()
    {
        if (!IsServer) return;
        if (countdownRunning || raceStarted.Value) return;

        int connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (connectedCount == 0) return;

        // On attend que la piste du host soit posée
        if (carSpawnPoints == null || carSpawnPoints.Length == 0)
        {
            Debug.Log("[RaceManager] TryStartRace : piste pas encore posée, on attend.");
            return;
        }

        // Tous les joueurs connectés doivent être ready
        if (readyClients.Count < connectedCount)
        {
            Debug.Log($"[RaceManager] TryStartRace : {readyClients.Count}/{connectedCount} prêts, on attend.");
            return;
        }

        Debug.Log("[RaceManager] Tous prêts ET piste posée → on lance le compte à rebours + spawn des voitures.");
        StartCoroutine(StartRaceCountdown());
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(int carIndex, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[RaceManager] SetReadyServerRpc reçu de {clientId} avec carIndex={carIndex}");

        if (raceStarted.Value)
        {
            Debug.Log("[RaceManager] Course déjà lancée, on ignore.");
            return;
        }

        // On clamp l'index pour éviter les bêtises
        if (carPrefabs == null || carPrefabs.Length == 0)
        {
            Debug.LogError("[RaceManager] carPrefabs non configuré !");
            carIndex = 0;
        }
        else
        {
            carIndex = Mathf.Clamp(carIndex, 0, carPrefabs.Length - 1);
        }

        // On mémorise le choix de voiture pour ce client
        playerCarChoices[clientId] = carIndex;

        // On marque ce client comme ready
        if (!readyClients.Add(clientId))
        {
            Debug.Log($"Client {clientId} était déjà ready.");
            return;
        }

        Debug.Log($"Client {clientId} ready ({readyClients.Count}/{NetworkManager.Singleton.ConnectedClientsIds.Count})");

        // On essaye de lancer la course (si tous prêts + piste OK)
        TryStartRace();
    }

    private void SpawnCarForClient(ulong clientId)
    {
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

        int carIndex = 0;
        if (!playerCarChoices.TryGetValue(clientId, out carIndex))
        {
            Debug.LogWarning($"[RaceManager] Pas de choix de voiture pour client {clientId}, on prend l'index 0.");
            carIndex = 0;
        }

        if (carPrefabs == null || carPrefabs.Length == 0)
        {
            Debug.LogError("[RaceManager] carPrefabs non configuré, impossible de spawn !");
            return;
        }

        if (carIndex < 0 || carIndex >= carPrefabs.Length)
        {
            Debug.LogWarning($"[RaceManager] carIndex {carIndex} hors borne, on clamp à 0.");
            carIndex = 0;
        }

        GameObject prefab = carPrefabs[carIndex];
        GameObject carObj = Instantiate(prefab, spawn.position, spawn.rotation);

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

        // 1) On spawn ici les voitures pour TOUS les joueurs ready
        foreach (var clientId in readyClients)
        {
            if (!playerCars.ContainsKey(clientId))
            {
                SpawnCarForClient(clientId);
            }
        }

        // (optionnel) petit délai pour laisser le Netcode spawner chez les clients
        yield return new WaitForSeconds(0.2f);

        // 2) Compte à rebours 3-2-1-GO
        for (int t = 3; t > 0; t--)
        {
            countdownValue.Value = t;
            Debug.Log($"Course dans {t}...");
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("GO !");
        countdownValue.Value = 0;  // 0 = GO!
        raceStarted.Value = true;

        // 3) Autoriser toutes les voitures à rouler
        foreach (var kvp in playerCars)
        {
            if (kvp.Value != null)
            {
                kvp.Value.canDrive.Value = true;
            }
        }

        // 4) On cache le texte après 1 seconde
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

        // if (carSpawnPoints == null || carSpawnPoints.Length == 0)
        //     return;
        //
        // if (!playerCars.ContainsKey(clientId))
        // {
        //     SpawnCarForClient(clientId);
        // }

        // Maintenant : juste log si tu veux
        Debug.Log($"[RaceManager] Client connecté : {clientId}");
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