using TC2.Core.Foundation;

namespace TC2.Core.Simulation
{
    /// <summary>
    /// Generation parameters and tunable world laws (01_WORLD_LAWS.md). Serialized with the world so a
    /// loaded world keeps the laws it was made with.
    /// </summary>
    public sealed class WorldConfig
    {
        public ulong Seed = 42;
        public int SurfaceN = 64;              // cubed-sphere N of the surface grid
        public double Radius = 6000.0;         // metres (D2-085: 6 km, the miniature; the ground's laws keep geo-lengths of a 10 km sphere)
        public double LandFraction = 0.36;
        public int Plates = 9;
        public int Settlements = 6;
        public int FoundersPerSettlement = 24;

        // system periods (seconds)
        public long ClimatePeriod = 6 * SimTime.Hour;
        public long WeatherPeriod = SimTime.Day;
        public long VegetationPeriod = 4 * SimTime.Day;
        public long FireStep = 30 * SimTime.Minute;

        // laws (tunable)
        public double WalkSpeed = 1.25;           // m/s adult on open ground
        public double LightningPerCellDay = 0.0004; // strikes per land cell per stormy day
        public double FirePerceptionRange = 700;   // m: smoke and flames seen
        public double MealsPerDay = 3;
        public double FoodPerAdultDay = 1.0;       // FU
        public double FieldYieldPerHa = 130;       // FU per hectare per good season (a 48-day year: a person eats 48 FU a year)
        public double FieldHaPerPerson = 0.5;
        public double MarriageRatePerDay = 0.012;
        public double ConceptionPerDay = 0.022;
        public double TripChancePerDecision = 0.012;
        public double MarriageReach = 6000;        // m: partners are found in villages within a day's easy walk
        public double FieldReach = 950;            // m: fields lie within a short walk of the houses

        public void Write(BinWriter w)
        {
            w.ULong(Seed); w.Int(SurfaceN); w.Double(Radius); w.Double(LandFraction); w.Int(Plates); w.Int(Settlements); w.Int(FoundersPerSettlement);
            w.Long(ClimatePeriod); w.Long(WeatherPeriod); w.Long(VegetationPeriod); w.Long(FireStep);
            w.Double(WalkSpeed); w.Double(LightningPerCellDay); w.Double(FirePerceptionRange); w.Double(MealsPerDay); w.Double(FoodPerAdultDay);
            w.Double(FieldYieldPerHa); w.Double(FieldHaPerPerson); w.Double(MarriageRatePerDay); w.Double(ConceptionPerDay); w.Double(TripChancePerDecision); w.Double(MarriageReach); w.Double(FieldReach);
        }

        public static WorldConfig Read(BinReader r)
        {
            var c = new WorldConfig();
            c.Seed = r.ULong(); c.SurfaceN = r.Int(); c.Radius = r.Double(); c.LandFraction = r.Double(); c.Plates = r.Int(); c.Settlements = r.Int(); c.FoundersPerSettlement = r.Int();
            c.ClimatePeriod = r.Long(); c.WeatherPeriod = r.Long(); c.VegetationPeriod = r.Long(); c.FireStep = r.Long();
            c.WalkSpeed = r.Double(); c.LightningPerCellDay = r.Double(); c.FirePerceptionRange = r.Double(); c.MealsPerDay = r.Double(); c.FoodPerAdultDay = r.Double();
            c.FieldYieldPerHa = r.Double(); c.FieldHaPerPerson = r.Double(); c.MarriageRatePerDay = r.Double(); c.ConceptionPerDay = r.Double(); c.TripChancePerDecision = r.Double(); c.MarriageReach = r.Double(); c.FieldReach = r.Double();
            return c;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Seed); h.Add(SurfaceN); h.Add(Radius); h.Add(LandFraction); h.Add(Plates); h.Add(Settlements); h.Add(FoundersPerSettlement);
            h.Add(ClimatePeriod); h.Add(WeatherPeriod); h.Add(VegetationPeriod); h.Add(FireStep);
            h.Add(WalkSpeed); h.Add(LightningPerCellDay); h.Add(FirePerceptionRange); h.Add(MealsPerDay); h.Add(FoodPerAdultDay);
            h.Add(FieldYieldPerHa); h.Add(FieldHaPerPerson); h.Add(MarriageRatePerDay); h.Add(ConceptionPerDay); h.Add(TripChancePerDecision); h.Add(MarriageReach); h.Add(FieldReach);
        }
    }
}
