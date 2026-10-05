using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TC2.EditorTools
{
    /// <summary>
    /// Configures the project from code (batchmode: -executeMethod TC2.EditorTools.ProjectSetup.Configure):
    /// URP assets, player settings for the Web (D2-011), the main scene and build settings.
    /// Idempotent: safe to run again.
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/TC2/Settings";
        const string ScenePath = "Assets/TC2/Scenes/Main.unity";

        [MenuItem("TC2/Configure Project")]
        public static void Configure()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory("Assets/TC2/Scenes");
            var urp = EnsureUrp();
            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);

            PlayerSettings.companyName = "Tiny Consequences";
            PlayerSettings.productName = "Tiny Consequences 2";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.WebGPU, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.wasm2023 = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[TC2] project configured: URP " + urp.name + ", scene " + ScenePath);
        }

        static UniversalRenderPipelineAsset EnsureUrp()
        {
            string assetPath = SettingsDir + "/TC2_URP.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp != null) return urp;
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, SettingsDir + "/TC2_URP_Renderer.asset");
            urp = UniversalRenderPipelineAsset.Create(rendererData);
            urp.name = "TC2_URP";
            urp.shadowDistance = 600f;
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            AssetDatabase.CreateAsset(urp, assetPath);
            return urp;
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("TC2");
                go.AddComponent<TC2.Game.Bootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
