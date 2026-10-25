using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TC2.Game
{
    /// <summary>
    /// Light and air (13 §1, D2-026): the horizon colour from the sun's height where the camera is (blue day, warm dusk,
    /// dark night), aerial haze that thickens as the camera comes down, a sky-coloured background below the atmosphere
    /// (black space from orbit), the terrain's detail noise, and the colour grade (neutral tonemapping, a little more
    /// saturation and contrast, soft bloom, vignette). Presentation only.
    /// </summary>
    public sealed class Sky : MonoBehaviour
    {
        public static int GlCascades = 1;          // the WebGL2 fallback's shadow cascades (tools may change it: the bridge's `glshadow`)
        public static float GlShadowReach = 1.6f;  // and its shadow distance per metre of the camera's height
        public static int GpuCascades = 4;         // WebGPU's (and the desktop's) shadow cascades (tools: the bridge's `shadow`)
        public static float GpuShadowReach = 3.2f; // and their shadow distance per metre of the camera's height
        /// <summary>Tools (the frame pass, 14 §7; the bridge's `shadowlite`): above 3 km, two cascades over twice the camera's height - every visible building of a great
        /// world was drawn into four cascades out to 7 km at the region view (the shadow layer x0.53-0.69 of the frame); off: as above.</summary>
        public static bool ShadowLite;

        public SimHost Host;
        public CameraRig Rig;
        public Light Sun;
        static readonly int FogColor = Shader.PropertyToID("_TC2FogColor");
        static readonly int FogDensity = Shader.PropertyToID("_TC2FogDensity");
        static readonly int Detail = Shader.PropertyToID("_TC2Detail");
        static readonly int SunDir = Shader.PropertyToID("_TC2SunDir");
        static readonly int SeaRadius = Shader.PropertyToID("_TC2SeaRadius");
        static readonly int TiltShift = Shader.PropertyToID("_TC2TiltShift");
        static readonly int MoonDir = Shader.PropertyToID("_TC2MoonDir");
        static readonly int Canopy = Shader.PropertyToID("_TC2Canopy");
        static readonly int CanopyTile = Shader.PropertyToID("_TC2CanopyTile");
        static readonly int CanopyCover = Shader.PropertyToID("_TC2Cover");
        static readonly int CanopyK = Shader.PropertyToID("_TC2CanopyK");
        static readonly int CoverMean = Shader.PropertyToID("_TC2CoverMean");
        static readonly int StandCdf = Shader.PropertyToID("_TC2StandCdf");
        static readonly int AtmoGround = Shader.PropertyToID("_TC2AtmoGround");
        static readonly int AtmoTop = Shader.PropertyToID("_TC2AtmoTop");
        static readonly int AtmoH = Shader.PropertyToID("_TC2AtmoH");
        static readonly int AtmoBeta = Shader.PropertyToID("_TC2AtmoBeta");
        static readonly int AtmoSun = Shader.PropertyToID("_TC2AtmoSun");
        static readonly int AtmoFade = Shader.PropertyToID("_TC2AtmoFade");
        static readonly int AtmoOverGround = Shader.PropertyToID("_TC2AtmoOverGround");
        Volume _volume;
        public Color Horizon;

        void Start()
        {
            Shader.SetGlobalTexture(Detail, MakeDetail(256));
            Shader.SetGlobalTexture(Canopy, TC2.Game.Canopy.MakeTexture(512));   // the woods' crowns (D2-079)
            Shader.SetGlobalTexture(CanopyCover, TC2.Game.Canopy.MakeCover(256)); // and the ground they cover, averaged far off (D2-081)
            Shader.SetGlobalFloat(CanopyK, TC2.Game.Canopy.K);
            Shader.SetGlobalVector(CoverMean, TC2.Game.Canopy.CoverMean);
            Shader.SetGlobalFloat(CanopyTile, TC2.Game.Canopy.TileMetres);
            Shader.SetGlobalTexture(StandCdf, TC2.Game.Canopy.MakeStandCdf());   // the woods' stands and glades (D2-084)
            Shader.SetGlobalFloat(FogDensity, 0f);
            if (Application.isBatchMode) Debug.Log("[TC2] canopy K " + TC2.Game.Canopy.K.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            MakeGrade();
        }

        void MakeGrade()
        {
            var go = new GameObject("Grade");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(14f); ca.contrast.Override(9f); ca.postExposure.Override(0.12f);
            var bl = profile.Add<Bloom>(true); bl.intensity.Override(0.32f); bl.threshold.Override(1.05f); bl.scatter.Override(0.62f);
            var vg = profile.Add<Vignette>(true); vg.intensity.Override(0.18f); vg.smoothness.Override(0.45f);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            _volume.sharedProfile = profile;
            var data = Rig.Cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) { data.renderPostProcessing = true; data.requiresDepthTexture = false; }
        }

        void LateUpdate()
        {
            var w = Host.World;
            if (w == null || Rig == null || Rig.Cam == null || Sun == null) return;
            Vector3 cam = Rig.Cam.transform.position;
            float R = (float)w.Planet.Radius;
            float altitude = cam.magnitude - R;
            Vector3 up = cam.normalized, toSun = -Sun.transform.forward;
            float elev = Vector3.Dot(up, toSun);                 // sun height seen from under the camera
            float day = Mathf.Clamp01(elev * 3f + 0.25f);
            Color night = new Color(0.06f, 0.09f, 0.19f), noon = new Color(0.66f, 0.79f, 0.93f), dusk = new Color(0.93f, 0.62f, 0.42f);
            float duskness = Mathf.Clamp01(1f - Mathf.Abs(elev) * 4.5f) * Mathf.Clamp01(elev * 6f + 1f);
            Horizon = Color.Lerp(Color.Lerp(night, noon, day), dusk, duskness * 0.55f);
            // (D2-084: the haze kept to depth, not to softening the subject - about 40 % thinner than D2-026's)
            float top = (float)(14000.0 * w.Planet.GeoScale);   // (the air's top in the picture goes with the planet's size, D2-085)
            float density = altitude < top ? Mathf.Lerp(1f / 26000f, 1f / 240000f, Mathf.Sqrt(Mathf.InverseLerp(150f, top, altitude))) : 0f;
            Shader.SetGlobalVector(FogColor, new Vector4(Horizon.linear.r, Horizon.linear.g, Horizon.linear.b, day));
            Shader.SetGlobalFloat(FogDensity, density * (1f - AirFade(altitude)));
            Shader.SetGlobalVector(SunDir, toSun);
            // the moon: opposite the sun and a little to the side, so night has a light with a direction
            Vector3 moon = (Quaternion.AngleAxis(150f, Vector3.up) * toSun + Vector3.up * 0.25f).normalized;
            Shader.SetGlobalVector(MoonDir, moon);
            // the miniature cue: a hint at the frame's top and bottom close to the ground, gone by the village view (D2-084: it softened
            // whole views - the miniature must not cost clarity)
            Shader.SetGlobalFloat(TiltShift, 0.55f * (1f - Mathf.InverseLerp(100f, 1100f, altitude)));
            Shader.SetGlobalFloat(SeaRadius, (float)(w.Planet.Radius + Ground.WaterHeight(w.Planet, w.Planet.SeaLevel)));
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                // the WebGL2 fallback draws its shadows nearer and in two cascades: there the shadow pass of the woods was the
                // frame (37 fps in the village view, 60 without shadows; WebGPU keeps four cascades)
                bool gl = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
                bool lite = ShadowLite && !gl && altitude > 3000f;
                int cascades = gl ? GlCascades : lite ? Mathf.Min(2, GpuCascades) : GpuCascades;
                if (urp.shadowCascadeCount != cascades) urp.shadowCascadeCount = cascades;
                urp.shadowDistance = gl ? Mathf.Clamp(altitude * GlShadowReach, 200f, 3000f) : Mathf.Clamp(altitude * (lite ? 2.0f : GpuShadowReach), 250f, 7000f);
            }
            // the air seen from afar (Atmosphere.shader): Rayleigh's blue scattered along each ray, in an atmosphere whose scale height goes
            // with the planet; it takes over from the views' haze and sky as the camera rises past the village view
            float Rg = (float)(w.Planet.Radius + Ground.WaterHeight(w.Planet, w.Planet.SeaLevel));
            float H = Rg * Look.AtmoH, k = Look.AtmoTau / H;
            float air = AirFade(altitude);
            Shader.SetGlobalFloat(AtmoGround, Rg);
            Shader.SetGlobalFloat(AtmoTop, Look.AirTop(Rg));
            Shader.SetGlobalFloat(AtmoH, H);
            Shader.SetGlobalVector(AtmoBeta, new Vector4(0.040f * k, 0.095f * k, 0.230f * k, Look.AtmoMie / (H * 0.33f)));
            Shader.SetGlobalFloat(AtmoSun, Look.AtmoSun);
            Shader.SetGlobalFloat(AtmoFade, air);
            Shader.SetGlobalFloat(AtmoOverGround, Look.AtmoGround);
            // below the air the empty sky is the sky; from higher up the air's own light against space
            float sky = 1f - air;
            Color zenith = Color.Lerp(new Color(0.01f, 0.015f, 0.035f), new Color(0.30f, 0.50f, 0.82f), day);
            Rig.Cam.backgroundColor = Color.Lerp(new Color(0.012f, 0.016f, 0.035f), Color.Lerp(zenith, Horizon, 0.45f), sky);
        }

        /// <summary>How far the drawn air (Atmosphere.shader) has taken over from the views' own haze and sky: nothing at the village view
        /// (900 m) and below, all of it from 2.2 km up.</summary>
        static float AirFade(float altitude) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(900f, 2200f, altitude));

        /// <summary>A tileable value-noise texture (four octaves) for small-scale ground detail.</summary>
        static Texture2D MakeDetail(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.R8, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "tc2-detail" };
            var px = new byte[size * size];
            var acc = new float[size * size];
            var rng = new System.Random(20261005);
            float amp = 1f, total = 0f;
            for (int o = 0; o < 5; o++)
            {
                int L = 8 << o;
                var lat = new float[L * L];
                for (int k = 0; k < lat.Length; k++) lat[k] = (float)rng.NextDouble();
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float fx = x * L / (float)size, fy = y * L / (float)size;
                        int x0 = (int)fx, y0 = (int)fy;
                        float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                        int x1 = (x0 + 1) % L, y1 = (y0 + 1) % L;
                        float a = lat[y0 * L + x0], b = lat[y0 * L + x1], c = lat[y1 * L + x0], d = lat[y1 * L + x1];
                        acc[y * size + x] += amp * Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                    }
                total += amp; amp *= 0.55f;
            }
            for (int k = 0; k < px.Length; k++) px[k] = (byte)Mathf.Clamp(Mathf.RoundToInt(acc[k] / total * 255f), 0, 255);
            tex.SetPixelData(px, 0);
            tex.Apply(true, true);
            return tex;
        }
    }
}
