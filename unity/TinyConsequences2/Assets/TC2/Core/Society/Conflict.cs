using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Society
{
    /// <summary>A band raised to fall on a village (D2-073): its people carry its id; it is resolved once, when it comes.</summary>
    public struct Band
    {
        public int Id, Origin, Target, Leader;
        public byte Size, Motive, State;   // Motive 1 its village's wrong, 2 a tributary's wrong its lord answers (For), 0 hunger; State 0 raised, 1 fallen
        public byte Called;                // tributaries whose people answered its lord's call (C10.1; 0: its own village's people only)
        public int For;                    // the tributary whose wrong it answers (Motive 2; D2-108)
        public long Formed, Arrive, Event;
        public float Loot;

        public void Write(BinWriter w) { w.Int(Id); w.Int(Origin); w.Int(Target); w.Int(Leader); w.Byte(Size); w.Byte(Motive); w.Byte(State); w.Long(Formed); w.Long(Arrive); w.Long(Event); w.Float(Loot); w.Byte(Called); w.Int(For); }
        public static Band Read(BinReader r) => new Band { Id = r.Int(), Origin = r.Int(), Target = r.Int(), Leader = r.Int(), Size = r.Byte(), Motive = r.Byte(), State = r.Byte(), Formed = r.Long(), Arrive = r.Long(), Event = r.Long(), Loot = r.Float(), Called = r.Byte(), For = r.Int() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Origin); h.Add(Target); h.Add(Leader); h.Add(Size); h.Add(Motive); h.Add(State); h.Add(Formed); h.Add(Arrive); h.Add(Event); h.Add(Loot); h.Add(Called); h.Add(For); }
    }

    /// <summary>
    /// Taking by need (D2-069; F6 slice 1; 10 §2): a hungry grown person may go in the night to a neighbour's full store and take a
    /// load of grain - more readily the bolder they are and the nearer to starving, held back by kin living there and by help that
    /// village gave theirs, freed by a wrong it did them (taking back is not stealing to the taker). At the store the homes round
    /// it may wake: seen, the taker runs and leaves it, known by name; unseen, they carry the load home to their household's larder
    /// and the loss may be found in the morning. A deed and history either way. Lived: once a night, at bedtime, they walk there
    /// and back in the dark; a COARSE day makes the same choice with the same chances at once.
    /// </summary>
    public sealed class Conflict
    {
        World _w;
        public void Bind(World w) { _w = w; }

        /// <summary>A load of grain (FU) a person carries (25 kg).</summary>
        public const double LoadFu = 25.0 / 0.75;
        /// <summary>A hungry grown person's chance a night to go taking (before boldness, hunger and restraint).</summary>
        public const double TakePerNight = 0.03;
        /// <summary>Only a store holding more than this many days of its own people's need, and a load beyond, is worth the walk.</summary>
        public const double FullStoreDays = 30.0;
        /// <summary>The homes within this distance (m) of a store may wake to a taker, each by this much; never more than the cap.</summary>
        public const double WakeRange = 50.0, WakePerHome = 0.05, SeenAtMost = 0.8;
        /// <summary>A loss is found when the load was at least this share of the store.</summary>
        public const double FoundShare = 0.02;
        /// <summary>What a taker seen at the store weighs to the village that saw them, as a share of the load they came for (the
        /// intent and the fright: one seen taker estranges two villages for a year and a half - Trade.Grudge).</summary>
        public const double SeenWorth = 0.5;

        // ---- the watch, blows and injury (D2-071)
        /// <summary>A watcher at the store sees a taker this often.</summary>
        public const double WatchSeen = 0.85;
        /// <summary>A village keeps a watch when what it lost to takers in the last year comes to more a night than this (FU): a
        /// watcher's lost sleep, the hours of the next day's work it costs (a COARSE day takes them off the watcher).</summary>
        public const double WatchCost = 1.0, WatchHours = 3.0;
        /// <summary>After the shout, someone comes out this often; a taker who runs is caught this often; a taker turns on them by
        /// boldness and desperation at most this often.</summary>
        public const double ComeOut = 0.7, Caught = 0.35, TurnOn = 0.3;
        /// <summary>A wound's worth to the one struck and theirs (FU a whole hurt), a killing's; a taker struck in the act weighs half.</summary>
        public const double WoundWorth = 100.0, KillWorth = 600.0, Deserved = 0.5;
        /// <summary>Hurt at which a person keeps to their bed, and below which they may take to the road again.</summary>
        public const double AbedHurt = 0.4, RoadHurt = 0.25;
        /// <summary>Healing a day (by how well they are fed) and the daily chance of dying of deep wounds (x 4 (hurt - 0.5)^2).</summary>
        public const double HealPerDay = 0.03, DieOfWounds = 0.3;

        // ---- answers, news and taking back (D2-072)
        /// <summary>Blood worth this much (FU, by its weight now) is worth walking over to ask amends for.</summary>
        public const double ClaimFrom = 10.0;
        /// <summary>What a village knows of another's beatings of takers (hurt dealt, by age) that keeps its takers away altogether.</summary>
        public const double AnswersFull = 2.0;
        /// <summary>A grievance this heavy (FU) moves the bold to take back; their chance a night at its fullest (200 FU).</summary>
        public const double TakeBackFrom = 30.0, TakeBackPerNight = 0.01;

        // ---- raids (D2-073)
        /// <summary>A wrong this heavy (FU) may raise a band, the more readily up to RaidFull; a village's chance a day at the fullest.</summary>
        public const double RaidFrom = 150.0, RaidFull = 600.0, RaidPerDay = 0.02;
        /// <summary>A band needs this many to go and takes no more than BandAtMost.</summary>
        public const int BandAtLeast = 3, BandAtMost = 12;
        /// <summary>The most a band grows to with the men its lord's tributaries owe it (C10.1).</summary>
        public const int CallAtMost = 36;
        /// <summary>How readily a tributary's fit grown person answers the lord's call: owed, so a quarter even of the meek, more of the bold.</summary>
        public const double CallOwed = 0.25, CallBold = 0.5;
        /// <summary>The weight a lord gives the wrongs its tributary holds when it answers them (D2-108: the protection tribute buys - a
        /// tributary's wrong moves its lord only when twice as heavy as would move the tributary itself).</summary>
        public const double LordsShare = 0.5;
        /// <summary>The defenders' home ground, the strength below which a side breaks against the other, a fighter's chance to land a
        /// blow a round, a raid's fright (FU); a band goes only when it is this strong against the defence it expects.</summary>
        public const double HomeGround = 1.2, BreakAt = 0.5, HitChance = 0.5, RaidFright = 30.0, BandDares = 0.8;
        /// <summary>Each round of a fight more of the village wakes and comes: two and this share of its fit grown people.</summary>
        public const double Reinforce = 0.15;

        // ---- defence that is built (D2-074)
        /// <summary>A palisade halves what takers and bands carry off (WallGuards) for the years it stands sound (WallLife); its labour
        /// and timber are worth LabourWorth FU an hour or a wood unit; timber rots in WallYears unmended; mending is kept up while what it
        /// guards outweighs MendShare of its cost.</summary>
        public const double WallGuards = 0.5, WallLife = 15.0, LabourWorth = 0.25, WallYears = 30.0, MendShare = 0.3;
        /// <summary>A palisade sees takers this often (they must climb it or come by a gate), and doubles its defenders' ground when sound.</summary>
        public const double WallSeen = 0.6;

        // ---- tribute and lordship (D2-075)
        /// <summary>A tributary pays its lord this share of each harvest; it bows only to raiders this much stronger (fit grown people).</summary>
        public const double TributeShare = 0.1, TributeOdds = 1.2;

        // ---- bread and dependence (C10.4, D2-157)
        /// <summary>`--nobread`: no village bows for the grain it cannot give back; a lord's relief to its tributaries counts what they owe; tribute
        /// does not pay relief down (D2-075's lordship by conquest alone) - on the same binary.</summary>
        public static bool BreadOff;
        /// <summary>Days of its people's food a village still owes one giver for relief after giving back all its new harvest can spare, from which it
        /// bows to that giver: a famine's whole relief (`Trade.ReliefUpToDays`) unreturned.</summary>
        public const double BowDays = 30.0;
        /// <summary>m: a lord for bread is a neighbour within a day's walk (its call and its relief come over the land).</summary>
        public const double BreadReach = 6000.0;
        /// <summary>Tools (`--breadtally`): village-harvests with no lord by the days of food still owed to the main giver after the harvest's spare
        /// (0: none, then 0-5, 5-10, 10-20, 20-30, 30-60, 60+), [7..13] those whose giver was also stronger by `TributeOdds` and within reach; [14] bonds.</summary>
        public static long[] BreadTally;

        // ---- a lordship's might (C10.5, D2-159)
        /// <summary>`--oldmight`: villages weigh each other as D2-075 did - by their fit grown people at home that day, a lord by its own village
        /// alone - on the same binary.</summary>
        public static bool MightOff;
        /// <summary>Tools (`--mighttally`): for each weighing - [0..3] a beaten village's bowing, [4..7] bowing for bread, [8..11] a bond's end at the
        /// harvest - the weighings, those the old count (D2-075) would carry, those might carries, and both; [12] lords' weighings in which their call
        /// counted, [13] the men it added; null in the game.</summary>
        public static double[] MightTally;

        // ---- tribute carried (C10.6, D2-161)
        /// <summary>`--oldtribute`: the tenth goes into the lord's store at the tributary's harvest, carried by nobody (D2-075 to C10.5) - on the same
        /// binary.</summary>
        public static bool CarryOff;
        /// <summary>Days in which a tributary's people mean to carry the lord's tenth to its store after the harvest (the loads a day follow from it).</summary>
        public const double CarryDays = 10.0;
        /// <summary>The most loads a village sends a day: one for every this many of its fit grown people.</summary>
        public const int CarryPer = 20;
        /// <summary>Tools (`--carrytally`): [0] loads carried, [1] FU carried, [2] of them by lived carriers, [3] FU set aside, [4] FU given back to a
        /// tributary whose bond ended before its due was carried, [5] FU carried on carts, [6] carriers' km, [7] FU still set aside at the next harvest
        /// (carried by nobody in a year) and given back, [8] harvests whose tenth was not set aside - no way on foot to the lord, [9] the FU of those
        /// tenths, [10] of those harvests a way by sea between their landings, [11] the tributary with plank boats, [12] the lord with plank boats, [13] the FU
        /// of [10]'s tenths (C9.5 survey), [14] loads carried by sea (C9.5a), [15] their FU, [16] loads fetched home in the lord's boats, [17] their FU;
        /// null in the game.</summary>
        public static double[] CarryTally;
        /// <summary>The length of `CarryTally`.</summary>
        public const int CarryTallyN = 18;

        /// <summary>A village's sound plank boats (C9.2b): what could row its tenth over the water (C9.5a); tools.</summary>
        public int PlankBoats(int sid)
        {
            var bl = _w.BuildingsOf(sid);
            int n = 0;
            for (int i = 0; i < bl.Count; i++) { ref var b = ref _w.Buildings.Get(bl[i]); if (Boats.IsPlank(b.Kind) && Boats.Sound(in b, _w.Now)) n++; }
            return n;
        }

        /// <summary>A village's sound boats that could carry a load to a village this far away (m): its plank boats, and its dugouts across a short
        /// water (C9.5b; `Boats.Carries`) - what could carry its tenth over the water.</summary>
        public int CrossBoats(int sid, double far)
        {
            var bl = _w.BuildingsOf(sid);
            int n = 0;
            for (int i = 0; i < bl.Count; i++) { ref var b = ref _w.Buildings.Get(bl[i]); if (Boats.Carries(b.Kind, far) && Boats.Sound(in b, _w.Now)) n++; }
            return n;
        }

        /// <summary>m between two villages as the crow flies.</summary>
        double Far(int a, int b) => Vec3d.ArcDistance(_w.Settlements.Get(a).Anchor, _w.Settlements.Get(b).Anchor, _w.Planet.Radius);

        /// <summary>A person is hungry enough to go when the last day fed them less than this share of their need (both resolutions
        /// keep that share, the body's day: a lived meal-time flag would clear at the first bite).</summary>
        public const double HungryBelow = 0.5;

        /// <summary>Whether a person could go taking tonight: grown and fit, hungry, free of a babe and of sickness.</summary>
        public static bool CanTake(in Person p, long now)
        {
            if (!p.Alive || p.Nutrition >= HungryBelow || p.Sick != 0 || p.PregDue != 0) return false;
            if (p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) return false;
            if (p.Hurt >= RoadHurt) return false;
            double age = p.AgeYears(now);
            return age >= 16 && age < 56 && p.Health > 0.4;
        }

        /// <summary>A night's chance that a hungry person goes, by their boldness and how near they are to starving (both resolutions).</summary>
        public double TakeChance(in Person p)
        {
            var w = _w;
            double cap = AgentSystem.ReserveCap(in p, w.Seed, w.Now);
            double starving = DMath.Clamp01(1.0 - p.Reserve / cap);
            return TakePerNight * (0.4 + 1.2 * p.Bold / 255.0) * (0.5 + starving);
        }

        /// <summary>
        /// The neighbour within a day's walk whose store a hungry person would go to, and how freely (0..1: their restraint and the
        /// chance of being seen held against it), or 0 when no store is full enough to be worth it.
        /// </summary>
        public int TakeTarget(in Person p, out double free)
        {
            var w = _w;
            free = 0;
            int home = p.Home, best = 0;
            if (!w.Settlements.Exists(home)) return 0;
            var anchor = w.Settlements.Get(home).Anchor;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == home) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || Vec3d.ArcDistance(anchor, t.Anchor, w.Planet.Radius) > 6000) continue;
                Trade.DaysOfFood(w, in t, out double daily);
                if (t.StoreSeen(w.Now) < FullStoreDays * daily + LoadFu) continue;   // (the store as days lived hold it: F8)
                if (w.Routes.Get(w, home, o) == null) continue;
                double f = (1.0 - Restraint(in p, o)) * (1.0 - SeenAt(o)) * (1.0 - DMath.Clamp01(w.DeedBook.Answers(home, o) / AnswersFull));
                if (f > free) { free = f; best = o; }
            }
            return best;
        }

        /// <summary>
        /// What holds a person back from taking from a village (0..1): kin living there; help it gave their village; less the
        /// wrong it did their village.
        /// </summary>
        public double Restraint(in Person p, int o)
        {
            var w = _w;
            double r = KinIn(in p, o) ? 0.85 : 0.0;
            r += 0.5 * DMath.Clamp01(w.DeedBook.Gratitude(p.Home, o) / 100.0);
            r -= DMath.Clamp01(w.DeedBook.Grievance(p.Home, o) / 30.0);
            return DMath.Clamp01(r);
        }

        /// <summary>Whether a person's parents, partner, children or brothers and sisters live in a village.</summary>
        public bool KinIn(in Person p, int o)
        {
            var w = _w;
            if (LivesIn(p.Mother, o) || LivesIn(p.Father, o) || LivesIn(p.Partner, o)) return true;
            var res = w.Residents(o);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive) continue;
                if (q.Mother == p.Id || q.Father == p.Id) return true;
                if (p.Mother != 0 && q.Mother == p.Mother) return true;
            }
            return false;
        }

        bool LivesIn(int pid, int o)
        {
            if (pid == 0 || !_w.People.Exists(pid)) return false;
            ref var q = ref _w.People.Get(pid);
            return q.Alive && q.Home == o;
        }

        /// <summary>The chance a taker at a village's store in the night is seen: the homes round the store, whose sleepers may wake.</summary>
        public double SeenChance(int o)
        {
            var w = _w;
            ref var t = ref w.Settlements.Get(o);
            Construction.GranarySpot(in t, out double gx, out double gy);
            var hhs = w.HouseholdsOf(o);
            int near = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                double dx = hh.HomeX - gx, dy = hh.HomeY - gy;
                if (dx * dx + dy * dy <= WakeRange * WakeRange) near++;
            }
            return DMath.Min(SeenAtMost, 1.0 - DMath.Exp(-WakePerHome * near));
        }

        /// <summary>The chance a taker at a village's store tonight is seen: its sleepers, or its watch (D2-071).</summary>
        public double SeenAt(int o)
        {
            ref var s = ref _w.Settlements.Get(o);
            double seen = SeenChance(o);
            if (Walled(in s, out double wc)) seen = DMath.Max(seen, WallSeen * wc);   // (over a palisade, or through its gate: D2-074)
            return s.Watcher != 0 ? DMath.Max(WatchSeen, seen) : seen;
        }

        /// <summary>The defenders' ground: their own, and behind a sound palisade the more (D2-074).</summary>
        public double Ground(int o)
        {
            ref var s = ref _w.Settlements.Get(o);
            return HomeGround * (Walled(in s, out double wc) ? 1.0 + wc : 1.0);
        }

        /// <summary>
        /// A village behind a palisade: its standing ring, or while a larger one is being raised, the old ring still standing inside (C7.1);
        /// how sound it is.
        /// </summary>
        public static bool Walled(in Settlement s, out double cond)
        {
            if (s.Wall == 2) { cond = s.WallCond; return true; }
            if (s.Wall == 1 && s.OldR > 0f && s.OldCond > 0f) { cond = s.OldCond; return true; }
            cond = 0; return false;
        }

        /// <summary>A walled village's homes beyond its ring (lived in), and all its lived-in homes.</summary>
        int Outside(int sid, out int homes)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            int outside = 0; homes = 0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (!Places.Structures.IsHome(b.Kind) || b.Household == 0) continue;
                homes++;
                double dx = (double)b.X - s.SquareX, dy = (double)b.Y - s.SquareY;
                if (DMath.Sqrt(dx * dx + dy * dy) > (double)s.WallR + 6.0) outside++;
            }
            return outside;
        }

        /// <summary>Homes beyond a sound palisade from which a town weighs a larger ring (at least this many, and a fifth of its homes).</summary>
        public const int OutgrownFrom = 4;

        /// <summary>The taker at the store in the night (lived: on arriving; COARSE: at once).</summary>
        public void Take(ref Person p, int at) => TakeWith(ref p, at, KeyedRng.Unit(_w.Seed, RngStream.Society, (ulong)p.Id, (ulong)_w.Now, 1));

        /// <summary>The same, with the roll against the chance of being seen given (tests).</summary>
        public void TakeWith(ref Person p, int at, double roll)
        {
            var w = _w;
            long now = w.Now;
            int home = p.Home;
            if (!w.Settlements.Exists(at) || !w.Settlements.Exists(home)) return;
            ref var t = ref w.Settlements.Get(at);
            double load = DMath.Min(LoadFu, t.Granary);
            if (load < 1.0) return;
            long cause = p.Nutrition < HungryBelow ? HungerCause(home, now) : w.Settlements.Get(home).FoeEvent;   // (hunger, or a wrong taken back: D2-072)
            bool watched = t.Watcher != 0;
            if (roll < SeenAt(at))
            {
                // seen: someone woke and called out; they run and leave it, and the village knows who it was
                long ev = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.TakingSeen, Time = now, Cell = t.Cell, Settlement = at, Subject = EntityRef.Person(p.Id),
                    Object = EntityRef.Settlement(at), Other = EntityRef.Settlement(home), Cause1 = cause, Rel1 = CauseRel.InResponseTo,
                    Magnitude = (float)load, RootKind = RootKind.People,
                });
                int d = w.DeedBook.Record(DeedKind.TriedToTake, p.Id, home, 0, at, Trade.Grain, 0.0, SeenWorth * load, ev);
                w.DeedBook.Learn(at, d, KnownHow.Saw);
                w.DeedBook.Learn(home, d, KnownHow.Own);
                w.Metrics.TakingsSeen++;
                Confront(ref p, at, watched, ev);   // (and what follows the shout: D2-071)
                return;
            }
            ref var t2 = ref w.Settlements.Get(at);
            t2.Granary -= load;
            p.Cargo = Trade.Grain; p.CargoQty = (float)load; p.CargoTo = Trade.ToLarder;
            long e2 = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.Taken, Time = now, Cell = t2.Cell, Settlement = at, Subject = EntityRef.Person(p.Id),
                Object = EntityRef.Settlement(at), Other = EntityRef.Settlement(home), Cause1 = cause, Rel1 = CauseRel.InResponseTo,
                Magnitude = (float)load, RootKind = RootKind.People,
            });
            int d2 = w.DeedBook.Record(DeedKind.Take, p.Id, home, 0, at, Trade.Grain, load, load, e2);
            w.DeedBook.Learn(home, d2, KnownHow.Own);
            if (load >= FoundShare * (t2.Granary + load)) w.DeedBook.Learn(at, d2, KnownHow.Found);   // (the store visibly lower in the morning)
            w.Metrics.Takings++;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Taking, home, at);
            w.Metrics.TakenFu += load;
            if (p.Nutrition >= HungryBelow) w.Metrics.TakenBack++;
        }

        // ------------------------------------------------------------------ what follows the shout (D2-071)
        /// <summary>
        /// A taker seen at the store: the woken - or the watcher - may come out; the taker runs, or turns on them when bold and
        /// desperate; a defender who catches them strikes. Who is hurt goes by strength, by the neighbours who come and by the
        /// metal they hold. Both resolutions.
        /// </summary>
        void Confront(ref Person taker, int at, bool watched, long seen)
        {
            var w = _w;
            long now = w.Now;
            ulong seed = w.Seed;
            ulong key = (ulong)taker.Id;
            if (KeyedRng.Unit(seed, RngStream.Society, key, (ulong)now, 4) >= (watched ? 1.0 : ComeOut)) return;   // nobody came out
            int defender = Defender(at, now, watched);
            if (defender == 0) return;
            double cap = AgentSystem.ReserveCap(in taker, seed, now);
            double desperate = DMath.Clamp01(1.0 - taker.Reserve / cap);
            bool turns = KeyedRng.Unit(seed, RngStream.Society, key, (ulong)now, 5) < TurnOn * (taker.Bold / 255.0) * (0.5 + desperate);
            if (!turns && KeyedRng.Unit(seed, RngStream.Society, key, (ulong)now, 6) >= Caught) return;   // they got away in the dark
            ref var d = ref w.People.Get(defender);
            double st = Strength(in taker), sd = Strength(in d) * (1.0 + 0.5 * Helpers(at));
            bool takerDown = KeyedRng.Unit(seed, RngStream.Society, key, (ulong)now, 7) < sd / (sd + st);
            double hard = 0.3 + 0.6 * KeyedRng.Unit(seed, RngStream.Society, key, (ulong)now, 8);
            double light = 0.15 * KeyedRng.Unit(seed, RngStream.Society, key, (ulong)now, 9);
            if (takerDown)
            {
                Strike(ref d, ref taker, hard, at, true, seen);
                if (light > 0.02) Strike(ref taker, ref d, light, at, false, seen);
            }
            else
            {
                Strike(ref taker, ref d, hard, at, false, seen);
                if (light > 0.02) Strike(ref d, ref taker, light, at, true, seen);
            }
        }

        /// <summary>Who comes out: the watcher, else the boldest fit grown person of the village (keyed by the night).</summary>
        int Defender(int at, long now, bool watched)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(at);
            if (watched && w.People.Exists(s.Watcher) && w.People.Get(s.Watcher).Alive && w.People.Get(s.Watcher).Home == at) return s.Watcher;
            var res = w.Residents(at);
            int best = 0; double bv = -1;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!Fit(in q, now) || q.At != at) continue;
                double v = q.Bold / 255.0 + 0.5 * KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)res[i], (ulong)now, 10);
                if (v > bv) { bv = v; best = res[i]; }
            }
            return best;
        }

        /// <summary>Grown, fit and free: one who could keep a watch or come out to a shout.</summary>
        public static bool Fit(in Person q, long now)
        {
            if (!q.Alive || q.Sick != 0 || q.Hurt >= RoadHurt || q.Health <= 0.6) return false;
            double age = q.AgeYears(now);
            return age >= 16 && age < 60;
        }

        /// <summary>A body's strength in a struggle: its health and years, its hurt, and the metal its village holds (knives, axes).</summary>
        public double Strength(in Person q)
        {
            var w = _w;
            double age = q.AgeYears(w.Now);
            double years = age < 18 ? 0.8 : age < 45 ? 1.0 : age < 60 ? 0.8 : 0.6;
            double armed = 0.0, arms = 0.0, iron = 0.0;
            if (w.Settlements.Exists(q.Home))
            {
                ref var hs = ref w.Settlements.Get(q.Home);
                armed = Knowledge.Tech.ToolShare(in hs);
                arms = hs.Population > 0 ? DMath.Clamp01(hs.Arms / (Places.Forge.ArmsPerHead * hs.Population)) : 0.0;   // (a spear or an axe for its fighters: C1.4)
                iron = hs.Population > 0 ? DMath.Clamp01(hs.IronArms / (Places.Forge.ArmsPerHead * hs.Population)) : 0.0;   // (of iron: C5.2b)
            }
            return (double)q.Health * years * (1.0 - (double)q.Hurt) * (1.0 + 0.3 * armed) * (1.0 + 0.3 * arms) * (1.0 + 0.15 * iron) * (1.0 + 0.6 * (double)q.SkFight);   // (trained: C1.5; iron: C5.2b)
        }

        /// <summary>The neighbours who come running at a shout: a third of the homes round the store, at most three.</summary>
        int Helpers(int at)
        {
            var w = _w;
            ref var t = ref w.Settlements.Get(at);
            Construction.GranarySpot(in t, out double gx, out double gy);
            var hhs = w.HouseholdsOf(at);
            int near = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                double dx = hh.HomeX - gx, dy = hh.HomeY - gy;
                if (dx * dx + dy * dy <= WakeRange * WakeRange) near++;
            }
            return near / 3 < 3 ? near / 3 : 3;
        }

        /// <summary>A blow: hurt on the one struck, a wound the deed of the striker, known to both villages; history tells it.</summary>
        void Strike(ref Person by, ref Person to, double hurt, int at, bool taking, long cause)
        {
            var w = _w;
            to.Hurt = (float)DMath.Min(1.0, (double)to.Hurt + hurt);
            Places.Training.Veteran(ref by); Places.Training.Veteran(ref to);   // (a blow given or taken teaches: C1.5)
            ref var s = ref w.Settlements.Get(at);
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.Struck, Time = w.Now, Cell = s.Cell, Settlement = at, Subject = EntityRef.Person(by.Id), Object = EntityRef.Person(to.Id),
                Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)hurt, ArgA = taking ? 1 : 0, RootKind = RootKind.People,
            });
            int deed = w.DeedBook.Record(DeedKind.Wound, by.Id, by.Home, to.Household, to.Home, 0, hurt, (taking ? Deserved : 1.0) * WoundWorth * hurt, ev);
            if (taking) { ref var wd = ref w.Deeds.Get(deed); wd.Flags |= DeedKind.InTheAct; }
            w.DeedBook.Learn(to.Home, deed, KnownHow.Saw);
            w.DeedBook.Learn(by.Home, deed, KnownHow.Own);
            to.HurtDeed = deed;
            w.Metrics.Blows++;
        }

        // ------------------------------------------------------------------ wounds, healing and the dead (D2-071; both resolutions)
        readonly System.Collections.Generic.List<int> _hurt = new System.Collections.Generic.List<int>();

        /// <summary>A village's day of wounds: they heal, faster fed; deep ones may kill - the death a killing, the striker's deed.</summary>
        public void Wounds(int sid)
        {
            var w = _w;
            long now = w.Now;
            var res = w.Residents(sid);
            _hurt.Clear();
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (q.Alive && q.Hurt > 0f) _hurt.Add(res[i]); }
            for (int i = 0; i < _hurt.Count; i++)
            {
                int pid = _hurt[i];
                ref var p = ref w.People.Get(pid);
                double hurt = p.Hurt;
                double over = hurt - 0.5;
                if (over > 0 && KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 20) < DieOfWounds * 4.0 * over * over)
                {
                    long cause = 0;
                    int wound = p.HurtDeed;
                    if (wound != 0 && w.Deeds.Exists(wound))
                    {
                        ref var d = ref w.Deeds.Get(wound);
                        cause = d.Event;
                        // (a killing weighs as the wound did - half when they were struck taking)
                        double share = d.Qty > 0f ? (double)d.Worth / (WoundWorth * (double)d.Qty) : 1.0;
                        int by = d.By, byS = d.ByS;
                        byte flags = (byte)(d.Flags & DeedKind.InTheAct);
                        int kill = w.DeedBook.Record(DeedKind.Kill, by, byS, p.Household, sid, 0, 1.0, share * KillWorth, cause);
                        { ref var kd = ref w.Deeds.Get(kill); kd.Flags = flags; }
                        w.DeedBook.Learn(sid, kill, KnownHow.Saw);
                        w.DeedBook.Learn(byS, kill, KnownHow.Own);
                    }
                    ref var p2 = ref w.People.Get(pid);
                    p2.Hurt = 0f; p2.HurtDeed = 0;
                    w.Agents.Kill(ref p2, cause, 11);
                    w.Metrics.WoundDeaths++;
                    continue;
                }
                p.Hurt = (float)DMath.Max(0.0, hurt - HealPerDay * (0.5 + (double)p.Nutrition));
                if (p.Hurt <= 0f) p.HurtDeed = 0;
            }
        }

        // ------------------------------------------------------------------ the watch (D2-071; both resolutions)
        /// <summary>
        /// A village's choice of a watch at its store tonight: kept when what it lost to takers in the last year (the harms it
        /// knows were done to it, the doer known or not) comes to more a night than a watcher's lost sleep; the watcher the boldest
        /// fit grown person by turns. History tells when a watch begins.
        /// </summary>
        public void Watch(int sid)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            double lost = w.DeedBook.HarmSince(sid, now - SimTime.Year, out long latest);
            if (lost / 48.0 <= WatchCost) { s.Watcher = 0; s.WatchEvent = 0; return; }
            var res = w.Residents(sid);
            int best = 0; double bv = -1;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!Fit(in q, now) || q.At != sid || q.PregDue != 0 || (q.NursingUntil != 0 && q.NursingUntil - now > SimTime.Year / 2)) continue;
                double v = q.Bold / 255.0 + KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)res[i], (ulong)now, 11);
                if (v > bv) { bv = v; best = res[i]; }
            }
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Watcher = best;
            if (best == 0) return;
            w.Metrics.WatchNights++;
            if (s2.WatchEvent == 0)
                s2.WatchEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.WatchSet, Time = now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Cause1 = latest, Rel1 = CauseRel.InResponseTo, Magnitude = (float)lost, RootKind = RootKind.People,
                });
        }


        // ------------------------------------------------------------------ answers: amends asked, made or refused (D2-072)
        /// <summary>
        /// A village's claims (once a day, both resolutions): the newest unanswered blood its people suffered from known doers -
        /// not struck taking - worth asking amends for. Someone of the wronged household (else of the village) goes to ask: lived,
        /// the next morning (their errand); COARSE, at once.
        /// </summary>
        void Claims(int sid, int days = 1)
        {
            var w = _w;
            var list = w.DeedBook.KnownBy(sid);
            if (list == null) return;
            long now = w.Now;
            int asked = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var k = list[i];
                ref var d = ref w.Deeds.Get(k.Deed);
                if (d.Time < now - SimTime.Year) break;   // (a year's wrongs: ids rise with time)
                if (k.How != KnownHow.Saw && k.How != KnownHow.Told) continue;
                if (!DeedKind.Blood(d.Kind) || d.ToS != sid || d.Answer != 0 || (d.Flags & (DeedKind.InTheAct | DeedKind.Claimed)) != 0) continue;
                if (!w.Settlements.Exists(d.ByS) || !w.Settlements.Get(d.ByS).Inhabited || d.ByS == sid) continue;
                if (w.DeedBook.Weight(in d, now) < ClaimFrom) continue;
                int claimant = Claimant(sid, d.ToHh, now);
                if (claimant == 0) return;
                int deed = d.Id, doers = d.ByS;
                d.Flags |= DeedKind.Claimed;
                w.Metrics.Claims++;
                Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Claim, sid, doers);
                ref var c = ref w.People.Get(claimant);
                if (w.Resolution == Resolution.Coarse) Answer(ref c, doers, deed);
                else c.Errand = deed;
                if (++asked >= days) return;   // (a claim a day: DEEP's step asks up to one for each of its days)
            }
        }

        /// <summary>Who goes to ask: a fit grown member of the wronged household, else the boldest fit grown person of the village.</summary>
        int Claimant(int sid, int hh, long now)
        {
            var w = _w;
            if (hh != 0 && w.Households.Exists(hh))
            {
                var mem = w.Members(hh);
                for (int i = 0; i < mem.Count; i++) { ref var q = ref w.People.Get(mem[i]); if (Fit(in q, now) && q.Home == sid && q.Errand == 0) return mem[i]; }
            }
            var res = w.Residents(sid);
            int best = 0; double bv = -1;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!Fit(in q, now) || q.Errand != 0) continue;
                if (q.Bold > bv) { bv = q.Bold; best = res[i]; }
            }
            return best;
        }

        /// <summary>
        /// The doers' village answers a claim (D2-072; Gluckman's ties, the evidence, their means and their own grievances): they
        /// make amends in grain from their store when they can spare it and are willing - the wrong is settled - or they refuse
        /// (when the claim rests on hearsay, they may deny it): a new wrong. Either way the talk carries news.
        /// </summary>
        public void Answer(ref Person claimant, int at, int deedId)
        {
            var w = _w;
            long now = w.Now;
            if (deedId == 0 || !w.Deeds.Exists(deedId)) return;
            ref var d = ref w.Deeds.Get(deedId);
            if (d.Answer != 0 || d.ByS != at || !w.Settlements.Exists(at)) return;
            int from = d.ToS;
            double worth = w.DeedBook.Weight(in d, now);
            w.DeedBook.Knows(from, deedId, out byte how);
            double evidence = how == KnownHow.Saw ? 1.0 : 0.6;
            double ties = (KinAcross(at, from) ? 0.4 : 0.0) + 0.3 * DMath.Clamp01(w.DeedBook.Gratitude(at, from) / 100.0);
            double counter = DMath.Clamp01(w.DeedBook.Grievance(at, from) / 50.0);
            double willing = 0.2 + 0.3 * evidence + ties - 0.6 * counter;
            double spare = w.Trade.Spare(at, Trade.Grain);
            double u = KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)deedId, (ulong)now, 30);
            ref var s = ref w.Settlements.Get(at);
            byte kind = d.Kind;
            long cause = d.Event;
            int hh = d.ToHh;
            int answer;
            if (spare >= worth && u < willing)
            {
                s.Granary -= worth;
                ref var f = ref w.Settlements.Get(from);
                f.Granary += worth;
                long ev = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.AmendsMade, Time = now, Cell = s.Cell, Settlement = at, Subject = EntityRef.Settlement(at), Object = EntityRef.Settlement(from),
                    Other = EntityRef.Person(claimant.Id), Cause1 = cause, Rel1 = CauseRel.InResponseTo, Magnitude = (float)worth, ArgA = kind, RootKind = RootKind.People,
                });
                answer = w.DeedBook.Record(DeedKind.Amends, 0, at, hh, from, Trade.Grain, worth, worth, ev);
                w.Metrics.Amends++;
            }
            else
            {
                bool deny = evidence < 1.0 && u < willing + 0.3;
                long ev = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.ClaimRefused, Time = now, Cell = s.Cell, Settlement = at, Subject = EntityRef.Settlement(at), Object = EntityRef.Settlement(from),
                    Other = EntityRef.Person(claimant.Id), Cause1 = cause, Rel1 = CauseRel.InResponseTo, Magnitude = (float)worth, ArgA = deny ? 1 : 0, ArgB = kind,
                    RootKind = RootKind.People,
                });
                answer = w.DeedBook.Record(DeedKind.Refusal, 0, at, hh, from, 0, 0.0, (deny ? 0.25 : 0.5) * worth, ev);
                w.Metrics.Refusals++;
            }
            w.DeedBook.Answered(deedId, answer);
            w.DeedBook.Learn(from, answer, KnownHow.Saw);
            w.DeedBook.Learn(at, answer, KnownHow.Own);
            w.DeedBook.Meet(from, at);
        }

        /// <summary>Whether people of two villages are close kin (parents, partners, children, brothers and sisters across the line).</summary>
        public bool KinAcross(int a, int b)
        {
            var w = _w;
            var ra = w.Residents(a);
            for (int i = 0; i < ra.Count; i++)
            {
                ref var q = ref w.People.Get(ra[i]);
                if (q.Alive && (LivesIn(q.Mother, b) || LivesIn(q.Father, b) || LivesIn(q.Partner, b))) return true;
            }
            var rb = w.Residents(b);
            for (int i = 0; i < rb.Count; i++)
            {
                ref var q = ref w.People.Get(rb[i]);
                if (q.Alive && (LivesIn(q.Mother, a) || LivesIn(q.Father, a))) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ taking back (D2-072)
        /// <summary>
        /// A night's chance that a fed person goes to take back from the village theirs holds most against (both resolutions): by
        /// boldness and the weight of the wrong; taking back is not stealing to the taker (restraint is lifted by the wrong).
        /// </summary>
        public double TakeBackChance(in Person p, in Settlement home)
        {
            if (home.Foe == 0 || home.FoeWorth < TakeBackFrom) return 0;
            return TakeBackPerNight * (0.4 + 1.2 * p.Bold / 255.0) * DMath.Clamp01((double)home.FoeWorth / 200.0);
        }

        /// <summary>Whether a person could go taking back tonight: grown, fit, free, not hungry (hunger has its own way).</summary>
        public static bool CanTakeBack(in Person p, long now)
        {
            if (!p.Alive || p.Nutrition < HungryBelow || p.Sick != 0 || p.PregDue != 0 || p.Hurt >= RoadHurt) return false;
            if (p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) return false;
            double age = p.AgeYears(now);
            return age >= 16 && age < 50 && p.Health > 0.6;
        }

        /// <summary>The foe's store if it is a walk away, has grain to take and is worth the risk; how freely (else 0).</summary>
        public int TakeBackTarget(in Person p, in Settlement home, out double free)
        {
            var w = _w;
            free = 0;
            int o = home.Foe;
            if (o == 0 || !w.Settlements.Exists(o)) return 0;
            ref var t = ref w.Settlements.Get(o);
            if (!t.Inhabited || t.StoreSeen(w.Now) < LoadFu || Vec3d.ArcDistance(home.Anchor, t.Anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, p.Home, o) == null) return 0;
            free = (1.0 - Restraint(in p, o)) * (1.0 - SeenAt(o)) * (1.0 - DMath.Clamp01(w.DeedBook.Answers(p.Home, o) / AnswersFull));
            return free > 0 ? o : 0;
        }

        /// <summary>The village's daily reading of its foe (both resolutions): who it holds most against, how much, the latest wrong.</summary>
        void ReadFoe(int sid)
        {
            var w = _w;
            int foe = w.DeedBook.Foe(sid, out double worst);
            ref var s = ref w.Settlements.Get(sid);
            s.Foe = foe; s.FoeWorth = (float)worst;
            s.FoeEvent = foe != 0 ? w.DeedBook.LatestHarm(sid, foe) : 0;
        }

        // ------------------------------------------------------------------ news at COARSE resolution (D2-072)
        /// <summary>The day's visits between a village and its neighbours (as many as a lived day makes) carry the news.</summary>
        void News(int sid, int days = 1)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            int adults = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (Fit(in q, now)) adults++; }
            if (adults == 0) return;
            int near = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                if (o != sid && w.Settlements.Get(o).Inhabited && Vec3d.ArcDistance(s.Anchor, w.Settlements.Get(o).Anchor, w.Planet.Radius) <= 6000) near++;
            if (near == 0) return;
            double expect = adults * TC2.Core.Health.Disease.VisitsPerAdultDay * days / near;   // (DEEP: the step's visits)
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid || !w.Settlements.Get(o).Inhabited) continue;
                ref var t = ref w.Settlements.Get(o);
                if (Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > 6000) continue;
                if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)sid, (ulong)now, (ulong)(40 + o)) < 1.0 - DMath.Exp(-expect))
                {
                    w.DeedBook.Meet(sid, o);
                    if (TC2.Core.Health.Disease.VisitTally != null) TC2.Core.Health.Disease.VisitTally[w.Routes.Get(w, sid, o) != null ? 4 : 5] += 1;   // (tools: C9.5c survey)
                }
            }
        }


        // ------------------------------------------------------------------ raids (D2-073)
        readonly System.Collections.Generic.List<int> _band = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.List<int> _defend = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.List<int> _callers = new System.Collections.Generic.List<int>();   // tributary, men answering: pairs (C10.1)
        readonly System.Collections.Generic.List<int> _members = new System.Collections.Generic.List<int>();

        /// <summary>Who may be of a band: its village's people, and when it called its tributaries, theirs (C10.1).</summary>
        void Members(in Band b, System.Collections.Generic.List<int> into)
        {
            var w = _w;
            into.Clear();
            var res = w.Residents(b.Origin);
            for (int i = 0; i < res.Count; i++) into.Add(res[i]);
            if (b.Called == 0) return;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == b.Origin || w.Settlements.Get(o).Lord != b.Origin) continue;
                var tres = w.Residents(o);
                for (int i = 0; i < tres.Count; i++) into.Add(tres[i]);
            }
        }

        /// <summary>
        /// A village's day may raise a band (both resolutions): when it holds a heavy wrong against its foe, or its grown people
        /// starve beside a full store a walk away. Its boldest fit person leads; each fit grown person decides to go - by boldness,
        /// by kinship with the leader, never against a village where they have close kin. Three or more go: lived, the next night
        /// (they leave at bedtime and come to the village together at one in the morning); COARSE, at once.
        /// </summary>
        /// <summary>Premise (tests, tools): the village's day raises a band now if it would (its motive, its people's choices, its daring),
        /// without the day's chance. Returns the band, or 0.</summary>
        public int RaiseBandNow(int sid) { long n0 = _w.Metrics.Bands; Raids(sid, true); return _w.Metrics.Bands > n0 ? _w.Ids.Peek(EntityKind.Band) - 1 : 0; }

        void Raids(int sid, bool force = false, int days = 1)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            for (int b = w.Bands.First(); b != 0; b = w.Bands.Next(b)) if (w.Bands.Get(b).Origin == sid) return;   // (one band at a time)
            // the motive: a heavy wrong, or hunger
            var res = w.Residents(sid);
            int fit = 0, hungry = 0;
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (!Fit(in q, now) || q.AgeYears(now) >= 50) continue; fit++; if (q.Nutrition < HungryBelow) hungry++; }
            if (fit < BandAtLeast) return;
            int target = 0, forT = 0; byte motive = 0;
            double urge = 0;
            if (s.Foe != 0 && s.FoeWorth >= RaidFrom && LordOf(s.Foe) != sid) { target = s.Foe; motive = 1; urge = DMath.Clamp01((double)s.FoeWorth / RaidFull); }
            // the lord answers for its tributaries (D2-108): the heaviest wrong one of them holds, weighed at the lord's share, when heavier
            // than its own - never against another of its tributaries, never against itself
            double heaviest = motive == 1 ? (double)s.FoeWorth : 0.0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var tr = ref w.Settlements.Get(o);
                if (tr.Lord != sid || !tr.Inhabited || tr.Foe == 0 || tr.Foe == sid || LordOf(tr.Foe) == sid) continue;
                double taken = LordsShare * tr.FoeWorth;
                if (taken < RaidFrom || taken <= heaviest) continue;
                heaviest = taken; target = tr.Foe; forT = o; motive = 2; urge = DMath.Clamp01(taken / RaidFull);
            }
            if (motive == 0 && hungry * 2 >= fit)
            {
                int leader0 = Boldest(sid, now, 0);
                if (leader0 != 0) { target = TakeTarget(in w.People.Get(leader0), out double free); urge = (double)hungry / fit * free; }
            }
            if (target == 0 || urge <= 0) return;
            // (a village whose takers and bands that target beat back thinks twice: D2-073)
            urge *= 1.0 - DMath.Clamp01(w.DeedBook.Answers(sid, target) / AnswersFull);
            if (!force && KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)sid, (ulong)now, 50) >= Places.SettlementSystem.Over(RaidPerDay * urge, days)) return;
            ref var t = ref w.Settlements.Get(target);
            if (!t.Inhabited || Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, sid, target) == null) return;
            int leader = Boldest(sid, now, target);
            if (leader == 0) return;
            // who goes with them: each decides
            _band.Clear();
            _band.Add(leader);
            ref var L = ref w.People.Get(leader);
            for (int i = 0; i < res.Count && _band.Count < BandAtMost; i++)
            {
                int pid = res[i];
                if (pid == leader) continue;
                ref var q = ref w.People.Get(pid);
                if (!Fit(in q, now) || q.AgeYears(now) >= 50 || q.PregDue != 0 || (q.NursingUntil != 0 && q.NursingUntil - now > SimTime.Year / 2) || q.Band != 0) continue;
                if (KinIn(in q, target)) continue;   // (not against their own kin)
                bool kin = q.Mother == leader || q.Father == leader || L.Mother == pid || L.Father == pid || q.Partner == leader || (q.Mother != 0 && q.Mother == L.Mother);
                double join = (0.15 + 0.7 * q.Bold / 255.0) * (kin ? 1.5 : 1.0) * (motive != 0 ? 1.0 : (q.Nutrition < HungryBelow ? 1.0 : 0.3));
                if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 51) < join) _band.Add(pid);
            }
            // the lord's call (C10.1): against those who wronged it, a lord's tributaries owe it men - each fit grown person of a tributary
            // within a day's walk of the foe goes as owed (a quarter even of the meek, more of the bold), never against their own kin
            _callers.Clear();
            if (motive != 0)
                for (int o = w.Settlements.First(); o != 0 && _band.Count < CallAtMost; o = w.Settlements.Next(o))
                {
                    ref var tr = ref w.Settlements.Get(o);
                    if (o == target || o == sid || tr.Lord != sid || !tr.Inhabited) continue;
                    if (Vec3d.ArcDistance(tr.Anchor, w.Settlements.Get(target).Anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, o, target) == null) continue;
                    int before = _band.Count;
                    var tres = w.Residents(o);
                    for (int i = 0; i < tres.Count && _band.Count < CallAtMost; i++)
                    {
                        int pid = tres[i];
                        ref var q = ref w.People.Get(pid);
                        if (!Fit(in q, now) || q.AgeYears(now) >= 50 || q.PregDue != 0 || (q.NursingUntil != 0 && q.NursingUntil - now > SimTime.Year / 2) || q.Band != 0) continue;
                        if (KinIn(in q, target)) continue;
                        if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 52) < CallOwed + CallBold * q.Bold / 255.0) _band.Add(pid);
                    }
                    if (_band.Count > before) { _callers.Add(o); _callers.Add(_band.Count - before); }
                }
            if (_band.Count < BandAtLeast) return;
            // too few against the defence they expect (the neighbours round the store and its watch, on their own ground): they stay
            double bandStrength = 0;
            for (int i = 0; i < _band.Count; i++) bandStrength += Strength(in w.People.Get(_band[i]));
            // (they know the village: its neighbours round the store, its watch, and how many more would wake)
            double expect = (1 + Helpers(target) + (w.Settlements.Get(target).Watcher != 0 ? 1 : 0) + 2 + Reinforce * FitAt(target, now)) * 0.8 * Ground(target);
            if (bandStrength < BandDares * expect) return;
            // the band
            int id = w.Ids.Next(EntityKind.Band);
            long off = w.LocalOffsetOf(sid);
            long arrive = SimTime.StartOfDay(now + off) + SimTime.Day + SimTime.Hours(1) - off;   // (one in the morning, the coming night)
            long cause = motive == 1 ? s.FoeEvent : motive == 2 ? w.Settlements.Get(forT).FoeEvent : HungerCause(sid, now);
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.BandRaised, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(leader), Object = EntityRef.Settlement(target),
                Other = forT != 0 ? EntityRef.Settlement(forT) : default, Cause1 = cause, Rel1 = CauseRel.InResponseTo, ArgA = _band.Count, ArgB = motive, RootKind = RootKind.People,
            });
            w.Bands.Add(id, new Band { Id = id, Origin = sid, Target = target, Leader = leader, Size = (byte)_band.Count, Motive = motive, Formed = now, Arrive = arrive, Event = ev, Called = (byte)(_callers.Count / 2), For = forT });
            for (int i = 0; i < _band.Count; i++) w.People.Get(_band[i]).Band = id;
            w.Metrics.Bands++;
            if (motive == 2) w.Metrics.LordsAnswered++;
            for (int k = 0; k < _callers.Count; k += 2)
            {
                // each tributary whose people answered: history, caused by the band's raising
                ref var tc = ref w.Settlements.Get(_callers[k]);
                w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.CallAnswered, Time = now, Cell = tc.Cell, Settlement = _callers[k], Subject = EntityRef.Settlement(_callers[k]),
                    Object = EntityRef.Settlement(sid), Other = EntityRef.Settlement(target), ArgA = _callers[k + 1], Cause1 = ev, Rel1 = CauseRel.InResponseTo,
                    RootKind = RootKind.People,
                });
                w.Metrics.CalledMen += _callers[k + 1];
            }
            if (_callers.Count > 0) w.Metrics.CalledBands++;
            if (w.Resolution == Resolution.Coarse) Fall(id, _band);
        }

        /// <summary>Premise (tests, tools): a band of these people of a village falls on another's store now (seen, or as the night goes).</summary>
        public void FallNow(int origin, int target, System.Collections.Generic.List<int> raiders, bool seen)
        {
            var w = _w;
            int id = w.Ids.Next(EntityKind.Band);
            w.Bands.Add(id, new Band { Id = id, Origin = origin, Target = target, Leader = raiders[0], Size = (byte)raiders.Count, Motive = 1, Formed = w.Now, Arrive = w.Now });
            for (int i = 0; i < raiders.Count; i++) w.People.Get(raiders[i]).Band = id;
            _forceSeen = seen ? 1 : 0;
            w.Ways.Walk(w, origin, target, 2.0 * raiders.Count);   // (the band walked there and back: C4.1)
            var list = new System.Collections.Generic.List<int>(raiders);
            Fall(id, list);
            _forceSeen = -1;
            for (int i = 0; i < raiders.Count; i++) { ref var q = ref w.People.Get(raiders[i]); if (q.CargoQty > 0f) w.Trade.Unload(ref q, origin); q.Band = 0; }
            if (w.Bands.Exists(id)) w.Bands.Remove(id);
        }
        int _forceSeen = -1;

        /// <summary>The boldest fit grown person of a village (who would lead a band), without close kin in the target.</summary>
        int Boldest(int sid, long now, int target)
        {
            var w = _w;
            var res = w.Residents(sid);
            int best = 0; double bv = -1;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!Fit(in q, now) || q.AgeYears(now) >= 50 || q.Band != 0) continue;
                if (target != 0 && KinIn(in q, target)) continue;
                double v = q.Bold / 255.0 + 0.2 * KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)res[i], (ulong)now, 52);
                if (v > bv) { bv = v; best = res[i]; }
            }
            return best;
        }

        /// <summary>Lived: a band's member arrives at the target; the first to come resolves the raid for all the band on the road there.</summary>
        public void RaidArrive(ref Person p, int at)
        {
            var w = _w;
            if (p.Band == 0 || !w.Bands.Exists(p.Band)) return;
            ref var b = ref w.Bands.Get(p.Band);
            if (b.State != 0 || b.Target != at) return;
            _band.Clear();
            Members(in b, _members);   // (its village's people and the tributaries' who answered the call: C10.1)
            for (int i = 0; i < _members.Count; i++)
            {
                ref var q = ref w.People.Get(_members[i]);
                if (q.Alive && q.Band == b.Id && (q.Id == p.Id || (q.Act == ActivityKind.TripOut && q.Why == WhyCode.Raid && q.Dest == at) || (q.At == at && q.Why == WhyCode.Raid))) _band.Add(_members[i]);
            }
            Fall(b.Id, _band);
        }

        /// <summary>
        /// The band falls on the village's store (D2-073; both resolutions). A band is louder than one taker: the store's sleepers
        /// or its watch see it the more readily. Unseen, each carries off a load. Seen, the watcher and the woken come out - a share
        /// of the village's grown people, up to the band's size and more - and blows go round by round, each side striking by its
        /// numbers and strength (health, years, hurt, metal), until one side breaks: the band that holds the field takes half its
        /// loads. Every blow a wound deed; the raid a deed the raided village holds against the raiders'; what the band carries
        /// home answers part of the wrong it went for.
        /// </summary>
        void Fall(int bandId, System.Collections.Generic.List<int> raiders)
        {
            var w = _w;
            long now = w.Now;
            ulong seed = w.Seed;
            ref var b = ref w.Bands.Get(bandId);
            int at = b.Target, origin = b.Origin;
            b.State = 1;
            ref var t = ref w.Settlements.Get(at);
            int n = raiders.Count;
            bool watched = t.Watcher != 0;
            double seenOne = SeenAt(at);
            double seen = 1.0 - DMath.Exp((1.0 + 0.3 * (n - 1)) * DMath.Log(DMath.Max(1e-9, 1.0 - seenOne)));
            bool isSeen = _forceSeen >= 0 ? _forceSeen == 1 : KeyedRng.Unit(seed, RngStream.Society, (ulong)bandId, (ulong)now, 60) < seen;
            double loads = n;
            int blows0 = (int)w.Metrics.Blows;
            byte outcome = 0;   // 0 unseen, 1 seen and the band held the field, 2 seen and the band was driven off
            if (isSeen)
            {
                // the defenders: the watcher, the first to wake and the neighbours round the store who come running; each round
                // more of the village wakes and comes
                _defend.Clear();
                int fitThere = FitAt(at, now);
                if (watched && w.People.Exists(t.Watcher) && Fit(in w.People.Get(t.Watcher), now)) _defend.Add(t.Watcher);
                Rally(at, 1 + Helpers(at) + (watched ? 1 : 0), now);
                outcome = 0;
                for (int round = 0; round < 4 && outcome == 0; round++)
                {
                    if (round > 0) Rally(at, _defend.Count + (int)DMath.Floor(2 + Reinforce * fitThere), now);
                    if (_defend.Count == 0) { outcome = 1; break; }
                    double ground = Ground(at);
                    double sr = SideStrength(raiders), sd = SideStrength(_defend) * ground;
                    // each side strikes by its numbers and strength
                    Exchange(raiders, _defend, sr / (sr + sd), bandId, round, true);
                    Exchange(_defend, raiders, sd / (sr + sd), bandId, round, false);
                    sr = SideStrength(raiders); sd = SideStrength(_defend) * ground;
                    if (sr < BreakAt * sd) outcome = 2;                    // the band breaks and runs
                    else if (round >= 1 && sd < BreakAt * sr) outcome = 1; // the defenders give way
                }
                if (outcome == 0) outcome = SideStrength(raiders) >= SideStrength(_defend) * Ground(at) ? (byte)1 : (byte)2;
                loads = outcome == 1 ? 0.5 * n : 0.0;
            }
            // the loot: a load each that the band carries (half when it had to fight for the field)
            ref var t2 = ref w.Settlements.Get(at);
            double loot = DMath.Min(loads * LoadFu, t2.Granary);
            if (loot > 0)
            {
                t2.Granary -= loot;
                double each = loot / n;
                for (int i = 0; i < n; i++) { ref var q = ref w.People.Get(raiders[i]); q.Cargo = Trade.Grain; q.CargoQty = (float)each; q.CargoTo = Trade.ToLarder; }
            }
            ref var b2 = ref w.Bands.Get(bandId);
            b2.Loot = (float)loot;
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.Raid, Time = now, Cell = t2.Cell, Settlement = at, Subject = EntityRef.Settlement(origin), Object = EntityRef.Settlement(at),
                Other = EntityRef.Person(b2.Leader), Cause1 = b2.Event, Rel1 = CauseRel.PartOf, Magnitude = (float)loot, ArgA = n, ArgB = outcome * 100 + ((int)w.Metrics.Blows - blows0),
                RootKind = RootKind.People,
            });
            // the raid a deed (the doers known when seen; a loss found when not), and what it answers
            int deed = w.DeedBook.Record(DeedKind.Raid, b2.Leader, origin, 0, at, Trade.Grain, loot, loot + RaidFright, ev);
            w.DeedBook.Learn(at, deed, isSeen ? KnownHow.Saw : KnownHow.Found);
            w.DeedBook.Learn(origin, deed, KnownHow.Own);
            // (what it carries home answers the wrong it went for: its village's own, or the tributary's its lord answered - once: D2-108)
            if (b2.Motive != 0 && loot > 0) w.DeedBook.SettleBy(b2.Motive == 2 && b2.For != 0 ? b2.For : origin, at, loot);
            w.Metrics.Raids++;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Raid, origin, at);
            w.Metrics.RaidLoot += loot;
            if (outcome == 2) w.Metrics.RaidsBeaten++;
            // COARSE: home at once with what they carry
            if (w.Resolution == Resolution.Coarse)
                for (int i = 0; i < n; i++) { ref var q = ref w.People.Get(raiders[i]); if (q.CargoQty > 0f) w.Trade.Unload(ref q, q.Home); q.Band = 0; }   // (to their own homes: a tributary's people too, C10.1)
            if (w.Resolution == Resolution.Coarse) w.Bands.Remove(bandId);
        }

        /// <summary>The defenders come out, the boldest first, until there are this many (as many as there are fit and at home).</summary>
        void Rally(int at, int count, long now)
        {
            var w = _w;
            var res = w.Residents(at);
            while (_defend.Count < count)
            {
                int best = 0; double bv = -1;
                for (int i = 0; i < res.Count; i++)
                {
                    ref var q = ref w.People.Get(res[i]);
                    if (!Fit(in q, now) || q.At != at || _defend.Contains(res[i])) continue;
                    double v = q.Bold / 255.0 + 0.5 * KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)res[i], (ulong)now, 61);
                    if (v > bv) { bv = v; best = res[i]; }
                }
                if (best == 0) return;
                _defend.Add(best);
            }
        }

        /// <summary>A village's fit grown people at home.</summary>
        public int FitAt(int sid, long now)
        {
            var w = _w;
            var res = w.Residents(sid);
            int n = 0;
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (Fit(in q, now) && q.At == sid) n++; }
            return n;
        }

        /// <summary>
        /// A village's people of fighting age (C10.5): its grown people of 16 to 60 who live there, wherever they are today. What a neighbour
        /// weighs it by over the seasons ahead: a day's wounds, sickness and hunger pass and away is not gone, so they do not count against it;
        /// the dead, the old and those who left do.
        /// </summary>
        public int Hands(int sid)
        {
            var w = _w;
            long now = w.Now;
            var res = w.Residents(sid);
            int n = 0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive) continue;
                double age = q.AgeYears(now);
                if (age >= 16 && age < 60) n++;
            }
            return n;
        }

        /// <summary>
        /// The might a village weighs another by (C10.5, D2-159) - in bowing to raiders (D2-075), in bowing for bread (C10.4) and in paying no more
        /// (a bond's end): its people of fighting age (`Hands`) and, for a lord, the men its call would bring against that village - of each of its
        /// tributaries within a day's walk of it, a quarter of the meek and more of the bold, never those with close kin there (C10.1's own call).
        /// The village weighed is never its own caller. D2-075 weighed a lord by its own village's fit people at home that day: a lord of several
        /// villages was no stronger than its seat, and a bond agreed the day after a raid (the beaten village's wounded abed) ended at the next
        /// harvest, when they had mended.
        /// </summary>
        public double Might(int sid, int against)
        {
            var w = _w;
            long now = w.Now;
            if (MightOff) return FitAt(sid, now);
            double m = Hands(sid);
            if (against == 0 || !w.Settlements.Exists(against)) return m;
            var anchor = w.Settlements.Get(against).Anchor;
            double called = 0;
            bool indexed = false;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == against || o == sid) continue;
                ref var tr = ref w.Settlements.Get(o);
                if (tr.Lord != sid || !tr.Inhabited) continue;
                if (Vec3d.ArcDistance(tr.Anchor, anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, o, against) == null) continue;
                if (!indexed) { KinIndex(against); indexed = true; }
                var tres = w.Residents(o);
                for (int i = 0; i < tres.Count; i++)
                {
                    ref var q = ref w.People.Get(tres[i]);
                    if (!q.Alive) continue;
                    double age = q.AgeYears(now);
                    if (age < 16 || age >= 50 || KinInIndexed(in q, against)) continue;   // (whom the call takes: grown and under fifty, as a band's own)
                    called += CallOwed + CallBold * q.Bold / 255.0;
                }
            }
            if (MightTally != null && called > 0) { MightTally[12] += 1; MightTally[13] += called; }
            return m + called;
        }

        // (Carry's scratch, C10.6: the households whose cart carried a load of the tenth in a COARSE call)
        readonly System.Collections.Generic.List<int> _carted = new System.Collections.Generic.List<int>();

        // (Might's kin test, `KinIn` with the village's parents gathered once: its living residents' mothers and fathers)
        readonly System.Collections.Generic.HashSet<int> _kinMothers = new System.Collections.Generic.HashSet<int>(), _kinFathers = new System.Collections.Generic.HashSet<int>();

        void KinIndex(int o)
        {
            var w = _w;
            _kinMothers.Clear(); _kinFathers.Clear();
            var res = w.Residents(o);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive) continue;
                if (q.Mother != 0) _kinMothers.Add(q.Mother);
                if (q.Father != 0) _kinFathers.Add(q.Father);
            }
        }

        /// <summary>`KinIn` read from `KinIndex(o)`: a parent, partner, child or sibling (by a mother) living in o.</summary>
        bool KinInIndexed(in Person p, int o)
        {
            if (LivesIn(p.Mother, o) || LivesIn(p.Father, o) || LivesIn(p.Partner, o)) return true;
            if (_kinMothers.Contains(p.Id) || _kinFathers.Contains(p.Id)) return true;   // (their child lives there)
            return p.Mother != 0 && _kinMothers.Contains(p.Mother);                    // (a brother or sister by their mother)
        }

        /// <summary>Tools: one weighing of the stronger, by the old count and by might ([0..3] bowing to raiders, [4..7] for bread, [8..11] a bond's end).</summary>
        void TallyMight(int kind, bool old, bool now)
        {
            if (MightTally == null) return;
            MightTally[kind * 4] += 1;
            if (old) MightTally[kind * 4 + 1] += 1;
            if (now) MightTally[kind * 4 + 2] += 1;
            if (old && now) MightTally[kind * 4 + 3] += 1;
        }

        double SideStrength(System.Collections.Generic.List<int> side)
        {
            double s = 0;
            for (int i = 0; i < side.Count; i++) { ref var q = ref _w.People.Get(side[i]); if (q.Alive && q.Hurt < AbedHurt) s += Strength(in q); }
            return s;
        }

        /// <summary>A round of blows from one side to the other: each fighter strikes with a chance by the side's share of strength.</summary>
        void Exchange(System.Collections.Generic.List<int> from, System.Collections.Generic.List<int> to, double share, int bandId, int round, bool raiders)
        {
            var w = _w;
            long now = w.Now;
            ulong seed = w.Seed;
            for (int i = 0; i < from.Count; i++)
            {
                ref var a = ref w.People.Get(from[i]);
                if (!a.Alive || a.Hurt >= AbedHurt) continue;
                ulong k = (ulong)(bandId * 1000 + round * 100 + (raiders ? 0 : 50) + i);
                if (KeyedRng.Unit(seed, RngStream.Society, k, (ulong)now, 62) >= HitChance * 2.0 * share) continue;
                // a fighter of the other side still standing, by a keyed pick
                int pick = -1;
                int start = (int)KeyedRng.Below(KeyedRng.Hash(seed, RngStream.Society, k, (ulong)now, 63), to.Count);
                for (int j = 0; j < to.Count; j++) { int c = (start + j) % to.Count; ref var q = ref w.People.Get(to[c]); if (q.Alive && q.Hurt < AbedHurt) { pick = c; break; } }
                if (pick < 0) return;
                ref var v = ref w.People.Get(to[pick]);
                double hurt = 0.2 + 0.6 * KeyedRng.Unit(seed, RngStream.Society, k, (ulong)now, 64);
                Strike(ref a, ref v, hurt, w.Bands.Get(bandId).Target, !raiders, w.Bands.Get(bandId).Event);
            }
        }

        /// <summary>Lived: a raider who has done (or was driven off) goes home with what they carry.</summary>
        public static bool RaidDone(in Person p, World w) => p.Band != 0 && w.Bands.Exists(p.Band) && w.Bands.Get(p.Band).State != 0;

        /// <summary>A band whose night has passed without falling (its people never set out) is given up.</summary>
        void Disband(int sid)
        {
            var w = _w;
            long now = w.Now;
            for (int b = w.Bands.First(); b != 0; b = w.Bands.Next(b))
            {
                ref var band = ref w.Bands.Get(b);
                if (band.Origin != sid) continue;
                if (band.State == 0 && now < band.Arrive + SimTime.Hours(6)) continue;
                if (band.State != 0 && now < band.Arrive + SimTime.Day) continue;
                Members(in band, _members);   // (and the tributaries' people who answered the call: C10.1)
                for (int i = 0; i < _members.Count; i++) { ref var q = ref w.People.Get(_members[i]); if (q.Band == b && (q.At == q.Home || !q.Alive)) q.Band = 0; }
                bool anyOut = false;
                for (int i = 0; i < _members.Count; i++) if (w.People.Get(_members[i]).Band == b) anyOut = true;
                if (!anyOut) { w.Bands.Remove(b); return; }
            }
        }

        // ------------------------------------------------------------------ defence that is built (D2-074; both resolutions)
        /// <summary>
        /// A village's day of walls: its palisade's timber rots (about thirty years unmended) and falls when spent; a village that
        /// has none weighs one - what other villages' people took and did to it these three years, a year, half of it saved for the
        /// years a wall stands, against the labour and timber of a ring round its homes - and begins one when the losses outweigh it
        /// (built as a village work by its people's hours and wood); a walled village keeps mending while what it guards is worth it.
        /// Never a count of raids: losses remembered against labour and wood.
        /// </summary>
        void Defence(int sid, int days = 1)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (s.OldR > 0f)
            {
                // the old ring inside a grown town is not mended: it rots and is gone (C7.1)
                s.OldCond = (float)DMath.Max(0.0, (double)s.OldCond - (double)days / (WallYears * 48.0));
                if (s.OldCond <= 0f) { s.OldR = 0f; s.OldCond = 0f; }
            }
            if (s.Wall == 2)
            {
                s.WallCond = (float)DMath.Max(0.0, (double)s.WallCond - (double)days / (WallYears * 48.0));   // (DEEP: the step's days of rot)
                if (s.WallCond <= 0f)
                {
                    s.Wall = 3; s.WallKeep = 0; s.Works = (byte)(s.Works & ~Construction.Palisade);
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.WallFallen, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                        Cause1 = s.WallEvent, Rel1 = CauseRel.PartOf, RootKind = RootKind.Nature,
                    });
                }
            }
            if (!s.Inhabited) return;
            double lost = w.DeedBook.HarmSince(sid, now - 3 * SimTime.Year, out long latest) / 3.0;
            double guards = WallGuards * lost * WallLife;
            if (s.Wall == 0 || s.Wall == 3)
            {
                double r = WallRadius(sid);
                double cost = LabourWorth * (Construction.PalisadeHoursPerM + Construction.PalisadeWoodPerM) * 2.0 * DMath.PI * r;
                if (guards <= cost) return;
                ref var s2 = ref w.Settlements.Get(sid);
                s2.Wall = 1; s2.WallR = (float)r; s2.WallCond = 0f; s2.WallKeep = 1;
                s2.WallDry = (float)Places.Structures.DryShare(w, in s2, r);   // (its posts on dry ground only: C1.6)
                Places.Structures.Opened(w, sid);   // (camps on its line move; empty homes across it come down: C13.3)
                s2.WallEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.WallPlanned, Time = now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Cause1 = latest, Rel1 = CauseRel.InResponseTo, Magnitude = (float)lost, RootKind = RootKind.People,
                });
                w.Metrics.WallsPlanned++;
                return;
            }
            if (s.Wall == 2)
            {
                double cost = LabourWorth * (Construction.PalisadeHoursPerM + Construction.PalisadeWoodPerM) * 2.0 * DMath.PI * (double)s.WallR;
                s.WallKeep = (byte)(guards > MendShare * cost ? 1 : 0);
                // the town has grown past its ring: a larger one, if what it loses still outweighs its labour and timber (C7.1); the old ring
                // stays inside, unmended, until it rots
                int outside = Outside(sid, out int homes);
                if (outside >= OutgrownFrom && outside * 5 >= homes)
                {
                    double r = WallRadius(sid);
                    ref var s3 = ref w.Settlements.Get(sid);
                    if (r > (double)s3.WallR + 15.0)
                    {
                        double dry = Places.Structures.DryShare(w, in s3, r);
                        double bigger = LabourWorth * (Construction.PalisadeHoursPerM + Construction.PalisadeWoodPerM) * 2.0 * DMath.PI * r * dry;
                        if (guards > bigger)
                        {
                            s3.OldR = s3.WallR; s3.OldCond = s3.WallCond;
                            s3.Wall = 1; s3.WallR = (float)r; s3.WallCond = 0f; s3.WallKeep = 1; s3.WallDry = (float)dry; s3.WorkBuild = 0f;
                            Places.Structures.Opened(w, sid);   // (C13.3)
                            s3.WallEvent = w.Record(new HistoryEvent
                            {
                                Kind = HistoryKind.WallPlanned, Time = now, Cell = s3.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                                Cause1 = latest, Rel1 = CauseRel.InResponseTo, Magnitude = (float)lost, ArgA = outside, ArgB = 1, RootKind = RootKind.People,
                            });
                            w.Metrics.WallsPlanned++; w.Metrics.RingsGrown++;
                        }
                    }
                }
            }
        }

        /// <summary>`--nowallworks`: a ring is laid clear of homes only (before session 17), on the same binary.</summary>
        public static bool WallWorksOff;

        /// <summary>The ring a palisade would run on: outside the village's farthest home, its yards and its store (25-170 m).</summary>
        public double WallRadius(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            Construction.GranarySpot(in s, out double gx, out double gy);
            double far = DMath.Sqrt((gx - s.SquareX) * (gx - s.SquareX) + (gy - s.SquareY) * (gy - s.SquareY));
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                double dx = (double)hh.HomeX - s.SquareX, dy = (double)hh.HomeY - s.SquareY;
                double d = DMath.Sqrt(dx * dx + dy * dy);
                if (d > far) far = d;
            }
            // and its works on the green and at its edge - stores, barns, the smithy - inside too (C1.4: a smithy stood astride the ring)
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Places.Structures.IsHome(b.Kind)) continue;
                var k = Places.Structures.Of(b.Kind);
                if (k.Zone != Places.Zone.Core && k.Zone != Places.Zone.Edge) continue;
                double dx = (double)b.X - s.SquareX, dy = (double)b.Y - s.SquareY;
                double d = DMath.Sqrt(dx * dx + dy * dy) + k.Footprint + 3.0 - 15.0;   // (its far side clear of the posts)
                if (d > far) far = d;
            }
            double r = DMath.Clamp(far + 15.0, 25.0, 170.0);   // (its pens and yards behind the homes inside)
            // (C13.3) a ring held to its greatest reach runs between houses, never through one: the nearest radius inward where none stands across it
            // (those beyond stand outside it, as suburbs do)
            // (the fewest across it where no radius is clear: those are taken down when they stand empty - Homes.Clear)
            double best = r; int least = int.MaxValue, nw;
            for (double dr = 0.0; dr <= 30.0 && r - dr >= 25.0; dr += 1.0)
            {
                int n = Astride(sid, r - dr, out nw);
                if (n == 0) return WallWorksOff || nw == 0 ? r - dr : PastWorks(sid, r - dr);
                if (n < least) { least = n; best = r - dr; }
            }
            return best;
        }

        /// <summary>
        /// (session 17, D2-141) A work stands where a ring clear of homes would run - a training ground beyond the homes: the ring goes past it only
        /// where that costs no home - inward while it crosses none (so it leaves outside no home the first ring held), or outward round it, whichever
        /// is nearer; with neither within 25 m the posts run as they would have. (A first form counted a work as a home in the inward search: rings
        /// went in past the works and left a fifth of the homes outside, and the town at once raised another - DefenceTests.)
        /// </summary>
        double PastWorks(int sid, double rh)
        {
            bool inward = true;
            for (double d = 1.0; d <= 25.0; d += 1.0)
            {
                if (inward && rh - d >= 25.0)
                {
                    int nh = Astride(sid, rh - d, out int wi);
                    if (nh > 0) inward = false;
                    else if (wi == 0) return rh - d;
                }
                if (rh + d <= 170.0 && Astride(sid, rh + d, out int wo) == 0 && wo == 0) return rh + d;
            }
            return rh;
        }

        /// <summary>How many homes of the village - standing, or a camp's lot kept for one - would stand across a ring of radius r (their ground within
        /// 1.5 m of the posts); `works` the works across it (none with `--nowallworks`).</summary>
        int Astride(int sid, double r, out int works)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            int n = 0;
            works = 0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Places.Homes.RuinLeft(in b, w.Now) <= 0) continue;
                double dx = (double)b.X - s.SquareX, dy = (double)b.Y - s.SquareY;
                if (!Places.Structures.IsHome(b.Kind))
                {
                    // (session 17) a work standing where the ring would run - a training ground beyond the homes - is no more crossed than a home: the
                    // posts had run through the beaten ground (14 such in seed 4's world at year 300). Boats and works far out (quarries, mines) are not near.
                    if (WallWorksOff || Places.Boats.IsBoat(b.Kind)) continue;
                    if (DMath.Abs(DMath.Sqrt(dx * dx + dy * dy) - r) < Places.Footprints.WorkR(b.Kind) + 1.5) works++;
                    continue;
                }
                if (DMath.Abs(DMath.Sqrt(dx * dx + dy * dy) - r) < Places.Footprints.Reach(Places.Footprints.PlanOf(in b)) + 1.5) n++;
            }
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var h = ref w.Households.Get(hl[i]);
                if (h.Building != 0) continue;
                double dx = (double)h.HomeX - s.SquareX, dy = (double)h.HomeY - s.SquareY;
                if (DMath.Abs(DMath.Sqrt(dx * dx + dy * dy) - r) < Places.Footprints.Reach(Places.Footprints.LotPlan(in s, h.HomeX, h.HomeY)) + 1.5) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ tribute and lordship (D2-075; both resolutions)
        /// <summary>
        /// A raided village weighs bowing to its raiders (Carneiro's circumscription): when a band of theirs held the field here within
        /// a month, they are stronger (a fifth more fit grown people), it knows no good land to flee to, and what they cost it these
        /// years outweighs a tenth of its harvest, it agrees to pay them that tenth each harvest for peace - they raise no band against
        /// their own tributary. History tells it, its cause the raid.
        /// </summary>
        void Submit(int sid)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited || s.Lord != 0) return;
            int lord = LatestRaider(sid, now - SimTime.Days(30), out long raidEvent);
            if (lord == 0 || !w.Settlements.Exists(lord) || !w.Settlements.Get(lord).Inhabited) return;
            if (LordOf(lord) == sid) return;   // (not to its own tributary's lord-in-turn)
            double cost = w.DeedBook.Grievance(sid, lord) / 3.0;   // what they cost it a year, as it weighs it
            double harvest = DMath.Max(s.LastHarvest, s.ExpectedHarvest);
            double tribute = TributeShare * harvest;
            s.Lore.Best(out float place);
            bool hemmed = place < 0.6f;                 // (no good land known to go to: flight is hard)
            if (!(hemmed && cost > tribute && tribute > 0)) return;
            // (C10.5: its people and theirs with the men their call brings - not the day after the raid's count, its wounded abed)
            bool weaker = Might(lord, sid) >= TributeOdds * Might(sid, lord);
            if (MightTally != null) TallyMight(0, FitAt(lord, now) >= TributeOdds * FitAt(sid, now), weaker);
            if (!weaker) return;
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Lord = lord; s2.LordSince = now;
            s2.LordEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TributeAgreed, Time = now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(lord),
                Cause1 = raidEvent, Rel1 = CauseRel.InResponseTo, Magnitude = (float)TributeShare, ArgA = PolitySize(Root(lord)), RootKind = RootKind.People,
            });
            w.Metrics.Tributes++;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Tribute, sid, lord);
        }

        /// <summary>The village whose band last held the field here (seen, loot carried off) since a time, and that raid's event; or 0.</summary>
        int LatestRaider(int sid, long since, out long ev)
        {
            ev = 0;
            var list = _w.DeedBook.KnownBy(sid);
            if (list == null) return 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var k = list[i];
                ref var d = ref _w.Deeds.Get(k.Deed);
                if (d.Time < since) break;
                if (d.Kind != DeedKind.Raid || d.ToS != sid || k.How != KnownHow.Saw || d.Qty <= 0f) continue;
                ev = d.Event;
                return d.ByS;
            }
            return 0;
        }

        /// <summary>
        /// A village's harvest (both resolutions): a tributary pays its lord a tenth of what it brought in - unless the lord has grown
        /// no stronger than it, or is gone: then it pays no more (a refusal the lord holds against it). The tribute answers the lord's
        /// wrongs against it.
        /// </summary>
        public void Harvested(int sid, double yield)
        {
            var w = _w;
            long now = w.Now;
            if (!BreadOff && w.Settlements.Get(sid).Lord == 0) Indebted(sid);   // (the grain it cannot give back: C10.4)
            ref var s = ref w.Settlements.Get(sid);
            if (s.Lord == 0) return;
            int lord = s.Lord;
            bool gone = !w.Settlements.Exists(lord) || !w.Settlements.Get(lord).Inhabited;
            // (C10.5: no longer the stronger with the men its call brings - a season's sick and wounded mend)
            double lordMight = gone ? 0.0 : Might(lord, sid), ownMight = gone ? 0.0 : Might(sid, lord);
            bool weak = !gone && lordMight < ownMight;
            if (MightTally != null && !gone) TallyMight(2, FitAt(lord, now) < FitAt(sid, now), weak);
            if (gone || weak)
            {
                long agreed = s.LordEvent;
                s.Lord = 0; s.LordEvent = 0;
                long ev = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.TributeEnded, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(lord),
                    Cause1 = agreed, Rel1 = CauseRel.PartOf, ArgA = gone ? 1 : 0, RootKind = RootKind.People,
                    // (C10.5: the weighing - what the lord could bring against it, and its own people of fighting age)
                    Magnitude = MightOff || gone ? 0f : (float)lordMight, ArgB = MightOff || gone ? 0 : (int)(ownMight + 0.5),
                });
                if (!gone)
                {
                    int refusal = w.DeedBook.Record(DeedKind.Refusal, 0, sid, 0, lord, Trade.Grain, 0.0, TributeShare * DMath.Max(yield, s.LastHarvest), ev);
                    w.DeedBook.Learn(lord, refusal, KnownHow.Saw);
                    w.DeedBook.Learn(sid, refusal, KnownHow.Own);
                }
                w.Metrics.TributesEnded++;
                // what was set aside for the lord and not yet carried is the village's again (C10.6)
                ref var se = ref w.Settlements.Get(sid);
                if (se.TributeDue > 0) { if (CarryTally != null) CarryTally[4] += se.TributeDue; se.Granary += se.TributeDue; se.TributeDue = 0; }
                se.CarryToday = 0;
                return;
            }
            if (!CarryOff)
            {
                // (C10.6) what still stands set aside of the last tenth, carried by nobody in a year - no hands fit to carry it, or the way cut since -
                // is the village's again: a store keeps no pile for a lord its people cannot bring it to
                // (below a basket's worth nothing is carried: `Carry` - a lived load's rounding stays with the next tenth)
                if (s.TributeDue >= 1.0) { if (CarryTally != null) CarryTally[7] += s.TributeDue; s.Granary += s.TributeDue; s.TributeDue = 0; s.CarryToday = 0; }
                // the tenth is set aside only where its people can carry it: along a way on foot to the lord's village. A bond for bread needs only a day's
                // walk as the crow flies (C10.4) and relief within it moves at once, so a village across water can be bound to a lord no way on foot
                // leads to - its tenth stays in its store, unless a way by sea joins their landings and it has a boat of planks to row it over or its lord
                // one to come for it (C9.5a)
                if (w.Routes.Get(w, sid, lord) == null && !(BySea(sid, lord) && (CrossBoats(sid, Far(sid, lord)) > 0 || CrossBoats(lord, Far(sid, lord)) > 0)))   // (a dugout across a short water too: C9.5b)
                {
                    if (CarryTally != null)
                    {
                        double tenth = DMath.Min(TributeShare * yield, s.Granary);
                        CarryTally[8] += 1; CarryTally[9] += tenth;
                        // (tools, C9.5 survey) a way by sea between their landings, and plank boats on either side to carry it
                        if (s.LandAt != 0 && w.Settlements.Get(lord).LandAt != 0 && w.SeaRoutes.Get(w, sid, lord) != null)
                        {
                            CarryTally[10] += 1; CarryTally[13] += tenth;
                            if (PlankBoats(sid) > 0) CarryTally[11] += 1;
                            if (PlankBoats(lord) > 0) CarryTally[12] += 1;
                        }
                    }
                    return;
                }
            }
            double pay = DMath.Min(TributeShare * yield, s.Granary);
            if (pay <= 0) return;
            s.Granary -= pay;
            if (!CarryOff)
            {
                // (C10.6: the tenth is set aside in the store for the lord, and its people carry it there - `Carry`, `Brought`)
                s.TributeDue += pay;
                if (CarryTally != null) CarryTally[3] += pay;
                w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.TributePaid, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(lord),
                    Cause1 = s.LordEvent, Rel1 = CauseRel.PartOf, Magnitude = (float)pay, RootKind = RootKind.People,
                    ArgA = MightOff ? 0 : (int)(lordMight + 0.5), ArgB = MightOff ? 0 : (int)(ownMight + 0.5),   // (C10.5: the weighing it paid by)
                });
                return;
            }
            ref var l = ref w.Settlements.Get(lord);
            l.Granary += pay;
            if (!Leaders.Off && !Leaders.TributeOff && l.HeadHh != 0) w.Holdings.Put(l.HeadHh, lord, pay);   // (the lord's own: its first household's grain - C10.2)
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TributePaid, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(lord),
                Cause1 = s.LordEvent, Rel1 = CauseRel.PartOf, Magnitude = (float)pay, RootKind = RootKind.People,
                ArgA = MightOff ? 0 : (int)(lordMight + 0.5), ArgB = MightOff ? 0 : (int)(ownMight + 0.5),   // (C10.5: the weighing it paid by)
            });
            w.DeedBook.SettleBy(lord, sid, pay);
            if (!BreadOff) w.Trade.Repaid(sid, lord, pay);   // (and gives back the grain its lord gave it in its famines: C10.4)
            w.Metrics.TributeFu += pay;
        }

        /// <summary>
        /// Tribute brought (C10.6): a load of the tenth reaches the lord's store - its first household's grain (C10.2); it answers the lord's wrongs
        /// against the tributary and gives back the grain the lord gave it in its famines (C10.4), as the tenth did at the harvest before.
        /// </summary>
        public void Brought(int sid, int lord, double fu)
        {
            var w = _w;
            if (fu <= 0 || !w.Settlements.Exists(lord)) return;
            Stored(lord, fu);
            Settle(sid, lord, fu);
        }

        /// <summary>The lord's tenth into its store (C10.6): its first household's grain (C10.2).</summary>
        public void Stored(int lord, double fu)
        {
            var w = _w;
            if (fu <= 0 || !w.Settlements.Exists(lord)) return;
            ref var l = ref w.Settlements.Get(lord);
            l.Granary += fu;
            if (!Leaders.Off && !Leaders.TributeOff && l.HeadHh != 0) w.Holdings.Put(l.HeadHh, lord, fu);   // (the lord's own: C10.2)
        }

        /// <summary>The tenth handed over (C10.6) - at the lord's store, or at the tributary's landing when the lord's boat comes for it (C9.5a): it
        /// answers the lord's wrongs against the tributary and gives back the grain the lord gave it in its famines (C10.4).</summary>
        public void Settle(int sid, int lord, double fu)
        {
            var w = _w;
            if (fu <= 0) return;
            w.DeedBook.SettleBy(lord, sid, fu);
            if (!BreadOff) w.Trade.Repaid(sid, lord, fu);
            w.Metrics.TributeFu += fu;
        }

        /// <summary>Grown and well enough to carry a load to the lord's store and back: as a trader goes.</summary>
        public static bool CanCarry(in Person p, long now)
        {
            if (!p.Alive || p.Sick != 0 || p.Health <= 0.7 || p.Hurt >= RoadHurt || p.PregDue != 0) return false;
            if (p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) return false;
            double age = p.AgeYears(now);
            return age >= 18 && age < 56;
        }

        /// <summary>
        /// Tribute carried (C10.6, D2-161), a village's day: while its lord's tenth stands set aside, its people carry it to the lord's store along the
        /// way between them - as many loads a day as clear it in `CarryDays`, never more than one for every `CarryPer` of its fit grown people; a back's
        /// load, or the household's cart's on a worn way (C4.2). Lived, the loads wait for the first of its grown people to take them in the morning
        /// (`CarryToday`); a COARSE day carries them at once at that rate. Each load wears the way (C4.1), carries its sickness and news (D2-066, D2-072)
        /// and is brought (`Brought`). The record: food rents carried to the lord's hall - the feorm of the early English kings, the bishop's and the
        /// lord's dues carted in after the harvest.
        /// </summary>
        void Carry(int sid, int days)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.TributeDue < 1.0) { s.CarryToday = 0; return; }
            int lord = s.Lord;
            if (lord == 0 || !w.Settlements.Exists(lord) || !w.Settlements.Get(lord).Inhabited) { s.CarryToday = 0; return; }
            if (w.Routes.Get(w, sid, lord) == null) { s.CarryToday = 0; if (BySea(sid, lord)) CarrySea(sid, lord, days); return; }   // (across water: by boat, C9.5a)
            long now = w.Now;
            int most = System.Math.Max(1, FitAt(sid, now) / CarryPer);
            int want = 1 + (int)DMath.Floor(s.TributeDue / (LoadFu * CarryDays));
            int k = System.Math.Min(want, most);
            if (w.Resolution != Resolution.Coarse) { s.CarryToday = (byte)System.Math.Min(255, k); return; }
            s.CarryToday = 0;
            var res = w.Residents(sid);
            int n = k * days, done = 0;
            double km = (w.Routes.Get(w, sid, lord)?.Length ?? 0.0) / 1000.0;
            _carted.Clear();   // (the households whose cart carried a load in these days: one cart, one load a day)
            for (int a = 0; done < n && a < 4 * n && res.Count > 0; a++)
            {
                ref var s1 = ref w.Settlements.Get(sid);
                if (s1.TributeDue < 1.0) break;
                int pid = res[(int)KeyedRng.Below(KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, (ulong)(900 + a)), res.Count)];
                ref var p = ref w.People.Get(pid);
                if (!CanCarry(in p, now) || p.At != sid) continue;
                double kg = Carts.LoadFor(w, p.Household, sid, lord);
                if (kg > Trade.LoadKg + 1e-9)
                {
                    int used = 0;
                    for (int i = 0; i < _carted.Count; i++) if (_carted[i] == p.Household) used++;
                    if (used >= days) kg = Trade.LoadKg; else _carted.Add(p.Household);   // (its cart has gone each day already: a back's load)
                }
                double fu = DMath.Min(kg / Trade.KgOf(Trade.Grain), s1.TributeDue);
                s1.TributeDue -= fu;
                Brought(sid, lord, fu);
                w.Ways.Walk(w, sid, lord, 2.0);   // (there and back: C4.1)
                w.Disease.Arrive(ref p, lord, SimTime.Hours(Trade.VisitHoursCoarse));
                w.DeedBook.Meet(sid, lord);
                done++;
                if (CarryTally != null) { CarryTally[0]++; CarryTally[1] += fu; if (kg > Trade.LoadKg + 1e-9) CarryTally[5] += fu; CarryTally[6] += 2.0 * km; }
            }
        }

        /// <summary>A way by sea joins the two villages' landings (C9.5a; `--oldacross`: none counts).</summary>
        bool BySea(int sid, int lord)
        {
            var w = _w;
            if (Trade.AcrossOff || Trade.NoSea || !w.Settlements.Exists(lord)) return false;
            return w.Settlements.Get(sid).LandAt != 0 && w.Settlements.Get(lord).LandAt != 0 && w.SeaRoutes.Get(w, sid, lord) != null;
        }

        /// <summary>
        /// The lord's tenth across water (C9.5a): no way on foot joins them but a way by sea does - its own free plank boats carry it, a boat's load
        /// each, in a calm sea, as its relief and its grain given back go (C9.3), or with none of its own free the lord's come for it, as a giver's
        /// come for the grain given back. Lived, the village's day books them for their households to row in the morning (`Trade.BookTribute`;
        /// delivered at the lord's landing or taken aboard at its own, `Trade.Deliver`); a COARSE day carries the boats' loads at once. With no boat
        /// free it waits - and what a year left uncarried is the village's again at the next harvest. The record: island dues carried in kind by ship
        /// - the Norse skat of the isles, the food rents Hebridean lords' galleys gathered.
        /// </summary>
        void CarrySea(int sid, int lord, int days)
        {
            var w = _w;
            var way = w.SeaRoutes.Get(w, sid, lord);
            if (way == null || Boats.WindAt(w, way.PointAt(0.5 * way.Length)) > Boats.Calm) return;   // (they wait for a calm sea)
            ref var s = ref w.Settlements.Get(sid);
            if (w.Resolution != Resolution.Coarse) { w.Trade.BookTribute(sid, lord, s.TributeDue); return; }
            // its own boats' loads first, the lord's for the rest (dugouts across a short water: C9.5b)
            double far = Far(sid, lord), mine = w.Trade.FreeLoadKg(sid, far), theirs = w.Trade.FreeLoadKg(lord, far);
            if (mine + theirs <= 0) return;
            double fu = DMath.Min((mine + theirs) / Trade.KgOf(Trade.Grain) * days, s.TributeDue);
            if (fu < 1.0) return;
            double fetched = fu - DMath.Min(fu, mine / Trade.KgOf(Trade.Grain) * days);
            s.TributeDue -= fu;
            Brought(sid, lord, fu);
            w.Metrics.Voyages++; w.Metrics.SeaCargoKg += fu * Trade.KgOf(Trade.Grain);
            w.DeedBook.Meet(sid, lord);
            if (CarryTally != null)
            {
                CarryTally[0]++; CarryTally[1] += fu; CarryTally[14]++; CarryTally[15] += fu;
                if (fetched > 0) { CarryTally[16]++; CarryTally[17] += fetched; }
            }
        }

        /// <summary>Lived (C10.6): a carrier at the end of the way - the load into the lord's store if the village still pays it; else they carry it home.</summary>
        public void CarrierArrived(ref Person p, int at)
        {
            var w = _w;
            if (p.Cargo != Trade.Grain || p.CargoQty <= 0f || p.CargoTo != Trade.ToLord) return;
            if (!w.Settlements.Exists(p.Home) || w.Settlements.Get(p.Home).Lord != at) return;   // (the bond ended on the way: home with it - `Trade.Unload`)
            double fu = p.CargoQty;
            p.Cargo = Trade.None; p.CargoQty = 0f; p.CargoTo = Trade.ToStore;
            Brought(p.Home, at, fu);
            if (CarryTally != null) { CarryTally[0]++; CarryTally[1] += fu; CarryTally[2]++; }
        }

        /// <summary>Lived (C10.6): a grown person of a village with loads to carry today takes one - a back's or their household's cart's - from what is
        /// set aside for the lord. The load's FU, or 0 when none waits.</summary>
        public double TakeLoad(ref Person p, int lord)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(p.Home);
            if (s.CarryToday == 0 || s.TributeDue < 1.0 || s.Lord != lord) return 0.0;
            if (p.CargoQty > 0f || p.CargoTo != Trade.ToStore) w.Trade.Unload(ref p, p.Home);   // (whatever they still hold is home first)
            double kg = Carts.LoadFor(w, p.Household, p.Home, lord);
            if (kg > Trade.LoadKg + 1e-9 && Carts.Out(w, p.Household, p.Id)) kg = Trade.LoadKg;   // (its cart is out with another of them: a back's load)
            float q = (float)DMath.Min(kg / Trade.KgOf(Trade.Grain), s.TributeDue);   // (as the person carries it: the store loses what they hold)
            s.TributeDue -= q;
            s.CarryToday--;
            p.Cargo = Trade.Grain; p.CargoQty = q; p.CargoTo = Trade.ToLord;
            if (CarryTally != null && kg > Trade.LoadKg + 1e-9) CarryTally[5] += q;
            return q;
        }

        /// <summary>
        /// Bread and dependence (C10.4, D2-157), at a village's harvest while it pays no lord: what it still owes the neighbour that fed it most in its
        /// famines (`Trade.MainCreditor`, the relief it saw given, weighed now) beyond all its new harvest can spare is more than a month of its people's
        /// food - a famine's whole relief unreturned - and that neighbour is within a day's walk and the stronger (`TributeOdds`): it bows to it, a tenth
        /// of each harvest, as Attica's hektemoroi paid a sixth to the creditors of their bad years and the early medieval poor commended themselves in
        /// famine to whoever would feed them. The tribute gives the grain back (`Trade.Repaid`), its lord feeds it in its famines without counting what it
        /// owes (`Trade.Relief`), and it answers its lord's call; it ends as D2-075's does - the lord gone or no longer the stronger. History tells it, its
        /// cause the gift.
        /// </summary>
        void Indebted(int sid)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            int giver = w.Trade.MainCreditor(sid, out double owed, out long gift);
            double daily;
            Places.Trade.DaysOfFood(w, in s, out daily);
            double left = owed - w.Trade.Spare(sid, Places.Trade.Grain);
            double days = left / daily;
            bool near = giver != 0 && w.Settlements.Exists(giver) && w.Settlements.Get(giver).Inhabited &&
                        Vec3d.ArcDistance(s.Anchor, w.Settlements.Get(giver).Anchor, w.Planet.Radius) <= BreadReach;
            // (the stronger with the men its call brings: C10.5 - weighed where it could matter, or for the tally)
            bool stronger = near && (BreadTally != null || MightTally != null || days >= BowDays) && Might(giver, sid) >= TributeOdds * Might(sid, giver);
            bool loop = near && Root(giver) == sid;   // (not to a village that pays it, directly or through its lords)
            if (MightTally != null && near && days >= BowDays && !loop) TallyMight(1, FitAt(giver, now) >= TributeOdds * FitAt(sid, now), stronger);
            if (BreadTally != null)
            {
                int b = giver == 0 || days <= 0 ? 0 : days < 5 ? 1 : days < 10 ? 2 : days < 20 ? 3 : days < 30 ? 4 : days < 60 ? 5 : 6;
                BreadTally[b]++;
                if (stronger && !loop) BreadTally[7 + b]++;
            }
            if (giver == 0 || days < BowDays || !stronger || loop) return;
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Lord = giver; s2.LordSince = now;
            s2.LordEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TributeAgreed, Time = now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(giver),
                Cause1 = gift, Rel1 = CauseRel.InResponseTo, Magnitude = (float)TributeShare, ArgA = PolitySize(Root(giver)), ArgB = 1, RootKind = RootKind.People,
            });
            w.Metrics.Tributes++; w.Metrics.BreadBonds++;
            if (Places.Trade.AcrossTally != null && w.Routes.Get(w, sid, giver) == null) Places.Trade.AcrossTally[4] += 1;   // (tools: a bow across water)
            if (BreadTally != null) BreadTally[14]++;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Tribute, sid, giver);
        }

        /// <summary>The village a village pays tribute to (0 none).</summary>
        public int LordOf(int sid) => sid > 0 && _w.Settlements.Exists(sid) ? _w.Settlements.Get(sid).Lord : 0;

        /// <summary>The head of a village's polity: its lord's lord, and so on (guarded against loops).</summary>
        public int Root(int sid)
        {
            int r = sid;
            for (int i = 0; i < 16; i++) { int l = LordOf(r); if (l == 0 || l == sid) break; r = l; }
            return r;
        }

        /// <summary>The villages that pay a lord, directly or through their own lords (its lordship, the lord not counted).</summary>
        public int PolitySize(int lord)
        {
            var w = _w;
            int n = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == lord || !w.Settlements.Get(o).Inhabited) continue;
                int r = LordOf(o);
                for (int i = 0; i < 16 && r != 0; i++) { if (r == lord) { n++; break; } if (r == o) break; r = LordOf(r); }
            }
            return n;
        }

        long HungerCause(int home, long now)
        {
            ref var s = ref _w.Settlements.Get(home);
            if (s.FamineEvent != 0 && now - s.FamineAt < SimTime.Year) return s.FamineEvent;
            return s.HungerEvent != 0 ? s.HungerEvent : s.RationEvent;
        }

        /// <summary>A village's night of taking at COARSE resolution: each hungry, fit grown person's lived chance, the act at once.</summary>
        public void Day(int sid, int days = 1)
        {
            var w = _w;
            var prof = w.Profiler;   // (tools: the parts of the day; null in the game)
            prof?.Begin(10023); Wounds(sid); Defence(sid, days); Watch(sid); prof?.End(10023);
            prof?.Begin(10024); ReadFoe(sid); Claims(sid, days); Submit(sid); Disband(sid); prof?.End(10024);
            if (!CarryOff) Carry(sid, days);   // (the lord's tenth carried to its store: C10.6)
            prof?.Begin(10025); Raids(sid, false, days); prof?.End(10025);
            if (w.Resolution != Resolution.Coarse) return;
            prof?.Begin(10026); News(sid, days); prof?.End(10026);
            prof?.Begin(10027); Taking(sid, days); prof?.End(10027);
        }

        /// <summary>COARSE: the hungry who go to a neighbour's store in the night, and the fed and bold who take back (D2-069, D2-072).</summary>
        void Taking(int sid, int days)
        {
            var w = _w;
            long now = w.Now;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                int pid = res[i];
                ref var p = ref w.People.Get(pid);
                if (p.At != p.Home || !CanTake(in p, now)) continue;
                w.Metrics.TakeWeighed++;
                if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 2) >= TakeChance(in p)) continue;
                int to = TakeTarget(in p, out double free);
                if (to == 0 || KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 3) >= free) continue;
                Take(ref p, to);
                if (p.CargoQty > 0f) w.Trade.Unload(ref p, sid);   // (home with it before the night is out)
            }
            // taking back: the fed and bold of a village that holds a heavy wrong against a neighbour (D2-072)
            ref var hs = ref w.Settlements.Get(sid);
            if (hs.Foe != 0 && hs.FoeWorth >= TakeBackFrom)
                for (int i = 0; i < res.Count; i++)
                {
                    int pid = res[i];
                    ref var p = ref w.People.Get(pid);
                    if (p.At != p.Home || !CanTakeBack(in p, now)) continue;
                    ref var home = ref w.Settlements.Get(sid);
                    if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 21) >= Places.SettlementSystem.Over(TakeBackChance(in p, in home), days)) continue;
                    int to = TakeBackTarget(in p, in home, out double free);
                    if (to == 0 || KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 22) >= free) continue;
                    Take(ref p, to);
                    if (p.CargoQty > 0f) w.Trade.Unload(ref p, sid);
                }
        }
    }
}
