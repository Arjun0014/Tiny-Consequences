using System;
using System.IO;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// `woodways --load SAVE` (session 22, D2-156 measured; reads only - each choice made on a copy of the village and put back): what the rules that
        /// keep the walks out of the water change in a world at a moment - each village's woodlot by the straight line (D2-123) against by the way its
        /// cutters walk, and how much farther; each household's croft with and without the beds in the water.
        /// </summary>
        public static int WoodWays(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            var P = w.Planet;
            int villages = 0, differ = 0, longer = 0, none = 0;
            double extra = 0, extraMax = 0, wayOld = 0, wayNew = 0;
            double cropOld = 0, cropNew = 0; int homes = 0, smaller = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                if (!w.Settlements.Get(sid).Inhabited) continue;
                villages++;
                var keep = w.Settlements.Get(sid);   // (Choose writes the woodlot, its distance and may record the woods receding: put back after)
                Afoot.Off = true;
                int oldLot; double oldDist;
                try { oldLot = Woods.Choose(w, sid); oldDist = w.Settlements.Get(sid).WoodDist; }
                finally { Afoot.Off = false; }
                w.Settlements.Get(sid) = keep;
                int newLot = Woods.Choose(w, sid); double newDist = w.Settlements.Get(sid).WoodDist;
                w.Settlements.Get(sid) = keep;
                if (oldLot < 0 && newLot < 0) { none++; continue; }
                wayOld += oldDist; wayNew += newDist;
                if (oldLot != newLot) differ++;
                if (newDist > oldDist + 1.0) { longer++; extra += newDist - oldDist; extraMax = Math.Max(extraMax, newDist - oldDist); }
                if (newLot >= 0)
                {
                    // the woods it fetches across the water with no way round (the fallback): is there woods it could walk to dry-shod a little farther off?
                    ref var s = ref w.Settlements.Get(sid);
                    double gx = 0, gy = 0;
                    Boats.OutOfTheWater(w, in s, ref gx, ref gy);
                    s.SphereToLocal(P.Grid.Center[newLot], P.Radius, out double lx, out double ly);
                    if (!Afoot.Dry(w, in s, gx, gy, lx, ly) && Afoot.Way(w, in s, gx, gy, lx, ly) < 0)
                    {
                        var cells = new System.Collections.Generic.List<int>();
                        w.Hazards.CellsWithin(w, s.Cell, 3.0 * Woods.Reach, cells);
                        double bestWay = -1, bestV = double.MaxValue, bestStraight = 0; float bestF = 0;
                        foreach (int c in cells)
                        {
                            if (!P.IsLand(c) || P.Ice[c] > 0.5f || P.FieldOwner[c] != 0 || P.Forest[c] < Woods.Worth || !TC2.Core.Planet.Shore.Dry(P, P.Grid.Center[c])) continue;
                            double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                            if (d > 3.0 * Woods.Reach) continue;
                            s.SphereToLocal(P.Grid.Center[c], P.Radius, out double cx, out double cy);
                            double way = Afoot.Way(w, in s, gx, gy, cx, cy);
                            if (way < 0) continue;
                            double v = (way + 200.0) / ((double)P.Forest[c] + 0.2);
                            if (v < bestV) { bestV = v; bestWay = way; bestStraight = d; bestF = P.Forest[c]; }
                        }
                        Console.WriteLine("  " + w.History.NameOf(EntityRef.Settlement(sid)) + " (" + s.Population + "): its woods across the water " + newDist.ToString("0") + " m off, no way round; " +
                                          (bestWay < 0 ? "no woods walked to dry-shod within " + (3.0 * Woods.Reach).ToString("0") + " m" :
                                           "the best woods walked to dry-shod: " + bestWay.ToString("0") + " m by the way (" + bestStraight.ToString("0") + " m straight), forest " + bestF.ToString("0.00")));
                    }
                }
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    var hh = w.Households.Get(hl[i]);
                    if (hh.Building == 0 || !w.Buildings.Exists(hh.Building)) continue;
                    Afoot.Off = true;
                    double m0;
                    try { m0 = Gardens.Measure(w, sid, hh.Building, out _, out _); }
                    finally { Afoot.Off = false; }
                    double m1 = Gardens.Measure(w, sid, hh.Building, out _, out _);
                    if (m0 <= 0 && m1 <= 0) continue;
                    homes++; cropOld += m0; cropNew += m1;
                    if (m1 < m0 - 1e-6) smaller++;
                }
            }
            Console.WriteLine("woodways at " + SimTime.Format(w.Now) + ": " + villages + " villages; woodlots chosen otherwise " + differ + ", farther " + longer +
                              " (by " + (extra / Math.Max(1, longer)).ToString("0") + " m on average, at most " + extraMax.ToString("0") + " m), none in reach " + none +
                              "; mean woodlot distance " + (wayOld / Math.Max(1, villages - none)).ToString("0") + " -> " + (wayNew / Math.Max(1, villages - none)).ToString("0") + " m");
            Console.WriteLine("crofts: " + homes + " homes, smaller by the water " + smaller + "; croft area " + cropOld.ToString("0") + " -> " + cropNew.ToString("0") + " m2 (x" +
                              (cropNew / Math.Max(1.0, cropOld)).ToString("0.000") + ")");
            return 0;
        }
    }
}
