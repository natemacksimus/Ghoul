using UnityEngine;
using UnityEngine.UI;

// A small world-space bar (dark background + left-anchored fill) that follows a character.
// Shared by PlayerHealthBar and PlayerStaminaBar so they match in size and style.
public class WorldSpaceBar
{
    // World-space bar dimensions: 1.0 x 0.1 units
    public const float CanvasScale = 0.005f;
    public const float BarWidthPx  = 200f;
    public const float BarHeightPx = 20f;
    public const float WorldHeight = BarHeightPx * CanvasScale;
    // Vertical gap between stacked bars, in world units.
    public const float StackSpacing = 0.02f;

    private readonly GameObject canvasGO;
    private readonly RectTransform fillRT;
    private readonly Image fillImage;

    public WorldSpaceBar(string name)
    {
        canvasGO = new GameObject(name);
        canvasGO.transform.localScale = Vector3.one * CanvasScale;

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(BarWidthPx, BarHeightPx);

        // Dark background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // Fill — anchored to the left; anchorMax.x drives the visible fraction
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        fillImage = fillGO.AddComponent<Image>();
    }

    public void SetFraction(float fraction) => fillRT.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

    public void SetColor(Color color) => fillImage.color = color;

    public void SetPosition(Vector3 worldPosition) => canvasGO.transform.position = worldPosition;

    public void Destroy() => Object.Destroy(canvasGO);
}
