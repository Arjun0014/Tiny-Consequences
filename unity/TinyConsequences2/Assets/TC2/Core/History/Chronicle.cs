using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>An age of the world, named after the fact (years [From, To)).</summary>
    public struct Era
    {
        public int From, To;
        public string Name;
        public byte Sign;      // what marked it most (Chronicle.Sign*)
        public bool Present;   // the age that has not ended
    }

    /// <summary>A line of the book: its words and the record they stand on (an event of the ledger, or a span of the annals).</summary>
    public struct BookLine
    {
        public long Event;          // the event the line tells (0: a line of numbers from the annals)
        public int AnnalFrom, AnnalTo;   // the annals' years a numbers line reads ([from, to))
        public string Text;
    }

    /// <summary>
    /// The historian (D2-077; 11 §7-8): an observer that reads the annals and the ledger and writes nothing. Eras are found after the
    /// fact by change points in the years' indicators (hunger, sickness, wounds and raids, growth, foundings, walls and tributes,
    /// ruin, warmth), each at least thirty years, named from what marked it most; each era's chapter is its most important events in
    /// order, every line standing on its record (an event with its causes, or the annals' numbers); a place's history and a
    /// person's life likewise. Nothing here is an input to the simulation.
    /// </summary>
    public static class Chronicle
    {
        public const byte SignQuiet = 0, SignHunger = 1, SignSickness = 2, SignStrife = 3, SignGrowth = 4, SignDecline = 5, SignWalls = 6, SignLords = 7,
                          SignRuin = 8, SignCold = 9, SignWarm = 10, SignFounding = 11;
        const int Features = 9;
        /// <summary>The longest age a historian names before cutting it at its best change (years).</summary>
        public const int MaxAge = 120;

        // ------------------------------------------------------------------ eras
        /// <summary>The ages of the world so far, found in its annals (the last one the present age). An age whose name another age shares
        /// is named by the realm that led it when one did - `under(fromYear, toYear)`: the greatest realm for most of its years, or null
        /// (the book reads it from the bonds of tribute: "The Years of Plenty under Raimstead") -, else by its order ("The Second Hungry Years").</summary>
        public static List<Era> Eras(World w, int minYears = 30, System.Func<int, int, string> under = null)
        {
            var a = w.Annals;
            int n = a.Count;
            var eras = new List<Era>();
            if (n == 0) return eras;
            var x = Indicators(a);
            var cuts = new List<int> { 0, n };
            if (n >= 2 * minYears) Split(new Sums(x), 0, n, minYears, cuts);
            cuts.Sort();
            double meanTemp = 0;
            for (int i = 0; i < n; i++) meanTemp += a[i].Temp;
            meanTemp /= n;
            var names = new List<string>();
            var shared = new Dictionary<string, int>();
            for (int k = 0; k + 1 < cuts.Count; k++)
            {
                int from = cuts[k], to = cuts[k + 1];
                byte sign = Signature(x, from, to, out double strength);
                string name = Name(w, sign, from, to, meanTemp);
                if (k == 0 && (sign == SignQuiet || sign == SignWarm || sign == SignCold || strength < 1.0)) name = "The First Years";
                names.Add(name);
                shared.TryGetValue(name, out int c);
                shared[name] = c + 1;
                eras.Add(new Era { From = a[from].Year, To = a[to - 1].Year + 1, Sign = sign, Present = to == n });
            }
            var used = new Dictionary<string, int>();
            for (int k = 0; k < eras.Count; k++)
            {
                var e = eras[k];
                string name = names[k];
                if (shared[name] > 1 && under != null && name != "The First Years")
                {
                    string r = under(e.From, e.To);
                    if (!string.IsNullOrEmpty(r)) name += " under " + r;
                }
                used.TryGetValue(name, out int times);
                used[name] = times + 1;
                if (times > 0) name = Ordinal(times + 1) + " " + name.Substring(4);   // ("The Hungry Years" -> "The Second Hungry Years")
                e.Name = e.Present && cuts[k + 1] - cuts[k] < minYears ? "The Present Age" : name;
                eras[k] = e;
            }
            return eras;
        }

        /// <summary>The years' indicators, smoothed over nine years and standardized: what change points are found in.</summary>
        static double[][] Indicators(Annals a)
        {
            int n = a.Count;
            var raw = new double[Features][];
            for (int f = 0; f < Features; f++) raw[f] = new double[n];
            for (int i = 0; i < n; i++)
            {
                var y = a[i];
                double people = y.People > 0 ? y.People : 1;
                double prev = i > 0 && a[i - 1].People > 0 ? a[i - 1].People : people;
                raw[0][i] = 1000.0 * y.Hunger / people;
                raw[1][i] = 1000.0 * y.Sick / people;
                raw[2][i] = 1000.0 * y.Wounds / people + 2.0 * y.Raids;
                raw[3][i] = 100.0 * (y.People - prev) / prev;
                raw[4][i] = y.Founded - y.Abandoned;
                raw[5][i] = y.Walls;
                raw[6][i] = y.Tributes;
                raw[7][i] = 1000.0 * y.Disaster / people;
                raw[8][i] = y.Temp;
            }
            var x = new double[Features][];
            for (int f = 0; f < Features; f++)
            {
                x[f] = new double[n];
                for (int i = 0; i < n; i++)
                {
                    int lo = i - 4 < 0 ? 0 : i - 4, hi = i + 4 >= n ? n - 1 : i + 4;
                    double s = 0;
                    for (int j = lo; j <= hi; j++) s += raw[f][j];
                    x[f][i] = s / (hi - lo + 1);
                }
                double mean = 0, var = 0;
                for (int i = 0; i < n; i++) mean += x[f][i];
                mean /= n;
                for (int i = 0; i < n; i++) var += (x[f][i] - mean) * (x[f][i] - mean);
                double sd = DMath.Sqrt(var / n);
                for (int i = 0; i < n; i++) x[f][i] = sd > 1e-9 ? (x[f][i] - mean) / sd : 0.0;
            }
            return x;
        }

        /// <summary>Binary segmentation: the cut of [a, b) that lowers the spread most, if it is worth a new age; then each side.</summary>
        static void Split(Sums x, int a, int b, int min, List<int> cuts)
        {
            if (b - a < 2 * min) return;
            double whole = x.Cost(a, b), best = 0;
            int at = -1;
            for (int k = a + min; k <= b - min; k++)
            {
                double gain = whole - x.Cost(a, k) - x.Cost(k, b);
                if (gain > best) { best = gain; at = k; }
            }
            // (worth a new age: a gain above what noise of this length gives - a BIC-like penalty; an age longer than a historian's
            // span is cut at its best change all the same)
            if (at < 0 || (best < 2.0 * Features * DMath.Log(b - a) && b - a <= MaxAge)) return;
            cuts.Add(at);
            Split(x, a, at, min, cuts);
            Split(x, at, b, min, cuts);
        }

        /// <summary>Running sums of the indicators and of their squares: the spread of any span of years in constant time (a world of
        /// 5,000 years is read as quickly as one of 500).</summary>
        sealed class Sums
        {
            readonly double[][] _s, _q;

            public Sums(double[][] x)
            {
                int n = x[0].Length;
                _s = new double[Features][];
                _q = new double[Features][];
                for (int f = 0; f < Features; f++)
                {
                    _s[f] = new double[n + 1];
                    _q[f] = new double[n + 1];
                    for (int i = 0; i < n; i++) { _s[f][i + 1] = _s[f][i] + x[f][i]; _q[f][i + 1] = _q[f][i] + x[f][i] * x[f][i]; }
                }
            }

            /// <summary>The spread of the years [a, b): over the indicators, the squares about the span's mean.</summary>
            public double Cost(int a, int b)
            {
                double c = 0, len = b - a;
                for (int f = 0; f < Features; f++)
                {
                    double s = _s[f][b] - _s[f][a];
                    c += _q[f][b] - _q[f][a] - s * s / len;
                }
                return c;
            }
        }

        /// <summary>What marked an age most: the indicator furthest from the world's usual, and which way - people's own marks first (the
        /// planet's warmth names an age only when nothing people lived through marks it).</summary>
        static byte Signature(double[][] x, int a, int b, out double strength)
        {
            int best = -1; double bv = 0.5;   // (an age nothing marks by half a spread is a quiet one)
            strength = 0;
            for (int f = 0; f < Features; f++)
            {
                if (f == 8 && best >= 0) break;
                double mean = 0;
                for (int i = a; i < b; i++) mean += x[f][i];
                mean /= b - a;
                double v = f == 3 || f == 4 || f == 8 ? DMath.Abs(mean) : mean;   // (growth, founding and warmth mark an age either way)
                if (v > bv) { bv = v; best = f; }
            }
            if (best < 0) return SignQuiet;
            strength = bv;
            double m = 0;
            for (int i = a; i < b; i++) m += x[best][i];
            switch (best)
            {
                case 0: return SignHunger;
                case 1: return SignSickness;
                case 2: return SignStrife;
                case 3: return m > 0 ? SignGrowth : SignDecline;
                case 4: return m > 0 ? SignFounding : SignDecline;
                case 5: return SignWalls;
                case 6: return SignLords;
                case 7: return SignRuin;
                default: return m > 0 ? SignWarm : SignCold;
            }
        }

        static string Name(World w, byte sign, int from, int to, double meanTemp)
        {
            if (sign == SignWarm || sign == SignCold)
            {
                // the planet's ages by its own events, else by how far the age's warmth stood from the long run's
                long t0 = w.Annals[from].Year * SimTime.Year, t1 = (w.Annals[to - 1].Year + 1) * SimTime.Year;
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Time < t0) continue;
                    if (e.Time >= t1) break;
                    if (e.Kind == HistoryKind.IceAge) return "The Coming of the Ice";
                    if (e.Kind == HistoryKind.IceAgeEnded) return "The Thaw";
                }
                double t = 0;
                for (int i = from; i < to; i++) t += w.Annals[i].Temp;
                t /= to - from;
                if (t > meanTemp + 0.3) return "The Warm Years";
                if (t < meanTemp - 0.3) return "The Cold Years";
                return "The Quiet Years";
            }
            switch (sign)
            {
                case SignHunger: return "The Hungry Years";
                case SignSickness:
                    {
                        string s = DeadliestSickness(w, w.Annals[from].Year, w.Annals[to - 1].Year + 1);
                        return s.Length > 0 ? "The Years of the " + Title(s.StartsWith("the ") ? s.Substring(4) : s) : "The Years of Sickness";
                    }
                case SignStrife: return "The Years of Feuds and Raids";
                case SignGrowth: return "The Years of Plenty";
                case SignFounding: return "The Age of Founders";
                case SignDecline: return "The Lean Years";
                case SignWalls: return "The Building of the Walls";
                case SignLords: return "The Years of the Lords";
                case SignRuin: return "The Years of Ruin";
                case SignCold: return "The Cold Years";
                case SignWarm: return "The Warm Years";
                default: return "The Quiet Years";
            }
        }

        /// <summary>The sickness whose epidemics in the years killed most (its name), or "".</summary>
        static string DeadliestSickness(World w, int fromYear, int toYear)
        {
            long t0 = fromYear * SimTime.Year, t1 = toYear * SimTime.Year;
            var dead = new Dictionary<long, double>();
            var order = new List<long>();
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Time < t0) continue;
                if (e.Time >= t1) break;
                if (e.Kind != HistoryKind.EpidemicEnded) continue;
                // (a strain counts for its sickness: the lineage that came from the animals)
                long k = e.Object.Packed;
                if (e.Object.Kind == EntityKind.Pathogen && w.Pathogens.Exists(e.Object.Id)) k = new EntityRef(EntityKind.Pathogen, w.Pathogens.Get(e.Object.Id).Family).Packed;
                if (!dead.ContainsKey(k)) { dead[k] = 0; order.Add(k); }
                dead[k] += (double)e.Magnitude;
            }
            long best = 0; double bv = 0;
            for (int i = 0; i < order.Count; i++) if (dead[order[i]] > bv) { bv = dead[order[i]]; best = order[i]; }
            return best != 0 ? w.History.NameOf(EntityRef.FromPacked(best)) : "";
        }

        static string Ordinal(int n) => n == 2 ? "The Second" : n == 3 ? "The Third" : n == 4 ? "The Fourth" : n == 5 ? "The Fifth" : "The " + n.ToString(CultureInfo.InvariantCulture) + "th";
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Each word capitalized, but "of" (a name in a title: "Black Pox", "Summer Spots of Dahope").</summary>
        static string Title(string s)
        {
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++) if (words[i].Length > 0 && words[i] != "of" && words[i] != "the") words[i] = Cap(words[i]);
            return string.Join(" ", words);
        }
        static string F(double v, string fmt = "0") => v.ToString(fmt, CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ chapters, places, lives
        /// <summary>
        /// An age's chapter: its numbers from the annals (people at its start and end, the dead by cause, its raids, walls and
        /// tributes) and its most important events in order - each line the record it stands on.
        /// </summary>
        public static List<BookLine> Chapter(World w, in Era era, int lines = 12)
        {
            var book = new List<BookLine>();
            var a = w.Annals;
            int i0 = -1, i1 = -1;
            for (int i = 0; i < a.Count; i++) { if (a[i].Year >= era.From && i0 < 0) i0 = i; if (a[i].Year < era.To) i1 = i; }
            if (i0 >= 0 && i1 >= i0)
            {
                long hunger = 0, sick = 0, wounds = 0, raids = 0, walls = 0, tributes = 0, founded = 0, abandoned = 0;
                for (int i = i0; i <= i1; i++) { hunger += a[i].Hunger; sick += a[i].Sick; wounds += a[i].Wounds; raids += a[i].Raids; walls += a[i].Walls; tributes += a[i].Tributes; founded += a[i].Founded; abandoned += a[i].Abandoned; }
                int before = i0 > 0 ? a[i0 - 1].People : a[i0].People;
                string text = "People: " + F(before) + " -> " + F(a[i1].People) + " in " + F(a[i1].Places) + " places; " + F(founded) + " founded, " + F(abandoned) + " abandoned. Dead of hunger " + F(hunger) +
                              ", of sickness " + F(sick) + ", of wounds " + F(wounds) + ". Raids " + F(raids) + ", walls begun " + F(walls) + ", tributes agreed " + F(tributes) + ".";
                book.Add(new BookLine { Event = 0, AnnalFrom = i0, AnnalTo = i1 + 1, Text = text });
            }
            long t0 = era.From * SimTime.Year, t1 = era.To * SimTime.Year;
            // half its lines the most important events of its years, half the most important of what marked it (its foundings, its
            // famines, its epidemics, its raids and blows, its walls, its tributes, its ruin)
            byte sign = era.Sign;
            var marked = new List<BookLine>();
            Top(w, lines / 2, e => e.Time >= t0 && e.Time < t1 && Marks(sign, e.Kind), marked);
            var any = new List<BookLine>();
            Top(w, lines - marked.Count, e => e.Time >= t0 && e.Time < t1 && !Marks(sign, e.Kind), any);
            int m1 = 0, m2 = 0;
            while (m1 < marked.Count || m2 < any.Count)
                book.Add(m2 >= any.Count || (m1 < marked.Count && marked[m1].Event < any[m2].Event) ? marked[m1++] : any[m2++]);
            return book;
        }

        /// <summary>The events that bear an age's mark.</summary>
        public static bool Marks(byte sign, HistoryKind k)
        {
            switch (sign)
            {
                case SignHunger: return k == HistoryKind.Famine || k == HistoryKind.Hunger || k == HistoryKind.HarvestFailed || k == HistoryKind.ReliefSent || k == HistoryKind.Taken;
                case SignSickness: return k == HistoryKind.EpidemicArrived || k == HistoryKind.PathogenAppeared || k == HistoryKind.EpidemicEnded;
                case SignStrife: return k == HistoryKind.Raid || k == HistoryKind.BandRaised || k == HistoryKind.Struck || k == HistoryKind.ClaimRefused;
                case SignGrowth: case SignFounding: return k == HistoryKind.SettlementFounded || k == HistoryKind.PartySetOut || k == HistoryKind.WorkBuilt || k == HistoryKind.TechniqueFound;
                case SignDecline: return k == HistoryKind.SettlementAbandoned || k == HistoryKind.Famine || k == HistoryKind.HouseholdMoved;
                case SignWalls: return k == HistoryKind.WallPlanned || k == HistoryKind.WallBuilt || k == HistoryKind.Raid;
                case SignLords: return k == HistoryKind.TributeAgreed || k == HistoryKind.TributeEnded || k == HistoryKind.TributeDemanded || k == HistoryKind.Raid;
                case SignRuin: return k == HistoryKind.Eruption || k == HistoryKind.Quake || k == HistoryKind.Tsunami || k == HistoryKind.Wildfire || k == HistoryKind.Storm || k == HistoryKind.AshFall;
                case SignCold: return k == HistoryKind.IceAge || k == HistoryKind.HarvestFailed;
                case SignWarm: return k == HistoryKind.IceAgeEnded;
                default: return false;
            }
        }

        /// <summary>A place's history: its most important events, in order.</summary>
        public static List<BookLine> Place(World w, int sid, int lines = 20)
        {
            var book = new List<BookLine>();
            var me = EntityRef.Settlement(sid);
            Top(w, lines, e => e.Settlement == sid || e.Subject == me || e.Object == me, book);
            return book;
        }

        /// <summary>A person's life: every event that names them, in order.</summary>
        public static List<BookLine> Life(World w, int pid, int lines = 30)
        {
            var book = new List<BookLine>();
            var me = EntityRef.Person(pid);
            Top(w, lines, e => e.Subject == me || e.Object == me || e.Other == me, book);
            return book;
        }

        /// <summary>The most important kept events that satisfy a test (above the personal tier for ages and places), in time order.</summary>
        static void Top(World w, int lines, System.Func<HistoryEvent, bool> pick, List<BookLine> into)
        {
            var best = new List<HistoryEvent>();
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.Harvest || e.Kind == HistoryKind.TributePaid || e.Kind == HistoryKind.LightningStrike) continue;   // (the ordinary)
                if (!pick(e)) continue;
                best.Add(e);
            }
            best.Sort((p, q) => { int c = q.Importance.CompareTo(p.Importance); return c != 0 ? c : p.Id.CompareTo(q.Id); });
            if (best.Count > lines) best.RemoveRange(lines, best.Count - lines);
            best.Sort((p, q) => p.Id.CompareTo(q.Id));
            for (int i = 0; i < best.Count; i++) into.Add(new BookLine { Event = best[i].Id, Text = HistoryText.Dated(w, best[i]) });
        }
    }
}
