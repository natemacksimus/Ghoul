using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RaycastController : MonoBehaviour
{
    public LayerMask collisionMask;

    public const float skinWidth = 0.015f;  //.25f worked, before  0.015f
    public float skinWidthHorizontal = 0.20f;

    const float dstBetweenRays = .15f;

    [HideInInspector] public int horizontalRayCount;  // rays shooting out in front of the player
                                                      //[HideInInspector] public int horizontalBackRayCount;  // rays shooting out behind the player
    [HideInInspector] public int verticalRayCount;

    [HideInInspector] public float horizontalRaySpacing;
    [HideInInspector] public float verticalRaySpacing;

    [HideInInspector] public BoxCollider2D myCollider;
    [HideInInspector] public RaycastOrigins raycastOrigins;

    public virtual void Awake()
    {
        myCollider = GetComponent<BoxCollider2D>();
    }

    public virtual void Start()
    {
        CalculateRaySpacing();
    }

    // Updating the raycast origin points based on the attached collider's bounds.
    public void UpdateRaycastOrigins()
    {
        Bounds bounds = myCollider.bounds;
        bounds.Expand(new Vector2(skinWidthHorizontal * -2, skinWidth * -2));  // contracting the attached collider's bounding box on x and y axes

        raycastOrigins.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        raycastOrigins.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        raycastOrigins.topLeft = new Vector2(bounds.min.x, bounds.max.y);
        raycastOrigins.topRight = new Vector2(bounds.max.x, bounds.max.y);
    }
    public void CalculateRaySpacing()
    {
        Bounds bounds = myCollider.bounds;

        // includes this following line to fix an issues with collision when falling through platforms.  Should test without it to see if it still persists
        // theoretically, the skinWidth should be small (e.g. 0.02), but this formula was used to test collisions from the back of the sprite looking forward.
        skinWidthHorizontal = bounds.size.x - skinWidth;  // sets the skinWidthHorizontal just short of the width of the bounds.  Could use a value other than skinWidth

        bounds.Expand(new Vector2(skinWidthHorizontal * -2, skinWidth * -2));  // contracting the attached collider's bounding box on x and y axes

        float boundsWidth = bounds.size.x;
        float boundsHeight = bounds.size.y;

        horizontalRayCount = Mathf.Abs(Mathf.RoundToInt(boundsHeight / dstBetweenRays));
        verticalRayCount = Mathf.Abs(Mathf.RoundToInt(boundsWidth / dstBetweenRays));

        horizontalRayCount = Mathf.Clamp(horizontalRayCount, 2, int.MaxValue);
        verticalRayCount = Mathf.Clamp(verticalRayCount, 2, int.MaxValue);

        //horizontalBackRayCount = horizontalRayCount;

        horizontalRaySpacing = bounds.size.y / (horizontalRayCount - 1);
        verticalRaySpacing = bounds.size.x / (verticalRayCount - 1);
    }
    public struct RaycastOrigins
    {
        public Vector2 topLeft, topRight;
        public Vector2 bottomLeft, bottomRight;
    }
}
