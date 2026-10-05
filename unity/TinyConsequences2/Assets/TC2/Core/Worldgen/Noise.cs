using TC2.Core.Foundation;

namespace TC2.Core.Worldgen
{
    /// <summary>Deterministic 3D value noise (hashed lattice, quintic fade) and fractal sums, for worldgen.</summary>
    public static class Noise
    {
        static double Lattice(ulong seed, uint salt, long x, long y, long z)
        {
            ulong h = KeyedRng.Hash(seed, RngStream.WorldgenRelief, (ulong)x * 0x9E3779B1UL ^ salt, (ulong)y, (ulong)z);
            return KeyedRng.Unit(h) * 2.0 - 1.0;
        }

        static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        public static double Value(ulong seed, uint salt, double x, double y, double z)
        {
            double fx = DMath.Floor(x), fy = DMath.Floor(y), fz = DMath.Floor(z);
            long ix = (long)fx, iy = (long)fy, iz = (long)fz;
            double tx = Fade(x - fx), ty = Fade(y - fy), tz = Fade(z - fz);
            double c000 = Lattice(seed, salt, ix, iy, iz), c100 = Lattice(seed, salt, ix + 1, iy, iz);
            double c010 = Lattice(seed, salt, ix, iy + 1, iz), c110 = Lattice(seed, salt, ix + 1, iy + 1, iz);
            double c001 = Lattice(seed, salt, ix, iy, iz + 1), c101 = Lattice(seed, salt, ix + 1, iy, iz + 1);
            double c011 = Lattice(seed, salt, ix, iy + 1, iz + 1), c111 = Lattice(seed, salt, ix + 1, iy + 1, iz + 1);
            double x00 = DMath.Lerp(c000, c100, tx), x10 = DMath.Lerp(c010, c110, tx);
            double x01 = DMath.Lerp(c001, c101, tx), x11 = DMath.Lerp(c011, c111, tx);
            double y0 = DMath.Lerp(x00, x10, ty), y1 = DMath.Lerp(x01, x11, ty);
            return DMath.Lerp(y0, y1, tz);
        }

        /// <summary>Fractal value noise on a unit-sphere point; result roughly in [-1, 1].</summary>
        public static double Fbm(ulong seed, uint salt, Vec3d p, double frequency, int octaves, double gain = 0.5, double lacunarity = 2.0)
        {
            double sum = 0, amp = 1, norm = 0, f = frequency;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(seed, salt + (uint)o * 101u, p.X * f + 17.3, p.Y * f - 9.1, p.Z * f + 3.7);
                norm += amp;
                amp *= gain;
                f *= lacunarity;
            }
            return sum / norm;
        }
    }

    /// <summary>Deterministic syllable names (one simple phonology per culture; F3+ grows this).</summary>
    public static class Names
    {
        static readonly string[] Onsets = { "", "b", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "v", "th", "br", "dr", "gr", "kr", "st", "tr", "sk" };
        static readonly string[] Vowels = { "a", "e", "i", "o", "u", "a", "e", "o", "ai", "ei", "ou", "ae" };
        static readonly string[] Codas = { "", "", "", "n", "r", "l", "s", "th", "k", "m", "nd", "rn", "st", "ld" };
        static readonly string[] PlaceEnds = { "holm", "by", "stead", "ford", "wick", "ton", "mere", "dal", "hope", "thorpe" };
        static readonly string[] MountEnds = { "fell", "tor", "berg", "crag", "pike", "scar" };

        static string Syllable(ref RandomSequence r)
        {
            return Onsets[r.NextBelow(Onsets.Length)] + Vowels[r.NextBelow(Vowels.Length)] + Codas[r.NextBelow(Codas.Length)];
        }

        static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        public static string Person(ulong seed, int id, bool female)
        {
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)id, 1));
            int n = 1 + r.NextBelow(2);
            string s = "";
            for (int i = 0; i < n; i++) s += Syllable(ref r);
            if (s.Length < 3) s += Syllable(ref r);
            if (female && r.Chance(0.6)) s += r.Chance(0.5) ? "a" : "e";
            return Cap(s);
        }

        public static string Family(ulong seed, int householdKey)
        {
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)householdKey, 2));
            string s = Syllable(ref r) + Syllable(ref r);
            return Cap(s);
        }

        public static string Mountain(ulong seed, int key)
        {
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)key, 4));
            string s = Syllable(ref r);
            if (s.Length < 3) s += Syllable(ref r);
            if (s.Length > 5) s = s.Substring(0, 5);
            return r.Chance(0.5) ? "Mount " + Cap(s) : Cap(s + MountEnds[r.NextBelow(MountEnds.Length)]);
        }

        public static string Place(ulong seed, int key)
        {
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)key, 3));
            string s = Syllable(ref r);
            if (r.Chance(0.5)) s += Syllable(ref r);
            s += PlaceEnds[r.NextBelow(PlaceEnds.Length)];
            return Cap(s);
        }
    }
}
