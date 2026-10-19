using System;
using System.Collections.Generic;
using System.IO;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// `lordships --load SAVE [--every 50] [--bonds]` (session 23, the long-run lordship check; reads only): every bond of tribute the world's
        /// history records (`TributeAgreed`, `TributeEnded` are historical: never compacted) - who paid whom, for bread or for peace, from when to when
        /// and how it ended (the lord gone, grown no stronger, the tributary abandoned, or still paid) - and the lordships every `every` years: villages,
        /// lords, tributaries, the largest lordship and its depth, what was agreed and ended in the period, raids and the lords' bands. Whether lordships
        /// hold, fragment, grow or snowball is read from these, never from one year.
        /// </summary>
        public static int Lordships(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            int every = a.Int("every", 50);
            bool listBonds = a.Has("bonds");
            Func<int, string> name = sid => w.History.NameOf(EntityRef.Settlement(sid));
            // villages: founded and abandoned times (the store keeps abandoned villages; events fill in any it does not)
            var founded = new Dictionary<int, long>(); var abandoned = new Dictionary<int, long>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                founded[sid] = s.Founded;
                if (s.Abandoned != 0) abandoned[sid] = s.Abandoned;
            }
            var ev = new List<HistoryEvent>();
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                switch (e.Kind)
                {
                    case HistoryKind.TributeAgreed: case HistoryKind.TributeEnded: case HistoryKind.Raid: case HistoryKind.BandRaised: case HistoryKind.CallAnswered:
                    case HistoryKind.VillageSplit: case HistoryKind.SettlementFounded: case HistoryKind.SettlementAbandoned: case HistoryKind.Famine: case HistoryKind.ReliefSent:
                        ev.Add(e); break;
                }
                if (e.Kind == HistoryKind.SettlementFounded && !founded.ContainsKey(e.Settlement)) founded[e.Settlement] = e.Time;
                if (e.Kind == HistoryKind.SettlementAbandoned && !abandoned.ContainsKey(e.Settlement)) abandoned[e.Settlement] = e.Time;
            }
            ev.Sort((x, y) => x.Time != y.Time ? x.Time.CompareTo(y.Time) : x.Id.CompareTo(y.Id));
            Func<int, long, bool> live = (sid, t) => founded.TryGetValue(sid, out long f) && f <= t && (!abandoned.TryGetValue(sid, out long ab) || ab > t);

            // the bonds, each from its agreement to its end
            var bonds = new List<Bond>();
            var open = new Dictionary<int, int>();   // payer -> index of its open bond
            var lord = new Dictionary<int, int>();
            long now = w.Now, Y = SimTime.Year;
            int periods = (int)((now + every * Y - 1) / (every * Y));
            var rows = new List<string>();
            int pAgreeBread = 0, pAgreePeace = 0, pEndGone = 0, pEndWeak = 0, pRaids = 0, pRaidsLord = 0, pCalls = 0, pCallMen = 0, pSplits = 0, pFound = 0, pAband = 0, pFamines = 0;
            double pRelief = 0;
            int k = 0;
            // per lord over the whole history: years held, the most paying at once
            var lordYears = new Dictionary<int, double>(); var lordMost = new Dictionary<int, int>();
            long lastT = 0;
            Action<long> integrate = t =>
            {
                if (t <= lastT) return;
                var counts = new Dictionary<int, int>();
                foreach (var kv in lord) if (live(kv.Key, lastT) && live(kv.Value, lastT)) { counts.TryGetValue(kv.Value, out int n); counts[kv.Value] = n + 1; }
                foreach (var kv in counts) { lordYears.TryGetValue(kv.Key, out double yv); lordYears[kv.Key] = yv + (t - lastT) / (double)Y; }
                lastT = t;
            };
            rows.Add("  year villages lords paying largest(lord: villages, depth)        agreed bread/peace  ended gone/weaker  raids (lords' for tributaries)  calls (men)  splits founded abandoned famines relief");
            for (int pi = 1; pi <= periods; pi++)
            {
                long T = Math.Min(now, (long)pi * every * Y);
                for (; k < ev.Count && ev[k].Time <= T; k++)
                {
                    var e = ev[k];
                    integrate(e.Time);
                    switch (e.Kind)
                    {
                        case HistoryKind.TributeAgreed:
                        {
                            int payer = e.Subject.Id, lo = e.Object.Id;
                            if (open.TryGetValue(payer, out int bi)) { bonds[bi].End = e.Time; bonds[bi].How = "replaced"; open.Remove(payer); }
                            bonds.Add(new Bond { Payer = payer, Lord = lo, Start = e.Time, Bread = e.ArgB == 1, SizeAfter = e.ArgA, Cause = CauseKind(w, e.Cause1) });
                            open[payer] = bonds.Count - 1; lord[payer] = lo;
                            if (e.ArgB == 1) pAgreeBread++; else pAgreePeace++;
                            break;
                        }
                        case HistoryKind.TributeEnded:
                        {
                            int payer = e.Subject.Id;
                            if (open.TryGetValue(payer, out int bi)) { bonds[bi].End = e.Time; bonds[bi].How = e.ArgA == 1 ? "lord gone" : "lord no stronger"; open.Remove(payer); }
                            lord.Remove(payer);
                            if (e.ArgA == 1) pEndGone++; else pEndWeak++;
                            break;
                        }
                        case HistoryKind.SettlementAbandoned:
                        {
                            int sid = e.Settlement;
                            if (open.TryGetValue(sid, out int bi)) { bonds[bi].End = e.Time; bonds[bi].How = "tributary abandoned"; open.Remove(sid); lord.Remove(sid); }
                            pAband++;
                            break;
                        }
                        case HistoryKind.Raid: pRaids++; break;
                        case HistoryKind.BandRaised: if (e.ArgB == 2) pRaidsLord++; break;
                        case HistoryKind.CallAnswered: pCalls++; pCallMen += e.ArgA; break;
                        case HistoryKind.VillageSplit: pSplits++; break;
                        case HistoryKind.SettlementFounded: pFound++; break;
                        case HistoryKind.Famine: pFamines++; break;
                        case HistoryKind.ReliefSent: pRelief += e.Magnitude; break;
                    }
                }
                integrate(T);
                // the lordships at T
                int villages = 0;
                foreach (var kv in founded) if (live(kv.Key, T)) villages++;
                var paying = new Dictionary<int, int>(); int tributaries = 0, depthMax = 0;
                var rootSize = new Dictionary<int, int>(); var rootDepth = new Dictionary<int, int>();
                foreach (var kv in lord)
                {
                    if (!live(kv.Key, T) || !live(kv.Value, T)) continue;
                    tributaries++;
                    paying.TryGetValue(kv.Value, out int n); paying[kv.Value] = n + 1;
                    int r = kv.Key, d = 0;
                    for (int i = 0; i < 16; i++) { if (!lord.TryGetValue(r, out int l) || !live(l, T) || l == kv.Key) break; r = l; d++; }
                    rootSize.TryGetValue(r, out int rs); rootSize[r] = rs + 1;
                    rootDepth.TryGetValue(r, out int rd); rootDepth[r] = Math.Max(rd, d);
                    depthMax = Math.Max(depthMax, d);
                }
                foreach (var kv in paying) { lordMost.TryGetValue(kv.Key, out int m); lordMost[kv.Key] = Math.Max(m, kv.Value); }
                int big = 0, bigN = 0;
                foreach (var kv in rootSize) if (kv.Value > bigN || (kv.Value == bigN && kv.Key < big)) { big = kv.Key; bigN = kv.Value; }
                string largest = big == 0 ? "-" : name(big) + ": " + bigN + ", depth " + rootDepth[big];
                rows.Add("  " + (T / Y).ToString().PadLeft(4) + " " + villages.ToString().PadLeft(8) + " " + paying.Count.ToString().PadLeft(5) + " " + tributaries.ToString().PadLeft(6) + "  " + largest.PadRight(34) +
                         " " + (pAgreeBread + "/" + pAgreePeace).PadLeft(14) + " " + (pEndGone + "/" + pEndWeak).PadLeft(16) + "  " + (pRaids + " (" + pRaidsLord + ")").PadLeft(14) + "  " + (pCalls + " (" + pCallMen + ")").PadLeft(16) +
                         "  " + pSplits.ToString().PadLeft(6) + " " + pFound.ToString().PadLeft(7) + " " + pAband.ToString().PadLeft(9) + " " + pFamines.ToString().PadLeft(7) + " " + pRelief.ToString("0").PadLeft(7));
                pAgreeBread = pAgreePeace = pEndGone = pEndWeak = pRaids = pRaidsLord = pCalls = pCallMen = pSplits = pFound = pAband = pFamines = 0; pRelief = 0;
            }
            int liveNow = 0; foreach (var kv in founded) if (live(kv.Key, now)) liveNow++;
            int bread = 0; foreach (var b in bonds) if (b.Bread) bread++;
            Console.WriteLine("lordships at " + SimTime.Format(now) + ": " + liveNow + " villages live; " + founded.Count + " ever; bonds recorded " + bonds.Count + " (for bread " + bread + ", for peace " + (bonds.Count - bread) + ")");
            foreach (var r in rows) Console.WriteLine(r);
            // how the bonds ended and how long they lasted
            var ended = new Dictionary<string, List<double>>();
            foreach (var b in bonds)
            {
                string how = b.How ?? "still paid";
                double yrs = ((b.End != 0 ? b.End : now) - b.Start) / (double)Y;
                string key = (b.Bread ? "bread " : "peace ") + how;
                if (!ended.TryGetValue(key, out var l)) ended[key] = l = new List<double>();
                l.Add(yrs);
            }
            var keys = new List<string>(ended.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                var l = ended[key]; l.Sort();
                double sum = 0; foreach (double v in l) sum += v;
                Console.WriteLine("  bonds " + key + ": " + l.Count + ", years median " + l[l.Count / 2].ToString("0") + ", mean " + (sum / l.Count).ToString("0") + ", range " + l[0].ToString("0") + "-" + l[l.Count - 1].ToString("0"));
            }
            // the same pair bound again; lords that came to pay a lord themselves
            var pairs = new Dictionary<long, int>(); int again = 0;
            foreach (var b in bonds) { long pk = ((long)b.Payer << 32) | (uint)b.Lord; pairs.TryGetValue(pk, out int n); if (n > 0) again++; pairs[pk] = n + 1; }
            var wasLord = new HashSet<int>(); foreach (var b in bonds) wasLord.Add(b.Lord);
            int lordsPaid = 0; foreach (var b in bonds) if (wasLord.Contains(b.Payer)) lordsPaid++;
            Console.WriteLine("  bonds of a pair bound before: " + again + "; bonds whose payer was ever a lord: " + lordsPaid + "; causes: " + Causes(bonds));
            // the lords over the whole history
            var lords = new List<int>(lordYears.Keys);
            lords.Sort((x, y) => lordYears[y].CompareTo(lordYears[x]));
            Console.WriteLine("  lords by years held (the most paying at once; bonds held in all):");
            for (int i = 0; i < lords.Count && i < 15; i++)
            {
                int lo = lords[i]; int held = 0; foreach (var b in bonds) if (b.Lord == lo) held++;
                lordMost.TryGetValue(lo, out int most);
                Console.WriteLine("    " + name(lo) + (live(lo, now) ? "" : " (gone)") + ": " + lordYears[lo].ToString("0") + " years, most " + most + ", bonds " + held + (w.Settlements.Exists(lo) ? ", people now " + w.Settlements.Get(lo).Population : ""));
            }
            // the ways between the villages still bound and their lords (C10.6: the tenth carried along them wears them)
            int[] wayStates = new int[4];
            foreach (var b in bonds)
                if (b.How == null && live(b.Payer, now) && live(b.Lord, now)) wayStates[w.Ways.State(now, b.Payer, b.Lord)]++;
            Console.WriteLine("  ways between the bound and their lords now: road " + wayStates[3] + ", path " + wayStates[2] + ", track " + wayStates[1] + ", none " + wayStates[0]);
            if (listBonds)
                foreach (var b in bonds)
                {
                    double km = w.Settlements.Exists(b.Payer) && w.Settlements.Exists(b.Lord) ? Vec3d.ArcDistance(w.Settlements.Get(b.Payer).Anchor, w.Settlements.Get(b.Lord).Anchor, w.Planet.Radius) / 1000.0 : -1;
                    Console.WriteLine("    " + name(b.Payer) + " -> " + name(b.Lord) + " (" + (b.Bread ? "bread" : "peace") + ", " + b.Cause + ", lordship " + b.SizeAfter + " after, " + km.ToString("0.0") + " km): " +
                                      (b.Start / Y) + "-" + (b.End != 0 ? (b.End / Y).ToString() : "now") + " " + (b.How ?? "still paid"));
                }
            return 0;
        }

        sealed class Bond { public int Payer, Lord, SizeAfter; public long Start, End; public bool Bread; public string How, Cause; }

        static string CauseKind(World w, long cause) => cause != 0 && w.History.TryGet(cause, out var c) ? c.Kind.ToString() : "none";

        static string Causes(List<Bond> bonds)
        {
            var n = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var b in bonds) { n.TryGetValue(b.Cause, out int c); n[b.Cause] = c + 1; }
            var parts = new List<string>(); foreach (var kv in n) parts.Add(kv.Key + " " + kv.Value);
            return string.Join(", ", parts);
        }
    }
}
