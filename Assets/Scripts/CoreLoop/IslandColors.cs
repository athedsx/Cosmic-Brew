using UnityEngine;

// Customizable island colors (deck, rim, kiosk), changed in Build Mode.
// Uses runtime material copies, so the material assets are never modified.
public class IslandColors : MonoBehaviour
{
    public static IslandColors Instance { get; private set; }

    [System.Serializable]
    public class Swatch
    {
        [Tooltip("Identifier (used in the save file)")]
        public string name;
        [Tooltip("Localization key, e.g. color.pink")]
        public string key;
        public Color color = Color.white;

        public string DisplayName => Loc.KeyOr(key, name);
    }

    [System.Serializable]
    public class Target
    {
        public string name;
        [Tooltip("Localization key, e.g. colors.deck")]
        public string nameKey;
        public Renderer[] renderers;
        [Tooltip("The first option is the original color")]
        public Swatch[] swatches;
        [System.NonSerialized] public int current;

        public string DisplayName => Loc.KeyOr(nameKey, name);
    }

    public Target[] targets;
    [Tooltip("Cost of each color change, in Star Coins")]
    public int changeCost = 5;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Apply(int target, int swatch)
    {
        if (target < 0 || target >= targets.Length) return;
        var t = targets[target];
        if (swatch < 0 || swatch >= t.swatches.Length) return;
        t.current = swatch;
        foreach (var r in t.renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials) m.SetColor("_BaseColor", t.swatches[swatch].color);
        }
    }

    // Which island part was clicked (-1 = none)
    public int FindTarget(Transform hit)
    {
        if (hit == null) return -1;
        for (int i = 0; i < targets.Length; i++)
            foreach (var r in targets[i].renderers)
                if (r != null && (hit == r.transform || hit.IsChildOf(r.transform))) return i;
        return -1;
    }

    // current color names (for the save file)
    public string[] CurrentNames()
    {
        var names = new string[targets.Length];
        for (int i = 0; i < targets.Length; i++) names[i] = targets[i].swatches[targets[i].current].name;
        return names;
    }

    public void ApplyNames(System.Collections.Generic.IList<string> names)
    {
        if (names == null) return;
        for (int i = 0; i < targets.Length && i < names.Count; i++)
            for (int s = 0; s < targets[i].swatches.Length; s++)
                if (targets[i].swatches[s].name == names[i]) { Apply(i, s); break; }
    }
}
