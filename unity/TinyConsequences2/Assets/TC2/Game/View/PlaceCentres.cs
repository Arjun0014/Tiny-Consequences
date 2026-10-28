using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Places;

namespace TC2.Game
{
    /// <summary>
    /// (session 29) Where a village's people live, for what points at the village - its name on the map, the pointer from afar, "go there": the centre
    /// of its lived-in homes when that lies more than <see cref="Apart"/> from its anchor, else the anchor. A village keeps its anchor - its first square -
    /// for ever (its roads and reach are measured from it), but its homes can move away from it: Kaihope's people (seed 2, year 600) live 250-400 m from
    /// a first square that lies in ruins since the sea took homes and they built on higher ground, and the camera and its name went to the ruins. Read
    /// from the buildings, cached for ten world days. Reads the world only.
    /// </summary>
    public static class PlaceCentres
    {
        public const double Apart = 120.0;   // m: the homes' centre this far from the anchor before the name and the camera follow it
        static readonly Dictionary<int, (Vec3d at, long when, int version)> _cache = new Dictionary<int, (Vec3d, long, int)>();

        public static Vec3d Of(TC2.Core.Simulation.World w, int sid, int worldVersion)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (_cache.TryGetValue(sid, out var c) && c.version == worldVersion && System.Math.Abs(w.Now - c.when) < 10 * SimTime.Day) return c.at;
            Vec3d at = s.Anchor;
            double sx = 0, sy = 0; int n = 0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (!Structures.IsHome(b.Kind) || b.Household == 0 || Homes.Fallen(in b, w.Now)) continue;
                sx += b.X; sy += b.Y; n++;
            }
            if (n > 0)
            {
                double mx = sx / n, my = sy / n;
                if (mx * mx + my * my > Apart * Apart) at = s.LocalToSphere(mx, my, w.Planet.Radius);
            }
            _cache[sid] = (at, w.Now, worldVersion);
            return at;
        }
    }
}
