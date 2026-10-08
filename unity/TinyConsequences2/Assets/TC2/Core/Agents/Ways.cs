using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Agents
{
    /// <summary>The ground between two villages as their people have walked it (C4.1).</summary>
    public struct Way
    {
        public int A, B;          // the two villages (A the lower id)
        public double Use;        // walks counted, halving each year (as at `Last`)
        public long Last;         // when it was last walked
        public byte Most;         // the most it has been: 1 a track, 2 a path, 3 a road (history tells each the first time)

        public void Write(BinWriter w) { w.Int(A); w.Int(B); w.Double(Use); w.Long(Last); w.Byte(Most); }
        public static Way Read(BinReader r) => new Way { A = r.Int(), B = r.Int(), Use = r.Double(), Last = r.Long(), Most = r.Byte() };
        public void Hash(ref StateHasher h) { h.Add(A); h.Add(B); h.Add(Use); h.Add(Last); h.Add(Most); }
    }

    /// <summary>
    /// Roads emerge from use (C4.1, D2-097; 24 §3): every journey walked between two villages - a visit, a trade, a claim, a band, a lived
    /// walk of any errand - wears the ground between them; the wear is a count of walks that halves each year it is not renewed (read with
    /// its decay at any moment, so no day passes over the ways). Walked a few times a year the way is a **track**, some forty a year a worn
    /// **path**, a couple of hundred a year a **road** beaten broad; worn ways are quicker underfoot (a path 1.08x, a road 1.15x), and an
    /// unused way greens over and is gone. Cleared and kept roads, bridges and carts come with C4.2. The view draws the ways as their
    /// state is, never a road between villages nobody walks between.
    /// </summary>
    public sealed class Ways
    {
        readonly Dictionary<long, Way> _ways = new Dictionary<long, Way>();

        /// <summary>Years in which an unwalked way's wear halves.</summary>
        public const double HalfLifeYears = 1.0;
        /// <summary>Wear (walks, decaying) from which a way is a track, a path, a road.</summary>
        public const double Track = 4.0, Path = 40.0, Road = 250.0;

        public static long Key(int a, int b) => RouteCache.Key(a, b);
        public int Count => _ways.Count;

        /// <summary>Walks between two villages (n may be fractional: a COARSE day's expected visits). History tells a path and a road the first time.</summary>
        public void Walk(World w, int a, int b, double n)
        {
            if (a <= 0 || b <= 0 || a == b || n <= 0) return;
            long k = Key(a, b);
            _ways.TryGetValue(k, out var way);
            if (way.A == 0) { way.A = a < b ? a : b; way.B = a < b ? b : a; }
            way.Use = Decayed(in way, w.Now) + n;
            way.Last = w.Now;
            byte state = StateOf(way.Use);
            if (state > way.Most)
            {
                way.Most = state;
                if (state >= 2)
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.WayWorn, Time = w.Now, Cell = w.Settlements.Exists(way.A) ? w.Settlements.Get(way.A).Cell : 0, Settlement = way.A,
                        Subject = EntityRef.Settlement(way.A), Object = EntityRef.Settlement(way.B), ArgA = state, Magnitude = (float)way.Use, RootKind = RootKind.People,
                    });
            }
            _ways[k] = way;
        }

        static double Decayed(in Way way, long now) => way.Last == 0 ? 0.0 : way.Use * DMath.Exp(-0.693147180559945 * (now - way.Last) / (HalfLifeYears * SimTime.Year));

        public static byte StateOf(double use) => use >= Road ? (byte)3 : use >= Path ? (byte)2 : use >= Track ? (byte)1 : (byte)0;

        /// <summary>The wear of the way between two villages now (0 none).</summary>
        public double UseOf(long now, int a, int b) => _ways.TryGetValue(Key(a, b), out var way) ? Decayed(in way, now) : 0.0;

        /// <summary>The way's state now: 0 nothing worn, 1 a track, 2 a path, 3 a road.</summary>
        public byte State(long now, int a, int b) => StateOf(UseOf(now, a, b));

        /// <summary>How much quicker a walker goes on it (a path 1.08x, a road 1.15x).</summary>
        public double Speed(long now, int a, int b)
        {
            byte s = State(now, a, b);
            return s >= 3 ? 1.15 : s == 2 ? 1.08 : 1.0;
        }

        /// <summary>Every way walked enough to show (a track or more), in key order (the view; tools).</summary>
        public void Collect(long now, List<Way> into)
        {
            into.Clear();
            var keys = new List<long>(_ways.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                var way = _ways[keys[i]];
                double u = Decayed(in way, now);
                if (u < Track) continue;
                way.Use = u; way.Last = now;
                into.Add(way);
            }
        }

        /// <summary>The ways of one village that were ever worn to a path or a road (C13.1: its streets), in key order.</summary>
        public void PathsOf(int sid, List<Way> into)
        {
            into.Clear();
            var keys = new List<long>(_ways.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                var way = _ways[keys[i]];
                if ((way.A == sid || way.B == sid) && way.Most >= 2) into.Add(way);
            }
        }

        /// <summary>Ways worn to nothing leave the table (yearly housekeeping).</summary>
        public void Prune(long now)
        {
            var gone = new List<long>();
            foreach (var kv in _ways) if (Decayed(kv.Value, now) < 0.05) gone.Add(kv.Key);
            for (int i = 0; i < gone.Count; i++) _ways.Remove(gone[i]);
        }

        public void Write(BinWriter w)
        {
            var keys = new List<long>(_ways.Keys);
            keys.Sort();
            w.Int(keys.Count);
            for (int i = 0; i < keys.Count; i++) _ways[keys[i]].Write(w);
        }

        public void Read(BinReader r)
        {
            _ways.Clear();
            int n = r.Int();
            for (int i = 0; i < n; i++) { var way = Way.Read(r); _ways[Key(way.A, way.B)] = way; }
        }

        public void Hash(ref StateHasher h)
        {
            var keys = new List<long>(_ways.Keys);
            keys.Sort();
            h.Add(keys.Count);
            for (int i = 0; i < keys.Count; i++) _ways[keys[i]].Hash(ref h);
        }
    }
}
