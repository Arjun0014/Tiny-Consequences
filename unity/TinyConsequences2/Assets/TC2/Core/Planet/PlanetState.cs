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
        public float[] RiverFlow;  // discharge smoothed over ~a month (m3/day): what makes a river a river
        public float[] LakeWater;  // water held by a depression, kept at its deepest cell (m3)
        public float[] LakeDepth;  // derived from LakeWater and the drainage: water depth over each cell (m)
        public double SeaLevel;      // the coastline's level: every land/sea mask reads it; moves in steps of >= 1 m
        public double SeaLevelNow;   // the climate's sea level (continuous); the coast follows it when it is 1 m away
        public double Tilt = 23.0 * DMath.Deg2Rad;   // obliquity at the last climate step (radians)
        public double GlobalTempOffset;
        // the slow climate (D2-024)
        public double[] ZonalT;     // zonal annual-mean sea-level temperature by band in sin(latitude) (degC)
        public float[] Ice;         // land ice thickness (m)
        public double CO2 = 280;    // ppm
        public double Aerosol;      // stratospheric veil (optical depth)
        public double Ecc, Perihelion;              // the orbit at the last climate step
        public double SeaLevel0, SeaLevelRef, TGlobalRef, OceanAreaRef, IceVolume, IceRef;
        public byte Glacial;        // 1 during an age of ice (hysteresis on the ice over land), for the history
        // derived from the climate state (never serialized)
        public float[] TMean, TAmp; // annual mean and seasonal swing per cell (degC)
        public double TGlobal, MoistFactor = 1.0;
        public int CoastVersion;    // bumps when the coastline moves (views rebuild)
        // plates in time (D2-025)
        public PlateInfo[] Plates = new PlateInfo[0];
        public byte[] Crust;        // 1 continental, 0 oceanic
        public float[] CrustAge;    // years since the crust formed at a ridge (oceanic)
        public float[] CrustOx, CrustOy; // the crust sample's offset inside its cell (m, east/north)
        public int ReliefVersion;   // bumps when the relief moves (views rebuild)
        public float[] DrainIce;    // the ice the drainage was last built with (its surface = rock + this ice)
        // volcanoes (D2-028)
        public float[] Magma;       // magma gathered under each cell, not yet taken by a vent
        public float[] Stress;      // slip deficit on fault patches (m; quakes, D2-030); 0 off the faults
        public float[] StormRain;   // storm rain waiting for the next weather day (rain units; D2-032)
        public float[] Gathered;    // kg of mast, roots and berries people took from a cell since the animals last ate (D2-042)
        public System.Collections.Generic.List<Storm> Storms = new System.Collections.Generic.List<Storm>();
        // wildlife (D2-035): the species and, per species and cell, how many animals and in what condition
        public System.Collections.Generic.List<Species> Species = new System.Collections.Generic.List<Species>();
        public float[] Abund = new float[0], Cond = new float[0];
        public float[] Coat = new float[0];   // per species and cell: how far the coat has moved from its kind's (C; D2-037)
        public float[] Drift = new float[0];  // per species and cell: a neutral lineage marker, apart only where populations are (D2-038)
        public System.Collections.Generic.List<Vent> Vents = new System.Collections.Generic.List<Vent>();
        public Hotspot[] Hotspots = new Hotspot[0];
        public double SupplyRef;    // the volcanoes' magma supply when the world began (the carbon cycle's reference)
        public double FreeLandRef;  // ice-free land (ha) when the world began: rock under ice does not weather (D2-036)

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
            Biome = new byte[Cells]; DryCause = new long[Cells]; FieldOwner = new int[Cells]; RiverFlow = new float[Cells];
            LakeWater = new float[Cells]; LakeDepth = new float[Cells];
            ZonalT = new double[Climate.Bands]; Ice = new float[Cells]; TMean = new float[Cells]; TAmp = new float[Cells];
            Crust = new byte[Cells]; CrustAge = new float[Cells]; CrustOx = new float[Cells]; CrustOy = new float[Cells];
            DrainIce = new float[Cells]; Magma = new float[Cells]; Stress = new float[Cells]; StormRain = new float[Cells];
            Gathered = new float[Cells];
        }

        public bool IsLand(int c) => Elev[c] > SeaLevel;

        /// <summary>The surface water runs over: rock plus the ice the drainage was built with.</summary>
        public double DrainSurface(int c) => (double)Elev[c] + DrainIce[c];

        /// <summary>Open water stands on the cell (a lake deeper than 0.3 m).</summary>
        public bool IsLake(int c) => LakeDepth[c] > 0.3f;

        /// <summary>A brook runs through the cell (smoothed discharge above ~12 l/s): fresh water to drink.</summary>
        public bool IsStream(int c) => RiverFlow[c] >= 1000f && LakeDepth[c] <= 0.3f;

        /// <summary>A river (above ~70 l/s): too wide to step over.</summary>
        public bool IsRiver(int c) => RiverFlow[c] >= 6000f && LakeDepth[c] <= 0.3f;

        /// <summary>Fresh water on the cell or next to it (a stream or a lake).</summary>
        public bool FreshWaterNear(int c)
        {
            if (IsStream(c) || IsLake(c)) return true;
            for (int k = 0; k < 8; k++) { int n = Grid.Nbr8[c * 8 + k]; if (IsLand(n) && (IsStream(n) || IsLake(n))) return true; }
            return false;
        }

        /// <summary>Cell area in hectares.</summary>
        public double AreaHa(int c) => Grid.Area[c] * Radius * Radius / 10000.0;

        /// <summary>
        /// The ground's own scale (D2-085): the planet's geophysics - plates, uplift and wear, coasts, ice, the rivers' courses,
        /// quakes, storms, eruptions - was tuned on the cells of a 10 km world and keeps that behaviour per cell at any size, so
        /// a seed makes the same ground on a smaller planet. Lengths inside those laws are geo-metres (a 10 km sphere's); heights
        /// stay metres. What lives on the ground - people, beasts, plants by the hectare, walks and reaches - uses the real size.
        /// </summary>
        public const double GeoRadius = 10000.0;

        /// <summary>Real metres per geo-metre (1 on the 10 km world).</summary>
        public double GeoScale => Radius / GeoRadius;

        /// <summary>Cell area in geo-hectares (the planet's laws).</summary>
        public double GeoAreaHa(int c) => Grid.Area[c] * GeoRadius * GeoRadius / 10000.0;

        /// <summary>Cell spacing in geo-metres (the planet's laws).</summary>
        public double GeoCellSize => Grid.MeanSpacing * GeoRadius;

        /// <summary>
        /// A cell's real area against the 10 km world's (1 there; 0.25 at 5 km): what living things hold or need of a cell or a ground
        /// of cells - a hunter's half-sated prey, a pack's range - was tuned per cell there and is per hectare, so it scales by this.
        /// </summary>
        public double AreaScale => GeoScale * GeoScale;

        /// <summary>
        /// Rings of cells round a cell that make a patch the size of the 10 km world's cell and its eight neighbours (~680 m across):
        /// a herd's ground, a hunting ground (D2-085). 1 there, 2 at 5-7 km, 3 at 4 km.
        /// </summary>
        public int PatchRings => System.Math.Max(1, (int)DMath.Floor(1.0 / GeoScale + 0.5));

        // the ground between cell centres (Shore, D2-088): derived, never saved or hashed
        public float[] ShoreDrawn; public int ShoreRelief = -1, ShoreCoast = -1, ShoreBasins = -1; public double ShoreSea = double.NaN;
        // the depressions a lake can fill (the drainage's basins: 1 in one), rebuilt with the drainage - the ground's own shape, which the lakes'
        // day-to-day levels never change - and its count of rebuilds; and the days' water routed (lake levels move every day). Derived, never
        // saved or hashed: what is cached from them is rebuilt from the same state by a loaded world (D2-119)
        public byte[] InBasin; public int BasinVersion, WaterDay;

        System.Collections.Generic.Dictionary<int, int[][]> _patches;   // (derived, never serialized: per ring count, per cell, built when asked)

        /// <summary>
        /// The cells within <paramref name="rings"/> steps of c (0: <see cref="PatchRings"/>): c first, then ring by ring in the order
        /// they are met (one ring: c and its eight neighbours in Nbr8 order, as the 10 km world's grounds always read them).
        /// </summary>
        public int[] Patch(int c, int rings = 0)
        {
            if (rings <= 0) rings = PatchRings;
            if (_patches == null) _patches = new System.Collections.Generic.Dictionary<int, int[][]>();
            if (!_patches.TryGetValue(rings, out var byCell)) { byCell = new int[Cells][]; _patches[rings] = byCell; }
            var p = byCell[c];
            if (p != null) return p;
            if (rings == 1)
            {
                // (exactly c and its Nbr8 - a cube corner's repeated neighbour counted twice, as it always was)
                p = new int[9]; p[0] = c;
                for (int k = 0; k < 8; k++) p[1 + k] = Grid.Nbr8[c * 8 + k];
                byCell[c] = p;
                return p;
            }
            var list = new System.Collections.Generic.List<int> { c };
            var seen = new System.Collections.Generic.HashSet<int> { c };
            int from = 0;
            for (int r = 0; r < rings; r++)
            {
                int to = list.Count;
                for (int i = from; i < to; i++)
                    for (int k = 0; k < 8; k++) { int nb = Grid.Nbr8[list[i] * 8 + k]; if (seen.Add(nb)) list.Add(nb); }
                from = to;
            }
            p = list.ToArray();
            byCell[c] = p;
            return p;
        }

        /// <summary>
        /// How well a cell takes the plough (0..1): warm enough, wet enough but not a marsh, not steep, low.
        /// Standing forest must be cleared first: it lowers what a season's work can open.
        /// </summary>
        public double Arability(int c, double meanTemp) => ArabilityOpen(c, meanTemp) * (1.0 - 0.6 * Forest[c]);

        /// <summary>How arable a cell's land is once its woods are cleared (Arability without the woods' share: C1.2).</summary>
        public double ArabilityOpen(int c, double meanTemp)
        {
            if (!IsLand(c)) return 0;
            double t = DMath.SmoothStep(3, 10, meanTemp) * (1.0 - DMath.SmoothStep(27, 34, meanTemp));
            double m = Moist[c];
            double wet = DMath.SmoothStep(0.15, 0.35, m) * (1.0 - 0.5 * DMath.SmoothStep(0.85, 1.0, m));
            double steep = 0;
            for (int k = 0; k < 4; k++) steep = DMath.Max(steep, DMath.Abs((double)Elev[Grid.Nbr4[c * 4 + k]] - Elev[c]));
            double flat = 1.0 - DMath.SmoothStep(60, 260, steep);
            double low = 1.0 - DMath.SmoothStep(900, 1600, Elev[c] - SeaLevel);
            return t * wet * flat * low;
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
        /// Temperature at a cell and time: the climate's annual mean + seasonal swing (opposite in the south, lagged
        /// ~6 days) + diurnal swing by local solar time (the same shape the climate tick uses).
        /// </summary>
        public double TemperatureAt(int c, long t)
        {
            Vec3d p = Grid.Center[c];
            bool land = Elev[c] > SeaLevel;
            double f = SimTime.YearFraction(t);
            double season = DMath.Sin(DMath.TwoPi * (f - 0.25)); // peak at f=0.5 (late summer, lagged)
            if (SinLat[c] < 0) season = -season;
            double local = LocalSolarHour(p, t);
            double diurnal = (land ? 5.0 : 1.0) * DMath.Cos(DMath.TwoPi * (local - 15.0) / 24.0);
            return TMean[c] + TAmp[c] * season + diurnal;
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
            double tmean = TMean[c];
            double m = Moist[c];
            double h = Elev[c] - SeaLevel;
            Biome b;
            if (Ice[c] > 5f || tmean < -6) b = Planet.Biome.Ice;
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
            w.FloatArray(RiverFlow, Cells);
            w.FloatArray(LakeWater, Cells);
            w.Int(ZonalT.Length); for (int j = 0; j < ZonalT.Length; j++) w.Double(ZonalT[j]);
            w.FloatArray(Ice, Cells);
            w.Double(CO2); w.Double(Aerosol); w.Double(Ecc); w.Double(Perihelion);
            w.Double(SeaLevel0); w.Double(SeaLevelRef); w.Double(TGlobalRef); w.Double(OceanAreaRef); w.Double(IceVolume);
            w.Byte(Glacial);
            w.Double(SeaLevelNow); w.Double(IceRef);
            w.Int(Plates.Length);
            for (int i = 0; i < Plates.Length; i++) { var pl = Plates[i]; w.Double(pl.Pole.X); w.Double(pl.Pole.Y); w.Double(pl.Pole.Z); w.Double(pl.Omega); w.Bool(pl.Continental); }
            w.ByteArray(Crust, Cells); w.FloatArray(CrustAge, Cells); w.FloatArray(CrustOx, Cells); w.FloatArray(CrustOy, Cells);
            w.FloatArray(DrainIce, Cells);
            w.FloatArray(Magma, Cells);
            w.FloatArray(Stress, Cells);
            w.FloatArray(StormRain, Cells);
            w.Int(Storms.Count);
            for (int i = 0; i < Storms.Count; i++) Storms[i].Write(w);
            w.Int(Species.Count);
            for (int i = 0; i < Species.Count; i++) Species[i].Write(w);
            w.Int(Abund.Length);
            w.FloatArray(Abund, Abund.Length);
            w.FloatArray(Cond, Cond.Length);
            w.FloatArray(Coat, Coat.Length);
            w.FloatArray(Drift, Drift.Length);
            w.Int(Hotspots.Length);
            for (int i = 0; i < Hotspots.Length; i++) { w.Double(Hotspots[i].At.X); w.Double(Hotspots[i].At.Y); w.Double(Hotspots[i].At.Z); w.Double(Hotspots[i].Rate); }
            w.Int(Vents.Count);
            for (int i = 0; i < Vents.Count; i++)
            {
                var v = Vents[i];
                w.Int(v.Id); w.Int(v.Cell); w.Double(v.At.X); w.Double(v.At.Y); w.Double(v.At.Z); w.Int(v.Plate); w.Byte(v.Kind); w.Byte(v.Flags); w.Double(v.Pressure); w.Long(v.Born); w.Long(v.LastEruption); w.Long(v.Next); w.Int(v.Eruptions); w.Float(v.Supply); w.Float(v.Seal); w.Float(v.Cone);
            }
            w.Double(SupplyRef);
            w.Double(FreeLandRef);
            w.FloatArray(Gathered, Cells);
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
            r.FloatArray(p.RiverFlow);
            r.FloatArray(p.LakeWater);
            int nb = r.Int(); for (int j = 0; j < nb; j++) p.ZonalT[j] = r.Double();
            r.FloatArray(p.Ice);
            p.CO2 = r.Double(); p.Aerosol = r.Double(); p.Ecc = r.Double(); p.Perihelion = r.Double();
            p.SeaLevel0 = r.Double(); p.SeaLevelRef = r.Double(); p.TGlobalRef = r.Double(); p.OceanAreaRef = r.Double(); p.IceVolume = r.Double();
            p.Glacial = r.Byte();
            p.SeaLevelNow = r.Double(); p.IceRef = r.Double();
            int np = r.Int();
            p.Plates = new PlateInfo[np];
            for (int i = 0; i < np; i++) { double x = r.Double(), y = r.Double(), z = r.Double(); p.Plates[i] = new PlateInfo { Pole = new Vec3d(x, y, z), Omega = r.Double(), Continental = r.Bool() }; }
            r.ByteArray(p.Crust); r.FloatArray(p.CrustAge); r.FloatArray(p.CrustOx); r.FloatArray(p.CrustOy);
            r.FloatArray(p.DrainIce);
            r.FloatArray(p.Magma);
            r.FloatArray(p.Stress);
            r.FloatArray(p.StormRain);
            int ns = r.Int();
            for (int i = 0; i < ns; i++) p.Storms.Add(Storm.Read(r));
            int nsp = r.Int();
            for (int i = 0; i < nsp; i++) p.Species.Add(Planet.Species.Read(r));
            int na = r.Int();
            p.Abund = new float[na]; p.Cond = new float[na]; p.Coat = new float[na]; p.Drift = new float[na];
            r.FloatArray(p.Abund);
            r.FloatArray(p.Cond);
            r.FloatArray(p.Coat);
            r.FloatArray(p.Drift);
            int nh = r.Int();
            p.Hotspots = new Hotspot[nh];
            for (int i = 0; i < nh; i++) { double x = r.Double(), y = r.Double(), z = r.Double(); p.Hotspots[i] = new Hotspot { At = new Vec3d(x, y, z), Rate = r.Double() }; }
            int nv = r.Int();
            for (int i = 0; i < nv; i++)
                p.Vents.Add(new Vent { Id = r.Int(), Cell = r.Int(), At = new Vec3d(r.Double(), r.Double(), r.Double()), Plate = r.Int(), Kind = r.Byte(), Flags = r.Byte(), Pressure = r.Double(), Born = r.Long(), LastEruption = r.Long(), Next = r.Long(), Eruptions = r.Int(), Supply = r.Float(), Seal = r.Float(), Cone = r.Float() });
            p.SupplyRef = r.Double();
            p.FreeLandRef = r.Double();
            r.FloatArray(p.Gathered);
            return p;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Radius); h.Add(Grid.N); h.Add(SeaLevel); h.Add(Tilt); h.Add(GlobalTempOffset);
            for (int j = 0; j < ZonalT.Length; j++) h.Add(ZonalT[j]);
            h.Add(CO2); h.Add(Aerosol); h.Add(Ecc); h.Add(Perihelion); h.Add(SeaLevel0); h.Add(SeaLevelRef); h.Add(TGlobalRef); h.Add(OceanAreaRef); h.Add(IceVolume); h.Add(Glacial); h.Add(SeaLevelNow); h.Add(IceRef);
            for (int i = 0; i < Cells; i++) h.Add(Ice[i]);
            for (int i = 0; i < Plates.Length; i++) { h.Add(Plates[i].Pole.X); h.Add(Plates[i].Pole.Y); h.Add(Plates[i].Pole.Z); h.Add(Plates[i].Omega); h.Add(Plates[i].Continental ? 1 : 0); }
            for (int i = 0; i < Cells; i++) { h.Add(Crust[i]); h.Add(CrustAge[i]); h.Add(CrustOx[i]); h.Add(CrustOy[i]); h.Add(DrainIce[i]); h.Add(Magma[i]); h.Add(Stress[i]); h.Add(StormRain[i]); }
            for (int i = 0; i < Storms.Count; i++) Storms[i].Hash(ref h);
            for (int i = 0; i < Species.Count; i++) Species[i].Hash(ref h);
            for (int i = 0; i < Abund.Length; i++) { h.Add(Abund[i]); h.Add(Cond[i]); h.Add(Coat[i]); h.Add(Drift[i]); }
            for (int i = 0; i < Hotspots.Length; i++) { h.Add(Hotspots[i].At.X); h.Add(Hotspots[i].At.Y); h.Add(Hotspots[i].At.Z); h.Add(Hotspots[i].Rate); }
            for (int i = 0; i < Vents.Count; i++) { var v = Vents[i]; h.Add(v.Id); h.Add(v.Cell); h.Add(v.At.X); h.Add(v.At.Y); h.Add(v.At.Z); h.Add(v.Plate); h.Add(v.Kind); h.Add(v.Flags); h.Add(v.Pressure); h.Add(v.Born); h.Add(v.LastEruption); h.Add(v.Next); h.Add(v.Eruptions); h.Add(v.Supply); h.Add(v.Seal); h.Add(v.Cone); }
            h.Add(SupplyRef);
            h.Add(FreeLandRef);
            for (int i = 0; i < Cells; i++)
            {
                h.Add(Elev[i]); h.Add(Plate[i]); h.Add(Moist[i]); h.Add(Temp[i]); h.Add(SoilWater[i]);
                h.Add(RainToday[i]); h.Add(Grass[i]); h.Add(Forest[i]); h.Add(Biome[i]); h.Add(DryCause[i]); h.Add(FieldOwner[i]); h.Add(RiverFlow[i]); h.Add(LakeWater[i]);
                h.Add(Gathered[i]);
            }
        }
    }
}
