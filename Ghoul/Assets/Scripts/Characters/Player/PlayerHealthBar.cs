using UnityEngine;

public class PlayerHealthBar : MonoBehaviour
{
    // Sits directly above the stamina bar (PlayerStaminaBar, at y 0.75): 0.75 + bar height
    // (0.1) + WorldSpaceBar.StackSpacing (0.02).
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.87f, 0f);
    [SerializeField] private Color fillColor = new Color(0.85f, 0.15f, 0.15f);

    private CharacterStats stats;
    private WorldSpaceBar bar;

    private void Start()
    {
        stats = GetComponent<CharacterStats>();
        if (stats == null) return;

        bar = new WorldSpaceBar("HealthBar_" + gameObject.name);
        bar.SetColor(fillColor);
        stats.HealthChanged += OnHealthChanged;
        OnHealthChanged(stats.CurrentHealth, stats.MaxHealth);
    }

    private void OnDestroy()
    {
        if (stats != null) stats.HealthChanged -= OnHealthChanged;
        bar?.Destroy();
    }

    private void LateUpdate()
    {
        bar?.SetPosition(transform.position + offset);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (bar == null) return;
        bar.SetFraction(max > 0f ? current / max : 0f);
    }
}
