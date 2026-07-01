using System.Collections.Generic;
using UnityEngine;

// Rides on the moving attack hitbox spawned by PlayerAttack. When the hitbox overlaps
// another character it deals damage and applies a directional knockback that flings the
// target along the hitbox's travel direction (with reflection bounces handled by the
// target's EntityController).
public class AttackHitboxLogic : MonoBehaviour
{
    private GameObject attacker;
    private float damage;
    private float knockbackPower;
    private float knockbackTime;
    private int knockbackBounces;
    private float knockbackBounciness;
    private Vector2 moveDirection;
    private readonly HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    public void Initialize(GameObject attacker, float damage, float knockbackPower, float knockbackTime, int knockbackBounces, float knockbackBounciness, Vector2 moveDirection)
    {
        this.attacker = attacker;
        this.damage = damage;
        this.knockbackPower = knockbackPower;
        this.knockbackTime = knockbackTime;
        this.knockbackBounces = knockbackBounces;
        this.knockbackBounciness = knockbackBounciness;
        this.moveDirection = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector2.right;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CharacterStats stats = other.GetComponentInParent<CharacterStats>();
        if (stats == null) return;
        if (stats.gameObject == attacker) return;
        if (hitTargets.Contains(stats.gameObject)) return;
        hitTargets.Add(stats.gameObject);

        stats.InflictDamage(damage);
        // Knockback travels in the direction the hitbox is moving; the target bounces off
        // surfaces knockbackBounces times (law of reflection) before it recovers.
        stats.KnockbackDirectional(moveDirection, knockbackPower, knockbackTime, knockbackBounces, knockbackBounciness);
    }
}
