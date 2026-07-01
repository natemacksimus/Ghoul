using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;
using Unity.Netcode;
//using LevelManagement;
//using Cinemachine;

public class PlayerController : EntityController
{
    [SerializeField] private float dodgeCooldownSet = 1f;
    [ShowOnly] [SerializeField] float dodgeCooldownTimer;

    private Vector2 directionalInput;
    private Vector2 lastDirInput = Vector2.right;
    private float directionX = 1; // isFacingRight

    private float velocityXSmoothing;
    [SerializeField] private float accelerationTimeAirborne = 0.2f;  // smoothing transition when moving left to right or vice versa
    [SerializeField] private float accelerationTimeGrounded = 0.1f;  // smoothing transition when moving left to right or vice versa
    [SerializeField] private float accelerationStopping = 0.1f;  // smoothing transition when moving left to right or vice versa
    [SerializeField] private float accelerationTurning = 0.1f;  // smoothing transition when moving left to right or vice versa

    [SerializeField] private float lookUpAmt = 0.8f;
    [SerializeField] private float lookDownAmt = 0.15f;

    [SerializeField] private float fallAmountMax = 40f;  // terminal fall velocity; keep >= maxJumpYVelocity so it never clips the jump arc (otherwise the descent floats at a constant speed instead of being parabolic)
    [SerializeField] private float gravity = -10f;
    [SerializeField] private float fallGravityMultiplier = 1.5f;  // extra gravity applied once falling, so the character drops faster than it rises (classic platformer feel)
    [SerializeField] private float coyoteTime = 0.05f;
    [SerializeField] private float jumpBuffer = 0.05f;
    [SerializeField] private float jumpBufferTimer = 0f;
    [SerializeField] private Vector2 airTimeStartPos;
    [SerializeField] private Vector2 airTimeEndPos;
    [SerializeField] private Vector2 airTimeMaxPos;
    [ShowOnly][SerializeField] private float heightDrop;
    [ShowOnly] [SerializeField] private float heightDropFromMax;
    [SerializeField] private float landingAnimThreshold = 5f;
    [SerializeField] private bool isAirborne = false;

    //public float jump = 5f;
    [SerializeField] private float maxJumpYVelocity = 30f;
    [SerializeField] private float minJumpYVelocity = 15f;
    [ShowOnly][SerializeField] private float addJumpXVelocity = 0f;
    [SerializeField] private float maxAddJumpXVelocity = 2f;
    [SerializeField] private float minAddJumpXVelocity = 0f;

    [SerializeField] private bool jumpPressed = false;
    public GameObject arrowSpawnPoint;
    [SerializeField] private GameObject hookInstance;
    public GameObject HookInstance { get => hookInstance; set => hookInstance = value; }

    [SerializeField] private GameObject movementSprite;

    [ShowOnly] [SerializeField] private float attackRateTimer;

    #region LADDERS
    public int nearLadder = 0;
    public GameObject currentLadder;
    [SerializeField] private bool onLadder;
    [SerializeField] private float xMoveOnLadderLeft;
    [SerializeField] private float xMoveOnLadderRight;
    [SerializeField] private float xMoveReleaseLeft = 0.2f;
    [SerializeField] private float xMoveReleaseRight = 0.2f;
    #endregion

    #region WALLSLIDING
    [SerializeField] private bool wallSliding;
    public bool WallSliding { get => wallSliding; }

    int wallDirX;
    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallLeap;
    public float wallSlideSpeedMax = 3;
    public float wallStickTime = .25f;
    public float timeToWallUnstick;
    #endregion

    private Vector2 moveDirection = Vector2.zero;

    public PlayerSounds sounds;

    private PlayerStats playerStats;
    private PlayerAttack playerAttack;
    //private PlayerInventory playerInventory;
    //[SerializeField] private CinemachineVirtualCamera virtualCamera;
    //private CinemachineFramingTransposer transposer;

    [SerializeField] private float deathDelay = 1.5f;  // time between when the player dies and the when the scene reloads

    [SerializeField] private Item itemToPickup = null;
    public Item ItemToPickup { get => itemToPickup; set => itemToPickup = value; }

    [SerializeField] private InteractableObjects objectToInteract = null;
    public InteractableObjects ObjectToInteract { get => objectToInteract; set => objectToInteract = value; }

    //public List<CreatureController> targeters;

    
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Remote players: disable input and local physics.
            // ClientNetworkTransform (added to the prefab) drives their position.
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) { playerInput.enabled = false; }
            enabled = false;
        }
    }

    protected override void Start()
    {
        base.Start();

        audioSource = GetComponent<AudioSource>();
        controller2D = GetComponent<Controller2D>();
        animator = GetComponent<Animator>();
        playerStats = GetComponent<PlayerStats>();
        playerAttack = GetComponent<PlayerAttack>();


        //GameObject virtualCameraObject = GameObject.FindGameObjectWithTag("Cinemachine Camera");
        //if (virtualCameraObject != null)
        //{
        //    virtualCamera = virtualCameraObject.GetComponent<CinemachineVirtualCamera>();
        //    transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        //}
    }

    protected override void Update()
    {
        base.Update();

        //moveDirection = moveAction.ReadValue<Vector2>();
    }


    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        
        // Manage timers
        if (dodgeCooldownTimer > 0) { dodgeCooldownTimer -= Time.deltaTime; }
        if (attackRateTimer > 0) { attackRateTimer -= Time.deltaTime; }
        if (jumpBufferTimer > 0) 
        { 
            jumpBufferTimer -= Time.deltaTime;
            
            
            if (controller2D.collisions.below) { AttemptJump(); }
        }

        // Calculate HeightDrop for each fall
        if (isAirborne)
        {
            if (transform.position.y > airTimeMaxPos.y)
            {
                airTimeMaxPos = transform.position;
            }
        }
           
        if ((controller2D.collisions.below || wallSliding) && isAirborne == true)
        {
            airTimeEndPos = transform.position;
            isAirborne = false;

            heightDrop = airTimeEndPos.y - airTimeStartPos.y;
            //Debug.Log("height drop: " + heightDrop);

            heightDropFromMax = airTimeEndPos.y - airTimeMaxPos.y;
        }

        if (!controller2D.collisions.below && !wallSliding && isAirborne == false) 
        { 
            airTimeStartPos = transform.position;
            airTimeMaxPos = airTimeStartPos;
            isAirborne = true;
        }

        


        SetDirectionalInput(directionalInput);

        if (directionalInput.x != 0)
        {
            //Debug.Log("dirX: " + directionalInput.x);
            if (controller2D.collisions.left && directionalInput.x == -1 || controller2D.collisions.right && directionalInput.x == 1) { isRunning = false; }
            else { isRunning = true; }
        }
        else
        {
            isRunning = false;
        }


        CalculateMoveAmount();
        //HandleWallSliding();
        //HandleAnimations();

        //LadderMovement();
        
        //if (disableInput && controller2D.collisions.below) { moveAmount.x = 0; }
        Move();

        // log last dir input to use for directing projectiles
        if (directionalInput != Vector2.zero) { lastDirInput = directionalInput; }
    }

    public void MoveInput(InputAction moveAction)
    {      
        Vector2 moveInput = moveAction.ReadValue<Vector2>();

        // movementSprite is for the ship's fire sprite when moving horizontally
        if (movementSprite != null) 
        { 
        if (moveInput.x == 0)
            {
                movementSprite.SetActive(false);
            }
        else
            {
                movementSprite.SetActive(true);
            }
        }

        directionalInput = new Vector2(Mathf.RoundToInt(moveInput.x), Mathf.RoundToInt(moveInput.y));
    }

    public void UpPress(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        //if (playerInventory == null) { return; }
        DisableInputIfMenuOpen();

        if (context.interaction is HoldInteraction)
        {
            if (disableInput) { return; }

            // If holding for x amt of secs, look down

            //if (context.)
            //Debug.Log("Look up");
            //if (transposer != null) { transposer.m_ScreenY = lookUpAmt; }


            // If jump is pressed, drop through platform (handled through Controller2D
        }
        else
        {
            // Grab ledge or climb
           
        }
    }

    public void LookReturn()
    {
        //Debug.Log("Look returned");
        //if (transposer != null) { transposer.m_ScreenY = 0.5f; }
    }

    public void DownPress(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        //if (playerInventory == null) { return; }
        DisableInputIfMenuOpen();

        if (context.interaction is HoldInteraction)
        {
            if (disableInput) { return; }

            // If holding for x amt of secs, look down

            //if (context.)
            //Debug.Log("Look down");
            //if (transposer != null) { transposer.m_ScreenY = lookDownAmt; }
            

            // If jump is pressed, drop through platform (handled through Controller2D
        }
        else
        {
            // test button: used to test out different things
            //PlayerInventory.Instance.PrintDictionary();
            //GameManager.Instance.BuildWorld.RerunSurePath();

            
        }

        // Return look down to original position, if not pressing down

    }

    public void InventoryLeft(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        DisableInputIfMenuOpen();


        if (context.interaction is HoldInteraction)
        {

        }
        else
        {
            // Shift highlighted inventory slot right
            //playerInventory.ChangeHighlightedSlot(-1);
            playerStats.UpdateCurrentDamageAndKnockback();
        }
    }

    public void InventoryRight(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        DisableInputIfMenuOpen();


        if (context.interaction is HoldInteraction)
        {

        }
        else
        {
            // Shift highlighted inventory slot right
            //playerInventory.ChangeHighlightedSlot(1);
            playerStats.UpdateCurrentDamageAndKnockback();
        }
    }

    public void OpenMenu(InputAction.CallbackContext context)
    {
        // Call the pause menu
        //Debug.Log("currentMenu: " + MenuManager.Instance.CurrentMenu);

        //if (MenuManager.Instance.CurrentMenu == GameMenu.Instance) 
        //{ 
        //    GameMenu.Instance.OnPausePressed(); 
        //}
        //else
        //{
        //    //Menu.OnBackPressed();
        //    MenuManager.Instance.CurrentMenu.OnBackPressed();
        //}
    }

    public void Dodge(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        //if (playerInventory == null) { return; }
        DisableInputIfMenuOpen();

        // Use variable: dodgePressed

        // Defense button (hold this + down = duck dodge, hold this + back = dodge step back, hold this + forward = roll forward, press/hold this + no direction = block, hold this + up = air dodge) 

        // Spend thirst to dodge

        if (dodgeCooldownTimer <= 0)
        {
            Debug.Log("Dodge");

            //animator.SetTrigger("dodge");
            dodgeCooldownTimer = dodgeCooldownSet;

            isDodging = true;  // need to make this false in the animation exit method.  Creat
        }
    }

    public void PickupItem(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        //if (playerInventory == null) { return; }
        DisableInputIfMenuOpen();

        //Debug.Log("Pickup Item pressed");

        Vector2 dropLocation = new Vector2(transform.position.x + directionX * 0.5f, transform.position.y);

        // Need to add the using statement: using UnityEngine.InputSystem.Interactions;
        if (context.interaction is HoldInteraction)
        {
            // Hold button to drop item
            //playerInventory.DropCurrentItem(dropLocation, false);
        }
        else
        {
            // Press button to pick up item into a slot, and drop an item if it occupies that slot
            // try drop current item in highlighted slot and pick up closest item in reach


            // Interact with items
            if (itemToPickup != null)
            {
                // check if item can be collected

                
                // drop current item in highlighted slot
                //if (playerInventory.CheckForAvailableSlot() == false) { playerInventory.DropCurrentItem(dropLocation, false); }
                
                // collect item
                itemToPickup.CollectInventoryItem();

                //PlayerInventory.Instance.GetCurrentHighlightedItem();
                return;
            }

            // Interact with Interactable
            if (objectToInteract != null)  // was: if (itemToPickup == null && objectToInteract != null)
            {
                objectToInteract.InteractWithObject();
            }
        }
    }

    public void UseItem(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }
        DisableInputIfMenuOpen();
        if (playerAttack != null)
        {
            // Fire in the direction held at press time, or the last held direction if
            // no direction is currently pressed.
            Vector2 attackDir = directionalInput != Vector2.zero ? directionalInput : lastDirInput;
            playerAttack.Attack(attackDir);
        }
    }

    private void DisableInputIfMenuOpen()
    {
        //if (BuyMenu.Instance != null) { if (BuyMenu.Instance.isOpen) { return; } }
        //if (ItemStorageMenu.Instance != null) { if (ItemStorageMenu.Instance.isOpen) { return; } }
        //if (ResearchMenu.Instance != null) { if (ResearchMenu.Instance.isOpen) { return; } }
        //if (PauseMenu.Instance != null) { if (PauseMenu.Instance.isOpen) { return; } }
    }

    public void OnJumpInputDown(InputAction.CallbackContext context)
    {
        //Debug.Log("jump");
        if (disableInput) { return; }
        if (disableJump) { return; }
        DisableInputIfMenuOpen();

        jumpPressed = true;

        // Fall through moving platform
        if (directionalInput.y == -1) { return; }  //Debug.Log("falling: " + controller2D.collisions.fallingThroughPlatform);

        // signal that jump has been pressed for BreakablePlatform

        //SetJumpVelocities();

        // reset jump buffer timer
        if (!controller2D.collisions.below)
        {
            jumpBufferTimer = jumpBuffer;
        }

        AttemptJump();
    }

    private void AttemptJump()
    {
        if (disableInput) { return; }
        if (disableJump) { return; }

        if (wallSliding)
        {
            WallJump();
        }
        else
        {
            Jump();
        }
    }

    private void WallJump()
    {
        // wallsliding jump

        // play jump sound
        PlayJump();

        LockFacingDir = false;

        if (wallDirX == directionalInput.x)
        {
            moveAmount.x = -wallDirX * wallJumpClimb.x;
            moveAmount.y = wallJumpClimb.y;
        }
        else if (directionalInput.x == 0)
        {
            moveAmount.x = -wallDirX * wallJumpOff.x;
            moveAmount.y = wallJumpOff.y;
        }
        else
        {
            moveAmount.x = -wallDirX * wallLeap.x;
            moveAmount.y = wallLeap.y;
        }
    }

    public override void Jump()
    {
        base.Jump();

        // normal jump

        //Debug.Log("onLadder1: " + onLadder);
        if (controller2D.collisions.below || airTime < coyoteTime || onLadder)
        {
            // Calc and set forward velocity
            float currentPercentOfMaxSpeed = 1f;
            currentPercentOfMaxSpeed = Mathf.Abs(moveAmount.x) / (playerStats.CurrentSpeed + addJumpXVelocity);

            float calcXVelocity = (maxAddJumpXVelocity - minAddJumpXVelocity) * currentPercentOfMaxSpeed + minAddJumpXVelocity;
            //Debug.Log("jump: calc: " + calcXVelocity + "; %: " + currentPercentOfMaxSpeed);
            addJumpXVelocity = calcXVelocity;

            //if (airTime < coyoteTime) { Debug.Log("airTime less than coyoteTime"); }

            onLadder = false;
            //Debug.Log("onLadder2: " + myController.collisions.slidingDownMaxSlope);
            if (controller2D.collisions.slidingDownMaxSlope)
            {
                //if (directionalInput.x != -Mathf.Sign(controller2D.collisions.slopeNormal.x))
                //{
                //    moveAmount.y = maxJumpVelocity * controller2D.collisions.slopeNormal.y;
                //    moveAmount.x = maxJumpVelocity * controller2D.collisions.slopeNormal.x;
                //}
            }
            else
            {
                // play jump sound
                PlayJump();

                // Jump
                moveAmount.y = maxJumpYVelocity;
            }
        }
    }

    public void OnJumpInputUp(InputAction.CallbackContext context)
    {
        if (disableInput) { return; }

        jumpPressed = false;

        DisableInputIfMenuOpen();

        if (moveAmount.y > minJumpYVelocity)
        {
            moveAmount.y = minJumpYVelocity;
        }
    }

    private void SetJumpVelocities()
    {
        //Debug.Log("maxjump: " + maxJumpVelocity);
        //Double jumpCalc = -0.0874 * Mathf.Pow(jump, 2f) + 2.371 * jump + 5.9324;

        // set max and min jump velocity
        //Double jumpCalc = Mathf.Sqrt(2 * Mathf.Abs(gravity) * jump);
        //maxJumpVelocity = (float)jumpCalc;
        //minJumpVelocity = maxJumpVelocity / 2;
        //Debug.Log("jump: " + jumpCalc);
    }

    public void SetDirectionalInput(Vector2 input)
    {
        directionX = (input.x == 0) ? directionX : Mathf.Sign(input.x);

        directionalInput = input;

        if ((directionX == 1 && isFacingRight == false && (Mathf.Abs(directionalInput.x) > 0)) ||
           ((directionX == -1 && isFacingRight == true) && (Mathf.Abs(directionalInput.x) > 0)))
        {
            if (!LockFacingDir) { Flip(); }
        }
    }
    private void CalculateMoveAmount()
    {
        float targetVelocityX = directionalInput.x * (playerStats.CurrentSpeed + addJumpXVelocity);
        //Debug.Log("addJumpX: " + addJumpXVelocity);
        //Debug.Log("targetVelocityX: " + targetVelocityX);
        if (!jumpPressed) { addJumpXVelocity = 0; }

        if (!disableInput)
        {
            float acceleration = 0f;
            //string accelUsed = null;
            if (!(Mathf.Abs(directionalInput.x) > 0)) { acceleration = accelerationStopping; }  // accelUsed = "stopping";
            else if (Mathf.Sign(directionalInput.x) != Mathf.Sign(moveAmount.x)) { acceleration = accelerationTurning; }  //  accelUsed = "turning";
            else { acceleration = accelerationTimeGrounded; } // accelUsed = "grounded";

            if (!controller2D.collisions.below) { acceleration = accelerationTimeAirborne; }  // accelUsed = "airborne"; 
            //Debug.Log("accelUsed: " + accelUsed);

            moveAmount.x = Mathf.SmoothDamp(moveAmount.x, targetVelocityX, ref velocityXSmoothing, acceleration);

            // Zero out most of the value of moveAmount.X if player is colliding with wall and pressing a direction towards it.  Wall leap won't work if zero out completely.
            // Moving away from wall should start accelerating from zero (or as close to zero as possible)
            if (controller2D.collisions.left && moveAmount.x < 0) { moveAmount.x = -0.1f; }
            if (controller2D.collisions.right && moveAmount.x > 0) { moveAmount.x = 0.1f; }
            //moveAmount.x = Mathf.Clamp(moveAmount.x, -(playerStats.CurrentSpeed + addJumpXVelocity), (playerStats.CurrentSpeed + addJumpXVelocity));


            //horzVelocity += movementInput.x;
            //horzVelocity *= Mathf.Pow(1f - acceleration, Time.deltaTime * 10f);
            //horzVelocity = Mathf.Clamp(horzVelocity, -(playerStats.CurrentSpeed + addJumpXVelocity), (playerStats.CurrentSpeed + addJumpXVelocity));
            //moveAmount.x = horzVelocity;
        }

        // While being flung by a directional knockback, the knockback owns moveAmount
        // (both axes) so the flight stays straight and reflects cleanly — skip gravity
        // and the terminal-velocity clamp entirely.
        if (!directionalKnockback)
        {
            if (!onLadder)
            {
                // Apply stronger gravity on the way down so the fall is faster than the rise.
                float gravityThisStep = moveAmount.y < 0f ? gravity * fallGravityMultiplier : gravity;
                moveAmount.y += gravityThisStep;
            }
            else
            {
                //moveAmount.y = directionalInput.y * (playerStats.speed + speedAdjustment);
                moveAmount.y = directionalInput.y * (playerStats.CurrentSpeed);
            }

            // Clamp the fall to terminal velocity only. fallAmountMax must stay above the
            // jump launch speed so a normal jump completes its parabola before this kicks in.
            if (moveAmount.y < -fallAmountMax) { moveAmount.y = -fallAmountMax; }
        }
    }

    private void Move()
    {
        //Debug.Log("player moveAmount.y: " + moveAmount.y);

        // Continue X velocity when moving in the air
        if (continueMoveX)
        {
            if (controller2D.collisions.below) { DisableContinueMoveX(); }
            else { moveAmount.x = lastXVelocity; }
        }

        // Zero X movement if on the ground when zeroMoveX is enabled
        if (ZeroMoveX)
        {
            if (controller2D.collisions.below) { moveAmount.x = 0; }
        }

        // Move
        controller2D.Move(moveAmount * Time.deltaTime, directionalInput, false, jumpPressed);

        // Don't zero vertical velocity on floor/ceiling contact during a directional
        // knockback — the reflection logic reads and flips that velocity next frame.
        if (!directionalKnockback && (controller2D.collisions.above || controller2D.collisions.below))
        {
            moveAmount.y = 0;
        }
    }

    //private void HandleAnimations()
    //{
    //    if (animator == null) { return; }
    //    if (controller2D.collisions.below)
    //    {
    //        if (animator.GetBool("isGrounded") == false) 
    //        {
    //            //Debug.Log("airTime: " + base.airTime);
    //            //animator.SetFloat("airTime", airTime);
    //            airTime = 0;
    //            animator.SetFloat("heightDrop", heightDropFromMax);
    //            PlayHitGround(); 
    //        }
    //        animator.SetBool("isGrounded", true);
    //    }
    //    else
    //    {
    //        animator.SetBool("isGrounded", false);
    //    } 
    //    if (!controller2D.collisions.below && moveAmount.y > 0)
    //    {
    //        animator.SetBool("isFalling", false);
    //    }
    //    else
    //    {
    //        animator.SetBool("isFalling", true);
    //    }
    //    float movingHorizontally = directionalInput.x;
    //    float movingVertically = moveAmount.y;
    //    animator.SetFloat("speed", Mathf.Abs(movingHorizontally));
    //}

    private void LadderMovement()
    {
        if (onLadder && nearLadder <= 0)
        {
            if (directionalInput.y == 1)
            {
                if (directionalInput.x != 0) { onLadder = false; }
                moveAmount.y = 0;
            }
            else
            {
                if (directionalInput != Vector2.zero)
                {
                    onLadder = false;
                }
                //moveAmount.y = 0;
            }

        }

        if (onLadder && nearLadder > 0)
        {
            // onLadder and nearLadder
            if (moveAmount.x != 0)
            {
                moveAmount.x = 0;

                if (directionalInput.x > 0)
                {
                    xMoveOnLadderRight += Time.deltaTime;

                    if (xMoveOnLadderRight > xMoveReleaseRight)
                    {
                        onLadder = false;
                        xMoveOnLadderRight = 0;
                    }
                }
                else
                {
                    xMoveOnLadderRight = 0;
                }

                if (directionalInput.x < 0)
                {
                    xMoveOnLadderLeft += Time.deltaTime;

                    if (xMoveOnLadderLeft > xMoveReleaseLeft)
                    {
                        onLadder = false;
                        xMoveOnLadderLeft = 0;
                    }
                }
                else
                {
                    xMoveOnLadderLeft = 0;
                }
            }
            else
            {
                xMoveOnLadderLeft = 0;
                xMoveOnLadderRight = 0;
            }
        }

        if (nearLadder > 0 && directionalInput.y != 0)
        {
            onLadder = true;
            if (currentLadder != null)
            {
                Vector3 newPos = new Vector3(currentLadder.transform.position.x, transform.position.y, transform.position.z);
                //transform.position = Vector3.MoveTowards(transform.position, newPos, Time.deltaTime * (playerStats.speed + speedAdjustment));
                transform.position = Vector3.MoveTowards(transform.position, newPos, Time.deltaTime * (playerStats.CurrentSpeed));
            }
        }
    }

    private void HandleWallSliding()
    {
        wallDirX = (controller2D.collisions.left) ? -1 : 1;

        if ((controller2D.collisions.left || controller2D.collisions.right) && !controller2D.collisions.below && moveAmount.y < 0)
        {
            wallSliding = true;
            
            // set face direction equal to wallDirX
            if (isFacingRight && controller2D.collisions.left) { Flip(); }
            if (!isFacingRight && controller2D.collisions.right) { Flip(); }
            lockFacingDir = true;
            
            airTime = 0;

            // Set Y move to wall slide max speed
            if (moveAmount.y < -wallSlideSpeedMax) { moveAmount.y = -wallSlideSpeedMax; }

            // decrease timeToWallUnstick timer when pressing away from wall and remain stuck to wall until timer reaches zero
            if (timeToWallUnstick > 0)
            {
                if (directionalInput.x != wallDirX && directionalInput.x != 0)
                {
                    timeToWallUnstick -= Time.deltaTime;
                }
                
                velocityXSmoothing = 0;
                ZeroMoveX = true;

                if (disableJump) { disableJump = false; }

                //moveAmount.x = 0;
            }
            else
            {
                // release wall and do wall jump off
                moveAmount.x = -wallDirX * wallJumpOff.x;
                moveAmount.y = wallJumpOff.y;
                ZeroMoveX = false;
            }
        }
        else 
        { 
            wallSliding = false;
        }

        // clear ZeroMoveX if not currently wallsliding
        if (!controller2D.collisions.left && !controller2D.collisions.right && !controller2D.collisions.below) { ZeroMoveX = false; }

        // reset timeToWallUnstick between each wallslide
        if (!wallSliding) { timeToWallUnstick = wallStickTime; }
    }

    //IEnumerator LoadBaseScene()
    //{
    //    DisableResting();
    //    DisableZeroMoveX();
    //    disableInput = true;
    //    InvincibilityOff();

    //    yield return new WaitForSeconds(deathDelay);
    //    //Reset inventory
    //    //playerInventory.ResetInventory();
        
        
    //    //LevelManagement.LevelLoader.LoadLevel("Base");

    //    //SceneController.ReloadScene();
    //}

    //public override void Kill()
    //{
    //    StartCoroutine(LoadBaseScene());
    //}

    public void UpdateWallslidingAbility(float newValue)
    {
        wallSlideSpeedMax = newValue;
    }


    // Used in animations to enable hit object
    public override void EnableDamageObject()
    {
        base.EnableDamageObject();
    }

    // Used in animations to disable hit object
    public override void DisableDamageObject()
    {
        base.DisableDamageObject();
    }

    public override void PlayFootStep()
    {
        if (audioSource == null || sounds.footstepSound.Length < 1) { return; }

        int selectClipNo = UnityEngine.Random.Range(0, sounds.footstepSound.Length);

        audioSource.PlayOneShot(sounds.footstepSound[selectClipNo]);
    }

    public override void PlayHurtSound()
    {
        if (audioSource == null || sounds.hurtSound.Length < 1) { return; }

        int selectClipNo = UnityEngine.Random.Range(0, sounds.hurtSound.Length);

        audioSource.PlayOneShot(sounds.hurtSound[selectClipNo]);
    }

    public void PlayJump()
    {
        if (audioSource == null || sounds.jumpSound == null) { return; }
        audioSource.PlayOneShot(sounds.jumpSound);
    }
    public void PlayHitGround()
    {
        if (audioSource == null || sounds.hitGroundSound == null) { return; }
        audioSource.PlayOneShot(sounds.hitGroundSound);
    }
}
