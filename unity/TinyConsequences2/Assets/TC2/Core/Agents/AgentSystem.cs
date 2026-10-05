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
            // the wild's skills are keyed by id, not drawn from the trait sequence, so every other draw stays where it was (D2-042)
            p.SkHunt = (float)(0.05 + 0.25 * KeyedRng.Unit(seed, RngStream.Traits, (ulong)id, 101));
            p.SkFish = (float)(0.05 + 0.25 * KeyedRng.Unit(seed, RngStream.Traits, (ulong)id, 102));
            p.Ground = -1;
            p.Reserve = (float)ReserveCap(in p, seed, w.Now);
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
        void BeginTravel(ref Person p, ActivityKind act, WhyCode why, Route r, double sFrom, double sTo, int dest, long arriveAt = 0)
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
            p.T1 = arriveAt > 0 ? Math.Max(now + 1, arriveAt) : now + Math.Max(1L, DMath.ToLong(DMath.RoundHalfUp(len / Speed(p, now))));
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
            // a scout at the far place: they look round, then walk home the way they came (D2-045)
            if (p.Act == ActivityKind.Scout && r != null && r.B < 0 && p.S1 > 0.5f)
            {
                w.Migration.Look(p.Home, RouteCache.CellOfEnd(r.B), p.Id);
                int back = r.A == p.Home ? p.Home : r.A;
                BeginTravel(ref p, back == p.Home ? ActivityKind.TripHome : ActivityKind.TripOut, WhyCode.GoingHome, r, r.Length, 0.0, back);
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
                case ActivityKind.Hunt:
                case ActivityKind.Fish:
                case ActivityKind.Gather:
                    if (p.Ground >= 0 && hours > 0)
                    {
                        // what they caught comes out of the land's own fields; they carry it home (D2-042)
                        ulong key = KeyedRng.Hash(w.Seed, RngStream.Work, (ulong)p.Id, (ulong)now);
                        double fu;
                        int kind = -1;
                        if (p.Act == ActivityKind.Hunt) { fu = w.Wild.Hunt(p.Ground, hours, p.SkHunt, key, out kind); p.SkHunt = (float)DMath.Min(1.0, p.SkHunt + 0.002 * hours); if (fu > 0) w.HerdSys.MarkHunted(p.Ground); }
                        else if (p.Act == ActivityKind.Fish) { fu = w.Wild.Fish(p.Ground, hours, p.SkFish, key); p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.002 * hours); if (fu > 0) kind = 64; }
                        else fu = w.Wild.Gather(p.Ground, hours, p.Health, key);
                        p.Carry = (float)(p.Carry + fu);
                        p.WhyArg = kind + 1;   // what they bring home (the view and the inspector read it from the haul)
                        w.Metrics.WildHours += hours; w.Metrics.WildFu += fu;
                    }
                    p.Ground = -1;
                    break;
                case ActivityKind.Haul:
                    if (p.Carry > 0f) { w.Wild.Deposit(p.Id, p.Carry); p.Carry = 0f; }
                    break;
                case ActivityKind.Build:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                        Construction.Work(w, p.At, p.Household, hours, BuildRate(in p));
                    break;
            }
        }

        static double AgeFood(double age) => age < 2 ? 0.35 : (age < 12 ? 0.6 : (age > 60 ? 0.8 : 1.0));

        /// <summary>FU a day a body needs (D2-042): by age, and a nursing mother's milk on top.</summary>
        public double DailyNeed(in Person p, long now)
        {
            double need = _w.Config.FoodPerAdultDay * AgeFood(p.AgeYears(now));
            if (p.NursingUntil > now) need += 0.25 * _w.Config.FoodPerAdultDay;
            return need;
        }

        /// <summary>
        /// Days of full food a body holds (D2-042): a grown body ~48 (x a build of 0.75-1.25, keyed by id), a child 16-34, an
        /// infant 8, the old less with age. A fasting adult lives 36-60 days, a child two to five weeks.
        /// </summary>
        public static double ReserveCap(in Person p, ulong seed, long now)
        {
            double age = p.AgeYears(now);
            double build = 0.75 + 0.5 * KeyedRng.Unit(seed, RngStream.Traits, (ulong)p.Id, 103);
            double days = age < 2 ? 8.0 : age < 12 ? 12.0 + 2.0 * age : age < 18 ? 36.0 + 2.0 * (age - 12) : age < 60 ? 48.0 : DMath.Max(20.0, 48.0 - 1.1 * (age - 60));
            return days * build;
        }

        /// <summary>A meal: what the hours since the last one burned (a share of the day's need), more while the body refills.</summary>
        double Portion(in Person p, long now)
        {
            double since = (now - p.LastMeal) / (double)SimTime.Day;
            double cap = ReserveCap(in p, _w.Seed, now);
            double refeed = 1.0 + 0.3 * DMath.Clamp01(1.0 - p.Reserve / cap);
            return DailyNeed(in p, now) * DMath.Clamp(since, 0.12, 0.55) * refeed;
        }

        bool FoodAvailable(in Person p)
        {
            var w = _w;
            if (p.At == p.Home && w.Households.Exists(p.Household) && w.Households.Get(p.Household).Larder > 0.01) return true;
            return p.At != 0 && w.Settlements.Get(p.At).Granary > 0.01;
        }

        internal void EatNow(ref Person p) => Eat(ref p, Portion(in p, _w.Now));

        /// <summary>A day's food at once (COARSE): the day's need, more while the body refills.</summary>
        internal void EatDay(ref Person p)
        {
            double cap = ReserveCap(in p, _w.Seed, _w.Now);
            Eat(ref p, DailyNeed(in p, _w.Now) * (1.0 + 0.3 * DMath.Clamp01(1.0 - p.Reserve / cap)));
        }

        void Eat(ref Person p, double need)
        {
            var w = _w;
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
            p.Ate = (float)(p.Ate + got);   // the body's account is kept once a day (Demography)
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

            // 0b. a catch reaches the larder when its carrier gets home with it (D2-042)
            if (p.Carry > 0f && p.At == p.Home)
            {
                BeginLocal(ref p, ActivityKind.Haul, WhyCode.BringHome, p.WhyArg, homeX, homeY, 10 * SimTime.Minute);
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
                // nothing at home or in the store: go out to what the woods, the game or the shore still hold (by day; D2-042)
                if (!child && lsec >= 6 * SimTime.Hour && lsec < 19 * SimTime.Hour && p.At == p.Home)
                {
                    int wk = WildChoice(in p, in here, age, now, out _);
                    if (wk >= 0) { BeginWild(ref p, ref here, wk, WhyCode.NoFood, ref rs); return; }
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
            int choice = 0; // 0 idle, 1 fields, 2 wood, 3 water, 4 social, 5 trip, 6 the wild, 7 building, 8 scouting
            int tripDest = 0;
            if (day && here.CropWorkNeed > here.CropWork)
            {
                double need = here.CropWorkNeed - here.CropWork;
                double u = (here.Crop == CropStage.Ripe ? 1.1 : 0.55 + 0.4 * DMath.Clamp01(need / Math.Max(1, here.CropWorkNeed))) * elder * (0.6 + 0.4 * p.Diligence / 255.0);
                u += 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 1; }
            }
            double woodTarget = 6.0 * Math.Max(1, here.Population);
            bool ownRoof = false, woodShort = false;
            bool canBuild = atHome && Construction.HasWork(w, in here, p.Household, out ownRoof, out woodShort);
            if (day && (here.Woodstore < woodTarget || woodShort))
            {
                // (wood is wanted most when a roof or a work waits for it)
                double u = (woodShort ? 0.7 : 0.5 * (1.0 - here.Woodstore / woodTarget)) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 2; }
            }
            if (day && canBuild)
            {
                // a camp wants a roof now; a hut wants to become a house; the village wants its well and its granary (D2-045)
                byte shelter = w.Households.Exists(p.Household) ? w.Households.Get(p.Household).Shelter : Construction.House;
                double need = ownRoof ? (shelter == Construction.Camp ? 0.85 : 0.4) : 0.45;
                double u = need * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 7; }
            }
            if (day && rs.Chance(0.12)) { double u = 0.3 + 0.1 * rs.NextUnit(); if (u > best) { best = u; choice = 3; } }
            int wildKind = -1;
            if (day && lsec < 15 * SimTime.Hour)
            {
                // the woods, the game and the shore: by need (short rations, empty stores) and habit, as good as the grounds are
                int wk = WildChoice(in p, in here, age, now, out double expect);
                if (wk >= 0)
                {
                    double u = WildUrge(in p, in here, wk, expect) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.06 * rs.NextUnit();
                    if (u > best) { best = u; choice = 6; wildKind = wk; }
                }
            }
            if (lsec >= 7 * SimTime.Hour && lsec < 13 * SimTime.Hour && age >= 16 && age < 62 && p.Health > 0.7 &&
                !(p.PregDue != 0 && p.PregDue - now < 10 * SimTime.Day) && !(p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) &&
                !(p.Alarm != 0 && now - p.AlarmT < SimTime.Day) &&
                rs.Chance(w.Config.TripChancePerDecision * (0.4 + 1.2 * p.Social / 255.0)))
            {
                tripDest = w.PickNeighbour(p.At, ref rs);
                if (tripDest != 0) { double u = 0.95; if (u > best) { best = u; choice = 5; } }
            }
            if (lsec >= 17 * SimTime.Hour) { double u = 0.25 + 0.3 * p.Social / 255.0 + 0.1 * rs.NextUnit(); if (u > best) { best = u; choice = 4; } }
            if (lsec >= 6 * SimTime.Hour && lsec < 10 * SimTime.Hour && age >= 16 && age < 55 && p.Health > 0.7 && atHome &&
                !(p.PregDue != 0) && !(p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 4) && w.Migration.WantsScout(in here))
            {
                // a bold one goes to see the land beyond for a pressed village that knows no good place (D2-045)
                double u = (0.35 + 0.5 * p.Bold / 255.0) * DMath.Min(1.0, w.Migration.Pressure(in here)) + 0.1 * rs.NextUnit();
                if (u > best) { best = u; choice = 8; }
            }

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
                case 6:
                    BeginWild(ref p, ref here, wildKind, WildNeed(in here) > 0 ? WhyCode.FoodShort : WhyCode.Provide, ref rs);
                    return;
                case 8:
                    if (BeginScout(ref p, ref here, ref rs)) return;
                    break;
                case 7:
                    {
                        // at their own lot, or at the village's well or granary
                        bool own = w.Households.Exists(p.Household) && w.Households.Get(p.Household).Shelter < Construction.House;
                        double bx = own ? homeX : (Construction.NextWork(in here) == Construction.Well ? here.WellX : here.SquareX + 14);
                        double by = own ? homeY : (Construction.NextWork(in here) == Construction.Well ? here.WellY : here.SquareY - 10);
                        BeginLocal(ref p, ActivityKind.Build, own ? WhyCode.NoRoof : WhyCode.VillageWork, 0, bx + rs.Range(-3, 3), by + rs.Range(-3, 3), SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
                        return;
                    }
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

        // ---------------------------------------------------------------- the wild's food (D2-042)
        /// <summary>How hard a village's stores push its people to the wild: the ration's shortfall, or stores run dry.</summary>
        public static double WildNeed(in Settlement s)
        {
            // a mild cut (prudence keeping a reserve) sends a few more out; a deep one sends everyone who can go
            double shortage = DMath.Max(0.0, 1.0 - s.Ration);
            double need = 1.8 * shortage;
            if (s.Granary < 1.0) need = DMath.Max(need, 1.0);
            return need;
        }

        /// <summary>
        /// A person's own pull to the craft on an ordinary day - the bold hunt, the skilled go back to what they know - worth
        /// less the fuller the village's stores (food beyond years of need only spoils): a quarter of it when the granary holds
        /// a year and a half for everyone.
        /// </summary>
        static double WildHabit(in Person p, in Settlement here, int kind)
        {
            double habit = kind == WildFood.Hunting ? 0.02 + 0.06 * p.Bold / 255.0 + 0.22 * p.SkHunt :
                           kind == WildFood.Fishing ? 0.03 + 0.22 * p.SkFish : 0.0;
            double years = here.Granary / (DMath.Max(1, here.Population) * SimTime.DaysPerYear);
            return habit * (1.0 - 0.75 * DMath.Clamp01(years / 1.5));
        }

        static double WildUrge(in Person p, in Settlement here, int kind, double expect) =>
            (WildNeed(in here) + WildHabit(in p, in here, kind)) * DMath.Clamp(expect / 0.7, 0.0, 1.3);

        /// <summary>
        /// The livelihood of the wild a person would take up now (the best FU an hour they can expect from today's survey of the
        /// grounds), or -1. Hunting asks a grown, healthy body free of a baby at the breast or one due; fishing and gathering less.
        /// </summary>
        public int WildChoice(in Person p, in Settlement here, double age, long now, out double expect)
        {
            expect = 0; int kind = -1;
            bool heavy = p.PregDue != 0 && p.PregDue - now < 10 * SimTime.Day;
            bool nursing = p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2;
            if (age >= 16 && age < 60 && p.Health > 0.5 && !heavy && !nursing && here.Hunt.Expect > 0f && here.Hunt.A >= 0)
            { double e = here.Hunt.Expect * (0.5 + p.SkHunt); if (e > expect) { expect = e; kind = WildFood.Hunting; } }
            if (age >= 14 && age < 70 && !heavy && here.Fish.Expect > 0f && here.Fish.A >= 0)
            { double e = here.Fish.Expect * (0.5 + p.SkFish); if (e > expect) { expect = e; kind = WildFood.Fishing; } }
            if (age >= 12 && here.Gath.Expect > 0f && here.Gath.A >= 0)
            { double e = here.Gath.Expect * (0.6 + 0.4 * p.Health); if (e > expect) { expect = e; kind = WildFood.Gathering; } }
            return kind;
        }

        /// <summary>Out to one of the village's best grounds for the livelihood (a shore for fishing), for a hunt's or a gathering's length.</summary>
        void BeginWild(ref Person p, ref Settlement here, int kind, WhyCode why, ref RandomSequence rs)
        {
            var w = _w;
            var g = kind == WildFood.Hunting ? here.Hunt : kind == WildFood.Fishing ? here.Fish : here.Gath;
            int ground = WildFood.PickGround(in g, ref rs);
            // a hunter goes where a herd is, when the village knows one (D2-047)
            if (kind == WildFood.Hunting) { int herd = w.HerdSys.BestGame(here.Id); if (herd != 0 && rs.Chance(0.7)) ground = w.Herds.Get(herd).Cell; }
            if (ground < 0) return;
            w.Wild.GroundPoint(here.Id, ground, kind == WildFood.Fishing, ref rs, out double x, out double y);
            double hours = kind == WildFood.Gathering ? 2.5 + 1.5 * rs.NextUnit() : 3.5 + 1.5 * rs.NextUnit();
            var act = kind == WildFood.Hunting ? ActivityKind.Hunt : kind == WildFood.Fishing ? ActivityKind.Fish : ActivityKind.Gather;
            BeginLocal(ref p, act, why, ground, x, y, SimTime.Hours(hours));
            p.Ground = ground;
            if (kind == WildFood.Hunting) w.Metrics.HuntTrips++; else if (kind == WildFood.Fishing) w.Metrics.FishTrips++; else w.Metrics.GatherTrips++;
            if (why != WhyCode.Provide) w.SettlementSys.NoteWildFood(here.Id);
        }

        /// <summary>Building pace: a woodworker's hand and a body's strength (labour-hours of progress per hour).</summary>
        static double BuildRate(in Person p) => (0.7 + 0.6 * p.SkWood) * (0.5 + 0.5 * p.Health);

        /// <summary>The most hours of a COARSE day given to the wild (a long trip's worth).</summary>
        public const double WildCoarseMax = 7.0;
        /// <summary>The hours out in the wild a pressing need asks of a COARSE day (a lived day makes two trips).</summary>
        public const double WildNeedDay = 9.0;

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
            bool shortNow = WildNeed(in s) > 0;
            if (w.Migration.WantsScout(in s))
            {
                // a pressed village that knows no good place sends someone to look (D2-045): the same place a lived day's scout
                // would choose, seen in the same day
                var srs = new RandomSequence(KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, 45));
                int target = w.Migration.ScoutTarget(sid, ref srs);
                ref var s1 = ref w.Settlements.Get(sid);
                if (target >= 0) { w.Migration.Look(sid, target, 0); w.Metrics.Scouts++; } else s1.ScoutAt = now;
            }
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                double age = p.AgeYears(now);
                // the day's food from the larder, then the granary (D2-042: one account a day, as the body keeps it)
                EatDay(ref p);
                if (p.Carry > 0f) { w.Wild.Deposit(p.Id, p.Carry); p.Carry = 0f; }
                if (age < 12 || p.Health < 0.3) continue;
                // youths (12-15) work part of a day, as they take part in a lived day's chores
                double hours = 7.5 * (0.6 + 0.4 * p.Diligence / 255.0) * (age > 60 ? 0.5 : (age < 16 ? 0.6 : 1.0)) * (0.5 + 0.5 * p.Health);
                double day0 = hours;
                if (cropNeed > 0)
                {
                    double rate = (0.6 + p.SkFarm) * (0.5 + 0.5 * p.Health);
                    double h = Math.Min(hours, cropNeed / rate);
                    s.CropWork += h * rate; cropNeed -= h * rate; hours -= h;
                    p.SkFarm = (float)DMath.Min(1.0, p.SkFarm + 0.0015 * h);
                }
                // building, as a lived day builds: in every free hour it beats idling for, unless hunger sends them out first (D2-045)
                bool ownRoof = false, noWood = false;
                bool build = hours > 0 && Construction.HasWork(w, in s, p.Household, out ownRoof, out noWood);
                double buildUrge = 0;
                if (build)
                {
                    byte shelter = w.Households.Exists(p.Household) ? w.Households.Get(p.Household).Shelter : Construction.House;
                    buildUrge = ownRoof ? (shelter == Construction.Camp ? 0.85 : 0.4) : 0.45;
                    if (!(shortNow && WildNeed(in s) > buildUrge))
                    {
                        double used = Construction.Work(w, sid, p.Household, DMath.Min(hours, 7.0), BuildRate(in p));
                        hours -= used;
                        build = false;
                    }
                }
                if (hours > 0)
                {
                    // the wild: the same choice and the same catch from the same fields as a day lived step by step (D2-042)
                    int wk = WildChoice(in p, in s, age, now, out double expect);
                    if (wk >= 0)
                    {
                        double u = WildUrge(in p, in s, wk, expect) * (age > 60 ? 0.5 : 1.0) * (0.6 + 0.4 * p.Diligence / 255.0);
                        // a lived day offers the wild every free daytime slot it beats idling at (idle 0.08-0.13 against the
                        // wild's urge + 0.06 of chance): the same odds, as a share of the free hours
                        // a pressing need keeps people out from dawn to mid-afternoon, beyond an ordinary day's work (two trips)
                        double free = shortNow ? DMath.Max(hours, WildNeedDay * (0.5 + 0.5 * p.Health) * (age > 60 ? 0.6 : (age < 16 ? 0.7 : 1.0)) - (day0 - hours)) : hours;
                        // (in ordinary times a lived day spends ~0.6 of the free hours this mapping gives: building, water and
                        // company take free slots too - measured, CoarseAndActivityLiveOffTheWildAlike)
                        double hw = u > 0.005 ? DMath.Min(free, WildNeedDay) * DMath.Clamp01((u + 0.01) / 0.1) * (shortNow ? 1.0 : 0.6) : 0.0;
                        if (hw >= 0.25)
                        {
                            var rs = NextRandom(ref p);
                            var g = wk == WildFood.Hunting ? s.Hunt : wk == WildFood.Fishing ? s.Fish : s.Gath;
                            int ground = WildFood.PickGround(in g, ref rs);
                            if (ground >= 0)
                            {
                                ulong key = KeyedRng.Hash(w.Seed, RngStream.Work, (ulong)p.Id, (ulong)now);
                                double fu;
                                if (wk == WildFood.Hunting)
                                {
                                    int herd = w.HerdSys.BestGame(sid);   // (where a herd is, as a lived day's hunter goes)
                                    if (herd != 0 && rs.Chance(0.7)) ground = w.Herds.Get(herd).Cell;
                                    fu = w.Wild.Hunt(ground, hw, p.SkHunt, key, out _); p.SkHunt = (float)DMath.Min(1.0, p.SkHunt + 0.002 * hw); w.Metrics.HuntTrips++;
                                    if (fu > 0) w.HerdSys.MarkHunted(ground);
                                }
                                else if (wk == WildFood.Fishing) { fu = w.Wild.Fish(ground, hw, p.SkFish, key); p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.002 * hw); w.Metrics.FishTrips++; }
                                else { fu = w.Wild.Gather(ground, hw, p.Health, key); w.Metrics.GatherTrips++; }
                                w.Wild.Deposit(p.Id, fu);
                                w.Metrics.WildHours += hw; w.Metrics.WildFu += fu;
                                hours = DMath.Max(0.0, hours - hw);
                                if (shortNow) w.SettlementSys.NoteWildFood(sid);
                            }
                        }
                    }
                }
                if (build && hours > 0) hours -= Construction.Work(w, sid, p.Household, DMath.Min(hours, 7.0), BuildRate(in p));
                if (hours > 0 && s.Woodstore < woodTarget)
                {
                    double h = hours * 0.4;
                    s.Woodstore += h * (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health);
                    p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * h);
                }
            }
        }

        // ---------------------------------------------------------------- moving and scouting (D2-045)
        /// <summary>
        /// A member of a party sets out with it (Migration.Depart): whatever they were doing ends; they walk the route at the
        /// party's pace, arriving with it. Those away from the village when it leaves follow when they next go home.
        /// </summary>
        public void Walk(int pid, Route r, bool forward, long arrive, int destEnd)
        {
            var w = _w;
            ref var p = ref w.People.Get(pid);
            if (!p.Alive || p.At != p.Home || p.At == 0) return;
            if (p.Carry > 0f) { w.Wild.Deposit(p.Id, p.Carry); p.Carry = 0f; }   // (a catch in hand goes into the larder that travels)
            CutStep(ref p);
            p.Ground = -1;
            // the party's pace, not their own
            BeginTravel(ref p, ActivityKind.Move, WhyCode.NewHome, r, forward ? 0.0 : r.Length, forward ? r.Length : 0.0, destEnd, arrive);
        }

        /// <summary>A scout sets out to look at the land beyond (their village is pressed and knows no good place).</summary>
        bool BeginScout(ref Person p, ref Settlement here, ref RandomSequence rs)
        {
            var w = _w;
            int target = w.Migration.ScoutTarget(here.Id, ref rs);
            if (target < 0) { here.ScoutAt = w.Now; return false; }
            var r = w.Routes.GetToCell(w, here.Id, target);
            if (r == null) { here.ScoutAt = w.Now; return false; }
            here.ScoutAt = w.Now;   // one goes; the others wait for what they see
            BeginTravel(ref p, ActivityKind.Scout, WhyCode.LookForLand, r, 0.0, r.Length, RouteCache.PlaceEnd(target));
            w.Metrics.Scouts++;
            return true;
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
