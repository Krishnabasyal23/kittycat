using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Auto-fit Cat (paws on ground + costumes on head)
    /// Looks at the actual painted pixels of the cat's idle frame (ignoring empty space in the cell),
    /// then: puts the paws exactly on the ground, and sizes/places every costume item on the head.
    /// Run it again any time you change the cat's scale or frames. Ctrl+Z undoes it.
    /// </summary>
    public static class CatAutoFit
    {
        [MenuItem("Kitty Crash/Auto-fit Cat (paws on ground + costumes on head)")]
        public static void Fit()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Stop Play mode first, then run this again.", "OK");
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var walker = Object.FindAnyObjectByType<CatWalker>();
#else
            var walker = Object.FindObjectOfType<CatWalker>();
#endif
            if (walker == null || walker.visual == null) { Msg("Open the KittyCrash scene first."); return; }

            var visual = walker.visual;
            var sr = visual.GetComponent<SpriteRenderer>();
            var anim = visual.GetComponent<CatSpriteAnimator>();
            if (sr == null) { Msg("Cat › Visual has no Sprite Renderer."); return; }

            // Use a real cat frame (idle → sit → walk), and show it in edit mode too.
            Sprite frame = null;
            if (anim != null)
            {
                if (anim.idle.IsValid) frame = anim.idle.frames[0];
                else if (anim.sit.IsValid) frame = anim.sit.frames[0];
                else if (anim.walk.IsValid) frame = anim.walk.frames[0];
            }
            if (frame == null) frame = sr.sprite;
            if (frame == null) { Msg("Give the cat its frames first (Sprite Sheet Wizard), then run this."); return; }

            bool flip = anim != null && anim.flipX; // art faces left, displayed mirrored

            // ── read the painted pixels of that frame ──
            string path = AssetDatabase.GetAssetPath(frame.texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { Msg("Couldn't read the cat image file."); return; }
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!raw.LoadImage(File.ReadAllBytes(path))) { Msg("Couldn't decode the cat image (PNG expected)."); return; }

            float sx = (float)raw.width / frame.texture.width, sy = (float)raw.height / frame.texture.height;
            Rect rect = frame.rect;
            int x0 = Mathf.FloorToInt(rect.x * sx), y0 = Mathf.FloorToInt(rect.y * sy);
            int x1 = Mathf.CeilToInt((rect.x + rect.width) * sx) - 1, y1 = Mathf.CeilToInt((rect.y + rect.height) * sy) - 1;
            var px = raw.GetPixels32();
            int W = raw.width;
            bool Opaque(int x, int y) => px[y * W + x].a > 20;

            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Opaque(x, y)) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) { Object.DestroyImmediate(raw); Msg("That frame is empty — check the slicing."); return; }

            int w = maxX - minX + 1, h = maxY - minY + 1;
            // Head = front 40% of the body (front = right side unless the art faces left).
            int fa = flip ? minX : maxX - Mathf.RoundToInt(w * 0.4f);
            int fb = flip ? minX + Mathf.RoundToInt(w * 0.4f) : maxX;
            int headTop = -1;
            for (int y = maxY; y >= minY && headTop < 0; y--)
                for (int x = fa; x <= fb; x++)
                    if (Opaque(x, y)) { headTop = y; break; }
            int headRows = Mathf.Max(2, Mathf.RoundToInt(h * 0.35f));
            float sum = 0; int cnt = 0;
            for (int y = headTop; y > headTop - headRows && y >= minY; y--)
                for (int x = fa; x <= fb; x++)
                    if (Opaque(x, y)) { sum += x; cnt++; }
            float headCx = cnt > 0 ? sum / cnt : (fa + fb) * 0.5f;
            Object.DestroyImmediate(raw);

            // ── pixel → Visual-local units ──
            float ppu = frame.pixelsPerUnit;
            Vector2 piv = frame.pivot; // pixels, relative to rect
            float LX(float x) => (flip ? -1f : 1f) * ((x / sx - rect.x) - piv.x) / ppu;
            float LY(float y) => ((y / sy - rect.y) - piv.y) / ppu;

            float feet = LY(minY);
            float top = LY(headTop + 1);
            float headX = LX(headCx + 0.5f);
            float bodyW = w / sx / ppu, bodyH = h / sy / ppu;

            Undo.SetCurrentGroupName("Auto-fit Cat");
            int group = Undo.GetCurrentGroup();

            // Paws on the ground: the cat root sits at ground height, so lift Visual by the empty space under the paws.
            Undo.RecordObject(visual, "Auto-fit cat");
            visual.localPosition = new Vector3(0f, -feet * visual.localScale.y, 0f);
            Undo.RecordObject(sr, "Auto-fit cat");
            sr.sprite = frame;
            sr.color = Color.white;

            // Costumes
            int placed = 0;
            foreach (Transform g in visual)
            {
                if (!g.name.StartsWith("Costume_")) continue;
                Undo.RecordObject(g, "Auto-fit costumes");
                g.localPosition = Vector3.zero; g.localRotation = Quaternion.identity; g.localScale = Vector3.one;

                foreach (Transform item in g)
                {
                    var isr = item.GetComponent<SpriteRenderer>();
                    if (isr == null || isr.sprite == null) continue;
                    Vector2 size = isr.sprite.bounds.size;
                    string n = item.name.ToLowerInvariant();

                    float widthFrac, yPos, s;
                    if (n.Contains("glass") || n.Contains("mask"))
                    {
                        widthFrac = 0.32f;
                        s = bodyW * widthFrac / Mathf.Max(0.001f, size.x);
                        yPos = top - bodyH * 0.24f;
                    }
                    else if (n.Contains("crown"))
                    {
                        widthFrac = 0.36f;
                        s = bodyW * widthFrac / Mathf.Max(0.001f, size.x);
                        yPos = top + size.y * s * 0.15f;
                    }
                    else // hats
                    {
                        widthFrac = 0.42f;
                        s = bodyW * widthFrac / Mathf.Max(0.001f, size.x);
                        yPos = top + size.y * s * 0.32f;
                    }

                    Undo.RecordObject(item, "Auto-fit costumes");
                    item.localPosition = new Vector3(headX, yPos, 0f);
                    item.localRotation = Quaternion.identity;
                    item.localScale = new Vector3(s, s, 1f);
                    isr.sortingOrder = sr.sortingOrder + 2;
                    placed++;
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(walker.gameObject.scene);
            Msg($"Done!\n• Paws placed on the ground\n• {placed} costume item(s) sized and put on the head\n\n" +
                "Fine-tune any item by selecting it in the Hierarchy and dragging the move-tool arrows. Save with Ctrl+S.");
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Auto-fit Cat", m, "OK");
    }
}
