using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Scheduling;

namespace TC2.Core.Simulation
{
    /// <summary>Deterministic processing counters (14 §2). Saved for continuity, never hashed.</summary>
    public sealed class Metrics
    {
        public long Events, Decisions, Steps, Stale, Activities, Journeys, JourneysInterrupted, Interrupts, InterruptsHandled;
        public long Meals, Births, Deaths, Marriages, SystemTicks, FireSteps, Lightning, Commands;
        public readonly long[] Eruptions = new long[8]; // by VEI
        public long VentsBorn, VentsDied, Islands, EruptionDeaths;
        public readonly long[] Quakes = new long[10];   // by whole magnitude
        public long QuakeDeaths, Landslides, Tsunamis, TsunamiDeaths;
        public long Storms, GreatStorms, StormLandfalls, StormDeaths, Surges;
        public readonly long[] StormPeaks = new long[8];  // storms by their strongest wind, 10 m/s bins
        // the wild's food (D2-042): trips, hours and what came home (kg live game, kg fish, kg mast)
        public long HuntTrips, FishTrips, GatherTrips, HungerDeaths, Famines;
        public double GameKg, FishKg, GatherKg, WildHours, WildFu;
        public long Parties, Foundings, Scouts;   // households on the move (D2-045)

        public void Write(BinWriter w)
        {
            w.Long(Events); w.Long(Decisions); w.Long(Steps); w.Long(Stale); w.Long(Activities); w.Long(Journeys); w.Long(JourneysInterrupted);
            w.Long(Interrupts); w.Long(InterruptsHandled); w.Long(Meals); w.Long(Births); w.Long(Deaths); w.Long(Marriages);
            w.Long(SystemTicks); w.Long(FireSteps); w.Long(Lightning); w.Long(Commands);
            for (int i = 0; i < Eruptions.Length; i++) w.Long(Eruptions[i]);
            w.Long(VentsBorn); w.Long(VentsDied); w.Long(Islands); w.Long(EruptionDeaths);
            for (int i = 0; i < Quakes.Length; i++) w.Long(Quakes[i]);
            w.Long(QuakeDeaths); w.Long(Landslides); w.Long(Tsunamis); w.Long(TsunamiDeaths);
            w.Long(Storms); w.Long(GreatStorms); w.Long(StormLandfalls); w.Long(StormDeaths); w.Long(Surges);
            for (int i = 0; i < StormPeaks.Length; i++) w.Long(StormPeaks[i]);
            w.Long(HuntTrips); w.Long(FishTrips); w.Long(GatherTrips); w.Long(HungerDeaths); w.Long(Famines);
            w.Double(GameKg); w.Double(FishKg); w.Double(GatherKg); w.Double(WildHours); w.Double(WildFu);
            w.Long(Parties); w.Long(Foundings); w.Long(Scouts);
        }

        public void Read(BinReader r)
        {
            Events = r.Long(); Decisions = r.Long(); Steps = r.Long(); Stale = r.Long(); Activities = r.Long(); Journeys = r.Long(); JourneysInterrupted = r.Long();
            Interrupts = r.Long(); InterruptsHandled = r.Long(); Meals = r.Long(); Births = r.Long(); Deaths = r.Long(); Marriages = r.Long();
            SystemTicks = r.Long(); FireSteps = r.Long(); Lightning = r.Long(); Commands = r.Long();
            for (int i = 0; i < Eruptions.Length; i++) Eruptions[i] = r.Long();
            VentsBorn = r.Long(); VentsDied = r.Long(); Islands = r.Long(); EruptionDeaths = r.Long();
            for (int i = 0; i < Quakes.Length; i++) Quakes[i] = r.Long();
            QuakeDeaths = r.Long(); Landslides = r.Long(); Tsunamis = r.Long(); TsunamiDeaths = r.Long();
            Storms = r.Long(); GreatStorms = r.Long(); StormLandfalls = r.Long(); StormDeaths = r.Long(); Surges = r.Long();
            for (int i = 0; i < StormPeaks.Length; i++) StormPeaks[i] = r.Long();
            HuntTrips = r.Long(); FishTrips = r.Long(); GatherTrips = r.Long(); HungerDeaths = r.Long(); Famines = r.Long();
            GameKg = r.Double(); FishKg = r.Double(); GatherKg = r.Double(); WildHours = r.Double(); WildFu = r.Double();
            Parties = r.Long(); Foundings = r.Long(); Scouts = r.Long();
        }

        public Metrics Clone() { var m = (Metrics)MemberwiseClone(); return m; }
    }

    /// <summary>How much a host lets AdvanceTo do before returning (state is consistent between events).</summary>
    public struct Budget
    {
        public long MaxEvents;
        public long Done;
        public Func<bool> ShouldYield;   // polled every 256 events (wall-clock budgets live in the host)
        public static Budget Unlimited => new Budget { MaxEvents = long.MaxValue };
        public static Budget Events(long n) => new Budget { MaxEvents = n };
    }

    public enum CommandKind : byte { None = 0, GodLightning = 1, Note = 2, SetResolution = 3, GodEruption = 4, GodQuake = 5, GodStorm = 6 }

    /// <summary>How people are simulated (04 §4): every activity, or households by the day (deep time).</summary>
    public enum Resolution : byte { Activity = 0, Coarse = 1 }

    /// <summary>Optional host-side profiler (wall clock lives in the host; never affects state).</summary>
    public interface ISimProfiler
    {
        void Begin(int kind);
        void End(int kind);
    }

    /// <summary>A player (god) or test command: applied as an event at its time, logged forever.</summary>
    public struct Command
    {
        public long Time;
        public CommandKind Kind;
        public int Cell;
        public long Arg;
        public void Write(BinWriter w) { w.Long(Time); w.Byte((byte)Kind); w.Int(Cell); w.Long(Arg); }
        public static Command Read(BinReader r) => new Command { Time = r.Long(), Kind = (CommandKind)r.Byte(), Cell = r.Int(), Arg = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Time); h.Add((byte)Kind); h.Add(Cell); h.Add(Arg); }
    }

    /// <summary>
    /// The world root (03 §5): owns all state and the one event queue; the only way time passes is
    /// <see cref="AdvanceTo"/>, which processes events strictly in (time, phase, seq) order.
    /// </summary>
    public sealed class World
    {
        public const int FormatSimVersion = 12; // 12: building, knowing places, parties (D2-045), 11: relief, ice and animal densities (D2-043, D2-044), 2: volcanoes (D2-028), 3: quakes (D2-030), 4: erosion and storms (D2-031, D2-032), 5: wildlife (D2-035), 6: ice-aware weathering (D2-036), 7: evolution (D2-037), 8: crag fare (D2-038), 9: the sea's life (D2-039), 10: wild food, body reserves, famine memory (D2-042)

        public WorldConfig Config;
        public ulong Seed => Config.Seed;
        public long Now;
        public long Created;
        public Resolution Resolution;   // authoritative; changed only by a logged command
        public readonly IdAllocator Ids = new IdAllocator();
        public readonly EventQueue Queue = new EventQueue();
        public readonly FxRing Fx = new FxRing();   // for the view only: never read by the simulation, never saved
        public readonly HistoryLedger History = new HistoryLedger();
        public PlanetState Planet;
        public PlanetSystems PlanetSys;     // derived helpers (weather blocks, mean temperatures)
        public FireSystem Fire;
        public Drainage Drainage;           // derived from relief and sea level
        public Tectonics Tect;              // plate motion (scratch and frames; the plates live in the planet state)
        public readonly PagedStore<Settlement> Settlements = new PagedStore<Settlement>();
        public readonly PagedStore<Household> Households = new PagedStore<Household>();
        public readonly PagedStore<Person> People = new PagedStore<Person>();
        public readonly PagedStore<Party> Parties = new PagedStore<Party>();   // households on their way to a new home (D2-045)
        public readonly PagedStore<Herd> Herds = new PagedStore<Herd>();       // herds and packs near villages (D2-047)
        public readonly List<Command> CommandLog = new List<Command>();
        public readonly Metrics Metrics = new Metrics();
        public ISimProfiler Profiler;

        // systems (stateless logic bound to this world)
        public readonly AgentSystem Agents = new AgentSystem();
        public readonly SettlementSystem SettlementSys = new SettlementSystem();
        public readonly Hazards Hazards = new Hazards();
        public readonly WildFood Wild = new WildFood();
        public readonly Migration Migration = new Migration();
        public readonly HerdSystem HerdSys = new HerdSystem();

        // derived indexes (rebuilt after load; never serialized)
        public readonly RouteCache Routes = new RouteCache();
        public TravelIndex Travel;
        IntList[] _residents = new IntList[16];
        IntList[] _visitors = new IntList[16];
        IntList[] _hhOf = new IntList[16];
        IntList[] _members = new IntList[64];
        IntList[] _fieldCells = new IntList[16];
        int[] _cellSettlement;
        long[] _localOffset = new long[16];

        public World(WorldConfig config)
        {
            Config = config;
            Agents.Bind(this);
            SettlementSys.Bind(this);
            Hazards.Bind(this);
            Wild.Bind(this);
            Migration.Bind(this);
            HerdSys.Bind(this);
        }

        /// <summary>Allocates the planet-sized structures (worldgen and load call this).</summary>
        public void InitPlanet(PlanetState planet)
        {
            Planet = planet;
            Fire = new FireSystem(planet.Cells);
            Travel = new TravelIndex(planet.Cells);
            _cellSettlement = new int[planet.Cells];
            PlanetSys = new PlanetSystems(planet);
            Drainage = new Drainage(planet.Cells);
            Tect = new Tectonics(planet);
        }

        // ------------------------------------------------------------- history
        public long Record(HistoryEvent e) => History.Record(Ids, e);

        // ------------------------------------------------------------- lists
        static IntList Slot(ref IntList[] arr, int id)
        {
            if (id >= arr.Length) { int n = arr.Length; while (n <= id) n *= 2; Array.Resize(ref arr, n); }
            return arr[id] ?? (arr[id] = new IntList());
        }
        public IntList Residents(int sid) => Slot(ref _residents, sid);
        public IntList Visitors(int sid) => Slot(ref _visitors, sid);
        public IntList HouseholdsOf(int sid) => Slot(ref _hhOf, sid);
        public IntList Members(int hid) => Slot(ref _members, hid);
        public IntList FieldCellsOf(int sid) => Slot(ref _fieldCells, sid);

        public int SettlementAtCell(int cell) => _cellSettlement == null || cell < 0 || cell >= _cellSettlement.Length ? 0 : _cellSettlement[cell];

        /// <summary>Seconds to add to world time for local solar time at a settlement (cached).</summary>
        public long LocalOffsetOf(int sid) => _localOffset[sid];

        public void IndexSettlementCells(int sid)
        {
            ref var s = ref Settlements.Get(sid);
            if (sid >= _localOffset.Length) { int n = _localOffset.Length; while (n <= sid) n *= 2; Array.Resize(ref _localOffset, n); }
            _localOffset[sid] = PlanetState.LocalOffset(s.Anchor);
            _cellSettlement[s.Cell] = sid;
            if (s.FieldCell >= 0 && _cellSettlement[s.FieldCell] == 0) _cellSettlement[s.FieldCell] = sid;
        }

        public string PlaceName(int cell)
        {
            int best = 0; double bd = double.MaxValue;
            Vec3d p = Planet.Grid.Center[cell];
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                double d = Vec3d.ArcDistance(p, Settlements.Get(sid).Anchor, Planet.Radius);
                if (d < bd) { bd = d; best = sid; }
            }
            if (best == 0) return "the wilds";
            string n = History.NameOf(EntityRef.Settlement(best));
            return bd < 600 ? n : "the land beyond " + n;
        }

        public string FamilyName(int householdKey) => History.NameOf(EntityRef.Household(householdKey));

        public bool FireNearSettlement(int sid, double range)
        {
            if (Fire.Active.Count == 0) return false;
            ref var s = ref Settlements.Get(sid);
            double lim = range + Planet.CellSize * 0.5;
            for (int i = 0; i < Fire.Active.Count; i++)
                if (Vec3d.ArcDistance(Planet.Grid.Center[Fire.Active[i]], s.Anchor, Planet.Radius) <= lim) return true;
            return false;
        }

        /// <summary>A neighbouring inhabited settlement within a day's easy walk, weighted by nearness.</summary>
        public int PickNeighbour(int sid, ref RandomSequence rs)
        {
            ref var s = ref Settlements.Get(sid);
            double total = 0;
            int n = 0;
            for (int o = Settlements.First(); o != 0; o = Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, Planet.Radius);
                if (d > 6000) continue;
                total += 1.0 / (300.0 + d); n++;
            }
            if (n == 0) return 0;
            double u = rs.NextUnit() * total;
            for (int o = Settlements.First(); o != 0; o = Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, Planet.Radius);
                if (d > 6000) continue;
                u -= 1.0 / (300.0 + d);
                if (u <= 0) return Routes.Get(this, sid, o) != null ? o : 0;
            }
            return 0;
        }

        public void Settlements_FireOnCell(int cell, int proc) => SettlementSys.FireOnCell(cell, proc);

        // ------------------------------------------------------------- commands
        /// <summary>Queues a command at a time not before now; returns its index in the log.</summary>
        public int Issue(Command c)
        {
            if (c.Time < Now) c.Time = Now;
            CommandLog.Add(c);
            int idx = CommandLog.Count - 1;
            Queue.Push(c.Time, Phase.Command, EventKind.Command, 0, 0, idx);
            return idx;
        }

        void ApplyCommand(int idx)
        {
            var c = CommandLog[idx];
            Metrics.Commands++;
            switch (c.Kind)
            {
                case CommandKind.SetResolution:
                    SetResolutionNow((Resolution)c.Arg);
                    break;
                case CommandKind.GodLightning:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        Strike(c.Cell, ev, RootKind.You, 1.6);
                        break;
                    }
                case CommandKind.GodStorm:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        // the god raises a storm at the place (its winds as asked, 50 m/s by default); it lives as storms do
                        Vec3d at = Planet.Grid.Center[c.Cell];
                        TC2.Core.Planet.Storms.Frame(at, out Vec3d east, out Vec3d north);
                        bool had = Planet.Storms.Count > 0;
                        float wind = c.Arg > 0 ? c.Arg : 50f;
                        Planet.Storms.Add(new TC2.Core.Planet.Storm
                        {
                            Id = Ids.Next(EntityKind.Process), At = at, Heading = (east * -0.95 + north * (at.Y >= 0 ? 0.3 : -0.3)).Normalized, Speed = 0.05f,
                            Wind = wind, Peak = wind, Radius = 2000f, Kind = 0, Born = Now, Cause = ev,
                        });
                        Metrics.Storms++;
                        if (!had) Queue.Push(Now + TC2.Core.Planet.Storms.StepSeconds, Phase.Process, EventKind.StormStep, 0);
                        break;
                    }
                case CommandKind.GodQuake:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        GodQuake(c.Cell, ev, c.Arg > 0 ? c.Arg : 7);
                        break;
                    }
                case CommandKind.GodEruption:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        GodEruption(c.Cell, ev, (int)c.Arg);
                        break;
                    }
            }
        }

        void SetResolutionNow(Resolution r)
        {
            if (r == Resolution) return;
            if (r == Resolution.Coarse) Agents.EnterCoarse();
            Resolution = r;
            if (r == Resolution.Activity) Agents.LeaveCoarse();
        }

        /// <summary>Queues a resolution change (a logged command: replays and saves agree).</summary>
        double _landM2 = -1;

        double LandM2()
        {
            if (_landM2 >= 0) return _landM2;
            double a = 0;
            for (int c = 0; c < Planet.Cells; c++) if (Planet.IsLand(c)) a += Planet.AreaHa(c) * 10000.0;
            return _landM2 = a;
        }

        /// <summary>After a season of climate: a coast that moved rebuilds the drainage; ages of ice enter the history.</summary>
        public void AfterClimate()
        {
            var P = Planet;
            if (DMath.Abs(P.SeaLevelNow - P.SeaLevel) >= 1.0) CoastMoved();
            double mean = P.IceVolume / DMath.Max(1.0, LandM2());
            // the planet's normal ice: a running mean of the last ~1,500 years, from the ice it was born with (D2-043) - ages of
            // ice are advances beyond what the world has lately known, not beyond one moment of its orbit
            P.IceRef += (P.IceVolume - P.IceRef) * Climate.StepYears / 1500.0;
            double rel = P.IceVolume / DMath.Max(1e6, P.IceRef);
            if (P.Glacial == 0 && rel > 1.35)
            {
                P.Glacial = 1;
                Record(new HistoryEvent { Kind = HistoryKind.IceAge, Time = Now, Cell = -1, RootKind = RootKind.Nature, Magnitude = (float)mean });
            }
            else if (P.Glacial == 1 && rel < 1.1)
            {
                P.Glacial = 0;
                Record(new HistoryEvent { Kind = HistoryKind.IceAgeEnded, Time = Now, Cell = -1, RootKind = RootKind.Nature, Magnitude = (float)mean });
            }
        }

        /// <summary>The sea rose or fell past the coast the drainage was built for: land use, waters and routes follow.</summary>
        void CoastMoved()
        {
            var P = Planet;
            double before = P.SeaLevel;
            P.SeaLevel = P.SeaLevelNow;
            LandChanged(before, true);
        }

        /// <summary>A step of the plates: the crust moves, boundaries rise and sink, villages ride their plate.</summary>
        public void TectonicStep()
        {
            var P = Planet;
            if (P.Plates.Length == 0) return;
            RideSettlements(Tectonics.StepYears); // with the motion the crust has during this step
            Volcanism.Ride(this, Tectonics.StepYears);
            var bud = ReliefBudget;   // tools only: the change of the continents' summed elevation by process (null in the game)
            double b0 = bud != null ? ContinentSum(P) : 0;
            Tect.Step(P, Tectonics.StepYears, Seed, Now);
            double b1 = bud != null ? ContinentSum(P) : 0;
            Volcanism.Step(this, Tectonics.StepYears, Tect.ArcSupply);
            double b2 = bud != null ? ContinentSum(P) : 0;
            TC2.Core.Planet.Quakes.Step(this, Tectonics.StepYears, Tect.Slip);
            double b3 = bud != null ? ContinentSum(P) : 0;
            // rivers cut and carry on the tree of the moved relief (D2-031); the tree is rebuilt again for what they changed
            Drainage.Rebuild(P);
            Erosion.Step(this, Tectonics.StepYears);
            double b4 = bud != null ? ContinentSum(P) : 0;
            Coast.Step(this, Tectonics.StepYears); // waves wear headlands, drift fills inlets (D2-034)
            if (bud != null) { double b5 = ContinentSum(P); bud[0] += b1 - b0; bud[1] += b2 - b1; bud[2] += b3 - b2; bud[3] += b4 - b3; bud[4] += b5 - b4; }
            Evolution.Speciate(this);              // isolated, diverged populations become species (D2-037)
            LandChanged(P.SeaLevel, false);
            P.ReliefVersion++;
        }

        /// <summary>Tools only (relief experiments): summed elevation change of continental crust by process - plates, volcanoes,
        /// quakes, erosion, coasts - accumulated over tectonic steps. Null in the game and the tests.</summary>
        public static double[] ReliefBudget;

        static double ContinentSum(PlanetState P)
        {
            double s = 0;
            for (int c = 0; c < P.Cells; c++) if (P.Crust[c] == 1) s += P.Elev[c];
            return s;
        }

        void RideSettlements(double dt)
        {
            var P = Planet;
            bool moved = false;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                int pl = P.Plate[s.Cell];
                var plate = P.Plates[pl];
                double a = plate.Omega * dt, cs = DMath.Cos(a), sn = DMath.Sin(a);
                s.Anchor = Tectonics.Rotate(s.Anchor, plate.Pole, cs, sn);
                s.East = Tectonics.Rotate(s.East, plate.Pole, cs, sn);
                s.North = Tectonics.Rotate(s.North, plate.Pole, cs, sn);
                int cell = P.Grid.CellOf(s.Anchor);
                int fieldCell = P.Grid.CellOf(s.LocalToSphere(s.FieldX, s.FieldY, P.Radius));
                if (cell != s.Cell || fieldCell != s.FieldCell) { s.Cell = cell; s.FieldCell = fieldCell; }
                moved = true;
            }
            if (!moved) return;
            for (int i = 0; i < _cellSettlement.Length; i++) _cellSettlement[i] = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) IndexSettlementCells(sid);
            Routes.Invalidate();
            Agents.RebuildTravelIndex(); // legs follow the new routes, exactly as a loaded world registers them
        }

        /// <summary>
        /// Land and sea changed under the drainage (the coastline or the relief moved): drowned land loses its use,
        /// new land starts bare, the waters, climate terms, biomes and routes follow, and the history tells of it.
        /// </summary>
        void LandChanged(double coastBefore, bool record)
        {
            var P = Planet;
            int drowned = 0, emerged = 0, villages = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                bool was = Drainage.BuiltLand(c), now = P.IsLand(c);
                if (was == now) continue;
                if (was)
                {
                    drowned++;
                    if (P.FieldOwner[c] != 0) { FieldCellsOf(P.FieldOwner[c]).Remove(c); P.FieldOwner[c] = 0; }
                    if (SettlementAtCell(c) != 0) villages++;
                    P.Forest[c] = 0; P.Grass[c] = 0; P.Ice[c] = 0; P.LakeWater[c] = 0; P.SoilWater[c] = 1; P.Gathered[c] = 0;
                }
                else { emerged++; P.SoilWater[c] = 0.6f; P.Grass[c] = 0.02f; P.Forest[c] = 0; }
            }
            Drainage.Rebuild(P);
            Drainage.SettleStored(P);
            PlanetSys.Climate.Derive(P);
            PlanetSys.RefreshMeans(P);
            for (int c = 0; c < P.Cells; c++) P.ClassifyBiome(c, Now);
            Routes.Invalidate();
            _landM2 = -1;
            if (drowned + emerged > 0) P.CoastVersion++;
            if (record && drowned + emerged > 0)
                Record(new HistoryEvent { Kind = HistoryKind.CoastChanged, Time = Now, Cell = -1, RootKind = RootKind.Nature, Magnitude = (float)(P.SeaLevel - coastBefore), ArgA = drowned, ArgB = emerged * 1000 + villages });
        }

        int NearestSettlement(int cell, double maxM)
        {
            int best = 0; double bd = maxM;
            Vec3d at = Planet.Grid.Center[cell];
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double d = Vec3d.ArcDistance(at, s.Anchor, Planet.Radius);
                if (d < bd) { bd = d; best = sid; }
            }
            return best;
        }

        public long RecordVentBorn(Vent v, long cause = 0)
        {
            Metrics.VentsBorn++;
            History.Register(new EntityRef(EntityKind.Place, v.Id), Worldgen.Names.Mountain(Seed, v.Id), Now);
            return Record(new HistoryEvent { Kind = HistoryKind.VentBorn, Time = Now, Cell = v.Cell, Settlement = NearestSettlement(v.Cell, 5000), RootKind = RootKind.Nature, ArgA = v.Id, ArgB = v.Kind, Subject = new EntityRef(EntityKind.Place, v.Id), Cause1 = cause });
        }

        /// <summary>
        /// The god wakes a volcano (R7): the nearest vent within 2.5 km erupts now with at least twice its critical
        /// pressure (its own seal decides how big); with none near, the ground splits and a new vent opens there.
        /// Every consequence roots in the command.
        /// </summary>
        void GodEruption(int cell, long cause, int vei = 0)
        {
            var P = Planet;
            int best = -1; double bd = 2500.0;
            Vec3d at = P.Grid.Center[cell];
            for (int v = 0; v < P.Vents.Count; v++)
            {
                double d = Vec3d.ArcDistance(P.Vents[v].At, at, P.Radius);
                if (d < bd) { bd = d; best = v; }
            }
            long why = cause;
            if (best < 0)
            {
                int id = Ids.Next(EntityKind.Place);
                P.Vents.Add(new Vent { Id = id, Cell = cell, At = at, Plate = P.Plate[cell], Kind = 0, Born = Now, Seal = 1f });
                best = P.Vents.Count - 1;
                why = RecordVentBorn(P.Vents[best], cause);
            }
            var vt = P.Vents[best];
            vt.Pressure = DMath.Max(vt.Pressure, 2.0 * Volcanism.PressureCrit * vt.Seal);
            if (vei > 0) vt.Pressure = DMath.Max(vt.Pressure, Volcanism.PressureFor(vei)); // a size asked for
            P.Vents[best] = vt;
            Erupt(vt.Id, why);
        }

        public void RecordSpeciation(int child, int parent, int cell)
        {
            var P = Planet;
            Record(new HistoryEvent
            {
                Kind = HistoryKind.Speciation, Time = Now, Cell = cell, Settlement = cell >= 0 ? NearestSettlement(cell, 9000) : 0, RootKind = RootKind.Nature,
                Subject = new EntityRef(EntityKind.Species, P.Species[child].Id), Object = new EntityRef(EntityKind.Species, P.Species[parent].Id),
                Magnitude = P.Species[child].ColdTol - P.Species[parent].ColdTol,
            });
        }

        public void RecordExtinction(int s)
        {
            var id = new EntityRef(EntityKind.Species, Planet.Species[s].Id);
            Record(new HistoryEvent { Kind = HistoryKind.Extinction, Time = Now, Cell = -1, RootKind = RootKind.Nature, Subject = id });
            History.EndEntity(id, Now);
        }

        public void RecordIslandRose(Vent v)
        {
            Metrics.Islands++;
            Record(new HistoryEvent { Kind = HistoryKind.IslandRose, Time = Now, Cell = v.Cell, RootKind = RootKind.Nature, ArgA = v.Id, Subject = new EntityRef(EntityKind.Place, v.Id) });
        }

        public void RecordVentExtinct(Vent v)
        {
            Metrics.VentsDied++;
            Record(new HistoryEvent { Kind = HistoryKind.VentExtinct, Time = Now, Cell = v.Cell, Settlement = NearestSettlement(v.Cell, 5000), RootKind = RootKind.Nature, ArgA = v.Id, ArgB = v.Eruptions, Subject = new EntityRef(EntityKind.Place, v.Id) });
            History.EndEntity(new EntityRef(EntityKind.Place, v.Id), Now);
        }

        /// <summary>
        /// A volcano erupts (D2-028): its size from the pressure released; the cone grows; lava runs down the steepest
        /// way and sets the land alight; ash falls round it on woods and fields; near a great eruption people die; the veil
        /// cools the world and the gas warms it a little - each consequence caused by the eruption.
        /// </summary>
        public void Erupt(int ventId, long cause = 0)
        {
            var P = Planet;
            var g = P.Grid;
            int v = Volcanism.IndexOf(P, ventId);
            if (v < 0) return;
            var vt = P.Vents[v];
            int vei = Volcanism.Vei(vt.Pressure);
            int c = vt.Cell;
            Metrics.Eruptions[vei]++;
            long ev = Record(new HistoryEvent
            {
                Kind = HistoryKind.Eruption, Time = Now, Cell = c, Settlement = NearestSettlement(c, 8000), RootKind = RootKind.Nature, Cause1 = cause,
                Tier = vei >= 4 ? Tier.Historical : Tier.Local, Magnitude = vei, ArgA = vt.Id, Subject = new EntityRef(EntityKind.Place, vt.Id),
            });
            // the cone it piles up joins the relief at the next tectonic step, where the drainage is rebuilt
            vt.Cone += (float)(22.0 * vei);
            // lava down the steepest way
            int cur = c, length = 1 + (vei * 3) / 2;
            for (int k = 0; k < length; k++)
            {
                int next = -1; float low = P.Elev[cur];
                for (int q = 0; q < 8; q++) { int nb = g.Nbr8[cur * 8 + q]; if (P.Elev[nb] < low) { low = P.Elev[nb]; next = nb; } }
                if (next < 0 || !P.IsLand(next)) break;
                cur = next;
                Fx.Add(Now, cur, FxKind.Lava, vei);
                Fire.TryIgnite(this, cur, ev, RootKind.Nature, 6.0);
                if (P.Forest[cur] > 0.02f) P.Forest[cur] = 0.02f;
                if (P.Grass[cur] > 0.05f) P.Grass[cur] = 0.05f;
                if (P.FieldOwner[cur] != 0) SettlementSys.AshOnFields(P.FieldOwner[cur], 1.0, ev);
            }
            // ash round the vent: woods and pasture suffer, and the crops of every village whose fields lie under it
            double ashR = 280.0 * DMath.Exp((vei - 2) * 0.6931);
            double ashCos = DMath.Cos(ashR / P.Radius);
            Vec3d vat = g.Center[c];
            var hit = new List<int>();
            for (int a = 0; a < P.Cells; a++)
            {
                if (!P.IsLand(a) || Vec3d.Dot(g.Center[a], vat) < ashCos) continue;
                P.Grass[a] *= 0.7f; P.Forest[a] *= 0.93f;
                int owner = P.FieldOwner[a];
                if (owner != 0 && !hit.Contains(owner)) hit.Add(owner);
            }
            foreach (int sid in hit) SettlementSys.AshOnFields(sid, DMath.Min(0.9, 0.12 * (vei - 1)), ev);
            // near a great eruption the clouds of hot ash kill
            if (vei >= 4)
            {
                double deadR = 350.0 * (vei - 3);
                Vec3d at = g.Center[c];
                for (int id = People.First(); id != 0; id = People.Next(id))
                {
                    ref var p = ref People.Get(id);
                    if (!p.Alive) continue;
                    if (Vec3d.ArcDistance(Agents.PositionAt(p, Now), at, P.Radius) > deadR) continue;
                    if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 51) < 0.55) { Agents.Kill(ref p, ev, 4); Metrics.EruptionDeaths++; }
                }
            }
            // the veil (cooling for a few years) and the gas
            P.Aerosol += 0.15 * DMath.Exp((vei - 6) * 1.61); // a VEI 6 (Pinatubo-like) veil ~0.15, a VEI 7 ~0.75
            vt.Pressure = 0; vt.LastEruption = Now; vt.Eruptions++; vt.Next = 0;
            P.Vents[v] = vt;
        }

        List<int> _rupture, _shaken;
        int[] _qsrc, _qstamp; int _qstampNow;

        /// <summary>
        /// A fault patch ruptures (D2-030): the cascade along the fault gives the quake's size; where it shakes, people
        /// indoors die as houses fall (most at night), stores spill, steep slopes slide, and a great quake under the sea
        /// sends a wave over low coasts - each with the quake as its cause. Small harmless quakes leave no history.
        /// </summary>
        /// <summary>The god shakes the earth (R7): a fault line through the cell loaded to the brink and broken at once - long
        /// enough for the magnitude asked for (one patch ~5.5, each x5 in length +1 M).</summary>
        void GodQuake(int cell, long cause, long magnitude)
        {
            var P = Planet;
            var g = P.Grid;
            int n = (int)DMath.Clamp(DMath.RoundHalfUp(DMath.Exp((magnitude - 5.5) / 1.4 * DMath.Log(10.0))), 1, 200);
            int c = cell;
            for (int i = 0; i < n; i++) { P.Stress[c] = (float)(TC2.Core.Planet.Quakes.Threshold * 1.01); c = g.Nbr4[c * 4 + (i % 3 == 2 ? 1 : 0)]; }
            Quake(cell, cause);
        }

        public void Quake(int c0, long cause = 0)
        {
            var P = Planet;
            var g = P.Grid;
            var rup = _rupture ?? (_rupture = new List<int>());
            double released = TC2.Core.Planet.Quakes.Cascade(this, c0, rup);
            if (rup.Count == 0) return;
            double M = TC2.Core.Planet.Quakes.Magnitude(released);
            Metrics.Quakes[(int)DMath.Clamp(DMath.Floor(M), 0, 9)]++;
            Fx.Add(Now, c0, FxKind.Quake, (float)M);
            // where it shook: a BFS out from the rupture to the reach of intensity 6, each cell knowing its nearest rupture
            double reach = TC2.Core.Planet.Quakes.Reach(M);
            if (_qsrc == null || _qsrc.Length != P.Cells) { _qsrc = new int[P.Cells]; _qstamp = new int[P.Cells]; _qstampNow = 0; }
            _qstampNow++;
            var shaken = _shaken ?? (_shaken = new List<int>());
            shaken.Clear();
            int sea = 0;
            foreach (int c in rup) { _qstamp[c] = _qstampNow; _qsrc[c] = c; shaken.Add(c); if (!P.IsLand(c)) sea++; }
            for (int head = 0; head < shaken.Count; head++)
            {
                int c = shaken[head];
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (_qstamp[nb] == _qstampNow) continue;
                    if (Vec3d.ArcDistance(g.Center[nb], g.Center[_qsrc[c]], P.Radius) > reach) continue;
                    _qstamp[nb] = _qstampNow; _qsrc[nb] = _qsrc[c];
                    shaken.Add(nb);
                }
            }
            long ev = 0;
            long Cause()
            {
                if (ev == 0)
                    ev = Record(new HistoryEvent
                    {
                        Kind = HistoryKind.Quake, Time = Now, Cell = c0, Settlement = NearestSettlement(c0, 8000), RootKind = RootKind.Nature, Cause1 = cause,
                        Tier = M >= 7.0 ? Tier.Historical : Tier.Local, Magnitude = (float)M, ArgA = rup.Count,
                    });
                return ev;
            }
            if (M >= TC2.Core.Planet.Quakes.MinRecord || cause != 0) Cause();
            double Shake(int c) => _qstamp[c] == _qstampNow ? TC2.Core.Planet.Quakes.Intensity(M, Vec3d.ArcDistance(g.Center[c], g.Center[_qsrc[c]], P.Radius)) : 0.0;

            // people: indoors, houses fall on them (most are indoors at night)
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                Vec3d at = Agents.PositionAt(p, Now);
                double mmi = Shake(g.CellOf(at));
                if (mmi <= 7.5) continue;
                long local = SimTime.FloorMod(Now + PlanetState.LocalOffset(at), SimTime.Day);
                bool night = local < 6 * SimTime.Hour || local >= 21 * SimTime.Hour;
                bool indoors = KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 61) < (night ? 0.9 : 0.35);
                double x = mmi - 7.5;
                double risk = (indoors ? 0.04 : 0.008) * x * x;
                if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 62) < risk) { Agents.Kill(ref p, Cause(), 5); Metrics.QuakeDeaths++; }
            }
            // stores spill and spoil
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double mmi = Shake(s.Cell);
                if (mmi < 7.0 || s.Granary <= 0) continue;
                double loss = DMath.Min(0.5, 0.08 * (mmi - 6.0));
                s.Granary *= 1.0 - loss;
                Record(new HistoryEvent { Kind = HistoryKind.StoresSpoiled, Time = Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Cause1 = Cause(), Rel1 = CauseRel.CausedBy, Magnitude = (float)loss });
            }
            // steep shaken slopes slide: woods and pasture stripped, rock moved down (a slide can dam a river)
            int slides = 0, near = 0, nearSid = 0;
            foreach (int c in shaken)
            {
                if (!P.IsLand(c)) continue;
                double mmi = Shake(c);
                if (mmi < 7.0) continue;
                int low = c; float le = P.Elev[c];
                for (int k = 0; k < 8; k++) { int nb = g.Nbr8[c * 8 + k]; if (P.Elev[nb] < le) { le = P.Elev[nb]; low = nb; } }
                if (low == c) continue;
                double slope = (P.Elev[c] - le) / P.CellSize;
                if (slope < 0.8) continue;
                double pr = DMath.Min(0.9, 0.08 * (mmi - 6.5) * DMath.Min(2.0, slope / 0.8));
                if (KeyedRng.Unit(Seed, RngStream.Tectonics, (ulong)c, (ulong)Now, 74) >= pr) continue;
                double drop = DMath.Min(40.0, 0.15 * (P.Elev[c] - le));
                P.Elev[c] = (float)(P.Elev[c] - drop);
                P.Elev[low] = (float)(P.Elev[low] + 0.9 * drop);
                if (P.Forest[c] > 0.05f) P.Forest[c] = 0.05f;
                if (P.Grass[c] > 0.1f) P.Grass[c] = 0.1f;
                if (P.Forest[low] > 0.2f) P.Forest[low] = 0.2f;
                slides++;
                Fx.Add(Now, c, FxKind.Landslide, (float)drop);
                int sid = NearestSettlement(c, 1500);
                if (sid != 0) { near++; nearSid = sid; }
            }
            if (slides > 0)
            {
                Metrics.Landslides += slides;
                if (near > 0 || M >= TC2.Core.Planet.Quakes.MinRecord)
                    Record(new HistoryEvent { Kind = HistoryKind.Landslides, Time = Now, Cell = c0, Settlement = nearSid != 0 ? nearSid : NearestSettlement(c0, 8000), Cause1 = Cause(), Rel1 = CauseRel.CausedBy, ArgA = slides });
                LandChanged(P.SeaLevel, false);
                P.ReliefVersion++;
            }
            if (sea * 2 > rup.Count && M >= 7.0) Tsunami(M, c0, Cause());
        }

        /// <summary>
        /// A wave from a great quake under the sea (D2-030): run-up 15 m (sim) at M 7, x3.2 per magnitude, weakening with
        /// distance; low land within two cells of the sea floods - woods and pasture spoiled, the standing crop of
        /// villages whose fields lie there lost, people there drown.
        /// </summary>
        void Tsunami(double M, int c0, long cause)
        {
            var P = Planet;
            var g = P.Grid;
            double h0 = 15.0 * DMath.Exp(0.5 * (M - 7.0) * DMath.Log(10.0));
            double reach = 4000.0 * DMath.Exp(0.5 * (M - 7.0) * DMath.Log(10.0));
            Vec3d at0 = g.Center[c0];
            double cosR = DMath.Cos(reach / P.Radius);
            var flooded = new List<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || P.LakeDepth[c] > 0.3f || Vec3d.Dot(g.Center[c], at0) < cosR) continue;
                // how near the sea: next to it, or one cell further in
                int coast = 2;
                for (int k = 0; k < 8 && coast > 0; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (!P.IsLand(nb)) coast = 0;
                    else if (coast > 1) for (int j = 0; j < 8; j++) if (!P.IsLand(g.Nbr8[nb * 8 + j])) { coast = 1; break; }
                }
                if (coast > 1) continue;
                double d = Vec3d.ArcDistance(g.Center[c], at0, P.Radius);
                double h = h0 * DMath.Sqrt(1500.0 / (d + 1500.0)) * (coast == 0 ? 1.0 : 0.5);
                if (P.Elev[c] - P.SeaLevel >= h) continue;
                flooded.Add(c);
            }
            if (flooded.Count == 0) return;
            Metrics.Tsunamis++;
            var hit = new Dictionary<int, int>();
            foreach (int c in flooded) { int o = P.FieldOwner[c]; if (o != 0) hit[o] = hit.TryGetValue(o, out int n) ? n + 1 : 1; }
            int most = 0, mostN = 0;
            foreach (var kv in hit) if (kv.Value > mostN || (kv.Value == mostN && kv.Key < most)) { most = kv.Key; mostN = kv.Value; }
            long wave = Record(new HistoryEvent
            {
                Kind = HistoryKind.Tsunami, Time = Now, Cell = flooded[0], Settlement = most != 0 ? most : NearestSettlement(c0, 12000),
                Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)h0, ArgA = flooded.Count,
            });
            var mark = new HashSet<int>(flooded);
            foreach (int c in flooded) { P.Forest[c] *= 0.5f; P.Grass[c] *= 0.3f; Fx.Add(Now, c, FxKind.Wave, (float)h0); }
            var sids = new List<int>(hit.Keys);
            sids.Sort();
            foreach (int sid in sids)
            {
                if (!Settlements.Exists(sid)) continue;
                int fc = Settlements.Get(sid).FieldCells;
                SettlementSys.CropLoss(sid, DMath.Min(1.0, hit[sid] / (double)DMath.Max(1, fc)), wave, HistoryKind.FieldsFlooded);
            }
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                if (!mark.Contains(g.CellOf(Agents.PositionAt(p, Now)))) continue;
                if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 63) < 0.5) { Agents.Kill(ref p, wave, 6); Metrics.TsunamiDeaths++; }
            }
        }

        /// <summary>
        /// Every storm moves and acts (D2-032): rain for the next day's weather, blowdowns in woods, crops flattened, people
        /// caught out in the worst winds, a surge over low coasts where a strong storm comes ashore - each consequence
        /// caused by the storm's history event (recorded once it is a great storm or first does harm).
        /// </summary>
        public void StormStep()
        {
            var P = Planet;
            var g = P.Grid;
            double dt = TC2.Core.Planet.Storms.StepSeconds;
            for (int i = 0; i < P.Storms.Count; i++)
            {
                var st = P.Storms[i];
                bool wasLand = (st.Flags & 1) != 0;
                TC2.Core.Planet.Storms.Move(P, ref st, dt, Now);
                int cc = g.CellOf(st.At);
                bool land = P.IsLand(cc);
                st.Flags = (byte)(land ? st.Flags | 1 : st.Flags & ~1);
                long Cause()
                {
                    if (st.Event == 0)
                    {
                        st.Event = Record(new HistoryEvent
                        {
                            Kind = HistoryKind.Storm, Time = Now, Cell = cc, Settlement = NearestSettlement(cc, 8000), RootKind = RootKind.Nature,
                            Magnitude = st.Wind, ArgA = st.Kind, Subject = new EntityRef(EntityKind.Process, st.Id), Cause1 = st.Cause,
                        });
                    }
                    return st.Event;
                }
                if (st.Wind >= TC2.Core.Planet.Storms.GreatWind && (st.Flags & 4) == 0) { st.Flags |= 4; Metrics.GreatStorms++; Cause(); }
                if (land && !wasLand && st.Wind >= 25f)
                {
                    Metrics.StormLandfalls++;
                    Record(new HistoryEvent
                    {
                        Kind = HistoryKind.StormLandfall, Time = Now, Cell = cc, Settlement = NearestSettlement(cc, 8000), Cause1 = Cause(), Rel1 = CauseRel.PartOf,
                        Tier = st.Wind >= 50f ? Tier.Historical : Tier.Local, Magnitude = st.Wind, Subject = new EntityRef(EntityKind.Process, st.Id),
                    });
                    st.Flags |= 2;
                    if (st.Wind >= 35f) Surge(st, Cause());
                }
                // the winds and the rain over everything within the storm's reach (a BFS out from its centre)
                double cosR = DMath.Cos(st.Radius / P.Radius);
                var crops = new SortedDictionary<int, double>();
                var reach = StormCells(cc, st.At, cosR);
                for (int ri = 0; ri < reach.Count; ri++)
                {
                    int c = reach[ri];
                    double dot = Vec3d.Dot(g.Center[c], st.At);
                    double r = DMath.Acos(DMath.Min(1.0, dot)) * P.Radius;
                    double wv = TC2.Core.Planet.Storms.WindAt(st, r);
                    double rf = 1.0 - r / st.Radius;
                    P.StormRain[c] = (float)(P.StormRain[c] + 1.0 * (st.Wind / 35.0) * rf * DMath.Sqrt(rf));
                    if (!P.IsLand(c)) continue;
                    if (wv > 32.0) { double x = wv - 32.0; P.Forest[c] = (float)(P.Forest[c] * (1.0 - DMath.Min(0.6, 0.0006 * x * x))); }
                    int owner = P.FieldOwner[c];
                    if (owner != 0 && wv > 25.0) crops[owner] = crops.TryGetValue(owner, out double m) ? DMath.Max(m, wv) : wv;
                }
                foreach (var kv in crops)
                {
                    double share = DMath.Min(0.6, 0.004 * (kv.Value - 25.0));
                    if (share < 0.01) continue;
                    // one record per storm and village: later steps of the same storm add to the loss quietly
                    bool told = false;
                    var evs = History.EventsOf(EntityRef.Settlement(kv.Key));
                    if (evs != null)
                        for (int k = evs.Count - 1; k >= 0 && k >= evs.Count - 12; k--)
                            if (History.TryGet(evs[k], out var he) && he.Kind == HistoryKind.CropsFlattened && he.Cause1 == st.Event && st.Event != 0) { told = true; break; }
                    if (told) { ref var sv = ref Settlements.Get(kv.Key); if (sv.Inhabited) sv.CropBurnt = DMath.Min(1.0, sv.CropBurnt + share); }
                    else SettlementSys.CropLoss(kv.Key, share, Cause(), HistoryKind.CropsFlattened);
                }
                // people caught out in the worst of it
                if (st.Wind > 40f)
                    for (int id = People.First(); id != 0; id = People.Next(id))
                    {
                        ref var p = ref People.Get(id);
                        if (!p.Alive) continue;
                        Vec3d at = Agents.PositionAt(p, Now);
                        double wv = TC2.Core.Planet.Storms.WindAt(st, Vec3d.ArcDistance(at, st.At, P.Radius));
                        if (wv <= 40.0) continue;
                        if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 64) < 0.0003 * (wv - 40.0)) { Agents.Kill(ref p, Cause(), 7); Metrics.StormDeaths++; }
                    }
                P.Storms[i] = st;
            }
            for (int i = P.Storms.Count - 1; i >= 0; i--)
                if (TC2.Core.Planet.Storms.Spent(P.Storms[i], Now)) { Metrics.StormPeaks[(int)DMath.Clamp(P.Storms[i].Peak / 10f, 0, 7)]++; P.Storms.RemoveAt(i); }
            if (P.Storms.Count > 0) Queue.Push(Now + TC2.Core.Planet.Storms.StepSeconds, Phase.Process, EventKind.StormStep, 0);
        }

        List<int> _stormCells;

        /// <summary>The cells whose centres lie within a storm's reach (dot with its centre at least cosR), by a BFS.</summary>
        List<int> StormCells(int c0, Vec3d at, double cosR)
        {
            var P = Planet;
            var g = P.Grid;
            if (_qsrc == null || _qsrc.Length != P.Cells) { _qsrc = new int[P.Cells]; _qstamp = new int[P.Cells]; _qstampNow = 0; }
            var list = _stormCells ?? (_stormCells = new List<int>());
            list.Clear();
            _qstampNow++;
            _qstamp[c0] = _qstampNow;
            if (Vec3d.Dot(g.Center[c0], at) >= cosR) list.Add(c0); else return list;
            for (int head = 0; head < list.Count; head++)
            {
                int c = list[head];
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (_qstamp[nb] == _qstampNow) continue;
                    _qstamp[nb] = _qstampNow;
                    if (Vec3d.Dot(g.Center[nb], at) >= cosR) list.Add(nb);
                }
            }
            return list;
        }

        /// <summary>A strong storm comes ashore: the sea is driven over low land near its centre (0.6 m per m/s above 30).</summary>
        void Surge(in TC2.Core.Planet.Storm st, long cause)
        {
            var P = Planet;
            var g = P.Grid;
            double h0 = 0.6 * (st.Wind - 30.0);
            double cosR = DMath.Cos(0.6 * st.Radius / P.Radius);
            var flooded = new List<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || P.LakeDepth[c] > 0.3f || Vec3d.Dot(g.Center[c], st.At) < cosR) continue;
                bool coast = false;
                for (int k = 0; k < 8 && !coast; k++) if (!P.IsLand(g.Nbr8[c * 8 + k])) coast = true;
                if (!coast || P.Elev[c] - P.SeaLevel >= h0) continue;
                flooded.Add(c);
            }
            if (flooded.Count == 0) return;
            Metrics.Surges++;
            long ev = Record(new HistoryEvent { Kind = HistoryKind.StormSurge, Time = Now, Cell = flooded[0], Settlement = NearestSettlement(flooded[0], 6000), Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)h0, ArgA = flooded.Count });
            var hit = new SortedDictionary<int, int>();
            var mark = new HashSet<int>(flooded);
            foreach (int c in flooded)
            {
                P.Forest[c] *= 0.7f; P.Grass[c] *= 0.5f;
                Fx.Add(Now, c, FxKind.Wave, (float)h0);
                int o = P.FieldOwner[c]; if (o != 0) hit[o] = hit.TryGetValue(o, out int n) ? n + 1 : 1;
            }
            foreach (var kv in hit)
            {
                if (!Settlements.Exists(kv.Key)) continue;
                SettlementSys.CropLoss(kv.Key, DMath.Min(1.0, kv.Value / (double)DMath.Max(1, Settlements.Get(kv.Key).FieldCells)), ev, HistoryKind.FieldsFlooded);
            }
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive || !mark.Contains(g.CellOf(Agents.PositionAt(p, Now)))) continue;
                if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 65) < 0.25) { Agents.Kill(ref p, ev, 8); Metrics.StormDeaths++; }
            }
        }

        public void RequestResolution(Resolution r, long at) => Issue(new Command { Time = at, Kind = CommandKind.SetResolution, Arg = (long)r });

        void Strike(int cell, long cause, RootKind root, double strength)
        {
            Metrics.Lightning++;
            long strike = Record(new HistoryEvent { Kind = HistoryKind.LightningStrike, Time = Now, Cell = cell, Settlement = SettlementAtCell(cell), Cause1 = cause, Rel1 = CauseRel.CausedBy, RootKind = root });
            Fire.TryIgnite(this, cell, strike, root, strength);
        }

        // ------------------------------------------------------------- time
        /// <summary>
        /// Processes every event with time &lt;= target in (time, phase, seq) order. Returns true when the
        /// world reached target; false if the budget ran out first (call again; nothing is skipped).
        /// </summary>
        public bool AdvanceTo(long target, ref Budget budget)
        {
            while (Queue.Count > 0 && Queue.PeekTime() <= target)
            {
                if (budget.Done >= budget.MaxEvents) return false;
                if ((budget.Done & 255) == 255 && budget.ShouldYield != null && budget.ShouldYield()) { budget.Done++; return false; }
                var e = Queue.Pop();
                if (e.Time < Now) throw new InvalidOperationException("event in the past: " + e + " now " + Now);
                Now = e.Time;
                if (Profiler != null) { int k = (int)e.Kind * 16 + (e.Kind == EventKind.SystemTick ? e.Target : 0); Profiler.Begin(k); Dispatch(in e); Profiler.End(k); }
                else Dispatch(in e);
                budget.Done++;
                Metrics.Events++;
            }
            if (target > Now) Now = target;
            return true;
        }

        public void AdvanceTo(long target) { var b = Budget.Unlimited; AdvanceTo(target, ref b); }

        void Dispatch(in ScheduledEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.SystemTick:
                    PlanetSys.Tick(this, e.Target);
                    Metrics.SystemTicks++;
                    Queue.Push(Now + PeriodOf(e.Target), Phase.SystemBase + e.Target, EventKind.SystemTick, e.Target);
                    break;
                case EventKind.ProcessStep:
                    if (e.Target == 1) { Fire.Step(this); Metrics.FireSteps++; }
                    break;
                case EventKind.AgentStep:
                    Agents.OnStep(e.Target, e.Version);
                    break;
                case EventKind.AgentInterrupt:
                    Agents.OnInterrupt(e.Target, e.Version, e.A, e.B);
                    break;
                case EventKind.SettlementDaily:
                    SettlementSys.Daily(e.Target);
                    break;
                case EventKind.Birth:
                    SettlementSys.OnBirth(e.Target, e.A);
                    break;
                case EventKind.Lightning:
                    Strike((int)e.A, e.B, RootKind.Nature, 1.0);
                    break;
                case EventKind.Quake:
                    Quake((int)e.A);
                    break;
                case EventKind.StormStep:
                    StormStep();
                    break;
                case EventKind.PartyDepart:
                    Migration.Depart(e.Target);
                    break;
                case EventKind.PartyArrive:
                    Migration.Arrive(e.Target);
                    break;
                case EventKind.Eruption:
                    {
                        int v = Volcanism.IndexOf(Planet, (int)e.A);
                        if (v >= 0 && Planet.Vents[v].Next == e.Time) Erupt((int)e.A); // a vent woken early has no second eruption
                        break;
                    }
                case EventKind.Command:
                    ApplyCommand((int)e.A);
                    break;
                case EventKind.Housekeeping:
                    Housekeeping();
                    Queue.Push(Now + SimTime.Year, Phase.Housekeeping, EventKind.Housekeeping, 0);
                    break;
            }
        }

        public long PeriodOf(int system)
        {
            bool coarse = Resolution == Resolution.Coarse;
            switch (system)
            {
                case SystemId.Climate: return coarse ? SimTime.Day : Config.ClimatePeriod;
                case SystemId.Weather: return Config.WeatherPeriod;
                case SystemId.Vegetation: return coarse ? 3 * Config.VegetationPeriod : Config.VegetationPeriod;
                case SystemId.ClimateSlow: return SimTime.Year / 4;
                case SystemId.Tectonics: return DMath.ToLong(Tectonics.StepYears * SimTime.Year);
                default: return SimTime.Day;
            }
        }

        readonly HashSet<int> _ancestors = new HashSet<int>();
        HashSet<int> _gen = new HashSet<int>(), _next = new HashSet<int>();   // (set membership only: order never matters)
        readonly List<int> _prune = new List<int>();

        void Housekeeping()
        {
            History.Compact(Now, SubjectKept);
            // the long dead who matter to no one living leave the live table (their history stays): those who are not a parent,
            // grandparent or great-grandparent of anyone alive - the living's ancestors marked once, not searched per dead
            _ancestors.Clear(); _gen.Clear();
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                if (p.Mother != 0) _gen.Add(p.Mother);
                if (p.Father != 0) _gen.Add(p.Father);
            }
            for (int g = 0; g < 3 && _gen.Count > 0; g++)   // parents, grandparents, great-grandparents
            {
                _ancestors.UnionWith(_gen);
                _next.Clear();
                foreach (int a in _gen)
                {
                    if (!People.Exists(a)) continue;
                    ref var q = ref People.Get(a);
                    if (q.Mother != 0) _next.Add(q.Mother);
                    if (q.Father != 0) _next.Add(q.Father);
                }
                var t = _gen; _gen = _next; _next = t;
            }
            _prune.Clear();
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (p.Alive || Now - p.Died < 60 * SimTime.Year) continue;
                if (_ancestors.Contains(id)) continue;
                _prune.Add(id);
            }
            for (int i = 0; i < _prune.Count; i++) People.Remove(_prune[i]);
        }



        bool SubjectKept(EntityRef r)
        {
            if (r.Kind != EntityKind.Person) return true;
            if (!People.Exists(r.Id)) return false;
            ref var p = ref People.Get(r.Id);
            return p.Alive || Now - p.Died < 30 * SimTime.Year;
        }

        /// <summary>Schedules the recurring systems for a new world (worldgen).</summary>
        public void ScheduleSystems()
        {
            for (int s = 1; s < SystemId.Count; s++) Queue.Push(Now, Phase.SystemBase + s, EventKind.SystemTick, s);
            Queue.Push(Now + SimTime.Year, Phase.Housekeeping, EventKind.Housekeeping, 0);
        }

        // ------------------------------------------------------------- derived state
        /// <summary>Rebuilds every derived index from authoritative state (after load).</summary>
        public void RebuildDerived()
        {
            PlanetSys = new PlanetSystems(Planet);
            Tect = new Tectonics(Planet);
            Drainage.Rebuild(Planet, fromSaved: true);
            Drainage.SettleStored(Planet);
            _landM2 = -1;
            _residents = new IntList[16]; _visitors = new IntList[16]; _hhOf = new IntList[16]; _members = new IntList[64]; _fieldCells = new IntList[16];
            for (int c = 0; c < Planet.Cells; c++) if (Planet.FieldOwner[c] != 0) FieldCellsOf(Planet.FieldOwner[c]).Add(c);
            for (int i = 0; i < _cellSettlement.Length; i++) _cellSettlement[i] = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) IndexSettlementCells(sid);
            for (int hid = Households.First(); hid != 0; hid = Households.Next(hid))
            {
                ref var h = ref Households.Get(hid);
                if (h.Dissolved == 0) HouseholdsOf(h.Settlement).Add(hid);
            }
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                Residents(p.Home).Add(id);
                Members(p.Household).Add(id);
                if (p.At != 0 && p.At != p.Home) Visitors(p.At).Add(id);
            }
            Routes.Invalidate();
            Agents.RebuildTravelIndex();
        }

        // ------------------------------------------------------------- persistence & hashing
        public void WriteState(BinWriter w)
        {
            Config.Write(w);
            w.Long(Now); w.Long(Created); w.Byte((byte)Resolution);
            Ids.Write(w);
            Queue.Write(w);
            Planet.Write(w);
            Fire.Write(w);
            w.Int(Settlements.Count); for (int id = Settlements.First(); id != 0; id = Settlements.Next(id)) Settlements.Get(id).Write(w);
            w.Int(Households.Count); for (int id = Households.First(); id != 0; id = Households.Next(id)) Households.Get(id).Write(w);
            w.Int(People.Count); for (int id = People.First(); id != 0; id = People.Next(id)) People.Get(id).Write(w);
            w.Int(Parties.Count); for (int id = Parties.First(); id != 0; id = Parties.Next(id)) Parties.Get(id).Write(w);
            w.Int(Herds.Count); for (int id = Herds.First(); id != 0; id = Herds.Next(id)) Herds.Get(id).Write(w);
            History.Write(w);
            w.Int(CommandLog.Count); for (int i = 0; i < CommandLog.Count; i++) CommandLog[i].Write(w);
            Metrics.Write(w);
        }

        public static World ReadState(BinReader r)
        {
            var cfg = WorldConfig.Read(r);
            var w = new World(cfg);
            w.Now = r.Long(); w.Created = r.Long(); w.Resolution = (Resolution)r.Byte();
            w.Ids.Read(r);
            w.Queue.Read(r);
            var planet = PlanetState.Read(r);
            w.InitPlanet(planet);
            w.Fire.Read(r);
            int ns = r.Int(); for (int i = 0; i < ns; i++) { var s = Settlement.Read(r); w.Settlements.Add(s.Id, s); }
            int nh = r.Int(); for (int i = 0; i < nh; i++) { var h = Household.Read(r); w.Households.Add(h.Id, h); }
            int np = r.Int(); for (int i = 0; i < np; i++) { var p = Person.Read(r); w.People.Add(p.Id, p); }
            int npa = r.Int(); for (int i = 0; i < npa; i++) { var pa = Party.Read(r); w.Parties.Add(pa.Id, pa); }
            int nhe = r.Int(); for (int i = 0; i < nhe; i++) { var he = Herd.Read(r); w.Herds.Add(he.Id, he); }
            w.Settlements.SetMaxId(w.Ids.Peek(EntityKind.Settlement) - 1);
            w.Households.SetMaxId(w.Ids.Peek(EntityKind.Household) - 1);
            w.People.SetMaxId(w.Ids.Peek(EntityKind.Person) - 1);
            w.Parties.SetMaxId(w.Ids.Peek(EntityKind.Process) - 1);
            w.Herds.SetMaxId(w.Ids.Peek(EntityKind.Process) - 1);
            w.History.Read(r);
            int nc = r.Int(); for (int i = 0; i < nc; i++) w.CommandLog.Add(Command.Read(r));
            w.Metrics.Read(r);
            w.RebuildDerived();
            return w;
        }

        /// <summary>Hash of all authoritative state (not metrics, not derived indexes).</summary>
        public ulong StateHash()
        {
            var h = StateHasher.Create();
            Config.Hash(ref h);
            h.Add(Now); h.Add(Created); h.Add((byte)Resolution);
            Ids.Hash(ref h);
            Queue.Hash(ref h);
            Planet.Hash(ref h);
            Fire.Hash(ref h);
            for (int id = Settlements.First(); id != 0; id = Settlements.Next(id)) Settlements.Get(id).Hash(ref h);
            for (int id = Households.First(); id != 0; id = Households.Next(id)) Households.Get(id).Hash(ref h);
            for (int id = People.First(); id != 0; id = People.Next(id)) People.Get(id).Hash(ref h);
            for (int id = Parties.First(); id != 0; id = Parties.Next(id)) Parties.Get(id).Hash(ref h);
            for (int id = Herds.First(); id != 0; id = Herds.Next(id)) Herds.Get(id).Hash(ref h);
            History.Hash(ref h);
            h.Add(CommandLog.Count); for (int i = 0; i < CommandLog.Count; i++) CommandLog[i].Hash(ref h);
            return h.Value;
        }

        // ------------------------------------------------------------- summaries
        public int LivingPeople()
        {
            int n = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) n += Settlements.Get(sid).Population;
            return n;
        }

        public int InhabitedSettlements()
        {
            int n = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) if (Settlements.Get(sid).Inhabited) n++;
            return n;
        }
    }
}
