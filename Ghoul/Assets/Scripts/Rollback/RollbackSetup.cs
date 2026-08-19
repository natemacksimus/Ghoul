using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Rollback
{
    /// <summary>
    /// Add to the NetworkManager GameObject (or any persistent scene root).
    ///
    /// Waits until both players have spawned, then:
    ///  1. Creates (or finds) a RollbackSession.
    ///  2. Collects ISnapshotable and IRollbackSimulated from each player.
    ///  3. Disables ClientNetworkTransform on all players (rollback drives positions).
    ///  4. Starts the session.
    ///
    /// Assign the two player NetworkObjects in the inspector, or let auto-detection
    /// find them by tag "Player" once both have spawned.
    /// </summary>
    public class RollbackSetup : MonoBehaviour
    {
        [Tooltip("Leave empty to auto-detect spawned players by tag.")]
        [SerializeField] private NetworkObject player0NetworkObject;
        [SerializeField] private NetworkObject player1NetworkObject;

        [Tooltip("Tag used for auto-detection if player references are empty.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Seconds to wait between each auto-detection poll.")]
        [SerializeField] private float pollInterval = 0.25f;

        private void Start()
        {
            StartCoroutine(WaitForPlayersAndStart());
        }

        private IEnumerator WaitForPlayersAndStart()
        {
            // Ensure NetworkManager is running before doing anything.
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                yield return new WaitForSeconds(pollInterval);

            // Poll until both player objects are available and fully spawned.
            NetworkObject p0 = null, p1 = null;
            while (true)
            {
                p0 = player0NetworkObject;
                p1 = player1NetworkObject;

                if (p0 == null || p1 == null)
                {
                    // Auto-detect by tag.
                    var all = GameObject.FindGameObjectsWithTag(playerTag);
                    if (all.Length >= 2)
                    {
                        NetworkObject a = all[0].GetComponent<NetworkObject>();
                        NetworkObject b = all[1].GetComponent<NetworkObject>();
                        if (a != null && b != null && a.IsSpawned && b.IsSpawned)
                        {
                            // Order: index 0 = host's local player (OwnerClientId 0),
                            //        index 1 = joined client's player.
                            p0 = a.OwnerClientId < b.OwnerClientId ? a : b;
                            p1 = a.OwnerClientId < b.OwnerClientId ? b : a;
                        }
                    }
                }

                if (p0 != null && p1 != null && p0.IsSpawned && p1.IsSpawned)
                    break;

                yield return new WaitForSeconds(pollInterval);
            }

            // Wait one frame so PlayerController.Start() can run and assign its
            // component references (controller2D, etc.) before simulation begins.
            yield return null;

            BeginRollbackSession(p0, p1);
        }

        private void BeginRollbackSession(NetworkObject p0Net, NetworkObject p1Net)
        {
            NetworkManager nm = NetworkManager.Singleton;

            // Determine which player index is local.
            int localIdx = (p0Net.IsOwner) ? 0 : 1;

            // Disable ClientNetworkTransform on both players — rollback owns position from now on.
            DisableCNT(p0Net.gameObject);
            DisableCNT(p1Net.gameObject);

            // Collect simulated players and snapshotables from each player GO.
            var players = new IRollbackSimulated[2];
            var snapshots = new List<ISnapshotable>();

            CollectFromPlayer(p0Net.gameObject, 0, players, snapshots);
            CollectFromPlayer(p1Net.gameObject, 1, players, snapshots);

            // Also add InputCapture on the local player.
            GameObject localPlayerGO = localIdx == 0 ? p0Net.gameObject : p1Net.gameObject;
            InputCapture capture = localPlayerGO.GetComponent<InputCapture>();
            if (capture == null) capture = localPlayerGO.AddComponent<InputCapture>();

            // Create or find the RollbackSession.
            RollbackSession session = RollbackSession.Instance;
            if (session == null)
            {
                GameObject go = new GameObject("RollbackSession");
                DontDestroyOnLoad(go);
                session = go.AddComponent<RollbackSession>();
            }

            session.StartSession(nm, localIdx, players, snapshots);
        }

        private static void CollectFromPlayer(GameObject go, int index,
                                               IRollbackSimulated[] players,
                                               List<ISnapshotable> snapshots)
        {
            var sim = go.GetComponent<IRollbackSimulated>();
            if (sim != null) players[index] = sim;
            else Debug.LogWarning($"[RollbackSetup] Player {index} ({go.name}) has no IRollbackSimulated.");

            foreach (var s in go.GetComponents<ISnapshotable>())
                snapshots.Add(s);
        }

        private static void DisableCNT(GameObject go)
        {
            var cnt = go.GetComponent<ClientNetworkTransform>();
            if (cnt != null) cnt.enabled = false;

            // Also disable the base NetworkTransform if present separately.
            var nt = go.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null && nt != cnt) nt.enabled = false;
        }
    }
}
