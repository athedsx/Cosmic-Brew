using System.Collections.Generic;
using UnityEngine;

// A xícara que o Ro carrega: ingredientes + visual 3D na frente dele
public class PlayerCarry : MonoBehaviour
{
    public const int MaxIngredients = 3;

    [Tooltip("Posição da xícara em relação ao Ro (entre as mãos)")]
    public Vector3 mugOffset = new Vector3(0f, 0.86f, 0.5f);

    private readonly List<Ingredient> cup = new List<Ingredient>();
    private GameObject mug;
    private Material liquidMat;
    private RoAnimator anim;
    private RoAction curAction;
    private float curProgress, pourAccum;
    private Vector3 handoff;

    public IReadOnlyList<Ingredient> Contents => cup;
    public bool HasCup => cup.Count > 0;
    public bool IsFull => cup.Count >= MaxIngredients;
    public Vector3 MugWorldPosition => mug != null ? mug.transform.position : transform.position + Vector3.up;
    public Color LiquidColor => liquidMat != null ? liquidMat.color : Color.white;
    public bool LiquidGlows { get { foreach (var i in cup) if (i == Ingredient.PoeiraEstelar) return true; return false; } }

    // A animação atual (o PlayerInteractor avisa a cada quadro)
    public void SetAction(RoAction action, float progress, Vector3 handoffPoint)
    {
        curAction = action;
        curProgress = progress;
        handoff = handoffPoint;
    }

    static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    // Xícara acompanhando a ação: despejar na lixeira, voar até o cliente ao servir
    void LateUpdate()
    {
        if (mug == null || !mug.activeSelf) return;
        float dt = Time.deltaTime;
        float p = curProgress;
        var mt = mug.transform;
        if (curAction == RoAction.Serve)
        {
            Vector3 from = transform.TransformPoint(mugOffset + new Vector3(0f, 0.05f, 0.2f));
            float f = Ease(p / 0.8f);
            Vector3 pos = Vector3.Lerp(from, handoff, f) + Vector3.up * 0.35f * Mathf.Sin(Mathf.PI * f);
            mt.position = pos;
            mt.rotation = Quaternion.Slerp(mt.rotation, transform.rotation, 1f - Mathf.Exp(-10f * dt));
            if (Random.value < dt * 6f)
                SimpleParticles.Instance.Emit(pos + Vector3.up * 0.15f, 1, SimpleParticles.Shape.Soft, new Color(1f, 1f, 1f, 0.35f), 0.12f, 0.9f, Vector3.up * 0.4f, 0.05f, 0f, 1.2f);
            return;
        }

        Vector3 target = mugOffset;
        Quaternion rot = Quaternion.identity;
        if (curAction == RoAction.Trash)
        {
            float lift = p < 0.6f ? Ease(p / 0.35f) : 1f - Ease((p - 0.6f) / 0.35f);
            target = mugOffset + new Vector3(0f, 0.32f * lift, 0.16f * lift);
            float pour = Ease((p - 0.3f) / 0.25f) * (1f - Ease((p - 0.75f) / 0.2f));
            rot = Quaternion.Euler(0f, 0f, 115f * pour);
            if (pour > 0.5f)
            {
                pourAccum += dt * 30f;
                while (pourAccum >= 1f)
                {
                    pourAccum -= 1f;
                    var c = LiquidColor; c.a = 0.9f;
                    SimpleParticles.Instance.Emit(mt.position + mt.right * -0.12f + Vector3.up * 0.05f, 1, SimpleParticles.Shape.Circle, c,
                                                  0.05f, 0.5f, Vector3.down * 0.3f, 0.08f, 6f);
                }
            }
        }
        float k = 1f - Mathf.Exp(-14f * dt);
        mt.localPosition = Vector3.Lerp(mt.localPosition, target, k);
        mt.localRotation = Quaternion.Slerp(mt.localRotation, rot, k);
    }

    void Start()
    {
        anim = GetComponent<RoAnimator>();
        BuildMug();
        Refresh();
    }

    public void Add(Ingredient i)
    {
        if (IsFull) return;
        cup.Add(i);
        Refresh();
    }

    public void Clear()
    {
        cup.Clear();
        Refresh();
    }

    void BuildMug()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var ceramic = new Material(lit) { color = new Color(1f, 0.95f, 0.88f) };
        ceramic.SetFloat("_Smoothness", 0.6f);
        liquidMat = new Material(lit);
        liquidMat.SetFloat("_Smoothness", 0.8f);

        mug = new GameObject("XicaraNaMao");
        mug.transform.SetParent(transform, false);
        mug.transform.localPosition = mugOffset;

        GameObject Part(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(mug.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }
        Part(PrimitiveType.Cylinder, "Corpo", Vector3.zero, new Vector3(0.24f, 0.12f, 0.24f), ceramic);
        Part(PrimitiveType.Cylinder, "Liquido", new Vector3(0f, 0.115f, 0f), new Vector3(0.2f, 0.01f, 0.2f), liquidMat);
        Part(PrimitiveType.Cube, "Alca", new Vector3(0.15f, 0.01f, 0f), new Vector3(0.05f, 0.13f, 0.04f), ceramic);
    }

    void Refresh()
    {
        if (mug != null)
        {
            bool wasHidden = !mug.activeSelf;
            mug.SetActive(HasCup);
            if (HasCup && wasHidden) { mug.transform.localPosition = mugOffset; mug.transform.localRotation = Quaternion.identity; }
        }
        if (HasCup && liquidMat != null)
        {
            Color c = Color.black;
            bool glow = false;
            foreach (var i in cup) { c += IngredientInfo.LiquidColor(i); glow |= i == Ingredient.PoeiraEstelar; }
            c /= cup.Count;
            liquidMat.color = c;
            if (glow) { liquidMat.EnableKeyword("_EMISSION"); liquidMat.SetColor("_EmissionColor", new Color(0.3f, 0.7f, 1.2f)); }
            else { liquidMat.DisableKeyword("_EMISSION"); liquidMat.SetColor("_EmissionColor", Color.black); }
        }
        if (anim != null) anim.holding = HasCup;
        if (GameUI.Instance != null) GameUI.Instance.SetCup(cup);
    }
}
