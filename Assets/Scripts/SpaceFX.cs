using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Efectos de batería (se crean desde ShiftScreen):
//   1) Viñeta roja que late en los bordes cuando la batería está baja (bajo 40 %).
//   2) Apagón al llegar a 0: la pantalla parpadea, se apaga, aparece una cinemática roja y al final el resumen.
public class SpaceFX : MonoBehaviour
{
    static Texture2D texVig;

    RectTransform self;
    RawImage vig, blackImg, cineVig;
    CanvasGroup cine;
    RawImage[] bars;
    Text cineText;
    IconGauge cineBat;
    float pct = 1f, vigV, barTimer;
    bool dead;

    public static SpaceFX Create(RectTransform shift)
    {
        var go = new GameObject("BatteryFX", typeof(RectTransform));
        go.transform.SetParent(shift, false);
        UIKit.Stretch((RectTransform)go.transform);
        var fx = go.AddComponent<SpaceFX>();
        fx.Build();
        return fx;
    }

    static void InitTexture()
    {
        if (texVig != null) return;
        int n = 128;
        texVig = new Texture2D(n, n, TextureFormat.RGBA32, false);
        texVig.wrapMode = TextureWrapMode.Clamp;
        texVig.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float rr = Mathf.Sqrt(u * u + v * v) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, rr));
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        texVig.SetPixels32(px);
        texVig.Apply();
    }

    static RawImage Img(Transform parent, string name, Texture2D tex, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var ri = go.GetComponent<RawImage>();
        ri.texture = tex;           // null = blanco liso
        ri.color = c;
        ri.raycastTarget = false;
        var rt = ri.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(10f, 10f);
        return ri;
    }

    static void SetA(RawImage r, float a) { var c = r.color; c.a = a; r.color = c; }

    void Build()
    {
        InitTexture();
        self = (RectTransform)transform;

        // viñeta roja de batería baja
        vig = Img(transform, "LowBatteryVignette", texVig, new Color(0.95f, 0.04f, 0.04f, 0f));
        UIKit.Stretch(vig.rectTransform);
        vig.gameObject.SetActive(false);

        // pantalla negra del apagón
        blackImg = Img(transform, "Blackout", null, new Color(0f, 0f, 0f, 0f));
        UIKit.Stretch(blackImg.rectTransform);
        blackImg.gameObject.SetActive(false);

        // cinemática roja
        var cgo = new GameObject("Cinematic", typeof(RectTransform), typeof(CanvasGroup));
        cgo.transform.SetParent(transform, false);
        UIKit.Stretch((RectTransform)cgo.transform);
        cine = cgo.GetComponent<CanvasGroup>();
        cine.blocksRaycasts = false; cine.interactable = false; cine.alpha = 0f;
        cineVig = Img(cgo.transform, "CineVig", texVig, new Color(0.9f, 0.02f, 0.02f, 0.5f));
        UIKit.Stretch(cineVig.rectTransform);
        bars = new RawImage[18];
        for (int i = 0; i < bars.Length; i++)
            bars[i] = Img(cgo.transform, "Bar", null, new Color(0.9f, 0.05f, 0.05f, 0f));
        cineBat = IconGauge.Create(cgo.transform, "BATERÍA", "battery", 0.40f, 0.50f, 0.60f, 0.92f, true);     // la batería que se vacía
        cineText = UIKit.Label(cgo.transform, "", 28, TextAnchor.MiddleCenter, new Color(0.95f, 0.12f, 0.12f, 1f), 0.08f, 0.06f, 0.92f, 0.46f);
        cineText.verticalOverflow = VerticalWrapMode.Overflow;
        cgo.SetActive(false);
    }

    // ShiftScreen lo llama con el porcentaje de energía (0 a 1).
    public void Tick(float batteryPct) { pct = batteryPct; }

    // Energía a 0: parpadeo, pantalla negra, cinemática roja y al final done() (muestra el resumen). clock = texto como "Día 7".
    public void PlayBlackout(string clock, Action done)
    {
        if (dead) return;
        StartCoroutine(BlackoutRoutine(clock, done));
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime, tm = Time.unscaledTime;
        if (!dead) UpdateVignette(dt, tm);
        UpdateCinematic(dt, tm);
    }

    void UpdateVignette(float dt, float tm)
    {
        float sev = pct < 0.4f ? Mathf.InverseLerp(0.4f, 0.03f, pct) : 0f;
        vigV = Mathf.MoveTowards(vigV, sev, dt * 0.8f);
        bool on = vigV > 0.01f;
        if (vig.gameObject.activeSelf != on) vig.gameObject.SetActive(on);
        if (!on) return;

        float a;
        if (UIFx.Enabled)
        {
            // latido doble ("lub-dub") que se acelera cuanto menos batería queda
            float x = Mathf.Repeat(tm * (1.1f + 2.6f * vigV), 1f);
            float b1 = Mathf.Exp(-Mathf.Pow((x - 0.06f) * 12f, 2f));
            float b2 = 0.7f * Mathf.Exp(-Mathf.Pow((x - 0.30f) * 12f, 2f));
            a = vigV * (0.22f + 0.65f * Mathf.Max(b1, b2));
        }
        else a = vigV * 0.4f;
        SetA(vig, Mathf.Clamp01(a));
    }

    void UpdateCinematic(float dt, float tm)
    {
        if (cine == null || !cine.gameObject.activeSelf) return;
        SetA(cineVig, 0.38f + 0.22f * Mathf.Sin(tm * 2.6f));
        cineText.rectTransform.anchoredPosition = UnityEngine.Random.value < 0.04f ? UnityEngine.Random.insideUnitCircle * 4f : Vector2.zero;

        barTimer -= dt;
        if (barTimer > 0f) return;
        barTimer = 0.06f;
        float w = Mathf.Max(100f, self.rect.width);
        for (int i = 0; i < bars.Length; i++)
        {
            var rt = bars[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w * UnityEngine.Random.Range(0.12f, 0.9f), UnityEngine.Random.Range(1f, 5f));
            SetA(bars[i], UnityEngine.Random.value < 0.5f ? UnityEngine.Random.Range(0.05f, 0.32f) : 0f);
        }
    }

    IEnumerator BlackoutRoutine(string clock, Action done)
    {
        dead = true;
        vig.gameObject.SetActive(false);

        // 1) la luz falla: parpadeos y se apaga todo
        blackImg.gameObject.SetActive(true);
        float t = 0f, next = 0f, dur = UIFx.Enabled ? 1.7f : 0.5f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            if (UIFx.Enabled)
            {
                if (t >= next)
                {
                    next = t + 0.07f;
                    float a = UnityEngine.Random.value < 0.3f + 0.55f * k
                        ? UnityEngine.Random.Range(0.55f, 1f) : UnityEngine.Random.Range(0f, 0.12f);
                    SetA(blackImg, a);
                }
            }
            else SetA(blackImg, k);
            yield return null;
        }
        SetA(blackImg, 1f);

        // 2) silencio en la oscuridad
        yield return new WaitForSecondsRealtime(1.2f);

        // 3) cinemática roja
        cine.gameObject.SetActive(true);
        cine.alpha = 0f;
        if (cineBat != null) cineBat.Animate(0.6f, 0f);          // la batería se vacía en pantalla
        t = 0f;
        while (t < 0.7f) { t += Time.unscaledDeltaTime; cine.alpha = Mathf.Clamp01(t / 0.7f); yield return null; }
        cine.alpha = 1f;

        string[] lines =
        {
            "SEÑAL PERDIDA",
            "ARES-1 YA NO RESPONDE",
            "Último reporte: " + clock,
            "Batería: 0 %",
            "<color=#ff6a6a>MISIÓN PERDIDA</color>"
        };
        string shown = "";
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i] + (i < lines.Length - 1 ? "\n" : "");
            var tw = Typewriter.Show(cineText, shown, line);
            while (!tw.Done) yield return null;
            shown += line;
            if (i == lines.Length - 1)
            {
                GameAudio.Play(Sfx.MissionLost);
                UIFx.Shake(10f, 0.5f);
            }
            yield return new WaitForSecondsRealtime(i == lines.Length - 1 ? 2.2f : 0.55f);
        }

        // 4) aparece el resumen (el negro se queda debajo; solo se apaga el texto)
        if (done != null) done();
        t = 0f;
        while (t < 0.8f) { t += Time.unscaledDeltaTime; cine.alpha = 1f - Mathf.Clamp01(t / 0.8f); yield return null; }
        cine.gameObject.SetActive(false);
    }
}