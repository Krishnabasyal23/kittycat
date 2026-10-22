using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Kitty Crash > Capture Screenshots: plays the scene and saves the game view at 1920x1080 and 1366x768
    /// to &lt;project&gt;/.tmp/shots: title screen, clean layout, menu open and the classic layout.
    /// The Game view size is switched through Unity's internal GameViewSizes (reflection); if that fails the
    /// shots are taken at the current Game view size and the console says so. Restores the layout setting.
    /// </summary>
    [InitializeOnLoad]
    public static class CaptureScreenshots
    {
        const string PendingKey = "KittyCrash.CaptureScreenshots.Pending";
        static readonly (int w, int h)[] Sizes = { (1920, 1080), (1366, 768) };
        static readonly string[] Shots = { "title", "clean", "menu", "classic" };

        static int step;
        static double nextAt;
        static bool classicBefore, sizeOk;
        static string outDir;

        static CaptureScreenshots()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
                {
                    SessionState.SetBool(PendingKey, false);
                    Begin();
                }
            };
        }

        [MenuItem("Kitty Crash/Capture Screenshots")]
        static void Capture()
        {
            if (EditorApplication.isPlaying) { Begin(); return; }
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Begin()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".tmp", "shots"));
            Directory.CreateDirectory(outDir);
            classicBefore = HudLayout.Classic;
            step = 0;
            nextAt = EditorApplication.timeSinceStartup + 4.0;   // let the scene connect and settle
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Capture] taking screenshots into " + outDir);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup < nextAt) return;

            int total = Sizes.Length * Shots.Length;
            if (step >= total * 2)
            {
                HudLayout.Classic = classicBefore;
                if (GameMenu.Instance != null) GameMenu.Instance.CloseAll();
                EditorApplication.update -= Tick;
                Debug.Log("[Capture] done: " + outDir);
                EditorUtility.RevealInFinder(outDir);
                return;
            }

            // even steps: set up the state; odd steps: capture it (a second later, after layout)
            int index = step / 2;
            var (w, h) = Sizes[index / Shots.Length];
            string shot = Shots[index % Shots.Length];
            if (step % 2 == 0)
            {
                if (index % Shots.Length == 0) sizeOk = SetGameViewSize(w, h);
                Prepare(shot);
            }
            else
            {
                string name = sizeOk ? $"{w}x{h}_{shot}.png" : $"current-size_{shot}.png";
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name));
                if (!sizeOk) Debug.LogWarning($"[Capture] couldn't switch the Game view to {w}x{h}; saved {name} at the current size");
            }
            step++;
            nextAt = EditorApplication.timeSinceStartup + (step % 2 == 1 ? 1.2 : 0.4);
        }

        static void Prepare(string shot)
        {
            var menu = GameMenu.Instance;
            if (menu == null) return;
            menu.CloseAll();
            HudLayout.Classic = shot == "classic";
            if (shot == "title") menu.ShowTitle();
            else if (shot == "menu") menu.ShowMenu();
        }

        // ── Game view size via UnityEditor internals ──

        static bool SetGameViewSize(int w, int h)
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance").GetValue(null);
                var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
                var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                var groupClass = group.GetType();

                int index = FindSize(group, w, h);
                if (index < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                    var fixedRes = Enum.Parse(kindType, "FixedResolution");
                    var size = ctor.Invoke(new[] { fixedRes, w, h, $"Catino {w}x{h}" });
                    groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = FindSize(group, w, h);
                }
                if (index < 0) return false;

                var gameViewType = asm.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType);
                var prop = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite) prop.SetValue(window, index);
                else
                {
                    var callback = gameViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "SizeSelectionCallback");
                    if (callback == null) return false;
                    callback.Invoke(window, new object[] { index, null });
                }
                window.Repaint();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] Game view size switch failed: " + e.Message);
                return false;
            }
        }

        static int FindSize(object group, int w, int h)
        {
            var groupClass = group.GetType();
            int count = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
            var get = groupClass.GetMethod("GetGameViewSize");
            for (int i = 0; i < count; i++)
            {
                var size = get.Invoke(group, new object[] { i });
                var t = size.GetType();
                if ((int)t.GetProperty("width").GetValue(size) == w && (int)t.GetProperty("height").GetValue(size) == h
                    && t.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution")
                    return i;
            }
            return -1;
        }
    }
}
