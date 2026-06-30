using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

// Relay + session helpers for the Multiplayer Services SDK.
//
// IMPORTANT (learned from the SDK source):
//   * CreateSessionAsync(...WithRelayNetwork()) and JoinSessionByCodeAsync START NGO
//     themselves (host/client) — do NOT also call NetworkManager.StartHost/StartClient,
//     that double-start makes the next attempt see IsListening==true and fail.
//   * Tear a session down with ISession.LeaveAsync, NOT NetworkManager.Shutdown — the
//     SDK warns that a manual Shutdown leaves the session in a bad state.
//
// We keep the active ISession here so the world scene can leave it cleanly, and we
// always clear any stale session before starting a new one (retry safety).
public static class RelayConnector
{
    private static ISession s_ActiveSession;

    public static bool HasActiveSession => s_ActiveSession != null;

    public static async Task InitServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    // Creates a Relay-backed session (which starts NGO as host) and returns the join
    // code to share. Does NOT call StartHost — the SDK already did.
    public static async Task<string> CreateSessionAndHost(int maxPlayers)
    {
        await InitServices();
        await LeaveActiveSession();

        SessionOptions options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
        s_ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
        return s_ActiveSession.Code;
    }

    // Joins a session by code (which starts NGO as client). Does NOT call StartClient.
    public static async Task JoinByCodeAndConnect(string code)
    {
        await InitServices();
        await LeaveActiveSession();

        s_ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
    }

    // Leaves the current session (the SDK shuts NGO down internally). Safe to call when
    // there is no session. The IsListening fallback covers any NGO state left behind.
    public static async Task LeaveActiveSession()
    {
        if (s_ActiveSession != null)
        {
            try { await s_ActiveSession.LeaveAsync(); }
            catch (System.Exception e) { Debug.LogWarning($"RelayConnector.LeaveActiveSession: {e.Message}"); }
            s_ActiveSession = null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}
