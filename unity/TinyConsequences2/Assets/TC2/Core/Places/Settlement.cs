using TC2.Core.Foundation;

namespace TC2.Core.Places
{
    public enum CropStage : byte { Fallow = 0, Sown = 1, Growing = 2, Ripe = 3, Harvested = 4 }

    /// <summary>
    /// A settlement (08_CIVILIZATION.md §1). Foundation version: an anchor and local tangent frame, a few
    /// place nodes in local metres (square, fields, woodlot, well), stores, and one field system with a
    /// seasonal crop. Layout graphs, buildings and parcels come in F3.
    /// </summary>
    public struct Settlement
    {
        public int Id;
        public int Cell;
        public Vec3d Anchor;            // unit vector
        public Vec3d East, North;       // local frame (unit vectors tangent at the anchor)
        public long Founded;
        public long Abandoned;          // 0 = inhabited
        public long FoundedEvent;

        public int Population;          // residents alive whose home is here (cached, maintained)
        public double Granary;          // FU
        public double Woodstore;        // wood units
        public double FieldHa;
        public CropStage Crop;
        public double CropGrowth;       // 0..1 growth this season
        public double CropWork;         // person-hours of sowing/tending this season
        public double Sown;             // 0..1 share of the fields sown this year
        public double Harvested;        // 0..1 share of the ripe crop brought in
        public double CropWorkNeed;     // person-hours wanted this season
        public double CropBurnt;        // 0..1 share of the standing crop lost to fire this season
        public long CropCause;          // event behind crop damage (fire), 0 none
        public double LastHarvest;
        public double ExpectedHarvest;
        public long HungerEvent;        // current hunger episode event (0 none)
        public int FieldCell;           // planet cell holding most of the fields
        public int Lots;                // house lots laid out so far (the next lot's index)
        public double Ration;           // share of full meals the granary allows until the next harvest
        public double ArableHa;         // arable hectares in the cells its fields claim
        public int FieldCells;          // cells claimed for fields
        public long FamineAt;           // when the last famine struck (0 never): remembered by those who lived it (D2-042)
        public long FamineEvent;        // that famine's history event
        public long RationEvent;        // the current rationing episode's event (0 none)
        public long WildEvent;          // the current episode of living off the wild (0 none)
        public double Memory;           // share of today's adults who lived through the last famine (derived daily, kept for the view)
        public Grounds Hunt, Fish, Gath; // the day's survey of its grounds (D2-042)
        public byte Works;              // what its people have built: 1 a well, 2 a granary (D2-045)
        public float WorkBuild;         // labour-hours into the work being built
        public KnownSites Lore;         // places its people have seen (D2-045)
        public long ScoutAt;            // when someone last went to look at the land beyond

        public float SquareX, SquareY, FieldX, FieldY, WoodX, WoodY, WellX, WellY;

        public bool Inhabited => Abandoned == 0;

        /// <summary>Unit vector of a local point (metres east, north) by the gnomonic projection.</summary>
        public Vec3d LocalToSphere(double x, double y, double radius)
        {
            return (Anchor * radius + East * x + North * y).Normalized;
        }

        public void SphereToLocal(Vec3d p, double radius, out double x, out double y)
        {
            double d = Vec3d.Dot(p, Anchor);
            if (d < 1e-6) d = 1e-6;
            Vec3d q = p * (radius / d); // onto the tangent plane at distance radius
            Vec3d rel = q - Anchor * radius;
            x = Vec3d.Dot(rel, East);
            y = Vec3d.Dot(rel, North);
        }

        /// <summary>Local position of house lot k: a golden-angle spiral around the square.</summary>
        public void LotPosition(int k, out float x, out float y)
        {
            double ang = k * 2.39996323, rad = 15.0 + 9.5 * DMath.Sqrt(k + 1);
            x = (float)(SquareX + DMath.Cos(ang) * rad);
            y = (float)(SquareY + DMath.Sin(ang) * rad);
        }

        public static void Frame(Vec3d anchor, out Vec3d east, out Vec3d north)
        {
            Vec3d up = Vec3d.UnitY;
            Vec3d e = Vec3d.Cross(up, anchor);
            if (e.LengthSq < 1e-10) e = Vec3d.Cross(Vec3d.UnitX, anchor);
            east = e.Normalized;
            north = Vec3d.Cross(anchor, east).Normalized;
        }

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Int(Cell); WriteV(w, Anchor); WriteV(w, East); WriteV(w, North); w.Long(Founded); w.Long(Abandoned); w.Long(FoundedEvent);
            w.Int(Population); w.Double(Granary); w.Double(Woodstore); w.Double(FieldHa); w.Byte((byte)Crop); w.Double(CropGrowth);
            w.Double(CropWork); w.Double(Sown); w.Double(Harvested); w.Double(CropWorkNeed); w.Double(CropBurnt); w.Long(CropCause); w.Double(LastHarvest); w.Double(ExpectedHarvest);
            w.Long(HungerEvent); w.Int(FieldCell); w.Int(Lots); w.Double(Ration); w.Double(ArableHa); w.Int(FieldCells);
            w.Long(FamineAt); w.Long(FamineEvent); w.Long(RationEvent); w.Long(WildEvent); w.Double(Memory);
            Hunt.Write(w); Fish.Write(w); Gath.Write(w);
            w.Byte(Works); w.Float(WorkBuild); Lore.Write(w); w.Long(ScoutAt);
            w.Float(SquareX); w.Float(SquareY); w.Float(FieldX); w.Float(FieldY); w.Float(WoodX); w.Float(WoodY); w.Float(WellX); w.Float(WellY);
        }

        public static Settlement Read(BinReader r)
        {
            Settlement s = default;
            s.Id = r.Int(); s.Cell = r.Int(); s.Anchor = ReadV(r); s.East = ReadV(r); s.North = ReadV(r); s.Founded = r.Long(); s.Abandoned = r.Long(); s.FoundedEvent = r.Long();
            s.Population = r.Int(); s.Granary = r.Double(); s.Woodstore = r.Double(); s.FieldHa = r.Double(); s.Crop = (CropStage)r.Byte(); s.CropGrowth = r.Double();
            s.CropWork = r.Double(); s.Sown = r.Double(); s.Harvested = r.Double(); s.CropWorkNeed = r.Double(); s.CropBurnt = r.Double(); s.CropCause = r.Long(); s.LastHarvest = r.Double(); s.ExpectedHarvest = r.Double();
            s.HungerEvent = r.Long(); s.FieldCell = r.Int(); s.Lots = r.Int(); s.Ration = r.Double(); s.ArableHa = r.Double(); s.FieldCells = r.Int();
            s.FamineAt = r.Long(); s.FamineEvent = r.Long(); s.RationEvent = r.Long(); s.WildEvent = r.Long(); s.Memory = r.Double();
            s.Hunt = Grounds.Read(r); s.Fish = Grounds.Read(r); s.Gath = Grounds.Read(r);
            s.Works = r.Byte(); s.WorkBuild = r.Float(); s.Lore = KnownSites.Read(r); s.ScoutAt = r.Long();
            s.SquareX = r.Float(); s.SquareY = r.Float(); s.FieldX = r.Float(); s.FieldY = r.Float(); s.WoodX = r.Float(); s.WoodY = r.Float(); s.WellX = r.Float(); s.WellY = r.Float();
            return s;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(Cell); HashV(ref h, Anchor); HashV(ref h, East); HashV(ref h, North); h.Add(Founded); h.Add(Abandoned); h.Add(FoundedEvent);
            h.Add(Population); h.Add(Granary); h.Add(Woodstore); h.Add(FieldHa); h.Add((byte)Crop); h.Add(CropGrowth);
            h.Add(CropWork); h.Add(Sown); h.Add(Harvested); h.Add(CropWorkNeed); h.Add(CropBurnt); h.Add(CropCause); h.Add(LastHarvest); h.Add(ExpectedHarvest);
            h.Add(HungerEvent); h.Add(FieldCell); h.Add(Lots); h.Add(Ration); h.Add(ArableHa); h.Add(FieldCells);
            h.Add(FamineAt); h.Add(FamineEvent); h.Add(RationEvent); h.Add(WildEvent); h.Add(Memory);
            Hunt.Hash(ref h); Fish.Hash(ref h); Gath.Hash(ref h);
            h.Add(Works); h.Add(WorkBuild); Lore.Hash(ref h); h.Add(ScoutAt);
            h.Add(SquareX); h.Add(SquareY); h.Add(FieldX); h.Add(FieldY); h.Add(WoodX); h.Add(WoodY); h.Add(WellX); h.Add(WellY);
        }

        internal static void WriteV(BinWriter w, Vec3d v) { w.Double(v.X); w.Double(v.Y); w.Double(v.Z); }
        internal static Vec3d ReadV(BinReader r) => new Vec3d(r.Double(), r.Double(), r.Double());
        internal static void HashV(ref StateHasher h, Vec3d v) { h.Add(v.X); h.Add(v.Y); h.Add(v.Z); }
    }

    /// <summary>A household: the unit of residence, food budget and inheritance (07 §2).</summary>
    public struct Household
    {
        public int Id;
        public int Settlement;
        public float HomeX, HomeY;   // local metres in the settlement frame
        public double Larder;        // FU
        public long Formed;
        public long Dissolved;       // 0 = exists
        public int FamilyName;       // registry key for the family name (household id of origin)
        public byte Shelter;         // 0 a camp, 1 a hut, 2 a house (D2-045: raised by its people's labour and the village's wood)
        public float Build;          // labour-hours into the next stage of its shelter
        public int Party;            // the party it travels with to a new home (0 none; D2-045)

        public void Write(BinWriter w) { w.Int(Id); w.Int(Settlement); w.Float(HomeX); w.Float(HomeY); w.Double(Larder); w.Long(Formed); w.Long(Dissolved); w.Int(FamilyName); w.Byte(Shelter); w.Float(Build); w.Int(Party); }
        public static Household Read(BinReader r) => new Household { Id = r.Int(), Settlement = r.Int(), HomeX = r.Float(), HomeY = r.Float(), Larder = r.Double(), Formed = r.Long(), Dissolved = r.Long(), FamilyName = r.Int(), Shelter = r.Byte(), Build = r.Float(), Party = r.Int() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Settlement); h.Add(HomeX); h.Add(HomeY); h.Add(Larder); h.Add(Formed); h.Add(Dissolved); h.Add(FamilyName); h.Add(Shelter); h.Add(Build); h.Add(Party); }
    }
}
