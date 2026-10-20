using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Server › Use v2 Server (port 8001) / Use v1 Server (port 8000)
    /// Points every Api Backend in the open scene at the chosen local server, keeping its host
    /// (127.0.0.1, a LAN IP, ...). v2 (wallets, points) runs on 8001; the v1 demo stays on 8000.
    /// Ctrl+Z undoes it. Save the scene with Ctrl+S.
    /// </summary>
    public static class ServerSwitch
    {
        [MenuItem("Kitty Crash/Server/Use v2 Server (port 8001)")]
        public static void UseV2() => SetPort(8001, "v2 (wallets, points)");

        [MenuItem("Kitty Crash/Server/Use v1 Server (port 8000)")]
        public static void UseV1() => SetPort(8000, "v1 demo");

        static void SetPort(int port, string label)
        {
            if (EditorApplication.isPlaying) { Msg("Stop Play mode first, then run this again."); return; }
#if UNITY_2023_1_OR_NEWER
            var backends = UnityEngine.Object.FindObjectsByType<ApiBackend>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var backends = UnityEngine.Object.FindObjectsOfType<ApiBackend>(true);
#endif
            if (backends.Length == 0) { Msg("No Api Backend in the open scene. Open the KittyCrash scene first."); return; }

            Undo.SetCurrentGroupName("Server port");
            int group = Undo.GetCurrentGroup();
            var lines = "";
            foreach (var api in backends)
            {
                string before = api.baseUrl;
                Undo.RecordObject(api, "Server port");
                api.baseUrl = WithPort(api.baseUrl, port);
                EditorUtility.SetDirty(api);
                EditorSceneManager.MarkSceneDirty(api.gameObject.scene);
                lines += $"• {api.gameObject.name}: {before}  →  {api.baseUrl}\n";
            }
            Undo.CollapseUndoOperations(group);

            Msg($"Game now talks to the {label} server.\n\n{lines}\n" +
                (port == 8001 ? "Start it with start_server.bat (port 8001).\n" : "Run the v1 demo from the v1.0-pitch tag.\n") +
                "Tick Use Api on the Round Manager to play against it.\n\nSave with Ctrl+S.");
        }

        static string WithPort(string url, int port)
        {
            if (Uri.TryCreate(string.IsNullOrWhiteSpace(url) ? "http://127.0.0.1" : url.Trim(), UriKind.Absolute, out var uri))
                return new UriBuilder(uri.Scheme, uri.Host, port).Uri.GetLeftPart(UriPartial.Authority);
            return $"http://127.0.0.1:{port}";
        }

        static void Msg(string text) => EditorUtility.DisplayDialog("Kitty Crash · Server", text, "OK");
    }
}
