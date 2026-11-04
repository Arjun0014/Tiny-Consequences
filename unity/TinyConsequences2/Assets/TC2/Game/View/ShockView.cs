using System.Collections.Generic;
using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 32: a shock seen) What a shock does to the ground where the camera looks, read from the world: a quake's wave running out over the ground in
    /// dust from where it broke (the Fx ring's quakes), and each building that has just fallen - to a quake, a stone from the sky - sending up its own dust
    /// as it comes down (a building fallen within the hour: the world sets its fall to the moment). Presentation only.
    /// </summary>
    public sealed class ShockView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        struct Wave { public Vec3d At; public float Born, Size; }
        readonly List<Wave> _waves = new List<Wave>();
        struct Fell { public Vector3 At; public float Born, Size, Seed; }
        readonly List<Fell> _fell = new List<Fell>();
        readonly HashSet<int> _seen = new HashSet<int>();
        long _fxSeen = -1;
        object _world;
        float _scanAt;

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null) return;
            if (!ReferenceEquals(w, _world)) { _world = w; _waves.Clear(); _fell.Clear(); _seen.Clear(); _fxSeen = w.Fx.Version; }
            var P = w.Planet;
            float now = Time.unscaledTime;
            Vector3 focus = Rig.FocusPoint;
            float alt = Rig.Cam.transform.position.magnitude - (float)P.Radius;
            if (alt > 12000f) { _fxSeen = w.Fx.Version; return; }   // (from orbit nothing of this reads)
            // a quake's wave: from the Fx ring, within sight
            var ring = w.Fx;
            for (long n = System.Math.Max(_fxSeen, ring.Version - ring.Capacity); n < ring.Version; n++)
            {
                var f = ring.At(n);
                if (f.Kind != TC2.Core.Simulation.FxKind.Quake || w.Now - f.Time > SimTime.Hour) continue;
                Vector3 ep = PlanetGeo.ToVector(P.Grid.Center[f.Cell]) * (float)P.Radius;
                if ((ep - focus).magnitude > 6000f) continue;
                _waves.Add(new Wave { At = P.Grid.Center[f.Cell], Born = now, Size = f.Size });
            }
            _fxSeen = ring.Version;
            // buildings just fallen near the camera's view: looked for twice a second
            if (now - _scanAt > 0.5f)
            {
                _scanAt = now;
                Vec3d fd = new Vec3d(focus.x, focus.y, focus.z).Normalized;
                double see = 900.0 + 1.5 * alt;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (Vec3d.ArcDistance(s.Anchor, fd, P.Radius) > see + 600.0) continue;
                    var bl = w.BuildingsOf(sid);
                    for (int i = 0; i < bl.Count; i++)
                    {
                        ref var b = ref w.Buildings.Get(bl[i]);
                        if (b.Left == 0 || _seen.Contains(bl[i]) || !TC2.Core.Places.Homes.Fallen(in b, w.Now)) continue;
                        long fellAt = b.Left + (long)(TC2.Core.Places.Homes.FallYears(in b) * SimTime.Year);
                        if (w.Now - fellAt > SimTime.Hour || w.Now < fellAt) continue;
                        _seen.Add(bl[i]);
                        Vec3d bd = s.LocalToSphere(b.X, b.Y, P.Radius);
                        float big = TC2.Core.Places.Structures.IsHome(b.Kind) ? (b.Kind == TC2.Core.Places.Construction.Hut ? 0.6f : 1f) : 1.4f;
                        _fell.Add(new Fell { At = Ground.Surface(P, bd, 0.5f), Born = now, Size = big, Seed = Mathf.Repeat(bl[i] * 0.618034f, 1f) });
                    }
                }
                if (_seen.Count > 4000) _seen.Clear();
            }
            // the waves: a ring of low dust running out from where it broke, three times as the ground rolls
            for (int i = _waves.Count - 1; i >= 0; i--)
            {
                var wv = _waves[i];
                float age = now - wv.Born;
                const float life = 4.5f;
                if (age > life) { _waves.RemoveAt(i); continue; }
                Frame(wv.At, out Vec3d e, out Vec3d nn);
                float reach = 400f + 260f * Mathf.Max(0f, wv.Size - 4f);
                for (int pulse = 0; pulse < 3; pulse++)
                {
                    float t = age - pulse * 0.55f;
                    if (t <= 0f || t > 3f) continue;
                    float r = reach * Mathf.Sqrt(t / 3f), fade = (1f - t / 3f) * (pulse == 0 ? 1f : 0.6f);
                    int n = 36;
                    for (int k = 0; k < n; k++)
                    {
                        double a = 2 * System.Math.PI * (k + 0.5f * pulse) / n;
                        Vec3d d = OnGround(wv.At, e, nn, r, a, P.Radius);
                        Vector3 p = Ground.Surface(P, d, 3f + 6f * t);
                        Puffs.Add(p, 18f + r * 0.08f, 0.45f * fade, 0.55f, 0f, Mathf.Repeat(k * 0.37f + pulse * 0.11f, 1f), 0.85f);
                    }
                }
            }
            // each fallen building's dust: a burst as it comes down, a cloud that rises and thins over some seconds
            for (int i = _fell.Count - 1; i >= 0; i--)
            {
                var fl = _fell[i];
                float age = now - fl.Born;
                const float life = 14f;
                if (age > life) { _fell.RemoveAt(i); continue; }
                Vector3 up = fl.At.normalized;
                float ph = age / life, s = Mathf.Clamp01((life - age) / (life * 0.5f));
                for (int k = 0; k < 6; k++)
                {
                    float sk = Mathf.Repeat(fl.Seed * 7.3f + k * 0.29f, 1f);
                    Vector3 off = Jitter(fl.Seed + k * 0.41f, (5f + 16f * Mathf.Sqrt(ph)) * fl.Size);
                    Vector3 p = fl.At + up * ((2f + 18f * Mathf.Sqrt(ph)) * fl.Size * (0.6f + 0.6f * sk)) + off - up * Vector3.Dot(off, up) * 0.6f;
                    Puffs.Add(p, (7f + 16f * Mathf.Sqrt(ph)) * fl.Size, 0.7f * s * (age < 0.4f ? age / 0.4f : 1f), 0.6f, 0f, sk, 0.85f);
                }
            }
        }

        static void Frame(Vec3d up, out Vec3d e, out Vec3d n)
        {
            e = Vec3d.Cross(new Vec3d(0, 1, 0), up); if (e.Length < 1e-6) e = new Vec3d(1, 0, 0);
            e = e.Normalized; n = Vec3d.Cross(up, e).Normalized;
        }

        static Vec3d OnGround(Vec3d up, Vec3d e, Vec3d n, double r, double a, double R)
        {
            double ang = r / R;
            return (up * System.Math.Cos(ang) + (e * System.Math.Cos(a) + n * System.Math.Sin(a)) * System.Math.Sin(ang)).Normalized;
        }

        static Vector3 Jitter(float s, float r)
        {
            float a = Mathf.Sin(s * 12.9898f) * 43758.5453f, b = Mathf.Sin(s * 78.233f) * 12543.1234f, c = Mathf.Sin(s * 37.719f) * 24634.6345f;
            return new Vector3(a - Mathf.Floor(a) - 0.5f, b - Mathf.Floor(b) - 0.5f, c - Mathf.Floor(c) - 0.5f) * (2f * r);
        }
    }
}
