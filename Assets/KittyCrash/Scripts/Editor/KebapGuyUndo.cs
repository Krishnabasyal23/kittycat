using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Undo KebapGuy Plant Pack
    /// Removes the Plants_Back / Plants_Front rows and puts the scenery back to what it was before:
    /// your own "Make Scenery" plants (Prefabs/Scenery) if you made any, otherwise the original Tree/Bush,
    /// plus the FestiveProp. Deletes the generated Prefabs/KebapGuy folder. Your PNGs stay as they are.
    /// </summary>
    public static class KebapGuyUndo
    {
        const string Root = "Assets/KittyCrash/Prefabs";

        [MenuItem("Kitty Crash/Undo KebapGuy Plant Pack")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
            var main = TreeSpawner.FindMain();
            if (main == null) { Msg("Open the KittyCrash scene first."); return; }

            Undo.SetCurrentGroupName("Undo KebapGuy plants");
            int group = Undo.GetCurrentGroup();

#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<TreeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<TreeSpawner>(true);
#endif
            int rows = 0;
            foreach (var s in all)
                if (s.row != TreeSpawner.Row.Main) { Undo.DestroyObjectImmediate(s.gameObject); rows++; }

            var list = new List<GameObject>();
            string source;
            var mine = AssetDatabase.IsValidFolder(Root + "/Scenery")
                ? AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Scenery" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).ToList()
                : new List<GameObject>();
            if (mine.Count > 0) { list.AddRange(mine); source = $"your {mine.Count} Make-Scenery plant(s)"; }
            else
            {
                var tree = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Tree.prefab");
                var bush = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Bush.prefab");
                if (tree != null) { list.Add(tree); list.Add(tree); }
                if (bush != null) list.Add(bush);
                source = "the original Tree/Bush";
            }
            var festive = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/FestiveProp.prefab");
            if (festive != null) list.Add(festive);

            Undo.RecordObject(main, "Scenery");
            main.prefabs = list.ToArray();
            main.spacing = new Vector2(2.5f, 7f);
            main.scaleRange = new Vector2(0.8f, 1.4f);
            main.randomFlip = false;
            main.overrideSorting = false;
            main.brightness = 1f;
            EditorUtility.SetDirty(main);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(main.gameObject.scene);

            if (AssetDatabase.IsValidFolder(Root + "/KebapGuy")) AssetDatabase.DeleteAsset(Root + "/KebapGuy");

            Msg($"Reverted.\n• Removed {rows} extra plant row(s)\n• Scenery uses {source}" + (festive != null ? " + FestiveProp" : "") +
                "\n• Deleted Prefabs/KebapGuy\n\nSave with Ctrl+S.");
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Undo KebapGuy", m, "OK");
    }
}
