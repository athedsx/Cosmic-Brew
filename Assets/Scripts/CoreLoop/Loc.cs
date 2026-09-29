using System.Collections.Generic;
using UnityEngine;

public enum Lang { PT, EN, RU, KO, ZH, JA }

// Game localization. Texts live in Resources/Localization/strings.txt (TSV: key, pt, en, ru, ko, zh, ja).
// Usage: Loc.Get("menu.play") or Loc.Get("customer.serve", drinkName). Loc.Changed fires when the language changes.
public static class Loc
{
    public const string PrefKey = "cosmicbrew.lang";

    public static readonly string[] Codes = { "pt", "en", "ru", "ko", "zh", "ja" };
    public static readonly string[] NativeNames = { "Português", "English", "Русский", "한국어", "中文（简体）", "日本語" };
    static readonly string[] FontFiles = { "Nunito-SemiBold", "Nunito-SemiBold", "Nunito-SemiBold", "NotoSansKR-Medium", "NotoSansSC-Medium", "NotoSansJP-Medium" };

    public static event System.Action Changed;

    static Dictionary<string, string[]> table;
    static Lang current;
    static readonly Font[] fonts = new Font[6];

    public static Lang Current { get { Ensure(); return current; } }
    public static Font Font => FontFor(Current);

    // Korean, Chinese and Japanese: fonts are already heavy, no synthetic bold
    public static bool IsCJK => Current == Lang.KO || Current == Lang.ZH || Current == Lang.JA;

    public static Font FontFor(Lang lang)
    {
        int i = (int)lang;
        if (fonts[i] == null) fonts[i] = Resources.Load<Font>("Fonts/" + FontFiles[i]);
        if (fonts[i] == null) fonts[i] = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return fonts[i];
    }

    public static string Get(string key)
    {
        Ensure();
        if (string.IsNullOrEmpty(key)) return "";
        if (!table.TryGetValue(key, out var row)) return key; // missing keys show on screen (easy to spot)
        string s = row[(int)current];
        if (string.IsNullOrEmpty(s)) s = row[(int)Lang.EN];
        if (string.IsNullOrEmpty(s)) s = row[(int)Lang.PT];
        return s;
    }

    public static string Get(string key, params object[] args)
    {
        string s = Get(key);
        try { return string.Format(s, args); }
        catch (System.FormatException) { return s; }
    }

    // Translates known keys; otherwise returns the text itself
    public static string KeyOr(string key, string fallback)
    {
        Ensure();
        return !string.IsNullOrEmpty(key) && table.ContainsKey(key) ? Get(key) : fallback;
    }

    public static void Set(Lang lang)
    {
        Ensure();
        if (lang == current) return;
        current = lang;
        PlayerPrefs.SetString(PrefKey, Codes[(int)lang]);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static void Ensure()
    {
        if (table != null) return;
        table = new Dictionary<string, string[]>();
        var asset = Resources.Load<TextAsset>("Localization/strings");
        if (asset != null)
        {
            var lines = asset.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split('\t');
                var row = new string[6];
                for (int c = 0; c < 6; c++) row[c] = c + 1 < parts.Length ? parts[c + 1] : "";
                table[parts[0]] = row;
            }
        }
        else Debug.LogWarning("[Loc] Resources/Localization/strings.txt not found");

        string saved = PlayerPrefs.GetString(PrefKey, "");
        int idx = System.Array.IndexOf(Codes, saved);
        current = idx >= 0 ? (Lang)idx : Detect();
    }

    // First run: use the system language
    static Lang Detect()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Portuguese: return Lang.PT;
            case SystemLanguage.Russian:
            case SystemLanguage.Ukrainian:
            case SystemLanguage.Belarusian: return Lang.RU;
            case SystemLanguage.Korean: return Lang.KO;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return Lang.ZH;
            case SystemLanguage.Japanese: return Lang.JA;
            default: return Lang.EN;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Changed = null;
        table = null;
    }
}
