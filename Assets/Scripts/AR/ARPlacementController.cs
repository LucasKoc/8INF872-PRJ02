using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Netcode;

public class ARPlacementController : MonoBehaviour
{
    [Header("AR")] public ARRaycastManager raycastManager;

    [Header("Circuit Prefabs")] public GameObject circuitPrefab;
    public GameObject carPrefab;

    [Header("Placement Indicator")] public GameObject placementIndicator;

    [Header("UI")] public Button placeButton;
    public Button resetButton;
    public Button readyButton;
    public TMPro.TMP_Text placementInstructionsText;

    [Header("Circuit Properties")] public bool autoScaleCircuit = true;
    public float distanceFromCamera = 1f;

    private GameObject previewInstance;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private Pose placementPose;
    private bool placementPoseIsValid = false;

    private GameObject spawnedCircuit;
    private GameObject spawnedCar;

    // Référence à RaceManager pour ready & go
    private RaceManager raceManager;

    public static Transform LocalCircuit { get; private set; }

    private void Start()
    {
        if (placeButton != null)
        {
            placeButton.onClick.AddListener(PlaceCircuit);
            placeButton.gameObject.SetActive(true);
        }


        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetPlacement);
            resetButton.gameObject.SetActive(false);
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
            readyButton.gameObject.SetActive(false);
        }

        // Trouver le RaceManager dans la scène
        raceManager = FindObjectOfType<RaceManager>();

        // Placement indicator
        if (placementIndicator != null) placementIndicator.SetActive(false);
    }

    private void Update()
    {
        // Si le circuit est déjà placé, on ne cherche plus de pose
        if (spawnedCircuit != null) return;

        UpdatePlacementPose();
    }

    private void UpdatePlacementPose()
    {
        var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            placementPoseIsValid = true;

            Pose hitPose = hits[0].pose;

            Transform cam = Camera.main.transform;
            Vector3 forward = cam.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 targetPos = cam.position + forward * distanceFromCamera;
            targetPos.y = hitPose.position.y;

            placementPose.position = targetPos;
            placementPose.rotation = Quaternion.LookRotation(forward, Vector3.up);

            // === Placement Indicator ===
            if (placementIndicator != null)
            {
                placementIndicator.SetActive(true);

                // position
                placementIndicator.transform.position = placementPose.position;

                // rotation: only yaw, no tilt
                float yaw = placementPose.rotation.eulerAngles.y;
                placementIndicator.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
        else
        {
            placementPoseIsValid = false;

            if (placementIndicator != null)
                placementIndicator.SetActive(false);
        }
    }


    public void PlaceCircuit()
    {
        if (!placementPoseIsValid)
        {
            Debug.Log("Aucun plan valide sous le centre de l'écran.");
            return;
        }

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }

        if (spawnedCircuit != null)
        {
            Debug.Log("Circuit déjà placé.");
            return;
        }

        // Instancier le circuit
        spawnedCircuit = Instantiate(circuitPrefab, placementPose.position, placementPose.rotation);

        // Auto-scale pour lui faire tenir dans le rectangle
        float circuitScale = AutoScaleCircuitToIndicator(spawnedCircuit);

        // Memoriser le circuit local
        LocalCircuit = spawnedCircuit.transform;

        // --- Lien avec RaceManager (circuit + spawn points) ---
        if (raceManager == null)
            raceManager = FindObjectOfType<RaceManager>();

        if (raceManager != null)
        {
            LapCounter lc = spawnedCircuit.GetComponentInChildren<LapCounter>();

            Transform startArea = spawnedCircuit.transform.Find("StartArea");
            var spawnPoints = new System.Collections.Generic.List<Transform>();

            for (int i = 1; i <= 3; i++)
            {
                Transform sp = startArea.Find($"CarSpawnPoint {i}");
                if (sp != null) spawnPoints.Add(sp);
            }

            Debug.Log($"[ARPlacement] Track enregistré : LapCounter={lc}, SpawnPoints={spawnPoints.Count}");
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                var sp = spawnPoints[i];
                Debug.Log($"[ARPlacement] SpawnPoint {i} name={sp.name} pos={sp.position}");
            }


            if (raceManager != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer)
            {
                raceManager.RegisterTrack(lc, spawnPoints.ToArray(), circuitScale);
            }

            Debug.Log($"[ARPlacement] Track enregistré : LapCounter={lc}, SpawnPoints={spawnPoints.Count}");
        }
        else
        {
            Debug.LogWarning("[ARPlacement] Aucun RaceManager trouvé dans la scène.");
        }

        // Désactiver l'indicateur et changement des bouttons
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        if (resetButton != null) resetButton.gameObject.SetActive(true);
        if (readyButton != null) readyButton.gameObject.SetActive(true);
        if (placementIndicator != null) placementIndicator.SetActive(false);

        Debug.Log("Circuit placé en AR.");
    }

    public void ResetPlacement()
    {
        if (spawnedCar != null) Destroy(spawnedCar);
        if (spawnedCircuit != null) Destroy(spawnedCircuit);

        spawnedCar = null;
        spawnedCircuit = null;

        if (placeButton != null) placeButton.gameObject.SetActive(true);
        if (resetButton != null) resetButton.gameObject.SetActive(false);
        if (readyButton != null) readyButton.gameObject.SetActive(false);

        Debug.Log("Placement AR réinitialisé.");
    }

    private void OnReadyClicked()
    {
        Debug.Log("Joueur prêt, envoi au serveur...");

        if (raceManager == null)
        {
            raceManager = FindObjectOfType<RaceManager>();
            Debug.Log($"[ARPlacement] raceManager recherché : {(raceManager != null ? "trouvé" : "null")}");
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[ARPlacement] NetworkManager.Singleton est NULL dans ARace_Game !");
            return;
        }

        if (raceManager != null &&
            NetworkManager.Singleton.IsClient &&
            raceManager.IsSpawned)
        {
            int carIndex = PlayerPrefs.GetInt("index_voiture_selectionnee", 0);

            Debug.Log($"[ARPlacement] Appel de SetReadyServerRpc(carIndex={carIndex})");
            raceManager.SetReadyServerRpc(carIndex);

            if (readyButton != null) readyButton.interactable = false;
            if (resetButton != null) resetButton.interactable = false;
            if (placementInstructionsText != null) placementInstructionsText.text = "";
        }
        else
        {
            Debug.LogWarning("[ARPlacement] Condition RPC non remplie.");
        }
    }

    private float AutoScaleCircuitToIndicator(GameObject circuit)
    {
        if (!autoScaleCircuit) return 1f;
        if (placementIndicator == null) return 1f;

        // On récupère tous les MeshRenderer du circuit
        var renderers = circuit.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return 1f;

        // Bounds du circuit en WORLD space
        Bounds circuitBounds = renderers[0].bounds;
        foreach (var r in renderers)
        {
            circuitBounds.Encapsulate(r.bounds);
        }

        float circuitWidth = circuitBounds.size.x; // largeur (X)
        float circuitDepth = circuitBounds.size.z; // profondeur (Z)

        // Bounds du rectangle d’indicateur (il a un MeshRenderer si c’est un Quad/Plane)
        var indicatorRenderer = placementIndicator.GetComponentInChildren<MeshRenderer>();
        if (indicatorRenderer == null) return 1f;

        Bounds indicatorBounds = indicatorRenderer.bounds;
        float targetWidth = indicatorBounds.size.x;
        float targetDepth = indicatorBounds.size.z;

        // Facteur d’échelle uniforme pour que le circuit tienne dans le rectangle
        float scaleFactorW = targetWidth / circuitWidth;
        float scaleFactorD = targetDepth / circuitDepth;
        float scaleFactor = Mathf.Min(scaleFactorW, scaleFactorD);

        // Appliquer l’échelle *par dessus l’échelle actuelle*
        circuit.transform.localScale *= scaleFactor;

        return scaleFactor;
    }
}