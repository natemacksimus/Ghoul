using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

// Hands the local player to the scene's Cinemachine camera as its Follow target.
// Only the owning machine does this: in online co-op every client runs its own
// instance with one CinemachineBrain on the Main Camera, so each one points its
// vcam at its own local player. Remote copies of the prefab leave the camera alone.
//
// Place this on the player prefab. The scene must contain:
//   - a Main Camera with a CinemachineBrain component
//   - one CinemachineCamera (vcam) with a CinemachinePositionComposer
public class PlayerCinemachineTarget : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam == null)
        {
            Debug.LogWarning("PlayerCinemachineTarget: no CinemachineCamera found in the scene to follow this player.");
            return;
        }

        vcam.Follow = transform;
    }
}
