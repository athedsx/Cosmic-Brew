using UnityEngine;

// Customer shuttle: flies in from space and hovers just outside the island edge;
// the customer steps off, and when they come back the ship leaves.
public class ShipArrival : MonoBehaviour
{
    public enum State { FlyingIn, Parked, FlyingOut }

    public float flySpeed = 6f;
    public float hoverAmplitude = 0.06f;

    public State CurrentState { get; private set; }
    public bool Parked => CurrentState == State.Parked;
    public Vector3 ParkPosition => parkPos;

    private Vector3 parkPos, startPos, endPos;
    private float t, bobOffset;
    private float flyTime;
    private Light flameLight;

    public void Init(Vector3 park, Vector3 outward)
    {
        parkPos = park;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Vector3.back;
        // arrives from far away, from the side and above (smooth curve), and leaves the other way
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        startPos = park + outward * 26f + side * 14f + Vector3.up * 7f;
        endPos = park + outward * 26f - side * 14f + Vector3.up * 9f;
        transform.position = startPos;
        flyTime = Vector3.Distance(startPos, park) / flySpeed;
        CurrentState = State.FlyingIn;
        t = 0f;
        bobOffset = Random.value * 10f;
        flameLight = GetComponentInChildren<Light>();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayShip(park, 0.7f);
    }

    public void Depart()
    {
        if (CurrentState != State.Parked) return;
        CurrentState = State.FlyingOut;
        t = 0f;
        flyTime = Vector3.Distance(parkPos, endPos) / flySpeed;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayShip(parkPos, 0.7f);
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float u) =>
        Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(b, c, u), u);

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        Vector3 prev = transform.position;
        switch (CurrentState)
        {
            case State.FlyingIn:
            {
                float u = Mathf.Clamp01(t / flyTime);
                float e = 1f - (1f - u) * (1f - u); // eases out while parking
                Vector3 mid = parkPos + (startPos - parkPos) * 0.35f + Vector3.up * 1.5f;
                transform.position = Bezier(startPos, mid, parkPos, e);
                Face(transform.position - prev, dt, e);
                if (u >= 1f) { CurrentState = State.Parked; t = 0f; }
                break;
            }
            case State.Parked:
                transform.position = parkPos + Vector3.up * Mathf.Sin((Time.time + bobOffset) * 1.6f) * hoverAmplitude;
                break;
            case State.FlyingOut:
            {
                float u = Mathf.Clamp01(t / flyTime);
                float e = u * u; // eases in when leaving
                Vector3 mid = parkPos + Vector3.up * 2f + (endPos - parkPos) * 0.2f;
                transform.position = Bezier(parkPos, mid, endPos, e);
                Face(transform.position - prev, dt, 1f);
                if (u >= 1f) Destroy(gameObject);
                break;
            }
        }
        if (flameLight != null)
        {
            float moving = CurrentState == State.Parked ? 0.35f : 1f;
            flameLight.intensity = 1.6f * moving * (1f + Mathf.Sin(Time.time * 40f) * 0.15f);
        }
    }

    void Face(Vector3 delta, float dt, float settle)
    {
        delta.y *= 0.3f;
        if (delta.sqrMagnitude < 1e-6f) return;
        var look = Quaternion.LookRotation(delta.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-6f * dt));
    }
}
