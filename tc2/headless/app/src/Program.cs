using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace TC2.Headless
{
    public static class Program
    {
        public static int Main(string[] argv)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            var args = new Args(argv);
            string cmd = args.Positional(0) ?? "help";
            try
            {
                switch (cmd)
                {
                    case "test":
                        Commands.Switches(args);
                        return TestRunner.Run(typeof(TC2.Tests.Core.Marker).Assembly, args.Get("filter"), args.Get("category"), args.Has("explicit"), args.Has("v"));
                    case "run":
                        return Commands.Run(args);
                    case "bench":
                        return Commands.Bench(args);
                    case "hash":
                        return Commands.Hash(args);
                    case "hashtest":
                        if (args.Has("parts")) System.Console.WriteLine(TC2.Core.Diagnostics.CrossRuntime.Parts());
                        System.Console.WriteLine(TC2.Core.Diagnostics.CrossRuntime.Probe());
                        return 0;
                    case "probe":
                        return Commands.Probe(args);
                    case "follow":
                        return Commands.Follow(args);
                    case "timeuse":
                        Commands.Switches(args);
                        return Commands.TimeUse(args);
                    case "chronicle":
                        return Commands.ChronicleBook(args);
                    case "inspect":
                        return Commands.Inspect(args);
                    case "person":
                        return Commands.PersonPage(args);
                    case "place":
                        Commands.Switches(args);   // (the rules before the world loads: `--oldrain` sets the rain's seasons as the planet's systems are built)
                        return Commands.PlacePage(args);
                    case "wet":
                        Commands.Switches(args);   // (the rules a save was lived under: a control world's switches)
                        return Commands.Wet(args);
                    case "crowds":
                        Commands.Switches(args);
                        return Commands.Crowds(args);
                    case "polity":
                        return Commands.Polity(args);
                    case "lordships":
                        return Commands.Lordships(args);
                    case "voyages":
                        Commands.Switches(args);
                        return Commands.Voyages(args);
                    case "woodways":
                        return Commands.WoodWays(args);
                    case "planet":
                        return Commands.Planet(args);
                    case "climate":
                        return Commands.ClimateRun(args);
                    default:
                        Console.WriteLine("tc2 headless: test [--filter S] [--category C] [--explicit] [--v] | run [--seed N] [--years Y] [--out DIR] | bench [--name B] | hash [--seed N] [--days D] | inspect --save FILE | planet [--seed N] [--years Y] [--every D] [--map DIR] [--nopeople]");
                        return cmd == "help" ? 0 : 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex);
                return 3;
            }
        }
    }

    /// <summary>--key value / --flag / positional argument parsing.</summary>
    public sealed class Args
    {
        readonly Dictionary<string, string> _kv = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<string> _pos = new List<string>();

        public Args(string[] a)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].StartsWith("--", StringComparison.Ordinal))
                {
                    string k = a[i].Substring(2);
                    if (i + 1 < a.Length && !a[i + 1].StartsWith("--", StringComparison.Ordinal)) _kv[k] = a[++i];
                    else _kv[k] = "true";
                }
                else _pos.Add(a[i]);
            }
        }

        public string Positional(int i) => i < _pos.Count ? _pos[i] : null;
        public bool Has(string k) => _kv.ContainsKey(k);
        public string Get(string k, string def = null) => _kv.TryGetValue(k, out var v) ? v : def;
        public int Int(string k, int def) => _kv.TryGetValue(k, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : def;
        public long Long(string k, long def) => _kv.TryGetValue(k, out var v) ? long.Parse(v, CultureInfo.InvariantCulture) : def;
        public double Double(string k, double def) => _kv.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : def;
    }
}
