using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Calm Scene + HD Text
    ///   • swaps the pixel font for Fredoka (smooth, rounded, crisp at any size)
    ///   • fewer plants, fewer petals/snowflakes, fewer butterflies/birds
    ///   • hides the foreground grass strip
    /// Run it again to thin things out even more. Ctrl+Z undoes it.
    /// </summary>
    public static class CalmSceneHdText
    {
        const string FontDir = "Assets/KittyCrash/Art/Fonts";

        [MenuItem("Kitty Crash/Calm Scene + HD Text")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
#if UNITY_2023_1_OR_NEWER
            var ui = Object.FindAnyObjectByType<CrashUI>();
            var spawners = Object.FindObjectsByType<TreeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var weather = Object.FindObjectsByType<WeatherSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var critters = Object.FindObjectsByType<CritterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var ui = Object.FindObjectOfType<CrashUI>();
            var spawners = Object.FindObjectsOfType<TreeSpawner>(true);
            var weather = Object.FindObjectsOfType<WeatherSystem>(true);
            var critters = Object.FindObjectsOfType<CritterSpawner>(true);
#endif
            if (ui == null && spawners.Length == 0) { Msg("Open the KittyCrash scene first."); return; }

            Undo.SetCurrentGroupName("Calm scene + HD text");
            int group = Undo.GetCurrentGroup();
            var report = new System.Text.StringBuilder();

            // 1. HD text
            var regular = LoadSmooth(FontDir + "/Fredoka-Medium.ttf");
            var bold = LoadSmooth(FontDir + "/Fredoka-Bold.ttf");
            if (ui != null && regular != null)
            {
                Undo.RecordObject(ui, "Font");
                ui.font = regular;
                ui.boldFont = bold != null ? bold : regular;
                EditorUtility.SetDirty(ui);
                report.AppendLine("• Text: Fredoka (smooth) instead of the pixel font");
            }
            else report.AppendLine("• Fonts not found in Art/Fonts — merge the kit first");

            // 2. fewer plants
            int plants = 0;
            foreach (var s in spawners)
            {
                Undo.RecordObject(s, "Fewer plants");
                s.poolSize = Mathf.Max(3, s.poolSize / 2);
                s.spacing = new Vector2(s.spacing.x * 1.8f, s.spacing.y * 1.8f);
                EditorUtility.SetDirty(s);
                plants++;
            }
            if (plants > 0) report.AppendLine($"• Plants: about half as many ({plants} row(s))");

            // 3. fewer petals / leaves / snowflakes
            foreach (var w in weather)
            {
                Undo.RecordObject(w, "Less weather");
                w.maxFlakes = Mathf.Max(12, w.maxFlakes / 2);
                EditorUtility.SetDirty(w);
                report.AppendLine($"• Weather: max {w.maxFlakes} petals/leaves/flakes");
            }

            // 4. fewer critters
            foreach (var c in critters)
            {
                Undo.RecordObject(c, "Fewer critters");
                c.maxCritters = Mathf.Max(2, c.maxCritters / 2);
                c.spawnEvery *= 1.8f;
                EditorUtility.SetDirty(c);
                report.AppendLine($"• Butterflies/birds: max {c.maxCritters}");
            }

            // 5. foreground grass strip off
            var fg = GameObject.Find("ForegroundGrass");
            if (fg != null)
            {
                Undo.RecordObject(fg, "Hide grass");
                fg.SetActive(false);
                report.AppendLine("• Foreground grass hidden (tick it back on in the Hierarchy if you miss it)");
            }

            Undo.CollapseUndoOperations(group);
            var scene = ui != null ? ui.gameObject.scene : spawners[0].gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            Msg("Done!\n" + report +
                "\nAlso set the Game view 'Scale' slider to 1x — zooming the Game view makes any text blurry.\n" +
                "Still too busy? Run this menu again.\n\nSave with Ctrl+S.");
        }

        static Font LoadSmooth(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (imp != null && (imp.fontTextureCase != FontTextureCase.Dynamic || imp.fontRenderingMode != FontRenderingMode.Smooth))
            {
                imp.fontTextureCase = FontTextureCase.Dynamic;
                imp.fontRenderingMode = FontRenderingMode.Smooth;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Calm Scene", m, "OK");
    }
}
