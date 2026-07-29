using UnityEngine;

// Gently expands and fades the torch light burst spawned by TorchItem, then destroys it.
public class TorchFlare : MonoBehaviour
{
    private float duration;
    private float elapsed;
    private SpriteRenderer sr;
    private Vector3 baseScale;
    private Color baseColor;

    public void Init(float duration)
    {
        this.duration = Mathf.Max(0.01f, duration);
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        baseColor = sr != null ? sr.color : Color.white;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        transform.localScale = baseScale * (1f + 0.5f * t);

        if (sr != null)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - t);
            sr.color = c;
        }

        if (t >= 1f) { Destroy(gameObject); }
    }
}
