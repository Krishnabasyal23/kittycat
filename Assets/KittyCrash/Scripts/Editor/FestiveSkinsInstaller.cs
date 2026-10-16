using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Install Skins + Festivals + Season Distractions
    ///   1. Cat skins: 6 cleaned-up cats (Art/Cats) on the shared 16-px grid + a skin picker; sleep no longer "does push-ups".
    ///   2. Festivals: Easter / Lunar New Year (spring), Summer Fireworks, Halloween / Diwali (autumn), Christmas (winter)
    ///      with garlands, props, fireworks, sky lanterns, bats, moon and dusk skies (Art/Festive).
    ///   3. Season distractions: summer fish, autumn leaves + tree climbing, winter snow naps, spring butterflies & birds.
    ///   4. Uniform pixels: Pixel Grid on the camera so every sprite shows the same pixel size; plants re-scaled to the grid.
    /// Safe to run again. Ctrl+Z undoes the scene part.
    /// </summary>
    public static class FestiveSkinsInstaller
    {
        const string Cats = "Assets/KittyCrash/Art/Cats";
        const string Fest = "Assets/KittyCrash/Art/Festive";
        const string Gen = "Assets/KittyCrash/Art/Generated";
        const float GridPPU = 32f;      // new art: 32 px per world unit (2x detail)
        const float PlantPPU = 16f;     // the plant pack's own pixel size
        static readonly string[] SkinNames = { "Ginger", "Shadow", "Siamese", "Sphynx", "Snowball", "Tabby" };

        [MenuItem("Kitty Crash/Install Skins + Festivals + Season Distractions")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
            var walker = Find<CatWalker>();
            var rm = Find<RoundManager>();
            if (walker == null || rm == null) { Msg("Open the KittyCrash scene first."); return; }
            if (!AssetDatabase.IsValidFolder(Cats) || !AssetDatabase.IsValidFolder(Fest)) { Msg("Art/Cats or Art/Festive is missing — merge the kit's Assets folder first."); return; }

            var report = new System.Text.StringBuilder();
            float catPPU = GridPPU * Mathf.Max(0.01f, walker.transform.lossyScale.x);   // cat root is scaled → keep 1 art px = 1/32 unit

            EditorUtility.DisplayProgressBar("Kitty Crash", "Importing cat skins...", 0.1f);
            try
            {
                // generated backgrounds/props are 2x detail now → re-import them at 2x pixels-per-unit (same size in the world)
                FestiveExtrasInstaller.ConfigureImports();
                CutenessInstaller.ConfigureImports();
                ImportFolder(Cats, catPPU, SpriteAlignment.BottomCenter, FilterMode.Point);
                ImportFolder(Fest, GridPPU, SpriteAlignment.BottomCenter, FilterMode.Point);
                // floaty things look better centred
                foreach (var n in new[] { "sky_lantern", "halloween_bat_0", "halloween_bat_1", "halloween_moon", "fx_glow", "fx_spark", "fx_dot",
                                          "distract_leaf", "distract_snowflake", "distract_bird", "lny_lantern", "diwali_kandil",
                                          "garland_easter", "garland_lny", "garland_summer", "garland_halloween", "garland_diwali", "garland_xmas" })
                    Reimport($"{Fest}/{n}.png", GridPPU, SpriteAlignment.Center, n == "fx_glow" ? FilterMode.Bilinear : FilterMode.Point);
            }
            finally { EditorUtility.ClearProgressBar(); }

            Undo.SetCurrentGroupName("Skins + festivals");
            int group = Undo.GetCurrentGroup();

            // ── 1. skins ──
            var skins = walker.GetComponent<CatSkins>();
            if (skins == null) skins = Undo.AddComponent<CatSkins>(walker.gameObject);
            Undo.RecordObject(skins, "Skins");
            var list = new List<CatSkins.Skin>();
            for (int i = 1; i <= 6; i++)
            {
                string d = $"{Cats}/Skin{i}";
                if (!AssetDatabase.IsValidFolder(d)) continue;
                list.Add(new CatSkins.Skin
                {
                    name = SkinNames[i - 1],
                    idle = Frames(d, "idle"), walk = Frames(d, "walk"), run = Frames(d, "run"),
                    sit = Frames(d, "sit"), sleep = Frames(d, "sleep"), pounce = Frames(d, "pounce"),
                });
            }
            skins.skins = list.ToArray();
            EditorUtility.SetDirty(skins);
            if (walker.visual != null)
            {
                Undo.RecordObject(walker.visual, "Cat visual");
                walker.visual.localScale = Vector3.one;          // sprites already carry the right size
                walker.visual.localPosition = Vector3.zero;      // pivot = paws
                var anim = walker.visual.GetComponent<CatSpriteAnimator>();
                if (anim != null && list.Count > 0)
                {
                    Undo.RecordObject(anim, "Cat frames");
                    var s = list[0];
                    anim.idle.frames = s.idle; anim.walk.frames = s.walk; anim.run.frames = s.run;
                    anim.sit.frames = s.sit; anim.sleep.frames = s.sleep; anim.sleep.loop = false; anim.sleep.fps = 7f;
                    anim.pounce.frames = s.pounce; anim.flipX = false;
                    EditorUtility.SetDirty(anim);
                }
                var sr = walker.visual.GetComponent<SpriteRenderer>();
                if (sr != null && list.Count > 0 && list[0].idle.Length > 0) { Undo.RecordObject(sr, "Cat sprite"); sr.sprite = list[0].idle[0]; sr.color = Color.white; }
            }
            var fx = walker.GetComponent<CatFx>();
            if (fx != null) { Undo.RecordObject(fx, "Shadow"); fx.shadowWidth = 0.9f; EditorUtility.SetDirty(fx); }
            report.AppendLine($"• {list.Count} cat skins + picker (bottom-left), sleep fixed");

            // ── 2. season distractions + climbing ──
            Undo.RecordObject(walker, "Distractions");
            var ds = walker.distractionSprites ?? new Sprite[0];
            if (ds.Length < 10) System.Array.Resize(ref ds, 10);
            ds[6] = S(Fest, "distract_leaf");
            ds[7] = S(Fest, "distract_snowflake");
            ds[8] = S(Fest, "distract_bird");
            ds[9] = S(Fest, "distract_tree_autumn");
            walker.distractionSprites = ds;
            EditorUtility.SetDirty(walker);
            var climb = walker.GetComponent<CatClimber>();
            if (climb == null) climb = Undo.AddComponent<CatClimber>(walker.gameObject);
            Undo.RecordObject(climb, "Climber");
            climb.treeAutumn = S(Fest, "distract_tree_autumn");
            climb.treeGreen = S(Fest, "distract_tree_green");
            EditorUtility.SetDirty(climb);
            report.AppendLine("• Season distractions: summer fish · autumn leaves & tree climbing · winter snow naps · spring butterflies & birds");

            // ── 3. festivals ──
            var fest = rm.GetComponent<FestivalSystem>();
            if (fest == null) fest = Undo.AddComponent<FestivalSystem>(rm.gameObject);
            Undo.RecordObject(fest, "Festivals");
            fest.skyLantern = S(Fest, "sky_lantern");
            fest.batA = S(Fest, "halloween_bat_0");
            fest.batB = S(Fest, "halloween_bat_1");
            fest.moonSprite = S(Fest, "halloween_moon");
            fest.glow = S(Fest, "fx_glow");
            fest.spark = S(Fest, "fx_spark");
            fest.dot = S(Fest, "fx_dot");
            var main = TreeSpawner.FindMain();
            if (main != null) fest.groundY = main.groundY;
            fest.festivals = new[]
            {
                F(Festival.Easter, "Easter", Season.Spring, "garland_easter", false, false, false, false, false,
                  new Color(1f, 0.8f, 0.9f), new Color[0], "easter_egg_a", "easter_egg_b", "easter_egg_c", "easter_bunny", "easter_basket"),
                F(Festival.LunarNewYear, "Lunar New Year", Season.Spring, "garland_lny", true, true, true, false, false,
                  new Color(1f, 0.35f, 0.25f), new[] { new Color(1f, 0.25f, 0.2f), new Color(1f, 0.82f, 0.25f) }, "lny_lantern_post", "lny_firecrackers", "lny_coin"),
                F(Festival.SummerFireworks, "Summer Fireworks", Season.Summer, "garland_summer", true, true, true, false, false,
                  new Color(1f, 0.85f, 0.5f), new[] { new Color(1f, 0.35f, 0.4f), new Color(0.35f, 0.7f, 1f), new Color(1f, 0.9f, 0.35f), Color.white }, "summer_umbrella", "summer_sandcastle", "summer_icecream"),
                F(Festival.Halloween, "Halloween", Season.Autumn, "garland_halloween", true, false, false, true, true,
                  new Color(1f, 0.55f, 0.15f), new Color[0], "halloween_jack", "halloween_tomb", "halloween_ghost"),
                F(Festival.Diwali, "Diwali", Season.Autumn, "garland_diwali", true, true, true, false, false,
                  new Color(1f, 0.75f, 0.3f), new[] { new Color(1f, 0.8f, 0.25f), new Color(1f, 0.35f, 0.6f), new Color(0.3f, 0.85f, 0.9f) }, "diwali_diya", "diwali_rangoli", "diwali_diya"),
                F(Festival.Christmas, "Christmas", Season.Winter, "garland_xmas", true, true, false, false, false,
                  new Color(1f, 0.9f, 0.6f), new[] { new Color(1f, 0.3f, 0.3f), new Color(0.4f, 1f, 0.5f), new Color(1f, 0.9f, 0.4f) }, "xmas_tree", "xmas_gift_a", "xmas_gift_b", "xmas_gift_c", "xmas_candycane"),
            };
            // the old generated snowman fits Christmas too
            var snowman = AssetDatabase.LoadAssetAtPath<Sprite>($"{Gen}/prop_snowman.png");
            if (snowman != null) { var xm = fest.festivals[5]; xm.groundProps = xm.groundProps.Concat(new[] { snowman }).ToArray(); }
            EditorUtility.SetDirty(fest);
            report.AppendLine("• Festivals: Easter / Lunar New Year · Summer Fireworks · Halloween / Diwali · Christmas (press F to cycle)");

            // ── 4. uniform pixels ──
            var cam = Camera.main;
            if (cam != null)
            {
                var grid = cam.GetComponent<PixelGrid>();
                if (grid == null) grid = Undo.AddComponent<PixelGrid>(cam.gameObject);
                Undo.RecordObject(grid, "Pixel grid");
                grid.pixelsPerUnit = GridPPU;
                grid.enabled = false;   // optional: the art is already sharp; tick it on the Main Camera for a strict one-pixel-size look
                EditorUtility.SetDirty(grid);
            }
            var ui = Find<CrashUI>();
            if (ui != null) { Undo.RecordObject(ui, "Zoom"); ui.gameViewSize = 3.5f; EditorUtility.SetDirty(ui); }
            var juice = cam != null ? cam.GetComponent<CameraJuice>() : null;
            if (juice != null) { Undo.RecordObject(juice, "Zoom"); juice.minSize = 3.5f * 0.88f; EditorUtility.SetDirty(juice); }

            // every spawner row at a whole pixel scale, plants drawn 1 art px = 1/16 unit
#if UNITY_2023_1_OR_NEWER
            var rows = Object.FindObjectsByType<TreeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var rows = Object.FindObjectsOfType<TreeSpawner>(true);
#endif
            foreach (var r in rows)
            {
                Undo.RecordObject(r, "Row scale");
                r.scaleRange = r.row == TreeSpawner.Row.Back ? new Vector2(0.5f, 0.5f) : Vector2.one;
                if (r.row == TreeSpawner.Row.Main && r.prefabs != null)
                    r.prefabs = r.prefabs.Where(p => p != null && !p.name.Contains("FestiveProp")).ToArray();   // festivals handle props now
                EditorUtility.SetDirty(r);
            }
            int rescaled = RescalePlantPrefabs();
            report.AppendLine($"• Sharper art: cat at 4x detail, backgrounds + props at 2x; {rescaled} plant prefab(s) re-scaled");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);
            Msg("Done!\n" + report +
                "\nTips: click < > at the bottom-left to change cat · press F to flip through festivals · N flips seasons.\n" +
                "Keep the Game view Scale slider at 1x for the sharpest picture.\n\nSave with Ctrl+S, then Play.");
        }

        static FestivalSystem.FestivalArt F(Festival f, string name, Season s, string garland, bool dusk, bool fireworks, bool lanterns, bool bats, bool moon,
                                            Color glow, Color[] fwColors, params string[] props)
        {
            return new FestivalSystem.FestivalArt
            {
                festival = f, displayName = name, season = s, garland = S(Fest, garland),
                groundProps = props.Select(p => S(Fest, p)).Where(p => p != null).ToArray(),
                dusk = dusk, fireworks = fireworks, skyLanterns = lanterns, bats = bats, moon = moon,
                glowColor = glow, fireworkColors = fwColors,
            };
        }

        static Sprite[] Frames(string dir, string clip)
        {
            return AssetDatabase.FindAssets(clip + "_", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(clip + "_"))
                .OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
                .Where(s => s != null).ToArray();
        }

        static Sprite S(string dir, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");

        static void ImportFolder(string folder, float ppu, SpriteAlignment align, FilterMode filter)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                Reimport(AssetDatabase.GUIDToAssetPath(guid), ppu, align, filter);
        }

        static void Reimport(string path, float ppu, SpriteAlignment align, FilterMode filter)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = filter;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spritePixelsPerUnit = ppu;
            s.spriteAlignment = (int)align;
            s.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }

        /// <summary>KebapGuy/Scenery plant prefabs: art scale so 1 art pixel = 1/16 unit (same as everything else).</summary>
        static int RescalePlantPrefabs()
        {
            int n = 0;
            foreach (var dir in new[] { "Assets/KittyCrash/Prefabs/KebapGuy", "Assets/KittyCrash/Prefabs/Scenery" })
            {
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    var art = root.transform.Find("Art");
                    var sr = art != null ? art.GetComponent<SpriteRenderer>() : null;
                    if (sr != null && sr.sprite != null)
                    {
                        float s = sr.sprite.pixelsPerUnit / PlantPPU;
                        art.localScale = new Vector3(s, s, 1f);
                        art.localPosition = new Vector3(0f, -sr.sprite.bounds.min.y * s, 0f);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        n++;
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return n;
        }

        static T Find<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Skins & Festivals", m, "OK");
    }
}
