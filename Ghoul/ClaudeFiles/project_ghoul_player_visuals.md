---
name: project-ghoul-player-visuals
description: Player art/animation setup — gameplay root vs Visual child, Animator on root driven by PlayerAnimator from simulated state, rules that keep animation from desyncing rollback
metadata:
  node_type: memory
  type: project
---

Set up 2026-10-06 to prepare for new player art and animation. Must follow [[project-ghoul-rollback]]: animation is cosmetic and never writes simulation state.

**Prefab structure (`Assets/Prefabs/Player.prefab`):**
- **Root** = gameplay: `BoxCollider2D` (1×1, gameplay size, deliberately not matched to the art), all gameplay scripts, **`Animator`** (the user's preference: Animator on the parent, renderers on children; Apply Root Motion off, Culling Always Animate, controller `Assets/Animation/Player/Player.controller`) and **`PlayerAnimator`**.
- **`Visual`** child = the `SpriteRenderer`, moved off the root with all its settings (placeholder "Test White", 16 px @ 16 PPU = 1 unit, so it matches the collider exactly). Put any extra renderers (body parts, weapon, effects) under it. Facing comes from the root's `localScale.x` sign, which children inherit. Never use the SpriteRenderer's Flip X.
- `PlayerColorSync` finds the renderer with `GetComponentInChildren<SpriteRenderer>()`. Its tint multiplies the art (host = white).

**`PlayerAnimator`** (Scripts/Characters/Player) sets Animator parameters in `LateUpdate` from the simulated state. It's read-only and only sets parameters the current controller defines (cached from `animator.parameters` whenever the controller changes), so a partial controller doesn't spam warnings. The parameters are:
- `speed` (|moveAmount.x|) and `verticalSpeed` (moveAmount.y),
- `grounded` (collisions.below),
- `knockback` (IsDirectionalKnockbackActive), `stunned` (PlayerStamina), `attacking` (`PlayerAttack.IsAttacking`), `dead` (health ≤ 0),
- `hurt` (a trigger fired when health dropped since the previous frame).

The starter controller defines all eight, plus an empty default `Idle` state.

**Triggers removed from the simulation:** `CharacterStats` used to call `animator.SetTrigger("damaged"/"die")` in 7 places, including inside the rollback simulation (`InflictDamage`). That would fire on guessed hits that get undone and again on every resimulated frame. All of them now go through `PlayHitAnimation()`, which does nothing when a `PlayerAnimator` is present, so players get hurt/dead only from `PlayerAnimator`. Characters without one keep the old behaviour.

**Rules for clips and controllers (the user is building these):**
- Only animate children (`Visual/...`). Never key the root Transform (position = simulated position, scale.x = facing) or any component on the root.
- Keep Apply Root Motion off. With Write Defaults on, the Animator only writes properties that some clip animates, so following the rule above keeps it off the root.
- Never call gameplay methods from animation events: `EntityController.DisableInputOn/Off`, `ZeroMoveX`, `ContinueMoveX`, `DisableResting`, and `CharacterStats.PlayWeaponSwing` are all marked "used in animations" but would desync. Sound/VFX events are fine.
- No `NetworkAnimator`/`NetworkTransform`/`NetworkObject` on the player or its children; rollback already makes both screens identical.
- The collider size can be changed in the prefab (not at runtime). It affects 1-unit level gaps, hitbox size (× `hitboxSizeScale`), attack reach from the center, the bar offsets (0.87/0.75 above the pivot) and spawn spacing (1.5). Rebuild both builds after any prefab change.

Verified in Play mode: the renderer is on `Visual`, sprite bounds = collider bounds, the Animator on the root receives live parameters, and the Console is clean.
