using UnityEngine;

// Minimal sample building. Persists its type and location via the base class.
public class BuildingEntity : SaveableWorldEntity
{
    public override WorldObjectCategory Category => WorldObjectCategory.Building;

    private void Reset()
    {
        if (string.IsNullOrEmpty(typeId)) { typeId = "building_basic"; }
    }
}
