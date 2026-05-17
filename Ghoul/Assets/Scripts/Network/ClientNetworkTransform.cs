using Unity.Netcode.Components;
using UnityEngine;

// Drop this component onto the player prefab instead of NetworkTransform.
// The owner client sends position updates; other clients interpolate.
// Required because Controller2D drives transform.position directly (not Rigidbody).
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
