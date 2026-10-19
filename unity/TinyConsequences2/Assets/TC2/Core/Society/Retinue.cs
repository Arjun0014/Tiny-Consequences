using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Society
{
    /// <summary>
    /// Sworn men (C10.7b, D2-165; `23` §3): a village's first household (C10.2) whose own grain runs years beyond its needs - a lord's by the tribute
    /// its tributaries carry in (C10.6), a great landholder's by its harvests - keeps men sworn to it. At each harvest of its village it weighs how many
    /// its grain beyond its own next year and `KeepYears` more could feed for those years, swears the willing young men of its village - the landless
    /// and land-short first (C3.6: those with least to lose at home), then the boldest - releases those it can no longer keep (the most landed and the
    /// least bold first), and gives each a year's food from its bin into his household's (one store: nothing is carried). The men keep their own
    /// homes; they practise arms every day at the village's ground (one is laid out for them: `Structures`), go with every band their village raises
    /// (`Conflict`) and, trained, weigh in its might by their lasting strength (C10.7a). The ring-giver's hall in small: Ine's laws (c. 694) count up
    /// to seven men thieves, seven to thirty-five a band; a lord's following ate at its board - its household men, landed and landless (`10` §12j).
    /// Released (history tells each): their lord's household no longer the first, its grain short, past forty, gone to live in another village.
    /// </summary>
    public static class Retinue
    {
        /// <summary>`--oldsworn`: nobody is sworn (C10.7a alone) on the same binary.</summary>
        public static bool Off;
        /// <summary>Years of its own need a first household keeps beyond its next year before it feeds men; and the years it would feed them for.</summary>
        public const double KeepYears = 3.0;
        /// <summary>Who may swear: men of this age.</summary>
        public const double FromAge = 18.0, ToAge = 40.0;
        /// <summary>Tools (`--sworntally`): [0] men sworn, released [1] the grain short, [2] their household no longer first, [3] past forty, [4] gone to
        /// another village; [5] FU given, [6] man-years fed, [7] practice hours of sworn men, [8] bands their village raised with sworn men in them,
        /// [9] sworn men in those bands, [10] village-harvests weighed with men kept, [11] the most sworn to one household; null in the game.</summary>
        public static double[] Tally;
        public const int TallyN = 12;

        /// <summary>A man's year of food.</summary>
        public static double ManYear(World w) => w.Config.FoodPerAdultDay * SimTime.DaysPerYear;

        /// <summary>Whether a person is sworn to a household of the village they live in (the one that feeds and keeps them).</summary>
        public static bool SwornHere(World w, in Person p) =>
            !Off && p.Sworn != 0 && w.Households.Exists(p.Sworn) && w.Households.Get(p.Sworn).Settlement == p.Home && w.Households.Get(p.Sworn).Dissolved == 0;

        /// <summary>The men of a village sworn to its households now (pages, planning; counted afresh).</summary>
        public static int Count(World w, int sid)
        {
            if (Off) return 0;
            var res = w.Residents(sid);
            int n = 0;
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (q.Alive && SwornHere(w, in q)) n++; }
            return n;
        }

        /// <summary>Whether men of a village are sworn to its households now (planning: a ground for them), and the swearing of one of them (the cause).</summary>
        public static bool Keeps(World w, int sid, out long cause)
        {
            cause = 0;
            if (Off) return false;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive || !SwornHere(w, in q)) continue;
                var evs = w.History.EventsOf(EntityRef.Person(q.Id));
                for (int k = evs == null ? -1 : evs.Count - 1; k >= 0; k--)
                    if (w.History.TryGet(evs[k], out var e) && e.Kind == HistoryKind.Sworn) { cause = e.Id; break; }
                return true;
            }
            return false;
        }

        /// <summary>The men a first household could keep: its grain beyond its own next year and `KeepYears` more, at a man's year for each of those years.</summary>
        public static int Keepable(World w, int hid)
        {
            if (hid == 0 || !w.Households.Exists(hid)) return 0;
            ref var hh = ref w.Households.Get(hid);
            if (hh.Party != 0 || hh.Dissolved != 0) return 0;
            double year = SimTime.DaysPerYear;
            double spare = hh.Grain - w.SettlementSys.HouseholdNeed(hid) * year * (1.0 + KeepYears);
            if (spare <= 0) return 0;
            return (int)(spare / (KeepYears * ManYear(w)));
        }

        static readonly List<int> _sworn = new List<int>();
        static readonly List<(double land, int bold, int pid)> _rank = new List<(double, int, int)>();

        /// <summary>
        /// A village's harvest (both resolutions; after its households' shares are in their bins and its tribute is paid): the men sworn to its first
        /// household are weighed again - those no longer kept released, the willing sworn up to what its grain keeps - and each given a year's food.
        /// `harvest` is the harvest's event (the cause).
        /// </summary>
        public static void Harvest(World w, int sid, long harvest)
        {
            if (Off || Leaders.Off) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            long now = w.Now;
            int first = Leaders.First(in s);
            // the men of the village sworn to a household: those whose household is not the first now, past forty, or living elsewhere are released
            _sworn.Clear();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (!q.Alive || q.Sworn == 0) continue;
                int to = q.Sworn;
                bool here = w.Households.Exists(to) && w.Households.Get(to).Settlement == sid && w.Households.Get(to).Dissolved == 0;
                byte why = !here ? (byte)4 : to != first ? (byte)2 : q.AgeYears(now) >= ToAge ? (byte)3 : (byte)0;
                if (why != 0) { Release(w, ref q, why, harvest); continue; }
                _sworn.Add(res[i]);
            }
            if (first == 0) return;
            int keep = Keepable(w, first);
            if (Tally != null && (keep > 0 || _sworn.Count > 0)) Tally[10] += 1;
            // too many to keep: the most landed and the least bold go home first
            if (_sworn.Count > keep)
            {
                Rank(w, _sworn);
                for (int k = _rank.Count - 1; k >= keep; k--) Release(w, ref w.People.Get(_rank[k].pid), 1, harvest);
                _sworn.Clear();
                for (int k = 0; k < keep && k < _rank.Count; k++) _sworn.Add(_rank[k].pid);
            }
            // room for more: the willing young men of the village, the landless and land-short first, then the boldest
            if (_sworn.Count < keep)
            {
                var cand = new List<int>();
                for (int i = 0; i < res.Count; i++)
                {
                    ref var q = ref w.People.Get(res[i]);
                    if (Willing(w, in q, sid, now) && q.Sworn == 0) cand.Add(res[i]);
                }
                Rank(w, cand);
                long tribute = LastTribute(w, sid, now);
                for (int k = 0; k < _rank.Count && _sworn.Count < keep; k++)
                {
                    ref var q = ref w.People.Get(_rank[k].pid);
                    q.Sworn = first;
                    _sworn.Add(q.Id);
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.Sworn, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(q.Id), Object = EntityRef.Household(first),
                        Cause1 = harvest, Rel1 = CauseRel.InResponseTo, Cause2 = tribute, Rel2 = tribute != 0 ? CauseRel.CausedBy : CauseRel.None,
                        Magnitude = (float)ManYear(w), ArgA = _sworn.Count, RootKind = RootKind.People,
                    });
                    if (Tally != null) Tally[0] += 1;
                }
            }
            // a year's food for each, from the first household's bin into his household's (the same store)
            double year = ManYear(w);
            for (int i = 0; i < _sworn.Count; i++)
            {
                ref var q = ref w.People.Get(_sworn[i]);
                ref var fh = ref w.Households.Get(first);
                double fu = DMath.Min(year, DMath.Max(0.0, fh.Grain));
                if (fu <= 0) break;
                if (q.Household != first && w.Households.Exists(q.Household))
                {
                    fh.Grain -= fu;
                    w.Households.Get(q.Household).Grain += fu;
                }
                if (Tally != null) { Tally[5] += fu; Tally[6] += fu / year; }
            }
            if (Tally != null && _sworn.Count > Tally[11]) Tally[11] = _sworn.Count;
        }

        /// <summary>One who may swear: a man of 18-40 of the village, fit, bold (as those who choose to practise: `Training.BoldFrom`), of one of its
        /// households (not one on the move), not the one first among its people.</summary>
        static bool Willing(World w, in Person q, int sid, long now)
        {
            if (!q.Alive || q.SexKind != Sex.Male || q.Home != sid || q.Household == 0 || q.Bold < Training.BoldFrom) return false;
            double age = q.AgeYears(now);
            if (age < FromAge || age >= ToAge || !Conflict.Fit(in q, now)) return false;
            if (w.Settlements.Get(sid).Head == q.Id || !w.Households.Exists(q.Household) || w.Households.Get(q.Household).Party != 0) return false;
            return true;
        }

        /// <summary>Ranks men into `_rank`: the least land a hand in their household first (C3.6: those with least to lose at home), then the boldest, then
        /// the lower id.</summary>
        static void Rank(World w, List<int> men)
        {
            _rank.Clear();
            for (int i = 0; i < men.Count; i++)
            {
                ref var q = ref w.People.Get(men[i]);
                double land = 0;
                if (w.Households.Exists(q.Household))
                {
                    double hands = w.Holdings.Hands(q.Household);
                    land = hands > 0 ? w.Households.Get(q.Household).Land / hands : w.Households.Get(q.Household).Land;
                }
                _rank.Add((land, q.Bold, men[i]));
            }
            _rank.Sort((a, b) => a.land != b.land ? a.land.CompareTo(b.land) : a.bold != b.bold ? b.bold.CompareTo(a.bold) : a.pid.CompareTo(b.pid));
        }

        /// <summary>A sworn man released (history; `why` 1 the grain short, 2 the household no longer first, 3 past forty, 4 gone to another village).</summary>
        static void Release(World w, ref Person q, byte why, long cause)
        {
            int to = q.Sworn;
            q.Sworn = 0;
            if (Tally != null && why >= 1 && why <= 4) Tally[why] += 1;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.SwornReleased, Time = w.Now, Cell = w.Settlements.Exists(q.Home) ? w.Settlements.Get(q.Home).Cell : 0, Settlement = q.Home,
                Subject = EntityRef.Person(q.Id), Object = to != 0 ? EntityRef.Household(to) : default, Cause1 = cause, Rel1 = CauseRel.InResponseTo, ArgA = why,
                RootKind = RootKind.People,
            });
        }

        /// <summary>The last tribute a village was paid within a year (its event), or 0 - what feeds its first household's men (the cause).</summary>
        static long LastTribute(World w, int sid, long now)
        {
            var evs = w.History.EventsOf(EntityRef.Settlement(sid));
            if (evs == null) return 0;
            for (int i = evs.Count - 1; i >= 0; i--)
            {
                if (!w.History.TryGet(evs[i], out var e)) continue;
                if (e.Time < now - SimTime.Year) return 0;
                if (e.Kind == HistoryKind.TributePaid && e.Object.Id == sid) return e.Id;
            }
            return 0;
        }

        /// <summary>The village's ground for a sworn man: its working training ground (0 none).</summary>
        public static int Ground(World w, in Person p) => SwornHere(w, in p) ? Training.Ground(w, p.Home) : 0;
    }
}
