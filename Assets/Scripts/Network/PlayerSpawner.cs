using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject carPrefab;
    public Transform[] spawnPoints;

    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        int index = (int)(clientId % (ulong)spawnPoints.Length);

        GameObject obj = Instantiate(carPrefab, spawnPoints[index].position, spawnPoints[index].rotation);

        // 👇 SUPER IMPORTANT
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}