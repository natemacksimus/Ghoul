using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemGravity : MonoBehaviour
{
    protected Vector2 moveAmount;
    protected Controller2D controller2D;

    [SerializeField] protected float currentGravity;
    [SerializeField] protected float fallAmountMax;

    [SerializeField] protected bool destroyOnGroundHit = false;

    [ShowOnly] [SerializeField] protected float knockbackTimer;
    public float KnockbackTimer { get { return knockbackTimer; } set { knockbackTimer = value; } }

    [ShowOnly] [SerializeField] protected Vector2 knockbackForce = Vector2.zero;
    public Vector2 KnockbackForce { get { return knockbackForce; } set { knockbackForce = value; } }

    [SerializeField] protected float knockbackReduceFactor = 0.1f;

    [SerializeField] protected bool isKnockbacked = false;
    public bool IsKnockbacked { get { return isKnockbacked; } set { isKnockbacked = value; } }

    [SerializeField] protected bool dropItemOnStart = true;
    [SerializeField] float minhorzForce = -2;
    [SerializeField] float maxhorzForce = 2;
    [SerializeField] float minVertForce = 5f;
    [SerializeField] float maxVertForce = 5f;

    // Start is called before the first frame update
    protected virtual void Start()
    {
        controller2D = GetComponent<Controller2D>();
        if (dropItemOnStart) { DropItem(); }
    }

    // Update is called once per frame
    protected virtual void FixedUpdate()
    {
        HandleKnockback();


        moveAmount.y += currentGravity * Time.deltaTime;
        //Debug.Log("moveAmount.y: " + moveAmount.y + " ; gravity: " + currentGravity);

        //cap Y velocity
        float yMoveSign = moveAmount.y > 1 ? 1 : -1;

        float fallMaxPerFrame = fallAmountMax * Time.deltaTime;


        if (moveAmount.y < 0 && Mathf.Abs(moveAmount.y) > fallMaxPerFrame) { moveAmount.y = fallMaxPerFrame * yMoveSign; }

        controller2D.Move(moveAmount, Vector2.zero, false, false);

        // set x move to zero when colliding with ground
        if (controller2D.collisions.below) { moveAmount.x = 0; }

        // destroy ground on collision with ground
        if (controller2D.collisions.below && destroyOnGroundHit) { DestroyBlood(); }
    }

    public void DropItem()
    {
        float xForce = UnityEngine.Random.Range(minhorzForce, maxhorzForce);
        xForce = Mathf.Round(xForce * 10) / 10;

        float yForce = UnityEngine.Random.Range(minVertForce, maxVertForce);
        yForce = Mathf.Round(yForce * 10) / 10;

        Vector2 dropForce = new Vector2(xForce, yForce);
        moveAmount += dropForce * Time.deltaTime;

        //StartCoroutine(DestroyPickup());
    }

    protected void DestroyBlood()
    {
        StartCoroutine(DestroyBloodOnTouch());
    }

    IEnumerator DestroyBloodOnTouch()
    {
        //animation here

        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);

        //Destroy(this.gameObject);

        //this.gameObject.SetActive(false);
    }

    protected void HandleKnockback()
    {
        if (knockbackTimer > 0)
        {
            knockbackTimer -= Time.deltaTime;
            int signX = knockbackForce.x > 0 ? -1 : 1;

            float newKnockbackX = Mathf.Abs(knockbackForce.x) > 0 ? knockbackForce.x * (1 - knockbackReduceFactor) : 0;

            knockbackForce = new Vector2(newKnockbackX, moveAmount.y);

            //Debug.Log("newX: " + newKnockbackX);

            if (this.tag == "Player")
            {
                moveAmount.x = knockbackForce.x;
            }
            else
            {
                moveAmount.x = knockbackForce.x * Time.deltaTime;
            }
        }
        else
        {
            if (isKnockbacked)
            {
                //lockFacingDir = false;
                //if (disableInput) { DisableInputOff(); }
                isKnockbacked = false;
            }
        }
    }

    public void ApplyKnockback(Vector2 knockbackPower, float knockbackTime)
    {
        //if (invincible) { return; }

        //Debug.Log("knockbackTime: " + knockbackTime);
        // Hit received determines the knockback power and stun time
        //lockFacingDir = true;  // might be able to use only isKnockbacked to do this

        isKnockbacked = true;

        knockbackForce = knockbackPower;
        knockbackTimer = knockbackTime;

        if (this.tag == "Player")
        {
            moveAmount = new Vector2(knockbackForce.x, knockbackForce.y);   // knockbackForce * Time.deltaTime;  // 
        }
        else
        {
            moveAmount = new Vector2(knockbackForce.x, knockbackForce.y) * Time.deltaTime;   // knockbackForce * Time.deltaTime;  // 
        }
    }
}
