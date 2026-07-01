using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the world-save / main-menu scaffolding so the system is playable without
// manual scene authoring. Mirrors the approach in MultiplayerSceneSetup.cs.
//
//   Tools > World > Setup Save System
//     1. Creates sample NPC/Building/Resource prefabs + a WorldObjectCatalog asset.
//     2. Builds Assets/Scenes/World_Main.unity (geometry, start point, Exit World UI).
//     3. Builds Assets/Scenes/MainMenu.unity (persistent NetworkManager + GameSession +
//        PlayerSpawner + WorldObjectRegistry + WorldLoader, and the 5-slot menu UI).
//     4. Registers all networked prefabs and adds both scenes to Build Settings.
//
// After running: open MainMenu and press Play. The Player prefab from
// Tools/Multiplayer/Setup Scene (Assets/Prefabs/Player.prefab) is reused if present.
public static class WorldSetup
{
    private const string ScenesDir = "Assets/Scenes";
    private const string PrefabsDir = "Assets/Prefabs";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string WorldScenePath = "Assets/Scenes/World_Main.unity";
    private const string CatalogPath = "Assets/Prefabs/WorldObjectCatalog.asset";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string GridPrefabPath = "Assets/Prefabs/Grid.prefab";
    private const string WorldSceneName = "World_Main";

    [MenuItem("Tools/World/Setup Save System")]
    public static void SetupAll()
    {
        EnsureFolders();

        WorldObjectCatalog catalog = EnsureWorldContent(out _);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogWarning("[WorldSetup] Assets/Prefabs/Player.prefab not found. Run Tools/Multiplayer/Setup Scene first, " +
                             "then re-run this so players can spawn.");
        }
        else
        {
            EnsurePlayerCameraTarget();
        }

        BuildWorldScene();
        BuildMainMenuScene(catalog, playerPrefab);
        AddScenesToBuildSettings();

        // Always boot MainMenu when entering Play mode (main editor + MPPM virtual
        // players), regardless of which scene is currently open in the editor.
        SceneAsset menuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
        EditorSceneManager.playModeStartScene = menuAsset;

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("World Setup",
            "Created:\n" +
            "  • Assets/Scenes/MainMenu.unity\n" +
            "  • Assets/Scenes/World_Main.unity\n" +
            "  • Sample NPC/Building/Resource prefabs + WorldObjectCatalog\n\n" +
            "Play Mode Start Scene is set to MainMenu — just press Play.\n" +
            "(Reset it via Edit > Project Settings, or File > Build Settings, if needed.)",
            "OK");
    }

    // -------------------------------------------------------------------------
    // Content: sample prefabs + catalog
    // -------------------------------------------------------------------------

    private static WorldObjectCatalog EnsureWorldContent(out List<GameObject> prefabs)
    {
        prefabs = new List<GameObject>();

        GameObject npc = EnsureEntityPrefab<NpcEntity>("WO_Npc", "npc_basic", new Color(0.3f, 1f, 0.4f));
        GameObject building = EnsureEntityPrefab<BuildingEntity>("WO_Building", "building_basic", new Color(0.7f, 0.7f, 0.75f));
        GameObject resource = EnsureEntityPrefab<ResourceNode>("WO_Resource", "resource_basic", new Color(1f, 0.85f, 0.2f));
        prefabs.Add(npc);
        prefabs.Add(building);
        prefabs.Add(resource);

        WorldObjectCatalog catalog = AssetDatabase.LoadAssetAtPath<WorldObjectCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WorldObjectCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty entries = so.FindProperty("entries");
        entries.arraySize = 3;
        SetCatalogEntry(entries.GetArrayElementAtIndex(0), "npc_basic", npc);
        SetCatalogEntry(entries.GetArrayElementAtIndex(1), "building_basic", building);
        SetCatalogEntry(entries.GetArrayElementAtIndex(2), "resource_basic", resource);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);

        return catalog;
    }

    private static void SetCatalogEntry(SerializedProperty element, string typeId, GameObject prefab)
    {
        element.FindPropertyRelative("typeId").stringValue = typeId;
        element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
    }

    // Creates (or overwrites) a small visible networked prefab carrying a saveable entity.
    private static GameObject EnsureEntityPrefab<T>(string name, string typeId, Color color) where T : SaveableWorldEntity
    {
        string path = $"{PrefabsDir}/{name}.prefab";

        GameObject go = new GameObject(name);
        go.AddComponent<NetworkObject>();

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sr.color = color;

        T entity = go.AddComponent<T>();
        SerializedObject so = new SerializedObject(entity);
        SerializedProperty typeIdProp = so.FindProperty("typeId");
        if (typeIdProp != null) { typeIdProp.stringValue = typeId; }
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // -------------------------------------------------------------------------
    // World scene
    // -------------------------------------------------------------------------

    private static void BuildWorldScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera + Cinemachine. The brain on the Main Camera is driven by a single
        // follow vcam; each owning client's PlayerCinemachineTarget assigns its own
        // local player as the Follow target at runtime (Follow left empty here).
        Camera cam = AddCamera();
        cam.gameObject.AddComponent<CinemachineBrain>();

        GameObject vcamGO = new GameObject("FollowCamera");
        CinemachineCamera vcam = vcamGO.AddComponent<CinemachineCamera>();
        vcam.Lens.OrthographicSize = 6f;
        CinemachinePositionComposer composer = vcamGO.AddComponent<CinemachinePositionComposer>();
        composer.CameraDistance = 10f;                       // keep camera on the 2D plane
        composer.Damping = new Vector3(0.5f, 0.5f, 0f);      // smooth follow

        // Level geometry: instantiate the Grid prefab as a prefab instance so edits to
        // Assets/Prefabs/Grid still propagate into worlds created by this tool.
        GameObject gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GridPrefabPath);
        if (gridPrefab != null)
        {
            GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab);
            grid.transform.position = Vector3.zero;
        }
        else
        {
            Debug.LogWarning($"[WorldSetup] {GridPrefabPath} not found — world scene has no level geometry.");
        }

        // Per-world default player start.
        GameObject start = new GameObject("WorldStartPoint");
        start.transform.position = new Vector3(0f, 0f, 0f);
        start.AddComponent<WorldStartPoint>();

        // UI: Exit World button + join-code text.
        EnsureEventSystem();
        GameObject canvasGO = MakeCanvas("WorldUI");

        GameObject codeGO = MakeText("JoinCodeText", canvasGO, "", 18, TextAnchor.MiddleLeft);
        Anchor(codeGO, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, 30f), new Vector2(120f, -24f));
        codeGO.GetComponent<Text>().color = new Color(0.8f, 0.95f, 1f);

        GameObject exitGO = MakeButton("ExitWorldButton", canvasGO, "Exit World", out Button exitButton, out _);
        Anchor(exitGO, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(150f, 40f), new Vector2(-85f, -28f));

        // Full-screen "Reconnecting…" overlay, hidden until a client drops its
        // connection (WorldSessionController toggles it during reconnect attempts).
        GameObject overlayGO = RectChild("ReconnectingOverlay", canvasGO);
        overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        Stretch(overlayGO, 0f);
        GameObject overlayLabelGO = MakeText("ReconnectingLabel", overlayGO, "Reconnecting…", 28, TextAnchor.MiddleCenter);
        Stretch(overlayLabelGO, 0f);
        overlayGO.SetActive(false);

        GameObject controllerGO = new GameObject("WorldSessionController");
        WorldSessionController controller = controllerGO.AddComponent<WorldSessionController>();
        SerializedObject soCtrl = new SerializedObject(controller);
        soCtrl.FindProperty("joinCodeText").objectReferenceValue = codeGO.GetComponent<Text>();
        soCtrl.FindProperty("exitButton").objectReferenceValue = exitButton;
        SetProp(soCtrl, "reconnectingOverlay", overlayGO);
        SetProp(soCtrl, "reconnectingLabel", overlayLabelGO.GetComponent<Text>());
        soCtrl.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, WorldScenePath);
        Debug.Log($"[WorldSetup] World scene saved → {WorldScenePath}");
    }

    // -------------------------------------------------------------------------
    // Main menu scene
    // -------------------------------------------------------------------------

    private static void BuildMainMenuScene(WorldObjectCatalog catalog, GameObject playerPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddCamera();

        // --- Persistent NetworkManager GO carrying the session-wide singletons. ---
        GameObject nmGO = new GameObject("NetworkManager");
        NetworkManager nm = nmGO.AddComponent<NetworkManager>();
        UnityTransport transport = nmGO.AddComponent<UnityTransport>();
        nmGO.AddComponent<GameSession>();          // PersistentSingleton — keeps the GO across scene loads
        nmGO.AddComponent<WorldObjectRegistry>();
        PlayerSpawner spawner = nmGO.AddComponent<PlayerSpawner>();
        WorldLoader loader = nmGO.AddComponent<WorldLoader>();

        // Transport + scene management on the NetworkManager. Set directly via the
        // public API — the serialized field is NetworkConfig (NOT m_NetworkConfig) in
        // this NGO version, so path-string wiring silently fails.
        if (nm.NetworkConfig == null) { nm.NetworkConfig = new NetworkConfig(); }
        nm.NetworkConfig.NetworkTransport = transport;
        nm.NetworkConfig.EnableSceneManagement = true;
        nm.NetworkConfig.ConnectionApproval = false;
        EditorUtility.SetDirty(nm);

        // No custom prefab list: NGO's auto-generated DefaultNetworkPrefabs.asset
        // already contains every NetworkObject prefab (Player + WO_*), and each
        // NetworkManager references it automatically. Registering our own list on top
        // double-registers and triggers "NetworkPrefab has a duplicate" errors.
        // Remove the stale custom list earlier tool versions created, if present.
        AssetDatabase.DeleteAsset("Assets/Prefabs/NetworkPrefabs.asset");

        // PlayerSpawner: player prefab (no scene spawn points — world flow uses start pos).
        SerializedObject soSpawner = new SerializedObject(spawner);
        SetProp(soSpawner, "playerPrefab", playerPrefab);
        soSpawner.ApplyModifiedProperties();

        // WorldLoader: catalog + new-world seed content.
        SerializedObject soLoader = new SerializedObject(loader);
        SetProp(soLoader, "catalog", catalog);
        BuildNewWorldSeed(soLoader.FindProperty("newWorldSeed"));
        soLoader.ApplyModifiedProperties();

        // --- Menu UI ---
        EnsureEventSystem();
        BuildMenuUI();

        EditorSceneManager.SaveScene(scene, MainMenuPath);
        Debug.Log($"[WorldSetup] Main menu scene saved → {MainMenuPath}");
    }

    private static void BuildNewWorldSeed(SerializedProperty seed)
    {
        if (seed == null) { return; }

        // typeId, offset-from-start, quantity
        (string id, Vector3 pos, int qty)[] entries =
        {
            ("npc_basic",      new Vector3(-2f, 0f, 0f), 1),
            ("npc_basic",      new Vector3( 2f, 0f, 0f), 1),
            ("building_basic", new Vector3( 5f, 0f, 0f), 1),
            ("resource_basic", new Vector3(-5f, 0f, 0f), 10),
            ("resource_basic", new Vector3( 7f, 0f, 0f), 10),
        };

        WorldObjectCategory[] cats =
        {
            WorldObjectCategory.Npc, WorldObjectCategory.Npc, WorldObjectCategory.Building,
            WorldObjectCategory.Resource, WorldObjectCategory.Resource,
        };

        seed.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty e = seed.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("category").enumValueIndex = (int)cats[i];
            e.FindPropertyRelative("typeId").stringValue = entries[i].id;
            e.FindPropertyRelative("position").vector3Value = entries[i].pos;
            e.FindPropertyRelative("quantity").intValue = entries[i].qty;
            e.FindPropertyRelative("stateJson").stringValue = string.Empty;
        }
    }

    private static void BuildMenuUI()
    {
        GameObject canvasGO = MakeCanvas("MenuUI");

        // Title
        GameObject titleGO = MakeText("Title", canvasGO, "GHOUL", 40, TextAnchor.MiddleCenter);
        Anchor(titleGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(400f, 60f), new Vector2(0f, -50f));

        Text[] slotLabels = new Text[SaveSystem.SlotCount];
        Button[] slotPrimary = new Button[SaveSystem.SlotCount];
        Text[] slotPrimaryLabels = new Text[SaveSystem.SlotCount];
        Button[] slotDelete = new Button[SaveSystem.SlotCount];

        // 5 slot rows
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            float y = -120f - i * 56f;

            GameObject row = RectChild($"SlotRow{i}", canvasGO);
            Anchor(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, 50f), new Vector2(0f, y));
            row.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            GameObject labelGO = MakeText($"Slot{i}Label", row, $"Slot {i + 1}", 16, TextAnchor.MiddleLeft);
            Anchor(labelGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(300f, 46f), new Vector2(160f, 0f));
            slotLabels[i] = labelGO.GetComponent<Text>();

            GameObject primaryGO = MakeButton($"Slot{i}Primary", row, "Create", out Button primaryBtn, out Text primaryLabel);
            Anchor(primaryGO, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(110f, 40f), new Vector2(-170f, 0f));
            slotPrimary[i] = primaryBtn;
            slotPrimaryLabels[i] = primaryLabel;

            GameObject deleteGO = MakeButton($"Slot{i}Delete", row, "Delete", out Button deleteBtn, out _);
            Anchor(deleteGO, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(90f, 40f), new Vector2(-60f, 0f));
            deleteGO.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f);
            slotDelete[i] = deleteBtn;
        }

        // Join panel
        GameObject joinLabel = MakeText("JoinLabel", canvasGO, "Join a friend's world:", 14, TextAnchor.MiddleCenter);
        Anchor(joinLabel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 20f), new Vector2(0f, 120f));

        GameObject inputGO = RectChild("JoinCodeInput", canvasGO);
        Anchor(inputGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 36f), new Vector2(-70f, 80f));
        inputGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);
        InputField joinInput = inputGO.AddComponent<InputField>();
        GameObject inputText = MakeText("Text", inputGO, "", 18, TextAnchor.MiddleLeft);
        inputText.GetComponent<Text>().color = Color.black;
        Stretch(inputText, 6f);
        joinInput.textComponent = inputText.GetComponent<Text>();

        GameObject joinBtnGO = MakeButton("JoinButton", canvasGO, "Join", out Button joinButton, out _);
        Anchor(joinBtnGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(90f, 36f), new Vector2(85f, 80f));

        // Code display (hidden until hosting) + status
        GameObject codeDisplay = RectChild("CodeDisplay", canvasGO);
        Anchor(codeDisplay, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 40f), new Vector2(0f, 36f));
        codeDisplay.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.3f, 1f);
        GameObject codeText = MakeText("CodeText", codeDisplay, "------", 24, TextAnchor.MiddleCenter);
        Stretch(codeText, 0f);
        codeText.GetComponent<Text>().color = Color.white;

        GameObject statusGO = MakeText("StatusText", canvasGO, "", 14, TextAnchor.MiddleCenter);
        Anchor(statusGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(460f, 24f), new Vector2(0f, 8f));
        statusGO.GetComponent<Text>().color = new Color(1f, 0.5f, 0.5f);

        // Wire MainMenuUI
        MainMenuUI menu = canvasGO.AddComponent<MainMenuUI>();
        SerializedObject so = new SerializedObject(menu);
        SetArray(so, "slotLabels", slotLabels);
        SetArray(so, "slotPrimaryButtons", slotPrimary);
        SetArray(so, "slotPrimaryLabels", slotPrimaryLabels);
        SetArray(so, "slotDeleteButtons", slotDelete);
        SetProp(so, "joinCodeInput", joinInput);
        SetProp(so, "joinButton", joinButton);
        SetProp(so, "codeDisplay", codeDisplay);
        SetProp(so, "codeText", codeText.GetComponent<Text>());
        SetProp(so, "statusText", statusGO.GetComponent<Text>());
        so.ApplyModifiedProperties();
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ScenesDir)) { AssetDatabase.CreateFolder("Assets", "Scenes"); }
        if (!AssetDatabase.IsValidFolder(PrefabsDir)) { AssetDatabase.CreateFolder("Assets", "Prefabs"); }
    }

    private static Camera AddCamera()
    {
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.13f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        return cam;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) { return; }
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject MakeCanvas(string name)
    {
        GameObject canvasGO = new GameObject(name);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        return canvasGO;
    }

    private static GameObject RectChild(string name, GameObject parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static GameObject MakeText(string name, GameObject parent, string text, int size, TextAnchor anchor)
    {
        GameObject go = RectChild(name, parent);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = BuiltinFont();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        return go;
    }

    private static GameObject MakeButton(string name, GameObject parent, string label, out Button button, out Text labelText)
    {
        GameObject go = RectChild(name, parent);
        go.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.78f);
        button = go.AddComponent<Button>();

        GameObject textGO = MakeText("Text", go, label, 16, TextAnchor.MiddleCenter);
        Stretch(textGO, 0f);
        labelText = textGO.GetComponent<Text>();
        return go;
    }

    private static void Anchor(GameObject go, Vector2 min, Vector2 max, Vector2 size, Vector2 pos)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static void Stretch(GameObject go, float padding)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static Font BuiltinFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        return f;
    }

    private static void SetProp(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) { p.objectReferenceValue = value; }
        else { Debug.LogWarning($"[WorldSetup] Missing serialized property '{path}'."); }
    }

    private static void SetArray(SerializedObject so, string path, Object[] values)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p == null) { Debug.LogWarning($"[WorldSetup] Missing serialized array '{path}'."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    // Adds PlayerCinemachineTarget to the Player prefab so the owning client's camera
    // follows it (the component finds the world scene's CinemachineCamera at runtime).
    private static void EnsurePlayerCameraTarget()
    {
        using (PrefabUtility.EditPrefabContentsScope scope = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;
            if (root.GetComponent<PlayerCinemachineTarget>() == null)
            {
                root.AddComponent<PlayerCinemachineTarget>();
            }
        }
    }

    private static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        AddSceneUnique(scenes, MainMenuPath);   // index 0 — first scene loaded
        AddSceneUnique(scenes, WorldScenePath);

        // Preserve any other existing scenes after ours.
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path != MainMenuPath && s.path != WorldScenePath) { scenes.Add(s); }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddSceneUnique(List<EditorBuildSettingsScene> scenes, string path)
    {
        scenes.Add(new EditorBuildSettingsScene(path, true));
    }
}
