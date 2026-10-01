using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;

public abstract class EntityController : NetworkBehaviour, IKillable, ISnapshotable
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

    // Directional knockback (used by the redesigned PlayerAttack hitbox). While active the
    // character is flung along knockbackVelocity (the decaying impulse) plus
    // knockbackFallVelocity (gravity, which builds up normally and is never decayed), so it
    // arcs like a thrown object. It bounces off walls, floors and ceilings (angle of
    // reflection = angle of incidence, using the real surface normal) up to
    // knockbackBouncesRemaining times; the next impact after that ends it, as does the
    // impulse decaying out. This runs in parallel with the legacy horizontal knockback
    // above without disturbing it.
    [ShowOnly][SerializeField] protected bool directionalKnockback = false;
    public bool IsDirectionalKnockbackActive { get { return directionalKnockback; } }

    [ShowOnly][SerializeField] protected Vector2 knockbackVelocity = Vector2.zero;
    [ShowOnly][SerializeField] protected float knockbackFallVelocity = 0f;   // gravity accumulated during the knockback
    [ShowOnly][SerializeField] protected bool knockbackAirborne = false;     // left the ground during this knockback
    [ShowOnly][SerializeField] protected float knockbackDurationScale = 1f;  // from the attacking weapon; >1 = longer push
    [ShowOnly][SerializeField] protected int knockbackBouncesRemaining = 0;
    [ShowOnly][SerializeField] protected float knockbackBounciness = 1f;  // speed retained per bounce (0..1)

    // Directional knockback is an impulse: the hit sets the full velocity instantly, then it
    // decays exponentially (v *= e^(-rate*dt) each step) so the push is sharp at impact and
    // bleeds off quickly. Receiver-side, like drag, so the hit/RPC chain doesn't change;
    // config only (same prefab on both rollback peers), not snapshotted.
    [Tooltip("Exponential decay rate of knockback speed, per second. Higher = sharper, shorter push. Speed after t seconds = initial * e^(-rate * t).")]
    [SerializeField] protected float knockbackDecayRate = 3f;
    [Tooltip("Knockback ends once speed decays below this (world units/sec). The attack's knockback time is only a safety cap.")]
    [SerializeField] protected float knockbackStopSpeed = 1.5f;

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
        // ClientNetworkTransform.SyncScaleX carries the root sprite flip (localScale.x)
        // in the same packet as position, so we must NOT also flip it here — that would
        // double-flip the sprite. Only the UI child counter-flip needs to be applied.
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

    protected void HandleKnockback()
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

    // Drives a directional knockback for one physics step: handles surface impacts (bounce
    // or end) from last frame's collisions, feeds impulse + fall velocity to moveAmount so
    // the normal Move() pipeline translates the character, then decays the impulse for the
    // next step (see knockbackDecayRate). Gravity is added separately by subclasses that
    // have it (PlayerController → ApplyKnockbackGravity), in place of their normal gravity.
    protected void HandleDirectionalKnockback()
    {
        if (knockbackTimer <= 0f) { EndDirectionalKnockback(); return; }
        knockbackTimer -= Time.deltaTime;

        Vector2 total;

        // controller2D.collisions describes last step's Move, and moveAmount still holds the
        // exact velocity that Move used (impulse + fall + that step's gravity) — i.e. the
        // velocity the character actually struck any surface with.
        Controller2D.CollisionInfo c = controller2D != null ? controller2D.collisions : default;
        Vector2 incoming = moveAmount;

        if (!c.below) { knockbackAirborne = true; }

        // Resting contact isn't an impact: a target still on the ground from a sideways or
        // upward hit is just supported by it (gravity only pressed it into the floor), so it
        // slides instead of bouncing. Landing after being airborne, or being knocked down
        // into the floor it stands on, is an impact.
        bool supported = c.below && !knockbackAirborne && knockbackVelocity.y >= 0f;
        if (supported)
        {
            knockbackFallVelocity = 0f;
            if (incoming.y < 0f) { incoming.y = 0f; }  // drop the gravity press into the floor
        }

        Vector2 floorNormal = c.below ? SurfaceNormal(c.verticalHitNormal, Vector2.up)
                            : c.above ? SurfaceNormal(c.verticalHitNormal, Vector2.down)
                            : Vector2.zero;
        Vector2 wallNormal  = c.left  ? SurfaceNormal(c.horizontalHitNormal, Vector2.right)
                            : c.right ? SurfaceNormal(c.horizontalHitNormal, Vector2.left)
                            : Vector2.zero;

        // An impact = moving into a touched surface (velocity against its normal).
        bool hitFloorOrCeiling = !supported && floorNormal != Vector2.zero && Vector2.Dot(incoming, floorNormal) < 0f;
        bool hitWall = wallNormal != Vector2.zero && Vector2.Dot(incoming, wallNormal) < 0f;

        if (hitFloorOrCeiling || hitWall)
        {
            // Out of bounces: the impact ends the knockback (landing, or stopping at a wall).
            if (knockbackBouncesRemaining <= 0)
            {
                EndDirectionalKnockback();
                return;
            }

            // Mirror the incoming velocity across the actual surface normal:
            // Reflect(v, n) = v - 2(v·n)n keeps the tangential part and flips the normal part,
            // so the angle of reflection equals the angle of incidence. A corner hit (floor or
            // ceiling and wall in the same step) mirrors across both, like a ball into a corner,
            // and counts as one bounce.
            Vector2 reflected = incoming;
            if (hitFloorOrCeiling) { reflected = Vector2.Reflect(reflected, floorNormal); }
            if (hitWall) { reflected = Vector2.Reflect(reflected, wallNormal); }

            // Bounciness scales speed only (1 = no loss), so the angle is unchanged. The
            // reflected velocity becomes the new impulse; gravity builds again from zero.
            knockbackVelocity = reflected * knockbackBounciness;
            knockbackFallVelocity = 0f;
            knockbackBouncesRemaining--;
            total = knockbackVelocity;
        }
        else
        {
            total = knockbackVelocity + new Vector2(0f, knockbackFallVelocity);
        }

        knockbackForce = total;
        moveAmount = total;

        // Decay the impulse after moving, so the first step after the hit travels at full
        // speed. Gravity's fall velocity is not decayed.
        knockbackVelocity *= Mathf.Exp(-(knockbackDecayRate / knockbackDurationScale) * Time.deltaTime);
        if (knockbackVelocity.sqrMagnitude < knockbackStopSpeed * knockbackStopSpeed)
        {
            // moveAmount keeps the current fall speed, so normal gravity continues seamlessly.
            EndDirectionalKnockback();
        }
    }

    // Adds one step of gravity during a directional knockback. Called by subclasses with
    // gravity (PlayerController) instead of their normal gravity step. It accumulates in
    // knockbackFallVelocity, because HandleDirectionalKnockback rebuilds moveAmount from the
    // knockback's own state each step, and is also applied to this step's moveAmount.
    protected void ApplyKnockbackGravity(float gravityStep, float maxFallSpeed)
    {
        knockbackFallVelocity += gravityStep;
        if (knockbackFallVelocity < -maxFallSpeed) { knockbackFallVelocity = -maxFallSpeed; }

        moveAmount.y += gravityStep;
        if (moveAmount.y < -maxFallSpeed) { moveAmount.y = -maxFallSpeed; }
    }

    // The real surface normal recorded by Controller2D's raycast (exact for flat ground,
    // slopes and angled walls), normalized; falls back to the axis-aligned normal implied
    // by the collision flag if none was recorded.
    private static Vector2 SurfaceNormal(Vector2 hitNormal, Vector2 fallback) =>
        hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : fallback;

    private void EndDirectionalKnockback()
    {
        directionalKnockback = false;
        knockbackVelocity = Vector2.zero;
        knockbackFallVelocity = 0f;
        knockbackAirborne = false;
        knockbackDurationScale = 1f;
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
    // velocity; bounces is how many times it may reflect off surfaces. durationScale (from the
    // attacking weapon, 1 = normal) stretches how long the push lasts: the impulse decays at
    // knockbackDecayRate / durationScale and the safety cap is scaled to match.
    public virtual void ApplyDirectionalKnockback(Vector2 velocity, float knockbackTime, int bounces, float bounciness, float durationScale = 1f)
    {
        if (invincible) { return; }

        isKnockbacked = true;
        lockFacingDir = true;
        directionalKnockback = true;
        knockbackVelocity = velocity;
        // The hit replaces the current motion; gravity builds up again from zero. A target
        // hit in mid-air counts as airborne, so touching down ends the knockback.
        knockbackFallVelocity = 0f;
        knockbackAirborne = controller2D != null && !controller2D.collisions.below;
        knockbackBouncesRemaining = Mathf.Max(0, bounces);
        knockbackBounciness = Mathf.Clamp01(bounciness);
        knockbackDurationScale = Mathf.Max(0.01f, durationScale);
        knockbackTimer = knockbackTime * knockbackDurationScale;

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

    // ── ISnapshotable ─────────────────────────────────────────────────────
    // Serializes all gameplay-affecting fields. Subclasses call base.SaveState/
    // LoadState and then write/read their own additional fields.

    public virtual void SaveState(BinaryWriter w)
    {
        // Position (the single source of truth for where this character is)
        w.Write(transform.position.x);
        w.Write(transform.position.y);

        // Velocity / movement
        w.Write(moveAmount.x);
        w.Write(moveAmount.y);
        w.Write(previousYVelocity);
        w.Write(previousXVelocity);
        w.Write(lastXVelocity);

        // Facing / state flags
        w.Write(isFacingRight);
        w.Write(lockFacingDir);
        w.Write(disableInput);
        w.Write(disableJump);
        w.Write(isDead);
        w.Write(invincible);
        w.Write(isResting);
        w.Write(isAttacking);
        w.Write(isRunning);
        w.Write(isDodging);
        w.Write(zeroMoveX);
        w.Write(continueMoveX);

        // Legacy knockback
        w.Write(knockbackForce.x);
        w.Write(knockbackForce.y);
        w.Write(knockbackTimer);
        w.Write(isKnockbacked);

        // Directional knockback
        w.Write(directionalKnockback);
        w.Write(knockbackVelocity.x);
        w.Write(knockbackVelocity.y);
        w.Write(knockbackBouncesRemaining);
        w.Write(knockbackBounciness);
        w.Write(knockbackFallVelocity);
        w.Write(knockbackAirborne);
        w.Write(knockbackDurationScale);

        // Air tracking
        w.Write(airTime);
        w.Write(yValueAirMax);
        w.Write(yValueAirMin);

        // Full Controller2D collision state. Reset() only clears above/below/left/right/
        // climbingSlope/descendingSlope/slidingDownMaxSlope/slopeNormal/slopeAngle each
        // Move() call — slopeAngleOld, moveAmountOld, faceDir, and fallingThroughPlatform
        // all persist ACROSS Move() calls and feed back into collision math on the next
        // one (slope-transition detection, slope-climb continuation, one-way-platform
        // drop-through). None of it is a pure function of position alone, so a rollback
        // that doesn't restore it leaves a resimulated frame reading whatever value real
        // time/the live (non-rewound) run happened to leave behind — already confirmed to
        // cause a permanent fall-through-the-level bug for fallingThroughPlatform; the
        // same class of bug is possible for any of the others under sustained correction.
        var c = controller2D.collisions;
        w.Write(c.above); w.Write(c.below); w.Write(c.left); w.Write(c.right);
        w.Write(c.climbingSlope); w.Write(c.descendingSlope); w.Write(c.slidingDownMaxSlope);
        w.Write(c.slopeAngle); w.Write(c.slopeAngleOld);
        w.Write(c.slopeNormal.x); w.Write(c.slopeNormal.y);
        w.Write(c.moveAmountOld.x); w.Write(c.moveAmountOld.y);
        w.Write(c.faceDir);
        w.Write(c.fallingThroughPlatform);
        w.Write(c.horizontalHitNormal.x); w.Write(c.horizontalHitNormal.y);
        w.Write(c.verticalHitNormal.x);   w.Write(c.verticalHitNormal.y);
    }

    public virtual void LoadState(BinaryReader r)
    {
        // Position
        float px = r.ReadSingle(), py = r.ReadSingle();
        transform.position = new Vector3(px, py, transform.position.z);

        // Velocity
        moveAmount        = new Vector2(r.ReadSingle(), r.ReadSingle());
        previousYVelocity = r.ReadSingle();
        previousXVelocity = r.ReadSingle();
        lastXVelocity     = r.ReadSingle();

        // Facing / flags
        bool wasRight = isFacingRight;
        isFacingRight = r.ReadBoolean();
        lockFacingDir = r.ReadBoolean();
        disableInput  = r.ReadBoolean();
        disableJump   = r.ReadBoolean();
        isDead        = r.ReadBoolean();
        invincible    = r.ReadBoolean();
        isResting     = r.ReadBoolean();
        isAttacking   = r.ReadBoolean();
        isRunning     = r.ReadBoolean();
        isDodging     = r.ReadBoolean();
        zeroMoveX     = r.ReadBoolean();
        continueMoveX = r.ReadBoolean();

        // Sync the sprite scale if facing direction was restored to a different value.
        if (isFacingRight != wasRight)
        {
            Vector3 s = transform.localScale;
            s.x *= -1f;
            transform.localScale = s;
        }

        // Legacy knockback
        knockbackForce = new Vector2(r.ReadSingle(), r.ReadSingle());
        knockbackTimer = r.ReadSingle();
        isKnockbacked  = r.ReadBoolean();

        // Directional knockback
        directionalKnockback      = r.ReadBoolean();
        knockbackVelocity         = new Vector2(r.ReadSingle(), r.ReadSingle());
        knockbackBouncesRemaining = r.ReadInt32();
        knockbackBounciness       = r.ReadSingle();
        knockbackFallVelocity     = r.ReadSingle();
        knockbackAirborne         = r.ReadBoolean();
        knockbackDurationScale    = r.ReadSingle();

        // Air tracking
        airTime      = r.ReadSingle();
        yValueAirMax = r.ReadSingle();
        yValueAirMin = r.ReadSingle();

        // Controller2D collision state (see SaveState for why this must be restored too)
        controller2D.collisions.above = r.ReadBoolean();
        controller2D.collisions.below = r.ReadBoolean();
        controller2D.collisions.left  = r.ReadBoolean();
        controller2D.collisions.right = r.ReadBoolean();
        controller2D.collisions.climbingSlope     = r.ReadBoolean();
        controller2D.collisions.descendingSlope   = r.ReadBoolean();
        controller2D.collisions.slidingDownMaxSlope = r.ReadBoolean();
        controller2D.collisions.slopeAngle    = r.ReadSingle();
        controller2D.collisions.slopeAngleOld = r.ReadSingle();
        controller2D.collisions.slopeNormal   = new Vector2(r.ReadSingle(), r.ReadSingle());
        controller2D.collisions.moveAmountOld = new Vector2(r.ReadSingle(), r.ReadSingle());
        controller2D.collisions.faceDir       = r.ReadInt32();
        controller2D.collisions.fallingThroughPlatform = r.ReadBoolean();
        controller2D.collisions.horizontalHitNormal = new Vector2(r.ReadSingle(), r.ReadSingle());
        controller2D.collisions.verticalHitNormal   = new Vector2(r.ReadSingle(), r.ReadSingle());
    }
}
