using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Install Cuteness Pack
    /// Gradient sky + sun, seasonal critters, foreground grass, cat emotes / bounce / hop / paw prints.
    /// Non-destructive, safe to re-run (replaces what it added before). Ctrl+Z undoes it.
    /// </summary>
    public static class CutenessInstaller
    {
        const string ArtDir = "Assets/KittyCrash/Art/Generated";
        const float GroundTop = -2.5f;

        [MenuItem("Kitty Crash/Install Cuteness Pack")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
#if UNITY_2023_1_OR_NEWER
            var walker = Object.FindAnyObjectByType<CatWalker>();
#else
            var walker = Object.FindObjectOfType<CatWalker>();
#endif
            if (walker == null) { Msg("Open the KittyCrash scene first."); return; }
            if (S("cute_bubble") == null && !File.Exists(ArtDir + "/cute_bubble.png")) { Msg("Cute art not found in " + ArtDir + " — copy the kit's Art/Generated folder in first."); return; }

            ConfigureImports();

            Undo.SetCurrentGroupName("Install Cuteness Pack");
            int group = Undo.GetCurrentGroup();

            // Sky + sun + critters
            var old = GameObject.Find("Cuteness");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject("Cuteness");
            Undo.RegisterCreatedObjectUndo(root, "Cuteness");

            var sky = root.AddComponent<SkyGradient>();
            sky.sunSprite = S("cute_sun");

            var critters = root.AddComponent<CritterSpawner>();
            critters.butterflyFrames = new[]
            {
                S("cute_butterfly_orange_0"), S("cute_butterfly_orange_1"),
                S("cute_butterfly_blue_0"), S("cute_butterfly_blue_1"),
                S("cute_butterfly_pink_0"), S("cute_butterfly_pink_1"),
            };
            critters.beeFrames = new[] { S("cute_bee_0"), S("cute_bee_1") };
            critters.birdFrames = new[] { S("cute_bird_0"), S("cute_bird_1") };
            critters.firefly = S("cute_firefly");
            critters.groundY = GroundTop;

            // Foreground grass (passes in front of the scene, faster than the world = depth)
            var fg = new GameObject("ForegroundGrass");
            fg.transform.SetParent(root.transform, false);
            var layer = fg.AddComponent<ParallaxLayer>();
            layer.spring = S("fg_grass_spring"); layer.summer = S("fg_grass_summer");
            layer.autumn = S("fg_grass_autumn"); layer.winter = S("fg_grass_winter");
            layer.follow = -0.35f;
            layer.baselineY = GroundTop - 1.95f;
            layer.sortingOrder = 40;

            // Cat
            var cute = walker.GetComponent<CatCute>();
            if (cute == null) cute = Undo.AddComponent<CatCute>(walker.gameObject);
            Undo.RecordObject(cute, "Cat cute");
            cute.bubble = S("cute_bubble");
            cute.heart = S("cute_icon_heart");
            cute.exclaim = S("cute_icon_exclaim");
            cute.question = S("cute_icon_question");
            cute.zzz = S("cute_icon_zzz");
            cute.note = S("cute_icon_note");
            cute.star = S("cute_icon_star");
            cute.heartParticle = S("cute_particle_heart");
            cute.sparkle = S("cute_particle_sparkle");
            cute.pawPrint = S("cute_paw");
            EditorUtility.SetDirty(cute);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(walker.gameObject.scene);
            Msg("Cuteness installed!\n\n" +
                "• Gradient sky + glowing sun per season\n" +
                "• Butterflies, bees, fireflies and birds (they follow the seasons)\n" +
                "• Foreground grass & flowers for depth\n" +
                "• Cat emotes (♪ ? ★ ♥ ! zzz), bouncy walk, happy hop + hearts on cash-out, paw prints\n\n" +
                "Save (Ctrl+S) and press Play. Press N to flip seasons.\n" +
                "Bubble too high/low? Cat › Cat Cute › Bubble Offset.");
        }

        static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        internal static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("cute_") && !name.StartsWith("fg_")) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = HdArt.Filter;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;

                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.spritePixelsPerUnit = HdArt.Ppu(name, 16);
                s.spriteAlignment = (int)(name.StartsWith("fg_") ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                s.spriteMeshType = name.StartsWith("fg_") ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Cuteness", m, "OK");
    }
}
