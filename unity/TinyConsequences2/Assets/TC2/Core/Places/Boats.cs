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
        public static bool Sound(in Building b, long now) => b.Kind == Structures.Boat && b.Done != 0 && b.Left == 0 && now - b.Built < (long)(Years * SimTime.Year);

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
            var hl = w.HouseholdsOf(sid);
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
            Landing(w, sid, out float x, out float y);
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

        /// <summary>Where the village's boats are drawn up: on the beach of its best fishing ground, a boat's length apart, on dry sand (the
        /// square when it fishes no water, or no dry sand lies near it: false).</summary>
        public static bool Landing(World w, int sid, out float x, out float y)
        {
            var P = w.Planet; var g = P.Grid;
            ref var s = ref w.Settlements.Get(sid);
            int ground = s.Fish.A >= 0 ? s.Fish.A : s.Sea.A;
            if (ground < 0) { x = s.SquareX; y = s.SquareY; return false; }
            // the water's edge of that ground nearest the village (WildFood.GroundPoint's shore): the land nearest the ground, ring by ring
            // of neighbours out from it (a ground fished from the shore may lie a few cells out), the nearest the village of the first ring
            // that reaches land, and the water it was reached from
            Vec3d at = g.Center[ground];
            if (ShoreOf(w, ground, s.Anchor, out int land, out int water)) at = (g.Center[land] + g.Center[water]).Normalized;
            s.SphereToLocal(at, P.Radius, out double ex, out double ey);
            // the nearest dry sand to that edge (rings outward: the square may lie across a bay), then along the beach by how many boats lie
            // there - each spot itself dry
            int n = 0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) { ref var o = ref w.Buildings.Get(bl[i]); if (o.Kind == Structures.Boat && o.Left == 0) n++; }
            if (!NearestDry(w, in s, ex, ey, out double px, out double py)) { x = s.SquareX; y = s.SquareY; return false; }
            double side = ((n & 1) == 0 ? 1.0 : -1.0) * ((n + 1) >> 1) * 3.5;
            double dx = px - ex, dy = py - ey, len = DMath.Max(1.0, DMath.Sqrt(dx * dx + dy * dy));
            double ux = dx / len, uy = dy / len;   // (up the beach: from the water's edge to the dry sand)
            if (!NearestDry(w, in s, px + 2.0 * ux - uy * side, py + 2.0 * uy + ux * side, out double bx, out double by)) { bx = px; by = py; }
            x = (float)bx; y = (float)by;
            return true;
        }

        /// <summary>
        /// A spot under water: the sea's surface or a lake over it. A boat drawn up a beach lies a little above the waterline - the two metres
        /// a home keeps above the sea (Shore.Dry) are not asked of it (a sea risen a few centimetres put boats "in the water" that lay on the sand).
        /// </summary>
        public static bool Afloat(PlanetState P, Vec3d dir)
        {
            double e = Shore.At(P, dir, out _, out double lake);
            return e <= P.SeaLevel + 0.3 || lake > 0.3;
        }

        /// <summary>A spot where a boat can be kept: out of the water, with the water's edge (the beach below a home's margin) within 40 m of it.</summary>
        public static bool AtWater(World w, in Settlement s, double x, double y)
        {
            var P = w.Planet;
            if (Afloat(P, s.LocalToSphere(x, y, P.Radius))) return false;
            for (int k = 0; k < 8; k++)
            {
                double a = k * DMath.PI / 4.0;
                if (!Shore.Dry(P, s.LocalToSphere(x + 40.0 * DMath.Cos(a), y + 40.0 * DMath.Sin(a), P.Radius))) return true;
            }
            return false;
        }

        /// <summary>
        /// The coast moved (World.LandChanged): a kept boat the sea has risen over, or one it has drawn back from - left on dry land away from
        /// the water - is drawn up again at the village's landing on the new shore. Before, boats lay on where the sea had gone (C9.1).
        /// </summary>
        public static void ShoreMoved(World w)
        {
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                if (!w.Settlements.Get(sid).Inhabited) continue;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (b.Kind != Structures.Boat || b.Left != 0 || AtWater(w, in w.Settlements.Get(sid), b.X, b.Y)) continue;
                    if (!Landing(w, sid, out float x, out float y)) continue;
                    ref var bb = ref w.Buildings.Get(bl[i]); bb.X = x; bb.Y = y;
                }
            }
        }

        /// <summary>
        /// The shore nearest a ground of water: rings of neighbours out from it (up to 16), the first ring with land in it, and of its land
        /// the cell nearest `toward` (the village); `water` the cell of the ring before it was reached from. False when no land is that near.
        /// </summary>
        static bool ShoreOf(World w, int ground, Vec3d toward, out int land, out int water)
        {
            var P = w.Planet; var g = P.Grid;
            land = -1; water = -1;
            var seen = new System.Collections.Generic.HashSet<int> { ground };
            var ring = new System.Collections.Generic.List<int> { ground };
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
                        if (P.IsLand(nb))
                        {
                            double d = Vec3d.ArcDistance(g.Center[nb], toward, P.Radius);
                            if (d < bd) { bd = d; land = nb; water = ring[i]; }
                        }
                        else next.Add(nb);
                    }
                if (land >= 0) return true;
                var t = ring; ring = next; next = t;
            }
            return false;
        }

        /// <summary>The nearest point on dry ground to (x, y) (local metres), searched in rings out to 300 m; false when there is none.</summary>
        static bool NearestDry(World w, in Settlement s, double x, double y, out double dx, out double dy)
        {
            var P = w.Planet;
            dx = x; dy = y;
            if (Shore.Dry(P, s.LocalToSphere(x, y, P.Radius))) return true;
            for (double r = 4.0; r <= 300.0; r += 4.0)
            {
                int steps = (int)DMath.Max(8.0, DMath.Floor(2.0 * DMath.PI * r / 4.0));
                for (int k = 0; k < steps; k++)
                {
                    double a = 2.0 * DMath.PI * k / steps;
                    double qx = x + r * DMath.Cos(a), qy = y + r * DMath.Sin(a);
                    if (Shore.Dry(P, s.LocalToSphere(qx, qy, P.Radius))) { dx = qx; dy = qy; return true; }
                }
            }
            return false;
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
