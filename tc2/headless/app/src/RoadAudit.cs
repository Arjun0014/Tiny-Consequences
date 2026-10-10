using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// `place --load SAVE --roads [--sid N] [--ex E] [--show K] [--rawways]` (the visual truth audit): every standing building of every inhabited village
        /// against the ground the view draws - its streets and back lanes (drawn where households front them, ~3 m either side), the ways between
        /// villages (each worn way's route, smoothed as TerrainLod.BuildRoads draws it, ~1.2-3.2 m either side by its wear), the square or market
        /// place's bare earth, the palisade's posts - at its true size and at the size the view draws it far off (scaled about its centre by the
        /// camera's distance up to 1.6x, as far as its neighbours leave it room: WorldObjectsView.DrawHome). Also the simulation's own test
        /// (`Footprints.Open`: within `Streets.Width` of a street or lane, the green, astride a ring). Reads only.
        /// </summary>
        static void RoadAudit(World w, int only, double exMax, int list, bool raw = false, string feature = null, bool a_works = false)
        {
            var P = w.Planet;
            // the ways as drawn: Chaikin twice over each worn way's route (TerrainLod.BuildRoads), each segment with the half width its ink reaches
            // half strength at (1 - Edge(0.55 h, 1.4 h, m) = 0.5 at m = 0.975 h, h = 1.2 + 2 worn on the near patches)
            var ways = new List<Way>();
            w.Ways.Collect(w.Now, ways);
            var segs = new List<(Vec3d a, Vec3d b, double half, int A, int B)>();
            var pts = new List<Vec3d>(); var tmp = new List<Vec3d>();
            for (int i = 0; i < ways.Count; i++)
            {
                var way = ways[i];
                if (!w.Settlements.Exists(way.A) || !w.Settlements.Exists(way.B)) continue;
                var r = w.Routes.Get(w, way.A, way.B);
                if (r == null || r.Pts == null || r.Pts.Length < 2) continue;
                byte state = Ways.StateOf(way.Use);
                double worn = state >= 3 ? 1.0 : state == 2 ? 0.62 + 0.3 * Clamp01((way.Use - Ways.Path) / (Ways.Road - Ways.Path))
                                                            : 0.25 + 0.3 * Clamp01((way.Use - Ways.Track) / (Ways.Path - Ways.Track));
                pts.Clear(); pts.AddRange(raw ? r.Pts : WayLines.Of(w, r));   // (--rawways: as drawn before session 17, the route's own points)
                for (int it = 0; it < 2; it++)
                {
                    tmp.Clear(); tmp.Add(pts[0]);
                    for (int k = 0; k + 1 < pts.Count; k++) { tmp.Add((pts[k] * 0.75 + pts[k + 1] * 0.25).Normalized); tmp.Add((pts[k] * 0.25 + pts[k + 1] * 0.75).Normalized); }
                    tmp.Add(pts[pts.Count - 1]);
                    pts.Clear(); pts.AddRange(tmp);
                }
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    if (!raw && OnASquare(w, (pts[k] + pts[k + 1]).Normalized)) continue;   // (as the view: no road drawn over a square)
                    segs.Add((pts[k], pts[k + 1], 0.975 * (1.2 + 2.0 * worn), way.A, way.B));
                }
            }
            string[] names = { "street", "lane", "way", "square", "wall", "home" };
            int nf = names.Length;
            var simN = new int[4];                      // the simulation's own test: street/lane, green, ring, river
            var at1 = new int[nf]; var atE = new int[nf];   // homes meeting each drawn feature at true size, at the far size
            var wk1 = new int[nf]; var wkE = new int[nf];   // works the same
            var deep = new List<(double depth, string what)>();
            int homes = 0, works = 0, villages = 0;
            var exOf = new Dictionary<int, double>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);   // (a copy: read by the local functions below)
                if (!s.Inhabited || (only != 0 && sid != only)) continue;
                villages++;
                string vname = w.History.NameOf(EntityRef.Settlement(sid));
                // the lines as the view draws them (TerrainLod.ReadGround / VillageGround): from the square (a lane from LaneStart - 10) out to the farthest
                // home that fronts each, and 4 m on (the fade's half), ~3 m either side (the earth's half strength: 1 - Edge(2.2, 3.8))
                int lines = Streets.Lines(in s);
                var reach = new double[lines];
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    ref var h = ref w.Households.Get(hl[i]);
                    if (Streets.Facing(in s, h.HomeX, h.HomeY, out double tx, out double ty, out int line) && line >= 0)
                    {
                        Streets.Line(in s, line, out double ox, out double oy, out double ux, out double uy, out _);
                        double along = (tx - ox) * ux + (ty - oy) * uy;
                        if (along > reach[line]) reach[line] = along;
                    }
                }
                // the ways near this village in its frame
                var local = new List<(double ax, double ay, double bx, double by, double half, string to)>();
                for (int k = 0; k < segs.Count; k++)
                {
                    var g = segs[k];
                    if (Vec3d.ArcDistance(g.a, s.Anchor, P.Radius) > 400 && Vec3d.ArcDistance(g.b, s.Anchor, P.Radius) > 400) continue;
                    s.SphereToLocal(g.a, P.Radius, out double ax, out double ay);
                    s.SphereToLocal(g.b, P.Radius, out double bx, out double by);
                    int other = g.A == sid ? g.B : g.A;
                    local.Add((ax, ay, bx, by, g.half, (g.A == sid || g.B == sid ? "" : "passing ") + w.History.NameOf(EntityRef.Settlement(other))));
                }
                double sqR = s.MarketR > 0 ? s.MarketR - 3.0 : 12.0;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind)) continue;
                    if (Homes.RuinLeft(in b, w.Now) < 1.0) continue;   // (a ruin falling in: drawn low)
                    bool home = Structures.IsHome(b.Kind);
                    if (home) homes++; else works++;
                    int plan = home ? Footprints.PlanOf(in b) : -1;
                    double face = Footprints.FaceOf(in s, in b);
                    if (home)
                    {
                        if (!Footprints.Open(in s, b.X, b.Y, face, plan))
                        {
                            bool green = false, line = false;
                            for (int r = 0; r < Footprints.Count(plan); r++)
                            {
                                Footprints.Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                                green |= Footprints.BoxDisc(b.X, b.Y, face, cx, cz, hx, hz, s.SquareX, s.SquareY, Footprints.Green(in s), 0.0);
                                line |= Streets.On(in s, b.X, b.Y, Streets.Width + Footprints.Reach(plan));
                            }
                            if (green) simN[1]++; else if (line) simN[0]++; else simN[2]++;
                        }
                        else if (Footprints.Wet(w, in s, b.X, b.Y, face, plan)) simN[3]++;
                    }
                    double room = Footprints.Room(w, sid, bl[i], 2.5);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        double e = pass == 0 ? 1.0 : Math.Min(exMax, room);
                        if (pass == 1 && e <= 1.0001) { for (int f = 0; f < nf; f++) if (Hit(f, 1.0)) (home ? atE : wkE)[f]++; continue; }
                        for (int f = 0; f < nf; f++) if (Hit(f, e)) (pass == 0 ? (home ? at1 : wk1) : (home ? atE : wkE))[f]++;
                    }

                    // how far the building's ground at scale e reaches into drawn feature f (m; > 0.5 counts: an obvious overlap)
                    bool Hit(int f, double e)
                    {
                        double depth = Depth(f, e, out string what);
                        if (depth <= 0.5) return false;
                        if (list > 0 && e <= 1.0001 && (feature != null ? feature == names[f] && (home || a_works) : home)) deep.Add((depth, vname + " #" + sid + " b" + bl[i] + " " + (home ? "home plan " + plan : "work " + b.Kind) + " built y" + (b.Built / SimTime.Year) +
                                                       " at " + Dist(b.X - s.SquareX, b.Y - s.SquareY).ToString("0") + " m: " + names[f] + (what != null ? " (" + what + ")" : "") +
                                                       " by " + depth.ToString("0.0") + " m at x" + e.ToString("0.00") + "  view " + LatLon(s.LocalToSphere(b.X, b.Y, P.Radius))));
                        return true;
                    }

                    double Depth(int f, double e, out string what)
                    {
                        what = null;
                        double most = 0;
                        int n = home ? Footprints.Count(plan) : 1;
                        for (int r = 0; r < n; r++)
                        {
                            double cx = 0, cz = 0, hx, hz;
                            if (home) { Footprints.Rect(plan, r, out cx, out cz, out hx, out hz); cx *= e; cz *= e; hx *= e; hz *= e; }
                            else { hx = hz = 0.886 * Footprints.WorkR(b.Kind) * e; }   // (a work's disc as the square of its area)
                            double fx = Math.Cos(face), fy = Math.Sin(face), ux = -fy, uy = fx;
                            double ox = b.X + ux * cx + fx * cz, oy = b.Y + uy * cx + fy * cz;
                            switch (f)
                            {
                                case 0: case 1:
                                    for (int li = 0; li < lines; li++)
                                    {
                                        if ((f == 1) != Streets.IsLane(li) || reach[li] <= 0) continue;
                                        Streets.Line(in s, li, out double lx, out double ly, out double vx, out double vy, out _);
                                        double from = Streets.IsLane(li) ? Streets.LaneStart - 4.0 : Footprints.SquareEarth(in s) - 1.0, to = reach[li] + 4.0;   // (as the view draws it: a street from the square's edge, a lane from where the simulation lines it)
                                        double d = SegRect(lx + vx * from, ly + vy * from, lx + vx * to, ly + vy * to, ox, oy, ux, uy, fx, fy, hx, hz);
                                        if (3.0 - d > most) { most = 3.0 - d; what = (Streets.IsLane(li) ? "lane " : "street ") + li; }
                                    }
                                    break;
                                case 2:
                                    for (int k = 0; k < local.Count; k++)
                                    {
                                        var g = local[k];
                                        double d = SegRect(g.ax, g.ay, g.bx, g.by, ox, oy, ux, uy, fx, fy, hx, hz);
                                        if (g.half - d > most) { most = g.half - d; what = "the way to " + g.to; }
                                    }
                                    break;
                                case 3:
                                {
                                    if (!home && (b.Kind == Structures.Well || b.Kind == Structures.Market || b.Kind == Structures.Granary || b.Kind == Structures.Barn)) break;   // (the well, the market's cross and the stores stand on the square by design - a market place widens round them)
                                    double d = SegRect(s.SquareX, s.SquareY, s.SquareX, s.SquareY, ox, oy, ux, uy, fx, fy, hx, hz);
                                    if (sqR - d > most) { most = sqR - d; what = s.MarketR > 0 ? "market place" : "square"; }
                                    break;
                                }
                                case 4:
                                {
                                    if (!(s.Wall == 1 || s.Wall == 2) && s.OldR <= 0f) break;
                                    double wr = s.Wall == 1 || s.Wall == 2 ? s.WallR : s.OldR;
                                    // the rectangle's nearest and farthest points from the square against the ring (0.4 m of posts)
                                    double dn = SegRect(s.SquareX, s.SquareY, s.SquareX, s.SquareY, ox, oy, ux, uy, fx, fy, hx, hz);
                                    double dc = Dist(ox - s.SquareX, oy - s.SquareY), df = dc + Math.Sqrt(hx * hx + hz * hz);
                                    if (dn < wr + 0.2 && df > wr - 0.2) { double dd = Math.Min(wr + 0.2 - dn, df - (wr - 0.2)); if (dd > most) { most = dd; what = "ring at " + wr.ToString("0") + " m"; } }
                                    break;
                                }
                                case 5:
                                {
                                    // another building drawn at its own far size
                                    for (int j = 0; j < bl.Count; j++)
                                    {
                                        if (j == i) continue;
                                        ref var o = ref w.Buildings.Get(bl[j]);
                                        if (Homes.Fallen(in o, w.Now) || Boats.IsBoat(o.Kind) || Homes.RuinLeft(in o, w.Now) < 1.0) continue;
                                        double dx = o.X - b.X, dy = o.Y - b.Y;
                                        if (dx * dx + dy * dy > 60 * 60) continue;
                                        double eo = e <= 1.0001 ? 1.0 : Math.Min(exMax, Footprints.Room(w, sid, bl[j], 2.5));
                                        bool oh = Structures.IsHome(o.Kind);
                                        int op = oh ? Footprints.PlanOf(in o) : -1;
                                        double of = Footprints.FaceOf(in s, in o);
                                        // (exact shapes: rectangles for homes, discs for works; 10 cm each way - touching at the room's scale is no overlap)
                                        bool hit = false;
                                        if (!home && !oh) hit = Math.Sqrt(dx * dx + dy * dy) < (Footprints.WorkR(b.Kind) * e + Footprints.WorkR(o.Kind) * eo) - 0.2;
                                        else if (!home) for (int q = 0; q < Footprints.Count(op) && !hit; q++)
                                        {
                                            Footprints.Rect(op, q, out double qx, out double qz, out double qhx, out double qhz);
                                            hit = Footprints.BoxDisc(o.X, o.Y, of, qx * eo, qz * eo, qhx * eo, qhz * eo, b.X, b.Y, Footprints.WorkR(b.Kind) * e, -0.2);
                                        }
                                        else if (!oh) hit = Footprints.BoxDisc(ox, oy, face, 0, 0, hx, hz, o.X, o.Y, Footprints.WorkR(o.Kind) * eo, -0.2);
                                        else for (int q = 0; q < Footprints.Count(op) && !hit; q++)
                                        {
                                            Footprints.Rect(op, q, out double qx, out double qz, out double qhx, out double qhz);
                                            hit = Footprints.Boxes(ox, oy, face, 0, 0, hx, hz, o.X, o.Y, of, qx * eo, qz * eo, qhx * eo, qhz * eo, -0.2);
                                        }
                                        if (hit && 1.0 > most) { most = 1.0; what = (oh ? "home" : "work " + o.Kind) + " b" + bl[j]; }
                                    }
                                    break;
                                }
                            }
                        }
                        return most;
                    }
                }
            }
            // the households' pens as LivestockView lays them (true size): the yard pen behind the home where it has free ground (Footprints.YardFree:
            // clear by construction), else a ring behind the home, away from the square, sized to the herd - what each ring reaches: a street's or
            // lane's drawn band, the square's earth, a building's ground (its own home's excepted)
            int pens = 0, yards = 0, unpenned = 0; var penIn = new int[4]; string[] penNames = { "street/lane", "square", "home", "work" };
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (!s.Inhabited || (only != 0 && sid != only)) continue;
                var hl = w.HouseholdsOf(sid);
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    var h = w.Households.Get(hl[i]);
                    int beasts = w.BeastsOf(hl[i]).Count;
                    if (beasts == 0 || h.Party != 0) continue;
                    pens++;
                    if (h.Building != 0 && w.Buildings.Exists(h.Building))
                    {
                        var hb = w.Buildings.Get(h.Building);
                        int hp = Footprints.PlanOf(in hb);
                        Footprints.Rect(hp, 0, out _, out _, out _, out double hhz);
                        double W = 2.0 * (hhz - Footprints.Eaves);   // (the kit's width, as WorldObjectsView's PlanW)
                        if (Structures.IsHome(hb.Kind) && Footprints.YardFree(w, sid, h.Building, 0.0, -W / 2.0 - 3.2 - 2.1, 4.4, 2.3)) { yards++; continue; }
                    }
                    double ax = h.HomeX - s.SquareX, ay = h.HomeY - s.SquareY, al = Math.Sqrt(ax * ax + ay * ay);
                    if (al < 1e-3) { ax = 0; ay = -1; al = 1; }
                    ax /= al; ay /= al;
                    double penR = 2.6 + 0.55 * Math.Sqrt(beasts);
                    double px = h.HomeX + ax * (6.0 + penR), py = h.HomeY + ay * (6.0 + penR);
                    if (!Footprints.DiscFree(w, sid, h.Building, px, py, penR)) { unpenned++; continue; }   // (no ring drawn where its ground is not free)
                    bool line = false, sq = false, home = false, work = false;
                    for (int li = 0; li < Streets.Lines(in s) && !line; li++)
                    {
                        if (Streets.Shut(in s, li)) continue;
                        Streets.Line(in s, li, out double ox, out double oy, out double lx, out double ly, out double from);
                        double rx = px - ox, ry = py - oy, along = rx * lx + ry * ly, off = Math.Abs(-rx * ly + ry * lx);
                        if (along + penR >= (Streets.IsLane(li) ? Streets.LaneStart - 10.0 : 0.0) && off < penR + Footprints.DrawnLine) line = true;
                    }
                    sq = Dist(px - s.SquareX, py - s.SquareY) < penR + Footprints.SquareEarth(in s);
                    for (int j = 0; j < bl.Count; j++)
                    {
                        var b = w.Buildings.Get(bl[j]);
                        if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind) || bl[j] == h.Building) continue;
                        double f = Footprints.FaceOf(in s, in b);
                        if (Structures.IsHome(b.Kind))
                        {
                            int bp = Footprints.PlanOf(in b);
                            for (int r = 0; r < Footprints.Count(bp); r++)
                            {
                                Footprints.Rect(bp, r, out double cx, out double cz, out double hx, out double hz);
                                if (Footprints.BoxDisc(b.X, b.Y, f, cx, cz, hx, hz, px, py, penR, 0.0)) home = true;
                            }
                        }
                        else if (Dist(b.X - px, b.Y - py) < penR + Footprints.WorkR(b.Kind)) work = true;
                    }
                    if (line) penIn[0]++; if (sq) penIn[1]++; if (home) penIn[2]++; if (work) penIn[3]++;
                }
            }
            Console.WriteLine("roads audit at " + SimTime.FormatShort(w.Now) + ": " + villages + " villages, " + homes + " homes, " + works + " works standing; drawn far off at up to x" + exMax.ToString("0.0"));
            Console.WriteLine("  the simulation's own ground (Footprints.Open): homes in a street or lane " + simN[0] + ", on the green " + simN[1] + ", astride a ring " + simN[2] + ", by a river " + simN[3]);
            string Row(int[] a) { var sb = new System.Text.StringBuilder(); for (int f = 0; f < nf; f++) sb.Append(" " + names[f] + " " + a[f]); return sb.ToString(); }
            Console.WriteLine("  homes drawn into (over 0.5 m) at true size:" + Row(at1));
            Console.WriteLine("  homes drawn into at the far size:          " + Row(atE));
            Console.WriteLine("  works drawn into at true size:             " + Row(wk1));
            Console.WriteLine("  works drawn into at the far size:          " + Row(wkE));
            Console.WriteLine("  livestock pens (LivestockView, true size) " + pens + ": in the yard behind the home " + yards + ", no free ground (no fence drawn) " + unpenned + "; rings reaching a street or lane " + penIn[0] + ", the square " + penIn[1] + ", another home " + penIn[2] + ", a work " + penIn[3]);
            if (list > 0)
            {
                deep.Sort((x, y) => y.depth.CompareTo(x.depth));
                for (int i = 0; i < Math.Min(list, deep.Count); i++) Console.WriteLine("    " + deep[i].what);
            }
        }

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

        /// <summary>Whether a point lies on a village's square (its bare earth less a metre: TerrainLod.OnSquare).</summary>
        static bool OnASquare(World w, Vec3d d)
        {
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                Vec3d c = s.LocalToSphere(s.SquareX, s.SquareY, w.Planet.Radius);
                if (Vec3d.ArcDistance(c, d, w.Planet.Radius) < Footprints.SquareEarth(in s) - 1.0) return true;
            }
            return false;
        }
        /// <summary>A point as the shots' "lat,lon" view (degrees), 60 m up.</summary>
        static string LatLon(Vec3d d) => (d.Latitude * 180.0 / Math.PI).ToString("0.00000") + "," + (d.Longitude * 180.0 / Math.PI).ToString("0.00000") + ",60";
        static double Dist(double x, double y) => Math.Sqrt(x * x + y * y);

        /// <summary>The distance from a segment (ax, ay)-(bx, by) to a rectangle centred at (ox, oy) with axes u (half hx) and f (half hz); 0 where they meet.</summary>
        static double SegRect(double ax, double ay, double bx, double by, double ox, double oy, double ux, double uy, double fx, double fy, double hx, double hz)
        {
            double a1 = (ax - ox) * ux + (ay - oy) * uy, a2 = (ax - ox) * fx + (ay - oy) * fy;
            double b1 = (bx - ox) * ux + (by - oy) * uy, b2 = (bx - ox) * fx + (by - oy) * fy;
            // where the segment crosses the box: 0
            double t0 = 0, t1 = 1, dx = b1 - a1, dz = b2 - a2;
            if (Slab(-dx, a1 + hx, ref t0, ref t1) && Slab(dx, hx - a1, ref t0, ref t1) && Slab(-dz, a2 + hz, ref t0, ref t1) && Slab(dz, hz - a2, ref t0, ref t1)) return 0.0;
            double best = Math.Min(PtBox(a1, a2, hx, hz), PtBox(b1, b2, hx, hz));
            for (int k = 0; k < 4; k++)
            {
                double px = (k & 1) == 0 ? -hx : hx, pz = (k & 2) == 0 ? -hz : hz;
                double vx = b1 - a1, vz = b2 - a2, l2 = vx * vx + vz * vz;
                double t = l2 > 1e-12 ? Math.Max(0, Math.Min(1, ((px - a1) * vx + (pz - a2) * vz) / l2)) : 0.0;
                double ex = a1 + vx * t - px, ez = a2 + vz * t - pz;
                best = Math.Min(best, Math.Sqrt(ex * ex + ez * ez));
            }
            return best;
        }

        static double PtBox(double px, double pz, double hx, double hz) => Dist(Math.Max(0.0, Math.Abs(px) - hx), Math.Max(0.0, Math.Abs(pz) - hz));

        static bool Slab(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-12) return q >= 0.0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
    }
}
