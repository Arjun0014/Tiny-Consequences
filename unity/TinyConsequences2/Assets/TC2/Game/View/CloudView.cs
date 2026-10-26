using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Clouds from the world's own weather (13 §2, D2-027; session 25: the planet from space; session 27: what the sky holds read four ways): a cover
    /// cubemap filled from each cell's weather - r the decks where it rains today and the storms' bands, g fair-weather cumulus where the sun heats
    /// moist ground (and over warm moist seas all day), b the layered cloud of a damp day: the weather's own draw (<see cref="PlanetSystems.DayAir"/>)
    /// near rain - the shields round the rain and the grey of the days that nearly rained, more of them where rain is likelier -, a how warm and
    /// convective the air is (heaped towers in warm rain, smooth sheets in cool). Eased toward each new day's weather so it never pops, one cube face a
    /// frame. A shell above the land is drawn with the forms of a 3-D noise (<see cref="CloudNoise"/>, TC2CloudForm.hlsl), gone when the camera is
    /// below it; the same forms shade the ground, the sea and what stands on them. Reads the world only; writes nothing in it.
    /// </summary>
    public sealed class CloudView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        const int Size = 64;
        const float ShellHeight = 380f;    // m above the reference sphere, drawn (on the 10 km world; session 25: 650 m floated the clouds as a shell apart
                                           // from the planet at its limb - the high ground pierces them now)
        readonly int[] _wc = new int[9];
        readonly float[] _ww = new float[9];
        Cubemap _cover;
        float[][] _now, _target;           // per face, four channels interleaved (deck, cumulus, layer, warmth)
        readonly bool[] _filled = new bool[6];
        Color[] _px;
        int _face;
        readonly float[] _nextFill = new float[6];
        GameObject _shell;
        Material _mat;
        // the day's air (PlanetSystems.DayAir), worked out once a weather day
        double[] _blocks;
        float[] _draw, _chance;
        long _airDay = long.MinValue;
        int _airVersion = -1;
        static readonly int CoverId = Shader.PropertyToID("_TC2Clouds");
        static readonly int NoiseId = Shader.PropertyToID("_TC2CloudNoise");
        static readonly int ShellId = Shader.PropertyToID("_TC2CloudShell");
        static readonly int FadeId = Shader.PropertyToID("_TC2CloudFade");
        static readonly int FormId = Shader.PropertyToID("_TC2CloudForm");
        static readonly int ScaleId = Shader.PropertyToID("_TC2CloudScale");

        void Start()
        {
            _cover = new Cubemap(Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-clouds" };
            _now = new float[6][]; _target = new float[6][];
            for (int f = 0; f < 6; f++) { _now[f] = new float[Size * Size * 4]; _target[f] = new float[Size * Size * 4]; }
            _px = new Color[Size * Size];
            Shader.SetGlobalTexture(CoverId, _cover);
            var t0 = Time.realtimeSinceStartup;
            Shader.SetGlobalTexture(NoiseId, CloudNoise.Make(64));
            if (Application.isBatchMode) Debug.Log("[TC2] cloud noise made in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            _mat = Materials.Load("Clouds");
            _shell = new GameObject("Clouds");
            _shell.transform.SetParent(transform, false);
            _shell.AddComponent<MeshFilter>().sharedMesh = ProcMeshes.Sphere(1f, 96, 192);
            var mr = _shell.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        static Vector3 FaceDir(int face, float u, float v)
        {
            // Unity cubemap face orientation (u right, v down on each face)
            switch ((CubemapFace)face)
            {
                case CubemapFace.PositiveX: return new Vector3(1, -v, -u);
                case CubemapFace.NegativeX: return new Vector3(-1, -v, u);
                case CubemapFace.PositiveY: return new Vector3(u, 1, v);
                case CubemapFace.NegativeY: return new Vector3(u, -1, -v);
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1);
                default: return new Vector3(-u, -v, -1);
            }
        }

        void Update()
        {
            var w = Host.World;
            if (w == null || Rig == null || Rig.Cam == null) return;
            var P = w.Planet;
            // the day's air: the weather's own draw, once a weather day (and again for a new world)
            long day = SimTime.FloorDiv(w.Now, SimTime.Day);
            if (day != _airDay || _airVersion != Host.WorldVersion)
            {
                if (_draw == null || _draw.Length != P.Cells) { _draw = new float[P.Cells]; _chance = new float[P.Cells]; }
                if (_blocks == null || _blocks.Length != w.PlanetSys.WeatherBlocks) _blocks = new double[w.PlanetSys.WeatherBlocks];
                w.PlanetSys.DayAir(w, day, _blocks, _draw, _chance);
                if (_airVersion != Host.WorldVersion) for (int f = 0; f < 6; f++) { _filled[f] = false; _nextFill[f] = 0f; }
                _airDay = day; _airVersion = Host.WorldVersion;
            }
            // one face a frame: its target from today's weather (refreshed each second), then ease toward it (a face's first fill is taken at once)
            int fc = _face; _face = (_face + 1) % 6;
            var now = _now[fc]; var target = _target[fc];
            if (Time.unscaledTime >= _nextFill[fc]) { FillTarget(w, fc, target); _nextFill[fc] = Time.unscaledTime + 1f; }
            float k = _filled[fc] ? Mathf.Clamp01(Time.unscaledDeltaTime * 6f * 0.35f) : 1f;
            _filled[fc] = true;
            for (int i = 0, j = 0; i < _px.Length; i++, j += 4)
            {
                now[j] += (target[j] - now[j]) * k; now[j + 1] += (target[j + 1] - now[j + 1]) * k;
                now[j + 2] += (target[j + 2] - now[j + 2]) * k; now[j + 3] += (target[j + 3] - now[j + 3]) * k;
                _px[i] = new Color(now[j], now[j + 1], now[j + 2], now[j + 3]);
            }
            _cover.SetPixels(_px, (CubemapFace)fc);
            _cover.Apply(false);
            // the shell above the land; it fades as the camera comes down under it
            float R = (float)P.Radius;
            float shell = (float)(ShellHeight * P.GeoScale);   // (with the planet's size, as its drawn relief: D2-085)
            _shell.transform.localScale = Vector3.one * (R + shell);
            float alt = Rig.Cam.transform.position.magnitude - R;
            // (session 25: gone at the village view and below - the clouds smeared it - and all there from the region view)
            float fade = Mathf.InverseLerp(1100f, 2400f, alt) * Mathf.InverseLerp(shell * 1.05f, shell * 2.2f, alt);
            _shell.SetActive(fade > 0.01f);
            Shader.SetGlobalFloat(FadeId, fade);
            Shader.SetGlobalFloat(ShellId, R + shell);
            Shader.SetGlobalVector(FormId, new Vector4(Look.CloudDeck * Settings.Clouds, Look.CloudCu * Settings.Clouds, Look.CloudShade, Mathf.Max(1f, Look.CloudStretch)));   // (x the player's clouds: F11.2)
            Shader.SetGlobalVector(ScaleId, new Vector4(Look.CloudTile, R + shell, Time.unscaledTime, Look.CloudShadow));
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        void FillTarget(TC2.Core.Simulation.World w, int face, float[] target)
        {
            var P = w.Planet;
            var g = P.Grid;
            Vector3 toSun = Rig.Sun != null ? -Rig.Sun.transform.forward : Vector3.up;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                    Vector3 d = FaceDir(face, u, v).normalized;
                    var dir = new Vec3d(d.x, d.y, d.z);
                    int c = g.CellOf(dir);
                    // the sun's heating of the ground under it: cumulus build over land by day and die away at dusk
                    float heat = Smooth(0.12f, 0.65f, Vector3.Dot(d, toSun));
                    int n = Ground.ColourWeights(P, dir, c, _wc, _ww);
                    float deck = 0, cu = 0, layer = 0, warm = 0;
                    for (int q = 0; q < n; q++)
                    {
                        int cq = _wc[q];
                        float wq = _ww[q];
                        float rain = Mathf.Clamp01(P.RainToday[cq] * 0.85f);
                        // the day's air: overcast where the weather's draw came near rain, clear where it lay far above the chance
                        float ch = _chance[cq];
                        float damp = 1f - Smooth(ch, ch + 0.07f + 0.45f * ch, _draw[cq]);
                        float t = P.Temp[cq];
                        float tw = Smooth(4f, 24f, t);
                        float moist = Mathf.Clamp01((P.Moist[cq] - 0.25f) * 1.5f);
                        float sunOn = P.IsLand(cq) ? heat : 0.6f;   // (the sea keeps its warmth through the night: trade cumulus all day)
                        deck += wq * rain;
                        layer += wq * damp * (1f - rain);
                        cu += wq * moist * tw * sunOn * (1f - rain) * (1f - 0.6f * damp);
                        warm += wq * Smooth(12f, 26f, t);
                    }
                    // storms: a dense ring round a clear eye, spiral bands outward (turning with the hemisphere); their air convective
                    for (int s = 0; s < P.Storms.Count; s++)
                    {
                        var st = P.Storms[s];
                        double dot = Vec3d.Dot(dir, st.At);
                        double reach = st.Radius * 1.25 / P.Radius;
                        if (dot < System.Math.Cos(reach)) continue;
                        float t = (float)(System.Math.Acos(System.Math.Min(1.0, dot)) * P.Radius / st.Radius);
                        TC2.Core.Planet.Storms.Frame(st.At, out Vec3d e, out Vec3d nn);
                        float ang = (float)System.Math.Atan2(Vec3d.Dot(dir, nn), Vec3d.Dot(dir, e));
                        float spin = st.At.Y >= 0 ? 1f : -1f;
                        float arm = 0.5f + 0.5f * Mathf.Cos(2f * ang + spin * 5f * Mathf.Log(t + 0.06f) + st.Id * 1.7f);
                        float strength = Mathf.Clamp01(st.Wind / 45f + 0.25f);
                        float sc;
                        if (t < 0.07f * (1f + 25f / Mathf.Max(20f, st.Wind))) sc = 0.05f;              // the eye
                        else if (t < 0.38f) sc = 1f;                                                      // the eyewall's ring
                        else sc = Mathf.Clamp01(arm * 1.3f - 0.15f) * Mathf.Clamp01((1.25f - t) * 1.6f);   // the bands
                        deck = Mathf.Max(deck, sc * strength);
                        if (sc * strength > 0.3f) { warm = Mathf.Max(warm, strength); layer *= 0.4f; cu *= 0.2f; }
                    }
                    int o = (y * Size + x) * 4;
                    target[o] = deck; target[o + 1] = cu; target[o + 2] = layer; target[o + 3] = warm;
                }
        }
    }
}
