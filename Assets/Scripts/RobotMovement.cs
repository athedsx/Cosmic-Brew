using UnityEngine;
using UnityEngine.InputSystem;

// Controla o movimento zen do R0-B0: velocidade direta com aceleração suave (sem "patinar"),
// sem atrito nas paredes (desliza em vez de travar) e rotação feita pela física.
[RequireComponent(typeof(Rigidbody))]
public class RobotMovement : MonoBehaviour
{
    [Header("Configurações de Vibe")]
    [Tooltip("Velocidade máxima (lenta e relaxante)")]
    public float moveSpeed = 3.2f;

    [Tooltip("Aceleração ao começar a andar (m/s²)")]
    public float acceleration = 14f;

    [Tooltip("Desaceleração ao soltar as teclas (m/s²)")]
    public float deceleration = 18f;

    [Tooltip("Velocidade de giro para a direção do movimento")]
    public float turnSpeed = 10f;

    [Tooltip("Entrada simulada (para testes automáticos); usada quando nenhuma tecla está pressionada")]
    public Vector2 simulatedInput;

    [Tooltip("Direção no mundo (para testes automáticos); ignora a câmera quando não é zero")]
    public Vector3 simulatedWorldDirection;

    [Tooltip("Travado enquanto o Ro está fazendo uma ação (ex.: tirando café)")]
    public bool movementLocked;

    private Rigidbody rb;
    private Vector3 inputDirection;
    // Enquanto uma tecla está segurada, a direção fica presa à visão de quando começou a andar;
    // assim a câmera pode girar sem o Ro sair fazendo curva/espiral.
    private bool basisLocked;
    private Vector3 lockedForward, lockedRight;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Sem atrito: encostar numa mesa/parede faz o Ro deslizar ao longo dela em vez de grudar
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
        // Input System novo (o projeto não usa mais o Input Manager antigo)
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

        // Movimento relativo à câmera (W = "para cima" na tela)
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

        // Vira suavemente para onde está indo
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(inputDirection, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, 1f - Mathf.Exp(-turnSpeed * dt)));
        }
    }
}
