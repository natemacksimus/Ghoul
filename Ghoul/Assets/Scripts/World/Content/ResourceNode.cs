using Unity.Netcode;
using UnityEngine;

// Minimal sample resource node (e.g. an ore vein / tree). Persists its type, location,
// and remaining amount. The amount is a server-owned NetworkVariable so all clients
// see the same value and it can be saved/restored.
public class ResourceNode : SaveableWorldEntity
{
    [SerializeField] private int startingAmount = 10;

    private readonly NetworkVariable<int> amount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override WorldObjectCategory Category => WorldObjectCategory.Resource;

    public int Amount => amount.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // New nodes (not yet restored from a save) start full.
        if (IsServer && amount.Value == 0) { amount.Value = startingAmount; }
    }

    // Server-side harvest hook for later gameplay.
    public void Harvest(int qty)
    {
        if (!IsServer) { return; }
        amount.Value = Mathf.Max(0, amount.Value - qty);
    }

    public override WorldObjectRecord Capture()
    {
        WorldObjectRecord record = base.Capture();
        record.quantity = amount.Value;
        return record;
    }

    public override void Restore(WorldObjectRecord record)
    {
        base.Restore(record);
        if (IsServer && record != null) { amount.Value = record.quantity; }
    }

    private void Reset()
    {
        if (string.IsNullOrEmpty(typeId)) { typeId = "resource_basic"; }
    }
}
