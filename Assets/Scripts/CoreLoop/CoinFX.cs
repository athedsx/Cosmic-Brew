using System.Collections.Generic;
using UnityEngine;

// Moedas Estelares 3D saltando do cliente quando ele paga (efeito visual)
public class CoinFX : MonoBehaviour
{
    public static CoinFX Instance { get; private set; }

    [Tooltip("Moeda modelo (fica desativada; é clonada)")]
    public GameObject coinTemplate;
    public float coinSize = 0.32f;
    public float lifetime = 1.5f;
    public float spinSpeed = 540f;

    class Coin { public Transform t; public Vector3 vel; public float born, spin; }
    private readonly List<Coin> coins = new List<Coin>();

    void Awake()
    {
        Instance = this;
        if (coinTemplate != null) coinTemplate.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Solta 'count' moedas a partir de 'origin'
    public void Burst(Vector3 origin, int count)
    {
        if (coinTemplate == null) return;
        count = Mathf.Clamp(count, 1, 8);
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(coinTemplate, origin, Quaternion.Euler(0f, Random.value * 360f, 0f), transform);
            go.SetActive(true);
            go.transform.localScale = Vector3.zero;
            float a = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            var vel = new Vector3(Mathf.Cos(a) * Random.Range(0.6f, 1.2f), Random.Range(3.2f, 4.2f), Mathf.Sin(a) * Random.Range(0.6f, 1.2f));
            coins.Add(new Coin { t = go.transform, vel = vel, born = Time.time + i * 0.05f, spin = spinSpeed * Random.Range(0.8f, 1.2f) });
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            var c = coins[i];
            float age = Time.time - c.born;
            if (age < 0f) continue;
            float u = age / lifetime;
            if (u >= 1f || c.t == null)
            {
                if (c.t != null) Destroy(c.t.gameObject);
                coins.RemoveAt(i);
                continue;
            }
            // arco com "gravidade" suave e depois sobe flutuando antes de sumir
            c.vel += Vector3.down * 7f * dt;
            if (u > 0.55f) c.vel = Vector3.Lerp(c.vel, Vector3.up * 1.5f, dt * 6f);
            c.t.position += c.vel * dt;
            c.t.Rotate(0f, c.spin * dt, 0f, Space.World);
            // aparece com "pop" e encolhe no final
            float s = u < 0.15f ? Mathf.SmoothStep(0f, 1.15f, u / 0.15f) : u > 0.75f ? Mathf.SmoothStep(1f, 0f, (u - 0.75f) / 0.25f) : 1f;
            c.t.localScale = Vector3.one * coinSize * s;
        }
    }
}
