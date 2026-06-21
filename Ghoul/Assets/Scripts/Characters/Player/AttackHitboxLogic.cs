using System.Collections.Generic;
using UnityEngine;

public class AttackHitboxLogic : MonoBehaviour
{
    private GameObject attacker;
    private float damage;
    private Vector2 knockbackPower;
    private float knockbackTime;
    private int attackDir;
    private readonly HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    public void Initialize(GameObject attacker, float damage, Vector2 knockbackPower, float knockbackTime, Vector2 attackDirection)
    {
        this.attacker = attacker;
        this.damage = damage;
        this.knockbackPower = knockbackPower;
        this.knockbackTime = knockbackTime;
        attackDir = attackDirection.x >= 0f ? 1 : -1;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CharacterStats stats = other.GetComponentInParent<CharacterStats>();
        if (stats == null) return;
        if (stats.gameObject == attacker) return;
        if (hitTargets.Contains(stats.gameObject)) return;
        hitTargets.Add(stats.gameObject);

        IDamageable damageable = stats;
        damageable.InflictDamage(damage);
        damageable.Knockback(knockbackPower, knockbackTime, attackDir);
    }
}
