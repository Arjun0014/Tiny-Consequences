using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// The slow climate (05 §3b, D2-024): a zonal energy balance in x = sin(latitude) (Budyko-Sellers-North),
    /// <c>C dT/dt = Q s(x; obliquity) (1 - albedo) - (A - G(CO2) + B T) - F(aerosol) + d/dx[D (1 - x^2) dT/dx]</c>,
    /// stepped implicitly each season; albedo from the cells (sea, sea ice, snow, land ice, forest, desert); land ice
    /// sheets per cell from snowfall, melt by positive degree-days, and flow down their surface; sea level from the
    /// ice volume and warmth. The orbit is a fictional fast closed form (obliquity ~1,300 y, precession ~600 y,
    /// eccentricity ~3,100 y). Writes the per-cell annual mean and seasonal swing the weather and life read.
    /// </summary>
    public sealed class Climate
    {
        public const int Bands = 48;
        public const double Insolation = 1361.0 / 4.0;   // W/m2, the sun's mean over the sphere
        public const double OlrA = 209.0, OlrB = 2.09;    // outgoing longwave A + B T (W/m2, T in degC)
        public const double Diffusion = 0.55;             // meridional heat transport (W/m2/K)
        // ice: a fast world law (sheets answer the orbit within centuries, not tens of millennia)
        public const double IceAccumulation = 2.6;        // x precipitation x the cold part of the year (m ice/yr)
        public const double IceMelt = 3.4;                // m ice per degC of mean positive temperature a year
        // ice sheets on a small world are thin (a plastic sheet ~3.4 sqrt(half-width) m: 150-250 m for these 5-30 km sheets):
        // they spread and drain nine times faster than D2-024's (D2-043), so deep glacials lower the sea by tens of metres, not 100
        public const double IceFlowMax = 810000;          // m2/yr: shallow-ice diffusivity cap
        public const double IceFlowPerM = 540.0;          // m2/yr of diffusivity per m of ice
        const double IceSubstep = 0.1;                    // years at most (stability: dt K / h^2 < 0.25, by the thickest ice)
        public const double CLand = 0.25, COcean = 10.0;  // heat capacity (W yr m-2 K-1): ~0.1 y on land, ~5 y at sea
        public const double StepYears = 0.25;             // one step a season
        public const double Outgassing = 0.5;             // ppm/yr at rest (balanced by weathering at 280 ppm: ~1,100-year memory)
        public const double Lapse = 0.0065;               // K per m of height above the sea
        const double IceAlbedo = 0.62, SeaIceAlbedo = 0.50, SeaAlbedo = 0.07;

        readonly int _cells;
        readonly int[] _bandOf;
        readonly double[] _cellX, _cellHa;
        readonly double[] _x = new double[Bands], _kHalf = new double[Bands + 1];
        readonly double[] _albSum = new double[Bands], _landHa = new double[Bands], _bandHa = new double[Bands];
        readonly double[] _a = new double[Bands], _b = new double[Bands], _c = new double[Bands], _d = new double[Bands];
        readonly float[] _dIce;
        readonly int[] _iced;   // cells with flowing ice this step (scratch)
        int _icedN;
        public double Obliquity, Eccentricity, Perihelion;   // derived from time (radians)

        public Climate(PlanetState P)
        {
            _cells = P.Cells;
            _bandOf = new int[_cells]; _cellX = new double[_cells]; _cellHa = new double[_cells]; _dIce = new float[_cells]; _iced = new int[_cells];
            double dx = 2.0 / Bands;
            for (int j = 0; j < Bands; j++) _x[j] = -1.0 + (j + 0.5) * dx;
            for (int j = 0; j <= Bands; j++) { double xh = -1.0 + j * dx; _kHalf[j] = Diffusion * (1.0 - xh * xh) / (dx * dx); }
            for (int c = 0; c < _cells; c++)
            {
                double x = P.SinLat[c];
                _cellX[c] = x;
                int j = (int)DMath.Floor((x + 1.0) / dx);
                _bandOf[c] = j < 0 ? 0 : (j >= Bands ? Bands - 1 : j);
                _cellHa[c] = P.AreaHa(c);
                _bandHa[_bandOf[c]] += _cellHa[c];
            }
        }

        static double P2(double x) => 1.5 * x * x - 0.5;

        /// <summary>The orbit at a time: obliquity, eccentricity and the longitude of perihelion (fictional, fast).</summary>
        public static void Orbit(ulong seed, long t, out double obliquity, out double ecc, out double perihelion)
        {
            double y = t / (double)SimTime.Year;
            double p1 = KeyedRng.Unit(seed, RngStream.Orbit, 1) * DMath.TwoPi;
            double p2 = KeyedRng.Unit(seed, RngStream.Orbit, 2) * DMath.TwoPi;
            double p3 = KeyedRng.Unit(seed, RngStream.Orbit, 3) * DMath.TwoPi;
            double p4 = KeyedRng.Unit(seed, RngStream.Orbit, 4) * DMath.TwoPi;
            double deg = 23.0 + 4.0 * DMath.Sin(DMath.TwoPi * y / 1300.0 + p1) + 1.0 * DMath.Sin(DMath.TwoPi * y / 870.0 + p4);
            obliquity = deg * DMath.PI / 180.0;
            ecc = DMath.Clamp(0.025 + 0.018 * DMath.Sin(DMath.TwoPi * y / 3100.0 + p2) + 0.007 * DMath.Sin(DMath.TwoPi * y / 1900.0 + p4), 0.0, 0.06);
            perihelion = DMath.TwoPi * y / 600.0 + p3;
        }

        /// <summary>Annual-mean insolation shape s(x) for an obliquity (North 1975: 1 + s2 P2(x)).</summary>
        public static double InsolationShape(double x, double obliquity)
        {
            double co = DMath.Cos(obliquity);
            double s2 = -0.625 * P2(co);
            return 1.0 + s2 * P2(x);
        }

        /// <summary>How much stronger a hemisphere's summer sun is than its mean (precession x eccentricity).</summary>
        public static double SummerFactor(PlanetState P, bool north) => 1.0 + 3.0 * P.Ecc * DMath.Sin(P.Perihelion + (north ? 0.0 : DMath.PI));

        /// <summary>Seasonal swing of temperature (half the summer-winter range, degC) at a cell.</summary>
        public double SeasonalAmp(PlanetState P, int c)
        {
            double x = _cellX[c];
            double o = DMath.Sin(P.Tilt) / 0.390731; // sin 23 deg
            return 13.0 * DMath.Abs(x) * (P.IsLand(c) ? 1.0 : 0.4) * o * SummerFactor(P, x >= 0);
        }

        /// <summary>Zonal temperature at x (linear between band centres).</summary>
        public static double ZonalAt(double[] zt, double x)
        {
            double u = (x + 1.0) * 0.5 * Bands - 0.5;
            if (u <= 0) return zt[0];
            if (u >= Bands - 1) return zt[Bands - 1];
            int j = (int)DMath.Floor(u);
            double f = u - j;
            return zt[j] * (1 - f) + zt[j + 1] * f;
        }

        /// <summary>Surface albedo of a cell given its annual mean temperature.</summary>
        double SurfaceAlbedo(PlanetState P, int c, double tMean)
        {
            if (!P.IsLand(c))
            {
                double ice = DMath.SmoothStep(-1.0, -9.0, tMean);
                return SeaAlbedo + (SeaIceAlbedo - SeaAlbedo) * ice;
            }
            if (P.Ice[c] > 2f) return IceAlbedo;
            double snow = DMath.SmoothStep(3.0, -10.0, tMean);
            double veg = 0.13 * P.Forest[c] + 0.20 * P.Grass[c] * (1.0 - P.Forest[c]);
            double bare = DMath.Max(0.0, 1.0 - P.Forest[c] - P.Grass[c] * (1.0 - P.Forest[c]));
            double ground = veg + 0.32 * bare;
            return ground + (0.55 - ground) * snow;
        }

        /// <summary>One season of the energy balance (dt in years). Updates ZonalT and the derived per-cell climate.</summary>
        public void Step(PlanetState P, ulong seed, long t, double dt, bool ice = true, bool carbon = true)
        {
            Orbit(seed, t, out double obl, out double ecc, out double peri);
            P.Tilt = obl; P.Ecc = ecc; P.Perihelion = peri;
            var zt = P.ZonalT;
            for (int j = 0; j < Bands; j++) { _albSum[j] = 0; _landHa[j] = 0; }
            for (int c = 0; c < _cells; c++)
            {
                int j = _bandOf[c];
                _albSum[j] += SurfaceAlbedo(P, c, P.TMean[c]) * _cellHa[c];
                if (P.IsLand(c)) _landHa[j] += _cellHa[c];
            }
            double g = 5.35 * DMath.Log(DMath.Max(1.0, P.CO2) / 280.0);
            double aer = 25.0 * P.Aerosol;
            double boost = 1.0 / DMath.Sqrt(1.0 - P.Ecc * P.Ecc);
            for (int j = 0; j < Bands; j++)
            {
                double surf = _bandHa[j] > 0 ? _albSum[j] / _bandHa[j] : SeaAlbedo;
                double alb = 0.20 + 0.62 * surf; // clouds over everything
                double land = _bandHa[j] > 0 ? _landHa[j] / _bandHa[j] : 0;
                double cap = land * CLand + (1 - land) * COcean;
                double s = Insolation * boost * InsolationShape(_x[j], P.Tilt) * (1.0 - alb);
                double km = _kHalf[j], kp = _kHalf[j + 1];
                _a[j] = -km; _c[j] = -kp;
                _b[j] = cap / dt + OlrB + km + kp;
                _d[j] = cap / dt * zt[j] + s - (OlrA - g) - aer + OlrB * P.GlobalTempOffset;
            }
            SolveTridiagonal(zt);
            P.Aerosol *= DMath.Exp(-dt / 0.7); // a veil fades over ~2 years
            if (carbon) Carbon(P, dt);
            Derive(P);
            if (ice) Ice(P, dt);
            UpdateSeaLevel(P);
            Derive(P); // from the final state, exactly as a loaded world derives it
        }

        /// <summary>
        /// The carbon thermostat (Walker-Hays-Kasting): volcanoes outgas in proportion to the magma they draw (against
        /// the supply the world began with), rock weathers faster when the air is warm and rich - CO2 relaxes toward
        /// their balance over ~2,000 years, so a world with more volcanoes grows warmer, slowly.
        /// </summary>
        public static void Carbon(PlanetState P, double dt)
        {
            double supply = 0;
            for (int v = 0; v < P.Vents.Count; v++) supply += P.Vents[v].Supply;
            // rock weathers where it is bare to the air: ice sheets shut it away (and so let CO2 build until they melt)
            double free = FreeLand(P);
            double exposed = P.FreeLandRef > 0 ? free / P.FreeLandRef : 1.0;
            double outgas = Outgassing * (0.6 + 0.4 * supply / DMath.Max(1.0, P.SupplyRef));
            // silicate weathering speeds up ~10 % per degree (faster reactions, more runoff): a cold world lets CO2 build
            double weather = Outgassing * DMath.Sqrt(DMath.Max(1.0, P.CO2) / 280.0) * DMath.Exp(0.1 * (P.TGlobal - P.TGlobalRef)) * exposed;
            P.CO2 = DMath.Clamp(P.CO2 + (outgas - weather) * dt, 100.0, 3000.0);
        }

        /// <summary>Land not under an ice sheet (ha).</summary>
        public static double FreeLand(PlanetState P)
        {
            double a = 0;
            for (int c = 0; c < P.Cells; c++) if (P.IsLand(c) && P.Ice[c] < 2f) a += P.AreaHa(c);
            return a;
        }

        void SolveTridiagonal(double[] x)
        {
            int n = Bands;
            for (int i = 1; i < n; i++)
            {
                double m = _a[i] / _b[i - 1];
                _b[i] -= m * _c[i - 1];
                _d[i] -= m * _d[i - 1];
            }
            x[n - 1] = _d[n - 1] / _b[n - 1];
            for (int i = n - 2; i >= 0; i--) x[i] = (_d[i] - _c[i] * x[i + 1]) / _b[i];
        }

        /// <summary>Initial temperatures before the climate has run (the old closed form): worldgen's first vegetation.</summary>
        public void Prime(PlanetState P, ulong seed, long t)
        {
            Orbit(seed, t, out double obl, out double ecc, out double peri);
            P.Tilt = obl; P.Ecc = ecc; P.Perihelion = peri;
            for (int j = 0; j < Bands; j++) P.ZonalT[j] = 27.0 - 40.0 * _x[j] * _x[j];
            P.TGlobalRef = 14.0;
            Derive(P);
        }

        /// <summary>Per-cell annual mean (zonal mean less the lapse over land and ice) and the global mean.</summary>
        public void Derive(PlanetState P)
        {
            var zt = P.ZonalT;
            double sum = 0, area = 0;
            double o = DMath.Sin(P.Tilt) / 0.390731;
            double ampN = 13.0 * o * SummerFactor(P, true), ampS = 13.0 * o * SummerFactor(P, false);
            for (int c = 0; c < _cells; c++)
            {
                double x = _cellX[c];
                double tz = ZonalAt(zt, x);
                bool land = P.Elev[c] > P.SeaLevel;
                double h = (double)P.Elev[c] + P.Ice[c] - P.SeaLevel;
                double tm = tz - (land ? Lapse * h : 0.0);
                P.TMean[c] = (float)tm;
                P.TAmp[c] = (float)((x >= 0 ? ampN : ampS) * DMath.Abs(x) * (land ? 1.0 : 0.4));
                sum += tz * _cellHa[c]; area += _cellHa[c];
            }
            P.TGlobal = sum / area;
            P.MoistFactor = DMath.Clamp(DMath.Exp(0.05 * (P.TGlobal - P.TGlobalRef)), 0.45, 1.6); // a warmer world is wetter
        }

        /// <summary>Mean over the year of max(0, T) for T = mean + amp sin(phase): positive degree-days per day.</summary>
        public static double MeanPositive(double mean, double amp)
        {
            amp = DMath.Abs(amp);
            if (amp < 1e-6) return mean > 0 ? mean : 0;
            double u = mean / amp;
            if (u >= 1) return mean;
            if (u <= -1) return 0;
            return amp / DMath.PI * (DMath.Sqrt(1 - u * u) + u * (DMath.HalfPi + DMath.Asin(u)));
        }

        /// <summary>Fraction of the year below freezing.</summary>
        public static double ColdFraction(double mean, double amp)
        {
            amp = DMath.Abs(amp);
            if (amp < 1e-6) return mean < 0 ? 1 : 0;
            double u = mean / amp;
            if (u >= 1) return 0;
            if (u <= -1) return 1;
            return 0.5 - DMath.Asin(u) / DMath.PI;
        }

        /// <summary>
        /// Land ice: snow that outlasts the summer piles up (precipitation from the moisture index, fast world law),
        /// positive degree-days melt it, and it flows down its surface (calving where it reaches the sea).
        /// </summary>
        void Ice(PlanetState P, double dt)
        {
            // mass balance: snow that outlasts the summer piles up, positive degree-days melt it
            _icedN = 0;
            for (int c = 0; c < _cells; c++)
            {
                if (!P.IsLand(c)) { P.Ice[c] = 0f; continue; }
                double tm = P.TMean[c], amp = P.TAmp[c];
                double h = P.Ice[c];
                if (h <= 0 && tm - DMath.Abs(amp) > 0) continue; // never freezes: nothing to keep
                double precip = 1.4 * DMath.Clamp01(P.Moist[c] * P.MoistFactor) + 0.1;  // m water a year
                double acc = precip * ColdFraction(tm, amp) * IceAccumulation;           // m ice a year
                double melt = IceMelt * MeanPositive(tm, amp);
                if (h <= 0 && acc <= melt) continue;
                h += (acc - melt) * dt;
                P.Ice[c] = (float)(h > 0 ? h : 0);
                if (P.Ice[c] > 1f) _iced[_icedN++] = c;
            }
            // sub-steps short enough for the fastest flow there is (explicit diffusion: dt k / L^2 < 0.24)
            double hmax = 0;
            for (int i = 0; i < _icedN; i++) if (P.Ice[_iced[i]] > hmax) hmax = P.Ice[_iced[i]];
            double kmax = DMath.Min(IceFlowPerM * hmax, IceFlowMax);
            double stable = kmax > 0 ? 0.24 * P.CellSize * P.CellSize / kmax : IceSubstep;
            int sub = 1 + (int)DMath.Floor(dt / DMath.Min(IceSubstep, stable) - 1e-9);
            double h0 = dt / sub;
            for (int k = 0; k < sub; k++) Flow(P, h0);
            double vol = 0;
            for (int c = 0; c < _cells; c++) if (P.Ice[c] > 0f) vol += P.Ice[c] * _cellHa[c] * 10000.0;
            P.IceVolume = vol;
        }

        /// <summary>Shallow ice: each iced cell sheds ice down its surface slope to its four neighbours (calving at the sea).</summary>
        void Flow(PlanetState P, double dt)
        {
            var g = P.Grid;
            double cell = P.CellSize, inv = 1.0 / (cell * cell);
            for (int i = 0; i < _icedN; i++)
            {
                int c = _iced[i];
                _dIce[c] = 0;
                for (int q = 0; q < 4; q++) _dIce[g.Nbr4[c * 4 + q]] = 0;
            }
            for (int i = 0; i < _icedN; i++)
            {
                int c = _iced[i];
                double h = P.Ice[c];
                if (h <= 1.0) continue;
                double s = P.Elev[c] + h;
                double k = DMath.Min(IceFlowPerM * h, IceFlowMax);
                for (int q = 0; q < 4; q++)
                {
                    int n = g.Nbr4[c * 4 + q];
                    bool land = P.IsLand(n);
                    double sn = land ? (double)P.Elev[n] + P.Ice[n] : P.SeaLevel;
                    if (sn >= s) continue;
                    double move = DMath.Min(k * (s - sn) * inv * dt, 0.2 * h);
                    _dIce[c] = (float)(_dIce[c] - move);
                    if (land) _dIce[n] = (float)(_dIce[n] + move); // into the sea: it calves and melts
                }
            }
            int n0 = _icedN;
            for (int i = 0; i < n0; i++)
            {
                int c = _iced[i];
                Apply(P, c);
                for (int q = 0; q < 4; q++)
                {
                    int n = g.Nbr4[c * 4 + q];
                    if (_dIce[n] == 0f) continue;
                    bool was = P.Ice[n] > 1f;
                    Apply(P, n);
                    if (!was && P.Ice[n] > 1f && _icedN < _iced.Length) _iced[_icedN++] = n; // the sheet spreads
                }
            }
        }

        void Apply(PlanetState P, int c)
        {
            float d = _dIce[c];
            if (d == 0f) return;
            _dIce[c] = 0f;
            double h = (double)P.Ice[c] + d;   // (in double: float + float rounds differently under Mono)
            P.Ice[c] = (float)(h > 0 ? h : 0);
        }

        /// <summary>Sea level from land ice (as water) spread over the reference ocean, and warm water's expansion.</summary>
        public void UpdateSeaLevel(PlanetState P)
        {
            if (P.OceanAreaRef <= 0) return;
            double fromIce = 0.9 * P.IceVolume / P.OceanAreaRef;
            P.SeaLevelNow = P.SeaLevelRef - fromIce + 0.5 * (P.TGlobal - P.TGlobalRef);
        }

        /// <summary>
        /// Worldgen: the climate at equilibrium for the start orbit, ice sheets grown for a few centuries, then the
        /// references set so the generated coast is where the world begins.
        /// </summary>
        public void SpinUp(PlanetState P, ulong seed, long t)
        {
            Orbit(seed, t, out double obl, out double ecc, out double peri);
            P.Tilt = obl; P.Ecc = ecc; P.Perihelion = peri;
            for (int j = 0; j < Bands; j++) P.ZonalT[j] = 27.0 - 40.0 * _x[j] * _x[j];
            P.TGlobalRef = 14.0; P.MoistFactor = 1.0;
            Derive(P);
            for (int i = 0; i < 60; i++) Step(P, seed, t, 2.0, false, false);
            P.TGlobalRef = P.TGlobal;
            for (int i = 0; i < 300; i++) Step(P, seed, t, 1.0, true, false);
            // the generated coastline is today's: the reference level carries the ice that has grown
            double sea = P.SeaLevel0;
            P.SeaLevelRef = sea + 0.9 * P.IceVolume / DMath.Max(1.0, OceanArea(P, sea));
            P.OceanAreaRef = OceanArea(P, sea);
            P.TGlobalRef = P.TGlobal;
            P.IceRef = DMath.Max(P.IceVolume, 1e6);
            P.FreeLandRef = FreeLand(P);
            UpdateSeaLevel(P);
            Derive(P);
        }

        static double OceanArea(PlanetState P, double level)
        {
            double a = 0;
            for (int c = 0; c < P.Cells; c++) if (P.Elev[c] <= level) a += P.AreaHa(c) * 10000.0;
            return a;
        }
    }
}
