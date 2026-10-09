using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The smithy (C1.4, D2-093; 21 §3, 23 §2): a smith's workshop - a hearth with bellows, an anvil, a quench trough - raised by a village
    /// whose people can work metal (a competent smith: D2-064) and have use for it. At the smithy a smith smelts and forges twice as fast as at
    /// an open hearth at home, on three-quarters of the charcoal, and only there are **arms** forged: spear and axe heads, a kilo of metal and two
    /// hours' forging each. A village forges arms when it fears its neighbours (a palisade raised or rising, a heavy wrong held, a watch kept)
    /// - up to about one for each of its people of fighting age - from the ore it smelts, or, with none, by reforging its tools. Arms are a
    /// store of the village's (`Settlement.Arms`), broken and lost at ~3 % a year, and its fighters strike harder with them
    /// (`Conflict.Strength`). Without a smithy, smiths keep to the open hearth (D2-064) and make no arms; a smithy burnt or fallen takes the
    /// forging with it. **Iron** (C5.2b, D2-102): only a smithy holds a bloomery; a smith who knows iron smelts bog ore there - 8 kg of ore and
    /// 15 kg of charcoal a kilo of bar iron, at about a third of copper's pace - for tools (2 kg a head, quicker and longer-lasting than copper,
    /// which they replace) and for arms, which strike harder. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Forge
    {
        /// <summary>A smith at a smithy works this many times as fast as at an open hearth (bellows, a proper hearth, an anvil).</summary>
        public const double Pace = 2.0;
        /// <summary>And burns this share of the open hearth's charcoal a kilo.</summary>
        public const double FuelShare = 0.75;
        /// <summary>kg of metal in an arm (a spearhead, an axe head) and the hours of forging it takes beyond the metal's smelting.</summary>
        public const double ArmKg = 1.0, ArmHours = 2.0;
        /// <summary>Arms a threatened village wants a head (about its people of fighting age).</summary>
        public const double ArmsPerHead = 0.35;
        /// <summary>Share of its arms a village loses in a year (broken, lost, rusted).</summary>
        public const double ArmsDecay = 0.03;
        /// <summary>Loads of stone a smithy's hearth takes when the village has them (otherwise clay and timber).</summary>
        public const double HearthStone = 16.0;
        /// <summary>FU of wrong held against a neighbour that makes a village fear it enough to arm (a third of what raises a band).</summary>
        public const double FearFrom = 50.0;

        public static int Bit => 1 << Knowledge.Tech.Metal;

        /// <summary>
        /// The places at a smithy (C1.4b, D2-125): its hearth and bellows, the anvil, the bloomery's furnace - its four best smiths by craft and
        /// practice work there (`Settlement.SmithBar`, read daily). Measured before: every smith of the village who could work copper worked
        /// at it in turn, in the order its people happened to come - a median 3 hands a year, up to 355 in a year of iron for tools (24,000
        /// hours in one smithy-year), the busiest hand 6 % of a smithy's hours: no village smith, and crowds at the forge.
        /// </summary>
        public const int Places = 4;
        /// <summary>The most smithies a village keeps (C1.4b, as its weaver's sheds: C3.1b).</summary>
        public const int MaxSmithies = 4;
        /// <summary>Days of a year its smithies' places could not do the work it wanted, from which it wants another smithy (the shed's 30: C3.1b).</summary>
        public const int ShortDays = 30;
        /// <summary>Practice a smith gains an hour at the smithy or the open hearth (`Person.SkSmith`; the potter's and the weaver's rate).</summary>
        public const double SkillRate = 0.0015;
        /// <summary>Tools: the smithy's law before C1.4b on the same binary (every smith who can work copper works there; no practice in the pace);
        /// false in the game.</summary>
        public static bool CrewOff;

        /// <summary>The places a village's smithies hold: its this many best smiths have them (C1.4b).</summary>
        public static int PlacesOf(in Settlement s) => Places * (s.Smithies > 1 ? s.Smithies : 1);

        /// <summary>
        /// Hours of work the village wants at its smithies now that its stores can feed (C1.4b): copper and iron still wanted as far as its ore and
        /// charcoal go, at its best smiths' pace, and the forging of the arms it still wants. Ore or charcoal short is not a smiths' shortfall.
        /// </summary>
        public static double Backlog(in Settlement s)
        {
            double cu = Knowledge.Tech.MetalAt(s.BestMetal) * Pace, fe = Knowledge.Tech.IronAt(s.BestIron) * Pace, h = 0.0;
            if (cu > 0) h += DMath.Min(CopperWanted(in s), CopperCan(in s)) / cu;
            if (fe > 0 && s.BestIron >= Mining.Smelter) h += DMath.Min(IronWanted(in s), IronCan(in s)) / fe;
            double arms = ArmsWant(in s) - s.Arms;
            if (arms > 0 && (cu > 0 || fe > 0)) h += arms * ArmHours;
            return h;
        }

        /// <summary>
        /// The village's day (C1.4b): a day its smithies' places could not do the work it wanted - its smiths, not its ore or its charcoal, fell
        /// short (the work before them more than all its places' labour day) - is a short day.
        /// </summary>
        public static void Tally(World w, int sid, int days)
        {
            if (CrewOff) return;
            ref var s = ref w.Settlements.Get(sid);
            if (s.Smithy == 0) return;
            if (Backlog(in s) <= PlacesOf(in s) * AgentSystem.LabourDay * days) return;
            s.SmithShort = (byte)System.Math.Min(255, s.SmithShort + days);
        }

        /// <summary>
        /// The yearly weighing (the day households weigh leaving, as the weaver's sheds: C3.1b): a village whose smiths were short on `ShortDays` or
        /// more of the year's days, with more able smiths than its smithies have places, wants another smithy - four more places for its next best
        /// smiths; the count starts again.
        /// </summary>
        public static void Year(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Smithy != 0 && s.Smithies >= 1 && s.Smithies < MaxSmithies && s.SmithShort >= ShortDays && NeedsMore(in s) && AbleCount(w, sid) > PlacesOf(in s)) s.SmithMore = 1;
            s.SmithShort = 0;
        }

        /// <summary>
        /// Hours a year its smiths need once its people are equipped (C1.4b): the tools that wear out (`Tech.ToolLoss` a year: iron where it smelts
        /// iron, else copper), the iron it sold over the past year, and the arms it keeps (worn `ArmsDecay` a year) - at its best smiths' pace.
        /// </summary>
        public static double YearNeed(in Settlement s)
        {
            double cu = Knowledge.Tech.MetalAt(s.BestMetal) * Pace, fe = Knowledge.Tech.IronAt(s.BestIron) * Pace, h = 0.0;
            bool iron = fe > 0 && s.BestIron >= Mining.Smelter;
            if (iron) h += (Knowledge.Tech.IronPerHead * s.Population * Knowledge.Tech.ToolLoss + s.IronSold) / fe;
            else if (cu > 0) h += Knowledge.Tech.MetalPerHead * s.Population * Knowledge.Tech.ToolLoss / cu;
            double rate = iron && IronArms(in s) ? fe : cu;
            if (rate > 0) h += ArmsWant(in s) * ArmsDecay * (ArmKg / rate + ArmHours);
            return h;
        }

        /// <summary>
        /// Its smiths could not keep up even once its people are equipped (C1.4b; as the looms' `NeedsMore`, C3.1b): its year's need is more than
        /// its places' year. Counted on shortfall alone, nearly every village that smelted iron raised three or four smithies for the years of its
        /// first equipping (seed 4: 95 standing against 27) - work a smith does once, over some years.
        /// </summary>
        public static bool NeedsMore(in Settlement s) => YearNeed(in s) > PlacesOf(in s) * AgentSystem.LabourDay * SimTime.DaysPerYear;

        /// <summary>A village wants another smithy now (C1.4b): wanted since its last yearly weighing, a store, no walls rising.</summary>
        public static bool MoreWanted(in Settlement s) => !CrewOff && s.SmithMore != 0 && s.Smithy != 0 && s.Smithies < MaxSmithies && s.Wall != 1 && (s.Works & Construction.Granary) != 0;

        /// <summary>The village's able smiths (copper competent, 16-59).</summary>
        public static int AbleCount(World w, int sid)
        {
            var res = w.Residents(sid);
            int n = 0;
            for (int i = 0; i < res.Count; i++) if (Able(in w.People.Get(res[i]), w.Now)) n++;
            return n;
        }

        /// <summary>The smithy a smith works at: the working one nearest their home (one smithy: that one; C1.4b).</summary>
        public static int SmithyFor(World w, in Settlement s, in Person p)
        {
            if (s.Smithies <= 1) return Smithy(w, s.Id);
            double hx = s.SquareX, hy = s.SquareY;
            if (w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); hx = hh.HomeX; hy = hh.HomeY; }
            int best = 0; double bd = double.MaxValue;
            var bl = w.BuildingsOf(s.Id);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (b.Kind != Structures.Smithy || !Structures.Working(in b, w.Now)) continue;
                double dx = (double)b.X - hx, dy = (double)b.Y - hy, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = bl[i]; }
            }
            return best;
        }

        /// <summary>A smith's hand: practice quickens it, 0.8 for a new hand to 1.2 for a practised one (as the potter's and the weaver's).</summary>
        public static double Hand(in Person p) => CrewOff ? 1.0 : 0.8 + 0.4 * p.SkSmith;

        /// <summary>A person's standing among the village's smiths: their craft in copper and iron, and their practice.</summary>
        public static double Key(in Person p) => (double)p.KMetal + p.KIron + p.SkSmith;

        /// <summary>Of an age and craft for a place at the smithy (a smith who can work copper, 16-59).</summary>
        static bool Able(in Person p, long now)
        {
            if (!p.Alive || p.KMetal < Knowledge.Tech.Competent) return false;
            double age = p.AgeYears(now);
            return age >= 16 && age < 60;
        }

        /// <summary>One of the village's best smiths, who have its smithy's places: at or above its bar, ties to the lower id.</summary>
        public static bool Maker(in Settlement s, in Person p, long now)
        {
            if (!Able(in p, now)) return false;
            double k = Key(in p);
            return k > s.SmithBar || (k == s.SmithBar && p.Id <= s.SmithBarId);
        }

        /// <summary>
        /// The village's bar for a place at its smithy (daily, and when the smithy is raised): the `Places`-th best of its able smiths by `Key`,
        /// ties broken by the lower id. Fewer able smiths than places: all of them.
        /// </summary>
        public static void Bar(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Smithy == 0) return;
            var res = w.Residents(sid);
            long now = w.Now;
            double barK = double.MaxValue; int barId = int.MinValue;
            int places = PlacesOf(in s);
            for (int k = 0; k < places; k++)
            {
                double bk = 0; int bid = 0; bool found = false;
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref w.People.Get(res[i]);
                    if (!Able(in p, now)) continue;
                    double key = Key(in p); int id = res[i];
                    if (!(key < barK || (key == barK && id > barId))) continue;
                    if (!found || key > bk || (key == bk && id < bid)) { bk = key; bid = id; found = true; }
                }
                if (!found) { s.SmithBar = -1.0; s.SmithBarId = int.MaxValue; return; }
                barK = bk; barId = bid;
            }
            s.SmithBar = barK; s.SmithBarId = barId;
        }

        /// <summary>The village's working smithy (0 none).</summary>
        public static int Smithy(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Smithy != 0 && w.Buildings.Exists(s.Smithy) && Structures.Working(in w.Buildings.Get(s.Smithy), w.Now)) return s.Smithy;
            return 0;
        }

        /// <summary>A village fears its neighbours: its palisade raised or rising, a heavy wrong held, or a watch kept at its store.</summary>
        public static bool Threatened(in Settlement s) => s.Wall == 1 || s.Wall == 2 || s.FoeWorth >= FearFrom || s.WatchEvent != 0;

        /// <summary>Arms a village wants in store now (0 unless it fears its neighbours).</summary>
        public static double ArmsWant(in Settlement s) => Threatened(in s) ? ArmsPerHead * s.Population : 0.0;

        /// <summary>The village's arms are forged of iron: someone of it smelts iron and it has bog ore or knows a bed.</summary>
        public static bool IronArms(in Settlement s) => s.BestIron >= Mining.Smelter && (s.FeCell != 0 || s.FeOre > 0);

        /// <summary>kg of copper the village still wants: tools while iron does not yet serve them, and arms while they are not of iron.</summary>
        public static double CopperWanted(in Settlement s) =>
            Knowledge.Tech.CopperWanted(in s) + (IronArms(in s) ? 0.0 : DMath.Max(0.0, ArmsWant(in s) - s.Arms) * ArmKg);

        /// <summary>Share beyond its own people's toolkits a village's smiths make to sell (a few pieces ahead for whoever comes to buy).</summary>
        public const double ForSale = 0.1;

        /// <summary>kg of iron the village's smiths make up to: its people's toolkits, a few pieces to sell, and as much again as it sold in the
        /// past year (demand known from real trades only: C5.3b).</summary>
        public static double IronTarget(in Settlement s) => Knowledge.Tech.IronPerHead * s.Population * (1.0 + ForSale) + s.IronSold;

        /// <summary>kg of iron the village still wants made: up to its target, and its arms where its smiths smelt iron.</summary>
        public static double IronWanted(in Settlement s) =>
            DMath.Max(0.0, IronTarget(in s) - s.Iron) + (IronArms(in s) ? DMath.Max(0.0, ArmsWant(in s) - s.Arms) * ArmKg : 0.0);

        /// <summary>kg of iron the village's bog ore and charcoal in store will make.</summary>
        public static double IronCan(in Settlement s) => DMath.Min(Mining.IronIn(in s), Charcoal.IronFrom(in s));

        /// <summary>kg of copper the village's copper ore and charcoal in store will make at a smithy.</summary>
        public static double CopperCan(in Settlement s) => DMath.Min(Mining.MetalIn(in s), Charcoal.MetalFrom(in s, FuelShare));

        /// <summary>
        /// A village has use for a smith's hours: ore and charcoal to smelt for tools short of metal (iron, or copper while iron does not serve
        /// them), or arms wanted (from ore or tools).
        /// </summary>
        public static bool Use(World w, in Settlement s)
        {
            bool fe = s.BestIron >= Mining.Smelter && IronCan(in s) >= 0.05;
            bool cu = CopperCan(in s) >= 0.05;
            if (fe && s.Iron < IronTarget(in s)) return true;
            if (cu && Knowledge.Tech.CopperWanted(in s) > 0) return true;
            return s.Arms < ArmsWant(in s) && (fe || cu || s.Metal + s.Iron >= ArmKg);
        }

        /// <summary>A smith of this village (competent in metal) has work at its smithy now.</summary>
        public static bool Wanted(World w, in Settlement s, in Person p, out int smithy)
        {
            smithy = 0;
            if (p.KMetal < Knowledge.Tech.Competent || s.Smithy == 0) return false;
            if (!CrewOff && !Maker(in s, in p, w.Now)) return false;   // (its places are its best smiths': C1.4b)
            smithy = CrewOff ? Smithy(w, s.Id) : SmithyFor(w, in s, in p);   // (at the smithy nearest home: C1.4b)
            return smithy != 0 && Use(w, in s);
        }

        /// <summary>
        /// Hours at the smithy: arms first while the village wants them (iron where this smith smelts it, else copper from ore, else reforged
        /// from its tools), then iron for its tools, then copper for its tools while iron does not yet serve them - twice the open hearth's
        /// pace, as far as the ore and the charcoal in store go. History tells the first arms of an arming. Returns the hours used.
        /// </summary>
        public static double Work(World w, ref Person p, int sid, int smithy, double hours, int days = 1)
        {
            if (hours <= 0 || smithy == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            double rate = Knowledge.Tech.MetalPerHour(in p) * Pace * Hand(in p);   // kg of copper an hour smelted and worked (practice quickens it: C1.4b)
            double feRate = Knowledge.Tech.IronPerHour(in p) * Pace * Hand(in p);  // kg of bar iron an hour from the bloomery and the anvil
            if (rate <= 0) return 0;
            bool fe = p.KIron >= Mining.Smelter && feRate > 0 && IronCan(in s) > 1e-6;
            bool ore = CopperCan(in s) > 1e-6;
            double used = 0, feUsed = 0;
            double armsWant = ArmsWant(in s) - s.Arms;
            if (armsWant > 0 && (fe || ore || s.Metal + s.Iron >= ArmKg))
            {
                // an arm: its kilo of metal (iron smelted, copper smelted, or taken from the tools) and two hours at the anvil
                double perArm = (fe ? ArmKg / feRate : ore ? ArmKg / rate : 0.0) + ArmHours;
                double arms = DMath.Min(hours / perArm, armsWant);
                byte from;
                if (fe)
                {
                    from = 2;
                    arms = DMath.Min(arms, IronCan(in s) / ArmKg);   // (no more arms than the bog ore and the charcoal smelt)
                    Charcoal.BurntIron(ref s, arms * ArmKg);
                    Mining.SmeltIron(ref s, arms * ArmKg);
                    s.IronArms += arms;
                    w.Metrics.IronKg += arms * ArmKg;
                    feUsed += arms * perArm;
                }
                else if (ore)
                {
                    from = 1;
                    arms = DMath.Min(arms, CopperCan(in s) / ArmKg);   // (no more arms than the ore and the charcoal smelt)
                    Charcoal.Burnt(ref s, arms * ArmKg, FuelShare);
                    Mining.Smelt(ref s, arms * ArmKg);
                    w.Metrics.MetalKg += arms * ArmKg;
                }
                else
                {
                    from = 0;
                    arms = DMath.Min(arms, (s.Metal + s.Iron) / ArmKg);
                    double ironPart = DMath.Min(arms * ArmKg, s.Iron);   // (iron tools reforged into iron arms first)
                    s.Iron -= ironPart; s.IronArms += ironPart / ArmKg;
                    s.Metal = DMath.Max(0.0, s.Metal - (arms * ArmKg - ironPart));
                }
                bool first = s.Arms < 0.5;
                s.Arms += arms;
                used = arms * perArm;
                w.Metrics.ArmsMade += arms;
                if (first && s.Arms >= 0.5)
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.ArmsMade, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(p.Id), Object = EntityRef.Settlement(sid),
                        Other = new EntityRef(EntityKind.Building, smithy), ArgA = from, RootKind = RootKind.People,
                        Cause1 = s.WallEvent != 0 ? s.WallEvent : (s.FoeEvent != 0 ? s.FoeEvent : s.WatchEvent), Rel1 = CauseRel.InResponseTo,
                    });
            }
            double left = hours - used;
            if (left > 0 && fe && IronCan(in s) > 1e-6 && s.Iron < IronTarget(in s))
            {
                // iron for its tools: bog ore in the bloomery, the bloom forged to a bar
                double kg = feRate * left;
                if (days > 1)
                {
                    // (DEEP: days lived stop smelting the day the village has its iron: F8)
                    double most = DMath.Max(0.0, IronTarget(in s) - s.Iron) + kg / days;
                    if (kg > most) { left *= most / kg; kg = most; }
                }
                double can = IronCan(in s);
                if (kg > can) { left *= can / kg; kg = can; }   // (as far as the bog ore and the charcoal in store go)
                s.Iron += kg;
                Charcoal.BurntIron(ref s, kg);
                Mining.SmeltIron(ref s, kg);
                w.Metrics.IronKg += kg;
                used += left; feUsed += left;
                left = hours - used;
            }
            if (left > 0 && CopperCan(in s) > 1e-6 && Knowledge.Tech.CopperWanted(in s) > 0)
            {
                // copper for its tools while iron does not yet serve them
                double kg = rate * left;
                if (days > 1)
                {
                    // (DEEP: days lived stop smelting the day the village has its metal, as at the open hearth: F8)
                    double most = Knowledge.Tech.CopperWanted(in s) + kg / days;
                    if (kg > most) { left *= most / kg; kg = most; }
                }
                double can = CopperCan(in s);
                if (kg > can) { left *= can / kg; kg = can; }   // (as far as the ore and the charcoal in store go)
                s.Metal += kg;
                Charcoal.Burnt(ref s, kg, FuelShare);
                Mining.Smelt(ref s, kg);
                w.Metrics.MetalKg += kg;
                used += left;
            }
            ref var b = ref w.Buildings.Get(smithy);
            b.Use = (float)(b.Use + used);
            w.Metrics.SmithyHours += used;
            w.Holdings.Commission(sid, p.Household, used);   // (the village pays its smith a day's food for a day's work: C3.3b)
            TC2.Core.Diagnostics.HouseLedger.Craft(w, p.Household, used);   // (tools: whose hours the smithy's were; null in the game)
            TC2.Core.Diagnostics.CraftTally.Add(TC2.Core.Diagnostics.CraftTally.Smithy, p.Id, used, w, sid);
            p.SkSmith = (float)DMath.Min(1.0, p.SkSmith + SkillRate * used);   // (their practised hand: their role is read from it, C1.4b)
            w.Learning.Practice(ref p, Knowledge.Tech.Metal, used);
            w.Learning.Practice(ref p, Knowledge.Tech.Iron, used);   // (a copper smith who has seen bog ore may find the bloomery: C5.2b)
            return used;
        }
    }
}
