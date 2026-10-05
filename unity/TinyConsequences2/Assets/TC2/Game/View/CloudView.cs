using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Clouds from the world's own weather (13 §2, D2-027): a coverage cubemap filled from each cell's rain today and
    /// moisture (the rain falls where the clouds are), eased toward each new day's weather so it never pops, one cube
    /// face per frame; a soft shell above the land drawn with drifting procedural detail, gone when the camera is below
    /// it; the same coverage darkens the ground as cloud shadow. Reads the world only.
    /// </summary>
    public sealed class CloudView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        const int Size = 64;
        const float ShellHeight = 650f;    // m above the reference sphere, drawn
        readonly int[] _wc = new int[9];
        readonly float[] _ww = new float[9];
        Cubemap _cover;
        float[][] _now, _target;
        Color[] _px;
        int _face;
        readonly float[] _nextFill = new float[6];
        GameObject _shell;
        Material _mat;
        static readonly int CoverId = Shader.PropertyToID("_TC2Clouds");
        static readonly int ShellId = Shader.PropertyToID("_TC2CloudShell");
        static readonly int FadeId = Shader.PropertyToID("_TC2CloudFade");

        void Start()
        {
            _cover = new Cubemap(Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-clouds" };
            _now = new float[6][]; _target = new float[6][];
            for (int f = 0; f < 6; f++) { _now[f] = new float[Size * Size]; _target[f] = new float[Size * Size]; }
            _px = new Color[Size * Size];
            Shader.SetGlobalTexture(CoverId, _cover);
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
            var now = _now[f]; var target = _target[f];
            if (Time.unscaledTime >= _nextFill[f]) { FillTarget(P, f, target); _nextFill[f] = Time.unscaledTime + 1f; }
            float k = Mathf.Clamp01(Time.unscaledDeltaTime * 6f * 0.35f);
            for (int i = 0; i < now.Length; i++)
            {
                now[i] += (target[i] - now[i]) * k;
                float c = now[i];
                _px[i] = new Color(c, c, c, 1f);
            }
            _cover.SetPixels(_px, (CubemapFace)f);
            _cover.Apply(false);
            // the shell above the land; it fades as the camera comes down under it
            float R = (float)P.Radius;
            _shell.transform.localScale = Vector3.one * (R + ShellHeight);
            float alt = Rig.Cam.transform.position.magnitude - R;
            float fade = Mathf.InverseLerp(ShellHeight * 1.05f, ShellHeight * 2.2f, alt);
            _shell.SetActive(fade > 0.01f);
            Shader.SetGlobalFloat(FadeId, fade);
            Shader.SetGlobalFloat(ShellId, R + ShellHeight);
        }

        void FillTarget(PlanetState P, int face, float[] target)
        {
            var g = P.Grid;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                    Vector3 d = FaceDir(face, u, v).normalized;
                    var dir = new Vec3d(d.x, d.y, d.z);
                    int c = g.CellOf(dir);
                    // where it rains there is cloud; damp air carries some cloud even on dry days (smoothed over cells)
                    int n = Ground.ColourWeights(P, dir, c, _wc, _ww);
                    float cover = 0;
                    for (int q = 0; q < n; q++)
                    {
                        int cq = _wc[q];
                        cover += _ww[q] * Mathf.Clamp01(P.RainToday[cq] * 0.75f + Mathf.Max(0f, P.Moist[cq] - 0.35f) * 0.55f);
                    }
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
