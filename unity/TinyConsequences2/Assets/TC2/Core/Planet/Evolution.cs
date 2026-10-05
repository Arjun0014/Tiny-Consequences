using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Adaptation and new species (06 §2, D2-037). Every species carries, in every cell, how far its coat has moved from
    /// its kind's (`P.Coat`, C of cold tolerance: below zero bears colder). Each step the local population is pulled
    /// toward the coat that place asks for (a 7 C margin under its annual mean) - slowly, faster for fast breeders; a
    /// coat far from that costs lives; wanderers carry their coats and mix them where they arrive. At each tectonic step
    /// a population cut off from the rest of its kind (no occupied path between them) whose coats differ by more than
    /// 5 C becomes a species of its own - the history tells of it - and a species with no animals left is extinct. New
    /// species come only from isolation and the climates that shaped them apart.
    /// </summary>
    public static class Evolution
    {
        public const double Margin = 7.0;          // C: the coat a place asks for leaves this margin under its mean
        public const double Pull = 0.0015;         // per year per (young per adult): how fast a population follows (e-fold ~250 y)
        public const double Split = 5.0;           // C of coat between two isolated populations that makes two species
        public const int MaxSpecies = 16;

        /// <summary>The coat a cell asks of a species (C, relative to the species' own cold tolerance).</summary>
        public static double Ideal(PlanetState P, in Species sp, int c) => DMath.Clamp(P.TMean[c] - Margin - sp.ColdTol, -14.0, 12.0);

        /// <summary>Selection for a step (dt years) in one cell; returns the extra mortality of a coat far from the ideal.</summary>
        public static double Select(PlanetState P, in Species sp, int i, int c, double dt, double keep)
        {
            // keep = e^(-Pull (fecundity + 0.5) dt), the share of the old coat kept this step (computed once a step)
            double z = P.Coat[i], ideal = Ideal(P, sp, c);
            P.Coat[i] = (float)(z + (ideal - z) * (1.0 - keep));
            double off = DMath.Abs(ideal - z);
            return off > 3.0 ? 0.01 * (off - 3.0) * dt : 0.0;
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
                if (total < 0.3)
                {
                    if (sp.Gone == 0) { sp.Gone = w.Now; P.Species[s] = sp; w.RecordExtinction(s); }
                    continue;
                }
                if (P.Species.Count >= MaxSpecies) continue;
                // the populations of this species: groups of occupied cells with no occupied path between them
                for (int c = 0; c < n; c++) _mark[c] = 0;
                int bestComp = -1; double bestN = 0, bestZ = 0;
                var groups = new List<(int id, double N, double z, int cells)>();
                int id = 0;
                for (int c0 = 0; c0 < n; c0++)
                {
                    if (_mark[c0] != 0 || P.Abund[s * n + c0] < 0.05f) continue;
                    id++;
                    queue.Clear(); queue.Add(c0); _mark[c0] = id;
                    double N = 0, zN = 0;
                    for (int h = 0; h < queue.Count; h++)
                    {
                        int c = queue[h];
                        double a = P.Abund[s * n + c];
                        N += a; zN += a * P.Coat[s * n + c];
                        for (int q = 0; q < 4; q++)
                        {
                            int nb = g.Nbr4[c * 4 + q];
                            if (_mark[nb] != 0 || P.Abund[s * n + nb] < 0.05f) continue;
                            _mark[nb] = id;
                            queue.Add(nb);
                        }
                    }
                    groups.Add((id, N, N > 0 ? zN / N : 0.0, queue.Count));
                    if (N > bestN) { bestN = N; bestComp = id; bestZ = N > 0 ? zN / N : 0.0; }
                }
                if (groups.Count < 2) continue;
                // the most diverged sizeable group (other than the main one) becomes a species of its own
                int pick = -1; double far = Split;
                foreach (var gr in groups)
                {
                    if (gr.id == bestComp || gr.cells < 12 || gr.N < 20) continue;
                    double d = DMath.Abs(gr.z - bestZ);
                    if (d > far) { far = d; pick = gr.id; }
                }
                if (pick < 0) continue;
                double zMean = 0;
                foreach (var gr in groups) if (gr.id == pick) zMean = gr.z;
                formed += Branch(w, s, pick, zMean);
                if (P.Species.Count >= MaxSpecies) break;
            }
            return formed;
        }

        /// <summary>The group marked <paramref name="group"/> of species s becomes a new species (its coat its own).</summary>
        static int Branch(Simulation.World w, int s, int group, double zMean)
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
            int cells = 0, at = -1;
            for (int c = 0; c < n; c++)
            {
                if (_mark[c] != group) continue;
                int i = s * n + c, j = k * n + c;
                ab[j] = ab[i]; co[j] = co[i]; ct[j] = (float)(ct[i] - zMean);
                ab[i] = 0f;
                cells++;
                if (at < 0) at = c;
            }
            P.Abund = ab; P.Cond = co; P.Coat = ct;
            P.Species.Add(child);
            string name = (zMean < 0 ? "snow " : "sand ") + Ecology.KindName(parent.Kind);
            w.History.Register(new EntityRef(EntityKind.Species, child.Id), name, w.Now);
            w.RecordSpeciation(k, s, at);
            return 1;
        }
    }
}
