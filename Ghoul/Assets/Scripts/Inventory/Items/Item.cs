using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum InventoryType { WEAPON, ITEM, RESOURCE, PASSIVE, INGREDIENT, BLANK };

// Which hand's inventory a world pickup is routed to when collected. Either resolves to
// the Right hand (see PlayerInventory.TryPickup).
public enum HandSlot { Right, Left, Either };

public class Item : MonoBehaviour, IDamageable
{
    //[SerializeField] private ItemDefinition itemDefinition;

    [HideInInspector] public PlayerController playerController;
    //[HideInInspector] public CreatureController creatureController;
    protected ItemGravity itemGravity;
    //protected PlayerInventory inventory;

    #region ITEM DEFINITION - COPIED FROM ITEM_SO
    public InventoryType inventoryType = InventoryType.WEAPON;
    public string itemName;
    public Sprite itemIcon = null;
    public GameObject itemGameObject = null;
    public bool isPermanentItem = false; // if true, item cannot be dropped
    public HandSlot handSlot = HandSlot.Either;  // which hand inventory this item is picked up into

    [Range(1, 999)] public int itemId = 0;
    [Range(0, 999)] public int itemQty = 0;
    [Range(1, 999)] public int itemMaxStackSize = 1;  // one implies it is not stackable; should be non-zero and positive

    //[Range(0, 9999)] public int pricePerUnit = 0;  // selling price per quantity of item
    //[Range(0, 999)] public float itemCooldown = 0;  // zero implies no cooldown
    //[Range(0, 999)] public float itemLife = 0;  // zero implies it lasts forever

    [Header("Item Stats")]
    [Range(-999, 999)] public int itemDamage = 0;  // value when item is equipped
    [Range(-999, 999)] public int itemAttackRate = 0;
    public Vector2 knockbackPower = Vector2.zero;
    [Range(-999, 999)] public int healthPoints = 0;  // amt of health points to add/subtract from player health when consumed
    [Range(-999, 999)] public int hungerPoints = 0;  // amt of health points to add/subtract from player health when consumed
    [Range(-999, 999)] public int thirstPoints = 0;  // amt of health points to add/subtract from player health when consumed
    [Range(0, 999)] public float supplyCredit = 0;  // value of the items when sold
    [Range(0, 999)] public float price = 0;  // value of the items when purchased

    [Header("Resources Provided")]
    [Range(0, 999)] public float food = 0;  // value of the items when purchased
    [Range(0, 999)] public float water = 0;  // value of the items when purchased
    [Range(0, 999)] public float wood = 0;  // value of the items when purchased
    [Range(0, 999)] public float ore = 0;  // value of the items when purchased
    [Range(0, 999)] public float leather = 0;  // value of the items when purchased

    [Header("Upgrade Stats")]
    [Range(-999, 999)] public int meleeDmgImpact = 0;  // value when item is equipped
    [Range(-999, 999)] public int rangeDmgImpact = 0;  // value when item is equipped
    [Range(-999, 999)] public int attackRateImpact = 0;  // impact caused by upgrade
    [Range(-999, 999)] public int speedImpact = 0;  // impact caused by upgrade
    [Range(-999, 999)] public int jumpImpact = 0;  // impact caused by upgrade

    [Header("Abilities Enabled")]
    //public bool climbingOn = false;
    //public bool regenerationOn = false;

    public bool isInventory = true;  // is cached on the spot if not Inventory
    public bool isConsumable = false;  // has quantity that can be consumed
    public bool destroyWhenConsumed = true;  // when qty reaches zero, remove the item from inventory
    public bool autoCacheItem = false;  // toggle on/off autoCollect to cache resources

    [SerializeField] protected GameObject craftableItemPrefab;
    [SerializeField] protected float woodRequiredForCraft;
    [SerializeField] protected float oreRequiredForCraft;
    [SerializeField] protected float leatherRequiredForCraft;
    [SerializeField] protected float timeNeededToCraft;

    #endregion

    protected bool isPickedUp = false;
    protected float timeToDeactivate = 1f;
    [SerializeField] protected float timeToEnablePickup = 1f;

    [SerializeField] private AudioClip pickupSound;
    public AudioClip weaponSwingSound;
    public AudioClip weaponHitSound;

    public AnimationClip useItemClip;
    public AnimationClip altUseItemClip;
    protected Animator animator;
    protected AudioSource audioSource;
    protected SpriteRenderer sprite;

    [SerializeField] protected bool creatureCanPickup = false;

    [SerializeField] protected bool interactable = false;
    public bool Interactable { get => interactable; set => interactable = value; }

    [SerializeField] protected bool invincible = false;
    [SerializeField] protected float invincibleTimer;
    [SerializeField] protected float invincibleTimerSet;

    private Collider2D itemCollider;

    protected void Awake()
    {
        //if (itemDefinition != null) { CopyDataFromScriptObj(); }
    }
    protected void Start()
    {
        interactable = false;
        audioSource = GetComponent<AudioSource>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        itemGravity = GetComponent<ItemGravity>();
        itemCollider = GetComponent<Collider2D>();
        //inventory = PlayerInventory.Instance;

        if (sprite != null) { sprite.enabled = true; }
        StartCoroutine(EnablePickup());
    }


    protected void OnTriggerStay2D(Collider2D other)
    {
        SetPickupStatus(other);
    }
    protected void OnTriggerEnter2D(Collider2D other)
    {
        SetPickupStatus(other);
    }

    protected void SetPickupStatus(Collider2D other)
    {
        if (!interactable) { return; }

        if (other.tag == "Player" && !isPickedUp)
        {
            playerController = other.GetComponent<PlayerController>();
            if (playerController == null) { return; }
            if (playerController.ItemToPickup == null)
            {
                playerController.ItemToPickup = this;
                if (animator != null) { animator.SetBool("nearPlayer", true); }

                // AutoCache items that enable autoCache, are not inventory (can't be put into inventory), and are resources.
                if (autoCacheItem && !isInventory && inventoryType == InventoryType.RESOURCE) { playerController.ItemToPickup.CacheItem(); }
            }
        }

        //if (other.tag == "Enemy" && !isPickedUp)
        //{
        //    creatureController = other.GetComponent<CreatureController>();
        //    if (creatureController == null) { return; }
        //    //animator.SetBool("nearPlayer", true);

        //    if (creatureController.IsHarvesting) 
        //    {
        //        creatureController.CallExitState();
        //        CreatureCollectItem(); 
        //    }
        //}
    }

    protected void OnTriggerExit2D(Collider2D other)
    {
        if (other.tag == "Player" && !isPickedUp)
        {
            playerController = other.GetComponent<PlayerController>();
            if (playerController == null) { return; }
            if (playerController.ItemToPickup == this)
            {
                playerController.ItemToPickup = null;
                if (animator != null) { animator.SetBool("nearPlayer", false); }
            }
        }

        //if (other.tag == "Enemy" && !isPickedUp)
        //{
        //    creatureController = other.GetComponent<CreatureController>();
        //    if (creatureController == null) { return; }
        //    animator.SetBool("nearPlayer", false);
        //}
    }

    public void CacheItem()
    {
        if (!interactable) { return; }
        if (playerController == null) { return; }

        // Try consume item to apply health/food/water and cache resources
        if (!isInventory)
        {
            // check if item can be cached or is the cache full and reject item if full
            // Update resources cached amount if cache has space
            //bool success = PlayerInventory.Instance.PlayerResources.TryCacheResource(this);

            //Debug.Log("CacheItem: " + success);

            // Decommission Item
            //if (success) { ProcessItemCollected(); }
        }
    }

     public void CollectInventoryItem()
    {
        if (!interactable) { return; }
        if (playerController == null) { return; }

        // if storable, store item.  Otherwise, consume it
        if (isInventory)
        {
            // Try add item to inventory
            //if (inventory != null) { inventory.StoreItem(this, true); }

            // Decommission Item
            ProcessItemCollected();
        }
    }

    // --- PlayerInventory integration ---------------------------------------------------
    // Pulled into a hand inventory: silence the world pickup and hide the object without
    // destroying it, so it can be dropped back into the world later (DropIntoWorld).
    public void StoreInInventory()
    {
        interactable = false;
        if (itemCollider != null) { itemCollider.enabled = false; }
        isPickedUp = true;
        PlayPickupSound();

        if (playerController != null && playerController.ItemToPickup == this)
        {
            playerController.ItemToPickup = null;
        }

        if (sprite != null) { sprite.enabled = false; }
        gameObject.SetActive(false);
    }

    // Returned to the world at position (cycled-out on a full pickup, or dropped by the
    // player holding an inventory button). Re-enables the pickup so it can be collected again.
    public void DropIntoWorld(Vector2 position)
    {
        transform.position = position;
        gameObject.SetActive(true);
        if (sprite != null) { sprite.enabled = true; }
        if (itemCollider != null) { itemCollider.enabled = true; }
        isPickedUp = false;
        interactable = true;
    }

    private void ProcessItemCollected()
    {
        //Debug.Log("Process Item Collected");

        interactable = false;
        itemCollider.enabled = false;

        isPickedUp = true;
        PlayPickupSound();

        if (animator != null) { animator.SetTrigger("collected"); }
        //if (sprite != null) { sprite.enabled = false; }

        // clear ItemToPickup
        if (playerController.ItemToPickup == this)
        {
            playerController.ItemToPickup = null;
        }

        StartCoroutine(DestroyPickup());
    }

    public void DestroyItem()
    {
        if (animator != null) { animator.SetTrigger("collected"); }
        StartCoroutine(DestroyPickup());
    }

    public virtual void UseItemAbility(PlayerController playerController)
    {
        // Implemented in derived class if applicable
        //Debug.Log("Use Item Ability");
    }

    public virtual void UseItemAltAbility(PlayerController playerController)
    {
        // Implemented in derived class if applicable
        //Debug.Log("Crafting: Use Item Alternate Ability");

        //if (craftableItemPrefab == null || playerController == null) { return; }

        //// Try pay for crafting item
        //bool paid = PlayerInventory.Instance.PlayerResources.TryReceivePayment(0, 0, woodRequiredForCraft, oreRequiredForCraft, leatherRequiredForCraft);

        //if (paid)
        //{
        //    if (craftableItemPrefab == null) { return; }

        //    // Destroy current item
        //    PlayerInventory.Instance.DestroyCurrentHighlightedItem();

        //    // Drop craftable item
        //    Vector2 dropLocation = new Vector2(playerController.transform.position.x + 1 * playerController.transform.localScale.x, playerController.transform.position.y);

        //    GameObject itemDropped = Instantiate(craftableItemPrefab, dropLocation, Quaternion.identity);
        //    //Item itemComponent = itemDropped.GetComponent<Item>();
        //    //itemComponent.itemQty = remainingQtyToDrop;
        //}
    }

    public void CreatureCollectItem()
    {
        if (!creatureCanPickup) { return; }

        isPickedUp = true;
        if (animator != null) { animator.SetTrigger("collected"); }
        //if (sprite != null) { sprite.enabled = false; }
        PlayPickupSound();

        //if (creatureController == null) { return; }
        //if (creatureController.CreatureStats != null)
        //{
        //    //Debug.Log("hp: " + healthPoints);
            
        //    creatureController.CreatureStats.ChangeHealth(healthPoints);
        //    creatureController.CreatureStats.ChangeHunger(hungerPoints);
        //    creatureController.CreatureStats.ChangeThirst(thirstPoints);
        //}

        StartCoroutine(DestroyPickup());
    }

    IEnumerator DestroyPickup()
    {
        yield return new WaitForSeconds(timeToDeactivate);
        Destroy(gameObject);
    }

    IEnumerator EnablePickup()
    {
        yield return new WaitForSeconds(timeToEnablePickup);
        interactable = true;
    }

    public void PlayPickupSound()
    {
        if (pickupSound == null || audioSource == null) { return; }
        audioSource.PlayOneShot(pickupSound);
    }

    public void InflictDamage(float damageTaken)
    {       
        if (!interactable) { return; }  //Debug.Log("item is not interactable");

        // for creatures, this is how they consume items (remove onTrigger creature functionality)
        //Debug.Log("No dmg to Items");
    }

    public void Knockback(Vector2 knockbackReceived, float knockbackTimeReceived, int attackDir)
    {
        if (!interactable) { return; }

        // Add ability to knockback an item
        //Debug.Log("knockback CharacterStats: " + gameObject.name);

        float knockbackForceX = knockbackReceived.x > 0 ? knockbackReceived.x : 0;  // removes "- knockbackResistance.x" from equation
        float knockbackForceY = knockbackReceived.y > 0 ? knockbackReceived.y : 0;  // removes "- knockbackResistance.y" from equation

        Vector2 knockbackForce = new Vector2(knockbackForceX * attackDir, knockbackForceY);

        itemGravity.ApplyKnockback(knockbackForce, knockbackTimeReceived);
    }
}
