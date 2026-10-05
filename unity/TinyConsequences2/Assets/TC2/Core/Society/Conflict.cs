using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Society
{
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
                if (t.Granary < FullStoreDays * daily + LoadFu) continue;
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
        public double SeenAt(int o) => _w.Settlements.Get(o).Watcher != 0 ? DMath.Max(WatchSeen, SeenChance(o)) : SeenChance(o);

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
        static bool Fit(in Person q, long now)
        {
            if (!q.Alive || q.Sick != 0 || q.Hurt >= RoadHurt || q.Health <= 0.6) return false;
            double age = q.AgeYears(now);
            return age >= 16 && age < 60;
        }

        /// <summary>A body's strength in a struggle: its health and years, its hurt, and the metal its village holds (knives, axes).</summary>
        double Strength(in Person q)
        {
            var w = _w;
            double age = q.AgeYears(w.Now);
            double years = age < 18 ? 0.8 : age < 45 ? 1.0 : age < 60 ? 0.8 : 0.6;
            double armed = 0.0;
            if (w.Settlements.Exists(q.Home)) { ref var hs = ref w.Settlements.Get(q.Home); armed = Knowledge.Tech.MetalShare(hs.Metal, hs.Population); }
            return (double)q.Health * years * (1.0 - (double)q.Hurt) * (1.0 + 0.3 * armed);
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
        void Claims(int sid)
        {
            var w = _w;
            var list = w.DeedBook.KnownBy(sid);
            if (list == null) return;
            long now = w.Now;
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
                ref var c = ref w.People.Get(claimant);
                if (w.Resolution == Resolution.Coarse) Answer(ref c, doers, deed);
                else c.Errand = deed;
                return;
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
            if (!t.Inhabited || t.Granary < LoadFu || Vec3d.ArcDistance(home.Anchor, t.Anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, p.Home, o) == null) return 0;
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
        void News(int sid)
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
            double expect = adults * TC2.Core.Health.Disease.VisitsPerAdultDay / near;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid || !w.Settlements.Get(o).Inhabited) continue;
                ref var t = ref w.Settlements.Get(o);
                if (Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > 6000) continue;
                if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)sid, (ulong)now, (ulong)(40 + o)) < 1.0 - DMath.Exp(-expect)) w.DeedBook.Meet(sid, o);
            }
        }


        long HungerCause(int home, long now)
        {
            ref var s = ref _w.Settlements.Get(home);
            if (s.FamineEvent != 0 && now - s.FamineAt < SimTime.Year) return s.FamineEvent;
            return s.HungerEvent != 0 ? s.HungerEvent : s.RationEvent;
        }

        /// <summary>A village's night of taking at COARSE resolution: each hungry, fit grown person's lived chance, the act at once.</summary>
        public void Day(int sid)
        {
            var w = _w;
            Wounds(sid);
            Watch(sid);
            ReadFoe(sid);
            Claims(sid);
            if (w.Resolution != Resolution.Coarse) return;
            News(sid);
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
                    if (KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 21) >= TakeBackChance(in p, in home)) continue;
                    int to = TakeBackTarget(in p, in home, out double free);
                    if (to == 0 || KeyedRng.Unit(w.Seed, RngStream.Society, (ulong)pid, (ulong)now, 22) >= free) continue;
                    Take(ref p, to);
                    if (p.CargoQty > 0f) w.Trade.Unload(ref p, sid);
                }
        }
    }
}
