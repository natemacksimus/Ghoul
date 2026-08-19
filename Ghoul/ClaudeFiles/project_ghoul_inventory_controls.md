---
name: project_ghoul_inventory_controls
description: Two-hand inventory + revised player controls added; pending Unity Editor wiring
metadata: 
  node_type: memory
  type: project
  originSessionId: 2e8fd9f6-a2c3-4b3e-99a4-f37c65be82d0
---

Revised player controls and added a two-hand inventory system (2026-07-28).

**Code (done):**
- `Assets/Scripts/Inventory/PlayerInventory.cs` — new. `Hand` enum, serializable `HandInventory` (capacity 3, active slot, Add/ReplaceActive/CycleNext/RemoveActive), and `PlayerInventory` MonoBehaviour holding rightHand + leftHand. Owner-local, not networked yet.
- `Item.cs` — added `HandSlot { Right, Left, Either }` enum + `handSlot` field (Either → Right), plus `StoreInInventory()` / `DropIntoWorld(pos)` (deactivate/reactivate instead of destroy). All `animator.SetBool/SetTrigger` calls null-guarded so bare pickups (no Animator) don't throw.
- `Assets/Scripts/Editor/TestPickupSetup.cs` — **Tools/World/Create Test Pickups**: makes 3 pickup prefabs in Assets/Prefabs/TestPickups (Test Sword WEAPON/Right, Test Axe WEAPON/Right, Test Torch ITEM/Left). Root = Item + trigger BoxCollider2D(~1u); child = SpriteRenderer scaled 0.8 using a generated 1-world-unit square sprite asset (_TestSquare.png, 64px@64ppu). Sword/Axe use `Item`, Torch uses `TorchItem`. Idempotent (clears old instances first). Drops connected instances in the scene. Local test props, not networked.
  - NOTE: first version used built-in UISprite (32px@200ppu = 0.16u ×0.6 = 0.10u) which rendered as an invisible speck at camera ortho size 6 — looked like items "disappeared on Play." Fixed by the 1-unit generated sprite (now 0.80u).
- `TorchItem.cs` (: Item) + `TorchFlare.cs` — Torch's `UseItemAbility` override spawns a procedural warm radial-glow burst at the player that expands+fades then self-destroys (no lighting package / sprite asset needed). Demonstrates a real non-weapon item ability via left-hand use.
- `PlayerController.cs` — Move captures raw left-stick vector (`moveVectorRaw`) → aims right hand; `AimInput` captures raw right-stick (`aimVectorRaw`) → aims left hand. `UseRightHand`/`UseLeftHand` use that hand's active item (weapon or empty → `PlayerAttack.Attack(dir)`; non-weapon → `Item.UseItemAbility`). Inventory buttons: tap = cycle, hold (`inventoryDropHoldTime` 0.4s) = drop active. `Interact` (hold) picks up nearby item into its `handSlot` hand, else calls `InteractableObjects.InteractWithObject()`. `OpenMenu` toggles a `PauseMenu`. Has `[RequireComponent(typeof(PlayerInventory))]`.
- `PlayerInput.cs` — Aim/UseLeft/UseRight rewired; InventoryLeft/Right now subscribe started+canceled (for tap-vs-hold).
- `Assets/Scripts/UI/PauseMenu.cs` — new. Toggles a panel GameObject + Time.timeScale.

**Editor wiring status:**
1. DONE — `PlayerInventory` component added to `Assets/Prefabs/Player.prefab` by hand-editing the prefab YAML (fileID 4998877665544332211, script guid f45478ccd28d7ab4d9d83f76ed080a98). Unity had already imported the new scripts so the GUID existed.
2. PENDING — set `handSlot` on weapon/item pickup prefabs so pickups route to the intended hand. (No item-pickup prefabs exist yet; nothing to set until they're created.)
3. PauseMenu — built an Editor tool instead of blind YAML: `Assets/Scripts/Editor/PauseMenuSetup.cs` adds menu items **Tools/World/Setup Pause Menu** (creates PauseUI canvas + full-screen dim PausePanel + PAUSED/hint text, wires PauseMenu.pausePanel, ensures EventSystem) and **Tools/World/Ensure Player Inventory** (idempotent add of PlayerInventory to Player.prefab via EditPrefabContentsScope). USER MUST RUN "Tools/World/Setup Pause Menu" once in World_Main and Ctrl+S. OpenMenu FindObjectOfType-fallbacks to the PauseMenu, so no per-player wiring.

Unity MCP unusable this session: server allows only ONE connection and `unity-relay-client` held the slot, so `claude-code` was denied ("revoked") even after Editor restart + relay disable + approving the client. Never bound. So compile-verification wasn't done via MCP — user should confirm the Console is clean.

Related: [[project_ghoul_combat]] (PlayerAttack directional hitbox), [[project_ghoul_multiplayer]] (inventory not yet synced).
