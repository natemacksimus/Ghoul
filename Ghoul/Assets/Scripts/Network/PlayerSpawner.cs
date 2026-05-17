using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Attach to a persistent NetworkObject in the scene (e.g. a "NetworkManager" GameObject).
// Assign playerPrefab (the Player prefab with NetworkObject + ClientNetworkTransform)
// and optionally set spawnPoints to control where each player appears.
public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Spawn the host's player immediately.
        SpawnPlayer(NetworkManager.ServerClientId);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) { return; }
        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (spawnedPlayers.TryGetValue(clientId, out GameObject player))
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) { netObj.Despawn(true); }
            spawnedPlayers.Remove(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: playerPrefab is not assigned.");
            return;
        }

        Vector3 spawnPos = Vector3.zero;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = spawnedPlayers.Count % spawnPoints.Length;
            spawnPos = spawnPoints[index].position;
        }

        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        spawnedPlayers[clientId] = player;
    }
}
