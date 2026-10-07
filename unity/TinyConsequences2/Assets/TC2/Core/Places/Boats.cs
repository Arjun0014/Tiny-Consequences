using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Boats (C9.1, D2-110; 24 §5): a dugout is a structure that floats - a row of `World.Buildings` of kind `Structures.Boat`, owned by
    /// the household that made it, lying at the village's landing when it is not out. A household makes one when it lives by the shore's
    /// fish, the sea its boats would reach promises clearly more than the shore, and its woods still stand with trunks big enough to
    /// hollow; its members fell, drag and hollow the trunk (labour and the store's timber, like any work). From then on up to two of them
    /// at a time row out to the open water's grounds (the offshore shoals the shore never reaches), at the cost of the row; nobody goes
    /// out in a strong wind, and a trip begun under a rising one may end with the boat overturned and its people in the water. A dugout
    /// splits and rots in a dozen years. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Boats
    {
        /// <summary>Labour-hours to fell, drag down and hollow a trunk into a dugout (experimental dugouts: ~100-200 h with stone tools,
        /// half that with iron - the builder's pace and tools apply).</summary>
        public const double Hours = 90.0;
        /// <summary>Loads (25 kg) of the store's timber a dugout takes: the trunk itself, hewn (~0.75 t).</summary>
        public const double Wood = 30.0;
        /// <summary>A woodlot's forest share that still has trunks fit to hollow (open, cut-over woods have none).</summary>
        public const double BigTrees = 0.3;
        /// <summary>Years a dugout lasts before it splits past mending.</summary>
        public const double Years = 12.0;
        /// <summary>People out in one boat at once.</summary>
        public const int Seats = 2;
        /// <summary>How much more an hour at sea must promise than an hour on the shore (the row paid) for a household to make a boat.</summary>
        public const double Worth = 1.25;
        /// <summary>A household lives by the shore's fish when one of its grown members has this much skill at fishing (a fisher: practice beyond what anyone is born with).</summary>
        public const double FisherSkill = 0.5;
        /// <summary>Hours at the grounds a trip (lived trips stay 3.5-5 h; COARSE charges the row against a trip of this length).</summary>
        public const double Stint = 4.0;
        /// <summary>m/s: below Calm a trip is safe; at Max and above nobody rows out.</summary>
        public const double Calm = 5.0, Max = 12.0;
        /// <summary>The chance, for a trip begun at the strongest wind they still go out in, that the weather turns and overturns the boat.</summary>
        public const double CaughtOut = 0.08;
        /// <summary>Each person in an overturned boat drowns with this chance (the rest cling to it, or swim ashore).</summary>
        public const double Drown = 0.4;

        /// <summary>A boat that can go out: made, kept (its household still owns it) and not yet split with age.</summary>
        public static bool Sound(in Building b, long now) => IsBoat(b.Kind) && b.Done != 0 && b.Left == 0 && now - b.Built < (long)(Years * SimTime.Year);

        /// <summary>The household's boat when it can go out now (worn ones are left on the beach first), or 0.</summary>
        public static int Of(World w, int hid)
        {
            if (hid == 0 || !w.Households.Exists(hid)) return 0;
            ref var hh = ref w.Households.Get(hid);
            int bid = hh.Boat;
            if (bid == 0) return 0;
            if (!w.Buildings.Exists(bid)) { hh.Boat = 0; return 0; }
            ref var b = ref w.Buildings.Get(bid);
            if (b.Done == 0) return 0;
            if (!Sound(in b, w.Now)) { Worn(w, hid); return 0; }
            return bid;
        }

        /// <summary>The household's boat being made (0 none).</summary>
        public static int Making(World w, int hid)
        {
            if (hid == 0 || !w.Households.Exists(hid)) return 0;
            int bid = w.Households.Get(hid).Boat;
            return bid != 0 && w.Buildings.Exists(bid) && w.Buildings.Get(bid).Done == 0 ? bid : 0;
        }

        /// <summary>A boat past its years: left on the beach as a hulk (gone into the sand in a couple of years); its household may make another.</summary>
        static void Worn(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            int bid = hh.Boat;
            hh.Boat = 0;
            if (!w.Buildings.Exists(bid)) return;
            ref var b = ref w.Buildings.Get(bid);
            b.Household = 0; b.Last = hid;
            b.Left = w.Now - DMath.ToLong(Homes.FallYears(in b) * SimTime.Year) - 1;   // (a hulk at once)
            w.Metrics.BoatsWorn++;
        }

        /// <summary>The share of a trip's hours left for fishing once the row out to the sea's grounds and back is paid (beyond the walk to the shore).</summary>
        public static double AfterRow(World w, in Settlement s)
        {
            if (s.Sea.A < 0) return 0;
            var P = w.Planet;
            double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[s.Sea.A], P.Radius);
            double shore = s.Fish.A >= 0 ? Vec3d.ArcDistance(s.Anchor, P.Grid.Center[s.Fish.A], P.Radius) : 0.0;
            double extra = DMath.Max(0.0, 2.0 * (d - shore) / w.Config.WalkSpeed / 3600.0);
            return Stint / (Stint + extra);
        }

        /// <summary>FU an hour an ordinary fisher can expect at sea from the village's boats, the row paid (0 with no sea grounds).</summary>
        public static double SeaExpect(World w, in Settlement s) => s.Sea.A >= 0 ? s.Sea.Expect * AfterRow(w, in s) : 0.0;

        /// <summary>The wind (m/s) at a point now: the strongest of the storms' winds there.</summary>
        public static double WindAt(World w, Vec3d at)
        {
            var st = w.Planet.Storms;
            double most = 0;
            for (int i = 0; i < st.Count; i++)
            {
                var s = st[i];
                double v = Storms.WindAt(s, Vec3d.ArcDistance(at, s.At, PlanetState.GeoRadius));
                if (v > most) most = v;
            }
            return most;
        }

        /// <summary>The wind over the village's sea grounds now (where its boats would go).</summary>
        public static double SeaWind(World w, in Settlement s) => s.Sea.A >= 0 ? WindAt(w, w.Planet.Grid.Center[s.Sea.A]) : 0.0;

        /// <summary>The chance that a trip begun at this wind ends with the boat overturned: none in a calm, CaughtOut at the strongest
        /// wind they still go out in (the weather turning on them).</summary>
        public static double Risk(double wind) => wind <= Calm ? 0.0 : CaughtOut * DMath.Clamp01((wind - Calm) / (Max - Calm));

        /// <summary>How many of the household are out in its boat now (lived: those at sea in it).</summary>
        public static int Aboard(World w, int hid, int bid)
        {
            var mem = w.Members(hid);
            int n = 0;
            for (int i = 0; i < mem.Count; i++) { ref var q = ref w.People.Get(mem[i]); if (q.Alive && q.Act == Agents.ActivityKind.Sail && q.WhyArg == bid) n++; }
            return n;
        }

        // ---------------------------------------------------------------- making one

        /// <summary>
        /// Whether a household would make a boat now: it has none (or none still sound), it is housed (a roof first), one of its grown
        /// members lives by the shore's fish, the village's sea grounds promise Worth times the shore's catch an hour (the row paid), its
        /// woods still have trunks fit to hollow, the store holds the timber (the trunk at hand) and the village is not short of food.
        /// </summary>
        public static bool Wants(World w, in Settlement s, int hid)
        {
            if (s.Fish.A < 0 || s.Sea.A < 0 || s.Fish.Expect <= 0f) return false;
            if (SeaExpect(w, in s) < Worth * s.Fish.Expect) return false;
            if (!w.Households.Exists(hid)) return false;
            ref var hh = ref w.Households.Get(hid);
            if (hh.Settlement != s.Id || hh.Party != 0 || hh.Shelter == Construction.Camp) return false;
            if (hh.Boat != 0 && (Making(w, hid) != 0 || Of(w, hid) != 0)) return false;
            if (!Fishers(w, hid)) return false;
            if (s.Woodstore < Wood) return false;   // (the trunk at hand: felled and in the store before they begin)
            if (Agents.AgentSystem.WildNeed(in s, w.Now) > 0) return false;   // (no long work begun while rations are short: every hand is out after food)
            int lot = Woods.Lot(in s);
            return lot >= 0 && w.Planet.Forest[lot] >= BigTrees;
        }

        /// <summary>One of the household's grown members fishes with some skill.</summary>
        static bool Fishers(World w, int hid)
        {
            var mem = w.Members(hid);
            long now = w.Now;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var q = ref w.People.Get(mem[i]);
                if (!q.Alive || q.SkFish < FisherSkill) continue;
                double age = q.AgeYears(now);
                if (age >= 14 && age < 60) return true;
            }
            return false;
        }

        /// <summary>
        /// A village's day of boats (both resolutions): the first of its households that would make a boat lays one down at the landing -
        /// one a day, as households decide (a DEEP step: one for each of its days).
        /// </summary>
        public static void Plan(World w, int sid, int days = 1)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited || s.Sea.A < 0) return;
            if (!EnsureLanding(w, sid)) return;   // (no beach to launch from: no boats - C9.2)
            var hl = w.HouseholdsOf(sid);
            // a sea risen over the beach: the boats lying in it are drawn up again at the landing (C9.2; a coast that changed cells: ShoreMoved)
            var P = w.Planet;
            for (int i = 0; i < hl.Count; i++)
            {
                int hb = w.Households.Get(hl[i]).Boat;
                if (hb == 0 || !w.Buildings.Exists(hb)) continue;
                ref var b = ref w.Buildings.Get(hb);
                ref var sb = ref w.Settlements.Get(sid);
                if (b.Left != 0 || !Afloat(P, sb.LocalToSphere(b.X, b.Y, P.Radius))) continue;
                if (Aboard(w, hl[i], hb) > 0) continue;   // (out at sea: drawn up when it is back)
                if (!Landing(w, sid, out float lx, out float ly, hb)) continue;
                ref var b2 = ref w.Buildings.Get(hb); b2.X = lx; b2.Y = ly;
            }
            // a boat still not made a year after it was laid down is given up (no timber came, or no hands): the half-hollowed trunk is left
            for (int i = 0; i < hl.Count; i++)
            {
                int mk = Making(w, hl[i]);
                if (mk == 0 || w.Now - w.Buildings.Get(mk).Built < SimTime.Year) continue;
                ref var b = ref w.Buildings.Get(mk);
                b.Left = w.Now; b.Household = 0; b.Last = hl[i];
                w.Households.Get(hl[i]).Boat = 0;
                w.Metrics.BoatsGivenUp++;
            }
            int begun = 0;
            for (int i = 0; i < hl.Count && begun < days; i++)
            {
                int hid = hl[i];
                if (!Wants(w, in w.Settlements.Get(sid), hid)) continue;
                Begin(w, sid, hid);
                begun++;
            }
        }

        /// <summary>Lays down a household's boat at the village's landing (not yet made): a row of the table, its place on the beach.</summary>
        public static int Begin(World w, int sid, int hid)
        {
            ref var s = ref w.Settlements.Get(sid);
            Landing(w, sid, out float x, out float y);   // (at the village's landing, a free spot on its beach: C9.2)
            int id = w.Ids.Next(EntityKind.Building);
            w.Buildings.Add(id, new Building { Id = id, Settlement = sid, X = x, Y = y, Kind = Structures.Boat, Household = hid, Style = hid, Built = w.Now, Cond = 1f });
            w.BuildingsOf(sid).InsertSorted(id);
            w.Households.Get(hid).Boat = id;
            w.Metrics.BoatsBegun++;
            return id;
        }

        /// <summary>
        /// Hours into the household's boat (a member's share), with the store's timber in step with the labour; when its hours are in it is
        /// made - history tells who made it, from the woods. Returns the hours that found work.
        /// </summary>
        public static double Work(World w, int sid, int hid, double hours, double rate, int worker)
        {
            int bid = Making(w, hid);
            if (bid == 0 || hours <= 0 || rate <= 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            ref var b = ref w.Buildings.Get(bid);
            if (b.Settlement != sid) return 0;
            double left = Hours - b.Hours, woodPerHour = Wood / Hours;
            double doable = DMath.Min(hours * rate, left);
            if (doable > s.Woodstore / woodPerHour) doable = s.Woodstore / woodPerHour;
            if (doable <= 0) return 0;
            s.Woodstore = DMath.Max(0.0, s.Woodstore - doable * woodPerHour);
            b.Hours = (float)(b.Hours + doable); b.Wood = (float)(b.Wood + doable * woodPerHour);
            if (b.Leader == 0 && worker != 0) b.Leader = worker;
            if (b.Hours >= Hours - Construction.Done)
            {
                b.Done = 1; b.Built = w.Now;   // (a boat's life is counted from the day it is made)
                b.Event = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.BoatBuilt, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(hid),
                    Object = new EntityRef(EntityKind.Building, bid), Other = b.Leader != 0 ? EntityRef.Person(b.Leader) : default, RootKind = RootKind.People,
                });
                w.Metrics.BoatsBuilt++;
            }
            return doable / rate;
        }

        /// <summary>Whether the household has a boat to make and the store has timber for it.</summary>
        public static bool HasWork(World w, in Settlement s, int hid) => Making(w, hid) != 0 && s.Woodstore >= Wood / Hours;

        // ---------------------------------------------------------------- the landing (C9.2)

        /// <summary>
        /// A place for a boat at the village's landing: the landing kept (EnsureLanding), then the first free spot along the beach either side
        /// of it - a boat's length and a half apart, each drawn up to just above the waterline there (Drawn), none within a boat's width of
        /// another boat of the village (`except`: the boat being placed). False when the village has no beach (the square then).
        /// </summary>
        public static bool Landing(World w, int sid, out float x, out float y, int except = 0)
        {
            ref var s0 = ref w.Settlements.Get(sid);
            x = s0.SquareX; y = s0.SquareY;
            if (!EnsureLanding(w, sid)) return false;
            ref var s = ref w.Settlements.Get(sid);
            double vx = -s.LandUy, vy = s.LandUx;   // (along the beach)
            for (int k = 0; k < 24; k++)
            {
                double side = ((k & 1) == 0 ? 1.0 : -1.0) * ((k + 1) >> 1) * 3.5;
                if (!Drawn(w, in s, s.LandX + vx * side, s.LandY + vy * side, s.LandUx, s.LandUy, out double bx, out double by)) continue;
                if (Taken(w, sid, bx, by, except)) continue;
                x = (float)bx; y = (float)by;
                return true;
            }
            x = s.LandX; y = s.LandY;   // (two dozen boats on one beach: the next lies by the first)
            return true;
        }

        /// <summary>m above the sea a boat is drawn up to - above the reach of a sea a little risen - or as far as UpTheBeachMost up a flat beach.</summary>
        const double DrawnUp = 1.0, UpTheBeachMost = 25.0;

        /// <summary>
        /// A boat drawn up from the sea at (x, y): down the beach (against u) to the sea's surface within 40 m, or - where that spot lies in the
        /// water (a beach curving out) - up it out of the water; then up the beach from the waterline (UpTheBeach). False when no sea is that near.
        /// </summary>
        static bool Drawn(World w, in Settlement s, double x, double y, double ux, double uy, out double bx, out double by)
        {
            var P = w.Planet;
            bx = x; by = y;
            if (Afloat(P, s.LocalToSphere(x, y, P.Radius)))
            {
                for (double t = 1.0; t <= 40.0; t += 1.0)
                    if (!Afloat(P, s.LocalToSphere(x + ux * t, y + uy * t, P.Radius))) return UpTheBeach(w, in s, x + ux * t, y + uy * t, ux, uy, out bx, out by);
                return false;
            }
            for (double t = 1.0; t <= 40.0; t += 1.0)
                if (AtSea(P, s.LocalToSphere(x - ux * t, y - uy * t, P.Radius)))
                    return UpTheBeach(w, in s, x - ux * (t - 1.0), y - uy * (t - 1.0), ux, uy, out bx, out by);
            return false;
        }

        /// <summary>From the waterline at (x, y), up the beach (u) until the ground stands DrawnUp above the sea - at most UpTheBeachMost metres:
        /// where a boat is drawn up out of the reach of a sea a little risen. False when that spot is itself in the water.</summary>
        static bool UpTheBeach(World w, in Settlement s, double x, double y, double ux, double uy, out double bx, out double by)
        {
            var P = w.Planet;
            bx = x; by = y;
            for (double t = 0.0; t <= UpTheBeachMost; t += 1.0)
            {
                bx = x + ux * t; by = y + uy * t;
                if (Shore.At(P, s.LocalToSphere(bx, by, P.Radius), out _, out double lake) >= P.SeaLevel + DrawnUp && lake <= 0.3) break;
            }
            return !Afloat(P, s.LocalToSphere(bx, by, P.Radius));
        }

        /// <summary>Another kept boat of the village lies within a boat's width of this spot.</summary>
        static bool Taken(World w, int sid, double x, double y, int except)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == except) continue;
                ref var o = ref w.Buildings.Get(bl[i]);
                if (!IsBoat(o.Kind) || o.Left != 0) continue;
                double dx = (double)o.X - x, dy = (double)o.Y - y;
                if (dx * dx + dy * dy < 2.5 * 2.5) return true;
            }
            return false;
        }

        /// <summary>A kind of structure that floats (C9.1: the dugout).</summary>
        public static bool IsBoat(byte kind) => kind == Structures.Boat;

        /// <summary>
        /// The village's landing (C9.2): the beach nearest its square - chosen when it first lays down a boat and kept while the water is there;
        /// when the coast has left it, the beach nearest then. Before, each boat lay at the beach of the day's best fishing ground, so a
        /// village's boats lay scattered along its shores, some a kilometre out.
        /// </summary>
        public static bool EnsureLanding(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.LandAt != 0 && AtWater(w, in s, s.LandX, s.LandY)) return true;
            if (!Beach(w, sid, out double x, out double y, out double ux, out double uy)) return false;
            ref var s2 = ref w.Settlements.Get(sid);
            s2.LandX = (float)x; s2.LandY = (float)y; s2.LandUx = (float)ux; s2.LandUy = (float)uy; s2.LandAt = w.Now;
            return true;
        }

        /// <summary>
        /// The beach nearest the village's square: the sea nearest it, ring by ring of cells out from the village's cell (of the first ring that
        /// reaches the sea, the cell nearest the square), then the drawn waterline on the way from the square to that sea (the shore as the
        /// view draws it, Shore.At - not a cell's edge: on a low coast the cells' midpoint lay a hundred metres from the water), the landing
        /// DrawnUp metres up from it; `ux, uy` up the beach (toward the square). Where the way meets no sea, the shore nearest the village's best
        /// fishing ground instead.
        /// </summary>
        static bool Beach(World w, int sid, out double x, out double y, out double ux, out double uy)
        {
            var P = w.Planet; var g = P.Grid;
            ref var s = ref w.Settlements.Get(sid);
            x = s.SquareX; y = s.SquareY; ux = 0; uy = 1;
            Vec3d square = s.LocalToSphere(s.SquareX, s.SquareY, P.Radius);
            if (Edge(w, s.Cell, square, false, out int sea, out _))
            {
                s.SphereToLocal(g.Center[sea], P.Radius, out double tx, out double ty);
                if (Waterline(w, in s, s.SquareX, s.SquareY, tx, ty, out x, out y, out ux, out uy)) return true;
            }
            int ground = s.Fish.A >= 0 ? s.Fish.A : s.Sea.A;
            if (ground < 0) return false;
            s.SphereToLocal(g.Center[ground], P.Radius, out double gx, out double gy);
            return Waterline(w, in s, s.SquareX, s.SquareY, gx, gy, out x, out y, out ux, out uy);
        }

        /// <summary>
        /// On the way from (fx, fy) toward (tx, ty) (and a little beyond: the drawn shore may lie past a cell's centre), the first spot at the
        /// sea's surface; the landing DrawnUp metres back from the last spot out of the water, `u` the way back. False when no sea is met.
        /// </summary>
        static bool Waterline(World w, in Settlement s, double fx, double fy, double tx, double ty, out double x, out double y, out double ux, out double uy)
        {
            var P = w.Planet;
            x = fx; y = fy; ux = 0; uy = 1;
            // (from dry ground: a square the sea has risen over starts from the nearest spot out of the water)
            OutOfTheWater(w, in s, ref fx, ref fy);
            if (Afloat(P, s.LocalToSphere(fx, fy, P.Radius))) return false;
            double dx = tx - fx, dy = ty - fy, len = DMath.Sqrt(dx * dx + dy * dy);
            if (len < 1.0) return false;
            dx /= len; dy /= len;
            for (double t = 2.0; t <= len + 300.0; t += 2.0)
            {
                if (!AtSea(P, s.LocalToSphere(fx + dx * t, fy + dy * t, P.Radius))) continue;
                ux = -dx; uy = -dy;
                double back = t - 2.0;   // (the last spot out of the water on the way)
                return UpTheBeach(w, in s, fx + dx * back, fy + dy * back, ux, uy, out x, out y);
            }
            return false;
        }

        /// <summary>The sea's surface at a spot (the drawn ground at or below it): the water a boat is launched into.</summary>
        public static bool AtSea(PlanetState P, Vec3d dir) => Shore.At(P, dir, out _, out _) <= P.SeaLevel + 0.3;

        /// <summary>
        /// A spot under water: the sea's surface or a lake over it. A boat drawn up a beach lies a little above the waterline - the two metres
        /// a home keeps above the sea (Shore.Dry) are not asked of it (a sea risen a few centimetres put boats "in the water" that lay on the sand).
        /// </summary>
        public static bool Afloat(PlanetState P, Vec3d dir)
        {
            double e = Shore.At(P, dir, out _, out double lake);
            return e <= P.SeaLevel + 0.3 || lake > 0.3;
        }

        /// <summary>A spot where a boat can be kept: out of the water, with the sea itself within 40 m of it (not merely low ground: a coastal
        /// plain below a home's two-metre margin let boats lie a hundred metres from the water).</summary>
        public static bool AtWater(World w, in Settlement s, double x, double y)
        {
            var P = w.Planet;
            if (Afloat(P, s.LocalToSphere(x, y, P.Radius))) return false;
            for (int k = 0; k < 16; k++)
            {
                double a = k * DMath.PI / 8.0;
                for (double r = 10.0; r <= 40.0; r += 10.0)
                    if (AtSea(P, s.LocalToSphere(x + r * DMath.Cos(a), y + r * DMath.Sin(a), P.Radius))) return true;
            }
            return false;
        }

        /// <summary>
        /// The coast moved (World.LandChanged): a landing the water has left (risen over it, or drawn back from it) gives way to the beach nearest
        /// now and its boats are drawn up there; a kept boat that lies in the water or away from it is drawn up at the landing. Before C9.1's
        /// close, boats lay on where the sea had gone.
        /// </summary>
        public static void ShoreMoved(World w)
        {
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                bool moved = s.LandAt != 0 && !AtWater(w, in s, s.LandX, s.LandY);
                if (moved) s.LandAt = 0;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (!IsBoat(b.Kind) || b.Left != 0) continue;
                    if (!moved && AtWater(w, in w.Settlements.Get(sid), b.X, b.Y)) continue;
                    if (!Landing(w, sid, out float x, out float y, bl[i])) continue;
                    ref var bb = ref w.Buildings.Get(bl[i]); bb.X = x; bb.Y = y;
                }
            }
        }

        /// <summary>
        /// The nearest cell of the other kind: rings of neighbours out from `start` (up to 16), the first ring with a cell that is land (`wantLand`)
        /// or sea, and of those the cell nearest `toward`; `from` the cell of the ring before it was reached from. False when none is that near.
        /// </summary>
        public static bool Edge(World w, int start, Vec3d toward, bool wantLand, out int found, out int from)
        {
            var P = w.Planet; var g = P.Grid;
            found = -1; from = -1;
            var seen = new System.Collections.Generic.HashSet<int> { start };
            var ring = new System.Collections.Generic.List<int> { start };
            var next = new System.Collections.Generic.List<int>();
            for (int r = 0; r < 16 && ring.Count > 0; r++)
            {
                next.Clear();
                double bd = double.MaxValue;
                for (int i = 0; i < ring.Count; i++)
                    for (int j = 0; j < 8; j++)
                    {
                        int nb = g.Nbr8[ring[i] * 8 + j];
                        if (!seen.Add(nb)) continue;
                        if (P.IsLand(nb) == wantLand)
                        {
                            double d = Vec3d.ArcDistance(g.Center[nb], toward, P.Radius);
                            if (d < bd) { bd = d; found = nb; from = ring[i]; }
                        }
                        else next.Add(nb);
                    }
                if (found >= 0) return true;
                var t = ring; ring = next; next = t;
            }
            return false;
        }

        /// <summary>A spot in the water moved to the nearest one out of it (rings out to 300 m, 4 m apart): where someone stands to fish from the shore.</summary>
        public static void OutOfTheWater(World w, in Settlement s, ref double x, ref double y)
        {
            var P = w.Planet;
            if (!Afloat(P, s.LocalToSphere(x, y, P.Radius))) return;
            for (double r = 4.0; r <= 300.0; r += 4.0)
            {
                int steps = (int)DMath.Max(8.0, DMath.Floor(2.0 * DMath.PI * r / 4.0));
                for (int k = 0; k < steps; k++)
                {
                    double a = 2.0 * DMath.PI * k / steps;
                    double qx = x + r * DMath.Cos(a), qy = y + r * DMath.Sin(a);
                    if (!Afloat(P, s.LocalToSphere(qx, qy, P.Radius))) { x = qx; y = qy; return; }
                }
            }
        }

        // ---------------------------------------------------------------- the sea's dangers

        /// <summary>
        /// A trip at sea ends under the wind it is out in (both resolutions; a DEEP step's days are its trips): with the chance `Risk` the boat
        /// overturns - lost, its people in the water, each drowning with the chance Drown (the household's loss in history, the drowned dead of
        /// it). Returns 0, 1 overturned, 2 overturned and drowned; `kill` false leaves the death to the caller (`lost`: the loss's event, its cause).
        /// </summary>
        public static int Trip(World w, int sid, int hid, int bid, int pid, double wind, out long lost, int days = 1, bool kill = true)
        {
            lost = 0;
            double risk = Risk(wind);
            if (risk <= 0) return 0;
            if (KeyedRng.Unit(w.Seed, RngStream.Work, (ulong)bid, (ulong)w.Now, 71) >= SettlementSystem.Over(risk, days)) return 0;
            return Overturned(w, sid, hid, bid, pid, wind, out lost, kill) ? 2 : 1;
        }

        /// <summary>The boat overturned with this person aboard: it is lost; they drown with the chance Drown. Returns whether they drowned.</summary>
        public static bool Overturned(World w, int sid, int hid, int bid, int pid, double wind, out long ev, bool kill = true)
        {
            ev = 0;
            bool drowned = KeyedRng.Unit(w.Seed, RngStream.Death, (ulong)pid, (ulong)w.Now, 72) < Drown;
            if (w.Buildings.Exists(bid))
            {
                ref var b = ref w.Buildings.Get(bid);
                if (b.Left == 0)
                {
                    b.Left = w.Now - DMath.ToLong(Homes.FallYears(in b) * SimTime.Year) - 1;   // (gone: nothing comes back but a few planks)
                    b.Cond = 0f;   // (lost, not worn: those still out in it are in the water)
                    b.Household = 0; b.Last = hid;
                    if (hid != 0 && w.Households.Exists(hid) && w.Households.Get(hid).Boat == bid) w.Households.Get(hid).Boat = 0;
                    ref var s = ref w.Settlements.Get(sid);
                    ev = w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.BoatLost, Time = w.Now, Cell = s.Sea.A >= 0 ? s.Sea.A : s.Cell, Settlement = sid, Subject = EntityRef.Household(hid),
                        Object = new EntityRef(EntityKind.Building, bid), Other = EntityRef.Person(pid), Magnitude = (float)wind, ArgA = drowned ? 1 : 0,
                        Cause1 = b.Event, Rel1 = CauseRel.PartOf, RootKind = RootKind.Nature,
                    });
                    w.Metrics.BoatsLost++;
                }
                else ev = b.Event;
            }
            if (drowned && w.People.Exists(pid))
            {
                ref var p = ref w.People.Get(pid);
                if (!p.Alive) return false;
                w.Metrics.Drowned++;
                if (kill) w.Agents.Kill(ref p, ev, 12);
            }
            return drowned;
        }

        // ---------------------------------------------------------------- households change

        /// <summary>A household leaves its village or ends: its boat stays on the beach, unkept (it rots), unless an heir there takes it.</summary>
        public static void Leave(World w, int hid, int heir = 0)
        {
            if (hid == 0 || !w.Households.Exists(hid)) return;
            ref var hh = ref w.Households.Get(hid);
            int bid = hh.Boat;
            hh.Boat = 0;
            if (bid == 0 || !w.Buildings.Exists(bid)) return;
            ref var b = ref w.Buildings.Get(bid);
            if (heir != 0 && w.Households.Exists(heir) && w.Households.Get(heir).Boat == 0 && w.Households.Get(heir).Settlement == b.Settlement)
            {
                b.Household = heir; w.Households.Get(heir).Boat = bid;   // (the heir's now)
                return;
            }
            b.Household = 0; b.Last = hid;
            b.Left = w.Now;
        }
    }
}
