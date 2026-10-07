using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The smithy (C1.4, D2-093; 21 §3, 23 §2): a smith's workshop - a hearth with bellows, an anvil, a quench trough - raised by a village
    /// whose people can work metal (a competent smith: D2-064) and have use for it. At the smithy a smith smelts and forges twice as fast as at
    /// an open hearth at home, on three-quarters of the fuel, and only there are **arms** forged: spear and axe heads, a kilo of metal and two
    /// hours' forging each. A village forges arms when it fears its neighbours (a palisade raised or rising, a heavy wrong held, a watch kept)
    /// - up to about one for each of its people of fighting age - from the ore it smelts, or, with no ore near, by reforging its tools. Arms
    /// are a store of the village's (`Settlement.Arms`), broken and lost at ~3 % a year, and its fighters strike harder with them
    /// (`Conflict.Strength`). Without a smithy, smiths keep to the open hearth (D2-064) and make no arms; a smithy burnt or fallen takes the
    /// forging with it. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Forge
    {
        /// <summary>A smith at a smithy works this many times as fast as at an open hearth (bellows, a proper hearth, an anvil).</summary>
        public const double Pace = 2.0;
        /// <summary>And burns this share of the open hearth's fuel a kilo.</summary>
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

        /// <summary>A village has use for a smith's hours: its tools short of metal with ore near to smelt, or arms wanted (from ore or tools).</summary>
        public static bool Use(World w, in Settlement s)
        {
            bool ore = w.Learning.Ore(s.Cell);
            if (ore && Knowledge.Tech.MetalShare(s.Metal, s.Population) < 1.0) return true;
            return s.Arms < ArmsWant(in s) && (ore || s.Metal >= ArmKg);
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
        /// Hours at the smithy: arms first while the village wants them (smelted from ore, or reforged from its tools with none near), else
        /// metal for its tools from ore - twice the open hearth's pace on three-quarters of its fuel, as far as the woodstore holds. History
        /// tells the first arms of an arming. Returns the hours used.
        /// </summary>
        public static double Work(World w, ref Person p, int sid, int smithy, double hours, int days = 1)
        {
            if (hours <= 0 || smithy == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            bool ore = w.Learning.Ore(s.Cell);
            double rate = Knowledge.Tech.MetalPerHour(in p) * Pace;   // kg an hour smelted and worked
            if (rate <= 0) return 0;
            double used = 0;
            double armsWant = ArmsWant(in s) - s.Arms;
            if (armsWant > 0 && (ore || s.Metal >= ArmKg))
            {
                // an arm: its kilo of metal (smelted, or taken from the tools) and two hours at the anvil
                double perArm = (ore ? ArmKg / rate : 0.0) + ArmHours;
                double arms = DMath.Min(hours / perArm, armsWant);
                if (!ore) arms = DMath.Min(arms, s.Metal / ArmKg);
                if (ore)
                {
                    double fuel = arms * ArmKg * Knowledge.Tech.MetalFuel * FuelShare;
                    if (s.Woodstore < fuel) { arms *= s.Woodstore / fuel; fuel = s.Woodstore; }
                    s.Woodstore -= fuel;
                    w.Metrics.MetalKg += arms * ArmKg;
                }
                else s.Metal -= arms * ArmKg;
                bool first = s.Arms < 0.5;
                s.Arms += arms;
                used = arms * perArm;
                w.Metrics.ArmsMade += arms;
                if (first && s.Arms >= 0.5)
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.ArmsMade, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(p.Id), Object = EntityRef.Settlement(sid),
                        Other = new EntityRef(EntityKind.Building, smithy), ArgA = ore ? 1 : 0, RootKind = RootKind.People,
                        Cause1 = s.WallEvent != 0 ? s.WallEvent : (s.FoeEvent != 0 ? s.FoeEvent : s.WatchEvent), Rel1 = CauseRel.InResponseTo,
                    });
            }
            double left = hours - used;
            if (left > 0 && ore && Knowledge.Tech.MetalShare(s.Metal, s.Population) < 1.0)
            {
                double kg = rate * left;
                if (days > 1)
                {
                    // (DEEP: days lived stop smelting the day the village has its metal, as at the open hearth: F8)
                    double most = DMath.Max(0.0, Knowledge.Tech.MetalPerHead * s.Population - s.Metal) + kg / days;
                    if (kg > most) { left *= most / kg; kg = most; }
                }
                double fuel = kg * Knowledge.Tech.MetalFuel * FuelShare;
                if (s.Woodstore < fuel) { kg = s.Woodstore / (Knowledge.Tech.MetalFuel * FuelShare); fuel = s.Woodstore; left = kg / rate; }
                s.Metal += kg; s.Woodstore -= fuel;
                w.Metrics.MetalKg += kg;
                used += left;
            }
            ref var b = ref w.Buildings.Get(smithy);
            b.Use = (float)(b.Use + used);
            w.Metrics.SmithyHours += used;
            w.Learning.Practice(ref p, Knowledge.Tech.Metal, used);
            return used;
        }
    }
}
