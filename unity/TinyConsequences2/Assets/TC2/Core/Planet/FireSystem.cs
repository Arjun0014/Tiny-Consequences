using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Core.Planet
{
    /// <summary>A fire as a process (04 §4.1): one per ignition, spreading cell to cell.</summary>
    public struct FireProc
    {
        public int Id;
        public long Started;
        public long Ended;
        public long Cause;          // the event that lit it (a lightning strike, a command)
        public long StartEvent;     // FireStarted
        public long WildfireEvent;  // Wildfire (aggregate) once it is large
        public int Origin;          // cell
        public int Burned;          // cells burnt so far
        public int Active;          // cells burning now
        public int MaxActive;

        public void Write(BinWriter w) { w.Int(Id); w.Long(Started); w.Long(Ended); w.Long(Cause); w.Long(StartEvent); w.Long(WildfireEvent); w.Int(Origin); w.Int(Burned); w.Int(Active); w.Int(MaxActive); }
        public static FireProc Read(BinReader r) => new FireProc { Id = r.Int(), Started = r.Long(), Ended = r.Long(), Cause = r.Long(), StartEvent = r.Long(), WildfireEvent = r.Long(), Origin = r.Int(), Burned = r.Int(), Active = r.Int(), MaxActive = r.Int() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Started); h.Add(Ended); h.Add(Cause); h.Add(StartEvent); h.Add(WildfireEvent); h.Add(Origin); h.Add(Burned); h.Add(Active); h.Add(MaxActive); }
    }

    /// <summary>
    /// Fire on the surface grid (05 §3.6): ignition, spread by fuel, dryness and wind, burn-out, scars,
    /// and the notification of everyone who can perceive it (travellers via the travel index, settlements by
    /// distance). It owns fire on the planet: nothing else writes burning state.
    /// </summary>
    public sealed class FireSystem
    {
        public float[] Intensity;
        public int[] ProcOf;
        public long[] BurnedAt;
        public readonly IntList Active = new IntList(64);   // burning cells, ascending
        public readonly PagedStore<FireProc> Procs = new PagedStore<FireProc>();
        public bool StepPending;
        public long StepIndex;

        // scratch
        readonly List<int> _ignite = new List<int>();
        readonly List<int> _out = new List<int>();
        readonly List<int> _lit = new List<int>(), _merged = new List<int>();   // a step's new burning cells, merged in once
        int[] _igniteStamp; int _stampNow;   // cells already among this step's ignitions (no search through the list)

        public FireSystem(int cells)
        {
            Intensity = new float[cells];
            ProcOf = new int[cells];
            BurnedAt = new long[cells];
        }

        public bool IsBurning(int c) => Intensity[c] > 0;

        /// <summary>Tries to light a cell; returns the new process id (0 if it did not catch).</summary>
        public int TryIgnite(World w, int cell, long cause, RootKind rootIfNoCause, double strength)
        {
            var P = w.Planet;
            if (!P.IsLand(cell) || IsBurning(cell)) return 0;
            double fuel = P.Fuel(cell), dry = P.Dryness(cell);
            double catchP = DMath.Clamp01(strength * fuel * (0.15 + dry) * 1.6);
            double u = KeyedRng.Unit(w.Seed, RngStream.FireSpread, (ulong)cell, (ulong)w.Now, 7);
            if (u >= catchP) return 0;
            int id = w.Ids.Next(EntityKind.Process);
            var proc = new FireProc { Id = id, Started = w.Now, Cause = cause, Origin = cell };
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.FireStarted, Time = w.Now, Cell = cell, Settlement = w.SettlementAtCell(cell),
                Subject = EntityRef.Process(id), Cause1 = cause, Rel1 = CauseRel.CausedBy, RootKind = rootIfNoCause,
                Magnitude = (float)(fuel * dry),
            });
            proc.StartEvent = ev;
            Procs.Add(id, proc);
            w.History.Register(EntityRef.Process(id), "the fire near " + w.PlaceName(cell), w.Now);
            Light(w, cell, id, 0.35 + 0.4 * dry);
            if (!StepPending) { StepPending = true; w.Queue.Push(w.Now + w.Config.FireStep, Phase.Process, EventKind.ProcessStep, 1); }
            return id;
        }

        void Light(World w, int cell, int proc, double intensity, bool batch = false)
        {
            Intensity[cell] = (float)DMath.Clamp(intensity, 0.05, 1.0);
            ProcOf[cell] = proc;
            if (batch) _lit.Add(cell); else Active.InsertSorted(cell);
            ref var p = ref Procs.Get(proc);
            p.Active++; p.Burned++;
            if (p.Active > p.MaxActive) p.MaxActive = p.Active;
            w.Hazards.FireAt(w, cell, proc, p.StartEvent);
        }

        /// <summary>One fire step for every burning cell (ascending cell order; spread reads the step's start state).</summary>
        public void Step(World w)
        {
            StepPending = false;
            StepIndex++;
            var P = w.Planet;
            var grid = P.Grid;
            _ignite.Clear(); _out.Clear(); _lit.Clear();
            if (_igniteStamp == null || _igniteStamp.Length != P.Cells) { _igniteStamp = new int[P.Cells]; _stampNow = 0; }
            _stampNow++;
            double hours = w.Config.FireStep / (double)SimTime.Hour;
            Vec3d windDir = Vec3d.Zero;
            long sod = SimTime.SecondOfDay(w.Now);
            for (int ai = 0; ai < Active.Count; ai++)
            {
                int c = Active[ai];
                double fuel = P.Fuel(c), dry = P.Dryness(c);
                double inten = Intensity[c];
                double rain = P.RainToday[c];
                if (rain > 0.4) inten *= 1.0 - DMath.Clamp01(rain * 0.5 * hours);
                double target = DMath.Clamp01(fuel * (0.35 + 0.9 * dry));
                inten += (target - inten) * 0.35;
                // consume fuel (grass fast, forest slower)
                double g = P.Grass[c], f = P.Forest[c];
                g -= g * 0.65 * inten * hours; f -= f * 0.22 * inten * hours;
                P.Grass[c] = (float)DMath.Max(0, g); P.Forest[c] = (float)DMath.Max(0, f);
                if (inten < 0.08 || P.Fuel(c) < 0.06) { _out.Add(c); continue; }
                Intensity[c] = (float)inten;
                // cooler, damper nights slow the spread (local solar time of the cell)
                double lh = (sod / 3600.0 + P.Lon[c] / DMath.TwoPi * 24.0);
                lh -= 24.0 * DMath.Floor(lh / 24.0);
                double night = (lh < 6 || lh > 20) ? 0.5 : 1.0;
                // spread
                Wind(grid.Center[c], out windDir);
                for (int k = 0; k < 8; k++)
                {
                    int nb = grid.Nbr8[c * 8 + k];
                    if (nb == c || !P.IsLand(nb) || IsBurning(nb)) continue;
                    if (BurnedAt[nb] != 0 && w.Now - BurnedAt[nb] < SimTime.Year && P.Fuel(nb) < 0.15) continue;
                    double nf = P.Fuel(nb), nd = P.Dryness(nb);
                    Vec3d dir = (grid.Center[nb] - grid.Center[c]).Normalized;
                    double align = Vec3d.Dot(dir, windDir);
                    double p = 0.42 * hours * inten * nf * (nd * nd * 1.6 - 0.08) * (1.0 + 0.8 * align) * (k < 4 ? 1.0 : 0.7) * night;
                    if (P.RainToday[nb] > 0.4) p *= 0.2;
                    double u = KeyedRng.Unit(w.Seed, RngStream.FireSpread, (ulong)nb, (ulong)StepIndex, (ulong)c);
                    if (u < p && _igniteStamp[nb] != _stampNow) { _igniteStamp[nb] = _stampNow; _ignite.Add(nb); _igniteFrom.Add(c); }
                }
                w.Settlements_FireOnCell(c, ProcOf[c]);
            }
            // burn-outs (the burning list compacted once, not searched per cell: big fires made this quadratic)
            for (int i = 0; i < _out.Count; i++) Extinguish(w, _out[i], false);
            if (_out.Count > 0)
            {
                int k = 0;
                for (int i = 0; i < Active.Count; i++) { int c = Active[i]; if (Intensity[c] > 0) Active[k++] = c; }
                Active.Truncate(k);
            }
            // new ignitions (in the order found: ascending source cells, then neighbour order; deterministic)
            for (int i = 0; i < _ignite.Count; i++)
            {
                int nb = _ignite[i];
                if (IsBurning(nb)) continue;
                int proc = ProcOf[_igniteFrom[i]];
                if (proc == 0) continue;
                Light(w, nb, proc, 0.3, true);
            }
            // the new burning cells join the list in one sorted merge (the same ascending list as inserting each)
            if (_lit.Count > 0)
            {
                _lit.Sort();
                _merged.Clear();
                int a = 0, b = 0;
                while (a < Active.Count || b < _lit.Count)
                {
                    if (b >= _lit.Count || (a < Active.Count && Active[a] < _lit[b])) _merged.Add(Active[a++]);
                    else _merged.Add(_lit[b++]);
                }
                Active.Clear();
                for (int i = 0; i < _merged.Count; i++) Active.Add(_merged[i]);
                _lit.Clear();
            }
            _igniteFrom.Clear();
            // process bookkeeping: large fires become history; finished fires end
            for (int id = Procs.First(); id != 0; id = Procs.Next(id))
            {
                ref var pr = ref Procs.Get(id);
                if (pr.Ended != 0) continue;
                if (pr.WildfireEvent == 0 && pr.Burned >= 8)
                {
                    pr.WildfireEvent = w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.Wildfire, Time = w.Now, Cell = pr.Origin, Settlement = w.SettlementAtCell(pr.Origin),
                        Subject = EntityRef.Process(id), Cause1 = pr.StartEvent, Rel1 = CauseRel.PartOf, Magnitude = pr.Burned,
                        Flags = EventFlags.Aggregate,
                    });
                }
                if (pr.Active == 0)
                {
                    pr.Ended = w.Now;
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.FireOut, Time = w.Now, Cell = pr.Origin, Subject = EntityRef.Process(id),
                        Cause1 = pr.WildfireEvent != 0 ? pr.WildfireEvent : pr.StartEvent, Rel1 = CauseRel.PartOf,
                        Magnitude = pr.Burned, ArgA = (int)((w.Now - pr.Started) / SimTime.Hour),
                    });
                    w.History.EndEntity(EntityRef.Process(id), w.Now);
                    _ended.Add(id);
                }
            }
            // finished processes live on in history only
            for (int i = 0; i < _ended.Count; i++) Procs.Remove(_ended[i]);
            _ended.Clear();
            if (Active.Count > 0 && !StepPending)
            {
                StepPending = true;
                w.Queue.Push(w.Now + w.Config.FireStep, Phase.Process, EventKind.ProcessStep, 1);
            }
        }

        readonly List<int> _igniteFrom = new List<int>();
        readonly List<int> _ended = new List<int>();

        void Extinguish(World w, int c, bool unlist = true)
        {
            int proc = ProcOf[c];
            Intensity[c] = 0;
            ProcOf[c] = 0;
            BurnedAt[c] = w.Now;
            if (unlist) Active.Remove(c);
            if (proc != 0 && Procs.Exists(proc)) Procs.Get(proc).Active--;
        }

        /// <summary>Prevailing wind (unit tangent vector): easterly trades, westerlies, polar easterlies.</summary>
        public static void Wind(Vec3d p, out Vec3d dir)
        {
            double lat = p.Latitude;
            double alat = DMath.Abs(lat) * DMath.Rad2Deg;
            Vec3d east = Vec3d.Cross(Vec3d.UnitY, p);
            if (east.LengthSq < 1e-12) east = Vec3d.UnitX;
            east = east.Normalized;
            double s = alat < 30 ? -1.0 : (alat < 60 ? 1.0 : -1.0);
            dir = east * s;
        }

        public void Write(BinWriter w)
        {
            int n = Intensity.Length;
            w.Int(n);
            for (int i = 0; i < n; i++) { w.Float(Intensity[i]); w.Int(ProcOf[i]); w.Long(BurnedAt[i]); }
            Active.Write(w);
            w.Int(Procs.Count);
            for (int id = Procs.First(); id != 0; id = Procs.Next(id)) Procs.Get(id).Write(w);
            w.Int(Procs.MaxId);
            w.Bool(StepPending); w.Long(StepIndex);
        }

        public void Read(BinReader r)
        {
            int n = r.Int();
            for (int i = 0; i < n; i++) { Intensity[i] = r.Float(); ProcOf[i] = r.Int(); BurnedAt[i] = r.Long(); }
            var act = IntList.Read(r);
            Active.Clear(); for (int i = 0; i < act.Count; i++) Active.Add(act[i]);
            Procs.Clear();
            int np = r.Int();
            for (int i = 0; i < np; i++) { var p = FireProc.Read(r); Procs.Add(p.Id, p); }
            Procs.SetMaxId(r.Int());
            StepPending = r.Bool(); StepIndex = r.Long();
        }

        public void Hash(ref StateHasher h)
        {
            for (int i = 0; i < Intensity.Length; i++) { h.Add(Intensity[i]); h.Add(ProcOf[i]); h.Add(BurnedAt[i]); }
            Active.Hash(ref h);
            for (int id = Procs.First(); id != 0; id = Procs.Next(id)) Procs.Get(id).Hash(ref h);
            h.Add(StepPending); h.Add(StepIndex);
        }
    }
}
