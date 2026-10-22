using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    public static class QuantumInstaller
    {
        [MenuItem("Kitty Crash/Install Fairness Seal Panel (Q)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Kitty Crash", "Stop Play mode first, then run this again.", "OK"); return; }
#if UNITY_2023_1_OR_NEWER
            var rm = Object.FindAnyObjectByType<RoundManager>();
#else
            var rm = Object.FindObjectOfType<RoundManager>();
#endif
            if (rm == null) { EditorUtility.DisplayDialog("Kitty Crash", "Open the KittyCrash scene first.", "OK"); return; }

            Undo.SetCurrentGroupName("Quantum box");
            int group = Undo.GetCurrentGroup();
            if (rm.GetComponent<QuantumBox>() == null) Undo.AddComponent<QuantumBox>(rm.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);

            EditorUtility.DisplayDialog("Kitty Crash — Fairness seal",
                "Done!\n" +
                "Fairness seal panel (top-left of the game view): every round is sealed with SHA-256 before anyone can bet; " +
                "when kitty gets distracted the game opens the seal and checks it. Press Q to hide/show.\n\n" +
                "It needs online play (the game server). Save with Ctrl+S, then Play.", "OK");
        }
    }
}
