using UnityEngine;

// Minimal pause / map UI toggle. Wire the panel (the pause menu or map canvas) in the
// inspector; PlayerController.OpenMenu calls Toggle() on the Pause input action.
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Panel / map canvas shown while paused. Hidden on start.")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Freeze game time (Time.timeScale = 0) while the menu is open.")]
    [SerializeField] private bool pauseTime = true;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        if (pausePanel != null) { pausePanel.SetActive(false); }
        IsOpen = false;
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (pausePanel != null) { pausePanel.SetActive(open); }
        if (pauseTime) { Time.timeScale = open ? 0f : 1f; }
    }
}
