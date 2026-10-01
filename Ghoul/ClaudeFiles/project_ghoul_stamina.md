---
name: project-ghoul-stamina
description: PlayerStamina — drain on hits/attacks, regen delay, stamina-gated knockback/stun, weapon knockback multipliers, stacked world-space bars
metadata:
  node_type: memory
  type: project
---

Stamina system added 2026-09-30. Builds on [[project-ghoul-combat]] (directional knockback) and must follow [[project-ghoul-rollback]] determinism rules.

**Design decisions from the user:**
- Stamina **gates** knockback. At ≥ `knockbackThreshold` (40%) a hit is damage only (no knockback, unlike before, when every hit knocked back). Below 40% it's knockback; below `stunThreshold` (10%) it's knockback plus a stun. Stamina is read **before** the hit's own cost is applied.
- The knockback force lives on the **target's** `PlayerStamina.knockbackForce` (default 20). `PlayerAttack.knockbackPower` was removed, so there's one force setting. The **attacking weapon** scales it: `Item.knockbackPowerMultiplier` scales force, and `Item.knockbackDurationMultiplier` stretches the push (decay rate ÷ multiplier, time cap × multiplier). An empty hand counts as 1/1.
- Stamina bars are visible to everyone, like health.

**Components (on Player.prefab):**
- `PlayerStamina` (MonoBehaviour, ISnapshotable; auto-registered by `RollbackSetup`'s `GetComponents<ISnapshotable>()` scan). Snapshot: currentStamina, regenDelayTimer, stunTimer, stunRolls. `Tick(fixedDeltaTime)` is called from `PlayerController.RunFixedStep`. `OnAttack()` is called from `PlayerAttack.Attack` when a swing actually starts. `OnHitTaken()` is called from `PlayerAttack.CheckHit` on the target and returns `HitReaction`. Costs are 25 per hit and 5 per attack (max 100), with a regen delay of 2 s that any hit or attack resets, and regen of 15/s. Stamina is clamped to 0..max. `ResetToFull()` exists for respawn and is called from Awake.
- **Stun duration is deterministic.** A hash of (OwnerClientId seed, snapshotted `stunRolls` counter) maps into [stunDurationMin, stunDurationMax] (1–2.5 s). Never use `UnityEngine.Random` in the simulation.
- **Stunned:** `PlayerController.ApplyRollbackInput` zeroes `directionalInput` and skips all button actions, the buffered jump is blocked, and `PlayerAttack.Attack` refuses to swing. Gravity and knockback still apply.
- **Knockback chain:** `CharacterStats.KnockbackDirectional(..., durationScale)` → RPCs → `EntityController.ApplyDirectionalKnockback(..., durationScale)`. It stores `knockbackDurationScale` (snapshotted), and the swing's weapon multipliers are snapshotted in `PlayerAttack`.
- **UI:** `WorldSpaceBar` (Scripts/UI) is a shared builder: a world-space canvas, 200×20 px at scale 0.005 (1×0.1 units), dark background, left-anchored fill. `PlayerStaminaBar` (green, offset y 0.75, the health bar's old spot) and `PlayerHealthBar` (refactored onto WorldSpaceBar, offset y 0.87 = 0.75 + 0.1 + 0.02 spacing; solid red fill via a serialized `fillColor`, replacing the old red-yellow-green gradient). Both update from events (`StaminaChanged`/`HealthChanged`), including on rollback LoadState.

**Gameplay note:** with default costs, a full player takes damage-only hits at 100/75/50, gets knocked back by the hit landing at 25, and gets stunned by hits landing at 0 (the <10% band is only reachable at 0 with 25-point steps).

**Known risk:** the weapon multipliers read `PlayerInventory`, which isn't in the rollback snapshot (inventory is owner-local and not networked yet; see [[project-ghoul-inventory-controls]]). If the two peers' inventories ever differ, the same swing would apply different knockback.

**Stun recovery (added 2026-09-30, at the user's request).** While a player is stunned, *that player's* stamina regenerates at `stunRegenRate` (default 60/s, 4× normal) with no regen delay, and it **can't decrease** (a guard in `SetStamina`, plus early returns in `OnAttack`/`OnHitTaken`). Hits during a stun still do damage, but they cost no stamina, don't reset the regen delay, and **can't re-stun** (a `KnockbackAndStun` result is downgraded to `Knockback`, so there's no stun-lock). They can still knock back, based on current stamina. The regen delay is cleared during the stun, so normal 15/s regen continues straight after. Verified in-editor: stunned at 0, after 0.25 s stamina is 15 and a hit leaves it at 15; a ~1 s stun ends at ~61; 1 s later it's 76; the next hit after the stun reduces it normally (76 → 51).
