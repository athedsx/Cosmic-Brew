using System.Collections.Generic;
using UnityEngine;

// Algo com que o Ro pode interagir apertando E quando está perto
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Tooltip("Distância (no chão) para aparecer o aviso de interação")]
    public float interactRadius = 1.9f;
    [Tooltip("Altura do aviso acima do objeto")]
    public float promptHeight = 1.9f;
    [Tooltip("Tempo da ação (o Ro fica parado fazendo)")]
    public float duration = 0.5f;
    [Tooltip("Som ao começar a ação (nome no AudioManager)")]
    public string actionSfx;
    [Tooltip("Som ao terminar a ação (nome no AudioManager)")]
    public string doneSfx;
    [Tooltip("Animação do Ro durante a ação")]
    public RoAction roAction = RoAction.None;

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    public virtual Vector3 PromptPosition => transform.position + Vector3.up * promptHeight;

    // Texto do aviso; null = não mostrar nada. enabled=false mostra o aviso apagado (sem ação).
    public abstract string GetPrompt(PlayerCarry player, out bool enabled);

    public abstract void Interact(PlayerCarry player);

    // Ganchos de animação/efeitos enquanto o Ro faz a ação (progress 0..1)
    public virtual void OnWorkStart(PlayerCarry player) { }
    public virtual void OnWorkProgress(PlayerCarry player, float progress) { }
    public virtual void OnWorkEnd(PlayerCarry player) { }

    // Para onde a xícara vai ao servir (clientes)
    public virtual Vector3 HandoffPoint => transform.position + Vector3.up * 1f;
}
