using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Efectos visuales del juego: partículas, números que flotan, anillos, paquetes de datos que vuelan,
// medidores con dibujos (astronauta, carita, batería...) y animaciones constantes.
// Todo se dibuja con código: no hace falta importar ninguna imagen.

public enum Ambient { Calm, Storm, Dust, Cold, Warn }

public static class Gfx
{
    public static readonly Color Good = new Color(0.40f, 1f, 0.50f, 1f);
    public static readonly Color Mid = new Color(1f, 0.78f, 0.20f, 1f);
    public static readonly Color Bad = new Color(1f, 0.30f, 0.25f, 1f);
    public static readonly Color Blue = new Color(0.40f, 0.75f, 1f, 1f);
    public static readonly Color Dim = new Color(0.16f, 0.20f, 0.18f, 1f);

    // Verde, ámbar o rojo según el nivel (0 a 1).
    public static Color Level(float v)
    {
        if (v < 0.25f) return Bad;
        if (v < 0.5f) return Mid;
        return Good;
    }

    // Nombre del dibujo de la carita según el ánimo (0 a 1): feliz, normal o triste.
    public static string FaceName(float v)
    {
        if (v < 0.25f) return "face_sad";
        if (v < 0.5f) return "face_mid";
        return "face_happy";
    }

    // Carita completa (cuerpo de color + ojos y boca) para pantallas fijas. Devuelve el cuerpo.
    public static Image Face(Transform parent, float v01, float x0, float y0, float x1, float y1)
    {
        string nm = FaceName(v01);
        var body = Icon(parent, nm, x0, y0, x1, y1, Level(v01));
        var go = new GameObject("FaceDetail", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(body.transform, false);
        UIKit.Stretch((RectTransform)go.transform);
        var d = go.GetComponent<Image>();
        d.sprite = Art.Detail(nm);
        d.preserveAspect = true;
        d.raycastTarget = false;
        d.color = new Color(0f, 0f, 0f, 0.75f);
        return body;
    }

    // Dibujo que ocupa una zona (por proporciones del padre).
    public static Image Icon(Transform parent, string icon, float x0, float y0, float x1, float y1, Color c)
    {
        var rt = UIKit.Panel(parent, "Icon_" + icon, x0, y0, x1, y1, c);
        var img = rt.GetComponent<Image>();
        img.sprite = Art.Body(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    // Dibujo pegado a la izquierda y centrado en vertical, de tamaño fijo en píxeles (para botones y filas).
    public static Image IconLeft(Transform parent, string icon, float size, float margin, Color c)
    {
        var go = new GameObject("Icon_" + icon, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(margin, 0f);
        var img = go.GetComponent<Image>();
        img.sprite = Art.Body(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = c;
        return img;
    }

    // Círculo o dibujo pequeño colocado por proporciones (x, y de 0 a 1) con tamaño fijo en píxeles.
    public static Image Dot(Transform parent, string icon, float ax, float ay, float size, Color c)
    {
        var go = new GameObject("Dot_" + icon, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(ax, ay);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = Art.Body(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = c;
        return img;
    }
}

// Late suavemente (y se enciende o se apaga). Sirve para los íconos de las máquinas, la noche actual, etc.
public class Pulser : MonoBehaviour
{
    public bool active = true;
    public float speed = 3f, amount = 0.08f;
    public Color onColor = Color.white, offColor = new Color(0.25f, 0.28f, 0.27f, 1f);
    Image img;
    float phase;

    void Awake() { img = GetComponent<Image>(); phase = Random.value * 6.28f; }

    void Update()
    {
        float s = 1f + (active ? amount * Mathf.Sin(Time.unscaledTime * speed + phase) : 0f);
        transform.localScale = new Vector3(s, s, 1f);
        if (img != null) img.color = Color.Lerp(img.color, active ? onColor : offColor, Mathf.Clamp01(Time.unscaledDeltaTime * 8f));
    }
}

// Lanza anillos de señal una y otra vez (la antena "hablando").
public class RingEmitter : MonoBehaviour
{
    public float interval = 2.4f, size = 110f;
    public Color color = Color.white;
    float t;

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t >= interval) { t = 0f; FxLayer.Ring(transform, color, size, 1.2f); }
    }
}

// Un medidor con dibujo: el dibujo se va llenando de abajo hacia arriba, cambia de color según el nivel,
// se mueve solito y muestra el porcentaje. Para el astronauta (salud) y la carita (ánimo) es más grande.
public class IconGauge : MonoBehaviour
{
    Image glow, baseImg, fillImg, detImg;
    Text pct, nameT;
    RectTransform iconRt;
    string kind, faceName = "";
    bool hero, first = true;
    float shown, target, flash, phase, sparkT;
    Color flashCol = Color.white, curCol = Gfx.Good;

    public static IconGauge Create(Transform parent, string title, string icon, float x0, float y0, float x1, float y1, bool isHero)
    {
        Color bg = isHero ? new Color(0.06f, 0.08f, 0.07f, 0.55f) : new Color(0.07f, 0.09f, 0.08f, 1f);
        var rt = UIKit.Panel(parent, "Gauge_" + icon, x0, y0, x1, y1, bg);
        rt.GetComponent<Image>().raycastTarget = false;
        if (!isHero) UIKit.Border(rt, new Color(0.22f, 0.30f, 0.25f, 1f), 2f);
        var g = rt.gameObject.AddComponent<IconGauge>();
        g.Build(rt, title, icon, isHero);
        return g;
    }

    static Image Layer(Transform parent, string name, Sprite sp, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        UIKit.Stretch((RectTransform)go.transform);
        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = c;
        return img;
    }

    void Build(RectTransform root, string title, string icon, bool isHero)
    {
        hero = isHero;
        kind = icon;
        phase = Random.value * 6.28f;
        float iy0 = hero ? 0.15f : 0.30f, iy1 = hero ? 0.93f : 0.82f;
        var area = UIKit.Panel(root, "IconArea", 0.06f, iy0, 0.94f, iy1, new Color(0f, 0f, 0f, 0f));
        area.GetComponent<Image>().raycastTarget = false;
        iconRt = area;

        // brillo de fondo (más grande que el dibujo)
        var gl = UIKit.Panel(area, "Glow", -0.25f, -0.25f, 1.25f, 1.25f, new Color(1f, 1f, 1f, 0f));
        glow = gl.GetComponent<Image>();
        glow.sprite = Art.Glow();
        glow.raycastTarget = false;

        string bodyName = icon == "face" ? "face_happy" : icon;
        baseImg = Layer(area, "Base", Art.Body(bodyName), Gfx.Dim);
        fillImg = Layer(area, "Fill", Art.Body(bodyName), Gfx.Good);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Vertical;
        fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImg.fillAmount = 0f;
        string detName = icon == "face" ? "face_happy" : icon;
        var det = Art.Detail(detName);
        if (det != null) detImg = Layer(area, "Detail", det, new Color(0f, 0f, 0f, icon == "face" ? 0.75f : 0.5f));

        nameT = UIKit.Label(root, title, hero ? 13 : 11, hero ? TextAnchor.UpperCenter : TextAnchor.UpperCenter,
                            new Color(0.62f, 0.78f, 0.68f, 1f), 0f, hero ? 0.93f : 0.82f, 1f, 1f);
        pct = UIKit.Label(root, "", hero ? 24 : 16, TextAnchor.MiddleCenter, Gfx.Good, 0f, 0f, 1f, hero ? 0.16f : 0.30f);
    }

    public void Flash(Color c) { flashCol = c; flash = 1f; }

    // Cambia el nombre del medidor (por ejemplo "BATERÍA  +0.7/s").
    public void SetTitle(string s) { if (nameT != null && nameT.text != s) nameT.text = s; }

    // El dibujo parte de "from" y se llena (o se vacía) hasta "to", sin números flotantes (para el informe de la mañana).
    public void Animate(float from01, float to01)
    {
        shown = Mathf.Clamp01(from01);
        target = Mathf.Clamp01(to01);
        first = false;
        pct.text = Mathf.RoundToInt(target * 100f) + "%";
    }

    // Pone el valor sin números flotantes. animate = true: el dibujo se llena desde cero (para los menús).
    public void Init(float v01, bool animate)
    {
        target = Mathf.Clamp01(v01);
        shown = animate ? 0f : target;
        first = false;
        pct.text = Mathf.RoundToInt(target * 100f) + "%";
    }

    // v01 = valor de 0 a 1. proj = cambio previsto para esta noche (en puntos). fx = false: guarda el cambio y lo muestra después.
    public void Set(float v01, float proj, bool fx)
    {
        v01 = Mathf.Clamp01(v01);
        if (!fx) return;                    // el cambio se mostrará en el siguiente Set con fx = true
        float old = target;
        target = v01;
        if (first) { shown = target; first = false; }
        else
        {
            float diff = (target - old) * 100f;
            if (Mathf.Abs(diff) >= 1.5f)
            {
                bool up = diff > 0f;
                Color c = up ? Gfx.Good : Gfx.Bad;
                FxLayer.FloatText(transform, (up ? "+" : "") + Mathf.RoundToInt(diff), c, hero ? 30 : 24);
                Flash(c);
                if (!up) FxLayer.Burst(transform, c, hero ? 12 : 7, 150f, 11f, "circle");
                else FxLayer.Burst(transform, c, hero ? 8 : 5, 110f, 12f, "star4");
            }
        }
        int pv = Mathf.RoundToInt(target * 100f);
        int dv = Mathf.RoundToInt(proj);
        string ds = dv == 0 ? "" : (dv > 0 ? " <size=12><color=#73ff8c>+" + dv + "</color></size>" : " <size=12><color=#ff6a5a>" + dv + "</color></size>");
        pct.text = pv + "%" + ds;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float tt = Time.unscaledTime + phase;
        shown = Mathf.MoveTowards(shown, target, dt * 0.9f);
        Color lc = Gfx.Level(shown);
        curCol = Color.Lerp(curCol, lc, Mathf.Clamp01(dt * 8f));
        fillImg.fillAmount = shown;
        fillImg.color = curCol;
        pct.color = curCol;

        bool low = shown < 0.25f;
        flash = Mathf.Max(0f, flash - dt * 1.6f);
        float ga = flash * 0.9f;
        Color gc = flashCol;
        if (low && flash < 0.05f) { ga = 0.30f + 0.30f * Mathf.Sin(tt * 7f); gc = Gfx.Bad; }
        else if (flash < 0.05f && hero) { ga = 0.10f + 0.07f * Mathf.Sin(tt * 2f); gc = curCol; }
        glow.color = new Color(gc.r, gc.g, gc.b, Mathf.Clamp01(ga));

        float s = 1f, rot = 0f;
        Vector2 off = Vector2.zero;
        if (kind == "astro")
        {
            off.y = Mathf.Sin(tt * 1.6f) * 3f;                 // flota sin gravedad
            rot = Mathf.Sin(tt * 1.1f) * 3f;
            s = 1f + 0.015f * Mathf.Sin(tt * 2.4f);
            if (low) off.x = Mathf.Sin(tt * 40f) * 2.5f;       // tiembla si está mal
        }
        else if (kind == "face")
        {
            string fn = Gfx.FaceName(shown);                       // la carita cambia con el ánimo
            if (fn != faceName && detImg != null) { faceName = fn; detImg.sprite = Art.Detail(fn); }
            if (shown >= 0.5f) { off.y = Mathf.Abs(Mathf.Sin(tt * 3f)) * 5f; rot = Mathf.Sin(tt * 2f) * 4f; }   // feliz: saltito
            else if (shown >= 0.25f) { s = 1f + 0.02f * Mathf.Sin(tt * 1.5f); }
            else { s = 0.96f + 0.02f * Mathf.Sin(tt * 1.2f); off.y = -3f; off.x = Mathf.Sin(tt * 30f) * 1.5f; }  // triste: caída y temblor
            sparkT -= dt;
            if (shown > 0.6f && sparkT <= 0f)
            {
                sparkT = 0.45f;
                FxLayer.Burst(transform, new Color(1f, 0.9f, 0.4f, 0.9f), 1, 70f, 11f, "star4");
            }
        }
        else
        {
            s = 1f + (low ? 0.06f * Mathf.Sin(tt * 8f) : 0.012f * Mathf.Sin(tt * 2f));
        }
        s *= 1f + flash * 0.10f;
        iconRt.localScale = new Vector3(s, s, 1f);
        iconRt.localRotation = Quaternion.Euler(0f, 0f, rot);
        iconRt.anchoredPosition = off;

    }
}

// Capa de efectos encima de la pantalla de juego (no recibe clics).
public class FxLayer : MonoBehaviour
{
    public static FxLayer I;
    public Color starTint = new Color(0.85f, 0.95f, 1f, 0.35f);     // color de las estrellitas (cambia con Marte o la Luna)

    class P
    {
        public RectTransform rt;
        public Image img;
        public Text txt;
        public Vector2 pos, vel, from, to;
        public float t, life, delay, grav, drag, s0, s1, rot, rotV, arc, aspect = 1f;
        public Color c;
        public int mode;          // 0 = libre, 1 = vuela de "from" a "to"
        public int env;           // 0 = se apaga al final, 1 = aparece y se apaga
        public bool shown = true;
    }

    RectTransform self;
    readonly List<P> live = new List<P>();
    readonly Stack<P> pool = new Stack<P>();
    Ambient amb = Ambient.Calm;
    float ambTimer, ambTimer2, stormTime;

    public static FxLayer Create(RectTransform parent)
    {
        var go = new GameObject("FxLayer", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIKit.Stretch((RectTransform)go.transform);
        var f = go.AddComponent<FxLayer>();
        f.self = (RectTransform)go.transform;
        I = f;
        return f;
    }

    void OnDestroy() { if (I == this) I = null; }

    // ---------------------------------------------------------------- utilidades
    Vector2 Local(Transform t)
    {
        Vector3 w = t.position;
        var r = t as RectTransform;
        if (r != null) w = r.TransformPoint(r.rect.center);
        Vector3 l = self.InverseTransformPoint(w);
        return new Vector2(l.x, l.y);
    }

    P NewP(string icon)
    {
        P p = pool.Count > 0 ? pool.Pop() : MakeP();
        p.img.sprite = Art.Body(icon);
        p.aspect = Art.Aspect(icon);
        p.vel = Vector2.zero; p.t = 0f; p.delay = 0f; p.grav = 0f; p.drag = 0f; p.rot = 0f; p.rotV = 0f; p.arc = 0f;
        p.mode = 0; p.env = 0; p.shown = true;
        p.rt.gameObject.SetActive(true);
        live.Add(p);
        return p;
    }

    P MakeP()
    {
        var go = new GameObject("P", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(self, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return new P { rt = rt, img = img };
    }

    void Release(P p)
    {
        if (p.txt != null) { Destroy(p.rt.gameObject); return; }
        p.rt.gameObject.SetActive(false);
        pool.Push(p);
    }

    // ---------------------------------------------------------------- efectos (se pueden llamar desde cualquier lado)
    // Chispas que salen en todas direcciones.
    public static void Burst(Transform at, Color c, int n, float speed, float size, string icon)
    {
        if (I == null || at == null) return;
        I.DoBurst(I.Local(at), c, n, speed, size, icon);
    }

    // Anillo que crece y se desvanece (una onda).
    public static void Ring(Transform at, Color c, float endSize, float life)
    {
        if (I == null || at == null) return;
        P p = I.NewP("ring");
        p.pos = I.Local(at); p.c = c; p.life = life; p.s0 = endSize * 0.15f; p.s1 = endSize;
    }

    // Un dibujo que aparece, crece y se desvanece (escudo, llave, luna...).
    public static void Pop(Transform at, string icon, Color c, float s0, float s1, float life, float rotSpeed)
    {
        if (I == null || at == null) return;
        P p = I.NewP(icon);
        p.pos = I.Local(at); p.c = c; p.life = life; p.s0 = s0; p.s1 = s1; p.env = 1; p.rotV = rotSpeed;
    }

    // Número o palabra que sube y se desvanece.
    public static void FloatText(Transform at, string text, Color c, int size)
    {
        if (I == null || at == null) return;
        I.DoFloatText(I.Local(at), text, c, size);
    }

    // Paquetes que vuelan de un lugar a otro (datos que se envían o llegan).
    public static void Fly(Transform from, Transform to, Color c, int n, float life, float arc)
    {
        if (I == null || from == null || to == null) return;
        Vector2 a = I.Local(from), b = I.Local(to);
        for (int i = 0; i < n; i++)
        {
            P p = I.NewP(i % 2 == 0 ? "star4" : "circle");
            p.mode = 1; p.from = a; p.to = b; p.pos = a;
            p.c = c; p.life = life; p.s0 = 20f; p.s1 = 12f; p.delay = i * 0.16f; p.arc = arc * (i % 2 == 0 ? 1f : -0.6f);
            p.rotV = 120f;
            p.shown = false;
            p.rt.gameObject.SetActive(false);
        }
    }

    // Lluvia de partículas de tormenta solar durante unos segundos.
    public static void Storm(float seconds)
    {
        if (I != null) I.stormTime = Mathf.Max(I.stormTime, seconds);
    }

    // Una roca que cruza la pantalla y golpea en "target".
    public static void Meteor(Transform target)
    {
        if (I == null || target == null) return;
        Vector2 end = I.Local(target);
        Vector2 start = end + new Vector2(-520f, 380f);
        for (int i = 0; i < 6; i++)
        {
            P p = I.NewP("circle");
            p.mode = 1; p.from = start - new Vector2(i * 14f, -i * 10f); p.to = end; p.pos = p.from;
            p.c = i == 0 ? new Color(1f, 0.9f, 0.6f, 1f) : new Color(1f, 0.5f + 0.05f * i, 0.2f, 0.8f - i * 0.1f);
            p.life = 0.55f; p.s0 = 34f - i * 4f; p.s1 = 10f; p.delay = i * 0.03f;
            p.shown = false; p.rt.gameObject.SetActive(false);
        }
        I.DoBurst(end, new Color(1f, 0.7f, 0.25f, 1f), 18, 260f, 14f, "star4");
    }

    public void SetAmbient(Ambient a) { amb = a; }

    // ---------------------------------------------------------------- partículas
    void DoBurst(Vector2 pos, Color c, int n, float speed, float size, string icon)
    {
        for (int i = 0; i < n; i++)
        {
            string ic = icon == "mix" ? (i % 2 == 0 ? "star4" : "circle") : icon;
            P p = NewP(ic);
            float ang = Random.value * 6.2832f;
            float sp = speed * Random.Range(0.45f, 1.1f);
            p.pos = pos;
            p.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
            p.grav = -220f; p.drag = 1.6f;
            p.c = c; p.life = Random.Range(0.5f, 0.95f);
            p.s0 = size * Random.Range(0.7f, 1.2f); p.s1 = size * 0.2f;
            p.rotV = Random.Range(-200f, 200f);
        }
    }

    void DoFloatText(Vector2 pos, string text, Color c, int size)
    {
        var go = new GameObject("FT", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(self, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 50f);
        var t = go.GetComponent<Text>();
        t.font = UIKit.GetFont();
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = c;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(2f, -2f);
        var p = new P { rt = rt, txt = t, c = c, life = 1.5f, s0 = 1f, s1 = 1f };
        p.pos = pos + new Vector2(Random.Range(-12f, 12f), 12f);
        p.vel = new Vector2(0f, 70f);
        p.drag = 0.7f;
        live.Add(p);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        TickAmbient(dt);
        for (int i = live.Count - 1; i >= 0; i--)
        {
            P p = live[i];
            if (!p.shown)
            {
                p.delay -= dt;
                if (p.delay > 0f) continue;
                p.shown = true;
                p.rt.gameObject.SetActive(true);
            }
            p.t += dt;
            float u = p.t / p.life;
            if (u >= 1f) { Release(p); live.RemoveAt(i); continue; }

            if (p.mode == 1)
            {
                float e = u * u * (3f - 2f * u);
                Vector2 pos = Vector2.Lerp(p.from, p.to, e);
                pos.y += Mathf.Sin(e * Mathf.PI) * p.arc;
                p.pos = pos;
            }
            else
            {
                p.vel.y += p.grav * dt;
                if (p.drag > 0f) p.vel *= Mathf.Max(0f, 1f - p.drag * dt);
                p.pos += p.vel * dt;
            }
            p.rot += p.rotV * dt;

            float a;
            if (p.env == 1) a = Mathf.Sin(u * Mathf.PI);
            else a = u < 0.55f ? 1f : (1f - u) / 0.45f;
            Color col = new Color(p.c.r, p.c.g, p.c.b, p.c.a * Mathf.Clamp01(a));
            p.rt.anchoredPosition = p.pos;
            if (p.txt != null)
            {
                p.txt.color = col;
                float k = 1f + 0.25f * Mathf.Sin(Mathf.Min(1f, u * 4f) * 1.57f);
                p.rt.localScale = new Vector3(k, k, 1f);
            }
            else
            {
                float size = Mathf.Lerp(p.s0, p.s1, u);
                p.rt.sizeDelta = new Vector2(size * p.aspect, size);
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);
                p.img.color = col;
            }
        }
    }

    // ---------------------------------------------------------------- efectos que no paran (según lo que pasa esta noche)
    void TickAmbient(float dt)
    {
        if (live.Count > 240) return;
        float w = self.rect.width, h = self.rect.height;
        if (w < 10f) { w = 1280f; h = 720f; }

        // estrellitas que titilan: siempre
        ambTimer2 -= dt;
        if (ambTimer2 <= 0f)
        {
            ambTimer2 = amb == Ambient.Calm ? 0.22f : 0.5f;
            P s = NewP("star4");
            s.pos = new Vector2(Random.Range(-w / 2f, w / 2f), Random.Range(-h / 2f, h / 2f));
            s.c = starTint;
            s.life = Random.Range(1.2f, 2.2f); s.s0 = 6f; s.s1 = Random.Range(10f, 18f); s.env = 1; s.rotV = 30f;
        }

        ambTimer -= dt;
        if (stormTime > 0f)
        {
            stormTime -= dt;
            while (ambTimer <= 0f)
            {
                ambTimer += 0.015f;
                P p = NewP("circle");
                p.pos = new Vector2(Random.Range(-w / 2f, w / 2f + 300f), h / 2f + 20f);
                p.vel = new Vector2(Random.Range(-780f, -520f), Random.Range(-720f, -480f));
                p.c = Random.value < 0.5f ? new Color(1f, 0.35f, 0.2f, 0.8f) : new Color(1f, 0.85f, 0.35f, 0.8f);
                p.life = 1.1f; p.s0 = Random.Range(8f, 14f); p.s1 = 3f;
            }
            return;
        }
        if (ambTimer > 0f) return;
        switch (amb)
        {
            case Ambient.Warn:
            {
                ambTimer = 0.16f;
                P p = NewP("circle");
                p.pos = new Vector2(Random.Range(-w / 2f, w / 2f), -h / 2f - 10f);
                p.vel = new Vector2(Random.Range(-25f, 25f), Random.Range(70f, 140f));
                p.c = new Color(1f, 0.45f, 0.2f, 0.55f); p.life = 2.6f; p.s0 = 9f; p.s1 = 2f;
                break;
            }
            case Ambient.Dust:
            {
                ambTimer = 0.05f;
                P p = NewP("circle");
                p.pos = new Vector2(-w / 2f - 20f, Random.Range(-h / 2f, h / 2f));
                p.vel = new Vector2(Random.Range(260f, 420f), Random.Range(-40f, 40f));
                p.c = new Color(0.85f, 0.6f, 0.32f, 0.35f); p.life = 3.4f; p.s0 = Random.Range(10f, 20f); p.s1 = 5f;
                break;
            }
            case Ambient.Cold:
            {
                ambTimer = 0.07f;
                P p = NewP("star4");
                p.pos = new Vector2(Random.Range(-w / 2f, w / 2f), h / 2f + 10f);
                p.vel = new Vector2(Random.Range(-18f, 18f), Random.Range(-130f, -80f));
                p.c = new Color(0.7f, 0.88f, 1f, 0.6f); p.life = 6f; p.s0 = 12f; p.s1 = 7f; p.rotV = 60f;
                break;
            }
            default:
                ambTimer = 0.5f;
                break;
        }
    }
}
