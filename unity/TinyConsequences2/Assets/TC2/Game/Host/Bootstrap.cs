using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TC2.Game
{
    /// <summary>
    /// Builds the game from code (the scene holds only this component on an object named "TC2", which the
    /// browser glue sends messages to): host, camera, sun, planet and object views, HUD, test bridge.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            gameObject.name = "TC2";
            Application.targetFrameRate = 60;
            var host = gameObject.AddComponent<SimHost>();

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.02f, 0.045f);
            cam.fieldOfView = 45f;
            cam.allowHDR = true;
            camGo.tag = "MainCamera";
            var camData = camGo.GetComponent<UniversalAdditionalCameraData>() ?? camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.32f);

            var rig = camGo.AddComponent<CameraRig>();
            rig.Host = host; rig.Cam = cam; rig.Sun = sun;

            var views = new GameObject("Views");
            var planet = views.AddComponent<PlanetView>();
            planet.Host = host;
            var terrain = views.AddComponent<TerrainLod>();
            terrain.Host = host; terrain.Rig = rig;
            var water = views.AddComponent<WaterView>();
            water.Host = host;
            var clouds = views.AddComponent<CloudView>();
            clouds.Host = host; clouds.Rig = rig;
            var sky = views.AddComponent<Sky>();
            sky.Host = host; sky.Rig = rig; sky.Sun = sun;
            var objects = views.AddComponent<WorldObjectsView>();
            objects.Host = host; objects.Rig = rig;
            var volcanoes = views.AddComponent<VolcanoView>();
            volcanoes.Host = host; volcanoes.Rig = rig;

            var hud = gameObject.AddComponent<Hud>();
            hud.Host = host; hud.Rig = rig; hud.Objects = objects;
            var bridge = gameObject.AddComponent<WebBridge>();
            bridge.Host = host; bridge.Rig = rig;
            if (ShotRunner.Arg("-tc2shots") != null)
            {
                var shots = gameObject.AddComponent<ShotRunner>();
                shots.Host = host; shots.Rig = rig; shots.Sun = sun;
            }
        }
    }
}
