using System.IO;
using UnityEngine;

// Simulates a square hitbox that travels from the player's position in the attack
// direction for a configurable distance. Fully driven by the deterministic rollback
// tick (Tick(), called once per SimulateFrame from PlayerController) rather than a
// coroutine, and resolved with a manual bounds check rather than a Physics2D trigger,
// so both peers reach identical results from identical inputs — no ownership gating,
// no reliance on Unity's async trigger-callback timing. Damage/knockback are applied
// as direct local state changes (CharacterStats/EntityController already fall back to
// local application when a rollback session is active).
public class PlayerAttack : MonoBehaviour, ISnapshotable
{
    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private float attackDamage = 20f;

    [Header("Hitbox travel")]
    [Tooltip("How far the hitbox travels from the player, in world units.")]
    [SerializeField] private float hitboxDistance = 4f;
    [Tooltip("Hitbox travel speed, in world units per second.")]
    [SerializeField] private float hitboxSpeed = 20f;
    [Tooltip("Hitbox size as a fraction of the player's collider size.")]
    [SerializeField] private float hitboxSizeScale = 0.5f;

    [Header("Knockback")]
    // Whether a hit knocks back, and how hard, is decided by the target's PlayerStamina
    // (its Knockback Force, scaled by the attacking weapon's Knockback Power Multiplier).
    [Tooltip("Safety cap on knockback duration, in seconds (scaled by the weapon's Knockback Duration Multiplier). Normally the knockback ends earlier, when its speed decays below the target's Knockback Stop Speed.")]
    [SerializeField] private float knockbackTime = 1f;
    [Tooltip("How many times a knocked-back target bounces off walls, floors or ceilings (angle of reflection = angle of incidence). The next surface impact after that ends the knockback.")]
    [SerializeField] private int knockbackBounces = 1;
    [Tooltip("Fraction of speed kept after each bounce (1 = no energy loss, 0 = stops on impact).")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackBounciness = 1f;

    // ── Simulated state (snapshotted) ───────────────────────────────────────
    private float cooldownTimer;
    private bool attackActive;
    private Vector2 attackDirection;
    private float attackTraveled;
    private bool hasHitTarget;
    private float weaponKnockbackPowerMul = 1f;     // from the weapon that started this swing
    private float weaponKnockbackDurationMul = 1f;

    private BoxCollider2D playerCollider;
    private EntityController entityController;
    private PlayerStamina stamina;
    private CharacterStats otherTarget;
    private PlayerStamina otherTargetStamina;

    // Cosmetic only — recreated from simulated state each tick, never snapshotted.
    private GameObject visual;
    private SpriteRenderer visualSprite;
    private static Sprite squareSprite;

    private void Awake()
    {
        playerCollider = GetComponent<BoxCollider2D>();
        entityController = GetComponent<EntityController>();
        stamina = GetComponent<PlayerStamina>();
    }

    // True while a swing's hitbox is travelling (read by PlayerAnimator; cosmetic use only).
    public bool IsAttacking => attackActive;

    // Called on attack-button press with the resolved attack direction (current input, or
    // last input if none is held) and the weapon in that hand (null = empty hand). Starts the
    // swing immediately if not on cooldown and not stunned, and costs stamina.
    public void Attack(Vector2 direction, Item weapon = null)
    {
        if (cooldownTimer > 0f || attackActive) return;
        if (stamina != null && stamina.IsStunned) return;
        cooldownTimer = attackCooldown;
        if (stamina != null) { stamina.OnAttack(); }

        weaponKnockbackPowerMul = weapon != null ? weapon.knockbackPowerMultiplier : 1f;
        weaponKnockbackDurationMul = weapon != null ? weapon.knockbackDurationMultiplier : 1f;

        Vector2 dir = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (entityController != null && entityController.IsFacingRight ? Vector2.right : Vector2.left);

        attackActive = true;
        attackDirection = dir;
        attackTraveled = 0f;
        hasHitTarget = false;
    }

    // Advances the active swing (if any) by one fixed simulation step. Called every
    // SimulateFrame tick — both for live simulation and rollback resimulation — so its
    // outcome depends only on simulated state and Time.fixedDeltaTime, never real time.
    public void Tick()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.fixedDeltaTime;

        if (!attackActive)
        {
            SetVisualActive(false);
            return;
        }

        float step = hitboxSpeed * Time.fixedDeltaTime;
        if (attackTraveled + step > hitboxDistance) { step = hitboxDistance - attackTraveled; }
        attackTraveled += step;

        Vector2 center = HitboxOrigin() + attackDirection * attackTraveled;

        if (!hasHitTarget) { CheckHit(center); }

        UpdateVisual(center);

        if (attackTraveled >= hitboxDistance)
        {
            attackActive = false;
            SetVisualActive(false);
        }
    }

    private Vector2 HitboxOrigin() =>
        playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)transform.position;

    private void CheckHit(Vector2 center)
    {
        CharacterStats target = FindOtherTarget();
        if (target == null) return;
        BoxCollider2D targetCollider = target.GetComponent<BoxCollider2D>();
        if (targetCollider == null) return;

        Vector2 playerSize = playerCollider != null ? (Vector2)playerCollider.bounds.size : Vector2.one;
        Vector2 hitboxSize = playerSize * hitboxSizeScale;
        var hitboxBounds = new Bounds(center, hitboxSize);

        if (!hitboxBounds.Intersects(targetCollider.bounds)) return;

        hasHitTarget = true;
        target.InflictDamage(attackDamage);

        // The target's stamina (checked before this hit's cost) decides the reaction:
        // damage only, knockback, or knockback + stun. A target without stamina is always
        // knocked back, as before.
        PlayerStamina.HitReaction reaction = otherTargetStamina != null
            ? otherTargetStamina.OnHitTaken()
            : PlayerStamina.HitReaction.Knockback;
        if (reaction == PlayerStamina.HitReaction.None) return;

        // Knockback travels in the direction the hitbox is moving; the target bounces off
        // surfaces knockbackBounces times (law of reflection) before it recovers. Force comes
        // from the target, scaled by this swing's weapon; the weapon also stretches duration.
        float force = (otherTargetStamina != null ? otherTargetStamina.KnockbackForce : DefaultKnockbackForce) * weaponKnockbackPowerMul;
        target.KnockbackDirectional(attackDirection, force, knockbackTime, knockbackBounces, knockbackBounciness, weaponKnockbackDurationMul);
    }

    // Knockback force for a target that has no PlayerStamina (none exist today).
    private const float DefaultKnockbackForce = 20f;

    // Exactly one other player exists in the current 2-player co-op model — resolved
    // once and cached rather than snapshotted (it's a fixed scene reference, not state).
    private CharacterStats FindOtherTarget()
    {
        if (otherTarget != null) return otherTarget;
        var all = FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        foreach (var cs in all)
        {
            if (cs.gameObject != gameObject) { otherTarget = cs; break; }
        }
        if (otherTarget != null) { otherTargetStamina = otherTarget.GetComponent<PlayerStamina>(); }
        return otherTarget;
    }

    // ── Cosmetic hitbox visual ───────────────────────────────────────────────

    private void UpdateVisual(Vector2 center)
    {
        EnsureVisual();
        visual.transform.position = center;
        Vector2 playerSize = playerCollider != null ? (Vector2)playerCollider.bounds.size : Vector2.one;
        Vector2 hitboxSize = playerSize * hitboxSizeScale;
        visual.transform.localScale = new Vector3(hitboxSize.x, hitboxSize.y, 1f);
        SetVisualActive(true);
    }

    private void SetVisualActive(bool active)
    {
        if (visual == null) { if (!active) return; EnsureVisual(); }
        if (visual.activeSelf != active) { visual.SetActive(active); }
    }

    private void EnsureVisual()
    {
        if (visual != null) return;
        visual = new GameObject("AttackHitboxVisual");
        visual.transform.SetParent(null);
        visualSprite = visual.AddComponent<SpriteRenderer>();
        visualSprite.sprite = GetSquareSprite();
        visualSprite.color = Color.red;
        visualSprite.sortingOrder = 10;
        visual.SetActive(false);
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        return squareSprite;
    }

    private void OnDestroy()
    {
        if (visual != null) { Destroy(visual); }
    }

    // ── ISnapshotable ─────────────────────────────────────────────────────

    public void SaveState(BinaryWriter w)
    {
        w.Write(cooldownTimer);
        w.Write(attackActive);
        w.Write(attackDirection.x); w.Write(attackDirection.y);
        w.Write(attackTraveled);
        w.Write(hasHitTarget);
        w.Write(weaponKnockbackPowerMul);
        w.Write(weaponKnockbackDurationMul);
    }

    public void LoadState(BinaryReader r)
    {
        cooldownTimer    = r.ReadSingle();
        attackActive     = r.ReadBoolean();
        attackDirection  = new Vector2(r.ReadSingle(), r.ReadSingle());
        attackTraveled   = r.ReadSingle();
        hasHitTarget     = r.ReadBoolean();
        weaponKnockbackPowerMul    = r.ReadSingle();
        weaponKnockbackDurationMul = r.ReadSingle();

        if (attackActive) { UpdateVisual(HitboxOrigin() + attackDirection * attackTraveled); }
        else { SetVisualActive(false); }
    }
}
