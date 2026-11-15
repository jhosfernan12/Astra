#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Asigna SOLO los 100 clips de "Assets/Sonidos/Lofi Horror Menu Sounds" a GameAudio > Pack.
//  - Automático: cada vez que pulsas Play (y cuando Unity recompila los scripts).
//  - Manual: menú  Quipu > Llenar pack de audios
public static class AutoAssignAudio
{
    const string Folder = "Assets/Sonidos/Lofi Horror Menu Sounds";

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.delayCall += delegate { Fill(true); };
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode) Fill(true);
    }

    [MenuItem("Quipu/Llenar pack de audios")]
    public static void FillFromMenu() { Fill(false); }

    static void Fill(bool silent)
    {
        if (EditorApplication.isPlaying) return;
        var audio = UnityEngine.Object.FindObjectOfType<GameAudio>();
        if (audio == null)
        {
            if (!silent) EditorUtility.DisplayDialog("Quipu", "No encuentro GameAudio en la escena abierta.\nAbre tu escena (la que tiene GameFlow) y repite.", "OK");
            return;
        }
        if (audio.pack == null) audio.pack = new LofiPack();

        var fields = new List<FieldInfo>();
        foreach (var f in typeof(LofiPack).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (f.FieldType == typeof(AudioClip)) fields.Add(f);

        // En modo silencioso, si ya está todo asignado no hace nada.
        if (silent)
        {
            bool allSet = true;
            foreach (var f in fields) if (f.GetValue(audio.pack) == null) { allSet = false; break; }
            if (allSet) return;
        }

        var clips = new List<AudioClip>();
        string[] guids = AssetDatabase.IsValidFolder(Folder)
            ? AssetDatabase.FindAssets("t:AudioClip", new string[] { Folder })
            : AssetDatabase.FindAssets("t:AudioClip");
        foreach (var guid in guids)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) clips.Add(c);
        }

        Undo.RecordObject(audio, "Llenar pack de audios");
        int ok = 0;
        var missing = new List<string>();
        foreach (var f in fields)
        {
            var clip = Find(clips, f.Name);
            if (clip == null) { missing.Add(f.Name); continue; }
            f.SetValue(audio.pack, clip);
            ok++;
        }

        EditorUtility.SetDirty(audio);
        EditorSceneManager.MarkSceneDirty(audio.gameObject.scene);

        string msg = "Pack de audios: " + ok + " de " + fields.Count + " clips asignados.";
        if (!AssetDatabase.IsValidFolder(Folder)) msg += "\n(No existe la carpeta " + Folder + ", busqué en todo el proyecto.)";
        if (missing.Count > 0) msg += "\nNo encontré: " + string.Join(", ", missing.ToArray());
        Debug.Log("[Quipu] " + msg);
        if (!silent)
        {
            Selection.activeObject = audio.gameObject;
            EditorUtility.DisplayDialog("Quipu - Audios", msg + "\n\nGuarda la escena (Ctrl+S).", "OK");
        }
    }

    // Acepta "Button_01" o "11.Button_01". Elige el nombre más corto que coincida.
    static AudioClip Find(List<AudioClip> clips, string key)
    {
        AudioClip best = null;
        foreach (var c in clips)
        {
            if (!c.name.EndsWith(key, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (best == null || c.name.Length < best.name.Length) best = c;
        }
        return best;
    }
}
#endif
