using System.Collections.Generic;
using UnityEngine;

// Something Ro can interact with by pressing E when nearby
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Tooltip("Floor distance at which the interaction prompt appears")]
    public float interactRadius = 1.9f;
    [Tooltip("Prompt height above the object")]
    public float promptHeight = 1.9f;
    [Tooltip("Action duration (Ro stays in place while doing it)")]
    public float duration = 0.5f;
    [Tooltip("Sound when the action starts (AudioManager name)")]
    public string actionSfx;
    [Tooltip("Sound when the action ends (AudioManager name)")]
    public string doneSfx;
    [Tooltip("Ro's animation during the action")]
    public RoAction roAction = RoAction.None;

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    public virtual Vector3 PromptPosition => transform.position + Vector3.up * promptHeight;

    // Prompt text; null = show nothing. enabled=false shows a dimmed prompt (no action).
    public abstract string GetPrompt(PlayerCarry player, out bool enabled);

    public abstract void Interact(PlayerCarry player);

    // Animation/effect hooks while Ro performs the action (progress 0..1)
    public virtual void OnWorkStart(PlayerCarry player) { }
    public virtual void OnWorkProgress(PlayerCarry player, float progress) { }
    public virtual void OnWorkEnd(PlayerCarry player) { }

    // Where the cup goes when serving (customers)
    public virtual Vector3 HandoffPoint => transform.position + Vector3.up * 1f;
}
