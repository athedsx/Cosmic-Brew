using UnityEngine;

// Language flags drawn in code (rounded corners):
// Brazil, USA, Russia, South Korea, China and Japan.
public static class FlagSprites
{
    const int W = 120, H = 80, SS = 3; // supersampling for smooth edges
    static readonly Sprite[] cache = new Sprite[6];

    public static Sprite For(Lang lang)
    {
        int i = (int)lang;
        if (cache[i] == null) cache[i] = Make(lang);
        return cache[i];
    }

    delegate Color Painter(float x, float y); // x, y in 0..1 (y up)

    static Sprite Make(Lang lang)
    {
        Painter p = lang switch
        {
            Lang.PT => Brazil,
            Lang.EN => USA,
            Lang.RU => Russia,
            Lang.KO => Korea,
            Lang.ZH => China,
            _ => Japan
        };
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        float radius = 10f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color acc = Color.clear;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float fx = (x + (sx + 0.5f) / SS) / W, fy = (y + (sy + 0.5f) / SS) / H;
                        acc += p(fx, fy);
                    }
                acc /= SS * SS;
                // rounded corners
                float cx = Mathf.Clamp(x + 0.5f, radius, W - radius), cy = Mathf.Clamp(y + 0.5f, radius, H - radius);
                float a = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) + 0.5f);
                acc.a = a;
                px[y * W + x] = acc;
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    static readonly float Aspect = (float)W / H;

    static bool InStar(float x, float y, float cx, float cy, float r, float rot)
    {
        // 5-pointed star (coordinates corrected for the flag's aspect ratio)
        float dx = (x - cx) * Aspect, dy = y - cy;
        float ang = Mathf.Atan2(dy, dx) - rot;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float k = Mathf.Repeat(ang, Mathf.PI * 2f / 5f) - Mathf.PI / 5f; // -36°..36°
        float inner = r * 0.382f;
        // edge radius in direction k (interpolating between tip and notch)
        float t = Mathf.Abs(k) / (Mathf.PI / 5f);
        float edge = Mathf.Lerp(r, inner, Mathf.Pow(t, 0.9f));
        return dist < edge;
    }

    static Color Brazil(float x, float y)
    {
        float dx = (x - 0.5f) * Aspect, dy = y - 0.5f;
        if (Mathf.Sqrt(dx * dx + dy * dy) < 0.24f)
        {
            // slightly curved white band across the blue circle
            float band = dy - (-0.03f + 0.12f * dx * dx * -2f + dx * 0.1f);
            return Mathf.Abs(band) < 0.03f ? Color.white : Hex("#002776");
        }
        if (Mathf.Abs(x - 0.5f) / 0.42f + Mathf.Abs(y - 0.5f) / 0.38f < 1f) return Hex("#FEDD00");
        return Hex("#009C3B");
    }

    static Color USA(float x, float y)
    {
        if (x < 0.42f && y > 1f - 0.54f)
        {
            // blue canton with a grid of little stars
            float gx = x / 0.42f * 6f, gy = (y - 0.46f) / 0.54f * 5f;
            bool odd = ((int)gy) % 2 == 1;
            float fx = Mathf.Repeat(gx + (odd ? 0.5f : 0f), 1f) - 0.5f, fy = Mathf.Repeat(gy, 1f) - 0.5f;
            return fx * fx + fy * fy < 0.07f ? Color.white : Hex("#3C3B6E");
        }
        int stripe = (int)((1f - y) * 13f);
        return stripe % 2 == 0 ? Hex("#B22234") : Color.white;
    }

    static Color Russia(float x, float y) =>
        y > 2f / 3f ? Color.white : y > 1f / 3f ? Hex("#0039A6") : Hex("#D52B1E");

    static Color Korea(float x, float y)
    {
        float cx = 0.5f, cy = 0.5f, r = 0.25f;
        float dx = (x - cx) * Aspect, dy = y - cy;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d < r)
        {
            // taegeuk: red half on top, blue below, with the "drops"
            float ang = -0.6f; // slight tilt
            float rx = dx * Mathf.Cos(ang) - dy * Mathf.Sin(ang), ry = dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang);
            bool top = ry > 0f;
            float lx = rx + r / 2f, rxx = rx - r / 2f;
            if (Mathf.Sqrt(lx * lx + ry * ry) < r / 2f) top = true;
            if (Mathf.Sqrt(rxx * rxx + ry * ry) < r / 2f) top = false;
            return top ? Hex("#CD2E3A") : Hex("#0047A0");
        }
        // simplified trigrams in the 4 corners, on the diagonal
        Vector2[] centers = { new Vector2(0.2f, 0.78f), new Vector2(0.8f, 0.22f), new Vector2(0.8f, 0.78f), new Vector2(0.2f, 0.22f) };
        foreach (var c in centers)
        {
            float ax = (x - c.x) * Aspect, ay = y - c.y;
            float diag = (c.x < 0.5f) == (c.y > 0.5f) ? 1f : -1f;
            float ang = diag * Mathf.PI / 4f + Mathf.PI / 2f;
            float u = ax * Mathf.Cos(ang) + ay * Mathf.Sin(ang);
            float v = -ax * Mathf.Sin(ang) + ay * Mathf.Cos(ang);
            if (Mathf.Abs(v) < 0.075f && Mathf.Abs(u) < 0.085f)
            {
                float bar = Mathf.Repeat((u + 0.085f) / 0.17f * 3f, 1f);
                if (bar > 0.28f) return Color.black;
            }
        }
        return Color.white;
    }

    static Color China(float x, float y)
    {
        if (InStar(x, y, 0.17f, 0.72f, 0.14f, Mathf.PI / 2f)) return Hex("#FFDE00");
        Vector2[] small = { new Vector2(0.33f, 0.9f), new Vector2(0.4f, 0.8f), new Vector2(0.4f, 0.66f), new Vector2(0.33f, 0.56f) };
        foreach (var s in small) if (InStar(x, y, s.x, s.y, 0.05f, 0.3f)) return Hex("#FFDE00");
        return Hex("#DE2910");
    }

    static Color Japan(float x, float y)
    {
        float dx = (x - 0.5f) * Aspect, dy = y - 0.5f;
        return dx * dx + dy * dy < 0.3f * 0.3f ? Hex("#BC002D") : Color.white;
    }
}
