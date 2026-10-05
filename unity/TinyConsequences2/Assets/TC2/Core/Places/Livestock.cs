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
        readonly List<int> _cells = new List<int>(), _herd = new List<int>(), _hhs = new List<int>();
        readonly List<double> _hhG = new List<double>(), _hhM = new List<double>(), _hhCap = new List<double>();
        readonly int[] _kept = new int[Kinds], _males = new int[Kinds];
        readonly int[] _prime = new int[Kinds], _young = new int[Kinds], _seenP = new int[Kinds], _seenY = new int[Kinds], _seenA = new int[Kinds];
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

        /// <summary>A village's herds live a day (both resolutions; SettlementSystem.Daily).</summary>
        public void Day(int sid)
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
            _season = PlanetSystems.Season(now);
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
            if (wantG > StockShare * offer || wantM > StockShare * mast * MastFind)
            {
                // the near pasture is short: the herders take the herds further out (onto land the neighbours' herds may graze too)
                w.Hazards.CellsWithin(w, s.Cell, FarReach, _cells);
                Offer(out offer, out mast);
            }
            double fedG = wantG > 0 ? DMath.Min(1.0, offer / wantG) : 1.0;
            double fedM = wantM > 0 ? DMath.Min(1.0, mast * MastFind / wantM) : 1.0;
            if (Diag != null && sid * 4 + 3 < Diag.Length) { Diag[sid * 4] = offer; Diag[sid * 4 + 1] = wantG; Diag[sid * 4 + 2] = mast * MastFind; Diag[sid * 4 + 3] = wantM; }
            double eatenG = wantG * fedG, eatenM = wantM * fedM;
            if (eatenG > 0 && offer > 0)
            {
                double share = eatenG / offer;
                for (int i = 0; i < _cells.Count; i++)
                {
                    int c = _cells[i];
                    if (!Grazes(P, c)) continue;
                    double take = DMath.Max(0.0, P.Grass[c] - Ecology.GrassFloor) * share * GrazeShare(c);
                    P.Grass[c] = (float)DMath.Max(Ecology.GrassFloor * 0.5, P.Grass[c] - take);
                }
            }
            if (eatenM > 0 && mast > 0)
                for (int i = 0; i < _cells.Count; i++)
                {
                    int c = _cells[i];
                    double left = WildFood.MastLeft(P, c);
                    if (left > 0) P.Gathered[c] = (float)(P.Gathered[c] + eatenM * left / mast);
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
            for (int i = 0; i < _herd.Count; i++)
            {
                int id = _herd[i];
                ref var b = ref w.Beasts.Get(id);
                if (!b.Alive) continue;
                var k = Laws[b.Kind];
                double age = b.AgeYears(now);
                // condition follows how well it ate (a goat's browse is never short)
                double ate = k.GrassShare * fedG + k.MastShare * fedM + k.ScrapShare * fedS + (1.0 - k.GrassShare - k.MastShare - k.ScrapShare);
                b.Cond = (float)DMath.Clamp01(b.Cond + (ate - 0.8) * 0.06);
                // old age, hunger, wolves
                double wolf = wolves * 0.00025 * (age < 1 ? 3.0 : 1.0) * (b.Kind == Cattle && age >= 1 ? 0.2 : 1.0);
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
            Destock(sid, StockShare * offer, StockShare * mast * MastFind);
            Cull(sid);
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
            _hhs.Clear(); _hhG.Clear(); _hhM.Clear(); _hhCap.Clear();
            double wantG = 0, wantM = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                var mine = w.BeastsOf(hid);
                if (mine.Count == 0 || w.Households.Get(hid).Party != 0) continue;
                double g = 0, m = 0;
                for (int j = 0; j < mine.Count; j++)
                {
                    ref var b = ref w.Beasts.Get(mine[j]);
                    double eat = _intake[b.Kind] * GrownShare(in b, now);
                    g += eat * Laws[b.Kind].GrassShare; m += eat * Laws[b.Kind].MastShare;
                }
                _hhs.Add(hid); _hhG.Add(g); _hhM.Add(m); _hhCap.Add(Hands(hid) * TendKg);
                wantG += g; wantM += m;
            }
            for (int guard = 0; guard < 100000; guard++)
            {
                // more than its hands can tend (the household most over first): its biggest eater beyond the core
                int pick = -1; int mode = 0; double most = 1.0;
                for (int i = 0; i < _hhs.Count; i++)
                {
                    double over = (_hhG[i] + _hhM[i]) / DMath.Max(1e-9, _hhCap[i]);
                    if (over > most && Spare(_hhs[i], 0) != 0) { most = over; pick = i; }
                }
                if (pick < 0)
                {
                    // the food that is shorter decides what is killed (any grazer for the grass, pigs for the mast)
                    double rg = offerG > 0 ? wantG / offerG : (wantG > 0 ? double.MaxValue : 0);
                    double rm = offerM > 0 ? wantM / offerM : (wantM > 0 ? double.MaxValue : 0);
                    if (rg <= 1.0 && rm <= 1.0) break;
                    mode = rm > rg ? 2 : 1;
                    most = 0;
                    for (int i = 0; i < _hhs.Count; i++)
                    {
                        int people = w.Members(_hhs[i]).Count;
                        double per = (mode == 2 ? _hhM[i] : _hhG[i]) / Math.Max(1, people);
                        if (per > most && Spare(_hhs[i], mode) != 0) { most = per; pick = i; }
                    }
                }
                if (pick < 0) break;   // nothing more anyone would kill: the herds go short
                int hid = _hhs[pick];
                int bid = Spare(hid, mode);
                ref var be = ref w.Beasts.Get(bid);
                double e = _intake[be.Kind] * GrownShare(in be, now);
                double dg = e * Laws[be.Kind].GrassShare, dm = e * Laws[be.Kind].MastShare;
                _hhG[pick] -= dg; _hhM[pick] -= dm; wantG -= dg; wantM -= dm;
                Count(be.Kind, mode == 0 ? 7 : mode == 1 ? 8 : 9);
                Slaughter(ref be, hid, sid);
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
        /// The animal a household would kill first: of what it would spare beyond its breeding core, the one that eats the most
        /// (mode 0), the most grass (1) or the most mast (2); the youngest among equals. 0 if none. The core of each kind: its
        /// first two males (the sire and his successor) and its few breeding females - the youngest in their prime, then the
        /// eldest of the young ones growing up to replace them, then the ageing.
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
            int spare = 0; double most = 0;
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
                if (eats > 0 && eats >= most) { most = eats; spare = b.Id; }
            }
            return spare;
        }

        void Slaughter(ref Beast b, int hid, int sid)
        {
            var w = _w;
            double fu = Laws[b.Kind].Mass * GrownShare(in b, w.Now) * DMath.Clamp(b.Cond, 0.4, 1.0) * WildFood.MeatShare * WildFood.FuPerKg;
            b.Alive = false; b.Died = w.Now;
            w.BeastsOf(hid).Remove(b.Id);
            Store(hid, sid, fu);
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
            }
            if (fu > 0) w.Settlements.Get(sid).Granary += fu;
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
            var w = _w; var P = w.Planet; int n = P.Cells;
            int cell = w.Settlements.Get(sid).Cell;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                var mine = w.BeastsOf(hhs[i]);
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
                    if (best >= 0) { P.Abund[best * n + cell] += 1f; w.Metrics.BeastsFeral++; }
                    else w.Metrics.BeastDeaths++;
                    b.Alive = false; b.Died = w.Now;
                }
                mine.Clear();
            }
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
