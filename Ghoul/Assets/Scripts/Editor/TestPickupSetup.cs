using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run via  Tools > World > Create Test Pickups
// Generates three simple Item pickups (prefab assets in Assets/Prefabs/TestPickups) and
// drops connected instances into the active scene so you can exercise the inventory:
//
//   * Test Sword  (WEAPON, Right hand)  - red
//   * Test Axe    (WEAPON, Right hand)  - orange   (gives the right hand 2 items to cycle)
//   * Test Torch  (ITEM,   Left hand)   - cyan     (TorchItem: emits a light burst on use)
//
// Each pickup: root has Item + a trigger BoxCollider2D (~1 unit); a child holds the
// SpriteRenderer scaled to render ~1 world unit. No Animator/ItemGravity needed (Item's
// animator calls are null-guarded; attack hitboxes ignore non-CharacterStats objects).
// These are local test props, not NetworkObjects. Re-running clears the old instances first.
public static class TestPickupSetup
{
    private const string FolderParent = "Assets/Prefabs";
    private const string FolderName = "TestPickups";
    private const string FolderPath = FolderParent + "/" + FolderName;
    private const string SpritePath = FolderPath + "/_TestSquare.png";

    private static readonly string[] PickupNames = { "TestSword", "TestAxe", "TestTorch" };

    [MenuItem("Tools/World/Create Test Pickups")]
    public static void CreateTestPickups()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
        {
            AssetDatabase.CreateFolder(FolderParent, FolderName);
        }

        // Remove any existing instances so re-running doesn't pile up duplicates.
        foreach (string n in PickupNames)
        {
            GameObject existing = GameObject.Find(n);
            while (existing != null) { Object.DestroyImmediate(existing); existing = GameObject.Find(n); }
        }

        Sprite square = EnsureSquareSprite();

        MakePickup("Test Sword", "TestSword", typeof(Item),      InventoryType.WEAPON, HandSlot.Right, square, new Color(0.85f, 0.2f, 0.2f), 20, new Vector2(30f, 0f), new Vector3(-2f, 1f, 0f));
        MakePickup("Test Axe",   "TestAxe",   typeof(Item),      InventoryType.WEAPON, HandSlot.Right, square, new Color(0.95f, 0.55f, 0.1f), 35, new Vector2(45f, 0f), new Vector3(0f, 1f, 0f));
        MakePickup("Test Torch", "TestTorch", typeof(TorchItem), InventoryType.ITEM,   HandSlot.Left,  square, new Color(0.2f, 0.8f, 0.9f), 0,  Vector2.zero,          new Vector3(2f, 1f, 0f));

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[TestPickupSetup] Created 3 test pickups in " + FolderPath +
                  " and placed instances in the scene. Save the scene (Ctrl+S), press Play, Host, and walk into them.");
    }

    // Creates (once) a plain white 64x64 sprite at 64 px/unit -> renders 1 world unit at
    // scale 1. Tinted per-pickup via SpriteRenderer.color.
    private static Sprite EnsureSquareSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) { return existing; }

        const int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) { px[i] = Color.white; }
        tex.SetPixels(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = size;   // 64 px / 64 ppu = 1 world unit
        ti.filterMode = FilterMode.Point;
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static void MakePickup(string itemName, string objectName, System.Type itemComponentType, InventoryType type, HandSlot slot, Sprite sprite, Color color, int damage, Vector2 knockback, Vector3 scenePos)
    {
        // Root: item logic + a sane-sized trigger. Kept at scale 1 so the collider is ~1 unit.
        GameObject temp = new GameObject(objectName);

        BoxCollider2D col = temp.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;

        Item item = (Item)temp.AddComponent(itemComponentType);
        item.itemName = itemName;
        item.inventoryType = type;
        item.handSlot = slot;
        item.itemDamage = damage;
        item.knockbackPower = knockback;
        item.isInventory = true;
        item.isConsumable = false;
        item.itemId = 1;
        item.itemQty = 1;
        item.itemMaxStackSize = 1;

        // Child: the visible sprite (Item.sprite = GetComponentInChildren<SpriteRenderer>()).
        GameObject spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(temp.transform, false);
        spriteGO.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        SpriteRenderer sr = spriteGO.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 5;

        string prefabPath = FolderPath + "/" + objectName + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath, out bool ok);
        Object.DestroyImmediate(temp);

        if (!ok || prefab == null)
        {
            Debug.LogWarning("[TestPickupSetup] Failed to save prefab: " + prefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = scenePos;
        Debug.Log("[TestPickupSetup] " + itemName + " (" + type + ", " + slot + " hand) -> " + prefabPath);
    }
}
