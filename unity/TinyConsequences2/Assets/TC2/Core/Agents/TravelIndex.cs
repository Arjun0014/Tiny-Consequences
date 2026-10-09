using System;
using System.Collections.Generic;

namespace TC2.Core.Agents
{
    /// <summary>
    /// The space-time index of journeys (04 §5): for every surface cell, the travellers whose current travel
    /// leg crosses it, with entry and exit times. Derived state: rebuilt from activities after a load.
    /// Stale entries (older activity versions, past exits) are dropped lazily when visited.
    /// Query results are always sorted, so the internal list order never leaks into the simulation.
    /// </summary>
    public sealed class TravelIndex
    {
        struct Entry { public int Person; public int Version; public long TEnter, TExit; public int Next; }

        int[] _head;
        Entry[] _pool = new Entry[1024];
        int _used;
        int _free = -1;
        public int LiveEntries;

        public TravelIndex(int cells) { _head = new int[cells]; for (int i = 0; i < cells; i++) _head[i] = -1; }

        public void Clear()
        {
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            _used = 0; _free = -1; LiveEntries = 0;
        }

        /// <summary>Whether the next entry would grow the pool.</summary>
        public bool Full => _free < 0 && _used == _pool.Length;

        /// <summary>
        /// Drops every stale entry (past its exit, or of an older activity) in every cell - the entries a query of that cell would drop - so the
        /// pool grows only for live journeys: cells nobody queries kept every leg walked through them since the load (D2-137). The live entries
        /// keep their order in each cell; if they still fill more than half the pool it is doubled now, so sweeps stay rare.
        /// </summary>
        public void Sweep(long now, Func<int, int> versionOf)
        {
            for (int cell = 0; cell < _head.Length; cell++)
            {
                int prev = -1, i = _head[cell];
                while (i >= 0)
                {
                    int next = _pool[i].Next;
                    ref Entry e = ref _pool[i];
                    if (e.TExit < now || versionOf(e.Person) != e.Version)
                    {
                        if (prev < 0) _head[cell] = next; else _pool[prev].Next = next;
                        e.Next = _free; _free = i; LiveEntries--;
                    }
                    else prev = i;
                    i = next;
                }
            }
            if (LiveEntries > _pool.Length / 2) Array.Resize(ref _pool, _pool.Length * 2);
        }

        public void Add(int cell, int person, int version, long tEnter, long tExit)
        {
            int i;
            if (_free >= 0) { i = _free; _free = _pool[i].Next; }
            else { if (_used == _pool.Length) Array.Resize(ref _pool, _pool.Length * 2); i = _used++; }
            _pool[i] = new Entry { Person = person, Version = version, TEnter = tEnter, TExit = tExit, Next = _head[cell] };
            _head[cell] = i;
            LiveEntries++;
        }

        /// <summary>
        /// Appends (person, tEnter) for live entries in <paramref name="cell"/> whose exit is at or after
        /// <paramref name="now"/>; <paramref name="versionOf"/> gives a person's current activity version
        /// (or -1 if gone). Prunes stale entries as it goes.
        /// </summary>
        public void Query(int cell, long now, Func<int, int> versionOf, List<(int person, long tEnter, long tExit)> into)
        {
            int prev = -1, i = _head[cell];
            while (i >= 0)
            {
                int next = _pool[i].Next;
                ref Entry e = ref _pool[i];
                bool stale = e.TExit < now || versionOf(e.Person) != e.Version;
                if (stale)
                {
                    if (prev < 0) _head[cell] = next; else _pool[prev].Next = next;
                    e.Next = _free; _free = i; LiveEntries--;
                }
                else
                {
                    into.Add((e.Person, e.TEnter, e.TExit));
                    prev = i;
                }
                i = next;
            }
        }
    }
}
