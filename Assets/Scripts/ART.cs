using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Dibujos hechos con código (sin imágenes externas): astronauta, carita, batería, escudo, etc.
// Cada dibujo es una lista de figuras: c = círculo, e = elipse, b = caja redondeada, l = línea gruesa, p = polígono convexo.
// Un guion al principio ("-c ...") RESTA la figura. Cada dibujo tiene un "cuerpo" (silueta) y un "detalle" (líneas oscuras encima).
public static class Art
{
    class Def { public int w, h; public string body, detail; }
    static readonly Dictionary<string, Def> defs = new Dictionary<string, Def>();
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static void Add(string name, int w, int h, string body, string detail)
    {
        defs[name] = new Def { w = w, h = h, body = body, detail = detail };
    }

    static Art()
    {
        Add("astro", 112, 144,
            "b 56 88 40 28 10;c 56 110 28;b 56 72 26 28 10;l 30 90 17 58 8;l 82 90 95 58 8;l 44 48 42 16 10;l 68 48 70 16 10;b 42 10 13 7 3;b 70 10 13 7 3",
            "e 56 110 19 15 0;b 56 74 13 9 3;l 33 52 79 52 2.2;l 56 66 56 52 1.5");
        Add("brain", 128, 112,
            "c 44 62 30;c 84 62 30;c 64 78 30;c 50 42 24;c 78 42 24;l 64 30 68 10 8",
            "l 64 100 64 38 2;l 30 74 50 82 2;l 26 54 46 60 2;l 98 74 78 82 2;l 102 54 82 60 2;l 44 38 54 48 2;l 84 38 74 48 2;l 40 90 52 94 2;l 88 90 76 94 2");
        Add("battery", 112, 128,
            "b 56 58 36 48 9;b 56 112 14 9 3",
            "p 66 100 36 54 56 54;p 54 66 76 66 44 16");
        // Oxígeno: tres burbujas (una grande y dos pequeñas).
        Add("air", 128, 128,
            "c 44 48 38;c 98 82 22;c 60 110 13",
            "l 22 60 28 72 3;l 88 88 92 95 2.5;c 56 114 3");
        // Polvo: rayas de viento con granitos.
        Add("dust", 128, 128,
            "l 12 98 80 98 8;l 28 64 100 64 8;l 12 30 62 30 8;c 104 98 8;c 118 64 8;c 84 30 8;c 106 30 5",
            "");
        Add("food", 128, 128,
            "c 46 56 34;c 82 56 34;b 64 40 30 26 12;l 64 92 70 118 4;e 90 108 18 8 0",
            "l 28 60 32 78 4;l 64 94 64 78 3");
        Add("shield", 112, 128,
            "p 10 104 56 120 102 104 102 62 56 6 10 62",
            "b 56 66 6 30 2;b 56 74 24 6 2");
        Add("house", 128, 112,
            "p 6 70 64 112 122 70;b 64 40 46 30 5",
            "b 64 28 11 20 3;b 36 48 9 9 2;b 92 48 9 9 2");
        Add("panel", 128, 128,
            "p 24 108 124 108 108 38 8 38;l 54 38 54 10 5;b 54 8 26 5 2",
            "l 57 108 41 38 1.8;l 90 108 75 38 1.8;l 16 73 116 73 1.8");
        Add("moon", 112, 112,
            "c 54 56 44;-c 76 66 38",
            "");
        Add("flask", 112, 128,
            "p 14 14 98 14 70 76 42 76;b 56 92 11 18 3;b 56 110 17 5 2",
            "l 30 36 82 36 2.5;c 50 26 4;c 68 22 3");
        Add("wrench", 128, 128,
            "l 24 24 78 78 10;c 92 92 26;-c 106 106 13",
            "c 34 34 4");
        Add("dish", 128, 128,
            "c 60 78 44;-b 60 122 70 46 0;l 60 78 60 108 2.6;c 60 112 6;l 60 38 60 14 5;b 60 10 26 5 2",
            "");
        Add("flame", 112, 128,
            "c 56 44 34;p 24 60 56 124 88 60",
            "c 56 36 16;p 44 38 56 74 68 38");
        Add("star4", 64, 64,
            "p 32 62 40 32 32 2 24 32;p 2 32 32 24 62 32 32 40",
            "");
        Add("bolt", 64, 64,
            "p 40 62 14 30 30 30;p 24 36 50 36 22 2",
            "");
        Add("circle", 64, 64,
            "c 32 32 29",
            "");
        Add("ring", 128, 128,
            "c 64 64 60;-c 64 64 50",
            "");
        // Regalo (bono de la noche)
        Add("gift", 112, 128,
            "b 56 46 42 34 5;b 56 92 50 16 5;c 36 114 14;c 76 114 14",
            "b 56 50 6 44 1;c 56 108 5");
        // Corazón (ánimo y noticias de casa)
        Add("heart", 128, 112,
            "c 40 74 30;c 88 74 30;p 12 64 116 64 64 6",
            "");
        // Cruz (mala noticia, error)
        Add("cross", 112, 112,
            "l 22 22 90 90 12;l 22 90 90 22 12",
            "");
        // Copo de nieve (congelado)
        Add("snow", 112, 112,
            "l 56 6 56 106 9;l 13 31 99 81 9;l 13 81 99 31 9;c 56 56 13;l 40 14 56 28 6;l 72 14 56 28 6;l 40 98 56 84 6;l 72 98 56 84 6",
            "");
        // Visto bueno (conectado)
        Add("check", 112, 112,
            "l 16 58 44 28 12;l 44 28 98 90 12",
            "");
        // Tierra: el océano es un círculo y esto son los continentes (se pintan encima en verde)
        Add("land", 128, 128,
            "e 44 80 22 15 0;e 82 56 22 19 0;e 58 36 14 10 0;c 96 92 10;e 30 56 10 8 0",
            "");
        // Carita del ánimo: feliz (verde), normal (amarilla) o triste (roja). Mismo cuerpo, distinta cara.
        const string eyes = "c 38 70 7;c 74 70 7";
        Add("face_happy", 112, 112, "c 56 56 52", eyes + ";l 30 44 38 33 4;l 38 33 56 27 4;l 56 27 74 33 4;l 74 33 82 44 4");
        Add("face_mid", 112, 112, "c 56 56 52", eyes + ";l 36 34 76 34 4");
        Add("face_sad", 112, 112, "c 56 56 52", eyes + ";l 30 26 38 36 4;l 38 36 56 42 4;l 56 42 74 36 4;l 74 36 82 26 4");
    }

    // Silueta del dibujo (blanca, para teñirla con Image.color o llenarla con Image.fillAmount).
    public static Sprite Body(string name) { return Get(name, false); }

    // Detalles del dibujo (para ponerlos encima, normalmente en oscuro).
    public static Sprite Detail(string name) { return Get(name, true); }

    // Proporción ancho / alto de un dibujo.
    public static float Aspect(string name)
    {
        Def d;
        return defs.TryGetValue(name, out d) ? (float)d.w / d.h : 1f;
    }

    static Sprite Get(string name, bool detail)
    {
        string key = name + (detail ? "#d" : "#b");
        Sprite sp;
        if (cache.TryGetValue(key, out sp)) return sp;
        Def d;
        if (!defs.TryGetValue(name, out d)) return null;
        string dsl = detail ? d.detail : d.body;
        if (string.IsNullOrEmpty(dsl)) { cache[key] = null; return null; }
        sp = MakeSprite(Raster(d.w, d.h, dsl), d.w, d.h);
        cache[key] = sp;
        return sp;
    }

    // Luz suave redonda (para brillos).
    public static Sprite Glow()
    {
        Sprite sp;
        if (cache.TryGetValue("#glow", out sp)) return sp;
        int n = 64;
        var a = new float[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float k = Mathf.Clamp01(1f - r);
                a[y * n + x] = k * k;
            }
        sp = MakeSprite(a, n, n);
        cache["#glow"] = sp;
        return sp;
    }

    static Sprite MakeSprite(float[] alpha, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---------------------------------------------------------------- rasterizador (distancia a cada figura)
    class Shape
    {
        public char kind;
        public bool sub;
        public float[] a;
        public float[] nx, ny;      // polígono: normales hacia afuera
    }

    static float[] Raster(int w, int h, string dsl)
    {
        var shapes = new List<Shape>();
        foreach (var tok in dsl.Split(';'))
        {
            string t = tok.Trim();
            if (t.Length == 0) continue;
            var parts = t.Split(' ');
            var s = new Shape();
            string k = parts[0];
            if (k.StartsWith("-")) { s.sub = true; k = k.Substring(1); }
            s.kind = k[0];
            s.a = new float[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++)
                s.a[i - 1] = float.Parse(parts[i], CultureInfo.InvariantCulture);
            if (s.kind == 'p') PrepPolygon(s);
            shapes.Add(s);
        }
        var res = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = 0f;
                for (int i = 0; i < shapes.Count; i++)
                {
                    float di = Dist(shapes[i], px, py);
                    if (i == 0) d = di;
                    else if (shapes[i].sub) d = Mathf.Max(d, -di);
                    else d = Mathf.Min(d, di);
                }
                res[y * w + x] = Mathf.Clamp01(0.5f - d);
            }
        return res;
    }

    static void PrepPolygon(Shape s)
    {
        int n = s.a.Length / 2;
        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += s.a[2 * i] * s.a[2 * j + 1] - s.a[2 * j] * s.a[2 * i + 1];
        }
        if (area < 0f)
        {
            var r = new float[s.a.Length];
            for (int i = 0; i < n; i++) { r[2 * i] = s.a[2 * (n - 1 - i)]; r[2 * i + 1] = s.a[2 * (n - 1 - i) + 1]; }
            s.a = r;
        }
        s.nx = new float[n]; s.ny = new float[n];
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            float ex = s.a[2 * j] - s.a[2 * i], ey = s.a[2 * j + 1] - s.a[2 * i + 1];
            float len = Mathf.Sqrt(ex * ex + ey * ey);
            s.nx[i] = ey / len; s.ny[i] = -ex / len;
        }
    }

    static float Dist(Shape s, float px, float py)
    {
        float[] a = s.a;
        switch (s.kind)
        {
            case 'c':
            {
                float dx = px - a[0], dy = py - a[1];
                return Mathf.Sqrt(dx * dx + dy * dy) - a[2];
            }
            case 'e':
            {
                float dx = (px - a[0]) / a[2], dy = (py - a[1]) / a[3];
                float k = Mathf.Sqrt(dx * dx + dy * dy);
                return (k - 1f) * Mathf.Min(a[2], a[3]);
            }
            case 'b':
            {
                float qx = Mathf.Abs(px - a[0]) - a[2] + a[4], qy = Mathf.Abs(py - a[1]) - a[3] + a[4];
                float mx = Mathf.Max(qx, 0f), my = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, qy), 0f) - a[4];
            }
            case 'l':
            {
                float bax = a[2] - a[0], bay = a[3] - a[1];
                float pax = px - a[0], pay = py - a[1];
                float hh = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
                float ox = pax - bax * hh, oy = pay - bay * hh;
                return Mathf.Sqrt(ox * ox + oy * oy) - a[4];
            }
            case 'p':
            {
                float d = -1e9f;
                int n = s.nx.Length;
                for (int i = 0; i < n; i++)
                {
                    float v = (px - a[2 * i]) * s.nx[i] + (py - a[2 * i + 1]) * s.ny[i];
                    if (v > d) d = v;
                }
                return d;
            }
        }
        return 1e9f;
    }
}