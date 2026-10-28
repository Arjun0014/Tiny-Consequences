using TC2.Core.Agents;
using TC2.Core.Foundation;

namespace TC2.Core.Knowledge
{
    /// <summary>
    /// Techniques as knowledge carried by people (D2-062; 09). Each person holds a proficiency 0..1 for each technique; there is
    /// no tree to unlock. The young copy the best practitioner they can watch - the copy falls short by a technique's loss
    /// (alpha, its complexity) and scatters by a personal spread (beta, a Gumbel draw fixed for the person) as in Henrich's
    /// model (2004), so a complex technique holds only where enough learners watch a master and is lost in a small group; a
    /// technique is found by people practising the work it belongs to where nobody knows it, with what it needs at hand (Arthur
    /// and Polak: invention as combination; D2-063: clay, stone, an ox), and travels with whoever moves.
    /// </summary>
    public static class Tech
    {
        public const int Nets = 0, Loom = 1, Quern = 2, Pottery = 3, Ard = 4, Metal = 5, Iron = 6, Boat = 7, Count = 8;

        public struct Law
        {
            public string Name;          // for the history: "make fishing nets"
            public string Now;           // "The people of X now ..."
            public string Short;         // (session 29) the craft named in a word or two, as a page lists it: "Nets", "Plank boats"
            public double Alpha, Beta;   // a copy's loss and its spread (Henrich): a village keeps the technique with ~exp(Alpha/Beta - 0.58) learners
            public double Discover;      // chance an hour of the related work finds it where nobody knows it
        }

        public static readonly Law[] Laws =
        {
            // knotting a net: simple, kept by a handful (~4 learners); found by those who fish
            new Law { Name = "make fishing nets", Now = "make fishing nets", Short = "Nets", Alpha = 0.15, Beta = 0.08, Discover = 2e-5 },
            // the warp-weighted loom: kept by ~10 learners; found by those who spin and weave by hand
            new Law { Name = "weave on the loom", Now = "weave on the loom", Short = "The loom", Alpha = 0.20, Beta = 0.07, Discover = 4e-7 },
            // dressing the stones of a rotary quern: complex, kept only by ~80 learners; found by grinders where rock is near
            new Law { Name = "dress the stones of a rotary quern", Now = "grind on rotary querns", Short = "Querns", Alpha = 0.30, Beta = 0.06, Discover = 2e-7 },
            // firing pots of clay: kept by ~7 learners; found by cooks where rivers and lakes lay clay (D2-063)
            new Law { Name = "fire pots of clay", Now = "fire pots of clay", Short = "Pottery", Alpha = 0.18, Beta = 0.07, Discover = 1e-6 },
            // the ard drawn by oxen: kept by ~10 learners; found by those who work the fields of a household that keeps cattle (D2-063)
            new Law { Name = "plough with an ard and oxen", Now = "plough with the ard", Short = "The ard", Alpha = 0.20, Beta = 0.07, Discover = 1e-6 },
            // smelting copper and working it: complex, kept by ~40 learners; found by potters (furnaces, crucibles) who make where
            // copper ore shows near a volcano's ground (D2-064)
            new Law { Name = "smelt copper and work it", Now = "smelt and work copper", Short = "Copper", Alpha = 0.28, Beta = 0.065, Discover = 3e-7 },
            // the bloomery: smelting bog ore into a bloom and forging it to a bar - complex, kept by ~60 learners; found at a smithy by a
            // copper smith whose village has seen bog ore (C5.2b, D2-102)
            new Law { Name = "smelt iron in a bloomery and forge it", Now = "smelt and forge iron", Short = "Iron", Alpha = 0.30, Beta = 0.065, Discover = 2e-5 },
            // boats of planks: the sides of a hollowed trunk raised with planks fitted and fastened along it, until the trunk is only a keel (the
            // expanded logboat behind the northern plank boats) - kept by ~13 learners; found by woodworkers hollowing a dugout, likelier with
            // edge tools of metal (C9.2b)
            new Law { Name = "build boats of planks", Now = "build boats of planks", Short = "Plank boats", Alpha = 0.22, Beta = 0.07, Discover = 1e-4 },
        };

        /// <summary>Proficiency at which a person counts as a practitioner, and a village as knowing the technique.</summary>
        public const double Competent = 0.5;
        /// <summary>Below this a village that knew the technique has lost it.</summary>
        public const double Forgotten = 0.25;
        /// <summary>A day's copying: the young close ~1 % of the gap to their target a day (most of it in five years).</summary>
        public const double LearnRate = 0.01;
        /// <summary>What someone who first finds a technique knows of it.</summary>
        public const double FirstFound = 0.4;

        public static float Get(in Person p, int t) => t == Nets ? p.KNets : t == Loom ? p.KLoom : t == Quern ? p.KQuern : t == Pottery ? p.KPottery : t == Ard ? p.KArd : t == Metal ? p.KMetal : t == Iron ? p.KIron : p.KBoat;

        public static void Set(ref Person p, int t, double v)
        {
            float f = (float)DMath.Clamp01(v);
            if (t == Nets) p.KNets = f; else if (t == Loom) p.KLoom = f; else if (t == Quern) p.KQuern = f; else if (t == Pottery) p.KPottery = f;
            else if (t == Ard) p.KArd = f; else if (t == Metal) p.KMetal = f; else if (t == Iron) p.KIron = f; else p.KBoat = f;
        }

        /// <summary>A person's own scatter in copying a technique: a standard Gumbel draw fixed by their id (Henrich's epsilon).</summary>
        public static double Scatter(ulong seed, int pid, int t)
        {
            double u = KeyedRng.Unit(seed, RngStream.Traits, (ulong)pid, (ulong)(300 + t));
            u = DMath.Clamp(u, 1e-12, 1.0 - 1e-12);
            return -DMath.Log(-DMath.Log(u));
        }

        // ------------------------------------------------------------------ what knowing does
        /// <summary>A fisher's hours with nets they can make and mend: up to 1.6 times the catch.</summary>
        public static double NetsFactor(in Person p) => 1.0 + 0.6 * p.KNets;

        /// <summary>A maker's hours at the loom: up to 1.6 times the cloth.</summary>
        public static double LoomFactor(in Person p) => 1.0 + 0.6 * p.KLoom;

        /// <summary>Grinding on a rotary quern - a household has one when one of its people can dress the stones, and stone is near: up to 1.8 times the meal.</summary>
        public static double QuernFactor(double householdBest) => 1.0 + 0.8 * DMath.Clamp01(householdBest / 0.6);

        /// <summary>The village's store kept in sealed pots: a year's spoilage of grain down to 0.65 of it (D2-063).</summary>
        public static double PotteryKeeps(double villageBest) => 1.0 - 0.35 * DMath.Clamp01(villageBest / 0.6);

        /// <summary>Cooking in fired pots: down to 0.8 of the wood a meal burns (D2-063).</summary>
        public static double PotteryFuel(double householdBest) => 1.0 - 0.2 * DMath.Clamp01(householdBest / 0.6);

        /// <summary>Kg of metal tools a person's toolkit holds (a knife, a sickle, an axe-head, an awl: D2-064).</summary>
        public const double MetalPerHead = 0.5;

        /// <summary>Share of a village's metal tools lost, broken and worn past mending in a year (copper and iron alike: D2-064, C5.2b).</summary>
        public const double ToolLoss = 0.03;

        /// <summary>The share of its people a village's metal can equip.</summary>
        public static double MetalShare(double metalKg, int people) => people > 0 ? DMath.Clamp01(metalKg / (MetalPerHead * people)) : 0.0;

        /// <summary>The pace of hands with metal tools: up to 1.25 times (axes, sickles, hoes of copper: D2-064).</summary>
        public static double MetalPace(double share) => 1.0 + 0.25 * share;

        /// <summary>Goods wear slower where the tools are metal: down to 0.75 of the wear.</summary>
        public static double MetalWear(double share) => 1.0 - 0.25 * share;

        /// <summary>Kg of metal an hour at the furnace and the anvil brings, by the smith's proficiency (D2-064).</summary>
        public static double MetalPerHour(in Person p) => MetalAt(p.KMetal);

        /// <summary>The same by a proficiency (a village's best smith's, for the work its smithies have before them: C1.4b).</summary>
        public static double MetalAt(double k) => 0.08 * k;

        /// <summary>Kg of iron tools a person's toolkit holds (a knife, a sickle, an axe, a spade's shoe, the ard's share, nails: C5.2b).</summary>
        public const double IronPerHead = 2.0;

        /// <summary>The share of its people a village's iron can equip.</summary>
        public static double IronShare(in Places.Settlement s) => s.Population > 0 ? DMath.Clamp01(s.Iron / (IronPerHead * s.Population)) : 0.0;

        /// <summary>kg of copper tools the village still wants: what iron does not yet serve.</summary>
        public static double CopperWanted(in Places.Settlement s) => DMath.Max(0.0, MetalPerHead * s.Population * (1.0 - IronShare(in s)) - s.Metal);

        /// <summary>The share of its people with metal tools of either kind.</summary>
        public static double ToolShare(in Places.Settlement s) => DMath.Max(MetalShare(s.Metal, s.Population), IronShare(in s));

        /// <summary>The pace of hands with the village's tools: copper up to 1.25 times (D2-064), iron - harder edges - up to 1.4 times, replacing it.</summary>
        public static double ToolPace(in Places.Settlement s)
        {
            double cu = MetalShare(s.Metal, s.Population), fe = IronShare(in s);
            return 1.0 + 0.25 * cu * (1.0 - fe) + 0.4 * fe;
        }

        /// <summary>Goods wear slower with metal tools: copper down to 0.75 of the wear, iron to 0.6.</summary>
        public static double ToolWear(in Places.Settlement s)
        {
            double cu = MetalShare(s.Metal, s.Population), fe = IronShare(in s);
            return 1.0 - 0.25 * cu * (1.0 - fe) - 0.4 * fe;
        }

        /// <summary>Kg of bar iron an hour at a bloomery and the anvil, by the smith's proficiency (a smelt a day of a few kilos of bloom,
        /// half of it lost forging it to a bar; at a smithy twice this: C5.2b).</summary>
        public static double IronPerHour(in Person p) => IronAt(p.KIron);

        /// <summary>The same by a proficiency (C1.4b).</summary>
        public static double IronAt(double k) => 0.03 * k;

        // (the charcoal a kilo of metal burns: `Places.Charcoal.PerKg`, made at the village's charcoal hearth since C5.2a)

        /// <summary>A field worker of a household with an ox, who can plough with the ard: up to 1.8 times the work (D2-063).</summary>
        public static double ArdFactor(in Person p, bool ox) => ox ? 1.0 + 0.8 * p.KArd : 1.0;
    }
}
