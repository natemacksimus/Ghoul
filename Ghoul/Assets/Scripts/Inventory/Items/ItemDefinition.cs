using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[CreateAssetMenu(fileName = "NewItem", menuName = "Create New Item", order = 1)]
public class ItemDefinition : ScriptableObject
{
    public InventoryType inventoryType = InventoryType.WEAPON;
    public string itemName;
    public Sprite itemIcon = null;
    public GameObject itemGameObject = null;
    public AnimatorOverrideController itemAnimator;

    //[Range(0, 9999)] public int pricePerUnit = 0;

    [Range(-999, 999)] public int itemDamage = 0;  // value when item is equipped
    [Range(-999, 999)] public int itemAttackRate = 0;
    [Range(-999, 999)] public int healthPoints = 0;  // amt of health points to add/subtract from player health when consumed
    [Range(-999, 999)] public int hungerPoints = 0;  // amt of health points to add/subtract from player health when consumed
    [Range(-999, 999)] public int thirstPoints = 0;  // amt of health points to add/subtract from player health when consumed

    public bool isStorable = true;  // is consumed on the spot if not storable
    public bool isConsumable = false;  // can be used when in inventory hotbar
}
