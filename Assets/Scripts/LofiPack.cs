using System.Reflection;
using UnityEngine;

// Los 100 sonidos del pack "Lofi Horror Menu Sounds". Cada campo tiene EXACTAMENTE el nombre del clip.
// Aparece en el Inspector dentro de GameAudio > Pack. Arrastra cada clip a su campo (o usa Quipu > Llenar pack de audios).
[System.Serializable]
public class LofiPack
{
    [Header("Broken (10)")]
    public AudioClip Broken_01;
    public AudioClip Broken_02;
    public AudioClip Broken_03;
    public AudioClip Broken_04;
    public AudioClip Broken_05;
    public AudioClip Broken_06;
    public AudioClip Broken_07;
    public AudioClip Broken_08;
    public AudioClip Broken_09;
    public AudioClip Broken_10;

    [Header("Button (30)")]
    public AudioClip Button_01;
    public AudioClip Button_02;
    public AudioClip Button_03;
    public AudioClip Button_04;
    public AudioClip Button_05;
    public AudioClip Button_06;
    public AudioClip Button_07;
    public AudioClip Button_08;
    public AudioClip Button_09;
    public AudioClip Button_10;
    public AudioClip Button_11;
    public AudioClip Button_12;
    public AudioClip Button_13;
    public AudioClip Button_14;
    public AudioClip Button_15;
    public AudioClip Button_16;
    public AudioClip Button_17;
    public AudioClip Button_18;
    public AudioClip Button_19;
    public AudioClip Button_20;
    public AudioClip Button_21;
    public AudioClip Button_22;
    public AudioClip Button_23;
    public AudioClip Button_24;
    public AudioClip Button_25;
    public AudioClip Button_26;
    public AudioClip Button_27;
    public AudioClip Button_28;
    public AudioClip Button_29;
    public AudioClip Button_30;

    [Header("Empty Tape (5)")]
    public AudioClip Empty_Tape_01;
    public AudioClip Empty_Tape_02;
    public AudioClip Empty_Tape_03;
    public AudioClip Empty_Tape_04;
    public AudioClip Empty_Tape_05;

    [Header("Error (10)")]
    public AudioClip Error_01;
    public AudioClip Error_02;
    public AudioClip Error_03;
    public AudioClip Error_04;
    public AudioClip Error_05;
    public AudioClip Error_06;
    public AudioClip Error_07;
    public AudioClip Error_08;
    public AudioClip Error_09;
    public AudioClip Error_10;

    [Header("Glitch (7)")]
    public AudioClip Glitch_01;
    public AudioClip Glitch_02;
    public AudioClip Glitch_03;
    public AudioClip Glitch_04;
    public AudioClip Glitch_05;
    public AudioClip Glitch_06;
    public AudioClip Glitch_07;

    [Header("Logo (5)")]
    public AudioClip Logo_01;
    public AudioClip Logo_02;
    public AudioClip Logo_03;
    public AudioClip Logo_04;
    public AudioClip Logo_05;

    [Header("Menu (20)")]
    public AudioClip Menu_01;
    public AudioClip Menu_02;
    public AudioClip Menu_03;
    public AudioClip Menu_04;
    public AudioClip Menu_05;
    public AudioClip Menu_06;
    public AudioClip Menu_07;
    public AudioClip Menu_08;
    public AudioClip Menu_09;
    public AudioClip Menu_10;
    public AudioClip Menu_11;
    public AudioClip Menu_12;
    public AudioClip Menu_13;
    public AudioClip Menu_14;
    public AudioClip Menu_15;
    public AudioClip Menu_16;
    public AudioClip Menu_17;
    public AudioClip Menu_18;
    public AudioClip Menu_19;
    public AudioClip Menu_20;

    [Header("Noise (8)")]
    public AudioClip Noise_01;
    public AudioClip Noise_02;
    public AudioClip Noise_03;
    public AudioClip Noise_04;
    public AudioClip Noise_05;
    public AudioClip Noise_06;
    public AudioClip Noise_07;
    public AudioClip Noise_08;

    [Header("Objective (5)")]
    public AudioClip Objective_01;
    public AudioClip Objective_02;
    public AudioClip Objective_03;
    public AudioClip Objective_04;
    public AudioClip Objective_05;

    // Devuelve el clip por su nombre ("Button_01"). null si no existe o está vacío.
    public AudioClip Get(string clipName)
    {
        var f = typeof(LofiPack).GetField(clipName, BindingFlags.Public | BindingFlags.Instance);
        return f == null ? null : f.GetValue(this) as AudioClip;
    }
}
