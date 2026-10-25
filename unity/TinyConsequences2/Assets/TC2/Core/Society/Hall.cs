using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Society
{
    /// <summary>
    /// The lord's household (C10.11): a village's first household whose grain runs years beyond its own need and its sworn men's keeps landless households
    /// as its dependants - a year of their need from its grain at each harvest, for as many as that grain keeps for the same years its sworn men are kept for.
    /// Its own village's landless first, the neediest first; a lord's then from its realm - landless households of its tributaries within a day's walk with a
    /// way on foot come to the seat to live in its keep. They are let go when the grain no longer keeps them (the least needy first), when another household
    /// becomes the village's first, or when they hold land of their own. Kept households do not weigh leaving: they are fed. The record's lord's household
    /// (the Anglo-Saxon hall's people, the Carolingian familia, the Irish clients) fed from the lord's surplus - seats of power drew the landless.
    /// </summary>
    public static class Hall
    {
        /// <summary>`--oldhall`: no first household keeps dependants (before C10.11), on the same binary.</summary>
        public static bool Off;
        /// <summary>Tools (`--halltally`; null in the game): [0] households taken in at home, [1] from the realm (summoned to the seat), [2] let go - the grain
        /// short, [3] another first, [4] land of their own, [5] FU given, [6] household-years fed, [7] the most kept by one household, [8] village-harvests
        /// weighed with dependants kept or keepable, [9] summoned households that could not go (no way on foot).</summary>
        public static double[] Tally;
        public const int TallyN = 10;

        static readonly List<int> _kept = new List<int>();
        static readonly List<(double days, int hid, int sid)> _cand = new List<(double, int, int)>();

        /// <summary>A household's year of need (FU).</summary>
        static double YearOf(World w, int hid) => w.SettlementSys.HouseholdNeed(hid) * SimTime.DaysPerYear;

        /// <summary>Days of its own need a household's grain and larder hold.</summary>
        static double Days(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            double need = w.SettlementSys.HouseholdNeed(hid);
            return need > 0 ? (hh.Grain + hh.Larder) / need : 0.0;
        }

        /// <summary>Whether a household holds no land (C3.6b) and is free to be kept: settled, not on the move, not a first household.</summary>
        static bool Landless(World w, int hid, int first)
        {
            if (hid == first || !w.Households.Exists(hid)) return false;
            ref var hh = ref w.Households.Get(hid);
            return hh.Dissolved == 0 && hh.Party == 0 && hh.Keeper == 0 && hh.Land < 0.05 && w.Members(hid).Count > 0;
        }

        /// <summary>
        /// A village's harvest (both resolutions; after its sworn men are fed: `Retinue.Harvest`): its first household's dependants weighed again - those no longer
        /// kept let go, more taken in up to what its grain keeps - and each given a year of its need. `harvest` is the harvest's event (the cause).
        /// </summary>
        public static void Harvest(World w, int sid, long harvest)
        {
            if (Off || Leaders.Off) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            long now = w.Now;
            int first = Leaders.First(in s);
            // the households this village's first households keep or kept: let go those kept by a household that is no longer its first, or holding land now
            _kept.Clear();
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                if (hh.Keeper == 0 || hh.Dissolved != 0) continue;
                if (hh.Keeper != first) { LetGo(w, hhs[i], 2, harvest); continue; }
                if (hh.Land >= 0.05) { LetGo(w, hhs[i], 3, harvest); continue; }
                _kept.Add(hhs[i]);
            }
            // (and those on their way here, or still at home in a tributary, summoned: kept all the same)
            if (first != 0)
                for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid))
                {
                    ref var hh = ref w.Households.Get(hid);
                    if (hh.Keeper == first && hh.Dissolved == 0 && hh.Settlement != sid) _kept.Add(hid);
                }
            if (first == 0 || !w.Households.Exists(first)) { for (int i = 0; i < _kept.Count; i++) LetGo(w, _kept[i], 2, harvest); return; }
            // what its grain keeps beyond its own next year and `Retinue.KeepYears` more and its sworn men's years (a household's year of need each, for those years)
            ref var fh = ref w.Households.Get(first);
            double years = Retinue.KeepYears;
            double spare = fh.Grain - YearOf(w, first) * (1.0 + years) - Retinue.Count(w, sid) * Retinue.ManYear(w) * years;
            double keepFu = 0;
            for (int i = 0; i < _kept.Count; i++) keepFu += YearOf(w, _kept[i]) * years;
            if (Tally != null && (_kept.Count > 0 || spare > 0)) Tally[8] += 1;
            // too many to keep: the least needy let go first
            if (keepFu > spare && _kept.Count > 0)
            {
                _cand.Clear();
                for (int i = 0; i < _kept.Count; i++) _cand.Add((Days(w, _kept[i]), _kept[i], 0));
                _cand.Sort((a, b) => a.days != b.days ? b.days.CompareTo(a.days) : a.hid.CompareTo(b.hid));
                for (int i = 0; i < _cand.Count && keepFu > spare; i++)
                {
                    keepFu -= YearOf(w, _cand[i].hid) * years;
                    _kept.Remove(_cand[i].hid);
                    LetGo(w, _cand[i].hid, 1, harvest);
                }
            }
            // room for more: its own village's landless, the neediest first; a lord's then its realm's, within a day's walk with a way on foot
            if (spare - keepFu > 0)
            {
                _cand.Clear();
                for (int i = 0; i < hhs.Count; i++) if (Landless(w, hhs[i], first)) _cand.Add((Days(w, hhs[i]), hhs[i], sid));
                if (w.Conflict.PolitySize(sid) > 0)
                    for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                    {
                        if (o == sid || !w.Settlements.Get(o).Inhabited || !InRealm(w, o, sid)) continue;
                        if (Vec3d.ArcDistance(s.Anchor, w.Settlements.Get(o).Anchor, w.Planet.Radius) > w.Config.MarriageReach) continue;
                        var oh = w.HouseholdsOf(o);
                        for (int i = 0; i < oh.Count; i++) if (Landless(w, oh[i], first)) _cand.Add((Days(w, oh[i]) + 0.5, oh[i], o));   // (home first, at a like need)
                    }
                _cand.Sort((a, b) => a.days != b.days ? a.days.CompareTo(b.days) : a.hid.CompareTo(b.hid));
                for (int i = 0; i < _cand.Count; i++)
                {
                    double cost = YearOf(w, _cand[i].hid) * years;
                    if (keepFu + cost > spare) break;
                    int hid = _cand[i].hid, from = _cand[i].sid;
                    if (from != sid)
                    {
                        long ev0 = TakeIn(w, hid, first, sid, from, harvest);
                        if (!w.Migration.Summon(hid, sid, ev0)) { w.Households.Get(hid).Keeper = 0; w.Households.Get(hid).KeptSince = 0; if (Tally != null) Tally[9] += 1; continue; }
                        if (Tally != null) Tally[1] += 1;
                    }
                    else { TakeIn(w, hid, first, sid, sid, harvest); if (Tally != null) Tally[0] += 1; }
                    keepFu += cost;
                    _kept.Add(hid);
                }
            }
            if (Tally != null && _kept.Count > Tally[7]) Tally[7] = _kept.Count;
            // a year of its need for each kept household that lives here now (one on its way is fed at the next harvest), from the first household's grain into its own
            for (int i = 0; i < _kept.Count; i++)
            {
                int hid = _kept[i];
                ref var kh = ref w.Households.Get(hid);
                if (kh.Settlement != sid || kh.Party != 0) continue;
                ref var fh2 = ref w.Households.Get(first);
                double fu = DMath.Min(YearOf(w, hid), DMath.Max(0.0, fh2.Grain));
                if (fu <= 0) break;
                fh2.Grain -= fu;
                kh.Grain += fu;
                if (Tally != null) { Tally[5] += fu; Tally[6] += 1; }
            }
        }

        /// <summary>Whether a village pays a lord, or pays one who pays it, up its lords' tiers.</summary>
        static bool InRealm(World w, int o, int lord)
        {
            for (int l = w.Conflict.LordOf(o), guard = 0; l != 0 && guard < 16; l = w.Conflict.LordOf(l), guard++) if (l == lord) return true;
            return false;
        }

        static long TakeIn(World w, int hid, int first, int sid, int from, long harvest)
        {
            ref var hh = ref w.Households.Get(hid);
            hh.Keeper = first; hh.KeptSince = w.Now;
            return w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TakenIn, Time = w.Now, Cell = w.Settlements.Get(sid).Cell, Settlement = sid, Subject = EntityRef.Household(hid), Object = EntityRef.Household(first),
                Other = from != sid ? EntityRef.Settlement(from) : default, Cause1 = harvest, Rel1 = CauseRel.InResponseTo, ArgA = from != sid ? 1 : 0, RootKind = RootKind.People,
            });
        }

        /// <summary>A kept household let go (history; `why` 1 the grain short, 2 another first among the people, 3 land of its own).</summary>
        static void LetGo(World w, int hid, byte why, long cause)
        {
            ref var hh = ref w.Households.Get(hid);
            int keeper = hh.Keeper;
            hh.Keeper = 0; hh.KeptSince = 0;
            if (Tally != null) Tally[1 + why] += 1;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.LetGo, Time = w.Now, Cell = w.Settlements.Exists(hh.Settlement) ? w.Settlements.Get(hh.Settlement).Cell : 0, Settlement = hh.Settlement,
                Subject = EntityRef.Household(hid), Object = keeper != 0 ? EntityRef.Household(keeper) : default, Cause1 = cause, Rel1 = CauseRel.InResponseTo, ArgA = why,
                RootKind = RootKind.People,
            });
        }

        /// <summary>The households a village's first household keeps now (pages).</summary>
        public static int Count(World w, int sid)
        {
            if (Off) return 0;
            ref var s = ref w.Settlements.Get(sid);
            int first = Leaders.First(in s);
            if (first == 0) return 0;
            int n = 0;
            for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid)) { ref var hh = ref w.Households.Get(hid); if (hh.Keeper == first && hh.Dissolved == 0) n++; }
            return n;
        }
    }
}
