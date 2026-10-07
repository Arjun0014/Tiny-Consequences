using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    public enum Role : byte { Grazer = 0, Browser = 1, Omnivore = 2, Predator = 3 }

    /// <summary>A species (06 §1): what it eats, how big it is, how fast it lives, where it can live.</summary>
    public struct Species
    {
        public int Id;              // entity id (history names it)
        public byte Kind;           // template: 0 deer, 1 hare, 2 aurochs, 3 boar, 4 ibex, 5 wolf, 6 fox, 7 bear
        public Role Role;
        public float Mass;          // kg (adult)
        public float Fecundity;     // young per adult per year in good condition
        public float Lifespan;      // years
        public float ColdTol;       // the coldest annual mean it bears (C)
        public float ForestPref;    // 0 open country .. 1 deep woods
        public float SlopePref;     // 0 flat .. 1 crags
        public float Grass, Browse; // share of its plant food from grass and from shoots (herbivores, omnivores)
        public float Mast, Small;   // ... from mast and roots in the woods, and from small fare in the grass (voles, beetles)
        public float Crag;          // ... from the herbs and lichens of steep open ground, which only climbers reach
        public float Plankton;      // ... from the sea's plankton (fish)
        public byte Habitat;        // 0 land, 1 sea
        public float HeatTol;       // the warmest annual mean it thrives in (C)
        public float Offshore;      // sea: 0 coasts and shelves .. 1 the open ocean
        public float Metab;         // food a year per kg^0.75 (9 for warm-blooded animals, ~2 for fish)
        public float Range;         // hunters: how many cells' worth of prey their rounds cover (a seal or a tuna roams far)
        public int Prey;            // bit mask of the kinds it hunts (predators, omnivores)
        public float Disperse;      // share that wanders to neighbouring cells each step
        public float Territory;     // hunters: animals a cell's range holds before the young find no room (0: no limit)
        public int Parent;          // lineage (0 for the first species)
        public long Born;
        public long Gone;           // when it died out (0: alive)

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Byte(Kind); w.Byte((byte)Role); w.Float(Mass); w.Float(Fecundity); w.Float(Lifespan); w.Float(ColdTol);
            w.Float(ForestPref); w.Float(SlopePref); w.Float(Grass); w.Float(Browse); w.Float(Mast); w.Float(Small); w.Float(Crag); w.Float(Plankton); w.Byte(Habitat); w.Float(HeatTol); w.Float(Offshore); w.Float(Metab); w.Float(Range); w.Int(Prey); w.Float(Disperse); w.Float(Territory); w.Int(Parent); w.Long(Born); w.Long(Gone);
        }

        public static Species Read(BinReader r) => new Species
        {
            Id = r.Int(), Kind = r.Byte(), Role = (Role)r.Byte(), Mass = r.Float(), Fecundity = r.Float(), Lifespan = r.Float(), ColdTol = r.Float(),
            ForestPref = r.Float(), SlopePref = r.Float(), Grass = r.Float(), Browse = r.Float(), Mast = r.Float(), Small = r.Float(), Crag = r.Float(), Plankton = r.Float(), Habitat = r.Byte(), HeatTol = r.Float(), Offshore = r.Float(), Metab = r.Float(), Range = r.Float(), Prey = r.Int(), Disperse = r.Float(), Territory = r.Float(), Parent = r.Int(), Born = r.Long(), Gone = r.Long(),
        };

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(Kind); h.Add((byte)Role); h.Add(Mass); h.Add(Fecundity); h.Add(Lifespan); h.Add(ColdTol);
            h.Add(ForestPref); h.Add(SlopePref); h.Add(Grass); h.Add(Browse); h.Add(Mast); h.Add(Small); h.Add(Crag); h.Add(Plankton); h.Add(Habitat); h.Add(HeatTol); h.Add(Offshore); h.Add(Metab); h.Add(Range); h.Add(Prey); h.Add(Disperse); h.Add(Territory); h.Add(Parent); h.Add(Born); h.Add(Gone);
        }

        /// <summary>Plant food it needs a year (kg dry matter), or meat for a predator: ~ mass^0.75 (less for cold-blooded fish).</summary>
        public double Intake => Metab * DMath.Exp(0.75 * DMath.Log(Mass));
    }

    /// <summary>
    /// Wildlife as abundance fields (06 §2, D2-035): every species holds a number of animals and their condition in each
    /// cell. Each vegetation step: herbivores eat the grass and the woods (which therefore grow back less), sharing what
    /// there is when there is too little; predators hunt by a saturating (Holling II) response with refuges in the woods;
    /// condition follows how well each ate; animals die of age, hunger and cold; young are born in spring by condition;
    /// a share wanders to the better neighbouring cells. Herds, packs and their cycles emerge from these rules. The
    /// world begins with its animals already spread to what the land holds (a spin-up before people).
    /// </summary>
    public static class Ecology
    {
        public const float LandMetab = 9f;       // a land animal's intake a year: this x mass^0.75 kg (livestock eat by the same law)
        public const double GrassMax = 1000.0;   // kg of grass on a hectare under full cover
        public const double BrowseMax = 600.0;   // kg of browse (shoots, leaves, mast) a hectare of full woods offers
        public const double EatShare = 0.3;      // at most this share of the standing grass above its floor is eaten in a step
        public const double GrassFloor = 0.25;   // the bottom of the sward (roots, short growth) cannot be grazed away
        public const double MastMax = 350.0;     // kg of mast, roots and fungi a hectare of full woods offers a year
        public const double SmallMax = 60.0;     // kg of voles and beetles a hectare of full grass offers a year
        public const double CragMax = 150.0;     // kg of herbs and lichens a hectare of steep open ground offers a year (climbers only)
        public const double PlanktonMax = 400.0; // kg of plankton a hectare of the richest sea offers fish a year (D2-039)
        public const double RiverRich = 20000.0; // m3/day of river at which a mouth's waters are as rich as they get
        public const int MaxK = 24;              // species arrays are sized for this many (Evolution.MaxSpecies <= MaxK)
        public const double BrowseRenew = 1.0;   // browse is the woods' shoots and leaves: this many times BrowseMax a year
        public const double HalfPrey = 120.0 * Density;   // kg of reachable prey in a cell at which a hunter eats half its fill
        /// <summary>
        /// The share of the plants' growth that wild animals can use as food (D2-044): most of what grows is out of reach, too
        /// poor to digest or lost to the lean season. At 0.15 deer and boar stand at ~15-20 a km2 and game at ~30-45 kg/ha (real
        /// temperate ranges 5-30 kg/ha); at 1 (D2-035-D2-041) they stood ten times thicker than any real land carries.
        /// Hunting's half-satiation, territories and the first numbers scale with it, so the food web keeps its shape.
        /// </summary>
        public const double Density = 0.15;

        struct Template
        {
            public string Name; public Role Role; public float Mass, Fec, Life, Cold, Forest, Slope, Grass, Browse, Mast, Small, Crag, Disperse, Territory; public int Prey;
            public float Plankton, Heat, Offshore, Metab, Range; public byte Habitat;
        }

        static readonly Template[] Templates =
        {
            new Template { Name = "deer",    Role = Role.Browser,  Mass = 90,  Fec = 0.8f, Life = 12, Cold = -8,  Forest = 0.55f, Slope = 0.2f, Grass = 0.5f, Browse = 0.5f, Disperse = 0.06f },
            new Template { Name = "hare",    Role = Role.Grazer,   Mass = 3,   Fec = 4.0f, Life = 3,  Cold = -14, Forest = 0.2f,  Slope = 0.2f, Grass = 0.9f, Browse = 0.1f, Disperse = 0.08f },
            new Template { Name = "aurochs", Role = Role.Grazer,   Mass = 600, Fec = 0.45f, Life = 18, Cold = -10, Forest = 0.15f, Slope = 0.0f, Grass = 1.0f, Disperse = 0.05f },
            new Template { Name = "boar",    Role = Role.Omnivore, Mass = 80,  Fec = 2.5f, Life = 9,  Cold = -6,  Forest = 0.75f, Slope = 0.2f, Grass = 0.15f, Mast = 0.85f, Disperse = 0.06f },
            // an ibex lives on the crags: grass and shrubs where it can, the herbs of the steep ground no grazer reaches
            new Template { Name = "ibex",    Role = Role.Grazer,   Mass = 60,  Fec = 0.7f, Life = 14, Cold = -16, Forest = 0.1f,  Slope = 0.9f, Grass = 0.45f, Browse = 0.15f, Crag = 0.4f, Disperse = 0.05f },
            new Template { Name = "wolf",    Role = Role.Predator, Mass = 35,  Fec = 1.5f, Life = 9,  Cold = -30, Forest = 0.4f,  Slope = 0.3f, Disperse = 0.10f, Territory = 0.5f, Prey = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) },
            // a fox lives as much on voles and beetles in the grass as on hares
            new Template { Name = "fox",     Role = Role.Omnivore, Mass = 6,   Fec = 2.0f, Life = 5,  Cold = -25, Forest = 0.4f,  Slope = 0.2f, Small = 1.0f, Disperse = 0.10f, Territory = 1.2f, Prey = 1 << 1 },
            // a bear eats most things: shoots, mast, and roots, insects and berries (small fare), a deer when it can
            new Template { Name = "bear",    Role = Role.Omnivore, Mass = 250, Fec = 0.3f, Life = 22, Cold = -20, Forest = 0.85f, Slope = 0.5f, Browse = 0.2f, Mast = 0.45f, Small = 0.35f, Disperse = 0.04f, Territory = 0.15f, Prey = 1 << 0 },
            // the sea (D2-039): two shoaling fish of the plankton, cold and warm; their hunters; seals on the coasts
            new Template { Name = "herring", Role = Role.Grazer,   Habitat = 1, Mass = 0.25f, Fec = 1.2f, Life = 9,  Cold = -1, Heat = 16, Offshore = 0.4f, Plankton = 1.0f, Metab = 2.2f, Disperse = 0.12f },
            new Template { Name = "sardine", Role = Role.Grazer,   Habitat = 1, Mass = 0.1f,  Fec = 1.6f, Life = 5,  Cold = 13, Heat = 30, Offshore = 0.3f, Plankton = 1.0f, Metab = 2.2f, Disperse = 0.12f },
            new Template { Name = "cod",     Role = Role.Predator, Habitat = 1, Mass = 6,     Fec = 0.6f, Life = 15, Cold = -2, Heat = 14, Offshore = 0.3f, Metab = 2.2f, Disperse = 0.08f, Territory = 40f, Prey = 1 << 8 },
            new Template { Name = "tuna",    Role = Role.Predator, Habitat = 1, Mass = 50,    Fec = 0.5f, Life = 12, Cold = 16, Heat = 31, Offshore = 0.8f, Metab = 2.5f, Range = 3f, Disperse = 0.2f, Territory = 3f, Prey = (1 << 9) | (1 << 8) },
            // (seals keep to the cold coasts where the fish are: the polar seas beyond the fish would starve them)
            new Template { Name = "seal",    Role = Role.Predator, Habitat = 1, Mass = 90,    Fec = 0.3f, Life = 25, Cold = -4, Heat = 16, Offshore = 0.0f, Metab = 9f, Range = 12f, Disperse = 0.05f, Territory = 0.5f, Prey = (1 << 8) | (1 << 10) },
        };

        public static string KindName(byte kind) => kind < Templates.Length ? Templates[kind].Name : "beast";

        [ThreadStatic] static double[] _grassEaten, _browseEaten;
        [ThreadStatic] static double[] _fx;   // wandering, per species and cell [i*4]: 0 left, 1 came, 2 their coats, 3 their lineage (zeroed as used)
        [ThreadStatic] static float[] _slope; // steepness of every cell (from the relief alone: rebuilt when it moves)
        [ThreadStatic] static PlanetState _slopeOf;
        [ThreadStatic] static int _slopeRelief;
        [ThreadStatic] static float[] _suit, _steep;
        [ThreadStatic] static double[] _vuln, _keep;   // per step: vulnerability [hunter*16+prey], coat-selection keep per species
        [ThreadStatic] static Species[] _sp;
        /// <summary>
        /// Diagnostics for tools only (null in the game): per species [s*8 + k] animals lost to k = 0 age, 1 hunger,
        /// 2 cold, 3 a coat far from the place, 4 hunters; 5 born; summed over the steps since it was cleared. Never read
        /// by the simulation.
        /// </summary>
        public static double[] Diag;
        public static readonly List<double[]> DiagSpin = new List<double[]>();   // tools: each species' animals after each spin-up step
        /// <summary>Tools only (relief and climate experiments): skip the animals' steps. Never set in the game or the tests.</summary>
        public static bool SkipForTools;

        /// <summary>Worldgen: this world's species (the templates, varied by the seed), spread over the land and settled.</summary>
        public static void Create(Simulation.World w)
        {
            var P = w.Planet;
            int k = Templates.Length;
            P.Species.Clear();
            var rs = new RandomSequence(KeyedRng.Hash(w.Seed, RngStream.Ecology, 1));
            for (int i = 0; i < k; i++)
            {
                var t = Templates[i];
                double v() => 0.85 + 0.3 * rs.NextUnit();
                int id = w.Ids.Next(EntityKind.Species);
                P.Species.Add(new Species
                {
                    Id = id, Kind = (byte)i, Role = t.Role, Mass = (float)(t.Mass * v()), Fecundity = (float)(t.Fec * v()), Lifespan = (float)(t.Life * v()),
                    ColdTol = (float)(t.Cold + 4.0 * (rs.NextUnit() - 0.5)), ForestPref = t.Forest, SlopePref = t.Slope, Grass = t.Grass, Browse = t.Browse,
                    Mast = t.Mast, Small = t.Small, Crag = t.Crag, Plankton = t.Plankton, Habitat = t.Habitat,
                    HeatTol = t.Habitat == 1 ? (float)(t.Heat + 2.0 * (rs.NextUnit() - 0.5)) : 34f, Offshore = t.Offshore, Metab = t.Metab > 0 ? t.Metab : LandMetab,
                    Range = t.Range > 0 ? t.Range : 1f,
                    Prey = t.Prey, Disperse = t.Disperse, Territory = (float)(t.Territory * Density), Born = 0,
                });
                w.History.Register(new EntityRef(EntityKind.Species, id), t.Name, 0);
            }
            P.Abund = new float[k * P.Cells];
            P.Cond = new float[k * P.Cells];
            P.Coat = new float[k * P.Cells];
            P.Drift = new float[k * P.Cells];
            // a few of each on every cell of its habitat it can live in; the spin-up spreads them to what the land (or sea) holds
            for (int s = 0; s < k; s++)
                for (int c = 0; c < P.Cells; c++)
                {
                    if (P.IsLand(c) != (P.Species[s].Habitat == 0)) continue;
                    double suit = Suitability(P, P.Species[s], c);
                    if (suit <= 0) continue;
                    // (hunters start few - on land their prey would not bear more; the sea's shoals bear more of theirs)
                    double share = P.Species[s].Role != Role.Predator ? 2.0 : P.Species[s].Habitat == 1 ? 1.5 : 0.3;
                    double start = suit * share * DMath.Exp(-0.25 * DMath.Log(P.Species[s].Mass / 50.0)) * Density;
                    if (P.Species[s].Territory > 0) start = DMath.Min(start, 0.5 * P.Species[s].Territory * P.AreaScale);   // hunters start within their ranges
                    P.Abund[s * P.Cells + c] = (float)start;
                    P.Cond[s * P.Cells + c] = 0.7f;
                }
            // twenty years before people, the plants growing back between the animals' steps. An untouched land booms and
            // busts (hares eat the grass down, foxes starve): refuges keep a hundredth of each kind's first numbers through
            // the spin-up only - the world is older than its history; from the first day on, a kind can die out
            var refuge = new float[k * P.Cells];
            for (int i = 0; i < refuge.Length; i++) refuge[i] = (float)(0.01 * P.Abund[i]);
            long t0 = 0;
            for (int i = 0; i < 40; i++)
            {
                w.PlanetSys.GrowVegetation(w, 0.5); Step(w, 0.5, t0); t0 += SimTime.Year / 2;
                for (int j = 0; j < refuge.Length; j++)
                    if (P.Abund[j] < refuge[j]) { P.Abund[j] = refuge[j]; if (P.Cond[j] < 0.5f) P.Cond[j] = 0.5f; }
                if (Diag != null) { var tot = new double[k]; for (int s = 0; s < k; s++) tot[s] = Total(P, s); DiagSpin.Add(tot); }
            }
        }

        /// <summary>How well a cell suits a species (0..1): climate, cover and slope on land; warmth, depth and coast at sea.</summary>
        public static double Suitability(PlanetState P, in Species sp, int c)
        {
            if (sp.Habitat == 1) { SeaGeometry(P); return SeaSuit(P, sp, c, 0.0, _shelf[c], _coastD[c]); }
            return Suitability(P, sp, c, Steepness(P, c), 0.0);
        }

        // the sea's shape (derived from the relief and the coastline, rebuilt when either moves; never saved)
        [ThreadStatic] static float[] _shelf, _plank, _mouth;
        [ThreadStatic] static double[] _cosLat;
        [ThreadStatic] static byte[] _coastD;
        [ThreadStatic] static PlanetState _seaOf, _mouthsOf;
        [ThreadStatic] static int _seaRelief, _seaCoast, _mouthsRelief, _mouthsCoast;
        [ThreadStatic] static double _seaLevel, _mouthsLevel;
        [ThreadStatic] static List<int> _mouthLand, _mouthSea, _touched;   // rivers reaching the sea; sea cells their waters enrich

        /// <summary>Shelf (1 shallow .. 0 the deep ocean) and distance to the coast (cells, to 6) of every sea cell.</summary>
        static void SeaGeometry(PlanetState P)
        {
            int n = P.Cells;
            // (keyed by everything it reads: the relief, the coastline and the sea's level, so a loaded world rebuilds the same)
            if (_seaOf == P && _shelf != null && _shelf.Length == n && _seaRelief == P.ReliefVersion && _seaCoast == P.CoastVersion && _seaLevel == P.SeaLevel) return;
            _seaOf = P; _seaRelief = P.ReliefVersion; _seaCoast = P.CoastVersion; _seaLevel = P.SeaLevel;
            if (_shelf == null || _shelf.Length != n) { _shelf = new float[n]; _coastD = new byte[n]; _plank = new float[n]; _mouth = new float[n]; _cosLat = new double[n]; }
            for (int c = 0; c < n; c++) _cosLat[c] = DMath.Sqrt(DMath.Max(0.0, 1.0 - P.SinLat[c] * P.SinLat[c]));
            var g = P.Grid;
            for (int c = 0; c < n; c++)
            {
                _coastD[c] = 255;
                if (P.IsLand(c)) { _shelf[c] = 0f; continue; }
                double depth = P.SeaLevel - P.Elev[c];
                _shelf[c] = (float)(1.0 - DMath.SmoothStep(60.0, 500.0, depth));
                for (int q = 0; q < 4; q++) if (P.IsLand(g.Nbr4[c * 4 + q])) { _coastD[c] = 1; break; }
            }
            // out from the coast, five more rings
            for (byte d = 1; d < 6; d++)
                for (int c = 0; c < n; c++)
                {
                    if (_coastD[c] != d) continue;
                    for (int q = 0; q < 4; q++)
                    {
                        int nb = g.Nbr4[c * 4 + q];
                        if (!P.IsLand(nb) && _coastD[nb] == 255) _coastD[nb] = (byte)(d + 1);
                    }
                }
        }

        /// <summary>How well a sea cell suits a sea species: warmth within its band, the depth it keeps to, a coast for seals.</summary>
        static double SeaSuit(PlanetState P, in Species sp, int c, double coat, double shelf, int coastD)
        {
            if (P.IsLand(c)) return 0;
            double tm = P.TMean[c];
            double cold = DMath.Clamp01((tm - (sp.ColdTol + coat)) / 4.0);
            double heat = 1.0 - DMath.SmoothStep(sp.HeatTol - 3.0, sp.HeatTol + 3.0, tm);
            double depth = sp.Offshore + (1.0 - sp.Offshore) * shelf;   // coastal kinds keep to the shelves
            double coast = sp.Offshore < 0.05 ? 1.0 - DMath.SmoothStep(1.5, 4.5, coastD) : 1.0;   // seals haul out on a shore
            return cold * heat * DMath.Max(0.05, depth) * coast;
        }

        /// <summary>The sea's plankton at a time of year (a copy, for tests and tools).</summary>
        public static float[] SeaFoodNow(Simulation.World w, double yearFraction) => (float[])SeaFood(w, yearFraction).Clone();

        /// <summary>
        /// The sea's plankton this step (0..~1.2 of the richest): light (the sun's height at noon - none in the polar
        /// night), nutrients (a little everywhere; cold seas that mix deep; shallow shelves; the mouths of rivers), the
        /// warmth that speeds growth, and the ice that shuts out the light.
        /// </summary>
        static float[] SeaFood(Simulation.World w, double yf)
        {
            var P = w.Planet;
            SeaGeometry(P);
            int n = P.Cells;
            var g = P.Grid;
            var d = w.Drainage;
            // the rivers that reach the sea (the drainage changes only with the relief and the coast)
            if (_mouthLand == null) { _mouthLand = new List<int>(); _mouthSea = new List<int>(); _touched = new List<int>(); }
            if (_mouthsOf != P || _mouthsRelief != P.ReliefVersion || _mouthsCoast != P.CoastVersion || _mouthsLevel != P.SeaLevel)
            {
                _mouthsOf = P; _mouthsRelief = P.ReliefVersion; _mouthsCoast = P.CoastVersion; _mouthsLevel = P.SeaLevel;
                _mouthLand.Clear(); _mouthSea.Clear();
                System.Array.Clear(_mouth, 0, n); _touched.Clear();
                if (d != null)
                    for (int c = 0; c < n; c++)
                    {
                        if (!P.IsLand(c)) continue;
                        int r = d.Receiver[c];
                        if (r >= 0 && !P.IsLand(r)) { _mouthLand.Add(c); _mouthSea.Add(r); }
                    }
            }
            for (int i = 0; i < _touched.Count; i++) _mouth[_touched[i]] = 0f;
            _touched.Clear();
            for (int m = 0; m < _mouthLand.Count; m++)
            {
                int r = _mouthSea[m];
                if (_mouth[r] == 0f) _touched.Add(r);
                _mouth[r] = (float)(_mouth[r] + DMath.Clamp01(P.RiverFlow[_mouthLand[m]] / RiverRich));
            }
            // a river's waters spread a little way out to sea: two rings, fading
            int mouths = _touched.Count;
            for (int i = 0; i < mouths; i++)
            {
                int r = _touched[i];
                float v = _mouth[r];
                for (int q = 0; q < 4; q++)
                {
                    int a = g.Nbr4[r * 4 + q];
                    if (P.IsLand(a)) continue;
                    if (_mouth[a] == 0f) _touched.Add(a);
                    if (0.6 * v > _mouth[a]) _mouth[a] = (float)(0.6 * v);
                    for (int q2 = 0; q2 < 4; q2++)
                    {
                        int b = g.Nbr4[a * 4 + q2];
                        if (P.IsLand(b)) continue;
                        if (_mouth[b] == 0f) _touched.Add(b);
                        if (0.36 * v > _mouth[b]) _mouth[b] = (float)(0.36 * v);
                    }
                }
            }
            double decl = P.Tilt * DMath.Sin(DMath.TwoPi * (yf - 0.125));   // the sun's declination (northern summer near mid-year)
            double cd = DMath.Cos(decl), sd = DMath.Sin(decl), season = DMath.Sin(DMath.TwoPi * (yf - 0.25));
            var cosLat = _cosLat;
            for (int c = 0; c < n; c++)
            {
                if (P.IsLand(c)) { _plank[c] = 0f; continue; }
                double sst = P.TMean[c] + 0.5 * P.TAmp[c] * (P.SinLat[c] >= 0 ? season : -season);
                double light = DMath.Clamp01(cosLat[c] * cd + P.SinLat[c] * sd);   // cos(latitude - declination): the noon sun's height
                double nutrients = 0.15 + 0.5 * (1.0 - DMath.SmoothStep(4.0, 20.0, P.TMean[c])) + 0.35 * _shelf[c] + DMath.Min(1.0, _mouth[c]);
                double grow = 0.4 + 0.6 * DMath.SmoothStep(-2.0, 18.0, sst);
                double ice = 1.0 - DMath.SmoothStep(-2.5, -0.5, sst);
                _plank[c] = (float)(light * DMath.Min(1.2, nutrients) * grow * (1.0 - 0.9 * ice));
            }
            return _plank;
        }

        static double Suitability(PlanetState P, in Species sp, int c, double steep, double coat)
        {
            if (!P.IsLand(c) || P.Ice[c] > 5f || P.LakeDepth[c] > 0.5f) return 0;
            double tm = P.TMean[c];
            double cold = DMath.Clamp01((tm - (sp.ColdTol + coat)) / 6.0);
            double heat = 1.0 - DMath.SmoothStep(30, 38, tm);
            double cover = 1.0 - DMath.Min(1.0, DMath.Abs((double)P.Forest[c] - sp.ForestPref) * 1.4);
            double slope = 0.5 + 0.5 * (1.0 - DMath.Abs(DMath.Clamp01(steep) - sp.SlopePref));
            return cold * heat * DMath.Max(0.15, cover) * slope;
        }

        public static double Steepness(PlanetState P, int c)
        {
            var g = P.Grid;
            double hi = P.Elev[c], lo = P.Elev[c];
            for (int k = 0; k < 4; k++) { double e = P.Elev[g.Nbr4[c * 4 + k]]; if (e > hi) hi = e; if (e < lo) lo = e; }
            return (hi - lo) / (2.0 * P.GeoCellSize);   // the ground's own steepness (D2-085: the same ground at any size)
        }

        /// <summary>A step of the wild animals (dt in years; t the time, for the breeding season).</summary>
        public static void Step(Simulation.World w, double dt, long t)
        {
            var P = w.Planet;
            var g = P.Grid;
            int n = P.Cells, k = P.Species.Count;
            if (k == 0 || P.Abund == null) return;
            if (_grassEaten == null || _grassEaten.Length != n) { _grassEaten = new double[n]; _browseEaten = new double[n]; }
            if (_fx == null || _fx.Length != k * n * 4) { _fx = new double[k * n * 4]; _suit = new float[k * n]; }
            if (_slopeOf != P || _slope == null || _slope.Length != n || _slopeRelief != P.ReliefVersion)
            {
                if (_slope == null || _slope.Length != n) _slope = new float[n];
                for (int c = 0; c < n; c++) _slope[c] = (float)Steepness(P, c);
                _slopeOf = P; _slopeRelief = P.ReliefVersion;
            }
            if (_steep == null || _steep.Length != n) _steep = new float[n];
            double yf = SimTime.YearFraction(t);
            Span<double> need = stackalloc double[MaxK];   // plant food each wants this step (kg)
            Span<double> got = stackalloc double[MaxK];    // what it got: plants, then meat
            Span<double> meat = stackalloc double[MaxK];   // meat each hunter wants this step (kg)
            Span<int> landSp = stackalloc int[MaxK], seaSp = stackalloc int[MaxK];   // the species of each habitat
            Span<double> wt = stackalloc double[4];
            Span<double> food = stackalloc double[6], sum = stackalloc double[6], f = stackalloc double[6];   // per cell, reused
            if (_vuln == null) { _vuln = new double[MaxK * MaxK]; _keep = new double[MaxK]; _sp = new Species[MaxK]; }
            for (int a = 0; a < k; a++) _sp[a] = P.Species[a];   // a List's indexer copies the struct: read them from an array
            // [ThreadStatic] fields cost a thread-local lookup at every touch: the loops below use locals
            var sps = _sp; var suit = _suit; var steep = _steep; var vuln = _vuln; var keepS = _keep;
            var fx = _fx; var slopes = _slope; var grassEaten = _grassEaten; var browseEaten = _browseEaten;
            // what each species eats a year and the prey that sates it half: computed once a step, not in every cell
            Span<double> intake = stackalloc double[MaxK], half = stackalloc double[MaxK];
            double areaK = P.AreaScale;   // (prey and ranges per hectare: a smaller planet's cells hold less, D2-085)
            // (half sated at less prey for a cold-blooded hunter, and for one whose rounds cover many cells)
            for (int a = 0; a < k; a++) { intake[a] = sps[a].Intake; half[a] = HalfPrey * DMath.Exp(0.75 * DMath.Log(sps[a].Mass / 35.0)) * (sps[a].Metab / 9.0) / sps[a].Range * areaK; }
            int nLand = 0, nSea = 0;
            for (int a = 0; a < k; a++) { if (sps[a].Habitat == 1) seaSp[nSea++] = a; else landSp[nLand++] = a; }
            var plank = SeaFood(w, yf);   // what the sea's plankton offers each sea cell this step (0..~1.2)
            var shelf = _shelf; var coastD = _coastD;
            for (int a = 0; a < k; a++)
            {
                keepS[a] = DMath.Exp(-Evolution.Pull * (sps[a].Fecundity + 0.5) * dt);
                for (int b = 0; b < k; b++) vuln[a * MaxK + b] = Vulnerable(sps[b].Mass, sps[a].Mass);
            }
            // suitability: what a cell offers (climate, cover, slope) once, then each species' match to it
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c))
                {
                    steep[c] = 0f;
                    for (int ti = 0; ti < nLand; ti++) suit[landSp[ti] * n + c] = 0f;
                    for (int ti = 0; ti < nSea; ti++) { int s = seaSp[ti]; suit[s * n + c] = (float)SeaSuit(P, sps[s], c, P.Coat[s * n + c], shelf[c], coastD[c]); }
                    continue;
                }
                for (int ti = 0; ti < nSea; ti++) suit[seaSp[ti] * n + c] = 0f;
                if (P.Ice[c] > 5f || P.LakeDepth[c] > 0.5f)
                {
                    steep[c] = 0f;
                    for (int ti = 0; ti < nLand; ti++) suit[landSp[ti] * n + c] = 0f;
                    continue;
                }
                double stp = slopes[c];
                steep[c] = (float)stp;
                double tm = P.TMean[c], heat = 1.0 - DMath.SmoothStep(30, 38, tm), forest = P.Forest[c], st = DMath.Clamp01(stp);
                for (int ti = 0; ti < nLand; ti++)
                {
                    int s = landSp[ti];
                    ref var sp = ref sps[s];
                    double cold = DMath.Clamp01((tm - ((double)sp.ColdTol + P.Coat[s * n + c])) / 6.0);
                    double cover = 1.0 - DMath.Min(1.0, DMath.Abs(forest - sp.ForestPref) * 1.4);
                    double slope = 0.5 + 0.5 * (1.0 - DMath.Abs(st - sp.SlopePref));
                    suit[s * n + c] = (float)(cold * heat * DMath.Max(0.15, cover) * slope);
                }
            }
            for (int c = 0; c < n; c++)
            {
                bool land = P.IsLand(c);
                var hs = land ? landSp : seaSp;   // the species of this cell's habitat
                int nh = land ? nLand : nSea;
                if (nh == 0) continue;
                double ha = P.AreaHa(c);
                double warm = 1.0;
                if (land)
                {
                    // what can be eaten this step: the grass above its floor; the woods' shoots and their mast; small fare in the grass
                    // winter is lean: in the cold the grass stops, the shoots are bare and the voles hide; mast lasts longer
                    warm = DMath.SmoothStep(-2.0, 8.0, P.Temp[c]);
                    double lean = 0.2 + 0.8 * warm;
                    food[0] = DMath.Max(0.0, P.Grass[c] - GrassFloor) * GrassMax * ha * EatShare * lean * Density;
                    food[1] = P.Forest[c] * BrowseMax * ha * BrowseRenew * dt * lean * Density;
                    food[2] = P.Forest[c] * MastMax * ha * dt * (0.5 + 0.5 * warm) * Density;
                    // what people gathered here since the animals last ate is gone from their mast (D2-042)
                    if (P.Gathered[c] > 0f) { food[2] = DMath.Max(0.0, food[2] - P.Gathered[c]); P.Gathered[c] = 0f; }
                    food[3] = P.Grass[c] * SmallMax * ha * dt * lean * Density;
                    food[4] = DMath.SmoothStep(0.3, 0.8, steep[c]) * (1.0 - P.Forest[c]) * CragMax * ha * dt * lean * Density;   // crags: ~5-10 % of land
                    food[5] = 0;
                }
                else
                {
                    food[0] = food[1] = food[2] = food[3] = food[4] = 0;
                    food[5] = plank[c] * PlanktonMax * ha * dt * Density;   // the sea: its plankton
                }
                sum[0] = sum[1] = sum[2] = sum[3] = sum[4] = sum[5] = 0;
                // 1. plants: each eats its preferred mix; what one food lacks it makes up from what is left of the others
                for (int hi = 0; hi < nh; hi++)
                {
                    int s = hs[hi];
                    ref var sp = ref sps[s];
                    need[s] = 0; got[s] = 0; meat[s] = 0;
                    double N = P.Abund[s * n + c];
                    if (N <= 0) continue;
                    if (sp.Role == Role.Predator) { meat[s] = N * intake[s] * dt; continue; }
                    double plant = sp.Role == Role.Omnivore ? (sp.Kind == 6 ? 0.6 : 0.7) : 1.0;   // a fox lives more on hares
                    double awake = sp.Kind == 7 ? 0.15 + 0.85 * warm : 1.0;   // a bear sleeps through the cold
                    need[s] = N * intake[s] * dt * plant * awake;
                    if (sp.Prey != 0) meat[s] = N * intake[s] * dt * (1.0 - plant) * awake;
                    sum[0] += need[s] * sp.Grass; sum[1] += need[s] * sp.Browse; sum[2] += need[s] * sp.Mast; sum[3] += need[s] * sp.Small; sum[4] += need[s] * sp.Crag;
                    sum[5] += need[s] * sp.Plankton;
                }
                double left = 0;
                for (int r = 0; r < 6; r++) { f[r] = sum[r] > 0 ? DMath.Min(1.0, food[r] / sum[r]) : 1.0; if (r < 4) left += food[r] - sum[r] * f[r]; }   // only climbers reach the crags
                double unmet = 0;
                for (int hi = 0; hi < nh; hi++)
                {
                    int s = hs[hi];
                    if (need[s] <= 0) continue;
                    ref var sp = ref sps[s];
                    got[s] = need[s] * (sp.Grass * f[0] + sp.Browse * f[1] + sp.Mast * f[2] + sp.Small * f[3] + sp.Crag * f[4] + sp.Plankton * f[5]);
                    unmet += need[s] - got[s];
                }
                // the second helping: what is left of any food, shared by the unmet need (a deer takes grass when shoots run out)
                double f2 = unmet > 1e-9 ? DMath.Min(1.0, left / unmet) : 0.0;
                if (f2 > 0) for (int hi = 0; hi < nh; hi++) { int s = hs[hi]; if (need[s] > 0) got[s] += (need[s] - got[s]) * 0.6 * f2; }
                double leftG = food[0] - sum[0] * f[0];
                grassEaten[c] = sum[0] * f[0] + (left > 0 ? unmet * 0.6 * f2 * leftG / left : 0.0);
                browseEaten[c] = sum[1] * f[1];
                // 2. hunting (Holling II): a hunter meets prey in proportion to the prey it can reach, and is sated at
                // what it can eat; a quarter of the prey (more in the woods) is always out of reach
                // (at sea, the deep water hides fish from the seals and the shoals scatter: a third, more off the shelves)
                double refuge = land ? 0.25 + 0.35 * P.Forest[c] : 0.3 + 0.3 * (1.0 - shelf[c]);
                for (int hi = 0; hi < nh; hi++)
                {
                    int s = hs[hi];
                    if (meat[s] <= 0) continue;
                    ref var sp = ref sps[s];
                    double avail = 0;
                    for (int qi = 0; qi < nh; qi++) { int q = hs[qi]; if ((sp.Prey & (1 << sps[q].Kind)) != 0) avail += (double)P.Abund[q * n + c] * sps[q].Mass * Reach(refuge, steep[c], sps[q].SlopePref) * vuln[s * MaxK + q]; }
                    if (avail <= 0) continue;
                    // half sated when a cell holds HalfPrey kg it can reach: hunters hold their prey near three times that
                    double take = DMath.Min(meat[s] * avail / (avail + half[s]), avail * 0.25);   // a small hunter is sated by less
                    for (int qi = 0; qi < nh; qi++)
                    {
                        int q = hs[qi];
                        if ((sp.Prey & (1 << sps[q].Kind)) == 0) continue;
                        double share = (double)P.Abund[q * n + c] * sps[q].Mass * Reach(refuge, steep[c], sps[q].SlopePref) * vuln[s * MaxK + q] / avail;
                        double eaten = DMath.Min(P.Abund[q * n + c], take * share / sps[q].Mass);
                        if (Diag != null) Diag[q * 8 + 4] += eaten;
                        P.Abund[q * n + c] = (float)(P.Abund[q * n + c] - eaten);
                    }
                    got[s] += take;
                }
                // 3. condition, deaths, births; hunters hold territories (their young find no room where packs are many)
                double tm = P.TMean[c];
                for (int hi = 0; hi < nh; hi++)
                {
                    int s = hs[hi];
                    int i = s * n + c;
                    double N = P.Abund[i];
                    if (N <= 0) continue;
                    ref var sp = ref sps[s];
                    double want = need[s] + meat[s];
                    double fed = want > 0 ? got[s] / want : 1.0;
                    double cond = P.Cond[i];
                    cond = DMath.Clamp01(cond + (fed - 0.75) * 2.0 * dt);
                    double mAge = dt / sp.Lifespan;                               // age
                    double mHunger = cond < 0.45 ? dt * 3.0 * (0.45 - cond) / 0.45 : 0.0;   // hunger
                    double tol = (double)sp.ColdTol + P.Coat[i];                         // this population's coat
                    double mCold = tm < tol ? dt * 0.8 * DMath.Min(1.0, (tol - tm) / 5.0) : 0.0; // cold
                    double mCoat = Evolution.Select(P, sp, i, c, dt, keepS[s]);   // the coat follows the place, slowly
                    double mort = mAge + mHunger + mCold + mCoat;
                    double N0 = N;
                    N *= ExpNeg(mort);
                    if (Diag != null && mort > 0)
                    {
                        double lost = (N0 - N) / mort;
                        Diag[s * 8 + 0] += lost * mAge; Diag[s * 8 + 1] += lost * mHunger; Diag[s * 8 + 2] += lost * mCold; Diag[s * 8 + 3] += lost * mCoat;
                    }
                    // spring: the young of the year, as many as condition (and, for hunters, room) allows
                    double season = P.SinLat[c] >= 0 ? yf : (yf + 0.5) % 1.0;
                    if (season >= 0.12 && season < 0.12 + dt)
                    {
                        // a hunter's range holds so many of its kind - sister kinds of one first kind share it (D2-046): a split
                        // does not make room for more wolves, the sisters compete for it
                        double kin = N;
                        if (sp.Territory > 0) for (int qi = 0; qi < nh; qi++) { int q = hs[qi]; if (q != s && sps[q].Kind == sp.Kind) kin += P.Abund[q * n + c]; }
                        double room = sp.Territory > 0 ? DMath.Clamp01(1.0 - kin / (sp.Territory * areaK)) : 1.0;
                        double young = N * sp.Fecundity * DMath.Clamp01((cond - 0.5) / 0.4) * room;   // only the well fed raise young
                        if (Diag != null) Diag[s * 8 + 5] += young;
                        N += young;
                    }
                    P.Abund[i] = (float)N;
                    P.Cond[i] = (float)cond;
                }
            }
            // 4. the plants eaten (they grow back from less)
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c)) continue;
                P.Gathered[c] = 0f;   // (a land with no animals left still forgets what was gathered)
                double ha = P.AreaHa(c);
                if (grassEaten[c] > 0) P.Grass[c] = (float)DMath.Max(0.0, P.Grass[c] - grassEaten[c] / (GrassMax * ha));
                // browse is the year's shoots and leaves: the woods stand (a heavy browsing slows their spread only)
            }
            // 5. wandering: a share of each cell's animals moves to its neighbours, more to the better ones - and a hunter
            // to where its prey is (no pack wanders far into land its prey has left)
            Span<int> preyOf = stackalloc int[MaxK];
            Span<double> preyW = stackalloc double[MaxK];
            for (int s = 0; s < k; s++)
            {
                ref var sp = ref sps[s];
                if (sp.Role != Role.Predator) continue;
                int np = 0;
                for (int q = 0; q < k; q++) if ((sp.Prey & (1 << sps[q].Kind)) != 0) { preyOf[np] = q * n; preyW[np] = sps[q].Mass * vuln[s * MaxK + q]; np++; }
                double inv = 1.0 / (2.0 * half[s]);
                var ab = P.Abund;
                for (int c = 0; c < n; c++)
                {
                    int i = s * n + c;
                    if (suit[i] <= 0f) continue;
                    double preyB = 0;
                    for (int pi = 0; pi < np; pi++) preyB += ab[preyOf[pi] + c] * preyW[pi];
                    suit[i] = (float)(suit[i] * (0.3 + 0.7 * DMath.Min(1.0, preyB * inv)));
                }
            }
            for (int s = 0; s < k; s++)
            {
                ref var sp = ref sps[s];
                double move = DMath.Min(0.5, sp.Disperse * dt * 4.0);
                for (int c = 0; c < n; c++)
                {
                    int i = s * n + c;
                    double N = P.Abund[i];
                    if (N < 0.01 * areaK) continue;
                    double wsum = 0;
                    for (int q = 0; q < 4; q++)
                    {
                        int nb = g.Nbr4[c * 4 + q];
                        double su = suit[s * n + nb];
                        wt[q] = su * su;
                        wsum += wt[q];
                    }
                    if (wsum <= 0) continue;
                    double out_ = N * move, per = out_ / wsum;
                    fx[i * 4] += out_;
                    double zc = P.Coat[i], dc = P.Drift[i];
                    for (int q = 0; q < 4; q++)
                    {
                        if (wt[q] <= 0) continue;
                        int j = (s * n + g.Nbr4[c * 4 + q]) * 4;
                        double m = per * wt[q];
                        fx[j + 1] += m; fx[j + 2] += m * zc; fx[j + 3] += m * dc;
                    }
                }
            }
            for (int i = 0; i < k * n; i++)
            {
                int j = i * 4;
                double left = fx[j], came = fx[j + 1];
                if (left == 0 && came == 0) continue;
                double stay = P.Abund[i] - left;   // the residents after some left
                double v = stay + came;
                // newcomers bring their coats: the cell's coat is the mix
                if (came > 0 && v > 1e-6)
                {
                    double res = DMath.Max(0.0, stay), all = res + came;
                    P.Coat[i] = (float)((res * P.Coat[i] + fx[j + 2]) / all);
                    P.Drift[i] = (float)((res * P.Drift[i] + fx[j + 3]) / all);   // and their lineage: gene flow keeps a range one kind
                }
                P.Abund[i] = (float)(v < 1e-4 ? 0.0 : v);
                if (P.Cond[i] <= 0f && v > 0) P.Cond[i] = 0.6f;
                fx[j] = 0; fx[j + 1] = 0; fx[j + 2] = 0; fx[j + 3] = 0;
            }
        }

        /// <summary>e^-x for the small rates of a step (x >= 0): a rational form, ~1 % at x = 1, exact enough for deaths.</summary>
        static double ExpNeg(double x) => 1.0 / (1.0 + x * (1.0 + x * (0.5 + x * (1.0 / 6.0 + x / 24.0))));

        /// <summary>How readily a hunter of this size takes prey of that size: a wolf takes deer, seldom a grown aurochs.</summary>
        static double Vulnerable(double prey, double hunter) => DMath.Clamp(DMath.Exp(1.2 * DMath.Log(4.0 * hunter / prey)), 0.05, 1.0);

        /// <summary>The share of a prey a hunter can reach in a cell: not those in the woods' cover, nor a climber on its crags.</summary>
        static double Reach(double refuge, double steep, double slopePref) => DMath.Max(0.05, 1.0 - refuge - 0.6 * DMath.Clamp01(steep) * slopePref);

        /// <summary>Animals of a species on the whole planet.</summary>
        public static double Total(PlanetState P, int s)
        {
            double t = 0;
            for (int c = 0; c < P.Cells; c++) t += P.Abund[s * P.Cells + c];
            return t;
        }
    }
}
