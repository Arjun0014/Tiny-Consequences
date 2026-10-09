using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.History
{
    /// <summary>A growable list of longs (event ids).</summary>
    public sealed class LongList
    {
        long[] _a = new long[4];
        int _n;
        public int Count => _n;
        public long this[int i] => _a[i];
        public void Add(long v) { if (_n == _a.Length) Array.Resize(ref _a, _a.Length * 2); _a[_n++] = v; }
        public void Clear() { _n = 0; }
    }

    /// <summary>What history knows about an entity (name, lifespan, notability).</summary>
    public struct EntityInfo
    {
        public string Name;
        public long Created;
        public long Ended;   // 0 = still exists
        public float Importance;
        public bool Notable;
    }

    /// <summary>
    /// The append-only causal ledger (D2-008, 11_HISTORY_AND_CAUSALITY.md). Systems record events with
    /// the causes they acted on; roots (NATURE / PEOPLE / YOU) are cached; importance flows up the
    /// ancestry; yearly compaction drops what no longer matters and rewires causes to kept ancestors.
    /// Observers read it; nothing here writes world state.
    /// </summary>
    public sealed class HistoryLedger
    {
        HistoryEvent[] _ev = new HistoryEvent[4096];
        int _n;
        readonly Dictionary<long, LongList> _byEntity = new Dictionary<long, LongList>();
        readonly Dictionary<long, EntityInfo> _entities = new Dictionary<long, EntityInfo>();
        // a village's political events by the village they happened in - its headmen, its splits (C10.2, C10.3): they name people and households,
        // not the village, so the entity index would not find them; for the pages only (the simulation never reads this index)
        readonly Dictionary<int, LongList> _byPlace = new Dictionary<int, LongList>();
        long _compactedThrough; // events with id <= this have been examined by compaction at least once
        public long Dropped;     // total events dropped by compaction (statistics)

        public int Count => _n;
        public HistoryEvent At(int index) => _ev[index];
        public ref HistoryEvent RefAt(int index) => ref _ev[index];
        public long LastId => _n == 0 ? 0 : _ev[_n - 1].Id;

        /// <summary>Records an event. Assigns id (from the world's allocator), tier, root and importance.</summary>
        public long Record(IdAllocator ids, HistoryEvent e)
        {
            e.Id = ids.NextEvent();
            if (e.Tier == Tier.Ephemeral) e.Tier = HistoryKinds.DefaultTier(e.Kind);
            if (e.Cause1 != 0)
            {
                int ci = IndexOf(e.Cause1);
                if (ci >= 0) { e.Root = _ev[ci].Root; e.RootKind = _ev[ci].RootKind; }
                else { e.Root = e.Cause1; if (e.RootKind == RootKind.Unknown) e.RootKind = RootKind.Nature; }
                if (e.Rel1 == CauseRel.None) e.Rel1 = CauseRel.CausedBy;
            }
            else
            {
                e.Root = e.Id;
                if (e.RootKind == RootKind.Unknown) e.RootKind = e.Tier == Tier.Personal ? RootKind.People : RootKind.Nature;
            }
            if (e.Cause2 != 0 && e.Rel2 == CauseRel.None) e.Rel2 = CauseRel.CausedBy;
            double baseImp = HistoryKinds.BaseImportance(e.Kind, e.Magnitude);
            e.Importance = (float)baseImp;

            if (_n == _ev.Length) Array.Resize(ref _ev, _ev.Length * 2);
            _ev[_n++] = e;

            Propagate(e.Cause1, baseImp * 0.5, 8);
            if (e.Cause2 != 0) Propagate(e.Cause2, baseImp * 0.25, 3);

            Index(e.Subject, e.Id);
            Index(e.Object, e.Id);
            Index(e.Other, e.Id);
            IndexPlace(in e);
            return e.Id;
        }

        void Propagate(long cause, double share, int maxDepth)
        {
            int depth = 0;
            while (cause != 0 && depth < maxDepth && share >= 0.01)
            {
                int i = IndexOf(cause);
                if (i < 0) return;
                _ev[i].Importance = (float)(_ev[i].Importance + share);
                cause = _ev[i].Cause1;
                share *= 0.5;
                depth++;
            }
        }

        void Index(EntityRef r, long id)
        {
            if (r.IsNone) return;
            if (!_byEntity.TryGetValue(r.Packed, out var list)) { list = new LongList(); _byEntity[r.Packed] = list; }
            list.Add(id);
        }

        static bool PlaceEvent(HistoryKind k) => k == HistoryKind.HeadmanRose || k == HistoryKind.VillageSplit || k == HistoryKind.FieldsWorkedHarder;

        void IndexPlace(in HistoryEvent e)
        {
            if (!PlaceEvent(e.Kind) || e.Settlement == 0) return;
            if (!_byPlace.TryGetValue(e.Settlement, out var list)) { list = new LongList(); _byPlace[e.Settlement] = list; }
            list.Add(e.Id);
        }

        /// <summary>A village's political events (its headmen risen, its splits), oldest first; null if none (pages only).</summary>
        public LongList PlaceEventsOf(int sid) => _byPlace.TryGetValue(sid, out var l) ? l : null;

        /// <summary>Binary search by id; -1 if not kept.</summary>
        public int IndexOf(long id)
        {
            int lo = 0, hi = _n - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                long v = _ev[mid].Id;
                if (v == id) return mid;
                if (v < id) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        public bool TryGet(long id, out HistoryEvent e)
        {
            int i = IndexOf(id);
            if (i < 0) { e = default; return false; }
            e = _ev[i];
            return true;
        }

        /// <summary>Index of the first event with id &gt; afterId (for observers' cursors).</summary>
        public int FirstIndexAfter(long afterId)
        {
            int lo = 0, hi = _n;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_ev[mid].Id <= afterId) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        public LongList EventsOf(EntityRef r) => _byEntity.TryGetValue(r.Packed, out var l) ? l : null;

        /// <summary>Follows primary causes from an event to its root; returns the chain (effect first).</summary>
        public int CausalChain(long id, long[] buffer)
        {
            int n = 0;
            long cur = id;
            while (cur != 0 && n < buffer.Length)
            {
                buffer[n++] = cur;
                int i = IndexOf(cur);
                if (i < 0) break;
                cur = _ev[i].Cause1;
            }
            return n;
        }

        // ---------- entity registry ----------
        public void Register(EntityRef r, string name, long created)
        {
            _entities[r.Packed] = new EntityInfo { Name = name, Created = created };
        }

        public void EndEntity(EntityRef r, long ended)
        {
            if (_entities.TryGetValue(r.Packed, out var info)) { info.Ended = ended; _entities[r.Packed] = info; }
        }

        public string NameOf(EntityRef r)
        {
            if (r.IsNone) return "";
            return _entities.TryGetValue(r.Packed, out var info) && info.Name != null ? info.Name : r.ToString();
        }

        public bool TryGetEntity(EntityRef r, out EntityInfo info) => _entities.TryGetValue(r.Packed, out info);
        public int EntityCount => _entities.Count;

        // ---------- compaction ----------
        /// <summary>
        /// Drops events that no longer matter (11 §4) and rewires causes of kept events to the nearest kept
        /// ancestor. <paramref name="subjectKept"/> says whether a personal event's subject still matters
        /// (alive, close kin of the living, notable). Returns the number dropped.
        /// </summary>
        public int Compact(long now, Func<EntityRef, bool> subjectKept)
        {
            var droppedCause = new Dictionary<long, long>();
            int w = 0;
            for (int i = 0; i < _n; i++)
            {
                ref HistoryEvent e = ref _ev[i];
                if (Keep(in e, now, subjectKept)) { if (w != i) _ev[w] = e; w++; }
                else droppedCause[e.Id] = e.Cause1;
            }
            int dropped = _n - w;
            for (int i = w; i < _n; i++) _ev[i] = default;
            _n = w;
            if (dropped > 0)
            {
                for (int i = 0; i < _n; i++)
                {
                    _ev[i].Cause1 = Rewire(_ev[i].Cause1, droppedCause);
                    _ev[i].Cause2 = Rewire(_ev[i].Cause2, droppedCause);
                }
                RebuildIndex();
            }
            _compactedThrough = LastId;
            Dropped += dropped;
            return dropped;
        }

        static long Rewire(long cause, Dictionary<long, long> dropped)
        {
            int guard = 0;
            while (cause != 0 && dropped.TryGetValue(cause, out long up) && guard++ < 64) cause = up;
            return cause;
        }

        static bool Keep(in HistoryEvent e, long now, Func<EntityRef, bool> subjectKept)
        {
            if (e.Tier >= Tier.Historical) return true;
            if ((e.Flags & EventFlags.Pinned) != 0) return true;
            long age = now - e.Time;
            if (e.Tier == Tier.Local)
            {
                if (age < 200 * SimTime.Year) return true;
                // a structure's own story is kept while the structure stands or its ruin lasts (C1.6: a 300-year-old town's buildings
                // keep their histories)
                if (e.Object.Kind == EntityKind.Building && subjectKept != null && subjectKept(e.Object)) return true;
                return e.Importance >= 10f;
            }
            // personal
            if (e.Importance >= 5f) return true;
            if (age < 2 * SimTime.Year) return true;
            return subjectKept != null && subjectKept(e.Subject);
        }

        void RebuildIndex()
        {
            _byEntity.Clear(); _byPlace.Clear();
            for (int i = 0; i < _n; i++)
            {
                Index(_ev[i].Subject, _ev[i].Id);
                Index(_ev[i].Object, _ev[i].Id);
                Index(_ev[i].Other, _ev[i].Id);
                IndexPlace(in _ev[i]);
            }
        }

        // ---------- persistence ----------
        public void Write(BinWriter w)
        {
            w.Long(_compactedThrough);
            w.Long(Dropped);
            w.Int(_n);
            for (int i = 0; i < _n; i++) _ev[i].Write(w);
            var keys = SortedEntityKeys();
            w.Int(keys.Length);
            for (int i = 0; i < keys.Length; i++)
            {
                var info = _entities[keys[i]];
                w.Long(keys[i]); w.String(info.Name); w.Long(info.Created); w.Long(info.Ended); w.Float(info.Importance); w.Bool(info.Notable);
            }
        }

        public void Read(BinReader r)
        {
            _compactedThrough = r.Long();
            Dropped = r.Long();
            int n = r.Int();
            if (_ev.Length < n) _ev = new HistoryEvent[Math.Max(4096, n * 2)];
            for (int i = 0; i < n; i++) _ev[i] = HistoryEvent.Read(r);
            _n = n;
            _entities.Clear();
            int ne = r.Int();
            for (int i = 0; i < ne; i++)
            {
                long key = r.Long();
                var info = new EntityInfo { Name = r.String(), Created = r.Long(), Ended = r.Long(), Importance = r.Float(), Notable = r.Bool() };
                _entities[key] = info;
            }
            RebuildIndex();
        }

        long[] SortedEntityKeys()
        {
            var keys = new long[_entities.Count];
            _entities.Keys.CopyTo(keys, 0);
            Array.Sort(keys);
            return keys;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(_n);
            for (int i = 0; i < _n; i++) _ev[i].Hash(ref h);
            var keys = SortedEntityKeys();
            h.Add(keys.Length);
            for (int i = 0; i < keys.Length; i++)
            {
                var info = _entities[keys[i]];
                h.Add(keys[i]); h.Add(info.Name); h.Add(info.Created); h.Add(info.Ended); h.Add(info.Importance); h.Add(info.Notable);
            }
        }
    }
}
