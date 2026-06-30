using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main-menu controller: 5 world save slots plus a join-by-code panel.
//
//  * Empty slot  -> "Create": make a new world, host it, load the world scene.
//  * Used slot   -> "Play":   restore the saved world, host it, load the world scene.
//                   "Delete": remove the save file and refresh the row.
//  * Join panel  -> connect to a friend's hosted world as a client (no save slot).
//
// The canvas + wiring is built by Tools/World/Setup Main Menu. The slot arrays are
// parallel (index 0..4): one entry per row.
public class MainMenuUI : MonoBehaviour
{
    [Header("Slot rows (size 5)")]
    [SerializeField] private Text[] slotLabels;
    [SerializeField] private Button[] slotPrimaryButtons;
    [SerializeField] private Text[] slotPrimaryLabels;
    [SerializeField] private Button[] slotDeleteButtons;

    [Header("Join panel")]
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private Button joinButton;

    [Header("Feedback")]
    [SerializeField] private GameObject codeDisplay;
    [SerializeField] private Text codeText;
    [SerializeField] private Text statusText;

    [SerializeField] private int maxPlayers = 4;

    private bool busy;

    private void Start()
    {
        if (codeDisplay != null) { codeDisplay.SetActive(false); }
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => _ = JoinWorld());
        }
        RefreshSlots();
    }

    // -------------------------------------------------------------------------
    // Slot rows
    // -------------------------------------------------------------------------

    private void RefreshSlots()
    {
        SlotSummary[] slots = SaveSystem.GetAllSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            int slot = i; // capture for closures
            SlotSummary s = slots[i];

            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
            {
                slotLabels[i].text = s.used ? $"{s.worldName}\n{FormatStamp(s.lastPlayedUtc)}" : "— Empty —";
            }

            if (slotPrimaryLabels != null && i < slotPrimaryLabels.Length && slotPrimaryLabels[i] != null)
            {
                slotPrimaryLabels[i].text = s.used ? "Play" : "Create";
            }

            if (slotPrimaryButtons != null && i < slotPrimaryButtons.Length && slotPrimaryButtons[i] != null)
            {
                Button b = slotPrimaryButtons[i];
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => _ = EnterWorld(slot, isNew: !s.used));
            }

            if (slotDeleteButtons != null && i < slotDeleteButtons.Length && slotDeleteButtons[i] != null)
            {
                Button d = slotDeleteButtons[i];
                d.gameObject.SetActive(s.used);
                d.onClick.RemoveAllListeners();
                d.onClick.AddListener(() => DeleteSlot(slot));
            }
        }
    }

    private void DeleteSlot(int slot)
    {
        if (busy) { return; }
        SaveSystem.Delete(slot);
        RefreshSlots();
        SetStatus($"Deleted slot {slot + 1}.");
    }

    // -------------------------------------------------------------------------
    // Host a world (create or restore)
    // -------------------------------------------------------------------------

    private async Task EnterWorld(int slot, bool isNew)
    {
        if (busy) { return; }

        WorldSaveData world = isNew
            ? WorldSaveData.CreateNew(slot, null, Vector3.zero)
            : SaveSystem.Load(slot);

        if (world == null)
        {
            SetStatus($"Slot {slot + 1} could not be loaded.");
            return;
        }

        if (!isNew) { world.slotIndex = slot; }

        if (NetworkManager.Singleton == null)
        {
            SetStatus("No NetworkManager found.\nRun Tools/World/Setup Save System and play from MainMenu.");
            return;
        }

        busy = true;
        SetInteractable(false);
        SetStatus(isNew ? "Creating world..." : "Loading world...");

        try
        {
            GameSession.Instance.BeginHosting(world, isNew);

            string code = await RelayConnector.CreateSessionAndHost(maxPlayers);
            GameSession.Instance.SetJoinCode(code);

            if (codeDisplay != null) { codeDisplay.SetActive(true); }
            if (codeText != null) { codeText.text = code; }
            SetStatus(string.Empty);

            // CreateSessionAndHost only returns once the host is fully started and
            // synchronized, so the network scene manager is ready for a networked load.
            SceneEventProgressStatus status =
                NetworkManager.Singleton.SceneManager.LoadScene(world.startScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"MainMenuUI: LoadScene('{world.startScene}') returned {status}.");
                SetStatus($"World load failed: {status}");
            }
        }
        catch (Exception e)
        {
            GameSession.Instance.Clear();
            _ = RelayConnector.LeaveActiveSession();
            SetStatus($"Host failed:\n{e.Message}");
            SetInteractable(true);
            busy = false;
        }
    }

    // -------------------------------------------------------------------------
    // Join a friend's world by code (client)
    // -------------------------------------------------------------------------

    private async Task JoinWorld()
    {
        if (busy) { return; }

        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a join code first.");
            return;
        }

        busy = true;
        SetInteractable(false);
        SetStatus("Joining...");

        try
        {
            GameSession.Instance.BeginJoining();
            await RelayConnector.JoinByCodeAndConnect(code);
            // The host's networked scene management syncs this client into the world.
            SetStatus("Connected — entering world...");
        }
        catch (Exception e)
        {
            GameSession.Instance.Clear();
            _ = RelayConnector.LeaveActiveSession();
            SetStatus($"Join failed:\n{e.Message}");
            SetInteractable(true);
            busy = false;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetInteractable(bool on)
    {
        if (slotPrimaryButtons != null) { foreach (Button b in slotPrimaryButtons) { if (b != null) { b.interactable = on; } } }
        if (slotDeleteButtons != null) { foreach (Button b in slotDeleteButtons) { if (b != null) { b.interactable = on; } } }
        if (joinButton != null) { joinButton.interactable = on; }
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) { statusText.text = msg; }
    }

    private static string FormatStamp(string utc)
    {
        if (DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            return dt.ToLocalTime().ToString("g");
        }
        return string.Empty;
    }
}
