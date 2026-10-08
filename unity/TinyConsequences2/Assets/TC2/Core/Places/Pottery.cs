using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The potter's kiln, the village's jars and its hearths' pots (C3.2, D2-121; 26 §2). Before, a village kept its grain in sealed pots and its
    /// households cooked in fired pots because one of their people knew how (D2-063) - also where no clay lay within reach. Now pots are made of
    /// real clay with real fire and break:
    /// - **Jars** (`Settlement.Jars`, kg of storage jars standing in its granaries and barns) keep the grain they hold sealed: it rots at `1 - Seal`
    ///   of the store's rate, the rest at the full rate (`RotFactor`). Large jars want a kiln's even fire: they come only from a kiln, made at home
    ///   or bought. They crack and break in handling (`JarBreak` a year).
    /// - **Hearths**: a household one of whose people can fire pots (`Knowledge.Tech.Competent`) where its village has clay within reach makes its
    ///   cooking pots among its goods (D2-058) and fires them in the open; one that cannot cooks in kiln pots it holds (`Household.Pots`), drawn from
    ///   its village's store (`Draw`) - or on the full wood (`CookFactor`). Kiln pots break with use (`PotDays`).
    /// - **The kiln** (`Structures.Kiln`, at the clay): raised by a village that can fire pots, with clay within reach, where the jars its store's
    ///   grain lacks would keep a potter at work for two months (`KilnFrom`). Its makers are the village's `Hands` best potters (`Settlement.PotBar`,
    ///   read daily from their skill and their practice there, `Person.SkPot`) - lived, a choice by day to the kiln; COARSE and DEEP, a labour day
    ///   there before the shared field work, less the walk (the smithy's law, D2-093). They make fired ware (`Settlement.Pots`, kg) from the clay
    ///   they dig by it and the village's wood (`WoodPerKg`; none in store, no firing) up to `Target`: the jars the store lacks, the pots its hearths
    ///   lack, a buffer a head, and what it sold lately.
    /// - **Trade**: ware is a trade good (`Trade.Pots`, kg) valued by each village at what jars would keep of its grain and pots would spare its
    ///   hearths' wood (`Worth`) - so villages with no clay buy it from those with kilns, and a kiln's makers make for the next buyers.
    /// One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Pottery
    {
        /// <summary>FU of grain a kg of storage jar keeps sealed (a jar of ~25 kg and ~100 litres holds ~75 kg of grain).</summary>
        public const double JarHolds = 4.0;
        /// <summary>Grain sealed in jars rots at 1 - Seal of the store's rate (D2-063's 0.65).</summary>
        public const double Seal = 0.35;
        /// <summary>The share of a store's jars cracked and broken in a year (they stand in the granaries; ~20 years each).</summary>
        public const double JarBreak = 0.05;
        /// <summary>Kg of kiln pots a member's share of a hearth's: a cooking pot or two, a water jar, bowls.</summary>
        public const double PerHead = 2.0;
        /// <summary>Days of use in which a hearth's kiln pots are cracked on the fire and broken (a cooking pot lasts a year or so).</summary>
        public const double PotDays = 400.0;
        /// <summary>A kiln's makers: the village's this many best potters (a master and hands).</summary>
        public const int Hands = 4;
        /// <summary>Kg of fired ware an hour of an ordinary hand at the kiln brings: clay dug by it and worked, thrown and coiled, dried, stacked and fired.</summary>
        public const double KgPerHour = 2.5;
        /// <summary>Loads (25 kg) of the village's wood a kg of ware burns in the kiln (~1.25 kg of wood: an updraught kiln).</summary>
        public const double WoodPerKg = 0.05;
        /// <summary>
        /// Kg of jars its store lacks from which a village raises a kiln: two months of a potter's work (~5,800 FU of unsealed grain) - a firing takes
        /// its crew days to load, fire, cool and draw, and a kiln is raised where there is ware for firings through a season; a hamlet fires its few
        /// pots in the open and buys its jars (at a month's work, hamlets of 25 raised kilns and gave their few grown hands' free days to them).
        /// </summary>
        public const double KilnFrom = 1440.0;
        /// <summary>Kg of ware a head its makers keep in store (for its hearths, the next jars and a few buyers) - and as much again as it sold lately.</summary>
        public const double Keep = 1.0;
        /// <summary>Kg of ware a head a village keeps back from trade.</summary>
        public const double Kept = 0.5;
        /// <summary>
        /// A kg of ware's worth to a village whose grain lies wholly unsealed, in the villages' trade values (grain 1-4 a day's food): it seals 4 FU,
        /// keeping 0.35 x 12 % of it a year for some twenty years (~3 FU of grain).
        /// </summary>
        public const double JarWorth = 2.5;
        /// <summary>And to a village none of whose hearths cook in pots: a kg spares ~2.4 loads of wood over its life, ~0.2 days' food of hands, and
        /// pots are held dear where none are to be had.</summary>
        public const double CookWorth = 0.6;
        /// <summary>A maker's practice at the kiln: skill an hour.</summary>
        public const double SkillRate = 0.0015;
        /// <summary>Labour-hours and loads of wood to raise a kiln: an updraught kiln of clay and stone, a shed to dry the ware in.</summary>
        public const double KilnHours = 120.0, KilnWood = 15.0;

        /// <summary>Tools: no kilns - pots and jars from knowing how, as before C3.2 (D2-063: the same binary, for A/B runs); false in the game.</summary>
        public static bool Off;

        public static int Bit => 1 << Knowledge.Tech.Pottery;

        /// <summary>The village's working kiln (0 none).</summary>
        public static int Kiln(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Kiln != 0 && w.Buildings.Exists(s.Kiln) && Structures.Working(in w.Buildings.Get(s.Kiln), w.Now)) return s.Kiln;
            return 0;
        }

        // ---------------------------------------------------------------- the store's jars

        /// <summary>Grain its granaries and barns hold (FU; what lies beyond them is in heaps, never in jars).</summary>
        static double Held(in Settlement s) => DMath.Min(s.Granary, Structures.StoreCapacity(in s));

        /// <summary>The share of the grain its granaries hold that its jars keep sealed.</summary>
        public static double Sealed(in Settlement s)
        {
            double g = Held(in s);
            return g > 1e-9 ? DMath.Clamp01(s.Jars * JarHolds / g) : 1.0;
        }

        /// <summary>The store's rot against the open store's (D2-063: by the village's best potter; C3.2: by the grain its jars seal).</summary>
        public static double RotFactor(in Settlement s) => Off ? Knowledge.Tech.PotteryKeeps(s.BestPottery) : 1.0 - Seal * Sealed(in s);

        /// <summary>Kg of jars the grain in its granaries lacks.</summary>
        public static double JarsWanted(in Settlement s) => DMath.Max(0.0, Held(in s) / JarHolds - s.Jars);

        // ---------------------------------------------------------------- hearths

        /// <summary>The household's best at firing pots.</summary>
        public static double Best(World w, int hid)
        {
            var mem = w.Members(hid);
            double best = 0;
            for (int i = 0; i < mem.Count; i++) { double k = w.People.Get(mem[i]).KPottery; if (k > best) best = k; }
            return best;
        }

        /// <summary>The household fires its own cooking pots: one of its people can, and its village has clay within reach.</summary>
        public static bool FiresOwn(World w, int sid, double best) => best >= Knowledge.Tech.Competent && w.Settlements.Exists(sid) && w.Learning.Clay(w.Settlements.Get(sid).Cell);

        /// <summary>
        /// The wood a household's cooking burns against an open fire's: its own fired pots (D2-063, by its best potter) where it fires its own, the kiln
        /// pots it holds for its members, or neither (C3.2). Off: by its best potter wherever it lives (before C3.2).
        /// </summary>
        public static double CookFactor(World w, int hid)
        {
            double best = Best(w, hid);
            if (Off) return Knowledge.Tech.PotteryFuel(best);
            ref var hh = ref w.Households.Get(hid);
            double own = FiresOwn(w, hh.Settlement, best) ? Knowledge.Tech.PotteryFuel(best) : 1.0;
            if (hh.Pots <= 0f) return own;
            int members = w.Members(hid).Count;
            double kiln = members > 0 ? 1.0 - 0.2 * DMath.Clamp01(hh.Pots / (PerHead * members)) : 1.0;
            return DMath.Min(own, kiln);
        }

        /// <summary>A household's kiln pots broken over a step of days of cooking.</summary>
        public static void Wear(ref Household hh, int members, int days)
        {
            if (hh.Pots <= 0f) return;
            hh.Pots = (float)DMath.Max(0.0, hh.Pots - members * PerHead / PotDays * days);
        }

        /// <summary>
        /// A household that cannot fire its own pots takes kiln pots from its village's store up to its members' share (each day, after the day's
        /// cooking wore them); returns what it still lacks (kg).
        /// </summary>
        public static double Draw(World w, ref Settlement s, ref Household hh, int hid, int members)
        {
            if (Off || members <= 0) return 0;
            double want = members * PerHead - hh.Pots;
            if (want <= 1e-9 || FiresOwn(w, s.Id, Best(w, hid))) return 0;
            double take = DMath.Min(want, s.Pots);
            if (take > 0)
            {
                s.Pots -= take;
                hh.Pots = (float)(hh.Pots + take);
                s.PotsOut = (float)(s.PotsOut + take);
                w.Metrics.PotsDrawn += take;
            }
            return want - take;
        }

        /// <summary>
        /// The village's day (after its hearths drew their pots): ware in store is stood in its granaries as jars as far as their grain lacks them;
        /// jars break; what its hearths lacked is known to its makers.
        /// </summary>
        public static void Day(World w, int sid, double hearthShort, int days)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.HearthShort = (float)hearthShort;
            if (Off) return;
            if (s.Jars > 0) s.Jars *= days == 1 ? 1.0 - JarBreak / SimTime.DaysPerYear : SettlementSystem.Keep(JarBreak / SimTime.DaysPerYear, days);
            double set = DMath.Min(JarsWanted(in s), s.Pots);
            if (set > 1e-9) { s.Pots -= set; s.Jars += set; w.Metrics.JarsSet += set; }
        }

        // ---------------------------------------------------------------- the kiln

        /// <summary>
        /// A village wants a kiln: it can fire pots, has clay within reach, a store, no walls rising, and its granaries' grain lacks jars enough to keep
        /// a potter at the kiln two months.
        /// </summary>
        public static bool KilnWanted(World w, in Settlement s)
        {
            if (Off || s.Kiln != 0 || (s.TechKnown & Bit) == 0 || s.Wall == 1 || (s.Works & Construction.Granary) == 0) return false;
            return JarsWanted(in s) >= KilnFrom && w.Learning.Clay(s.Cell);
        }

        /// <summary>Kg of ware its makers make up to: the jars its grain lacks, the pots its hearths lack, a buffer a head, what it sold lately.</summary>
        public static double Target(in Settlement s) => JarsWanted(in s) + s.HearthShort + Keep * s.Population + s.PotsSold;

        /// <summary>Kg its makers have to make now.</summary>
        public static double Short(in Settlement s) => Target(in s) - s.Pots;

        /// <summary>The makers have work: ware short of the target and wood to fire it.</summary>
        public static bool Use(in Settlement s) => Short(in s) > 1e-9 && s.Woodstore >= 10.0 * WoodPerKg;

        /// <summary>A person's standing among the village's potters: their skill and their practice at the kiln.</summary>
        public static double Key(in Person p) => (double)p.KPottery + (double)p.SkPot;

        /// <summary>Of an age and craft to work at the kiln (a competent potter of 16-59).</summary>
        static bool Able(in Person p, long now)
        {
            if (!p.Alive || p.KPottery < Knowledge.Tech.Competent) return false;
            double age = p.AgeYears(now);
            return age >= 16 && age < 60;
        }

        /// <summary>One of the village's best potters, who work at its kiln: at or above its bar, ties to the lower id.</summary>
        public static bool Maker(in Settlement s, in Person p, long now)
        {
            if (!Able(in p, now)) return false;
            double k = Key(in p);
            return k > s.PotBar || (k == s.PotBar && p.Id <= s.PotBarId);
        }

        /// <summary>A maker of this village has work at its kiln now.</summary>
        public static bool Wanted(World w, in Settlement s, in Person p, out int kiln)
        {
            kiln = 0;
            if (s.Kiln == 0 || !Use(in s) || !Maker(in s, in p, w.Now)) return false;
            kiln = Kiln(w, s.Id);
            return kiln != 0;
        }

        /// <summary>
        /// The village's bar for a place at its kiln (daily, and when the kiln is raised): the `Hands`-th best of its able potters by `Key`, ties
        /// broken by the lower id. Fewer able potters than hands: all of them.
        /// </summary>
        public static void Bar(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Kiln == 0) return;
            var res = w.Residents(sid);
            long now = w.Now;
            double barK = double.MaxValue; int barId = int.MinValue;
            for (int k = 0; k < Hands; k++)
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
                if (!found) { s.PotBar = -1.0; s.PotBarId = int.MaxValue; return; }
                barK = bk; barId = bid;
            }
            s.PotBar = barK; s.PotBarId = barId;
        }

        /// <summary>m from the village's square to its kiln (the walk out to the clay).</summary>
        public static double Dist(World w, in Settlement s, int kiln)
        {
            if (kiln == 0) return 0;
            ref var b = ref w.Buildings.Get(kiln);
            double dx = (double)b.X - s.SquareX, dy = (double)b.Y - s.SquareY;
            return DMath.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Kg an hour of this potter at the kiln brings: their skill and practice, their age and health.</summary>
        public static double Rate(in Person p, long now) => KgPerHour * (p.AgeYears(now) < 12 ? 0.5 : 1.0) * (0.5 + 0.5 * p.Health) * (1.0 + 0.6 * p.KPottery) * (0.8 + 0.4 * p.SkPot);

        /// <summary>
        /// Hours at the kiln: ware made from the clay by it and fired on the village's wood, up to what its makers have to make, as far as its wood
        /// goes; the kiln's hours and ware, the maker's practice. COARSE: the walk out to the kiln and home once a stint comes off the hours
        /// (`coarse`). Returns the hours used.
        /// </summary>
        public static double Work(World w, ref Person p, int sid, int kiln, double hours, int days = 1, bool coarse = false)
        {
            if (hours <= 0 || kiln == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            double rate = Rate(in p, w.Now) * (coarse ? Stone.AfterWalk(Dist(w, in s, kiln), w.Config.WalkSpeed) : 1.0);
            double want = Short(in s);
            if (rate <= 0 || want <= 0) return 0;
            double made = rate * hours, used = hours;
            // (up to what is short; DEEP: days lived would make a day's more as the step's draws begin)
            double most = want + (days > 1 ? made / days : 0.0);
            if (made > most) { used *= most / made; made = most; }
            double can = s.Woodstore / WoodPerKg;
            if (made > can) { used *= can / made; made = can; }   // (as far as the wood in store fires)
            if (made <= 0) return 0;
            s.Woodstore = DMath.Max(0.0, s.Woodstore - made * WoodPerKg);
            s.Pots += made;
            ref var b = ref w.Buildings.Get(kiln);
            b.Use = (float)(b.Use + used); b.Stock = (float)(b.Stock + made);
            p.SkPot = (float)DMath.Min(1.0, p.SkPot + SkillRate * used);
            w.Learning.Practice(ref p, Knowledge.Tech.Pottery, used);
            w.Metrics.KilnHours += used; w.Metrics.PotsMade += made;
            TC2.Core.Diagnostics.HouseLedger.Craft(w, p.Household, used);
            return used;
        }

        /// <summary>Wood a day's making at the kiln burns while its makers have work (the woodcutters cut for it: `SettlementSystem` fuel).</summary>
        public static double WoodDay(in Settlement s)
        {
            if (Off || s.Kiln == 0 || Short(in s) <= 0) return 0;
            return DMath.Min(Short(in s), Hands * 8.0 * KgPerHour * 1.6) * WoodPerKg;
        }

        // ---------------------------------------------------------------- where the kiln stands

        /// <summary>
        /// The kiln's site: at the clay nearest the village - the ground off the river's bank and dry, nearest the clay's middle (searched out to 120 m
        /// from it); clay within the village is worked at its edge (`Structures` AtSite). False when the village has no clay within reach.
        /// </summary>
        public static bool Site(World w, int sid, out double x, out double y)
        {
            ref var s = ref w.Settlements.Get(sid);
            var P = w.Planet;
            int c = w.Learning.ClayCell(s.Cell);
            x = s.SquareX; y = s.SquareY;
            if (c < 0) return false;
            s.SphereToLocal(P.Grid.Center[c], P.Radius, out double cx, out double cy);
            double wr = Footprints.WorkR(Structures.Kiln);
            double dx = cx - s.SquareX, dy = cy - s.SquareY, d = DMath.Sqrt(dx * dx + dy * dy);
            if (d < Structures.HomesReach(w, sid) + wr) { x = cx; y = cy; return true; }   // (within the village: laid out at its edge on the clay's side)
            for (double r = 0.0; r <= 120.0; r += 4.0)
            {
                int steps = r < 1.0 ? 1 : (int)DMath.Max(8.0, DMath.Floor(2.0 * DMath.PI * r / 5.0));
                double a0 = DMath.Atan2(-dy, -dx);   // (from the clay's middle toward the village first)
                for (int j = 0; j < steps; j++)
                {
                    int side = (j & 1) == 0 ? 1 : -1;
                    double a = a0 + side * ((j + 1) >> 1) * (2.0 * DMath.PI / steps);
                    double px = cx + r * DMath.Cos(a), py = cy + r * DMath.Sin(a);
                    if (Shore.Dry(P, s.LocalToSphere(px, py, P.Radius)) && Footprints.WorkFits(w, sid, px, py, Structures.Kiln, 0.0, false)) { x = px; y = py; return true; }
                }
            }
            x = cx; y = cy;
            return true;
        }

        // ---------------------------------------------------------------- trade

        /// <summary>A kg of ware's worth to a village: the grain its jars would keep, the wood its hearths' pots would spare (C3.2).</summary>
        public static double Worth(World w, in Settlement s)
        {
            double unsealed = 1.0 - Sealed(in s);
            // (jars are worth what they would keep: a village with a store worth sealing - a quarter of a year's food or more)
            double store = DMath.Clamp01(Held(in s) / DMath.Max(1.0, 12.0 * s.Population));
            double hearths = s.Population > 0 ? DMath.Clamp01((double)s.HearthShort / (PerHead * s.Population)) : 0.0;
            return 0.3 + JarWorth * unsealed * store + CookWorth * hearths;
        }

        /// <summary>Kg of ware a village can spare: what it holds beyond what it keeps back a head.</summary>
        public static double Spare(in Settlement s) => DMath.Max(0.0, s.Pots - Kept * s.Population);
    }
}
