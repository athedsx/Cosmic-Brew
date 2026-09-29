using UnityEngine;

// Estação que adiciona um ingrediente na xícara (máquina de café, moedor de poeira estelar...)
public class Station : Interactable
{
    public Ingredient ingredient;
    [Tooltip("Chave de tradução da ação no aviso, ex.: station.coffee")]
    public string actionText = "station.coffee";

    public enum Fx { None, Steam, Stardust }
    [Tooltip("Efeito visual enquanto o Ro prepara")]
    public Fx fx = Fx.None;
    [Tooltip("Altura (acima da mesa) de onde sai o efeito")]
    public float fxHeight = 1.7f;

    private Transform model;
    private Vector3 modelBasePos;
    private Quaternion modelBaseRot;
    private float emitAccum;

    Vector3 FxPoint => transform.position + Vector3.up * fxHeight;

    public override void OnWorkStart(PlayerCarry player)
    {
        model = null;
        foreach (Transform c in transform) if (c.name.StartsWith("Modelo_")) { model = c; break; }
        if (model != null) { modelBasePos = model.localPosition; modelBaseRot = model.localRotation; }
        emitAccum = 0f;
    }

    public override void OnWorkProgress(PlayerCarry player, float p)
    {
        var fxs = SimpleParticles.Instance;
        float dt = Time.deltaTime;
        if (fx == Fx.Steam && p > 0.3f)
        {
            // máquina vibrando + vapor subindo
            if (model != null) model.localPosition = modelBasePos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.006f;
            emitAccum += dt * 16f;
            while (emitAccum >= 1f)
            {
                emitAccum -= 1f;
                fxs.Emit(FxPoint, 1, SimpleParticles.Shape.Soft, new Color(1f, 1f, 1f, 0.55f), 0.22f, 1.3f,
                         new Vector3(0f, 0.7f, 0f), 0.12f, -0.1f, 1.8f, 0.5f);
            }
        }
        else if (fx == Fx.Stardust)
        {
            // moedor tremendo no ritmo da manivela + poeira estelar brilhando
            if (model != null) model.localRotation = modelBaseRot * Quaternion.Euler(0f, Mathf.Sin(p * Mathf.PI * 2f * 5f) * 4f, 0f);
            emitAccum += dt * 22f;
            while (emitAccum >= 1f)
            {
                emitAccum -= 1f;
                fxs.Emit(FxPoint, 1, SimpleParticles.Shape.Star, new Color(0.6f, 0.92f, 1f, 1f), 0.09f, 0.8f,
                         new Vector3(0f, 0.5f, 0f), 0.6f, 0.4f, 0f, 1f);
            }
        }
    }

    public override void OnWorkEnd(PlayerCarry player)
    {
        if (model != null) { model.localPosition = modelBasePos; model.localRotation = modelBaseRot; }
        var fxs = SimpleParticles.Instance;
        if (fx == Fx.Steam)
            fxs.Emit(FxPoint, 5, SimpleParticles.Shape.Soft, new Color(1f, 1f, 1f, 0.5f), 0.3f, 1.2f, Vector3.up * 0.8f, 0.3f, 0f, 1.5f, 0.8f);
        else if (fx == Fx.Stardust)
            fxs.Emit(FxPoint, 12, SimpleParticles.Shape.Star, new Color(0.7f, 0.95f, 1f, 1f), 0.12f, 1f, Vector3.up * 0.6f, 1.2f, 1f, 0f, 1.5f);
    }

    public override string GetPrompt(PlayerCarry player, out bool enabled)
    {
        if (player.IsFull)
        {
            enabled = false;
            return Loc.Get("station.full");
        }
        enabled = true;
        return Loc.KeyOr(actionText, actionText);
    }

    public override void Interact(PlayerCarry player)
    {
        player.Add(ingredient);
        if (GameUI.Instance != null)
            GameUI.Instance.ShowPopup(PromptPosition, Loc.Get("station.added", IngredientInfo.Name(ingredient)), IngredientInfo.UiOnDark(ingredient));
    }
}
