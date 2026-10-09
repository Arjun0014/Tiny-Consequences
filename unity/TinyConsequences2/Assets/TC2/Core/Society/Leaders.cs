using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Society
{
    /// <summary>
    /// Standing and the village's first household (C10.2, D2-127; `10` §4, §6.6). A household's following is the village's households bound to it:
    /// its close kin, those that owe it grain (C3.3), and those it fed in hard times or lent to lately (the tie outlives the debt) - Sahlins' big man, whose gifts and loans make followers,
    /// and the elders' standing of kin. The household with the largest following is the village's first; the eldest grown member of it is first among
    /// its people (the headman). It keeps the place while its following stays near the largest (an incumbent is not unseated by a hair), and the place
    /// passes within the household at a death while the household still holds its following - so standing that is inherited (claims pass to heirs,
    /// C3.3) makes lines of headmen. What the first household does: in hard times it opens its grain to the village's hungry down to the village's
    /// ration, before anyone's loans (`Holdings.Draw`) - and those it fed follow it. Nothing here is assigned: who leads is read from who owes whom,
    /// who is kin to whom and who fed whom. Tribute paid to a lord village is its first household's grain (the lord's own store), not its common
    /// grain: what a lordship takes makes its first household rich.
    /// </summary>
    public static class Leaders
    {
        /// <summary>`--nohead`: no first household and no headman (C3.3 alone) on the same binary.</summary>
        public static bool Off;
        /// <summary>`--nosplit`: a contested succession never splits a village (C10.2 alone) on the same binary.</summary>
        public static bool SplitOff;
        /// <summary>The least following - households, the rival's own counted - and people that can go off to make a village of their own.</summary>
        public const int SplitHouseholds = 6, SplitPeople = 20;
        /// <summary>Years a faction that would not follow looks for land before it gives up and stays.</summary>
        public const int SplitYears = 3;
        /// <summary>`--nostanding`: a headman gone to live in another household of the village leaves the place vacant (contested), and a household's
        /// clients do not follow the household that took it in (before D2-138).</summary>
        public static bool StandingOff;
        /// <summary>`--notribhead`: tribute to a lord village is its common grain, not its first household's (C10.2 headmen alone).</summary>
        public static bool TributeOff;
        /// <summary>An incumbent household keeps the first place while its following is at least this share of the largest.</summary>
        public const double Hold = 0.5;
        /// <summary>Years a household fed by another in hard times follows it.</summary>
        public const int FollowYears = 8;
        /// <summary>The age from which a member may be first among the village's people.</summary>
        public const double FromAge = 18.0;

        // ------------------------------------------------------------------ the following (read once a year and at a vacancy)
        static readonly Dictionary<int, HashSet<int>> _by = new Dictionary<int, HashSet<int>>();
        static readonly Dictionary<int, int> _kinN = new Dictionary<int, int>(), _debtN = new Dictionary<int, int>(), _fedN = new Dictionary<int, int>();

        static HashSet<int> Set(int hid) { if (!_by.TryGetValue(hid, out var s)) { s = new HashSet<int>(); _by[hid] = s; } return s; }
        static void Bump(Dictionary<int, int> d, int hid) { d.TryGetValue(hid, out int n); d[hid] = n + 1; }

        /// <summary>Each household's following in a village (and its parts: kin, debtors, fed), into the static tables.</summary>
        static void Followings(World w, int sid)
        {
            _by.Clear(); _kinN.Clear(); _debtN.Clear(); _fedN.Clear();
            var kin = Holdings.KinMap(w, sid);
            foreach (var kv in kin) { var s = Set(kv.Key); for (int i = 0; i < kv.Value.Count; i++) { s.Add(kv.Value[i]); Bump(_kinN, kv.Key); } }   // (each part counted on its own; the following is their union)
            var hs = w.Holdings;
            for (int i = 0; i < hs.DebtCount; i++)
            {
                var d = hs.DebtAt(i);
                if (!w.Households.Exists(d.Debtor) || !w.Households.Exists(d.Creditor)) continue;
                if (w.Households.Get(d.Debtor).Settlement != sid || w.Households.Get(d.Creditor).Settlement != sid) continue;
                Set(d.Creditor).Add(d.Debtor); Bump(_debtN, d.Creditor);
            }
            long since = w.Now - FollowYears * SimTime.Year;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Patron == 0 || hh.PatronAt < since || !w.Households.Exists(hh.Patron) || w.Households.Get(hh.Patron).Settlement != sid) continue;
                Set(hh.Patron).Add(hl[i]); Bump(_fedN, hh.Patron);
            }
        }

        static int FollowingOf(int hid) => _by.TryGetValue(hid, out var s) ? s.Count : 0;

        /// <summary>Tools and pages: every household's following in a village (a fresh copy; reads only).</summary>
        public static Dictionary<int, HashSet<int>> All(World w, int sid)
        {
            Followings(w, sid);
            var d = new Dictionary<int, HashSet<int>>();
            foreach (var kv in _by) d[kv.Key] = new HashSet<int>(kv.Value);
            return d;
        }

        /// <summary>A household's following now (pages and tools): households of kin, owing it, fed by it.</summary>
        public static int Following(World w, int sid, int hid, out int kin, out int owing, out int fed)
        {
            Followings(w, sid);
            _kinN.TryGetValue(hid, out kin); _debtN.TryGetValue(hid, out owing); _fedN.TryGetValue(hid, out fed);
            return FollowingOf(hid);
        }

        /// <summary>The member who would be first among the village's people for a household: its eldest grown, able member living there (0 none).</summary>
        public static int Eldest(World w, int sid, int hid)
        {
            var mem = w.Members(hid);
            int best = 0; long born = long.MaxValue;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var p = ref w.People.Get(mem[i]);
                if (!p.Alive || p.Home != sid || p.AgeYears(w.Now) < FromAge || p.Health < 0.3f) continue;
                if (p.Born < born || (p.Born == born && p.Id < best)) { born = p.Born; best = p.Id; }
            }
            return best;
        }

        /// <summary>
        /// The village's first household and its headman, weighed (yearly after the harvest, and when the place falls vacant): the household with
        /// the largest following - ties to the one with more grain, then the lower id - unless the incumbent's following is still `Hold` of it.
        /// </summary>
        public static void Weigh(World w, int sid, long cause = 0, bool vacancy = false)
        {
            if (Off) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            Followings(w, sid);
            var hl = w.HouseholdsOf(sid);
            int best = 0, bestF = -1; double bestG = -1;
            for (int i = 0; i < hl.Count; i++)
            {
                int h = hl[i];
                ref var hh = ref w.Households.Get(h);
                if (hh.Party != 0 || Eldest(w, sid, h) == 0) continue;
                int f = FollowingOf(h);
                if (f > bestF || (f == bestF && hh.Grain > bestG)) { best = h; bestF = f; bestG = hh.Grain; }
            }
            if (best == 0) { s.Head = 0; s.HeadHh = 0; s.HeadSince = w.Now; return; }
            int inc = s.HeadHh;
            // the first household gone into another with its headman (the old to live with a child: C3.6e) - its people, land, grain, claims and
            // followers are there: that household is the incumbent now (D2-138)
            if (!StandingOff && inc != 0 && w.Households.Exists(inc) && w.Households.Get(inc).Dissolved != 0 && s.Head != 0 && w.People.Exists(s.Head))
            {
                ref var hp = ref w.People.Get(s.Head);
                if (hp.Alive && hp.Home == sid && hp.Household != inc && w.Households.Exists(hp.Household)) inc = hp.Household;
            }
            bool keep = inc != 0 && w.Households.Exists(inc) && w.Households.Get(inc).Settlement == sid && w.Households.Get(inc).Dissolved == 0
                        && w.Households.Get(inc).Party == 0 && Eldest(w, sid, inc) != 0 && FollowingOf(inc) >= Hold * bestF;
            int house = keep ? inc : best;
            int head = s.Head;
            bool headStays = head != 0 && w.People.Exists(head) && w.People.Get(head).Alive && w.People.Get(head).Home == sid && w.People.Get(head).Household == house;
            if (headStays && house == s.HeadHh) return;
            // the same person first, their household now the one theirs went into (the old gone to live with a child: C3.6e) - nobody rose (D2-138)
            if (headStays && !vacancy && !StandingOff) { s.HeadHh = house; return; }
            int prev = head, prevHouse = s.HeadHh;
            int now = headStays ? head : Eldest(w, sid, house);
            if (now == 0) return;
            byte how = prevHouse == 0 ? (byte)0 : house == prevHouse ? (byte)1 : (byte)2;   // the first; within the household; another household rose
            if (cause == 0 && prev != 0 && w.People.Exists(prev) && !w.People.Get(prev).Alive) cause = w.People.Get(prev).DeathEvent;
            s.Head = now; s.HeadHh = house; s.HeadSince = w.Now;
            s.HeadEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.HeadmanRose, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(now), Object = EntityRef.Household(house),
                Other = prev != 0 ? EntityRef.Person(prev) : default, Cause1 = cause, Rel1 = cause != 0 ? CauseRel.CausedBy : CauseRel.None,
                Magnitude = FollowingOf(house), ArgA = FollowingOf(house), ArgB = how, RootKind = RootKind.People,
            });
            w.Metrics.Headmen++;
            if (how == 1) w.Metrics.HeadsWithin++; else if (how == 2) w.Metrics.HeadsRisen++;
            if (vacancy) Contest(w, sid, house, cause, 0);
        }

        static readonly HashSet<int> _bound = new HashSet<int>();
        static readonly List<int> _faction = new List<int>();

        /// <summary>
        /// A succession contested (C10.3; Sahlins: conflict at a big man's passing; villages fission past ~130-200 people unless their people are bound
        /// together - Alberti 2014, Chagnon): at a vacancy, the household with the largest following among those not bound to the new first household -
        /// its own and its followers', none of them bound to the first - will not follow it when that following is at least `Hold` of the first's and a
        /// village's worth (`SplitHouseholds`, `SplitPeople`): it leads them away to found a village at the best place the village knows. The tables of
        /// `Followings` are the ones this weighing read.
        /// </summary>
        static void Contest(World w, int sid, int first, long cause, int only)
        {
            if (SplitOff || first == 0) return;
            _bound.Clear(); _bound.Add(first);
            if (_by.TryGetValue(first, out var fs)) foreach (var h in fs) _bound.Add(h);
            var hl = w.HouseholdsOf(sid);
            int rival = 0, best = 0;
            for (int i = 0; i < hl.Count; i++)
            {
                int h = hl[i];
                if (only != 0 && h != only) continue;   // (a faction already looking for land: weighed again)
                if (_bound.Contains(h) || w.Households.Get(h).Party != 0 || !_by.TryGetValue(h, out var f)) continue;
                int n = 1;
                foreach (var x in f) if (!_bound.Contains(x) && w.Households.Get(x).Party == 0) n++;
                if (n > best || (n == best && h < rival)) { best = n; rival = h; }
            }
            var t = ContestTally;
            if (t != null) { t[0]++; t[7] += best; t[8] += FollowingOf(first) + 1; }
            if (rival == 0 || best < SplitHouseholds) { if (t != null) t[1]++; return; }
            if (best < Hold * (FollowingOf(first) + 1)) { if (t != null) t[2]++; return; }
            _faction.Clear(); _faction.Add(rival);
            int people = w.Members(rival).Count;
            var rf = new List<int>(_by[rival]); rf.Sort();
            for (int i = 0; i < rf.Count; i++)
            {
                int x = rf[i];
                if (_bound.Contains(x) || !w.Households.Exists(x) || w.Households.Get(x).Party != 0 || w.Households.Get(x).Settlement != sid) continue;
                _faction.Add(x); people += w.Members(x).Count;
            }
            if (people < SplitPeople) { if (t != null) t[3]++; return; }
            ref var s = ref w.Settlements.Get(sid);
            if (!w.Migration.KnowsSite(sid))
            {
                // no place known: they look for land (the village sends scouts for them) and go when it is found
                if (s.SplitHh == 0) { s.SplitHh = rival; s.SplitCause = cause; s.SplitSince = w.Now; if (t != null) t[4]++; }
                return;
            }
            s.SplitHh = 0;
            int lead = Eldest(w, sid, rival);
            long ev = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.VillageSplit, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = lead != 0 ? EntityRef.Person(lead) : EntityRef.Household(rival),
                Object = EntityRef.Household(rival), Other = s.Head != 0 ? EntityRef.Person(s.Head) : default, Cause1 = cause, Rel1 = cause != 0 ? CauseRel.CausedBy : CauseRel.None,
                Magnitude = _faction.Count, ArgA = _faction.Count, ArgB = people, RootKind = RootKind.People,
            });
            if (w.Migration.Split(sid, _faction, ev)) { w.Metrics.Splits++; w.Metrics.SplitHouseholds += _faction.Count; if (t != null) t[5]++; }
        }

        /// <summary>A faction looking for land: gone, grown into the first, or tired of looking - it stays; a place known - weighed again, and it goes.</summary>
        static void Pending(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            int r = s.SplitHh;
            var t = ContestTally;
            if (SplitOff || !w.Households.Exists(r) || w.Households.Get(r).Dissolved != 0 || w.Households.Get(r).Settlement != sid || r == s.HeadHh)
            { s.SplitHh = 0; if (t != null) t[9]++; return; }
            if (w.Now - s.SplitSince > SplitYears * SimTime.Year) { s.SplitHh = 0; if (t != null) t[6]++; return; }
            if (!w.Migration.KnowsSite(sid)) return;
            Followings(w, sid);
            long cause = s.SplitCause;
            s.SplitHh = 0;
            if (t != null) t[10]++;
            Contest(w, sid, s.HeadHh, cause, r);
        }

        /// <summary>Tools: contested successions weighed - [0] weighings (vacancies and factions weighed again), [1] no unbound rival of a village's
        /// worth, [2] its following under half the first's, [3] too few people, [4] no place known: they look for land, [5] splits, [7] sum of rivals'
        /// followings, [8] of the firsts'; a faction looking for land: [6] gave up after `SplitYears`, [9] gone (its household dissolved, moved or
        /// risen to first), [10] a place found and weighed again (of these [0]-[5] count again). Null in the game.</summary>
        public static double[] ContestTally;

        /// <summary>The village's day: a headman dead or gone leaves the place vacant - weighed at once; a village without one weighs its first.</summary>
        public static void Day(World w, int sid)
        {
            if (Off) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            if (s.SplitHh != 0) Pending(w, sid);
            if (s.Head == 0) { if (w.HouseholdsOf(sid).Count > 0 && (s.HeadSince == 0 || w.Now - s.HeadSince >= SimTime.Year)) Weigh(w, sid); return; }   // (none could lead: weighed again a year on)
            if (!w.People.Exists(s.Head)) { Weigh(w, sid); return; }
            ref var p = ref w.People.Get(s.Head);
            if (!p.Alive || p.Home != sid || (StandingOff && p.Household != s.HeadHh)) Weigh(w, sid, p.Alive ? 0 : p.DeathEvent, true);   // dead or gone: the place is vacant
            else if (p.Household != s.HeadHh) Weigh(w, sid);   // living on in another household of the village (gone to live with a child, a marriage): weighed again, nothing to contest (D2-138)
        }

        /// <summary>The village's first household (0 none) - the one whose grain opens to the hungry in hard times (`Holdings.Draw`).</summary>
        public static int First(in Settlement s) => Off ? 0 : s.HeadHh;
    }
}
