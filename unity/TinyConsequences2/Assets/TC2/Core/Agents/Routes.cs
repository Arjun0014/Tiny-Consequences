using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;

namespace TC2.Core.Agents
{
    /// <summary>
    /// A walking route between two settlements over the surface cells: a polyline of unit vectors with
    /// cumulative arc length (metres). Geometry always runs from the lower settlement id A to the higher B;
    /// a walk from B to A runs S from Length down to 0. Derived data: cached, rebuilt identically on demand.
    /// </summary>
    public sealed class Route
    {
        public int A, B;
        public Vec3d[] Pts;
        public double[] S;
        public int[] Cell;     // cell of each segment (segment k is Pts[k] -> Pts[k+1])
        public double Length;

        public Vec3d PointAt(double s)
        {
            if (s <= 0) return Pts[0];
            if (s >= Length) return Pts[Pts.Length - 1];
            int k = Segment(s);
            double seg = S[k + 1] - S[k];
            double u = seg > 0 ? (s - S[k]) / seg : 0;
            return (Pts[k] * (1 - u) + Pts[k + 1] * u).Normalized;
        }

        /// <summary>Index k of the segment containing arc length s (S[k] &lt;= s &lt; S[k+1]).</summary>
        public int Segment(double s)
        {
            int lo = 0, hi = S.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (S[mid] <= s) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }

    /// <summary>Finds and caches routes (A* over land cells, 8-connected, slope and forest costs). Every input of a route stays as it is until the
    /// cache is cleared - the relief, the land and the settlements change only with a clearing, and the woods are read as they stood at the last
    /// turn of the plates (`PlanetState.WayForest`, D2-160) - so a route built late, after a load or first for the view is the route built early.</summary>
    public sealed class RouteCache
    {
        /// <summary>`--oldways`: routes read the woods as they are when the route is built (before D2-160) - a route built after a load, or first
        /// for the view, could differ from the one the running world had cached. On the same binary.</summary>
        public static bool LiveForest;

        readonly Dictionary<long, Route> _cache = new Dictionary<long, Route>();
        readonly HashSet<long> _unreachable = new HashSet<long>();
        readonly Dictionary<long, Route> _toCell = new Dictionary<long, Route>();   // a settlement to a place (D2-045)
        readonly HashSet<long> _cellUnreachable = new HashSet<long>();
        public int Version;

        public static long Key(int a, int b) { if (a > b) { int t = a; a = b; b = t; } return ((long)a << 32) | (uint)b; }

        /// <summary>A place as a route's end: cell c is -(c + 1) (settlements are positive ids).</summary>
        public static int PlaceEnd(int cell) => -(cell + 1);
        public static int CellOfEnd(int end) => -end - 1;

        public void Invalidate() { _cache.Clear(); _unreachable.Clear(); _toCell.Clear(); _cellUnreachable.Clear(); Version++; }

        public Route Get(Simulation.World w, int s1, int s2)
        {
            if (s1 < 0) return GetToCell(w, s2, CellOfEnd(s1));
            if (s2 < 0) return GetToCell(w, s1, CellOfEnd(s2));
            int a = Math.Min(s1, s2), b = Math.Max(s1, s2);
            long key = Key(a, b);
            if (_cache.TryGetValue(key, out var r)) return r;
            if (_unreachable.Contains(key)) return null;
            r = Build(w, a, b);
            if (r == null) _unreachable.Add(key); else _cache[key] = r;
            return r;
        }

        /// <summary>A walk from a settlement to a place (a cell's centre): A the settlement, B = PlaceEnd(cell).</summary>
        public Route GetToCell(Simulation.World w, int sid, int cell)
        {
            if (sid <= 0 || cell < 0 || !w.Settlements.Exists(sid)) return null;
            long key = ((long)sid << 32) | (uint)cell;
            if (_toCell.TryGetValue(key, out var r)) return r;
            if (_cellUnreachable.Contains(key)) return null;
            ref var s = ref w.Settlements.Get(sid);
            r = Path(w, s.Cell, cell, s.Anchor, w.Planet.Grid.Center[cell], sid, PlaceEnd(cell));
            if (r == null) _cellUnreachable.Add(key); else _toCell[key] = r;
            return r;
        }

        // A* scratch (reused; derived data only)
        double[] _g; int[] _came; int[] _stamp; int _stampNow;
        readonly List<long> _open = new List<long>();

        Route Build(Simulation.World w, int a, int b)
        {
            ref var sa = ref w.Settlements.Get(a);
            ref var sb = ref w.Settlements.Get(b);
            return Path(w, sa.Cell, sb.Cell, sa.Anchor, sb.Anchor, a, b);
        }

        Route Path(Simulation.World w, int start, int goal, Vec3d from, Vec3d to, int a, int b)
        {
            var P = w.Planet;
            int n = P.Cells;
            if (_g == null || _g.Length != n) { _g = new double[n]; _came = new int[n]; _stamp = new int[n]; }
            _stampNow++;
            var heap = new MinHeap();
            double cellM = P.CellSize;
            _g[start] = 0; _came[start] = -1; _stamp[start] = _stampNow;
            heap.Push(Heuristic(P, start, goal), start);
            var grid = P.Grid;
            int found = -1, expanded = 0;
            while (heap.Count > 0)
            {
                heap.Pop(out double f, out int c);
                if (c == goal) { found = c; break; }
                if (++expanded > 20000) break;
                double gc = _g[c];
                for (int k = 0; k < 8; k++)
                {
                    int nb = grid.Nbr8[c * 8 + k];
                    if (nb == c || !P.IsLand(nb)) continue;
                    double d = Vec3d.ArcDistance(grid.Center[c], grid.Center[nb], P.Radius);
                    double climb = DMath.Abs((double)P.Elev[nb] - P.Elev[c]) / Math.Max(1.0, d / P.GeoScale);   // (the ground's own steepness at any size, D2-085)
                    double cost = d * (1.0 + 4.0 * climb + 0.35 * (LiveForest ? P.Forest[nb] : P.WayForest[nb]));   // (the woods as they stood at the last turn of the plates, when the routes were last cleared: D2-160)
                    double ng = gc + cost;
                    if (_stamp[nb] != _stampNow || ng < _g[nb] - 1e-9)
                    {
                        _stamp[nb] = _stampNow; _g[nb] = ng; _came[nb] = c;
                        heap.Push(ng + Heuristic(P, nb, goal), nb);
                    }
                }
            }
            if (found < 0) return null;
            var cells = new List<int>();
            for (int c = found; c != -1; c = _came[c]) cells.Add(c);
            cells.Reverse();
            // polyline: anchor A, centres of the intermediate cells, anchor B (or the place's centre)
            var pts = new List<Vec3d> { from };
            for (int i = 1; i < cells.Count - 1; i++) pts.Add(grid.Center[cells[i]]);
            pts.Add(to);
            var route = new Route { A = a, B = b, Pts = pts.ToArray() };
            route.S = new double[route.Pts.Length];
            route.Cell = new int[Math.Max(1, route.Pts.Length - 1)];
            for (int i = 1; i < route.Pts.Length; i++)
            {
                route.S[i] = route.S[i - 1] + Vec3d.ArcDistance(route.Pts[i - 1], route.Pts[i], P.Radius);
                // the segment's cell: the cell at its midpoint
                route.Cell[i - 1] = grid.CellOf(route.Pts[i - 1] + route.Pts[i]);
            }
            route.Length = route.S[route.S.Length - 1];
            return route;
        }

        static double Heuristic(PlanetState P, int c, int goal) => Vec3d.ArcDistance(P.Grid.Center[c], P.Grid.Center[goal], P.Radius);

        /// <summary>Binary min-heap of (priority, cell) with deterministic ties (lower cell first).</summary>
        sealed class MinHeap
        {
            double[] _p = new double[256]; int[] _c = new int[256]; int _n;
            public int Count => _n;
            static bool Less(double pa, int ca, double pb, int cb) => pa < pb || (pa == pb && ca < cb);
            public void Push(double p, int c)
            {
                if (_n == _p.Length) { Array.Resize(ref _p, _n * 2); Array.Resize(ref _c, _n * 2); }
                int i = _n++;
                while (i > 0) { int par = (i - 1) >> 1; if (!Less(p, c, _p[par], _c[par])) break; _p[i] = _p[par]; _c[i] = _c[par]; i = par; }
                _p[i] = p; _c[i] = c;
            }
            public void Pop(out double p, out int c)
            {
                p = _p[0]; c = _c[0];
                _n--;
                if (_n == 0) return;
                double lp = _p[_n]; int lc = _c[_n];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1; if (l >= _n) break;
                    int r = l + 1; int m = (r < _n && Less(_p[r], _c[r], _p[l], _c[l])) ? r : l;
                    if (!Less(_p[m], _c[m], lp, lc)) break;
                    _p[i] = _p[m]; _c[i] = _c[m]; i = m;
                }
                _p[i] = lp; _c[i] = lc;
            }
        }
    }
}
