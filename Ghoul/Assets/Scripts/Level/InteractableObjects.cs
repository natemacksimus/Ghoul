using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableObjects : MonoBehaviour, IDamageable
{
    [SerializeField] protected bool disableObject = false;  // disables IDamageable methods
    public bool DisableObject { get => disableObject; set => disableObject = value; }

    [SerializeField] protected GameObject inputPromptMessage;

    [SerializeField] protected bool hideInputPrompt = true;

    protected Animator animator;
    protected AudioSource audioSource;
    [SerializeField] protected PlayerController playerController;
    protected GameObject player;
    //protected CreatureController creatureController;

    [SerializeField] protected float interactRange = 1f;

    [SerializeField] protected bool defaultIsInteractable = false;  // enables InteractWithObject();
    [SerializeField] protected bool defaultIsDamageable = true;  // enables InflictDamage();
    [SerializeField] protected bool defaultIsKnockbackable = true;  // enables Knockback();

    [SerializeField] protected bool isInteractable = false;  // enables InteractWithObject();
    [SerializeField] protected bool isDamageable = true;  // enables InflictDamage();
    [SerializeField] protected bool isKnockbackable = true;  // enables Knockback();
    public bool IsInteractable { get => isInteractable; set => isInteractable = value; }


    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        playerController = player.GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        isInteractable = defaultIsInteractable;
        isDamageable = defaultIsDamageable;
        isKnockbackable = defaultIsKnockbackable;
    }

    protected virtual void Update()
    {
        if (disableObject) { return; }
        if (!isInteractable)
        {
            if (inputPromptMessage != null && inputPromptMessage.activeSelf == true) { inputPromptMessage.SetActive(false); }
            return;
        }

        Vector2 distanceToPlayer = player.transform.position - transform.position;
        float distanceToPlayerMagnitude = distanceToPlayer.magnitude;
        //if (playerController.ObjectToInteract == this) { Debug.Log("Teleported: " + distanceToPlayerMagnitude); }
        if (playerController.ObjectToInteract == null && distanceToPlayerMagnitude < interactRange) 
        {
            if (inputPromptMessage != null && isInteractable) { inputPromptMessage.SetActive(true); }
            if (animator != null) { animator.SetBool("interactable", true); }
            playerController.ObjectToInteract = this; 
        }
        if (playerController.ObjectToInteract == this && distanceToPlayerMagnitude > interactRange) 
        {
            if (inputPromptMessage != null && hideInputPrompt) { inputPromptMessage.SetActive(false); }
            if (animator != null) { animator.SetBool("interactable", false); }
            playerController.ObjectToInteract = null; 
        }
    }

    public virtual void InteractWithObject()
    {
        Debug.Log("interact 1");
        //if (!isInteractable || disableObject) { return; }
    }

    public virtual void InflictDamage(float damageTaken)
    {
        //if (!isDamageable || disableObject) { return; }
    }

    public virtual void Knockback(Vector2 knockbackPower, float knockbackTime, int attackDir)
    {
        //if (!isKnockbackable || disableObject) { return; }
    }
}
