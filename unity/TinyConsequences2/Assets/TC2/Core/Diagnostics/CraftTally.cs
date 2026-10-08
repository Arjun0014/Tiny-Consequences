using System.Collections.Generic;
using TC2.Core.Simulation;

namespace TC2.Core.Diagnostics
{
    /// <summary>
    /// Tools (roles from practice, the survey after C4.2): the hours each person spends at a craft's work - 0 the smithy, 1 a plank boat, 2 a weaver's
    /// shed, 3 a kiln - from the moment the tally is turned on. Null in the game; never read by the simulation (a run with it is the same run).
    /// </summary>
    public static class CraftTally
    {
        public const int Smithy = 0, Plank = 1, Shed = 2, Kiln = 3, Kinds = 4;
        /// <summary>Per kind: person id -> hours.</summary>
        public static Dictionary<int, double>[] By;

        /// <summary>Per kind: (village id x 4096 + year) -> [hours, distinct hands] (the hands as a set of ids in `Hands`).</summary>
        public static Dictionary<long, double>[] VillageYear;
        static readonly Dictionary<long, HashSet<int>> _hands = new Dictionary<long, HashSet<int>>();

        public static void Add(int kind, int pid, double hours, World w = null, int sid = 0)
        {
            var by = By;
            if (by == null || pid == 0 || hours <= 0) return;
            by[kind].TryGetValue(pid, out double v);
            by[kind][pid] = v + hours;
            if (w == null || sid == 0 || VillageYear == null) return;
            long key = ((long)sid << 12) + (long)(w.Now / Foundation.SimTime.Year);
            VillageYear[kind].TryGetValue(key, out double h);
            VillageYear[kind][key] = h + hours;
            long hk = key * 8 + kind;
            if (!_hands.TryGetValue(hk, out var set)) { set = new HashSet<int>(); _hands[hk] = set; }
            set.Add(pid);
        }

        /// <summary>Distinct hands at a craft in a village-year.</summary>
        public static int HandsOf(int kind, long key) => _hands.TryGetValue(key * 8 + kind, out var set) ? set.Count : 0;
    }
}
