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
        System.Func<int, int> _versionOf;   // (a person's activity version for the travel index's sweep, -1 gone: D2-137)

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
                if (p.TBend > p.T0 && p.TBend < p.TA)
                {
                    // round the water: to the bend, then on from it (D2-156)
                    if (t < p.TBend)
                    {
                        double u0 = DMath.Clamp01((t - p.T0) / (double)(p.TBend - p.T0));
                        x = p.FromX + ((double)p.BendX - p.FromX) * u0;
                        y = p.FromY + ((double)p.BendY - p.FromY) * u0;
                    }
                    else
                    {
                        double u1 = DMath.Clamp01((t - p.TBend) / (double)(p.TA - p.TBend));
                        x = p.BendX + ((double)p.ToX - p.BendX) * u1;
                        y = p.BendY + ((double)p.ToY - p.BendY) * u1;
                    }
                    return;
                }
                double span = p.TA - p.T0;
                double u = span > 0 ? DMath.Clamp01((t - p.T0) / span) : 1.0;
                x = p.FromX + ((double)p.ToX - p.FromX) * u;
                y = p.FromY + ((double)p.ToY - p.FromY) * u;
            }
            else { x = p.ToX; y = p.ToY; }
        }

        public static double RouteSAt(in Person p, double t)
        {
            double span = p.T1 - p.T0;
            double u = span > 0 ? DMath.Clamp01((t - p.T0) / span) : 1.0;
            return p.S0 + ((double)p.S1 - p.S0) * u;
        }

        /// <summary>World direction (unit vector) of a person at time t (seconds, fractional allowed).</summary>
        public Vec3d PositionAt(in Person p, double t)
        {
            if (p.Step == StepKind.Travel)
            {
                var r = RouteOf(in p);
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
            p.Derive(seed);
            p.Reserve = (float)ReserveCap(in p, seed, w.Now);
            ref var hh = ref w.Households.Get(household);
            p.FromX = hh.HomeX; p.FromY = hh.HomeY; p.ToX = hh.HomeX; p.ToY = hh.HomeY;
            p.Step = StepKind.Local; p.Act = ActivityKind.Idle; p.T0 = w.Now; p.TA = w.Now; p.T1 = Math.Max(firstDecision, w.Now + 1);
            w.People.Add(id, p);
            w.Residents(home).InsertSorted(id);
            w.Members(household).InsertSorted(id);
            w.History.Register(EntityRef.Person(id), Worldgen.Names.Person(seed, id, sex == Sex.Female, w.Settlements.Get(home).Tongue) + " " + w.FamilyName(household), born);   // (named in the tongue of the village born in: C11.1)
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
        void BeginLocal(ref Person p, ActivityKind act, WhyCode why, int whyArg, double tx, double ty, long stay, long endAt = 0, double pace = 1.0)
        {
            long now = _w.Now;
            LocalAt(p, now, out double x, out double y);
            p.Act = act; p.Why = why; p.WhyArg = whyArg; p.ActVer++;
            // (out of the water, D2-156: every lived spot on dry ground - a well's spot under a lake's edge, a herd's ground by a bay, a threshing floor
            // a lake has come up to: the dry ground nearest it - and round a lake or a bay that lies across the straight way, by the shortest bend
            // found on dry ground beside it; a boat's crew rows out over the water)
            double bx = 0, by = 0, d1 = 0;
            bool bent = false;
            int sid = p.At != 0 ? p.At : p.Home;   // (the village whose frame they walk in: PositionAt's)
            bool afoot = !Afoot.Off && act != ActivityKind.Sail && _w.Settlements.Exists(sid);
            if (afoot) Boats.OutOfTheWater(_w, in _w.Settlements.Get(sid), ref tx, ref ty);
            double dx = tx - x, dy = ty - y;
            double d = DMath.Sqrt(dx * dx + dy * dy);
            if (afoot && d > Afoot.Short && !Afoot.Dry(_w, in _w.Settlements.Get(sid), x, y, tx, ty))
            {
                bent = Afoot.Bend(_w, in _w.Settlements.Get(sid), x, y, tx, ty, out bx, out by);
                if (bent)
                {
                    double ex = bx - x, ey = by - y, fx = tx - bx, fy = ty - by;
                    d1 = DMath.Sqrt(ex * ex + ey * ey);
                    d = d1 + DMath.Sqrt(fx * fx + fy * fy);
                    _w.Metrics.BentWalks++;
                }
                else _w.Metrics.WetWalks++;
                if (Afoot.Tally != null) Afoot.Tally[(bent ? 0 : 64) + Math.Min(63, (int)act)]++;
            }
            long walk = d > 2.0 ? Math.Max(1L, DMath.ToLong(DMath.RoundHalfUp(d / (Speed(p, now) * pace)))) : 0;   // (pace: rowed out in a plank boat, C9.2b)
            p.Step = StepKind.Local;
            p.FromX = (float)x; p.FromY = (float)y; p.ToX = (float)tx; p.ToY = (float)ty;
            p.T0 = now; p.TA = now + walk; p.T1 = endAt > 0 ? Math.Max(p.TA + 60L, endAt) : p.TA + Math.Max(60L, stay);   // (endAt: done with others - a boat's crew back together)
            p.StayDur = 0;
            if (bent && walk >= 2) { p.BendX = (float)bx; p.BendY = (float)by; p.TBend = now + Math.Max(1L, Math.Min(walk - 1, DMath.ToLong(DMath.RoundHalfUp(walk * d1 / d)))); }
            else if (p.TBend != 0) { p.BendX = 0f; p.BendY = 0f; p.TBend = 0; }
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
            // (a worn way is quicker underfoot: C4.1; a loaded cart slower than a walker: C4.2)
            p.T1 = arriveAt > 0 ? Math.Max(now + 1, arriveAt) : now + Math.Max(1L, DMath.ToLong(DMath.RoundHalfUp(len / (Speed(p, now) * _w.Ways.Speed(now, r.A, r.B) * (act == ActivityKind.Voyage ? 1.0 : Carts.Pace(_w, in p))))));
            Schedule(in p);
            RegisterLeg(ref p, r);
            _w.Metrics.Journeys++;
            if (act != ActivityKind.Voyage) _w.Ways.Walk(_w, r.A, r.B, 1.0);   // (and its walking wears it: C4.1; the sea keeps no tracks)
        }

        /// <summary>The way a traveller is on: a road between villages, or the way over the sea between their landings on a voyage (C9.3).</summary>
        public Route RouteOf(in Person p) => p.Act == ActivityKind.Voyage ? _w.SeaRoutes.Get(_w, p.RouteA, p.RouteB) : _w.Routes.Get(_w, p.RouteA, p.RouteB);

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
            if (_w.Travel.Full)
            {
                // stale legs of cells nobody looked at, dropped before the index grows (derived state: D2-137)
                if (_versionOf == null) _versionOf = pid => _w.People.Exists(pid) && _w.People.Get(pid).Alive ? _w.People.Get(pid).ActVer : -1;   // (as the hazards' queries read it)
                _w.Travel.Sweep(_w.Now, _versionOf);
            }
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
                var r = RouteOf(in p);
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
                    if (p.TBend != 0) { p.BendX = 0f; p.BendY = 0f; p.TBend = 0; }
                    Complete(ref p, 1.0);
                    if (!p.Alive) return;   // (drowned when their boat overturned: C9.1)
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
            var r = RouteOf(in p);
            if (p.Act == ActivityKind.Voyage) { VoyageLanded(ref p, r); return; }   // (at a landing: C9.3)
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
            int arrived = r == null ? p.Home : (r.Length <= 1.0 && p.Dest != 0 && (p.Dest == r.A || p.Dest == r.B) ? p.Dest : (p.S1 <= 0.5f ? r.A : r.B));   // (a way of no length: where they were going - D2-137)
            if (!w.Settlements.Exists(arrived)) arrived = p.Home;
            p.At = arrived;
            Green(arrived, out double ax, out double ay);   // (at its green: C9.2)
            if (arrived == p.Home && w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); ax = hh.HomeX; ay = hh.HomeY; }
            p.Step = StepKind.Local; p.FromX = (float)ax; p.FromY = (float)ay; p.ToX = p.FromX; p.ToY = p.FromY; p.TA = w.Now; p.T0 = w.Now;
            if (arrived != p.Home)
            {
                w.Visitors(arrived).InsertSorted(p.Id);
                var rs = NextRandom(ref p);
                p.Until = w.Now + SimTime.Hours(1.5 + 3.0 * rs.NextUnit());
                if (p.Why == WhyCode.Take) { p.Until = w.Now; w.Conflict.Take(ref p, arrived); }   // at the store in the dark: no visit (D2-069)
                else if (p.Why == WhyCode.Raid) { p.Until = w.Now; w.Conflict.RaidArrive(ref p, arrived); }   // the band falls on the store (D2-073)
                else
                {
                    p.Visits++;
                    w.Disease.Arrive(ref p, arrived, p.Until - w.Now);   // what they may catch there and carry home (D2-066)
                    if (p.Why == WhyCode.Trade) w.Trade.Exchange(ref p, arrived);   // the load for what they want back (D2-067)
                    if (p.Why == WhyCode.Tribute) w.Conflict.CarrierArrived(ref p, arrived);   // the lord's tenth into its store (C10.6)
                    w.DeedBook.Meet(p.Home, arrived);                        // the news each village tells (D2-072)
                    if (p.Why == WhyCode.Claim) { int deed = p.Errand; p.Errand = 0; w.Conflict.Answer(ref p, arrived, deed); }   // amends asked (D2-072)
                }
            }
            else if (p.CargoQty > 0f) w.Trade.Unload(ref p, arrived);   // home with it
            if (arrived == p.Home && p.Band != 0 && TC2.Core.Society.Conflict.RaidDone(in p, w)) p.Band = 0;
            p.Dest = 0;
            Decide(ref p);
        }

        /// <summary>
        /// (session 32) The shock step: while a shock felt here holds (<see cref="Hazards.ShockHold"/> from when it was felt), the unhurt run to open ground
        /// clear of where it struck - the village's green, or past it when it struck near the green - and keep together there until it has passed, then
        /// go back to their day. The badly hurt are left to the step that takes them abed. False when no shock holds them.
        /// </summary>
        bool ShockStep(ref Person p, RandomSequence rs, bool night)
        {
            var w = _w;
            long now = w.Now;
            if (p.At == 0 || !w.Settlements.Exists(p.At) || p.Voyage != 0 || p.Act == ActivityKind.Infant) return false;
            if (!w.History.TryGet(p.Alarm, out var e)) return false;
            long hold = Hazards.ShockHold(e.Kind);
            if (e.Kind == HistoryKind.Storm && p.Hurt >= TC2.Core.Society.Conflict.AbedHurt) return false;
            if (hold == 0 || now - p.AlarmT >= hold || e.Cell < 0) return false;
            if (p.Hurt >= TC2.Core.Society.Conflict.AbedHurt) return false;
            if (p.SickPhase == TC2.Core.Health.Disease.Infectious && p.At == p.Home) return false;   // (review, session 32: the sick abed stay abed - they had run to the crowd)
            ref var s = ref w.Settlements.Get(p.At);
            long left0 = p.AlarmT + hold - now;
            if (e.Kind == HistoryKind.Storm)
            {
                // a gale: in, and keep in while it blows - home if this is home, else the green's shelter of the village they are in; at night those at home are
                // in already: they sleep (review, session 32: they had stood up all night to keep in)
                if (night && p.At == p.Home) return false;
                double hx, hy;
                if (p.At == p.Home && w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); hx = hh.HomeX; hy = hh.HomeY; }
                else Green(p.At, out hx, out hy);
                LocalAt(p, now, out double sx, out double sy);
                if ((sx - hx) * (sx - hx) + (sy - hy) * (sy - hy) > 25.0 * 25.0) BeginLocal(ref p, ActivityKind.Flee, WhyCode.Shock, (int)e.Kind, hx, hy, Math.Min(left0, 10 * SimTime.Minute), 0, 1.6);
                else BeginLocal(ref p, ActivityKind.Idle, WhyCode.Shock, (int)e.Kind, hx, hy, Math.Max(60L, left0));
                return true;
            }
            s.SphereToLocal(w.Planet.Grid.Center[e.Cell], w.Planet.Radius, out double ix, out double iy);
            Green(p.At, out double gx, out double gy);
            double dx = gx - ix, dy = gy - iy, d = DMath.Sqrt(dx * dx + dy * dy);
            double rx = gx, ry = gy;
            if (d < 180.0)
            {
                double ux = d > 1.0 ? dx / d : 1.0, uy = d > 1.0 ? dy / d : 0.0;
                rx = ix + ux * 220.0; ry = iy + uy * 220.0;
            }
            LocalAt(p, now, out double px, out double py);
            double near = DMath.Sqrt((px - rx) * (px - rx) + (py - ry) * (py - ry));
            // each their own spot round the refuge: a crowd, not a point
            double ax = rx + (rs.NextUnit() - 0.5) * 30.0, ay = ry + (rs.NextUnit() - 0.5) * 30.0;
            long left = p.AlarmT + hold - now;
            if (near > 40.0) BeginLocal(ref p, ActivityKind.Flee, WhyCode.Shock, (int)e.Kind, ax, ay, Math.Min(left, 10 * SimTime.Minute), 0, 2.2);   // (running)
            else BeginLocal(ref p, ActivityKind.Social, WhyCode.Shock, (int)e.Kind, ax, ay, Math.Max(60L, left));
            return true;
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
            if (reason == InterruptReason.ShockFelt)
            {
                // (session 32) a shock felt where they are: one already shaken by a shock that holds as long goes on as they were (a stone's fall and its
                // own tremor are one fright)
                if (p.At != place) { w.Metrics.Stale++; return; }
                if (p.Alarm != 0 && w.Now - p.AlarmT < 10 * SimTime.Minute && w.History.TryGet(p.Alarm, out var was) && w.History.TryGet(cause, out var now2) &&
                    Hazards.ShockHold(was.Kind) >= Hazards.ShockHold(now2.Kind)) return;
            }
            // (session 32) a fire seen by one already running from a shock (the stone's own fire): they are clear of it and keep together; once the shock has
            // passed, the fire's rule keeps them to shelter while it burns near
            else if (p.Alarm != 0 && w.History.TryGet(p.Alarm, out var held) && w.Now - p.AlarmT < Hazards.ShockHold(held.Kind)) return;
            p.Alarm = cause; p.AlarmT = w.Now;
            w.Metrics.InterruptsHandled++;
            if (p.Step == StepKind.Travel)
            {
                var r = RouteOf(in p);
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
                if (p.TBend != 0) { p.BendX = 0f; p.BendY = 0f; p.TBend = 0; }
            }
            p.T1 = now;
        }

        // ---------------------------------------------------------------- effects
        void Complete(ref Person p, double frac)
        {
            var w = _w;
            long now = w.Now;
            double hours = (p.T1 - Math.Max(p.T0, p.TA)) * frac / (double)SimTime.Hour;
            if (IsWork(p.Act)) p.Worked = (float)(p.Worked + hours);   // a day's labour (D2-058)
            switch (p.Act)
            {
                case ActivityKind.Eat:
                    if (frac >= 0.5) EatNow(ref p);
                    break;
                case ActivityKind.Fields:
                    if (p.At != 0)
                    {
                        ref var s = ref w.Settlements.Get(p.At);
                        double cw = hours * (0.6 + p.SkFarm) * (0.5 + 0.5 * p.Health) * Tools(p.Household) * Knowledge.Tech.ArdFactor(in p, w.Stock.HasOx(p.Household));
                        s.CropWork += cw;
                        TC2.Core.Diagnostics.HouseLedger.Work(p.Household, cw);   // (tools: whose work the crop was; null in the game)
                        w.Learning.Practice(ref p, Knowledge.Tech.Ard, hours);
                        p.SkFarm = (float)DMath.Min(1.0, p.SkFarm + 0.0015 * hours);
                    }
                    break;
                case ActivityKind.Wood:
                    if (p.At != 0)
                    {
                        // felled at the village's woodlot, carried home from there (C1.2: the woods it came from are thinner by it)
                        Woods.Cut(w, p.At, hours, (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health) * Tools(p.Household), false, p.Household);
                        p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * hours);
                    }
                    break;
                case ActivityKind.Sleep:
                    p.Woke = now;
                    p.Worked = 0f;   // a new day's labour
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
                        double worked = hours * Tools(p.Household);   // (worn spears and torn nets catch less in the hours: D2-058)
                        if (p.Act == ActivityKind.Hunt)
                        {
                            fu = w.Wild.Hunt(p.Ground, worked, p.SkHunt, key, out kind, out double hides); p.SkHunt = (float)DMath.Min(1.0, p.SkHunt + 0.002 * hours); if (fu > 0) w.HerdSys.MarkHunted(p.Home, p.Ground);
                            if (hides > 0 && w.Settlements.Exists(p.Home)) w.Settlements.Get(p.Home).Hides += hides;   // (the hides home with the meat: D2-063)
                        }
                        else if (p.Act == ActivityKind.Fish)
                        {
                            fu = w.Wild.Fish(p.Ground, worked * Knowledge.Tech.NetsFactor(in p), p.SkFish, key); p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.002 * hours); if (fu > 0) kind = 64;
                            w.Learning.Practice(ref p, Knowledge.Tech.Nets, hours);
                        }
                        else fu = w.Wild.Gather(p.Ground, worked, p.Health, key);
                        p.Carry = (float)(p.Carry + fu);
                        p.WhyArg = kind + 1;   // what they bring home (the view and the inspector read it from the haul)
                        w.Metrics.WildHours += hours; w.Metrics.WildFu += fu;
                    }
                    p.Ground = -1;
                    break;
                case ActivityKind.Sail:
                    if (p.Ground >= 0 && hours > 0)
                    {
                        // out at the sea's grounds in the household's boat (C9.1): the open water's shoals; the trip ends under the wind it is
                        // out in - the boat may overturn (with someone else aboard it already has: they are in the water too)
                        int bid = p.WhyArg;
                        double wind = Boats.WindAt(w, w.Planet.Grid.Center[p.Ground]);
                        bool gone = !w.Buildings.Exists(bid) || w.Buildings.Get(bid).Cond <= 0f;
                        if (gone) Boats.Overturned(w, p.Home, p.Household, bid, p.Id, wind, out _);
                        else if (frac >= 1.0 && Boats.Trip(w, p.Home, p.Household, bid, p.Id, wind, out _) != 0) gone = true;
                        if (!p.Alive) { p.Ground = -1; break; }
                        double fu = 0;
                        if (!gone)
                        {
                            ulong key = KeyedRng.Hash(w.Seed, RngStream.Work, (ulong)p.Id, (ulong)now);
                            fu = w.Wild.Fish(p.Ground, hours * Tools(p.Household) * Knowledge.Tech.NetsFactor(in p), p.SkFish, key, 1, true, Boats.IsPlank(w.Buildings.Get(bid).Kind));
                            w.Learning.Practice(ref p, Knowledge.Tech.Nets, hours);
                        }
                        p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.002 * hours);
                        p.Carry = (float)(p.Carry + fu);
                        p.WhyArg = 65;   // (fish: what they bring home)
                        w.Metrics.WildHours += hours; w.Metrics.WildFu += fu;
                    }
                    p.Ground = -1;
                    break;
                case ActivityKind.Haul:
                    if (p.Carry > 0f) { w.Wild.Deposit(p.Id, p.Carry); p.Carry = 0f; }
                    break;
                case ActivityKind.Build:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        if (p.Why == WhyCode.Boat) Boats.Work(w, p.At, p.Household, hours, BuildRate(in p) * Tools(p.Household), p.Id);   // (the household's boat: C9.1)
                        else if (p.Why == WhyCode.Cart) Carts.Work(w, p.Household, hours, BuildRate(in p) * Tools(p.Household), p.Id);   // (the household's cart: C4.2)
                        else Construction.Work(w, p.At, p.Household, hours, BuildRate(in p) * Tools(p.Household), p.Id);
                    }
                    break;
                case ActivityKind.Train:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        int gid = Training.Ground(w, p.At);   // (practice at the training ground: C1.5)
                        if (gid != 0) Training.Practise(w, ref p, p.At, gid, hours);
                        if (gid != 0 && TC2.Core.Society.Retinue.Tally != null && TC2.Core.Society.Retinue.SwornHere(w, in p)) TC2.Core.Society.Retinue.Tally[7] += hours;   // (tools: C10.7b)
                    }
                    break;
                case ActivityKind.Smith:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        int fid = p.WhyArg != 0 && w.Buildings.Exists(p.WhyArg) && w.Buildings.Get(p.WhyArg).Kind == Structures.Smithy && Structures.Working(in w.Buildings.Get(p.WhyArg), w.Now)
                            ? p.WhyArg : Forge.Smithy(w, p.At);   // (smelting, working metal, forging arms at the smithy they went to: C1.4, C1.4b)
                        if (fid != 0) Forge.Work(w, ref p, p.At, fid, hours);
                    }
                    break;
                case ActivityKind.Weave:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        int wid = p.WhyArg != 0 && w.Buildings.Exists(p.WhyArg) && w.Buildings.Get(p.WhyArg).Kind == Structures.Workshop && Structures.Working(in w.Buildings.Get(p.WhyArg), w.Now)
                            ? p.WhyArg : Weaving.Shed(w, p.At);   // (weaving and finishing the village's wares at the shed they went to: C3.1, C3.1b)
                        if (wid != 0) Weaving.Work(w, ref p, p.At, wid, hours);
                    }
                    break;
                case ActivityKind.Pot:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        int kid = Pottery.Kiln(w, p.At);   // (clay dug, pots and jars thrown and fired at the village's kiln: C3.2)
                        if (kid != 0) Pottery.Work(w, ref p, p.At, kid, hours);
                    }
                    break;
                case ActivityKind.Burn:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        // wood felled at the woodlot and burnt into charcoal at the village's hearth (C5.2a)
                        int chid = Charcoal.Site(w, p.At);
                        ref var chs = ref w.Settlements.Get(p.At);
                        if (chid != 0) Charcoal.Burn(w, p.At, chid, hours, (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health) * Tools(p.Household), false, DMath.Max(0.0, (double)chs.CharcoalWant - chs.Charcoal));
                        p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * hours);
                    }
                    break;
                case ActivityKind.Mine:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        // ore dug at the village's mine - or bog ore at its pits - and carried home, a load at a time (C5.1, C5.2b)
                        byte mk = p.Why == WhyCode.BogOre ? Mining.Iron : Mining.Copper;
                        int mid = Mining.Mine(w, p.At, mk);
                        ref var ms = ref w.Settlements.Get(p.At);
                        if (mid != 0 && Mining.MayDig(w, p.At, mk)) Mining.Dig(w, p.At, mid, hours, Mining.DigRate(in p, Tools(p.Household)), false, DMath.Max(0.0, Mining.WantOf(in ms, mk) - Mining.OreIn(in ms, mk)), mk, p.Id);
                    }
                    break;
                case ActivityKind.Quarry:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        // stone cut at the quarry and carried home, a load at a time (C1.3)
                        int qid = Stone.Quarry(w, p.At);
                        if (qid != 0) Stone.Cut(w, p.At, qid, hours, Stone.CutRate(in p, Tools(p.Household)), false, p.Household);
                    }
                    break;
                case ActivityKind.Garden:
                    if (p.At != 0 && p.At == p.Home && hours > 0 && w.Households.Exists(p.Household))
                    {
                        // the household's garden (C3.7a): dug and weeded while the crops grow, what it bears gathered into the larder from the ripening
                        ref var hh = ref w.Households.Get(p.Household);
                        ref var s = ref w.Settlements.Get(p.At);
                        double gH = hours * (0.5 + 0.5 * p.Health);
                        int gDoy = SettlementSystem.DayOfYear(in s, now);   // (the village's own calendar: D2-144)
                        double gFu = Gardens.Work(ref hh, gDoy, s.CropGrowth, gH);
                        w.Metrics.GardenHours += gH; w.Metrics.GardenFu += gFu;
                        if (Gardens.Tally != null) Gardens.Count(in s, gDoy, gH, gFu);
                    }
                    break;
                case ActivityKind.Muck:
                    if (p.At != 0 && p.At == p.Home && hours > 0)
                    {
                        // the yards' muck carted out to the fields and spread (C3.7c): the stint's loads, at the household's cart's pace or a back's
                        ref var s = ref w.Settlements.Get(p.At);
                        Muck.Cart(w, ref s, hours, Muck.HoursPerTonne(w, p.Household), 0.5 + 0.5 * p.Health);
                    }
                    break;
                case ActivityKind.Thresh:
                    if (p.At != 0 && w.Settlements.Exists(p.At))
                    {
                        // the village's sheaves threshed with the flail at its floor (C3.7b): the stint's grain out of the sheaf, as far as it lasted
                        ref var s = ref w.Settlements.Get(p.At);
                        double rate = Threshing.PerHour * (0.5 + 0.5 * p.Health);
                        if (hours > 0) Threshing.Thresh(w, ref s, hours * rate, hours);
                        double planned = (p.T1 - Math.Max(p.T0, p.TA)) / (double)SimTime.Hour * rate;
                        s.ThreshOut = (float)DMath.Max(0.0, s.ThreshOut - planned);
                    }
                    break;
                case ActivityKind.Craft:
                    if (p.At != 0 && p.At == p.Home && hours > 0 && w.Households.Exists(p.Household))
                    {
                        hours = Smith(ref p, hours);   // (a smith's half at the furnace: D2-064)
                        w.Learning.Practice(ref p, Knowledge.Tech.Metal, hours);
                        ref var hh = ref w.Households.Get(p.Household);
                        double made = Goods.Materials(ref w.Settlements.Get(p.At), Goods.MakePerHour(p.AgeYears(now), p.Health) * Knowledge.Tech.LoomFactor(in p) * hours);
                        w.Learning.Practice(ref p, Knowledge.Tech.Loom, hours);
                        hh.Gear = (float)(hh.Gear + made);
                        w.Metrics.CraftHours += hours; w.Metrics.GearMade += made;
                    }
                    break;
                case ActivityKind.Cook:
                    if (p.At != 0 && p.At == p.Home && hours > 0 && w.Households.Exists(p.Household))
                    {
                        // the larder's grain ground and cooked (fetched from the village's store when the larder is empty), on the
                        // village's wood - half as fast with none to burn (D2-056)
                        ref var hh = ref w.Households.Get(p.Household);
                        ref var s = ref w.Settlements.Get(p.At);
                        double made = Hearth.PrepPerHour(p.AgeYears(now), p.Health, s.Woodstore > 0.05) * Knowledge.Tech.QuernFactor(QuernBest(p.Household)) * hours;
                        w.Learning.Practice(ref p, Knowledge.Tech.Quern, hours);
                        double fromLarder = Math.Min(made, hh.Larder);
                        hh.Larder -= fromLarder;
                        double fromStore = Math.Min(made - fromLarder, DMath.Min(Threshing.Threshed(in s), w.Holdings.Available(p.At, p.Household)));   // (in hard times its own grain and the common's only: C3.3c; out of the sheaf: C3.7b)
                        s.Granary -= fromStore;
                        w.Holdings.Take(p.At, p.Household, fromStore);   // (its own grain first: C3.3)
                        double done = fromLarder + fromStore;
                        hh.Ready += done;
                        s.Woodstore = DMath.Max(0.0, s.Woodstore - done * Hearth.CookFuel * Pottery.CookFactor(w, p.Household));   // (in fired pots, its own or a kiln's: D2-063, C3.2)
                        w.Metrics.PrepHours += hours; w.Metrics.PrepFu += done;
                        w.Learning.Practice(ref p, Knowledge.Tech.Pottery, hours);
                    }
                    break;
                case ActivityKind.Water:
                    if (frac >= 0.5 && p.At != 0 && p.At == p.Home && w.Households.Exists(p.Household))
                    {
                        ref var hh = ref w.Households.Get(p.Household);
                        hh.Water = (float)(hh.Water + Hearth.Carry(p.AgeYears(now)));
                        w.Metrics.WaterTrips++;
                        w.Metrics.WaterHours += (p.T1 - p.T0 + (p.TA - p.T0)) / (double)SimTime.Hour;   // there, the filling, and back
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- goods (D2-058)
        /// <summary>Activities that are work: they count toward a day's labour.</summary>
        static bool IsWork(ActivityKind a) => a == ActivityKind.Fields || a == ActivityKind.Wood || a == ActivityKind.Build || a == ActivityKind.Hunt ||
                                              a == ActivityKind.Fish || a == ActivityKind.Gather || a == ActivityKind.Haul || a == ActivityKind.Tend ||
                                              a == ActivityKind.Cook || a == ActivityKind.Water || a == ActivityKind.Craft || a == ActivityKind.Quarry || a == ActivityKind.Smith || a == ActivityKind.Train || a == ActivityKind.Mine || a == ActivityKind.Burn || a == ActivityKind.Sail || a == ActivityKind.Weave || a == ActivityKind.Pot || a == ActivityKind.Garden || a == ActivityKind.Thresh || a == ActivityKind.Muck;

        /// <summary>How much a day's labour still leaves for the slack's work: whole until five hours, a fifth past eight.</summary>
        static double DayLeft(in Person p) => DMath.Clamp(1.0 - (p.Worked - 5.0) / 3.0, 0.2, 1.0);

        /// <summary>The pace of a person's hands with their household's clothes and tools (worn ones down to 0.7), quicker where its village's tools are metal (D2-064).</summary>
        double Tools(int hid)
        {
            var w = _w;
            if (!w.Households.Exists(hid)) return 1.0;
            ref var hh = ref w.Households.Get(hid);
            double metal = 1.0;
            if (w.Settlements.Exists(hh.Settlement)) { ref var s = ref w.Settlements.Get(hh.Settlement); metal = Knowledge.Tech.ToolPace(in s); }
            return Goods.WorkFactor(Goods.Condition(hh.Gear, w.Members(hid).Count)) * metal;
        }

        /// <summary>
        /// A maker who can smelt and work copper, where ore is near and the village is short of metal tools, gives half their
        /// making to the furnace and the anvil (D2-064): metal into the village's store, charcoal out of its woodstore. Returns the
        /// hours left for the household's goods.
        /// </summary>
        double Smith(ref Person p, double hours, int days = 1)
        {
            var w = _w;
            if (hours <= 0 || p.KMetal < 0.3f || p.Home == 0 || !w.Settlements.Exists(p.Home)) return hours;
            if (Forge.Smithy(w, p.Home) != 0) return hours;   // (with a smithy the smith works there, not at the open hearth: C1.4)
            ref var s = ref w.Settlements.Get(p.Home);
            if (Knowledge.Tech.CopperWanted(in s) <= 0 || Mining.MetalIn(in s) <= 1e-6 || s.Charcoal <= 1e-6) return hours;   // (ore dug and carried home, charcoal burnt: C5.1, C5.2a)
            double h = 0.5 * hours;
            double kg = Knowledge.Tech.MetalPerHour(in p) * h * Forge.Hand(in p);   // (practice quickens a smith's hand: C1.4b)
            if (days > 1)
            {
                // (DEEP: days lived stop smelting the day the village has its metal - a step smelted all its days past it: F8)
                double most = Knowledge.Tech.CopperWanted(in s) + kg / days;
                if (kg > most) { h *= most / kg; kg = most; }
            }
            double can = DMath.Min(Mining.MetalIn(in s), Charcoal.MetalFrom(in s, 1.0));
            if (kg > can) { h *= can / kg; kg = can; }   // (as far as the ore and the charcoal in store go)
            s.Metal += kg;
            Charcoal.Burnt(ref s, kg, 1.0);
            Mining.Smelt(ref s, kg);
            w.Metrics.MetalKg += kg;
            p.SkSmith = (float)DMath.Min(1.0, p.SkSmith + Forge.SkillRate * h);   // (the open hearth's hours make a smith too: C1.4b)
            w.Holdings.Commission(p.Home, p.Household, h);   // (paid by the village as at the smithy: C3.3b)
            w.Learning.Practice(ref p, Knowledge.Tech.Metal, h);
            return hours - h;
        }

        /// <summary>A household short of goods takes wares from its village's store before its own hands make more (C3.1, `Weaving.Draw`).</summary>
        void TakeWares(int hid)
        {
            var w = _w;
            if (!w.Households.Exists(hid)) return;
            ref var hh = ref w.Households.Get(hid);
            if (!w.Settlements.Exists(hh.Settlement)) return;
            ref var s = ref w.Settlements.Get(hh.Settlement);
            if (s.Wares > 1e-9) Weaving.Draw(w, ref s, ref hh, w.Members(hid).Count);
        }

        /// <summary>The household's goods against what it keeps its members in (1 = the target), counting what is being made.</summary>
        double GoodsHeld(int hid, double making)
        {
            var w = _w;
            int members = w.Members(hid).Count;
            if (members == 0) return 1.0;
            return (w.Households.Get(hid).Gear + making) / (members * Goods.Target);
        }

        /// <summary>Making and mending at home: spinning, weaving, sewing, hafting, plaiting (D2-058).</summary>
        void BeginCraft(ref Person p, double homeX, double homeY, double hours, ref RandomSequence rs)
        {
            BeginLocal(ref p, ActivityKind.Craft, WhyCode.GoodsWorn, 0, homeX + rs.Range(-3, 3), homeY + rs.Range(-3, 3), SimTime.Hours(hours));
        }

        // ---------------------------------------------------------------- the household's own work (D2-056)
        /// <summary>FU of cooked food a meal of the whole household takes (a third of its day's need).</summary>
        double MealOf(int hid) => _w.SettlementSys.HouseholdNeed(hid) / 3.0;

        /// <summary>Litres the household's pots should hold: a day's water for its members.</summary>
        double WaterTarget(int hid)
        {
            var w = _w;
            var mem = w.Members(hid);
            double l = 0;
            for (int i = 0; i < mem.Count; i++) { ref var q = ref w.People.Get(mem[i]); if (q.Alive) l += Hearth.WaterNeed(q.AgeYears(w.Now)); }
            return l;
        }

        /// <summary>What the household's members now at the hearth or gone to the well will add (so that two do not do one task).</summary>
        void UnderWay(int hid, out double cooking, out double fetching) => UnderWay(hid, out cooking, out fetching, out _);

        void UnderWay(int hid, out double cooking, out double fetching, out double making)
        {
            var w = _w;
            long now = w.Now;
            var mem = w.Members(hid);
            cooking = 0; fetching = 0; making = 0;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var q = ref w.People.Get(mem[i]);
                if (!q.Alive || q.T1 <= now) continue;
                double left = (q.T1 - Math.Max(now, q.TA)) / (double)SimTime.Hour;
                if (q.Act == ActivityKind.Cook) cooking += Hearth.PrepPerHour(q.AgeYears(now), q.Health, true) * Knowledge.Tech.QuernFactor(QuernBest(hid)) * left;
                else if (q.Act == ActivityKind.Water) fetching += Hearth.Carry(q.AgeYears(now));
                else if (q.Act == ActivityKind.Craft) making += Goods.MakePerHour(q.AgeYears(now), q.Health) * Knowledge.Tech.LoomFactor(in q) * left;
            }
        }

        /// <summary>Food at home (or in the village's store) that a cook could make ready.</summary>
        bool CanCook(in Person p)
        {
            var w = _w;
            if (p.At == 0 || p.At != p.Home || !w.Households.Exists(p.Household)) return false;
            return w.Households.Get(p.Household).Larder > 0.01 || Threshing.Threshed(in w.Settlements.Get(p.At)) > 0.01;   // (grain out of the sheaf: C3.7b)
        }

        /// <summary>Grinding and cooking at the household's hearth until the next two meals are made (D2-056).</summary>
        void BeginCook(ref Person p, WhyCode why, double homeX, double homeY, ref RandomSequence rs)
        {
            var w = _w;
            ref var hh = ref w.Households.Get(p.Household);
            UnderWay(p.Household, out double cooking, out _);
            double want = 2.0 * MealOf(p.Household) - hh.Ready - cooking;
            double rate = Hearth.PrepPerHour(p.AgeYears(w.Now), p.Health, w.Settlements.Get(p.At).Woodstore > 0.05) * Knowledge.Tech.QuernFactor(QuernBest(p.Household));
            double h = DMath.Clamp(want / DMath.Max(0.05, rate), 0.5, 2.5);
            BeginLocal(ref p, ActivityKind.Cook, why, 0, homeX + rs.Range(-2.5, 2.5), homeY + rs.Range(-2.5, 2.5), SimTime.Hours(h));
        }

        /// <summary>To the well (or the stream where none is dug yet) with a pot: filling it, a word with whoever is there (D2-056).</summary>
        void BeginWater(ref Person p, in Settlement here, ref RandomSequence rs)
        {
            BeginLocal(ref p, ActivityKind.Water, WhyCode.WaterNeeded, 0, here.WellX + rs.Range(-2, 2), here.WellY + rs.Range(-2, 2), SimTime.Minutes(6 + 8 * rs.NextUnit()));
        }

        static double AgeFood(double age) => age < 2 ? 0.35 : (age < 12 ? 0.6 : (age > 60 ? 0.8 : 1.0));

        /// <summary>A body's build, keyed by id (0.75-1.25: Person.Build keeps it).</summary>
        public static double BuildOf(ulong seed, int id) => 0.75 + 0.5 * KeyedRng.Unit(seed, RngStream.Traits, (ulong)id, 103);

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
            double build = p.Derived != 0 ? p.Build : BuildOf(seed, p.Id);
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
            // at home, what is cooked (D2-056); away, a host's table
            if (p.At == p.Home && w.Households.Exists(p.Household)) return w.Households.Get(p.Household).Ready >= 0.4 * Portion(in p, w.Now);   // a meal, not scraps
            return p.At != 0 && Threshing.Threshed(in w.Settlements.Get(p.At)) > 0.01;   // (grain out of the sheaf: C3.7b)
        }

        internal void EatNow(ref Person p) => Eat(ref p, Portion(in p, _w.Now), false);

        /// <summary>A day's food at once (COARSE): the day's need, more while the body refills.</summary>
        internal void EatDay(ref Person p)
        {
            double cap = ReserveCap(in p, _w.Seed, _w.Now);
            Eat(ref p, DailyNeed(in p, _w.Now) * (1.0 + 0.3 * DMath.Clamp01(1.0 - p.Reserve / cap)), true);
        }

        /// <summary>A DEEP step's food at once (F8): the step's need, more while the body refills - as a COARSE day's, for its days.</summary>
        internal void EatDays(ref Person p, int days)
        {
            double cap = ReserveCap(in p, _w.Seed, _w.Now);
            Eat(ref p, DailyNeed(in p, _w.Now) * days * (1.0 + 0.3 * DMath.Clamp01(1.0 - p.Reserve / cap)), true);
        }

        void Eat(ref Person p, double need, bool coarse)
        {
            var w = _w;
            double got = 0;
            bool home = p.At == p.Home && w.Households.Exists(p.Household);
            if (home)
            {
                // a meal is what is cooked (D2-056); a COARSE day makes the rest ready from the larder with its share of hours
                ref var hh = ref w.Households.Get(p.Household);
                double take = Math.Min(need, hh.Ready);
                hh.Ready -= take; got += take;
                if (coarse && got < need - 1e-9) { take = Math.Min(need - got, hh.Larder); hh.Larder -= take; got += take; }
                // the pots empty as the day's food is eaten: a day's water with a day's food (COARSE keeps them filled)
                double dn = DailyNeed(in p, w.Now);
                if (!coarse && dn > 0) hh.Water = (float)DMath.Max(0.0, hh.Water - Hearth.WaterNeed(p.AgeYears(w.Now)) * got / dn);
            }
            if (got < need - 1e-9 && p.At != 0 && (coarse || !home))
            {
                ref var s = ref w.Settlements.Get(p.At);
                double take = Math.Min(need - got, DMath.Min(Threshing.Threshed(in s), w.Holdings.Available(p.At, p.Household)));   // (in hard times its own grain and the common's only: C3.3c; out of the sheaf: C3.7b)
                s.Granary -= take; got += take;
                w.Holdings.Take(p.At, p.Household, take);   // (one of its own: its grain first; a visitor's meal is the village's: C3.3)
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
            Green(p.At, out double homeX, out double homeY);
            if (atHome && w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); homeX = hh.HomeX; homeY = hh.HomeY; }
            var rs = NextRandom(ref p);

            // 0'. (session 32) a shock felt here a little while ago - a stone from the sky, the ground shaking, a bolt at the door: run clear of where it
            //     struck, then keep together on open ground with the others until it has passed; the badly hurt are taken in (abed, below)
            if (p.Alarm != 0 && now - p.AlarmT < 4 * SimTime.Hour && ShockStep(ref p, rs, night)) return;

            // 0. danger seen and still burning near this place: keep to shelter
            if (p.Alarm != 0 && now - p.AlarmT < 8 * SimTime.Hour && w.FireNearSettlement(p.At, w.Config.FirePerceptionRange))
            {
                BeginLocal(ref p, ActivityKind.Flee, WhyCode.FireNear, 0, homeX, homeY, SimTime.Hour);
                return;
            }

            // 0a. a voyage under way (C9.3): gathered at the landing, the crew put out together; at the other landing, back to the boat
            //     when the stay is over and home over the sea
            if (p.Voyage != 0 && VoyageStep(ref p)) return;

            // 0b. a catch reaches the larder when its carrier gets home with it (D2-042)
            if (p.Carry > 0f && p.At == p.Home)
            {
                BeginLocal(ref p, ActivityKind.Haul, WhyCode.BringHome, p.WhyArg, homeX, homeY, 10 * SimTime.Minute);
                return;
            }

            // 0c. a taker's work at the store is done (or they were seen): home by the road at once, in the dark (D2-069)
            if (!atHome && (p.Why == WhyCode.Take || p.Why == WhyCode.Raid) && TakerHome(ref p)) return;

            // 1. night: sleep until the morning - unless, going to bed hungry with nothing to eat, they go in the dark to a
            //    neighbour's full store (once a night, the first night decision before 2 am: D2-069)
            if (night)
            {
                // a band is to fall on a village tonight and they are of it: they go at bedtime, to come there together (D2-073)
                if (atHome && p.Band != 0 && w.Bands.Exists(p.Band))
                {
                    ref var band = ref w.Bands.Get(p.Band);
                    if (band.State == 0 && band.Arrive > now && band.Arrive - now <= SimTime.Hours(6) && p.Hurt < TC2.Core.Society.Conflict.RoadHurt && StartRaid(ref p)) return;
                }
                // the village keeps a watch at its store tonight and it is their turn: until two in the morning (D2-071)
                if (atHome && here.Watcher == p.Id && p.Act != ActivityKind.Watch && (lsec >= bed || lsec < 2 * SimTime.Hour))
                {
                    Construction.GranarySpot(in here, out double wx, out double wy);
                    long untilTwo = SimTime.FloorMod(2 * SimTime.Hour - lsec, SimTime.Day);
                    if (untilTwo > 0) { BeginLocal(ref p, ActivityKind.Watch, WhyCode.Watch, 0, wx + rs.Range(-2, 2), wy + rs.Range(-2, 2), untilTwo); return; }
                }
                if (atHome && (lsec >= bed || lsec < 2 * SimTime.Hour) && p.Why != WhyCode.Take && TC2.Core.Society.Conflict.CanTake(in p, now))
                {
                    w.Metrics.TakeWeighed++;
                    if (rs.Chance(w.Conflict.TakeChance(in p)))
                    {
                        int to = w.Conflict.TakeTarget(in p, out double free);
                        if (to != 0 && rs.Chance(free) && StartTake(ref p, to)) return;
                    }
                }
                else if (atHome && (lsec >= bed || lsec < 2 * SimTime.Hour) && p.Why != WhyCode.Take && here.Foe != 0 &&
                         TC2.Core.Society.Conflict.CanTakeBack(in p, now) && rs.Chance(w.Conflict.TakeBackChance(in p, in here)))
                {
                    // fed, but the village holds a heavy wrong against a neighbour: the bold go to take back (D2-072)
                    int to = w.Conflict.TakeBackTarget(in p, in here, out double free);
                    if (to != 0 && rs.Chance(free) && StartTake(ref p, to)) return;
                }
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
                // nothing cooked at home: a hungry one of eight or more makes the meal (D2-056); the small wait for someone who will
                if (age >= 8 && CanCook(in p)) { BeginCook(ref p, WhyCode.Hungry, homeX, homeY, ref rs); return; }
                // nothing threshed left in the store but its sheaves: a hungry hand threshes before foraging - an hour at the floor feeds a household
                // for days (C3.7b); while enough of the village's people are at the floor, the rest wait at home for the grain rather than go out
                if (age >= 12 && p.At == p.Home && p.Health > 0.3 && lsec >= 6 * SimTime.Hour && lsec < 19 * SimTime.Hour && !Threshing.Off && here.Sheaves > 0.5 &&
                    Threshing.Threshed(in here) < 1.0)
                {
                    if (here.ThreshOut < Threshing.DayNeed(w, in here)) { BeginThresh(ref p, ref here, true, ref rs); return; }
                    BeginLocal(ref p, ActivityKind.Idle, WhyCode.Hungry, 0, homeX, homeY, 40 * SimTime.Minute);
                    return;
                }
                if (p.HungryFrom == 0 && now - p.LastMeal > SimTime.Day) p.HungryFrom = now;
                // nothing at home or in the store: go out to what the woods, the game or the shore still hold (by day; D2-042)
                if (!child && lsec >= 6 * SimTime.Hour && lsec < 19 * SimTime.Hour && p.At == p.Home)
                {
                    int wk = WildChoice(in p, in here, age, now, out _, out int hboat);
                    if (wk >= 0) { BeginWild(ref p, ref here, wk, WhyCode.NoFood, ref rs, hboat); return; }
                }
            }

            // 3b. sick with a fever, a pox, a flux: abed at home until it passes (D2-066); badly hurt: abed until it heals (D2-071)
            if (p.SickPhase == TC2.Core.Health.Disease.Infectious && p.At == p.Home)
            {
                BeginLocal(ref p, ActivityKind.Ill, WhyCode.Sickness, p.Sick, homeX, homeY, SimTime.Hours(4));
                return;
            }
            if (p.Hurt >= TC2.Core.Society.Conflict.AbedHurt && p.At == p.Home)
            {
                BeginLocal(ref p, ActivityKind.Ill, WhyCode.Hurt, 0, homeX, homeY, SimTime.Hours(4));
                return;
            }

            // 4. visiting elsewhere: stay a while, then go home
            if (!atHome)
            {
                if (p.Voyage == 0 && (now >= p.Until || lsec > 16 * SimTime.Hour))
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
                Green(here.Id, out double vx, out double vy);
                if (here.Market != 0 && w.Buildings.Exists(here.Market)) { ref var mk = ref w.Buildings.Get(here.Market); if (mk.Done != 0) { vx = mk.X; vy = mk.Y; } }   // (at the stalls: C1.6)
                vx += rs.Range(-15, 15); vy += rs.Range(-15, 15);
                Ashore(here.Id, ref vx, ref vy);
                BeginLocal(ref p, ActivityKind.Visit, WhyCode.VisitKin, 0, vx, vy, stay);
                return;
            }

            // 5. children: from eight they take the household's animals out to their ground at first light and watch them until
            //    evening (D2-050); then chores - from six they carry water when the pots are low, from eight they help at the
            //    quern when nothing is cooked (D2-056); then they play near home or the square
            if (child && age >= 8 && atHome && lsec >= 6 * SimTime.Hour + 30 * SimTime.Minute && lsec < 15 * SimTime.Hour && Tend(ref p, ref here, lsec, true, ref rs)) return;
            if (child && age >= 6 && atHome && lsec >= 7 * SimTime.Hour && lsec < 19 * SimTime.Hour && w.Households.Exists(p.Household))
            {
                ref var hh = ref w.Households.Get(p.Household);
                UnderWay(p.Household, out double cooking, out double fetching);
                if (hh.Water + fetching < 0.5 * WaterTarget(p.Household) && rs.Chance(0.7)) { BeginWater(ref p, in here, ref rs); return; }
                if (age >= 8 && hh.Ready + cooking < MealOf(p.Household) && CanCook(in p) && rs.Chance(0.5)) { BeginCook(ref p, WhyCode.MealsToMake, homeX, homeY, ref rs); return; }
                // from eight a hand at the spinning and the plaiting when the household's goods are wearing thin (D2-058) - wares from the
                // village's store first (C3.1)
                TakeWares(p.Household);
                UnderWay(p.Household, out _, out _, out double making);
                if (age >= 8 && GoodsHeld(p.Household, making) < 0.85 && rs.Chance(0.35)) { BeginCraft(ref p, homeX, homeY, 1.0 + rs.NextUnit(), ref rs); return; }
            }
            if (child)
            {
                bool sq = rs.Chance(0.5);
                double px = homeX, py = homeY;
                if (sq) Green(here.Id, out px, out py);
                px += rs.Range(-20, 20); py += rs.Range(-20, 20);
                Ashore(here.Id, ref px, ref py);
                BeginLocal(ref p, ActivityKind.Play, WhyCode.Young, 0, px, py, SimTime.Hours(2 + 1.5 * rs.NextUnit()));
                return;
            }

            // 6. grown-ups: score what could be done now
            bool day = lsec >= 6 * SimTime.Hour && lsec < 19.5 * SimTime.Hour;
            double elder = age > 60 ? 0.5 : 1.0;
            double best = 0.08 + 0.05 * rs.NextUnit();
            int choice = 0; // 0 idle, 1 fields, 2 wood, 3 water, 4 social, 5 trip, 6 the wild, 7 building, 8 scouting, 9 tending, 10 cooking, 11 making, 12 trading, 13 an errand (amends), 14 quarrying, 15 the smithy, 16 practice, 17 mining, 18 charcoal, 19 bog ore, 20 the household's boat, 21 a voyage, 22 the weaver's shed, 23 the kiln, 24 the household's cart, 28 a load of the lord's tenth (C10.6)
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
            double fuelDays = here.FuelDay > 1e-6 ? here.Woodstore / here.FuelDay : 99.0;
            if (day && (here.Woodstore < woodTarget || woodShort || fuelDays < 6.0))
            {
                // (wood is wanted most when a roof or a work waits for it, and more yet when the hearths are about to go cold: D2-056)
                double u = woodShort ? 0.7 : 0.5 * (1.0 - here.Woodstore / woodTarget);
                if (fuelDays < 6.0) u = DMath.Max(u, 0.85 * (1.0 - fuelDays / 6.0));
                u = u * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 2; }
            }
            int ground = 0;
            bool sworn = TC2.Core.Society.Retinue.SwornHere(w, in p);
            if (sworn && atHome && lsec >= 14 * SimTime.Hour && lsec < 17 * SimTime.Hour && Training.Willing(in p, now) && p.Act != ActivityKind.Train &&
                (ground = Training.Ground(w, here.Id)) != 0)
            {
                // a man sworn to the village's first household practises at its ground every afternoon: it feeds him for that (C10.7b)
                double u = 0.9 + 0.1 * rs.NextUnit();
                if (u > best) { best = u; choice = 16; }
            }
            else if (!sworn && atHome && lsec >= 14 * SimTime.Hour && lsec < 17 * SimTime.Hour && Training.Willing(in p, now) && Training.Practising(w, in here, out ground) &&
                Training.Day(p.Id, SimTime.FloorDiv(now + w.LocalOffsetOf(p.At), SimTime.Day)) && p.Act != ActivityKind.Train)
            {
                // the village fears its neighbours: on their practice day the bold go to its training ground in the afternoon (C1.5)
                double u = (0.45 + 0.3 * p.Bold / 255.0) + 0.1 * rs.NextUnit();
                if (u > best) { best = u; choice = 16; }
            }
            int smithy = 0;
            if (day && atHome && lsec < 16 * SimTime.Hour && Forge.Wanted(w, in here, in p, out smithy))
            {
                // a smith with work at the village's smithy (C1.4): its tools short of metal with ore near, or arms wanted - their craft
                double u = (here.Arms < Forge.ArmsWant(in here) ? 0.7 : 0.5) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 15; }
            }
            int shed = 0;
            if (day && atHome && lsec < 16 * SimTime.Hour && Weaving.Wanted(w, in here, in p, out shed))
            {
                // one of the village's best weavers, with work at its weaver's shed (C3.1): its wares short of what it keeps - their craft
                double u = 0.5 * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 22; }
            }
            int kiln = 0;
            if (day && atHome && lsec < 15 * SimTime.Hour && Pottery.Wanted(w, in here, in p, out kiln))
            {
                // one of the village's best potters, with work at its kiln (C3.2): its jars or its hearths' pots short - their craft
                double u = 0.5 * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 23; }
            }
            int quarry = 0;
            if (day && atHome && lsec < 15 * SimTime.Hour && age >= 16 && age < 60 && p.Health > 0.6 && Stone.Wanted(w, in here, out quarry))
            {
                // the village wants stone for its houses' footings and its quarry stands (C1.3): cut and carry it - wanted as a village work is
                double u = (0.3 + 0.2 * DMath.Clamp01(1.0 - here.Stone / DMath.Max(1.0, (double)here.StoneWant))) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 14; }
            }
            int mine = 0;
            if (day && atHome && lsec < 14 * SimTime.Hour && age >= 16 && age < 60 && p.Health > 0.6 && Mining.Wanted(w, in here, out mine))
            {
                // the village's smiths want ore and its mine stands (C5.1): dig it and carry it home - wanted as a village work is
                double u = (0.3 + 0.2 * DMath.Clamp01(1.0 - here.Ore / DMath.Max(1.0, (double)here.OreWant))) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 17; }
            }
            int bog = 0;
            if (day && atHome && lsec < 14 * SimTime.Hour && age >= 16 && age < 60 && p.Health > 0.6 && Mining.Wanted(w, in here, out bog, Mining.Iron))
            {
                // the village's smiths want bog ore and its pits stand (C5.2b): dig it and carry it home - wanted as a village work is
                double u = (0.3 + 0.2 * DMath.Clamp01(1.0 - here.FeOre / DMath.Max(1.0, (double)here.FeOreWant))) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 19; }
            }
            int hearth = 0;
            if (day && atHome && lsec < 14 * SimTime.Hour && age >= 16 && age < 60 && p.Health > 0.6 && Charcoal.Wanted(w, in here, out hearth))
            {
                // the village's smiths want charcoal and its hearth stands at the woodlot (C5.2a): fell and burn - wanted as a village work is
                double u = (0.3 + 0.2 * DMath.Clamp01(1.0 - here.Charcoal / DMath.Max(1.0, (double)here.CharcoalWant))) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 18; }
            }
            if (day && canBuild)
            {
                // a camp wants a roof now; a hut wants to become a house; the village wants its well and its granary (D2-045)
                byte shelter = w.Households.Exists(p.Household) ? w.Households.Get(p.Household).Shelter : Construction.House;
                double need = ownRoof ? (shelter == Construction.Camp ? 0.85 : shelter == Construction.Hut ? 0.4 : 0.3) : 0.45;   // (a house rebuilt waits on the rest: D2-114)
                double u = need * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 7; }
            }
            if (day && atHome && w.Households.Exists(p.Household))
            {
                // the household's own work (D2-056): the next meal to make when nothing is cooked, water when the pots are low
                ref var hh = ref w.Households.Get(p.Household);
                UnderWay(p.Household, out double cooking, out double fetching);
                double meal = MealOf(p.Household), ready = hh.Ready + cooking;
                if (ready < meal && CanCook(in p))
                {
                    double u = (0.5 + 0.35 * (1.0 - ready / DMath.Max(1e-6, meal))) * (0.6 + 0.4 * p.Diligence / 255.0) + 0.1 * rs.NextUnit();
                    if (u > best) { best = u; choice = 10; }
                }
                double half = 0.5 * WaterTarget(p.Household), have = hh.Water + fetching;
                if (have < half)
                {
                    double u = 0.35 + 0.45 * (1.0 - have / DMath.Max(1e-6, half)) + 0.08 * rs.NextUnit();
                    if (u > best) { best = u; choice = 3; }
                }
            }
            if (atHome && w.Households.Exists(p.Household))
            {
                // making and mending (D2-058): the household's clothes and tools below what it keeps its members in - by day,
                // and by the fire in the evening (night is sleep, above) - once it has taken what wares the village's store holds (C3.1)
                TakeWares(p.Household);
                UnderWay(p.Household, out _, out _, out double making);
                double held = GoodsHeld(p.Household, making);
                if (held < 1.0)
                {
                    // (the slack's work: after a full day's labour it weighs less than rest and company)
                    double u = ((0.2 + 0.5 * (1.0 - held)) * (0.6 + 0.4 * p.Diligence / 255.0) + 0.1 * rs.NextUnit()) * DayLeft(in p);
                    if (u > best) { best = u; choice = 11; }
                }
            }
            if (day && atHome && age >= 16 && age < 60 && p.Health > 0.6 && Boats.HasWork(w, in here, p.Household))
            {
                // the household is making a boat (C9.1): its grown members fell, drag down and hollow the trunk, as they would raise a roof
                double u = 0.42 * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 20; }
            }
            if (day && atHome && w.Households.Exists(p.Household) && !Gardens.Off)
            {
                // the household's garden behind its house (C3.7a): dug and weeded while the crops grow, gathered from the ripening - its own work, as its
                // meals and water are, wanted as far as it is behindhand (above the slack's company and idling, below the fields, the meals and the fuel)
                ref var gHh = ref w.Households.Get(p.Household);
                double gBehind = Gardens.Behindhand(in gHh, SettlementSystem.DayOfYear(in here, now), here.CropGrowth);
                if (gBehind > 0.5)
                {
                    double u = Gardens.UrgeBase(gBehind, gHh.Garden) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.1 * rs.NextUnit();
                    if (u > best) { best = u; choice = 25; }
                }
            }
            if (day && atHome && age >= 14 && age < 65 && p.Health > 0.5 && lsec < 18 * SimTime.Hour && here.Sheaves > 0.5 && !Threshing.Off)
            {
                // the village's sheaves at its threshing floor (C3.7b): its threshed grain short of its need and its days ahead, less what others are
                // threshing now - wanted as a village work is, the more the shorter (a few days' worth in the sowing and the reaping, more in the slack)
                double target = Threshing.DayNeed(w, in here) * Threshing.AheadDays(here.Crop);
                double have = Threshing.Threshed(in here) + here.ThreshOut;
                if (have < target)
                {
                    double u = (0.25 + 0.6 * (1.0 - have / DMath.Max(1e-6, target))) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.1 * rs.NextUnit();
                    if (u > best) { best = u; choice = 26; }
                }
            }
            if (day && atHome && age >= 16 && age < 60 && p.Health > 0.6 && lsec < 17 * SimTime.Hour && here.Muck > 1.0 && Muck.Window(in here))
            {
                // the yards' muck out on the fields before the ploughing (C3.7c): wanted as a village work is, the more the bigger the heaps for its fields
                double heaps = here.Muck / 1000.0 / DMath.Max(1.0, here.FieldHa);
                double u = (0.30 + 0.25 * DMath.Clamp01(heaps / 3.0)) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.1 * rs.NextUnit();
                if (u > best) { best = u; choice = 27; }
            }
            if (day && atHome && age >= 16 && age < 60 && p.Health > 0.6 && Carts.HasWork(w, p.Household))
            {
                // the household is making a cart (C4.2): its grown members cut and shape the wheels, the axle and the bed in the yard - their own small job,
                // taken up beside the village's building
                double u = 0.5 * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.12 * rs.NextUnit();
                if (u > best) { best = u; choice = 24; }
            }
            int wildKind = -1, wildBoat = 0;
            if (day && lsec < 15 * SimTime.Hour)
            {
                // the woods, the game and the shore: by need (short rations, empty stores) and habit, as good as the grounds are
                int wk = WildChoice(in p, in here, age, now, out double expect, out int wboat);
                if (wk >= 0)
                {
                    double u = WildUrge(in p, in here, wk, expect, now) * elder * (0.6 + 0.4 * p.Diligence / 255.0) + 0.06 * rs.NextUnit();
                    if (u > best) { best = u; choice = 6; wildKind = wk; wildBoat = wboat; }
                }
            }
            if (lsec >= 7 * SimTime.Hour && lsec < 13 * SimTime.Hour && age >= 16 && age < 62 && p.Health > 0.7 && p.Hurt < TC2.Core.Society.Conflict.RoadHurt &&
                !(p.PregDue != 0 && p.PregDue - now < 10 * SimTime.Day) && !(p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) &&
                !(p.Alarm != 0 && now - p.AlarmT < SimTime.Day) &&
                rs.Chance(w.Config.TripChancePerDecision * (0.4 + 1.2 * p.Social / 255.0)))
            {
                tripDest = w.PickNeighbour(p.At, ref rs);
                if (tripDest != 0) { double u = 0.95; if (u > best) { best = u; choice = 5; } }
            }
            if (lsec >= 17 * SimTime.Hour) { double u = 0.25 + 0.3 * p.Social / 255.0 + 0.1 * rs.NextUnit(); if (u > best) { best = u; choice = 4; } }
            int tradeTo = 0;
            if (p.Errand != 0 && lsec >= 7 * SimTime.Hour && lsec < 11 * SimTime.Hour && atHome && p.Hurt < TC2.Core.Society.Conflict.RoadHurt && p.Sick == 0 &&
                w.Deeds.Exists(p.Errand) && w.Deeds.Get(p.Errand).Answer == 0)
            {
                // a wrong to ask amends for: the village sent them (D2-072)
                double u = 1.05; if (u > best) { best = u; choice = 13; }
            }
            else if (p.Errand != 0 && (!w.Deeds.Exists(p.Errand) || w.Deeds.Get(p.Errand).Answer != 0)) p.Errand = 0;
            if (lsec >= 7 * SimTime.Hour && lsec < 11 * SimTime.Hour && age >= 18 && age < 56 && p.Health > 0.7 && p.Hurt < TC2.Core.Society.Conflict.RoadHurt && atHome &&
                p.PregDue == 0 && !(p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2) && p.Sick == 0 &&
                rs.Chance(TradeChance * (0.5 + p.Bold / 255.0)))
            {
                // a load of what the village spares to a neighbour that wants it more, for what it wants back (D2-067)
                tradeTo = w.Trade.BestPartner(p.Home, out double gain);
                if (tradeTo != 0) { double u = 0.9; if (u > best) { best = u; choice = 12; } }
            }
            int lordTo = 0;
            if (lsec >= 7 * SimTime.Hour && lsec < 11 * SimTime.Hour && atHome && here.CarryToday > 0 && here.TributeDue >= 1.0 && here.Lord != 0 &&
                TC2.Core.Society.Conflict.CanCarry(in p, now))
            {
                // the village's tenth for its lord stands set aside: a grown member takes a load of it to the lord's store (C10.6)
                double u = 0.93; if (u > best) { best = u; choice = 28; lordTo = here.Lord; }
            }
            int voyage = -1;
            if (atHome && lsec >= 6 * SimTime.Hour && lsec < 12 * SimTime.Hour && (voyage = w.Trade.BookingOf(p.Household)) >= 0 && Trade.CanSail(in p, p.Home, now))
            {
                // the village has booked the household's boat for a voyage (C9.3): one of them takes it out with a crew of the household
                double u = 1.1; if (u > best) { best = u; choice = 21; }
            }
            if (day && atHome && lsec < 14 * SimTime.Hour && (age < 16 || lsec >= 9 * SimTime.Hour) && TendDue(here.Id))
            {
                // the animals wait to go out: a youth's chore first, a grown member's when no child has taken them by mid-morning (D2-050)
                double u = (age < 16 ? 0.75 : 0.42) * elder + 0.1 * rs.NextUnit();
                if (u > best) { best = u; choice = 9; }
            }
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
                {
                    // (out of the water: a village's field spot lies where its founders' arable land lay, and where the sea or a lake has come over it
                    // since, its field hands stood in the water - 0.6 % of all person-hours in a year-300 world, `wet`; D2-111's rule)
                    // (on its household's own part of the fields since session 20 - every hand of a village stood on its one field spot before)
                    FieldSpots.Spot(w, in here, p.Household, 60.0, ref rs, out double fx, out double fy);
                    Ashore(here.Id, ref fx, ref fy);
                    BeginLocal(ref p, ActivityKind.Fields, here.Crop == CropStage.Ripe ? WhyCode.Harvest : WhyCode.CropNeedsWork, 0, fx, fy, SimTime.Hours(3 + 1.5 * rs.NextUnit()));
                    return;
                }
                case 2:
                {
                    double wx = here.WoodX + rs.Range(-50, 50), wy = here.WoodY + rs.Range(-50, 50);
                    Ashore(here.Id, ref wx, ref wy);   // (a woodlot by the shore: its cutters - and their carts - stood in the sea, C4.2's shots)
                    BeginLocal(ref p, ActivityKind.Wood, WhyCode.WoodLow, 0, wx, wy, SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
                    return;
                }
                case 3:
                    BeginWater(ref p, in here, ref rs);
                    return;
                case 10:
                    BeginCook(ref p, WhyCode.MealsToMake, homeX, homeY, ref rs);
                    return;
                case 11:
                    BeginCraft(ref p, homeX, homeY, 1.5 + 1.5 * rs.NextUnit(), ref rs);
                    return;
                case 12:
                    if (StartTrade(ref p, tradeTo)) return;
                    break;
                case 28:
                    if (StartCarry(ref p, lordTo)) return;
                    break;
                case 21:
                    if (StartVoyage(ref p, voyage)) return;
                    if (!p.Alive) return;   // (caught out by the wind as they put out: C9.3)
                    break;
                case 13:
                    if (StartErrand(ref p)) return;
                    break;
                case 16:
                    {
                        ref var gb = ref w.Buildings.Get(ground);
                        BeginLocal(ref p, ActivityKind.Train, WhyCode.Train, 0, gb.X + rs.Range(-8, 8), gb.Y + rs.Range(-6, 6), SimTime.Hours(Training.Session));
                        return;
                    }
                case 23:
                    {
                        ref var kb = ref w.Buildings.Get(kiln);
                        BeginLocal(ref p, ActivityKind.Pot, WhyCode.Pot, kiln, kb.X + rs.Range(-4, 4), kb.Y + rs.Range(-4, 4), SimTime.Hours(3.0 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 22:
                    {
                        ref var wb = ref w.Buildings.Get(shed);
                        BeginLocal(ref p, ActivityKind.Weave, WhyCode.Weave, shed, wb.X + rs.Range(-3, 3), wb.Y + rs.Range(-3, 3), SimTime.Hours(3.0 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 15:
                    {
                        ref var sb = ref w.Buildings.Get(smithy);
                        BeginLocal(ref p, ActivityKind.Smith, WhyCode.Forge, smithy, sb.X + rs.Range(-2, 2), sb.Y + rs.Range(-2, 2), SimTime.Hours(3.0 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 19:
                    {
                        ref var bb = ref w.Buildings.Get(bog);
                        // (a stint as long as the ore still wanted takes, half an hour at least)
                        double need = DMath.Max(0.0, (double)here.FeOreWant - here.FeOre) / DMath.Max(1.0, Mining.HourRate(w, here.Id, Mining.DigRate(in p, Tools(p.Household)), false, Mining.Iron));
                        BeginLocal(ref p, ActivityKind.Mine, WhyCode.BogOre, 0, bb.X + rs.Range(-5, 5), bb.Y + rs.Range(-5, 5), SimTime.Hours(DMath.Clamp(need, 0.5, 3.0 + 1.5 * rs.NextUnit())));
                        return;
                    }
                case 18:
                    {
                        ref var hb = ref w.Buildings.Get(hearth);
                        // (a stint as long as the charcoal still wanted takes, half an hour at least)
                        double need = DMath.Max(0.0, (double)here.CharcoalWant - here.Charcoal) / DMath.Max(0.5, Charcoal.HourRate(w, here.Id, (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health) * Tools(p.Household), false));
                        BeginLocal(ref p, ActivityKind.Burn, WhyCode.Charcoal, 0, hb.X + rs.Range(-5, 5), hb.Y + rs.Range(-5, 5), SimTime.Hours(DMath.Clamp(need, 0.5, 3.0 + 1.5 * rs.NextUnit())));
                        return;
                    }
                case 17:
                    {
                        ref var mb = ref w.Buildings.Get(mine);
                        // (a stint as long as the ore still wanted takes, half an hour at least)
                        double need = DMath.Max(0.0, (double)here.OreWant - here.Ore) / DMath.Max(1.0, Mining.HourRate(w, here.Id, Mining.DigRate(in p, Tools(p.Household)), false));
                        BeginLocal(ref p, ActivityKind.Mine, WhyCode.Ore, 0, mb.X + rs.Range(-4, 4), mb.Y + rs.Range(-4, 4), SimTime.Hours(DMath.Clamp(need, 0.5, 3.0 + 1.5 * rs.NextUnit())));
                        return;
                    }
                case 14:
                    {
                        ref var qb = ref w.Buildings.Get(quarry);
                        BeginLocal(ref p, ActivityKind.Quarry, WhyCode.Stone, 0, qb.X + rs.Range(-6, 6), qb.Y + rs.Range(-6, 6), SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 25:
                    {
                        ref var gHh = ref w.Households.Get(p.Household);   // (at its croft: the first free strip behind or beside the house)
                        double gx = gHh.HomeX, gy = gHh.HomeY;
                        if (gHh.Building != 0 && w.Buildings.Exists(gHh.Building)) Gardens.Measure(w, p.Home, gHh.Building, out gx, out gy);
                        BeginLocal(ref p, ActivityKind.Garden, WhyCode.Garden, 0, gx + rs.Range(-2, 2), gy + rs.Range(-2, 2), SimTime.Hours(1.5 + 1.0 * rs.NextUnit()));
                        return;
                    }
                case 26:
                    BeginThresh(ref p, ref here, false, ref rs);
                    return;
                case 27:
                    {
                        // (out on the village's fields, where its field hands work; a stint of a few loads)
                        FieldSpots.Spot(w, in here, p.Household, 50.0, ref rs, out double mx, out double my);   // (onto its household's own part of them)
                        Ashore(here.Id, ref mx, ref my);
                        BeginLocal(ref p, ActivityKind.Muck, WhyCode.Muck, 0, mx, my, SimTime.Hours(1.5 + 1.0 * rs.NextUnit()));
                        return;
                    }
                case 24:
                    {
                        ref var ch = ref w.Households.Get(p.Household);   // (in the yard by the house)
                        BeginLocal(ref p, ActivityKind.Build, WhyCode.Cart, 0, ch.HomeX + rs.Range(-4, 4), ch.HomeY + rs.Range(-4, 4), SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 20:
                    {
                        ref var bb = ref w.Buildings.Get(Boats.Making(w, p.Household));
                        BeginLocal(ref p, ActivityKind.Build, WhyCode.Boat, 0, bb.X + rs.Range(-3, 3), bb.Y + rs.Range(-3, 3), SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 4:
                    {
                        Green(here.Id, out double gx, out double gy);
                        gx += rs.Range(-12, 12); gy += rs.Range(-12, 12);
                        Ashore(here.Id, ref gx, ref gy);
                        BeginLocal(ref p, ActivityKind.Social, WhyCode.Leisure, 0, gx, gy, SimTime.Hours(1.5 + 1.5 * rs.NextUnit()));
                        return;
                    }
                case 6:
                    BeginWild(ref p, ref here, wildKind, WildNeed(in here, w.Now) > 0 ? WhyCode.FoodShort : WhyCode.Provide, ref rs, wildBoat);
                    return;
                case 8:
                    if (BeginScout(ref p, ref here, ref rs)) return;
                    break;
                case 9:
                    if (Tend(ref p, ref here, lsec, false, ref rs)) return;
                    break;
                case 7:
                    {
                        // at their own lot, or at the village's well or granary
                        // (where their hours go: their own roof, or the village's work - C7.1: a rising palisade before a hut is made a house)
                        bool own = Construction.OwnWork(w, in here, p.Household);
                        Construction.WorkSpot(w, in here, Construction.NextWork(in here), rs.NextUnit(), out double gx, out double gy);
                        double bx = own ? homeX : gx;
                        double by = own ? homeY : gy;
                        bool rebuild = own && w.Households.Exists(p.Household) && w.Households.Get(p.Household).Shelter >= Construction.House;
                        BeginLocal(ref p, ActivityKind.Build, rebuild ? WhyCode.Rebuild : own ? WhyCode.NoRoof : WhyCode.VillageWork, 0, bx + rs.Range(-3, 3), by + rs.Range(-3, 3), SimTime.Hours(2.5 + 1.5 * rs.NextUnit()));
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

        // ---------------------------------------------------------------- tending the household's animals (D2-050)
        /// <summary>The village's herd wants more herders today than have gone out with it.</summary>
        bool TendDue(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            int today = (int)SimTime.FloorDiv(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            int out_ = s.Tended == today ? s.HerdersOut : 0;
            return out_ < w.Stock.HerdersWanted(sid);
        }

        /// <summary>
        /// Out with the village's herd - the households' cattle and goats together - to today's ground: a child until evening, a
        /// grown member for a stint of four or five hours. False when the herd has its herders or there is nowhere to graze.
        /// </summary>
        bool Tend(ref Person p, ref Settlement here, long lsec, bool child, ref RandomSequence rs)
        {
            var w = _w;
            if (!TendDue(here.Id)) return false;
            int today = (int)SimTime.FloorDiv(w.Now + w.LocalOffsetOf(here.Id), SimTime.Day);
            int ground = w.Stock.GroundOf(here.Id, today);
            if (ground < 0) return false;
            here.SphereToLocal(w.Planet.Grid.Center[ground], w.Planet.Radius, out double x, out double y);
            long stay = child ? Math.Max(SimTime.Hour, 17 * SimTime.Hour + 36 * SimTime.Minute - lsec) : SimTime.Hours(4.0 + rs.NextUnit());
            if (here.Tended != today) { here.Tended = today; here.HerdersOut = 0; }
            here.HerdersOut++;
            w.Metrics.TendDays++;
            BeginLocal(ref p, ActivityKind.Tend, WhyCode.Herd, ground, x + rs.Range(-25, 25), y + rs.Range(-25, 25), stay);
            return true;
        }

        // ---------------------------------------------------------------- the wild's food (D2-042)
        /// <summary>How hard a village's stores push its people to the wild: the ration's shortfall, or stores run dry.</summary>
        public static double WildNeed(in Settlement s, long now) => WildNeed(in s, now, s.Ration);

        /// <summary>The same, with the ration given.</summary>
        public static double WildNeed(in Settlement s, long now, double ration)
        {
            // a mild cut (prudence keeping a reserve) sends a few more out; a deep one sends everyone who can go
            double shortage = DMath.Max(0.0, 1.0 - ration);
            double need = 1.8 * shortage;
            if (s.StoreSeen(now) < 1.0) need = DMath.Max(need, 1.0);   // (the store as days lived hold it: F8)
            return need;
        }

        /// <summary>
        /// A person's own pull to the craft on an ordinary day - the bold hunt, the skilled go back to what they know - worth
        /// less the fuller the village's stores (food beyond years of need only spoils): a quarter of it when the granary holds
        /// a year and a half for everyone.
        /// </summary>
        static double WildHabit(in Person p, in Settlement here, int kind, long now)
        {
            double habit = kind == WildFood.Hunting ? 0.02 + 0.06 * p.Bold / 255.0 + 0.22 * p.SkHunt :
                           kind == WildFood.Fishing ? 0.03 + 0.22 * p.SkFish : 0.0;
            double years = here.StoreSeen(now) / (DMath.Max(1, here.Population) * SimTime.DaysPerYear);
            return habit * (1.0 - 0.75 * DMath.Clamp01(years / 1.5));
        }

        static double WildUrge(in Person p, in Settlement here, int kind, double expect, long now) => WildUrge(in p, in here, kind, expect, now, here.Ration);
        static double WildUrge(in Person p, in Settlement here, int kind, double expect, long now, double ration) =>
            (WildNeed(in here, now, ration) + WildHabit(in p, in here, kind, now)) * DMath.Clamp(expect / 0.7, 0.0, 1.3);

        /// <summary>
        /// The livelihood of the wild a person would take up now (the best FU an hour they can expect from today's survey of the
        /// grounds), or -1. Hunting asks a grown, healthy body free of a baby at the breast or one due; fishing and gathering less.
        /// </summary>
        public int WildChoice(in Person p, in Settlement here, double age, long now, out double expect) => WildChoice(in p, in here, age, now, out expect, out _);

        /// <summary>The same, and the household's boat when they would take it out to the sea's grounds (0: the shore; C9.1).</summary>
        public int WildChoice(in Person p, in Settlement here, double age, long now, out double expect, out int boat)
        {
            expect = 0; int kind = -1; boat = 0;
            bool heavy = p.PregDue != 0 && p.PregDue - now < 10 * SimTime.Day;
            bool nursing = p.NursingUntil != 0 && p.NursingUntil - now > SimTime.Year / 2;
            if (age >= 16 && age < 60 && p.Health > 0.5 && !heavy && !nursing && here.Hunt.Expect > 0f && here.Hunt.A >= 0)
            { double e = here.Hunt.Expect * (0.5 + p.SkHunt); if (e > expect) { expect = e; kind = WildFood.Hunting; } }
            if (age >= 14 && age < 70 && !heavy && here.Fish.Expect > 0f && here.Fish.A >= 0)
            { double e = here.Fish.Expect * (0.5 + p.SkFish); if (e > expect) { expect = e; kind = WildFood.Fishing; } }
            if (age >= 14 && age < 60 && !heavy && here.Sea.A >= 0 && here.Fish.A >= 0)
            {
                // the household's boat, a seat free and the wind fair: the open sea, the row paid (C9.1)
                int b = SeaBoat(in p, in here);
                if (b != 0) { double e = SeaHere(in here, out _, Boats.IsPlank(_w.Buildings.Get(b).Kind)) * (0.5 + p.SkFish); if (e > expect) { expect = e; kind = WildFood.Fishing; boat = b; } }
            }
            if (age >= 12 && here.Gath.Expect > 0f && here.Gath.A >= 0)
            { double e = here.Gath.Expect * (0.6 + 0.4 * p.Health); if (e > expect) { expect = e; kind = WildFood.Gathering; boat = 0; } }
            return kind;
        }

        /// <summary>
        /// The household's boat if this person could take it out now (C9.1): sound, lying at this village's landing, the wind over the sea's
        /// grounds below what anyone rows out in, and - lived - not out at sea already (a boat goes out once, with its crew: C9.2; before,
        /// a second of the household took the "same" boat out hours after the first, and the view drew two), COARSE a seat of the day's trip
        /// free; or 0.
        /// </summary>
        int SeaBoat(in Person p, in Settlement here)
        {
            var w = _w;
            int b = Boats.Of(w, p.Household);
            if (b == 0 || w.Buildings.Get(b).Settlement != here.Id) return 0;
            if (!_seatsOn && w.Trade.IsBooked(b)) return 0;   // (it is to sail on a voyage today: C9.3)
            byte bk = w.Buildings.Get(b).Kind;
            SeaHere(in here, out double wind);
            if (wind >= Boats.MaxOf(bk)) return 0;   // (a plank boat goes out in winds that keep dugouts ashore: C9.2b)
            if (!_seatsOn)
            {
                // lived: at the landing, or just put out with a seat free - they catch it before it is out (a crew gathers as it leaves)
                int aboard = Boats.Aboard(w, p.Household, b);
                return aboard == 0 || (aboard < Boats.SeatsOf(bk) && Outing(p.Household, b, out _) != 0) ? b : 0;
            }
            return (_seats.TryGetValue(b, out int n) ? n : 0) < Boats.SeatsOf(bk) ? b : 0;
        }

        /// <summary>How long a boat's crew waits for those who would come (lived): one who chooses the sea this soon after it put out joins it.</summary>
        public const long CrewWait = 30 * SimTime.Minute;

        /// <summary>The one of the household who took its boat out, if the boat put out within CrewWait (0 otherwise); `back` when its trip ends.</summary>
        int Outing(int hid, int boat, out long back)
        {
            var w = _w;
            back = 0;
            var mem = w.Members(hid);
            long now = w.Now;
            int first = 0; long t0 = long.MaxValue;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var q = ref w.People.Get(mem[i]);
                if (!q.Alive || q.Act != ActivityKind.Sail || q.WhyArg != boat) continue;
                if (q.T0 < t0) { t0 = q.T0; first = mem[i]; back = q.T1; }
            }
            return first != 0 && now - t0 <= CrewWait ? first : 0;
        }

        /// <summary>
        /// Lived (C9.2): one of the household takes its boat out - those of it at leisure in the village who could go to sea go with them, a
        /// seat each: the same boat, the same ground, back together. Returns how many went with the one who took it out.
        /// </summary>
        int Crew(ref Person skipper, ref Settlement here, int boat, WhyCode why, int ground, double tx, double ty)
        {
            var w = _w;
            long now = w.Now;
            var mem = w.Members(skipper.Household);
            int crew = 0;
            int seats = Boats.SeatsOf(w.Buildings.Get(boat).Kind);
            for (int i = 0; i < mem.Count && 1 + crew < seats; i++)
            {
                if (mem[i] == skipper.Id) continue;
                ref var q = ref w.People.Get(mem[i]);
                if (!q.Alive || q.At != here.Id || q.Home != here.Id || q.Band != 0) continue;
                if (q.Act != ActivityKind.Idle && q.Act != ActivityKind.Social && q.Act != ActivityKind.Craft) continue;   // (at leisure, or mending what can wait)
                double age = q.AgeYears(now);
                if (age < 14 || age >= 60 || q.Health <= 0.5) continue;
                if (q.PregDue != 0 && q.PregDue - now < 10 * SimTime.Day) continue;
                if (q.NursingUntil != 0 && q.NursingUntil - now > SimTime.Year / 2) continue;
                BeginLocal(ref q, ActivityKind.Sail, why, boat, tx, ty, 0, skipper.T1, Boats.RowPace(w.Buildings.Get(boat).Kind));
                q.Ground = ground;
                crew++;
                w.Metrics.FishTrips++; w.Metrics.BoatTrips++;
            }
            if (crew > 0) w.Metrics.CrewSeats += crew;
            return crew;
        }

        /// <summary>A village's sea now (C9.1): what an hour at its sea's grounds is worth, the row paid, and the wind over them - read once
        /// a village and moment (each of its boat households' people asked it: two spherical distances and every storm's wind a person).</summary>
        double SeaHere(in Settlement here, out double wind, bool plank = false)
        {
            var w = _w;
            var st = w.Planet.Storms;
            double storms = st.Count;   // (the storms as they are: a storm born, moved or grown since is another sea)
            for (int i = 0; i < st.Count; i++) storms += (double)st[i].Wind + st[i].At.X + st[i].At.Y;
            if (_seaSid != here.Id || _seaAt != w.Now || _seaGround != here.Sea.A || _seaExpect != here.Sea.Expect || _seaStorms != storms)
            {
                _seaSid = here.Id; _seaAt = w.Now; _seaGround = here.Sea.A; _seaExpect = here.Sea.Expect; _seaStorms = storms;
                _seaValue = Boats.SeaExpect(w, in here); _seaValueP = Boats.SeaExpect(w, in here, true); _seaWind = Boats.SeaWind(w, in here);
            }
            wind = _seaWind;
            return plank ? _seaValueP : _seaValue;
        }
        int _seaSid, _seaGround; long _seaAt; float _seaExpect; double _seaValue, _seaValueP, _seaWind, _seaStorms;   // (a cache, not state)

        readonly System.Collections.Generic.Dictionary<int, int> _seats = new System.Collections.Generic.Dictionary<int, int>();   // COARSE: seats taken in each boat today
        bool _seatsOn;
        readonly System.Collections.Generic.List<int> _drowned = new System.Collections.Generic.List<int>();   // COARSE: the drowned, to die after the day's loop
        readonly System.Collections.Generic.List<long> _drownedEv = new System.Collections.Generic.List<long>();   // and each one's loss (its event)


        /// <summary>Out to one of the village's best grounds for the livelihood (a shore for fishing), for a hunt's or a gathering's length.</summary>
        void BeginWild(ref Person p, ref Settlement here, int kind, WhyCode why, ref RandomSequence rs, int boat = 0)
        {
            var w = _w;
            if (boat != 0 && kind == WildFood.Fishing)
            {
                // out in the household's boat to one of the sea's grounds (C9.1): rowed out over the water, fishing there
                int skipper = Outing(p.Household, boat, out long back);
                if (skipper != 0)
                {
                    // the boat has just put out with a seat free: they go with it - the same ground, back with the rest (C9.2)
                    ref var sk = ref w.People.Get(skipper);
                    BeginLocal(ref p, ActivityKind.Sail, why, boat, sk.ToX, sk.ToY, 0, back, Boats.RowPace(w.Buildings.Get(boat).Kind));
                    p.Ground = sk.Ground;
                    w.Metrics.FishTrips++; w.Metrics.BoatTrips++; w.Metrics.CrewSeats++;
                    if (why != WhyCode.Provide) w.SettlementSys.NoteWildFood(here.Id);
                    return;
                }
                int sg = WildFood.PickGround(in here.Sea, ref rs);
                if (sg < 0) return;
                here.SphereToLocal(w.Planet.Grid.Center[sg], w.Planet.Radius, out double sx, out double sy);
                double tx = sx + rs.Range(-60, 60), ty = sy + rs.Range(-60, 60);
                BeginLocal(ref p, ActivityKind.Sail, why, boat, tx, ty, SimTime.Hours(3.5 + 1.5 * rs.NextUnit()), 0, Boats.RowPace(w.Buildings.Get(boat).Kind));
                p.Ground = sg;
                w.Metrics.FishTrips++; w.Metrics.BoatTrips++; w.Metrics.BoatOutings++;
                Crew(ref p, ref here, boat, why, sg, tx, ty);   // (its crew: those of the household at leisure go too - C9.2)
                if (why != WhyCode.Provide) w.SettlementSys.NoteWildFood(here.Id);
                return;
            }
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
            Stone.Saw(w, here.Id, p.Id, ground);   // (what rock shows where they go: C1.3)
            if (kind == WildFood.Hunting) w.Metrics.HuntTrips++; else if (kind == WildFood.Fishing) w.Metrics.FishTrips++; else w.Metrics.GatherTrips++;
            if (why != WhyCode.Provide) w.SettlementSys.NoteWildFood(here.Id);
        }

        /// <summary>
        /// Where a village's people gather (C9.2): its square, or - where the sea has risen over the square - the dry ground nearest it. Shots of
        /// drowned towns (0-5 squares a world under the sea by year 300) showed their people at leisure standing out in the sea.
        /// </summary>
        void Green(int sid, out double x, out double y)
        {
            var w = _w; var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            if (_greens.TryGetValue(sid, out var g) && g.Coast == P.CoastVersion && g.Relief == P.ReliefVersion && g.Water == P.WaterDay && g.Sea == P.SeaLevel && g.Sx == s.SquareX && g.Sy == s.SquareY) { x = g.X; y = g.Y; return; }
            x = s.SquareX; y = s.SquareY;
            Boats.OutOfTheWater(w, in s, ref x, ref y);
            _greens[sid] = new GreenAt { Coast = P.CoastVersion, Relief = P.ReliefVersion, Water = P.WaterDay, Sea = P.SeaLevel, Sx = s.SquareX, Sy = s.SquareY, X = x, Y = y };
        }

        /// <summary>A spot near the green that falls in the water moved to the nearest one out of it.</summary>
        void Ashore(int sid, ref double x, ref double y) => Boats.OutOfTheWater(_w, in _w.Settlements.Get(sid), ref x, ref y);

        struct GreenAt { public int Coast, Relief, Water; public double Sea; public float Sx, Sy; public double X, Y; }
        readonly System.Collections.Generic.Dictionary<int, GreenAt> _greens = new System.Collections.Generic.Dictionary<int, GreenAt>();   // (a cache of each village's green: by the coast, the relief, the sea's level and the day's lakes - the ground and water round it; a
        // green kept while a lake rose made a loaded world, which reads the water afresh, go another way: D2-119)

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
            w.Trade.ClearBookings();   // (nobody will sail what was booked: C9.3)
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                if (p.Step == StepKind.Local || p.Step == StepKind.Stay) CutStep(ref p);
                if (p.At != 0 && p.At != p.Home) w.Visitors(p.At).Remove(p.Id);
                p.At = p.Home;
                if ((p.CargoQty > 0f || p.CargoTo != Trade.ToStore) && w.Settlements.Exists(p.Home)) w.Trade.Unload(ref p, p.Home);   // (a trader's load reaches home with them: D2-067; a boat's too, C9.3)
                p.Voyage = 0;
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
        readonly List<int> _tendBy = new List<int>(), _turns = new List<int>();   // today's herders who give working hours to it (COARSE)

        /// <summary>
        /// COARSE: up to `want` of a village's residents aged [lo, hi) in health go out with the herd today, by turns (the turn
        /// moves on each day); those of twelve and over give their stint's hours. Returns how many went.
        /// </summary>
        int Herders(int sid, int today, int want, double lo, double hi, double health)
        {
            var w = _w;
            var res = w.Residents(sid);
            long now = w.Now;
            _turns.Clear();
            for (int i = 0; i < res.Count; i++)
            {
                ref var m = ref w.People.Get(res[i]);
                double a = m.AgeYears(now);
                if (m.Alive && m.Health >= health && a >= lo && a < hi && !_tendBy.Contains(res[i])) _turns.Add(res[i]);
            }
            int went = 0;
            for (int k = 0; k < _turns.Count && went < want; k++)
            {
                int pid = _turns[(int)((today + k) % _turns.Count)];
                if (w.People.Get(pid).AgeYears(now) >= 12) _tendBy.Add(pid);
                went++;
            }
            return went;
        }
        public const double TendStint = 4.5;             // hours a youth or a grown member gives to the herd (a lived stint is 4-5)
        public const double LabourDay = 9.5;             // a COARSE day's labour, as far as a lived day's slack work fills it (fitted, D2-058)

        /// <summary>Tools: COARSE hours by use - hearth wood, crops, building, (unused), building after the wild, store wood, the hours
        /// people had, and the person-days they had them for; null in the game.</summary>
        public static double[] Hours;

        /// <summary>Tools (C3.6 survey; null in the game): a COARSE working person's day by whether its village's arable within reach is short
        /// of its people's need (0 open, 1 bound) and the crop's stage (5) - person-days, crop hours, hours left after the crops, wild hours, wild FU.</summary>
        public static double[] LandHours;
        public const int LandK = 5;
        static int LandSlot(World w, in Settlement s) => ((s.ArableHa < s.Population * w.Config.FieldHaPerPerson ? 1 : 0) * 5 + (int)s.Crop) * LandK;

        /// <summary>A COARSE day's making and mending for one person (D2-058): up to maxH hours while the household holds less than
        /// `below` of what it keeps its members in.</summary>
        void CoarseCraft(ref Person p, double age, double maxH, double below, int days = 1)
        {
            var w = _w;
            if (maxH <= 0 || !w.Households.Exists(p.Household)) return;
            int members = w.Members(p.Household).Count;
            if (members == 0) return;
            TakeWares(p.Household);   // (wares from the village's store before its own hands make more: C3.1)
            ref var hh = ref w.Households.Get(p.Household);
            double want = below * members * Goods.Target - hh.Gear;
            if (want <= 0) return;
            maxH = Smith(ref p, maxH, days);   // (a smith's half at the furnace: D2-064)
            double rate = Goods.MakePerHour(age, p.Health) * Knowledge.Tech.LoomFactor(in p);
            double h = DMath.Min(maxH, want / rate);
            double made = Goods.Materials(ref w.Settlements.Get(hh.Settlement), h * rate);   // (as far as the store's fibre and hides go: D2-063)
            hh.Gear = (float)(hh.Gear + made);
            w.Metrics.CraftHours += h; w.Metrics.GearMade += made;
            w.Learning.Practice(ref p, Knowledge.Tech.Loom, h);
            w.Learning.Practice(ref p, Knowledge.Tech.Metal, h);
        }

        /// <summary>Tools: a COARSE day's garden takes its hours before all but the fields, the fuel and the crafts' places, as C3.7a built it
        /// (`--oldgardenorder`: the worlds before D2-146).</summary>
        public static bool CoarseGardenOld;

        /// <summary>
        /// Whether a COARSE day's garden takes this member's hours now (true), after the household's own building (`later`), or not today: what its
        /// season still wants, against the wild when hunger presses and against building its own roof - each by the urge a lived day gives it.
        /// </summary>
        /// <summary>
        /// Whether a COARSE day's muck waits for the household's own roof (D2-158): the roof's urge (a camp's 0.85, a hut's 0.4, a house's none) beats
        /// the muck's as a lived day weighs it (0.30 + 0.25 of the heaps a field hectare up to 3 t, by diligence, and its chance - 0.05 on average).
        /// </summary>
        bool RoofBeforeMuck(World w, in Settlement s, in Person p)
        {
            if (!w.Households.Exists(p.Household)) return false;
            ref var hh = ref w.Households.Get(p.Household);
            double roof = hh.Shelter == Construction.Camp ? 0.85 : hh.Shelter == Construction.Hut ? 0.4 : 0.0;
            if (roof <= 0) return false;
            double heaps = s.Muck / 1000.0 / DMath.Max(1.0, s.FieldHa);
            double u = (0.30 + 0.25 * DMath.Clamp01(heaps / 3.0)) * (0.6 + 0.4 * p.Diligence / 255.0) + 0.05;
            return roof > u;
        }

        /// <summary>`--oldmuckorder`: a COARSE day's muck carted before the household's own roof, as C3.7c built it (D2-158's control).</summary>
        public static bool CoarseMuckOld;

        bool GardenOrder(World w, in Settlement s, in Person p, int days, long now, bool shortNow, double ration, bool old, out bool later)
        {
            later = false;
            if (!w.Households.Exists(p.Household)) return false;
            if (old) return true;
            ref var hh = ref w.Households.Get(p.Household);
            double behind = Gardens.Behindhand(in hh, SettlementSystem.DayOfYear(in s, now + (days - 1) * SimTime.Day), s.CropGrowth);
            if (behind <= 0) return false;
            double u = Gardens.UrgeBase(behind, hh.Garden) * (0.6 + 0.4 * p.Diligence / 255.0) + 0.05;   // (as a lived day's, its chance 0.05 on average)
            if (shortNow && WildNeed(in s, now, ration) > u) return false;   // (the wild first)
            double roof = hh.Shelter == Construction.Camp ? 0.85 : hh.Shelter == Construction.Hut ? 0.4 : 0.0;
            if (roof > u) { later = true; return false; }
            return true;
        }

        /// <summary>A COARSE day's hours in the household's garden (C3.7a): as far as its season wants it by the step's last day. Returns the hours.</summary>
        double CoarseGarden(World w, in Settlement s, int hid, int days, long now, double hours)
        {
            if (hours <= 0 || !w.Households.Exists(hid)) return 0.0;
            ref var gHh = ref w.Households.Get(hid);
            int gDoy = SettlementSystem.DayOfYear(in s, now + (days - 1) * SimTime.Day);
            double gWant = Gardens.Behindhand(in gHh, gDoy, s.CropGrowth);
            if (gWant <= 0) return 0.0;
            double gH = DMath.Min(hours, gWant);
            double gFu = Gardens.Work(ref gHh, gDoy, s.CropGrowth, gH);
            w.Metrics.GardenHours += gH; w.Metrics.GardenFu += gFu;
            if (Gardens.Tally != null) Gardens.Count(in s, gDoy, gH, gFu);
            return gH;
        }

        /// <summary>A stint at the village's threshing floor (C3.7b): at their place in the ring round its middle, as long as the floor's want
        /// shared by a few takes (half an hour to three hours); a hungry hand an hour - what the household eats today and more.</summary>
        void BeginThresh(ref Person p, ref Settlement here, bool hungry, ref RandomSequence rs)
        {
            var w = _w;
            Threshing.Floor(w, here.Id, out double fx, out double fy);
            double a = Threshing.RingAngle(p.Id);
            double rate = Threshing.PerHour * (0.5 + 0.5 * p.Health);
            double h = hungry ? 1.0 : DMath.Clamp(Threshing.Want(w, in here, 1.0) / (4.0 * rate), 0.5, 2.0 + 1.0 * rs.NextUnit());
            here.ThreshOut = (float)(here.ThreshOut + h * rate);
            BeginLocal(ref p, ActivityKind.Thresh, hungry ? WhyCode.Hungry : WhyCode.Thresh, 0, fx + Threshing.Ring * DMath.Cos(a), fy + Threshing.Ring * DMath.Sin(a), SimTime.Hours(h));
        }

        /// <summary>FU a village's people are threshing now in stints under way at its floor (lived days; C3.7b) - counted again each day.</summary>
        public double ThreshingUnderWay(int sid)
        {
            var w = _w;
            var res = w.Residents(sid);
            long now = w.Now;
            double fu = 0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive || q.Act != ActivityKind.Thresh || q.T1 <= now) continue;
                fu += (q.T1 - Math.Max(now, Math.Max(q.T0, q.TA))) / (double)SimTime.Hour * Threshing.PerHour * (0.5 + 0.5 * q.Health);
            }
            return fu;
        }

        /// <summary>
        /// A COARSE day's (a DEEP step's) threshing (C3.7b): the village's sheaves threshed as far as its threshed grain is short of the step's need
        /// and its days ahead - by its grown and able people alike, a thresher's grain by their strength - charged before their other work as the
        /// households' chores are (the step's meals come out of it). Lived days thresh the same grain by the floor's urge.
        /// </summary>
        void CoarseThresh(int sid, int days)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            double want = Threshing.Want(w, in s, days);
            if (want <= 1e-9) return;
            long now = w.Now;
            var res = w.Residents(sid);
            _grown.Clear();
            double pace = 0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive || q.At != sid) continue;
                double age = q.AgeYears(now);
                if (age < 14 || q.Health < 0.3 || q.SickPhase == TC2.Core.Health.Disease.Infectious || q.Hurt >= TC2.Core.Society.Conflict.AbedHurt) continue;
                _grown.Add(res[i]);
                pace += (age > 60 ? 0.6 : 1.0) * (0.5 + 0.5 * q.Health);
            }
            if (pace <= 0) return;
            double each = DMath.Min(6.0 * days, want / (Threshing.PerHour * pace));   // (hours each; never more than a long day's at the floor)
            double fu = 0, hours = 0;
            for (int k = 0; k < _grown.Count; k++)
            {
                ref var q = ref w.People.Get(_grown[k]);
                Add(_grown[k], each);
                hours += each;
                fu += each * Threshing.PerHour * (q.AgeYears(now) > 60 ? 0.6 : 1.0) * (0.5 + 0.5 * q.Health);
            }
            Threshing.Thresh(w, ref s, fu, hours);
        }

        /// <summary>The household's best at dressing quern stones: it grinds on a rotary quern when one of its people can keep one (D2-062).</summary>
        double QuernBest(int hid) => HouseholdBest(hid, Knowledge.Tech.Quern);

        /// <summary>The household's best at a technique.</summary>
        double HouseholdBest(int hid, int t)
        {
            var w = _w;
            var mem = w.Members(hid);
            double best = 0;
            for (int i = 0; i < mem.Count; i++) { double k = Knowledge.Tech.Get(in w.People.Get(mem[i]), t); if (k > best) best = k; }
            return best;
        }

        readonly Dictionary<int, double> _chore = new Dictionary<int, double>();
        readonly List<int> _kids = new List<int>(), _grown = new List<int>();

        /// <summary>
        /// A COARSE day's household work (D2-056), as lived days share it: each household's food for the day made ready (the
        /// part its larder and the store can feed) and its water carried - a child of six to eleven carries a pot every other
        /// day, those of eight help at the quern; the grown members share the rest evenly. The hours are taken off their day before any other
        /// work. The cooking burns the village's wood.
        /// </summary>
        void Chores(int sid, int days = 1)
        {
            var w = _w;
            long now = w.Now;
            _chore.Clear();
            CoarseThresh(sid, days);   // (the village's sheaves threshed first, as far as the step's meals and its days ahead want: C3.7b)
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                ref var s = ref w.Settlements.Get(sid);
                var mem = w.Members(hid);
                double need = 0, litres = 0;
                _kids.Clear(); _grown.Clear();
                for (int k = 0; k < mem.Count; k++)
                {
                    ref var q = ref w.People.Get(mem[k]);
                    if (!q.Alive || q.At != sid) continue;
                    double age = q.AgeYears(now);
                    need += DailyNeed(in q, now);
                    litres += Hearth.WaterNeed(age);
                    if (q.Health < 0.3 || q.SickPhase == TC2.Core.Health.Disease.Infectious || q.Hurt >= TC2.Core.Society.Conflict.AbedHurt) continue;
                    if (age >= 6 && age < 12) _kids.Add(mem[k]); else if (age >= 12) _grown.Add(mem[k]);
                }
                if (need <= 0) continue;
                double stepNeed = need * days, stepLitres = litres * days;   // (DEEP: the step's meals and water)
                bool wood = s.Woodstore > 0.05;
                double quern = Knowledge.Tech.QuernFactor(QuernBest(hid));   // (a rotary quern in the household: D2-062)
                // food: what is not yet cooked of what they will eat today
                double raw = DMath.Max(0.0, DMath.Min(stepNeed, hh.Ready + hh.Larder + Threshing.Threshed(in s)) - hh.Ready);   // (out of the sheaf: C3.7b)
                double left = raw, hoursAll = 0;
                for (int k = 0; k < _kids.Count && left > 1e-9; k++)
                {
                    ref var q = ref w.People.Get(_kids[k]);
                    double age = q.AgeYears(now);
                    if (age < 8) continue;
                    double rate = Hearth.PrepPerHour(age, q.Health, wood) * quern;
                    double h = DMath.Min((_grown.Count > 0 ? 1.5 : 4.0) * days, left / rate);
                    Add(_kids[k], h); left -= h * rate; hoursAll += h;
                    w.Learning.Practice(ref q, Knowledge.Tech.Quern, h);
                    w.Learning.Practice(ref q, Knowledge.Tech.Pottery, h);
                }
                if (left > 1e-9 && _grown.Count > 0)
                {
                    double per = left / _grown.Count;
                    for (int k = 0; k < _grown.Count; k++)
                    {
                        ref var q = ref w.People.Get(_grown[k]);
                        double h = per / (Hearth.PrepPerHour(q.AgeYears(now), q.Health, wood) * quern);
                        Add(_grown[k], h); hoursAll += h;
                        w.Learning.Practice(ref q, Knowledge.Tech.Quern, h);
                        w.Learning.Practice(ref q, Knowledge.Tech.Pottery, h);
                    }
                    left = 0;
                }
                double made = raw - left;
                s.Woodstore = DMath.Max(0.0, s.Woodstore - made * Hearth.CookFuel * Pottery.CookFactor(w, hid));   // (in fired pots, its own or a kiln's: D2-063, C3.2)
                w.Metrics.PrepHours += hoursAll; w.Metrics.PrepFu += made;
                // water: the day's litres, a pot a trip
                double wleft = stepLitres, wh = 0;
                for (int k = 0; k < _kids.Count && wleft > 1e-9; k++)
                {
                    double trips = DMath.Min(0.5 * days, wleft / Hearth.CarryChild);   // a pot every other day: a lived day's grown members mostly see the need first
                    Add(_kids[k], trips * Hearth.TripHours); wleft -= trips * Hearth.CarryChild; wh += trips * Hearth.TripHours;
                    w.Metrics.WaterTrips += DMath.ToLong(-DMath.Floor(-trips));
                }
                if (wleft > 1e-9 && _grown.Count > 0)
                {
                    double trips = wleft / Hearth.CarryAdult, per = trips / _grown.Count;
                    for (int k = 0; k < _grown.Count; k++) Add(_grown[k], per * Hearth.TripHours);
                    wh += trips * Hearth.TripHours;
                    w.Metrics.WaterTrips += DMath.ToLong(-DMath.Floor(-trips));
                }
                w.Metrics.WaterHours += wh;
                hh.Water = (float)litres;   // the pots kept filled
            }
        }

        void Add(int pid, double h) => _chore[pid] = (_chore.TryGetValue(pid, out double had) ? had : 0.0) + h;

        public void CoarseDay(int sid, int days = 1)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            var res = w.Residents(sid);
            long now = w.Now;
            double cropNeed = Math.Max(0, s.CropWorkNeed - s.CropWork);
            double woodTarget = 6.0 * Math.Max(1, s.Population);
            // (DEEP: the step's first day's ration for all its work - the step's foraging, milk and meat hold the store as days lived
            // would; walking it down by the step's need alone marked whole steps short for a dip at their end, and sent people out for
            // need on 44 % of days against 33 % lived: F8 calibration)
            double ration = s.Ration;
            bool shortNow = WildNeed(in s, now, ration) > 0;
            // the village's herd goes out with its herders, one for every sixty head: its children of eight to fifteen by turns,
            // else its grown members by turns - a youth or a grown one gives a lived day's stint to it (D2-050)
            w.Profiler?.Begin(10018);
            int today = (int)SimTime.FloorDiv(now + w.LocalOffsetOf(sid), SimTime.Day);
            _tendBy.Clear();
            {
                ref var st = ref w.Settlements.Get(sid);
                int want = w.Stock.HerdersWanted(sid) - (st.Tended == today ? st.HerdersOut : 0);
                if (want > 0 && w.Stock.GroundOf(sid, today) >= 0)
                {
                    int went = Herders(sid, today, want, 8, 16, 0.3) ;
                    if (went < want) went += Herders(sid, today, want - went, 16, 60, 0.5);
                    if (went > 0)
                    {
                        if (st.Tended != today) { st.Tended = today; st.HerdersOut = 0; }
                        st.HerdersOut = (byte)Math.Min(255, st.HerdersOut + went);
                        w.Metrics.TendDays += went * days;
                    }
                }
            }
            {
                // a pressed village that knows no good place sends someone to look (D2-045): the same place a lived day's scout
                // would choose, seen in the same day (DEEP: those days lived would send in the step - one each six days - until
                // a good place is known)
                long first = now;
                int scouts = days == 1 ? (w.Migration.WantsScout(in s) ? 1 : 0) : w.Migration.ScoutsDue(in s, days, out first);
                for (int k = 0; k < scouts; k++)
                {
                    long at = first + k * 6 * SimTime.Day;
                    var srs = new RandomSequence(KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)at, (ulong)45));
                    int target = w.Migration.ScoutTarget(sid, ref srs);
                    ref var s1 = ref w.Settlements.Get(sid);
                    if (target >= 0) { w.Migration.Look(sid, target, 0); w.Metrics.Scouts++; } else s1.ScoutAt = now;
                    if (days == 1) continue;
                    s1.ScoutAt = at;
                    s1.Lore.Best(out float known);
                    if (known >= 0.6f) break;
                }
            }
            w.Profiler?.End(10018);
            var prof = w.Profiler;   // (tools: the parts of a COARSE day; null in the game)
            prof?.Begin(10013); Chores(sid, days); prof?.End(10013);
            // the hearths' wood comes first when it is running out: six days of fires kept in store (D2-056)
            double fuelWanted = DMath.Max(0.0, (5.0 + days) * s.FuelDay - s.Woodstore);   // (and the step's fires: DEEP)
            _seats.Clear(); _seatsOn = true; _drowned.Clear();   // (the boats' seats a day gives; the drowned die after the day: C9.1)
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                double age = p.AgeYears(now);
                // the day's food from the larder, then the granary (D2-042: one account a day, as the body keeps it)
                prof?.Begin(10019);
                if (days == 1) EatDay(ref p); else EatDays(ref p, days);
                prof?.End(10019);
                if (p.Carry > 0f) { w.Wild.Deposit(p.Id, p.Carry); p.Carry = 0f; }
                // children of 8-11 lend a hand at the making when the household's goods are wearing thin (D2-058)
                if (age >= 8 && age < 12 && p.Health >= 0.3) CoarseCraft(ref p, age, 0.6 * days, 0.85, days);
                if (age < 12 || p.Health < 0.3 || p.SickPhase == TC2.Core.Health.Disease.Infectious) continue;   // (the sick abed: D2-066)
                if (p.Hurt >= TC2.Core.Society.Conflict.AbedHurt) continue;                                        // (the badly hurt: D2-071)
                double tools = Tools(p.Household);
                // youths (12-15) work part of a day, as they take part in a lived day's chores
                double hours = 7.5 * (0.6 + 0.4 * p.Diligence / 255.0) * (age > 60 ? 0.5 : (age < 16 ? 0.6 : 1.0)) * (0.5 + 0.5 * p.Health) * days;   // (DEEP: the step's days)
                if (_tendBy.Contains(p.Id)) hours = DMath.Max(0.0, hours - TendStint * days);   // out with the herd (D2-050)
                if (p.Id == s.Watcher) hours = DMath.Max(0.0, hours - TC2.Core.Society.Conflict.WatchHours * days);   // a night at the store (D2-071)
                if (_chore.TryGetValue(p.Id, out double chore)) hours = DMath.Max(0.0, hours - chore);   // the household's own work (D2-056)
                double day0 = hours;
                if (Hours != null) { Hours[6] += day0; Hours[7] += days; }
                double smithExtra = 0;
                if (hours > 0 && Forge.Wanted(w, in s, in p, out int fid))
                {
                    // a smith with work at the village's smithy (C1.4): their labour day there, less the household's own work, before the
                    // village's shared field work - a lived smith works full days at the forge while it has work and the others farm
                    // (measured: a lived iron smith ~7.4 h a day, a COARSE one given 0.6 of the free hours 2.6: C5.2b)
                    double give = DMath.Max(hours, LabourDay * (age > 60 ? 0.85 : 1.0) * days - chore);
                    double used = Forge.Work(w, ref p, sid, fid, give, days);
                    if (used > hours) { smithExtra = used - hours; hours = 0; } else hours -= used;
                }
                if (hours > 0 && Weaving.Wanted(w, in s, in p, out int wid))
                {
                    // one of the village's best weavers with work at its weaver's shed (C3.1): their labour day there, less the household's own
                    // work, before the village's shared field work - as the smith's (D2-093)
                    double give = DMath.Max(hours, LabourDay * days - chore);
                    double used = Weaving.Work(w, ref p, sid, wid, give, days);
                    if (used > hours) { smithExtra += used - hours; hours = 0; } else hours -= used;
                }
                if (hours > 0 && Pottery.Wanted(w, in s, in p, out int kid))
                {
                    // one of the village's best potters with work at its kiln (C3.2): their labour day there, less the household's own work and the
                    // walk out to the clay, before the village's shared field work - as the smith's (D2-093)
                    double give = DMath.Max(hours, LabourDay * days - chore);
                    double used = Pottery.Work(w, ref p, sid, kid, give, days, true);
                    if (used > hours) { smithExtra += used - hours; hours = 0; } else hours -= used;
                }
                if (fuelWanted > 0 && hours > 0)
                {
                    // (C1.2: at the woodlot's distance, the walk out once a stint and every load home - felled from its woods)
                    double r0 = (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health) * tools, rate = Woods.HourRate(w, sid, r0, true, p.Household);
                    if (rate > 0)
                    {
                        double h = DMath.Min(hours, fuelWanted / rate);
                        double loads = Woods.Cut(w, sid, h, r0, true, p.Household);
                        fuelWanted -= loads; hours -= h; if (Hours != null) Hours[0] += h;
                        p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * h);
                    }
                }
                double cropH = 0;
                if (cropNeed > 0)
                {
                    double rate = (0.6 + p.SkFarm) * (0.5 + 0.5 * p.Health) * tools * Knowledge.Tech.ArdFactor(in p, w.Stock.HasOx(p.Household));
                    double h = Math.Min(hours, cropNeed / rate);
                    cropH = h;
                    s.CropWork += h * rate; cropNeed -= h * rate; hours -= h; if (Hours != null) Hours[1] += h;
                    TC2.Core.Diagnostics.HouseLedger.Work(p.Household, h * rate);
                    w.Learning.Practice(ref p, Knowledge.Tech.Ard, h);
                    p.SkFarm = (float)DMath.Min(1.0, p.SkFarm + 0.0015 * h);
                }
                bool muckLater = false;
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && s.Muck > 1.0 && Muck.Window(in s))
                {
                    // the yards' muck out on the fields before the ploughing (C3.7c): after the fields' own work, at most half the free hours (as a lived
                    // day's urge gives it beside the rest), at the household's cart's pace or a back's - and after the household's own roof where that
                    // pulls harder, as a lived day's urges order them (D2-158: a camp's always, a hut's house unless the heaps are high - as the garden's,
                    // D2-146; carted first, a household in a camp still had no house after sixty days of free hours)
                    if (!CoarseMuckOld && RoofBeforeMuck(w, in s, in p)) muckLater = true;
                    else
                    {
                        double used = Muck.Cart(w, ref s, hours * 0.5, Muck.HoursPerTonne(w, p.Household), 0.5 + 0.5 * p.Health);
                        hours -= used;
                    }
                }
                int lslot = -1;
                if (LandHours != null) { lslot = LandSlot(w, in s); LandHours[lslot] += days; LandHours[lslot + 1] += cropH; LandHours[lslot + 2] += hours; }
                // the household's garden (C3.7a): its own ground after the village's fields, as far as its season wants it by the step's last day - in the
                // order a lived day's urges give it (D2-146): a hungry day forages first where the wild pulls harder, and a household building its own roof
                // builds first where that pulls harder (a camp's always, a hut's house unless the garden is far behind)
                bool gardenLater = false;
                if (hours > 0 && !Gardens.Off && GardenOrder(w, in s, in p, days, now, shortNow, ration, CoarseGardenOld, out gardenLater)) hours -= CoarseGarden(w, in s, p.Household, days, now, hours);
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && Carts.HasWork(w, p.Household) && !(shortNow && WildNeed(in s, now, ration) > Carts.ShortBound))
                {
                    // the household is making a cart (C4.2): a couple of its grown members' hours a day go to it before the village's building - their own
                    // small job, as a lived day takes it up beside the village's work (given only what building left, half the carts begun were never made)
                    double used = Carts.Work(w, p.Household, DMath.Min(hours, Carts.HoursADay * days), BuildRate(in p) * tools, p.Id);
                    hours -= used;
                }
                // building, as a lived day builds: in every free hour it beats idling for, unless hunger sends them out first (D2-045)
                bool ownRoof = false, noWood = false;
                bool build = hours > 0 && Construction.HasWork(w, in s, p.Household, out ownRoof, out noWood);
                double buildUrge = 0;
                if (build)
                {
                    byte shelter = w.Households.Exists(p.Household) ? w.Households.Get(p.Household).Shelter : Construction.House;
                    buildUrge = ownRoof ? (shelter == Construction.Camp ? 0.85 : shelter == Construction.Hut ? 0.4 : 0.3) : 0.45;   // (a house rebuilt waits on the rest: D2-114)
                    if (!(shortNow && WildNeed(in s, now, ration) > buildUrge))
                    {
                        double used = Construction.Work(w, sid, p.Household, DMath.Min(hours, 7.0 * days), BuildRate(in p) * tools, p.Id);
                        hours -= used; if (Hours != null) Hours[2] += used;
                        build = false;
                    }
                }
                if (gardenLater && hours > 0) hours -= CoarseGarden(w, in s, p.Household, days, now, hours);
                if (muckLater && hours > 0) hours -= Muck.Cart(w, ref s, hours * 0.5, Muck.HoursPerTonne(w, p.Household), 0.5 + 0.5 * p.Health);   // (after the roof: D2-158)
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && Boats.HasWork(w, in s, p.Household) && !(shortNow && WildNeed(in s, now, ration) > 0.42))
                {
                    // the household is making a boat (C9.1): its grown members' free hours go to it, as a lived day's would
                    double used = Boats.Work(w, sid, p.Household, DMath.Min(hours, 7.0 * days), BuildRate(in p) * tools, p.Id);
                    hours -= used;
                }
                prof?.Begin(10022);
                if (hours > 0)
                {
                    // the wild: the same choice and the same catch from the same fields as a day lived step by step (D2-042)
                    int wk = WildChoice(in p, in s, age, now, out double expect, out int boat);
                    if (wk >= 0)
                    {
                        double u = WildUrge(in p, in s, wk, expect, now, ration) * (age > 60 ? 0.5 : 1.0) * (0.6 + 0.4 * p.Diligence / 255.0);
                        // a lived day offers the wild every free daytime slot it beats idling at (idle 0.08-0.13 against the
                        // wild's urge + 0.06 of chance): the same odds, as a share of the free hours
                        // a pressing need keeps people out from dawn to mid-afternoon, beyond an ordinary day's work (two trips) - each
                        // day of the step (DEEP: allowed once a step, a short step foraged a day's need-hours less a day, stayed short
                        // longer and sent people out for need on 40 % of days against 33 % lived: F8 calibration)
                        double free = shortNow ? DMath.Max(hours, WildNeedDay * days * (0.5 + 0.5 * p.Health) * (age > 60 ? 0.6 : (age < 16 ? 0.7 : 1.0)) - (day0 - hours)) : hours;
                        // (in ordinary times a lived day spends ~0.9 of the free hours this mapping gives: building and company take
                        // free slots too; the household's meals and water are charged first since D2-056, when it was 0.6 -
                        // measured, CoarseAndActivityLiveOffTheWildAlike)
                        double hw = u > 0.005 ? DMath.Min(free, WildNeedDay * days) * DMath.Clamp01((u + 0.01) / 0.1) * (shortNow ? 1.0 : 0.9) : 0.0;
                        if (Hours != null) { Hours[8] += hw; Hours[9] += days; Hours[10] += u * days; if (hw < 0.25 * days) Hours[11] += hw; Hours[12] += free; Hours[13 + wk] += days; int cyc = (int)(SimTime.FloorDiv(now, SimTime.Day) % 12); Hours[16 + cyc] += days; if (wk == WildFood.Hunting) Hours[28 + cyc] += days; Hours[40] += DMath.Max(0.0, 1.0 - ration) * days; if (s.StoreSeen(now) < 1.0) Hours[41] += days; if (shortNow) Hours[42] += days; }
                        if (hw >= 0.25 * days)   // (DEEP: a quarter-hour a day, as a day's least trip - not a quarter-hour a step)
                        {
                            var rs = NextRandom(ref p);
                            var g = wk == WildFood.Hunting ? s.Hunt : wk == WildFood.Fishing ? (boat != 0 ? s.Sea : s.Fish) : s.Gath;   // (by boat: the sea's grounds, C9.1)
                            int ground = WildFood.PickGround(in g, ref rs);
                            if (ground >= 0)
                            {
                                Stone.Saw(w, sid, p.Id, ground);   // (what rock shows where they go: C1.3)
                                ulong key = KeyedRng.Hash(w.Seed, RngStream.Work, (ulong)p.Id, (ulong)now);
                                double fu;
                                if (wk == WildFood.Hunting)
                                {
                                    int herd = w.HerdSys.BestGame(sid);   // (where a herd is, as a lived day's hunter goes)
                                    if (herd != 0 && rs.Chance(0.7)) ground = w.Herds.Get(herd).Cell;
                                    fu = w.Wild.Hunt(ground, hw * tools, p.SkHunt, key, out _, out double hides, days);
                                    s.Hides += hides;   // (D2-063) p.SkHunt = (float)DMath.Min(1.0, p.SkHunt + 0.002 * hw); w.Metrics.HuntTrips++;
                                    if (fu > 0) w.HerdSys.MarkHunted(sid, ground);
                                }
                                else if (wk == WildFood.Fishing && boat != 0)
                                {
                                    // out in the household's boat (C9.1): a seat of it today, the row out paid from the hours, the open sea's shoals, and
                                    // the trip's chance under the wind it is out in (the drowned die after the day)
                                    int taken = _seats.TryGetValue(boat, out int tk) ? tk : 0;
                                    _seats[boat] = taken + 1;
                                    if (taken == 0) w.Metrics.BoatOutings++; else w.Metrics.CrewSeats++;   // (the day's trip and its crew: C9.2)
                                    int outcome = Boats.Trip(w, sid, p.Household, boat, p.Id, Boats.WindAt(w, w.Planet.Grid.Center[ground]), out long lostEv, days, false);
                                    bool plank = Boats.IsPlank(w.Buildings.Get(boat).Kind);
                                    fu = outcome == 0 ? w.Wild.Fish(ground, hw * Boats.AfterRow(w, in s, plank) * tools * Knowledge.Tech.NetsFactor(in p), p.SkFish, key, days, true, plank) : 0.0;
                                    if (outcome == 2) { _drowned.Add(p.Id); _drownedEv.Add(lostEv); }
                                    p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.002 * hw); w.Metrics.FishTrips++; w.Metrics.BoatTrips++;
                                    w.Learning.Practice(ref p, Knowledge.Tech.Nets, hw);
                                }
                                else if (wk == WildFood.Fishing)
                                {
                                    fu = w.Wild.Fish(ground, hw * tools * Knowledge.Tech.NetsFactor(in p), p.SkFish, key, days); p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.002 * hw); w.Metrics.FishTrips++;
                                    w.Learning.Practice(ref p, Knowledge.Tech.Nets, hw);
                                }
                                else { fu = w.Wild.Gather(ground, hw * tools, p.Health, key, days); w.Metrics.GatherTrips++; }
                                w.Wild.Deposit(p.Id, fu);
                                if (days > 1) w.Wild.Retake(sid, wk, boat != 0);   // (DEEP: the next chooses on the ground this step's take left)
                                w.Metrics.WildHours += hw; w.Metrics.WildFu += fu;
                                hours = DMath.Max(0.0, hours - hw);
                                if (lslot >= 0) { LandHours[lslot + 3] += hw; LandHours[lslot + 4] += fu; }
                                if (shortNow) w.SettlementSys.NoteWildFood(sid);
                            }
                        }
                    }
                }
                prof?.End(10022);
                if (build && hours > 0) { double used2 = Construction.Work(w, sid, p.Household, DMath.Min(hours, 7.0 * days), BuildRate(in p) * tools, p.Id); hours -= used2; if (Hours != null) Hours[4] += used2; }
                bool sworn = TC2.Core.Society.Retinue.SwornHere(w, in p);
                int gid = 0;
                if (hours > 0 && Training.Willing(in p, now) && (sworn ? (gid = Training.Ground(w, sid)) != 0 : Training.Practising(w, in s, out gid)))
                {
                    // the bold practise at the village's training ground on their practice days, as a lived afternoon (C1.5); a man sworn to the
                    // village's first household every day (C10.7b)
                    int n = sworn ? days : Training.Days(p.Id, SimTime.FloorDiv(now + w.LocalOffsetOf(sid), SimTime.Day), days);
                    double h = DMath.Min(hours, Training.Session * n);
                    if (h > 0) { Training.Practise(w, ref p, sid, gid, h); hours -= h; if (sworn && TC2.Core.Society.Retinue.Tally != null) TC2.Core.Society.Retinue.Tally[7] += h; }
                }
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && Stone.Wanted(w, in s, out int qid))
                {
                    // the village wants stone and its quarry stands (C1.3): cut and carry it in the free hours, up to what it wants
                    double r0 = Stone.CutRate(in p, tools), rate = Stone.HourRate(w, sid, qid, r0, true, p.Household);
                    if (rate > 0)
                    {
                        double h = DMath.Min(hours * 0.5, (s.StoneWant - s.Stone) / rate);
                        Stone.Cut(w, sid, qid, h, r0, true, p.Household);
                        hours -= h;
                    }
                }
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && Mining.Wanted(w, in s, out int mid))
                {
                    // the village's smiths want ore and its mine stands (C5.1): dig and carry it in the free hours, up to what they want
                    double r0 = Mining.DigRate(in p, tools), rate = Mining.HourRate(w, sid, r0, true);
                    if (rate > 0)
                    {
                        double h = DMath.Min(hours * 0.5, (s.OreWant - s.Ore) / rate);
                        Mining.Dig(w, sid, mid, h, r0, true, double.MaxValue, Mining.Copper, p.Id);
                        hours -= h;
                    }
                }
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && Mining.Wanted(w, in s, out int bid2, Mining.Iron))
                {
                    // the village's smiths want bog ore and its pits stand (C5.2b): dig and carry it in the free hours, up to what they want
                    double r0 = Mining.DigRate(in p, tools), rate = Mining.HourRate(w, sid, r0, true, Mining.Iron);
                    if (rate > 0)
                    {
                        double h = DMath.Min(hours * 0.5, (s.FeOreWant - s.FeOre) / rate);
                        Mining.Dig(w, sid, bid2, h, r0, true, double.MaxValue, Mining.Iron, p.Id);
                        hours -= h;
                    }
                }
                if (hours > 0 && age >= 16 && age < 60 && p.Health > 0.6 && Charcoal.Wanted(w, in s, out int chid2))
                {
                    // the village's smiths want charcoal and its hearth stands (C5.2a): fell and burn in the free hours, up to what they want
                    double r0 = (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health) * tools, rate = Charcoal.HourRate(w, sid, r0, true);
                    if (rate > 0)
                    {
                        double h = DMath.Min(hours * 0.5, (s.CharcoalWant - s.Charcoal) / rate);
                        Charcoal.Burn(w, sid, chid2, h, r0, true);
                        p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * h);
                        hours -= h;
                    }
                }
                if (hours > 0 && s.Woodstore < woodTarget && Woods.Lot(in s) >= 0)
                {
                    double h = hours * 0.4;
                    Woods.Cut(w, sid, h, (0.6 + p.SkWood) * (0.5 + 0.5 * p.Health) * tools, true, p.Household);   // (C1.2)
                    p.SkWood = (float)DMath.Min(1.0, p.SkWood + 0.0015 * h);
                    hours -= h; if (Hours != null) Hours[5] += h;
                }
                // making and mending in the rest of a day's labour - the day's slack and the evening by the fire - as a lived day
                // fills it (D2-058)
                double worked = chore + (_tendBy.Contains(p.Id) ? TendStint * days : 0.0) + (day0 - hours) + smithExtra;
                prof?.Begin(10020); CoarseCraft(ref p, age, DMath.Max(0.0, LabourDay * (age > 60 ? 0.85 : 1.0) * days - worked), 1.0, days); prof?.End(10020);
            }
            _seatsOn = false;
            for (int k = 0; k < _drownedEv.Count; k++)
            {
                // those drowned when their boats overturned today (C9.1: not while the day's list of its people is walked)
                ref var q = ref w.People.Get(_drowned[k]);
                if (q.Alive) Kill(ref q, _drownedEv[k], 12);
            }
            _drowned.Clear(); _drownedEv.Clear();
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

        // ---------------------------------------------------------------- voyages (C9.3)
        /// <summary>How long a boat's crew is given to gather at the landing before it puts out.</summary>
        public const long VoyageGather = 20 * SimTime.Minute;
        /// <summary>How long a crew stays at the other landing's village before they row home (a COARSE voyage's stay is VisitHoursCoarse).</summary>
        public const long VoyageStay = 3 * SimTime.Hour;

        /// <summary>The time a boat takes over a way by sea: rowed at a walker's pace times the boat's (a plank boat half as fast again).</summary>
        long VoyageLeg(Route way, int bid)
        {
            byte kind = _w.Buildings.Exists(bid) ? _w.Buildings.Get(bid).Kind : Structures.PlankBoat;
            return Math.Max(60L, DMath.ToLong(DMath.RoundHalfUp(way.Length / (_w.Config.WalkSpeed * Boats.RowPace(kind)))));
        }

        /// <summary>
        /// Lived (C9.3): the village booked the household's boat for a voyage - this one takes it out. The weather over the way's middle
        /// must serve (a calm sea for grain; a trade's load in any wind the boat would fish in: else they wait, the booking lapsing at the
        /// village's next day); two of the household at least who can take an oar (all who can, a seat each); the load aboard; the sea's
        /// chance as they put out (as a COARSE voyage has it); then all walk down to the landing and put out together.
        /// </summary>
        bool StartVoyage(ref Person p, int bi)
        {
            var w = _w;
            long now = w.Now;
            if (bi < 0 || bi >= w.Trade.Bookings) return false;
            var bk = w.Trade.BookingAt(bi);
            int sid = p.Home, bid = bk.Boat, hid = p.Household;
            if (bk.From != sid || !w.Buildings.Exists(bid) || !w.Settlements.Exists(bk.To) || !w.Settlements.Get(bk.To).Inhabited) { w.Trade.Unbook(bi); return false; }
            ref var b = ref w.Buildings.Get(bid);
            byte kind = b.Kind;
            if (!Boats.Sound(in b, now) || b.Household != hid || Boats.Aboard(w, hid, bid) > 0) { w.Trade.Unbook(bi); return false; }
            var way = w.SeaRoutes.Get(w, sid, bk.To);
            if (way == null) { w.Trade.Unbook(bi); return false; }
            double wind = Boats.WindAt(w, way.PointAt(0.5 * way.Length));
            if (bk.Kind == Trade.ForTrade ? wind >= Boats.MaxOf(kind) : wind > Boats.Calm) return false;   // (they wait for the weather)
            // the crew: the household's who can take an oar, a seat each, the one who takes it out first
            var crew = _voyCrew; crew.Clear(); crew.Add(p.Id);
            var mem = w.Members(hid);
            for (int i = 0; i < mem.Count && crew.Count < Boats.SeatsOf(kind); i++)
            {
                if (mem[i] == p.Id) continue;
                ref var q = ref w.People.Get(mem[i]);
                if (!Trade.CanSail(in q, sid, now) || q.Act == ActivityKind.Sleep || q.Act == ActivityKind.Ill) continue;
                crew.Add(mem[i]);
            }
            if (crew.Count < 2) return false;   // (a boat's load wants two at the oars at least: they wait for hands)
            w.Trade.Unbook(bi);
            double kg = w.Trade.LoadAboard(ref p, in bk);
            if (kg <= 0) return false;   // (nothing to carry now: the deal is gone, the grain eaten)
            // the sea's chance as they put out: overturned, the boat is lost with its load and all of them are in the water
            if (Boats.Trip(w, sid, hid, bid, p.Id, wind, out _, 1, true) != 0)
            {
                for (int i = 1; i < crew.Count; i++) Boats.Overturned(w, sid, hid, bid, crew[i], wind, out _, true);
                if (p.Alive) { p.Cargo = Trade.None; p.CargoQty = 0f; p.CargoTo = Trade.ToStore; }
                return false;
            }
            // down to the landing together, then out
            ref var s = ref w.Settlements.Get(sid);
            long off = now + VoyageGather;
            for (int i = 0; i < crew.Count; i++)
            {
                ref var q = ref w.People.Get(crew[i]);
                if (i > 0 && (q.Step == StepKind.Local || q.Step == StepKind.Stay)) CutStep(ref q);   // (the others leave what they were doing)
                q.Voyage = bid; q.Dest = bk.To; q.Until = off;
                BeginLocal(ref q, ActivityKind.Voyage, WhyCode.Voyage, bid, s.LandX + 1.5 * (i - 1), s.LandY, 0, off);
            }
            w.Metrics.Voyages++;
            if (bk.Kind == Trade.ForTrade) w.Metrics.VoyageSeats += crew.Count;   // (the seats of trade voyages, as COARSE counts them)
            if (bk.Kind != Trade.ForFetch && bk.Kind != Trade.ForTenth) w.Metrics.SeaCargoKg += kg;   // (going for grain: nothing aboard yet)
            if (!Boats.IsPlank(kind) && Boats.CrossTally != null) Boats.CrossTally[3]++;   // (tools: a dugout's crossing, lived - C9.5b)
            return true;
        }

        readonly List<int> _voyCrew = new List<int>();

        /// <summary>
        /// A voyage's next step (C9.3), true when it took the decision: gathered at the landing at home, out over the sea to the other landing;
        /// at the other village, down to its landing when the stay is nearly over, then home over the sea - the crew together (their times are
        /// the boat's, not each one's). A boat lost or a way gone ends the voyage: home by the road, or home.
        /// </summary>
        bool VoyageStep(ref Person p)
        {
            var w = _w;
            long now = w.Now;
            int bid = p.Voyage;
            bool boat = w.Buildings.Exists(bid) && w.Buildings.Get(bid).Cond > 0f && w.Buildings.Get(bid).Left == 0;
            if (p.At == p.Home)
            {
                if (p.Act != ActivityKind.Voyage || p.Step != StepKind.Local || p.Dest == 0 || !boat) { EndVoyage(ref p); return false; }
                var way = w.SeaRoutes.Get(w, p.Home, p.Dest);
                if (way == null) { EndVoyage(ref p); return false; }
                PutOut(ref p, way, p.Home, p.Dest, Math.Max(p.Until, now) + VoyageLeg(way, bid));
                return true;
            }
            // at the other village
            if (!boat || !w.Settlements.Exists(p.At)) { EndVoyage(ref p); return false; }
            if (now < p.Until - 15 * SimTime.Minute) return false;   // (their stay: as any visitor)
            ref var here = ref w.Settlements.Get(p.At);
            if (now < p.Until)
            {
                // down to the boat at its landing
                double lx = here.LandAt != 0 ? here.LandX : here.SquareX, ly = here.LandAt != 0 ? here.LandY : here.SquareY;
                BeginLocal(ref p, ActivityKind.Visit, WhyCode.Voyage, 0, lx, ly, 0, p.Until);
                return true;
            }
            var back = w.SeaRoutes.Get(w, p.At, p.Home);
            if (back == null) { EndVoyage(ref p); return false; }
            PutOut(ref p, back, p.At, p.Home, Math.Max(p.Until, now) + VoyageLeg(back, bid));
            return true;
        }

        /// <summary>
        /// The boat puts out from a landing (C9.3): the one whose step brought it and every other of its crew there go aboard together -
        /// whatever they are doing - and row the way with the one time of arrival (a crew is one boat, never people on their own times).
        /// </summary>
        void PutOut(ref Person p, Route way, int from, int to, long arrive)
        {
            var w = _w;
            int bid = p.Voyage;
            double sFrom = from == way.A ? 0.0 : way.Length, sTo = to == way.A ? 0.0 : way.Length;
            var mem = w.Members(p.Household);
            for (int i = 0; i < mem.Count; i++)
            {
                if (mem[i] == p.Id) continue;
                ref var q = ref w.People.Get(mem[i]);
                if (!q.Alive || q.Voyage != bid || q.At != from || q.Step == StepKind.Travel) continue;
                if (q.Step == StepKind.Local || q.Step == StepKind.Stay) CutStep(ref q);
                if (from != q.Home) w.Visitors(from).Remove(q.Id);
                BeginTravel(ref q, ActivityKind.Voyage, WhyCode.Voyage, way, sFrom, sTo, to, arrive);
                q.WhyArg = bid;
            }
            if (from != p.Home) w.Visitors(from).Remove(p.Id);
            BeginTravel(ref p, ActivityKind.Voyage, WhyCode.Voyage, way, sFrom, sTo, to, arrive);
            p.WhyArg = bid;
        }

        /// <summary>A voyage over or given up: whatever they still carry is home (or goes home with them by the road).</summary>
        void EndVoyage(ref Person p)
        {
            var w = _w;
            if (p.At == p.Home && (p.CargoQty > 0f || p.CargoTo != Trade.ToStore) && w.Settlements.Exists(p.Home)) w.Trade.Unload(ref p, p.Home);
            p.Voyage = 0;
            if (p.At != p.Home && p.At != 0 && w.Routes.Get(w, p.At, p.Home) == null)
            {
                // (no road home from there: they come home with the next boat - the island's people do not keep them)
                w.Visitors(p.At).Remove(p.Id);
                p.At = p.Home;
                if ((p.CargoQty > 0f || p.CargoTo != Trade.ToStore) && w.Settlements.Exists(p.Home)) w.Trade.Unload(ref p, p.Home);
                if (w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); p.FromX = hh.HomeX; p.FromY = hh.HomeY; p.ToX = p.FromX; p.ToY = p.FromY; }
            }
        }

        /// <summary>
        /// A voyage's crew at a landing (C9.3): home - the load into the store, the voyage over; at the other village - visitors there for the
        /// stay (what they may catch, the news), the one with the load dealing it: a trade for the return load, grain given to a famine,
        /// grain given back, or grain fetched back aboard.
        /// </summary>
        void VoyageLanded(ref Person p, Route r)
        {
            var w = _w;
            long now = w.Now;
            int arrived = r == null ? p.Home : (r.Length <= 1.0 && p.Dest != 0 && (p.Dest == r.A || p.Dest == r.B) ? p.Dest : (p.S1 <= 0.5f ? r.A : r.B));   // (a way of no length: where they were going - D2-137)
            if (!w.Settlements.Exists(arrived)) arrived = p.Home;
            ref var s = ref w.Settlements.Get(arrived);
            double lx = s.LandAt != 0 ? s.LandX : s.SquareX, ly = s.LandAt != 0 ? s.LandY : s.SquareY;
            p.At = arrived;
            p.Step = StepKind.Local; p.Act = ActivityKind.Visit; p.FromX = (float)lx; p.FromY = (float)ly; p.ToX = p.FromX; p.ToY = p.FromY; p.TA = now; p.T0 = now;
            p.Dest = 0;
            if (arrived == p.Home)
            {
                if (p.CargoQty > 0f || p.CargoTo != Trade.ToStore) w.Trade.Unload(ref p, arrived);   // (the load home: C9.3)
                p.Voyage = 0;
                Decide(ref p);
                return;
            }
            w.Visitors(arrived).InsertSorted(p.Id);
            p.Until = now + VoyageStay;
            p.Visits++;
            w.Disease.Arrive(ref p, arrived, VoyageStay);   // what they may catch there and carry home (D2-066)
            if (p.CargoQty > 0f || p.CargoTo != Trade.ToStore) w.Trade.Deliver(ref p, arrived);
            w.DeedBook.Meet(p.Home, arrived);              // the news each village tells (D2-072)
            Decide(ref p);
        }

        /// <summary>A trader's chance a morning decision to go, given a deal (fitted to Trade.TripsPerAdultDay at COARSE: D2-067).</summary>
        public const double TradeChance = 0.004;

        /// <summary>A trader sets out with the village's load for a neighbour (D2-067). False if there is no load or no road.</summary>
        bool StartTrade(ref Person p, int to)
        {
            var w = _w;
            if (to == 0 || p.At != p.Home) return false;
            var r = w.Routes.Get(w, p.At, to);
            if (r == null) { if (Trade.AcrossTally != null) Trade.AcrossTally[6] += 1; return false; }   // (tools: a partner across water, C9.5 survey)
            w.Trade.Load(ref p, p.Home, to);
            if (p.CargoQty <= 0f) return false;
            double from = p.At == r.A ? 0.0 : r.Length, dest = to == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, ActivityKind.TripOut, WhyCode.Trade, r, from, dest, to);
            return true;
        }

        /// <summary>A carrier sets out with a load of the lord's tenth for the lord's store (C10.6). False if none waits or there is no road.</summary>
        bool StartCarry(ref Person p, int lord)
        {
            var w = _w;
            if (lord == 0 || p.At != p.Home) return false;
            var r = w.Routes.Get(w, p.At, lord);
            if (r == null) return false;
            if (w.Conflict.TakeLoad(ref p, lord) <= 0) return false;
            double from = p.At == r.A ? 0.0 : r.Length, dest = lord == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, ActivityKind.TripOut, WhyCode.Tribute, r, from, dest, lord);
            return true;
        }

        /// <summary>A hungry person sets out in the dark for a neighbour's store (D2-069). False if there is no road.</summary>
        bool StartTake(ref Person p, int to)
        {
            var w = _w;
            if (to == 0 || p.At != p.Home) return false;
            var r = w.Routes.Get(w, p.At, to);
            if (r == null) return false;
            double from = p.At == r.A ? 0.0 : r.Length, dest = to == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, ActivityKind.TripOut, WhyCode.Take, r, from, dest, to);
            return true;
        }

        /// <summary>A claimant sets out for the doers' village to ask amends (D2-072). False if there is no road.</summary>
        bool StartErrand(ref Person p)
        {
            var w = _w;
            if (p.Errand == 0 || !w.Deeds.Exists(p.Errand) || p.At != p.Home) return false;
            int to = w.Deeds.Get(p.Errand).ByS;
            if (!w.Settlements.Exists(to) || !w.Settlements.Get(to).Inhabited) { p.Errand = 0; return false; }
            var r = w.Routes.Get(w, p.At, to);
            if (r == null) { p.Errand = 0; return false; }
            double from = p.At == r.A ? 0.0 : r.Length, dest = to == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, ActivityKind.TripOut, WhyCode.Claim, r, from, dest, to);
            return true;
        }

        /// <summary>A taker leaves the village they took from (or were seen at) for home at once.</summary>
        bool TakerHome(ref Person p)
        {
            var w = _w;
            var r = w.Routes.Get(w, p.At, p.Home);
            if (r == null) return false;
            double from = p.At == r.A ? 0.0 : r.Length, to = p.Home == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, ActivityKind.TripHome, p.Why == WhyCode.Raid ? WhyCode.Raid : WhyCode.Take, r, from, to, p.Home);
            return true;
        }

        /// <summary>A band's member sets out at their bedtime, the band to come to the village together (D2-073).</summary>
        bool StartRaid(ref Person p)
        {
            var w = _w;
            if (p.Band == 0 || !w.Bands.Exists(p.Band) || p.At != p.Home) return false;
            ref var b = ref w.Bands.Get(p.Band);
            var r = w.Routes.Get(w, p.At, b.Target);
            if (r == null) return false;
            double from = p.At == r.A ? 0.0 : r.Length, dest = b.Target == r.A ? 0.0 : r.Length;
            BeginTravel(ref p, ActivityKind.TripOut, WhyCode.Raid, r, from, dest, b.Target, b.Arrive);
            return true;
        }

        /// <summary>Premise (tests, tools): a person sets out now for a village's store in the dark, as if hunger had sent them.</summary>
        public bool SendTaker(int pid, int to)
        {
            ref var p = ref _w.People.Get(pid);
            if (!p.Alive || p.At != p.Home) return false;
            CutStep(ref p);
            return StartTake(ref p, to);
        }

        /// <summary>Premise (tests, tools): a person sets out now to trade with a village, as if they had chosen to.</summary>
        public bool SendTrader(int pid, int to)
        {
            ref var p = ref _w.People.Get(pid);
            if (!p.Alive || p.At != p.Home) return false;
            CutStep(ref p);
            return StartTrade(ref p, to);
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
            Burials.Bury(w, in p, why);   // (in the village they lived or died in - none for the drowned, the wave's or the lava's dead: D2-151)
            if (p.At != 0 && p.At != p.Home) w.Visitors(p.At).Remove(p.Id);
            w.Residents(p.Home).Remove(p.Id);
            if (w.Households.Exists(p.Household)) w.Members(p.Household).Remove(p.Id);
            if (p.Partner != 0 && w.People.Exists(p.Partner)) w.People.Get(p.Partner).Partner = 0;
            w.Settlements.Get(p.Home).Population--;
            w.Metrics.Deaths++;
            // the last of their household: it dies out, its home stands empty, its kin inherit (D2-059)
            if (w.Households.Exists(p.Household) && w.Members(p.Household).Count == 0) w.SettlementSys.DieOut(p.Household, p.Id);
        }
    }
}
