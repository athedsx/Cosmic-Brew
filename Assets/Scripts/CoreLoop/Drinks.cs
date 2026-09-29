using System.Collections.Generic;
using UnityEngine;

// Ingredients that stations add to the cup
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

    // liquid color in the 3D cup
    public static Color LiquidColor(Ingredient i) => i switch
    {
        Ingredient.Cafe => new Color(0.42f, 0.24f, 0.14f),
        Ingredient.PoeiraEstelar => new Color(0.55f, 0.85f, 1f),
        Ingredient.LeiteNebuloso => new Color(1f, 0.9f, 0.96f),
        _ => Color.white
    };

    // text color on dark backgrounds (HUD) and light backgrounds (bubbles)
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

    // does any active station on the island provide this ingredient?
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
    [Tooltip("Localization key for the name (strings.txt)")]
    public string key;
    public Ingredient[] ingredients;
    public int price;

    public string Name => Loc.Get(key);

    // Order does not matter (zen vibe): it only needs the same ingredients
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
        // require the Nebula Milk Steamer (bought in Build Mode)
        new Recipe { key = "recipe.latte", ingredients = new[] { Ingredient.Cafe, Ingredient.LeiteNebuloso }, price = 13 },
        new Recipe { key = "recipe.cloud", ingredients = new[] { Ingredient.LeiteNebuloso, Ingredient.PoeiraEstelar }, price = 14 },
        new Recipe { key = "recipe.galaxy", ingredients = new[] { Ingredient.Cafe, Ingredient.LeiteNebuloso, Ingredient.PoeiraEstelar }, price = 20 },
    };

    // only orders what the stations on the island can make
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

// The player's Star Coins
public static class CoinWallet
{
    public static int Coins { get; private set; }
    public static event System.Action<int, int> Changed; // (total, delta)

    public static void Add(int amount)
    {
        Coins += amount;
        Changed?.Invoke(Coins, amount);
    }

    // used when loading the save
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
