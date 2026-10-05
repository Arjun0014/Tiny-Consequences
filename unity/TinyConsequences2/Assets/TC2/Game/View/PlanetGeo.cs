using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Mapping between simulation geometry (unit vectors, metres) and the scene (planet at the origin,
    /// 1 unit = 1 m). Relief is drawn at <see cref="VScale"/> of its simulated height (presentation only).
    /// </summary>
    public static class PlanetGeo
    {
        public const float VScale = 0.5f;

        /// <summary>Simulated elevation at a direction: bilinear over the cell centres of its cube face.</summary>
        public static double Elevation(PlanetState P, Vec3d dir)
        {
            CubeSphere.ToFace(dir, out int f, out double a, out double b);
            return ElevationFace(P, f, a, b);
        }

        public static double ElevationFace(PlanetState P, int f, double a, double b)
        {
            int n = P.Grid.N;
            double step = DMath.HalfPi / n;
            double u = (a + DMath.PI / 4) / step - 0.5, v = (b + DMath.PI / 4) / step - 0.5;
            int i0 = (int)System.Math.Floor(u), j0 = (int)System.Math.Floor(v);
            double fu = u - i0, fv = v - j0;
            if (i0 < 0) { i0 = 0; fu = 0; } else if (i0 >= n - 1) { i0 = n - 2; fu = 1; }
            if (j0 < 0) { j0 = 0; fv = 0; } else if (j0 >= n - 1) { j0 = n - 2; fv = 1; }
            int c00 = f * n * n + j0 * n + i0;
            // the surface: rock plus the ice sheets on it
            double e00 = P.Elev[c00] + P.Ice[c00], e10 = P.Elev[c00 + 1] + P.Ice[c00 + 1], e01 = P.Elev[c00 + n] + P.Ice[c00 + n], e11 = P.Elev[c00 + n + 1] + P.Ice[c00 + n + 1];
            return (e00 * (1 - fu) + e10 * fu) * (1 - fv) + (e01 * (1 - fu) + e11 * fu) * fv;
        }

        /// <summary>Scene position of a surface point (on land, never below the sea surface).</summary>
        public static Vector3 Surface(PlanetState P, Vec3d dir, float lift = 0f)
        {
            double e = Elevation(P, dir);
            double h = System.Math.Max(e, P.SeaLevel);
            double r = P.Radius + h * VScale + lift;
            return new Vector3((float)(dir.X * r), (float)(dir.Y * r), (float)(dir.Z * r));
        }

        public static Vector3 ToVector(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        public static Vec3d ToVec(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        /// <summary>A rotation whose up is the local surface normal (forward roughly north).</summary>
        public static Quaternion Upright(Vector3 up, float yawDegrees)
        {
            Vector3 n = up.normalized;
            Vector3 fwd = Vector3.Cross(Vector3.right, n);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(Vector3.forward, n);
            return Quaternion.AngleAxis(yawDegrees, n) * Quaternion.LookRotation(fwd.normalized, n);
        }

        public static Color BiomeColor(Biome b)
        {
            switch (b)
            {
                case Biome.DeepOcean: return new Color(0.05f, 0.12f, 0.24f);
                case Biome.Ocean: return new Color(0.09f, 0.22f, 0.36f);
                case Biome.Coast: return new Color(0.18f, 0.40f, 0.48f);
                case Biome.Ice: return new Color(0.92f, 0.95f, 0.98f);
                case Biome.Tundra: return new Color(0.58f, 0.60f, 0.52f);
                case Biome.Taiga: return new Color(0.17f, 0.33f, 0.24f);
                case Biome.Grassland: return new Color(0.50f, 0.66f, 0.30f);
                case Biome.Woodland: return new Color(0.32f, 0.52f, 0.22f);
                case Biome.Forest: return new Color(0.18f, 0.40f, 0.16f);
                case Biome.Jungle: return new Color(0.10f, 0.36f, 0.14f);
                case Biome.Savanna: return new Color(0.70f, 0.64f, 0.34f);
                case Biome.Desert: return new Color(0.86f, 0.76f, 0.52f);
                case Biome.Mountain: return new Color(0.48f, 0.44f, 0.40f);
                case Biome.Snowcap: return new Color(0.94f, 0.95f, 0.97f);
                case Biome.Burnt: return new Color(0.20f, 0.17f, 0.14f);
                case Biome.Farmland: return new Color(0.74f, 0.66f, 0.36f);
                default: return Color.magenta;
            }
        }
    }
}
