using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Agents
{
    /// <summary>
    /// A way between villages as it is drawn and as its travellers are seen walking it (the visual truth audit, session 17): its route (`Route`,
    /// the A* path from cell to cell), except that where an end lies in a village with streets, it comes in along the street nearest its
    /// bearing - from the square out past the village's last building (a street's line is kept clear of houses all its length: C13.1-C13.3) -
    /// then round the village's edge to where the route itself leaves the built ground (no chord back across the lots), and on along the route;
    /// through a village it passes, in by the street nearest where it comes, over the square and out by the street nearest where it goes. Before, a way was drawn (and its travellers seen) straight from the village's centre to the
    /// next cell's centre, across the lots the village had laid out: houses stood on roads (seed 4 at year 300: 286 homes). Ways merged into one
    /// street (within 35 degrees) all come in by it; a way with no street of its own (a track, or a fifth way) by the nearest. A way into a hamlet
    /// with no street keeps its route. Derived (rebuilt when either village's streets, square or households change); reads only; the
    /// simulation's travellers keep their route (their arc length is read onto this line by its share).
    /// </summary>
    public static class WayLines
    {
        sealed class Entry { public Route R; public long Sig; public Vec3d[] Pts; public double[] S; public double Len; }
        static World _w;
        static readonly Dictionary<long, Entry> _cache = new Dictionary<long, Entry>();

        /// <summary>m past the last house along a street that a way's street part reaches (and at least `Streets.Look`).</summary>
        public const double Past = 8.0;

        /// <summary>The drawn line of route r (its points on the sphere).</summary>
        public static Vec3d[] Of(World w, Route r) => Get(w, r).Pts;

        /// <summary>Where a traveller s metres along route r is seen: the same share of the drawn line.</summary>
        public static Vec3d PointAt(World w, Route r, double s)
        {
            var e = Get(w, r);
            if (ReferenceEquals(e.Pts, r.Pts)) return r.PointAt(s);
            double u = r.Length > 0 ? DMath.Clamp01(s / r.Length) : 0.0;
            double d = u * e.Len;
            var pts = e.Pts; var S = e.S;
            if (d <= 0) return pts[0];
            if (d >= e.Len) return pts[pts.Length - 1];
            int lo = 0, hi = S.Length - 2;
            while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (S[mid] <= d) lo = mid; else hi = mid - 1; }
            double seg = S[lo + 1] - S[lo];
            double f = seg > 0 ? (d - S[lo]) / seg : 0;
            return (pts[lo] * (1 - f) + pts[lo + 1] * f).Normalized;
        }

        static long Key(Route r) => ((long)r.A << 32) ^ (uint)r.B;

        static Entry Get(World w, Route r)
        {
            if (!ReferenceEquals(_w, w)) { _w = w; _cache.Clear(); }
            long key = Key(r);
            long sig = (Sig(w, r.A) * 1000003L + Sig(w, r.B)) * 31L + SimTime.FloorDiv(w.Now, SimTime.Day);   // (and once a day: the villages it passes grow)
            if (_cache.TryGetValue(key, out var e) && ReferenceEquals(e.R, r) && e.Sig == sig) return e;
            e = new Entry { R = r, Sig = sig };
            Build(w, r, e);
            _cache[key] = e;
            return e;
        }

        /// <summary>What the drawn line depends on at a village end: its streets, its square, how many households line them.</summary>
        static long Sig(World w, int sid)
        {
            if (sid <= 0 || !w.Settlements.Exists(sid)) return 0;
            ref var s = ref w.Settlements.Get(sid);
            long h = s.StreetN;
            for (int k = 0; k < s.StreetN; k++) h = h * 31 + (long)(s.StreetAt(k) * 1000.0);
            h = h * 31 + (long)(s.SquareX * 10.0); h = h * 31 + (long)(s.SquareY * 10.0);
            h = h * 31 + w.HouseholdsOf(sid).Count;
            return h;
        }

        static readonly List<Vec3d> _pts = new List<Vec3d>();

        static readonly List<Vec3d> _head = new List<Vec3d>(), _tail = new List<Vec3d>();

        static void Build(World w, Route r, Entry e)
        {
            var P = w.Planet;
            var src = r.Pts;
            int first = 0, last = src.Length - 1;
            bool h = End(w, r, true, _head, out int j), t = End(w, r, false, _tail, out int j2);
            if (h) first = j;
            if (t) last = j2;
            _pts.Clear();
            if (h) _pts.AddRange(_head); else _pts.Add(src[0]);
            for (int i = first; i <= last; i++) if (i > 0 && i < src.Length - 1) _pts.Add(src[i]);
            if (t) for (int i = _tail.Count - 1; i >= 0; i--) _pts.Add(_tail[i]); else _pts.Add(src[src.Length - 1]);
            bool passed = Through(w, r, _pts);
            if (!h && !t && !passed) { e.Pts = src; e.S = r.S; e.Len = r.Length; return; }
            e.Pts = _pts.ToArray();
            e.S = new double[e.Pts.Length];
            for (int i = 1; i < e.Pts.Length; i++) e.S[i] = e.S[i - 1] + Vec3d.ArcDistance(e.Pts[i - 1], e.Pts[i], P.Radius);
            e.Len = e.S[e.S.Length - 1];
        }

        /// <summary>m from a village's square to the far side of its farthest home (the built ground: works far out - a quarry at its rock, a mine -
        /// are no part of it).</summary>
        public static double Built(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            double built = 0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.Fallen(in b, w.Now) || !Structures.IsHome(b.Kind)) continue;
                double bx = (double)b.X - s.SquareX, by = (double)b.Y - s.SquareY, bd = DMath.Sqrt(bx * bx + by * by) + Footprints.Reach(Footprints.PlanOf(in b));
                if (bd > built) built = bd;
            }
            return built;
        }

        static readonly List<Vec3d> _pass = new List<Vec3d>();

        /// <summary>
        /// The villages a way passes (not its ends): the part of the line within a village's built ground is replaced by its passage - in by the
        /// street nearest where it comes (round the edge to its end), over the square, out by the street nearest where it goes (a hamlet with no
        /// street, or a way that only grazes the edge: round the edge). True when any was.
        /// </summary>
        static bool Through(World w, Route r, List<Vec3d> pts)
        {
            var P = w.Planet;
            bool any = false;
            double ox0, oy0;
            for (int v = w.Settlements.First(); v != 0; v = w.Settlements.Next(v))
            {
                if (v == r.A || v == r.B) continue;
                ref var s = ref w.Settlements.Get(v);
                if (!s.Inhabited || w.HouseholdsOf(v).Count == 0) continue;
                // (a quick reject: no point of the line within 600 m of the village)
                bool near = false;
                for (int i = 0; i < pts.Count && !near; i++) near = Vec3d.ArcDistance(pts[i], s.Anchor, P.Radius) < 600.0;
                if (!near) continue;
                double R = Built(w, v) + 8.0;
                if (R < 20.0) continue;
                int a = -1, b = -1;
                for (int i = 0; i < pts.Count; i++)
                {
                    s.SphereToLocal(pts[i], P.Radius, out double x, out double y);
                    double d = DMath.Sqrt((x - s.SquareX) * (x - s.SquareX) + (y - s.SquareY) * (y - s.SquareY));
                    if (d < R) { if (a < 0) a = i; b = i; }
                }
                // (a segment can also cut a corner of the circle between two outside points: the nearest approach of each)
                if (a < 0)
                {
                    for (int i = 0; i + 1 < pts.Count && a < 0; i++)
                    {
                        s.SphereToLocal(pts[i], P.Radius, out double x0, out double y0);
                        s.SphereToLocal(pts[i + 1], P.Radius, out double x1, out double y1);
                        double vx = x1 - x0, vy = y1 - y0, l2 = vx * vx + vy * vy;
                        double f = l2 > 1e-9 ? DMath.Clamp01(((s.SquareX - x0) * vx + (s.SquareY - y0) * vy) / l2) : 0.0;
                        double cx = x0 + vx * f - s.SquareX, cy = y0 + vy * f - s.SquareY;
                        if (cx * cx + cy * cy < R * R) { a = i + 1; b = i; }   // (no point inside: the passage goes between i and i + 1)
                    }
                    if (a < 0) continue;
                }
                if (a == 0 || b == pts.Count - 1) continue;   // (an end inside another village's ground: left as it is)
                // where the line enters and leaves the circle
                // (points inside: in between a - 1 and a, out between b and b + 1; none inside: both on the segment a - 1 to a)
                Cross(in s, pts[a - 1], pts[a], R, P.Radius, out double ix, out double iy, true);
                if (a <= b) Cross(in s, pts[b], pts[b + 1], R, P.Radius, out ox0, out oy0, false);
                else Cross(in s, pts[a - 1], pts[a], R, P.Radius, out ox0, out oy0, false);
                double ox = ox0, oy = oy0;
                double ti = DMath.Atan2(iy, ix), to = DMath.Atan2(oy, ox);
                _pass.Clear();
                _pass.Add(s.LocalToSphere(s.SquareX + ix, s.SquareY + iy, P.Radius));
                int ka = NearestStreet(in s, ti), kb = NearestStreet(in s, to);
                if (ka >= 0 && kb >= 0 && ka != kb)
                {
                    double sa = s.StreetAt(ka), sb = s.StreetAt(kb);
                    Arc(in s, ti, sa, R, P.Radius, _pass);
                    _pass.Add(s.LocalToSphere(s.SquareX + DMath.Cos(sa) * R, s.SquareY + DMath.Sin(sa) * R, P.Radius));
                    Along(in s, sa, R, 0.0, P.Radius, _pass);
                    _pass.Add(s.LocalToSphere(s.SquareX, s.SquareY, P.Radius));
                    Along(in s, sb, 0.0, R, P.Radius, _pass);
                    Arc(in s, sb, to, R, P.Radius, _pass);
                }
                else Arc(in s, ti, to, R, P.Radius, _pass);
                _pass.Add(s.LocalToSphere(s.SquareX + ox, s.SquareY + oy, P.Radius));
                int from = a, count = b - a + 1;
                if (count > 0) pts.RemoveRange(from, count);
                pts.InsertRange(from, _pass);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Points along a street (bearing a from the square) from d0 to d1 m (the far end included, the near end not): close to each end - 10 m off
        /// the square's end, 6 m off the other - so the drawing's rounding of a corner stays within a few metres of it (the green, the edge).
        /// </summary>
        static void Along(in Settlement s, double a, double d0, double d1, double radius, List<Vec3d> into)
        {
            double ca = DMath.Cos(a), sa = DMath.Sin(a);
            double near0 = d0 < d1 ? d0 + (d0 < 1.0 ? 10.0 : 6.0) : d0 - 6.0, near1 = d0 < d1 ? d1 - 6.0 : d1 + (d1 < 1.0 ? 10.0 : 6.0);
            if ((d0 < d1 && near0 < near1) || (d0 > d1 && near0 > near1))
            {
                into.Add(s.LocalToSphere(s.SquareX + ca * near0, s.SquareY + sa * near0, radius));
                into.Add(s.LocalToSphere(s.SquareX + ca * near1, s.SquareY + sa * near1, radius));
            }
            if (d1 > 1e-6) into.Add(s.LocalToSphere(s.SquareX + ca * d1, s.SquareY + sa * d1, radius));
        }

        /// <summary>The street of a village nearest a bearing from its square (-1: none).</summary>
        static int NearestStreet(in Settlement s, double a)
        {
            int k = -1; double best = 0.75;   // (within 43 degrees: else round the edge)
            for (int i = 0; i < s.StreetN; i++) { double d = Streets.Apart(a, s.StreetAt(i)); if (d < best) { best = d; k = i; } }
            return k;
        }

        /// <summary>Points round a village's edge (radius R about its square) from bearing a0 to a1 the shorter way, every ~8.6 degrees (not the ends).</summary>
        static void Arc(in Settlement s, double a0, double a1, double R, double radius, List<Vec3d> into)
        {
            double turn = a1 - a0;
            while (turn > DMath.PI) turn -= DMath.TwoPi;
            while (turn < -DMath.PI) turn += DMath.TwoPi;
            int steps = (int)(DMath.Abs(turn) / 0.15);
            for (int k = 1; k <= steps; k++) { double aa = a0 + turn * k / (steps + 1); into.Add(s.LocalToSphere(s.SquareX + DMath.Cos(aa) * R, s.SquareY + DMath.Sin(aa) * R, radius)); }
        }

        /// <summary>Where the segment p0-p1 meets the circle of radius R about a village's square (local metres from the square): entering
        /// (the first meeting from p0) or leaving (the last).</summary>
        static void Cross(in Settlement s, Vec3d p0, Vec3d p1, double R, double radius, out double cx, out double cy, bool entering)
        {
            s.SphereToLocal(p0, radius, out double x0, out double y0);
            s.SphereToLocal(p1, radius, out double x1, out double y1);
            x0 -= s.SquareX; y0 -= s.SquareY; x1 -= s.SquareX; y1 -= s.SquareY;
            double vx = x1 - x0, vy = y1 - y0, A = vx * vx + vy * vy, B = 2 * (x0 * vx + y0 * vy), C = x0 * x0 + y0 * y0 - R * R;
            double disc = B * B - 4 * A * C;
            double f;
            if (A < 1e-12 || disc < 0) f = 0.5;
            else
            {
                double sq = DMath.Sqrt(disc), f0 = (-B - sq) / (2 * A), f1 = (-B + sq) / (2 * A);
                f = DMath.Clamp01(entering ? f0 : f1);
            }
            cx = x0 + vx * f; cy = y0 + vy * f;
        }

        /// <summary>
        /// One end of a route in a village with streets: its lead from the square outward (along the street, then round the village's edge to where
        /// the route leaves it), and the index of the route's first point (from that end) beyond the edge - the route's inner points before it are
        /// dropped. False where the end is no village, or it has no street.
        /// </summary>
        static bool End(World w, Route r, bool head, List<Vec3d> lead, out int join)
        {
            lead.Clear(); join = head ? 0 : r.Pts.Length - 1;
            int sid = head ? r.A : r.B;
            if (sid <= 0 || !w.Settlements.Exists(sid) || r.Length < 1.0) return false;
            ref var s = ref w.Settlements.Get(sid);
            if (s.StreetN == 0) return false;
            var P = w.Planet;
            // the bearing the way leaves at (as Streets.Refresh reads it), and the street within the merge of it
            double along = DMath.Min(Streets.Look, 0.5 * r.Length);
            Vec3d p = r.PointAt(head ? along : r.Length - along);
            s.SphereToLocal(p, P.Radius, out double x, out double y);
            double dx = x - s.SquareX, dy = y - s.SquareY;
            if (dx * dx + dy * dy < 1.0) return false;
            double a = DMath.Atan2(dy, dx), bestA = double.MaxValue; int k = -1;
            for (int i = 0; i < s.StreetN; i++) { double d = Streets.Apart(a, s.StreetAt(i)); if (d < bestA) { bestA = d; k = i; } }
            if (k < 0) return false;
            // how far the street is drawn: its last fronting house and a little (the view's VillageGround), at least `Streets.Look`
            Streets.Line(in s, 3 * k, out double ox, out double oy, out double ux, out double uy, out _);
            double reach = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var h = ref w.Households.Get(hl[i]);
                if (Streets.Facing(in s, h.HomeX, h.HomeY, out double tx, out double ty, out int line) && line == 3 * k)
                {
                    double al = (tx - ox) * ux + (ty - oy) * uy;
                    if (al > reach) reach = al;
                }
            }
            // out past the village's built ground (its farthest building from the square and a little) - the street's line is clear of houses
            double E = DMath.Min(DMath.Max(DMath.Max(Streets.Look, reach + Past), Built(w, sid) + 12.0), 0.45 * r.Length);
            lead.Add(s.LocalToSphere(s.SquareX, s.SquareY, P.Radius));
            double sa = DMath.Atan2(uy, ux);
            // (points a few metres from each corner: the drawing rounds a corner by cutting a quarter of each leg - Chaikin - and a long leg's
            // quarter would cut diagonally across the lots)
            Along(in s, sa, 0.0, E, P.Radius, lead);
            // where the route leaves the circle of the built ground (from this end); round the edge to it, then on along the route
            int n = r.Pts.Length;
            double pxPrev = 0, pyPrev = 0, dPrev = 0;
            for (int step = 0; step < n; step++)
            {
                int i = head ? step : n - 1 - step;
                s.SphereToLocal(r.Pts[i], P.Radius, out double px, out double py);
                px -= s.SquareX; py -= s.SquareY;
                double d = DMath.Sqrt(px * px + py * py);
                if (step > 0 && d > E)
                {
                    double f = d - dPrev > 1e-9 ? DMath.Clamp01((E - dPrev) / (d - dPrev)) : 1.0;
                    double cx = pxPrev + (px - pxPrev) * f, cy = pyPrev + (py - pyPrev) * f;
                    double ca = DMath.Atan2(cy, cx), turn = ca - sa;
                    while (turn > DMath.PI) turn -= DMath.TwoPi;
                    while (turn < -DMath.PI) turn += DMath.TwoPi;
                    int steps = (int)(DMath.Abs(turn) / 0.15);
                    for (int k2 = 1; k2 <= steps; k2++) { double aa = sa + turn * k2 / (steps + 1); lead.Add(s.LocalToSphere(s.SquareX + DMath.Cos(aa) * E, s.SquareY + DMath.Sin(aa) * E, P.Radius)); }
                    lead.Add(s.LocalToSphere(s.SquareX + cx, s.SquareY + cy, P.Radius));
                    join = i;
                    return true;
                }
                pxPrev = px; pyPrev = py; dPrev = d;
            }
            join = head ? n - 1 : 0;   // (a short way that never leaves the built ground: the two ends meet)
            return true;
        }
    }
}
