using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Diagnostics
{
    /// <summary>
    /// Tools only (D2-085, the planet's size): how far the world's contacts reach - marriages, trades, relief, households moving,
    /// foundings, takings, raids, claims, sickness carried, visits, tribute - as the distance between the two villages each joins.
    /// Off (null) unless a tool turns it on; never saved or hashed, never read by the simulation.
    /// </summary>
    public static class ScaleTally
    {
        public const int Marriage = 0, Trade = 1, Relief = 2, Party = 3, Founding = 4, Taking = 5, Raid = 6, Claim = 7, Sickness = 8, Visit = 9, Tribute = 10, Kinds = 11;
        public static readonly string[] Names = { "marriage", "trade", "relief", "party", "founding", "taking", "raid", "claim", "sickness", "visit", "tribute" };

        /// <summary>Per kind: count, same-village count, sum of km, sum of km squared, the largest km, count beyond 3 km.</summary>
        public static double[] Sums;
        public const int Stride = 6;

        public static void Pair(World w, int kind, int a, int b)
        {
            var t = Sums;
            if (t == null || a == 0 || b == 0 || !w.Settlements.Exists(a) || !w.Settlements.Exists(b)) return;
            double km = a == b ? 0.0 : Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius) / 1000.0;
            int o = kind * Stride;
            t[o]++;
            if (a == b) t[o + 1]++;
            t[o + 2] += km; t[o + 3] += km * km;
            if (km > t[o + 4]) t[o + 4] = km;
            if (km > 3.0) t[o + 5]++;
        }
    }
}
