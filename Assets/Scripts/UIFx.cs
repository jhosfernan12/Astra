using UnityEngine;
using UnityEngine.UI;

// Efectos de pantalla: sacudida, destello, parpadeo de apagón y líneas CRT.
// Todo es opcional y se puede apagar desde el menú de pausa (por si alguien es sensible a los destellos).
public class UIFx : MonoBehaviour
{
    public static UIFx I;
    public static bool Enabled = true;      // sacudidas, destellos y parpadeos
    public static bool Scanlines = true;    // líneas de barrido CRT

    RectTransform content, roll;
    Image flash, black, red;
    RawImage scan;
    float shakeT, shakeDur, shakeAmp, flashA, flickerT, redA, redTarget;
    int lastH = -1;

    public static UIFx Create(RectTransform canvas, RectTransform content)
    {
        var go = new GameObject("UIFx", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        UIKit.Stretch((RectTransform)go.transform);
        var fx = go.AddComponent<UIFx>();
        fx.Build(content);
        I = fx;
        return fx;
    }

    // ---------------------------------------------------------------- API estática (null-safe)
    public static void Shake(float amplitudePx, float seconds)
    {
        if (I == null || !Enabled) return;
        I.shakeAmp = amplitudePx; I.shakeDur = seconds; I.shakeT = seconds;
    }

    public static void Flash(Color c, float peakAlpha)
    {
        if (I == null || !Enabled) return;
        I.flashA = peakAlpha;
        I.flash.color = new Color(c.r, c.g, c.b, peakAlpha);
    }

    public static void Flicker(float seconds)
    {
        if (I == null || !Enabled) return;
        I.flickerT = seconds;
    }

    // Cuando se pierde todo: la pantalla se tiñe de rojo y late despacio (se queda hasta salir de la pantalla).
    public static void RedOut(float alpha)
    {
        if (I == null) return;
        I.redTarget = Mathf.Clamp01(alpha);
        Flicker(1.2f);
        Shake(14f, 0.8f);
    }

    public static void ClearRed() { if (I != null) I.redTarget = 0f; }

    // ---------------------------------------------------------------- construcción
    void Build(RectTransform c)
    {
        content = c;
        var rt = (RectTransform)transform;

        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels32(new Color32[] { new Color32(0, 0, 0, 70), new Color32(0, 0, 0, 70), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0) });
        tex.Apply();
        var sgo = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
        sgo.transform.SetParent(rt, false);
        UIKit.Stretch((RectTransform)sgo.transform);
        scan = sgo.GetComponent<RawImage>();
        scan.texture = tex; scan.raycastTarget = false;

        roll = Overlay(rt, "Roll", new Color(1f, 1f, 1f, 0.035f));
        red = Overlay(rt, "RedOut", new Color(0.85f, 0.03f, 0.03f, 0f)).GetComponent<Image>();
        flash = Overlay(rt, "Flash", new Color(1f, 0.2f, 0.1f, 0f)).GetComponent<Image>();
        black = Overlay(rt, "Black", new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
    }

    static RectTransform Overlay(RectTransform parent, string name, Color c)
    {
        var p = UIKit.Panel(parent, name, 0f, 0f, 1f, 1f, c);
        p.GetComponent<Image>().raycastTarget = false;
        return p;
    }

    // ---------------------------------------------------------------- animación
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        scan.enabled = Scanlines;
        roll.gameObject.SetActive(Scanlines);
        if (Scanlines)
        {
            if (Screen.height != lastH) { lastH = Screen.height; scan.uvRect = new Rect(0f, 0f, 1f, lastH / 4f); }
            float y = Mathf.Repeat(Time.unscaledTime * 0.08f, 1.3f) - 0.15f;
            roll.anchorMin = new Vector2(0f, y);
            roll.anchorMax = new Vector2(1f, y + 0.12f);
        }

        if (shakeT > 0f && Enabled)
        {
            shakeT -= dt;
            float k = Mathf.Clamp01(shakeT / Mathf.Max(0.01f, shakeDur));
            content.anchoredPosition = Random.insideUnitCircle * shakeAmp * k;
        }
        else content.anchoredPosition = Vector2.zero;

        if (flashA > 0f)
        {
            flashA = Mathf.MoveTowards(flashA, 0f, dt * 1.4f);
            var col = flash.color; col.a = flashA; flash.color = col;
        }

        // tinte rojo de "todo perdido": sube despacio y late (el latido se apaga si desactivas los efectos)
        redA = Mathf.MoveTowards(redA, redTarget, dt * 0.7f);
        float pulse = (Enabled && redA > 0.01f) ? 0.08f * Mathf.Sin(Time.unscaledTime * 3f) : 0f;
        var rc = red.color; rc.a = Mathf.Clamp01(redA + pulse); red.color = rc;

        var bc = black.color;
        if (flickerT > 0f && Enabled)
        {
            flickerT -= dt;
            bc.a = Random.value > 0.45f ? Random.Range(0.35f, 0.9f) : 0f;
        }
        else bc.a = 0f;
        black.color = bc;
    }
}
