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
        public byte Kind;          // Mining.Copper, Mining.Iron (bog ore, C5.2b)
        public double Ore, Taken;  // kg of ore left in the ground; kg dug out
        public long Found;         // when it was first seen
        public int By;             // the village that first saw it
        public long GaveOut;       // when the last of it was dug (0 while ore is left)
        public long Event;         // what laid it there, when the geology did not (the god's ore: F9.6; 0 for the ground's own)

        public void Write(BinWriter w) { w.Int(Id); w.Int(Cell); w.Byte(Kind); w.Double(Ore); w.Double(Taken); w.Long(Found); w.Int(By); w.Long(GaveOut); w.Long(Event); }
        public static Deposit Read(BinReader r) => new Deposit { Id = r.Int(), Cell = r.Int(), Kind = r.Byte(), Ore = r.Double(), Taken = r.Double(), Found = r.Long(), By = r.Int(), GaveOut = r.Long(), Event = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Cell); h.Add(Kind); h.Add(Ore); h.Add(Taken); h.Add(Found); h.Add(By); h.Add(GaveOut); if (Event != 0) h.Add(Event); }   // (a world with no god's ore hashes as before)
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
    /// Ore from the ground (C5.1, D2-100; C5.2b, D2-102; 22 §1-§2). **Copper** shows where rock breaks through the land (the quarry's outcrop,
    /// 80 m from a land neighbour) within 2 km (geo) of a volcano, living or gone quiet, on some such crags (15 %), each body 1-20 t of workable
    /// ore (the oxidised cap of a vein: green and blue stone); 5 kg of ore make a kilo of copper. **Bog iron** forms where water stands on flat
    /// ground (a lake, a brook draining through, or a wet climate's fen, no neighbour 12 m above or below) - in a third of such places, a bed of 0.5-8 t of rust-red lumps;
    /// 8 kg of it make a kilo of bar iron in a bloomery; a bed dug out forms again in forty years. A village knows ore only when one of its
    /// people has seen it (the grounds they go to, the woodlot, the land the founders looked over) and keeps, of each kind, the nearest body it
    /// knows within 2 km that still has ore. A village whose people can smelt it and whose tools or arms are short of it opens a **mine** at a
    /// copper body (a pit with a windlass) or **bog-ore pits** at a bed, and digs: a load (25 kg) of ore an hour, every load carried home. No ore
    /// in store, no metal - nothing is smelted from nothing. A body is counted down by what is dug, whoever digs it; when the last is dug it
    /// **gives out**: history, the mine is left, the villages that knew it forget it and look for another. One law for lived days and COARSE/DEEP.
    /// </summary>
    public static class Mining
    {
        public const byte Copper = 1, Iron = 2;
        /// <summary>m: how far from a volcano copper shows (geo-m: the arc's ground, D2-085).</summary>
        public const double ArcReach = 2000.0;
        /// <summary>Share of the crags near an arc that carry a copper body; of the wet flats that hold a bed of bog ore.</summary>
        public const double CopperShare = 0.15, BogShare = 0.35;
        /// <summary>m: a wet cell is flat when no land neighbour stands this much above or below it (water stands; iron settles out).</summary>
        public const double BogFlat = 12.0;
        /// <summary>kg of workable ore a body holds: from this to that, evenly in its logarithm (keyed by its cell).</summary>
        public const double OreMin = 1000.0, OreMax = 20000.0, BogMin = 500.0, BogMax = 8000.0;
        /// <summary>Years in which a dug-out bed of bog ore forms again (iron carried in by the water, settling out).</summary>
        public const double BogRegrowYears = 40.0;
        /// <summary>m: the farthest ore a village digs from home (worth a longer walk than stone).</summary>
        public const double Reach = 2000.0;
        /// <summary>m: how far round their site the founders of the first villages looked over the land.</summary>
        public const double FoundersLook = 1000.0;
        /// <summary>kg of ore dug an hour at the face by a fit grown person (a load: picks, fire-setting, sorting; spades in a bog).</summary>
        public const double DigPerHour = 25.0;
        /// <summary>kg of ore a kilo of copper takes (a rich oxidised ore, smelted in a crucible or a small furnace).</summary>
        public const double OrePerKg = 5.0;
        /// <summary>kg of bog ore a kilo of bar iron takes (the bloom a fifth of the ore, half of it lost forging it to a bar).</summary>
        public const double FeOrePerKg = 8.0;
        /// <summary>kg of ore a village keeps ahead at most (what its smiths will smelt in a season or two).</summary>
        public const double StoreMost = 1500.0, FeStoreMost = 3000.0;
        /// <summary>Skill in a metal from which someone smelts it (the open hearth's rule, D2-064).</summary>
        public const float Smelter = 0.3f;

        // ---------------------------------------------------------------- a kind's fields

        public static int CellOf(in Settlement s, byte kind) => kind == Iron ? s.FeCell : s.OreCell;
        public static double DistOf(in Settlement s, byte kind) => kind == Iron ? s.FeDist : s.OreDist;
        public static double OreIn(in Settlement s, byte kind) => kind == Iron ? s.FeOre : s.Ore;
        public static double WantOf(in Settlement s, byte kind) => kind == Iron ? s.FeOreWant : s.OreWant;
        public static long EventOf(in Settlement s, byte kind) => kind == Iron ? s.FeEvent : s.OreEvent;
        static int MineOf(in Settlement s, byte kind) => kind == Iron ? s.FeMine : s.Mine;
        public static byte MineKind(byte kind) => kind == Iron ? Structures.BogPits : Structures.Mine;

        static void SetKnown(ref Settlement s, byte kind, int cell1, double dist)
        {
            if (kind == Iron) { s.FeCell = cell1; s.FeDist = (float)dist; } else { s.OreCell = cell1; s.OreDist = (float)dist; }
        }

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

        /// <summary>m3 a day of water draining through a cell from which its ground is a brook's wet margin (a stream is 1,000).</summary>
        public const double BrookFlow = 300.0;
        /// <summary>The climate's moisture index from which flat ground lies waterlogged (fens, wet meadows).</summary>
        public const double FenMoist = 0.7;

        /// <summary>The ground lies wet on this cell: a lake, a brook or a stream draining through it, or a wet climate's fen - land under no ice.</summary>
        static bool Wet(PlanetState P, int c) => P.IsLand(c) && P.Ice[c] < 0.5f && (P.LakeDepth[c] > 0.3f || P.RiverFlow[c] >= BrookFlow || P.Moist[c] >= FenMoist);

        /// <summary>A bed of bog ore lies at this cell: water standing on flat ground, above the shore, in a share of such places.</summary>
        public static bool BogAt(World w, int c)
        {
            var P = w.Planet;
            if (!Wet(P, c)) return false;
            for (int k = 0; k < 4; k++)
            {
                int nb = P.Grid.Nbr4[c * 4 + k];
                if (P.IsLand(nb) && DMath.Abs((double)P.Elev[nb] - P.Elev[c]) >= BogFlat) return false;
            }
            if (!Shore.Dry(P, P.Grid.Center[c])) return false;
            return KeyedRng.Unit(w.Seed, RngStream.Geology, (ulong)c, 3UL) < BogShare;
        }

        public static bool At(World w, int c, byte kind) => kind == Iron ? BogAt(w, c) : CopperAt(w, c);

        /// <summary>kg of ore the body at a cell holds when first seen (keyed by the cell: the same body whoever finds it, whenever).</summary>
        public static double OreOf(World w, int c, byte kind = Copper)
        {
            double u = KeyedRng.Unit(w.Seed, RngStream.Geology, (ulong)c, kind == Iron ? 4UL : 2UL);
            double lo = kind == Iron ? BogMin : OreMin, hi = kind == Iron ? BogMax : OreMax;
            return DMath.Exp(DMath.Log(lo) + u * (DMath.Log(hi) - DMath.Log(lo)));
        }

        /// <summary>The kept deposit at a cell, made the first time anyone sees it (-1 when none lies there or it is dug out); a bed of
        /// bog ore dug out forty years ago has formed again.</summary>
        static int Seen(World w, int sid, int c, byte kind)
        {
            int i = w.Deposits.IndexAt(c, kind);
            if (i >= 0)
            {
                ref var d = ref w.Deposits.At(i);
                if (d.GaveOut == 0) return i;
                if (kind != Iron || w.Now - d.GaveOut < DMath.ToLong(BogRegrowYears * SimTime.Year)) return -1;
                d.Ore = OreOf(w, c, kind); d.GaveOut = 0;   // (formed again)
                return i;
            }
            if (!At(w, c, kind)) return -1;
            return w.Deposits.Add(new Deposit { Id = w.Deposits.Count + 1, Cell = c, Kind = kind, Ore = OreOf(w, c, kind), Found = w.Now, By = sid });
        }

        // ---------------------------------------------------------------- the god's ore (F9.6)

        /// <summary>Whether a body the god laid bare lies at a cell (F9.6) - the ground's own bodies are found by the geology's rule alone.</summary>
        static bool Laid(World w, int c, byte kind) { int i = w.Deposits.IndexAt(c, kind); return i >= 0 && w.Deposits.Get(i).Event != 0; }

        /// <summary>kg of ore in a body the god lays bare: twice the richest the ground holds (a vein's whole cap, green and blue stone).</summary>
        public const double GodOreKg = 2.0 * OreMax;

        /// <summary>
        /// The god lays bare a body of rich copper ore at a place on dry land (F9.6): it enters the world's state at once as a body in the ground - not yet
        /// seen by anyone; its people find it as they find any ore, where they go (`Saw`), and dig, smelt and trade it by the world's own rules. Where a body
        /// lies there already its ore is renewed. Returns the record of its laying (0: no dry land there).
        /// </summary>
        public static long Reveal(World w, int cell, long cause)
        {
            var P = w.Planet;
            if (cell < 0 || !P.IsLand(cell) || P.Ice[cell] > 0.5f || !Shore.Dry(P, P.Grid.Center[cell])) return 0;
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.OreRevealed, Time = w.Now, Cell = cell, Settlement = w.VillageNear(cell, Reach), Cause1 = cause, Rel1 = CauseRel.CausedBy,
                ArgA = Copper, Magnitude = (float)GodOreKg, RootKind = RootKind.You,
            });
            int i = w.Deposits.IndexAt(cell, Copper);
            if (i >= 0) { ref var d = ref w.Deposits.At(i); d.Ore += GodOreKg; d.GaveOut = 0; d.Event = ev; }
            else w.Deposits.Add(new Deposit { Id = w.Deposits.Count + 1, Cell = cell, Kind = Copper, Ore = GodOreKg, Found = w.Now, By = 0, Event = ev });
            return ev;
        }

        // ---------------------------------------------------------------- whose ore (C5.4)

        /// <summary>The share of its people with metal tools below which a village's need presses: it digs ore others work.</summary>
        public const double Pressing = 0.5;
        /// <summary>Days within which another stint at the same village's ore adds to the same taking.</summary>
        public const int ClaimDays = 12;

        /// <summary>
        /// The village that works the body of a kind at a cell: the one whose standing mine (or pits) there was raised first (the lowest
        /// building id) - 0 when none works it. Worked ore is its own; others digging it take from it.
        /// </summary>
        public static int WorkedBy(World w, int cell, byte kind)
        {
            int owner = 0, first = int.MaxValue;
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                ref var s = ref w.Settlements.Get(id);
                if (!s.Inhabited || CellOf(in s, kind) != cell + 1) continue;
                int m = Mine(w, id, kind);
                if (m != 0 && m < first) { first = m; owner = id; }
            }
            return owner;
        }

        /// <summary>
        /// Whether a village's people dig the body of a kind it knows: freely when no other village works it (or it works it first); at another's
        /// worked ore only when its need presses (fewer than half its people with metal tools) or it holds a wrong against the owners (taking
        /// back is not taking to them: D2-069).
        /// </summary>
        public static bool MayDig(World w, int sid, byte kind)
        {
            ref var s = ref w.Settlements.Get(sid);
            int cell = CellOf(in s, kind) - 1;
            if (cell < 0) return false;
            int owner = WorkedBy(w, cell, kind);
            if (owner == 0 || owner == sid) return true;
            return Knowledge.Tech.ToolShare(in s) < Pressing || w.DeedBook.Grievance(sid, owner) > Trade.Grudge;
        }

        /// <summary>
        /// Ore dug at another village's worked body: a taking from it (a harm deed of the metal it would have made, worth that metal in the
        /// owners' own values against their grain), seen by the owners working beside them; history the first time in a while; stints within
        /// twelve days add to the same deed. The owners' grievance, amends asked and refused, bands and walls follow as for any taking.
        /// </summary>
        static void Claim(World w, int sid, int owner, int pid, byte kind, double kg)
        {
            byte good = kind == Iron ? Trade.Iron : Trade.Metal;
            double metal = kg / (kind == Iron ? FeOrePerKg : OrePerKg);
            double worth = metal * w.Trade.Value(owner, good) / DMath.Max(0.5, w.Trade.Value(owner, Trade.Grain));
            ref var s = ref w.Settlements.Get(sid);
            w.Metrics.OreClaimedKg += kg;
            if (s.ClaimDeed != 0 && w.Deeds.Exists(s.ClaimDeed) && s.ClaimOn == owner && w.Now - s.ClaimAt < ClaimDays * SimTime.Day)
            {
                w.DeedBook.AddWorth(s.ClaimDeed, metal, worth);
                s.ClaimAt = w.Now;
                return;
            }
            ref var o = ref w.Settlements.Get(owner);
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.OreTaken, Time = w.Now, Cell = CellOf(in s, kind) - 1, Settlement = owner, Subject = EntityRef.Settlement(sid),
                Object = EntityRef.Settlement(owner), Other = pid != 0 ? EntityRef.Person(pid) : default, ArgA = kind, Magnitude = (float)kg,
                RootKind = RootKind.People, Cause1 = EventOf(in o, kind), Rel1 = CauseRel.InResponseTo,
            });
            int d = w.DeedBook.Record(Society.DeedKind.Take, pid, sid, 0, owner, good, metal, worth, ev);
            w.DeedBook.Learn(owner, d, Society.KnownHow.Saw);
            w.DeedBook.Learn(sid, d, Society.KnownHow.Own);
            ref var s2 = ref w.Settlements.Get(sid);
            s2.ClaimDeed = d; s2.ClaimOn = owner; s2.ClaimAt = w.Now;
            w.Metrics.OreClaims++;
        }

        // ---------------------------------------------------------------- places by their ore (C5.3)

        /// <summary>m: a site this near ore its village lacks is worth more to its people (a short walk to the work).</summary>
        public const double SitePull = 1000.0;
        /// <summary>How much more such a site is worth.</summary>
        public const double SiteBonus = 1.5;

        /// <summary>
        /// A village lacks ore of a kind: someone of it smelts it and its tools or arms want it, but it knows no body within its reach.
        /// </summary>
        public static bool Lacks(in Settlement s, byte kind)
        {
            if (CellOf(in s, kind) != 0 || !s.Inhabited) return false;
            if (kind == Iron) return s.BestIron >= Smelter && Forge.IronWanted(in s) > 0;
            return s.BestMetal >= Smelter && Forge.CopperWanted(in s) > 0;
        }

        /// <summary>
        /// What a village's scout, looking over `sight`, sees of the ore their village lacks: the first such body (a deposit kept from then),
        /// or -1. Its kind in `kind`.
        /// </summary>
        public static int OreInSight(World w, int sid, List<int> sight, out byte kind)
        {
            kind = 0;
            ref var s = ref w.Settlements.Get(sid);
            for (byte k = Copper; k <= Iron; k++)
            {
                if (!Lacks(in s, k)) continue;
                for (int i = 0; i < sight.Count; i++)
                    if (Seen(w, sid, sight[i], k) >= 0 && WorkedBy(w, sight[i], k) == 0) { kind = k; return sight[i]; }   // (ore nobody works: C5.4)
            }
            return -1;
        }

        // ---------------------------------------------------------------- knowing it

        /// <summary>The copper body the village knows (-1 none).</summary>
        public static int Known(in Settlement s) => s.OreCell - 1;

        /// <summary>
        /// Someone of a village (0: its people at large) was at a cell and saw what shows there or beside it: ore within reach that is nearer
        /// than the body of its kind the village knows is kept; the first is history.
        /// </summary>
        public static void Saw(World w, int sid, int pid, int cell)
        {
            if (cell < 0 || !w.Settlements.Exists(sid)) return;
            var P = w.Planet;
            for (byte kind = Copper; kind <= Iron; kind++)
            {
                ref var s = ref w.Settlements.Get(sid);
                int known = CellOf(in s, kind);
                // (ore another village works counts as twice as far: a free body is better to know - C5.4)
                int knownOwner = known > 0 ? WorkedBy(w, known - 1, kind) : 0;
                int best = -1; double bd = known > 0 ? DistOf(in s, kind) * (knownOwner != 0 && knownOwner != sid ? 2.0 : 1.0) : double.MaxValue;
                for (int k = -1; k < 8; k++)
                {
                    int c = k < 0 ? cell : P.Grid.Nbr8[cell * 8 + k];
                    if (c + 1 == known) continue;
                    if ((kind == Copper ? !Stone.Outcrop(P, c) : !Wet(P, c)) && !Laid(w, c, kind)) continue;   // (the cheap part of the rule first; a body the god laid bare where it does not hold is there all the same: F9.6)
                    double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                    if (d > Reach || d >= bd - 1e-6) continue;
                    if (Seen(w, sid, c, kind) < 0) continue;
                    int ow = WorkedBy(w, c, kind);
                    double de = ow != 0 && ow != sid ? 2.0 * d : d;
                    if (de >= bd - 1e-6) continue;
                    bd = de; best = c;
                }
                if (best >= 0) Know(w, sid, pid, best, Vec3d.ArcDistance(w.Settlements.Get(sid).Anchor, P.Grid.Center[best], P.Radius), kind);
            }
        }

        static void Know(World w, int sid, int pid, int cell, double dist, byte kind)
        {
            ref var s = ref w.Settlements.Get(sid);
            bool first = CellOf(in s, kind) == 0;   // (ore known again after a body gave out is history too; a nearer one found is not)
            SetKnown(ref s, kind, cell + 1, dist);
            if (!first) return;
            w.Metrics.OreFound++;
            int di = w.Deposits.IndexAt(cell, kind);
            long laid = di >= 0 ? w.Deposits.Get(di).Event : 0;   // (a body the god laid bare: its finding runs back to the cast - F9.6)
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.OreFound, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Other = pid != 0 ? EntityRef.Person(pid) : default, ArgA = kind, Magnitude = (float)dist, RootKind = laid != 0 ? RootKind.Unknown : RootKind.People,
                Cause1 = laid, Rel1 = laid != 0 ? CauseRel.EnabledBy : CauseRel.None,
            });
            ref var s2 = ref w.Settlements.Get(sid);
            if (kind == Iron) s2.FeEvent = ev; else s2.OreEvent = ev;
        }

        /// <summary>The founders of a first village looked over the land round their site (worldgen): the ore they saw there.</summary>
        public static void Look(World w, int sid)
        {
            var near = new List<int>();
            w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, FoundersLook, near);
            near.Sort();
            for (byte kind = Copper; kind <= Iron; kind++)
            {
                int best = -1; double bd = Reach + 1;
                for (int i = 0; i < near.Count; i++)
                {
                    double d = Vec3d.ArcDistance(w.Settlements.Get(sid).Anchor, w.Planet.Grid.Center[near[i]], w.Planet.Radius);
                    if (d >= bd - 1e-6 || Seen(w, sid, near[i], kind) < 0) continue;
                    bd = d; best = near[i];
                }
                if (best >= 0) Know(w, sid, 0, best, bd, kind);
            }
        }

        // ---------------------------------------------------------------- wanting it

        /// <summary>
        /// kg of ore of a kind the village wants in store (read once a day): copper for its tools while iron does not yet serve them, iron for
        /// its tools, and either for the arms it wants when it fears its neighbours (iron where its smiths can smelt it) - while it knows the ore
        /// and someone of it can smelt it; 0 without.
        /// </summary>
        public static double Want(World w, int sid, byte kind = Copper)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (CellOf(in s, kind) == 0 || !s.Inhabited) return 0;
            if (kind == Iron)
            {
                if (s.BestIron < Smelter) return 0;
                return DMath.Min(FeStoreMost, Forge.IronWanted(in s) * FeOrePerKg);
            }
            if (s.BestMetal < Smelter) return 0;
            return DMath.Min(StoreMost, Forge.CopperWanted(in s) * OrePerKg);
        }

        /// <summary>The village's standing mine of a kind (0 none).</summary>
        public static int Mine(World w, int sid, byte kind = Copper)
        {
            ref var s = ref w.Settlements.Get(sid);
            int m = MineOf(in s, kind);
            if (m != 0 && w.Buildings.Exists(m) && Structures.Working(in w.Buildings.Get(m), w.Now)) return m;
            return 0;
        }

        /// <summary>The village wants ore of a kind dug now: its mine stands and its store is short of what it wants.</summary>
        public static bool Wanted(World w, in Settlement s, out int mine, byte kind = Copper)
        {
            mine = 0;
            double want = WantOf(in s, kind);
            if (want <= 0 || OreIn(in s, kind) >= want) return false;
            mine = Mine(w, s.Id, kind);
            return mine != 0 && MayDig(w, s.Id, kind);   // (another's worked ore only when need presses: C5.4)
        }

        // ---------------------------------------------------------------- digging it

        /// <summary>kg of ore a miner digs in an hour at the face (a fit grown person a load; a weak body and worn tools less, metal tools more).</summary>
        public static double DigRate(in Person p, double tools) => DigPerHour * (0.5 + 0.5 * p.Health) * tools;

        /// <summary>kg of ore an hour of mining brings home from the village's mine: every 25 kg load carried both ways (Stone's law).</summary>
        public static double HourRate(World w, int sid, double r0, bool coarse, byte kind = Copper)
        {
            ref var s = ref w.Settlements.Get(sid);
            double walk = w.Config.WalkSpeed, d = DistOf(in s, kind);
            return Stone.Rate(r0 / Trade.LoadKg, d, walk) * Trade.LoadKg * (coarse ? Stone.AfterWalk(d, walk) : 1.0);
        }

        /// <summary>
        /// Mining hours given at the village's mine of a kind: ore dug from the body (never more than it holds) and carried home into the
        /// village's store; the body gives out when the last is dug; a lived stint stops at what the store still wants (`most`, others dig
        /// too). Returns kg.
        /// </summary>
        public static double Dig(World w, int sid, int mine, double hours, double r0, bool coarse, double most = double.MaxValue, byte kind = Copper, int pid = 0)
        {
            if (hours <= 0 || mine == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            int di = w.Deposits.IndexAt(CellOf(in s, kind) - 1, kind);
            if (di < 0) return 0;
            ref var d = ref w.Deposits.At(di);
            double rate = HourRate(w, sid, r0, coarse, kind);
            double kg = DMath.Min(DMath.Min(hours * rate, d.Ore), most);
            if (kg <= 0 || rate <= 0) return 0;
            hours = DMath.Min(hours, kg / rate);
            d.Ore -= kg; d.Taken += kg;
            if (kind == Iron) s.FeOre += kg; else s.Ore += kg;
            ref var m = ref w.Buildings.Get(mine);
            m.Stock = (float)(m.Stock + kg); m.Use = (float)(m.Use + hours);
            w.Metrics.OreDug += kg; w.Metrics.MineHours += hours;
            if (kind == Iron) w.Metrics.BogOreDug += kg;
            int owner = WorkedBy(w, d.Cell, kind);
            if (owner != 0 && owner != sid) Claim(w, sid, owner, pid, kind, kg);   // (another village's ore: C5.4)
            if (w.Deposits.Get(di).Ore < 1.0) GaveOut(w, di, sid);
            return kg;
        }

        /// <summary>
        /// The last of a body is dug: history (under the village that dug it out), its mines are left, every village that knew it forgets it
        /// (they look for ore again).
        /// </summary>
        static void GaveOut(World w, int di, int sid)
        {
            ref var d = ref w.Deposits.At(di);
            d.Ore = 0; d.GaveOut = w.Now;
            w.Metrics.OreSpent++;
            int cell = d.Cell; double taken = d.Taken; byte kind = d.Kind;
            ref var s0 = ref w.Settlements.Get(sid);
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.OreGaveOut, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = new EntityRef(EntityKind.Building, MineOf(in s0, kind)), ArgA = kind, Magnitude = (float)taken,
                RootKind = RootKind.People, Cause1 = EventOf(in s0, kind), Rel1 = CauseRel.CausedBy,
            });
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                ref var s = ref w.Settlements.Get(id);
                if (CellOf(in s, kind) != cell + 1) continue;
                SetKnown(ref s, kind, 0, 0);
                int m = MineOf(in s, kind);
                if (m != 0 && w.Buildings.Exists(m))
                {
                    ref var mb = ref w.Buildings.Get(m);
                    if (mb.Left == 0) mb.Left = w.Now;
                    Structures.Lost(w, m);
                }
            }
        }

        // ---------------------------------------------------------------- smelting it

        /// <summary>kg of copper the village's copper ore in store will make.</summary>
        public static double MetalIn(in Settlement s) => s.Ore / OrePerKg;

        /// <summary>Copper ore smelted into kg of copper: taken from the store.</summary>
        public static void Smelt(ref Settlement s, double kg) { s.Ore = DMath.Max(0.0, s.Ore - kg * OrePerKg); }

        /// <summary>kg of bar iron the village's bog ore in store will make.</summary>
        public static double IronIn(in Settlement s) => s.FeOre / FeOrePerKg;

        /// <summary>Bog ore smelted into kg of iron: taken from the store.</summary>
        public static void SmeltIron(ref Settlement s, double kg) { s.FeOre = DMath.Max(0.0, s.FeOre - kg * FeOrePerKg); }
    }
}
