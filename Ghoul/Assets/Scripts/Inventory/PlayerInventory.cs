using System.Collections.Generic;
using UnityEngine;

public enum Hand { Right, Left }

// One hand's worth of held items: a capped list with a single "active" (equipped) slot.
// Cycling advances the active slot; the active item is what UseRightHand/UseLeftHand uses.
[System.Serializable]
public class HandInventory
{
    [SerializeField] private int capacity = 3;
    [SerializeField] private List<Item> items = new List<Item>();
    [ShowOnly] [SerializeField] private int activeIndex = 0;

    public int Capacity { get => capacity; set => capacity = value; }
    public int Count => items.Count;
    public bool IsFull => items.Count >= capacity;
    public int ActiveIndex => activeIndex;

    public Item Active
    {
        get
        {
            if (items.Count == 0) { return null; }
            activeIndex = Mathf.Clamp(activeIndex, 0, items.Count - 1);
            return items[activeIndex];
        }
    }

    // Adds to a non-full inventory and makes the new item active. Returns false if full.
    public bool Add(Item item)
    {
        if (item == null || IsFull) { return false; }
        items.Add(item);
        activeIndex = items.Count - 1;
        return true;
    }

    // Swaps the incoming item into the active slot, returning the item it displaced.
    public Item ReplaceActive(Item item)
    {
        if (item == null) { return null; }
        if (items.Count == 0) { Add(item); return null; }
        activeIndex = Mathf.Clamp(activeIndex, 0, items.Count - 1);
        Item removed = items[activeIndex];
        items[activeIndex] = item;
        return removed;
    }

    public void CycleNext()
    {
        if (items.Count == 0) { return; }
        activeIndex = (activeIndex + 1) % items.Count;
    }

    // Removes and returns the active item, keeping activeIndex in range.
    public Item RemoveActive()
    {
        if (items.Count == 0) { return null; }
        activeIndex = Mathf.Clamp(activeIndex, 0, items.Count - 1);
        Item removed = items[activeIndex];
        items.RemoveAt(activeIndex);
        if (activeIndex >= items.Count) { activeIndex = Mathf.Max(0, items.Count - 1); }
        return removed;
    }
}

// Holds the player's two hand inventories and drives pickup / cycle / drop / use lookups.
// Owner-local: not networked yet (world Item objects here are not NetworkObjects).
[RequireComponent(typeof(PlayerController))]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private int startingCapacity = 3;
    [Tooltip("How far in front of the player a dropped item is spawned, in world units.")]
    [SerializeField] private float dropOffset = 0.6f;

    [SerializeField] private HandInventory rightHand = new HandInventory();
    [SerializeField] private HandInventory leftHand = new HandInventory();

    private PlayerController playerController;
    private PlayerStats playerStats;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerStats = GetComponent<PlayerStats>();

        rightHand.Capacity = startingCapacity;
        leftHand.Capacity = startingCapacity;
    }

    private HandInventory Get(Hand hand) => hand == Hand.Right ? rightHand : leftHand;

    public Item GetActive(Hand hand) => Get(hand).Active;

    // Routes a picked-up item into the hand it declares (Either -> Right). If that hand is
    // full, the incoming item replaces the active one and the displaced item drops.
    public bool TryPickup(Item item)
    {
        if (item == null) { return false; }

        Hand hand = item.handSlot == HandSlot.Left ? Hand.Left : Hand.Right;
        HandInventory inv = Get(hand);

        item.StoreInInventory();

        if (!inv.IsFull)
        {
            inv.Add(item);
        }
        else
        {
            Item displaced = inv.ReplaceActive(item);
            if (displaced != null) { DropItemIntoWorld(displaced); }
        }

        RefreshStats();
        return true;
    }

    public void Cycle(Hand hand)
    {
        Get(hand).CycleNext();
        RefreshStats();
    }

    public void DropActive(Hand hand)
    {
        Item removed = Get(hand).RemoveActive();
        if (removed != null) { DropItemIntoWorld(removed); }
        RefreshStats();
    }

    private void DropItemIntoWorld(Item item)
    {
        float dir = playerController != null && playerController.IsFacingRight ? 1f : -1f;
        Vector2 pos = (Vector2)transform.position + new Vector2(dir * dropOffset, 0f);
        item.DropIntoWorld(pos);
    }

    private void RefreshStats()
    {
        if (playerStats != null) { playerStats.UpdateCurrentDamageAndKnockback(); }
    }
}
