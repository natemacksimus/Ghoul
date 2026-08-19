using Unity.Netcode.Components;
using UnityEngine;

// Drop this component onto the player prefab instead of NetworkTransform.
// The owner client sends position updates; other clients receive them.
// Required because Controller2D drives transform.position directly (not Rigidbody).
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;

    protected override void Awake()
    {
        base.Awake();
        // Snap remote players to the received position each tick instead of smoothly
        // lerping toward it. Lerping makes jumps look like slow floaty acceleration
        // and makes stops look like the player slides to a halt.
        Interpolate = false;

        // Carry the facing direction (localScale.x sign) in the same sync packet as
        // position so the sprite flip arrives at the same time as the movement update,
        // rather than one extra server round-trip later via a NetworkVariable.
        SyncScaleX = true;

        // Send every position and scale change without dead-band suppression so
        // small movements (e.g. landing adjustment) are never silently dropped.
        PositionThreshold = 0f;
        ScaleThreshold = 0f;
    }
}
