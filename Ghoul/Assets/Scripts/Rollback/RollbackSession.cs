using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;

namespace Rollback
{
    /// <summary>
    /// Core rollback orchestrator. Replaces ClientNetworkTransform's state-sync with
    /// per-frame input exchange. Lives on the NetworkManager GameObject (or a DontDestroyOnLoad
    /// root added by RollbackSetup) and drives all player simulation after both players spawn.
    ///
    /// FRAME LOOP (runs in FixedUpdate)
    /// ──────────────────────────────────
    /// 1. Capture local input → store in circular buffer → send to remote.
    /// 2. Advance confirmedFrame as far as we have both players' inputs.
    /// 3. If any newly confirmed remote input differs from our saved prediction,
    ///    restore the snapshot at that frame and re-simulate up to currentFrame.
    /// 4. Save snapshot for currentFrame, simulate it speculatively, advance currentFrame.
    ///
    /// NETWORK TRANSPORT
    /// ──────────────────
    /// Uses NGO CustomMessagingManager (Unreliable) so no extra connection is needed —
    /// the existing Relay/NGO session carries the rollback packets.
    /// Each packet bundles the last INPUT_REDUNDANCY frames to survive packet loss.
    /// </summary>
    public class RollbackSession : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────
        public static RollbackSession Instance { get; private set; }

        // ── Settings ─────────────────────────────────────────────────────
        [Header("Rollback")]
        [Tooltip("Maximum frames we can roll back. 8 frames @ 60 Hz = 133 ms.")]
        [SerializeField] private int maxRollbackFrames = 8;
        [Tooltip("How many consecutive input frames to bundle per packet for loss recovery.")]
        [SerializeField] private int inputRedundancy = 3;

        // ── Public state ─────────────────────────────────────────────────
        public bool IsSessionActive { get; private set; }
        public int  CurrentFrame    { get; private set; }
        public int  ConfirmedFrame  { get; private set; } = -1;

        // ── Frame buffers ─────────────────────────────────────────────────
        // Buffer size must exceed maxRollbackFrames * 2 with headroom.
        private const int BufSize = 128;

        private RollbackInput[] _localInputs  = new RollbackInput[BufSize];
        private RollbackInput[] _remoteInputs = new RollbackInput[BufSize];
        // Stores the frame number that confirmed each slot (-1 = unconfirmed).
        // Using frame numbers instead of a bool prevents stale confirmations from a
        // previous buffer cycle (frame N and frame N+BufSize share the same slot index).
        private int[] _remoteConfirmedFrame = new int[BufSize];

        // Per-frame predicted remote input (what we assumed at simulation time)
        private RollbackInput[] _remotePredicted = new RollbackInput[BufSize];

        // State snapshots keyed by frame % BufSize
        private byte[][] _snapshots     = new byte[BufSize][];
        private bool[]   _snapshotValid = new bool[BufSize];

        // ── Registered participants ───────────────────────────────────────
        private List<ISnapshotable>    _snapshotables = new List<ISnapshotable>();
        private IRollbackSimulated[]   _players       = new IRollbackSimulated[2];
        private int _localPlayerIndex = -1;

        // ── Network ───────────────────────────────────────────────────────
        private const string MsgName = "RB";
        private NetworkManager _nm;

        // ── Unity lifecycle ───────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnregisterNetworkHandlers();
        }

        private void FixedUpdate()
        {
            if (!IsSessionActive) return;
            TickFrame();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Call after both players have spawned and their components are ready.
        /// localPlayerIndex: 0 = host player, 1 = joined client player.
        /// </summary>
        public void StartSession(NetworkManager nm,
                                 int localPlayerIndex,
                                 IRollbackSimulated[] players,
                                 List<ISnapshotable> snapshotables)
        {
            _nm                = nm;
            _localPlayerIndex  = localPlayerIndex;
            _players           = players;
            _snapshotables     = snapshotables;
            CurrentFrame       = 0;
            ConfirmedFrame     = -1;

            for (int i = 0; i < BufSize; i++) _remoteConfirmedFrame[i] = -1;
            Array.Clear(_snapshotValid, 0, BufSize);

            RegisterNetworkHandlers();
            IsSessionActive = true;
            Debug.Log($"[Rollback] Session started. Local player index: {localPlayerIndex}");
        }

        public void StopSession()
        {
            IsSessionActive = false;
            UnregisterNetworkHandlers();
            Debug.Log("[Rollback] Session stopped.");
        }

        public void RegisterSnapshotable(ISnapshotable s)
        {
            if (!_snapshotables.Contains(s)) _snapshotables.Add(s);
        }

        public void UnregisterSnapshotable(ISnapshotable s) => _snapshotables.Remove(s);

        // ── Frame tick ───────────────────────────────────────────────────

        private void TickFrame()
        {
            int frame = CurrentFrame;

            // 1. Gather local input and send to remote.
            RollbackInput localInput = GatherLocalInput();
            _localInputs[frame % BufSize] = localInput;
            SendInputToRemote(frame, localInput);

            // 2. Advance confirmed frame pointer.
            AdvanceConfirmedFrame();

            // 3. Check for mispredictions and rollback if needed.
            int rollbackTo = FindEarliestMisprediction();
            if (rollbackTo >= 0)
            {
                PerformRollback(rollbackTo);
            }

            // 4. Simulate current frame speculatively and advance.
            SaveSnapshot(frame);
            SimulateFrame(frame);
            CurrentFrame++;
        }

        // ── Input handling ────────────────────────────────────────────────

        private RollbackInput GatherLocalInput()
        {
            return InputCapture.Current != null
                ? InputCapture.Current.GetAndClearFrame()
                : default;
        }

        private void SendInputToRemote(int frame, RollbackInput input)
        {
            if (_nm == null || !_nm.IsConnectedClient && !_nm.IsServer) return;
            ulong remoteId = GetRemoteClientId();
            if (remoteId == ulong.MaxValue) return;

            // Bundle the last inputRedundancy frames in case earlier packets were dropped.
            int count = Mathf.Min(inputRedundancy, frame + 1);
            // Header: frame(4) + count(1) = 5 bytes; each input = 10 bytes.
            int msgSize = 5 + count * 10;

            using var writer = new FastBufferWriter(msgSize, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(frame);
            writer.WriteValueSafe((byte)count);
            for (int i = 0; i < count; i++)
            {
                int f = frame - i;
                RollbackInput inp = _localInputs[f % BufSize];
                writer.WriteValueSafe(inp.Buttons);
                writer.WriteValueSafe(inp.MoveX); writer.WriteValueSafe(inp.MoveY);
                writer.WriteValueSafe(inp.AimX);  writer.WriteValueSafe(inp.AimY);
            }

            _nm.CustomMessagingManager.SendNamedMessage(
                MsgName, remoteId, writer, NetworkDelivery.Unreliable);
        }

        private void OnRemoteInputReceived(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int latestFrame);
            reader.ReadValueSafe(out byte count);

            for (int i = 0; i < count; i++)
            {
                int f = latestFrame - i;
                if (f < 0) break;
                // Don't overwrite an already-confirmed frame with redundant data.
                int idx = f % BufSize;
                if (_remoteConfirmedFrame[idx] != f)
                {
                    RollbackInput inp = default;
                    reader.ReadValueSafe(out inp.Buttons);
                    reader.ReadValueSafe(out inp.MoveX); reader.ReadValueSafe(out inp.MoveY);
                    reader.ReadValueSafe(out inp.AimX);  reader.ReadValueSafe(out inp.AimY);
                    _remoteInputs[idx]         = inp;
                    _remoteConfirmedFrame[idx] = f;
                }
                else
                {
                    // Skip the bytes for this frame (already confirmed, discard).
                    reader.ReadValueSafe(out ushort _b);
                    reader.ReadValueSafe(out short  _mx); reader.ReadValueSafe(out short _my);
                    reader.ReadValueSafe(out short  _ax); reader.ReadValueSafe(out short _ay);
                }
            }
        }

        private RollbackInput PredictRemoteInput(int frame)
        {
            // Classic GGPO prediction: assume remote repeats last confirmed input.
            if (ConfirmedFrame < 0) return default;
            return _remoteInputs[ConfirmedFrame % BufSize];
        }

        // ── Confirmed frame tracking ──────────────────────────────────────

        private void AdvanceConfirmedFrame()
        {
            int next = ConfirmedFrame + 1;
            while (next <= CurrentFrame && _remoteConfirmedFrame[next % BufSize] == next)
            {
                ConfirmedFrame = next;
                next++;
            }
        }

        // Returns the earliest frame index where our saved prediction differs from the
        // now-confirmed remote input. Returns -1 if no rollback is needed.
        private int FindEarliestMisprediction()
        {
            int earliest = -1;
            int oldest = Mathf.Max(0, CurrentFrame - maxRollbackFrames + 1);

            for (int f = oldest; f <= ConfirmedFrame; f++)
            {
                int idx = f % BufSize;
                if (_remoteConfirmedFrame[idx] != f) continue;
                if (!_remotePredicted[idx].Equals(_remoteInputs[idx]))
                {
                    earliest = f;
                    break;
                }
            }
            return earliest;
        }

        // ── Rollback ─────────────────────────────────────────────────────

        private void PerformRollback(int toFrame)
        {
            if (!_snapshotValid[toFrame % BufSize])
            {
                Debug.LogWarning($"[Rollback] No snapshot for frame {toFrame}, skipping rollback.");
                return;
            }

            LoadSnapshot(toFrame);

            // Re-simulate from toFrame up to (but not including) the current frame.
            for (int f = toFrame; f < CurrentFrame; f++)
            {
                SimulateFrame(f);
                SaveSnapshot(f + 1); // overwrite the speculative snapshot ahead
            }
        }

        // ── Simulation ───────────────────────────────────────────────────

        private void SimulateFrame(int frame)
        {
            int localIdx  = _localPlayerIndex;
            int remoteIdx = 1 - localIdx;

            RollbackInput localInput = _localInputs[frame % BufSize];

            int ri = frame % BufSize;
            RollbackInput remoteInput;
            if (_remoteConfirmedFrame[ri] == frame)
                remoteInput = _remoteInputs[ri];
            else
            {
                remoteInput = PredictRemoteInput(frame);
                _remotePredicted[ri] = remoteInput; // save prediction for later comparison
            }

            RollbackInput p0 = localIdx == 0 ? localInput : remoteInput;
            RollbackInput p1 = localIdx == 1 ? localInput : remoteInput;

            _players[0]?.SimulateFrame(p0);
            _players[1]?.SimulateFrame(p1);
        }

        // ── Snapshot ─────────────────────────────────────────────────────

        private void SaveSnapshot(int frame)
        {
            using var ms = new MemoryStream(512);
            using var w  = new BinaryWriter(ms);
            foreach (var s in _snapshotables)
            {
                // Skip (and stop) if a registered component was destroyed under us.
                if (s is UnityEngine.Object obj && !obj) { StopSession(); return; }
                s.SaveState(w);
            }
            _snapshots[frame % BufSize]     = ms.ToArray();
            _snapshotValid[frame % BufSize] = true;
        }

        private void LoadSnapshot(int frame)
        {
            byte[] data = _snapshots[frame % BufSize];
            if (data == null) return;
            using var ms = new MemoryStream(data);
            using var r  = new BinaryReader(ms);
            foreach (var s in _snapshotables) s.LoadState(r);
        }

        // ── Networking helpers ────────────────────────────────────────────

        private void RegisterNetworkHandlers()
        {
            if (_nm?.CustomMessagingManager == null) return;
            _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgName, OnRemoteInputReceived);
        }

        private void UnregisterNetworkHandlers()
        {
            if (_nm?.CustomMessagingManager == null) return;
            _nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgName);
        }

        private ulong GetRemoteClientId()
        {
            if (_nm == null) return ulong.MaxValue;
            foreach (ulong id in _nm.ConnectedClientsIds)
            {
                if (id != _nm.LocalClientId) return id;
            }
            return ulong.MaxValue;
        }
    }
}
