using System;
using System.Diagnostics;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    /// <summary>
    /// The slow climate alone (no weather, life or people): orbit, energy balance, ice and sea level over millennia in
    /// seconds, for tuning. Usage: climate [--seed N] [--years Y] [--every Y] [--co2 PPM] [--aerosol TAU]
    /// </summary>
    public static partial class Commands
    {
        public static int ClimateRun(Args a)
        {
            var cfg = ConfigFrom(a);
            cfg.Settlements = 0;
            var sw = Stopwatch.StartNew();
            var w = WorldGenerator.Create(cfg);
            var P = w.Planet;
            var C = w.PlanetSys.Climate;
            Console.WriteLine("worldgen " + sw.ElapsedMilliseconds + " ms");
            if (a.Has("co2")) P.CO2 = a.Double("co2", 280);
            if (a.Has("aerosol")) P.Aerosol = a.Double("aerosol", 0);
            double years = a.Double("years", 3000), every = a.Double("every", 100);
            Console.WriteLine("year     obliq  ecc    sumN  sumS   Tglob  Teq   Tpole  iceKm3  ice%   sea(m)  glacial");
            long t = w.Now;
            int steps = (int)(years / Climate.StepYears);
            int each = Math.Max(1, (int)(every / Climate.StepYears));
            var run = Stopwatch.StartNew();
            for (int i = 0; i <= steps; i++)
            {
                if (i % each == 0)
                {
                    int land = 0, iced = 0;
                    for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) { land++; if (P.Ice[c] > 5f) iced++; }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,6:0} {1,6:0.0} {2,6:0.000} {3,5:0.00} {4,5:0.00} {5,7:0.00} {6,5:0.0} {7,6:0.0} {8,7:0.00} {9,6:0.0} {10,7:0.0} {11,4}",
                        (t - w.Now) / (double)SimTime.Year, P.Tilt * 180 / DMath.PI, P.Ecc, Climate.SummerFactor(P, true), Climate.SummerFactor(P, false),
                        P.TGlobal, P.ZonalT[Climate.Bands / 2], P.ZonalT[Climate.Bands - 1], P.IceVolume / 1e9, 100.0 * iced / land,
                        P.SeaLevelNow - P.SeaLevel0, P.Glacial));
                }
                C.Step(P, w.Seed, t, Climate.StepYears);
                t += SimTime.Year / 4;
            }
            Console.WriteLine("ran " + years + " climate years in " + run.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
            return 0;
        }
    }
}
