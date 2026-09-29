using UnityEngine;

// Customer: arrives, waits at a stool with the order in a bubble, drinks ("Ahhh…"), pays and leaves.
// Never leaves upset: a wrong order just gets a head shake and they keep waiting.
public class Customer : Interactable
{
    public enum State { Arriving, Waiting, Drinking, Leaving }

    public float walkSpeed = 1.4f;
    public float drinkTime = 3.4f;
    public float bubbleHeight = 1.55f;

    [System.NonSerialized] public ShipArrival ship; // ship that brought the customer (leaves with them)

    public State CurrentState { get; private set; }
    public Recipe Order { get; private set; }

    private CustomerSpawner spawner;
    private Transform seat;
    private Vector3 seatPos, exitPos, lookTarget;
    private float stateTime, refuseTime = -10f, scale;
    private GameUI.Bubble bubble;

    // drinking: cup in hand, a sip, "Ahhh…" and only then the tip
    private Transform cup;
    private Vector3 cupStartLocal;
    private bool ahhDone;
    private int pendingPay, pendingTip;
    private float steamAccum;
    static readonly Vector3 HandLocal = new Vector3(0.3f, 0.7f, 0.56f);
    static readonly Vector3 MouthLocal = new Vector3(0.08f, 0.96f, 0.58f);

    public void Init(CustomerSpawner owner, Transform seatTransform, Vector3 seatPosition, Vector3 exit, Vector3 look)
    {
        spawner = owner;
        seat = seatTransform;
        seatPos = seatPosition;
        exitPos = exit;
        lookTarget = look;
        Order = RecipeBook.Random();
        CurrentState = State.Arriving;
        scale = 0f;
        transform.localScale = Vector3.one * 0.01f;
        interactRadius = 2.1f;
        promptHeight = 1.95f;
        duration = 0.75f;
        roAction = RoAction.Serve;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        stateTime += dt;

        // appears/disappears with a soft "pop"
        float targetScale = CurrentState == State.Leaving && Arrived(exitPos) ? 0f : 1f;
        scale = Mathf.MoveTowards(scale, targetScale, dt * 2.5f);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, Mathf.SmoothStep(0f, 1f, scale));

        switch (CurrentState)
        {
            case State.Arriving:
                if (WalkTo(seatPos, dt)) SetState(State.Waiting);
                break;

            case State.Waiting:
                // after refusing a drink, shakes the head ("not this one~") without moving
                float shake = Time.time - refuseTime < 0.9f ? Mathf.Sin((Time.time - refuseTime) * 22f) * 18f : 0f;
                Face(lookTarget, dt, shake);
                break;

            case State.Drinking:
                Face(lookTarget, dt, 0f);
                AnimateDrink(stateTime / drinkTime, dt);
                if (stateTime > drinkTime) SetState(State.Leaving);
                break;

            case State.Leaving:
                WalkTo(exitPos, dt);
                if (Arrived(exitPos) && scale <= 0f)
                {
                    if (ship != null) ship.Depart();
                    Destroy(gameObject);
                }
                break;
        }
    }

    void SetState(State s)
    {
        CurrentState = s;
        stateTime = 0f;
        var ui = GameUI.Instance;
        switch (s)
        {
            case State.Waiting:
                if (AudioManager.Instance != null) AudioManager.Instance.Play("customer_arrive", transform.position, 0.8f);
                if (ui != null) bubble = ui.CreateBubble(transform, bubbleHeight);
                bubble?.Set(Order.Name, IngredientsText(Order));
                break;
            case State.Drinking:
                ahhDone = false;
                bubble?.Set("…", null);
                break;
            case State.Leaving:
                if (!ahhDone) Ahhh(); // safety net: always pays
                if (cup != null) Destroy(cup.gameObject);
                cup = null;
                bubble?.Close();
                bubble = null;
                if (spawner != null) spawner.FreeSeat(seat);
                break;
        }
    }

    static string IngredientsText(Recipe r)
    {
        var parts = new string[r.ingredients.Length];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = $"<color=#{ColorUtility.ToHtmlStringRGB(IngredientInfo.UiOnLight(r.ingredients[i]))}>{IngredientInfo.Name(r.ingredients[i])}</color>";
        return string.Join(" + ", parts);
    }

    bool Arrived(Vector3 target)
    {
        Vector3 d = target - transform.position; d.y = 0f;
        return d.magnitude < 0.05f;
    }

    bool WalkTo(Vector3 target, float dt)
    {
        Vector3 p = transform.position;
        Vector3 d = target - p; d.y = 0f;
        if (d.magnitude < 0.05f) return true;
        transform.position = Vector3.MoveTowards(p, new Vector3(target.x, p.y, target.z), walkSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 1f - Mathf.Exp(-8f * dt));
        return false;
    }

    void Face(Vector3 target, float dt, float yawOffset)
    {
        Vector3 d = target - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(d.normalized) * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-10f * dt));
    }

    // ---------- Interactable: serving ----------
    public override Vector3 PromptPosition => transform.position + Vector3.up * promptHeight;

    public override string GetPrompt(PlayerCarry player, out bool enabled)
    {
        enabled = CurrentState == State.Waiting && player.HasCup;
        return enabled ? Loc.Get("customer.serve", Order.Name) : null;
    }

    public override void Interact(PlayerCarry player)
    {
        if (CurrentState != State.Waiting) return;
        var ui = GameUI.Instance;
        if (Order.Matches(player.Contents))
        {
            // the cup moves from Ro's hands to the customer's
            BuildCup(player.MugWorldPosition, player.LiquidColor, player.LiquidGlows);
            player.Clear();
            pendingTip = Random.Range(1, 5);
            pendingPay = Order.price + pendingTip;
            SetState(State.Drinking);
        }
        else
        {
            refuseTime = Time.time;
            if (AudioManager.Instance != null) AudioManager.Instance.Play("wrong", transform.position);
            bubble?.Say(Loc.Get("customer.wrong"), 1.8f);
        }
    }

    // ---------- drinking ----------
    public override Vector3 HandoffPoint => transform.TransformPoint(HandLocal);

    static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    void BuildCup(Vector3 worldPos, Color liquid, bool glow)
    {
        if (cup != null) Destroy(cup.gameObject);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var ceramic = new Material(lit) { color = new Color(1f, 0.95f, 0.88f) };
        var drink = new Material(lit) { color = liquid };
        if (glow) { drink.EnableKeyword("_EMISSION"); drink.SetColor("_EmissionColor", new Color(0.3f, 0.7f, 1.2f)); }
        cup = new GameObject("XicaraDoCliente").transform;
        cup.SetParent(transform, false);
        void Part(PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(cup, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }
        Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.24f, 0.12f, 0.24f), ceramic);
        Part(PrimitiveType.Cylinder, new Vector3(0f, 0.115f, 0f), new Vector3(0.2f, 0.01f, 0.2f), drink);
        Part(PrimitiveType.Cube, new Vector3(0.15f, 0.01f, 0f), new Vector3(0.05f, 0.13f, 0.04f), ceramic);
        cup.position = worldPos;
        cupStartLocal = cup.localPosition;
        // the customer scales in on arrival; the cup keeps its world size
        cup.localScale = Vector3.one / Mathf.Max(0.01f, transform.localScale.x);
    }

    void AnimateDrink(float u, float dt)
    {
        if (cup == null) return;
        // 0–.12 grab · .12–.25 raise · .25–.5 sip · .5 "Ahhh" · .55–.75 lower · .8–1 vanish
        Vector3 pos;
        Quaternion rot = Quaternion.identity;
        if (u < 0.12f) pos = Vector3.Lerp(cupStartLocal, HandLocal, Ease(u / 0.12f));
        else if (u < 0.25f) pos = Vector3.Lerp(HandLocal, MouthLocal, Ease((u - 0.12f) / 0.13f));
        else if (u < 0.55f)
        {
            float sip = Ease((u - 0.25f) / 0.08f) * (1f - Ease((u - 0.47f) / 0.08f));
            pos = MouthLocal + Vector3.up * Mathf.Sin(u * 40f) * 0.008f * sip;
            rot = Quaternion.Euler(-38f * sip, 0f, 0f);
        }
        else pos = Vector3.Lerp(MouthLocal, HandLocal, Ease((u - 0.55f) / 0.2f));
        cup.localPosition = pos;
        cup.localRotation = rot;
        float shrink = 1f - Ease((u - 0.8f) / 0.2f);
        cup.localScale = Vector3.one * shrink / Mathf.Max(0.01f, transform.localScale.x);

        // steam rising from the cup at first
        if (u < 0.45f)
        {
            steamAccum += dt * 8f;
            while (steamAccum >= 1f)
            {
                steamAccum -= 1f;
                SimpleParticles.Instance.Emit(cup.position + Vector3.up * 0.18f, 1, SimpleParticles.Shape.Soft,
                                              new Color(1f, 1f, 1f, 0.4f), 0.14f, 1f, Vector3.up * 0.45f, 0.06f, 0f, 1.4f, 0.5f);
            }
        }
        if (!ahhDone && u >= 0.5f) Ahhh();
    }

    // "Ahhh…": happy hop, sparkles and the tip
    void Ahhh()
    {
        ahhDone = true;
        bubble?.Set(Loc.Get("customer.ahh"), null);
        var groove = GetComponentInChildren<AlienGroove>();
        if (groove != null) groove.TriggerHop();
        var audio = AudioManager.Instance;
        if (audio != null) { audio.Play("customer_ahh", transform.position); audio.Play("coin", transform.position, 1f, 0.25f); }
        SimpleParticles.Instance.Emit(transform.position + Vector3.up * 1.35f, 10, SimpleParticles.Shape.Star,
                                      new Color(1f, 0.6f, 0.78f, 1f), 0.11f, 1.1f, Vector3.up * 1.1f, 0.8f, 0.8f);
        if (pendingPay > 0)
        {
            CoinWallet.Add(pendingPay);
            if (GameUI.Instance != null) GameUI.Instance.ShowPopup(transform.position + Vector3.up * 2.2f, Loc.Get("customer.paid", pendingPay), new Color(1f, 0.83f, 0.45f));
            if (CoinFX.Instance != null) CoinFX.Instance.Burst(transform.position + Vector3.up * 1.1f, 3 + pendingTip / 2);
            pendingPay = 0;
        }
    }

    // language changed: rewrite the bubble
    void RefreshBubble()
    {
        if (bubble == null || Order == null) return;
        if (CurrentState == State.Waiting) bubble.Set(Order.Name, IngredientsText(Order));
        else if (CurrentState == State.Drinking) bubble.Set(ahhDone ? Loc.Get("customer.ahh") : "…", null);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Loc.Changed += RefreshBubble;
        var ui = GameUI.Instance;
        if (ui == null || bubble != null || Order == null) return;
        if (CurrentState == State.Waiting) { bubble = ui.CreateBubble(transform, bubbleHeight); bubble.Set(Order.Name, IngredientsText(Order)); }
        else if (CurrentState == State.Drinking) { bubble = ui.CreateBubble(transform, bubbleHeight); bubble.Set(ahhDone ? Loc.Get("customer.ahh") : "…", null); }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Loc.Changed -= RefreshBubble;
        bubble?.Close();
        bubble = null;
    }
}
