using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Install Mood Face + HD Art + Cat Highlight
    ///   1. HD art: re-imports Art/Generated (now 4x resolution, smooth edges) with Bilinear + 4x Pixels-Per-Unit
    ///      so every sprite keeps its size but looks sharp.
    ///   2. Mood face: the cat-emoji in the top-right that shows how the player should feel (calm → panic, win, cry),
    ///      plus tension meter, heartbeat vignette, milestone pop-ups and floating +/- numbers.
    ///   3. Cat highlight: outline + soft glow so kitty stands out from the background (turns red as tension rises).
    ///   4. Temptations: fake-out distractions on the path — kitty stares… and keeps walking.
    /// Safe to run again. Ctrl+Z undoes the scene part.
    /// </summary>
    public static class MoodInstaller
    {
        const string MoodDir = "Assets/KittyCrash/Art/Mood";
        const string MatPath = "Assets/KittyCrash/Shaders/CatSilhouette.mat";

        [MenuItem("Kitty Crash/Install Mood Face + HD Art + Cat Highlight")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
#if UNITY_2023_1_OR_NEWER
            var rm = Object.FindAnyObjectByType<RoundManager>();
            var walker = Object.FindAnyObjectByType<CatWalker>();
#else
            var rm = Object.FindObjectOfType<RoundManager>();
            var walker = Object.FindObjectOfType<CatWalker>();
#endif
            if (rm == null || walker == null) { Msg("Open the KittyCrash scene first."); return; }

            var report = new System.Text.StringBuilder();

            // 1. HD art imports
            if (HdArt.IsHd)
            {
                EditorUtility.DisplayProgressBar("Kitty Crash", "Re-importing HD art...", 0.2f);
                try
                {
                    FestiveExtrasInstaller.ConfigureImports();
                    CutenessInstaller.ConfigureImports();
                }
                finally { EditorUtility.ClearProgressBar(); }
                report.AppendLine("• HD art: backgrounds, ground, props, critters re-imported sharp (4x)");
            }
            else report.AppendLine("• Art: crisp pixel look");

            // 2. mood face sprites
            ConfigureMoodImports();
            Undo.SetCurrentGroupName("Mood face + highlight");
            int group = Undo.GetCurrentGroup();

            var mood = rm.GetComponent<MoodFace>();
            if (mood == null) mood = Undo.AddComponent<MoodFace>(rm.gameObject);
            Undo.RecordObject(mood, "Mood sprites");
            mood.calm = M("calm"); mood.happy = M("happy"); mood.excited = M("excited");
            mood.nervous = M("nervous"); mood.anxious = M("anxious"); mood.panic = M("panic");
            mood.win = M("win"); mood.relieved = M("relieved"); mood.sad = M("sad"); mood.surprised = M("surprised");
            EditorUtility.SetDirty(mood);
            report.AppendLine(mood.calm != null ? "• Mood face added (top-right of the game view)" : "• Mood face added, but Art/Mood sprites are missing — merge the kit");

            // 3. highlight
            var mat = SilhouetteMaterial();
            var hl = walker.GetComponent<CatHighlight>();
            if (hl == null) hl = Undo.AddComponent<CatHighlight>(walker.gameObject);
            Undo.RecordObject(hl, "Highlight");
            hl.silhouetteMaterial = mat;
            EditorUtility.SetDirty(hl);
            report.AppendLine(mat != null ? "• Cat outline + glow added" : "• Cat glow added (outline shader not compiled yet — run this again in a moment)");

            // 4. temptations
            if (walker.GetComponent<CatTemptations>() == null) Undo.AddComponent<CatTemptations>(walker.gameObject);
            report.AppendLine("• Temptations on the path (kitty stares, then keeps walking)");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);

            Msg("Done!\n" + report +
                "\nTweak: Managers › Mood Face (size, vignette strength, milestones) · Cat › Cat Highlight (thickness, colours, glow) · " +
                "Cat › Cat Temptations (how often).\n\nSave with Ctrl+S, then Play.");
        }

        static Sprite M(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{MoodDir}/mood_{n}.png");

        static void ConfigureMoodImports()
        {
            if (!AssetDatabase.IsValidFolder(MoodDir)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { MoodDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;   // pixel-art faces stay crisp
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.spritePixelsPerUnit = 100;
                imp.SaveAndReimport();
            }
        }

        static Material SilhouetteMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return mat;
            var sh = Shader.Find("KittyCrash/SpriteSilhouette");
            if (sh == null) return null;
            mat = new Material(sh) { name = "CatSilhouette" };
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Mood & Highlight", m, "OK");
    }
}
