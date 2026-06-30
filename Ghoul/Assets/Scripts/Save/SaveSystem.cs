using System.IO;
using UnityEngine;

// Static JSON save/load for the 5 world slots. Host-only: clients joining a
// friend's world never call into this — the host owns the save file.
//
// Files live at  {Application.persistentDataPath}/Saves/slot_{0..4}.json
public static class SaveSystem
{
    public const int SlotCount = 5;

    private static string SaveDir => Path.Combine(Application.persistentDataPath, "Saves");

    private static string PathForSlot(int slot) => Path.Combine(SaveDir, $"slot_{slot}.json");

    private static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    public static bool Exists(int slot)
    {
        return IsValidSlot(slot) && File.Exists(PathForSlot(slot));
    }

    public static void Save(WorldSaveData data)
    {
        if (data == null || !IsValidSlot(data.slotIndex))
        {
            Debug.LogError("SaveSystem.Save: null data or invalid slot index.");
            return;
        }

        data.lastPlayedUtc = System.DateTime.UtcNow.ToString("o");

        Directory.CreateDirectory(SaveDir);
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(PathForSlot(data.slotIndex), json);
    }

    public static WorldSaveData Load(int slot)
    {
        if (!Exists(slot)) { return null; }

        try
        {
            string json = File.ReadAllText(PathForSlot(slot));
            WorldSaveData data = JsonUtility.FromJson<WorldSaveData>(json);
            if (data != null) { data.slotIndex = slot; }
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveSystem.Load: failed to read slot {slot}: {e.Message}");
            return null;
        }
    }

    public static void Delete(int slot)
    {
        if (Exists(slot)) { File.Delete(PathForSlot(slot)); }
    }

    // Cheap metadata for every slot, for the main-menu slot rows. Reads each file
    // (small) to pull the world name + last-played stamp without the caller
    // needing the full object list.
    public static SlotSummary[] GetAllSlots()
    {
        SlotSummary[] summaries = new SlotSummary[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            SlotSummary s = new SlotSummary { index = i, used = false };
            if (Exists(i))
            {
                WorldSaveData data = Load(i);
                if (data != null)
                {
                    s.used = true;
                    s.worldName = data.worldName;
                    s.lastPlayedUtc = data.lastPlayedUtc;
                }
            }
            summaries[i] = s;
        }
        return summaries;
    }
}
