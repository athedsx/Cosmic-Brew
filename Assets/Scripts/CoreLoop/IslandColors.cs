using UnityEngine;

// Cores personalizáveis da ilha (deck, borda, quiosque), trocadas no Modo Construção.
// Usa cópias dos materiais em tempo de execução, então os arquivos de material não mudam.
public class IslandColors : MonoBehaviour
{
    public static IslandColors Instance { get; private set; }

    [System.Serializable]
    public class Swatch
    {
        [Tooltip("Identificador (usado no save)")]
        public string name;
        [Tooltip("Chave de tradução, ex.: color.pink")]
        public string key;
        public Color color = Color.white;

        public string DisplayName => Loc.KeyOr(key, name);
    }

    [System.Serializable]
    public class Target
    {
        public string name;
        [Tooltip("Chave de tradução, ex.: colors.deck")]
        public string nameKey;
        public Renderer[] renderers;
        [Tooltip("A primeira opção é a cor original")]
        public Swatch[] swatches;
        [System.NonSerialized] public int current;

        public string DisplayName => Loc.KeyOr(nameKey, name);
    }

    public Target[] targets;
    [Tooltip("Preço de cada troca de cor, em Moedas Estelares")]
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

    // Qual parte da ilha foi clicada (-1 = nenhuma)
    public int FindTarget(Transform hit)
    {
        if (hit == null) return -1;
        for (int i = 0; i < targets.Length; i++)
            foreach (var r in targets[i].renderers)
                if (r != null && (hit == r.transform || hit.IsChildOf(r.transform))) return i;
        return -1;
    }

    // nomes das cores atuais (para o save)
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
