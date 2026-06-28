using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

// Lobby UI with Unity Multiplayer Services (com.unity.services.multiplayer) Relay support.
//
// Canvas layout (built by Tools > Multiplayer > Setup Scene):
//   buttonPanel            — outer panel, always visible until a client session starts
//     lobbyView            — group containing input + buttons, hidden after hosting
//       joinCodeInputLabel — instructs the client to type a code here
//       joinCodeInput      — InputField the client types the host's join code into
//       hostButton         — creates a Relay session
//       clientButton       — joins using the code in joinCodeInput
//       serverButton       — local server (LAN / editor testing only)
//     codeDisplay          — shown after hosting; high-contrast background + join code
//       joinCodeText       — the 6-character code to share
//   statusText             — error / connecting messages outside the panel
//
// Max players is configurable via maxPlayers in the Inspector.
public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private GameObject buttonPanel;
    [SerializeField] private GameObject lobbyView;
    [SerializeField] private GameObject codeDisplay;
    [SerializeField] private Text joinCodeText;
    [SerializeField] private Text statusText;
    [SerializeField] private int maxPlayers = 4;

    private void Awake()
    {
        // Fall back to finding by name if the inspector references weren't wired.
        if (lobbyView == null && buttonPanel != null)
            lobbyView = buttonPanel.transform.Find("LobbyView")?.gameObject;
        if (codeDisplay == null && buttonPanel != null)
            codeDisplay = buttonPanel.transform.Find("CodeDisplay")?.gameObject;

        if (lobbyView != null)   { lobbyView.SetActive(true); }
        if (codeDisplay != null) { codeDisplay.SetActive(false); }

        if (hostButton != null)   { hostButton.onClick.AddListener(() => _ = StartHostWithRelay()); }
        if (clientButton != null) { clientButton.onClick.AddListener(() => _ = StartClientWithRelay()); }
        if (serverButton != null) { serverButton.onClick.AddListener(StartServer); }
    }

    // -------------------------------------------------------------------------
    // Relay host — session is created, lobby swaps to a high-contrast code display.
    // -------------------------------------------------------------------------

    private async Task StartHostWithRelay()
    {
        SetStatus("Signing in...");
        try
        {
            await InitServices();

            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);

            NetworkManager.Singleton.StartHost();

            // Swap lobby view for the code display inside the same panel.
            if (lobbyView != null)   { lobbyView.SetActive(false); }
            if (codeDisplay != null) { codeDisplay.SetActive(true); }
            if (joinCodeText != null){ joinCodeText.text = session.Code; }
            SetStatus(string.Empty);
        }
        catch (Exception e)
        {
            SetStatus($"Host failed:\n{e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Relay client — resolves the join code and starts NGO as a client.
    // -------------------------------------------------------------------------

    private async Task StartClientWithRelay()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : string.Empty;
        if (string.IsNullOrEmpty(code)) { SetStatus("Enter a join code first."); return; }

        SetStatus("Connecting...");
        try
        {
            await InitServices();

            await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            NetworkManager.Singleton.StartClient();
            if (buttonPanel != null) { buttonPanel.SetActive(false); }
            SetStatus("Connected!");
        }
        catch (SessionException e)
        {
            SetStatus($"Join failed:\n{e.Message}");
        }
        catch (Exception e)
        {
            SetStatus($"Error:\n{e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Local server — no Relay, useful for LAN play or editor testing.
    // -------------------------------------------------------------------------

    private void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        if (buttonPanel != null) { buttonPanel.SetActive(false); }
        SetStatus("Server running...");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task InitServices()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) { statusText.text = msg; }
    }
}
