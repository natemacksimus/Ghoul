using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.U2D;
using Unity.Netcode;

[SelectionBase]
public abstract class CharacterStats : NetworkBehaviour, IDamageable
{
    #region BASE CHARACTER DEFINITIONS
    [Header("Character Stats")]
    [SerializeField] protected float maxHealth = 100;
    [SerializeField] protected float currentHealth = 0;

    [SerializeField] protected float currentSpeed = 5;
    public float CurrentSpeed { get => currentSpeed; set => currentSpeed = value; }

    [SerializeField] protected float baseSpeed = 5;
    public float BaseSpeed { get => baseSpeed; set => baseSpeed = value; }

    [SerializeField] protected float attackRate = 0;
    public float AttackRate { get => attackRate; set => attackRate = value; }

    [SerializeField] protected float baseDamage = 1;
    [SerializeField] protected float currentDamage = 0;

    [SerializeField] protected Vector2 baseKnockbackPower = Vector2.zero;
    [SerializeField] protected Vector2 currentKnockbackPower = Vector2.zero;
    [SerializeField] protected Vector2 knockbackResistance = Vector2.zero;
    [SerializeField] protected float knockbackForceTime = 0.25f;
    #endregion

    [SerializeField] protected AudioClip weaponSwingSound;
    public AudioClip WeaponSwingSound { get => weaponSwingSound; set => weaponSwingSound = value; }

    [SerializeField] protected AudioClip weaponHitSound;
    public AudioClip WeaponHitSound { get => weaponHitSound; set => weaponHitSound = value; }

    // Server-authoritative health visible to all clients.
    private NetworkVariable<float> netHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    protected Animator animator;
    protected IKillable killable;
    protected EntityController characterController;
    protected AudioSource audioSource;

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();
        killable = GetComponent<IKillable>();
        characterController = GetComponent<EntityController>();
        audioSource = GetComponent<AudioSource>();
    }

    protected virtual void Update() { }

    public override void OnNetworkSpawn()
    {
        if (IsServer) { netHealth.Value = maxHealth; }
        currentHealth = netHealth.Value;
        netHealth.OnValueChanged += OnNetHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        netHealth.OnValueChanged -= OnNetHealthChanged;
    }

    private void OnNetHealthChanged(float prev, float next)
    {
        currentHealth = next;
        if (next <= 0f && prev > 0f)
        {
            if (animator != null) { animator.SetTrigger("die"); }
            if (killable != null) { killable.Kill(); }
        }
        else if (next < prev)
        {
            if (animator != null) { animator.SetTrigger("damaged"); }
        }
        OnHealthChanged(prev, next);
    }

    // Subclasses override this to react to health changes in a networked game (e.g. update HUD).
    protected virtual void OnHealthChanged(float prev, float next) { }

    public void ChangeHealth(int amount)
    {
        if (!IsSpawned)
        {
            currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
            if (currentHealth <= 0)
            {
                if (animator != null) { animator.SetTrigger("die"); }
                if (killable != null) { killable.Kill(); }
            }
            return;
        }
        if (IsServer) { netHealth.Value = Mathf.Clamp(netHealth.Value + amount, 0, maxHealth); }
        else { ChangeHealthServerRpc(amount); }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeHealthServerRpc(int amount)
    {
        netHealth.Value = Mathf.Clamp(netHealth.Value + amount, 0, maxHealth);
    }

    public virtual float GetCurrentDamage() => currentDamage;
    public virtual float GetCurrentHealth() => currentHealth;
    public virtual Vector2 GetKnockbackPower() => currentKnockbackPower;
    public virtual float GetKnockbackTime() => knockbackForceTime;

    public virtual void InflictDamage(float damageTaken)
    {
        if (!IsSpawned)
        {
            LocalInflictDamage(damageTaken);
            return;
        }
        if (IsServer) { ServerApplyDamage(damageTaken); }
        else { InflictDamageServerRpc(damageTaken); }
    }

    [ServerRpc(RequireOwnership = false)]
    private void InflictDamageServerRpc(float damageTaken)
    {
        ServerApplyDamage(damageTaken);
    }

    private void ServerApplyDamage(float damageTaken)
    {
        netHealth.Value = Mathf.Max(0f, netHealth.Value - damageTaken);
    }

    private void LocalInflictDamage(float damageTaken)
    {
        if (characterController != null && characterController.Invincible) { return; }
        currentHealth -= damageTaken;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (animator != null) { animator.SetTrigger("die"); }
            if (killable != null) { killable.Kill(); }
        }
        else
        {
            if (animator != null) { animator.SetTrigger("damaged"); }
        }
    }

    public virtual void Knockback(Vector2 knockbackReceived, float knockbackTimeReceived, int attackDir)
    {
        float kx = knockbackReceived.x - knockbackResistance.x > 0 ? knockbackReceived.x - knockbackResistance.x : 0;
        float ky = knockbackReceived.y - knockbackResistance.y > 0 ? knockbackReceived.y - knockbackResistance.y : 0;
        Vector2 force = new Vector2(kx * attackDir, ky);

        if (!IsSpawned)
        {
            if (characterController != null) { characterController.ApplyKnockback(force, knockbackTimeReceived); }
            return;
        }
        if (IsServer) { SendKnockbackToOwner(force, knockbackTimeReceived); }
        else { KnockbackServerRpc(force, knockbackTimeReceived); }
    }

    [ServerRpc(RequireOwnership = false)]
    private void KnockbackServerRpc(Vector2 force, float knockbackTime)
    {
        SendKnockbackToOwner(force, knockbackTime);
    }

    private void SendKnockbackToOwner(Vector2 force, float knockbackTime)
    {
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
        };
        ApplyKnockbackClientRpc(force, knockbackTime, rpcParams);
    }

    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector2 force, float knockbackTime, ClientRpcParams rpcParams = default)
    {
        if (characterController != null) { characterController.ApplyKnockback(force, knockbackTime); }
    }

    // Triggered in animation clips
    public void PlayWeaponSwing()
    {
        if (audioSource == null || weaponSwingSound == null) { return; }
        audioSource.PlayOneShot(weaponSwingSound);
    }
}
