using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Install Festive Extras
    /// Adds the generated pixel art into the CURRENT scene without wiping anything:
    ///   • parallax background (clouds, Himalaya-style mountains, hills) with 4 seasonal versions that crossfade
    ///   • seasonal ground tiles (grass → autumn grass → snow)
    ///   • festival costumes on the cat (flower crown / sunglasses / witch hat + mask / Santa hat) + black Halloween fur
    ///   • real distraction sprites (fish, Z, yarn, butterfly, laser, box)
    ///   • a FestiveProp prefab in the scenery (Holi colours / sunflower / jack-o'-lantern / snowman)
    ///   • season names (Holi, Summer Fun, Halloween, Christmas) + weather sprites (petals, pollen, leaves, snow)
    /// Safe to run again — it replaces what it added before. Ctrl+Z undoes it.
    /// </summary>
    public static class FestiveExtrasInstaller
    {
        const string ArtDir = "Assets/KittyCrash/Art/Generated";
        const string PrefabDir = "Assets/KittyCrash/Prefabs";
        const float GroundTop = -2.5f;

        [MenuItem("Kitty Crash/Install Festive Extras (background, costumes, props, distractions)")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Stop Play mode first (press the ▶ button), then run this again.\nChanges made during Play mode get thrown away.", "OK");
                return;
            }
            var walker = Find<CatWalker>();
            var seasons = Find<SeasonSystem>();
            if (walker == null || seasons == null)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Open the KittyCrash scene first (Assets/KittyCrash/Scenes).", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(ArtDir))
            {
                EditorUtility.DisplayDialog("Kitty Crash", $"Art folder not found: {ArtDir}\nCopy the Art/Generated folder from the kit first.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Install Festive Extras");
            int undoGroup = Undo.GetCurrentGroup();

            ConfigureImports();
            InstallBackground();
            InstallGround();
            InstallCostumes(walker);
            InstallDistractions(walker);
            InstallFestiveProps();
            InstallSeasonSettings(seasons);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(walker.gameObject.scene);
            EditorUtility.DisplayDialog("Kitty Crash",
                "Festive extras installed!\n\n" +
                "• Save the scene (Ctrl+S) and press Play. Press N to flip through seasons.\n" +
                "• Costumes live under Cat › Visual › Costume_<Season>. In edit mode all four show at once — " +
                "select a hat/mask and move/scale it in the Scene view to sit on your cat's head.\n" +
                "• Halloween black fur: Cat › Cat Season Look.",
                "Meow");
        }

        // ── imports ──────────────────────────────

        static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int size = tex != null ? Mathf.Max(tex.width, tex.height) : 16;

                int ppu = 16;
                var align = SpriteAlignment.Center;
                var mesh = SpriteMeshType.Tight;
                if (name.StartsWith("costume_")) ppu = 32;
                if (name.StartsWith("weather_")) ppu = size;                     // 1 sprite = 1 unit; WeatherSystem scales it
                if (name.StartsWith("prop_") || name.StartsWith("bg_")) align = SpriteAlignment.BottomCenter;
                if (name.StartsWith("bg_") || name.StartsWith("ground_")) mesh = SpriteMeshType.FullRect;

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;

                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.spriteAlignment = (int)align;
                s.spriteMeshType = mesh;
                s.spritePixelsPerUnit = ppu;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        // ── background ───────────────────────────

        static void InstallBackground()
        {
            var old = GameObject.Find("Background");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Background");
            Undo.RegisterCreatedObjectUndo(root, "Background");

            Layer(root.transform, "Clouds", "bg_clouds", "bg_clouds", "bg_clouds", "bg_clouds", follow: 0.95f, baseline: 0.6f, order: -45, drift: -0.12f);
            Layer(root.transform, "Mountains", "bg_mountains_spring", "bg_mountains_summer", "bg_mountains_autumn", "bg_mountains_winter", follow: 0.9f, baseline: GroundTop + 0.6f, order: -40);
            Layer(root.transform, "Hills", "bg_hills_spring", "bg_hills_summer", "bg_hills_autumn", "bg_hills_winter", follow: 0.6f, baseline: GroundTop - 0.2f, order: -30);
        }

        static void Layer(Transform parent, string name, string sp, string su, string au, string wi, float follow, float baseline, int order, float drift = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<ParallaxLayer>();
            l.spring = S(sp); l.summer = S(su); l.autumn = S(au); l.winter = S(wi);
            l.follow = follow; l.baselineY = baseline; l.sortingOrder = order; l.drift = drift;
        }

        // ── ground ───────────────────────────────

        static void InstallGround()
        {
            var looper = Find<GroundLooper>();
            if (looper == null || looper.segments == null) return;
            foreach (var seg in looper.segments)
            {
                if (seg == null) continue;
                var sr = seg.GetComponent<SpriteRenderer>();
                var ss = seg.GetComponent<SeasonalSprite>();
                if (sr == null) continue;
                if (ss == null) ss = Undo.AddComponent<SeasonalSprite>(seg.gameObject);

                Undo.RecordObject(seg, "Ground");
                Undo.RecordObject(sr, "Ground");
                Undo.RecordObject(ss, "Ground");
                seg.localScale = Vector3.one;
                sr.sprite = S("ground_summer");
                sr.color = Color.white;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(looper.segmentWidth + 0.05f, 4f);
                ss.tintRole = SeasonalSprite.TintRole.None;
                ss.shadeVariation = 0f;
                ss.spring = S("ground_spring"); ss.summer = S("ground_summer"); ss.autumn = S("ground_autumn"); ss.winter = S("ground_winter");
                EditorUtility.SetDirty(sr);
                EditorUtility.SetDirty(ss);
            }
        }

        // ── cat costumes + fur ───────────────────

        static void InstallCostumes(CatWalker walker)
        {
            var visual = walker.visual;
            if (visual == null) return;

            foreach (Transform child in visual.Cast<Transform>().ToList())
                if (child.name.StartsWith("Costume_")) Undo.DestroyObjectImmediate(child.gameObject);

            var catSR = visual.GetComponent<SpriteRenderer>();
            int order = (catSR != null ? catSR.sortingOrder : 10) + 2;

            // Guess the head position from the current sprite's bounds (cat faces right).
            Bounds b = catSR != null && catSR.sprite != null ? catSR.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
            var headTop = new Vector3(b.center.x + b.extents.x * 0.45f, b.min.y + b.size.y * 0.8f, 0f);
            var eyes = new Vector3(b.center.x + b.extents.x * 0.55f, b.min.y + b.size.y * 0.62f, 0f);

            Costume(visual, Season.Spring, order, ("FlowerCrown", "costume_flower_crown", headTop));
            Costume(visual, Season.Summer, order, ("Sunglasses", "costume_sunglasses", eyes));
            Costume(visual, Season.Autumn, order, ("WitchHat", "costume_witch_hat", headTop + new Vector3(0f, 0.08f, 0f)),
                                                  ("HalloweenMask", "costume_halloween_mask", eyes));
            Costume(visual, Season.Winter, order, ("SantaHat", "costume_santa_hat", headTop));

            var look = walker.GetComponent<CatSeasonLook>();
            if (look == null) look = Undo.AddComponent<CatSeasonLook>(walker.gameObject);
            Undo.RecordObject(look, "Cat look");
            look.catRenderer = catSR;
            EditorUtility.SetDirty(look);
        }

        static void Costume(Transform visual, Season season, int order, params (string name, string sprite, Vector3 pos)[] parts)
        {
            var group = new GameObject("Costume_" + season);
            Undo.RegisterCreatedObjectUndo(group, "Costume");
            group.transform.SetParent(visual, false);
            group.AddComponent<SeasonalGroup>().season = season;

            foreach (var p in parts)
            {
                var go = new GameObject(p.name);
                go.transform.SetParent(group.transform, false);
                go.transform.localPosition = p.pos;
                go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = S(p.sprite);
                sr.sortingOrder = order;
            }
        }

        // ── distractions ─────────────────────────

        static void InstallDistractions(CatWalker walker)
        {
            Undo.RecordObject(walker, "Distraction sprites");
            // Order matches DistractionType: Fish, Sleep, Yarn, Butterfly, Laser, CardboardBox
            walker.distractionSprites = new[]
            {
                S("distraction_fish"), S("distraction_sleep"), S("distraction_yarn"),
                S("distraction_butterfly"), S("distraction_laser"), S("distraction_box"),
            };
            EditorUtility.SetDirty(walker);
        }

        // ── festive props in the scenery ─────────

        static void InstallFestiveProps()
        {
            var root = new GameObject("FestiveProp");
            var props = new (Season season, string sprite)[]
            {
                (Season.Spring, "prop_holi"), (Season.Summer, "prop_sunflower"),
                (Season.Autumn, "prop_pumpkin"), (Season.Winter, "prop_snowman"),
            };
            foreach (var p in props)
            {
                var g = new GameObject(p.season + "Prop");
                g.transform.SetParent(root.transform, false);
                g.AddComponent<SeasonalGroup>().season = p.season;
                var art = new GameObject("Art");
                art.transform.SetParent(g.transform, false);
                var sr = art.AddComponent<SpriteRenderer>();
                sr.sprite = S(p.sprite);
                sr.sortingOrder = -7; // in front of trees, behind the cat
            }
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/FestiveProp.prefab");
            Object.DestroyImmediate(root);

            var spawner = Find<TreeSpawner>();
            if (spawner == null || prefab == null) return;
            Undo.RecordObject(spawner, "Festive props");
            var list = new List<GameObject>(spawner.prefabs ?? new GameObject[0]);
            list.RemoveAll(x => x == null);
            if (list.Count(x => x == prefab) < 2) { list.Add(prefab); list.Add(prefab); }
            spawner.prefabs = list.ToArray();
            EditorUtility.SetDirty(spawner);
        }

        // ── season settings ──────────────────────

        static void InstallSeasonSettings(SeasonSystem seasons)
        {
            Undo.RecordObject(seasons, "Season settings");
            foreach (var t in seasons.themes)
            {
                if (t == null) continue;
                t.weatherColor = Color.white; // the sprites carry their own colours now
                switch (t.season)
                {
                    case Season.Spring: t.festiveName = "Holi"; t.weatherSprite = S("weather_petal"); t.weatherSize = 0.22f; t.weatherSpin = 90f; break;
                    case Season.Summer: t.festiveName = "Summer Fun"; t.weatherSprite = S("weather_pollen"); t.weatherSize = 0.12f; t.weatherSpin = 0f; break;
                    case Season.Autumn: t.festiveName = "Halloween"; t.weatherSprite = S("weather_leaf"); t.weatherSize = 0.3f; t.weatherSpin = 140f; break;
                    case Season.Winter: t.festiveName = "Christmas"; t.weatherSprite = S("weather_snow"); t.weatherSize = 0.16f; t.weatherSpin = 30f; break;
                }
            }
            EditorUtility.SetDirty(seasons);
        }

        static T Find<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
