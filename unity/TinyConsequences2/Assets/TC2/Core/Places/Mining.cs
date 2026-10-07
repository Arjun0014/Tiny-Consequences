using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>An ore body in the ground (C5.1): where it is, what it is, how much is left - kept once someone has seen it.</summary>
    public struct Deposit
    {
        public int Id, Cell;
        public byte Kind;          // Mining.Copper (iron with C5.2)
        public double Ore, Taken;  // kg of ore left in the ground; kg dug out
        public long Found;         // when it was first seen
        public int By;             // the village that first saw it
        public long GaveOut;       // when the last of it was dug (0 while ore is left)

        public void Write(BinWriter w) { w.Int(Id); w.Int(Cell); w.Byte(Kind); w.Double(Ore); w.Double(Taken); w.Long(Found); w.Int(By); w.Long(GaveOut); }
        public static Deposit Read(BinReader r) => new Deposit { Id = r.Int(), Cell = r.Int(), Kind = r.Byte(), Ore = r.Double(), Taken = r.Double(), Found = r.Long(), By = r.Int(), GaveOut = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Cell); h.Add(Kind); h.Add(Ore); h.Add(Taken); h.Add(Found); h.Add(By); h.Add(GaveOut); }
    }

    /// <summary>
    /// The ore bodies people have seen (C5.1): a deposit is laid by the planet's geology wherever its rule holds, but it enters the world's
    /// state only when someone first sees it - from then its ore is counted down by what is dug, whoever digs it (two villages working one
    /// body share it). Ids ascend in the order they were seen; the cell index is a lookup, never iterated.
    /// </summary>
    public sealed class Deposits
    {
        Deposit[] _rows = new Deposit[16];
        int _n;
        readonly Dictionary<long, int> _at = new Dictionary<long, int>();   // cell * 8 + kind -> row

        public int Count => _n;
        static long Key(int cell, byte kind) => (long)cell * 8 + kind;

        /// <summary>The deposit of a kind at a cell (-1 none kept).</summary>
        public int IndexAt(int cell, byte kind) => _at.TryGetValue(Key(cell, kind), out int i) ? i : -1;
        public ref Deposit At(int index) => ref _rows[index];
        public Deposit Get(int index) => _rows[index];

        public int Add(in Deposit d)
        {
            if (_n == _rows.Length) System.Array.Resize(ref _rows, _n * 2);
            _rows[_n] = d;
            _at[Key(d.Cell, d.Kind)] = _n;
            return _n++;
        }

        public void Write(BinWriter w) { w.Int(_n); for (int i = 0; i < _n; i++) _rows[i].Write(w); }
        public void Read(BinReader r)
        {
            _n = 0; _at.Clear();
            int n = r.Int();
            for (int i = 0; i < n; i++) Add(Deposit.Read(r));
        }
        public void Hash(ref StateHasher h) { h.Add(_n); for (int i = 0; i < _n; i++) _rows[i].Hash(ref h); }
    }

    /// <summary>
    /// Ore from the ground (C5.1, D2-100; 22 §1-§2): copper shows where rock breaks through the land (the quarry's outcrop, 80 m from a
    /// land neighbour) near a volcanic arc - a volcano, living or gone quiet, within 3 km - and only on some such crags (half), each body
    /// holding 1-20 t of workable ore (the oxidised cap of a vein: green and blue stone). A village knows copper only when one of its
    /// people has seen it (the grounds they go to, the woodlot, the land the founders looked over) and keeps the nearest body it knows
    /// within 3 km that still has ore. A village whose people can smelt (one who works copper) and whose tools or arms are short of
    /// metal opens a **mine** at the body (a pit with a windlass and a shelter: a structure at the site) and digs: ~a load (25 kg) of ore
    /// an hour, every load carried home - the walk is most of the work at a far body. Smelting at the hearth or the smithy takes
    /// 5 kg of ore for a kilo of copper (D2-064's rates and fuel stand); no ore in store, no metal - copper is never made from nothing.
    /// The body is counted down by what is dug, whoever digs it; when the last is dug it **gives out**: history, the mine is left,
    /// the villages that knew it forget it and look for another. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Mining
    {
        public const byte Copper = 1;
        /// <summary>m: how far from a volcano copper shows (geo-m: the arc's ground, D2-085; D2-064's rule).</summary>
        public const double ArcReach = 2000.0;
        /// <summary>Share of the crags near an arc that carry a copper body.</summary>
        public const double CopperShare = 0.15;
        /// <summary>kg of workable ore a body holds: from this to that, evenly in its logarithm (keyed by its cell).</summary>
        public const double OreMin = 1000.0, OreMax = 20000.0;
        /// <summary>m: the farthest copper a village digs from home (worth a longer walk than stone).</summary>
        public const double Reach = 2000.0;
        /// <summary>m: how far round their site the founders of the first villages looked over the land.</summary>
        public const double FoundersLook = 1000.0;
        /// <summary>kg of ore dug an hour at the face by a fit grown person (a load: picks, fire-setting, sorting).</summary>
        public const double DigPerHour = 25.0;
        /// <summary>kg of ore a kilo of copper takes (a rich oxidised ore, smelted in a crucible or a small furnace).</summary>
        public const double OrePerKg = 5.0;
        /// <summary>kg of ore a village keeps ahead at most (what its smiths will smelt in a season or two).</summary>
        public const double StoreMost = 1500.0;

        // ---------------------------------------------------------------- the ground

        /// <summary>A copper body lies at this cell (the geology's rule; whether anyone has seen it is another matter).</summary>
        public static bool CopperAt(World w, int c)
        {
            var P = w.Planet;
            if (!Stone.Outcrop(P, c) || !Shore.Dry(P, P.Grid.Center[c])) return false;
            if (KeyedRng.Unit(w.Seed, RngStream.Geology, (ulong)c, 1UL) >= CopperShare) return false;
            Vec3d at = P.Grid.Center[c];
            for (int i = 0; i < P.Vents.Count; i++) if (Vec3d.ArcDistance(P.Vents[i].At, at, PlanetState.GeoRadius) <= ArcReach) return true;
            return false;
        }

        /// <summary>kg of ore the body at a cell held when first seen (keyed by the cell: the same body whoever finds it, whenever).</summary>
        public static double OreOf(World w, int c)
        {
            double u = KeyedRng.Unit(w.Seed, RngStream.Geology, (ulong)c, 2UL);
            return DMath.Exp(DMath.Log(OreMin) + u * (DMath.Log(OreMax) - DMath.Log(OreMin)));
        }

        /// <summary>The kept deposit at a cell, made the first time anyone sees it (-1 when no copper lies there or it gave out).</summary>
        static int Seen(World w, int sid, int c)
        {
            int i = w.Deposits.IndexAt(c, Copper);
            if (i >= 0) return w.Deposits.Get(i).GaveOut == 0 ? i : -1;
            if (!CopperAt(w, c)) return -1;
            return w.Deposits.Add(new Deposit { Id = w.Deposits.Count + 1, Cell = c, Kind = Copper, Ore = OreOf(w, c), Found = w.Now, By = sid });
        }

        // ---------------------------------------------------------------- knowing it

        /// <summary>The copper body the village knows (-1 none).</summary>
        public static int Known(in Settlement s) => s.OreCell - 1;

        /// <summary>
        /// Someone of a village (0: its people at large) was at a cell and saw what shows there or beside it: copper within reach that is
        /// nearer than the body the village knows is kept; the first is history.
        /// </summary>
        public static void Saw(World w, int sid, int pid, int cell)
        {
            if (cell < 0 || !w.Settlements.Exists(sid)) return;
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            int best = -1; double bd = s.OreCell > 0 ? s.OreDist : Reach + 1;
            for (int k = -1; k < 8; k++)
            {
                int c = k < 0 ? cell : P.Grid.Nbr8[cell * 8 + k];
                if (c + 1 == s.OreCell || !Stone.Outcrop(P, c)) continue;
                double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                if (d >= bd - 1e-6) continue;
                if (Seen(w, sid, c) < 0) continue;
                bd = d; best = c;
            }
            if (best < 0) return;
            Know(w, sid, pid, best, bd);
        }

        static void Know(World w, int sid, int pid, int cell, double dist)
        {
            ref var s = ref w.Settlements.Get(sid);
            bool first = s.OreCell == 0;   // (copper known again after a body gave out is history too; a nearer one found is not)
            s.OreCell = cell + 1; s.OreDist = (float)dist;
            if (!first) return;
            w.Metrics.OreFound++;
            s.OreEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.OreFound, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Other = pid != 0 ? EntityRef.Person(pid) : default, ArgA = Copper, Magnitude = (float)dist, RootKind = RootKind.People,
                });
        }

        /// <summary>The founders of a first village looked over the land round their site (worldgen): the copper they saw there.</summary>
        public static void Look(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var near = new List<int>();
            w.Hazards.CellsWithin(w, s.Cell, FoundersLook, near);
            near.Sort();
            int best = -1; double bd = Reach + 1;
            for (int i = 0; i < near.Count; i++)
            {
                double d = Vec3d.ArcDistance(s.Anchor, w.Planet.Grid.Center[near[i]], w.Planet.Radius);
                if (d >= bd - 1e-6 || Seen(w, sid, near[i]) < 0) continue;
                bd = d; best = near[i];
            }
            if (best >= 0) Know(w, sid, 0, best, bd);
        }

        // ---------------------------------------------------------------- wanting it

        /// <summary>
        /// kg of ore the village wants in store (read once a day): what its smiths need to bring its tools to their metal and its arms to what
        /// it fears - while it knows copper and someone of it can smelt; 0 without.
        /// </summary>
        public static double Want(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.OreCell == 0 || !s.Inhabited || s.BestMetal < Smelter) return 0;
            double metal = DMath.Max(0.0, Knowledge.Tech.MetalPerHead * s.Population - s.Metal) + DMath.Max(0.0, Forge.ArmsWant(in s) - s.Arms) * Forge.ArmKg;
            return DMath.Min(StoreMost, metal * OrePerKg);
        }

        /// <summary>Skill in copper from which someone smelts (the open hearth's rule, D2-064).</summary>
        public const float Smelter = 0.3f;

        /// <summary>The village's standing mine (0 none).</summary>
        public static int Mine(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Mine != 0 && w.Buildings.Exists(s.Mine) && Structures.Working(in w.Buildings.Get(s.Mine), w.Now)) return s.Mine;
            return 0;
        }

        /// <summary>The village wants ore dug now: a mine stands and its store is short of what it wants.</summary>
        public static bool Wanted(World w, in Settlement s, out int mine)
        {
            mine = 0;
            if (s.OreWant <= 0 || s.Ore >= s.OreWant) return false;
            mine = Mine(w, s.Id);
            return mine != 0;
        }

        // ---------------------------------------------------------------- digging it

        /// <summary>kg of ore a miner digs in an hour at the face (a fit grown person a load; a weak body and worn tools less, metal tools more).</summary>
        public static double DigRate(in Person p, double tools) => DigPerHour * (0.5 + 0.5 * p.Health) * tools;

        /// <summary>kg of ore an hour of mining brings home from the village's mine: every 25 kg load carried both ways (Stone's law).</summary>
        public static double HourRate(World w, int sid, double r0, bool coarse)
        {
            ref var s = ref w.Settlements.Get(sid);
            double walk = w.Config.WalkSpeed, d = s.OreDist;
            return Stone.Rate(r0 / Trade.LoadKg, d, walk) * Trade.LoadKg * (coarse ? Stone.AfterWalk(d, walk) : 1.0);
        }

        /// <summary>
        /// Mining hours given at the village's mine: ore dug from the body (never more than it holds) and carried home into the village's store;
        /// the body gives out when the last is dug; a lived stint stops at what the store still wants (`most`, others dig too). Returns kg.
        /// </summary>
        public static double Dig(World w, int sid, int mine, double hours, double r0, bool coarse, double most = double.MaxValue)
        {
            if (hours <= 0 || mine == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            int di = w.Deposits.IndexAt(s.OreCell - 1, Copper);
            if (di < 0) return 0;
            ref var d = ref w.Deposits.At(di);
            double rate = HourRate(w, sid, r0, coarse);
            double kg = DMath.Min(DMath.Min(hours * rate, d.Ore), most);
            if (kg <= 0 || rate <= 0) return 0;
            hours = DMath.Min(hours, kg / rate);
            d.Ore -= kg; d.Taken += kg;
            s.Ore += kg;
            ref var m = ref w.Buildings.Get(mine);
            m.Stock = (float)(m.Stock + kg); m.Use = (float)(m.Use + hours);
            w.Metrics.OreDug += kg; w.Metrics.MineHours += hours;
            if (d.Ore < 1.0) GaveOut(w, di, sid);
            return kg;
        }

        /// <summary>
        /// The last of a body is dug: history (under the village that dug it out), its mines are left, every village that knew it forgets it
        /// (they look for copper again).
        /// </summary>
        static void GaveOut(World w, int di, int sid)
        {
            ref var d = ref w.Deposits.At(di);
            d.Ore = 0; d.GaveOut = w.Now;
            w.Metrics.OreSpent++;
            int cell = d.Cell; double taken = d.Taken;
            long cause = w.Settlements.Get(sid).OreEvent;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.OreGaveOut, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = new EntityRef(EntityKind.Building, w.Settlements.Get(sid).Mine), ArgA = Copper, Magnitude = (float)taken,
                RootKind = RootKind.People, Cause1 = cause, Rel1 = CauseRel.CausedBy,
            });
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                ref var s = ref w.Settlements.Get(id);
                if (s.OreCell != cell + 1) continue;
                s.OreCell = 0; s.OreDist = 0;
                if (s.Mine != 0 && w.Buildings.Exists(s.Mine))
                {
                    ref var m = ref w.Buildings.Get(s.Mine);
                    if (m.Left == 0) m.Left = w.Now;
                    Structures.Lost(w, s.Mine);
                }
            }
        }

        // ---------------------------------------------------------------- smelting it

        /// <summary>kg of copper the village's ore in store will make.</summary>
        public static double MetalIn(in Settlement s) => s.Ore / OrePerKg;

        /// <summary>Ore smelted into kg of copper: taken from the store.</summary>
        public static void Smelt(ref Settlement s, double kg) { s.Ore = DMath.Max(0.0, s.Ore - kg * OrePerKg); }
    }
}
