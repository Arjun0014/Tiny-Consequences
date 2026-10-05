using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    public enum Biome : byte
    {
        DeepOcean = 0, Ocean = 1, Coast = 2, Ice = 3, Tundra = 4, Taiga = 5, Grassland = 6, Woodland = 7,
        Forest = 8, Jungle = 9, Savanna = 10, Desert = 11, Mountain = 12, Snowcap = 13, Burnt = 14, Farmland = 15,
    }

    /// <summary>
    /// Planet fields on the N64 surface grid (05_PLANET_SIMULATION.md §2). Foundation set: relief, plates,
    /// moisture index, temperature, soil water, rain, vegetation (grass, forest), biome; fire lives in
    /// <see cref="FireSystem"/>. All arithmetic in double, storage in float (D2-003).
    /// </summary>
    public sealed class PlanetState
    {
        public readonly CubeSphere Grid;
        public readonly double Radius;
        public readonly int Cells;

        public float[] Elev;       // m above the reference sphere
        public byte[] Plate;
        public float[] Moist;      // 0..1 climate moisture index
        public float[] Temp;       // degC now (climate tick)
        public float[] SoilWater;  // 0..1
        public float[] RainToday;  // 0..~3, the last weather day
        public float[] Grass;      // 0..1 biomass
        public float[] Forest;     // 0..1 biomass
        public byte[] Biome;
        public long[] DryCause;    // cause tag: event dominating a drought anomaly (0 none)
        public int[] FieldOwner;   // settlement whose fields cover this cell (0 none): land use
        public double SeaLevel;
        public double Tilt = 23.0 * DMath.Deg2Rad;
        public double GlobalTempOffset;

        // derived per-cell geometry (from the grid; never serialized)
        public readonly double[] Lat;       // radians
        public readonly double[] Lon;       // radians
        public readonly double[] SinLat;

        public PlanetState(int n, double radius)
        {
            Grid = new CubeSphere(n);
            Radius = radius;
            Cells = Grid.CellCount;
            Lat = new double[Cells]; Lon = new double[Cells]; SinLat = new double[Cells];
            for (int c = 0; c < Cells; c++) { Lat[c] = Grid.Center[c].Latitude; Lon[c] = Grid.Center[c].Longitude; SinLat[c] = DMath.Sin(Lat[c]); }
            Elev = new float[Cells]; Plate = new byte[Cells]; Moist = new float[Cells]; Temp = new float[Cells];
            SoilWater = new float[Cells]; RainToday = new float[Cells]; Grass = new float[Cells]; Forest = new float[Cells];
            Biome = new byte[Cells]; DryCause = new long[Cells]; FieldOwner = new int[Cells];
        }

        public bool IsLand(int c) => Elev[c] > SeaLevel;

        /// <summary>Cell area in hectares.</summary>
        public double AreaHa(int c) => Grid.Area[c] * Radius * Radius / 10000.0;

        /// <summary>
        /// How well a cell takes the plough (0..1): warm enough, wet enough but not a marsh, not steep, low.
        /// Standing forest must be cleared first: it lowers what a season's work can open.
        /// </summary>
        public double Arability(int c, double meanTemp)
        {
            if (!IsLand(c)) return 0;
            double t = DMath.SmoothStep(3, 10, meanTemp) * (1.0 - DMath.SmoothStep(27, 34, meanTemp));
            double m = Moist[c];
            double wet = DMath.SmoothStep(0.15, 0.35, m) * (1.0 - 0.5 * DMath.SmoothStep(0.85, 1.0, m));
            double steep = 0;
            for (int k = 0; k < 4; k++) steep = DMath.Max(steep, DMath.Abs(Elev[Grid.Nbr4[c * 4 + k]] - Elev[c]));
            double flat = 1.0 - DMath.SmoothStep(60, 260, steep);
            double low = 1.0 - DMath.SmoothStep(900, 1600, Elev[c] - SeaLevel);
            return t * wet * flat * low * (1.0 - 0.6 * Forest[c]);
        }

        /// <summary>Cell spacing in metres.</summary>
        public double CellSize => Grid.MeanSpacing * Radius;

        public double Latitude(int c) => Lat[c];
        public double Longitude(int c) => Lon[c];

        /// <summary>Solar declination for a time (equinox at day 6, northern midsummer at day 18).</summary>
        public double Declination(long t)
        {
            double f = SimTime.YearFraction(t);
            return Tilt * DMath.Sin(DMath.TwoPi * (f - 0.125));
        }

        /// <summary>Annual mean sea-level temperature by latitude (degC).</summary>
        public static double MeanTempAtLat(double lat)
        {
            double s = DMath.Sin(lat);
            return 27.0 - 40.0 * s * s;
        }

        /// <summary>
        /// Temperature at a cell and time: annual mean + seasonal swing (opposite in the south, lagged ~6 days)
        /// + diurnal swing by local solar time + elevation lapse + global offset. Deterministic closed form.
        /// </summary>
        public double TemperatureAt(int c, long t)
        {
            Vec3d p = Grid.Center[c];
            double lat = p.Latitude;
            bool land = Elev[c] > SeaLevel;
            double mean = MeanTempAtLat(lat);
            double amp = 13.0 * DMath.Abs(DMath.Sin(lat)) * (land ? 1.0 : 0.4);
            double f = SimTime.YearFraction(t);
            double season = DMath.Sin(DMath.TwoPi * (f - 0.125 - 0.125)); // peak at f=0.5 (late summer, lagged)
            if (lat < 0) season = -season;
            double local = LocalSolarHour(p, t);
            double diurnal = (land ? 5.0 : 1.0) * DMath.Cos(DMath.TwoPi * (local - 15.0) / 24.0);
            double lapse = land ? -0.0065 * (Elev[c] - SeaLevel) : 0.0;
            return mean + amp * season + diurnal + lapse + GlobalTempOffset;
        }

        /// <summary>Local solar time in hours [0, 24) at a direction (longitude 0 = world clock).</summary>
        public static double LocalSolarHour(Vec3d p, long t)
        {
            double lon = p.Longitude; // [-pi, pi]
            double h = (double)SimTime.SecondOfDay(t) / SimTime.Hour + lon / DMath.TwoPi * 24.0;
            h -= 24.0 * DMath.Floor(h / 24.0);
            return h;
        }

        /// <summary>Local solar second-of-day at a direction (for scheduling people's days).</summary>
        public static long LocalSecondOfDay(Vec3d p, long t)
        {
            double lonFrac = p.Longitude / DMath.TwoPi; // -0.5..0.5
            long offset = DMath.ToLong(DMath.Floor(lonFrac * SimTime.Day + 0.5));
            return SimTime.FloorMod(t + offset, SimTime.Day);
        }

        /// <summary>Offset to add to world time to get local solar time at p (seconds).</summary>
        public static long LocalOffset(Vec3d p)
        {
            double lonFrac = p.Longitude / DMath.TwoPi;
            return DMath.ToLong(DMath.Floor(lonFrac * SimTime.Day + 0.5));
        }

        public double Dryness(int c)
        {
            double w = SoilWater[c];
            return DMath.Clamp01(1.0 - w / 0.55);
        }

        /// <summary>Fuel available to fire: litter and grass burn readily, forest more slowly.</summary>
        public double Fuel(int c) => 0.7 * Grass[c] + Forest[c];

        public void ClassifyBiome(int c, long t)
        {
            if (!IsLand(c))
            {
                double depth = SeaLevel - Elev[c];
                Biome[c] = (byte)(depth > 600 ? Planet.Biome.DeepOcean : (depth > 120 ? Planet.Biome.Ocean : Planet.Biome.Coast));
                return;
            }
            double lat = Latitude(c);
            double tmean = MeanTempAtLat(lat) - 0.0065 * (Elev[c] - SeaLevel) + GlobalTempOffset;
            double m = Moist[c];
            double h = Elev[c] - SeaLevel;
            Biome b;
            if (tmean < -6) b = Planet.Biome.Ice;
            else if (h > 1900) b = tmean < 2 ? Planet.Biome.Snowcap : Planet.Biome.Mountain;
            else if (tmean < 0) b = Planet.Biome.Tundra;
            else if (Forest[c] > 0.55) b = tmean > 22 && m > 0.6 ? Planet.Biome.Jungle : (tmean < 6 ? Planet.Biome.Taiga : Planet.Biome.Forest);
            else if (Forest[c] > 0.25) b = tmean < 6 ? Planet.Biome.Taiga : Planet.Biome.Woodland;
            else if (m < 0.16 && tmean > 6) b = Planet.Biome.Desert;
            else if (tmean > 20 && m < 0.45) b = Planet.Biome.Savanna;
            else b = Planet.Biome.Grassland;
            Biome[c] = (byte)b;
        }

        public void Write(BinWriter w)
        {
            w.Double(Radius); w.Int(Grid.N); w.Double(SeaLevel); w.Double(Tilt); w.Double(GlobalTempOffset);
            w.FloatArray(Elev, Cells); w.ByteArray(Plate, Cells); w.FloatArray(Moist, Cells); w.FloatArray(Temp, Cells);
            w.FloatArray(SoilWater, Cells); w.FloatArray(RainToday, Cells); w.FloatArray(Grass, Cells); w.FloatArray(Forest, Cells);
            w.ByteArray(Biome, Cells);
            w.Int(Cells); for (int i = 0; i < Cells; i++) w.Long(DryCause[i]);
            w.IntArray(FieldOwner, Cells);
        }

        public static PlanetState Read(BinReader r)
        {
            double radius = r.Double(); int n = r.Int();
            var p = new PlanetState(n, radius);
            p.SeaLevel = r.Double(); p.Tilt = r.Double(); p.GlobalTempOffset = r.Double();
            r.FloatArray(p.Elev); r.ByteArray(p.Plate); r.FloatArray(p.Moist); r.FloatArray(p.Temp);
            r.FloatArray(p.SoilWater); r.FloatArray(p.RainToday); r.FloatArray(p.Grass); r.FloatArray(p.Forest);
            r.ByteArray(p.Biome);
            int nc = r.Int(); for (int i = 0; i < nc; i++) p.DryCause[i] = r.Long();
            r.IntArray(p.FieldOwner);
            return p;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Radius); h.Add(Grid.N); h.Add(SeaLevel); h.Add(Tilt); h.Add(GlobalTempOffset);
            for (int i = 0; i < Cells; i++)
            {
                h.Add(Elev[i]); h.Add(Plate[i]); h.Add(Moist[i]); h.Add(Temp[i]); h.Add(SoilWater[i]);
                h.Add(RainToday[i]); h.Add(Grass[i]); h.Add(Forest[i]); h.Add(Biome[i]); h.Add(DryCause[i]); h.Add(FieldOwner[i]);
            }
        }
    }
}
