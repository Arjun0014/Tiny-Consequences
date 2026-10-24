using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Clouds from the world's own weather (13 §2, D2-027): a coverage cubemap filled from each cell's weather - red the decks
    /// where it rains today and the storms' bands, green the fair-weather cumulus the climate's moist air carries on dry days
    /// (session 25: the planet from space) - eased toward each new day's weather so it never pops, one cube face per frame; a
    /// shell above the land drawn with drifting forms drawn out along the latitude bands' winds (Clouds.shader; Look's
    /// tunables), gone when the camera is below it; the same coverage darkens the ground as cloud shadow. Reads the world only.
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
        float[][] _now, _target, _nowG, _targetG;
        Color[] _px;
        int _face;
        readonly float[] _nextFill = new float[6];
        GameObject _shell;
        Material _mat;
        static readonly int CoverId = Shader.PropertyToID("_TC2Clouds");
        static readonly int ShellId = Shader.PropertyToID("_TC2CloudShell");
        static readonly int FadeId = Shader.PropertyToID("_TC2CloudFade");
        static readonly int CellsId = Shader.PropertyToID("_TC2Cells");
        static readonly int FormId = Shader.PropertyToID("_TC2CloudForm");
        static readonly int ScaleId = Shader.PropertyToID("_TC2CloudScale");
        const float FormMetres = 4000f;   // the forms' noise tile along a meridian (m; its finest octave ~1/64 of it)

        void Start()
        {
            _cover = new Cubemap(Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-clouds" };
            _now = new float[6][]; _target = new float[6][]; _nowG = new float[6][]; _targetG = new float[6][];
            for (int f = 0; f < 6; f++) { _now[f] = new float[Size * Size]; _target[f] = new float[Size * Size]; _nowG[f] = new float[Size * Size]; _targetG[f] = new float[Size * Size]; }
            _px = new Color[Size * Size];
            Shader.SetGlobalTexture(CoverId, _cover);
            Shader.SetGlobalTexture(CellsId, MakeCells(128, 8));
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
            // one face a frame: its target from today's weather (refreshed each second), then ease toward it
            int f = _face; _face = (_face + 1) % 6;
            var now = _now[f]; var target = _target[f]; var nowG = _nowG[f]; var targetG = _targetG[f];
            if (Time.unscaledTime >= _nextFill[f]) { FillTarget(P, f, target, targetG); _nextFill[f] = Time.unscaledTime + 1f; }
            float k = Mathf.Clamp01(Time.unscaledDeltaTime * 6f * 0.35f);
            for (int i = 0; i < now.Length; i++)
            {
                now[i] += (target[i] - now[i]) * k;
                nowG[i] += (targetG[i] - nowG[i]) * k;
                _px[i] = new Color(now[i], nowG[i], 0f, 1f);
            }
            _cover.SetPixels(_px, (CubemapFace)f);
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
            // the forms' noise: whole tiles round each parallel (no seam), drawn out east-west by Look.CloudStretch
            float around = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * (R + shell) / (Mathf.Max(0.5f, Look.CloudStretch) * FormMetres)));
            Shader.SetGlobalVector(FormId, new Vector4(Look.CloudDeck, Look.CloudCu, Look.CloudShade, around));
            Shader.SetGlobalVector(ScaleId, new Vector4(FormMetres, 2f * Mathf.PI * (R + shell) / around, R + shell, 0f));
        }

        /// <summary>A tileable cellular texture (Worley F1: the distance to the nearest of a jittered grid's points, in cell widths) for
        /// the cumulus fields' puffs.</summary>
        static Texture2D MakeCells(int size, int cells)
        {
            var tex = new Texture2D(size, size, TextureFormat.R8, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2, name = "tc2-cells" };
            var rng = new System.Random(20261024);
            var pts = new Vector2[cells * cells];
            for (int k = 0; k < pts.Length; k++) pts[k] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
            var px = new byte[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) * cells / size, fy = (y + 0.5f) * cells / size;
                    int cx = (int)fx, cy = (int)fy;
                    float best = 9f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int gx = cx + dx, gy = cy + dy;
                            var pt = pts[((gy % cells + cells) % cells) * cells + ((gx % cells + cells) % cells)];
                            float ex = gx + pt.x - fx, ey = gy + pt.y - fy;
                            best = Mathf.Min(best, ex * ex + ey * ey);
                        }
                    px[y * size + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(best) * 255f), 0, 255);
                }
            tex.SetPixelData(px, 0);
            tex.Apply(true, true);
            return tex;
        }

        void FillTarget(PlanetState P, int face, float[] target, float[] targetG)
        {
            var g = P.Grid;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                    Vector3 d = FaceDir(face, u, v).normalized;
                    var dir = new Vec3d(d.x, d.y, d.z);
                    int c = g.CellOf(dir);
                    // where it rains there is a deck of cloud; the climate's damp air carries fair-weather cumulus on dry days, the
                    // fewer the drier (smoothed over cells)
                    int n = Ground.ColourWeights(P, dir, c, _wc, _ww);
                    float cover = 0, cu = 0;
                    for (int q = 0; q < n; q++)
                    {
                        int cq = _wc[q];
                        float rain = Mathf.Clamp01(P.RainToday[cq] * 0.85f);
                        cover += _ww[q] * rain;
                        cu += _ww[q] * Mathf.Clamp01((P.Moist[cq] - 0.35f) * 1.3f) * (1f - rain);
                    }
                    targetG[y * Size + x] = cu;
                    // storms: a dense ring round a clear eye, spiral bands outward (turning with the hemisphere)
                    for (int k = 0; k < P.Storms.Count; k++)
                    {
                        var st = P.Storms[k];
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
                        cover = Mathf.Max(cover, sc * strength);
                    }
                    target[y * Size + x] = cover;
                }
        }
    }
}
