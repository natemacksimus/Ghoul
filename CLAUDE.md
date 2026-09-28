# Ghoul — Claude Code Context

## Project memory
Detailed context files live in `Ghoul/ClaudeFiles/` and are committed to the repo.
Read these at the start of every session:

- `Ghoul/ClaudeFiles/MEMORY.md` — index
- `Ghoul/ClaudeFiles/project_ghoul_multiplayer.md` — NGO 2.1.1 session layer, client-authority fallback, Cinemachine 3.x
- `Ghoul/ClaudeFiles/project_ghoul_save_system.md` — 5-slot world save, host-authoritative, editor setup tool
- `Ghoul/ClaudeFiles/project_ghoul_combat.md` — directional traveling hitbox + reflecting knockback
- `Ghoul/ClaudeFiles/project_ghoul_inventory_controls.md` — two-hand inventory, stick-aimed use, tap/hold controls
- `Ghoul/ClaudeFiles/project_ghoul_rollback.md` — deterministic input-sync rollback netcode replacing CNT state sync

**Do NOT write memory to `~/.claude/projects/…`** — this project is worked on across multiple machines; all persistent context must be committed here in `Ghoul/ClaudeFiles/`.

## Project layout
```
Ghoul/                  ← Unity project root
  Assets/
    Scripts/
      Characters/       ← EntityController, Controller2D, RaycastController, CharacterStats, ItemGravity
        Player/         ← PlayerController, PlayerInput, PlayerAttack, PlayerStats, PlayerCinemachineTarget, PlayerHealthBar
      Inventory/        ← PlayerInventory
        Items/          ← Item, ItemDefinition, TorchItem, TorchFlare
      Item Pickups/     ← ItemDrop
      Level/            ← IInteractable, InteractableObjects
      Save/             ← SaveSystem, WorldSaveData, WorldObjectRegistry, WorldObjectCatalog, ISaveableWorldObject
      World/            ← GameSession, RelayConnector, WorldLoader, WorldSessionController, WorldStartPoint
        Content/        ← BuildingEntity, NpcEntity, ResourceNode, SaveableWorldEntity
      Network/          ← PlayerSpawner, ClientNetworkTransform, NetworkManagerUI, PlayerColorSync
        Editor/         ← MultiplayerSceneSetup
      Rollback/         ← RollbackSession, RollbackSetup, InputCapture, RollbackInput, ISnapshotable, Fix64, FixVec2
      UI/               ← MainMenuUI, PauseMenu
      Editor/           ← WorldSetup, PauseMenuSetup, TestPickupSetup
      Utilities/        ← PersistentSingleton, ScreenFader, EventSys, ShowOnlyAttribute
  ClaudeFiles/          ← committed project memory (read on session start)
```

## Key conventions
- Unity 2D, Universal Render Pipeline, Unity 6000+
- Multiplayer: Netcode for GameObjects 2.1.1 + Unity Relay (join-code co-op)
- Custom raycast physics via `Controller2D` — no Rigidbody2D on characters
- Rollback netcode drives gameplay: deterministic input-sync simulation (`RollbackSession`) owns position and health; `ClientNetworkTransform` and health/damage RPCs are disabled at session start and only apply when rollback is inactive — see `project_ghoul_rollback.md`
- Host-only saves; clients never read/write save files
- Cinemachine 3.x (`Unity.Cinemachine` namespace, `CinemachineCamera` — NOT 2.x `CinemachineVirtualCamera`)
- Editor setup tools live under **Tools/World/** and **Tools/Multiplayer/** menus
