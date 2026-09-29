using UnityEngine;

// Cliente curtindo o lo-fi: balança no ritmo (squash & stretch), inclina de um lado pro outro
// e de vez em quando dá um pulinho feliz. Pivô do modelo deve estar nos pés.
public class AlienGroove : MonoBehaviour
{
    public float bpm = 78f;
    public float squash = 0.06f;
    public float swayAngle = 5f;
    public Vector2 hopInterval = new Vector2(7f, 12f);
    public float hopHeight = 0.18f;

    private Vector3 baseScale, basePos;
    private Quaternion baseRot;
    private float timeOffset, nextHop, hopStart = -10f;
    private const float HopDuration = 0.55f;

    void Start()
    {
        Setup();
        timeOffset = Random.value * 10f;
        nextHop = Time.time + Random.Range(hopInterval.x, hopInterval.y);
    }

    public void Setup()
    {
        baseScale = transform.localScale;
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
    }

    // pulinho feliz agora (ex.: depois do gole)
    public void TriggerHop()
    {
        hopStart = Time.time;
        nextHop = Time.time + HopDuration + Random.Range(hopInterval.x, hopInterval.y);
    }

    void Update()
    {
        // pulinho feliz
        if (Time.time > nextHop) { hopStart = Time.time; nextHop = Time.time + HopDuration + Random.Range(hopInterval.x, hopInterval.y); }
        float h = (Time.time - hopStart) / HopDuration;
        float hop = (h >= 0f && h <= 1f) ? Mathf.Sin(h * Mathf.PI) : 0f;
        ApplyPose(Time.time + timeOffset, hop);
    }

    // Pose determinística (também usada para pré-visualizar no editor)
    public void ApplyPose(float t, float hop)
    {
        float beat = t * bpm / 60f;

        // pulso no tempo: afunda rápido e volta suave
        float frac = beat - Mathf.Floor(beat);
        float pulse = Mathf.Exp(-frac * 6f);

        float sy = 1f - squash * pulse + hop * 0.08f;
        float sxz = 1f + squash * 0.5f * pulse - hop * 0.04f;
        transform.localScale = new Vector3(baseScale.x * sxz, baseScale.y * sy, baseScale.z * sxz);
        transform.localPosition = basePos + Vector3.up * hop * hopHeight;

        // inclina alternando a cada batida (esquerda / direita)
        float sway = Mathf.Sin(beat * Mathf.PI) * swayAngle;
        float nod = pulse * 3f;
        transform.localRotation = baseRot * Quaternion.Euler(nod, 0f, sway);
    }
}
