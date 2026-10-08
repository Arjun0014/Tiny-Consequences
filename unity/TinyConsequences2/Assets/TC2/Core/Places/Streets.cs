using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// A village's streets (C13.1, D2-116). Villages grow along their roads: the ways out of a village that its people wear to paths become
    /// its streets, and the frontage along them is the lot a household seeks first - closer to the comings and goings, the carts and the
    /// neighbours' traffic - so new houses line the streets and face them, and a market village strings out along its roads (the street
    /// villages and market streets of medieval Europe). A hamlet whose people wear no way to a neighbour keeps its houses round its green
    /// (the spiral of lots, D2-084). A street is durable: laid once, at the bearing at which the way actually leaves the village (read along
    /// its route), it stays while houses line it even when the way falls out of use; ways leaving within 35 degrees of each other are one
    /// street; four at most. Nothing is built in a street (its half-width is kept clear of houses and works).
    /// </summary>
    public static class Streets
    {
        /// <summary>Streets a village keeps at most.</summary>
        public const int Most = 4;
        /// <summary>Radians within which two ways leave by one street (35 degrees).</summary>
        public const double Merge = 0.6108652381980153;
        /// <summary>m along the way at which a street's bearing is read (where it leaves the village).</summary>
        public const double Look = 80.0;
        /// <summary>m between frontage lots along a street (a longhouse's length and a gap), the house's set back from the street's line,
        /// and the first frontage lot's distance from the square.</summary>
        public const double Spacing = 13.0, Setback = 8.0, Start = 22.0;
        /// <summary>m either side of a street's line kept clear of houses and works.</summary>
        public const double Width = 4.0;
        /// <summary>A frontage lot is weighed as though it lay this share of its distance from the square (frontage is sought).</summary>
        public const double Lean = 0.7;

        static readonly List<Way> _ways = new List<Way>();

        /// <summary>Tools: no streets are laid (C13.1 against D2-114 in one binary); false in the game.</summary>
        public static bool None;

        /// <summary>
        /// The village's ways worn to paths that are not yet its streets are laid as streets (read once a lot is sought): the bearing at which
        /// each leaves, merged with a street within 35 degrees.
        /// </summary>
        public static void Refresh(World w, int sid)
        {
            if (None || w.Settlements.Get(sid).StreetN >= Most) return;
            w.Ways.PathsOf(sid, _ways);
            for (int i = 0; i < _ways.Count; i++)
            {
                if (w.Settlements.Get(sid).StreetN >= Most) return;
                int other = _ways[i].A == sid ? _ways[i].B : _ways[i].A;
                if (!w.Settlements.Exists(other)) continue;
                var r = w.Routes.Get(w, sid, other);
                if (r == null || r.Length < 1.0) continue;
                double along = DMath.Min(Look, 0.5 * r.Length);
                Vec3d p = r.PointAt(r.A == sid ? along : r.Length - along);
                ref var s = ref w.Settlements.Get(sid);
                s.SphereToLocal(p, w.Planet.Radius, out double x, out double y);
                double dx = x - s.SquareX, dy = y - s.SquareY;
                if (dx * dx + dy * dy < 1.0) continue;
                double a = DMath.Atan2(dy, dx);
                bool same = false;
                for (int k = 0; k < s.StreetN; k++) if (Apart(a, s.StreetAt(k)) < Merge) { same = true; break; }
                if (same) continue;
                s.SetStreet(s.StreetN, a);
                s.StreetN++;
            }
        }

        /// <summary>The angle between two bearings (0 to pi).</summary>
        public static double Apart(double a, double b)
        {
            double d = a - b;
            while (d > DMath.PI) d -= 2.0 * DMath.PI;
            while (d < -DMath.PI) d += 2.0 * DMath.PI;
            return DMath.Abs(d);
        }

        /// <summary>A frontage lot: the j-th along street i (j from 0), on side -1 or +1.</summary>
        public static void Lot(in Settlement s, int i, int j, int side, out double x, out double y)
        {
            double a = s.StreetAt(i), d = Start + j * Spacing;
            double ux = DMath.Cos(a), uy = DMath.Sin(a);
            x = s.SquareX + ux * d - uy * Setback * side;
            y = s.SquareY + uy * d + ux * Setback * side;
        }

        /// <summary>Whether a point lies in a street (within `half` of its line, beyond the green).</summary>
        public static bool On(in Settlement s, double x, double y, double half)
        {
            for (int i = 0; i < s.StreetN; i++)
            {
                double a = s.StreetAt(i), ux = DMath.Cos(a), uy = DMath.Sin(a);
                double px = x - s.SquareX, py = y - s.SquareY;
                double along = px * ux + py * uy;
                if (along < 12.0) continue;
                double off = DMath.Abs(-px * uy + py * ux);
                if (off < half) return true;
            }
            return false;
        }

        /// <summary>
        /// What a house at (x, y) faces: the nearest point of a street within its frontage (C13.1), else the square. True when it fronts a
        /// street.
        /// </summary>
        public static bool Facing(in Settlement s, double x, double y, out double fx, out double fy)
        {
            fx = s.SquareX; fy = s.SquareY;
            double best = Setback + 6.0;
            bool found = false;
            for (int i = 0; i < s.StreetN; i++)
            {
                double a = s.StreetAt(i), ux = DMath.Cos(a), uy = DMath.Sin(a);
                double px = x - s.SquareX, py = y - s.SquareY;
                double along = px * ux + py * uy;
                if (along < 12.0) continue;
                double off = DMath.Abs(-px * uy + py * ux);
                if (off >= best) continue;
                best = off; found = true;
                fx = s.SquareX + ux * along; fy = s.SquareY + uy * along;
            }
            return found;
        }
    }
}
