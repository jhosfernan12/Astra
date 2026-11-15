using System;
using UnityEngine;
using UnityEngine.UI;

// Menú de pausa (tecla ESC): volúmenes, tamaño de texto, efectos, reiniciar y créditos.
public class PauseMenu : MonoBehaviour
{
    // EDITA AQUÍ los créditos de tu equipo.
    public static string Credits =
        "<color=#ffc033>ARES-1: UN PUESTO EN MARTE O EN LA LUNA</color>\n" +
        "NASA Space Apps Challenge 2026 - Space Mission Design Game\n\n" +
        "<color=#ffc033>EQUIPO</color>\n" +
        "(escribe aquí los nombres del equipo)\n\n" +
        "<color=#ffc033>HERRAMIENTAS Y RECURSOS</color>\n" +
        "Motor: Unity\n" +
        "Render PSX: Haunted PSX Render Pipeline (pastasfuture)\n" +
        "Audio: Lofi Horror Menu Sounds\n" +
        "Curiosidades y datos reales: NASA / JPL\n\n" +
        "<color=#ffc033>DATOS REALES USADOS</color>\n" +
        "Llamaradas solares: NASA DONKI (ccmc.gsfc.nasa.gov/tools/DONKI)\n" +
        "Radiación en Marte: Curiosity RAD (NASA/JPL, SwRI)\n" +
        "Radiación en la Luna: Chang'e-4 LND (Zhang y col., Science Advances, 2020)\n" +
        "Salud espacial: NASA OSDR (osdr.nasa.gov), solo como referencia\n" +
        "Detalle y glosario: botón DATOS NASA\n";

    RectTransform canvas;
    GameObject panel, creditsPanel;
    Action onRestart;
    Text fxLabel, crtLabel;
    float prevScale = 1f;
    bool open;

    public static PauseMenu Create(RectTransform canvasRect, Action restart)
    {
        var go = new GameObject("PauseMenu", typeof(RectTransform));
        go.transform.SetParent(canvasRect, false);
        UIKit.Stretch((RectTransform)go.transform);
        var pm = go.AddComponent<PauseMenu>();
        pm.canvas = canvasRect;
        pm.onRestart = restart;
        pm.Build();
        return pm;
    }

    void Build()
    {
        var a = GameAudio.I;
        if (a != null)
        {
            if (PlayerPrefs.HasKey("quipu_master")) a.masterVolume = PlayerPrefs.GetFloat("quipu_master");
            if (PlayerPrefs.HasKey("quipu_music")) a.musicVolume = PlayerPrefs.GetFloat("quipu_music");
            if (PlayerPrefs.HasKey("quipu_sfx")) a.sfxVolume = PlayerPrefs.GetFloat("quipu_sfx");
        }

        var rt = (RectTransform)transform;
        var dim = UIKit.Panel(rt, "Dim", 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.78f));
        panel = dim.gameObject;
        var box = UIKit.Panel(dim, "Box", 0.29f, 0.05f, 0.71f, 0.95f, UIKit.Bezel);
        UIKit.Border(box, UIKit.Amber, 3f);
        UIKit.Label(box, "PAUSA", 32, TextAnchor.MiddleCenter, UIKit.Amber, 0f, 0.92f, 1f, 1f);

        SliderRow(box, "VOLUMEN GENERAL", 0.90f, 0f, 1f, a != null ? a.masterVolume : 1f, delegate (float v)
        { if (GameAudio.I != null) GameAudio.I.masterVolume = v; PlayerPrefs.SetFloat("quipu_master", v); });
        SliderRow(box, "MÚSICA Y AMBIENTE", 0.82f, 0f, 1f, a != null ? a.musicVolume : 1f, delegate (float v)
        { if (GameAudio.I != null) GameAudio.I.musicVolume = v; PlayerPrefs.SetFloat("quipu_music", v); });
        SliderRow(box, "EFECTOS DE SONIDO", 0.74f, 0f, 1f, a != null ? a.sfxVolume : 1f, delegate (float v)
        { if (GameAudio.I != null) GameAudio.I.sfxVolume = v; PlayerPrefs.SetFloat("quipu_sfx", v); });
        SliderRow(box, "TAMAÑO DE TEXTO", 0.66f, UIKit.MinScale, UIKit.MaxScale, UIKit.Scale, delegate (float v)
        { UIKit.TextScale = v; UIKit.RescaleAll(canvas, panel.transform); });

        var b1 = UIKit.MakeButton(box, "", 15, 0.06f, 0.46f, 0.94f, 0.54f, UIKit.Dark, delegate
        { UIFx.Enabled = !UIFx.Enabled; RefreshToggles(); });
        fxLabel = b1.GetComponentInChildren<Text>();
        var b2 = UIKit.MakeButton(box, "", 15, 0.06f, 0.36f, 0.94f, 0.44f, UIKit.Dark, delegate
        { UIFx.Scanlines = !UIFx.Scanlines; RefreshToggles(); });
        crtLabel = b2.GetComponentInChildren<Text>();
        RefreshToggles();

        UIKit.MakeButton(box, "REANUDAR", 20, 0.06f, 0.24f, 0.94f, 0.33f, UIKit.RadioButton, Close);
        UIKit.MakeButton(box, "REINICIAR PARTIDA", 16, 0.06f, 0.14f, 0.94f, 0.22f, UIKit.Dark, delegate
        { Close(); if (onRestart != null) onRestart(); });
#if UNITY_WEBGL
        UIKit.MakeButton(box, "CRÉDITOS", 15, 0.06f, 0.04f, 0.48f, 0.12f, UIKit.Dark, ShowCredits);
        UIKit.MakeButton(box, "DATOS NASA", 15, 0.52f, 0.04f, 0.94f, 0.12f, new Color(0.05f, 0.14f, 0.34f, 1f), ShowDatos);
#else
        UIKit.MakeButton(box, "CRÉDITOS", 14, 0.06f, 0.04f, 0.34f, 0.12f, UIKit.Dark, ShowCredits);
        UIKit.MakeButton(box, "DATOS NASA", 14, 0.36f, 0.04f, 0.64f, 0.12f, new Color(0.05f, 0.14f, 0.34f, 1f), ShowDatos);
        UIKit.MakeButton(box, "SALIR", 14, 0.66f, 0.04f, 0.94f, 0.12f, new Color(0.3f, 0.08f, 0.08f, 1f), delegate
        { Time.timeScale = 1f; Application.Quit(); });
#endif

        // Créditos
        var cp = UIKit.Panel(dim, "Credits", 0.29f, 0.05f, 0.71f, 0.95f, UIKit.Bezel);
        creditsPanel = cp.gameObject;
        UIKit.Border(cp, UIKit.Amber, 3f);
        UIKit.Label(cp, "CRÉDITOS", 28, TextAnchor.MiddleCenter, UIKit.Amber, 0f, 0.90f, 1f, 1f);
        UIKit.ScrollText(cp, Credits, 16, UIKit.Phosphor, 0.05f, 0.14f, 0.95f, 0.90f);
        UIKit.MakeButton(cp, "VOLVER", 18, 0.30f, 0.03f, 0.70f, 0.11f, UIKit.RadioButton, delegate { creditsPanel.SetActive(false); });
        creditsPanel.SetActive(false);

        panel.SetActive(false);
    }

    void SliderRow(RectTransform parent, string label, float yTop, float min, float max, float value, Action<float> onChange)
    {
        UIKit.Label(parent, label, 14, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.03f, yTop - 0.06f, 0.48f, yTop);
        UIKit.MakeSlider(parent, 0.50f, yTop - 0.05f, 0.95f, yTop - 0.01f, min, max, value, onChange);
    }

    void RefreshToggles()
    {
        if (fxLabel != null) fxLabel.text = "SACUDIDAS Y DESTELLOS:  " + (UIFx.Enabled ? "SÍ" : "NO");
        if (crtLabel != null) crtLabel.text = "LÍNEAS CRT:  " + (UIFx.Scanlines ? "SÍ" : "NO");
    }

    void ShowCredits() { creditsPanel.SetActive(true); }

    void ShowDatos()
    {
        var m = UIKit.Panel((RectTransform)panel.transform, "Datos", 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.85f));
        NasaPanel.Fill(m, delegate { UnityEngine.Object.Destroy(m.gameObject); });
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (open) Close(); else Open();
        }
    }

    void Open()
    {
        open = true;
        prevScale = Time.timeScale;
        Time.timeScale = 0f;
        creditsPanel.SetActive(false);
        panel.SetActive(true);
        transform.SetAsLastSibling();
    }

    void Close()
    {
        open = false;
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;
        panel.SetActive(false);
        PlayerPrefs.Save();
    }

    void OnDestroy() { if (open) Time.timeScale = 1f; }
}
