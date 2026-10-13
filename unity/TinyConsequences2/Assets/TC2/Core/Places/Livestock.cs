using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>A household's animal (D2-048): an individual with a body, a mother, an owner. State: saved.</summary>
    public struct Beast
    {
        public int Id;
        public byte Kind;          // Livestock.Cattle, Goat, Pig
        public byte Sex;           // Agents.Sex
        public bool Alive;
        public long Born, Died;
        public int Mother;
        public int Owner;          // household
        public float Cond;         // body condition 0..1 (how well it has eaten)
        public long Due;           // a pregnancy's end (0 none)
        public long MilkUntil;     // in milk until (0 none)

        public void Write(BinWriter w) { w.Int(Id); w.Byte(Kind); w.Byte(Sex); w.Bool(Alive); w.Long(Born); w.Long(Died); w.Int(Mother); w.Int(Owner); w.Float(Cond); w.Long(Due); w.Long(MilkUntil); }
        public static Beast Read(BinReader r) => new Beast { Id = r.Int(), Kind = r.Byte(), Sex = r.Byte(), Alive = r.Bool(), Born = r.Long(), Died = r.Long(), Mother = r.Int(), Owner = r.Int(), Cond = r.Float(), Due = r.Long(), MilkUntil = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Kind); h.Add(Sex); h.Add(Alive); h.Add(Born); h.Add(Died); h.Add(Mother); h.Add(Owner); h.Add(Cond); h.Add(Due); h.Add(MilkUntil); }

        public double AgeYears(long now) => (now - Born) / (double)SimTime.Year;
    }

    /// <summary>
    /// Livestock (D2-048): cattle, goats and pigs, each animal an individual owned by a household. Once a day, with its village:
    /// the herds graze the village's pasture - the same grass the wild grazers live on, taken out of `P.Grass` (further out when
    /// the near pasture is short) - pigs root for the same mast the boar and the gatherers find (`P.Gathered`) and live on the
    /// village's scraps; they eat what is there (lean in winter, short when too many share it), their condition follows, they
    /// breed in their season when well fed and a sire is near (their village's or a neighbour's), give milk while their young are
    /// small, grow old and die; wolves take some where wolves are. Households keep their herds to what their hands can tend and
    /// what the land offers (the autumn slaughter as the grass grows lean), keeping a breeding core of each kind; they kill the
    /// grown males beyond a sire and his successor, the old and the failing, and more of their herd when their larders run empty.
    /// Milk and meat come home like the hunters' catch. A child marrying out takes a share of the family's herd; a household's
    /// animals pass to the living when it ends, go where it goes, and go wild when their village is abandoned. Intake follows the
    /// wild animals' law (Ecology: metabolism x mass^0.75 a year); meat is valued as game is (WildFood).
    /// </summary>
    public sealed class Livestock
    {
        public const byte Cattle = 0, Goat = 1, Pig = 2, Kinds = 3;

        public struct KindLaw
        {
            public string Name;
            public double Mass;        // kg grown
            public double GrassShare;  // of its food, the share that is grass (grazed from the pasture)
            public double MastShare;   // the share that is mast and roots (rooted in the woods: pigs)
            public double ScrapShare;  // the share that is the household's scraps and the harvest's chaff and bran (pigs)
            public double Gestation;   // days
            public double Litter;      // young at a birth (mean)
            public double Lifespan;    // years
            public double Mature;      // years to breed
            public double MilkFu;      // FU of milk a day in milk (what the household takes, the young's share left)
            public double MilkDays;    // days in milk after a birth
            public double Season;      // year fraction when they come into season (spring), as the wild animals breed
            public byte WildKind;      // the wild kind its animals join when they go feral (Ecology template kinds)
            public int Core;           // females a household keeps to breed whatever the pasture (the youngest in their prime first)
        }

        public static readonly KindLaw[] Laws =
        {
            // goats browse a fifth of their food from scrub and woods (plenty); a pig roots for mast and lives on the village's scraps
            new KindLaw { Name = "cattle", Mass = 350, GrassShare = 1.0,  MastShare = 0.0,  Gestation = 36, Litter = 1.0, Lifespan = 18, Mature = 2.5, MilkFu = 0.9,  MilkDays = 24, Season = 0.12, WildKind = 2, Core = 3 },
            new KindLaw { Name = "goat",   Mass = 45,  GrassShare = 0.8,  MastShare = 0.0,  Gestation = 18, Litter = 1.6, Lifespan = 12, Mature = 1.0, MilkFu = 0.25, MilkDays = 18, Season = 0.12, WildKind = 4, Core = 4 },
            new KindLaw { Name = "pig",    Mass = 90,  GrassShare = 0.3,  MastShare = 0.4,  ScrapShare = 0.3, Gestation = 14, Litter = 6.0, Lifespan = 9,  Mature = 0.8, MilkFu = 0.0,  MilkDays = 8,  Season = 0.12, WildKind = 3, Core = 2 },
        };

        static readonly double[] _intake = { IntakeOf(Cattle), IntakeOf(Goat), IntakeOf(Pig) };

        /// <summary>Food a grown animal eats a day (kg, dry): the wild animals' law, a year's intake over the year's days.</summary>
        static double IntakeOf(byte kind) => Ecology.LandMetab * DMath.Exp(0.75 * DMath.Log(Laws[kind].Mass)) / SimTime.DaysPerYear;
        public static double IntakeDay(byte kind) => _intake[kind];

        public const double PastureReach = 900;   // m: where a village's herds graze and root (beyond its fields)
        public const double FarReach = 1500;      // m: how far the herders drive them when the near pasture falls short
        public const double SireReach = 3500;     // m: a neighbouring village whose sire serves a herd that has none
        public const double MastFind = 1.0 / 6.0; // of the mast left in reach, what the pigs can find in a day
        public const double ScrapsKg = 0.4;       // scraps, chaff and bran a day for every person in the village (kg, as pig food)
        public const double FailingCond = 0.45;   // herders kill an animal this thin (better eaten than starved)
        public const double StockShare = 0.6;     // herders keep what their herds eat to this share of what the land offers a day
        public const int CoreMales = 2;           // males of a kind a household keeps: the sire and his successor
        public const double TendKg = 50;          // food a day (kg) of the animals one pair of grown hands can herd, milk and pen

        World _w;
        readonly List<int> _cells = new List<int>(), _herd = new List<int>(), _hhs = new List<int>(), _ground = new List<int>();
        readonly List<double> _hhG = new List<double>(), _hhM = new List<double>(), _hhAll = new List<double>(), _hhCap = new List<double>();
        readonly List<byte> _hhOut = new List<byte>();   // per household and mode (bits 1, 2, 4): nothing more it would spare
        readonly int[] _kept = new int[Kinds], _males = new int[Kinds];
        readonly int[] _prime = new int[Kinds], _young = new int[Kinds], _seenP = new int[Kinds], _seenY = new int[Kinds], _seenA = new int[Kinds];
        readonly int[] _spares = new int[Kinds], _pickOf = new int[Kinds];
        readonly double[] _pickEats = new double[Kinds];
        readonly bool[] _pickMale = new bool[Kinds];
        readonly sbyte[] _sireNear = new sbyte[Kinds];   // today's answer per kind: -1 not yet asked, 0 none, 1 a sire near
        double _season;   // the season's phase today (the grass's leanness reads the day's mean temperature)

        /// <summary>Tools only (null in the game): per village id, the last day's grass offered and wanted, mast offered and wanted (kg).</summary>
        public static double[] Diag;

        /// <summary>Tools only (null in the game): ends by kind (x Causes) - see CauseNames - and births by kind at [Kinds * Causes + kind].</summary>
        public static long[] DiagEnds;
        public static readonly string[] CauseNames = { "age", "wolves", "hunger", "spare sire", "old", "failing", "eaten hungry", "hands", "pasture", "mast" };
        public const int Causes = 10;
        static void Count(byte kind, int cause) { if (DiagEnds != null) DiagEnds[kind * Causes + cause]++; }

        public void Bind(World w) { _w = w; }

        /// <summary>An animal is born to a household (worldgen, births). Returns its id.</summary>
        public int Add(byte kind, byte sex, long born, int mother, int owner, float cond = 0.8f)
        {
            var w = _w;
            int id = w.Ids.Next(EntityKind.Beast);
            w.Beasts.Add(id, new Beast { Id = id, Kind = kind, Sex = sex, Alive = true, Born = born, Mother = mother, Owner = owner, Cond = cond });
            w.BeastsOf(owner).InsertSorted(id);
            return id;
        }

        /// <summary>
        /// The live animals at a village (households in id order, each one's animals in id order). A household on the road has
        /// its animals with it: they are not on this pasture. Reads only.
        /// </summary>
        void HerdOf(int sid, List<int> into)
        {
            var w = _w;
            into.Clear();
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                if (w.Households.Get(hid).Party != 0) continue;
                var mine = w.BeastsOf(hid);
                for (int j = 0; j < mine.Count; j++) into.Add(mine[j]);
            }
        }

        /// <summary>
        /// A household with nobody left alive leaves its animals to the village's household with people that has the fewest
        /// (the lowest id among equals): no herd is left unkept.
        /// </summary>
        void Settle(int sid)
        {
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                if (w.BeastsOf(hid).Count == 0 || w.Members(hid).Count > 0) continue;
                int heir = 0, least = int.MaxValue;
                for (int j = 0; j < hhs.Count; j++)
                {
                    int o = hhs[j];
                    if (o == hid || w.Members(o).Count == 0) continue;
                    int has = w.BeastsOf(o).Count;
                    if (has < least) { least = has; heir = o; }
                }
                if (heir != 0) Inherit(hid, heir);
            }
        }

        public const int PerHerder = 60;   // head of cattle and goats one herder keeps together on the pasture

        /// <summary>
        /// The village herd's grazing ground on a day (D2-050): one of the ten nearest cells of the village's pasture (open land in
        /// reach, no fields, no water, beyond the houses), a different one from day to day by a key of the village and the day -
        /// where its herders take the households' cattle and goats together and where the view draws them. -1 if the village has
        /// no pasture. Reads only.
        /// </summary>
        public int GroundOf(int sid, long day)
        {
            var w = _w; var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            if (!Stubble.Off && s.Crop == CropStage.Harvested && s.Stubble > 0.25 * Stubble.PerHa * s.FieldHa)
            {
                // (C3.7d) from the harvest to the sowing the herds go out onto the village's own stubble while it lasts: one of its field cells a day
                // (beyond the houses and on dry ground, as the waste's grounds are)
                var fc = w.FieldCellsOf(sid);
                _ground.Clear();
                for (int i = 0; i < fc.Count; i++)
                {
                    int c = fc[i];
                    if (!P.IsLand(c) || P.LakeDepth[c] > 0.3f || Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius) < 160.0) continue;
                    _ground.Add(c);
                }
                if (_ground.Count > 0) return _ground[(int)(KeyedRng.Unit(KeyedRng.Mix((ulong)sid * 0x9E3779B97F4A7C15UL + (ulong)day * 0xC2B2AE3D27D4EB4FUL + 0x57B8)) * _ground.Count) % _ground.Count];
            }
            w.Hazards.CellsWithin(w, s.Cell, PastureReach, _ground);
            int n = 0;
            for (int i = 0; i < _ground.Count && n < 10; i++)
            {
                int c = _ground[i];
                if (!Grazes(P, c) || P.LakeDepth[c] > 0.3f || Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius) < 160.0) continue;
                _ground[n++] = c;
            }
            if (n == 0) return -1;
            return _ground[(int)(KeyedRng.Unit(KeyedRng.Mix((ulong)sid * 0x9E3779B97F4A7C15UL + (ulong)day * 0xC2B2AE3D27D4EB4FUL + 0x5EED)) * n) % n];
        }

        /// <summary>The village's cattle and goats that go out to graze (pigs keep to the yards; a household on the road has its herd with it).</summary>
        public int Grazers(int sid)
        {
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            int n = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                if (w.Households.Get(hhs[i]).Party != 0) continue;
                var mine = w.BeastsOf(hhs[i]);
                for (int j = 0; j < mine.Count; j++) if (w.Beasts.Get(mine[j]).Kind != Pig) n++;
            }
            return n;
        }

        /// <summary>Herders the village's herd wants a day: one for every sixty head (none without grazers, none in a hard frost).</summary>
        public int HerdersWanted(int sid)
        {
            if (Frost(sid, _w.Now)) return 0;
            int g = Grazers(sid);
            return g == 0 ? 0 : (g + PerHerder - 1) / PerHerder;
        }

        /// <summary>A hard frost at a village on a day (the day's mean below -4 C): the animals stay in their pens.</summary>
        public bool Frost(int sid, long t) => _w.PlanetSys.DayMean(_w.Settlements.Get(sid).Cell, PlanetSystems.Season(t)) < -4.0;

        /// <summary>
        /// The chance a day that wolves round the pasture take an animal: by their numbers, the young three times as exposed,
        /// grown cattle a fifth; a herd someone watched the day before a third as exposed (D2-050).
        /// </summary>
        public static double WolfRisk(double wolves, double age, byte kind, bool watched) =>
            wolves * 0.00025 * (age < 1 ? 3.0 : 1.0) * (kind == Cattle && age >= 1 ? 0.2 : 1.0) * (watched ? 0.35 : 1.0);

        public static double GrownShare(in Beast b, long now) => DMath.Clamp(0.25 + 0.75 * b.AgeYears(now) / Laws[b.Kind].Mature, 0.25, 1.0);

        static bool Grazes(PlanetState P, int c) => P.IsLand(c) && P.FieldOwner[c] == 0 && P.Ice[c] <= 5f;

        /// <summary>
        /// The share of the standing grass above its floor the herds can graze in a day: lean in the cold, as the wild grazers
        /// find it (the day's mean temperature: the village's day turns at midnight, in the night's chill).
        /// </summary>
        double GrazeShare(int c) => 0.004 + 0.016 * DMath.SmoothStep(-2.0, 8.0, _w.PlanetSys.DayMean(c, _season));

        /// <summary>What the pasture in `_cells` offers the herds today (kg of grass) and the mast left in its woods (kg).</summary>
        void Offer(out double grass, out double mast)
        {
            var P = _w.Planet;
            grass = 0; mast = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                int c = _cells[i];
                if (Grazes(P, c)) grass += DMath.Max(0.0, P.Grass[c] - Ecology.GrassFloor) * Ecology.GrassMax * P.AreaHa(c) * GrazeShare(c);
                mast += WildFood.MastLeft(P, c);
            }
        }

        /// <summary>
        /// What the village's own fields offer its herds today (C3.7d, kg of grass): the stubble's day share from the harvest's end to the sowing, and
        /// the sward of the arable it rests this year (its fallow hectares at the field cells' own grass, as lean in the cold as the waste's). None on a
        /// day of hard frost (the beasts are penned) or with `--nostubble`.
        /// </summary>
        void FieldOffer(int sid, in Settlement s, out double stubble, out double fallow)
        {
            stubble = 0; fallow = 0;
            if (Stubble.Off || Frost(sid, _w.Now)) return;
            var P = _w.Planet;
            if (s.Crop == CropStage.Harvested && s.Stubble > 0) stubble = s.Stubble * Stubble.DayShare;
            double restHa = DMath.Max(0.0, s.ArableHa - s.FieldHa);
            if (restHa <= 0) return;
            var fc = _w.FieldCellsOf(sid);
            double area = 0, sward = 0;
            for (int i = 0; i < fc.Count; i++)
            {
                int c = fc[i]; double a = P.AreaHa(c);
                area += a; sward += a * DMath.Max(0.0, P.Grass[c] - Ecology.GrassFloor) * Ecology.GrassMax * GrazeShare(c);
            }
            if (area > 0) fallow = restHa * sward / area;
        }

        /// <summary>The fallow's grass grazed (C3.7d): `kg` taken from the rested arable of the village's field cells, each by its share of the sward -
        /// a cell's grass is its whole ground's, so it falls by the fallow's part of the cell only.</summary>
        void GrazeFallow(int sid, in Settlement s, double kg, double fallowOffer)
        {
            if (kg <= 0 || fallowOffer <= 0) return;
            var P = _w.Planet;
            var fc = _w.FieldCellsOf(sid);
            double area = 0;
            for (int i = 0; i < fc.Count; i++) area += P.AreaHa(fc[i]);
            if (area <= 0) return;
            double part = DMath.Min(1.0, DMath.Max(0.0, s.ArableHa - s.FieldHa) / area);
            double share = DMath.Min(1.0, kg / fallowOffer);
            for (int i = 0; i < fc.Count; i++)
            {
                int c = fc[i];
                double take = DMath.Max(0.0, P.Grass[c] - Ecology.GrassFloor) * share * GrazeShare(c) * part;
                P.Grass[c] = (float)DMath.Max(Ecology.GrassFloor * 0.5, P.Grass[c] - take);
            }
        }

        /// <summary>A village's herds live a day (both resolutions; SettlementSystem.Daily).</summary>
        public void Day(int sid, int days = 1)
        {
            var w = _w; var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            if (s.Population <= 0) return;   // nobody left to keep them: the village's end leaves them to the land (Feral)
            Settle(sid);
            HerdOf(sid, _herd);
            if (_herd.Count == 0) return;
            long now = w.Now;
            ulong seed = w.Seed;
            long day = SimTime.FloorDiv(now, SimTime.Day);
            _season = PlanetSystems.Season(days == 1 ? now : now + days / 2 * SimTime.Day);   // (DEEP: the step's middle day)
            // 1. grazing and rooting: the pasture is the open land within reach not under the village's fields; what the herds
            //    eat comes out of its grass, and what the pigs root out of the woods' mast - the wild find that much less
            double wantG = 0, wantM = 0, wantS = 0;
            for (int i = 0; i < _herd.Count; i++)
            {
                ref var b = ref w.Beasts.Get(_herd[i]);
                var k = Laws[b.Kind];
                double eat = _intake[b.Kind] * GrownShare(in b, now);
                wantG += eat * k.GrassShare; wantM += eat * k.MastShare; wantS += eat * k.ScrapShare;
            }
            double fedS = wantS > 0 ? DMath.Min(1.0, ScrapsKg * s.Population / wantS) : 1.0;
            w.Hazards.CellsWithin(w, s.Cell, PastureReach, _cells);
            Offer(out double offer, out double mast);
            FieldOffer(sid, in s, out double stubOffer, out double fallowOffer);   // (their own fields first: the stubble, the fallow - C3.7d)
            double fieldOffer = stubOffer + fallowOffer;
            if (wantG > StockShare * (offer + fieldOffer) || wantM > StockShare * mast * MastFind)
            {
                // the near pasture is short: the herders take the herds further out (onto land the neighbours' herds may graze too)
                w.Hazards.CellsWithin(w, s.Cell, FarReach, _cells);
                Offer(out offer, out mast);
            }
            double fedG = wantG > 0 ? DMath.Min(1.0, (offer + fieldOffer) / wantG) : 1.0;
            double fedM = wantM > 0 ? DMath.Min(1.0, mast * MastFind / wantM) : 1.0;
            if (Diag != null && sid * 4 + 3 < Diag.Length) { Diag[sid * 4] = offer; Diag[sid * 4 + 1] = wantG; Diag[sid * 4 + 2] = mast * MastFind; Diag[sid * 4 + 3] = wantM; }
            double eatenG = wantG * fedG, eatenM = wantM * fedM;
            double onStub = DMath.Min(eatenG, stubOffer), onFallow = DMath.Min(eatenG - onStub, fallowOffer), onWaste = eatenG - onStub - onFallow;
            if (onStub > 0) s.Stubble = DMath.Max(0.0, s.Stubble - onStub * days);
            if (onFallow > 0) GrazeFallow(sid, in s, onFallow * days, fallowOffer * days);
            Stubble.Dung(ref s, (onStub + onFallow) * days);   // (the day's dung dropped on the fields themselves: C3.7d)
            if (Stubble.Tally != null) { Stubble.Tally[0] += onStub * days; Stubble.Tally[1] += onFallow * days; }
            s.Grazed += eatenG * days;   // (their dung comes back to the fields: C3.6f)
            Muck.Make(ref s, eatenG * days, Frost(sid, now));   // (the night's in the yards' heaps - a day's in a hard frost, penned: C3.7c)
            if (onWaste > 0 && offer > 0)
            {
                // (DEEP: the step's grazing - each cell gives its day's share for each day; capped at one day's offer, a step of twelve
                // days grazed less than two, the pastures stayed lush and the cattle tripled: F8 calibration)
                double share = days == 1 ? onWaste / offer : onWaste * days / offer;
                for (int i = 0; i < _cells.Count; i++)
                {
                    int c = _cells[i];
                    if (!Grazes(P, c)) continue;
                    double take = DMath.Max(0.0, P.Grass[c] - Ecology.GrassFloor) * share * GrazeShare(c);
                    P.Grass[c] = (float)DMath.Max(Ecology.GrassFloor * 0.5, P.Grass[c] - take);
                }
            }
            // (DEEP: the pigs root day by day - each day what they find of what is left, a sixth at most, the woods emptying as they go
            // and their fill with them; a step's days at the first day's find took up to twice the woods, left them bare, and the herders
            // culled the pigs to their core each step: F8 calibration)
            double rootedM = eatenM;
            if (days > 1 && wantM > 0)
            {
                double m = mast, sum = 0, fill = 0;
                for (int d = 0; d < days; d++) { double f = DMath.Min(1.0, m * MastFind / wantM); sum += wantM * f; fill += f; m -= wantM * f; }
                rootedM = sum; fedM = fill / days;
            }
            if (rootedM > 0 && mast > 0)
                for (int i = 0; i < _cells.Count; i++)
                {
                    int c = _cells[i];
                    double left = WildFood.MastLeft(P, c);
                    if (left > 0) P.Gathered[c] = (float)(P.Gathered[c] + (days == 1 ? DMath.Min(left, eatenM * days * left / mast) : DMath.Min(left, rootedM * left / mast)));
                }
            // wolves round the pasture take the weak and the young now and then
            double wolves = 0;
            for (int q = 0; q < P.Species.Count; q++)
            {
                if (P.Species[q].Habitat != 0 || P.Species[q].Kind != 5 || P.Species[q].Gone != 0) continue;
                for (int i = 0; i < _cells.Count; i++) wolves += P.Abund[q * P.Cells + _cells[i]];
            }
            // 2. each animal's day
            for (int k = 0; k < Kinds; k++) _sireNear[k] = -1;
            int today = (int)SimTime.FloorDiv(now + w.LocalOffsetOf(sid), SimTime.Day);
            bool watched = (s.Tended >= today - 1 && s.HerdersOut > 0) || Frost(sid, now - SimTime.Day);   // its herders yesterday, or penned
            for (int i = 0; i < _herd.Count; i++)
            {
                int id = _herd[i];
                ref var b = ref w.Beasts.Get(id);
                if (!b.Alive) continue;
                var k = Laws[b.Kind];
                double age = b.AgeYears(now);
                // condition follows how well it ate (a goat's browse is never short)
                double ate = k.GrassShare * fedG + k.MastShare * fedM + k.ScrapShare * fedS + (1.0 - k.GrassShare - k.MastShare - k.ScrapShare);
                if (days > 1) { DeepBeast(id, sid, ate, age, WolfRisk(wolves, age, b.Kind, watched), day, days); continue; }
                b.Cond = (float)DMath.Clamp01(b.Cond + (ate - 0.8) * 0.06);
                // old age, hunger, wolves
                double wolf = WolfRisk(wolves, age, b.Kind, watched);
                double hazard = (1.0 / k.Lifespan) * (age > k.Lifespan * 0.7 ? 3.0 : 0.6) / SimTime.DaysPerYear + wolf;
                if (b.Cond < 0.3) hazard += (0.3 - b.Cond) * 0.08;
                double u = KeyedRng.Unit(seed, RngStream.Death, (ulong)id | (1UL << 40), (ulong)day);
                if (u < hazard) { Count(b.Kind, u < wolf ? 1 : b.Cond < 0.3 && u >= hazard - (0.3 - b.Cond) * 0.08 ? 2 : 0); Die(ref b, u < wolf); continue; }
                // birth
                if (b.Due != 0 && now >= b.Due) Birth(ref b);
                // milk for the household while the young are small
                if (b.MilkUntil > now && k.MilkFu > 0) Store(b.Owner, sid, k.MilkFu * DMath.Clamp01(b.Cond / 0.6));
                // the season: a grown, well-fed female with a grown male of her kind in the village conceives
                if (b.Sex == Sex.Female && b.Due == 0 && age >= k.Mature && b.Cond > 0.55)
                {
                    double yf = SimTime.YearFraction(now + w.LocalOffsetOf(sid));
                    double season = s.Anchor.Y >= 0 ? yf : (yf + 0.5) % 1.0;
                    if (season >= k.Season && season < k.Season + 0.25 && SireNear(sid, b.Kind) &&
                        KeyedRng.Unit(seed, RngStream.Birth, (ulong)id | (1UL << 40), (ulong)day) < 0.08 * b.Cond)
                        b.Due = now + DMath.ToLong(k.Gestation * SimTime.Day);
                }
            }
            // 3. the households keep their herds to what their hands can tend and the land feeds (with grass to spare: a pasture
            //    grazed to its offer every day wears thin), and eat from them when hungry
            // (DEEP: to the pasture as the step leaves it - days lived keep the herds to the grass at its leanest, before it grows
            // again, not to the grass as it stood before the step's grazing, which kept herds half as large again: F8 calibration)
            if (days > 1) { Offer(out offer, out mast); FieldOffer(sid, in w.Settlements.Get(sid), out stubOffer, out fallowOffer); fieldOffer = stubOffer + fallowOffer; }
            Destock(sid, StockShare * (offer + fieldOffer), StockShare * mast * MastFind);
            Cull(sid);
        }

        /// <summary>
        /// DEEP (F8): the share of the mast within the village's herds' reach still left on the step's middle day, its pigs having
        /// rooted day by day until then - the same reckoning as the step's rooting (read before the step's work, to see the woods as
        /// its middle day finds them). 1 when nothing roots.
        /// </summary>
        public double MidStepMast(int sid, int days)
        {
            Rooting(sid, out double wantM, out double mast);
            if (wantM <= 0 || mast <= 0) return 1.0;
            double half = (days - 1) * 0.5, m = mast;
            for (int d = 0; d < (int)half; d++) m -= wantM * DMath.Min(1.0, m * MastFind / wantM);
            m -= (half - (int)half) * wantM * DMath.Min(1.0, m * MastFind / wantM);
            return DMath.Clamp01(m / mast);
        }

        /// <summary>DEEP (F8): what the village's herds would root out of the woods in a day at the full (kg of mast), and the mast left within their reach.</summary>
        public void Rooting(int sid, out double perDay, out double inReach)
        {
            perDay = 0; inReach = 0;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.Population <= 0) return;
            HerdOf(sid, _herd);
            if (_herd.Count == 0) return;
            long now = w.Now;
            double wantG = 0, wantM = 0;
            for (int i = 0; i < _herd.Count; i++)
            {
                ref var b = ref w.Beasts.Get(_herd[i]);
                double eat = _intake[b.Kind] * GrownShare(in b, now);
                wantG += eat * Laws[b.Kind].GrassShare; wantM += eat * Laws[b.Kind].MastShare;
            }
            if (wantM <= 0) return;
            _season = PlanetSystems.Season(now);
            w.Hazards.CellsWithin(w, s.Cell, PastureReach, _cells);
            Offer(out double offer, out double mast);
            FieldOffer(sid, in s, out double stubOffer, out double fallowOffer);   // (as the day weighs it: C3.7d)
            if (wantG > StockShare * (offer + stubOffer + fallowOffer) || wantM > StockShare * mast * MastFind) { w.Hazards.CellsWithin(w, s.Cell, FarReach, _cells); Offer(out offer, out mast); }
            perDay = wantM;
            inReach = mast;
        }

        /// <summary>
        /// DEEP (F8): an animal's step of several days, lived day by day where the days differ - its condition day by day at the
        /// step's feeding, each day's hazard on that day's condition, milk on the days in milk, and conception on the days of the
        /// step in its kind's season, each with that day's condition (the day it conceives found from one draw, so it is born when
        /// its time comes). Taking the step's change of condition first and the season at the step's start made the kinds that
        /// recover in spring (cattle on the new grass) breed three times as often as days lived, and pigs a fifth (F8 calibration).
        /// </summary>
        void DeepBeast(int id, int sid, double ate, double age, double wolf, long day, int days)
        {
            var w = _w;
            ref var b = ref w.Beasts.Get(id);
            var k = Laws[b.Kind];
            long now = w.Now;
            ulong seed = w.Seed;
            double dc = (ate - 0.8) * 0.06;
            double old = (1.0 / k.Lifespan) * (age > k.Lifespan * 0.7 ? 3.0 : 0.6) / SimTime.DaysPerYear;
            // old age, hunger, wolves: the step's survival, each day on its condition
            double c = b.Cond, keep = 1.0, keepFed = 1.0;
            for (int j = 0; j < days; j++)
            {
                c = DMath.Clamp01(c + dc);
                keep *= 1.0 - (old + wolf + (c < 0.3 ? (0.3 - c) * 0.08 : 0.0));
                keepFed *= 1.0 - (old + wolf);
            }
            double u = KeyedRng.Unit(seed, RngStream.Death, (ulong)id | (1UL << 40), (ulong)day);
            if (u < 1.0 - keep)
            {
                double wolfAll = SettlementSystem.Over(wolf, days);
                Count(b.Kind, u < wolfAll ? 1 : u >= 1.0 - keepFed ? 2 : 0);
                Die(ref b, u < wolfAll);
                return;
            }
            // born within the step: at its start
            if (b.Due != 0 && now + (days - 1) * SimTime.Day >= b.Due) Birth(ref b);
            long off = w.LocalOffsetOf(sid);
            bool south = w.Settlements.Get(sid).Anchor.Y < 0;
            double ub = KeyedRng.Unit(seed, RngStream.Birth, (ulong)id | (1UL << 40), (ulong)day);
            double milk = 0, open = 1.0;
            c = b.Cond;
            for (int j = 0; j < days; j++)
            {
                c = DMath.Clamp01(c + dc);
                long t = now + j * SimTime.Day;
                if (b.MilkUntil > t) milk += DMath.Clamp01(c / 0.6);
                if (b.Sex != Sex.Female || b.Due != 0 || c <= 0.55 || age + j / (double)SimTime.DaysPerYear < k.Mature) continue;
                double yf = SimTime.YearFraction(t + off);
                double season = south ? (yf + 0.5) % 1.0 : yf;
                if (season < k.Season || season >= k.Season + 0.25 || !SireNear(sid, b.Kind)) continue;
                open *= 1.0 - 0.08 * c;
                if (ub < 1.0 - open) b.Due = t + DMath.ToLong(k.Gestation * SimTime.Day);
            }
            b.Cond = (float)c;
            if (milk > 0 && k.MilkFu > 0) Store(b.Owner, sid, k.MilkFu * milk);
        }

        /// <summary>
        /// The herders slaughter what they cannot keep (never their breeding core; the meat comes home): a household whose herd
        /// is more than its hands can tend kills its biggest eaters beyond the core; when the herds eat more than the herders
        /// would let the pasture (or the woods' mast) give, whoever grazes most for their number kills first, the biggest eater of
        /// what is short. As the grass grows lean in autumn this is the autumn slaughter.
        /// </summary>
        void Destock(int sid, double offerG, double offerM)
        {
            var w = _w;
            long now = w.Now;
            var hhs = w.HouseholdsOf(sid);
            _hhs.Clear(); _hhG.Clear(); _hhM.Clear(); _hhAll.Clear(); _hhCap.Clear(); _hhOut.Clear();
            double wantG = 0, wantM = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                var mine = w.BeastsOf(hid);
                if (mine.Count == 0 || w.Households.Get(hid).Party != 0) continue;
                double g = 0, m = 0, all = 0;
                for (int j = 0; j < mine.Count; j++)
                {
                    ref var b = ref w.Beasts.Get(mine[j]);
                    double eat = _intake[b.Kind] * GrownShare(in b, now);
                    g += eat * Laws[b.Kind].GrassShare; m += eat * Laws[b.Kind].MastShare; all += eat;
                }
                _hhs.Add(hid); _hhG.Add(g); _hhM.Add(m); _hhAll.Add(all); _hhCap.Add(Hands(hid) * TendKg); _hhOut.Add(0);
                wantG += g; wantM += m;
            }
            for (int guard = 0; guard < 100000; guard++)
            {
                // more than its hands can tend (the household most over first): its biggest eater beyond the core
                int pick = -1, mode = 0, bid = 0;
                while (true)
                {
                    double most = 1.0; pick = -1;
                    for (int i = 0; i < _hhs.Count; i++)
                    {
                        if ((_hhOut[i] & 1) != 0) continue;
                        double over = _hhAll[i] / DMath.Max(1e-9, _hhCap[i]);   // (all it eats: the work is the whole herd's)
                        if (over > most) { most = over; pick = i; }
                    }
                    if (pick < 0) break;
                    bid = Spare(_hhs[pick], 0);
                    if (bid != 0) break;
                    _hhOut[pick] |= 1;   // (nothing it would spare: it stays so while others kill)
                }
                if (pick < 0)
                {
                    // the food that is shorter decides what is killed (any grazer for the grass, pigs for the mast)
                    double rg = offerG > 0 ? wantG / offerG : (wantG > 0 ? double.MaxValue : 0);
                    double rm = offerM > 0 ? wantM / offerM : (wantM > 0 ? double.MaxValue : 0);
                    if (rg <= 1.0 && rm <= 1.0) break;
                    mode = rm > rg ? 2 : 1;
                    byte bit = (byte)(mode == 2 ? 4 : 2);
                    while (true)
                    {
                        double most = 0; pick = -1;
                        for (int i = 0; i < _hhs.Count; i++)
                        {
                            if ((_hhOut[i] & bit) != 0) continue;
                            int people = w.Members(_hhs[i]).Count;
                            double per = (mode == 2 ? _hhM[i] : _hhG[i]) / Math.Max(1, people);
                            if (per > most) { most = per; pick = i; }
                        }
                        if (pick < 0) break;
                        bid = Spare(_hhs[pick], mode);
                        if (bid != 0) break;
                        _hhOut[pick] |= bit;
                    }
                }
                if (pick < 0) break;   // nothing more anyone would spare
                int hid = _hhs[pick];
                ref var be = ref w.Beasts.Get(bid);
                double e = _intake[be.Kind] * GrownShare(in be, now);
                double dg = e * Laws[be.Kind].GrassShare, dm = e * Laws[be.Kind].MastShare;
                _hhG[pick] -= dg; _hhM[pick] -= dm; _hhAll[pick] -= e; wantG -= dg; wantM -= dm;
                Count(be.Kind, mode == 0 ? 7 : mode == 1 ? 8 : 9);
                Slaughter(ref be, hid, sid);
            }
            HardTimes(sid, offerG / StockShare, offerM / StockShare, ref wantG, ref wantM);
        }

        /// <summary>
        /// Hard times: the land cannot feed even the breeding cores (a pasture lost to fields, fire or drought) - the herders kill
        /// into them rather than watch the herd starve: from the kind the village has most of, the most grown, until the herd eats
        /// no more than the day's whole offer, keeping a nucleus of each kind in the village (a sire and two females) to breed
        /// again when the land allows.
        /// </summary>
        void HardTimes(int sid, double offerG, double offerM, ref double wantG, ref double wantM)
        {
            var w = _w;
            long now = w.Now;
            if (wantG <= offerG && wantM <= offerM) return;
            for (int k = 0; k < Kinds; k++) { _kept[k] = 0; _males[k] = 0; }   // the village's females and males of each kind
            HerdOf(sid, _herd);
            for (int i = 0; i < _herd.Count; i++) { ref var b = ref w.Beasts.Get(_herd[i]); if (b.Sex == Sex.Male) _males[b.Kind]++; else _kept[b.Kind]++; }
            for (int guard = 0; guard < 100000 && (wantG > offerG || wantM > offerM); guard++)
            {
                bool mastShort = wantM / DMath.Max(offerM, 1e-9) > wantG / DMath.Max(offerG, 1e-9);
                int kind = -1, most = 0;
                for (int k = 0; k < Kinds; k++)
                {
                    if ((mastShort ? Laws[k].MastShare : Laws[k].GrassShare) <= 0) continue;
                    int beyond = Math.Max(0, _males[k] - 1) + Math.Max(0, _kept[k] - 2);
                    if (beyond > most) { most = beyond; kind = k; }
                }
                if (kind < 0) return;   // only the nuclei are left: they go short
                bool male = _males[kind] - 1 >= _kept[kind] - 2 && _males[kind] > 1;
                int pick = 0; double grown = -1;
                for (int i = 0; i < _herd.Count; i++)
                {
                    ref var b = ref w.Beasts.Get(_herd[i]);
                    if (!b.Alive || b.Kind != kind || (b.Sex == Sex.Male) != male) continue;
                    double g = GrownShare(in b, now) + (b.Due == 0 && b.MilkUntil <= now ? 0.001 : 0.0);   // (an empty one first, among equals)
                    if (g > grown) { grown = g; pick = _herd[i]; }
                }
                if (pick == 0) { if (male) _males[kind] = 1; else _kept[kind] = 2; continue; }
                ref var be = ref w.Beasts.Get(pick);
                double e = _intake[be.Kind] * GrownShare(in be, now);
                wantG -= e * Laws[be.Kind].GrassShare; wantM -= e * Laws[be.Kind].MastShare;
                if (male) _males[kind]--; else _kept[kind]--;
                Count(be.Kind, mastShort ? 9 : 8);
                Slaughter(ref be, be.Owner, sid);
            }
        }

        /// <summary>A household's hands for herding: its grown people, children from eight and the old for half.</summary>
        public double Hands(int hid)
        {
            var w = _w;
            var ms = w.Members(hid);
            double hands = 0;
            for (int i = 0; i < ms.Count; i++)
            {
                double age = w.People.Get(ms[i]).AgeYears(w.Now);
                hands += age < 8 ? 0.0 : age < 15 ? 0.5 : age < 60 ? 1.0 : 0.5;
            }
            return hands;
        }

        /// <summary>
        /// The animal a household would kill first: of what it would spare beyond its breeding core, from the kind it has most to
        /// spare of (herders keep a mixed herd), a male before a female, the most grown first; among the animals that eat (mode 0),
        /// eat grass (1) or mast (2). 0 if none. The core of each kind: its first two males (the sire and his successor) and its few
        /// breeding females - the youngest in their prime, then the eldest of the young ones growing up to replace them, then the
        /// ageing.
        /// </summary>
        int Spare(int hid, int mode)
        {
            var w = _w;
            long now = w.Now;
            var mine = w.BeastsOf(hid);
            for (int k = 0; k < Kinds; k++) { _males[k] = 0; _prime[k] = 0; _young[k] = 0; _seenP[k] = 0; _seenY[k] = 0; _seenA[k] = 0; }
            for (int j = 0; j < mine.Count; j++)
            {
                ref var b = ref w.Beasts.Get(mine[j]);
                if (b.Sex != Sex.Female) continue;
                double age = b.AgeYears(now);
                if (age < Laws[b.Kind].Mature) _young[b.Kind]++;
                else if (age < 0.6 * Laws[b.Kind].Lifespan) _prime[b.Kind]++;
            }
            for (int k = 0; k < Kinds; k++) { _spares[k] = 0; _pickOf[k] = 0; _pickEats[k] = 0; _pickMale[k] = false; }
            for (int j = 0; j < mine.Count; j++)   // (id order: the eldest first, the youngest last)
            {
                ref var b = ref w.Beasts.Get(mine[j]);
                int k = b.Kind, keep = Laws[k].Core;
                bool core;
                if (b.Sex == Sex.Male) core = ++_males[k] <= CoreMales;
                else
                {
                    double age = b.AgeYears(now);
                    if (age < Laws[k].Mature) core = ++_seenY[k] <= keep - _prime[k];                          // the eldest young
                    else if (age < 0.6 * Laws[k].Lifespan) core = _prime[k] - ++_seenP[k] < keep;              // the youngest in their prime
                    else core = ++_seenA[k] <= keep - _prime[k] - _young[k];                                   // the ageing, if few else
                }
                if (core) continue;
                double eats = _intake[b.Kind] * GrownShare(in b, now) * (mode == 0 ? 1.0 : mode == 2 ? Laws[b.Kind].MastShare : Laws[b.Kind].GrassShare);
                if (eats <= 0) continue;
                _spares[k]++;
                bool male = b.Sex == Sex.Male;
                if (_pickOf[k] == 0 || (male && !_pickMale[k]) || (male == _pickMale[k] && eats > _pickEats[k])) { _pickOf[k] = b.Id; _pickEats[k] = eats; _pickMale[k] = male; }
            }
            int kind = -1;
            for (int k = 0; k < Kinds; k++) if (_spares[k] > 0 && (kind < 0 || _spares[k] > _spares[kind])) kind = k;
            return kind < 0 ? 0 : _pickOf[kind];
        }

        /// <summary>A grown head of cattle in the household to draw an ard (D2-063).</summary>
        public bool HasOx(int hid)
        {
            var w = _w;
            var mine = w.BeastsOf(hid);
            for (int i = 0; i < mine.Count; i++)
            {
                ref var b = ref w.Beasts.Get(mine[i]);
                if (b.Alive && b.Kind == Cattle && b.AgeYears(w.Now) >= Laws[Cattle].Mature) return true;
            }
            return false;
        }

        void Slaughter(ref Beast b, int hid, int sid)
        {
            var w = _w;
            double fu = Laws[b.Kind].Mass * GrownShare(in b, w.Now) * DMath.Clamp(b.Cond, 0.4, 1.0) * WildFood.MeatShare * WildFood.FuPerKg;
            b.Alive = false; b.Died = w.Now;
            w.BeastsOf(hid).Remove(b.Id);
            Store(hid, sid, fu);
            w.Settlements.Get(sid).Hides += b.Kind == Cattle ? 1.0 : 0.5;   // its hide to the store (D2-063)
            w.Metrics.BeastsEaten++;
            w.Metrics.BeastFu += fu;
        }

        bool SireNear(int sid, byte kind)
        {
            if (_sireNear[kind] < 0) _sireNear[kind] = (sbyte)(HasMale(sid, kind) ? 1 : 0);
            return _sireNear[kind] > 0;
        }

        /// <summary>
        /// A grown male of a kind serves the village's females: its own, or else a neighbouring village's within a walk (a bull
        /// borrowed, a sow taken to the neighbour's boar).
        /// </summary>
        bool HasMale(int sid, byte kind)
        {
            var w = _w;
            for (int i = 0; i < _herd.Count; i++) if (GrownMale(_herd[i], kind)) return true;
            var P = w.Planet;
            int cell = w.Settlements.Get(sid).Cell;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                ref var os = ref w.Settlements.Get(o);
                if (o == sid || !os.Inhabited || Vec3d.ArcDistance(P.Grid.Center[cell], P.Grid.Center[os.Cell], P.Radius) > SireReach) continue;
                var hhs = w.HouseholdsOf(o);
                for (int i = 0; i < hhs.Count; i++)
                {
                    var mine = w.BeastsOf(hhs[i]);
                    for (int j = 0; j < mine.Count; j++) if (GrownMale(mine[j], kind)) return true;
                }
            }
            return false;
        }

        bool GrownMale(int id, byte kind)
        {
            ref var b = ref _w.Beasts.Get(id);
            return b.Alive && b.Kind == kind && b.Sex == Sex.Male && b.AgeYears(_w.Now) >= Laws[kind].Mature;
        }

        void Birth(ref Beast mother)
        {
            var w = _w;
            var k = Laws[mother.Kind];
            mother.Due = 0;
            mother.MilkUntil = w.Now + DMath.ToLong(k.MilkDays * SimTime.Day);
            ulong key = KeyedRng.Hash(w.Seed, RngStream.Birth, (ulong)mother.Id | (1UL << 40), (ulong)w.Now);
            var rs = new RandomSequence(key);
            int young = Math.Max(1, (int)DMath.Floor(k.Litter * (0.6 + 0.8 * rs.NextUnit()) * DMath.Clamp01(mother.Cond / 0.7) + 0.5));
            for (int y = 0; y < young; y++) Add(mother.Kind, rs.Chance(0.5) ? Sex.Female : Sex.Male, w.Now, mother.Id, mother.Owner, 0.7f);
            w.Metrics.BeastBirths += young;
            if (DiagEnds != null) DiagEnds[Kinds * Causes + mother.Kind] += young;
        }

        void Die(ref Beast b, bool wolf)
        {
            b.Alive = false; b.Died = _w.Now;
            _w.BeastsOf(b.Owner).Remove(b.Id);
            if (wolf) _w.Metrics.BeastsTaken++; else _w.Metrics.BeastDeaths++;
        }

        /// <summary>Milk and meat come home as the hunters' catch does: the household's larder first, the rest to the granary.</summary>
        void Store(int hid, int sid, double fu)
        {
            if (fu <= 0) return;
            var w = _w;
            if (w.Households.Exists(hid))
            {
                ref var hh = ref w.Households.Get(hid);
                double keep = DMath.Min(fu, DMath.Max(0.0, 4.0 * w.SettlementSys.HouseholdNeed(hid) - hh.Larder));
                hh.Larder += keep; fu -= keep;
                TC2.Core.Diagnostics.HouseLedger.Bring(w, hid, sid, keep, fu);
            }
            if (fu > 0) { w.Settlements.Get(sid).Granary += fu; w.Holdings.Put(hid, sid, fu); }   // (its own beasts' milk and meat: C3.3)
        }

        /// <summary>
        /// Slaughter (D2-048): each household keeps two grown males of a kind (the sire and his successor) and kills the others as
        /// they grow, the old past their work and the failing; when its larder and the granary are bare, the best-fleshed of the
        /// rest beyond a pair of each kind (never one heavy with young or in milk). The meat comes home.
        /// </summary>
        void Cull(int sid)
        {
            var w = _w;
            long now = w.Now;
            var hhs = w.HouseholdsOf(sid);
            double granary = w.Settlements.Get(sid).Granary;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                var mine = w.BeastsOf(hid);
                if (mine.Count == 0 || w.Households.Get(hid).Party != 0) continue;
                double need = w.SettlementSys.HouseholdNeed(hid);
                bool hungry = w.Households.Get(hid).Larder < need && granary < need * 2;
                for (int k = 0; k < Kinds; k++) { _kept[k] = 0; _males[k] = 0; }
                _herd.Clear();
                for (int j = 0; j < mine.Count; j++) _herd.Add(mine[j]);   // (the list changes as animals are killed)
                for (int j = 0; j < _herd.Count; j++)
                {
                    ref var b = ref w.Beasts.Get(_herd[j]);
                    if (!b.Alive) continue;
                    var k = Laws[b.Kind];
                    double age = b.AgeYears(now);
                    bool carrying = b.Due != 0 || b.MilkUntil > now;
                    bool grownMale = b.Sex == Sex.Male && age >= k.Mature;
                    bool sire = grownMale && _males[b.Kind] < CoreMales;
                    bool slaughter = (grownMale && !sire)                                                  // two breeding males are enough
                                     || age > k.Lifespan * 0.75                                            // past its work
                                     || (b.Cond < FailingCond && !carrying && !sire)                       // failing: better eaten than starved
                                     || (hungry && b.Cond > 0.4 && _kept[b.Kind] >= 2 && !carrying);       // hunger: eat the herd, keep a pair
                    if (!slaughter)
                    {
                        _kept[b.Kind]++;
                        if (grownMale) _males[b.Kind]++;
                        continue;
                    }
                    Count(b.Kind, grownMale && !sire ? 3 : age > k.Lifespan * 0.75 ? 4 : b.Cond < FailingCond && !carrying && !sire ? 5 : 6);
                    Slaughter(ref b, hid, sid);
                    if (hungry && w.Households.Get(hid).Larder >= need * 3) hungry = false;
                }
            }
        }

        /// <summary>
        /// A child marrying out takes a share of the family's herd: of each kind and sex its household keeps more than one of, the
        /// youngest goes with the new household (from both families: the couple's first animals, a breeding pair where the
        /// families have them). Animals follow the generations, not only the deaths.
        /// </summary>
        public void Endow(int fromHid, int toHid)
        {
            var w = _w;
            if (fromHid == toHid) return;
            var from = w.BeastsOf(fromHid);
            if (from.Count < 2) return;
            var to = w.BeastsOf(toHid);
            for (byte k = 0; k < Kinds; k++)
                for (byte sex = Sex.Female; sex <= Sex.Male; sex++)
                {
                    int count = 0, youngest = 0;
                    for (int j = 0; j < from.Count; j++) { ref var b = ref w.Beasts.Get(from[j]); if (b.Kind == k && b.Sex == sex) { count++; youngest = from[j]; } }
                    if (count < 2) continue;
                    from.Remove(youngest);
                    w.Beasts.Get(youngest).Owner = toHid;
                    to.InsertSorted(youngest);
                }
        }

        /// <summary>A household's animals go to another household (its kin's when it ends at a marriage, a neighbour's when it dies out).</summary>
        public void Inherit(int fromHid, int toHid)
        {
            var w = _w;
            if (fromHid == toHid) return;
            var from = w.BeastsOf(fromHid);
            var to = w.BeastsOf(toHid);
            for (int i = 0; i < from.Count; i++) { int b = from[i]; w.Beasts.Get(b).Owner = toHid; to.InsertSorted(b); }
            from.Clear();
        }

        /// <summary>
        /// A village abandoned: its animals are left to the land and join the wild of their kind round it (pigs the boar, goats
        /// the ibex, cattle the aurochs - the living species of that kind most numerous there); where none of that kind lives, they
        /// die out.
        /// </summary>
        public void Feral(int sid)
        {
            var w = _w;
            int cell = w.Settlements.Get(sid).Cell;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) FeralHousehold(hhs[i], cell);
        }

        /// <summary>One household's animals left to the land at a cell (its keepers died out with nobody left to take them: D2-059).</summary>
        public void FeralHousehold(int hid, int cell)
        {
            var w = _w; var P = w.Planet; int n = P.Cells;
            var mine = w.BeastsOf(hid);
            for (int j = 0; j < mine.Count; j++)
            {
                ref var b = ref w.Beasts.Get(mine[j]);
                byte wk = Laws[b.Kind].WildKind;
                int best = -1; double bv = -1;
                for (int q = 0; q < P.Species.Count; q++)
                {
                    var sp = P.Species[q];
                    if (sp.Habitat != 0 || sp.Gone != 0 || sp.Kind != wk) continue;
                    double v = P.Abund[q * n + cell];
                    if (v > bv) { bv = v; best = q; }
                }
                if (best >= 0) { P.Abund[best * n + cell] = (float)(P.Abund[best * n + cell] + 1.0); w.Metrics.BeastsFeral++; }
                else w.Metrics.BeastDeaths++;
                b.Alive = false; b.Died = w.Now;
            }
            mine.Clear();
        }

        /// <summary>Dead animals leave the table (their history stays in the counts).</summary>
        public void Prune()
        {
            var w = _w;
            _herd.Clear();
            for (int b = w.Beasts.First(); b != 0; b = w.Beasts.Next(b)) if (!w.Beasts.Get(b).Alive) _herd.Add(b);
            for (int i = 0; i < _herd.Count; i++) w.Beasts.Remove(_herd[i]);
        }

        /// <summary>A village's animals of a kind, on the pasture or on the road with their households (reads only).</summary>
        public int CountOf(int sid, byte kind)
        {
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            int count = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                var mine = w.BeastsOf(hhs[i]);
                for (int j = 0; j < mine.Count; j++) if (w.Beasts.Get(mine[j]).Kind == kind) count++;
            }
            return count;
        }
    }
}
