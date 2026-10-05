using System;
using TC2.Core.Foundation;

namespace TC2.Core.Scheduling
{
    /// <summary>Kinds of scheduled events. Append only: values are saved.</summary>
    public enum EventKind : ushort
    {
        None = 0,
        Command = 1,          // player/test command (A = command index in the command log)
        SystemTick = 2,       // periodic system (Target = system id)
        ProcessStep = 3,      // a process (fire, flood...) steps (Target = process id)
        AgentStep = 4,        // a person's activity step ends (Target = person id, Version = activity version)
        AgentInterrupt = 5,   // a person must re-decide now (Target = person id, A = reason code, B = cause event)
        SettlementDaily = 6,  // per-settlement daily demography and accounts (Target = settlement id)
        Birth = 7,            // a pregnancy comes to term (Target = mother id, Version = pregnancy stamp)
        Housekeeping = 8,     // yearly compaction, audits
        Lightning = 9,        // a natural strike at a cell (A = cell)
        Eruption = 10,        // a volcano erupts (A = vent id)
        Quake = 11,           // a fault patch ruptures (A = cell)
        StormStep = 12,       // every storm moves and acts (A unused)
        PartyDepart = 13,     // a party of households sets out (Target = party id; D2-045)
        PartyArrive = 14,     // a party arrives at its new home (Target = party id)
    }

    /// <summary>Ordering classes at equal time (04 §2).</summary>
    public static class Phase
    {
        public const int Command = 0;
        public const int SystemBase = 10;  // + system order (10..19)
        public const int Natural = 18;     // natural triggers (lightning)
        public const int Process = 20;
        public const int Agent = 30;
        public const int Settlement = 35;
        public const int Housekeeping = 40;
    }

    /// <summary>An event in the future event list. Plain data: serialized exactly with the world.</summary>
    public struct ScheduledEvent
    {
        public long Time;
        public int Phase;
        public long Seq;
        public EventKind Kind;
        public int Target;
        public int Version;
        public long A, B;

        public bool Precedes(in ScheduledEvent o)
        {
            if (Time != o.Time) return Time < o.Time;
            if (Phase != o.Phase) return Phase < o.Phase;
            return Seq < o.Seq;
        }

        public void Write(BinWriter w)
        {
            w.Long(Time); w.Int(Phase); w.Long(Seq); w.UShort((ushort)Kind); w.Int(Target); w.Int(Version); w.Long(A); w.Long(B);
        }

        public static ScheduledEvent Read(BinReader r)
        {
            ScheduledEvent e;
            e.Time = r.Long(); e.Phase = r.Int(); e.Seq = r.Long(); e.Kind = (EventKind)r.UShort(); e.Target = r.Int();
            e.Version = r.Int(); e.A = r.Long(); e.B = r.Long();
            return e;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Time); h.Add(Phase); h.Add(Seq); h.Add((int)Kind); h.Add(Target); h.Add(Version); h.Add(A); h.Add(B);
        }

        public override string ToString() => SimTime.FormatShort(Time) + " p" + Phase + " #" + Seq + " " + Kind + " t" + Target + " v" + Version;
    }

    /// <summary>
    /// The future event list: a 4-ary min-heap of structs ordered by (time, phase, seq) (D2-005).
    /// Lazy cancellation: callers compare version stamps when an event is popped.
    /// The heap's array order is not semantic; the serialized form is sorted so that equal states
    /// always produce identical bytes and hashes.
    /// </summary>
    public sealed class EventQueue
    {
        ScheduledEvent[] _h = new ScheduledEvent[1024];
        int _n;
        long _seq = 1;

        public int Count => _n;
        public long NextSeq => _seq;

        public long Push(long time, int phase, EventKind kind, int target, int version = 0, long a = 0, long b = 0)
        {
            ScheduledEvent e;
            e.Time = time; e.Phase = phase; e.Seq = _seq++; e.Kind = kind; e.Target = target; e.Version = version; e.A = a; e.B = b;
            PushEvent(e);
            return e.Seq;
        }

        void PushEvent(in ScheduledEvent e)
        {
            if (_n == _h.Length) Array.Resize(ref _h, _h.Length * 2);
            int i = _n++;
            // sift up
            while (i > 0)
            {
                int parent = (i - 1) >> 2;
                if (!e.Precedes(in _h[parent])) break;
                _h[i] = _h[parent];
                i = parent;
            }
            _h[i] = e;
        }

        public ref readonly ScheduledEvent Peek()
        {
            if (_n == 0) throw new InvalidOperationException("empty event queue");
            return ref _h[0];
        }

        public long PeekTime() => _n == 0 ? long.MaxValue : _h[0].Time;

        public ScheduledEvent Pop()
        {
            if (_n == 0) throw new InvalidOperationException("empty event queue");
            ScheduledEvent top = _h[0];
            _n--;
            if (_n > 0)
            {
                ScheduledEvent last = _h[_n];
                int i = 0;
                while (true)
                {
                    int c = (i << 2) + 1;
                    if (c >= _n) break;
                    int best = c;
                    int end = c + 4 < _n ? c + 4 : _n;
                    for (int k = c + 1; k < end; k++)
                        if (_h[k].Precedes(in _h[best])) best = k;
                    if (!_h[best].Precedes(in last)) break;
                    _h[i] = _h[best];
                    i = best;
                }
                _h[i] = last;
            }
            _h[_n] = default;
            return top;
        }

        /// <summary>All pending events in their dispatch order (allocates; for saves, hashes and audits).</summary>
        public ScheduledEvent[] SortedSnapshot()
        {
            var a = new ScheduledEvent[_n];
            Array.Copy(_h, a, _n);
            Array.Sort(a, (x, y) => x.Precedes(in y) ? -1 : (y.Precedes(in x) ? 1 : 0));
            return a;
        }

        /// <summary>Visits pending events in heap order (not dispatch order) - for audits only.</summary>
        public int RawCount => _n;
        public ref readonly ScheduledEvent RawAt(int i) => ref _h[i];

        public void Clear() { _n = 0; _seq = 1; }

        public void Write(BinWriter w)
        {
            w.Long(_seq);
            var s = SortedSnapshot();
            w.Int(s.Length);
            for (int i = 0; i < s.Length; i++) s[i].Write(w);
        }

        public void Read(BinReader r)
        {
            Clear();
            _seq = r.Long();
            int n = r.Int();
            if (_h.Length < n) _h = new ScheduledEvent[Math.Max(1024, n * 2)];
            // already sorted: a sorted array is a valid heap
            for (int i = 0; i < n; i++) _h[i] = ScheduledEvent.Read(r);
            _n = n;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(_seq);
            var s = SortedSnapshot();
            h.Add(s.Length);
            for (int i = 0; i < s.Length; i++) s[i].Hash(ref h);
        }
    }
}
