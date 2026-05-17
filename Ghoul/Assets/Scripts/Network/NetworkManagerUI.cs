using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Minimal lobby UI. Create a Canvas with:
//   - A panel ("buttonPanel") containing three buttons wired to hostButton, clientButton, serverButton
//   - A Text element for statusText
// Hide the panel once a session starts.
public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private GameObject buttonPanel;
    [SerializeField] private Text statusText;

    private void Awake()
    {
        if (hostButton != null)   { hostButton.onClick.AddListener(StartHost); }
        if (clientButton != null) { clientButton.onClick.AddListener(StartClient); }
        if (serverButton != null) { serverButton.onClick.AddListener(StartServer); }
    }

    private void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        HideButtons("Hosting...");
    }

    private void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        HideButtons("Connecting...");
    }

    private void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        HideButtons("Server running...");
    }

    private void HideButtons(string status)
    {
        if (buttonPanel != null) { buttonPanel.SetActive(false); }
        if (statusText != null)  { statusText.text = status; }
    }
}
