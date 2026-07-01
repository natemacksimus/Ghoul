using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public abstract class EntityController : NetworkBehaviour, IKillable
{
    [ShowOnly][SerializeField] protected Vector2 moveAmount;

    [SerializeField] protected bool isFacingRight = false;
    public bool IsFacingRight { get { return isFacingRight; } }

    [SerializeField] protected bool lockFacingDir = false;
    public bool LockFacingDir { get { return lockFacingDir; } set { lockFacingDir = value; } }

    [SerializeField] protected bool disableInput = false;

    [SerializeField] protected bool disableJump = false;

    [SerializeField] protected GameObject characterUI;

    [SerializeField] protected Damage hitObject;
    public Damage HitObject { get { return hitObject; } }

    public Vector2 MoveAmount { get { return moveAmount; } set { moveAmount = value; } }

    [ShowOnly] [SerializeField] protected float previousYVelocity;
    public float PreviousYVelocity { get { return previousYVelocity; } set { previousYVelocity = value; } }

    [ShowOnly] [SerializeField] protected float previousXVelocity;
    public float PreviousXVelocity { get { return previousXVelocity; } set { previousXVelocity = value; } }

    [ShowOnly] [SerializeField] protected float lastXVelocity;
    public float LastXVelocity { get { return lastXVelocity; } set { lastXVelocity = value; } }

    [ShowOnly] [SerializeField] protected float airTime;
    public float AirTime { get { return airTime; } set { airTime = value; } }

    [ShowOnly][SerializeField] protected float yValueAirMax;
    public float YValueAirMax { get { return yValueAirMax; } set { yValueAirMax = value; } }

    [ShowOnly][SerializeField] protected float yValueAirMin;
    public float YValueAirMin { get { return yValueAirMin; } set { yValueAirMin = value; } }

    [SerializeField] protected bool isDead = false;
    public bool IsDead { get => isDead; set => isDead = value; }

    [SerializeField] protected bool invincible = false;
    public bool Invincible { get { return invincible; } set { invincible = value; } }

    [SerializeField] protected bool isResting = false;
    public bool IsResting { get { return isResting; } set { isResting = value; } }

    [SerializeField] protected bool isAttacking = false;
    public bool IsAttacking { get { return isAttacking; } set { isAttacking = value; } }

    [SerializeField] protected bool isRunning = false;
    public bool IsRunning { get { return isRunning; } set { isRunning = value; } }

    [SerializeField] protected bool isDodging = false;
    public bool IsDodging { get { return isDodging; } set { isDodging = value; } }

    [SerializeField] protected bool zeroMoveX = false;
    public bool ZeroMoveX { get { return zeroMoveX; } set { zeroMoveX = value; } }

    [SerializeField] protected bool continueMoveX = false;
    public bool ContinueMoveX { get { return continueMoveX; } set { continueMoveX = value; } }

    [ShowOnly][SerializeField] protected Vector2 knockbackForce = Vector2.zero;
    public Vector2 KnockbackForce { get { return knockbackForce; } set { knockbackForce = value; } }

    [ShowOnly][SerializeField] protected float knockbackTimer;
    public float KnockbackTimer { get { return knockbackTimer; } set { knockbackTimer = value; } }

    //protected float knockbackSmoothing;

    [SerializeField] protected bool isKnockbacked = false;
    public bool IsKnockbacked { get { return isKnockbacked; } set { isKnockbacked = value; } }

    [SerializeField] private float knockbackReduceFactor = 0.1f;

    // Directional knockback (used by the redesigned PlayerAttack hitbox). While active
    // the character is flung along knockbackVelocity and reflects off surfaces (angle of
    // reflection = angle of incidence) up to knockbackBouncesRemaining times. This runs
    // in parallel with the legacy horizontal knockback above without disturbing it.
    [ShowOnly][SerializeField] protected bool directionalKnockback = false;
    public bool IsDirectionalKnockbackActive { get { return directionalKnockback; } }

    [ShowOnly][SerializeField] protected Vector2 knockbackVelocity = Vector2.zero;
    [ShowOnly][SerializeField] protected int knockbackBouncesRemaining = 0;
    [ShowOnly][SerializeField] protected float knockbackBounciness = 1f;  // speed retained per bounce (0..1)

    // Syncs the owner's facing direction to all other clients.
    private NetworkVariable<bool> netFacingRight = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    protected Controller2D controller2D;
    protected AudioSource audioSource;
    protected BoxCollider2D boxCollider2D;

    protected Animator animator;
    public Animator Animator { get { return animator; } }

    protected virtual void Start()
    {
        boxCollider2D = GetComponent<BoxCollider2D>();
    }

    public override void OnNetworkSpawn()
    {
        netFacingRight.OnValueChanged += OnFacingDirectionChanged;
        if (IsOwner)
        {
            netFacingRight.Value = isFacingRight;
        }
        else
        {
            SyncFacingDirection(netFacingRight.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        netFacingRight.OnValueChanged -= OnFacingDirectionChanged;
    }

    private void OnFacingDirectionChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner) { SyncFacingDirection(newValue); }
    }

    private void SyncFacingDirection(bool facingRight)
    {
        if (isFacingRight == facingRight) { return; }
        isFacingRight = facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
        if (characterUI != null)
        {
            Vector3 uiScale = characterUI.transform.localScale;
            uiScale.x *= -1;
            characterUI.transform.localScale = uiScale;
        }
    }

    protected virtual void Update()
    {
        previousYVelocity = moveAmount.y;
        previousXVelocity = moveAmount.x;
    }
    protected virtual void FixedUpdate()
    {
        if (directionalKnockback) { HandleDirectionalKnockback(); }
        else { HandleKnockback(); }

        // Count up fallTime when character is in the air. (Jump resets fallTime)
        if (!controller2D.collisions.below) { airTime += Time.deltaTime; }


        //if (isResting) { moveAmount.x = 0; }
    }

    private void HandleKnockback()
    {
        if (knockbackTimer > 0)
        {
            knockbackTimer -= Time.deltaTime;
            int signX = knockbackForce.x > 0 ? -1 : 1;

            float newKnockbackX = Mathf.Abs(knockbackForce.x) > 0 ? knockbackForce.x * (1 - knockbackReduceFactor) : 0;

            knockbackForce = new Vector2(newKnockbackX, moveAmount.y);

            //Debug.Log("newX: " + newKnockbackX);

            if (this.tag == "Player")
            {
                moveAmount.x = knockbackForce.x;
            }
            else
            {
                moveAmount.x = knockbackForce.x * Time.deltaTime;
            }
        }
        else
        {
            if (isKnockbacked)
            {
                lockFacingDir = false;
                if (disableInput) { DisableInputOff(); }
                isKnockbacked = false;
            }
        }
    }

    // Drives a directional knockback for one physics step: reflects the velocity off any
    // surface hit last frame (up to the allowed bounce count) then feeds the velocity to
    // moveAmount so the normal Move() pipeline translates the character. PlayerController
    // suppresses gravity/velocity-clamping while this is active so the flight stays
    // straight between bounces and the reflection is clean.
    private void HandleDirectionalKnockback()
    {
        if (knockbackTimer <= 0f) { EndDirectionalKnockback(); return; }
        knockbackTimer -= Time.deltaTime;

        // controller2D.collisions reflects last frame's Move. If we ran into a surface,
        // bounce off it; once the configured bounces are spent, end the knockback.
        Vector2 normal = GetCollisionNormal();
        if (normal != Vector2.zero && Vector2.Dot(knockbackVelocity, normal) < 0f)
        {
            if (knockbackBouncesRemaining > 0)
            {
                // Reflect (angle out = angle in), then scale the speed by bounciness so
                // each bounce can lose energy. A value of 1 keeps the speed unchanged.
                knockbackVelocity = Vector2.Reflect(knockbackVelocity, normal) * knockbackBounciness;
                knockbackBouncesRemaining--;
            }
            else
            {
                EndDirectionalKnockback();
                return;
            }
        }

        knockbackForce = knockbackVelocity;
        moveAmount = knockbackVelocity;
    }

    // Builds a surface normal from the controller's last collision result. Slopes carry a
    // real normal; axis-aligned walls/floors/ceilings derive one from the collision flags.
    private Vector2 GetCollisionNormal()
    {
        if (controller2D == null) { return Vector2.zero; }
        Controller2D.CollisionInfo c = controller2D.collisions;
        if (c.slopeNormal != Vector2.zero) { return c.slopeNormal; }

        Vector2 n = Vector2.zero;
        if (c.below) { n += Vector2.up; }
        if (c.above) { n += Vector2.down; }
        if (c.left)  { n += Vector2.right; }
        if (c.right) { n += Vector2.left; }
        return n == Vector2.zero ? Vector2.zero : n.normalized;
    }

    private void EndDirectionalKnockback()
    {
        directionalKnockback = false;
        knockbackVelocity = Vector2.zero;
        knockbackBouncesRemaining = 0;
        if (isKnockbacked)
        {
            lockFacingDir = false;
            if (disableInput) { DisableInputOff(); }
            isKnockbacked = false;
        }
    }

    public virtual void Kill()
    {

    }

    public virtual void ApplyKnockback(Vector2 knockbackPower, float knockbackTime)
    {
        if (invincible) { return; }
        
        //Debug.Log("knockbackTime: " + knockbackTime);
        // Hit received determines the knockback power and stun time
        isKnockbacked = true;
        lockFacingDir = true;  // might be able to use only isKnockbacked to do this

        knockbackForce = knockbackPower;
        knockbackTimer = knockbackTime;

        DisableInputOn();

        if (this.tag == "Player")
        {
            moveAmount = new Vector2(knockbackForce.x, knockbackForce.y);   // knockbackForce * Time.deltaTime;  // 
        }
        else
        {
            moveAmount = new Vector2(knockbackForce.x, knockbackForce.y) * Time.deltaTime;   // knockbackForce * Time.deltaTime;  // 
        }
    }

    // Entry point for the redesigned PlayerAttack knockback (routed here from
    // CharacterStats on the receiver's owning client). velocity is the full 2D fling
    // velocity; bounces is how many times it may reflect off surfaces.
    public virtual void ApplyDirectionalKnockback(Vector2 velocity, float knockbackTime, int bounces, float bounciness)
    {
        if (invincible) { return; }

        isKnockbacked = true;
        lockFacingDir = true;
        directionalKnockback = true;
        knockbackVelocity = velocity;
        knockbackBouncesRemaining = Mathf.Max(0, bounces);
        knockbackBounciness = Mathf.Clamp01(bounciness);
        knockbackTimer = knockbackTime;

        DisableInputOn();

        knockbackForce = velocity;
        moveAmount = velocity;
    }

    public virtual void Jump()
    {
        // implemented in derived class


    }

    public virtual void Flip()
    {
        isFacingRight = !isFacingRight;
        if (IsSpawned && IsOwner) { netFacingRight.Value = isFacingRight; }

        Vector3 playerScale = transform.localScale;
        playerScale.x *= -1;
        transform.localScale = playerScale;

        if (characterUI == null) { return; }
        Vector3 characterUIScale = characterUI.transform.localScale;
        characterUIScale.x *= -1;
        characterUI.transform.localScale = characterUIScale;
    }

    // Used in animations
    public void InvincibilityOn()
    {
        //Debug.Log("Invinc on");
        invincible = true;
    }

    // Used in animations
    public void InvincibilityOff()
    {
        //Debug.Log("Invinc off");
        invincible = false;
    }

    public void ToggleBoxCollider(bool isOn)
    {
        boxCollider2D.enabled = isOn;
    }

    //public void EnableAnimator(bool set)
    //{
    //    if (animator != null) 
    //    { 
    //        animator.enabled = set;
    //        Debug.Log("animator enabled: " + animator.enabled);
    //    }
    //}

    // Following methods are used in animations

    // in resting anim
    public void DisableInputOn()
    {
        //Debug.Log("disable input on");
        disableInput = true;
    }

    // in resting off anim
    public void DisableInputOff()
    {
        //Debug.Log("disable input off");
        disableInput = false;
    }

    // in damageTaken anim
    public void DisableResting()
    {
        //Debug.Log("disable resting");
        isResting = false;
        animator.SetBool("isResting", false);
    }

    // in attack anim, resting anim
    public void EnableZeroMoveX()
    {
        zeroMoveX = true;
        disableJump = true;
        lockFacingDir = true;
    }

    // in attack anim
    public void DisableZeroMoveX()
    {
        zeroMoveX = false;
        disableJump = false;
        lockFacingDir = false;
    }

    // in attack anim
    public void EnableContinueMoveX()
    {
        continueMoveX = true;
    }

    // in attack anim
    public void DisableContinueMoveX()
    {
        continueMoveX = false;
    }

    // Used in animations to enable hit object
    public virtual void EnableDamageObject()
    {
        if (hitObject == null) { return; }
        hitObject.EnableHitObject();
    }

    // Used in animations to disable hit object
    public virtual void DisableDamageObject()
    {
        if (hitObject == null) { return; }
        hitObject.DisableHitObject();
    }

    public void SetIsAttackingOn()
    {
        isAttacking = true;
    }

    public void SetIsAttackingOff()
    {
        isAttacking = false;
    }

    public void SetIsDodgingOff()
    {
        isDodging = false;
    }

    public virtual void PlayFootStep()
    {
        // implemented derived class
    }

    public virtual void PlayHurtSound()
    {
        // implemented derived class
    }

    [System.Serializable]
    public class PlayerSounds
    {
        public AudioClip[] footstepSound;
        public AudioClip jumpSound;
        public AudioClip hitGroundSound;
        public AudioClip[] hurtSound;
        public AudioClip wallClimbStart;
        public AudioClip wallClimbSlide;
    }
}
