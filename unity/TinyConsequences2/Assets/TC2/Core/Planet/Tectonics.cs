using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>One rigid plate: an Euler pole and an angular speed (radians a year), continental or oceanic at birth.</summary>
    public struct PlateInfo
    {
        public Vec3d Pole;
        public double Omega;
        public bool Continental;
    }

    /// <summary>
    /// Plates in time (05 §3b, D2-025). Every cell holds one crust sample - its plate, kind, age, elevation and exact
    /// position inside the cell (an offset in the cell's east/north frame) - so relief rides its plate without the
    /// numerical blur of resampling. Each step rotates the samples about their plates' poles and gives every cell the
    /// sample that landed in it: none at a divergent boundary is new oceanic crust (a ridge); none inside a plate is the
    /// plate itself (aliasing, filled from its neighbours); several from different plates is convergence (continental
    /// crust overrides oceanic, younger oceanic overrides older, continents collide and thicken). Boundaries raise and
    /// lower the crust by their convergence; oceanic crust sinks as it ages; slopes wear down (hillslope diffusion, the
    /// placeholder for stream-power erosion). The view of the planet and everything on it read the cells.
    /// </summary>
    public sealed class Tectonics
    {
        public const double StepYears = 10;
        public const int ResampleEvery = 16;         // cells change hands once the plates have moved about a cell (160 y)
        public const double RidgeDepth = -650;       // m: new ocean floor
        public const double CollisionGain = 2.2;     // uplift (m/yr) per m/yr of continental convergence (before spreading)
        public const double ArcGain = 1.5;           // uplift on an overriding plate per m/yr of subduction
        public const double TrenchGain = -0.35;      // the subducting side sinks (offshore only)
        public const double RiftGain = -0.4;         // continents pulled apart sag
        public const double Thicken = 0.5;           // a collided column adds this share of its bulk (elev + 600 m) to the winner
        public const double AccretionChance = 0.35; // a subducted sample welds a strip of crust onto the overriding continent
        public const double CollisionDrag = 6.0;     // colliding plates trade motion in proportion to the front's length
        public const double DriftYears = 3000;       // plate motions wander on this timescale (the mantle's forces)
        public const double PeakSpread = 2600;       // m: above this a range spreads its mass to its flanks
        public const double Buoyant = -40;           // m from the sea: thin continental crust floats back to a shelf
        public const double BuoyancyYears = 3000;    // isostatic rebound and basins filling (crust above -300 m only)
        public const double Wear = 6.0;              // m2/yr: hillslope creep (a range 3 cells wide fades in ~10^4 y)

        readonly int _cells;
        readonly Vec3d[] _east, _north;
        // diagnostics (cumulative counts, not state)
        public long Collided, Subducted, Ridges, Fills, Accreted, ContLostToFill, ContGainedByFill, DropC, DropO, RidgeOnCont;
        public bool Drift = true;   // plate motions change (collision drag, the mantle's wander); tests may hold them fixed
        public double UpliftCont, UpliftOcean;
        // scratch
        readonly int[] _target, _count, _start, _order, _winner;
        readonly float[] _newElev, _newAge, _newOx, _newOy, _uplift, _tmp;
        readonly byte[] _newPlate, _newKind;
        readonly Vec3d[] _pos;
        readonly int[] _accrete; int _accreteN;
        readonly long[] _front = new long[16 * 16];   // collisions between plate pairs this step
        readonly int[] _plateCells = new int[16];

        public Tectonics(PlanetState P)
        {
            _cells = P.Cells;
            _east = new Vec3d[_cells]; _north = new Vec3d[_cells];
            for (int c = 0; c < _cells; c++) Frame(P.Grid.Center[c], out _east[c], out _north[c]);
            _target = new int[_cells]; _count = new int[_cells]; _start = new int[_cells + 1]; _order = new int[_cells]; _winner = new int[_cells];
            _newElev = new float[_cells]; _newAge = new float[_cells]; _newOx = new float[_cells]; _newOy = new float[_cells];
            _uplift = new float[_cells]; _tmp = new float[_cells];
            _newPlate = new byte[_cells]; _newKind = new byte[_cells];
            _pos = new Vec3d[_cells];
            _accrete = new int[_cells];
        }

        static void Frame(Vec3d up, out Vec3d east, out Vec3d north)
        {
            Vec3d e = Vec3d.Cross(Vec3d.UnitY, up);
            if (e.LengthSq < 1e-10) e = Vec3d.Cross(Vec3d.UnitX, up);
            east = e.Normalized;
            north = Vec3d.Cross(up, east).Normalized;
        }

        /// <summary>Velocity of the ground at a point on a plate (m/yr, tangent).</summary>
        public static Vec3d Velocity(PlanetState P, int plate, Vec3d at)
        {
            var pl = P.Plates[plate];
            return Vec3d.Cross(pl.Pole, at) * (pl.Omega * P.Radius);
        }

        /// <summary>Rotates a unit vector about a unit axis by an angle (Rodrigues).</summary>
        public static Vec3d Rotate(Vec3d v, Vec3d k, double cos, double sin)
        {
            Vec3d kxv = Vec3d.Cross(k, v);
            double kv = Vec3d.Dot(k, v);
            return (v * cos + kxv * sin + k * (kv * (1 - cos))).Normalized;
        }

        /// <summary>Exact position of a cell's crust sample (gnomonic offset in the cell's tangent plane).</summary>
        public Vec3d SamplePos(PlanetState P, int c)
        {
            return (P.Grid.Center[c] * P.Radius + _east[c] * P.CrustOx[c] + _north[c] * P.CrustOy[c]).Normalized;
        }

        /// <summary>The gnomonic offset of a direction in cell c's tangent plane (the exact inverse of SamplePos).</summary>
        void Offset(PlanetState P, int c, Vec3d v, out float ox, out float oy)
        {
            double vc = Vec3d.Dot(v, P.Grid.Center[c]);
            if (vc < 1e-6) vc = 1e-6;
            ox = (float)(P.Radius * Vec3d.Dot(v, _east[c]) / vc);
            oy = (float)(P.Radius * Vec3d.Dot(v, _north[c]) / vc);
        }

        /// <summary>One step of plate motion and boundary processes (dt in years). Returns cells that changed land/sea.</summary>
        public void Step(PlanetState P, double dt, ulong seed, long now)
        {
            var g = P.Grid;
            int n = _cells;
            _accreteN = 0;
            for (int i = 0; i < _front.Length; i++) _front[i] = 0;
            int k = P.Plates.Length;
            Span<double> cs = stackalloc double[k], sn = stackalloc double[k];
            for (int i = 0; i < k; i++) { double a = P.Plates[i].Omega * dt; cs[i] = DMath.Cos(a); sn[i] = DMath.Sin(a); }

            // 1. boundary forcing from the present arrangement (convergence of neighbouring plates)
            for (int c = 0; c < n; c++)
            {
                int pc = P.Plate[c];
                Vec3d p = g.Center[c];
                double u = 0;
                Vec3d vc = Velocity(P, pc, p);
                for (int q = 0; q < 4; q++)
                {
                    int nb = g.Nbr4[c * 4 + q];
                    int pn = P.Plate[nb];
                    if (pn == pc) continue;
                    Vec3d dir = (g.Center[nb] - p).Normalized;
                    double conv = Vec3d.Dot(vc - Velocity(P, pn, g.Center[nb]), dir); // m/yr, > 0 approaching
                    bool contC = P.Crust[c] == 1, contN = P.Crust[nb] == 1;
                    if (conv > 0)
                    {
                        if (contC && contN) u += CollisionGain * conv;
                        else if (contC) u += ArcGain * conv;                    // the overriding continent rises
                        else if (contN) u += TrenchGain * conv;                 // the ocean going under sinks
                        else u += (P.CrustAge[c] < P.CrustAge[nb] ? ArcGain * 0.6 : TrenchGain) * conv; // younger rides older
                    }
                    else if (contC) u += RiftGain * -conv;
                }
                _uplift[c] = (float)(u * 0.25); // per neighbour-sum -> per cell
            }
            // belts have width: spread the forcing over a few cells
            for (int pass = 0; pass < 2; pass++)
            {
                for (int c = 0; c < n; c++)
                {
                    double s = _uplift[c] * 2;
                    bool cont = P.Crust[c] == 1;
                    for (int q = 0; q < 4; q++)
                    {
                        int nb = g.Nbr4[c * 4 + q];
                        double u = _uplift[nb];
                        if (cont && u < 0 && P.Crust[nb] == 0) u = 0; // a trench's sinking stays offshore
                        s += u;
                    }
                    _tmp[c] = (float)(s / 6.0);
                }
                Array.Copy(_tmp, _uplift, n);
            }

            long idx = now / DMath.ToLong(StepYears * SimTime.Year);
            if (idx % ResampleEvery != 0)
            {
                // the crust moves on inside its cells (exactly: the offsets carry it); the cells keep their samples
                for (int c = 0; c < n; c++)
                {
                    int pl = P.Plate[c];
                    Vec3d v = Rotate(SamplePos(P, c), P.Plates[pl].Pole, cs[pl], sn[pl]);
                    Offset(P, c, v, out _newOx[c], out _newOy[c]);
                    _newElev[c] = P.Elev[c]; _newPlate[c] = P.Plate[c]; _newKind[c] = P.Crust[c];
                    _newAge[c] = (float)(P.CrustAge[c] + dt);
                }
                Commit(P, dt);
                Wearing(P, dt);
                Plates(P, dt, seed, now);
                return;
            }

            // 2. move every sample with its plate and find where it lands
            for (int c = 0; c < n; c++)
            {
                int pl = P.Plate[c];
                Vec3d v = Rotate(SamplePos(P, c), P.Plates[pl].Pole, cs[pl], sn[pl]);
                _pos[c] = v;
                _target[c] = g.CellOf(v);
                _count[c] = 0;
            }
            for (int c = 0; c < n; c++) _count[_target[c]]++;
            _start[0] = 0;
            for (int c = 0; c < n; c++) _start[c + 1] = _start[c] + _count[c];
            for (int c = 0; c < n; c++) _count[c] = 0;
            for (int c = 0; c < n; c++) { int t = _target[c]; _order[_start[t] + _count[t]++] = c; } // sources ascending per target

            // 3. every cell takes the sample that wins it
            for (int t = 0; t < n; t++)
            {
                int a = _start[t], m = _start[t + 1] - a;
                if (m == 0) { _winner[t] = -1; continue; }
                int best = _order[a];
                for (int i = 1; i < m; i++) { int s = _order[a + i]; if (Wins(P, s, best, _pos, g.Center[t])) best = s; }
                _winner[t] = best;
                double elev = P.Elev[best];
                // the others: a duplicate of the same plate is aliasing (dropped, its neighbour cell keeps the plate);
                // another plate's crust goes under, or - two continents - piles up and thickens the winner
                Vec3d at = g.Center[t];
                for (int i = 0; i < m; i++)
                {
                    int s = _order[a + i];
                    if (s == best) continue;
                    if (P.Plate[s] == P.Plate[best]) { if (P.Crust[s] == 1) DropC++; else DropO++; continue; }
                    // two plates in one cell: convergence only if they close on each other here, else aliasing
                    if (Closing(P, best, s, at) < 0.05) { if (P.Crust[s] == 1) DropC++; else DropO++; continue; }
                    if (P.Crust[s] == 1 && P.Crust[best] == 1)
                    {
                        elev += Thicken * DMath.Max(0, P.Elev[s] + 600); Collided++;
                        int pa = P.Plate[s], pb = P.Plate[best];
                        if (pa < 16 && pb < 16) { _front[pa * 16 + pb]++; _front[pb * 16 + pa]++; }
                    }
                    else
                    {
                        Subducted++;
                        if (P.Crust[best] == 1 && _accreteN < _accrete.Length && KeyedRng.Unit(seed, RngStream.Tectonics, (ulong)s, (ulong)now) < AccretionChance)
                            _accrete[_accreteN++] = t;
                    }
                }
                _newElev[t] = (float)elev;
                _newPlate[t] = P.Plate[best];
                _newKind[t] = P.Crust[best];
                _newAge[t] = (float)(P.CrustAge[best] + dt);
                Offset(P, t, _pos[best], out _newOx[t], out _newOy[t]);
            }
            // 4. cells nothing landed in: a ridge where plates part, else the plate's own crust (aliasing)
            for (int t = 0; t < n; t++)
            {
                if (_winner[t] >= 0) continue;
                FillGap(P, t, dt);
            }
            Commit(P, dt);
            Accrete(P);
            Wearing(P, dt);
            Plates(P, dt, seed, now);
        }

        /// <summary>The new arrangement becomes the crust; age, boundary forcing, cooling and buoyancy change the relief.</summary>
        void Commit(PlanetState P, double dt)
        {
            int n = _cells;
            for (int c = 0; c < n; c++)
            {
                P.Plate[c] = _newPlate[c]; P.Crust[c] = _newKind[c]; P.CrustAge[c] = _newAge[c];
                P.CrustOx[c] = _newOx[c]; P.CrustOy[c] = _newOy[c];
                double e = _newElev[c] + _uplift[c] * dt;
                if (_newKind[c] == 1) UpliftCont += _uplift[c] * dt; else UpliftOcean += _uplift[c] * dt;
                if (P.Crust[c] == 0)
                {
                    double age = DMath.Max(25.0, P.CrustAge[c]);
                    if (age < 5000) e -= 6.36 / DMath.Sqrt(age) * dt; // the ocean floor sinks as it cools
                }
                else
                {
                    double rel = e - P.SeaLevel;
                    if (rel > -300 && rel < Buoyant) e += (Buoyant - rel) * dt / BuoyancyYears; // continents float
                }
                P.Elev[c] = (float)DMath.Clamp(e, -2600, 4500);
                // arcs and uplifted sea floor that reach the surface are new continental crust (accretion)
                if (P.Crust[c] == 0 && P.Elev[c] > P.SeaLevel - 100) { P.Crust[c] = 1; Accreted++; }
            }
        }

        /// <summary>
        /// Plate motions change: colliding plates drag each other toward one motion (a suture), and every plate's
        /// motion wanders slowly (keyed by the seed) while keeping its speed - so collisions stall and new ones start.
        /// </summary>
        void Plates(PlanetState P, double dt, ulong seed, long now)
        {
            int k = P.Plates.Length;
            if (k > 16 || !Drift) return;
            for (int i = 0; i < k; i++) _plateCells[i] = 0;
            for (int c = 0; c < _cells; c++) if (P.Plate[c] < k) _plateCells[P.Plate[c]]++;
            Span<double> ox = stackalloc double[k], oy = stackalloc double[k], oz = stackalloc double[k];
            for (int i = 0; i < k; i++) { var pl = P.Plates[i]; ox[i] = pl.Pole.X * pl.Omega; oy[i] = pl.Pole.Y * pl.Omega; oz[i] = pl.Pole.Z * pl.Omega; }
            Span<double> nx = stackalloc double[k], ny = stackalloc double[k], nz = stackalloc double[k];
            for (int i = 0; i < k; i++)
            {
                double x = ox[i], y = oy[i], z = oz[i];
                double size = DMath.Max(1, _plateCells[i]);
                for (int j = 0; j < k; j++)
                {
                    long f = _front[i * 16 + j];
                    if (f == 0 || j == i) continue;
                    double share = DMath.Min(0.25, CollisionDrag * f / size);
                    x += share * (ox[j] - ox[i]); y += share * (oy[j] - oy[i]); z += share * (oz[j] - oz[i]);
                }
                // the mantle's slow change of mind: a small keyed turn of the motion, the speed relaxing to its own
                double speed = DMath.Sqrt(x * x + y * y + z * z);
                double ref0 = DMath.Max(1e-9, DMath.Abs(P.Plates[i].Omega));
                double sig = DMath.Sqrt(dt / DriftYears) * 0.35 * ref0;
                double rx = KeyedRng.Unit(seed, RngStream.Tectonics, (ulong)(i + 1), (ulong)now, 1) * 2 - 1;
                double ry = KeyedRng.Unit(seed, RngStream.Tectonics, (ulong)(i + 1), (ulong)now, 2) * 2 - 1;
                double rz = KeyedRng.Unit(seed, RngStream.Tectonics, (ulong)(i + 1), (ulong)now, 3) * 2 - 1;
                x += sig * rx; y += sig * ry; z += sig * rz;
                nx[i] = x; ny[i] = y; nz[i] = z;
            }
            for (int i = 0; i < k; i++)
            {
                double m = DMath.Sqrt(nx[i] * nx[i] + ny[i] * ny[i] + nz[i] * nz[i]);
                if (m < 1e-12) continue;
                var pl = P.Plates[i];
                pl.Pole = new Vec3d(nx[i] / m, ny[i] / m, nz[i] / m);
                pl.Omega = m;
                P.Plates[i] = pl;
            }
        }

        /// <summary>Where ocean dives under a continent, scraped-off crust and arc rock weld onto the margin.</summary>
        void Accrete(PlanetState P)
        {
            var g = P.Grid;
            for (int i = 0; i < _accreteN; i++)
            {
                int t = _accrete[i];
                int best = -1; double be = double.MinValue;
                for (int q = 0; q < 8; q++)
                {
                    int nb = g.Nbr8[t * 8 + q];
                    if (P.Crust[nb] != 0) continue;
                    if (P.Elev[nb] > be || (P.Elev[nb] == be && nb < best)) { be = P.Elev[nb]; best = nb; }
                }
                if (best < 0) continue;
                P.Crust[best] = 1;
                P.Plate[best] = P.Plate[t];
                P.Elev[best] = (float)DMath.Max(P.Elev[best], P.SeaLevel - 250);
                Accreted++;
            }
        }

        /// <summary>Which of two samples landing in one cell keeps it.</summary>
        /// <summary>How fast the plates of two samples close on each other (m/yr; below 0.05 they only alias).</summary>
        static double Closing(PlanetState P, int a, int b, Vec3d at)
        {
            Vec3d dir = P.Grid.Center[a] - P.Grid.Center[b];
            double len = DMath.Sqrt(Vec3d.Dot(dir, dir));
            return len > 0 ? Vec3d.Dot(Velocity(P, P.Plate[b], at) - Velocity(P, P.Plate[a], at), dir) / len : 0;
        }

        static bool Wins(PlanetState P, int a, int b, Vec3d[] pos, Vec3d centre)
        {
            if (P.Plate[a] != P.Plate[b] && Closing(P, a, b, centre) >= 0.05)
            {
                if (P.Crust[a] != P.Crust[b]) return P.Crust[a] == 1;        // continents never sink
                if (P.Crust[a] == 0 && P.CrustAge[a] != P.CrustAge[b]) return P.CrustAge[a] < P.CrustAge[b]; // older ocean goes under
                if (P.Elev[a] != P.Elev[b]) return P.Elev[a] > P.Elev[b];     // the higher continent rides up
                return P.Plate[a] < P.Plate[b];
            }
            // aliasing (the same plate, or plates not closing here): the sample nearest the cell's centre keeps it
            double da = Vec3d.Dot(pos[a], centre), db = Vec3d.Dot(pos[b], centre);
            if (da != db) return da > db;
            return a < b;
        }

        void FillGap(PlanetState P, int t, double dt)
        {
            var g = P.Grid;
            // the plates around the gap (as they now stand)
            int plateA = -1, plateB = -1, nearest = -1;
            double near = -2;
            Vec3d ca = Vec3d.Zero, cb = Vec3d.Zero;
            Vec3d centre = g.Center[t];
            for (int q = 0; q < 8; q++)
            {
                int nb = g.Nbr8[t * 8 + q];
                if (_winner[nb] < 0) continue;
                int pn = _newPlate[nb];
                if (plateA < 0 || pn == plateA)
                {
                    plateA = pn; ca = ca + g.Center[nb];
                    double d = Vec3d.Dot(_pos[_winner[nb]], centre);
                    if (d > near) { near = d; nearest = nb; }
                }
                else if (plateB < 0 || pn == plateB) { plateB = pn; cb = cb + g.Center[nb]; }
            }
            bool parting = false;
            if (plateA >= 0 && plateB >= 0)
            {
                // the plates part where B moves away from A across the gap
                Vec3d at = g.Center[t];
                Vec3d across = (cb.Normalized - ca.Normalized).Normalized;
                Vec3d rel = Velocity(P, plateB, at) - Velocity(P, plateA, at);
                double apart = Vec3d.Dot(rel, across);
                parting = apart > 0.35 * DMath.Sqrt(Vec3d.Dot(rel, rel)) && apart > 0.02; // clearly opening, not sliding past
            }
            if (plateA < 0 || parting)
            {
                // a ridge: new ocean floor, young and shallow
                _newPlate[t] = (byte)(plateA >= 0 ? plateA : P.Plate[t]);
                _newKind[t] = 0;
                _newAge[t] = 0;
                _newElev[t] = (float)RidgeDepth;
                Ridges++;
                if (P.Crust[t] == 1) RidgeOnCont++;
            }
            else
            {
                // aliasing inside a plate: the nearest sample of the plate stands for it (no averaging, no blur)
                _newPlate[t] = (byte)plateA;
                _newKind[t] = _newKind[nearest];
                _newAge[t] = _newAge[nearest];
                _newElev[t] = _newElev[nearest];
                Fills++;
                if (P.Crust[t] == 1 && _newKind[t] == 0) ContLostToFill++;
                if (P.Crust[t] == 0 && _newKind[t] == 1) ContGainedByFill++;
            }
            _newOx[t] = 0; _newOy[t] = 0;
        }

        /// <summary>Slopes wear down (diffusion of relief on land; the sea floor is left to the plates).</summary>
        void Wearing(PlanetState P, double dt)
        {
            var g = P.Grid;
            double h2 = P.CellSize * P.CellSize;
            double k = Wear * dt / h2;
            if (k <= 0) return;
            double sea = P.SeaLevel;
            for (int c = 0; c < _cells; c++)
            {
                double e = P.Elev[c];
                if (e <= sea) { _tmp[c] = (float)e; continue; }
                // soil creeps between land cells only (no flux across the coast: what reaches the sea is the
                // rivers' work); the flux is symmetric, so the land keeps its mass
                double lap = 0;
                for (int q = 0; q < 4; q++)
                {
                    double en = P.Elev[g.Nbr4[c * 4 + q]];
                    if (en > sea) lap += en - e;
                }
                _tmp[c] = (float)(e + k * lap);
            }
            Array.Copy(_tmp, P.Elev, _cells);
            // over-thick ranges spread to their lower flanks (mass kept: what one cell loses its neighbours gain)
            for (int c = 0; c < _cells; c++) _tmp[c] = 0;
            for (int c = 0; c < _cells; c++)
            {
                double e = P.Elev[c];
                if (e <= PeakSpread) continue;
                double give = 0.1 * (e - PeakSpread);
                int lower = 0;
                for (int q = 0; q < 4; q++) if (P.Elev[g.Nbr4[c * 4 + q]] < e) lower++;
                if (lower == 0) continue;
                _tmp[c] -= (float)give;
                for (int q = 0; q < 4; q++) { int nb = g.Nbr4[c * 4 + q]; if (P.Elev[nb] < e) _tmp[nb] += (float)(give / lower); }
            }
            for (int c = 0; c < _cells; c++) if (_tmp[c] != 0f) P.Elev[c] += _tmp[c];
        }

        /// <summary>Worldgen: plate kinematics in m/yr and the first crust from the generated relief.</summary>
        public static PlateInfo[] MakePlates(Vec3d[] poles, double[] omega, bool[] continental, double radius)
        {
            var plates = new PlateInfo[poles.Length];
            for (int i = 0; i < poles.Length; i++)
                plates[i] = new PlateInfo { Pole = poles[i], Omega = omega[i] * 1.0 / radius, Continental = continental[i] }; // ~0.5-1.5 m/yr
            return plates;
        }

        public static void InitCrust(PlanetState P, ulong seed)
        {
            for (int c = 0; c < P.Cells; c++)
            {
                bool cont = P.Elev[c] > P.SeaLevel - 250 || (P.Plates[P.Plate[c]].Continental && P.Elev[c] > P.SeaLevel - 700);
                P.Crust[c] = (byte)(cont ? 1 : 0);
                P.CrustAge[c] = (float)(cont ? 0 : 1500 + 3000 * KeyedRng.Unit(seed, RngStream.Tectonics, (ulong)c));
                P.CrustOx[c] = 0; P.CrustOy[c] = 0;
            }
        }
    }
}
