using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Install Pro UI + Graphics
    /// Adds the crash-game UI (CrashUI), camera juice, cat FX and the URP lighting/post-processing pass
    /// to the current scene. Non-destructive and safe to re-run. Ctrl+Z undoes it.
    /// </summary>
    public static class ProUiInstaller
    {
        const string FontDir = "Assets/KittyCrash/Art/Fonts";

        [MenuItem("Kitty Crash/Install Pro UI + Graphics")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Stop Play mode first, then run this again.", "OK");
                return;
            }
#if UNITY_2023_1_OR_NEWER
            var rm = Object.FindAnyObjectByType<RoundManager>();
            var walker = Object.FindAnyObjectByType<CatWalker>();
            var hud = Object.FindAnyObjectByType<CrashHUD>();
#else
            var rm = Object.FindObjectOfType<RoundManager>();
            var walker = Object.FindObjectOfType<CatWalker>();
            var hud = Object.FindObjectOfType<CrashHUD>();
#endif
            if (rm == null || walker == null)
            {
                EditorUtility.DisplayDialog("Kitty Crash", "Open the KittyCrash scene first.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Install Pro UI + Graphics");
            int group = Undo.GetCurrentGroup();

            // 1. Crash UI on Managers, old debug HUD off
            var ui = rm.GetComponent<CrashUI>();
            if (ui == null) ui = Undo.AddComponent<CrashUI>(rm.gameObject);
            Undo.RecordObject(ui, "Crash UI");
            ui.font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/PixelifySans-Regular.ttf");
            ui.boldFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/PixelifySans-Bold.ttf");
            EditorUtility.SetDirty(ui);
            if (hud != null) { Undo.RecordObject(hud, "Old HUD"); hud.enabled = false; }

            // 2. Camera: juice + frame the cat a bit more centrally (the bet card sits bottom-left)
            var cam = Camera.main;
            if (cam != null)
            {
                if (cam.GetComponent<CameraJuice>() == null) Undo.AddComponent<CameraJuice>(cam.gameObject);
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null) { Undo.RecordObject(follow, "Camera"); follow.offsetX = 1.5f; }
            }

            // 3. Cat FX (shadow, dust, coins)
            if (walker.GetComponent<CatFx>() == null) Undo.AddComponent<CatFx>(walker.gameObject);

            // 4. URP lighting + post-processing
            var gb = GameObject.Find("GraphicsBoost");
            if (gb == null)
            {
                gb = new GameObject("GraphicsBoost");
                Undo.RegisterCreatedObjectUndo(gb, "GraphicsBoost");
                gb.AddComponent<GraphicsBoost>();
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);
            EditorUtility.DisplayDialog("Kitty Crash",
                "Pro UI + graphics installed!\n\n" +
                "• New crash-game UI (bet card, live curve, history, toasts) — the old HUD is switched off\n" +
                "• Camera zooms in as the multiplier climbs, shakes on distraction\n" +
                "• Cat shadow, running dust, coin shower on cash-out\n" +
                "• Seasonal lighting + bloom/vignette (URP)\n\n" +
                "Save (Ctrl+S) and press Play. Tweak colours on Managers › Crash UI and GraphicsBoost.",
                "Nice");
        }
    }
}
