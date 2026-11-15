using System;
using System.Collections.Generic;
using System.Text;

public enum EvType { Solar, Dust, Night, Fail, Meteor, Leak, Supply, Earth, Luck, Mishap, Bonus, News }

// Un evento aleatorio de la misión. day = día en que empieza; dur = cuántos días dura.
public class GameEvent
{
    public EvType type;
    public int day, dur = 1;
    public float sev = 1f;
    public int sys;      // solo fallas: 0 invernadero, 1 reciclador, 2 calefacción, 3 paneles
    public FlareDef flare;   // solo tormentas solares: la llamarada REAL de DONKI en la que se basa

    public bool ActiveOn(int d) { return d >= day && d < day + dur; }
}

// Un aviso que se muestra en el informe de la mañana.
public class EventNote
{
    public string text;
    public LogKind kind;
    public EvType type;
    public string icon;      // opcional: dibujo propio para el informe (si es null se usa el del tipo)
}

// Genera los eventos de la misión y calcula el pronóstico.
public static class EventDirector
{
    public static readonly string[] SystemNames = { "Invernadero", "Máquina de aire", "Calefacción", "Paneles solares" };

    static int R(Random rng, int lo, int hi)
    {
        if (hi < lo) hi = lo;
        return rng.Next(lo, hi + 1);
    }

    static GameEvent Add(List<GameEvent> list, EvType t, int day, int dur, float sev, int sys)
    {
        var e = new GameEvent { type = t, day = day, dur = dur, sev = sev, sys = sys };
        list.Add(e);
        return e;
    }

    static void Shuffle(List<FlareDef> l, Random rng)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            FlareDef tmp = l[i]; l[i] = l[j]; l[j] = tmp;
        }
    }

    // Las tormentas solares se basan en llamaradas REALES de DONKI (cambian de una partida a otra).
    public static List<GameEvent> Generate(DifficultyDef d, bool tutorial, Random rng)
    {
        var l = new List<GameEvent>();
        var site = Session.Site;
        int days = d.days;
        int minD = tutorial ? 3 : 2;      // en el tutorial no pasa nada malo los dos primeros días
        float sv = d.severity;

        var pool = NasaData.Pool();
        Shuffle(pool, rng);
        FlareDef tut = null;
        if (tutorial)
        {
            // La primera tormenta del tutorial es de fuerza media, para poder enseñar el blindaje sin matar a nadie.
            var mid = new List<FlareDef>();
            foreach (var f in pool) if (f.Sev >= 0.8f && f.Sev <= 1.1f) mid.Add(f);
            tut = mid.Count > 0 ? mid[rng.Next(0, mid.Count)] : pool[0];
            pool.Remove(tut);
        }
        int fi = 0;

        for (int i = 0; i < d.storms; i++)
        {
            FlareDef fl;
            if (i == 0 && tut != null) fl = tut;
            else { fl = pool[fi % pool.Count]; fi++; }
            var ev = Add(l, EvType.Solar, R(rng, Math.Max(3, minD), days - 3), 1, fl.Sev * sv, 0);
            ev.flare = fl;
        }
        // dos tormentas del Sol nunca caen la misma noche
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i].type != EvType.Solar) continue;
            for (int j = 0; j < i; j++)
                if (l[j].type == EvType.Solar && l[j].day == l[i].day)
                    l[i].day = l[i].day + 1 <= days - 1 ? l[i].day + 1 : l[i].day - 1;
        }
        for (int i = 0; i < (d.panels ? site.dust : 0); i++) Add(l, EvType.Dust, R(rng, minD, days - 4), Tuning.DustNights, sv, 0);      // la tormenta de polvo dura SOLO esta cantidad de noches (1)
        if (d.heater) Add(l, EvType.Night, R(rng, 5, days - 3), d.longDur + site.nightExtra, sv, 0);
        for (int i = 0; i < d.fails; i++) Add(l, EvType.Fail, R(rng, minD, days - 1), 1, sv, rng.Next(0, 4));
        for (int i = 0; i < (d.hazards ? site.meteors : 0); i++) Add(l, EvType.Meteor, R(rng, Math.Max(3, minD), days - 1), 1, sv, 0);
        for (int i = 0; i < d.leaks; i++) Add(l, EvType.Leak, R(rng, Math.Max(3, minD), days - 1), 1, sv, 0);
        for (int i = 0; i < d.supplies; i++) Add(l, EvType.Supply, R(rng, minD, days - 1), 1, 1f, 0);

        if (tutorial)
        {
            // La primera tormenta solar siempre llega el día 4: así el tutorial puede enseñarte el blindaje.
            bool pinned = false;
            foreach (var e in l)
            {
                if (e.type != EvType.Solar) continue;
                if (!pinned) { e.day = 4; pinned = true; }
                else if (e.day == 4) e.day = Math.Min(days - 2, 7);
            }
        }
        return l;
    }

    public static bool Has(List<GameEvent> ev, int day, EvType t)
    {
        foreach (var e in ev) if (e.type == t && e.ActiveOn(day)) return true;
        return false;
    }

    // Cuánto sol llega hoy (1 = normal) y si hace mucho frío.
    public static float SolarFactor(List<GameEvent> ev, int day, out bool cold)
    {
        float f = 1f;
        cold = false;
        foreach (var e in ev)
        {
            if (!e.ActiveOn(day)) continue;
            if (e.type == EvType.Dust) f = Math.Min(f, Tuning.DustSolar);
            if (e.type == EvType.Night) { f = Math.Min(f, Tuning.LongNightSolar); cold = true; }
        }
        return f;
    }

    static string DaysWord(int n) { return n == 1 ? "1 noche" : n + " noches"; }

    static string Strength(float flareSev)
    {
        if (flareSev < 0.8f) return "débil";
        if (flareSev < 1.1f) return "fuerte";
        return "MUY fuerte";
    }

    // Pronóstico de las próximas 'ahead' noches. Solo se pueden predecir tormentas, polvo, noches largas y suministros.
    // Si la Tierra te ayudó, el pronóstico llega más lejos.
    public static string Forecast(List<GameEvent> ev, int today, int ahead)
    {
        var sb = new StringBuilder();
        string night = Session.Site.nightName;
        for (int k = 1; k <= ahead; k++)
        {
            int d = today + k;
            string line = "";
            foreach (var e in ev)
            {
                if (e.day != d) continue;
                switch (e.type)
                {
                    case EvType.Solar:
                        line += "<color=#ff4040>TORMENTA DEL SOL";
                        if (e.flare != null) line += " (" + Strength(e.flare.Sev) + ")";
                        line += "</color> ";
                        if (Session.Diff.house) line += "<color=#ffb060>[daña la casa y sube la temperatura]</color> ";
                        break;
                    case EvType.Dust: line += "<color=#ff9a3c>POLVO (" + DaysWord(e.dur) + ")</color> "; if (Session.Diff.house) line += "<color=#ffb060>[ensucia paneles y raspa la casa]</color> "; break;
                    case EvType.Night: line += "<color=#7fa0ff>" + night + " (" + DaysWord(e.dur) + ")</color> "; if (Session.Diff.house && Session.Diff.heater) line += "<color=#9fb8ff>[sin calefacción: frío y daño a la casa]</color> "; break;
                    case EvType.Supply: if (k == 1) line += "<color=#73ff8c>¡llega una cápsula!</color> "; break;
                }
            }
            if (line.Length == 0) line = "<color=#7fbf8a>todo tranquilo</color>";
            sb.Append("NOCHE " + d + ": " + line + "\n");
        }
        if (Session.Diff.house) sb.Append("<color=#7fbf8a>Roturas y golpes: sin aviso.</color>");
        return sb.ToString();
    }

    // Texto corto del pronóstico para el informe de la mañana (sin colores de línea completa).
    public static bool StormTomorrow(List<GameEvent> ev, int today)
    {
        foreach (var e in ev) if (e.type == EvType.Solar && e.day == today + 1) return true;
        return false;
    }

    public static bool LongEventSoon(List<GameEvent> ev, int today, EvType t)
    {
        foreach (var e in ev) if (e.type == t && (e.day == today + 1 || e.day == today + 2)) return true;
        return false;
    }

    public static int Flags(List<GameEvent> ev, int day)
    {
        int f = 0;
        foreach (var e in ev)
        {
            if (e.type == EvType.Dust && e.ActiveOn(day)) f |= 2;
            if (e.type == EvType.Night && e.ActiveOn(day)) f |= 4;
            if (e.day != day) continue;
            if (e.type == EvType.Solar) f |= 1;
            if (e.type == EvType.Fail) f |= 8;
            if (e.type == EvType.Meteor || e.type == EvType.Leak) f |= 16;
            if (e.type == EvType.Supply) f |= 32;
        }
        return f;
    }
}