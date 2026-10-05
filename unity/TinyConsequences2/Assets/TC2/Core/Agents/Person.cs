using TC2.Core.Foundation;

namespace TC2.Core.Agents
{
    /// <summary>What a person is doing (ACTIVITY layer, D2-006). Append only: saved.</summary>
    public enum ActivityKind : byte
    {
        None = 0, Idle = 1, Sleep = 2, Eat = 3, Fields = 4, Wood = 5, Social = 6, Play = 7,
        TripOut = 8, Visit = 9, TripHome = 10, Flee = 11, Wait = 12, Infant = 13, Water = 14,
    }

    /// <summary>The kind of the current step of an activity.</summary>
    public enum StepKind : byte
    {
        None = 0,
        Local = 1,   // walking between two local points of the settlement it is at
        Stay = 2,    // at a local point doing the activity
        Travel = 3,  // walking a route between settlements
    }

    /// <summary>Why the current choice won (formatted only on inspection, 07 §3).</summary>
    public enum WhyCode : byte
    {
        None = 0, Night = 1, Hungry = 2, CropNeedsWork = 3, WoodLow = 4, Leisure = 5, VisitKin = 6, VisitFriend = 7,
        GoingHome = 8, FireAhead = 9, FireNear = 10, WaitingForFire = 11, Young = 12, Harvest = 13, Tired = 14,
        WaterNeeded = 15, FireBehind = 16, NoFood = 17,
    }

    public static class Sex { public const byte Female = 0, Male = 1; }

    /// <summary>
    /// One person (07 §1). Body state is analytic between events: hunger from the time of the last meal,
    /// energy from an anchor. Positions are analytic: a local step lerps From -> To over [T0, T1]; a travel
    /// step walks route (RouteA -> RouteB) from arc length S0 to S1 over [T0, T1].
    /// </summary>
    public struct Person
    {
        public int Id;
        public byte SexKind;
        public bool Alive;
        public long Born, Died;
        public long DeathEvent;
        public int Mother, Father, Partner, Household, Home;
        public int At;                   // settlement the person is at (0 while travelling)

        public long LastMeal;
        public long Woke;                // time of the last waking (energy model)
        public float Health;             // 0..1
        public long HungryFrom;          // first time unfed beyond a day (0 none)
        public long PregDue; public int PregFather; public long NursingUntil;

        public byte Chrono, Pace, Diligence, Social, Bold, Curious;
        public float SkFarm, SkWood;

        public ActivityKind Act;
        public StepKind Step;
        public WhyCode Why;
        public int WhyArg;
        public int Dest;                 // destination settlement of a trip (0 none)
        public int RouteA, RouteB;       // route key while travelling (geometry from A to B)
        public long T0, T1;
        public float FromX, FromY, ToX, ToY;
        public float S0, S1;             // arc lengths along the route (m)
        public int ActVer;
        public uint RngCount;
        public long Alarm;               // event of the danger last noticed (0 none)
        public int AlarmProc;            // process id of that danger (dedupe)
        public int AlarmVer;             // activity version when warned (dedupe per activity)
        public int StayDur;              // seconds of the Stay step that follows a Local walk (plan)
        public int Visits;               // trips made (statistics, inspector)
        public long Until;               // end of a multi-step activity (a visit's length)
        public long AlarmT;

        public double AgeYears(long now) => (now - Born) / (double)SimTime.Year;
        public bool IsFemale => SexKind == Sex.Female;

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Byte(SexKind); w.Bool(Alive); w.Long(Born); w.Long(Died); w.Long(DeathEvent);
            w.Int(Mother); w.Int(Father); w.Int(Partner); w.Int(Household); w.Int(Home); w.Int(At);
            w.Long(LastMeal); w.Long(Woke); w.Float(Health); w.Long(HungryFrom); w.Long(PregDue); w.Int(PregFather); w.Long(NursingUntil);
            w.Byte(Chrono); w.Byte(Pace); w.Byte(Diligence); w.Byte(Social); w.Byte(Bold); w.Byte(Curious); w.Float(SkFarm); w.Float(SkWood);
            w.Byte((byte)Act); w.Byte((byte)Step); w.Byte((byte)Why); w.Int(WhyArg); w.Int(Dest); w.Int(RouteA); w.Int(RouteB);
            w.Long(T0); w.Long(T1); w.Float(FromX); w.Float(FromY); w.Float(ToX); w.Float(ToY); w.Float(S0); w.Float(S1);
            w.Int(ActVer); w.UInt(RngCount); w.Long(Alarm); w.Int(AlarmProc); w.Long(AlarmT); w.Int(AlarmVer); w.Int(StayDur); w.Int(Visits); w.Long(Until);
        }

        public static Person Read(BinReader r)
        {
            Person p = default;
            p.Id = r.Int(); p.SexKind = r.Byte(); p.Alive = r.Bool(); p.Born = r.Long(); p.Died = r.Long(); p.DeathEvent = r.Long();
            p.Mother = r.Int(); p.Father = r.Int(); p.Partner = r.Int(); p.Household = r.Int(); p.Home = r.Int(); p.At = r.Int();
            p.LastMeal = r.Long(); p.Woke = r.Long(); p.Health = r.Float(); p.HungryFrom = r.Long(); p.PregDue = r.Long(); p.PregFather = r.Int(); p.NursingUntil = r.Long();
            p.Chrono = r.Byte(); p.Pace = r.Byte(); p.Diligence = r.Byte(); p.Social = r.Byte(); p.Bold = r.Byte(); p.Curious = r.Byte(); p.SkFarm = r.Float(); p.SkWood = r.Float();
            p.Act = (ActivityKind)r.Byte(); p.Step = (StepKind)r.Byte(); p.Why = (WhyCode)r.Byte(); p.WhyArg = r.Int(); p.Dest = r.Int(); p.RouteA = r.Int(); p.RouteB = r.Int();
            p.T0 = r.Long(); p.T1 = r.Long(); p.FromX = r.Float(); p.FromY = r.Float(); p.ToX = r.Float(); p.ToY = r.Float(); p.S0 = r.Float(); p.S1 = r.Float();
            p.ActVer = r.Int(); p.RngCount = r.UInt(); p.Alarm = r.Long(); p.AlarmProc = r.Int(); p.AlarmT = r.Long(); p.AlarmVer = r.Int(); p.StayDur = r.Int(); p.Visits = r.Int(); p.Until = r.Long();
            return p;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(SexKind); h.Add(Alive); h.Add(Born); h.Add(Died); h.Add(DeathEvent);
            h.Add(Mother); h.Add(Father); h.Add(Partner); h.Add(Household); h.Add(Home); h.Add(At);
            h.Add(LastMeal); h.Add(Woke); h.Add(Health); h.Add(HungryFrom); h.Add(PregDue); h.Add(PregFather); h.Add(NursingUntil);
            h.Add(Chrono); h.Add(Pace); h.Add(Diligence); h.Add(Social); h.Add(Bold); h.Add(Curious); h.Add(SkFarm); h.Add(SkWood);
            h.Add((byte)Act); h.Add((byte)Step); h.Add((byte)Why); h.Add(WhyArg); h.Add(Dest); h.Add(RouteA); h.Add(RouteB);
            h.Add(T0); h.Add(T1); h.Add(FromX); h.Add(FromY); h.Add(ToX); h.Add(ToY); h.Add(S0); h.Add(S1);
            h.Add(ActVer); h.Add(RngCount); h.Add(Alarm); h.Add(AlarmProc); h.Add(AlarmT); h.Add(AlarmVer); h.Add(StayDur); h.Add(Visits); h.Add(Until);
        }
    }
}
