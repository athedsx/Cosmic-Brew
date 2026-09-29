using UnityEngine;

// Rádio interativo (GDD): o Ro vai até ele e "troca a fita" para mudar a música lo-fi
public class RadioStation : Interactable
{
    private Transform model;
    private Vector3 baseScale;

    public override void OnWorkStart(PlayerCarry player)
    {
        model = transform.Find("Modelo_Radio");
        if (model != null) baseScale = model.localScale;
    }

    // o rádio dá pulinhos a cada toque
    public override void OnWorkProgress(PlayerCarry player, float p)
    {
        if (model == null) return;
        float poke = Mathf.Abs(Mathf.Sin(Mathf.Clamp01((p - 0.2f) / 0.6f) * Mathf.PI * 2f));
        model.localScale = new Vector3(baseScale.x * (1f + 0.06f * poke), baseScale.y * (1f - 0.08f * poke), baseScale.z * (1f + 0.06f * poke));
    }

    public override void OnWorkEnd(PlayerCarry player)
    {
        if (model != null) model.localScale = baseScale;
        SimpleParticles.Instance.Emit(transform.position + Vector3.up * 1.5f, 10, SimpleParticles.Shape.Star,
                                      new Color(0.62f, 0.95f, 0.8f, 1f), 0.1f, 1.1f, Vector3.up * 0.9f, 0.7f, 0.6f);
    }

    public override string GetPrompt(PlayerCarry player, out bool enabled)
    {
        enabled = AudioManager.Instance != null;
        return Loc.Get("radio.prompt");
    }

    public override void Interact(PlayerCarry player)
    {
        var audio = AudioManager.Instance;
        if (audio == null) return;
        string title = audio.NextTrack();
        if (GameUI.Instance != null) GameUI.Instance.ShowPopup(PromptPosition, Loc.Get("radio.popup", title), new Color(0.62f, 0.9f, 0.76f));
        if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
    }
}
