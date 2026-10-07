using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Charcoal (C5.2a, D2-101; 22 §1): smelting wants a fire hotter than wood gives, so a village whose smiths smelt makes charcoal where its
    /// wood is - at a **charcoal hearth** laid out at its woodlot (a levelled floor where clamps are stacked, earthed over and fired, used for
    /// years). Its burners fell the wood there (the woods' own law: each load's mass off the forest share, the walk paid per load) and stack,
    /// cover, fire and watch it: a load of wood (25 kg) makes 5 kg of charcoal, and the clamp smoulders for three days after it is fired. The
    /// charcoal is the village's store, and every kilo of copper smelted burns 2 kg of it at an open hearth (the 10 kg of wood D2-064 counted),
    /// three-quarters of that at a smithy - none in store, no smelting. When the woods recede past the hearth, the next is laid out at the new
    /// woodlot and the old one left: charcoal follows the woods. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Charcoal
    {
        /// <summary>kg of charcoal a kg of wood makes in an earth clamp (15-25 %).</summary>
        public const double Yield = 0.2;
        /// <summary>Hours of stacking, earthing over, firing and watching a load's worth of wood in a clamp (beyond felling it).</summary>
        public const double BurnPerLoad = 0.6;
        /// <summary>kg of charcoal a kilo of copper takes at an open hearth (D2-064's fuel: 10 kg of wood).</summary>
        public const double PerKg = 2.0;
        /// <summary>kg of charcoal a village keeps ahead at most.</summary>
        public const double StoreMost = 400.0;
        /// <summary>How long a clamp smoulders once fired.</summary>
        public static readonly long Smoulder = 3 * SimTime.Day;
        /// <summary>m: a hearth this far from where the village now fells its wood is left, and the next laid out at the woods.</summary>
        public const double Move = 300.0;

        /// <summary>
        /// kg of charcoal the village wants in store (read once a day): what its smiths will burn smelting the metal its tools and arms still
        /// want, while it has ore or knows copper and someone of it can smelt; 0 without.
        /// </summary>
        public static double Want(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited || s.BestMetal < Mining.Smelter || (s.OreCell == 0 && s.Ore <= 0)) return 0;
            double metal = DMath.Max(0.0, Knowledge.Tech.MetalPerHead * s.Population - s.Metal) + DMath.Max(0.0, Forge.ArmsWant(in s) - s.Arms) * Forge.ArmKg;
            return DMath.Min(StoreMost, metal * PerKg);
        }

        /// <summary>The village's working charcoal hearth (0 none).</summary>
        public static int Site(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.CharHearth != 0 && w.Buildings.Exists(s.CharHearth) && Structures.Working(in w.Buildings.Get(s.CharHearth), w.Now)) return s.CharHearth;
            return 0;
        }

        /// <summary>The village wants charcoal burnt now: its hearth stands and its store is short of what it wants.</summary>
        public static bool Wanted(World w, in Settlement s, out int hearth)
        {
            hearth = 0;
            if (s.CharcoalWant <= 0 || s.Charcoal >= s.CharcoalWant) return false;
            hearth = Site(w, s.Id);
            return hearth != 0;
        }

        /// <summary>The hearth stands farther than `Move` from where the village fells its wood now.</summary>
        public static bool Far(World w, int sid, int hearth)
        {
            ref var s = ref w.Settlements.Get(sid);
            ref var b = ref w.Buildings.Get(hearth);
            double dx = (double)b.X - s.WoodX, dy = (double)b.Y - s.WoodY;
            return dx * dx + dy * dy > Move * Move;
        }

        /// <summary>kg of charcoal an hour of burning brings in: felling a load at the woodlot (the woods' rate) and its share of the clamp's work.</summary>
        public static double HourRate(World w, int sid, double r0, bool coarse)
        {
            double fell = Woods.HourRate(w, sid, r0, coarse);
            if (fell <= 0) return 0;
            return Woods.LoadKg * Yield / (1.0 / fell + BurnPerLoad);
        }

        /// <summary>
        /// Burning hours given at the village's hearth: wood felled from the woods (as far as they hold) and burnt into the village's charcoal
        /// store; the clamp smoulders on. A lived stint stops at what the store still wants (`most`). Returns kg of charcoal.
        /// </summary>
        public static double Burn(World w, int sid, int hearth, double hours, double r0, bool coarse, double most = double.MaxValue)
        {
            if (hours <= 0 || hearth == 0) return 0;
            double rate = HourRate(w, sid, r0, coarse);
            if (rate <= 0) return 0;
            double kg = DMath.Min(hours * rate, most);
            if (kg <= 0) return 0;
            double loads = Woods.Fell(w, sid, kg / (Woods.LoadKg * Yield));
            kg = loads * Woods.LoadKg * Yield;
            if (kg <= 0) return 0;
            hours = DMath.Min(hours, kg / rate);
            ref var s = ref w.Settlements.Get(sid);
            s.Charcoal += kg;
            if (s.ClampUntil < w.Now + Smoulder) s.ClampUntil = w.Now + Smoulder;
            ref var b = ref w.Buildings.Get(hearth);
            b.Stock = (float)(b.Stock + kg); b.Use = (float)(b.Use + hours);
            w.Metrics.CharcoalKg += kg; w.Metrics.BurnHours += hours;
            return kg;
        }

        /// <summary>kg of copper the village's charcoal will smelt, at a fire that burns `share` of the open hearth's charcoal.</summary>
        public static double MetalFrom(in Settlement s, double share) => s.Charcoal / (PerKg * share);

        /// <summary>The charcoal burnt smelting kg of copper at that fire.</summary>
        public static void Burnt(ref Settlement s, double kg, double share) { s.Charcoal = DMath.Max(0.0, s.Charcoal - kg * PerKg * share); }
    }
}
