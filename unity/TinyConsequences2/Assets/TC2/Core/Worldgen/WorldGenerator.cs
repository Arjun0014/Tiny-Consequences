using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Worldgen
{
    /// <summary>
    /// Generates a world (D2-014): plates and relief, a sea level for the wanted land fraction, a moisture
    /// index, vegetation at equilibrium, then founding settlements of persistent people, and the first
    /// events. Foundation version (F0); tectonics in time, erosion and climate dynamics come in F1.
    /// </summary>
    public static class WorldGenerator
    {
        public static World Create(WorldConfig cfg)
        {
            var w = new World(cfg);
            var P = new PlanetState(cfg.SurfaceN, cfg.Radius);
            w.InitPlanet(P);
            Relief(w, P);
            Volcanism.SpinUp(w);
            Quakes.Init(w);
            w.Drainage.Rebuild(P);
            Moisture(w, P);
            w.PlanetSys.Climate.Prime(P, w.Seed, 0);
            Vegetation(w, P);
            // the climate at equilibrium with this planet's land, sea and life; ice sheets grown; the coast kept
            P.SeaLevel0 = P.SeaLevel;
            w.PlanetSys.Climate.SpinUp(P, w.Seed, 0);
            P.SeaLevel = P.SeaLevel0; P.SeaLevelNow = P.SeaLevel0;
            w.PlanetSys.RefreshMeans(P);
            for (int c = 0; c < P.Cells; c++) { P.Temp[c] = (float)P.TemperatureAt(c, 0); P.ClassifyBiome(c, 0); }
            SpinUpRivers(w, P);
            Ecology.Create(w);   // the wild animals, spread over the land before people come (D2-035)
            w.Record(new HistoryEvent { Kind = HistoryKind.WorldCreated, Time = 0, Cell = -1, Settlement = 0, RootKind = RootKind.Nature, Subject = new EntityRef(EntityKind.World, 1) });
            w.History.Register(new EntityRef(EntityKind.World, 1), "the world", 0);
            var sites = ChooseSites(w, P);
            // the story begins on a morning at the first village (world time 0 is midnight at longitude 0)
            if (sites.Count > 0) w.Now = SimTime.FloorMod(9 * SimTime.Hour + 30 * SimTime.Minute - PlanetState.LocalOffset(P.Grid.Center[sites[0]]), SimTime.Day);
            w.ScheduleSystems();
            if (cfg.Settlements <= 0) sites.Clear();   // a world without people (tools): the clock still starts at the first site's morning
            for (int i = 0; i < sites.Count; i++) Found(w, sites[i], i);
            return w;
        }

        // ------------------------------------------------------------------ relief
        static void Relief(World w, PlanetState P)
        {
            var cfg = w.Config;
            ulong seed = cfg.Seed;
            var g = P.Grid;
            int n = P.Cells;
            int k = Math.Max(3, cfg.Plates);
            var seeds = new Vec3d[k];
            var poles = new Vec3d[k];
            var omega = new double[k];
            var continental = new bool[k];
            var rs = new RandomSequence(KeyedRng.Hash(seed, RngStream.WorldgenPlates, 1));
            for (int i = 0; i < k; i++)
            {
                seeds[i] = new Vec3d(rs.NextNormal(), rs.NextNormal(), rs.NextNormal()).Normalized;
                poles[i] = new Vec3d(rs.NextNormal(), rs.NextNormal(), rs.NextNormal()).Normalized;
                omega[i] = (0.5 + rs.NextUnit()) * (rs.Chance(0.5) ? 1 : -1);
                continental[i] = rs.Chance(0.45);
            }
            continental[0] = true; // at least one continent
            // plates: nearest seed to a noise-warped position
            for (int c = 0; c < n; c++)
            {
                Vec3d p = g.Center[c];
                Vec3d q = new Vec3d(
                    p.X + 0.22 * Noise.Fbm(seed, 11, p, 2.0, 3),
                    p.Y + 0.22 * Noise.Fbm(seed, 12, p, 2.0, 3),
                    p.Z + 0.22 * Noise.Fbm(seed, 13, p, 2.0, 3)).Normalized;
                int best = 0; double bd = -2;
                for (int i = 0; i < k; i++) { double d = Vec3d.Dot(q, seeds[i]); if (d > bd) { bd = d; best = i; } }
                P.Plate[c] = (byte)best;
            }
            // boundary uplift by convergence of plate velocities
            var uplift = new double[n];
            for (int c = 0; c < n; c++)
            {
                int pc = P.Plate[c];
                Vec3d p = g.Center[c];
                Vec3d vc = Vec3d.Cross(poles[pc], p) * omega[pc];
                for (int j = 0; j < 4; j++)
                {
                    int nb = g.Nbr4[c * 4 + j];
                    int pn = P.Plate[nb];
                    if (pn == pc) continue;
                    Vec3d vn = Vec3d.Cross(poles[pn], g.Center[nb]) * omega[pn];
                    Vec3d dir = (g.Center[nb] - p).Normalized;
                    double conv = Vec3d.Dot(vc - vn, dir); // > 0: plates approach
                    double u;
                    if (conv > 0)
                    {
                        if (continental[pc] && continental[pn]) u = 1700 * conv;
                        else if (continental[pc]) u = 1100 * conv;      // arc on the overriding continent
                        else if (continental[pn]) u = -500 * conv;      // trench on the subducting ocean
                        else u = 500 * conv;                            // island arc
                    }
                    else u = continental[pc] ? 120 * conv : -250 * conv; // rift / ridge
                    uplift[c] += u;
                }
            }
            // spread the uplift a few cells (mountain belts have width)
            var tmp = new double[n];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int c = 0; c < n; c++)
                {
                    double s = uplift[c] * 2;
                    for (int j = 0; j < 4; j++) s += uplift[g.Nbr4[c * 4 + j]];
                    tmp[c] = s / 6.0;
                }
                Array.Copy(tmp, uplift, n);
            }
            var elev = new double[n];
            for (int c = 0; c < n; c++)
            {
                Vec3d p = g.Center[c];
                double baseH = continental[P.Plate[c]] ? 350 : -1100;
                double detail = Noise.Fbm(seed, 21, p, 3.0, 5) * 700 + Noise.Fbm(seed, 22, p, 9.0, 3) * 160;
                double ridge = 1.0 - DMath.Abs(Noise.Fbm(seed, 23, p, 5.0, 3));
                elev[c] = baseH + detail + uplift[c] * (0.6 + 0.6 * ridge);
            }
            // smooth once (double buffered)
            for (int c = 0; c < n; c++)
            {
                double s = elev[c] * 4;
                for (int j = 0; j < 4; j++) s += elev[g.Nbr4[c * 4 + j]];
                tmp[c] = s / 8.0;
            }
            // sea level for the wanted land fraction (sorting exact values is deterministic)
            var sorted = (double[])tmp.Clone();
            Array.Sort(sorted);
            int idx = DMath.ToInt(DMath.Floor((1.0 - cfg.LandFraction) * (n - 1)));
            double sea = sorted[Math.Max(0, Math.Min(n - 1, idx))];
            for (int c = 0; c < n; c++)
            {
                double h = tmp[c] - sea;
                // shape: gentle plains near the coast, tall peaks inland, deep basins offshore
                if (h > 0) h = h * (0.55 + 0.45 * DMath.SmoothStep(0, 1500, h));
                else h = h * 1.1;
                P.Elev[c] = (float)DMath.Clamp(h, -2200, 2900);
            }
            P.SeaLevel = 0;
            P.Plates = Tectonics.MakePlates(poles, omega, continental, P.Radius);
            Tectonics.InitCrust(P, seed);
            P.Hotspots = Volcanism.MakeHotspots(seed);
        }

        /// <summary>
        /// Rivers and lakes at their long-run state: each cell's mean runoff from its moisture routed through the
        /// depressions for a few hundred days (lakes fill where inflow beats evaporation), then the mean flow.
        /// </summary>
        static void SpinUpRivers(World w, PlanetState P)
        {
            var runoff = new float[P.Cells];
            for (int c = 0; c < P.Cells; c++)
                if (P.IsLand(c)) runoff[c] = (float)(DMath.Clamp01(P.Moist[c] - 0.15) * 0.0035); // m a day, wet lands shed more
            var d = w.Drainage;
            d.SettleStored(P);
            for (int day = 0; day < 400; day++) d.Route(P, runoff);
            for (int c = 0; c < P.Cells; c++) P.RiverFlow[c] = d.Discharge[c];
        }

        // ------------------------------------------------------------------ moisture
        static void Moisture(World w, PlanetState P)
        {
            var g = P.Grid;
            int n = P.Cells;
            // distance to the sea in cells (multi-source BFS)
            var dist = new int[n];
            var q = new int[n];
            int head = 0, tail = 0;
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c)) { dist[c] = 0; q[tail++] = c; }
                else dist[c] = int.MaxValue;
            }
            while (head < tail)
            {
                int c = q[head++];
                for (int j = 0; j < 4; j++)
                {
                    int nb = g.Nbr4[c * 4 + j];
                    if (dist[nb] > dist[c] + 1) { dist[nb] = dist[c] + 1; q[tail++] = nb; }
                }
            }
            ulong seed = w.Seed;
            for (int c = 0; c < n; c++)
            {
                double lat = DMath.Abs(P.Latitude(c)) * DMath.Rad2Deg;
                // circulation bands: wet equator, dry subtropics, wet mid-latitudes, dry poles
                double band = 0.55 + 0.45 * DMath.Cos(lat / 90.0 * DMath.PI * 3.0) * (lat < 70 ? 1.0 : 0.6);
                band = DMath.Clamp(band, 0.1, 1.0);
                double coast = P.IsLand(c) ? DMath.Exp(-dist[c] / 9.0) : 1.0;
                double noise = 0.12 * Noise.Fbm(seed, 31, g.Center[c], 4.0, 3);
                double highDry = P.IsLand(c) ? DMath.SmoothStep(1200, 2600, P.Elev[c]) * 0.25 : 0;
                double m = band * (0.35 + 0.65 * coast) + noise - highDry;
                P.Moist[c] = (float)DMath.Clamp(m, 0.03, 1.0);
                P.SoilWater[c] = (float)(P.IsLand(c) ? DMath.Clamp(m * 0.7, 0.05, 0.9) : 1.0);
            }
        }

        static void Vegetation(World w, PlanetState P)
        {
            ulong seed = w.Seed;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) { P.Grass[c] = 0; P.Forest[c] = 0; continue; }
                double tm = P.TMean[c];
                double m = P.Moist[c];
                double tSuitF = DMath.SmoothStep(-1, 8, tm) * (1.0 - DMath.SmoothStep(30, 38, tm));
                double kF = DMath.Clamp01(1.25 * m - 0.12) * tSuitF;
                double kG = DMath.Clamp01(0.25 + 0.9 * m) * DMath.SmoothStep(-6, 4, tm);
                double patch = 0.65 + 0.35 * (0.5 + 0.5 * Noise.Fbm(seed, 41, P.Grid.Center[c], 7.0, 3));
                P.Forest[c] = (float)DMath.Clamp01(kF * patch);
                P.Grass[c] = (float)DMath.Clamp01(kG * (1.0 - 0.5 * P.Forest[c]));
            }
        }

        // ------------------------------------------------------------------ people
        static List<int> ChooseSites(World w, PlanetState P)
        {
            var cfg = w.Config;
            ulong seed = cfg.Seed;
            var g = P.Grid;
            int n = P.Cells;
            // habitability score per land cell
            var score = new double[n];
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c)) { score[c] = -1; continue; }
                if (P.Ice[c] > 0.5f) { score[c] = -1; continue; }
                double tm = P.TMean[c];
                double m = P.Moist[c];
                double s = DMath.SmoothStep(4, 11, tm) * (1.0 - DMath.SmoothStep(24, 30, tm));
                s *= DMath.SmoothStep(0.22, 0.4, m) * (1.0 - DMath.SmoothStep(0.85, 1.0, m));
                s *= 1.0 - DMath.SmoothStep(500, 1100, P.Elev[c]);
                bool coastal = false;
                for (int j = 0; j < 4; j++) if (!P.IsLand(g.Nbr4[c * 4 + j])) coastal = true;
                if (coastal) s *= 1.25;
                if (P.FreshWaterNear(c)) s *= 1.35;
                s *= 0.85 + 0.3 * KeyedRng.Unit(seed, RngStream.WorldgenSettle, (ulong)c);
                score[c] = s;
            }
            // choose a home region (best cell), then settlements around it within walking reach
            int first = -1; double fb = 0;
            for (int c = 0; c < n; c++) if (score[c] > fb) { fb = score[c]; first = c; }
            if (first < 0) return new List<int>();
            var chosen = new List<int> { first };
            double minSep = 5.0 * P.CellSize, maxSep = 16.0 * P.CellSize;
            int want = Math.Max(1, cfg.Settlements);
            int guard = 0;
            while (chosen.Count < want && guard++ < 50)
            {
                int bestC = -1; double bestS = 0;
                for (int c = 0; c < n; c++)
                {
                    if (score[c] <= 0.05) continue;
                    double nearest = double.MaxValue;
                    for (int i = 0; i < chosen.Count; i++) nearest = Math.Min(nearest, Vec3d.ArcDistance(g.Center[c], g.Center[chosen[i]], P.Radius));
                    if (nearest < minSep || nearest > maxSep) continue;
                    if (score[c] > bestS) { bestS = score[c]; bestC = c; }
                }
                if (bestC < 0) { maxSep *= 1.5; continue; }
                chosen.Add(bestC);
            }
            return chosen;
        }

        static void Found(World w, int cell, int index)
        {
            var cfg = w.Config;
            var P = w.Planet;
            ulong seed = cfg.Seed;
            int sid = w.Ids.Next(EntityKind.Settlement);
            Vec3d anchor = P.Grid.Center[cell];
            Settlement.Frame(anchor, out Vec3d east, out Vec3d north);
            var rs = new RandomSequence(KeyedRng.Hash(seed, RngStream.WorldgenSettle, (ulong)sid, 7));
            // the field cell: the most fertile land neighbour
            int fieldCell = cell; double fbest = -1;
            for (int j = 0; j < 8; j++)
            {
                int nb = P.Grid.Nbr8[cell * 8 + j];
                if (!P.IsLand(nb)) continue;
                double f = P.Moist[nb] + 0.3 * P.Grass[nb] - 0.2 * P.Forest[nb];
                if (f > fbest) { fbest = f; fieldCell = nb; }
            }
            // the first villages are already standing when history begins: houses, a well, a granary (D2-045)
            var s = new Settlement { Id = sid, Cell = cell, Anchor = anchor, East = east, North = north, Founded = w.Now, FieldCell = fieldCell, Ration = 1.0,
                                     Hunt = Grounds.Empty, Fish = Grounds.Empty, Gath = Grounds.Empty, Works = Construction.Well | Construction.Granary,
                                     Lore = KnownSites.Empty };
            s.SphereToLocal(P.Grid.Center[fieldCell], P.Radius, out double fx, out double fy);
            double fl = DMath.Sqrt(fx * fx + fy * fy);
            if (fl < 1) { fx = 150; fy = 0; fl = 150; }
            s.FieldX = (float)(fx / fl * 170); s.FieldY = (float)(fy / fl * 170);
            double wa = rs.Range(0, DMath.TwoPi);
            s.WoodX = (float)(DMath.Cos(wa) * 210); s.WoodY = (float)(DMath.Sin(wa) * 210);
            s.WellX = (float)rs.Range(-12, 12); s.WellY = (float)rs.Range(-12, 12);
            s.SquareX = 0; s.SquareY = 0;
            w.Settlements.Add(sid, s);
            w.IndexSettlementCells(sid);
            string name = Names.Place(seed, sid);
            w.History.Register(EntityRef.Settlement(sid), name, w.Now);
            long arrived = w.Record(new HistoryEvent { Kind = HistoryKind.PeopleArrived, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid), RootKind = RootKind.People });
            ref var sr = ref w.Settlements.Get(sid);
            sr.FoundedEvent = w.Record(new HistoryEvent { Kind = HistoryKind.SettlementFounded, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Cause1 = arrived, Rel1 = CauseRel.CausedBy });
            // founding households: couples with children, a few elders, a few single adults
            int target = cfg.FoundersPerSettlement;
            int made = 0;
            while (made < target)
            {
                int hid = w.Ids.Next(EntityKind.Household);
                ref var srr = ref w.Settlements.Get(sid);
                srr.LotPosition(srr.Lots++, out float hx, out float hy);
                var hh = new Household { Id = hid, Settlement = sid, Formed = w.Now, FamilyName = hid, HomeX = hx, HomeY = hy, Larder = 6, Shelter = Construction.House, Water = 30f, Indoor = (float)Hearth.Comfort };
                w.Households.Add(hid, hh);
                w.HouseholdsOf(sid).InsertSorted(hid);
                w.History.Register(EntityRef.Household(hid), Names.Family(seed, hid), w.Now);
                double motherAge = rs.Range(19, 40);
                double fatherAge = motherAge + rs.Range(-2, 7);
                int mother = MakeFounder(w, Sex.Female, motherAge, 0, 0, hid, sid, ref rs);
                int father = MakeFounder(w, Sex.Male, fatherAge, 0, 0, hid, sid, ref rs);
                w.People.Get(mother).Partner = father; w.People.Get(father).Partner = mother;
                made += 2;
                int kids = Math.Min(target - made, rs.NextBelow(Math.Max(1, (int)((motherAge - 17) / 3.5)) + 1));
                double childAge = rs.Range(0.2, 2.5);
                for (int c = 0; c < kids && childAge < motherAge - 16; c++)
                {
                    MakeFounder(w, rs.Chance(0.5) ? Sex.Female : Sex.Male, childAge, mother, father, hid, sid, ref rs);
                    // a baby still at the breast: its mother is nursing it (D2-042: milk carries it through hard days)
                    if (childAge < 1.5) w.People.Get(mother).NursingUntil = w.Now + SimTime.Years(1.5 - childAge);
                    made++;
                    childAge += rs.Range(1.8, 3.6);
                }
                if (made < target && rs.Chance(0.25)) { MakeFounder(w, rs.Chance(0.55) ? Sex.Female : Sex.Male, rs.Range(56, 70), 0, 0, hid, sid, ref rs); made++; }
                w.Households.Get(hid).Gear = (float)(1.1 * w.Members(hid).Count);   // clothed and equipped (D2-058)
                Homes.Raised(w, hid);   // its house stands on its lot (D2-059)
                StartingHerd(w, hid, sid);
            }
            // stores to see the founders through to their first harvest
            ref var st = ref w.Settlements.Get(sid);
            st.Granary = st.Population * cfg.FoodPerAdultDay * 70;
            st.Woodstore = st.Population * 4;
            w.SettlementSys.ClaimInitialFields(sid, st.Population * cfg.FieldHaPerPerson);
            ref var st2 = ref w.Settlements.Get(sid);
            st2.FieldHa = Math.Max(0.5, Math.Min(st2.Population * cfg.FieldHaPerPerson, st2.ArableHa));
            // the crop already in the ground for the season the world starts in
            int doy = SimTime.DayOfYear(w.Now + PlanetState.LocalOffset(anchor));
            if (anchor.Y < 0) doy = (doy + 24) % SimTime.DaysPerYear;
            st.Crop = doy < 12 ? CropStage.Sown : (doy < 30 ? CropStage.Growing : (doy < 39 ? CropStage.Ripe : CropStage.Harvested));
            st.Sown = 0.9; st.CropGrowth = doy < 12 ? 0 : 0.5;
            st.CropWorkNeed = st.Crop == CropStage.Sown ? st.FieldHa * 40 : (st.Crop == CropStage.Growing ? st.FieldHa * 8 : (st.Crop == CropStage.Ripe ? st.FieldHa * 45 : 0));
            st.ExpectedHarvest = st.FieldHa * cfg.FieldYieldPerHa * 0.8;
            w.Wild.Survey(sid);   // the founders know the grounds round their new home (D2-042)
            w.SettlementSys.ScheduleFirstDaily(sid);
        }

        /// <summary>
        /// A founding household's animals (D2-048; keyed by the household, so the people's draws stay where they were): a few goats,
        /// a cow or two in some, pigs in some - grown animals of both sexes, the village's herds breeding among themselves.
        /// </summary>
        static void StartingHerd(World w, int hid, int sid)
        {
            var hrs = new RandomSequence(KeyedRng.Hash(w.Seed, RngStream.WorldgenSettle, (ulong)hid, 48));
            int goats = 2 + hrs.NextBelow(3);
            for (int i = 0; i < goats; i++) w.Stock.Add(Livestock.Goat, i == 0 || hrs.Chance(0.3) ? Sex.Male : Sex.Female, w.Now - SimTime.Years(1.2 + 4 * hrs.NextUnit()), 0, hid);
            if (hrs.Chance(0.45)) { w.Stock.Add(Livestock.Cattle, Sex.Female, w.Now - SimTime.Years(3 + 5 * hrs.NextUnit()), 0, hid); if (hrs.Chance(0.5)) w.Stock.Add(Livestock.Cattle, Sex.Male, w.Now - SimTime.Years(3 + 4 * hrs.NextUnit()), 0, hid); }
            if (hrs.Chance(0.4)) { w.Stock.Add(Livestock.Pig, Sex.Female, w.Now - SimTime.Years(1 + 3 * hrs.NextUnit()), 0, hid); w.Stock.Add(Livestock.Pig, Sex.Male, w.Now - SimTime.Years(1 + 3 * hrs.NextUnit()), 0, hid); }
        }

        static int MakeFounder(World w, byte sex, double age, int mother, int father, int hid, int sid, ref RandomSequence rs)
        {
            long born = w.Now - SimTime.Years(age);
            long first = w.Now + (long)rs.Range(0, 3600);
            int id = w.Agents.CreatePerson(sex, born, mother, father, hid, sid, first);
            ref var p = ref w.People.Get(id);
            p.LastMeal = w.Now - (long)rs.Range(0, 4 * 3600);
            if (age > 20) { p.SkFarm = (float)(0.35 + 0.45 * rs.NextUnit()); p.SkWood = (float)(0.3 + 0.45 * rs.NextUnit()); }
            // grown founders have hunted and fished before (keyed by id, so the founders' other draws stay put; D2-042)
            if (age > 18)
            {
                p.SkHunt = (float)DMath.Min(1.0, p.SkHunt + 0.35 * KeyedRng.Unit(w.Seed, RngStream.Traits, (ulong)id, 104));
                p.SkFish = (float)DMath.Min(1.0, p.SkFish + 0.35 * KeyedRng.Unit(w.Seed, RngStream.Traits, (ulong)id, 105));
            }
            return id;
        }
    }
}
