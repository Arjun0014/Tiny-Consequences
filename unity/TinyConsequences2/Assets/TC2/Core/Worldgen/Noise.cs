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

        // ---- a people's tongue (C11.1): the sounds its names are made of, as a code - which onsets (beyond none), vowels, codas (beyond none) and
        // place-name endings it uses, its women's names' ending and how long its given names run. Carried by founders, a sound at a time changed
        // (`DriftTongue`): neighbours founded from one another speak alike, a line's far daughters differently, a first people's own tongue apart.
        const int OnsetBit = 0, VowelBit = 22, CodaBit = 34, EndBit = 45, FemBit = 55, LenBit = 57;
        const int OnsetN = 22, VowelN = 12, CodaN = 11, EndN = 10;   // (Onsets[1..22], Vowels[0..11], Codas[3..13], PlaceEnds[0..9])
        const int CodaEmpty = 3;                                      // (the empty codas: Codas[0..2] - most syllables end open)
        const int OnsetMin = 5, VowelMin = 3, CodaMin = 2, EndMin = 1;

        static int Count(long t, int bit, int n) { int c = 0; for (int i = 0; i < n; i++) if (((t >> (bit + i)) & 1L) != 0) c++; return c; }
        static int Nth(long t, int bit, int n, int k) { for (int i = 0; i < n; i++) if (((t >> (bit + i)) & 1L) != 0 && k-- == 0) return i; return 0; }

        /// <summary>A first people's own tongue: about ten onsets, six vowels, five codas, two or three endings of its places.</summary>
        public static long NewTongue(ulong seed, int key)
        {
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)key, 11));
            long t = 0;
            t |= Pick(ref r, OnsetBit, OnsetN, 9 + r.NextBelow(5));
            t |= Pick(ref r, VowelBit, VowelN, 5 + r.NextBelow(4));
            t |= Pick(ref r, CodaBit, CodaN, 4 + r.NextBelow(4));
            t |= Pick(ref r, EndBit, EndN, 2 + r.NextBelow(2));
            t |= (long)r.NextBelow(4) << FemBit;
            t |= (long)r.NextBelow(3) << LenBit;
            return t;
        }

        static long Pick(ref RandomSequence r, int bit, int n, int want)
        {
            long m = 0;
            int have = 0;
            while (have < want && have < n)
            {
                int i = r.NextBelow(n);
                if (((m >> i) & 1L) != 0) continue;
                m |= 1L << i; have++;
            }
            return m << bit;
        }

        /// <summary>
        /// The tongue a village's founders bring from the one they came from: as a few people carry a speech somewhere new, now and then (half the
        /// foundings) one thing about it changes - a sound taken up or let go, a place-name ending, the ending of women's names, the length of names -
        /// never below what a tongue needs.
        /// </summary>
        public static long DriftTongue(ulong seed, int key, long t)
        {
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)key, 12));
            if (!r.Chance(TongueDrift)) return t;
            double u = r.NextUnit();
            if (u < 0.40) return Flip(ref r, t, OnsetBit, OnsetN, OnsetMin);
            if (u < 0.55) return Flip(ref r, t, VowelBit, VowelN, VowelMin);
            if (u < 0.75) return Flip(ref r, t, CodaBit, CodaN, CodaMin);
            if (u < 0.90) return Flip(ref r, t, EndBit, EndN, EndMin);
            if (u < 0.95) return (t & ~(3L << FemBit)) | ((long)r.NextBelow(4) << FemBit);
            return (t & ~(3L << LenBit)) | ((long)r.NextBelow(3) << LenBit);
        }

        /// <summary>The chance a founding's speech changes one thing.</summary>
        public const double TongueDrift = 0.5;

        static long Flip(ref RandomSequence r, long t, int bit, int n, int min)
        {
            int i = r.NextBelow(n);
            bool on = ((t >> (bit + i)) & 1L) != 0;
            if (on && Count(t, bit, n) <= min)
            {
                // (it cannot lose a sound it needs: it takes one up instead)
                for (int k = 0; k < n; k++) { int j = (i + k) % n; if (((t >> (bit + j)) & 1L) == 0) return t | (1L << (bit + j)); }
                return t;
            }
            return t ^ (1L << (bit + i));
        }

        static string Syllable(ref RandomSequence r, long t)
        {
            int no = Count(t, OnsetBit, OnsetN), nv = Count(t, VowelBit, VowelN), nc = Count(t, CodaBit, CodaN);
            int o = r.NextBelow(no + 1);   // (or none)
            string on = o == no ? "" : Onsets[1 + Nth(t, OnsetBit, OnsetN, o)];
            string vo = Vowels[Nth(t, VowelBit, VowelN, r.NextBelow(System.Math.Max(1, nv)))];
            int c = r.NextBelow(nc + CodaEmpty);
            string co = c >= nc ? "" : Codas[CodaEmpty + Nth(t, CodaBit, CodaN, c)];
            return on + vo + co;
        }

        /// <summary>A person's given name in their people's tongue.</summary>
        public static string Person(ulong seed, int id, bool female, long tongue)
        {
            if (tongue == 0) return Person(seed, id, female);
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)id, 1));
            int len = (int)((tongue >> LenBit) & 3L);
            int n = len == 1 ? (r.Chance(0.75) ? 1 : 2) : len == 2 ? (r.Chance(0.75) ? 2 : 1) : 1 + r.NextBelow(2);
            string s = "";
            for (int i = 0; i < n; i++) s += Syllable(ref r, tongue);
            if (s.Length < 3) s += Syllable(ref r, tongue);
            if (female && r.Chance(0.6))
            {
                int fem = (int)((tongue >> FemBit) & 3L);
                s += fem == 0 ? "a" : fem == 1 ? "e" : fem == 2 ? "i" : (r.Chance(0.5) ? "a" : "e");
            }
            return Cap(s);
        }

        /// <summary>A family's name in its first people's tongue.</summary>
        public static string Family(ulong seed, int householdKey, long tongue)
        {
            if (tongue == 0) return Family(seed, householdKey);
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)householdKey, 2));
            return Cap(Syllable(ref r, tongue) + Syllable(ref r, tongue));
        }

        /// <summary>A place's name in its founders' tongue: one or two syllables and one of its endings.</summary>
        public static string Place(ulong seed, int key, long tongue)
        {
            if (tongue == 0) return Place(seed, key);
            var r = new RandomSequence(KeyedRng.Hash(seed, RngStream.Names, (ulong)key, 3));
            string s = Syllable(ref r, tongue);
            if (r.Chance(0.5)) s += Syllable(ref r, tongue);
            s += PlaceEnds[Nth(tongue, EndBit, EndN, r.NextBelow(System.Math.Max(1, Count(tongue, EndBit, EndN))))];
            return Cap(s);
        }

        /// <summary>The endings a tongue gives its places (tests, tools).</summary>
        public static string[] EndsOf(long tongue)
        {
            var l = new System.Collections.Generic.List<string>();
            for (int i = 0; i < EndN; i++) if (((tongue >> (EndBit + i)) & 1L) != 0) l.Add(PlaceEnds[i]);
            return l.ToArray();
        }

        /// <summary>How many of the things a tongue is made of two tongues differ in (tools: how far apart two peoples speak).</summary>
        public static int Apart(long a, long b)
        {
            long x = (a ^ b) & ((1L << FemBit) - 1);
            int d = 0;
            for (int i = 0; i < FemBit; i++) if (((x >> i) & 1L) != 0) d++;
            if (((a >> FemBit) & 3L) != ((b >> FemBit) & 3L)) d++;
            if (((a >> LenBit) & 3L) != ((b >> LenBit) & 3L)) d++;
            return d;
        }
    }
}
