using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Grava o trailer do jogo: roteiro de câmera + jogo sendo jogado sozinho, renderizado em 1920x1080
// (independente do tamanho da janela) e salvo quadro a quadro em JPG. O vídeo é montado depois
// com Tools/trailer/make_trailer.py (ffmpeg + música lo-fi).
// Uso (em Play): new GameObject("Trailer").AddComponent<TrailerDirector>().outDir = "...";
public class TrailerDirector : MonoBehaviour
{
    public string outDir = "Trailer/frames";
    public int width = 1920, height = 1080, fps = 30;

    public bool Done { get; private set; }
    public int FramesWritten => frame;

    static readonly Color Night = new Color(0.16f, 0.12f, 0.27f, 0.9f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.93f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.45f);

    private Camera cam;
    private IsometricCameraFollow follow;
    private RenderTexture rt;
    private Texture2D grab;
    private bool recording;
    private int frame;

    private CanvasGroup titleGroup, captionGroup, fadeGroup;
    private Text titleText, subText, captionText;
    private RectTransform captionPanel;

    private RobotMovement move;
    private PlayerInteractor interactor;
    private PlayerCarry carry;
    private CustomerSpawner spawner;
    private BuildMode build;
    private Lang oldLang;

    // ------------------------------------------------------------------ preparação
    IEnumerator Start()
    {
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir, "*.jpg")) File.Delete(f);

        // o trailer não mexe no save do jogador
        var save = FindAnyObjectByType<SaveSystem>();
        if (save != null) Destroy(save);

        oldLang = Loc.Current;
        Loc.Set(Lang.PT);
        Time.captureFramerate = fps;
        Cursor.visible = false;

        if (MenuUI.Instance != null) MenuUI.Instance.StartGameNow();
        if (GameUI.Instance != null) GameUI.Instance.SetHintVisible(false);

        var ro = GameObject.Find("Ro");
        move = ro.GetComponent<RobotMovement>();
        interactor = ro.GetComponent<PlayerInteractor>();
        carry = ro.GetComponent<PlayerCarry>();
        spawner = FindAnyObjectByType<CustomerSpawner>();
        build = BuildMode.Instance;
        build.scripted = true;

        cam = Camera.main;
        follow = cam.GetComponent<IsometricCameraFollow>();
        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "Trailer_RT" };
        cam.targetTexture = rt;
        grab = new Texture2D(width, height, TextureFormat.RGB24, false);

        // a UI do jogo passa a ser desenhada pela câmera (entra na textura do trailer)
        var gameCanvas = GameUI.Instance.Canvas;
        gameCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        gameCanvas.worldCamera = cam;
        gameCanvas.planeDistance = cam.nearClipPlane + 1f;
        BuildOverlay();

        StartCoroutine(Capture());
        yield return Script();

        recording = false;
        yield return null;
        File.WriteAllText(Path.Combine(outDir, "done.txt"), frame.ToString());
        Time.captureFramerate = 0;
        cam.targetTexture = null;
        Loc.Set(oldLang);
        Cursor.visible = true;
        Done = true;
        Debug.Log($"[Trailer] {frame} quadros em {outDir}");
    }

    IEnumerator Capture()
    {
        var eof = new WaitForEndOfFrame();
        while (true)
        {
            yield return eof;
            if (!recording) continue;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            grab.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outDir, $"f{frame:D5}.jpg"), grab.EncodeToJPG(93));
            frame++;
        }
    }

    // ------------------------------------------------------------------ roteiro
    IEnumerator Script()
    {
        // aquecimento (não gravado): já chegam alguns clientes
        CoinWallet.Set(34);
        spawner.spawnInterval = new Vector2(3f, 6f);
        spawner.ArriveNow();
        yield return Wait(0.7f);
        spawner.ArriveNow();
        yield return Wait(6f);
        fadeGroup.alpha = 1f;

        // 1 · título: a ilha flutuando no espaço
        recording = true;
        Hud(false);
        StartCoroutine(Fade(fadeGroup, 1f, 0f, 1.2f));
        StartCoroutine(TitleCard(Loc.Get("trailer.subtitle"), 0.9f, 4.6f));
        yield return Orbit(new Vector3(0f, 0.3f, 0.6f), 10.5f, 8.6f, 22f, 10f, 42f, 6.5f);
        Hud(true);

        // 2 · uma nave chega e o viajante desce
        var ship = spawner.ArriveNow();
        StartCoroutine(Caption(Loc.Get("trailer.cap_travelers"), 0.3f, 6.2f));
        if (ship != null) yield return FollowShip(ship, 7f);
        else yield return Orbit(new Vector3(0f, 0.3f, -3f), 5f, 4.5f, 28f, 30f, 50f, 5f);

        // 3 · o Ro prepara o pedido com calma, sem cronômetro
        follow.enabled = true;
        var customer = PickCustomer();
        StartCoroutine(Caption(Loc.Get("trailer.cap_brew"), 0.4f, 5.5f));
        if (customer != null)
        {
            foreach (var ing in customer.Order.ingredients)
            {
                var station = NearestStation(ing);
                if (station == null) continue;
                yield return WalkTo(station.transform.position, station.interactRadius * 0.75f, 7f);
                interactor.simulatePress = true;
                yield return Wait(station.duration + 0.35f);
            }
            yield return WalkTo(customer.transform.position, 1.5f, 7f);
            interactor.simulatePress = true;
            yield return Wait(0.9f);

            // 4 · close no "Ahhh…" e nas moedas
            follow.enabled = false;
            StartCoroutine(Caption(Loc.Get("trailer.cap_tips"), 0.2f, 3.4f));
            yield return Hold(customer.transform, 2.6f, 2.3f, 26f, 3.8f);
            follow.enabled = true;
        }

        // 5 · Modo Construção: comprar, posicionar e pintar
        CoinWallet.Set(260);
        build.Enter();
        StartCoroutine(Caption(Loc.Get("trailer.cap_build"), 0.9f, 10.5f, 360f));
        yield return Wait(1.4f);
        yield return BuyAndPlace("Lanterna Quente", new Vector3(-1.5f, 0f, -5.8f), 4.2f, 6.2f, 210f);
        yield return BuyAndPlace("Planeta de Brinquedo", new Vector3(3.5f, 0f, 3.5f), 3.6f, 6f, 40f);
        build.ScriptCancel();
        yield return Wait(0.4f);
        Decoration stool = null;
        foreach (var d in FindObjectsByType<Decoration>(FindObjectsSortMode.InstanceID)) if (d.isSeat && d.enabled) { stool = d; break; }
        if (stool != null)
        {
            build.ScriptSelect(stool);
            yield return Wait(0.7f);
            build.ScriptColor(2);
            yield return Wait(0.8f);
            build.ScriptColor(5);
            yield return Wait(0.8f);
        }
        build.ScriptSelectPart(0);
        yield return Wait(0.7f);
        build.ScriptColor(3);
        yield return Wait(1.1f);
        build.ScriptSelectPart(1);
        yield return Wait(0.5f);
        build.ScriptColor(2);
        yield return Wait(1.2f);
        build.Exit();

        // 6 · encerramento
        follow.enabled = false;
        spawner.ArriveNow();
        Hud(false);
        StartCoroutine(TitleCard(Loc.Get("trailer.tagline"), 0.6f, 6f));
        StartCoroutine(FadeLater(fadeGroup, 5.8f, 0f, 1f, 1.1f));
        yield return Orbit(new Vector3(0f, 0.3f, 0.6f), 8f, 9.6f, 24f, 200f, 232f, 7f);
    }

    // cenas de título: sem HUD, balões nem dicas
    void Hud(bool visible)
    {
        GameUI.Instance.SetHudVisible(visible);
        GameUI.Instance.SetHintVisible(false);
    }

    // ------------------------------------------------------------------ ações do roteiro
    Customer PickCustomer()
    {
        Customer best = null;
        foreach (var c in FindObjectsByType<Customer>(FindObjectsSortMode.None))
        {
            if (c.CurrentState != Customer.State.Waiting) continue;
            bool doable = true;
            foreach (var i in c.Order.ingredients) if (NearestStation(i) == null) doable = false;
            if (!doable) continue;
            if (best == null || c.Order.ingredients.Length == 2) best = c;
        }
        return best;
    }

    Station NearestStation(Ingredient ing)
    {
        Station best = null;
        float bestD = float.MaxValue;
        foreach (var s in FindObjectsByType<Station>(FindObjectsSortMode.None))
        {
            if (s.ingredient != ing || !s.isActiveAndEnabled) continue;
            float d = Vector3.Distance(s.transform.position, move.transform.position);
            if (d < bestD) { best = s; bestD = d; }
        }
        return best;
    }

    IEnumerator WalkTo(Vector3 target, float stopDistance, float timeout)
    {
        float t = 0f;
        while (t < timeout)
        {
            Vector3 d = target - move.transform.position; d.y = 0f;
            if (d.magnitude <= stopDistance) break;
            move.simulatedWorldDirection = d.normalized;
            t += Time.deltaTime;
            yield return null;
        }
        move.simulatedWorldDirection = Vector3.zero;
        yield return Wait(0.25f);
    }

    IEnumerator BuyAndPlace(string id, Vector3 from, float radiusMin, float radiusMax, float angleDeg)
    {
        var item = build.FindTemplate(id);
        if (item == null) yield break;
        build.ScriptBuy(item);
        // acha um lugar livre perto do ângulo pedido
        Vector3 spot = Vector3.zero;
        bool found = false;
        for (int k = 0; k < 24 && !found; k++)
            for (float r = radiusMin; r <= radiusMax && !found; r += 0.5f)
            {
                float a = (angleDeg + (k % 2 == 0 ? 1 : -1) * (k / 2) * 12f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                build.ScriptGhost(p, 0f);
                if (build.GhostValid) { spot = p; found = true; }
            }
        if (!found) { build.ScriptCancel(); yield break; }
        // o fantasma desliza até o lugar, gira e é colocado
        float dur = 1.3f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, t / dur);
            build.ScriptGhost(Vector3.Lerp(from, spot, e), e > 0.6f ? 90f : 0f);
            yield return null;
        }
        build.ScriptGhost(spot, 90f);
        yield return Wait(0.35f);
        build.ScriptPlace();
        yield return Wait(0.6f);
    }

    // ------------------------------------------------------------------ câmera
    void SetCam(Vector3 focus, float size, float pitch, float yaw)
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * 30f, rot);
        cam.orthographicSize = size;
    }

    IEnumerator Orbit(Vector3 focus, float size0, float size1, float pitch, float yaw0, float yaw1, float dur)
    {
        follow.enabled = false;
        float t = 0f;
        while (t < dur)
        {
            float u = t / dur;
            float e = Mathf.SmoothStep(0f, 1f, u) * 0.5f + u * 0.5f;
            SetCam(focus, Mathf.Lerp(size0, size1, e), pitch, Mathf.Lerp(yaw0, yaw1, e));
            t += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator FollowShip(ShipArrival ship, float dur)
    {
        follow.enabled = false;
        // câmera do lado de fora, olhando para a ilha: a nave entra no quadro e estaciona
        Vector3 park = ship.ParkPosition;
        Vector3 outward = park; outward.y = 0f;
        float yaw = Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg + 28f;
        Vector3 focus = Vector3.Lerp(park, Vector3.zero, 0.22f) + Vector3.up * 0.6f, vel = Vector3.zero;
        float t = 0f, size = 4.4f, sizeVel = 0f;
        Customer passenger = null;
        while (t < dur)
        {
            if (passenger == null && ship != null && ship.Parked)
                foreach (var c in FindObjectsByType<Customer>(FindObjectsSortMode.None)) if (c.ship == ship) passenger = c;
            Vector3 target = passenger != null ? passenger.transform.position + Vector3.up * 0.7f : focus;
            focus = Vector3.SmoothDamp(focus, target, ref vel, 0.8f);
            size = Mathf.SmoothDamp(size, passenger != null ? 3.4f : 4.4f, ref sizeVel, 1.2f);
            SetCam(focus, size, 22f, yaw + t * 1.5f);
            t += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator Hold(Transform target, float size0, float size1, float pitch, float dur)
    {
        float yaw = cam.transform.eulerAngles.y;
        float t = 0f;
        while (t < dur && target != null)
        {
            SetCam(target.position + Vector3.up * 0.9f, Mathf.Lerp(size0, size1, t / dur), pitch, yaw + t * 3f);
            t += Time.deltaTime;
            yield return null;
        }
    }

    // ------------------------------------------------------------------ letreiros
    void BuildOverlay()
    {
        var go = new GameObject("TrailerOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.5f;
        canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var root = (RectTransform)go.transform;

        // título central
        var title = Group("Titulo", root, out titleGroup);
        titleText = Label(title, "Cosmic Brew", 150, Gold, new Vector2(0f, 60f), new Vector2(1600f, 190f));
        subText = Label(title, "", 44, Cream, new Vector2(0f, -70f), new Vector2(1600f, 70f));
        titleGroup.alpha = 0f;

        // legenda embaixo
        var cap = Group("Legenda", root, out captionGroup);
        captionPanel = new GameObject("Fundo", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        captionPanel.SetParent(cap, false);
        captionPanel.anchorMin = captionPanel.anchorMax = new Vector2(0.5f, 0f);
        captionPanel.anchoredPosition = new Vector2(0f, 150f);
        var img = captionPanel.GetComponent<Image>();
        img.sprite = UISprites.Rounded;
        img.type = Image.Type.Sliced;
        img.color = Night;
        captionText = Label(captionPanel, "", 46, Cream, Vector2.zero, new Vector2(1500f, 90f));
        captionText.rectTransform.anchorMin = captionText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        captionGroup.alpha = 0f;

        // preto para abrir e fechar
        var fade = Group("Fade", root, out fadeGroup);
        var black = fade.gameObject.AddComponent<Image>();
        black.color = new Color(0.06f, 0.04f, 0.1f, 1f);
        fadeGroup.alpha = 0f;
    }

    static RectTransform Group(string name, RectTransform parent, out CanvasGroup group)
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        group = rt.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        return rt;
    }

    static Text Label(RectTransform parent, string text, int size, Color color, Vector2 pos, Vector2 box)
    {
        var t = new GameObject("Texto", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.sizeDelta = box;
        t.rectTransform.anchoredPosition = pos;
        t.font = Loc.Font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.1f, 0.05f, 0.18f, 0.75f);
        shadow.effectDistance = new Vector2(3f, -4f);
        return t;
    }

    IEnumerator TitleCard(string subtitle, float delay, float hold)
    {
        subText.text = subtitle;
        yield return Wait(delay);
        yield return Fade(titleGroup, 0f, 1f, 0.8f);
        yield return Wait(hold - 1.6f);
        yield return Fade(titleGroup, 1f, 0f, 0.8f);
    }

    IEnumerator Caption(string text, float delay, float hold, float y = 150f)
    {
        yield return Wait(delay);
        captionPanel.anchoredPosition = new Vector2(0f, y);
        captionText.text = text;
        captionPanel.sizeDelta = new Vector2(captionText.preferredWidth + 90f, 96f);
        yield return Fade(captionGroup, 0f, 1f, 0.45f);
        yield return Wait(Mathf.Max(0f, hold - 0.9f));
        yield return Fade(captionGroup, 1f, 0f, 0.45f);
    }

    IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            g.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
            t += Time.deltaTime;
            yield return null;
        }
        g.alpha = to;
    }

    IEnumerator FadeLater(CanvasGroup g, float delay, float from, float to, float dur)
    {
        yield return Wait(delay);
        yield return Fade(g, from, to, dur);
    }

    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
    }
}
