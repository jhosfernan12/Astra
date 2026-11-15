using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIBar
{
    public RectTransform fill;
    public Image fillImage;
    public bool vertical;     // true = la barra sube de abajo hacia arriba
    public void Set(float v, Color c)
    {
        fill.anchorMax = vertical ? new Vector2(1f, Mathf.Clamp01(v)) : new Vector2(Mathf.Clamp01(v), 1f);
        fillImage.color = c;
    }
}

// Sonidos de hover/clic para cualquier botón (también suena "denegado" si está bloqueado).
public class UiSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public Selectable target;
    public Sfx? click = Sfx.UiClick;
    public bool alwaysDenied;

    public void OnPointerEnter(PointerEventData e)
    {
        if (alwaysDenied) return;
        if (target == null || target.IsInteractable()) GameAudio.Play(Sfx.UiHover);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (alwaysDenied || (target != null && !target.IsInteractable())) { GameAudio.Play(Sfx.UiDenied); return; }
        if (click.HasValue) GameAudio.Play(click.Value);
    }
}

// Guarda el tamaño base de un texto para poder reescalarlo en vivo (menú de pausa).
public class TextBase : MonoBehaviour
{
    public int size; public int lastMax; public float lastScale = 1f;
    public float maxF;                       // tamaño máximo SIN redondear (así subir y bajar el tamaño no pierde precisión)
    public bool hasBox, boxDone;             // caja original del texto (para poder agrandarla un poco cuando se sube el tamaño)
    public Vector2 boxMin, boxMax;
    public float boxExp;                     // cuántos píxeles se agrandó la caja arriba y abajo
    int born;

    void Awake() { born = Time.frameCount; }

    // Unos cuadros después de nacer (cuando el diseño ya tiene su tamaño real) se le da a la caja el margen que pide el tamaño de texto actual.
    void Update()
    {
        if (Time.frameCount < born + 2) return;
        boxDone = true;
        var t = GetComponent<Text>();
        if (t != null) UIKit.ExpandBox(t, this);
        enabled = false;
    }
}

// Hace parpadear un marco de resaltado.
public class PulseAlpha : MonoBehaviour
{
    CanvasGroup cg;
    void Awake() { cg = GetComponent<CanvasGroup>(); }
    void Update() { if (cg != null) cg.alpha = 0.3f + 0.7f * Mathf.PingPong(Time.unscaledTime * 2.2f, 1f); }
}

// Mini librería para construir UI por código (cero prefabs, cero arrastrar cosas)
public static class UIKit
{
    public static Font CustomFont;
    public static float TextScale = 1f;   // multiplicador de TODOS los textos (se cambia en GameFlow > Text Scale o en la pausa)

    // Límites del tamaño de texto (1.0 = ya viene grande; 1.8 = el máximo).
    static float appliedScale = -1f;      // último tamaño aplicado con RescaleAll
    public const float MinScale = 1.0f;
    public const float MaxScale = 1.8f;
    public const int MinFont = 11;        // un texto con ajuste automático NUNCA baja de este tamaño (antes llegaba a 6 y no se leía)
    public static float Scale { get { return Mathf.Clamp(TextScale, MinScale, MaxScale); } }

    // Aplica el multiplicador global a un tamaño de letra.
    // Las letras pequeñas se agrandan más (11 -> 14.6, 13 -> 15.8, 15 -> 17); las grandes (20 o más) casi no cambian.
    public static int Size(int baseSize)
    {
        float b = baseSize + Mathf.Max(0f, 20f - baseSize) * 0.4f;
        return Mathf.Max(MinFont, Mathf.RoundToInt(b * Scale));
    }
    static Font fallback;

    public static readonly Color Bg = new Color(0.03f, 0.06f, 0.04f, 0.96f);
    public static readonly Color Bezel = new Color(0.13f, 0.13f, 0.14f, 0.97f);
    public static readonly Color Phosphor = new Color(0.45f, 1f, 0.55f, 1f);
    public static readonly Color Amber = new Color(1f, 0.75f, 0.2f, 1f);
    public static readonly Color Red = new Color(1f, 0.25f, 0.2f, 1f);
    public static readonly Color Dark = new Color(0.08f, 0.10f, 0.09f, 1f);
    public static readonly Color Selected = new Color(0.12f, 0.35f, 0.18f, 1f);

    // ---- Estilo de la Ing. Freeman (recuadro azul para distinguirla del resto de la interfaz)
    public static readonly Color RadioBg = new Color(0.05f, 0.14f, 0.34f, 0.98f);
    public static readonly Color RadioEdge = new Color(0.35f, 0.65f, 1f, 1f);
    public static readonly Color RadioText = new Color(0.88f, 0.95f, 1f, 1f);
    public static readonly Color RadioButton = new Color(0.12f, 0.30f, 0.60f, 1f);
    public const string RadioName = "ING. FREEMAN";
    public const string RadioNameColor = "#7fc4ff";
    public static int RadioFont = 22;         // <-- tamaño de letra del mensaje (súbelo o bájalo aquí)
    public static int RadioHeaderFont = 18;

    public static void SetSize(Text t, int baseSize)
    {
        var tb = t.GetComponent<TextBase>();
        if (tb == null) tb = t.gameObject.AddComponent<TextBase>();
        tb.size = baseSize;
        tb.lastScale = Scale;
        t.fontSize = Size(baseSize);
        if (t.resizeTextForBestFit) { t.resizeTextMaxSize = Size(baseSize); tb.lastMax = t.resizeTextMaxSize; tb.maxF = tb.lastMax; }
    }

    // Agranda la caja del texto un poco hacia arriba y hacia abajo cuando se sube el tamaño: con la caja original el texto ya llenaba
    // el espacio y "no crecía" aunque la letra pidiera más (por eso parecía que solo se podía bajar el tamaño).
    public static void ExpandBox(Text t, TextBase tb)
    {
        if (t == null || tb == null) return;
        if (t.GetComponent<ContentSizeFitter>() != null) return;          // los textos con barra de desplazamiento crecen solos
        var rt = t.rectTransform;
        float k = Mathf.Max(0f, Scale - 1f);
        float h0 = Mathf.Max(0f, rt.rect.height - 2f * tb.boxExp);          // alto sin la ampliación anterior
        float d = Mathf.Min(h0 * 0.5f * k, 30f * k);
        float delta = d - tb.boxExp;                                        // solo se aplica la diferencia: no pisa otros cambios de posición
        if (Mathf.Abs(delta) < 0.01f) return;
        rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y - delta);
        rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y + delta);
        tb.boxExp = d;
        tb.hasBox = true;
    }

    // Reaplica TextScale a todos los textos bajo "root" (menos los que cuelgan de "skip").
    // Todos los textos tienen ajuste automático: si ya no caben con el tamaño nuevo, se encogen solos en vez de cortarse.
    public static void RescaleAll(Transform root, Transform skip)
    {
        float now = Scale;
        float prev = appliedScale > 0f ? appliedScale : now;
        foreach (var t in root.GetComponentsInChildren<Text>(true))
        {
            if (skip != null && t.transform.IsChildOf(skip)) continue;
            var tb = t.GetComponent<TextBase>();
            if (tb == null)
            {
                // Texto creado por otro código: se toma su tamaño actual como base para que también cambie.
                tb = t.gameObject.AddComponent<TextBase>();
                tb.size = Mathf.Max(MinFont, Mathf.RoundToInt(t.fontSize / Mathf.Max(0.5f, prev)));
                tb.lastScale = prev;
                tb.maxF = t.resizeTextForBestFit ? t.resizeTextMaxSize : 0f;
                tb.boxDone = true; tb.enabled = false;
            }
            float ratio = now / Mathf.Max(0.01f, tb.lastScale);
            tb.lastScale = now;
            t.resizeTextMinSize = MinFont;
            if (!t.resizeTextForBestFit && t.GetComponent<Typewriter>() != null)
            {
                // Un texto que el Typewriter dejó con tamaño fijo vuelve al ajuste automático para no perder líneas.
                t.resizeTextForBestFit = true;
                t.resizeTextMaxSize = Size(tb.size);
                tb.lastMax = t.resizeTextMaxSize; tb.maxF = tb.lastMax;
            }
            else if (t.resizeTextForBestFit)
            {
                float cur = t.resizeTextMaxSize;
                if (tb.maxF <= 0f || Mathf.Abs(Mathf.Round(tb.maxF) - cur) > 0.5f) tb.maxF = cur;      // alguien más lo cambió: se parte de ese valor
                tb.maxF *= ratio;                                                                    // sin redondear: subir y bajar no pierde tamaño
                t.resizeTextMaxSize = Mathf.Max(MinFont, Mathf.RoundToInt(tb.maxF));
                tb.lastMax = t.resizeTextMaxSize;
            }
            else t.fontSize = Size(tb.size);
            ExpandBox(t, tb);
        }
        appliedScale = now;
    }

    public static Slider MakeSlider(Transform parent, float x0, float y0, float x1, float y1,
                                    float min, float max, float value, Action<float> onChange)
    {
        var rt = Panel(parent, "Slider", x0, y0, x1, y1, new Color(0.1f, 0.1f, 0.1f, 1f));
        var slider = rt.gameObject.AddComponent<Slider>();
        var fillArea = Panel(rt, "Fill Area", 0f, 0.3f, 1f, 0.7f, new Color(0f, 0f, 0f, 0f));
        fillArea.GetComponent<Image>().raycastTarget = false;
        var fill = Panel(fillArea, "Fill", 0f, 0f, 1f, 1f, Phosphor);
        fill.GetComponent<Image>().raycastTarget = false;
        var handleArea = Root(rt, "Handle Slide Area");
        var handle = Panel(handleArea, "Handle", 0f, 0f, 0f, 1f, Amber);
        handle.sizeDelta = new Vector2(16f, 0f);
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min; slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(delegate (float v) { if (onChange != null) onChange(v); });
        return slider;
    }

    public static Font GetFont()
    {
        if (CustomFont != null) return CustomFont;
        if (fallback == null)
            fallback = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Arial" }, 16);
        return fallback;
    }

    public static Canvas CreateCanvas(Transform parent, Camera cam)
    {
        var go = new GameObject("UI Canvas");
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
        }
        else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        return canvas;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    public static RectTransform Root(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Stretch(rt);
        return rt;
    }

    public static RectTransform Panel(Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color,
                             float x0, float y0, float x1, float y1)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = new Vector2(6f, 3f); rt.offsetMax = new Vector2(-6f, -3f);
        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = Size(size);
        // TODOS los textos se ajustan solos a su caja: al subir el tamaño de texto se encogen lo justo para no perder letras.
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = MinFont;
        t.resizeTextMaxSize = Size(size);
        var tbase = go.AddComponent<TextBase>();
        tbase.size = size;
        tbase.lastScale = Scale;
        tbase.lastMax = t.resizeTextMaxSize;
        tbase.maxF = tbase.lastMax;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    // Texto LARGO con barra de desplazamiento: conserva un tamaño de letra cómodo (no se encoge) y se mueve con la rueda del ratón,
    // arrastrando o con la barra de la derecha. Devuelve el Text por si quieres cambiarlo luego.
    public static Text ScrollText(Transform parent, string text, int size, Color color, float x0, float y0, float x1, float y1)
    {
        var vp = Panel(parent, "ScrollText", x0, y0, x1, y1, new Color(0f, 0f, 0f, 0f));
        vp.GetComponent<Image>().raycastTarget = true;
        vp.gameObject.AddComponent<RectMask2D>();

        var go = new GameObject("Content", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        go.transform.SetParent(vp, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-30f, 0f);          // 8 px de margen a la izquierda y 22 a la derecha (para la barra)
        rt.anchoredPosition = new Vector2(-7f, 0f);

        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = Size(size);
        t.resizeTextForBestFit = false;
        t.alignment = TextAnchor.UpperLeft;
        t.color = color;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
        var tbase = go.AddComponent<TextBase>();
        tbase.size = size;
        tbase.lastScale = Scale;

        var fit = go.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var track = Panel(vp, "Bar", 0.982f, 0f, 1f, 1f, new Color(0.10f, 0.12f, 0.10f, 0.9f));
        var handle = Panel(track, "Handle", 0f, 0f, 1f, 1f, Phosphor);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle;
        bar.targetGraphic = handle.GetComponent<Image>();
        bar.direction = Scrollbar.Direction.BottomToTop;

        var sr = vp.gameObject.AddComponent<ScrollRect>();
        sr.content = rt;
        sr.viewport = vp;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 35f;
        sr.verticalScrollbar = bar;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return t;
    }

    // clickSfx: sonido al hacer clic (null = silencio, útil si el botón reproduce su propio sonido).
    public static Button MakeButton(Transform parent, string label, int size,
                                    float x0, float y0, float x1, float y1,
                                    Color bg, Action onClick,
                                    TextAnchor align = TextAnchor.MiddleCenter, Sfx? clickSfx = Sfx.UiClick)
    {
        var rt = Panel(parent, "Button", x0, y0, x1, y1, bg);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = rt.GetComponent<Image>();
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        Label(rt, label, size, align, Phosphor, 0f, 0f, 1f, 1f);
        btn.onClick.AddListener(() => { if (onClick != null) onClick(); });
        var snd = rt.gameObject.AddComponent<UiSound>();
        snd.target = btn;
        snd.click = clickSfx;
        return btn;
    }

    public static UIBar MakeBar(Transform parent, float x0, float y0, float x1, float y1)
    {
        var back = Panel(parent, "Bar", x0, y0, x1, y1, new Color(0.1f, 0.1f, 0.1f, 1f));
        var fill = Panel(back, "Fill", 0f, 0f, 1f, 1f, Phosphor);
        return new UIBar { fill = fill, fillImage = fill.GetComponent<Image>() };
    }

    // Barra vertical: se llena de abajo hacia arriba (para SUBIDA y DESCARGA).
    public static UIBar MakeVBar(Transform parent, float x0, float y0, float x1, float y1)
    {
        var back = Panel(parent, "VBar", x0, y0, x1, y1, new Color(0.1f, 0.1f, 0.1f, 1f));
        Border(back, new Color(0.30f, 0.38f, 0.33f, 1f), 2f);
        var fill = Panel(back, "Fill", 0f, 0f, 1f, 0f, Phosphor);
        return new UIBar { fill = fill, fillImage = fill.GetComponent<Image>(), vertical = true };
    }

    // Marco parpadeante alrededor de un elemento (para señalar cosas en el tutorial).
    public static CanvasGroup Frame(RectTransform target, Color color, float thickness = 4f, float grow = 5f)
    {
        var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(target, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-grow, -grow); rt.offsetMax = new Vector2(grow, grow);
        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        Edge(rt, 0f, 1f, 1f, 1f, new Vector2(0f, -thickness), Vector2.zero, color);      // arriba
        Edge(rt, 0f, 0f, 1f, 0f, Vector2.zero, new Vector2(0f, thickness), color);       // abajo
        Edge(rt, 0f, 0f, 0f, 1f, Vector2.zero, new Vector2(thickness, 0f), color);       // izquierda
        Edge(rt, 1f, 0f, 1f, 1f, new Vector2(-thickness, 0f), Vector2.zero, color);      // derecha
        go.AddComponent<PulseAlpha>();
        return cg;
    }

    // Borde fijo (sin parpadeo) alrededor de un elemento.
    public static GameObject Border(RectTransform target, Color color, float thickness)
    {
        var go = new GameObject("Border", typeof(RectTransform));
        go.transform.SetParent(target, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Edge(rt, 0f, 1f, 1f, 1f, new Vector2(0f, -thickness), Vector2.zero, color);
        Edge(rt, 0f, 0f, 1f, 0f, Vector2.zero, new Vector2(0f, thickness), color);
        Edge(rt, 0f, 0f, 0f, 1f, Vector2.zero, new Vector2(thickness, 0f), color);
        Edge(rt, 1f, 0f, 1f, 1f, new Vector2(-thickness, 0f), Vector2.zero, color);
        return go;
    }

    static void Edge(Transform parent, float ax0, float ay0, float ax1, float ay1, Vector2 omin, Vector2 omax, Color c)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(ax0, ay0); rt.anchorMax = new Vector2(ax1, ay1);
        rt.offsetMin = omin; rt.offsetMax = omax;
        var img = go.GetComponent<Image>();
        img.color = c; img.raycastTarget = false;
    }

    public static string Hex(LogKind k)
    {
        switch (k)
        {
            case LogKind.Good: return "#73ff8c";
            case LogKind.Warn: return "#ffc033";
            case LogKind.Bad: return "#ff4040";
        }
        return "#7fbf8a";
    }
}
