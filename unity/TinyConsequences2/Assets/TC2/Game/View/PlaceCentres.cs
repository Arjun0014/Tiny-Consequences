using System.Collections.Generic;
using TC2.Core.Foundation;

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
        public const double Apart = TC2.Core.History.Whys.LivedApartM;   // m: the homes' centre this far from the anchor before the name and the camera follow it
        static readonly Dictionary<int, (Vec3d at, long when, int version)> _cache = new Dictionary<int, (Vec3d, long, int)>();

        public static Vec3d Of(TC2.Core.Simulation.World w, int sid, int worldVersion)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (_cache.TryGetValue(sid, out var c) && c.version == worldVersion && System.Math.Abs(w.Now - c.when) < 10 * SimTime.Day) return c.at;
            Vec3d at = s.Anchor;
            if (TC2.Core.History.Whys.LivedApart(w, sid, out double mx, out double my, out _)) at = s.LocalToSphere(mx, my, w.Planet.Radius);   // (the place page tells the same)
            _cache[sid] = (at, w.Now, worldVersion);
            return at;
        }
    }
}
