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
            smithy = Smithy(w, s.Id);
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
            double rate = Knowledge.Tech.MetalPerHour(in p) * Pace;   // kg of copper an hour smelted and worked
            double feRate = Knowledge.Tech.IronPerHour(in p) * Pace;  // kg of bar iron an hour from the bloomery and the anvil
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
            w.Learning.Practice(ref p, Knowledge.Tech.Metal, used);
            w.Learning.Practice(ref p, Knowledge.Tech.Iron, used);   // (a copper smith who has seen bog ore may find the bloomery: C5.2b)
            return used;
        }
    }
}
