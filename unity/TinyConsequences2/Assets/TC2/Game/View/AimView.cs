using System.Collections.Generic;
using TC2.Core.Foundation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// (session 27: the powers as play) What the player's hand looks like on the world: while a power is aimed, its reach laid on the ground - a soft
    /// disc in the power's colour, a bright band at its edge breathing slowly, a small ring at its heart - following the planet's curve and the
    /// ground's height; when it is cast, a burst - a flash at the point and a ring running out past the reach and fading. The HUD marks what is aimed
    /// each frame it draws the aim (<see cref="Aim"/>: a mark the HUD stops renewing - hidden, the book open, the title - is gone the next frame) and
    /// reports each cast (<see cref="Cast"/>). Presentation only: the world applies the command and keeps its record.
    /// </summary>
    public sealed class AimView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        /// <summary>(shots: --aim) a reach laid on the ground with no HUD renewing it.</summary>
        public static bool Pinned;
        public static Vec3d AimDir;
        public static double AimReach;
        public static Color AimColor = Color.white;
        static int _aimFrame = -10;

        /// <summary>The power aimed this frame: its point, reach (m) and colour. (session 32) The ring drawn eases to it (it steps from cell to cell).</summary>
        public static void Aim(Vec3d dir, double reach, Color col)
        {
            if (Time.frameCount - _aimFrame > 2 || Vec3d.Dot(_shownDir, dir) < 0.99) _shownDir = dir;   // (a new aim, or far: at once)
            AimDir = dir; AimReach = reach; AimColor = col; _aimFrame = Time.frameCount;
        }
        static Vec3d _shownDir = new Vec3d(0, 1, 0);

        /// <summary>(session 32) A flash over the whole picture (0..1): a bolt's, a stone's landing - the HUD draws it, fading in a quarter second.</summary>
        public static void Flash(float strength) { FlashStrength = Mathf.Max(FlashAlpha, Mathf.Clamp01(strength)); FlashAt = Time.unscaledTime; }
        public static float FlashStrength, FlashAt = -99f;
        /// <summary>The flash's opacity now.</summary>
        public static float FlashAlpha { get { float a = Time.unscaledTime - FlashAt; return a < 0f || a > 0.35f ? 0f : FlashStrength * (1f - a / 0.35f) * (1f - a / 0.35f); } }

        /// <summary>A mark is drawn while the HUD renewed it this frame or the last (the HUD draws after the views' LateUpdate).</summary>
        public static bool Aiming => Pinned || Time.frameCount - _aimFrame <= 1;

        struct Burst { public Vec3d Dir; public double Reach; public Color Col; public float Born, Shake; public bool Shaken; }
        static readonly List<Burst> Bursts = new List<Burst>();
        // (session 27) a stone falling from the sky to where it was thrown (the meteor), its burst and the ground's shaking when it lands
        struct Fall { public Vec3d Dir; public Color Col; public float Born; }
        static readonly List<Fall> Falls = new List<Fall>();
        public const float FallTime = 1.1f;   // (session 32: a beat longer - its coming seen)
        struct Throw { public Vec3d Dir; public float LandAt; }
        static readonly List<Throw> Throws = new List<Throw>();   // the stones thrown in the last seconds and when each lands (VolcanoView: one fall)

        /// <summary>Whether a stone was thrown at this point in the last few seconds (the meteor's cast), and when it lands (Time.unscaledTime).</summary>
        public static bool Thrown(Vec3d dir, out float landAt)
        {
            landAt = 0f;
            for (int i = Throws.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - Throws[i].LandAt < 6f && Vec3d.Dot(Throws[i].Dir, dir) > 0.9999999) { landAt = Throws[i].LandAt; return true; }
            return false;
        }
        // (session 27) lightning seen: a bolt for each strike the ledger records near where the camera looks - the storms' and the god's alike
        struct Bolt { public Vector3 At; public float Born; public ulong Seed; }
        readonly List<Bolt> _bolts = new List<Bolt>();
        long _boltSeen = -1;
        TC2.Core.Simulation.World _boltWorld;
        const float BoltTime = 0.75f;   // (session 32: three strokes, long enough to be seen at a low frame rate)

        struct Mark { public Vec3d Dir; public double R; public Color Col; public int Frame; }
        static readonly Mark[] Marks = new Mark[2];

        /// <summary>(session 27) A building marked on the ground this frame - slot 0 the one under the pointer, 1 the one chosen: a band round its
        /// footprint (radius m) in its household's colour. Gone the frame the HUD stops renewing it.</summary>
        public static void MarkAt(int slot, Vec3d dir, double radius, Color col) => Marks[slot] = new Mark { Dir = dir, R = radius, Col = col, Frame = Time.frameCount };

        /// <summary>A power cast at a point: its burst on the ground and the camera shaken by <paramref name="shake"/> (0 none); <paramref name="fall"/>:
        /// a stone falls from the sky first, the burst and the shaking when it lands.</summary>
        public static void Cast(Vec3d dir, double reach, Color col, float shake = 0f, bool fall = false)
        {
            float born = Time.unscaledTime;
            if (fall)
            {
                Falls.Add(new Fall { Dir = dir, Col = col, Born = born }); born += FallTime;
                Throws.RemoveAll(x => Time.unscaledTime - x.LandAt > 6f);
                Throws.Add(new Throw { Dir = dir, LandAt = born });
            }
            Bursts.Add(new Burst { Dir = dir, Reach = reach, Col = col, Born = born, Shake = shake });
        }

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
                // (eased toward where it strikes)
                double ek = 1.0 - System.Math.Exp(-Time.unscaledDeltaTime / 0.06);
                _shownDir = new Vec3d(_shownDir.X + (AimDir.X - _shownDir.X) * ek, _shownDir.Y + (AimDir.Y - _shownDir.Y) * ek, _shownDir.Z + (AimDir.Z - _shownDir.Z) * ek).Normalized;
                Vec3d aimAt = _shownDir;
                double r = System.Math.Max(minR, AimReach);
                float breathe = 0.5f + 0.5f * Mathf.Sin(now * 3.2f);
                Color fill = AimColor; fill.a = 0.10f + 0.04f * breathe;
                Color edge = AimColor; edge.a = 0.42f + 0.28f * breathe;
                Color clear = AimColor; clear.a = 0f;
                // the band and the mark at its heart sized for the eye (the camera's height), not by the reach: a 900 m reach had laid a 50 m opaque
                // ring over the village at its heart and a band as wide as a field at its edge
                double wid = System.Math.Min(r * 0.03, System.Math.Max(1.0, alt * 0.0045));
                Disc(w, aimAt, r - wid * 0.5, fill, lift);
                Band(w, aimAt, r - wid * 0.5, r + wid * 0.5, edge, edge, lift);
                Band(w, aimAt, r + wid * 0.5, r + wid * 2.0, edge, clear, lift);
                Band(w, aimAt, minR * 0.30, minR * 0.42, edge, edge, lift);
            }
            for (int k = 0; k < Marks.Length; k++)
            {
                var mk = Marks[k];
                if (mk.R <= 0 || Time.frameCount - mk.Frame > 1) continue;
                double wid = System.Math.Max(0.3, alt * 0.0022);   // (a band the eye finds at the village view, fine up close)
                Color c = Color.Lerp(mk.Col, Color.white, 0.25f); c.a = k == 1 ? 0.92f : 0.75f;
                Color clear = c; clear.a = 0f;
                float low = Mathf.Max(0.25f, alt * 0.0008f);
                Band(w, mk.Dir, mk.R - wid * 1.5, mk.R, clear, c, low);
                Band(w, mk.Dir, mk.R, mk.R + wid, c, c, low);
                Band(w, mk.Dir, mk.R + wid, mk.R + wid * 2.5, c, clear, low);
            }
            for (int i = Bursts.Count - 1; i >= 0; i--)
            {
                var b = Bursts[i];
                float age = now - b.Born;
                if (age > 1.8f) { Bursts.RemoveAt(i); continue; }
                if (age < 0f) continue;   // (a falling stone's burst waits for it to land)
                if (!b.Shaken) { b.Shaken = true; Bursts[i] = b; if (b.Shake > 0f) Rig.Shake(b.Shake, 0.6f + b.Shake * 1.4f); }
                double r = System.Math.Max(minR, b.Reach);
                float k = age / 1.8f;
                double ring = r * (0.15 + 1.25 * Mathf.Sqrt(k));
                Color c = b.Col; c.a = 0.9f * (1f - k);
                Color clear = b.Col; clear.a = 0f;
                Band(w, b.Dir, ring * 0.9, ring, clear, c, lift);
                Band(w, b.Dir, ring, ring * 1.04, c, clear, lift);
                if (age < 0.5f) { Color flash = Color.Lerp(b.Col, Color.white, 0.6f); flash.a = 0.55f * (1f - age / 0.5f); Disc(w, b.Dir, r * 0.35, flash, lift); }
            }
            Vector3 cam = Rig.Cam.transform.position;
            Strikes(w, now, alt);
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                float age = now - b.Born;
                if (age > BoltTime) { _bolts.RemoveAt(i); continue; }
                // two strokes and their afterglow: lit at once, dark a moment, lit again, fading
                float lit = age < 0.10f ? 1f : age < 0.16f ? 0.15f : age < 0.30f ? 1f : age < 0.36f ? 0.2f : age < 0.46f ? 0.9f : 0.9f * (1f - (age - 0.46f) / (BoltTime - 0.46f));
                Vector3 up = b.At.normalized;
                Vector3 east = Vector3.Cross(Vector3.up, up); if (east.sqrMagnitude < 1e-6f) east = Vector3.right; east.Normalize();
                Vector3 north = Vector3.Cross(up, east);
                float H = Mathf.Clamp(alt * 0.55f, 220f, 900f), wid = Mathf.Max(2.2f, alt * 0.006f);
                Color core = new Color(0.86f, 0.91f, 1f, lit), glow = new Color(0.55f, 0.65f, 1f, 0.35f * lit);
                ulong h = b.Seed;
                Vector3 prev = b.At + up * H;
                const int n = 9;
                for (int k = 1; k <= n; k++)
                {
                    h = KeyedRng.Mix(h + (ulong)k);
                    float jx = (float)KeyedRng.Unit(h) - 0.5f, jy = (float)KeyedRng.Unit(KeyedRng.Mix(h ^ 0x9E37UL)) - 0.5f;
                    float s = k / (float)n;
                    Vector3 next = k == n ? b.At : b.At + up * (H * (1f - s)) + (east * jx + north * jy) * (H * 0.16f * (1f - s * 0.6f));
                    Segment(prev, next, wid * 3.5f, glow, cam);
                    Segment(prev, next, wid, core, cam);
                    prev = next;
                }
                Sprite(b.At + up * (wid * 2f), H * 0.22f, new Color(0.8f, 0.86f, 1f, 0.55f * lit), cam);
            }
            // a falling stone: a burning head coming down a slanted path from high over the point, its trail behind it fading from fire to smoke
            for (int i = Falls.Count - 1; i >= 0; i--)
            {
                var f = Falls[i];
                float t = (now - f.Born) / FallTime;
                if (t >= 1f) { Falls.RemoveAt(i); continue; }
                Vector3 g = PlanetGeo.Surface(w.Planet, f.Dir, 0f), up = g.normalized;
                Vector3 east = Vector3.Cross(Vector3.up, up); if (east.sqrMagnitude < 1e-6f) east = Vector3.right; east.Normalize();
                Vector3 north = Vector3.Cross(up, east);
                float L = Mathf.Clamp(alt * 1.3f, 300f, 6000f);
                Vector3 from = g + (up * 0.82f + east * 0.45f + north * 0.36f).normalized * L;
                float k = t * t;   // (it falls faster as it comes)
                Vector3 head = Vector3.Lerp(from, g, k), tail = Vector3.Lerp(from, g, Mathf.Max(0f, k - 0.22f - 0.25f * t));
                float wHead = Mathf.Max(5f, alt * 0.012f);
                Color hot = Color.Lerp(f.Col, new Color(1f, 0.95f, 0.8f), 0.6f); hot.a = 1f;
                Color smoke = new Color(0.55f, 0.48f, 0.42f, 0f);
                Ribbon(tail, head, wHead * 0.15f, wHead, smoke, hot, cam);
                Sprite(head, wHead * 1.6f, hot, cam);
                // (session 32: anticipation) its glow on the ground where it will strike, widening and brightening as it comes; a hot halo round its head
                Color glowG = Color.Lerp(f.Col, new Color(1f, 0.85f, 0.6f), 0.4f); glowG.a = 0.10f + 0.35f * k;
                Disc(w, f.Dir, 20.0 + 150.0 * k, glowG, lift);
                Color halo = hot; halo.a = 0.35f;
                Sprite(head, wHead * (4f + 3f * k), halo, cam);
            }
            if (_v.Count == 0) return;
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            var rp = new RenderParams(_mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, camera = Rig.Cam, worldBounds = new Bounds(Vector3.zero, Vector3.one * 2e5f) };
            Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
        }

        /// <summary>A ribbon from a to b facing the camera, its width and colour from one end to the other.</summary>
        void Ribbon(Vector3 a, Vector3 b, float wa, float wb, Color ca, Color cb, Vector3 cam)
        {
            const int n = 10;
            Vector3 along = b - a;
            if (along.sqrMagnitude < 1e-4f) return;
            int b0 = _v.Count;
            for (int i = 0; i <= n; i++)
            {
                float s = i / (float)n;
                Vector3 p = a + along * s;
                Vector3 side = Vector3.Cross(along, p - cam).normalized * (Mathf.Lerp(wa, wb, s) * 0.5f);
                Color c = Color.Lerp(ca, cb, s * s);
                _v.Add(p - side); _c.Add(c);
                _v.Add(p + side); _c.Add(c);
            }
            for (int i = 0; i < n; i++)
            {
                int k = b0 + i * 2;
                _t.Add(k); _t.Add(k + 2); _t.Add(k + 1);
                _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
            }
        }

        /// <summary>(session 27) The strikes the ledger has recorded since the last frame near where the camera looks (a few a frame, a bolt each), placed
        /// within their cell by the record's own number (the world knows the cell struck, not the spot).</summary>
        void Strikes(TC2.Core.Simulation.World w, float now, float alt)
        {
            var H = w.History;
            if (!ReferenceEquals(w, _boltWorld)) { _boltWorld = w; _boltSeen = H.LastId; _bolts.Clear(); return; }
            if (H.LastId == _boltSeen) return;
            var P = w.Planet;
            Vector3 f = Rig.FocusPoint;
            Vec3d dir = new Vec3d(f.x, f.y, f.z).Normalized;
            double see = 2500.0 + 2.0 * alt;
            int added = 0;
            for (int i = H.FirstIndexAfter(_boltSeen); i < H.Count && added < 4; i++)
            {
                var e = H.At(i);
                if (e.Kind != TC2.Core.History.HistoryKind.LightningStrike || e.Cell < 0 || e.Cell >= P.Cells) continue;
                Vec3d c = P.Grid.Center[e.Cell];
                if (Vec3d.ArcDistance(dir, c, P.Radius) > see) continue;
                ulong h = KeyedRng.Mix((ulong)e.Id * 0x9E3779B97F4A7C15UL);
                Vec3d east = Vec3d.Cross(new Vec3d(0, 1, 0), c); if (east.Length < 1e-9) east = new Vec3d(1, 0, 0); east = east.Normalized;
                Vec3d north = Vec3d.Cross(c, east).Normalized;
                double spread = e.RootKind == TC2.Core.History.RootKind.You ? 0.0 : 90.0;   // (session 32: the god's bolt falls where it was aimed - the heart of its ring - on the roof that burns)
                double ox = (KeyedRng.Unit(h) - 0.5) * spread, oy = (KeyedRng.Unit(KeyedRng.Mix(h)) - 0.5) * spread;
                Vec3d at = (c * P.Radius + east * ox + north * oy).Normalized;
                _bolts.Add(new Bolt { At = PlanetGeo.Surface(P, at, 0f), Born = now, Seed = h });
                added++;
            }
            _boltSeen = H.LastId;
        }

        /// <summary>A straight band from a to b facing the camera.</summary>
        void Segment(Vector3 a, Vector3 b, float wdt, Color col, Vector3 cam)
        {
            Vector3 along = b - a;
            if (along.sqrMagnitude < 1e-4f) return;
            Vector3 side = Vector3.Cross(along, (a + b) * 0.5f - cam).normalized * (wdt * 0.5f);
            int k = _v.Count;
            _v.Add(a - side); _v.Add(a + side); _v.Add(b - side); _v.Add(b + side);
            _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
            _t.Add(k); _t.Add(k + 2); _t.Add(k + 1); _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
        }

        /// <summary>A glowing disc facing the camera: bright at its heart, clear at its edge.</summary>
        void Sprite(Vector3 at, float r, Color col, Vector3 cam)
        {
            Vector3 view = (at - cam).normalized;
            Vector3 sx = Vector3.Cross(view, Vector3.up); if (sx.sqrMagnitude < 1e-6f) sx = Vector3.right; sx.Normalize();
            Vector3 sy = Vector3.Cross(sx, view);
            int c0 = _v.Count;
            _v.Add(at); _c.Add(col);
            Color clear = col; clear.a = 0f;
            const int n = 16;
            for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2f / n; _v.Add(at + (sx * Mathf.Cos(a) + sy * Mathf.Sin(a)) * r); _c.Add(clear); }
            for (int i = 0; i < n; i++) { _t.Add(c0); _t.Add(c0 + 1 + i); _t.Add(c0 + 2 + i); }
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
