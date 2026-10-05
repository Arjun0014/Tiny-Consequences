using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Food from the wild (D2-042): game, fish and the woods' mast, taken from the same fields the ecology steps.
    /// Each day a village surveys its grounds (what the land and sea within reach hold now); a person who goes out picks
    /// one of the best and takes what they catch there - whole animals from the abundance fields, fish from the sea's,
    /// mast that the boar and bears then do not find. ACTIVITY (an activity step) and COARSE (a day's hours) call the same
    /// functions: one land, two resolutions.
    /// </summary>
    public sealed class WildFood
    {
        public const int Hunting = 0, Fishing = 1, Gathering = 2;
        public const double HuntReach = 1300, FishReach = 900, GatherReach = 750;   // m from the village
        // live kg an hour for an ordinary hunter (skill 0.5) on a ground of B reachable kg: HuntRate B / (B + HuntHalf)
        public const double HuntRate = 2.8, HuntHalf = 3000.0 * Ecology.Density;
        public const double FishRate = 3.0, FishHalf = 1500.0 * Ecology.Density;
        public const double GatherRate = 0.8, GatherHalf = 150.0 * Ecology.Density;
        public const double MeatShare = 0.5, FishShare = 0.6;     // edible share of the live weight
        public const double FuPerKg = 0.65, FuPerKgMast = 0.9;    // food units per kg of meat or fish / of nuts, roots, berries
        public const double PeopleMastShare = 0.6;                // of a step's mast, what people can find (the rest is the animals')
        public const double WindowYears = 0.25;                   // the ecology's step: what was gathered is forgotten at each

        World _w;
        readonly int[] _ground = new int[9];
        // this call's game and fish (a List's indexer copies each struct: read them once into small tables)
        readonly int[] _gq = new int[Ecology.MaxK], _fq = new int[Ecology.MaxK];
        readonly double[] _gm = new double[Ecology.MaxK], _gs = new double[Ecology.MaxK], _gk = new double[Ecology.MaxK];
        readonly double[] _fm = new double[Ecology.MaxK], _fr = new double[Ecology.MaxK];
        readonly double[] _share = new double[9 * Ecology.MaxK];   // a hunt: each ground cell's reachable animals of each kind
        int _gN, _fN;
        readonly Dictionary<int, Reach> _reach = new Dictionary<int, Reach>();   // per settlement, by its cell (geometry only)
        readonly List<int> _scratch = new List<int>();

        sealed class Reach { public int Cell = -1; public int[] Cells; public float[] Dist; }

        public void Bind(World w) { _w = w; _reach.Clear(); }

        // ------------------------------------------------------------------ what counts as food
        public static bool IsGame(in Species sp) => sp.Habitat == 0 && sp.Gone == 0 && sp.Role != Role.Predator && sp.Prey == 0 && sp.Mass >= 2f;
        public static bool IsFish(in Species sp) => sp.Habitat == 1 && sp.Gone == 0 && sp.Metab < 5f;

        /// <summary>The share of a fish people reach from the shore and small boats: shoals of the shelves more than the ocean's.</summary>
        static double ReachF(in Species sp) => DMath.Max(0.1, 1.0 - 0.85 * sp.Offshore);

        /// <summary>Mast, roots and berries people can still find in a cell before the animals next eat (kg).</summary>
        public static double MastLeft(PlanetState P, int c)
        {
            if (!P.IsLand(c) || P.Ice[c] > 5f) return 0;
            double warm = DMath.SmoothStep(-2.0, 8.0, P.Temp[c]);
            double flow = P.Forest[c] * Ecology.MastMax * Ecology.Density * P.AreaHa(c) * WindowYears * (0.5 + 0.5 * warm) * PeopleMastShare;
            return DMath.Max(0.0, flow - P.Gathered[c]);
        }

        /// <summary>This world's game and fish now (kinds come and go): read once per call into small tables.</summary>
        void Kinds()
        {
            var P = _w.Planet;
            _gN = 0; _fN = 0;
            for (int q = 0; q < P.Species.Count; q++)
            {
                var sp = P.Species[q];
                if (IsGame(sp)) { _gq[_gN] = q; _gm[_gN] = sp.Mass; _gs[_gN] = sp.SlopePref; _gk[_gN] = sp.Kind; _gN++; }
                else if (IsFish(sp)) { _fq[_fN] = q; _fm[_fN] = sp.Mass; _fr[_fN] = ReachF(sp); _fN++; }
            }
        }

        /// <summary>People's reach of a game kind in a cell: its cover (woods hide game) and steepness (climbers keep to crags).</summary>
        static double ReachIn(double cover, double steep, double slopePref) => DMath.Max(0.1, 1.0 - cover - 0.6 * steep * slopePref);

        double GameKg(int c)
        {
            var P = _w.Planet; int n = P.Cells;
            if (!P.IsLand(c)) return 0;
            double cover = 0.15 + 0.3 * P.Forest[c], steep = -1, v = 0;
            for (int i = 0; i < _gN; i++)
            {
                double a = P.Abund[_gq[i] * n + c];
                if (a <= 0) continue;
                if (steep < 0) steep = DMath.Clamp01(Ecology.Steepness(P, c));
                v += a * _gm[i] * ReachIn(cover, steep, _gs[i]);
            }
            return v;
        }

        double FishKg(int c)
        {
            var P = _w.Planet; int n = P.Cells;
            if (P.IsLand(c)) return 0;
            double v = 0;
            for (int i = 0; i < _fN; i++)
            {
                double a = P.Abund[_fq[i] * n + c];
                if (a > 0) v += a * _fm[i] * _fr[i];
            }
            return v;
        }

        /// <summary>The cells of a ground: its centre and those of its eight neighbours in the same habitat.</summary>
        int GroundCells(int ground, bool land)
        {
            var P = _w.Planet; var g = P.Grid;
            int k = 0;
            _ground[k++] = ground;
            for (int j = 0; j < 8; j++) { int nb = g.Nbr8[ground * 8 + j]; if (P.IsLand(nb) == land) _ground[k++] = nb; }
            return k;
        }

        // ------------------------------------------------------------------ the daily survey
        Reach ReachOf(int sid)
        {
            ref var s = ref _w.Settlements.Get(sid);
            if (!_reach.TryGetValue(sid, out var r)) { r = new Reach(); _reach[sid] = r; }
            if (r.Cell == s.Cell) return r;
            // every cell within the farthest reach, with its distance from the village (the grid's geometry only)
            var P = _w.Planet;
            _w.Hazards.CellsWithin(_w, s.Cell, HuntReach, _scratch);
            r.Cell = s.Cell;
            r.Cells = _scratch.ToArray();
            r.Dist = new float[r.Cells.Length];
            for (int i = 0; i < r.Cells.Length; i++) r.Dist[i] = (float)Vec3d.ArcDistance(P.Grid.Center[s.Cell], P.Grid.Center[r.Cells[i]], P.Radius);
            return r;
        }

        /// <summary>
        /// A village looks over its grounds (each day, at its midnight): the three best hunting grounds, fishing grounds and
        /// woods for gathering within reach, by what they hold now and how far they are, and what a day there is worth.
        /// </summary>
        public void Survey(int sid)
        {
            var P = _w.Planet;
            Kinds();
            var r = ReachOf(sid);
            Grounds hunt = Grounds.Empty, fish = Grounds.Empty, gath = Grounds.Empty;
            for (int i = 0; i < r.Cells.Length; i++)
            {
                int c = r.Cells[i]; double d = r.Dist[i];
                if (P.IsLand(c))
                {
                    double gk = GameKg(c);
                    if (gk > 0) hunt.Offer(c, gk, gk / (1.0 + d / 700.0));
                    if (d <= GatherReach) { double m = MastLeft(P, c); if (m > 0) gath.Offer(c, m, m / (1.0 + d / 400.0)); }
                }
                else if (d <= FishReach)
                {
                    double fk = FishKg(c);
                    if (fk > 0) fish.Offer(c, fk, fk / (1.0 + d / 500.0));
                }
            }
            hunt.Expect = hunt.A >= 0 ? (float)ExpectHunt(hunt.A) : 0f;
            fish.Expect = fish.A >= 0 ? (float)ExpectFish(fish.A) : 0f;
            gath.Expect = gath.A >= 0 ? (float)ExpectGather(gath.A) : 0f;
            ref var s = ref _w.Settlements.Get(sid);
            s.Hunt = hunt; s.Fish = fish; s.Gath = gath;
        }

        /// <summary>FU an hour an ordinary hunter (skill 0.5) can expect on a ground.</summary>
        public double ExpectHunt(int ground)
        {
            Kinds();
            double B = 0; int k = GroundCells(ground, true);
            for (int i = 0; i < k; i++) B += GameKg(_ground[i]);
            return HuntRate * B / (B + HuntHalf) * MeatShare * FuPerKg;
        }

        public double ExpectFish(int ground)
        {
            Kinds();
            double F = 0; int k = GroundCells(ground, false);
            for (int i = 0; i < k; i++) F += FishKg(_ground[i]);
            return FishRate * F / (F + FishHalf) * FishShare * FuPerKg;
        }

        public double ExpectGather(int ground)
        {
            var P = _w.Planet;
            double A = 0; int k = GroundCells(ground, true);
            for (int i = 0; i < k; i++) A += MastLeft(P, _ground[i]);
            return GatherRate * A / (A + GatherHalf) * FuPerKgMast;
        }

        /// <summary>One of a village's three best grounds for a livelihood (richer more often), or -1.</summary>
        public static int PickGround(in Grounds g, ref RandomSequence rs)
        {
            if (g.A < 0) return -1;
            double wa = (double)g.Va * g.Va, wb = g.B >= 0 ? (double)g.Vb * g.Vb : 0, wc = g.C >= 0 ? (double)g.Vc * g.Vc : 0;
            double u = rs.NextUnit() * (wa + wb + wc);
            if (u < wa || wb + wc <= 0) return g.A;
            if (u < wa + wb || wc <= 0) return g.B;
            return g.C;
        }

        // ------------------------------------------------------------------ the catch
        /// <summary>
        /// A hunt (D2-042): over the ground's reachable game B the hunter can expect HuntRate (0.5 + skill) B / (B + half)
        /// live kg an hour; kills are whole animals, a keyed Poisson count per species and cell by its share of B, taken out of
        /// the abundance field. Returns the FU of meat (and the kind of the largest kill, -1 none).
        /// </summary>
        public double Hunt(int ground, double hours, double skill, ulong key, out int mainKind)
        {
            mainKind = -1;
            var P = _w.Planet; int n = P.Cells;
            if (ground < 0 || hours <= 0 || !P.IsLand(ground)) return 0;
            Kinds();
            int k = GroundCells(ground, true);
            double B = 0;
            for (int i = 0; i < k; i++) B += GameKg(_ground[i]);
            if (B <= 0) return 0;
            double expectKg = DMath.Min(hours * HuntRate * (0.5 + skill) * B / (B + HuntHalf), 0.3 * B);
            var rs = new RandomSequence(key);
            double kg = 0, biggest = 0;
            // each kind's kills over the whole ground are one Poisson count (most hunts take none of a kind); any kills are then
            // placed among the ground's cells by each cell's share of that kind's reachable animals - the same distribution as
            // a count per cell, with a draw per kind instead of per kind and cell
            for (int i = 0; i < k; i++)
            {
                int c = _ground[i];
                double cover = 0.15 + 0.3 * P.Forest[c], steep = DMath.Clamp01(Ecology.Steepness(P, c));
                for (int j = 0; j < _gN; j++)
                {
                    double a = P.Abund[_gq[j] * n + c];
                    _share[i * Ecology.MaxK + j] = a > 0 ? a * _gm[j] * ReachIn(cover, steep, _gs[j]) : 0.0;
                }
            }
            for (int j = 0; j < _gN; j++)
            {
                double bj = 0;
                for (int i = 0; i < k; i++) bj += _share[i * Ecology.MaxK + j];
                if (bj <= 0) continue;
                int kills = Poisson(expectKg * bj / B / _gm[j], ref rs);
                int q = _gq[j];
                for (int x = 0; x < kills; x++)
                {
                    // which cell: by its share of what is left of this kind on the ground
                    double u = rs.NextUnit() * bj, acc = 0;
                    int pick = -1;
                    for (int i = 0; i < k; i++) { acc += _share[i * Ecology.MaxK + j]; if (u < acc) { pick = i; break; } }
                    if (pick < 0) break;
                    int c = _ground[pick];
                    double a = P.Abund[q * n + c];
                    if (a <= 0) continue;
                    // a whole animal; where the field holds less than one, what it holds (the field is the truth)
                    double taken = DMath.Min(1.0, a);
                    P.Abund[q * n + c] = (float)(a - taken);
                    double was = _share[pick * Ecology.MaxK + j];
                    double now = a - taken > 0 ? was * (a - taken) / a : 0.0;
                    _share[pick * Ecology.MaxK + j] = now; bj -= was - now;
                    kg += taken * _gm[j];
                    if (bj <= 0) break;
                }
                double kindKg = 0;   // (the largest catch of the hunt names what they carry home)
                if (kills > 0) { kindKg = kills * _gm[j]; if (kindKg > biggest) { biggest = kindKg; mainKind = (int)_gk[j]; } }
            }
            _w.Metrics.GameKg += kg;
            return kg * MeatShare * FuPerKg;
        }

        /// <summary>A day's fishing: shoals are many, so the catch is continuous kg with the luck of the day, taken from the fields.</summary>
        public double Fish(int ground, double hours, double skill, ulong key)
        {
            var P = _w.Planet; int n = P.Cells;
            if (ground < 0 || hours <= 0 || P.IsLand(ground)) return 0;
            Kinds();
            int k = GroundCells(ground, false);
            double F = 0;
            for (int i = 0; i < k; i++) F += FishKg(_ground[i]);
            if (F <= 0) return 0;
            double luck = 0.5 + KeyedRng.Unit(key);
            double kg = DMath.Min(hours * FishRate * (0.5 + skill) * F / (F + FishHalf) * luck, 0.2 * F);
            for (int i = 0; i < k; i++)
            {
                int c = _ground[i];
                for (int j = 0; j < _fN; j++)
                {
                    int q = _fq[j];
                    double a = P.Abund[q * n + c];
                    if (a <= 0) continue;
                    double take = kg * (a * _fm[j] * _fr[j] / F) / _fm[j];
                    P.Abund[q * n + c] = (float)DMath.Max(0.0, a - take);
                }
            }
            _w.Metrics.FishKg += kg;
            return kg * FishShare * FuPerKg;
        }

        /// <summary>Gathering in the woods: nuts, roots, berries and fungi the animals will then not find (P.Gathered).</summary>
        public double Gather(int ground, double hours, double health, ulong key)
        {
            var P = _w.Planet;
            if (ground < 0 || hours <= 0 || !P.IsLand(ground)) return 0;
            int k = GroundCells(ground, true);
            double A = 0;
            for (int i = 0; i < k; i++) A += MastLeft(P, _ground[i]);
            if (A <= 0) return 0;
            double luck = 0.7 + 0.6 * KeyedRng.Unit(key);
            double kg = DMath.Min(hours * GatherRate * (0.6 + 0.4 * health) * A / (A + GatherHalf) * luck, 0.5 * A);
            for (int i = 0; i < k; i++)
            {
                int c = _ground[i];
                double left = MastLeft(P, c);
                if (left > 0) P.Gathered[c] = (float)(P.Gathered[c] + kg * left / A);
            }
            _w.Metrics.GatherKg += kg;
            return kg * FuPerKgMast;
        }

        /// <summary>Where on its ground a worker stands (local metres in the village frame): a shore for a fishing ground.</summary>
        public void GroundPoint(int sid, int ground, bool fishing, ref RandomSequence rs, out double x, out double y)
        {
            var P = _w.Planet; var g = P.Grid;
            ref var s = ref _w.Settlements.Get(sid);
            Vec3d at = g.Center[ground];
            if (fishing)
            {
                // the water's edge: halfway to the land neighbour nearest the village
                int best = -1; double bd = double.MaxValue;
                for (int j = 0; j < 8; j++)
                {
                    int nb = g.Nbr8[ground * 8 + j];
                    if (!P.IsLand(nb)) continue;
                    double d = Vec3d.ArcDistance(g.Center[nb], s.Anchor, P.Radius);
                    if (d < bd) { bd = d; best = nb; }
                }
                if (best >= 0) at = (g.Center[best] + g.Center[ground]).Normalized;
            }
            s.SphereToLocal(at, P.Radius, out x, out y);
            double j0 = fishing ? 25 : 70;
            x += rs.Range(-j0, j0); y += rs.Range(-j0, j0);
        }

        /// <summary>Keyed Poisson count (Knuth below 30, a rounded normal above): whole animals from an expected number.</summary>
        static int Poisson(double lambda, ref RandomSequence rs)
        {
            if (lambda <= 0) return 0;
            if (lambda < 30)
            {
                double L = DMath.Exp(-lambda), p = 1.0;
                int k = 0;
                do { k++; p *= rs.NextUnit(); } while (p > L && k < 200);
                return k - 1;
            }
            double z = rs.NextNormal();
            return Math.Max(0, (int)DMath.Floor(lambda + z * DMath.Sqrt(lambda) + 0.5));
        }

        /// <summary>
        /// A catch reaches home (D2-042): the household's larder takes up to four days of its need, the village store the rest
        /// (big game is shared). Into the store when the household is gone.
        /// </summary>
        public void Deposit(int pid, double fu)
        {
            if (fu <= 0) return;
            var w = _w;
            ref var p = ref w.People.Get(pid);
            int home = p.Home;
            if (w.Households.Exists(p.Household))
            {
                ref var hh = ref w.Households.Get(p.Household);
                double room = DMath.Max(0.0, 4.0 * w.SettlementSys.HouseholdNeed(p.Household) - hh.Larder);
                double keep = DMath.Min(fu, room);
                hh.Larder += keep; fu -= keep;
            }
            if (fu > 0 && w.Settlements.Exists(home)) w.Settlements.Get(home).Granary += fu;
        }
    }

    /// <summary>A village's three best grounds for one livelihood, from its daily survey (D2-042). State: saved.</summary>
    public struct Grounds
    {
        public int A, B, C;          // cells, best first (-1 none)
        public float Va, Vb, Vc;     // what each holds (reachable kg of game, fish or mast)
        public float Sa, Sb, Sc;     // their scores (what they hold, nearer better)
        public float Expect;         // FU an hour an ordinary worker can expect at the best

        public static Grounds Empty => new Grounds { A = -1, B = -1, C = -1 };

        public void Offer(int c, double v, double score)
        {
            if (A < 0 || score > Sa) { C = B; Vc = Vb; Sc = Sb; B = A; Vb = Va; Sb = Sa; A = c; Va = (float)v; Sa = (float)score; }
            else if (B < 0 || score > Sb) { C = B; Vc = Vb; Sc = Sb; B = c; Vb = (float)v; Sb = (float)score; }
            else if (C < 0 || score > Sc) { C = c; Vc = (float)v; Sc = (float)score; }
        }

        public void Write(BinWriter w) { w.Int(A); w.Int(B); w.Int(C); w.Float(Va); w.Float(Vb); w.Float(Vc); w.Float(Sa); w.Float(Sb); w.Float(Sc); w.Float(Expect); }
        public static Grounds Read(BinReader r) => new Grounds { A = r.Int(), B = r.Int(), C = r.Int(), Va = r.Float(), Vb = r.Float(), Vc = r.Float(), Sa = r.Float(), Sb = r.Float(), Sc = r.Float(), Expect = r.Float() };
        public void Hash(ref StateHasher h) { h.Add(A); h.Add(B); h.Add(C); h.Add(Va); h.Add(Vb); h.Add(Vc); h.Add(Sa); h.Add(Sb); h.Add(Sc); h.Add(Expect); }
    }
}
