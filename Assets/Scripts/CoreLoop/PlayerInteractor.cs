using UnityEngine;
using UnityEngine.InputSystem;

// Mostra o aviso do objeto interagível mais próximo e executa a ação com E (ou Espaço)
[RequireComponent(typeof(PlayerCarry))]
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("Aperto simulado de E (para testes automáticos)")]
    public bool simulatePress;

    private PlayerCarry carry;
    private RobotMovement move;
    private RoAnimator anim;
    private Rigidbody rb;
    private Interactable working;
    private string workingText;
    private float workTime;

    void Awake()
    {
        carry = GetComponent<PlayerCarry>();
        move = GetComponent<RobotMovement>();
        anim = GetComponent<RoAnimator>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        var ui = GameUI.Instance;

        // fazendo alguma coisa: fica parado, virado para o objeto, com barra de progresso
        if (working != null)
        {
            workTime += Time.deltaTime;
            float progress = Mathf.Clamp01(workTime / Mathf.Max(0.01f, working.duration));
            FaceTowards(working.transform.position);
            if (anim != null) anim.actionProgress = progress;
            carry.SetAction(working.roAction, progress, working.HandoffPoint);
            working.OnWorkProgress(carry, progress);
            if (ui != null) ui.SetPrompt(working.PromptPosition, workingText, true, progress);
            if (workTime >= working.duration)
            {
                var target = working;
                working = null;
                SetBusy(false);
                target.OnWorkEnd(carry);
                if (!string.IsNullOrEmpty(target.doneSfx) && AudioManager.Instance != null) AudioManager.Instance.Play(target.doneSfx, target.transform.position);
                target.Interact(carry);
            }
            return;
        }

        // escolhe o interagível mais próximo que tem algo a dizer
        Interactable best = null;
        string bestText = null;
        bool bestEnabled = false;
        float bestDist = float.MaxValue;
        Vector3 me = transform.position;
        foreach (var it in Interactable.All)
        {
            if (it == null || !it.isActiveAndEnabled) continue;
            Vector3 d = it.transform.position - me; d.y = 0f;
            float dist = d.magnitude;
            if (dist > it.interactRadius || dist >= bestDist) continue;
            string text = it.GetPrompt(carry, out bool enabled);
            if (text == null) continue;
            best = it; bestText = text; bestEnabled = enabled; bestDist = dist;
        }

        bool pressed = simulatePress;
        simulatePress = false;
        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) pressed = true;

        if (best == null)
        {
            if (ui != null) ui.HidePrompt();
            return;
        }
        if (ui != null) ui.SetPrompt(best.PromptPosition, bestText, bestEnabled, -1f);

        if (pressed && bestEnabled)
        {
            working = best;
            workingText = bestText;
            workTime = 0f;
            SetBusy(true);
            best.OnWorkStart(carry);
            if (!string.IsNullOrEmpty(best.actionSfx) && AudioManager.Instance != null) AudioManager.Instance.Play(best.actionSfx, best.transform.position);
        }
    }

    void SetBusy(bool busy)
    {
        if (move != null) move.movementLocked = busy;
        if (anim != null)
        {
            anim.working = busy;
            anim.action = busy && working != null ? working.roAction : RoAction.None;
            anim.actionProgress = 0f;
        }
        if (!busy) carry.SetAction(RoAction.None, 0f, Vector3.zero);
    }

    void FaceTowards(Vector3 target)
    {
        Vector3 d = target - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(d.normalized, Vector3.up);
        Quaternion next = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-10f * Time.deltaTime));
        if (rb != null) rb.MoveRotation(next); else transform.rotation = next;
    }
}
