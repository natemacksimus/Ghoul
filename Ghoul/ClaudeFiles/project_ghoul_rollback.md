---
name: project-ghoul-rollback
description: Rollback netcode implementation — deterministic input-sync simulation replacing ClientNetworkTransform state sync
metadata:
  node_type: memory
  type: project
---

Rollback netcode implemented (2026-08-18). Builds on [[project-ghoul-multiplayer]] session infrastructure.

**Architecture:**
- NGO stays for session management (Relay, PlayerSpawner, scene loading). Relay is still used for NAT traversal.
- `ClientNetworkTransform` is DISABLED at session start. Rollback drives all positions.
- `RollbackSession` (new) drives the deterministic simulation loop via FixedUpdate.
- Inputs only (10 bytes/frame) are sent over the wire via NGO CustomMessagingManager (Unreliable).
- Last 3 frames bundled per packet for loss recovery (standard fighting-game practice).

**New files — Assets/Scripts/Rollback/:**
- `Fix64.cs` — Q16.16 fixed-point math (groundwork; Controller2D still uses floats for now)
- `FixVec2.cs` — 2D fixed-point vector
- `RollbackInput.cs` — 10-byte input packet (bitmask + 2 analog axes as int16)
- `ISnapshotable.cs` — interface: SaveState(BinaryWriter) / LoadState(BinaryReader)
- `InputCapture.cs` — accumulates Unity Input System events between frames; GetAndClearFrame() called by RollbackSession
- `RollbackSession.cs` — circular buffer (128 frames), prediction (repeat last confirmed), misprediction detection, rollback + re-simulation
- `RollbackSetup.cs` — add to NetworkManager GO; auto-detects spawned players by tag, wires session

**Modified files:**
- `EntityController` — implements ISnapshotable (position, velocity, all state flags, knockback); HandleKnockback/HandleDirectionalKnockback made protected
- `PlayerController` — implements IRollbackSimulated (SimulateFrame); FixedUpdate suppressed when session active; RunFixedStep() extracted as shared body; SaveState/LoadState override
- `PlayerInput` — callbacks route to InputCapture when rollback active, fall through to PlayerController otherwise
- `CharacterStats` — implements ISnapshotable (health, speed, damage, knockback power); InflictDamage applies locally (no RPC) when rollback active

**Determinism note:**
Controller2D still uses floats. On same-platform (Windows→Windows) IEEE 754 is bit-identical so this is acceptable for co-op. Full cross-platform determinism requires migrating Controller2D to Fix64 (follow-up task).

**Unity Editor wiring required:**
1. Add `RollbackSetup` component to the NetworkManager GameObject (or any DontDestroyOnLoad root).
2. Either assign player NetworkObjects in RollbackSetup inspector, OR ensure player prefab is tagged "Player".
3. Ensure `InputCapture` component exists on the player prefab (RollbackSetup adds it automatically if missing).
4. Set NetworkManager Tick Rate to 60 and Unity FixedUpdate to 0.01667 (1/60s).
5. Verify Console is clean on first import — CustomMessagingManager API, FastBufferWriter usage may need iteration.

**NGO systems retired for gameplay (kept for session only):**
- `ClientNetworkTransform` (disabled at session start)
- `netFacingRight` NetworkVariable in EntityController (still present but irrelevant; facing is in snapshot)
- Damage/knockback RPCs in CharacterStats (bypassed when rollback active)
