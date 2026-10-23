using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// Menu: Kitty Crash › Build › WebGL for the server (/play)
    /// Builds the game scene for the browser into KittyAPI\web\play, which the Catino server serves at /play
    /// (same address as its API, so the page talks to the server it came from). Uncompressed files: the server
    /// compresses them on the fly. From a command line:
    /// Unity.exe -batchmode -quit -projectPath D:\hhh\catt -executeMethod KittyCrash.EditorTools.WebGlBuild.BuildFromCommandLine [-buildPath dir]
    /// </summary>
    public static class WebGlBuild
    {
        const string Scene = "Assets/KittyCrash/Scenes/KittyCrash.unity";

        static string DefaultOutput() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "KittyAPI", "web", "play"));

        [MenuItem("Kitty Crash/Build/WebGL for the server (/play)")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Catino", "Stop Play mode first.", "OK"); return; }
            bool ok = Build(DefaultOutput());
            EditorUtility.DisplayDialog("Catino", ok ? "WebGL build finished: KittyAPI\\web\\play\n\nStart the server and open /play." : "The WebGL build failed. See the Console.", "OK");
        }

        public static void BuildFromCommandLine()
        {
            string path = DefaultOutput();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildPath") path = args[i + 1];
            EditorApplication.Exit(Build(path) ? 0 : 1);
        }

        static bool Build(string outputDir)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;

            Directory.CreateDirectory(outputDir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[Catino] WebGL build {s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024 * 1024)} MB, {s.totalTime}");
            return s.result == BuildResult.Succeeded;
        }
    }
}
