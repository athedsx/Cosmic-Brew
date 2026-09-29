using System.Collections.Generic;
using UnityEngine;

// Item de decoração que pode ser comprado, colocado, movido, pintado e guardado no Modo Construção
public class Decoration : MonoBehaviour
{
    [Tooltip("Identificador (usado no save) e nome de reserva")]
    public string displayName = "Decoração";
    [Tooltip("Chave de tradução do nome, ex.: deco.plant")]
    public string nameKey;

    public string DisplayName => Loc.KeyOr(nameKey, displayName);
    [Tooltip("Preço no catálogo (e o valor devolvido ao guardar)")]
    public int price = 10;
    [Tooltip("Raio ocupado no chão (m)")]
    public float footprint = 0.45f;
    [Tooltip("Clientes podem esperar aqui (banquinho)")]
    public bool isSeat;
    [Tooltip("Fica no chão e não bloqueia (ex.: tapete)")]
    public bool isFloor;
    [Tooltip("Partes que mudam de cor ao pintar (vazio = todas sem textura)")]
    public Renderer[] colorParts;

    // Paleta das decorações (a primeira é a cor original do modelo)
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
            foreach (var m in r.materials) // cópias só deste item
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
