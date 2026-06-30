using System;
using System.Collections.Generic;
using UnityEngine;

// Plain serializable model for a single saved world (one save slot).
// Serialized with JsonUtility, which handles Vector3, List<T>, and enums.
//
// "Number, type, and location" of world content is captured by the `objects`
// list: count = number of records, typeId = type (key into WorldObjectCatalog),
// position = location. Per-object extras (resource amount, building health, etc.)
// ride along in quantity / stateJson.

public enum WorldObjectCategory
{
    Npc,
    Building,
    Item,
    Resource,
}

[Serializable]
public class WorldObjectRecord
{
    public WorldObjectCategory category;
    public string typeId;
    public Vector3 position;
    public int quantity = 1;
    public string stateJson;
}

[Serializable]
public class WorldSaveData
{
    public int slotIndex;
    public string worldName;
    public string createdUtc;
    public string lastPlayedUtc;

    // A world is "1 or more scenes." scenes[0] / startScene is loaded on entry;
    // the list leaves room for additional scenes per world later.
    public string[] scenes = { "World_Main" };
    public string startScene = "World_Main";

    // Per-world player start location.
    public Vector3 startPosition = Vector3.zero;

    public List<WorldObjectRecord> objects = new List<WorldObjectRecord>();

    public static WorldSaveData CreateNew(int slot, string name, Vector3 startPosition)
    {
        string nowUtc = DateTime.UtcNow.ToString("o");
        return new WorldSaveData
        {
            slotIndex = slot,
            worldName = string.IsNullOrWhiteSpace(name) ? $"World {slot + 1}" : name,
            createdUtc = nowUtc,
            lastPlayedUtc = nowUtc,
            startPosition = startPosition,
            objects = new List<WorldObjectRecord>(),
        };
    }
}

// Lightweight per-slot summary for the main menu (avoids loading full object lists).
[Serializable]
public struct SlotSummary
{
    public int index;
    public bool used;
    public string worldName;
    public string lastPlayedUtc;
}
