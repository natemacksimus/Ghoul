using UnityEngine;

// Carries the active world across the MainMenu -> World scene boundary (and back).
// Lives on the persistent NetworkManager GameObject created in the MainMenu scene.
// Reuses the project's PersistentSingleton so it survives scene loads.
public class GameSession : PersistentSingleton<GameSession>
{
    // The world currently being hosted/restored. Null while at the menu with no
    // selection. Only meaningful on the host — clients joining a friend's world
    // leave this null and receive world content over the network.
    public WorldSaveData ActiveWorld { get; private set; }

    // True when this player created/restored the world (host). False for join-by-code.
    public bool IsHostingWorld { get; private set; }

    // True for a freshly created world (seed default content) vs a restored one
    // (respawn saved content). Lets WorldLoader pick seeding vs restoring.
    public bool IsNewWorld { get; private set; }

    // Relay join code for the active hosted session, surfaced in the world scene so
    // the host can keep sharing it after the menu unloads.
    public string JoinCode { get; private set; }

    public void SetJoinCode(string code) => JoinCode = code;

    // A one-shot message for the main menu to show after returning from a world (e.g.
    // "The host closed the world."). Survives Clear(); MainMenuUI consumes it.
    public string PendingMenuMessage { get; set; }

    public string ConsumeMenuMessage()
    {
        string msg = PendingMenuMessage;
        PendingMenuMessage = null;
        return msg;
    }

    public void BeginHosting(WorldSaveData world, bool isNew)
    {
        ActiveWorld = world;
        IsHostingWorld = true;
        IsNewWorld = isNew;
    }

    public void BeginJoining()
    {
        ActiveWorld = null;
        IsHostingWorld = false;
        IsNewWorld = false;
    }

    public void Clear()
    {
        ActiveWorld = null;
        IsHostingWorld = false;
        IsNewWorld = false;
        JoinCode = null;
    }
}
