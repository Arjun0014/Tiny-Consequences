using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TC2.EditorTools
{
    /// <summary>
    /// Command-line builds: -executeMethod TC2.EditorTools.BuildScript.BuildWeb [-tc2Out PATH] [-tc2Dev] [-tc2Threads] [-tc2WebGL2Only] [-tc2Symbols]
    /// </summary>
    public static class BuildScript
    {
        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        [MenuItem("TC2/Build Web")]
        public static void BuildWeb()
        {
            ProjectSetup.Configure();
            string outDir = Arg("-tc2Out", Path.GetFullPath("../../tc2/web-build/web"));
            bool dev = Flag("-tc2Dev");
            PlayerSettings.WebGL.threadsSupport = Flag("-tc2Threads");
            if (Flag("-tc2WebGL2Only"))
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/TC2/Scenes/Main.unity" },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            // -tc2Jobs N: IL2CPP converts and compiles with N jobs instead of one per thread (a shared machine: less memory and CPU at
            // once, a slower build); the project's own setting is restored afterwards
            string jobs = Arg("-tc2Jobs", null);
            // (a build stopped before its restore leaves its --jobs behind: any such is dropped first)
            string il2cppArgs = System.Text.RegularExpressions.Regex.Replace(PlayerSettings.GetAdditionalIl2CppArgs() ?? "", @"\s*--jobs=\d+", "").Trim();
            if (jobs != null) PlayerSettings.SetAdditionalIl2CppArgs((il2cppArgs + " --jobs=" + jobs).Trim());
            // -tc2Symbols: the functions' names in a file beside the build (Build/web.symbols.json; the wasm itself unchanged) - what
            // realmsperf --profile names a CPU profile's wasm frames by; the project's own setting is restored afterwards
            var symbols = PlayerSettings.WebGL.debugSymbolMode;
            if (Flag("-tc2Symbols")) PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(opts); }
            finally { if (jobs != null) PlayerSettings.SetAdditionalIl2CppArgs(il2cppArgs); PlayerSettings.WebGL.debugSymbolMode = symbols; }
            var s = report.summary;
            Debug.Log("[TC2] web build " + s.result + " in " + s.totalTime + " size " + s.totalSize + " -> " + outDir);
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("TC2/Build Windows")]
        public static void BuildWindows()
        {
            ProjectSetup.Configure();
            string outDir = Arg("-tc2Out", Path.GetFullPath("../../tc2/web-build/win/TC2.exe"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/TC2/Scenes/Main.unity" },
                locationPathName = outDir,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("[TC2] windows build " + report.summary.result + " -> " + outDir);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
