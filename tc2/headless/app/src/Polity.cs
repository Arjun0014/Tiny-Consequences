using System;
using System.Collections.Generic;
using System.IO;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// `polity --load SAVE [--near 6000]` (session 22, the governance slice measured first; reads only): the ties between villages a polity could be
        /// made of - each village's mother (who founded it, by a party or a split, and how far), its lord, the kin between its people and other villages'
        /// (living parents and children, siblings by a mother), whether its first household is close kin of another village's first household, the grain
        /// it owes others for relief (`DeedBook.Owed`), and the harms and help the deeds still weighed record between them; then the pairs within
        /// `near` m: mother and daughter against the others.
        /// </summary>
        public static int Polity(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            var P = w.Planet;
            double near = a.Double("near", 6000.0);
            var live = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) live.Add(sid);
            Func<int, string> name = sid => w.History.NameOf(EntityRef.Settlement(sid));
            // mothers: the founding event's object (the village whose people founded it), and whether a split sent them
            var mother = new Dictionary<int, int>(); var split = new HashSet<int>();
            foreach (int sid in live)
            {
                ref var s = ref w.Settlements.Get(sid);
                if (s.FoundedEvent == 0 || !w.History.TryGet(s.FoundedEvent, out var fe) || fe.Kind != HistoryKind.SettlementFounded) continue;
                if (fe.Object.Kind != EntityKind.Settlement || fe.Object.Id == 0 || fe.Object.Id == sid) continue;
                mother[sid] = fe.Object.Id;
                if (fe.Cause1 != 0 && w.History.TryGet(fe.Cause1, out var fc) && fc.Cause1 != 0 && w.History.TryGet(fc.Cause1, out var fs) && fs.Kind == HistoryKind.VillageSplit) split.Add(sid);
            }
            // kin between villages: living parent-child pairs and siblings (by their mother) living in two villages
            var kin = new Dictionary<long, int>();
            Func<int, int, long> key = (x, y) => x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
            var byMother = new Dictionary<int, List<int>>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (!p.Alive || p.Home == 0) continue;
                foreach (int par in new[] { p.Mother, p.Father })
                {
                    if (par == 0 || !w.People.Exists(par)) continue;
                    var q = w.People.Get(par);
                    if (!q.Alive || q.Home == 0 || q.Home == p.Home) continue;
                    long k = key(p.Home, q.Home); kin.TryGetValue(k, out int n); kin[k] = n + 1;
                }
                if (p.Mother != 0) { if (!byMother.TryGetValue(p.Mother, out var l)) byMother[p.Mother] = l = new List<int>(); l.Add(p.Home); }
            }
            foreach (var kv in byMother)
                for (int i = 0; i < kv.Value.Count; i++)
                    for (int j = i + 1; j < kv.Value.Count; j++)
                        if (kv.Value[i] != kv.Value[j]) { long k = key(kv.Value[i], kv.Value[j]); kin.TryGetValue(k, out int n); kin[k] = n + 1; }
            // first households' close kin: a member's parent, child or sibling a member of the other's
            Func<int, int, bool> headKin = (sa, sb) =>
            {
                int ha = w.Settlements.Get(sa).HeadHh, hb = w.Settlements.Get(sb).HeadHh;
                if (ha == 0 || hb == 0 || !w.Households.Exists(ha) || !w.Households.Exists(hb)) return false;
                var ma = w.Members(ha); var mb = w.Members(hb);
                for (int i = 0; i < ma.Count; i++)
                {
                    var m = w.People.Get(ma[i]);
                    for (int j = 0; j < mb.Count; j++)
                    {
                        var n = w.People.Get(mb[j]);
                        if (m.Mother == n.Id || m.Father == n.Id || n.Mother == m.Id || n.Father == m.Id || (m.Mother != 0 && m.Mother == n.Mother) || (m.Father != 0 && m.Father == n.Father)) return true;
                    }
                }
                return false;
            };
            // deeds still weighed, by the pair: harms done (takes, raids, blood, refusals) and help given (relief)
            var harm = new Dictionary<long, int>(); var help = new Dictionary<long, double>();
            for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id))
            {
                var d = w.Deeds.Get(id);
                if (d.ByS == 0 || d.ToS == 0 || d.ByS == d.ToS) continue;
                long k = key(d.ByS, d.ToS);
                if (DeedKind.Harm(d.Kind)) { harm.TryGetValue(k, out int n); harm[k] = n + 1; }
                else if (d.Kind == DeedKind.Relief) { help.TryGetValue(k, out double q); help[k] = q + d.Qty; }
            }
            Console.WriteLine("polity at " + SimTime.Format(w.Now) + ": " + live.Count + " villages");
            foreach (int sid in live)
            {
                ref var s = ref w.Settlements.Get(sid);
                string line = "  " + name(sid) + " (" + s.Population + ")";
                if (mother.TryGetValue(sid, out int m) && w.Settlements.Exists(m))
                {
                    bool mlive = w.Settlements.Get(m).Inhabited;
                    double km = Vec3d.ArcDistance(s.Anchor, w.Settlements.Get(m).Anchor, P.Radius) / 1000.0;
                    line += " founded " + (s.Founded / SimTime.Year) + " by " + (split.Contains(sid) ? "a split of " : "") + name(m) + (mlive ? "" : " (gone)") + " " + km.ToString("0.0") + " km";
                    if (mlive) line += kin.TryGetValue(key(sid, m), out int kn) ? ", kin " + kn : ", kin 0";
                    if (mlive && headKin(sid, m)) line += ", first households kin";
                }
                else line += " (first places)";
                if (s.Lord != 0) line += "; pays " + name(s.Lord);
                double owe = 0; int cred = 0;
                foreach (int o in live) { if (o == sid) continue; double q = w.DeedBook.Owed(sid, o); if (q > owe) { owe = q; cred = o; } }
                if (owe > 0) line += "; owes " + name(cred) + " " + owe.ToString("0") + " FU of relief";
                var hk = new List<string>();
                foreach (int o in live) if (o != sid && headKin(sid, o)) hk.Add(name(o));
                if (hk.Count > 0) line += "; first household kin of " + string.Join(", ", hk);
                Console.WriteLine(line);
            }
            // the pairs within reach: mother and daughter against the others
            int[] pairs = new int[2], kinPairs = new int[2], headPairs = new int[2], harmPairs = new int[2], helpPairs = new int[2];
            long[] kinSum = new long[2];
            for (int i = 0; i < live.Count; i++)
                for (int j = i + 1; j < live.Count; j++)
                {
                    int x = live[i], y = live[j];
                    if (Vec3d.ArcDistance(w.Settlements.Get(x).Anchor, w.Settlements.Get(y).Anchor, P.Radius) > near) continue;
                    bool md = (mother.TryGetValue(x, out int mx) && mx == y) || (mother.TryGetValue(y, out int my) && my == x);
                    int c = md ? 0 : 1;
                    long k = key(x, y);
                    pairs[c]++;
                    if (kin.TryGetValue(k, out int kn)) { kinPairs[c]++; kinSum[c] += kn; }
                    if (headKin(x, y)) headPairs[c]++;
                    if (harm.ContainsKey(k)) harmPairs[c]++;
                    if (help.ContainsKey(k)) helpPairs[c]++;
                }
            for (int c = 0; c < 2; c++)
                Console.WriteLine((c == 0 ? "mother and daughter" : "other pairs") + " within " + (near / 1000.0).ToString("0") + " km: " + pairs[c] + "; with kin between them " + kinPairs[c] +
                                  " (kin links " + kinSum[c] + ", " + (kinSum[c] / (double)Math.Max(1, pairs[c])).ToString("0.0") + " a pair), first households kin " + headPairs[c] +
                                  ", harms weighed now " + harmPairs[c] + ", relief weighed now " + helpPairs[c]);
            int daughters = mother.Count, splits = split.Count, lords = 0, tributaries = 0;
            foreach (int sid in live) { if (w.Settlements.Get(sid).Lord != 0) tributaries++; }
            foreach (int sid in live) { foreach (int o in live) if (w.Settlements.Get(o).Lord == sid) { lords++; break; } }
            Console.WriteLine("founded by others " + daughters + " of " + live.Count + " (by splits " + splits + "); lords " + lords + ", tributaries " + tributaries);
            return 0;
        }
    }
}
