using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Cinemachine;

public class PlayerStats : CharacterStats
{
    [Header("PlayerStats Variables")]

    [SerializeField] protected int meatStoreCurrent = 0;
    [SerializeField] protected int meatStoreMax = 3;
    [SerializeField] protected int hungerPerMeatRecovered = 50;  // hunger points recovered per meat
    public int HungerPerMeatRecovered { get => hungerPerMeatRecovered; set => hungerPerMeatRecovered = value; }

    [SerializeField] protected int waterStoreCurrent = 0;
    [SerializeField] protected int waterStoreMax = 3;
    [SerializeField] protected int thirstPerWaterRecovered = 3;  // thirst points recovered per water
    public int ThirstPerWaterRecovered { get => thirstPerWaterRecovered; set => thirstPerWaterRecovered = value; }


    [SerializeField] protected float currentSpeed = 0;
    public float CurrentSpeed { get => currentSpeed; set => currentSpeed = value; }

    [SerializeField] protected float baseSpeed = 5;
    public float BaseSpeed { get => baseSpeed; set => baseSpeed = value; }

    [SerializeField] protected float attackRate = 0;
    public float AttackRate { get => attackRate; set => attackRate = value; }

    [SerializeField] protected float supplyCredit = 0;
    public float SupplyCredit { get => supplyCredit; set => supplyCredit = value; }



    //[SerializeField] private Debuff foodDebuffObject;
    //[SerializeField] private Debuff waterDebuffObject;


    //[SerializeField] protected DisplayStats displayStats;

    [SerializeField] private float maxOrthoSize = 4f;
    [SerializeField] private float minOrthoSize = 2f;
    [SerializeField] private float healthOrthoIncrement = 0.5f;

    private float lastOrthoSize;
    private float currentOrthoSize;
    //[SerializeField] private CinemachineVirtualCamera virtualCamera;
    //[SerializeField] private CinemachineShake camShake;

    [SerializeField] private float shakeMagnitude;
    [SerializeField] private float shakeFrequency;
    [SerializeField] private float shakeDuration;


    private float smoothVelocity;
    [SerializeField] private float orthoSmoothTime = 1f;

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
        AdjustOrthoCameraSize();

        //if (PlayerInventory.Instance == null) { return; }
        //supplyCredit = PlayerInventory.Instance.CurrentSupplyCredit;
    }

    protected override void Update()
    {
        base.Update();

    }

    //public override void ProcessConsumableItem(Item itemToUse)
    //{
    //    base.ProcessConsumableItem(itemToUse);

    //    //PlayerInventory.Instance.PlayerResources.CacheItem(itemToUse);

    //    UpdateDisplayStats();
    //    AdjustOrthoCameraSize();
    //}

    public void MovementFatigue(bool isRunning, bool isWallsliding)
    {
        // Enable run/dodge fatigue
        if (isRunning)
        {
            movementFatigueRun = true;
        }
        else
        {
            movementFatigueRun = false;
        }

        // Enable attack/jump/wallsliding fatigue
        if (isWallsliding)
        {
            movementFatigueAttack = true;
        }
        else
        {
            movementFatigueAttack = false;
        }

        //if (movementFatigueAttack || movementFatigueRun) { movementFatigue = true; }
        //else { movementFatigue = false; }

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

        currentDamage = baseDamage + weaponDamage - damageDebuffAmount;
        currentKnockbackPower = baseKnockbackPower + weaponKnockback;
        UpdateDisplayStats();
    }

    public void UpdateCurrentSpeed()
    {
        currentSpeed = baseSpeed - speedDebuffAmount;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    public void ResetFoodWater()
    {
        currentHunger = maxHunger;
        currentThirst = maxThirst;
    }

    public void CollectInformation(float creditReceived)
    {
        //Debug.Log("Supply credit received");
        supplyCredit += creditReceived;

        UpdateDisplayStats();
        AdjustOrthoCameraSize();
    }

    protected override void IncrementHunger()
    {
        base.IncrementHunger();

        UpdateDisplayStats();
        AdjustOrthoCameraSize();
    }

    protected override void IncrementThirst()
    {
        base.IncrementThirst();

        UpdateDisplayStats();
        AdjustOrthoCameraSize();
    }

    public override void InflictDamage(float damageTaken)
    {
        base.InflictDamage(damageTaken);

        //if (camShake != null) { camShake.Shake(shakeMagnitude, shakeFrequency, shakeDuration); }

        UpdateDisplayStats();
        AdjustOrthoCameraSize();
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

    private void AdjustOrthoCameraSize()
    {
        lastOrthoSize = Camera.main.orthographicSize;

        float newOrthoSize = 0;

        CalculateFoodDebuff();

        CalculateWaterDebuff();

        UpdateDisplayStats();

        float healthImpact = DetermineHealthImpactAndBlood();

        newOrthoSize = maxOrthoSize - healthImpact;  //  - foodDamageImpact - waterSpeedImpact
        if (newOrthoSize < minOrthoSize) { newOrthoSize = minOrthoSize; }

        //if (newOrthoSize != lastOrthoSize) { Debug.Log("Increment: " + (newOrthoSize) + ": healthImpact: " + healthImpact); }


        currentOrthoSize = Mathf.SmoothDamp(lastOrthoSize, newOrthoSize, ref smoothVelocity, orthoSmoothTime);

        //Debug.Log("ortho size: " + virtualCamera.m_Lens.OrthographicSize);
        //virtualCamera.m_Lens.OrthographicSize = currentOrthoSize;
    }

    private float DetermineHealthImpactAndBlood()
    {
        float currentHealthPercent = (float)currentHealth / (float)maxHealth;
        float healthImpact = 0f;
        //Debug.Log("currentHealthPercent: " + currentHealthPercent);

        if (currentHealthPercent >= 1.00f) { healthImpact = 0; }
        if (currentHealthPercent >= 0.75f && currentHealthPercent < 1.00f) { healthImpact = healthOrthoIncrement * 0.5f; }
        if (currentHealthPercent >= 0.50f && currentHealthPercent < 0.75f) { healthImpact = healthOrthoIncrement * 1; }
        if (currentHealthPercent >= 0.25f && currentHealthPercent < 0.50f) { healthImpact = healthOrthoIncrement * 2; }
        if (currentHealthPercent >= 0.10f && currentHealthPercent < 0.25f) { healthImpact = healthOrthoIncrement * 3; }
        if (currentHealthPercent <= 0.10f) { healthImpact = healthOrthoIncrement * 4; }

        //if (currentHealthPercent < 0.75f) { bloodSpawner1.SetShowBloodStatus(true); } else { bloodSpawner1.SetShowBloodStatus(false); }
        //if (currentHealthPercent < 0.50f) { bloodSpawner2.SetShowBloodStatus(true); } else { bloodSpawner2.SetShowBloodStatus(false); }
        //if (currentHealthPercent < 0.25f) { bloodSpawner3.SetShowBloodStatus(true); } else { bloodSpawner3.SetShowBloodStatus(false); }
        //if (currentHealthPercent <= 0.10f)
        //{
        //    bloodSpawner1.SetHalfSpillRate(true);
        //    bloodSpawner2.SetHalfSpillRate(true);
        //    bloodSpawner3.SetHalfSpillRate(true);
        //}
        //else
        //{
        //    bloodSpawner1.SetHalfSpillRate(false);
        //    bloodSpawner2.SetHalfSpillRate(false);
        //    bloodSpawner3.SetHalfSpillRate(false);
        //}

        return healthImpact;
    }

    private void CalculateWaterDebuff()
    {
        float currentWaterPercent = (float)currentThirst / (float)maxThirst;
        int waterSpeedImpact = 0;
        int setDebuffStage = 0;

        if (currentWaterPercent >= 0.75f) { waterSpeedImpact = 0; }
        if (currentWaterPercent >= 0.50f && currentWaterPercent < 0.75f) { waterSpeedImpact = 0; }
        if (currentWaterPercent >= 0.25f && currentWaterPercent < 0.50f) { waterSpeedImpact = 0; }
        if (currentWaterPercent >= 0.1f && currentWaterPercent < 0.25f) { waterSpeedImpact = 1; setDebuffStage = 2; }
        if (currentWaterPercent <= 0.1f) { waterSpeedImpact = 2; setDebuffStage = 3; }

        // Need to move to its own method that takes items/inventory into account
        float newSpeedDebuff = waterSpeedImpact * waterDebuffIncrement;
        if (newSpeedDebuff != speedDebuffAmount)
        {
            speedDebuffAmount = newSpeedDebuff;
            UpdateCurrentSpeed();
        }


        //Debug.Log("water1: " + waterSpeedImpact);
        //if (waterDebuffObject != null) { waterDebuffObject.SetDebuffStage(setDebuffStage); }
    }

    private void CalculateFoodDebuff()
    {
        float currentFoodPercent = (float)currentHunger / (float)maxHunger;
        int foodDamageImpact = 0;
        int setDebuffStage = 0;
        //Debug.Log("currentFoodPercent: " + currentFoodPercent);

        if (currentFoodPercent >= 0.75f) { foodDamageImpact = 0; }
        if (currentFoodPercent >= 0.50f && currentFoodPercent < 0.75f) { foodDamageImpact = 0; }
        if (currentFoodPercent >= 0.25f && currentFoodPercent < 0.50f) { foodDamageImpact = 0; }
        if (currentFoodPercent >= 0.1f && currentFoodPercent < 0.25f) { foodDamageImpact = 1; setDebuffStage = 2; }
        if (currentFoodPercent <= 0.1f) { foodDamageImpact = 2; setDebuffStage = 3; }

        // Need to move to its own method that takes items/inventory into account
        float newDamageDebuff = foodDamageImpact * foodDebuffIncrement;
        if (newDamageDebuff != damageDebuffAmount)
        {
            damageDebuffAmount = newDamageDebuff;
            UpdateCurrentDamageAndKnockback();
        }


        //Debug.Log("food1: " + foodDamageImpact);
        //if (foodDebuffObject != null) { foodDebuffObject.SetDebuffStage(setDebuffStage); }
    }
}
