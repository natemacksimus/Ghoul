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

        // Set by UI (e.g. PauseMenu) while a menu owns the controls. Static so it also
        // applies to an InputCapture that RollbackSetup adds after the menu was opened.
        // While blocked, frames are neutral (no buttons, zero axes) but axes keep tracking
        // the real stick so a direction still held on close takes effect immediately
        // (the Input System won't re-fire Move for an unchanged value).
        public static bool InputBlocked { get; private set; }

        private bool _invLeftArmed;
        private bool _invRightArmed;

        private void OnEnable()  { Current = this; }
        private void OnDisable() { if (Current == this) Current = null; }

        public static void SetInputBlocked(bool blocked)
        {
            if (InputBlocked == blocked) return;
            InputBlocked = blocked;
            if (Current != null)
            {
                // Drop presses made while blocked and any inventory hold in progress, so
                // nothing buffered in the menu fires on the first frame after closing.
                Current._pending.Buttons = 0;
                Current._invLeftArmed  = false;
                Current._invRightArmed = false;
            }
        }

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

        public void OnJumpDown()    => Press(RollbackInput.BtnJump);
        public void OnJumpUp()      => Press(RollbackInput.BtnJumpRelease);
        public void OnUseRight()    => Press(RollbackInput.BtnUseRight);
        public void OnUseLeft()     => Press(RollbackInput.BtnUseLeft);
        public void OnDodge()       => Press(RollbackInput.BtnDodge);
        public void OnInteract()    => Press(RollbackInput.BtnInteract);
        public void OnPickup()      => Press(RollbackInput.BtnPickup);

        private void Press(ushort bit)
        {
            if (!InputBlocked) _pending.Buttons |= bit;
        }

        // Tap vs hold: PlayerInput sends started/canceled; InputCapture resolves
        // them into a cycle bit or a drop bit. A hold only counts if it both started
        // and ended while input was unblocked.
        public void OnInvLeftStarted()
        {
            _invLeftHoldStart = Time.unscaledTime;
            _invLeftArmed = !InputBlocked;
        }
        public void OnInvLeftCanceled()
        {
            if (!_invLeftArmed || InputBlocked) return;
            _invLeftArmed = false;
            float held = Time.unscaledTime - _invLeftHoldStart;
            if (held >= inventoryDropHoldTime) _pending.Buttons |= RollbackInput.BtnDropLeft;
            else                               _pending.Buttons |= RollbackInput.BtnInvLeft;
        }

        public void OnInvRightStarted()
        {
            _invRightHoldStart = Time.unscaledTime;
            _invRightArmed = !InputBlocked;
        }
        public void OnInvRightCanceled()
        {
            if (!_invRightArmed || InputBlocked) return;
            _invRightArmed = false;
            float held = Time.unscaledTime - _invRightHoldStart;
            if (held >= inventoryDropHoldTime) _pending.Buttons |= RollbackInput.BtnDropRight;
            else                               _pending.Buttons |= RollbackInput.BtnInvRight;
        }

        // ── Called by RollbackSession ────────────────────────────────────

        /// <summary>
        /// Returns the accumulated input for the current frame and resets for the next.
        /// Analog axes are NOT cleared (they carry their last value until changed).
        /// Returns a neutral frame while input is blocked.
        /// </summary>
        public RollbackInput GetAndClearFrame()
        {
            RollbackInput frame = InputBlocked ? default : _pending;
            // Clear only buttons (events); keep analog axes as "last known"
            _pending.Buttons = 0;
            return frame;
        }
    }
}
