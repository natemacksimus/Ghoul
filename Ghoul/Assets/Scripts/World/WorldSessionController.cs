using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lives in the world scene. Owns "leave the world" behaviour:
//   - Host presses Exit World  -> capture + save the world, shut down the network
//     (which disconnects everyone), then return to the main menu.
//   - A client losing the host  -> NGO reports the local client stopped, and we
//     send that player back to the main menu too.
//
// Returning to the menu is funnelled through ReturnToMenu() with a guard so it
// only happens once, even though host shutdown raises several callbacks.
public class WorldSessionController : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private Text joinCodeText;   // host-only: shows the shareable code
    [SerializeField] private Button exitButton;   // "Exit World"

    private bool returning;

    private void Start()
    {
        // Show the join code to the host (clients have no code / aren't hosting).
        if (joinCodeText != null)
        {
            bool isHost = GameSession.HasInstance && GameSession.Instance.IsHostingWorld;
            string code = GameSession.HasInstance ? GameSession.Instance.JoinCode : null;
            joinCodeText.text = isHost && !string.IsNullOrEmpty(code) ? $"Code: {code}" : string.Empty;
        }
    }

    private void OnEnable()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitWorld);
            exitButton.onClick.AddListener(ExitWorld);
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientStopped += OnClientStopped;
            nm.OnServerStopped += OnServerStopped;
        }
    }

    private void OnDisable()
    {
        if (exitButton != null) { exitButton.onClick.RemoveListener(ExitWorld); }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientStopped -= OnClientStopped;
            nm.OnServerStopped -= OnServerStopped;
        }
    }

    // Hook this to the world scene's "Exit World" button.
    public void ExitWorld()
    {
        if (returning) { return; }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer)
        {
            // Host owns the save — persist current world content before tearing down.
            SaveCurrentWorld();
        }

        if (RelayConnector.HasActiveSession || (nm != null && nm.IsListening))
        {
            // Leaving the session shuts NGO down, which raises OnClientStopped /
            // OnServerStopped, and ReturnToMenu runs from there.
            _ = RelayConnector.LeaveActiveSession();
        }
        else
        {
            ReturnToMenu();
        }
    }

    private void SaveCurrentWorld()
    {
        if (!GameSession.HasInstance) { return; }
        WorldSaveData world = GameSession.Instance.ActiveWorld;
        if (world == null) { return; }

        if (WorldObjectRegistry.Instance != null)
        {
            world.objects = WorldObjectRegistry.Instance.CaptureAll();
        }
        SaveSystem.Save(world);
    }

    private void OnClientStopped(bool wasHost) => ReturnToMenu();

    private void OnServerStopped(bool wasHost) => ReturnToMenu();

    private void ReturnToMenu()
    {
        if (returning) { return; }
        returning = true;

        if (GameSession.HasInstance) { GameSession.Instance.Clear(); }

        // Network is down at this point, so load the menu locally (not networked).
        SceneManager.LoadScene(mainMenuScene);
    }
}
