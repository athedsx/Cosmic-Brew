using UnityEngine;
using UnityEngine.InputSystem;

// Cozy isometric camera:
// - follows Ro with SmoothDamp and looks slightly ahead of where he walks;
// - orbits the kiosk as Ro walks, always staying on his side
//   (so the building never hides the character);
// - zooms out a little while he walks and back in when he stops;
// - Z/C rotate the camera and the mouse wheel zooms.
public class IsometricCameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Follow")]
    [Tooltip("Time for the camera to catch up with the target (slow, smooth vibe)")]
    public float smoothTime = 0.35f;
    [Tooltip("How far to look ahead of the movement (seconds of velocity)")]
    public float lookAhead = 0.45f;
    public float focusHeight = 0.8f;
    public float distance = 30f;
    [Range(15f, 60f)] public float pitch = 32f;

    [Header("Orbit around the kiosk")]
    public bool orbitWithPlayer = true;
    [Tooltip("Orbit center (the kiosk building)")]
    public Vector3 orbitCenter = new Vector3(0f, 0f, 1.2f);
    [Tooltip("Extra angle that keeps a diagonal (isometric) view instead of a frontal one")]
    public float yawBias = 25f;
    [Tooltip("Time for the camera to rotate to the new view")]
    public float yawSmoothTime = 1.1f;
    [Tooltip("Below this distance from the center the camera stops orbiting (avoids jitter)")]
    public float minOrbitRadius = 1.2f;

    [Header("Zoom")]
    public float idleSize = 4.6f;
    public float movingSize = 5.4f;
    public float zoomSmoothTime = 0.9f;
    public Vector2 zoomLimits = new Vector2(3f, 9f);

    [Header("Build Mode")]
    [Tooltip("Top-down view of the whole island (enabled by Build Mode)")]
    public bool buildView;
    public Vector3 buildFocus = new Vector3(0f, 0f, -0.3f);
    [Tooltip("Shifts the island up on screen to make room for the catalog")]
    public float buildFocusDrop = 2.6f;
    public float buildSize = 8.8f;
    public float buildPitch = 52f;

    [Header("Title menu")]
    [Tooltip("Camera slowly circling the island (title screen)")]
    public bool menuView;
    public float menuSpinSpeed = 5f;
    public float menuSize = 7.6f;
    public float menuPitch = 26f;
    [Tooltip("Shifts the island to the right of the screen (the menu sits on the left)")]
    public float menuSideShift = 4.2f;

    [Header("Manual controls")]
    public float manualRotateSpeed = 90f;
    public float scrollZoomStep = 0.5f;

    private Camera cam;
    private Rigidbody targetRb;
    private Vector3 focus, focusVel;
    private float yaw, yawVel, size, sizeVel, zoomOffset, manualYaw, orbitYaw, currentPitch, pitchVel;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            focus = target.position + Vector3.up * focusHeight;
        }
        yaw = transform.eulerAngles.y;
        orbitYaw = yaw;
        if (orbitWithPlayer && target != null) yaw = orbitYaw = DesiredYaw();
        currentPitch = pitch;
        size = cam != null && cam.orthographic ? cam.orthographicSize : idleSize;
        Place();
    }

    float DesiredYaw()
    {
        Vector3 fromCenter = target.position - orbitCenter;
        fromCenter.y = 0f;
        if (fromCenter.magnitude < minOrbitRadius) return orbitYaw;
        // camera on Ro's side, looking toward the kiosk
        float radial = Mathf.Atan2(-fromCenter.x, -fromCenter.z) * Mathf.Rad2Deg;
        return radial + yawBias;
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;

        if (menuView)
        {
            orbitYaw += menuSpinSpeed * Time.unscaledDeltaTime;
            yaw = Mathf.SmoothDampAngle(yaw, orbitYaw, ref yawVel, 0.6f, Mathf.Infinity, Time.unscaledDeltaTime);
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            focus = Vector3.SmoothDamp(focus, orbitCenter - right * menuSideShift, ref focusVel, 0.8f, Mathf.Infinity, Time.unscaledDeltaTime);
            size = Mathf.SmoothDamp(size, menuSize, ref sizeVel, 0.8f, Mathf.Infinity, Time.unscaledDeltaTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, menuPitch, ref pitchVel, 0.8f, Mathf.Infinity, Time.unscaledDeltaTime);
            Place();
            return;
        }

        // manual controls
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.zKey.isPressed) manualYaw -= manualRotateSpeed * dt;
            if (kb.cKey.isPressed) manualYaw += manualRotateSpeed * dt;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) zoomOffset -= Mathf.Sign(scroll) * scrollZoomStep;
        }

        // focus with look-ahead
        Vector3 vel = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        vel.y = 0f;
        Vector3 flatForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 desiredFocus = buildView ? buildFocus - flatForward * buildFocusDrop : target.position + Vector3.up * focusHeight + vel * lookAhead;
        focus = Vector3.SmoothDamp(focus, desiredFocus, ref focusVel, smoothTime);

        // camera yaw following Ro's path
        if (orbitWithPlayer && !buildView) orbitYaw = DesiredYaw();
        float desiredYaw = orbitYaw + manualYaw;
        yaw = Mathf.SmoothDampAngle(yaw, desiredYaw, ref yawVel, yawSmoothTime);

        // zoom: out while walking, in when idle
        float moving = Mathf.Clamp01(vel.magnitude / 2.5f);
        float baseSize = buildView ? buildSize : Mathf.Lerp(idleSize, movingSize, moving);
        float desiredSize = Mathf.Clamp(baseSize + zoomOffset, zoomLimits.x, zoomLimits.y);
        currentPitch = Mathf.SmoothDamp(currentPitch, buildView ? buildPitch : pitch, ref pitchVel, 0.6f);
        size = Mathf.SmoothDamp(size, desiredSize, ref sizeVel, zoomSmoothTime);

        Place();
    }

    void Place()
    {
        Quaternion rot = Quaternion.Euler(currentPitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = focus - rot * Vector3.forward * distance;
        if (cam != null && cam.orthographic) cam.orthographicSize = size;
    }
}
