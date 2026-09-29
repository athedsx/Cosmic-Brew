using UnityEngine;
using UnityEngine.InputSystem;

// Drives R0-B0's zen movement: direct velocity with smooth acceleration (no "ice skating"),
// frictionless walls (slides instead of sticking) and physics-driven rotation.
[RequireComponent(typeof(Rigidbody))]
public class RobotMovement : MonoBehaviour
{
    [Header("Vibe settings")]
    [Tooltip("Top speed (slow and relaxing)")]
    public float moveSpeed = 3.2f;

    [Tooltip("Acceleration when starting to walk (m/s²)")]
    public float acceleration = 14f;

    [Tooltip("Deceleration when the keys are released (m/s²)")]
    public float deceleration = 18f;

    [Tooltip("Turn speed toward the movement direction")]
    public float turnSpeed = 10f;

    [Tooltip("Simulated input (for automated tests); used when no key is pressed")]
    public Vector2 simulatedInput;

    [Tooltip("World-space direction (for automated tests); ignores the camera when non-zero")]
    public Vector3 simulatedWorldDirection;

    [Tooltip("Locked while Ro performs an action (e.g. brewing coffee)")]
    public bool movementLocked;

    private Rigidbody rb;
    private Vector3 inputDirection;
    // While a key is held, the direction stays tied to the view from when walking started,
    // so the camera can rotate without Ro curving or spiraling.
    private bool basisLocked;
    private Vector3 lockedForward, lockedRight;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // No friction: touching a table or wall makes Ro slide along it instead of sticking
        var slippery = new PhysicsMaterial("Ro_SemAtrito")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (var col in GetComponents<Collider>()) col.sharedMaterial = slippery;
    }

    void Update()
    {
        // New Input System (the project no longer uses the legacy Input Manager)
        var kb = Keyboard.current;
        float horizontal = 0f, vertical = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
        }
        if (horizontal == 0f && vertical == 0f) { horizontal = simulatedInput.x; vertical = simulatedInput.y; }
        if (movementLocked) { inputDirection = Vector3.zero; basisLocked = false; return; }

        if (simulatedWorldDirection.sqrMagnitude > 0.0001f)
        {
            inputDirection = Vector3.ClampMagnitude(new Vector3(simulatedWorldDirection.x, 0f, simulatedWorldDirection.z), 1f);
            return;
        }

        // Camera-relative movement (W = "up" on screen)
        bool hasInput = horizontal != 0f || vertical != 0f;
        if (!hasInput) basisLocked = false;
        else if (!basisLocked)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            lockedForward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            lockedRight = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
            basisLocked = true;
        }
        inputDirection = hasInput ? Vector3.ClampMagnitude(lockedRight * horizontal + lockedForward * vertical, 1f) : Vector3.zero;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        Vector3 desired = inputDirection * moveSpeed;
        float rate = desired.sqrMagnitude > 0.01f ? acceleration : deceleration;
        flat = Vector3.MoveTowards(flat, desired, rate * dt);
        rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);

        // Turn smoothly toward the heading
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(inputDirection, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, 1f - Mathf.Exp(-turnSpeed * dt)));
        }
    }
}
