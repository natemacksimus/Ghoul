---
name: project-ghoul-save-system
description: "World save system + main menu + networked world sessions for Ghoul (5 slots, host-authoritative, NGO scene management)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 13fab25e-bd1b-486c-b50b-451de29f7c95
---

World save system added (2026-06-29). Builds on [[project-ghoul-multiplayer]] (NGO 2.1.1 + Relay join codes).

**Goal:** MainMenu with 5 save slots; each slot = one persistent world. Create/restore a world => you Host (Relay code generated). Join-by-code path for clients. Host exits => everyone returns to MainMenu. Saves persist number/type/location of NPCs, buildings, items, resources + per-world player start.

**Architecture:**
- Persistent `NetworkManager` lives ONLY in MainMenu (DontDestroyOnLoad). Host starts Relay there, then `NetworkManager.SceneManager.LoadScene(startScene, Single)` networked-loads the world; late joiners auto-sync. World scene has NO NetworkManager (divergence from `MultiplayerSceneSetup` which puts it in the gameplay scene). Duplicate NM on return-to-menu is destroyed by `GameSession` (PersistentSingleton) sharing the NM GameObject — NGO itself does NOT destroy duplicate NetworkManagers (only sets Singleton if null).
- Host-authoritative saves: only host reads/writes `Application.persistentDataPath/Saves/slot_{0..4}.json` via `JsonUtility`.
- All saveable world content is DYNAMIC (spawned by server), never scene objects — avoids duplicate-on-restore. New world => `WorldLoader` spawns `newWorldSeed`; restored world => spawns from `WorldSaveData.objects`.

**Key files (Assets/Scripts/):**
- `Save/`: `WorldSaveData.cs` (WorldObjectRecord + WorldObjectCategory enum + SlotSummary), `SaveSystem.cs` (static, 5 slots), `ISaveableWorldObject.cs`, `WorldObjectRegistry.cs` (server runtime list on NM GO), `WorldObjectCatalog.cs` (ScriptableObject typeId->prefab).
- `World/`: `GameSession.cs` (PersistentSingleton: ActiveWorld, IsHostingWorld, IsNewWorld, JoinCode), `RelayConnector.cs` (static host/join helpers extracted from NetworkManagerUI), `WorldLoader.cs` (server seeds/restores on OnLoadComplete), `WorldSessionController.cs` (Exit World: host saves+Shutdown; client OnClientStopped->menu; self-wires exitButton), `WorldStartPoint.cs`.
- `World/Content/`: `SaveableWorldEntity.cs` (base NetworkBehaviour+ISaveableWorldObject, registers w/ registry on server spawn), `NpcEntity.cs`/`BuildingEntity.cs`/`ResourceNode.cs` (typeIds npc_basic/building_basic/resource_basic; ResourceNode has server NetworkVariable<int> amount).
- `UI/MainMenuUI.cs` (5 slot rows + join panel; Create/Play/Delete).
- `Editor/WorldSetup.cs` — `Tools/World/Setup Save System` builds MainMenu.unity + World_Main.unity + sample prefabs + WorldObjectCatalog.asset, registers network prefabs (reuses existing Assets/Prefabs/NetworkPrefabs.asset), adds both scenes to Build Settings.
- Modified `Network/PlayerSpawner.cs`: world flow spawns players at `ActiveWorld.startPosition` (+per-index offset) after the world scene loads (NOT OnServerStarted, which fires while still in menu); legacy single-scene flow (no GameSession) unchanged.

**Gotchas fixed (2026-06-29):**
- NGO auto-generates `Assets/DefaultNetworkPrefabs.asset` (IsDefault) containing EVERY NetworkObject prefab, and each NetworkManager references it automatically at runtime (this is why the Test scene worked despite MultiplayerSceneSetup's broken custom-list wiring). So `WorldSetup` must NOT register its own prefab list — doing so double-registers → "NetworkPrefab (X) has a duplicate". Fix: removed custom-list registration entirely; rely on the default list. Tool deletes the stale `Assets/Prefabs/NetworkPrefabs.asset`.
- NGO NetworkManager's config field is `public NetworkConfig NetworkConfig;` (NOT `m_NetworkConfig`) in this version. Serialized-path wiring `m_NetworkConfig.NetworkTransport` / `m_NetworkConfig.Prefabs` silently FAILS (FindProperty returns null) → "NetworkManager must have a UnityTransport component" at host start. `WorldSetup` now wires transport + prefab list via the public API: `nm.NetworkConfig.NetworkTransport = transport`, `nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list)`, then `EditorUtility.SetDirty(nm)`. The existing `MultiplayerSceneSetup` STILL uses the broken `m_NetworkConfig` path (its Test-scene NetworkManager transport was likely wired manually).
- Multiplayer Services SDK: `CreateSessionAsync(WithRelayNetwork())` / `JoinSessionByCodeAsync` START NGO themselves (host/client). Do NOT also call `NetworkManager.StartHost/StartClient` — the double-start makes the next attempt see `IsListening==true` so the SDK's internal StartHost returns false → `NetworkManagerStartFailed` ("session was never started" is just cleanup noise). Tear down with `ISession.LeaveAsync`, NOT `NetworkManager.Shutdown`. `RelayConnector` now keeps the active ISession, leaves stale sessions before hosting, and exposes `LeaveActiveSession()`; `WorldSessionController.ExitWorld` and `MainMenuUI` catch-blocks use it. (NOTE: legacy `NetworkManagerUI` in Test scene still has the old double-StartHost pattern — out of scope.)
- Host scene load: call `SceneManager.LoadScene` AFTER `await CreateSessionAndHost` returns (host is fully started+synchronized by then) — NOT from an OnServerStarted callback, which fires mid-StartHost inside the SDK and reenters badly.
- Host scene load must be DEFERRED to `NetworkManager.OnServerStarted` (subscribe before StartHost to catch a synchronous fire), not called immediately after StartHost — `NetworkSceneManager` isn't ready yet and `LoadScene` errors. `MainMenuUI.HandleServerStartedLoadScene` does this and checks `SceneEventProgressStatus.Started`.
- Pressing Play runs the OPEN scene, not build scene 0. `WorldSetup` sets `EditorSceneManager.playModeStartScene = MainMenu` so Play always boots MainMenu (main editor + MPPM virtual players). Re-run the tool to (re)apply.

- Camera follow: `WorldSetup.BuildWorldScene` adds a `CinemachineBrain` to the Main Camera + a `CinemachineCamera` (FollowCamera) with `CinemachinePositionComposer` (CameraDistance 10, Damping 0.5). `EnsurePlayerCameraTarget()` adds `PlayerCinemachineTarget` to Player.prefab. The tool-generated world scene previously had only a plain camera, so per-player follow was broken until this was added. (CM 3.x: `CinemachineCamera.Lens` is a public FIELD so `vcam.Lens.OrthographicSize = 6f` is legal.)

**Client reconnection (added 2026-06-30):** Dropped clients auto-reconnect per Unity MPS "reconnect to a session" docs. `RelayConnector` now caches `s_LastJoinCode` + exposes `ActiveSessionId` + `ReconnectActiveSession()` (confirm membership via `MultiplayerService.Instance.GetJoinedSessionIdsAsync()`, then `ISession.ReconnectAsync()`, then full rejoin-by-code to rebuild NGO). `WorldSessionController` sets `intentionalExit` in `ExitWorld`; `OnClientStopped` now routes an *unexpected* pure-client drop to `ReconnectOrReturn()` (3 attempts, 2s apart, "Reconnecting…" overlay) and only `ReturnToMenu()` if all fail. `WorldSetup.BuildWorldScene` builds + wires the overlay (`reconnectingOverlay`/`reconnectingLabel`). GOTCHA: do NOT use `MultiplayerService.Instance.ReconnectToSessionAsync(sessionId)` — `SessionManager.ReconnectAsync` throws "Session type is required" whenever Type is empty, and our sessions are created without a Type; use the retained `ISession.ReconnectAsync()` instead. Host-side reconnect/migration is NOT handled (out of scope).

**Pending manual/verify steps:**
1. Run `Tools/Multiplayer/Setup Scene` first if `Assets/Prefabs/Player.prefab` doesn't exist (WorldSetup reuses it for the player; warns if missing).
2. Run `Tools/World/Setup Save System`.
3. World_Main level geometry is an instance of `Assets/Prefabs/Grid.prefab` (instantiated via `PrefabUtility.InstantiatePrefab` at origin), replacing the old procedural Ground. Edit Grid.prefab to change world geometry.
4. Open MainMenu, Play, Create world in a slot; verify host code, world load, player at start, sample content visible; Exit World writes slot_N.json; re-enter restores; 2nd editor Join by code; host exit returns client to menu; Delete removes file.
5. Compile not yet verified live (Unity MCP was offline at build time) — check console on first import.
