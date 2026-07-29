using UnityEngine;

// A simple usable (non-weapon) item: when used with the left hand
// (UseLeftHand -> Item.UseItemAbility), it emits a brief warm light burst at the player.
// Demonstrates a real item ability distinct from weapon attacks. Fully procedural — no
// lighting package or sprite asset required.
public class TorchItem : Item
{
    [Header("Torch Flare")]
    [SerializeField] private float flareRadius = 1.5f;
    [SerializeField] private float flareDuration = 0.6f;
    [SerializeField] private Color flareColor = new Color(1f, 0.75f, 0.35f, 1f);

    private static Sprite glowSprite;

    public override void UseItemAbility(PlayerController playerController)
    {
        if (playerController == null) { return; }

        // Spawn a standalone flare object so its lifetime is independent of this item
        // (which is deactivated while held in the inventory).
        GameObject flare = new GameObject("TorchFlare");
        flare.transform.position = playerController.transform.position + Vector3.up * 0.3f;
        flare.transform.localScale = Vector3.one * flareRadius;

        SpriteRenderer sr = flare.AddComponent<SpriteRenderer>();
        sr.sprite = GetGlowSprite();
        sr.color = flareColor;
        sr.sortingOrder = 20;

        flare.AddComponent<TorchFlare>().Init(flareDuration);
    }

    // A soft radial-gradient sprite generated once and reused for every flare.
    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) { return glowSprite; }

        const int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.Clamp01(1f - d);
                a *= a;  // softer falloff toward the edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        return glowSprite;
    }
}
