using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Informe de la misión: casi todo en dibujos. Medidores que se llenan, números grandes, barras de colores y la gráfica de la misión.
public class ReviewScreen
{
    static readonly string[] ResLabels = { "BATERÍA", "AIRE", "COMIDA", "CASA", "ESCUDO", "SALUD", "ÁNIMO", "PANELES" };
    static readonly string[] ResIcons = { "battery", "air", "food", "house", "shield", "astro", "face", "panel" };
    static readonly string[] PackIcons = { "ring", "shield", "bolt" };
    static readonly Color[] PackCols = { new Color(0.40f, 1f, 0.50f, 1f), new Color(0.40f, 0.75f, 1f, 1f), new Color(1f, 0.85f, 0.25f, 1f) };
    static readonly Color[] PlanetCols = { new Color(0.86f, 0.40f, 0.22f, 1f), new Color(0.74f, 0.76f, 0.80f, 1f) };
    static readonly Color CardBg = new Color(0.07f, 0.09f, 0.08f, 1f);
    static readonly Color Soft = new Color(0.6f, 0.75f, 0.65f, 1f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.25f, 1f);

    readonly RectTransform root;
    FxLayer fxl;

    public ReviewScreen(RectTransform parent, Action retry)
    {
        root = UIKit.Root(parent, "ReviewScreen");
        UIKit.Panel(root, "Bg", 0f, 0f, 1f, 1f, new Color(0.02f, 0.03f, 0.04f, 0.96f));
        GameAudio.Play(Sfx.ReviewOpen);

        float score = Campaign.Score();
        string grade = Campaign.Grade(score);
        Transform badge = BuildHeader(score, grade);
        BuildGauges();
        BuildStats();
        BuildDecisions();
        BuildStorms();
        BuildLessons();
        BuildChart();

        var db = UIKit.MakeButton(root, "DATOS NASA", 16, 0.02f, 0.02f, 0.26f, 0.105f, new Color(0.05f, 0.14f, 0.34f, 1f), ShowDatos, TextAnchor.MiddleCenter, null);
        ShiftText(db, 36f);
        Gfx.IconLeft(db.transform, "dish", 32f, 10f, UIKit.RadioEdge);
        var rb = UIKit.MakeButton(root, "JUGAR OTRA VEZ", 22, 0.30f, 0.02f, 0.70f, 0.105f, UIKit.Selected, retry, TextAnchor.MiddleCenter, Sfx.BtnRedesign);
        ShiftText(rb, 44f);
        UIKit.Border((RectTransform)rb.transform, UIKit.Phosphor, 3f);
        var fl = Gfx.IconLeft(rb.transform, "flame", 40f, 12f, UIKit.Amber);
        var fp = fl.gameObject.AddComponent<Pulser>();
        fp.onColor = UIKit.Amber; fp.amount = 0.18f; fp.speed = 6f;
        UIKit.Label(root, "Datos reales: NASA DONKI, Curiosity RAD y OSDR", 11, TextAnchor.MiddleRight, new Color(0.5f, 0.75f, 1f, 0.9f), 0.72f, 0.02f, 0.98f, 0.105f);

        // ---- efectos: estrellitas y, si ganaste, confeti
        fxl = FxLayer.Create(root);
        fxl.SetAmbient(Campaign.survived ? Ambient.Calm : Ambient.Warn);
        if (Campaign.survived)
        {
            FxLayer.Burst(badge, Gold, 26, 340f, 14f, "mix");
            FxLayer.Burst(badge, new Color(0.45f, 1f, 0.55f, 1f), 18, 260f, 12f, "star4");
            FxLayer.Ring(badge, Gold, 320f, 1.2f);
        }
        else FxLayer.Ring(badge, Gfx.Bad, 280f, 1.2f);
    }

    static void ShiftText(Button b, float left)
    {
        var t = b.GetComponentInChildren<Text>();
        if (t == null) return;
        var r = t.rectTransform;
        r.offsetMin = new Vector2(left, r.offsetMin.y);
    }

    void ShowDatos()
    {
        var m = UIKit.Panel(root, "Datos", 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.75f));
        NasaPanel.Fill(m, delegate { UnityEngine.Object.Destroy(m.gameObject); });
    }

    static string CauseShort(string cause)
    {
        switch (cause)
        {
            case "power": return "se acabó la batería";
            case "oxygen": return "se acabó el aire";
            case "health": return "el equipo murió (salud en 0)";
            case "structure": return "la casa se derrumbó";
            case "science": return "faltó la ciencia obligatoria";
        }
        return "fin de la misión";
    }

    // Tarjeta con título (y borde).
    RectTransform Card(float x0, float y0, float x1, float y1, string title)
    {
        var p = UIKit.Panel(root, "Card", x0, y0, x1, y1, CardBg);
        p.GetComponent<Image>().raycastTarget = false;
        UIKit.Border(p, new Color(0.22f, 0.30f, 0.25f, 1f), 2f);
        UIKit.Label(p, title, 13, TextAnchor.MiddleLeft, UIKit.Amber, 0.02f, 0.84f, 0.98f, 1f);
        return p;
    }

    static RectTransform Zone(Transform parent, float x0, float y0, float x1, float y1, Color c)
    {
        var rt = UIKit.Panel(parent, "Z", x0, y0, x1, y1, c);
        rt.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    // ---------------------------------------------------------------- arriba: resultado, nota y puntos
    Transform BuildHeader(float score, string grade)
    {
        bool win = Campaign.survived;
        float mood = win ? Mathf.Clamp01(Campaign.StateAverage() / 100f) : 0.1f;
        var face = Gfx.Face(root, mood, 0.015f, 0.885f, 0.075f, 0.995f);
        var fpul = face.gameObject.AddComponent<Pulser>();
        fpul.onColor = face.color; fpul.amount = 0.05f; fpul.speed = 2.5f;

        bool full = win && Campaign.goalMet;
        UIKit.Label(root, full ? "¡MISIÓN CUMPLIDA!" : (win ? "¡SOBREVIVIERON!" : "MISIÓN PERDIDA"), 32, TextAnchor.MiddleLeft,
                    full ? new Color(0.45f, 1f, 0.55f, 1f) : (win ? UIKit.Amber : UIKit.Red), 0.085f, 0.935f, 0.55f, 0.995f);
        string sub = full
            ? "4 astronautas  |  " + Campaign.totalDays + " noches  |  " + Session.Site.name
            : (win ? "Faltó ciencia: mandaste " + Campaign.experiments + " de " + Campaign.goalPacks + " paquetes a la Tierra"
                   : (Campaign.endCause == "science" ? "Sobrevivieron, pero mandaste " + Campaign.experiments + " de " + Campaign.goalPacks + " paquetes"
                      : "Noche " + (Campaign.daysSurvived + 1) + " de " + Campaign.totalDays + ": " + CauseShort(Campaign.endCause)));
        UIKit.Label(root, sub, 15, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.085f, 0.885f, 0.55f, 0.935f);

        // nota: círculo de color con la letra, y estrellas
        Color gc = grade == "S" ? Gold : grade == "A" ? new Color(0.45f, 1f, 0.55f, 1f)
                 : grade == "B" ? new Color(0.70f, 0.95f, 0.45f, 1f) : grade == "C" ? UIKit.Amber : UIKit.Red;
        var badge = Gfx.Icon(root, "circle", 0.575f, 0.88f, 0.64f, 0.995f, gc);
        var bp = badge.gameObject.AddComponent<Pulser>();
        bp.onColor = gc; bp.amount = 0.05f; bp.speed = 3f;
        UIKit.Label(badge.transform, grade, 48, TextAnchor.MiddleCenter, new Color(0.04f, 0.06f, 0.05f, 1f), 0f, 0f, 1f, 1f);
        int stars = (grade == "S" || grade == "A") ? 3 : grade == "B" ? 2 : grade == "C" ? 1 : 0;
        for (int k = 0; k < 3; k++)
            Gfx.Dot(root, "star4", 0.665f + k * 0.032f, 0.955f, 30f, k < stars ? Gold : Gfx.Dim);

        UIKit.Label(root, score.ToString("0") + " PTS", 28, TextAnchor.MiddleRight, UIKit.Amber, 0.76f, 0.93f, 0.985f, 0.995f);

        // mejor puntaje guardado (por dificultad y modo)
        string key = "ares_best_d" + Session.difficulty + (Session.tutorial ? "_t" : "_d") + "_s" + Session.site;
        float best = PlayerPrefs.GetFloat(key, 0f);
        bool record = score > best;
        if (record) { PlayerPrefs.SetFloat(key, score); PlayerPrefs.Save(); best = score; }
        UIKit.Label(root, record ? "<color=#ffd940>¡NUEVO RÉCORD!</color>  " + Session.DifficultyName
                                 : "MEJOR: " + best.ToString("0") + "  |  " + Session.DifficultyName,
                    13, TextAnchor.MiddleRight, UIKit.Phosphor, 0.64f, 0.885f, 0.985f, 0.93f);
        return badge.transform;
    }

    // ---------------------------------------------------------------- medidores: así terminó la base
    void BuildGauges()
    {
        int[] order = { 0, 1, 2, 4, 5, 6, 3, 7 };
        var list = new List<int>();
        for (int k = 0; k < order.Length; k++) if (Session.ResShown(order[k])) list.Add(order[k]);
        float w = 0.96f / list.Count;
        for (int k = 0; k < list.Count; k++)
        {
            int i = list[k];
            float x0 = 0.02f + k * w;
            var g = IconGauge.Create(root, ResLabels[i], ResIcons[i], x0 + 0.004f, 0.60f, x0 + w - 0.004f, 0.875f, false);
            g.Init(Campaign.finalV[i] / 100f, true);
        }
    }

    // ---------------------------------------------------------------- números grandes
    void StatTile(string icon, Color ic, string big, string small, float x0, float x1)
    {
        var p = UIKit.Panel(root, "Stat", x0, 0.50f, x1, 0.59f, CardBg);
        p.GetComponent<Image>().raycastTarget = false;
        UIKit.Border(p, new Color(ic.r * 0.6f, ic.g * 0.6f, ic.b * 0.6f, 1f), 2f);
        Gfx.IconLeft(p, icon, 38f, 8f, ic);
        UIKit.Label(p, big, 24, TextAnchor.MiddleLeft, Color.white, 0.30f, 0.36f, 1f, 1f);
        UIKit.Label(p, small, 11, TextAnchor.UpperLeft, Soft, 0.30f, 0f, 1f, 0.40f);
    }

    void BuildStats()
    {
        var pk = Packs.All[Mathf.Clamp(Session.pack, 0, Packs.All.Length - 1)];
        int pi = Mathf.Clamp(Session.pack, 0, PackIcons.Length - 1);
        Color pc = PlanetCols[Mathf.Clamp(Session.site, 0, PlanetCols.Length - 1)];
        int n = 6;
        float gap = 0.008f, w = (0.96f - gap * (n - 1)) / n;
        float x = 0.02f;
        StatTile("moon", new Color(1f, 0.92f, 0.60f, 1f), Campaign.daysSurvived + "/" + Campaign.totalDays, "NOCHES", x, x + w); x += w + gap;
        StatTile("flask", new Color(0.75f, 0.55f, 1f, 1f), Campaign.experiments.ToString(), "CIENCIA", x, x + w); x += w + gap;
        StatTile("bolt", Gold, Campaign.tasksUsed.ToString(), "TAREAS USADAS", x, x + w); x += w + gap;
        StatTile("bolt", new Color(0.6f, 0.62f, 0.6f, 1f), Campaign.tasksWasted.ToString(), "SIN USAR", x, x + w); x += w + gap;
        StatTile("circle", pc, Session.Site.name, "LUGAR", x, x + w); x += w + gap;
        StatTile(PackIcons[pi], PackCols[pi], pk.name, "SUMINISTROS", x, x + w);
    }

    // ---------------------------------------------------------------- lo que más importó (barras: verde = ayudó, rojo = costó)
    void BuildDecisions()
    {
        var c = Card(0.02f, 0.27f, 0.36f, 0.49f, "LO QUE MÁS IMPORTÓ");
        Gfx.Dot(c, "circle", 0.56f, 0.92f, 11f, Gfx.Good);
        UIKit.Label(c, "AYUDÓ", 11, TextAnchor.MiddleLeft, Gfx.Good, 0.585f, 0.84f, 0.76f, 1f);
        Gfx.Dot(c, "circle", 0.79f, 0.92f, 11f, Gfx.Bad);
        UIKit.Label(c, "COSTÓ", 11, TextAnchor.MiddleLeft, Gfx.Bad, 0.815f, 0.84f, 0.98f, 1f);

        var list = Campaign.ledger.Ranked(false);
        if (list.Count == 0)
        {
            Gfx.Face(c, 0.4f, 0.04f, 0.18f, 0.20f, 0.74f);
            UIKit.Label(c, "Ninguna decisión cambió mucho.", 14, TextAnchor.MiddleLeft, Soft, 0.23f, 0.18f, 0.98f, 0.74f);
            return;
        }
        float maxInf = 1f;
        for (int i = 0; i < list.Count && i < 4; i++) maxInf = Mathf.Max(maxInf, list[i].Influence());
        for (int r = 0; r < list.Count && r < 4; r++)
        {
            var e = list[r];
            float y1 = 0.83f - r * 0.205f, y0 = y1 - 0.18f;
            UIKit.Label(c, e.name, 12, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.02f, y0, 0.50f, y1);
            var track = Zone(c, 0.51f, y0 + 0.03f, 0.98f, y1 - 0.03f, new Color(0.12f, 0.15f, 0.13f, 1f));
            float inf = Mathf.Max(0.01f, e.Influence());
            float total = Mathf.Clamp01(inf / maxInf);
            float gl = total * (e.GainPts() / inf), cl = total * (e.CostPts() / inf);
            if (gl > 0.001f) Zone(track, 0f, 0f, gl, 1f, Gfx.Good);
            if (cl > 0.001f) Zone(track, gl, 0f, gl + cl, 1f, Gfx.Bad);
        }
    }

    // ---------------------------------------------------------------- tormentas reales de la NASA y lo que frenó tu escudo
    void BuildStorms()
    {
        var c = Card(0.37f, 0.27f, 0.66f, 0.49f, "TUS TORMENTAS REALES (NASA)");
        if (Campaign.hits.Count == 0)
        {
            Gfx.Icon(c, "flame", 0.04f, 0.18f, 0.18f, 0.74f, Gfx.Dim);
            UIKit.Label(c, "Ninguna llegó antes de que terminara.", 14, TextAnchor.MiddleLeft, Soft, 0.22f, 0.18f, 0.98f, 0.74f);
            return;
        }
        for (int r = 0; r < Campaign.hits.Count && r < 3; r++)
        {
            var h = Campaign.hits[r];
            float y1 = 0.83f - r * 0.23f, y0 = y1 - 0.20f;
            var row = Zone(c, 0.02f, y0, 0.98f, y1, new Color(0f, 0f, 0f, 0f));
            Gfx.IconLeft(row, "flame", 26f, 2f, new Color(1f, 0.55f, 0.25f, 1f));
            string cls = h.flare != null ? h.flare.Cls : "Sol";
            UIKit.Label(row, "N" + h.day + "  " + cls, 12, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.10f, 0f, 0.45f, 1f);
            var track = Zone(row, 0.46f, 0.18f, 1f, 0.82f, new Color(0.12f, 0.15f, 0.13f, 1f));
            float tot = Mathf.Max(0.01f, h.dose + h.avoided);
            float av = h.avoided / tot;
            if (av > 0.001f) Zone(track, 0f, 0f, av, 1f, new Color(0.40f, 0.75f, 1f, 1f));
            if (av < 0.999f) Zone(track, av, 0f, 1f, 1f, Gfx.Bad);
        }
        Gfx.Dot(c, "circle", 0.05f, 0.09f, 10f, new Color(0.40f, 0.75f, 1f, 1f));
        UIKit.Label(c, "ESCUDO FRENÓ", 10, TextAnchor.MiddleLeft, Soft, 0.08f, 0f, 0.50f, 0.18f);
        Gfx.Dot(c, "circle", 0.53f, 0.09f, 10f, Gfx.Bad);
        UIKit.Label(c, "RECIBIDO", 10, TextAnchor.MiddleLeft, Soft, 0.56f, 0f, 0.98f, 0.18f);
    }

    // ---------------------------------------------------------------- lo que aprendiste
    void BuildLessons()
    {
        var c = Card(0.67f, 0.27f, 0.98f, 0.49f, "LO QUE APRENDISTE");
        int max = Session.Diff.kids ? 2 : 3;
        if (Campaign.lessons.Count == 0)
        {
            Gfx.Face(c, 0.9f, 0.04f, 0.18f, 0.20f, 0.74f);
            UIKit.Label(c, "¡Misión limpia! Muy buenas decisiones.", 14, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.23f, 0.18f, 0.98f, 0.74f);
            return;
        }
        for (int i = 0; i < Campaign.lessons.Count && i < max; i++)
        {
            float y1 = 0.84f - i * (0.84f / max), y0 = y1 - (0.84f / max) + 0.02f;
            Gfx.Dot(c, "star4", 0.05f, (y0 + y1) / 2f, 16f, Gold);
            UIKit.Label(c, Campaign.lessons[i], 12, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.10f, y0, 0.99f, y1);
        }
    }

    // ---------------------------------------------------------------- gráfica de la misión
    void BuildChart()
    {
        var p = UIKit.Panel(root, "Chart", 0.02f, 0.115f, 0.98f, 0.265f, UIKit.Bezel);
        p.GetComponent<Image>().raycastTarget = false;
        var d = Session.Diff;
        var site = Session.Site;
        var items = new List<object[]>();
        items.Add(new object[] { "BATERÍA", new Color(0.45f, 1f, 0.55f, 1f) });
        items.Add(new object[] { "AIRE", new Color(0.40f, 0.80f, 1f, 1f) });
        items.Add(new object[] { "COMIDA", new Color(1f, 0.75f, 0.20f, 1f) });
        items.Add(new object[] { "SALUD", new Color(1f, 0.31f, 0.31f, 1f) });
        if (d.house) items.Add(new object[] { "CASA", new Color(0.9f, 0.9f, 0.9f, 1f) });
        items.Add(new object[] { "TORMENTA", new Color(1f, 1f, 0.40f, 1f) });
        if (d.panels && site.dust > 0) items.Add(new object[] { "POLVO", new Color(1f, 0.60f, 0.24f, 1f) });
        if (d.heater) items.Add(new object[] { "NOCHE LARGA", new Color(0.47f, 0.47f, 1f, 1f) });
        if (d.fails > 0) items.Add(new object[] { "ROTURA", new Color(1f, 0.25f, 0.78f, 1f) });
        if (d.hazards) items.Add(new object[] { "GOLPE/FUGA", new Color(1f, 0.25f, 0.25f, 1f) });
        items.Add(new object[] { "CÁPSULA", new Color(0.45f, 1f, 0.78f, 1f) });
        float w = 1f / items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            Color c = (Color)items[i][1];
            Gfx.Dot(p, "circle", i * w + 0.012f, 0.90f, 11f, c);
            UIKit.Label(p, (string)items[i][0], 11, TextAnchor.MiddleLeft, c, i * w + 0.022f, 0.80f, (i + 1) * w, 1f);
        }
        var go = new GameObject("ChartImage", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.01f, 0.04f); rt.anchorMax = new Vector2(0.99f, 0.80f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var raw = go.GetComponent<RawImage>();
        raw.texture = ChartTexture();
        raw.raycastTarget = false;
    }

    static Texture2D ChartTexture()
    {
        const int W = 1000, H = 130;
        var px = new Color32[W * H];
        var bg = new Color32(8, 14, 10, 255);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        for (int g = 0; g <= 4; g++)
        {
            int y = g * (H - 1) / 4;
            for (int x = 0; x < W; x++) px[y * W + x] = new Color32(30, 48, 36, 255);
        }

        var h = Campaign.history;
        int n = h.Count;
        if (n >= 2)
        {
            for (int i = 0; i < n; i++)
            {
                int ev = h[i].ev;
                if (ev == 0) continue;
                int x = Mathf.RoundToInt(i * (W - 1f) / (n - 1));
                if ((ev & 2) != 0) VLine(px, W, H, x, new Color32(120, 70, 25, 255));
                if ((ev & 4) != 0) VLine(px, W, H, x, new Color32(60, 60, 140, 255));
                if ((ev & 32) != 0) VLine(px, W, H, x, new Color32(115, 255, 200, 255));
                if ((ev & 16) != 0) VLine(px, W, H, x, new Color32(255, 64, 64, 255));
                if ((ev & 8) != 0) VLine(px, W, H, x, new Color32(255, 64, 200, 255));
                if ((ev & 1) != 0) VLine(px, W, H, x, new Color32(255, 255, 102, 255));
            }
            int[] ser = { 3, 5, 2, 1, 0 };   // casa, salud, comida, aire, batería (la batería se dibuja encima)
            Color32[] col =
            {
                new Color32(230, 230, 230, 255), new Color32(255, 80, 80, 255), new Color32(255, 191, 51, 255),
                new Color32(102, 204, 255, 255), new Color32(115, 255, 140, 255)
            };
            for (int s = 0; s < ser.Length; s++)
            {
                if (!Session.ResShown(ser[s])) continue;
                int px0 = 0, py0 = 0;
                for (int i = 0; i < n; i++)
                {
                    int x = Mathf.RoundToInt(i * (W - 1f) / (n - 1));
                    int y = Mathf.RoundToInt(Mathf.Clamp01(h[i].v[ser[s]] / 100f) * (H - 1));
                    if (i > 0) DrawLine(px, W, H, px0, py0, x, y, col[s]);
                    px0 = x; py0 = y;
                }
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void VLine(Color32[] px, int W, int H, int x, Color32 c)
    {
        if (x < 0 || x >= W) return;
        for (int y = 0; y < H; y++) px[y * W + x] = c;
    }

    static void Plot(Color32[] px, int W, int H, int x, int y, Color32 c)
    {
        for (int dy = 0; dy <= 1; dy++)
        {
            int yy = y + dy;
            if (x >= 0 && x < W && yy >= 0 && yy < H) px[yy * W + x] = c;
        }
    }

    static void DrawLine(Color32[] px, int W, int H, int x0, int y0, int x1, int y1, Color32 c)
    {
        int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Plot(px, W, H, x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public void Destroy() { UnityEngine.Object.Destroy(root.gameObject); }
}
