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
        public const int Tectonics = 5;
        public const int Count = 6;
    }

    /// <summary>
    /// Foundation versions of the planet's periodic systems (05 §3, §4): closed-form temperature, daily
    /// stochastic weather with spatially correlated rain, soil water, lightning, and vegetation growth.
    /// The full energy-balance climate, hydrology and PFT vegetation replace these in F1.
    /// </summary>
    public sealed class PlanetSystems
    {
        readonly int[] _block;   // surface cell -> weather block (N8 cube-sphere cell)
        // weather spells are smooth across blocks: each cell blends the daily normal draws of its block and the
        // neighbouring blocks (kernel weights scaled to unit variance), then maps the blend back to a uniform value
        const int Taps = 9;
        readonly int[] _tapBlock;
        readonly float[] _tapWeight;
        readonly int _blocks;
        readonly double[] _blockDraw;
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
            _blocks = coarse.CellCount;
            _blockDraw = new double[_blocks];
            _tapBlock = new int[n * Taps]; _tapWeight = new float[n * Taps];
            double r = coarse.MeanSpacing * 1.15, r2 = r * r;
            for (int c = 0; c < n; c++)
            {
                Vec3d at = P.Grid.Center[c];
                int b0 = coarse.CellOf(at);
                double sw2 = 0;
                for (int k = 0; k < Taps; k++)
                {
                    int b = k == 0 ? b0 : coarse.Nbr8[b0 * 8 + k - 1];
                    Vec3d ce = coarse.Center[b];
                    double dx = at.X - ce.X, dy = at.Y - ce.Y, dz = at.Z - ce.Z;
                    double d2 = dx * dx + dy * dy + dz * dz;
                    double u = d2 < r2 ? 1.0 - d2 / r2 : 0.0;
                    double wgt = u * u;
                    _tapBlock[c * Taps + k] = b; _tapWeight[c * Taps + k] = (float)wgt;
                    sw2 += wgt * wgt;
                }
                double norm = sw2 > 0 ? 1.0 / DMath.Sqrt(sw2) : 0.0;
                for (int k = 0; k < Taps; k++) _tapWeight[c * Taps + k] = (float)(_tapWeight[c * Taps + k] * norm);
            }
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
                case SystemId.Tectonics: w.TectonicStep(); break;
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

        // 1/(1+e^(-1.702 z)) tabulated over z in [-8, 8] (32 per unit) and interpolated: basic IEEE operations only,
        // so it is the same on every runtime, and the weather's hottest loop has no exp in it (error < 2e-5)
        static readonly double[] _logistic = BuildLogistic();
        static double[] BuildLogistic()
        {
            var t = new double[16 * 32 + 2];
            for (int i = 0; i < t.Length; i++) t[i] = 1.0 / (1.0 + DMath.Exp(-1.702 * (-8.0 + i / 32.0)));
            return t;
        }
        static double Logistic(double z)
        {
            double x = (z + 8.0) * 32.0;
            if (x <= 0.0) return _logistic[0];
            if (x >= 512.0) return _logistic[512];
            int i = (int)x;
            double f = x - i;
            return _logistic[i] + (_logistic[i + 1] - _logistic[i]) * f;
        }

        void Weather(World w)
        {
            var P = w.Planet;
            long day = SimTime.FloorDiv(w.Now, SimTime.Day);
            double yf = SimTime.YearFraction(w.Now);
            double seasonN = DMath.Sin(DMath.TwoPi * (yf - 0.125));
            ulong seed = w.Seed;
            ulong dayKey = KeyedRng.Hash(seed, RngStream.Weather, 100000UL, (ulong)day);
            Storms.Spawn(w, day, yf);
            for (int b = 0; b < _blocks; b++)
                _blockDraw[b] = KeyedRng.Normal(KeyedRng.Hash(seed, RngStream.Weather, (ulong)b + 200000UL, (ulong)day), KeyedRng.Hash(seed, RngStream.Weather, (ulong)b + 300000UL, (ulong)day));
            for (int c = 0; c < P.Cells; c++)
            {
                // wetter summers at mid latitudes, wet season shifting with the sun in the tropics
                double season = seasonN * _hemi[c];
                double moist = DMath.Min(1.0, P.Moist[c] * P.MoistFactor); // a warmer world is a wetter one
                double pRain = DMath.Clamp(0.06 + 0.5 * moist + 0.08 * season * moist, 0.02, 0.85);
                double z = 0;
                int o = c * Taps;
                for (int k = 0; k < Taps; k++) z += _tapWeight[o + k] * _blockDraw[_tapBlock[o + k]];
                double ub = Logistic(z); // the normal CDF (logistic form): a uniform again
                double uc = KeyedRng.Unit(KeyedRng.Mix(dayKey ^ ((ulong)c * 0xC2B2AE3D27D4EB4FUL))); // one mix a cell: the day is in the key
                double u = 0.7 * ub + 0.3 * uc;
                double rain = u < pRain ? (pRain - u) / pRain * 1.6 : 0.0;
                if (P.StormRain[c] > 0f) { rain += P.StormRain[c]; P.StormRain[c] = 0f; }  // what the storms brought
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
            double dtY = w.Config.VegetationPeriod / (double)SimTime.Year;
            if (w.Resolution == Resolution.Coarse) { GrowVegetation(w, dtY * 3); if (!Ecology.SkipForTools) Ecology.Step(w, dtY * 3, w.Now); return; }
            GrowVegetation(w, dtY);
            // the animals live on a slower clock than the grass: every third plant step, by the world's time (D2-035)
            if (!Ecology.SkipForTools && SimTime.FloorDiv(w.Now, w.Config.VegetationPeriod) % 3 == 0) Ecology.Step(w, dtY * 3, w.Now);
        }

        /// <summary>A step of plant growth (dt in years): forests and grass toward what climate and water allow.</summary>
        public void GrowVegetation(World w, double dtY)
        {
            var P = w.Planet;
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
