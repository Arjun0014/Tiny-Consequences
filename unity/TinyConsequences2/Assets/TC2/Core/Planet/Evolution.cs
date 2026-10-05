using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Adaptation and new species (06 §2, D2-037, D2-038). Every species carries, in every cell, how far its coat has moved
    /// from its kind's (`P.Coat`, C of cold tolerance: below zero bears colder) and a neutral lineage marker (`P.Drift`).
    /// Each step the local population is pulled toward the coat that place asks for (a 7 C margin under its annual mean) -
    /// slowly, faster for fast breeders; a coat far from that costs lives; wanderers carry coats and lineage and mix them
    /// where they arrive. At each tectonic step (ten years) every population (a group of occupied cells with no occupied path to the
    /// rest of its kind) has its lineage wander a little - one range wanders together, ranges cut off from each other wander
    /// apart - and a sizeable population whose lineage has wandered far enough from the main range's becomes a species of
    /// its own (sooner where their climates made their coats different); the history tells of it. A species with no
    /// animals left is extinct. New species come only from isolation, over time.
    /// </summary>
    public static class Evolution
    {
        public const double Margin = 7.0;          // C: the coat a place asks for leaves this margin under its mean
        public const double Pull = 0.0015;         // per year per (young per adult): how fast a population follows (e-fold ~250 y)
        public const int MaxSpecies = 20;          // (Ecology.MaxK sizes the arrays)
        public const double Load = 0.002;          // per year per C beyond 3 C of mismatch: the lives adaptation costs
        public const double DriftRate = 0.015;     // a lineage marker wanders this much per square root of a year (at a 10-year life):
                                                   // two populations cut off reach the line in ~2,000-4,000 years (D2-046; at 0.003 it
                                                   // took 25,000+ and no 10,000-year run split; at 0.02 six kinds split by year 830)
        public const double CoatWeight = 0.05;     // per C of coat difference: how much sooner two lineages part (10 C: 1.5 x)
        public const int MinCells = 40;            // a population that can become a kind: not a fragment at a range's edge
        public const double MinAnimals = 80 * Ecology.Density;
        public const int MaxPerKind = 4;           // living kinds descended from one first kind (deer and their offshoots)
        public const double MinViable = 5.0;       // a kind down to a handful of animals in all the world dies out by chance within
                                                   // decades (too few to find mates); the fields are continuous: without this a remnant
                                                   // of three animals held its place for millennia (D2-046). The fields hold real
                                                   // numbers (D2-044): the miniature's seals (~11-15) and bears (~26-45) are small
                                                   // but whole populations, above it.

        /// <summary>
        /// The chance a tectonic step (ten years) that lineages this far apart become two kinds: none below 1, small just
        /// past it, certain by 4 - so the many refuges of one range, parted together, do not all split at once.
        /// </summary>
        public static double SplitChance(double dist) => dist <= 1.0 ? 0.0 : DMath.Min(1.0, 0.002 + 0.03 * (dist - 1.0) * (dist - 1.0));

        /// <summary>How far a population's lineage wanders in a tectonic step (standard deviation): faster in short lives.</summary>
        public static double DriftStep(in Species sp) => DriftRate * DMath.Sqrt(Tectonics.StepYears) * DMath.Sqrt(DMath.Sqrt(10.0 / DMath.Max(1.0, sp.Lifespan)));

        /// <summary>The coat a cell asks of a species (C, relative to the species' own cold tolerance).</summary>
        public static double Ideal(PlanetState P, in Species sp, int c) => DMath.Clamp(P.TMean[c] - Margin - sp.ColdTol, -14.0, 12.0);

        /// <summary>Selection for a step (dt years) in one cell; returns the extra mortality of a coat far from the ideal.</summary>
        public static double Select(PlanetState P, in Species sp, int i, int c, double dt, double keep)
        {
            // keep = e^(-Pull (fecundity + 0.5) dt), the share of the old coat kept this step (computed once a step)
            double z = P.Coat[i], ideal = Ideal(P, sp, c);
            P.Coat[i] = (float)(z + (ideal - z) * (1.0 - keep));
            double off = DMath.Abs(ideal - z);
            return off > 3.0 ? Load * (off - 3.0) * dt : 0.0;
        }

        [ThreadStatic] static List<int> _comp, _queue;
        [ThreadStatic] static int[] _mark;

        /// <summary>
        /// At a tectonic step: isolated, diverged populations become species; emptied species are extinct. Returns how
        /// many species formed.
        /// </summary>
        public static int Speciate(Simulation.World w)
        {
            var P = w.Planet;
            var g = P.Grid;
            int n = P.Cells;
            if (_mark == null || _mark.Length != n) _mark = new int[n];
            var comp = _comp ?? (_comp = new List<int>());
            var queue = _queue ?? (_queue = new List<int>());
            int formed = 0;
            int k0 = P.Species.Count;
            for (int s = 0; s < k0; s++)
            {
                var sp = P.Species[s];
                double total = 0;
                for (int c = 0; c < n; c++) total += P.Abund[s * n + c];
                // a kind this few may die out by chance this step (the fewer, the likelier); with none left it is gone
                if (total >= 0.3 && total < MinViable && KeyedRng.Unit(w.Seed, RngStream.Ecology, 0xD1E0UL + (ulong)sp.Id, (ulong)w.Now) < 1.0 - total / MinViable)
                {
                    for (int c = 0; c < n; c++) P.Abund[s * n + c] = 0f;
                    total = 0;
                }
                if (total < 0.3)
                {
                    if (sp.Gone == 0) { sp.Gone = w.Now; P.Species[s] = sp; w.RecordExtinction(s); }
                    continue;
                }
                if (P.Species.Count >= MaxSpecies) continue;
                // the populations of this species: groups of occupied cells with no occupied path between them
                for (int c = 0; c < n; c++) _mark[c] = 0;
                int bestComp = -1; double bestN = 0, bestZ = 0, bestD = 0;
                var groups = new List<(int id, double N, double z, double d, int cells, int first)>();
                int id = 0;
                for (int c0 = 0; c0 < n; c0++)
                {
                    if (_mark[c0] != 0 || P.Abund[s * n + c0] < 0.05f) continue;
                    id++;
                    queue.Clear(); queue.Add(c0); _mark[c0] = id;
                    double N = 0, zN = 0, dN = 0;
                    for (int h = 0; h < queue.Count; h++)
                    {
                        int c = queue[h];
                        double a = P.Abund[s * n + c];
                        N += a; zN += a * P.Coat[s * n + c]; dN += a * P.Drift[s * n + c];
                        for (int q = 0; q < 4; q++)
                        {
                            int nb = g.Nbr4[c * 4 + q];
                            if (_mark[nb] != 0 || P.Abund[s * n + nb] < 0.05f) continue;
                            _mark[nb] = id;
                            queue.Add(nb);
                        }
                    }
                    groups.Add((id, N, N > 0 ? zN / N : 0.0, N > 0 ? dN / N : 0.0, queue.Count, c0));
                    if (N > bestN) { bestN = N; bestComp = id; bestZ = N > 0 ? zN / N : 0.0; bestD = N > 0 ? dN / N : 0.0; }
                }
                if (groups.Count < 2) continue;
                // the most diverged sizeable group (other than the main one) becomes a species of its own: lineages apart
                // long enough (the marker), or sooner where their climates made them different (the coat)
                int pick = -1; double far = 1.0, zMean = 0, dMean = 0;
                foreach (var gr in groups)
                {
                    if (gr.id == bestComp || gr.cells < MinCells || gr.N < MinAnimals) continue;
                    // only time apart makes two kinds; a different climate (the coat) makes them part sooner
                    double dist = DMath.Abs(gr.d - bestD) * (1.0 + CoatWeight * DMath.Abs(gr.z - bestZ));
                    if (dist > far) { far = dist; pick = gr.id; zMean = gr.z; dMean = gr.d; }
                }
                if (pick >= 0)
                {
                    int alive = 0;
                    for (int q = 0; q < P.Species.Count; q++) if (P.Species[q].Kind == sp.Kind && P.Species[q].Gone == 0) alive++;
                    double u = KeyedRng.Unit(w.Seed, RngStream.Ecology, 0x5EC1E5UL + (ulong)sp.Id, (ulong)w.Now);
                    if (alive >= MaxPerKind || u >= SplitChance(far)) pick = -1;   // not this time
                }
                if (pick >= 0) formed += Branch(w, s, pick, zMean, dMean);
                // every sizeable population's lineage wanders a little; where they are one range they wander together
                // (passing fragments at a range's edge do not: they are no lineage of their own, and soon mix back)
                double step = DriftStep(sp);
                var incr = new float[id + 1];   // by group id (ids run 1..id)
                foreach (var gr in groups)
                {
                    if (gr.id == pick || gr.cells < MinCells || gr.N < MinAnimals) continue;
                    double u = KeyedRng.Unit(w.Seed, RngStream.Ecology, 0x6472UL + (ulong)sp.Id, (ulong)w.Now, (ulong)gr.first);
                    incr[gr.id] = (float)((2.0 * u - 1.0) * 1.7320508 * step);   // uniform, standard deviation `step`
                }
                var drift = P.Drift;
                for (int c = 0; c < n; c++) if (_mark[c] != 0) drift[s * n + c] = (float)((double)drift[s * n + c] + incr[_mark[c]]);
                if (P.Species.Count >= MaxSpecies) break;
            }
            Compact(P);
            return formed;
        }

        /// <summary>Extinct kinds leave the fields (the history keeps them): their slots are free for new kinds.</summary>
        static void Compact(PlanetState P)
        {
            int n = P.Cells, k = P.Species.Count, keep = 0;
            for (int s = 0; s < k; s++) if (P.Species[s].Gone == 0) keep++;
            if (keep == k) return;
            var ab = new float[keep * n]; var co = new float[keep * n]; var ct = new float[keep * n]; var dr = new float[keep * n];
            var list = new List<Species>(keep);
            for (int s = 0; s < k; s++)
            {
                if (P.Species[s].Gone != 0) continue;
                int j = list.Count;
                System.Array.Copy(P.Abund, s * n, ab, j * n, n); System.Array.Copy(P.Cond, s * n, co, j * n, n);
                System.Array.Copy(P.Coat, s * n, ct, j * n, n); System.Array.Copy(P.Drift, s * n, dr, j * n, n);
                list.Add(P.Species[s]);
            }
            P.Species.Clear(); P.Species.AddRange(list);
            P.Abund = ab; P.Cond = co; P.Coat = ct; P.Drift = dr;
        }

        /// <summary>The group marked <paramref name="group"/> of species s becomes a new species (its coat its own).</summary>
        static int Branch(Simulation.World w, int s, int group, double zMean, double dMean)
        {
            var P = w.Planet;
            int n = P.Cells, k = P.Species.Count;
            var parent = P.Species[s];
            var child = parent;
            child.Id = w.Ids.Next(EntityKind.Species);
            child.Parent = parent.Id;
            child.Born = w.Now;
            child.Gone = 0;
            child.ColdTol = (float)(parent.ColdTol + zMean);
            // the arrays grow by one species; the group's animals, condition and (re-centred) coats move over
            var ab = new float[(k + 1) * n]; Array.Copy(P.Abund, ab, k * n);
            var co = new float[(k + 1) * n]; Array.Copy(P.Cond, co, k * n);
            var ct = new float[(k + 1) * n]; Array.Copy(P.Coat, ct, k * n);
            var dr = new float[(k + 1) * n]; Array.Copy(P.Drift, dr, k * n);
            int cells = 0, at = -1;
            for (int c = 0; c < n; c++)
            {
                if (_mark[c] != group) continue;
                int i = s * n + c, j = k * n + c;
                ab[j] = ab[i]; co[j] = co[i]; ct[j] = (float)(ct[i] - zMean); dr[j] = (float)(dr[i] - dMean);
                ab[i] = 0f;
                cells++;
                if (at < 0) at = c;
            }
            P.Abund = ab; P.Cond = co; P.Coat = ct; P.Drift = dr;
            P.Species.Add(child);
            string name = (parent.Habitat == 1 ? (zMean < 0 ? "ice " : "warm-water ") : (zMean < 0 ? "snow " : "sand ")) + Ecology.KindName(parent.Kind);
            w.History.Register(new EntityRef(EntityKind.Species, child.Id), name, w.Now);
            w.RecordSpeciation(k, s, at);
            return 1;
        }
    }
}
