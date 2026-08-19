# Ghoul — Claude Code Context

## Project memory
Detailed context files live in `Ghoul/ClaudeFiles/` and are committed to the repo.
Read these at the start of every session:

- `Ghoul/ClaudeFiles/MEMORY.md` — index
- `Ghoul/ClaudeFiles/project_ghoul_multiplayer.md` — NGO 2.1.1 co-op, client-authority model, Cinemachine 3.x
- `Ghoul/ClaudeFiles/project_ghoul_save_system.md` — 5-slot world save, host-authoritative, editor setup tool
- `Ghoul/ClaudeFiles/project_ghoul_combat.md` — directional traveling hitbox + reflecting knockback
- `Ghoul/ClaudeFiles/project_ghoul_inventory_controls.md` — two-hand inventory, stick-aimed use, tap/hold controls

**Do NOT write memory to `~/.claude/projects/…`** — this project is worked on across multiple machines; all persistent context must be committed here in `Ghoul/ClaudeFiles/`.

## Project layout
```
Ghoul/                  ← Unity project root
  Assets/
    Scripts/
      Characters/       ← EntityController, Controller2D, PlayerController, PlayerAttack, CharacterStats
      Inventory/        ← PlayerInventory, HandInventory, Item, TorchItem
      Save/             ← SaveSystem, WorldSaveData, WorldObjectRegistry, ISaveableWorldObject
      World/            ← GameSession, RelayConnector, WorldLoader, WorldSessionController
      Network/          ← PlayerSpawner, ClientNetworkTransform, NetworkManagerUI
      UI/               ← MainMenuUI, PauseMenu
      Editor/           ← WorldSetup, PauseMenuSetup, TestPickupSetup, MultiplayerSceneSetup
      Utilities/        ← PersistentSingleton, ScreenFader, EventSys, ShowOnlyAttribute
  ClaudeFiles/          ← committed project memory (read on session start)
```

## Key conventions
- Unity 2D, Universal Render Pipeline, Unity 6000+
- Multiplayer: Netcode for GameObjects 2.1.1 + Unity Relay (join-code co-op)
- Custom raycast physics via `Controller2D` — no Rigidbody2D on characters
- Client-authority position (`ClientNetworkTransform`); server-authority health (`NetworkVariable`, Server write)
- Host-only saves; clients never read/write save files
- Cinemachine 3.x (`Unity.Cinemachine` namespace, `CinemachineCamera` — NOT 2.x `CinemachineVirtualCamera`)
- Editor setup tools live under **Tools/World/** and **Tools/Multiplayer/** menus
