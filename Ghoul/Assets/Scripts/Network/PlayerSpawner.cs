using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Spawns one player object per connected client (server side). Supports two flows:
//
//  * World flow (GameSession has an ActiveWorld): the host starts in the MainMenu
//    scene, so we must NOT spawn on OnServerStarted — players are spawned once the
//    networked world scene finishes loading for each client. Spawn position comes
//    from the world's per-world start location.
//
//  * Legacy flow (no GameSession / no ActiveWorld): single-scene setup from
//    Tools/Multiplayer/Setup Scene — spawn the host on OnServerStarted and each
//    client on connect, using the scene's spawnPoints.
//
// Lives on the persistent NetworkManager GameObject in the world flow.
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float playerSpacing = 1.5f;

    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();
    private readonly HashSet<int> usedColorIndices = new();
    private readonly Dictionary<ulong, int> clientColorIndices = new();
    private int spawnCount;

    private bool worldSceneLoaded;
    private bool sceneEventsHooked;

    private static bool UseWorldFlow => GameSession.HasInstance && GameSession.Instance.ActiveWorld != null;

    private void Start()
    {
        if (NetworkManager.Singleton == null) { return; }
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) { return; }
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        UnhookSceneEvents();
    }

    private void OnServerStarted()
    {
        if (UseWorldFlow)
        {
            // Defer spawning until the world scene has loaded for each client.
            HookSceneEvents();
        }
        else
        {
            SpawnPlayer(NetworkManager.Singleton.LocalClientId);
        }
    }

    private void OnServerStopped(bool wasHost)
    {
        // Reset so the next hosted session (same persistent NetworkManager) starts clean.
        UnhookSceneEvents();
        worldSceneLoaded = false;
        spawnedPlayers.Clear();
        usedColorIndices.Clear();
        clientColorIndices.Clear();
        spawnCount = 0;
    }

    private void HookSceneEvents()
    {
        if (sceneEventsHooked || NetworkManager.Singleton.SceneManager == null) { return; }
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadComplete;
        sceneEventsHooked = true;
    }

    private void UnhookSceneEvents()
    {
        if (!sceneEventsHooked || NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) { return; }
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnLoadComplete;
        sceneEventsHooked = false;
    }

    // World flow: each client (host included) gets a player once it finishes loading
    // the world scene.
    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (!NetworkManager.Singleton.IsServer || !UseWorldFlow) { return; }
        if (sceneName != GameSession.Instance.ActiveWorld.startScene) { return; }

        worldSceneLoaded = true;
        if (!spawnedPlayers.ContainsKey(clientId)) { SpawnPlayer(clientId); }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) { return; }

        if (UseWorldFlow)
        {
            // Late joiners arriving after the world is already loaded. Clients present
            // during the initial load are handled by OnLoadComplete.
            if (worldSceneLoaded && !spawnedPlayers.ContainsKey(clientId)) { SpawnPlayer(clientId); }
            return;
        }

        // Legacy flow — host already spawned in OnServerStarted.
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
        if (clientColorIndices.TryGetValue(clientId, out int idx))
        {
            usedColorIndices.Remove(idx);
            clientColorIndices.Remove(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null) { Debug.LogError("PlayerSpawner: playerPrefab is not assigned."); return; }

        GameObject player = Instantiate(playerPrefab, ResolveSpawnPosition(), Quaternion.identity);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        spawnedPlayers[clientId] = player;
        spawnCount++;

        int colorIndex = PlayerColorSync.NextColorIndex(usedColorIndices);
        usedColorIndices.Add(colorIndex);
        clientColorIndices[clientId] = colorIndex;
        PlayerColorSync colorSync = player.GetComponent<PlayerColorSync>();
        if (colorSync != null) { colorSync.AssignColorIndex(colorIndex); }
    }

    private Vector3 ResolveSpawnPosition()
    {
        if (UseWorldFlow)
        {
            WorldSaveData world = GameSession.Instance.ActiveWorld;
            Vector3 basePos;
            if (GameSession.Instance.IsNewWorld && WorldStartPoint.Instance != null)
            {
                // First entry into a new world: adopt the scene's start point and
                // persist it so future restores spawn players in the same place.
                basePos = WorldStartPoint.Instance.transform.position;
                world.startPosition = basePos;
            }
            else
            {
                basePos = world.startPosition;
            }
            return basePos + new Vector3(spawnCount * playerSpacing, 0f, 0f);
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[spawnCount % spawnPoints.Length].position;
        }
        return Vector3.zero + new Vector3(spawnCount * playerSpacing, 0f, 0f);
    }
}
