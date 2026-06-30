using System.Collections.Generic;
using UnityEngine;

// Server-side runtime list of every saveable world object currently spawned.
// Lives on the persistent NetworkManager GameObject. Saveable objects add/remove
// themselves as they spawn/despawn; the host calls CaptureAll() at save time.
//
// Players are NOT registered here — their start location is per-world data, and
// their live positions belong to the netcode transforms, not the world save.
public class WorldObjectRegistry : MonoBehaviour
{
    public static WorldObjectRegistry Instance { get; private set; }

    private readonly HashSet<ISaveableWorldObject> tracked = new HashSet<ISaveableWorldObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }

    public void Register(ISaveableWorldObject obj)
    {
        if (obj != null) { tracked.Add(obj); }
    }

    public void Unregister(ISaveableWorldObject obj)
    {
        if (obj != null) { tracked.Remove(obj); }
    }

    // Snapshot all tracked objects into save records (host only).
    public List<WorldObjectRecord> CaptureAll()
    {
        List<WorldObjectRecord> records = new List<WorldObjectRecord>(tracked.Count);
        foreach (ISaveableWorldObject obj in tracked)
        {
            if (obj == null) { continue; }
            WorldObjectRecord record = obj.Capture();
            if (record != null) { records.Add(record); }
        }
        return records;
    }
}
