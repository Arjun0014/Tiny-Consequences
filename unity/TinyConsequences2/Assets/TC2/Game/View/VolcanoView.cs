using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Volcanoes in the air (D2-029): steam from every vent that is fed or has erupted lately, the fire of a plume
    /// volcano's lava lake at night, and after an eruption its ash column (height by its VEI, spreading at the top,
    /// drifting with the latitude's wind, lit from below) and the lava's glow down the steepest way - the path the
    /// simulation's lava took. Everything is read from the vent's state and the eruption's history event; a show lasts
    /// its time in the world or a few real seconds, whichever is longer, so eruptions are seen at any speed.
    /// Soft camera-facing puffs (`Puff.shader`, data in the instance matrices), sorted far to near.
    /// </summary>
    public sealed class VolcanoView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        Material _mat;
        Mesh _quad;
        readonly List<Matrix4x4> _puffs = new List<Matrix4x4>(2048);
        readonly List<float> _keys = new List<float>(2048);
        Matrix4x4[] _sorted = new Matrix4x4[2048];
        float[] _sortKeys = new float[2048];
        int[] _order = new int[2048];
        readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        readonly Dictionary<int, Show> _shows = new Dictionary<int, Show>();
        readonly List<int> _path = new List<int>();
        object _world;
        public int PuffsDrawn;

        struct Show { public int Eruptions; public float Real; public int Vei; public long At; }

        // happenings read from the world's Fx ring: each shown for a few real seconds
        struct Live { public TC2.Core.Simulation.Fx Fx; public float Real; }
        readonly List<Live> _live = new List<Live>();
        long _fxSeen = -1;

        void Start()
        {
            _mat = Materials.Load("Puff");
            _mat.enableInstancing = true;
            _quad = new Mesh { name = "tc2-puff" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || _mat == null) return;
            if (!ReferenceEquals(w, _world)) { _world = w; _shows.Clear(); _live.Clear(); _fxSeen = 0; } // replay what is recent
            var P = w.Planet;
            VolcanoShape.SetTime(w.Now);
            _puffs.Clear();
            Happenings(w); _keys.Clear();
            Vector3 cam = Rig.Cam.transform.position;
            float now = Time.unscaledTime;
            for (int v = 0; v < P.Vents.Count; v++)
            {
                var vt = P.Vents[v];
                Vector3 top = VolcanoShape.Summit(P, v);
                float dist = (top - cam).magnitude;
                if (dist > 30000f) continue;
                Vector3 up = top.normalized;
                Vector3 wind = Wind(vt.At);
                uint h = (uint)(vt.Id * 2654435761u);
                float seed = (h & 0xffff) / 65535f;
                // what this vent has done since the view last looked
                if (!_shows.TryGetValue(vt.Id, out var show))
                {
                    show = new Show { Eruptions = vt.Eruptions, Real = -1e9f, At = vt.LastEruption };
                    if (vt.Eruptions > 0 && w.Now - vt.LastEruption < SimTime.Day) { show.Real = now; show.Vei = VeiOf(w, vt.Id); }
                }
                else if (vt.Eruptions != show.Eruptions)
                {
                    show.Eruptions = vt.Eruptions; show.Real = now; show.At = vt.LastEruption; show.Vei = VeiOf(w, vt.Id);
                }
                _shows[vt.Id] = show;
                float sinceReal = now - show.Real;
                double sinceWorld = (w.Now - vt.LastEruption) / (double)SimTime.Day;
                // the ash column: half a day in the world or 14 real seconds
                float column = vt.Eruptions > 0 ? Mathf.Max(Fade(sinceReal, 14f), (float)FadeD(sinceWorld, 0.5)) : 0f;
                // the lava's glow: four days or 25 real seconds
                float lava = vt.Eruptions > 0 ? Mathf.Max(Fade(sinceReal, 25f), (float)FadeD(sinceWorld, 4.0)) : 0f;
                // steam: a fed vent, or one that erupted in the last three centuries
                float steam = Mathf.Clamp(0.15f + vt.Supply / 3f, 0f, 0.8f);
                if (vt.Supply < 0.3f && (vt.Eruptions == 0 || sinceWorld > 300 * 48)) steam = 0f;
                if (steam > 0f && dist < 9000f && column < 0.5f) Steam(top, up, wind, steam * (1f - column), seed, now, vt.Kind == 1); // the column replaces the steam
                if (column > 0f) Column(top, up, wind, column, Mathf.Max(1, show.Vei), seed, now);
                if (lava > 0f && dist < 14000f && !LavaLive()) Lava(w, vt.Cell, up, lava, Mathf.Max(1, show.Vei), now);
            }
            Draw(cam);
        }

        /// <summary>New entries of the Fx ring: a quake shakes the camera if felt where it looks; slides raise dust,
        /// waves foam, lava glows - each for a few real seconds (the world may be running fast).</summary>
        void Happenings(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            var ring = w.Fx;
            float now = Time.unscaledTime;
            long from = System.Math.Max(_fxSeen, ring.Version - ring.Capacity);
            Vector3 focus = Rig.FocusPoint;
            for (long n = from; n < ring.Version; n++)
            {
                var f = ring.At(n);
                if (w.Now - f.Time > (f.Kind == TC2.Core.Simulation.FxKind.Quake ? SimTime.Hour : SimTime.Day)) continue; // long past
                if (f.Kind == TC2.Core.Simulation.FxKind.Quake)
                {
                    // felt at the camera's focus: intensity by the quake's own law
                    Vector3 ep = PlanetGeo.ToVector(P.Grid.Center[f.Cell]) * (float)P.Radius;
                    double d = (ep - focus).magnitude;
                    double mmi = TC2.Core.Planet.Quakes.Intensity(f.Size, d);
                    if (mmi > 3.5) Rig.Shake(Mathf.Clamp01((float)(mmi - 3.5) / 5.5f), 1.5f + f.Size - 5f);
                    continue;
                }
                if (_live.Count < 600) _live.Add(new Live { Fx = f, Real = now });
            }
            _fxSeen = ring.Version;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var l = _live[i];
                float age = now - l.Real;
                float life = l.Fx.Kind == TC2.Core.Simulation.FxKind.Lava ? 25f : (l.Fx.Kind == TC2.Core.Simulation.FxKind.Wave ? 10f : 12f);
                if (age > life) { _live.RemoveAt(i); continue; }
                float s = Mathf.Clamp01((life - age) / (life * 0.4f));
                Vec3d dir = P.Grid.Center[l.Fx.Cell];
                Vector3 at = Ground.Surface(P, dir, 1f);
                Vector3 up = at.normalized;
                float seed = Mathf.Repeat(l.Fx.Cell * 0.618034f, 1f);
                switch (l.Fx.Kind)
                {
                    case TC2.Core.Simulation.FxKind.Landslide:
                        // a cloud of dust off the slope, settling
                        for (int k = 0; k < 7; k++)
                        {
                            float ph = Mathf.Clamp01(age / life + k * 0.05f);
                            Vector3 p = at + up * (10f + 60f * ph) + Jitter(seed + k * 0.41f, 30f + 50f * ph);
                            Add(p, 40f + 110f * ph, 0.55f * s * (1f - ph * 0.6f), 0.45f, 0f, Mathf.Repeat(seed * 3f + k * 0.37f, 1f), 0.9f);
                        }
                        break;
                    case TC2.Core.Simulation.FxKind.Wave:
                        // white water over the flooded shore
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 p = at + up * 3f + Jitter(seed + k * 0.29f + age * 0.05f, 70f);
                            Add(p, 60f + 30f * Mathf.Sin(age * 2f + k), 0.6f * s, 0.002f, 0f, Mathf.Repeat(seed + k * 0.23f, 1f), 1f);
                        }
                        break;
                    case TC2.Core.Simulation.FxKind.Lava:
                        {
                            // the lava's own cells (the flow as the simulation ran it)
                            float flick = 0.7f + 0.3f * Mathf.Sin(now * 2.3f + seed * 9f);
                            Add(at + up * 2f, 30f + 5f * l.Fx.Size, 0.35f * s, 0.95f, 1.1f * s * flick, seed, 0.15f);
                            break;
                        }
                }
            }
        }

        static float Fade(float since, float length) => since < 0 || since > length ? 0f : Mathf.Clamp01((length - since) / (length * 0.3f));
        static double FadeD(double since, double length) => since < 0 || since > length ? 0 : System.Math.Min(1.0, (length - since) / (length * 0.3));

        /// <summary>The prevailing wind at a place (scene direction): trades blow west, westerlies east, polar easterlies west.</summary>
        static Vector3 Wind(Vec3d at)
        {
            Vec3d east = Vec3d.Cross(new Vec3d(0, 1, 0), at);
            if (east.Length < 1e-6) east = new Vec3d(1, 0, 0);
            east = east.Normalized;
            double lat = System.Math.Abs(at.Latitude) * 180.0 / System.Math.PI;
            float s = lat < 30 ? -1f : (lat < 60 ? 1f : -0.6f);
            return new Vector3((float)east.X, (float)east.Y, (float)east.Z) * s;
        }

        static int VeiOf(TC2.Core.Simulation.World w, int ventId)
        {
            var list = w.History.EventsOf(new EntityRef(EntityKind.Place, ventId));
            if (list == null) return 2;
            for (int i = list.Count - 1; i >= 0; i--)
                if (w.History.TryGet(list[i], out var e) && e.Kind == HistoryKind.Eruption) return Mathf.Clamp((int)e.Magnitude, 1, 7);
            return 2;
        }

        void Add(Vector3 at, float size, float alpha, float ash, float glow, float seed, float sun)
        {
            var m = Matrix4x4.identity;
            m.m03 = at.x; m.m13 = at.y; m.m23 = at.z;
            m.m00 = size; m.m11 = Mathf.Max(0.002f, alpha); m.m22 = Mathf.Max(0.002f, ash);
            m.m01 = glow; m.m02 = seed; m.m10 = sun;
            _puffs.Add(m);
        }

        void Steam(Vector3 top, Vector3 up, Vector3 wind, float s, float seed, float now, bool lavaLake)
        {
            const int n = 26;
            const float life = 24f;
            for (int k = 0; k < n; k++)
            {
                float ph = Mathf.Repeat(now / life + k / (float)n + seed, 1f);
                float rise = (30f + 150f * s) * ph;
                Vector3 p = top + up * (6f + rise) + wind * (110f * ph * ph) + Jitter(seed + k * 0.37f, 4f + 14f * ph);
                float size = (14f + 70f * ph) * (0.6f + 0.6f * s);
                float a = s * 0.32f * Mathf.Pow(1f - ph, 1.3f) * Mathf.Min(1f, ph * 8f);
                Add(p, size, a, 0.06f + 0.12f * ph, 0f, Mathf.Repeat(seed * 7f + k * 0.61f, 1f), 1f);
            }
            if (lavaLake)
            {
                // the lava lake's fire, seen most at night
                float flick = 0.75f + 0.25f * Mathf.Sin(now * 3.1f + seed * 20f);
                Add(top + up * 4f, 26f, 0.25f, 0.9f, 0.9f * flick, seed, 0.2f);
            }
        }

        void Column(Vector3 top, Vector3 up, Vector3 wind, float s, int vei, float seed, float now)
        {
            int n = 18 + 6 * vei;
            float life = 9f;
            float height = Mathf.Min(1800f, 220f * vei);
            float width = 30f + 22f * vei;
            Vector3 side = Vector3.Cross(up, wind).normalized;
            for (int k = 0; k < n; k++)
            {
                float ph = Mathf.Repeat(now / life + k / (float)n + seed, 1f);
                float y = height * Mathf.Pow(ph, 0.7f);
                Vector3 p = top + up * y;
                // the umbrella: past two thirds of the way up the column spreads out and drifts downwind
                float spread = Mathf.Clamp01((ph - 0.62f) / 0.38f);
                float ang = k * 2.39996f;
                p += (side * Mathf.Cos(ang) + wind * Mathf.Sin(ang)) * spread * height * 0.45f + wind * (height * 0.25f * ph * ph);
                p += Jitter(seed + k * 0.29f, width * 0.4f);
                float size = (width + 120f * ph * (0.5f + 0.15f * vei)) * (0.8f + 0.2f * s);
                float a = s * 0.85f * Mathf.Pow(1f - ph, 0.6f) * Mathf.Min(1f, ph * 10f);
                float glow = ph < 0.12f ? 0.55f * (1f - ph / 0.12f) : 0f;
                Add(p, size, a, 0.82f + 0.1f * Mathf.Repeat(seed + k * 0.7f, 1f), glow * s, Mathf.Repeat(seed * 5f + k * 0.53f, 1f), 0.9f);
            }
            // the vent's fire
            Add(top + up * 6f, width * 0.7f, 0.35f * s, 0.9f, 0.8f * s, seed, 0.2f);
        }

        void Lava(TC2.Core.Simulation.World w, int cell, Vector3 up, float s, int vei, float now)
        {
            var P = w.Planet;
            var g = P.Grid;
            // the steepest way down, as the simulation's lava went
            _path.Clear();
            int cur = cell, length = 1 + (vei * 3) / 2;
            _path.Add(cur);
            for (int k = 0; k < length; k++)
            {
                int next = -1; float low = P.Elev[cur];
                for (int q = 0; q < 8; q++) { int nb = g.Nbr8[cur * 8 + q]; if (P.Elev[nb] < low) { low = P.Elev[nb]; next = nb; } }
                if (next < 0 || !P.IsLand(next)) break;
                cur = next;
                _path.Add(cur);
            }
            for (int i = 0; i + 1 < _path.Count; i++)
            {
                Vec3d a = g.Center[_path[i]], b = g.Center[_path[i + 1]];
                for (int j = 0; j < 5; j++)
                {
                    float t = (j + 0.5f) / 5f;
                    Vec3d d = (a * (1 - t) + b * t).Normalized;
                    Vector3 p = Ground.Surface(P, d, 2f);
                    float along = (i + t) / Mathf.Max(1, _path.Count - 1);
                    float flick = 0.7f + 0.3f * Mathf.Sin(now * 2.3f + i * 1.7f + j);
                    // the front cools first: the glow fades from the end of the flow back to the vent
                    float hot = s * Mathf.Clamp01(1.25f - along * (1.2f - s));
                    if (hot <= 0.01f) continue;
                    Add(p, 22f + 6f * vei, 0.35f * hot, 0.95f, 1.1f * hot * flick, Mathf.Repeat(i * 0.31f + j * 0.17f, 1f), 0.15f);
                }
            }
        }

        bool LavaLive()
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].Fx.Kind == TC2.Core.Simulation.FxKind.Lava) return true;
            return false;
        }

        static Vector3 Jitter(float s, float r)
        {
            float a = Mathf.Sin(s * 12.9898f) * 43758.5453f, b = Mathf.Sin(s * 78.233f) * 12543.1234f, c = Mathf.Sin(s * 37.719f) * 24634.6345f;
            return new Vector3(a - Mathf.Floor(a) - 0.5f, b - Mathf.Floor(b) - 0.5f, c - Mathf.Floor(c) - 0.5f) * (2f * r);
        }

        void Draw(Vector3 cam)
        {
            int n = _puffs.Count;
            PuffsDrawn = n;
            if (n == 0) return;
            if (_sortKeys.Length < n) { _sortKeys = new float[n * 2]; _order = new int[n * 2]; }
            for (int i = 0; i < n; i++)
            {
                var m = _puffs[i];
                float dx = m.m03 - cam.x, dy = m.m13 - cam.y, dz = m.m23 - cam.z;
                _sortKeys[i] = -(dx * dx + dy * dy + dz * dz); // far first
                _order[i] = i;
            }
            System.Array.Sort(_sortKeys, _order, 0, n);
            var rp = new RenderParams(_mat)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 2e5f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                camera = Rig.Cam,
            };
            int done = 0;
            while (done < n)
            {
                int k = Mathf.Min(_batch.Length, n - done);
                for (int i = 0; i < k; i++) _batch[i] = _puffs[_order[done + i]];
                Graphics.RenderMeshInstanced(rp, _quad, 0, _batch, k);
                done += k;
            }
        }
    }
}
