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

        public void Write(BinWriter w)
        {
            w.Long(Events); w.Long(Decisions); w.Long(Steps); w.Long(Stale); w.Long(Activities); w.Long(Journeys); w.Long(JourneysInterrupted);
            w.Long(Interrupts); w.Long(InterruptsHandled); w.Long(Meals); w.Long(Births); w.Long(Deaths); w.Long(Marriages);
            w.Long(SystemTicks); w.Long(FireSteps); w.Long(Lightning); w.Long(Commands);
        }

        public void Read(BinReader r)
        {
            Events = r.Long(); Decisions = r.Long(); Steps = r.Long(); Stale = r.Long(); Activities = r.Long(); Journeys = r.Long(); JourneysInterrupted = r.Long();
            Interrupts = r.Long(); InterruptsHandled = r.Long(); Meals = r.Long(); Births = r.Long(); Deaths = r.Long(); Marriages = r.Long();
            SystemTicks = r.Long(); FireSteps = r.Long(); Lightning = r.Long(); Commands = r.Long();
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

    public enum CommandKind : byte { None = 0, GodLightning = 1, Note = 2, SetResolution = 3 }

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
        public const int FormatSimVersion = 1;

        public WorldConfig Config;
        public ulong Seed => Config.Seed;
        public long Now;
        public long Created;
        public Resolution Resolution;   // authoritative; changed only by a logged command
        public readonly IdAllocator Ids = new IdAllocator();
        public readonly EventQueue Queue = new EventQueue();
        public readonly HistoryLedger History = new HistoryLedger();
        public PlanetState Planet;
        public PlanetSystems PlanetSys;     // derived helpers (weather blocks, mean temperatures)
        public FireSystem Fire;
        public readonly PagedStore<Settlement> Settlements = new PagedStore<Settlement>();
        public readonly PagedStore<Household> Households = new PagedStore<Household>();
        public readonly PagedStore<Person> People = new PagedStore<Person>();
        public readonly List<Command> CommandLog = new List<Command>();
        public readonly Metrics Metrics = new Metrics();
        public ISimProfiler Profiler;

        // systems (stateless logic bound to this world)
        public readonly AgentSystem Agents = new AgentSystem();
        public readonly SettlementSystem SettlementSys = new SettlementSystem();
        public readonly Hazards Hazards = new Hazards();

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
        }

        /// <summary>Allocates the planet-sized structures (worldgen and load call this).</summary>
        public void InitPlanet(PlanetState planet)
        {
            Planet = planet;
            Fire = new FireSystem(planet.Cells);
            Travel = new TravelIndex(planet.Cells);
            _cellSettlement = new int[planet.Cells];
            PlanetSys = new PlanetSystems(planet);
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
                default: return SimTime.Day;
            }
        }

        void Housekeeping()
        {
            History.Compact(Now, SubjectKept);
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                // the long dead who matter to no one living leave the live table (their history stays)
                ref var p = ref People.Get(id);
                if (p.Alive || Now - p.Died < 60 * SimTime.Year) continue;
                if (KinOfLiving(id)) continue;
                People.Remove(id);
            }
        }

        bool SubjectKept(EntityRef r)
        {
            if (r.Kind != EntityKind.Person) return true;
            if (!People.Exists(r.Id)) return false;
            ref var p = ref People.Get(r.Id);
            return p.Alive || Now - p.Died < 30 * SimTime.Year;
        }

        bool KinOfLiving(int id)
        {
            // a parent, grandparent or great-grandparent of anyone alive
            for (int pid = People.First(); pid != 0; pid = People.Next(pid))
            {
                ref var p = ref People.Get(pid);
                if (!p.Alive) continue;
                if (IsAncestor(id, pid, 3)) return true;
            }
            return false;
        }

        bool IsAncestor(int anc, int pid, int depth)
        {
            if (depth == 0 || !People.Exists(pid)) return false;
            ref var p = ref People.Get(pid);
            if (p.Mother == anc || p.Father == anc) return true;
            return (p.Mother != 0 && IsAncestor(anc, p.Mother, depth - 1)) || (p.Father != 0 && IsAncestor(anc, p.Father, depth - 1));
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
            w.Settlements.SetMaxId(w.Ids.Peek(EntityKind.Settlement) - 1);
            w.Households.SetMaxId(w.Ids.Peek(EntityKind.Household) - 1);
            w.People.SetMaxId(w.Ids.Peek(EntityKind.Person) - 1);
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
