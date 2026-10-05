using System;

namespace TC2.Core.Foundation
{
    /// <summary>
    /// Rows of structs addressed directly by their stable id (ids are monotonic, never reused).
    /// Pages of 1,024 rows; a page is freed when every id in it is gone (old cohorts of the dead) - also the newest page,
    /// once a later id lands beyond it: a store whose ids are sparse in a shared counter (fires among the herds' process ids)
    /// kept every such page for ever and walked them all on each pass. Iteration skips empty pages whole.
    /// O(1) access, stable iteration in ascending id order (the only order simulation logic may use).
    /// </summary>
    public sealed class PagedStore<T> where T : struct
    {
        public const int Shift = 10;
        public const int PageSize = 1 << Shift;
        public const int Mask = PageSize - 1;

        T[][] _pages = new T[8][];
        bool[][] _alive = new bool[8][];
        int[] _pageLive = new int[8];
        int _count;
        int _maxId;

        public int Count => _count;
        public int MaxId => _maxId;

        /// <summary>Pages allocated now (diagnostics and tests: memory held, pages a pass may visit).</summary>
        public int PagesHeld { get { int n = 0; for (int p = 0; p < _pages.Length; p++) if (_pages[p] != null) n++; return n; } }

        void EnsurePage(int p)
        {
            EnsureSlots(p);
            if (_pages[p] == null) { _pages[p] = new T[PageSize]; _alive[p] = new bool[PageSize]; }
        }

        /// <summary>The page tables reach page p (pages themselves are allocated only when a row lands in them).</summary>
        void EnsureSlots(int p)
        {
            if (p < _pages.Length) return;
            int n = _pages.Length;
            while (n <= p) n *= 2;
            Array.Resize(ref _pages, n);
            Array.Resize(ref _alive, n);
            Array.Resize(ref _pageLive, n);
        }

        public bool Exists(int id)
        {
            if (id <= 0 || id > _maxId) return false;
            int p = id >> Shift;
            var a = _alive[p];
            return a != null && a[id & Mask];
        }

        public ref T Get(int id)
        {
            var page = _pages[id >> Shift];
            if (page == null || !_alive[id >> Shift][id & Mask]) throw new InvalidOperationException(typeof(T).Name + " #" + id + " does not exist");
            return ref page[id & Mask];
        }

        /// <summary>Access without the existence check (hot paths that already know the id is live).</summary>
        public ref T GetUnchecked(int id) => ref _pages[id >> Shift][id & Mask];

        public ref T Add(int id, in T row)
        {
            if (id <= 0) throw new ArgumentException("id must be positive");
            int p = id >> Shift;
            int top = _maxId >> Shift;
            if (p > top && _maxId > 0 && _pages[top] != null && _pageLive[top] == 0) { _pages[top] = null; _alive[top] = null; }   // the newest page emptied before ids moved on
            EnsurePage(p);
            if (_alive[p][id & Mask]) throw new InvalidOperationException(typeof(T).Name + " #" + id + " already exists");
            _pages[p][id & Mask] = row;
            _alive[p][id & Mask] = true;
            _pageLive[p]++;
            _count++;
            if (id > _maxId) _maxId = id;
            return ref _pages[p][id & Mask];
        }

        public void Remove(int id)
        {
            if (!Exists(id)) return;
            int p = id >> Shift;
            _alive[p][id & Mask] = false;
            _pages[p][id & Mask] = default;
            _count--;
            if (--_pageLive[p] == 0 && ((p + 1) << Shift) <= _maxId)
            {
                // free the page only when no future id can land in it (ids are monotonic)
                _pages[p] = null; _alive[p] = null;
            }
        }

        /// <summary>First live id, or 0.</summary>
        public int First() => Next(0);

        /// <summary>Next live id after <paramref name="id"/>, or 0.</summary>
        public int Next(int id)
        {
            int i = id + 1;
            while (i <= _maxId)
            {
                int p = i >> Shift;
                if (p >= _alive.Length) break;
                var a = _alive[p];
                if (a == null || _pageLive[p] == 0) { i = (p + 1) << Shift; continue; }
                int end = Math.Min(((p + 1) << Shift) - 1, _maxId);
                for (; i <= end; i++) if (a[i & Mask]) return i;
            }
            return 0;
        }

        public void Clear()
        {
            _pages = new T[8][]; _alive = new bool[8][]; _pageLive = new int[8]; _count = 0; _maxId = 0;
        }

        /// <summary>Restores the max id after loading (so freed pages stay consistent with the allocator).</summary>
        public void SetMaxId(int maxId) { if (maxId > _maxId) { _maxId = maxId; EnsureSlots(maxId >> Shift); } }
    }

    /// <summary>A small growable list of ints without per-operation allocation churn.</summary>
    public sealed class IntList
    {
        int[] _a;
        int _n;
        public IntList(int cap = 4) { _a = new int[Math.Max(1, cap)]; }
        public int Count => _n;
        public int this[int i] { get => _a[i]; set => _a[i] = value; }
        public void Add(int v) { if (_n == _a.Length) Array.Resize(ref _a, _a.Length * 2); _a[_n++] = v; }
        public bool Contains(int v) { for (int i = 0; i < _n; i++) if (_a[i] == v) return true; return false; }
        public int IndexOf(int v) { for (int i = 0; i < _n; i++) if (_a[i] == v) return i; return -1; }
        /// <summary>Removes the first occurrence keeping order (order can be semantic).</summary>
        public bool Remove(int v)
        {
            int i = IndexOf(v);
            if (i < 0) return false;
            for (int j = i + 1; j < _n; j++) _a[j - 1] = _a[j];
            _n--;
            return true;
        }
        /// <summary>Inserts keeping ascending order (for id lists).</summary>
        public void InsertSorted(int v)
        {
            if (_n == _a.Length) Array.Resize(ref _a, _a.Length * 2);
            int i = _n;
            while (i > 0 && _a[i - 1] > v) { _a[i] = _a[i - 1]; i--; }
            _a[i] = v; _n++;
        }
        public void Clear() { _n = 0; }
        /// <summary>Keeps the first n values (a list compacted in place).</summary>
        public void Truncate(int n) { if (n >= 0 && n < _n) _n = n; }
        public int[] Raw => _a;

        public void Write(BinWriter w) { w.Int(_n); for (int i = 0; i < _n; i++) w.Int(_a[i]); }
        public static IntList Read(BinReader r)
        {
            int n = r.Int();
            var l = new IntList(Math.Max(4, n));
            for (int i = 0; i < n; i++) l.Add(r.Int());
            return l;
        }
        public void Hash(ref StateHasher h) { h.Add(_n); for (int i = 0; i < _n; i++) h.Add(_a[i]); }
    }
}
