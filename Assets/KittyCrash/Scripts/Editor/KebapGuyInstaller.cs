using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Use KebapGuy Plant Pack (Scenery + Background)
    ///
    /// One click to dress the whole path with the Kebap Guy "Flower &amp; Plant Pack":
    ///   1. Finds every PNG in a folder whose path contains "KebapGuy"/"Kebab" (or the folder you selected
    ///      in the Project window), fixes pixel-art import settings (Point, 16 PPU, no compression).
    ///   2. Slices each sheet automatically: finds every plant by its transparent gaps, and detects
    ///      ANIMATION FRAMES (neighbouring cells with the same colours) so swaying flowers really sway.
    ///   3. SEASONS: files/folders named spring / summer / autumn (fall) / winter are matched up, so the same
    ///      plant swaps to its seasonal version with the game's crossfade.
    ///   4. Makes one prefab per plant in Assets/KittyCrash/Prefabs/KebapGuy and fills three depth rows:
    ///        Plants_Back  – small, darker, behind everything   (background)
    ///        Scenery      – the main row (trees/bushes/flowers)
    ///      (light touch: sparse and small, nothing in front of the cat)
    ///   Solid squares (ground tiles) and preview images are skipped and listed in the report.
    /// Re-run it any time (it rebuilds its own prefabs). Ctrl+Z undoes the scene changes.
    /// </summary>
    public static class KebapGuyInstaller
    {
        const string OutDir = "Assets/KittyCrash/Prefabs/KebapGuy";
        const float UnitsPer16Px = 1.5f;   // same scale as SceneryMaker: a 16px flower is 1.5 units tall
        const int PPU = 16;
        const int MaxPlants = 120;

        static readonly string[] SkipWords = { "preview", "cover", "banner", "thumb", "thumbnail", "showcase", "example", "mockup", "screenshot", "promo", "logo", "readme" };

        class Cell
        {
            public RectInt r;
            public Dictionary<int, float> hist;
            public int count;
            public bool tile;
            public bool[] mask; // opaque pixels, row-major inside r
        }

        class PlantSet
        {
            public string name;
            public int heightPx;
            public Sprite[] any;
            public readonly Dictionary<Season, Sprite[]> seasons = new Dictionary<Season, Sprite[]>();
            public Sprite[] Base => any ?? (seasons.TryGetValue(Season.Summer, out var s) ? s : seasons.TryGetValue(Season.Spring, out s) ? s : seasons.Values.First());
            public bool Animated => (any != null && any.Length > 1) || seasons.Values.Any(f => f.Length > 1);
        }

        [MenuItem("Kitty Crash/Use KebapGuy Plant Pack (Scenery + Background)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
            var main = TreeSpawner.FindMain();
            if (main == null) { Msg("Open the KittyCrash scene first (it needs the Scenery object)."); return; }

            var textures = FindTextures(out string where);
            if (textures.Count == 0)
            {
                Msg("Couldn't find the plant pack.\n\nLooked in: " + where + "\n\n" +
                    "Fix: in the Project window click the pack's folder (the one with the PNGs) so it's selected, then run this menu again.");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex("Kitty Crash — KebapGuy plants",
                $"Found {textures.Count} image(s) in {where}.\n\n" +
                "Slice them automatically? (Recommended — finds each plant and its animation frames.)\n\n" +
                "Choose 'Keep my slicing' if you already sliced the sheets yourself in the Sprite Editor.",
                "Auto-slice (recommended)", "Cancel", "Keep my slicing");
            if (choice == 1) return;
            bool reslice = choice == 0;

            var report = new StringBuilder();
            var groups = new Dictionary<string, PlantSet>();
            int skippedPreview = 0, tiles = 0, sheets = 0;

            try
            {
                for (int i = 0; i < textures.Count; i++)
                {
                    string path = textures[i];
                    EditorUtility.DisplayProgressBar("KebapGuy plants", Path.GetFileName(path), (float)i / textures.Count);
                    var res = ProcessTexture(path, reslice, ref skippedPreview, ref tiles);
                    if (res == null) continue;
                    sheets++;

                    Season? season = SeasonOf(path, out string baseKey);
                    for (int p = 0; p < res.Count; p++)
                    {
                        var (frames, hPx) = res[p];
                        if (frames.Length == 0) continue;
                        string key = season.HasValue ? baseKey + "#" + p : path + "#" + p;
                        if (!groups.TryGetValue(key, out var g))
                        {
                            g = new PlantSet { name = $"{CleanName(Path.GetFileNameWithoutExtension(path))}_{p + 1:00}", heightPx = hPx };
                            groups[key] = g;
                        }
                        if (season.HasValue) g.seasons[season.Value] = frames;
                        else g.any = frames;
                        g.heightPx = Mathf.Max(g.heightPx, hPx);
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            var plants = groups.Values.Where(g => g.any != null || g.seasons.Count > 0).ToList();
            if (plants.Count == 0)
            {
                Msg($"Read {sheets} sheet(s) but found no plants (skipped {tiles} solid tile(s), {skippedPreview} preview image(s)).\n\n" +
                    "If the pack's sheets are sliced already, run again and choose 'Keep my slicing'.");
                return;
            }
            if (plants.Count > MaxPlants)
            {
                report.AppendLine($"• {plants.Count} plants found — using the first {MaxPlants} (spread across all sheets)");
                float step = plants.Count / (float)MaxPlants;
                plants = Enumerable.Range(0, MaxPlants).Select(k => plants[Mathf.FloorToInt(k * step)]).ToList();
            }

            // ── prefabs ──
            if (AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.DeleteAsset(OutDir);
            Directory.CreateDirectory(OutDir);
            AssetDatabase.Refresh();

            var all = new List<GameObject>();
            var small = new List<GameObject>();
            var tall = new List<GameObject>();
            int animated = 0, seasonal = 0;
            foreach (var pl in plants)
            {
                var prefab = MakePrefab(pl);
                if (prefab == null) continue;
                all.Add(prefab);
                if (pl.heightPx <= 24) small.Add(prefab);
                if (pl.heightPx >= 32) tall.Add(prefab);
                if (pl.Animated) animated++;
                if (pl.seasons.Count > 0) seasonal++;
            }

            // ── rows ──
            Undo.SetCurrentGroupName("KebapGuy plants");
            int undo = Undo.GetCurrentGroup();
            float groundY = main.groundY;

            // LIGHT TOUCH: just a sprinkle of plants, small enough to sit next to the cat, lots of open ground.
            Undo.RecordObject(main, "Scenery");
            var keep = (main.prefabs ?? new GameObject[0]).Where(p => p != null && p.name.Contains("FestiveProp")).ToList();
            main.prefabs = all.Concat(keep).ToArray();
            main.poolSize = 8;
            main.overrideSorting = false;
            main.brightness = 1f;
            main.randomFlip = true;
            main.yJitter = 0f;
            main.spacing = new Vector2(6f, 14f);        // one plant every 6–14 units
            main.scaleRange = new Vector2(0.5f, 0.75f); // about half the old size
            EditorUtility.SetDirty(main);

            var back = Row(TreeSpawner.Row.Back, "Plants_Back", main);
            back.prefabs = (small.Count > 0 ? small : all).ToArray();
            back.poolSize = 10;
            back.groundY = groundY + 0.3f;
            back.spacing = new Vector2(4f, 10f);
            back.scaleRange = new Vector2(0.35f, 0.5f);
            back.overrideSorting = true;
            back.sortingOrder = -16;     // above ground (-20) and paw prints (-19), behind the main row (-12/-8)
            back.brightness = 0.6f;
            back.yJitter = 0.04f;
            back.randomFlip = true;
            EditorUtility.SetDirty(back);

            // No plants in front of the cat (that's what made it look cluttered). Remove the row if an earlier run made it.
#if UNITY_2023_1_OR_NEWER
            var rows = Object.FindObjectsByType<TreeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var rows = Object.FindObjectsOfType<TreeSpawner>(true);
#endif
            foreach (var r in rows) if (r.row == TreeSpawner.Row.Front) Undo.DestroyObjectImmediate(r.gameObject);

            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
            AssetDatabase.SaveAssets();

            Msg($"Done! {all.Count} plant prefab(s) from {sheets} sheet(s)\n" +
                $"• {animated} animated, {seasonal} with seasonal versions\n" +
                (tiles > 0 ? $"• {tiles} solid square tile(s) skipped (ground tiles, not plants)\n" : "") +
                (skippedPreview > 0 ? $"• {skippedPreview} preview/large image(s) skipped\n" : "") +
                report +
                $"\nLight touch: a few small plants on the path + a faint background row (kept {keep.Count} festive slot(s)).\n\n" +
                "Want even fewer? Scenery / Plants_Back › Spacing: raise both numbers. Smaller? lower Scale Range.\n" +
                "Prefabs are in Assets/KittyCrash/Prefabs/KebapGuy — delete any you don't like from the row's Prefabs list.\n\nSave with Ctrl+S, then Play.");
        }

        // ───────────────────────────────── discovery ─────────────────────────────────

        static List<string> FindTextures(out string where)
        {
            var folders = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(AssetDatabase.IsValidFolder).ToArray();
            IEnumerable<string> paths;
            if (folders.Length > 0)
            {
                paths = AssetDatabase.FindAssets("t:Texture2D", folders).Select(AssetDatabase.GUIDToAssetPath);
                where = string.Join(", ", folders);
            }
            else
            {
                paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => { var l = p.ToLowerInvariant(); return l.Contains("kebap") || l.Contains("kebab"); });
                where = "folders named KebapGuy / Kebab";
            }
            return paths.Distinct()
                .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) && !p.StartsWith("Assets/KittyCrash/"))
                .OrderBy(p => p).ToList();
        }

        static Season? SeasonOf(string path, out string baseKey)
        {
            var tokens = Regex.Split(path.ToLowerInvariant(), "[^a-z0-9]+").Where(t => t.Length > 0).ToList();
            Season? found = null;
            var rest = new List<string>();
            foreach (var t in tokens)
            {
                Season? s = t == "spring" ? Season.Spring : t == "summer" ? Season.Summer
                          : t == "autumn" || t == "fall" ? Season.Autumn : t == "winter" ? Season.Winter : (Season?)null;
                if (s.HasValue && !found.HasValue) found = s;
                else rest.Add(t);
            }
            baseKey = string.Join("_", rest);
            return found;
        }

        static string CleanName(string s) => Regex.Replace(s, "[^A-Za-z0-9_-]+", "_");

        // ───────────────────────────────── slicing ─────────────────────────────────

        /// <summary>Returns one entry per plant: its frames (1 = static) and its height in pixels.</summary>
        static List<(Sprite[] frames, int hPx)> ProcessTexture(string path, bool reslice, ref int skippedPreview, ref int tiles)
        {
            string lname = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (SkipWords.Any(w => lname.Contains(w))) { skippedPreview++; return null; }

            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!raw.LoadImage(File.ReadAllBytes(path))) return null;
                int W = raw.width, H = raw.height;
                if (W > 1024 || H > 1024) { skippedPreview++; return null; }
                var px = raw.GetPixels32();

                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) return null;

                bool hadSlices = imp.spriteImportMode == SpriteImportMode.Multiple &&
                                 AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Any();
                PixelArtSettings(imp, W, H, keepMode: !reslice && hadSlices);
                imp.SaveAndReimport();

                // Cells grouped into rows (top → bottom), each row left → right.
                List<List<Cell>> bands;
                bool useExisting = !reslice && hadSlices;
                if (useExisting) bands = BandsFromExisting(path, px, W);
                else bands = DetectBands(px, W, H);

                // Plants = runs of look-alike neighbours (animation frames).
                var plantRects = new List<(List<RectInt> frames, int hPx)>();
                foreach (var band in bands)
                {
                    var cells = band.Where(c => !c.tile).ToList();
                    tiles += band.Count - cells.Count;
                    var run = new List<Cell>();
                    foreach (var c in cells)
                    {
                        if (run.Count > 0 && !Similar(run[run.Count - 1], c)) { plantRects.Add(FramesFor(run, W, H, useExisting)); run = new List<Cell>(); }
                        run.Add(c);
                    }
                    if (run.Count > 0) plantRects.Add(FramesFor(run, W, H, useExisting));
                }
                plantRects.RemoveAll(pr => pr.hPx < 4); // stray pixels, not plants
                if (plantRects.Count == 0) return new List<(Sprite[], int)>();

                string tex = CleanName(Path.GetFileNameWithoutExtension(path));
                if (!useExisting)
                {
                    var rects = new List<SpriteRect>();
                    for (int p = 0; p < plantRects.Count; p++)
                        for (int f = 0; f < plantRects[p].frames.Count; f++)
                        {
                            var r = plantRects[p].frames[f];
                            rects.Add(new SpriteRect
                            {
                                name = $"{tex}_{p + 1:00}_{f + 1:00}",
                                spriteID = GUID.Generate(),
                                rect = new Rect(r.x, r.y, r.width, r.height),
                                alignment = SpriteAlignment.BottomCenter,
                                pivot = new Vector2(0.5f, 0f),
                            });
                        }
                    imp.spriteImportMode = SpriteImportMode.Multiple;
                    imp.SaveAndReimport();
                    var factory = new SpriteDataProviderFactories();
                    factory.Init();
                    var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
                    dp.InitSpriteEditorDataProvider();
                    dp.SetSpriteRects(rects.ToArray());
#if UNITY_2021_2_OR_NEWER
                    var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
                    if (ids != null) ids.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());
#endif
                    dp.Apply();
                    imp.SaveAndReimport();
                }

                // Map rects → sprites.
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
                var result = new List<(Sprite[], int)>();
                foreach (var (frames, hPx) in plantRects)
                {
                    var list = new List<Sprite>();
                    foreach (var r in frames)
                    {
                        var sp = sprites.FirstOrDefault(s => Mathf.RoundToInt(s.rect.x) == r.x && Mathf.RoundToInt(s.rect.y) == r.y &&
                                                             Mathf.RoundToInt(s.rect.width) == r.width && Mathf.RoundToInt(s.rect.height) == r.height);
                        if (sp != null) list.Add(sp);
                    }
                    if (list.Count > 0) result.Add((list.ToArray(), hPx));
                }
                return result;
            }
            finally { Object.DestroyImmediate(raw); }
        }

        static void PixelArtSettings(TextureImporter imp, int W, int H, bool keepMode)
        {
            imp.textureType = TextureImporterType.Sprite;
            if (!keepMode) imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.maxTextureSize = Mathf.Max(imp.maxTextureSize, Mathf.NextPowerOfTwo(Mathf.Max(W, H)));
        }

        static bool Op(Color32[] px, int W, int x, int y) => px[y * W + x].a > 16;

        /// <summary>Splits the sheet at fully transparent rows, then each row band at transparent columns.</summary>
        static List<List<Cell>> DetectBands(Color32[] px, int W, int H)
        {
            var bands = new List<List<Cell>>();
            var rowHas = new bool[H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (Op(px, W, x, y)) { rowHas[y] = true; break; }

            int yTop = H - 1;
            while (yTop >= 0)
            {
                if (!rowHas[yTop]) { yTop--; continue; }
                int yBot = yTop;
                while (yBot - 1 >= 0 && rowHas[yBot - 1]) yBot--;
                // band = rows yBot..yTop
                var cells = new List<RectInt>();
                int x = 0;
                while (x < W)
                {
                    if (!ColHas(px, W, x, yBot, yTop)) { x++; continue; }
                    int x0 = x;
                    while (x < W && ColHas(px, W, x, yBot, yTop)) x++;
                    cells.Add(Trim(px, W, new RectInt(x0, yBot, x - x0, yTop - yBot + 1)));
                }
                cells = SplitWide(px, W, MergeSpecks(px, W, cells));
                bands.Add(cells.Where(c => c.width > 0).Select(c => MakeCell(px, W, c)).ToList());
                yTop = yBot - 1;
            }
            return bands;
        }

        static bool ColHas(Color32[] px, int W, int x, int y0, int y1)
        {
            for (int y = y0; y <= y1; y++) if (Op(px, W, x, y)) return true;
            return false;
        }

        static RectInt Trim(Color32[] px, int W, RectInt r)
        {
            int xMin = int.MaxValue, xMax = -1, yMin = int.MaxValue, yMax = -1;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    if (Op(px, W, x, y)) { xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x); yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y); }
            return xMax < 0 ? new RectInt(r.x, r.y, 0, 0) : new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        /// <summary>Tiny bits (a loose petal, a pollen pixel) join the nearest cell instead of becoming a "plant".</summary>
        static List<RectInt> MergeSpecks(Color32[] px, int W, List<RectInt> cells)
        {
            var list = new List<RectInt>(cells);
            bool changed = true;
            while (changed && list.Count > 1)
            {
                changed = false;
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    if (c.width > 3 && c.height > 3) continue;
                    int best = -1, bestGap = 4;
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (j == i) continue;
                        int gap = list[j].xMin >= c.xMax ? list[j].xMin - c.xMax : c.xMin - list[j].xMax;
                        if (gap < bestGap) { bestGap = gap; best = j; }
                    }
                    if (best >= 0)
                    {
                        var o = list[best];
                        int x0 = Mathf.Min(o.xMin, c.xMin), y0 = Mathf.Min(o.yMin, c.yMin);
                        list[best] = new RectInt(x0, y0, Mathf.Max(o.xMax, c.xMax) - x0, Mathf.Max(o.yMax, c.yMax) - y0);
                    }
                    list.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
            return list.OrderBy(r => r.x).ToList();
        }

        /// <summary>Frames drawn edge-to-edge (no gap) arrive as one wide cell: cut them on the sheet's grid.</summary>
        static List<RectInt> SplitWide(Color32[] px, int W, List<RectInt> cells)
        {
            var outList = new List<RectInt>();
            int[] grids = { 16, 24, 32, 48, 64, 96, 128 };
            foreach (var c in cells)
            {
                if (c.width < c.height * 1.8f || c.width < 24) { outList.Add(c); continue; }
                int g = 0;
                foreach (var cand in grids.Where(k => W % k == 0 && k < c.width).OrderBy(k => Mathf.Abs(k - c.height)))
                {
                    int pieces = 0;
                    for (int gx = (c.xMin / cand) * cand; gx < c.xMax; gx += cand)
                        if (Trim(px, W, new RectInt(gx, c.yMin, cand, c.height)).width > 0) pieces++;
                    if (pieces >= 2) { g = cand; break; }
                }
                if (g == 0) { outList.Add(c); continue; }
                for (int gx = (c.xMin / g) * g; gx < c.xMax; gx += g)
                {
                    var piece = Trim(px, W, new RectInt(gx, c.yMin, Mathf.Min(g, W - gx), c.height));
                    if (piece.width > 0) outList.Add(piece);
                }
            }
            return outList;
        }

        static Cell MakeCell(Color32[] px, int W, RectInt r)
        {
            var h = new Dictionary<int, float>();
            int n = 0;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    var c = px[y * W + x];
                    if (c.a <= 16) continue;
                    int k = ((c.r >> 4) << 8) | ((c.g >> 4) << 4) | (c.b >> 4);
                    h.TryGetValue(k, out float v);
                    h[k] = v + 1f;
                    n++;
                }
            if (n > 0) foreach (var k in h.Keys.ToList()) h[k] /= n;
            float fill = r.width * r.height > 0 ? n / (float)(r.width * r.height) : 0f;
            var mask = new bool[Mathf.Max(0, r.width * r.height)];
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                    mask[y * r.width + x] = Op(px, W, r.xMin + x, r.yMin + y);
            return new Cell { r = r, hist = h, count = n, mask = mask, tile = fill > 0.92f && r.width >= 12 && r.height >= 12 };
        }

        static List<List<Cell>> BandsFromExisting(string path, Color32[] px, int W)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));
            dp.InitSpriteEditorDataProvider();
            var rects = dp.GetSpriteRects()
                .Select(s => new RectInt(Mathf.RoundToInt(s.rect.x), Mathf.RoundToInt(s.rect.y), Mathf.RoundToInt(s.rect.width), Mathf.RoundToInt(s.rect.height)))
                .OrderByDescending(r => r.yMax).ThenBy(r => r.x).ToList();
            var bands = new List<List<Cell>>();
            foreach (var r in rects)
            {
                var cell = MakeCell(px, W, r);
                if (cell.count == 0) continue;
                var last = bands.Count > 0 ? bands[bands.Count - 1] : null;
                if (last != null && Mathf.Abs(last[0].r.yMax - r.yMax) <= 2) last.Add(cell);
                else bands.Add(new List<Cell> { cell });
            }
            foreach (var b in bands) b.Sort((a, c) => a.r.x.CompareTo(c.r.x));
            return bands;
        }

        /// <summary>Animation frames have (nearly) the same colours and size; different plants don't.</summary>
        static bool Similar(Cell a, Cell b)
        {
            if (a.count == 0 || b.count == 0) return false;
            if (Mathf.Min(a.count, b.count) / (float)Mathf.Max(a.count, b.count) < 0.6f) return false;
            // Frames of one animation are (almost) the same size; different plants rarely are.
            int hMax = Mathf.Max(a.r.height, b.r.height), wMax = Mathf.Max(a.r.width, b.r.width);
            if (Mathf.Abs(a.r.height - b.r.height) > Mathf.Max(1, Mathf.RoundToInt(hMax * 0.08f))) return false;
            if (Mathf.Abs(a.r.width - b.r.width) > Mathf.Max(2, Mathf.RoundToInt(wMax * 0.15f))) return false;
            float inter = 0f;
            foreach (var kv in a.hist)
                if (b.hist.TryGetValue(kv.Key, out float v)) inter += Mathf.Min(kv.Value, v);
            if (inter < 0.65f) return false;
            // Same shape too (bottom-centre aligned, 1px slack for the sway) — so different bushes
            // that happen to share a palette don't get glued into one flickering "animation".
            return Mathf.Min(Cover(a, b), Cover(b, a)) >= 0.86f;
        }

        /// <summary>Fraction of a's opaque pixels that have an opaque pixel within 1px in b.</summary>
        static float Cover(Cell a, Cell b)
        {
            int dx = (b.r.width - a.r.width) / 2; // align bottom-centre
            int hit = 0, tot = 0;
            for (int y = 0; y < a.r.height; y++)
                for (int x = 0; x < a.r.width; x++)
                {
                    if (!a.mask[y * a.r.width + x]) continue;
                    tot++;
                    bool found = false;
                    for (int oy = -1; oy <= 1 && !found; oy++)
                        for (int ox = -1; ox <= 1 && !found; ox++)
                        {
                            int bx = x + dx + ox, by = y + oy;
                            if (bx >= 0 && by >= 0 && bx < b.r.width && by < b.r.height && b.mask[by * b.r.width + bx]) found = true;
                        }
                    if (found) hit++;
                }
            return tot == 0 ? 0f : hit / (float)tot;
        }

        /// <summary>
        /// One static cell → its own tight rect. Several frames → equal-size rects sharing one bottom line,
        /// on the sheet's grid when it has one, so the plant doesn't jitter while it animates.
        /// </summary>
        static (List<RectInt> frames, int hPx) FramesFor(List<Cell> run, int W, int H, bool existing)
        {
            if (existing || run.Count == 1)
            {
                var single = run.Select(c => c.r).ToList();
                return (single, run.Max(c => c.r.height));
            }

            int yMin = run.Min(c => c.r.yMin), yMax = run.Max(c => c.r.yMax);
            int maxW = run.Max(c => c.r.width);
            int h = yMax - yMin;

            int[] grids = { 16, 24, 32, 48, 64, 96, 128 };
            foreach (int g in grids)
            {
                if (g < maxW) continue;
                var cols = new HashSet<int>();
                bool ok = true;
                foreach (var c in run)
                {
                    int ca = c.r.xMin / g, cb = (c.r.xMax - 1) / g;
                    if (ca != cb || !cols.Add(ca)) { ok = false; break; }
                }
                if (!ok) continue;
                return (run.Select(c => new RectInt((c.r.xMin / g) * g, yMin, Mathf.Min(g, W - (c.r.xMin / g) * g), h)).ToList(), h);
            }

            // No grid: same width for every frame, centred on each cell.
            var frames = new List<RectInt>();
            foreach (var c in run)
            {
                int cx = c.r.xMin + c.r.width / 2;
                int x0 = Mathf.Clamp(cx - maxW / 2, 0, Mathf.Max(0, W - maxW));
                frames.Add(new RectInt(x0, yMin, Mathf.Min(maxW, W), h));
            }
            return (frames, h);
        }

        // ───────────────────────────────── prefabs & rows ─────────────────────────────────

        static GameObject MakePrefab(PlantSet pl)
        {
            var baseFrames = pl.Base;
            var first = baseFrames[0];

            var root = new GameObject("KG_" + pl.name);
            var art = new GameObject("Art");
            art.transform.SetParent(root.transform, false);
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = first;
            sr.sortingOrder = pl.heightPx >= 32 ? -12 : -8; // big plants behind, small ones in front (gap of 2 for the crossfade)

            // One consistent pixel scale for the whole pack (16 art px = UnitsPer16Px world units).
            float s = UnitsPer16Px * first.pixelsPerUnit / 16f;
            art.transform.localScale = new Vector3(s, s, 1f);
            art.transform.localPosition = new Vector3(0f, -first.bounds.min.y * s, 0f);

            var ss = art.AddComponent<SeasonalSprite>();
            ss.tintRole = SeasonalSprite.TintRole.None; // keep the pack's own colours
            ss.shadeVariation = 0.06f;
            Sprite[] F(Season k) => pl.seasons.TryGetValue(k, out var f) ? f : null;
            ss.spring = F(Season.Spring)?[0];
            ss.summer = F(Season.Summer)?[0];
            ss.autumn = F(Season.Autumn)?[0];
            ss.winter = F(Season.Winter)?[0];

            if (pl.Animated)
            {
                var an = art.AddComponent<PlantAnimator>();
                an.frames = baseFrames;
                an.springFrames = F(Season.Spring);
                an.summerFrames = F(Season.Summer);
                an.autumnFrames = F(Season.Autumn);
                an.winterFrames = F(Season.Winter);
                an.fps = 6f;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{OutDir}/{root.name}.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static TreeSpawner Row(TreeSpawner.Row row, string name, TreeSpawner main)
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<TreeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<TreeSpawner>(true);
#endif
            var found = all.FirstOrDefault(s => s.row == row);
            if (found != null) { Undo.RecordObject(found, name); return found; }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(main.transform.parent, false);
            go.transform.position = main.transform.position;
            var sp = go.AddComponent<TreeSpawner>();
            sp.row = row;
            SceneVisibilityManager.instance.DisablePicking(go, true);
            return sp;
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — KebapGuy plants", m, "OK");
    }
}
