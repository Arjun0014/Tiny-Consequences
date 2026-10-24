using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>What a block of the book is: prose, a line standing on one record, the numbers of a span, a heading within a chapter.</summary>
    public enum BookKind : byte { Para = 0, Line = 1, Fact = 2, Heading = 3 }

    /// <summary>A block of a chapter. A line stands on its record (`Event`, whose causes the reader can walk); prose, numbers and headings
    /// are the historian's words over the records and the annals; `Settlement` is the place a reader may go to (0 none).</summary>
    public struct BookBlock
    {
        public BookKind Kind;
        public string Text;
        public long Event;
        public int Settlement;
    }

    public sealed class BookChapter
    {
        public string Title, Sub;
        public int Settlement;   // the place the chapter is about, if one (0 none)
        public readonly List<BookBlock> Blocks = new List<BookBlock>();
        internal void Para(string t, int sid = 0) { if (!string.IsNullOrEmpty(t)) Blocks.Add(new BookBlock { Kind = BookKind.Para, Text = t, Settlement = sid }); }
        internal void Fact(string t) { if (!string.IsNullOrEmpty(t)) Blocks.Add(new BookBlock { Kind = BookKind.Fact, Text = t }); }
        internal void Heading(string t, int sid = 0) { Blocks.Add(new BookBlock { Kind = BookKind.Heading, Text = t, Settlement = sid }); }
        internal void Line(World w, in HistoryEvent e) => Blocks.Add(new BookBlock { Kind = BookKind.Line, Text = HistoryText.Dated(w, e), Event = e.Id, Settlement = e.Settlement });
    }

    public sealed class BookPart
    {
        public string Title, Intro;
        public readonly List<BookChapter> Chapters = new List<BookChapter>();
    }

    /// <summary>
    /// The world's years at a glance (the book's Timeline page): its people by year, its ages, each great realm's villages year by year (the lord
    /// and every tier under it), and its greatest moments - all read from the annals and the ledger, nothing drawn that did not happen.
    /// </summary>
    public sealed class BookTimeline
    {
        public int From, To;                 // years [From, To]
        public int[] People;                 // by year (From..To), from the annals
        public int MaxPeople, MaxRealm;
        public readonly List<Era> Ages = new List<Era>();
        public readonly List<TimelineRealm> Realms = new List<TimelineRealm>();   // the greatest first
        public readonly List<TimelineMark> Marks = new List<TimelineMark>();      // in time order
    }

    public sealed class TimelineRealm
    {
        public int Seat, Peak, PeakYear;
        public string Name;
        public int[] Villages;               // by year (From..To): the villages of its realm with the seat, 0 when it was none
    }

    public struct TimelineMark
    {
        public int Year;
        public string Text;
        public long Event;
        public byte Kind;                    // 0 a realm's height, 1 a great famine, 2 a plague, 3 the earth and the sky, 4 a founding or an end
    }

    public sealed class WorldBook
    {
        public string Title, Sub;
        public int Year;
        public readonly List<string> Preface = new List<string>();
        public readonly List<BookPart> Parts = new List<BookPart>();
    }

    /// <summary>
    /// The History Book as a book (session 25; 11 §7, D2-077's chronicle made a book): a historian - an observer that reads the ledger
    /// and the annals and writes nothing - composes the world's history after the fact in eight parts: the Ages (eras found by change
    /// points, Chronicle), Realms and Rulers (each lordship rebuilt year by year from its bonds - when it rose, how far it grew, its
    /// tiers, the houses that led its seat, how it ended), Wars and Feuds (the raids between two villages, a war ended by a bow
    /// between them or by twelve quiet years), Famine, Plague and Disaster (hungry times found as the peaks of the annals' hunger),
    /// Places, Lives (rulers, founders, war leaders, finders), Threads of Cause (the longest chains the ledger keeps, from their root
    /// to their last effect, runs of like links told once) and a Chronology by century. Every line stands on a record whose causes
    /// can be walked; prose only counts and names what those records and the annals hold. Nothing here is an input to the simulation.
    /// </summary>
    public static class HistoryBook
    {
        static string F(double v, string fmt = "N0") => v.ToString(fmt, CultureInfo.InvariantCulture);
        static string Num(long n) => n >= 0 && n < Words.Length ? Words[n] : n.ToString("N0", CultureInfo.InvariantCulture);
        static readonly string[] Words = { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
        static string Plural(long n, string one, string many) => Num(n) + " " + (n == 1 ? one : many);
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : s.StartsWith("A ") || s.StartsWith("An ") || s.StartsWith("The ") || s.StartsWith("In ") ? char.ToLowerInvariant(s[0]) + s.Substring(1) : s;
        static int YearOf(long t) => (int)SimTime.YearOf(t);
        static string Name(World w, int sid) => w.History.NameOf(EntityRef.Settlement(sid));
        static string Span(int a, int b, bool present) => present ? "year " + a + " to this day" : a == b ? "year " + a : "years " + a + " to " + b;
        static string Years(int a, int b) => a == b ? "year " + a : "years " + a + "-" + b;
        static string House(World w, EntityRef household) => HistoryText.Plural(w.History.NameOf(household));

        /// <summary>The parts' titles, in order (a reader's contents before any part is written).</summary>
        public static readonly string[] PartTitles = { "The Ages of the World", "Realms and Rulers", "Wars and Feuds", "Famine, Plague and Disaster", "Places", "Lives", "Threads of Cause", "A Chronology" };

        /// <summary>Writes the whole book of the world as it stands.</summary>
        public static WorldBook Write(World w)
        {
            var bw = new Writer(w);
            for (int i = 0; i < PartTitles.Length; i++) bw.Book.Parts.Add(bw.Part(i));
            return bw.Book;
        }

        /// <summary>The world's years at a glance (the Timeline page): people, ages, the greatest realms year by year, the greatest moments.</summary>
        public static BookTimeline Timeline(World w, int realms = 8)
        {
            var c = new Ctx(w);
            var t = new BookTimeline { From = c.First, To = c.Now };
            int n = t.To - t.From + 1;
            if (n <= 0) return t;
            t.People = new int[n];
            var a = w.Annals;
            for (int i = 0; i < a.Count; i++) { int k = a[i].Year - t.From; if (k >= 0 && k < n) { t.People[k] = a[i].People; t.MaxPeople = System.Math.Max(t.MaxPeople, a[i].People); } }
            t.Ages.AddRange(Chronicle.Eras(w));
            for (int r = 0; r < c.RealmOrder.Count && t.Realms.Count < realms; r++)
            {
                var rr = c.Realms[c.RealmOrder[r]];
                if (rr.Peak < 2) continue;
                var tr = new TimelineRealm { Seat = rr.Seat, Name = Name(w, rr.Seat), Peak = rr.Peak + 1, PeakYear = rr.PeakYear, Villages = new int[n] };
                foreach (var kv in rr.Sizes) { int k = kv.Key - t.From; if (k >= 0 && k < n) tr.Villages[k] = kv.Value + 1; }
                t.MaxRealm = System.Math.Max(t.MaxRealm, tr.Peak);
                t.Realms.Add(tr);
                t.Marks.Add(new TimelineMark { Year = rr.PeakYear, Kind = 0, Text = tr.Name + "'s realm at its height, " + Plural(tr.Peak, "village", "villages") });
            }
            // the worst hungry times (the annals' peaks) and the deadliest plagues and violence of the earth: the moments a reader looks for
            int peak = -1, max = 0;
            for (int i = 0; i < a.Count; i++) if (a[i].Hunger > max) { max = a[i].Hunger; peak = i; }
            if (peak >= 0 && max >= 5) t.Marks.Add(new TimelineMark { Year = a[peak].Year, Kind = 1, Text = "the worst year of hunger: " + F(max) + " dead" });
            int sickPeak = -1, sickMax = 0, ruinPeak = -1, ruinMax = 0;
            for (int i = 0; i < a.Count; i++) { if (a[i].Sick > sickMax) { sickMax = a[i].Sick; sickPeak = i; } if (a[i].Disaster > ruinMax) { ruinMax = a[i].Disaster; ruinPeak = i; } }
            if (sickPeak >= 0 && sickMax >= 5) t.Marks.Add(new TimelineMark { Year = a[sickPeak].Year, Kind = 2, Text = "the worst year of sickness: " + F(sickMax) + " dead" });
            if (ruinPeak >= 0 && ruinMax >= 3) t.Marks.Add(new TimelineMark { Year = a[ruinPeak].Year, Kind = 3, Text = "the earth's and the sky's worst year: " + F(ruinMax) + " dead" });
            t.Marks.Sort((p, q) => p.Year.CompareTo(q.Year));
            return t;
        }

        /// <summary>The book written part by part (the game writes a part when its reader opens it): the title, the preface and the
        /// ledger's first reading at once, each part on demand.</summary>
        public sealed class Writer
        {
            readonly Ctx _c;
            public readonly WorldBook Book;
            public double LastMs;

            public Writer(World w)
            {
                var t = System.Diagnostics.Stopwatch.StartNew();
                _c = new Ctx(w);
                Book = new WorldBook { Year = YearOf(w.Now), Title = "The History of the World" };
                Book.Sub = _c.Years > 0 ? "An account of its " + F(_c.Years) + " years, from the first settlers to year " + Book.Year : "Its first days";
                Preface(_c, Book);
                LastMs = t.Elapsed.TotalMilliseconds;
            }

            public BookPart Part(int i)
            {
                var t = System.Diagnostics.Stopwatch.StartNew();
                BookPart p;
                switch (i)
                {
                    case 0: p = Ages(_c); break;
                    case 1: p = Realms(_c); break;
                    case 2: p = Wars(_c); break;
                    case 3: p = Disasters(_c); break;
                    case 4: p = Places(_c); break;
                    case 5: p = Lives(_c); break;
                    case 6: p = Threads(_c); break;
                    default: p = Chronology(_c); break;
                }
                LastMs = t.Elapsed.TotalMilliseconds;
                return p;
            }
        }

        // ================================================================== what the historian reads first
        /// <summary>The ledger read once: the bonds of tribute rebuilt, the rulers of each place, the realms year by year.</summary>
        sealed class Ctx
        {
            public readonly World W;
            public readonly int Now, First, Years;
            public readonly List<Bond> Bonds = new List<Bond>();
            public readonly Dictionary<int, List<HistoryEvent>> Rulers = new Dictionary<int, List<HistoryEvent>>();
            public readonly Dictionary<int, Realm> Realms = new Dictionary<int, Realm>();
            public readonly List<int> RealmOrder = new List<int>();
            public readonly Dictionary<int, HistoryEvent> Founded = new Dictionary<int, HistoryEvent>();
            public readonly Dictionary<int, HistoryEvent> Abandoned = new Dictionary<int, HistoryEvent>();
            public readonly Dictionary<int, List<int>> At = new Dictionary<int, List<int>>();   // a place's events (ledger indices), oldest first
            public readonly List<HistoryEvent> Refusals = new List<HistoryEvent>();              // askings refused (C10.8)
            public readonly List<HistoryEvent> Judgments = new List<HistoryEvent>();             // amends made at a lord's judgment (C10.10)
            public List<War> WarList;

            public Ctx(World w)
            {
                W = w;
                Now = YearOf(w.Now);
                First = w.Annals.Count > 0 ? w.Annals[0].Year : Now;
                Years = Now - First;
                var open = new Dictionary<int, int>();   // payer -> its open bond's index
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Settlement > 0) { if (!At.TryGetValue(e.Settlement, out var al)) At[e.Settlement] = al = new List<int>(); al.Add(i); }
                    switch (e.Kind)
                    {
                        case HistoryKind.TributeAgreed:
                            {
                                int payer = e.Subject.Id, lord = e.Object.Id;
                                if (open.TryGetValue(payer, out int b)) { var o = Bonds[b]; o.To = e.Time; Bonds[b] = o; }
                                open[payer] = Bonds.Count;
                                Bonds.Add(new Bond { Payer = payer, Lord = lord, From = e.Time, To = long.MaxValue, Made = e.Id, How = e.ArgB });
                                break;
                            }
                        case HistoryKind.TributeEnded:
                            {
                                int payer = e.Subject.Id;
                                if (open.TryGetValue(payer, out int b)) { var o = Bonds[b]; o.To = e.Time; o.Ended = e.Id; o.EndHow = e.ArgA; Bonds[b] = o; open.Remove(payer); }
                                break;
                            }
                        case HistoryKind.HeadmanRose:
                            if (e.Settlement > 0)
                            {
                                if (!Rulers.TryGetValue(e.Settlement, out var l)) Rulers[e.Settlement] = l = new List<HistoryEvent>();
                                l.Add(e);
                            }
                            break;
                        case HistoryKind.SettlementFounded:
                            if (e.Subject.Kind == EntityKind.Settlement && !Founded.ContainsKey(e.Subject.Id)) Founded[e.Subject.Id] = e;
                            break;
                        case HistoryKind.SettlementAbandoned:
                            if (e.Subject.Kind == EntityKind.Settlement) Abandoned[e.Subject.Id] = e;
                            break;
                        case HistoryKind.TributeDemanded:
                            if (e.ArgA == 0) Refusals.Add(e);
                            break;
                        case HistoryKind.AmendsMade:
                            if (e.ArgB != 0) Judgments.Add(e);
                            break;
                    }
                }
                YearByYear();
            }

            /// <summary>Each year's realms: at the year's end every bond then held, each village under its highest lord; a realm's seat
            /// is a lord that pays nobody.</summary>
            void YearByYear()
            {
                if (Bonds.Count == 0) return;
                int y0 = Now;
                for (int i = 0; i < Bonds.Count; i++) y0 = System.Math.Min(y0, YearOf(Bonds[i].From));
                var lordAt = new Dictionary<int, int>();
                var size = new Dictionary<int, int>();
                var depth = new Dictionary<int, int>();
                var keys = new List<int>();
                for (int y = y0; y <= Now; y++)
                {
                    long t = (y + 1) * SimTime.Year - 1;
                    if (t > W.Now) t = W.Now;
                    lordAt.Clear(); size.Clear(); depth.Clear();
                    for (int i = 0; i < Bonds.Count; i++) if (Bonds[i].From <= t && Bonds[i].To > t) lordAt[Bonds[i].Payer] = Bonds[i].Lord;
                    keys.Clear(); keys.AddRange(lordAt.Keys); keys.Sort();
                    foreach (int v in keys)
                    {
                        int top = v, d = 0;
                        while (lordAt.TryGetValue(top, out int up) && d < 16 && up != v) { top = up; d++; }
                        size.TryGetValue(top, out int n); size[top] = n + 1;
                        depth.TryGetValue(top, out int dd); if (d > dd) depth[top] = d;
                    }
                    keys.Clear(); keys.AddRange(size.Keys); keys.Sort();
                    foreach (int seat in keys)
                    {
                        if (!Realms.TryGetValue(seat, out var r)) { Realms[seat] = r = new Realm { Seat = seat, From = y }; RealmOrder.Add(seat); }
                        int n = size[seat];
                        r.To = y;
                        r.Years++;
                        if (n > r.Peak) { r.Peak = n; r.PeakYear = y; r.PeakDepth = depth[seat]; }
                        r.Sizes.Add(new KeyValuePair<int, int>(y, n));
                    }
                }
                RealmOrder.Sort((a, b) => { var p = Realms[a]; var q = Realms[b]; int c = q.Peak.CompareTo(p.Peak); if (c != 0) return c; c = q.Years.CompareTo(p.Years); return c != 0 ? c : a.CompareTo(b); });
            }

            /// <summary>The ledger's index of the first event at or after a time (events are kept in time order).</summary>
            public int IndexAt(long t)
            {
                int lo = 0, hi = W.History.Count;
                while (lo < hi) { int mid = (lo + hi) >> 1; if (W.History.At(mid).Time < t) lo = mid + 1; else hi = mid; }
                return lo;
            }

            /// <summary>A place's events: those at it, and those naming it elsewhere (its raids on others, its bonds), oldest first.</summary>
            public List<HistoryEvent> Of(int sid)
            {
                var l = new List<HistoryEvent>();
                var seen = new HashSet<long>();
                if (At.TryGetValue(sid, out var at)) foreach (int i in at) { var e = W.History.At(i); seen.Add(e.Id); l.Add(e); }
                var me = EntityRef.Settlement(sid);
                var ev = W.History.EventsOf(me);
                if (ev != null)
                    for (int i = 0; i < ev.Count; i++)
                        if (!seen.Contains(ev[i]) && W.History.TryGet(ev[i], out var e) && (e.Subject == me || e.Object == me)) { seen.Add(e.Id); l.Add(e); }
                l.Sort((a, b) => a.Id.CompareTo(b.Id));
                return l;
            }

            /// <summary>The houses that led a place over some years: runs of its firsts by household (a house's run ends when another rises).</summary>
            public List<HouseRun> Houses(int sid, int fromYear, int toYear)
            {
                var runs = new List<HouseRun>();
                if (!Rulers.TryGetValue(sid, out var l)) return runs;
                for (int i = 0; i < l.Count; i++)
                {
                    int y0 = YearOf(l[i].Time), y1 = i + 1 < l.Count ? YearOf(l[i + 1].Time) : Now;
                    if (y1 < fromYear || y0 > toYear) continue;
                    var house = l[i].Object;
                    string name = W.History.NameOf(house);
                    if (runs.Count > 0 && runs[runs.Count - 1].Name == name) { var r = runs[runs.Count - 1]; r.To = y1; r.Heads++; runs[runs.Count - 1] = r; }
                    else runs.Add(new HouseRun { House = house, Name = name, From = System.Math.Max(y0, fromYear), To = y1, Heads = 1, First = l[i].Id });
                }
                return runs;
            }
        }

        struct Bond { public int Payer, Lord, How, EndHow; public long From, To, Made, Ended; }
        struct HouseRun { public EntityRef House; public string Name; public int From, To, Heads; public long First; }

        sealed class Realm
        {
            public int Seat, From, To, Years, Peak, PeakYear, PeakDepth;
            public readonly List<KeyValuePair<int, int>> Sizes = new List<KeyValuePair<int, int>>();
            public int SizeAt(int y) { for (int i = Sizes.Count - 1; i >= 0; i--) if (Sizes[i].Key <= y) return Sizes[i].Key == y ? Sizes[i].Value : 0; return 0; }
        }

        /// <summary>The most important of some events, at most n, in time order.</summary>
        static void Best(List<HistoryEvent> evs, int n)
        {
            if (n <= 0) { evs.Clear(); return; }
            if (evs.Count > n)
            {
                // the n-th greatest importance by a sort of the keys alone (a great town's thousands of records), then those above it
                // and, of those equal to it, the oldest (the same choice as a sort by importance, then by id)
                var keys = new float[evs.Count];
                for (int i = 0; i < keys.Length; i++) keys[i] = evs[i].Importance;
                System.Array.Sort(keys);
                float cut = keys[keys.Length - n];
                int above = 0;
                for (int i = 0; i < evs.Count; i++) if (evs[i].Importance > cut) above++;
                int equal = n - above;
                var kept = new List<HistoryEvent>(n);
                for (int i = 0; i < evs.Count; i++)
                {
                    var e = evs[i];
                    if (e.Importance > cut) kept.Add(e);
                    else if (e.Importance == cut && equal > 0) { kept.Add(e); equal--; }
                }
                evs.Clear();
                evs.AddRange(kept);
            }
            evs.Sort((p, q) => p.Id.CompareTo(q.Id));
        }

        /// <summary>Where a chain of causes began, as a sentence and its year ("A harvest failed at Tolby (40% short), in year 40."), or "".</summary>
        static string RootOf(World w, long id, long[] buf)
        {
            int n = w.History.CausalChain(id, buf);
            if (n < 2 || !w.History.TryGet(buf[n - 1], out var r)) return "";
            return HistoryText.Describe(w, r).TrimEnd('.') + ", in year " + YearOf(r.Time) + ".";
        }

        // ================================================================== preface
        static void Preface(Ctx c, WorldBook book)
        {
            var w = c.W;
            var a = w.Annals;
            long hunger = 0, sick = 0, wounds = 0, disaster = 0, raids = 0, founded = 0, abandoned = 0;
            for (int i = 0; i < a.Count; i++) { hunger += a[i].Hunger; sick += a[i].Sick; wounds += a[i].Wounds; disaster += a[i].Disaster; raids += a[i].Raids; founded += a[i].Founded; abandoned += a[i].Abandoned; }
            int places = 0, gone = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { if (w.Settlements.Get(sid).Inhabited) places++; else gone++; }
            book.Preface.Add("This book tells what the ledger of the world keeps of its " + F(c.Years) + " years. " + F(w.LivingPeople()) + " people live now in " +
                             Plural(places, "place", "places") + (gone > 0 ? "; " + Plural(gone, "place stands", "places stand") + " abandoned" : "") + ". In its years " + F(founded) + " villages were founded and " +
                             F(abandoned) + " abandoned; " + F(hunger) + " people died of hunger, " + F(sick) + " of sickness, " + F(wounds) + " of wounds and " + F(disaster) +
                             " in the violence of the earth and the sky; there were " + F(raids) + " raids.");
            if (c.RealmOrder.Count > 0)
            {
                var r = c.Realms[c.RealmOrder[0]];
                int nowLords = 0, nowPaying = 0;
                foreach (int seat in c.RealmOrder) { var q = c.Realms[seat]; if (q.To == c.Now) { nowLords++; nowPaying += q.SizeAt(c.Now); } }
                book.Preface.Add("The greatest realm the world has known was " + Name(w, r.Seat) + "'s: at its height, in year " + r.PeakYear + ", " + Plural(r.Peak, "village", "villages") + " paid it tribute" +
                                 (r.PeakDepth > 1 ? ", in " + Num(r.PeakDepth + 1) + " tiers" : "") + ". " +
                                 (nowLords > 0 ? "Today " + Plural(nowLords, "lord takes", "lords take") + " tribute from " + Plural(nowPaying, "village", "villages") + "." : "Today no village pays tribute to another."));
            }
            book.Preface.Add("Every line of the book stands on a record of the ledger: follow it to its causes, back to where its chain began - in nature, in people's choices, or in yours.");
        }

        // ================================================================== I. the ages
        static BookPart Ages(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "The Ages of the World", Intro = "The years fall into ages, found after the fact where what marked them changed - hunger, sickness, feuds and raids, growth, foundings, walls, lords, ruin, warmth." };
            var a = w.Annals;
            foreach (var era in Chronicle.Eras(w))
            {
                var ch = new BookChapter { Title = era.Name, Sub = Span(era.From, era.To - 1, era.Present) };
                int i0 = -1, i1 = -1;
                for (int i = 0; i < a.Count; i++) { if (a[i].Year >= era.From && i0 < 0) i0 = i; if (a[i].Year < era.To) i1 = i; }
                if (i0 >= 0 && i1 >= i0)
                {
                    long hunger = 0, sick = 0, wounds = 0, raids = 0, walls = 0, tributes = 0, founded = 0, abandoned = 0, disaster = 0, born = 0, died = 0;
                    int worstY = 0; long worst = -1; byte sign = era.Sign;
                    for (int i = i0; i <= i1; i++)
                    {
                        var y = a[i];
                        hunger += y.Hunger; sick += y.Sick; wounds += y.Wounds; raids += y.Raids; walls += y.Walls; tributes += y.Tributes; founded += y.Founded; abandoned += y.Abandoned; disaster += y.Disaster;
                        born += y.Births; died += y.Deaths;
                        long v = sign == Chronicle.SignHunger ? y.Hunger : sign == Chronicle.SignSickness ? y.Sick : sign == Chronicle.SignStrife ? y.Wounds + y.Raids : sign == Chronicle.SignRuin ? y.Disaster : -1;
                        if (v > worst) { worst = v; worstY = y.Year; }
                    }
                    int before = i0 > 0 ? a[i0 - 1].People : a[i0].People, after = a[i1].People;
                    var sb = new StringBuilder();
                    sb.Append(Mark(era, hunger, sick, wounds, raids, walls, tributes, founded, abandoned, disaster, worstY, worst, before, after)).Append(' ');
                    if (sign != Chronicle.SignGrowth && sign != Chronicle.SignDecline)
                        sb.Append("People ").Append(after > before * 1.05 ? "grew from " + F(before) + " to " + F(after) : after < before * 0.95 ? "fell from " + F(before) + " to " + F(after) : "held at about " + F(after)).Append("; ");
                    else sb.Append(F(before)).Append(" people became ").Append(F(after)).Append("; ");
                    sb.Append(Plural(a[i1].Places, "place was", "places were")).Append(" lived in at its end.");
                    int bestSeat = 0, bestN = 0;
                    foreach (int seat in c.RealmOrder) { int n = c.Realms[seat].SizeAt(a[i1].Year); if (n > bestN) { bestN = n; bestSeat = seat; } }
                    if (bestSeat != 0) sb.Append(' ').Append(era.Present ? "Now" : "At its end").Append(" the greatest realm was ").Append(Name(w, bestSeat)).Append("'s, of ").Append(Plural(bestN + 1, "village", "villages")).Append('.');
                    ch.Para(sb.ToString());
                    ch.Fact("Born " + F(born) + ", died " + F(died) + " - of hunger " + F(hunger) + ", of sickness " + F(sick) + ", of wounds " + F(wounds) + ", in disasters " + F(disaster) +
                            ". Founded " + F(founded) + ", abandoned " + F(abandoned) + ". Raids " + F(raids) + ", palisades begun " + F(walls) + ", tributes agreed " + F(tributes) + ".");
                }
                {
                    long t0 = era.From * SimTime.Year, t1 = era.To * SimTime.Year;
                    int j0 = c.IndexAt(t0), j1 = c.IndexAt(t1);
                    var marked = new List<HistoryEvent>();
                    var any = new List<HistoryEvent>();
                    for (int j = j0; j < j1; j++)
                    {
                        var e = w.History.At(j);
                        if (Ordinary(e.Kind)) continue;
                        (Chronicle.Marks(era.Sign, e.Kind) ? marked : any).Add(e);
                    }
                    Best(marked, 7);
                    Best(any, 14 - marked.Count);
                    marked.AddRange(any);
                    marked.Sort((p, q) => p.Id.CompareTo(q.Id));
                    foreach (var e in marked) ch.Line(w, e);
                }
                part.Chapters.Add(ch);
            }
            return part;
        }

        /// <summary>The ordinary, never a line of the book by itself (as Chronicle's).</summary>
        static bool Ordinary(HistoryKind k) => k == HistoryKind.Harvest || k == HistoryKind.TributePaid || k == HistoryKind.LightningStrike;

        /// <summary>What marked an age, in a sentence from its numbers.</summary>
        static string Mark(in Era era, long hunger, long sick, long wounds, long raids, long walls, long tributes, long founded, long abandoned, long disaster, int worstY, long worst, int before, int after)
        {
            switch (era.Sign)
            {
                case Chronicle.SignHunger: return "Hunger marked these years: " + F(hunger) + " people died of want" + (worst > 0 ? ", the most in year " + worstY + " (" + F(worst) + ")" : "") + ".";
                case Chronicle.SignSickness: return "Sickness marked these years: " + F(sick) + " people died of it" + (worst > 0 ? ", the most in year " + worstY + " (" + F(worst) + ")" : "") + ".";
                case Chronicle.SignStrife: return "Feuds and raids marked these years: " + Plural(raids, "raid", "raids") + ", and " + F(wounds) + " dead of their wounds.";
                case Chronicle.SignGrowth: return "These were years of plenty: the people grew by " + F(100.0 * (after - before) / System.Math.Max(1, before)) + " %.";
                case Chronicle.SignDecline: return "These were lean years: " + (after < before ? "the people fell by " + F(100.0 * (before - after) / System.Math.Max(1, before)) + " %" : F(abandoned) + " places were abandoned") + ".";
                case Chronicle.SignFounding: return "It was an age of founders: " + Plural(founded, "new village", "new villages") + (abandoned > 0 ? ", and " + Num(abandoned) + " left" : "") + ".";
                case Chronicle.SignWalls: return "Villages walled themselves in these years: " + Plural(walls, "palisade was", "palisades were") + " begun.";
                case Chronicle.SignLords: return "Lords rose in these years: " + Plural(tributes, "village", "villages") + " bowed to pay tribute.";
                case Chronicle.SignRuin: return "The earth and the sky marked these years: " + F(disaster) + " people died in their violence" + (worst > 0 ? ", the most in year " + worstY : "") + ".";
                case Chronicle.SignCold: return "These were cold years.";
                case Chronicle.SignWarm: return "These were warm years.";
                default: return "These were quiet years.";
            }
        }

        // ================================================================== II. realms and rulers
        static BookPart Realms(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "Realms and Rulers", Intro = c.RealmOrder.Count == 0 ? "No village has yet bowed to another." :
                "A realm is a lord and the villages that pay it a tenth of their harvests - and those that pay them. Each is rebuilt here year by year from its bonds: when it rose, how far it grew, the houses that led its seat, its wars, and how it ended." };
            int shown = 0;
            var lesser = new List<int>();
            foreach (int seat in c.RealmOrder)
            {
                var r = c.Realms[seat];
                if (shown >= 12 || r.Peak < 2) { lesser.Add(seat); continue; }
                shown++;
                part.Chapters.Add(RealmChapter(c, r));
            }
            if (lesser.Count > 0)
            {
                var ch = new BookChapter { Title = "Lesser Lordships", Sub = Plural(lesser.Count, "lordship", "lordships") };
                foreach (int seat in lesser)
                {
                    var r = c.Realms[seat];
                    ch.Para(Name(w, seat) + " - " + Span(r.From, r.To, r.To == c.Now) + "; at most " + Plural(r.Peak, "village", "villages") + " paying it (year " + r.PeakYear + ").", seat);
                }
                part.Chapters.Add(ch);
            }
            return part;
        }

        static BookChapter RealmChapter(Ctx c, Realm r)
        {
            var w = c.W;
            string seat = Name(w, r.Seat);
            bool present = r.To == c.Now;
            var ch = new BookChapter { Title = "The Realm of " + seat, Sub = Span(r.From, r.To, present) + "; at its height " + Plural(r.Peak + 1, "village", "villages") + " (year " + r.PeakYear + ")", Settlement = r.Seat };
            var mine = new List<Bond>();
            for (int i = 0; i < c.Bonds.Count; i++) if (c.Bonds[i].Lord == r.Seat) mine.Add(c.Bonds[i]);
            var sb = new StringBuilder();
            if (mine.Count > 0 && w.History.TryGet(mine[0].Made, out var first))
            {
                int fy = YearOf(first.Time);
                sb.Append(seat).Append(fy < r.From ? " first took tribute in year " : "'s lordship began in year ").Append(fy).Append(", when ").Append(w.History.NameOf(first.Subject))
                  .Append(first.ArgB == 1 ? " bowed to it for the famine grain it could not give back" : first.ArgB == 2 ? " bowed when it asked a tenth" : " bowed to it after its bands came").Append(". ");
                if (fy < r.From) sb.Append("It paid a lord of its own until year ").Append(r.From).Append(", when it came to stand at the head of its realm. ");
            }
            var marks = new List<string>();
            int lastMark = -1;
            foreach (var kv in r.Sizes)
                if ((kv.Key % 100 == 0 || kv.Key == r.PeakYear) && kv.Key != lastMark) { lastMark = kv.Key; marks.Add(kv.Value + " by year " + kv.Key); }
            if (marks.Count > 1) sb.Append("Villages paying it, in all its tiers: ").Append(string.Join(", ", marks.ToArray())).Append(". ");
            int made = 0, bread = 0, asked = 0, endedWeak = 0, endedGone = 0;
            foreach (var b in mine) { made++; if (b.How == 1) bread++; else if (b.How == 2) asked++; if (b.Ended != 0) { if (b.EndHow == 1) endedGone++; else endedWeak++; } }
            sb.Append(Cap(Plural(made, "bond", "bonds"))).Append(" were made with ").Append(seat).Append(" itself");
            var hows = new List<string>();
            if (made - bread - asked > 0) hows.Add(Num(made - bread - asked) + " after its bands came");
            if (bread > 0) hows.Add(Num(bread) + " for famine grain");
            if (asked > 0) hows.Add(Num(asked) + " when it asked");
            if (hows.Count > 0) sb.Append(" (").Append(string.Join(", ", hows.ToArray())).Append(')');
            sb.Append("; ").Append(Num(endedWeak)).Append(" ended when it was no longer the stronger").Append(endedGone > 0 ? ", " + Num(endedGone) + " when it was gone" : "").Append('.');
            if (present) sb.Append(" Today ").Append(Plural(r.SizeAt(c.Now), "village pays", "villages pay")).Append(" it.");
            else
            {
                string end = null;
                for (int i = 0; i < c.Bonds.Count; i++)
                    if (c.Bonds[i].Payer == r.Seat && YearOf(c.Bonds[i].From) >= r.To && YearOf(c.Bonds[i].From) <= r.To + 1) { end = seat + " itself bowed to " + Name(w, c.Bonds[i].Lord) + " in year " + YearOf(c.Bonds[i].From); break; }
                if (end == null && c.Abandoned.TryGetValue(r.Seat, out var ab) && YearOf(ab.Time) <= r.To + 2) end = seat + " was abandoned in year " + YearOf(ab.Time);
                if (end == null) end = "the last of the villages that paid it stopped in year " + (r.To + 1);
                sb.Append(" It ended when ").Append(end).Append('.');
            }
            ch.Para(sb.ToString(), r.Seat);
            // the houses that led its seat while it was a realm
            var runs = c.Houses(r.Seat, r.From, r.To);
            if (runs.Count > 0)
            {
                ch.Heading("The houses that led " + seat);
                ch.Para(HousesText(c, runs, r.From, r.To), r.Seat);
            }
            ch.Heading("How it grew and shrank");
            var evs = new List<HistoryEvent>();
            foreach (var b in mine)
            {
                if (w.History.TryGet(b.Made, out var e1)) evs.Add(e1);
                if (b.Ended != 0 && w.History.TryGet(b.Ended, out var e2)) evs.Add(e2);
            }
            // (C10.8) the askings its seat made that were refused - a realm's story is also the tenths it was denied
            if (c.At.TryGetValue(r.Seat, out var _))
                foreach (var kv in c.Refusals) if (kv.Subject.Id == r.Seat) evs.Add(kv);
            Best(evs, 14);
            foreach (var e in evs) ch.Line(w, e);
            // (C10.10) the peace its seat kept: the claims for blood between villages it was lord over that it judged
            var judged = new List<HistoryEvent>();
            foreach (var j in c.Judgments) if (j.ArgB == r.Seat) judged.Add(j);
            if (judged.Count > 0)
            {
                double fu = 0;
                int yj0 = int.MaxValue, yj1 = 0;
                foreach (var j in judged) { fu += j.Magnitude; yj0 = System.Math.Min(yj0, YearOf(j.Time)); yj1 = System.Math.Max(yj1, YearOf(j.Time)); }
                ch.Heading("Its peace");
                ch.Para(Cap(Plural(judged.Count, "claim", "claims")) + " for blood between villages that paid " + seat + " came before it in " + Years(yj0, yj1) + ", and at its judgment the doers made amends - " +
                        F(fu) + " days' grain in all.", r.Seat);
                Best(judged, 4);
                foreach (var e in judged) ch.Line(w, e);
            }
            // its wars, by the village it fought
            var foes = new Dictionary<int, int[]>();
            var order = new List<int>();
            foreach (var x in AllWars(c))
            {
                if (x.A != r.Seat && x.B != r.Seat) continue;
                int foe = x.A == r.Seat ? x.B : x.A;
                if (!foes.TryGetValue(foe, out var v)) { foes[foe] = v = new[] { 0, 0, int.MaxValue, 0 }; order.Add(foe); }
                v[0]++; v[1] += x.RaidsA + x.RaidsB; v[2] = System.Math.Min(v[2], x.From); v[3] = System.Math.Max(v[3], x.To);
            }
            if (order.Count > 0)
            {
                order.Sort((p, q) => { int k = foes[q][1].CompareTo(foes[p][1]); return k != 0 ? k : p.CompareTo(q); });
                var parts = new List<string>();
                for (int i = 0; i < order.Count && i < 8; i++)
                {
                    var v = foes[order[i]];
                    parts.Add(Name(w, order[i]) + " (" + Plural(v[0], "war", "wars") + ", " + Plural(v[1], "raid", "raids") + ", " + Years(v[2], v[3]) + ")");
                }
                ch.Heading("Its wars");
                ch.Para("It fought " + string.Join("; ", parts.ToArray()) + (order.Count > 8 ? "; and " + Num(order.Count - 8) + " more" : "") + ".", r.Seat);
            }
            return ch;
        }

        /// <summary>The houses that led a place: each run with its years and heads (the longest first if there are many), and the longest-serving first.</summary>
        static string HousesText(Ctx c, List<HouseRun> runs, int from, int to)
        {
            var w = c.W;
            var sb = new StringBuilder();
            var shown = new List<HouseRun>(runs);
            if (shown.Count > 10)
            {
                shown.Sort((p, q) => { int k = (q.To - q.From).CompareTo(p.To - p.From); return k != 0 ? k : p.From.CompareTo(q.From); });
                shown.RemoveRange(10, shown.Count - 10);
                shown.Sort((p, q) => p.From.CompareTo(q.From));
            }
            var parts = new List<string>();
            foreach (var h in shown) parts.Add("the " + House(w, h.House) + " (" + Years(h.From, h.To) + (h.Heads > 1 ? ", " + Num(h.Heads) + " heads" : "") + ")");
            var distinct = new HashSet<string>();
            foreach (var h in runs) distinct.Add(h.Name);
            sb.Append(Cap(Plural(distinct.Count, "house", "houses"))).Append(" came first among its people in turn").Append(runs.Count > shown.Count ? "; the longest-holding" : "").Append(": ").Append(string.Join(", ", parts.ToArray())).Append('.');
            // the one first longest
            if (c.Rulers.TryGetValue(runs[0].First != 0 && w.History.TryGet(runs[0].First, out var fe) ? fe.Settlement : 0, out var l))
            {
                int best = -1, bestLen = 0;
                for (int i = 0; i < l.Count; i++)
                {
                    int y0 = YearOf(l[i].Time), y1 = i + 1 < l.Count ? YearOf(l[i + 1].Time) : c.Now;
                    if (y1 < from || y0 > to) continue;
                    if (y1 - y0 > bestLen) { bestLen = y1 - y0; best = i; }
                }
                if (best >= 0 && bestLen >= 5)
                {
                    int y0 = YearOf(l[best].Time);
                    sb.Append(" The longest first among them was ").Append(w.History.NameOf(l[best].Subject)).Append(" of the ").Append(House(w, l[best].Object)).Append(", for ").Append(F(bestLen)).Append(" years from year ").Append(y0).Append('.');
                }
            }
            return sb.ToString();
        }

        // ================================================================== III. wars and feuds
        sealed class War
        {
            public int A, B, From, To, RaidsA, RaidsB, Blows, DrivenOff;
            public double Loot, Weight;
            public string Title;
            public long Peace;   // the bow that ended it, if one
            public readonly List<HistoryEvent> Events = new List<HistoryEvent>();
        }

        const int WarGap = 12;    // quiet years between two villages that end a war
        const int FeudYears = 40; // a war longer than this is a feud

        static List<War> AllWars(Ctx c)
        {
            if (c.WarList != null) return c.WarList;
            var w = c.W;
            var byPair = new Dictionary<long, List<HistoryEvent>>();
            var pairs = new List<long>();
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                int a, b;
                if (e.Kind == HistoryKind.Raid) { a = e.Subject.Id; b = e.Object.Id; }
                else if (e.Kind == HistoryKind.BandRaised && e.Object.Kind == EntityKind.Settlement && e.Settlement > 0) { a = e.Settlement; b = e.Object.Id; }
                else if (e.Kind == HistoryKind.TributeDemanded && e.ArgA == 0) { a = e.Subject.Id; b = e.Object.Id; }   // (a refused asking: the asker holds it a wrong)
                else if (e.Kind == HistoryKind.TributeAgreed && e.ArgB != 1) { a = e.Subject.Id; b = e.Object.Id; }   // (a bow ends a war between the two)
                else continue;
                if (a <= 0 || b <= 0 || a == b) continue;
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!byPair.TryGetValue(key, out var l)) { byPair[key] = l = new List<HistoryEvent>(); pairs.Add(key); }
                l.Add(e);
            }
            pairs.Sort();
            var wars = new List<War>();
            foreach (long key in pairs)
            {
                War cur = null;
                foreach (var e in byPair[key])
                {
                    int y = YearOf(e.Time);
                    if (e.Kind == HistoryKind.TributeAgreed)
                    {
                        if (cur != null && y - cur.To <= 2) { cur.Peace = e.Id; cur.To = y; wars.Add(cur); cur = null; }
                        continue;
                    }
                    if (cur == null || y - cur.To > WarGap) { if (cur != null) wars.Add(cur); cur = new War { A = (int)(key >> 32), B = (int)(key & 0xFFFFFFFFL), From = y, To = y }; }
                    cur.To = y;
                    cur.Events.Add(e);
                    if (e.Kind != HistoryKind.Raid) continue;
                    if (e.Subject.Id == cur.A) cur.RaidsA++; else cur.RaidsB++;
                    cur.Blows += e.ArgB % 100;
                    if (e.ArgB / 100 == 2) cur.DrivenOff++;
                    cur.Loot += e.Magnitude;
                    cur.Weight += e.Importance;
                }
                if (cur != null) wars.Add(cur);
            }
            wars.RemoveAll(x => x.RaidsA + x.RaidsB < 2);
            foreach (var x in wars)
            {
                string a = Name(w, x.A), b = Name(w, x.B);
                if (x.To - x.From > FeudYears) x.Title = "The Long Feud of " + a + " and " + b;
                else if (x.RaidsA > 0 && x.RaidsB > 0) x.Title = "The War of " + a + " and " + b;
                else x.Title = "The Raids of " + (x.RaidsA > 0 ? a + " on " + b : b + " on " + a);
            }
            // two of one name: the later the second, third...
            var order = new List<War>(wars);
            order.Sort((p, q) => p.From != q.From ? p.From.CompareTo(q.From) : p.A.CompareTo(q.A));
            var named = new Dictionary<string, int>();
            foreach (var x in order) { named.TryGetValue(x.Title, out int n); named[x.Title] = n + 1; if (n > 0) x.Title = "The " + (n == 1 ? "Second" : n == 2 ? "Third" : n == 3 ? "Fourth" : "Later") + x.Title.Substring(3); }
            wars.Sort((p, q) => { int k = (q.RaidsA + q.RaidsB).CompareTo(p.RaidsA + p.RaidsB); if (k != 0) return k; k = q.Weight.CompareTo(p.Weight); return k != 0 ? k : p.From.CompareTo(q.From); });
            c.WarList = wars;
            return wars;
        }

        static BookPart Wars(Ctx c)
        {
            var w = c.W;
            var wars = AllWars(c);
            var buf = new long[1024];
            var part = new BookPart { Title = "Wars and Feuds", Intro = wars.Count == 0 ? "No two villages have raided each other more than once." :
                "A war here is the raids between two villages, ended by a bow between them or by " + WarGap + " quiet years; one that lasted more than " + FeudYears + " years is a feud. The greatest first; each raid's record holds its causes - the wrongs, the hunger, the lords' calls." };
            int shown = 0;
            var rest = new List<War>();
            foreach (var x in wars)
            {
                if (shown >= 14) { rest.Add(x); continue; }
                shown++;
                var ch = new BookChapter { Title = x.Title, Sub = Span(x.From, x.To, false), Settlement = x.A };
                string a = Name(w, x.A), b = Name(w, x.B);
                var sb = new StringBuilder();
                int raids = x.RaidsA + x.RaidsB;
                sb.Append(Cap(Plural(raids, "raid", "raids"))).Append(" in ").Append(x.To - x.From + 1 == 1 ? "one year" : F(x.To - x.From + 1) + " years");
                if (x.RaidsA > 0 && x.RaidsB > 0) sb.Append(" - ").Append(Num(x.RaidsA)).Append(" by ").Append(a).Append(", ").Append(Num(x.RaidsB)).Append(" by ").Append(b);
                sb.Append(": ").Append(Plural(x.DrivenOff, "band was", "bands were")).Append(" driven off, ").Append(Plural(x.Blows, "blow was", "blows were")).Append(" struck, and ").Append(F(x.Loot)).Append(" days' grain carried off.");
                if (x.Events.Count > 0)
                {
                    sb.Append(" It began when ").Append(Lower(HistoryText.Describe(w, x.Events[0])));
                    string root = RootOf(w, x.Events[0].Id, buf);
                    if (root.Length > 0) sb.Append(" Its first cause on record: ").Append(Lower(root));
                }
                if (x.Peace != 0 && w.History.TryGet(x.Peace, out var pe)) sb.Append(" It ended in year ").Append(YearOf(pe.Time)).Append(" when ").Append(w.History.NameOf(pe.Subject)).Append(" bowed to ").Append(w.History.NameOf(pe.Object)).Append('.');
                else sb.Append(" It ended with no bow: ").Append(x.To >= c.Now - WarGap ? "it may not be over." : "the raids stopped.");
                ch.Para(sb.ToString(), x.A);
                var evs = new List<HistoryEvent>(x.Events);
                if (x.Peace != 0 && w.History.TryGet(x.Peace, out var pe2)) evs.Add(pe2);
                Best(evs, 12);
                foreach (var e in evs) ch.Line(w, e);
                part.Chapters.Add(ch);
            }
            if (rest.Count > 0)
            {
                var ch = new BookChapter { Title = "Lesser Wars and Raids", Sub = Plural(rest.Count, "more", "more") };
                rest.Sort((p, q) => p.From.CompareTo(q.From));
                foreach (var x in rest) ch.Para(x.Title + " - " + Span(x.From, x.To, false) + ": " + Plural(x.RaidsA + x.RaidsB, "raid", "raids") + (x.Peace != 0 ? ", ended by a bow" : "") + ".", x.A);
                part.Chapters.Add(ch);
            }
            return part;
        }

        // ================================================================== IV. famine, plague and disaster
        sealed class Episode
        {
            public int From, To;
            public long Key;
            public double Toll;
            public readonly List<HistoryEvent> Events = new List<HistoryEvent>();
            public readonly HashSet<int> Places = new HashSet<int>();
        }

        static BookPart Disasters(Ctx c)
        {
            var w = c.W;
            var buf = new long[1024];
            var part = new BookPart { Title = "Famine, Plague and Disaster", Intro = "The hungry times, the sicknesses that went from village to village, and the violence of the earth and the sky - each with its toll from the annals and the records." };
            var a = w.Annals;
            // famines: the peaks of the annals' hunger - each the worst year not yet told, widened while its neighbours starved at least
            // two fifths as many; a world at its land's ceiling hungers every year, and the book tells its worst times
            {
                int n = a.Count;
                var hunger = new int[n];
                long all = 0; int hungryYears = 0;
                for (int i = 0; i < n; i++) { hunger[i] = a[i].Hunger; all += hunger[i]; if (hunger[i] > 0) hungryYears++; }
                var told = new bool[n];
                var famines = new List<Episode>();
                int max = 0; for (int i = 0; i < n; i++) max = System.Math.Max(max, hunger[i]);
                while (famines.Count < 8)
                {
                    int peak = -1;
                    for (int i = 0; i < n; i++) if (!told[i] && (peak < 0 || hunger[i] > hunger[peak])) peak = i;
                    if (peak < 0 || hunger[peak] < System.Math.Max(5, max / 10)) break;
                    int lo = peak, hi = peak;
                    while (lo > 0 && !told[lo - 1] && hunger[lo - 1] * 5 >= hunger[peak] * 2) lo--;
                    while (hi + 1 < n && !told[hi + 1] && hunger[hi + 1] * 5 >= hunger[peak] * 2) hi++;
                    var ep = new Episode { From = a[lo].Year, To = a[hi].Year };
                    for (int i = lo; i <= hi; i++) { told[i] = true; ep.Toll += hunger[i]; }
                    famines.Add(ep);
                }
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.Famine) continue;
                    int y = YearOf(e.Time);
                    foreach (var f in famines) if (y >= f.From - 1 && y <= f.To) { f.Events.Add(e); f.Places.Add(e.Settlement); }
                }
                famines.Sort((p, q) => { int k = q.Toll.CompareTo(p.Toll); return k != 0 ? k : p.From.CompareTo(q.From); });
                var ch = new BookChapter { Title = "Famines", Sub = famines.Count == 0 ? "none" : "the worst " + Plural(famines.Count, "hungry time", "hungry times") };
                ch.Para(all == 0 ? "No one has died of hunger." : F(all) + " people died of hunger in " + Plural(hungryYears, "year", "years") + " of the world's " + F(c.Years) + ". These were the worst times:");
                for (int k = 0; k < famines.Count; k++)
                {
                    var f = famines[k];
                    ch.Heading((k == 0 ? "The Great Famine of " : "The famine of ") + Years(f.From, f.To));
                    var sb = new StringBuilder();
                    sb.Append(F(f.Toll)).Append(" died of hunger in those years; ").Append(Plural(f.Places.Count, "village", "villages")).Append(" knew famine.");
                    if (f.Events.Count > 0)
                    {
                        var first = f.Events[0];
                        sb.Append(" It came first to ").Append(Name(w, first.Settlement)).Append(", in year ").Append(YearOf(first.Time)).Append('.');
                        string root = RootOf(w, first.Id, buf);
                        if (root.Length > 0) sb.Append(" Its chain of causes begins: ").Append(root);
                    }
                    ch.Para(sb.ToString(), f.Events.Count > 0 ? f.Events[0].Settlement : 0);
                    var evs = new List<HistoryEvent>(f.Events);
                    Best(evs, 4);
                    foreach (var e in evs) ch.Line(w, e);
                }
                part.Chapters.Add(ch);
            }
            // plagues: each sickness's epidemics, gathered while no more than five years passed between
            {
                var plagues = new List<Episode>();
                var open = new Dictionary<long, Episode>();
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.EpidemicEnded && e.Kind != HistoryKind.EpidemicArrived && e.Kind != HistoryKind.PathogenAppeared) continue;
                    var pr = e.Kind == HistoryKind.PathogenAppeared ? e.Subject : e.Object;
                    long k = pr.Packed;
                    if (pr.Kind == EntityKind.Pathogen && w.Pathogens.Exists(pr.Id)) k = new EntityRef(EntityKind.Pathogen, w.Pathogens.Get(pr.Id).Family).Packed;
                    int y = YearOf(e.Time);
                    if (!open.TryGetValue(k, out var ep) || y - ep.To > 5) { ep = new Episode { From = y, To = y, Key = k }; open[k] = ep; plagues.Add(ep); }
                    ep.To = y;
                    ep.Events.Add(e);
                    if (e.Settlement > 0) ep.Places.Add(e.Settlement);
                    if (e.Kind == HistoryKind.EpidemicEnded) ep.Toll += e.Magnitude;
                }
                plagues.RemoveAll(p => p.Toll < 1);
                plagues.Sort((p, q) => { int k = q.Toll.CompareTo(p.Toll); return k != 0 ? k : p.From.CompareTo(q.From); });
                var ch = new BookChapter { Title = "Plagues", Sub = plagues.Count == 0 ? "none" : Plural(plagues.Count, "outbreak", "outbreaks") };
                double all = 0; foreach (var p in plagues) all += p.Toll;
                long sickAll = 0; for (int i = 0; i < a.Count; i++) sickAll += a[i].Sick;
                ch.Para(plagues.Count == 0 ? "No sickness has gone from village to village with deaths the ledger still keeps." :
                        F(sickAll) + " people died of sickness in the world's years. Sicknesses went from village to village " + Plural(plagues.Count, "time", "times") + " that the ledger still keeps, killing " + F(all) + ". The worst:");
                for (int k = 0; k < plagues.Count && k < 8; k++)
                {
                    var p = plagues[k];
                    string sick = w.History.NameOf(EntityRef.FromPacked(p.Key));
                    ch.Heading(Cap(sick.StartsWith("the ") ? sick : "the " + sick) + ", " + Years(p.From, p.To));
                    var first = p.Events[0];
                    ch.Para("It killed " + F(p.Toll) + " in " + Plural(p.Places.Count, "village", "villages") + (first.Settlement > 0 ? ", beginning at " + Name(w, first.Settlement) + " in year " + YearOf(first.Time) : "") + ".", first.Settlement);
                    var evs = new List<HistoryEvent>(p.Events);
                    Best(evs, 4);
                    foreach (var e in evs) ch.Line(w, e);
                }
                part.Chapters.Add(ch);
            }
            // the earth and the sky
            {
                var ch = new BookChapter { Title = "The Earth and the Sky", Sub = "eruptions, earthquakes, waves, storms, fires and ice" };
                var evs = new List<HistoryEvent>();
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    bool big = e.Kind == HistoryKind.Eruption && e.Magnitude >= 3f || e.Kind == HistoryKind.Quake && e.Magnitude >= 6f || e.Kind == HistoryKind.Tsunami || e.Kind == HistoryKind.StormLandfall ||
                               e.Kind == HistoryKind.StormSurge || e.Kind == HistoryKind.IceAge || e.Kind == HistoryKind.IceAgeEnded || e.Kind == HistoryKind.HomesDrowned ||
                               e.Kind == HistoryKind.Wildfire && e.Magnitude >= 40f || e.Kind == HistoryKind.CoastChanged || e.Kind == HistoryKind.VentBorn || e.Kind == HistoryKind.IslandRose || e.Kind == HistoryKind.Extinction;
                    if (big) evs.Add(e);
                }
                long dead = 0; int worstY = 0, worst = 0;
                for (int i = 0; i < a.Count; i++) { dead += a[i].Disaster; if (a[i].Disaster > worst) { worst = a[i].Disaster; worstY = a[i].Year; } }
                ch.Para(evs.Count == 0 ? "The earth and the sky have done little the ledger keeps." : F(dead) + " people died in the violence of the earth and the sky" + (worst > 0 ? ", the most in year " + worstY + " (" + F(worst) + ")" : "") + ". What the ledger keeps of it:");
                Best(evs, 24);
                foreach (var e in evs) ch.Line(w, e);
                part.Chapters.Add(ch);
            }
            return part;
        }

        // ================================================================== V. places
        static BookPart Places(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "Places", Intro = "The villages and towns, the most storied first: when and from where they were founded, the houses that led them, whom they paid and who paid them, and what befell them." };
            var weight = new Dictionary<int, double>();
            var sids = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                sids.Add(sid);
                double v = 0;
                if (c.At.TryGetValue(sid, out var at)) foreach (int i in at) v += w.History.At(i).Importance;
                if (c.Realms.TryGetValue(sid, out var r)) v += 200.0 * r.Peak;
                ref var s = ref w.Settlements.Get(sid);
                v += s.Inhabited ? s.Population : 0;
                weight[sid] = v;
            }
            sids.Sort((p, q) => { int k = weight[q].CompareTo(weight[p]); return k != 0 ? k : p.CompareTo(q); });
            var rest = new List<int>();
            for (int k = 0; k < sids.Count; k++)
            {
                if (k >= 24) { rest.Add(sids[k]); continue; }
                part.Chapters.Add(PlaceChapter(c, sids[k]));
            }
            if (rest.Count > 0)
            {
                var ch = new BookChapter { Title = "Other Places", Sub = Plural(rest.Count, "place", "places") };
                rest.Sort();
                foreach (int sid in rest) ch.Para(Name(w, sid) + " - " + Fate(c, sid) + ".", sid);
                part.Chapters.Add(ch);
            }
            return part;
        }

        static string Fate(Ctx c, int sid)
        {
            ref var s = ref c.W.Settlements.Get(sid);
            int f = YearOf(s.Founded);
            return s.Inhabited ? "founded in year " + f + "; " + Gazetteer.Word(in s).Substring(2) + " of " + F(s.Population) + " people today" : "founded in year " + f + ", abandoned in year " + YearOf(s.Abandoned);
        }

        static BookChapter PlaceChapter(Ctx c, int sid)
        {
            var w = c.W;
            ref var s = ref w.Settlements.Get(sid);
            string name = Name(w, sid);
            var ch = new BookChapter { Title = name, Sub = Fate(c, sid), Settlement = sid };
            var sb = new StringBuilder();
            if (c.Founded.TryGetValue(sid, out var fe))
            {
                string whence = null;
                var buf = new long[8];
                int n = w.History.CausalChain(fe.Id, buf);
                for (int i = 1; i < n; i++)
                    if (w.History.TryGet(buf[i], out var pe) && (pe.Kind == HistoryKind.PartySetOut || pe.Kind == HistoryKind.VillageSplit))
                    {
                        whence = pe.Kind == HistoryKind.VillageSplit ? "households of " + Name(w, pe.Settlement) + " who would not follow its new first" : "households of " + w.History.NameOf(pe.Subject);
                        break;
                    }
                sb.Append(name).Append(" was founded in year ").Append(YearOf(fe.Time)).Append(whence != null ? " by " + whence : "").Append(". ");
            }
            var runs = c.Houses(sid, 0, c.Now);
            if (runs.Count > 0)
            {
                var held = new Dictionary<string, int>();
                foreach (var h in runs) { held.TryGetValue(h.Name, out int y); held[h.Name] = y + (h.To - h.From); }
                EntityRef bestH = default; int bestY = -1;
                foreach (var h in runs) if (held[h.Name] > bestY) { bestY = held[h.Name]; bestH = h.House; }
                sb.Append(Cap(Plural(held.Count, "house has", "houses have"))).Append(" led its people; the ").Append(House(w, bestH)).Append(" the longest, ").Append(F(bestY)).Append(" years in all");
                if (s.Inhabited && c.Rulers.TryGetValue(sid, out var rl) && rl.Count > 0) sb.Append("; now ").Append(w.History.NameOf(rl[rl.Count - 1].Subject)).Append(" is first among them, since year ").Append(YearOf(rl[rl.Count - 1].Time));
                sb.Append(". ");
            }
            var lords = new List<string>();
            int tributaries = 0;
            foreach (var b in c.Bonds)
            {
                if (b.Payer == sid) lords.Add(Name(w, b.Lord) + " (" + YearOf(b.From) + "-" + (b.To == long.MaxValue ? "" : YearOf(b.To).ToString(CultureInfo.InvariantCulture)) + ")");
                if (b.Lord == sid) tributaries++;
            }
            if (lords.Count > 0) sb.Append("It paid tribute to ").Append(string.Join(", ", lords.ToArray())).Append(". ");
            if (c.Realms.TryGetValue(sid, out var r)) sb.Append("It was the seat of a realm of up to ").Append(Plural(r.Peak + 1, "village", "villages")).Append(" (year ").Append(r.PeakYear).Append("); ").Append(Plural(tributaries, "bond was", "bonds were")).Append(" made with it. ");
            int famines = 0, epidemics = 0, raidsOn = 0, raidsBy = 0, splits = 0, walled = -1;
            var all = c.Of(sid);
            foreach (var e in all)
            {
                if (e.Kind == HistoryKind.Famine && e.Settlement == sid) famines++;
                else if (e.Kind == HistoryKind.EpidemicArrived && e.Settlement == sid) epidemics++;
                else if (e.Kind == HistoryKind.Raid) { if (e.Object.Id == sid) raidsOn++; else if (e.Subject.Id == sid) raidsBy++; }
                else if (e.Kind == HistoryKind.WallBuilt && e.Settlement == sid && walled < 0) walled = YearOf(e.Time);
                else if (e.Kind == HistoryKind.VillageSplit && e.Settlement == sid) splits++;
            }
            var befell = new List<string>();
            if (famines > 0) befell.Add(Plural(famines, "famine", "famines"));
            if (epidemics > 0) befell.Add(Plural(epidemics, "epidemic", "epidemics"));
            if (raidsOn > 0) befell.Add(Plural(raidsOn, "raid", "raids") + " on it");
            if (raidsBy > 0) befell.Add(Plural(raidsBy, "raid", "raids") + " by its bands");
            if (splits > 0) befell.Add(Plural(splits, "split", "splits"));
            if (befell.Count > 0) sb.Append("The ledger keeps ").Append(string.Join(", ", befell.ToArray())).Append(". ");
            if (walled >= 0) sb.Append("Its palisade was finished in year ").Append(walled).Append(". ");
            ch.Para(sb.ToString().TrimEnd(), sid);
            all.RemoveAll(e => Ordinary(e.Kind));
            Best(all, 14);
            foreach (var e in all) ch.Line(w, e);
            return ch;
        }

        // ================================================================== VI. lives
        static BookPart Lives(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "Lives", Intro = "People the ledger remembers for what they did: those first among the people of the great seats, those who led followings away to found villages, those who led the most bands, and those who found new crafts." };
            var listed = new HashSet<int>();
            // rulers: at each great realm's seat, the one first longest while it was a realm
            {
                var ch = new BookChapter { Title = "Rulers", Sub = "the longest first among the people of the great seats" };
                int shown = 0;
                foreach (int seat in c.RealmOrder)
                {
                    if (shown >= 8) break;
                    var r = c.Realms[seat];
                    if (r.Peak < 2 || !c.Rulers.TryGetValue(seat, out var l)) continue;
                    int best = -1, bestLen = 0;
                    for (int i = 0; i < l.Count; i++)
                    {
                        int y0 = YearOf(l[i].Time), y1 = i + 1 < l.Count ? YearOf(l[i + 1].Time) : c.Now;
                        if (y1 < r.From || y0 > r.To) continue;
                        if (y1 - y0 > bestLen) { bestLen = y1 - y0; best = i; }
                    }
                    if (best < 0) continue;
                    var e = l[best];
                    int pid = e.Subject.Id;
                    if (!listed.Add(pid)) continue;
                    shown++;
                    int y = YearOf(e.Time), until = best + 1 < l.Count ? YearOf(l[best + 1].Time) : c.Now;
                    ch.Heading(w.History.NameOf(e.Subject), seat);
                    ch.Para(Who(c, pid) + " First among the people of " + Name(w, seat) + " for " + F(until - y) + " years (" + Years(y, until) + "), while " + Plural(r.SizeAt(y), "village", "villages") + " paid it when it began and " +
                            Plural(r.SizeAt(until), "village", "villages") + " when it ended.", seat);
                    Deeds(c, ch, pid, 4);
                }
                if (ch.Blocks.Count > 0) part.Chapters.Add(ch);
            }
            // founders: who led a following away to make a village of their own
            {
                var ch = new BookChapter { Title = "Founders", Sub = "who led followings away to found villages" };
                var evs = new List<HistoryEvent>();
                for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.VillageSplit && e.Subject.Kind == EntityKind.Person) evs.Add(e); }
                evs.Sort((p, q) => { int k = q.ArgB.CompareTo(p.ArgB); return k != 0 ? k : p.Id.CompareTo(q.Id); });
                for (int i = 0, shown = 0; i < evs.Count && shown < 6; i++)
                {
                    var e = evs[i];
                    if (!listed.Add(e.Subject.Id)) continue;
                    shown++;
                    ch.Heading(w.History.NameOf(e.Subject), e.Settlement);
                    ch.Para(Who(c, e.Subject.Id) + " In year " + YearOf(e.Time) + " they led " + Plural(e.ArgB, "person", "people") + " of " + Name(w, e.Settlement) + " away to make a village of their own.", e.Settlement);
                    Deeds(c, ch, e.Subject.Id, 4);
                }
                if (ch.Blocks.Count > 0) part.Chapters.Add(ch);
            }
            // war leaders: who raised the most bands
            {
                var ch = new BookChapter { Title = "War Leaders", Sub = "who raised the most bands" };
                var count = new Dictionary<int, int>();
                var ids = new List<int>();
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.BandRaised || e.Subject.Kind != EntityKind.Person) continue;
                    if (!count.ContainsKey(e.Subject.Id)) { count[e.Subject.Id] = 0; ids.Add(e.Subject.Id); }
                    count[e.Subject.Id]++;
                }
                ids.Sort((p, q) => { int k = count[q].CompareTo(count[p]); return k != 0 ? k : p.CompareTo(q); });
                for (int i = 0, shown = 0; i < ids.Count && shown < 5; i++)
                {
                    int pid = ids[i];
                    if (!listed.Add(pid)) continue;
                    shown++;
                    ch.Heading(w.History.NameOf(EntityRef.Person(pid)));
                    ch.Para(Who(c, pid) + " They raised " + Plural(count[pid], "band", "bands") + ".");
                    Deeds(c, ch, pid, 4);
                }
                if (ch.Blocks.Count > 0) part.Chapters.Add(ch);
            }
            // finders: who first found a craft by practice
            {
                var ch = new BookChapter { Title = "Finders", Sub = "who first found a craft" };
                var seen = new HashSet<int>();
                for (int i = 0, shown = 0; i < w.History.Count && shown < 8; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.TechniqueFound || e.Subject.Kind != EntityKind.Person || !seen.Add(e.ArgA)) continue;
                    if (!listed.Add(e.Subject.Id)) continue;
                    shown++;
                    ch.Heading(w.History.NameOf(e.Subject), e.Settlement);
                    ch.Para(Who(c, e.Subject.Id), e.Settlement);
                    ch.Line(w, e);
                }
                if (ch.Blocks.Count > 0) part.Chapters.Add(ch);
            }
            return part;
        }

        /// <summary>A person in a sentence from the registry: born, died, their home.</summary>
        static string Who(Ctx c, int pid)
        {
            var w = c.W;
            var me = EntityRef.Person(pid);
            var sb = new StringBuilder();
            if (w.History.TryGetEntity(me, out var info))
            {
                sb.Append("Born in year ").Append(YearOf(info.Created));
                if (info.Ended != 0) sb.Append(", died in year ").Append(YearOf(info.Ended)).Append(" aged ").Append((info.Ended - info.Created) / SimTime.Year);
                else if (w.People.Exists(pid) && w.People.Get(pid).Alive) sb.Append(", living");
                sb.Append('.');
            }
            return sb.ToString();
        }

        static void Deeds(Ctx c, BookChapter ch, int pid, int n)
        {
            var w = c.W;
            var evs = new List<HistoryEvent>();
            var l = w.History.EventsOf(EntityRef.Person(pid));
            if (l != null) for (int i = 0; i < l.Count; i++) if (w.History.TryGet(l[i], out var e) && e.Kind != HistoryKind.PersonBorn && e.Kind != HistoryKind.Marriage) evs.Add(e);
            Best(evs, n);
            foreach (var e in evs) ch.Line(w, e);
        }

        // ================================================================== VII. threads of cause
        /// <summary>A kind's family, for telling a run of like links once.</summary>
        static int Family(HistoryKind k)
        {
            switch (k)
            {
                case HistoryKind.Taken: case HistoryKind.TakingSeen: case HistoryKind.Struck: case HistoryKind.ClaimRefused: case HistoryKind.AmendsMade: case HistoryKind.WatchSet: return 1;
                case HistoryKind.BandRaised: case HistoryKind.Raid: case HistoryKind.CallAnswered: return 2;
                case HistoryKind.TributeAgreed: case HistoryKind.TributeEnded: case HistoryKind.TributePaid: case HistoryKind.TributeDemanded: return 3;
                case HistoryKind.HarvestFailed: case HistoryKind.Rationing: case HistoryKind.Hunger: case HistoryKind.Famine: case HistoryKind.WildFood: case HistoryKind.ReliefSent: case HistoryKind.ReliefReturned: return 4;
                case HistoryKind.PathogenAppeared: case HistoryKind.EpidemicArrived: case HistoryKind.EpidemicEnded: return 5;
                case HistoryKind.PartySetOut: case HistoryKind.SettlementFounded: case HistoryKind.VillageSplit: case HistoryKind.Scouted: case HistoryKind.PartyJoined: case HistoryKind.HouseholdMoved: return 6;
                case HistoryKind.HeadmanRose: case HistoryKind.Sworn: case HistoryKind.SwornReleased: return 7;
                case HistoryKind.WorkBegun: case HistoryKind.WorkBuilt: case HistoryKind.WallPlanned: case HistoryKind.WallBuilt: case HistoryKind.WallFallen: return 8;
                case HistoryKind.LightningStrike: case HistoryKind.FireStarted: case HistoryKind.Wildfire: case HistoryKind.FireOut: case HistoryKind.Drought: case HistoryKind.Eruption: case HistoryKind.Quake:
                case HistoryKind.Storm: case HistoryKind.StormLandfall: case HistoryKind.Tsunami: case HistoryKind.IceAge: case HistoryKind.IceAgeEnded: case HistoryKind.CoastChanged: return 9;
                default: return 10;
            }
        }

        static readonly string[] FamilyNames = { "", "takings, blows and refusals", "bands and raids", "bonds of tribute", "hunger", "sickness", "settings out and foundings", "leadership", "works", "nature", "other things" };

        static int Bits(int m) { int n = 0; while (m != 0) { n += m & 1; m >>= 1; } return n; }

        static BookPart Threads(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "Threads of Cause", Intro = "The long chains of causes that passed through the most kinds of things - nature, hunger, sickness, deeds, war, tribute, leaving and founding - from where each began to its last effect. Every link is a record; the book shows each chain's turning points." };
            int n = w.History.Count;
            var index = new Dictionary<long, int>(n);
            var root = new int[n];
            var mask = new int[n];
            for (int i = 0; i < n; i++)
            {
                var e = w.History.At(i);
                index[e.Id] = i;
                int bit = 1 << Family(e.Kind);
                if (e.Cause1 != 0 && index.TryGetValue(e.Cause1, out int p)) { root[i] = root[p]; mask[i] = mask[p] | bit; }
                else { root[i] = i; mask[i] = bit; }
            }
            var cand = new List<KeyValuePair<double, int>>();
            for (int i = 0; i < n; i++)
            {
                var e = w.History.At(i);
                if (e.Tier < Tier.Local || root[i] == i || e.Kind == HistoryKind.TributePaid || e.Kind == HistoryKind.Harvest) continue;
                int kinds = Bits(mask[i]);
                double years = (e.Time - w.History.At(root[i]).Time) / (double)SimTime.Year;
                if (years < 10 || kinds < 3) continue;
                cand.Add(new KeyValuePair<double, int>(DMath.Sqrt(years) * kinds * kinds * (1.0 + DMath.Log(1.0 + e.Importance)), i));
            }
            cand.Sort((p, q) => { int k = q.Key.CompareTo(p.Key); return k != 0 ? k : p.Value.CompareTo(q.Value); });
            var roots = new HashSet<int>();
            var chain = new List<HistoryEvent>();
            foreach (var kv in cand)
            {
                if (part.Chapters.Count >= 8) break;
                int i = kv.Value;
                if (!roots.Add(root[i])) continue;
                var le = w.History.At(i);
                // the whole chain, root first
                chain.Clear();
                long cur = le.Id;
                while (cur != 0 && w.History.TryGet(cur, out var ce) && chain.Count < 100000) { chain.Add(ce); cur = ce.Cause1; }
                chain.Reverse();
                var re = chain[0];
                var ch = new BookChapter { Title = "From " + Short(w, re) + " to " + Short(w, le), Sub = Years(YearOf(re.Time), YearOf(le.Time)) + "; " + Plural(chain.Count, "link", "links"), Settlement = le.Settlement };
                var kinds = new List<string>();
                for (int f = 1; f < FamilyNames.Length; f++) if ((mask[i] & (1 << f)) != 0) kinds.Add(FamilyNames[f]);
                ch.Para("It began in " + (re.RootKind == RootKind.Nature ? "nature" : re.RootKind == RootKind.You ? "your hand" : "people's own choices") + ", " + F((le.Time - re.Time) / (double)SimTime.Year) +
                        " years before its last link here, and passed through " + string.Join(", ", kinds.ToArray()) + ".");
                // the links shown: the root, each family's first link, the most important, the last two
                var show = new bool[chain.Count];
                show[0] = true; show[chain.Count - 1] = true; if (chain.Count > 1) show[chain.Count - 2] = true;
                int seen = 0;
                for (int k = 0; k < chain.Count; k++) { int bit = 1 << Family(chain[k].Kind); if ((seen & bit) == 0) { seen |= bit; show[k] = true; } }
                var byImp = new List<int>();
                for (int k = 0; k < chain.Count; k++) byImp.Add(k);
                byImp.Sort((p, q) => { int x = chain[q].Importance.CompareTo(chain[p].Importance); return x != 0 ? x : p.CompareTo(q); });
                for (int k = 0; k < byImp.Count && k < 6; k++) show[byImp[k]] = true;
                int gapFrom = -1, gapMask = 0;
                for (int k = 0; k < chain.Count; k++)
                {
                    if (!show[k]) { if (gapFrom < 0) { gapFrom = k; gapMask = 0; } gapMask |= 1 << Family(chain[k].Kind); continue; }
                    if (gapFrom >= 0)
                    {
                        var gk = new List<string>();
                        for (int f = 1; f < FamilyNames.Length; f++) if ((gapMask & (1 << f)) != 0) gk.Add(FamilyNames[f]);
                        ch.Fact("... " + Plural(k - gapFrom, "link", "links") + " of " + string.Join(", ", gk.ToArray()) + ", " + Years(YearOf(chain[gapFrom].Time), YearOf(chain[k - 1].Time)) + " ...");
                        gapFrom = -1;
                    }
                    ch.Line(w, chain[k]);
                }
                part.Chapters.Add(ch);
            }
            return part;
        }

        /// <summary>An event in a few words, for a thread's title ("the drought of year 41", "the founding of Tolby").</summary>
        static string Short(World w, in HistoryEvent e)
        {
            string at = e.Settlement > 0 ? Name(w, e.Settlement) : (e.Cell >= 0 ? w.PlaceName(e.Cell) : "");
            string y = " of year " + YearOf(e.Time);
            switch (e.Kind)
            {
                case HistoryKind.SettlementFounded: return "the founding of " + w.History.NameOf(e.Subject);
                case HistoryKind.SettlementAbandoned: return "the end of " + w.History.NameOf(e.Subject);
                case HistoryKind.Drought: return "the drought" + y;
                case HistoryKind.HarvestFailed: return "a failed harvest at " + at;
                case HistoryKind.Rationing: return "short rations at " + at;
                case HistoryKind.Famine: case HistoryKind.Hunger: return "hunger at " + at;
                case HistoryKind.ReliefSent: return "grain sent to " + w.History.NameOf(e.Object);
                case HistoryKind.Eruption: return "the eruption" + y;
                case HistoryKind.Quake: return "the earthquake" + y;
                case HistoryKind.Storm: case HistoryKind.StormLandfall: return "the storm" + y;
                case HistoryKind.LightningStrike: return "a lightning strike" + y;
                case HistoryKind.FireStarted: case HistoryKind.Wildfire: return "a fire" + y;
                case HistoryKind.TributeAgreed: return w.History.NameOf(e.Subject) + "'s bow to " + w.History.NameOf(e.Object);
                case HistoryKind.TributeEnded: return "the end of " + w.History.NameOf(e.Subject) + "'s tribute";
                case HistoryKind.Raid: return "a raid on " + w.History.NameOf(e.Object);
                case HistoryKind.BandRaised: return "a band against " + w.History.NameOf(e.Object);
                case HistoryKind.Taken: case HistoryKind.TakingSeen: return "a taking in the night at " + at;
                case HistoryKind.Struck: return "a blow in the night at " + at;
                case HistoryKind.ClaimRefused: return "amends refused by " + w.History.NameOf(e.Subject);
                case HistoryKind.PartySetOut: return "the setting out from " + w.History.NameOf(e.Subject);
                case HistoryKind.EpidemicArrived: case HistoryKind.EpidemicEnded: return w.History.NameOf(e.Object) + (at.Length > 0 ? " at " + at : "");
                case HistoryKind.PathogenAppeared: return "the first " + w.History.NameOf(e.Subject);
                case HistoryKind.HeadmanRose: return w.History.NameOf(e.Subject) + "'s rise at " + at;
                case HistoryKind.VillageSplit: return "the split of " + at;
                case HistoryKind.WallPlanned: case HistoryKind.WallBuilt: return "the palisade of " + at;
                case HistoryKind.WorkBuilt: case HistoryKind.WorkBegun: return "a work at " + at;
                case HistoryKind.Sworn: return "men sworn at " + at;
                case HistoryKind.PersonDied: return "the death of " + w.History.NameOf(e.Subject);
                case HistoryKind.HouseholdMoved: return "the " + House(w, e.Subject) + "' leaving";
                default: return "year " + YearOf(e.Time) + (at.Length > 0 ? " at " + at : "");
            }
        }

        // ================================================================== VIII. chronology
        static BookPart Chronology(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "A Chronology", Intro = "Each century's most important records, in order." };
            int first = c.First / 100 * 100;
            var byCentury = new Dictionary<int, List<HistoryEvent>>();
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Tier < Tier.Local || e.Kind == HistoryKind.TributePaid || e.Kind == HistoryKind.Harvest || e.Kind == HistoryKind.LightningStrike) continue;
                int cen = YearOf(e.Time) / 100;
                if (!byCentury.TryGetValue(cen, out var l)) byCentury[cen] = l = new List<HistoryEvent>();
                l.Add(e);
            }
            for (int cen = first / 100; cen <= c.Now / 100; cen++)
            {
                if (!byCentury.TryGetValue(cen, out var l)) continue;
                Best(l, 10);
                var ch = new BookChapter { Title = "Years " + cen * 100 + " to " + (cen * 100 + 99 > c.Now ? c.Now : cen * 100 + 99) };
                foreach (var e in l) ch.Line(w, e);
                part.Chapters.Add(ch);
            }
            return part;
        }

        // ================================================================== as text
        /// <summary>The book as Markdown (headless `book`; a reader outside the game). Lines carry their record's id.</summary>
        public static string Markdown(WorldBook book, bool ids = true)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(book.Title).Append("\n\n*").Append(book.Sub).Append("*\n\n");
            foreach (var p in book.Preface) sb.Append(p).Append("\n\n");
            sb.Append("## Contents\n\n");
            for (int i = 0; i < book.Parts.Count; i++)
            {
                sb.Append(Roman(i + 1)).Append(". ").Append(book.Parts[i].Title).Append("\n");
                foreach (var ch in book.Parts[i].Chapters) sb.Append("   - ").Append(ch.Title).Append(string.IsNullOrEmpty(ch.Sub) ? "" : " (" + ch.Sub + ")").Append("\n");
            }
            sb.Append('\n');
            for (int i = 0; i < book.Parts.Count; i++)
            {
                var p = book.Parts[i];
                sb.Append("## ").Append(Roman(i + 1)).Append(". ").Append(p.Title).Append("\n\n");
                if (!string.IsNullOrEmpty(p.Intro)) sb.Append('*').Append(p.Intro).Append("*\n\n");
                foreach (var ch in p.Chapters)
                {
                    sb.Append("### ").Append(ch.Title).Append("\n\n");
                    if (!string.IsNullOrEmpty(ch.Sub)) sb.Append('*').Append(ch.Sub).Append("*\n\n");
                    bool inList = false;
                    foreach (var b in ch.Blocks)
                    {
                        if (b.Kind != BookKind.Line && inList) { sb.Append('\n'); inList = false; }
                        switch (b.Kind)
                        {
                            case BookKind.Heading: sb.Append("#### ").Append(b.Text).Append("\n\n"); break;
                            case BookKind.Fact: sb.Append("> ").Append(b.Text).Append("\n\n"); break;
                            case BookKind.Line: sb.Append("- ").Append(b.Text).Append(ids ? " `[" + b.Event + "]`" : "").Append('\n'); inList = true; break;
                            default: sb.Append(b.Text).Append("\n\n"); break;
                        }
                    }
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        public static string Roman(int n) => n >= 1 && n <= 10 ? new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[n - 1] : n.ToString(CultureInfo.InvariantCulture);
    }
}
