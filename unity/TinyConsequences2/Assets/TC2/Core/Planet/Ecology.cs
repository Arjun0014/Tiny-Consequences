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
        public int Prey;            // bit mask of the kinds it hunts (predators, omnivores)
        public float Disperse;      // share that wanders to neighbouring cells each step
        public float Territory;     // hunters: animals a cell's range holds before the young find no room (0: no limit)
        public int Parent;          // lineage (0 for the first species)
        public long Born;

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Byte(Kind); w.Byte((byte)Role); w.Float(Mass); w.Float(Fecundity); w.Float(Lifespan); w.Float(ColdTol);
            w.Float(ForestPref); w.Float(SlopePref); w.Float(Grass); w.Float(Browse); w.Float(Mast); w.Float(Small); w.Int(Prey); w.Float(Disperse); w.Float(Territory); w.Int(Parent); w.Long(Born);
        }

        public static Species Read(BinReader r) => new Species
        {
            Id = r.Int(), Kind = r.Byte(), Role = (Role)r.Byte(), Mass = r.Float(), Fecundity = r.Float(), Lifespan = r.Float(), ColdTol = r.Float(),
            ForestPref = r.Float(), SlopePref = r.Float(), Grass = r.Float(), Browse = r.Float(), Mast = r.Float(), Small = r.Float(), Prey = r.Int(), Disperse = r.Float(), Territory = r.Float(), Parent = r.Int(), Born = r.Long(),
        };

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(Kind); h.Add((byte)Role); h.Add(Mass); h.Add(Fecundity); h.Add(Lifespan); h.Add(ColdTol);
            h.Add(ForestPref); h.Add(SlopePref); h.Add(Grass); h.Add(Browse); h.Add(Mast); h.Add(Small); h.Add(Prey); h.Add(Disperse); h.Add(Territory); h.Add(Parent); h.Add(Born);
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
        public const double BrowseRenew = 1.0;   // browse is the woods' shoots and leaves: this many times BrowseMax a year
        public const double HalfPrey = 120.0;    // kg of reachable prey in a cell at which a hunter eats half its fill

        struct Template
        {
            public string Name; public Role Role; public float Mass, Fec, Life, Cold, Forest, Slope, Grass, Browse, Mast, Small, Disperse, Territory; public int Prey;
        }

        static readonly Template[] Templates =
        {
            new Template { Name = "deer",    Role = Role.Browser,  Mass = 90,  Fec = 0.8f, Life = 12, Cold = -8,  Forest = 0.55f, Slope = 0.2f, Grass = 0.5f, Browse = 0.5f, Disperse = 0.06f },
            new Template { Name = "hare",    Role = Role.Grazer,   Mass = 3,   Fec = 4.0f, Life = 3,  Cold = -14, Forest = 0.2f,  Slope = 0.2f, Grass = 0.9f, Browse = 0.1f, Disperse = 0.08f },
            new Template { Name = "aurochs", Role = Role.Grazer,   Mass = 600, Fec = 0.35f, Life = 18, Cold = -10, Forest = 0.15f, Slope = 0.0f, Grass = 1.0f, Disperse = 0.05f },
            new Template { Name = "boar",    Role = Role.Omnivore, Mass = 80,  Fec = 2.5f, Life = 9,  Cold = -6,  Forest = 0.75f, Slope = 0.2f, Grass = 0.15f, Mast = 0.85f, Disperse = 0.06f },
            new Template { Name = "ibex",    Role = Role.Grazer,   Mass = 60,  Fec = 0.7f, Life = 14, Cold = -16, Forest = 0.1f,  Slope = 0.9f, Grass = 0.8f, Browse = 0.2f, Disperse = 0.05f },
            new Template { Name = "wolf",    Role = Role.Predator, Mass = 35,  Fec = 1.5f, Life = 9,  Cold = -30, Forest = 0.4f,  Slope = 0.3f, Disperse = 0.10f, Territory = 0.5f, Prey = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) },
            // a fox lives as much on voles and beetles in the grass as on hares
            new Template { Name = "fox",     Role = Role.Omnivore, Mass = 6,   Fec = 2.0f, Life = 5,  Cold = -25, Forest = 0.4f,  Slope = 0.2f, Small = 1.0f, Disperse = 0.10f, Territory = 1.2f, Prey = 1 << 1 },
            new Template { Name = "bear",    Role = Role.Omnivore, Mass = 250, Fec = 0.3f, Life = 22, Cold = -20, Forest = 0.85f, Slope = 0.5f, Browse = 0.25f, Mast = 0.75f, Disperse = 0.04f, Territory = 0.15f, Prey = 1 << 0 },
        };

        public static string KindName(byte kind) => kind < Templates.Length ? Templates[kind].Name : "beast";

        [ThreadStatic] static double[] _grassEaten, _browseEaten, _flow;
        [ThreadStatic] static float[] _suit, _steep;

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
                    Mast = t.Mast, Small = t.Small,
                    Prey = t.Prey, Disperse = t.Disperse, Territory = t.Territory, Born = 0,
                });
                w.History.Register(new EntityRef(EntityKind.Species, id), t.Name, 0);
            }
            P.Abund = new float[k * P.Cells];
            P.Cond = new float[k * P.Cells];
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
            // twenty years before people, the plants growing back between the animals' steps
            long t0 = 0;
            for (int i = 0; i < 80; i++) { w.PlanetSys.GrowVegetation(w, 0.25); Step(w, 0.25, t0); t0 += SimTime.Year / 4; }
        }

        /// <summary>How well a cell suits a species (0..1): climate, cover and slope.</summary>
        public static double Suitability(PlanetState P, in Species sp, int c) => Suitability(P, sp, c, Steepness(P, c));

        static double Suitability(PlanetState P, in Species sp, int c, double steep)
        {
            if (!P.IsLand(c) || P.Ice[c] > 5f || P.LakeDepth[c] > 0.5f) return 0;
            double tm = P.TMean[c];
            double cold = DMath.Clamp01((tm - sp.ColdTol) / 6.0);
            double heat = 1.0 - DMath.SmoothStep(30, 38, tm);
            double cover = 1.0 - DMath.Min(1.0, DMath.Abs(P.Forest[c] - sp.ForestPref) * 1.4);
            double slope = 0.5 + 0.5 * (1.0 - DMath.Abs(DMath.Clamp01(steep) - sp.SlopePref));
            return cold * heat * DMath.Max(0.15, cover) * slope;
        }

        static double Steepness(PlanetState P, int c)
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
            if (_flow == null || _flow.Length != k * n) { _flow = new double[k * n]; _suit = new float[k * n]; }
            if (_steep == null || _steep.Length != n) _steep = new float[n];
            double yf = SimTime.YearFraction(t);
            Span<double> need = stackalloc double[16];   // plant food each wants this step (kg)
            Span<double> got = stackalloc double[16];    // what it got: plants, then meat
            Span<double> meat = stackalloc double[16];   // meat each hunter wants this step (kg)
            Span<double> wt = stackalloc double[4];
            Span<double> food = stackalloc double[4], sum = stackalloc double[4], f = stackalloc double[4];   // per cell, reused
            for (int c = 0; c < n; c++) _steep[c] = (float)Steepness(P, c);
            for (int s = 0; s < k; s++)
            {
                var sp = P.Species[s];
                for (int c = 0; c < n; c++) _suit[s * n + c] = (float)Suitability(P, sp, c, _steep[c]);
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
                sum[0] = sum[1] = sum[2] = sum[3] = 0;
                // 1. plants: each eats its preferred mix; what one food lacks it makes up from what is left of the others
                for (int s = 0; s < k; s++)
                {
                    var sp = P.Species[s];
                    need[s] = 0; got[s] = 0; meat[s] = 0;
                    double N = P.Abund[s * n + c];
                    if (N <= 0) continue;
                    if (sp.Role == Role.Predator) { meat[s] = N * sp.Intake * dt; continue; }
                    double plant = sp.Role == Role.Omnivore ? (sp.Small > 0 ? 0.6 : 0.7) : 1.0;
                    double awake = sp.Kind == 7 ? 0.15 + 0.85 * warm : 1.0;   // a bear sleeps through the cold
                    need[s] = N * sp.Intake * dt * plant * awake;
                    if (sp.Prey != 0) meat[s] = N * sp.Intake * dt * (1.0 - plant) * awake;
                    sum[0] += need[s] * sp.Grass; sum[1] += need[s] * sp.Browse; sum[2] += need[s] * sp.Mast; sum[3] += need[s] * sp.Small;
                }
                double left = 0;
                for (int r = 0; r < 4; r++) { f[r] = sum[r] > 0 ? DMath.Min(1.0, food[r] / sum[r]) : 1.0; left += food[r] - sum[r] * f[r]; }
                double unmet = 0;
                for (int s = 0; s < k; s++)
                {
                    if (need[s] <= 0) continue;
                    var sp = P.Species[s];
                    got[s] = need[s] * (sp.Grass * f[0] + sp.Browse * f[1] + sp.Mast * f[2] + sp.Small * f[3]);
                    unmet += need[s] - got[s];
                }
                // the second helping: what is left of any food, shared by the unmet need (a deer takes grass when shoots run out)
                double f2 = unmet > 1e-9 ? DMath.Min(1.0, left / unmet) : 0.0;
                if (f2 > 0) for (int s = 0; s < k; s++) if (need[s] > 0) got[s] += (need[s] - got[s]) * 0.6 * f2;
                double leftG = food[0] - sum[0] * f[0];
                _grassEaten[c] = sum[0] * f[0] + (left > 0 ? unmet * 0.6 * f2 * leftG / left : 0.0);
                _browseEaten[c] = sum[1] * f[1];
                // 2. hunting (Holling II): a hunter meets prey in proportion to the prey it can reach, and is sated at
                // what it can eat; a quarter of the prey (more in the woods) is always out of reach
                double refuge = 0.25 + 0.35 * P.Forest[c];
                for (int s = 0; s < k; s++)
                {
                    if (meat[s] <= 0) continue;
                    var sp = P.Species[s];
                    double avail = 0;
                    for (int q = 0; q < k; q++) if ((sp.Prey & (1 << P.Species[q].Kind)) != 0) avail += P.Abund[q * n + c] * P.Species[q].Mass * (1.0 - refuge) * Vulnerable(P.Species[q].Mass, sp.Mass);
                    if (avail <= 0) continue;
                    // half sated when a cell holds HalfPrey kg it can reach: hunters hold their prey near three times that
                    double half = HalfPrey * DMath.Exp(0.75 * DMath.Log(sp.Mass / 35.0));   // a small hunter is sated by less
                    double take = DMath.Min(meat[s] * avail / (avail + half), avail * 0.25);
                    for (int q = 0; q < k; q++)
                    {
                        if ((sp.Prey & (1 << P.Species[q].Kind)) == 0) continue;
                        double share = P.Abund[q * n + c] * P.Species[q].Mass * (1.0 - refuge) * Vulnerable(P.Species[q].Mass, sp.Mass) / avail;
                        P.Abund[q * n + c] = (float)DMath.Max(0.0, P.Abund[q * n + c] - take * share / P.Species[q].Mass);
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
                    var sp = P.Species[s];
                    double want = need[s] + meat[s];
                    double fed = want > 0 ? got[s] / want : 1.0;
                    double cond = P.Cond[i];
                    cond = DMath.Clamp01(cond + (fed - 0.75) * 2.0 * dt);
                    double mort = dt / sp.Lifespan;                               // age
                    if (cond < 0.45) mort += dt * 3.0 * (0.45 - cond) / 0.45;      // hunger
                    if (tm < sp.ColdTol) mort += dt * 0.8 * DMath.Min(1.0, (sp.ColdTol - tm) / 5.0); // cold
                    N *= DMath.Exp(-mort);
                    // spring: the young of the year, as many as condition (and, for hunters, room) allows
                    double season = P.SinLat[c] >= 0 ? yf : (yf + 0.5) % 1.0;
                    if (season >= 0.12 && season < 0.12 + dt)
                    {
                        double room = sp.Territory > 0 ? DMath.Clamp01(1.0 - N / sp.Territory) : 1.0;
                        N += N * sp.Fecundity * DMath.Clamp01((cond - 0.5) / 0.4) * room;   // only the well fed raise young
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
                if (_grassEaten[c] > 0) P.Grass[c] = (float)DMath.Max(0.0, P.Grass[c] - _grassEaten[c] / (GrassMax * ha));
                // browse is the year's shoots and leaves: the woods stand (a heavy browsing slows their spread only)
            }
            // 5. wandering: a share of each cell's animals moves to its neighbours, more to the better ones
            Array.Clear(_flow, 0, k * n);
            for (int s = 0; s < k; s++)
            {
                var sp = P.Species[s];
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
                        double su = _suit[s * n + nb];
                        wt[q] = su * su;
                        wsum += wt[q];
                    }
                    if (wsum <= 0) continue;
                    double out_ = N * move;
                    _flow[i] -= out_;
                    for (int q = 0; q < 4; q++)
                    {
                        if (wt[q] <= 0) continue;
                        int nb = g.Nbr4[c * 4 + q];
                        _flow[s * n + nb] += out_ * wt[q] / wsum;
                    }
                }
            }
            for (int i = 0; i < k * n; i++)
            {
                if (_flow[i] == 0) continue;
                double v = P.Abund[i] + _flow[i];
                P.Abund[i] = (float)(v < 1e-4 ? 0.0 : v);
                if (P.Cond[i] <= 0f && v > 0) P.Cond[i] = 0.6f;
            }
        }

        /// <summary>How readily a hunter of this size takes prey of that size: a wolf takes deer, seldom a grown aurochs.</summary>
        static double Vulnerable(double prey, double hunter) => DMath.Clamp(DMath.Exp(0.8 * DMath.Log(4.0 * hunter / prey)), 0.05, 1.0);

        /// <summary>Animals of a species on the whole planet.</summary>
        public static double Total(PlanetState P, int s)
        {
            double t = 0;
            for (int c = 0; c < P.Cells; c++) t += P.Abund[s * P.Cells + c];
            return t;
        }
    }
}
