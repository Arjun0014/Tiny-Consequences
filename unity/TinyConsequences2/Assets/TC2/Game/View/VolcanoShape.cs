using TC2.Core.Foundation;
using TC2.Core.Planet;

namespace TC2.Game
{
    /// <summary>
    /// What a volcano looks like on the ground (presentation only, D2-028): a cone over each vent - steep concave flanks
    /// for an arc volcano, a broad low shield over a plume - with a crater at the top and radial gullies; its size grows
    /// with the eruptions it has had. The simulation keeps the mountain's mass at cell scale (its cones and lava raise
    /// the cells); this draws the shape the 245 m cells cannot hold. A per-cell index finds the one vent within reach
    /// (vents stand more than 5 cells apart, so cones never overlap). Rebuilt when the relief or the vents change.
    /// </summary>
    public static class VolcanoShape
    {
        struct Cone { public Vec3d At, East, North; public double H, R, Crater, Depth; public bool Shield; public int Vent; }

        static PlanetState _P;
        static int _relief = -1, _count = -1, _erupted = -1;
        static long _now;

        /// <summary>The world's time for the cones' ages (set by the view each frame; cones are rebuilt with the relief).</summary>
        public static void SetTime(long now) { _now = now; }
        static int[] _cellCone;      // cone index for cells within reach of a vent, -1 none
        static Cone[] _cones = new Cone[0];

        static void Refresh(PlanetState P)
        {
            int erupted = Signature(P);
            if (P == _P && P.ReliefVersion == _relief && P.Vents.Count == _count && erupted == _erupted) return;
            _P = P; _relief = P.ReliefVersion; _count = P.Vents.Count; _erupted = erupted;
            if (_cellCone == null || _cellCone.Length != P.Cells) _cellCone = new int[P.Cells];
            for (int c = 0; c < _cellCone.Length; c++) _cellCone[c] = -1;
            _cones = new Cone[P.Vents.Count];
            var g = P.Grid;
            for (int v = 0; v < P.Vents.Count; v++)
            {
                var vt = P.Vents[v];
                bool shield = vt.Kind == 1;
                // a volcano grows with its age and its eruptions (the worldgen's are old mountains already)
                double age = vt.Born <= 0 ? 1.0 : System.Math.Min(1.0, (_now - vt.Born) / (double)(2000L * TC2.Core.Foundation.SimTime.Year));
                double size = System.Math.Max(0.35, System.Math.Min(1.6, 0.35 + 0.65 * age + 0.22 * System.Math.Log(1.0 + vt.Eruptions)));
                double h = System.Math.Min(340.0, (shield ? 70.0 : 175.0) * size);
                double r = (shield ? 3.4 : 1.9) * P.CellSize * System.Math.Min(1.4, 0.8 + 0.2 * size);
                Vec3d up = vt.At;
                Vec3d east = Vec3d.Cross(new Vec3d(0, 1, 0), up);
                if (east.Length < 1e-6) east = Vec3d.Cross(new Vec3d(1, 0, 0), up);
                east = east.Normalized;
                Vec3d north = Vec3d.Cross(up, east);
                _cones[v] = new Cone { At = up, East = east, North = north, H = h, R = r, Crater = r * (shield ? 0.09 : 0.13), Depth = h * (shield ? 0.12 : 0.22), Shield = shield, Vent = v };
                // index the cells whose centres lie within reach (plus a cell for the kernel's spread)
                double lim = System.Math.Cos((r + 1.5 * P.CellSize) / P.Radius);
                int c0 = vt.Cell;
                var stack = new System.Collections.Generic.List<int> { c0 };
                _cellCone[c0] = v;
                for (int head = 0; head < stack.Count; head++)
                {
                    int c = stack[head];
                    for (int k = 0; k < 8; k++)
                    {
                        int nb = g.Nbr8[c * 8 + k];
                        if (_cellCone[nb] == v || Vec3d.Dot(g.Center[nb], up) < lim) continue;
                        _cellCone[nb] = v;
                        stack.Add(nb);
                    }
                }
            }
        }

        /// <summary>(session 32 QA, s32erupt1) The vents and their eruptions in one number: the ground's patches are built again when it changes
        /// (TerrainLod) - a vent the god opened stood its cone only in the patches built after it, the rest open beside it (dark wedges of the
        /// sea below, a mountain in the air).</summary>
        public static int Signature(PlanetState P)
        {
            int sig = P.Vents.Count * 7919;
            for (int v = 0; v < P.Vents.Count; v++) sig = sig * 31 + P.Vents[v].Eruptions;
            return sig;
        }

        /// <summary>
        /// The cone's drawn height (m) at a direction in cell c, and how much of the point is volcanic ground (0..1:
        /// bare rock and ash at the summit, fading down the flanks; <paramref name="summit"/> 1 inside the crater rim).
        /// </summary>
        public static double At(PlanetState P, Vec3d dir, int c, out float rock, out float summit)
        {
            rock = 0f; summit = 0f;
            if (P.Vents.Count == 0) return 0;
            Refresh(P);
            int k = _cellCone[c];
            if (k < 0) return 0;
            ref var cn = ref _cones[k];
            double dot = Vec3d.Dot(dir, cn.At);
            if (dot >= 1.0) dot = 1.0;
            double r = System.Math.Acos(dot) * P.Radius;
            if (r >= cn.R) return 0;
            double t = r / cn.R;
            double h;
            if (cn.Shield) h = cn.H * (1.0 - t * t) * (1.0 - 0.35 * t);     // a broad dome
            else h = cn.H * System.Math.Pow(1.0 - t, 1.7);                 // concave flanks
            // the crater: a bowl inside the rim
            if (r < cn.Crater)
            {
                double u = r / cn.Crater;
                h -= cn.Depth * (1.0 - u * u);
                summit = 1f;
            }
            // radial gullies down the flanks (an angle round the vent, wobbled by noise)
            double ex = Vec3d.Dot(dir, cn.East), ny = Vec3d.Dot(dir, cn.North);
            double ang = System.Math.Atan2(ny, ex);
            double wob = Noise.Perlin(dir.X * 60.0, dir.Y * 60.0, dir.Z * 60.0) * 0.8;
            double gully = System.Math.Abs(System.Math.Sin(ang * (cn.Shield ? 9.0 : 17.0) + wob * 3.0));
            h -= (cn.Shield ? 2.0 : 4.0) * (1.0 - gully) * t * (1.0 - t) * 4.0;
            // bare lava rock and cinders at the summit; the lower flanks keep their green
            rock = (float)System.Math.Max(0.0, System.Math.Min(1.0, 1.0 - (t - 0.22) / 0.3));
            rock *= cn.Shield ? 0.8f : 1f;
            return h;
        }

        /// <summary>The vent whose cone covers this cell (index into P.Vents), or -1.</summary>
        public static int VentAt(PlanetState P, int c)
        {
            if (P.Vents.Count == 0) return -1;
            Refresh(P);
            int k = _cellCone[c];
            return k < 0 ? -1 : _cones[k].Vent;
        }

        /// <summary>The summit of a vent as drawn (scene position, metres from the planet's centre).</summary>
        public static UnityEngine.Vector3 Summit(PlanetState P, int vent)
        {
            Refresh(P);
            var at = P.Vents[vent].At;
            return Ground.Surface(P, at, 0f);
        }

        /// <summary>A cone's drawn height (for plumes: how high the crater stands).</summary>
        public static double ConeHeight(PlanetState P, int vent)
        {
            Refresh(P);
            return vent >= 0 && vent < _cones.Length ? _cones[vent].H : 0;
        }
    }
}
