using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    public static class SceneryMaker
    {
        const string OutDir = "Assets/KittyCrash/Prefabs/Scenery";
        /// <summary>World units per 16 art pixels. 1.5 → a 16px flower is 1.5 units tall, a 48px tree 4.5.</summary>
        const float UnitsPer16Px = 1.5f;

        [MenuItem("Kitty Crash/Make Scenery From Selected Sprites")]
        public static void Make()
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }

            var sprites = new List<Sprite>();
            foreach (var o in Selection.objects)
            {
                if (o is Sprite s) sprites.Add(s);
                else if (o is Texture2D t)
                    sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)).OfType<Sprite>());
            }
            sprites = sprites.Distinct().ToList();
            if (sprites.Count == 0)
            {
                Msg("Select some plant sprites in the Project window first.\n\n" +
                    "Tip: click the ▸ arrow on a sliced sheet to see its sprites, then click the first and shift-click the last.\n" +
                    "Not sliced yet? Use Kitty Crash › Sprite Sheet Wizard with cell size 16, press Slice, then come back.");
                return;
            }
            if (sprites.Count > 40 && !EditorUtility.DisplayDialog("Kitty Crash", $"{sprites.Count} sprites selected — that's a lot of prefabs. Continue?", "Yes", "Cancel"))
                return;

            Directory.CreateDirectory(OutDir);
            var made = new List<GameObject>();
            foreach (var sp in sprites)
            {
                var root = new GameObject("Plant_" + sp.name);
                var art = new GameObject("Art");
                art.transform.SetParent(root.transform, false);
                var sr = art.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingOrder = sp.rect.height >= 32 ? -12 : -8; // big trees behind, small plants in front

                // Size from pixel height (independent of the sheet's Pixels Per Unit), bottom on the ground.
                float targetH = sp.rect.height / 16f * UnitsPer16Px;
                float s = targetH / Mathf.Max(0.0001f, sp.bounds.size.y);
                art.transform.localScale = new Vector3(s, s, 1f);
                art.transform.localPosition = new Vector3(0f, -sp.bounds.min.y * s, 0f);

                var ss = art.AddComponent<SeasonalSprite>();
                ss.tintRole = SeasonalSprite.TintRole.None; // keep the pack's own colours
                ss.shadeVariation = 0.06f;

                string path = AssetDatabase.GenerateUniqueAssetPath($"{OutDir}/{Sanitize(root.name)}.prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                if (prefab != null) made.Add(prefab);
            }

#if UNITY_2023_1_OR_NEWER
            var spawner = Object.FindAnyObjectByType<TreeSpawner>();
#else
            var spawner = Object.FindObjectOfType<TreeSpawner>();
#endif
            if (spawner == null)
            {
                Msg($"Made {made.Count} prefab(s) in {OutDir}, but the KittyCrash scene isn't open, so the spawner wasn't updated.\n" +
                    "Open the scene and drag them into Scenery › Tree Spawner › Prefabs.");
                return;
            }

            Undo.RecordObject(spawner, "Scenery");
            var keep = (spawner.prefabs ?? new GameObject[0]).Where(p => p != null && p.name.Contains("FestiveProp")).ToList();
            var list = new List<GameObject>(made);
            list.AddRange(keep);
            spawner.prefabs = list.ToArray();
            spawner.poolSize = Mathf.Max(spawner.poolSize, 18);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

            Msg($"Made {made.Count} scenery prefab(s) and swapped them in for the squares " +
                $"(kept {keep.Count} festive prop slot(s)).\n\n" +
                "• Press Play to see them.\n" +
                "• Too big/small? Open a prefab in Assets/KittyCrash/Prefabs/Scenery and scale its Art child.\n" +
                "• Seasonal versions: on the prefab's Art › Seasonal Sprite, drag spring/summer/autumn/winter sprites into the slots.\n" +
                "• More of one plant? Drag that prefab into Scenery › Tree Spawner › Prefabs again.\n\nSave with Ctrl+S.");
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static void Msg(string m) => EditorUtility.DisplayDialog("Kitty Crash — Scenery", m, "OK");
    }
}