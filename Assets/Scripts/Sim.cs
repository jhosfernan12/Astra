using System;
using System.Collections.Generic;
using UnityEngine;

public enum TaskId { Reinforce = 0, Repair = 1, Clean = 2, Experiment = 3, Rest = 4, Call = 5, Aim = 6, Upload = 7 }

// El puesto ARES-1. Modelo por turnos: cada NOCHE el jugador gasta sus tareas y luego termina la noche.
public class Outpost
{
    public const int E = 0, O = 1, F = 2, I = 3, B = 4, H = 5, M = 6, P = 7;     // posiciones en v[]
    public const int SysGh = 0, SysRc = 1, SysHt = 2, SysPn = 3;

    public float[] v = new float[8];          // energía, oxígeno, comida, estructura, blindaje, salud, moral, paneles
    public bool gh, rc, ht;                   // invernadero, reciclador de O2, calefacción (encendidos)
    public bool[] broken = new bool[4];       // averías
    public bool[] changedToday = new bool[3];
    public int[] actsToday = new int[8];
    public int[] actsTotal = new int[8];
    public int day = 1, ap, experiments, tasksUsed, tasksWasted;
    public float panelsNoClean = 100f;        // cómo estarían los paneles si NUNCA los limpiaras (para medir el valor de limpiar)
    public Ledger ledger;                     // null = simulación (no registra nada)

    // ---- Antena y buffer (como en QUIPU): los experimentos llenan el buffer y hay que subirlo a la Tierra
    public float buffer, uploaded;            // MB guardados a bordo / MB ya enviados
    public bool aimEarth;                     // false = la antena apunta a CIENCIA, true = apunta a la TIERRA
    public static int MaxDataPacks { get { return Mathf.RoundToInt(Tuning.BufferMax / Tuning.ExperimentData); } }   // paquetes que caben
    public int DataPacks { get { return Mathf.RoundToInt(buffer / Tuning.ExperimentData); } }                        // paquetes guardados

    // ---- Llamadas a la Tierra: la respuesta tarda noches en llegar (retraso de comunicación)
    public const int ReqForecast = 0, ReqRepair = 1, ReqFamily = 2;
    public int reqKind = -1, reqSent, reqDay;  // reqKind -1 = no hay llamada pendiente
    public int forecastUntil;                  // hasta qué noche el pronóstico llega lejos
    public bool repairGuide;                   // la próxima reparación sale gratis y mejor

    // ---- ÁNIMO, premios y cápsulas de emergencia
    public static System.Random Rng = new System.Random();
    public int lifelines;                      // cápsulas de emergencia que te quedan
    public int boostNights;                    // noches que dura el extra de una BUENA NOTICIA
    public int bonusAp;                        // tareas extra de la próxima noche (NOCHE PERFECTA)
    public int bonusRes = -1;                  // recurso que subió el BONO DE LA NOCHE (-1 = ninguno)
    public float bonusAmt;                     // cuánto subió
    public bool bonusPerfect;                  // la noche fue perfecta
    public readonly List<int> rescued = new List<int>();   // recursos salvados por una cápsula de emergencia (de la noche que pasó)

    // ---- Flujos EN VIVO del día (se aplican poco a poco mientras juegas; lo que falte se aplica al terminar la noche)
    public float liveGen, liveCons, liveDecay, liveMood;   // lo que ya se aplicó hoy
    public float liveFoodGen, liveFoodBase, liveO2Gen, liveO2Base, liveCold;   // comida/aire/frío ya aplicados hoy
    public float rateE, rateP, rateM, rateO, rateF, rateH; // velocidad actual por segundo (para mostrarla en pantalla)
    public float bodyTemp;                                 // temperatura del equipo: negativo = FRÍO (copo de nieve), positivo = CALOR (llama)
    public string liveDead;                                // si la salud llegó a 0 mientras jugabas: por qué ("air", "mood", ...)

    Outpost() { }

    public Outpost(int pack, Ledger l)
    {
        var p = Packs.All[Mathf.Clamp(pack, 0, Packs.All.Length - 1)];
        for (int i = 0; i < 8; i++) v[i] = p.start[i];
        ledger = l;
        lifelines = Session.Diff.lifelines;
        Clamp();                      // lo que no existe en este nivel empieza al máximo
        ap = ApMax();
    }

    public Outpost Clone()
    {
        var o = new Outpost();
        Array.Copy(v, o.v, 8);
        o.gh = gh; o.rc = rc; o.ht = ht;
        Array.Copy(broken, o.broken, 4);
        o.day = day; o.ap = ap; o.panelsNoClean = panelsNoClean;
        o.buffer = buffer; o.uploaded = uploaded; o.aimEarth = aimEarth;
        o.liveGen = liveGen; o.liveCons = liveCons; o.liveDecay = liveDecay; o.liveMood = liveMood;
        o.bodyTemp = bodyTemp;
        o.liveFoodGen = liveFoodGen; o.liveFoodBase = liveFoodBase; o.liveO2Gen = liveO2Gen; o.liveO2Base = liveO2Base; o.liveCold = liveCold;
        o.ledger = null;
        return o;
    }

    public float[] Snapshot() { return (float[])v.Clone(); }

    // 3 = ANIMADO, 2 = BIEN, 1 = CANSADO, 0 = DESANIMADO
    public int MoodTier()
    {
        if (!Session.Diff.morale) return 2;
        if (v[M] >= Tuning.MoodHigh) return 3;
        if (v[M] >= Tuning.MoodOk) return 2;
        if (v[M] >= Tuning.MoodLow) return 1;
        return 0;
    }

    public string MoodName()
    {
        switch (MoodTier()) { case 3: return "ANIMADO"; case 2: return "BIEN"; case 1: return "CANSADO"; }
        return "DESANIMADO";
    }

    // Cuánto rinde cada tarea: con ánimo alto rinde más, con ánimo bajo rinde menos.
    public float MoodMult()
    {
        float k;
        switch (MoodTier())
        {
            case 3: k = Tuning.MoodHighMult; break;
            case 2: k = Tuning.MoodOkMult; break;
            case 1: k = Tuning.MoodTiredMult; break;
            default: k = Tuning.MoodLowMult; break;
        }
        if (boostNights > 0) k *= Tuning.MoodBoostMult;
        return k;
    }

    public int ApMax()
    {
        int n = Tuning.TasksBase + Session.Diff.bonusTasks;
        switch (MoodTier()) { case 3: n += 1; break; case 1: n -= 1; break; case 0: n -= 2; break; }
        return Mathf.Max(1, n);
    }

    // Batería que gasta una tarea (la dificultad la multiplica: en los niveles altos la batería baja MUCHO).
    public float Cost(TaskId t)
    {
        float b = 0f;
        switch (t)
        {
            case TaskId.Reinforce: b = Tuning.EnReinforce; break;
            case TaskId.Repair: b = repairGuide ? 0f : Tuning.EnRepair; break;
            case TaskId.Clean: b = Tuning.EnClean; break;
            case TaskId.Experiment: b = Tuning.EnExperiment; break;
            case TaskId.Upload: b = Tuning.EnUpload; break;
            case TaskId.Aim: b = Tuning.EnAim; break;
            case TaskId.Call: b = Tuning.EnCall; break;
        }
        float c = b * Session.Diff.energyMult;
        if (t == TaskId.Experiment) c /= Mathf.Clamp(MoodMult(), 0.5f, 1.5f);
        return c;
    }

    bool NoPower(TaskId t, out string msg)
    {
        float c = Cost(t);
        msg = "";
        if (c > 0.01f && v[E] < c + Tuning.EnMargin)
        {
            msg = "Falta batería: esta tarea gasta " + c.ToString("0") + " y quedan " + v[E].ToString("0") + ".";
            return true;
        }
        return false;
    }

    // Paquetes de datos que se mandan con una subida (el equipo animado manda más).
    public int UploadPacks()
    {
        if (Kids) return MaxDataPacks;
        return Mathf.Max(1, Mathf.RoundToInt(Tuning.UploadRate / Tuning.ExperimentData * MoodMult() + 0.001f));
    }

    static bool Kids { get { return Session.Diff.kids; } }

    public bool GhOk { get { return gh && !broken[SysGh]; } }
    public bool RcOk { get { return rc && !broken[SysRc]; } }
    public bool HtOk { get { return ht && !broken[SysHt]; } }

    public bool SysOn(int s) { return s == SysGh ? gh : (s == SysRc ? rc : ht); }

    void Clamp()
    {
        for (int i = 0; i < 8; i++) v[i] = Mathf.Clamp(v[i], 0f, 100f);
        // Lo que no existe en este nivel se queda siempre al máximo y no cuenta.
        var d = Session.Diff;
        if (!d.morale) v[M] = 100f;
        if (!d.house) v[I] = 100f;
        if (!d.panels) v[P] = 100f;
    }

    // ---- helpers de libro (si ledger es null no hacen nada)
    void G(string k, Res r, float a) { if (ledger != null) ledger.Gain(k, r, a); }
    void C(string k, Res r, float a) { if (ledger != null) ledger.Cost(k, r, a); }

    public int FirstBroken()
    {
        // Prioridad: lo vital primero (aire, comida, calefacción, paneles)
        if (broken[SysRc]) return SysRc;
        if (broken[SysGh]) return SysGh;
        if (broken[SysHt]) return SysHt;
        if (broken[SysPn]) return SysPn;
        return -1;
    }

    // =================================================================== inicio de noche
    // Aplica los eventos que ocurren HOY, repone las tareas y devuelve los avisos para el informe de la mañana.
    public List<EventNote> BeginDay(List<GameEvent> events)
    {
        var notes = new List<EventNote>();
        liveGen = liveCons = liveDecay = liveMood = 0f; rateE = rateP = rateM = rateO = rateF = rateH = 0f;
        liveFoodGen = liveFoodBase = liveO2Gen = liveO2Base = liveCold = 0f; liveDead = null;
        for (int i = 0; i < 8; i++) actsToday[i] = 0;
        for (int i = 0; i < 3; i++) changedToday[i] = false;

        // Cápsulas de emergencia y bono de la noche que acaba de pasar
        foreach (int ri in rescued)
        {
            Campaign.rescues++;
            notes.Add(new EventNote { type = EvType.Supply, kind = LogKind.Good, icon = "bolt",
                text = "¡CÁPSULA DE EMERGENCIA! La Tierra mandó " + Ledger.ResNames[ri] + " justo a tiempo (+" + Tuning.RescueAmount.ToString("0") + "). Quedan " + lifelines + "." });
        }
        if (bonusRes >= 0)
        {
            Campaign.bonuses++;
            notes.Add(new EventNote { type = EvType.Bonus, kind = LogKind.Good, icon = "gift",
                text = "¡BONO DE LA NOCHE! Jugaste bien: " + Ledger.ResNames[bonusRes] + " +" + bonusAmt.ToString("0") + "." });
        }
        else if (bonusPerfect)
        {
            Campaign.bonuses++;
            notes.Add(new EventNote { type = EvType.Bonus, kind = LogKind.Good, icon = "gift",
                text = "¡NOCHE PERFECTA! Todo va muy bien: hoy tienes 1 tarea extra." });
        }

        // ¿Llegó hoy la respuesta de la Tierra? Puede traer una buena o una mala noticia (25 % cada una).
        if (reqKind >= 0 && day >= reqDay)
        {
            float roll = (float)Rng.NextDouble();
            int news = roll < Tuning.NewsGoodChance ? 1 : (roll < Tuning.NewsGoodChance + Tuning.NewsBadChance ? -1 : 0);
            bool fam = reqKind == ReqFamily;
            switch (reqKind)
            {
                case ReqForecast:
                    forecastUntil = day + 2;
                    notes.Add(new EventNote { type = EvType.Earth, kind = LogKind.Good,
                        text = "LA TIERRA RESPONDE: por 3 noches verás el pronóstico " + Tuning.ForecastLong + " noches adelante." });
                    break;
                case ReqRepair:
                    repairGuide = true;
                    notes.Add(new EventNote { type = EvType.Earth, kind = LogKind.Good,
                        text = "LA TIERRA RESPONDE: tu próxima REPARACIÓN es gratis y arregla más." });
                    break;
            }
            if (fam)
            {
                if (news == 1)
                {
                    float gm = Tuning.FamilyMorale * 2f;
                    float mm = Mathf.Min(gm, 100f - v[M]);
                    v[M] += gm; G("news", Res.Morale, mm);
                    boostNights = Tuning.MoodBoostNights;
                    notes.Add(new EventNote { type = EvType.News, kind = LogKind.Good, icon = "heart",
                        text = "¡BUENA NOTICIA! " + GoodNews[Rng.Next(0, GoodNews.Length)] + " Ánimo +" + mm.ToString("0") + " y rindes más " + Tuning.MoodBoostNights + " noches." });
                }
                else if (news == -1)
                {
                    v[M] -= Tuning.NewsBadMood; C("news", Res.Morale, Tuning.NewsBadMood);
                    notes.Add(new EventNote { type = EvType.News, kind = LogKind.Bad, icon = "cross",
                        text = "MALA NOTICIA. " + BadNews[Rng.Next(0, BadNews.Length)] + " Ánimo -" + Tuning.NewsBadMood.ToString("0") + "." });
                }
                else
                {
                    float m = Mathf.Min(Tuning.FamilyMorale, 100f - v[M]);
                    v[M] += Tuning.FamilyMorale;
                    G("call", Res.Morale, m);
                    notes.Add(new EventNote { type = EvType.Earth, kind = LogKind.Good, icon = "heart",
                        text = "LA TIERRA RESPONDE: llegan mensajes de sus familias. ¡El ánimo sube +" + m.ToString("0") + "!" });
                }
            }
            else if (news == 1)
            {
                float mm = Mathf.Min(Tuning.NewsGoodMood, 100f - v[M]);
                v[M] += Tuning.NewsGoodMood; G("news", Res.Morale, mm);
                boostNights = Tuning.MoodBoostNights;
                notes.Add(new EventNote { type = EvType.News, kind = LogKind.Good, icon = "heart",
                    text = "¡BUENA NOTICIA! " + GoodNews[Rng.Next(0, GoodNews.Length)] + " Ánimo +" + mm.ToString("0") + " y rindes más " + Tuning.MoodBoostNights + " noches." });
            }
            else if (news == -1)
            {
                v[M] -= Tuning.NewsBadMood; C("news", Res.Morale, Tuning.NewsBadMood);
                notes.Add(new EventNote { type = EvType.News, kind = LogKind.Bad, icon = "cross",
                    text = "MALA NOTICIA. " + BadNews[Rng.Next(0, BadNews.Length)] + " Ánimo -" + Tuning.NewsBadMood.ToString("0") + "." });
            }
            lastNews = news;
            reqKind = -1;
        }

        foreach (var e in events)
        {
            float sv = e.sev;
            if (e.day == day)
            {
                switch (e.type)
                {
                    case EvType.Solar:
                    {
                        float full = Tuning.StormDose * sv;
                        float k = Tuning.StormShieldEffect * Mathf.Max(0f, v[B]) / 100f;
                        float dose = full * (1f - k), avoided = full * k;
                        v[H] -= dose; v[M] -= 10f; v[E] -= 8f; v[B] -= 10f;
                        float houseHit = 0f;
                        if (Session.Diff.house) { houseHit = Tuning.StormHouseDamage * sv * HouseDamageMult(); v[I] -= houseHit; C("ev_solar", Res.Integrity, houseHit); }
                        float lostMB = 0f;
                        if (buffer > 0.5f && Session.Diff.dataLoss)
                        {
                            lostMB = Mathf.Ceil(buffer / Tuning.ExperimentData * Tuning.StormBufferLoss) * Tuning.ExperimentData;   // la mitad (hacia arriba)
                            buffer -= lostMB;
                            Campaign.bufferLost += lostMB;
                            C("ev_solar", Res.Science, lostMB / Tuning.ExperimentData);
                        }
                        G("shield", Res.Health, avoided);
                        C("ev_solar", Res.Health, dose); C("ev_solar", Res.Morale, 10f); C("ev_solar", Res.Energy, 8f);
                        string real = e.flare != null ? " ¡Fue REAL! Llamarada " + e.flare.Short + " (NASA)." : "";
                        Campaign.hits.Add(new FlareHit { day = day, flare = e.flare, dose = dose, avoided = avoided });
                        notes.Add(new EventNote
                        {
                            type = e.type, kind = dose > 25f ? LogKind.Bad : LogKind.Warn,
                            text = Kids ? "¡TORMENTA DEL SOL! " + (avoided >= 1f ? "Tu escudo protegió al equipo." : "No había escudo.") + real
                                 : "TORMENTA DEL SOL: el equipo recibió " + dose.ToString("0") + " de radiación" +
                                   (avoided >= 1f ? " (el escudo evitó " + avoided.ToString("0") + ")." : ". No había escudo.") +
                                   (houseHit >= 1f ? " La casa recibió " + houseHit.ToString("0") + " de daño." : "") +
                                   (lostMB > 0.5f ? " ¡Se borraron " + Mathf.RoundToInt(lostMB / Tuning.ExperimentData) + " paquetes de datos!" : "") + real
                        });
                        break;
                    }
                    case EvType.Fail:
                    {
                        if (broken[e.sys])
                            notes.Add(new EventNote { type = e.type, kind = LogKind.Warn, text = "Otra falla en " + EventDirector.SystemNames[e.sys] + ", que ya estaba roto." });
                        else
                        {
                            broken[e.sys] = true;
                            notes.Add(new EventNote { type = e.type, kind = LogKind.Bad, text = "SE ROMPIÓ ALGO: " + EventDirector.SystemNames[e.sys].ToUpper() + ". Usa REPARAR." });
                        }
                        break;
                    }
                    case EvType.Meteor:
                    {
                        float d = Tuning.MeteorDamage * sv * HouseDamageMult();
                        v[I] -= d;
                        C("ev_meteor", Res.Integrity, d);
                        notes.Add(new EventNote { type = e.type, kind = LogKind.Bad, text = Kids ? "¡Una roca del espacio golpeó la casa!" : "UNA ROCA DEL ESPACIO GOLPEÓ LA CASA: pierde " + d.ToString("0") + " puntos." });
                        break;
                    }
                    case EvType.Leak:
                    {
                        float o = Tuning.LeakOxygen * sv;
                        float li = Tuning.LeakIntegrity * HouseDamageMult();
                        v[O] -= o; v[I] -= li;
                        C("ev_leak", Res.Oxygen, o); C("ev_leak", Res.Integrity, li);
                        notes.Add(new EventNote { type = e.type, kind = LogKind.Bad, text = "FUGA DE AIRE: se escapan " + o.ToString("0") + " puntos de aire." });
                        break;
                    }
                    case EvType.Supply:
                    {
                        float f = Mathf.Min(25f, 100f - v[F]), en = Mathf.Min(10f, 100f - v[E]);
                        v[F] += 25f; v[E] += 10f;
                        G("ev_supply", Res.Food, f); G("ev_supply", Res.Energy, en);
                        notes.Add(new EventNote { type = e.type, kind = LogKind.Good, text = Kids ? "¡Llegó una cápsula con comida y baterías!" : "¡LLEGÓ UNA CÁPSULA! Trae comida y baterías de repuesto." });
                        break;
                    }
                    case EvType.Dust:
                        notes.Add(new EventNote { type = e.type, kind = LogKind.Warn,
                            text = Kids ? "¡Tormenta de polvo! Llega menos luz a los paneles."
                                   : "TORMENTA DE POLVO (" + e.dur + (e.dur == 1 ? " noche" : " noches") + "): llega poca luz a los paneles. No se pueden mandar datos." });
                        break;
                    case EvType.Night:
                        notes.Add(new EventNote { type = e.type, kind = LogKind.Warn,
                            text = Kids ? Session.Site.nightName + ": ¡hace mucho frío! Enciende la calefacción."
                                   : Session.Site.nightName + " (" + e.dur + (e.dur == 1 ? " noche" : " noches") + "): " + Session.Site.nightInfo + ". ¡Calefacción!" });
                        break;
                }
            }
            else if ((e.type == EvType.Dust || e.type == EvType.Night) && e.ActiveOn(day))
            {
                notes.Add(new EventNote { type = e.type, kind = LogKind.Warn,
                    text = (e.type == EvType.Dust ? "La tormenta de polvo sigue" : (Session.Site.nightArt == "el" ? "El " : "La ") + Session.Site.nightName.ToLower() + " sigue") +
                           " (noche " + (day - e.day + 1) + " de " + e.dur + ")." });
            }
        }
        Surprise(notes);
        if (Rng.NextDouble() < Session.Diff.extraSurprise) Surprise(notes);        // a veces cae un SEGUNDO imprevisto
        Clamp();
        ap = ApMax() + bonusAp;
        bonusAp = 0;
        return notes;
    }

    public int lastNews;      // la última respuesta de la Tierra: 1 buena, -1 mala, 0 normal

    static readonly string[] GoodNews =
    {
        "Un equipo de niños de una escuela les mandó dibujos.",
        "¡Nació un bebé en la familia de un astronauta!",
        "El equipo de fútbol de casa ganó el campeonato.",
        "Miles de personas vieron su lanzamiento y los aplauden.",
    };
    static readonly string[] BadNews =
    {
        "Hubo una tormenta fuerte en casa y los extrañan más.",
        "Se perdió un mensaje de sus amigos y están tristes.",
        "La familia de un astronauta tiene un problema pequeño.",
        "Hoy es un cumpleaños y no pueden estar en casa.",
    };

    public const int BadPool = 18;     // cuántos imprevistos malos distintos hay

    // SOLO PARA PRUEBAS: fuerza el imprevisto malo número 'pick' (0 a BadPool-1). Devuelve la noticia o null si ese no aplica en este nivel.
    public EventNote DebugBadEvent(int pick)
    {
        var notes = new List<EventNote>();
        BadSurprise(Mathf.Clamp(pick, 0, BadPool - 1), notes);
        Clamp();
        return notes.Count > 0 ? notes[0] : null;
    }

    // Un imprevisto EN VIVO (cae mientras juegas, sin avisar). Devuelve la noticia o null.
    public EventNote LiveEvent(bool force = false)
    {
        var d = Session.Diff;
        if (!force && (day < 2 || (Session.tutorial && day <= 4) || d.liveEvents <= 0f)) return null;
        if (!force && Rng.NextDouble() >= d.liveEvents) return null;
        bool good = Rng.NextDouble() < d.luck;
        var notes = new List<EventNote>();
        for (int tries = 0; tries < 10 && notes.Count == 0; tries++)
        {
            int pick = Rng.Next(0, good ? 6 : BadPool);
            if (good) GoodSurprise(pick, notes); else BadSurprise(pick, notes);
        }
        if (notes.Count == 0) return null;
        Clamp();
        return notes[0];
    }

    // Un imprevisto al azar (bueno o malo). NO sale en el pronóstico: ¡por eso es sorpresa!
    void Surprise(List<EventNote> notes)
    {
        var d = Session.Diff;
        if (day < 2 || (Session.tutorial && day <= 4) || d.surprise <= 0f) return;
        if (Rng.NextDouble() >= d.surprise) return;
        bool good = Rng.NextDouble() < d.luck;
        for (int tries = 0; tries < 8; tries++)
        {
            int pick = Rng.Next(0, good ? 6 : BadPool);
            if (good ? GoodSurprise(pick, notes) : BadSurprise(pick, notes)) return;
        }
    }

    bool GoodSurprise(int pick, List<EventNote> notes)
    {
        string k = "ev_luck";
        switch (pick)
        {
            case 0:
            {
                float en = Mathf.Min(20f, 100f - v[E]);
                v[E] += 20f; G(k, Res.Energy, en);
                notes.Add(new EventNote { type = EvType.Luck, kind = LogKind.Good, icon = "bolt",
                    text = Kids ? "¡Hoy el Sol brilló más fuerte! Batería +20." : "SOL EXTRA: la luz llegó más fuerte de lo normal. Batería +20." });
                return true;
            }
            case 1:
            {
                if (buffer > Tuning.BufferMax - Tuning.ExperimentData + 0.01f) return false;
                buffer += Tuning.ExperimentData;
                G(k, Res.Science, 1f);
                notes.Add(new EventNote { type = EvType.Luck, kind = LogKind.Good, icon = "flask",
                    text = Kids ? "¡Un sensor encontró algo! +1 paquete de datos." : "DESCUBRIMIENTO: un sensor automático encontró algo interesante. +1 paquete de datos." });
                return true;
            }
            case 2:
            {
                float o2 = Mathf.Min(15f, 100f - v[O]);
                v[O] += 15f; G(k, Res.Oxygen, o2);
                string where = Session.site == 0 ? "bajo el suelo (como detectó Phoenix en Marte)" : "en un cráter en sombra (como midió LCROSS en la Luna)";
                notes.Add(new EventNote { type = EvType.Luck, kind = LogKind.Good, icon = "air",
                    text = Kids ? "¡Encontraron hielo de agua! Aire +15." : "HIELO DE AGUA: el equipo lo halló " + where + ". Aire +15." });
                return true;
            }
            case 3:
            {
                if (!Session.Diff.panels || Session.site != 0) return false;
                float pn = Mathf.Min(25f, 100f - v[P]);
                v[P] += 25f; panelsNoClean += 25f; G(k, Res.Panels, pn);
                notes.Add(new EventNote { type = EvType.Luck, kind = LogKind.Good, icon = "dust",
                    text = "VIENTO LIMPIO: un remolino de viento sopló el polvo de los paneles, como pasó con los rovers de Marte. Paneles +25." });
                return true;
            }
            case 4:
            {
                float mm = Mathf.Min(20f, 100f - v[M]);
                v[M] += 20f; v[F] -= 5f;
                G(k, Res.Morale, mm); C(k, Res.Food, 5f);
                notes.Add(new EventNote { type = EvType.Luck, kind = LogKind.Good, icon = "face_happy",
                    text = Kids ? "¡Es un cumpleaños! Comen pastel: ánimo +20, comida -5." : "CUMPLEAÑOS: el equipo celebra con comida extra. Ánimo +20, comida -5." });
                return true;
            }
            default:
            {
                float mm = Mathf.Min(15f, 100f - v[M]);
                v[M] += 15f; boostNights = Tuning.MoodBoostNights; G("news", Res.Morale, mm);
                notes.Add(new EventNote { type = EvType.News, kind = LogKind.Good, icon = "heart",
                    text = "¡BUENA NOTICIA! " + GoodNews[Rng.Next(0, GoodNews.Length)] + " Ánimo +15 y rindes más " + Tuning.MoodBoostNights + " noches." });
                return true;
            }
        }
    }

    bool BadSurprise(int pick, List<EventNote> notes)
    {
        string k = "ev_mishap";
        float sf = Kids ? 0.7f : 1f;
        float sv = sf * Session.Diff.severity;
        switch (pick)
        {
            case 0:
            {
                float d = 15f * sf; v[E] -= d; C(k, Res.Energy, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "bolt",
                    text = Kids ? "¡Chispas! Un cable falló y se perdió batería: -" + d.ToString("0") + "." : "CORTOCIRCUITO: un cable falló y se perdió batería: -" + d.ToString("0") + "." });
                return true;
            }
            case 1:
            {
                float d = 12f * sf; v[F] -= d; C(k, Res.Food, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "food",
                    text = Kids ? "¡Hongos en la comida! Se perdió comida: -" + d.ToString("0") + "." : "MOHO: apareció moho en la comida guardada. Comida -" + d.ToString("0") + "." });
                return true;
            }
            case 2:
            {
                float d = 8f * sf; v[H] -= d; C(k, Res.Health, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "astro",
                    text = Kids ? "El equipo se resfrió. Salud -" + d.ToString("0") + "." : "RESFRIADO: en el espacio las defensas del cuerpo bajan. Salud -" + d.ToString("0") + "." });
                return true;
            }
            case 3:
            {
                float d = Tuning.NewsBadMood * 0.8f; v[M] -= d; C("news", Res.Morale, d);
                notes.Add(new EventNote { type = EvType.News, kind = LogKind.Bad, icon = "cross",
                    text = "MALA NOTICIA. " + BadNews[Rng.Next(0, BadNews.Length)] + " Ánimo -" + d.ToString("0") + "." });
                return true;
            }
            case 4:
            {
                if (!Session.Diff.panels) return false;
                float d = 15f; v[P] -= d; panelsNoClean -= d; C(k, Res.Panels, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "dust",
                    text = "POLVO FINO: una ráfaga ensució los paneles. Paneles -" + d.ToString("0") + "." });
                return true;
            }
            case 5:
            {
                if (!Session.Diff.house) return false;
                float d = Tuning.CrackDamage * HouseDamageMult(); v[I] -= d; C("ev_crack", Res.Integrity, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "house",
                    text = "GRIETA: una pared se agrietó por el cambio de temperatura. Casa -" + d.ToString("0") + (ShieldBroken ? " (¡sin escudo el daño es doble!)" : "") + "." });
                return true;
            }
            case 6:
            {
                if (!Session.Diff.house) return false;
                float d = Tuning.QuakeDamage * HouseDamageMult(); v[I] -= d; v[M] -= Tuning.QuakeMood;
                C("ev_quake", Res.Integrity, d); C("ev_quake", Res.Morale, Tuning.QuakeMood);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "house",
                    text = (Session.Site == null || Session.site == 1 ? "SISMO LUNAR: la Luna tembló y la casa se sacudió. " : "MARSMOTO: Marte tembló y la casa se sacudió. ") +
                           "Casa -" + d.ToString("0") + ", ánimo -" + Tuning.QuakeMood.ToString("0") + (ShieldBroken ? " (¡sin escudo el daño es doble!)" : "") + "." });
                return true;
            }
            case 7:
            {
                if (!Session.Diff.house) return false;
                float d = Tuning.HitDamage * HouseDamageMult(); v[I] -= d; C("ev_hit", Res.Integrity, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "house",
                    text = "RÁFAGA DE ARENA Y ROCAS: golpeó la casa. Casa -" + d.ToString("0") + (ShieldBroken ? " (¡sin escudo el daño es doble!)" : "") + "." });
                return true;
            }
            case 8:
            {
                var cand = new List<int>();
                if (!broken[SysGh]) cand.Add(SysGh);
                if (!broken[SysRc]) cand.Add(SysRc);
                if (Session.Diff.heater && !broken[SysHt]) cand.Add(SysHt);
                if (Session.Diff.panels && !broken[SysPn]) cand.Add(SysPn);
                if (cand.Count == 0) return false;
                int sy = cand[Rng.Next(0, cand.Count)];
                broken[sy] = true;
                float d = 8f * sv; v[E] -= d; C(k, Res.Energy, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "wrench",
                    text = "AVERÍA SORPRESA: se rompió " + EventDirector.SystemNames[sy].ToUpper() + " y saltó una chispa (batería -" + d.ToString("0") + "). Usa REPARAR." });
                return true;
            }
            case 9:
            {
                float d = 15f * sv; v[O] -= d; C(k, Res.Oxygen, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "air",
                    text = "FILTRO DE AIRE TAPADO: se perdió aire. Aire -" + d.ToString("0") + "." });
                return true;
            }
            case 10:
            {
                float d = 18f * sv; v[F] -= d; C(k, Res.Food, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "food",
                    text = "PLAGA EN EL INVERNADERO: unos bichos se comieron las plantas. Comida -" + d.ToString("0") + "." });
                return true;
            }
            case 11:
            {
                float d = 20f * sv; v[B] -= d; C(k, Res.Shield, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "shield",
                    text = "ESCUDO AGRIETADO: una grieta dejó pasar radiación. Escudo -" + d.ToString("0") + "." });
                return true;
            }
            case 12:
            {
                if (!Session.Diff.morale) return false;
                float d = 12f * sv; v[M] -= d; C(k, Res.Morale, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "face",
                    text = "DISCUSIÓN: el equipo se peleó por el turno de la comida. Ánimo -" + d.ToString("0") + "." });
                return true;
            }
            case 13:
            {
                float d = 25f * sv; v[E] -= d; C(k, Res.Energy, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "battery",
                    text = "CELDA DE BATERÍA DAÑADA: se escapó energía. Batería -" + d.ToString("0") + "." });
                return true;
            }
            case 14:
            {
                if (!Session.Diff.panels) return false;
                float d = 20f; v[P] -= d; panelsNoClean -= d; C(k, Res.Panels, d);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "panel",
                    text = "MICROMETEORITOS: granitos de roca rayaron los paneles. Paneles -" + d.ToString("0") + "." });
                return true;
            }
            case 15:
            {
                float d = 10f * sv; v[H] -= d; v[M] -= 5f; C(k, Res.Health, d); C(k, Res.Morale, 5f);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "astro",
                    text = "FIEBRE: un astronauta amaneció con fiebre. Salud -" + d.ToString("0") + ", ánimo -5." });
                return true;
            }
            case 16:
            {
                float d = 15f * sv; v[F] -= d; v[H] -= 3f; C(k, Res.Food, d); C(k, Res.Health, 3f);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "food",
                    text = "COMIDA EN MAL ESTADO: unas raciones se echaron a perder. Comida -" + d.ToString("0") + ", salud -3." });
                return true;
            }
            default:
            {
                float d = 12f * sv; v[E] -= d; v[M] -= 5f; C(k, Res.Energy, d); C(k, Res.Morale, 5f);
                notes.Add(new EventNote { type = EvType.Mishap, kind = LogKind.Bad, icon = "bolt",
                    text = "SOBRECARGA: una máquina pidió demasiada energía de golpe. Batería -" + d.ToString("0") + ", ánimo -5." });
                return true;
            }
        }
    }

    // =================================================================== tareas
    // Texto corto de cómo está rindiendo el equipo (se añade al mensaje de cada tarea).
    string MoodNote()
    {
        switch (MoodTier())
        {
            case 3: return " ¡Equipo animado: rinde más!";
            case 1: return " (equipo cansado: rinde menos)";
            case 0: return " (equipo sin ánimo: rinde muy poco)";
        }
        return boostNights > 0 ? " ¡Con la buena noticia rinde más!" : "";
    }

    public bool Do(TaskId t, out string msg)
    {
        msg = "";
        if (t == TaskId.Call) return false;          // las llamadas a la Tierra van por CallEarth
        if (ap <= 0) { msg = "Ya no quedan tareas esta noche. ¡Termina la noche!"; return false; }
        if (t == TaskId.Aim || t == TaskId.Upload) return false;      // van por Aim() y Upload()

        if (t == TaskId.Experiment && aimEarth && !Kids)
        { msg = "La antena mira a la TIERRA. Apúntala a CIENCIA para hacer experimentos."; return false; }
        if (t == TaskId.Experiment && buffer > Tuning.BufferMax - Tuning.ExperimentData + 0.01f)
        { msg = Kids ? "¡La memoria está llena! Manda los datos a la Tierra." : "La memoria está llena (" + DataPacks + " de " + MaxDataPacks + "). Apunta a la TIERRA y SUBE los datos."; return false; }
        if (t != TaskId.Rest && NoPower(t, out msg)) return false;

        float k = MoodMult();
        float en = Cost(t);
        string key = "";
        switch (t)
        {
            case TaskId.Reinforce:
            {
                float gain = Mathf.Min(Tuning.ReinforceAmount * k, 100f - v[B]);
                v[B] += gain; v[M] -= Tuning.FatigueReinforce; v[E] -= en;
                C("shield", Res.Energy, en); C("shield", Res.Morale, Tuning.FatigueReinforce);
                key = "shield";
                msg = "Escudo reforzado (+" + gain.ToString("0") + ")." + MoodNote();
                break;
            }
            case TaskId.Repair:
            {
                int s = FirstBroken();
                bool guide = repairGuide;
                repairGuide = false;
                float gain = Mathf.Min((Tuning.RepairAmount + (guide ? Tuning.RepairGuideBonus : 0f)) * k, 100f - v[I]);
                v[I] += gain;
                G("repair", Res.Integrity, gain);
                if (!guide)
                {
                    v[M] -= Tuning.FatigueRepair; v[E] -= en;
                    C("repair", Res.Energy, en); C("repair", Res.Morale, Tuning.FatigueRepair);
                }
                key = "repair";
                string extra = guide ? " (con la guía de la Tierra: gratis)" : "";
                if (s >= 0) { broken[s] = false; msg = EventDirector.SystemNames[s] + " reparado. Casa +" + gain.ToString("0") + "." + extra + MoodNote(); }
                else msg = "Casa reparada: +" + gain.ToString("0") + "." + extra + MoodNote();
                break;
            }
            case TaskId.Clean:
            {
                float gain = Mathf.Min(Tuning.CleanAmount * k, 100f - v[P]);
                v[P] += gain; v[M] -= Tuning.FatigueClean; v[E] -= en;
                C("clean", Res.Energy, en); C("clean", Res.Morale, Tuning.FatigueClean);
                key = "clean";
                msg = "Paneles limpios (+" + gain.ToString("0") + ")." + MoodNote();
                break;
            }
            case TaskId.Experiment:
            {
                v[E] -= en; v[M] -= Tuning.FatigueExperiment;
                int packs = 1;
                bool dbl = MoodTier() == 3 && buffer <= Tuning.BufferMax - 2f * Tuning.ExperimentData + 0.01f && Rng.NextDouble() < Tuning.DoubleExperiment;
                if (dbl) packs = 2;
                buffer = Mathf.Min(Tuning.BufferMax, buffer + Tuning.ExperimentData * packs);
                C("exp", Res.Energy, en); C("exp", Res.Morale, Tuning.FatigueExperiment);
                key = "exp";
                msg = (dbl ? "¡Equipo animado: el experimento salió DOBLE! " : "¡Experimento listo! ") +
                      (Kids ? "Datos: " + DataPacks + " de " + MaxDataPacks + "."
                            : "Datos guardados: " + DataPacks + " de " + MaxDataPacks + ". Hay que mandarlos a la Tierra.");
                break;
            }
            case TaskId.Rest:
            {
                float m = Mathf.Min(Tuning.RestMorale, 100f - v[M]), h = Mathf.Min(Tuning.RestHealth, 100f - v[H]);
                v[M] += Tuning.RestMorale; v[H] += Tuning.RestHealth;
                float be = Mathf.Min(Tuning.RestEnergy, 100f - v[E]);
                v[E] += Tuning.RestEnergy;
                G("rest", Res.Morale, m); G("rest", Res.Health, h); G("rest", Res.Energy, be);
                key = "rest";
                msg = "El equipo descansa: ánimo +" + m.ToString("0") + ", salud +" + h.ToString("0") + ", batería +" + be.ToString("0") + ". ¡Pero da hambre!";
                break;
            }
        }
        SpendFood(key, t);                                          // CADA tarea da hambre
        if (t == TaskId.Experiment) { v[H] -= Tuning.HealthExperiment; C("exp", Res.Health, Tuning.HealthExperiment); }     // y los experimentos cansan el cuerpo
        if (ledger != null) ledger.Count(key);
        ap--; tasksUsed++; actsToday[(int)t]++; actsTotal[(int)t]++;
        Clamp();
        return true;
    }

    // Mueve la antena: de CIENCIA a TIERRA o al revés. Cuesta una tarea y un poco de batería.
    public bool Aim(out string msg)
    {
        msg = "";
        if (Kids) { msg = "La antena apunta sola."; return false; }
        if (ap <= 0) { msg = "Ya no quedan tareas esta noche. ¡Termina la noche!"; return false; }
        if (NoPower(TaskId.Aim, out msg)) return false;
        float en = Cost(TaskId.Aim);
        aimEarth = !aimEarth;
        v[E] -= en; v[M] -= Tuning.AimFatigue;
        C("aim", Res.Energy, en); C("aim", Res.Morale, Tuning.AimFatigue);
        SpendFood("aim", TaskId.Aim);
        if (ledger != null) ledger.Count("aim");
        ap--; tasksUsed++; actsToday[(int)TaskId.Aim]++; actsTotal[(int)TaskId.Aim]++;
        Clamp();
        msg = aimEarth ? "La antena mira a la TIERRA: ya puedes subir los datos."
                       : "La antena mira a CIENCIA: ya puedes hacer experimentos.";
        return true;
    }

    // Sube datos del buffer a la Tierra. Hay que apuntar a la Tierra, y el polvo tapa la señal.
    public bool Upload(bool linkBlocked, out string msg)
    {
        msg = "";
        if (ap <= 0) { msg = "Ya no quedan tareas esta noche. ¡Termina la noche!"; return false; }
        if (!aimEarth && !Kids) { msg = "La antena no mira a la TIERRA. Apúntala primero."; return false; }
        if (linkBlocked && Session.Diff.dataLoss) { msg = "El polvo tapa la señal: esta noche no se puede mandar nada."; return false; }
        if (buffer < 0.5f) { msg = "No hay datos. Haz experimentos para llenar la memoria."; return false; }
        if (NoPower(TaskId.Upload, out msg)) return false;
        float en = Cost(TaskId.Upload);
        float sent = Mathf.Min(buffer, UploadPacks() * Tuning.ExperimentData);
        buffer -= sent; uploaded += sent;
        experiments = Mathf.FloorToInt(uploaded / Tuning.ExperimentData + 0.001f);
        v[E] -= en; v[M] -= Tuning.UploadFatigue;
        C("upload", Res.Energy, en); C("upload", Res.Morale, Tuning.UploadFatigue);
        G("exp", Res.Science, sent / Tuning.ExperimentData);
        SpendFood("upload", TaskId.Upload);
        if (ledger != null) ledger.Count("upload");
        ap--; tasksUsed++; actsToday[(int)TaskId.Upload]++; actsTotal[(int)TaskId.Upload]++;
        Clamp();
        msg = Kids ? "¡Mandaste " + Mathf.RoundToInt(sent / Tuning.ExperimentData) + " paquetes a la Tierra!"
                   : "¡Mandaste " + Mathf.RoundToInt(sent / Tuning.ExperimentData) + " paquetes de datos a la Tierra! Ciencia enviada: " + experiments + ".";
        return true;
    }

    // Pide algo a la Tierra. Cuesta una tarea y energía de antena AHORA; la respuesta llega 'delay' noches después
    // (y puede traer una buena o una mala noticia).
    public bool CallEarth(int kind, int delay, out string msg)
    {
        msg = "";
        if (ap <= 0) { msg = "Ya no quedan tareas esta noche. ¡Termina la noche!"; return false; }
        if (reqKind >= 0) { msg = "Ya esperas una respuesta: la Tierra contesta la noche " + reqDay + "."; return false; }
        if (NoPower(TaskId.Call, out msg)) return false;
        float en = Cost(TaskId.Call);
        v[E] -= en; v[M] -= Tuning.CallFatigue;
        C("call", Res.Energy, en); C("call", Res.Morale, Tuning.CallFatigue);
        SpendFood("call", TaskId.Call);
        if (ledger != null) ledger.Count("call");
        reqKind = kind; reqSent = day; reqDay = day + Mathf.Max(1, delay);
        ap--; tasksUsed++; actsToday[(int)TaskId.Call]++; actsTotal[(int)TaskId.Call]++;
        Clamp();
        msg = "Mensaje enviado. La Tierra contestará la noche " + reqDay + ".";
        return true;
    }

    // Enciende/apaga una máquina. Es GRATIS (no gasta tareas): lo que cuesta es la batería que gasta cada noche.
    public bool Toggle(int s, bool on, out string msg)
    {
        msg = "";
        if (SysOn(s) == on) { msg = "Ya está " + (on ? "encendido." : "apagado."); return false; }
        if (broken[s]) { msg = EventDirector.SystemNames[s] + " está roto: usa REPARAR."; return false; }
        if (s == SysGh) gh = on; else if (s == SysRc) rc = on; else ht = on;
        string key = s == SysGh ? "gh" : (s == SysRc ? "rc" : "ht");
        if (on && ledger != null) ledger.Count(key);
        msg = EventDirector.SystemNames[s] + (on ? " encendido." : " apagado.");
        return true;
    }

    // =================================================================== fin de noche
    // Devuelve null si la tripulación sigue viva, o "power" / "oxygen" / "health" / "structure".
    public string EndDay(List<GameEvent> events)
    {
        int left = ap;
        tasksWasted += ap;
        bonusRes = -1; bonusAmt = 0f; bonusPerfect = false; rescued.Clear();
        string dead = EndDayCore(events, ledger);
        if (dead != null) dead = TryRescue(dead);
        if (dead == null)
        {
            if (boostNights > 0) boostNights--;
            bool sentData = Tuning.BonusNeedsUpload == 0 || actsToday[(int)TaskId.Upload] > 0;     // sin transmisión no hay bono
            if (left <= Tuning.BonusMaxWasted && sentData) GiveNightBonus();
        }
        return dead;
    }

    // Si algo llegó a 0, la Tierra manda una cápsula de EMERGENCIA (solo unas pocas por misión).
    string TryRescue(string dead)
    {
        int guard = 0;
        while (dead != null && lifelines > 0 && guard++ < 4)
        {
            int idx = dead == "power" ? E : (dead == "oxygen" ? O : (dead == "health" ? H : I));
            v[idx] = Tuning.RescueAmount;
            lifelines--; rescued.Add(idx);
            G("ev_rescue", (Res)idx, Tuning.RescueAmount);
            if (v[E] <= 0f) dead = "power";
            else if (v[O] <= 0f) dead = "oxygen";
            else if (v[H] <= 0f) dead = "health";
            else if (v[I] <= 0f) dead = "structure";
            else dead = null;
        }
        return dead;
    }

    // Recompensa por jugar bien (casi todas las tareas usadas): sube lo que más le falta al equipo.
    void GiveNightBonus()
    {
        int[] cand = { E, O, F, H, I, M };
        int best = -1;
        float bv = 999f;
        for (int k = 0; k < cand.Length; k++)
        {
            int i = cand[k];
            if (!Session.ResShown(i)) continue;
            if (v[i] < bv) { bv = v[i]; best = i; }
        }
        if (best < 0) return;
        if (bv >= Tuning.BonusPerfect) { bonusAp = 1; bonusPerfect = true; return; }
        float amt;
        switch (best)
        {
            case E: amt = Tuning.BonusEnergy; break;
            case O: amt = Tuning.BonusAir; break;
            case F: amt = Tuning.BonusFood; break;
            case H: amt = Tuning.BonusHealth; break;
            case I: amt = Tuning.BonusHouse; break;
            default: amt = Tuning.BonusMood; break;
        }
        float real = Mathf.Min(amt, 100f - v[best]);
        v[best] += amt;
        bonusRes = best; bonusAmt = real;
        G("bonus", (Res)best, real);
        Clamp();
    }

    // Lo que cambiaría esta noche si terminaras la noche ahora (para mostrarlo en el tablero).
    public float[] ProjectOvernight(List<GameEvent> events)
    {
        var c = Clone();
        c.EndDayCore(events, null);
        var d = new float[8];
        for (int i = 0; i < 8; i++) d[i] = c.v[i] - v[i];
        return d;
    }

    // ---- Cosas que están FALLANDO (cada una hace bajar el ánimo del equipo)
    public int Troubles()
    {
        var d = Session.Diff;
        int n = 0;
        for (int s = 0; s < 4; s++) if (broken[s]) n++;
        if (v[E] < 25f) n++;
        if (v[O] < 30f) n++;
        if (v[F] < 30f) n++;
        if (v[H] < 30f) n++;
        if (d.house && v[I] < 30f) n++;
        if (d.panels && v[P] < 20f) n++;
        return n;
    }

    // Ánimo que se pierde en una noche por lo que está fallando.
    public float TroubleMood()
    {
        return Mathf.Min(Tuning.MoodTroubleCap, Troubles() * Tuning.MoodPerTrouble) * Session.Diff.ease;
    }

    // Comida que gasta una tarea (cada acción da hambre).
    public float FoodCost(TaskId t)
    {
        float b = 0f;
        switch (t)
        {
            case TaskId.Reinforce: b = Tuning.FoodReinforce; break;
            case TaskId.Repair: b = repairGuide ? 0f : Tuning.FoodRepair; break;
            case TaskId.Clean: b = Tuning.FoodClean; break;
            case TaskId.Experiment: b = Tuning.FoodExperiment; break;
            case TaskId.Rest: b = Tuning.FoodRest; break;
            case TaskId.Aim: b = Tuning.FoodAim; break;
            case TaskId.Upload: b = Tuning.FoodUpload; break;
            case TaskId.Call: b = Tuning.FoodCall; break;
        }
        return b * Session.Diff.foodMult;
    }

    void SpendFood(string key, TaskId t)
    {
        float f = FoodCost(t);
        if (f <= 0f) return;
        v[F] -= f;
        C(key, Res.Food, f);
    }

    // Cuánta luz aprovecha un panel con ese % de limpieza (con paneles muy limpios carga MÁS rápido).
    public static float PanelEff(float p) { return Mathf.Pow(Mathf.Clamp01(p / 100f), Tuning.PanelCurve); }

    public bool ShieldBroken { get { return v[B] <= Tuning.ShieldBrokenBelow; } }
    public bool HouseBroken { get { return Session.Diff.house && v[I] <= Tuning.HouseBrokenBelow; } }

    // Con el escudo ROTO la casa sufre el doble. Con escudo igual recibe daño, aunque un poco menos.
    public float HouseDamageMult()
    {
        if (!Session.Diff.house) return 1f;
        if (ShieldBroken) return Tuning.ShieldBrokenHouseMult;
        return 1f - Tuning.ShieldHouseReduction * Mathf.Clamp01(v[B] / 100f);
    }

    // Por qué se está muriendo el equipo (para la pantalla de muerte).
    public string DeathReason()
    {
        if (v[O] <= 0.5f) return "air";
        if (v[F] <= 0.5f) return "food";
        if (HouseBroken) return "house";
        if (Mathf.Abs(bodyTemp) >= Tuning.TempDamageAt) return bodyTemp < 0f ? "cold" : "heat";
        if (Session.Diff.morale && v[M] < Tuning.MoodOk) return "mood";
        return "radiation";
    }

    // ---- Temperatura del equipo: la noche larga SIN calefacción los congela; la calefacción encendida durante una tormenta solar los acalora.
    public bool StormActive(List<GameEvent> events)
    {
        foreach (var e in events)
            if (e.type == EvType.Solar && day >= e.day && day <= e.day + Tuning.StormHeatLingerDays) return true;
        return false;
    }

    void StepTemp(float secs, bool cold, bool storm)
    {
        if (!Session.Diff.heater) { bodyTemp = 0f; return; }
        float rate = 0f;
        if (cold && !HtOk) rate -= Tuning.TempChillPerSec;                         // noche larga sin calefacción: se enfría
        if (storm) rate += Tuning.TempStormPerSec + (HtOk ? Tuning.TempHeatPerSec : 0f);   // tormenta solar: sube; con la calefacción prendida sube MUCHO más
        if (Mathf.Abs(rate) > 0.0001f) bodyTemp += rate * secs;
        else bodyTemp = Mathf.MoveTowards(bodyTemp, 0f, Tuning.TempRelaxPerSec * secs);
        bodyTemp = Mathf.Clamp(bodyTemp, -100f, 100f);
    }

    // Temperatura que se muestra (grados): normal = TempNormalC; cada punto interno son TempCPerUnit grados.
    public float TempC { get { return Tuning.TempNormalC + bodyTemp * Tuning.TempCPerUnit; } }

    // -1 = CONGELADOS (copo de nieve), +1 = CALOR (llama), 0 = bien
    public int TempState
    {
        get
        {
            if (!Session.Diff.heater) return 0;
            if (bodyTemp <= -Tuning.TempIconAt) return -1;
            if (bodyTemp >= Tuning.TempIconAt) return 1;
            return 0;
        }
    }

    // Cuántas máquinas están encendidas y funcionando (cada una gasta MachineDrainPerSec de batería por segundo).
    public int MachinesOn { get { return (GhOk ? 1 : 0) + (RcOk ? 1 : 0) + (HtOk ? 1 : 0); } }

    // Batería que gastan las máquinas encendidas POR SEGUNDO (para mostrar el CONSUMO en pantalla).
    public float MachineDrain(List<GameEvent> events) { return Tuning.MachineDrainPerSec * MachinesOn; }

    struct DayFlowSet
    {
        public float gen, dev, coldBat, decay, mood, foodGen, foodBase, o2Gen, o2Base;
        public bool cold;
    }

    // Lo que pasa en un día entero con lo que está encendido AHORA (sin aplicarlo).
    DayFlowSet DayFlows(List<GameEvent> events)
    {
        var f = new DayFlowSet();
        bool cold;
        float solar = EventDirector.SolarFactor(events, day, out cold);
        var site = Session.Site;
        float ez = Session.Diff.ease;
        float smax = Tuning.SolarMax * site.solarMult * Session.Diff.solarScale;
        float pk = broken[SysPn] ? 0.5f : 1f;
        f.gen = smax * PanelEff(v[P]) * pk * solar;              // más paneles limpios = más rápido se carga
        f.cold = cold;                                           // (las máquinas ya no gastan por noche: gastan POR SEGUNDO en LiveTick)
        f.coldBat = cold ? Tuning.ColdBatteryDrain * (HtOk ? Tuning.ColdHeaterFactor : 1f) : 0f;
        f.decay = Session.Diff.panels ? Tuning.PanelDecay * site.panelDecayMult * ez : 0f;
        f.mood = Session.Diff.morale ? TroubleMood() : 0f;
        f.foodGen = 0f;                                          // el invernadero sube la comida POR SEGUNDO (en LiveTick)
        f.foodBase = Tuning.FoodBase * ez;
        f.o2Gen = 0f; f.o2Base = 0f;                              // el aire sube y baja POR SEGUNDO (en LiveTick)
        return f;
    }

    static float Step(float total, float share, float sec, float dt, float done)
    {
        return Mathf.Clamp(total * share / sec * dt, 0f, Mathf.Max(0f, total * share - done));
    }

    // Avanza el reloj del día dt segundos:
    //  - los paneles cargan la batería, las máquinas encendidas la gastan, el frío también;
    //  - el invernadero sube la comida y la máquina de aire sube el aire; vivir gasta aire y comida;
    //  - el polvo ensucia los paneles y lo que falla baja el ánimo;
    //  - y, POR SEGUNDO: ánimo bajo, sin aire, sin comida, casa rota y frío le quitan salud.
    // Devuelve true si algo cambió. Si la salud llega a 0, liveDead dice por qué.
    public bool LiveTick(float dt, List<GameEvent> events)
    {
        rateE = rateP = rateM = rateO = rateF = rateH = 0f;
        if (dt <= 0f || liveDead != null) return false;
        var f = DayFlows(events);
        var dif = Session.Diff;
        float sh = Tuning.LiveShare, sec = Mathf.Max(1f, Tuning.LiveSeconds), shD = Tuning.LiveShareDust;
        float dGen = Step(f.gen, sh, sec, dt, liveGen);
        int nMach = MachinesOn;
        float dDev = Tuning.MachineDrainPerSec * nMach * dt;                      // CADA máquina encendida gasta MachineDrainPerSec por segundo
        float dCold = Step(f.coldBat, sh, sec, dt, liveCold);
        float dDec = Step(f.decay, shD, sec, dt, liveDecay);
        float dMood = Step(f.mood, sh, sec, dt, liveMood);
        float dFg = GhOk ? Tuning.GreenhouseFoodPerSec * dt : 0f;                  // invernadero encendido: sube la comida cada segundo
        float dFb = Step(f.foodBase, sh, sec, dt, liveFoodBase);
        float dOg = RcOk ? Tuning.RecyclerAirPerSec * dt : 0f;                    // máquina de aire encendida: sube el aire cada segundo
        float dOb = Tuning.AirDrainPerSec * dt;                                   // respirar: el aire baja siempre
        // ---- eventos que pasan AHORA: el polvo ensucia los paneles y raspa la casa; la tormenta solar sacude la casa
        bool dustNow = false, stormNow = false;
        foreach (var evn in events) { if (!evn.ActiveOn(day)) continue; if (evn.type == EvType.Dust) dustNow = true; else if (evn.type == EvType.Solar) stormNow = true; }
        float dDust = (dif.panels && dustNow) ? Tuning.DustPanelsPerSec * dt : 0f;
        float dHouseEv = (dif.house && (dustNow || stormNow)) ? ((dustNow ? Tuning.DustHousePerSec : 0f) + (stormNow ? Tuning.StormHousePerSec : 0f)) * HouseDamageMult() * dt : 0f;

        // ---- lo que le pasa a la salud y al ánimo POR SEGUNDO
        float hps = 0f, mps = 0f;
        if (dif.morale)
        {
            if (v[M] < Tuning.MoodLow) hps += Tuning.MoodLowHealthPerSec;
            else if (v[M] < Tuning.MoodOk) hps += Tuning.MoodTiredHealthPerSec;
        }
        if (v[O] <= 0.01f) hps += Tuning.NoAirHealthPerSec;
        if (v[F] <= 0.01f) hps += Tuning.StarveHealthPerSec;
        if (HouseBroken) { hps += Tuning.HouseBrokenHealthPerSec; mps += Tuning.HouseBrokenMoodPerSec; }
        float tBefore = bodyTemp;
        StepTemp(dt, f.cold, StormActive(events));                                  // frío o calor
        if (Mathf.Abs(bodyTemp) >= Tuning.TempDamageAt) { hps += Tuning.TempHealthPerSec; mps += Tuning.TempMoodPerSec; }
        hps *= dif.liveDrain; mps *= dif.liveDrain;

        float acc = dGen + dDev + dCold + dDec + dDust + dHouseEv + dMood + dFg + dFb + dOg + dOb;
        if (acc <= 0f && hps <= 0f && mps <= 0f && Mathf.Approximately(tBefore, bodyTemp)) return false;

        liveGen += dGen; liveCons += dDev; liveCold += dCold; liveDecay += dDec; liveMood += dMood;
        liveFoodGen += dFg; liveFoodBase += dFb; liveO2Gen += dOg; liveO2Base += dOb;

        float e = v[E] + dGen - dDev - dCold;
        v[E] = Mathf.Clamp(e, 0f, 100f);
        if (ledger != null && nMach > 0)
        {
            float one = Tuning.MachineDrainPerSec * dt;
            if (GhOk) { C("gh", Res.Energy, one); G("gh", Res.Food, Tuning.GreenhouseFoodPerSec * dt); }
            if (RcOk) { C("rc", Res.Energy, one); G("rc", Res.Oxygen, Tuning.RecyclerAirPerSec * dt); }
            if (HtOk) C("ht", Res.Energy, one);
        }
        if (v[E] <= 0.001f && !LiveRescue(E)) liveDead = "power";                // batería en 0: una cápsula de emergencia te salva; sin cápsulas, apagón y fin de la misión
        v[P] = Mathf.Max(0f, v[P] - dDec - dDust); panelsNoClean -= dDec + dDust;
        if (dHouseEv > 0f) { v[I] -= dHouseEv; C(dustNow ? "ev_dust" : "ev_solar", Res.Integrity, dHouseEv); }
        if (dDust > 0f) C("ev_dust", Res.Panels, dDust);
        v[M] -= dMood + mps * dt;
        v[F] += dFg - dFb;
        v[O] += dOg - dOb;
        v[H] -= hps * dt;

        rateE = (dGen - dDev - dCold) / dt; rateP = -(dDec + dDust) / dt; rateM = -(dMood / dt + mps);
        rateO = (dOg - dOb) / dt; rateF = (dFg - dFb) / dt; rateH = -hps;
        Clamp();
        if (liveDead == null && v[H] <= 0.001f && !LiveRescue(H)) liveDead = DeathReason();
        return true;
    }

    // Una cápsula de EMERGENCIA también salva a mitad de la noche: si la batería o la salud llegan a 0 y quedan cápsulas,
    // la Tierra repone lo que falta (y, si la causa es el aire o la comida, también eso para que no vuelvan a caer enseguida).
    public int liveRescued = -1;      // recurso que acaba de salvar una cápsula (ShiftScreen lo muestra y lo borra)
    bool LiveRescue(int idx)
    {
        if (Tuning.LiveRescue < 1 || lifelines <= 0) return false;
        lifelines--; Campaign.rescues++;
        v[idx] = Tuning.RescueAmount;
        if (idx == H)
        {
            if (v[O] <= 0.5f) v[O] = Tuning.RescueAmount;
            if (v[F] <= 0.5f) v[F] = Tuning.RescueAmount;
        }
        G("ev_rescue", (Res)idx, Tuning.RescueAmount);
        liveRescued = idx;
        return true;
    }

    string EndDayCore(List<GameEvent> events, Ledger L)
    {
        bool cold;
        float solar = EventDirector.SolarFactor(events, day, out cold);
        bool night = EventDirector.Has(events, day, EvType.Night);
        bool dust = EventDirector.Has(events, day, EvType.Dust);
        var site = Session.Site;
        float smax = Tuning.SolarMax * site.solarMult * Session.Diff.solarScale;       // la Luna recibe más luz que Marte
        float pk = broken[SysPn] ? 0.5f : 1f;
        float eff = PanelEff(v[P]);
        float pe = eff * pk;
        float gen = smax * pe * solar;
        bool ghOk = GhOk, rcOk = RcOk, htOk = HtOk;
        float hm = HouseDamageMult();                       // sin escudo, la casa sufre el doble
        float lm = Session.Diff.liveDrain;

        float ez = Session.Diff.ease;                        // niveles fáciles: todo gasta y se desgasta más despacio
        float dm = Session.Diff.drainMult;                   // en los niveles difíciles el puesto y las máquinas gastan MUCHA más batería
        float baseCons = Tuning.BaseDrain * ez * dm;
        float cons = baseCons;
        float coldBat = cold ? Tuning.ColdBatteryDrain * (htOk ? Tuning.ColdHeaterFactor : 1f) : 0f;

        if (L != null)
        {
            if (ghOk) { L.Day("gh"); }
            if (rcOk) { L.Day("rc"); }
            if (htOk) { L.Day("ht"); }
            if (gh && broken[SysGh]) L.Cost("broken", Res.Food, Tuning.GreenhouseFoodPerSec * Tuning.LiveSeconds);
            if (rc && broken[SysRc]) L.Cost("broken", Res.Oxygen, Tuning.RecyclerAirPerSec * Tuning.LiveSeconds);
            float diffP = PanelEff(v[P]) - PanelEff(panelsNoClean);
            if (diffP > 0f) L.Gain("clean", Res.Energy, diffP * smax * solar * pk);
            L.Cost("dirt", Res.Energy, smax * solar * (1f - eff) * pk);
            if (broken[SysPn]) L.Cost("broken", Res.Energy, smax * solar * eff * 0.5f);
            if (solar < 1f) L.Cost(night ? "ev_night" : "ev_dust", Res.Energy, smax * pe * (1f - solar));
            if (coldBat > 0f) L.Cost("ev_night", Res.Energy, coldBat);
        }

        float devCons = cons - baseCons;                                      // lo que gastan las máquinas encendidas
        v[E] += Mathf.Max(0f, gen - liveGen) - (baseCons + Mathf.Max(0f, devCons - liveCons) + Mathf.Max(0f, coldBat - liveCold));    // lo ya aplicado en vivo no se repite
        if (v[E] > 100f) v[E] = 100f;

        v[F] -= Mathf.Max(0f, Tuning.FoodBase * ez - liveFoodBase);
        v[I] -= Tuning.Wear * ez * hm;
        v[B] -= Tuning.ShieldDecay * ez;
        float decayNight = Mathf.Max(0f, Tuning.PanelDecay * site.panelDecayMult * ez - liveDecay);     // el polvo lunar se pega más
        v[P] -= decayNight;
        panelsNoClean -= decayNight;

        float bpos = Mathf.Max(0f, v[B]);
        float dailyDose = Tuning.DailyDose * site.doseMult * ez;  // en la Luna la radiación diaria es el doble
        v[H] -= dailyDose * (1f - bpos / 100f);
        if (L != null) L.Gain("shield", Res.Health, dailyDose * bpos / 100f);
        if (v[O] > 30f && v[F] > 30f) v[H] += Tuning.HealthRegen;

        v[M] -= 3f * ez;
        float troubleM = TroubleMood();                           // cada cosa que falla baja el ánimo (rápido)
        v[M] -= Mathf.Max(0f, troubleM - liveMood);
        if (L != null) L.Cost("trouble", Res.Morale, troubleM);
        if (v[F] <= 0f) v[H] -= 12f;

        // frío o calor fuerte: hacen daño al equipo (la noche cuenta unos segundos)
        StepTemp(Tuning.TempNightSecs, cold, StormActive(events));
        if (Mathf.Abs(bodyTemp) >= Tuning.TempDamageAt)
        {
            float th = Tuning.TempHealthPerSec * Tuning.TempNightSecs * lm, tm = Tuning.TempMoodPerSec * Tuning.TempNightSecs * lm;
            v[H] -= th; v[M] -= tm;
            if (L != null) { L.Cost("temp", Res.Health, th); L.Cost("temp", Res.Morale, tm); }
        }

        // la casa ROTA hace daño al equipo: salud y ánimo bajan de golpe
        if (Session.Diff.house && v[I] <= Tuning.HouseBrokenBelow)
        {
            float hh = Tuning.HouseBrokenNightHealth * lm, hmd = Tuning.HouseBrokenNightMood * lm;
            v[H] -= hh; v[M] -= hmd;
            if (L != null) { L.Cost("housebroken", Res.Health, hh); L.Cost("housebroken", Res.Morale, hmd); }
        }

        foreach (var e in events)
        {
            if (!e.ActiveOn(day)) continue;
            float sv = e.sev;
            if (e.type == EvType.Dust)
            {
                // el polvo ensucia los paneles y raspa la casa POR SEGUNDO mientras juegas (ver LiveTick)
            }
            else if (e.type == EvType.Night)
            {
                if (!htOk)
                {
                    float dmg = Tuning.LongNightDamage * sv * hm, hl = Tuning.ColdNightHealth * lm;
                    v[I] -= dmg; v[M] -= Tuning.LongNightMorale; v[H] -= hl;
                    string k = (ht && broken[SysHt]) ? "broken" : "ht";
                    if (L != null) { L.Cost(k, Res.Integrity, dmg); L.Cost(k, Res.Morale, Tuning.LongNightMorale); L.Cost(k, Res.Health, hl); }
                }
                else if (L != null)
                {
                    L.Gain("ht", Res.Integrity, Tuning.LongNightDamage * sv);
                    L.Gain("ht", Res.Morale, Tuning.LongNightMorale);
                }
            }
        }
        Clamp();

        string dead = null;
        if (v[E] <= 0f) dead = "power";
        else if (v[O] <= 0f) dead = "oxygen";
        else if (v[H] <= 0f) dead = "health";
        else if (v[I] <= 0f) dead = "structure";
        day++;
        return dead;
    }
}