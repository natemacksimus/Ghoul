using System.Collections;
using UnityEngine;
using Unity.Netcode;

// Spawns a square hitbox that originates at the player's position and travels in the
// attack direction for a configurable distance. The direction is chosen at the moment
// the attack button is pressed (see PlayerController.UseRightHand/UseLeftHand): the
// aimed stick direction, or the last aimed direction if centered. On contact the hitbox knocks
// the target back along its travel direction (AttackHitboxLogic).
public class PlayerAttack : MonoBehaviour
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
    [Tooltip("Knockback speed applied to a hit target, in world units per second.")]
    [SerializeField] private float knockbackPower = 30f;
    [SerializeField] private float knockbackTime = 0.35f;
    [Tooltip("How many times a knocked-back target reflects off surfaces before recovering.")]
    [SerializeField] private int knockbackBounces = 1;
    [Tooltip("Fraction of speed kept after each bounce (1 = no energy loss, 0 = stops on impact).")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackBounciness = 1f;

    private float cooldownTimer;
    private BoxCollider2D playerCollider;
    private EntityController entityController;
    private NetworkObject networkObject;
    private static Sprite squareSprite;

    private bool IsOwner => networkObject != null && networkObject.IsOwner;

    private void Awake()
    {
        playerCollider = GetComponent<BoxCollider2D>();
        entityController = GetComponent<EntityController>();
        networkObject = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
    }

    // Called on attack-button press with the resolved attack direction (current input, or
    // last input if none is held). Fires immediately if not on cooldown.
    public void Attack(Vector2 direction)
    {
        if (!IsOwner) return;
        if (cooldownTimer > 0f) return;
        cooldownTimer = attackCooldown;

        Vector2 dir = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (entityController != null && entityController.IsFacingRight ? Vector2.right : Vector2.left);

        StartCoroutine(RunHitbox(dir));
    }

    private IEnumerator RunHitbox(Vector2 dir)
    {
        Vector2 startCenter = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)transform.position;
        Vector2 playerSize = playerCollider != null ? (Vector2)playerCollider.bounds.size : Vector2.one;
        Vector2 hitboxSize = playerSize * hitboxSizeScale;

        GameObject hitbox = new GameObject("AttackHitbox");
        hitbox.transform.position = startCenter;
        hitbox.transform.localScale = new Vector3(hitboxSize.x, hitboxSize.y, 1f);

        SpriteRenderer sr = hitbox.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.color = Color.red;
        sr.sortingOrder = 10;

        BoxCollider2D col = hitbox.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;

        AttackHitboxLogic logic = hitbox.AddComponent<AttackHitboxLogic>();
        logic.Initialize(gameObject, attackDamage, knockbackPower, knockbackTime, knockbackBounces, knockbackBounciness, dir);

        // Slide the hitbox outward from the player until it has covered hitboxDistance.
        float traveled = 0f;
        while (traveled < hitboxDistance && hitbox != null)
        {
            float step = hitboxSpeed * Time.deltaTime;
            if (traveled + step > hitboxDistance) { step = hitboxDistance - traveled; }
            hitbox.transform.position += (Vector3)(dir * step);
            traveled += step;
            yield return null;
        }

        if (hitbox != null) Destroy(hitbox);
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
}
