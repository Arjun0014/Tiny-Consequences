using TC2.Core.Foundation;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Core.Planet
{
    /// <summary>Ids of periodic systems (their order is the order at equal times: phase = SystemBase + id).</summary>
    public static class SystemId
    {
        public const int Climate = 1;
        public const int Weather = 2;
        public const int Vegetation = 3;
        public const int ClimateSlow = 4;
        public const int Count = 5;
    }

    /// <summary>
    /// Foundation versions of the planet's periodic systems (05 §3, §4): closed-form temperature, daily
    /// stochastic weather with spatially correlated rain, soil water, lightning, and vegetation growth.
    /// The full energy-balance climate, hydrology and PFT vegetation replace these in F1.
    /// </summary>
    public sealed class PlanetSystems
    {
        readonly int[] _block;   // surface cell -> weather block (N8 cube-sphere cell)
        readonly float[] _tmean; // annual mean temperature per cell (static for a static climate)
        // closed-form temperature terms per cell (derived): T = base + amp*season + dAmp*cos(dayPhase + phase)
        readonly double[] _base, _amp, _dAmp, _cosPh, _sinPh, _hemi;
        readonly float[] _runoff;
        public readonly Climate Climate;

        public PlanetSystems(PlanetState P)
        {
            var coarse = new CubeSphere(8);
            int n = P.Cells;
            _block = new int[n]; _tmean = new float[n];
            _base = new double[n]; _amp = new double[n]; _dAmp = new double[n]; _cosPh = new double[n]; _sinPh = new double[n]; _hemi = new double[n];
            _runoff = new float[n];
            for (int c = 0; c < n; c++)
            {
                _block[c] = coarse.CellOf(P.Grid.Center[c]);
                _hemi[c] = P.Lat[c] >= 0 ? 1.0 : -1.0;
                // local solar hour = world hour + lon/2pi*24; diurnal peak at 15:00 local
                double ph = P.Lon[c] - DMath.TwoPi * 15.0 / 24.0;
                _cosPh[c] = DMath.Cos(ph); _sinPh[c] = DMath.Sin(ph);
            }
            Climate = new Climate(P);
            Climate.Derive(P);
            RefreshMeans(P);
        }

        /// <summary>The fast climate's terms from the slow climate's per-cell annual mean and seasonal swing.</summary>
        public void RefreshMeans(PlanetState P)
        {
            for (int c = 0; c < P.Cells; c++)
            {
                bool land = P.IsLand(c);
                double t = P.TMean[c];
                _tmean[c] = (float)t;
                _base[c] = t;
                _amp[c] = P.TAmp[c] * _hemi[c];
                _dAmp[c] = land ? 5.0 : 1.0;
            }
        }

        public double MeanTemp(int c) => _tmean[c];

        public void Tick(World w, int system)
        {
            switch (system)
            {
                case SystemId.Climate: FastClimate(w); break;
                case SystemId.Weather: Weather(w); Rivers(w); break;
                case SystemId.Vegetation: Vegetation(w); break;
                case SystemId.ClimateSlow: SlowClimate(w); break;
            }
        }

        /// <summary>A season of the slow climate: energy balance, ice, sea level; then the fast terms and the coast.</summary>
        void SlowClimate(World w)
        {
            var P = w.Planet;
            Climate.Step(P, w.Seed, w.Now, Climate.StepYears);
            RefreshMeans(P);
            w.AfterClimate();
        }

        void FastClimate(World w)
        {
            var P = w.Planet;
            double f = SimTime.YearFraction(w.Now);
            double season = DMath.Sin(DMath.TwoPi * (f - 0.25));
            double a = DMath.TwoPi * SimTime.SecondOfDay(w.Now) / (double)SimTime.Day;
            double ca = DMath.Cos(a), sa = DMath.Sin(a);
            var T = P.Temp;
            for (int c = 0; c < T.Length; c++)
                T[c] = (float)(_base[c] + _amp[c] * season + _dAmp[c] * (ca * _cosPh[c] - sa * _sinPh[c]));
        }

        void Weather(World w)
        {
            var P = w.Planet;
            long day = SimTime.FloorDiv(w.Now, SimTime.Day);
            double yf = SimTime.YearFraction(w.Now);
            double seasonN = DMath.Sin(DMath.TwoPi * (yf - 0.125));
            ulong seed = w.Seed;
            for (int c = 0; c < P.Cells; c++)
            {
                // wetter summers at mid latitudes, wet season shifting with the sun in the tropics
                double season = seasonN * _hemi[c];
                double moist = DMath.Min(1.0, P.Moist[c] * P.MoistFactor); // a warmer world is a wetter one
                double pRain = DMath.Clamp(0.06 + 0.5 * moist + 0.08 * season * moist, 0.02, 0.85);
                double ub = KeyedRng.Unit(seed, RngStream.Weather, (ulong)_block[c], (ulong)day);
                double uc = KeyedRng.Unit(seed, RngStream.Weather, (ulong)c + 100000UL, (ulong)day);
                double u = 0.7 * ub + 0.3 * uc;
                double rain = u < pRain ? (pRain - u) / pRain * 1.6 : 0.0;
                P.RainToday[c] = (float)rain;
                _runoff[c] = 0;
                if (!P.IsLand(c)) continue;
                double t = P.Temp[c];
                if (P.LakeDepth[c] > 0.3f) { _runoff[c] = (float)(rain * 0.020); P.SoilWater[c] = 1f; continue; } // open water
                double sw = P.SoilWater[c];
                // a rain unit is ~20 mm; a downpour runs off before it soaks in, a full soil sheds the rest
                double quick = rain > 1.0 ? (rain - 1.0) * 0.35 : 0.0;
                sw += (rain - quick) * 0.32;
                double evap = (0.004 + 0.0016 * DMath.Max(0, t)) * (0.4 + sw);
                sw -= evap;
                double excess = sw > 0.85 ? sw - 0.85 : 0.0;
                sw -= excess;
                double baseflow = sw * 0.004;
                sw -= baseflow;
                _runoff[c] = (float)((quick + excess / 0.32 + baseflow / 0.32) * 0.020); // metres of water
                P.SoilWater[c] = (float)DMath.Clamp01(sw);
                // lightning: warm, wet, stormy days
                if (rain > 0.35 && t > 10)
                {
                    double lam = w.Config.LightningPerCellDay * rain * (t - 10) / 10.0;
                    double ul = KeyedRng.Unit(seed, RngStream.Lightning, (ulong)c, (ulong)day);
                    if (ul < lam)
                    {
                        // afternoon-weighted time within the day
                        double tu = KeyedRng.Unit(seed, RngStream.Lightning, (ulong)c, (ulong)day, 1);
                        long at = w.Now + 10 * SimTime.Hour + DMath.ToLong(tu * 10 * SimTime.Hour);
                        w.Queue.Push(at, Phase.Natural, EventKind.Lightning, 0, 0, c);
                    }
                }
            }
        }

        /// <summary>The day's runoff down the rivers; the monthly-smoothed flow defines rivers (and floods later).</summary>
        void Rivers(World w)
        {
            var P = w.Planet;
            var d = w.Drainage;
            if (d == null) return;
            d.Route(P, _runoff);
            var flow = P.RiverFlow;
            for (int c = 0; c < flow.Length; c++)
            {
                double q = d.Discharge[c];
                flow[c] = (float)(flow[c] + (q - flow[c]) * (1.0 / 30.0));
            }
        }

        void Vegetation(World w)
        {
            var P = w.Planet;
            double dtY = w.Config.VegetationPeriod / (double)SimTime.Year;
            var grid = P.Grid;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) { P.ClassifyBiome(c, w.Now); continue; }
                double tm = _tmean[c];
                double m = P.Moist[c];
                double wet = DMath.Clamp01(0.5 * m + 0.5 * P.SoilWater[c] / 0.6);
                double tSuitF = DMath.SmoothStep(-1, 8, tm) * (1.0 - DMath.SmoothStep(30, 38, tm));
                double tSuitG = DMath.SmoothStep(-6, 4, tm);
                double kF = DMath.Clamp01(1.25 * m - 0.12) * tSuitF;
                double kG = DMath.Clamp01(0.25 + 0.9 * wet) * tSuitG;
                double f = P.Forest[c], g = P.Grass[c];
                f += 0.085 * dtY * f * (1.0 - f / DMath.Max(0.02, kF));
                if (f < 0.03 && kF > 0.15)
                {
                    double nbF = 0;
                    for (int k = 0; k < 4; k++) nbF += P.Forest[grid.Nbr4[c * 4 + k]];
                    f += dtY * 0.02 * (nbF > 0.3 ? 1.0 : 0.05);
                }
                g += 1.1 * dtY * g * (1.0 - (g + 0.5 * f) / DMath.Max(0.05, kG));
                if (g < 0.02 && kG > 0.1) g += dtY * 0.05;
                P.Forest[c] = (float)DMath.Clamp01(f);
                P.Grass[c] = (float)DMath.Clamp01(g);
                P.ClassifyBiome(c, w.Now);
            }
        }
    }
}
