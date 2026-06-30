using Unity.Netcode;
using UnityEngine;

// Base for networked world content that persists in a world save. Handles server-side
// registration with WorldObjectRegistry and the default position capture/restore.
// Subclasses set their category and may add extra state (e.g. resource amount).
public abstract class SaveableWorldEntity : NetworkBehaviour, ISaveableWorldObject
{
    [SerializeField] protected string typeId;

    public abstract WorldObjectCategory Category { get; }
    public string TypeId => typeId;

    public override void OnNetworkSpawn()
    {
        if (IsServer && WorldObjectRegistry.Instance != null)
        {
            WorldObjectRegistry.Instance.Register(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && WorldObjectRegistry.Instance != null)
        {
            WorldObjectRegistry.Instance.Unregister(this);
        }
    }

    public virtual WorldObjectRecord Capture()
    {
        return new WorldObjectRecord
        {
            category = Category,
            typeId = typeId,
            position = transform.position,
            quantity = 1,
        };
    }

    public virtual void Restore(WorldObjectRecord record)
    {
        if (record != null) { transform.position = record.position; }
    }
}
