using System;

// Modo de juego, dificultad y suministros elegidos para la misión.
public static class Session
{
    public static bool tutorial = true;                 // true = TUTORIAL (los 4 primeros días guiados por la Ing. Freeman), false = DIRECTO
    public static int difficulty = 0;                   // 0 = EXPLORADOR (la más fácil), 1 = ASTRONAUTA, 2 = COMANDANTE
    public static int pack = 0;                         // paquete de suministros iniciales (ver Packs)
    public static int site = 0;                         // 0 = MARTE, 1 = LUNA (ver NasaData.Sites)

    public static SiteDef Site { get { return NasaData.Sites[Math.Min(Math.Max(site, 0), NasaData.Sites.Length - 1)]; } }

    public static DifficultyDef Diff { get { return Tuning.Difficulties[difficulty]; } }
    public static string DifficultyName { get { return Diff.name; } }
    public static string DifficultyLabel { get { return Diff.name + " (" + Diff.ages + ")"; } }
    // ¿Este recurso existe en el nivel elegido? (0 batería, 1 aire, 2 comida, 3 casa, 4 escudo, 5 salud, 6 ánimo, 7 paneles)
    public static bool ResShown(int i)
    {
        if (i == 6) return Diff.morale;
        if (i == 3) return Diff.house;
        if (i == 7) return Diff.panels;
        return true;
    }
    public static bool Kids { get { return Diff.kids; } }          // EXPLORADOR: menos texto y reglas más simples
}

// Un paso del tutorial: o es un mensaje con pausa (botón ENTENDIDO) o un objetivo que se cumple jugando.
public class TutorialStep
{
    public string text;
    public Func<string> textFn;   // si no es null, el texto se calcula al empezar el paso
    public bool pause = true;     // true = mensaje en pantalla (hay que pulsar ENTENDIDO)
    public Func<bool> done;       // condición para avanzar (solo pasos sin pausa)
    public Action onEnter;        // desbloquear botones, resaltar cosas...

    public string Text() { return textFn != null ? textFn() : text; }

    public static TutorialStep Say(string text, Action enter)
    {
        return new TutorialStep { text = text, pause = true, onEnter = enter };
    }

    public static TutorialStep SayFn(Func<string> fn, Action enter)
    {
        return new TutorialStep { textFn = fn, pause = true, onEnter = enter };
    }

    public static TutorialStep Wait(string text, Func<bool> done, Action enter)
    {
        return new TutorialStep { text = text, pause = false, done = done, onEnter = enter };
    }
}