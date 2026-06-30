// Implemented by any world object whose number/type/location should persist in a
// world save (NPCs, buildings, items, resources). The host's WorldObjectRegistry
// collects every active ISaveableWorldObject when saving, and WorldLoader recreates
// them from the catalog when a world loads.
public interface ISaveableWorldObject
{
    WorldObjectCategory Category { get; }

    // Stable key matching an entry in the WorldObjectCatalog (typeId -> prefab).
    string TypeId { get; }

    // Snapshot this object's persistent state into a record (position, quantity, etc.).
    WorldObjectRecord Capture();

    // Apply a previously saved record after the prefab has been instantiated.
    void Restore(WorldObjectRecord record);
}
