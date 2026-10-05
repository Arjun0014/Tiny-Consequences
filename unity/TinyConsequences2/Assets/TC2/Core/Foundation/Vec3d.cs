namespace TC2.Core.Foundation
{
    /// <summary>Double-precision 3-vector for planet geometry (deterministic: + - * / sqrt only).</summary>
    public struct Vec3d
    {
        public double X, Y, Z;

        public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3d Zero = new Vec3d(0, 0, 0);
        public static readonly Vec3d UnitX = new Vec3d(1, 0, 0);
        public static readonly Vec3d UnitY = new Vec3d(0, 1, 0);
        public static readonly Vec3d UnitZ = new Vec3d(0, 0, 1);

        public static Vec3d operator +(Vec3d a, Vec3d b) => new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3d operator -(Vec3d a, Vec3d b) => new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3d operator -(Vec3d a) => new Vec3d(-a.X, -a.Y, -a.Z);
        public static Vec3d operator *(Vec3d a, double s) => new Vec3d(a.X * s, a.Y * s, a.Z * s);
        public static Vec3d operator *(double s, Vec3d a) => new Vec3d(a.X * s, a.Y * s, a.Z * s);
        public static Vec3d operator /(Vec3d a, double s) => new Vec3d(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vec3d Cross(Vec3d a, Vec3d b) => new Vec3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public double LengthSq => X * X + Y * Y + Z * Z;
        public double Length => DMath.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3d Normalized
        {
            get
            {
                double l = DMath.Sqrt(X * X + Y * Y + Z * Z);
                return l > 0 ? new Vec3d(X / l, Y / l, Z / l) : new Vec3d(0, 0, 1);
            }
        }

        /// <summary>Angle in radians between two unit vectors (robust for small angles).</summary>
        public static double Angle(Vec3d a, Vec3d b)
        {
            Vec3d c = Cross(a, b);
            return DMath.Atan2(c.Length, Dot(a, b));
        }

        /// <summary>Great-circle distance on a sphere of radius r between unit vectors.</summary>
        public static double ArcDistance(Vec3d a, Vec3d b, double r) => Angle(a, b) * r;

        /// <summary>Spherical interpolation between unit vectors.</summary>
        public static Vec3d Slerp(Vec3d a, Vec3d b, double t)
        {
            double ang = Angle(a, b);
            if (ang < 1e-12) return a;
            double s = DMath.Sin(ang);
            double wa = DMath.Sin((1 - t) * ang) / s;
            double wb = DMath.Sin(t * ang) / s;
            return (a * wa + b * wb).Normalized;
        }

        /// <summary>Rotates v about the unit axis k by angle a (Rodrigues).</summary>
        public static Vec3d Rotate(Vec3d v, Vec3d k, double a)
        {
            double c = DMath.Cos(a), s = DMath.Sin(a);
            return v * c + Cross(k, v) * s + k * (Dot(k, v) * (1 - c));
        }

        /// <summary>Latitude (radians, +north) of a unit vector with Y as the planet's axis.</summary>
        public double Latitude => DMath.Asin(DMath.Clamp(Y, -1, 1));
        /// <summary>Longitude (radians) of a unit vector with Y as the axis.</summary>
        public double Longitude => DMath.Atan2(X, Z);

        public static Vec3d FromLatLon(double lat, double lon)
        {
            double cl = DMath.Cos(lat);
            return new Vec3d(cl * DMath.Sin(lon), DMath.Sin(lat), cl * DMath.Cos(lon));
        }

        public override string ToString() => "(" + X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ", " + Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ", " + Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }
}
