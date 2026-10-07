using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;
using Resolution = TC2.Core.Simulation.Resolution;

namespace TC2.Core.Places
{
    /// <summary>
    /// A settlement's day (07 §7, 08 §2), once at local midnight: the crop calendar and harvest, food from
    /// the granary to the larders, hunger, deaths, marriages and new households, conceptions; births are
    /// scheduled events of their own. Iteration is always in ascending id order.
    /// </summary>
    public sealed class SettlementSystem
    {
        World _w;
        readonly List<int> _scratch = new List<int>();
        readonly List<int> _men = new List<int>();

        public void Bind(World w) { _w = w; }

        public void ScheduleFirstDaily(int sid)
        {
            ref var s = ref _w.Settlements.Get(sid);
            long off = Planet.PlanetState.LocalOffset(s.Anchor);
            // next local midnight
            long t = _w.Now;
            long localNow = t + off;
            long nextLocalMidnight = SimTime.StartOfDay(localNow) + SimTime.Day;
            _w.Queue.Push(nextLocalMidnight - off, Phase.Settlement, EventKind.SettlementDaily, sid);
        }

        public void Daily(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            long now = w.Now;
            int doy = SimTime.DayOfYear(now + Planet.PlanetState.LocalOffset(s.Anchor));
            if (s.Anchor.Y < 0) doy = (doy + 24) % SimTime.DaysPerYear; // southern seasons
            // DEEP (04 §4.3b, F8): a calm village lives several days at a step - never across a day of its calendar (sowing, the crop
            // growing, ripe, the harvest, the day households weigh leaving), so a step's work belongs to one stage
            int days = 1;
            if (w.Resolution == Resolution.Coarse && w.StepDays > 1 && Calm(sid))
            {
                days = DaysToCalendar(doy, w.StepDays);
                // nor across the plants' next step: what its herds root and graze belongs to the window they take it from (a step that
                // spanned one charged its whole rooting before the pulse, which forgot it - the woods looked full all the next window
                // and fewer went hunting: F8 calibration)
                days = Math.Min(days, Math.Max(1, DaysUntil(w.NextPlantTick, now)));
            }
            if (s.AheadTo != 0 && s.AheadTo <= now) { s.Ahead = 0; s.AheadFrom = 0; s.AheadTo = 0; }   // (the last step's days are lived)
            w.Metrics.VillageSteps++; w.Metrics.VillageDays += days;
            var prof = w.Profiler;   // (tools: the parts of a village's day; null in the game)
            prof?.Begin(10000); Crops(ref s, doy, 1); prof?.End(10000);   // (the step's first day: a stage begins on it, before its work)
            double g0 = s.Granary;   // (DEEP: the store before the step's meals, larders and catch; the harvest is the day's own)
            prof?.Begin(10001); Food(ref s, doy, days); prof?.End(10001);
            { ref var sw = ref w.Settlements.Get(sid); sw.StoneWant = (float)Stone.Want(w, sid); sw.OreWant = (float)Mining.Want(w, sid); sw.CharcoalWant = (float)Charcoal.Want(w, sid); }   // (stone for its next houses' footings: C1.3; ore for its smiths: C5.1)
            Structures.Plan(w, sid);   // what its people want built next, by need (C1.1)
            if (days > 1) Ahead(sid, g0, days);   // (the step's own work reads its store as days lived hold it too: F8)
            prof?.Begin(10002); prof?.Begin(10007); w.Wild.Survey(sid); prof?.End(10007); prof?.Begin(10008); w.HerdSys.Update(sid); prof?.End(10008); prof?.End(10002);   // the grounds and the herds round it today (D2-042, D2-047)
            if (days > 1) w.Wild.MidStep(sid, w.Stock.MidStepMast(sid, days));   // (DEEP: the woods as the step's middle day finds them)
            if (doy == Migration.DecideDay) { prof?.Begin(10003); w.Migration.Weigh(sid); prof?.End(10003); }   // who leaves (D2-045)
            if (w.Resolution == Resolution.Coarse) { prof?.Begin(10004); w.Agents.CoarseDay(sid, days); prof?.End(10004); }
            // DEEP: the step's later days grow the crop after its work, as days lived grow each day on the work done before it (grown
            // before the step's weeding, a growing step's days were three-quarters tended - harvests fell and households left: F8)
            if (days > 1) { prof?.Begin(10000); ref var sc = ref w.Settlements.Get(sid); for (int k = 1; k < days; k++) CropDay(ref sc, (doy + k) % SimTime.DaysPerYear, k); prof?.End(10000); }
            prof?.Begin(10006); w.Stock.Day(sid, days); prof?.End(10006);   // the herds' day: grazing, milk, births, slaughter (D2-048)
            prof?.Begin(10012); Hearths(sid, days); prof?.End(10012);       // the homes' warmth against the day's cold (D2-056)
            prof?.Begin(10005); Demography(sid, days); prof?.End(10005);
            prof?.Begin(10014); w.Learning.Day(sid, days); prof?.End(10014);   // the young copy the best they can watch (D2-062)
            Training.Daily(w, sid, days);   // fighting skill fades unused; the village's best is known (C1.5)
            prof?.Begin(10015); w.Disease.Day(sid, days); prof?.End(10015);    // sickness: its course, its spread, new ones from the animals (D2-066)
            if (days > 1) Ahead(sid, g0, days);                                 // (its store as days lived would hold it: F8)
            prof?.Begin(10016); w.Trade.Day(sid, days); prof?.End(10016);      // relief, and COARSE trading trips (D2-067)
            prof?.Begin(10017); w.Conflict.Day(sid, days); prof?.End(10017);   // COARSE: the hungry who go to a neighbour's store in the night (D2-069)
            if (days > 1) Ahead(sid, g0, days);
            ref var s2 = ref w.Settlements.Get(sid);
            if (s2.Population <= 0 && s2.Abandoned == 0)
            {
                s2.Abandoned = now;
                Structures.Abandon(w, sid);   // its works are left unkept (C1.1)
                w.HerdSys.Dissolve(sid);
                w.Stock.Feral(sid);   // its animals are left to the land (D2-048)
                w.ReleaseSettlementLand(sid);   // and its fields and ground (D2-052)
                w.Record(new HistoryEvent { Kind = HistoryKind.SettlementAbandoned, Time = now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Cause1 = s2.HungerEvent });
                w.History.EndEntity(EntityRef.Settlement(sid), now);
                return;
            }
            w.Queue.Push(now + days * SimTime.Day, Phase.Settlement, EventKind.SettlementDaily, sid);
        }

        /// <summary>
        /// DEEP (F8): a step takes its days' meals and larders out of the store, and brings in their catch and meat, at its start -
        /// days lived take and bring them a day at a time. What the step moved beyond its first day is kept as the store's lead
        /// (<see cref="Settlement.Ahead"/>), spent evenly through the step, so that the store seen by trade, relief and the hungry
        /// (<see cref="Settlement.StoreSeen"/>) is the one days lived would hold at that hour (seen at the step's start, it was
        /// half a step poorer: relief came at half the rate of days lived - F8 calibration).
        /// </summary>
        void Ahead(int sid, double g0, int days)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            s.Ahead = (g0 - s.Granary) * (days - 1) / days;
            s.AheadFrom = w.Now;
            s.AheadTo = w.Now + days * SimTime.Day;
        }

        /// <summary>The village's calendar days (its own year's local days): a DEEP step never runs across one.</summary>
        static readonly int[] Calendar = { 0, 12, 30, 39, Migration.DecideDay };

        /// <summary>A step of up to <paramref name="most"/> days from <paramref name="doy"/> that ends at or before the next calendar day.</summary>
        public static int DaysToCalendar(int doy, int most)
        {
            int days = most;
            for (int i = 0; i < Calendar.Length; i++)
            {
                int ahead = Calendar[i] - doy;
                if (ahead <= 0) ahead += SimTime.DaysPerYear;
                if (ahead < days) days = ahead;
            }
            return Math.Max(1, days);
        }

        /// <summary>
        /// A village calm enough to live several days at a step (DEEP, F8): fed (no hunger, rations of 0.7 or more), nobody sick in it, nobody
        /// abed with wounds, no household leaving, no band of its out and no raid on it under way. Otherwise it lives day by day.
        /// </summary>
        public bool Calm(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.HungerEvent != 0 || s.Ration < 0.7 || s.Granary < 1.0) return Unrest(0);   // (a prudent ration is lived as well by the step; hunger is not)
            if (s.Epidemic != 0 || s.InfNow > 0) return Unrest(1);
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                if (p.Sick != 0) return Unrest(2);
                if (p.Hurt > 0f) return Unrest(3);
                if (p.At != sid || p.Step == Agents.StepKind.Travel) return Unrest(4);
            }
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) if (w.Households.Get(hhs[i]).Party != 0) return Unrest(5);
            for (int b = w.Bands.First(); b != 0; b = w.Bands.Next(b))
            {
                ref var band = ref w.Bands.Get(b);
                if (band.Origin == sid || band.Target == sid) return Unrest(6);
            }
            return true;
        }

        /// <summary>Tools: the days villages were not calm, by why (hunger or short rations, an epidemic or infection, someone sick, someone hurt,
        /// someone away, a household leaving, a band); null in the game.</summary>
        public static long[] Unrests;

        /// <summary>Tools: the harvests - yield, hectares, how many, and the sums of the crop's growth, the share sown and the share brought
        /// in; null in the game.</summary>
        public static double[] Harvests;

        /// <summary>Tools: demography - person-days, natural deaths, -, infant-days, infant deaths, conception exposure weighted by fertility
        /// and health, conceptions, marriage exposure weighted by age, marriages, conception woman-days; null in the game.</summary>
        public static double[] Demo;
        static bool Unrest(int why) { if (Unrests != null) Unrests[why]++; return false; }

        /// <summary>The days of a step from <paramref name="now"/> (a village's midnight) before the one on which time t has come (0 when it has).</summary>
        public static int DaysUntil(long t, long now) => t <= now ? 0 : (int)((t - now + SimTime.Day - 1) / SimTime.Day);

        /// <summary>(1 - p)^n by repeated products (exact and the same on every runtime).</summary>
        public static double Keep(double p, int n)
        {
            double k = 1.0 - p, r = 1.0;
            for (int i = 0; i < n; i++) r *= k;
            return r;
        }

        /// <summary>The chance that a daily chance p comes at least once in n days (p itself for one day).</summary>
        public static double Over(double p, int n) => n == 1 ? p : 1.0 - Keep(p, n);

        /// <summary>
        /// The hearths' day (D2-056): each household's fire against the day's cold, as far as the village's woodstore allows (a
        /// short store is shared alike), and how warm its home is - the roof and the fire between the weather and the body. The
        /// first day of homes cold enough to harm is history; the spell ends when every home is warm again.
        /// </summary>
        void Hearths(int sid, int days)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            // (DEEP: the weather of the step's middle day - the season turns through a step of twelve days)
            double tOut = w.PlanetSys.DayMean(s.Cell, Planet.PlanetSystems.Season(days == 1 ? w.Now : w.Now + days / 2 * SimTime.Day));
            var hhs = w.HouseholdsOf(sid);
            double want = 0, cook = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                want += Hearth.Wanted(tOut, w.Households.Get(hhs[i]).Shelter) * Hearth.WarmFuel;
                cook += HouseholdNeed(hhs[i]) * Hearth.CookFuel;
            }
            s.FuelDay = (float)(want + cook);   // what tomorrow's hearths will want: the wood to cut is judged by it
            double share = want > 1e-9 ? DMath.Min(1.0, s.Woodstore / (want * days)) : 1.0;   // (DEEP: the step's fires)
            s.Woodstore = DMath.Max(0.0, s.Woodstore - want * days * share);
            int cold = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                // a household in a camp moves into a home that stands empty (D2-059)
                if (hh.Building == 0 && hh.Party == 0) Homes.TakeEmpty(w, hhs[i]);
                double fire = Hearth.Wanted(tOut, hh.Shelter) * share;
                hh.Indoor = (float)Hearth.Indoors(tOut, hh.Shelter, fire);
                int members = w.Members(hhs[i]).Count;
                // the day's wear of its members' clothes and tools (D2-058), slower where the tools are metal (D2-064)
                hh.Gear = (float)DMath.Max(0.0, hh.Gear - members * Goods.WearPerDay * days * Knowledge.Tech.MetalWear(Knowledge.Tech.MetalShare(s.Metal, s.Population)));
                if (members > 0 && hh.Indoor < Goods.ChillLine(Goods.Condition(hh.Gear, members))) cold++;
            }
            if (cold > 0 && s.ColdEvent == 0)
            {
                s.ColdEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.ColdHomes, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Magnitude = cold, ArgA = share < 0.999 ? 1 : 0, ArgB = (int)DMath.RoundHalfUp(tOut), RootKind = RootKind.Nature,
                });
                w.Metrics.ColdSpells++;
            }
            else if (cold == 0) s.ColdEvent = 0;
        }

        /// <summary>
        /// The last of a household has died (D2-059): its home stands empty; its larder, goods and animals go to the nearest kin
        /// in the village (a child's household, else a parent's, else a sibling's; else the household with the fewest animals,
        /// which keeps them with the village's herd), and where nobody is left the animals go to the wild; the household is no more.
        /// </summary>
        public void DieOut(int hid, int lastPid)
        {
            var w = _w;
            ref var hh = ref w.Households.Get(hid);
            if (hh.Dissolved != 0) return;
            int sid = hh.Settlement;
            int heir = Heir(hid, lastPid, sid);
            Homes.Leave(w, hid);
            if (heir != 0)
            {
                ref var to = ref w.Households.Get(heir);
                to.Larder += hh.Larder + hh.Ready;
                to.Gear = (float)(to.Gear + (double)hh.Gear);
                w.Stock.Inherit(hid, heir);
            }
            else w.Stock.FeralHousehold(hid, w.Settlements.Get(sid).Cell);
            hh.Larder = 0; hh.Ready = 0; hh.Gear = 0f;
            hh.Dissolved = w.Now;
            w.History.EndEntity(EntityRef.Household(hid), w.Now);
            w.HouseholdsOf(sid).Remove(hid);
            w.Metrics.DiedOut++;
        }

        int Heir(int hid, int lastPid, int sid)
        {
            var w = _w;
            ref var d = ref w.People.Get(lastPid);
            int best = 0, rank = 9;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count && rank > 0; i++)
            {
                int h = hhs[i];
                if (h == hid) continue;
                var mem = w.Members(h);
                for (int k = 0; k < mem.Count; k++)
                {
                    ref var q = ref w.People.Get(mem[k]);
                    if (!q.Alive) continue;
                    int r = q.Mother == lastPid || q.Father == lastPid ? 0
                          : (q.Id == d.Mother || q.Id == d.Father ? 1
                          : ((d.Mother != 0 && q.Mother == d.Mother) || (d.Father != 0 && q.Father == d.Father) ? 2 : 9));
                    if (r < rank) { rank = r; best = h; }
                }
            }
            if (best != 0) return best;
            int fewest = int.MaxValue;
            for (int i = 0; i < hhs.Count; i++)
            {
                int h = hhs[i];
                if (h == hid || w.Members(h).Count == 0) continue;
                int n = w.BeastsOf(h).Count;
                if (n < fewest) { fewest = n; best = h; }
            }
            return best;
        }

        /// <summary>Kg of flax fibre a hectare of the village's fields gives a year (a corner of them sown with flax: D2-063).</summary>
        public const double FibrePerHa = 1.0;

        /// <summary>The crop calendar over a step's days, each as a day of its own (DEEP, F8: a step never crosses a stage's day).</summary>
        void Crops(ref Settlement s, int doy, int days)
        {
            for (int k = 0; k < days; k++) CropDay(ref s, (doy + k) % SimTime.DaysPerYear, k);
        }

        void CropDay(ref Settlement s, int doy, int k = 0)
        {
            var w = _w;
            var cfg = w.Config;
            var P = w.Planet;
            if (doy == 0)
            {
                s.Crop = CropStage.Sown; s.CropWork = 0; s.Sown = 0; s.Harvested = 0; s.CropGrowth = 0; s.CropBurnt = 0; s.CropCause = 0;
                // a village that remembers famine sows more than it needs, as far as its land allows (D2-042)
                double perPerson = cfg.FieldHaPerPerson * (1.0 + 0.5 * s.Memory);
                ClaimFields(ref s, s.Population * perPerson);
                s.FieldHa = Math.Max(0.5, Math.Min(s.Population * perPerson, s.ArableHa));
                s.CropWorkNeed = s.FieldHa * 40.0;
                s.ExpectedHarvest = s.FieldHa * cfg.FieldYieldPerHa * 0.8;
            }
            else if (s.Crop == CropStage.Sown && doy >= 12)
            {
                s.Sown = DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                // weeding, and keeping birds and beasts off the young crop: ~24 hours a hectare over the stage (hand weeding of a
                // cereal took 50-100 hours a hectare a season; D2-058)
                s.Crop = CropStage.Growing; s.CropWork = 0; s.CropWorkNeed = s.FieldHa * 24.0;
            }
            else if (s.Crop == CropStage.Growing && doy >= 30)
            {
                s.Crop = CropStage.Ripe; s.CropWork = 0; s.CropWorkNeed = s.FieldHa * 45.0;
            }
            else if (s.Crop == CropStage.Ripe && doy >= 39)
            {
                s.Harvested = DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                double yield = s.FieldHa * cfg.FieldYieldPerHa * DMath.Clamp01(s.CropGrowth) * s.Sown * s.Harvested * (1.0 - s.CropBurnt);
                if (Harvests != null) { Harvests[0] += yield; Harvests[1] += s.FieldHa; Harvests[2]++; Harvests[3] += DMath.Clamp01(s.CropGrowth); Harvests[4] += s.Sown; Harvests[5] += s.Harvested; }
                s.Granary += yield;
                s.LastHarvest = yield;
                // and the flax grown on a corner of the fields: fibre for clothes and cordage (D2-063)
                s.Fibre += s.FieldHa * FibrePerHa * DMath.Clamp01(s.ExpectedHarvest > 0 ? yield / s.ExpectedHarvest : 1.0);
                s.Crop = CropStage.Harvested; s.CropWorkNeed = 0; s.CropWork = 0;
                long hev = w.Record(new HistoryEvent { Kind = HistoryKind.Harvest, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id), Magnitude = (float)yield });
                Structures.AfterHarvest(w, s.Id, hev);   // (an overfilled store wants another granary: C1.1)
                w.Conflict.Harvested(s.Id, yield);   // (a tributary pays its lord: D2-075)
                if (s.ExpectedHarvest > 0 && yield < 0.5 * s.ExpectedHarvest)
                {
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.HarvestFailed, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id),
                        Cause1 = s.CropCause, Rel1 = CauseRel.CausedBy, Cause2 = hev, Rel2 = CauseRel.PartOf,
                        Magnitude = (float)(1.0 - yield / s.ExpectedHarvest), RootKind = RootKind.Nature,
                    });
                }
            }
            if (s.Crop == CropStage.Growing)
            {
                // growth on the claimed cells, weighted by their arable area (the weather of each field)
                // (DEEP: a later day of the step is warmer or colder by the season's curve from the step's start - the first growing
                // step began at the year's mean as summer warmed to its height, and grew the crop several degrees too cold)
                double s0 = 0, sk = 0;
                if (k > 0) { s0 = Planet.PlanetSystems.Season(w.Now); sk = Planet.PlanetSystems.Season(w.Now + k * SimTime.Day); }
                double tf = 0, wf = 0, wsum = 0;
                ForEachField(s.Id, (c, ha) =>
                {
                    double t = k == 0 ? P.Temp[c] : P.Temp[c] + (w.PlanetSys.DayMean(c, sk) - w.PlanetSys.DayMean(c, s0));
                    tf += ha * DMath.SmoothStep(3, 13, t) * (1.0 - DMath.SmoothStep(31, 39, t));
                    wf += ha * DMath.SmoothStep(0.06, 0.32, P.SoilWater[c]);
                    wsum += ha;
                });
                if (wsum > 0) { tf /= wsum; wf /= wsum; }
                else
                {
                    int fc = s.FieldCell;
                    double t = k == 0 ? P.Temp[fc] : P.Temp[fc] + (w.PlanetSys.DayMean(fc, sk) - w.PlanetSys.DayMean(fc, s0));
                    tf = DMath.SmoothStep(3, 13, t) * (1.0 - DMath.SmoothStep(31, 39, t));
                    wf = DMath.SmoothStep(0.06, 0.32, P.SoilWater[fc]);
                }
                // the weeds grow with the crop: a field is kept while its weeding keeps pace with the season so far (D2-058)
                double season = DMath.Clamp((doy - 11) / 18.0, 0.05, 1.0);
                double tend = 0.75 + 0.25 * DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed * season));
                s.CropGrowth = DMath.Clamp01(s.CropGrowth + tf * wf * tend / 16.0);
            }
        }

        readonly List<int> _claims = new List<int>();
        readonly List<(double score, int cell)> _cand = new List<(double, int)>();

        /// <summary>Calls back (cell, arable ha) for every cell the settlement's fields claim.</summary>
        void ForEachField(int sid, Action<int, double> f)
        {
            var w = _w; var P = w.Planet;
            var list = w.FieldCellsOf(sid);
            for (int i = 0; i < list.Count; i++)
            {
                int c = list[i];
                f(c, P.AreaHa(c) * P.Arability(c, w.PlanetSys.MeanTemp(c)));
            }
        }

        /// <summary>
        /// Each spring a settlement holds on to the fields it needs and claims more within reach when it has grown
        /// (best land first, never another village's), or lets the worst go when it has shrunk (08 §1).
        /// </summary>
        void ClaimFields(ref Settlement s, double needHa)
        {
            var w = _w; var P = w.Planet; var cfg = w.Config;
            var mine = w.FieldCellsOf(s.Id);
            double have = 0;
            for (int i = 0; i < mine.Count; i++) have += P.AreaHa(mine[i]) * P.Arability(mine[i], w.PlanetSys.MeanTemp(mine[i]));
            if (have > needHa * 1.3 && mine.Count > 1)
            {
                // release the poorest claims beyond the need
                _cand.Clear();
                for (int i = 0; i < mine.Count; i++) { int c = mine[i]; _cand.Add((P.Arability(c, w.PlanetSys.MeanTemp(c)), c)); }
                _cand.Sort((x, y) => x.score != y.score ? x.score.CompareTo(y.score) : x.cell.CompareTo(y.cell));
                for (int i = 0; i < _cand.Count && have > needHa * 1.15; i++)
                {
                    int c = _cand[i].cell;
                    double ha = P.AreaHa(c) * _cand[i].score;
                    if (have - ha < needHa) break;
                    have -= ha;
                    P.FieldOwner[c] = 0;
                    mine.Remove(c);
                }
            }
            else if (have < needHa)
            {
                w.Hazards.CellsWithin(w, s.Cell, cfg.FieldReach, _claims);
                _cand.Clear();
                for (int i = 0; i < _claims.Count; i++)
                {
                    int c = _claims[i];
                    if (P.FieldOwner[c] != 0 || !P.IsLand(c)) continue;
                    if (w.SettlementAtCell(c) != 0) continue; // a village's own ground: houses, yards, gardens
                    double a = P.Arability(c, w.PlanetSys.MeanTemp(c));
                    if (a < 0.08) continue;
                    double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                    _cand.Add((a / (1.0 + d / 600.0), c));
                }
                _cand.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.cell.CompareTo(y.cell));
                for (int i = 0; i < _cand.Count && have < needHa; i++)
                {
                    int c = _cand[i].cell;
                    P.FieldOwner[c] = s.Id;
                    mine.InsertSorted(c);
                    have += P.AreaHa(c) * P.Arability(c, w.PlanetSys.MeanTemp(c));
                }
            }
            s.ArableHa = have;
            s.FieldCells = mine.Count;
        }

        public void ClaimInitialFields(int sid, double needHa)
        {
            ref var s = ref _w.Settlements.Get(sid);
            ClaimFields(ref s, needHa);
        }

        void Food(ref Settlement s, int doy, int days)
        {
            var w = _w;
            var prof = w.Profiler;
            // famine is remembered by the adults who lived through it: the memory fades as they die (D2-042)
            prof?.Begin(10009);
            s.Memory = FamineMemory(s.Id, s.FamineAt);
            prof?.End(10009);
            prof?.Begin(10010);
            // the granary refills each household's larder to two days of its members' needs - rationed so
            // that what is stored lasts until the next harvest (the reversible first step of coping), and by
            // prudence a reserve beyond it, larger where famine is remembered (rations no lower than 0.8 for that alone)
            double daily = 0;
            var hhs = w.HouseholdsOf(s.Id);
            for (int i = 0; i < hhs.Count; i++) daily += HouseholdNeed(hhs[i]);
            prof?.End(10010);
            int toHarvest = 39 - doy; if (toHarvest <= 0) toHarvest += SimTime.DaysPerYear;
            double lasts = daily > 0 ? DMath.Clamp(s.Granary / (daily * (toHarvest + 6)), 0.45, 1.0) : 1.0;
            double keep = 6.0 + 30.0 * s.Memory;
            double prudent = daily > 0 ? DMath.Clamp(s.Granary / (daily * (toHarvest + 6 + keep)), 0.8, 1.0) : 1.0;
            double ration = DMath.Min(lasts, prudent);
            s.Ration = ration;
            if (ration < 0.85 && s.RationEvent == 0 && s.Population > 0)
            {
                long failed = LastFailedHarvest(s.Id, SimTime.Year + SimTime.Year / 2);
                s.RationEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.Rationing, Time = w.Now, Cell = s.Cell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id),
                    Cause1 = failed, Rel1 = CauseRel.CausedBy, Magnitude = (float)ration, RootKind = failed == 0 ? RootKind.Nature : RootKind.Unknown,
                });
            }
            else if (ration >= 0.98 && s.RationEvent != 0) s.RationEvent = 0;
            prof?.Begin(10011);
            double gFill = s.Granary;   // (DEEP: the store before the step's larders and decay)
            double cover = days == 1 ? 2.0 : days + 1.0;   // (the larders hold the step's meals and a day more: two days for a day)
            double total = daily * cover * ration;
            double share = total > 0 ? Math.Min(1.0, s.Granary / total) : 1.0;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                double want = HouseholdNeed(hid) * cover * ration - hh.Larder - hh.Ready;
                if (want <= 0) continue;
                double give = Math.Min(want, want * share);
                give = Math.Min(give, s.Granary);
                hh.Larder += give; s.Granary -= give;
            }
            prof?.End(10011);
            // spoilage of stored grain (~12% a year; less kept in fired pots: D2-063)
            double rot = 0.12 / SimTime.DaysPerYear * Knowledge.Tech.PotteryKeeps(s.BestPottery);
            Structures.Rot(ref s, rot, days);   // (what lies beyond the granaries' capacity rots faster: C1.1)
            if (s.Arms > 0) s.Arms *= Keep(Forge.ArmsDecay / SimTime.DaysPerYear, days);   // (arms broken, lost, rusted: C1.4)
            if (s.TradeIn > 0f) s.TradeIn = (float)((double)s.TradeIn * Keep(0.693147 / SimTime.DaysPerYear, days));   // (traders' coming, a year: C1.6)
            // stored hides rot and are eaten by vermin (~20 % a year), fibre less (~5 %): a store holds what is used (D2-063)
            s.Hides *= days == 1 ? 1.0 - 0.20 / SimTime.DaysPerYear : Keep(0.20 / SimTime.DaysPerYear, days);
            s.Fibre *= days == 1 ? 1.0 - 0.05 / SimTime.DaysPerYear : Keep(0.05 / SimTime.DaysPerYear, days);
            s.Metal *= days == 1 ? 1.0 - 0.03 / SimTime.DaysPerYear : Keep(0.03 / SimTime.DaysPerYear, days);   // tools lost, broken, worn past mending (D2-064)
            // (DEEP: empty as days lived would find it after their first day - a step's larders and decay at once could empty a lean
            // store that days lived would not, and record a hunger: F8)
            bool empty = (days == 1 ? s.Granary : s.Granary + (gFill - s.Granary) * (days - 1) / days) < 1.0;
            if (empty && s.HungerEvent == 0 && s.Population > 0)
            {
                s.HungerEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Hunger, Time = w.Now, Cell = s.Cell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id), Cause1 = LastFailedHarvest(s.Id), Rel1 = CauseRel.CausedBy });
            }
            else if (!empty && s.Granary > 2.0 * total && s.HungerEvent != 0) s.HungerEvent = 0;
            if (s.RationEvent == 0 && s.HungerEvent == 0) s.WildEvent = 0;
        }

        /// <summary>The share of a village's adults who were eight or older when its last famine struck (D2-042).</summary>
        double FamineMemory(int sid, long famineAt)
        {
            if (famineAt <= 0) return 0;
            var w = _w;
            var res = w.Residents(sid);
            long cutoff = famineAt - 8 * SimTime.Year;
            int adults = 0, lived = 0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive || p.AgeYears(w.Now) < 16) continue;
                adults++;
                if (p.Born <= cutoff) lived++;
            }
            return adults > 0 ? lived / (double)adults : 0;
        }

        /// <summary>The first need-driven trip to the wild of an episode enters the history, in response to the shortage (D2-042).</summary>
        public void NoteWildFood(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.WildEvent != 0 || !s.Inhabited) return;
            long cause = s.HungerEvent != 0 ? s.HungerEvent : s.RationEvent;
            if (cause == 0) return;   // a prudent cut is not a shortage the history tells of
            s.WildEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.WildFood, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Cause1 = cause, Rel1 = CauseRel.InResponseTo, RootKind = cause == 0 ? RootKind.Nature : RootKind.Unknown,
            });
        }

        /// <summary>A village's famine (D2-042): the first hunger death of a year records it, and later deaths that year share it.</summary>
        long FamineCause(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.FamineEvent == 0 || w.Now - s.FamineAt > SimTime.Year)
            {
                long cause = s.HungerEvent != 0 ? s.HungerEvent : s.RationEvent;
                s.FamineEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.Famine, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Cause1 = cause, Rel1 = CauseRel.CausedBy, RootKind = cause == 0 ? RootKind.Nature : RootKind.Unknown,
                });
                s.FamineAt = w.Now;
                w.Metrics.Famines++;
            }
            return s.FamineEvent;
        }

        long LastFailedHarvest(int sid, long maxAge = long.MaxValue)
        {
            var list = _w.History.EventsOf(EntityRef.Settlement(sid));
            if (list == null) return 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!_w.History.TryGet(list[i], out var e)) continue;
                if (_w.Now - e.Time > maxAge) return 0;
                if (e.Kind == HistoryKind.HarvestFailed) return e.Id;
            }
            return 0;
        }

        /// <summary>FU a day a household's members need (by age, nursing mothers more: D2-042).</summary>
        public double HouseholdNeed(int hid)
        {
            var w = _w;
            var mem = w.Members(hid);
            double need = 0;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var p = ref w.People.Get(mem[i]);
                need += w.Agents.DailyNeed(in p, w.Now);
            }
            return need;
        }

        void Demography(int sid, int days)
        {
            var w = _w;
            var cfg = w.Config;
            long now = w.Now;
            ulong seed = w.Seed;
            long day = SimTime.FloorDiv(now, SimTime.Day);
            var res = w.Residents(sid);
            _scratch.Clear();
            for (int i = 0; i < res.Count; i++) _scratch.Add(res[i]);
            // deaths and health
            for (int i = 0; i < _scratch.Count; i++)
            {
                int pid = _scratch[i];
                ref var p = ref w.People.Get(pid);
                if (!p.Alive) continue;
                double age = p.AgeYears(now);
                double annual = age < 1 ? 0.22 : age < 5 ? 0.045 : age < 15 ? 0.008 : age < 45 ? 0.010 : age < 60 ? 0.020 : age < 70 ? 0.045 : age < 80 ? 0.10 : 0.22;
                double u = KeyedRng.Unit(seed, RngStream.Death, (ulong)pid, (ulong)day);
                // the body's day (D2-042): what was eaten against what it needed draws on or refills its reserve (DEEP: the step's
                // meals against the step's need)
                double need = w.Agents.DailyNeed(in p, now) * days;
                double share = need > 0 ? p.Ate / need : 1.0;
                p.Ate = 0f;
                if (now - p.Born < SimTime.Day * days) share = 1.0;   // born today (or this step): at the breast from the first hour
                if (Demo != null) { Demo[0] += days; if (age < 1) Demo[3] += days; }
                if (days > 1) { DeepBody(ref p, sid, share, age, annual, u, day, days); continue; }
                if (age < 2 && p.Mother != 0 && w.People.Exists(p.Mother))
                {
                    // a baby at the breast lives on its mother's milk while her body can make it
                    ref var mo = ref w.People.Get(p.Mother);
                    if (mo.Alive && mo.NursingUntil > now && mo.Household == p.Household)
                        share = DMath.Max(share, DMath.Clamp01(0.3 + 1.4 * mo.Reserve / AgentSystem.ReserveCap(in mo, seed, now)));
                }
                p.Nutrition = (float)DMath.Clamp01(share);
                double cap = AgentSystem.ReserveCap(in p, seed, now);
                p.Reserve = (float)DMath.Min(cap, p.Reserve + share - 1.0);
                if (share < 0.4) { if (p.HungryFrom == 0) p.HungryFrom = now; }
                else if (share >= 0.8) p.HungryFrom = 0;
                if (p.Reserve <= 0f)
                {
                    // the body's reserve is spent: they die of hunger, the village's famine their cause
                    w.Agents.Kill(ref p, FamineCause(sid), 2);
                    w.Metrics.HungerDeaths++;
                    continue;
                }
                // health follows the reserve once half of it is gone; it mends slowly when the body is full again
                double rr = p.Reserve / cap;
                double fed = rr >= 0.5 ? 1.0 : 0.15 + 1.7 * rr;
                // a cold home takes health, most from infants and the old (D2-056)
                double chill = 0.0;
                if (p.At == p.Home && w.Households.Exists(p.Household))
                {
                    ref var home = ref w.Households.Get(p.Household);
                    chill = Hearth.Chill(home.Indoor, age, Goods.Condition(home.Gear, w.Members(p.Household).Count));
                }
                p.Health = (float)DMath.Max(0.02, DMath.Min(p.Health + 0.05, fed) - chill);
                // a body the cold has worn to nothing dies of exposure, as a starved one does when its reserve is spent
                if (chill > 0 && p.Health <= 0.05 && KeyedRng.Unit(seed, RngStream.Death, (ulong)pid, (ulong)day, 9) < DMath.Min(1.0, 2.0 * chill))
                {
                    w.Agents.Kill(ref p, w.Settlements.Get(sid).ColdEvent, 9);
                    w.Metrics.ColdDeaths++;
                    continue;
                }
                double hazard = annual / SimTime.DaysPerYear * (1.0 + 3.0 * (1.0 - p.Health));
                if (u < hazard)
                {
                    // the part of the day's risk the cold added (health under what the body's reserve allows) is the cold's
                    double warm = annual / SimTime.DaysPerYear * (1.0 + 3.0 * (1.0 - fed));
                    if (chill > 0 && u >= warm) { w.Agents.Kill(ref p, w.Settlements.Get(sid).ColdEvent, 9); w.Metrics.ColdDeaths++; }
                    else { w.Agents.Kill(ref p, 0, 1); if (Demo != null) { Demo[1]++; if (age < 1) Demo[4]++; } }
                    continue;
                }
            }
            // marriages (women in id order choose among unmarried men of the place)
            for (int i = 0; i < _scratch.Count; i++)
            {
                int pid = _scratch[i];
                ref var woman = ref w.People.Get(pid);
                if (!woman.Alive || !woman.IsFemale || woman.Partner != 0) continue;
                double age = woman.AgeYears(now);
                int open = days;   // (DEEP: the step's days she may marry on - from her eighteenth birthday if it falls within it)
                if (age < 18)
                {
                    if (days == 1) continue;
                    open = days - DaysUntil(woman.Born + SimTime.Years(18), now);
                    if (open <= 0) continue;
                }
                if (age > 36) continue;
                double peak = age < 21 ? 0.6 : (age < 27 ? 1.0 : (age < 31 ? 0.6 : 0.3));
                if (Demo != null) Demo[7] += open * peak;
                if (KeyedRng.Unit(seed, RngStream.Marriage, (ulong)pid, (ulong)day) >= Over(cfg.MarriageRatePerDay * peak, open)) continue;   // (DEEP: over the step)
                _men.Clear();
                AddSuitors(in woman, sid, now);
                // neighbouring villages within a day's easy walk (exogamy keeps small places alive)
                ref var here = ref w.Settlements.Get(sid);
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                {
                    if (o == sid) continue;
                    ref var other = ref w.Settlements.Get(o);
                    if (!other.Inhabited) continue;
                    if (Vec3d.ArcDistance(here.Anchor, other.Anchor, w.Planet.Radius) > cfg.MarriageReach) continue;
                    if (w.Routes.Get(w, sid, o) == null) continue;
                    AddSuitors(in woman, o, now);
                }
                if (_men.Count == 0) continue;
                int pick = _men[KeyedRng.Below(KeyedRng.Hash(seed, RngStream.Marriage, (ulong)pid, (ulong)day, 1), _men.Count)];
                Marry(w.People.Get(pick).Home, pid, pick);
                if (Demo != null) Demo[8]++;
            }
            // conceptions
            for (int i = 0; i < _scratch.Count; i++)
            {
                int pid = _scratch[i];
                ref var p = ref w.People.Get(pid);
                if (!p.Alive || !p.IsFemale || p.Partner == 0 || p.PregDue != 0) continue;
                // (DEEP: nursing may end within the step - she may conceive from that day on, as days lived would let her; waiting
                // for the next step cost ~5 days a birth, 3 % of births, a sixth of the people by year 200: F8 calibration)
                int from = 0;
                if (p.NursingUntil > now)
                {
                    if (days == 1) continue;
                    from = DaysUntil(p.NursingUntil, now);
                    if (from >= days) continue;
                }
                double age = p.AgeYears(now);
                if (age < 16)
                {
                    if (days == 1) continue;
                    from = Math.Max(from, DaysUntil(p.Born + SimTime.Years(16), now));   // (her sixteenth birthday within the step)
                    if (from >= days) continue;
                }
                if (age > 44) continue;
                ref var partner = ref w.People.Get(p.Partner);
                if (!partner.Alive) continue;
                double fert = age < 25 ? 1.0 : (age < 30 ? 0.93 : (age < 35 ? 0.82 : (age < 40 ? 0.62 : 0.3)));
                double health = days > 1 && p.StepHealth > 0 ? p.StepHealth : p.Health;   // (DEEP: the step's mean health)
                if (KeyedRng.Unit(seed, RngStream.Birth, (ulong)pid, (ulong)day) < Over(cfg.ConceptionPerDay * fert * health, days - from))
                {
                    // (DEEP: conceived on one of the step's days she could, by a keyed draw; a day's step is its own day)
                    long onDay = days == 1 ? 0 : from + (long)(KeyedRng.Unit(seed, RngStream.Birth, (ulong)pid, (ulong)day, 7) * (days - from));
                    p.PregDue = now + (36 + onDay) * SimTime.Day;
                    p.PregFather = p.Partner;
                    if (Demo != null) { Demo[5] += (onDay - from + 1) * fert * health; Demo[9] += onDay - from + 1; Demo[6]++; }
                    w.Queue.Push(p.PregDue, Phase.Agent, EventKind.Birth, pid, 0, p.PregDue);
                }
                else if (Demo != null) { Demo[5] += (days - from) * fert * health; Demo[9] += days - from; }
            }
        }

        /// <summary>
        /// A body's step of several days (DEEP, F8): the step's meals against its need draw on or refill the reserve, health follows
        /// it day by day against the cold at home, and the step's deaths come by the same causes at their chance over its days.
        /// </summary>
        void DeepBody(ref Person p, int sid, double share, double age, double annual, double u, long day, int days)
        {
            var w = _w;
            long now = w.Now;
            ulong seed = w.Seed;
            if (age < 2 && p.Mother != 0 && w.People.Exists(p.Mother))
            {
                // a baby at the breast lives on its mother's milk while her body can make it
                ref var mo = ref w.People.Get(p.Mother);
                if (mo.Alive && mo.NursingUntil > now && mo.Household == p.Household)
                    share = DMath.Max(share, DMath.Clamp01(0.3 + 1.4 * mo.Reserve / AgentSystem.ReserveCap(in mo, seed, now)));
            }
            p.Nutrition = (float)DMath.Clamp01(share);
            double cap = AgentSystem.ReserveCap(in p, seed, now);
            p.Reserve = (float)DMath.Min(cap, p.Reserve + (share - 1.0) * days);
            if (share < 0.4) { if (p.HungryFrom == 0) p.HungryFrom = now; }
            else if (share >= 0.8) p.HungryFrom = 0;
            if (p.Reserve <= 0f)
            {
                w.Agents.Kill(ref p, FamineCause(sid), 2);
                w.Metrics.HungerDeaths++;
                return;
            }
            double rr = p.Reserve / cap;
            double fed = rr >= 0.5 ? 1.0 : 0.15 + 1.7 * rr;
            double chill = 0.0;
            if (p.At == p.Home && w.Households.Exists(p.Household))
            {
                ref var home = ref w.Households.Get(p.Household);
                chill = Hearth.Chill(home.Indoor, age, Goods.Condition(home.Gear, w.Members(p.Household).Count));
            }
            // health day by day over the step: the cold may wear a body to nothing on one of its days; each day's chance of death on
            // that day's health (the step's end health for all its days gave fewer deaths as health came back in spring and more as
            // it ebbed in winter - a DEEP world grew faster: F8 calibration); the step's mean health for its conceptions
            double hp = p.Health, daily = annual / SimTime.DaysPerYear, keep = 1.0, keepWarm = 1.0, sum = 0.0;
            double warmDay = 1.0 - daily * (1.0 + 3.0 * (1.0 - fed));
            for (int k = 0; k < days; k++)
            {
                hp = DMath.Max(0.02, DMath.Min(hp + 0.05, fed) - chill);
                if (chill > 0 && hp <= 0.05 && KeyedRng.Unit(seed, RngStream.Death, (ulong)p.Id, (ulong)(day + k), 9) < DMath.Min(1.0, 2.0 * chill))
                {
                    p.Health = (float)hp;
                    w.Agents.Kill(ref p, w.Settlements.Get(sid).ColdEvent, 9);
                    w.Metrics.ColdDeaths++;
                    return;
                }
                keep *= 1.0 - daily * (1.0 + 3.0 * (1.0 - hp));
                keepWarm *= warmDay;
                sum += hp;
            }
            p.Health = (float)hp;
            p.StepHealth = sum / days;
            // the step's other deaths, the cold's part as the day's
            if (u < 1.0 - keep)
            {
                if (chill > 0 && u >= 1.0 - keepWarm) { w.Agents.Kill(ref p, w.Settlements.Get(sid).ColdEvent, 9); w.Metrics.ColdDeaths++; }
                else { w.Agents.Kill(ref p, 0, 1); if (Demo != null) { Demo[1]++; if (age < 1) Demo[4]++; } }
            }
        }

        void AddSuitors(in Person woman, int sid, long now)
        {
            var res = _w.Residents(sid);
            for (int j = 0; j < res.Count; j++)
            {
                int mid = res[j];
                ref var man = ref _w.People.Get(mid);
                if (!man.Alive || man.IsFemale || man.Partner != 0) continue;
                double ma = man.AgeYears(now);
                if (ma < 18 || ma > 48) continue;
                if (CloseKin(in woman, in man)) continue;
                _men.Add(mid);
            }
        }

        static bool CloseKin(in Person a, in Person b)
        {
            if (a.Mother != 0 && a.Mother == b.Mother) return true;
            if (a.Father != 0 && a.Father == b.Father) return true;
            if (a.Mother == b.Id || a.Father == b.Id || b.Mother == a.Id || b.Father == a.Id) return true;
            return false;
        }

        /// <summary>Premise (tests, tools): a woman and a man of a village marry now, as the village's day would have them do.</summary>
        public void Wed(int sid, int womanId, int manId) => Marry(sid, womanId, manId);

        void Marry(int sid, int womanId, int manId)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            int hid = w.Ids.Next(EntityKind.Household);
            int manHh = w.People.Get(manId).Household;
            int fam = w.Households.Exists(manHh) ? w.Households.Get(manHh).FamilyName : manHh;
            var hh = new Household
            {
                Id = hid, Settlement = sid, Formed = w.Now, FamilyName = fam, Indoor = (float)Hearth.Comfort,
            };
            w.Households.Add(hid, hh);
            // an empty home in the village if one stands, else a camp on a new lot (D2-059)
            if (!Homes.TakeEmpty(w, hid))
            {
                Homes.FreeLot(w, sid, hid, out float hx, out float hy);
                ref var nh = ref w.Households.Get(hid);
                nh.HomeX = hx; nh.HomeY = hy;
            }
            w.HouseholdsOf(sid).InsertSorted(hid);
            w.History.Register(EntityRef.Household(hid), w.FamilyName(hh.FamilyName), w.Now);
            MoveTo(womanId, hid);
            MoveTo(manId, hid);
            ref var woman = ref w.People.Get(womanId);
            ref var man = ref w.People.Get(manId);
            woman.Partner = manId; man.Partner = womanId;
            int brideFrom = woman.Home;   // (tools: how far brides come, D2-085)
            if (woman.Home != sid)
            {
                // she will walk to her new home when she next decides (her old village is no longer home)
                w.Residents(woman.Home).Remove(womanId);
                w.Settlements.Get(woman.Home).Population--;
                woman.Home = sid;
                w.Residents(sid).InsertSorted(womanId);
                w.Settlements.Get(sid).Population++;
                if (w.Resolution == Resolution.Coarse) { if (woman.At != sid && woman.At != 0) w.Visitors(woman.At).Remove(womanId); woman.At = sid; }
                else if (woman.At != 0 && woman.At != sid) { w.Visitors(woman.At).InsertSorted(womanId); woman.Until = w.Now; }
                else if (woman.At == sid) w.Visitors(sid).Remove(womanId);
            }
            long mev = w.Record(new HistoryEvent { Kind = HistoryKind.Marriage, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(womanId), Object = EntityRef.Person(manId), RootKind = RootKind.People });
            w.Record(new HistoryEvent { Kind = HistoryKind.HouseholdFormed, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(hid), Cause1 = mev, Rel1 = CauseRel.CausedBy });
            w.Metrics.Marriages++;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Marriage, brideFrom, sid);
        }

        void MoveTo(int pid, int hid)
        {
            var w = _w;
            ref var p = ref w.People.Get(pid);
            int old = p.Household;
            if (old != 0 && w.Households.Exists(old))
            {
                w.Members(old).Remove(pid);
                ref var oh = ref w.Households.Get(old);
                // a share of the old larder comes along
                int left = w.Members(old).Count;
                double take = left == 0 ? oh.Larder : oh.Larder / (left + 1);
                oh.Larder -= take;
                w.Households.Get(hid).Larder += take;
                // and their own clothes and tools (D2-058)
                double gear = left == 0 ? oh.Gear : (double)oh.Gear / (left + 1);
                oh.Gear = (float)(oh.Gear - gear);
                w.Households.Get(hid).Gear = (float)(w.Households.Get(hid).Gear + gear);
                if (left == 0) { Homes.Leave(w, old); oh.Dissolved = w.Now; w.History.EndEntity(EntityRef.Household(old), w.Now); w.HouseholdsOf(oh.Settlement).Remove(old); w.Stock.Inherit(old, hid); }
                else w.Stock.Endow(old, hid);   // and a share of the herd (D2-048)
            }
            p.Household = hid;
            w.Members(hid).InsertSorted(pid);
        }

        public void OnBirth(int motherId, long due)
        {
            var w = _w;
            if (!w.People.Exists(motherId)) return;
            ref var m = ref w.People.Get(motherId);
            if (!m.Alive || m.PregDue != due) return;
            m.PregDue = 0;
            m.NursingUntil = w.Now + SimTime.Years(1.5);
            int father = m.PregFather;
            m.PregFather = 0;
            byte sex = (byte)(KeyedRng.Unit(w.Seed, RngStream.Birth, (ulong)motherId, (ulong)due, 2) < 0.5 ? Sex.Female : Sex.Male);
            int child = w.Agents.CreatePerson(sex, w.Now, motherId, father, m.Household, m.Home, w.Now + SimTime.Hour);
            ref var mm = ref w.People.Get(motherId);
            long born = 0;
            born = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.PersonBorn, Time = w.Now, Cell = w.Settlements.Get(mm.Home).Cell, Settlement = mm.Home,
                Subject = EntityRef.Person(child), Object = EntityRef.Person(motherId), Other = EntityRef.Person(father), RootKind = RootKind.People,
            });
            w.Metrics.Births++;
            // childbirth is dangerous: about one mother in a hundred dies of it (more when weak)
            double risk = 0.010 * (1.0 + 2.0 * (1.0 - mm.Health));
            if (KeyedRng.Unit(w.Seed, RngStream.Death, (ulong)motherId, (ulong)due, 9) < risk) w.Agents.Kill(ref mm, born, 3);
        }

        /// <summary>Ash (or lava) on a village's fields: the standing crop is lost by this share, the eruption its cause.</summary>
        public void AshOnFields(int sid, double share, long cause) => CropLoss(sid, share, cause, HistoryKind.AshFall);

        /// <summary>A share of a village's standing crop lost (ash, the sea), with its cause; recorded as the given kind.</summary>
        public void CropLoss(int sid, double share, long cause, HistoryKind kind)
        {
            var w = _w;
            if (!w.Settlements.Exists(sid)) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            if (s.Crop != CropStage.Sown && s.Crop != CropStage.Growing && s.Crop != CropStage.Ripe) return;
            double before = s.CropBurnt;
            s.CropBurnt = DMath.Min(1.0, s.CropBurnt + share);
            if (s.CropBurnt - before < 0.02) return;
            long ash = w.Record(new HistoryEvent
            {
                Kind = kind, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)(s.CropBurnt - before), RootKind = RootKind.Nature,
            });
            if (s.CropCause == 0) s.CropCause = ash;
        }

        /// <summary>Fire burning on a settlement's field or home cell damages the standing crop.</summary>
        public void FireOnCell(int cell, int proc)
        {
            var w = _w;
            int sid = w.Planet.FieldOwner[cell];
            if (sid == 0) sid = w.SettlementAtCell(cell);
            if (sid == 0) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            if (s.Crop != CropStage.Sown && s.Crop != CropStage.Growing && s.Crop != CropStage.Ripe) return;
            double before = s.CropBurnt;
            double share = s.FieldCells > 0 ? 1.0 / s.FieldCells : 1.0;
            s.CropBurnt = DMath.Min(1.0, s.CropBurnt + 0.5 * share * w.Fire.Intensity[cell]);
            if (s.CropCause == 0 && s.CropBurnt > 0.05 && before <= 0.05)
            {
                long start = w.Fire.Procs.Exists(proc) ? w.Fire.Procs.Get(proc).StartEvent : 0;
                s.CropCause = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.FieldsBurned, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Object = EntityRef.Process(proc), Cause1 = start, Rel1 = CauseRel.CausedBy, Magnitude = (float)s.CropBurnt,
                });
            }
        }
    }
}
