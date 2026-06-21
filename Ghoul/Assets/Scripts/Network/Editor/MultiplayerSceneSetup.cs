using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

// Run via  Tools > Multiplayer > Setup Scene
// The script will:
//   1. Turn the tagged Player object into a prefab (Assets/Prefabs/Player.prefab)
//   2. Add NetworkObject + ClientNetworkTransform to it
//   3. Create a NetworkManager with UnityTransport, pointing at the player prefab
//   4. Create two spawn-point GameObjects spread apart on the X axis
//   5. Create a PlayerSpawner that uses those spawn points
//   6. Create a canvas with Host / Client / Server buttons
//   7. Remove the original Player from the scene (the spawner re-creates it at runtime)
//
// After running: save the scene with Ctrl+S, then press Play and click Host.
// Open a second Editor instance (Multiplayer Play Mode) or a build and click Client.
public static class MultiplayerSceneSetup
{
    [MenuItem("Tools/Multiplayer/Setup Scene")]
    public static void SetupScene()
    {
        GameObject scenePlayer = GameObject.FindWithTag("Player");
        if (scenePlayer == null)
        {
            EditorUtility.DisplayDialog("Multiplayer Setup", "No GameObject with tag 'Player' found in the active scene.", "OK");
            return;
        }

        // --- 1. Build the Player prefab ---
        if (scenePlayer.GetComponent<NetworkObject>() == null)          { scenePlayer.AddComponent<NetworkObject>(); }
        if (scenePlayer.GetComponent<ClientNetworkTransform>() == null) { scenePlayer.AddComponent<ClientNetworkTransform>(); }
        if (scenePlayer.GetComponent<PlayerColorSync>() == null)        { scenePlayer.AddComponent<PlayerColorSync>(); }
        if (scenePlayer.GetComponent<PlayerAttack>() == null)           { scenePlayer.AddComponent<PlayerAttack>(); }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) { AssetDatabase.CreateFolder("Assets", "Prefabs"); }

        const string prefabPath = "Assets/Prefabs/Player.prefab";
        GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(scenePlayer, prefabPath, out bool prefabOk);
        if (!prefabOk || playerPrefab == null)
        {
            EditorUtility.DisplayDialog("Multiplayer Setup", $"Could not save prefab to {prefabPath}.", "OK");
            return;
        }
        Debug.Log($"[MultiplayerSetup] Player prefab saved → {prefabPath}");

        // Remove scene instance — PlayerSpawner will instantiate it at runtime.
        Object.DestroyImmediate(scenePlayer);

        // --- 2. NetworkManager ---
        NetworkManager existingNM = Object.FindObjectOfType<NetworkManager>();
        if (existingNM == null)
        {
            GameObject nmGO = new GameObject("NetworkManager");
            NetworkManager nm = nmGO.AddComponent<NetworkManager>();
            UnityTransport transport = nmGO.AddComponent<UnityTransport>();

            // Wire transport via SerializedObject so it survives serialization.
            SerializedObject soNM = new SerializedObject(nm);
            SetRef(soNM, "m_NetworkConfig.NetworkTransport", transport);
            soNM.ApplyModifiedProperties();

            Debug.Log("[MultiplayerSetup] NetworkManager created.");
        }
        else
        {
            Debug.Log("[MultiplayerSetup] NetworkManager already exists — skipped.");
        }

        // --- 3. Spawn points ---
        GameObject sp1 = EnsureGameObject("SpawnPoint_P1");
        sp1.transform.position = new Vector3(-3f, 1f, 0f);
        GameObject sp2 = EnsureGameObject("SpawnPoint_P2");
        sp2.transform.position = new Vector3(3f, 1f, 0f);

        // --- 4. PlayerSpawner ---
        PlayerSpawner spawner = Object.FindObjectOfType<PlayerSpawner>();
        if (spawner == null)
        {
            // Attach to the NetworkManager GO so it persists.
            NetworkManager nm2 = Object.FindObjectOfType<NetworkManager>();
            spawner = nm2 != null ? nm2.gameObject.AddComponent<PlayerSpawner>() : new GameObject("PlayerSpawner").AddComponent<PlayerSpawner>();
        }

        SerializedObject soSpawner = new SerializedObject(spawner);
        SetRef(soSpawner, "playerPrefab", playerPrefab);
        SerializedProperty pointsProp = soSpawner.FindProperty("spawnPoints");
        pointsProp.arraySize = 2;
        pointsProp.GetArrayElementAtIndex(0).objectReferenceValue = sp1.transform;
        pointsProp.GetArrayElementAtIndex(1).objectReferenceValue = sp2.transform;
        soSpawner.ApplyModifiedProperties();

        // --- 5. Register prefab with NetworkManager ---
        RegisterNetworkPrefab(playerPrefab);

        // --- 6. Lobby UI ---
        if (Object.FindObjectOfType<NetworkManagerUI>() == null) { BuildLobbyUI(); }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[MultiplayerSetup] Done — save the scene (Ctrl+S), then press Play and click Host.");
    }

    // -------------------------------------------------------------------------

    private static void SetRef(SerializedObject so, string path, Object value)
    {
        SerializedProperty prop = so.FindProperty(path);
        if (prop != null) { prop.objectReferenceValue = value; }
        else              { Debug.LogWarning($"[MultiplayerSetup] Could not find serialized property '{path}' — set it manually in the Inspector."); }
    }

    private static GameObject EnsureGameObject(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) { go = new GameObject(name); }
        return go;
    }

    private static void RegisterNetworkPrefab(GameObject prefab)
    {
        NetworkManager nm = Object.FindObjectOfType<NetworkManager>();
        if (nm == null) { return; }

        // Try the NGO 2.x NetworkPrefabsList path, then fall back to the 1.x list path.
        SerializedObject soNM = new SerializedObject(nm);

        // NGO 2.x: m_NetworkConfig.Prefabs is a NetworkPrefabsList (ScriptableObject ref).
        // Create one if missing, then add the player prefab to it.
        SerializedProperty prefabsListProp = soNM.FindProperty("m_NetworkConfig.Prefabs");
        if (prefabsListProp != null && prefabsListProp.propertyType == SerializedPropertyType.ObjectReference)
        {
            NetworkPrefabsList prefabsList = prefabsListProp.objectReferenceValue as NetworkPrefabsList;
            if (prefabsList == null)
            {
                prefabsList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                string listPath = "Assets/Prefabs/NetworkPrefabs.asset";
                AssetDatabase.CreateAsset(prefabsList, listPath);
                AssetDatabase.SaveAssets();
                prefabsListProp.objectReferenceValue = prefabsList;
                soNM.ApplyModifiedProperties();
            }

            if (!prefabsList.Contains(prefab))
            {
                prefabsList.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(prefabsList);
            }
            return;
        }

        // Fallback: NGO 1.x style flat list.
        SerializedProperty legacyList = soNM.FindProperty("m_NetworkConfig.NetworkPrefabs");
        if (legacyList != null)
        {
            // Check if already registered.
            for (int i = 0; i < legacyList.arraySize; i++)
            {
                if (legacyList.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) { return; }
            }
            legacyList.arraySize++;
            SerializedProperty entry = legacyList.GetArrayElementAtIndex(legacyList.arraySize - 1);
            entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            soNM.ApplyModifiedProperties();
        }
    }

    private static void BuildLobbyUI()
    {
        // EventSystem — required for all UI interaction.
        // The new Input System is already in this project, so use its UI module.
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Canvas
        GameObject canvasGO = new GameObject("NetworkUI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Semi-transparent panel — taller to fit the join-code input field
        GameObject panel = RectChild("ButtonPanel", canvasGO);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.55f);
        SizeAt(panel, new Vector2(220f, 220f), Vector2.zero);

        // Join-code input field (client types the host's code here)
        GameObject inputGO = RectChild("JoinCodeInput", panel);
        InputField inputField = inputGO.AddComponent<InputField>();
        Image inputImg = inputGO.AddComponent<Image>();
        inputImg.color = new Color(1f, 1f, 1f, 0.9f);
        SizeAt(inputGO, new Vector2(180f, 32f), new Vector2(0f, 80f));

        GameObject inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(inputGO.transform, false);
        RectTransform inputTextRT = inputTextGO.AddComponent<RectTransform>();
        inputTextRT.anchorMin = Vector2.zero; inputTextRT.anchorMax = Vector2.one;
        inputTextRT.offsetMin = new Vector2(4f, 0f); inputTextRT.offsetMax = new Vector2(-4f, 0f);
        Text inputText = inputTextGO.AddComponent<Text>();
        inputText.font = BuiltinFont();
        inputText.fontSize = 16;
        inputText.color = Color.black;
        inputField.textComponent = inputText;

        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(inputGO.transform, false);
        RectTransform phRT = placeholderGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(4f, 0f); phRT.offsetMax = new Vector2(-4f, 0f);
        Text placeholder = placeholderGO.AddComponent<Text>();
        placeholder.font = BuiltinFont();
        placeholder.fontSize = 16;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholder.text = "Join code...";
        inputField.placeholder = placeholder;

        // Three buttons
        GameObject hostGO   = MakeButton("Host",   panel, new Vector2(0f,  38f));
        GameObject clientGO = MakeButton("Client", panel, new Vector2(0f, -16f));
        GameObject serverGO = MakeButton("Server", panel, new Vector2(0f, -70f));

        // Status label above the panel
        GameObject statusGO = RectChild("StatusText", canvasGO);
        Text status = statusGO.AddComponent<Text>();
        status.font = BuiltinFont();
        status.fontSize = 14;
        status.alignment = TextAnchor.MiddleCenter;
        status.color = Color.white;
        SizeAt(statusGO, new Vector2(400f, 50f), new Vector2(0f, 140f));

        // Wire NetworkManagerUI
        NetworkManagerUI nmUI = canvasGO.AddComponent<NetworkManagerUI>();
        SerializedObject soUI = new SerializedObject(nmUI);
        soUI.FindProperty("hostButton").objectReferenceValue   = hostGO.GetComponent<Button>();
        soUI.FindProperty("clientButton").objectReferenceValue = clientGO.GetComponent<Button>();
        soUI.FindProperty("serverButton").objectReferenceValue = serverGO.GetComponent<Button>();
        soUI.FindProperty("joinCodeInput").objectReferenceValue = inputField;
        soUI.FindProperty("buttonPanel").objectReferenceValue  = panel;
        soUI.FindProperty("statusText").objectReferenceValue   = status;
        soUI.ApplyModifiedProperties();
    }

    private static GameObject RectChild(string name, GameObject parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void SizeAt(GameObject go, Vector2 size, Vector2 pos)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static GameObject MakeButton(string label, GameObject parent, Vector2 pos)
    {
        GameObject btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(parent.transform, false);
        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 40f);
        rt.anchoredPosition = pos;

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.18f, 0.38f, 0.78f);

        btnGO.AddComponent<Button>();

        // Label
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = label;
        txt.font = BuiltinFont();
        txt.fontSize = 18;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        return btnGO;
    }

    private static Font BuiltinFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        return f;
    }

    // Run via Tools > Multiplayer > Update Player Prefab
    // Adds PlayerColorSync and PlayerAttack to the existing Player.prefab without
    // re-running the full scene setup.
    [MenuItem("Tools/Multiplayer/Update Player Prefab")]
    public static void UpdatePlayerPrefab()
    {
        const string prefabPath = "Assets/Prefabs/Player.prefab";
        using (PrefabUtility.EditPrefabContentsScope scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = scope.prefabContentsRoot;
            if (root.GetComponent<PlayerColorSync>() == null) { root.AddComponent<PlayerColorSync>(); }
            if (root.GetComponent<PlayerAttack>() == null)    { root.AddComponent<PlayerAttack>(); }
        }
        Debug.Log("[MultiplayerSetup] Player prefab updated — PlayerColorSync and PlayerAttack added.");
    }
}
