---
name: project-ghoul-multiplayer
description: Multiplayer integration status for Ghoul — a 2D co-op platformer using Netcode for GameObjects 2.1.1
metadata: 
  node_type: memory
  type: project
  originSessionId: 9065dd02-8200-4b2e-9cb6-415d179dc7c7
---

Multiplayer (NGO 2.1.1) added to the Ghoul project using client-authority co-op model.

**Why:** User wants co-op multiplayer; client authority chosen for compatibility with custom Controller2D raycast physics.

**How to apply:** When suggesting further networking features, remember the client-authority model. Server owns health (NetworkVariable), owner client owns position (ClientNetworkTransform). Knockback is delivered via targeted ClientRpc to the owning client only.

**Architecture:**
- `EntityController` → `NetworkBehaviour`; `netFacingRight` NetworkVariable (Owner write) syncs flip
- `CharacterStats` → `NetworkBehaviour`; `netHealth` NetworkVariable (Server write); damage/knockback via ServerRpc+ClientRpc
- `PlayerController.OnNetworkSpawn` disables self + PlayerInput for non-owners; NetworkTransform drives their position
- `ClientNetworkTransform` (owner-auth) added to player prefab instead of NetworkTransform
- `PlayerSpawner` spawns player prefab per client connection on server
- `NetworkManagerUI` provides Host/Client/Server buttons

**Pending Unity Editor steps (user must do manually):**
1. Open Package Manager → confirm NGO 2.1.1 installed
2. Create Player prefab from scene object
3. Add NetworkObject + ClientNetworkTransform to player prefab
4. Add NetworkManager + PlayerSpawner + NetworkManagerUI to scene
5. Assign playerPrefab in PlayerSpawner, set Unity Transport in NetworkManager
6. Register player prefab in NetworkManager's "Network Prefabs"

**Follow camera (Cinemachine):**
- Cinemachine 3.1.6 already in manifest. NOTE: the commented-out camera code in PlayerController is Cinemachine 2.x API (`CinemachineVirtualCamera`, `CinemachineFramingTransposer`) and won't compile against 3.x. In 3.x: namespace `Unity.Cinemachine`, `CinemachineCamera`, `CinemachinePositionComposer`.
- Online co-op model: one camera per machine following the local owner (NOT split-screen). `PlayerCinemachineTarget` (on player prefab) sets the scene vcam's `Follow = transform` only when `IsOwner`.
- An earlier hand-rolled `PlayerCameraFollow.cs` was created then deleted in favor of Cinemachine.
- Pending Editor steps: add `PlayerCinemachineTarget` to player prefab; add CinemachineBrain to Main Camera (tagged MainCamera); create a CinemachineCamera (Follow Camera) with Follow left EMPTY; Position Composer with Camera Distance 10, dead zone + damping; optional Confiner2D for level bounds.
