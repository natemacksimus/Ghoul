using System.IO;

/// <summary>
/// Implement on every MonoBehaviour that holds gameplay state that must be
/// captured before speculative simulation and restored on rollback.
///
/// Rules:
///  - SaveState and LoadState must write/read the same fields in the same order.
///  - Do NOT write reference types (GameObjects, Components). Serialize only
///    value-type fields (float, int, bool, Vector2, etc.) that affect simulation.
///  - transform.position must be included by whichever class owns the character's
///    position (EntityController does this).
/// </summary>
public interface ISnapshotable
{
    void SaveState(BinaryWriter writer);
    void LoadState(BinaryReader reader);
}

/// <summary>
/// Implemented by PlayerController. Called by RollbackSession each frame with the
/// confirmed (or predicted) inputs for that player.
/// </summary>
public interface IRollbackSimulated
{
    void SimulateFrame(RollbackInput input);
}
