using UnityEngine;

// Marks the default player start location in a world scene. When a brand-new world
// is created, MainMenuUI seeds WorldSaveData.startPosition from this point (if the
// scene is already loaded) — otherwise the world's serialized startPosition is used.
// Useful as a designer-visible handle for "where players appear" per world.
public class WorldStartPoint : MonoBehaviour
{
    public static WorldStartPoint Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
    }
}
