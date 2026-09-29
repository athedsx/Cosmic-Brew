using UnityEngine;
using UnityEngine.InputSystem;

// Câmera isométrica aconchegante:
// - segue o Ro com Smooth Damp e olha um pouco à frente para onde ele anda;
// - orbita em volta do quiosque conforme o Ro caminha, ficando sempre do lado dele
//   (assim o prédio nunca tampa o personagem);
// - abre um pouco o zoom enquanto ele anda e fecha quando para;
// - Z/C giram a câmera e a rodinha do mouse dá zoom.
public class IsometricCameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Seguir")]
    [Tooltip("Tempo para a câmera alcançar o alvo (vibe lenta/suave)")]
    public float smoothTime = 0.35f;
    [Tooltip("Quanto olhar à frente do movimento (segundos de velocidade)")]
    public float lookAhead = 0.45f;
    public float focusHeight = 0.8f;
    public float distance = 30f;
    [Range(15f, 60f)] public float pitch = 32f;

    [Header("Órbita em volta do quiosque")]
    public bool orbitWithPlayer = true;
    [Tooltip("Centro da órbita (o prédio do quiosque)")]
    public Vector3 orbitCenter = new Vector3(0f, 0f, 1.2f);
    [Tooltip("Ângulo extra para manter a visão diagonal (isométrica) em vez de frontal")]
    public float yawBias = 25f;
    [Tooltip("Tempo para a câmera girar até a nova visão")]
    public float yawSmoothTime = 1.1f;
    [Tooltip("Abaixo dessa distância do centro a câmera não gira (evita tremer)")]
    public float minOrbitRadius = 1.2f;

    [Header("Zoom")]
    public float idleSize = 4.6f;
    public float movingSize = 5.4f;
    public float zoomSmoothTime = 0.9f;
    public Vector2 zoomLimits = new Vector2(3f, 9f);

    [Header("Modo Construção")]
    [Tooltip("Visão de cima da ilha inteira (ligada pelo Modo Construção)")]
    public bool buildView;
    public Vector3 buildFocus = new Vector3(0f, 0f, -0.3f);
    [Tooltip("Desloca a ilha para cima na tela, abrindo espaço para o catálogo")]
    public float buildFocusDrop = 2.6f;
    public float buildSize = 8.8f;
    public float buildPitch = 52f;

    [Header("Menu inicial")]
    [Tooltip("Câmera girando devagar em volta da ilha (tela inicial)")]
    public bool menuView;
    public float menuSpinSpeed = 5f;
    public float menuSize = 7.6f;
    public float menuPitch = 26f;
    [Tooltip("Desloca a ilha para a direita da tela (o menu fica à esquerda)")]
    public float menuSideShift = 4.2f;

    [Header("Controles manuais")]
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
        // câmera do mesmo lado que o Ro, olhando em direção ao quiosque
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

        // controles manuais
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

        // foco com olhar à frente
        Vector3 vel = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        vel.y = 0f;
        Vector3 flatForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 desiredFocus = buildView ? buildFocus - flatForward * buildFocusDrop : target.position + Vector3.up * focusHeight + vel * lookAhead;
        focus = Vector3.SmoothDamp(focus, desiredFocus, ref focusVel, smoothTime);

        // giro da câmera acompanhando o caminho do Ro
        if (orbitWithPlayer && !buildView) orbitYaw = DesiredYaw();
        float desiredYaw = orbitYaw + manualYaw;
        yaw = Mathf.SmoothDampAngle(yaw, desiredYaw, ref yawVel, yawSmoothTime);

        // zoom: abre andando, fecha parado
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
