using UnityEngine;

// Actions with their own animation (set by each station/customer)
public enum RoAction { None, Brew, Grind, Trash, Radio, Serve }

// Procedural animation for Ro (rigid parts): a "cozy tired" idle and a walk cycle,
// blended by the Rigidbody speed.
public class RoAnimator : MonoBehaviour
{
    [Header("Parts (auto-filled by name)")]
    public Transform hips, head, armL, armR, legL, legR;

    [Header("Tired idle")]
    [Tooltip("Angle of the arms hanging at the sides (from the T-pose)")]
    public float armRestAngle = 74f;
    public float slumpAngle = 5f;         // torso leaning forward
    public float headDroop = 10f;         // drooping head
    public float breathRate = 0.22f;      // breaths per second
    public Vector2 sighInterval = new Vector2(6f, 10f);

    [Header("Walk")]
    public float maxSpeed = 3.5f;
    public float stepsPerSecond = 1.6f;
    public float legSwing = 28f;
    public float armSwing = 22f;
    public float bobHeight = 0.018f;

    [Header("Actions (driven by the core loop)")]
    [Tooltip("Holding the cup: arms forward")]
    public bool holding;
    [Tooltip("Working at a station: busy arms and head looking down")]
    public bool working;
    [Tooltip("Current action (drives the arm animation)")]
    public RoAction action;
    [Tooltip("Action progress, 0..1")]
    [Range(0f, 1f)] public float actionProgress;

    private float holdBlend, workBlend;

    public float WalkPhase => phase;
    public float WalkBlend => walkBlend;
    private Rigidbody rb;
    private Vector3 hipsBase, headBase;
    private float phase, walkBlend, nextSigh, sighStart = -10f;
    private const float SighDuration = 1.8f;

    void Awake() { Setup(); }

    public void Setup()
    {
        rb = GetComponent<Rigidbody>();
        Transform rig = transform.Find("Modelo_Ro/Rig");
        if (rig != null)
        {
            if (hips == null) hips = rig.Find("Hips");
            if (head == null) head = rig.Find("Hips/Head");
            if (armL == null) armL = rig.Find("Hips/ArmL");
            if (armR == null) armR = rig.Find("Hips/ArmR");
            if (legL == null) legL = rig.Find("LegL");
            if (legR == null) legR = rig.Find("LegR");
        }
        if (hips != null) hipsBase = hips.localPosition;
        if (head != null) headBase = head.localPosition;
        nextSigh = Time.time + Random.Range(sighInterval.x, sighInterval.y);
    }

    void LateUpdate()
    {
        if (hips == null) return;
        float dt = Time.deltaTime;
        float t = Time.time;

        // horizontal speed -> idle/walk blend
        float speed = 0f;
        if (rb != null) { Vector3 v = rb.linearVelocity; v.y = 0f; speed = v.magnitude; }
        float target = Mathf.Clamp01(speed / (maxSpeed * 0.6f));
        walkBlend = Mathf.Lerp(walkBlend, target, 1f - Mathf.Exp(-8f * dt));
        phase += dt * stepsPerSecond * Mathf.Lerp(0.6f, 1.2f, target) * Mathf.PI * 2f * walkBlend;

        // occasional tired sigh (only when standing still)
        if (t > nextSigh && walkBlend < 0.1f) { sighStart = t; nextSigh = t + SighDuration + Random.Range(sighInterval.x, sighInterval.y); }
        float u = (t - sighStart) / SighDuration;
        float sigh = (u >= 0f && u <= 1f) ? Mathf.Sin(u * Mathf.PI) : 0f;
        sigh *= 1f - walkBlend;

        holdBlend = Mathf.Lerp(holdBlend, holding ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
        workBlend = Mathf.Lerp(workBlend, working ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
        sigh *= 1f - workBlend;

        ApplyPose(t, walkBlend, phase, sigh);
    }

    // Deterministic pose (also used for editor previews)
    public void ApplyPose(float t, float walkBlend, float phase, float sigh)
    {
        if (hips == null) return;
        float breath = Mathf.Sin(t * breathRate * Mathf.PI * 2f);
        float s = Mathf.Sin(phase);
        float bounce = Mathf.Abs(Mathf.Sin(phase));

        // ---------- torso ----------
        float idleLean = slumpAngle + breath * 1.2f + sigh * 7f;
        float walkLean = 7f;
        float lean = Mathf.Lerp(idleLean, walkLean, walkBlend);
        float roll = Mathf.Lerp(Mathf.Sin(t * 0.35f) * 1.5f, s * 3.5f, walkBlend);
        hips.localRotation = Quaternion.Euler(lean, 0f, roll);
        float idleY = breath * 0.006f - sigh * 0.015f;
        float walkY = bounce * bobHeight;
        hips.localPosition = hipsBase + Vector3.up * Mathf.Lerp(idleY, walkY, walkBlend);

        // ---------- head ----------
        if (head != null)
        {
            float idlePitch = headDroop + breath * 2f + sigh * 10f;
            float idleYaw = Mathf.Sin(t * 0.27f) * 10f + Mathf.Sin(t * 0.61f) * 3f;   // looking around, unhurried
            float idleRoll = Mathf.Sin(t * 0.4f) * 4f;
            float walkPitch = 4f + bounce * 3f;
            float actionPitch = action == RoAction.Brew || action == RoAction.Grind ? 12f
                              : action == RoAction.Trash ? 6f : action == RoAction.Serve ? -4f : 8f;
            head.localRotation = Quaternion.Euler(
                Mathf.Lerp(idlePitch, walkPitch, walkBlend) + workBlend * actionPitch,
                Mathf.Lerp(idleYaw, 0f, walkBlend),
                Mathf.Lerp(idleRoll, -s * 3f, walkBlend));
            head.localPosition = headBase;
        }

        // ---------- arms (hanging + swing) ----------
        float idleArm = breath * 2f + sigh * 4f;
        float swing = s * armSwing * walkBlend * (1f - 0.8f * holdBlend);
        // holding the cup: arms forward; working: arms moving alternately
        float carry = -55f * holdBlend;
        ActionArms(t, out float workL, out float workR, out float zL, out float zR);
        workL *= workBlend; workR *= workBlend; zL *= workBlend; zR *= workBlend;
        float inward = 8f * Mathf.Max(holdBlend, workBlend);
        if (armL != null) armL.localRotation = Quaternion.Euler(-swing + idleArm * 0.5f + Mathf.Min(carry, 0f) * (1f - workBlend) + workL, 0f, 0f) * Quaternion.Euler(0f, 0f, armRestAngle - idleArm + inward + zL);
        if (armR != null) armR.localRotation = Quaternion.Euler(swing + idleArm * 0.5f + Mathf.Min(carry, 0f) * (1f - workBlend) + workR, 0f, 0f) * Quaternion.Euler(0f, 0f, -armRestAngle + idleArm - inward - zR);

        // ---------- legs ----------
        float leg = s * legSwing * walkBlend;
        if (legL != null) legL.localRotation = Quaternion.Euler(leg, 0f, 0f);
        if (legR != null) legR.localRotation = Quaternion.Euler(-leg, 0f, 0f);
    }

    static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    static float Bump(float x) => x <= 0f || x >= 1f ? 0f : Mathf.Sin(x * Mathf.PI);

    // Arm poses per action. Negative X = arm forward; z = arm inward.
    void ActionArms(float t, out float xL, out float xR, out float zL, out float zR)
    {
        float p = actionProgress;
        zL = zR = 0f;
        switch (action)
        {
            case RoAction.Brew:
            {
                // reaches for the machine, presses the button and waits, swaying to the beat
                float reach = Ease(p / 0.2f);
                xL = xR = -62f * reach;
                xR += 22f * Bump((p - 0.24f) / 0.18f);           // presses the button
                float wait = Ease((p - 0.45f) / 0.1f);
                xL += Mathf.Sin(t * 5f) * 4f * wait;
                xR += Mathf.Sin(t * 5f + 1f) * 4f * wait;
                break;
            }
            case RoAction.Grind:
            {
                // left hand holds the grinder; the right one turns the crank
                float reach = Ease(p / 0.15f);
                float ang = p * Mathf.PI * 2f * 2.5f;
                xL = -52f * reach;
                xR = (-60f + 24f * Mathf.Sin(ang)) * reach;
                zR = 14f * Mathf.Cos(ang) * reach;
                zL = 6f * reach;
                break;
            }
            case RoAction.Trash:
            {
                // lifts the cup, tips it to pour and lowers it
                float lift = p < 0.6f ? Ease(p / 0.35f) : 1f - Ease((p - 0.6f) / 0.35f);
                xL = xR = -58f - 40f * lift;
                xR -= 12f * Bump((p - 0.38f) / 0.25f);
                break;
            }
            case RoAction.Radio:
            {
                // two taps on the radio button
                float reach = Ease(p / 0.2f) * (1f - Ease((p - 0.85f) / 0.15f));
                xL = -8f * reach;
                xR = -72f * reach + 16f * Mathf.Abs(Mathf.Sin(Mathf.Clamp01((p - 0.2f) / 0.6f) * Mathf.PI * 2f));
                break;
            }
            case RoAction.Serve:
            {
                // stretches the arms out offering the cup
                float ext = Ease(p / 0.55f);
                xL = xR = -55f - 30f * ext;
                zL = zR = 6f * ext;
                break;
            }
            default:
                xL = -45f + Mathf.Sin(t * 9f) * 14f;
                xR = -45f - Mathf.Sin(t * 9f) * 14f;
                break;
        }
    }
}
