using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementManager : MonoBehaviour
{
    [Header("AR References")]
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;

    [Header("Prefabs")]
    public GameObject circuitPrefab;        // full game circuit
    public GameObject carPrefab;            // car
    public GameObject previewCircuitPrefab; // ghost preview circuit

    [Header("UI")]
    public Button placeButton;
    public Button resetButton;

    [Header("Placement")]
    public float distanceFromCamera = 3f;   // meters in front of camera

    private GameObject spawnedCircuit;
    private GameObject spawnedCar;
    private GameObject previewCircuit;

    private bool circuitPlaced = false;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Start()
    {
        if (placeButton != null)
            placeButton.onClick.AddListener(OnPlaceButtonPressed);

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonPressed);
            resetButton.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (circuitPlaced)
            return;

        if (raycastManager == null)
            return;

        // Raycast from screen center to detect a plane
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            // Position in front of the camera on horizontal plane
            Transform cam = Camera.main.transform;

            Vector3 forward = cam.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 targetPos = cam.position + forward * distanceFromCamera;
            targetPos.y = hitPose.position.y; // snap to plane height

            Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

            if (previewCircuit == null && previewCircuitPrefab != null)
            {
                previewCircuit = Instantiate(previewCircuitPrefab, targetPos, targetRot);
            }
            else if (previewCircuit != null)
            {
                previewCircuit.transform.SetPositionAndRotation(targetPos, targetRot);
                if (!previewCircuit.activeSelf)
                    previewCircuit.SetActive(true);
            }
        }
        else
        {
            if (previewCircuit != null && previewCircuit.activeSelf)
                previewCircuit.SetActive(false);
        }
    }

    void OnPlaceButtonPressed()
    {
        if (circuitPlaced)
            return;

        if (previewCircuit == null)
        {
            Debug.Log("ARPlacementManager: no preview to place.");
            return;
        }

        // 1) Instantiate circuit at preview pose
        Vector3 targetPos = previewCircuit.transform.position;
        Quaternion targetRot = previewCircuit.transform.rotation;

        spawnedCircuit = Instantiate(circuitPrefab, targetPos, targetRot);

        // 2) Find CarSpawnPoint *in the spawned circuit*
        Transform spawnPoint = spawnedCircuit.transform.Find("CarSpawnPoint");

        if (spawnPoint != null)
        {
            // We want CarSpawnPoint to be exactly at preview position
            // Compute offset from circuit pivot to spawn point
            Vector3 delta = spawnPoint.position - spawnedCircuit.transform.position;

            // Move the whole circuit so that spawnPoint sits at targetPos
            spawnedCircuit.transform.position = targetPos - delta;

            // Recompute spawnPoint position after moving circuit
            spawnPoint = spawnedCircuit.transform.Find("CarSpawnPoint");

            // Spawn car at that point
            spawnedCar = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            // Fallback: no spawn point, just put car at preview position
            spawnedCar = Instantiate(carPrefab, targetPos + Vector3.up * 0.2f, targetRot);
            Debug.LogWarning("CarSpawnPoint not found in circuit, car spawned at preview position.");
        }

        circuitPlaced = true;

        if (previewCircuit != null)
        {
            Destroy(previewCircuit);
            previewCircuit = null;
        }

        SetPlanesActive(false);

        if (placeButton != null)
            placeButton.gameObject.SetActive(false);

        if (resetButton != null)
            resetButton.gameObject.SetActive(true);
    }

    void OnResetButtonPressed()
    {
        if (spawnedCircuit != null)
            Destroy(spawnedCircuit);

        if (spawnedCar != null)
            Destroy(spawnedCar);

        if (previewCircuit != null)
        {
            Destroy(previewCircuit);
            previewCircuit = null;
        }

        circuitPlaced = false;
        SetPlanesActive(true);

        if (placeButton != null)
            placeButton.gameObject.SetActive(true);

        if (resetButton != null)
            resetButton.gameObject.SetActive(false);
    }

    void SetPlanesActive(bool value)
    {
        if (planeManager == null)
            return;

        planeManager.enabled = value;

        foreach (var plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(value);
        }
    }
}
