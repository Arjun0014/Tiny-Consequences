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
            int doy = DayOfYear(in s, now);   // (its own calendar: the southern seasons half a year on)
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
            Threshing.Day(ref s);   // (what left the store since went threshed first: C3.7b)
            Muck.Age(ref s, days);   // (what waits uncarted in the heaps leaches: C3.7c)
            if (s.Sheaves > 0 || s.ThreshOut > 0f) s.ThreshOut = (float)w.Agents.ThreshingUnderWay(sid);   // (the stints at the floor under way, counted again)
            w.Metrics.VillageSteps++; w.Metrics.VillageDays += days;
            var prof = w.Profiler;   // (tools: the parts of a village's day; null in the game)
            prof?.Begin(10000); Crops(ref s, doy, 1); prof?.End(10000);   // (the step's first day: a stage begins on it, before its work)
            double g0 = s.Granary;   // (DEEP: the store before the step's meals, larders and catch; the harvest is the day's own)
            Society.Leaders.Day(w, sid);   // (a headman gone: the place weighed at once - C10.2)
            prof?.Begin(10001); Food(ref s, doy, days); prof?.End(10001);
            { ref var sw = ref w.Settlements.Get(sid); sw.StoneWant = (float)Stone.Want(w, sid); sw.OreWant = (float)Mining.Want(w, sid); sw.CharcoalWant = (float)Charcoal.Want(w, sid); sw.FeOreWant = (float)Mining.Want(w, sid, Mining.Iron); }   // (stone for its next houses' footings: C1.3; ore for its smiths: C5.1)
            Mining.Prospect(w, sid, days);   // (a village whose smiths lack ore goes looking for it: C5.3c)
            Structures.Plan(w, sid);   // what its people want built next, by need (C1.1)
            if (days > 1) Ahead(sid, g0, days);   // (the step's own work reads its store as days lived hold it too: F8)
            prof?.Begin(10002); prof?.Begin(10007); w.Wild.Survey(sid); prof?.End(10007); Boats.Plan(w, sid, days); Carts.Plan(w, sid); prof?.Begin(10008); w.HerdSys.Update(sid); prof?.End(10008); prof?.End(10002);   // the grounds and the herds round it today (D2-042, D2-047)
            if (days > 1) w.Wild.MidStep(sid, w.Stock.MidStepMast(sid, days));   // (DEEP: the woods as the step's middle day finds them)
            if (doy == Migration.DecideDay) { prof?.Begin(10003); Diagnostics.SizeTally.Year(w, sid); Retire(sid); w.Migration.Weigh(sid); Society.Leaders.Weigh(w, sid); Architecture.Year(w, sid); Homes.WeighRebuilding(w, sid); Weaving.Year(w, sid); Forge.Year(w, sid); prof?.End(10003); }   // who leaves (D2-045); the lord's way copied (C11.1b); who rebuilds (D2-114); another shed (C3.1b)
            if (w.Resolution == Resolution.Coarse) { prof?.Begin(10004); w.Agents.CoarseDay(sid, days); prof?.End(10004); }
            // DEEP: the step's later days grow the crop after its work, as days lived grow each day on the work done before it (grown
            // before the step's weeding, a growing step's days were three-quarters tended - harvests fell and households left: F8)
            if (days > 1) { prof?.Begin(10000); ref var sc = ref w.Settlements.Get(sid); for (int k = 1; k < days; k++) CropDay(ref sc, (doy + k) % SimTime.DaysPerYear, k); prof?.End(10000); }
            prof?.Begin(10006); w.Stock.Day(sid, days); prof?.End(10006);   // the herds' day: grazing, milk, births, slaughter (D2-048)
            prof?.Begin(10012); Hearths(sid, days); prof?.End(10012);       // the homes' warmth against the day's cold (D2-056)
            prof?.Begin(10005); Demography(sid, days); prof?.End(10005);
            prof?.Begin(10014); w.Learning.Day(sid, days); prof?.End(10014);   // the young copy the best they can watch (D2-062)
            Weaving.Bar(w, sid);            // its best weavers, who have the looms at its weaver's shed (C3.1)
            Weaving.Tally(w, sid, days);    // a day its looms, not its flax, left its wares short (C3.1b)
            Pottery.Bar(w, sid);            // its best potters, who work at its kiln (C3.2)
            Forge.Bar(w, sid);              // its best smiths, who have the places at its smithies (C1.4b)
            Forge.Tally(w, sid, days);      // a day its smiths, not its ore, left the work it wanted undone (C1.4b)
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

        /// <summary>
        /// Tools (the hungry gap survey; null in the game): hunger deaths by days to the village's harvest [0-47], by its crop's stage [48-52], by age
        /// (under 2, 2-11, 12-59, 60+) [53-56], by its store in days of its people's food (under 1, 1-5, 5-20, 20+) [57-60], by days the dead had been
        /// hungry (under 6, 6-12, 12-24, 24+) [61-64], by whether its land binds [65-66]; the sums of its last harvest in years of its people's bread
        /// [67], its ration [68], its wild food in the last day per person (FU) - not kept, 0 - [69], and its beasts per person [70].
        /// </summary>
        public static double[] GapTally;
        public const int GapK = 71;

        void GapDeath(int sid, in Person p)
        {
            var w = _w;
            var t = GapTally;
            ref var s = ref w.Settlements.Get(sid);
            int doy = DayOfYear(in s, w.Now);
            int toHarvest = HarvestDay - doy; if (toHarvest < 0) toHarvest += SimTime.DaysPerYear;
            t[Math.Min(47, toHarvest)]++;
            t[48 + (int)s.Crop]++;
            double age = p.AgeYears(w.Now);
            t[age < 2 ? 53 : age < 12 ? 54 : age < 60 ? 55 : 56]++;
            double daily = DMath.Max(1.0, s.Population * w.Config.FoodPerAdultDay);
            double sd = s.Granary / daily;
            t[sd < 1 ? 57 : sd < 5 ? 58 : sd < 20 ? 59 : 60]++;
            double hd = p.HungryFrom != 0 ? (w.Now - p.HungryFrom) / (double)SimTime.Day : 0.0;
            t[hd < 6 ? 61 : hd < 12 ? 62 : hd < 24 ? 63 : 64]++;
            t[s.ArableHa < s.Population * w.Config.FieldHaPerPerson ? 66 : 65]++;
            t[67] += s.LastHarvest / DMath.Max(1.0, s.Population * (double)SimTime.DaysPerYear);
            t[68] += s.Ration;
            int beasts = 0; var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) beasts += w.BeastsOf(hhs[i]).Count;
            t[70] += beasts / DMath.Max(1.0, (double)s.Population);
        }

        /// <summary>Tools (C3.6 survey; null in the game): the harvests by whether the village's arable within reach is short of its people's need
        /// (0 open, 1 bound) - count, yield, field ha, need ha, growth, sown, brought in, burnt, a year's bread, its beasts' daily intake (kg), arable
        /// ha, its beasts' head; the extra work planned and done (C3.6: shares of the ordinary season's hours), what it added (FU), harvests worked
        /// harder.</summary>
        public static double[] LandTally;
        public const int LandTallyK = 20;

        void TallyLand(in Settlement s, double yield)
        {
            var w = _w;
            double needHa = s.Population * w.Config.FieldHaPerPerson;
            int o = (s.ArableHa < needHa ? 1 : 0) * LandTallyK;
            double intake = 0; int head = 0;
            var hhs = w.HouseholdsOf(s.Id);
            for (int i = 0; i < hhs.Count; i++)
            {
                var mine = w.BeastsOf(hhs[i]);
                for (int k = 0; k < mine.Count; k++) { ref var b = ref w.Beasts.Get(mine[k]); if (b.Alive) { intake += Livestock.IntakeDay(b.Kind); head++; } }
            }
            var t = LandTally;
            t[o]++; t[o + 1] += yield; t[o + 2] += s.FieldHa; t[o + 3] += needHa; t[o + 4] += DMath.Clamp01(s.CropGrowth); t[o + 5] += s.Sown;
            t[o + 6] += s.Harvested; t[o + 7] += s.CropBurnt; t[o + 8] += s.Population * (double)SimTime.DaysPerYear; t[o + 9] += intake; t[o + 10] += s.ArableHa; t[o + 11] += head;
            t[o + 12] += s.Tilth; t[o + 13] += DMath.Min(s.Tilth, s.FieldHa > 0 ? s.TilthDone / (s.FieldHa * SeasonHa) : 0.0); t[o + 14] += yield - yield / (1.0 + s.TilthGain); if (s.Tilth > 0) t[o + 15]++;
            t[o + 16] += s.Tired; t[o + 17] += s.ArableHa > 0 ? 1.0 - DMath.Clamp01(s.FieldHa / s.ArableHa) : 0.0; t[o + 18] += Heart(in s); t[o + 19] += s.ArableHa > 0 ? s.Grazed / SimTime.DaysPerYear / s.ArableHa : 0.0;   // (C3.6f)
        }

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
            s.FuelDay = (float)(want + cook + Pottery.WoodDay(in s));   // what tomorrow's hearths (and its kiln, while its potters have work: C3.2) will want: the wood to cut is judged by it
            double share = want > 1e-9 ? DMath.Min(1.0, s.Woodstore / (want * days)) : 1.0;   // (DEEP: the step's fires)
            s.Woodstore = DMath.Max(0.0, s.Woodstore - want * days * share);
            int cold = 0;
            double held = 0, hearthShort = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                // a household in a camp moves into a home that stands empty (D2-059)
                if (hh.Building == 0 && hh.Party == 0) Homes.TakeEmpty(w, hhs[i]);
                double fire = Hearth.Wanted(tOut, hh.Shelter) * share;
                hh.Indoor = (float)Hearth.Indoors(tOut, hh.Shelter, fire);
                int members = w.Members(hhs[i]).Count;
                // the day's wear of its members' clothes and tools (D2-058), slower where the tools are metal (D2-064) and for wares (C3.1)
                Goods.Wear(ref hh, members, days, Knowledge.Tech.ToolWear(in s));
                Weaving.Draw(w, ref s, ref hh, members);   // (short of goods: wares from the village's store before its own hands make more)
                held += hh.Wares;
                Pottery.Wear(ref hh, members, days);   // (kiln pots cracked on the fire: C3.2)
                TC2.Core.Diagnostics.HouseLedger.Days(w, hhs[i], sid, days);
                hearthShort += Pottery.Draw(w, ref s, ref hh, hhs[i], members);   // (a hearth that cannot fire its own takes kiln pots from the store)
                if (members > 0 && hh.Indoor < Goods.ChillLine(Goods.Condition(hh.Gear, members))) cold++;
            }
            s.WaresHeld = (float)held;   // (what its households hold of wares: the share of its people in them, `Weaving.Share`)
            Pottery.Day(w, sid, hearthShort, days);   // (ware stood in its granaries as jars; jars broken; what its hearths lack known: C3.2)
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
            w.Holdings.Inherit(hid, heir, sid);   // (its grain, debts and claims to the heir: C3.3)
            Homes.Leave(w, hid);
            Boats.Leave(w, hid, heir);   // (the heir takes the boat, else it is left on the beach: C9.1)
            if (heir != 0)
            {
                ref var to = ref w.Households.Get(heir);
                to.Larder += hh.Larder + hh.Ready;
                to.Gear = (float)(to.Gear + (double)hh.Gear);
                to.Wares = (float)(to.Wares + (double)hh.Wares);   // (C3.1)
                to.Pots = (float)(to.Pots + (double)hh.Pots);      // (C3.2)
                if (hh.CartState == Carts.Made && to.CartState != Carts.Made) { to.CartKind = hh.CartKind; to.CartState = Carts.Made; to.CartHours = hh.CartHours; to.CartAt = hh.CartAt; }   // (the cart to the heir: C4.2)
                w.Stock.Inherit(hid, heir);
            }
            else w.Stock.FeralHousehold(hid, w.Settlements.Get(sid).Cell);
            hh.Larder = 0; hh.Ready = 0; hh.Gear = 0f; hh.Wares = 0f; hh.Pots = 0f; hh.CartState = Carts.NoCart;
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

        // ---------------------------------------------------------------- the old hand on their holding (C3.6e, D2-136)
        /// <summary>The age of a household's youngest member past which it no longer works its own land (the old's half day: COARSE hours).</summary>
        public const double RetireAge = 60.0;
        /// <summary>Tools: the old keep their land and live on alone (--noretire).</summary>
        public static bool RetireOff;
        /// <summary>Tools (null in the game): retirements [0], to a child's household [1], ha handed [2], FU of grain [3], households that held land and
        /// had nobody to go to [4].</summary>
        public static double[] RetireTally;
        readonly List<int> _old = new List<int>();

        /// <summary>
        /// The yearly weighing (C3.6e): a household that holds land and whose youngest member is past working it hands it on in return for its keep -
        /// it goes to live with a child's household in the village (the one with the least land a head), else, by an agreement for its keep, with the
        /// household of the village short of land that has the most hands; its land, grain, goods and beasts go with its people (`MoveTo`, `Close`), its
        /// home stands empty (maintenance agreements: Homans 1941; E. Clark 1982; R. Smith 1984).
        /// </summary>
        public void Retire(int sid)
        {
            if (RetireOff || Holdings.LandOff) return;
            var w = _w;
            long now = w.Now;
            _old.Clear();
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                if (hh.Party != 0 || hh.Land < 0.05) continue;
                var mem = w.Members(hid);
                if (mem.Count == 0) continue;
                bool old = true;
                for (int k = 0; k < mem.Count && old; k++) if (w.People.Get(mem[k]).AgeYears(now) < RetireAge) old = false;
                if (old) _old.Add(hid);
            }
            for (int i = 0; i < _old.Count; i++)
            {
                int hid = _old[i];
                if (!w.Households.Exists(hid) || w.Households.Get(hid).Dissolved != 0 || w.Members(hid).Count == 0) continue;
                int to = Successor(sid, hid, out bool kin);
                if (to == 0) { if (RetireTally != null) RetireTally[4]++; continue; }
                var mem = w.Members(hid);
                int eldest = 0; double ea = -1;
                for (int k = 0; k < mem.Count; k++) { double a = w.People.Get(mem[k]).AgeYears(now); if (a > ea) { ea = a; eldest = mem[k]; } }
                ref var hh = ref w.Households.Get(hid);
                double land = hh.Land, grain = hh.Grain;
                w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.Retired, Time = now, Cell = w.Settlements.Get(sid).Cell, Settlement = sid, Subject = EntityRef.Household(hid), Object = EntityRef.Household(to),
                    Other = EntityRef.Person(eldest), ArgA = (int)DMath.RoundHalfUp(land), ArgB = kin ? 1 : 0, Magnitude = (float)grain, RootKind = RootKind.People,
                });
                // its people move in, the eldest last (the last out takes the land: C3.6b)
                _moving.Clear();
                for (int k = 0; k < mem.Count; k++) _moving.Add(mem[k]);
                _moving.Sort((a, b) => a == eldest ? 1 : b == eldest ? -1 : a.CompareTo(b));
                for (int k = 0; k < _moving.Count; k++) MoveTo(_moving[k], to);
                if (RetireTally != null) { RetireTally[0]++; if (kin) RetireTally[1]++; RetireTally[2] += land; RetireTally[3] += grain; }
            }
        }
        readonly List<int> _moving = new List<int>();

        /// <summary>Who takes an old household in (C3.6e): a child's household of the village with the least land a head; else the village's household
        /// short of its want with the most hands (not itself old); 0 none.</summary>
        int Successor(int sid, int hid, out bool kin)
        {
            var w = _w;
            kin = false;
            var mem = w.Members(hid);
            var hhs = w.HouseholdsOf(sid);
            int best = 0; double bestPer = double.MaxValue;
            for (int i = 0; i < hhs.Count; i++)
            {
                int o = hhs[i];
                if (o == hid || w.Households.Get(o).Party != 0) continue;
                var m2 = w.Members(o);
                if (m2.Count == 0) continue;
                bool child = false;
                for (int k = 0; k < m2.Count && !child; k++) { ref var q = ref w.People.Get(m2[k]); for (int j = 0; j < mem.Count; j++) if (q.Mother == mem[j] || q.Father == mem[j]) { child = true; break; } }
                if (!child) continue;
                double per = w.Households.Get(o).Land / m2.Count;
                if (per < bestPer) { bestPer = per; best = o; }
            }
            if (best != 0) { kin = true; return best; }
            double mostHands = 0.9;   // (at least a grown hand to work the land)
            for (int i = 0; i < hhs.Count; i++)
            {
                int o = hhs[i];
                if (o == hid) continue;
                ref var oh = ref w.Households.Get(o);
                if (oh.Party != 0 || w.Members(o).Count == 0 || oh.Land >= w.Holdings.LandWant(o) - 1e-9) continue;
                double h = w.Holdings.Hands(o);
                if (h > mostHands) { mostHands = h; best = o; }
            }
            return best;
        }

        // ---------------------------------------------------------------- the first sheaves (the hungry gap, D2-132)
        /// <summary>Tools: the whole harvest comes into the store on its last day, as before (--nosheaves).</summary>
        public static bool SheavesOff;

        /// <summary>Tools (null in the game): harvests, their grain, and the grain of them that came in as it was reaped, before the harvest's day.</summary>
        public static double[] SheafTally;

        /// <summary>
        /// The ripe stage's sheaves (D2-132): at each day's start the grain reaped so far - the reaped share of what the fields bear, less what fire
        /// took standing - comes into the store and its households' bins by their mouths, so a hungry village eats from its first sheaves rather than
        /// starving beside them. The year's debts wait for the harvest's day.
        /// </summary>
        void Sheaves(ref Settlement s)
        {
            double reaped = DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
            double add = s.CropYield * (1.0 - s.CropBurnt) * reaped - s.Reaped;
            if (add <= 1e-9) return;
            s.Granary += add; s.Reaped += add;
            Threshing.In(ref s, add);   // (in the sheaf, to be threshed: C3.7b)
            _w.Holdings.Harvest(s.Id, add, false);
        }

        // ---------------------------------------------------------------- working the land harder (C3.6, D2-131)
        /// <summary>The ordinary season's work a hectare, by stage: ploughing and sowing, weeding and keeping the crop, reaping (work-hours).</summary>
        public const double SowHa = 40.0, TendHa = 24.0, ReapHa = 45.0, SeasonHa = SowHa + TendHa + ReapHa;
        /// <summary>The most extra ploughing and hoeing can add to a hectare's yield (a second ploughing and harrowing, two more hoeings).</summary>
        public const double TilthGainMax = 0.30;
        /// <summary>Extra hours, as a share of the ordinary season's, that bring 63 % of that: returns fall with every hour (Boserup 1965).</summary>
        public const double TilthScale = 0.4;
        /// <summary>The most extra work a village plans (+80 % of the season's hours: +26 % of the yield, its last hour returning a tenth
        /// of an ordinary hour's).</summary>
        public const double TilthMax = 0.8;
        /// <summary>Tools: the village works its land as before whatever its want (--notilth).</summary>
        public static bool TilthOff;

        /// <summary>What extra work e (a share of the ordinary season's hours a hectare) adds to the yield: a share of the harvest.</summary>
        public static double GainOf(double e) => e <= 0 ? 0.0 : TilthGainMax * (1.0 - DMath.Exp(-e / TilthScale));

        /// <summary>
        /// The extra work a village plans at sowing (C3.6): none while its fields are what its people want; else enough more ploughing and
        /// hoeing a hectare to bring the harvest of the hectares it has up to what the wanted ones would give, as far as falling returns allow.
        /// </summary>
        public static double TilthFor(double fieldHa, double wantHa)
        {
            if (TilthOff || fieldHa <= 0 || wantHa <= fieldHa) return 0.0;
            double g = wantHa / fieldHa - 1.0;
            if (g >= GainOf(TilthMax)) return TilthMax;
            return -TilthScale * DMath.Log(1.0 - g / TilthGainMax);
        }

        /// <summary>The years a village works its land harder, told once (until its land within reach covers its want with a tenth to spare).</summary>
        void Tilled(ref Settlement s, double wantHa)
        {
            var w = _w;
            if (s.Tilth > 0 && s.TilthEvent == 0 && s.Population > 0)
            {
                long fam = s.FamineEvent != 0 && s.Memory > 0.3 ? s.FamineEvent : 0;   // (a village that remembers famine wants more: D2-042)
                s.TilthEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.FieldsWorkedHarder, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id),
                    Cause1 = fam, Rel1 = fam != 0 ? CauseRel.InResponseTo : CauseRel.None, Magnitude = (float)s.Tilth,
                    ArgA = (int)DMath.RoundHalfUp(s.ArableHa), ArgB = s.Population, RootKind = fam != 0 ? RootKind.Unknown : RootKind.People,
                });
            }
            else if (s.TilthEvent != 0 && s.ArableHa >= 1.1 * wantHa) s.TilthEvent = 0;
        }

        /// <summary>The first year a village rests a tenth or more of its arable is history (C3.6f); the record ends when it crops nearly all again.</summary>
        void Rested(ref Settlement s)
        {
            var w = _w;
            if (s.Rest >= 0.1f && s.RestEvent == 0 && s.Population > 0)
            {
                s.RestEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.FieldsRested, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id), Magnitude = s.Rest,
                    ArgA = (int)DMath.RoundHalfUp(100.0 * Heart(in s)), ArgB = (int)DMath.RoundHalfUp(s.ArableHa), RootKind = RootKind.People,
                });
            }
            else if (s.RestEvent != 0 && s.Rest < 0.05f) s.RestEvent = 0;
        }

        // ---------------------------------------------------------------- the soil: the fields' heart, rest and the herds' dung (C3.6f, D2-139)
        /// <summary>Tools: the fields never tire (--nosoil: every hectare bears a fresh field's crop every year, as before).</summary>
        public static bool SoilOff;
        /// <summary>Tools: the fields tire but no village rests them (--norest).</summary>
        public static bool RestOff;
        /// <summary>Tools: the herds' dung gives nothing back to the fields (--nodung).</summary>
        public static bool DungOff;
        /// <summary>What fields worn out bear, of a fresh field's crop: what rain and the soil alone give back (Broadbalk's unmanured wheat settled
        /// near half the yield of nitrogen-fed plots; old varieties and weeds less).</summary>
        public const double SoilFloor = 0.35;
        /// <summary>The tiredness a full crop leaves on the land that bore it (a year's).</summary>
        public const double SoilTake = 0.08;
        /// <summary>The share of its tiredness the land gets back in a year from rain and the soil alone (cropped every year, unmanured, fields
        /// settle near 0.45 of a fresh crop in a decade or two).</summary>
        public const double SoilBack = 0.0426;
        /// <summary>The share of its tiredness a year at rest gives back to the land resting (grass, weeds turned in, the soil's own mending).</summary>
        public const double RestBack = 0.5;
        /// <summary>The share of the fields' tiredness a year's dung gives back for each kg a day the village's herds eat off the waste a hectare of
        /// its arable (beasts make no fertility: they carry the waste's onto the fields).</summary>
        public const double DungBack = 0.01;
        /// <summary>The share of its arable a village rests for each unit of the tiredness it has seen in its crops, and the most.</summary>
        public const double RestK = 0.4, RestMax = 0.5;

        /// <summary>
        /// The hectares a head its people would sow (C3.6f): half a hectare of fresh land's crop, more where their crops have shown the land tired -
        /// they judge their land by what it bears (what they claim and give out, how short of land a household feels, where a household would go).
        /// </summary>
        public static double HaPerHead(World w, in Settlement s) => SoilOff ? w.Config.FieldHaPerPerson : w.Config.FieldHaPerPerson / (1.0 - (1.0 - SoilFloor) * s.TiredSeen);

        /// <summary>What a sown hectare of the village's fields bears, of a fresh field's crop (1 land first broken; `SoilFloor` worn).</summary>
        public static double Heart(in Settlement s) => SoilOff ? 1.0 : 1.0 - (1.0 - SoilFloor) * s.Tired;

        /// <summary>
        /// The fields' year, at the harvest (C3.6f): the crop takes from the land that bore it by what it bore (of a fresh field's crop, on the share
        /// of the arable sown); rain and the soil's own mending, the land at rest and the dung of the herds that grazed the waste give back a share of
        /// the tiredness; its people see it in their crops a few harvests late. Pure: the tests call it.
        /// </summary>
        public static void SoilYear(ref Settlement s, double yield, double freshPerHa)
        {
            double arable = s.ArableHa;
            double grazed = s.Grazed;
            s.Grazed = 0;
            if (!Muck.Off) { grazed = Muck.GrassOf(s.Spread); s.Spread = 0; }   // (only the muck carted out and spread: C3.7c)
            if (SoilOff || arable <= 0 || s.FieldHa <= 0) return;
            double sown = DMath.Clamp01(s.FieldHa / arable);
            double bore = DMath.Clamp(yield / (s.FieldHa * freshPerHa), 0.0, 1.5);
            double dung = DungOff ? 0.0 : DungBack * DMath.Min(20.0, grazed / SimTime.DaysPerYear / arable);
            double back = DMath.Min(1.0, SoilBack + RestBack * (1.0 - sown) + dung);
            s.Tired = DMath.Clamp01(s.Tired + SoilTake * bore * sown - back * s.Tired);
            s.TiredSeen = (float)(s.TiredSeen + 0.5 * (s.Tired - s.TiredSeen));
        }

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
                Stubble.Ploughed(ref s);   // (the stubble ploughed in: C3.7d)
                // a village that remembers famine sows more than it needs, as far as its land allows (D2-042)
                double perPerson = HaPerHead(w, in s) * (1.0 + 0.5 * s.Memory);   // (more where the land is seen tired: C3.6f)
                // the fields' rest (C3.6f): a share of the arable rests this year by the tiredness its people have seen in their crops, and the
                // village would hold that much more land to sow what it wants; land newly broken or cleared comes in fresh (an assart)
                s.Rest = SoilOff || RestOff ? 0f : (float)DMath.Min(RestMax, RestK * s.TiredSeen);
                double arable0 = s.ArableHa;
                ClaimFields(ref s, s.Population * perPerson / (1.0 - s.Rest));
                if (!SoilOff && s.ArableHa > arable0) s.Tired = arable0 > 0 ? s.Tired * arable0 / s.ArableHa : 0.0;
                w.Holdings.LandYear(s.Id);   // (whose the fields are: lost land from all, unheld land to the least landed - C3.6b)
                s.FieldHa = Math.Max(0.5, Math.Min(s.Population * perPerson, s.ArableHa * (1.0 - s.Rest)));
                // where the land within reach is short of that, the village works each hectare harder (C3.6): more ploughing and hoeing
                s.Tilth = TilthFor(s.FieldHa, s.Population * perPerson); s.TilthDone = 0;
                s.CropWorkNeed = s.FieldHa * (SowHa + 0.5 * s.Tilth * SeasonHa);
                s.ExpectedHarvest = s.FieldHa * cfg.FieldYieldPerHa * 0.8 * (1.0 + GainOf(s.Tilth)) * Heart(in s);
                Tilled(ref s, s.Population * perPerson);
                Rested(ref s);
                Gardens.Year(w, s.Id);   // (each household's croft measured again, its garden's year begun: C3.7a)
            }
            else if (s.Crop == CropStage.Sown && doy >= 12)
            {
                double sow = s.FieldHa * SowHa;
                s.Sown = DMath.Clamp01(s.CropWork / Math.Max(1.0, sow));
                s.TilthDone += DMath.Max(0.0, s.CropWork - sow);   // (the second ploughing and the harrowing: C3.6)
                // weeding, and keeping birds and beasts off the young crop: ~24 hours a hectare over the stage (hand weeding of a
                // cereal took 50-100 hours a hectare a season; D2-058) - and as much more hoeing as the village planned (C3.6)
                s.Crop = CropStage.Growing; s.CropWork = 0; s.CropWorkNeed = s.FieldHa * (TendHa + 0.5 * s.Tilth * SeasonHa);
            }
            else if (s.Crop == CropStage.Growing && doy >= 30)
            {
                s.TilthDone += DMath.Max(0.0, s.CropWork - s.FieldHa * TendHa);   // (the extra hoeing: C3.6)
                s.Crop = CropStage.Ripe; s.CropWork = 0; s.CropWorkNeed = s.FieldHa * ReapHa;
                // the crop is grown: what its fields bear, to be reaped over the stage (the first sheaves feed the village: D2-132)
                s.TilthGain = (float)GainOf(DMath.Min(s.Tilth, s.FieldHa > 0 ? s.TilthDone / (s.FieldHa * SeasonHa) : 0.0));
                s.CropYield = s.FieldHa * cfg.FieldYieldPerHa * DMath.Clamp01(s.CropGrowth) * s.Sown * (1.0 + s.TilthGain) * Heart(in s);
                s.Reaped = 0;
            }
            else if (s.Crop == CropStage.Ripe && doy >= HarvestDay)
            {
                s.Harvested = DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                double yield, inNow;
                if (SheavesOff)
                {
                    // what the extra ploughing and hoeing done added, never more than the village planned (C3.6)
                    double gain = GainOf(DMath.Min(s.Tilth, s.FieldHa > 0 ? s.TilthDone / (s.FieldHa * SeasonHa) : 0.0));
                    s.TilthGain = (float)gain;
                    yield = s.FieldHa * cfg.FieldYieldPerHa * DMath.Clamp01(s.CropGrowth) * s.Sown * s.Harvested * (1.0 - s.CropBurnt) * (1.0 + gain) * Heart(in s);
                    inNow = yield;
                }
                else
                {
                    // the last of it brought in (D2-132): what was reaped, less what fire took standing - sheaves already in the store are not
                    // burnt again
                    yield = DMath.Max(s.Reaped, s.CropYield * (1.0 - s.CropBurnt) * s.Harvested);
                    inNow = yield - s.Reaped;
                    s.Reaped = yield;
                }
                if (Harvests != null) { Harvests[0] += yield; Harvests[1] += s.FieldHa; Harvests[2]++; Harvests[3] += DMath.Clamp01(s.CropGrowth); Harvests[4] += s.Sown; Harvests[5] += s.Harvested; }
                if (LandTally != null) TallyLand(in s, yield);
                if (SheafTally != null) { SheafTally[0]++; SheafTally[1] += yield; SheafTally[2] += yield - inNow; }
                s.Granary += inNow;
                Threshing.In(ref s, inNow);   // (in the sheaf, to be threshed: C3.7b)
                s.LastHarvest = yield;
                SoilYear(ref s, yield, cfg.FieldYieldPerHa);   // (what the crop took from the fields, what rest, the floor and the herds' dung gave back: C3.6f)
                TC2.Core.Diagnostics.HouseLedger.Harvest(w, s.Id, yield);   // (tools: the harvest by whose work it was; null in the game)
                w.Holdings.Harvest(s.Id, inNow);   // (shared among its households by their mouths; the year's debts repaid: C3.3)
                if (Structures.RotTally != null) Structures.RotTally[2] += yield;
                // and the flax grown on a corner of the fields: fibre for clothes and cordage (D2-063)
                s.Fibre += s.FieldHa * FibrePerHa * DMath.Clamp01(s.ExpectedHarvest > 0 ? yield / s.ExpectedHarvest : 1.0);
                s.Crop = CropStage.Harvested; s.CropWorkNeed = 0; s.CropWork = 0;
                Stubble.Harvested(ref s);   // (the reaped fields thrown open to the herds until the sowing: C3.7d)
                long hev = w.Record(new HistoryEvent { Kind = HistoryKind.Harvest, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id), Magnitude = (float)yield });
                Structures.AfterHarvest(w, s.Id, hev);   // (an overfilled store wants another granary: C1.1)
                w.Conflict.Harvested(s.Id, yield);   // (a tributary pays its lord: D2-075)
                TC2.Core.Society.Retinue.Harvest(w, s.Id, hev);   // (its first household's sworn men weighed, sworn, released and fed: C10.7b)
                TC2.Core.Society.Hall.Harvest(w, s.Id, hev);   // (and its dependants, the landless it keeps: C10.11)
                if (s.ExpectedHarvest > 0 && yield < 0.5 * s.ExpectedHarvest)
                {
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.HarvestFailed, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id),
                        Cause1 = s.CropCause, Rel1 = CauseRel.CausedBy, Cause2 = hev, Rel2 = CauseRel.PartOf,
                        Magnitude = (float)(1.0 - yield / s.ExpectedHarvest), RootKind = RootKind.Nature,
                    });
                    // (session 32: every power checked) a harvest failed in a famine: the famine is told anew from its next death, the failed harvest its
                    // cause - before, a village starving already went on dying of the famine begun before (s32powers: a blight's failed harvest at a town
                    // in a long rationing, 500 deaths in two years all of a famine rooted in nature, none in the blight's tree)
                    if (s.FamineEvent != 0 && w.Now - s.FamineAt <= SimTime.Year) s.FamineEvent = 0;
                }
            }
            if (s.Crop == CropStage.Ripe && k == 0 && !SheavesOff) Sheaves(ref s);
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
                double tendHa = s.FieldHa * TendHa;   // (the ordinary weeding: the extra hoeing's gain comes at the harvest, C3.6)
                double tend = 0.75 + 0.25 * DMath.Clamp01(DMath.Min(s.CropWork, tendHa) / Math.Max(1.0, tendHa * season));
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
            int toHarvest = HarvestDay - doy; if (toHarvest <= 0) toHarvest += SimTime.DaysPerYear;
            // rationed to the next new grain (D2-132): the first sheaves, the day after the crop ripens; once it is ripe, the grain in and the crop
            // still standing are the year's bread, rationed to next year's first sheaves (rationed only to this harvest's last day, the first
            // sheaves were eaten at full meals and the next year starved; rationed to the last day all year, every year was rationed a week short)
            double inHand = s.Granary;
            int horizon = toHarvest;
            if (!SheavesOff)
            {
                horizon = ToBread(doy, s.Crop);
                if (s.Crop == CropStage.Ripe && s.CropYield > 0) inHand += DMath.Max(0.0, s.CropYield * (1.0 - s.CropBurnt) - s.Reaped);
            }
            double lasts = daily > 0 ? DMath.Clamp(inHand / (daily * (horizon + 6)), 0.45, 1.0) : 1.0;
            double keep = 6.0 + 30.0 * s.Memory;
            double prudent = daily > 0 ? DMath.Clamp(inHand / (daily * (horizon + 6 + keep)), 0.8, 1.0) : 1.0;
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
            double share = total > 0 ? Math.Min(1.0, Threshing.Threshed(in s) / total) : 1.0;   // (only grain out of the sheaf: C3.7b)
            w.Holdings.Reconcile(s.Id);   // (whose grain the store holds: what went out or came in since, by no household's hand - C3.3)
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                double want = HouseholdNeed(hid) * cover * ration - hh.Larder - hh.Ready;
                if (want <= 0) continue;
                double give = Math.Min(want, want * share);
                give = Math.Min(give, Threshing.Threshed(in s));
                give = w.Holdings.Provide(s.Id, hid, give, horizon);   // (its own grain, the common grain, its kin's spare, a loan; in hard times no more: C3.3)
                hh.Larder += give; s.Granary -= give;
                TC2.Core.Diagnostics.HouseLedger.Draw(w, hid, s.Id, give);
            }
            prof?.End(10011);
            // spoilage of stored grain (~12% a year; less kept sealed in fired pots: D2-063 - in the jars its granaries hold since C3.2)
            double rot = 0.12 / SimTime.DaysPerYear * Pottery.RotFactor(in s);
            Structures.Rot(ref s, rot, days);   // (what lies beyond the granaries' capacity rots faster: C1.1)
            if (s.Arms > 0) s.Arms *= Keep(Forge.ArmsDecay / SimTime.DaysPerYear, days);   // (arms broken, lost, rusted: C1.4)
            if (s.TradeIn > 0f) s.TradeIn = (float)((double)s.TradeIn * Keep(0.693147 / SimTime.DaysPerYear, days));   // (traders' coming, a year: C1.6)
            if (s.IronSold > 0f) s.IronSold = (float)((double)s.IronSold * Keep(0.693147 / SimTime.DaysPerYear, days));   // (iron sold, a year: C5.3b)
            if (s.WaresOut > 0f) s.WaresOut = (float)((double)s.WaresOut * Keep(0.693147 / SimTime.DaysPerYear, days));   // (wares given out to its households, a year: C3.1)
            if (s.WaresSold > 0f) s.WaresSold = (float)((double)s.WaresSold * Keep(0.693147 / SimTime.DaysPerYear, days));   // (and sold: its makers make that much more for the next buyers)
            if (s.PotsOut > 0f) s.PotsOut = (float)((double)s.PotsOut * Keep(0.693147 / SimTime.DaysPerYear, days));   // (pots its hearths took, a year: C3.2)
            if (s.PotsSold > 0f) s.PotsSold = (float)((double)s.PotsSold * Keep(0.693147 / SimTime.DaysPerYear, days));   // (and ware sold: its potters make that much more)
            // stored hides rot and are eaten by vermin (~20 % a year), fibre less (~5 %): a store holds what is used (D2-063)
            s.Hides *= days == 1 ? 1.0 - 0.20 / SimTime.DaysPerYear : Keep(0.20 / SimTime.DaysPerYear, days);
            s.Fibre *= days == 1 ? 1.0 - 0.05 / SimTime.DaysPerYear : Keep(0.05 / SimTime.DaysPerYear, days);
            s.Metal *= days == 1 ? 1.0 - Knowledge.Tech.ToolLoss / SimTime.DaysPerYear : Keep(Knowledge.Tech.ToolLoss / SimTime.DaysPerYear, days);   // tools lost, broken, worn past mending (D2-064)
            if (s.Iron > 0) s.Iron *= days == 1 ? 1.0 - Knowledge.Tech.ToolLoss / SimTime.DaysPerYear : Keep(Knowledge.Tech.ToolLoss / SimTime.DaysPerYear, days);   // (iron rusts and is lost too: C5.2b)
            if (s.IronArms > s.Arms) s.IronArms = s.Arms;   // (iron arms are lost with the rest)
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
                // (session 32: every power checked) its cause the newer of the village's empty store and its last failed harvest within the year (a
                // famine in a rationing begun years before is the failed harvest's - the rationing its second cause); else the rationing
                bool anew = s.FamineEvent == 0 && s.FamineAt > 0 && w.Now - s.FamineAt <= SimTime.Year;   // (the same famine, told anew: a harvest failed in it)
                long failed = LastFailedHarvest(sid, SimTime.Year);
                long cause = s.HungerEvent > failed ? s.HungerEvent : failed != 0 ? failed : s.RationEvent;
                long second = cause != s.RationEvent ? s.RationEvent : 0;
                s.FamineEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.Famine, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Cause1 = cause, Rel1 = CauseRel.CausedBy, Cause2 = second, Rel2 = second != 0 ? CauseRel.CausedBy : default,
                    RootKind = cause == 0 ? RootKind.Nature : RootKind.Unknown,
                });
                if (!anew) { s.FamineAt = w.Now; w.Metrics.Famines++; }   // (a famine told anew is the famine it was: its memory dates from its start)
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

        /// <summary>Tools: hunger deaths by village (sid); null in the game.</summary>
        public static double[] HungerBy;

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
                    Holdings.HungerDeath(w, sid, p.Household);   // (tools: whose household; null in the game)
                    if (GapTally != null) GapDeath(sid, in p);
                    w.Agents.Kill(ref p, FamineCause(sid), 2);
                    w.Metrics.HungerDeaths++;
                    if (HungerBy != null && sid < HungerBy.Length) HungerBy[sid]++;
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
                if (!MeansOff && !Holdings.Off)
                {
                    // a couple marries when they can keep a household of their own: what they bring of their households' grain feeds the two
                    // of them to the next harvest and a few days beyond - else her match waits (C3.5: Hajnal 1965, a household of one's own;
                    // Razi 1980, no holding no marriage; Wrigley & Schofield 1981, the preventive check)
                    int keep = 0;
                    for (int k = 0; k < _men.Count; k++)
                    {
                        int m = _men[k], at = w.People.Get(m).Home;   // (they will live in his village: its store, its harvest)
                        double bring = CoupleDays(at, pid, m);
                        if (bring < ToHarvest(at) + Holdings.Reserve) continue;
                        // a living (C3.5b, D2-140): the land they would hold and the hands they would give keep a household of two a year, else what
                        // they bring carries the shortfall for `NicheYears` (Hajnal's niche: no holding, no marriage - a little later for those with grain)
                        if (NicheOn && !Holdings.LandOff)
                        {
                            double living = CoupleLiving(at, pid, m), years = bring / SimTime.DaysPerYear;
                            if (living < 1.0 && years < (1.0 - living) * NicheYears) { if (NicheTally != null) NicheTally[12]++; continue; }
                        }
                        _men[keep++] = m;
                    }
                    _men.RemoveRange(keep, _men.Count - keep);
                    if (_men.Count == 0) { if (MarriageTally != null) MarriageTally[7]++; continue; }
                }
                int pick = _men[KeyedRng.Below(KeyedRng.Hash(seed, RngStream.Marriage, (ulong)pid, (ulong)day, 1), _men.Count)];
                if (NicheTally != null) TallyNiche(w.People.Get(pick).Home, pid, pick);   // (tools: before they move)
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
                Holdings.HungerDeath(w, sid, p.Household);   // (tools: whose household; null in the game)
                if (GapTally != null) GapDeath(sid, in p);
                w.Agents.Kill(ref p, FamineCause(sid), 2);
                w.Metrics.HungerDeaths++;
                if (HungerBy != null && sid < HungerBy.Length) HungerBy[sid]++;
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

        /// <summary>The day of the year the ripe crop is brought in (its own calendar).</summary>
        public const int HarvestDay = 39;

        /// <summary>`--nomeans`: a couple marries whatever its means (the rule before C3.5) on the same binary.</summary>
        public static bool MeansOff;

        /// <summary>
        /// What a couple would bring to a new household in village `sid`, in days of their bread: each one's mouth's share of their household's own
        /// grain and larder (C3.3: what `MoveTo` moves with them; a household of another village keeps its grain in its own store) against the two
        /// of them's needs.
        /// </summary>
        public double CoupleDays(int sid, int womanId, int manId)
        {
            double need = 0, grain = Bring(sid, womanId, ref need) + Bring(sid, manId, ref need);
            return need > 0 ? grain / need : 0.0;
        }

        // ---------------------------------------------------------------- a living: the household's niche (C3.5b)
        /// <summary>The household's niche (C3.5b, D2-140): a couple marries only when the land they would hold and the hands they would give keep them a
        /// year at the village's last harvest, or what they bring carries the shortfall for `NicheYears`. `--noniche` (false): C3.5's rule alone.</summary>
        public static bool NicheOn = true;
        /// <summary>The years of a shortfall what a couple brings must carry before they marry without a living (C3.5b).</summary>
        public static double NicheYears = 4.0;
        /// <summary>Tools (null in the game): marriages by the couple's living (of a year's bread: under a half, to three quarters, to one, one or
        /// more) times what they brought (under a year, to two, two or more) - [4 x 3]; [12] matches a living turned away.</summary>
        public static double[] NicheTally;
        int _handsSid = -1; long _handsDay = -1; double _handsAll;

        /// <summary>
        /// A couple's living in village `sid` (C3.5b), in years of their bread a year: at its last harvest, the land's half by the hectares they would
        /// hold (his family's portion, and hers if her family is of the village: land does not travel) over its arable, and the labour's half by
        /// their two hands over all its hands (C3.6c's shares). Wild food, herds and crafts are not counted: a niche is a holding or a place in
        /// the fields.
        /// </summary>
        public double CoupleLiving(int sid, int womanId, int manId)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.ArableHa <= 0 || s.LastHarvest <= 0) return 1.0;   // (a village yet to bring in its first harvest: nothing to judge by)
            long day = SimTime.FloorDiv(w.Now, SimTime.Day);
            if (_handsSid != sid || _handsDay != day)
            {
                _handsSid = sid; _handsDay = day; _handsAll = 0;
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).Party == 0) _handsAll += w.Holdings.Hands(hl[i]);
            }
            double land = 0;
            int mh = w.People.Get(manId).Household, wh = w.People.Get(womanId).Household;
            if (w.Households.Exists(mh) && w.Households.Get(mh).Settlement == sid) land += w.Holdings.PortionFor(mh);
            if (w.Households.Exists(wh) && w.Households.Get(wh).Settlement == sid && wh != mh) land += w.Holdings.PortionFor(wh);
            double hands = w.Holdings.HandOf(womanId) + w.Holdings.HandOf(manId);
            double yearly = s.LastHarvest * (Holdings.LandShare * DMath.Min(1.0, land / s.ArableHa) + (1.0 - Holdings.LandShare) * (_handsAll > 0 ? DMath.Min(1.0, hands / _handsAll) : 0.0));
            double need = (w.Agents.DailyNeed(in w.People.Get(womanId), w.Now) + w.Agents.DailyNeed(in w.People.Get(manId), w.Now)) * SimTime.DaysPerYear;
            return need > 0 ? yearly / need : 1.0;
        }

        void TallyNiche(int sid, int womanId, int manId)
        {
            var t = NicheTally;
            if (t == null || Holdings.LandOff) return;
            double living = CoupleLiving(sid, womanId, manId), years = CoupleDays(sid, womanId, manId) / SimTime.DaysPerYear;
            int a = living < 0.5 ? 0 : living < 0.75 ? 1 : living < 1.0 ? 2 : 3, b = years < 1 ? 0 : years < 2 ? 1 : 2;
            t[a * 3 + b]++;
        }

        /// <summary>Pages: the days one person's mouth's share of their household's grain and larder would keep them (their household's own village).</summary>
        public double OwnDays(int pid)
        {
            var w = _w;
            if (!w.People.Exists(pid) || !w.Households.Exists(w.People.Get(pid).Household)) return 0;
            double need = 0, grain = Bring(w.Households.Get(w.People.Get(pid).Household).Settlement, pid, ref need);
            return need > 0 ? grain / need : 0.0;
        }

        /// <summary>Whether the means of a household are what a village's matches wait for now (C3.5 on, the households' grain reckoned).</summary>
        public static bool MeansCount => !MeansOff && !Holdings.Off;

        /// <summary>The same in years of their bread (tools, pages).</summary>
        public double CoupleMeans(int sid, int womanId, int manId) => CoupleDays(sid, womanId, manId) / SimTime.DaysPerYear;

        /// <summary>
        /// A village's own day of the year at time t - the day its crop calendar, its households' year and its gardens keep: its local day (by its
        /// longitude), the southern seasons half a year on. Every use of the village's calendar goes through here (a garden kept the north's days
        /// in southern villages: D2-144).
        /// </summary>
        public static int DayOfYear(in Settlement s, long t) => DayOfYear(s.Anchor, t);

        /// <summary>The same for a place's anchor (a village being founded).</summary>
        public static int DayOfYear(Vec3d anchor, long t)
        {
            int doy = SimTime.DayOfYear(t + Planet.PlanetState.LocalOffset(anchor));
            return anchor.Y < 0 ? (doy + SimTime.DaysPerYear / 2) % SimTime.DaysPerYear : doy;
        }

        /// <summary>Days from today to a village's next new grain (its own calendar: the southern seasons half a year on): its first sheaves
        /// (D2-132; --nosheaves: its harvest's day, 48 on that day).</summary>
        public int ToHarvest(int sid)
        {
            ref var s = ref _w.Settlements.Get(sid);
            int doy = DayOfYear(in s, _w.Now);
            if (!SheavesOff) return ToBread(doy, s.Crop);
            int toHarvest = HarvestDay - doy;
            return toHarvest <= 0 ? toHarvest + SimTime.DaysPerYear : toHarvest;
        }

        /// <summary>The day of the year the first sheaves come in (the crop ripens on day 30 and the first day's reaping is in the next morning).</summary>
        public const int SheafDay = 31;

        /// <summary>Days from a day of the year to the next new grain: the first sheaves; once the crop is ripe, next year's (D2-132).</summary>
        public static int ToBread(int doy, CropStage crop)
        {
            int d = SheafDay - doy;
            if (crop == CropStage.Ripe || d <= 0) d += SimTime.DaysPerYear;
            return d;
        }

        double Bring(int sid, int pid, ref double need)
        {
            var w = _w;
            ref var p = ref w.People.Get(pid);
            double np = w.Agents.DailyNeed(in p, w.Now);
            need += np;
            if (!w.Households.Exists(p.Household)) return 0;
            ref var hh = ref w.Households.Get(p.Household);
            if (hh.Settlement != sid) return 0;
            double nh = HouseholdNeed(p.Household), food = hh.Grain + hh.Larder;   // (the larder too: a DEEP step has filled it for its days - lived days for two)
            return nh > 0 ? food * DMath.Min(1.0, np / nh) : food;
        }

        /// <summary>Tools: marriages by what the couple brought, in years of their bread: [0] under a quarter, [1] to a half, [2] to one, [3] to two,
        /// [4] two or more; [5] in villages whose sharing had contracted; [6] the sum of years (each at most 5). Null in the game.</summary>
        public static double[] MarriageTally;

        void Marry(int sid, int womanId, int manId)
        {
            var w = _w;
            int brought = MeansCount ? (int)DMath.Min(9999.0, DMath.RoundHalfUp(CoupleDays(sid, womanId, manId))) : 0;   // (days of their bread: the record tells it - C3.5)
            var mt = MarriageTally;
            if (mt != null && !Holdings.Off)
            {
                double y = CoupleMeans(sid, womanId, manId);
                mt[y < 0.25 ? 0 : y < 0.5 ? 1 : y < 1 ? 2 : y < 2 ? 3 : 4]++;
                if (Holdings.Hard(in w.Settlements.Get(sid))) mt[5]++;
                mt[6] += DMath.Min(5.0, y);
            }
            ref var s = ref w.Settlements.Get(sid);
            int hid = w.Ids.Next(EntityKind.Household);
            int manHh = w.People.Get(manId).Household;
            int fam = w.Households.Exists(manHh) ? w.Households.Get(manHh).FamilyName : manHh;
            var hh = new Household
            {
                Id = hid, Settlement = sid, Formed = w.Now, FamilyName = fam, Indoor = (float)Hearth.Comfort,
                Way = Architecture.WayAtMarriage(w, sid, hid, manHh, w.People.Get(womanId).Household),   // (the groom's house's way, copied now and then: C11.1b)
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
            long mev = w.Record(new HistoryEvent { Kind = HistoryKind.Marriage, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(womanId), Object = EntityRef.Person(manId), ArgA = brought, RootKind = RootKind.People });
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
                double needP = w.Agents.DailyNeed(in p, w.Now), needOld = HouseholdNeed(old);   // (their mouth's share of its grain comes along: C3.3)
                w.Members(old).Remove(pid);
                w.Holdings.Split(old, hid, needOld > 0 ? needP / needOld : 1.0);
                w.Holdings.Portion(old, hid);   // (and a portion of its land, as far as it can spare it: C3.6b)
                ref var oh = ref w.Households.Get(old);
                // a share of the old larder comes along
                int left = w.Members(old).Count;
                double take = left == 0 ? oh.Larder : oh.Larder / (left + 1);
                oh.Larder -= take;
                w.Households.Get(hid).Larder += take;
                // and their own clothes and tools (D2-058)
                double gear = left == 0 ? oh.Gear : (double)oh.Gear / (left + 1);
                double wares = left == 0 ? oh.Wares : (double)oh.Wares / (left + 1);   // (their share of its wares among them: C3.1)
                oh.Gear = (float)(oh.Gear - gear);
                oh.Wares = (float)DMath.Max(0.0, DMath.Min((double)oh.Gear, oh.Wares - wares));
                w.Households.Get(hid).Gear = (float)(w.Households.Get(hid).Gear + gear);
                w.Households.Get(hid).Wares = (float)(w.Households.Get(hid).Wares + wares);
                if (left == 0) { w.Households.Get(hid).Pots = (float)(w.Households.Get(hid).Pots + (double)oh.Pots); oh.Pots = 0f; }   // (the hearth's pots go with its last: C3.2)
                if (left == 0 && oh.CartState == Carts.Made && w.Households.Get(hid).CartState != Carts.Made) { ref var nh = ref w.Households.Get(hid); nh.CartKind = oh.CartKind; nh.CartState = Carts.Made; nh.CartAt = oh.CartAt; oh.CartState = Carts.NoCart; }   // (and its cart: C4.2)
                if (left == 0) w.Holdings.Close(old, hid);   // (its debts and claims go with its last: C3.3)
                if (left == 0) { Homes.Leave(w, old); Boats.Leave(w, old, hid); oh.Dissolved = w.Now; w.History.EndEntity(EntityRef.Household(old), w.Now); w.HouseholdsOf(oh.Settlement).Remove(old); w.Stock.Inherit(old, hid); }
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
