using System.Collections.Generic;
using TC2.Core.Foundation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// (session 27: the powers as play) What the player's hand looks like on the world: while a power is aimed, its reach laid on the ground - a soft
    /// disc in the power's colour, a bright band at its edge breathing slowly, a small ring at its heart - following the planet's curve and the
    /// ground's height; when it is cast, a burst - a flash at the point and a ring running out past the reach and fading. The HUD sets what is aimed
    /// (<see cref="Aiming"/>) and reports each cast (<see cref="Cast"/>). Presentation only: the world applies the command and keeps its record.
    /// </summary>
    public sealed class AimView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        public static bool Aiming;
        public static Vec3d AimDir;
        public static double AimReach;
        public static Color AimColor = Color.white;

        struct Burst { public Vec3d Dir; public double Reach; public Color Col; public float Born; }
        static readonly List<Burst> Bursts = new List<Burst>();

        /// <summary>A power cast at a point: its burst on the ground.</summary>
        public static void Cast(Vec3d dir, double reach, Color col) => Bursts.Add(new Burst { Dir = dir, Reach = reach, Col = col, Born = Time.unscaledTime });

        Mesh _mesh;
        Material _mat;
        readonly List<Vector3> _v = new List<Vector3>(2048);
        readonly List<Color> _c = new List<Color>(2048);
        readonly List<int> _t = new List<int>(6144);
        const int Seg = 64;

        void Start()
        {
            _mat = Materials.Load("Overlay");
            _mesh = new Mesh { name = "tc2-aim" };
            _mesh.MarkDynamic();
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null || _mat == null) return;
            _v.Clear(); _c.Clear(); _t.Clear();
            float now = Time.unscaledTime;
            float alt = Rig.Cam.transform.position.magnitude - (float)w.Planet.Radius;
            float lift = Mathf.Max(1.5f, alt * 0.002f);
            double minR = Mathf.Max(4f, alt * 0.012f);   // (a mark is never smaller than the eye can find)
            if (Aiming)
            {
                double r = System.Math.Max(minR, AimReach);
                float breathe = 0.5f + 0.5f * Mathf.Sin(now * 3.2f);
                Color fill = AimColor; fill.a = 0.10f + 0.04f * breathe;
                Color edge = AimColor; edge.a = 0.65f + 0.3f * breathe;
                Color clear = AimColor; clear.a = 0f;
                Disc(w, AimDir, r * 0.97, fill, lift);
                Band(w, AimDir, r * 0.97, r * 1.03, edge, edge, lift);
                Band(w, AimDir, r * 1.03, r * 1.12, edge, clear, lift);
                Band(w, AimDir, System.Math.Max(minR * 0.25, r * 0.04), System.Math.Max(minR * 0.35, r * 0.06), edge, edge, lift);
            }
            for (int i = Bursts.Count - 1; i >= 0; i--)
            {
                var b = Bursts[i];
                float age = now - b.Born;
                if (age > 1.8f) { Bursts.RemoveAt(i); continue; }
                double r = System.Math.Max(minR, b.Reach);
                float k = age / 1.8f;
                double ring = r * (0.15 + 1.25 * Mathf.Sqrt(k));
                Color c = b.Col; c.a = 0.9f * (1f - k);
                Color clear = b.Col; clear.a = 0f;
                Band(w, b.Dir, ring * 0.9, ring, clear, c, lift);
                Band(w, b.Dir, ring, ring * 1.04, c, clear, lift);
                if (age < 0.5f) { Color flash = Color.Lerp(b.Col, Color.white, 0.6f); flash.a = 0.55f * (1f - age / 0.5f); Disc(w, b.Dir, r * 0.35, flash, lift); }
            }
            if (_v.Count == 0) return;
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            var rp = new RenderParams(_mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, camera = Rig.Cam, worldBounds = new Bounds(Vector3.zero, Vector3.one * 2e5f) };
            Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
        }

        /// <summary>A point at an angle and an arc's length (m) from a centre on the ground, lifted a little above it.</summary>
        static Vector3 At(TC2.Core.Simulation.World w, Vec3d up, Vec3d e, Vec3d n, double r, double a, float lift)
        {
            double ang = r / w.Planet.Radius;
            Vec3d p = (up * System.Math.Cos(ang) + (e * System.Math.Cos(a) + n * System.Math.Sin(a)) * System.Math.Sin(ang)).Normalized;
            return PlanetGeo.Surface(w.Planet, p, lift);
        }

        static void Frame(Vec3d up, out Vec3d e, out Vec3d n)
        {
            e = Vec3d.Cross(new Vec3d(0, 1, 0), up); if (e.Length < 1e-6) e = new Vec3d(1, 0, 0);
            e = e.Normalized; n = Vec3d.Cross(up, e).Normalized;
        }

        /// <summary>A band on the ground between two radii, its colour from the inner edge to the outer.</summary>
        void Band(TC2.Core.Simulation.World w, Vec3d dir, double r0, double r1, Color c0, Color c1, float lift)
        {
            Frame(dir, out var e, out var n);
            int b0 = _v.Count;
            for (int i = 0; i <= Seg; i++)
            {
                double a = 2 * System.Math.PI * i / Seg;
                _v.Add(At(w, dir, e, n, r0, a, lift)); _c.Add(c0);
                _v.Add(At(w, dir, e, n, r1, a, lift)); _c.Add(c1);
            }
            for (int i = 0; i < Seg; i++)
            {
                int k = b0 + i * 2;
                _t.Add(k); _t.Add(k + 2); _t.Add(k + 1);
                _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
            }
        }

        /// <summary>A filled disc on the ground (rings out to its radius, so it follows the ground's height).</summary>
        void Disc(TC2.Core.Simulation.World w, Vec3d dir, double r, Color c, float lift)
        {
            const int rings = 6;
            for (int k = 0; k < rings; k++) Band(w, dir, r * k / rings, r * (k + 1) / rings, c, c, lift);
        }
    }
}
