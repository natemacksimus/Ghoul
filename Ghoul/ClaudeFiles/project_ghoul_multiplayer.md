---
name: project-ghoul-multiplayer
description: Multiplayer integration for Ghoul — a 2D co-op platformer using Netcode for GameObjects 2.1.1; client-authority fallback beneath rollback
metadata: 
  node_type: memory
  type: project
  originSessionId: 9065dd02-8200-4b2e-9cb6-415d179dc7c7
---

Multiplayer (NGO 2.1.1) added to the Ghoul project, originally with a client-authority co-op model.

**Why:** User wants co-op multiplayer; client authority was chosen for compatibility with custom Controller2D raycast physics.

**How to apply:** Gameplay networking is now driven by [[project-ghoul-rollback]] — while a rollback session is active, both peers simulate both players from exchanged inputs, and the client-authority paths below (ClientNetworkTransform, health/damage/knockback RPCs) are bypassed. Treat them as the fallback used only when rollback is inactive. New gameplay features should be deterministic and part of the rollback tick, not new RPCs/NetworkVariables.

**Client-authority architecture (active only without rollback):**
- `EntityController` → `NetworkBehaviour`; `netFacingRight` NetworkVariable (Owner write) syncs flip
- `CharacterStats` → `NetworkBehaviour`; `netHealth` NetworkVariable (Server write); damage/knockback via ServerRpc + targeted ClientRpc to the owning client
- `ClientNetworkTransform` (owner-auth) on the player prefab drives remote positions
- `PlayerController.OnNetworkSpawn` disables only `PlayerInput` on non-owners — `PlayerController` itself stays enabled so rollback can simulate the remote player
- `PlayerSpawner` spawns the player prefab per client connection on the server
- `NetworkManagerUI` provides Host/Client/Server buttons (legacy Test-scene flow; the real flow is MainMenu, see [[project-ghoul-save-system]])

Player.prefab already has NetworkObject, ClientNetworkTransform, and PlayerCinemachineTarget; the NetworkManager lives in `MainMenu.unity` (built by `Tools/World/Setup Save System`).

**Follow camera (Cinemachine 3.x):**
- Namespace `Unity.Cinemachine`, `CinemachineCamera`, `CinemachinePositionComposer`. The commented-out camera code in PlayerController is 2.x API (`CinemachineVirtualCamera`, `CinemachineFramingTransposer`) and won't compile.
- One camera per machine following the local owner (NOT split-screen). `PlayerCinemachineTarget` sets the scene vcam's `Follow = transform` only when `IsOwner`.
- `WorldSetup.BuildWorldScene` creates the CinemachineBrain + FollowCamera (Position Composer, distance 10, damping 0.5). Optional: Confiner2D for level bounds.
- An earlier hand-rolled `PlayerCameraFollow.cs` was deleted in favor of Cinemachine.
