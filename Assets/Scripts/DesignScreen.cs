using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Preparativos de la misión (el menú): eliges el NIVEL, DÓNDE está la base (Marte o la Luna) y con qué suministros llega.
// Casi todo son dibujos: planetas, estrellas, medidores que se llenan, iconos y puntitos. Muy poco texto.
public class DesignScreen
{
    static readonly string[] PackTipsKids =
    {
        "Equilibrado: todo parejo. ¡Ideal para empezar!",
        "Blindado: mucho escudo, pero menos batería y comida.",
        "Energético: mucha batería, pero poco escudo."
    };

    static readonly string[] PackTips =
    {
        "Equilibrado: todo parejo. Es el mejor para aprender.",
        "Blindado: escudo fuerte, pero menos batería y comida. ¡Enciende el invernadero pronto!",
        "Energético: mucha batería, pero poca comida y casi sin escudo. ¡Una tormenta temprana sería peligrosa!"
    };

    static readonly string[] SiteTipsKids =
    {
        "Marte: hay polvo y mucho frío. La Tierra tarda en contestar.",
        "La Luna: no hay aire que frene la radiación. ¡El escudo es clave!"
    };

    static readonly string[] PackIcons = { "ring", "shield", "bolt" };
    static readonly Color[] PackCols =
    {
        new Color(0.40f, 1f, 0.50f, 1f), new Color(0.40f, 0.75f, 1f, 1f), new Color(1f, 0.85f, 0.25f, 1f)
    };
    static readonly string[] TileNames = { "BATERÍA", "AIRE", "COMIDA", "CASA", "ESCUDO" };
    static readonly string[] TileIcons = { "battery", "air", "food", "house", "shield" };
    static readonly int[] TileRes = { 0, 1, 2, 3, 4 };           // índice del recurso en Pack.start
    static readonly Color[] PlanetCols = { new Color(0.86f, 0.40f, 0.22f, 1f), new Color(0.74f, 0.76f, 0.80f, 1f) };

    readonly RectTransform root;
    readonly Action onLaunch, onToggleMode;
    readonly Action<int> onSetDifficulty;
    readonly bool guided;
    readonly List<Image> cardBg = new List<Image>();
    readonly List<GameObject> cardBorder = new List<GameObject>();
    readonly List<RectTransform> cardRt = new List<RectTransform>();
    readonly List<List<IconGauge>> cardGauges = new List<List<IconGauge>>();
    readonly List<Image> siteBg = new List<Image>();
    readonly List<GameObject> siteBorder = new List<GameObject>();
    readonly List<RectTransform> planetRt = new List<RectTransform>();
    RectTransform chipRow;
    Text briefing, note;
    Typewriter briefType;
    FxLayer fxl;

    public DesignScreen(RectTransform parent, Action launch, Action toggleMode, Action<int> setDifficulty)
    {
        onLaunch = launch;
        onToggleMode = toggleMode;
        onSetDifficulty = setDifficulty;
        guided = Session.tutorial;
        root = UIKit.Root(parent, "DesignScreen");
        Build();
        Refresh(false);
        Say(Session.Kids
            ? "¡Hola, comandante! Soy la Ing. Freeman. Elige dónde estará tu base."
            : "¡Hola, comandante! Soy la Ing. Freeman. Elige dónde ponemos la base: Marte o la Luna. Las tormentas del Sol son REALES, de la NASA.");
    }

    public void Destroy() { UnityEngine.Object.Destroy(root.gameObject); }

    void Say(string text)
    {
        if (guided) briefType = Typewriter.Show(briefing, "<color=" + UIKit.RadioNameColor + ">" + UIKit.RadioName + ":</color> ", text);
        else { if (briefType != null) briefType.Stop(); briefing.text = text; }
    }

    // Rectángulo invisible (solo sirve para agrupar cosas).
    static RectTransform Zone(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var rt = UIKit.Panel(parent, name, x0, y0, x1, y1, new Color(0f, 0f, 0f, 0f));
        rt.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    static void ShiftText(Button b, float left)
    {
        var t = b.GetComponentInChildren<Text>();
        if (t == null) return;
        var r = t.rectTransform;
        r.offsetMin = new Vector2(left, r.offsetMin.y);
    }

    void Build()
    {
        var d = Session.Diff;
        UIKit.Panel(root, "Bg", 0f, 0f, 1f, 1f, new Color(0.02f, 0.03f, 0.04f, 0.94f));

        // ---- barra de arriba: nombre del juego, 3 niveles y la guía
        var logo = Gfx.Icon(root, "astro", 0.015f, 0.925f, 0.06f, 0.995f, UIKit.Amber);
        var lp = logo.gameObject.AddComponent<Pulser>();
        lp.onColor = UIKit.Amber; lp.amount = 0.06f; lp.speed = 2f;
        UIKit.Label(root, Tuning.GameName, 30, TextAnchor.MiddleLeft, UIKit.Amber, 0.065f, 0.925f, 0.30f, 0.995f);

        for (int i = 0; i < Tuning.Difficulties.Length; i++)
        {
            int idx = i;
            var dd = Tuning.Difficulties[i];
            bool sel = i == Session.difficulty;
            float x0 = 0.31f + i * 0.148f, x1 = x0 + 0.14f;
            var b = UIKit.MakeButton(root, "", 12, x0, 0.93f, x1, 0.995f, sel ? UIKit.Selected : UIKit.Dark,
                                     delegate { if (idx != Session.difficulty && onSetDifficulty != null) onSetDifficulty(idx); },
                                     TextAnchor.MiddleCenter, Sfx.BtnMode);
            var brt = (RectTransform)b.transform;
            if (sel) UIKit.Border(brt, UIKit.Amber, 3f);
            UIKit.Label(brt, dd.name, 14, TextAnchor.MiddleLeft, sel ? UIKit.Amber : UIKit.Phosphor, 0.03f, 0.48f, 1f, 1f);
            for (int k = 0; k <= i; k++) Gfx.Dot(brt, "star4", 0.09f + k * 0.10f, 0.27f, 15f, sel ? UIKit.Amber : new Color(0.6f, 0.65f, 0.55f, 1f));
            UIKit.Label(brt, dd.ages, 11, TextAnchor.MiddleRight, new Color(0.6f, 0.75f, 0.65f, 1f), 0.38f, 0f, 1f, 0.48f);
        }
        var gb = UIKit.MakeButton(root, guided ? "CON GUÍA" : "SIN GUÍA", 15, 0.77f, 0.93f, 0.985f, 0.995f,
                                  guided ? UIKit.Selected : UIKit.Dark, delegate { if (onToggleMode != null) onToggleMode(); },
                                  TextAnchor.MiddleCenter, Sfx.BtnMode);
        ShiftText(gb, 30f);
        Gfx.IconLeft(gb.transform, "dish", 28f, 8f, guided ? UIKit.RadioEdge : new Color(0.45f, 0.5f, 0.5f, 1f));

        // ---- mensaje de la Ing. Freeman
        var mp = UIKit.Panel(root, "Radio", 0.02f, 0.845f, 0.98f, 0.915f, guided ? UIKit.RadioBg : UIKit.Bezel);
        mp.GetComponent<Image>().raycastTarget = false;
        if (guided) UIKit.Border(mp, UIKit.RadioEdge, 3f);
        Gfx.IconLeft(mp, "dish", 36f, 8f, guided ? UIKit.RadioEdge : new Color(0.5f, 0.6f, 0.55f, 1f));
        briefing = UIKit.Label(mp, "", guided ? UIKit.RadioFont - 3 : 16, TextAnchor.MiddleLeft,
                               guided ? UIKit.RadioText : UIKit.Phosphor, 0.045f, 0f, 1f, 1f);

        // ---- fichas de resumen (noches, tareas, peligros)
        chipRow = Zone(root, "Chips", 0.02f, 0.775f, 0.98f, 0.835f);

        // ---- 1. DÓNDE
        UIKit.Label(root, "1  ¿DÓNDE?", 15, TextAnchor.MiddleLeft, UIKit.Amber, 0.02f, 0.742f, 0.6f, 0.775f);
        for (int i = 0; i < NasaData.Sites.Length; i++)
        {
            int idx = i;
            float x0 = 0.02f + i * 0.49f, x1 = x0 + 0.47f;
            var st = NasaData.Sites[i];
            var btn = UIKit.MakeButton(root, "", 14, x0, 0.445f, x1, 0.74f, UIKit.Dark, delegate { PickSite(idx); },
                                       TextAnchor.MiddleCenter, Sfx.DesignSelect);
            var rt = (RectTransform)btn.transform;
            siteBg.Add(rt.GetComponent<Image>());
            siteBorder.Add(UIKit.Border(rt, UIKit.Amber, 3f));

            // el planeta: círculo con cráteres que flota suavemente, y su órbita
            Gfx.Icon(rt, "ring", 0.01f, 0.02f, 0.31f, 0.98f, new Color(1f, 1f, 1f, 0.22f));
            Color pc = PlanetCols[Mathf.Min(i, PlanetCols.Length - 1)];
            var planet = Gfx.Icon(rt, "circle", 0.05f, 0.14f, 0.27f, 0.86f, pc);
            var pp = planet.gameObject.AddComponent<Pulser>();
            pp.onColor = pc; pp.amount = 0.03f; pp.speed = 1.3f;
            var prt = (RectTransform)planet.transform;
            planetRt.Add(prt);
            Color dark = new Color(pc.r * 0.70f, pc.g * 0.70f, pc.b * 0.70f, 1f);
            Gfx.Dot(prt, "circle", 0.30f, 0.66f, 26f, dark);
            Gfx.Dot(prt, "circle", 0.64f, 0.40f, 38f, dark);
            Gfx.Dot(prt, "circle", 0.42f, 0.18f, 18f, dark);
            Gfx.Dot(prt, "circle", 0.70f, 0.78f, 15f, dark);
            Gfx.Dot(prt, "circle", 0.22f, 0.36f, 13f, dark);

            UIKit.Label(rt, st.name, 26, TextAnchor.MiddleLeft, UIKit.Amber, 0.33f, 0.76f, 0.98f, 0.98f);
            var rows = new List<object[]>();
            rows.Add(new object[] { "star4", "RADIACIÓN", Mathf.Clamp(Mathf.RoundToInt(st.dose * 3f), 1, 5), new Color(1f, 0.45f, 0.30f, 1f) });
            rows.Add(new object[] { "panel", "SOL", Mathf.Clamp(Mathf.RoundToInt(st.solarMult * 3f), 1, 5), new Color(1f, 0.88f, 0.30f, 1f) });
            if (d.panels) rows.Add(new object[] { "dust", "POLVO", Mathf.Clamp(Mathf.RoundToInt(st.dust * 2f + st.panelDecayMult * 1.5f), 1, 5), new Color(0.85f, 0.62f, 0.35f, 1f) });
            rows.Add(new object[] { "dish", "TARDA", Mathf.Clamp(st.delayDays * 2, 1, 5), new Color(0.40f, 0.75f, 1f, 1f) });
            float rh = 0.185f;
            for (int r = 0; r < rows.Count; r++)
            {
                float y1 = 0.745f - r * rh;
                StatRow(rt, (string)rows[r][0], (string)rows[r][1], (int)rows[r][2], (Color)rows[r][3], 0.33f, y1 - rh + 0.01f, 0.98f, y1 - 0.01f);
            }
        }

        // ---- 2. QUÉ SUMINISTROS
        UIKit.Label(root, "2  ¿QUÉ LLEVAS?", 15, TextAnchor.MiddleLeft, UIKit.Amber, 0.02f, 0.41f, 0.6f, 0.443f);
        int nTiles = d.house ? 5 : 4;
        int[] tileIdx = d.house ? new int[] { 0, 1, 2, 3, 4 } : new int[] { 0, 1, 2, 4 };
        for (int i = 0; i < Packs.All.Length; i++)
        {
            int idx = i;
            float x0 = 0.02f + i * 0.33f, x1 = x0 + 0.31f;
            var p = Packs.All[i];
            var btn = UIKit.MakeButton(root, "", 14, x0, 0.125f, x1, 0.405f, UIKit.Dark, delegate { Pick(idx); },
                                       TextAnchor.MiddleCenter, Sfx.DesignSelect);
            var rt = (RectTransform)btn.transform;
            cardBg.Add(rt.GetComponent<Image>());
            cardBorder.Add(UIKit.Border(rt, UIKit.Amber, 3f));
            cardRt.Add(rt);
            Gfx.Icon(rt, PackIcons[i], 0.03f, 0.80f, 0.13f, 0.97f, PackCols[i]);
            UIKit.Label(rt, p.name, 19, TextAnchor.MiddleLeft, UIKit.Amber, 0.15f, 0.80f, 0.98f, 0.98f);
            var list = new List<IconGauge>();
            float tw = 0.96f / nTiles;
            for (int k = 0; k < nTiles; k++)
            {
                int ti = tileIdx[k];
                float tx0 = 0.02f + k * tw;
                var g = IconGauge.Create(rt, TileNames[ti], TileIcons[ti], tx0 + 0.004f, 0.03f, tx0 + tw - 0.004f, 0.78f, false);
                g.Init(p.start[TileRes[ti]] / 100f, true);
                list.Add(g);
            }
            cardGauges.Add(list);
        }

        note = UIKit.Label(root, "", 11, TextAnchor.MiddleLeft, new Color(0.5f, 0.75f, 1f, 0.9f), 0.72f, 0.015f, 0.98f, 0.115f);

        var db = UIKit.MakeButton(root, "DATOS NASA", 16, 0.02f, 0.025f, 0.26f, 0.115f, new Color(0.05f, 0.14f, 0.34f, 1f), ShowDatos,
                                  TextAnchor.MiddleCenter, null);
        ShiftText(db, 36f);
        Gfx.IconLeft(db.transform, "dish", 32f, 10f, UIKit.RadioEdge);
        var lb = UIKit.MakeButton(root, "INICIAR MISIÓN", 24, 0.30f, 0.025f, 0.70f, 0.115f, UIKit.Selected,
                                  delegate { if (onLaunch != null) onLaunch(); }, TextAnchor.MiddleCenter, Sfx.LaunchConfirm);
        ShiftText(lb, 44f);
        UIKit.Border((RectTransform)lb.transform, UIKit.Phosphor, 3f);
        var fl = Gfx.IconLeft(lb.transform, "flame", 40f, 12f, UIKit.Amber);
        var fp = fl.gameObject.AddComponent<Pulser>();
        fp.onColor = UIKit.Amber; fp.amount = 0.18f; fp.speed = 6f;
        RefreshDataStatus();

        // ---- efectos (estrellitas que titilan y chispas al elegir)
        fxl = FxLayer.Create(root);
        fxl.SetAmbient(Ambient.Calm);
    }

    // Una fila: icono + palabra + 5 puntitos (los encendidos dicen "cuánto").
    static void StatRow(RectTransform parent, string icon, string label, int pips, Color c, float x0, float y0, float x1, float y1)
    {
        var row = Zone(parent, "Row", x0, y0, x1, y1);
        Gfx.IconLeft(row, icon, 26f, 2f, c);
        UIKit.Label(row, label, 13, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.11f, 0f, 0.54f, 1f);
        for (int k = 0; k < 5; k++) Gfx.Dot(row, "circle", 0.60f + k * 0.082f, 0.5f, 15f, k < pips ? c : Gfx.Dim);
    }

    // Ficha pequeña: icono + una o dos palabras.
    void Chip(string icon, string text, Color c, float x0, float x1)
    {
        var p = UIKit.Panel(chipRow, "Chip", x0, 0.04f, x1, 0.96f, new Color(0.07f, 0.09f, 0.08f, 1f));
        p.GetComponent<Image>().raycastTarget = false;
        UIKit.Border(p, new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 1f), 2f);
        Gfx.IconLeft(p, icon, 28f, 6f, c);
        UIKit.Label(p, text, 13, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.26f, 0f, 1f, 1f);
    }

    void BuildChips()
    {
        for (int i = chipRow.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(chipRow.GetChild(i).gameObject);
        var s = Session.Site;
        var d = Session.Diff;
        var chips = new List<object[]>();
        chips.Add(new object[] { "moon", d.days + " NOCHES", new Color(1f, 0.92f, 0.60f, 1f) });
        chips.Add(new object[] { "bolt", (Tuning.TasksBase + d.bonusTasks) + " TAREAS", new Color(1f, 0.85f, 0.25f, 1f) });
        chips.Add(new object[] { "astro", "4 ASTRONAUTAS", new Color(0.75f, 0.9f, 1f, 1f) });
        chips.Add(new object[] { "flame", "TORMENTAS", new Color(1f, 0.45f, 0.30f, 1f) });
        if (d.panels && s.dust > 0) chips.Add(new object[] { "dust", "POLVO", new Color(0.85f, 0.62f, 0.35f, 1f) });
        if (d.fails > 0) chips.Add(new object[] { "wrench", "ROTURAS", new Color(1f, 0.62f, 0.22f, 1f) });
        if (d.hazards) chips.Add(new object[] { "circle", "ROCAS", new Color(0.7f, 0.72f, 0.76f, 1f) });
        if (d.heater) chips.Add(new object[] { "star4", "FRÍO", new Color(0.55f, 0.85f, 1f, 1f) });
        int n = chips.Count;
        float gap = 0.006f, w = (1f - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
            Chip((string)chips[i][0], (string)chips[i][1], (Color)chips[i][2], i * (w + gap), i * (w + gap) + w);
    }

    // El estado de la descarga de DONKI puede cambiar mientras estás en esta pantalla.
    public void RefreshDataStatus()
    {
        if (note == null) return;
        note.text = NasaData.StatusLine();
    }

    void ShowDatos()
    {
        var m = UIKit.Panel(root, "Datos", 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.75f));
        NasaPanel.Fill(m, delegate { UnityEngine.Object.Destroy(m.gameObject); });
    }

    void Pick(int i)
    {
        Session.pack = i;
        Refresh(true);
        if (i >= 0 && i < cardRt.Count)
        {
            FxLayer.Burst(cardRt[i], PackCols[i], 14, 220f, 12f, "mix");
            FxLayer.Ring(cardRt[i], PackCols[i], 300f, 0.8f);
            // los medidores de ese paquete se vuelven a llenar
            var p = Packs.All[i];
            var d = Session.Diff;
            int[] tileIdx = d.house ? new int[] { 0, 1, 2, 3, 4 } : new int[] { 0, 1, 2, 4 };
            for (int k = 0; k < cardGauges[i].Count; k++) cardGauges[i][k].Init(p.start[TileRes[tileIdx[k]]] / 100f, true);
        }
    }

    void PickSite(int i)
    {
        Session.site = i;
        Refresh(false);
        if (i >= 0 && i < planetRt.Count)
        {
            Color pc = PlanetCols[Mathf.Min(i, PlanetCols.Length - 1)];
            FxLayer.Burst(planetRt[i], pc, 14, 220f, 12f, "mix");
            FxLayer.Ring(planetRt[i], UIKit.Amber, 260f, 0.9f);
        }
        Say(Session.Kids ? SiteTipsKids[Mathf.Clamp(i, 0, SiteTipsKids.Length - 1)] : Session.Site.tip);
    }

    void Refresh(bool speak)
    {
        for (int i = 0; i < cardBg.Count; i++)
        {
            bool on = i == Session.pack;
            cardBg[i].color = on ? UIKit.Selected : UIKit.Dark;
            cardBorder[i].SetActive(on);
        }
        for (int i = 0; i < siteBg.Count; i++)
        {
            bool on = i == Session.site;
            siteBg[i].color = on ? UIKit.Selected : UIKit.Dark;
            siteBorder[i].SetActive(on);
        }
        BuildChips();
        var d = Session.Diff;
        if (speak) Say((d.kids ? PackTipsKids : PackTips)[Mathf.Clamp(Session.pack, 0, PackTips.Length - 1)]);
    }
}
