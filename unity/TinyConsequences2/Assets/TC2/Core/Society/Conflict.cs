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

        /// <summary>A person is hungry enough to go when the last day fed them less than this share of their need (both resolutions
        /// keep that share, the body's day: a lived meal-time flag would clear at the first bite).</summary>
        public const double HungryBelow = 0.5;

        /// <summary>Whether a person could go taking tonight: grown and fit, hungry, free of a babe and of sickness.</summary>
        public static bool CanTake(in Person p, long now)
        {
            if (!p.Alive || p.Nutrition >= HungryBelow || p.Sick != 0 || p.PregDue != 0) return false;
            if (p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) return false;
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
                double f = (1.0 - Restraint(in p, o)) * (1.0 - SeenChance(o));
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
            long cause = HungerCause(home, now);
            if (roll < SeenChance(at))
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
            if (w.Resolution != Resolution.Coarse) return;
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
        }
    }
}
