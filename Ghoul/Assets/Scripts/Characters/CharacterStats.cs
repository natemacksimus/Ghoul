using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.U2D;

[SelectionBase]
public abstract class CharacterStats : MonoBehaviour, IDamageable
{
    #region BASE CHARACTER DEFINITIONS
    [Header("Character Stats")]
    [SerializeField] protected float maxHealth = 100;
    //[SerializeField] protected int baseHealth = 100;
    [SerializeField] protected float currentHealth = 0;

    [SerializeField] protected float baseDamage = 1;  // offense stat
    [SerializeField] protected float currentDamage = 0;

    [SerializeField] protected Vector2 baseKnockbackPower = Vector2.zero;
    [SerializeField] protected Vector2 currentKnockbackPower = Vector2.zero;
    [SerializeField] protected Vector2 knockbackResistance = Vector2.zero;
    [SerializeField] protected float knockbackForceTime = 0.25f;


    [Header("Hunger/Thirst Variables")]
    //[SerializeField] protected int baseHunger = 0;
    [SerializeField] protected int currentHunger = 150;
    [SerializeField] protected int maxHunger = 150;
    [SerializeField] protected int hungerDamage = 1;
    [SerializeField] protected int incrementHungerRun = 1;  // use this rate when running, dodging
    [SerializeField] protected int incrementHungerAttack = 3; // use this rate when attacking, wallsliding, jumping
    [SerializeField] protected int healthPerFood = 10;
    public int HealthPerFood { get => healthPerFood; set => healthPerFood = value; }


    //[SerializeField] protected int baseThirst = 0;
    [SerializeField] protected int currentThirst = 150;
    [SerializeField] protected int maxThirst = 150;
    [SerializeField] protected int thirstDamage = 1;
    [SerializeField] protected int incrementThirstRun = 1;  // use this rate when running, dodging
    [SerializeField] protected int incrementThirstAttack = 3; // use this rate when attacking, wallsliding, jumping
    [SerializeField] protected int healthPerWater = 10;
    public int HealthPerWater { get => healthPerWater; set => healthPerWater = value; }

    //protected bool movementFatigue = false;
    [ShowOnly] [SerializeField] protected bool movementFatigueRun = false;  // use this rate when running, dodging
    [ShowOnly] [SerializeField] protected bool movementFatigueAttack = false;  // use this rate when attacking, wallsliding, jumping


    [SerializeField] protected float foodDebuffIncrement = 1f;
    [SerializeField] protected float waterDebuffIncrement = 0.5f;
    [SerializeField] protected float damageDebuffAmount;
    [SerializeField] protected float speedDebuffAmount;


    //public int MaxHealth { get => maxHealth; set => maxHealth = value; }
    //public int CurrentHealth { get => currentHealth; set => currentHealth = value; }
    //public int BaseDamage { get => baseDamage; set => baseDamage = value; }
    //public int CurrentDamage { get => currentDamage; set => currentDamage = value; }
    //public int BaseHunger { get => baseHunger; set => baseHunger = value; }
    //public int CurrentHunger { get => currentHunger; set => currentHunger = value; }
    //public int BaseThirst { get => baseThirst; set => baseThirst = value; }
    //public int CurrentThirst { get => currentThirst; set => currentThirst = value; }

    #endregion

    //[SerializeField] protected bool regenFoodEnabled = false;
    //[SerializeField] protected bool regenWaterEnabled = false;
    //[SerializeField] protected bool isResting = false;
    //[SerializeField] protected int restingMultiple = 2;
    //[SerializeField] protected int currentRegenPoints = 0;
    //[SerializeField] protected int regenPointsPerHealth = 4;
    //[SerializeField] protected int regenHealthAmt = 1;
    //[SerializeField] protected float stillTimer;
    //[SerializeField] protected float stillThreshold = 3f;

    [SerializeField] protected GameObject foodRegenParticles;
    [SerializeField] protected GameObject waterRegenParticles;

    [SerializeField] protected bool pauseHunger;
    [SerializeField] protected bool pauseThirst;
    [SerializeField] protected bool canStarve = false;

    [SerializeField] protected float timerHungerSet = 1f;  //Total time per increment
    [SerializeField] protected float timerThirstSet = 1f;  //Total time per increment; 1f is 1 second; aim for 
    [SerializeField] protected float timerStarveSet = 3f;

    [ShowOnly] [SerializeField] protected float timerHunger;
    [ShowOnly] [SerializeField] protected float timerThirst;
    [ShowOnly] [SerializeField] protected float timerStarve;

    [SerializeField] protected AudioClip weaponSwingSound;
    public AudioClip WeaponSwingSound { get => weaponSwingSound; set => weaponSwingSound = value; }

    [SerializeField] protected AudioClip weaponHitSound;
    public AudioClip WeaponHitSound { get => weaponHitSound; set => weaponHitSound = value; }

    protected Animator animator;
    protected IKillable killable;
    protected GameCharacterController characterController;
    protected AudioSource audioSource;

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();
        killable = GetComponent<IKillable>();
        characterController = GetComponent<GameCharacterController>();
        audioSource = GetComponent<AudioSource>();

        timerStarve = timerStarveSet;
    }

    protected virtual void Update()
    {
        IncrementHunger();

        IncrementThirst();

        Starve();

        //CheckIfResting();
    }

    private void Starve()
    {
        if (!canStarve) { return; }
        if (currentHunger > 0 || currentThirst > 0) { return; }

        if (timerStarve > 0)
        {
            timerStarve -= Time.deltaTime;
        }
        else
        {
            InflictDamage((hungerDamage + thirstDamage));
            timerStarve = timerStarveSet;
        }
    }

    //private void CheckIfResting()
    //{
    //    if (characterController.IsResting)
    //    {
    //        isResting = true;
    //    }
    //    else
    //    {
    //        isResting = false;
    //    }
        
    //    //if (!movementFatigue)
    //    //{
    //    //    stillTimer += Time.deltaTime;

    //    //    if (stillTimer > stillThreshold && isResting == false) { isResting = true; }
    //    //}
    //    //else
    //    //{
    //    //    if (stillTimer != 0) { stillTimer = 0; }
    //    //    if (isResting == true) { isResting = false; }
    //    //}

    //    if (currentHealth == maxHealth || !isResting || currentHunger <= 0)
    //    {
    //        if (foodRegenParticles == null) { return; }
    //        if (foodRegenParticles.activeSelf) { foodRegenParticles.SetActive(false); }
    //    }

    //    if (currentHealth == maxHealth || !isResting || currentThirst <= 0)
    //    {
    //        if (waterRegenParticles == null) { return; }
    //        if (waterRegenParticles.activeSelf) { waterRegenParticles.SetActive(false); }
    //    }
    //}

    //public virtual void ProcessConsumableItem(Item itemToUse)
    //{
    //    ChangeHealth(itemToUse.healthPoints);
    //    ChangeHunger(itemToUse.hungerPoints);
    //    ChangeThirst(itemToUse.thirstPoints);
    //}

    private void HungerAffect()
    {
        //if (currentFood == 0) { InflictDamage(hungerDamage); }
    }

    private void ThirstAffect()
    {
        //if (currentWater == 0) { InflictDamage(thirstDamage); }
    }

    //protected virtual void ToggleRegenHealth()
    //{

    //    if (currentHunger > 0 && currentHealth != maxHealth) { regenFoodEnabled = true; }
    //    else { regenFoodEnabled = false; }

    //    if (currentThirst > 0 && currentHealth != maxHealth) { regenWaterEnabled = true; }
    //    else { regenWaterEnabled = false; }
    //}

    //protected virtual void RegenHealth(int regenPoints)
    //{
    //    currentRegenPoints += regenPoints;

    //    if (currentRegenPoints >= regenPointsPerHealth) 
    //    {
    //        ChangeHealth(regenHealthAmt);
    //        currentRegenPoints = currentRegenPoints - regenPointsPerHealth;
    //    }
    //}

    protected virtual void IncrementHunger()
    {
        if (!pauseHunger)
        {
            if (timerHunger > 0)
            {
                timerHunger -= Time.deltaTime;
            }
            else
            {
                //ToggleRegenHealth();

                // if moving, pay food cost to move
                if (movementFatigueAttack)   // && !isResting
                {
                    // reduce the food usage per increment by how many food debuffs are applied
                    int debuffEffect = Mathf.RoundToInt(damageDebuffAmount / foodDebuffIncrement);  // 1.0 for each debuff active (equal to 1 at first debuff and 3 
                    debuffEffect = (debuffEffect > incrementHungerAttack) ? incrementHungerAttack : debuffEffect;

                    //Debug.Log("debuffEffect food: " + (Mathf.RoundToInt(damageDebuffAmount / foodDebuffIncrement)));

                    //Debug.Log("incrementHunger attack: " + (incrementHungerAttack));
                    currentHunger -= (incrementHungerAttack - debuffEffect);
                }

                //// if resting, with less than max health and food available:
                //if (regenFoodEnabled && isResting) 
                //{
                //    // turn on PS effect for food regen
                //    if (foodRegenParticles != null) 
                //    {
                //        if (!foodRegenParticles.activeSelf) { foodRegenParticles.SetActive(true); }
                //    }

                //    // pay food to regen health
                //    currentHunger -= incrementHungerRun;
                //    RegenHealth(incrementHungerRun);
                //}

                // clamp current food
                currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

                timerHunger = timerHungerSet;

                //if (currentHunger > maxHunger) { currentHunger = maxHunger; }
                //if (currentHunger < 0) { currentHunger = 0; }

                // reset timer to process another increment
                //if (isResting) { timerHunger = timerHungerSet / restingMultiple; }
                //else { timerHunger = timerHungerSet; }

                //if (gameObject.tag == "Player") { Debug.Log("timerhunger: " + timerHunger); }

                // affect Hunger
                //HungerAffect();  
            }
        }
    }

    protected virtual void IncrementThirst()
    {
        if (!pauseThirst)
        {
            if (timerThirst > 0)
            {
                timerThirst -= Time.deltaTime;
            }
            else
            {
                //ToggleRegenHealth();

                if (movementFatigueRun)  // && !isResting
                {
                    // reduce the water usage per increment by how many water debuffs are applied
                    int debuffEffect = Mathf.RoundToInt(speedDebuffAmount / waterDebuffIncrement);
                    debuffEffect = (debuffEffect > incrementThirstRun) ? incrementThirstRun : debuffEffect;

                    //Debug.Log("debuffEffect water: " + (Mathf.RoundToInt(speedDebuffAmount / waterDebuffIncrement)));

                    //Debug.Log("incrementThirstRun: " + (incrementThirstRun));

                    currentThirst -= (incrementThirstRun - debuffEffect);             
                }

                //if (regenWaterEnabled && isResting) 
                //{
                //    if (waterRegenParticles != null)
                //    { 
                //        if (!waterRegenParticles.activeSelf) { waterRegenParticles.SetActive(true); } 
                //    }

                //    //Debug.Log("increment: " + increment);
                //    currentThirst -= incrementThirstRun;
                //    RegenHealth(incrementThirstRun);
                //}


                currentThirst = Mathf.Clamp(currentThirst, 0, maxThirst);

                timerThirst = timerThirstSet;


                //if (currentThirst > maxThirst) { currentThirst = maxThirst; }
                //if (currentThirst < 0) { currentThirst = 0; }

                // reset timer to process another increment
                //if (isResting) { timerThirst = timerThirstSet / restingMultiple; }
                //else { timerThirst = timerThirstSet; }

                //if (gameObject.tag == "Player") { Debug.Log("timerThirst: " + timerThirst); }

                //ThirstAffect();
            }
        }
    }

    
    public void ChangeHealth(int amount)
    {
        //Debug.Log("Health 1 " + currentHealth);
        currentHealth += amount;
        if (currentHealth < 0)
        {
            currentHealth = 0;

            if (animator != null) { animator.SetTrigger("die"); }

            if (killable != null) { killable.Kill(); }

            //Debug.Log("Player Died"); 
        }
        if (currentHealth > maxHealth) { currentHealth = maxHealth; }

        //Debug.Log("Health 2 " + currentHealth);

        //return currentHealth;
    }

    public void ChangeHunger(int amount)
    {
        //Debug.Log("Changed Hunger");
        if (pauseHunger && amount < 0) { return; }

        currentHunger += amount;

        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);
    }

    public void ChangeThirst(int amount)
    {
        //Debug.Log("Changed Thirst");
        if (pauseThirst && amount < 0) { return; }

        currentThirst += amount;

        currentThirst = Mathf.Clamp(currentThirst, 0, maxThirst);
    }

    public virtual float GetCurrentDamage()
    {
        return currentDamage;
    }

    public virtual float GetCurrentHealth()
    {
        return currentHealth;
    }

    public virtual Vector2 GetKnockbackPower()
    {
        return currentKnockbackPower;
    }

    public virtual float GetKnockbackTime()
    {
        return knockbackForceTime;
    }

    public virtual void PauseHunger(bool isPaused)
    {
        pauseHunger = isPaused;
    }

    public virtual void PauseThirst(bool isPaused)
    {
        pauseThirst = isPaused;
    }

    //public void ChangeDamage(int amount)
    //{
    //    Debug.Log("Changed Damage");
    //}

    //public void ChangeSpeed(int amount)
    //{
    //    Debug.Log("Changed Speed");
    //}

    public virtual void InflictDamage(float damageTaken)
    {
        if (characterController.Invincible)
        {
            Debug.Log("is invincible; negate damage");
            return;
        }

        //Debug.Log("inflict damage in character stats");
        currentHealth -= damageTaken;
        if (currentHealth <= 0)
        {
            currentHealth = 0;

            //Debug.Log("animator is null: " + animator == null);
            if (animator != null) { animator.SetTrigger("die"); }

            if (killable != null) { killable.Kill(); }

            //PlayBreakSound();
        }
        else
        {
            if (animator != null) { animator.SetTrigger("damaged"); }
            //PlayHitSound();
        }
    }

    public virtual void Knockback(Vector2 knockbackReceived, float knockbackTimeReceived, int attackDir)
    {
        //Debug.Log("knockback CharacterStats: " + gameObject.name);

        float knockbackForceX = knockbackReceived.x - knockbackResistance.x > 0 ? knockbackReceived.x - knockbackResistance.x : 0;
        float knockbackForceY = knockbackReceived.y - knockbackResistance.y > 0 ? knockbackReceived.y - knockbackResistance.y : 0;

        Vector2 knockbackForce = new Vector2(knockbackForceX * attackDir, knockbackForceY);

        characterController.ApplyKnockback(knockbackForce, knockbackTimeReceived);
    }

    // Triggered in animation clips
    public void PlayWeaponSwing()
    {
        if (audioSource == null || weaponSwingSound == null) { return; }
        audioSource.PlayOneShot(weaponSwingSound);
    }
}
