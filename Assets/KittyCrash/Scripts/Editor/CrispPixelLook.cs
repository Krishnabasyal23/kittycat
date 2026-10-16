using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Crisp Pixel Look + Clear UI
    ///   • art back to sharp pixel art (no smoothing / blur): Point filtering, original resolution
    ///   • mood faces as pixel art (match the theme)
    ///   • UI text in Fredoka so the multiplier, credits, bet and history are easy to read
    ///   • cat highlight made very light (thin soft outline, no glow, no red)
    /// Ctrl+Z undoes the scene part.
    /// </summary>
    public static class CrispPixelLook
    {
        const string FontDir = "Assets/KittyCrash/Art/Fonts";

        [MenuItem("Kitty Crash/Crisp Pixel Look + Clear UI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
#if UNITY_2023_1_OR_NEWER
            var ui = Object.FindAnyObjectByType<CrashUI>();
            var hl = Object.FindAnyObjectByType<CatHighlight>();
            var mood = Object.FindAnyObjectByType<MoodFace>();
#else
            var ui = Object.FindObjectOfType<CrashUI>();
            var hl = Object.FindObjectOfType<CatHighlight>();
            var mood = Object.FindObjectOfType<MoodFace>();
#endif
            var report = new System.Text.StringBuilder();

            // 1. pixel art, sharp
            string marker = HdArt.ArtDir + "/_HD.txt";
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(marker) != null) AssetDatabase.DeleteAsset(marker);
            EditorUtility.DisplayProgressBar("Kitty Crash", "Re-importing pixel art...", 0.3f);
            try
            {
                FestiveExtrasInstaller.ConfigureImports();
                CutenessInstaller.ConfigureImports();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/KittyCrash/Art/Mood" }))
                {
                    var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                    if (imp == null) continue;
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.filterMode = FilterMode.Point;
                    imp.mipmapEnabled = false;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.spritePixelsPerUnit = 100;
                    imp.SaveAndReimport();
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            report.AppendLine("• Art: sharp pixel art again (no blur)");
            report.AppendLine("• Mood faces: pixel art");

            Undo.SetCurrentGroupName("Crisp pixel look");
            int group = Undo.GetCurrentGroup();

            // 2. readable UI text
            var regular = LoadFont(FontDir + "/Fredoka-Medium.ttf");
            var bold = LoadFont(FontDir + "/Fredoka-Bold.ttf");
            if (ui != null && regular != null)
            {
                Undo.RecordObject(ui, "Font");
                ui.font = regular;
                ui.boldFont = bold != null ? bold : regular;
                EditorUtility.SetDirty(ui);
                report.AppendLine("• UI text: Fredoka (clean and easy to read)");
            }

            // 3. light highlight
            if (hl != null)
            {
                Undo.RecordObject(hl, "Light highlight");
                hl.thickness = 1f;
                hl.calmColor = new Color(1f, 1f, 1f, 0.4f);
                hl.tenseColor = new Color(1f, 0.9f, 0.8f, 0.5f);
                hl.winColor = new Color(1f, 0.92f, 0.55f, 0.6f);
                hl.glow = false;
                EditorUtility.SetDirty(hl);
                report.AppendLine("• Cat highlight: light, thin outline only (no glow)");
            }

            if (mood != null)
            {
                Undo.RecordObject(mood, "Mood");
                mood.faceSize = 160f; // 32px faces x5 = crisp
                mood.vignetteStrength = 0.35f;
                EditorUtility.SetDirty(mood);
            }

            Undo.CollapseUndoOperations(group);
            var scene = ui != null ? ui.gameObject.scene : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);

            Msg("Done!\n" + report +
                "\nIMPORTANT: set the Game view 'Scale' slider to 1x — 1.5x zoom is what makes everything blurry.\n" +
                "Best demo size: Game view aspect 'Full HD (1920x1080)' or 16:9.\n\nSave with Ctrl+S.");
        }

        static Font LoadFont(string path)
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

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Crisp Pixel Look", m, "OK");
    }
}
