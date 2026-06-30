using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Server-side: when the active world's scene finishes loading over the network,
// recreate every saved world object (NPCs, buildings, items, resources) from the
// catalog and spawn them for all clients. Lives on the persistent NetworkManager
// GameObject alongside GameSession / WorldObjectRegistry.
public class WorldLoader : MonoBehaviour
{
    [SerializeField] private WorldObjectCatalog catalog;

    // Default content spawned when a brand-new world is created. Each entry is a
    // typeId from the catalog plus where to place it (relative to the world start).
    // The world setup tool populates this with a few sample NPCs/buildings/resources.
    [SerializeField] private WorldObjectRecord[] newWorldSeed;

    private bool hooked;

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += HookSceneEvents;
            NetworkManager.Singleton.OnServerStopped += UnhookSceneEvents;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HookSceneEvents;
            NetworkManager.Singleton.OnServerStopped -= UnhookSceneEvents;
        }
        UnhookSceneEvents(false);
    }

    private void HookSceneEvents()
    {
        if (hooked || NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) { return; }
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadComplete;
        hooked = true;
    }

    private void UnhookSceneEvents(bool _)
    {
        if (!hooked || NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) { return; }
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnLoadComplete;
        hooked = false;
    }

    // Fires once per client as each finishes loading a networked scene. We only act
    // on the server's own completion, so world objects are spawned a single time.
    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) { return; }
        if (clientId != nm.LocalClientId) { return; }

        WorldSaveData world = GameSession.HasInstance ? GameSession.Instance.ActiveWorld : null;
        if (world == null) { return; }
        if (sceneName != world.startScene) { return; }

        if (catalog == null)
        {
            Debug.LogWarning("WorldLoader: no WorldObjectCatalog assigned — world content will not be created.");
            return;
        }

        bool isNew = GameSession.HasInstance && GameSession.Instance.IsNewWorld;
        if (isNew)
        {
            // Generate the new world: spawn the default seed, each at start + offset.
            if (newWorldSeed != null)
            {
                foreach (WorldObjectRecord seed in newWorldSeed)
                {
                    SpawnRecord(seed, world.startPosition + seed.position);
                }
            }
        }
        else
        {
            // Restore an existing world from its saved records (absolute positions).
            if (world.objects != null)
            {
                foreach (WorldObjectRecord record in world.objects)
                {
                    SpawnRecord(record, record.position);
                }
            }
        }
    }

    private void SpawnRecord(WorldObjectRecord record, Vector3 position)
    {
        if (record == null) { return; }
        if (!catalog.TryGetPrefab(record.typeId, out GameObject prefab))
        {
            Debug.LogWarning($"WorldLoader: no catalog entry for typeId '{record.typeId}' — skipped.");
            return;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);

        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj != null) { netObj.Spawn(destroyWithScene: true); }

        ISaveableWorldObject saveable = instance.GetComponent<ISaveableWorldObject>();
        if (saveable != null)
        {
            // Restore() expects the record's own position; pass a copy placed correctly.
            WorldObjectRecord applied = new WorldObjectRecord
            {
                category = record.category,
                typeId = record.typeId,
                position = position,
                quantity = record.quantity,
                stateJson = record.stateJson,
            };
            saveable.Restore(applied);
        }
    }
}
