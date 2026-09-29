using System.Collections.Generic;
using UnityEngine;

// Partículas simples e leves (quads virados para a câmera): vapor, brilhos, nuvenzinhas.
// Criado sob demanda; não precisa estar na cena.
public class SimpleParticles : MonoBehaviour
{
    public enum Shape { Soft, Star, Circle }

    static SimpleParticles instance;
    public static SimpleParticles Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Particulas").AddComponent<SimpleParticles>();
            return instance;
        }
    }

    class P
    {
        public Transform t;
        public Renderer r;
        public Vector3 vel;
        public float born, life, size, grow, gravity, drag, spin, angle;
        public Color color;
    }

    readonly List<P> live = new List<P>();
    readonly Stack<P> pool = new Stack<P>();
    readonly Dictionary<Shape, Material> mats = new Dictionary<Shape, Material>();
    MaterialPropertyBlock block;
    Mesh quad;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    Material MatFor(Shape s)
    {
        if (!mats.TryGetValue(s, out var m))
        {
            var tex = s == Shape.Star ? UISprites.Star.texture : s == Shape.Circle ? UISprites.Circle.texture : UISprites.Soft.texture;
            m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            mats[s] = m;
        }
        return m;
    }

    // Emite 'count' partículas
    public void Emit(Vector3 pos, int count, Shape shape, Color color, float size, float life,
                     Vector3 baseVelocity, float spread, float gravity = 0f, float grow = 0f, float drag = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            var p = pool.Count > 0 ? pool.Pop() : Create();
            p.t.gameObject.SetActive(true);
            p.t.position = pos + Random.insideUnitSphere * 0.05f;
            p.r.sharedMaterial = MatFor(shape);
            p.vel = baseVelocity + Random.insideUnitSphere * spread;
            p.born = Time.time;
            p.life = life * Random.Range(0.8f, 1.2f);
            p.size = size * Random.Range(0.8f, 1.2f);
            p.grow = grow;
            p.gravity = gravity;
            p.drag = drag;
            p.spin = Random.Range(-90f, 90f);
            p.angle = Random.value * 360f;
            p.color = color;
            live.Add(p);
        }
    }

    P Create()
    {
        var go = new GameObject("p", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = quad;
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return new P { t = go.transform, r = r };
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Quaternion face = cam.transform.rotation;
        float dt = Time.deltaTime;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            float u = (Time.time - p.born) / p.life;
            if (u >= 1f)
            {
                p.t.gameObject.SetActive(false);
                pool.Push(p);
                live.RemoveAt(i);
                continue;
            }
            p.vel += Vector3.down * p.gravity * dt;
            p.vel *= 1f - Mathf.Clamp01(p.drag * dt);
            p.t.position += p.vel * dt;
            p.angle += p.spin * dt;
            p.t.rotation = face * Quaternion.Euler(0f, 0f, p.angle);
            float s = p.size * (1f + p.grow * u) * (u < 0.12f ? u / 0.12f : 1f);
            p.t.localScale = new Vector3(s, s, s);
            var c = p.color;
            c.a *= u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f;
            block.SetColor("_Color", c);
            p.r.SetPropertyBlock(block);
        }
    }
}
