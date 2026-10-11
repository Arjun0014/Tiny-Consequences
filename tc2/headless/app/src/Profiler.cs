using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    /// <summary>Wall-clock time per event kind (and per periodic system) for profiling runs.</summary>
    /// <summary>Tools (`--trace FILE`): every event and part of a day entered and left, written as it happens (flushed) - where a run that hangs
    /// stopped is its last line. Never affects state.</summary>
    public sealed class TraceProfiler : ISimProfiler
    {
        readonly System.IO.StreamWriter _o;
        readonly TC2.Core.Simulation.World _w;
        int _depth;
        public TraceProfiler(string path, TC2.Core.Simulation.World w) { _o = new System.IO.StreamWriter(path) { AutoFlush = true }; _w = w; }
        static string Name(int k) => k >= 10000 ? "part " + (k - 10000) : ((EventKind)(k / 16)).ToString() + (k / 16 == (int)EventKind.SystemTick ? "#" + (k % 16) : "");
        public void Begin(int kind) { _o.WriteLine(new string(' ', 2 * _depth) + "> " + Name(kind) + "  " + TC2.Core.Foundation.SimTime.Format(_w.Now)); _depth++; }
        public void End(int kind) { _depth--; _o.WriteLine(new string(' ', 2 * _depth) + "< " + Name(kind)); }
    }

    public sealed class KindProfiler : ISimProfiler
    {
        readonly Dictionary<int, (long ticks, long count)> _t = new Dictionary<int, (long, long)>();
        readonly Stopwatch _sw = Stopwatch.StartNew();
        readonly Stack<long> _start = new Stack<long>();   // (parts of an event nest inside it: keys >= 10000)
        public void Begin(int kind) { _start.Push(_sw.ElapsedTicks); }
        public void End(int kind)
        {
            long d = _sw.ElapsedTicks - _start.Pop();
            _t[kind] = _t.TryGetValue(kind, out var v) ? (v.ticks + d, v.count + 1) : (d, 1);
        }
        static readonly string[] Parts = { "crops", "food", "survey", "weigh", "coarse-day", "demography", "stock", "survey.grounds", "survey.herds", "food.memory", "food.need", "food.share", "hearths", "coarse.chores", "learning", "disease", "trade", "conflict",
                                           "coarse.herd", "coarse.eat", "coarse.craft", "coarse.work", "coarse.wild", "conflict.watch", "conflict.claims", "conflict.raids", "conflict.news", "conflict.take" };
        public void Print()
        {
            double total = _t.Where(k => k.Key < 10000).Sum(v => (double)v.Value.ticks);
            foreach (var kv in _t.OrderByDescending(k => k.Value.ticks))
            {
                var kind = (EventKind)(kv.Key / 16);
                string name = kv.Key >= 10000 ? "  daily." + (kv.Key - 10000 < Parts.Length ? Parts[kv.Key - 10000] : (kv.Key - 10000).ToString()) :
                              kind + (kind == EventKind.SystemTick ? "#" + (kv.Key % 16) : "");
                double ms = kv.Value.ticks * 1000.0 / Stopwatch.Frequency;
                Console.WriteLine("  " + name.PadRight(22) + ms.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(9) + " ms  " +
                                  (100 * kv.Value.ticks / total).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5) + "%  n " + kv.Value.count +
                                  "  " + (ms * 1000 / kv.Value.count).ToString("0.00", CultureInfo.InvariantCulture) + " us each");
            }
        }
    }
}
