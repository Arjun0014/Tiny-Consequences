namespace TC2.Core.Foundation
{
    /// <summary>
    /// Purpose streams for keyed randomness. Never reorder or reuse values: saves and determinism
    /// depend on them. Append new streams at the end.
    /// </summary>
    public enum RngStream : uint
    {
        None = 0,
        Worldgen = 1,
        WorldgenPlates = 2,
        WorldgenRelief = 3,
        WorldgenClimate = 4,
        WorldgenSettle = 5,
        Names = 6,
        Traits = 7,
        Weather = 8,
        Lightning = 9,
        FireSpread = 10,
        Decide = 11,
        Demography = 12,
        Marriage = 13,
        Birth = 14,
        Death = 15,
        Travel = 16,
        Work = 17,
        Perception = 18,
        Harvest = 19,
        Tectonics = 20,
        Ecology = 21,
        Orbit = 22,
        Knowledge = 23,     // techniques found by practice (D2-062)
        Test = 1000,
    }

    /// <summary>
    /// Counter-based keyed randomness (D2-003): every draw is a pure function of
    /// (world seed, purpose stream, keys). Integer arithmetic only, identical on every runtime.
    /// SplitMix64's finaliser is the mixer (passes BigCrush as a counter-based generator).
    /// </summary>
    public static class KeyedRng
    {
        public static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public static ulong Hash(ulong seed, RngStream stream, ulong a)
        {
            ulong h = Mix(seed ^ Mix((ulong)stream * 0xD1B54A32D192ED03UL));
            return Mix(h ^ a);
        }

        public static ulong Hash(ulong seed, RngStream stream, ulong a, ulong b)
        {
            ulong h = Hash(seed, stream, a);
            return Mix(h ^ (b * 0x9E3779B97F4A7C15UL));
        }

        public static ulong Hash(ulong seed, RngStream stream, ulong a, ulong b, ulong c)
        {
            ulong h = Hash(seed, stream, a, b);
            return Mix(h ^ (c * 0xC2B2AE3D27D4EB4FUL));
        }

        /// <summary>Uniform double in [0, 1) from the top 53 bits.</summary>
        public static double Unit(ulong h) => (h >> 11) * (1.0 / 9007199254740992.0);

        public static double Unit(ulong seed, RngStream s, ulong a) => Unit(Hash(seed, s, a));
        public static double Unit(ulong seed, RngStream s, ulong a, ulong b) => Unit(Hash(seed, s, a, b));
        public static double Unit(ulong seed, RngStream s, ulong a, ulong b, ulong c) => Unit(Hash(seed, s, a, b, c));

        /// <summary>Uniform int in [0, n) (n > 0), unbiased enough for n far below 2^32 (multiply-shift).</summary>
        public static int Below(ulong h, int n)
        {
            if (n <= 1) return 0;
            ulong top = h >> 32;
            return (int)((top * (ulong)n) >> 32);
        }

        /// <summary>Standard normal by Box-Muller from two keyed uniforms (deterministic math).</summary>
        public static double Normal(ulong h1, ulong h2)
        {
            double u1 = Unit(h1);
            double u2 = Unit(h2);
            if (u1 < 1e-300) u1 = 1e-300;
            return DMath.Sqrt(-2.0 * DMath.Log(u1)) * DMath.Cos(DMath.TwoPi * u2);
        }
    }

    /// <summary>
    /// A short-lived sequence of draws inside one decision or one generation step, seeded from a keyed
    /// hash. Never stored in world state: persistent entities keep their own draw counter instead.
    /// </summary>
    public struct RandomSequence
    {
        ulong _state;

        public RandomSequence(ulong seed) { _state = seed; }

        public ulong NextULong() { _state += 0x9E3779B97F4A7C15UL; return KeyedRng.Mix(_state); }
        public double NextUnit() => KeyedRng.Unit(NextULong());
        public int NextBelow(int n) => KeyedRng.Below(NextULong(), n);
        public double Range(double lo, double hi) => lo + (hi - lo) * NextUnit();
        public bool Chance(double p) => NextUnit() < p;
        public double NextNormal() => KeyedRng.Normal(NextULong(), NextULong());
    }
}
