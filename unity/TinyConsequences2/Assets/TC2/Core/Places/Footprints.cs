using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The ground a building covers (C13.3, D2-118): every home is the rectangles of its plan as the kit builds it, walls and eaves, turned to the
    /// way it faces (`Building.Face`, a bearing in its village's local frame: the direction its front looks); a work is a disc that holds all of
    /// it as drawn. A lot is free, a plan fits and a work can be laid out only where its ground meets no other building's, no camp's, no street
    /// or lane, no ring and not the green or the market place - so no house ever stands in another (D2-116's 6 m between lots let a longhouse's
    /// 12 m run into its neighbours). A home's frame: x along its front (the model's length), z toward its front; local = centre + x u + z f with
    /// f = (cos Face, sin Face) and u = (-sin Face, cos Face).
    /// </summary>
    public static class Footprints
    {
        /// <summary>m of roof beyond the walls (the kit's overhang, 0.38) and a hair more.</summary>
        public const double Eaves = 0.4;
        /// <summary>m between two buildings' ground at the least (an eaves-drip between neighbours).</summary>
        public const double Gap = 0.3;
        /// <summary>m round a camp kept for the home its household will raise there (a tent, a hut to come).</summary>
        public const double CampR = 3.0;
        /// <summary>m: the green's radius - open ground round the square that no building takes (the square's beaten earth and the green round
        /// it); a market place widens it (`Settlement.MarketR`).</summary>
        public const double GreenR = 16.0;
        /// <summary>A plan's index for a hut (round: a square that holds it).</summary>
        public const int HutPlan = 7;
        /// <summary>The side of a hall-house its cross wing stands on, along the house's x: the kit builds it at +x, and the model's x comes into the
        /// game mirrored (the FBX import's handedness: the hall's bounds centre at x -2.45 in the game, D2-118), so -x.</summary>
        public const double WingSide = -1.0;

        /// <summary>How many rectangles a plan covers (a hall-house two: its hall and its cross wing).</summary>
        public static int Count(int plan) => plan == Architecture.Hall ? 2 : 1;

        /// <summary>
        /// Rectangle i of a plan in the home's frame: its centre (cx along the front, cz toward it) and half sizes (hx, hz), walls and eaves.
        /// The kit's plans (L x W): one-room 4.8 x 3.8, cottage 7.0 x 5.0, longhouse 12.0 x 5.4, two-storey 6.6 x 5.4 with its upper storey
        /// jettied 0.5 m out to the front, hall 10.0 x 6.2 with its porch and a cross wing of 5.2 x 8.6 at one end, the narrow town house 4.6 x 7.0
        /// gable to the street, 4.6 x 6.2 jettied 0.4 m; a hut 2.3 m round (the ground kept for a lot holds it and a one-room cottage).
        /// </summary>
        public static void Rect(int plan, int i, out double cx, out double cz, out double hx, out double hz)
        {
            cx = 0; cz = 0;
            switch (plan)
            {
                case Architecture.Small: hx = 2.4 + Eaves; hz = 1.9 + Eaves; return;
                case Architecture.Cottage: hx = 3.5 + Eaves; hz = 2.5 + Eaves; return;
                case Architecture.Long: hx = 6.0 + Eaves; hz = 2.7 + Eaves; return;
                case Architecture.Two: hx = 3.4 + Eaves; hz = 2.95 + Eaves; cz = 0.25; return;
                case Architecture.Hall:
                    if (i == 0) { hx = 5.0 + Eaves; cz = 0.8; hz = 4.3; return; }   // (the hall, its porch in front: -3.5 .. 5.1)
                    cx = WingSide * 7.3; hx = 2.6 + Eaves; hz = 4.3 + Eaves; return;   // (the cross wing, two storeys, across the hall's end)
                case Architecture.Row: hx = 2.3 + Eaves; cz = 0.2; hz = 3.1 + 0.2 + Eaves; return;   // (-3.5 .. 3.9: its upper storeys jettied 0.4 m out)
                default: hx = 2.8; hz = 2.8; return;   // (a hut, 2.3 m round under its thatch - and the ground a lot keeps for the home to come: a one-room cottage turned any way)
            }
        }

        /// <summary>The radius of a disc round a building's centre that holds all of a plan's ground (a table: read in every check).</summary>
        public static double Reach(int plan) => _reach[plan & 7];
        static readonly double[] _reach = Reaches();
        static double[] Reaches() { var t = new double[8]; for (int p = 0; p < 8; p++) t[p] = ReachOf(p); return t; }

        static double ReachOf(int plan)
        {
            double r = 0;
            for (int i = 0; i < Count(plan); i++)
            {
                Rect(plan, i, out double cx, out double cz, out double hx, out double hz);
                double d = DMath.Sqrt((DMath.Abs(cx) + hx) * (DMath.Abs(cx) + hx) + (DMath.Abs(cz) + hz) * (DMath.Abs(cz) + hz));
                if (d > r) r = d;
            }
            return r;
        }

        /// <summary>
        /// A work's ground as a disc (m): all of it as drawn - a barn's 15 x 8.4 under its eaves, a smithy drawn a fifth larger than life, a market's
        /// cross (its stalls stand in the market place); boats and works at their sites by the table's footprint.
        /// </summary>
        public static double WorkR(byte kind)
        {
            switch (kind)
            {
                case Structures.Well: return 1.6;
                case Structures.Granary: return 2.8;
                case Structures.Barn: return 9.4;
                case Structures.Smithy: return 6.0;
                case Structures.Market: return 2.6;
                default: return Structures.Of(kind).Footprint;
            }
        }

        /// <summary>A home's plan for its ground: its form's plan, a hut's own (huts are round, whatever form they carry).</summary>
        public static int PlanOf(in Building b) => b.Kind < Construction.House ? HutPlan : Architecture.Has(b.Form) ? Architecture.PlanOf(b.Form) : Architecture.Cottage;

        /// <summary>Whether two rectangles overlap (with `gap` between them at the least): a separating-axis test.</summary>
        public static bool Boxes(double ax, double ay, double af, double acx, double acz, double ahx, double ahz,
                                 double bx, double by, double bf, double bcx, double bcz, double bhx, double bhz, double gap)
        {
            double afx = DMath.Cos(af), afy = DMath.Sin(af), aux = -afy, auy = afx;
            double bfx = DMath.Cos(bf), bfy = DMath.Sin(bf), bux = -bfy, buy = bfx;
            double pax = ax + aux * acx + afx * acz, pay = ay + auy * acx + afy * acz;
            double pbx = bx + bux * bcx + bfx * bcz, pby = by + buy * bcx + bfy * bcz;
            double dx = pbx - pax, dy = pby - pay;
            ahx += gap * 0.5; ahz += gap * 0.5; bhx += gap * 0.5; bhz += gap * 0.5;
            // the four axes: each rectangle's two
            if (Apart(dx, dy, aux, auy, ahx, ahz, aux, auy, afx, afy, bhx, bhz, bux, buy, bfx, bfy)) return false;
            if (Apart(dx, dy, afx, afy, ahx, ahz, aux, auy, afx, afy, bhx, bhz, bux, buy, bfx, bfy)) return false;
            if (Apart(dx, dy, bux, buy, ahx, ahz, aux, auy, afx, afy, bhx, bhz, bux, buy, bfx, bfy)) return false;
            if (Apart(dx, dy, bfx, bfy, ahx, ahz, aux, auy, afx, afy, bhx, bhz, bux, buy, bfx, bfy)) return false;
            return true;
        }

        static bool Apart(double dx, double dy, double nx, double ny, double ahx, double ahz, double aux, double auy, double afx, double afy,
                          double bhx, double bhz, double bux, double buy, double bfx, double bfy)
        {
            double ra = ahx * DMath.Abs(aux * nx + auy * ny) + ahz * DMath.Abs(afx * nx + afy * ny);
            double rb = bhx * DMath.Abs(bux * nx + buy * ny) + bhz * DMath.Abs(bfx * nx + bfy * ny);
            return DMath.Abs(dx * nx + dy * ny) > ra + rb;
        }

        /// <summary>Whether a rectangle meets a disc (centre px, py, radius r, with `gap` between at the least).</summary>
        public static bool BoxDisc(double ax, double ay, double af, double acx, double acz, double ahx, double ahz, double px, double py, double r, double gap)
        {
            double fx = DMath.Cos(af), fy = DMath.Sin(af), ux = -fy, uy = fx;
            double qx = px - (ax + ux * acx + fx * acz), qy = py - (ay + uy * acx + fy * acz);
            double lx = qx * ux + qy * uy, lz = qx * fx + qy * fy;   // (the disc's centre in the rectangle's frame)
            double nx = DMath.Clamp(lx, -ahx, ahx), nz = DMath.Clamp(lz, -ahz, ahz);
            double ex = lx - nx, ez = lz - nz, rr = r + gap;
            return ex * ex + ez * ez < rr * rr;
        }

        /// <summary>The bearing a home at (x, y) faces when laid out now (C13.1-C13.3): square to the street or lane whose frontage it is on, a
        /// couple of degrees off by its builders' hand; else toward the square, up to ten degrees off. `style` is the household that raises it.</summary>
        public static double FaceAt(in Settlement s, double x, double y, int style)
        {
            bool fronts = Streets.Facing(in s, x, y, out double tx, out double ty);
            double a = DMath.Atan2(ty - y, tx - x);
            int k = (style * 37) % (fronts ? 5 : 21);
            if (k < 0) k += fronts ? 5 : 21;
            double off = (k - (fronts ? 2 : 10)) * (DMath.PI / 180.0);
            return a + off;
        }

        /// <summary>The bearing a standing building faces: its own when laid out with one (D2-118), else as it would be laid out where it stands
        /// (a home toward its street or the square, a work toward the square, as the game drew them before).</summary>
        public static double FaceOf(in Settlement s, in Building b)
        {
            if (b.Faced != 0) return b.Face;
            if (Structures.IsHome(b.Kind)) return FaceAt(in s, b.X, b.Y, b.Style);
            double a = DMath.Atan2(s.SquareY - (double)b.Y, s.SquareX - (double)b.X);
            int k = (b.Id * 37) % 21; if (k < 0) k += 21;
            return a + (k - 10) * (DMath.PI / 180.0);
        }

        /// <summary>
        /// Whether a home of `plan` at (x, y) facing `face` would stand clear (C13.3): of every other building of the village standing or in ruins
        /// (`exceptBid` the building it replaces), every other household's camp (`exceptHid` its own), the streets and lanes where they are lined,
        /// the green and the market place, and the palisade's ring.
        /// </summary>
        public static bool HomeFits(World w, int sid, double x, double y, double face, int plan, int exceptBid, int exceptHid)
        {
            ref var s = ref w.Settlements.Get(sid);
            double reach = Reach(plan);
            int n = Count(plan);
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == exceptBid) continue;
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in b, w.Now) <= 0) continue;   // (gone into the land)
                double dx = (double)b.X - x, dy = (double)b.Y - y;
                if (Structures.IsHome(b.Kind))
                {
                    int bp = PlanOf(in b);
                    double rr = reach + Reach(bp) + Gap;
                    if (dx * dx + dy * dy >= rr * rr) continue;
                    double bf = FaceOf(in s, in b);
                    for (int r = 0; r < n; r++)
                    {
                        Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                        for (int q = 0; q < Count(bp); q++)
                        {
                            Rect(bp, q, out double qx, out double qz, out double qhx, out double qhz);
                            if (Boxes(x, y, face, cx, cz, hx, hz, b.X, b.Y, bf, qx, qz, qhx, qhz, Gap)) return false;
                        }
                    }
                }
                else
                {
                    double wr = WorkR(b.Kind);
                    double rr = reach + wr + Gap;
                    if (dx * dx + dy * dy >= rr * rr) continue;
                    for (int r = 0; r < n; r++)
                    {
                        Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                        if (BoxDisc(x, y, face, cx, cz, hx, hz, b.X, b.Y, wr, Gap)) return false;
                    }
                }
            }
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                if (hl[i] == exceptHid) continue;
                ref var h = ref w.Households.Get(hl[i]);
                if (h.Building != 0) continue;   // (its home is among the buildings)
                // a camp keeps its lot's ground for the home to come (a hut and a one-room cottage; a narrow house on a divided plot)
                int cp = LotPlan(in s, h.HomeX, h.HomeY);
                double dx = (double)h.HomeX - x, dy = (double)h.HomeY - y, rr = reach + Reach(cp) + Gap;
                if (dx * dx + dy * dy >= rr * rr) continue;
                double cf = FaceAt(in s, h.HomeX, h.HomeY, hl[i]);
                Rect(cp, 0, out double qx, out double qz, out double qhx, out double qhz);
                for (int r = 0; r < n; r++)
                {
                    Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                    if (Boxes(x, y, face, cx, cz, hx, hz, h.HomeX, h.HomeY, cf, qx, qz, qhx, qhz, Gap)) return false;
                }
            }
            return Open(in s, x, y, face, plan) && !Wet(w, in s, x, y, face, plan);
        }

        /// <summary>m a building's ground keeps from a river's bank (D2-118).</summary>
        public const double RiverMargin = 1.5;
        static readonly List<int> _near = new List<int>();

        /// <summary>
        /// Whether a home's ground at (x, y) facing `face` reaches a river - within its half width and `RiverMargin` of its line (`RiverLines`, the
        /// line the view draws) - D2-118: before, only a lot's middle was kept on dry ground and a house could stand across a river's ribbon.
        /// </summary>
        public static bool Wet(World w, in Settlement s, double x, double y, double face, int plan)
        {
            var P = w.Planet;
            if (w.Drainage == null) return false;
            RiverLines.Ensure(P, w.Drainage, w.Seed);
            if (RiverLines.All.Count == 0) return false;
            RiverLines.Near(P, P.Grid.CellOf(s.LocalToSphere(x, y, P.Radius)), _near);
            if (_near.Count == 0) return false;
            double fx = DMath.Cos(face), fy = DMath.Sin(face), ux = -fy, uy = fx;
            for (int i = 0; i < _near.Count; i++)
            {
                var pc = RiverLines.All[_near[i]];
                if (!RiverLines.Stream(P, in pc)) continue;   // (a reach dry this season: D2-119)
                double half = RiverLines.HalfWidth(P.RiverFlow[pc.Cell]) + RiverMargin;
                s.SphereToLocal(pc.A, P.Radius, out double ax, out double ay);
                s.SphereToLocal(pc.B, P.Radius, out double bx, out double by);
                for (int r = 0; r < Count(plan); r++)
                {
                    Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                    double ox = x + ux * cx + fx * cz, oy = y + uy * cx + fy * cz;
                    // the piece in the rectangle's frame
                    double a1 = (ax - ox) * ux + (ay - oy) * uy, a2 = (ax - ox) * fx + (ay - oy) * fy;
                    double b1 = (bx - ox) * ux + (by - oy) * uy, b2 = (bx - ox) * fx + (by - oy) * fy;
                    if (SegBox(a1, a2, b1, b2, hx, hz) < half) return true;
                }
            }
            return false;
        }

        /// <summary>Whether a work's disc reaches a river (its radius and `RiverMargin` off the bank).</summary>
        public static bool WetDisc(World w, in Settlement s, double x, double y, double radius)
        {
            var P = w.Planet;
            if (w.Drainage == null) return false;
            RiverLines.Ensure(P, w.Drainage, w.Seed);
            if (RiverLines.All.Count == 0) return false;
            RiverLines.Near(P, P.Grid.CellOf(s.LocalToSphere(x, y, P.Radius)), _near);
            for (int i = 0; i < _near.Count; i++)
            {
                var pc = RiverLines.All[_near[i]];
                if (!RiverLines.Stream(P, in pc)) continue;   // (a reach dry this season: D2-119)
                s.SphereToLocal(pc.A, P.Radius, out double ax, out double ay);
                s.SphereToLocal(pc.B, P.Radius, out double bx, out double by);
                if (SegBox(ax - x, ay - y, bx - x, by - y, 0.0, 0.0) < radius + RiverMargin + RiverLines.HalfWidth(P.RiverFlow[pc.Cell])) return true;
            }
            return false;
        }

        /// <summary>The distance between a segment (a1,a2)-(b1,b2) and the box [-hx,hx] x [-hz,hz] (0 where they meet).</summary>
        static double SegBox(double a1, double a2, double b1, double b2, double hx, double hz)
        {
            // where the segment crosses the box: 0
            if (SegHitsBox(a1, a2, b1, b2, hx, hz)) return 0.0;
            double best = DMath.Min(PointBox(a1, a2, hx, hz), PointBox(b1, b2, hx, hz));
            for (int k = 0; k < 4; k++)
            {
                double px = (k & 1) == 0 ? -hx : hx, pz = (k & 2) == 0 ? -hz : hz;
                double d = PointSeg(px, pz, a1, a2, b1, b2);
                if (d < best) best = d;
            }
            return best;
        }

        static double PointBox(double px, double pz, double hx, double hz)
        {
            double dx = DMath.Max(0.0, DMath.Abs(px) - hx), dz = DMath.Max(0.0, DMath.Abs(pz) - hz);
            return DMath.Sqrt(dx * dx + dz * dz);
        }

        static double PointSeg(double px, double pz, double a1, double a2, double b1, double b2)
        {
            double vx = b1 - a1, vz = b2 - a2, l2 = vx * vx + vz * vz;
            double t = l2 > 1e-12 ? DMath.Clamp(((px - a1) * vx + (pz - a2) * vz) / l2, 0.0, 1.0) : 0.0;
            double dx = a1 + vx * t - px, dz = a2 + vz * t - pz;
            return DMath.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Whether a segment meets an axis-aligned box (the slab test).</summary>
        static bool SegHitsBox(double a1, double a2, double b1, double b2, double hx, double hz)
        {
            double t0 = 0.0, t1 = 1.0, dx = b1 - a1, dz = b2 - a2;
            if (!Slab(-dx, a1 + hx, ref t0, ref t1) || !Slab(dx, hx - a1, ref t0, ref t1)) return false;
            if (!Slab(-dz, a2 + hz, ref t0, ref t1) || !Slab(dz, hz - a2, ref t0, ref t1)) return false;
            return true;
        }

        static bool Slab(double p, double q, ref double t0, ref double t1)
        {
            if (DMath.Abs(p) < 1e-12) return q >= 0.0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        /// <summary>The ground a lot keeps for its home before it stands: a narrow house's on a divided plot of a town's core, else a hut's.</summary>
        public static int LotPlan(in Settlement s, double x, double y) => Streets.CorePlot(in s, x, y) ? Architecture.Row : HutPlan;

        /// <summary>Whether a plan keeps within a divided plot's width (C13.3: half a lot along the street, less the eaves-drip between them).</summary>
        public static bool Narrow(int plan)
        {
            Rect(plan, 0, out double cx, out _, out double hx, out _);
            return Count(plan) == 1 && DMath.Abs(cx) + hx <= 0.25 * Streets.Spacing - 0.5 * Gap;
        }

        /// <summary>Whether a home's ground keeps out of the streets and lanes, the green and the market place, and the ring.</summary>
        public static bool Open(in Settlement s, double x, double y, double face, int plan)
        {
            double green = Green(in s);
            for (int r = 0; r < Count(plan); r++)
            {
                Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                if (BoxDisc(x, y, face, cx, cz, hx, hz, s.SquareX, s.SquareY, green, 0.0)) return false;
                if (InLine(in s, x, y, face, cx, cz, hx, hz)) return false;
            }
            double reach = Reach(plan);
            double qx = x - s.SquareX, qy = y - s.SquareY, d = DMath.Sqrt(qx * qx + qy * qy);
            if (s.OldR > 0f && DMath.Abs(d - (double)s.OldR) < reach + 1.5) return false;   // (never astride a ring, old or standing: C1.4, C7.1)
            if ((s.Wall == 1 || s.Wall == 2) && DMath.Abs(d - (double)s.WallR) < reach + 1.5) return false;
            return true;
        }

        /// <summary>The green's radius now: the green, or its market place where traders have widened it (D2-118).</summary>
        public static double Green(in Settlement s) => DMath.Max(GreenR, (double)s.MarketR);

        /// <summary>
        /// How many standing buildings a line would run through (C13.3: a street being laid, or one of its lanes) - from `from` along it outward,
        /// within `Streets.Width` of it: homes by their rectangles, works by their discs.
        /// </summary>
        public static int LineCuts(World w, int sid, double ox, double oy, double lx, double ly, double from)
        {
            ref var s = ref w.Settlements.Get(sid);
            int cuts = 0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in b, w.Now) <= 0 || Boats.IsBoat(b.Kind)) continue;
                double rx = (double)b.X - ox, ry = (double)b.Y - oy;
                double along = rx * lx + ry * ly, off = DMath.Abs(-rx * ly + ry * lx);
                if (Structures.IsHome(b.Kind))
                {
                    int plan = PlanOf(in b);
                    double reach = Reach(plan);
                    if (off >= reach + Streets.Width || along + reach < from) continue;
                    double f = FaceOf(in s, in b), fx = DMath.Cos(f), fy = DMath.Sin(f), ux = -fy, uy = fx, nx = -ly, ny = lx;
                    for (int r = 0; r < Count(plan); r++)
                    {
                        Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                        double px = b.X + ux * cx + fx * cz - ox, py = b.Y + uy * cx + fy * cz - oy;
                        double al = px * lx + py * ly, of = DMath.Abs(-px * ly + py * lx);
                        double rn = hx * DMath.Abs(ux * nx + uy * ny) + hz * DMath.Abs(fx * nx + fy * ny);
                        double ra = hx * DMath.Abs(ux * lx + uy * ly) + hz * DMath.Abs(fx * lx + fy * ly);
                        if (al + ra >= from && of - rn < Streets.Width) { cuts++; break; }
                    }
                }
                else
                {
                    double wr = WorkR(b.Kind);
                    if (along + wr >= from && off - wr < Streets.Width) cuts++;
                }
            }
            // and the lots camps keep for the homes to come
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var h = ref w.Households.Get(hl[i]);
                if (h.Building != 0) continue;
                double rx = (double)h.HomeX - ox, ry = (double)h.HomeY - oy;
                double along = rx * lx + ry * ly, off = DMath.Abs(-rx * ly + ry * lx), reach = Reach(LotPlan(in s, h.HomeX, h.HomeY));
                if (along + reach >= from && off - reach < Streets.Width) cuts++;
            }
            return cuts;
        }

        /// <summary>Whether a rectangle reaches into a street or lane where it is lined (within `Streets.Width` of its line).</summary>
        static bool InLine(in Settlement s, double x, double y, double face, double cx, double cz, double hx, double hz)
        {
            double fx = DMath.Cos(face), fy = DMath.Sin(face), ux = -fy, uy = fx;
            double px = x + ux * cx + fx * cz, py = y + uy * cx + fy * cz;
            int lines = Streets.Lines(in s);
            for (int i = 0; i < lines; i++)
            {
                Streets.Line(in s, i, out double ox, out double oy, out double lx, out double ly, out double from);
                double rx = px - ox, ry = py - oy;
                double along = rx * lx + ry * ly, off = -rx * ly + ry * lx;
                // the rectangle's half extent along the line's normal and along the line
                double nx = -ly, ny = lx;
                double rn = hx * DMath.Abs(ux * nx + uy * ny) + hz * DMath.Abs(fx * nx + fy * ny);
                double ra = hx * DMath.Abs(ux * lx + uy * ly) + hz * DMath.Abs(fx * lx + fy * ly);
                if (along + ra < from) continue;   // (before the line is lined: the green's own ground)
                if (DMath.Abs(off) - rn < Streets.Width) return true;
            }
            return false;
        }

        /// <summary>
        /// The tally of a village's ground (headless and tests, reads only): pairs of standing buildings (huts, houses and works not yet gone into
        /// the land) whose ground overlaps - home with home, home with work, work with work - and homes standing in a street or lane, the green
        /// or the market place, or astride the ring (ruins and boats left out of the last).
        /// </summary>
        public static void Audit(World w, int sid, out int homeHome, out int homeWork, out int workWork, out int inOpen)
        {
            homeHome = homeWork = workWork = inOpen = 0;
            ref var s = ref w.Settlements.Get(sid);
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var a = ref w.Buildings.Get(bl[i]);
                if (Homes.Fallen(in a, w.Now) || Boats.IsBoat(a.Kind)) continue;
                bool ah = Structures.IsHome(a.Kind);
                int ap = ah ? PlanOf(in a) : 0;
                double af = FaceOf(in s, in a), ar = ah ? Reach(ap) : WorkR(a.Kind);
                if (ah && (!Open(in s, a.X, a.Y, af, ap) || Wet(w, in s, a.X, a.Y, af, ap))) inOpen++;
                for (int j = i + 1; j < bl.Count; j++)
                {
                    ref var b = ref w.Buildings.Get(bl[j]);
                    if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind)) continue;
                    bool bh = Structures.IsHome(b.Kind);
                    int bp = bh ? PlanOf(in b) : 0;
                    double br = bh ? Reach(bp) : WorkR(b.Kind);
                    double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y;
                    if (dx * dx + dy * dy >= (ar + br) * (ar + br)) continue;
                    double bf = FaceOf(in s, in b);
                    bool hit = false;
                    if (ah && bh)
                    {
                        for (int r = 0; r < Count(ap) && !hit; r++)
                        {
                            Rect(ap, r, out double cx, out double cz, out double hx, out double hz);
                            for (int q = 0; q < Count(bp) && !hit; q++)
                            {
                                Rect(bp, q, out double qx, out double qz, out double qhx, out double qhz);
                                hit = Boxes(a.X, a.Y, af, cx, cz, hx, hz, b.X, b.Y, bf, qx, qz, qhx, qhz, 0.0);
                            }
                        }
                        if (hit) homeHome++;
                    }
                    else if (ah || bh)
                    {
                        ref var h = ref ah ? ref a : ref b;
                        ref var k = ref ah ? ref b : ref a;
                        int hp = ah ? ap : bp; double hf = ah ? af : bf;
                        for (int r = 0; r < Count(hp) && !hit; r++)
                        {
                            Rect(hp, r, out double cx, out double cz, out double hx, out double hz);
                            hit = BoxDisc(h.X, h.Y, hf, cx, cz, hx, hz, k.X, k.Y, WorkR(k.Kind), 0.0);
                        }
                        if (hit) homeWork++;
                    }
                    else if (dx * dx + dy * dy < (ar + br) * (ar + br)) workWork++;
                }
            }
        }

        /// <summary>
        /// How much larger than life a standing building may be drawn (scaled about its centre, as its neighbours are) before its ground meets
        /// another's (the view's exaggeration at a distance, D2-118: never pushes a house into its neighbour) - at most `cap`, at least 1 - and
        /// before it reaches the open ground the view draws (session 17, the visual truth audit: far off, homes were drawn up to 1.6x into their
        /// streets): the trodden band of a street or lane (`DrawnLine` either side of its line, from the square - a lane from `LaneStart` - 10 m),
        /// the square's or market place's bare earth (`DrawnSquare`), the palisade's posts.
        /// </summary>
        public static double Room(World w, int sid, int bid, double cap)
        {
            ref var s = ref w.Settlements.Get(sid);
            ref var a = ref w.Buildings.Get(bid);
            bool ah = Structures.IsHome(a.Kind);
            int ap = ah ? PlanOf(in a) : 0;
            double af = FaceOf(in s, in a), ar = ah ? Reach(ap) : WorkR(a.Kind);
            double best = DMath.Min(cap, OpenScale(in s, a.X, a.Y, af, ah ? ap : -1, ar));
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == bid) continue;
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in b, w.Now) <= 0 || Boats.IsBoat(b.Kind)) continue;
                bool bh = Structures.IsHome(b.Kind);
                int bp = bh ? PlanOf(in b) : 0;
                double br = bh ? Reach(bp) : WorkR(b.Kind);
                double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y, d2 = dx * dx + dy * dy, far = best * (ar + br);
                if (d2 >= far * far) continue;
                double bf = FaceOf(in s, in b), e;
                if (ah && bh) e = PairScale(a.X, a.Y, af, ap, b.X, b.Y, bf, bp);
                else if (ah) e = HomeDiscScale(a.X, a.Y, af, ap, b.X, b.Y, WorkR(b.Kind));
                else if (bh) e = HomeDiscScale(b.X, b.Y, bf, bp, a.X, a.Y, WorkR(a.Kind));
                else e = DMath.Sqrt(d2) / (ar + br);
                if (e < best) best = e;
            }
            return DMath.Max(1.0, best);
        }

        /// <summary>m either side of a street's or lane's line that the view draws as trodden earth (its half strength: VillageGround's
        /// 1 - Edge(2.2, 3.8)); the square's bare earth (Edge(9, 15)) and a market place's (its radius less 3 m).</summary>
        public const double DrawnLine = 3.0, DrawnSquare = 12.0;

        /// <summary>The square's or market place's bare earth as drawn (m round the square).</summary>
        public static double SquareEarth(in Settlement s) => s.MarketR > 0f ? DMath.Max(DrawnSquare, s.MarketR - 3.0) : DrawnSquare;

        /// <summary>
        /// The largest scale about its centre at which a building's ground (a home's rectangles by `plan`, or a work's disc of radius `reach` when
        /// plan is -1) keeps out of the open ground the view draws: each street's and lane's trodden band where it is drawn, the square's or market
        /// place's earth, the palisade's ring of posts (the side the building stands on). 1e9 when nothing near limits it.
        /// </summary>
        public static double OpenScale(in Settlement s, double x, double y, double face, int plan, double reach)
        {
            double best = 1e9;
            double fx = DMath.Cos(face), fy = DMath.Sin(face), ux = -fy, uy = fx;
            int lines = Streets.Lines(in s);
            for (int i = 0; i < lines; i++)
            {
                if (Streets.Shut(in s, i)) continue;
                Streets.Line(in s, i, out double ox, out double oy, out double lx, out double ly, out _);
                double from = Streets.IsLane(i) ? Streets.LaneStart - 4.0 : 0.0;
                double rx = x - ox, ry = y - oy, along = rx * lx + ry * ly, off = -rx * ly + ry * lx;
                if (along + 2.5 * reach < from) continue;   // (short of where the line is drawn, even drawn larger)
                double nx = -ly, ny = lx, D = off;
                if (D < 0) { D = -D; nx = -nx; ny = -ny; }   // (the normal from the line toward the building)
                if (plan < 0) { if (reach > 1e-9) best = DMath.Min(best, (D - DrawnLine) / reach); continue; }
                for (int r = 0; r < Count(plan); r++)
                {
                    Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                    double cn = (ux * cx + fx * cz) * nx + (uy * cx + fy * cz) * ny;   // (the rectangle's centre offset toward the building's side)
                    double rn = hx * DMath.Abs(ux * nx + uy * ny) + hz * DMath.Abs(fx * nx + fy * ny);
                    if (rn - cn > 1e-9) best = DMath.Min(best, (D - DrawnLine) / (rn - cn));
                }
            }
            double dx = x - s.SquareX, dy = y - s.SquareY, d = DMath.Sqrt(dx * dx + dy * dy);
            double ext = plan < 0 ? reach : Reach(plan);
            if (ext > 1e-9)
            {
                best = DMath.Min(best, (d - SquareEarth(in s)) / ext);
                double ring = s.Wall == 1 || s.Wall == 2 ? s.WallR : 0.0;
                for (int k = 0; k < 2; k++)
                {
                    double R = k == 0 ? ring : s.OldR;
                    if (R < 5.0) continue;
                    best = DMath.Min(best, (d < R ? R - 0.6 - d : d - R - 0.6) / ext);
                }
            }
            return best;
        }

        /// <summary>The largest factor two homes scaled about their centres keep apart by: the least over their rectangles' pairs of the most over the
        /// separating axes of what each axis allows (apart along n while e (ra + rb - offsets) stays under their centres' distance along n).</summary>
        static double PairScale(double ax, double ay, double af, int ap, double bx, double by, double bf, int bp)
        {
            double afx = DMath.Cos(af), afy = DMath.Sin(af), aux = -afy, auy = afx;
            double bfx = DMath.Cos(bf), bfy = DMath.Sin(bf), bux = -bfy, buy = bfx;
            double least = double.MaxValue;
            for (int r = 0; r < Count(ap); r++)
            {
                Rect(ap, r, out double acx, out double acz, out double ahx, out double ahz);
                for (int q = 0; q < Count(bp); q++)
                {
                    Rect(bp, q, out double bcx, out double bcz, out double bhx, out double bhz);
                    double dx = bx - ax, dy = by - ay;   // (the buildings' centres stay; the rectangles' offsets grow with the scale)
                    double ox = (bux * bcx + bfx * bcz) - (aux * acx + afx * acz), oy = (buy * bcx + bfy * bcz) - (auy * acx + afy * acz);
                    double most = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        double nx = k == 0 ? aux : k == 1 ? afx : k == 2 ? bux : bfx, ny = k == 0 ? auy : k == 1 ? afy : k == 2 ? buy : bfy;
                        double p = dx * nx + dy * ny, o = ox * nx + oy * ny;
                        if (p < 0) { p = -p; o = -o; }
                        double R = ahx * DMath.Abs(aux * nx + auy * ny) + ahz * DMath.Abs(afx * nx + afy * ny) + bhx * DMath.Abs(bux * nx + buy * ny) + bhz * DMath.Abs(bfx * nx + bfy * ny);
                        double e = R - o > 1e-9 ? p / (R - o) : 1e9;
                        if (e > most) most = e;
                    }
                    if (most < least) least = most;
                }
            }
            return least;
        }

        /// <summary>The same for a home and a work's disc (the home's axes and the line between them).</summary>
        static double HomeDiscScale(double hx0, double hy0, double hf, int hp, double px, double py, double pr)
        {
            double fx = DMath.Cos(hf), fy = DMath.Sin(hf), ux = -fy, uy = fx;
            double least = double.MaxValue;
            for (int r = 0; r < Count(hp); r++)
            {
                Rect(hp, r, out double cx, out double cz, out double hx, out double hz);
                double dx = px - hx0, dy = py - hy0, ox = -(ux * cx + fx * cz), oy = -(uy * cx + fy * cz);
                double dl = DMath.Sqrt(dx * dx + dy * dy);
                double most = 0;
                for (int k = 0; k < 3; k++)
                {
                    double nx = k == 0 ? ux : k == 1 ? fx : (dl > 1e-9 ? dx / dl : 1.0), ny = k == 0 ? uy : k == 1 ? fy : (dl > 1e-9 ? dy / dl : 0.0);
                    double p = dx * nx + dy * ny, o = ox * nx + oy * ny;
                    if (p < 0) { p = -p; o = -o; }
                    double R = hx * DMath.Abs(ux * nx + uy * ny) + hz * DMath.Abs(fx * nx + fy * ny) + pr;
                    double e = R - o > 1e-9 ? p / (R - o) : 1e9;
                    if (e > most) most = e;
                }
                if (most < least) least = most;
            }
            return least;
        }

        /// <summary>
        /// Whether ground beside or behind a home - a rectangle in its frame (centre mx along its front, mz toward it; half sizes) - is free for its
        /// yard (a kitchen garden, a pen, a woodpile: the view, D2-118): no other building's ground there, no street or lane, not the green.
        /// </summary>
        public static bool YardFree(World w, int sid, int bid, double mx, double mz, double hx, double hz)
        {
            ref var s = ref w.Settlements.Get(sid);
            ref var a = ref w.Buildings.Get(bid);
            double af = FaceOf(in s, in a);
            double reach = DMath.Sqrt((DMath.Abs(mx) + hx) * (DMath.Abs(mx) + hx) + (DMath.Abs(mz) + hz) * (DMath.Abs(mz) + hz));
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == bid) continue;
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in b, w.Now) <= 0) continue;
                double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y;
                if (Structures.IsHome(b.Kind))
                {
                    int bp = PlanOf(in b);
                    double rr = reach + Reach(bp);
                    if (dx * dx + dy * dy >= rr * rr) continue;
                    double bf = FaceOf(in s, in b);
                    for (int q = 0; q < Count(bp); q++)
                    {
                        Rect(bp, q, out double qx, out double qz, out double qhx, out double qhz);
                        if (Boxes(a.X, a.Y, af, mx, mz, hx, hz, b.X, b.Y, bf, qx, qz, qhx, qhz, 0.0)) return false;
                    }
                }
                else
                {
                    double wr = WorkR(b.Kind), rr = reach + wr;
                    if (dx * dx + dy * dy >= rr * rr) continue;
                    if (BoxDisc(a.X, a.Y, af, mx, mz, hx, hz, b.X, b.Y, wr, 0.0)) return false;
                }
            }
            if (BoxDisc(a.X, a.Y, af, mx, mz, hx, hz, s.SquareX, s.SquareY, Green(in s), 0.0)) return false;
            return !InLine(in s, a.X, a.Y, af, mx, mz, hx, hz);
        }

        /// <summary>
        /// Whether a work of `kind` at (x, y) would stand clear (C13.3): of every building's ground (a home's rectangles, a work's disc), every camp,
        /// the streets and lanes, the green and its market place (unless it is a work of the green: a market, the village's first well and granary
        /// keep their spots), and the ring.
        /// </summary>
        /// <summary>
        /// Whether a disc of the village's ground (a pen of the view's, LivestockView) is free: no building's ground there (`exceptBid` its own home
        /// excepted), no street or lane as drawn (`DrawnLine` either side, where lined), not the square's earth. Reads only.
        /// </summary>
        public static bool DiscFree(World w, int sid, int exceptBid, double x, double y, double r)
        {
            ref var s = ref w.Settlements.Get(sid);
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == exceptBid) continue;
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in b, w.Now) <= 0 || Boats.IsBoat(b.Kind)) continue;
                double dx = (double)b.X - x, dy = (double)b.Y - y;
                if (Structures.IsHome(b.Kind))
                {
                    int bp = PlanOf(in b);
                    double rr = r + Reach(bp);
                    if (dx * dx + dy * dy >= rr * rr) continue;
                    double bf = FaceOf(in s, in b);
                    for (int q = 0; q < Count(bp); q++)
                    {
                        Rect(bp, q, out double qx, out double qz, out double qhx, out double qhz);
                        if (BoxDisc(b.X, b.Y, bf, qx, qz, qhx, qhz, x, y, r, 0.0)) return false;
                    }
                }
                else
                {
                    double rr = r + WorkR(b.Kind);
                    if (dx * dx + dy * dy < rr * rr) return false;
                }
            }
            double sx = (double)s.SquareX - x, sy = (double)s.SquareY - y, sr = SquareEarth(in s) + r;
            if (sx * sx + sy * sy < sr * sr) return false;
            return !Streets.On(in s, x, y, r + DrawnLine);
        }

        /// <summary>`--noworkclear`: a work keeps 1 m off a street's or lane's line (before session 17), on the same binary.</summary>
        public static bool WorkClearOff;

        public static bool WorkFits(World w, int sid, double x, double y, byte kind, double extra, bool green)
        {
            ref var s = ref w.Settlements.Get(sid);
            double wr = WorkR(kind) + extra;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in b, w.Now) <= 0) continue;
                double dx = (double)b.X - x, dy = (double)b.Y - y;
                if (Structures.IsHome(b.Kind))
                {
                    int bp = PlanOf(in b);
                    double rr = wr + Reach(bp) + Gap;
                    if (dx * dx + dy * dy >= rr * rr) continue;
                    double bf = FaceOf(in s, in b);
                    for (int q = 0; q < Count(bp); q++)
                    {
                        Rect(bp, q, out double qx, out double qz, out double qhx, out double qhz);
                        if (BoxDisc(b.X, b.Y, bf, qx, qz, qhx, qhz, x, y, wr, Gap)) return false;
                    }
                }
                else
                {
                    double rr = wr + WorkR(b.Kind) + 1.0;
                    if (dx * dx + dy * dy < rr * rr) return false;
                }
            }
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var h = ref w.Households.Get(hl[i]);
                if (h.Building != 0) continue;
                double dx = (double)h.HomeX - x, dy = (double)h.HomeY - y, rr = wr + CampR + 3.0;   // (room left for the home to come)
                if (dx * dx + dy * dy < rr * rr) return false;
            }
            if (!green)
            {
                double qx = (double)s.SquareX - x, qy = (double)s.SquareY - y, gr = Green(in s) + wr;
                if (qx * qx + qy * qy < gr * gr) return false;
            }
            // (as a home: its ground `Streets.Width` off a street's or lane's line - session 17: 1 m before, and kilns, sheds, barns stood a metre or
            // two into the street as drawn)
            if (Streets.On(in s, x, y, wr + (WorkClearOff ? 1.0 : Streets.Width))) return false;
            if (WetDisc(w, in s, x, y, wr)) return false;   // (nor on a river: D2-118)
            return !Structures.OnRing(in s, x, y, wr + 2.5);
        }
    }
}
