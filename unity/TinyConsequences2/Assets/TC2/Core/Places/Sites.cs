using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Places to live (D2-045): how good a cell is for a new village, judged as a scout would judge what they see - warmth and
    /// rain enough, low ground, fresh water or a coast, arable land within a short walk that no village's fields hold, and no
    /// village too near. Knowledge of such places comes only from someone who went and looked (`KnownSites`).
    /// </summary>
    public static class Sites
    {
        public const double Spacing = 1900;    // m: a new village keeps this far from any other (two fields' reaches)
        public const double LookRadius = 500;  // m: what a scout sees round where they stand
        public const double MinQuality = 0.25; // a site worth moving a household to

        /// <summary>The worldgen habitability of a cell (the same form as WorldGenerator's site score, without its noise).</summary>
        public static double Habitability(PlanetState P, int c)
        {
            if (!P.IsLand(c) || P.Ice[c] > 0.5f || P.LakeDepth[c] > 0.3f) return 0;
            double tm = P.TMean[c], m = P.Moist[c];
            double s = DMath.SmoothStep(4, 11, tm) * (1.0 - DMath.SmoothStep(24, 30, tm));
            s *= DMath.SmoothStep(0.22, 0.4, m) * (1.0 - DMath.SmoothStep(0.85, 1.0, m));
            s *= 1.0 - DMath.SmoothStep(500, 1100, P.Elev[c] - P.SeaLevel);
            var g = P.Grid;
            bool coastal = false;
            for (int j = 0; j < 4; j++) if (!P.IsLand(g.Nbr4[c * 4 + j])) coastal = true;
            if (coastal) s *= 1.25;
            if (P.FreshWaterNear(c)) s *= 1.35;
            return s;
        }

        /// <summary>
        /// A site's worth (0..~1.7): habitable, with water (fresh, or the sea's shore for fishing folk), arable land within the
        /// fields' reach that no village claims (enough for ~25 people at full), and no village within the spacing.
        /// </summary>
        public static double Quality(World w, int c, List<int> scratch)
        {
            var P = w.Planet;
            double hab = Habitability(P, c);
            if (hab <= 0) return 0;
            if (!P.FreshWaterNear(c))
            {
                bool shore = false;
                for (int k = 0; k < 8; k++) if (!P.IsLand(P.Grid.Nbr8[c * 8 + k])) shore = true;
                if (!shore) return 0;   // no water to drink or fish: no village
            }
            Vec3d at = P.Grid.Center[c];
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                if (Vec3d.ArcDistance(at, s.Anchor, P.Radius) < Spacing) return 0;
            }
            w.Hazards.CellsWithin(w, c, w.Config.FieldReach, scratch);
            double arable = 0;
            for (int i = 0; i < scratch.Count; i++)
            {
                int a = scratch[i];
                if (!P.IsLand(a) || P.FieldOwner[a] != 0) continue;
                arable += P.AreaHa(a) * P.Arability(a, w.PlanetSys.MeanTemp(a));
            }
            return hab * DMath.Min(1.0, arable / (25 * w.Config.FieldHaPerPerson));
        }
    }

    /// <summary>
    /// A village's knowledge of places (D2-045): the three best sites its people have seen with their own eyes (a scout, a
    /// hunter far afield), with how good they looked and when. Saved state; it ages - an old report is checked again when used.
    /// </summary>
    public struct KnownSites
    {
        public int C0, C1, C2;        // cells (-1 none)
        public float Q0, Q1, Q2;      // how good each looked
        public long T0, T1, T2;       // when they were seen

        public static KnownSites Empty => new KnownSites { C0 = -1, C1 = -1, C2 = -1 };

        public int Count => (C0 >= 0 ? 1 : 0) + (C1 >= 0 ? 1 : 0) + (C2 >= 0 ? 1 : 0);

        /// <summary>Remembers a site (replacing the worst known, or updating the same cell).</summary>
        public void Learn(int cell, double q, long t)
        {
            if (cell < 0 || q <= 0) return;
            if (C0 == cell) { Q0 = (float)q; T0 = t; return; }
            if (C1 == cell) { Q1 = (float)q; T1 = t; return; }
            if (C2 == cell) { Q2 = (float)q; T2 = t; return; }
            if (C0 < 0) { C0 = cell; Q0 = (float)q; T0 = t; return; }
            if (C1 < 0) { C1 = cell; Q1 = (float)q; T1 = t; return; }
            if (C2 < 0) { C2 = cell; Q2 = (float)q; T2 = t; return; }
            // the least worth
            if (Q0 <= Q1 && Q0 <= Q2) { if (q > Q0) { C0 = cell; Q0 = (float)q; T0 = t; } }
            else if (Q1 <= Q2) { if (q > Q1) { C1 = cell; Q1 = (float)q; T1 = t; } }
            else if (q > Q2) { C2 = cell; Q2 = (float)q; T2 = t; }
        }

        public void Forget(int cell)
        {
            if (C0 == cell) { C0 = -1; Q0 = 0; T0 = 0; }
            if (C1 == cell) { C1 = -1; Q1 = 0; T1 = 0; }
            if (C2 == cell) { C2 = -1; Q2 = 0; T2 = 0; }
        }

        public int Best(out float q)
        {
            int c = -1; q = 0;
            if (C0 >= 0 && Q0 > q) { c = C0; q = Q0; }
            if (C1 >= 0 && Q1 > q) { c = C1; q = Q1; }
            if (C2 >= 0 && Q2 > q) { c = C2; q = Q2; }
            return c;
        }

        public void Write(BinWriter w) { w.Int(C0); w.Int(C1); w.Int(C2); w.Float(Q0); w.Float(Q1); w.Float(Q2); w.Long(T0); w.Long(T1); w.Long(T2); }
        public static KnownSites Read(BinReader r) => new KnownSites { C0 = r.Int(), C1 = r.Int(), C2 = r.Int(), Q0 = r.Float(), Q1 = r.Float(), Q2 = r.Float(), T0 = r.Long(), T1 = r.Long(), T2 = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(C0); h.Add(C1); h.Add(C2); h.Add(Q0); h.Add(Q1); h.Add(Q2); h.Add(T0); h.Add(T1); h.Add(T2); }
    }
}
