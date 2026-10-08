using TC2.Core.Foundation;

namespace TC2.Core.History
{
    /// <summary>Kinds of recorded history. Append only: values are saved.</summary>
    public enum HistoryKind : ushort
    {
        None = 0,
        WorldCreated = 1,
        PeopleArrived = 2,
        SettlementFounded = 3,
        SettlementAbandoned = 4,
        PersonBorn = 5,
        PersonDied = 6,
        Marriage = 7,
        LightningStrike = 8,
        FireStarted = 9,
        Wildfire = 10,          // a fire process that grew past a size (aggregate)
        FireOut = 11,
        FieldsBurned = 12,
        BuildingBurned = 13,
        JourneyInterrupted = 14,
        JourneyAbandoned = 15,
        Harvest = 16,
        HarvestFailed = 17,
        Hunger = 18,
        Famine = 19,
        HouseholdFormed = 20,
        DangerNoticed = 21,
        GodCommand = 22,
        FledDanger = 23,
        Drought = 24,
        CoastChanged = 25,      // the sea rose or fell past a coast (Magnitude = metres, ArgA drowned, ArgB emerged*1000+villages)
        IceAge = 26,
        IceAgeEnded = 27,
        Eruption = 28,          // a volcano erupted (Magnitude = VEI, ArgA = vent id)
        VentBorn = 29,          // a volcano opened (ArgA = vent id, ArgB = kind: 0 arc, 1 hotspot, 2 rift)
        VentExtinct = 30,       // a volcano fell silent (ArgB = eruptions in its life)
        AshFall = 31,           // ash fell on a village's fields (Magnitude = share of the crop lost)
        IslandRose = 32,        // a volcano in the sea built itself into an island
        Quake = 33,             // the ground shook (Magnitude = M, ArgA = fault patches ruptured)
        Landslides = 34,        // shaken slopes slid (ArgA = slopes)
        StoresSpoiled = 35,     // a quake spilled a village's stores (Magnitude = share lost)
        Tsunami = 36,           // a wave from a quake under the sea struck the coast (Magnitude = run-up m, ArgA = cells flooded)
        FieldsFlooded = 37,     // the sea came over a village's fields (Magnitude = share of the crop lost)
        Storm = 38,             // a great storm gathered (Magnitude = wind m/s, ArgA = kind: 0 cyclone, 1 winter storm)
        StormLandfall = 39,     // a storm came ashore (Magnitude = wind m/s)
        CropsFlattened = 40,    // a storm's wind flattened a village's crop (Magnitude = share lost)
        StormSurge = 41,        // a storm drove the sea over a coast (Magnitude = surge m, ArgA = cells flooded)
        Speciation = 42,        // a population cut off from its kind became a species (Subject new, Object parent; Magnitude = coat C)
        Extinction = 43,        // a species died out (Subject)
        Rationing = 44,         // a village began to ration its grain (Magnitude = ration, Cause1 the failed harvest) (D2-042)
        WildFood = 45,          // a village turned to the woods and the shore for food (InResponseTo the rationing or the hunger)
        HomeBuilt = 46,         // a household raised its shelter (ArgA = 1 a hut, 2 a house) (D2-045)
        WorkBuilt = 47,         // a village finished a structure (Object the building, Other who led it; ArgA its kind (Structures), ArgB granaries after; C1.1)
        PartySetOut = 48,       // households left a village to make a new home (Subject the origin, ArgA households, ArgB people)
        Scouted = 49,           // someone of a village saw good land (Cell the site)
        HouseholdMoved = 50,    // a household left its village for another (Subject the household, Object the village it went to)
        HerdHuntedOut = 51,     // people hunted a herd near a village to nothing (Subject the herd, Object its kind; D2-047)
        TechniqueFound = 54,    // someone found a technique by practice where nobody knew it (Subject the person, Object the village, ArgA the technique; D2-062)
        TechniqueKnown = 55,    // a village first has a competent practitioner (Object the best of them, ArgA the technique)
        TechniqueLost = 56,     // a village's last competent practitioner is gone (ArgA the technique)
        PathogenAppeared = 57,  // a new sickness came over from animals (Subject the pathogen, Object the village, ArgA 1 its animals; D2-066)
        EpidemicArrived = 58,   // a sickness came to a village (Object the pathogen, Other its carrier; Cause1 the epidemic it came from)
        EpidemicEnded = 59,     // it left (Magnitude the dead, ArgA its days)
        ReliefSent = 60,        // a village sent grain to a neighbour in famine (Subject the giver, Object the receiver, Magnitude FU; D2-067)
        Taken = 61,             // someone took a load from a village's store in the night, unseen (Subject the taker, Object the village, Other theirs; Magnitude FU; D2-069)
        TakingSeen = 62,        // a taker was seen at a village's store and ran, leaving it (the same fields)
        Struck = 63,            // a blow in the night at a store (Subject the striker, Object the struck; Magnitude hurt; ArgA 1 the struck was taking; D2-071)
        WatchSet = 64,          // a village began to keep a watch at its store at night (Magnitude the year's losses, FU)
        AmendsMade = 65,        // a village made amends for blood its people shed (Subject the payers, Object the wronged, Other who asked; Magnitude FU; ArgA the deed kind; D2-072)
        ClaimRefused = 66,      // a village would not make amends (ArgA 1 they denied it; ArgB the deed kind)
        BandRaised = 67,        // someone raised a band to fall on a village (Subject the leader, Object the target; ArgA its size, ArgB 1 a wrong, 2 a tributary's wrong its lord answers (Other the tributary; D2-108), 0 hunger; D2-073)
        Raid = 68,              // a band fell on a village's store (Subject the raiders' village, Object the raided, Other the leader; Magnitude loot; ArgA size, ArgB outcome x 100 + blows)
        WallPlanned = 69,       // a village began a palisade (Magnitude its losses a year, FU; Cause1 the latest; D2-074)
        WallBuilt = 70,         // a village finished its palisade (ArgA its length, m)
        WallFallen = 71,        // a palisade rotted and fell
        TributeAgreed = 72,     // a village bowed to its raiders and agreed to pay them tribute (Subject the payer, Object the lord; Magnitude the share; ArgA the lordship's size after; D2-075)
        TributePaid = 73,       // a tributary paid its lord at harvest (Magnitude FU)
        TributeEnded = 74,      // a tributary paid no more (ArgA 1 the lord is gone, 0 grown too weak)
        HomesDrowned = 75,      // the sea rose over homes' lots (Settlement; ArgA homes lost; Cause1 the coast's change; D2-087)
        WoodsReceded = 77,      // a village's woods were cut back past a mark (Magnitude the woodlot's distance, m; ArgA 1 600 m, 2 1 km, 3 1.5 km, 4 none within reach; C1.2)
        StoneFound = 78,        // someone of a village saw rock fit for building (Cell the rock, Other who; Magnitude m from the village; C1.3)
        ArmsMade = 79,          // a village's smiths began to forge arms (Subject the smith, Object the village, Other the smithy; ArgA 1 from ore, 0 its tools; Cause1 what it fears; C1.4)
        OreFound = 81,          // someone of a village saw copper ore showing (Cell the body, Other who; ArgA the kind; Magnitude m from the village; C5.1)
        OreGaveOut = 82,
        OreTaken = 83,
        BoatBuilt = 85,         // a household made a boat (Subject the household, Object the boat, Other who led the making; C9.1)
        HomeRebuilt = 88,       // a household rebuilt its house as its people build now (Subject the household, Object the house; ArgA the plan built, ArgB the one before; D2-114)
        ReliefReturned = 87,    // a village gave back grain it was given in a famine (Subject who gave it back, Object the giver; Magnitude FU first returned; ArgA 1 by sea; Cause1 the gift; D2-113)
        BoatLost = 86,          // a boat overturned at sea and was lost (Subject the household, Object the boat, Other who was aboard; Magnitude the wind; ArgA 1 they drowned)
        CallAnswered = 84,      // people of a tributary went with their lord's band (Subject the tributary, Object the lord, Other the target; ArgA how many; C10.1)          // people of one village dug ore at a body another works (Subject the diggers' village, Object the owners, Other the digger; ArgA the kind; Magnitude kg; C5.4)        // the last of an ore body was dug (Subject the village that dug it out, Object its mine; Magnitude kg dug from it in all; C5.1)
        WayWorn = 80,           // the way between two villages is worn into a path (ArgA 2) or beaten into a road (ArgA 3) by their people's walking (Subject, Object the villages; Magnitude the wear; C4.1)
        WorkBegun = 76,         // a village laid out a structure and began it (Object the building; ArgA its kind, ArgB granaries standing; Cause1 the need; C1.1)
        HomeTaken = 53,         // a household made its home in one that stood empty (Object: who lived there last; ArgA 1 a hut, 2 a house; D2-059)
        ColdHomes = 52,         // a village's homes were cold enough to harm (Magnitude homes, ArgA 1 the woodstore ran short, ArgB the day's mean C; D2-056)
    }

    /// <summary>History tiers (11 §4).</summary>
    public enum Tier : byte
    {
        Ephemeral = 0,   // never stored
        Personal = 1,
        Local = 2,
        Historical = 3,
        Epochal = 4,
    }

    /// <summary>How a cause relates to its effect.</summary>
    public enum CauseRel : byte
    {
        None = 0,
        CausedBy = 1,
        EnabledBy = 2,
        InResponseTo = 3,
        PartOf = 4,
    }

    /// <summary>Where a chain of causes ends.</summary>
    public enum RootKind : byte
    {
        Unknown = 0,
        Nature = 1,
        People = 2,
        You = 3,
    }

    [System.Flags]
    public enum EventFlags : byte
    {
        None = 0,
        Aggregate = 1,
        Pinned = 2,
        Notable = 4,
    }

    /// <summary>One recorded event. Plain data; ~100 bytes.</summary>
    public struct HistoryEvent
    {
        public long Id;
        public long Time;
        public HistoryKind Kind;
        public Tier Tier;
        public EventFlags Flags;
        public RootKind RootKind;
        public int Cell;
        public int Settlement;
        public EntityRef Subject, Object, Other;
        public long Cause1, Cause2;
        public CauseRel Rel1, Rel2;
        public long Root;
        public float Magnitude;
        public int ArgA, ArgB;
        public float Importance;

        public void Write(BinWriter w)
        {
            w.Long(Id); w.Long(Time); w.UShort((ushort)Kind); w.Byte((byte)Tier); w.Byte((byte)Flags); w.Byte((byte)RootKind);
            w.Int(Cell); w.Int(Settlement); w.Long(Subject.Packed); w.Long(Object.Packed); w.Long(Other.Packed);
            w.Long(Cause1); w.Long(Cause2); w.Byte((byte)Rel1); w.Byte((byte)Rel2); w.Long(Root);
            w.Float(Magnitude); w.Int(ArgA); w.Int(ArgB); w.Float(Importance);
        }

        public static HistoryEvent Read(BinReader r)
        {
            HistoryEvent e;
            e.Id = r.Long(); e.Time = r.Long(); e.Kind = (HistoryKind)r.UShort(); e.Tier = (Tier)r.Byte(); e.Flags = (EventFlags)r.Byte(); e.RootKind = (RootKind)r.Byte();
            e.Cell = r.Int(); e.Settlement = r.Int(); e.Subject = EntityRef.FromPacked(r.Long()); e.Object = EntityRef.FromPacked(r.Long()); e.Other = EntityRef.FromPacked(r.Long());
            e.Cause1 = r.Long(); e.Cause2 = r.Long(); e.Rel1 = (CauseRel)r.Byte(); e.Rel2 = (CauseRel)r.Byte(); e.Root = r.Long();
            e.Magnitude = r.Float(); e.ArgA = r.Int(); e.ArgB = r.Int(); e.Importance = r.Float();
            return e;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(Time); h.Add((int)Kind); h.Add((byte)Tier); h.Add((byte)Flags); h.Add((byte)RootKind);
            h.Add(Cell); h.Add(Settlement); h.Add(Subject.Packed); h.Add(Object.Packed); h.Add(Other.Packed);
            h.Add(Cause1); h.Add(Cause2); h.Add((byte)Rel1); h.Add((byte)Rel2); h.Add(Root);
            h.Add(Magnitude); h.Add(ArgA); h.Add(ArgB); h.Add(Importance);
        }
    }

    /// <summary>Static description of each kind: tier, base importance, how a root of this kind is classified.</summary>
    public static class HistoryKinds
    {
        public static Tier DefaultTier(HistoryKind k)
        {
            switch (k)
            {
                case HistoryKind.WorldCreated:
                case HistoryKind.PeopleArrived:
                case HistoryKind.IceAge:
                case HistoryKind.IceAgeEnded: return Tier.Epochal;
                case HistoryKind.CoastChanged: return Tier.Local;
                case HistoryKind.HomesDrowned: return Tier.Local;
                case HistoryKind.Eruption: return Tier.Historical;
                case HistoryKind.VentBorn: return Tier.Historical;
                case HistoryKind.IslandRose: return Tier.Historical;
                case HistoryKind.Quake: return Tier.Local;
                case HistoryKind.Landslides: return Tier.Local;
                case HistoryKind.StoresSpoiled: return Tier.Local;
                case HistoryKind.Tsunami: return Tier.Historical;
                case HistoryKind.FieldsFlooded: return Tier.Local;
                case HistoryKind.Storm: return Tier.Local;
                case HistoryKind.StormLandfall: return Tier.Local;
                case HistoryKind.CropsFlattened: return Tier.Local;
                case HistoryKind.StormSurge: return Tier.Historical;
                case HistoryKind.Speciation: return Tier.Epochal;
                case HistoryKind.Extinction: return Tier.Epochal;
                case HistoryKind.VentExtinct: return Tier.Local;
                case HistoryKind.AshFall: return Tier.Local;
                case HistoryKind.SettlementFounded:
                case HistoryKind.SettlementAbandoned:
                case HistoryKind.Wildfire:
                case HistoryKind.Famine:
                case HistoryKind.GodCommand:
                case HistoryKind.Drought: return Tier.Historical;
                case HistoryKind.LightningStrike:
                case HistoryKind.FireStarted:
                case HistoryKind.FireOut:
                case HistoryKind.FieldsBurned:
                case HistoryKind.BuildingBurned:
                case HistoryKind.Harvest:
                case HistoryKind.HarvestFailed:
                case HistoryKind.Hunger:
                case HistoryKind.Rationing:
                case HistoryKind.WildFood:
                case HistoryKind.WorkBuilt:
                case HistoryKind.WorkBegun:
                case HistoryKind.WoodsReceded:
                case HistoryKind.StoneFound:
                case HistoryKind.ArmsMade:
                case HistoryKind.WayWorn:
                case HistoryKind.OreFound:
                case HistoryKind.OreGaveOut:
                case HistoryKind.OreTaken:
                case HistoryKind.CallAnswered:
                case HistoryKind.BoatBuilt:
                case HistoryKind.BoatLost:
                case HistoryKind.Scouted: return Tier.Local;
                case HistoryKind.PartySetOut: return Tier.Historical;
                case HistoryKind.HerdHuntedOut: return Tier.Local;
                case HistoryKind.ColdHomes: return Tier.Local;
                case HistoryKind.TechniqueFound: return Tier.Historical;
                case HistoryKind.TechniqueKnown: return Tier.Local;
                case HistoryKind.TechniqueLost: return Tier.Historical;
                case HistoryKind.PathogenAppeared: return Tier.Historical;
                case HistoryKind.EpidemicArrived: return Tier.Historical;
                case HistoryKind.EpidemicEnded: return Tier.Local;
                case HistoryKind.ReliefSent: return Tier.Historical;
                case HistoryKind.ReliefReturned: return Tier.Local;
                case HistoryKind.HomeRebuilt: return Tier.Local;
                case HistoryKind.Taken: return Tier.Local;
                case HistoryKind.TakingSeen: return Tier.Local;
                case HistoryKind.Struck: return Tier.Local;
                case HistoryKind.WatchSet: return Tier.Local;
                case HistoryKind.AmendsMade: return Tier.Local;
                case HistoryKind.ClaimRefused: return Tier.Local;
                case HistoryKind.BandRaised: return Tier.Local;
                case HistoryKind.Raid: return Tier.Historical;
                case HistoryKind.WallPlanned: return Tier.Local;
                case HistoryKind.WallBuilt: return Tier.Historical;
                case HistoryKind.WallFallen: return Tier.Historical;
                case HistoryKind.TributeAgreed: return Tier.Historical;
                case HistoryKind.TributePaid: return Tier.Local;
                case HistoryKind.TributeEnded: return Tier.Historical;
                default: return Tier.Personal;
            }
        }

        /// <summary>Computed in double, stored as float (D2-003: float arithmetic may differ between runtimes).</summary>
        public static float BaseImportance(HistoryKind k, double magnitude)
        {
            double b;
            switch (k)
            {
                case HistoryKind.WorldCreated: b = 100; break;
                case HistoryKind.PeopleArrived: b = 60; break;
                case HistoryKind.SettlementFounded: b = 30; break;
                case HistoryKind.SettlementAbandoned: b = 40; break;
                case HistoryKind.Famine: b = 40; break;
                case HistoryKind.IceAge: b = 50; break;
                case HistoryKind.IceAgeEnded: b = 40; break;
                case HistoryKind.CoastChanged: b = 4; break;
                case HistoryKind.HomesDrowned: b = 4; break;
                case HistoryKind.Eruption: b = 0.6 * DMath.Exp(0.8 * magnitude); break; // VEI 2 ~5, VEI 4 ~26, VEI 6 ~140
                case HistoryKind.VentBorn: b = 3; break;
                case HistoryKind.IslandRose: b = 8; break;
                case HistoryKind.Quake: b = 2.0 * DMath.Exp(1.2 * (magnitude - 5.5)); break; // M6 ~4, M7 ~12, M8 ~40
                case HistoryKind.Landslides: b = 3; break;
                case HistoryKind.StoresSpoiled: b = 4; break;
                case HistoryKind.Tsunami: b = 30; break;
                case HistoryKind.FieldsFlooded: b = 5; break;
                case HistoryKind.Storm: b = 0.5 * DMath.Exp(0.06 * magnitude); break; // 33 m/s ~3.6, 60 m/s ~18
                case HistoryKind.StormLandfall: b = 0.6 * DMath.Exp(0.06 * magnitude); break;
                case HistoryKind.CropsFlattened: b = 5; break;
                case HistoryKind.StormSurge: b = 20; break;
                case HistoryKind.Speciation: b = 45; break;
                case HistoryKind.Extinction: b = 40; break;
                case HistoryKind.AshFall: b = 5; break;
                case HistoryKind.Wildfire: b = 15; break;
                case HistoryKind.GodCommand: b = 10; break;
                case HistoryKind.PersonDied: b = 2; break;
                case HistoryKind.PersonBorn: b = 1; break;
                case HistoryKind.Marriage: b = 1.5; break;
                case HistoryKind.HouseholdFormed: b = 1; break;
                case HistoryKind.LightningStrike: b = 1; break;
                case HistoryKind.FireStarted: b = 2; break;
                case HistoryKind.JourneyInterrupted: b = 1; break;
                case HistoryKind.Harvest: b = 0.5; break;
                case HistoryKind.HarvestFailed: b = 6; break;
                case HistoryKind.Hunger: b = 4; break;
                case HistoryKind.Rationing: b = 3; break;
                case HistoryKind.WildFood: b = 3; break;
                case HistoryKind.HomeBuilt: b = 1; break;
                case HistoryKind.WorkBuilt: b = 3; break;
                case HistoryKind.WorkBegun: b = 2; break;
                case HistoryKind.WoodsReceded: b = 4; break;
                case HistoryKind.StoneFound: b = 5; break;
                case HistoryKind.ArmsMade: b = 8; break;
                case HistoryKind.WayWorn: b = 4; break;
                case HistoryKind.OreFound: b = 6; break;
                case HistoryKind.OreGaveOut: b = 7; break;
                case HistoryKind.OreTaken: b = 7; break;
                case HistoryKind.CallAnswered: b = 8; break;
                case HistoryKind.BoatBuilt: b = 3; break;
                case HistoryKind.BoatLost: b = 6; break;
                case HistoryKind.PartySetOut: b = 20; break;
                case HistoryKind.Scouted: b = 2; break;
                case HistoryKind.HouseholdMoved: b = 1.5; break;
                case HistoryKind.HerdHuntedOut: b = 3; break;
                case HistoryKind.ColdHomes: b = 4; break;
                case HistoryKind.TechniqueFound: b = 25; break;
                case HistoryKind.TechniqueKnown: b = 6; break;
                case HistoryKind.TechniqueLost: b = 15; break;
                case HistoryKind.PathogenAppeared: b = 30; break;
                case HistoryKind.EpidemicArrived: b = 12; break;
                case HistoryKind.EpidemicEnded: b = 5; break;
                case HistoryKind.ReliefSent: b = 10; break;
                case HistoryKind.ReliefReturned: b = 4; break;
                case HistoryKind.HomeRebuilt: b = 2; break;
                case HistoryKind.Taken: b = 6; break;
                case HistoryKind.TakingSeen: b = 8; break;
                case HistoryKind.Struck: b = 10; break;
                case HistoryKind.WatchSet: b = 6; break;
                case HistoryKind.AmendsMade: b = 8; break;
                case HistoryKind.ClaimRefused: b = 9; break;
                case HistoryKind.BandRaised: b = 8; break;
                case HistoryKind.Raid: b = 14; break;
                case HistoryKind.WallPlanned: b = 8; break;
                case HistoryKind.WallBuilt: b = 16; break;
                case HistoryKind.WallFallen: b = 12; break;
                case HistoryKind.TributeAgreed: b = 18; break;
                case HistoryKind.TributePaid: b = 4; break;
                case HistoryKind.TributeEnded: b = 16; break;
                default: b = 1; break;
            }
            // magnitude raises importance sub-linearly - where it is a size; a person's events carry an age or a count there,
            // and a death at sixty scored 6.1 (over the keep-for-ever line): every adult's death stayed in the ledger
            switch (k)
            {
                case HistoryKind.PersonDied: case HistoryKind.PersonBorn: case HistoryKind.Marriage: case HistoryKind.HouseholdFormed:
                case HistoryKind.HouseholdMoved: case HistoryKind.HomeBuilt: case HistoryKind.HomeTaken:
                    return (float)b;
            }
            double m = magnitude < 0 ? 0 : magnitude;
            return (float)(b * (1.0 + DMath.Log(1.0 + m) * 0.5));
        }
    }
}
