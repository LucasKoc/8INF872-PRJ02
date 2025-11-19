using Unity.Netcode;
using UnityEngine;

public class PlayerControllerNetworked : NetworkBehaviour {
    public float speed = 5f;
    private Vector2 input;

    void Update() {
        if (!IsOwner) return; // seul le propriétaire applique son input localement
        input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        // Appliquer le mouvement localement (prediction)
        transform.position += (Vector3)input * speed * Time.deltaTime;
        // Envoyer l'input aux autres pairs
        SubmitInputServerRpc(input, NetworkManager.LocalClientId);
    }

    // Ici on envoie à *un pair responsable* : dans un layout P2P on pourrait
    // appeler un RPC broadcast vers tous les autres pour qu'ils appliquent l'état.
    [ServerRpc(RequireOwnership = false)]
    void SubmitInputServerRpc(Vector2 input, ulong clientId) {
        // Si tu es en host, tu es serveur ici. Sinon, tu dois relayer/forwarder.
        // Broadcast de l'input aux autres clients (les pairs)
        ApplyInputClientRpc(input, clientId);
    }

    [ClientRpc]
    void ApplyInputClientRpc(Vector2 input, ulong clientId) {
        // Si on n'est pas le propriétaire, on applique l'update reçu pour SMB.
        if (NetworkManager.LocalClientId == clientId) return;
        // Simple application (tu ajouteras interpolation/reconciliation)
        transform.position += (Vector3)input * speed * Time.deltaTime;
    }
}