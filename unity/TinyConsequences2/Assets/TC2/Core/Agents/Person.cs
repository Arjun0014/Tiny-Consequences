using TC2.Core.Foundation;

namespace TC2.Core.Agents
{
    /// <summary>What a person is doing (ACTIVITY layer, D2-006). Append only: saved.</summary>
    public enum ActivityKind : byte
    {
        None = 0, Idle = 1, Sleep = 2, Eat = 3, Fields = 4, Wood = 5, Social = 6, Play = 7,
        TripOut = 8, Visit = 9, TripHome = 10, Flee = 11, Wait = 12, Infant = 13, Water = 14, Forage = 15,
        Hunt = 16, Fish = 17, Gather = 18, Haul = 19,   // the wild's food (D2-042): Forage is kept for old saves, Gather replaced it
        Build = 20,                                     // raising a shelter or a village's work (D2-045)
        Scout = 21, Move = 22,                          // looking over land far off; walking to a new home (D2-045)
        Tend = 23,                                      // out with the household's animals on their ground (D2-050)
        Cook = 24,                                      // grinding grain and cooking at the household's hearth (D2-056)
        Craft = 25,                                     // making and mending the household's clothes and tools (D2-058)
        Ill = 26,                                       // abed at home with a sickness (D2-066) or wounds (D2-071)
        Watch = 27,                                     // keeping the night watch at the village's store (D2-071)
        Quarry = 28,                                    // cutting stone at the village's quarry and carrying it home (C1.3)
        Smith = 29,                                     // at the village's smithy: smelting, working metal, forging arms (C1.4)
        Train = 30,                                     // practising at the village's training ground (C1.5)
        Mine = 31,                                      // digging ore at the village's mine and carrying it home (C5.1)
        Burn = 32,                                      // felling wood and burning charcoal at the village's hearth by its woodlot (C5.2a)
        Sail = 33,                                      // out in the household's boat fishing the open sea (C9.1; WhyArg the boat)
        Voyage = 34,                                    // a crew in the household's boat on a voyage to another landing and back (C9.3; WhyArg the boat)
        Weave = 35,                                     // at the village's weaver's shed: weaving and finishing its wares (C3.1)
        Pot = 36,                                       // at the village's kiln by its clay: digging, throwing and firing its pots and jars (C3.2)
    }

    /// <summary>The kind of the current step of an activity.</summary>
    public enum StepKind : byte
    {
        None = 0,
        Local = 1,   // walking From -> To over [T0, TA] then doing the activity at To until T1 (one event)
        Stay = 2,    // (kept for old saves) at a local point doing the activity
        Travel = 3,  // walking a route between settlements
    }

    /// <summary>Why the current choice won (formatted only on inspection, 07 §3).</summary>
    public enum WhyCode : byte
    {
        None = 0, Night = 1, Hungry = 2, CropNeedsWork = 3, WoodLow = 4, Leisure = 5, VisitKin = 6, VisitFriend = 7,
        GoingHome = 8, FireAhead = 9, FireNear = 10, WaitingForFire = 11, Young = 12, Harvest = 13, Tired = 14,
        WaterNeeded = 15, FireBehind = 16, NoFood = 17, GaveUpWaiting = 18,
        FoodShort = 19,   // rations are short: the woods, the shore and the sea must make up the rest (D2-042)
        Provide = 20,     // an ordinary day's hunting or fishing: meat and fish for the table
        BringHome = 21,   // carrying a catch home
        NoRoof = 22,      // the household lives in a camp or a hut: a roof to raise (D2-045)
        VillageWork = 23, // the village needs a well or a granary
        LookForLand = 24, // the village is crowded or hungry: someone goes to see the land beyond
        NewHome = 25,     // walking with the household to a new home
        Herd = 26,        // the household's animals are out on the pasture: someone watches them (D2-050)
        MealsToMake = 27, // nothing is cooked for the next meals: someone grinds and cooks (D2-056)
        GoodsWorn = 28,   // the household's clothes and tools are wearing thin: someone makes and mends (D2-058)
        Sickness = 29,    // infectious with a sickness: abed (D2-066)
        Trade = 30,       // carrying what the village spares to a neighbour that wants it, for what it wants back (D2-067)
        Take = 31,        // hungry, with nothing at home: to a neighbour's full store in the dark (D2-069)
        Hurt = 32,        // abed with wounds (D2-071)
        Watch = 33,       // the village keeps a watch at its store: grain has been taken from it
        Claim = 34,       // walking to ask amends for blood shed (D2-072)
        Raid = 35,        // with a band to fall on a village's store (D2-073)
        Stone = 36,       // the village wants stone for its houses' footings: someone cuts it at the quarry (C1.3)
        Forge = 37,       // the village has use for its smith: tools short of metal, or arms wanted (C1.4)
        Train = 38,       // the village fears its neighbours: its bold practise at its training ground (C1.5)
        Ore = 39,         // the village's smiths want ore and its mine stands: someone digs it (C5.1)
        BogOre = 41,      // the village's smiths want bog iron ore and its pits stand: someone digs it (C5.2b)
        Charcoal = 40,    // the village's smiths want charcoal and its hearth stands: someone burns it (C5.2a)
        Boat = 42,        // the household makes a boat: the sea beyond the shore promises more fish (C9.1)
        Voyage = 43,      // the village sends the household's boat to another landing: a load to trade, grain to a famine, grain given back (C9.3)
        Rebuild = 44,     // the household rebuilds its house as its people build now (D2-114)
        Weave = 45,       // the village's wares are short of what it keeps: one of its best weavers works at its weaver's shed (C3.1)
        Pot = 46,         // the village's jars or pots are short: one of its best potters works at its kiln (C3.2)
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
        public float Nutrition;          // running share of food needs met (0..1); 1 at birth
        public long HungryFrom;          // first time unfed beyond a day (0 none)
        public long PregDue; public int PregFather; public long NursingUntil;

        public byte Chrono, Pace, Diligence, Social, Bold, Curious;
        public float SkFarm, SkWood;
        public float SkHunt, SkFish;     // practice-driven, keyed by id at birth (D2-042)
        public float Reserve;            // days of full food the body holds (fat and muscle): hunger draws it down (D2-042)
        public float Ate;                // FU eaten since the last daily accounting
        public float Carry;              // FU of catch being carried home (it reaches the larder when they do)
        public int Ground;               // the cell of the current hunting, fishing or gathering ground (-1 none)

        public ActivityKind Act;
        public StepKind Step;
        public WhyCode Why;
        public int WhyArg;
        public int Dest;                 // destination settlement of a trip (0 none)
        public int RouteA, RouteB;       // route key while travelling (geometry from A to B)
        public long T0, T1;
        public long TA;                  // a local step walks From->To over [T0, TA], then stays at To until T1
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
        public float Worked;             // hours of work since waking: a day's labour weighs on the slack's choices (D2-058)
        public float KNets, KLoom, KQuern;   // proficiency in the techniques a person carries (D2-062; Knowledge.Tech)
        public float KPottery, KArd;         // (D2-063)
        public float KMetal;                 // (D2-064)
        public float KIron;                  // the bloomery (C5.2b)
        public float KBoat;                  // boats of planks (C9.2b)
        public int Sick;                     // the pathogen they carry (0 none; D2-066)
        public byte SickPhase;               // 1 latent, 2 infectious (abed)
        public long SickUntil;               // the end of this phase
        public long SickCause;               // the epidemic they caught it in (history)
        public int Imm0, Imm1, Imm2, Imm3, Imm4, Imm5;   // antigens they are immune to after recovering (D2-066; six remembered: D2-070)
        public long ImmUntil0, ImmUntil1, ImmUntil2, ImmUntil3, ImmUntil4, ImmUntil5;   // until when
        public byte Cargo;                   // a trader's load: Trade.Grain/Metal/Fibre/Hides (0 none; D2-067)
        public float CargoQty;
        public byte CargoTo;                 // where it goes at home: Trade.ToStore (the village's), ToLarder (the household's: D2-069)
        public float Hurt;                   // wounds, 0..1: abed above 0.4, off the roads above 0.25; heals by the day (D2-071)
        public int HurtDeed;                 // the wound deed behind it (its striker; a death of it is a killing)
        public int Errand;                   // a wrong to ask amends for at the doers' village (a deed id; 0 none: D2-072)
        public int Band;                     // the band they go with (0 none; D2-073)
        public float SkFight;                // fighting skill, 0..1: practice at a training ground, fights survived; fades unused (C1.5)
        public byte Fights;                  // fights they have been in (blows given or taken) and lived through
        public int Voyage;                   // the boat they are on a voyage in, from the landing at home and back (0 none; C9.3)
        public float SkCraft;                // a maker's practised hand at a workshop, 0..1: grown by hours at the weaver's shed (C3.1); their role is read from it
        public float SkPot;                  // a potter's practised hand at the kiln, 0..1: grown by hours there (C3.2); their role is read from it

        // derived from the world's seed and the person's id - never saved or hashed: filled when a person is made or loaded (Derive);
        // readers fall back to computing them where it is 0 (the same values: an exact speedup, F8)
        public byte Derived;
        public double Build;                 // the body's build (AgentSystem.ReserveCap)
        public double Sc0, Sc1, Sc2, Sc3, Sc4, Sc5, Sc6, Sc7;   // how each technique is copied, above or below the model (Knowledge.Tech.Scatter)

        public void Derive(ulong seed)
        {
            Build = AgentSystem.BuildOf(seed, Id);
            Sc0 = Knowledge.Tech.Scatter(seed, Id, 0); Sc1 = Knowledge.Tech.Scatter(seed, Id, 1); Sc2 = Knowledge.Tech.Scatter(seed, Id, 2);
            Sc3 = Knowledge.Tech.Scatter(seed, Id, 3); Sc4 = Knowledge.Tech.Scatter(seed, Id, 4); Sc5 = Knowledge.Tech.Scatter(seed, Id, 5); Sc6 = Knowledge.Tech.Scatter(seed, Id, 6); Sc7 = Knowledge.Tech.Scatter(seed, Id, 7);
            Derived = 1;
        }

        public double ScatterOf(int t) => t == 0 ? Sc0 : t == 1 ? Sc1 : t == 2 ? Sc2 : t == 3 ? Sc3 : t == 4 ? Sc4 : t == 5 ? Sc5 : t == 6 ? Sc6 : Sc7;

        /// <summary>DEEP: the mean health over the village step just lived (set by the step's bodies, read by its conceptions in the
        /// same call; never saved).</summary>
        public double StepHealth;

        public double AgeYears(long now) => (now - Born) / (double)SimTime.Year;
        public bool IsFemale => SexKind == Sex.Female;

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Byte(SexKind); w.Bool(Alive); w.Long(Born); w.Long(Died); w.Long(DeathEvent);
            w.Int(Mother); w.Int(Father); w.Int(Partner); w.Int(Household); w.Int(Home); w.Int(At);
            w.Long(LastMeal); w.Long(Woke); w.Float(Health); w.Float(Nutrition); w.Long(HungryFrom); w.Long(PregDue); w.Int(PregFather); w.Long(NursingUntil);
            w.Byte(Chrono); w.Byte(Pace); w.Byte(Diligence); w.Byte(Social); w.Byte(Bold); w.Byte(Curious); w.Float(SkFarm); w.Float(SkWood);
            w.Float(SkHunt); w.Float(SkFish); w.Float(Reserve); w.Float(Ate); w.Float(Carry); w.Int(Ground);
            w.Byte((byte)Act); w.Byte((byte)Step); w.Byte((byte)Why); w.Int(WhyArg); w.Int(Dest); w.Int(RouteA); w.Int(RouteB);
            w.Long(T0); w.Long(T1); w.Long(TA); w.Float(FromX); w.Float(FromY); w.Float(ToX); w.Float(ToY); w.Float(S0); w.Float(S1);
            w.Int(ActVer); w.UInt(RngCount); w.Long(Alarm); w.Int(AlarmProc); w.Long(AlarmT); w.Int(AlarmVer); w.Int(StayDur); w.Int(Visits); w.Long(Until); w.Float(Worked); w.Float(KNets); w.Float(KLoom); w.Float(KQuern); w.Float(KPottery); w.Float(KArd); w.Float(KMetal); w.Float(KIron); w.Float(KBoat);
            w.Int(Sick); w.Byte(SickPhase); w.Long(SickUntil); w.Long(SickCause); w.Int(Imm0); w.Int(Imm1); w.Long(ImmUntil0); w.Long(ImmUntil1); w.Byte(Cargo); w.Float(CargoQty); w.Byte(CargoTo);
            w.Int(Imm2); w.Int(Imm3); w.Int(Imm4); w.Int(Imm5); w.Long(ImmUntil2); w.Long(ImmUntil3); w.Long(ImmUntil4); w.Long(ImmUntil5);
            w.Float(Hurt); w.Int(HurtDeed); w.Int(Errand); w.Int(Band); w.Float(SkFight); w.Byte(Fights); w.Int(Voyage); w.Float(SkCraft); w.Float(SkPot);
        }

        public static Person Read(BinReader r)
        {
            Person p = default;
            p.Id = r.Int(); p.SexKind = r.Byte(); p.Alive = r.Bool(); p.Born = r.Long(); p.Died = r.Long(); p.DeathEvent = r.Long();
            p.Mother = r.Int(); p.Father = r.Int(); p.Partner = r.Int(); p.Household = r.Int(); p.Home = r.Int(); p.At = r.Int();
            p.LastMeal = r.Long(); p.Woke = r.Long(); p.Health = r.Float(); p.Nutrition = r.Float(); p.HungryFrom = r.Long(); p.PregDue = r.Long(); p.PregFather = r.Int(); p.NursingUntil = r.Long();
            p.Chrono = r.Byte(); p.Pace = r.Byte(); p.Diligence = r.Byte(); p.Social = r.Byte(); p.Bold = r.Byte(); p.Curious = r.Byte(); p.SkFarm = r.Float(); p.SkWood = r.Float();
            p.SkHunt = r.Float(); p.SkFish = r.Float(); p.Reserve = r.Float(); p.Ate = r.Float(); p.Carry = r.Float(); p.Ground = r.Int();
            p.Act = (ActivityKind)r.Byte(); p.Step = (StepKind)r.Byte(); p.Why = (WhyCode)r.Byte(); p.WhyArg = r.Int(); p.Dest = r.Int(); p.RouteA = r.Int(); p.RouteB = r.Int();
            p.T0 = r.Long(); p.T1 = r.Long(); p.TA = r.Long(); p.FromX = r.Float(); p.FromY = r.Float(); p.ToX = r.Float(); p.ToY = r.Float(); p.S0 = r.Float(); p.S1 = r.Float();
            p.ActVer = r.Int(); p.RngCount = r.UInt(); p.Alarm = r.Long(); p.AlarmProc = r.Int(); p.AlarmT = r.Long(); p.AlarmVer = r.Int(); p.StayDur = r.Int(); p.Visits = r.Int(); p.Until = r.Long(); p.Worked = r.Float(); p.KNets = r.Float(); p.KLoom = r.Float(); p.KQuern = r.Float(); p.KPottery = r.Float(); p.KArd = r.Float(); p.KMetal = r.Float(); p.KIron = r.Float(); p.KBoat = r.Float();
            p.Sick = r.Int(); p.SickPhase = r.Byte(); p.SickUntil = r.Long(); p.SickCause = r.Long(); p.Imm0 = r.Int(); p.Imm1 = r.Int(); p.ImmUntil0 = r.Long(); p.ImmUntil1 = r.Long(); p.Cargo = r.Byte(); p.CargoQty = r.Float(); p.CargoTo = r.Byte();
            p.Imm2 = r.Int(); p.Imm3 = r.Int(); p.Imm4 = r.Int(); p.Imm5 = r.Int(); p.ImmUntil2 = r.Long(); p.ImmUntil3 = r.Long(); p.ImmUntil4 = r.Long(); p.ImmUntil5 = r.Long();
            p.Hurt = r.Float(); p.HurtDeed = r.Int(); p.Errand = r.Int(); p.Band = r.Int(); p.SkFight = r.Float(); p.Fights = r.Byte(); p.Voyage = r.Int(); p.SkCraft = r.Float(); p.SkPot = r.Float();
            return p;
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(SexKind); h.Add(Alive); h.Add(Born); h.Add(Died); h.Add(DeathEvent);
            h.Add(Mother); h.Add(Father); h.Add(Partner); h.Add(Household); h.Add(Home); h.Add(At);
            h.Add(LastMeal); h.Add(Woke); h.Add(Health); h.Add(Nutrition); h.Add(HungryFrom); h.Add(PregDue); h.Add(PregFather); h.Add(NursingUntil);
            h.Add(Chrono); h.Add(Pace); h.Add(Diligence); h.Add(Social); h.Add(Bold); h.Add(Curious); h.Add(SkFarm); h.Add(SkWood);
            h.Add(SkHunt); h.Add(SkFish); h.Add(Reserve); h.Add(Ate); h.Add(Carry); h.Add(Ground);
            h.Add((byte)Act); h.Add((byte)Step); h.Add((byte)Why); h.Add(WhyArg); h.Add(Dest); h.Add(RouteA); h.Add(RouteB);
            h.Add(T0); h.Add(T1); h.Add(TA); h.Add(FromX); h.Add(FromY); h.Add(ToX); h.Add(ToY); h.Add(S0); h.Add(S1);
            h.Add(ActVer); h.Add(RngCount); h.Add(Alarm); h.Add(AlarmProc); h.Add(AlarmT); h.Add(AlarmVer); h.Add(StayDur); h.Add(Visits); h.Add(Until); h.Add(Worked); h.Add(KNets); h.Add(KLoom); h.Add(KQuern); h.Add(KPottery); h.Add(KArd); h.Add(KMetal); h.Add(KIron); h.Add(KBoat);
            h.Add(Sick); h.Add(SickPhase); h.Add(SickUntil); h.Add(SickCause); h.Add(Imm0); h.Add(Imm1); h.Add(ImmUntil0); h.Add(ImmUntil1); h.Add(Cargo); h.Add(CargoQty); h.Add(CargoTo);
            h.Add(Imm2); h.Add(Imm3); h.Add(Imm4); h.Add(Imm5); h.Add(ImmUntil2); h.Add(ImmUntil3); h.Add(ImmUntil4); h.Add(ImmUntil5);
            h.Add(Hurt); h.Add(HurtDeed); h.Add(Errand); h.Add(Band); h.Add(SkFight); h.Add(Fights); h.Add(Voyage); h.Add(SkCraft); h.Add(SkPot);
        }
    }
}
