using UnityEngine;

// Ro's footsteps, synced with the walk animation
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
        // one step each time a leg crosses the middle of the swing
        if (anim.WalkBlend > 0.3f && Mathf.Sign(s) != Mathf.Sign(lastSin) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("step", transform.position, volume * anim.WalkBlend);
            StepsPlayed++;
        }
        lastSin = s;
    }
}
