using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.75f, 0f);

    private CharacterStats stats;
    private GameObject canvasGO;
    private RectTransform fillRT;
    private Image fillImage;

    // World-space bar dimensions: 1.0 x 0.1 units
    private const float CanvasScale = 0.005f;
    private const float BarWidthPx  = 200f;
    private const float BarHeightPx = 20f;

    private void Start()
    {
        stats = GetComponent<CharacterStats>();
        if (stats == null) return;

        BuildBar();
        stats.HealthChanged += OnHealthChanged;
        OnHealthChanged(stats.CurrentHealth, stats.MaxHealth);
    }

    private void OnDestroy()
    {
        if (stats != null) stats.HealthChanged -= OnHealthChanged;
        if (canvasGO != null) Destroy(canvasGO);
    }

    private void LateUpdate()
    {
        if (canvasGO != null)
            canvasGO.transform.position = transform.position + offset;
    }

    private void BuildBar()
    {
        canvasGO = new GameObject("HealthBar_" + gameObject.name);
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

    private void OnHealthChanged(float current, float max)
    {
        if (fillRT == null) return;
        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        fillRT.anchorMax = new Vector2(fraction, 1f);

        if (fraction > 0.5f)
            fillImage.color = Color.Lerp(Color.yellow, Color.green, (fraction - 0.5f) * 2f);
        else
            fillImage.color = Color.Lerp(Color.red, Color.yellow, fraction * 2f);
    }
}
