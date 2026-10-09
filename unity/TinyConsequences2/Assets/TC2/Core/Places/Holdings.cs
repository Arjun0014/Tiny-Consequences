using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>A household's debt to another (C3.3, D2-126): grain it drew from the other's bin when its own, its village's and its kin's ran out.</summary>
    public struct Debt
    {
        public int Debtor, Creditor;
        public double Fu;
        public long Since;   // when the debt was first run up (repaid oldest first)

        public void Write(BinWriter w) { w.Int(Debtor); w.Int(Creditor); w.Double(Fu); w.Long(Since); }
        public static Debt Read(BinReader r) => new Debt { Debtor = r.Int(), Creditor = r.Int(), Fu = r.Double(), Since = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Debtor); h.Add(Creditor); h.Add(Fu); h.Add(Since); }
    }

    /// <summary>
    /// The household's grain and what households owe each other (C3.3, D2-126; `26` §3). A village's granary holds its households' grain in their
    /// own bins and the village's common grain beside them - the harvest of the village's fields, worked in common, is shared among its households by
    /// their mouths when it comes in (the commune's law), and what a household brings home beyond its larder (milk and meat of its own beasts, its
    /// catch) is its own. Its larder is filled from its own bin first; then from the common grain (what the village was given, paid or sold - fed to
    /// those who lack, unreckoned); then from what its close kin can spare beyond their own needs until the next harvest (a gift, unreckoned); then as
    /// a loan from the households it already owes and those with the most to spare (the debt recorded, repaid from its surplus after the next harvest).
    /// What is still wanting the village's ration takes from every bin by its share (in a shortage the store is everyone's). Losses nobody drew (rot,
    /// theft, sales, relief or tribute given, a visitor's meal) are borne by all who keep grain in the store by their shares; grain that came in from
    /// no household is the village's common grain. The physical store is the village's as before: this is whose grain it is, and what is owed.
    /// </summary>
    public sealed class Holdings
    {
        /// <summary>`--nohold`: the village's store as before (no bins, no debts) on the same binary.</summary>
        public static bool Off;
        /// <summary>(tests) `Architecture.Means` reads the village's granary as D2-114 did - the accounting then moves nothing in the world.</summary>
        public static bool MeansOff;
        /// <summary>Days of its own need a household keeps back beyond the next harvest before it lends, gives or repays.</summary>
        public const double Reserve = 6.0;

        World _w;
        internal readonly List<Debt> _debts = new List<Debt>();   // sorted by debtor, then creditor
        public void Bind(World w) { _w = w; }

        // ------------------------------------------------------------------ the day's context (derived, never saved)
        int _ctxSid; long _ctxAt = long.MinValue; int _ctxToHarvest;
        readonly Dictionary<int, double> _need = new Dictionary<int, double>();
        readonly Dictionary<int, List<int>> _kin = new Dictionary<int, List<int>>();
        readonly Dictionary<int, List<int>> _sibs = new Dictionary<int, List<int>>();
        bool _kinBuilt, _lendersBuilt;
        readonly List<int> _lenders = new List<int>();
        readonly List<(double spare, int hid)> _sort = new List<(double, int)>();

        void Begin(int sid, int toHarvest)
        {
            var w = _w;
            if (_ctxSid == sid && _ctxAt == w.Now && _ctxToHarvest == toHarvest) return;
            _ctxSid = sid; _ctxAt = w.Now; _ctxToHarvest = toHarvest;
            _need.Clear(); _kinBuilt = false; _lendersBuilt = false;
        }

        double NeedOf(int hid)
        {
            if (!_need.TryGetValue(hid, out double n)) { n = _w.SettlementSys.HouseholdNeed(hid); _need[hid] = n; }
            return n;
        }

        /// <summary>What a household can spare beyond its own needs until the next harvest and a few days more (0 for one on the move).</summary>
        double Spare(int hid)
        {
            ref var hh = ref _w.Households.Get(hid);
            if (hh.Party != 0) return 0;
            return DMath.Max(0.0, hh.Grain - NeedOf(hid) * (_ctxToHarvest + Reserve));
        }

        static void Link(Dictionary<int, List<int>> kin, int a, int b)
        {
            if (a == 0 || b == 0 || a == b) return;
            if (!kin.TryGetValue(a, out var la)) { la = new List<int>(); kin[a] = la; }
            if (!la.Contains(b)) la.Add(b);
            if (!kin.TryGetValue(b, out var lb)) { lb = new List<int>(); kin[b] = lb; }
            if (!lb.Contains(a)) lb.Add(a);
        }

        readonly List<List<int>> _pool = new List<List<int>>();
        int _pooled;
        List<int> Fresh() { if (_pooled == _pool.Count) _pool.Add(new List<int>()); var l = _pool[_pooled++]; l.Clear(); return l; }

        void LinkPooled(int a, int b)
        {
            if (a == 0 || b == 0 || a == b) return;
            if (!_kin.TryGetValue(a, out var la)) { la = Fresh(); _kin[a] = la; }
            if (!la.Contains(b)) la.Add(b);
            if (!_kin.TryGetValue(b, out var lb)) { lb = Fresh(); _kin[b] = lb; }
            if (!lb.Contains(a)) lb.Add(a);
        }

        /// <summary>Close kin among the village's households: its members' parents' and children's households and their brothers' and sisters'.</summary>
        void BuildKin(int sid)
        {
            if (_kinBuilt) return;
            _kinBuilt = true;
            var w = _w;
            _kin.Clear(); _sibs.Clear(); _pooled = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive || p.Household == 0) continue;
                for (int k = 0; k < 2; k++)
                {
                    int par = k == 0 ? p.Mother : p.Father;
                    if (par == 0 || !w.People.Exists(par)) continue;
                    ref var q = ref w.People.Get(par);
                    if (q.Alive && q.Home == sid) LinkPooled(p.Household, q.Household);
                    if (!_sibs.TryGetValue(par, out var l)) { l = Fresh(); _sibs[par] = l; }
                    if (!l.Contains(p.Household)) l.Add(p.Household);
                }
            }
            foreach (var kv in _sibs) for (int a = 0; a < kv.Value.Count; a++) for (int b = a + 1; b < kv.Value.Count; b++) LinkPooled(kv.Value[a], kv.Value[b]);
            foreach (var kv in _kin) kv.Value.Sort();
        }

        /// <summary>The village's households with grain to spare, the most first (ties to the lower id): whom the needy go to for a loan.</summary>
        void BuildLenders(int sid)
        {
            if (_lendersBuilt) return;
            _lendersBuilt = true;
            _sort.Clear(); _lenders.Clear();
            var hhs = _w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) { double sp = Spare(hhs[i]); if (sp > 1e-6) _sort.Add((sp, hhs[i])); }
            _sort.Sort((a, b) => a.spare != b.spare ? b.spare.CompareTo(a.spare) : a.hid.CompareTo(b.hid));
            for (int i = 0; i < _sort.Count; i++) _lenders.Add(_sort[i].hid);
        }

        /// <summary>The households of a village close kin to this one (pages and tools: built afresh, never the day's cache).</summary>
        public static List<int> KinOf(World w, int sid, int hid)
        {
            var kin = KinMap(w, sid);
            if (!kin.TryGetValue(hid, out var mine)) return new List<int>();
            mine.Sort();
            return mine;
        }

        /// <summary>Every household of a village and its close kin households there (built afresh; lists unsorted).</summary>
        public static Dictionary<int, List<int>> KinMap(World w, int sid)
        {
            var kin = new Dictionary<int, List<int>>(); var sibs = new Dictionary<int, List<int>>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive || p.Household == 0) continue;
                for (int k = 0; k < 2; k++)
                {
                    int par = k == 0 ? p.Mother : p.Father;
                    if (par == 0 || !w.People.Exists(par)) continue;
                    ref var q = ref w.People.Get(par);
                    if (q.Alive && q.Home == sid) Link(kin, p.Household, q.Household);
                    if (!sibs.TryGetValue(par, out var l)) { l = new List<int>(); sibs[par] = l; }
                    if (!l.Contains(p.Household)) l.Add(p.Household);
                }
            }
            foreach (var kv in sibs) for (int a = 0; a < kv.Value.Count; a++) for (int b = a + 1; b < kv.Value.Count; b++) Link(kin, kv.Value[a], kv.Value[b]);
            return kin;
        }

        // ------------------------------------------------------------------ the store's account
        /// <summary>
        /// The bins and the common grain against the granary: an outflow nobody drew is borne by all who keep grain in the store by their shares; an
        /// inflow nobody brought is the village's common grain. <paramref name="granary"/> is the store to reckon against (the store before a
        /// harvest that has already come in).
        /// </summary>
        public void Reconcile(int sid, double granary)
        {
            if (Off) return;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            var hhs = w.HouseholdsOf(sid);
            double held = s.Common;
            for (int i = 0; i < hhs.Count; i++) held += w.Households.Get(hhs[i]).Grain;
            if (granary < 0) granary = 0;
            if (held > granary + 1e-9)
            {
                double f = held > 0 ? granary / held : 0.0;
                s.Common *= f;
                for (int i = 0; i < hhs.Count; i++) { ref var hh = ref w.Households.Get(hhs[i]); hh.Grain *= f; }
            }
            else if (held < granary - 1e-9) s.Common += granary - held;
        }

        public void Reconcile(int sid) => Reconcile(sid, _w.Settlements.Get(sid).Granary);

        /// <summary>
        /// A village's harvest (already in its granary) shared among its households by their mouths - those setting out to a new home take no share
        /// - and then the year's debts repaid from what each can spare beyond a year's needs.
        /// </summary>
        public void Harvest(int sid, double yield, bool repay = true)
        {
            if (Off) return;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            Reconcile(sid, s.Granary - yield);
            Begin(sid, SimTime.DaysPerYear);
            var hhs = w.HouseholdsOf(sid);
            if (!LandOff && !ByMouths) { ByLandAndHands(sid, yield); if (repay) Repay(sid); return; }
            double total = 0;
            for (int i = 0; i < hhs.Count; i++) if (w.Households.Get(hhs[i]).Party == 0) total += NeedOf(hhs[i]);
            if (total <= 0) { s.Common += yield; return; }
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                if (hh.Party != 0) continue;
                hh.Grain += yield * NeedOf(hhs[i]) / total;
            }
            if (repay) Repay(sid);   // (the sheaves before the harvest's day share without settling the year's debts: D2-132)
        }

        /// <summary>Tools: the harvest shared by mouths, as before holdings counted (--bymouths; C3.6c off).</summary>
        public static bool ByMouths;

        /// <summary>The land's share of a harvest (C3.6c): the half-share of the metayer - half to the land, half to the hands that worked it.</summary>
        public const double LandShare = 0.5;

        /// <summary>
        /// A harvest shared by land and labour (C3.6c, D2-134): its land's half by the hectares each household holds of the village's arable (the
        /// unheld land's part the village's own grain), its labour's half by the hands each household gave the fields - the village still works its
        /// fields together, so every hand is paid alike: grown people a hand, youths of 12-15 six tenths, the old half, all by their health; the sick
        /// and the badly hurt abed none. Households setting out take no share.
        /// </summary>
        void ByLandAndHands(int sid, double yield)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            var hhs = w.HouseholdsOf(sid);
            double landPart = yield * LandShare, handsPart = yield - landPart;
            double arable = s.ArableHa, held = 0, hands = 0;
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                if (hh.Party != 0) continue;
                held += hh.Land; hands += Hands(hhs[i]);
            }
            if (arable < held) arable = held;
            if (arable > 0)
            {
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var hh = ref w.Households.Get(hhs[i]);
                    if (hh.Party != 0 || hh.Land <= 0) continue;
                    hh.Grain += landPart * hh.Land / arable;
                }
                s.Common += landPart * (arable - held) / arable;
            }
            else s.Common += landPart;
            if (hands > 0)
            {
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var hh = ref w.Households.Get(hhs[i]);
                    if (hh.Party != 0) continue;
                    double h = Hands(hhs[i]);
                    if (h > 0) hh.Grain += handsPart * h / hands;
                }
            }
            else s.Common += handsPart;
            if (ShareTally != null) Tally(sid, yield);
        }

        /// <summary>The hands a household gives the fields (C3.6c): as the village's working day weighs its people.</summary>
        public double Hands(int hid)
        {
            var w = _w;
            var mem = w.Members(hid);
            double h = 0;
            for (int i = 0; i < mem.Count; i++) h += HandOf(mem[i]);
            return h;
        }

        /// <summary>One person's hand in the fields (C3.6c): grown a hand, youths of 12-15 six tenths, the old half, by health; the sick and the
        /// badly hurt abed none.</summary>
        public double HandOf(int pid)
        {
            var w = _w;
            ref var p = ref w.People.Get(pid);
            if (!p.Alive) return 0.0;
            double age = p.AgeYears(w.Now);
            if (age < 12 || p.Health < 0.3 || p.SickPhase == TC2.Core.Health.Disease.Infectious || p.Hurt >= TC2.Core.Society.Conflict.AbedHurt) return 0.0;
            return (age > 60 ? 0.5 : age < 16 ? 0.6 : 1.0) * (0.5 + 0.5 * p.Health);
        }

        /// <summary>Tools (null in the game): the harvests' grain shared [0]; to households with no land [1], their mouths' share of the village's [2];
        /// to households holding at least their want [3], their mouths' share [4].</summary>
        public static double[] ShareTally;

        void Tally(int sid, double yield)
        {
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            double mouths = 0;
            for (int i = 0; i < hhs.Count; i++) if (w.Households.Get(hhs[i]).Party == 0) mouths += NeedOf(hhs[i]);
            if (mouths <= 0) return;
            ShareTally[0] += yield;
            double arable = DMath.Max(w.Settlements.Get(sid).ArableHa, 1e-9), hands = 0;
            for (int i = 0; i < hhs.Count; i++) if (w.Households.Get(hhs[i]).Party == 0) hands += Hands(hhs[i]);
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                if (hh.Party != 0) continue;
                double got = yield * LandShare * hh.Land / arable + (hands > 0 ? yield * (1.0 - LandShare) * Hands(hhs[i]) / hands : 0.0);
                double fair = yield * NeedOf(hhs[i]) / mouths;
                if (hh.Land < 0.05) { ShareTally[1] += got; ShareTally[2] += fair; }
                else if (hh.Land >= LandWant(hhs[i]) - 0.05) { ShareTally[3] += got; ShareTally[4] += fair; }
            }
        }

        /// <summary>Each debtor of the village repays what it can spare beyond a year's needs, its oldest debts first.</summary>
        void Repay(int sid)
        {
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                int first = FirstOf(hid);
                if (first < 0) continue;
                ref var hh = ref w.Households.Get(hid);
                if (hh.Party != 0) continue;
                double spare = hh.Grain - NeedOf(hid) * (SimTime.DaysPerYear + Reserve);
                while (spare > 1e-6)
                {
                    int j = OldestOf(hid);
                    if (j < 0) break;
                    var d = _debts[j];
                    if (!w.Households.Exists(d.Creditor) || w.Households.Get(d.Creditor).Dissolved != 0 || w.Households.Get(d.Creditor).Settlement != sid)
                    {
                        w.Metrics.DebtVoidFu += d.Fu; _debts.RemoveAt(j); continue;   // (the creditor is gone or lives elsewhere: no one to repay)
                    }
                    double pay = DMath.Min(spare, d.Fu);
                    hh.Grain -= pay; w.Households.Get(d.Creditor).Grain += pay; spare -= pay;
                    w.Metrics.RepaidFu += pay;
                    if (d.Fu - pay <= 1e-6) _debts.RemoveAt(j);
                    else { d.Fu -= pay; _debts[j] = d; }
                }
            }
        }

        /// <summary>A household's own produce brought home beyond its larder (milk and meat of its beasts, its catch) - already in the granary - is its own.</summary>
        public void Put(int hid, int sid, double fu)
        {
            if (Off || fu <= 0 || !_w.Households.Exists(hid)) return;
            ref var hh = ref _w.Households.Get(hid);
            if (hh.Settlement != sid || hh.Dissolved != 0 || hh.Party != 0) return;   // (else it is the village's: reckoned as common grain)
            hh.Grain += fu;
        }

        /// <summary>`--nohard`: sharing never contracts in hard times (C3.3a-b alone) on the same binary.</summary>
        public static bool HardOff;
        /// <summary>
        /// The village's ration below which sharing contracts to the household and its close kin (C3.3c; Dirks 1980: in a mild shortage sharing
        /// widens, in a deep and lasting one households keep what they have for themselves and their close kin, and those with nothing go short).
        /// </summary>
        public const double Contract = 0.7;

        /// <summary>
        /// Tools: hunger deaths by the household's own grain at the death (days of its food: [0] none, [1] under 5, [2] 5-20, [3] 20 or more), [4] in
        /// villages whose sharing had contracted, [5] in households in debt, [6] in households with no close kin in the village; in villages whose sharing
        /// had contracted, what the households with grain spare beyond their own needs at the village's ration to the harvest (and the common grain),
        /// against what all the households without grain need at that ration to the harvest: [7] enough for all of them, [8] a quarter to all, [9]
        /// under a quarter, [10] the sum of the ratio (each death at most 10), [11] deaths with under a day of the village's food in the store, [12] the
        /// sum of days to the harvest. Null in the game.
        /// </summary>
        public static double[] HungerTally;

        /// <summary>Tools: a hunger death in a household (counted by its own grain, the village's sharing, its debts and kin, and what others spare).</summary>
        public static void HungerDeath(World w, int sid, int hid)
        {
            var t = HungerTally;
            if (t == null || !w.Households.Exists(hid)) return;
            double need = w.SettlementSys.HouseholdNeed(hid), d = need > 0 ? w.Households.Get(hid).Grain / need : 0;
            t[d <= 0.01 ? 0 : d < 5 ? 1 : d < 20 ? 2 : 3]++;
            if (w.Holdings.Owes(hid, out _) > 0.05) t[5]++;
            if (KinOf(w, sid, hid).Count == 0) t[6]++;
            if (t.Length > 15) { ref var lh = ref w.Households.Get(hid); double lw = w.Holdings.LandWant(hid); t[lh.Land < 0.05 ? 13 : lh.Land < lw - 0.05 ? 14 : 15]++; }   // (by its land: C3.6e survey)
            if (!w.Settlements.Exists(sid) || !Hard(in w.Settlements.Get(sid))) return;
            t[4]++;
            ref var s = ref w.Settlements.Get(sid);
            int doy = SimTime.DayOfYear(w.Now + Planet.PlanetState.LocalOffset(s.Anchor));
            if (s.Anchor.Y < 0) doy = (doy + 24) % SimTime.DaysPerYear;
            int toHarvest = SettlementSystem.HarvestDay - doy; if (toHarvest <= 0) toHarvest += SimTime.DaysPerYear;
            // what the holders spare beyond their own needs at the village's ration to the harvest, against what every household without grain
            // needs at that ration to the harvest
            double horizon = s.Ration * (toHarvest + Reserve), spare = DMath.Max(0.0, s.Common), empty = 0, daily = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                double n = w.SettlementSys.HouseholdNeed(hl[i]), g = w.Households.Get(hl[i]).Grain;
                daily += n;
                if (g < n) empty += n * horizon;
                else spare += DMath.Max(0.0, g - n * horizon);
            }
            double z = empty > 0 ? spare / empty : 10.0;
            t[z >= 1 ? 7 : z >= 0.25 ? 8 : 9]++;
            t[10] += DMath.Min(10.0, z);
            if (s.Granary < daily) t[11]++;
            t[12] += toHarvest;
        }

        /// <summary>Whether a village's sharing has contracted: its store cannot feed it to the harvest at more than <see cref="Contract"/> of its meals.</summary>
        public static bool Hard(in Settlement s) => !Off && !HardOff && s.Ration < Contract;

        /// <summary>
        /// A household's larder to be filled from the store with up to <paramref name="fu"/> (not yet taken from the granary): what it gets. Its own grain,
        /// the common grain, its kin's spare (a gift), loans (debts); in ordinary times what is still wanting the village's ration takes from all by their
        /// shares (it gets all it asked); in hard times (`Hard`) kin share down to the village's ration and others lend only what they truly spare - no more.
        /// </summary>
        public double Provide(int sid, int hid, double fu, int toHarvest)
        {
            if (Off || fu <= 0) return fu;
            ref var s = ref _w.Settlements.Get(sid);
            if (!Hard(in s)) { Draw(sid, hid, fu, toHarvest); return fu; }
            double got = Draw(sid, hid, fu, toHarvest, true);
            _w.Metrics.UnsharedFu += fu - got;
            return got;
        }

        /// <summary>
        /// A household's larder filled from the store with <paramref name="fu"/> (already taken from the granary): its own grain, the common grain, its
        /// kin's spare (a gift), loans (debts); what is left the village's ration takes from all by their shares. With <paramref name="hard"/>, kin give
        /// down to the village's ration and nothing is taken from anyone's need: returns what it got.
        /// </summary>
        public double Draw(int sid, int hid, double fu, int toHarvest, bool hard = false)
        {
            if (Off || fu <= 0) return fu;
            double asked = fu;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            ref var hh = ref w.Households.Get(hid);
            double own = DMath.Min(fu, hh.Grain);
            hh.Grain -= own; fu -= own;
            if (fu <= 1e-9) return asked;
            double c = DMath.Min(fu, s.Common);
            s.Common -= c; fu -= c; w.Metrics.CommonFedFu += c;
            if (fu <= 1e-9) return asked;
            if (hh.Party != 0) return hard ? asked - fu : asked;   // (a household setting out borrows nothing)
            Begin(sid, toHarvest);
            // close kin give what they can spare (unreckoned) - in hard times down to the village's ration: the kin share the shortage
            BuildKin(sid);
            if (_kin.TryGetValue(hid, out var kin))
            {
                for (int k = 0; k < kin.Count && fu > 1e-9; k++)
                {
                    int g = kin[k];
                    if (!w.Households.Exists(g) || w.Households.Get(g).Settlement != sid) continue;
                    double t = DMath.Min(fu, hard ? Spare(g, s.Ration) : Spare(g));
                    if (t <= 0) continue;
                    w.Households.Get(g).Grain -= t; fu -= t; w.Metrics.KinFu += t;
                }
            }
            if (fu <= 1e-9) return asked;
            // in hard times the village's first household opens its grain to the hungry down to the village's ration - and they follow it (C10.2)
            if (hard)
            {
                int first = Society.Leaders.First(in s);
                if (first != 0 && first != hid && w.Households.Exists(first) && w.Households.Get(first).Settlement == sid && (kin == null || !kin.Contains(first)))
                {
                    double t = DMath.Min(fu, Spare(first, s.Ration));
                    if (t > 0)
                    {
                        w.Households.Get(first).Grain -= t; fu -= t; w.Metrics.HeadFedFu += t;
                        ref var me = ref w.Households.Get(hid); me.Patron = first; me.PatronAt = w.Now;
                    }
                }
                if (fu <= 1e-9) return asked;
            }
            // a loan: from those it owes already (they lent before), then the households with the most to spare
            for (int j = FirstOf(hid); j >= 0 && j < _debts.Count && _debts[j].Debtor == hid && fu > 1e-9; j++)
            {
                int cr = _debts[j].Creditor;
                if (!w.Households.Exists(cr) || w.Households.Get(cr).Settlement != sid) continue;
                double t = DMath.Min(fu, Spare(cr));
                if (t <= 0) continue;
                w.Households.Get(cr).Grain -= t; fu -= t;
                var d = _debts[j]; d.Fu += t; _debts[j] = d;
                w.Metrics.LoanFu += t;
            }
            if (fu <= 1e-9) return asked;
            BuildLenders(sid);
            for (int k = 0; k < _lenders.Count && fu > 1e-9; k++)
            {
                int l = _lenders[k];
                if (l == hid) continue;
                double t = DMath.Min(fu, Spare(l));
                if (t <= 0) continue;
                w.Households.Get(l).Grain -= t; fu -= t;
                Lend(hid, l, t);
                w.Metrics.LoanFu += t;
            }
            // (what is still wanting: the village's ration takes it from every bin by its share - borne at the next reckoning; in hard times nobody's need is taken)
            return hard ? asked - fu : asked;
        }

        /// <summary>What a household of kin can give in hard times: beyond its own needs at the village's ration until the harvest and six days.</summary>
        double Spare(int hid, double ration)
        {
            ref var hh = ref _w.Households.Get(hid);
            if (hh.Party != 0) return 0;
            return DMath.Max(0.0, hh.Grain - NeedOf(hid) * ration * (_ctxToHarvest + Reserve));
        }

        /// <summary>What one of a village's own households may take straight from its store now (an empty larder): all it needs in ordinary times; in
        /// hard times only its own grain and the common grain.</summary>
        public double Available(int sid, int hid)
        {
            if (!_w.Settlements.Exists(sid)) return double.MaxValue;
            ref var s = ref _w.Settlements.Get(sid);
            if (!Hard(in s) || !_w.Households.Exists(hid)) return double.MaxValue;
            ref var hh = ref _w.Households.Get(hid);
            if (hh.Settlement != sid) return double.MaxValue;   // (a visitor's meal is the village's hospitality)
            return hh.Grain + s.Common;
        }

        /// <summary>
        /// A meal or a cooking taken straight from the store by one of its own households (an empty larder; already taken from the granary): its
        /// own grain, then the common grain; what is left falls on all by their shares. A visitor's meal is the village's hospitality.
        /// </summary>
        public void Take(int sid, int hid, double fu)
        {
            if (Off || fu <= 0 || !_w.Households.Exists(hid)) return;
            var w = _w;
            ref var hh = ref w.Households.Get(hid);
            if (hh.Settlement != sid || hh.Dissolved != 0) return;
            double own = DMath.Min(fu, hh.Grain);
            hh.Grain -= own; fu -= own;
            if (fu <= 1e-9) return;
            ref var s = ref w.Settlements.Get(sid);
            double c = DMath.Min(fu, s.Common);
            s.Common -= c; w.Metrics.CommonFedFu += c;
        }

        /// <summary>A married child (or another who leaves a household for a new one in the same village) takes their share of its grain by their mouth.</summary>
        public void Split(int from, int to, double share)
        {
            if (Off || share <= 0 || !_w.Households.Exists(from) || !_w.Households.Exists(to)) return;
            ref var a = ref _w.Households.Get(from);
            ref var b = ref _w.Households.Get(to);
            if (a.Settlement != b.Settlement) return;   // (grain is not carried to another village's store)
            double g = a.Grain * DMath.Clamp01(share);
            a.Grain -= g; b.Grain += g;
        }

        /// <summary>The last of a household has died: its grain, its debts and what it is owed go to its heir; with no heir, its grain is the village's and its debts lapse.</summary>
        public void Inherit(int hid, int heir, int sid)
        {
            if (Off) return;
            var w = _w;
            ref var hh = ref w.Households.Get(hid);
            if (!LandOff && heir != 0 && w.Households.Exists(heir) && w.Households.Get(heir).Settlement == hh.Settlement) { w.Households.Get(heir).Land += hh.Land; if (LandTally != null) LandTally[3] += hh.Land; }   // (its land to the heir; none: the village's - C3.6b)
            hh.Land = 0;
            if (heir != 0 && w.Households.Exists(heir))
            {
                w.Households.Get(heir).Grain += hh.Grain;
                Transfer(hid, heir);
                if (w.Households.Get(heir).Settlement == hh.Settlement) Clients(hid, heir, hh.Settlement);
            }
            else
            {
                if (w.Settlements.Exists(sid)) w.Settlements.Get(sid).Common += hh.Grain;
                w.Metrics.DebtVoidFu += Drop(hid);
            }
            hh.Grain = 0;
        }

        /// <summary>
        /// A household's last member has left it for a new household (a marriage): its debts and claims go with them in the same village (its grain
        /// went by `Split`); a household of another village leaves its grain to that village and its debts lapse.
        /// </summary>
        public void Close(int old, int to)
        {
            if (!_w.Households.Exists(old)) return;
            var w = _w;
            ref var oh = ref w.Households.Get(old);
            bool same = w.Households.Exists(to) && w.Households.Get(to).Settlement == oh.Settlement;
            if (!LandOff && same) { w.Households.Get(to).Land += oh.Land; if (LandTally != null) LandTally[2] += oh.Land; }   // (the land goes with the last to leave it in the village: C3.6b)
            oh.Land = 0;
            if (Off) return;
            if (same) { Transfer(old, to); Clients(old, to, oh.Settlement); }
            else
            {
                if (w.Settlements.Exists(oh.Settlement)) w.Settlements.Get(oh.Settlement).Common += oh.Grain;
                w.Metrics.DebtVoidFu += Drop(old);
            }
            oh.Grain = 0;
        }

        /// <summary>
        /// Households setting out take the party's food (<paramref name="food"/>, already taken from the granary) from their own grain first, then
        /// the common grain; what they cannot carry of their own is left to the village; their debts and claims lapse.
        /// </summary>
        public void SetOut(int sid, List<int> leaving, double food)
        {
            var w = _w;
            for (int i = 0; i < leaving.Count; i++) { ref var lh = ref w.Households.Get(leaving[i]); if (LandTally != null) LandTally[4] += lh.Land; lh.Land = 0; }   // (their land is left to the village: C3.6b)
            if (Off) return;
            ref var s = ref w.Settlements.Get(sid);
            double bins = 0;
            for (int i = 0; i < leaving.Count; i++) bins += w.Households.Get(leaving[i]).Grain;
            double fromBins = DMath.Min(food, bins);
            double left = bins - fromBins;
            for (int i = 0; i < leaving.Count; i++) { ref var hh = ref w.Households.Get(leaving[i]); hh.Grain = 0; w.Metrics.DebtVoidFu += Drop(leaving[i]); }
            s.Common += left;
            double rest = food - fromBins;
            if (rest > 0) s.Common -= DMath.Min(rest, s.Common);   // (and the remainder falls on all by their shares)
        }

        /// <summary>A party's food reaches its new home (already in the granary there): theirs, by their mouths.</summary>
        public void Arrive(int dest, List<int> arrived, double food)
        {
            if (Off || food <= 0) return;
            var w = _w;
            ref var d = ref w.Settlements.Get(dest);
            Reconcile(dest, d.Granary - food);
            double total = 0;
            for (int i = 0; i < arrived.Count; i++) total += w.SettlementSys.HouseholdNeed(arrived[i]);
            if (total <= 0) { d.Common += food; return; }
            for (int i = 0; i < arrived.Count; i++) w.Households.Get(arrived[i]).Grain += food * w.SettlementSys.HouseholdNeed(arrived[i]) / total;
        }

        /// <summary>A new village's store (world generation): its households' by their mouths.</summary>
        public void Found(int sid)
        {
            if (Off) return;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            var hhs = w.HouseholdsOf(sid);
            double total = 0;
            for (int i = 0; i < hhs.Count; i++) total += w.SettlementSys.HouseholdNeed(hhs[i]);
            if (total <= 0) { s.Common = s.Granary; return; }
            for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Grain = s.Granary * w.SettlementSys.HouseholdNeed(hhs[i]) / total;
            s.Common = 0;
        }

        // ------------------------------------------------------------------ land (C3.6b, D2-133): each household's holding of its village's fields
        /// <summary>Tools: no holdings are kept (--noland).</summary>
        public static bool LandOff;

        /// <summary>Tools (null in the game): portions given at marriages [0] and their ha [1], the last-out's land [2] ha, inherited [3] ha, left to
        /// the village by those setting out [4] ha, allotted from the unheld [5] ha to [6] households, lost with the arable [7] ha.</summary>
        public static double[] LandTally;

        /// <summary>The fields a household's people would sow (the village's want a head - more where famine is remembered - times its members).</summary>
        public double LandWant(int hid)
        {
            var w = _w;
            ref var hh = ref w.Households.Get(hid);
            double memory = w.Settlements.Exists(hh.Settlement) ? w.Settlements.Get(hh.Settlement).Memory : 0.0;
            double head = w.Settlements.Exists(hh.Settlement) ? SettlementSystem.HaPerHead(w, in w.Settlements.Get(hh.Settlement)) : w.Config.FieldHaPerPerson;   // (by what the land bears: C3.6f)
            return w.Members(hid).Count * head * (1.0 + 0.5 * memory);
        }

        /// <summary>The village's arable not held by any of its households (ha): the land its people have taken in and not yet given out.</summary>
        public double Unheld(int sid)
        {
            var w = _w;
            double held = 0;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) held += w.Households.Get(hhs[i]).Land;
            return DMath.Max(0.0, w.Settlements.Get(sid).ArableHa - held);
        }

        /// <summary>A new village's fields (world generation): held by its founding households by their heads.</summary>
        public void LandFound(int sid)
        {
            if (LandOff) return;
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            double heads = 0;
            for (int i = 0; i < hhs.Count; i++) heads += w.Members(hhs[i]).Count;
            if (heads <= 0) return;
            double ha = w.Settlements.Get(sid).ArableHa;
            for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Land = ha * w.Members(hhs[i]).Count / heads;
        }

        readonly List<(double perHead, int hid)> _short = new List<(double, int)>();

        /// <summary>
        /// At sowing, after the village has claimed its fields (C3.6b): land it lost (released, gone to wood or water) is lost from every holding by
        /// the same share; land it holds unheld - taken in for its people's want, left by those who went or died without heirs - is given out to its
        /// households short of what they would sow, the least landed a head first.
        /// </summary>
        public void LandYear(int sid)
        {
            if (LandOff) return;
            var w = _w;
            var hhs = w.HouseholdsOf(sid);
            double held = 0;
            for (int i = 0; i < hhs.Count; i++) held += w.Households.Get(hhs[i]).Land;
            double arable = w.Settlements.Get(sid).ArableHa;
            if (held > arable + 1e-9)
            {
                double f = held > 0 ? arable / held : 0.0;
                for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Land *= f;
                if (LandTally != null) LandTally[7] += held - arable;
                return;
            }
            double free = arable - held;
            if (free <= 1e-9) return;
            _short.Clear();
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                int n = w.Members(hid).Count;
                if (hh.Party != 0 || n == 0) continue;
                if (hh.Land < LandWant(hid) - 1e-9) _short.Add((hh.Land / n, hid));
            }
            _short.Sort((a, b) => a.perHead != b.perHead ? a.perHead.CompareTo(b.perHead) : a.hid.CompareTo(b.hid));
            for (int i = 0; i < _short.Count && free > 1e-9; i++)
            {
                int hid = _short[i].hid;
                ref var hh = ref w.Households.Get(hid);
                double give = DMath.Min(free, LandWant(hid) - hh.Land);
                if (give <= 0) continue;
                hh.Land += give; free -= give;
                if (LandTally != null) { LandTally[5] += give; LandTally[6]++; }
            }
        }

        /// <summary>
        /// A child marrying out of a household of the village (already gone from its members) takes a portion of its land: what the land can spare
        /// beyond what its remaining people would sow, up to the child's own want - where the family's land is short, nothing (C3.6b).
        /// </summary>
        /// <summary>The land a child marrying out of a household would take (C3.6b's portion, reckoned before anyone moves; a household's last member
        /// takes all it holds): the niche a match can count on (C3.5b).</summary>
        public double PortionFor(int family)
        {
            if (LandOff || !_w.Households.Exists(family)) return 0.0;
            var w = _w;
            ref var a = ref w.Households.Get(family);
            if (!w.Settlements.Exists(a.Settlement) || w.Members(family).Count <= 1) return 0.0;   // (the last to leave takes it all: Close - reckoned as the whole)
            ref var s = ref w.Settlements.Get(a.Settlement);
            double head = SettlementSystem.HaPerHead(w, in s) * (1.0 + 0.5 * s.Memory);
            double spare = a.Land - (w.Members(family).Count - 1) * head;
            return DMath.Clamp(spare, 0.0, head);
        }

        public void Portion(int from, int to)
        {
            if (LandOff || !_w.Households.Exists(from) || !_w.Households.Exists(to)) return;
            var w = _w;
            ref var a = ref w.Households.Get(from);
            ref var b = ref w.Households.Get(to);
            if (a.Settlement != b.Settlement || w.Members(from).Count == 0) return;   // (the last to leave takes it all: Close)
            double spare = a.Land - LandWant(from);
            if (spare <= 1e-9) return;
            ref var s = ref w.Settlements.Get(a.Settlement);
            double give = DMath.Min(spare, SettlementSystem.HaPerHead(w, in s) * (1.0 + 0.5 * s.Memory));
            a.Land -= give; b.Land += give;
            if (LandTally != null) { LandTally[0]++; LandTally[1] += give; }
        }

        // ------------------------------------------------------------------ pages (read only)
        static string Days(double d) => d >= 9.5 ? d.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        static string Ha(double ha) => (ha >= 9.5 ? ha.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : ha.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)) + " ha";

        /// <summary>The settlement page's land line (C3.6b): how its fields are held - households with land and without, the largest holding, the unheld.</summary>
        public static string LandLine(World w, int sid)
        {
            if (LandOff) return "";
            var hl = w.HouseholdsOf(sid);
            int with = 0, without = 0, n = 0, most = 0; double held = 0, mostHa = 0;
            for (int i = 0; i < hl.Count; i++)
            {
                if (w.Members(hl[i]).Count == 0) continue;
                n++;
                double l = w.Households.Get(hl[i]).Land;
                held += l;
                if (l < 0.05) without++; else with++;
                if (l > mostHa) { mostHa = l; most = hl[i]; }
            }
            if (n == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("Land: ").Append(with).Append(with == 1 ? " household holds " : " households hold ").Append(Ha(held)).Append(" of its fields");
            if (most != 0 && with > 1) sb.Append(" (the most the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(most)))).Append(", ").Append(Ha(mostHa)).Append(")");
            if (without > 0) sb.Append("; ").Append(without).Append(without == 1 ? " household holds none" : " households hold none");
            double free = w.Holdings.Unheld(sid);
            if (free >= 0.05) sb.Append("; ").Append(Ha(free)).Append(" unheld");
            sb.Append(".\n");
            return sb.ToString();
        }

        /// <summary>The household page's line: its grain in the store in days of its members' food, what it owes and to whom, what it is owed.</summary>
        public static string Line(World w, int hid)
        {
            if (!w.Households.Exists(hid)) return "";
            ref var hh = ref w.Households.Get(hid);
            double need = w.SettlementSys.HouseholdNeed(hid);
            var sb = new System.Text.StringBuilder();
            if (!LandOff)
            {
                // its land (C3.6b): the hectares it holds against what its people would sow
                double want = w.Holdings.LandWant(hid);
                if (hh.Land < 0.05) sb.Append("It holds no land of the village's fields").Append(want > 0 ? " (its people would sow " + Ha(want) + ")" : "").Append(".\n");
                else sb.Append("It holds ").Append(Ha(hh.Land)).Append(" of the village's fields").Append(want > 0 ? (hh.Land >= want - 0.05 ? ", as much as its people would sow" : ", where its people would sow " + Ha(want)) : "").Append(".\n");
            }
            sb.Append("Its own grain in the village's granary: ").Append(Days(need > 0 ? hh.Grain / need : 0)).Append(" days' food for its people");
            if (hh.WaresDue > 0.05 || hh.PotsDue > 0.5) sb.Append("; of its makers' work in the store, not yet taken: ").Append(hh.WaresDue > 0.05 ? Days(hh.WaresDue) + " wares" : "").Append(hh.WaresDue > 0.05 && hh.PotsDue > 0.5 ? " and " : "").Append(hh.PotsDue > 0.5 ? hh.PotsDue.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " kg of ware" : "");
            sb.Append(".\n");
            var hs = w.Holdings;
            double owes = hs.Owes(hid, out int to);
            if (owes > 0.05)
            {
                int big = 0; double bigFu = 0; long since = long.MaxValue;
                for (int j = hs.FirstOf(hid); j >= 0 && j < hs._debts.Count && hs._debts[j].Debtor == hid; j++)
                {
                    var d = hs._debts[j];
                    if (d.Fu > bigFu) { bigFu = d.Fu; big = d.Creditor; }
                    if (d.Since < since) since = d.Since;
                }
                sb.Append("It owes ").Append(Days(owes)).Append(" days' grain").Append(need > 0 ? " (" + Days(owes / need) + " days of its food)" : "")
                  .Append(" - the most to the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(big)))).Append(to > 1 ? " and " + (to - 1) + (to == 2 ? " other household" : " other households") : "")
                  .Append(", since year ").Append(since / SimTime.Year).Append(".\n");
            }
            double owed = hs.Owed(hid, out int by);
            if (owed > 0.05) sb.Append("It is owed ").Append(Days(owed)).Append(" days' grain by ").Append(by).Append(by == 1 ? " household" : " households").Append(" it lent to when their own ran out.\n");
            return sb.ToString();
        }

        /// <summary>The settlement page's line: its households' grain and the village's own, the spread between them, its debts.</summary>
        public static string VillageLine(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var hl = w.HouseholdsOf(sid);
            double held = 0, least = double.MaxValue, most = 0; int n = 0;
            int mostHid = 0;
            for (int i = 0; i < hl.Count; i++)
            {
                double need = w.SettlementSys.HouseholdNeed(hl[i]);
                if (need <= 0) continue;
                double g = w.Households.Get(hl[i]).Grain;
                held += g; n++;
                double d = g / need;
                if (d < least) least = d;
                if (d > most) { most = d; mostHid = hl[i]; }
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("Grain: its households keep ").Append(held.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" days' grain in its store, the village ").Append(s.Common.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" of its own");
            if (n > 1) sb.Append("; from ").Append(Days(least)).Append(" to ").Append(Days(most)).Append(" days of their food a household (the most, the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(mostHid)))).Append(")");
            int debtors = 0, last = 0; double debt = 0; var by = new Dictionary<int, int>();
            var hs = w.Holdings;
            for (int j = 0; j < hs._debts.Count; j++)
            {
                var d = hs._debts[j];
                if (!w.Households.Exists(d.Debtor) || w.Households.Get(d.Debtor).Settlement != sid) continue;
                debt += d.Fu;
                if (d.Debtor != last) { debtors++; last = d.Debtor; }
                by.TryGetValue(d.Creditor, out int c); by[d.Creditor] = c + 1;
            }
            if (debtors > 0)
            {
                int top = 0, topN = 0;
                foreach (var kv in by) if (kv.Value > topN || (kv.Value == topN && kv.Key < top)) { topN = kv.Value; top = kv.Key; }
                sb.Append("; ").Append(debtors).Append(debtors == 1 ? " household owes " : " households owe ").Append(debt.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" days' grain to ").Append(by.Count).Append(by.Count == 1 ? " other" : " others")
                  .Append(topN > 1 ? " (" + topN + " of them to the " + TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(top))) + ")" : "");
            }
            sb.Append(".\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ makers paid in kind (C3.3b)
        /// <summary>`--nopay`: makers are not paid (C3.3a alone) on the same binary.</summary>
        public static bool PayOff;
        public const byte Wares = 0, Pots = 1;

        /// <summary>
        /// The worth of a unit of a maker's make in grain: the food of the working days it took a maker of this village (a ware at its best loom's
        /// pace; a kg of ware at its best potter's) - a day's bread for a day's work at the workshop.
        /// </summary>
        public static double Price(World w, in Settlement s, byte kind)
        {
            double perDay = kind == Wares ? Weaving.MakerDay(in s) : Pottery.KgPerHour * (1.0 + 0.6 * s.BestPottery) * TC2.Core.Agents.AgentSystem.LabourDay;
            return perDay > 1e-9 ? w.Config.FoodPerAdultDay / perDay : 0.0;
        }

        /// <summary>The food an hour of a smith's work for the village earns (a working day's for a labour day).</summary>
        public static double HourWage(World w) => w.Config.FoodPerAdultDay / TC2.Core.Agents.AgentSystem.LabourDay;

        readonly int[] _mkSid = new int[2];
        readonly long[] _mkAt = { long.MinValue, long.MinValue };
        readonly List<int>[] _makers = { new List<int>(), new List<int>() };

        static double DueOf(in Household hh, byte kind) => kind == Wares ? hh.WaresDue : hh.PotsDue;
        static void SetDue(ref Household hh, byte kind, double v) { if (kind == Wares) hh.WaresDue = v; else hh.PotsDue = v; }

        /// <summary>The village's households with make of this kind in its store not yet paid for (for the day).</summary>
        List<int> Makers(int sid, byte kind)
        {
            var w = _w;
            var l = _makers[kind];
            if (_mkSid[kind] == sid && _mkAt[kind] == w.Now) return l;
            _mkSid[kind] = sid; _mkAt[kind] = w.Now;
            l.Clear();
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (DueOf(in w.Households.Get(hl[i]), kind) > 1e-9) l.Add(hl[i]);
            return l;
        }

        /// <summary>
        /// How much of a kind a household can pay for from its own grain now (its own make in the store it may take freely): no credit - a
        /// household short of grain makes its own goods or cooks on the full wood rather than run into debt with the makers.
        /// </summary>
        public double CanBuy(int sid, int hid, byte kind)
        {
            if (Off || PayOff || !_w.Households.Exists(hid) || !_w.Settlements.Exists(sid)) return double.MaxValue;
            ref var hh = ref _w.Households.Get(hid);
            double price = Price(_w, in _w.Settlements.Get(sid), kind);
            if (price <= 0) return double.MaxValue;
            return DMath.Max(0.0, hh.Grain) / price + DueOf(in hh, kind);
        }

        /// <summary>A maker's hands put <paramref name="qty"/> into the village's store: theirs until someone takes it.</summary>
        public void Made(int hid, byte kind, double qty)
        {
            if (Off || PayOff || qty <= 0 || !_w.Households.Exists(hid)) return;
            ref var hh = ref _w.Households.Get(hid);
            if (hh.Dissolved != 0) return;
            SetDue(ref hh, kind, DueOf(in hh, kind) + qty);
            if (_mkSid[kind] == hh.Settlement && !_makers[kind].Contains(hid)) _mkAt[kind] = long.MinValue;   // (a new maker: the day's list again)
        }

        /// <summary>
        /// <paramref name="qty"/> of a kind left the village's store - taken by a household (<paramref name="payer"/>), else by the village for its
        /// own use or sold (0): its makers are paid its worth, by their shares of what is theirs in store; what was not made here (bought in) was the
        /// village's, and its worth is the village's common grain. A household pays from its own grain (it takes no more than it can pay: `CanBuy`);
        /// the village pays from its common grain, then by a levy on all who keep grain in its store by their shares.
        /// </summary>
        public void Out(int sid, int payer, byte kind, double qty)
        {
            if (Off || PayOff || qty <= 1e-12) return;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            double price = Price(w, in s, kind);
            if (price <= 0) return;
            var mk = Makers(sid, kind);
            double due = 0;
            for (int i = 0; i < mk.Count; i++) due += DueOf(in w.Households.Get(mk[i]), kind);
            double here = DMath.Min(qty, due);   // (made by its own makers; the rest was bought in)
            double ownPart = 0;   // (a maker taking from the store pays nothing for its own make)
            if (payer != 0 && here > 1e-12)
            {
                double pd = DueOf(in w.Households.Get(payer), kind);
                if (pd > 0) ownPart = here * pd / due;
            }
            double toMakers = (here - ownPart) * price, toCommon = (qty - here) * price;
            double forMakers, forCommon, owedMakers = 0;
            if (payer != 0)
            {
                ref var ph = ref w.Households.Get(payer);
                // the makers first, then the village for what it bought in; what it cannot pay it owes the makers (the village's part is forgone)
                forMakers = DMath.Min(ph.Grain, toMakers);
                ph.Grain -= forMakers;
                forCommon = DMath.Min(ph.Grain, toCommon);
                ph.Grain -= forCommon;
                owedMakers = toMakers - forMakers;
                w.Metrics.MakersPaidFu += forMakers;
            }
            else
            {
                double c = DMath.Min(s.Common, toMakers);
                s.Common -= c;
                double levy = Levy(sid, toMakers - c);
                forMakers = c + levy; forCommon = 0;
                w.Metrics.LevyFu += levy; w.Metrics.CommonPaidFu += c;
            }
            s.Common += forCommon;
            if (here <= 1e-12 || due <= 1e-12) return;
            double others = due - (payer != 0 ? DueOf(in w.Households.Get(payer), kind) : 0.0);   // (what is due to makers other than the payer)
            for (int i = 0; i < mk.Count; i++)
            {
                int m = mk[i];
                ref var mh = ref w.Households.Get(m);
                double d = DueOf(in mh, kind);
                SetDue(ref mh, kind, DMath.Max(0.0, d - here * d / due));
                if (m == payer || others <= 1e-12) continue;
                double share = d / others;
                mh.Grain += forMakers * share;
                w.Metrics.MakersEarnedFu += forMakers * share;
                if (owedMakers * share > 0.01) { Lend(payer, m, owedMakers * share); w.Metrics.MakersOwedFu += owedMakers * share; }   // (takers pay as they take - `CanBuy`; only a remainder by rounding could be owed)
            }
        }

        /// <summary>A smith's hours at the village's tools and arms (`hid` their household): paid by the village - its common grain, then a levy.</summary>
        public void Commission(int sid, int hid, double hours)
        {
            if (Off || PayOff || hours <= 0 || !_w.Households.Exists(hid)) return;
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (w.Households.Get(hid).Settlement != sid || w.Households.Get(hid).Party != 0) return;
            double fu = hours * HourWage(w);
            double c = DMath.Min(s.Common, fu);
            s.Common -= c;
            double levy = Levy(sid, fu - c, hid);
            w.Households.Get(hid).Grain += c + levy;
            w.Metrics.LevyFu += levy; w.Metrics.CommonPaidFu += c; w.Metrics.SmithsEarnedFu += c + levy;
        }

        /// <summary>A levy on every household's grain in the village's store by its share (the one paid excepted): what it raised.</summary>
        double Levy(int sid, double fu, int except = 0)
        {
            if (fu <= 1e-12) return 0;
            var w = _w;
            var hl = w.HouseholdsOf(sid);
            double held = 0;
            for (int i = 0; i < hl.Count; i++) if (hl[i] != except && w.Households.Get(hl[i]).Party == 0) held += w.Households.Get(hl[i]).Grain;
            if (held <= 1e-9) return 0;
            double f = DMath.Min(1.0, fu / held), got = 0;
            for (int i = 0; i < hl.Count; i++)
            {
                if (hl[i] == except) continue;
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Party != 0) continue;
                double t = hh.Grain * f;
                hh.Grain -= t; got += t;
            }
            return got;
        }

        // ------------------------------------------------------------------ debts
        static long Key(int d, int c) => ((long)d << 32) | (uint)c;

        int Find(int debtor, int creditor)
        {
            long k = Key(debtor, creditor);
            int lo = 0, hi = _debts.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                long m = Key(_debts[mid].Debtor, _debts[mid].Creditor);
                if (m == k) return mid;
                if (m < k) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }

        /// <summary>The first of a debtor's debts (by creditor), or -1.</summary>
        public int FirstOf(int debtor)
        {
            int i = Find(debtor, 0);   // (creditors are ids from 1: (debtor, 0) sorts before all of its debts)
            if (i < 0) i = ~i;
            return i < _debts.Count && _debts[i].Debtor == debtor ? i : -1;
        }

        int OldestOf(int debtor)
        {
            int best = -1;
            for (int j = FirstOf(debtor); j >= 0 && j < _debts.Count && _debts[j].Debtor == debtor; j++)
                if (best < 0 || _debts[j].Since < _debts[best].Since) best = j;
            return best;
        }

        void Lend(int debtor, int creditor, double fu)
        {
            { ref var dh = ref _w.Households.Get(debtor); dh.Patron = creditor; dh.PatronAt = _w.Now; }   // (the household it turned to: the tie outlives the debt - C10.2)
            int i = Find(debtor, creditor);
            if (i >= 0) { var d = _debts[i]; d.Fu += fu; _debts[i] = d; }
            else _debts.Insert(~i, new Debt { Debtor = debtor, Creditor = creditor, Fu = fu, Since = _w.Now });
        }

        /// <summary>Premise (tests, tools): a household owes another this much, run up at that time.</summary>
        public void Owe(int debtor, int creditor, double fu, long since)
        {
            int i = Find(debtor, creditor);
            if (i >= 0) { var d = _debts[i]; d.Fu += fu; if (since < d.Since) d.Since = since; _debts[i] = d; }
            else _debts.Insert(~i, new Debt { Debtor = debtor, Creditor = creditor, Fu = fu, Since = since });
        }

        /// <summary>A household's debts and claims become its heir's (a debt to itself is no more).</summary>
        void Transfer(int from, int to)
        {
            var moved = new List<Debt>();
            for (int i = _debts.Count - 1; i >= 0; i--)
            {
                var d = _debts[i];
                if (d.Debtor != from && d.Creditor != from) continue;
                _debts.RemoveAt(i);
                if (d.Debtor == from) d.Debtor = to;
                if (d.Creditor == from) d.Creditor = to;
                if (d.Debtor != d.Creditor) moved.Add(d);
            }
            for (int i = moved.Count - 1; i >= 0; i--)
            {
                var d = moved[i];
                int j = Find(d.Debtor, d.Creditor);
                if (j >= 0) { var e = _debts[j]; e.Fu += d.Fu; if (d.Since < e.Since) e.Since = d.Since; _debts[j] = e; }
                else _debts.Insert(~j, d);
            }
        }

        /// <summary>
        /// The households a household fed or lent to follow the household that took it in (an heir's, the one its last member went to - the old gone to
        /// live with a child: C3.6e): the grain, the claims and the people are there now, and so is the tie (C10.2; the years it lasts count from the
        /// feeding). A household that followed the one it went into follows no one. (D2-138; `--nostanding`: the ties lapse with the household.)
        /// </summary>
        void Clients(int from, int to, int sid)
        {
            if (Society.Leaders.StandingOff) return;
            var w = _w;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); if (h.Patron == from) h.Patron = hl[i] == to ? 0 : to; }
        }

        /// <summary>Every debt a household owes or is owed lapses; the grain they stood for.</summary>
        double Drop(int hid)
        {
            double fu = 0;
            for (int i = _debts.Count - 1; i >= 0; i--)
            {
                if (_debts[i].Debtor != hid && _debts[i].Creditor != hid) continue;
                fu += _debts[i].Fu; _debts.RemoveAt(i);
            }
            return fu;
        }

        public int DebtCount => _debts.Count;
        public Debt DebtAt(int i) => _debts[i];

        /// <summary>What a household owes in all, and to how many.</summary>
        public double Owes(int hid, out int to)
        {
            double fu = 0; to = 0;
            for (int j = FirstOf(hid); j >= 0 && j < _debts.Count && _debts[j].Debtor == hid; j++) { fu += _debts[j].Fu; to++; }
            return fu;
        }

        /// <summary>What a household is owed in all, and by how many.</summary>
        public double Owed(int hid, out int by)
        {
            double fu = 0; by = 0;
            for (int j = 0; j < _debts.Count; j++) if (_debts[j].Creditor == hid) { fu += _debts[j].Fu; by++; }
            return fu;
        }

        public void Write(BinWriter w) { w.Int(_debts.Count); for (int i = 0; i < _debts.Count; i++) _debts[i].Write(w); }
        public void Read(BinReader r) { _debts.Clear(); int n = r.Int(); for (int i = 0; i < n; i++) _debts.Add(Debt.Read(r)); }
        public void Hash(ref StateHasher h) { if (_debts.Count == 0) return; h.Add(_debts.Count); for (int i = 0; i < _debts.Count; i++) _debts[i].Hash(ref h); }
    }
}
