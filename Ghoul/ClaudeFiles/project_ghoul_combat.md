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

**2026-09-09 rewrite — folded into rollback determinism.** Playtesting [[project-ghoul-rollback]] surfaced a hard desync: `PlayerAttack.Attack()` was gated `if (!IsOwner) return;`, but `RollbackSession` simulates BOTH players locally on BOTH peers every tick. So only the attacking player's own machine ever spawned the hitbox, ran its `OnTriggerEnter2D`, and called `InflictDamage`/`KnockbackDirectional` — the defending peer's local copy of the target never took the hit at all. Health and (via directional knockback) position permanently split the instant anyone landed an attack. The hitbox was also driven by `StartCoroutine`+`yield return null` (real render-frame cadence, not the fixed tick) and wasn't `ISnapshotable`, so even the attacker's own rollback resimulation could silently drop it.

Fixed by making the swing part of the deterministic simulation instead of a side effect of ownership:
- `AttackHitboxLogic.cs` **deleted**. `PlayerAttack` now implements `ISnapshotable` directly and owns all swing state (`cooldownTimer`, `attackActive`, `attackDirection`, `attackTraveled`, `hasHitTarget`) — auto-registered by `RollbackSetup.CollectFromPlayer`'s `GetComponents<ISnapshotable>()` scan, no wiring needed.
- No more `IsOwner` gate, no more coroutine. `Attack()` just arms the swing; `PlayerAttack.Tick()` advances it by `hitboxSpeed * Time.fixedDeltaTime` and is called every tick from `PlayerController.RunFixedStep()` — which already runs identically on both peers via `SimulateFrame`, live or during rollback resimulation.
- Hit detection is a manual `Bounds.Intersects` check against the other player's `BoxCollider2D.bounds` (resolved once via `FindObjectsByType<CharacterStats>`, cached — exactly one other player exists in this 2-player co-op model) instead of a Physics2D trigger callback. Avoids depending on Unity's async trigger-dispatch timing relative to the manual resimulation loop, and sidesteps the live-Physics2D-world determinism risk entirely for this hit check.
- `CharacterStats.KnockbackDirectional` gained the same rollback-active local-apply branch `InflictDamage` already had — it no longer routes through the ServerRpc→ClientRpc chain while rollback owns the session, since an RPC lands out-of-band from the fixed tick and would be silently discarded on the next rollback resimulation.
- The red square hitbox visual is now a single persistent `GameObject` (position/size/active mirrored from the simulated state each `Tick()`), so it renders identically on both peers instead of only the attacker's screen.

Verified: clean compile via Unity MCP, no console errors/warnings.
