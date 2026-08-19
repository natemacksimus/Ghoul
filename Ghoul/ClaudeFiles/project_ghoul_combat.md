---
name: project-ghoul-combat
description: PlayerAttack combat redesign for Ghoul — directional traveling hitbox + reflecting knockback
metadata: 
  node_type: memory
  type: project
  originSessionId: 44a7af0a-eb48-4f2f-bd53-d4047564afcf
---

PlayerAttack was redesigned (2026-06-30) into a traveling directional hitbox with reflecting knockback. Builds on [[project-ghoul-multiplayer]] client-authority model.

**Behaviour:** On attack press, a square hitbox spawns at the player's collider center and slides in the held direction (or `lastDirInput` if none held) for `hitboxDistance` world units at `hitboxSpeed`. On contact it damages + knocks the target back **along the hitbox travel direction**. The knocked-back target reflects off surfaces (`Vector2.Reflect`, angle out = angle in) up to `knockbackBounces` times, then recovers. Each bounce multiplies speed by `knockbackBounciness` (0..1, `[Range]` on PlayerAttack; 1 = no energy loss, threaded through the RPC chain into `ApplyDirectionalKnockback`).

**Key design decisions (the non-obvious "why"):**
- Did NOT change the shared `IDamageable.Knockback(Vector2,float,int)` interface — it's still used by the legacy animation-driven `Damage.cs` / `Item.cs` / `InteractableObjects.cs`. Instead added a PARALLEL directional path so those aren't disturbed. `AttackHitboxLogic` calls the concrete `CharacterStats.KnockbackDirectional(dir, power, time, bounces)` directly (it already resolves `CharacterStats` via GetComponentInParent).
- Networking mirrors the existing knockback exactly: `CharacterStats.KnockbackDirectional` → ServerRpc(RequireOwnership=false) → targeted ClientRpc to `OwnerClientId` → `EntityController.ApplyDirectionalKnockback` runs on the victim's OWNING client (client-authority), so bounce physics simulate on the owner and sync to others via ClientNetworkTransform.
- Reflection normal comes from `Controller2D.collisions`: `slopeNormal` for slopes, else derived from the `below/above/left/right` flags (up/down/right/left). Read at the START of the next FixedUpdate (collisions reflect the previous frame's `Move`).
- `EntityController` gained a directional-knockback state (`directionalKnockback`, `knockbackVelocity`, `knockbackBouncesRemaining`) running IN PARALLEL to legacy `HandleKnockback` — `FixedUpdate` branches on `directionalKnockback`. Velocity is units/sec (Move multiplies by dt), same convention as jump/speed.
- `PlayerController` suppresses gravity + terminal-velocity clamp (in `CalculateMoveAmount`) and the floor/ceiling y-zeroing (in `Move`) while `directionalKnockback` is active, so the flight is a straight line between bounces and reflection stays clean. `disableInput` (set by ApplyDirectionalKnockback) already skips x-smoothing.
- Attack now fires IMMEDIATELY on press (`PlayerController.UseItem` → `playerAttack.Attack(dir)`); removed the old "wait for a direction" `pendingAttack`/`FirePendingAttack`/`IsAttackPending` flow.

**Files:** `PlayerAttack.cs` (rewrite), `AttackHitboxLogic.cs` (rewrite), `EntityController.cs` (+directional knockback), `CharacterStats.cs` (+KnockbackDirectional RPC chain), `PlayerController.cs` (fire-on-press + knockback guards). Player.prefab PlayerAttack fields updated (hitboxDistance/Speed/SizeScale, knockbackPower/Time/Bounces); old attackDuration/attackKnockback(Vector2)/attackKnockbackTime removed.

**Not yet verified live** — Unity MCP was offline (Connection revoked) at implementation time; needs a compile + playtest. The hitbox is a local (non-networked) visual on the attacker only, as before.
