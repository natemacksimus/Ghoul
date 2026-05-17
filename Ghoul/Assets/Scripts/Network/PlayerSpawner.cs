using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Place on any persistent scene GameObject (e.g. the NetworkManager GO).
// Assign playerPrefab and, optionally, spawnPoints.
// Make sure NetworkConfig.PlayerPrefab on the NetworkManager component is EMPTY
// when using this script, or players will be spawned twice.
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();
    private int spawnCount;

    private void Start()
    {
        if (NetworkManager.Singleton == null) { return; }
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) { return; }
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnServerStarted()
    {
        // Spawn the host/server player.
        SpawnPlayer(NetworkManager.Singleton.LocalClientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) { return; }
        // Host player was already spawned in OnServerStarted.
        if (NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId) { return; }
        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) { return; }
        if (spawnedPlayers.TryGetValue(clientId, out GameObject player))
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) { netObj.Despawn(true); }
            spawnedPlayers.Remove(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null) { Debug.LogError("PlayerSpawner: playerPrefab is not assigned."); return; }

        Vector3 spawnPos = Vector3.zero;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            spawnPos = spawnPoints[spawnCount % spawnPoints.Length].position;
        }
        spawnCount++;

        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        spawnedPlayers[clientId] = player;
    }
}
