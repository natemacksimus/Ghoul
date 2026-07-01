using System.Collections.Generic;
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

    // The code a client joined with, remembered so a dropped client can rejoin the
    // same world (see ReconnectActiveSession). Null on the host / when not joined.
    private static string s_LastJoinCode;

    public static bool HasActiveSession => s_ActiveSession != null;

    // The id of the active session, used to confirm membership before reconnecting.
    public static string ActiveSessionId => s_ActiveSession?.Id;

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
        s_LastJoinCode = code;
    }

    // Attempts to restore a client connection that dropped unexpectedly, following the
    // Multiplayer Services "reconnect to a session" flow
    // (https://docs.unity.com/en-us/mps-sdk/join-session):
    //   1. Confirm we're still a member of the session (GetJoinedSessionIdsAsync).
    //   2. Reconnect the retained session handle (ISession.ReconnectAsync).
    // If the transport doesn't come back from the lobby-level reconnect alone (the usual
    // case after a hard NGO drop), we fully rejoin by the saved code to re-establish the
    // NGO client + Relay. Returns true once NGO is connected/listening again.
    //
    // NOTE: we use ISession.ReconnectAsync() rather than the docs'
    // MultiplayerService.ReconnectToSessionAsync(sessionId) because that path requires a
    // session Type, which CreateSessionAndHost/JoinByCodeAndConnect don't set.
    public static async Task<bool> ReconnectActiveSession()
    {
        if (s_ActiveSession == null && string.IsNullOrEmpty(s_LastJoinCode)) { return false; }

        try
        {
            await InitServices();

            string sessionId = s_ActiveSession?.Id;
            if (!string.IsNullOrEmpty(sessionId))
            {
                // Step 1: only reconnect if the service still lists us as a member
                // (a player removed by the host or service must rejoin from scratch).
                List<string> joined = await MultiplayerService.Instance.GetJoinedSessionIdsAsync();
                if (joined != null && joined.Contains(sessionId))
                {
                    // Step 2: reconnect the existing handle.
                    try { await s_ActiveSession.ReconnectAsync(); }
                    catch (System.Exception e) { Debug.LogWarning($"RelayConnector.ReconnectActiveSession (handle): {e.Message}"); }
                }
            }

            // If the transport recovered on its own, we're done.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                return true;
            }

            // Otherwise rebuild the NGO client + Relay by rejoining with the saved code.
            if (!string.IsNullOrEmpty(s_LastJoinCode))
            {
                await JoinByCodeAndConnect(s_LastJoinCode);
                return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"RelayConnector.ReconnectActiveSession failed: {e.Message}");
        }

        return false;
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

        s_LastJoinCode = null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}
