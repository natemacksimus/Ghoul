using UnityEngine;

// Minimal sample NPC. Persists its type and location via the base class.
public class NpcEntity : SaveableWorldEntity
{
    public override WorldObjectCategory Category => WorldObjectCategory.Npc;

    private void Reset()
    {
        if (string.IsNullOrEmpty(typeId)) { typeId = "npc_basic"; }
    }
}
