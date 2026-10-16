using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Install Schrödinger Box + Meow Cash-Out
    ///   • Schrödinger Box: live provably-fair quantum seal (needs the patched KittyAPI for real qubits;
    ///     offline/watch rounds use a classical seal so it always works)
    ///   • Meow to cash out: microphone toggle at the bottom-right of the game view
    /// </summary>
    public static class QuantumInstaller
    {
        [MenuItem("Kitty Crash/Install Schrödinger Box + Meow Cash-Out")]
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
            if (rm.GetComponent<VoiceCashOut>() == null) Undo.AddComponent<VoiceCashOut>(rm.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);

            EditorUtility.DisplayDialog("Kitty Crash — Quantum",
                "Done!\n" +
                "• Schrödinger Box (top-left of the game view): every round is sealed with SHA-256 before anyone can bet; " +
                "when kitty gets distracted the qubits collapse and the game verifies the seal + the math itself. Press Q to hide/show.\n" +
                "• Meow to cash out (bottom-right): click it, then meow / clap / yell to cash out.\n\n" +
                "For real qubits: run the patched KittyAPI and click USE API.\n\nSave with Ctrl+S, then Play.", "OK");
        }
    }
}
