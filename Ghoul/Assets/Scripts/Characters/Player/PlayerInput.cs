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
    private InputAction attackAction;
    private InputAction jumpAction;
    private InputAction inventoryLeftAction;
    private InputAction inventoryRightAction;
    private InputAction pickupAction;
    private InputAction dodgeAction;
    private InputAction menuAction;
    private InputAction downAction;
    private InputAction upAction;
    private InputAction foodAction;
    private InputAction waterAction;
    #endregion


    // testing out delegate usage (use when we want the player to remap the controls via options menu)
    //public delegate void PressJumpAction(InputAction.CallbackContext context);
    //private PressJumpAction pressJumpHandler;



    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        playerControls = new InputSystem_Actions();

        playerControls.Player.Move.performed += MoveInput;
        playerControls.Player.Move.canceled += MoveInput;
        playerControls.Player.Jump.performed += OnJumpInputDown;
        playerControls.Player.Jump.canceled += OnJumpInputUp;
        //playerControls.Player.Fire.performed += UseItem;
        //playerControls.Player.InventoryLeft.performed += InventoryLeft;
        //playerControls.Player.InventoryRight.performed += InventoryRight;
        //playerControls.Player.Pickup.performed += PickupItem;
        //playerControls.Player.Dodge.performed += Dodge;
        //playerControls.Player.Menu.performed += OpenMenu;
        //playerControls.Player.DownPress.performed += DownPress;
        //playerControls.Player.DownPress.canceled += LookReturn;
        //playerControls.Player.UpPress.performed += UpPress;
        //playerControls.Player.UpPress.canceled += LookReturn;
        //playerControls.Player.UseFood.performed += UseFood;
        //playerControls.Player.UseWater.performed += UseWater;
    }
    private void OnEnable()
    {
        moveAction = playerControls.Player.Move;
        jumpAction = playerControls.Player.Jump;
        //attackAction = playerControls.Player.Fire;
        //inventoryLeftAction = playerControls.Player.InventoryLeft;
        //inventoryRightAction = playerControls.Player.InventoryRight;
        //pickupAction = playerControls.Player.Pickup;
        //dodgeAction = playerControls.Player.Dodge;
        //menuAction = playerControls.Player.Menu;
        //downAction = playerControls.Player.DownPress;
        //upAction = playerControls.Player.UpPress;
        //foodAction = playerControls.Player.UseFood;
        //waterAction = playerControls.Player.UseWater;


        moveAction.Enable();
        jumpAction.Enable();
        attackAction.Enable();
        inventoryLeftAction.Enable();
        inventoryRightAction.Enable();
        pickupAction.Enable();
        dodgeAction.Enable();
        menuAction.Enable();
        downAction.Enable();
        upAction.Enable();
        foodAction.Enable();
        waterAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        attackAction.Disable();
        inventoryLeftAction.Disable();
        inventoryRightAction.Disable();
        pickupAction.Disable();
        dodgeAction.Disable();
        menuAction.Disable();
        downAction.Disable();
        upAction.Disable();
        foodAction.Disable();
        waterAction.Disable();


        playerControls.Player.Move.performed -= MoveInput;
        playerControls.Player.Move.canceled -= MoveInput;
        playerControls.Player.Jump.performed -= OnJumpInputDown;
        playerControls.Player.Jump.canceled -= OnJumpInputUp;
        //playerControls.Player.Fire.performed -= UseItem;
        //playerControls.Player.InventoryLeft.performed -= InventoryLeft;
        //playerControls.Player.InventoryRight.performed -= InventoryRight;
        //playerControls.Player.Pickup.performed -= PickupItem;
        //playerControls.Player.Dodge.performed -= Dodge;
        //playerControls.Player.Menu.performed -= OpenMenu;
        //playerControls.Player.DownPress.performed -= DownPress;
        //playerControls.Player.DownPress.canceled -= LookReturn;
        //playerControls.Player.UpPress.performed -= UpPress;
        //playerControls.Player.UpPress.canceled -= LookReturn;
        //playerControls.Player.UseFood.performed -= UseFood;
        //playerControls.Player.UseWater.performed -= UseWater;
    }

    //private void Start()
    //{
    //    pressJumpHandler = ReassignButton;
    //}

    private void MoveInput(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.MoveInput(moveAction);
    }

    private void UpPress(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.UpPress(context);
    }

    private void LookReturn(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.LookReturn();
    }

    private void DownPress(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.DownPress(context);
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

    private void UseFood(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.UseFood(context);
    }

    private void UseWater(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.UseWater(context);
    }

    private void OpenMenu(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.OpenMenu(context);
    }

    private void Dodge(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        //playerController.Dodge(context);


        //// testing out delegate usage
        //if (!(pressJumpHandler == ReassignButton))
        //{
        //    Debug.Log("reassign 1: jump");
        //    pressJumpHandler = ReassignButton;
        //}
        //else
        //{
        //    Debug.Log("reassign 2: use item");
        //    pressJumpHandler = ReassignButton2;
        //}
    }

    //// testing out delegate usage
    //private void ReassignButton(InputAction.CallbackContext context)
    //{
    //    playerController.OnJumpInputDown(context);
    //}
    //// testing out delegate usage
    //private void ReassignButton2(InputAction.CallbackContext context)
    //{
    //    playerController.UseItem(context);
    //}


    private void PickupItem(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.PickupItem(context);
    }

    private void UseItem(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.UseItem(context);
    }

    private void OnJumpInputDown(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.OnJumpInputDown(context);

        // testing out delegate usage
        //if (pressJumpHandler != null) { pressJumpHandler(context); }
    }

    private void OnJumpInputUp(InputAction.CallbackContext context)
    {
        if (playerController == null) { return; }

        playerController.OnJumpInputUp(context);
    }
}
