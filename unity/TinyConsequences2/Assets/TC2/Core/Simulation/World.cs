using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Scheduling;

namespace TC2.Core.Simulation
{
    /// <summary>Deterministic processing counters (14 §2). Saved for continuity, never hashed.</summary>
    public sealed class Metrics
    {
        public long Events, Decisions, Steps, Stale, Activities, Journeys, JourneysInterrupted, Interrupts, InterruptsHandled;
        public long Meals, Births, Deaths, Marriages, SystemTicks, FireSteps, Lightning, Commands;
        public readonly long[] Eruptions = new long[8]; // by VEI
        public long VentsBorn, VentsDied, Islands, EruptionDeaths;
        public readonly long[] Quakes = new long[10];   // by whole magnitude
        public long QuakeDeaths, Landslides, Tsunamis, TsunamiDeaths;
        public long Storms, GreatStorms, StormLandfalls, StormDeaths, Surges;
        public readonly long[] StormPeaks = new long[8];  // storms by their strongest wind, 10 m/s bins
        // the wild's food (D2-042): trips, hours and what came home (kg live game, kg fish, kg mast)
        public long HuntTrips, FishTrips, GatherTrips, HungerDeaths, Famines;
        public double GameKg, FishKg, GatherKg, WildHours, WildFu;
        public long Parties, Foundings, Scouts;   // households on the move (D2-045)
        public long BeastBirths, BeastDeaths, BeastsTaken, BeastsEaten, BeastsFeral;   // livestock (D2-048): born, died, taken by wolves, slaughtered, gone wild
        public long TendDays;                                                         // household-days someone took the animals out (D2-050)
        public double BeastFu;                                            // FU of meat from slaughter
        public double PrepHours, PrepFu, WaterHours;                      // the households' own work: hours making food ready, FU made, hours carrying water (D2-056)
        public long WaterTrips, ColdDeaths, ColdSpells;                   // trips to the well; deaths the cold at home caused; spells of cold homes
        public double CraftHours, GearMade;                               // hours making and mending goods, person-equivalents made (D2-058)
        public long HomesTaken, DiedOut;                                  // empty homes taken; households that died out (D2-059)
        public long TechniquesFound, TechniquesLost;                      // techniques found by practice; lost by villages (D2-062)
        public double MetalKg;                                            // kg of metal smelted and worked (D2-064)
        public long Infections, DiseaseDeaths, Epidemics, PathogensBorn, Mutations;   // (D2-066)
        public long TradeTrips;                                           // trades made (D2-067)
        public double TradeValue, MetalTraded, ReliefFu;                  // gain in the villages' own values; kg of metal moved; grain given in famines
        public long Takings, TakingsSeen, TakeWeighed;                    // loads taken from neighbours' stores in the night; takers seen; hungry nights weighed (D2-069)
        public double TakenFu;
        public long Blows, WoundDeaths, WatchNights;                      // blows struck; deaths of wounds; nights a watch was kept (D2-071)
        public long Claims, Amends, Refusals, TakenBack;                  // amends asked, made, refused; loads taken back by the fed (D2-072)
        public long Bands, Raids, RaidsBeaten;                            // bands raised; raids made; raids driven off (D2-073)
        public long WallsPlanned;                                         // palisades begun (D2-074)
        public long Tributes, TributesEnded;                              // tribute agreed; ended (D2-075)
        public long BreadBonds;                                           // of them, for the relief a village could not give back (C10.4, D2-157)
        public double TributeFu;
        public double RaidLoot;
        public long VillageSteps, VillageDays;                            // village days processed, and the days they covered (DEEP, F8: several a step)
        public long BentWalks, WetWalks;                                  // lived walks bent round the water; walks no bend kept dry (D2-156)
        public double WoodFelled;                                        // loads of wood felled from the woods (C1.2)
        public long WoodsReceded;                                         // woods receded past a mark from a village (C1.2)
        public double StoneCut, QuarryHours;                              // loads of stone cut and carried home; hours at the quarries (C1.3)
        public long Footings;                                             // houses raised on stone footings
        public double ArmsMade, SmithyHours;                              // arms forged; hours at the smithies (C1.4)
        public double TrainHours;                                         // hours of practice at the training grounds (C1.5)
        public long RingsGrown;                                           // larger palisades begun round grown towns (C7.1)
        public double OreDug, MineHours;                                  // kg of ore dug and carried home; hours at the mines (C5.1)
        public long OreFound, OreSpent;                                   // villages that came to know copper; ore bodies dug out (C5.1)
        public double CharcoalKg, BurnHours;                              // kg of charcoal burnt from felled wood; hours at the hearths (C5.2a)
        public double IronKg, BogOreDug;                                  // kg of bar iron smelted; kg of bog ore dug (C5.2b)
        public double IronTraded;                                         // kg of iron carried in trade (C5.3b)
        public double OreClaimedKg; public long OreClaims;                // ore dug at bodies other villages work; takings recorded (C5.4)
        public long BoatsBegun, BoatsBuilt, BoatsWorn, BoatsLost, BoatTrips, Drowned, BoatsGivenUp, BoatOutings, CrewSeats, PlanksBuilt, Voyages, VoyageSeats, VoyagesLapsed, HomesRebuilt, HomesCleared, TownHouses; public double BoatFishKg, PlankFishKg, SeaCargoKg, ReliefBySeaFu, ReliefReturnedFu, WaresMade, WaresDrawn, WaresTraded, ShedHours;   // boats laid down, made, worn out, lost at sea; trips out; drowned; given up half-made; kg of fish from boats (C9.1)
        public long CalledBands, CalledMen, LordsAnswered;                // bands joined by tributaries' people; how many (C10.1); bands lords raised over their tributaries' wrongs (D2-108)
        public long ScoutedForOre, FoundedByOre;                          // good land told as by the ore wanted; villages founded knowing ore round their site (C5.3)
        public double PotsMade, KilnHours, PotsDrawn, JarsSet, PotsTraded;  // kg of ware fired at kilns and hours there; kg of pots taken by hearths; kg stood as jars; kg traded (C3.2)
        public double GardenHours, GardenFu;   // hours worked in the households' gardens and FU gathered from them (C3.7a)
        public double ThreshHours, ThreshFu;   // hours at the threshing floor and FU threshed out of the sheaf (C3.7b)
        public double MuckHours, MuckKg;       // hours carting muck out to the fields and kg of it spread (C3.7c)
        public long CartsBegun, CartsMade, CartsWorn, CartsGivenUp, CartTrips; public double CartKg;   // carts begun, made, worn out, given up; trading trips with a cart and kg they carried out (C4.2)
        public double CommonFedFu, KinFu, LoanFu, RepaidFu, DebtVoidFu;   // FU households drew from the village's common grain, were given by kin, borrowed, repaid; debts that lapsed (C3.3)
        public long Splits, SplitHouseholds;   // villages split at a contested succession; households that went with the rival (C10.3)
        public long Headmen, HeadsWithin, HeadsRisen; public double HeadFedFu;   // headmen risen; of them within the same household, from another household; FU the first households fed the hungry with in hard times (C10.2)
        public double UnsharedFu;   // FU larders asked of the store in hard times that no grain of their own, their kin's or a lender's spare could fill (C3.3c)
        public double MakersPaidFu, MakersEarnedFu, MakersOwedFu, SmithsEarnedFu, LevyFu, CommonPaidFu;   // households' payments for makers' make; what makers earned, were owed; smiths' pay; the villages' levies and common grain paid (C3.3b)

        public void Write(BinWriter w)
        {
            w.Long(Events); w.Long(Decisions); w.Long(Steps); w.Long(Stale); w.Long(Activities); w.Long(Journeys); w.Long(JourneysInterrupted);
            w.Long(Interrupts); w.Long(InterruptsHandled); w.Long(Meals); w.Long(Births); w.Long(Deaths); w.Long(Marriages);
            w.Long(SystemTicks); w.Long(FireSteps); w.Long(Lightning); w.Long(Commands);
            for (int i = 0; i < Eruptions.Length; i++) w.Long(Eruptions[i]);
            w.Long(VentsBorn); w.Long(VentsDied); w.Long(Islands); w.Long(EruptionDeaths);
            for (int i = 0; i < Quakes.Length; i++) w.Long(Quakes[i]);
            w.Long(QuakeDeaths); w.Long(Landslides); w.Long(Tsunamis); w.Long(TsunamiDeaths);
            w.Long(Storms); w.Long(GreatStorms); w.Long(StormLandfalls); w.Long(StormDeaths); w.Long(Surges);
            for (int i = 0; i < StormPeaks.Length; i++) w.Long(StormPeaks[i]);
            w.Long(HuntTrips); w.Long(FishTrips); w.Long(GatherTrips); w.Long(HungerDeaths); w.Long(Famines);
            w.Double(GameKg); w.Double(FishKg); w.Double(GatherKg); w.Double(WildHours); w.Double(WildFu);
            w.Long(Parties); w.Long(Foundings); w.Long(Scouts);
            w.Long(BeastBirths); w.Long(BeastDeaths); w.Long(BeastsTaken); w.Long(BeastsEaten); w.Long(BeastsFeral); w.Double(BeastFu); w.Long(TendDays);
            w.Double(PrepHours); w.Double(PrepFu); w.Double(WaterHours); w.Long(WaterTrips); w.Long(ColdDeaths); w.Long(ColdSpells);
            w.Double(CraftHours); w.Double(GearMade); w.Long(HomesTaken); w.Long(DiedOut); w.Long(TechniquesFound); w.Long(TechniquesLost); w.Double(MetalKg); w.Long(Infections); w.Long(DiseaseDeaths); w.Long(Epidemics); w.Long(PathogensBorn); w.Long(Mutations); w.Long(TradeTrips); w.Double(TradeValue); w.Double(MetalTraded); w.Double(ReliefFu); w.Long(Takings); w.Long(TakingsSeen); w.Long(TakeWeighed); w.Double(TakenFu); w.Long(Blows); w.Long(WoundDeaths); w.Long(WatchNights); w.Long(Claims); w.Long(Amends); w.Long(Refusals); w.Long(TakenBack); w.Long(Bands); w.Long(Raids); w.Long(RaidsBeaten); w.Double(RaidLoot); w.Long(WallsPlanned); w.Long(Tributes); w.Long(TributesEnded); w.Double(TributeFu); w.Long(VillageSteps); w.Long(VillageDays); w.Double(WoodFelled); w.Long(WoodsReceded); w.Double(StoneCut); w.Double(QuarryHours); w.Long(Footings); w.Double(ArmsMade); w.Double(SmithyHours); w.Double(TrainHours); w.Long(RingsGrown); w.Double(OreDug); w.Double(MineHours); w.Long(OreFound); w.Long(OreSpent); w.Double(CharcoalKg); w.Double(BurnHours); w.Double(IronKg); w.Double(BogOreDug); w.Long(ScoutedForOre); w.Long(FoundedByOre); w.Double(IronTraded); w.Double(OreClaimedKg); w.Long(OreClaims); w.Long(CalledBands); w.Long(CalledMen); w.Long(LordsAnswered); w.Long(BoatsBegun); w.Long(BoatsBuilt); w.Long(BoatsWorn); w.Long(BoatsLost); w.Long(BoatTrips); w.Long(Drowned); w.Double(BoatFishKg); w.Long(BoatsGivenUp); w.Long(BoatOutings); w.Long(CrewSeats); w.Long(PlanksBuilt); w.Double(PlankFishKg); w.Long(Voyages); w.Double(SeaCargoKg); w.Double(ReliefBySeaFu); w.Double(ReliefReturnedFu); w.Long(VoyageSeats); w.Long(VoyagesLapsed); w.Long(HomesRebuilt); w.Long(HomesCleared); w.Long(TownHouses); w.Double(WaresMade); w.Double(WaresDrawn); w.Double(WaresTraded); w.Double(ShedHours); w.Double(PotsMade); w.Double(KilnHours); w.Double(PotsDrawn); w.Double(JarsSet); w.Double(PotsTraded); w.Long(CartsBegun); w.Long(CartsMade); w.Long(CartsWorn); w.Long(CartsGivenUp); w.Long(CartTrips); w.Double(CartKg); w.Double(GardenHours); w.Double(GardenFu); w.Double(ThreshHours); w.Double(ThreshFu); w.Double(MuckHours); w.Double(MuckKg);
            w.Double(CommonFedFu); w.Double(KinFu); w.Double(LoanFu); w.Double(RepaidFu); w.Double(DebtVoidFu);   // (C3.3)
            w.Double(MakersPaidFu); w.Double(MakersEarnedFu); w.Double(MakersOwedFu); w.Double(SmithsEarnedFu); w.Double(LevyFu); w.Double(CommonPaidFu);   // (C3.3b)
            w.Double(UnsharedFu);   // (C3.3c)
            w.Long(Headmen); w.Long(HeadsWithin); w.Long(HeadsRisen); w.Double(HeadFedFu);   // (C10.2)
            w.Long(Splits); w.Long(SplitHouseholds);   // (C10.3)
        }

        public void Read(BinReader r)
        {
            Events = r.Long(); Decisions = r.Long(); Steps = r.Long(); Stale = r.Long(); Activities = r.Long(); Journeys = r.Long(); JourneysInterrupted = r.Long();
            Interrupts = r.Long(); InterruptsHandled = r.Long(); Meals = r.Long(); Births = r.Long(); Deaths = r.Long(); Marriages = r.Long();
            SystemTicks = r.Long(); FireSteps = r.Long(); Lightning = r.Long(); Commands = r.Long();
            for (int i = 0; i < Eruptions.Length; i++) Eruptions[i] = r.Long();
            VentsBorn = r.Long(); VentsDied = r.Long(); Islands = r.Long(); EruptionDeaths = r.Long();
            for (int i = 0; i < Quakes.Length; i++) Quakes[i] = r.Long();
            QuakeDeaths = r.Long(); Landslides = r.Long(); Tsunamis = r.Long(); TsunamiDeaths = r.Long();
            Storms = r.Long(); GreatStorms = r.Long(); StormLandfalls = r.Long(); StormDeaths = r.Long(); Surges = r.Long();
            for (int i = 0; i < StormPeaks.Length; i++) StormPeaks[i] = r.Long();
            HuntTrips = r.Long(); FishTrips = r.Long(); GatherTrips = r.Long(); HungerDeaths = r.Long(); Famines = r.Long();
            GameKg = r.Double(); FishKg = r.Double(); GatherKg = r.Double(); WildHours = r.Double(); WildFu = r.Double();
            Parties = r.Long(); Foundings = r.Long(); Scouts = r.Long();
            BeastBirths = r.Long(); BeastDeaths = r.Long(); BeastsTaken = r.Long(); BeastsEaten = r.Long(); BeastsFeral = r.Long(); BeastFu = r.Double(); TendDays = r.Long();
            PrepHours = r.Double(); PrepFu = r.Double(); WaterHours = r.Double(); WaterTrips = r.Long(); ColdDeaths = r.Long(); ColdSpells = r.Long();
            CraftHours = r.Double(); GearMade = r.Double(); HomesTaken = r.Long(); DiedOut = r.Long(); TechniquesFound = r.Long(); TechniquesLost = r.Long(); MetalKg = r.Double(); Infections = r.Long(); DiseaseDeaths = r.Long(); Epidemics = r.Long(); PathogensBorn = r.Long(); Mutations = r.Long(); TradeTrips = r.Long(); TradeValue = r.Double(); MetalTraded = r.Double(); ReliefFu = r.Double(); Takings = r.Long(); TakingsSeen = r.Long(); TakeWeighed = r.Long(); TakenFu = r.Double(); Blows = r.Long(); WoundDeaths = r.Long(); WatchNights = r.Long(); Claims = r.Long(); Amends = r.Long(); Refusals = r.Long(); TakenBack = r.Long(); Bands = r.Long(); Raids = r.Long(); RaidsBeaten = r.Long(); RaidLoot = r.Double(); WallsPlanned = r.Long(); Tributes = r.Long(); TributesEnded = r.Long(); TributeFu = r.Double(); VillageSteps = r.Long(); VillageDays = r.Long(); WoodFelled = r.Double(); WoodsReceded = r.Long(); StoneCut = r.Double(); QuarryHours = r.Double(); Footings = r.Long(); ArmsMade = r.Double(); SmithyHours = r.Double(); TrainHours = r.Double(); RingsGrown = r.Long(); OreDug = r.Double(); MineHours = r.Double(); OreFound = r.Long(); OreSpent = r.Long(); CharcoalKg = r.Double(); BurnHours = r.Double(); IronKg = r.Double(); BogOreDug = r.Double(); ScoutedForOre = r.Long(); FoundedByOre = r.Long(); IronTraded = r.Double(); OreClaimedKg = r.Double(); OreClaims = r.Long(); CalledBands = r.Long(); CalledMen = r.Long(); LordsAnswered = r.Long(); BoatsBegun = r.Long(); BoatsBuilt = r.Long(); BoatsWorn = r.Long(); BoatsLost = r.Long(); BoatTrips = r.Long(); Drowned = r.Long(); BoatFishKg = r.Double(); BoatsGivenUp = r.Long(); BoatOutings = r.Long(); CrewSeats = r.Long(); PlanksBuilt = r.Long(); PlankFishKg = r.Double(); Voyages = r.Long(); SeaCargoKg = r.Double(); ReliefBySeaFu = r.Double(); ReliefReturnedFu = r.Double(); VoyageSeats = r.Long(); VoyagesLapsed = r.Long(); HomesRebuilt = r.Long(); HomesCleared = r.Long(); TownHouses = r.Long(); WaresMade = r.Double(); WaresDrawn = r.Double(); WaresTraded = r.Double(); ShedHours = r.Double(); PotsMade = r.Double(); KilnHours = r.Double(); PotsDrawn = r.Double(); JarsSet = r.Double(); PotsTraded = r.Double(); CartsBegun = r.Long(); CartsMade = r.Long(); CartsWorn = r.Long(); CartsGivenUp = r.Long(); CartTrips = r.Long(); CartKg = r.Double(); GardenHours = r.Double(); GardenFu = r.Double(); ThreshHours = r.Double(); ThreshFu = r.Double(); MuckHours = r.Double(); MuckKg = r.Double();
            CommonFedFu = r.Double(); KinFu = r.Double(); LoanFu = r.Double(); RepaidFu = r.Double(); DebtVoidFu = r.Double();
            MakersPaidFu = r.Double(); MakersEarnedFu = r.Double(); MakersOwedFu = r.Double(); SmithsEarnedFu = r.Double(); LevyFu = r.Double(); CommonPaidFu = r.Double();
            UnsharedFu = r.Double();
            Headmen = r.Long(); HeadsWithin = r.Long(); HeadsRisen = r.Long(); HeadFedFu = r.Double();
            Splits = r.Long(); SplitHouseholds = r.Long();
        }

        public Metrics Clone() { var m = (Metrics)MemberwiseClone(); return m; }
    }

    /// <summary>How much a host lets AdvanceTo do before returning (state is consistent between events).</summary>
    public struct Budget
    {
        public long MaxEvents;
        public long Done;
        public Func<bool> ShouldYield;   // polled every 256 events (wall-clock budgets live in the host)
        public static Budget Unlimited => new Budget { MaxEvents = long.MaxValue };
        public static Budget Events(long n) => new Budget { MaxEvents = n };
    }

    public enum CommandKind : byte { None = 0, GodLightning = 1, Note = 2, SetResolution = 3, GodEruption = 4, GodQuake = 5, GodStorm = 6, SetStepDays = 7, GodPlague = 8, GodBlight = 9, GodMeteor = 10, GodDrought = 11 }

    /// <summary>(F9.4) A god's drought: rain withheld from the land round a place until a time. State: saved (hashed while one is held).</summary>
    public struct DrySpell
    {
        public int Cell;          // the place
        public double Reach;      // m round it
        public long Until;        // the rain comes back
        public long Event;        // the drought's record
        public void Write(BinWriter w) { w.Int(Cell); w.Double(Reach); w.Long(Until); w.Long(Event); }
        public static DrySpell Read(BinReader r) => new DrySpell { Cell = r.Int(), Reach = r.Double(), Until = r.Long(), Event = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Cell); h.Add(Reach); h.Add(Until); h.Add(Event); }
    }

    /// <summary>How people are simulated (04 §4): every activity, or households by the day (deep time).</summary>
    public enum Resolution : byte { Activity = 0, Coarse = 1 }

    /// <summary>Optional host-side profiler (wall clock lives in the host; never affects state).</summary>
    public interface ISimProfiler
    {
        void Begin(int kind);
        void End(int kind);
    }

    /// <summary>A player (god) or test command: applied as an event at its time, logged forever.</summary>
    public struct Command
    {
        public long Time;
        public CommandKind Kind;
        public int Cell;
        public long Arg;
        public void Write(BinWriter w) { w.Long(Time); w.Byte((byte)Kind); w.Int(Cell); w.Long(Arg); }
        public static Command Read(BinReader r) => new Command { Time = r.Long(), Kind = (CommandKind)r.Byte(), Cell = r.Int(), Arg = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Time); h.Add((byte)Kind); h.Add(Cell); h.Add(Arg); }
    }

    /// <summary>
    /// The world root (03 §5): owns all state and the one event queue; the only way time passes is
    /// <see cref="AdvanceTo"/>, which processes events strictly in (time, phase, seq) order.
    /// </summary>
    public sealed class World
    {
        public const int FormatSimVersion = 98; // 98: the god's meteor and drought - a strike from the sky that flattens, kills, burns and shakes round its place; the rain withheld from a land for a summer (F9.4), 97: the god's sickness and blight - two powers that act through the world's own systems, a pestilence breaking out in a village and a blight on its standing crop, each rooted in the cast (F9.3), 96: the burning-in - a band that holds the field fires the homes of the raided village's people who killed people of the village whose wrong it went for; the vengeance answers the killing (C8.0), 95: the land between villages - leavers who find no free good place known and no village to join go together, enough to make one, to found a daughter village on their own village's margins, its houses beyond every village's fields (C3.6g), 94: a people's tongue - each village's people speak as its founders did, a sound changed now and then; places, families and the born are named in it (C11.1a, D2-175), 93: the lord's peace - villages of one realm raise no bands against each other; a claim for blood between them is judged by their common lord, whose judgment binds the doers where it is no weaker (C10.10, D2-174), 92: the lord's death - when the one first among a lord village's people dies, the bonds for peace made with that village lapse; its tributaries owe the heir nothing until it has won them again by asking; bonds for bread stand (C10.9, D2-173), 91: the asking - a free village is asked a tenth at its harvest by its nearest stronger neighbour whose first among the people keeps sworn men: it bows where its people could neither stand against them nor go elsewhere, else the refusal is a wrong the asker holds (C10.8, D2-168), 90: people across water - a COARSE day's visits, the sickness and news they carry and the ways they wear go only to neighbours a way on foot joins, as lived visitors walk one (C9.5c, D2-166), 89: sworn men - a village's first household whose grain runs years beyond its needs keeps young men of its village sworn to it, a year's food each at its harvest; they practise arms every day and go with every band (C10.7b, D2-165), 88: a lordship's might by lasting strength - villages weigh each other's people of fighting age by their years, tools and arms and fighting skill, not by heads (C10.7a, D2-164), 87: a boat to cross - a village whose neighbours across the water are worth reaching makes a boat for that, and a dugout carries a load across a short water (C9.5b), 86: grain across water goes by boat - within a day's walk grain moves at once only where a way on foot joins two villages, and traders walk only along one; relief, grain given back and the lord's tenth cross water in boats (the lord's come for the tenth where its tributary has none) or not at all (C9.5a), 85: tribute carried - the lord's tenth set aside at the tributary's harvest and carried to its store by its people (C10.6, D2-161), 84: the ways' woods - routes worked out from the woods as they stood at the last turn of the plates, so a route built late, after a load or first for the view is the same (D2-160), 83: a lordship's might - villages weigh each other by their people of fighting age and the men a lord's call brings, not the day's fit at home (C10.5, D2-159), 82: bread and dependence - a village bows to the neighbour whose famine grain it cannot give back; tribute gives the relief back, a lord feeds its own (C10.4, D2-157), 81: walks on foot round the water - a lived walk bends round a lake or a bay across its way, the woods by the way their cutters walk, crofts and the well's and the idle's spots out of the water (D2-156), 80: the herds on the stubble and the fallow - their day's dung on the fields (C3.7d, D2-155), 79: the muck - the herds' night dung in the yards, carted out to the fields (C3.7c, D2-153), 78: the burial ground - the dead buried where they lived, their graves fresh and sinking (D2-151), 77: the threshing floor - the harvest held in the sheaf until its people thresh it (C3.7b); the woods remember their felling (W-FELL), 76: the household's garden - its croft dug, weeded and gathered (C3.7a), 75: the household's niche - a couple marries when its land and hands keep it, or its grain carries the shortfall (C3.5b, D2-140), 74: the soil - the fields tire, rest and the herds' dung give back (C3.6f, D2-139), 73: a headman gone to live in another household of the village stays first; clients follow the household that took theirs in (D2-138), 72: a founding party joins a village standing at its place; a way of no length arrives where it goes (D2-137), 71: the old hand on their holding for their keep (C3.6e, D2-136), 70: each household weighs leaving by its own land (C3.6d, D2-135), 69: the harvest shared by land and labour (C3.6c, D2-134), 68: each household holds land - founding shares, portions, the last out, heirs, land taken up (C3.6b, D2-133), 67: the harvest's grain comes into the store as it is reaped (the hungry gap, D2-132), 66: villages work their land harder where it is short of their want (C3.6, D2-131), 65: a couple marries when it can keep a household to the next harvest (C3.5, D2-129), 64: a contested succession splits a village (C10.3, D2-128), 63: the village's first household and its headman (C10.2, D2-127), 62: the household's grain and what households owe (C3.3, D2-126), 61: the village smith - a smithy's four places, a smith's practice (C1.4b, D2-125), 60: carts (C4.2, D2-122), 59: the potter's kiln, jars and hearths' pots of real clay; clay by the ground's shape (C3.2, D2-121), 58: more weaver's sheds where a town's makers cannot keep up (C3.1b, D2-120), 57: the weaver's shed and the village's wares (C3.1, D2-119), 56: footprints, a town's divided core, the market place (C13.3, D2-118), 55: streets (C13.1, D2-116), 54: how buildings are built, building traditions (D2-114), 53: relief owed back, social storage (C9.3, D2-113), 52: trade by sea (C9.3, D2-113), 51: boats of planks, the boat shed (C9.2b, D2-112), 50: the landing and the crew (C9.2a, D2-111), 49: boats from the woods (C9.1, D2-110), 48: the lord answers for its tributaries (D2-108), 47: the lord's call (C10.1, D2-107), 46: whose ore (C5.4, D2-105), 45: iron traded, made for buyers (C5.3b, D2-104), 44: places by their ore (C5.3, D2-103), 43: bog iron and the bloomery (C5.2b, D2-102), 42: charcoal (C5.2a, D2-101), 41: copper ore bodies and mines (C5.1, D2-100), 40: rings that grow (C7.1, D2-098), 39: ways worn by use (C4.1, D2-097), 38: markets, rings on dry ground (C1.6, D2-095), 37: training grounds and fighting skill (C1.5, D2-094), 36: the smithy and arms (C1.4, D2-093), 35: stone and the quarry (C1.3, D2-092), 34: timber from the woods (C1.2, D2-091), 33: structures by kind, works by need, granary capacity (C1.1, D2-090), 32: the store a DEEP step leads (F8), 31: DEEP steps (F8), 30: the annals (D2-077), 29: tribute and lordship (D2-075), 28: palisades (D2-074), 27: raids (D2-073), 26: amends, news and taking back (D2-072), 25: the watch, blows and injury (D2-071), 24: sickness at scale (D2-070), 23: deeds and taking (D2-069), 22: trade and relief (D2-067), 21: sickness (D2-066), 20: metal (D2-064), 19: materials and two more techniques (D2-063), 18: techniques carried by people (D2-062), 17: homes standing on their own (D2-059), 16: goods made and mended (D2-058), 15: the household's work and warmth (D2-056), 14: tending (D2-050), 13: herds near people, livestock (D2-047, D2-048), 12: building, knowing places, parties (D2-045), 11: relief, ice and animal densities (D2-043, D2-044), 2: volcanoes (D2-028), 3: quakes (D2-030), 4: erosion and storms (D2-031, D2-032), 5: wildlife (D2-035), 6: ice-aware weathering (D2-036), 7: evolution (D2-037), 8: crag fare (D2-038), 9: the sea's life (D2-039), 10: wild food, body reserves, famine memory (D2-042)

        public WorldConfig Config;
        public ulong Seed => Config.Seed;
        public long Now;
        public long Created;
        public Resolution Resolution;   // authoritative; changed only by a logged command
        /// <summary>
        /// DEEP (04 §4.3b, F8): in COARSE, the days a calm village lives at a step (1 or 0: every day). Changed only by a logged
        /// command, at a midnight; Ancient and Deep time catch-ups set it.
        /// </summary>
        public int StepDays;

        /// <summary>
        /// When the plants and the animals next take their step (the vegetation system's tick): a DEEP step never spans one, so what
        /// a village's herds root and graze is charged to the plants' window it belongs to (F8). Derived from the queue: never saved.
        /// </summary>
        public long NextPlantTick;
        public readonly IdAllocator Ids = new IdAllocator();
        public readonly EventQueue Queue = new EventQueue();
        public readonly FxRing Fx = new FxRing();   // for the view only: never read by the simulation, never saved
        public readonly HistoryLedger History = new HistoryLedger();
        public PlanetState Planet;
        public PlanetSystems PlanetSys;     // derived helpers (weather blocks, mean temperatures)
        public FireSystem Fire;
        public Drainage Drainage;           // derived from relief and sea level
        public Tectonics Tect;              // plate motion (scratch and frames; the plates live in the planet state)
        public readonly PagedStore<Settlement> Settlements = new PagedStore<Settlement>();
        public readonly PagedStore<Household> Households = new PagedStore<Household>();
        public readonly PagedStore<Person> People = new PagedStore<Person>();
        public readonly PagedStore<Party> Parties = new PagedStore<Party>();   // households on their way to a new home (D2-045)
        public readonly PagedStore<Herd> Herds = new PagedStore<Herd>();       // herds and packs near villages (D2-047)
        public readonly PagedStore<Beast> Beasts = new PagedStore<Beast>();    // households' animals (D2-048)
        public readonly PagedStore<Building> Buildings = new PagedStore<Building>();   // homes standing on their lots (D2-059)
        public readonly PagedStore<TC2.Core.Health.Pathogen> Pathogens = new PagedStore<TC2.Core.Health.Pathogen>();   // sicknesses' lineages (D2-066)
        public readonly TC2.Core.Health.Disease Disease = new TC2.Core.Health.Disease();
        public readonly Trade Trade = new Trade();   // trade by want and relief (D2-067)
        public readonly Holdings Holdings = new Holdings();   // the household's grain in its village's store, what households owe each other (C3.3)
        public readonly PagedStore<TC2.Core.Society.Deed> Deeds = new PagedStore<TC2.Core.Society.Deed>();   // deeds kept while weighed (D2-069)
        public readonly TC2.Core.Society.DeedBook DeedBook = new TC2.Core.Society.DeedBook();   // what each village knows of them
        public readonly TC2.Core.Society.Conflict Conflict = new TC2.Core.Society.Conflict();   // taking by need (D2-069)
        public readonly PagedStore<TC2.Core.Society.Band> Bands = new PagedStore<TC2.Core.Society.Band>();   // bands raised (D2-073)
        public readonly Agents.Ways Ways = new Agents.Ways();   // the ground between villages as their people walk it (C4.1)
        public readonly Places.Deposits Deposits = new Places.Deposits();   // the ore bodies people have seen (C5.1)
        public readonly TC2.Core.History.Annals Annals = new TC2.Core.History.Annals();   // each year in numbers, for historians (D2-077)
        public readonly List<Command> CommandLog = new List<Command>();
        /// <summary>(F9.4) The god's droughts held over the land now (rain withheld under each until its time).</summary>
        public readonly List<DrySpell> DrySpells = new List<DrySpell>();
        public readonly Metrics Metrics = new Metrics();
        public ISimProfiler Profiler;

        // systems (stateless logic bound to this world)
        public readonly AgentSystem Agents = new AgentSystem();
        public readonly SettlementSystem SettlementSys = new SettlementSystem();
        public readonly Hazards Hazards = new Hazards();
        public readonly WildFood Wild = new WildFood();
        public readonly Migration Migration = new Migration();
        public readonly HerdSystem HerdSys = new HerdSystem();
        public readonly Livestock Stock = new Livestock();
        public readonly TC2.Core.Knowledge.Learning Learning = new TC2.Core.Knowledge.Learning();   // techniques carried by people (D2-062)

        // derived indexes (rebuilt after load; never serialized)
        public readonly RouteCache Routes = new RouteCache();
        public readonly SeaRoutes SeaRoutes = new SeaRoutes();   // ways over the sea between landings (C9.3)
        public TravelIndex Travel;
        IntList[] _residents = new IntList[16];
        IntList[] _visitors = new IntList[16];
        IntList[] _hhOf = new IntList[16];
        IntList[] _members = new IntList[64];
        IntList[] _fieldCells = new IntList[16];
        IntList[] _beastsOf = new IntList[64];   // each household's live animals (derived)
        IntList[] _herdsOf = new IntList[16];    // each village's wild herds (derived)
        IntList[] _buildingsOf = new IntList[16];   // each village's buildings, standing or fallen (derived)
        int[] _cellSettlement;
        long[] _localOffset = new long[16];

        public World(WorldConfig config)
        {
            Config = config;
            Agents.Bind(this);
            SettlementSys.Bind(this);
            Hazards.Bind(this);
            Wild.Bind(this);
            Migration.Bind(this);
            HerdSys.Bind(this);
            Stock.Bind(this);
            Learning.Bind(this);
            Disease.Bind(this);
            Trade.Bind(this); SeaRoutes.Bind(this); Holdings.Bind(this);
            DeedBook.Bind(this);
            Conflict.Bind(this);
        }

        /// <summary>Allocates the planet-sized structures (worldgen and load call this).</summary>
        public void InitPlanet(PlanetState planet)
        {
            Planet = planet;
            Fire = new FireSystem(planet.Cells);
            Travel = new TravelIndex(planet.Cells);
            _cellSettlement = new int[planet.Cells];
            PlanetSys = new PlanetSystems(planet);
            Drainage = new Drainage(planet.Cells);
            Tect = new Tectonics(planet);
        }

        // ------------------------------------------------------------- history
        public long Record(HistoryEvent e) { if (Diagnostics.SizeTally.Sums != null) Diagnostics.SizeTally.Event(this, in e); return History.Record(Ids, e); }   // (tools: villages by size)

        // ------------------------------------------------------------- lists
        static IntList Slot(ref IntList[] arr, int id)
        {
            if (id >= arr.Length) { int n = arr.Length; while (n <= id) n *= 2; Array.Resize(ref arr, n); }
            return arr[id] ?? (arr[id] = new IntList());
        }
        public IntList Residents(int sid) => Slot(ref _residents, sid);
        public IntList Visitors(int sid) => Slot(ref _visitors, sid);
        public IntList HouseholdsOf(int sid) => Slot(ref _hhOf, sid);
        public IntList Members(int hid) => Slot(ref _members, hid);
        public IntList FieldCellsOf(int sid) => Slot(ref _fieldCells, sid);
        public IntList BeastsOf(int hid) => Slot(ref _beastsOf, hid);
        public IntList HerdsOf(int sid) => Slot(ref _herdsOf, sid);
        public IntList BuildingsOf(int sid) => Slot(ref _buildingsOf, sid);

        public int SettlementAtCell(int cell) => _cellSettlement == null || cell < 0 || cell >= _cellSettlement.Length ? 0 : _cellSettlement[cell];

        /// <summary>Seconds to add to world time for local solar time at a settlement (cached).</summary>
        public long LocalOffsetOf(int sid) => _localOffset[sid];

        public void IndexSettlementCells(int sid)
        {
            ref var s = ref Settlements.Get(sid);
            if (sid >= _localOffset.Length) { int n = _localOffset.Length; while (n <= sid) n *= 2; Array.Resize(ref _localOffset, n); }
            _localOffset[sid] = PlanetState.LocalOffset(s.Anchor);
            if (!s.Inhabited) return;   // an abandoned village's ground is anyone's again (D2-052)
            _cellSettlement[s.Cell] = sid;
            if (s.FieldCell >= 0 && _cellSettlement[s.FieldCell] == 0) _cellSettlement[s.FieldCell] = sid;
        }

        /// <summary>
        /// A village is abandoned (D2-052): its fields go back to the land (the woods and the grass take them as the
        /// vegetation will) and its ground is no longer a village's - another may claim or found there. Its houses stand
        /// as ruins (the view's elaboration of the abandoned households' last homes).
        /// </summary>
        public void ReleaseSettlementLand(int sid)
        {
            ref var s = ref Settlements.Get(sid);
            var cells = FieldCellsOf(sid);
            for (int i = 0; i < cells.Count; i++) if (Planet.FieldOwner[cells[i]] == sid) Planet.FieldOwner[cells[i]] = 0;
            cells.Clear();
            s.FieldHa = 0; s.FieldCells = 0;
            if (_cellSettlement[s.Cell] == sid) _cellSettlement[s.Cell] = 0;
            if (s.FieldCell >= 0 && _cellSettlement[s.FieldCell] == sid) _cellSettlement[s.FieldCell] = 0;
        }

        /// <summary>(F9.4) A god's drought: how far round the place the rain is withheld (m) and for how many days (a third of the year: a summer).</summary>
        public const double DroughtReach = 4000.0;
        public const int DroughtDays = 16;
        /// <summary>(F9.4) A meteor: what its blast flattens (m), within which those at home die, and the strength of the fire it lights.</summary>
        public const double BlastM = 140.0, KillM = 70.0, MeteorFire = 4.0;

        /// <summary>Whether the god's drought withholds the rain from a cell today (any spell held over it).</summary>
        public bool RainWithheld(int cell)
        {
            if (DrySpells.Count == 0) return false;
            Vec3d p = Planet.Grid.Center[cell];
            for (int i = 0; i < DrySpells.Count; i++)
                if (Now < DrySpells[i].Until && Vec3d.ArcDistance(p, Planet.Grid.Center[DrySpells[i].Cell], Planet.Radius) <= DrySpells[i].Reach) return true;
            return false;
        }

        /// <summary>(F9.4) The god's droughts whose time is past are let go (the weather's day).</summary>
        public void EndDrySpells() { for (int i = DrySpells.Count - 1; i >= 0; i--) if (Now >= DrySpells[i].Until) DrySpells.RemoveAt(i); }

        /// <summary>
        /// (F9.4) A meteor strikes a place: its blast flattens every building within `BlastM` (homes fall and their households go to an empty home or
        /// a camp; works are lost), those at home within `KillM` die and the rest of its homes' people are hurt; it lights a fire where it falls and
        /// the ground shakes - every consequence rooted in the cast.
        /// </summary>
        void Meteor(int cell, long cause)
        {
            var P = Planet;
            Vec3d at = P.Grid.Center[cell];
            // who and what the blast reaches (found first, so its record holds its counts and is the cause of each)
            _meteorDie.Clear(); _meteorHurt.Clear(); _meteorFall.Clear();
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                if (!s.Inhabited || Vec3d.ArcDistance(at, s.Anchor, P.Radius) > BlastM + 1000.0) continue;
                var res = Residents(sid);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref People.Get(res[i]);
                    if (!p.Alive || p.At != sid || !Households.Exists(p.Household)) continue;
                    ref var hh = ref Households.Get(p.Household);
                    double d = Vec3d.ArcDistance(at, s.LocalToSphere(hh.HomeX, hh.HomeY, P.Radius), P.Radius);
                    if (d < KillM) _meteorDie.Add(res[i]); else if (d < BlastM) _meteorHurt.Add(res[i]);
                }
                var bl = BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref Buildings.Get(bl[i]);
                    if (Places.Homes.Fallen(in b, Now) || Places.Boats.IsBoat(b.Kind)) continue;
                    if (Vec3d.ArcDistance(at, s.LocalToSphere(b.X, b.Y, P.Radius), P.Radius) < BlastM) _meteorFall.Add(bl[i]);
                }
            }
            long ev = Record(new HistoryEvent
            {
                Kind = HistoryKind.MeteorStruck, Time = Now, Cell = cell, Settlement = VillageNear(cell, BlastM + 1000.0), Cause1 = cause, Rel1 = CauseRel.CausedBy,
                ArgA = _meteorFall.Count, ArgB = _meteorDie.Count, RootKind = RootKind.You,
            });
            for (int i = 0; i < _meteorDie.Count; i++) { ref var p = ref People.Get(_meteorDie[i]); if (p.Alive) Agents.Kill(ref p, ev, 14); }
            for (int i = 0; i < _meteorHurt.Count; i++) { ref var p = ref People.Get(_meteorHurt[i]); if (p.Alive) p.Hurt = (float)DMath.Max(p.Hurt, 0.5); }
            for (int i = 0; i < _meteorFall.Count; i++)
            {
                int bid = _meteorFall[i];
                if (!Buildings.Exists(bid)) continue;
                ref var b = ref Buildings.Get(bid);
                if (Places.Structures.IsHome(b.Kind) && b.Household != 0 && Households.Exists(b.Household) && Households.Get(b.Household).Building == bid) { Places.Homes.Burnt(this, b.Household); continue; }
                ref var b2 = ref Buildings.Get(bid);
                b2.Household = 0; b2.Left = Now - DMath.ToLong(Places.Homes.FallYears(in b2) * SimTime.Year) - 1;   // (fallen at once)
                if (!Places.Structures.IsHome(b2.Kind)) Places.Structures.Lost(this, bid);
            }
            Fx.Add(Now, cell, FxKind.Stone, _meteorFall.Count);   // (for the view: the stone falling, the flash, the dust)
            Fire.TryIgnite(this, cell, ev, RootKind.You, MeteorFire);
            GodQuake(cell, ev, 5);
        }
        readonly List<int> _meteorDie = new List<int>(), _meteorHurt = new List<int>(), _meteorFall = new List<int>();

        /// <summary>(F9.3) How far from the place a god's plague or blight reaches (m): a village within a walk of it.</summary>
        public const double GodReach = 2500.0;
        /// <summary>(F9.3) The share of a standing crop a god's blight takes.</summary>
        public const double BlightShare = 0.7;

        /// <summary>The inhabited village nearest a cell within a distance (ties to the lower id), 0 none.</summary>
        public int VillageNear(int cell, double within)
        {
            Vec3d p = Planet.Grid.Center[cell];
            int best = 0; double bd = within;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double d = Vec3d.ArcDistance(p, s.Anchor, Planet.Radius);
                if (d < bd) { bd = d; best = sid; }
            }
            return best;
        }

        public string PlaceName(int cell)
        {
            int best = 0; double bd = double.MaxValue;
            Vec3d p = Planet.Grid.Center[cell];
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                double d = Vec3d.ArcDistance(p, Settlements.Get(sid).Anchor, Planet.Radius);
                if (d < bd) { bd = d; best = sid; }
            }
            if (best == 0) return "the wilds";
            string n = History.NameOf(EntityRef.Settlement(best));
            return bd < 600 ? n : "the land beyond " + n;
        }

        public string FamilyName(int householdKey) => History.NameOf(EntityRef.Household(householdKey));

        public bool FireNearSettlement(int sid, double range)
        {
            if (Fire.Active.Count == 0) return false;
            ref var s = ref Settlements.Get(sid);
            double lim = range + Planet.CellSize * 0.5;
            for (int i = 0; i < Fire.Active.Count; i++)
                if (Vec3d.ArcDistance(Planet.Grid.Center[Fire.Active[i]], s.Anchor, Planet.Radius) <= lim) return true;
            return false;
        }

        /// <summary>A neighbouring inhabited settlement within a day's easy walk, weighted by nearness.</summary>
        public int PickNeighbour(int sid, ref RandomSequence rs)
        {
            ref var s = ref Settlements.Get(sid);
            double total = 0;
            int n = 0;
            for (int o = Settlements.First(); o != 0; o = Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, Planet.Radius);
                if (d > 6000) continue;
                total += 1.0 / (300.0 + d); n++;
            }
            if (n == 0) return 0;
            double u = rs.NextUnit() * total;
            for (int o = Settlements.First(); o != 0; o = Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, Planet.Radius);
                if (d > 6000) continue;
                u -= 1.0 / (300.0 + d);
                if (u <= 0) { if (Routes.Get(this, sid, o) == null) return 0; Diagnostics.ScaleTally.Pair(this, Diagnostics.ScaleTally.Visit, sid, o); return o; }
            }
            return 0;
        }

        public void Settlements_FireOnCell(int cell, int proc) => SettlementSys.FireOnCell(cell, proc);

        // ------------------------------------------------------------- commands
        /// <summary>Queues a command at a time not before now; returns its index in the log.</summary>
        public int Issue(Command c)
        {
            if (c.Time < Now) c.Time = Now;
            CommandLog.Add(c);
            int idx = CommandLog.Count - 1;
            Queue.Push(c.Time, Phase.Command, EventKind.Command, 0, 0, idx);
            return idx;
        }

        void ApplyCommand(int idx)
        {
            var c = CommandLog[idx];
            Metrics.Commands++;
            switch (c.Kind)
            {
                case CommandKind.SetResolution:
                    SetResolutionNow((Resolution)c.Arg);
                    break;
                case CommandKind.SetStepDays:
                    StepDays = c.Arg > 0 ? (int)c.Arg : 0;   // (DEEP: the days a calm village lives at a step, F8)
                    break;
                case CommandKind.GodLightning:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        Strike(c.Cell, ev, RootKind.You, 1.6);
                        break;
                    }
                case CommandKind.GodStorm:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        // the god raises a storm at the place (its winds as asked, 50 m/s by default); it lives as storms do
                        Vec3d at = Planet.Grid.Center[c.Cell];
                        TC2.Core.Planet.Storms.Frame(at, out Vec3d east, out Vec3d north);
                        bool had = Planet.Storms.Count > 0;
                        float wind = c.Arg > 0 ? c.Arg : 50f;
                        Planet.Storms.Add(new TC2.Core.Planet.Storm
                        {
                            Id = Ids.Next(EntityKind.Process), At = at, Heading = (east * -0.95 + north * (at.Y >= 0 ? 0.3 : -0.3)).Normalized, Speed = 0.05f,
                            Wind = wind, Peak = wind, Radius = 2000f, Kind = 0, Born = Now, Cause = ev,
                        });
                        Metrics.Storms++;
                        if (!had) Queue.Push(Now + TC2.Core.Planet.Storms.StepSeconds, Phase.Process, EventKind.StormStep, 0);
                        break;
                    }
                case CommandKind.GodQuake:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        GodQuake(c.Cell, ev, c.Arg > 0 ? c.Arg : 7);
                        break;
                    }
                case CommandKind.GodPlague:
                    {
                        // (F9.3) a new sickness breaks out in the village nearest the place, within a walk of it: its traits drawn as a sickness's from
                        // the animals, but a pestilence; one of its people its first host; the outbreak and all that follows rooted in the cast
                        int sid = VillageNear(c.Cell, GodReach);
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = sid, RootKind = RootKind.You, ArgA = (int)c.Kind });
                        if (sid != 0) Disease.Pestilence(sid, ev);
                        break;
                    }
                case CommandKind.GodBlight:
                    {
                        // (F9.3) a blight on the fields round the place: each village within a walk of it loses most of its standing crop (nothing when
                        // none stands - fallow, or reaped); the failed harvest that follows names it as its cause
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = VillageNear(c.Cell, GodReach), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        Vec3d at = Planet.Grid.Center[c.Cell];
                        for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
                        {
                            ref var s = ref Settlements.Get(sid);
                            if (s.Inhabited && Vec3d.ArcDistance(at, s.Anchor, Planet.Radius) <= GodReach) SettlementSys.CropLoss(sid, BlightShare, ev, HistoryKind.CropsBlighted);
                        }
                        break;
                    }
                case CommandKind.GodMeteor:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        Meteor(c.Cell, ev);
                        break;
                    }
                case CommandKind.GodDrought:
                    {
                        // (F9.4) the rain withheld from the land round the place for a season: the soil dries, the crops wilt and the woods and fields
                        // take fire more readily, as the weather's own droughts do; the crops standing there name the drought as their harm
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = VillageNear(c.Cell, GodReach), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        long dev = Record(new HistoryEvent { Kind = HistoryKind.Drought, Time = Now, Cell = c.Cell, Settlement = VillageNear(c.Cell, DroughtReach), Cause1 = ev, Rel1 = CauseRel.CausedBy, ArgA = DroughtDays, RootKind = RootKind.You });
                        DrySpells.Add(new DrySpell { Cell = c.Cell, Reach = DroughtReach, Until = Now + DroughtDays * SimTime.Day, Event = dev });
                        Vec3d at = Planet.Grid.Center[c.Cell];
                        for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
                        {
                            ref var s = ref Settlements.Get(sid);
                            if (s.Inhabited && s.CropCause == 0 && Vec3d.ArcDistance(at, s.Anchor, Planet.Radius) <= DroughtReach) s.CropCause = dev;
                        }
                        break;
                    }
                case CommandKind.GodEruption:
                    {
                        long ev = Record(new HistoryEvent { Kind = HistoryKind.GodCommand, Time = Now, Cell = c.Cell, Settlement = SettlementAtCell(c.Cell), RootKind = RootKind.You, ArgA = (int)c.Kind });
                        GodEruption(c.Cell, ev, (int)c.Arg);
                        break;
                    }
            }
        }

        void SetResolutionNow(Resolution r)
        {
            if (r == Resolution) return;
            if (r == Resolution.Coarse) Agents.EnterCoarse();
            Resolution = r;
            if (r == Resolution.Activity) Agents.LeaveCoarse();
        }

        /// <summary>Queues a resolution change (a logged command: replays and saves agree).</summary>
        double _landM2 = -1;

        double LandM2()
        {
            if (_landM2 >= 0) return _landM2;
            double a = 0;
            for (int c = 0; c < Planet.Cells; c++) if (Planet.IsLand(c)) a += Planet.AreaHa(c) * 10000.0;
            return _landM2 = a;
        }

        /// <summary>After a season of climate: a coast that moved rebuilds the drainage; ages of ice enter the history.</summary>
        public void AfterClimate()
        {
            var P = Planet;
            if (DMath.Abs(P.SeaLevelNow - P.SeaLevel) >= 1.0) CoastMoved();
            double mean = P.IceVolume / DMath.Max(1.0, LandM2());
            // the planet's normal ice: a running mean of the last ~1,500 years, from the ice it was born with (D2-043) - ages of
            // ice are advances beyond what the world has lately known, not beyond one moment of its orbit
            P.IceRef += (P.IceVolume - P.IceRef) * Climate.StepYears / 1500.0;
            double rel = P.IceVolume / DMath.Max(1e6, P.IceRef);
            if (P.Glacial == 0 && rel > 1.35)
            {
                P.Glacial = 1;
                Record(new HistoryEvent { Kind = HistoryKind.IceAge, Time = Now, Cell = -1, RootKind = RootKind.Nature, Magnitude = (float)mean });
            }
            else if (P.Glacial == 1 && rel < 1.1)
            {
                P.Glacial = 0;
                Record(new HistoryEvent { Kind = HistoryKind.IceAgeEnded, Time = Now, Cell = -1, RootKind = RootKind.Nature, Magnitude = (float)mean });
            }
        }

        /// <summary>The sea rose or fell past the coast the drainage was built for: land use, waters and routes follow.</summary>
        void CoastMoved()
        {
            var P = Planet;
            double before = P.SeaLevel;
            P.SeaLevel = P.SeaLevelNow;
            LandChanged(before, true);
        }

        /// <summary>A step of the plates: the crust moves, boundaries rise and sink, villages ride their plate.</summary>
        public void TectonicStep()
        {
            var P = Planet;
            if (P.Plates.Length == 0) return;
            Profiler?.Begin(10200 + 0); RideSettlements(Tectonics.StepYears); Profiler?.End(10200 + 0); // with the motion the crust has during this step
            Profiler?.Begin(10200 + 1); Volcanism.Ride(this, Tectonics.StepYears); Profiler?.End(10200 + 1);
            var bud = ReliefBudget;   // tools only: the change of the continents' summed elevation by process (null in the game)
            double b0 = bud != null ? ContinentSum(P) : 0;
            Profiler?.Begin(10200 + 2); Tect.Step(P, Tectonics.StepYears, Seed, Now); Profiler?.End(10200 + 2);
            double b1 = bud != null ? ContinentSum(P) : 0;
            Profiler?.Begin(10200 + 3); Volcanism.Step(this, Tectonics.StepYears, Tect.ArcSupply); Profiler?.End(10200 + 3);
            double b2 = bud != null ? ContinentSum(P) : 0;
            Profiler?.Begin(10200 + 4); TC2.Core.Planet.Quakes.Step(this, Tectonics.StepYears, Tect.Slip); Profiler?.End(10200 + 4);
            double b3 = bud != null ? ContinentSum(P) : 0;
            // rivers cut and carry on the tree of the moved relief (D2-031); the tree is rebuilt again for what they changed
            Profiler?.Begin(10200 + 5); Drainage.Rebuild(P); Profiler?.End(10200 + 5);
            Profiler?.Begin(10200 + 6); Erosion.Step(this, Tectonics.StepYears); Profiler?.End(10200 + 6);
            double b4 = bud != null ? ContinentSum(P) : 0;
            Profiler?.Begin(10200 + 7); Coast.Step(this, Tectonics.StepYears); Profiler?.End(10200 + 7); // waves wear headlands, drift fills inlets (D2-034)
            if (bud != null) { double b5 = ContinentSum(P); bud[0] += b1 - b0; bud[1] += b2 - b1; bud[2] += b3 - b2; bud[3] += b4 - b3; bud[4] += b5 - b4; }
            Profiler?.Begin(10200 + 8); Evolution.Speciate(this); Profiler?.End(10200 + 8);              // isolated, diverged populations become species (D2-037)
            Profiler?.Begin(10200 + 9); LandChanged(P.SeaLevel, false); Profiler?.End(10200 + 9);
            System.Array.Copy(P.Forest, P.WayForest, P.Cells);   // (the routes were cleared with the land: the woods they will be worked out from until the next turn - D2-160)
            P.ReliefVersion++;
        }

        /// <summary>Tools only (relief experiments): summed elevation change of continental crust by process - plates, volcanoes,
        /// quakes, erosion, coasts - accumulated over tectonic steps. Null in the game and the tests.</summary>
        public static double[] ReliefBudget;

        static double ContinentSum(PlanetState P)
        {
            double s = 0;
            for (int c = 0; c < P.Cells; c++) if (P.Crust[c] == 1) s += P.Elev[c];
            return s;
        }

        void RideSettlements(double dt)
        {
            var P = Planet;
            bool moved = false;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                int pl = P.Plate[s.Cell];
                var plate = P.Plates[pl];
                double a = plate.Omega * dt, cs = DMath.Cos(a), sn = DMath.Sin(a);
                s.Anchor = Tectonics.Rotate(s.Anchor, plate.Pole, cs, sn);
                s.East = Tectonics.Rotate(s.East, plate.Pole, cs, sn);
                s.North = Tectonics.Rotate(s.North, plate.Pole, cs, sn);
                int cell = P.Grid.CellOf(s.Anchor);
                int fieldCell = P.Grid.CellOf(s.LocalToSphere(s.FieldX, s.FieldY, P.Radius));
                if (cell != s.Cell || fieldCell != s.FieldCell) { s.Cell = cell; s.FieldCell = fieldCell; }
                moved = true;
            }
            if (!moved) return;
            for (int i = 0; i < _cellSettlement.Length; i++) _cellSettlement[i] = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) IndexSettlementCells(sid);
            Routes.Invalidate();
            Agents.RebuildTravelIndex(); // legs follow the new routes, exactly as a loaded world registers them
        }

        /// <summary>
        /// Land and sea changed under the drainage (the coastline or the relief moved): drowned land loses its use,
        /// new land starts bare, the waters, climate terms, biomes and routes follow, and the history tells of it.
        /// </summary>
        void LandChanged(double coastBefore, bool record)
        {
            var P = Planet;
            int drowned = 0, emerged = 0, villages = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                bool was = Drainage.BuiltLand(c), now = P.IsLand(c);
                if (was == now) continue;
                if (was)
                {
                    drowned++;
                    if (P.FieldOwner[c] != 0) { FieldCellsOf(P.FieldOwner[c]).Remove(c); P.FieldOwner[c] = 0; }
                    if (SettlementAtCell(c) != 0) villages++;
                    P.Forest[c] = 0; P.Grass[c] = 0; P.Ice[c] = 0; P.LakeWater[c] = 0; P.SoilWater[c] = 1; P.Gathered[c] = 0; P.Felled[c] = 0;
                }
                else { emerged++; P.SoilWater[c] = 0.6f; P.Grass[c] = 0.02f; P.Forest[c] = 0; }
            }
            Profiler?.Begin(10220); Drainage.Rebuild(P); Profiler?.End(10220);
            Profiler?.Begin(10221); Drainage.SettleStored(P); Profiler?.End(10221);
            Profiler?.Begin(10222); PlanetSys.Climate.Derive(P); Profiler?.End(10222);
            PlanetSys.RefreshMeans(P);
            for (int c = 0; c < P.Cells; c++) P.ClassifyBiome(c, Now);
            Routes.Invalidate();
            _landM2 = -1;
            if (drowned + emerged > 0) P.CoastVersion++;
            long coastEvent = 0;
            if (record && drowned + emerged > 0)
                coastEvent = Record(new HistoryEvent { Kind = HistoryKind.CoastChanged, Time = Now, Cell = -1, RootKind = RootKind.Nature, Magnitude = (float)(P.SeaLevel - coastBefore), ArgA = drowned, ArgB = emerged * 1000 + villages });
            Profiler?.Begin(10223); if (drowned > 0) Homes.SeaTook(this, coastEvent); Profiler?.End(10223);   // (homes on drowned lots are lost: D2-087)
            Profiler?.Begin(10224); if (drowned + emerged > 0) Boats.ShoreMoved(this); Profiler?.End(10224);   // (boats follow the water's edge, risen or fallen: C9.1)
        }

        int NearestSettlement(int cell, double maxM)
        {
            int best = 0; double bd = maxM;
            Vec3d at = Planet.Grid.Center[cell];
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double d = Vec3d.ArcDistance(at, s.Anchor, Planet.Radius);
                if (d < bd) { bd = d; best = sid; }
            }
            return best;
        }

        public long RecordVentBorn(Vent v, long cause = 0)
        {
            Metrics.VentsBorn++;
            History.Register(new EntityRef(EntityKind.Place, v.Id), Worldgen.Names.Mountain(Seed, v.Id), Now);
            return Record(new HistoryEvent { Kind = HistoryKind.VentBorn, Time = Now, Cell = v.Cell, Settlement = NearestSettlement(v.Cell, 5000), RootKind = RootKind.Nature, ArgA = v.Id, ArgB = v.Kind, Subject = new EntityRef(EntityKind.Place, v.Id), Cause1 = cause });
        }

        /// <summary>
        /// The god wakes a volcano (R7): the nearest vent within 2.5 km erupts now with at least twice its critical
        /// pressure (its own seal decides how big); with none near, the ground splits and a new vent opens there.
        /// Every consequence roots in the command.
        /// </summary>
        void GodEruption(int cell, long cause, int vei = 0)
        {
            var P = Planet;
            int best = -1; double bd = 2500.0;
            Vec3d at = P.Grid.Center[cell];
            for (int v = 0; v < P.Vents.Count; v++)
            {
                double d = Vec3d.ArcDistance(P.Vents[v].At, at, PlanetState.GeoRadius);
                if (d < bd) { bd = d; best = v; }
            }
            long why = cause;
            if (best < 0)
            {
                int id = Ids.Next(EntityKind.Place);
                P.Vents.Add(new Vent { Id = id, Cell = cell, At = at, Plate = P.Plate[cell], Kind = 0, Born = Now, Seal = 1f });
                best = P.Vents.Count - 1;
                why = RecordVentBorn(P.Vents[best], cause);
            }
            var vt = P.Vents[best];
            vt.Pressure = DMath.Max(vt.Pressure, 2.0 * Volcanism.PressureCrit * vt.Seal);
            if (vei > 0) vt.Pressure = DMath.Max(vt.Pressure, Volcanism.PressureFor(vei)); // a size asked for
            P.Vents[best] = vt;
            Erupt(vt.Id, why);
        }

        public void RecordSpeciation(int child, int parent, int cell)
        {
            var P = Planet;
            Record(new HistoryEvent
            {
                Kind = HistoryKind.Speciation, Time = Now, Cell = cell, Settlement = cell >= 0 ? NearestSettlement(cell, 9000) : 0, RootKind = RootKind.Nature,
                Subject = new EntityRef(EntityKind.Species, P.Species[child].Id), Object = new EntityRef(EntityKind.Species, P.Species[parent].Id),
                Magnitude = (float)((double)P.Species[child].ColdTol - P.Species[parent].ColdTol),
            });
        }

        public void RecordExtinction(int s)
        {
            var id = new EntityRef(EntityKind.Species, Planet.Species[s].Id);
            Record(new HistoryEvent { Kind = HistoryKind.Extinction, Time = Now, Cell = -1, RootKind = RootKind.Nature, Subject = id });
            History.EndEntity(id, Now);
        }

        public void RecordIslandRose(Vent v)
        {
            Metrics.Islands++;
            Record(new HistoryEvent { Kind = HistoryKind.IslandRose, Time = Now, Cell = v.Cell, RootKind = RootKind.Nature, ArgA = v.Id, Subject = new EntityRef(EntityKind.Place, v.Id) });
        }

        public void RecordVentExtinct(Vent v)
        {
            Metrics.VentsDied++;
            Record(new HistoryEvent { Kind = HistoryKind.VentExtinct, Time = Now, Cell = v.Cell, Settlement = NearestSettlement(v.Cell, 5000), RootKind = RootKind.Nature, ArgA = v.Id, ArgB = v.Eruptions, Subject = new EntityRef(EntityKind.Place, v.Id) });
            History.EndEntity(new EntityRef(EntityKind.Place, v.Id), Now);
        }

        /// <summary>
        /// A volcano erupts (D2-028): its size from the pressure released; the cone grows; lava runs down the steepest
        /// way and sets the land alight; ash falls round it on woods and fields; near a great eruption people die; the veil
        /// cools the world and the gas warms it a little - each consequence caused by the eruption.
        /// </summary>
        public void Erupt(int ventId, long cause = 0)
        {
            var P = Planet;
            var g = P.Grid;
            int v = Volcanism.IndexOf(P, ventId);
            if (v < 0) return;
            var vt = P.Vents[v];
            int vei = Volcanism.Vei(vt.Pressure);
            int c = vt.Cell;
            Metrics.Eruptions[vei]++;
            long ev = Record(new HistoryEvent
            {
                Kind = HistoryKind.Eruption, Time = Now, Cell = c, Settlement = NearestSettlement(c, 8000), RootKind = RootKind.Nature, Cause1 = cause,
                Tier = vei >= 4 ? Tier.Historical : Tier.Local, Magnitude = vei, ArgA = vt.Id, Subject = new EntityRef(EntityKind.Place, vt.Id),
            });
            // the cone it piles up joins the relief at the next tectonic step, where the drainage is rebuilt
            vt.Cone = (float)(vt.Cone + 22.0 * vei);
            // lava down the steepest way
            int cur = c, length = 1 + (vei * 3) / 2;
            for (int k = 0; k < length; k++)
            {
                int next = -1; float low = P.Elev[cur];
                for (int q = 0; q < 8; q++) { int nb = g.Nbr8[cur * 8 + q]; if (P.Elev[nb] < low) { low = P.Elev[nb]; next = nb; } }
                if (next < 0 || !P.IsLand(next)) break;
                cur = next;
                Fx.Add(Now, cur, FxKind.Lava, vei);
                Fire.TryIgnite(this, cur, ev, RootKind.Nature, 6.0);
                if (P.Forest[cur] > 0.02f) P.Forest[cur] = 0.02f;
                if (P.Grass[cur] > 0.05f) P.Grass[cur] = 0.05f;
                if (P.FieldOwner[cur] != 0) SettlementSys.AshOnFields(P.FieldOwner[cur], 1.0, ev);
            }
            // ash round the vent: woods and pasture suffer, and the crops of every village whose fields lie under it
            double ashR = 280.0 * DMath.Exp((vei - 2) * 0.6931);
            double ashCos = DMath.Cos(ashR / PlanetState.GeoRadius);
            Vec3d vat = g.Center[c];
            var hit = new List<int>();
            for (int a = 0; a < P.Cells; a++)
            {
                if (!P.IsLand(a) || Vec3d.Dot(g.Center[a], vat) < ashCos) continue;
                P.Grass[a] = (float)(P.Grass[a] * 0.7); P.Forest[a] = (float)(P.Forest[a] * 0.93);
                int owner = P.FieldOwner[a];
                if (owner != 0 && !hit.Contains(owner)) hit.Add(owner);
            }
            foreach (int sid in hit) SettlementSys.AshOnFields(sid, DMath.Min(0.9, 0.12 * (vei - 1)), ev);
            // near a great eruption the clouds of hot ash kill
            if (vei >= 4)
            {
                double deadR = 350.0 * (vei - 3);
                Vec3d at = g.Center[c];
                for (int id = People.First(); id != 0; id = People.Next(id))
                {
                    ref var p = ref People.Get(id);
                    if (!p.Alive) continue;
                    if (Vec3d.ArcDistance(Agents.PositionAt(p, Now), at, PlanetState.GeoRadius) > deadR) continue;
                    if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 51) < 0.55) { Agents.Kill(ref p, ev, 4); Metrics.EruptionDeaths++; }
                }
            }
            // the veil (cooling for a few years) and the gas
            P.Aerosol += 0.15 * DMath.Exp((vei - 6) * 1.61); // a VEI 6 (Pinatubo-like) veil ~0.15, a VEI 7 ~0.75
            vt.Pressure = 0; vt.LastEruption = Now; vt.Eruptions++; vt.Next = 0;
            P.Vents[v] = vt;
        }

        List<int> _rupture, _shaken;
        int[] _qsrc, _qstamp; int _qstampNow;

        /// <summary>
        /// A fault patch ruptures (D2-030): the cascade along the fault gives the quake's size; where it shakes, people
        /// indoors die as houses fall (most at night), stores spill, steep slopes slide, and a great quake under the sea
        /// sends a wave over low coasts - each with the quake as its cause. Small harmless quakes leave no history.
        /// </summary>
        /// <summary>The god shakes the earth (R7): a fault line through the cell loaded to the brink and broken at once - long
        /// enough for the magnitude asked for (one patch ~5.5, each x5 in length +1 M).</summary>
        void GodQuake(int cell, long cause, long magnitude)
        {
            var P = Planet;
            var g = P.Grid;
            int n = (int)DMath.Clamp(DMath.RoundHalfUp(DMath.Exp((magnitude - 5.5) / 1.4 * DMath.Log(10.0))), 1, 200);
            int c = cell;
            for (int i = 0; i < n; i++) { P.Stress[c] = (float)(TC2.Core.Planet.Quakes.Threshold * 1.01); c = g.Nbr4[c * 4 + (i % 3 == 2 ? 1 : 0)]; }
            Quake(cell, cause);
        }

        public void Quake(int c0, long cause = 0)
        {
            var P = Planet;
            var g = P.Grid;
            var rup = _rupture ?? (_rupture = new List<int>());
            double released = TC2.Core.Planet.Quakes.Cascade(this, c0, rup);
            if (rup.Count == 0) return;
            double M = TC2.Core.Planet.Quakes.Magnitude(released);
            Metrics.Quakes[(int)DMath.Clamp(DMath.Floor(M), 0, 9)]++;
            Fx.Add(Now, c0, FxKind.Quake, (float)M);
            // where it shook: a BFS out from the rupture to the reach of intensity 6, each cell knowing its nearest rupture
            double reach = TC2.Core.Planet.Quakes.Reach(M);
            if (_qsrc == null || _qsrc.Length != P.Cells) { _qsrc = new int[P.Cells]; _qstamp = new int[P.Cells]; _qstampNow = 0; }
            _qstampNow++;
            var shaken = _shaken ?? (_shaken = new List<int>());
            shaken.Clear();
            int sea = 0;
            foreach (int c in rup) { _qstamp[c] = _qstampNow; _qsrc[c] = c; shaken.Add(c); if (!P.IsLand(c)) sea++; }
            for (int head = 0; head < shaken.Count; head++)
            {
                int c = shaken[head];
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (_qstamp[nb] == _qstampNow) continue;
                    if (Vec3d.ArcDistance(g.Center[nb], g.Center[_qsrc[c]], PlanetState.GeoRadius) > reach) continue;
                    _qstamp[nb] = _qstampNow; _qsrc[nb] = _qsrc[c];
                    shaken.Add(nb);
                }
            }
            long ev = 0;
            long Cause()
            {
                if (ev == 0)
                    ev = Record(new HistoryEvent
                    {
                        Kind = HistoryKind.Quake, Time = Now, Cell = c0, Settlement = NearestSettlement(c0, 8000), RootKind = RootKind.Nature, Cause1 = cause,
                        Tier = M >= 7.0 ? Tier.Historical : Tier.Local, Magnitude = (float)M, ArgA = rup.Count,
                    });
                return ev;
            }
            if (M >= TC2.Core.Planet.Quakes.MinRecord || cause != 0) Cause();
            double Shake(int c) => _qstamp[c] == _qstampNow ? TC2.Core.Planet.Quakes.Intensity(M, Vec3d.ArcDistance(g.Center[c], g.Center[_qsrc[c]], PlanetState.GeoRadius)) : 0.0;

            // people: indoors, houses fall on them (most are indoors at night)
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                Vec3d at = Agents.PositionAt(p, Now);
                double mmi = Shake(g.CellOf(at));
                if (mmi <= 7.5) continue;
                long local = SimTime.FloorMod(Now + PlanetState.LocalOffset(at), SimTime.Day);
                bool night = local < 6 * SimTime.Hour || local >= 21 * SimTime.Hour;
                bool indoors = KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 61) < (night ? 0.9 : 0.35);
                double x = mmi - 7.5;
                double risk = (indoors ? 0.04 : 0.008) * x * x;
                if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 62) < risk) { Agents.Kill(ref p, Cause(), 5); Metrics.QuakeDeaths++; }
            }
            // stores spill and spoil
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid))
            {
                ref var s = ref Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double mmi = Shake(s.Cell);
                if (mmi < 7.0 || s.Granary <= 0) continue;
                double loss = DMath.Min(0.5, 0.08 * (mmi - 6.0));
                s.Granary *= 1.0 - loss;
                s.Sheaves *= 1.0 - loss;   // (the ricks and the bins alike: C3.7b)
                Record(new HistoryEvent { Kind = HistoryKind.StoresSpoiled, Time = Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Cause1 = Cause(), Rel1 = CauseRel.CausedBy, Magnitude = (float)loss });
            }
            // steep shaken slopes slide: woods and pasture stripped, rock moved down (a slide can dam a river)
            int slides = 0, near = 0, nearSid = 0;
            foreach (int c in shaken)
            {
                if (!P.IsLand(c)) continue;
                double mmi = Shake(c);
                if (mmi < 7.0) continue;
                int low = c; float le = P.Elev[c];
                for (int k = 0; k < 8; k++) { int nb = g.Nbr8[c * 8 + k]; if (P.Elev[nb] < le) { le = P.Elev[nb]; low = nb; } }
                if (low == c) continue;
                double slope = ((double)P.Elev[c] - le) / P.GeoCellSize;
                if (slope < 0.8) continue;
                double pr = DMath.Min(0.9, 0.08 * (mmi - 6.5) * DMath.Min(2.0, slope / 0.8));
                if (KeyedRng.Unit(Seed, RngStream.Tectonics, (ulong)c, (ulong)Now, 74) >= pr) continue;
                double drop = DMath.Min(40.0, 0.15 * ((double)P.Elev[c] - le));
                P.Elev[c] = (float)(P.Elev[c] - drop);
                P.Elev[low] = (float)(P.Elev[low] + 0.9 * drop);
                if (P.Forest[c] > 0.05f) P.Forest[c] = 0.05f;
                if (P.Grass[c] > 0.1f) P.Grass[c] = 0.1f;
                if (P.Forest[low] > 0.2f) P.Forest[low] = 0.2f;
                slides++;
                Fx.Add(Now, c, FxKind.Landslide, (float)drop);
                int sid = NearestSettlement(c, 1500);
                if (sid != 0) { near++; nearSid = sid; }
            }
            if (slides > 0)
            {
                Metrics.Landslides += slides;
                if (near > 0 || M >= TC2.Core.Planet.Quakes.MinRecord)
                    Record(new HistoryEvent { Kind = HistoryKind.Landslides, Time = Now, Cell = c0, Settlement = nearSid != 0 ? nearSid : NearestSettlement(c0, 8000), Cause1 = Cause(), Rel1 = CauseRel.CausedBy, ArgA = slides });
                LandChanged(P.SeaLevel, false);
                P.ReliefVersion++;
            }
            if (sea * 2 > rup.Count && M >= 7.0) Tsunami(M, c0, Cause());
        }

        /// <summary>
        /// A wave from a great quake under the sea (D2-030): run-up 15 m (sim) at M 7, x3.2 per magnitude, weakening with
        /// distance; low land within two cells of the sea floods - woods and pasture spoiled, the standing crop of
        /// villages whose fields lie there lost, people there drown.
        /// </summary>
        void Tsunami(double M, int c0, long cause)
        {
            var P = Planet;
            var g = P.Grid;
            double h0 = 15.0 * DMath.Exp(0.5 * (M - 7.0) * DMath.Log(10.0));
            double reach = 4000.0 * DMath.Exp(0.5 * (M - 7.0) * DMath.Log(10.0));
            Vec3d at0 = g.Center[c0];
            double cosR = DMath.Cos(reach / PlanetState.GeoRadius);
            var flooded = new List<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || P.LakeDepth[c] > 0.3f || Vec3d.Dot(g.Center[c], at0) < cosR) continue;
                // how near the sea: next to it, or one cell further in
                int coast = 2;
                for (int k = 0; k < 8 && coast > 0; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (!P.IsLand(nb)) coast = 0;
                    else if (coast > 1) for (int j = 0; j < 8; j++) if (!P.IsLand(g.Nbr8[nb * 8 + j])) { coast = 1; break; }
                }
                if (coast > 1) continue;
                double d = Vec3d.ArcDistance(g.Center[c], at0, PlanetState.GeoRadius);
                double h = h0 * DMath.Sqrt(1500.0 / (d + 1500.0)) * (coast == 0 ? 1.0 : 0.5);
                if (P.Elev[c] - P.SeaLevel >= h) continue;
                flooded.Add(c);
            }
            if (flooded.Count == 0) return;
            Metrics.Tsunamis++;
            var hit = new Dictionary<int, int>();
            foreach (int c in flooded) { int o = P.FieldOwner[c]; if (o != 0) hit[o] = hit.TryGetValue(o, out int n) ? n + 1 : 1; }
            int most = 0, mostN = 0;
            foreach (var kv in hit) if (kv.Value > mostN || (kv.Value == mostN && kv.Key < most)) { most = kv.Key; mostN = kv.Value; }
            long wave = Record(new HistoryEvent
            {
                Kind = HistoryKind.Tsunami, Time = Now, Cell = flooded[0], Settlement = most != 0 ? most : NearestSettlement(c0, 12000),
                Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)h0, ArgA = flooded.Count,
            });
            var mark = new HashSet<int>(flooded);
            foreach (int c in flooded) { P.Forest[c] = (float)(P.Forest[c] * 0.5); P.Grass[c] = (float)(P.Grass[c] * 0.3); Fx.Add(Now, c, FxKind.Wave, (float)h0); }
            var sids = new List<int>(hit.Keys);
            sids.Sort();
            foreach (int sid in sids)
            {
                if (!Settlements.Exists(sid)) continue;
                int fc = Settlements.Get(sid).FieldCells;
                SettlementSys.CropLoss(sid, DMath.Min(1.0, hit[sid] / (double)DMath.Max(1, fc)), wave, HistoryKind.FieldsFlooded);
            }
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                if (!mark.Contains(g.CellOf(Agents.PositionAt(p, Now)))) continue;
                if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 63) < 0.5) { Agents.Kill(ref p, wave, 6); Metrics.TsunamiDeaths++; }
            }
        }

        /// <summary>
        /// Every storm moves and acts (D2-032): rain for the next day's weather, blowdowns in woods, crops flattened, people
        /// caught out in the worst winds, a surge over low coasts where a strong storm comes ashore - each consequence
        /// caused by the storm's history event (recorded once it is a great storm or first does harm).
        /// </summary>
        public void StormStep()
        {
            var P = Planet;
            var g = P.Grid;
            double dt = TC2.Core.Planet.Storms.StepSeconds;
            for (int i = 0; i < P.Storms.Count; i++)
            {
                var st = P.Storms[i];
                bool wasLand = (st.Flags & 1) != 0;
                TC2.Core.Planet.Storms.Move(P, ref st, dt, Now);
                int cc = g.CellOf(st.At);
                bool land = P.IsLand(cc);
                st.Flags = (byte)(land ? st.Flags | 1 : st.Flags & ~1);
                long Cause()
                {
                    if (st.Event == 0)
                    {
                        st.Event = Record(new HistoryEvent
                        {
                            Kind = HistoryKind.Storm, Time = Now, Cell = cc, Settlement = NearestSettlement(cc, 8000), RootKind = RootKind.Nature,
                            Magnitude = st.Wind, ArgA = st.Kind, Subject = new EntityRef(EntityKind.Process, st.Id), Cause1 = st.Cause,
                        });
                    }
                    return st.Event;
                }
                if (st.Wind >= TC2.Core.Planet.Storms.GreatWind && (st.Flags & 4) == 0) { st.Flags |= 4; Metrics.GreatStorms++; Cause(); }
                if (land && !wasLand && st.Wind >= 25f)
                {
                    Metrics.StormLandfalls++;
                    Record(new HistoryEvent
                    {
                        Kind = HistoryKind.StormLandfall, Time = Now, Cell = cc, Settlement = NearestSettlement(cc, 8000), Cause1 = Cause(), Rel1 = CauseRel.PartOf,
                        Tier = st.Wind >= 50f ? Tier.Historical : Tier.Local, Magnitude = st.Wind, Subject = new EntityRef(EntityKind.Process, st.Id),
                    });
                    st.Flags |= 2;
                    if (st.Wind >= 35f) Surge(st, Cause());
                }
                // the winds and the rain over everything within the storm's reach (a BFS out from its centre)
                double cosR = DMath.Cos(st.Radius / PlanetState.GeoRadius);
                var crops = new SortedDictionary<int, double>();
                var reach = StormCells(cc, st.At, cosR);
                for (int ri = 0; ri < reach.Count; ri++)
                {
                    int c = reach[ri];
                    double dot = Vec3d.Dot(g.Center[c], st.At);
                    double r = DMath.Acos(DMath.Min(1.0, dot)) * PlanetState.GeoRadius;
                    double wv = TC2.Core.Planet.Storms.WindAt(st, r);
                    double rf = 1.0 - r / st.Radius;
                    P.StormRain[c] = (float)(P.StormRain[c] + 1.0 * (st.Wind / 35.0) * rf * DMath.Sqrt(rf));
                    if (!P.IsLand(c)) continue;
                    if (wv > 32.0) { double x = wv - 32.0; P.Forest[c] = (float)(P.Forest[c] * (1.0 - DMath.Min(0.6, 0.0006 * x * x))); }
                    int owner = P.FieldOwner[c];
                    if (owner != 0 && wv > 25.0) crops[owner] = crops.TryGetValue(owner, out double m) ? DMath.Max(m, wv) : wv;
                }
                foreach (var kv in crops)
                {
                    double share = DMath.Min(0.6, 0.004 * (kv.Value - 25.0));
                    if (share < 0.01) continue;
                    // one record per storm and village: later steps of the same storm add to the loss quietly
                    bool told = false;
                    var evs = History.EventsOf(EntityRef.Settlement(kv.Key));
                    if (evs != null)
                        for (int k = evs.Count - 1; k >= 0 && k >= evs.Count - 12; k--)
                            if (History.TryGet(evs[k], out var he) && he.Kind == HistoryKind.CropsFlattened && he.Cause1 == st.Event && st.Event != 0) { told = true; break; }
                    if (told) { ref var sv = ref Settlements.Get(kv.Key); if (sv.Inhabited) sv.CropBurnt = DMath.Min(1.0, sv.CropBurnt + share); }
                    else SettlementSys.CropLoss(kv.Key, share, Cause(), HistoryKind.CropsFlattened);
                }
                // people caught out in the worst of it
                if (st.Wind > 40f)
                    for (int id = People.First(); id != 0; id = People.Next(id))
                    {
                        ref var p = ref People.Get(id);
                        if (!p.Alive) continue;
                        Vec3d at = Agents.PositionAt(p, Now);
                        double wv = TC2.Core.Planet.Storms.WindAt(st, Vec3d.ArcDistance(at, st.At, PlanetState.GeoRadius));
                        if (wv <= 40.0) continue;
                        if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 64) < 0.0003 * (wv - 40.0)) { Agents.Kill(ref p, Cause(), 7); Metrics.StormDeaths++; }
                    }
                P.Storms[i] = st;
            }
            for (int i = P.Storms.Count - 1; i >= 0; i--)
                if (TC2.Core.Planet.Storms.Spent(P.Storms[i], Now)) { Metrics.StormPeaks[(int)DMath.Clamp(P.Storms[i].Peak / 10.0, 0, 7)]++; P.Storms.RemoveAt(i); }
            if (P.Storms.Count > 0) Queue.Push(Now + TC2.Core.Planet.Storms.StepSeconds, Phase.Process, EventKind.StormStep, 0);
        }

        List<int> _stormCells;

        /// <summary>The cells whose centres lie within a storm's reach (dot with its centre at least cosR), by a BFS.</summary>
        List<int> StormCells(int c0, Vec3d at, double cosR)
        {
            var P = Planet;
            var g = P.Grid;
            if (_qsrc == null || _qsrc.Length != P.Cells) { _qsrc = new int[P.Cells]; _qstamp = new int[P.Cells]; _qstampNow = 0; }
            var list = _stormCells ?? (_stormCells = new List<int>());
            list.Clear();
            _qstampNow++;
            _qstamp[c0] = _qstampNow;
            if (Vec3d.Dot(g.Center[c0], at) >= cosR) list.Add(c0); else return list;
            for (int head = 0; head < list.Count; head++)
            {
                int c = list[head];
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (_qstamp[nb] == _qstampNow) continue;
                    _qstamp[nb] = _qstampNow;
                    if (Vec3d.Dot(g.Center[nb], at) >= cosR) list.Add(nb);
                }
            }
            return list;
        }

        /// <summary>A strong storm comes ashore: the sea is driven over low land near its centre (0.6 m per m/s above 30).</summary>
        void Surge(in TC2.Core.Planet.Storm st, long cause)
        {
            var P = Planet;
            var g = P.Grid;
            double h0 = 0.6 * (st.Wind - 30.0);
            double cosR = DMath.Cos(0.6 * st.Radius / PlanetState.GeoRadius);
            var flooded = new List<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || P.LakeDepth[c] > 0.3f || Vec3d.Dot(g.Center[c], st.At) < cosR) continue;
                bool coast = false;
                for (int k = 0; k < 8 && !coast; k++) if (!P.IsLand(g.Nbr8[c * 8 + k])) coast = true;
                if (!coast || P.Elev[c] - P.SeaLevel >= h0) continue;
                flooded.Add(c);
            }
            if (flooded.Count == 0) return;
            Metrics.Surges++;
            long ev = Record(new HistoryEvent { Kind = HistoryKind.StormSurge, Time = Now, Cell = flooded[0], Settlement = NearestSettlement(flooded[0], 6000), Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)h0, ArgA = flooded.Count });
            var hit = new SortedDictionary<int, int>();
            var mark = new HashSet<int>(flooded);
            foreach (int c in flooded)
            {
                P.Forest[c] = (float)(P.Forest[c] * 0.7); P.Grass[c] = (float)(P.Grass[c] * 0.5);
                Fx.Add(Now, c, FxKind.Wave, (float)h0);
                int o = P.FieldOwner[c]; if (o != 0) hit[o] = hit.TryGetValue(o, out int n) ? n + 1 : 1;
            }
            foreach (var kv in hit)
            {
                if (!Settlements.Exists(kv.Key)) continue;
                SettlementSys.CropLoss(kv.Key, DMath.Min(1.0, kv.Value / (double)DMath.Max(1, Settlements.Get(kv.Key).FieldCells)), ev, HistoryKind.FieldsFlooded);
            }
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive || !mark.Contains(g.CellOf(Agents.PositionAt(p, Now)))) continue;
                if (KeyedRng.Unit(Seed, RngStream.Death, (ulong)id, (ulong)Now, 65) < 0.25) { Agents.Kill(ref p, ev, 8); Metrics.StormDeaths++; }
            }
        }

        public void RequestResolution(Resolution r, long at) => Issue(new Command { Time = at, Kind = CommandKind.SetResolution, Arg = (long)r });

        /// <summary>Queues DEEP's step (F8): from then on a calm village lives this many days at a step in COARSE (0 or 1: every day).</summary>
        public void RequestStepDays(int days, long at) => Issue(new Command { Time = at, Kind = CommandKind.SetStepDays, Arg = days });

        void Strike(int cell, long cause, RootKind root, double strength)
        {
            Metrics.Lightning++;
            long strike = Record(new HistoryEvent { Kind = HistoryKind.LightningStrike, Time = Now, Cell = cell, Settlement = SettlementAtCell(cell), Cause1 = cause, Rel1 = CauseRel.CausedBy, RootKind = root });
            Fire.TryIgnite(this, cell, strike, root, strength);
            Fx.Add(Now, cell, FxKind.Bolt, (float)strength);   // (for the view: the bolt)
        }

        // ------------------------------------------------------------- time
        /// <summary>
        /// Processes every event with time &lt;= target in (time, phase, seq) order. Returns true when the
        /// world reached target; false if the budget ran out first (call again; nothing is skipped).
        /// </summary>
        public bool AdvanceTo(long target, ref Budget budget)
        {
            while (Queue.Count > 0 && Queue.PeekTime() <= target)
            {
                if (budget.Done >= budget.MaxEvents) return false;
                if ((budget.Done & 255) == 255 && budget.ShouldYield != null && budget.ShouldYield()) { budget.Done++; return false; }
                var e = Queue.Pop();
                if (e.Time < Now) throw new InvalidOperationException("event in the past: " + e + " now " + Now);
                Now = e.Time;
                if (Profiler != null) { int k = (int)e.Kind * 16 + (e.Kind == EventKind.SystemTick ? e.Target : 0); Profiler.Begin(k); Dispatch(in e); Profiler.End(k); }
                else Dispatch(in e);
                budget.Done++;
                Metrics.Events++;
            }
            if (target > Now) Now = target;
            return true;
        }

        public void AdvanceTo(long target) { var b = Budget.Unlimited; AdvanceTo(target, ref b); }

        void Dispatch(in ScheduledEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.SystemTick:
                    PlanetSys.Tick(this, e.Target);
                    Metrics.SystemTicks++;
                    Queue.Push(Now + PeriodOf(e.Target), Phase.SystemBase + e.Target, EventKind.SystemTick, e.Target);
                    if (e.Target == SystemId.Vegetation) NextPlantTick = Now + PeriodOf(e.Target);
                    break;
                case EventKind.ProcessStep:
                    if (e.Target == 1) { Fire.Step(this); Metrics.FireSteps++; }
                    break;
                case EventKind.AgentStep:
                    Agents.OnStep(e.Target, e.Version);
                    break;
                case EventKind.AgentInterrupt:
                    Agents.OnInterrupt(e.Target, e.Version, e.A, e.B);
                    break;
                case EventKind.SettlementDaily:
                    SettlementSys.Daily(e.Target);
                    break;
                case EventKind.Birth:
                    SettlementSys.OnBirth(e.Target, e.A);
                    break;
                case EventKind.Lightning:
                    Strike((int)e.A, e.B, RootKind.Nature, 1.0);
                    break;
                case EventKind.Quake:
                    Quake((int)e.A);
                    break;
                case EventKind.StormStep:
                    StormStep();
                    break;
                case EventKind.PartyDepart:
                    Migration.Depart(e.Target);
                    break;
                case EventKind.PartyArrive:
                    Migration.Arrive(e.Target);
                    break;
                case EventKind.Eruption:
                    {
                        int v = Volcanism.IndexOf(Planet, (int)e.A);
                        if (v >= 0 && Planet.Vents[v].Next == e.Time) Erupt((int)e.A); // a vent woken early has no second eruption
                        break;
                    }
                case EventKind.Command:
                    ApplyCommand((int)e.A);
                    break;
                case EventKind.Housekeeping:
                    Housekeeping();
                    Queue.Push(Now + SimTime.Year, Phase.Housekeeping, EventKind.Housekeeping, 0);
                    break;
            }
        }

        public long PeriodOf(int system)
        {
            bool coarse = Resolution == Resolution.Coarse;
            switch (system)
            {
                case SystemId.Climate: return coarse ? SimTime.Day : Config.ClimatePeriod;
                case SystemId.Weather: return Config.WeatherPeriod;
                case SystemId.Vegetation: return coarse ? 3 * Config.VegetationPeriod : Config.VegetationPeriod;
                case SystemId.ClimateSlow: return SimTime.Year / 4;
                case SystemId.Tectonics: return DMath.ToLong(Tectonics.StepYears * SimTime.Year);
                default: return SimTime.Day;
            }
        }

        readonly HashSet<int> _ancestors = new HashSet<int>();
        HashSet<int> _gen = new HashSet<int>(), _next = new HashSet<int>();   // (set membership only: order never matters)
        readonly List<int> _prune = new List<int>();

        void Housekeeping()
        {
            Annals.Year(this);   // (the year just ended, in numbers: D2-077)
            DeedBook.Year();   // (deeds whose weight has faded are forgotten: D2-069)
            History.Compact(Now, SubjectKept);
            // the long dead who matter to no one living leave the live table (their history stays): those who are not a parent,
            // grandparent or great-grandparent of anyone alive - the living's ancestors marked once, not searched per dead
            _ancestors.Clear(); _gen.Clear();
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                if (p.Mother != 0) _gen.Add(p.Mother);
                if (p.Father != 0) _gen.Add(p.Father);
            }
            for (int g = 0; g < 3 && _gen.Count > 0; g++)   // parents, grandparents, great-grandparents
            {
                _ancestors.UnionWith(_gen);
                _next.Clear();
                foreach (int a in _gen)
                {
                    if (!People.Exists(a)) continue;
                    ref var q = ref People.Get(a);
                    if (q.Mother != 0) _next.Add(q.Mother);
                    if (q.Father != 0) _next.Add(q.Father);
                }
                var t = _gen; _gen = _next; _next = t;
            }
            _prune.Clear();
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (p.Alive || Now - p.Died < 60 * SimTime.Year) continue;
                if (_ancestors.Contains(id)) continue;
                _prune.Add(id);
            }
            for (int i = 0; i < _prune.Count; i++) People.Remove(_prune[i]);
            Stock.Prune();   // dead animals leave the table
            Homes.Prune(this);   // ruins gone into the land leave it too (D2-059)
            Ways.Prune(Now);     // and ways nobody walks any more (C4.1)
        }



        bool SubjectKept(EntityRef r)
        {
            // a household's own events matter while it has living members, and thirty years after it ends (as a person's
            // after death); its notable ones are kept by their importance - before, every household's huts and houses stayed
            // in the ledger for ever (38,769 of 95,615 events at year 300 of seed 42)
            if (r.Kind == EntityKind.Building) return Buildings.Exists(r.Id);   // (a structure's story while it or its ruin lasts: C1.6)
            if (r.Kind == EntityKind.Household)
            {
                if (!Households.Exists(r.Id)) return false;
                ref var h = ref Households.Get(r.Id);
                if (h.Dissolved != 0) return Now - h.Dissolved < 30 * SimTime.Year;
                return Members(r.Id).Count > 0;
            }
            if (r.Kind != EntityKind.Person) return true;
            if (!People.Exists(r.Id)) return false;
            ref var p = ref People.Get(r.Id);
            return p.Alive || Now - p.Died < 30 * SimTime.Year;
        }

        /// <summary>Schedules the recurring systems for a new world (worldgen).</summary>
        public void ScheduleSystems()
        {
            for (int s = 1; s < SystemId.Count; s++) Queue.Push(Now, Phase.SystemBase + s, EventKind.SystemTick, s);
            NextPlantTick = Now;
            Queue.Push(Now + SimTime.Year, Phase.Housekeeping, EventKind.Housekeeping, 0);
        }

        // ------------------------------------------------------------- derived state
        /// <summary>Rebuilds every derived index from authoritative state (after load).</summary>
        public void RebuildDerived()
        {
            NextPlantTick = 0;
            for (int i = 0; i < Queue.RawCount; i++)
            {
                var e = Queue.RawAt(i);
                if (e.Kind == EventKind.SystemTick && e.Target == SystemId.Vegetation && (NextPlantTick == 0 || e.Time < NextPlantTick)) NextPlantTick = e.Time;
            }
            PlanetSys = new PlanetSystems(Planet);
            Tect = new Tectonics(Planet);
            Drainage.Rebuild(Planet, fromSaved: true);
            Drainage.SettleStored(Planet);
            _landM2 = -1;
            _residents = new IntList[16]; _visitors = new IntList[16]; _hhOf = new IntList[16]; _members = new IntList[64]; _fieldCells = new IntList[16];
            _beastsOf = new IntList[64]; _herdsOf = new IntList[16]; _buildingsOf = new IntList[16];
            for (int b = Buildings.First(); b != 0; b = Buildings.Next(b)) BuildingsOf(Buildings.Get(b).Settlement).Add(b);
            for (int b = Beasts.First(); b != 0; b = Beasts.Next(b)) { ref var be = ref Beasts.Get(b); if (be.Alive) BeastsOf(be.Owner).Add(b); }
            for (int h = Herds.First(); h != 0; h = Herds.Next(h)) HerdsOf(Herds.Get(h).Village).Add(h);
            for (int c = 0; c < Planet.Cells; c++) if (Planet.FieldOwner[c] != 0) FieldCellsOf(Planet.FieldOwner[c]).Add(c);
            for (int i = 0; i < _cellSettlement.Length; i++) _cellSettlement[i] = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) IndexSettlementCells(sid);
            for (int hid = Households.First(); hid != 0; hid = Households.Next(hid))
            {
                ref var h = ref Households.Get(hid);
                if (h.Dissolved == 0) HouseholdsOf(h.Settlement).Add(hid);
            }
            for (int id = People.First(); id != 0; id = People.Next(id))
            {
                ref var p = ref People.Get(id);
                if (!p.Alive) continue;
                Residents(p.Home).Add(id);
                Members(p.Household).Add(id);
                if (p.At != 0 && p.At != p.Home) Visitors(p.At).Add(id);
            }
            Routes.Invalidate();
            Agents.RebuildTravelIndex();
        }

        // ------------------------------------------------------------- persistence & hashing
        public void WriteState(BinWriter w)
        {
            Config.Write(w);
            w.Long(Now); w.Long(Created); w.Byte((byte)Resolution); w.Int(StepDays);
            Ids.Write(w);
            Queue.Write(w);
            Planet.Write(w);
            Fire.Write(w);
            w.Int(Settlements.Count); for (int id = Settlements.First(); id != 0; id = Settlements.Next(id)) Settlements.Get(id).Write(w);
            w.Int(Households.Count); for (int id = Households.First(); id != 0; id = Households.Next(id)) Households.Get(id).Write(w);
            w.Int(People.Count); for (int id = People.First(); id != 0; id = People.Next(id)) People.Get(id).Write(w);
            w.Int(Parties.Count); for (int id = Parties.First(); id != 0; id = Parties.Next(id)) Parties.Get(id).Write(w);
            w.Int(Herds.Count); for (int id = Herds.First(); id != 0; id = Herds.Next(id)) Herds.Get(id).Write(w);
            w.Int(Beasts.Count); for (int id = Beasts.First(); id != 0; id = Beasts.Next(id)) Beasts.Get(id).Write(w);
            w.Int(Buildings.Count); for (int id = Buildings.First(); id != 0; id = Buildings.Next(id)) Buildings.Get(id).Write(w);
            w.Int(Pathogens.Count); for (int id = Pathogens.First(); id != 0; id = Pathogens.Next(id)) Pathogens.Get(id).Write(w);
            w.Int(Deeds.Count); for (int id = Deeds.First(); id != 0; id = Deeds.Next(id)) Deeds.Get(id).Write(w);
            w.Int(Bands.Count); for (int id = Bands.First(); id != 0; id = Bands.Next(id)) Bands.Get(id).Write(w);
            DeedBook.Write(w);
            Ways.Write(w);
            Trade.Write(w);
            Deposits.Write(w);
            History.Write(w);
            w.Int(CommandLog.Count); for (int i = 0; i < CommandLog.Count; i++) CommandLog[i].Write(w);
            w.Int(DrySpells.Count); for (int i = 0; i < DrySpells.Count; i++) DrySpells[i].Write(w);   // (F9.4, sim version 98)
            Metrics.Write(w);
            Annals.Write(w);
            Holdings.Write(w);   // (C3.3, sim version 62)
        }

        public static World ReadState(BinReader r)
        {
            var cfg = WorldConfig.Read(r);
            var w = new World(cfg);
            w.Now = r.Long(); w.Created = r.Long(); w.Resolution = (Resolution)r.Byte(); w.StepDays = r.Int();
            w.Ids.Read(r);
            w.Queue.Read(r);
            var planet = PlanetState.Read(r);
            w.InitPlanet(planet);
            w.Fire.Read(r);
            int ns = r.Int(); for (int i = 0; i < ns; i++) { var s = Settlement.Read(r); w.Settlements.Add(s.Id, s); }
            int nh = r.Int(); for (int i = 0; i < nh; i++) { var h = Household.Read(r); w.Households.Add(h.Id, h); }
            int np = r.Int(); for (int i = 0; i < np; i++) { var p = Person.Read(r); p.Derive(w.Seed); w.People.Add(p.Id, p); }
            int npa = r.Int(); for (int i = 0; i < npa; i++) { var pa = Party.Read(r); w.Parties.Add(pa.Id, pa); }
            int nhe = r.Int(); for (int i = 0; i < nhe; i++) { var he = Herd.Read(r); w.Herds.Add(he.Id, he); }
            int nbe = r.Int(); for (int i = 0; i < nbe; i++) { var be = Beast.Read(r); w.Beasts.Add(be.Id, be); }
            int nbu = r.Int(); for (int i = 0; i < nbu; i++) { var bu = Building.Read(r); w.Buildings.Add(bu.Id, bu); }
            int npg = r.Int(); for (int i = 0; i < npg; i++) { var pg = TC2.Core.Health.Pathogen.Read(r); w.Pathogens.Add(pg.Id, pg); }
            int nde = r.Int(); for (int i = 0; i < nde; i++) { var de = TC2.Core.Society.Deed.Read(r); w.Deeds.Add(de.Id, de); }
            int nba = r.Int(); for (int i = 0; i < nba; i++) { var ba = TC2.Core.Society.Band.Read(r); w.Bands.Add(ba.Id, ba); }
            w.DeedBook.Read(r);
            w.Ways.Read(r);
            w.Trade.Read(r);
            w.Deposits.Read(r);
            w.Settlements.SetMaxId(w.Ids.Peek(EntityKind.Settlement) - 1);
            w.Households.SetMaxId(w.Ids.Peek(EntityKind.Household) - 1);
            w.People.SetMaxId(w.Ids.Peek(EntityKind.Person) - 1);
            w.Parties.SetMaxId(w.Ids.Peek(EntityKind.Process) - 1);
            w.Herds.SetMaxId(w.Ids.Peek(EntityKind.Process) - 1);
            w.Beasts.SetMaxId(w.Ids.Peek(EntityKind.Beast) - 1);
            w.Buildings.SetMaxId(w.Ids.Peek(EntityKind.Building) - 1);
            w.Pathogens.SetMaxId(w.Ids.Peek(EntityKind.Pathogen) - 1);
            w.Deeds.SetMaxId(w.Ids.Peek(EntityKind.Deed) - 1);
            w.Bands.SetMaxId(w.Ids.Peek(EntityKind.Band) - 1);
            w.History.Read(r);
            int nc = r.Int(); for (int i = 0; i < nc; i++) w.CommandLog.Add(Command.Read(r));
            int nds = r.Int(); for (int i = 0; i < nds; i++) w.DrySpells.Add(DrySpell.Read(r));
            w.Metrics.Read(r);
            w.Annals.Read(r);
            w.Holdings.Read(r);
            w.RebuildDerived();
            return w;
        }

        /// <summary>Hash of all authoritative state (not metrics, not derived indexes).</summary>
        public ulong StateHash()
        {
            var h = StateHasher.Create();
            Config.Hash(ref h);
            h.Add(Now); h.Add(Created); h.Add((byte)Resolution);
            if (StepDays > 1) h.Add(StepDays);   // (a world never in DEEP hashes as it did before F8)
            Ids.Hash(ref h);
            Queue.Hash(ref h);
            Planet.Hash(ref h);
            Fire.Hash(ref h);
            for (int id = Settlements.First(); id != 0; id = Settlements.Next(id)) Settlements.Get(id).Hash(ref h);
            for (int id = Households.First(); id != 0; id = Households.Next(id)) Households.Get(id).Hash(ref h);
            for (int id = People.First(); id != 0; id = People.Next(id)) People.Get(id).Hash(ref h);
            for (int id = Parties.First(); id != 0; id = Parties.Next(id)) Parties.Get(id).Hash(ref h);
            for (int id = Herds.First(); id != 0; id = Herds.Next(id)) Herds.Get(id).Hash(ref h);
            for (int id = Beasts.First(); id != 0; id = Beasts.Next(id)) Beasts.Get(id).Hash(ref h);
            for (int id = Buildings.First(); id != 0; id = Buildings.Next(id)) Buildings.Get(id).Hash(ref h);
            for (int id = Pathogens.First(); id != 0; id = Pathogens.Next(id)) Pathogens.Get(id).Hash(ref h);
            for (int id = Deeds.First(); id != 0; id = Deeds.Next(id)) Deeds.Get(id).Hash(ref h);
            for (int id = Bands.First(); id != 0; id = Bands.Next(id)) Bands.Get(id).Hash(ref h);
            DeedBook.Hash(ref h);
            Ways.Hash(ref h);
            Trade.Hash(ref h);
            Deposits.Hash(ref h);
            History.Hash(ref h);
            h.Add(CommandLog.Count); for (int i = 0; i < CommandLog.Count; i++) CommandLog[i].Hash(ref h);
            if (DrySpells.Count > 0) { h.Add(DrySpells.Count); for (int i = 0; i < DrySpells.Count; i++) DrySpells[i].Hash(ref h); }   // (F9.4: hashed while one is held)
            Annals.Hash(ref h);
            Holdings.Hash(ref h);
            return h.Value;
        }

        // ------------------------------------------------------------- summaries
        public int LivingPeople()
        {
            int n = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) n += Settlements.Get(sid).Population;
            return n;
        }

        public int InhabitedSettlements()
        {
            int n = 0;
            for (int sid = Settlements.First(); sid != 0; sid = Settlements.Next(sid)) if (Settlements.Get(sid).Inhabited) n++;
            return n;
        }
    }
}
