using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lives in the world scene. Owns "leave the world" behaviour:
//   - Host presses Exit World  -> capture + save the world, shut down the network
//     (which disconnects everyone), then return to the main menu.
//   - A client dropping unexpectedly -> try to reconnect to the same session a few
//     times (see Unity MPS "reconnect to a session"); only fall back to the main
//     menu if every attempt fails.
//   - A deliberate Exit (or the host going away) -> return to the main menu.
//
// Returning to the menu is funnelled through ReturnToMenu() with a guard so it
// only happens once, even though host shutdown raises several callbacks.
public class WorldSessionController : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private Text joinCodeText;   // host-only: shows the shareable code
    [SerializeField] private Button copyCodeButton; // host-only: copies the code to the system clipboard
    [SerializeField] private float copiedFeedbackSeconds = 1.5f;
    [SerializeField] private Button exitButton;   // "Exit World"

    [Header("Reconnect (client only)")]
    [SerializeField] private int reconnectAttempts = 3;
    [SerializeField] private float reconnectDelaySeconds = 2f;
    [SerializeField] private GameObject reconnectingOverlay; // hidden until a drop
    [SerializeField] private Text reconnectingLabel;

    private bool returning;
    private bool reconnecting;
    private bool intentionalExit;  // set when the player deliberately leaves the world

    private Text copyCodeLabel;
    private string copyCodeLabelDefault;
    private Coroutine copiedFeedback;

    private void Start()
    {
        if (reconnectingOverlay != null) { reconnectingOverlay.SetActive(false); }

        // Show the join code (and its Copy button) to the host only — clients have no
        // code / aren't hosting.
        bool isHost = GameSession.HasInstance && GameSession.Instance.IsHostingWorld;
        string code = GameSession.HasInstance ? GameSession.Instance.JoinCode : null;
        bool showCode = isHost && !string.IsNullOrEmpty(code);

        if (joinCodeText != null)
        {
            joinCodeText.text = showCode ? $"Code: {code}" : string.Empty;
        }
        if (copyCodeButton != null)
        {
            copyCodeButton.gameObject.SetActive(showCode);
            copyCodeLabel = copyCodeButton.GetComponentInChildren<Text>();
            if (copyCodeLabel != null) { copyCodeLabelDefault = copyCodeLabel.text; }
        }
    }

    // Puts the bare join code (no "Code: " prefix) on the OS clipboard so it can be
    // pasted into Discord, a browser, etc.
    public void CopyJoinCode()
    {
        string code = GameSession.HasInstance ? GameSession.Instance.JoinCode : null;
        if (string.IsNullOrEmpty(code)) { return; }

        GUIUtility.systemCopyBuffer = code;

        if (copyCodeLabel != null)
        {
            if (copiedFeedback != null) { StopCoroutine(copiedFeedback); }
            copiedFeedback = StartCoroutine(ShowCopiedFeedback());
        }
    }

    // Realtime wait so the label still resets if the offline pause menu has frozen timeScale.
    private System.Collections.IEnumerator ShowCopiedFeedback()
    {
        copyCodeLabel.text = "Copied!";
        yield return new WaitForSecondsRealtime(copiedFeedbackSeconds);
        copyCodeLabel.text = copyCodeLabelDefault;
        copiedFeedback = null;
    }

    private void OnEnable()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitWorld);
            exitButton.onClick.AddListener(ExitWorld);
        }
        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.RemoveListener(CopyJoinCode);
            copyCodeButton.onClick.AddListener(CopyJoinCode);
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientStopped += OnClientStopped;
            nm.OnServerStopped += OnServerStopped;
        }

        Application.quitting += OnApplicationQuitting;
    }

    private void OnDisable()
    {
        if (exitButton != null) { exitButton.onClick.RemoveListener(ExitWorld); }
        if (copyCodeButton != null) { copyCodeButton.onClick.RemoveListener(CopyJoinCode); }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientStopped -= OnClientStopped;
            nm.OnServerStopped -= OnServerStopped;
        }

        Application.quitting -= OnApplicationQuitting;
    }

    // Quitting the app (or exiting Play mode in the Editor) shuts NGO down too, which
    // raises OnClientStopped. That's not a dropped connection, so don't try to reconnect.
    private void OnApplicationQuitting() => intentionalExit = true;

    // Hook this to the world scene's "Exit World" button.
    public void ExitWorld()
    {
        if (returning) { return; }

        // Mark this as a deliberate leave so the resulting OnClientStopped doesn't
        // get mistaken for a dropped connection and trigger a reconnect.
        intentionalExit = true;

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

    private void OnClientStopped(bool wasHost)
    {
        // A deliberate Exit, or the host's own client stopping, just goes to the menu.
        // A pure client dropping unexpectedly first tries to reconnect.
        if (intentionalExit || wasHost) { ReturnToMenu(); }
        else { _ = ReconnectOrReturn(); }
    }

    private void OnServerStopped(bool wasHost) => ReturnToMenu();

    // Attempts to rejoin the same session a few times before giving up and returning
    // to the menu. On success the host re-synchronises this client into the world
    // scene (a fresh networked load), which replaces this controller — so there's
    // nothing more to do here once a reconnect lands.
    private async Task ReconnectOrReturn()
    {
        if (returning || reconnecting) { return; }
        reconnecting = true;

        int attempts = Mathf.Max(1, reconnectAttempts);
        for (int attempt = 1; attempt <= attempts && !returning; attempt++)
        {
            ShowReconnecting($"Connection lost.\nReconnecting… ({attempt}/{attempts})");

            if (await RelayConnector.ReconnectActiveSession())
            {
                if (this == null) { return; }  // scene already reloaded by host sync
                reconnecting = false;
                HideReconnecting();
                return;
            }

            if (this == null) { return; }
            if (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.1f, reconnectDelaySeconds)));
                if (this == null) { return; }
            }
        }

        reconnecting = false;
        HideReconnecting();
        ReturnToMenu();
    }

    private void ShowReconnecting(string message)
    {
        if (reconnectingOverlay != null) { reconnectingOverlay.SetActive(true); }
        if (reconnectingLabel != null) { reconnectingLabel.text = message; }
    }

    private void HideReconnecting()
    {
        if (reconnectingOverlay != null) { reconnectingOverlay.SetActive(false); }
    }

    private void ReturnToMenu()
    {
        if (returning) { return; }
        returning = true;

        if (GameSession.HasInstance) { GameSession.Instance.Clear(); }

        // Network is down at this point, so load the menu locally (not networked).
        SceneManager.LoadScene(mainMenuScene);
    }
}
