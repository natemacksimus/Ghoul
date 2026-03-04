using UnityEngine;

public interface IDamageable
{
    void InflictDamage(float damageTaken);

    void Knockback(Vector2 knockbackPower, float knockbackTime, int attackDir);
}
