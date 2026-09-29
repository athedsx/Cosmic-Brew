using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// In-game UI (uGUI, built in code):
// Star Coin HUD, current cup, controls hint,
// "E · action" prompt over the nearest object, order bubbles over customers and floating texts.
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("3D coin in the HUD")]
    [Tooltip("Star Coin model (cloned and rendered live in the screen corner)")]
    public GameObject hudCoinModel;
    [Tooltip("Layer used only by the HUD coin (hidden from the main camera)")]
    public int hudCoinLayer = 31;

    static readonly Color Cream = new Color(1f, 0.97f, 0.93f, 0.97f);
    static readonly Color Ink = new Color(0.23f, 0.16f, 0.33f);
    static readonly Color Night = new Color(0.16f, 0.12f, 0.27f, 0.88f);
    static readonly Color Peach = new Color(1f, 0.72f, 0.6f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.45f);
    static readonly Color Mint = new Color(0.56f, 0.9f, 0.77f);
    static readonly Color Lilac = new Color(0.8f, 0.74f, 0.95f);

    private Canvas canvas;
    private RectTransform root;
    private Font font;

    private RectTransform coinPanel;
    private Text coinText;
    private float coinPunch;
    private Transform hudCoin;
    private RenderTexture hudCoinRT;
    private float coinSpin; // extra spin (degrees) when coins are earned
    private Text cupText, hintText;
    private RectTransform cupPanel;

    public RectTransform Root => root;

    private RectTransform prompt;
    private Text promptText;
    private RectTransform promptKey;
    private RectTransform promptBar, promptFill;
    private Vector3 promptWorld;
    private bool promptVisible;

    private RectTransform toast;
    private Text toastText;
    private CanvasGroup toastGroup;
    private float toastStart = -10f;

    // localization: every created text (to swap fonts) + texts bound to a key
    private readonly List<Text> texts = new List<Text>();
    private readonly HashSet<Text> fixedFont = new HashSet<Text>();
    private readonly Dictionary<Text, FontStyle> styles = new Dictionary<Text, FontStyle>();
    private readonly List<KeyValuePair<Text, System.Func<string>>> bindings = new List<KeyValuePair<Text, System.Func<string>>>();
    private bool building;
    private List<Ingredient> lastCup;

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private readonly List<FloatText> floats = new List<FloatText>();

    void Awake()
    {
        Instance = this;
        font = Loc.Font;
        Loc.Changed += OnLanguageChanged;
        BuildCanvas();
        BuildHud();
        BuildPrompt();
        CoinWallet.Changed += OnCoins;
        OnCoins(CoinWallet.Coins, 0);
        SetCup(null);
    }

    void OnDestroy()
    {
        CoinWallet.Changed -= OnCoins;
        Loc.Changed -= OnLanguageChanged;
        if (hudCoinRT != null) hudCoinRT.Release();
        if (Instance == this) Instance = null;
    }

    // ---------------- building blocks ----------------
    public RectTransform Panel(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    public Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        styles[t] = style;
        t.fontStyle = Loc.IsCJK ? FontStyle.Normal : style;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        texts.Add(t);
        return t;
    }

    // Text that refreshes itself when the language changes
    public void Bind(Text t, System.Func<string> getText)
    {
        bindings.Add(new KeyValuePair<Text, System.Func<string>>(t, getText));
        t.text = getText();
    }

    // Text with a fixed font (e.g. each language name written in its own script)
    public void FixFont(Text t, Font f)
    {
        t.font = f;
        fixedFont.Add(t);
    }

    void OnLanguageChanged()
    {
        font = Loc.Font;
        texts.RemoveAll(t => t == null);
        foreach (var t in texts)
        {
            if (fixedFont.Contains(t)) continue;
            t.font = font;
            if (styles.TryGetValue(t, out var st)) t.fontStyle = Loc.IsCJK ? FontStyle.Normal : st;
        }
        bindings.RemoveAll(b => b.Key == null);
        foreach (var b in bindings) b.Key.text = b.Value();
        SetCup(lastCup);
        if (hintText != null) hintText.text = Loc.Get(building ? "hint.build" : "hint.play");
    }

    public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    void BuildCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)go.transform;
    }

    void BuildHud()
    {
        // Star Coins (top-left corner)
        coinPanel = Panel("Moedas", root, UISprites.Rounded, Night, new Vector2(300, 84));
        Anchor(coinPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -32));
        if (!BuildHudCoin())
        {
            // no 3D model: fall back to a drawn star
            var star = Panel("Estrela", coinPanel, UISprites.Star, Gold, new Vector2(56, 56));
            Anchor(star, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 0));
        }
        coinText = Label("Valor", coinPanel, "0", 40, Cream, TextAnchor.MiddleLeft);
        coinText.rectTransform.sizeDelta = new Vector2(200, 50);
        Anchor(coinText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(86, 9));
        var sub = Label("Rotulo", coinPanel, "", 18, Lilac, TextAnchor.MiddleLeft, FontStyle.Normal);
        Bind(sub, () => Loc.Get("ui.coins"));
        sub.rectTransform.sizeDelta = new Vector2(200, 24);
        Anchor(sub.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(88, -22));

        // current cup (bottom center)
        var cup = cupPanel = Panel("Xicara", root, UISprites.Rounded, Night, new Vector2(680, 64));
        Anchor(cup, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32));
        cupText = Label("Texto", cup, "", 24, Cream, TextAnchor.MiddleCenter);
        Stretch(cupText.rectTransform, 16, 16);

        // controls hint (top-right corner)
        var hint = hintText = Label("Controles", root, Loc.Get("hint.play"), 18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperRight, FontStyle.Normal);
        hint.rectTransform.sizeDelta = new Vector2(900, 30);
        Anchor(hint.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -36));
    }

    // 3D Star Coin: a mini "studio" far from the island with its own camera rendering to a texture
    bool BuildHudCoin()
    {
        if (hudCoinModel == null) return false;
        int mask = 1 << hudCoinLayer;

        var rig = new GameObject("HudMoedaEstudio");
        rig.transform.SetParent(transform, false);
        rig.transform.position = new Vector3(0f, -500f, 0f);

        var coin = Instantiate(hudCoinModel, rig.transform);
        coin.name = "MoedaHUD";
        coin.SetActive(true);
        coin.transform.localPosition = Vector3.zero;
        coin.transform.localScale = Vector3.one;
        foreach (var t in coin.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = hudCoinLayer;
        hudCoin = coin.transform;

        hudCoinRT = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "MoedaHUD_RT" };
        var cam = new GameObject("HudMoedaCamera").AddComponent<Camera>();
        cam.transform.SetParent(rig.transform, false);
        cam.transform.localPosition = new Vector3(0f, 0.47f, -2.7f);
        cam.transform.LookAt(rig.transform.position + new Vector3(0f, 0.47f, 0f));
        cam.fieldOfView = 24f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = mask;
        cam.allowHDR = false;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.targetTexture = hudCoinRT;

        var key = new GameObject("HudMoedaLuz").AddComponent<Light>();
        key.transform.SetParent(rig.transform, false);
        key.type = LightType.Directional;
        key.transform.rotation = Quaternion.Euler(25f, -35f, 0f);
        key.intensity = 1.1f;
        key.color = new Color(1f, 0.95f, 0.88f);
        key.cullingMask = mask;

        // the main camera does not see the HUD coin
        if (Camera.main != null) Camera.main.cullingMask &= ~mask;

        var go = new GameObject("MoedaImagem", typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(coinPanel, false);
        rt.sizeDelta = new Vector2(78, 78);
        Anchor(rt, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 0));
        var img = go.GetComponent<RawImage>();
        img.texture = hudCoinRT;
        img.raycastTarget = false;
        return true;
    }

    void BuildPrompt()
    {
        prompt = Panel("Aviso", root, UISprites.Rounded, Night, new Vector2(300, 58));
        prompt.pivot = new Vector2(0.5f, 0f);
        promptKey = Panel("Tecla", prompt, UISprites.Circle, Peach, new Vector2(40, 40));
        Anchor(promptKey, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32, 0));
        var keyText = Label("E", promptKey, "E", 24, Ink, TextAnchor.MiddleCenter);
        Stretch(keyText.rectTransform);
        promptText = Label("Texto", prompt, "", 22, Cream, TextAnchor.MiddleLeft);
        Stretch(promptText.rectTransform, 62, 18);
        promptBar = Panel("Barra", prompt, null, new Color(1f, 1f, 1f, 0.15f), Vector2.zero);
        promptBar.anchorMin = new Vector2(0, 0);
        promptBar.anchorMax = new Vector2(1, 0);
        promptBar.offsetMin = new Vector2(18, -14);
        promptBar.offsetMax = new Vector2(-18, -7);
        promptFill = Panel("Progresso", promptBar, null, Mint, Vector2.zero);
        promptFill.anchorMin = Vector2.zero;
        promptFill.anchorMax = new Vector2(0, 1);
        promptFill.offsetMin = promptFill.offsetMax = Vector2.zero;
        prompt.gameObject.SetActive(false);
    }

    // ---------------- API ----------------

    // Title screen: hides the HUD (coins, cup, hints, bubbles)
    public void SetHudVisible(bool visible)
    {
        if (coinPanel != null) coinPanel.gameObject.SetActive(visible);
        if (cupPanel != null) cupPanel.gameObject.SetActive(visible && !building);
        if (hintText != null) hintText.gameObject.SetActive(visible);
        foreach (var b in bubbles) if (b.rt != null) b.rt.gameObject.SetActive(visible);
        if (!visible) HidePrompt();
    }

    // Subtle corner toast (e.g. "Game saved")
    public void ShowToast(string text)
    {
        if (toast == null)
        {
            toast = Panel("Aviso_Salvo", root, UISprites.Rounded, Night, new Vector2(190, 44));
            Anchor(toast, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28));
            var dot = Panel("Estrela", toast, UISprites.Star, Mint, new Vector2(22, 22));
            Anchor(dot, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24, 0));
            toastText = Label("Texto", toast, "", 19, Cream, TextAnchor.MiddleLeft, FontStyle.Normal);
            Stretch(toastText.rectTransform, 44, 14);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        }
        toastText.text = text;
        toast.sizeDelta = new Vector2(toastText.preferredWidth + 62f, 44f);
        toastStart = Time.unscaledTime;
    }

    // Build Mode: hides the cup and swaps the controls hint
    public void SetBuildHud(bool building)
    {
        if (cupPanel != null) cupPanel.gameObject.SetActive(!building);
        this.building = building;
        if (hintText != null)
        {
            hintText.text = Loc.Get(building ? "hint.build" : "hint.play");
            // while building the banner takes the top, so the hint sits centered just below it
            if (building) Anchor(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f));
            else Anchor(hintText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -36));
            hintText.alignment = building ? TextAnchor.UpperCenter : TextAnchor.UpperRight;
        }
        if (building) HidePrompt();
    }

    void OnCoins(int total, int delta)
    {
        if (coinText != null) coinText.text = total.ToString();
        if (delta > 0) { coinPunch = 1f; coinSpin += 360f; }
    }

    public void SetCup(IReadOnlyList<Ingredient> cup)
    {
        if (cupText == null) return;
        lastCup = cup == null ? null : new List<Ingredient>(cup);
        if (cup == null || cup.Count == 0)
        {
            cupText.text = "<color=#b7a9d9>" + Loc.Get("cup.empty") + "</color>";
            return;
        }
        var parts = new List<string>();
        foreach (var i in cup)
            parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(IngredientInfo.UiOnDark(i))}>{IngredientInfo.Name(i)}</color>");
        var match = RecipeBook.Find(cup);
        cupText.text = Loc.Get("cup.label") + " " + string.Join(" + ", parts) + (match != null ? $"   <color=#8ee6c4>= {match.Name}</color>" : "");
    }

    // progress < 0: plain prompt; progress >= 0: action progress bar
    public void SetPrompt(Vector3 world, string text, bool enabled, float progress)
    {
        promptVisible = true;
        promptWorld = world;
        prompt.gameObject.SetActive(true);
        bool showKey = enabled && progress < 0f;
        promptKey.gameObject.SetActive(showKey);
        promptText.text = text;
        promptText.color = enabled ? Cream : new Color(1f, 1f, 1f, 0.55f);
        float left = showKey ? 62f : 22f;
        promptText.rectTransform.offsetMin = new Vector2(left, 0f);
        prompt.sizeDelta = new Vector2(Mathf.Max(170f, promptText.preferredWidth + left + 24f), 58f);
        bool showBar = progress >= 0f;
        promptBar.gameObject.SetActive(showBar);
        if (showBar) promptFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    public void HidePrompt()
    {
        promptVisible = false;
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    public void ShowPopup(Vector3 world, string text, Color color)
    {
        var t = Label("TextoFlutuante", root, text, 30, color, TextAnchor.MiddleCenter);
        t.rectTransform.sizeDelta = new Vector2(500, 50);
        var outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.08f, 0.2f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        floats.Add(new FloatText { rt = t.rectTransform, text = t, world = world, start = Time.time, color = color });
    }

    public Bubble CreateBubble(Transform anchor, float height)
    {
        var rt = Panel("Balao", root, UISprites.Rounded, Cream, new Vector2(220, 92));
        rt.pivot = new Vector2(0.5f, 0f);
        rt.SetSiblingIndex(0); // behind the prompt and the HUD
        var dot1 = Panel("Bolinha1", rt, UISprites.Circle, Cream, new Vector2(20, 20));
        Anchor(dot1, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-18, -14));
        var dot2 = Panel("Bolinha2", rt, UISprites.Circle, Cream, new Vector2(11, 11));
        Anchor(dot2, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-30, -33));
        var title = Label("Titulo", rt, "", 24, Ink, TextAnchor.UpperCenter);
        var details = Label("Detalhes", rt, "", 18, new Color(0.42f, 0.33f, 0.55f), TextAnchor.LowerCenter, FontStyle.Normal);
        var b = new Bubble { rt = rt, title = title, details = details, anchor = anchor, height = height, owner = this };
        bubbles.Add(b);
        return b;
    }

    // ---------------- screen placement ----------------
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        float now = Time.time;

        if (promptVisible) Place(prompt, cam, promptWorld, 0f);

        for (int i = bubbles.Count - 1; i >= 0; i--)
        {
            var b = bubbles[i];
            if (b.anchor == null) { b.Close(); continue; }
            if (b.sayUntil > 0f && now >= b.sayUntil) { b.sayUntil = 0f; b.Apply(b.baseTitle, b.baseDetails); }
            Place(b.rt, cam, b.anchor.position + Vector3.up * b.height, 16f);
        }

        for (int i = floats.Count - 1; i >= 0; i--)
        {
            var f = floats[i];
            float a = (now - f.start) / 1.4f;
            if (a >= 1f) { Destroy(f.rt.gameObject); floats.RemoveAt(i); continue; }
            Place(f.rt, cam, f.world, 10f + a * 90f);
            var c = f.color; c.a = 1f - a * a; f.text.color = c;
        }

        if (toastGroup != null)
        {
            float age = Time.unscaledTime - toastStart;
            toastGroup.alpha = age < 0.2f ? age / 0.2f : age < 1.6f ? 1f : Mathf.Clamp01(1f - (age - 1.6f) / 0.6f);
        }

        coinPunch = Mathf.MoveTowards(coinPunch, 0f, Time.unscaledDeltaTime * 3f);
        coinPanel.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(coinPunch * Mathf.PI));

        // 3D coin: sways slowly and spins a full turn when coins are earned
        if (hudCoin != null)
        {
            coinSpin = Mathf.MoveTowards(coinSpin, 0f, Time.unscaledDeltaTime * Mathf.Max(360f, coinSpin * 2.5f));
            float sway = Mathf.Sin(Time.unscaledTime * 1.3f) * 22f;
            float bob = Mathf.Sin(Time.unscaledTime * 2.1f) * 0.03f;
            hudCoin.localRotation = Quaternion.Euler(0f, 180f + sway + coinSpin, Mathf.Sin(Time.unscaledTime * 0.9f) * 5f);
            hudCoin.localPosition = new Vector3(0f, bob, 0f);
        }
    }

    void Place(RectTransform rt, Camera cam, Vector3 world, float offsetY)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f) sp = new Vector3(-10000f, -10000f, 0f);
        var p = new Vector2(sp.x, sp.y + offsetY * canvas.scaleFactor);
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) rt.position = new Vector3(p.x, p.y, 0f);
        else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(root, p, canvas.worldCamera, out Vector3 w)) rt.position = w; // trailer
    }

    public Canvas Canvas => canvas;

    // corner controls hint (hidden by the trailer)
    public void SetHintVisible(bool visible)
    {
        if (hintText != null) hintText.gameObject.SetActive(visible);
    }

    // ---------------- types ----------------
    class FloatText
    {
        public RectTransform rt;
        public Text text;
        public Vector3 world;
        public float start;
        public Color color;
    }

    public class Bubble
    {
        internal RectTransform rt;
        internal Text title, details;
        internal Transform anchor;
        internal float height, sayUntil;
        internal string baseTitle, baseDetails;
        internal GameUI owner;

        public void Set(string t, string d)
        {
            baseTitle = t;
            baseDetails = d;
            if (sayUntil <= 0f) Apply(t, d);
        }

        public void Say(string t, float seconds)
        {
            sayUntil = Time.time + seconds;
            Apply(t, null);
        }

        internal void Apply(string t, string d)
        {
            if (rt == null) return;
            bool hasDetails = !string.IsNullOrEmpty(d);
            title.text = t;
            details.text = d ?? "";
            details.gameObject.SetActive(hasDetails);
            title.alignment = hasDetails ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
            Stretch(title.rectTransform, 14, 14, hasDetails ? 14 : 0, 0);
            Stretch(details.rectTransform, 14, 14, 0, 16);
            float w = Mathf.Max(title.preferredWidth, hasDetails ? details.preferredWidth : 0f) + 52f;
            rt.sizeDelta = new Vector2(Mathf.Max(160f, w), hasDetails ? 94f : 62f);
        }

        public void Close()
        {
            if (rt != null) Destroy(rt.gameObject);
            rt = null;
            owner?.bubbles.Remove(this);
        }
    }
}
