using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Kitty Crash › Sprite Sheet Wizard
    /// 1. Click a sprite sheet in the Project window (or drag it into the Sheet field).
    /// 2. Adjust the cell size until the red grid boxes each contain exactly one pose.
    /// 3. Slice. Every row then shows an animated preview — pick which clip each row is
    ///    (Idle / Walk / Run / Sit / Sleep / Pounce).
    /// 4. Apply to Cat. Frames go into the CatSpriteAnimator, feet are pinned to the ground,
    ///    and the cat is scaled up.
    /// Works for one big sheet (one animation per row) or for separate files per animation
    /// (run the wizard on each file; assignments add up).
    /// </summary>
    public class SpriteSheetWizard : EditorWindow
    {
        [MenuItem("Kitty Crash/Sprite Sheet Wizard (slice + assign cat clips)")]
        static void Open() => GetWindow<SpriteSheetWizard>("Sprite Sheet Wizard");

        Texture2D sheet;
        int cellW = 32, cellH = 32, ppu = 32;
        bool pinFeet = true;

        CatSpriteAnimator target;
        bool fixVisual = true, artFacesLeft;
        float visualScale = 2.5f;

        Vector2 rawSize;
        Vector2 scroll;

        class Row
        {
            public int index;
            public List<Sprite> frames = new List<Sprite>();
            public int clip;
            public float fps = 10f;
        }
        readonly List<Row> rows = new List<Row>();
        static readonly string[] ClipNames = { "— skip —", "Idle", "Walk", "Run", "Sit", "Sleep", "Pounce" };

        void OnEnable()
        {
            if (Selection.activeObject is Texture2D t) { sheet = t; OnSheetChanged(); }
            FindTarget();
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is Texture2D t && t != sheet)
            {
                sheet = t;
                OnSheetChanged();
                Repaint();
            }
        }

        void Update()
        {
            if (rows.Count > 0) Repaint(); // keep previews animating
        }

        void FindTarget()
        {
            if (target != null) return;
#if UNITY_2023_1_OR_NEWER
            target = FindAnyObjectByType<CatSpriteAnimator>();
#else
            target = FindObjectOfType<CatSpriteAnimator>();
#endif
        }

        // ── UI ───────────────────────────────────

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("1 · Sheet", EditorStyles.boldLabel);
            var newSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite sheet", sheet, typeof(Texture2D), false);
            if (newSheet != sheet) { sheet = newSheet; OnSheetChanged(); }

            if (sheet == null)
            {
                EditorGUILayout.HelpBox("Click a cat sprite sheet in the Project window.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField($"Image size: {rawSize.x} × {rawSize.y} px");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2 · Grid", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            cellW = Mathf.Max(1, EditorGUILayout.IntField("Cell width", cellW));
            if (GUILayout.Button("16", GUILayout.Width(32))) cellW = cellH = 16;
            if (GUILayout.Button("32", GUILayout.Width(32))) cellW = cellH = 32;
            if (GUILayout.Button("48", GUILayout.Width(32))) cellW = cellH = 48;
            if (GUILayout.Button("64", GUILayout.Width(32))) cellW = cellH = 64;
            EditorGUILayout.EndHorizontal();
            cellH = Mathf.Max(1, EditorGUILayout.IntField("Cell height", cellH));

            int cols = Mathf.Max(1, (int)rawSize.x / cellW), rws = Mathf.Max(1, (int)rawSize.y / cellH);
            bool even = (int)rawSize.x % cellW == 0 && (int)rawSize.y % cellH == 0;
            EditorGUILayout.LabelField($"→ {cols} columns × {rws} rows" + (even ? "" : "   ⚠ size doesn't divide evenly — probably the wrong cell size"));
            EditorGUILayout.HelpBox("Each red box should contain exactly ONE cat pose. If a box cuts a cat in half or holds two cats, change the cell size.", MessageType.None);

            DrawSheetWithGrid();

            ppu = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Pixels per unit", "Usually = cell height. Smaller = bigger in the world."), ppu));
            pinFeet = EditorGUILayout.Toggle(new GUIContent("Pin feet to ground", "Sets the pivot at the lowest painted pixel so paws touch the ground."), pinFeet);

            if (GUILayout.Button("Slice", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Slice sheet", $"This replaces any existing slices on {sheet.name}. Continue?", "Slice", "Cancel"))
                    Slice();
            }

            if (rows.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("3 · What is each row?", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Watch the animated preview on the left of each row and pick the matching clip. Skip rows you don't need.", MessageType.None);
                foreach (var r in rows) DrawRow(r);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("4 · Apply", EditorStyles.boldLabel);
                target = (CatSpriteAnimator)EditorGUILayout.ObjectField("Cat (Visual)", target, typeof(CatSpriteAnimator), true);
                fixVisual = EditorGUILayout.Toggle(new GUIContent("Fix Visual for me", "White colour, feet on ground (Y = 0), scale below."), fixVisual);
                if (fixVisual) visualScale = EditorGUILayout.Slider("Cat size (scale)", visualScale, 0.5f, 6f);
                artFacesLeft = EditorGUILayout.Toggle("Art faces left (flip)", artFacesLeft);

                GUI.enabled = target != null && rows.Any(r => r.clip > 0);
                if (GUILayout.Button("Apply to Cat", GUILayout.Height(32))) ApplyToCat();
                GUI.enabled = true;
                if (target == null) EditorGUILayout.HelpBox("Open the KittyCrash scene, or drag Cat › Visual into the field above.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawSheetWithGrid()
        {
            float maxW = position.width - 30f;
            float scale = Mathf.Min(maxW / Mathf.Max(1f, rawSize.x), 6f);
            scale = Mathf.Max(scale, 0.25f);
            float w = rawSize.x * scale, h = rawSize.y * scale;
            var rect = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            GUI.DrawTexture(rect, sheet, ScaleMode.StretchToFill);

            var line = new Color(1f, 0.2f, 0.2f, 0.85f);
            for (float x = 0; x <= rawSize.x + 0.1f; x += cellW)
                EditorGUI.DrawRect(new Rect(rect.x + x * scale, rect.y, 1, h), line);
            for (float y = 0; y <= rawSize.y + 0.1f; y += cellH)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + y * scale, w, 1), line);
        }

        void DrawRow(Row r)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var preview = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
            EditorGUI.DrawRect(preview, new Color(0.25f, 0.25f, 0.25f));
            if (r.frames.Count > 0)
            {
                int i = (int)(EditorApplication.timeSinceStartup * Mathf.Max(1f, r.fps)) % r.frames.Count;
                DrawSprite(preview, r.frames[i]);
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"Row {r.index + 1}  ·  {r.frames.Count} frames", EditorStyles.boldLabel);
            r.clip = EditorGUILayout.Popup("This row is", r.clip, ClipNames);
            r.fps = EditorGUILayout.Slider("Speed (fps)", r.fps, 2f, 20f);

            var strip = GUILayoutUtility.GetRect(position.width - 120, 28);
            float cell = 26f;
            for (int k = 0; k < r.frames.Count && (k + 1) * (cell + 2) < strip.width; k++)
                DrawSprite(new Rect(strip.x + k * (cell + 2), strip.y, cell, cell), r.frames[k]);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        static void DrawSprite(Rect area, Sprite s)
        {
            if (s == null || s.texture == null) return;
            var tex = s.texture;
            var tr = s.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            float aspect = tr.width / Mathf.Max(1f, tr.height);
            var fit = area;
            if (aspect > 1f) { fit.height = area.width / aspect; fit.y += (area.height - fit.height) * 0.5f; }
            else { fit.width = area.height * aspect; fit.x += (area.width - fit.width) * 0.5f; }
            GUI.DrawTextureWithTexCoords(fit, tex, uv);
        }

        // ── logic ────────────────────────────────

        void OnSheetChanged()
        {
            rows.Clear();
            if (sheet == null) return;

            var raw = LoadRaw(AssetDatabase.GetAssetPath(sheet));
            rawSize = raw != null ? new Vector2(raw.width, raw.height) : new Vector2(sheet.width, sheet.height);
            if (raw != null) DestroyImmediate(raw);

            GuessCell();
            ppu = cellH;
            LoadRowsFromExistingSlices();
        }

        void GuessCell()
        {
            int W = (int)rawSize.x, H = (int)rawSize.y;
            if (H > 0 && W % H == 0 && H <= 128) { cellW = cellH = H; return; } // single strip of square frames
            foreach (int s in new[] { 64, 48, 32, 24, 16 })
                if (W % s == 0 && H % s == 0 && W / s <= 24) { cellW = cellH = s; return; }
            cellW = cellH = 32;
        }

        static Texture2D LoadRaw(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return t.LoadImage(File.ReadAllBytes(path)) ? t : null;
        }

        void Slice()
        {
            string path = AssetDatabase.GetAssetPath(sheet);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var raw = LoadRaw(path);
            if (imp == null || raw == null) { EditorUtility.DisplayDialog("Slice", "Couldn't read this image (only PNG/JPG sheets inside Assets are supported).", "OK"); return; }

            int W = raw.width, H = raw.height;
            int cols = W / cellW, rws = H / cellH;
            var px = raw.GetPixels32();

            var filled = new bool[rws, cols];
            int lowest = cellH;
            for (int r = 0; r < rws; r++)
                for (int c = 0; c < cols; c++)
                {
                    int x0 = c * cellW, y0 = H - (r + 1) * cellH; // texture rows start at the bottom
                    for (int yy = 0; yy < cellH && !filled[r, c]; yy++)
                        for (int xx = 0; xx < cellW; xx++)
                            if (px[(y0 + yy) * W + x0 + xx].a > 8)
                            {
                                filled[r, c] = true;
                                lowest = Mathf.Min(lowest, yy);
                                break;
                            }
                }
            DestroyImmediate(raw);

            float pivotY = pinFeet && lowest < cellH ? (float)lowest / cellH : 0.5f;
            var rects = new List<SpriteRect>();
            for (int r = 0; r < rws; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!filled[r, c]) continue;
                    rects.Add(new SpriteRect
                    {
                        name = $"{sheet.name}_r{r + 1}_{c + 1:00}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(c * cellW, H - (r + 1) * cellH, cellW, cellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, pivotY),
                    });
                }

            if (rects.Count == 0) { EditorUtility.DisplayDialog("Slice", "No painted pixels found — wrong file?", "OK"); return; }

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = Mathf.Max(imp.maxTextureSize, Mathf.NextPowerOfTwo(Mathf.Max(W, H)));
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

            LoadRowsFromExistingSlices();
        }

        /// <summary>Groups the sheet's sprites into rows (top to bottom), frames left to right.</summary>
        void LoadRowsFromExistingSlices()
        {
            var keep = rows.ToDictionary(r => r.index, r => (r.clip, r.fps));
            rows.Clear();
            string path = AssetDatabase.GetAssetPath(sheet);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            if (sprites.Count == 0) return;

            int idx = 0;
            foreach (var g in sprites.GroupBy(s => Mathf.RoundToInt(s.rect.y)).OrderByDescending(g => g.Key))
            {
                var row = new Row { index = idx, frames = g.OrderBy(s => s.rect.x).ToList() };
                if (keep.TryGetValue(idx, out var k)) { row.clip = k.clip; row.fps = k.fps; }
                rows.Add(row);
                idx++;
            }
        }

        void ApplyToCat()
        {
            Undo.RecordObject(target, "Assign cat clips");
            Sprite first = null;
            foreach (var r in rows)
            {
                if (r.clip <= 0 || r.frames.Count == 0) continue;
                var clip = Clip(target, r.clip);
                clip.frames = r.frames.ToArray();
                clip.fps = r.fps;
                if (first == null || r.clip == 1) first = r.frames[0];
            }
            target.flipX = artFacesLeft;

            if (fixVisual)
            {
                var sr = target.GetComponent<SpriteRenderer>();
                Undo.RecordObject(sr, "Fix cat visual");
                sr.color = Color.white;
                if (first != null) sr.sprite = first;

                var t = target.transform;
                Undo.RecordObject(t, "Fix cat visual");
                t.localScale = new Vector3(visualScale, visualScale, 1f);
                t.localPosition = new Vector3(t.localPosition.x, pinFeet ? 0f : t.localPosition.y, t.localPosition.z);
            }

            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            EditorUtility.DisplayDialog("Sprite Sheet Wizard", "Clips assigned. Save the scene (Ctrl+S) and press Play.", "Nice");
        }

        static CatSpriteAnimator.ClipFrames Clip(CatSpriteAnimator a, int i)
        {
            switch (i)
            {
                case 1: return a.idle;
                case 2: return a.walk;
                case 3: return a.run;
                case 4: return a.sit;
                case 5: return a.sleep;
                default: return a.pounce;
            }
        }
    }
}
