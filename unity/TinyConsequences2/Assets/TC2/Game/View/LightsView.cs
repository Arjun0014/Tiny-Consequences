using TC2.Core.Foundation;
using TC2.Core.Places;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 27: the planet from orbit) The lights of the night side: a cubemap of where people keep a fire - every home lived in, a soft point
    /// at its lot - read from the world's buildings again when they change (at most every few seconds). The ground's shader (Terrain.shader) lays
    /// it on the land after dusk as a warm glow, from the region view outward, where the houses' own windows are too small to see: each place
    /// shines in the shape of its streets, a town brighter than a hamlet, an abandoned village dark. Reads the world only.
    /// </summary>
    public sealed class LightsView : MonoBehaviour
    {
        public SimHost Host;
        const int Size = 512;              // ~18 m a texel on the 6 km planet - about a home's spacing, so each home is its own point and a place shines in the
                                           // shape of its streets (session 27: at 256, ~37 m, a town's core summed past white into one even blob)
        Cubemap _lights;
        float[][] _acc;
        byte[] _px;
        long _sig = long.MinValue;
        float _next;
        int _version = -1;
        static readonly int LightsId = Shader.PropertyToID("_TC2Lights");
        public int Homes;                  // homes lit at the last fill (shots' log)

        void Start()
        {
            _lights = new Cubemap(Size, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-lights" };   // (one byte a texel: the refill stays cheap)
            _acc = new float[6][];
            for (int f = 0; f < 6; f++) _acc[f] = new float[Size * Size];
            _px = new byte[Size * Size];
            Shader.SetGlobalTexture(LightsId, _lights);
        }

        void Update()
        {
            var w = Host.World;
            if (w == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 3f;
            // what lights the land: the homes lived in, each where it stands (a cheap signature: count and the sum of their ids)
            long sig = 0; int n = 0;
            for (int b = w.Buildings.First(); b != 0; b = w.Buildings.Next(b))
            {
                ref var bb = ref w.Buildings.Get(b);
                if (bb.Household == 0 || bb.Done == 0 || !Structures.IsHome(bb.Kind)) continue;
                sig = sig * 31L + b; n++;
            }
            sig = sig * 31L + n;
            if (sig == _sig && _version == Host.WorldVersion) return;
            _sig = sig; _version = Host.WorldVersion;
            Fill(w);
        }

        void Fill(TC2.Core.Simulation.World w)
        {
            for (int f = 0; f < 6; f++) System.Array.Clear(_acc[f], 0, _acc[f].Length);
            double R = w.Planet.Radius;
            Homes = 0;
            for (int b = w.Buildings.First(); b != 0; b = w.Buildings.Next(b))
            {
                ref var bb = ref w.Buildings.Get(b);
                if (bb.Household == 0 || bb.Done == 0 || !Structures.IsHome(bb.Kind) || !w.Settlements.Exists(bb.Settlement)) continue;
                ref var s = ref w.Settlements.Get(bb.Settlement);
                Vec3d d = s.LocalToSphere(bb.X, bb.Y, R);
                Splat(d, bb.Kind >= Construction.House ? 0.55f : 0.35f);
                Homes++;
            }
            for (int f = 0; f < 6; f++)
            {
                var a = _acc[f];
                for (int i = 0; i < a.Length; i++) _px[i] = (byte)(Mathf.Clamp01(a[i]) * 255f + 0.5f);
                _lights.SetPixelData(_px, 0, (CubemapFace)f);   // (rows as CloudView fills its faces: v down)
            }
            _lights.Apply(false);
        }

        /// <summary>A home's light: a soft point over the texel its direction falls in and its neighbours (bilinear weights).</summary>
        void Splat(Vec3d d, float amount)
        {
            double ax = System.Math.Abs(d.X), ay = System.Math.Abs(d.Y), az = System.Math.Abs(d.Z);
            int face; double u, v;
            // the inverse of CloudView.FaceDir (Unity's cube faces: u right, v down)
            if (ax >= ay && ax >= az) { if (d.X > 0) { face = 0; u = -d.Z / ax; v = -d.Y / ax; } else { face = 1; u = d.Z / ax; v = -d.Y / ax; } }
            else if (ay >= az) { if (d.Y > 0) { face = 2; u = d.X / ay; v = d.Z / ay; } else { face = 3; u = d.X / ay; v = -d.Z / ay; } }
            else { if (d.Z > 0) { face = 4; u = d.X / az; v = -d.Y / az; } else { face = 5; u = -d.X / az; v = -d.Y / az; } }
            float fx = (float)((u + 1.0) * 0.5 * Size - 0.5), fy = (float)((v + 1.0) * 0.5 * Size - 0.5);
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            var a = _acc[face];
            Add(a, x0, y0, amount * (1 - tx) * (1 - ty)); Add(a, x0 + 1, y0, amount * tx * (1 - ty));
            Add(a, x0, y0 + 1, amount * (1 - tx) * ty); Add(a, x0 + 1, y0 + 1, amount * tx * ty);
        }

        static void Add(float[] a, int x, int y, float v)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            a[y * Size + x] += v;
        }
    }
}
