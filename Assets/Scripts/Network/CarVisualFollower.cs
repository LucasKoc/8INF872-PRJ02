using UnityEngine;
using Unity.Netcode;

public class CarVisualFollower : NetworkBehaviour
{
    [Header("Références")]
    public Transform localCircuit;    // assigné par ARPlacementController
    public Transform visualRoot;      // ce qu’on bouge visuellement (souvent le même que transform)

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

        // Si toujours rien → on ne fait rien (circuit pas encore posé)
        if (state == null || localCircuit == null || visualRoot == null)
            return;

        // IMPORTANT : ne pas override la physique du host
        if (IsServer)
            return;  // le host laisse SimpleCarController gérer la voiture

        // Position / rotation locales communes
        Vector3 localPos = state.trackLocalPos.Value;
        Quaternion localRot = state.trackLocalRot.Value;

        // Reprojection dans NOTRE monde AR local
        visualRoot.position = localCircuit.TransformPoint(localPos);
        visualRoot.rotation = localCircuit.rotation * localRot;
    }
}