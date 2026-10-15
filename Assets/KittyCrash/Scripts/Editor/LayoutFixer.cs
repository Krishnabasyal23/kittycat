using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
   
    public static class LayoutFixer
    {
        const float GroundTop = -2.5f;

        [MenuItem("Kitty Crash/Fix Layout (snap cat, ground, camera back)")]
        public static void Fix()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Stop Play mode first (press the ▶ button), then run this again.\nChanges made during Play mode get thrown away.", "OK");
                return;
            }
            var report = new StringBuilder();
            Undo.SetCurrentGroupName("Fix Layout");
            int group = Undo.GetCurrentGroup();

            // Camera: y = 0, follow the cat
            var cam = Camera.main;
            if (cam != null)
            {
                var t = cam.transform;
                if (Mathf.Abs(t.position.y) > 0.01f || Mathf.Abs(t.position.z + 10f) > 0.01f)
                {
                    Undo.RecordObject(t, "Fix camera");
                    t.position = new Vector3(t.position.x, 0f, -10f);
                    report.AppendLine("• Camera height reset");
                }
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null && (Mathf.Abs(follow.offsetX - 3f) > 0.01f || follow.target == null))
                {
                    Undo.RecordObject(follow, "Fix camera");
                    follow.offsetX = 3f;
                    if (follow.target == null) { var w = Find<CatWalker>(); if (w != null) follow.target = w.transform; }
                    report.AppendLine("• Camera follow reset");
                }
            }

            // Cat root on the ground, Visual centred on the root
            var walker = Find<CatWalker>();
            if (walker != null)
            {
                var root = walker.transform;
                if (Mathf.Abs(root.position.y - GroundTop) > 0.01f || Mathf.Abs(root.position.x) > 0.01f)
                {
                    Undo.RecordObject(root, "Fix cat");
                    root.position = new Vector3(0f, GroundTop, 0f);
                    root.rotation = Quaternion.identity;
                    report.AppendLine("• Cat moved back onto the ground");
                }
                if (walker.visual != null)
                {
                    var v = walker.visual;
                    var sr = v.GetComponent<SpriteRenderer>();
                    float feet = sr != null && sr.sprite != null ? -sr.sprite.bounds.min.y * v.localScale.y : v.localScale.y * 0.5f;
                    float y = v.localPosition.y;
                    if (y < -0.05f || y > feet + 0.3f) y = feet;
                    if (Mathf.Abs(v.localPosition.x) > 0.01f || Mathf.Abs(v.localPosition.y - y) > 0.001f)
                    {
                        Undo.RecordObject(v, "Fix cat visual");
                        v.localPosition = new Vector3(0f, y, 0f);
                        v.localRotation = Quaternion.identity;
                        report.AppendLine("• Cat Visual re-centred (it had been dragged)");
                    }
                    if (sr != null && sr.sortingOrder < 10) { Undo.RecordObject(sr, "Fix cat"); sr.sortingOrder = 10; }

                    // Costume groups sit on the Visual's origin (the hats inside keep their own offsets)
                    foreach (Transform c in v)
                        if (c.name.StartsWith("Costume_") && c.localPosition.sqrMagnitude > 0.0001f)
                        {
                            Undo.RecordObject(c, "Fix costume");
                            c.localPosition = Vector3.zero;
                            report.AppendLine($"• {c.name} group re-centred");
                        }
                }
            }

            // Ground: three segments side by side, top surface at GroundTop
            var looper = Find<GroundLooper>();
            if (looper != null && looper.segments != null)
            {
                Undo.RecordObject(looper.transform, "Fix ground");
                looper.transform.position = Vector3.zero;
                looper.transform.localScale = Vector3.one;
                float w = looper.segmentWidth;
                for (int i = 0; i < looper.segments.Length; i++)
                {
                    var s = looper.segments[i];
                    if (s == null) continue;
                    var sr = s.GetComponent<SpriteRenderer>();

                    // Always: tiled draw mode, scale 1, exactly segmentWidth × 4 units, top edge at GroundTop.
                    // (Works for both the placeholder square and the 16×64 grass/dirt tiles.)
                    Undo.RecordObject(s, "Fix ground");
                    s.position = new Vector3(-w + i * w, GroundTop - 2f, 0f);
                    s.rotation = Quaternion.identity;
                    s.localScale = Vector3.one;
                    if (sr != null)
                    {
                        Undo.RecordObject(sr, "Fix ground");
                        sr.drawMode = SpriteDrawMode.Tiled;
                        sr.tileMode = SpriteTileMode.Continuous;
                        sr.size = new Vector2(w + 0.05f, 4f);
                        EditorUtility.SetDirty(sr);
                    }
                    // Remove any leftover crossfade child created during a previous Play session.
                    var cross = s.Find("SeasonCrossfade");
                    if (cross != null) Undo.DestroyObjectImmediate(cross.gameObject);
                }
                report.AppendLine("• Ground segments snapped back in line");
                SceneVisibilityManager.instance.DisablePicking(looper.gameObject, true);
            }

            // Scenery + background baselines
            var spawner = Find<TreeSpawner>();
            if (spawner != null && Mathf.Abs(spawner.groundY - GroundTop) > 0.01f)
            {
                Undo.RecordObject(spawner, "Fix scenery");
                spawner.groundY = GroundTop;
                report.AppendLine("• Scenery ground height reset");
            }
            var bg = GameObject.Find("Background");
            if (bg != null)
            {
                Undo.RecordObject(bg.transform, "Fix background");
                bg.transform.position = Vector3.zero;
                foreach (Transform layer in bg.transform)
                {
                    Undo.RecordObject(layer, "Fix background");
                    layer.localPosition = Vector3.zero;
                }
                SceneVisibilityManager.instance.DisablePicking(bg, true);
            }
            var overlay = cam != null ? cam.transform.Find("BrightnessOverlay") : null;
            if (overlay != null) SceneVisibilityManager.instance.DisablePicking(overlay.gameObject, true);

            Undo.CollapseUndoOperations(group);
            if (walker != null) EditorSceneManager.MarkSceneDirty(walker.gameObject.scene);

            EditorUtility.DisplayDialog("Kitty Crash — Fix Layout",
                (report.Length > 0 ? report.ToString() : "Everything was already in place.\n") +
                "\nGround, background and the brightness overlay are now NOT clickable in the Scene view, " +
                "so you can't drag them by accident.\n\nSave with Ctrl+S.",
                "OK");
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