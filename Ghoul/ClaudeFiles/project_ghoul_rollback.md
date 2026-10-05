---
name: project-ghoul-rollback
description: Rollback netcode implementation — deterministic input-sync simulation replacing ClientNetworkTransform state sync
metadata:
  node_type: memory
  type: project
---

Rollback netcode implemented 2026-08-18; desync hardening 2026-09-09 and 2026-09-16. Builds on [[project-ghoul-multiplayer]] session infrastructure.

## Architecture
- NGO stays for session management only (Relay, PlayerSpawner, scene loading). All traffic goes through Relay, even two instances on one machine.
- `RollbackSession` runs the deterministic loop from FixedUpdate and simulates **both players on both peers** every tick (player 0 then player 1, fixed order).
- Only inputs go over the wire: 10-byte `RollbackInput` (button bitmask + 2 analog axes as int16) via NGO CustomMessagingManager (Unreliable), last 3 frames bundled per packet for loss recovery.
- 128-frame circular snapshot/input buffer; `maxRollbackFrames = 30` (500ms).
- **Frame-advantage time sync (added 2026-10-05).** Each peer starts its session when *it* sees both players, so the host typically starts first and used to run permanently ahead. The peer ahead receives the other peer's inputs later relative to its own frame, so it predicts and visibly corrects that player all the time. In builds this showed as **the client looking jerky on the host's screen while the host looked smooth on the client's**. Fix: every input packet carries the sender's lead (`CurrentFrame - ConfirmedFrame`, as a short, so the header is now frame(4) + count(1) + lead(2)). If `localLead - remoteLead >= timeSyncThreshold` (3), the peer skips one tick, at most once per `timeSyncInterval` (5) ticks, handled like a stall. A frame skew of S shows up as a lead difference of about 2S. It logs once when it starts slowing down and once when it catches up.
- **`RollbackSetup` restarts sessions (added 2026-10-05).** It lives on the persistent NetworkManager, and `Start()` used to start the "wait for 2 players, then StartSession" coroutine only once per app run. A second world, or a client rejoining, **silently ran without rollback** (ClientNetworkTransform state sync instead), which looked smoother and hid the jerkiness above. `RunSessions()` now loops: wait for 2 spawned players, start, wait for `IsSessionActive == false`, repeat.
- Retired for gameplay while rollback is active: `ClientNetworkTransform` (disabled at session start), `netFacingRight` NetworkVariable (facing is in the snapshot), and damage/knockback RPCs in `CharacterStats` (`InflictDamage` / `KnockbackDirectional` apply locally instead).

## Files
- `Assets/Scripts/Rollback/`: `RollbackSession`, `RollbackSetup` (on the NetworkManager GO in `MainMenu.unity`; finds players by "Player" tag, collects every `ISnapshotable`/`IRollbackSimulated` on them, adds `InputCapture` at runtime if missing), `InputCapture` (accumulates Input System events; `GetAndClearFrame()`), `RollbackInput`, `ISnapshotable` (`SaveState(BinaryWriter)`/`LoadState(BinaryReader)`), `Fix64`/`FixVec2` (Q16.16 groundwork, not yet used).
- `EntityController` — ISnapshotable: position, velocity, state flags, knockback, and the **entire** `Controller2D.CollisionInfo` struct.
- `PlayerController` — `IRollbackSimulated.SimulateFrame` → `RunFixedStep()` (shared with non-rollback FixedUpdate, which is suppressed while a session is active); calls `PlayerAttack.Tick()`.
- `PlayerAttack` — ISnapshotable; swing is part of the tick (see [[project-ghoul-combat]]).
- `CharacterStats` — ISnapshotable (health, speed, damage, knockback power).
- `PlayerInput` — routes callbacks to `InputCapture` when rollback is active, otherwise straight to `PlayerController`.

## Determinism rules (each one was a real desync — don't regress them)
1. **Anything that affects the simulation must run inside the tick and be in a snapshot.** No `IsOwner` gates, coroutines, Physics2D trigger callbacks, or RPCs in gameplay paths — those only happen on one peer or land out-of-band and are lost on resimulation.
2. **Snapshot every field that carries over between `Move()` calls**, including `Controller2D.collisions` (e.g. `fallingThroughPlatform`, `slopeAngleOld`, `moveAmountOld`). A field left out keeps its "future" value after a rewind — `fallingThroughPlatform` once made a player fall through the level on one peer only.
3. **Prediction repeats analog axes only; `Buttons` are zeroed.** Button bits are one-frame press events — repeating them re-fired `Attack()` on every predicted frame.
4. **Misprediction scan range is `[CurrentFrame - maxRollbackFrames + 1, min(ConfirmedFrame, CurrentFrame - 1)]`.** Without the `CurrentFrame - 1` clamp, a peer running ahead made `PerformRollback` load a never-simulated buffer slot (log symptom: negative span, e.g. `toFrame=128 currentFrame=125`).
5. **Only check frames that were actually predicted** (`_wasPredicted[]`); otherwise stale buffer data triggers a rollback nearly every tick.
6. **`ConfirmedFrame` is a high-water mark** updated in `OnRemoteInputReceived`, not a gapless counter — a lost first packet used to leave it stuck at -1 forever. Per-slot `_remoteConfirmedFrame[idx] != f` checks cover gaps.
7. **`maxRollbackFrames` must exceed real confirmation lag** (measured ~18 frames / ~300ms through Relay). If lag exceeds the window, late mispredictions are never checked and wrong guesses stay permanent.
8. **`Physics2D.SyncTransforms()` after every `transform.Translate()` in `Controller2D.Move()`** — `autoSyncTransforms` is off project-wide, so resimulation raycasts would otherwise see the other player's stale position.
9. **`RaycastAll` results are sorted (`SortByDistance`)** with a tie-break on hit `point` coordinates (`Array.Sort` is unstable). Never tie-break on `GetInstanceID` — it differs between processes.
10. **Max-prediction stall (added 2026-09-29).** `TickFrame` doesn't advance while `CurrentFrame - (ConfirmedFrame + 1) >= maxPredictionFrames` (default 28, clamped to `maxRollbackFrames - 2` in `Awake`). Without it, any peer whose `FixedUpdate` stopped for more than ~0.5s (window drag, hitch, breakpoint, `timeScale = 0`) fell permanently behind, and all its later inputs landed outside the other peer's rollback window, so they were never corrected. While stalled, it still runs rollback corrections and re-sends its newest input bundle every tick (so lost packets can't deadlock two stalled peers), and doesn't drain `InputCapture`. It also absorbs start-up skew between the two `StartSession` calls. `[Rollback] Stalling…` / `Resumed … after N stalled ticks` logs mark each stall.

11. **`Physics2D.SyncTransforms()` at the end of `LoadSnapshot` (added 2026-09-30).** `LoadState` teleports transforms, but with auto-sync off, collider bounds kept the pre-rollback (future) positions. The first resimulated step reads bounds before any `Move()` syncs: `PlayerAttack.Tick` (hitbox origin + `Bounds.Intersects` hit check) and `Controller2D.Move` → `UpdateRaycastOrigins`. So the peer doing the rollback ran hit detection and collisions against stale geometry. It was latent since rollback shipped (barely visible on flat ground), and surfaced as a desync once knockback started ending on an exact landing frame. **General rule: anything that moves a transform outside `Controller2D.Move` must sync before the next bounds read.**

## Known gaps
- `Controller2D` still uses floats. Fine for Windows↔Windows (IEEE 754 is identical there); cross-platform needs a Fix64 migration.
- The `fallingThroughPlatform` reset is a real-time `Invoke()`, so its firing frame can differ by ~1 frame between peers. It's snapshotted, so a wrong value no longer survives a rewind, but the timer should eventually become tick-based.
- `previousXVelocity`/`previousYVelocity` in `EntityController` are written in render `Update()` and never read back — harmless, but they make full-snapshot hashes differ between peers.
- Raycasts hit the shared live physics world, but only the players are snapshotted. That's safe today because `NpcEntity`/`ResourceNode`/`BuildingEntity` don't move at runtime and `ItemDrop`/`ItemGravity` aren't used by any live spawn path (`Item.DropIntoWorld()` just teleports). Anything that starts moving during play must join the snapshot system.
- **Holes from lost packets are never re-sent.** Each packet carries only the last `inputRedundancy` (3) frames. If more than 3 consecutive packets are lost, those frames stay unconfirmed for good. `FindEarliestMisprediction` skips them, so a wrong prediction there is never corrected. It's rare on Relay, but a fix would be to ack the highest gapless frame and resend from there.
- `PauseMenu` (2026-09-28) must never set `Time.timeScale` while a session is active: that stops this peer's tick, which just forces the other peer to stall. It's an overlay only in online play: `InputCapture.SetInputBlocked(true)` sends neutral frames, drops presses made in the menu and inventory holds in progress, and keeps the real stick value so a direction still held on close takes effect. Offline, it still freezes time.

## Testing gotchas
- **Rebuild/relaunch the Multiplayer Play Mode virtual player after every script change.** When it runs as a built Player (`[Player N]` / `Windows_Build.exe` in the console) it does not auto-rebuild, so tests silently run stale code.
- `PlayerSettings.runInBackground` must be `true` (set 2026-09-16). With it off, an unfocused standalone build pauses completely and then catches up in one burst, which looks like a rollback snap.
- Short transient divergence (e.g. mid-jump Y) that goes away within ~10 frames is normal prediction lag. **A divergence that stays after the player stops moving is a real bug.**

## Debugging a desync
Temporarily add: (a) a log every N frames in `RollbackSession.TickFrame()` with `_localPlayerIndex`, `CurrentFrame`, `ConfirmedFrame`, both players' positions, and a hash of every `ISnapshotable.SaveState()`'s bytes (N=10 for fast-appearing bugs, N=60 for slow ones); (b) a log on every `PerformRollback()` with the frame span (a negative span means rule 4 has regressed); (c) targeted logs at the suspected mutation point (e.g. `PlayerAttack.CheckHit`, `EntityController.ApplyDirectionalKnockback`). Diff the two peers' logs frame by frame — the first frame where the hash/position splits, or where only one peer logs an event, points to the bug.
