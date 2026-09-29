using System.Collections.Generic;
using UnityEngine;

// Ingredientes que as estações adicionam na xícara
public enum Ingredient { Cafe, PoeiraEstelar, LeiteNebuloso }

public static class IngredientInfo
{
    public static string Name(Ingredient i) => i switch
    {
        Ingredient.Cafe => Loc.Get("ing.coffee"),
        Ingredient.PoeiraEstelar => Loc.Get("ing.stardust"),
        Ingredient.LeiteNebuloso => Loc.Get("ing.milk"),
        _ => i.ToString()
    };

    // cor do líquido na xícara 3D
    public static Color LiquidColor(Ingredient i) => i switch
    {
        Ingredient.Cafe => new Color(0.42f, 0.24f, 0.14f),
        Ingredient.PoeiraEstelar => new Color(0.55f, 0.85f, 1f),
        Ingredient.LeiteNebuloso => new Color(1f, 0.9f, 0.96f),
        _ => Color.white
    };

    // cor do texto sobre fundo escuro (HUD) e sobre fundo claro (balões)
    public static Color UiOnDark(Ingredient i) => i switch
    {
        Ingredient.Cafe => new Color(1f, 0.78f, 0.58f),
        Ingredient.PoeiraEstelar => new Color(0.6f, 0.9f, 1f),
        Ingredient.LeiteNebuloso => new Color(1f, 0.78f, 0.9f),
        _ => Color.white
    };

    public static Color UiOnLight(Ingredient i) => i switch
    {
        Ingredient.Cafe => new Color(0.55f, 0.32f, 0.18f),
        Ingredient.PoeiraEstelar => new Color(0.2f, 0.52f, 0.78f),
        Ingredient.LeiteNebuloso => new Color(0.72f, 0.36f, 0.6f),
        _ => Color.black
    };

    // alguma estação ativa na ilha oferece este ingrediente?
    public static bool Available(Ingredient i)
    {
        foreach (var it in Interactable.All)
            if (it is Station s && s.ingredient == i && s.isActiveAndEnabled) return true;
        return false;
    }
}

[System.Serializable]
public class Recipe
{
    [Tooltip("Chave de tradução do nome (strings.txt)")]
    public string key;
    public Ingredient[] ingredients;
    public int price;

    public string Name => Loc.Get(key);

    // A ordem não importa (vibe zen): só precisa ter os mesmos ingredientes
    public bool Matches(IReadOnlyList<Ingredient> cup)
    {
        if (cup == null || cup.Count != ingredients.Length) return false;
        var counts = new Dictionary<Ingredient, int>();
        foreach (var i in ingredients) counts[i] = counts.TryGetValue(i, out var c) ? c + 1 : 1;
        foreach (var i in cup)
        {
            if (!counts.TryGetValue(i, out var c) || c == 0) return false;
            counts[i] = c - 1;
        }
        return true;
    }
}

public static class RecipeBook
{
    public static readonly Recipe[] All =
    {
        new Recipe { key = "recipe.espresso", ingredients = new[] { Ingredient.Cafe }, price = 8 },
        new Recipe { key = "recipe.stellar", ingredients = new[] { Ingredient.Cafe, Ingredient.PoeiraEstelar }, price = 12 },
        new Recipe { key = "recipe.double", ingredients = new[] { Ingredient.Cafe, Ingredient.Cafe }, price = 10 },
        // precisam do Vaporizador de Leite Nebuloso (comprado no Modo Construção)
        new Recipe { key = "recipe.latte", ingredients = new[] { Ingredient.Cafe, Ingredient.LeiteNebuloso }, price = 13 },
        new Recipe { key = "recipe.cloud", ingredients = new[] { Ingredient.LeiteNebuloso, Ingredient.PoeiraEstelar }, price = 14 },
        new Recipe { key = "recipe.galaxy", ingredients = new[] { Ingredient.Cafe, Ingredient.LeiteNebuloso, Ingredient.PoeiraEstelar }, price = 20 },
    };

    // só pede o que dá para fazer com as estações que existem na ilha
    public static Recipe Random()
    {
        var possible = new List<Recipe>();
        foreach (var r in All)
        {
            bool ok = true;
            foreach (var i in r.ingredients) if (!IngredientInfo.Available(i)) { ok = false; break; }
            if (ok) possible.Add(r);
        }
        if (possible.Count == 0) return All[0];
        return possible[UnityEngine.Random.Range(0, possible.Count)];
    }

    public static Recipe Find(IReadOnlyList<Ingredient> cup)
    {
        foreach (var r in All) if (r.Matches(cup)) return r;
        return null;
    }
}

// Moedas Estelares do jogador
public static class CoinWallet
{
    public static int Coins { get; private set; }
    public static event System.Action<int, int> Changed; // (total, variação)

    public static void Add(int amount)
    {
        Coins += amount;
        Changed?.Invoke(Coins, amount);
    }

    // usado ao carregar o save
    public static void Set(int amount)
    {
        Coins = Mathf.Max(0, amount);
        Changed?.Invoke(Coins, 0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Coins = 0;
        Changed = null;
    }
}
