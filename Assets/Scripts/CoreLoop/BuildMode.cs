using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Modo Construção (B): catálogo de decorações pagas com Moedas Estelares,
// posicionamento em grid com pré-visualização (verde = pode, vermelho = não pode).
// Tudo por clique: clique num objeto da ilha para selecionar. Decorações podem ser
// movidas, giradas, pintadas e guardadas; deck, borda e quiosque podem ser pintados.
public class BuildMode : MonoBehaviour
{
    public static BuildMode Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.active;
    // quadro em que o Esc fechou algo aqui (a pausa ignora esse Esc)
    public static int LastEscFrame { get; private set; } = -1;

    [Header("Catálogo")]
    [Tooltip("Modelos das decorações (ficam desativados; são clonados ao comprar)")]
    public Decoration[] catalog;
    public Transform decorationsParent;
    public CustomerSpawner spawner;

    [Header("Grid")]
    public float cellSize = 1f;
    public Vector3 islandCenter = Vector3.zero;
    [Tooltip("Raio útil da ilha para colocar coisas")]
    public float islandRadius = 7.1f;
    [Tooltip("Colisores ignorados na checagem de espaço (chão, limites)")]
    public Transform[] ignoreForOverlap;
    public int thumbnailLayer = 30;
    [Tooltip("Preço de pintar uma decoração (usa o da ilha se houver IslandColors)")]
    public int decorationColorCost = 5;

    static readonly Color Night = new Color(0.16f, 0.12f, 0.27f, 0.92f);
    static readonly Color CardColor = new Color(0.22f, 0.17f, 0.36f, 0.97f);
    static readonly Color CardSelected = new Color(0.42f, 0.3f, 0.5f, 1f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.93f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.45f);
    static readonly Color Pink = new Color(1f, 0.55f, 0.65f);
    static readonly Color Lilac = new Color(0.8f, 0.74f, 0.95f);

    private bool active;
    private Decoration placingTemplate;      // item novo sendo comprado
    private Decoration moving;               // item existente sendo movido
    private Vector3 movingOrigPos;
    private Quaternion movingOrigRot;
    private GameObject ghost;
    private float ghostYaw;
    private bool ghostValid;
    private Vector3 ghostPos;
    private Material okMat, badMat;
    private GameObject grid;
    private string messageText;
    private float messageUntil;

    // seleção: uma decoração OU uma parte da ilha (índice em IslandColors.targets)
    private Decoration selectedDeco;
    private int selectedPart = -1;
    private Transform selRing, hoverRing;
    private Material selRingMat, hoverRingMat;

    // área reservada aos clientes: onde esperam + caminho até a borda (faixas rosa)
    struct Zone { public Vector3 a, b; }
    private readonly List<Zone> zones = new List<Zone>();
    private readonly List<GameObject> zoneVisuals = new List<GameObject>();
    private Material zoneMat;
    private readonly List<Customer> hiddenCustomers = new List<Customer>();
    const float ZoneHalfWidth = 0.45f;

    private RectTransform catalogPanel, banner;
    private Text bannerText;
    private readonly List<Card> cards = new List<Card>();

    // painel da seleção (lado direito)
    private RectTransform selPanel, swatchBox, actionRow;
    private Text selTitle, selColorLabel, selCurrent;
    private Text storeLabel;
    private readonly List<SwatchUI> swatchUIs = new List<SwatchUI>();
    private int hoveredSwatch = -1;

    class Card
    {
        public Decoration item;
        public Image bg;
        public Text price;
        public CanvasGroup group;
    }

    class SwatchUI
    {
        public int index;
        public RectTransform ring;
    }

    private RobotMovement roMove;
    private PlayerInteractor roInteract;
    private IsometricCameraFollow camFollow;

    void Awake()
    {
        Instance = this;
        var ro = GameObject.Find("Ro");
        if (ro != null)
        {
            roMove = ro.GetComponent<RobotMovement>();
            roInteract = ro.GetComponent<PlayerInteractor>();
        }
        camFollow = FindAnyObjectByType<IsometricCameraFollow>();
        foreach (var d in catalog) if (d != null) d.gameObject.SetActive(false);
        EnsureEventSystem();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var sprite = Shader.Find("Sprites/Default");
        okMat = new Material(sprite) { color = new Color(0.5f, 1f, 0.72f, 0.5f) };
        badMat = new Material(sprite) { color = new Color(1f, 0.4f, 0.5f, 0.5f) };
        zoneMat = new Material(sprite) { color = new Color(1f, 0.42f, 0.66f, 0.55f), mainTexture = UISprites.Rounded.texture };
        BuildGrid();
        BuildRings(sprite);
        BuildUI();
        SetVisible(false);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    int ColorCost => IslandColors.Instance != null ? IslandColors.Instance.changeCost : decorationColorCost;

    // ------------------------------------------------------------------ loop
    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        if (MenuUI.IsBlocking) return;

        if (kb.bKey.wasPressedThisFrame) { if (active) Exit(); else Enter(); return; }
        if (!active) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            LastEscFrame = Time.frameCount;
            if (ghost != null) CancelPlacing();
            else if (HasSelection) Deselect();
            else Exit();
            return;
        }

        RefreshCards();
        RefreshSelection();
        if (scripted) return; // trailer: quem controla é o roteiro, não o mouse
        bool overUI =EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector3? ground = MouseOnGround(mouse);

        if (ghost != null)
        {
            ShowRing(hoverRing, null);
            if (kb.rKey.wasPressedThisFrame) ghostYaw = (ghostYaw + 90f) % 360f;
            if (ground.HasValue && !overUI)
            {
                ghostPos = Snap(ground.Value);
                ghost.transform.SetPositionAndRotation(ghostPos, Quaternion.Euler(0f, ghostYaw, 0f));
                ghostValid = IsValid(ghostPos);
                SetGhostMaterial(ghostValid ? okMat : badMat);
                ghost.SetActive(true);
                if (mouse.leftButton.wasPressedThisFrame && ghostValid) Place();
            }
            else ghost.SetActive(false);
            if (mouse.rightButton.wasPressedThisFrame) CancelPlacing();
            var item = Current;
            if (item != null)
                SetBanner(moving != null
                    ? Loc.Get("build.moving", item.DisplayName)
                    : Loc.Get("build.placing", item.DisplayName, item.price));
            return;
        }

        // atalhos da decoração selecionada
        if (selectedDeco != null)
        {
            if (kb.rKey.wasPressedThisFrame) RotateSelected();
            else if (kb.xKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) { StoreSelected(); return; }
        }
        if (mouse.rightButton.wasPressedThisFrame && !overUI) Deselect();

        Decoration hoverDeco = null;
        int hoverPart = -1;
        if (!overUI) Pick(mouse, out hoverDeco, out hoverPart);
        ShowRing(hoverRing, hoverDeco != null && hoverDeco != selectedDeco ? hoverDeco.transform : null);

        if (mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            if (hoverDeco != null)
            {
                if (hoverDeco == selectedDeco) StartMoving(hoverDeco);
                else SelectDecoration(hoverDeco);
            }
            else if (hoverPart >= 0) SelectPart(hoverPart);
            else Deselect();
            if (ghost != null) return;
        }

        if (hoverDeco != null && hoverDeco != selectedDeco) SetBanner(Loc.Get("build.hover", hoverDeco.DisplayName));
        else if (hoverPart >= 0 && hoverPart != selectedPart) SetBanner(Loc.Get("build.hover", IslandColors.Instance.targets[hoverPart].DisplayName));
        else if (selectedDeco != null) SetBanner(Loc.Get("build.selected", selectedDeco.DisplayName));
        else if (selectedPart >= 0) SetBanner(Loc.Get("build.selected_part", IslandColors.Instance.targets[selectedPart].DisplayName));
        else SetBanner(Loc.Get("build.default"));
    }

    // ------------------------------------------------------------------ roteiro do trailer
    [System.NonSerialized] public bool scripted;
    public bool GhostValid => ghost != null && ghostValid;
    public void ScriptBuy(Decoration item) => Select(item);
    public void ScriptSelect(Decoration d) => SelectDecoration(d);
    public void ScriptSelectPart(int part) => SelectPart(part);
    public void ScriptColor(int swatch) => PickColor(swatch);
    public void ScriptCancel() => CancelPlacing();
    public void ScriptGhost(Vector3 worldPos, float yaw)
    {
        if (ghost == null) return;
        ghostYaw = yaw;
        ghostPos = Snap(worldPos);
        ghost.transform.SetPositionAndRotation(ghostPos, Quaternion.Euler(0f, ghostYaw, 0f));
        ghostValid = IsValid(ghostPos);
        SetGhostMaterial(ghostValid ? okMat : badMat);
        ghost.SetActive(true);
        var item = Current;
        if (item != null) SetBanner(Loc.Get("build.placing", item.DisplayName, item.price));
    }
    public bool ScriptPlace()
    {
        if (ghost == null || !ghostValid) return false;
        Place();
        return true;
    }

    Decoration Current => moving != null ? moving : placingTemplate;

    public bool IsMovingSomething => moving != null;

    bool HasSelection => selectedDeco != null || selectedPart >= 0;

    public Decoration FindTemplate(string id)
    {
        foreach (var d in catalog) if (d != null && d.displayName == id) return d;
        return null;
    }

    // ------------------------------------------------------------------ entrar / sair
    public void Enter()
    {
        active = true;
        if (AudioManager.Instance != null) AudioManager.Instance.Play("build_open");
        if (roMove != null) roMove.movementLocked = true;
        if (roInteract != null) roInteract.enabled = false;
        if (GameUI.Instance != null) GameUI.Instance.SetBuildHud(true);
        if (camFollow != null) camFollow.buildView = true;
        // clientes somem enquanto constrói (e não chega ninguém novo)
        if (spawner != null) spawner.paused = true;
        hiddenCustomers.Clear();
        foreach (var c in FindObjectsByType<Customer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            hiddenCustomers.Add(c);
            c.gameObject.SetActive(false);
        }
        SetVisible(true);
        RebuildZones();
    }

    public void Exit()
    {
        CancelPlacing();
        Deselect();
        if (active) if (AudioManager.Instance != null) AudioManager.Instance.Play("build_close");
        active = false;
        if (roMove != null) roMove.movementLocked = false;
        if (roInteract != null) roInteract.enabled = true;
        if (GameUI.Instance != null) GameUI.Instance.SetBuildHud(false);
        if (camFollow != null) camFollow.buildView = false;
        foreach (var c in hiddenCustomers) if (c != null) c.gameObject.SetActive(true);
        hiddenCustomers.Clear();
        if (spawner != null) spawner.paused = false;
        SetVisible(false);
        ClearZoneVisuals();
    }

    void SetVisible(bool v)
    {
        if (grid != null) grid.SetActive(v);
        if (banner != null) banner.gameObject.SetActive(v);
        if (catalogPanel != null) catalogPanel.gameObject.SetActive(v);
        if (selPanel != null) selPanel.gameObject.SetActive(v && HasSelection && ghost == null);
        if (!v) { ShowRing(selRing, null); ShowRing(hoverRing, null); }
    }

    // ------------------------------------------------------------------ seleção
    void Pick(Mouse mouse, out Decoration deco, out int part)
    {
        deco = null;
        part = -1;
        var cam = Camera.main;
        if (cam == null) return;
        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        var ic = IslandColors.Instance;
        foreach (var h in hits)
        {
            var d = h.collider.GetComponentInParent<Decoration>();
            if (d != null && d.enabled) { deco = d; return; }
            if (h.collider.GetComponentInParent<Customer>() != null || h.transform.root.name == "Ro") continue;
            int p = ic != null ? ic.FindTarget(h.collider.transform) : -1;
            if (p >= 0) { part = p; return; }
        }
    }

    void SelectDecoration(Decoration d)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play("ui_click");
        selectedDeco = d;
        selectedPart = -1;
        OpenSelectionPanel();
    }

    void SelectPart(int part)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play("ui_click");
        selectedDeco = null;
        selectedPart = part;
        OpenSelectionPanel();
    }

    void Deselect()
    {
        selectedDeco = null;
        selectedPart = -1;
        if (selPanel != null) selPanel.gameObject.SetActive(false);
        ShowRing(selRing, null);
    }

    void RotateSelected()
    {
        if (selectedDeco == null) return;
        if (selectedDeco.isSeat && spawner != null && spawner.IsSeatOccupied(selectedDeco.transform)) { Message(Loc.Get("build.occupied")); return; }
        // o espaço ocupado é um círculo: girar no lugar sempre cabe
        selectedDeco.transform.rotation = Quaternion.Euler(0f, (selectedDeco.transform.eulerAngles.y + 90f) % 360f, 0f);
        if (AudioManager.Instance != null) AudioManager.Instance.Play("place", selectedDeco.transform.position);
        Physics.SyncTransforms();
        RebuildZones();
        if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
    }

    void StoreSelected()
    {
        var d = selectedDeco;
        if (d == null) return;
        if (Store(d)) Deselect();
    }

    void PickColor(int swatch)
    {
        int cost = ColorCost;
        if (selectedDeco != null)
        {
            if (selectedDeco.ColorIndex == swatch) return;
            if (CoinWallet.Coins < cost) { Message(Loc.Get("build.color_cost", cost)); return; }
            CoinWallet.Add(-cost);
            selectedDeco.ApplyColor(swatch);
            ColorDone(selectedDeco.DisplayName, Decoration.Palette[swatch].DisplayName, cost, selectedDeco.transform.position);
        }
        else if (selectedPart >= 0)
        {
            var ic = IslandColors.Instance;
            var t = ic.targets[selectedPart];
            if (t.current == swatch) return;
            if (CoinWallet.Coins < cost) { Message(Loc.Get("build.color_cost", cost)); return; }
            CoinWallet.Add(-cost);
            ic.Apply(selectedPart, swatch);
            Vector3 pos = t.renderers.Length > 0 && t.renderers[0] != null ? t.renderers[0].bounds.center : islandCenter;
            ColorDone(t.DisplayName, t.swatches[swatch].DisplayName, cost, pos);
        }
    }

    void ColorDone(string what, string color, int cost, Vector3 pos)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play("place");
        if (SimpleParticles.Instance != null)
            SimpleParticles.Instance.Emit(pos + Vector3.up * 0.8f, 10, SimpleParticles.Shape.Star, Gold, 0.1f, 0.9f, Vector3.up * 0.8f, 0.9f, 0.6f);
        Message(Loc.Get("build.color_done", what, color, cost));
        if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
    }

    IslandColors.Swatch[] SelectedSwatches =>
        selectedDeco != null ? Decoration.Palette
        : selectedPart >= 0 ? IslandColors.Instance.targets[selectedPart].swatches : null;

    int SelectedColor =>
        selectedDeco != null ? selectedDeco.ColorIndex
        : selectedPart >= 0 ? IslandColors.Instance.targets[selectedPart].current : -1;

    // ------------------------------------------------------------------ colocar / mover / guardar
    void Select(Decoration item)
    {
        if (item == null) return;
        if (CoinWallet.Coins < item.price) { Message(Loc.Get("build.need", item.price - CoinWallet.Coins, item.DisplayName)); return; }
        CancelPlacing();
        Deselect();
        if (AudioManager.Instance != null) AudioManager.Instance.Play("ui_click");
        placingTemplate = item;
        ghostYaw = 0f;
        CreateGhost(item.gameObject);
    }

    void StartMoving(Decoration d)
    {
        if (d.isSeat && spawner != null && spawner.IsSeatOccupied(d.transform)) { Message(Loc.Get("build.occupied")); return; }
        CancelPlacing();
        if (AudioManager.Instance != null) AudioManager.Instance.Play("ui_click");
        moving = d;
        movingOrigPos = d.transform.position;
        movingOrigRot = d.transform.rotation;
        ghostYaw = d.transform.eulerAngles.y;
        CreateGhost(d.gameObject);
        d.gameObject.SetActive(false);
        if (selPanel != null) selPanel.gameObject.SetActive(false);
        RebuildZones();
    }

    void Place()
    {
        var rot = Quaternion.Euler(0f, ghostYaw, 0f);
        if (AudioManager.Instance != null) AudioManager.Instance.Play("place", ghostPos);
        if (moving != null)
        {
            var moved = moving;
            moving.transform.SetPositionAndRotation(ghostPos, rot);
            moving.gameObject.SetActive(true);
            moving = null;
            DestroyGhost();
            Physics.SyncTransforms();
            RebuildZones();
            if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
            SelectDecoration(moved); // continua selecionado no lugar novo
            return;
        }
        var item = placingTemplate;
        if (item == null || CoinWallet.Coins < item.price) { CancelPlacing(); return; }
        CoinWallet.Add(-item.price);
        var station = item.GetComponent<Station>();
        bool newRecipes = station != null && !IngredientInfo.Available(station.ingredient);
        if (newRecipes && GameUI.Instance != null) GameUI.Instance.ShowToast(Loc.Get("toast.new_recipes"));
        var go = Instantiate(item.gameObject, ghostPos, rot, decorationsParent);
        go.name = item.displayName;
        go.SetActive(true);
        var d = go.GetComponent<Decoration>();
        if (d != null && d.isSeat && spawner != null) spawner.AddSeat(go.transform);
        if (GameUI.Instance != null) GameUI.Instance.ShowPopup(ghostPos + Vector3.up * 1.6f, $"-{item.price}", Gold);
        Physics.SyncTransforms();
        RebuildZones();
        if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
        if (CoinWallet.Coins < item.price) CancelPlacing(); // acabou o dinheiro: para de colocar
    }

    bool Store(Decoration d)
    {
        if (d.isSeat && spawner != null && !spawner.RemoveSeat(d.transform)) { Message(Loc.Get("build.occupied")); return false; }
        CoinWallet.Add(d.price);
        if (AudioManager.Instance != null) AudioManager.Instance.Play("store", d.transform.position);
        if (GameUI.Instance != null) GameUI.Instance.ShowPopup(d.transform.position + Vector3.up * 1.6f, Loc.Get("build.stored", d.price), Gold);
        d.gameObject.SetActive(false); // some já (Destroy só acontece no fim do frame)
        Destroy(d.gameObject);
        RebuildZones();
        if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
        return true;
    }

    void CancelPlacing()
    {
        if (moving != null)
        {
            var moved = moving;
            moving.transform.SetPositionAndRotation(movingOrigPos, movingOrigRot);
            moving.gameObject.SetActive(true);
            moving = null;
            if (active) { RebuildZones(); SelectDecoration(moved); }
        }
        placingTemplate = null;
        DestroyGhost();
    }

    void CreateGhost(GameObject source)
    {
        DestroyGhost();
        ghost = Instantiate(source);
        ghost.name = "Fantasma";
        ghost.SetActive(true);
        foreach (var c in ghost.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Destroy(c); }
        foreach (var b in ghost.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        foreach (var l in ghost.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (var r in ghost.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SetGhostMaterial(okMat);
        ghost.SetActive(false);
    }

    void SetGhostMaterial(Material m)
    {
        foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }

    // ------------------------------------------------------------------ anéis de destaque
    void BuildRings(Shader sprite)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);
                a = Mathf.Max(a, Mathf.Clamp01(1f - d) * 0.35f * (d < 0.86f ? 1f : 0f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        selRingMat = new Material(sprite) { mainTexture = tex, color = Gold };
        hoverRingMat = new Material(sprite) { mainTexture = tex, color = new Color(1f, 1f, 1f, 0.55f) };
        selRing = MakeRing("AnelSelecao", selRingMat);
        hoverRing = MakeRing("AnelHover", hoverRingMat);
    }

    Transform MakeRing(string name, Material m)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var mr = q.GetComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        q.SetActive(false);
        return q.transform;
    }

    void ShowRing(Transform ring, Transform target)
    {
        if (ring == null) return;
        if (target == null) { ring.gameObject.SetActive(false); return; }
        var bounds = new Bounds(target.position, Vector3.zero);
        foreach (var r in target.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        float radius = Mathf.Max(0.45f, Mathf.Max(bounds.extents.x, bounds.extents.z)) * 1.35f;
        if (ring == selRing) radius *= 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
        ring.position = new Vector3(bounds.center.x, 0.08f, bounds.center.z);
        ring.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        ring.gameObject.SetActive(true);
    }

    // ------------------------------------------------------------------ regras do grid
    Vector3 Snap(Vector3 p) =>
        new Vector3((Mathf.Floor(p.x / cellSize) + 0.5f) * cellSize, 0f, (Mathf.Floor(p.z / cellSize) + 0.5f) * cellSize);

    bool IsValid(Vector3 pos)
    {
        var item = Current;
        if (item == null) return false;
        Vector3 flat = pos - islandCenter; flat.y = 0f;
        if (flat.magnitude + item.footprint > islandRadius) return false;
        if (item.isFloor) return true;
        float r = item.footprint * 0.95f;
        var hits = Physics.OverlapCapsule(pos + Vector3.up * (r + 0.05f), pos + Vector3.up * 1.6f, r, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (IsIgnored(h.transform)) continue;
            return false;
        }
        // não pode ficar onde os clientes esperam nem no caminho deles
        foreach (var z in zones)
            if (DistanceToSegment(pos, z.a, z.b) < item.footprint + ZoneHalfWidth) return false;
        // banquinho novo: o caminho dos clientes até ele precisa estar livre
        if (item.isSeat && spawner != null)
        {
            spawner.GetCustomerPath(pos, out Vector3 stand, out Vector3 entry);
            Vector3 sf = stand - islandCenter; sf.y = 0f;
            if (sf.magnitude > islandRadius - 0.2f) return false;
            var pathHits = Physics.OverlapCapsule(stand + Vector3.up * 0.6f, entry + Vector3.up * 0.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in pathHits)
            {
                if (IsIgnored(h.transform) || h.transform.root.name == "Ro") continue;
                return false;
            }
        }
        return true;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    // ------------------------------------------------------------------ zonas dos clientes
    void RebuildZones()
    {
        ClearZoneVisuals();
        zones.Clear();
        if (spawner == null) return;
        foreach (var seat in spawner.seats)
        {
            if (seat == null || !seat.gameObject.activeInHierarchy) continue;
            spawner.GetCustomerPath(seat.position, out Vector3 stand, out Vector3 entry);
            zones.Add(new Zone { a = stand, b = entry });
            if (!active) continue;
            // faixa rosa do lugar de espera até a borda
            Vector3 dir = entry - stand; dir.y = 0f;
            float len = dir.magnitude;
            var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
            strip.name = "ZonaClientes";
            Destroy(strip.GetComponent<Collider>());
            strip.transform.SetParent(transform, false);
            strip.transform.position = (stand + entry) * 0.5f + Vector3.up * 0.07f;
            strip.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward) * Quaternion.Euler(90f, 0f, 0f);
            strip.transform.localScale = new Vector3(ZoneHalfWidth * 2f, len + ZoneHalfWidth * 2f, 1f);
            var mr = strip.GetComponent<MeshRenderer>();
            mr.sharedMaterial = zoneMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zoneVisuals.Add(strip);
        }
    }

    void ClearZoneVisuals()
    {
        foreach (var z in zoneVisuals) if (z != null) Destroy(z);
        zoneVisuals.Clear();
    }

    bool IsIgnored(Transform t)
    {
        if (ignoreForOverlap == null) return false;
        foreach (var ig in ignoreForOverlap) if (ig != null && t.IsChildOf(ig)) return true;
        return false;
    }

    static Vector3? MouseOnGround(Mouse mouse)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : (Vector3?)null;
    }

    // ------------------------------------------------------------------ grid visual
    void BuildGrid()
    {
        const int size = 1024;
        float world = (islandRadius + 0.4f) * 2f;
        float pxPerM = size / world;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float wx = (x + 0.5f) / pxPerM - world / 2f + islandCenter.x;
                float wz = (y + 0.5f) / pxPerM - world / 2f + islandCenter.z;
                float dist = Mathf.Sqrt((wx - islandCenter.x) * (wx - islandCenter.x) + (wz - islandCenter.z) * (wz - islandCenter.z));
                float fx = Mathf.Abs(wx / cellSize - Mathf.Round(wx / cellSize)) * cellSize * pxPerM;
                float fz = Mathf.Abs(wz / cellSize - Mathf.Round(wz / cellSize)) * cellSize * pxPerM;
                float line = Mathf.Clamp01(1.6f - Mathf.Min(fx, fz));
                float inside = Mathf.Clamp01((islandRadius - dist) * 3f);
                float border = Mathf.Clamp01(1.5f - Mathf.Abs(dist - islandRadius) * pxPerM * 0.5f);
                float a = Mathf.Max(line * 0.45f * inside, border * 0.8f);
                px[y * size + x] = new Color32(255, 246, 236, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(true);

        grid = GameObject.CreatePrimitive(PrimitiveType.Quad);
        grid.name = "GridConstrucao";
        Destroy(grid.GetComponent<Collider>());
        grid.transform.SetParent(transform, false);
        grid.transform.position = islandCenter + Vector3.up * 0.06f;
        grid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        grid.transform.localScale = new Vector3(world, world, 1f);
        var mr = grid.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ UI
    void BuildUI()
    {
        var ui = GameUI.Instance;
        if (ui == null) return;

        banner = ui.Panel("Construcao_Faixa", ui.Root, UISprites.Rounded, Night, new Vector2(1240, 52));
        GameUI.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f));
        bannerText = ui.Label("Texto", banner, "", 21, Cream, TextAnchor.MiddleCenter);
        bannerText.resizeTextForBestFit = true;
        bannerText.resizeTextMinSize = 14;
        bannerText.resizeTextMaxSize = 21;
        bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        GameUI.Stretch(bannerText.rectTransform, 18, 18);

        const float cardW = 170f, cardH = 212f, gap = 14f;
        float width = catalog.Length * (cardW + gap) + gap + 8f;
        catalogPanel = ui.Panel("Catalogo", ui.Root, UISprites.Rounded, Night, new Vector2(width, cardH + 56f));
        GameUI.Anchor(catalogPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f));
        catalogPanel.GetComponent<Image>().raycastTarget = true; // clique no painel não passa para a ilha
        var title = ui.Label("Titulo", catalogPanel, "", 20, Gold, TextAnchor.UpperLeft);
        ui.Bind(title, () => Loc.Get("build.catalog"));
        title.rectTransform.sizeDelta = new Vector2(500, 30);
        GameUI.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f));

        for (int i = 0; i < catalog.Length; i++)
        {
            var item = catalog[i];
            if (item == null) continue;
            var card = ui.Panel("Item_" + item.displayName, catalogPanel, UISprites.Rounded, CardColor, new Vector2(cardW, cardH));
            GameUI.Anchor(card, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(gap + 4f + i * (cardW + gap), 14f));
            var bg = card.GetComponent<Image>();
            bg.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.pressedColor = new Color(0.9f, 0.9f, 0.9f); button.colors = colors;
            var captured = item;
            button.onClick.AddListener(() => Select(captured));
            var group = card.gameObject.AddComponent<CanvasGroup>();

            var thumbGo = new GameObject("Miniatura", typeof(RectTransform), typeof(RawImage));
            var thumb = (RectTransform)thumbGo.transform;
            thumb.SetParent(card, false);
            thumb.sizeDelta = new Vector2(124, 124);
            GameUI.Anchor(thumb, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f));
            var raw = thumbGo.GetComponent<RawImage>();
            raw.texture = RenderThumbnail(item);
            raw.raycastTarget = false;

            var name = ui.Label("Nome", card, "", 17, Cream, TextAnchor.MiddleCenter);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 11;
            name.resizeTextMaxSize = 17;
            ui.Bind(name, () => captured.DisplayName);
            name.rectTransform.sizeDelta = new Vector2(cardW - 10f, 26f);
            GameUI.Anchor(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f));

            var star = ui.Panel("Estrela", card, UISprites.Star, Gold, new Vector2(20, 20));
            GameUI.Anchor(star, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-22f, 22f));
            var price = ui.Label("Preco", card, item.price.ToString(), 20, Gold, TextAnchor.MiddleLeft);
            price.rectTransform.sizeDelta = new Vector2(80f, 26f);
            GameUI.Anchor(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-8f, 22f));

            cards.Add(new Card { item = item, bg = bg, price = price, group = group });
        }

        BuildSelectionPanel(ui);
    }

    // Painel à direita: nome do objeto clicado, cores e ações
    void BuildSelectionPanel(GameUI ui)
    {
        selPanel = ui.Panel("Selecao", ui.Root, UISprites.Rounded, Night, new Vector2(372, 320));
        GameUI.Anchor(selPanel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 120f));
        selPanel.GetComponent<Image>().raycastTarget = true;

        selTitle = ui.Label("Nome", selPanel, "", 24, Gold, TextAnchor.MiddleLeft);
        selTitle.rectTransform.sizeDelta = new Vector2(290f, 34f);
        GameUI.Anchor(selTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f));

        var close = MakeButton(ui, "Fechar", selPanel, new Vector2(38f, 38f), CardColor, () => "x", Deselect, 24);
        GameUI.Anchor(close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f));

        selColorLabel = ui.Label("Cor", selPanel, "", 17, Lilac, TextAnchor.MiddleLeft, FontStyle.Normal);
        selColorLabel.rectTransform.sizeDelta = new Vector2(330f, 24f);
        GameUI.Anchor(selColorLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -54f));

        var boxGo = new GameObject("Cores", typeof(RectTransform));
        swatchBox = (RectTransform)boxGo.transform;
        swatchBox.SetParent(selPanel, false);
        swatchBox.sizeDelta = new Vector2(330f, 130f);
        GameUI.Anchor(swatchBox, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -84f));

        selCurrent = ui.Label("Atual", selPanel, "", 17, Cream, TextAnchor.MiddleLeft, FontStyle.Normal);
        selCurrent.rectTransform.sizeDelta = new Vector2(330f, 24f);

        var rowGo = new GameObject("Acoes", typeof(RectTransform));
        actionRow = (RectTransform)rowGo.transform;
        actionRow.SetParent(selPanel, false);
        actionRow.sizeDelta = new Vector2(332f, 46f);
        GameUI.Anchor(actionRow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 18f));
        var move = MakeButton(ui, "Mover", actionRow, new Vector2(94f, 46f), CardSelected, () => Loc.Get("build.btn_move"), () => { if (selectedDeco != null) StartMoving(selectedDeco); }, 18);
        GameUI.Anchor(move, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero);
        var rotate = MakeButton(ui, "Girar", actionRow, new Vector2(94f, 46f), CardSelected, () => Loc.Get("build.btn_rotate"), RotateSelected, 18);
        GameUI.Anchor(rotate, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(102f, 0f));
        var store = MakeButton(ui, "Guardar", actionRow, new Vector2(128f, 46f), new Color(0.55f, 0.3f, 0.45f, 1f),
                               () => Loc.Get("build.btn_store", selectedDeco != null ? selectedDeco.price : 0), StoreSelected, 18);
        GameUI.Anchor(store, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(204f, 0f));
        storeLabel = store.GetComponentInChildren<Text>();

        selPanel.gameObject.SetActive(false);
    }

    RectTransform MakeButton(GameUI ui, string name, RectTransform parent, Vector2 size, Color color, System.Func<string> text, System.Action onClick, int fontSize)
    {
        var b = ui.Panel("Botao_" + name, parent, UISprites.Rounded, color, size);
        b.GetComponent<Image>().raycastTarget = true;
        var button = b.gameObject.AddComponent<Button>();
        var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(0.85f, 0.85f, 0.85f); button.colors = colors;
        button.onClick.AddListener(() => onClick());
        var t = ui.Label("Texto", b, "", fontSize, Cream, TextAnchor.MiddleCenter);
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 11;
        t.resizeTextMaxSize = fontSize;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        ui.Bind(t, text);
        GameUI.Stretch(t.rectTransform, 4, 4);
        return b;
    }

    // Recria as bolinhas de cor para o objeto selecionado
    void OpenSelectionPanel()
    {
        var ui = GameUI.Instance;
        if (ui == null || selPanel == null) return;
        foreach (Transform c in swatchBox) Destroy(c.gameObject);
        swatchUIs.Clear();
        hoveredSwatch = -1;

        var swatches = SelectedSwatches;
        const int perRow = 5;
        const float step = 64f;
        for (int s = 0; s < swatches.Length; s++)
        {
            var sw = swatches[s];
            float x = 30f + (s % perRow) * step;
            float y = -30f - (s / perRow) * step;
            var ring = ui.Panel("Selecionada", swatchBox, UISprites.Circle, Gold, new Vector2(56, 56));
            GameUI.Anchor(ring, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y));
            Color c = s == 0 && selectedDeco != null ? new Color(0.93f, 0.9f, 0.96f) : sw.color;
            var dot = ui.Panel("Cor_" + sw.name, swatchBox, UISprites.Circle, new Color(c.r, c.g, c.b, 1f), new Vector2(44, 44));
            GameUI.Anchor(dot, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y));
            dot.GetComponent<Image>().raycastTarget = true;
            if (s == 0)
            {
                // a cor original leva uma estrelinha
                var mark = ui.Panel("Original", dot, UISprites.Star, new Color(0.5f, 0.4f, 0.65f, 0.8f), new Vector2(20, 20));
                GameUI.Anchor(mark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            int cs = s;
            dot.gameObject.AddComponent<Button>().onClick.AddListener(() => PickColor(cs));
            var trigger = dot.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => hoveredSwatch = cs);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (hoveredSwatch == cs) hoveredSwatch = -1; });
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
            swatchUIs.Add(new SwatchUI { index = s, ring = ring });
        }

        int rows = Mathf.CeilToInt(swatches.Length / (float)perRow);
        float swatchesBottom = 84f + rows * step;
        GameUI.Anchor(selCurrent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -swatchesBottom - 4f));
        bool deco = selectedDeco != null;
        actionRow.gameObject.SetActive(deco);
        selPanel.sizeDelta = new Vector2(372f, swatchesBottom + 38f + (deco ? 72f : 12f));
        selPanel.gameObject.SetActive(true);
        RefreshSelection();
    }

    void RefreshSelection()
    {
        if (selPanel == null) return;
        if (selectedDeco == null && selectedPart < 0) { ShowRing(selRing, null); return; }
        if ((object)selectedDeco != null && !selectedDeco) { Deselect(); return; } // destruído
        bool panelVisible = ghost == null;
        if (selPanel.gameObject.activeSelf != panelVisible) selPanel.gameObject.SetActive(panelVisible);

        var swatches = SelectedSwatches;
        int current = SelectedColor;
        selTitle.text = selectedDeco != null ? selectedDeco.DisplayName : IslandColors.Instance.targets[selectedPart].DisplayName;
        selColorLabel.text = Loc.Get("build.sel_color", ColorCost);
        int shown = hoveredSwatch >= 0 && hoveredSwatch < swatches.Length ? hoveredSwatch : current;
        selCurrent.text = shown == current
            ? Loc.Get("build.sel_current", swatches[current].DisplayName)
            : swatches[shown].DisplayName;
        foreach (var s in swatchUIs) s.ring.gameObject.SetActive(s.index == current);
        if (storeLabel != null && selectedDeco != null) storeLabel.text = Loc.Get("build.btn_store", selectedDeco.price);

        ShowRing(selRing, selectedDeco != null && ghost == null ? selectedDeco.transform : null);
    }

    void RefreshCards()
    {
        foreach (var c in cards)
        {
            bool affordable = CoinWallet.Coins >= c.item.price;
            c.group.alpha = affordable ? 1f : 0.5f;
            c.price.color = affordable ? Gold : Pink;
            c.bg.color = placingTemplate == c.item ? CardSelected : CardColor;
        }
    }

    void SetBanner(string text)
    {
        if (bannerText == null) return;
        bool showMessage = Time.time < messageUntil;
        bannerText.text = showMessage ? messageText : text;
        bannerText.color = showMessage ? Pink : Cream;
    }

    void Message(string text)
    {
        messageText = text;
        messageUntil = Time.time + 2.2f;
    }

    // Renderiza uma miniatura 3D do item (uma vez, ao montar o catálogo)
    Texture RenderThumbnail(Decoration item)
    {
        const int size = 192;
        int mask = 1 << thumbnailLayer;
        var studio = new GameObject("EstudioMiniatura");
        studio.transform.position = new Vector3(0f, -800f, 0f);
        var clone = Instantiate(item.gameObject, studio.transform);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
        clone.SetActive(true);
        foreach (var b in clone.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = thumbnailLayer;

        var bounds = new Bounds(studio.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in clone.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
        }

        var cam = new GameObject("CamMiniatura").AddComponent<Camera>();
        cam.transform.SetParent(studio.transform, false);
        cam.enabled = false;
        cam.fieldOfView = 26f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = mask;
        cam.allowHDR = false;
        float dist = bounds.extents.magnitude / Mathf.Tan(13f * Mathf.Deg2Rad) * 1.02f;
        Vector3 dir = Quaternion.Euler(24f, 0f, 0f) * Vector3.back;
        cam.transform.position = bounds.center + dir * dist;
        cam.transform.LookAt(bounds.center);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = dist * 3f;

        var key = new GameObject("LuzMiniatura").AddComponent<Light>();
        key.transform.SetParent(studio.transform, false);
        key.type = LightType.Directional;
        key.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        key.intensity = 1.3f;
        key.cullingMask = mask;

        var rt = RenderTexture.GetTemporary(size, size, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(studio);
        return tex;
    }
}
