using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>What a structure is for (21 §1).</summary>
    public enum Function : byte { Dwelling = 0, Water = 1, Store = 2, Smith = 3, Quarry = 4, Train = 5, Market = 6, Gate = 7, Mine = 8, Charcoal = 9, Boat = 10, Boatyard = 11 }   // (bog-ore pits are mines too)

    /// <summary>Where a structure is laid out (21 §4).</summary>
    public enum Zone : byte { Homes = 0, Core = 1, Edge = 2, Outer = 3, Site = 4, Shore = 5 }

    /// <summary>A kind of structure (a row of the kinds table, 21 §1): its function, where it goes, what raising it takes, how long it lasts.</summary>
    public readonly struct KindInfo
    {
        public readonly string Name;
        public readonly Function Function;
        public readonly Zone Zone;
        public readonly double Footprint;     // m: the clearance it keeps round its centre
        public readonly double Hours, Wood;   // labour-hours and 25 kg loads of wood to raise it
        public readonly double FallYears;     // an unkept structure falls in after this many years
        public readonly double RuinYears;     // and its ruin is gone into the land after this many more
        public readonly double Capacity;      // what it holds or takes (a store's FU)

        public KindInfo(string name, Function f, Zone z, double footprint, double hours, double wood, double fall, double ruin, double capacity)
        {
            Name = name; Function = f; Zone = z; Footprint = footprint; Hours = hours; Wood = wood; FallYears = fall; RuinYears = ruin; Capacity = capacity;
        }
    }

    /// <summary>
    /// Structures (C1.1, D2-090; 21_SETTLEMENT_ECONOMY.md): every built thing on a village's ground is a row of `World.Buildings` with a
    /// kind from this table - homes since D2-059, and now the village's works. A work is planned when a need in state calls for it (no
    /// water: a well; a harvest to keep: a granary; a store overfilled at harvest: another granary), laid out by its zone, raised by its
    /// people's labour and the village's wood (Construction), and from then on does what it is for: a granary keeps grain (what lies
    /// beyond the granaries' capacity rots four times as fast). Its history - why it was begun, who began it, when it stood, when it
    /// fell - is in the ledger under the building. The village's `Works` bits are a cache of what stands (read by older systems).
    /// </summary>
    public static class Structures
    {
        public const byte Hut = Construction.Hut, House = Construction.House;   // homes (D2-045, D2-059)
        public const byte Well = 10, Granary = 11;                                // the village's works (C1.1)
        public const byte Smithy = 12, Quarry = 13, Ground = 14, Market = 15;     // (C1.3-C1.6: in the table, planned as their slices land)
        public const byte Barn = 16;                                              // a large store (C1.1)
        public const byte Mine = 17;                                              // a pit at an ore body (C5.1)
        public const byte CharHearth = 18;                                            // a charcoal hearth at the woodlot (C5.2a)
        public const byte BogPits = 19;                                               // pits at a bed of bog ore (C5.2b)
        public const byte Boat = 20;                                                  // a household's dugout at the landing (C9.1, `Boats`)
        public const byte PlankBoat = 21;                                             // a household's boat of planks (C9.2b)
        public const byte BoatShed = 22;                                              // the village's boat shed at the landing, where plank boats are built (C9.2b)

        /// <summary>FU of grain a granary keeps well (~7.5 t: a raised timber store of ~10 m3).</summary>
        public const double GranaryHolds = 10000.0;
        /// <summary>FU a barn keeps (~30 t: a great timber barn - grain in the sheaf and threshed, ~40 m3).</summary>
        public const double BarnHolds = 40000.0;
        /// <summary>How many times faster grain rots when it lies beyond the granaries' capacity (heaps, pits, sacks in the homes).</summary>
        public const double HeapRot = 4.0;
        /// <summary>A store filled past this share of its capacity at harvest wants another granary.</summary>
        public const double FullAtHarvest = 0.9;

        static readonly KindInfo[] Table = Build();

        static KindInfo[] Build()
        {
            var t = new KindInfo[24];
            t[Hut] = new KindInfo("hut", Function.Dwelling, Zone.Homes, 3.0, Construction.HutHours, Construction.HutWood, 20, 80, 0);
            t[House] = new KindInfo("house", Function.Dwelling, Zone.Homes, 4.5, Construction.HouseHours, Construction.HouseWood, 60, 300, 0);
            t[Well] = new KindInfo("well", Function.Water, Zone.Core, 2.0, Construction.WellHours, Construction.WellWood, 150, 600, 0);
            t[Granary] = new KindInfo("granary", Function.Store, Zone.Core, 4.0, Construction.GranaryHours, Construction.GranaryWood, 40, 150, GranaryHolds);
            t[Smithy] = new KindInfo("smithy", Function.Smith, Zone.Edge, 5.0, 160, 20, 50, 200, 0);
            t[Quarry] = new KindInfo("quarry", Function.Quarry, Zone.Site, 8.0, 20, 2, 1000, 3000, 0);
            t[Ground] = new KindInfo("training ground", Function.Train, Zone.Outer, 12.0, 30, 4, 15, 40, 0);
            t[Market] = new KindInfo("market", Function.Market, Zone.Core, 9.0, 60, 15, 30, 100, 0);
            t[CharHearth] = new KindInfo("charcoal hearth", Function.Charcoal, Zone.Site, 6.0, 12, 0, 20, 60, 0);
            t[BogPits] = new KindInfo("bog-ore pits", Function.Mine, Zone.Site, 7.0, 16, 2, 15, 40, 0);
            t[Mine] = new KindInfo("mine", Function.Mine, Zone.Site, 7.0, 40, 6, 60, 300, 0);
            t[Barn] = new KindInfo("barn", Function.Store, Zone.Edge, 7.0, 360, 90, 60, 200, BarnHolds);
            t[Boat] = new KindInfo("dugout", Function.Boat, Zone.Shore, 2.5, Boats.Hours, Boats.Wood, 5, 2, 0);   // (left unkept it splits in a few years)
            t[PlankBoat] = new KindInfo("plank boat", Function.Boat, Zone.Shore, 3.0, Boats.PlankHours, Boats.PlankWood, 8, 3, 0);
            t[BoatShed] = new KindInfo("boat shed", Function.Boatyard, Zone.Shore, 5.5, Boats.ShedHours, Boats.ShedWood, 30, 60, 0);   // (a naust: a long low timber shed, open to the sea)
            return t;
        }

        public static KindInfo Of(byte kind) => kind < Table.Length && Table[kind].Name != null ? Table[kind] : Table[House];
        public static string Name(byte kind) => Of(kind).Name;
        public static bool IsHome(byte kind) => kind == Hut || kind == House;

        // ---------------------------------------------------------------- state over time

        /// <summary>How sound a structure is at time t: its condition, and once left unkept, down to 0 over its fall years (homes: D2-059).</summary>
        public static double Condition(in Building b, long t)
        {
            double c = b.Left == 0 ? 1.0 : DMath.Clamp01(1.0 - (t - b.Left) / (Homes.FallYears(in b) * SimTime.Year));
            return IsHome(b.Kind) ? c : DMath.Min(c, (double)b.Cond);
        }

        /// <summary>A structure that stands and does what it is for: raised, and not fallen in.</summary>
        public static bool Working(in Building b, long t) => b.Done != 0 && !Homes.Fallen(in b, t) && Condition(in b, t) >= Homes.Habitable;

        /// <summary>FU the village's standing granaries and barns keep well.</summary>
        public static double StoreCapacity(in Settlement s) => s.Granaries * GranaryHolds + s.Barns * BarnHolds;

        /// <summary>
        /// Spoilage of the village's grain over a step of days (D2-042, D2-063): what its granaries hold rots at the store's rate, what lies
        /// beyond them (heaps, pits, sacks) at HeapRot times that (C1.1).
        /// </summary>
        public static void Rot(ref Settlement s, double rot, int days)
        {
            double cap = StoreCapacity(in s);
            if (s.Granary <= cap) { s.Granary *= days == 1 ? 1.0 - rot : SettlementSystem.Keep(rot, days); return; }
            double over = s.Granary - cap, heap = DMath.Min(0.9, rot * HeapRot);
            s.Granary = cap * (days == 1 ? 1.0 - rot : SettlementSystem.Keep(rot, days)) + over * (days == 1 ? 1.0 - heap : SettlementSystem.Keep(heap, days));
        }

        // ---------------------------------------------------------------- what the village wants built (21 §2)

        /// <summary>
        /// The village's works by need, read once a day (and at founding): with no work under way, the first need in state - no well (water
        /// before all), no granary once there is a harvest to keep - becomes a planned structure, laid out and begun. The palisade (D2-074)
        /// and its mending stay ring works (Construction.NextWork).
        /// </summary>
        public static void Plan(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited || s.Project != 0) return;
            if ((s.Works & Construction.Well) == 0) { Begin(w, sid, Well, s.FoundedEvent, CauseRel.EnabledBy); return; }
            if ((s.Works & Construction.Granary) == 0 && (s.LastHarvest > 0 || s.Granary > 5 * s.Population))
            { Begin(w, sid, Granary, s.HarvestEvent != 0 ? s.HarvestEvent : s.FoundedEvent, CauseRel.CausedBy); return; }
            // copper known, someone who can smelt it, and tools or arms short of metal: a mine at the body (C5.1) - after water and a store,
            // never while walls rise
            if (s.OreWant > 0 && s.Mine == 0 && s.OreCell > 0 && s.Ore < s.OreWant && s.Wall != 1 && (s.Works & Construction.Granary) != 0 && Mining.MayDig(w, sid, Mining.Copper))
            {
                if (!Shore.Dry(w.Planet, w.Planet.Grid.Center[s.OreCell - 1])) { s.OreCell = 0; s.OreDist = 0; return; }   // (the sea has taken that ground)
                Begin(w, sid, Mine, s.OreEvent, CauseRel.EnabledBy); return;
            }
            // bog ore known, someone who can smelt iron, and iron short: pits at the bed (C5.2b)
            if (s.FeOreWant > 0 && s.FeMine == 0 && s.FeCell > 0 && s.FeOre < s.FeOreWant && s.Wall != 1 && (s.Works & Construction.Granary) != 0 && Mining.MayDig(w, sid, Mining.Iron))
            {
                if (!Shore.Dry(w.Planet, w.Planet.Grid.Center[s.FeCell - 1])) { s.FeCell = 0; s.FeDist = 0; return; }
                Begin(w, sid, BogPits, s.FeEvent, CauseRel.EnabledBy); return;
            }
            // charcoal wanted for its smiths: a hearth at the woodlot - and when the woods have receded past it, the next at the woods (C5.2a)
            if (s.CharcoalWant > 0 && s.Charcoal < s.CharcoalWant && s.Wall != 1 && (s.Works & Construction.Granary) != 0 && Woods.Lot(in s) >= 0)
            {
                int hearth = Charcoal.Site(w, sid);
                if (hearth == 0 || Charcoal.Far(w, sid, hearth))
                {
                    if (hearth != 0) { ref var old = ref w.Buildings.Get(hearth); old.Left = w.Now; Lost(w, hearth); }
                    Begin(w, sid, CharHearth, s.OreEvent != 0 ? s.OreEvent : s.SmithEvent, CauseRel.EnabledBy); return;
                }
            }
            // boats of planks known and a boat wanted by one of its households: a boat shed at the landing to build them in (C9.2b) - after water
            // and a store, never while walls rise
            if (s.Shed == 0 && (s.TechKnown & Boats.Bit) != 0 && s.Wall != 1 && (s.Works & Construction.Granary) != 0 && Boats.ShedWanted(w, sid))
            { Begin(w, sid, BoatShed, s.BoatEvent, CauseRel.EnabledBy); return; }
            // a competent smith and use for metal: a smithy (C1.4) - after water and a store, never while walls rise
            if (s.Smithy == 0 && (s.TechKnown & Forge.Bit) != 0 && s.Wall != 1 && (s.Works & Construction.Granary) != 0 && Forge.Use(w, in s))
            { Begin(w, sid, Smithy, s.SmithEvent, CauseRel.EnabledBy); return; }
            // traders come often to deal: a market on its green (C1.6)
            if (s.Market == 0 && s.TradeIn >= Trade.MarketFrom && s.Population >= 100 && s.Wall != 1 && (s.Works & Construction.Granary) != 0)
            { Begin(w, sid, Market, 0, CauseRel.None); return; }
            // a village that fears its neighbours lays out a training ground (C1.5) - after water and a store, never while walls rise
            if (s.Ground == 0 && Forge.Threatened(in s) && s.Wall != 1 && (s.Works & Construction.Granary) != 0)
            { Begin(w, sid, Ground, s.WallEvent != 0 ? s.WallEvent : (s.FoeEvent != 0 ? s.FoeEvent : s.WatchEvent), CauseRel.InResponseTo); return; }
            // stone wanted and rock known within reach: a quarry opened at the rock (C1.3) - after water and a store, never while walls rise
            if (s.StoneWant > 0 && s.Quarry == 0 && s.StoneCell > 0 && s.Wall != 1 && (s.Works & Construction.Granary) != 0)
            {
                if (!Shore.Dry(w.Planet, w.Planet.Grid.Center[s.StoneCell - 1])) { s.StoneCell = 0; s.StoneDist = 0; return; }   // (the sea has taken that rock)
                Begin(w, sid, Quarry, s.StoneEvent, CauseRel.EnabledBy);
            }
        }

        /// <summary>
        /// At harvest (C1.1): a store filled past its granaries' capacity wants another granary - unless a work is under way or a palisade is
        /// being raised (walls before more stores). Its cause is the harvest that overfilled it.
        /// </summary>
        public static void AfterHarvest(World w, int sid, long harvestEvent)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.HarvestEvent = harvestEvent;
            if (!s.Inhabited || s.Project != 0 || s.Granaries == 0 || s.Wall == 1) return;
            // (a village whose harvest alone would fill a granary, or whose store lies more than a granary's worth beyond its stores, raises
            // a barn - a great store for a great harvest; a small one another granary)
            double cap = StoreCapacity(in s);
            if (s.Granary > FullAtHarvest * cap) Begin(w, sid, s.Granary - cap > GranaryHolds || s.LastHarvest > GranaryHolds ? Barn : Granary, harvestEvent, CauseRel.CausedBy);
        }

        /// <summary>
        /// Lays out a structure of a kind and begins it as the village's work: a row in the table (not yet standing), its place chosen by its
        /// zone, its cause recorded; history tells it began and why.
        /// </summary>
        public static int Begin(World w, int sid, byte kind, long cause, CauseRel rel)
        {
            ref var s = ref w.Settlements.Get(sid);
            Place(w, sid, kind, out float x, out float y);
            int id = w.Ids.Next(EntityKind.Building);
            w.Buildings.Add(id, new Building { Id = id, Settlement = sid, X = x, Y = y, Kind = kind, Style = sid, Built = w.Now, Cond = 1f, Cause = cause });
            w.BuildingsOf(sid).InsertSorted(id);
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Project = id; s2.ProjectKind = kind;
            if (kind == Market) w.Buildings.Get(id).Stock = s2.TradeIn;   // (how many traders came the year it was laid out)
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.WorkBegun, Time = w.Now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = new EntityRef(EntityKind.Building, id), ArgA = kind, ArgB = s2.Granaries, Cause1 = cause, Rel1 = rel, RootKind = RootKind.People,
            });
            return id;
        }

        /// <summary>
        /// Labour put into the village's planned structure (Construction.Work): its hours and wood go in; the first to work on it is
        /// remembered as the one who led it; when its hours are in it stands - the Works cache, the store's capacity, history.
        /// </summary>
        public static void Raised(World w, int sid, int bid, double done, double wood, int worker)
        {
            ref var b = ref w.Buildings.Get(bid);
            b.Hours = (float)(b.Hours + done); b.Wood = (float)(b.Wood + wood);
            if (b.Leader == 0 && worker != 0) b.Leader = worker;
            var k = Of(b.Kind);
            if (b.Hours < k.Hours - Construction.Done) return;
            b.Done = 1;
            b.Form = Architecture.WorkForm(w, sid, bid);   // (built of what its people build with: D2-114)
            ref var s = ref w.Settlements.Get(sid);
            s.Project = 0; s.ProjectKind = 0;
            if (b.Kind == Well) s.Works |= Construction.Well;
            if (b.Kind == Granary) { s.Works |= Construction.Granary; s.Granaries++; }
            if (b.Kind == Barn) s.Barns++;
            if (b.Kind == Quarry) s.Quarry = bid;
            if (b.Kind == Mine) s.Mine = bid;
            if (b.Kind == CharHearth) s.CharHearth = bid;
            if (b.Kind == BogPits) s.FeMine = bid;
            if (b.Kind == Ground) s.Ground = bid;
            if (b.Kind == Market) s.Market = bid;
            if (b.Kind == BoatShed) s.Shed = bid;
            if (b.Kind == Smithy)
            {
                s.Smithy = bid;
                if (s.Stone >= Forge.HearthStone) { s.Stone -= Forge.HearthStone; b.Stone = (float)(b.Stone + Forge.HearthStone); b.Mat = 1; }   // (a hearth of stone when there is stone)
            }
            b.Event = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.WorkBuilt, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = new EntityRef(EntityKind.Building, bid), Other = b.Leader != 0 ? EntityRef.Person(b.Leader) : default,
                ArgA = b.Kind, ArgB = s.Granaries, RootKind = RootKind.People, Cause1 = b.Cause, Rel1 = CauseRel.CausedBy,
            });
        }

        /// <summary>A structure stands from the start (worldgen: the first villages' well and granary).</summary>
        public static int Standing(World w, int sid, byte kind, float x, float y)
        {
            int id = w.Ids.Next(EntityKind.Building);
            var k = Of(kind);
            w.Buildings.Add(id, new Building { Id = id, Settlement = sid, X = x, Y = y, Kind = kind, Style = sid, Built = w.Now, Cond = 1f, Done = 1, Hours = (float)k.Hours, Wood = (float)k.Wood });
            w.BuildingsOf(sid).InsertSorted(id);
            w.Buildings.Get(id).Form = Architecture.WorkForm(w, sid, id);   // (D2-114)
            ref var s = ref w.Settlements.Get(sid);
            if (kind == Well) s.Works |= Construction.Well;
            if (kind == Granary) { s.Works |= Construction.Granary; s.Granaries++; }
            if (kind == Barn) s.Barns++;
            if (kind == Quarry) s.Quarry = id;
            if (kind == Mine) s.Mine = id;
            if (kind == CharHearth) s.CharHearth = id;
            if (kind == BogPits) s.FeMine = id;
            if (kind == Smithy) s.Smithy = id;
            if (kind == Ground) s.Ground = id;
            if (kind == Market) s.Market = id;
            if (kind == BoatShed) s.Shed = id;
            return id;
        }

        /// <summary>A structure stops doing what it was for (the sea took it, it fell in): the Works cache and the store's capacity follow.</summary>
        public static void Lost(World w, int bid)
        {
            ref var b = ref w.Buildings.Get(bid);
            if (!w.Settlements.Exists(b.Settlement)) return;
            ref var s = ref w.Settlements.Get(b.Settlement);
            if (s.Project == bid) { s.Project = 0; s.ProjectKind = 0; return; }   // (an unfinished work abandoned)
            if (b.Done == 0) return;
            if (b.Kind == Granary && s.Granaries > 0) { s.Granaries--; if (s.Granaries == 0) s.Works = (byte)(s.Works & ~Construction.Granary); }
            if (b.Kind == Barn && s.Barns > 0) s.Barns--;
            if (b.Kind == Quarry && s.Quarry == bid) { s.Quarry = 0; s.StoneCell = 0; s.StoneDist = 0; }   // (the rock it stood at is gone: they must find rock again)
            if (b.Kind == Mine && s.Mine == bid) s.Mine = 0;
            if (b.Kind == CharHearth && s.CharHearth == bid) s.CharHearth = 0;
            if (b.Kind == BogPits && s.FeMine == bid) s.FeMine = 0;
            if (b.Kind == Smithy && s.Smithy == bid) s.Smithy = 0;
            if (b.Kind == Ground && s.Ground == bid) s.Ground = 0;
            if (b.Kind == Market && s.Market == bid) s.Market = 0;
            if (b.Kind == BoatShed && s.Shed == bid) s.Shed = 0;
            if (b.Kind == Well && !Has(w, b.Settlement, Well, bid)) s.Works = (byte)(s.Works & ~Construction.Well);
        }

        /// <summary>The village has another working structure of this kind besides `except`.</summary>
        static bool Has(World w, int sid, byte kind, int except)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == except) continue;
                ref var o = ref w.Buildings.Get(bl[i]);
                if (o.Kind == kind && Working(in o, w.Now)) return true;
            }
            return false;
        }

        /// <summary>
        /// The village is abandoned (D2-052): its works are left unkept from now and decay as its homes do; an unfinished work is dropped.
        /// </summary>
        public static void Abandon(World w, int sid)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (IsHome(b.Kind) || b.Left != 0) continue;
                b.Left = w.Now;
            }
            ref var s = ref w.Settlements.Get(sid);
            s.Project = 0; s.ProjectKind = 0;
        }

        // ---------------------------------------------------------------- where it goes (21 §4)

        /// <summary>
        /// Where a structure of a kind is laid out: the first well where the village's people drew water (its spot), the first granary
        /// by the square where the store's heap was (Construction.GranarySpot); later ones by their zone - Core round the square, Edge on
        /// the main road past the homes, Outer beyond the homes away from the fields - on dry ground clear of every other structure, lot and
        /// camp.
        /// </summary>
        public static void Place(World w, int sid, byte kind, out float x, out float y)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (kind == Well && !HasAny(w, sid, Well)) { x = s.WellX; y = s.WellY; return; }
            if (kind == Granary && !HasAny(w, sid, Granary)) { Construction.GranarySpot(in s, out double gx, out double gy); x = (float)gx; y = (float)gy; return; }
            if (kind == BogPits && s.FeCell > 0) { s.SphereToLocal(w.Planet.Grid.Center[s.FeCell - 1], w.Planet.Radius, out double bx, out double by); x = (float)bx; y = (float)by; return; }   // (at the bed)
            if (kind == CharHearth && Woods.Lot(in s) >= 0) { x = s.WoodX; y = s.WoodY; return; }   // (at the woods)
            if (kind == Mine && s.OreCell > 0) { s.SphereToLocal(w.Planet.Grid.Center[s.OreCell - 1], w.Planet.Radius, out double mx, out double my); x = (float)mx; y = (float)my; return; }   // (at the ore)
            if (kind == Quarry && s.StoneCell > 0) { s.SphereToLocal(w.Planet.Grid.Center[s.StoneCell - 1], w.Planet.Radius, out double qx, out double qy); x = (float)qx; y = (float)qy; return; }   // (at the rock)
            if (kind == BoatShed && Boats.ShedSpot(w, sid, out x, out y)) return;   // (at the head of the landing: C9.2b)
            var k = Of(kind);
            var P = w.Planet;
            double r0, dir, r1;
            switch (k.Zone)
            {
                case Zone.Edge: r0 = HomesReach(w, sid) + k.Footprint + 4.0; dir = MainRoad(w, sid); r1 = r0 + 40.0; break;
                case Zone.Outer: r0 = HomesReach(w, sid) + k.Footprint + 10.0; dir = DMath.Atan2(-(double)s.FieldY + s.SquareY, -(double)s.FieldX + s.SquareX); r1 = r0 + 60.0; break;
                default: r0 = 12.0 + k.Footprint; dir = 0.0; r1 = 70.0; break;
            }
            for (double r = r0; r <= r1; r += 3.0)
            {
                int steps = (int)DMath.Max(8.0, DMath.Floor(2.0 * DMath.PI * r / 4.0));
                for (int j = 0; j < steps; j++)
                {
                    // (from the zone's direction outward to both sides, so the nearest good spot to the road or the green comes first)
                    int side = (j & 1) == 0 ? 1 : -1;
                    double a = dir + side * ((j + 1) >> 1) * (2.0 * DMath.PI / steps);
                    double px = s.SquareX + r * DMath.Cos(a), py = s.SquareY + r * DMath.Sin(a);
                    if (Clear(w, sid, px, py, k.Footprint) && Shore.Dry(P, s.LocalToSphere(px, py, P.Radius))) { x = (float)px; y = (float)py; return; }
                }
            }
            x = (float)(s.SquareX + r1 * DMath.Cos(dir)); y = (float)(s.SquareY + r1 * DMath.Sin(dir));
        }

        static bool HasAny(World w, int sid, byte kind)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) if (w.Buildings.Get(bl[i]).Kind == kind) return true;
            return false;
        }

        /// <summary>Clearance a structure or lot keeps: a home's lot 6 m (D2-086), a work its footprint and a little more.</summary>
        public static double Clearance(byte kind) => IsHome(kind) ? 6.0 : Of(kind).Footprint + 3.0;

        /// <summary>No structure, ruin, lot of a home or camp within the clearance of either.</summary>
        public static bool Clear(World w, int sid, double px, double py, double footprint)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                double dx = (double)b.X - px, dy = (double)b.Y - py, need = footprint + Clearance(b.Kind);
                if (dx * dx + dy * dy < need * need) return false;
            }
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var h = ref w.Households.Get(hl[i]);
                if (h.Building != 0) continue;
                double dx = (double)h.HomeX - px, dy = (double)h.HomeY - py, need = footprint + 6.0;
                if (dx * dx + dy * dy < need * need) return false;
            }
            ref var s = ref w.Settlements.Get(sid);
            double qx = (double)s.SquareX - px, qy = (double)s.SquareY - py, sq = footprint + 8.0;   // (the green itself stays open)
            if (qx * qx + qy * qy < sq * sq) return false;
            if (Streets.On(in s, px, py, footprint + 1.0)) return false;   // (nor in a street: C13.1)
            return !OnRing(in s, px, py, footprint + 2.5);
        }

        /// <summary>Where the palisade's ring at angle a (radians, local frame) stands on dry ground (never in the sea or a lake: C1.6).</summary>
        public static bool RingDry(World w, in Settlement s, double r, double a)
        {
            var P = w.Planet;
            return Shore.Dry(P, s.LocalToSphere(s.SquareX + r * DMath.Cos(a), s.SquareY + r * DMath.Sin(a), P.Radius));
        }

        /// <summary>The share of a ring of this radius that stands on dry ground (72 points).</summary>
        public static double DryShare(World w, in Settlement s, double r)
        {
            int dry = 0;
            for (int k = 0; k < 72; k++) if (RingDry(w, in s, r, 2.0 * DMath.PI * (k + 0.5) / 72.0)) dry++;
            return dry / 72.0;
        }

        /// <summary>A spot lies across the village's palisade ring (standing or being raised): nothing is built astride its posts.</summary>
        public static bool OnRing(in Settlement s, double px, double py, double half)
        {
            double dx = px - s.SquareX, dy = py - s.SquareY, r = DMath.Sqrt(dx * dx + dy * dy);
            if (s.OldR > 0f && DMath.Abs(r - (double)s.OldR) < half) return true;   // (the old ring inside a grown town: C7.1)
            if (s.Wall != 1 && s.Wall != 2) return false;
            return DMath.Abs(r - (double)s.WallR) < half;
        }

        /// <summary>How far from the square the village's homes reach (the farthest standing home or camp).</summary>
        public static double HomesReach(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            double most = 20.0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (!IsHome(b.Kind) || Homes.Fallen(in b, w.Now)) continue;
                double dx = (double)b.X - s.SquareX, dy = (double)b.Y - s.SquareY, d = DMath.Sqrt(dx * dx + dy * dy);
                if (d > most) most = d;
            }
            return most;
        }

        /// <summary>The direction (radians, local frame) of the village's main road: toward its nearest inhabited neighbour.</summary>
        public static double MainRoad(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var P = w.Planet;
            int best = 0; double bd = double.MaxValue;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, P.Radius);
                if (d < bd) { bd = d; best = o; }
            }
            if (best == 0) return 0.0;
            s.SphereToLocal(w.Settlements.Get(best).Anchor, P.Radius, out double x, out double y);
            return DMath.Atan2(y - s.SquareY, x - s.SquareX);
        }
    }
}
