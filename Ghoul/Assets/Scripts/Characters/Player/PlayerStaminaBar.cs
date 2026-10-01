using UnityEngine;

// World-space stamina bar under the health bar, visible to everyone (like the health bar).
// Updates in real time from PlayerStamina.StaminaChanged, including rollback restores.
public class PlayerStaminaBar : MonoBehaviour
{
    // The spot the health bar used to occupy; the health bar now sits directly above.
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private Color fillColor = new Color(0.2f, 0.85f, 0.25f);

    private PlayerStamina stamina;
    private WorldSpaceBar bar;

    private void Start()
    {
        stamina = GetComponent<PlayerStamina>();
        if (stamina == null) return;

        bar = new WorldSpaceBar("StaminaBar_" + gameObject.name);
        bar.SetColor(fillColor);
        stamina.StaminaChanged += OnStaminaChanged;
        OnStaminaChanged(stamina.CurrentStamina, stamina.MaxStamina);
    }

    private void OnDestroy()
    {
        if (stamina != null) stamina.StaminaChanged -= OnStaminaChanged;
        bar?.Destroy();
    }

    private void LateUpdate()
    {
        bar?.SetPosition(transform.position + offset);
    }

    private void OnStaminaChanged(float current, float max)
    {
        bar?.SetFraction(max > 0f ? current / max : 0f);
    }
}
