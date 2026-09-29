using UnityEngine;

// Balanço suave de flutuação (quiosque, planetas, naves ao fundo)
public class FloatingBob : MonoBehaviour
{
    public float amplitude = 0.15f;
    public float frequency = 0.25f;
    public float rotationSpeed = 0f; // graus por segundo no eixo Y

    private Vector3 startPos;
    private float phase;

    void Start()
    {
        startPos = transform.localPosition;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + phase) * amplitude;
        transform.localPosition = startPos + Vector3.up * y;
        if (rotationSpeed != 0f)
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
    }
}
