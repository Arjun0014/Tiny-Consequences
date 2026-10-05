using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TC2.EditorTools
{
    /// <summary>Batch helpers: -executeMethod TC2.EditorTools.BatchTools.HashTest [-tc2Out FILE] (runs Core under the editor's Mono).</summary>
    [InitializeOnLoad]
    public static class BatchTools
    {
        static readonly double Started = EditorApplication.timeSinceStartup;

        static BatchTools()
        {
            // survives the domain reload of entering Play Mode: the editor quits once the game has written its shots
            if (ShotsDir() != null) EditorApplication.update += PollShots;
        }

        static string ShotsDir()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-tc2shots") return a[i + 1];
            return null;
        }

        /// <summary>-executeMethod TC2.EditorTools.BatchTools.PlayShots -tc2shots DIR [...]: the real game in Play Mode, screenshots, quit.</summary>
        public static void PlayShots()
        {
            string dir = ShotsDir();
            Directory.CreateDirectory(dir);
            string done = Path.Combine(dir, "done.txt");
            if (File.Exists(done)) File.Delete(done);
            EditorSceneManager.OpenScene("Assets/TC2/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }

        static void PollShots()
        {
            string dir = ShotsDir();
            if (dir == null) return;
            if (File.Exists(Path.Combine(dir, "done.txt")) || EditorApplication.timeSinceStartup - Started > 600)
            {
                EditorApplication.update -= PollShots;
                EditorApplication.Exit(File.Exists(Path.Combine(dir, "done.txt")) ? 0 : 4);
            }
        }

        public static void HashTest()
        {
            string probe = TC2.Core.Diagnostics.CrossRuntime.Probe();
            string outFile = null;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-tc2Out") outFile = a[i + 1];
            if (outFile != null) File.WriteAllText(outFile, TC2.Core.Diagnostics.CrossRuntime.Parts() + "\n" + probe);
            Debug.Log("[TC2] hashtest " + probe);
        }
    }
}
