using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Knowledge
{
    /// <summary>
    /// How techniques live in a village (D2-062): each day its young copy its best practitioner toward a target that falls short
    /// of the model by the technique's loss and scatters by their own spread; grown people learn the same way, more slowly. The village knows a technique while one of its
    /// people is competent: the day it first is, and the day its last competent one is gone, are history. Someone practising the
    /// related work where nobody knows the technique may find it (a keyed chance an hour). The same daily step at both resolutions.
    /// </summary>
    public sealed class Learning
    {
        World _w;
        public void Bind(World w) { _w = w; }

        /// <summary>The village's day of learning (SettlementSystem.Daily, after demography).</summary>
        public void Day(int sid, int days = 1)
        {
            var w = _w;
            long now = w.Now;
            ulong seed = w.Seed;
            var res = w.Residents(sid);
            for (int t = 0; t < Tech.Count; t++)
            {
                var law = Tech.Laws[t];
                // the best practitioner the village has today
                double best = 0; int bestPid = 0;
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref w.People.Get(res[i]);
                    if (!p.Alive) continue;
                    double k = Tech.Get(in p, t);
                    if (k > best) { best = k; bestPid = res[i]; }
                }
                if (best > 0.05)
                {
                    // each copies the village's best practitioner toward the copy's target - the model less the technique's loss,
                    // plus their own scatter (Henrich 2004: learners copy the most skilled; the best of many copies may surpass
                    // the master, the best of a few falls short)
                    for (int i = 0; i < res.Count; i++)
                    {
                        ref var p = ref w.People.Get(res[i]);
                        if (!p.Alive) continue;
                        double age = p.AgeYears(now);
                        if (age < 8) continue;
                        double target = DMath.Clamp01(best - law.Alpha + law.Beta * (p.Derived != 0 ? p.ScatterOf(t) : Tech.Scatter(seed, res[i], t)));   // (kept with the person: F8)
                        double had = Tech.Get(in p, t);
                        if (had >= target) continue;
                        double rate = age < 20 ? Tech.LearnRate : 0.25 * Tech.LearnRate;
                        // (DEEP: the step's days at once - the same daily copying, closed: x moves 1 - (1 - r)^days of the way)
                        Tech.Set(ref p, t, days == 1 ? had + rate * (target - had) : had + (1.0 - Places.SettlementSystem.Keep(rate, days)) * (target - had));
                    }
                }
                // the village's knowing: told when it begins and when it ends
                ref var s = ref w.Settlements.Get(sid);
                byte bit = (byte)(1 << t);
                bool knew = (s.TechKnown & bit) != 0;
                if (!knew && best >= Tech.Competent)
                {
                    s.TechKnown |= bit;
                    long kev = w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.TechniqueKnown, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                        Object = bestPid != 0 ? EntityRef.Person(bestPid) : default, ArgA = t, RootKind = RootKind.People,
                    });
                    if (t == Tech.Metal) { ref var sk = ref w.Settlements.Get(sid); sk.SmithEvent = kev; }   // (what moves its smithy: C1.4)
                    if (t == Tech.Boat) { ref var sb = ref w.Settlements.Get(sid); sb.BoatEvent = kev; }     // (what moves its boat shed: C9.2b)
                    if (t == Tech.Loom) { ref var sl = ref w.Settlements.Get(sid); sl.LoomEvent = kev; }     // (what moves its weaver's shed: C3.1)
                    if (t == Tech.Pottery) { ref var sp = ref w.Settlements.Get(sid); sp.PotEvent = kev; }   // (what moves its kiln: C3.2)
                }
                else if (knew && best < Tech.Forgotten)
                {
                    s.TechKnown = (byte)(s.TechKnown & ~bit);
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.TechniqueLost, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                        ArgA = t, RootKind = RootKind.People,
                    });
                    w.Metrics.TechniquesLost++;
                }
                SetBest(ref s, t, best);
            }
        }

        /// <summary>
        /// What a new world's founders know (worldgen): nets in most villages by the sea and some inland, the loom in some, the
        /// rotary quern in few - its grown people competent (0.55-0.9), its children learning (to 0.25). Keyed by the village.
        /// </summary>
        public static void Founders(World w, int sid)
        {
            ulong seed = w.Seed;
            ref var s = ref w.Settlements.Get(sid);
            var P = w.Planet; var g = P.Grid;
            bool coast = false;
            for (int a = 0; a < 8 && !coast; a++)
            {
                int c1 = g.Nbr8[s.Cell * 8 + a];
                if (!P.IsLand(c1)) coast = true;
                for (int b = 0; b < 8 && !coast; b++) if (!P.IsLand(g.Nbr8[c1 * 8 + b])) coast = true;
            }
            // (pots where clay is near, the ard where the founders keep cattle: D2-063)
            bool clay = w.Learning.Clay(s.Cell), cattle = w.Stock.CountOf(sid, Livestock.Cattle) > 0, ore = s.OreCell != 0;   // (the copper its founders saw: C5.1)
            double nets = coast ? 0.8 : 0.2, loom = 0.6, quern = 0.35, pottery = clay ? 0.75 : 0.2, ard = cattle ? 0.15 : 0.0, metal = ore ? 0.25 : 0.0;
            var res = w.Residents(sid);
            for (int t = 0; t < Tech.Count; t++)
            {
                double chance = t == Tech.Nets ? nets : t == Tech.Loom ? loom : t == Tech.Quern ? quern : t == Tech.Pottery ? pottery : t == Tech.Ard ? ard : t == Tech.Metal ? metal : 0.0;   // (nobody begins knowing the bloomery: C5.2b, nor boats of planks: C9.2b)
                if (KeyedRng.Unit(seed, RngStream.Knowledge, (ulong)sid, 7000UL + (ulong)t) >= chance) continue;
                double best = 0;
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref w.People.Get(res[i]);
                    double age = p.AgeYears(w.Now);
                    double u = KeyedRng.Unit(seed, RngStream.Knowledge, (ulong)res[i], 7100UL + (ulong)t);
                    double k = age >= 16 ? 0.55 + 0.35 * u : (age >= 8 ? 0.25 * u : 0.0);
                    Tech.Set(ref p, t, k);
                    if (k > best) best = k;
                }
                ref var s2 = ref w.Settlements.Get(sid);
                SetBest(ref s2, t, best);
                if (best >= Tech.Competent) s2.TechKnown |= (byte)(1 << t);
            }
        }

        static void SetBest(ref Settlement s, int t, double v)
        {
            float f = (float)v;
            if (t == Tech.Nets) s.BestNets = f; else if (t == Tech.Loom) s.BestLoom = f; else if (t == Tech.Quern) s.BestQuern = f;
            else if (t == Tech.Pottery) s.BestPottery = f; else if (t == Tech.Ard) s.BestArd = f; else if (t == Tech.Metal) s.BestMetal = f; else if (t == Tech.Iron) s.BestIron = f; else s.BestBoat = f;
        }

        public static double Best(in Settlement s, int t) =>
            t == Tech.Nets ? s.BestNets : t == Tech.Loom ? s.BestLoom : t == Tech.Quern ? s.BestQuern : t == Tech.Pottery ? s.BestPottery : t == Tech.Ard ? s.BestArd : t == Tech.Metal ? s.BestMetal : t == Tech.Iron ? s.BestIron : s.BestBoat;

        /// <summary>
        /// Hours of a person's work that belongs to a technique (fishing to nets, making and mending to the loom, grinding to the
        /// quern): where nobody in their village knows it, a keyed chance an hour (by curiosity) that they find it.
        /// </summary>
        public void Practice(ref Person p, int t, double hours)
        {
            var w = _w;
            if (hours <= 0 || p.Home == 0 || !w.Settlements.Exists(p.Home)) return;
            ref var s = ref w.Settlements.Get(p.Home);
            if (Best(in s, t) >= 0.1 || Tech.Get(in p, t) >= 0.1) return;
            if (t == Tech.Quern && !Rock(s.Cell)) return;   // a quern wants hard stone near: rock showing on steep ground
            if (t == Tech.Pottery && !Clay(s.Cell)) return; // pots want clay: where rivers and lakes lay it (D2-063)
            if (t == Tech.Ard && !w.Stock.HasOx(p.Household)) return;   // an ard wants an ox to draw it
            // copper wants ore near (D2-064), and a potter's furnace and crucible: a technique that needs a technique
            if (t == Tech.Metal && (s.OreCell == 0 || p.KPottery < Tech.Competent)) return;   // (copper its people have seen: C5.1)
            // the bloomery wants a copper smith's furnace and bellows, and bog ore its people have seen (C5.2b)
            if (t == Tech.Iron && (s.FeCell == 0 || p.KMetal < Tech.Competent)) return;
            // boats of planks want a woodworker's hand at a dugout (the only practice that calls it: Boats.Work); edge tools of metal make the
            // planks easier to split and fit (C9.2b)
            if (t == Tech.Boat && p.SkWood < BoatWright) return;
            double chance = Tech.Laws[t].Discover * hours * (0.5 + p.Curious / 255.0) * (t == Tech.Boat ? 0.3 + 0.7 * Tech.ToolShare(in s) : 1.0);
            double u = KeyedRng.Unit(w.Seed, RngStream.Knowledge, (ulong)p.Id, (ulong)w.Now, (ulong)t);
            if (u >= chance) return;
            Tech.Set(ref p, t, Tech.FirstFound);
            SetBest(ref s, t, DMath.Max(Best(in s, t), Tech.FirstFound));
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TechniqueFound, Time = w.Now, Cell = s.Cell, Settlement = p.Home, Subject = EntityRef.Person(p.Id),
                Object = EntityRef.Settlement(p.Home), ArgA = t, RootKind = RootKind.People,
            });
            w.Metrics.TechniquesFound++;
        }

        /// <summary>A woodworker's skill that can find boats of planks while hollowing a dugout (C9.2b).</summary>
        public const double BoatWright = 0.3;

        readonly System.Collections.Generic.List<int> _near = new System.Collections.Generic.List<int>();

        /// <summary>
        /// Clay near a village (D2-063): ground within 1.2 km where floods and still water have laid it over the ages - a river with a floodplain (a
        /// reach draining `ClayAreaHa` or more) or a depression (a lake's bed) - wet or dry this season. By the ground's shape since C3.2 (before, by
        /// the day's streams and lakes: a dry spell took a village's clay away and gave it back, D2-121).
        /// </summary>
        public bool Clay(int cell) => ClayCell(cell) >= 0;

        /// <summary>The clay ground nearest a cell within reach (-1 none): where a kiln is laid out (C3.2). Derived from the drainage only.</summary>
        public int ClayCell(int cell)
        {
            var w = _w; var P = w.Planet; var d = w.Drainage;
            if (d == null) return -1;
            if (_clayVer != d.Version || _clayDrain != d) { _clay.Clear(); _clayVer = d.Version; _clayDrain = d; }
            if (_clay.TryGetValue(cell, out int known)) return known;
            w.Hazards.CellsWithin(w, cell, ClayReach, _near);
            int best = -1; double bd = double.MaxValue;
            for (int i = 0; i < _near.Count; i++)
            {
                int c = _near[i];
                if (!P.IsLand(c)) continue;
                bool clay = (d.BasinOf[c] < 0 && d.AreaHa[c] >= ClayAreaHa) || (P.InBasin != null && P.InBasin[c] != 0);
                if (!clay) continue;
                double dist = Vec3d.ArcDistance(P.Grid.Center[cell], P.Grid.Center[c], P.Radius);
                if (dist < bd || (dist == bd && c < best)) { bd = dist; best = c; }
            }
            _clay[cell] = best;
            return best;
        }

        readonly System.Collections.Generic.Dictionary<int, int> _clay = new System.Collections.Generic.Dictionary<int, int>();   // (a cache by the drainage's version: the ground's shape)
        int _clayVer = -1; Planet.Drainage _clayDrain;

        /// <summary>How far a village fetches its clay (m).</summary>
        public const double ClayReach = 1200.0;

        /// <summary>Ground (ha) a reach drains from which its floods lay clay along it (a river with a floodplain: C3.2's survey - by the day's flows,
        /// 2-20 villages a world had clay; by this, about as many, every day).</summary>
        public const double ClayAreaHa = 200.0;

        /// <summary>Rock showing near a village: a cell within two rings of it standing 40 m or more above or below a neighbour.</summary>
        public bool Rock(int cell)
        {
            // (two rings on the 10 km world, ~490 m: a short walk for stone at any size - more rings on a smaller planet, D2-085)
            var P = _w.Planet;
            var pc = P.Patch(cell, 2 * P.PatchRings);
            for (int a = 0; a < pc.Length; a++) if (Steep(P, pc[a])) return true;
            return false;
        }

        static bool Steep(Planet.PlanetState P, int c)
        {
            if (!P.IsLand(c)) return false;
            for (int k = 0; k < 4; k++) if (DMath.Abs((double)P.Elev[P.Grid.Nbr4[c * 4 + k]] - P.Elev[c]) >= 40.0) return true;
            return false;
        }
    }
}
