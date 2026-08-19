using UnityEngine;

namespace Rollback
{
    /// <summary>
    /// Sits on the local player and accumulates Unity Input System events between
    /// rollback frames. RollbackSession calls GetAndClearFrame() once per FixedUpdate
    /// to drain the buffer into a RollbackInput for that frame.
    ///
    /// Button bits are OR-accumulated (any press within the window is captured).
    /// Analog axes take the most recent value.
    ///
    /// PlayerInput routes its callbacks here instead of directly into PlayerController
    /// when a rollback session is active.
    /// </summary>
    public class InputCapture : MonoBehaviour
    {
        public static InputCapture Current { get; private set; }

        private RollbackInput _pending;

        // Inventory hold tracking (tap vs hold distinction happens in InputCapture,
        // then the resolved event — cycle or drop — is encoded as a single bit).
        [SerializeField] private float inventoryDropHoldTime = 0.4f;
        private float _invLeftHoldStart;
        private float _invRightHoldStart;

        private void OnEnable()  { Current = this; }
        private void OnDisable() { if (Current == this) Current = null; }

        // ── Called from PlayerInput callbacks ────────────────────────────

        public void OnMove(Vector2 move)
        {
            _pending.MoveX = RollbackInput.EncodeAxis(move.x);
            _pending.MoveY = RollbackInput.EncodeAxis(move.y);
        }

        public void OnAim(Vector2 aim)
        {
            _pending.AimX = RollbackInput.EncodeAxis(aim.x);
            _pending.AimY = RollbackInput.EncodeAxis(aim.y);
        }

        public void OnJumpDown()    => _pending.Buttons |= RollbackInput.BtnJump;
        public void OnJumpUp()      => _pending.Buttons |= RollbackInput.BtnJumpRelease;
        public void OnUseRight()    => _pending.Buttons |= RollbackInput.BtnUseRight;
        public void OnUseLeft()     => _pending.Buttons |= RollbackInput.BtnUseLeft;
        public void OnDodge()       => _pending.Buttons |= RollbackInput.BtnDodge;
        public void OnInteract()    => _pending.Buttons |= RollbackInput.BtnInteract;
        public void OnPickup()      => _pending.Buttons |= RollbackInput.BtnPickup;

        // Tap vs hold: PlayerInput sends started/canceled; InputCapture resolves
        // them into a cycle bit or a drop bit.
        public void OnInvLeftStarted()
        {
            _invLeftHoldStart = Time.unscaledTime;
        }
        public void OnInvLeftCanceled()
        {
            float held = Time.unscaledTime - _invLeftHoldStart;
            if (held >= inventoryDropHoldTime) _pending.Buttons |= RollbackInput.BtnDropLeft;
            else                               _pending.Buttons |= RollbackInput.BtnInvLeft;
        }

        public void OnInvRightStarted()
        {
            _invRightHoldStart = Time.unscaledTime;
        }
        public void OnInvRightCanceled()
        {
            float held = Time.unscaledTime - _invRightHoldStart;
            if (held >= inventoryDropHoldTime) _pending.Buttons |= RollbackInput.BtnDropRight;
            else                               _pending.Buttons |= RollbackInput.BtnInvRight;
        }

        // ── Called by RollbackSession ────────────────────────────────────

        /// <summary>
        /// Returns the accumulated input for the current frame and resets for the next.
        /// Analog axes are NOT cleared (they carry their last value until changed).
        /// </summary>
        public RollbackInput GetAndClearFrame()
        {
            RollbackInput frame = _pending;
            // Clear only buttons (events); keep analog axes as "last known"
            _pending.Buttons = 0;
            return frame;
        }
    }
}
