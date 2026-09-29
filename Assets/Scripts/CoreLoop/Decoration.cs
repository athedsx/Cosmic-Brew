using System.Collections.Generic;
using UnityEngine;

// Decoration that can be bought, placed, moved, painted and stored in Build Mode
public class Decoration : MonoBehaviour
{
    [Tooltip("Identifier (used in the save file) and fallback name")]
    public string displayName = "Decoração";
    [Tooltip("Localization key for the name, e.g. deco.plant")]
    public string nameKey;

    public string DisplayName => Loc.KeyOr(nameKey, displayName);
    [Tooltip("Catalog price (also refunded when stored)")]
    public int price = 10;
    [Tooltip("Footprint radius on the floor (m)")]
    public float footprint = 0.45f;
    [Tooltip("Customers can wait here (stool)")]
    public bool isSeat;
    [Tooltip("Lies flat and does not block (e.g. rug)")]
    public bool isFloor;
    [Tooltip("Parts recolored when painting (empty = every untextured part)")]
    public Renderer[] colorParts;

    // Decoration palette (the first entry is the model's original color)
    public static readonly IslandColors.Swatch[] Palette =
    {
        new IslandColors.Swatch { name = "original", key = "color.original", color = Color.white },
        new IslandColors.Swatch { name = "peach", key = "color.peach", color = new Color(1f, 0.76f, 0.64f) },
        new IslandColors.Swatch { name = "pink", key = "color.pink", color = new Color(1f, 0.7f, 0.8f) },
        new IslandColors.Swatch { name = "lavender", key = "color.lavender", color = new Color(0.78f, 0.7f, 0.96f) },
        new IslandColors.Swatch { name = "sky", key = "color.sky", color = new Color(0.64f, 0.83f, 1f) },
        new IslandColors.Swatch { name = "mint", key = "color.mint", color = new Color(0.62f, 0.92f, 0.78f) },
        new IslandColors.Swatch { name = "gold", key = "color.gold", color = new Color(1f, 0.85f, 0.46f) },
        new IslandColors.Swatch { name = "cream", key = "color.cream", color = new Color(1f, 0.96f, 0.88f) },
    };

    public int ColorIndex { get; private set; }
    public string ColorName => Palette[ColorIndex].name;

    private readonly Dictionary<Material, Color> originals = new Dictionary<Material, Color>();

    public static int FindColor(string name)
    {
        for (int i = 0; i < Palette.Length; i++) if (Palette[i].name == name) return i;
        return 0;
    }

    public void ApplyColor(int index)
    {
        if (index < 0 || index >= Palette.Length) return;
        ColorIndex = index;
        foreach (var r in Parts())
        {
            foreach (var m in r.materials) // per-item material copies
            {
                if (!m.HasProperty("_BaseColor")) continue;
                if (!originals.TryGetValue(m, out var orig)) originals[m] = orig = m.GetColor("_BaseColor");
                m.SetColor("_BaseColor", index == 0 ? orig : Palette[index].color);
            }
        }
    }

    IEnumerable<Renderer> Parts()
    {
        if (colorParts != null && colorParts.Length > 0)
        {
            foreach (var r in colorParts) if (r != null) yield return r;
            yield break;
        }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial != null && r.sharedMaterial.mainTexture == null && !(r is ParticleSystemRenderer)) yield return r;
    }
}
