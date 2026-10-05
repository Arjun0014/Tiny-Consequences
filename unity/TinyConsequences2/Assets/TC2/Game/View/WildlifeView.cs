using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The wild animals near the camera (D2-035), elaborated from the simulation's abundance fields: every cell close
    /// enough shows the animals its fields hold (a fraction shows as a chance, keyed and stable), as herds, packs or
    /// lone animals that wander the cell on slow paths of the world's time, keep their spacing and face where they go;
    /// they keep away from villages and water. Presentation only: what is drawn is decided by the fields and the
    /// keys, so watching never changes the world.
    /// </summary>
    public sealed class WildlifeView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        Instancer[] _kinds;
        Material _mat;
        static readonly string[] Models = { "animal_deer", "animal_hare", "animal_aurochs", "animal_boar", "animal_ibex", "animal_wolf", "animal_fox", "animal_bear" };
        static readonly float[] BaseMass = { 90, 3, 600, 80, 60, 35, 6, 250 };
        static readonly int[] MaxGroup = { 12, 5, 16, 8, 9, 7, 2, 2 };
        static readonly float[] Spacing = { 4.5f, 6f, 6.5f, 3.5f, 4f, 5f, 8f, 10f };
        readonly List<int> _cells = new List<int>();
        readonly HashSet<int> _seen = new HashSet<int>();
        public int Drawn;
        float _timer;

        void Start()
        {
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            _kinds = new Instancer[Models.Length];
            for (int i = 0; i < Models.Length; i++) _kinds[i] = new Instancer(TC2.Game.Models.Get(Models[i], ProcMeshes.Person), _mat, true);
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null || _kinds == null) return;
            var cam = Rig.Cam;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f) { _timer = 0.1f; Rebuild(w); }
            for (int i = 0; i < _kinds.Length; i++) _kinds[i].Draw(cam);
        }

        void Rebuild(TC2.Core.Simulation.World w)
        {
            for (int i = 0; i < _kinds.Length; i++) _kinds[i].Clear();
            Drawn = 0;
            var P = w.Planet;
            if (P.Species.Count == 0 || P.Abund.Length == 0) return;
            Vector3 camPos = Rig.Cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            if (alt > 1800f) return;
            float reach = Mathf.Clamp(alt * 2.6f, 350f, 2400f);
            // the cells round the point the camera looks at (a BFS out to the reach)
            Vector3 f = Rig.FocusPoint;
            var focus = new Vec3d(f.x, f.y, f.z).Normalized;
            var g = P.Grid;
            int c0 = g.CellOf(focus);
            _cells.Clear(); _seen.Clear();
            _cells.Add(c0); _seen.Add(c0);
            double cosR = System.Math.Cos(reach / P.Radius);
            for (int head = 0; head < _cells.Count && _cells.Count < 900; head++)
            {
                int c = _cells[head];
                for (int k = 0; k < 4; k++)
                {
                    int nb = g.Nbr4[c * 4 + k];
                    if (_seen.Contains(nb)) continue;
                    _seen.Add(nb);
                    if (Vec3d.Dot(g.Center[nb], focus) >= cosR) _cells.Add(nb);
                }
            }
            double gameT = Host.RenderTime;
            int n = P.Cells;
            foreach (int c in _cells)
            {
                if (!P.IsLand(c) || P.LakeDepth[c] > 0.3f) continue;
                Vec3d cc = g.Center[c];
                // shy of villages: no herds within 350 m of a village's square (foxes and hares slip in)
                bool nearVillage = false;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.Inhabited && Vec3d.ArcDistance(s.Anchor, cc, P.Radius) < 350.0) { nearVillage = true; break; }
                }
                Storms.Frame(cc, out Vec3d east, out Vec3d north);
                for (int si = 0; si < P.Species.Count; si++)
                {
                    var sp = P.Species[si];
                    int kind = sp.Kind;
                    if (kind >= Models.Length) continue;
                    if (nearVillage && kind != 1 && kind != 6) continue;
                    double N = P.Abund[si * n + c];
                    if (N < 0.02) continue;
                    // how many to show: the whole part, and the fraction as a stable chance
                    ulong key = KeyedRng.Mix((ulong)c * 0x9E3779B97F4A7C15UL ^ (ulong)(si + 1) * 0xC2B2AE3D27D4EB4FUL);
                    int count = (int)N;
                    if (KeyedRng.Unit(key) < N - count) count++;
                    count = Mathf.Min(count, MaxGroup[kind]);
                    if (count <= 0) continue;
                    // the group's centre wanders the cell on a slow loop of the world's time
                    double period = 9.0 * SimTime.Hour * (1.0 + KeyedRng.Unit(KeyedRng.Mix(key + 1)));
                    double ph = gameT / period + KeyedRng.Unit(KeyedRng.Mix(key + 2)) * 6.28318;
                    double rx = P.CellSize * 0.32 * System.Math.Sin(ph * 1.0), ry = P.CellSize * 0.32 * System.Math.Sin(ph * 0.73 + 1.3);
                    double vx = System.Math.Cos(ph * 1.0), vy = 0.73 * System.Math.Cos(ph * 0.73 + 1.3);  // the way it moves
                    float heading = Mathf.Atan2((float)vx, (float)vy) * Mathf.Rad2Deg + 180f; // Upright's zero faces south
                    float scale = Mathf.Pow(sp.Mass / BaseMass[kind], 1f / 3f);
                    for (int m = 0; m < count; m++)
                    {
                        ulong mk = KeyedRng.Mix(key + 10 + (ulong)m);
                        float ang = (float)(KeyedRng.Unit(mk) * 6.28318), rad = Spacing[kind] * Mathf.Sqrt(m + 0.5f) * (0.7f + 0.6f * (float)KeyedRng.Unit(KeyedRng.Mix(mk + 1)));
                        double x = rx + Mathf.Cos(ang) * rad, y = ry + Mathf.Sin(ang) * rad;
                        Vec3d d = (cc * P.Radius + east * x + north * y).Normalized;
                        Vector3 pos = Ground.Surface(P, d);
                        float yaw = heading + (float)(KeyedRng.Unit(KeyedRng.Mix(mk + 2)) - 0.5) * 70f;
                        var rot = PlanetGeo.Upright(pos, yaw);
                        _kinds[kind].Add(0, Color.white, Matrix4x4.TRS(pos, rot, Vector3.one * scale * Exaggeration(pos)));
                        Drawn++;
                        if (Drawn >= 1500) return;
                    }
                }
            }
        }

        float Exaggeration(Vector3 at)
        {
            // a miniature's animals are a little larger than life (people are too), more so far away
            float d = Vector3.Distance(Rig.Cam.transform.position, at);
            return Mathf.Clamp(1.4f * d / 500f, 1.4f, 3f);
        }
    }
}
