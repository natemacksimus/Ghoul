using System.Collections.Generic;
using UnityEngine;

// Maps a saved object's typeId to the prefab used to recreate it. Each prefab must
// have a NetworkObject (so the host can spawn it for all clients) and a component
// implementing ISaveableWorldObject. Every prefab listed here must also be
// registered in the NetworkManager's network prefab list.
//
// Create via  Assets > Create > Ghoul > World Object Catalog
// (the world setup tool also generates one at Assets/Prefabs/WorldObjectCatalog.asset).
[CreateAssetMenu(fileName = "WorldObjectCatalog", menuName = "Ghoul/World Object Catalog")]
public class WorldObjectCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string typeId;
        public GameObject prefab;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, GameObject> lookup;

    private void BuildLookup()
    {
        lookup = new Dictionary<string, GameObject>();
        foreach (Entry e in entries)
        {
            if (!string.IsNullOrEmpty(e.typeId) && e.prefab != null && !lookup.ContainsKey(e.typeId))
            {
                lookup.Add(e.typeId, e.prefab);
            }
        }
    }

    public bool TryGetPrefab(string typeId, out GameObject prefab)
    {
        if (lookup == null) { BuildLookup(); }
        return lookup.TryGetValue(typeId ?? string.Empty, out prefab);
    }

    public IReadOnlyList<Entry> Entries => entries;
}
