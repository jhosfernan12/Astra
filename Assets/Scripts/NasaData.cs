using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================================
//  DATOS REALES DE LA NASA que usa el juego (y cómo se muestran dentro del propio juego).
//  - Llamaradas solares: fecha y clase REALES de DONKI (NASA). Hay una lista guardada ("archivo") y,
//    si hay internet, GameFlow descarga las más recientes en vivo.
//  - Dosis de radiación: Curiosity RAD (Marte) y Chang'e-4 LND (Luna).
//  - Retraso de comunicación: valores reales Tierra-Marte y Tierra-Luna.
// =====================================================================================================

// Una llamarada solar real. Clase: A, B, C, M, X (cada letra es 10 veces más fuerte que la anterior).
public class FlareDef
{
    public string date;      // "2024-05-10"
    public char letter;      // 'M' o 'X'
    public float num;        // 3.9 en X3.9
    public bool live;        // true = descargada en vivo de DONKI; false = lista guardada (archivo)

    static readonly string[] Months = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

    public string Cls { get { return letter.ToString() + num.ToString("0.0", CultureInfo.InvariantCulture); } }

    // Fuerza comparada con una X1 (una M5 es 0,5 de una X1).
    public float XValue { get { return letter == 'X' ? num : num * 0.1f; } }

    // Multiplicador de la dosis de la tormenta: llamaradas más fuertes = tormentas más peligrosas.
    public float Sev { get { return Mathf.Clamp(0.5f + 0.12f * XValue, 0.5f, 1.3f); } }

    public string DateEs
    {
        get
        {
            if (date == null || date.Length < 10) return "fecha desconocida";
            int m, d;
            int.TryParse(date.Substring(5, 2), out m);
            int.TryParse(date.Substring(8, 2), out d);
            string y = date.Substring(0, 4);
            return d + " " + Months[Mathf.Clamp(m - 1, 0, 11)] + " " + y;
        }
    }

    public string Source { get { return live ? "DONKI, EN VIVO" : "DONKI, archivo"; } }
    public string Short { get { return "clase " + Cls + " del " + DateEs; } }
}

// Un lugar donde puede estar el puesto. Los números "reales" salen de mediciones; los "multiplicadores" son del juego.
public class SiteDef
{
    public string name;            // MARTE
    public string place;           // "Marte" / "la Luna"
    public float dose;             // dosis real medida en la superficie (mSv por día)
    public string doseSource;      // de dónde sale
    public float doseMult;         // cuánto más daña la radiación diaria que en Marte (en el juego)
    public float solarMult;        // cuánta energía dan los paneles comparado con Marte
    public float panelDecayMult;   // qué tan rápido se ensucian los paneles
    public int dust;               // tormentas de polvo por misión
    public int meteors;            // impactos de micrometeoritos por misión
    public int nightExtra;         // noches extra de oscuridad larga
    public string nightName;       // nombre del evento "noche larga" en este sitio
    public string nightArt;        // "el" o "la" (para escribir bien las frases)
    public string nightInfo;
    public int delayDays;          // días de juego que tarda la respuesta de la Tierra (comprimido)
    public string delayReal;       // retraso real de la señal
    public string desc;            // texto de la tarjeta de selección
    public string tip;             // consejo de la Ing. Freeman al elegirlo
}

public static class NasaData
{
    // ------------------------------------------------------------------ sitios
    public static readonly SiteDef[] Sites =
    {
        new SiteDef
        {
            name = "MARTE", place = "Marte", dose = 0.67f, doseSource = "Curiosity RAD (NASA/JPL)",
            doseMult = 1f, solarMult = 1f, panelDecayMult = 1f, dust = 1, meteors = 1, nightExtra = 0,
            nightName = "INVIERNO MARCIANO", nightArt = "el", nightInfo = "poco sol y mucho frío",
            delayDays = 2, delayReal = "3 a 22 minutos",
            desc = "Poco aire, polvo e invierno helado.\n" +
                   "Radiación REAL: 0,67 mSv al día (Curiosity).\n" +
                   "La Tierra te contesta en 2 noches.",
            tip = "Marte tiene un aire muy fino que frena un poco la radiación. El polvo puede tapar el Sol y la Tierra queda lejos: su ayuda tarda 2 noches."
        },
        new SiteDef
        {
            name = "LUNA", place = "la Luna (polo sur)", dose = 1.37f, doseSource = "Chang'e-4 LND (Science Advances, 2020)",
            doseMult = 2f, solarMult = 1.3f, panelDecayMult = 1.5f, dust = 0, meteors = 2, nightExtra = 1,
            nightName = "OSCURIDAD LUNAR", nightArt = "la", nightInfo = "sin sol y mucho frío",
            delayDays = 1, delayReal = "1,3 segundos",
            desc = "Sin aire: más radiación y más rocas.\n" +
                   "Radiación REAL: 1,37 mSv al día (Chang'e-4).\n" +
                   "La Tierra te contesta en 1 noche.",
            tip = "En la Luna no hay aire que frene la radiación: ¡el escudo es muy importante! Llega más sol, pero el polvo se pega a los paneles y la oscuridad dura muchísimo."
        },
    };

    // ------------------------------------------------------------------ llamaradas reales guardadas (archivo DONKI)
    static FlareDef F(string date, char letter, float num)
    {
        return new FlareDef { date = date, letter = letter, num = num, live = false };
    }

    public static readonly FlareDef[] Curated =
    {
        F("2011-08-09", 'X', 6.9f),
        F("2012-03-07", 'X', 5.4f),
        F("2017-09-06", 'X', 9.3f),
        F("2017-09-10", 'X', 8.2f),
        F("2023-04-30", 'M', 2.4f),
        F("2024-05-06", 'X', 4.5f),
        F("2024-05-08", 'X', 1.0f),
        F("2024-05-10", 'X', 3.9f),
        F("2024-05-14", 'X', 8.7f),
        F("2024-05-15", 'X', 2.9f),
        F("2024-05-29", 'X', 1.4f),
        F("2024-10-03", 'X', 9.0f),
    };

    // ------------------------------------------------------------------ llamaradas en vivo (las llena GameFlow)
    public static readonly List<FlareDef> Live = new List<FlareDef>();
    public static int LiveState;     // 0 = no consultado, 1 = consultando, 2 = en vivo, 3 = sin conexión (se usa el archivo)

    // Todas las llamaradas posibles para esta partida (las de verdad cambian de una partida a otra).
    public static List<FlareDef> Pool()
    {
        var l = new List<FlareDef>();
        for (int i = 0; i < Live.Count; i++) l.Add(Live[i]);
        for (int i = 0; i < Curated.Length; i++)
        {
            bool dup = false;
            for (int k = 0; k < Live.Count; k++) if (Live[k].date == Curated[i].date && Live[k].Cls == Curated[i].Cls) dup = true;
            if (!dup) l.Add(Curated[i]);
        }
        return l;
    }

    static readonly Regex ReClass = new Regex("\"classType\"\\s*:\\s*\"([A-Z])([0-9]+(?:\\.[0-9]+)?)\"");
    static readonly Regex ReDate = new Regex("\"beginTime\"\\s*:\\s*\"([0-9]{4}-[0-9]{2}-[0-9]{2})");

    // Lee la respuesta JSON de https://api.nasa.gov/DONKI/FLR y guarda las llamaradas fuertes (X, o M5 o más).
    public static int ParseFlares(string json)
    {
        Live.Clear();
        if (string.IsNullOrEmpty(json)) return 0;
        string[] parts = json.Split(new string[] { "\"flrID\"" }, StringSplitOptions.None);
        var found = new List<FlareDef>();
        for (int i = 1; i < parts.Length; i++)
        {
            Match mc = ReClass.Match(parts[i]);
            Match md = ReDate.Match(parts[i]);
            if (!mc.Success || !md.Success) continue;
            char letter = mc.Groups[1].Value[0];
            float num;
            if (!float.TryParse(mc.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out num)) continue;
            if (letter != 'X' && !(letter == 'M' && num >= 5f)) continue;
            found.Add(new FlareDef { date = md.Groups[1].Value, letter = letter, num = num, live = true });
        }
        found.Sort(delegate (FlareDef a, FlareDef b) { return b.XValue.CompareTo(a.XValue); });
        for (int i = 0; i < found.Count && i < 5; i++) Live.Add(found[i]);
        return Live.Count;
    }

    public static string StatusLine()
    {
        switch (LiveState)
        {
            case 1: return "DONKI: consultando datos en vivo...";
            case 2:
                return Live.Count > 0
                    ? "DONKI: EN VIVO (" + Live.Count + " llamaradas fuertes de los últimos 120 días, más las guardadas)"
                    : "DONKI: EN VIVO (no hubo llamaradas fuertes últimamente; se usan las guardadas)";
            case 3: return "DONKI: sin internet, se usan las llamaradas reales guardadas";
        }
        return "DONKI: llamaradas reales guardadas";
    }

    // ------------------------------------------------------------------ textos
    static string SourcesKids()
    {
        var sb = new StringBuilder();
        sb.Append("<color=#ffc033>DATOS REALES DE LA NASA</color>\n\n");
        sb.Append("<color=#ffc033>TORMENTAS DEL SOL</color>\nSon llamaradas REALES guardadas en DONKI, una base de datos de la NASA.\n");
        sb.Append("<color=#7fc4ff>" + StatusLine() + ".</color>\n\n");
        sb.Append("<color=#ffc033>RADIACIÓN</color>\nEl rover Curiosity la midió en Marte: 100 veces más que en la Tierra. En la Luna, casi 200 veces más.\n\n");
        sb.Append("<color=#ffc033>MANDAR DATOS</color>\nLos datos de Marte pasan por satélites y llegan a 3 antenas gigantes de la NASA: en California, España y Australia.\n\n");
        sb.Append("<color=#ffc033>LA SEÑAL TARDA</color>\nDe Marte a la Tierra tarda de 3 a 22 minutos. De la Luna, 1 segundo.");
        return sb.ToString();
    }

    static string GlossaryKids()
    {
        var sb = new StringBuilder();
        sb.Append("<color=#ffc033>PALABRAS NUEVAS</color>\n\n");
        sb.Append("<color=#ffc033>LLAMARADA</color>: una explosión de luz en el Sol.\n\n");
        sb.Append("<color=#ffc033>RADIACIÓN</color>: energía invisible que daña el cuerpo.\n\n");
        sb.Append("<color=#ffc033>ESCUDO</color>: frena la radiación. Se gasta, ¡hay que reforzarlo!\n\n");
        sb.Append("<color=#ffc033>BUFFER</color>: la memoria donde se guardan los datos.\n\n");
        sb.Append("<color=#ffc033>ANTENA</color>: manda los datos a la Tierra.\n\n");
        sb.Append("<color=#7fbf8a>Lo REAL: las llamaradas, la radiación y el tiempo que tarda la señal.</color>");
        return sb.ToString();
    }

    public static string SourcesText()
    {
        if (Session.Diff.kids) return SourcesKids();
        var sb = new StringBuilder();
        sb.Append("<color=#ffc033>DE DÓNDE SALEN LOS DATOS</color>\nTodo esto es REAL: viene de la NASA y de otros científicos.\n\n");
        sb.Append("<color=#ffc033>1. TORMENTAS DEL SOL: DONKI (NASA)</color>\n");
        sb.Append("Las tormentas del juego son llamaradas REALES del Sol. DONKI es la base de datos de la NASA que las guarda. Cuanto más fuerte la llamarada, más peligrosa la tormenta.\n");
        sb.Append("<color=#7fc4ff>" + StatusLine() + ".</color>\n\n");
        sb.Append("<color=#ffc033>2. RADIACIÓN EN MARTE: Curiosity (NASA/JPL)</color>\n");
        sb.Append("El rover Curiosity midió 0,67 mSv al día. En la Tierra recibimos unas 100 veces menos.\n\n");
        sb.Append("<color=#ffc033>3. RADIACIÓN EN LA LUNA: Chang'e-4</color>\n");
        sb.Append("La nave Chang'e-4 midió 1,37 mSv al día, unas 2,6 veces más que dentro de la Estación Espacial (Science Advances, 2020). Es una misión de China, no de la NASA.\n\n");
        sb.Append("<color=#ffc033>4. MANDAR DATOS A LA TIERRA: Red del Espacio Profundo (NASA)</color>\n");
        sb.Append("Curiosity manda sus datos a satélites que giran sobre Marte, como MRO y Odyssey. Ellos los guardan y los pasan a la Tierra. Aquí los reciben 3 antenas gigantes: en California, España y Australia.\n\n");
        sb.Append("<color=#ffc033>5. SEÑAL Y POLVO (NASA/JPL)</color>\n");
        sb.Append("Una señal tarda de 3 a 22 minutos de Marte a la Tierra, y 1,3 segundos desde la Luna. ");
        sb.Append("En 2018 una tormenta de polvo tapó el Sol y el rover Opportunity dejó de hablar.\n\n");
        sb.Append("<color=#ffc033>6. SALUD EN EL ESPACIO: OSDR (NASA)</color>\n");
        sb.Append("osdr.nasa.gov guarda experimentos de salud y biología en el espacio. El juego NO usa sus números.");
        return sb.ToString();
    }

    public static string GlossaryText()
    {
        if (Session.Diff.kids) return GlossaryKids();
        var sb = new StringBuilder();
        sb.Append("<color=#ffc033>PALABRAS NUEVAS</color>\n\n");
        sb.Append("<color=#ffc033>LLAMARADA SOLAR</color>: una explosión de luz en el Sol. Tiene letras (A, B, C, M, X) y cada letra es 10 veces más fuerte.\n\n");
        sb.Append("<color=#ffc033>TORMENTA SOLAR</color>: partículas muy rápidas que lanza el Sol. Atraviesan paredes finas y dañan a las personas.\n\n");
        sb.Append("<color=#ffc033>RADIACIÓN</color>: energía invisible que daña el cuerpo. Se mide en mSv. En la Tierra: 0,007 al día. En Marte: 0,67. En la Luna: 1,37.\n\n");
        sb.Append("<color=#ffc033>ESCUDO</color>: capas de tierra, agua o metal que frenan la radiación. Se gasta, ¡hay que reforzarlo!\n\n");
        sb.Append("<color=#ffc033>BUFFER</color>: la memoria donde se guardan los datos hasta mandarlos a la Tierra.\n\n");
        sb.Append("<color=#ffc033>ANTENA</color>: manda los datos a la Tierra, pero solo si apunta hacia ella.\n\n");
        sb.Append("<color=#ffc033>RETRASO DE LA SEÑAL</color>: la señal viaja muy rápido, pero el espacio es enorme. Por eso la Tierra no puede manejar tu base: ¡tú decides!\n\n");
        sb.Append("<color=#ffc033>POLVO</color>: se pega a los paneles solares y les quita luz.\n\n");
        sb.Append("<color=#7fbf8a>LO QUE ES DEL JUEGO</color>: las noches pasan más rápido y los números de 0 a 100 son una simplificación. Lo REAL: las llamaradas, la radiación medida y el retraso de la señal.");
        return sb.ToString();
    }

    // Línea corta para la pantalla de misión: lo real del sitio elegido.
    public static string SiteLine(SiteDef s)
    {
        return s.name + ": radiación real " + s.dose.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') +
               " mSv/día (" + s.doseSource + "). Señal a la Tierra: " + s.delayReal + ".";
    }
}

// Un puntito que viaja una y otra vez de un lado a otro (para mostrar cómo viaja la señal).
public class PacketAnim : MonoBehaviour
{
    public RectTransform rt;
    public float x0, x1, y, speed = 0.35f, phase;

    void Update()
    {
        if (rt == null) return;
        float t = Mathf.Repeat(Time.unscaledTime * speed + phase, 1f);
        var a = new Vector2(Mathf.Lerp(x0, x1, t), y);
        rt.anchorMin = a; rt.anchorMax = a;
        rt.anchoredPosition = Vector2.zero;
    }
}

// Ventana "DATOS NASA": cuatro tarjetas con dibujos (qué es real en el juego) y palabras nuevas que se tocan para aprender.
public static class NasaPanel
{
    static readonly Color Card = new Color(0.07f, 0.09f, 0.10f, 1f);
    static readonly Color Soft = new Color(0.62f, 0.76f, 0.70f, 1f);

    static RectTransform Zone(Transform parent, float x0, float y0, float x1, float y1, Color c)
    {
        var rt = UIKit.Panel(parent, "Z", x0, y0, x1, y1, c);
        rt.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    // Tarjeta: icono grande + título arriba, fuente abajo. Devuelve la tarjeta para dibujar encima.
    static RectTransform MakeCard(Transform parent, int i, string icon, Color c, string title, string source)
    {
        float x0 = 0.015f + i * 0.2465f;
        var p = UIKit.Panel(parent, "NasaCard", x0, 0.43f, x0 + 0.2365f, 0.91f, Card);
        p.GetComponent<Image>().raycastTarget = false;
        UIKit.Border(p, new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 1f), 3f);
        var ic = Gfx.Icon(p, icon, 0.05f, 0.79f, 0.27f, 0.97f, c);
        var pu = ic.gameObject.AddComponent<Pulser>();
        pu.onColor = c; pu.amount = 0.07f; pu.speed = 2.5f;
        UIKit.Label(p, title, 15, TextAnchor.MiddleLeft, c, 0.30f, 0.78f, 0.98f, 0.98f);
        UIKit.Label(p, source, 10, TextAnchor.MiddleLeft, new Color(0.5f, 0.75f, 1f, 0.95f), 0.04f, 0f, 0.98f, 0.10f);
        return p;
    }

    public static void Fill(RectTransform parent, Action onClose)
    {
        var box = UIKit.Panel(parent, "NasaBox", 0.04f, 0.03f, 0.96f, 0.97f, new Color(0.02f, 0.03f, 0.05f, 0.99f));
        UIKit.Border(box, UIKit.RadioEdge, 3f);
        var dish = Gfx.Icon(box, "dish", 0.015f, 0.925f, 0.06f, 0.995f, UIKit.RadioEdge);
        var dp = dish.gameObject.AddComponent<Pulser>();
        dp.onColor = UIKit.RadioEdge; dp.amount = 0.06f; dp.speed = 2.5f;
        UIKit.Label(box, "DATOS REALES DE LA NASA", 26, TextAnchor.MiddleLeft, UIKit.Amber, 0.07f, 0.925f, 0.70f, 0.995f);

        // ---- 1) Tormentas del Sol: la escala de letras (cada letra es 10 veces más fuerte)
        Color sun = new Color(1f, 0.62f, 0.25f, 1f);
        var c1 = MakeCard(box, 0, "flame", sun, "TORMENTAS DEL SOL", "DONKI  |  NASA");
        bool live = NasaData.LiveState == 2;
        Gfx.Dot(c1, "circle", 0.07f, 0.725f, 12f, live ? Gfx.Good : Gfx.Mid);
        UIKit.Label(c1, live ? "EN VIVO" : "GUARDADAS", 12, TextAnchor.MiddleLeft, live ? Gfx.Good : Gfx.Mid, 0.12f, 0.67f, 0.98f, 0.78f);
        UIKit.Label(c1, "Cada letra es 10 veces más fuerte", 11, TextAnchor.MiddleLeft, Soft, 0.04f, 0.56f, 0.98f, 0.67f);
        string[] letters = { "A", "B", "C", "M", "X" };
        float[] heights = { 0.10f, 0.22f, 0.36f, 0.58f, 0.96f };
        Color[] hc = { Gfx.Good, new Color(0.7f, 0.95f, 0.4f, 1f), Gfx.Mid, new Color(1f, 0.55f, 0.2f, 1f), Gfx.Bad };
        for (int k = 0; k < 5; k++)
        {
            float bx = 0.06f + k * 0.18f;
            var bar = Zone(c1, bx, 0.22f, bx + 0.14f, 0.54f, new Color(0.10f, 0.13f, 0.12f, 1f));
            Zone(bar, 0f, 0f, 1f, heights[k], hc[k]);
            UIKit.Label(c1, letters[k], 16, TextAnchor.MiddleCenter, hc[k], bx - 0.02f, 0.11f, bx + 0.16f, 0.22f);
        }

        // ---- 2) Radiación: barras Tierra / Marte / Luna
        Color rad = new Color(1f, 0.85f, 0.25f, 1f);
        var c2 = MakeCard(box, 1, "star4", rad, "RADIACIÓN", "Curiosity (NASA)  |  Chang'e-4");
        float dm = NasaData.Sites[0].dose, dl = NasaData.Sites[1].dose;
        string[] rn = { "TIERRA", "MARTE", "LUNA" };
        float[] rv = { 0.007f, dm, dl };
        string[] rt = { "0,007", dm.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ','), dl.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') };
        Color[] rc = { Gfx.Good, new Color(1f, 0.55f, 0.25f, 1f), Gfx.Bad };
        for (int k = 0; k < 3; k++)
        {
            float y1 = 0.74f - k * 0.20f, y0 = y1 - 0.17f;
            UIKit.Label(c2, rn[k] + "\n<color=#ffffff>" + rt[k] + "</color>", 12, TextAnchor.MiddleLeft, rc[k], 0.03f, y0, 0.38f, y1);
            var tr = Zone(c2, 0.40f, y0 + 0.03f, 0.97f, y1 - 0.03f, new Color(0.10f, 0.13f, 0.12f, 1f));
            Zone(tr, 0f, 0f, Mathf.Clamp(rv[k] / dl, 0.02f, 1f), 1f, rc[k]);
        }
        UIKit.Label(c2, "mSv por día (dosis)", 11, TextAnchor.MiddleLeft, Soft, 0.04f, 0.10f, 0.98f, 0.20f);

        // ---- 3) La señal: tarda y viaja por satélites hasta 3 antenas gigantes
        Color sig = new Color(0.40f, 0.75f, 1f, 1f);
        var c3 = MakeCard(box, 2, "dish", sig, "LA SEÑAL TARDA", "Red del Espacio Profundo  |  NASA");
        UIKit.Label(c3, "MARTE\n<color=#ffffff>3 a 22 min</color>", 13, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.25f, 1f), 0.04f, 0.58f, 0.50f, 0.77f);
        UIKit.Label(c3, "LUNA\n<color=#ffffff>1,3 seg</color>", 13, TextAnchor.MiddleLeft, new Color(0.8f, 0.82f, 0.86f, 1f), 0.52f, 0.58f, 0.98f, 0.77f);
        Zone(c3, 0.12f, 0.355f, 0.86f, 0.375f, new Color(0.2f, 0.3f, 0.4f, 1f));
        Gfx.Dot(c3, "circle", 0.12f, 0.365f, 30f, new Color(0.86f, 0.40f, 0.22f, 1f));
        Gfx.Dot(c3, "panel", 0.50f, 0.365f, 34f, sig);
        Gfx.Dot(c3, "dish", 0.86f, 0.365f, 36f, sig);
        UIKit.Label(c3, "MARTE", 10, TextAnchor.UpperCenter, Soft, 0.0f, 0.14f, 0.30f, 0.28f);
        UIKit.Label(c3, "SATÉLITE", 10, TextAnchor.UpperCenter, Soft, 0.34f, 0.14f, 0.66f, 0.28f);
        UIKit.Label(c3, "3 ANTENAS", 10, TextAnchor.UpperCenter, Soft, 0.68f, 0.14f, 1.0f, 0.28f);
        for (int k = 0; k < 3; k++)
        {
            var g = new GameObject("Packet", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(c3, false);
            var im = g.GetComponent<Image>();
            im.sprite = Art.Body("star4"); im.color = Color.white; im.raycastTarget = false;
            var prt = (RectTransform)g.transform;
            prt.sizeDelta = new Vector2(14f, 14f);
            var an = g.AddComponent<PacketAnim>();
            an.rt = prt; an.x0 = 0.12f; an.x1 = 0.86f; an.y = 0.365f; an.speed = 0.3f; an.phase = k / 3f;
        }

        // ---- 4) Polvo: el año en que una tormenta tapó todo Marte
        Color dust = new Color(0.85f, 0.62f, 0.35f, 1f);
        var c4 = MakeCard(box, 3, "dust", dust, "TORMENTA DE POLVO", "NASA/JPL");
        UIKit.Label(c4, "2018", 44, TextAnchor.MiddleCenter, Color.white, 0.05f, 0.50f, 0.95f, 0.76f);
        UIKit.Label(c4, "Una tormenta cubrió todo Marte. El rover Opportunity dejó de hablar.", 13, TextAnchor.UpperLeft, UIKit.Phosphor, 0.05f, 0.14f, 0.95f, 0.50f);
        for (int k = 0; k < 9; k++)
            Gfx.Dot(c4, "circle", 0.12f + (k * 37 % 80) / 100f, 0.46f + (k * 53 % 8) / 100f, 6f + (k % 3) * 3f, new Color(dust.r, dust.g, dust.b, 0.5f));

        // ---- palabras nuevas: toca una para ver qué significa
        UIKit.Label(box, "PALABRAS NUEVAS  (toca una)", 14, TextAnchor.MiddleLeft, UIKit.Amber, 0.015f, 0.375f, 0.7f, 0.425f);
        string[] wn = { "LLAMARADA", "RADIACIÓN", "ESCUDO", "BUFFER", "ANTENA", "OSDR" };
        string[] wi = { "flame", "star4", "shield", "flask", "dish", "astro" };
        Color[] wc = { sun, rad, new Color(0.40f, 0.75f, 1f, 1f), new Color(0.75f, 0.55f, 1f, 1f), sig, new Color(0.75f, 0.9f, 1f, 1f) };
        string[] wd =
        {
            "Una explosión de luz en el Sol. Tiene letras: A, B, C, M y X.",
            "Energía invisible que daña el cuerpo. Se mide en mSv.",
            "Capas de tierra, agua o metal que frenan la radiación. ¡Se gasta!",
            "La memoria donde se guardan los datos hasta mandarlos a la Tierra.",
            "Manda los datos a la Tierra. Hay 3 antenas gigantes de la NASA.",
            "Base de datos de la NASA con experimentos de salud en el espacio. Este juego todavía no usa sus números."
        };
        var def = UIKit.Panel(box, "Def", 0.015f, 0.16f, 0.985f, 0.255f, Card);
        def.GetComponent<Image>().raycastTarget = false;
        UIKit.Border(def, new Color(0.22f, 0.30f, 0.25f, 1f), 2f);
        var defText = UIKit.Label(def, "Toca una palabra para ver qué significa.", 17, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.02f, 0f, 0.98f, 1f);
        var chipBg = new Image[wn.Length];
        var chipBd = new GameObject[wn.Length];
        for (int k = 0; k < wn.Length; k++)
        {
            int kk = k;
            float x0 = 0.015f + k * 0.1625f;
            var b = UIKit.MakeButton(box, "", 12, x0, 0.265f, x0 + 0.1545f, 0.37f, UIKit.Dark, delegate
            {
                for (int j = 0; j < chipBg.Length; j++) { chipBg[j].color = j == kk ? UIKit.Selected : UIKit.Dark; chipBd[j].SetActive(j == kk); }
                defText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(wc[kk]) + ">" + wn[kk] + ":</color>  " + wd[kk];
            }, TextAnchor.MiddleCenter, Sfx.DesignSelect);
            var brt = (RectTransform)b.transform;
            chipBg[k] = brt.GetComponent<Image>();
            chipBd[k] = UIKit.Border(brt, wc[k], 3f);
            chipBd[k].SetActive(false);
            Gfx.IconLeft(brt, wi[k], 30f, 8f, wc[k]);
            UIKit.Label(brt, wn[k], 13, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.28f, 0f, 1f, 1f);
        }

        // ---- real o del juego
        var real = Zone(box, 0.015f, 0.095f, 0.495f, 0.15f, new Color(0.05f, 0.18f, 0.08f, 1f));
        UIKit.Border(real, Gfx.Good, 2f);
        Gfx.IconLeft(real, "face_happy", 28f, 8f, Gfx.Good);
        UIKit.Label(real, "<color=#73ff8c>REAL:</color> llamaradas, radiación y tiempo de la señal", 12, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.09f, 0f, 1f, 1f);
        var game = Zone(box, 0.505f, 0.095f, 0.985f, 0.15f, new Color(0.20f, 0.14f, 0.04f, 1f));
        UIKit.Border(game, Gfx.Mid, 2f);
        Gfx.IconLeft(game, "face_mid", 28f, 8f, Gfx.Mid);
        UIKit.Label(game, "<color=#ffc033>DEL JUEGO:</color> noches rápidas y números de 0 a 100", 12, TextAnchor.MiddleLeft, UIKit.Phosphor, 0.09f, 0f, 1f, 1f);

        var close = UIKit.MakeButton(box, "CERRAR", 20, 0.35f, 0.012f, 0.65f, 0.085f, UIKit.Selected,
                                     delegate { if (onClose != null) onClose(); }, TextAnchor.MiddleCenter, Sfx.ManualClose);
        UIKit.Border((RectTransform)close.transform, UIKit.Phosphor, 3f);
        GameAudio.Play(Sfx.ManualOpen);
    }
}