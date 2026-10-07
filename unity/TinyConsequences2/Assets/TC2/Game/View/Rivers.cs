using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The rivers as drawn (D2-084): one shape for every stream cell's reach to the cell it drains into (the drainage's own path,
    /// laid in pieces that meander by a smooth noise of where they are), shared by the water's ribbons (<see cref="WaterView"/>),
    /// the corridor the ground draws along them - wet banks, the riparian strip, reeds - and the woods that keep to the water
    /// (<see cref="TerrainLod"/>). Its width comes from the river's flow (the simulation's RiverFlow), widened for a miniature
    /// (about 2.5x a real river's at the same flow); its rank among the planet's rivers says how far off it should still read.
    /// Presentation only: rebuilt when the drainage changes; the widths and ranks follow the flows every few seconds.
    /// </summary>
    public static class Rivers
    {
        public const int Pieces = 8;
        /// <summary>One piece of a reach: its ends (unit directions), its flow (m3/day), half its drawn width near the camera (m) and its
        /// rank among the planet's rivers by flow (0-1: the largest 1).</summary>
        public struct Piece { public Vec3d A, B; public float Flow, Half, Rank; public int Cell; }

        public static readonly List<Piece> All = new List<Piece>();
        static readonly Dictionary<int, List<int>> _byCell = new Dictionary<int, List<int>>();
        static int _drain = -1, _world = -1;
        static float _widthsAt = -999f;
        public static int Version;

        /// <summary>Half the drawn width (m) of a river carrying this flow (m3/day) near the camera.</summary>
        public static float HalfWidth(float flow) => 0.5f * Mathf.Clamp(0.07f * Mathf.Sqrt(Mathf.Max(0f, flow)), 1.5f, 60f);

        /// <summary>Rebuilds the shape when the drainage or the world changed, and the widths when the flows may have (every few seconds).</summary>
        public static void Refresh(TC2.Core.Simulation.World w, int worldVersion)
        {
            var P = w.Planet; var d = w.Drainage;
            if (d == null) return;
            if (_drain == d.Version && _world == worldVersion)
            {
                if (Time.realtimeSinceStartup - _widthsAt < 4f) return;
                Widths(P);
                return;
            }
            _drain = d.Version; _world = worldVersion;
            All.Clear(); _byCell.Clear();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || !P.IsStream(c)) continue;
                int r = d.Receiver[c];
                if (r < 0) continue;
                Vec3d a = P.Grid.Center[c];
                Vec3d b = P.IsLand(r) ? P.Grid.Center[r] : (a * 0.35 + P.Grid.Center[r] * 0.65).Normalized;   // (into the sea)
                Vec3d prev = a;
                for (int s = 1; s <= Pieces; s++)
                {
                    Vec3d along = Vec3d.Slerp(a, b, s / (double)Pieces);
                    if (s < Pieces) along = Meander(P, along, a, b);
                    int idx = All.Count;
                    All.Add(new Piece { A = prev, B = along, Cell = c });
                    Index(P.Grid.CellOf(prev), idx); Index(P.Grid.CellOf(along), idx);
                    prev = along;
                }
            }
            Widths(P);
        }

        static void Index(int cell, int piece)
        {
            if (!_byCell.TryGetValue(cell, out var list)) { list = new List<int>(4); _byCell[cell] = list; }
            if (list.Count == 0 || list[list.Count - 1] != piece) list.Add(piece);
        }

        static readonly List<float> _flows = new List<float>();

        static void Widths(PlanetState P)
        {
            _widthsAt = Time.realtimeSinceStartup;
            _flows.Clear();
            for (int i = 0; i < All.Count; i += Pieces) _flows.Add(P.RiverFlow[All[i].Cell]);
            _flows.Sort();
            for (int i = 0; i < All.Count; i++)
            {
                var p = All[i];
                p.Flow = P.RiverFlow[p.Cell];
                p.Half = HalfWidth(p.Flow);
                int at = _flows.BinarySearch(p.Flow); if (at < 0) at = ~at;
                p.Rank = _flows.Count > 1 ? at / (float)(_flows.Count - 1) : 1f;
                All[i] = p;
            }
            Version++;
        }

        /// <summary>Rivers wind: a point on a reach is pushed sideways by a smooth noise of where it is (presentation).</summary>
        public static Vec3d Meander(PlanetState P, Vec3d at, Vec3d a, Vec3d b)
        {
            Vec3d dir = (b - a).Normalized;
            Vec3d side = Vec3d.Cross(at, dir).Normalized;
            double n = Noise.Fbm(at.X * 180.0, at.Y * 180.0, at.Z * 180.0, 2);
            return (at * P.Radius + side * (n * 34.0)).Normalized;
        }

        /// <summary>
        /// The nearest river's centre line to a point of the ground (m along the surface, approximately), and half its drawn width
        /// there; float.MaxValue where no river runs within a cell or so.
        /// </summary>
        public static float Nearest(PlanetState P, Vec3d dir, int cell, out float half)
        {
            half = 0f;
            float best = float.MaxValue;
            if (All.Count == 0) return best;
            var g = P.Grid;
            for (int k = -1; k < 8; k++)
            {
                int cc = k < 0 ? cell : g.Nbr8[cell * 8 + k];
                if (!_byCell.TryGetValue(cc, out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = All[list[i]];
                    float dd = (float)(SegDist(dir, p.A, p.B) * P.Radius);
                    if (dd < best) { best = dd; half = p.Half; }
                }
            }
            return best;
        }

        /// <summary>Distance (unit-sphere chord) from a point to a short segment between two unit vectors.</summary>
        static double SegDist(Vec3d p, Vec3d a, Vec3d b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, abz = b.Z - a.Z;
            double apx = p.X - a.X, apy = p.Y - a.Y, apz = p.Z - a.Z;
            double l2 = abx * abx + aby * aby + abz * abz;
            double t = l2 > 1e-18 ? (apx * abx + apy * aby + apz * abz) / l2 : 0.0;
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            double dx = apx - abx * t, dy = apy - aby * t, dz = apz - abz * t;
            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
