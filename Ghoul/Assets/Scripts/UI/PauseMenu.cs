using Rollback;
using UnityEngine;

// Minimal pause / map UI toggle. Wire the panel (the pause menu or map canvas) in the
// inspector; PlayerController.OpenMenu calls Toggle() on the Pause input action.
//
// During an online rollback session the menu is an overlay only: freezing Time.timeScale
// would stop this peer's FixedUpdate (and so its rollback tick) while the other peer keeps
// simulating, opening a frame gap that rollback can't correct. Instead, gameplay input is
// blocked (neutral frames sent) while the menu is open. Offline, it still pauses time.
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Panel / map canvas shown while paused. Hidden on start.")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Freeze game time (Time.timeScale = 0) while the menu is open. Ignored during an online rollback session.")]
    [SerializeField] private bool pauseTime = true;

    public bool IsOpen { get; private set; }

    // Only restore timeScale if this menu is the one that froze it.
    private bool frozeTime;

    private void Start()
    {
        if (pausePanel != null) { pausePanel.SetActive(false); }
        IsOpen = false;
    }

    private void OnDestroy()
    {
        if (IsOpen) { SetOpen(false); }
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (pausePanel != null) { pausePanel.SetActive(open); }

        InputCapture.SetInputBlocked(open);

        bool rollbackActive = RollbackSession.Instance != null && RollbackSession.Instance.IsSessionActive;
        if (open && pauseTime && !rollbackActive)
        {
            Time.timeScale = 0f;
            frozeTime = true;
        }
        else if (!open && frozeTime)
        {
            Time.timeScale = 1f;
            frozeTime = false;
        }
    }
}
