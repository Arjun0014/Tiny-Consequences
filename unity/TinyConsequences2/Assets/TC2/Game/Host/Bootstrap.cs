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
            camGo.AddComponent<AudioListener>();   // (the world's sound is heard at the camera: F10.1)
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
            var lights = views.AddComponent<LightsView>();   // (the night side's lights: session 27)
            lights.Host = host;
            var sky = views.AddComponent<Sky>();
            sky.Host = host; sky.Rig = rig; sky.Sun = sun;
            var objects = views.AddComponent<WorldObjectsView>();
            objects.Host = host; objects.Rig = rig;
            var volcanoes = views.AddComponent<VolcanoView>();
            volcanoes.Host = host; volcanoes.Rig = rig;
            var shocks = views.AddComponent<ShockView>();   // (session 32: a quake's wave and fallen buildings' dust)
            shocks.Host = host; shocks.Rig = rig;
            var puffs = views.AddComponent<PuffRenderer>();
            puffs.Rig = rig;
            var rain = views.AddComponent<RainView>();
            rain.Host = host; rain.Rig = rig;
            var wild = views.AddComponent<WildlifeView>();
            wild.Host = host; wild.Rig = rig;
            var stock = views.AddComponent<LivestockView>();
            stock.Host = host; stock.Rig = rig;
            var sea = views.AddComponent<SeaLifeView>();
            sea.Host = host; sea.Rig = rig;
            var audio = views.AddComponent<AudioView>();   // (the world's sound, an observer: F10.1)
            audio.Host = host; audio.Rig = rig;
            var aim = views.AddComponent<AimView>();   // (the powers as play: their marks on the ground, session 27)
            aim.Host = host; aim.Rig = rig;
            var realms = views.AddComponent<RealmsView>();   // (the realms map layer, D2-167: off until the HUD's Realms or R)
            realms.Host = host; realms.Rig = rig;

            var hud = gameObject.AddComponent<Hud>();
            hud.Host = host; hud.Rig = rig; hud.Objects = objects;
            var bridge = gameObject.AddComponent<WebBridge>();
            bridge.Host = host; bridge.Rig = rig;
            // (F11.2) the player's settings kept from the last visit - never in shots or batch runs, whose pictures and numbers stay their own
            if (ShotRunner.Arg("-tc2shots") == null && !Application.isBatchMode) Settings.Load();
            // (session 27, the person's word) shots and batch runs play in the background while the machine is in use: they are silent
            if (ShotRunner.Arg("-tc2shots") != null || Application.isBatchMode) AudioListener.volume = 0f;
            if (ShotRunner.Arg("-tc2shots") != null)
            {
                var shots = gameObject.AddComponent<ShotRunner>();
                shots.Host = host; shots.Rig = rig; shots.Sun = sun;
            }
        }
    }
}
