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
            // the lords now: their first households' grain against their own needs, and the tribute that came in over the last ten years (what a
            // lordship's surplus could keep - the measure for the lord's household men, `23` §3)
            {
                var tribIn = new Dictionary<int, double>();
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.TributePaid || now - e.Time > 10 * Y) continue;
                    tribIn.TryGetValue(e.Object.Id, out double t); tribIn[e.Object.Id] = t + e.Magnitude;
                }
                var heads = new List<int>(lordYears.Keys); heads.Sort();
                foreach (int lo in heads)
                {
                    if (!live(lo, now) || !w.Settlements.Exists(lo)) continue;
                    int paying = 0; foreach (var kv in lord) if (kv.Value == lo && live(kv.Key, now)) paying++;
                    if (paying == 0) continue;
                    ref var ls = ref w.Settlements.Get(lo);
                    int hh = ls.HeadHh;
                    double grain = hh != 0 && w.Households.Exists(hh) ? w.Households.Get(hh).Grain : 0;
                    int mem = hh != 0 && w.Households.Exists(hh) ? w.Members(hh).Count : 0;
                    double yearNeed = Math.Max(1, mem) * w.Config.FoodPerAdultDay * SimTime.DaysPerYear;
                    tribIn.TryGetValue(lo, out double ti);
                    Console.WriteLine("  lord " + name(lo) + " (" + ls.Population + ", " + paying + " paying): first household " + mem + " people, grain " + grain.ToString("0") +
                                      " FU = " + (grain / yearNeed).ToString("0.0") + " years of its own need; tribute in over ten years " + ti.ToString("0") + " FU (" +
                                      (ti / 10.0 / (w.Config.FoodPerAdultDay * SimTime.DaysPerYear)).ToString("0.0") + " people's food a year)");
                }
            }
            // the ways between the villages still bound and their lords (C10.6: the tenth carried along them wears them)
            int[] wayStates = new int[4];
            foreach (var b in bonds)
                if (b.How == null && live(b.Payer, now) && live(b.Lord, now)) wayStates[w.Ways.State(now, b.Payer, b.Lord)]++;
            Console.WriteLine("  ways between the bound and their lords now: road " + wayStates[3] + ", path " + wayStates[2] + ", track " + wayStates[1] + ", none " + wayStates[0]);
            {
                // the baseline in the same world: the ways between the other pairs of living villages within a day's walk of each other
                int[] other = new int[4];
                var liveIds = new List<int>();
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) liveIds.Add(sid);
                for (int i = 0; i < liveIds.Count; i++)
                    for (int j = i + 1; j < liveIds.Count; j++)
                    {
                        int x = liveIds[i], y = liveIds[j];
                        if (w.Settlements.Get(x).Lord == y || w.Settlements.Get(y).Lord == x) continue;
                        if (Vec3d.ArcDistance(w.Settlements.Get(x).Anchor, w.Settlements.Get(y).Anchor, w.Planet.Radius) > 6000) continue;
                        other[w.Ways.State(now, x, y)]++;
                    }
                Console.WriteLine("  ways between other neighbours within a day's walk: road " + other[3] + ", path " + other[2] + ", track " + other[1] + ", none " + other[0]);
            }
            // whether the tenth can reach the lord at all (C10.6: carried on foot along a route, or - where none - by sea): a bond for bread needs only a
            // day's walk as the crow flies (C10.4), and relief within it moves at once, so a village across water can be bound to a lord its people
            // cannot walk to - and what it sets aside for the lord then waits
            {
                int noFoot = 0, bySea = 0, waiting = 0; double due = 0, dueStuck = 0, mostDays = 0;
                foreach (var b in bonds)
                {
                    if (b.How != null || !live(b.Payer, now) || !live(b.Lord, now) || !w.Settlements.Exists(b.Payer) || !w.Settlements.Exists(b.Lord)) continue;
                    ref var ps = ref w.Settlements.Get(b.Payer);
                    bool foot = w.Routes.Get(w, b.Payer, b.Lord) != null;
                    bool sea = !foot && ps.LandAt != 0 && w.Settlements.Get(b.Lord).LandAt != 0 && w.SeaRoutes.Get(w, b.Payer, b.Lord) != null;
                    if (!foot) noFoot++;
                    if (sea) bySea++;
                    if (ps.TributeDue >= 1.0)
                    {
                        waiting++; due += ps.TributeDue;
                        if (!foot) dueStuck += ps.TributeDue;
                        TC2.Core.Places.Trade.DaysOfFood(w, in ps, out double perDay);
                        if (perDay > 0) mostDays = Math.Max(mostDays, ps.TributeDue / perDay);
                    }
                    if (listBonds && !foot)   // (C9.5a: the ids to frame in shots, and the plank boats on either side that carry the tenth over)
                        Console.WriteLine("    no way on foot: " + name(b.Payer) + " (" + b.Payer + ") -> " + name(b.Lord) + " (" + b.Lord + ")" + (sea ? " (a sea way between their landings)" : " (no sea way)") +
                                          ", plank boats " + w.Conflict.PlankBoats(b.Payer) + " / " + w.Conflict.PlankBoats(b.Lord) + ", set aside " + ps.TributeDue.ToString("0") + " FU");
                }
                Console.WriteLine("  bonds whose tenth has no way on foot to the lord: " + noFoot + " (a sea way " + bySea + "); set aside and waiting: " + due.ToString("0") + " FU in " + waiting +
                                  " villages (" + dueStuck.ToString("0") + " FU with no way on foot; the most " + mostDays.ToString("0") + " days' food)");
            }
            if (listBonds)
                foreach (var b in bonds)
                {
                    double km = w.Settlements.Exists(b.Payer) && w.Settlements.Exists(b.Lord) ? Vec3d.ArcDistance(w.Settlements.Get(b.Payer).Anchor, w.Settlements.Get(b.Lord).Anchor, w.Planet.Radius) / 1000.0 : -1;
                    Console.WriteLine("    " + name(b.Payer) + " -> " + name(b.Lord) + " (" + (b.Bread ? "bread" : "peace") + ", " + b.Cause + ", lordship " + b.SizeAfter + " after, " + km.ToString("0.0") + " km): " +
                                      (b.Start / Y) + "-" + (b.End != 0 ? (b.End / Y).ToString() : "now") + " " + (b.How ?? "still paid"));
                }
            if (a.Has("might")) MightNow(w, name);
            if (a.Has("heads")) HeadsOver(w, bonds, name);
            if (a.Has("margins")) Margins(w, name);
            if (a.Has("could")) Could(w, name);
            if (a.Has("wrongs")) Wrongs(w, name);
            if (a.Has("together")) Together(w, name);
            if (a.Has("heirs")) Heirs(w, name);
            return 0;
        }

        /// <summary>
        /// (C10.9 measure, `lordships --heirs`) from the ledger: each change of the one first among a lord village's people while bonds were paid to it -
        /// by a death (the rise's cause a death) or otherwise - and the bonds for peace and for bread then held: how often a lord's death would free its
        /// tributaries (a thousand bond-years), how many each would free, and the years between deaths at the seats that held the most.
        /// </summary>
        static void Heirs(World w, Func<int, string> name)
        {
            var H = w.History;
            var peace = new Dictionary<int, HashSet<int>>();   // lord -> payers for peace now
            var bread = new Dictionary<int, HashSet<int>>();
            var lordOf = new Dictionary<int, int>();
            var bondFrom = new Dictionary<int, long>();
            double bondYears = 0;
            int deaths = 0, others = 0, freed = 0, deathsWithPeace = 0;
            var freedHist = new int[8];
            var lastDeath = new Dictionary<int, long>();
            var gaps = new List<double>();
            long Y = SimTime.Year;
            for (int i = 0; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Kind == HistoryKind.TributeAgreed)
                {
                    int payer = e.Subject.Id, lord = e.Object.Id;
                    if (lordOf.TryGetValue(payer, out int old)) { peace.TryGetValue(old, out var po); po?.Remove(payer); bread.TryGetValue(old, out var bo); bo?.Remove(payer); bondYears += (e.Time - bondFrom[payer]) / (double)Y; }
                    lordOf[payer] = lord; bondFrom[payer] = e.Time;
                    var set = e.ArgB == 1 ? bread : peace;
                    if (!set.TryGetValue(lord, out var l)) set[lord] = l = new HashSet<int>();
                    l.Add(payer);
                }
                else if (e.Kind == HistoryKind.TributeEnded)
                {
                    int payer = e.Subject.Id;
                    if (lordOf.TryGetValue(payer, out int lord))
                    {
                        peace.TryGetValue(lord, out var po); po?.Remove(payer); bread.TryGetValue(lord, out var bo); bo?.Remove(payer);
                        bondYears += (e.Time - bondFrom[payer]) / (double)Y; lordOf.Remove(payer);
                    }
                }
                else if (e.Kind == HistoryKind.HeadmanRose && e.Settlement > 0)
                {
                    int lord = e.Settlement;
                    int np = peace.TryGetValue(lord, out var pl) ? pl.Count : 0, nb = bread.TryGetValue(lord, out var bl) ? bl.Count : 0;
                    if (np + nb == 0) continue;
                    bool death = e.Cause1 != 0 && H.TryGet(e.Cause1, out var c) && c.Kind == HistoryKind.PersonDied;
                    if (!death) { others++; continue; }
                    deaths++; freed += np; if (np > 0) deathsWithPeace++;
                    freedHist[Math.Min(7, np)]++;
                    if (lastDeath.TryGetValue(lord, out long t0)) gaps.Add((e.Time - t0) / (double)Y);
                    lastDeath[lord] = e.Time;
                }
            }
            foreach (var kv in lordOf) bondYears += (w.Now - bondFrom[kv.Key]) / (double)Y;
            gaps.Sort();
            double med = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0;
            Console.WriteLine("  heirs (--heirs): " + bondYears.ToString("0") + " bond-years; firsts at lords' seats changed by a death " + deaths + " times (" + (1000.0 * deaths / Math.Max(1, bondYears)).ToString("0.0") +
                              " a thousand bond-years), otherwise " + others + "; a death would have freed " + freed + " bonds for peace (" + deathsWithPeace + " deaths freed any; by how many 0..7+: " +
                              string.Join(" ", Array.ConvertAll(freedHist, x => x.ToString())) + "); years between deaths at one seat: median " + med.ToString("0.0") + " (" + gaps.Count + " gaps)");
        }

        /// <summary>
        /// (C10.9 measure, `lordships --together`) each lord with two or more villages paying it directly: each alone weighs the lord's might against it -
        /// the lord's own strength and the call of its other tributaries within a day's walk (`Might(lord, it)`) - over its own; together, the lord's own
        /// strength without any of their calls (`Might(lord, 0)`) against the sum of theirs (each with its own tributaries' call, `Might(it, lord)`); and
        /// with the near ones only (within a day's walk of the lord: those that could march on it together). How many realms could not be stood against
        /// by any one of their villages but could by them together - the room a rising together would have.
        /// </summary>
        static void Together(World w, Func<int, string> name)
        {
            var C = w.Conflict;
            int lords = 0, aloneNone = 0, couldTogether = 0, couldNear = 0, tribs = 0;
            for (int lord = w.Settlements.First(); lord != 0; lord = w.Settlements.Next(lord))
            {
                ref var ls = ref w.Settlements.Get(lord);
                if (!ls.Inhabited) continue;
                var mine = new List<int>();
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Inhabited && os.Lord == lord) mine.Add(o); }
                if (mine.Count < 2) continue;
                lords++; tribs += mine.Count;
                double own = C.Might(lord, 0), sum = 0, sumNear = 0, best = 0;
                int near = 0;
                bool anyAlone = false;
                foreach (int t in mine)
                {
                    double lm = C.Might(lord, t), tm = C.Might(t, lord);
                    if (tm >= lm) anyAlone = true;
                    best = Math.Max(best, tm / Math.Max(1e-9, lm));
                    sum += tm;
                    if (Vec3d.ArcDistance(w.Settlements.Get(t).Anchor, ls.Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, t, lord) != null) { sumNear += tm; near++; }
                }
                if (!anyAlone) aloneNone++;
                bool together = !anyAlone && sum >= own, togetherNear = !anyAlone && sumNear >= own;
                if (together) couldTogether++;
                if (togetherNear) couldNear++;
                Console.WriteLine("    together " + name(lord) + " (" + ls.Population + " people): " + mine.Count + " paying directly (" + near + " within a day's walk by a way on foot); its own strength " + own.ToString("0") +
                                  "; the strongest alone x" + best.ToString("0.00") + " of what the lord brings against it; all together " + sum.ToString("0") + " (x" + (sum / Math.Max(1e-9, own)).ToString("0.00") +
                                  "), the near ones " + sumNear.ToString("0") + " (x" + (sumNear / Math.Max(1e-9, own)).ToString("0.00") + ")" + (together ? (togetherNear ? " - could rise together, the near ones alone" : " - could rise together") : ""));
            }
            Console.WriteLine("  together (--together): " + lords + " lords with two or more villages paying directly (" + tribs + " villages); none of whose villages could stand against it alone " + aloneNone +
                              "; of those, all their villages together outweigh the lord's own strength " + couldTogether + ", the near ones alone " + couldNear);
        }

        /// <summary>
        /// (session 24, a lord's death and its tributaries measured first: `lordships --heads`) how the bonds lived through their lords' successions: for
        /// each bond, the times the one first among its lord village's people changed while it held (`HeadmanRose` - a local event the ledger may compact,
        /// so a lower bound; and, for the bonds still paid, the lord's present first person against the bond's start, which no compaction hides), those at
        /// the one before's death and those where another household rose; and whether bonds end more often in the five years after such a change than in
        /// their other years.
        /// </summary>
        static void HeadsOver(World w, List<Bond> bonds, Func<int, string> name)
        {
            long now = w.Now, Y = SimTime.Year, after = 5 * Y;
            int changes = 0, atDeath = 0, otherHh = 0, bondsWith = 0, stillPaid = 0, stillNewHead = 0, ends = 0, endsAfter = 0;
            double years = 0, yearsAfter = 0;
            var per = new List<int>();
            var times = new List<long>();
            foreach (var b in bonds)
            {
                long end = b.End != 0 ? b.End : now;
                if (end <= b.Start) continue;
                years += (end - b.Start) / (double)Y;
                times.Clear();
                var pe = w.History.PlaceEventsOf(b.Lord);
                if (pe != null)
                    for (int i = 0; i < pe.Count; i++)
                    {
                        if (!w.History.TryGet(pe[i], out var e) || e.Kind != HistoryKind.HeadmanRose) continue;
                        if (e.Time <= b.Start || e.Time >= end) continue;
                        times.Add(e.Time); changes++;
                        if (e.Cause1 != 0) atDeath++;
                        if (e.ArgB == 2) otherHh++;
                    }
                per.Add(times.Count);
                if (times.Count > 0) bondsWith++;
                // the bond's years within five years after a change of its lord's first person (the windows merged)
                long covered = 0, upTo = b.Start;
                foreach (long t in times)
                {
                    long a0 = Math.Max(t, upTo), a1 = Math.Min(t + after, end);
                    if (a1 > a0) { covered += a1 - a0; upTo = a1; }
                }
                yearsAfter += covered / (double)Y;
                bool endedByLord = b.End != 0 && (b.How == "lord no stronger" || b.How == "lord gone");
                if (endedByLord)
                {
                    ends++;
                    foreach (long t in times) if (b.End > t && b.End - t <= after) { endsAfter++; break; }
                }
                if (b.End == 0)
                {
                    stillPaid++;
                    if (w.Settlements.Exists(b.Lord) && w.Settlements.Get(b.Lord).HeadSince > b.Start) stillNewHead++;
                }
            }
            per.Sort();
            double rateAfter = yearsAfter > 0 ? endsAfter / yearsAfter : 0, rateOther = years - yearsAfter > 0 ? (ends - endsAfter) / (years - yearsAfter) : 0;
            Console.WriteLine("  successions under the bonds (--heads): " + bonds.Count + " bonds, " + years.ToString("0") + " bond-years; the lord's first person changed " + changes +
                              " times while a bond held (" + atDeath + " at a death, " + otherHh + " another household rising; a lower bound - the ledger compacts local events), in " +
                              bondsWith + " bonds (per bond median " + (per.Count > 0 ? per[per.Count / 2] : 0) + ", most " + (per.Count > 0 ? per[per.Count - 1] : 0) + ")");
            Console.WriteLine("    bonds still paid " + stillPaid + ": the lord's first person now came after the bond began in " + stillNewHead);
            Console.WriteLine("    bonds ended by the lord (gone or no stronger) " + ends + ": within five years after a change of its first person " + endsAfter + " in " +
                              yearsAfter.ToString("0") + " bond-years (" + (1000 * rateAfter).ToString("0.0") + " a thousand bond-years), in their other " + (years - yearsAfter).ToString("0") +
                              " years " + (ends - endsAfter) + " (" + (1000 * rateOther).ToString("0.0") + ")");
        }

        /// <summary>
        /// (`lordships --margins`) each bond still paid at the save's moment: the lord's might against the tributary over the tributary's own (C10.5; by
        /// lasting strength, C10.7a). A bond is made only where the lord is the stronger by `TributeOdds` (1.2) and ends only when it is no longer the
        /// stronger (below 1): how many bonds hold in between - bonds that would not be made today.
        /// </summary>
        static void Margins(World w, Func<int, string> name)
        {
            var C = w.Conflict;
            int n = 0, below = 0, between = 0, above = 0, breadN = 0, breadBetween = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Lord == 0 || !w.Settlements.Exists(s.Lord) || !w.Settlements.Get(s.Lord).Inhabited) continue;
                double lm = C.Might(s.Lord, sid), om = C.Might(sid, s.Lord);
                double r = lm / Math.Max(1e-9, om);
                bool bread = s.LordEvent != 0 && w.History.TryGet(s.LordEvent, out var le) && le.ArgB == 1;
                n++; if (bread) breadN++;
                if (r < 1.0) below++;
                else if (r < TC2.Core.Society.Conflict.TributeOdds) { between++; if (bread) breadBetween++; }
                else above++;
                ref var ls = ref w.Settlements.Get(s.Lord);
                Console.WriteLine("    margin " + name(sid) + " -> " + name(s.Lord) + " (" + (bread ? "bread" : "peace") + ", since year " + (s.LordSince / SimTime.Year) + "): the lord " +
                                  lm.ToString("0") + " against " + om.ToString("0") + " = x" + r.ToString("0.00") + "; the lord's first person since year " + (ls.HeadSince / SimTime.Year));
            }
            Console.WriteLine("  margins (--margins): " + n + " bonds still paid (" + breadN + " for bread): the lord below its tributary " + below + ", between x1 and x" +
                              TC2.Core.Society.Conflict.TributeOdds.ToString("0.0") + " (held, not made) " + between + " (for bread " + breadBetween + "), the stronger by the odds " + above);
        }

        /// <summary>
        /// (C10.7a measure, `lordships --might`) each living village's people of fighting age (C10.5's might counts them by heads) and their lasting
        /// strength - the village's tools, arms and iron (C1.4, C5.2b) and each fighter's skill (C1.5), as `Conflict.Strength` weighs them in a fight, the
        /// day's health and wounds left out: how far a weighing by lasting strength would part from the head count, and whether the bonds still paid
        /// would hold by it.
        /// </summary>
        static void MightNow(World w, Func<int, string> name)
        {
            long now = w.Now;
            var rows = new List<(double factor, int hands, int sid)>();
            int skilled = 0, grown = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double armed = TC2.Core.Knowledge.Tech.ToolShare(in s);
                double arms = s.Population > 0 ? DMath.Clamp01(s.Arms / (TC2.Core.Places.Forge.ArmsPerHead * s.Population)) : 0.0;
                double iron = s.Population > 0 ? DMath.Clamp01(s.IronArms / (TC2.Core.Places.Forge.ArmsPerHead * s.Population)) : 0.0;
                // (the simulation's own weighings: heads, and their lasting strength - years, kit and skill)
                int n = w.Conflict.Hands(sid);
                double sum = w.Conflict.Lasting(sid), skill = 0;
                var res = w.Residents(sid);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var q = ref w.People.Get(res[i]);
                    if (!q.Alive) continue;
                    double age = q.AgeYears(now);
                    if (age < 16 || age >= 60) continue;
                    grown++; skill += q.SkFight; if (q.SkFight > 0.1f) skilled++;
                }
                if (n == 0) continue;
                rows.Add((sum / n, n, sid));
                bool isLord = w.Conflict.PolitySize(sid) > 0 && w.Settlements.Get(sid).Lord == 0;
                Console.WriteLine("    might " + name(sid) + " (" + sid + (s.Lord != 0 ? ", pays " + name(s.Lord) : isLord ? ", a lord" : "") + "): " + n + " of fighting age, lasting strength x" +
                                  (sum / n).ToString("0.00") + " (kit x" + w.Conflict.Kit(sid).ToString("0.00") + ": tools " + armed.ToString("0.00") + ", arms " + arms.ToString("0.00") + ", iron " + iron.ToString("0.00") +
                                  "; fighting skill " + (skill / n).ToString("0.00") + " a head) = " + sum.ToString("0"));
            }
            rows.Sort((x, y) => x.factor.CompareTo(y.factor));
            if (rows.Count > 0)
                Console.WriteLine("  lasting strength a head across " + rows.Count + " villages: least x" + rows[0].factor.ToString("0.00") + ", median x" + rows[rows.Count / 2].factor.ToString("0.00") +
                                  ", most x" + rows[rows.Count - 1].factor.ToString("0.00") + "; grown people with fighting skill over 0.1: " + skilled + " of " + grown);
            // the bonds still paid: the lord the stronger by heads, and by lasting strength
            int bonds = 0, byHeads = 0, byLasting = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Lord == 0 || !w.Settlements.Exists(s.Lord) || !w.Settlements.Get(s.Lord).Inhabited) continue;
                bonds++;
                // (both weighings on this binary: C10.5's heads with `--oldmightarms`, C10.7a's lasting strength without)
                bool was = TC2.Core.Society.Conflict.MightArmsOff;
                TC2.Core.Society.Conflict.MightArmsOff = true;
                if (w.Conflict.Might(s.Lord, sid) >= w.Conflict.Might(sid, s.Lord)) byHeads++;
                TC2.Core.Society.Conflict.MightArmsOff = false;
                if (w.Conflict.Might(s.Lord, sid) >= w.Conflict.Might(sid, s.Lord)) byLasting++;
                TC2.Core.Society.Conflict.MightArmsOff = was;
            }
            Console.WriteLine("  bonds still paid " + bonds + ": the lord the stronger by heads " + byHeads + ", by lasting strength " + byLasting);
        }

        /// <summary>
        /// (`lordships --could`, session 24: what limits the lordships - formation?) the bonds that could be: pairs of living villages within a day's walk
        /// (6 km) of different realms, neither paying the other, where the one is the stronger by the odds of a bow (`TributeOdds` x1.2, `Might` with its
        /// call) - a village that bowed to every such neighbour would; how many strong villages have such a weaker neighbour and how many weak villages such a
        /// stronger one, whether a way on foot joins them, and whether the strong keep sworn men (C10.7b) or a lord's first household could feed them.
        /// </summary>
        static void Could(World w, Func<int, string> name)
        {
            var C = w.Conflict;
            var live = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) live.Add(sid);
            int pairs = 0, afoot = 0, freeWeak = 0, bound = 0;
            var strong = new HashSet<int>(); var weak = new HashSet<int>(); var lordsCould = new HashSet<int>();
            foreach (int a in live)
                foreach (int b in live)
                {
                    if (a == b) continue;
                    ref var sa = ref w.Settlements.Get(a); ref var sb = ref w.Settlements.Get(b);
                    if (Vec3d.ArcDistance(sa.Anchor, sb.Anchor, w.Planet.Radius) > 6000) continue;
                    if (C.Root(a) == C.Root(b)) { if (sb.Lord == a) bound++; continue; }
                    double ma = C.Might(a, b), mb = C.Might(b, a);
                    if (ma < TC2.Core.Society.Conflict.TributeOdds * mb) continue;
                    pairs++; strong.Add(a); weak.Add(b);
                    if (w.Routes.Get(w, a, b) != null) afoot++;
                    if (sb.Lord == 0) freeWeak++;
                    if (C.PolitySize(a) > 0 || TC2.Core.Society.Retinue.Count(w, a) > 0) lordsCould.Add(a);
                }
            Console.WriteLine("  could be (--could): " + live.Count + " villages; bonds now within a day's walk " + bound + "; pairs of different realms where one is the stronger by the odds " + pairs +
                              " (a way on foot joins " + afoot + "; the weaker paying nobody " + freeWeak + "): " + strong.Count + " villages could make a neighbour bow, " + weak.Count +
                              " could be made to; of the strong, lords or keepers of sworn men " + lordsCould.Count);
        }

        /// <summary>
        /// (`lordships --wrongs`, session 24: the record's other road into lordship - protection) the wrongs held now: villages whose heaviest wrong
        /// (`Settlement.FoeWorth`, D2-072) is heavy enough to raise a band (`RaidFrom`); the foe's might against theirs (could their own band answer it?);
        /// and a third village within a day's walk, joined by a way on foot, of another realm than the foe's, stronger than the foe by the odds of a bow -
        /// a neighbour it could commend itself to, whose bands would answer its wrongs (D2-108).
        /// </summary>
        static void Wrongs(World w, Func<int, string> name)
        {
            var C = w.Conflict;
            int held = 0, heavy = 0, foeStronger = 0, protector = 0, commend = 0, paying = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Foe == 0 || !w.Settlements.Exists(s.Foe) || !w.Settlements.Get(s.Foe).Inhabited) continue;
                held++;
                if (s.FoeWorth < TC2.Core.Society.Conflict.RaidFrom) continue;
                heavy++;
                if (s.Lord != 0) paying++;
                int foe = s.Foe;
                double r = C.Might(foe, sid) / Math.Max(1e-9, C.Might(sid, foe));
                if (r >= 1.0) foeStronger++;
                int by = 0;
                for (int o = w.Settlements.First(); o != 0 && by == 0; o = w.Settlements.Next(o))
                {
                    if (o == sid || o == foe || !w.Settlements.Get(o).Inhabited || C.Root(o) == C.Root(foe)) continue;
                    if (Vec3d.ArcDistance(s.Anchor, w.Settlements.Get(o).Anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, sid, o) == null) continue;
                    if (C.Might(o, foe) >= TC2.Core.Society.Conflict.TributeOdds * C.Might(foe, o)) by = o;
                }
                if (by != 0) protector++;
                if (by != 0 && r >= 1.0 && s.Lord == 0) commend++;
                Console.WriteLine("    wrong " + name(sid) + (s.Lord != 0 ? " (pays " + name(s.Lord) + ")" : "") + " holds " + s.FoeWorth.ToString("0") + " FU against " + name(foe) +
                                  ": the foe x" + r.ToString("0.00") + " its might" + (by != 0 ? "; " + name(by) + " is the foe's stronger by the odds" : "; no neighbour the foe's stronger"));
            }
            Console.WriteLine("  wrongs (--wrongs): villages holding a wrong " + held + ", heavy enough to raise a band " + heavy + " (paying a lord " + paying + "); the foe the stronger " +
                              foeStronger + "; a neighbour within a day's walk the foe's stronger by the odds " + protector + "; free villages that could commend themselves (foe stronger, a protector) " + commend);
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
