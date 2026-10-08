using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Where the rivers run on the ground (D2-084's river shape, moved into the core by D2-118 so one line serves the simulation and the view, as one
    /// shore does since D2-088): for every stream cell, its reach to the cell it drains into - the drainage's own path, laid in pieces that meander
    /// by a smooth noise of where they are - ending where it meets the drawn water (the waterline, a couple of metres in), or carried on across a
    /// beach to it where the sea lies beyond. Laid for every reach draining enough ground to be a stream in some season; whether it is one
    /// today is its flow's (`Stream`) - the lines themselves follow only the drainage, so a loaded world lays the same (D2-119). Homes and works keep their ground off a river (`Places.Footprints`); the view draws its ribbon on the
    /// same pieces. Derived from the drainage, rebuilt when it changes; never saved.
    /// </summary>
    public static class RiverLines
    {
        public const int Pieces = 8;
        /// <summary>One piece of a reach: its ends (unit directions) and the stream cell whose reach it is.</summary>
        public struct Piece { public Vec3d A, B; public int Cell; }

        public static readonly List<Piece> All = new List<Piece>();
        static readonly Dictionary<int, List<int>> _byCell = new Dictionary<int, List<int>>();
        static Drainage _drain;
        static int _version = -1;
        static ulong _seed;
        /// <summary>Bumped whenever the shape is rebuilt (the view rebuilds its ribbons).</summary>
        public static int Version;

        /// <summary>
        /// Upstream ground (ha) from which a reach is laid: a smoothed flow of 1000 m3/day (a stream, `PlanetState.IsStream`) needs 10 ha even under
        /// 10 mm of runoff a day for a month - a headwater cell alone never carries one.
        /// </summary>
        public const double StreamAreaHa = 10.0;

        /// <summary>The reach of this piece is a stream now (by its flow and the lakes today): the only ones homes keep off and the view draws.</summary>
        public static bool Stream(PlanetState P, in Piece p) => P.IsStream(p.Cell);

        /// <summary>Half a river's width on the ground (m) for its flow (m3/day) - about 2.5 times a real river's at that flow (the miniature).</summary>
        public static double HalfWidth(double flow) => 0.5 * DMath.Clamp(0.07 * DMath.Sqrt(DMath.Max(0.0, flow)), 1.5, 60.0);

        /// <summary>The shape for this planet's drainage now (rebuilt when the drainage changed).</summary>
        public static void Ensure(PlanetState P, Drainage d, ulong seed)
        {
            if (d == null) { All.Clear(); _byCell.Clear(); _drain = null; return; }
            if (ReferenceEquals(d, _drain) && d.Version == _version && seed == _seed) return;
            _drain = d; _version = d.Version; _seed = seed;
            All.Clear(); _byCell.Clear();
            for (int c = 0; c < P.Cells; c++)
            {
                // (every reach that drains enough ground to be a stream in some season - its flow decides daily whether it is one: `Stream`; the shape
                // is the drainage's own, so a loaded world lays the same lines: D2-119)
                if (!P.IsLand(c) || d.AreaHa[c] < StreamAreaHa || d.BasinOf[c] >= 0) continue;
                int r = d.Receiver[c];
                if (r < 0) continue;
                Vec3d a = P.Grid.Center[c];
                Vec3d b = P.IsLand(r) ? P.Grid.Center[r] : (a * 0.35 + P.Grid.Center[r] * 0.65).Normalized;   // (into the sea)
                if (!Shore.DryLand(P, a)) continue;   // (its own cell's middle under the drawn water: the sea or a lake draws it)
                Vec3d prev = a;
                bool reached = false;
                for (int s = 1; s <= Pieces && !reached; s++)
                {
                    Vec3d along = Vec3d.Slerp(a, b, s / (double)Pieces);
                    if (s < Pieces) along = Meander(P, along, a, b, seed);
                    // the river ends where it meets the drawn water: its mouth at the waterline (before, a reach into a sea cell ran 65 % of the way to
                    // that cell's middle - out over the sea, D2-118)
                    reached = !Shore.DryLand(P, along);
                    if (reached) along = Mouth(P, prev, along);
                    Add(P, c, prev, along);
                    prev = along;
                }
                // a reach into the sea still on dry ground - a wide beach between it and the water - runs on across it the way it was going (400 m)
                if (reached || P.IsLand(r)) continue;
                Vec3d dir = (P.Grid.Center[r] - a).Normalized, last = prev;
                for (double t = 8.0; t <= 400.0; t += 8.0)
                {
                    Vec3d q = (prev * P.Radius + dir * t).Normalized;
                    if (Shore.DryLand(P, q)) { last = q; continue; }
                    Vec3d end = Mouth(P, last, q);
                    int n = 1 + (int)DMath.Floor(Vec3d.ArcDistance(prev, end, P.Radius) / 30.0);
                    Vec3d from = prev;
                    for (int k = 1; k <= n; k++) { Vec3d to = Vec3d.Slerp(prev, end, k / (double)n); Add(P, c, from, to); from = to; }
                    break;
                }
            }
            Version++;
        }

        static void Add(PlanetState P, int c, Vec3d from, Vec3d to)
        {
            int idx = All.Count;
            All.Add(new Piece { A = from, B = to, Cell = c });
            Index(P.Grid.CellOf(from), idx); Index(P.Grid.CellOf(to), idx);
            // (a long piece crossing a cell between its ends: indexed at its middle too)
            Index(P.Grid.CellOf(Vec3d.Slerp(from, to, 0.5)), idx);
        }

        static void Index(int cell, int piece)
        {
            if (!_byCell.TryGetValue(cell, out var list)) { list = new List<int>(4); _byCell[cell] = list; }
            if (!list.Contains(piece)) list.Add(piece);
        }

        /// <summary>Where a piece from dry ground to the drawn water meets the waterline - by halving - carried 2 m on into the water.</summary>
        static Vec3d Mouth(PlanetState P, Vec3d dry, Vec3d wet)
        {
            Vec3d lo = dry, hi = wet;
            for (int i = 0; i < 14; i++)
            {
                Vec3d mid = Vec3d.Slerp(lo, hi, 0.5);
                if (Shore.DryLand(P, mid)) lo = mid; else hi = mid;
            }
            double left = Vec3d.ArcDistance(lo, wet, P.Radius);
            return left > 1e-6 ? Vec3d.Slerp(lo, wet, DMath.Min(1.0, 2.0 / left)) : hi;
        }

        /// <summary>Rivers wind: a point of a reach is pushed sideways by a smooth noise of where it is (up to ~34 m).</summary>
        public static Vec3d Meander(PlanetState P, Vec3d at, Vec3d a, Vec3d b, ulong seed)
        {
            Vec3d dir = (b - a).Normalized;
            Vec3d side = Vec3d.Cross(at, dir).Normalized;
            double n = Worldgen.Noise.Fbm(seed, 7101u, at, 180.0, 2);
            return (at * P.Radius + side * (n * 34.0)).Normalized;
        }

        /// <summary>The pieces near a cell (its own and its eight neighbours'): the river lines that may pass within a cell of a point in it.</summary>
        public static void Near(PlanetState P, int cell, List<int> into)
        {
            into.Clear();
            for (int k = -1; k < 8; k++)
            {
                int cc = k < 0 ? cell : P.Grid.Nbr8[cell * 8 + k];
                if (cc < 0 || !_byCell.TryGetValue(cc, out var list)) continue;
                for (int i = 0; i < list.Count; i++) if (!into.Contains(list[i])) into.Add(list[i]);
            }
        }

        /// <summary>
        /// The nearest river's centre line to a point of the ground (m along the surface, approximately) and half its width there (by its flow now);
        /// double.MaxValue where none runs within a cell or so.
        /// </summary>
        public static double Nearest(PlanetState P, Vec3d dir, int cell, out double half)
        {
            half = 0;
            double best = double.MaxValue;
            if (All.Count == 0) return best;
            var g = P.Grid;
            for (int k = -1; k < 8; k++)
            {
                int cc = k < 0 ? cell : g.Nbr8[cell * 8 + k];
                if (cc < 0 || !_byCell.TryGetValue(cc, out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = All[list[i]];
                    if (!P.IsStream(p.Cell)) continue;
                    double dd = SegDist(dir, p.A, p.B) * P.Radius;
                    if (dd < best) { best = dd; half = HalfWidth(P.RiverFlow[p.Cell]); }
                }
            }
            return best;
        }

        /// <summary>Distance (unit-sphere chord) from a point to a short segment between two unit vectors.</summary>
        public static double SegDist(Vec3d p, Vec3d a, Vec3d b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, abz = b.Z - a.Z;
            double apx = p.X - a.X, apy = p.Y - a.Y, apz = p.Z - a.Z;
            double l2 = abx * abx + aby * aby + abz * abz;
            double t = l2 > 1e-18 ? (apx * abx + apy * aby + apz * abz) / l2 : 0.0;
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            double dx = apx - abx * t, dy = apy - aby * t, dz = apz - abz * t;
            return DMath.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
