using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The weaver's shed and the village's wares (C3.1, D2-119; 26 §1). Every household makes and mends its own clothes and tools at home
    /// (D2-058) in its spare hours; what it cannot do at home is weave close cloth on a broad loom and full it, as a workshop's practised
    /// hands do. A village that knows the loom raises a **weaver's shed** (looms, a warping frame, a dye vat, tenters) where its people's
    /// wear of goods would keep several makers at its looms all year - larger places only. Its **makers** are the village's best weavers,
    /// up to the shed's looms (`Settlement.LoomBar`: the eighth best, read daily from their loom and their practice there), and they work
    /// there while the village's store of **wares** (`Settlement.Wares`, person-equivalents of goods, as `Household.Gear`) is below what it
    /// keeps - lived, a choice by day to the shed; COARSE and DEEP, a labour day there before the shared field work (the smithy's law,
    /// D2-093) - at `Pace` times a home maker's rate and faster with practice (`Person.SkCraft`), on the store's fibre only (no cloth from
    /// bark and grass). **Wares last**: close-woven, fulled cloth and a practised maker's goods wear at `WaresWear` of homespun's rate, so
    /// the same flax clothes more people. A household short of goods takes wares from the store before its own hands make more
    /// (`Draw`, counted: `Settlement.WaresOut`); its members wear the mix they hold (`Goods.Wear`). Wares are a trade good (`Trade.Wares`)
    /// valued by each village at the hours they would save its households (`Worth`) for the share of its people who wear none yet; what
    /// a village sells its makers make for the next buyers (`Settlement.WaresSold`, as the smiths' iron: C5.3b). One law for lived days
    /// and COARSE/DEEP steps.
    /// </summary>
    public static class Weaving
    {
        /// <summary>A maker at the shed works this many times as fast as at home: the broad loom, the warping frame, the work laid out.</summary>
        public const double Pace = 1.5;
        /// <summary>A shed's looms: its makers are the village's this many best weavers for each shed it has.</summary>
        public const int Looms = 8;
        /// <summary>The most sheds a village raises (C3.1b): another where its makers left its store short most of a year.</summary>
        public const int MaxSheds = 4;
        /// <summary>Days of a year (of 48) its households drained its store with fibre to weave (`Tally`), from which it wants another shed.</summary>
        public const int ShortDays = 30;
        /// <summary>Wares wear at this share of homespun's rate: woven close on the broad loom, fulled, finished by practised hands.</summary>
        public const double WaresWear = 0.6;
        /// <summary>A village raises a shed where its people's wear of goods would keep this many makers at its looms all year.</summary>
        public const double MakersFrom = 6.0;
        /// <summary>Wares a head its makers keep in store (a buffer and a few for buyers) - and as much again as it sold lately.</summary>
        public const double Keep = 0.15;
        /// <summary>Wares a head a village keeps back from trade.</summary>
        public const double Kept = 0.1;
        /// <summary>
        /// A ware's worth to a village none of whose people wear wares yet, in the villages' trade values (grain is 1-4 a day's food): over
        /// its life a ware does the work of 1 / WaresWear homespun goods, ~300 hours of a household's making at home (250 h each, at the
        /// loom's 1.4); an hour of a household's spare hands brings ~0.09 days' food from the wild (the marginal product of labour in these
        /// worlds, `run` report "wild") - so a ware saves ~27 days' food of hours.
        /// </summary>
        public const double Worth = 25.0;
        /// <summary>A maker's practice at the shed: skill an hour (a few hundred days at the loom make a master's hand).</summary>
        public const double SkillRate = 0.0015;
        /// <summary>Labour-hours and loads of wood to raise a shed (a long timber shed with wide bays for light, ~10 x 5 m).</summary>
        public const double ShedHours = 220.0, ShedWood = 35.0;

        public static int Bit => 1 << Knowledge.Tech.Loom;

        /// <summary>Tools: no weaver's sheds (the same binary as before C3.1, for A/B runs); false in the game.</summary>
        public static bool Off;

        /// <summary>The looms a village's sheds hold: its makers are its this many best weavers (C3.1b).</summary>
        public static int LoomsOf(in Settlement s) => Looms * (s.Sheds > 1 ? s.Sheds : 1);

        /// <summary>The village's (first) working weaver's shed (0 none).</summary>
        public static int Shed(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Workshop != 0 && w.Buildings.Exists(s.Workshop) && Structures.Working(in w.Buildings.Get(s.Workshop), w.Now)) return s.Workshop;
            return 0;
        }

        /// <summary>Goods a day the village's people wear through, as homespun (D2-058; slower with metal tools, D2-064).</summary>
        public static double Wear(in Settlement s) => s.Population * Goods.WearPerDay * Knowledge.Tech.ToolWear(in s);

        /// <summary>Wares a maker's labour day at the shed makes, at the village's best weaver's loom.</summary>
        public static double MakerDay(in Settlement s) => AgentSystem.LabourDay * Pace * (1.0 + 0.6 * s.BestLoom) / Goods.MakeHours;

        /// <summary>
        /// A village wants a weaver's shed: it knows the loom, its people's wear of goods would keep `MakersFrom` makers at its looms, it has
        /// water and a store, and no walls are rising.
        /// </summary>
        public static bool ShedWanted(World w, in Settlement s)
        {
            if (Off || s.Workshop != 0 || (s.TechKnown & Bit) == 0 || s.Wall == 1 || (s.Works & Construction.Granary) == 0) return false;
            return Wear(in s) >= MakersFrom * MakerDay(in s);
        }

        /// <summary>Wares the village's makers make up to: a buffer a head, and as many again as it sold lately (demand known from real sales).</summary>
        public static double Target(in Settlement s) => Keep * s.Population + s.WaresSold;

        /// <summary>The makers have work: the store below its target and fibre to weave.</summary>
        public static bool Use(in Settlement s) => s.Wares < Target(in s) - 1e-9 && s.Fibre >= 0.05 * Goods.MaterialPerGood;

        /// <summary>A person's standing among the village's weavers: their loom and their practice at the shed.</summary>
        public static double Key(in Person p) => (double)p.KLoom + (double)p.SkCraft;

        /// <summary>Of an age and craft to have a loom at the shed (a competent weaver of 16-59).</summary>
        static bool Able(in Person p, long now)
        {
            if (!p.Alive || p.KLoom < Knowledge.Tech.Competent) return false;
            double age = p.AgeYears(now);
            return age >= 16 && age < 60;
        }

        /// <summary>One of the village's best weavers, who has a loom at its shed: at or above its bar (`Bar`), ties to the lower id.</summary>
        public static bool Maker(in Settlement s, in Person p, long now)
        {
            if (!Able(in p, now)) return false;
            double k = Key(in p);
            return k > s.LoomBar || (k == s.LoomBar && p.Id <= s.LoomBarId);
        }

        /// <summary>A maker of this village has work at its sheds now (`shed`: the one they work at).</summary>
        public static bool Wanted(World w, in Settlement s, in Person p, out int shed)
        {
            shed = 0;
            if (s.Workshop == 0 || !Use(in s) || !Maker(in s, in p, w.Now)) return false;
            shed = ShedFor(w, in s, in p);
            return shed != 0;
        }

        /// <summary>The shed a maker works at: the working one nearest their home (one shed: that one; C3.1b).</summary>
        public static int ShedFor(World w, in Settlement s, in Person p)
        {
            if (s.Sheds <= 1) return Shed(w, s.Id);
            double hx = s.SquareX, hy = s.SquareY;
            if (w.Households.Exists(p.Household)) { ref var hh = ref w.Households.Get(p.Household); hx = hh.HomeX; hy = hh.HomeY; }
            int best = 0; double bd = double.MaxValue;
            var bl = w.BuildingsOf(s.Id);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (b.Kind != Structures.Workshop || !Structures.Working(in b, w.Now)) continue;
                double dx = (double)b.X - hx, dy = (double)b.Y - hy, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = bl[i]; }
            }
            return best;
        }

        /// <summary>
        /// The village's day (C3.1b): a day its own households had drawn its store below half what it keeps back from trade (`Kept`) while it held
        /// fibre for a day's work at every loom is a day short - its looms, not its flax, could not keep up with its people. (Traders take only what
        /// lies above `Kept`: a store drained to it by sales says nothing of the looms - counted so, every village that sold wanted more sheds.)
        /// </summary>
        public static void Tally(World w, int sid, int days)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Workshop == 0 || s.Wares >= 0.5 * Kept * s.Population) return;
            if (s.Fibre < LoomsOf(in s) * MakerDay(in s) * Goods.MaterialPerGood) return;
            s.ShedShort = (byte)System.Math.Min(255, s.ShedShort + days);
        }

        /// <summary>
        /// The yearly weighing (the day households weigh leaving, C3.1b): a village whose makers were short on `ShortDays` or more of the year's days,
        /// and whose people wear through more wares than its looms make even once all wear wares (`NeedsMore`: not only while their homespun is
        /// still wearing out - counted on shortfall alone, villages of 150-270 raised a second shed as they changed over), wants another shed - eight
        /// more looms for its next best weavers - as an overfilled store wants another granary; the count starts again.
        /// </summary>
        public static void Year(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Workshop != 0 && s.Sheds >= 1 && s.Sheds < MaxSheds && s.ShedShort >= ShortDays && NeedsMore(in s)) s.ShedMore = 1;
            s.ShedShort = 0;
        }

        /// <summary>Its people, all in wares, would wear through more a day than its looms make (C3.1b: ~500 people for a second shed, ~730 a third).</summary>
        public static bool NeedsMore(in Settlement s) => Wear(in s) * WaresWear > LoomsOf(in s) * MakerDay(in s);

        /// <summary>A village wants another shed now (C3.1b): wanted since its last yearly weighing, water and a store, no walls rising.</summary>
        public static bool MoreWanted(in Settlement s) => !Off && s.ShedMore != 0 && s.Workshop != 0 && s.Sheds < MaxSheds && s.Wall != 1 && (s.Works & Construction.Granary) != 0;

        /// <summary>
        /// The village's bar for a loom at its shed (daily, and when the shed is raised): the `Looms`-th best of its able weavers by `Key`, ties
        /// broken by the lower id - so its makers are the same people from day to day, those who practise there keeping their places, and the
        /// next best taking a loom when one dies or leaves. Fewer able weavers than looms: all of them.
        /// </summary>
        public static void Bar(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Workshop == 0) return;
            var res = w.Residents(sid);
            long now = w.Now;
            double barK = double.MaxValue; int barId = int.MinValue;
            int looms = LoomsOf(in s);
            for (int k = 0; k < looms; k++)
            {
                // the next in order below the last taken: the highest key, then the lowest id
                double bk = 0; int bid = 0; bool found = false;
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref w.People.Get(res[i]);
                    if (!Able(in p, now)) continue;
                    double key = Key(in p); int id = res[i];
                    if (!(key < barK || (key == barK && id > barId))) continue;
                    if (!found || key > bk || (key == bk && id < bid)) { bk = key; bid = id; found = true; }
                }
                if (!found) { s.LoomBar = -1.0; s.LoomBarId = int.MaxValue; return; }
                barK = bk; barId = bid;
            }
            s.LoomBar = barK; s.LoomBarId = barId;
        }

        /// <summary>
        /// Hours at the shed: wares woven and finished at `Pace` times the maker's home rate, faster with their practice there, up to the
        /// store's target, as far as the store's fibre goes (2.5 kg a ware); the shed's hours and wares, the maker's skill. Returns the hours
        /// used.
        /// </summary>
        public static double Work(World w, ref Person p, int sid, int shed, double hours, int days = 1)
        {
            if (hours <= 0 || shed == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            double rate = Goods.MakePerHour(p.AgeYears(w.Now), p.Health) * Knowledge.Tech.LoomFactor(in p) * Pace * (0.8 + 0.4 * p.SkCraft);
            double want = Target(in s) - s.Wares;
            if (rate <= 0 || want <= 0) return 0;
            double made = rate * hours, used = hours;
            // (up to the target; DEEP: days lived would make a day's more as the step's draws begin)
            double most = want + (days > 1 ? made / days : 0.0);
            if (made > most) { used *= most / made; made = most; }
            double can = s.Fibre / Goods.MaterialPerGood;
            if (made > can) { used *= can / made; made = can; }   // (as far as the fibre in store goes)
            if (made <= 0) return 0;
            s.Fibre = DMath.Max(0.0, s.Fibre - made * Goods.MaterialPerGood);
            s.Wares += made;
            ref var b = ref w.Buildings.Get(shed);
            b.Use = (float)(b.Use + used); b.Stock = (float)(b.Stock + made);
            p.SkCraft = (float)DMath.Min(1.0, p.SkCraft + SkillRate * used);
            w.Learning.Practice(ref p, Knowledge.Tech.Loom, used);
            w.Metrics.ShedHours += used; w.Metrics.WaresMade += made;
            return used;
        }

        /// <summary>
        /// A household short of goods takes wares from the village's store before its own hands make more (each day, after the day's wear):
        /// up to what it keeps its members in; counted as the village's wares given out.
        /// </summary>
        public static void Draw(World w, ref Settlement s, ref Household hh, int members)
        {
            if (s.Wares <= 1e-9 || members <= 0) return;
            double want = members * Goods.Target - hh.Gear;
            if (want <= 1e-9) return;
            double take = DMath.Min(want, s.Wares);
            s.Wares -= take;
            hh.Gear = (float)(hh.Gear + take); hh.Wares = (float)(hh.Wares + take);
            s.WaresOut = (float)(s.WaresOut + take);
            w.Metrics.WaresDrawn += take;
        }

        /// <summary>The share of the village's people its wares - its households' and its store's - would keep in wares.</summary>
        public static double Share(in Settlement s) => s.Population > 0 ? DMath.Clamp01(((double)s.WaresHeld + s.Wares) / (Goods.Target * s.Population)) : 1.0;
    }
}
