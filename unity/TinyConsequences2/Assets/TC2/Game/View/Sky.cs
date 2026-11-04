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
        /// <summary>The light shadows (the player's setting "lighter shadows from high up", the bridge's `shadowlite`; session 27: the default): four
        /// cascades only below 300 m, two below 900 m, one above, over reaches matched to the view - each cascade is a pass over every caster (s27perf:
        /// one cascade x1.51 of the village view's frame on the integrated GPU); off: four cascades over 3.2x the camera's height at every view.</summary>
        public static bool ShadowLite = true;

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
        static readonly int NightLift = Shader.PropertyToID("_TC2NightLift");
        Volume _volume;
        static readonly int LandVaryId = Shader.PropertyToID("_TC2LandVary");
        static readonly int StandFarId = Shader.PropertyToID("_TC2StandFar"), StandGrainId = Shader.PropertyToID("_TC2StandGrain");
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
            _ca = profile.Add<ColorAdjustments>(true); _ca.saturation.Override(Look.GradeSat); _ca.contrast.Override(Look.GradeContrast); _ca.postExposure.Override(Look.GradeExposure);
            // (session 31) greens taken down and a touch toward olive (the hues of leaf and field, by a curve over hue: 0.25-0.45); the rest as it is
            _cc = profile.Add<ColorCurves>(true);
            SetGreenCurve(Look.GradeGreen);
            var st = profile.Add<SplitToning>(true); st.shadows.Override(new Color(0.46f, 0.50f, 0.60f)); st.highlights.Override(new Color(0.58f, 0.53f, 0.46f)); st.balance.Override(10f);
            var bl = profile.Add<Bloom>(true); bl.intensity.Override(0.32f); bl.threshold.Override(1.05f); bl.scatter.Override(0.62f);
            var vg = profile.Add<Vignette>(true); vg.intensity.Override(0.20f); vg.smoothness.Override(0.5f);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            _volume.sharedProfile = profile;
            var data = Rig.Cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) { data.renderPostProcessing = true; data.requiresDepthTexture = false; }
        }

        ColorAdjustments _ca; ColorCurves _cc; float _gradeGreen = -1f;

        void SetGreenCurve(float v)
        {
            _gradeGreen = v;
            _cc.hueVsSat.Override(new TextureCurve(new[] { new Keyframe(0.15f, 0.5f), new Keyframe(0.24f, v), new Keyframe(0.42f, v), new Keyframe(0.52f, 0.5f) }, 0.5f, true, new Vector2(0f, 1f)));
        }

        // (session 31) the stars behind the planet from high up: a mesh of small quads, each a direction, a colour and a size; a faint band across the sky where they gather
        GameObject _starsGo; Material _starsMat;
        static readonly int StarFade = Shader.PropertyToID("_TC2StarFade");

        void Stars(float air)
        {
            if (_starsGo == null)
            {
                var sh = Resources.Load<Shader>("TC2Shaders/Stars");
                if (sh == null) return;
                _starsMat = new Material(sh);
                const int N = 2600;
                var v = new Vector3[N * 4]; var c = new Color[N * 4]; var u = new Vector2[N * 4]; var t = new int[N * 6];
                var rng = new System.Random(20261101);
                Vector3 band = new Vector3(0.35f, 0.82f, 0.45f).normalized;
                for (int i = 0; i < N; i++)
                {
                    Vector3 d;
                    while (true)
                    {
                        d = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                        float m = d.sqrMagnitude;
                        if (m > 1f || m < 1e-4f) continue;
                        d /= Mathf.Sqrt(m);
                        // (half of them gather near a great circle: the galaxy's band)
                        float off = Mathf.Abs(Vector3.Dot(d, band));
                        if (i % 2 == 0 || (float)rng.NextDouble() > off * 3.5f) break;
                    }
                    float mag = (float)rng.NextDouble();
                    float size = Mathf.Lerp(0.55f, 1.0f, mag * mag * mag);
                    float bright = Mathf.Lerp(0.20f, 0.95f, mag * mag * mag * mag);
                    float tint = (float)rng.NextDouble();
                    Color col = tint < 0.18f ? new Color(1f, 0.84f, 0.70f) : tint < 0.42f ? new Color(0.78f, 0.86f, 1f) : new Color(0.96f, 0.96f, 1f);
                    col *= bright; col.a = size;
                    for (int k = 0; k < 4; k++) { v[i * 4 + k] = d; c[i * 4 + k] = col; }
                    u[i * 4] = new Vector2(-1f, -1f); u[i * 4 + 1] = new Vector2(1f, -1f); u[i * 4 + 2] = new Vector2(1f, 1f); u[i * 4 + 3] = new Vector2(-1f, 1f);
                    t[i * 6] = i * 4; t[i * 6 + 1] = i * 4 + 1; t[i * 6 + 2] = i * 4 + 2; t[i * 6 + 3] = i * 4; t[i * 6 + 4] = i * 4 + 2; t[i * 6 + 5] = i * 4 + 3;
                }
                var mesh = new Mesh { name = "tc2-stars", vertices = v, colors = c, uv = u, triangles = t };
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                _starsGo = new GameObject("Stars");
                _starsGo.transform.SetParent(transform, false);
                _starsGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = _starsGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _starsMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            bool show = air > 0.02f;
            if (_starsGo.activeSelf != show) _starsGo.SetActive(show);
            if (show) Shader.SetGlobalFloat(StarFade, air);
        }

        // (session 31) the sky behind everything near the ground: pale at the horizon, deepening above it, a glow toward the sun (SkyDome.shader)
        GameObject _dome; Material _domeMat;
        static readonly int SkyHorizon = Shader.PropertyToID("_TC2SkyHorizon"), SkyZenith = Shader.PropertyToID("_TC2SkyZenith");

        void Dome(float air, float elev, Color horizon, Color zenith, Vector3 cam)
        {
            if (_dome == null)
            {
                var sh = Resources.Load<Shader>("TC2Shaders/SkyDome");
                if (sh == null) return;
                _domeMat = new Material(sh);
                _dome = new GameObject("SkyDome");
                _dome.transform.SetParent(transform, false);
                _dome.AddComponent<MeshFilter>().sharedMesh = ProcMeshes.Sphere(1f, 16, 32);
                var mr = _dome.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _domeMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            bool show = air < 0.995f;
            if (_dome.activeSelf != show) _dome.SetActive(show);
            if (!show) return;
            _dome.transform.position = cam;
            _dome.transform.localScale = Vector3.one * Mathf.Min(Rig.Cam.farClipPlane * 0.8f, 200000f);
            var hl = horizon.linear; var zl = zenith.linear;
            _domeMat.SetVector(SkyHorizon, new Vector4(hl.r, hl.g, hl.b, 1f - air));
            _domeMat.SetVector(SkyZenith, new Vector4(zl.r, zl.g, zl.b, Mathf.Clamp01(1f - elev * 3.2f)));
        }

        void LateUpdate()
        {
            var w = Host.World;
            if (w == null || Rig == null || Rig.Cam == null || Sun == null) return;
            Vector3 cam = Rig.Cam.transform.position;
            if (_ca != null) { _ca.saturation.value = Look.GradeSat; _ca.contrast.value = Look.GradeContrast; _ca.postExposure.value = Look.GradeExposure; if (_gradeGreen != Look.GradeGreen) SetGreenCurve(Look.GradeGreen); }
            Shader.SetGlobalFloat(LandVaryId, Look.LandVary);
            Shader.SetGlobalFloat(StandFarId, Look.StandFar); Shader.SetGlobalFloat(StandGrainId, Look.StandGrain);   // (session 32)
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
            Shader.SetGlobalVector(SunDir, new Vector4(toSun.x, toSun.y, toSun.z, Mathf.Clamp01(elev * 6f + 0.5f)));
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
                // (session 27, s27perf on the integrated GPU: each cascade is a pass over every caster - one cascade x1.51 of the village view's frame,
                // two x1.29, while halving the reach gave x1.02) the light shadows, the default: four cascades only close in, where a shadow's detail
                // shows (below 300 m), two below 900 m, one above, each over a reach matched to the view
                bool lite = ShadowLite && !gl;
                int cascades = gl ? GlCascades : !lite ? GpuCascades : altitude < 300f ? GpuCascades : altitude < 900f ? 2 : 1;
                float reach = !lite ? GpuShadowReach : altitude < 300f ? GpuShadowReach : altitude < 900f ? 2.0f : 1.25f;
                if (urp.shadowCascadeCount != cascades) urp.shadowCascadeCount = cascades;
                urp.shadowDistance = gl ? Mathf.Clamp(altitude * GlShadowReach, 200f, 3000f) : Mathf.Clamp(altitude * reach, 250f, 7000f);
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
            // (session 27) the night's sky and moon light on the ground: all of it near the ground (a village at night must read), little from orbit,
            // where the night side is dark but for its lights
            Shader.SetGlobalFloat(NightLift, Mathf.Lerp(1f, Look.NightFar, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1200f, 7000f, altitude))));
            // below the air the empty sky is the sky; from higher up the air's own light against space
            float sky = 1f - air;
            Color zenith = Color.Lerp(new Color(0.01f, 0.015f, 0.035f), new Color(0.30f, 0.50f, 0.82f), day);
            Rig.Cam.backgroundColor = Color.Lerp(new Color(0.012f, 0.016f, 0.035f), Color.Lerp(zenith, Horizon, 0.45f), sky);
            Dome(air, elev, Horizon, zenith, cam);
            Stars(air);
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
