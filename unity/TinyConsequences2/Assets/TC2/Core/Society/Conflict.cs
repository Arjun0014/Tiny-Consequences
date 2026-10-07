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
        public byte Size, Motive, State;   // Motive 1 a wrong, 0 hunger; State 0 raised, 1 fallen
        public long Formed, Arrive, Event;
        public float Loot;

        public void Write(BinWriter w) { w.Int(Id); w.Int(Origin); w.Int(Target); w.Int(Leader); w.Byte(Size); w.Byte(Motive); w.Byte(State); w.Long(Formed); w.Long(Arrive); w.Long(Event); w.Float(Loot); }
        public static Band Read(BinReader r) => new Band { Id = r.Int(), Origin = r.Int(), Target = r.Int(), Leader = r.Int(), Size = r.Byte(), Motive = r.Byte(), State = r.Byte(), Formed = r.Long(), Arrive = r.Long(), Event = r.Long(), Loot = r.Float() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Origin); h.Add(Target); h.Add(Leader); h.Add(Size); h.Add(Motive); h.Add(State); h.Add(Formed); h.Add(Arrive); h.Add(Event); h.Add(Loot); }
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
            double armed = 0.0, arms = 0.0;
            if (w.Settlements.Exists(q.Home))
            {
                ref var hs = ref w.Settlements.Get(q.Home);
                armed = Knowledge.Tech.MetalShare(hs.Metal, hs.Population);
                arms = hs.Population > 0 ? DMath.Clamp01(hs.Arms / (Places.Forge.ArmsPerHead * hs.Population)) : 0.0;   // (a spear or an axe for its fighters: C1.4)
            }
            return (double)q.Health * years * (1.0 - (double)q.Hurt) * (1.0 + 0.3 * armed) * (1.0 + 0.3 * arms) * (1.0 + 0.6 * (double)q.SkFight);   // (trained: C1.5)
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
                if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)sid, (ulong)now, (ulong)(40 + o)) < 1.0 - DMath.Exp(-expect)) w.DeedBook.Meet(sid, o);
            }
        }


        // ------------------------------------------------------------------ raids (D2-073)
        readonly System.Collections.Generic.List<int> _band = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.List<int> _defend = new System.Collections.Generic.List<int>();

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
            int target = 0; byte motive = 0;
            double urge = 0;
            if (s.Foe != 0 && s.FoeWorth >= RaidFrom && LordOf(s.Foe) != sid) { target = s.Foe; motive = 1; urge = DMath.Clamp01((double)s.FoeWorth / RaidFull); }
            else if (hungry * 2 >= fit)
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
                double join = (0.15 + 0.7 * q.Bold / 255.0) * (kin ? 1.5 : 1.0) * (motive == 1 ? 1.0 : (q.Nutrition < HungryBelow ? 1.0 : 0.3));
                if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 51) < join) _band.Add(pid);
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
            long cause = motive == 1 ? s.FoeEvent : HungerCause(sid, now);
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.BandRaised, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(leader), Object = EntityRef.Settlement(target),
                Cause1 = cause, Rel1 = CauseRel.InResponseTo, ArgA = _band.Count, ArgB = motive, RootKind = RootKind.People,
            });
            w.Bands.Add(id, new Band { Id = id, Origin = sid, Target = target, Leader = leader, Size = (byte)_band.Count, Motive = motive, Formed = now, Arrive = arrive, Event = ev });
            for (int i = 0; i < _band.Count; i++) w.People.Get(_band[i]).Band = id;
            w.Metrics.Bands++;
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
            var res = w.Residents(b.Origin);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (q.Alive && q.Band == b.Id && (q.Id == p.Id || (q.Act == ActivityKind.TripOut && q.Why == WhyCode.Raid && q.Dest == at) || (q.At == at && q.Why == WhyCode.Raid))) _band.Add(res[i]);
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
            if (b2.Motive == 1 && loot > 0) w.DeedBook.SettleBy(origin, at, loot);
            w.Metrics.Raids++;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Raid, origin, at);
            w.Metrics.RaidLoot += loot;
            if (outcome == 2) w.Metrics.RaidsBeaten++;
            // COARSE: home at once with what they carry
            if (w.Resolution == Resolution.Coarse)
                for (int i = 0; i < n; i++) { ref var q = ref w.People.Get(raiders[i]); if (q.CargoQty > 0f) w.Trade.Unload(ref q, origin); q.Band = 0; }
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
        int FitAt(int sid, long now)
        {
            var w = _w;
            var res = w.Residents(sid);
            int n = 0;
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (Fit(in q, now) && q.At == sid) n++; }
            return n;
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
                var res = w.Residents(sid);
                for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (q.Band == b && (q.At == q.Home || !q.Alive)) q.Band = 0; }
                bool anyOut = false;
                for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Band == b) anyOut = true;
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
            return DMath.Clamp(far + 15.0, 25.0, 170.0);   // (its pens and yards behind the homes inside)
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
            int mine = FitAt(sid, now), theirs = FitAt(lord, now);
            s.Lore.Best(out float place);
            bool hemmed = place < 0.6f;                 // (no good land known to go to: flight is hard)
            bool weaker = theirs >= TributeOdds * mine;
            if (!(hemmed && weaker && cost > tribute && tribute > 0)) return;
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
            ref var s = ref w.Settlements.Get(sid);
            if (s.Lord == 0) return;
            int lord = s.Lord;
            bool gone = !w.Settlements.Exists(lord) || !w.Settlements.Get(lord).Inhabited;
            if (gone || FitAt(lord, now) < FitAt(sid, now))
            {
                long agreed = s.LordEvent;
                s.Lord = 0; s.LordEvent = 0;
                long ev = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.TributeEnded, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(lord),
                    Cause1 = agreed, Rel1 = CauseRel.PartOf, ArgA = gone ? 1 : 0, RootKind = RootKind.People,
                });
                if (!gone)
                {
                    int refusal = w.DeedBook.Record(DeedKind.Refusal, 0, sid, 0, lord, Trade.Grain, 0.0, TributeShare * DMath.Max(yield, s.LastHarvest), ev);
                    w.DeedBook.Learn(lord, refusal, KnownHow.Saw);
                    w.DeedBook.Learn(sid, refusal, KnownHow.Own);
                }
                w.Metrics.TributesEnded++;
                return;
            }
            double pay = DMath.Min(TributeShare * yield, s.Granary);
            if (pay <= 0) return;
            s.Granary -= pay;
            ref var l = ref w.Settlements.Get(lord);
            l.Granary += pay;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TributePaid, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Settlement(lord),
                Cause1 = s.LordEvent, Rel1 = CauseRel.PartOf, Magnitude = (float)pay, RootKind = RootKind.People,
            });
            w.DeedBook.SettleBy(lord, sid, pay);
            w.Metrics.TributeFu += pay;
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
