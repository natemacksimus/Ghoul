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

## Known gaps
- `Controller2D` still uses floats. Fine for Windows↔Windows (IEEE 754 is identical there); cross-platform needs a Fix64 migration.
- The `fallingThroughPlatform` reset is a real-time `Invoke()`, so its firing frame can differ by ~1 frame between peers. It's snapshotted, so a wrong value no longer survives a rewind, but the timer should eventually become tick-based.
- `previousXVelocity`/`previousYVelocity` in `EntityController` are written in render `Update()` and never read back — harmless, but they make full-snapshot hashes differ between peers.
- Raycasts hit the shared live physics world, but only the players are snapshotted. That's safe today because `NpcEntity`/`ResourceNode`/`BuildingEntity` don't move at runtime and `ItemDrop`/`ItemGravity` aren't used by any live spawn path (`Item.DropIntoWorld()` just teleports). Anything that starts moving during play must join the snapshot system.
- `PauseMenu` sets `Time.timeScale`, which stops `FixedUpdate` on one peer only — not yet reviewed against rollback.

## Testing gotchas
- **Rebuild/relaunch the Multiplayer Play Mode virtual player after every script change.** When it runs as a built Player (`[Player N]` / `Windows_Build.exe` in the console) it does not auto-rebuild, so tests silently run stale code.
- `PlayerSettings.runInBackground` must be `true` (set 2026-09-16). With it off, an unfocused standalone build pauses completely and then catches up in one burst, which looks like a rollback snap.
- Short transient divergence (e.g. mid-jump Y) that goes away within ~10 frames is normal prediction lag. **A divergence that stays after the player stops moving is a real bug.**

## Debugging a desync
Temporarily add: (a) a log every N frames in `RollbackSession.TickFrame()` with `_localPlayerIndex`, `CurrentFrame`, `ConfirmedFrame`, both players' positions, and a hash of every `ISnapshotable.SaveState()`'s bytes (N=10 for fast-appearing bugs, N=60 for slow ones); (b) a log on every `PerformRollback()` with the frame span (a negative span means rule 4 has regressed); (c) targeted logs at the suspected mutation point (e.g. `PlayerAttack.CheckHit`, `EntityController.ApplyDirectionalKnockback`). Diff the two peers' logs frame by frame — the first frame where the hash/position splits, or where only one peer logs an event, points to the bug.
