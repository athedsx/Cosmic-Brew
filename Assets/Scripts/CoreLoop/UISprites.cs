using UnityEngine;

// Simple sprites generated at runtime (rounded rect, circle, star),
// so the UI does not depend on image files.
public static class UISprites
{
    static Sprite rounded, circle, star;

    public static Sprite Rounded { get { if (rounded == null) rounded = MakeRounded(64, 24f, true); return rounded; } }
    public static Sprite Circle { get { if (circle == null) circle = MakeRounded(64, 32f, false); return circle; } }
    public static Sprite Star { get { if (star == null) star = MakeStar(128); return star; } }
    public static Sprite Soft { get { if (soft == null) soft = MakeSoft(64); return soft; } }

    static Sprite soft;

    // circle with a very soft edge (steam, little clouds)
    static Sprite MakeSoft(int size)
    {
        var tex = NewTexture(size);
        var px = new Color32[size * size];
        float c = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D NewTexture(int size) =>
        new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    static Sprite MakeRounded(int size, float radius, bool sliced)
    {
        var tex = NewTexture(size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 c = new Vector2(Mathf.Clamp(p.x, radius, size - radius), Mathf.Clamp(p.y, radius, size - radius));
                float a = Mathf.Clamp01(radius - Vector2.Distance(p, c) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        float b = sliced ? radius + 1f : 0f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    static Sprite MakeStar(int size)
    {
        // 5-pointed star with slightly chubby tips
        var pts = new Vector2[10];
        float cx = size / 2f, cy = size / 2f, outer = size * 0.48f, inner = size * 0.23f;
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float r = i % 2 == 0 ? outer : inner;
            pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }
        var tex = NewTexture(size);
        var px = new Color32[size * size];
        const int ss = 4; // supersampling for a smooth edge
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (Inside(pts, new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss))) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (ss * ss)));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }
}
