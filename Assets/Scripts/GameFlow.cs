using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

// ÚNICO script que hay que poner en la escena (en un GameObject vacío). Agrega GameAudio solo.
[RequireComponent(typeof(GameAudio))]
public class GameFlow : MonoBehaviour
{
    public Font pixelFont;     // opcional: una fuente pixelada (.ttf) para look PSX
    public Camera uiCamera;    // opcional: arrástrale la cámara PSX para que la UI también se pixelee
    public Sprite logoSprite;  // opcional: tu logo (Texture Type = Sprite). Si lo dejas vacío se dibuja uno por código.
    public bool showBootScreen = true;
    [Range(1f, 1.5f)] public float textScale = 1f;   // tamaño de TODOS los textos (1 = ya viene grande, 1.5 = el máximo; los textos largos se pueden desplazar)

    [Header("Datos reales de la NASA")]
    public bool useLiveDonki = true;          // al abrir el juego intenta descargar las llamaradas solares más recientes de NASA DONKI
    public string nasaApiKey = "DEMO_KEY";    // clave gratis de api.nasa.gov (DEMO_KEY sirve para probar). Sin internet se usa el archivo guardado.

    [Header("Efectos de pantalla (también se cambian con ESC en el juego)")]
    public bool impactEffects = true;   // sacudidas, destellos y parpadeo de apagón
    public bool crtLines = true;        // líneas de barrido CRT

    [Header("DEBUG (solo para probar): marca una casilla durante el juego y se desmarca sola")]
    public bool debugNextDay;        // termina el día en curso al instante (sin confirmar tareas)
    public bool debugStormTomorrow;  // programa una tormenta solar para mañana (para probar el blindaje)
    public bool debugWinMission;     // termina la misión como COMPLETADA
    public bool debugLoseMission;    // termina la misión como PERDIDA (salud)
    public bool debugBatteryZero;    // energía 0: apagón + cinemática roja + resumen
    public bool debugGoToReview;     // salta directo al informe final

    [Header("DEBUG: frío, calor, muertes, apagón e imprevistos (marca una casilla durante el juego)")]
    public bool debugFreeze;           // el equipo se CONGELA: sale el copo de nieve y empieza a perder salud y ánimo
    public bool debugOverheat;         // el equipo se ACALORA: sale la llama
    public bool debugDieFrozen;        // muerte por CONGELAMIENTO (pantalla de pérdida)
    public bool debugDieHeat;          // muerte por CALOR (pantalla de pérdida)
    public bool debugDiePlayer;        // muerte del equipo por salud 0 (causa: radiación)
    public bool debugDieNoAir;         // muerte por falta de aire
    public bool debugDieMood;          // muerte por ánimo muy bajo
    public bool debugDieHouse;         // muerte por casa rota
    public bool debugMachinesBlackout; // batería casi en 0 con las 3 máquinas encendidas: llega a 0 y se produce el apagón (fin de la misión)
    public bool debugLiveEvent;        // lanza un imprevisto EN VIVO al azar ahora mismo
    [Tooltip("Escribe un número del 0 al 17 para lanzar ESE imprevisto malo (vuelve a -1 solo). 8 = avería, 9 = filtro de aire, 10 = plaga, 11 = escudo, 13 = celda de batería, 15 = fiebre, 16 = comida mala, 17 = sobrecarga")]
    public int debugBadEvent = -1;


    RectTransform canvasRect, contentRect;
    BootScreen boot;
    DesignScreen design;
    ReviewScreen review;
    GameObject shiftObj;

    void Start()
    {
        UIKit.CustomFont = pixelFont;
        UIKit.TextScale = Mathf.Clamp(textScale, UIKit.MinScale, UIKit.MaxScale);
        var canvas = UIKit.CreateCanvas(transform, uiCamera);
        canvasRect = (RectTransform)canvas.transform;
        contentRect = UIKit.Root(canvasRect, "Content");     // aquí van todas las pantallas (es lo que se sacude)
        UIFx.Create(canvasRect, contentRect);
        UIFx.Enabled = impactEffects;
        UIFx.Scanlines = crtLines;
        PauseMenu.Create(canvasRect, RestartRun);            // ESC = pausa
        if (useLiveDonki) StartCoroutine(FetchDonki());
        if (showBootScreen) ShowBoot();
        else ShowDesign();
    }

    // Descarga las llamaradas solares de los últimos 120 días de DONKI (NASA). Si falla, el juego usa su lista guardada.
    IEnumerator FetchDonki()
    {
        NasaData.LiveState = 1;
        System.DateTime end = System.DateTime.UtcNow;
        System.DateTime start = end.AddDays(-120);
        string url = "https://api.nasa.gov/DONKI/FLR?startDate=" + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                     "&endDate=" + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&api_key=" + nasaApiKey;
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();
            bool ok;
#if UNITY_2020_2_OR_NEWER
            ok = req.result == UnityWebRequest.Result.Success;
#else
            ok = !req.isNetworkError && !req.isHttpError;
#endif
            if (ok)
            {
                int n = NasaData.ParseFlares(req.downloadHandler.text);
                NasaData.LiveState = 2;
                Debug.Log("[NASA DONKI] Descarga en vivo correcta: " + n + " llamaradas fuertes en los últimos 120 días.");
            }
            else
            {
                NasaData.LiveState = 3;
                Debug.Log("[NASA DONKI] No se pudo descargar (" + req.error + "). Se usa el archivo guardado.");
            }
        }
        if (design != null) design.RefreshDataStatus();
    }

    ShiftScreen Shift()
    {
        var shift = shiftObj != null ? shiftObj.GetComponent<ShiftScreen>() : null;
        if (shift == null) Debug.Log("[DEBUG] No hay una misión en curso: pulsa INICIAR MISIÓN primero.");
        return shift;
    }

    void Update()
    {
        if (debugNextDay) { debugNextDay = false; var s = Shift(); if (s != null) s.DebugNextDay(); }
        if (debugStormTomorrow) { debugStormTomorrow = false; var s = Shift(); if (s != null) s.DebugStormTomorrow(); }
        if (debugWinMission) { debugWinMission = false; var s = Shift(); if (s != null) s.DebugFinish(true); }
        if (debugLoseMission) { debugLoseMission = false; var s = Shift(); if (s != null) s.DebugFinish(false); }
        if (debugBatteryZero) { debugBatteryZero = false; var s = Shift(); if (s != null) s.DebugBlackout(); }
        if (debugFreeze) { debugFreeze = false; var s = Shift(); if (s != null) s.DebugSetTemp(-60f); }
        if (debugOverheat) { debugOverheat = false; var s = Shift(); if (s != null) s.DebugSetTemp(60f); }
        if (debugDieFrozen) { debugDieFrozen = false; var s = Shift(); if (s != null) s.DebugDie("cold"); }
        if (debugDieHeat) { debugDieHeat = false; var s = Shift(); if (s != null) s.DebugDie("heat"); }
        if (debugDiePlayer) { debugDiePlayer = false; var s = Shift(); if (s != null) s.DebugDie("radiation"); }
        if (debugDieNoAir) { debugDieNoAir = false; var s = Shift(); if (s != null) s.DebugDie("air"); }
        if (debugDieMood) { debugDieMood = false; var s = Shift(); if (s != null) s.DebugDie("mood"); }
        if (debugDieHouse) { debugDieHouse = false; var s = Shift(); if (s != null) s.DebugDie("house"); }
        if (debugMachinesBlackout) { debugMachinesBlackout = false; var s = Shift(); if (s != null) s.DebugMachinesBlackout(); }
        if (debugLiveEvent) { debugLiveEvent = false; var s = Shift(); if (s != null) s.DebugLiveEvent(); }
        if (debugBadEvent >= 0) { int n = debugBadEvent; debugBadEvent = -1; var s = Shift(); if (s != null) s.DebugBadEvent(n); }

        if (debugGoToReview)
        {
            debugGoToReview = false;
            if (!Campaign.finished && Campaign.history.Count == 0) Campaign.Reset();
            ShowReview();
        }
    }

    void ClearAll()
    {
        UIFx.ClearRed();
        if (boot != null) Destroy(boot.gameObject);
        if (design != null) design.Destroy();
        if (review != null) review.Destroy();
        if (shiftObj != null) Destroy(shiftObj);
        boot = null; design = null; review = null; shiftObj = null;
    }

    void ShowBoot()
    {
        ClearAll();
        GameAudio.StopAllLoops();
        var go = new GameObject("BootScreen", typeof(RectTransform));
        go.transform.SetParent(contentRect, false);
        boot = go.AddComponent<BootScreen>();
        boot.Begin(logoSprite, ShowDesign);
    }

    void RestartRun() { ShowDesign(); }

    void ToggleMode()
    {
        Session.tutorial = !Session.tutorial;
        ShowDesign();
    }

    void SetDifficulty(int level)
    {
        Session.difficulty = Mathf.Clamp(level, 0, Tuning.Difficulties.Length - 1);
        ShowDesign();
    }

    void ShowDesign()
    {
        ClearAll();
        Campaign.Reset();
        GameAudio.StopAllLoops();
        GameAudio.SetLoop(Loop.MusicDesign, true);
        design = new DesignScreen(contentRect, StartMission, ToggleMode, SetDifficulty);
    }

    void StartMission()
    {
        ClearAll();
        GameAudio.StopAllLoops();
        shiftObj = new GameObject("ShiftScreen", typeof(RectTransform));
        shiftObj.transform.SetParent(contentRect, false);
        UIKit.Stretch((RectTransform)shiftObj.transform);
        var shift = shiftObj.AddComponent<ShiftScreen>();
        shift.Begin(ShowReview);
    }

    void ShowReview()
    {
        ClearAll();
        GameAudio.StopAllLoops();
        GameAudio.SetLoop(Loop.MusicReview, true);
        review = new ReviewScreen(contentRect, ShowDesign);
    }
}