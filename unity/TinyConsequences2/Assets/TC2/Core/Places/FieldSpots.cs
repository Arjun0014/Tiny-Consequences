using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Where a household's hands work the village's fields (session 20): its own part of them - one of the village's field cells beyond its houses,
    /// chosen by the household and kept all the year (in the record a household's strips lay scattered through the open fields; here a cell a
    /// household) - for the crop's work and for carting the muck out. Before, every field hand and every carter of a village went to its one field
    /// spot (`Settlement.FieldX/FieldY`, +-60 m): a town's hundred carters stood in one crowd on a few plots (`act:Muck`: 126 within 60 m).
    /// </summary>
    public static class FieldSpots
    {
        /// <summary>Tools: the village's one field spot as before (`--onefieldspot`), on the same binary.</summary>
        public static bool One;

        /// <summary>A spot on household `hid`'s part of the village's fields (`jitter` m about its cell's middle); the one field spot with `--onefieldspot`
        /// or where the village has no field cell beyond its houses. Two draws of `rs` either way.</summary>
        public static void Spot(World w, in Settlement s, int hid, double jitter, ref RandomSequence rs, out double x, out double y)
        {
            double dx = rs.Range(-jitter, jitter), dy = rs.Range(-jitter, jitter);
            x = s.FieldX + dx; y = s.FieldY + dy;
            if (One) return;
            var P = w.Planet;
            var fc = w.FieldCellsOf(s.Id);
            int n = 0;
            for (int i = 0; i < fc.Count; i++) if (Beyond(w, in s, fc[i])) n++;
            if (n == 0) return;
            int k = (int)(KeyedRng.Mix((ulong)hid * 0x9E3779B97F4A7C15UL ^ 0xF1E1D5UL) % (ulong)n);
            for (int i = 0; i < fc.Count; i++)
            {
                if (!Beyond(w, in s, fc[i])) continue;
                if (k-- > 0) continue;
                s.SphereToLocal(P.Grid.Center[fc[i]], P.Radius, out double cx, out double cy);
                x = cx + dx; y = cy + dy;
                return;
            }
        }

        /// <summary>A field cell beyond the village's houses and on dry ground (as the herds' grounds are).</summary>
        static bool Beyond(World w, in Settlement s, int c)
        {
            var P = w.Planet;
            return P.IsLand(c) && P.LakeDepth[c] <= 0.3f && Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius) >= 140.0;
        }
    }
}
