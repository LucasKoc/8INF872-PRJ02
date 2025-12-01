using Unity.Netcode;
using UnityEngine;

public class NetworkCarState : NetworkBehaviour
{
    // Position de la voiture dans le repère du circuit du host
    public NetworkVariable<Vector3> trackLocalPos = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // rotation locale, si tu veux synchroniser l'orientation
    public NetworkVariable<Quaternion> trackLocalRot = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
}