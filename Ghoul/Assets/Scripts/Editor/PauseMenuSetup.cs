using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Run via  Tools > World > Setup Pause Menu
// Builds a screen-overlay pause / map canvas in the active scene and wires it to the
// PauseMenu component. PlayerController.OpenMenu (the Pause input action) toggles it via
// FindObjectOfType at runtime, so no per-player reference wiring is needed.
//
// Also exposes  Tools > World > Ensure Player Inventory  to (re)add the PlayerInventory
// component to Assets/Prefabs/Player.prefab, matching the project's other setup tools.
public static class PauseMenuSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/World/Setup Pause Menu")]
    public static void SetupPauseMenu()
    {
        if (Object.FindObjectOfType<PauseMenu>() != null)
        {
            EditorUtility.DisplayDialog("Pause Menu Setup",
                "A PauseMenu already exists in the active scene — nothing to do.", "OK");
            return;
        }

        // EventSystem — required for any UI interaction (buttons added to the map later).
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Canvas
        GameObject canvasGO = new GameObject("PauseUI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;  // draw above gameplay/other HUD canvases
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen dim panel (placeholder for the pause menu / map contents).
        GameObject panel = RectChild("PausePanel", canvasGO);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(panel);

        // Title
        GameObject titleGO = RectChild("PausedTitle", panel);
        Text title = titleGO.AddComponent<Text>();
        title.font = BuiltinFont();
        title.fontSize = 40;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.text = "PAUSED";
        SizeAt(titleGO, new Vector2(400f, 60f), new Vector2(0f, 40f));

        // Subtitle / hint (placeholder for the map)
        GameObject hintGO = RectChild("PausedHint", panel);
        Text hint = hintGO.AddComponent<Text>();
        hint.font = BuiltinFont();
        hint.fontSize = 16;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        hint.text = "Map / Menu — press Pause to resume";
        SizeAt(hintGO, new Vector2(400f, 30f), new Vector2(0f, -20f));

        // PauseMenu component on the canvas root; wire the panel and hide it for edit mode
        // (PauseMenu.Start also hides it at play start).
        PauseMenu pauseMenu = canvasGO.AddComponent<PauseMenu>();
        SerializedObject so = new SerializedObject(pauseMenu);
        SerializedProperty panelProp = so.FindProperty("pausePanel");
        if (panelProp != null) { panelProp.objectReferenceValue = panel; }
        so.ApplyModifiedProperties();
        panel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[PauseMenuSetup] PauseUI created and wired. Save the scene (Ctrl+S). " +
                  "Re-enable 'PausePanel' in the Hierarchy to edit its contents.");
    }

    [MenuItem("Tools/World/Ensure Player Inventory")]
    public static void EnsurePlayerInventory()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Ensure Player Inventory",
                $"No prefab found at {PlayerPrefabPath}.", "OK");
            return;
        }

        using (PrefabUtility.EditPrefabContentsScope scope = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;
            if (root.GetComponent<PlayerInventory>() == null)
            {
                root.AddComponent<PlayerInventory>();
                Debug.Log("[PauseMenuSetup] PlayerInventory added to Player.prefab.");
            }
            else
            {
                Debug.Log("[PauseMenuSetup] PlayerInventory already present on Player.prefab — skipped.");
            }
        }
    }

    // -------------------------------------------------------------------------

    private static GameObject RectChild(string name, GameObject parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SizeAt(GameObject go, Vector2 size, Vector2 pos)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static Font BuiltinFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        return f;
    }
}
