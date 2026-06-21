using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private Vector2 attackKnockback = new Vector2(8f, 5f);
    [SerializeField] private float attackKnockbackTime = 0.35f;

    private float cooldownTimer;
    private bool pendingAttack;
    private BoxCollider2D playerCollider;
    private EntityController entityController;
    private NetworkObject networkObject;
    private static Sprite squareSprite;

    public bool IsAttackPending => pendingAttack;

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

    // Called when the attack button is pressed. Starts the cooldown and waits for
    // the player to press a direction before firing.
    public void BeginAttack()
    {
        if (!IsOwner) return;
        if (cooldownTimer > 0f) return;
        cooldownTimer = attackCooldown;
        pendingAttack = true;
    }

    // Called by PlayerController once a non-zero directional input is detected.
    public void FirePendingAttack(Vector2 dir)
    {
        pendingAttack = false;
        StartCoroutine(SpawnAttackHitbox(dir));
    }

    private IEnumerator SpawnAttackHitbox(Vector2 dir)
    {
        if (dir == Vector2.zero)
            dir = (entityController != null && entityController.IsFacingRight) ? Vector2.right : Vector2.left;

        Vector2 playerSize = playerCollider != null ? playerCollider.bounds.size : Vector2.one;
        Vector2 hitboxSize = playerSize * 0.5f;

        Vector2 offset = new Vector2(
            dir.x != 0f ? dir.x * (playerSize.x * 0.5f + hitboxSize.x * 0.5f) : 0f,
            dir.y != 0f ? dir.y * (playerSize.y * 0.5f + hitboxSize.y * 0.5f) : 0f
        );

        Vector3 hitboxPos = (playerCollider != null ? playerCollider.bounds.center : transform.position)
                            + (Vector3)offset;

        GameObject hitbox = new GameObject("AttackHitbox");
        hitbox.transform.position = hitboxPos;
        hitbox.transform.localScale = new Vector3(hitboxSize.x, hitboxSize.y, 1f);

        SpriteRenderer sr = hitbox.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.color = Color.red;
        sr.sortingOrder = 10;

        BoxCollider2D col = hitbox.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;

        AttackHitboxLogic logic = hitbox.AddComponent<AttackHitboxLogic>();
        logic.Initialize(gameObject, attackDamage, attackKnockback, attackKnockbackTime, dir);

        yield return new WaitForSeconds(attackDuration);

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
