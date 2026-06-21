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
// Canvas layout required:
//   buttonPanel      — parent panel shown before a session starts
//     hostButton     — creates a Relay session and displays the join code
//     clientButton   — joins using the code typed in joinCodeInput
//     serverButton   — local server (LAN / editor testing only, no Relay)
//     joinCodeInput  — InputField the client types the host's join code into
//   statusText       — Text shown below the panel (join code, errors, etc.)
//
// Max players is configurable via maxPlayers in the Inspector.
public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private GameObject buttonPanel;
    [SerializeField] private Text statusText;
    [SerializeField] private int maxPlayers = 4;

    private void Awake()
    {
        if (hostButton != null)   { hostButton.onClick.AddListener(() => _ = StartHostWithRelay()); }
        if (clientButton != null) { clientButton.onClick.AddListener(() => _ = StartClientWithRelay()); }
        if (serverButton != null) { serverButton.onClick.AddListener(StartServer); }
    }

    // -------------------------------------------------------------------------
    // Relay host — MultiplayerService creates the Relay allocation and returns
    // a join code; we then start NGO as host.
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
            HideButtons($"Join code:\n{session.Code}");
        }
        catch (Exception e)
        {
            SetStatus($"Host failed:\n{e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Relay client — MultiplayerService resolves the join code to a Relay
    // allocation; we then start NGO as a client.
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
            HideButtons("Connecting...");
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
        HideButtons("Server running...");
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

    private void HideButtons(string status)
    {
        if (buttonPanel != null) { buttonPanel.SetActive(false); }
        SetStatus(status);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) { statusText.text = msg; }
    }
}
