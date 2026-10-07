using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Ways over the sea between two villages' landings (C9.3): from one landing out to the nearest sea cell, A* over the sea's cells
    /// (8-connected, by distance) to the sea cell nearest the other landing, and in to it - a polyline with arc lengths, the same `Route`
    /// a walk follows. Derived from the coast and the landings: cached by the pair, rebuilt when the coast moves or a landing is chosen
    /// again. None between villages without a landing, or farther than `Most` by sea.
    /// </summary>
    public sealed class SeaRoutes
    {
        /// <summary>m: the longest way by sea a route is looked for (a long day's row there and back).</summary>
        public const double Most = 16000.0;

        readonly Dictionary<long, Route> _cache = new Dictionary<long, Route>();
        readonly Dictionary<long, long> _made = new Dictionary<long, long>();   // (the pair's landings and coast when it was made)
        int _coast = -1;

        public void Bind(World w) { _cache.Clear(); _made.Clear(); _coast = -1; }

        /// <summary>The way by sea between two villages' landings (A the lower id), or null.</summary>
        public Route Get(World w, int s1, int s2)
        {
            if (s1 == s2 || !w.Settlements.Exists(s1) || !w.Settlements.Exists(s2)) return null;
            if (_coast != w.Planet.CoastVersion) { _cache.Clear(); _made.Clear(); _coast = w.Planet.CoastVersion; }
            int a = s1 < s2 ? s1 : s2, b = s1 < s2 ? s2 : s1;
            ref var sa = ref w.Settlements.Get(a);
            ref var sb = ref w.Settlements.Get(b);
            if (sa.LandAt == 0 || sb.LandAt == 0) return null;
            long key = RouteCache.Key(a, b), stamp = sa.LandAt * 31 + sb.LandAt;
            if (_made.TryGetValue(key, out long st) && st == stamp) return _cache.TryGetValue(key, out var r0) ? r0 : null;
            var r = Build(w, a, b);
            _made[key] = stamp;
            if (r == null) _cache.Remove(key); else _cache[key] = r;
            return r;
        }

        static Route Build(World w, int a, int b)
        {
            var P = w.Planet; var g = P.Grid;
            ref var sa = ref w.Settlements.Get(a);
            ref var sb = ref w.Settlements.Get(b);
            Vec3d la = sa.LocalToSphere(sa.LandX, sa.LandY, P.Radius), lb = sb.LocalToSphere(sb.LandX, sb.LandY, P.Radius);
            if (Vec3d.ArcDistance(la, lb, P.Radius) > Most) return null;
            if (!Boats.Edge(w, g.CellOf(la), la, false, out int ca, out _) || !Boats.Edge(w, g.CellOf(lb), lb, false, out int cb, out _)) return null;
            var path = Search(w, ca, cb);
            if (path == null) return null;
            var pts = new List<Vec3d> { la };
            for (int i = 0; i < path.Count; i++) pts.Add(g.Center[path[i]]);
            pts.Add(lb);
            var r = new Route { A = a, B = b, Pts = pts.ToArray(), S = new double[pts.Count], Cell = new int[pts.Count - 1] };
            for (int i = 1; i < pts.Count; i++) r.S[i] = r.S[i - 1] + Vec3d.ArcDistance(pts[i - 1], pts[i], P.Radius);
            for (int i = 0; i < r.Cell.Length; i++) r.Cell[i] = i == 0 ? ca : path[System.Math.Min(i - 1, path.Count - 1)];
            r.Length = r.S[pts.Count - 1];
            return r.Length <= Most ? r : null;
        }

        /// <summary>A* over the sea's cells from one to another (8-connected, cost the distance between centres); null past `Most`.</summary>
        static List<int> Search(World w, int from, int to)
        {
            var P = w.Planet; var g = P.Grid;
            double R = P.Radius;
            var cost = new Dictionary<int, double> { [from] = 0.0 };
            var came = new Dictionary<int, int>();
            var open = new SortedSet<(double f, int c)> { (Vec3d.ArcDistance(g.Center[from], g.Center[to], R), from) };
            var done = new HashSet<int>();
            while (open.Count > 0)
            {
                var top = open.Min; open.Remove(top);
                int c = top.c;
                if (c == to)
                {
                    var path = new List<int> { c };
                    while (came.TryGetValue(c, out int pc)) { c = pc; path.Add(c); }
                    path.Reverse();
                    return path;
                }
                if (!done.Add(c)) continue;
                double gc = cost[c];
                if (gc > Most) continue;
                for (int j = 0; j < 8; j++)
                {
                    int nb = g.Nbr8[c * 8 + j];
                    if (P.IsLand(nb) || done.Contains(nb)) continue;
                    double gn = gc + Vec3d.ArcDistance(g.Center[c], g.Center[nb], R);
                    if (cost.TryGetValue(nb, out double old) && old <= gn) continue;
                    cost[nb] = gn; came[nb] = c;
                    open.Add((gn + Vec3d.ArcDistance(g.Center[nb], g.Center[to], R), nb));
                }
            }
            return null;
        }
    }
}
