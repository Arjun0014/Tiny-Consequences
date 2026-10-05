using TC2.Core.Agents;
using TC2.Core.Foundation;

namespace TC2.Core.Knowledge
{
    /// <summary>
    /// Techniques as knowledge carried by people (D2-062; 09). Each person holds a proficiency 0..1 for each technique; there is
    /// no tree to unlock. The young copy the best practitioner they can watch - the copy falls short by a technique's loss
    /// (alpha, its complexity) and scatters by a personal spread (beta, a Gumbel draw fixed for the person) as in Henrich's
    /// model (2004), so a complex technique holds only where enough learners watch a master and is lost in a small group; a
    /// technique is found by people practising the work it belongs to where nobody knows it (Arthur and Polak: invention as
    /// combination), and travels with whoever moves.
    /// </summary>
    public static class Tech
    {
        public const int Nets = 0, Loom = 1, Quern = 2, Count = 3;

        public struct Law
        {
            public string Name;          // for the history: "make fishing nets"
            public double Alpha, Beta;   // a copy's loss and its spread (Henrich): a village keeps the technique with ~exp(Alpha/Beta - 0.58) learners
            public double Discover;      // chance an hour of the related work finds it where nobody knows it
        }

        public static readonly Law[] Laws =
        {
            // knotting a net: simple, kept by a handful (~4 learners); found by those who fish
            new Law { Name = "make fishing nets", Alpha = 0.15, Beta = 0.08, Discover = 2e-5 },
            // the warp-weighted loom: kept by ~10 learners; found by those who spin and weave by hand
            new Law { Name = "weave on the loom", Alpha = 0.20, Beta = 0.07, Discover = 4e-7 },
            // dressing the stones of a rotary quern: complex, kept only by ~80 learners; found by grinders where rock is near
            new Law { Name = "dress the stones of a rotary quern", Alpha = 0.30, Beta = 0.06, Discover = 2e-7 },
        };

        /// <summary>Proficiency at which a person counts as a practitioner, and a village as knowing the technique.</summary>
        public const double Competent = 0.5;
        /// <summary>Below this a village that knew the technique has lost it.</summary>
        public const double Forgotten = 0.25;
        /// <summary>A day's copying: the young close ~1 % of the gap to their target a day (most of it in five years).</summary>
        public const double LearnRate = 0.01;
        /// <summary>What someone who first finds a technique knows of it.</summary>
        public const double FirstFound = 0.4;

        public static float Get(in Person p, int t) => t == Nets ? p.KNets : t == Loom ? p.KLoom : p.KQuern;

        public static void Set(ref Person p, int t, double v)
        {
            float f = (float)DMath.Clamp01(v);
            if (t == Nets) p.KNets = f; else if (t == Loom) p.KLoom = f; else p.KQuern = f;
        }

        /// <summary>A person's own scatter in copying a technique: a standard Gumbel draw fixed by their id (Henrich's epsilon).</summary>
        public static double Scatter(ulong seed, int pid, int t)
        {
            double u = KeyedRng.Unit(seed, RngStream.Traits, (ulong)pid, (ulong)(300 + t));
            u = DMath.Clamp(u, 1e-12, 1.0 - 1e-12);
            return -DMath.Log(-DMath.Log(u));
        }

        // ------------------------------------------------------------------ what knowing does
        /// <summary>A fisher's hours with nets they can make and mend: up to 1.6 times the catch.</summary>
        public static double NetsFactor(in Person p) => 1.0 + 0.6 * p.KNets;

        /// <summary>A maker's hours at the loom: up to 1.6 times the cloth.</summary>
        public static double LoomFactor(in Person p) => 1.0 + 0.6 * p.KLoom;

        /// <summary>Grinding on a rotary quern - a household has one when one of its people can dress the stones: up to 1.8 times the meal.</summary>
        public static double QuernFactor(double householdBest) => 1.0 + 0.8 * DMath.Clamp01(householdBest / 0.6);
    }
}
