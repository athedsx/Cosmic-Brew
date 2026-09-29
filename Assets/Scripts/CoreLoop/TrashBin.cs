using UnityEngine;

// Zen orders: wrong drink? dump it and start over, no penalty
public class TrashBin : Interactable
{
    private Transform lid, knob;
    private Vector3 lidBase, knobBase;

    public override void OnWorkStart(PlayerCarry player)
    {
        lid = transform.Find("Modelo_Tampa");
        if (lid == null) lid = transform.Find("Placeholder_Tampa");
        knob = transform.Find("Placeholder_Pegador");
        if (lid != null) lidBase = lid.localPosition;
        if (knob != null) knobBase = knob.localPosition;
    }

    // the lid pops open while Ro pours
    public override void OnWorkProgress(PlayerCarry player, float p)
    {
        float open = Mathf.Sin(Mathf.Clamp01((p - 0.2f) / 0.7f) * Mathf.PI);
        Vector3 up = Vector3.up * 0.18f * open;
        if (lid != null) { lid.localPosition = lidBase + up; lid.localRotation = Quaternion.Euler(0f, 0f, -35f * open); }
        if (knob != null) knob.localPosition = knobBase + up + Vector3.right * -0.05f * open;
    }

    public override void OnWorkEnd(PlayerCarry player)
    {
        if (lid != null) { lid.localPosition = lidBase; lid.localRotation = Quaternion.identity; }
        if (knob != null) knob.localPosition = knobBase;
        SimpleParticles.Instance.Emit(transform.position + Vector3.up * 0.95f, 8, SimpleParticles.Shape.Soft,
                                      new Color(0.85f, 0.78f, 1f, 0.6f), 0.25f, 0.8f, Vector3.up * 0.5f, 0.6f, 0f, 1.2f, 1.5f);
    }

    public override string GetPrompt(PlayerCarry player, out bool enabled)
    {
        enabled = player.HasCup;
        return player.HasCup ? Loc.Get("trash.prompt") : null;
    }

    public override void Interact(PlayerCarry player)
    {
        player.Clear();
        if (GameUI.Instance != null)
            GameUI.Instance.ShowPopup(PromptPosition, Loc.Get("trash.popup"), new Color(0.8f, 0.74f, 0.95f));
    }
}
