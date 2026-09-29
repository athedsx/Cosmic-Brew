using UnityEngine;

// Test tool: walks Ro along a route and logs where he gets stuck.
// Added only during tests (not kept in the scene).
public class WalkAutopilot : MonoBehaviour
{
    public Vector3[] waypoints;
    public float reachRadius = 0.35f;
    public int laps = 1;

    RobotMovement move;
    Rigidbody rb;
    int index, lap;
    float stuckTime, startTime;
    int stuckEvents;

    void Start()
    {
        move = GetComponent<RobotMovement>();
        rb = GetComponent<Rigidbody>();
        startTime = Time.time;
        Debug.Log($"[Autopilot] start pos={transform.position:F2}");
    }

    void Update()
    {
        if (move == null || waypoints == null || waypoints.Length == 0) return;
        if (lap >= laps) { move.simulatedWorldDirection = Vector3.zero; return; }

        Vector3 to = waypoints[index] - transform.position; to.y = 0f;
        if (to.magnitude < reachRadius)
        {
            Debug.Log($"[Autopilot] waypoint {index} ok t={Time.time - startTime:F1}s");
            index++;
            if (index >= waypoints.Length) { index = 0; lap++; if (lap >= laps) Debug.Log($"[Autopilot] DONE stuck={stuckEvents} time={Time.time - startTime:F1}s"); }
            return;
        }
        move.simulatedWorldDirection = to.normalized;

        Vector3 v = rb.linearVelocity; v.y = 0f;
        if (Time.time - startTime > 0.5f && v.magnitude < 0.4f) stuckTime += Time.deltaTime; else stuckTime = 0f;
        if (stuckTime > 0.6f)
        {
            stuckEvents++;
            Debug.LogWarning($"[Autopilot] STUCK heading to waypoint {index} at pos={transform.position:F2}");
            stuckTime = -2f;
        }
    }
}
