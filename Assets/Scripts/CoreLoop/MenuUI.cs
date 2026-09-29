using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menus do jogo (uGUI por código): tela inicial, pausa (Esc), opções
// (áudio, idioma com bandeiras, vídeo, controles) e confirmação de "Novo Jogo".
public class MenuUI : MonoBehaviour
{
    public static MenuUI Instance { get; private set; }
    // true enquanto a tela inicial ou a pausa estão abertas (bloqueia o jogo)
    public static bool IsBlocking => Instance != null && Instance.state != State.Playing;

    enum State { Title, Playing, Paused }

    static bool skipTitleOnce; // "Novo Jogo" recarrega a cena direto no jogo

    static readonly Color Night = new Color(0.14f, 0.1f, 0.24f, 0.94f);
    static readonly Color Card = new Color(0.24f, 0.18f, 0.38f, 1f);
    static readonly Color CardHover = new Color(0.4f, 0.3f, 0.52f, 1f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.93f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.45f);
    static readonly Color Peach = new Color(1f, 0.72f, 0.6f);
    static readonly Color Mint = new Color(0.56f, 0.9f, 0.77f);
    static readonly Color Lilac = new Color(0.8f, 0.74f, 0.95f);
    static readonly Color Dim = new Color(0.06f, 0.04f, 0.12f, 0.55f);

    State state = State.Title;
    GameUI ui;
    RectTransform root, titleScreen, pauseScreen, optionsScreen, confirmScreen;
    RectTransform[] optionPages;
    Image[] optionTabs;
    readonly List<(Lang lang, Image bg)> langCards = new List<(Lang, Image)>();
    Button continueButton, firstTitleButton, firstPauseButton;
    bool optionsFromPause;
    int optionsTab;

    RobotMovement roMove;
    PlayerInteractor roInteract;
    IsometricCameraFollow cam;
    CustomerSpawner spawner;
    Text resolutionValue, qualityValue, fullscreenValue, vsyncValue;
    readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    void Awake()
    {
        Instance = this;
        GameSettings.Load();
        var ro = GameObject.Find("Ro");
        if (ro != null) { roMove = ro.GetComponent<RobotMovement>(); roInteract = ro.GetComponent<PlayerInteractor>(); }
        cam = FindAnyObjectByType<IsometricCameraFollow>();
        spawner = FindAnyObjectByType<CustomerSpawner>();
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    void OnDestroy()
    {
        Loc.Changed -= RefreshVideo;
        if (Instance == this) { Instance = null; Time.timeScale = 1f; }
    }

    void Start()
    {
        ui = GameUI.Instance;
        GameSettings.ApplyVideo();
        Build();
        if (skipTitleOnce) { skipTitleOnce = false; StartPlaying(); }
        else ShowTitle();
    }

    // ================================================================== estados
    void ShowTitle()
    {
        state = State.Title;
        Time.timeScale = 1f;
        SetGameplay(false);
        if (cam != null) cam.menuView = true;
        if (ui != null) ui.SetHudVisible(false);
        continueButton.gameObject.SetActive(System.IO.File.Exists(SaveSystem.SavePath));
        Show(titleScreen);
        Select(continueButton.gameObject.activeSelf ? continueButton : firstTitleButton);
    }

    // começa a jogar sem passar pelo menu (usado pelo trailer)
    public void StartGameNow() => StartPlaying();

    void StartPlaying()
    {
        state = State.Playing;
        Time.timeScale = 1f;
        SetGameplay(true);
        if (cam != null) cam.menuView = false;
        if (ui != null) ui.SetHudVisible(true);
        Show(null);
    }

    void Pause()
    {
        state = State.Paused;
        Time.timeScale = 0f;
        SetGameplay(false);
        Show(pauseScreen);
        Select(firstPauseButton);
    }

    void SetGameplay(bool on)
    {
        if (roMove != null) roMove.movementLocked = !on;
        if (roInteract != null) roInteract.enabled = on;
        if (spawner != null) spawner.paused = !on || BuildMode.IsActive;
        if (!on && ui != null) ui.HidePrompt();
    }

    void Show(RectTransform screen)
    {
        root.gameObject.SetActive(screen != null);
        foreach (var s in new[] { titleScreen, pauseScreen, optionsScreen, confirmScreen })
            s.gameObject.SetActive(s == screen);
    }

    void Select(Selectable s)
    {
        if (EventSystem.current != null && s != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        if (BuildMode.LastEscFrame == Time.frameCount) return; // o Esc já fechou o Modo Construção

        if (confirmScreen.gameObject.activeSelf) { Show(titleScreen); return; }
        if (optionsScreen.gameObject.activeSelf) { CloseOptions(); return; }
        if (state == State.Playing && !BuildMode.IsActive) Pause();
        else if (state == State.Paused) StartPlaying();
    }

    // ================================================================== ações
    void OnContinue() { Click(); StartPlaying(); }

    void OnNewGame()
    {
        Click();
        if (System.IO.File.Exists(SaveSystem.SavePath)) { Show(confirmScreen); return; }
        StartPlaying();
    }

    void ConfirmNewGame()
    {
        Click();
        try { System.IO.File.Delete(SaveSystem.SavePath); } catch { }
        if (SaveSystem.Instance != null) SaveSystem.Instance.enabled = false; // não salva de novo antes de recarregar
        CoinWallet.Set(0);
        skipTitleOnce = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnQuit()
    {
        Click();
        if (SaveSystem.Instance != null) SaveSystem.Instance.Save(false);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnMainMenu()
    {
        Click();
        if (SaveSystem.Instance != null) SaveSystem.Instance.Save(false);
        if (BuildMode.IsActive) BuildMode.Instance.Exit();
        ShowTitle();
    }

    void OpenOptions(bool fromPause)
    {
        Click();
        optionsFromPause = fromPause;
        Show(optionsScreen);
        ShowOptionsTab(optionsTab);
    }

    void CloseOptions()
    {
        Click();
        GameSettings.Save();
        if (optionsFromPause) { Show(pauseScreen); Select(firstPauseButton); }
        else { Show(titleScreen); Select(continueButton.gameObject.activeSelf ? continueButton : firstTitleButton); }
    }

    static void Click() { if (AudioManager.Instance != null) AudioManager.Instance.Play("ui_click"); }

    // ================================================================== construção da UI
    void Build()
    {
        var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        BuildTitle();
        BuildPause();
        BuildOptions();
        BuildConfirm();
    }

    RectTransform MakeScreen(string name, Color? dim = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        GameUI.Stretch(rt);
        var img = go.GetComponent<Image>();
        img.color = dim ?? Color.clear;
        img.raycastTarget = dim.HasValue;
        return rt;
    }

    Text LabelKey(Transform parent, string key, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Bold)
    {
        var t = ui.Label("Texto", parent, "", size, color, align, style);
        ui.Bind(t, () => Loc.Get(key));
        return t;
    }

    Button MakeButton(Transform parent, string key, Vector2 size, System.Action onClick, Color? color = null)
    {
        var rt = ui.Panel("Botao_" + key, parent, UISprites.Rounded, Color.white, size);
        var img = rt.GetComponent<Image>();
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.normalColor = color ?? Card;
        cb.highlightedColor = CardHover;
        cb.selectedColor = CardHover;
        cb.pressedColor = Peach;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.onClick.AddListener(() => onClick());
        if (!string.IsNullOrEmpty(key))
        {
            var t = LabelKey(rt, key, 30, Cream, TextAnchor.MiddleCenter);
            GameUI.Stretch(t.rectTransform, 16, 16);
        }
        return b;
    }

    RectTransform Column(Transform parent, Vector2 anchor, Vector2 pos, float spacing)
    {
        var go = new GameObject("Coluna", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        GameUI.Anchor(rt, anchor, anchor, pos);
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = v.childControlHeight = false;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        var f = go.GetComponent<ContentSizeFitter>();
        f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    // ---------------------------------------------------------------- tela inicial
    void BuildTitle()
    {
        titleScreen = MakeScreen("TelaInicial");
        // degradê suave à esquerda para o texto aparecer sobre a ilha
        var shade = ui.Panel("Sombra", titleScreen, null, new Color(0.08f, 0.05f, 0.16f, 0.55f), Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = new Vector2(0.42f, 1f); shade.offsetMin = shade.offsetMax = Vector2.zero;

        var logo = ui.Label("Logo", titleScreen, "Cosmic Brew", 118, Gold, TextAnchor.MiddleCenter);
        logo.rectTransform.sizeDelta = new Vector2(760, 150);
        GameUI.Anchor(logo.rectTransform, new Vector2(0.2f, 0.76f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var shadow = logo.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0.35f, 0.15f, 0.4f, 0.8f); shadow.effectDistance = new Vector2(5, -6);
        ui.FixFont(logo, Loc.FontFor(Lang.EN)); // o nome do jogo é sempre em latim
        var star = ui.Panel("Estrela", titleScreen, UISprites.Star, Peach, new Vector2(60, 60));
        GameUI.Anchor(star, new Vector2(0.2f, 0.76f), new Vector2(0.5f, 0.5f), new Vector2(340, 58));
        var sub = LabelKey(titleScreen, "menu.subtitle", 30, Lilac, TextAnchor.MiddleCenter, FontStyle.Normal);
        sub.rectTransform.sizeDelta = new Vector2(760, 50);
        GameUI.Anchor(sub.rectTransform, new Vector2(0.2f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero);

        var col = Column(titleScreen, new Vector2(0.2f, 0.36f), Vector2.zero, 18f);
        var size = new Vector2(420, 78);
        continueButton = MakeButton(col, "menu.continue", size, OnContinue, new Color(0.36f, 0.24f, 0.46f, 1f));
        firstTitleButton = MakeButton(col, "menu.new", size, OnNewGame);
        MakeButton(col, "menu.options", size, () => OpenOptions(false));
        MakeButton(col, "menu.quit", size, OnQuit);

        // troca rápida de idioma pelas bandeirinhas
        var flags = new GameObject("Idiomas", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var frt = (RectTransform)flags.transform; frt.SetParent(titleScreen, false);
        GameUI.Anchor(frt, new Vector2(0.2f, 0.08f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var h = flags.GetComponent<HorizontalLayoutGroup>(); h.spacing = 14; h.childControlWidth = h.childControlHeight = false;
        var fit = flags.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < 6; i++)
        {
            var lang = (Lang)i;
            var f = ui.Panel("Bandeira_" + Loc.Codes[i], frt, FlagSprites.For(lang), Color.white, new Vector2(60, 40));
            var img = f.GetComponent<Image>(); img.raycastTarget = true;
            var b = f.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f); cb.normalColor = new Color(0.85f, 0.85f, 0.85f); b.colors = cb;
            b.onClick.AddListener(() => { Click(); Loc.Set(lang); RefreshLangCards(); });
        }

        var version = ui.Label("Versao", titleScreen, "v0.1", 18, new Color(1, 1, 1, 0.4f), TextAnchor.LowerRight, FontStyle.Normal);
        version.rectTransform.sizeDelta = new Vector2(200, 30);
        GameUI.Anchor(version.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 18));
    }

    // ---------------------------------------------------------------- pausa
    void BuildPause()
    {
        pauseScreen = MakeScreen("Pausa", Dim);
        var panel = ui.Panel("Painel", pauseScreen, UISprites.Rounded, Night, new Vector2(520, 470));
        GameUI.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var title = LabelKey(panel, "pause.title", 44, Gold, TextAnchor.MiddleCenter);
        title.rectTransform.sizeDelta = new Vector2(480, 70);
        GameUI.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24));
        var col = Column(panel, new Vector2(0.5f, 0.42f), Vector2.zero, 16f);
        var size = new Vector2(400, 72);
        firstPauseButton = MakeButton(col, "pause.resume", size, () => { Click(); StartPlaying(); }, new Color(0.36f, 0.24f, 0.46f, 1f));
        MakeButton(col, "menu.options", size, () => OpenOptions(true));
        MakeButton(col, "pause.main_menu", size, OnMainMenu);
    }

    // ---------------------------------------------------------------- confirmação
    void BuildConfirm()
    {
        confirmScreen = MakeScreen("Confirmar", Dim);
        var panel = ui.Panel("Painel", confirmScreen, UISprites.Rounded, Night, new Vector2(760, 320));
        GameUI.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var q = LabelKey(panel, "menu.confirm_new", 30, Cream, TextAnchor.MiddleCenter, FontStyle.Normal);
        q.horizontalOverflow = HorizontalWrapMode.Wrap;
        q.rectTransform.sizeDelta = new Vector2(660, 150);
        GameUI.Anchor(q.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30));
        var yes = MakeButton(panel, "menu.yes", new Vector2(250, 72), ConfirmNewGame, new Color(0.62f, 0.3f, 0.42f, 1f));
        GameUI.Anchor((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-140, 34));
        var no = MakeButton(panel, "menu.no", new Vector2(250, 72), () => { Click(); Show(titleScreen); Select(firstTitleButton); });
        GameUI.Anchor((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(140, 34));
    }

    // ---------------------------------------------------------------- opções
    void BuildOptions()
    {
        optionsScreen = MakeScreen("Opcoes", Dim);
        var panel = ui.Panel("Painel", optionsScreen, UISprites.Rounded, Night, new Vector2(1180, 760));
        GameUI.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var title = LabelKey(panel, "menu.options", 44, Gold, TextAnchor.MiddleLeft);
        title.rectTransform.sizeDelta = new Vector2(600, 70);
        GameUI.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44, -22));

        string[] tabs = { "opt.tab_audio", "opt.tab_language", "opt.tab_video", "opt.tab_controls" };
        optionTabs = new Image[tabs.Length];
        optionPages = new RectTransform[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            var b = MakeButton(panel, tabs[i], new Vector2(250, 60), () => { Click(); ShowOptionsTab(idx); });
            GameUI.Anchor((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44 + i * 272, -106));
            b.GetComponentInChildren<Text>().fontSize = 25;
            optionTabs[i] = b.GetComponent<Image>();
            var page = new GameObject("Pagina_" + tabs[i], typeof(RectTransform));
            var prt = (RectTransform)page.transform; prt.SetParent(panel, false);
            prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 1);
            prt.offsetMin = new Vector2(44, 120); prt.offsetMax = new Vector2(-44, -190);
            optionPages[i] = prt;
        }
        var back = MakeButton(panel, "menu.back", new Vector2(240, 66), CloseOptions, new Color(0.36f, 0.24f, 0.46f, 1f));
        GameUI.Anchor((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44, 34));

        BuildAudioPage(optionPages[0]);
        BuildLanguagePage(optionPages[1]);
        BuildVideoPage(optionPages[2]);
        BuildControlsPage(optionPages[3]);
    }

    void ShowOptionsTab(int i)
    {
        optionsTab = i;
        for (int t = 0; t < optionPages.Length; t++)
        {
            optionPages[t].gameObject.SetActive(t == i);
            var b = optionTabs[t].GetComponent<Button>();
            var cb = b.colors; cb.normalColor = t == i ? new Color(0.5f, 0.36f, 0.58f, 1f) : Card; b.colors = cb;
        }
        if (i == 1) RefreshLangCards();
    }

    RectTransform Row(RectTransform page, int index, string key)
    {
        var row = new GameObject("Linha_" + key, typeof(RectTransform));
        var rt = (RectTransform)row.transform; rt.SetParent(page, false);
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 70); rt.anchoredPosition = new Vector2(0, -index * 84);
        var label = LabelKey(rt, key, 28, Cream, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = new Vector2(0, 0); label.rectTransform.anchorMax = new Vector2(0.34f, 1);
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return rt;
    }

    void BuildAudioPage(RectTransform page)
    {
        AddSlider(Row(page, 0, "opt.master"), () => GameSettings.Master, v => { GameSettings.Master = v; AudioListener.volume = v; });
        AddSlider(Row(page, 1, "opt.music"), () => GameSettings.Music, v => GameSettings.Music = v);
        AddSlider(Row(page, 2, "opt.sfx"), () => GameSettings.Sfx, v => { GameSettings.Sfx = v; }, "cup_clink");
        AddSlider(Row(page, 3, "opt.characters"), () => GameSettings.Characters, v => GameSettings.Characters = v, "customer_ahh");
        AddSlider(Row(page, 4, "opt.ambience"), () => GameSettings.Ambience, v => GameSettings.Ambience = v);
    }

    void AddSlider(RectTransform row, System.Func<float> get, System.Action<float> set, string previewSfx = null)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var rt = (RectTransform)go.transform; rt.SetParent(row, false);
        rt.anchorMin = new Vector2(0.36f, 0.5f); rt.anchorMax = new Vector2(0.86f, 0.5f); rt.sizeDelta = new Vector2(0, 30); rt.anchoredPosition = Vector2.zero;
        var bg = ui.Panel("Fundo", rt, UISprites.Rounded, new Color(1, 1, 1, 0.12f), Vector2.zero);
        bg.anchorMin = new Vector2(0, 0.25f); bg.anchorMax = new Vector2(1, 0.75f); bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().type = Image.Type.Simple; bg.GetComponent<Image>().sprite = null;
        var fillArea = new GameObject("AreaPreenchimento", typeof(RectTransform));
        var fa = (RectTransform)fillArea.transform; fa.SetParent(rt, false);
        fa.anchorMin = new Vector2(0, 0.25f); fa.anchorMax = new Vector2(1, 0.75f); fa.offsetMin = fa.offsetMax = Vector2.zero;
        var fill = ui.Panel("Preenchimento", fa, null, Mint, Vector2.zero);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
        var handleArea = new GameObject("AreaAlca", typeof(RectTransform));
        var ha = (RectTransform)handleArea.transform; ha.SetParent(rt, false);
        ha.anchorMin = Vector2.zero; ha.anchorMax = Vector2.one; ha.offsetMin = new Vector2(14, 0); ha.offsetMax = new Vector2(-14, 0);
        var handle = ui.Panel("Alca", ha, UISprites.Circle, Cream, new Vector2(34, 34));
        handle.GetComponent<Image>().raycastTarget = true;
        var s = go.GetComponent<Slider>();
        s.fillRect = fill; s.handleRect = handle; s.targetGraphic = handle.GetComponent<Image>();
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0; s.maxValue = 1;
        var cb = s.colors; cb.highlightedColor = Peach; cb.selectedColor = Peach; cb.pressedColor = Gold; s.colors = cb;
        s.value = get();
        var pct = ui.Label("Valor", row, "", 26, Gold, TextAnchor.MiddleRight);
        pct.rectTransform.anchorMin = new Vector2(0.88f, 0); pct.rectTransform.anchorMax = new Vector2(1, 1);
        pct.rectTransform.offsetMin = pct.rectTransform.offsetMax = Vector2.zero;
        pct.text = Mathf.RoundToInt(s.value * 100) + "%";
        float lastPreview = 0f;
        s.onValueChanged.AddListener(v =>
        {
            set(v);
            pct.text = Mathf.RoundToInt(v * 100) + "%";
            if (previewSfx != null && Time.unscaledTime - lastPreview > 0.25f && AudioManager.Instance != null)
            {
                lastPreview = Time.unscaledTime;
                AudioManager.Instance.Play(previewSfx);
            }
        });
    }

    void BuildLanguagePage(RectTransform page)
    {
        for (int i = 0; i < 6; i++)
        {
            var lang = (Lang)i;
            var card = ui.Panel("Idioma_" + Loc.Codes[i], page, UISprites.Rounded, Card, new Vector2(340, 150));
            GameUI.Anchor(card, new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % 3) * 364, -(i / 3) * 172));
            var img = card.GetComponent<Image>(); img.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.selectedColor = cb.highlightedColor; b.colors = cb;
            b.onClick.AddListener(() => { Click(); Loc.Set(lang); RefreshLangCards(); });
            var flag = ui.Panel("Bandeira", card, FlagSprites.For(lang), Color.white, new Vector2(108, 72));
            GameUI.Anchor(flag, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0));
            var name = ui.Label("Nome", card, Loc.NativeNames[i], 30, Cream, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0, 0); name.rectTransform.anchorMax = new Vector2(1, 1);
            name.rectTransform.offsetMin = new Vector2(146, 0); name.rectTransform.offsetMax = new Vector2(-12, 0);
            ui.FixFont(name, Loc.FontFor(lang)); // cada idioma escrito com a própria fonte
            langCards.Add((lang, img));
        }
    }

    void RefreshLangCards()
    {
        foreach (var (lang, bg) in langCards)
            bg.color = lang == Loc.Current ? new Color(0.52f, 0.36f, 0.6f, 1f) : Card;
    }

    void BuildVideoPage(RectTransform page)
    {
        foreach (var r in UnityEngine.Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(v)) resolutions.Add(v);
        }
        if (resolutions.Count == 0) resolutions.Add(new Vector2Int(1920, 1080));

        fullscreenValue = Selector(Row(page, 0, "opt.fullscreen"), () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; ApplyVideoAndRefresh(); },
                                   () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; ApplyVideoAndRefresh(); });
        resolutionValue = Selector(Row(page, 1, "opt.resolution"), () => StepResolution(-1), () => StepResolution(1));
        qualityValue = Selector(Row(page, 2, "opt.quality"), () => { GameSettings.Quality = Mathf.Max(0, GameSettings.Quality - 1); ApplyVideoAndRefresh(); },
                                () => { GameSettings.Quality = Mathf.Min(2, GameSettings.Quality + 1); ApplyVideoAndRefresh(); });
        vsyncValue = Selector(Row(page, 3, "opt.vsync"), () => { GameSettings.VSync = !GameSettings.VSync; ApplyVideoAndRefresh(); },
                              () => { GameSettings.VSync = !GameSettings.VSync; ApplyVideoAndRefresh(); });
        RefreshVideo();
        Loc.Changed += RefreshVideo;
    }

    void StepResolution(int dir)
    {
        int i = resolutions.IndexOf(new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight));
        i = i < 0 ? resolutions.Count - 1 : Mathf.Clamp(i + dir, 0, resolutions.Count - 1);
        GameSettings.ResolutionWidth = resolutions[i].x;
        GameSettings.ResolutionHeight = resolutions[i].y;
        ApplyVideoAndRefresh();
    }

    void ApplyVideoAndRefresh()
    {
        Click();
        GameSettings.ApplyVideo();
        GameSettings.Save();
        RefreshVideo();
    }

    void RefreshVideo()
    {
        if (fullscreenValue == null) return;
        fullscreenValue.text = Loc.Get(GameSettings.Fullscreen ? "opt.on" : "opt.off");
        vsyncValue.text = Loc.Get(GameSettings.VSync ? "opt.on" : "opt.off");
        qualityValue.text = Loc.Get(GameSettings.Quality <= 0 ? "opt.q_low" : GameSettings.Quality == 1 ? "opt.q_med" : "opt.q_high");
        resolutionValue.text = GameSettings.ResolutionWidth + " × " + GameSettings.ResolutionHeight;
    }

    Text Selector(RectTransform row, System.Action prev, System.Action next)
    {
        var left = MakeButton(row, null, new Vector2(64, 60), prev);
        GameUI.Anchor((RectTransform)left.transform, new Vector2(0.36f, 0.5f), new Vector2(0, 0.5f), Vector2.zero);
        var lt = ui.Label("<", left.transform, "<", 36, Cream, TextAnchor.MiddleCenter); GameUI.Stretch(lt.rectTransform, 0, 0, 0, 6);
        var right = MakeButton(row, null, new Vector2(64, 60), next);
        GameUI.Anchor((RectTransform)right.transform, new Vector2(0.86f, 0.5f), new Vector2(1, 0.5f), Vector2.zero);
        var rtx = ui.Label(">", right.transform, ">", 36, Cream, TextAnchor.MiddleCenter); GameUI.Stretch(rtx.rectTransform, 0, 0, 0, 6);
        var value = ui.Label("Valor", row, "", 28, Gold, TextAnchor.MiddleCenter);
        value.rectTransform.anchorMin = new Vector2(0.36f, 0); value.rectTransform.anchorMax = new Vector2(0.86f, 1);
        value.rectTransform.offsetMin = new Vector2(70, 0); value.rectTransform.offsetMax = new Vector2(-70, 0);
        return value;
    }

    void BuildControlsPage(RectTransform page)
    {
        (string keys, string action)[] rows =
        {
            ("W A S D", "ctl.move"), ("E  /  key.space", "ctl.interact"), ("B", "ctl.build"), ("Z  /  C", "ctl.rotate_cam"),
            ("key.wheel", "ctl.zoom"), ("M", "ctl.music"), ("Esc", "ctl.pause"), ("R", "ctl.rotate_item"), ("X", "ctl.store"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var (keys, action) = rows[i];
            int col = i < 5 ? 0 : 1;
            int line = i < 5 ? i : i - 5;
            var pill = ui.Panel("Tecla", page, UISprites.Rounded, Card, new Vector2(230, 54));
            GameUI.Anchor(pill, new Vector2(col * 0.5f, 1), new Vector2(0, 1), new Vector2(0, -line * 76));
            var kt = ui.Label("Texto", pill, "", 24, Gold, TextAnchor.MiddleCenter);
            GameUI.Stretch(kt.rectTransform, 8, 8);
            string captured = keys;
            ui.Bind(kt, () => captured.Replace("key.space", Loc.Get("key.space")).Replace("key.wheel", Loc.Get("key.wheel")));
            var at = LabelKey(page, action, 26, Cream, TextAnchor.MiddleLeft);
            at.rectTransform.sizeDelta = new Vector2(300, 54);
            GameUI.Anchor(at.rectTransform, new Vector2(col * 0.5f, 1), new Vector2(0, 1), new Vector2(250, -line * 76));
        }
    }

    // ------------------------------------------------------------------ utilidades p/ outros scripts
    public static void SkipTitleNextLoad() => skipTitleOnce = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => skipTitleOnce = false;
}
