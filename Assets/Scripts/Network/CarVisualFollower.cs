using Unity.Netcode;
using UnityEngine;

public class CarVisualFollower : NetworkBehaviour
{
    [Header("Références")]
    public Transform localCircuit;    // assigné par ARPlacementController
    public Transform visualRoot;      // ce qu’on bouge visuellement

    private NetworkCarState state;

    private void Awake()
    {
        state = GetComponent<NetworkCarState>();
        if (visualRoot == null)
            visualRoot = transform;
    }

    private void Update()
    {
        // Tant qu'on n'a pas encore de circuit, on essaie de prendre celui du joueur
        if (localCircuit == null && ARPlacementController.LocalCircuit != null)
        {
            localCircuit = ARPlacementController.LocalCircuit;
        }

        if (state == null || localCircuit == null || visualRoot == null)
            return;

        // IMPORTANT : ne pas override la physique du host
        if (IsServer) return;

        Vector3 localPos  = state.trackLocalPos.Value;
        Quaternion localRot = state.trackLocalRot.Value;

        // Reprojection dans le monde AR local
        Vector3 worldPos = localCircuit.TransformPoint(localPos);
        Quaternion worldRot = localCircuit.rotation * localRot;

        // On force la rotation à rester horizontale (seulement Y)
        Vector3 e = worldRot.eulerAngles;
        worldRot = Quaternion.Euler(0f, e.y, 0f);

        visualRoot.SetPositionAndRotation(worldPos, worldRot);
    }
}