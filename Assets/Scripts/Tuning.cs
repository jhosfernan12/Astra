using UnityEngine;

// Una dificultad = cuántas noches dura la misión y cuántos eventos aleatorios te caen encima.
public class DifficultyDef
{
    public string name;
    public int days;          // duración de la misión
    public float severity;    // multiplicador de la fuerza de los eventos
    public int storms;        // tormentas solares
    public int fails;         // fallas técnicas
    public int longDur;       // días que duran las tormentas de polvo y las noches largas
    public int leaks;         // fugas en el módulo
    public int supplies;      // cápsulas de suministros (buenas noticias)
    public bool hints;        // la Ing. Freeman da consejos durante la misión
    public string ages = "";  // edades recomendadas
    public float ease = 1f;   // 1 = normal; menos de 1 = la base gasta y se desgasta más despacio cada noche
    public int bonusTasks;    // tareas extra cada noche
    public bool kids;         // modo para los más pequeños: menos reglas, menos texto, la antena apunta sola
    // ---- qué cosas existen en este nivel (EXPLORADOR tiene pocas; COMANDANTE las tiene todas)
    public bool morale;       // ÁNIMO del equipo: las tareas cansan y con poco ánimo hay menos tareas
    public bool house;        // CASA: se desgasta, REPARAR, cosas que se rompen
    public bool panels;       // PANELES: se ensucian, LIMPIAR PANELES, tormentas de polvo
    public bool heater;       // CALEFACCIÓN y la oscuridad/noche larga
    public bool aim;          // hay que APUNTAR la antena a CIENCIA o a la TIERRA
    public bool call;         // LLAMAR A LA TIERRA (la respuesta tarda noches) y barra de DESCARGA
    public bool hazards;      // rocas del espacio y fugas de aire
    public bool dataLoss;     // la tormenta del Sol borra datos y el polvo tapa la señal
    // ---- dureza y suerte
    public float energyMult = 1f;   // cuánto gasta de batería cada tarea (1 = base; más = la batería baja mucho al trabajar)
    public float solarScale = 1f;   // 1 = los paneles dan toda la luz; menos = dan menos batería
    public int lifelines;           // cápsulas de EMERGENCIA: te salvan una vez si algo llega a 0
    public float surprise;          // probabilidad de un imprevisto (bueno o malo) cada noche
    public float luck = 0.5f;       // de los imprevistos, qué parte son buenos
    public int goalPacks = 3;       // META DE CIENCIA: paquetes de datos que hay que MANDAR a la Tierra para cumplir la misión
    public float liveDrain = 1f;    // fuerza de lo que le quita salud/ánimo POR SEGUNDO (1 = completo; 0.4 = suave para los más pequeños)
    public float drainMult = 1f;    // cuánta batería gastan el puesto y las máquinas encendidas (más = la batería se vacía rapidísimo)
    public float foodMult = 1f;     // cuánta comida gasta cada tarea (más = más hambre)
    public float liveEvents = 0.2f; // probabilidad de un imprevisto EN VIVO cada cierto tiempo mientras juegas
    public float extraSurprise = 0f;// probabilidad de un SEGUNDO imprevisto cada noche
}

// TODOS LOS NÚMEROS DEL JUEGO ESTÁN AQUÍ. Todo se mide en "puntos" de 0 a 100 (energía = % de batería).
// Cambia un número, guarda y prueba: no hay que tocar nada más.
public static class Tuning
{
    public const string GameName = "ARES-1";

    public static readonly DifficultyDef[] Difficulties =
    {
        // 3 niveles, y cada uno tiene MÁS COSAS que el anterior:
        //  EXPLORADOR (7-9 años): barras de batería, aire, comida, salud, escudo y ÁNIMO. 2 máquinas y 4 tareas.
        //  ASTRONAUTA (10-12): + casa, paneles, calefacción, reparar, limpiar, polvo, noche larga, apuntar la antena y llamar a la Tierra. Las tareas gastan MUCHA batería.
        //  COMANDANTE (13+): TODO: + rocas, fugas, datos borrados por tormentas y sin consejos. Aún más dura.
        new DifficultyDef { name = "EXPLORADOR", ages = "7-9 años",   days = 7,  severity = 1.0f, storms = 2, fails = 1, longDur = 1, leaks = 0, supplies = 2, hints = true,  ease = 0.75f, bonusTasks = 0, kids = true,
                            morale = true, energyMult = 1.6f, solarScale = 0.8f, lifelines = 2, surprise = 0.60f, luck = 0.50f, goalPacks = 8, liveDrain = 0.4f,
                            drainMult = 1.0f, foodMult = 1.1f, liveEvents = 0.25f, extraSurprise = 0.10f },
        new DifficultyDef { name = "ASTRONAUTA", ages = "10-12 años", days = 9,  severity = 1.2f, storms = 2, fails = 3, longDur = 2, leaks = 1, supplies = 1, hints = true,  ease = 0.95f,
                            morale = true, house = true, panels = true, heater = true, aim = true, call = true,
                            energyMult = 1.3f, lifelines = 3, surprise = 0.50f, luck = 0.40f, goalPacks = 8, liveDrain = 0.9f,
                            drainMult = 1.0f, foodMult = 1.4f, liveEvents = 0.30f, extraSurprise = 0.20f },
        new DifficultyDef { name = "COMANDANTE", ages = "13+ años",   days = 12, severity = 1.35f, storms = 3, fails = 4, longDur = 2, leaks = 2, supplies = 0, hints = false, ease = 1.0f,
                            morale = true, house = true, panels = true, heater = true, aim = true, call = true, hazards = true, dataLoss = true,
                            energyMult = 1.35f, solarScale = 0.95f, lifelines = 2, surprise = 0.80f, luck = 0.25f, goalPacks = 6, liveDrain = 1.2f,
                            drainMult = 1.2f, foodMult = 1.5f, liveEvents = 0.45f, extraSurprise = 0.50f },
    };


    // ---- ÁNIMO: con buen ánimo hay más tareas y cada una rinde más; con mal ánimo, menos tareas y rinden menos
    public static int TasksBase = 4;             // tareas por noche con ánimo normal
    public static float MoodHigh = 70f;          // ánimo >= esto: ANIMADO  (+1 tarea, rinde x1.25, experimentos dobles)
    public static float MoodOk = 40f;            // ánimo >= esto: BIEN     (normal)
    public static float MoodLow = 20f;           // ánimo >= esto: CANSADO  (-1 tarea, rinde x0.75); menos: DESANIMADO (-2 tareas, rinde x0.5)
    public static float MoodHighMult = 1.25f, MoodOkMult = 1f, MoodTiredMult = 0.75f, MoodLowMult = 0.5f;
    public static float MoodBoostMult = 1.2f;    // extra mientras dura una BUENA NOTICIA
    public static int MoodBoostNights = 2;       // noches que dura ese extra
    public static float DoubleExperiment = 0.35f;// con el equipo ANIMADO: probabilidad de que un experimento salga doble

    // ---- Energía (cada día)
    public static float SolarMax = 145f;          // lo que generan los paneles limpios, con sol normal
    public static float PanelCurve = 1.3f;       // 1 = la carga sube en línea recta con el % de paneles; más de 1 = con paneles casi al 100 % carga MÁS rápido
    public static float RestEnergy = 3f;         // batería que se ahorra/recupera al DESCANSAR (el equipo apaga cosas)
    public static float MachineDrainPerSec = 3f;  // batería que gasta CADA máquina encendida (invernadero, aire, calefacción) POR SEGUNDO. Si la batería llega a 0 se acaba la misión (apagón).
    public static float BaseDrain = 9f;          // consumo fijo del puesto
    public static float DustSolar = 0.35f;       // fracción de sol que llega en tormenta de polvo
    public static float LongNightSolar = 0.2f;   // fracción de sol en noche larga

    // ---- Oxígeno y comida (cada día)
    public static float AirDrainPerSec = 1f;          // el aire baja SIEMPRE esto por segundo (el equipo respira)
    public static float RecyclerAirPerSec = 4f;       // aire que sube POR SEGUNDO con la máquina de aire encendida
    public static float FoodBase = 8f;
    public static float GreenhouseFoodPerSec = 4f;    // comida que sube el invernadero encendido POR SEGUNDO

    // ---- Desgaste diario
    public static float Wear = 2f;               // estructura
    public static float ShieldDecay = 3f;        // blindaje
    public static float PanelDecay = 5f;         // polvo que se pega a los paneles cada noche (baja poco a poco durante el día)
    // ---- Flujos EN VIVO: la batería sube con los paneles y baja con las máquinas mientras juegas
    public static float LiveShare = 0.65f;        // qué parte de lo de la noche se aplica en vivo (el resto, al terminar la noche)
    public static float LiveShareDust = 1f;      // el polvo de los paneles cae TODO en vivo (se nota el % bajando)
    public static float LiveSeconds = 30f;       // segundos que tarda en aplicarse esa parte
    public static float MoodPerTrouble = 5f;     // ánimo que baja cada noche por CADA cosa que falla
    public static float MoodTroubleCap = 20f;    // máximo de ánimo que se pierde por noche por fallas
    public static float DailyDose = 5f;          // radiación diaria sin blindaje (se reduce con el blindaje)
    public static float HealthRegen = 2f;        // salud que se recupera si hay aire y comida

    // ---- Tareas (cuánto cambian y cuánto cansan)
    public static float ReinforceAmount = 30f;
    public static float RepairAmount = 25f;
    public static float CleanAmount = 60f;
    public static float RestMorale = 25f;
    public static float RestHealth = 8f;
    public static float FatigueReinforce = 3f;   // ánimo que cuesta cada tarea
    public static float FatigueRepair = 3f;
    public static float FatigueClean = 2f;
    public static float FatigueExperiment = 4f;
    // Batería que gasta cada tarea (se multiplica por energyMult de la dificultad). DESCANSAR no gasta nada.
    public static float EnReinforce = 6f, EnRepair = 6f, EnClean = 4f, EnExperiment = 6f, EnUpload = 6f, EnAim = 2f, EnCall = 5f;
    // Comida que gasta cada tarea (se multiplica por 'ease' de la dificultad). DESCANSAR también da hambre.
    public static float FoodReinforce = 6f, FoodRepair = 7f, FoodClean = 6f, FoodExperiment = 10f, FoodRest = 6.5f, FoodAim = 3f, FoodUpload = 4f, FoodCall = 3f;
    public static float HealthExperiment = 4f;   // salud que cuesta cada experimento (trabajo pesado)
    public static float EnMargin = 4f;           // batería que debe sobrar para poder hacer una tarea

    // ---- Antena: apuntar y subir el buffer (como en QUIPU)
    public static float BufferMax = 100f;        // MB de datos que caben a bordo
    public static float ExperimentData = 25f;    // MB que guarda cada experimento
    public static float UploadRate = 50f;        // MB que se suben con cada tarea SUBIR BUFFER
    public static float AimSeconds = 2.2f;       // segundos que tarda en girar la antena (suena el motor)
    public static float AimFatigue = 1f;
    public static float UploadFatigue = 2f;
    public static float StormBufferLoss = 0.5f;  // fracción del buffer que borra una tormenta solar

    // ---- Llamar a la Tierra (la respuesta llega con retraso)
    public static float CallFatigue = 2f;        // moral que cuesta
    public static float RepairGuideBonus = 10f;  // estructura extra si la Tierra te guía la reparación
    public static float FamilyMorale = 20f;      // moral que da el mensaje de la familia
    public static float NewsGoodChance = 0.25f;  // la respuesta trae una BUENA noticia (25 %)
    public static float NewsBadChance = 0.25f;   // la respuesta trae una MALA noticia (25 %); el resto (50 %) es normal
    public static float NewsGoodMood = 20f;      // ánimo extra con una buena noticia (con la familia: se duplica el mensaje)
    public static float NewsBadMood = 15f;       // ánimo que se pierde con una mala noticia
    public static int ForecastLong = 5;          // días de pronóstico cuando la Tierra te ayuda
    public static int ForecastShort = 2;         // días de pronóstico normales

    // ---- Eventos
    public static float StormDose = 40f;         // dosis de radiación de una tormenta solar sin blindaje
    public static float StormShieldEffect = 0.8f;// cuánto reduce el blindaje al 100 %
    public static float MeteorDamage = 15f;
    public static float LeakOxygen = 20f;
    public static float LeakIntegrity = 5f;
    public static float LongNightDamage = 12f;   // estructura que se pierde si no hay calefacción
    public static float LongNightMorale = 8f;
    public static float HighDrainAt = 8f;        // si las máquinas encendidas gastan más que esto POR SEGUNDO sale "¡CONSUMO ALTO!" en rojo
    public static float LiveEventSeconds = 25f;  // cada cuántos segundos jugando puede caer un imprevisto EN VIVO (con la probabilidad 'liveEvents' de la dificultad)
    public static int DustNights = 1;                // noches que dura una tormenta de polvo (1 = solo una noche, no continúa)
    public static float DustPanelsPerSec = 3.5f;      // TORMENTA DE POLVO: los paneles pierden esto (%) por segundo
    public static float DustHousePerSec = 0.3f;       // la tormenta de polvo raspa la casa: daño por segundo (menos con el escudo arriba)
    public static float StormHousePerSec = 0.4f;      // durante una TORMENTA SOLAR la casa sufre este daño por segundo
    public static float StormHouseDamage = 8f;        // y un golpe de daño a la casa cuando llega la tormenta solar
    public static float QuakeDamage = 18f;       // casa que pierde un SISMO (imprevisto)
    public static float QuakeMood = 6f;          // ánimo que quita un sismo
    public static float CrackDamage = 12f;       // casa que pierde una GRIETA por cambio de temperatura (imprevisto)
    public static float HitDamage = 14f;         // casa que pierde un GOLPE DE ARENA (imprevisto)

    // ---- ESCUDO y CASA: sin escudo la casa sufre MUCHO más
    public static float ShieldBrokenBelow = 10f;     // blindaje <= esto: el escudo está ROTO
    public static float ShieldBrokenHouseMult = 2f;  // con el escudo roto, TODO el daño a la casa se multiplica por esto (crítico)
    public static float ShieldHouseReduction = 0.3f; // con el escudo al 100 % el daño a la casa baja esta fracción (nunca llega a 0)
    public static float HouseBrokenBelow = 25f;      // casa <= esto: la casa está ROTA
    public static float HouseBrokenHealthPerSec = 0.6f; // salud que se pierde POR SEGUNDO con la casa rota
    public static float HouseBrokenMoodPerSec = 0.7f;   // ánimo que se pierde POR SEGUNDO con la casa rota
    public static float HouseBrokenNightHealth = 8f;    // salud extra que se pierde cada noche con la casa rota
    public static float HouseBrokenNightMood = 8f;      // ánimo extra que se pierde cada noche con la casa rota

    // ---- SALUD y ÁNIMO POR SEGUNDO (pasan mientras juegas; se multiplican por 'liveDrain' de la dificultad)
    public static float MoodTiredHealthPerSec = 1f;  // ánimo BAJO (menos de MoodOk): salud que se pierde por segundo
    public static float MoodLowHealthPerSec = 1f;    // ánimo MUY BAJO (menos de MoodLow): salud que se pierde por segundo
    public static float NoAirHealthPerSec = 4f;      // SIN AIRE (0 %): salud que se pierde por segundo
    public static float StarveHealthPerSec = 1.5f;   // SIN COMIDA (0 %): salud que se pierde por segundo

    // ---- FRÍO y CALOR (temperatura del equipo). Con la noche larga SIN calefacción se CONGELAN (copo de nieve junto al astronauta);
    //      con la calefacción encendida mucho rato durante una TORMENTA SOLAR sienten CALOR (llama). Los dos bajan salud y ánimo.
    public static float ColdBatteryDrain = 12f;      // batería extra que gasta el frío en un día
    public static float ColdHeaterFactor = 0.35f;    // con la calefacción funcionando, el frío pega solo esta fracción de batería
    public static float TempNormalC = 20f;           // temperatura normal del equipo (grados)
    public static float TempCPerUnit = 0.5f;        // cuántos grados es cada punto de temperatura interna
    public static float TempStormPerSec = 0.35f;    // la TORMENTA SOLAR sube la temperatura por segundo, aunque la calefacción esté apagada
    public static float TempChillPerSec = 0.8f;      // cuánto se enfría el equipo por segundo (noche larga sin calefacción)
    public static float TempHeatPerSec = 0.7f;       // extra por segundo si además la CALEFACCIÓN está encendida durante la tormenta solar
    public static float TempRelaxPerSec = 1.0f;      // cuánto vuelve a la normalidad por segundo
    public static float TempIconAt = 25f;            // a partir de aquí aparece el copo de nieve o la llama junto al astronauta
    public static float TempDamageAt = 50f;          // a partir de aquí el equipo pierde salud y ánimo
    public static float TempHealthPerSec = 0.5f;     // salud que se pierde por segundo con frío o calor fuerte
    public static float TempMoodPerSec = 0.5f;       // ánimo que se pierde por segundo con frío o calor fuerte
    public static float TempNightSecs = 12f;         // segundos que cuenta cada noche que pasa para la temperatura
    public static int StormHeatLingerDays = 1;       // la tormenta solar sigue calentando esta cantidad de días después
    public static float ColdNightHealth = 4f;        // salud que se pierde en la noche larga sin calefacción

    // ---- Recompensa por jugar bien y cápsula de emergencia
    public static int BonusMaxWasted = 1;        // para ganar el BONO DE LA NOCHE puedes dejar sin usar como máximo estas tareas
    public static int BonusNeedsUpload = 1;      // 1 = el BONO solo se gana si esa noche MANDASTE datos a la Tierra (0 = siempre)
    public static float BonusEnergy = 15f, BonusAir = 15f, BonusFood = 15f, BonusHealth = 10f, BonusHouse = 12f, BonusMood = 15f;
    public static float BonusPerfect = 75f;      // si todo está por encima de esto: NOCHE PERFECTA (+1 tarea mañana)
    public static float RescueAmount = 40f;      // lo que repone una cápsula de emergencia
    public static int LiveRescue = 1;            // 1 = la cápsula también salva A MITAD DE LA NOCHE (si la batería o la salud llegan a 0 y quedan cápsulas); 0 = solo al terminar la noche

    // ---- Puntaje
    public static float PtsPerDay = 25f;
    public static float PtsPerExperiment = 70f;     // cada experimento SUBIDO a la Tierra (cuesta más tareas que antes)
    public static float PtsCompletion = 100f;
    public static float PtsState = 2f;           // x promedio del estado final (0-100)
    public static float PtsRefPerDay = 75f;      // para calcular la nota
    public static float GradeS = 0.85f, GradeA = 0.65f, GradeB = 0.48f, GradeC = 0.30f;
}
