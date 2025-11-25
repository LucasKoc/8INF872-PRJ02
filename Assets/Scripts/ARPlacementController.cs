using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementController : MonoBehaviour
{
    [Header("AR")] public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;

    [Header("Prefabs")] public GameObject circuitPrefab;
    public GameObject carPrefab;

    [Header("UI")] public Button placeButton;
    public Button resetButton;

    [Header("Placement")] public float distanceFromCamera = 1f;

    [Header("Placement Indicator")] public GameObject placementIndicator;

    [Header("Auto Scale")] public bool autoScaleCircuit = true;

    private GameObject previewInstance;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private Pose placementPose;
    private bool placementPoseIsValid = false;

    private GameObject spawnedCircuit;
    private GameObject spawnedCar;

    private void Start()
    {
        if (placeButton != null)
            placeButton.onClick.AddListener(PlaceCircuit);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetPlacement);

        // Au début : on peut placer, mais pas reset
        if (placeButton != null) placeButton.gameObject.SetActive(true);
        if (resetButton != null) resetButton.gameObject.SetActive(false);

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
        AutoScaleCircuitToIndicator(spawnedCircuit);

        // Recherche du point de spawn de la voiture dans le circuit et instanciation de la voiture
        Transform spawnPoint = spawnedCircuit.transform.Find("StartArea").transform.Find("CarSpawnPoint");
        spawnedCar = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation, spawnedCircuit.transform);

        // Désactiver l'indicateur et changement des bouttons
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        if (resetButton != null) resetButton.gameObject.SetActive(true);
        if (placementIndicator != null) placementIndicator.SetActive(false);

        Debug.Log("Circuit + voiture placés en AR.");
    }

    public void ResetPlacement()
    {
        if (spawnedCar != null) Destroy(spawnedCar);
        if (spawnedCircuit != null) Destroy(spawnedCircuit);

        spawnedCar = null;
        spawnedCircuit = null;

        if (placeButton != null) placeButton.gameObject.SetActive(true);
        if (resetButton != null) resetButton.gameObject.SetActive(false);

        Debug.Log("Placement AR réinitialisé.");
    }

    private void AutoScaleCircuitToIndicator(GameObject circuit)
    {
        if (!autoScaleCircuit) return;
        if (placementIndicator == null) return;

        // On récupère tous les MeshRenderer du circuit
        var renderers = circuit.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;

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
        if (indicatorRenderer == null) return;

        Bounds indicatorBounds = indicatorRenderer.bounds;
        float targetWidth = indicatorBounds.size.x;
        float targetDepth = indicatorBounds.size.z;

        // Facteur d’échelle uniforme pour que le circuit tienne dans le rectangle
        float scaleFactorW = targetWidth / circuitWidth;
        float scaleFactorD = targetDepth / circuitDepth;
        float scaleFactor = Mathf.Min(scaleFactorW, scaleFactorD);

        // Appliquer l’échelle *par dessus l’échelle actuelle*
        circuit.transform.localScale *= scaleFactor;

        Debug.Log(
            $"AutoScale : circuit {circuitWidth}x{circuitDepth} -> cible {targetWidth}x{targetDepth}, factor={scaleFactor}");
    }
}