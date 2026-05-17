using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Cinemachine;

public class PlayerStats : CharacterStats
{
    //[Header("PlayerStats Variables")]

    //[SerializeField] protected DisplayStats displayStats;

    //[SerializeField] private CinemachineVirtualCamera virtualCamera;
    //[SerializeField] private CinemachineShake camShake;

    //[SerializeField] private float shakeMagnitude;
    //[SerializeField] private float shakeFrequency;
    //[SerializeField] private float shakeDuration;

    protected override void Start()
    {
        base.Start();

        characterController = GetComponent<PlayerController>();
        //if (GameManager.Instance != null) { displayStats = GameManager.Instance.DisplayStats; }
        //GameObject virtualCameraObject = GameObject.FindGameObjectWithTag("Cinemachine Camera");
        //if (virtualCameraObject != null) 
        //{
        //    virtualCamera = virtualCameraObject.GetComponent<CinemachineVirtualCamera>();
        //    camShake = virtualCameraObject.GetComponent<CinemachineShake>(); 
        //}

        UpdateDisplayStats();
    }

    protected override void Update()
    {
        base.Update();

    }

    public void UpdateCurrentDamageAndKnockback()
    {
        float weaponDamage = 0;
        Vector2 weaponKnockback = Vector2.zero;

        //Item item = PlayerInventory.Instance.GetCurrentHighlightedItem();

        //if (item != null) 
        //{
        //    weaponHitSound = item.weaponHitSound;
        //    weaponSwingSound = item.weaponSwingSound;
        //    weaponDamage = item.itemDamage;
        //    weaponKnockback = item.knockbackPower;

        //    //Debug.Log("itemName: " + item.itemName + "weaponHitSound: " + weaponHitSound + "; item:sound: " + item.weaponHitSound);
        //}

        //weaponDamage = PlayerInventory.Instance.CheckItemDamage();

        currentDamage = baseDamage + weaponDamage;
        currentKnockbackPower = baseKnockbackPower + weaponKnockback;
        UpdateDisplayStats();
    }

    public void UpdateCurrentSpeed()
    {
        currentSpeed = baseSpeed;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    protected override void OnHealthChanged(float prev, float next)
    {
        UpdateDisplayStats();
    }

    public override void InflictDamage(float damageTaken)
    {
        base.InflictDamage(damageTaken);

        //if (camShake != null) { camShake.Shake(shakeMagnitude, shakeFrequency, shakeDuration); }

        if (!IsSpawned) { UpdateDisplayStats(); }
    }

    private void UpdateDisplayStats()
    {
        //if (displayStats)
        //{
        //    //Debug.Log("update stats");
        //    displayStats.UpdateHealthBar(currentHealth, maxHealth);
        //    displayStats.UpdateHungerBar(currentHunger, maxHunger);
        //    displayStats.UpdateThirstBar(currentThirst, maxThirst);
        //    displayStats.UpdateSupplyCredit(supplyCredit);
        //    displayStats.UpdateCurrentDamage(currentDamage);
        //    displayStats.UpdateCurrentSpeed(currentSpeed);
        //    displayStats.UpdateKnockback(currentKnockbackPower);
        //}
    }

}
