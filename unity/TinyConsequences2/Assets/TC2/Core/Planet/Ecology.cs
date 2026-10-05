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
        public int Prey;            // bit mask of the kinds it hunts (predators, omnivores)
        public float Disperse;      // share that wanders to neighbouring cells each step
        public float Territory;     // hunters: animals a cell's range holds before the young find no room (0: no limit)
        public int Parent;          // lineage (0 for the first species)
        public long Born;
        public long Gone;           // when it died out (0: alive)

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Byte(Kind); w.Byte((byte)Role); w.Float(Mass); w.Float(Fecundity); w.Float(Lifespan); w.Float(ColdTol);
            w.Float(ForestPref); w.Float(SlopePref); w.Float(Grass); w.Float(Browse); w.Float(Mast); w.Float(Small); w.Float(Crag); w.Int(Prey); w.Float(Disperse); w.Float(Territory); w.Int(Parent); w.Long(Born); w.Long(Gone);
        }

        public static Species Read(BinReader r) => new Species
        {
            Id = r.Int(), Kind = r.Byte(), Role = (Role)r.Byte(), Mass = r.Float(), Fecundity = r.Float(), Lifespan = r.Float(), ColdTol = r.Float(),
            ForestPref = r.Float(), SlopePref = r.Float(), Grass = r.Float(), Browse = r.Float(), Mast = r.Float(), Small = r.Float(), Crag = r.Float(), Prey = r.Int(), Disperse = r.Float(), Territory = r.Float(), Parent = r.Int(), Born = r.Long(), Gone = r.Long(),
        };

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(Kind); h.Add((byte)Role); h.Add(Mass); h.Add(Fecundity); h.Add(Lifespan); h.Add(ColdTol);
            h.Add(ForestPref); h.Add(SlopePref); h.Add(Grass); h.Add(Browse); h.Add(Mast); h.Add(Small); h.Add(Crag); h.Add(Prey); h.Add(Disperse); h.Add(Territory); h.Add(Parent); h.Add(Born); h.Add(Gone);
        }

        /// <summary>Plant food it needs a year (kg dry matter), or meat for a predator: ~ mass^0.75.</summary>
        public double Intake => 9.0 * DMath.Exp(0.75 * DMath.Log(Mass));
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
        public const double GrassMax = 1000.0;   // kg of grass on a hectare under full cover
        public const double BrowseMax = 600.0;   // kg of browse (shoots, leaves, mast) a hectare of full woods offers
        public const double EatShare = 0.3;      // at most this share of the standing grass above its floor is eaten in a step
        public const double GrassFloor = 0.25;   // the bottom of the sward (roots, short growth) cannot be grazed away
        public const double MastMax = 350.0;     // kg of mast, roots and fungi a hectare of full woods offers a year
        public const double SmallMax = 60.0;     // kg of voles and beetles a hectare of full grass offers a year
        public const double CragMax = 150.0;     // kg of herbs and lichens a hectare of steep open ground offers a year (climbers only)
        public const double BrowseRenew = 1.0;   // browse is the woods' shoots and leaves: this many times BrowseMax a year
        public const double HalfPrey = 120.0;    // kg of reachable prey in a cell at which a hunter eats half its fill

        struct Template
        {
            public string Name; public Role Role; public float Mass, Fec, Life, Cold, Forest, Slope, Grass, Browse, Mast, Small, Crag, Disperse, Territory; public int Prey;
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
        };

        public static string KindName(byte kind) => kind < Templates.Length ? Templates[kind].Name : "beast";

        [ThreadStatic] static double[] _grassEaten, _browseEaten, _flow, _in, _inZ, _inD;
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
                    Mast = t.Mast, Small = t.Small, Crag = t.Crag,
                    Prey = t.Prey, Disperse = t.Disperse, Territory = t.Territory, Born = 0,
                });
                w.History.Register(new EntityRef(EntityKind.Species, id), t.Name, 0);
            }
            P.Abund = new float[k * P.Cells];
            P.Cond = new float[k * P.Cells];
            P.Coat = new float[k * P.Cells];
            P.Drift = new float[k * P.Cells];
            // a few of each on every land cell they can live in; the spin-up spreads them to what the land holds
            for (int s = 0; s < k; s++)
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c)) continue;
                    double suit = Suitability(P, P.Species[s], c);
                    if (suit <= 0) continue;
                    double start = suit * (P.Species[s].Role == Role.Predator ? 0.3 : 2.0) * DMath.Exp(-0.25 * DMath.Log(P.Species[s].Mass / 50.0));
                    if (P.Species[s].Territory > 0) start = DMath.Min(start, 0.5 * P.Species[s].Territory);   // hunters start within their ranges
                    P.Abund[s * P.Cells + c] = (float)start;
                    P.Cond[s * P.Cells + c] = 0.7f;
                }
            // twenty years before people, the plants growing back between the animals' steps. An untouched land booms and
            // busts (hares eat the grass down, foxes starve): refuges keep a hundredth of each kind's first numbers through
            // the spin-up only - the world is older than its history; from the first day on, a kind can die out
            var refuge = new float[k * P.Cells];
            for (int i = 0; i < refuge.Length; i++) refuge[i] = 0.01f * P.Abund[i];
            long t0 = 0;
            for (int i = 0; i < 80; i++)
            {
                w.PlanetSys.GrowVegetation(w, 0.25); Step(w, 0.25, t0); t0 += SimTime.Year / 4;
                for (int j = 0; j < refuge.Length; j++)
                    if (P.Abund[j] < refuge[j]) { P.Abund[j] = refuge[j]; if (P.Cond[j] < 0.5f) P.Cond[j] = 0.5f; }
                if (Diag != null) { var tot = new double[k]; for (int s = 0; s < k; s++) tot[s] = Total(P, s); DiagSpin.Add(tot); }
            }
        }

        /// <summary>How well a cell suits a species (0..1): climate, cover and slope.</summary>
        public static double Suitability(PlanetState P, in Species sp, int c) => Suitability(P, sp, c, Steepness(P, c), 0.0);

        static double Suitability(PlanetState P, in Species sp, int c, double steep, double coat)
        {
            if (!P.IsLand(c) || P.Ice[c] > 5f || P.LakeDepth[c] > 0.5f) return 0;
            double tm = P.TMean[c];
            double cold = DMath.Clamp01((tm - (sp.ColdTol + coat)) / 6.0);
            double heat = 1.0 - DMath.SmoothStep(30, 38, tm);
            double cover = 1.0 - DMath.Min(1.0, DMath.Abs(P.Forest[c] - sp.ForestPref) * 1.4);
            double slope = 0.5 + 0.5 * (1.0 - DMath.Abs(DMath.Clamp01(steep) - sp.SlopePref));
            return cold * heat * DMath.Max(0.15, cover) * slope;
        }

        public static double Steepness(PlanetState P, int c)
        {
            var g = P.Grid;
            double hi = P.Elev[c], lo = P.Elev[c];
            for (int k = 0; k < 4; k++) { double e = P.Elev[g.Nbr4[c * 4 + k]]; if (e > hi) hi = e; if (e < lo) lo = e; }
            return (hi - lo) / (2.0 * P.CellSize);
        }

        /// <summary>A step of the wild animals (dt in years; t the time, for the breeding season).</summary>
        public static void Step(Simulation.World w, double dt, long t)
        {
            var P = w.Planet;
            var g = P.Grid;
            int n = P.Cells, k = P.Species.Count;
            if (k == 0 || P.Abund == null) return;
            if (_grassEaten == null || _grassEaten.Length != n) { _grassEaten = new double[n]; _browseEaten = new double[n]; }
            if (_flow == null || _flow.Length != k * n) { _flow = new double[k * n]; _suit = new float[k * n]; _in = new double[k * n]; _inZ = new double[k * n]; _inD = new double[k * n]; }
            if (_steep == null || _steep.Length != n) _steep = new float[n];
            double yf = SimTime.YearFraction(t);
            Span<double> need = stackalloc double[16];   // plant food each wants this step (kg)
            Span<double> got = stackalloc double[16];    // what it got: plants, then meat
            Span<double> meat = stackalloc double[16];   // meat each hunter wants this step (kg)
            Span<double> wt = stackalloc double[4];
            Span<double> food = stackalloc double[5], sum = stackalloc double[5], f = stackalloc double[5];   // per cell, reused
            if (_vuln == null) { _vuln = new double[16 * 16]; _keep = new double[16]; _sp = new Species[16]; }
            for (int a = 0; a < k; a++) _sp[a] = P.Species[a];   // a List's indexer copies the struct: read them from an array
            // [ThreadStatic] fields cost a thread-local lookup at every touch: the loops below use locals
            var sps = _sp; var suit = _suit; var steep = _steep; var vuln = _vuln; var keepS = _keep;
            var flow = _flow; var inflow = _in; var inZ = _inZ; var inD = _inD; var grassEaten = _grassEaten; var browseEaten = _browseEaten;
            // what each species eats a year and the prey that sates it half: computed once a step, not in every cell
            Span<double> intake = stackalloc double[16], half = stackalloc double[16];
            for (int a = 0; a < k; a++) { intake[a] = sps[a].Intake; half[a] = HalfPrey * DMath.Exp(0.75 * DMath.Log(sps[a].Mass / 35.0)); }
            for (int a = 0; a < k; a++)
            {
                keepS[a] = DMath.Exp(-Evolution.Pull * (sps[a].Fecundity + 0.5) * dt);
                for (int b = 0; b < k; b++) vuln[a * 16 + b] = Vulnerable(sps[b].Mass, sps[a].Mass);
            }
            // suitability: what a cell offers (climate, cover, slope) once, then each species' match to it
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c) || P.Ice[c] > 5f || P.LakeDepth[c] > 0.5f)
                {
                    steep[c] = 0f;
                    for (int s = 0; s < k; s++) suit[s * n + c] = 0f;
                    continue;
                }
                double stp = Steepness(P, c);
                steep[c] = (float)stp;
                double tm = P.TMean[c], heat = 1.0 - DMath.SmoothStep(30, 38, tm), forest = P.Forest[c], st = DMath.Clamp01(stp);
                for (int s = 0; s < k; s++)
                {
                    ref var sp = ref sps[s];
                    double cold = DMath.Clamp01((tm - (sp.ColdTol + P.Coat[s * n + c])) / 6.0);
                    double cover = 1.0 - DMath.Min(1.0, DMath.Abs(forest - sp.ForestPref) * 1.4);
                    double slope = 0.5 + 0.5 * (1.0 - DMath.Abs(st - sp.SlopePref));
                    suit[s * n + c] = (float)(cold * heat * DMath.Max(0.15, cover) * slope);
                }
            }
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c)) continue;
                double ha = P.AreaHa(c);
                // what can be eaten this step: the grass above its floor; the woods' shoots and their mast; small fare in the grass
                // winter is lean: in the cold the grass stops, the shoots are bare and the voles hide; mast lasts longer
                double warm = DMath.SmoothStep(-2.0, 8.0, P.Temp[c]);
                double lean = 0.2 + 0.8 * warm;
                food[0] = DMath.Max(0.0, P.Grass[c] - GrassFloor) * GrassMax * ha * EatShare * lean;
                food[1] = P.Forest[c] * BrowseMax * ha * BrowseRenew * dt * lean;
                food[2] = P.Forest[c] * MastMax * ha * dt * (0.5 + 0.5 * warm);
                food[3] = P.Grass[c] * SmallMax * ha * dt * lean;
                food[4] = DMath.SmoothStep(0.3, 0.8, steep[c]) * (1.0 - P.Forest[c]) * CragMax * ha * dt * lean;   // crags: ~5-10 % of land
                sum[0] = sum[1] = sum[2] = sum[3] = sum[4] = 0;
                // 1. plants: each eats its preferred mix; what one food lacks it makes up from what is left of the others
                for (int s = 0; s < k; s++)
                {
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
                }
                double left = 0;
                for (int r = 0; r < 5; r++) { f[r] = sum[r] > 0 ? DMath.Min(1.0, food[r] / sum[r]) : 1.0; if (r < 4) left += food[r] - sum[r] * f[r]; }   // only climbers reach the crags
                double unmet = 0;
                for (int s = 0; s < k; s++)
                {
                    if (need[s] <= 0) continue;
                    ref var sp = ref sps[s];
                    got[s] = need[s] * (sp.Grass * f[0] + sp.Browse * f[1] + sp.Mast * f[2] + sp.Small * f[3] + sp.Crag * f[4]);
                    unmet += need[s] - got[s];
                }
                // the second helping: what is left of any food, shared by the unmet need (a deer takes grass when shoots run out)
                double f2 = unmet > 1e-9 ? DMath.Min(1.0, left / unmet) : 0.0;
                if (f2 > 0) for (int s = 0; s < k; s++) if (need[s] > 0) got[s] += (need[s] - got[s]) * 0.6 * f2;
                double leftG = food[0] - sum[0] * f[0];
                grassEaten[c] = sum[0] * f[0] + (left > 0 ? unmet * 0.6 * f2 * leftG / left : 0.0);
                browseEaten[c] = sum[1] * f[1];
                // 2. hunting (Holling II): a hunter meets prey in proportion to the prey it can reach, and is sated at
                // what it can eat; a quarter of the prey (more in the woods) is always out of reach
                double refuge = 0.25 + 0.35 * P.Forest[c];
                for (int s = 0; s < k; s++)
                {
                    if (meat[s] <= 0) continue;
                    ref var sp = ref sps[s];
                    double avail = 0;
                    for (int q = 0; q < k; q++) if ((sp.Prey & (1 << sps[q].Kind)) != 0) avail += P.Abund[q * n + c] * sps[q].Mass * Reach(refuge, steep[c], sps[q].SlopePref) * vuln[s * 16 + q];
                    if (avail <= 0) continue;
                    // half sated when a cell holds HalfPrey kg it can reach: hunters hold their prey near three times that
                    double take = DMath.Min(meat[s] * avail / (avail + half[s]), avail * 0.25);   // a small hunter is sated by less
                    for (int q = 0; q < k; q++)
                    {
                        if ((sp.Prey & (1 << sps[q].Kind)) == 0) continue;
                        double share = P.Abund[q * n + c] * sps[q].Mass * Reach(refuge, steep[c], sps[q].SlopePref) * vuln[s * 16 + q] / avail;
                        double eaten = DMath.Min(P.Abund[q * n + c], take * share / sps[q].Mass);
                        if (Diag != null) Diag[q * 8 + 4] += eaten;
                        P.Abund[q * n + c] = (float)(P.Abund[q * n + c] - eaten);
                    }
                    got[s] += take;
                }
                // 3. condition, deaths, births; hunters hold territories (their young find no room where packs are many)
                double tm = P.TMean[c];
                for (int s = 0; s < k; s++)
                {
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
                    double tol = sp.ColdTol + P.Coat[i];                         // this population's coat
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
                        double room = sp.Territory > 0 ? DMath.Clamp01(1.0 - N / sp.Territory) : 1.0;
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
                double ha = P.AreaHa(c);
                if (grassEaten[c] > 0) P.Grass[c] = (float)DMath.Max(0.0, P.Grass[c] - grassEaten[c] / (GrassMax * ha));
                // browse is the year's shoots and leaves: the woods stand (a heavy browsing slows their spread only)
            }
            // 5. wandering: a share of each cell's animals moves to its neighbours, more to the better ones
            Array.Clear(flow, 0, k * n); Array.Clear(inflow, 0, k * n); Array.Clear(inZ, 0, k * n); Array.Clear(inD, 0, k * n);
            for (int s = 0; s < k; s++)
            {
                ref var sp = ref sps[s];
                double move = DMath.Min(0.5, sp.Disperse * dt * 4.0);
                for (int c = 0; c < n; c++)
                {
                    int i = s * n + c;
                    double N = P.Abund[i];
                    if (N < 0.01) continue;
                    double wsum = 0;
                    for (int q = 0; q < 4; q++)
                    {
                        int nb = g.Nbr4[c * 4 + q];
                        double su = suit[s * n + nb];
                        wt[q] = su * su;
                        wsum += wt[q];
                    }
                    if (wsum <= 0) continue;
                    double out_ = N * move;
                    flow[i] -= out_;
                    double zc = P.Coat[i], dc = P.Drift[i];
                    for (int q = 0; q < 4; q++)
                    {
                        if (wt[q] <= 0) continue;
                        int nb = g.Nbr4[c * 4 + q];
                        double m = out_ * wt[q] / wsum;
                        flow[s * n + nb] += m;
                        inflow[s * n + nb] += m; inZ[s * n + nb] += m * zc; inD[s * n + nb] += m * dc;
                    }
                }
            }
            for (int i = 0; i < k * n; i++)
            {
                if (flow[i] == 0) continue;
                double stay = P.Abund[i] + (flow[i] - inflow[i]);   // the residents after some left
                double v = P.Abund[i] + flow[i];
                // newcomers bring their coats: the cell's coat is the mix
                if (inflow[i] > 0 && v > 1e-6)
                {
                    double res = DMath.Max(0.0, stay), all = res + inflow[i];
                    P.Coat[i] = (float)((res * P.Coat[i] + inZ[i]) / all);
                    P.Drift[i] = (float)((res * P.Drift[i] + inD[i]) / all);   // and their lineage: gene flow keeps a range one kind
                }
                P.Abund[i] = (float)(v < 1e-4 ? 0.0 : v);
                if (P.Cond[i] <= 0f && v > 0) P.Cond[i] = 0.6f;
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
