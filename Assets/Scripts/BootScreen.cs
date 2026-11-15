using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Pantalla de inicio: el logo Polarstern aparece en el centro (fade + sonido), sube un poco y se queda
// mientras carga el sistema hasta 100%; al final aparece el botón EMPEZAR.
public class BootScreen : MonoBehaviour
{
    public static float FactSeconds = 7f;     // cuánto tiempo se muestra cada curiosidad (súbelo para leer con más calma)

    class Stage
    {
        public float to, time; public string text;
        public Stage(float to, float time, string text) { this.to = to; this.time = time; this.text = text; }
    }

    static readonly Stage[] Stages =
    {
        new Stage(0.12f, 1.5f, "Verificando integridad del núcleo..."),
        new Stage(0.35f, 1.9f, "Cargando módulos de misión..."),
        new Stage(0.58f, 1.7f, "Sincronizando reloj orbital..."),
        new Stage(0.80f, 2.0f, "Calibrando antena de espacio profundo..."),
        new Stage(0.96f, 1.7f, "Estableciendo enlace con la Tierra..."),
        new Stage(1.00f, 0.8f, "Sistema listo."),
    };

    static readonly string[] Facts =
    {
        "¿Sabías que...? Una señal de radio tarda entre 3 y 22 minutos en llegar de Marte a la Tierra. Desde la Luna llega en solo 1,3 segundos. (NASA)",
        "El rover Curiosity tiene un detector de radiación. En Marte midió unos 0,67 mSv por día; en la Tierra recibimos unos 0,007. (NASA/JPL)",
        "En 2018 una tormenta de polvo cubrió todo Marte y el rover Opportunity dejó de comunicarse con la Tierra. (NASA/JPL)",
        "Las llamaradas del Sol tienen letras: A, B, C, M y X. Cada letra es 10 veces más fuerte que la anterior. La NASA las guarda en DONKI.",
        "En septiembre de 2017 una tormenta solar duplicó la radiación que Curiosity había medido en Marte. (NASA/JPL)",
        "La nave Chang'e-4 midió la radiación en la Luna: es unas 2,6 veces más que dentro de la Estación Espacial. (Science Advances, 2020)",
        "NASA OSDR guarda experimentos de biología espacial: cómo cambian las plantas y los astronautas en el espacio. ¡Es ciencia abierta para todos!",
        "La Red del Espacio Profundo de la NASA tiene antenas gigantes en California, Madrid y Canberra, para que ninguna nave se quede sin señal.",
        "El satélite MRO guarda los datos del rover Curiosity y los manda a la Tierra después. ¡Igual que el buffer de tu base! (NASA/JPL)",
        "Para ir a Marte hay que esperar el momento justo: cada 26 meses la Tierra y Marte quedan bien alineados.",
    };

    Action onDone;
    CanvasGroup rootGroup, logoGroup, subGroup, loadGroup, startGroup, factGroup;
    RectTransform logoRt;
    Text titleText, pctText, statusText, factText;
    UIBar bar;
    Button startBtn;
    bool started;

    // logoSprite: tu imagen (opcional). Si es null se dibuja un logo de estrella polar por código.
    public void Begin(Sprite logoSprite, Action done)
    {
        onDone = done;
        var rt = (RectTransform)transform;
        UIKit.Stretch(rt);
        rootGroup = gameObject.AddComponent<CanvasGroup>();
        UIKit.Panel(rt, "Black", 0f, 0f, 1f, 1f, Color.black);

        // ---- Carga (aparece debajo del logo)
        var ld = UIKit.Root(rt, "Loading");
        loadGroup = ld.gameObject.AddComponent<CanvasGroup>();
        loadGroup.alpha = 0f;
        titleText = UIKit.Label(ld, "INICIANDO SISTEMA", 26, TextAnchor.MiddleCenter, UIKit.Phosphor, 0.1f, 0.43f, 0.9f, 0.49f);
        pctText = UIKit.Label(ld, "CARGANDO  0%", 20, TextAnchor.LowerLeft, UIKit.Phosphor, 0.25f, 0.385f, 0.75f, 0.43f);
        bar = UIKit.MakeBar(ld, 0.25f, 0.345f, 0.75f, 0.385f);
        bar.Set(0f, UIKit.Phosphor);
        statusText = UIKit.Label(ld, "", 16, TextAnchor.UpperLeft, new Color(0.45f, 1f, 0.55f, 0.7f), 0.25f, 0.30f, 0.75f, 0.345f);
        factText = UIKit.Label(ld, "", 16, TextAnchor.UpperLeft, new Color(0.75f, 0.88f, 1f, 0.95f), 0.20f, 0.14f, 0.80f, 0.30f);
        factText.resizeTextForBestFit = true;
        factText.resizeTextMinSize = UIKit.MinFont;
        factText.resizeTextMaxSize = UIKit.Size(16);
        factGroup = factText.gameObject.AddComponent<CanvasGroup>();
        factGroup.alpha = 0f;

        startBtn = UIKit.MakeButton(ld, "EMPEZAR", 30, 0.38f, 0.04f, 0.62f, 0.12f, UIKit.Selected, OnStart,
                                    TextAnchor.MiddleCenter, null);   // el sonido lo reproduce BootStart
        startGroup = startBtn.gameObject.AddComponent<CanvasGroup>();
        startGroup.alpha = 0f;
        UIKit.Frame((RectTransform)startBtn.transform, UIKit.Phosphor, 3f, 5f);
        startBtn.gameObject.SetActive(false);

        // ---- Logo (queda visible TODO el tiempo)
        var lg = UIKit.Root(rt, "Logo");
        logoRt = lg;
        logoGroup = lg.gameObject.AddComponent<CanvasGroup>();
        logoGroup.alpha = 0f; logoGroup.blocksRaycasts = false;
        var imgGo = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(lg, false);
        var irt = (RectTransform)imgGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.72f);
        irt.sizeDelta = new Vector2(190f, 190f);
        var img = imgGo.GetComponent<Image>();
        img.sprite = logoSprite != null ? logoSprite : MakeLogo(192);
        img.color = logoSprite != null ? Color.white : UIKit.Phosphor;
        img.preserveAspect = true; img.raycastTarget = false;
        // Cajas altas + ajuste automático de tamaño: así el texto nunca se oculta aunque cambies Text Scale o la resolución.
        var word = UIKit.Label(lg, "P O L A R S T E R N", 44, TextAnchor.MiddleCenter, UIKit.Phosphor, 0.05f, 0.46f, 0.95f, 0.61f);
        word.resizeTextForBestFit = true; word.resizeTextMinSize = UIKit.MinFont; word.resizeTextMaxSize = 72;
        var sub = UIKit.Label(lg, "CENTRO DE CONTROL DE MISIÓN", 18, TextAnchor.MiddleCenter, new Color(0.45f, 1f, 0.55f, 0.55f), 0.05f, 0.40f, 0.95f, 0.46f);
        sub.resizeTextForBestFit = true; sub.resizeTextMinSize = UIKit.MinFont; sub.resizeTextMaxSize = 28;
        subGroup = sub.gameObject.AddComponent<CanvasGroup>();

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(0.6f);

        // El logo empieza centrado en la pantalla
        float h = ((RectTransform)transform).rect.height;
        float startY = -0.171f * h;
        logoRt.anchoredPosition = new Vector2(0f, startY);

        GameAudio.Play(Sfx.BootLogo);
        yield return Fade(logoGroup, 1f, 1.4f);
        yield return new WaitForSecondsRealtime(1.6f);

        // El logo sube un poco (sin desaparecer) y aparece la carga debajo
        GameAudio.SetLoop(Loop.MusicBoot, true);
        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.9f));
            logoRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, 0f, k));
            subGroup.alpha = 1f - k;
            loadGroup.alpha = k;
            yield return null;
        }
        logoRt.anchoredPosition = Vector2.zero;
        subGroup.alpha = 0f; loadGroup.alpha = 1f;

        StartCoroutine(FactLoop());

        // Carga
        float p = 0f;
        int lastTick = 0;
        foreach (var st in Stages)
        {
            statusText.text = "> " + st.text;
            float from = p, tt = 0f;
            while (tt < st.time)
            {
                tt += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tt / st.time));
                p = Mathf.Lerp(from, st.to, k);
                SetProgress(p);
                titleText.text = "INICIANDO SISTEMA" + new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
                int tick = Mathf.FloorToInt(p * 10f);
                if (tick > lastTick) { lastTick = tick; GameAudio.Play(Sfx.BootTick); }
                yield return null;
            }
            p = st.to; SetProgress(p);
        }

        // 100%: aparece el botón
        titleText.text = "SISTEMA POLARSTERN LISTO";
        statusText.text = "> Sistema listo. Pulsa EMPEZAR.";
        GameAudio.Play(Sfx.BootReady);
        startBtn.gameObject.SetActive(true);
        yield return Fade(startGroup, 1f, 0.5f);
    }

    // Una curiosidad cada FactSeconds (con fade), hasta que pulses EMPEZAR.
    IEnumerator FactLoop()
    {
        int i = UnityEngine.Random.Range(0, Facts.Length);
        while (!started)
        {
            factText.text = "<color=#7fc4ff>¿SABÍAS QUE?</color>\n" + Facts[i % Facts.Length] +
                            "\n<color=#7f8fa0>Datos reales: NASA (DONKI, Curiosity RAD, OSDR) y publicaciones científicas.</color>";
            i++;
            yield return Fade(factGroup, 1f, 0.4f);
            yield return new WaitForSecondsRealtime(FactSeconds);
            yield return Fade(factGroup, 0f, 0.4f);
        }
    }

    void SetProgress(float p)
    {
        bar.Set(p, p >= 1f ? UIKit.Amber : UIKit.Phosphor);
        pctText.text = "CARGANDO  " + Mathf.RoundToInt(p * 100f) + "%";
    }

    void OnStart()
    {
        if (started) return;
        started = true;
        startBtn.interactable = false;
        GameAudio.Play(Sfx.BootStart);
        StartCoroutine(Finish());
    }

    IEnumerator Finish()
    {
        yield return Fade(rootGroup, 0f, 0.7f);
        if (onDone != null) onDone();
    }

    static IEnumerator Fade(CanvasGroup g, float to, float dur)
    {
        float from = g.alpha, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        g.alpha = to;
    }

    // Logo por código: estrella polar de 4 puntas + puntas diagonales + anillo.
    static Sprite MakeLogo(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float au = Mathf.Abs(u) / 0.86f, av = Mathf.Abs(v) / 0.86f;
                bool star = Mathf.Sqrt(au) + Mathf.Sqrt(av) < 1f;
                float a = (u + v) * 0.7071f, b = (u - v) * 0.7071f;
                bool diag = Mathf.Sqrt(Mathf.Abs(a) / 0.5f) + Mathf.Sqrt(Mathf.Abs(b) / 0.5f) < 1f;
                bool ring = Mathf.Abs(r - 0.95f) < 0.035f;
                px[y * n + x] = (star || diag || ring) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}