using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// El tablero del puesto ARES-1: decides, gastas tus tareas de la noche y terminas la noche. Cada noche nueva llega un informe.
public class ShiftScreen : MonoBehaviour
{
    const string DefaultHint = "Elige qué hacer esta noche. Al terminar, pulsa TERMINAR NOCHE.";
    static readonly string[] ResLabels = { "BATERÍA", "AIRE", "COMIDA", "CASA", "ESCUDO", "SALUD", "ÁNIMO", "PANELES" };
    static readonly string[] SysLabels = { "INVERNADERO", "MÁQUINA DE AIRE", "CALEFACCIÓN" };

    Outpost o;
    List<GameEvent> events;
    DifficultyDef diff;
    bool Kids { get { return diff != null && diff.kids; } }
    int nSys = 3;                                                 // máquinas que existen en este nivel (2 o 3)      // EXPLORADOR: menos texto y la antena apunta sola
    Action onFinished;
    int totalDays, daysDone, dayTasks;
    bool guided, busy, finished, endArmed, lowPlayed, critPlayed;
    bool lockSys, lockTasks, lockEnd;
    float lastCoach = -99f;
    float[] proj = new float[8];

    Text dayText, subText, apText, forecastText, alertText, logText, bannerText, moodText, aimText;
    readonly IconGauge[] gauges = new IconGauge[8];                 // los dibujos que se llenan (astronauta, carita, batería...)
    readonly Image[] sysIcons = new Image[3];
    readonly Pulser[] sysPulse = new Pulser[3];
    readonly Image[] taskIcons = new Image[6];
    Pulser[] nightPulse;                                            // línea de noches (arriba)
    Image[] taskDots, bufCells;                                     // rayos de tareas (arriba) y casillas de datos (ENLACE)
    Transform dishTf;
    RingEmitter dishEm;
    FxLayer fxl;
    RectTransform fadeAnchor;                                       // lugar libre de la cortina negra donde sale la luna
    float liveT, liveRefreshT;                                      // reloj de los flujos en vivo
    int liveTier = -1;
    bool flowShown;
    Text machHeader;                                                // "MÁQUINAS" + consumo
    float liveEvtT;                                                 // reloj de los imprevistos EN VIVO
    int tempShown;                                                  // -1 frío, 0 bien, +1 calor (lo que se ve junto al astronauta)
    Image tempIcon; Pulser tempPulse; Text tempLabel, tempCounter;
    RectTransform tempFillRt; Image tempFillImg;
    int tempCShown = -999;
    bool holdFx;                                                    // true mientras el informe de la mañana tapa el tablero
    int prevAp = -1, prevDay = -1, prevPacks;
    static readonly Color[] SiteCols = { new Color(0.86f, 0.40f, 0.22f, 1f), new Color(0.74f, 0.76f, 0.80f, 1f) };
    static readonly string[] ResIcons = { "battery", "air", "food", "house", "shield", "astro", "face", "panel" };
    static readonly string[] TaskIconNames = { "shield", "wrench", "panel", "flask", "moon", "dish" };
    static readonly Color[] TaskCols =
    {
        new Color(0.40f, 0.75f, 1f, 1f), new Color(1f, 0.62f, 0.22f, 1f), new Color(1f, 0.88f, 0.30f, 1f),
        new Color(0.75f, 0.55f, 1f, 1f), new Color(1f, 0.92f, 0.60f, 1f), new Color(0.35f, 0.72f, 1f, 1f)
    };
    static readonly Color[] SysCols = { new Color(0.40f, 1f, 0.50f, 1f), new Color(0.40f, 0.75f, 1f, 1f), new Color(1f, 0.60f, 0.22f, 1f) };
    readonly Button[] onBtn = new Button[3];
    readonly Button[] offBtn = new Button[3];
    readonly GameObject[] onBorder = new GameObject[3];
    readonly GameObject[] offBorder = new GameObject[3];
    readonly Text[] sysTexts = new Text[3];
    readonly RectTransform[] sysRows = new RectTransform[3];
    readonly Button[] taskBtn = new Button[6];
    readonly Text[] taskTexts = new Text[6];
    Button endBtn;
    Text endText;
    RectTransform topBar, leftPanel, rightPanel, taskArea, datosRect, linkPanel;

    // ---- ENLACE con la Tierra: una barra de SUBIDA y otra de DESCARGA (se llenan de abajo hacia arriba)
    UIBar upBar, downBar;
    Text upPct, downPct, upCap, downCap;
    bool upAnim, downAnim;
    static readonly Color UpColor = new Color(0.35f, 0.72f, 1f, 1f);
    static readonly Color DownColor = new Color(1f, 0.76f, 0.2f, 1f);
    GameObject bannerBorder;
    Image bannerBgImage;
    Typewriter bannerType, radioType;
    RectTransform modalRoot;
    CanvasGroup fadeGroup;
    Text fadeText;
    SpaceFX fx;
    readonly List<GameObject> frames = new List<GameObject>();
    readonly List<GameObject> tipFrames = new List<GameObject>();     // marco que señala la tarea recomendada

    List<TutorialStep> steps = new List<TutorialStep>();
    int stepIndex = -1;

    bool TutorialRunning { get { return guided && stepIndex >= 0 && stepIndex < steps.Count; } }

    // ================================================================ inicio
    public void Begin(Action finishedCallback)
    {
        onFinished = finishedCallback;
        diff = Session.Diff;
        totalDays = diff.days;
        guided = Session.tutorial;
        Campaign.Reset();
        Campaign.totalDays = totalDays;
        Campaign.siteName = Session.Site.place;
        o = new Outpost(Session.pack, Campaign.ledger);
        events = EventDirector.Generate(diff, guided, new System.Random(Environment.TickCount));
        o.BeginDay(events);
        dayTasks = o.ap;
        lockSys = lockTasks = lockEnd = guided;

        BuildUI();
        fx = SpaceFX.Create((RectTransform)transform);

        LogEvent("Empieza la misión en " + Session.Site.place + " (suministros " + Packs.All[Session.pack].name + "). Hay que aguantar " + totalDays + " noches.", LogKind.Info);
        AddSample(false);
        GameAudio.Play(Sfx.NightStart);
        GameAudio.SetLoop(Loop.Ambience, true);
        steps = BuildSteps();
        Refresh();
        if (guided) NextStep();
        else Coach("¡Tú mandas, comandante! Mira el pronóstico y usa bien tus tareas.");
    }

    // ================================================================ interfaz
    static void Fit(Text t, int min, int max)
    {
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = UIKit.MinFont;      // el mínimo real es siempre UIKit.MinFont, así nunca se corta el texto
        t.resizeTextMaxSize = UIKit.Size(max);
    }

    // Rectángulo invisible que sirve solo para marcarlo con un recuadro parpadeante.
    static RectTransform Marker(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var rt = UIKit.Panel(parent, name, x0, y0, x1, y1, new Color(0f, 0f, 0f, 0f));
        rt.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    void BuildUI()
    {
        // ---- barra superior
        topBar = UIKit.Panel(transform, "Top", 0.01f, 0.92f, 0.99f, 0.99f, UIKit.Bezel);
        dayText = UIKit.Label(topBar, "", 24, TextAnchor.MiddleLeft, UIKit.Amber, 0.01f, 0f, 0.17f, 1f);
        // una bolita por noche: las que pasaron en verde, la de hoy late
        int nN = Mathf.Max(1, totalDays);
        nightPulse = new Pulser[nN];
        float nSize = Mathf.Clamp(380f / nN - 4f, 8f, 22f);
        for (int i = 0; i < nN; i++)
        {
            var nd = Gfx.Dot(topBar, "circle", 0.18f + (i + 0.5f) * (0.32f / nN), 0.5f, nSize, Gfx.Dim);
            nightPulse[i] = nd.gameObject.AddComponent<Pulser>();
            nightPulse[i].amount = 0.25f; nightPulse[i].speed = 5f;
        }
        // el lugar elegido (Marte o la Luna): planeta dibujado + nombre, y el borde de la barra con su color
        Color sc = SiteCols[Mathf.Clamp(Session.site, 0, SiteCols.Length - 1)];
        UIKit.Border(topBar, new Color(sc.r * 0.8f, sc.g * 0.8f, sc.b * 0.8f, 1f), 3f);
        var planet = Gfx.Dot(topBar, "circle", 0.527f, 0.5f, 40f, sc);
        var ppul = planet.gameObject.AddComponent<Pulser>();
        ppul.onColor = sc; ppul.amount = 0.04f; ppul.speed = 1.5f;
        Color crater = new Color(sc.r * 0.70f, sc.g * 0.70f, sc.b * 0.70f, 1f);
        Gfx.Dot(planet.transform, "circle", 0.32f, 0.66f, 9f, crater);
        Gfx.Dot(planet.transform, "circle", 0.66f, 0.38f, 13f, crater);
        Gfx.Dot(planet.transform, "circle", 0.40f, 0.20f, 6f, crater);
        UIKit.Label(topBar, Session.Site.name, 16, TextAnchor.MiddleLeft, sc, 0.552f, 0f, 0.642f, 1f);
        subText = UIKit.Label(topBar, "", 13, TextAnchor.MiddleCenter, UIKit.Phosphor, 0.645f, 0f, 0.74f, 1f);
        apText = UIKit.Label(topBar, "TAREAS", 13, TextAnchor.MiddleRight, UIKit.Phosphor, 0.745f, 0f, 0.84f, 1f);
        taskDots = new Image[8];
        for (int i = 0; i < 8; i++) taskDots[i] = Gfx.Dot(topBar, "bolt", 0.848f + (i + 0.5f) * 0.0175f, 0.5f, 24f, Gfx.Dim);

        // ---- estado del puesto (izquierda): dibujos que se llenan, con color y porcentaje
        leftPanel = UIKit.Panel(transform, "Left", 0.01f, 0.17f, 0.30f, 0.91f, UIKit.Bezel);
        var dd = Session.Diff;
        nSys = dd.heater ? 3 : 2;
        BuildGauges(dd);
        if (!Kids)
        {
            var site0 = Session.Site;
            UIKit.Label(leftPanel, "<color=#7fc4ff>DATO NASA: aquí hay " + (site0.dose > 1f ? "casi 200" : "casi 100") + " veces más radiación que en la Tierra</color>",
                        11, TextAnchor.LowerLeft, new Color(0.5f, 0.7f, 0.55f, 1f), 0f, 0f, 1f, 0.085f);
        }

        // ---- consola de mando (centro)
        var center = UIKit.Panel(transform, "Center", 0.305f, 0.17f, 0.655f, 0.91f, UIKit.Bezel);
        machHeader = UIKit.Label(center, "MÁQUINAS  (gastan batería)", 13, TextAnchor.MiddleLeft,
                    new Color(0.5f, 0.7f, 0.55f, 1f), 0f, 0.935f, 1f, 1f);
        float sysStep = Kids ? 0.11f : 0.082f;
        for (int s = 0; s < nSys; s++)
        {
            int ss = s;
            float y1 = 0.935f - s * sysStep, y0 = y1 - (sysStep - 0.004f);
            sysRows[s] = Marker(center, "SysRow", 0.005f, y0, 0.995f, y1);
            sysTexts[s] = UIKit.Label(center, "", Kids ? 17 : 14, TextAnchor.MiddleLeft, UIKit.Phosphor, Kids ? 0.14f : 0.11f, y0, 0.52f, y1);
            string[] sIc = { "food", "air", "flame" };
            sysIcons[s] = Gfx.IconLeft(sysRows[s], sIc[s], Kids ? 44f : 34f, 6f, SysCols[s]);
            sysPulse[s] = sysIcons[s].gameObject.AddComponent<Pulser>();
            sysPulse[s].onColor = SysCols[s];
            sysPulse[s].amount = 0.10f; sysPulse[s].speed = 4f;
            onBtn[s] = UIKit.MakeButton(center, "ON", 17, 0.53f, y0 + 0.005f, 0.75f, y1 - 0.005f, UIKit.Dark,
                                        delegate { OnToggle(ss, true); }, TextAnchor.MiddleCenter, null);
            offBtn[s] = UIKit.MakeButton(center, "OFF", 17, 0.77f, y0 + 0.005f, 0.99f, y1 - 0.005f, UIKit.Dark,
                                         delegate { OnToggle(ss, false); }, TextAnchor.MiddleCenter, null);
            onBorder[s] = UIKit.Border((RectTransform)onBtn[s].transform, UIKit.Phosphor, 3f);
            offBorder[s] = UIKit.Border((RectTransform)offBtn[s].transform, UIKit.Red, 3f);
            onBorder[s].SetActive(false);
            offBorder[s].SetActive(false);
        }

        UIKit.Label(center, "TAREAS", 13, TextAnchor.MiddleLeft,
                    new Color(0.5f, 0.7f, 0.55f, 1f), 0f, 0.63f, 0.20f, 0.69f);
        moodText = UIKit.Label(center, "", 13, TextAnchor.MiddleRight, Gfx.Good, 0.18f, 0.63f, 1f, 0.69f);
        taskArea = Marker(center, "TaskArea", 0f, 0.105f, 1f, 0.625f);
        // EXPLORADOR tiene solo 4 tareas (escudo, experimento, descansar, antena); los otros niveles tienen las 6.
        int[] visTasks = Kids ? new int[] { 0, 3, 4, 5 } : new int[] { 0, 1, 2, 3, 4, 5 };
        float taskStep = 0.52f / visTasks.Length;
        int taskFont = Kids ? 17 : 15;
        for (int n = 0; n < visTasks.Length; n++)
        {
            int t = visTasks[n];
            int tt = t;
            float y1 = 0.625f - n * taskStep, y0 = y1 - (taskStep - 0.0047f);
            taskBtn[t] = UIKit.MakeButton(center, "", taskFont, 0.01f, y0, 0.99f, y1, UIKit.Dark,
                                          delegate { OnTask((TaskId)tt); }, TextAnchor.MiddleLeft, null);
            taskTexts[t] = taskBtn[t].GetComponentInChildren<Text>();
            Fit(taskTexts[t], 8, taskFont);
            float iconSz = Mathf.Min(54f, taskStep * 0.74f * 720f * 0.82f);
            taskIcons[t] = Gfx.IconLeft(taskBtn[t].transform, TaskIconNames[t], iconSz, 8f, TaskCols[t]);
            var trt = taskTexts[t].rectTransform;
            trt.offsetMin = new Vector2(iconSz + 16f, trt.offsetMin.y);
        }
        endBtn = UIKit.MakeButton(center, "TERMINAR NOCHE", 20, 0.01f, 0.015f, 0.99f, 0.095f, new Color(0.40f, 0.26f, 0.03f, 1f),
                                  OnEndPressed, TextAnchor.MiddleCenter, Sfx.BtnOverlay);
        endText = endBtn.GetComponentInChildren<Text>();
        Fit(endText, 9, 20);

        // ---- pronóstico, alertas y diario (derecha)
        rightPanel = UIKit.Panel(transform, "Right", 0.66f, 0.17f, 0.89f, 0.91f, UIKit.Bezel);
        UIKit.Label(rightPanel, "PRONÓSTICO", 16, TextAnchor.UpperLeft, UIKit.Amber, 0f, 0.94f, 1f, 1f);
        forecastText = UIKit.Label(rightPanel, "", 14, TextAnchor.UpperLeft, UIKit.Phosphor, 0f, 0.64f, 1f, 0.94f);
        UIKit.Label(rightPanel, "AVISOS", 16, TextAnchor.UpperLeft, UIKit.Amber, 0f, 0.58f, 1f, 0.64f);
        alertText = UIKit.Label(rightPanel, "", 14, TextAnchor.UpperLeft, UIKit.Red, 0f, 0.34f, 1f, 0.58f);
        UIKit.Label(rightPanel, "LO QUE PASÓ", 16, TextAnchor.UpperLeft, UIKit.Amber, 0f, 0.28f, 1f, 0.34f);
        logText = UIKit.Label(rightPanel, "", 13, TextAnchor.LowerLeft, UIKit.Phosphor, 0f, 0f, 1f, 0.28f);
        BuildLinkPanel();

        // ---- barra de abajo: manual + mensajes de la Ing. Freeman
        var bottom = UIKit.Panel(transform, "Bottom", 0.01f, 0.02f, 0.99f, 0.155f, UIKit.Bezel);
        UIKit.MakeButton(bottom, "MANUAL", 16, 0.005f, 0.1f, 0.10f, 0.9f, UIKit.Dark, ToggleManual,
                         TextAnchor.MiddleCenter, Sfx.ManualOpen);
        var datosBtn = UIKit.MakeButton(bottom, "DATOS NASA", 16, 0.105f, 0.1f, 0.215f, 0.9f, new Color(0.05f, 0.14f, 0.34f, 1f), ShowDatos,
                                        TextAnchor.MiddleCenter, null);
        datosRect = (RectTransform)datosBtn.transform;
        Fit(datosBtn.GetComponentInChildren<Text>(), 7, 16);
        var bannerBg = UIKit.Panel(bottom, "BannerBg", 0.222f, 0.05f, 0.995f, 0.95f, new Color(0f, 0f, 0f, 0f));
        bannerBgImage = bannerBg.GetComponent<Image>();
        bannerBorder = UIKit.Border(bannerBg, UIKit.RadioEdge, 2f);
        bannerBorder.SetActive(false);
        bannerText = UIKit.Label(bannerBg, DefaultHint, 15, TextAnchor.MiddleLeft, UIKit.Amber, 0f, 0f, 1f, 1f);

        // ---- cortina negra para pasar de una noche a otra
        var fp = UIKit.Panel(transform, "Fade", 0f, 0f, 1f, 1f, Color.black);
        fadeGroup = fp.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeText = UIKit.Label(fp, "", 34, TextAnchor.MiddleCenter, UIKit.Amber, 0f, 0.20f, 1f, 0.34f);
        fadeAnchor = UIKit.Panel(fp, "MoonAnchor", 0.45f, 0.55f, 0.55f, 0.70f, new Color(0f, 0f, 0f, 0f));
        fadeAnchor.GetComponent<Image>().raycastTarget = false;

        // ---- capa de efectos (encima de todo, no recibe clics)
        fxl = FxLayer.Create((RectTransform)transform);
        fxl.starTint = new Color(Mathf.Lerp(0.85f, sc.r, 0.5f), Mathf.Lerp(0.95f, sc.g, 0.5f), Mathf.Lerp(1f, sc.b, 0.5f), 0.35f);
    }

    // Los medidores con dibujo: astronauta (salud), carita (ánimo) y baldosas con batería, aire, comida, escudo, casa y paneles.
    void BuildGauges(DifficultyDef dd)
    {
        float bot = 0.50f, top = 0.985f;
        if (dd.morale)
        {
            gauges[Outpost.H] = IconGauge.Create(leftPanel, ResLabels[Outpost.H], "astro", 0.02f, bot, 0.50f, top, true);
            gauges[Outpost.M] = IconGauge.Create(leftPanel, ResLabels[Outpost.M], "face", 0.50f, bot, 0.98f, top, true);
        }
        else gauges[Outpost.H] = IconGauge.Create(leftPanel, ResLabels[Outpost.H], "astro", 0.22f, bot, 0.78f, top, true);

        if (gauges[Outpost.H] != null && dd.heater)
        {
            var ht = gauges[Outpost.H].transform;
            tempIcon = Gfx.Dot(ht, "snow", 0.86f, 0.62f, 54f, Color.white);
            tempPulse = tempIcon.gameObject.AddComponent<Pulser>(); tempPulse.speed = 4f; tempPulse.amount = 0.12f;
            tempLabel = UIKit.Label(ht, "", 12, TextAnchor.MiddleCenter, Color.white, 0.70f, 0.30f, 1.0f, 0.46f);
            // termómetro: barra que se rellena (azul = frío, verde = normal, roja = calor) con los grados al lado
            var tbg = UIKit.Panel(ht, "TempBar", 0.025f, 0.20f, 0.085f, 0.88f, new Color(0.02f, 0.04f, 0.04f, 0.95f));
            tbg.GetComponent<Image>().raycastTarget = false;
            UIKit.Border(tbg, new Color(0.40f, 0.52f, 0.46f, 1f), 1.5f);
            tempFillRt = UIKit.Panel(tbg, "TempFill", 0.14f, 0.02f, 0.86f, 0.50f, Gfx.Good);
            tempFillRt.GetComponent<Image>().raycastTarget = false;
            tempFillImg = tempFillRt.GetComponent<Image>();
            tempCounter = UIKit.Label(ht, "", 13, TextAnchor.MiddleLeft, Color.white, 0.10f, 0.40f, 0.42f, 0.52f);
            tempIcon.gameObject.SetActive(false); tempLabel.gameObject.SetActive(false);
        }

        var tiles = new List<int>();
        tiles.Add(Outpost.E); tiles.Add(Outpost.O); tiles.Add(Outpost.F); tiles.Add(Outpost.B);
        if (dd.house) tiles.Add(Outpost.I);
        if (dd.panels) tiles.Add(Outpost.P);
        int cols = tiles.Count > 4 ? 3 : 2;
        int rows = (tiles.Count + cols - 1) / cols;
        float areaTop = bot - 0.012f, areaBot = Kids ? 0.015f : 0.09f;
        float cw = 0.96f / cols, ch = (areaTop - areaBot) / rows;
        for (int k = 0; k < tiles.Count; k++)
        {
            int i = tiles[k];
            int c = k % cols, r = k / cols;
            float x0 = 0.02f + c * cw, y1 = areaTop - r * ch;
            gauges[i] = IconGauge.Create(leftPanel, ResLabels[i], ResIcons[i], x0 + 0.006f, y1 - ch + 0.008f, x0 + cw - 0.006f, y1 - 0.008f, false);
        }
    }

    Transform Hero() { return gauges[Outpost.H] != null ? (Transform)gauges[Outpost.H].transform : (Transform)leftPanel; }
    Transform Tgt(int i) { return gauges[i] != null ? (Transform)gauges[i].transform : Hero(); }

    // ================================================================ mensajes de la Ing. Freeman
    // typed = true: la Ing. Freeman habla (recuadro azul y texto letra por letra). false: aviso simple.
    void SetBanner(string text, bool typed)
    {
        bannerBgImage.color = UIKit.RadioBg;
        bannerBorder.SetActive(true);
        bannerText.color = UIKit.RadioText;
        UIKit.SetSize(bannerText, UIKit.RadioFont - 3);
        if (typed) bannerType = Typewriter.Show(bannerText, "<color=" + UIKit.RadioNameColor + ">" + UIKit.RadioName + ":</color> ", text);
        else { if (bannerType != null) bannerType.Stop(); bannerText.text = "<color=" + UIKit.RadioNameColor + ">" + UIKit.RadioName + ":</color> " + text; }
    }

    void ClearBanner()
    {
        if (bannerType != null) bannerType.Stop();
        bannerBgImage.color = new Color(0f, 0f, 0f, 0f);
        bannerBorder.SetActive(false);
        bannerText.color = UIKit.Amber;
        UIKit.SetSize(bannerText, 14);
        bannerText.text = DefaultHint;
    }

    // Consejo de la Ing. Freeman (solo si la dificultad lo permite y el tutorial no está hablando).
    void Coach(string msg)
    {
        if (!diff.hints || TutorialRunning || finished) return;
        SetBanner(msg, true);
        lastCoach = Time.unscaledTime;
    }

    // Aviso corto (por ejemplo "no quedan tareas"). Durante el tutorial no pisa el objetivo.
    void Notice(string msg)
    {
        if (TutorialRunning || string.IsNullOrEmpty(msg)) return;
        SetBanner(msg, false);
    }

    // Consejos cortitos para el nivel EXPLORADOR.
    int SciHave { get { return o.experiments + o.DataPacks; } }

    // ¿Vamos atrasados con la ciencia obligatoria? (mandar los paquetes de la META es necesario para ganar)
    bool SciBehind()
    {
        int miss = diff.goalPacks - SciHave;
        if (miss <= 0 || o.day < 2) return false;
        float pace = diff.goalPacks * (o.day - 1) / (float)totalDays;
        int left = totalDays - o.day + 1;
        return SciHave + 0.5f < pace || left * 2 <= miss + 1;
    }

    string AdviceKids()
    {
        if (o.v[Outpost.O] <= 0.5f) return "¡SIN AIRE! El equipo se enferma rápido. ¡Enciende la máquina de aire!";
        if (diff.morale && o.v[Outpost.M] < Tuning.MoodOk) return "¡Están tristes y se enferman! Usa DESCANSAR ya.";
        for (int sy = 0; sy < 4; sy++)
            if (o.broken[sy]) return "¡Algo se rompió! Usa REPARAR.";
        if (EventDirector.StormTomorrow(events, o.day) && o.v[Outpost.B] < 60f) return "¡Mañana viene una tormenta del Sol! Usa REFORZAR ESCUDO.";
        bool nightNow = EventDirector.Has(events, o.day, EvType.Night);
        if (o.v[Outpost.P] < 45f) return "Los paneles están sucios. Usa LIMPIAR PANELES.";
        if (o.v[Outpost.E] < 30f) return "Queda poca batería. Apaga lo que no uses.";
        if (o.v[Outpost.O] < 35f && !o.rc) return "Falta aire. Enciende la máquina de aire.";
        if (o.v[Outpost.F] < 35f && !o.gh) return "Falta comida. Enciende el invernadero.";
        if (o.v[Outpost.H] < 50f || o.v[Outpost.M] < 35f) return "El equipo está cansado. Usa DESCANSAR.";
        if (o.DataPacks >= 2) return "¡Tienes datos! Pulsa ENVIAR DATOS A LA TIERRA.";
        if (SciBehind()) return "¡Faltan datos! Es OBLIGATORIO mandar " + diff.goalPacks + " paquetes. Haz un EXPERIMENTO.";
        return null;
    }

    string Advice()
    {
        if (Kids) return AdviceKids();
        if (o.v[Outpost.O] <= 0.5f) return "¡SIN AIRE! La salud baja " + (Tuning.NoAirHealthPerSec * diff.liveDrain).ToString("0.#") + " por segundo. Enciende o repara la máquina de aire YA.";
        if (diff.morale && o.v[Outpost.M] < Tuning.MoodOk) return "ÁNIMO BAJO: la salud baja cada segundo. Usa DESCANSAR ahora.";
        if (o.HouseBroken) return "LA CASA ESTÁ ROTA: el equipo pierde salud y ánimo sin parar. Usa REPARAR y levanta el escudo.";
        for (int sy = 0; sy < 4; sy++)
            if (o.broken[sy])
                return EventDirector.SystemNames[sy] + " está roto. Usa REPARAR" +
                       (sy == Outpost.SysRc ? ": sin la máquina de aire, el aire se acaba rápido." : ".");
        if (EventDirector.StormTomorrow(events, o.day) && o.v[Outpost.B] < 60f)
            return "¡La próxima noche llega una tormenta del Sol! Tu escudo está en " + Mathf.RoundToInt(o.v[Outpost.B]) + " %. Refuérzalo ya.";
        bool nightNow = EventDirector.Has(events, o.day, EvType.Night);
        string nm = Session.Site.nightName.ToLower();
        if (nightNow && !o.HtOk) return "Es " + Session.Site.nightArt + " " + nm + " y la calefacción no funciona: la casa se está dañando. ¡Enciéndela!";
        if ((nightNow || EventDirector.LongEventSoon(events, o.day, EvType.Night)) && !o.ht)
            return "Se acerca " + Session.Site.nightArt + " " + nm + ": enciende la calefacción antes de que empiece.";
        if ((nightNow || EventDirector.LongEventSoon(events, o.day, EvType.Night)) && o.gh && o.v[Outpost.E] < 75f)
            return "Casi no habrá sol y el invernadero gasta mucha batería. Apágalo mientras dure, si tienes comida.";
        bool dustNow = EventDirector.Has(events, o.day, EvType.Dust);
        if (!dustNow && o.v[Outpost.P] < 45f) return "Los paneles están sucios y dan poca batería. Usa LIMPIAR PANELES.";
        if (o.v[Outpost.E] < 30f) return "Queda poca batería. Apaga lo que no necesites y limpia los paneles.";
        if (o.v[Outpost.O] < 35f && !o.rc) return "El aire se acaba. Enciende la máquina de aire.";
        if (o.v[Outpost.F] < 35f && !o.gh) return "Queda poca comida. Enciende el invernadero.";
        if (o.v[Outpost.H] < 50f) return "El equipo está enfermo. Dales un DESCANSO y sube el escudo.";
        if (diff.morale && o.v[Outpost.M] < 35f) return "El ánimo está bajo y por eso tendrás menos tareas. Deja descansar al equipo.";
        if (o.DataPacks >= Outpost.MaxDataPacks) return "La memoria está llena: abre ANTENA, apunta a la TIERRA y sube los datos.";
        if (diff.dataLoss && EventDirector.StormTomorrow(events, o.day) && o.buffer >= 25f) return "La próxima noche llega una tormenta y borra la mitad de tus datos. ¡Súbelos ya!";
        if (SciBehind()) return "CIENCIA OBLIGATORIA: llevas " + SciHave + " de " + diff.goalPacks + " paquetes. Si no los mandas a la Tierra, la misión FRACASA. Haz experimentos y súbelos.";
        if (o.v[Outpost.E] < 45f && o.ap >= 2) return "La batería baja con cada tarea. DESCANSAR no gasta batería y sube el ánimo.";
        return null;
    }

    // Qué tarea recomendar (para que su botón brille). -1 = ninguna en especial.
    int TipTask()
    {
        for (int sy = 0; sy < 4; sy++) if (o.broken[sy]) return (int)TaskId.Repair;
        if (EventDirector.StormTomorrow(events, o.day) && o.v[Outpost.B] < 60f) return (int)TaskId.Reinforce;
        bool dustNow = EventDirector.Has(events, o.day, EvType.Dust);
        if (!dustNow && o.v[Outpost.P] < 45f) return (int)TaskId.Clean;
        if (o.v[Outpost.H] < 50f || (diff.morale && o.v[Outpost.M] < Tuning.MoodOk + 5f)) return (int)TaskId.Rest;
        if (SciBehind() && o.DataPacks < 2) return (int)TaskId.Experiment;
        if (o.buffer >= 50f || (diff.dataLoss && EventDirector.StormTomorrow(events, o.day) && o.buffer >= 25f)) return (int)TaskId.Call;   // el botón ANTENA
        return -1;
    }

    void UpdateTip()
    {
        foreach (var f in tipFrames) if (f != null) Destroy(f);
        tipFrames.Clear();
        if (diff == null || !diff.hints || TutorialRunning || finished || busy || lockTasks || o.ap <= 0) return;
        int t = TipTask();
        if (t < 0) return;
        if (taskBtn[t] == null) return;
        var cg = UIKit.Frame((RectTransform)taskBtn[t].transform, UIKit.Amber, 3f);
        tipFrames.Add(cg.gameObject);
    }

    // ================================================================ modales (informe, manual, resumen, radio)
    RectTransform OpenModal(float dim)
    {
        CloseModal();
        modalRoot = UIKit.Panel(transform, "Modal", 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, dim));
        if (fxl != null) fxl.transform.SetAsLastSibling();      // los efectos se ven también sobre los informes
        return modalRoot;
    }

    void CloseModal()
    {
        radioType = null;
        if (modalRoot != null) Destroy(modalRoot.gameObject);
        modalRoot = null;
    }

    void ToggleManual()
    {
        if (busy || finished) return;
        if (modalRoot != null) return;
        var m = OpenModal(0.7f);
        var p = UIKit.Panel(m, "Manual", 0.14f, 0.06f, 0.86f, 0.94f, new Color(0.03f, 0.03f, 0.03f, 0.99f));
        UIKit.Border(p, UIKit.Phosphor, 3f);
        UIKit.Label(p, "CÓMO SE JUEGA", 24, TextAnchor.UpperLeft, UIKit.Amber, 0f, 0.92f, 1f, 1f);
        var site = Session.Site;
        var sb = new StringBuilder();
        if (Kids)
        {
            sb.Append("<color=#ffc033>META</color>: cuida a 4 astronautas " + totalDays + " noches en " + site.place + ".\n");
            sb.Append("Si BATERÍA, AIRE o SALUD llegan a 0, pierdes.\n<color=#7fc4ff>META</color>: ¡tienes que mandar " + diff.goalPacks + " paquetes de datos a la Tierra! Si no, pierdes.\n\n");
            sb.Append("<color=#ffc033>MÁQUINAS</color> (encenderlas es gratis, pero cada una gasta " + Tuning.MachineDrainPerSec.ToString("0.#") + " de batería POR SEGUNDO)\n");
            sb.Append("- INVERNADERO: sube la comida " + Tuning.GreenhouseFoodPerSec.ToString("0.#") + " cada segundo.\n- MÁQUINA DE AIRE: da aire.\n\n");
            sb.Append("<color=#ffc033>TAREAS</color> (normalmente " + (Tuning.TasksBase + diff.bonusTasks) + " cada noche)\n");
            sb.Append("- ESCUDO: te protege del Sol.\n");
            sb.Append("- EXPERIMENTO: guarda datos.\n- DESCANSAR: el equipo se recupera y NO gasta batería.\n");
            sb.Append("La CARITA es el ÁNIMO: alegre = más tareas, triste = menos y rinden menos.\nCada tarea gasta batería.\n");
            sb.Append("¡Si usas casi todas tus tareas Y mandas datos a la Tierra esa noche, ganas un BONO! Sin enviar datos, no hay bono.\n");
            sb.Append("Los PANELES cargan la batería poco a poco (más si están limpios) y el POLVO los ensucia. Las máquinas encendidas gastan batería sin parar.\n");
            sb.Append("Si algo falla, el ÁNIMO baja rápido. ¡Con ánimo bajo el equipo pierde salud cada segundo!\n");
            sb.Append("Cada tarea da HAMBRE: deja el INVERNADERO encendido. Sin aire la salud cae rapidísimo. Si la salud llega a 0, el equipo muere.\n");
            sb.Append("- <color=#7fc4ff>ENVIAR DATOS</color>: manda lo guardado a la Tierra.\n\n");
            sb.Append("Mira la barra de SUBIDA: mandar datos demora.\n");
            sb.Append("Un botón que <color=#ffc033>brilla</color> es lo que te recomienda la Ing. Freeman.\n");
        }
        else
        {
        var md = diff;
        sb.Append("<color=#ffc033>META</color>: cuida a 4 astronautas durante " + totalDays + " noches en " + site.place + ".\n");
        sb.Append("Pierdes si la BATERÍA, el AIRE, la SALUD" + (md.house ? " o la CASA" : "") + " llegan a 0.\n\n");
        sb.Append("<color=#ffc033>META OBLIGATORIA</color>: manda " + md.goalPacks + " paquetes de datos a la Tierra (haciendo experimentos) y sobrevive. ¡Sin ciencia la misión fracasa!\n\n");
        sb.Append("<color=#ffc033>CADA NOCHE</color> tienes " + (Tuning.TasksBase + md.bonusTasks) + " tareas. ÁNIMO alto: +1 tarea y todo rinde más. ÁNIMO bajo: menos tareas y rinden menos. DESCANSAR y las llamadas a la familia lo suben.\n");
        sb.Append("Cada tarea GASTA BATERÍA (DESCANSAR no): los " + (md.panels ? "PANELES la recargan, ¡límpialos!" : "paneles la recargan.") + "\n");
        sb.Append("Si usas casi todas tus tareas Y envías datos a la Tierra esa noche ganas un <color=#ffc033>BONO DE LA NOCHE</color>. Sin transmisión no hay bono.\n");
        if (md.heater) sb.Append("<color=#7fc4ff>FRÍO Y CALOR</color>: sin CALEFACCIÓN en la noche larga el equipo se CONGELA (copo de nieve junto al astronauta). Las TORMENTAS SOLARES SUBEN la temperatura, y si además la calefacción está encendida sube mucho más y sienten CALOR (llama). Mira el contador TEMP junto al astronauta. Con mucho frío o mucho calor bajan la salud y el ánimo.\n");
        sb.Append("<color=#ff6a5a>IMPREVISTOS</color>: caen sorpresas mientras juegas (averías, plagas, fiebre...). ¡Ten reservas!\n");
        sb.Append("<color=#ffc033>EN VIVO</color>: los paneles cargan la batería poco a poco (más rápido cuanto más cerca del 100 %), el polvo los ensucia y cada máquina encendida gasta batería cada segundo. El invernadero sube la comida y la máquina de aire sube el aire. Cada cosa que falla baja el ÁNIMO rápido. En la noche larga el FRÍO gasta batería y daña salud y ánimo.\n");
        sb.Append("<color=#ffc033>COMIDA</color>: CADA tarea (también DESCANSAR) da hambre, y un experimento cuesta además salud.\n");
        sb.Append("<color=#ff6a5a>SALUD POR SEGUNDO</color>: ánimo bajo -" + (Tuning.MoodTiredHealthPerSec * md.liveDrain).ToString("0.#") + "/s, sin aire -" + (Tuning.NoAirHealthPerSec * md.liveDrain).ToString("0.#") + "/s" +
                  (md.house ? ", casa rota -" + (Tuning.HouseBrokenHealthPerSec * md.liveDrain).ToString("0.#") + "/s" : "") + ". Si llega a 0, el equipo MUERE.\n");
        if (md.house) sb.Append("<color=#ffc033>ESCUDO</color>: con el escudo roto la casa recibe el DOBLE de daño. Con escudo también recibe daño, pero menos.\n");
        sb.Append("Hay " + md.lifelines + (md.lifelines == 1 ? " cápsula" : " cápsulas") + " de EMERGENCIA: si algo llega a 0, la Tierra te salva una vez.\n\n");
        sb.Append("<color=#ffc033>MÁQUINAS</color> (prender y apagar es gratis, pero CADA UNA gasta " + Tuning.MachineDrainPerSec.ToString("0.#") + " de batería POR SEGUNDO: ¡con varias encendidas se vacía rapidísimo! Si la batería llega a 0 se acaba la misión)\n");
        sb.Append("- INVERNADERO: sube la comida " + Tuning.GreenhouseFoodPerSec.ToString("0.#") + " POR SEGUNDO mientras está encendido.\n- MÁQUINA DE AIRE: el aire baja " + Tuning.AirDrainPerSec.ToString("0.#") + " POR SEGUNDO (respirar) y con la máquina encendida sube " + Tuning.RecyclerAirPerSec.ToString("0.#") + " POR SEGUNDO.\n");
        if (md.panels) sb.Append("<color=#ff9a3c>TORMENTA DE POLVO</color>: los paneles pierden " + Tuning.DustPanelsPerSec.ToString("0.#") + " % POR SEGUNDO y el polvo raspa la casa. ¡Límpialos después!\n");
        if (md.house) sb.Append("<color=#ff9a3c>EVENTOS Y CASA</color>: las tormentas solares, el polvo y la noche larga sin calefacción dañan la casa (el pronóstico te avisa). El escudo reduce ese daño.\n");
        if (md.heater) sb.Append("- CALEFACCIÓN: el frío no daña la casa.\n");
        sb.Append("\n<color=#ffc033>TAREAS</color>\n");
        sb.Append("- REFORZAR ESCUDO: te protege de la radiación del Sol.\n");
        if (md.house) sb.Append("- REPARAR: arregla lo roto.\n");
        if (md.panels) sb.Append("- LIMPIAR PANELES: más batería.\n");
        sb.Append("- EXPERIMENTO: guarda datos en el BUFFER (la memoria).\n");
        sb.Append("- DESCANSAR: sube " + (md.morale ? "el ánimo y " : "") + "la salud.\n");
        sb.Append("- <color=#7fc4ff>ANTENA</color>: apunta, manda datos" + (md.call ? " o llama a la Tierra" : "") + ".\n\n");
        sb.Append("<color=#ffc033>ANTENA Y BUFFER</color>\n");
        sb.Append("1. Haz EXPERIMENTOS con la antena en CIENCIA. Cada uno guarda 1 paquete (caben " + Outpost.MaxDataPacks + ").\n");
        sb.Append("2. Apunta la antena a la TIERRA.\n");
        sb.Append("3. SUBE el buffer: ¡ahora sí cuenta como ciencia!\n");
        sb.Append("A la derecha, la barra de SUBIDA se llena mientras mandas datos (demora)." + (md.call ? " La de DESCARGA sube cada noche mientras esperas la respuesta de la Tierra." : "") + "\n");
        if (md.dataLoss) sb.Append("Ojo: una tormenta del Sol borra la mitad de los datos, y el polvo tapa la señal.\n");
        if (md.call) sb.Append("LLAMAR A LA TIERRA pide ayuda, pero la respuesta tarda " + site.delayDays + (site.delayDays == 1 ? " noche" : " noches") + ".\n");
        sb.Append("\n<color=#ffc033>PELIGROS</color>: tormentas del Sol (¡REALES, de la NASA!)");
        if (md.panels) sb.Append(", polvo");
        if (md.heater) sb.Append(", " + site.nightName.ToLower());
        if (md.house) sb.Append(", roturas");
        if (md.hazards) sb.Append(" y rocas");
        sb.Append(". El PRONÓSTICO avisa de lo que se puede prever.\n\n");
        sb.Append("Un botón que <color=#ffc033>brilla</color> es la tarea que recomienda la Ing. Freeman.\n");
        sb.Append("<color=#7fc4ff>DATOS NASA: qué es real y qué significan las palabras nuevas.</color>");
        }
        UIKit.ScrollText(p, sb.ToString(), 16, UIKit.Phosphor, 0.02f, 0.12f, 0.98f, 0.91f);
        UIKit.MakeButton(p, "CERRAR", 18, 0.35f, 0.02f, 0.65f, 0.10f, UIKit.Selected, delegate { CloseModal(); }, TextAnchor.MiddleCenter, Sfx.ManualClose);
    }

    void ShowRadio(string text)
    {
        var m = OpenModal(0.45f);
        var p = UIKit.Panel(m, "Radio", 0.17f, 0.10f, 0.83f, 0.46f, UIKit.RadioBg);
        UIKit.Border(p, UIKit.RadioEdge, 3f);
        UIKit.Label(p, "<color=" + UIKit.RadioNameColor + ">[ RADIO ]  " + UIKit.RadioName + " - JEFA DE MISIÓN</color>",
                    UIKit.RadioHeaderFont, TextAnchor.UpperLeft, UIKit.RadioText, 0f, 0.86f, 1f, 1f);
        var body = UIKit.Label(p, "", UIKit.RadioFont, TextAnchor.UpperLeft, UIKit.RadioText, 0.02f, 0.27f, 0.98f, 0.86f);
        radioType = Typewriter.Show(body, "", text);
        UIKit.MakeButton(p, "ENTENDIDO", 18, 0.36f, 0.04f, 0.64f, 0.23f, UIKit.RadioButton, OnRadioOk,
                         TextAnchor.MiddleCenter, Sfx.RadioNext);
        GameAudio.Play(Sfx.RadioOpen);
    }

    void OnRadioOk()
    {
        if (radioType != null && !radioType.Done) { radioType.Complete(); return; }   // 1er clic: termina de escribir
        CloseModal();
        NextStep();
    }

    static string EvIcon(EvType t)
    {
        switch (t)
        {
            case EvType.Solar: return "flame";
            case EvType.Dust: return "dust";
            case EvType.Night: return "star4";
            case EvType.Fail: return "wrench";
            case EvType.Meteor: return "circle";
            case EvType.Leak: return "air";
            case EvType.Supply: return "bolt";
            case EvType.Luck: return "star4";
            case EvType.Mishap: return "bolt";
            case EvType.Bonus: return "gift";
            case EvType.News: return "heart";
        }
        return "dish";
    }

    static string NoteIcon(EventNote n) { return string.IsNullOrEmpty(n.icon) ? EvIcon(n.type) : n.icon; }

    // Las novedades buenas (suerte, bono, noticias) van doradas; las malas, rojas.
    static Color NoteColor(EventNote n)
    {
        if (n.type == EvType.Luck || n.type == EvType.News || n.type == EvType.Bonus || n.type == EvType.Mishap)
            return n.kind == LogKind.Bad ? Gfx.Bad : new Color(1f, 0.85f, 0.30f, 1f);
        return EvColor(n.type);
    }

    static Color EvColor(EvType t)
    {
        switch (t)
        {
            case EvType.Solar: return new Color(1f, 0.45f, 0.30f, 1f);
            case EvType.Dust: return new Color(0.85f, 0.62f, 0.35f, 1f);
            case EvType.Night: return new Color(0.55f, 0.85f, 1f, 1f);
            case EvType.Fail: return new Color(1f, 0.62f, 0.22f, 1f);
            case EvType.Meteor: return new Color(0.75f, 0.77f, 0.80f, 1f);
            case EvType.Leak: return new Color(0.40f, 0.75f, 1f, 1f);
            case EvType.Supply: return new Color(0.45f, 1f, 0.55f, 1f);
        }
        return new Color(0.40f, 0.75f, 1f, 1f);
    }

    static RectTransform MZone(Transform parent, float x0, float y0, float x1, float y1, Color c)
    {
        var rt = UIKit.Panel(parent, "Z", x0, y0, x1, y1, c);
        rt.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    // Informe de la mañana: medidores que se llenan o se vacían, novedades con iconos y el pronóstico en tarjetitas.
    void ShowMorning(float[] before, List<EventNote> notes)
    {
        var m = OpenModal(0.72f);
        bool stormNext = EventDirector.StormTomorrow(events, o.day);
        var p = UIKit.Panel(m, "Morning", 0.08f, 0.07f, 0.92f, 0.93f, new Color(0.02f, 0.03f, 0.04f, 0.99f));
        UIKit.Border(p, stormNext ? Gfx.Bad : UIKit.Phosphor, 3f);

        // ---- cabecera: luna, número de noche y una bolita por noche
        var mi = Gfx.Icon(p, "moon", 0.015f, 0.88f, 0.07f, 0.985f, new Color(1f, 0.92f, 0.60f, 1f));
        var mp = mi.gameObject.AddComponent<Pulser>();
        mp.onColor = mi.color; mp.amount = 0.06f; mp.speed = 2f;
        UIKit.Label(p, "NOCHE " + o.day + " / " + totalDays, 32, TextAnchor.MiddleLeft, UIKit.Amber, 0.08f, 0.88f, 0.40f, 0.99f);
        int nNights = Mathf.Max(1, totalDays);
        float dSize = Mathf.Clamp(380f / nNights - 4f, 8f, 22f);
        for (int i = 0; i < nNights; i++)
        {
            bool cur = i + 1 == o.day, past = i + 1 < o.day;
            var dt = Gfx.Dot(p, "circle", 0.42f + (i + 0.5f) * (0.40f / nNights), 0.935f, dSize,
                             cur ? UIKit.Amber : (past ? Gfx.Good : Gfx.Dim));
            if (cur) { var pu = dt.gameObject.AddComponent<Pulser>(); pu.onColor = UIKit.Amber; pu.amount = 0.25f; pu.speed = 5f; }
        }

        // ---- así amaneció la base: cada recurso se llena o se vacía, con su cambio abajo
        int[] order = { 0, 1, 2, 4, 5, 6, 3, 7 };
        var list = new List<int>();
        for (int k = 0; k < order.Length; k++) if (Session.ResShown(order[k])) list.Add(order[k]);
        float tw = 0.96f / list.Count;
        for (int k = 0; k < list.Count; k++)
        {
            int i = list[k];
            float x0 = 0.02f + k * tw;
            var g = IconGauge.Create(p, ResLabels[i], ResIcons[i], x0 + 0.004f, 0.47f, x0 + tw - 0.004f, 0.86f, false);
            g.Animate(before[i] / 100f, o.v[i] / 100f);
            int d = Mathf.RoundToInt(o.v[i]) - Mathf.RoundToInt(before[i]);
            Color dc = d > 0 ? Gfx.Good : (d < 0 ? Gfx.Bad : new Color(0.45f, 0.55f, 0.5f, 1f));
            UIKit.Label(p, d == 0 ? "=" : (d > 0 ? "+" : "") + d, 22, TextAnchor.MiddleCenter, dc, x0, 0.385f, x0 + tw, 0.465f);
        }

        // ---- novedades de la noche (un icono por cosa que pasó)
        var nc = MZone(p, 0.02f, 0.125f, 0.52f, 0.375f, new Color(0.07f, 0.09f, 0.08f, 1f));
        UIKit.Border(nc, new Color(0.22f, 0.30f, 0.25f, 1f), 2f);
        UIKit.Label(nc, "NOVEDADES", 13, TextAnchor.MiddleLeft, UIKit.Amber, 0.02f, 0.83f, 0.98f, 1f);
        if (notes.Count == 0)
        {
            Gfx.Face(nc, 0.9f, 0.04f, 0.14f, 0.18f, 0.80f);
            UIKit.Label(nc, "Una noche tranquila", 16, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.22f, 0.14f, 0.98f, 0.80f);
        }
        var ordered = new List<EventNote>();                 // primero el bono (la recompensa), después lo que pasó, al final lo que "sigue"
        foreach (var n0 in notes) if (n0.type == EvType.Bonus) ordered.Add(n0);
        foreach (var n0 in notes) if (n0.type != EvType.Bonus && !n0.text.Contains(" sigue (noche ")) ordered.Add(n0);
        foreach (var n0 in notes) if (n0.type != EvType.Bonus && n0.text.Contains(" sigue (noche ")) ordered.Add(n0);
        int shownN = Mathf.Min(ordered.Count, 3);
        for (int r = 0; r < shownN; r++)
        {
            var n = ordered[r];
            float y1 = 0.82f - r * 0.27f, y0 = y1 - 0.25f;
            var row = MZone(nc, 0.02f, y0, 0.98f, y1, new Color(0f, 0f, 0f, 0f));
            var nic = Gfx.IconLeft(row, NoteIcon(n), 28f, 2f, NoteColor(n));
            if (n.type == EvType.Bonus || n.type == EvType.News)
            {
                var np = nic.gameObject.AddComponent<Pulser>();
                np.onColor = nic.color; np.amount = 0.14f; np.speed = 5f;
            }
            string txt = n.text;
            if (r == shownN - 1 && ordered.Count > shownN) txt += "  (+" + (ordered.Count - shownN) + " más)";
            UIKit.Label(row, txt, 13, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.09f, 0f, 1f, 1f);
        }

        // ---- pronóstico: una tarjetita por noche que viene
        var fc = MZone(p, 0.53f, 0.125f, 0.98f, 0.375f, new Color(0.07f, 0.09f, 0.08f, 1f));
        UIKit.Border(fc, new Color(0.22f, 0.30f, 0.25f, 1f), 2f);
        UIKit.Label(fc, "PRONÓSTICO", 13, TextAnchor.MiddleLeft, UIKit.Amber, 0.02f, 0.83f, 0.98f, 1f);
        int ahead = ForecastDays();
        float cw = 0.96f / ahead;
        for (int k = 1; k <= ahead; k++)
        {
            int day = o.day + k;
            GameEvent hit = null;
            foreach (var e in events)
            {
                bool match = (e.type == EvType.Solar || e.type == EvType.Supply) ? e.day == day
                           : ((e.type == EvType.Dust || e.type == EvType.Night) && e.ActiveOn(day));
                if (!match) continue;
                if (hit == null || e.type == EvType.Solar || (e.type == EvType.Night && hit.type != EvType.Solar)
                    || (e.type == EvType.Dust && hit.type == EvType.Supply)) hit = e;
            }
            float cx0 = 0.02f + (k - 1) * cw;
            Color cc = hit != null ? EvColor(hit.type) : new Color(0.45f, 0.6f, 0.5f, 1f);
            bool storm = hit != null && hit.type == EvType.Solar;
            var card = MZone(fc, cx0 + 0.006f, 0.06f, cx0 + cw - 0.006f, 0.80f, storm ? new Color(0.28f, 0.07f, 0.06f, 1f) : new Color(0.04f, 0.06f, 0.05f, 1f));
            UIKit.Border(card, new Color(cc.r * 0.7f, cc.g * 0.7f, cc.b * 0.7f, 1f), storm ? 3f : 2f);
            UIKit.Label(card, "NOCHE " + day, 10, TextAnchor.MiddleCenter, new Color(0.62f, 0.76f, 0.70f, 1f), 0f, 0.78f, 1f, 1f);
            string icon = hit != null ? EvIcon(hit.type) : "moon";
            var ic = Gfx.Icon(card, icon, 0.18f, 0.30f, 0.82f, 0.78f, cc);
            if (storm) { var sp = ic.gameObject.AddComponent<Pulser>(); sp.onColor = cc; sp.amount = 0.15f; sp.speed = 6f; }
            string word = "CALMA";
            if (hit != null)
            {
                switch (hit.type)
                {
                    case EvType.Solar: word = hit.flare != null ? hit.flare.Cls : "SOL"; break;
                    case EvType.Dust: word = "POLVO"; break;
                    case EvType.Night: word = "FRÍO"; break;
                    case EvType.Supply: word = "CÁPSULA"; break;
                }
            }
            UIKit.Label(card, word, 11, TextAnchor.MiddleCenter, cc, 0f, 0f, 1f, 0.30f);
        }

        var go = UIKit.MakeButton(p, "EMPEZAR LA NOCHE " + o.day, 22, 0.28f, 0.015f, 0.72f, 0.105f, UIKit.Selected, OnMorningOk,
                                  TextAnchor.MiddleCenter, Sfx.RadioNext);
        var gt = go.GetComponentInChildren<Text>();
        if (gt != null) gt.rectTransform.offsetMin = new Vector2(44f, gt.rectTransform.offsetMin.y);
        UIKit.Border((RectTransform)go.transform, UIKit.Phosphor, 3f);
        var fl = Gfx.IconLeft(go.transform, "flame", 40f, 12f, UIKit.Amber);
        var fpu = fl.gameObject.AddComponent<Pulser>();
        fpu.onColor = UIKit.Amber; fpu.amount = 0.18f; fpu.speed = 6f;
        GameAudio.Play(Sfx.ReviewOpen);
        EventEffects(notes);
    }

    void OnMorningOk()
    {
        CloseModal();
        holdFx = false;                // ahora sí: los medidores muestran lo que cambió
        Refresh();
        if (!TutorialRunning)
        {
            string adv = Advice();
            if (adv != null) Coach(adv); else ClearBanner();
        }
    }

    // Sonido y sacudida del evento más grave de la mañana.
    void EventEffects(List<EventNote> notes)
    {
        bool solar = false, meteor = false, leak = false, fail = false, dust = false, night = false, supply = false, earth = false;
        bool luck = false, mishap = false, bonus = false, newsGood = false, newsBad = false;
        foreach (var n in notes)
        {
            if (n.text.Contains(" sigue (noche ")) continue;     // "sigue": no suena de nuevo
            switch (n.type)
            {
                case EvType.Luck: luck = true; break;
                case EvType.Mishap: mishap = true; break;
                case EvType.Bonus: bonus = true; break;
                case EvType.News: if (n.kind == LogKind.Bad) newsBad = true; else newsGood = true; break;
                case EvType.Solar: solar = true; break;
                case EvType.Meteor: meteor = true; break;
                case EvType.Leak: leak = true; break;
                case EvType.Fail: fail = true; break;
                case EvType.Dust: dust = true; break;
                case EvType.Night: night = true; break;
                case EvType.Supply: supply = true; break;
                case EvType.Earth: earth = true; break;
            }
        }
        if (solar) { GameAudio.Play(Sfx.FlareImpact); UIFx.Shake(12f, 0.7f); UIFx.Flash(new Color(1f, 0.85f, 0.5f), 0.6f); }
        else if (meteor) { GameAudio.Play(Sfx.WheelImpact); UIFx.Shake(10f, 0.6f); }
        else if (leak) { GameAudio.Play(Sfx.AlertWarning); UIFx.Shake(6f, 0.5f); }
        else if (fail) { GameAudio.Play(Sfx.AlertWarning); UIFx.Shake(4f, 0.4f); }
        else if (dust) { GameAudio.Play(Sfx.DustImpact); }
        else if (night) { GameAudio.Play(Sfx.EclipseStart); }
        else if (supply) { GameAudio.Play(Sfx.DataChime); }
        else if (earth) { GameAudio.Play(Sfx.CmdArrive); }
        else if (mishap || newsBad) { GameAudio.Play(Sfx.ResponseBad); }
        else if (newsGood || luck || bonus) { GameAudio.Play(Sfx.ResponseGood); }
        EventVisuals(solar, meteor, leak, fail, supply, earth);
        ExtraVisuals(luck, mishap, bonus, newsGood, newsBad);
    }

    // Efectos de los imprevistos, el bono de la noche y las noticias de la Tierra.
    void ExtraVisuals(bool luck, bool mishap, bool bonus, bool newsGood, bool newsBad)
    {
        Color gold = new Color(1f, 0.85f, 0.30f, 1f);
        if (bonus)
        {
            Transform tg = o.bonusRes >= 0 ? Tgt(o.bonusRes) : Hero();
            FxLayer.Pop(tg, "gift", gold, 40f, 170f, 1.2f, 0f);
            FxLayer.Burst(tg, gold, 18, 220f, 13f, "star4");
            FxLayer.Ring(tg, gold, 220f, 1.0f);
            FxLayer.FloatText(tg, o.bonusRes >= 0 ? "BONO +" + Mathf.RoundToInt(o.bonusAmt) : "+1 TAREA", Gfx.Good, 30);
            if (o.bonusRes >= 0 && gauges[o.bonusRes] != null) gauges[o.bonusRes].Flash(Gfx.Good);
        }
        if (newsGood)
        {
            Transform fc = Tgt(Outpost.M);
            FxLayer.Pop(fc, "heart", Gfx.Good, 40f, 190f, 1.2f, 0f);
            FxLayer.Burst(fc, Gfx.Good, 16, 200f, 13f, "star4");
            FxLayer.FloatText(fc, "¡ÁNIMO x" + Tuning.MoodBoostMult.ToString("0.0#") + "!", Gfx.Good, 28);
            if (gauges[Outpost.M] != null) gauges[Outpost.M].Flash(Gfx.Good);
        }
        if (newsBad)
        {
            Transform fc = Tgt(Outpost.M);
            FxLayer.Pop(fc, "cross", Gfx.Bad, 40f, 150f, 1.0f, 0f);
            FxLayer.Burst(fc, new Color(0.45f, 0.55f, 0.95f, 1f), 10, 120f, 12f, "circle");
            FxLayer.FloatText(fc, "ÁNIMO BAJA", Gfx.Bad, 26);
            if (gauges[Outpost.M] != null) gauges[Outpost.M].Flash(Gfx.Bad);
        }
        if (luck)
        {
            FxLayer.Burst(Hero(), gold, 14, 200f, 13f, "star4");
            FxLayer.Ring(Hero(), gold, 220f, 1.0f);
        }
        if (mishap)
        {
            UIFx.Shake(5f, 0.4f);
            FxLayer.Burst(Hero(), new Color(1f, 0.5f, 0.3f, 1f), 12, 170f, 12f, "star4");
        }
    }

    void EventVisuals(bool solar, bool meteor, bool leak, bool fail, bool supply, bool earth)
    {
        if (solar)
        {
            FxLayer.Storm(3.5f);
            FxLayer.Ring(Hero(), Gfx.Bad, 320f, 1.3f);
            FxLayer.Burst(Hero(), new Color(1f, 0.85f, 0.4f, 1f), 16, 240f, 14f, "star4");
            if (gauges[Outpost.B] != null) gauges[Outpost.B].Flash(Gfx.Mid);
        }
        if (meteor) FxLayer.Meteor(Tgt(Outpost.I));
        if (leak) FxLayer.Burst(Tgt(Outpost.O), Gfx.Blue, 14, 130f, 14f, "circle");
        if (fail) FxLayer.Burst(Tgt(Outpost.E), new Color(1f, 0.6f, 0.2f, 1f), 16, 200f, 12f, "star4");
        if (supply) FxLayer.Burst(Tgt(Outpost.F), new Color(1f, 0.82f, 0.25f, 1f), 16, 180f, 13f, "mix");
        if (earth && dishTf != null)
        {
            FxLayer.Ring(dishTf, DownColor, 150f, 1.2f);
            FxLayer.Fly(dishTf, Hero(), DownColor, 4, 1.0f, 30f);
        }
    }

    // ================================================================ tutorial
    void Highlight(params RectTransform[] targets)
    {
        ClearHighlights();
        foreach (var t in targets)
        {
            if (t == null) continue;
            var cg = UIKit.Frame(t, UIKit.RadioEdge, 4f);
            frames.Add(cg.gameObject);
        }
    }

    void ClearHighlights()
    {
        foreach (var f in frames) if (f != null) Destroy(f);
        frames.Clear();
    }

    void NextStep()
    {
        ClearHighlights();
        stepIndex++;
        if (stepIndex >= steps.Count) { OnTutorialDone(); return; }
        var st = steps[stepIndex];
        if (st.onEnter != null) st.onEnter();
        if (st.pause) { ClearBanner(); ShowRadio(st.Text()); }
        else SetBanner("OBJETIVO: " + st.Text(), true);
        Refresh();
    }

    void OnTutorialDone()
    {
        stepIndex = steps.Count;
        lockSys = lockTasks = lockEnd = false;
        ClearHighlights();
        ClearBanner();
        Refresh();
    }

    // Texto de la META (misión en dos partes) que se repite en los tutoriales.
    string MissionText()
    {
        return "PARA GANAR TIENES QUE HACER 2 COSAS: 1) MANDAR " + diff.goalPacks + " paquetes de datos de ciencia a la Tierra (¡esa es la forma de ganar!) y 2) SOBREVIVIR " + totalDays +
               " noches. ¡Las dos son obligatorias: si no mandas los datos, la misión fracasa aunque todos sobrevivan!";
    }

    string ExperimentCostText()
    {
        return "Un EXPERIMENTO cuesta " + (Tuning.EnExperiment * diff.energyMult).ToString("0") + " de batería, " +
               (Tuning.FoodExperiment * diff.foodMult).ToString("0") + " de comida y " + Tuning.HealthExperiment.ToString("0") + " de salud: ¡no es gratis!";
    }

    // Deja lo necesario para que el jugador pueda completar un paso del tutorial (tareas y batería suficientes) y desbloquea las tareas.
    void TutorialSafety(int needAp)
    {
        lockTasks = false;
        if (o.ap < needAp) o.ap = needAp;
        if (o.v[Outpost.E] < 45f) o.v[Outpost.E] = 45f;
        if (o.v[Outpost.F] < 30f) o.v[Outpost.F] = 30f;
    }

    // Tutorial del nivel EXPLORADOR (la antena apunta sola): misión, DATOS desde la primera noche, máquinas, escudo y tormenta.
    List<TutorialStep> BuildStepsKids()
    {
        var L = new List<TutorialStep>();
        L.Add(TutorialStep.Say("¡Hola! Soy la Ing. Freeman. Cuidas una base en " + Session.Site.place + " con 4 astronautas. ¡Los datos son reales, de la NASA!", null));
        L.Add(TutorialStep.Say(MissionText(), delegate { Highlight((RectTransform)subText.transform); }));
        L.Add(TutorialStep.Say("Mira la META arriba: lleva " + o.experiments + " de " + diff.goalPacks + ". ¡Mandar datos es lo MÁS IMPORTANTE! Vas a aprender a hacerlo ahora mismo, en tu primera noche.",
            delegate { Highlight((RectTransform)subText.transform); }));
        L.Add(TutorialStep.Say("A la IZQUIERDA ves cómo está tu base. Si algo llega a 0, pierdes. La BATERÍA es la más importante: si llega a 0, ¡se acaba la misión!", delegate { Highlight(leftPanel); }));
        L.Add(TutorialStep.Say("La CARITA es el ÁNIMO del equipo. Si está triste, haces menos tareas y rinden menos. ¡DESCANSAR la alegra!", delegate { Highlight(leftPanel); }));
        L.Add(TutorialStep.Say("Aquí están tus TAREAS. Cada noche tienes pocas, así que úsalas bien. Cada tarea gasta batería y da hambre.", delegate { Highlight(topBar, taskArea); }));
        // ---- los DATOS, desde la primera noche
        L.Add(TutorialStep.Say("¡Vamos a ganar puntos de ciencia! Se hace en 2 pasos: 1) un EXPERIMENTO crea un paquete y lo guarda en la memoria de la base. 2) ENVIAR DATOS lo manda a la Tierra. ¡Solo cuenta cuando llega a la Tierra!",
            delegate { lockTasks = false; Highlight((RectTransform)taskBtn[3].transform, (RectTransform)taskBtn[5].transform); }));
        L.Add(TutorialStep.Wait("PASO 1: haz un EXPERIMENTO. " + ExperimentCostText(),
            delegate { return o.buffer > 0f || o.uploaded > 0f; }, delegate { TutorialSafety(2); Highlight((RectTransform)taskBtn[3].transform); }));
        L.Add(TutorialStep.Say("¡Mira! Se encendió una ESTRELLA DORADA abajo en el panel azul: es un paquete guardado en la MEMORIA de la base. Caben " + Outpost.MaxDataPacks + ". ¡Todavía NO cuenta para la META!",
            delegate { Highlight(linkPanel); }));
        L.Add(TutorialStep.Wait("PASO 2: pulsa ENVIAR DATOS A LA TIERRA.",
            delegate { return o.uploaded > 0f; }, delegate { TutorialSafety(1); Highlight((RectTransform)taskBtn[5].transform); }));
        L.Add(TutorialStep.Say("¡Mira la barra de SUBIDA! Mandar datos demora. La NASA también espera: los datos de Marte pasan por satélites antes de llegar a la Tierra.",
            delegate { Highlight(linkPanel); }));
        L.Add(TutorialStep.SayFn(delegate
            {
                return "¡Datos recibidos! La META ya lleva " + o.experiments + " de " + diff.goalPacks + ". Repite EXPERIMENTO y ENVIAR DATOS, noche tras noche, hasta llegar a " + diff.goalPacks + ". ¡Así se gana!";
            }, delegate { Highlight((RectTransform)subText.transform); }));
        // ---- sobrevivir: máquinas
        L.Add(TutorialStep.Say("Ahora hay que SOBREVIVIR. Las MÁQUINAS mantienen vivo al equipo: encenderlas es gratis, pero gastan batería.", delegate { Highlight(sysRows[0], sysRows[1]); }));
        L.Add(TutorialStep.Wait("enciende el INVERNADERO. ¡Sube la comida " + Tuning.GreenhouseFoodPerSec.ToString("0.#") + " cada segundo!",
            delegate { return o.gh; }, delegate { lockSys = false; Highlight(sysRows[0]); }));
        L.Add(TutorialStep.Wait("enciende la MÁQUINA DE AIRE. ¡Sube el aire " + Tuning.RecyclerAirPerSec.ToString("0.#") + " cada segundo!",
            delegate { return o.rc; }, delegate { Highlight(sysRows[1]); }));
        L.Add(TutorialStep.Say("¡Ojo! Cada máquina encendida gasta " + Tuning.MachineDrainPerSec.ToString("0.#") + " de batería CADA SEGUNDO. " +
            "El aire baja " + Tuning.AirDrainPerSec.ToString("0.#") + " por segundo aunque no hagas nada. Enciende solo lo que necesites y vigila el aviso de CONSUMO.",
            delegate { Highlight((RectTransform)machHeader.transform, leftPanel); }));
        L.Add(TutorialStep.Wait("pulsa TERMINAR NOCHE.",
            delegate { return o.day >= 2; }, delegate { lockEnd = false; Highlight((RectTransform)endBtn.transform); }));
        // ---- noche 2: escudo y repetir el ciclo
        L.Add(TutorialStep.Say("¡Nueva noche! El Sol envía radiación. El ESCUDO te protege.", delegate { lockTasks = false; Highlight(taskArea); }));
        L.Add(TutorialStep.Wait("usa REFORZAR ESCUDO.",
            delegate { return o.actsTotal[(int)TaskId.Reinforce] >= 1; }, delegate { TutorialSafety(2); Highlight((RectTransform)taskBtn[0].transform); }));
        L.Add(TutorialStep.Wait("¡Repite el ciclo de datos! Haz otro EXPERIMENTO.",
            delegate { return o.actsTotal[(int)TaskId.Experiment] >= 2; }, delegate { TutorialSafety(1); Highlight((RectTransform)taskBtn[3].transform); }));
        L.Add(TutorialStep.Say("¡Muy bien! Ese paquete queda guardado en la memoria hasta que lo ENVÍES. Puedes juntar varios y mandarlos de una vez. ¡Pero no esperes demasiado!",
            delegate { Highlight(linkPanel); }));
        L.Add(TutorialStep.Wait("termina la noche.",
            delegate { return o.day >= 3; }, delegate { Highlight((RectTransform)endBtn.transform); }));
        // ---- noche 3: tormenta
        L.Add(TutorialStep.SayFn(delegate
            {
                return "¡Mañana viene una TORMENTA DEL SOL! Tu escudo está en " + Mathf.RoundToInt(o.v[Outpost.B]) + " %. ¡Refuérzalo!";
            }, delegate { Highlight(rightPanel); }));
        L.Add(TutorialStep.Wait("refuerza el escudo y termina la noche.",
            delegate { return o.day >= 4; }, delegate { Highlight((RectTransform)taskBtn[0].transform, (RectTransform)endBtn.transform); }));
        L.Add(TutorialStep.SayFn(delegate
            {
                string real = "";
                if (Campaign.hits.Count > 0 && Campaign.hits[0].flare != null)
                    real = " ¡Fue una llamarada REAL de la NASA!";
                return "¡Pasó la tormenta!" + real + " Te quedan " + Mathf.Max(0, diff.goalPacks - o.experiments) + " paquetes por mandar. Recuerda: EXPERIMENTO y ENVIAR DATOS.";
            }, delegate { Highlight((RectTransform)subText.transform); }));
        L.Add(TutorialStep.Say("¡Ya sabes jugar! Un botón que BRILLA es lo que te recomiendo. Recuerda: MANDA DATOS y SOBREVIVE. ¡Suerte!", delegate { ClearHighlights(); }));
        return L;
    }

    // Tutorial de ASTRONAUTA y COMANDANTE: misión, DATOS desde la primera noche (experimento, antena y subir), máquinas, escudo, tormentas y temperatura.
    List<TutorialStep> BuildSteps()
    {
        if (Kids) return BuildStepsKids();
        var L = new List<TutorialStep>();
        L.Add(TutorialStep.Say("¡Hola, comandante! Soy la Ing. Freeman, tu jefa en la Tierra. Cuidas la base " + Tuning.GameName +
            " en " + Session.Site.place + " con 4 astronautas. Aquí no hay monstruos: solo el lugar, la radiación y tus decisiones. " +
            "¡Los datos son reales, de la NASA!", null));
        L.Add(TutorialStep.Say(MissionText(), delegate { Highlight((RectTransform)subText.transform); }));
        L.Add(TutorialStep.Say("Mira la META arriba: lleva " + o.experiments + " de " + diff.goalPacks + " (obligatoria). Sin ciencia enviada, la misión fracasa aunque sobrevivas. " +
            "Por eso vas a aprender a mandar datos AHORA, en tu primera noche.", delegate { Highlight((RectTransform)subText.transform); }));
        L.Add(TutorialStep.Say("A la IZQUIERDA ves cómo está la base. " +
            "Si la batería, el aire, la salud" + (diff.house ? " o la casa" : "") + " llegan a 0, la misión se pierde. ¡Con la batería en 0 hay apagón!", delegate { Highlight(leftPanel); }));
        L.Add(TutorialStep.Say("La CARITA es el ÁNIMO. Con buen ánimo tienes 1 tarea extra y todo rinde más; con mal ánimo, menos tareas y rinden menos. " +
            "DESCANSAR y llamar a la familia lo suben. Con el ánimo muy bajo el equipo pierde salud cada segundo.", delegate { Highlight(leftPanel); }));
        L.Add(TutorialStep.Say("En el CENTRO está tu panel. Cada noche tienes pocas TAREAS (arriba a la derecha). " +
            "Cada tarea cansa al equipo, da HAMBRE y GASTA BATERÍA" + (diff.panels ? ": ¡los paneles solares la recargan!" : ".") + " DESCANSAR no gasta batería.", delegate { Highlight(topBar, taskArea); }));
        // ---- los DATOS, paso a paso, desde la primera noche
        L.Add(TutorialStep.Say("¡Vamos a la CIENCIA! Mandar datos son 3 pasos: 1) EXPERIMENTO: crea un paquete y lo guarda en el BUFFER, la memoria de la base. " +
            "2) ANTENA: apunta a la TIERRA. 3) SUBIR BUFFER: manda los paquetes. Solo cuentan los que LLEGAN a la Tierra." +
            (diff.dataLoss ? " ¡Ojo: una tormenta solar borra la mitad del buffer, y el polvo tapa la señal!" : ""),
            delegate { lockTasks = false; Highlight((RectTransform)taskBtn[3].transform, (RectTransform)taskBtn[5].transform, linkPanel); }));
        L.Add(TutorialStep.Wait("PASO 1: haz un EXPERIMENTO (la antena mira a CIENCIA). " + ExperimentCostText(),
            delegate { return o.buffer > 0f || o.uploaded > 0f; }, delegate { TutorialSafety(3); if (o.aimEarth) o.aimEarth = false; Highlight((RectTransform)taskBtn[3].transform); }));
        L.Add(TutorialStep.Say("¡Mira! Se encendió una ESTRELLA DORADA abajo, en el panel azul de la derecha: es un paquete guardado en el BUFFER. Caben " + Outpost.MaxDataPacks +
            ". Con buen ánimo un experimento puede salir DOBLE. Todavía NO cuenta para la META: hay que mandarlo.",
            delegate { Highlight(linkPanel); }));
        L.Add(TutorialStep.Wait("PASO 2: abre ANTENA y elige APUNTAR A LA TIERRA. (La antena mira a CIENCIA para experimentar y a la TIERRA para mandar datos; cambiarla es una tarea.)",
            delegate { return o.aimEarth; }, delegate { TutorialSafety(2); Highlight((RectTransform)taskBtn[5].transform); }));
        L.Add(TutorialStep.Wait("PASO 3: abre ANTENA otra vez y elige SUBIR BUFFER.",
            delegate { return o.uploaded > 0f; }, delegate { TutorialSafety(1); Highlight((RectTransform)taskBtn[5].transform); }));
        L.Add(TutorialStep.Say("¡Datos enviados! Mandar datos DEMORA: mira la barra de SUBIDA, a la derecha. " +
            "La NASA lo hace igual: el satélite MRO guarda los datos de Curiosity y los manda a la Tierra, a 3 antenas gigantes en California, España y Australia.",
            delegate { Highlight(linkPanel); }));
        L.Add(TutorialStep.SayFn(delegate
            {
                return "¡Datos recibidos en la Tierra! La META ya lleva " + o.experiments + " de " + diff.goalPacks + ". " +
                       "Para ganar repites este ciclo: EXPERIMENTO, apuntar a la TIERRA y SUBIR BUFFER. " +
                       "Para no perder tareas apuntando, junta varios paquetes y súbelos juntos.";
            }, delegate { Highlight((RectTransform)subText.transform); }));
        // ---- sobrevivir: máquinas, consumo y temperatura
        L.Add(TutorialStep.Say("Ahora a SOBREVIVIR. Las MÁQUINAS mantienen vivo al equipo. Encenderlas y apagarlas es gratis, pero gastan batería.", delegate { Highlight(sysRows[0], sysRows[1]); }));
        L.Add(TutorialStep.Wait("enciende el INVERNADERO. Sube la comida " + Tuning.GreenhouseFoodPerSec.ToString("0.#") + " cada segundo.",
            delegate { return o.gh; }, delegate { lockSys = false; Highlight(sysRows[0]); }));
        L.Add(TutorialStep.Wait("enciende la MÁQUINA DE AIRE. El aire baja " + Tuning.AirDrainPerSec.ToString("0.#") + " por segundo; con la máquina sube " + Tuning.RecyclerAirPerSec.ToString("0.#") + " por segundo.",
            delegate { return o.rc; }, delegate { Highlight(sysRows[1]); }));
        if (diff.heater)
            L.Add(TutorialStep.Say("La CALEFACCIÓN: enciéndela cuando llegue " + Session.Site.nightArt + " " + Session.Site.nightName.ToLower() + " (mira el pronóstico). " +
                "Sin ella el equipo se CONGELA y la casa sufre. Junto al astronauta hay un TERMÓMETRO: azul = frío, verde = normal, rojo = calor. " +
                "¡Pero ojo! Con una TORMENTA SOLAR la temperatura sube, y con la calefacción encendida sube mucho más: ¡apágala!",
                delegate { Highlight(sysRows[2], leftPanel); }));
        L.Add(TutorialStep.Say("¡CUIDADO CON LA BATERÍA! Cada máquina encendida gasta " + Tuning.MachineDrainPerSec.ToString("0.#") + " de batería CADA SEGUNDO. " +
            "Arriba de las máquinas verás el CONSUMO: si sale en rojo, ¡es MUY ALTO! Apaga lo que no necesites. " +
            (diff.panels ? "Los paneles solares la recargan, y si se ensucian de polvo cargan menos: usa LIMPIAR PANELES." : "Los paneles solares la recargan.") +
            " Si la batería llega a 0, la misión se acaba.", delegate { Highlight((RectTransform)machHeader.transform, leftPanel); }));
        L.Add(TutorialStep.Say("Los números entre ( ) muestran lo que cambiará esta noche. Mientras lees este tutorial el tiempo está PAUSADO; " +
            "cuando termine, la batería, el aire y la comida cambiarán en vivo, segundo a segundo.", delegate { Highlight(leftPanel); }));
        L.Add(TutorialStep.Wait("pulsa TERMINAR NOCHE para pasar a la noche 2.",
            delegate { return o.day >= 2; }, delegate { lockEnd = false; Highlight((RectTransform)endBtn.transform); }));
        // ---- noche 2: escudo y repetir el ciclo de datos
        L.Add(TutorialStep.Say("Así empieza cada noche: un resumen y tareas nuevas. Ahora toca el ESCUDO: la radiación daña todas las noches " +
            "y el escudo se gasta solo.", delegate { lockTasks = false; Highlight(taskArea); }));
        L.Add(TutorialStep.Wait("usa la tarea REFORZAR ESCUDO.",
            delegate { return o.actsTotal[(int)TaskId.Reinforce] >= 1; }, delegate { TutorialSafety(3); Highlight((RectTransform)taskBtn[0].transform); }));
        L.Add(TutorialStep.Say("¡Ahora repite el ciclo de DATOS! La antena sigue mirando a la TIERRA, y para experimentar tiene que volver a CIENCIA. " +
            "Por eso conviene juntar varios paquetes en el buffer y subirlos juntos: así gastas menos tareas apuntando.",
            delegate { Highlight((RectTransform)taskBtn[5].transform, linkPanel); }));
        L.Add(TutorialStep.Wait("abre ANTENA y elige APUNTAR A CIENCIA.",
            delegate { return !o.aimEarth; }, delegate { TutorialSafety(2); Highlight((RectTransform)taskBtn[5].transform); }));
        L.Add(TutorialStep.Wait("haz otro EXPERIMENTO.",
            delegate { return o.actsTotal[(int)TaskId.Experiment] >= 2; }, delegate { TutorialSafety(1); Highlight((RectTransform)taskBtn[3].transform); }));
        L.Add(TutorialStep.Say("¡Bien! A la derecha está el PRONÓSTICO: avisa de las tormentas con dos noches de margen y de qué dañarán " +
            (diff.house ? "(la casa también sufre con las tormentas y el polvo). " : ". ") +
            "Lo que se rompe llega sin aviso: guarda siempre una tarea de reserva.", delegate { Highlight(rightPanel); }));
        L.Add(TutorialStep.Wait("termina la noche.",
            delegate { return o.day >= 3; }, delegate { Highlight((RectTransform)endBtn.transform); }));
        // ---- noche 3: tormenta solar
        L.Add(TutorialStep.SayFn(delegate
            {
                return "¡Ojo! La próxima noche llega una TORMENTA DEL SOL. Con el escudo alto, el equipo recibe menos radiación. " +
                       "Tu escudo está en " + Mathf.RoundToInt(o.v[Outpost.B]) + " %." +
                       (o.buffer > 0.5f && diff.dataLoss ? " ¡Y tienes datos guardados en el buffer: la tormenta borra la mitad! Mándalos antes si puedes." : "");
            }, delegate { Highlight(rightPanel); }));
        L.Add(TutorialStep.Wait("prepárate: refuerza el escudo y termina la noche.",
            delegate { return o.day >= 4; }, delegate { Highlight((RectTransform)taskBtn[0].transform, (RectTransform)endBtn.transform); }));
        L.Add(TutorialStep.SayFn(delegate
            {
                string real = "";
                if (Campaign.hits.Count > 0 && Campaign.hits[0].flare != null)
                    real = "¡Fue una llamarada REAL! " + Campaign.hits[0].flare.Short + ", guardada por la NASA en DONKI. ";
                return "Esa fue la tormenta. " + real + "Al final verás cuánta radiación evitó tu escudo. " +
                       "A la META le faltan " + Mathf.Max(0, diff.goalPacks - o.experiments) + " paquetes: ¡sigue con el ciclo EXPERIMENTO, ANTENA a la TIERRA, SUBIR BUFFER!";
            }, delegate { Highlight((RectTransform)subText.transform); }));
        if (diff.call)
            L.Add(TutorialStep.Say("Dentro de ANTENA también puedes LLAMAR A LA TIERRA para pedir ayuda o hablar con la familia. La respuesta llega " + Session.Site.delayDays +
                (Session.Site.delayDays == 1 ? " noche" : " noches") + " después: ¡hay que pedirla con tiempo! " +
                "Mira la barra de DESCARGA: sube cada noche hasta que llega la respuesta. Con DATOS NASA ves qué es real.",
                delegate { Highlight((RectTransform)taskBtn[5].transform, linkPanel); }));
        L.Add(TutorialStep.Say("Ya sabes lo básico: " + (diff.house ? "REPARA lo roto, " : "") + (diff.panels ? "el POLVO ensucia los paneles, " : "") + Session.Site.nightArt + " " + Session.Site.nightName.ToLower() +
            " pide calefacción, y apaga las máquinas que no necesites. Caerán imprevistos mientras juegas: ¡guarda reservas! Un botón que BRILLA es la tarea que te recomiendo. " +
            "Recuerda: MANDA " + diff.goalPacks + " paquetes de datos y SOBREVIVE. ¡Suerte, comandante!", delegate { ClearHighlights(); }));
        return L;
    }

    // ================================================================ acciones del jugador
    bool Blocked() { return busy || finished || modalRoot != null; }

    void LogEvent(string text, LogKind kind) { Campaign.Add(o.day, text, kind); }

    void OnToggle(int s, bool on)
    {
        if (Blocked()) return;
        endArmed = false;
        string msg;
        if (!o.Toggle(s, on, out msg)) { GameAudio.Play(Sfx.UiDenied); Notice(msg); return; }
        if (s == Outpost.SysGh) GameAudio.Play(on ? Sfx.CmdPayloadOn : Sfx.CmdPayloadOff);
        else if (s == Outpost.SysRc) GameAudio.Play(on ? Sfx.CmdAntennaOn : Sfx.CmdAntennaOff);
        else GameAudio.Play(on ? Sfx.CmdHeaterOn : Sfx.CmdHeaterOff);
        LogEvent(msg, LogKind.Info);
        Refresh();
        if (sysIcons[s] != null)
        {
            if (on)
            {
                FxLayer.Burst(sysIcons[s].transform, SysCols[s], 10, 130f, 11f, "mix");
                FxLayer.Ring(sysIcons[s].transform, SysCols[s], 90f, 0.7f);
            }
            else FxLayer.Burst(sysIcons[s].transform, new Color(0.5f, 0.55f, 0.55f, 0.8f), 6, 70f, 9f, "circle");
            if (gauges[Outpost.E] != null) gauges[Outpost.E].Flash(on ? Gfx.Mid : Gfx.Good);
        }
    }

    void OnTask(TaskId t)
    {
        if (Blocked()) return;
        endArmed = false;
        if (t == TaskId.Call) { if (Kids) OnAntennaChoice(1); else ShowAntennaMenu(); return; }
        string msg;
        if (!o.Do(t, out msg)) { GameAudio.Play(Sfx.UiDenied); Notice(msg); return; }
        switch (t)
        {
            case TaskId.Reinforce: GameAudio.Play(Sfx.CmdSafeMode); break;
            case TaskId.Repair: GameAudio.Play(Sfx.CmdDesaturate); break;
            case TaskId.Clean: GameAudio.Play(Sfx.CmdPointSun); break;
            case TaskId.Experiment: GameAudio.Play(Sfx.DataChime); break;
            case TaskId.Rest: GameAudio.Play(Sfx.ResponseGood); break;
        }
        LogEvent(msg, LogKind.Good);
        Refresh();
        PlayTaskFx(t);
    }

    // Animación de cada tarea: se ve qué cambió y dónde.
    void PlayTaskFx(TaskId t)
    {
        int ti = (int)t;
        if (ti >= 0 && ti < 6 && taskIcons[ti] != null)
            FxLayer.Burst(taskIcons[ti].transform, TaskCols[ti], 8, 130f, 11f, "star4");
        Transform hero = Hero();
        switch (t)
        {
            case TaskId.Reinforce:
                FxLayer.Ring(hero, Gfx.Blue, 260f, 0.9f);
                FxLayer.Pop(hero, "shield", Gfx.Blue, 60f, 230f, 0.9f, 0f);
                FxLayer.Burst(Tgt(Outpost.B), Gfx.Blue, 12, 150f, 12f, "star4");
                if (gauges[Outpost.B] != null) gauges[Outpost.B].Flash(Gfx.Blue);
                break;
            case TaskId.Repair:
                FxLayer.Pop(Tgt(Outpost.I), "wrench", TaskCols[1], 50f, 170f, 0.9f, 220f);
                FxLayer.Burst(Tgt(Outpost.I), new Color(1f, 0.75f, 0.25f, 1f), 16, 200f, 12f, "star4");
                if (gauges[Outpost.I] != null) gauges[Outpost.I].Flash(Gfx.Good);
                break;
            case TaskId.Clean:
                FxLayer.Burst(Tgt(Outpost.P), new Color(1f, 0.95f, 0.6f, 1f), 18, 190f, 13f, "star4");
                FxLayer.Ring(Tgt(Outpost.P), new Color(1f, 0.9f, 0.4f, 1f), 160f, 0.8f);
                if (gauges[Outpost.P] != null) gauges[Outpost.P].Flash(Gfx.Good);
                break;
            case TaskId.Experiment:
                if (taskIcons[3] != null)
                {
                    FxLayer.Pop(taskIcons[3].transform, "flask", TaskCols[3], 40f, 120f, 0.8f, 0f);
                    if (bufCells != null && bufCells.Length > 0)
                    {
                        int ci = Mathf.Clamp(o.DataPacks - 1, 0, bufCells.Length - 1);
                        FxLayer.Fly(taskIcons[3].transform, bufCells[ci].transform, TaskCols[3], 3, 0.9f, 40f);
                    }
                }
                break;
            case TaskId.Rest:
                FxLayer.Pop(hero, "moon", TaskCols[4], 50f, 190f, 1.1f, 0f);
                FxLayer.FloatText(hero, "Zzz", Gfx.Blue, 28);
                FxLayer.Burst(hero, Gfx.Good, 10, 120f, 11f, "star4");
                if (gauges[Outpost.H] != null) gauges[Outpost.H].Flash(Gfx.Good);
                if (gauges[Outpost.M] != null) gauges[Outpost.M].Flash(Gfx.Good);
                break;
        }
    }

    // ---- Llamar a la Tierra: la ayuda llega con retraso
    int CallDelay()
    {
        int d = Session.Site.delayDays;
        if (EventDirector.Has(events, o.day, EvType.Dust)) d += 1;     // el polvo tapa la antena
        return d;
    }

    int ForecastDays() { return o.forecastUntil >= o.day ? Tuning.ForecastLong : Tuning.ForecastShort; }

    void ShowDatos()
    {
        if (busy || finished || modalRoot != null) return;
        var m = OpenModal(0.72f);
        NasaPanel.Fill(m, CloseModal);
    }

    // ================================================================ ANTENA y LLAMADA A LA TIERRA
    static readonly Color EarthBlue = new Color(0.25f, 0.55f, 0.95f, 1f);
    static readonly Color EarthGreen = new Color(0.35f, 0.85f, 0.45f, 1f);
    static readonly Color AntBg = new Color(0.02f, 0.04f, 0.09f, 0.99f);
    static readonly Color CardBg = new Color(0.07f, 0.16f, 0.34f, 1f);
    static readonly Color CardOff = new Color(0.08f, 0.10f, 0.14f, 1f);
    static readonly Color DimText = new Color(0.55f, 0.62f, 0.72f, 1f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.25f, 1f);

    // El planeta Tierra: un círculo azul con los continentes encima. on = false: apagado (gris azulado).
    static Image EarthIcon(Transform parent, float x0, float y0, float x1, float y1, bool on)
    {
        var ocean = Gfx.Icon(parent, "circle", x0, y0, x1, y1, on ? EarthBlue : new Color(0.16f, 0.22f, 0.32f, 1f));
        Gfx.Icon(ocean.transform, "land", 0f, 0f, 1f, 1f, on ? EarthGreen : new Color(0.20f, 0.30f, 0.25f, 1f));
        return ocean;
    }

    // Tarjeta grande con dibujo, título y una línea de coste. ok = false: se ve apagada (al pulsarla explica por qué no se puede).
    Button AntCard(Transform parent, string icon, Color ic, string title, string sub, float y0, float y1, bool ok, Action act)
    {
        var b = UIKit.MakeButton(parent, title + "\n<color=#" + (ok ? "9fd0a8" : "ff9a3c") + ">" + sub + "</color>", 19, 0.03f, y0, 0.97f, y1,
                                 ok ? CardBg : CardOff, act, TextAnchor.MiddleLeft, Sfx.RadioNext);
        var tx = b.GetComponentInChildren<Text>();
        Fit(tx, 9, 19);
        tx.color = ok ? Color.white : new Color(0.55f, 0.60f, 0.66f, 1f);
        tx.rectTransform.offsetMin = new Vector2(86f, tx.rectTransform.offsetMin.y);
        var img = Gfx.IconLeft(b.transform, icon, 56f, 14f, ok ? ic : new Color(0.30f, 0.33f, 0.36f, 1f));
        if (ok)
        {
            var pu = img.gameObject.AddComponent<Pulser>();
            pu.onColor = ic; pu.amount = 0.08f; pu.speed = 3f;
        }
        UIKit.Border((RectTransform)b.transform, ok ? UIKit.RadioEdge : new Color(0.20f, 0.24f, 0.30f, 1f), 2f);
        return b;
    }

    // ---- ANTENA: apuntar (CIENCIA o TIERRA), subir el buffer o llamar a la Tierra
    void ShowAntennaMenu()
    {
        if (o.ap <= 0) { GameAudio.Play(Sfx.UiDenied); Notice("Ya no quedan tareas esta noche. ¡Termina la noche!"); return; }
        GameAudio.Play(Sfx.LinkOpen);
        var m = OpenModal(0.78f);
        var p = UIKit.Panel(m, "Antenna", 0.10f, 0.05f, 0.90f, 0.95f, AntBg);
        UIKit.Border(p, UIKit.RadioEdge, 3f);
        bool dust = EventDirector.Has(events, o.day, EvType.Dust);
        Color batCol = Gfx.Level(o.v[Outpost.E] / 100f);

        // ---- cabecera: antena que emite ondas, título y cuánto te queda
        var dish = Gfx.Icon(p, "dish", 0.02f, 0.845f, 0.10f, 0.985f, UpColor);
        var dem = dish.gameObject.AddComponent<RingEmitter>();
        dem.color = UpColor; dem.interval = 1.4f; dem.size = 100f;
        UIKit.Label(p, "ANTENA", 34, TextAnchor.MiddleLeft, UIKit.Amber, 0.115f, 0.86f, 0.42f, 0.985f);
        Gfx.Dot(p, "bolt", 0.50f, 0.915f, 30f, Gold);
        UIKit.Label(p, o.ap + (o.ap == 1 ? " tarea" : " tareas"), 17, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.52f, 0.87f, 0.68f, 0.96f);
        Gfx.Dot(p, "battery", 0.715f, 0.915f, 32f, batCol);
        UIKit.Label(p, "batería " + Mathf.RoundToInt(o.v[Outpost.E]) + " %", 17, TextAnchor.MiddleLeft, batCol, 0.735f, 0.87f, 0.97f, 0.96f);

        // ---- ¿a qué mira la antena? (CIENCIA a la izquierda, TIERRA a la derecha, y la antena se inclina hacia donde mira)
        if (!Kids)
        {
            bool e = o.aimEarth;
            var sci = UIKit.Panel(p, "TileSci", 0.03f, 0.545f, 0.25f, 0.82f, e ? new Color(0.06f, 0.07f, 0.10f, 1f) : new Color(0.26f, 0.19f, 0.04f, 1f));
            UIKit.Border(sci, e ? new Color(0.20f, 0.22f, 0.28f, 1f) : UIKit.Amber, e ? 2f : 4f);
            var fi = Gfx.Icon(sci, "flask", 0.22f, 0.30f, 0.78f, 0.95f, e ? new Color(0.35f, 0.36f, 0.42f, 1f) : TaskCols[3]);
            if (!e) { var pf = fi.gameObject.AddComponent<Pulser>(); pf.onColor = fi.color; pf.amount = 0.08f; pf.speed = 4f; }
            UIKit.Label(sci, "CIENCIA", 15, TextAnchor.MiddleCenter, e ? DimText : UIKit.Amber, 0f, 0.02f, 1f, 0.30f);

            var ant = Gfx.Icon(p, "dish", 0.275f, 0.56f, 0.385f, 0.81f, UpColor);
            ant.transform.localRotation = Quaternion.Euler(0f, 0f, e ? -30f : 30f);
            var aem = ant.gameObject.AddComponent<RingEmitter>();
            aem.color = UpColor; aem.interval = 1.8f; aem.size = 90f;

            var ter = UIKit.Panel(p, "TileEarth", 0.41f, 0.545f, 0.63f, 0.82f, e ? new Color(0.04f, 0.12f, 0.26f, 1f) : new Color(0.06f, 0.07f, 0.10f, 1f));
            UIKit.Border(ter, e ? UIKit.RadioEdge : new Color(0.20f, 0.22f, 0.28f, 1f), e ? 4f : 2f);
            var ei = EarthIcon(ter, 0.22f, 0.30f, 0.78f, 0.95f, e);
            if (e) { var pe = ei.gameObject.AddComponent<Pulser>(); pe.onColor = ei.color; pe.amount = 0.05f; pe.speed = 3f; }
            UIKit.Label(ter, "TIERRA", 15, TextAnchor.MiddleCenter, e ? UIKit.RadioEdge : DimText, 0f, 0.02f, 1f, 0.30f);
        }

        // ---- datos guardados: una estrella por paquete
        var dp = UIKit.Panel(p, "Data", Kids ? 0.03f : 0.66f, 0.545f, 0.97f, 0.82f, new Color(0.05f, 0.07f, 0.12f, 1f));
        UIKit.Border(dp, new Color(0.22f, 0.30f, 0.45f, 1f), 2f);
        UIKit.Label(dp, "DATOS GUARDADOS", 13, TextAnchor.MiddleCenter, UIKit.RadioEdge, 0f, 0.78f, 1f, 1f);
        int nC = Mathf.Clamp(Outpost.MaxDataPacks, 1, 8);
        for (int i = 0; i < nC; i++)
        {
            var c = Gfx.Dot(dp, "star4", (i + 0.5f) / nC, 0.52f, Mathf.Min(46f, 170f / nC), i < o.DataPacks ? Gold : Gfx.Dim);
            if (i < o.DataPacks) { var pc = c.gameObject.AddComponent<Pulser>(); pc.onColor = Gold; pc.amount = 0.12f; pc.speed = 4f; }
        }
        UIKit.Label(dp, o.DataPacks + " de " + Outpost.MaxDataPacks, 16, TextAnchor.MiddleCenter, UIKit.Phosphor, 0f, 0.02f, 1f, 0.27f);
        if (diff.dataLoss)
            UIKit.Label(p, dust ? "<color=#ff9a3c>Hay polvo: esta noche no se puede mandar nada.</color>"
                                : "<color=#ff9a3c>Ojo: una tormenta del Sol borra la mitad de los datos.</color>",
                        13, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.03f, 0.495f, 0.97f, 0.54f);

        // ---- las tarjetas de acciones
        var entries = new List<int>();
        if (!Kids) entries.Add(0);
        entries.Add(1);
        entries.Add(2);
        float stepY = 0.385f / entries.Count;
        int delay = CallDelay();
        for (int n = 0; n < entries.Count; n++)
        {
            int k = entries[n];
            int kk = k;
            float y1 = 0.49f - n * stepY, y0 = y1 - (stepY - 0.012f);
            string title = "", sub = "", icon = "dish";
            Color ic = UpColor;
            bool ok = true;
            if (k == 0)
            {
                float c = o.Cost(TaskId.Aim);
                title = o.aimEarth ? "APUNTAR A CIENCIA" : "APUNTAR A LA TIERRA";
                sub = "gira la antena " + Tuning.AimSeconds.ToString("0.#") + " s  |  " + Bat(c) + Fd(TaskId.Aim) + "  |  1 tarea";
                if (o.v[Outpost.E] < c + Tuning.EnMargin) { ok = false; sub = "falta batería"; }
            }
            else if (k == 1)
            {
                float c = o.Cost(TaskId.Upload);
                icon = "flask"; ic = TaskCols[3];
                title = Kids ? "MANDAR DATOS A LA TIERRA" : "SUBIR BUFFER A LA TIERRA";
                int up = o.UploadPacks();
                sub = "manda " + up + (up == 1 ? " paquete" : " paquetes") + "  |  " + Bat(c) + Fd(TaskId.Upload) + "  |  1 tarea";
                if (!Kids && !o.aimEarth) { ok = false; sub = "primero apunta la antena a la TIERRA"; }
                else if (diff.dataLoss && dust) { ok = false; sub = "el polvo tapa la señal"; }
                else if (o.buffer < 0.5f) { ok = false; sub = "no hay datos: haz experimentos"; }
                else if (o.v[Outpost.E] < c + Tuning.EnMargin) { ok = false; sub = "falta batería"; }
            }
            else
            {
                float c = o.Cost(TaskId.Call);
                icon = "heart"; ic = new Color(1f, 0.50f, 0.62f, 1f);
                title = "LLAMAR A LA TIERRA";
                sub = "ayuda o noticias  |  tarda " + delay + (delay == 1 ? " noche" : " noches") + "  |  " + Bat(c);
                if (o.reqKind >= 0) { ok = false; sub = "esperando respuesta (llega la noche " + o.reqDay + ")"; }
                else if (o.v[Outpost.E] < c + Tuning.EnMargin) { ok = false; sub = "falta batería"; }
            }
            AntCard(p, icon, ic, title, sub, y0, y1, ok, delegate { OnAntennaChoice(kk); });
        }
        UIKit.MakeButton(p, "CERRAR", 16, 0.38f, 0.015f, 0.62f, 0.085f, UIKit.Dark, delegate { CloseModal(); }, TextAnchor.MiddleCenter, Sfx.ManualClose);
    }

    void OnAntennaChoice(int k)
    {
        CloseModal();
        if (k == 2) { ShowCallMenu(); return; }
        string msg;
        bool ok;
        int packsBefore = o.DataPacks;
        if (k == 0) ok = o.Aim(out msg);
        else ok = o.Upload(EventDirector.Has(events, o.day, EvType.Dust), out msg);
        if (!ok) { GameAudio.Play(Sfx.UiDenied); Notice(msg); return; }
        if (k == 1)
        {
            int sentPacks = Mathf.Max(1, packsBefore - o.DataPacks);
            StartCoroutine(UploadRoutine(1.4f + 1.0f * sentPacks, "ENVIANDO\n" + sentPacks + (sentPacks == 1 ? " paquete" : " paquetes"),
                                         msg, LogKind.Good, Sfx.DataChime));
            return;
        }
        StartCoroutine(AimRoutine(msg));
    }

    // ---- LLAMAR A LA TIERRA: elige qué pedir. La respuesta puede traer una buena o una mala noticia.
    void ShowCallMenu()
    {
        if (o.ap <= 0) { GameAudio.Play(Sfx.UiDenied); Notice("Ya no quedan tareas esta noche. ¡Termina la noche!"); return; }
        if (o.reqKind >= 0) { GameAudio.Play(Sfx.UiDenied); Notice("Ya esperas una respuesta: la Tierra contesta la noche " + o.reqDay + "."); return; }
        GameAudio.Play(Sfx.LinkOpen);
        int delay = CallDelay();
        var site = Session.Site;
        var m = OpenModal(0.78f);
        var p = UIKit.Panel(m, "Call", 0.10f, 0.05f, 0.90f, 0.95f, AntBg);
        UIKit.Border(p, UIKit.RadioEdge, 3f);

        // ---- cabecera: la Tierra, el título y cuánto tarda la respuesta
        var earth = EarthIcon(p, 0.02f, 0.80f, 0.11f, 0.985f, true);
        var ep = earth.gameObject.AddComponent<Pulser>();
        ep.onColor = earth.color; ep.amount = 0.05f; ep.speed = 2f;
        UIKit.Label(p, "LLAMAR A LA TIERRA", 30, TextAnchor.MiddleLeft, UIKit.Amber, 0.125f, 0.86f, 0.62f, 0.985f);
        Gfx.Dot(p, "moon", 0.665f, 0.925f, 30f, new Color(1f, 0.92f, 0.6f, 1f));
        UIKit.Label(p, "responde en <color=#ffc033>" + delay + (delay == 1 ? " noche" : " noches") + "</color>", 16, TextAnchor.MiddleLeft,
                    UIKit.Phosphor, 0.685f, 0.87f, 0.98f, 0.98f);
        string dustNote = delay > site.delayDays ? "  <color=#ff9a3c>(el polvo tapa la antena: +1 noche)</color>" : "";
        UIKit.Label(p, Kids ? "¡Pide ayuda con tiempo! La Tierra está lejos."
                            : "La señal viaja rápido (" + site.delayReal + "), pero la respuesta llega noches después." + dustNote,
                    14, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.03f, 0.765f, 0.97f, 0.85f);

        // ---- qué puede traer la respuesta: 50 % normal, 25 % buena noticia, 25 % mala noticia
        UIKit.Label(p, "LA RESPUESTA PUEDE TRAER...", 12, TextAnchor.MiddleLeft, DimText, 0.03f, 0.705f, 0.6f, 0.765f);
        string[] cn = { "50 %  NORMAL\n<color=#9fd0a8>lo que pediste</color>", "25 %  BUENA NOTICIA\n<color=#9fd0a8>el ánimo sube y rindes más</color>",
                        "25 %  MALA NOTICIA\n<color=#ffb0a0>el ánimo baja</color>" };
        string[] ci = { "dish", "heart", "cross" };
        Color[] cc = { UpColor, Gfx.Good, Gfx.Bad };
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.03f + i * 0.32f;
            var chip = UIKit.Panel(p, "Chip", x0, 0.595f, x0 + 0.30f, 0.70f, new Color(cc[i].r * 0.12f, cc[i].g * 0.12f, cc[i].b * 0.12f, 1f));
            UIKit.Border(chip, new Color(cc[i].r * 0.7f, cc[i].g * 0.7f, cc[i].b * 0.7f, 1f), 2f);
            Gfx.IconLeft(chip, ci[i], 34f, 8f, cc[i]);
            var ct = UIKit.Label(chip, cn[i], 14, TextAnchor.MiddleLeft, Color.white, 0f, 0f, 1f, 1f);
            Fit(ct, 8, 14);
            ct.rectTransform.offsetMin = new Vector2(48f, ct.rectTransform.offsetMin.y);
        }

        // ---- las tres cosas que puedes pedir
        string[] names = { "PRONÓSTICO MÁS LARGO", "AYUDA PARA REPARAR", "MENSAJE DE LAS FAMILIAS" };
        string[] subs =
        {
            "verás " + Tuning.ForecastLong + " noches adelante durante 3 noches",
            "tu próxima reparación es gratis y +" + Tuning.RepairGuideBonus.ToString("0") + " más",
            "ánimo +" + Tuning.FamilyMorale.ToString("0") + "  (con buena noticia: +" + (Tuning.FamilyMorale * 2f).ToString("0") + ")"
        };
        string[] icons = { "flame", "wrench", "heart" };
        Color[] cols = { new Color(1f, 0.78f, 0.30f, 1f), TaskCols[1], new Color(1f, 0.50f, 0.62f, 1f) };
        float cost = o.Cost(TaskId.Call);
        bool power = o.v[Outpost.E] >= cost + Tuning.EnMargin;
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            float y1 = 0.575f - k * 0.155f, y0 = y1 - 0.143f;
            AntCard(p, icons[k], cols[k], names[k], power ? subs[k] + "  |  " + Bat(cost) : "falta batería", y0, y1, power, delegate { DoCall(kk); });
        }
        UIKit.MakeButton(p, "CERRAR", 16, 0.38f, 0.015f, 0.62f, 0.085f, UIKit.Dark, delegate { CloseModal(); }, TextAnchor.MiddleCenter, Sfx.ManualClose);
    }

    void DoCall(int kind)
    {
        string msg;
        int delay = CallDelay();
        if (!o.CallEarth(kind, delay, out msg)) { GameAudio.Play(Sfx.UiDenied); CloseModal(); Notice(msg); return; }
        if (o.reqDay > totalDays) msg += " Ojo: llegará cuando la misión ya haya terminado.";
        StartCoroutine(CallRoutine(kind, msg));
    }

    // Animación de la llamada: "LLAMANDO..." con puntitos de señal, y al final "¡CONECTADO!".
    IEnumerator CallRoutine(int kind, string msg)
    {
        busy = true;
        Refresh();
        var m = OpenModal(0.86f);
        var p = UIKit.Panel(m, "Calling", 0.15f, 0.18f, 0.85f, 0.82f, AntBg);
        UIKit.Border(p, UIKit.RadioEdge, 3f);
        string[] asks = { "PIDES: PRONÓSTICO MÁS LARGO", "PIDES: AYUDA PARA REPARAR", "PIDES: MENSAJE DE LAS FAMILIAS" };
        UIKit.Label(p, asks[Mathf.Clamp(kind, 0, 2)], 16, TextAnchor.MiddleCenter, DimText, 0.05f, 0.88f, 0.95f, 0.98f);

        var dish = Gfx.Icon(p, "dish", 0.05f, 0.42f, 0.27f, 0.86f, UpColor);
        dish.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
        var em = dish.gameObject.AddComponent<RingEmitter>();
        em.color = UpColor; em.interval = 0.45f; em.size = 150f;
        var earth = EarthIcon(p, 0.70f, 0.36f, 0.95f, 0.88f, true);
        var epu = earth.gameObject.AddComponent<Pulser>();
        epu.onColor = earth.color; epu.amount = 0.05f; epu.speed = 2.5f;
        var sig = new Image[5];
        for (int i = 0; i < 5; i++) sig[i] = Gfx.Dot(p, "star4", 0.34f + i * 0.075f, 0.64f, 24f, Gfx.Dim);
        var title = UIKit.Label(p, "LLAMANDO", 40, TextAnchor.MiddleCenter, UIKit.Amber, 0.05f, 0.18f, 0.95f, 0.36f);
        var subL = UIKit.Label(p, "Conectando con la Tierra...", 16, TextAnchor.MiddleCenter, UIKit.Phosphor, 0.05f, 0.05f, 0.95f, 0.18f);

        GameAudio.Play(Sfx.RadioOpen);
        GameAudio.SetLoop(Loop.Downlink, true);
        float t = 0f, tickT = 0f;
        while (t < 2.6f)
        {
            t += Time.unscaledDeltaTime;
            int dots = 1 + (int)(t / 0.35f) % 3;
            title.text = "LLAMANDO" + new string('.', dots);
            int lit = (int)(t / 0.22f) % 6;
            for (int i = 0; i < 5; i++) sig[i].color = i < lit ? UpColor : Gfx.Dim;
            tickT -= Time.unscaledDeltaTime;
            if (tickT <= 0f) { tickT = 0.42f; GameAudio.Play(Sfx.BootTick); }
            yield return null;
        }

        // ¡conectado!
        GameAudio.SetLoop(Loop.Downlink, false);
        for (int i = 0; i < 5; i++) sig[i].gameObject.SetActive(false);
        em.color = Gfx.Good; em.interval = 0.7f;
        var ck = Gfx.Icon(p, "check", 0.40f, 0.46f, 0.60f, 0.80f, Gfx.Good);
        var ckp = ck.gameObject.AddComponent<Pulser>();
        ckp.onColor = Gfx.Good; ckp.amount = 0.10f; ckp.speed = 6f;
        title.text = "¡CONECTADO!"; title.color = Gfx.Good;
        subL.text = "Mensaje enviado. La Tierra responde la noche " + o.reqDay + ".";
        GameAudio.Play(Sfx.CmdArrive);
        FxLayer.Ring(earth.transform, Gfx.Good, 280f, 1.0f);
        FxLayer.Burst(earth.transform, Gfx.Good, 16, 230f, 13f, "star4");
        yield return new WaitForSecondsRealtime(1.7f);

        CloseModal();
        LogEvent(msg, LogKind.Info);
        busy = false;
        Refresh();
    }

    // Apuntar la antena: gira hacia la Tierra (o vuelve a CIENCIA) con un pequeño temblor.
    IEnumerator AimRoutine(string msg)
    {
        busy = true; upAnim = true;
        Refresh();
        GameAudio.Play(o.aimEarth ? Sfx.CmdPointEarth : Sfx.CmdPointSun);
        GameAudio.Play(Sfx.EclipseStart);                            // arranca el motor de la antena
        GameAudio.SetLoop(Loop.Downlink, true);                      // zumbido mientras gira
        if (upCap != null) { upCap.text = o.aimEarth ? "APUNTANDO\nA LA TIERRA" : "APUNTANDO\nA CIENCIA"; upCap.color = UpColor; }
        float from = 0f;
        if (dishTf != null) { from = dishTf.localEulerAngles.z; if (from > 180f) from -= 360f; }
        float target = o.aimEarth ? -30f : 0f;
        float t = 0f, dur = Mathf.Max(0.5f, Tuning.AimSeconds), ringT = 0f, servoT = 0.25f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            if (dishTf != null) dishTf.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, target, k) + Mathf.Sin(t * 16f) * (1f - k) * 5f);
            servoT -= Time.unscaledDeltaTime;
            if (servoT <= 0f) { servoT = 0.45f; GameAudio.Play(Sfx.CmdDesaturate); }     // clics del motor
            ringT -= Time.unscaledDeltaTime;
            if (ringT <= 0f && dishTf != null) { ringT = 0.35f; FxLayer.Ring(dishTf, UpColor, 120f, 0.9f); }
            yield return null;
        }
        if (dishTf != null)
        {
            dishTf.localRotation = Quaternion.Euler(0f, 0f, target);
            FxLayer.Ring(dishTf, UpColor, 160f, 1f);
            FxLayer.Burst(dishTf, UpColor, 8, 110f, 10f, "star4");
        }
        GameAudio.SetLoop(Loop.Downlink, false);
        if (upCap != null) upCap.text = o.aimEarth ? "¡APUNTA A\nLA TIERRA!" : "¡APUNTA A\nCIENCIA!";
        GameAudio.Play(Sfx.CmdArrive);
        LogEvent(msg, LogKind.Good);
        yield return new WaitForSecondsRealtime(0.6f);
        upAnim = false; busy = false;
        Refresh();
    }

    void OnEndPressed()
    {
        if (Blocked()) return;
        if (o.ap > 0 && !endArmed && !TutorialRunning)
        {
            endArmed = true;
            GameAudio.Play(Sfx.AlertWarning);
            SetBanner("Aún te " + (o.ap == 1 ? "queda 1 tarea" : "quedan " + o.ap + " tareas") + ". Si ya terminaste, pulsa TERMINAR NOCHE otra vez.", false);
            Refresh();
            return;
        }
        StartCoroutine(EndDayRoutine());
    }


    // ================================================================ ENLACE: barras de SUBIDA y DESCARGA
    void BuildLinkPanel()
    {
        linkPanel = UIKit.Panel(transform, "Link", 0.895f, 0.17f, 0.99f, 0.91f, new Color(0.06f, 0.08f, 0.13f, 0.98f));
        UIKit.Border(linkPanel, new Color(0.22f, 0.36f, 0.58f, 1f), 3f);
        // la antena "habla": lanza ondas cada pocos segundos y se inclina hacia donde mira
        var dish = Gfx.Icon(linkPanel, "dish", 0.20f, 0.885f, 0.80f, 0.995f, UpColor);
        dishTf = dish.transform;
        dishEm = dish.gameObject.AddComponent<RingEmitter>();
        dishEm.color = UpColor; dishEm.interval = 2.4f; dishEm.size = 120f;
        aimText = UIKit.Label(linkPanel, "", 10, TextAnchor.MiddleCenter, UIKit.Amber, 0f, 0.845f, 1f, 0.89f);
        // casillas de datos guardados (se encienden con cada experimento)
        int nCells = Mathf.Clamp(Outpost.MaxDataPacks, 1, 8);
        bufCells = new Image[nCells];
        for (int i = 0; i < nCells; i++)
            bufCells[i] = Gfx.Dot(linkPanel, "star4", (i + 0.5f) / nCells, 0.035f, Mathf.Min(18f, 100f / nCells), Gfx.Dim);
        if (!Session.Diff.call)
        {
            // EXPLORADOR: solo la barra de SUBIDA (no hay llamadas a la Tierra)
            UIKit.Label(linkPanel, "SUBIDA", 12, TextAnchor.MiddleCenter, UpColor, 0f, 0.80f, 1f, 0.845f);
            upBar = UIKit.MakeVBar(linkPanel, 0.25f, 0.27f, 0.75f, 0.795f);
            upPct = UIKit.Label(linkPanel, "0%", 16, TextAnchor.MiddleCenter, UpColor, 0f, 0.21f, 1f, 0.265f);
            upCap = UIKit.Label(linkPanel, "", 13, TextAnchor.UpperCenter, UpColor, 0f, 0.075f, 1f, 0.205f);
            return;
        }
        UIKit.Label(linkPanel, "SUBIDA", 11, TextAnchor.MiddleCenter, UpColor, 0f, 0.80f, 0.5f, 0.845f);
        UIKit.Label(linkPanel, "DESCARGA", 10, TextAnchor.MiddleCenter, DownColor, 0.5f, 0.80f, 1f, 0.845f);
        upBar = UIKit.MakeVBar(linkPanel, 0.10f, 0.27f, 0.40f, 0.795f);
        downBar = UIKit.MakeVBar(linkPanel, 0.60f, 0.27f, 0.90f, 0.795f);
        upPct = UIKit.Label(linkPanel, "0%", 14, TextAnchor.MiddleCenter, UpColor, 0f, 0.21f, 0.5f, 0.265f);
        downPct = UIKit.Label(linkPanel, "0%", 14, TextAnchor.MiddleCenter, DownColor, 0.5f, 0.21f, 1f, 0.265f);
        upCap = UIKit.Label(linkPanel, "", 12, TextAnchor.UpperCenter, UpColor, 0f, 0.075f, 0.5f, 0.205f);
        downCap = UIKit.Label(linkPanel, "", 12, TextAnchor.UpperCenter, DownColor, 0.5f, 0.075f, 1f, 0.205f);
    }

    // Cuánto falta para que llegue la respuesta de la Tierra (0 a 1).
    float LinkDownFrac()
    {
        if (o == null || o.reqKind < 0) return 0f;
        return Mathf.Clamp01((o.day - o.reqSent) / (float)Mathf.Max(1, o.reqDay - o.reqSent));
    }

    void RefreshLink()
    {
        if (upBar == null || o == null) return;
        if (bufCells != null)
        {
            int dp = Mathf.Clamp(o.DataPacks, 0, bufCells.Length);
            Color gold = new Color(1f, 0.82f, 0.25f, 1f);
            for (int i = 0; i < bufCells.Length; i++) bufCells[i].color = i < dp ? gold : Gfx.Dim;
            for (int i = prevPacks; i < dp; i++) FxLayer.Burst(bufCells[i].transform, gold, 6, 90f, 10f, "star4");
            prevPacks = dp;
        }
        if (aimText != null)
        {
            aimText.text = Kids ? "" : (o.aimEarth ? "<color=#7fc4ff>MIRA A TIERRA</color>" : "MIRA A CIENCIA");
        }
        if (!upAnim && dishTf != null)
            dishTf.localRotation = Quaternion.Euler(0f, 0f, (!Kids && o.aimEarth) ? -30f : 0f);
        if (!upAnim)
        {
            upBar.Set(0f, UpColor);
            upPct.text = "0%";
            if (Session.Diff.dataLoss && EventDirector.Has(events, o.day, EvType.Dust)) { upCap.text = "POLVO\nTAPA"; upCap.color = UIKit.Red; }
            else if (!Kids && !o.aimEarth) { upCap.text = "MIRA A\nCIENCIA"; upCap.color = new Color(0.5f, 0.6f, 0.55f, 1f); }
            else if (o.DataPacks <= 0) { upCap.text = "SIN\nDATOS"; upCap.color = new Color(0.5f, 0.6f, 0.55f, 1f); }
            else { upCap.text = "LISTA"; upCap.color = UpColor; }
        }
        if (!downAnim && downBar != null)
        {
            float f = LinkDownFrac();
            downBar.Set(f, DownColor);
            downPct.text = Mathf.RoundToInt(f * 100f) + "%";
            if (o.reqKind >= 0) { downCap.text = "ESPERA\nnoche " + o.reqDay; downCap.color = DownColor; }
            else { downCap.text = "LIBRE"; downCap.color = new Color(0.5f, 0.6f, 0.55f, 1f); }
        }
    }

    // La barra de SUBIDA se llena poco a poco: mandar datos demora. Mientras sube no se puede hacer otra cosa.
    IEnumerator UploadRoutine(float dur, string cap, string logMsg, LogKind kind, Sfx doneSfx)
    {
        busy = true; upAnim = true;
        Refresh();
        GameAudio.Play(Sfx.CmdAntennaOn);
        GameAudio.SetLoop(Loop.Downlink, true);                      // suena mientras se sube
        upCap.text = cap; upCap.color = UpColor;
        if (dishEm != null) dishEm.interval = 0.35f;                 // la antena emite más rápido mientras sube
        if (bufCells != null && bufCells.Length > 0)
            FxLayer.Fly(bufCells[0].transform, upBar.fill.parent, UpColor, Mathf.Clamp(Mathf.RoundToInt(dur), 2, 6), 0.8f, 30f);
        float t = 0f, sparkT = 0f, tickT = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / dur);
            upBar.Set(f, UpColor);
            upPct.text = Mathf.RoundToInt(f * 100f) + "%";
            tickT -= Time.unscaledDeltaTime;
            if (tickT <= 0f) { tickT = 0.2f; GameAudio.Play(Sfx.BootTick); }      // "bip bip" de datos que suben
            sparkT -= Time.unscaledDeltaTime;
            if (sparkT <= 0f) { sparkT = 0.12f; FxLayer.Burst(upBar.fill.parent, UpColor, 1, 70f, 10f, "star4"); }
            yield return null;
        }
        GameAudio.SetLoop(Loop.Downlink, false);
        upBar.Set(1f, UpColor);
        upPct.text = "100%";
        upCap.text = "¡LISTO!";
        FxLayer.Burst(upBar.fill.parent, UpColor, 16, 200f, 13f, "mix");
        if (dishTf != null) FxLayer.Ring(dishTf, UpColor, 150f, 1f);
        if (dishEm != null) dishEm.interval = 2.4f;
        GameAudio.Play(doneSfx);
        LogEvent(logMsg, kind);
        Refresh();
        yield return new WaitForSecondsRealtime(0.8f);
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            upBar.Set(1f - Mathf.Clamp01(t / 0.5f), UpColor);
            yield return null;
        }
        upAnim = false; busy = false;
        Refresh();
    }

    // La barra de DESCARGA sube cada noche mientras esperas la respuesta de la Tierra; al llegar se llena del todo.
    IEnumerator DownloadRoutine(float from, float to, float dur, string cap, bool arrival)
    {
        downAnim = true;
        downCap.text = cap; downCap.color = DownColor;
        GameAudio.SetLoop(Loop.Downlink, true);                      // suena mientras llegan los datos
        if (dishTf != null) FxLayer.Ring(dishTf, DownColor, 130f, 1f);
        if (!arrival && downBar != null) FxLayer.Fly(dishTf, downBar.fill.parent, DownColor, 3, 0.9f, 25f);
        float t = 0f, tickD = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            downBar.Set(f, DownColor);
            downPct.text = Mathf.RoundToInt(f * 100f) + "%";
            tickD -= Time.unscaledDeltaTime;
            if (tickD <= 0f) { tickD = 0.25f; GameAudio.Play(Sfx.BootTick); }
            yield return null;
        }
        GameAudio.SetLoop(Loop.Downlink, false);
        downBar.Set(to, DownColor);
        downPct.text = Mathf.RoundToInt(to * 100f) + "%";
        if (arrival)
        {
            downCap.text = "¡LLEGÓ!";
            GameAudio.Play(Sfx.DataChime);
            FxLayer.Fly(dishTf, Hero(), DownColor, 6, 1.0f, 40f);
            FxLayer.Burst(downBar.fill.parent, DownColor, 14, 180f, 13f, "mix");
            if (gauges[Outpost.H] != null) gauges[Outpost.H].Flash(Gfx.Blue);
            yield return new WaitForSecondsRealtime(0.9f);
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                downBar.Set(1f - Mathf.Clamp01(t / 0.5f), DownColor);
                yield return null;
            }
        }
        downAnim = false;
        RefreshLink();
    }

    // ================================================================ el paso de una noche a otra
    IEnumerator Fade(float a0, float a1, float dur)
    {
        fadeGroup.blocksRaycasts = true;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(a0, a1, Mathf.Clamp01(t / dur));
            yield return null;
        }
        fadeGroup.alpha = a1;
        if (a1 <= 0.001f) fadeGroup.blocksRaycasts = false;
    }

    IEnumerator EndDayRoutine()
    {
        busy = true; endArmed = false;
        Refresh();
        float[] before = o.Snapshot();
        int today = o.day;
        bool waiting = o.reqKind >= 0;                 // ¿hay una respuesta de la Tierra en camino?
        float downFrom = LinkDownFrac();
        if (waiting) downAnim = true;                  // la barra de DESCARGA se anima al final
        GameAudio.Play(Sfx.HourChime);
        FxLayer.Pop(fadeAnchor, "moon", new Color(1f, 0.92f, 0.6f, 1f), 60f, 190f, 1.6f, 0f);
        FxLayer.Burst(fadeAnchor, new Color(0.8f, 0.9f, 1f, 1f), 14, 220f, 14f, "star4");
        fadeText.text = "FIN DE LA NOCHE " + today;
        yield return StartCoroutine(Fade(0f, 1f, 0.5f));
        yield return new WaitForSecondsRealtime(0.6f);

        string dead = o.EndDay(events);
        if (dead != null)
        {
            daysDone = today - 1;
            AddSample(true);
            fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false; fadeText.text = "";
            Finish(false, dead);
            yield break;
        }
        daysDone = today;
        if (o.day > totalDays)
        {
            AddSample(true);
            fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false; fadeText.text = "";
            Finish(true, null);
            yield break;
        }

        holdFx = true;                                  // los medidores esperan a que cierres el informe de la mañana
        var notes = o.BeginDay(events);
        dayTasks = o.ap;
        foreach (var n in notes) LogEvent(n.text, n.kind);
        AddSample(false);
        Refresh();
        fadeText.text = "NOCHE " + o.day;
        yield return new WaitForSecondsRealtime(0.35f);
        yield return StartCoroutine(Fade(1f, 0f, 0.5f));
        fadeText.text = "";
        FxLayer.Burst(fadeAnchor, UIKit.Amber, 16, 280f, 14f, "star4");
        if (waiting)
        {
            bool arrived = o.reqKind < 0;
            yield return StartCoroutine(DownloadRoutine(downFrom, arrived ? 1f : LinkDownFrac(), arrived ? 1.8f : 1.0f,
                                                        arrived ? "RECIBIENDO\nRESPUESTA" : "LLEGANDO...", arrived));
        }
        busy = false;
        ShowMorning(before, notes);
    }

    void AddSample(bool final)
    {
        var s = new DaySample { day = o.day };
        for (int i = 0; i < 8; i++) s.v[i] = o.v[i];
        s.ev = final ? (byte)0 : (byte)EventDirector.Flags(events, o.day);
        Campaign.history.Add(s);
    }

    // ================================================================ refresco del tablero
    static Color Lvl(float v)
    {
        if (v < 25f) return UIKit.Red;
        if (v < 50f) return UIKit.Amber;
        return UIKit.Phosphor;
    }

    // "batería -12" (o "no gasta batería")
    // Comida (y salud) que cuesta una tarea.
    string Fd(TaskId t)
    {
        float f = o.FoodCost(t);
        string r = f > 0.4f ? "  |  comida <color=#ffb347>-" + f.ToString("0") + "</color>" : "";
        if (t == TaskId.Experiment && Tuning.HealthExperiment > 0.4f) r += "  |  salud <color=#ff7a6a>-" + Tuning.HealthExperiment.ToString("0") + "</color>";
        return r;
    }

    static string Bat(float c)
    {
        if (c < 0.5f) return "<color=#73ff8c>no gasta batería</color>";
        return "batería <color=#ffc033>-" + c.ToString("0") + "</color>";
    }

    static string Delta(float d)
    {
        int r = Mathf.RoundToInt(d);
        if (r == 0) return "<color=#5f7f66>(=)</color>";
        return r > 0 ? "<color=#73ff8c>(+" + r + ")</color>" : "<color=#ff6a5a>(" + r + ")</color>";
    }

    static void PaintToggle(Button b, GameObject border, bool active, Color activeColor)
    {
        b.GetComponent<Image>().color = active ? activeColor : UIKit.Dark;
        border.SetActive(active);
        var t = b.GetComponentInChildren<Text>();
        if (t != null) t.color = active ? Color.white : new Color(0.45f, 0.6f, 0.5f, 1f);
    }

    void Refresh()
    {
        if (o == null) return;
        proj = o.ProjectOvernight(events);
        RefreshLink();
        dayText.text = "NOCHE " + Mathf.Min(o.day, totalDays) + " / " + totalDays;
        string metaCol = SciBehind() ? "#ff6a5a" : "#7fc4ff";
        subText.text = Kids ? "<color=" + metaCol + ">META</color>  " + o.experiments + "/" + diff.goalPacks
                            : Session.DifficultyName + (guided ? "  -  TUTORIAL" : "") +
                              "\n<color=" + metaCol + ">META " + o.experiments + "/" + diff.goalPacks + " (obligatoria)</color>  |  " + (o.aimEarth ? "TIERRA" : "CIENCIA");

        if (machHeader != null)
        {
            float dr = o.MachineDrain(events);
            bool hi = dr >= Tuning.HighDrainAt;
            machHeader.text = dr <= 0.01f ? "MÁQUINAS  (apagadas: no gastan batería)"
                : "MÁQUINAS  " + (hi ? "<color=#ff6a5a>¡CONSUMO ALTO!  batería -" : "<color=#ffc033>consumo: batería -") + dr.ToString("0.#") + " POR SEGUNDO</color>";
        }

        // ÁNIMO: dice cuántas tareas hay y cuánto rinde cada una
        if (moodText != null)
        {
            int tier = o.MoodTier();
            moodText.color = tier >= 3 ? Gfx.Good : (tier == 2 ? UIKit.Phosphor : (tier == 1 ? Gfx.Mid : Gfx.Bad));
            int tr = o.Troubles();
            moodText.text = "ÁNIMO " + o.MoodName() + "  |  rinde x" + o.MoodMult().ToString("0.##") + (o.boostNights > 0 ? "  (¡buena noticia!)" : "") +
                            (tr > 0 ? "\n<color=#ff6a5a>" + tr + (tr == 1 ? " cosa falla" : " cosas fallan") + ": el ánimo baja rápido</color>" : "");
        }


        for (int i = 0; i < 8; i++)
            if (gauges[i] != null) gauges[i].Set(o.v[i] / 100f, proj[i], !holdFx);

        // línea de noches
        if (nightPulse != null)
            for (int i = 0; i < nightPulse.Length; i++)
            {
                int d = i + 1;
                nightPulse[i].active = d == o.day && o.day <= totalDays;
                nightPulse[i].onColor = UIKit.Amber;
                nightPulse[i].offColor = d < o.day ? Gfx.Good : Gfx.Dim;
            }
        // rayos de tareas: al gastar uno, chispea
        if (o.day != prevDay) { prevDay = o.day; prevAp = o.ap; }
        else if (o.ap < prevAp && taskDots != null) FxLayer.Burst(taskDots[Mathf.Clamp(o.ap, 0, taskDots.Length - 1)].transform, Gfx.Mid, 6, 90f, 10f, "star4");
        prevAp = o.ap;
        if (taskDots != null)
            for (int i = 0; i < taskDots.Length; i++)
            {
                taskDots[i].gameObject.SetActive(i < dayTasks);
                taskDots[i].color = i < o.ap ? new Color(1f, 0.85f, 0.2f, 1f) : Gfx.Dim;
            }
        apText.color = o.ap > 0 ? UIKit.Phosphor : UIKit.Amber;

        string[] info =
        {
            "comida +" + Tuning.GreenhouseFoodPerSec.ToString("0.#") + "/s, batería -" + Tuning.MachineDrainPerSec.ToString("0.#") + "/s",
            "aire +" + Tuning.RecyclerAirPerSec.ToString("0.#") + "/s, batería -" + Tuning.MachineDrainPerSec.ToString("0.#") + "/s",
            "evita el frío, batería -" + Tuning.MachineDrainPerSec.ToString("0.#") + "/s"
        };
        for (int s = 0; s < nSys; s++)
        {
            bool on = o.SysOn(s), br = o.broken[s];
            bool can = !busy && !lockSys && !br && !finished;
            onBtn[s].interactable = can;
            offBtn[s].interactable = can;
            PaintToggle(onBtn[s], onBorder[s], on && !br, UIKit.Selected);
            PaintToggle(offBtn[s], offBorder[s], !on && !br, new Color(0.40f, 0.10f, 0.10f, 1f));
            sysTexts[s].text = Kids
                ? (br ? "<color=#ff4040>" + SysLabels[s] + " - ROTO</color>" : SysLabels[s])
                : (br ? "<color=#ff4040>" + SysLabels[s] + " - ROTO</color>\n<color=#ff9a9a>usa REPARAR</color>"
                      : SysLabels[s] + "\n<color=#9fd0a8>" + info[s] + "</color>");
            if (sysPulse[s] != null)
            {
                sysPulse[s].active = on && !br;
                sysPulse[s].offColor = br ? new Color(0.65f, 0.15f, 0.12f, 1f) : Gfx.Dim;
            }
        }

        int brk = o.FirstBroken();
        string repairName = brk >= 0 ? "REPARAR: " + EventDirector.SystemNames[brk].ToUpper() : "REPARAR LA CASA";
        float mk = o.MoodMult();
        string[] tl = Kids ? new string[]
        {
            "REFORZAR ESCUDO\n<color=#9fd0a8>" + Bat(o.Cost(TaskId.Reinforce)) + Fd(TaskId.Reinforce) + "</color>",
            repairName,
            "LIMPIAR PANELES",
            "HACER UN EXPERIMENTO\n<color=#9fd0a8>" + Bat(o.Cost(TaskId.Experiment)) + Fd(TaskId.Experiment) + "</color>",
            "DESCANSAR\n<color=#9fd0a8>ánimo +" + Tuning.RestMorale.ToString("0") + "  |  " + "batería <color=#73ff8c>+" + Tuning.RestEnergy.ToString("0") + "</color>" + Fd(TaskId.Rest) + "</color>",
            "ENVIAR DATOS A LA TIERRA\n<color=#9fd0a8>" + Bat(o.Cost(TaskId.Upload)) + Fd(TaskId.Upload) + "</color>"
        } : new string[]
        {
            "REFORZAR ESCUDO\n<color=#9fd0a8>protege del Sol +" + Mathf.RoundToInt(Tuning.ReinforceAmount * mk) + "  |  " + Bat(o.Cost(TaskId.Reinforce)) + Fd(TaskId.Reinforce) + "</color>",
            repairName + "\n<color=#9fd0a8>" + (brk >= 0 ? "arregla lo roto, " : "") + "casa +" + Mathf.RoundToInt((Tuning.RepairAmount + (o.repairGuide ? Tuning.RepairGuideBonus : 0f)) * mk) +
                "  |  " + (o.repairGuide ? "<color=#73ff8c>GRATIS (guía de la Tierra)</color>" : Bat(o.Cost(TaskId.Repair)) + Fd(TaskId.Repair)) + "</color>",
            "LIMPIAR PANELES\n<color=#9fd0a8>paneles +" + Mathf.RoundToInt(Tuning.CleanAmount * mk) + "  |  " + Bat(o.Cost(TaskId.Clean)) + Fd(TaskId.Clean) + "</color>",
            "HACER UN EXPERIMENTO\n<color=#9fd0a8>guarda 1 paquete" + (o.MoodTier() == 3 ? " (¡a veces 2!)" : "") + "  |  " + Bat(o.Cost(TaskId.Experiment)) + Fd(TaskId.Experiment) + "</color>",
            "DESCANSAR\n<color=#9fd0a8>ánimo +" + Tuning.RestMorale.ToString("0") + ", salud +" + Tuning.RestHealth.ToString("0") + "  |  " + "batería <color=#73ff8c>+" + Tuning.RestEnergy.ToString("0") + "</color>" + Fd(TaskId.Rest) + "</color>",
            "ANTENA\n<color=#9fd0a8>mira a " + (o.aimEarth ? "la TIERRA" : "CIENCIA") + "  |  datos " + o.DataPacks + " de " + Outpost.MaxDataPacks + "</color>"
        };
        bool canTask = !busy && !lockTasks && !finished && o.ap > 0;
        for (int t = 0; t < 6; t++)
        {
            if (taskBtn[t] == null) continue;               // esa tarea no existe en este nivel
            taskTexts[t].text = tl[t];
            taskBtn[t].interactable = canTask;
            taskTexts[t].color = Color.white;
            if (taskIcons[t] != null) taskIcons[t].color = canTask ? TaskCols[t] : new Color(0.3f, 0.33f, 0.32f, 1f);
        }

        endBtn.interactable = !busy && !lockEnd && !finished;
        endText.text = endArmed ? "PULSA OTRA VEZ PARA CONFIRMAR"
                     : (o.ap > 0 ? "TERMINAR NOCHE  (te quedan " + o.ap + (o.ap == 1 ? " tarea)" : " tareas)") : "TERMINAR NOCHE");

        forecastText.text = EventDirector.Forecast(events, o.day, ForecastDays());
        alertText.text = BuildAlerts();
        logText.text = BuildLog();
        UpdateTip();

        if (fxl != null)
        {
            Ambient am = Ambient.Calm;
            if (EventDirector.Has(events, o.day, EvType.Night)) am = Ambient.Cold;
            else if (EventDirector.Has(events, o.day, EvType.Dust)) am = Ambient.Dust;
            else if (EventDirector.StormTomorrow(events, o.day)) am = Ambient.Warn;
            fxl.SetAmbient(am);
        }
        if (fx != null) fx.Tick(o.v[Outpost.E] / 100f);
        float e = o.v[Outpost.E];
        if (e < 25f && !lowPlayed) { lowPlayed = true; GameAudio.Play(Sfx.BatteryLow); }
        else if (e >= 30f) lowPlayed = false;
        if (e < 10f && !critPlayed) { critPlayed = true; GameAudio.Play(Sfx.BatteryCritical); }
        else if (e >= 15f) critPlayed = false;
        bool crit = e < 10f || o.v[Outpost.O] < 20f || o.v[Outpost.H] < 25f || o.v[Outpost.I] < 20f;
        GameAudio.SetLoop(Loop.Alarm, crit && !finished);
    }

    string BuildAlerts()
    {
        var sb = new StringBuilder();
        bool cold;
        float sol = EventDirector.SolarFactor(events, o.day, out cold);
        if (EventDirector.Has(events, o.day, EvType.Night)) sb.Append(Session.Site.nightName + ": sin sol y con frío\n");
        else if (EventDirector.Has(events, o.day, EvType.Dust)) sb.Append("POLVO: llega poco sol\n");
        for (int s = 0; s < 4; s++) if (o.broken[s]) sb.Append("ROTO: " + EventDirector.SystemNames[s] + "\n");
        if (o.v[Outpost.E] < 25f) sb.Append("¡POCA BATERÍA!\n");
        if (o.v[Outpost.O] < 30f) sb.Append("¡POCO AIRE!\n");
        if (o.v[Outpost.F] < 30f) sb.Append("POCA COMIDA\n");
        if (o.v[Outpost.H] < 40f) sb.Append("EQUIPO ENFERMO\n");
        if (o.v[Outpost.I] < 30f) sb.Append("CASA MUY DAÑADA\n");
        if (o.v[Outpost.M] < 25f) sb.Append("ÁNIMO BAJO: menos tareas\n");
        if (sb.Length == 0) return "<color=#73ff8c>Todo en orden.</color>";
        return sb.ToString();
    }

    string BuildLog()
    {
        var sb = new StringBuilder();
        int from = Mathf.Max(0, Campaign.log.Count - 5);
        for (int i = from; i < Campaign.log.Count; i++)
        {
            var e = Campaign.log[i];
            sb.Append("<color=" + UIKit.Hex(e.kind) + ">N" + e.day + " " + e.text + "</color>\n");
        }
        return sb.ToString();
    }

    void Update()
    {
        if (o == null || finished) return;
        LiveStep();
        if (TutorialRunning && modalRoot == null && !busy)
        {
            var st = steps[stepIndex];
            if (!st.pause && st.done != null && st.done())
            {
                GameAudio.Play(Sfx.StepComplete);
                NextStep();
            }
        }
    }

    // ================================================================ final de la misión
    // Solo para pruebas (los llama GameFlow desde las casillas de DEBUG).
    public void DebugNextDay()
    {
        if (Blocked() || o == null) return;
        StartCoroutine(EndDayRoutine());
    }

    public void DebugFinish(bool survive)
    {
        if (finished || o == null) return;
        daysDone = survive ? totalDays : Mathf.Max(0, o.day - 1);
        AddSample(true);
        fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false;
        Finish(survive, survive ? null : "health");
    }

    public void DebugBlackout()
    {
        if (finished || o == null) return;
        o.v[Outpost.E] = 0f;
        daysDone = Mathf.Max(0, o.day - 1);
        AddSample(true);
        fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false;
        Finish(false, "power");
    }

    // ---- DEBUG: frío, calor, muertes, apagón de máquinas e imprevistos (los llama GameFlow desde las casillas de DEBUG)
    public void DebugSetTemp(float t)
    {
        if (finished || o == null) return;
        o.bodyTemp = t;
        UpdateTempIcon(); Refresh();
        Debug.Log("[DEBUG] Temperatura del equipo = " + t.ToString("0") + (t < 0f ? " (CONGELADOS: copo de nieve)" : " (CALOR: llama)") + ". Con |temperatura| >= " + Tuning.TempDamageAt.ToString("0") + " pierden salud y ánimo.");
    }

    // Mata al equipo por la causa dada: "cold", "heat", "air", "food", "house", "mood", "radiation".
    public void DebugDie(string reason)
    {
        if (finished || o == null) return;
        if (reason == "cold") o.bodyTemp = -100f; else if (reason == "heat") o.bodyTemp = 100f;
        o.liveDead = reason;
        o.v[Outpost.H] = 0f;
        Debug.Log("[DEBUG] El equipo muere por: " + reason);
        LiveDeath();
    }

    public void DebugMachinesBlackout()
    {
        if (finished || o == null) return;
        o.gh = o.rc = true; o.ht = Session.Diff.heater;
        o.v[Outpost.E] = 6f;
        Refresh();
        Debug.Log("[DEBUG] Batería casi vacía con las máquinas encendidas: en 1-2 segundos llega a 0 y se produce el apagón.");
    }

    public void DebugBadEvent(int pick)
    {
        if (finished || o == null) return;
        var ev = o.DebugBadEvent(pick);
        if (ev == null) { Debug.Log("[DEBUG] El imprevisto " + pick + " no aplica en este nivel (por ejemplo casa o paneles apagados)."); return; }
        Debug.Log("[DEBUG] Imprevisto " + pick + ": " + ev.text);
        Campaign.Add(o.day, ev.text, ev.kind);
        var one = new List<EventNote>(); one.Add(ev);
        EventEffects(one);
        SetBanner("¡IMPREVISTO! " + ev.text, false);
        Refresh();
    }

    public void DebugLiveEvent()
    {
        if (finished || o == null) return;
        for (int i = 0; i < 20; i++)
        {
            var ev = o.LiveEvent(true);
            if (ev == null) continue;
            Debug.Log("[DEBUG] Imprevisto en vivo: " + ev.text);
            Campaign.Add(o.day, ev.text, ev.kind);
            var one = new List<EventNote>(); one.Add(ev);
            EventEffects(one);
            SetBanner("¡IMPREVISTO! " + ev.text, false);
            Refresh();
            return;
        }
        Debug.Log("[DEBUG] No salió ningún imprevisto en vivo (prueba otra vez).");
    }

    public void DebugStormTomorrow()
    {
        if (o == null || finished) return;
        var pool = NasaData.Pool();
        var fl = pool[UnityEngine.Random.Range(0, pool.Count)];
        events.Add(new GameEvent { type = EvType.Solar, day = o.day + 1, dur = 1, sev = fl.Sev * diff.severity, flare = fl });
        Refresh();
        Debug.Log("[DEBUG] Tormenta solar programada para la noche " + (o.day + 1) + ".");
    }

    static string CauseText(string cause)
    {
        switch (cause)
        {
            case "power": return "Se acabó la batería. Todas las máquinas se apagaron.";
            case "oxygen": return "Se acabó el aire dentro de la base.";
            case "health":
                switch (Campaign.deathReason)
                {
                    case "air": return "El equipo murió: se quedó sin aire para respirar.";
                    case "food": return "El equipo murió de hambre: no había comida.";
                    case "house": return "La casa rota enfermó al equipo hasta que murió.";
                    case "mood": return "El ánimo quedó tan bajo que el equipo se enfermó y murió.";
                    case "cold": return "El equipo se congeló: sin calefacción en la noche larga no sobrevivieron.";
                    case "heat": return "El equipo se sobrecalentó: la calefacción a tope durante la tormenta solar los enfermó.";
                }
                return "La radiación y las malas condiciones enfermaron demasiado al equipo: murió.";
            case "structure": return "La casa se dañó tanto que se derrumbó.";
            case "science": return "Sobrevivieron, pero la misión fracasó: no mandaste a la Tierra los datos de ciencia que pedía la META.";
        }
        return "La misión terminó.";
    }

    void BuildLessons(string cause)
    {
        switch (cause)
        {
            case "power": Campaign.Lesson("Sin batería todo se apaga. Apaga las máquinas que no necesites y limpia los paneles antes del polvo."); break;
            case "oxygen": Campaign.Lesson("Sin la máquina de aire (o con una fuga) el aire dura pocas noches. Mantenla encendida y repárala en cuanto se rompa."); break;
            case "health":
                if (Campaign.deathReason == "air") Campaign.Lesson("Sin aire la salud baja rapidísimo. Deja siempre encendida la máquina de aire y repárala en cuanto se rompa.");
                else if (Campaign.deathReason == "mood") Campaign.Lesson("Con el ánimo bajo el equipo pierde salud cada segundo. Usa DESCANSAR a tiempo y arregla lo que falla: todo baja el ánimo.");
                else if (Campaign.deathReason == "house") Campaign.Lesson("Con la casa rota el equipo pierde salud y ánimo sin parar. Repara a tiempo y no dejes el escudo en 0: sin escudo la casa sufre el doble.");
                else if (Campaign.deathReason == "cold") Campaign.Lesson("En la noche larga el frío congela al equipo. Enciende la CALEFACCIÓN a tiempo (y vigila la batería).");
                else if (Campaign.deathReason == "heat") Campaign.Lesson("Con la tormenta solar, la calefacción encendida mucho rato acalora al equipo. Apágala cuando no haga frío.");
                else if (Campaign.deathReason == "food") Campaign.Lesson("Cada tarea da hambre. Mantén el invernadero encendido para que la comida suba.");
                else Campaign.Lesson("La radiación se va sumando. Refuerza el escudo ANTES de las tormentas solares y deja descansar al equipo.");
                break;
            case "structure": Campaign.Lesson("La oscuridad larga sin calefacción, el polvo y los golpes dañan la casa. Enciende la calefacción y repara."); break;
            case "science": Campaign.Lesson("Una misión no es solo sobrevivir: la ciencia es OBLIGATORIA. Haz experimentos desde las primeras noches y súbelos a la Tierra con la antena apuntando a la TIERRA."); break;
        }
        var L = Campaign.ledger;
        var dirt = L.Find("dirt");
        if (dirt != null && dirt.cost[(int)Res.Energy] >= 40f)
            Campaign.Lesson("Los paneles sucios te hicieron perder unos " + dirt.cost[(int)Res.Energy].ToString("0") + " puntos de batería. Límpialos después del polvo.");
        var br = L.Find("broken");
        if (br != null && br.Influence() >= 25f)
            Campaign.Lesson("Las cosas rotas te costaron aire, comida o batería. Repáralas apenas se rompan.");
        var ht = L.Find("ht");
        if (ht != null && ht.cost[(int)Res.Integrity] >= 10f)
            Campaign.Lesson("Sin calefacción en la oscuridad larga, el frío dañó la casa.");
        var storm = L.Find("ev_solar");
        if (storm != null && storm.cost[(int)Res.Health] >= 30f)
            Campaign.Lesson("Una tormenta solar te dio " + storm.cost[(int)Res.Health].ToString("0") + " puntos de radiación. El pronóstico avisa antes: refuerza el escudo a tiempo.");
        foreach (var h in Campaign.hits)
        {
            if (h.flare != null && h.avoided >= 10f)
            {
                Campaign.Lesson("Tu escudo te protegió en la tormenta REAL (llamarada " + h.flare.Short + "): evitó " + h.avoided.ToString("0") + " puntos de radiación.");
                break;
            }
        }
        if (diff.call && o.actsTotal[(int)TaskId.Call] == 0)
            Campaign.Lesson("Nunca llamaste a la Tierra. Su ayuda tarda noches en llegar: hay que pedirla con tiempo, no en plena emergencia.");
        if (o.reqKind >= 0)
            Campaign.Lesson("Tu última llamada llegó tarde. Por el retraso de la señal, la ayuda se pide ANTES de la emergencia.");
        if (o.actsTotal[(int)TaskId.Reinforce] == 0)
            Campaign.Lesson("Nunca reforzaste el escudo: la radiación diaria y las tormentas solares dañan la salud.");
        if (Campaign.tasksWasted >= 6)
            Campaign.Lesson("Dejaste " + Campaign.tasksWasted + " tareas sin usar. Podías reforzar el escudo, descansar o hacer experimentos.");
        if (Campaign.bufferLost >= 25f)
            Campaign.Lesson("Una tormenta del Sol borró " + Mathf.RoundToInt(Campaign.bufferLost / Tuning.ExperimentData) + " paquetes de datos. Los equipos reales mandan los datos a la Tierra lo antes posible.");
        if (o.DataPacks >= 2)
            Campaign.Lesson("Terminaste con " + o.DataPacks + " paquetes sin mandar. Los datos que no llegan a la Tierra no cuentan como ciencia.");
        if (cause == null && o.experiments == 0)
            Campaign.Lesson("No hiciste ningún experimento. Sobrevivir es lo primero, pero la misión también busca ciencia.");
        else if (cause == null && o.experiments < diff.goalPacks)
            Campaign.Lesson("Sobreviviste, pero la META era mandar " + diff.goalPacks + " paquetes de datos y mandaste " + o.experiments + ". Planea experimentos y subidas con tiempo.");
        if (Campaign.rescues > 0)
            Campaign.Lesson("La Tierra te salvó con " + Campaign.rescues + (Campaign.rescues == 1 ? " cápsula" : " cápsulas") + " de emergencia. Los equipos reales tienen planes de rescate, pero tardan: mejor no llegar a 0.");
        if (diff.morale && o.v[Outpost.M] < 25f)
            Campaign.Lesson("El ánimo quedó muy bajo: con poco ánimo hay menos tareas y rinden menos. Descansar a tiempo ayuda.");
    }

    void Finish(bool survived, string cause)
    {
        if (survived && o.experiments < diff.goalPacks) { survived = false; cause = "science"; }     // la ciencia es OBLIGATORIA
        finished = true; busy = true;
        CloseModal(); ClearHighlights();
        GameAudio.SetLoop(Loop.Alarm, false);
        GameAudio.SetLoop(Loop.Ambience, false);
        GameAudio.SetLoop(Loop.Downlink, false);

        Campaign.finished = true;
        Campaign.survived = survived;
        Campaign.endCause = survived ? "" : (cause ?? "health");
        Campaign.daysSurvived = daysDone;
        Campaign.experiments = o.experiments;
        Campaign.tasksUsed = o.tasksUsed;
        Campaign.tasksWasted = o.tasksWasted;
        Campaign.goalPacks = diff.goalPacks;
        Campaign.goalMet = !survived || o.experiments >= diff.goalPacks;
        for (int i = 0; i < 8; i++) Campaign.finalV[i] = o.v[i];
        LogEvent(survived ? (Campaign.goalMet ? "MISIÓN CUMPLIDA: todos sobrevivieron." : "SOBREVIVIERON, pero faltó ciencia.") : "MISIÓN PERDIDA: " + CauseText(Campaign.endCause),
                 survived ? (Campaign.goalMet ? LogKind.Good : LogKind.Warn) : LogKind.Bad);
        if (!survived && Campaign.endCause == "health") Campaign.deathReason = o.liveDead ?? o.DeathReason();
        BuildLessons(survived ? null : Campaign.endCause);

        bool blackout = !survived && Campaign.endCause == "power" && fx != null;
        if (survived) GameAudio.Play(Sfx.NightComplete);
        else if (blackout) GameAudio.Play(Sfx.Blackout);
        else { UIFx.RedOut(0.5f); GameAudio.Play(Sfx.MissionLost); }

        var sb = new StringBuilder();
        if (survived) sb.Append("¡Los 4 astronautas sobrevivieron " + totalDays + " noches en " + Session.Site.place + "!\n" +
                                (Campaign.goalMet ? "¡Cumpliste la META de ciencia!\n\n" : "Pero faltó ciencia: mandaste " + o.experiments + " de " + diff.goalPacks + " paquetes.\n\n"));
        else sb.Append(CauseText(Campaign.endCause) + "\nNoches que aguantaste: " + daysDone + " de " + totalDays + ".\n\n");
        sb.Append("Batería " + Mathf.RoundToInt(o.v[0]) + "   Aire " + Mathf.RoundToInt(o.v[1]) + "   Comida " + Mathf.RoundToInt(o.v[2]) + "\n");
        sb.Append((diff.house ? "Casa " + Mathf.RoundToInt(o.v[3]) + "   " : "") + "Salud " + Mathf.RoundToInt(o.v[5]) +
                  (diff.morale ? "   Ánimo " + Mathf.RoundToInt(o.v[6]) : "") + "\n");
        sb.Append("Ciencia entregada: " + o.experiments + "   Tareas sin usar: " + o.tasksWasted);

        string title = survived ? (Campaign.goalMet ? "¡MISIÓN CUMPLIDA!" : "¡SOBREVIVIERON!") : "MISIÓN PERDIDA";
        Action show = delegate
        {
            if (survived) ShowOverlay(title, sb.ToString(), "VER EL INFORME DE LA MISIÓN", delegate { if (onFinished != null) onFinished(); }, false);
            else ShowLossScreen(Campaign.endCause, delegate { if (onFinished != null) onFinished(); });
        };
        if (blackout) fx.PlayBlackout("Noche " + Mathf.Max(1, daysDone + 1), delegate { UIFx.RedOut(0.5f); show(); });
        else show();
    }

    // ================================================================ flujos EN VIVO (la noche corre mientras juegas)
    // Los paneles cargan la batería poco a poco, las máquinas encendidas la gastan, el polvo ensucia los paneles
    // y las cosas que fallan bajan el ánimo. Se pausa con menús, animaciones y el tutorial.
    void LiveStep()
    {
        if (TutorialRunning || busy || finished || modalRoot != null || holdFx || o.day > totalDays)
        {
            liveT = 0f;
            if (flowShown) { o.rateE = o.rateP = o.rateM = o.rateO = o.rateF = o.rateH = 0f; SetFlowTitles(); flowShown = false; }
            return;
        }
        liveT += Time.unscaledDeltaTime;
        if (liveT < 0.25f) return;
        float dt = liveT; liveT = 0f;
        bool changed = o.LiveTick(dt, events);
        if (o.liveRescued >= 0)
        {
            int ri = o.liveRescued; o.liveRescued = -1;
            changed = true;
            string rmsg = "¡CÁPSULA DE EMERGENCIA! La Tierra mandó " + Ledger.ResNames[ri] + " justo a tiempo (+" + Tuning.RescueAmount.ToString("0") + "). Quedan " + o.lifelines + ".";
            Campaign.Add(o.day, rmsg, LogKind.Good);
            SetBanner(rmsg, false);
            GameAudio.Play(Sfx.NightComplete);
            UIFx.Flash(new Color(0.45f, 1f, 0.55f), 0.4f);
            liveRefreshT = 99f;
        }
        liveEvtT += dt;
        if (liveEvtT >= Tuning.LiveEventSeconds)
        {
            liveEvtT = 0f;
            var ev = o.LiveEvent();
            if (ev != null)
            {
                changed = true;
                Campaign.Add(o.day, ev.text, ev.kind);
                var one = new List<EventNote>(); one.Add(ev);
                EventEffects(one);
                SetBanner("¡IMPREVISTO! " + ev.text, false);
                liveRefreshT = 99f;
            }
        }
        SetFlowTitles();
        UpdateTempIcon();
        flowShown = changed;
        if (o.liveDead != null) { LiveDeath(); return; }       // la salud llegó a 0: el equipo murió
        if (!changed) return;
        liveRefreshT += dt;
        int tier = o.MoodTier();
        if (tier != liveTier || liveRefreshT >= 1f) { liveTier = tier; liveRefreshT = 0f; Refresh(); }
    }

    // Copo de nieve (frío) o llama (calor) junto al astronauta.
    void UpdateTempIcon()
    {
        if (tempIcon == null || o == null) return;
        int st = o.TempState;
        int tc = Mathf.RoundToInt(o.TempC);
        float frac = Mathf.Clamp01((o.bodyTemp + 100f) / 200f);                    // 0 = congelado, 0.5 = normal, 1 = hirviendo
        float amt = Mathf.Clamp01(Mathf.Abs(o.bodyTemp) / Mathf.Max(1f, Tuning.TempIconAt));
        Color bc = o.bodyTemp < 0f ? Color.Lerp(Gfx.Good, new Color(0.30f, 0.62f, 1f, 1f), amt) : Color.Lerp(Gfx.Good, new Color(1f, 0.25f, 0.18f, 1f), amt);
        if (tempFillRt != null)
        {
            tempFillRt.anchorMax = new Vector2(0.86f, Mathf.Lerp(0.02f, 0.98f, frac));
            tempFillRt.offsetMin = Vector2.zero; tempFillRt.offsetMax = Vector2.zero;
            tempFillImg.color = bc;
        }
        if (tempCounter != null)
        {
            float yc = Mathf.Lerp(0.20f, 0.88f, frac);
            tempCounter.rectTransform.anchorMin = new Vector2(0.10f, yc - 0.06f); tempCounter.rectTransform.anchorMax = new Vector2(0.45f, yc + 0.06f);
            tempCounter.rectTransform.offsetMin = Vector2.zero; tempCounter.rectTransform.offsetMax = Vector2.zero;
            if (tc != tempCShown) { tempCShown = tc; tempCounter.text = tc + "°C"; }
            tempCounter.color = bc;
        }
        if (st != tempShown)
        {
            tempShown = st;
            if (st < 0) GameAudio.Play(Sfx.FreezeWarning); else if (st > 0) GameAudio.Play(Sfx.OverheatWarning);
            if (st != 0) { UIFx.Flash(st < 0 ? new Color(0.55f, 0.8f, 1f) : new Color(1f, 0.55f, 0.25f), 0.35f); }
            tempIcon.sprite = Art.Body(st < 0 ? "snow" : "flame");
            tempIcon.color = st < 0 ? new Color(0.55f, 0.85f, 1f, 1f) : new Color(1f, 0.55f, 0.2f, 1f);
            tempIcon.gameObject.SetActive(st != 0); tempLabel.gameObject.SetActive(st != 0);
            tempPulse.onColor = tempIcon.color;
        }
        if (st != 0)
        {
            bool hurt = Mathf.Abs(o.bodyTemp) >= Tuning.TempDamageAt;
            tempPulse.speed = hurt ? 9f : 4f;
            tempLabel.text = (st < 0 ? "CONGELADOS" : "¡CALOR!") + (hurt ? "\n<color=#ff6a5a>pierden salud</color>" : "");
            tempLabel.color = tempIcon.color;
        }
    }

    // La salud llegó a 0 mientras jugabas (sin aire, ánimo muy bajo, casa rota...): fin de la misión.
    void LiveDeath()
    {
        if (finished || o == null) return;
        daysDone = Mathf.Max(0, o.day - 1);
        AddSample(true);
        fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false;
        Finish(false, o.liveDead == "power" ? "power" : "health");        // batería en 0 = apagón
    }

    void SetFlowTitles()
    {
        FlowTitle(Outpost.E, o.rateE);
        FlowTitle(Outpost.P, o.rateP);
        FlowTitle(Outpost.M, o.rateM);
        FlowTitle(Outpost.O, o.rateO);
        FlowTitle(Outpost.F, o.rateF);
        FlowTitle(Outpost.H, o.rateH);
    }

    void FlowTitle(int i, float rate)
    {
        if (gauges[i] == null) return;
        string t = ResLabels[i];
        if (Mathf.Abs(rate) >= 0.03f)
            t += "  <color=" + (rate > 0f ? "#73ff8c" : "#ff6a5a") + ">" + (rate > 0f ? "+" : "-") + Mathf.Abs(rate).ToString("0.0#") + "/s</color>";
        gauges[i].SetTitle(t);
    }

    // ================================================================ pantalla de PÉRDIDA (gráfica)
    static string LossTitle(string cause)
    {
        switch (cause)
        {
            case "power": return "SIN BATERÍA";
            case "oxygen": return "SIN AIRE";
            case "health": return "EL EQUIPO MURIÓ";
            case "structure": return "CASA DERRUMBADA";
            case "science": return "SIN CIENCIA";
        }
        return "FIN DE LA MISIÓN";
    }

    static string LossIcon(string cause)
    {
        switch (cause)
        {
            case "power": return "battery";
            case "oxygen": return "air";
            case "health": return "astro";
            case "structure": return "house";
            case "science": return "flask";
        }
        return "flame";
    }

    static int LossRes(string cause)
    {
        switch (cause)
        {
            case "power": return Outpost.E;
            case "oxygen": return Outpost.O;
            case "health": return Outpost.H;
            case "structure": return Outpost.I;
        }
        return -1;
    }

    static string LossTip(string cause)
    {
        switch (cause)
        {
            case "power": return "Los PANELES dan la batería: límpialos y no los dejes sucios. Apaga las máquinas que no uses. ¡DESCANSAR no gasta batería!";
            case "oxygen": return "Deja siempre encendida la MÁQUINA DE AIRE y repárala en cuanto se rompa. Una fuga de aire se paga cara.";
            case "health":
                switch (Campaign.deathReason)
                {
                    case "air": return "Sin aire la salud cae a 4 por segundo. Mantén la MÁQUINA DE AIRE encendida y repárala al instante.";
                    case "food": return "Cada tarea da hambre: deja el INVERNADERO encendido y no abuses de las tareas.";
                    case "house": return "Con la casa rota el equipo se muere. REPARA y no dejes el escudo en 0: sin escudo el daño es doble.";
                    case "mood": return "Con el ánimo bajo la salud baja cada segundo. Usa DESCANSAR y arregla lo que falla.";
                    case "cold": return "Enciende la CALEFACCIÓN antes de la noche larga: sin ella el equipo se congela.";
                    case "heat": return "Durante una tormenta solar apaga la CALEFACCIÓN si no hace frío: si no, el equipo se acalora.";
                }
                return "La radiación se acumula. Refuerza el ESCUDO antes de cada tormenta del Sol y deja DESCANSAR al equipo.";
            case "structure": return "Enciende la CALEFACCIÓN en la noche larga y usa REPARAR: el frío y las rocas dañan la casa.";
            case "science": return "Mandar datos es OBLIGATORIO. Haz un EXPERIMENTO casi todas las noches y súbelos con la antena apuntando a la TIERRA.";
        }
        return "Mira el PRONÓSTICO cada noche y guarda siempre una tarea de reserva.";
    }

    void ShowLossScreen(string cause, Action onClick)
    {
        var m = OpenModal(0.55f);
        var tint = UIKit.Panel(m, "LostTint", 0f, 0f, 1f, 1f, new Color(0.70f, 0.02f, 0.02f, 0.40f));
        tint.GetComponent<Image>().raycastTarget = false;
        tint.gameObject.AddComponent<CanvasGroup>();
        tint.gameObject.AddComponent<PulseAlpha>();             // el rojo late
        UIFx.Flash(new Color(1f, 0.1f, 0.05f), 0.6f);
        UIFx.Shake(14f, 0.8f);

        var p = UIKit.Panel(m, "Loss", 0.08f, 0.05f, 0.92f, 0.95f, new Color(0.06f, 0.015f, 0.02f, 0.99f));
        UIKit.Border(p, UIKit.Red, 4f);
        bool power = cause == "power";
        int failRes = LossRes(cause);
        Color deadRed = new Color(0.42f, 0.10f, 0.10f, 1f);

        // ---- título
        UIKit.Label(p, "MISIÓN PERDIDA", 16, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.50f, 1f), 0f, 0.935f, 1f, 0.99f);
        UIKit.Label(p, LossTitle(cause), 44, TextAnchor.MiddleCenter, UIKit.Red, 0f, 0.82f, 1f, 0.94f);

        // ---- dibujo grande de lo que falló (late en rojo; la batería parpadea con "0 %")
        var big = Gfx.Icon(p, LossIcon(cause), 0.04f, 0.40f, 0.32f, 0.80f, deadRed);
        var bp = big.gameObject.AddComponent<Pulser>();
        bp.onColor = UIKit.Red; bp.offColor = UIKit.Red; bp.amount = power ? 0.03f : 0.08f; bp.speed = power ? 14f : 3f;
        if (power) UIKit.Label(big.transform, "0 %", 40, TextAnchor.MiddleCenter, Color.white, 0f, 0.12f, 1f, 0.50f);
        bool sci = cause == "science";
        bool death = cause == "health";
        if (death) UIKit.Label(big.transform, "0 %", 40, TextAnchor.MiddleCenter, Color.white, 0f, 0.05f, 1f, 0.40f);
        if (sci) UIKit.Label(big.transform, o.experiments + "/" + diff.goalPacks, 30, TextAnchor.MiddleCenter, Color.white, 0f, 0.05f, 1f, 0.40f);
        Gfx.Icon(p, "cross", 0.255f, 0.40f, 0.335f, 0.50f, Gfx.Bad);

        // ---- qué pasó y cuántas noches aguantaste (una bolita por noche)
        UIKit.Label(p, CauseText(cause), 20, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.88f, 1f), 0.36f, 0.62f, 0.97f, 0.80f);
        UIKit.Label(p, sci ? "Sobrevivieron, pero mandaste <color=#ffc033>" + o.experiments + " de " + diff.goalPacks + " paquetes</color>"
                           : "Aguantaste <color=#ffc033>" + daysDone + " de " + totalDays + " noches</color>", 20, TextAnchor.MiddleLeft,
                    UIKit.Phosphor, 0.36f, 0.52f, 0.97f, 0.62f);
        int nN = death ? 0 : Mathf.Max(1, totalDays);
        float dsz = Mathf.Clamp(300f / Mathf.Max(1, nN), 10f, 24f);
        if (death)
        {
            // electrocardiograma: unos latidos y luego la línea plana
            float[] beat = { 0.15f, 0.15f, 0.55f, 1f, 0.25f, 0.7f, 0.15f, 0.15f };
            for (int i = 0; i < 26; i++)
            {
                float hgt = i < 12 ? beat[i % beat.Length] : 0.04f;
                float bx = 0.375f + i * 0.0225f;
                var bar = UIKit.Panel(p, "ecg", bx, 0.47f - hgt * 0.04f, bx + 0.012f, 0.47f + hgt * 0.04f, i < 12 ? new Color(1f, 0.35f, 0.30f, 1f) : new Color(0.75f, 0.15f, 0.12f, 1f));
                bar.GetComponent<Image>().raycastTarget = false;
                if (i >= 12) bar.gameObject.AddComponent<CanvasGroup>().alpha = 1f;
            }
        }
        for (int i = 0; i < nN; i++)
        {
            Color dc = i < daysDone ? Gfx.Good : (i == daysDone ? Gfx.Bad : Gfx.Dim);
            var dt = Gfx.Dot(p, "circle", 0.375f + (i + 0.5f) * (0.58f / nN), 0.47f, dsz, dc);
            if (i == daysDone)
            {
                var dpu = dt.gameObject.AddComponent<Pulser>();
                dpu.onColor = Gfx.Bad; dpu.amount = 0.30f; dpu.speed = 7f;
            }
        }

        // ---- cómo estaba cada cosa al final (la que falló, marcada en rojo)
        int[] order = { 0, 1, 2, 5, 3, 6 };
        var list = new List<int>();
        for (int k = 0; k < order.Length; k++) if (Session.ResShown(order[k])) list.Add(order[k]);
        float tw = 0.94f / list.Count;
        for (int k = 0; k < list.Count; k++)
        {
            int i = list[k];
            float v01 = Mathf.Clamp01(o.v[i] / 100f);
            bool bad = i == failRes;
            float x0 = 0.03f + k * tw;
            var tile = UIKit.Panel(p, "Tile", x0 + 0.004f, 0.205f, x0 + tw - 0.004f, 0.385f, bad ? new Color(0.25f, 0.04f, 0.04f, 1f) : new Color(0.08f, 0.07f, 0.07f, 1f));
            UIKit.Border(tile, bad ? UIKit.Red : new Color(0.25f, 0.22f, 0.22f, 1f), bad ? 3f : 2f);
            Image ti;
            if (i == Outpost.M) ti = Gfx.Face(tile, v01, 0.25f, 0.34f, 0.75f, 0.95f);
            else ti = Gfx.Icon(tile, ResIcons[i], 0.25f, 0.34f, 0.75f, 0.95f, bad ? UIKit.Red : Gfx.Level(v01));
            if (bad)
            {
                var tp = ti.gameObject.AddComponent<Pulser>();
                tp.onColor = UIKit.Red; tp.amount = 0.10f; tp.speed = 8f;
            }
            UIKit.Label(tile, ResLabels[i] + "  " + Mathf.RoundToInt(o.v[i]) + "%", 13, TextAnchor.MiddleCenter,
                        bad ? UIKit.Red : new Color(0.80f, 0.85f, 0.82f, 1f), 0f, 0.02f, 1f, 0.33f);
        }

        // ---- consejo para la próxima vez
        var tip = UIKit.Panel(p, "Tip", 0.03f, 0.10f, 0.97f, 0.195f, new Color(0.16f, 0.10f, 0.02f, 1f));
        UIKit.Border(tip, UIKit.Amber, 2f);
        var star = Gfx.IconLeft(tip, "star4", 34f, 10f, UIKit.Amber);
        var spu = star.gameObject.AddComponent<Pulser>();
        spu.onColor = UIKit.Amber; spu.amount = 0.15f; spu.speed = 5f;
        var tt = UIKit.Label(tip, "<color=#ffc033>PARA LA PRÓXIMA:</color> " + LossTip(cause), 16, TextAnchor.MiddleLeft, UIKit.RadioText, 0f, 0f, 1f, 1f);
        tt.rectTransform.offsetMin = new Vector2(52f, tt.rectTransform.offsetMin.y);

        // ---- botón
        var go = UIKit.MakeButton(p, "VER EL INFORME DE LA MISIÓN", 20, 0.26f, 0.012f, 0.74f, 0.088f, UIKit.Selected, onClick, TextAnchor.MiddleCenter, Sfx.BtnOverlay);
        var gt = go.GetComponentInChildren<Text>();
        if (gt != null) gt.rectTransform.offsetMin = new Vector2(44f, gt.rectTransform.offsetMin.y);
        UIKit.Border((RectTransform)go.transform, UIKit.Phosphor, 3f);
        var fl = Gfx.IconLeft(go.transform, "flame", 36f, 12f, UIKit.Amber);
        var fpu = fl.gameObject.AddComponent<Pulser>();
        fpu.onColor = UIKit.Amber; fpu.amount = 0.18f; fpu.speed = 6f;

        FxLayer.Ring(big.transform, UIKit.Red, 300f, 1.4f);
        FxLayer.Burst(big.transform, new Color(1f, 0.35f, 0.25f, 1f), 16, 220f, 13f, "circle");
    }

    // lost = true: toda la pantalla se pone roja (pulsando) y el cuadro de resumen queda encima, con marco.
    void ShowOverlay(string title, string body, string button, Action onClick, bool lost)
    {
        var m = OpenModal(lost ? 0.30f : 0.65f);
        if (lost)
        {
            var tint = UIKit.Panel(m, "LostTint", 0f, 0f, 1f, 1f, new Color(0.75f, 0.02f, 0.02f, 0.55f));
            tint.GetComponent<Image>().raycastTarget = false;
            tint.gameObject.AddComponent<CanvasGroup>();
            tint.gameObject.AddComponent<PulseAlpha>();       // el rojo late
            UIFx.Flash(new Color(1f, 0.1f, 0.05f), 0.6f);
            UIFx.Shake(14f, 0.8f);
        }
        var p = UIKit.Panel(m, "Overlay", 0.26f, 0.22f, 0.74f, 0.78f, new Color(0.03f, 0.03f, 0.03f, 0.99f));
        UIKit.Border(p, lost ? UIKit.Red : UIKit.Phosphor, 4f);
        UIKit.Label(p, title, 28, TextAnchor.MiddleCenter, lost ? UIKit.Red : UIKit.Amber, 0f, 0.82f, 1f, 1f);
        UIKit.ScrollText(p, body, 16, UIKit.Phosphor, 0.04f, 0.26f, 0.96f, 0.82f);
        UIKit.MakeButton(p, button, 16, 0.18f, 0.05f, 0.82f, 0.21f, UIKit.Selected, onClick, TextAnchor.MiddleCenter, Sfx.BtnOverlay);
    }
}