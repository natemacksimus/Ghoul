using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerInput : MonoBehaviour  
{
    [SerializeField] private PlayerController playerController;
    public PlayerController PlayerController { get => playerController; set => playerController = value; }

    #region PLAYER INPUT
    public InputSystem_Actions playerControls;
    private InputAction moveAction;
    private InputAction aimAction;
    private InputAction useLeftHandAction;
    private InputAction useRightHandAction;
    private InputAction inventoryLeftAction;
    private InputAction inventoryRightAction;
    private InputAction interactAction;
    private InputAction skillAction;
    private InputAction menuAction;
    #endregion



    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        playerControls = new InputSystem_Actions();

        playerControls.Player.Move.performed += MoveInput;
        playerControls.Player.Move.canceled += MoveInput;
        playerControls.Player.Aim.performed += AimInput;
        playerControls.Player.Aim.canceled += AimInput;
        playerControls.Player.UseLeftHand.performed += UseLeftHand;
        playerControls.Player.UseRightHand.performed += UseRightHand;
        playerControls.Player.InventoryLeftHand.started += InventoryLeft;
        playerControls.Player.InventoryLeftHand.canceled += InventoryLeft;
        playerControls.Player.InventoryRightHand.started += InventoryRight;
        playerControls.Player.InventoryRightHand.canceled += InventoryRight;
        playerControls.Player.Interact.performed += Interact;
        playerControls.Player.Skill.performed += OnJumpInputDown;
        playerControls.Player.Skill.canceled += OnJumpInputUp;
        playerControls.Player.Pause.performed += OpenMenu;

    }

    private void OnEnable()
    {
        moveAction = playerControls.Player.Move;
        aimAction = playerControls.Player.Aim;
        useLeftHandAction = playerControls.Player.UseLeftHand;
        useRightHandAction = playerControls.Player.UseRightHand;
        inventoryLeftAction = playerControls.Player.InventoryLeftHand;
        inventoryRightAction = playerControls.Player.InventoryRightHand;
        interactAction = playerControls.Player.Interact;
        skillAction = playerControls.Player.Skill;
        menuAction = playerControls.Player.Pause;


        moveAction.Enable();
        aimAction.Enable();
        useLeftHandAction.Enable();
        useRightHandAction.Enable();
        inventoryLeftAction.Enable();
        inventoryRightAction.Enable();
        interactAction.Enable();
        skillAction.Enable();
        menuAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        aimAction.Disable();
        useLeftHandAction.Disable();
        useRightHandAction.Disable();
        inventoryLeftAction.Disable();
        inventoryRightAction.Disable();
        interactAction.Disable();
        skillAction.Disable();
        menuAction.Disable();


        playerControls.Player.Move.performed -= MoveInput;
        playerControls.Player.Move.canceled -= MoveInput;
        playerControls.Player.Aim.performed -= AimInput;
        playerControls.Player.Aim.canceled -= AimInput;
        playerControls.Player.UseLeftHand.performed -= UseLeftHand;
        playerControls.Player.UseRightHand.performed -= UseRightHand;
        playerControls.Player.InventoryLeftHand.started -= InventoryLeft;
        playerControls.Player.InventoryLeftHand.canceled -= InventoryLeft;
        playerControls.Player.InventoryRightHand.started -= InventoryRight;
        playerControls.Player.InventoryRightHand.canceled -= InventoryRight;
        playerControls.Player.Interact.performed -= Interact;
        playerControls.Player.Skill.performed -= OnJumpInputDown;
        playerControls.Player.Skill.canceled -= OnJumpInputUp;
        playerControls.Player.Pause.performed -= OpenMenu;

    }


    private void MoveInput(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.MoveInput(moveAction);
    }

    private void AimInput(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.AimInput(aimAction);
    }

    private void UseLeftHand(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.UseLeftHand(context);
    }

    private void UseRightHand(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.UseRightHand(context);
    }

    private void InventoryLeft(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.InventoryLeft(context);
    }

    private void InventoryRight(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.InventoryRight(context);
    }

    private void Interact(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.Interact(context);
    }

    private void OnJumpInputDown(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.OnJumpInputDown(context);
    }

    private void OnJumpInputUp(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.OnJumpInputUp(context);
    }

    private void OpenMenu(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.OpenMenu(context);
    }

}
