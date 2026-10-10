using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The household's garden (C3.7a, `26` §2h): the croft behind its house - leeks, cabbages, onions, peas and beans, herbs - dug and planted in
    /// the sowing, weeded and watered while the crops grow, gathered from the ripening into the year's end, the produce into the household's larder
    /// as it is gathered. Its ground is what the village's layout leaves the home: strips behind it and beside it, free of every other building,
    /// street, lane and the green and nearer this home than its neighbours' (`Measure`), measured when the year turns (a town's built-up core leaves little or none).
    /// Real rates: spade work ~0.28 h a m2 a year, a mixed garden ~500 kcal a m2 - in this world's food units (a person-day, a 48-day year: a
    /// real year's food is 48 FU) ~0.03 FU a m2: a croft of 150-300 m2 gives a household ~2-4 % of its food for 40-90 hours. The weather's growth
    /// (the village's crop growth) reaches it as it reaches the fields. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Gardens
    {
        /// <summary>Tools: no gardens (`--nogarden`: the worlds before C3.7a).</summary>
        public static bool Off;

        /// <summary>Work-hours a m2 a year, by its season: digging and planting (the sowing), weeding and watering (the growing), gathering.</summary>
        public const double DigH = 0.10, TendH = 0.12, GatherH = 0.06, YearH = DigH + TendH + GatherH;
        /// <summary>FU a m2 a year a garden kept well bears in a good year.</summary>
        public const double FuPerM2 = 0.03;
        /// <summary>m2: the most a household's croft is measured to (a tenth of an acre and a little more).</summary>
        public const double MaxM2 = 320.0;
        /// <summary>m: how far its croft is measured - 16 m behind its back wall, 8 m beside each end (in 4 m columns).</summary>
        const double Strip = 4.0, Behind = 16.0, Beside = 8.0;
        /// <summary>The crop calendar's days (SettlementSystem): the sowing to day 12, the growing to day 30, the gathering to the year's end.</summary>
        const double SowEnd = 12.0, GrowEnd = 30.0, YearEnd = 48.0;

        /// <summary>The house's half extent in its own frame (along its front, toward it), walls and eaves.</summary>
        static void Extent(int plan, out double hx, out double hz)
        {
            hx = 0; hz = 0;
            for (int q = 0; q < Footprints.Count(plan); q++)
            {
                Footprints.Rect(plan, q, out double cx, out double cz, out double rhx, out double rhz);
                hx = DMath.Max(hx, DMath.Abs(cx) + rhx); hz = DMath.Max(hz, DMath.Abs(cz) + rhz);
            }
        }

        /// <summary>
        /// The croft a home's layout leaves it (m2), and where its gardener works (the middle of its first free strip, local metres): strips 4 m deep
        /// behind its back wall, as wide as the house and 2 m more each side, while each is free (`Footprints.YardFree`) and short of half the way to
        /// the next home behind; then beside each end, 4 m wide, from a metre behind its front line to 4 m behind its back, while free and short of
        /// half the way to the next home along. A town's built-up core leaves little or none.
        /// </summary>
        public static double Measure(World w, int sid, int bid, out double gx, out double gy)
        {
            ref var b = ref w.Buildings.Get(bid);
            gx = b.X; gy = b.Y;
            _strips.Clear();
            Strips(w, sid, bid, _strips);
            if (_strips.Count == 0) return 0.0;
            double area = 0.0;
            for (int i = 0; i < _strips.Count; i++) area += 4.0 * _strips[i].hx * _strips[i].hz;
            ref var s = ref w.Settlements.Get(sid);
            double af = Footprints.FaceOf(in s, in b), fx = DMath.Cos(af), fy = DMath.Sin(af);
            Local(in b, -fy, fx, fx, fy, _strips[0].cx, _strips[0].cz, out gx, out gy);
            return DMath.Min(MaxM2, area);
        }

        static readonly System.Collections.Generic.List<(double cx, double cz, double hx, double hz)> _strips = new System.Collections.Generic.List<(double, double, double, double)>();

        /// <summary>m: the croft's rows behind the house (three columns across it, 2 m deep); beside each end it is followed 4 m at a time.</summary>
        const double Row = 2.0;
        static readonly System.Collections.Generic.List<int> _near = new System.Collections.Generic.List<int>();

        /// <summary>
        /// The croft's free ground in the house's frame as rectangles (centre along its front, toward it; half sizes), one a column or row - behind it
        /// first (its middle column first: where its gardener goes), then beside each end; the view draws its beds on them and `Measure` sums them. Behind: three columns as wide as
        /// the house and 2 m more each side, each followed back 2 m at a time while free and short of half the way to the next home behind; beside each
        /// end: the half by the house and the half behind it, each followed outward 4 m at a time while free and short of half the way to the next home
        /// along. Free: no other building's ground (the near ones, gathered once), not the green, no street or lane where lined (as `YardFree`). Read only.
        /// </summary>
        public static void Strips(World w, int sid, int bid, System.Collections.Generic.List<(double cx, double cz, double hx, double hz)> into)
        {
            ref var s = ref w.Settlements.Get(sid);
            ref var b = ref w.Buildings.Get(bid);
            if (Off || !Structures.IsHome(b.Kind)) return;
            int plan = Footprints.PlanOf(in b);   // (any plan: a town's built-up core leaves no free ground, and burgage plots ran behind town houses)
            Extent(plan, out double hx, out double hz);
            double af = Footprints.FaceOf(in s, in b), fx = DMath.Cos(af), fy = DMath.Sin(af), ux = -fy, uy = fx;
            // the near buildings (within the croft's reach and their own), and half the way to the nearest home behind and along each end
            double reach = DMath.Sqrt((hx + Beside + Strip) * (hx + Beside + Strip) + (hz + Behind) * (hz + Behind));
            double back = Behind, left = Beside, right = Beside;
            _near.Clear();
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == bid) continue;
                ref var o = ref w.Buildings.Get(bl[i]);
                if (Homes.RuinLeft(in o, w.Now) <= 0) continue;
                double dx = (double)o.X - b.X, dy = (double)o.Y - b.Y;
                bool home = Structures.IsHome(o.Kind);
                double rr = reach + (home ? Footprints.Reach(Footprints.PlanOf(in o)) : Footprints.WorkR(o.Kind));
                if (dx * dx + dy * dy >= rr * rr) continue;
                _near.Add(bl[i]);
                if (!home) continue;
                double along = -(dx * fx + dy * fy), across = dx * ux + dy * uy;   // (along > 0 behind; across > 0 to its right)
                if (along > 0 && DMath.Abs(across) < hx + 2.0 + 4.0) back = DMath.Min(back, along * 0.5 - hz);
                if (DMath.Abs(along) < hz + 4.0)
                {
                    if (across > 0) right = DMath.Min(right, across * 0.5 - hx);
                    else left = DMath.Min(left, -across * 0.5 - hx);
                }
            }
            double wx = hx + 2.0, cw = wx * 2.0 / 3.0;
            for (int k = 0; k < 3; k++)
            {
                double cx = (k == 0 ? 0 : k == 1 ? -1 : 1) * cw, d = 0.0;
                while (d + Row <= back + 1e-9 && Free(w, in s, in b, af, cx, -(hz + d + Row * 0.5), cw * 0.5, Row * 0.5)) d += Row;
                if (d > 0) into.Add((cx, -(hz + d * 0.5), cw * 0.5, d * 0.5));   // (the column's free rows, one bed: contiguous from the wall)
            }
            double zf = hz - 1.0, zb = -(hz + Strip), zm = 0.5 * (zf + zb), hzs = 0.25 * (zf - zb);
            for (int side = -1; side <= 1; side += 2)
            {
                double room = side > 0 ? right : left;
                for (int row = 0; row < 2; row++)
                {
                    double cz = row == 0 ? zm + hzs : zm - hzs, d = 0.0;
                    while (d + Strip <= room + 1e-9 && Free(w, in s, in b, af, side * (hx + d + Strip * 0.5), cz, Strip * 0.5, hzs)) d += Strip;
                    if (d > 0) into.Add((side * (hx + d * 0.5), cz, d * 0.5, hzs));   // (the row's free columns, one bed)
                }
            }
        }

        /// <summary>A rectangle of the home's yard (its frame) free of the near buildings' ground, the green and the streets and lanes (`Footprints.YardFree`
        /// over the near buildings only).</summary>
        static bool Free(World w, in Settlement s, in Building a, double af, double mx, double mz, double hx, double hz)
        {
            for (int i = 0; i < _near.Count; i++)
            {
                ref var o = ref w.Buildings.Get(_near[i]);
                if (Structures.IsHome(o.Kind))
                {
                    int op = Footprints.PlanOf(in o);
                    double of = Footprints.FaceOf(in s, in o);
                    for (int q = 0; q < Footprints.Count(op); q++)
                    {
                        Footprints.Rect(op, q, out double qx, out double qz, out double qhx, out double qhz);
                        if (Footprints.Boxes(a.X, a.Y, af, mx, mz, hx, hz, o.X, o.Y, of, qx, qz, qhx, qhz, 0.0)) return false;
                    }
                }
                else if (Footprints.BoxDisc(a.X, a.Y, af, mx, mz, hx, hz, o.X, o.Y, Footprints.WorkR(o.Kind), 0.0)) return false;
            }
            if (Footprints.BoxDisc(a.X, a.Y, af, mx, mz, hx, hz, s.SquareX, s.SquareY, Footprints.Green(in s), 0.0)) return false;
            return !Footprints.InLine(in s, a.X, a.Y, af, mx, mz, hx, hz);
        }

        static void Local(in Building b, double ux, double uy, double fx, double fy, double cx, double cz, out double x, out double y)
        {
            x = b.X + ux * cx + fx * cz;
            y = b.Y + uy * cx + fy * cz;
        }

        /// <summary>The year turns (the sowing's first day, SettlementSystem): each household's croft measured again, its year's work and produce
        /// begun again.</summary>
        public static void Year(World w, int sid)
        {
            long year = SimTime.FloorDiv(w.Now, SimTime.Year);
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                hh.GardenWork = 0f; hh.GardenFu = 0f;
                if (hh.Building == 0 || !w.Buildings.Exists(hh.Building)) { hh.Garden = 0f; hh.GardenAt = 0; continue; }
                // (measured again when its home changed, else every fifth year: crofts change slowly as neighbours build - a fifth of the cost)
                if (hh.GardenAt == hh.Building && (year + hh.Id) % 5 != 0) continue;
                hh.Garden = (float)Measure(w, sid, hh.Building, out _, out _);
                hh.GardenAt = hh.Building;
            }
        }

        /// <summary>FU the garden bears this year: its area, how much of its digging and weeding was done while the crops grew, the season's growth.</summary>
        public static double Bears(in Household hh, double cropGrowth)
        {
            double m2 = hh.Garden;
            if (m2 <= 0) return 0.0;
            double kept = DMath.Clamp01(hh.GardenWork / ((DigH + TendH) * m2));
            return FuPerM2 * m2 * kept * DMath.Clamp01(cropGrowth);
        }

        /// <summary>
        /// What its croft shows on this day of the year (the view; read only): `dug` the share of its beds dug this year (fresh earth; the digging
        /// comes first of its year's work), `cleared` the share gathered and cleared from the ripening on; true while the dug beds are in leaf (from the
        /// growing on - in the sowing they are planted but bare). A bed is in leaf while dug and not yet cleared; nothing grown clears all.
        /// </summary>
        public static bool Shown(in Household hh, int doy, double cropGrowth, out double dug, out double cleared)
        {
            dug = 0.0; cleared = 0.0;
            double m2 = hh.Garden;
            if (Off || m2 <= 0) return false;
            dug = DMath.Clamp01(hh.GardenWork / (DigH * m2));
            if (doy < SowEnd) return false;
            if (doy < GrowEnd) return true;
            double bears = Bears(in hh, cropGrowth);
            cleared = bears > 1e-9 ? dug * DMath.Clamp01(hh.GardenFu / bears) : dug;
            return true;
        }

        /// <summary>
        /// Hours of its garden's work still wanted by this day of the year: in the sowing and the growing, the digging and the weeding due by now less
        /// what was done; from the ripening, the gathering due by now less what was gathered (none where the garden bears nothing).
        /// </summary>
        public static double Behindhand(in Household hh, int doy, double cropGrowth)
        {
            if (Off || hh.Garden <= 0f || hh.Building == 0) return 0.0;
            double m2 = hh.Garden, d = doy + 1.0;
            if (doy < GrowEnd)
                return m2 * (DigH * DMath.Clamp01(d / SowEnd) + TendH * DMath.Clamp01((d - SowEnd) / (GrowEnd - SowEnd))) - hh.GardenWork;
            double bears = Bears(in hh, cropGrowth);
            if (bears <= 1e-9) return 0.0;
            double gatherH = GatherH * m2;
            return gatherH * DMath.Clamp01((d - GrowEnd) / (YearEnd - GrowEnd)) - gatherH * hh.GardenFu / bears;
        }

        /// <summary>
        /// Hours given to the household's garden on this day of the year: while the crops grow they dig, plant and weed it (as far as it wants);
        /// from the ripening they gather what it bears, each hour its share, into the larder. Returns the FU gathered.
        /// </summary>
        public static double Work(ref Household hh, int doy, double cropGrowth, double hours)
        {
            if (hours <= 0 || hh.Garden <= 0f) return 0.0;
            double m2 = hh.Garden;
            if (doy < GrowEnd)
            {
                hh.GardenWork = (float)DMath.Min((DigH + TendH) * m2, (double)hh.GardenWork + hours);
                return 0.0;
            }
            double bears = Bears(in hh, cropGrowth);
            double fu = DMath.Min(bears * hours / (GatherH * m2), DMath.Max(0.0, bears - hh.GardenFu));
            if (fu <= 0) return 0.0;
            hh.GardenFu = (float)(hh.GardenFu + fu);
            hh.Larder += fu;
            return fu;
        }
    }
}
