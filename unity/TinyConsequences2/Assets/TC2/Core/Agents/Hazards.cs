using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Core.Agents
{
    public static class InterruptReason
    {
        /// <summary>Seen from a travel leg (the interrupt is valid only for that leg's activity version).</summary>
        public const int FireSeenOnRoad = 1;
        /// <summary>Seen while at a settlement (valid while the person is still there; A carries the settlement in bits 8+).</summary>
        public const int FireSeenAtPlace = 2;
    }

    /// <summary>
    /// Perception of dangers (07 §6): when a fire appears in a cell, everyone who will be within sight of it
    /// is found - travellers through the space-time index, people at settlements by distance - and
    /// interrupted at the moment they would notice it, after their own reaction delay. People never know of
    /// dangers they have not perceived.
    /// </summary>
    public sealed class Hazards
    {
        readonly List<(int person, long tEnter, long tExit)> _hits = new List<(int, long, long)>();
        readonly List<int> _cells = new List<int>();
        readonly List<(int person, long at, int place)> _alarm = new List<(int, long, int)>();
        Func<int, int> _versionOf;
        World _w;

        public void Bind(World w)
        {
            _w = w;
            _versionOf = pid => _w.People.Exists(pid) && _w.People.Get(pid).Alive ? _w.People.Get(pid).ActVer : -1;
        }

        /// <summary>Cells whose centres lie within <paramref name="range"/> metres of the cell (BFS over neighbours).</summary>
        int[] _stamp; int _stampNow;

        public void CellsWithin(World w, int cell, double range, List<int> into)
        {
            into.Clear();
            var P = w.Planet; var g = P.Grid;
            if (_stamp == null || _stamp.Length != P.Cells) { _stamp = new int[P.Cells]; _stampNow = 0; }
            _stampNow++;
            Vec3d c0 = g.Center[cell];
            double lim = range + 0.75 * P.CellSize;
            into.Add(cell); _stamp[cell] = _stampNow;
            for (int head = 0; head < into.Count; head++)
            {
                int c = into[head];
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (_stamp[nb] == _stampNow) continue;
                    _stamp[nb] = _stampNow;
                    if (Vec3d.ArcDistance(c0, g.Center[nb], P.Radius) <= lim) into.Add(nb);
                }
                if (into.Count > 400) break;
            }
        }

        public void FireAt(World w, int cell, int proc, long fireEvent)
        {
            double range = w.Config.FirePerceptionRange;
            CellsWithin(w, cell, range, _cells);
            _hits.Clear();
            for (int i = 0; i < _cells.Count; i++) w.Travel.Query(_cells[i], w.Now, _versionOf, _hits);
            _alarm.Clear();
            for (int i = 0; i < _hits.Count; i++) _alarm.Add((_hits[i].person, Math.Max(w.Now, _hits[i].tEnter), 0));
            // people at settlements within sight: outdoors and awake, or everyone if it is at their door
            var P = w.Planet;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[cell], P.Radius);
                if (d > range + P.CellSize) continue;
                bool atDoor = d < P.CellSize * 1.1;
                AddPresent(w, sid, w.Residents(sid), atDoor);
                AddPresent(w, sid, w.Visitors(sid), atDoor);
            }
            Dispatch(w, proc, fireEvent);
        }

        void AddPresent(World w, int sid, IntList list, bool atDoor)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int pid = list[i];
                ref var p = ref w.People.Get(pid);
                if (!p.Alive || p.At != sid) continue;
                bool outdoors = p.Act == ActivityKind.Fields || p.Act == ActivityKind.Wood || p.Act == ActivityKind.Social ||
                                p.Act == ActivityKind.Play || p.Act == ActivityKind.Water || p.Act == ActivityKind.Visit || p.Act == ActivityKind.Idle;
                if (outdoors || atDoor) _alarm.Add((pid, w.Now, sid));
            }
        }

        /// <summary>When a new travel leg begins, it may pass near fires already burning (the traveller will see them).</summary>
        public void CheckNewLeg(World w, int pid, List<(int cell, long tEnter)> legCells)
        {
            var fire = w.Fire;
            if (fire.Active.Count == 0) return;
            var P = w.Planet;
            double lim = w.Config.FirePerceptionRange + 0.5 * P.CellSize;
            long best = long.MaxValue; int bestProc = 0; long bestEvent = 0;
            for (int a = 0; a < fire.Active.Count; a++)
            {
                int fc = fire.Active[a];
                Vec3d fp = P.Grid.Center[fc];
                for (int i = 0; i < legCells.Count; i++)
                {
                    if (legCells[i].tEnter >= best) break;
                    if (Vec3d.ArcDistance(fp, P.Grid.Center[legCells[i].cell], P.Radius) <= lim)
                    {
                        best = legCells[i].tEnter; bestProc = fire.ProcOf[fc];
                        bestEvent = fire.Procs.Exists(bestProc) ? fire.Procs.Get(bestProc).StartEvent : 0;
                        break;
                    }
                }
            }
            if (best == long.MaxValue) return;
            _alarm.Clear();
            _alarm.Add((pid, Math.Max(w.Now, best), 0));
            Dispatch(w, bestProc, bestEvent);
        }

        void Dispatch(World w, int proc, long fireEvent)
        {
            if (_alarm.Count == 0) return;
            // deterministic order: by person, earliest perception first
            _alarm.Sort((a, b) => a.person != b.person ? a.person.CompareTo(b.person) : (a.at != b.at ? a.at.CompareTo(b.at) : a.place.CompareTo(b.place)));
            int last = 0;
            for (int i = 0; i < _alarm.Count; i++)
            {
                var (pid, at, place) = _alarm[i];
                if (pid == last) continue;
                last = pid;
                ref var p = ref w.People.Get(pid);
                if (!p.Alive) continue;
                if (p.AlarmProc == proc && (p.AlarmVer == p.ActVer || place != 0)) continue; // already warned
                long react = 45 + (long)(240 * (1.0 - p.Curious / 255.0));
                long when = at + react;
                if (place == 0)
                {
                    if (p.Step != StepKind.Travel || when >= p.T1) continue; // arrives before noticing
                    w.Queue.Push(when, Phase.Agent, EventKind.AgentInterrupt, pid, p.ActVer, InterruptReason.FireSeenOnRoad, fireEvent);
                }
                else
                {
                    w.Queue.Push(when, Phase.Agent, EventKind.AgentInterrupt, pid, -1, InterruptReason.FireSeenAtPlace | ((long)place << 8), fireEvent);
                }
                p.AlarmProc = proc;
                p.AlarmVer = p.ActVer;
                w.Metrics.Interrupts++;
            }
        }
    }
}
