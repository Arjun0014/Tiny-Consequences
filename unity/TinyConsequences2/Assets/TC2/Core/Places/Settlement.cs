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
        public int Tended;              // the local day the village's herd last went out with its herders (D2-050)
        public byte HerdersOut;         // how many herders went out with it that day
        public long ColdEvent;          // the current spell of cold homes (0 none; D2-056)
        public float FuelDay;           // wood units its hearths and cooking burn a day (the day's cold, its homes, its mouths)
        public float BestNets, BestLoom, BestQuern;   // its best practitioner's proficiency in each technique (D2-062)
        public float BestPottery, BestArd;            // (D2-063)
        public double Fibre;            // kg of flax fibre in store (from the harvest) for clothes and cordage (D2-063)
        public double Hides;            // hides in store (from the hunt and the slaughter)
        public float BestMetal;         // (D2-064)
        public double Metal;            // kg of metal tools among its people (D2-064)
        public long Epidemic;           // the current epidemic's arrival event (0 none; D2-066)
        public int EpiPath, EpiDead;    // its pathogen; its dead so far
        public long EpiStart;
        public int ReliefFrom;          // the last village that sent it grain in a famine (D2-067)
        public long ReliefAt;           // the famine (its event) whose relief history has told
        public int Watcher;             // who keeps the watch at its store tonight (0 none; D2-071)
        public long WatchEvent;         // the watch's beginning in history (0: no watch kept)
        public int Lord;                // the village it pays tribute to (0 none), since when, and the agreement's event (D2-075)
        public long LordSince, LordEvent;
        public byte Wall;               // its palisade: 0 none, 1 being built, 2 standing, 3 fallen (D2-074)
        public byte WallKeep;           // whether it still mends it (what it loses still outweighs the mending)
        public int Tradition;           // its building tradition: carried by its founders from the village they came from (D2-114)
        public byte StreetN;            // its streets: the ways out of it its people wore to paths, laid as streets (C13.1, D2-116)
        public float Street0, Street1, Street2, Street3;   // each street's bearing from the square (radians, local frame: atan2(y, x))
        public float WallR, WallCond;   // the ring's radius (m) round the square; the timber's soundness (1 new, 0 fallen)
        public long WallEvent;          // the palisade's beginning in history
        public int Foe;                 // the village it holds most against today, its weight (FU) and the latest wrong (D2-072)
        public float FoeWorth;
        public long FoeEvent;
        public int InfNow, InfPath, InfPresent;   // infectious at home at its last day, their main pathogen, and all at home (COARSE visits read them)
        public byte TechKnown;          // the techniques it knows (bits: a competent practitioner lives here)
        public double Ahead;            // DEEP (F8): what its last step moved in or out of the store at the step's start that days lived
        public long AheadFrom, AheadTo; // would move through it (the step's start and end; 0 when none)
        public int Project;             // the structure its people are raising now (0 none: C1.1, `Structures.Plan`)
        public byte ProjectKind;        // its kind (a cache: walls go before all but water and the first store)
        public byte Granaries, Barns;   // its standing granaries and barns (a cache of the structures: the store's capacity)
        public long HarvestEvent;       // its last harvest in history (what overfilled its store: C1.1)
        public int WoodLot;             // its woodlot: the cell its people fell wood at, + 1 (0 none chosen; C1.2, `Woods`)
        public float WoodDist;          // m from its square to the woodlot
        public byte WoodFar;            // how far its woods have receded (0 near, 1 past 600 m, 2 past 1 km, 3 past 1.5 km, 4 none within reach)
        public long WoodLook;           // when it last looked for woods (none within reach: it looks again every twelve days)
        public int StoneCell;           // the nearest rock its people have seen, + 1 (0 none; C1.3, `Stone`)
        public float StoneDist;         // m from its square to that rock
        public long StoneEvent;         // when they first found rock (history)
        public double Stone;            // loads of stone in its store
        public float StoneWant;         // loads it wants in store (footings for its next houses), read once a day
        public int Quarry;              // its quarry (a structure at the rock; 0 none)
        public int Smithy;              // its smithy (0 none; C1.4, `Forge`)
        public long SmithEvent;         // when it first had a competent smith (history: what moved its smithy)
        public double Arms;             // arms in its store: spear and axe heads (C1.4)
        public int Ground;              // its training ground (0 none; C1.5, `Training`)
        public float BestFight;         // its best fighter's skill (the one the others learn from), read once a day
        public int Market;              // its market (0 none; C1.6)
        public float TradeIn;           // traders arriving to deal here, a year (a count halving each year: what makes a market)
        public float MarketR;           // m: the open market place round the square, widened by the traders who came to its market (C13.3, D2-118; 0 none)
        public float CoreR;             // m: how far along its streets from the green a town's frontage is divided into narrow plots (C13.3, D2-118; 0 none)
        public byte LanesShut;          // the back lanes not yet open - laid through houses standing when their street was laid (bit 2k, 2k+1: street k's two; C13.3)
        public float WallDry;           // the share of its palisade's ring on dry ground (C1.6: posts are never set in the sea; 0 = all)
        public float OldR, OldCond;     // an old, smaller ring left inside a grown town, unmended until it rots (0 none; C7.1)
        public int OreCell;             // the nearest copper body its people have seen that still has ore, + 1 (0 none; C5.1, `Mining`)
        public float OreDist;           // m from the village's anchor
        public long OreEvent;           // the history record of its copper found (the mine's cause)
        public double Ore;              // kg of ore in its store, dug and carried home, waiting for its smiths
        public float OreWant;           // kg of ore it wants in store (read once a day: metal short for its tools and arms)
        public int Mine;                // its mine (a structure at the body; 0 none)
        public double Charcoal;         // kg of charcoal in its store (C5.2a, `Charcoal`)
        public float CharcoalWant;      // kg it wants in store (read once a day: for the metal its smiths will smelt)
        public int CharHearth;          // its charcoal hearth (a structure at the woodlot; 0 none)
        public long ClampUntil;         // its clamp smoulders until then (fired with the last wood burnt)
        public float BestIron;          // its best in the bloomery (C5.2b)
        public double Iron, IronArms;   // kg of iron tools among its people; how many of its arms are iron
        public int FeCell;              // the nearest bed of bog ore its people have seen that still has ore, + 1 (0 none)
        public float FeDist;            // m from the village's anchor
        public long FeEvent;            // the history record of its bog ore found
        public double FeOre;            // kg of bog ore in its store
        public float FeOreWant;         // kg it wants in store (read once a day)
        public int FeMine;              // its bog-ore pits (a structure at the bed; 0 none)
        public float IronSold;          // kg of iron it parted with in trade, halving each year: what its smiths make for the next buyers (C5.3b)
        public int ClaimDeed, ClaimOn;  // its people's latest taking of ore another village works (the deed) and that village (C5.4)
        public long ClaimAt;            // when they last dug there
        public Grounds Sea;             // the day's survey of the sea its boats reach: farther out, the offshore shoals (C9.1, `Boats`)
        public float LandX, LandY;      // its landing (C9.2): where its boats are drawn up - the beach nearest its square, kept while the water is there
        public float LandUx, LandUy;    // up the beach there: from the water's edge toward the dry sand (a unit vector, local)
        public long LandAt;             // when the landing was chosen (0: none yet)
        public float BestBoat;          // its best at building boats of planks (C9.2b)
        public long BoatEvent;          // the history record of its knowing it (what moves its boat shed)
        public int Shed;                // its boat shed (a structure at the landing; 0 none)
        public float SeaP;              // FU an hour an ordinary fisher can expect from a plank boat at its sea's best ground (the offshore shoals farther)
        public int Workshop;            // its weaver's shed (0 none; C3.1, `Weaving`)
        public long LoomEvent;          // the history record of its knowing the loom (what moves its weaver's shed; 0: its founders knew it)
        public double Wares;            // person-equivalents of goods in its store, made at its shed or traded in (as `Household.Gear`)
        public float WaresHeld;         // what its households hold of wares (read daily: the share of its people in wares)
        public float WaresOut;          // wares its households took from the store, halving each year: the demand known from real use
        public float WaresSold;         // wares it parted with in trade, halving each year: what its makers make for the next buyers
        public double LoomBar;          // the bar for a loom at its shed: its eighth best weaver's loom and practice (read daily; -1: all its weavers)
        public int LoomBarId;           // and that weaver (ties go to the lower id)
        public byte Sheds;              // its working weaver's sheds (C3.1b: another where its makers cannot keep up; `Workshop` is the first)
        public byte ShedShort;          // days this year its makers left its store short with fibre to weave (counted daily, weighed yearly)
        public byte ShedMore;           // another shed wanted: its makers fell short most of last year (laid out when no work is under way)
        public int Kiln;                // its potter's kiln, at its clay (0 none; C3.2, `Pottery`)
        public long PotEvent;           // the history record of its knowing how to fire pots (what moves its kiln; 0: its founders knew)
        public double Pots;             // kg of fired ware in its store - made at its kiln or traded in - not yet in use
        public double Jars;             // kg of storage jars standing in its granaries and barns: the grain they hold kept sealed
        public float PotsOut;           // kg of pots its hearths took from the store, halving each year
        public float PotsSold;          // kg of ware it parted with in trade, halving each year: what its makers make for the next buyers
        public float HearthShort;       // kg of pots its hearths that cannot fire their own still lacked today (read daily)
        public double PotBar;           // the bar for a place at its kiln: its fourth best potter's skill and practice (read daily; -1: all its potters)
        public int PotBarId;            // and that potter (ties go to the lower id)
        public double SmithBar;         // the bar for a place at its smithy: its fourth best smith's skill and practice (read daily; -1: all its smiths; C1.4b)
        public int SmithBarId;          // and that smith (ties go to the lower id)
        public byte Smithies;           // its working smithies (C1.4b: another where its smiths cannot keep up; `Smithy` is the first)
        public byte SmithShort;         // days this year its smithies' places could not do the day's work it wanted (C1.4b)
        public byte SmithMore;          // it wants another smithy (weighed yearly, C1.4b)
        public double Common;           // FU of its granary that are the village's own, not any household's: what it was given, paid or sold (C3.3, `Holdings`)
        public int Head, HeadHh;        // first among its people (the headman) and the household with the largest following (C10.2, `Society.Leaders`; 0 none)
        public long HeadSince, HeadEvent;   // since when (or when last weighed with none) and the record of their rise
        public int SplitHh;             // a household that would not follow the new first and looks for land for its following (C10.3; 0 none)
        public long SplitCause, SplitSince;   // the death that opened that succession; when they would not follow
        public double Tilth;            // this season's extra work a hectare it planned, as a share of the ordinary tillage and weeding again (C3.6; 0 none)
        public double TilthDone;        // work-hours done this season beyond the ordinary sowing and tending
        public float TilthGain;         // what that work added to its last harvest (a share of the harvest it would have had)
        public long TilthEvent;         // the history record of the years it has worked its land harder (0: it works it as before)
        public double CropYield;        // what its grown crop bears this year (FU, before reaping and fire; set when it ripens: D2-132)
        public double Reaped;           // of it, the grain brought into the store so far this harvest
        public double Tired;            // how tired its fields are: 0 land first broken, 1 worn to what rain and the soil alone give back (C3.6f, D2-139)
        public float TiredSeen;         // that tiredness as its people have seen it in their crops (a few harvests' memory)
        public float Rest;              // the share of its arable it rests this year (fallow)
        public double Grazed;           // kg of grass its herds have eaten off the waste since the last harvest (their dung comes back to the fields)
        public long RestEvent;          // the history record of the years it has rested its fields (0: it crops all it sows)
        public double Sheaves;          // of its granary, the grain still in the sheaf - reaped and stacked, not yet threshed (C3.7b: only threshed grain is eaten)
        public float ThreshOut;         // FU its people are threshing now in stints under way (lived days; read by the next who would go to the floor)
        public int BurialCount;         // its dead buried in its ground, ever (the k-th lies in slot k mod Burials.Slots: D2-151)
        public double Graves, GravesFresh;   // its graves still marked and those still fresh earth, as at GravesAt (decaying with the years: Burials.Read)
        public long GravesAt;           // when Graves and GravesFresh were last brought up to date
        public float GraveX, GraveY;    // its burial ground (its frame, m), laid out at the first burial
        public double Muck;             // kg of muck in its yards' heaps: the herds' night dung and bedding, not yet carted out (C3.7c, D2-153)
        public double Spread;           // kg of muck carted out and spread on its fields since the last harvest (the soil reads it at the next)
        public double Stubble;          // kg (dry) of forage left on its reaped fields for its herds until the sowing ploughs it in (C3.7d)

        public float SquareX, SquareY, FieldX, FieldY, WoodX, WoodY, WellX, WellY;

        public bool Inhabited => Abandoned == 0;

        /// <summary>
        /// The store as its neighbours and its own trade see it at a moment (F8): the granary, and while a DEEP step runs, the
        /// share of what the step took at its start (meals, larders filled) or brought in (the wild's food, meat) that days lived
        /// would not yet have taken or brought - the store days lived would hold then. Equal to the granary outside DEEP.
        /// </summary>
        public double StoreSeen(long now) => AheadTo <= now ? Granary : Granary + Ahead * ((AheadTo - now) / (double)(AheadTo - AheadFrom));

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
        /// <summary>A street's bearing (radians from local east, counter-clockwise; C13.1).</summary>
        public double StreetAt(int i) => i == 0 ? Street0 : i == 1 ? Street1 : i == 2 ? Street2 : Street3;
        public void SetStreet(int i, double a) { if (i == 0) Street0 = (float)a; else if (i == 1) Street1 = (float)a; else if (i == 2) Street2 = (float)a; else Street3 = (float)a; }

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
            w.Byte(Works); w.Float(WorkBuild); Lore.Write(w); w.Long(ScoutAt); w.Int(Tended); w.Byte(HerdersOut); w.Long(ColdEvent); w.Float(FuelDay); w.Float(BestNets); w.Float(BestLoom); w.Float(BestQuern); w.Byte(TechKnown); w.Float(BestPottery); w.Float(BestArd); w.Double(Fibre); w.Double(Hides); w.Float(BestMetal); w.Double(Metal); w.Long(Epidemic); w.Int(EpiPath); w.Int(EpiDead); w.Long(EpiStart); w.Int(InfNow); w.Int(InfPath); w.Int(InfPresent); w.Int(ReliefFrom); w.Long(ReliefAt); w.Int(Watcher); w.Long(WatchEvent); w.Int(Foe); w.Float(FoeWorth); w.Long(FoeEvent); w.Byte(Wall); w.Byte(WallKeep); w.Float(WallR); w.Float(WallCond); w.Long(WallEvent); w.Int(Lord); w.Long(LordSince); w.Long(LordEvent); w.Int(Tradition); w.Byte(StreetN); w.Float(Street0); w.Float(Street1); w.Float(Street2); w.Float(Street3);
            w.Double(Ahead); w.Long(AheadFrom); w.Long(AheadTo);
            w.Int(Project); w.Byte(ProjectKind); w.Byte(Granaries); w.Byte(Barns); w.Long(HarvestEvent); w.Int(WoodLot); w.Float(WoodDist); w.Byte(WoodFar); w.Long(WoodLook); w.Int(StoneCell); w.Float(StoneDist); w.Long(StoneEvent); w.Double(Stone); w.Float(StoneWant); w.Int(Quarry); w.Int(Smithy); w.Long(SmithEvent); w.Double(Arms); w.Int(Ground); w.Float(BestFight); w.Int(Market); w.Float(TradeIn); w.Float(WallDry); w.Float(OldR); w.Float(OldCond); w.Int(OreCell); w.Float(OreDist); w.Long(OreEvent); w.Double(Ore); w.Float(OreWant); w.Int(Mine); w.Double(Charcoal); w.Float(CharcoalWant); w.Int(CharHearth); w.Long(ClampUntil); w.Float(BestIron); w.Double(Iron); w.Double(IronArms); w.Int(FeCell); w.Float(FeDist); w.Long(FeEvent); w.Double(FeOre); w.Float(FeOreWant); w.Int(FeMine); w.Float(IronSold); w.Int(ClaimDeed); w.Int(ClaimOn); w.Long(ClaimAt); Sea.Write(w); w.Float(LandX); w.Float(LandY); w.Float(LandUx); w.Float(LandUy); w.Long(LandAt); w.Float(BestBoat); w.Long(BoatEvent); w.Int(Shed); w.Float(SeaP);
            w.Float(SquareX); w.Float(SquareY); w.Float(FieldX); w.Float(FieldY); w.Float(WoodX); w.Float(WoodY); w.Float(WellX); w.Float(WellY);
            w.Float(MarketR); w.Float(CoreR); w.Byte(LanesShut);   // (C13.3, sim version 56)
            w.Int(Workshop); w.Long(LoomEvent); w.Double(Wares); w.Float(WaresHeld); w.Float(WaresOut); w.Float(WaresSold); w.Double(LoomBar); w.Int(LoomBarId);   // (C3.1, sim version 57)
            w.Byte(Sheds); w.Byte(ShedShort); w.Byte(ShedMore);   // (C3.1b, sim version 58)
            w.Int(Kiln); w.Long(PotEvent); w.Double(Pots); w.Double(Jars); w.Float(PotsOut); w.Float(PotsSold); w.Float(HearthShort); w.Double(PotBar); w.Int(PotBarId);   // (C3.2, sim version 59)
            w.Double(SmithBar); w.Int(SmithBarId); w.Byte(Smithies); w.Byte(SmithShort); w.Byte(SmithMore);   // (C1.4b, sim version 61)
            w.Double(Common);   // (C3.3, sim version 62)
            w.Int(Head); w.Int(HeadHh); w.Long(HeadSince); w.Long(HeadEvent);   // (C10.2, sim version 63)
            w.Int(SplitHh); w.Long(SplitCause); w.Long(SplitSince);   // (C10.3, sim version 64)
            w.Double(Tilth); w.Double(TilthDone); w.Float(TilthGain); w.Long(TilthEvent);   // (C3.6, sim version 66)
            w.Double(CropYield); w.Double(Reaped);   // (D2-132, sim version 67)
            w.Double(Tired); w.Float(TiredSeen); w.Float(Rest); w.Double(Grazed); w.Long(RestEvent);   // (C3.6f, sim version 74)
            w.Double(Sheaves); w.Float(ThreshOut);   // (C3.7b, sim version 77)
            w.Int(BurialCount); w.Double(Graves); w.Double(GravesFresh); w.Long(GravesAt); w.Float(GraveX); w.Float(GraveY);   // (D2-151, sim version 78)
            w.Double(Muck); w.Double(Spread);   // (C3.7c, sim version 79)
            w.Double(Stubble);   // (C3.7d, sim version 80)
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
            s.Works = r.Byte(); s.WorkBuild = r.Float(); s.Lore = KnownSites.Read(r); s.ScoutAt = r.Long(); s.Tended = r.Int(); s.HerdersOut = r.Byte(); s.ColdEvent = r.Long(); s.FuelDay = r.Float(); s.BestNets = r.Float(); s.BestLoom = r.Float(); s.BestQuern = r.Float(); s.TechKnown = r.Byte(); s.BestPottery = r.Float(); s.BestArd = r.Float(); s.Fibre = r.Double(); s.Hides = r.Double(); s.BestMetal = r.Float(); s.Metal = r.Double(); s.Epidemic = r.Long(); s.EpiPath = r.Int(); s.EpiDead = r.Int(); s.EpiStart = r.Long(); s.InfNow = r.Int(); s.InfPath = r.Int(); s.InfPresent = r.Int(); s.ReliefFrom = r.Int(); s.ReliefAt = r.Long(); s.Watcher = r.Int(); s.WatchEvent = r.Long(); s.Foe = r.Int(); s.FoeWorth = r.Float(); s.FoeEvent = r.Long(); s.Wall = r.Byte(); s.WallKeep = r.Byte(); s.WallR = r.Float(); s.WallCond = r.Float(); s.WallEvent = r.Long(); s.Lord = r.Int(); s.LordSince = r.Long(); s.LordEvent = r.Long(); s.Tradition = r.Int(); s.StreetN = r.Byte(); s.Street0 = r.Float(); s.Street1 = r.Float(); s.Street2 = r.Float(); s.Street3 = r.Float();
            s.Ahead = r.Double(); s.AheadFrom = r.Long(); s.AheadTo = r.Long();
            s.Project = r.Int(); s.ProjectKind = r.Byte(); s.Granaries = r.Byte(); s.Barns = r.Byte(); s.HarvestEvent = r.Long(); s.WoodLot = r.Int(); s.WoodDist = r.Float(); s.WoodFar = r.Byte(); s.WoodLook = r.Long(); s.StoneCell = r.Int(); s.StoneDist = r.Float(); s.StoneEvent = r.Long(); s.Stone = r.Double(); s.StoneWant = r.Float(); s.Quarry = r.Int(); s.Smithy = r.Int(); s.SmithEvent = r.Long(); s.Arms = r.Double(); s.Ground = r.Int(); s.BestFight = r.Float(); s.Market = r.Int(); s.TradeIn = r.Float(); s.WallDry = r.Float(); s.OldR = r.Float(); s.OldCond = r.Float(); s.OreCell = r.Int(); s.OreDist = r.Float(); s.OreEvent = r.Long(); s.Ore = r.Double(); s.OreWant = r.Float(); s.Mine = r.Int(); s.Charcoal = r.Double(); s.CharcoalWant = r.Float(); s.CharHearth = r.Int(); s.ClampUntil = r.Long(); s.BestIron = r.Float(); s.Iron = r.Double(); s.IronArms = r.Double(); s.FeCell = r.Int(); s.FeDist = r.Float(); s.FeEvent = r.Long(); s.FeOre = r.Double(); s.FeOreWant = r.Float(); s.FeMine = r.Int(); s.IronSold = r.Float(); s.ClaimDeed = r.Int(); s.ClaimOn = r.Int(); s.ClaimAt = r.Long(); s.Sea = Grounds.Read(r); s.LandX = r.Float(); s.LandY = r.Float(); s.LandUx = r.Float(); s.LandUy = r.Float(); s.LandAt = r.Long(); s.BestBoat = r.Float(); s.BoatEvent = r.Long(); s.Shed = r.Int(); s.SeaP = r.Float();
            s.SquareX = r.Float(); s.SquareY = r.Float(); s.FieldX = r.Float(); s.FieldY = r.Float(); s.WoodX = r.Float(); s.WoodY = r.Float(); s.WellX = r.Float(); s.WellY = r.Float();
            s.MarketR = r.Float(); s.CoreR = r.Float(); s.LanesShut = r.Byte();
            s.Workshop = r.Int(); s.LoomEvent = r.Long(); s.Wares = r.Double(); s.WaresHeld = r.Float(); s.WaresOut = r.Float(); s.WaresSold = r.Float(); s.LoomBar = r.Double(); s.LoomBarId = r.Int();
            s.Sheds = r.Byte(); s.ShedShort = r.Byte(); s.ShedMore = r.Byte();
            s.Kiln = r.Int(); s.PotEvent = r.Long(); s.Pots = r.Double(); s.Jars = r.Double(); s.PotsOut = r.Float(); s.PotsSold = r.Float(); s.HearthShort = r.Float(); s.PotBar = r.Double(); s.PotBarId = r.Int();
            s.SmithBar = r.Double(); s.SmithBarId = r.Int(); s.Smithies = r.Byte(); s.SmithShort = r.Byte(); s.SmithMore = r.Byte();
            s.Common = r.Double();
            s.Head = r.Int(); s.HeadHh = r.Int(); s.HeadSince = r.Long(); s.HeadEvent = r.Long();
            s.SplitHh = r.Int(); s.SplitCause = r.Long(); s.SplitSince = r.Long();
            s.Tilth = r.Double(); s.TilthDone = r.Double(); s.TilthGain = r.Float(); s.TilthEvent = r.Long();
            s.CropYield = r.Double(); s.Reaped = r.Double();
            s.Tired = r.Double(); s.TiredSeen = r.Float(); s.Rest = r.Float(); s.Grazed = r.Double(); s.RestEvent = r.Long();
            s.Sheaves = r.Double(); s.ThreshOut = r.Float();
            s.BurialCount = r.Int(); s.Graves = r.Double(); s.GravesFresh = r.Double(); s.GravesAt = r.Long(); s.GraveX = r.Float(); s.GraveY = r.Float();
            s.Muck = r.Double(); s.Spread = r.Double();
            s.Stubble = r.Double();
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
            h.Add(Works); h.Add(WorkBuild); Lore.Hash(ref h); h.Add(ScoutAt); h.Add(Tended); h.Add(HerdersOut); h.Add(ColdEvent); h.Add(FuelDay); h.Add(BestNets); h.Add(BestLoom); h.Add(BestQuern); h.Add(TechKnown); h.Add(BestPottery); h.Add(BestArd); h.Add(Fibre); h.Add(Hides); h.Add(BestMetal); h.Add(Metal); h.Add(Epidemic); h.Add(EpiPath); h.Add(EpiDead); h.Add(EpiStart); h.Add(InfNow); h.Add(InfPath); h.Add(InfPresent); h.Add(ReliefFrom); h.Add(ReliefAt); h.Add(Watcher); h.Add(WatchEvent); h.Add(Foe); h.Add(FoeWorth); h.Add(FoeEvent); h.Add(Wall); h.Add(WallKeep); h.Add(WallR); h.Add(WallCond); h.Add(WallEvent); h.Add(Lord); h.Add(LordSince); h.Add(LordEvent); h.Add(Tradition); h.Add(StreetN); h.Add(Street0); h.Add(Street1); h.Add(Street2); h.Add(Street3);
            if (AheadTo != 0) { h.Add(Ahead); h.Add(AheadFrom); h.Add(AheadTo); }   // (only DEEP sets them: other worlds hash as before)
            h.Add(Project); h.Add(ProjectKind); h.Add(Granaries); h.Add(Barns); h.Add(HarvestEvent); h.Add(WoodLot); h.Add(WoodDist); h.Add(WoodFar); h.Add(WoodLook); h.Add(StoneCell); h.Add(StoneDist); h.Add(StoneEvent); h.Add(Stone); h.Add(StoneWant); h.Add(Quarry); h.Add(Smithy); h.Add(SmithEvent); h.Add(Arms); h.Add(Ground); h.Add(BestFight); h.Add(Market); h.Add(TradeIn); h.Add(WallDry); h.Add(OldR); h.Add(OldCond); h.Add(OreCell); h.Add(OreDist); h.Add(OreEvent); h.Add(Ore); h.Add(OreWant); h.Add(Mine); h.Add(Charcoal); h.Add(CharcoalWant); h.Add(CharHearth); h.Add(ClampUntil); h.Add(BestIron); h.Add(Iron); h.Add(IronArms); h.Add(FeCell); h.Add(FeDist); h.Add(FeEvent); h.Add(FeOre); h.Add(FeOreWant); h.Add(FeMine); h.Add(IronSold); h.Add(ClaimDeed); h.Add(ClaimOn); h.Add(ClaimAt); Sea.Hash(ref h); h.Add(LandX); h.Add(LandY); h.Add(LandUx); h.Add(LandUy); h.Add(LandAt); h.Add(BestBoat); h.Add(BoatEvent); h.Add(Shed); h.Add(SeaP);
            h.Add(SquareX); h.Add(SquareY); h.Add(FieldX); h.Add(FieldY); h.Add(WoodX); h.Add(WoodY); h.Add(WellX); h.Add(WellY);
            h.Add(MarketR); h.Add(CoreR); h.Add(LanesShut);
            h.Add(Workshop); h.Add(LoomEvent); h.Add(Wares); h.Add(WaresHeld); h.Add(WaresOut); h.Add(WaresSold); h.Add(LoomBar); h.Add(LoomBarId);
            h.Add(Sheds); h.Add(ShedShort); h.Add(ShedMore);
            h.Add(Kiln); h.Add(PotEvent); h.Add(Pots); h.Add(Jars); h.Add(PotsOut); h.Add(PotsSold); h.Add(HearthShort); h.Add(PotBar); h.Add(PotBarId);
            h.Add(SmithBar); h.Add(SmithBarId); h.Add(Smithies); h.Add(SmithShort); h.Add(SmithMore);
            h.Add(Common);
            h.Add(Head); h.Add(HeadHh); h.Add(HeadSince); h.Add(HeadEvent);
            h.Add(SplitHh); h.Add(SplitCause); h.Add(SplitSince);
            h.Add(Tilth); h.Add(TilthDone); h.Add(TilthGain); h.Add(TilthEvent);
            h.Add(CropYield); h.Add(Reaped);
            h.Add(Tired); h.Add(TiredSeen); h.Add(Rest); h.Add(Grazed); h.Add(RestEvent);
            h.Add(Sheaves); h.Add(ThreshOut);
            h.Add(BurialCount); h.Add(Graves); h.Add(GravesFresh); h.Add(GravesAt); h.Add(GraveX); h.Add(GraveY);
            h.Add(Muck); h.Add(Spread);
            h.Add(Stubble);
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
        public double Ready;         // FU of food made ready: ground and cooked, what meals are eaten from (D2-056)
        public float Water;          // litres in the household's pots
        public float Indoor;         // how warm its home was today (C): the roof and the fire against the day's cold
        public float Gear;           // person-equivalents of clothes, tools and vessels in hand (D2-058)
        public int Building;         // the building it lives in (0: a camp; D2-059)
        public int Boat;             // its boat (a structure; 0 none; being made while not Done: C9.1)
        public byte Rebuild;         // rebuilding its house: 1 + the plan it builds (0 not; Build holds the hours put in: D2-114)
        public float Wares;          // of its goods, those that are wares from a weaver's shed (they wear slower: C3.1; never more than Gear)
        public float Pots;           // kg of kiln pots it cooks in (a household that cannot fire its own: C3.2)
        public byte CartKind;        // its cart: 1 a handcart, 2 an ox cart (C4.2, `Carts`)
        public byte CartState;       // 0 none, 1 wanted, 2 being made, 3 made
        public float CartHours;      // labour-hours into the one being made
        public long CartAt;          // when it was last wanted, begun or made (its years are counted from the making)
        public byte CartNeed;        // its traders' trips with a back's load too small, counted while they come within two years of each other
        public double Grain;         // FU of its own grain in its village's granary: its share of the harvests, its own produce, what it was repaid (C3.3, `Holdings`)
        public double WaresDue, PotsDue;   // its makers' make in the village's store not yet paid for: wares, kg of ware (C3.3b, `Holdings.Out`)
        public int Patron;           // the household that fed it in hard times (it follows it for some years: C10.2, `Society.Leaders`; 0 none)
        public long PatronAt;        // when
        public double Land;          // ha of its village's fields it holds (C3.6b, `Holdings` land: a founder's share, a portion, an inheritance, land taken up)
        public float Garden;         // m2 of its croft: the free ground behind and beside its home, measured when the year turns (C3.7a, `Gardens`)
        public float GardenWork;     // hours of the year's digging and weeding done in it (to what it wants)
        public float GardenFu;       // FU gathered from it this year
        public int GardenAt;         // the building its croft was last measured at (measured again when its home changes, else every fifth year)

        public void Write(BinWriter w) { w.Int(Id); w.Int(Settlement); w.Float(HomeX); w.Float(HomeY); w.Double(Larder); w.Long(Formed); w.Long(Dissolved); w.Int(FamilyName); w.Byte(Shelter); w.Float(Build); w.Int(Party); w.Double(Ready); w.Float(Water); w.Float(Indoor); w.Float(Gear); w.Int(Building); w.Int(Boat); w.Byte(Rebuild); w.Float(Wares); w.Float(Pots); w.Byte(CartKind); w.Byte(CartState); w.Float(CartHours); w.Long(CartAt); w.Byte(CartNeed); w.Double(Grain); w.Double(WaresDue); w.Double(PotsDue); w.Int(Patron); w.Long(PatronAt); w.Double(Land); w.Float(Garden); w.Float(GardenWork); w.Float(GardenFu); w.Int(GardenAt); }
        public static Household Read(BinReader r) => new Household { Id = r.Int(), Settlement = r.Int(), HomeX = r.Float(), HomeY = r.Float(), Larder = r.Double(), Formed = r.Long(), Dissolved = r.Long(), FamilyName = r.Int(), Shelter = r.Byte(), Build = r.Float(), Party = r.Int(), Ready = r.Double(), Water = r.Float(), Indoor = r.Float(), Gear = r.Float(), Building = r.Int(), Boat = r.Int(), Rebuild = r.Byte(), Wares = r.Float(), Pots = r.Float(), CartKind = r.Byte(), CartState = r.Byte(), CartHours = r.Float(), CartAt = r.Long(), CartNeed = r.Byte(), Grain = r.Double(), WaresDue = r.Double(), PotsDue = r.Double(), Patron = r.Int(), PatronAt = r.Long(), Land = r.Double(), Garden = r.Float(), GardenWork = r.Float(), GardenFu = r.Float(), GardenAt = r.Int() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Settlement); h.Add(HomeX); h.Add(HomeY); h.Add(Larder); h.Add(Formed); h.Add(Dissolved); h.Add(FamilyName); h.Add(Shelter); h.Add(Build); h.Add(Party); h.Add(Ready); h.Add(Water); h.Add(Indoor); h.Add(Gear); h.Add(Building); h.Add(Boat); h.Add(Rebuild); h.Add(Wares); h.Add(Pots); h.Add(CartKind); h.Add(CartState); h.Add(CartHours); h.Add(CartAt); h.Add(CartNeed); h.Add(Grain); h.Add(WaresDue); h.Add(PotsDue); h.Add(Patron); h.Add(PatronAt); h.Add(Land); h.Add(Garden); h.Add(GardenWork); h.Add(GardenFu); h.Add(GardenAt); }
    }
}
