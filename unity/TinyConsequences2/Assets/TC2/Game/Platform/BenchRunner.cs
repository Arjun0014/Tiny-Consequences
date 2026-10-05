using System.Diagnostics;
using System.Globalization;
using System.Text;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// In-browser benchmark (14 §3): builds its own world (never the player's), advances it in frame slices with a
    /// large budget, and reports pure simulation time, wall time and throughput through the test bridge.
    /// Started by the bridge command "bench SETTLEMENTS FOUNDERS YEARS activity|coarse".
    /// </summary>
    public sealed class BenchRunner : MonoBehaviour
    {
        World _w;
        long _target;
        double _simMs;
        readonly Stopwatch _wall = new Stopwatch();
        readonly Stopwatch _frame = new Stopwatch();
        public string Result = "";
        public bool Running => _w != null;
        string _label;
        int _frames;
        double _genMs;

        public void Begin(int settlements, int founders, double years, bool coarse)
        {
            var sw = Stopwatch.StartNew();
            _w = WorldGenerator.Create(new WorldConfig { Seed = 42, Settlements = settlements, FoundersPerSettlement = founders });
            _genMs = sw.Elapsed.TotalMilliseconds;
            if (coarse) _w.RequestResolution(TC2.Core.Simulation.Resolution.Coarse, SimTime.StartOfDay(_w.Now) + SimTime.Day);
            _target = _w.Now + SimTime.Years(years);
            _simMs = 0; _frames = 0;
            _label = settlements + "x" + founders + " " + (coarse ? "coarse" : "activity") + " " + years.ToString("0.##", CultureInfo.InvariantCulture) + "y";
            Result = "running " + _label;
            _wall.Restart();
        }

        void Update()
        {
            if (_w == null) return;
            _frame.Restart();
            var budget = new Budget { MaxEvents = long.MaxValue, ShouldYield = () => _frame.Elapsed.TotalMilliseconds > 45 };
            bool done = _w.AdvanceTo(_target, ref budget);
            _simMs += _frame.Elapsed.TotalMilliseconds;
            _frames++;
            if (!done) { Result = "running " + _label + " " + SimTime.FormatShort(_w.Now) + " pop " + _w.LivingPeople(); return; }
            double years = (_target - _w.Created) / (double)SimTime.Year;
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.Append("done ").Append(_label)
              .Append(" gen_ms ").Append(_genMs.ToString("0", inv))
              .Append(" sim_ms ").Append(_simMs.ToString("0", inv))
              .Append(" wall_ms ").Append(_wall.Elapsed.TotalMilliseconds.ToString("0", inv))
              .Append(" years_per_s ").Append((years / (_simMs / 1000.0)).ToString("0.000", inv))
              .Append(" events ").Append(_w.Metrics.Events)
              .Append(" us_per_event ").Append((_simMs * 1000.0 / System.Math.Max(1, _w.Metrics.Events)).ToString("0.000", inv))
              .Append(" pop ").Append(_w.LivingPeople())
              .Append(" frames ").Append(_frames);
            Result = sb.ToString();
            UnityEngine.Debug.Log("[TC2] bench " + Result);
            _w = null;
        }
    }
}
