using System.Collections.Generic;
using UnityEngine;

// Cada valor es un "momento" del juego. El juego pide Play(Sfx.X) y GameAudio decide cómo suena.
public enum Sfx
{
    // Interfaz
    UiHover, UiClick, UiDenied,
    // Diseño
    DesignSelect, DesignDeselect, LaunchConfirm,
    // Radio / tutorial
    RadioOpen, RadioNext, StepComplete, Unlock,
    // Comandos
    CmdSend, CmdArrive,
    CmdHeaterOn, CmdHeaterOff, CmdPayloadOn, CmdPayloadOff, CmdAntennaOn, CmdAntennaOff,
    CmdPointSun, CmdPointEarth, CmdDesaturate, CmdSafeMode,
    // Eventos de la misión
    LinkOpen, LinkClose, EclipseStart, EclipseEnd, DataChime,
    AlertWarning, FlareImpact, WheelImpact, DustImpact, ResponseGood, ResponseBad,
    BatteryLow, BatteryCritical, FreezeWarning, OverheatWarning, Blackout,
    ManualOpen, ManualClose,
    NightStart, HourChime, NightComplete, MissionLost, ReviewOpen,
    // Pantalla de carga
    BootLogo, BootTick, BootReady, BootStart,
    // Botones con sonido propio
    BtnMode, BtnOverlay, BtnRedesign, ModLauncher, ModPower, ModComms, ModPayload, ModSafety
}

public enum Loop { Ambience, Alarm, Downlink, MusicDesign, MusicReview, MusicBoot, Tension }

// Grupo del sonido: en Ui, Cmd y Story solo puede sonar UNO a la vez (el nuevo reemplaza al viejo si es igual o más importante).
public enum Cat { Ui, Cmd, Event, Alert, Story }

[System.Serializable]
public class SfxSlot
{
    public string name;                 // solo para que el Inspector se lea bonito
    public Sfx id;
    public AudioClip clip;              // OPCIONAL: si arrastras uno aquí, usa SOLO este (en vez del pack)
    [Range(0f, 2f)] public float gain = 1f;   // multiplicador de volumen de esta acción

    [System.NonSerialized] public AudioClip[] pool;   // pool[0] = el clip de esta acción
    [System.NonSerialized] public int prio;
    [System.NonSerialized] public Cat cat;
    [System.NonSerialized] public float baseVol = 1f;
    [System.NonSerialized] public float cooldown;
    [System.NonSerialized] public bool pitch;
    [System.NonSerialized] public float lastTime = -99f;
}

[System.Serializable]
public class LoopSlot
{
    public string name;
    public Loop id;
    public AudioClip clip;              // OPCIONAL: si arrastras uno aquí, usa este en vez del pack
    [Range(0f, 2f)] public float gain = 1f;

    [System.NonSerialized] public AudioClip use;
    [System.NonSerialized] public float baseVol = 0.3f;
}

// Pon este componente en el mismo GameObject que GameFlow (se agrega solo).
public class GameAudio : MonoBehaviour
{
    public static GameAudio I;

    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;   // música y ambiente
    [Range(0f, 1f)] public float sfxVolume = 1f;     // efectos

    [Header("Sonido de escritura del tutorial (estilo Undertale): ASÍGNALO AQUÍ")]
    public AudioClip typewriterSound;
    [Range(0f, 1f)] public float typewriterVolume = 0.5f;

    [Header("1) Los 100 sonidos del pack: arrastra cada clip a su campo (o se llenan solos)")]
    public LofiPack pack = new LofiPack();

    [Header("2) Opcional: un clip aquí manda sobre el del pack para esa acción")]
    public List<SfxSlot> sfx = new List<SfxSlot>();
    public List<LoopSlot> loops = new List<LoopSlot>();

    // ---- Mezcla inteligente
    const int MaxVoices = 4;            // máximo de sonidos sueltos a la vez
    const int PoolSize = 6;

    class Voice { public AudioSource src; public int prio; public Cat cat; public float start; public float vol; }
    class Pend { public Sfx id; public float t; }

    Voice[] voices;
    Dictionary<Sfx, SfxSlot> sfxMap;
    Dictionary<Loop, AudioSource> loopSources;
    Dictionary<Loop, LoopSlot> loopMap;
    readonly List<Pend> queue = new List<Pend>();
    float gateUntil;                    // los sonidos importantes no se pisan: esperan su turno
    float duckUntil;
    float duck = 1f;
    AudioSource typeSrc;

    void Awake()
    {
        I = this;
        voices = new Voice[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f;
            voices[i] = new Voice { src = src };
        }
        typeSrc = gameObject.AddComponent<AudioSource>();
        typeSrc.playOnAwake = false; typeSrc.spatialBlend = 0f;
        ApplyDefaults();
        sfxMap = new Dictionary<Sfx, SfxSlot>();
        foreach (var s in sfx) sfxMap[s.id] = s;
        loopSources = new Dictionary<Loop, AudioSource>();
        loopMap = new Dictionary<Loop, LoopSlot>();
        foreach (var l in loops)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = l.use; src.loop = true; src.playOnAwake = false; src.spatialBlend = 0f;
            src.volume = l.baseVol * l.gain * masterVolume;
            loopSources[l.id] = src;
            loopMap[l.id] = l;
        }
    }

    // ---------------------------------------------------------------- API estática (null-safe)
    public static void Play(Sfx id) { if (I != null) I.PlayInternal(id, false); }

    // Reproduce "specific"; si no tiene clip usa "fallback".
    public static void PlayOr(Sfx specific, Sfx fallback)
    {
        if (I == null) return;
        SfxSlot slot;
        if (I.sfxMap.TryGetValue(specific, out slot) && slot.pool != null && slot.pool.Length > 0) I.PlayInternal(specific, false);
        else I.PlayInternal(fallback, false);
    }

    public static void SetLoop(Loop id, bool on)
    {
        if (I == null) return;
        I.SetLoopRaw(id, on);
        if (id == Loop.Alarm) I.SetLoopRaw(Loop.Tension, on);   // la alarma trae una capa de tensión
    }

    // Un "blip" de escritura (lo llama Typewriter por cada letra). Si no asignaste clip, no suena nada.
    public static void Blip()
    {
        if (I == null || I.typewriterSound == null || I.typeSrc == null) return;
        I.typeSrc.PlayOneShot(I.typewriterSound, I.typewriterVolume * I.masterVolume * I.sfxVolume);
    }

    public static void StopAllLoops()
    {
        if (I == null) return;
        foreach (var kv in I.loopSources) if (kv.Value.isPlaying) kv.Value.Stop();
    }

    void SetLoopRaw(Loop id, bool on)
    {
        AudioSource src;
        if (!loopSources.TryGetValue(id, out src) || src.clip == null) return;
        if (on && !src.isPlaying) src.Play();
        else if (!on && src.isPlaying) src.Stop();
    }

    static bool Exclusive(Cat c) { return c == Cat.Ui || c == Cat.Cmd || c == Cat.Story; }

    void PlayInternal(Sfx id, bool fromQueue)
    {
        SfxSlot s;
        if (!sfxMap.TryGetValue(id, out s) || s.pool == null || s.pool.Length == 0) return;
        float now = Time.unscaledTime;
        if (now - s.lastTime < s.cooldown) return;

        // Un sonido importante no se pisa con otro: el importante espera su turno, el menor se descarta.
        if (!fromQueue && s.prio >= 1 && now < gateUntil)
        {
            if (s.prio >= 2) Enqueue(id, gateUntil);
            return;
        }

        int active = 0;
        Voice lowest = null, sameCat = null;
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            if (!v.src.isPlaying) continue;
            active++;
            if (Exclusive(s.cat) && v.cat == s.cat) sameCat = v;
            if (lowest == null || v.prio < lowest.prio || (v.prio == lowest.prio && v.start < lowest.start)) lowest = v;
        }
        if (s.prio == 0 && active >= 2) return;     // lo suave no se suma a una mezcla ocupada
        if (s.prio == 1 && active >= 3) return;

        Voice use = null;
        if (sameCat != null)
        {
            if (sameCat.prio > s.prio) return;      // ya suena algo más importante de este grupo
            use = sameCat;
        }
        else if (active >= MaxVoices)
        {
            if (lowest == null || lowest.prio > s.prio) return;
            use = lowest;
        }
        else
        {
            for (int i = 0; i < voices.Length; i++) if (!voices[i].src.isPlaying) { use = voices[i]; break; }
        }
        if (use == null) return;

        var clip = s.pool[0];       // siempre el mismo clip: así cada botón se reconoce
        if (clip == null) return;

        use.src.Stop();
        use.src.pitch = s.pitch ? UnityEngine.Random.Range(0.94f, 1.06f) : 1f;
        use.src.clip = clip;
        use.vol = s.baseVol * s.gain * masterVolume * sfxVolume;
        use.src.volume = use.vol;
        use.prio = s.prio; use.cat = s.cat; use.start = now;
        use.src.Play();
        s.lastTime = now;

        if (s.prio >= 1) gateUntil = now + (s.prio >= 3 ? 0.45f : s.prio == 2 ? 0.30f : 0.12f);

        // Sonidos importantes: bajan los demás un momento para que se escuchen limpios.
        if (s.prio >= 2)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var v = voices[i];
                if (v != use && v.src.isPlaying && v.prio < s.prio) v.src.volume = v.vol * 0.35f;
            }
            duckUntil = Mathf.Max(duckUntil, now + Mathf.Min(clip.length, 3f) * 0.8f);
        }
    }

    void Enqueue(Sfx id, float t)
    {
        if (queue.Count >= 3) return;
        for (int i = 0; i < queue.Count; i++) if (queue[i].id == id) return;
        queue.Add(new Pend { id = id, t = t });
    }

    void Update()
    {
        float now = Time.unscaledTime;

        // cola de sonidos que esperaban su turno: sale uno por vez
        if (queue.Count > 0)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].t > now) continue;
                var p = queue[i];
                queue.RemoveAt(i);
                PlayInternal(p.id, true);
                for (int j = 0; j < queue.Count; j++) queue[j].t = Mathf.Max(queue[j].t, gateUntil);
                break;
            }
        }

        // los bucles (música/ambiente) bajan cuando suena algo importante
        float target = now < duckUntil ? 0.4f : 1f;
        duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * (target < duck ? 6f : 1.2f));
        foreach (var kv in loopSources)
        {
            LoopSlot l;
            if (loopMap.TryGetValue(kv.Key, out l)) kv.Value.volume = l.baseVol * l.gain * masterVolume * musicVolume * duck;
        }
    }

    // ---------------------------------------------------------------- Qué clips usa cada momento
    class Def
    {
        public Cat cat; public int prio; public float vol; public float cd; public bool pitch; public string[] clips;
    }

    // Un solo clip FIJO por acción. TODOS los botones normales usan Button_26.
    static Def D(Cat cat, int prio, float vol, float cd, string clip)
    {
        return new Def { cat = cat, prio = prio, vol = vol, cd = cd, pitch = false, clips = new string[] { clip } };
    }

    // Cat, prioridad (0 suave ... 3 crítico), volumen, cooldown y el clip del pack.
    // Para cambiar el sonido de una acción, cambia el nombre del clip aquí.
    static readonly Dictionary<Sfx, Def> SfxDefaults = new Dictionary<Sfx, Def>
    {
        // Interfaz
        { Sfx.UiHover,         D(Cat.Ui, 0, 0.22f, 0.15f, "Menu_01") },
        { Sfx.UiClick,         D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.UiDenied,        D(Cat.Ui, 1, 0.55f, 0.15f, "Error_03") },
        { Sfx.BtnMode,         D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },       // botón MODO: TUTORIAL/DIRECTO
        { Sfx.BtnOverlay,      D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },       // SIGUIENTE NOCHE / VER INFORME
        { Sfx.BtnRedesign,     D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },       // REDISEÑAR Y VOLVER A INTENTAR
        // Diseño
        { Sfx.ModLauncher,     D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.ModPower,        D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.ModComms,        D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.ModPayload,      D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.ModSafety,       D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.DesignSelect,    D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.DesignDeselect,  D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.LaunchConfirm,   D(Cat.Story, 2, 0.85f, 0.5f, "Logo_02") },
        // Radio / tutorial
        { Sfx.RadioOpen,       D(Cat.Story, 2, 0.70f, 0.3f, "Menu_05") },
        { Sfx.RadioNext,       D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.StepComplete,    D(Cat.Event, 2, 0.80f, 0.3f, "Objective_01") },
        { Sfx.Unlock,          D(Cat.Event, 2, 0.80f, 0.3f, "Objective_02") },
        // Comandos: al pulsar suena el clic normal (Button_26); al LLEGAR a la nave suena CmdArrive
        { Sfx.CmdSend,         D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdArrive,       D(Cat.Cmd, 2, 0.60f, 0.1f, "Menu_10") },
        { Sfx.CmdHeaterOn,     D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdHeaterOff,    D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdPayloadOn,    D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdPayloadOff,   D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdAntennaOn,    D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdAntennaOff,   D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdPointSun,     D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdPointEarth, D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdDesaturate,   D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.CmdSafeMode,     D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        // Eventos de la misión
        { Sfx.LinkOpen,        D(Cat.Event, 1, 0.65f, 0.5f, "Menu_13") },
        { Sfx.LinkClose,       D(Cat.Event, 1, 0.65f, 0.5f, "Menu_14") },
        { Sfx.EclipseStart,    D(Cat.Event, 2, 0.60f, 1.0f, "Noise_01") },
        { Sfx.EclipseEnd,      D(Cat.Event, 2, 0.60f, 1.0f, "Menu_15") },
        { Sfx.DataChime,       D(Cat.Event, 1, 0.50f, 2.0f, "Objective_03") },
        { Sfx.AlertWarning,    D(Cat.Alert, 3, 0.85f, 1.0f, "Error_01") },
        { Sfx.FlareImpact,     D(Cat.Alert, 3, 0.90f, 0.6f, "Noise_08") },
        { Sfx.WheelImpact,     D(Cat.Alert, 3, 0.85f, 0.6f, "Glitch_03") },
        { Sfx.DustImpact,      D(Cat.Alert, 3, 0.80f, 0.6f, "Noise_06") },
        { Sfx.ResponseGood,    D(Cat.Event, 2, 0.80f, 0.5f, "Objective_04") },
        { Sfx.ResponseBad,     D(Cat.Event, 2, 0.75f, 0.5f, "Error_05") },
        { Sfx.BatteryLow,      D(Cat.Alert, 2, 0.75f, 2.0f, "Error_02") },
        { Sfx.BatteryCritical, D(Cat.Alert, 3, 0.90f, 2.0f, "Error_06") },
        { Sfx.FreezeWarning,   D(Cat.Alert, 2, 0.75f, 3.0f, "Glitch_01") },
        { Sfx.OverheatWarning, D(Cat.Alert, 2, 0.75f, 3.0f, "Glitch_02") },
        { Sfx.Blackout,        D(Cat.Alert, 3, 0.95f, 1.0f, "Broken_03") },
        // Manual y turno
        { Sfx.ManualOpen,      D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.ManualClose,     D(Cat.Ui, 1, 0.65f, 0.04f, "Button_26") },
        { Sfx.NightStart,      D(Cat.Story, 3, 0.85f, 1.0f, "Logo_04") },
        { Sfx.HourChime,       D(Cat.Event, 1, 0.45f, 1.0f, "Menu_20") },
        { Sfx.NightComplete,   D(Cat.Story, 3, 0.95f, 1.0f, "Objective_05") },
        { Sfx.MissionLost,     D(Cat.Story, 3, 0.95f, 1.0f, "Broken_02") },
        { Sfx.ReviewOpen,      D(Cat.Story, 3, 0.85f, 1.0f, "Logo_01") },
        // Pantalla de carga
        { Sfx.BootLogo,        D(Cat.Story, 2, 0.80f, 1.0f, "Logo_05") },
        { Sfx.BootTick,        D(Cat.Ui, 0, 0.30f, 0.05f, "Menu_19") },
        { Sfx.BootReady,       D(Cat.Event, 2, 0.70f, 1.0f, "Menu_18") },
        { Sfx.BootStart,       D(Cat.Story, 3, 0.90f, 0.5f, "Logo_03") },
    };

    class LDef { public string clip; public float vol; public LDef(string c, float v) { clip = c; vol = v; } }

    static readonly Dictionary<Loop, LDef> LoopDefaults = new Dictionary<Loop, LDef>
    {
        { Loop.Ambience,    new LDef("Empty_Tape_01", 0.22f) },
        { Loop.Alarm,       new LDef("Error_09",      0.22f) },
        { Loop.Downlink,    new LDef("Noise_07",      0.10f) },
        { Loop.MusicDesign, new LDef("Empty_Tape_02", 0.30f) },
        { Loop.MusicReview, new LDef("Empty_Tape_03", 0.30f) },
        { Loop.MusicBoot,   new LDef("Empty_Tape_04", 0.30f) },
        { Loop.Tension,     new LDef("Empty_Tape_05", 0.16f) },
    };

    // Resuelve (en memoria) los clips de cada slot a partir del pack.
    void ApplyDefaults()
    {
        OnValidate();
        foreach (var x in sfx)
        {
            Def d;
            if (!SfxDefaults.TryGetValue(x.id, out d)) continue;
            x.cat = d.cat; x.prio = d.prio; x.baseVol = d.vol; x.cooldown = d.cd; x.pitch = d.pitch;
            if (x.clip != null) { x.pool = new AudioClip[] { x.clip }; continue; }
            var list = new List<AudioClip>();
            if (pack != null)
                foreach (var n in d.clips) { var c = pack.Get(n); if (c != null) list.Add(c); }
            x.pool = list.ToArray();
        }
        foreach (var x in loops)
        {
            LDef d;
            if (!LoopDefaults.TryGetValue(x.id, out d)) continue;
            x.baseVol = d.vol;
            x.use = x.clip != null ? x.clip : (pack != null ? pack.Get(d.clip) : null);
        }
    }

    static string DefName(Sfx id)
    {
        Def d;
        return SfxDefaults.TryGetValue(id, out d) ? "   [" + string.Join(", ", d.clips) + "]" : "";
    }
    static string DefName(Loop id)
    {
        LDef d;
        return LoopDefaults.TryGetValue(id, out d) ? "   [" + d.clip + "]" : "";
    }

    // ---------------------------------------------------------------- Inspector
    void Reset() { OnValidate(); }

    void OnValidate()
    {
        foreach (Sfx id in System.Enum.GetValues(typeof(Sfx)))
            if (sfx.Find(x => x.id == id) == null) sfx.Add(new SfxSlot { id = id });
        sfx.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
        foreach (var x in sfx) x.name = x.id + "  -  " + Describe(x.id) + DefName(x.id);

        foreach (Loop id in System.Enum.GetValues(typeof(Loop)))
            if (loops.Find(x => x.id == id) == null) loops.Add(new LoopSlot { id = id });
        loops.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
        foreach (var x in loops) x.name = x.id + "  -  " + Describe(x.id) + DefName(x.id);
    }

    static string Describe(Loop l)
    {
        switch (l)
        {
            case Loop.Ambience: return "zumbido de la sala de control (durante el turno)";
            case Loop.Alarm: return "alarma mientras hay una alerta por impactar";
            case Loop.Downlink: return "transmisión de datos a la Tierra";
            case Loop.MusicDesign: return "música de la pantalla de planificación";
            case Loop.MusicReview: return "música del informe final";
            case Loop.MusicBoot: return "música de la pantalla de carga";
            case Loop.Tension: return "capa de tensión mientras suena la alarma";
        }
        return "";
    }

    static string Describe(Sfx s)
    {
        switch (s)
        {
            case Sfx.UiHover: return "pasar el mouse sobre un botón";
            case Sfx.UiClick: return "clic en un botón (genérico)";
            case Sfx.UiDenied: return "acción bloqueada o no permitida";
            case Sfx.DesignSelect: return "(sin uso) elegir módulo genérico";
            case Sfx.DesignDeselect: return "quitar cualquier módulo";
            case Sfx.LaunchConfirm: return "pulsar LANZAR MISIÓN";
            case Sfx.RadioOpen: return "llega un mensaje de la Jefa de Misión";
            case Sfx.RadioNext: return "pulsar ENTENDIDO / CONTINUAR";
            case Sfx.StepComplete: return "objetivo del tutorial cumplido";
            case Sfx.Unlock: return "se desbloquea un control";
            case Sfx.CmdSend: return "(sin uso) clic genérico de comando";
            case Sfx.CmdArrive: return "cualquier comando llega a la nave";
            case Sfx.CmdHeaterOn: return "pulsar CALENTADOR ON";
            case Sfx.CmdHeaterOff: return "pulsar CALENTADOR OFF";
            case Sfx.CmdPayloadOn: return "pulsar PAYLOAD ON";
            case Sfx.CmdPayloadOff: return "pulsar PAYLOAD OFF";
            case Sfx.CmdAntennaOn: return "pulsar ANTENA ON";
            case Sfx.CmdAntennaOff: return "pulsar ANTENA OFF";
            case Sfx.CmdPointSun: return "pulsar APUNTADO SOL";
            case Sfx.CmdPointEarth: return "pulsar APUNTADO TIERRA";
            case Sfx.CmdDesaturate: return "pulsar DESATURAR RUEDAS";
            case Sfx.CmdSafeMode: return "pulsar MODO SEGURO";
            case Sfx.LinkOpen: return "se abre la ventana de enlace";
            case Sfx.LinkClose: return "se cierra la ventana de enlace";
            case Sfx.EclipseStart: return "empieza el eclipse";
            case Sfx.EclipseEnd: return "termina el eclipse";
            case Sfx.DataChime: return "cada 50 MB devueltos a la Tierra";
            case Sfx.AlertWarning: return "aparece una alerta";
            case Sfx.FlareImpact: return "impacta la llamarada solar";
            case Sfx.WheelImpact: return "ocurre la saturación de ruedas";
            case Sfx.DustImpact: return "empieza la tormenta de polvo";
            case Sfx.ResponseGood: return "respondiste bien a la alerta";
            case Sfx.ResponseBad: return "respondiste mal a la alerta";
            case Sfx.BatteryLow: return "batería bajo 25%";
            case Sfx.BatteryCritical: return "batería bajo 10%";
            case Sfx.FreezeWarning: return "la nave se está congelando";
            case Sfx.OverheatWarning: return "la nave se sobrecalienta";
            case Sfx.Blackout: return "apagón total";
            case Sfx.ManualOpen: return "abrir el manual";
            case Sfx.ManualClose: return "cerrar el manual";
            case Sfx.NightStart: return "empieza el turno";
            case Sfx.HourChime: return "cada hora del reloj (1 AM, 2 AM...)";
            case Sfx.NightComplete: return "turno superado (amanece)";
            case Sfx.MissionLost: return "misión perdida";
            case Sfx.ReviewOpen: return "se abre el informe final";
            case Sfx.BootLogo: return "aparece el logo Polarstern";
            case Sfx.BootTick: return "avance de la barra de carga (cada 10%)";
            case Sfx.BootReady: return "carga al 100%";
            case Sfx.BootStart: return "pulsar EMPEZAR";
            case Sfx.BtnMode: return "botón MODO: TUTORIAL / DIRECTO";
            case Sfx.BtnOverlay: return "botón SIGUIENTE NOCHE / VER INFORME";
            case Sfx.BtnRedesign: return "botón REDISEÑAR Y VOLVER A INTENTAR";
            case Sfx.ModLauncher: return "elegir un LANZADOR";
            case Sfx.ModPower: return "elegir un módulo de ENERGÍA";
            case Sfx.ModComms: return "elegir una ANTENA";
            case Sfx.ModPayload: return "elegir un instrumento (PAYLOAD)";
            case Sfx.ModSafety: return "elegir un módulo de SEGURIDAD";
        }
        return "";
    }
}
