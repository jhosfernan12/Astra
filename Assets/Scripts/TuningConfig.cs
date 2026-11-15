using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// =====================================================================================================
//  TODOS LOS NÚMEROS DEL JUEGO, EDITABLES EN EL INSPECTOR.
//
//  Cómo se usa:
//   1. Dale Play una vez: se crea solo el archivo  Assets/Resources/TuningConfig.asset
//      (o clic derecho en Project > Create > ARES-1 > Tuning).
//   2. Selecciónalo en la ventana Project y cambia los números en el Inspector (cada uno tiene su explicación al pasar el mouse).
//   3. Dale Play: el juego usa esos números. Para volver a los del código: clic derecho en el título del Inspector > "Restaurar".
//
//  Si apagas "Usar Estos Valores" el juego usa los números que están escritos en Tuning.cs.
// =====================================================================================================
[CreateAssetMenu(fileName = "TuningConfig", menuName = "ARES-1/Tuning (números del juego)")]
public class TuningConfig : ScriptableObject
{
    // Súbelo cuando cambien los valores por defecto del código: así el archivo viejo se actualiza solo.
    public const int CodeVersion = 10;

    [Header("CONTROL")]
    [Tooltip("Apagado: el juego usa los números de Tuning.cs y no estos.")]
    public bool usarEstosValores = true;
    [Tooltip("No lo cambies: sirve para actualizar este archivo cuando el código trae números nuevos.")]
    public int versionDelCodigo = CodeVersion;

    [Serializable]
    public class DifficultyValues
    {
        public string name = "";
        [Tooltip("duración de la misión")]
        public int days;
        [Tooltip("multiplicador de la fuerza de los eventos")]
        public float severity;
        [Tooltip("tormentas solares")]
        public int storms;
        [Tooltip("fallas técnicas")]
        public int fails;
        [Tooltip("días que duran las tormentas de polvo y las noches largas")]
        public int longDur;
        [Tooltip("fugas en el módulo")]
        public int leaks;
        [Tooltip("cápsulas de suministros (buenas noticias)")]
        public int supplies;
        [Tooltip("la Ing. Freeman da consejos durante la misión")]
        public bool hints;
        [Tooltip("edades recomendadas")]
        public string ages = "";
        [Tooltip("1 = normal; menos de 1 = la base gasta y se desgasta más despacio cada noche")]
        public float ease = 1f;
        [Tooltip("tareas extra cada noche")]
        public int bonusTasks;
        [Tooltip("modo para los más pequeños: menos reglas, menos texto, la antena apunta sola")]
        public bool kids;
        [Tooltip("ÁNIMO del equipo: las tareas cansan y con poco ánimo hay menos tareas")]
        public bool morale;
        [Tooltip("CASA: se desgasta, REPARAR, cosas que se rompen")]
        public bool house;
        [Tooltip("PANELES: se ensucian, LIMPIAR PANELES, tormentas de polvo")]
        public bool panels;
        [Tooltip("CALEFACCIÓN y la oscuridad/noche larga")]
        public bool heater;
        [Tooltip("hay que APUNTAR la antena a CIENCIA o a la TIERRA")]
        public bool aim;
        [Tooltip("LLAMAR A LA TIERRA (la respuesta tarda noches) y barra de DESCARGA")]
        public bool call;
        [Tooltip("rocas del espacio y fugas de aire")]
        public bool hazards;
        [Tooltip("la tormenta del Sol borra datos y el polvo tapa la señal")]
        public bool dataLoss;
        [Tooltip("cuánto gasta de batería cada tarea (1 = base; más = la batería baja mucho al trabajar)")]
        public float energyMult = 1f;
        [Tooltip("1 = los paneles dan toda la luz; menos = dan menos batería")]
        public float solarScale = 1f;
        [Tooltip("cápsulas de EMERGENCIA: te salvan una vez si algo llega a 0")]
        public int lifelines;
        [Tooltip("probabilidad de un imprevisto (bueno o malo) cada noche")]
        public float surprise;
        [Tooltip("de los imprevistos, qué parte son buenos")]
        public float luck = 0.5f;
        [Tooltip("META DE CIENCIA: paquetes de datos que hay que MANDAR a la Tierra para cumplir la misión")]
        public int goalPacks = 3;
        [Tooltip("fuerza de lo que le quita salud/ánimo POR SEGUNDO (1 = completo; 0.4 = suave para los más pequeños)")]
        public float liveDrain = 1f;
        [Tooltip("cuánta batería gastan el puesto y las máquinas encendidas (más = la batería se vacía rapidísimo)")]
        public float drainMult = 1f;
        [Tooltip("cuánta comida gasta cada tarea (más = más hambre)")]
        public float foodMult = 1f;
        [Tooltip("probabilidad de un imprevisto EN VIVO cada cierto tiempo mientras juegas")]
        public float liveEvents = 0.2f;
        [Tooltip("probabilidad de un SEGUNDO imprevisto cada noche")]
        public float extraSurprise = 0f;
    }

    [Header("DIFICULTADES (0 = EXPLORADOR, 1 = ASTRONAUTA, 2 = COMANDANTE)")]
    public DifficultyValues[] dificultades = new DifficultyValues[0];

    [Header("ÁNIMO: CON BUEN ÁNIMO HAY MÁS TAREAS Y CADA UNA RINDE MÁS; CON MAL ÁNIMO, MENOS TAREAS Y RINDEN MENOS")]
    [Tooltip("tareas por noche con ánimo normal")]
    public int TasksBase = 4;
    [Tooltip("ánimo >= esto: ANIMADO  (+1 tarea, rinde x1.25, experimentos dobles)")]
    public float MoodHigh = 70f;
    [Tooltip("ánimo >= esto: BIEN     (normal)")]
    public float MoodOk = 40f;
    [Tooltip("ánimo >= esto: CANSADO  (-1 tarea, rinde x0.75); menos: DESANIMADO (-2 tareas, rinde x0.5)")]
    public float MoodLow = 20f;
    public float MoodHighMult = 1.25f;
    public float MoodOkMult = 1f;
    public float MoodTiredMult = 0.75f;
    public float MoodLowMult = 0.5f;
    [Tooltip("extra mientras dura una BUENA NOTICIA")]
    public float MoodBoostMult = 1.2f;
    [Tooltip("noches que dura ese extra")]
    public int MoodBoostNights = 2;
    [Tooltip("con el equipo ANIMADO: probabilidad de que un experimento salga doble")]
    public float DoubleExperiment = 0.35f;

    [Header("ENERGÍA (CADA DÍA)")]
    [Tooltip("lo que generan los paneles limpios, con sol normal")]
    public float SolarMax = 145f;
    [Tooltip("1 = la carga sube en línea recta con el % de paneles; más de 1 = con paneles casi al 100 % carga MÁS rápido")]
    public float PanelCurve = 1.3f;
    [Tooltip("batería que se ahorra/recupera al DESCANSAR (el equipo apaga cosas)")]
    public float RestEnergy = 3f;
    [Tooltip("batería que gasta CADA máquina encendida (invernadero, aire, calefacción) POR SEGUNDO. Si la batería llega a 0 se acaba la misión (apagón).")]
    public float MachineDrainPerSec = 3f;
    [Tooltip("consumo fijo del puesto")]
    public float BaseDrain = 9f;
    [Tooltip("fracción de sol que llega en tormenta de polvo")]
    public float DustSolar = 0.35f;
    [Tooltip("fracción de sol en noche larga")]
    public float LongNightSolar = 0.2f;

    [Header("OXÍGENO Y COMIDA (CADA DÍA)")]
    [Tooltip("el aire baja SIEMPRE esto por segundo (el equipo respira)")]
    public float AirDrainPerSec = 1f;
    [Tooltip("aire que sube POR SEGUNDO con la máquina de aire encendida")]
    public float RecyclerAirPerSec = 4f;
    public float FoodBase = 8f;
    [Tooltip("comida que sube el invernadero encendido POR SEGUNDO")]
    public float GreenhouseFoodPerSec = 4f;

    [Header("DESGASTE DIARIO")]
    [Tooltip("estructura")]
    public float Wear = 2f;
    [Tooltip("blindaje")]
    public float ShieldDecay = 3f;
    [Tooltip("polvo que se pega a los paneles cada noche (baja poco a poco durante el día)")]
    public float PanelDecay = 5f;

    [Header("FLUJOS EN VIVO: LA BATERÍA SUBE CON LOS PANELES Y BAJA CON LAS MÁQUINAS MIENTRAS JUEGAS")]
    [Tooltip("qué parte de lo de la noche se aplica en vivo (el resto, al terminar la noche)")]
    public float LiveShare = 0.65f;
    [Tooltip("el polvo de los paneles cae TODO en vivo (se nota el % bajando)")]
    public float LiveShareDust = 1f;
    [Tooltip("segundos que tarda en aplicarse esa parte")]
    public float LiveSeconds = 30f;
    [Tooltip("ánimo que baja cada noche por CADA cosa que falla")]
    public float MoodPerTrouble = 5f;
    [Tooltip("máximo de ánimo que se pierde por noche por fallas")]
    public float MoodTroubleCap = 20f;
    [Tooltip("radiación diaria sin blindaje (se reduce con el blindaje)")]
    public float DailyDose = 5f;
    [Tooltip("salud que se recupera si hay aire y comida")]
    public float HealthRegen = 2f;

    [Header("TAREAS (CUÁNTO CAMBIAN Y CUÁNTO CANSAN)")]
    public float ReinforceAmount = 30f;
    public float RepairAmount = 25f;
    public float CleanAmount = 60f;
    public float RestMorale = 25f;
    public float RestHealth = 8f;
    [Tooltip("ánimo que cuesta cada tarea")]
    public float FatigueReinforce = 3f;
    public float FatigueRepair = 3f;
    public float FatigueClean = 2f;
    public float FatigueExperiment = 4f;
    public float EnReinforce = 6f;
    public float EnRepair = 6f;
    public float EnClean = 4f;
    public float EnExperiment = 6f;
    public float EnUpload = 6f;
    public float EnAim = 2f;
    public float EnCall = 5f;
    public float FoodReinforce = 6f;
    public float FoodRepair = 7f;
    public float FoodClean = 6f;
    public float FoodExperiment = 10f;
    public float FoodRest = 6.5f;
    public float FoodAim = 3f;
    public float FoodUpload = 4f;
    public float FoodCall = 3f;
    [Tooltip("salud que cuesta cada experimento (trabajo pesado)")]
    public float HealthExperiment = 4f;
    [Tooltip("batería que debe sobrar para poder hacer una tarea")]
    public float EnMargin = 4f;

    [Header("ANTENA: APUNTAR Y SUBIR EL BUFFER (COMO EN QUIPU)")]
    [Tooltip("MB de datos que caben a bordo")]
    public float BufferMax = 100f;
    [Tooltip("MB que guarda cada experimento")]
    public float ExperimentData = 25f;
    [Tooltip("MB que se suben con cada tarea SUBIR BUFFER")]
    public float UploadRate = 50f;
    [Tooltip("segundos que tarda en girar la antena (suena el motor)")]
    public float AimSeconds = 2.2f;
    public float AimFatigue = 1f;
    public float UploadFatigue = 2f;
    [Tooltip("fracción del buffer que borra una tormenta solar")]
    public float StormBufferLoss = 0.5f;

    [Header("LLAMAR A LA TIERRA (LA RESPUESTA LLEGA CON RETRASO)")]
    [Tooltip("moral que cuesta")]
    public float CallFatigue = 2f;
    [Tooltip("estructura extra si la Tierra te guía la reparación")]
    public float RepairGuideBonus = 10f;
    [Tooltip("moral que da el mensaje de la familia")]
    public float FamilyMorale = 20f;
    [Tooltip("la respuesta trae una BUENA noticia (25 %)")]
    public float NewsGoodChance = 0.25f;
    [Tooltip("la respuesta trae una MALA noticia (25 %); el resto (50 %) es normal")]
    public float NewsBadChance = 0.25f;
    [Tooltip("ánimo extra con una buena noticia (con la familia: se duplica el mensaje)")]
    public float NewsGoodMood = 20f;
    [Tooltip("ánimo que se pierde con una mala noticia")]
    public float NewsBadMood = 15f;
    [Tooltip("días de pronóstico cuando la Tierra te ayuda")]
    public int ForecastLong = 5;
    [Tooltip("días de pronóstico normales")]
    public int ForecastShort = 2;

    [Header("EVENTOS")]
    [Tooltip("dosis de radiación de una tormenta solar sin blindaje")]
    public float StormDose = 40f;
    [Tooltip("cuánto reduce el blindaje al 100 %")]
    public float StormShieldEffect = 0.8f;
    public float MeteorDamage = 15f;
    public float LeakOxygen = 20f;
    public float LeakIntegrity = 5f;
    [Tooltip("estructura que se pierde si no hay calefacción")]
    public float LongNightDamage = 12f;
    public float LongNightMorale = 8f;
    [Tooltip("si las máquinas encendidas gastan más que esto POR SEGUNDO sale \"¡CONSUMO ALTO!\" en rojo")]
    public float HighDrainAt = 8f;
    [Tooltip("cada cuántos segundos jugando puede caer un imprevisto EN VIVO (con la probabilidad 'liveEvents' de la dificultad)")]
    public float LiveEventSeconds = 25f;
    [Tooltip("noches que dura una tormenta de polvo (1 = solo una noche, no continúa)")]
    public int DustNights = 1;
    [Tooltip("TORMENTA DE POLVO: los paneles pierden esto (%) por segundo")]
    public float DustPanelsPerSec = 3.5f;
    [Tooltip("la tormenta de polvo raspa la casa: daño por segundo (menos con el escudo arriba)")]
    public float DustHousePerSec = 0.3f;
    [Tooltip("durante una TORMENTA SOLAR la casa sufre este daño por segundo")]
    public float StormHousePerSec = 0.4f;
    [Tooltip("y un golpe de daño a la casa cuando llega la tormenta solar")]
    public float StormHouseDamage = 8f;
    [Tooltip("casa que pierde un SISMO (imprevisto)")]
    public float QuakeDamage = 18f;
    [Tooltip("ánimo que quita un sismo")]
    public float QuakeMood = 6f;
    [Tooltip("casa que pierde una GRIETA por cambio de temperatura (imprevisto)")]
    public float CrackDamage = 12f;
    [Tooltip("casa que pierde un GOLPE DE ARENA (imprevisto)")]
    public float HitDamage = 14f;

    [Header("ESCUDO Y CASA: SIN ESCUDO LA CASA SUFRE MUCHO MÁS")]
    [Tooltip("blindaje <= esto: el escudo está ROTO")]
    public float ShieldBrokenBelow = 10f;
    [Tooltip("con el escudo roto, TODO el daño a la casa se multiplica por esto (crítico)")]
    public float ShieldBrokenHouseMult = 2f;
    [Tooltip("con el escudo al 100 % el daño a la casa baja esta fracción (nunca llega a 0)")]
    public float ShieldHouseReduction = 0.3f;
    [Tooltip("casa <= esto: la casa está ROTA")]
    public float HouseBrokenBelow = 25f;
    [Tooltip("salud que se pierde POR SEGUNDO con la casa rota")]
    public float HouseBrokenHealthPerSec = 0.6f;
    [Tooltip("ánimo que se pierde POR SEGUNDO con la casa rota")]
    public float HouseBrokenMoodPerSec = 0.7f;
    [Tooltip("salud extra que se pierde cada noche con la casa rota")]
    public float HouseBrokenNightHealth = 8f;
    [Tooltip("ánimo extra que se pierde cada noche con la casa rota")]
    public float HouseBrokenNightMood = 8f;

    [Header("SALUD Y ÁNIMO POR SEGUNDO (PASAN MIENTRAS JUEGAS; SE MULTIPLICAN POR 'LIVEDRAIN' DE LA DIFICULTAD)")]
    [Tooltip("ánimo BAJO (menos de MoodOk): salud que se pierde por segundo")]
    public float MoodTiredHealthPerSec = 1f;
    [Tooltip("ánimo MUY BAJO (menos de MoodLow): salud que se pierde por segundo")]
    public float MoodLowHealthPerSec = 1f;
    [Tooltip("SIN AIRE (0 %): salud que se pierde por segundo")]
    public float NoAirHealthPerSec = 4f;
    [Tooltip("SIN COMIDA (0 %): salud que se pierde por segundo")]
    public float StarveHealthPerSec = 1.5f;

    [Header("FRÍO Y CALOR (TEMPERATURA DEL EQUIPO). CON LA NOCHE LARGA SIN CALEFACCIÓN SE CONGELAN (COPO DE NIEVE JUNTO AL ASTRONAUTA);")]
    [Tooltip("batería extra que gasta el frío en un día")]
    public float ColdBatteryDrain = 12f;
    [Tooltip("con la calefacción funcionando, el frío pega solo esta fracción de batería")]
    public float ColdHeaterFactor = 0.35f;
    [Tooltip("temperatura normal del equipo (grados)")]
    public float TempNormalC = 20f;
    [Tooltip("cuántos grados es cada punto de temperatura interna")]
    public float TempCPerUnit = 0.5f;
    [Tooltip("la TORMENTA SOLAR sube la temperatura por segundo, aunque la calefacción esté apagada")]
    public float TempStormPerSec = 0.35f;
    [Tooltip("cuánto se enfría el equipo por segundo (noche larga sin calefacción)")]
    public float TempChillPerSec = 0.8f;
    [Tooltip("extra por segundo si además la CALEFACCIÓN está encendida durante la tormenta solar")]
    public float TempHeatPerSec = 0.7f;
    [Tooltip("cuánto vuelve a la normalidad por segundo")]
    public float TempRelaxPerSec = 1.0f;
    [Tooltip("a partir de aquí aparece el copo de nieve o la llama junto al astronauta")]
    public float TempIconAt = 25f;
    [Tooltip("a partir de aquí el equipo pierde salud y ánimo")]
    public float TempDamageAt = 50f;
    [Tooltip("salud que se pierde por segundo con frío o calor fuerte")]
    public float TempHealthPerSec = 0.5f;
    [Tooltip("ánimo que se pierde por segundo con frío o calor fuerte")]
    public float TempMoodPerSec = 0.5f;
    [Tooltip("segundos que cuenta cada noche que pasa para la temperatura")]
    public float TempNightSecs = 12f;
    [Tooltip("la tormenta solar sigue calentando esta cantidad de días después")]
    public int StormHeatLingerDays = 1;
    [Tooltip("salud que se pierde en la noche larga sin calefacción")]
    public float ColdNightHealth = 4f;

    [Header("RECOMPENSA POR JUGAR BIEN Y CÁPSULA DE EMERGENCIA")]
    [Tooltip("para ganar el BONO DE LA NOCHE puedes dejar sin usar como máximo estas tareas")]
    public int BonusMaxWasted = 1;
    [Tooltip("1 = el BONO solo se gana si esa noche MANDASTE datos a la Tierra (0 = siempre)")]
    public int BonusNeedsUpload = 1;
    public float BonusEnergy = 15f;
    public float BonusAir = 15f;
    public float BonusFood = 15f;
    public float BonusHealth = 10f;
    public float BonusHouse = 12f;
    public float BonusMood = 15f;
    [Tooltip("si todo está por encima de esto: NOCHE PERFECTA (+1 tarea mañana)")]
    public float BonusPerfect = 75f;
    [Tooltip("lo que repone una cápsula de emergencia")]
    public float RescueAmount = 40f;
    [Tooltip("1 = la cápsula también salva A MITAD DE LA NOCHE (si la batería o la salud llegan a 0 y quedan cápsulas); 0 = solo al terminar la noche")]
    public int LiveRescue = 1;

    [Header("PUNTAJE")]
    public float PtsPerDay = 25f;
    [Tooltip("cada experimento SUBIDO a la Tierra (cuesta más tareas que antes)")]
    public float PtsPerExperiment = 70f;
    public float PtsCompletion = 100f;
    [Tooltip("x promedio del estado final (0-100)")]
    public float PtsState = 2f;
    [Tooltip("para calcular la nota")]
    public float PtsRefPerDay = 75f;
    public float GradeS = 0.85f;
    public float GradeA = 0.65f;
    public float GradeB = 0.48f;
    public float GradeC = 0.30f;

    // ------------------------------------------------------------------------------------------------
    static readonly BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;
    static readonly BindingFlags PubStatic = BindingFlags.Public | BindingFlags.Static;

    // Pasa los números del Inspector al juego.
    public void ApplyToTuning()
    {
        if (!usarEstosValores) return;
        foreach (var f in typeof(TuningConfig).GetFields(Pub))
        {
            var t = typeof(Tuning).GetField(f.Name, PubStatic);
            if (t != null && !t.IsInitOnly && t.FieldType == f.FieldType) t.SetValue(null, f.GetValue(this));
        }
        int n = Mathf.Min(dificultades.Length, Tuning.Difficulties.Length);
        for (int i = 0; i < n; i++) Copy(dificultades[i], Tuning.Difficulties[i]);
    }

    // Pasa los números del código al Inspector (para crear el archivo o restaurarlo).
    [ContextMenu("Restaurar con los números del código (Tuning.cs)")]
    public void PullFromTuning()
    {
        foreach (var f in typeof(TuningConfig).GetFields(Pub))
        {
            var t = typeof(Tuning).GetField(f.Name, PubStatic);
            if (t != null && t.FieldType == f.FieldType) f.SetValue(this, t.GetValue(null));
        }
        dificultades = new DifficultyValues[Tuning.Difficulties.Length];
        for (int i = 0; i < dificultades.Length; i++)
        {
            dificultades[i] = new DifficultyValues();
            Copy(Tuning.Difficulties[i], dificultades[i]);
        }
        versionDelCodigo = CodeVersion;
        usarEstosValores = true;
    }

    static void Copy(object from, object to)
    {
        foreach (var f in from.GetType().GetFields(Pub))
        {
            var t = to.GetType().GetField(f.Name, Pub);
            if (t != null && t.FieldType == f.FieldType) t.SetValue(to, f.GetValue(from));
        }
    }

    // Al arrancar el juego: busca el archivo y aplica sus números.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var cfg = Resources.Load<TuningConfig>("TuningConfig");
#if UNITY_EDITOR
        if (cfg == null) cfg = CreateAssetFile();
        else if (cfg.versionDelCodigo < CodeVersion)
        {
            Debug.Log("[TuningConfig] El código trae números nuevos: se actualizó Assets/Resources/TuningConfig.asset con los valores del código.");
            cfg.PullFromTuning();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
        }
#endif
        if (cfg != null) cfg.ApplyToTuning();
    }

#if UNITY_EDITOR
    static TuningConfig CreateAssetFile()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var c = CreateInstance<TuningConfig>();
        c.PullFromTuning();
        AssetDatabase.CreateAsset(c, "Assets/Resources/TuningConfig.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("[TuningConfig] Creado Assets/Resources/TuningConfig.asset: ahí puedes cambiar todos los números del juego desde el Inspector.");
        return c;
    }
#endif
}