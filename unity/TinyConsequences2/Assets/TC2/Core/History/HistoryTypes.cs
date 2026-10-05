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
                case HistoryKind.Hunger: return Tier.Local;
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
                default: b = 1; break;
            }
            // magnitude raises importance sub-linearly
            double m = magnitude < 0 ? 0 : magnitude;
            return (float)(b * (1.0 + DMath.Log(1.0 + m) * 0.5));
        }
    }
}
