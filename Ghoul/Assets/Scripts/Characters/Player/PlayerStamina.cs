using System.IO;
using Unity.Netcode;
using UnityEngine;

// Player stamina: drained by taking hits and by attacking, regenerates after a quiet period,
// and decides how the player reacts to a hit (none / knockback / knockback + stun) based on
// stamina at the moment the hit lands, before that hit's cost is applied.
//
// All state is simulated in the deterministic rollback tick (Tick() from
// PlayerController.RunFixedStep, OnAttack/OnHitTaken from inside the simulated attack) and
// snapshotted — RollbackSetup registers it automatically as an ISnapshotable on the player.
// The "random" stun duration comes from a seeded hash rather than UnityEngine.Random, so both
// peers roll the same value.
public class PlayerStamina : MonoBehaviour, ISnapshotable
{
    public enum HitReaction { None, Knockback, KnockbackAndStun }

    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [Tooltip("Stamina lost each time this player is hit.")]
    [SerializeField, Min(0f)] private float damageStaminaCost = 25f;
    [Tooltip("Stamina lost each time this player starts an attack.")]
    [SerializeField, Min(0f)] private float attackStaminaCost = 5f;

    [Header("Hit reactions (checked before the hit's cost is applied)")]
    [Tooltip("Below this fraction of max stamina, a hit knocks the player back.")]
    [SerializeField, Range(0f, 1f)] private float knockbackThreshold = 0.4f;
    [Tooltip("Below this fraction of max stamina, a hit knocks back AND stuns.")]
    [SerializeField, Range(0f, 1f)] private float stunThreshold = 0.1f;
    [Tooltip("Initial knockback speed (world units/sec) when a hit knocks this player back. Scaled by the attacking weapon's Knockback Power Multiplier.")]
    [SerializeField, Min(0f)] private float knockbackForce = 20f;
    [Tooltip("Stun duration range, in seconds. While stunned the player can't move or attack.")]
    [SerializeField, Min(0f)] private float stunDurationMin = 1f;
    [SerializeField, Min(0f)] private float stunDurationMax = 2.5f;

    [Header("Regeneration")]
    [Tooltip("Seconds without being hit or attacking before stamina starts regenerating.")]
    [SerializeField, Min(0f)] private float regenDelay = 2f;
    [Tooltip("Stamina regained per second once regeneration starts.")]
    [SerializeField, Min(0f)] private float regenRate = 15f;
    [Tooltip("Stamina regained per second while stunned (no regen delay). Stamina can't decrease while stunned, so the player recovers instead of being stun-locked.")]
    [SerializeField, Min(0f)] private float stunRegenRate = 60f;

    // ── Simulated state (snapshotted) ───────────────────────────────────────
    private float currentStamina;
    private float regenDelayTimer;
    private float stunTimer;
    private int stunRolls;   // advances the seeded stun-duration sequence

    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public float KnockbackForce => knockbackForce;
    public bool IsStunned => stunTimer > 0f;

    // (current, max) — raised whenever stamina changes, including on rollback restore.
    public event System.Action<float, float> StaminaChanged;

    private void Awake()
    {
        ResetToFull();
    }

    // Full stamina, no stun, regen ready (e.g. on spawn/respawn).
    public void ResetToFull()
    {
        regenDelayTimer = 0f;
        stunTimer = 0f;
        currentStamina = maxStamina;
        StaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    private void OnValidate()
    {
        if (stunThreshold > knockbackThreshold) { stunThreshold = knockbackThreshold; }
        if (stunDurationMax < stunDurationMin) { stunDurationMax = stunDurationMin; }
    }

    // One fixed simulation step. While stunned: fast regeneration with no delay. Otherwise:
    // count down the regen delay, then regenerate normally.
    public void Tick(float dt)
    {
        if (IsStunned)
        {
            SetStamina(currentStamina + stunRegenRate * dt);
            regenDelayTimer = 0f;   // the stun was the recovery; normal regen continues right after
            stunTimer = Mathf.Max(0f, stunTimer - dt);
            return;
        }

        if (regenDelayTimer > 0f)
        {
            regenDelayTimer = Mathf.Max(0f, regenDelayTimer - dt);
        }
        else if (currentStamina < maxStamina)
        {
            SetStamina(currentStamina + regenRate * dt);
        }
    }

    // Called when this player starts an attack swing.
    public void OnAttack()
    {
        if (IsStunned) { return; }   // can't attack while stunned anyway; never reduces stamina then
        SetStamina(currentStamina - attackStaminaCost);
        regenDelayTimer = regenDelay;
    }

    // Called when a hit lands on this player. Decides the reaction from stamina BEFORE this
    // hit's cost, then applies the cost, resets the regen delay, and starts any stun.
    // While already stunned the hit costs no stamina, doesn't reset the regen delay and
    // can't stun again (no stun-lock); it can still knock back based on current stamina.
    public HitReaction OnHitTaken()
    {
        float fraction = currentStamina / maxStamina;
        HitReaction reaction =
            fraction < stunThreshold      ? HitReaction.KnockbackAndStun :
            fraction < knockbackThreshold ? HitReaction.Knockback :
                                            HitReaction.None;

        if (IsStunned)
        {
            return reaction == HitReaction.KnockbackAndStun ? HitReaction.Knockback : reaction;
        }

        SetStamina(currentStamina - damageStaminaCost);
        regenDelayTimer = regenDelay;

        if (reaction == HitReaction.KnockbackAndStun)
        {
            stunTimer = Mathf.Lerp(stunDurationMin, stunDurationMax, NextStunRoll());
        }
        return reaction;
    }

    private void SetStamina(float value)
    {
        // Stamina can't be reduced while stunned (only recovers until the stun ends).
        if (IsStunned && value < currentStamina) { return; }
        float clamped = Mathf.Clamp(value, 0f, maxStamina);
        if (clamped == currentStamina) { return; }
        currentStamina = clamped;
        StaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    // Deterministic value in [0,1]: a hash of this player's identity (same on both peers) and
    // the number of stuns rolled so far (snapshotted), so rollback resimulation and the
    // other peer always produce the same duration.
    private float NextStunRoll()
    {
        uint seed = 0x9E3779B9u;
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned) { seed ^= (uint)(netObj.OwnerClientId + 1) * 0x85EBCA6Bu; }

        uint h = seed ^ ((uint)stunRolls * 0xC2B2AE35u);
        h ^= h >> 16; h *= 0x7FEB352Du;
        h ^= h >> 15; h *= 0x846CA68Bu;
        h ^= h >> 16;
        stunRolls++;
        return h / (float)uint.MaxValue;
    }

    // ── ISnapshotable ─────────────────────────────────────────────────────

    public void SaveState(BinaryWriter w)
    {
        w.Write(currentStamina);
        w.Write(regenDelayTimer);
        w.Write(stunTimer);
        w.Write(stunRolls);
    }

    public void LoadState(BinaryReader r)
    {
        currentStamina  = r.ReadSingle();
        regenDelayTimer = r.ReadSingle();
        stunTimer       = r.ReadSingle();
        stunRolls       = r.ReadInt32();
        StaminaChanged?.Invoke(currentStamina, maxStamina);
    }
}
