using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Escribe un texto letra por letra (estilo Undertale) y reproduce un "blip" por letra.
// El sonido lo asignas tú en GameAudio > Typewriter Sound.
public class Typewriter : MonoBehaviour
{
    public static float CharsPerSecond = 38f;   // velocidad de escritura (sube el número para escribir más rápido)
    public static int BlipEvery = 2;            // un blip cada N letras

    Text target;
    string prefix, body;
    int total, shown, blipCount;
    float t, pause;
    bool done = true;
    bool fitWanted;   // el texto se ajusta solo a su caja: se calcula el tamaño con el texto completo para que no salte al escribir
    readonly List<int> cut = new List<int>();   // cut[i] = cuántos caracteres crudos mostrar para i letras visibles

    public bool Done { get { return done; } }

    public static Typewriter Show(Text text, string prefix, string body)
    {
        var tw = text.GetComponent<Typewriter>();
        if (tw == null) tw = text.gameObject.AddComponent<Typewriter>();
        tw.Begin(text, prefix, body);
        return tw;
    }

    void Begin(Text text, string pre, string b)
    {
        target = text; prefix = pre ?? ""; body = b ?? "";
        cut.Clear(); cut.Add(0);
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] == '<')
            {
                int close = body.IndexOf('>', i);
                if (close > i && i + 1 < body.Length && (char.IsLetter(body[i + 1]) || body[i + 1] == '/')) { i = close; continue; }
            }
            cut.Add(i + 1);
        }
        total = cut.Count - 1;
        shown = 0; blipCount = 0; t = 0f; pause = 0f;
        done = false;
        if (target.resizeTextForBestFit) fitWanted = true;
        if (fitWanted) FitToFullText();
        target.text = prefix;
    }

    // Calcula el tamaño que tendrá el texto COMPLETO y lo fija mientras se escribe (si no, la letra cambiaría de tamaño a cada paso).
    void FitToFullText()
    {
        target.resizeTextForBestFit = true;
        Vector2 size = target.rectTransform.rect.size;
        if (size.x < 2f || size.y < 2f) return;
        var gen = target.cachedTextGenerator;
        gen.Populate(prefix + body, target.GetGenerationSettings(size));
        int used = gen.fontSizeUsedForBestFit;
        if (used > 0) { target.resizeTextForBestFit = false; target.fontSize = used; }
    }

    void RestoreFit() { if (fitWanted && target != null) target.resizeTextForBestFit = true; }

    // Termina de escribir al instante.
    public void Complete()
    {
        if (target == null) return;
        done = true;
        target.text = prefix + body;
        RestoreFit();
    }

    // Deja de escribir (el texto actual se queda como está).
    public void Stop() { done = true; RestoreFit(); }

    void Update()
    {
        if (done || target == null) return;
        if (pause > 0f) { pause -= Time.unscaledDeltaTime; return; }

        t += Time.unscaledDeltaTime * CharsPerSecond;
        int want = Mathf.Min(total, Mathf.FloorToInt(t));
        while (shown < want)
        {
            shown++;
            char c = CharAtVisible(shown - 1);
            if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
            {
                blipCount++;
                if (blipCount % Mathf.Max(1, BlipEvery) == 0) GameAudio.Blip();
            }
            if (c == '.' || c == '!' || c == '?') pause = 0.18f;
            else if (c == ',' || c == ':' || c == ';') pause = 0.08f;
            if (pause > 0f) { t = shown; break; }
        }

        if (shown >= total) { done = true; target.text = prefix + body; RestoreFit(); }
        else target.text = prefix + body.Substring(0, cut[shown]);
    }

    char CharAtVisible(int visibleIndex) { return body[cut[visibleIndex + 1] - 1]; }
}