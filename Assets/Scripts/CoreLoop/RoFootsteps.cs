using UnityEngine;

// Passinhos do Ro sincronizados com a animação de caminhada
[RequireComponent(typeof(RoAnimator))]
public class RoFootsteps : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.55f;

    public int StepsPlayed { get; private set; }

    private RoAnimator anim;
    private float lastSin;

    void Awake() => anim = GetComponent<RoAnimator>();

    void Update()
    {
        float s = Mathf.Sin(anim.WalkPhase);
        // um passo cada vez que a perna cruza o meio do balanço
        if (anim.WalkBlend > 0.3f && Mathf.Sign(s) != Mathf.Sign(lastSin) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("step", transform.position, volume * anim.WalkBlend);
            StepsPlayed++;
        }
        lastSin = s;
    }
}
