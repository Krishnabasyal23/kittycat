using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Set Up Round Cycle + Split Screen
    ///   • removes the festival costumes (hats/masks) and the black-fur tint
    ///   • turns on the automatic round cycle (betting countdown → walk → distracted → countdown…)
    ///   • turns off the warm-up (the countdown replaces it)
    ///   • switches the UI to split screen (game on top, betting dock at the bottom)
    /// Ctrl+Z undoes it.
    /// </summary>
    public static class RoundCycleSetup
    {
        [MenuItem("Kitty Crash/Set Up Round Cycle + Split Screen")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
#if UNITY_2023_1_OR_NEWER
            var rm = Object.FindAnyObjectByType<RoundManager>();
            var walker = Object.FindAnyObjectByType<CatWalker>();
#else
            var rm = Object.FindObjectOfType<RoundManager>();
            var walker = Object.FindObjectOfType<CatWalker>();
#endif
            if (rm == null || walker == null) { Msg("Open the KittyCrash scene first."); return; }

            Undo.SetCurrentGroupName("Round cycle setup");
            int group = Undo.GetCurrentGroup();
            int removed = 0;

            // 1. costumes off
            if (walker.visual != null)
            {
                foreach (Transform c in walker.visual.Cast<Transform>().ToList())
                    if (c.name.StartsWith("Costume_")) { Undo.DestroyObjectImmediate(c.gameObject); removed++; }
                var sr = walker.visual.GetComponent<SpriteRenderer>();
                var anim = walker.visual.GetComponent<CatSpriteAnimator>();
                if (sr != null && anim != null && anim.HasFrames) { Undo.RecordObject(sr, "Cat colour"); sr.color = Color.white; }
            }
            var look = walker.GetComponent<CatSeasonLook>();
            if (look != null) Undo.DestroyObjectImmediate(look);

            // 2. round cycle
            Undo.RecordObject(rm, "Round cycle");
            rm.autoRounds = true;
            if (rm.bettingCountdown < 3f) rm.bettingCountdown = 8f;
            if (rm.resultDuration < 2f) rm.resultDuration = 3.5f;
            EditorUtility.SetDirty(rm);

            // 3. no warm-up (offline); the server patch should use WARMUP_SECONDS=0 too
            var local = rm.GetComponent<LocalBackend>();
            if (local != null) { Undo.RecordObject(local, "Warm-up"); local.warmupSeconds = 0f; EditorUtility.SetDirty(local); }

            // 4. split-screen UI
            var ui = rm.GetComponent<CrashUI>();
            bool hasUi = ui != null;
            if (hasUi) { Undo.RecordObject(ui, "Split screen"); ui.docked = true; EditorUtility.SetDirty(ui); }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);
            Msg($"Done!\n• Removed {removed} costume group(s) and the fur tint\n• Round cycle ON (countdown {rm.bettingCountdown:0}s, result pause {rm.resultDuration:0.#}s)\n• Warm-up OFF (offline)\n" +
                (hasUi ? "• Split screen ON\n" : "• Crash UI not found — run Kitty Crash › Install Pro UI + Graphics, then this again\n") +
                "\nIf you use the API with the warm-up patch, restart the server with WARMUP_SECONDS=0.\n" +
                "Tune: Managers › Round Manager (Betting Countdown, Result Duration), Managers › Crash UI (Dock Height, Game View Size).\n\nSave with Ctrl+S.");
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Round Cycle", m, "OK");
    }
}
