using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Core.Agents
{
    public static class JourneyOutcome
    {
        public const int TurnedBack = 1, Waited = 2, Continued = 3, Resumed = 4, GaveUp = 5;
    }

    /// <summary>
    /// The ACTIVITY layer for people (07): decisions by candidate scoring at step ends, analytic local walks,
    /// stays and route travel, effects at step ends, interrupts from perceived dangers. The current step's
    /// end is each person's only pending step event (versions cancel the rest lazily).
    /// </summary>
    public sealed class AgentSystem
    {
        World _w;
        readonly List<(int cell, long tEnter)> _leg = new List<(int, long)>();
        readonly List<int> _tmp = new List<int>();

        public void Bind(World w) { _w = w; }

        // ---------------------------------------------------------------- body and motion
        public double Speed(in Person p, long now)
        {
            double age = p.AgeYears(now);
            double f = 0.85 + 0.3 * (p.Pace / 255.0);
            if (age < 6) f *= 0.6; else if (age < 12) f *= 0.85; else if (age > 60) f *= 0.75;
            f *= 0.6 + 0.4 * p.Health;
            return _w.Config.WalkSpeed * f;
        }

        public static void LocalAt(in Person p, double t, out double x, out double y)
        {
            if ((p.Step == StepKind.Local || p.Step == StepKind.Stay) && t < p.TA)
            {
                double span = p.TA - p.T0;
                double u = span > 0 ? DMath.Clamp01((t - p.T0) / span) : 1.0;
                x = p.FromX + (p.ToX - p.FromX) * u;
                y = p.FromY + (p.ToY - p.FromY) * u;
            }
            else { x = p.ToX; y = p.ToY; }
        }

        public static double RouteSAt(in Person p, double t)
        {
            double span = p.T1 - p.T0;
            double u = span > 0 ? DMath.Clamp01((t - p.T0) / span) : 1.0;
            return p.S0 + (p.S1 - p.S0) * u;
        }

        /// <summary>World direction (unit vector) of a person at time t (seconds, fractional allowed).</summary>
        public Vec3d PositionAt(in Person p, double t)
        {
            if (p.Step == StepKind.Travel)
            {
                var r = _w.Routes.Get(_w, p.RouteA, p.RouteB);
                if (r != null) return r.PointAt(RouteSAt(p, t));
            }
            int sid = p.At != 0 ? p.At : p.Home;
            if (!_w.Settlements.Exists(sid)) return Vec3d.UnitY;
            ref var s = ref _w.Settlements.Get(sid);
            LocalAt(p, t, out double x, out double y);
            return s.LocalToSphere(x, y, _w.Planet.Radius);
        }

        // ---------------------------------------------------------------- creation
        /// <summary>Creates a person and schedules their first decision (creation always schedules, TC1 D-176).</summary>
        public int CreatePerson(byte sex, long born, int mother, int father, int household, int home, long firstDecision)
        {
            var w = _w;
            int id = w.Ids.Next(EntityKind.Person);
            ulong seed = w.Seed;
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Traits, (ulong)id));
            var p = new Person
            {
                Id = id, SexKind = sex, Alive = true, Born = born, Mother = mother, Father = father, Household = household,
                Home = home, At = home, LastMeal = w.Now, Woke = w.Now, Health = 1f, Nutrition = 1f,
                Chrono = (byte)r.NextBelow(256), Pace = (byte)r.NextBelow(256), Diligence = (byte)r.NextBelow(256),
                Social = (byte)r.NextBelow(256), Bold = (byte)r.NextBelow(256), Curious = (byte)r.NextBelow(256),
                SkFarm = (float)(0.2 + 0.3 * r.NextUnit()), SkWood = (float)(0.2 + 0.3 * r.NextUnit()),
            };
            ref var hh = ref w.Households.Get(household);
            p.FromX = hh.HomeX; p.FromY = hh.HomeY; p.ToX = hh.HomeX; p.ToY = hh.HomeY;
            p.Step = StepKind.Local; p.Act = ActivityKind.Idle; p.T0 = w.Now; p.TA = w.Now; p.T1 = Math.Max(firstDecision, w.Now + 1);
            w.People.Add(id, p);
            w.Residents(home).InsertSorted(id);
            w.Members(household).InsertSorted(id);
            w.History.Register(EntityRef.Person(id), Worldgen.Names.Person(seed, id, sex == Sex.Female) + " " + w.FamilyName(household), born);
            ref var pr = ref w.People.Get(id);
            if (w.Resolution == Simulation.Resolution.Activity) Schedule(in pr);
            else { pr.Step = StepKind.None; pr.T1 = w.Now; }
            w.Settlements.Get(home).Population++;
            return id;
        }

        public void Schedule(in Person p)
        {
            _w.Queue.Push(p.T1, Phase.Agent, EventKind.AgentStep, p.Id, p.ActVer);
        }

        // ---------------------------------------------------------------- steps
        /// <summary>One event per local activity: walk to (tx, ty) (analytic), then do it there for stay seconds.</summary>
        void BeginLocal(ref Person p, ActivityKind act, WhyCode why, int whyArg, double tx, double ty, long stay)
        {
            long now = _w.Now;
            LocalAt(p, now, out double x, out double y);
            p.Act = act; p.Why = why; p.WhyArg = whyArg; p.ActVer++;
            double dx = tx - x, dy = ty - y;
            double d = DMath.Sqrt(dx * dx + dy * dy);
            long walk = d > 2.0 ? Math.Max(1L, DMath.ToLong(DMath.RoundHalfUp(d / Speed(p, now)))) : 0;
            p.Step = StepKind.Local;
            p.FromX = (float)x; p.FromY = (float)y; p.ToX = (float)tx; p.ToY = (float)ty;
            p.T0 = now; p.TA = now + walk; p.T1 = p.TA + Math.Max(60L, stay);
            p.StayDur = 0;
            Schedule(in p);
            _w.Metrics.Activities++;
        }

        /// <summary>Starts walking a route from arc length sFrom to sTo (either direction).</summary>
        void BeginTravel(ref Person p, ActivityKind act, WhyCode why, Route r, double sFrom, double sTo, int dest)
        {
            long now = _w.Now;
            if (p.At != 0 && p.At != p.Home) _w.Visitors(p.At).Remove(p.Id);
            p.At = 0;
            p.Act = act; p.Why = why; p.WhyArg = dest; p.Dest = dest; p.ActVer++;
            p.Step = StepKind.Travel;
            p.RouteA = r.A; p.RouteB = r.B;
            p.S0 = (float)sFrom; p.S1 = (float)sTo;
            double len = DMath.Abs(sTo - sFrom);
            p.T0 = now;
            p.T1 = now + Math.Max(1L, DMath.ToLong(DMath.RoundHalfUp(len / Speed(p, now))));
            Schedule(in p);
            RegisterLeg(ref p, r);
            _w.Metrics.Journeys++;
        }

        /// <summary>A traveller standing still on the road (waiting), as a travel step with S0 = S1.</summary>
        void BeginRoadWait(ref Person p, Route r, double s, long dur, int dest)
        {
            long now = _w.Now;
            p.At = 0;
            p.Act = ActivityKind.Wait; p.Why = WhyCode.WaitingForFire; p.Dest = dest; p.ActVer++;
            p.Step = StepKind.Travel; p.RouteA = r.A; p.RouteB = r.B; p.S0 = (float)s; p.S1 = (float)s;
            p.T0 = now; p.T1 = now + dur;
            Schedule(in p);
            RegisterLeg(ref p, r);
        }

        void RegisterLeg(ref Person p, Route r)
        {
            _leg.Clear();
            double s0 = p.S0, s1 = p.S1;
            double lo = Math.Min(s0, s1), hi = Math.Max(s0, s1);
            double span = hi - lo;
            int kStart = r.Segment(lo), kEnd = r.Segment(hi);
            bool forward = s1 >= s0;
            int count = kEnd - kStart + 1;
            int lastCell = -1; long lastEnter = 0, lastExit = 0;
            for (int n = 0; n < count; n++)
            {
                int k = forward ? kStart + n : kEnd - n;
                double a = Math.Max(lo, r.S[k]), b = Math.Min(hi, r.S[k + 1]);
                long tA, tB;
                if (span <= 0) { tA = p.T0; tB = p.T1; }
                else
                {
                    tA = p.T0 + DMath.ToLong((DMath.Abs(a - s0) / span) * (p.T1 - p.T0));
                    tB = p.T0 + DMath.ToLong((DMath.Abs(b - s0) / span) * (p.T1 - p.T0));
                }
                long enter = Math.Min(tA, tB), exit = Math.Max(tA, tB);
                int cell = r.Cell[k];
                if (cell == lastCell) { lastExit = Math.Max(lastExit, exit); continue; }
                if (lastCell >= 0) { _w.Travel.Add(lastCell, p.Id, p.ActVer, lastEnter, lastExit); _leg.Add((lastCell, lastEnter)); }
                lastCell = cell; lastEnter = enter; lastExit = exit;
            }
            if (lastCell >= 0) { _w.Travel.Add(lastCell, p.Id, p.ActVer, lastEnter, lastExit); _leg.Add((lastCell, lastEnter)); }
            _w.Hazards.CheckNewLeg(_w, p.Id, _leg);
        }

        /// <summary>Re-registers the current travel leg of everyone travelling (after a load: the index is derived).</summary>
        public void RebuildTravelIndex()
        {
            _w.Travel.Clear();
            for (int id = _w.People.First(); id != 0; id = _w.People.Next(id))
            {
                ref var p = ref _w.People.Get(id);
                if (!p.Alive || p.Step != StepKind.Travel) continue;
                var r = _w.Routes.Get(_w, p.RouteA, p.RouteB);
                if (r == null) continue;
                // the same entries RegisterLeg made, without re-checking hazards (their interrupts are in the queue)
                double s0 = p.S0, s1 = p.S1, lo = Math.Min(s0, s1), hi = Math.Max(s0, s1), span = hi - lo;
                int kStart = r.Segment(lo), kEnd = r.Segment(hi);
                bool forward = s1 >= s0; int count = kEnd - kStart + 1;
                int lastCell = -1; long lastEnter = 0, lastExit = 0;
                for (int n = 0; n < count; n++)
                {
                    int k = forward ? kStart + n : kEnd - n;
                    double a = Math.Max(lo, r.S[k]), b = Math.Min(hi, r.S[k + 1]);
                    long tA, tB;
                    if (span <= 0) { tA = p.T0; tB = p.T1; }
                    else
                    {
                        tA = p.T0 + DMath.ToLong((DMath.Abs(a - s0) / span) * (p.T1 - p.T0));
                        tB = p.T0 + DMath.ToLong((DMath.Abs(b - s0) / span) * (p.T1 - p.T0));
                    }
                    long enter = Math.Min(tA, tB), exit = Math.Max(tA, tB);
                    int cell = r.Cell[k];
                    if (cell == lastCell) { lastExit = Math.Max(lastExit, exit); continue; }
                    if (lastCell >= 0) _w.Travel.Add(lastCell, p.Id, p.ActVer, lastEnter, lastExit);
                    lastCell = cell; lastEnter = enter; lastExit = exit;
                }
                if (lastCell >= 0) _w.Travel.Add(lastCell, p.Id, p.ActVer, lastEnter, lastExit);
            }
        }

        // ---------------------------------------------------------------- events
        public void OnStep(int pid, int ver)
        {
            var w = _w;
            if (!w.People.Exists(pid)) { w.Metrics.Stale++; return; }
            ref var p = ref w.People.Get(pid);
            if (!p.Alive || ver != p.ActVer) { w.Metrics.Stale++; return; }
            w.Metrics.Steps++;
            switch (p.Step)
            {
                case StepKind.Local:
                case StepKind.Stay:
                    p.FromX = p.ToX; p.FromY = p.ToY;
                    Complete(ref p, 1.0);
                    Decide(ref p);
                    return;
                case StepKind.Travel:
                    OnTravelEnd(ref p);
                    return;
                default:
                    Decide(ref p);
                    return;
            }
        }

        void OnTravelEnd(ref Person p)
        {
            var w = _w;
            var r = w.Routes.Get(w, p.RouteA, p.RouteB);
            if (p.Act == ActivityKind.Wait && r != null)
            {
                // finished waiting on the road: go on if the way ahead is clear, else back, else wait again
                double s = p.S0;
                double toward = p.Dest == r.A ? 0.0 : r.Length;
                double back = p.Dest == r.A ? r.Length : 0.0;
                if (!FireNearRoute(r, s, toward)) { Record(ref p, JourneyOutcome.Resumed, p.Alarm); BeginTravel(ref p, p.Dest == p.Home ? ActivityKind.TripHome : ActivityKind.TripOut, WhyCode.GoingHome, r, s, toward, p.Dest); return; }
                int backDest = p.Dest == r.A ? r.B : r.A;
                if (!FireNearRoute(r, s, back)) { Record(ref p, JourneyOutcome.TurnedBack, p.Alarm); BeginTravel(ref p, backDest == p.Home ? ActivityKind.TripHome : ActivityKind.TripOut, WhyCode.FireAhead, r, s, back, backDest); return; }
                if (w.Now >= p.Until)
                {
                    // waited long enough: go home whatever the smoke (a person stuck on a road does not starve there)
                    int homeEnd = p.Home == r.A ? 0 : (p.Home == r.B ? 1 : -1);
                    double sHome = homeEnd == 0 ? 0.0 : r.Length;
                    int destGo = homeEnd >= 0 ? p.Home : (p.Dest == r.A ? r.B : r.A);
                    if (homeEnd < 0) sHome = destGo == r.A ? 0.0 : r.Length;
                    Record(ref p, JourneyOutcome.GaveUp, p.Alarm);
                    BeginTravel(ref p, destGo == p.Home ? ActivityKind.TripHome : ActivityKind.TripOut, WhyCode.GaveUpWaiting, r, s, sHome, destGo);
                    return;
                }
                BeginRoadWait(ref p, r, s, SimTime.Hour, p.Dest);
                return;
            }
            // arrival
            int arrived = r == null ? p.Home : (p.S1 <= 0.5f ? r.A : r.B);
            if (!w.Settlements.Exists(arrived)) arrived = p.Home;
            p.At = arrived;
            ref var s2 = ref w.Settlements.Get(arrived);
            double ax = s2.SquareX, ay = s2.SquareY;
            if (arrived == p.Home && w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); ax = hh.HomeX; ay = hh.HomeY; }
            p.Step = StepKind.Local; p.FromX = (float)ax; p.FromY = (float)ay; p.ToX = p.FromX; p.ToY = p.FromY; p.TA = w.Now; p.T0 = w.Now;
            if (arrived != p.Home)
            {
                w.Visitors(arrived).InsertSorted(p.Id);
                var rs = NextRandom(ref p);
                p.Until = w.Now + SimTime.Hours(1.5 + 3.0 * rs.NextUnit());
                p.Visits++;
            }
            p.Dest = 0;
            Decide(ref p);
        }

        public void OnInterrupt(int pid, int ver, long a, long cause)
        {
            var w = _w;
            if (!w.People.Exists(pid)) return;
            ref var p = ref w.People.Get(pid);
            if (!p.Alive) return;
            int reason = (int)(a & 0xFF);
            int place = (int)(a >> 8);
            if (reason == InterruptReason.FireSeenOnRoad && ver != p.ActVer) { w.Metrics.Stale++; return; }
            if (reason == InterruptReason.FireSeenAtPlace && p.At != place) { w.Metrics.Stale++; return; }
            p.Alarm = cause; p.AlarmT = w.Now;
            w.Metrics.InterruptsHandled++;
            if (p.Step == StepKind.Travel)
            {
                var r = w.Routes.Get(w, p.RouteA, p.RouteB);
                if (r == null) return;
                double s = RouteSAt(p, w.Now);
                double end = p.S1, start = p.S0;
                if (p.Act == ActivityKind.Wait) return; // already waiting: the wait's end re-decides
                if (!FireNearRoute(r, s, end)) return; // behind or beside: carry on (not history)
                int fromSettlement = start <= 0.5 ? r.A : r.B;
                if (!FireNearRoute(r, s, start))
                {
                    Record(ref p, JourneyOutcome.TurnedBack, cause);
                    BeginTravel(ref p, fromSettlement == p.Home ? ActivityKind.TripHome : ActivityKind.TripOut, WhyCode.FireAhead, r, s, start, fromSettlement);
                }
                else
                {
                    Record(ref p, JourneyOutcome.Waited, cause);
                    p.Until = w.Now + 3 * SimTime.Hour;
                    BeginRoadWait(ref p, r, s, SimTime.Hour, p.Dest != 0 ? p.Dest : (end <= 0.5 ? r.A : r.B));
                }
                return;
            }
            // at a place: cut what they are doing (partial effects), go to shelter
            CutStep(ref p);
            Decide(ref p);
        }

        void Record(ref Person p, int outcome, long cause)
        {
            _w.Record(new HistoryEvent
            {
                Kind = HistoryKind.JourneyInterrupted, Time = _w.Now, Cell = _w.Planet.Grid.CellOf(PositionAt(p, _w.Now)),
                Subject = EntityRef.Person(p.Id), Object = EntityRef.Settlement(p.Dest), Cause1 = cause, Rel1 = CauseRel.InResponseTo,
                ArgA = outcome,
            });
            _w.Metrics.JourneysInterrupted++;
        }

        /// <summary>Is any burning cell close to the route between arc lengths a and b?</summary>
        public bool FireNearRoute(Route r, double a, double b)
        {
            var fire = _w.Fire;
            if (fire.Active.Count == 0) return false;
            var P = _w.Planet;
            double lim = P.CellSize * 1.6;
            double lo = Math.Min(a, b), hi = Math.Max(a, b);
            int k0 = r.Segment(lo), k1 = r.Segment(hi);
            for (int f = 0; f < fire.Active.Count; f++)
            {
                Vec3d fp = P.Grid.Center[fire.Active[f]];
                for (int k = k0; k <= k1 + 1 && k < r.Pts.Length; k++)
                    if (Vec3d.ArcDistance(fp, r.Pts[k], P.Radius) <= lim) return true;
            }
            return false;
        }

        void CutStep(ref Person p)
        {
            long now = _w.Now;
            if (p.Step == StepKind.Local || p.Step == StepKind.Stay)
            {
                double worked = now > p.TA ? (now - p.TA) / (double)Math.Max(1L, p.T1 - p.TA) : 0.0;
                if (worked > 0) Complete(ref p, DMath.Clamp01(worked));
                LocalAt(p, now, out double x, out double y);
                p.FromX = (float)x; p.FromY = (float)y; p.ToX = p.FromX; p.ToY = p.FromY;
                p.TA = now;
            }
            p.T1 = now;
        }

        // ---------------------------------------------------------------- effects
        void Complete(ref Person p, double frac)
        {
            var w = _w;
            long now = w.Now;
            double hours = (p.T1 - Math.Max(p.T0, p.TA)) * frac / (double)SimTime.Hour;
            switch (p.Act)
            {
                case ActivityKind.Eat:
                    if (frac >= 0.5) EatNow(ref p);
                    break;
                case ActivityKind.Fields:
                    if (p.At != 0)
                    {
                        ref var s = ref w.Settlements.Get(p.At);
                        s.CropWork += hours * (0.6 + p.SkFarm) * (0.5 + 0.5 * p.Health);
                        p.SkFarm = (float)DMath.Min(1.0, p.SkFarm + 0.0015 * hours);
                    }
                    break;
                case ActivityKind.Wood:
                    if (p.At != 0)
                    {
                        ref var s = ref w.Settlements.Get(p.At);
                        s.Woodstore += hours * (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health);
                        p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * hours);
                    }
                    break;
                case ActivityKind.Sleep:
                    p.Woke = now;
                    break;
                case ActivityKind.Forage:
                    if (p.At != 0)
                    {
                        // wild food from the meadows and woods around the place
                        var P = w.Planet;
                        ref var s = ref w.Settlements.Get(p.At);
                        double wild = 0.5 * P.Grass[s.Cell] + 0.8 * P.Forest[s.Cell];
                        double found = hours * 0.28 * DMath.Clamp01(wild) * (0.6 + 0.4 * p.Health);
                        if (w.Households.Exists(p.Household)) w.Households.Get(p.Household).Larder += found;
                        else s.Granary += found;
                    }
                    break;
            }
        }

        double Portion(in Person p) => _w.Config.FoodPerAdultDay / _w.Config.MealsPerDay * AgeFood(p.AgeYears(_w.Now));
        static double AgeFood(double age) => age < 2 ? 0.35 : (age < 12 ? 0.6 : (age > 60 ? 0.8 : 1.0));

        bool FoodAvailable(in Person p)
        {
            var w = _w;
            if (p.At == p.Home && w.Households.Exists(p.Household) && w.Households.Get(p.Household).Larder > 0.01) return true;
            return p.At != 0 && w.Settlements.Get(p.At).Granary > 0.01;
        }

        internal void EatNow(ref Person p)
        {
            var w = _w;
            double need = Portion(p);
            double got = 0;
            if (p.At == p.Home && w.Households.Exists(p.Household))
            {
                ref var hh = ref w.Households.Get(p.Household);
                double take = Math.Min(need, hh.Larder);
                hh.Larder -= take; got += take;
            }
            if (got < need - 1e-9 && p.At != 0)
            {
                ref var s = ref w.Settlements.Get(p.At);
                double take = Math.Min(need - got, s.Granary);
                s.Granary -= take; got += take;
            }
            double share = need > 0 ? got / need : 1.0;
            p.Nutrition = (float)(0.85 * p.Nutrition + 0.15 * share);
            if (share >= 0.4)
            {
                p.LastMeal = w.Now;
                p.HungryFrom = 0;
            }
            w.Metrics.Meals++;
        }

        RandomSequence NextRandom(ref Person p)
        {
            p.RngCount++;
            return new RandomSequence(KeyedRng.Hash(_w.Seed, RngStream.Decide, (ulong)p.Id, p.RngCount));
        }

        // ---------------------------------------------------------------- decisions
        public void Decide(ref Person p)
        {
            var w = _w;
            long now = w.Now;
            w.Metrics.Decisions++;
            if (p.At == 0) { p.At = p.Home; }
            ref var here = ref w.Settlements.Get(p.At);
            double age = p.AgeYears(now);
            long lsec = SimTime.FloorMod(now + w.LocalOffsetOf(p.At), SimTime.Day);
            // integer seconds: a fractional wake time made sleepers wake one second "before morning" and sleep a whole day more
            long shift = DMath.ToLong(DMath.Floor((p.Chrono - 128) / 128.0 * 1.2 * SimTime.Hour));
            bool child = age < 12;
            long wake = (child ? 6 * SimTime.Hour + 30 * SimTime.Minute : 5 * SimTime.Hour + 30 * SimTime.Minute) + shift;
            long bed = (child ? 20 * SimTime.Hour : 21 * SimTime.Hour + 30 * SimTime.Minute) + shift;
            bool night = lsec < wake || lsec >= bed;
            bool atHome = p.At == p.Home;
            double homeX = here.SquareX, homeY = here.SquareY;
            if (atHome && w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); homeX = hh.HomeX; homeY = hh.HomeY; }
            var rs = NextRandom(ref p);

            // 0. danger seen and still burning near this place: keep to shelter
            if (p.Alarm != 0 && now - p.AlarmT < 8 * SimTime.Hour && w.FireNearSettlement(p.At, w.Config.FirePerceptionRange))
            {
                BeginLocal(ref p, ActivityKind.Flee, WhyCode.FireNear, 0, homeX, homeY, SimTime.Hour);
                return;
            }

            // 1. night: sleep until the morning
            if (night)
            {
                long untilWake = wake - lsec;
                if (untilWake <= 0) untilWake += SimTime.Day;
                BeginLocal(ref p, ActivityKind.Sleep, WhyCode.Night, 0, homeX, homeY, untilWake);
                return;
            }

            // 2. infants stay with the household and are fed there
            if (age < 2)
            {
                if (now - p.LastMeal > 4 * SimTime.Hour && FoodAvailable(p)) EatNow(ref p);
                BeginLocal(ref p, ActivityKind.Infant, WhyCode.Young, 0, homeX, homeY, 4 * SimTime.Hour);
                return;
            }

            // 3. hunger
            if (now - p.LastMeal > 5 * SimTime.Hour)
            {
                if (FoodAvailable(p))
                {
                    BeginLocal(ref p, ActivityKind.Eat, WhyCode.Hungry, 0, homeX, homeY, 25 * SimTime.Minute);
                    return;
                }
                if (p.HungryFrom == 0 && now - p.LastMeal > SimTime.Day) p.HungryFrom = now;
                // nothing at home or in the store: go and gather what the land offers (by day)
                if (!child && lsec >= 6 * SimTime.Hour && lsec < 19 * SimTime.Hour && p.At == p.Home)
                {
                    double fx = here.WoodX * 0.7 + rs.Range(-80, 80), fy = here.WoodY * 0.7 + rs.Range(-80, 80);
                    BeginLocal(ref p, ActivityKind.Forage, WhyCode.NoFood, 0, fx, fy, SimTime.Hours(2.5 + rs.NextUnit()));
                    return;
                }
            }

            // 4. visiting elsewhere: stay a while, then go home
            if (!atHome)
            {
                if (now >= p.Until || lsec > 16 * SimTime.Hour)
                {
                    var r = w.Routes.Get(w, p.At, p.Home);
                    if (r != null && !FireNearRoute(r, 0, r.Length))
                    {
                        double from = p.At == r.A ? 0.0 : r.Length, to = p.Home == r.A ? 0.0 : r.Length;
                        BeginTravel(ref p, ActivityKind.TripHome, WhyCode.GoingHome, r, from, to, p.Home);
                        return;
                    }
                }
                long stay = Math.Max(20 * SimTime.Minute, Math.Min(SimTime.Hour, p.Until - now));
                BeginLocal(ref p, ActivityKind.Visit, WhyCode.VisitKin, 0, here.SquareX + rs.Range(-15, 15), here.SquareY + rs.Range(-15, 15), stay);
                return;
            }

            // 5. children play near home or the square
            if (child)
            {
                bool sq = rs.Chance(0.5);
                double px = sq ? here.SquareX : homeX, py = sq ? here.SquareY : homeY;
                BeginLocal(ref p, ActivityKind.Play, WhyCode.Young, 0, px + rs.Range(-20, 20), py + rs.Range(-20, 20), SimTime.Hours(2 + 1.5 * rs.NextUnit()));
                return;
            }

            // 6. grown-ups: score what could be done now
            bool day = lsec >= 6 * SimTime.Hour && lsec < 19.5 * SimTime.Hour;
            double elder = age > 60 ? 0.5 : 1.0;
            double best = 0.08 + 0.05 * rs.NextUnit();
            int choice = 0; // 0 idle, 1 fields, 2 wood, 3 water, 4 social, 5 trip
            int tripDest = 0;
            if (day && here.CropWorkNeed > here.CropWork)
            {
                double need = here.CropWorkNeed - here.CropWork;
                double u = (here.Crop == CropStage.Ripe ? 1.1 : 0.55 + 0.4 * DMath.Clamp01(need / Math.Max(1, here.CropWorkNeed))) * elder * (0.6 + 0.4 * p.Diligence / 255.0);
                u += 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 1; }
            }
            double woodTarget = 6.0 * Math.Max(1, here.Population);
            if (day && here.Woodstore < woodTarget)
            {
                double u = 0.5 * (1.0 - here.Woodstore / woodTarget) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 2; }
            }
            if (day && rs.Chance(0.12)) { double u = 0.3 + 0.1 * rs.NextUnit(); if (u > best) { best = u; choice = 3; } }
            if (lsec >= 7 * SimTime.Hour && lsec < 13 * SimTime.Hour && age >= 16 && age < 62 && p.Health > 0.7 &&
                !(p.PregDue != 0 && p.PregDue - now < 10 * SimTime.Day) && !(p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) &&
                !(p.Alarm != 0 && now - p.AlarmT < SimTime.Day) &&
                rs.Chance(w.Config.TripChancePerDecision * (0.4 + 1.2 * p.Social / 255.0)))
            {
                tripDest = w.PickNeighbour(p.At, ref rs);
                if (tripDest != 0) { double u = 0.95; if (u > best) { best = u; choice = 5; } }
            }
            if (lsec >= 17 * SimTime.Hour) { double u = 0.25 + 0.3 * p.Social / 255.0 + 0.1 * rs.NextUnit(); if (u > best) { best = u; choice = 4; } }

            switch (choice)
            {
                case 1:
                    BeginLocal(ref p, ActivityKind.Fields, here.Crop == CropStage.Ripe ? WhyCode.Harvest : WhyCode.CropNeedsWork, 0,
                        here.FieldX + rs.Range(-60, 60), here.FieldY + rs.Range(-60, 60), SimTime.Hours(3 + 1.5 * rs.NextUnit()));
                    return;
                case 2:
                    BeginLocal(ref p, ActivityKind.Wood, WhyCode.WoodLow, 0, here.WoodX + rs.Range(-50, 50), here.WoodY + rs.Range(-50, 50), SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
                    return;
                case 3:
                    BeginLocal(ref p, ActivityKind.Water, WhyCode.WaterNeeded, 0, here.WellX, here.WellY, 20 * SimTime.Minute);
                    return;
                case 4:
                    BeginLocal(ref p, ActivityKind.Social, WhyCode.Leisure, 0, here.SquareX + rs.Range(-12, 12), here.SquareY + rs.Range(-12, 12), SimTime.Hours(1.5 + 1.5 * rs.NextUnit()));
                    return;
                case 5:
                    {
                        var r = w.Routes.Get(w, p.At, tripDest);
                        if (r != null)
                        {
                            double from = p.At == r.A ? 0.0 : r.Length, to = tripDest == r.A ? 0.0 : r.Length;
                            BeginTravel(ref p, ActivityKind.TripOut, WhyCode.VisitFriend, r, from, to, tripDest);
                            return;
                        }
                        break;
                    }
            }
            BeginLocal(ref p, ActivityKind.Idle, WhyCode.Leisure, 0, homeX + rs.Range(-6, 6), homeY + rs.Range(-6, 6), SimTime.Hours(1.5 + 1.5 * rs.NextUnit()));
        }

        // ---------------------------------------------------------------- COARSE resolution (04 §4.3)
        /// <summary>Everyone stops what they are doing and is at home; nobody has a step event in COARSE.</summary>
        public void EnterCoarse()
        {
            var w = _w;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                if (p.Step == StepKind.Local || p.Step == StepKind.Stay) CutStep(ref p);
                if (p.At != 0 && p.At != p.Home) w.Visitors(p.At).Remove(p.Id);
                p.At = p.Home;
                p.Act = ActivityKind.Idle; p.Step = StepKind.None; p.Why = WhyCode.None; p.Dest = 0;
                p.ActVer++; // cancels the pending step
                if (w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); p.FromX = hh.HomeX; p.FromY = hh.HomeY; p.ToX = hh.HomeX; p.ToY = hh.HomeY; }
                p.T0 = w.Now; p.TA = w.Now; p.T1 = w.Now;
            }
        }

        /// <summary>Back to ACTIVITY: everyone decides afresh within the hour (positions come from those decisions).</summary>
        public void LeaveCoarse()
        {
            var w = _w;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                long first = w.Now + 1 + (long)(KeyedRng.Unit(w.Seed, RngStream.Decide, (ulong)id, (ulong)w.Now, 77) * SimTime.Hour);
                p.Step = StepKind.Local; p.Act = ActivityKind.Idle; p.T0 = w.Now; p.TA = w.Now; p.T1 = first;
                p.ActVer++;
                Schedule(in p);
            }
        }

        /// <summary>
        /// A settlement's people for one day in COARSE: the same work priorities and rates as the decision generators
        /// (crop stage needs, then wood, foraging when stores are empty), and three rationed meals per person.
        /// </summary>
        public void CoarseDay(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            var res = w.Residents(sid);
            long now = w.Now;
            double cropNeed = Math.Max(0, s.CropWorkNeed - s.CropWork);
            double woodTarget = 6.0 * Math.Max(1, s.Population);
            bool noFood = s.Granary < 1.0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                double age = p.AgeYears(now);
                // meals: three portions from the larder, then the granary
                for (int m = 0; m < 3; m++) EatNow(ref p);
                if (age < 16 || p.Health < 0.3) continue;
                double hours = 7.5 * (0.6 + 0.4 * p.Diligence / 255.0) * (age > 60 ? 0.5 : 1.0) * (0.5 + 0.5 * p.Health);
                if (cropNeed > 0)
                {
                    double rate = (0.6 + p.SkFarm) * (0.5 + 0.5 * p.Health);
                    double h = Math.Min(hours, cropNeed / rate);
                    s.CropWork += h * rate; cropNeed -= h * rate; hours -= h;
                    p.SkFarm = (float)DMath.Min(1.0, p.SkFarm + 0.0015 * h);
                }
                if (hours > 0 && noFood)
                {
                    var P = w.Planet;
                    double wild = 0.5 * P.Grass[s.Cell] + 0.8 * P.Forest[s.Cell];
                    double fh = Math.Min(hours, 7.0);
                    double found = fh * 0.28 * DMath.Clamp01(wild) * (0.6 + 0.4 * p.Health);
                    if (w.Households.Exists(p.Household)) w.Households.Get(p.Household).Larder += found;
                    hours -= fh;
                }
                if (hours > 0 && s.Woodstore < woodTarget)
                {
                    double h = hours * 0.4;
                    s.Woodstore += h * (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health);
                    p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * h);
                }
            }
        }

        // ---------------------------------------------------------------- premises (tests, tools)
        /// <summary>
        /// Premise: the person sets out now for another settlement, as if they had chosen to (a state the
        /// world reaches by itself). Returns false if they cannot (not at a place, no route).
        /// </summary>
        public bool StartTrip(int pid, int dest)
        {
            var w = _w;
            ref var p = ref w.People.Get(pid);
            if (!p.Alive || p.At == 0 || p.At == dest) return false;
            var r = w.Routes.Get(w, p.At, dest);
            if (r == null) return false;
            CutStep(ref p);
            double from = p.At == r.A ? 0.0 : r.Length, to = dest == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, dest == p.Home ? ActivityKind.TripHome : ActivityKind.TripOut, WhyCode.VisitFriend, r, from, to, dest);
            return true;
        }

        // ---------------------------------------------------------------- death
        public void Kill(ref Person p, long cause, int why)
        {
            var w = _w;
            if (!p.Alive) return;
            p.Alive = false; p.Died = w.Now; p.ActVer++;
            p.DeathEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.PersonDied, Time = w.Now, Cell = w.Planet.Grid.CellOf(PositionAt(p, w.Now)), Settlement = p.Home,
                Subject = EntityRef.Person(p.Id), Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)p.AgeYears(w.Now), ArgA = why,
                RootKind = cause == 0 ? RootKind.Nature : RootKind.Unknown,
            });
            w.History.EndEntity(EntityRef.Person(p.Id), w.Now);
            if (p.At != 0 && p.At != p.Home) w.Visitors(p.At).Remove(p.Id);
            w.Residents(p.Home).Remove(p.Id);
            if (w.Households.Exists(p.Household)) w.Members(p.Household).Remove(p.Id);
            if (p.Partner != 0 && w.People.Exists(p.Partner)) w.People.Get(p.Partner).Partner = 0;
            w.Settlements.Get(p.Home).Population--;
            w.Metrics.Deaths++;
        }
    }
}
