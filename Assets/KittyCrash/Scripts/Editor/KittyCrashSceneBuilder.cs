using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menus:
    ///   Kitty Crash/Build Prototype Scene        – full playable greybox wired to Local + API backends
    ///   Kitty Crash/Fix Pixel Art Import Settings – select textures first; sets Point filter, no compression
    /// </summary>
    public static class KittyCrashSceneBuilder
    {
        const string Root = "Assets/KittyCrash";
        const string ArtDir = Root + "/Art";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/KittyCrash.unity";
        const float GroundTop = -2.5f;
        const float SegmentWidth = 30f;

        // Sorting orders (gaps of 2 so SeasonalSprite's crossfade layer fits at +1)
        const int OrderGround = -20, OrderTrunk = -12, OrderLeaves = -10, OrderBush = -8,
                  OrderCat = 10, OrderWeather = 20, OrderFestive = 30, OrderOverlay = 100;

        [MenuItem("Kitty Crash/Build Prototype Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Stop Play mode first (press the ▶ button), then run this again.\nChanges made during Play mode get thrown away.", "OK");
                return;
            }
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Kitty Crash", "KittyCrash.unity already exists. Rebuild it and its prefabs? (Your Cat frames/settings in the scene will be lost.)", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);

            AllowHttp();
            var square = GetOrCreateSquareSprite();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Camera + brightness overlay + festive decor ──
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(3f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
            var follow = camGO.AddComponent<CameraFollow>();

            var overlay = Quad("BrightnessOverlay", camGO.transform, square, new Vector3(0f, 0f, 1f), new Vector2(60f, 30f), new Color(0f, 0f, 0f, 0f), OrderOverlay);
            BuildFestiveDecor(camGO.transform, square);

            // ── Managers ──
            var managers = new GameObject("Managers");
            var local = managers.AddComponent<LocalBackend>();
            var api = managers.AddComponent<ApiBackend>();
            var round = managers.AddComponent<RoundManager>();
            round.localBackend = local;
            round.apiBackend = api;
            round.useApi = false;
            var seasons = managers.AddComponent<SeasonSystem>();
            seasons.brightnessOverlay = overlay;
            var hud = managers.AddComponent<CrashHUD>();
            hud.round = round;

            // ── Weather ──
            var weather = new GameObject("Weather").AddComponent<WeatherSystem>();
            weather.defaultSprite = square;
            weather.sortingOrder = OrderWeather;

            // ── Ground ──
            var groundRoot = new GameObject("Ground");
            var looper = groundRoot.AddComponent<GroundLooper>();
            looper.segmentWidth = SegmentWidth;
            var segs = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var seg = Quad("Segment_" + i, groundRoot.transform, square,
                    new Vector3(-SegmentWidth + i * SegmentWidth, GroundTop - 2f, 0f), new Vector2(SegmentWidth + 0.05f, 4f), Color.white, OrderGround);
                var ss = seg.gameObject.AddComponent<SeasonalSprite>();
                ss.tintRole = SeasonalSprite.TintRole.Ground;
                ss.shadeVariation = 0f;
                segs.Add(seg.transform);
            }
            looper.segments = segs.ToArray();

            // ── Scenery ──
            var tree = SavePrefab(BuildTree(square), "Tree");
            var bush = SavePrefab(BuildBush(square), "Bush");
            var scenery = new GameObject("Scenery").AddComponent<TreeSpawner>();
            scenery.prefabs = new[] { tree, tree, bush };
            scenery.groundY = GroundTop;

            // ── Cat ──
            var catGO = new GameObject("Cat");
            catGO.transform.position = new Vector3(0f, GroundTop, 0f);
            var visual = Quad("Visual", catGO.transform, square, new Vector3(0f, 0.35f, 0f), new Vector2(0.9f, 0.7f), new Color(1f, 0.6f, 0.2f), OrderCat);
            var anim = visual.gameObject.AddComponent<CatSpriteAnimator>();
            var walker = catGO.AddComponent<CatWalker>();
            walker.visual = visual.transform;
            walker.spriteAnimator = anim;
            walker.placeholderSprite = square;

            follow.target = catGO.transform;
            hud.cat = walker;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = catGO;

            EditorUtility.DisplayDialog("Kitty Crash",
                "Scene built: " + ScenePath + "\n\n" +
                "Plays OFFLINE by default (same rules as the server).\n" +
                "To use your API: run the server, then tick 'Use API' in the game HUD (or Managers › RoundManager).\n\n" +
                "Space = bet / cash out.  N = next season.",
                "Let's go");
        }

        // ── builders ─────────────────────────────

        static GameObject BuildTree(Sprite square)
        {
            var root = new GameObject("Tree");
            var trunk = Quad("Trunk", root.transform, square, new Vector3(0f, 0.9f, 0f), new Vector2(0.35f, 1.8f), Color.white, OrderTrunk);
            trunk.gameObject.AddComponent<SeasonalSprite>().tintRole = SeasonalSprite.TintRole.Trunk;
            var leaves = Quad("Leaves", root.transform, square, new Vector3(0f, 2.3f, 0f), new Vector2(1.7f, 1.5f), Color.white, OrderLeaves);
            leaves.gameObject.AddComponent<SeasonalSprite>().tintRole = SeasonalSprite.TintRole.Leaves;
            return root;
        }

        static GameObject BuildBush(Sprite square)
        {
            var root = new GameObject("Bush");
            var leaves = Quad("Leaves", root.transform, square, new Vector3(0f, 0.3f, 0f), new Vector2(1.1f, 0.6f), Color.white, OrderBush);
            leaves.gameObject.AddComponent<SeasonalSprite>().tintRole = SeasonalSprite.TintRole.Leaves;
            return root;
        }

        /// <summary>Placeholder festive bunting across the top of the screen, one row per season, fading with it.</summary>
        static void BuildFestiveDecor(Transform cam, Sprite square)
        {
            var root = new GameObject("FestiveDecor").transform;
            root.SetParent(cam, false);
            root.localPosition = new Vector3(0f, 4.4f, 5f);

            var palettes = new[]
            {
                new[] { new Color(1f,0.3f,0.6f), new Color(1f,0.85f,0.1f), new Color(0.2f,0.7f,1f), new Color(0.4f,0.9f,0.3f) },  // spring – Holi colours
                new[] { new Color(1f,0.8f,0.2f), new Color(1f,0.6f,0.1f) },                                                          // summer
                new[] { new Color(1f,0.65f,0.1f), new Color(0.95f,0.3f,0.1f) },                                                      // autumn – Tihar lights
                new[] { new Color(0.9f,0.15f,0.15f), new Color(0.15f,0.6f,0.25f), Color.white },                                    // winter
            };

            for (int s = 0; s < 4; s++)
            {
                var g = new GameObject(((Season)s) + "Festive");
                g.transform.SetParent(root, false);
                g.AddComponent<SeasonalGroup>().season = (Season)s;
                for (int i = 0; i < 11; i++)
                {
                    float x = -8f + i * 1.6f;
                    var c = palettes[s][i % palettes[s].Length];
                    Quad("Flag_" + i, g.transform, square, new Vector3(x, -Mathf.Abs(Mathf.Sin(i * 0.9f)) * 0.25f, 0f), new Vector2(0.35f, 0.45f), c, OrderFestive);
                }
            }
        }

        static SpriteRenderer Quad(string name, Transform parent, Sprite sprite, Vector3 localPos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AllowHttp()
        {
            // Unity 2022+ blocks plain http:// by default; the local FastAPI server is http.
#if UNITY_2022_1_OR_NEWER
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
#endif
        }

        static Sprite GetOrCreateSquareSprite()
        {
            string path = ArtDir + "/Placeholder_Square.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 4;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void AddSceneToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) return;
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ── pixel art import helper ──────────────────

        [MenuItem("Kitty Crash/Fix Pixel Art Import Settings (selected textures)")]
        public static void FixPixelArt()
        {
            int n = 0;
            foreach (var obj in Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets))
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
                n++;
            }
            EditorUtility.DisplayDialog("Kitty Crash", n > 0
                ? $"Fixed {n} texture(s): Sprite, Point filter, no compression, no mipmaps.\nSprite Mode / Pixels Per Unit / slicing were NOT changed."
                : "Select one or more textures (or a folder) in the Project window first.", "OK");
        }
    }
}
