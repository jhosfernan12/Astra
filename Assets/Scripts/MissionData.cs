using System.Collections.Generic;
using UnityEngine;

public enum LogKind { Info, Good, Warn, Bad }

// Recursos del puesto (0-100). Shield y Panels son "instrumentales": solo importan por lo que producen.
public enum Res { Energy = 0, Oxygen = 1, Food = 2, Integrity = 3, Shield = 4, Health = 5, Morale = 6, Panels = 7, Science = 8 }

// ---------------------------------------------------------------- paquetes de suministros iniciales
public class PackDef
{
    public string name, desc;
    public float[] start;    // energía, oxígeno, comida, estructura, blindaje, salud, moral, paneles
}

public static class Packs
{
    public static readonly PackDef[] All =
    {
        new PackDef { name = "EQUILIBRADO",
            desc = "Todo parejo, sin sorpresas.\nEs el mejor para aprender.",
            start = new float[] { 70f, 80f, 70f, 80f, 30f, 100f, 80f, 100f } },
        new PackDef { name = "BLINDADO",
            desc = "Escudo fuerte, pero menos batería y comida.\nAguanta mejor las tormentas solares.",
            start = new float[] { 60f, 80f, 60f, 85f, 70f, 100f, 80f, 100f } },
        new PackDef { name = "ENERGÉTICO",
            desc = "Mucha batería, pero poca comida y casi sin escudo.\n¡Enciende el invernadero pronto!",
            start = new float[] { 90f, 80f, 50f, 80f, 20f, 100f, 80f, 100f } },
    };
}

// ---------------------------------------------------------------- libro de decisiones (para el informe final)
// Cada decisión (o evento) va sumando lo que APORTÓ (gain) y lo que COSTÓ (cost) en cada recurso.
public class LedgerEntry
{
    public string key, name;
    public bool isEvent;
    public int count, days;
    public float[] gain = new float[9];
    public float[] cost = new float[9];

    public float GainPts() { float s = 0f; for (int i = 0; i < 9; i++) s += gain[i] * Ledger.Weight[i]; return s; }
    public float CostPts() { float s = 0f; for (int i = 0; i < 9; i++) s += cost[i] * Ledger.Weight[i]; return s; }
    public float Net() { return GainPts() - CostPts(); }
    public float Influence() { return GainPts() + CostPts(); }
}

public class Ledger
{
    // Qué tan importante es cada recurso al comparar decisiones (energía y moral valen menos que aire, comida y salud).
    public static readonly float[] Weight = { 0.6f, 1.5f, 1.0f, 1.0f, 0f, 1.5f, 0.6f, 0f, 8f };
    public static readonly string[] ResNames =
        { "batería", "aire", "comida", "casa", "escudo", "salud", "ánimo", "paneles", "ciencia" };

    readonly Dictionary<string, LedgerEntry> map = new Dictionary<string, LedgerEntry>();

    public static string NameOf(string key)
    {
        switch (key)
        {
            case "shield": return "REFORZAR EL ESCUDO";
            case "gh": return "INVERNADERO";
            case "rc": return "MÁQUINA DE AIRE";
            case "ht": return "CALEFACCIÓN";
            case "clean": return "LIMPIAR LOS PANELES";
            case "repair": return "REPARAR";
            case "exp": return "EXPERIMENTOS";
            case "rest": return "DESCANSAR";
            case "call": return "LLAMAR A LA TIERRA";
            case "aim": return "APUNTAR LA ANTENA";
            case "upload": return "SUBIR EL BUFFER";
            case "dirt": return "PANELES SUCIOS";
            case "trouble": return "COSAS QUE FALLAN";
            case "housebroken": return "CASA ROTA";
            case "temp": return "FRÍO Y CALOR";
            case "ev_crack": return "Grieta en la casa";
            case "ev_quake": return "Sismo";
            case "ev_hit": return "Ráfaga de arena y rocas";
            case "broken": return "COSAS ROTAS SIN REPARAR";
            case "ev_solar": return "Tormenta solar";
            case "ev_dust": return "Tormenta de polvo";
            case "ev_night": return "Oscuridad larga";
            case "ev_meteor": return "Roca del espacio";
            case "ev_leak": return "Fuga de aire";
            case "ev_supply": return "Cápsula de suministros";
            case "ev_luck": return "Golpe de suerte";
            case "ev_mishap": return "Imprevisto";
            case "ev_rescue": return "Cápsula de emergencia";
            case "bonus": return "BONO DE LA NOCHE";
            case "news": return "Noticias de la Tierra";
        }
        return key;
    }

    LedgerEntry Get(string key)
    {
        LedgerEntry e;
        if (!map.TryGetValue(key, out e))
        {
            e = new LedgerEntry { key = key, name = NameOf(key), isEvent = key.StartsWith("ev_") };
            map[key] = e;
        }
        return e;
    }

    public void Gain(string key, Res r, float amount) { if (!Session.ResShown((int)r)) return; if (amount > 0.01f) Get(key).gain[(int)r] += amount; }
    public void Cost(string key, Res r, float amount) { if (!Session.ResShown((int)r)) return; if (amount > 0.01f) Get(key).cost[(int)r] += amount; }
    public void Count(string key) { Get(key).count++; }
    public void Day(string key) { Get(key).days++; }

    public LedgerEntry Find(string key) { LedgerEntry e; return map.TryGetValue(key, out e) ? e : null; }

    // Decisiones (events = false) o eventos (events = true), de la que más influyó a la que menos.
    public List<LedgerEntry> Ranked(bool events)
    {
        var l = new List<LedgerEntry>();
        foreach (var e in map.Values)
            if (e.isEvent == events && e.Influence() > 0.5f) l.Add(e);
        l.Sort((a, b) => b.Influence().CompareTo(a.Influence()));
        return l;
    }

    public void Clear() { map.Clear(); }
}

// ---------------------------------------------------------------- registro y gráfica
public class LogEntry { public int day; public string text; public LogKind kind; }

// Una muestra por día para la gráfica del informe. ev: 1 tormenta solar, 2 polvo, 4 noche larga, 8 falla, 16 impacto/fuga, 32 suministros.
public class DaySample { public int day; public float[] v = new float[8]; public byte ev; }

// Una tormenta solar que golpeó el puesto, con la llamarada REAL en la que se basó.
public class FlareHit
{
    public int day;
    public FlareDef flare;
    public float dose, avoided;
}

// Estado de toda la misión (se conserva hasta el informe final)
public static class Campaign
{
    public static List<FlareHit> hits = new List<FlareHit>();
    public static string siteName = "";
    public static List<LogEntry> log = new List<LogEntry>();
    public static List<DaySample> history = new List<DaySample>();
    public static List<string> lessons = new List<string>();
    public static Ledger ledger = new Ledger();

    public static bool finished, survived;
    public static string endCause = "";        // power / oxygen / health / structure / science
    public static string deathReason = "";     // si murieron (health): air / food / house / mood / radiation
    public static int totalDays, daysSurvived, experiments, tasksUsed, tasksWasted;
    public static bool goalMet = true;         // ¿se cumplió la META DE CIENCIA?
    public static int goalPacks;
    public static int rescues, bonuses;       // cápsulas de emergencia usadas y bonos de noche ganados
    public static float bufferLost;            // MB que borraron las tormentas solares
    public static float[] finalV = new float[8];

    public static void Reset()
    {
        log.Clear(); history.Clear(); lessons.Clear(); ledger.Clear(); hits.Clear();
        finished = false; survived = false; endCause = ""; deathReason = "";
        totalDays = 0; daysSurvived = 0; experiments = 0; tasksUsed = 0; tasksWasted = 0; bufferLost = 0f;
        rescues = 0; bonuses = 0; goalMet = true; goalPacks = 0;
        finalV = new float[8];
    }

    public static void Add(int day, string text, LogKind kind)
    {
        log.Add(new LogEntry { day = day, text = text, kind = kind });
    }

    public static void Lesson(string text)
    {
        if (!lessons.Contains(text)) lessons.Add(text);
    }

    // Promedio del estado final de lo que mantiene vivos a los astronautas.
    public static float StateAverage()
    {
        return (finalV[0] + finalV[1] + finalV[2] + finalV[3] + finalV[5] + finalV[6]) / 6f;
    }

    public static float Score()
    {
        float s = daysSurvived * Tuning.PtsPerDay + experiments * Tuning.PtsPerExperiment;
        if (survived) s += Tuning.PtsCompletion + StateAverage() * Tuning.PtsState;
        return s;
    }

    public static string Grade(float score)
    {
        float ratio = score / Mathf.Max(1f, totalDays * Tuning.PtsRefPerDay);
        if (survived && !goalMet) ratio = Mathf.Min(ratio, Tuning.GradeB - 0.01f);     // sin cumplir la meta de ciencia, la nota no pasa de C
        if (ratio >= Tuning.GradeS) return "S";
        if (ratio >= Tuning.GradeA) return "A";
        if (ratio >= Tuning.GradeB) return "B";
        if (ratio >= Tuning.GradeC) return "C";
        return "D";
    }
}
