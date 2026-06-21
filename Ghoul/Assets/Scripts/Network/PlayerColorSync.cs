using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerColorSync : NetworkBehaviour
{
    private static readonly Color[] Palette = new Color[]
    {
        Color.white,
        new Color(0f,   0.9f, 1f,   1f),  // cyan
        new Color(1f,   0.55f, 0f,  1f),  // orange
        new Color(0.7f, 0.2f, 1f,   1f),  // purple
        new Color(0.2f, 1f,   0.3f, 1f),  // green
        new Color(1f,   0.9f, 0f,   1f),  // yellow
    };

    private NetworkVariable<int> netColorIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private SpriteRenderer spriteRenderer;

    public static int NextColorIndex(HashSet<int> usedIndices)
    {
        for (int i = 0; i < Palette.Length; i++)
        {
            if (!usedIndices.Contains(i)) return i;
        }
        return usedIndices.Count % Palette.Length;
    }

    public void AssignColorIndex(int index)
    {
        netColorIndex.Value = index;
    }

    public override void OnNetworkSpawn()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        netColorIndex.OnValueChanged += OnColorIndexChanged;
        ApplyColor(netColorIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        netColorIndex.OnValueChanged -= OnColorIndexChanged;
    }

    private void OnColorIndexChanged(int prev, int next) => ApplyColor(next);

    private void ApplyColor(int index)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = index >= 0 && index < Palette.Length ? Palette[index] : Color.white;
    }
}
