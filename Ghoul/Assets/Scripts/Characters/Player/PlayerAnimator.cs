using System.Collections.Generic;
using UnityEngine;

// Drives the player's Animator (on the root, next to the gameplay components) from the
// simulated state, once per rendered frame. Animation is cosmetic only:
//   - It READS simulation state and never writes it, so rollback stays deterministic (the
//     Animator runs on render time and isn't rolled back). After a rollback correction the
//     animation simply follows the corrected state next frame.
//   - Clips must only animate the art on child objects (e.g. "Visual") — never the root's
//     Transform (position = simulated position, scale.x sign = facing) or its collider — and
//     Apply Root Motion stays off. Don't call gameplay methods from animation events.
//
// Parameters (all optional — only those present in the Animator Controller are set):
//   speed (float)          |horizontal velocity|, world units/sec
//   verticalSpeed (float)  vertical velocity (+ up)
//   grounded (bool)        standing on the ground
//   knockback (bool)       being knocked back
//   stunned (bool)         stunned (PlayerStamina)
//   attacking (bool)       an attack swing is travelling
//   dead (bool)            health is 0
//   hurt (trigger)         health dropped since the previous frame
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedId         = Animator.StringToHash("speed");
    private static readonly int VerticalSpeedId = Animator.StringToHash("verticalSpeed");
    private static readonly int GroundedId      = Animator.StringToHash("grounded");
    private static readonly int KnockbackId     = Animator.StringToHash("knockback");
    private static readonly int StunnedId       = Animator.StringToHash("stunned");
    private static readonly int AttackingId     = Animator.StringToHash("attacking");
    private static readonly int DeadId          = Animator.StringToHash("dead");
    private static readonly int HurtId          = Animator.StringToHash("hurt");

    private Animator animator;
    private EntityController entity;
    private Controller2D controller2D;
    private CharacterStats stats;
    private PlayerStamina stamina;
    private PlayerAttack attack;

    // Parameters the current controller defines (setting a missing one logs a warning
    // every frame). Rebuilt whenever the controller asset changes.
    private readonly HashSet<int> availableParams = new HashSet<int>();
    private RuntimeAnimatorController cachedController;

    private float lastHealth = -1f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        entity = GetComponent<EntityController>();
        controller2D = GetComponent<Controller2D>();
        stats = GetComponent<CharacterStats>();
        stamina = GetComponent<PlayerStamina>();
        attack = GetComponent<PlayerAttack>();
    }

    private void LateUpdate()
    {
        if (animator.runtimeAnimatorController == null) { return; }
        if (animator.runtimeAnimatorController != cachedController) { CacheParameters(); }

        if (entity != null)
        {
            SetFloat(SpeedId, Mathf.Abs(entity.MoveAmount.x));
            SetFloat(VerticalSpeedId, entity.MoveAmount.y);
            SetBool(KnockbackId, entity.IsDirectionalKnockbackActive);
        }
        if (controller2D != null) { SetBool(GroundedId, controller2D.collisions.below); }
        SetBool(StunnedId, stamina != null && stamina.IsStunned);
        SetBool(AttackingId, attack != null && attack.IsAttacking);

        if (stats != null)
        {
            float health = stats.CurrentHealth;
            SetBool(DeadId, health <= 0f);
            if (lastHealth >= 0f && health < lastHealth && health > 0f && availableParams.Contains(HurtId))
            {
                animator.SetTrigger(HurtId);
            }
            lastHealth = health;
        }
    }

    private void CacheParameters()
    {
        cachedController = animator.runtimeAnimatorController;
        availableParams.Clear();
        foreach (AnimatorControllerParameter p in animator.parameters) { availableParams.Add(p.nameHash); }
    }

    private void SetFloat(int id, float value)
    {
        if (availableParams.Contains(id)) { animator.SetFloat(id, value); }
    }

    private void SetBool(int id, bool value)
    {
        if (availableParams.Contains(id)) { animator.SetBool(id, value); }
    }
}
