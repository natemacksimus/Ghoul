using UnityEngine;

public class Controller2D : RaycastController
{
    [SerializeField] float maxSlopeAngle = 80f;
    [SerializeField] float resetFallingThroughTimer = 0.25f;

    public CollisionInfo collisions;
    [HideInInspector]
    public Vector2 playerInput;
    public bool jumped;

    public override void Start()  // this looks at the base class and runs the "virtual" start class in RaycastController
    {
        base.Start();
        collisions.faceDir = 1;  // 1 = face right
    }

    public void Move(Vector2 moveAmount, bool standingOnPlatform)  // created overload method to handle Moving platform reference, since it does not pass in "input"
    {
        Move(moveAmount, Vector2.zero, standingOnPlatform);
    }

    public void Move(Vector2 moveAmount, Vector2 input, bool standingOnPlatform = false, bool jumpPressed = false)
    {
        UpdateRaycastOrigins();
        collisions.Reset();
        collisions.moveAmountOld = moveAmount;
        playerInput = input;
        jumped = jumpPressed;

        if (moveAmount.y < 0) { DescendSlope(ref moveAmount); }

        if (moveAmount.x != 0) { collisions.faceDir = (int)Mathf.Sign(moveAmount.x); } // the "(int)" is a cast to convert the float to int type

        HorizontalCollisions(ref moveAmount);  // the ref keyword is used so that we can make changes to the moveAmount value using this method.

        if (moveAmount.y != 0) { VerticalCollisions(ref moveAmount); }

        transform.Translate(moveAmount);

        if (standingOnPlatform) { collisions.below = true; }
    }
    void HorizontalCollisions(ref Vector2 moveAmount)
    {
        float directionX = collisions.faceDir;
        float directionY = Mathf.Sign(moveAmount.y);
        float rayLength = Mathf.Abs(moveAmount.x) + skinWidthHorizontal;

        for (int i = 0; i < horizontalRayCount; i++)
        {
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
            rayOrigin += Vector2.up * (horizontalRaySpacing * i);
            RaycastHit2D[] hit2 = Physics2D.RaycastAll(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            //Debug.DrawRay(rayOrigin, Vector2.right * directionX, Color.red);

            //  if the rays hit something, proceed
            if (hit2.Length > 0)
            {
                foreach (RaycastHit2D touch in hit2)
                {
                    if (touch.collider.gameObject == gameObject) { continue; }  // if the ray hits the player, ignore

                    if (touch.distance == 0)
                    {
                        continue;  // move on to the next ray.
                    }

                    // if standing on a platform tagged as "Through", ignore collisions
                    if (touch.collider.tag == "Through")
                    {
                        // if player is jumping up or falling through platform, ignore collisions

                        // ignore horizontal collisions
                        if (!collisions.left || !collisions.right)
                        {
                            continue;
                        }

                        // ignore if player is moving up
                        if (directionY == 1 || touch.distance == 0)
                        {
                            continue;
                        }

                        // ignore if falling through platform is turned on
                        if (collisions.fallingThroughPlatform)
                        {
                            continue;
                        }
                    }

                    float slopeAngle = Vector2.Angle(touch.normal, Vector2.up);  // this calculates the difference between the normal of the collider and global up.
                    if (i == 0 && slopeAngle <= maxSlopeAngle)
                    {
                        if (collisions.descendingSlope)
                        {
                            collisions.descendingSlope = false;
                            moveAmount = collisions.moveAmountOld;
                        }
                        float distanceToSlopeStart = 0;
                        if (slopeAngle != collisions.slopeAngleOld)
                        {
                            distanceToSlopeStart = touch.distance - skinWidthHorizontal;
                            moveAmount.x -= distanceToSlopeStart * directionX;
                        }
                        ClimbSlope(ref moveAmount, slopeAngle, touch.normal);
                        moveAmount.x += distanceToSlopeStart * directionX;
                    }

                    if (!collisions.climbingSlope || slopeAngle > maxSlopeAngle)  // if climbing slope
                    {
                        moveAmount.x = (touch.distance - skinWidthHorizontal) * directionX;
                        rayLength = touch.distance;

                        // Keeps the player from trying to climb unclimbable slopes/walls
                        if (collisions.climbingSlope)
                        {
                            //Debug.Log("Climbing slope even though its beyond max angle???");
                            moveAmount.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x);
                        }

                        //if (gameObject.tag == "Impasse") { Debug.Log("collisions layer: " + touch.collider.gameObject.layer + "; left: " + (directionX == -1)) ; }
                        collisions.left = directionX == -1;  // if we hit something and directionX is -1, set collisions.left = true
                        collisions.right = directionX == 1;
                    }
                }
            }
        }
    }

    void VerticalCollisions(ref Vector2 moveAmount)
    {
        float directionY = Mathf.Sign(moveAmount.y);
        float rayLength = Mathf.Abs(moveAmount.y) + skinWidth;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i + moveAmount.x);

            RaycastHit2D[] hit2 = Physics2D.RaycastAll(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            //Debug.DrawRay(rayOrigin, Vector2.up * directionY, Color.red);

            if (hit2.Length > 0)
            {
                foreach (RaycastHit2D touch in hit2)
                {
                    if (touch.collider.gameObject == gameObject) { continue; }

                    // One-way platform behavior
                    if (touch.collider.tag == "Through")
                    {
                        if (directionY == 1 || touch.distance == 0)
                        {
                            continue;
                        }
                        if (collisions.fallingThroughPlatform)
                        {
                            continue;
                        }
                        if (playerInput.y == -1 && jumped)
                        {
                            collisions.fallingThroughPlatform = true;
                            Invoke("ResetFallingThrouhPlatform", resetFallingThroughTimer);
                            continue;
                        }
                    }

                    moveAmount.y = (touch.distance - skinWidth) * directionY;
                    rayLength = touch.distance;

                    if (collisions.climbingSlope)
                    {
                        moveAmount.x = moveAmount.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(moveAmount.x);
                    }

                    collisions.below = directionY == -1;
                    collisions.above = directionY == 1;
                }
            }
        }
        if (collisions.climbingSlope)
        {
            float directionX = Mathf.Sign(moveAmount.x);
            rayLength = Mathf.Abs(moveAmount.x) + skinWidth;
            Vector2 rayOrigin = ((directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight) + Vector2.up * moveAmount.y;

            RaycastHit2D[] hit2 = Physics2D.RaycastAll(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            if (hit2.Length > 0)
            {
                foreach (RaycastHit2D touch in hit2)
                {
                    if (touch.collider.gameObject == gameObject) { continue; }

                    float slopeAngle = Vector2.Angle(touch.normal, Vector2.up);
                    if (slopeAngle != collisions.slopeAngle)
                    {
                        moveAmount.x = (touch.distance - skinWidth) * directionX;
                        collisions.slopeAngle = slopeAngle;
                        collisions.slopeNormal = touch.normal;
                    }
                }
            }
        }
    }
    private void ClimbSlope(ref Vector2 moveAmount, float slopeAngle, Vector2 slopeNormal)
    {
        float moveDistance = Mathf.Abs(moveAmount.x);  //taking how far we would move without the slope and then translating it to the distance travelled on the slope.
        float climbVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

        if (moveAmount.y <= climbVelocityY)
        {
            moveAmount.y = climbVelocityY;
            moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);
            collisions.below = true;
            collisions.climbingSlope = true;
            collisions.slopeAngle = slopeAngle;
            collisions.slopeNormal = slopeNormal;
        }
    }

    void DescendSlope(ref Vector2 moveAmount)
    {
        RaycastHit2D maxSlopeHitLeft = new RaycastHit2D();
        RaycastHit2D maxSlopeHitRight = new RaycastHit2D();

        RaycastHit2D[] maxSlopeHitLeft2 = Physics2D.RaycastAll(raycastOrigins.bottomLeft, Vector2.down, Mathf.Abs(moveAmount.y) + skinWidth, collisionMask);
        RaycastHit2D[] maxSlopeHitRight2 = Physics2D.RaycastAll(raycastOrigins.bottomRight, Vector2.down, Mathf.Abs(moveAmount.y) + skinWidth, collisionMask);

        bool bottomLeftHit = false;
        bool bottomRightHit = false;

        foreach (RaycastHit2D touch in maxSlopeHitLeft2)
        {
            if (touch.collider.gameObject == gameObject)
            {
                continue;
            }
            else
            {
                bottomLeftHit = true;
                maxSlopeHitLeft = touch;
            }
        }
        foreach (RaycastHit2D touch in maxSlopeHitRight2)
        {
            if (touch.collider.gameObject == gameObject)
            {
                continue;
            }
            else
            {
                bottomRightHit = true;
                maxSlopeHitRight = touch;
            }
        }
        // ^ computes the logical exclusive OR, also known as the logical XOR (computes the same result as the inequality operator != ) maxSlopeHitLeft != maxSlopeHitRight
        if (bottomLeftHit ^ bottomRightHit)  // if (maxSlopeHitLeft ^ maxSlopeHitRight) 
        {
            //.Log("xor is true");
            SlideDownMaxSlope(maxSlopeHitLeft, ref moveAmount);
            SlideDownMaxSlope(maxSlopeHitRight, ref moveAmount);
        }


        if (!collisions.slidingDownMaxSlope)
        {
            float directionX = Mathf.Sign(moveAmount.x);
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;

            RaycastHit2D[] hit2 = Physics2D.RaycastAll(rayOrigin, -Vector2.up, Mathf.Infinity, collisionMask);

            if (hit2.Length > 0)
            {
                foreach (RaycastHit2D touch in hit2)
                {
                    if (touch.collider.gameObject == gameObject) { continue; }

                    float slopeAngle = Vector2.Angle(touch.normal, Vector2.up);  // was hit.normal
                    if (slopeAngle != 0 && slopeAngle <= maxSlopeAngle)
                    {
                        if (Mathf.Sign(touch.normal.x) == directionX)  // was hit.normal
                        {
                            if (touch.distance - skinWidthHorizontal <= Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x)) // was hit.distance
                            {
                                float moveDistance = Mathf.Abs(moveAmount.x);
                                float descendVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;
                                moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);
                                moveAmount.y -= descendVelocityY;

                                collisions.slopeAngle = slopeAngle;
                                collisions.descendingSlope = true;
                                collisions.below = true;
                                collisions.slopeNormal = touch.normal; // was hit.normal
                            }
                        }
                    }
                }
            }
        }
    }

    void SlideDownMaxSlope(RaycastHit2D hit, ref Vector2 moveAmount)
    {
        if (hit)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle > maxSlopeAngle)
            {
                moveAmount.x = hit.normal.x * (Mathf.Abs(moveAmount.y) - hit.distance) / Mathf.Tan(slopeAngle * Mathf.Deg2Rad);

                collisions.slopeAngle = slopeAngle;
                collisions.slidingDownMaxSlope = true;
                collisions.slopeNormal = hit.normal;
            }
        }
    }

    void ResetFallingThrouhPlatform()
    {
        collisions.fallingThroughPlatform = false;
    }

    public struct CollisionInfo
    {
        public bool above, below;
        public bool left, right;

        public bool climbingSlope, descendingSlope;
        public bool slidingDownMaxSlope;

        public float slopeAngle, slopeAngleOld;
        public Vector2 slopeNormal;
        public Vector2 moveAmountOld;
        public int faceDir;
        public bool fallingThroughPlatform;

        public void Reset()
        {
            above = below = false;
            left = right = false;
            climbingSlope = false;
            descendingSlope = false;
            slidingDownMaxSlope = false;
            slopeNormal = Vector2.zero;

            slopeAngleOld = slopeAngle;
            slopeAngle = 0;
        }
    }
}
