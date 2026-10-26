using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>What a block of the book is: prose, a line standing on one record, the numbers of a span, a heading within a chapter, a note
    /// (session 27: the lesser records of a span told together, in the margin of the story).</summary>
    public enum BookKind : byte { Para = 0, Line = 1, Fact = 2, Heading = 3, Note = 4 }

    /// <summary>A block of a chapter. A line stands on its record (`Event`, whose causes the reader can walk); prose, numbers and headings
    /// are the historian's words over the records and the annals; `Settlement` is the place a reader may go to (0 none). `Weight` (session 27):
    /// a line's weight in the story - 2 a great event (a new sickness, a discovery, a realm's great bow, a place's end, the earth's or your
    /// violence), 1 a notable one, 0 a lesser record - so the reader sets the great above the small.</summary>
    public struct BookBlock
    {
        public BookKind Kind;
        public string Text;
        public long Event;
        public int Settlement;
        public byte Weight;
    }

    public sealed class BookChapter
    {
        public string Title, Sub;
        public int Settlement;   // the place the chapter is about, if one (0 none)
        public int MapSeat, MapYear;   // a realm's portrait on a map: its seat and the year drawn (its height); MapSeat 0 none
        public readonly List<BookBlock> Blocks = new List<BookBlock>();
        internal void Para(string t, int sid = 0) { if (!string.IsNullOrEmpty(t)) Blocks.Add(new BookBlock { Kind = BookKind.Para, Text = t, Settlement = sid }); }
        internal void Fact(string t) { if (!string.IsNullOrEmpty(t)) Blocks.Add(new BookBlock { Kind = BookKind.Fact, Text = t }); }
        internal void Note(string t, int sid = 0) { if (!string.IsNullOrEmpty(t)) Blocks.Add(new BookBlock { Kind = BookKind.Note, Text = t, Settlement = sid }); }
        internal void Heading(string t, int sid = 0) { Blocks.Add(new BookBlock { Kind = BookKind.Heading, Text = t, Settlement = sid }); }
        internal void Line(World w, in HistoryEvent e) => Blocks.Add(new BookBlock { Kind = BookKind.Line, Text = HistoryText.Dated(w, e), Event = e.Id, Settlement = e.Settlement, Weight = HistoryBook.Weigh(w, in e) });
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

        /// <summary>Your Actions' part (F9.2; the last since session 27's Discoveries and Works).</summary>
        public const int YourActionsPart = 9;

        /// <summary>The parts' titles, in order (a reader's contents before any part is written).</summary>
        public static readonly string[] PartTitles = { "The Ages of the World", "Realms and Rulers", "Wars and Feuds", "Famine, Plague and Disaster", "Places", "Lives", "Threads of Cause", "A Chronology", "Discoveries and Works", "Your Actions" };

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

            /// <summary>(The book's realm maps) The villages standing at the end of a year - founded by then and not yet abandoned, as the ledger tells
            /// it - split into those of a seat's realm (every village whose highest lord then was the seat, and the seat) and the rest. Reads only.</summary>
            public void RealmAt(int seat, int year, List<int> members, List<int> others)
            {
                members.Clear(); others.Clear();
                var w = _c.W;
                long t = (year + 1) * SimTime.Year - 1;
                if (t > w.Now) t = w.Now;
                var lordAt = new Dictionary<int, int>();
                for (int i = 0; i < _c.Bonds.Count; i++) if (_c.Bonds[i].From <= t && _c.Bonds[i].To > t) lordAt[_c.Bonds[i].Payer] = _c.Bonds[i].Lord;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    if (_c.Founded.TryGetValue(sid, out var f) && f.Time > t) continue;
                    if (_c.Abandoned.TryGetValue(sid, out var a) && a.Time <= t) continue;
                    if (!_c.Founded.ContainsKey(sid) && !w.Settlements.Get(sid).Inhabited && !_c.Abandoned.ContainsKey(sid)) continue;
                    int top = sid, d = 0;
                    while (lordAt.TryGetValue(top, out int up) && d < 16 && up != sid) { top = up; d++; }
                    if (top == seat) members.Add(sid); else others.Add(sid);
                }
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
                    case 8: p = Works(_c); break;         // (session 27)
                    case YourActionsPart: p = YourActions(_c); break;   // (F9.2)
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
                            if (e.Subject.Kind == EntityKind.Settlement)
                            {
                                Abandoned[e.Subject.Id] = e;
                                // a village that pays no longer pays when it is gone: its bond ends with it (no harvest of its own tells its end)
                                if (open.TryGetValue(e.Subject.Id, out int b)) { var o = Bonds[b]; o.To = e.Time; o.Ended = e.Id; o.EndHow = 3; Bonds[b] = o; open.Remove(e.Subject.Id); }
                            }
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
        /// <summary>
        /// The ages (session 27: a chronicle, not a list): each age opens with what marked it in numbers, then its story - the realms that rose,
        /// reached their height or ended in it, the wars begun, its worst sickness, the crafts first found, the first great works, your hand - then
        /// its great events set above the rest, the lesser records of the age told together as notes (the bows to lords by lord, the foundings, the
        /// works by kind, the raids, the hungry and the sick), a few notable records more, and its tally. Every name and number stands on the ledger.
        /// </summary>
        static BookPart Ages(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "The Ages of the World", Intro = "The years fall into ages, found after the fact where what marked them changed - hunger, sickness, feuds and raids, growth, foundings, walls, lords, ruin, warmth." };
            var a = w.Annals;
            var wars = AllWars(c);
            var found = FirstFindings(c);
            foreach (var era in Chronicle.Eras(w))
            {
                var ch = new BookChapter { Title = era.Name, Sub = Span(era.From, era.To - 1, era.Present) };
                int i0 = -1, i1 = -1;
                for (int i = 0; i < a.Count; i++) { if (a[i].Year >= era.From && i0 < 0) i0 = i; if (a[i].Year < era.To) i1 = i; }
                string tally = null;
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
                    tally = "Born " + F(born) + ", died " + F(died) + " - of hunger " + F(hunger) + ", of sickness " + F(sick) + ", of wounds " + F(wounds) + ", in disasters " + F(disaster) +
                            ". Founded " + F(founded) + ", abandoned " + F(abandoned) + ". Raids " + F(raids) + ", palisades begun " + F(walls) + ", tributes agreed " + F(tributes) + ".";
                }
                long t0 = era.From * SimTime.Year, t1 = era.To * SimTime.Year;
                int j0 = c.IndexAt(t0), j1 = c.IndexAt(t1);
                string story = AgeStory(c, era, j0, j1, wars, found);
                if (story.Length > 0) ch.Para(story);
                // the great events, the weightiest first chosen and told in order; the lesser records of the age told together; a few notable records more
                var great = new List<HistoryEvent>();
                var notable = new List<HistoryEvent>();
                var folded = new List<HistoryEvent>();
                for (int j = j0; j < j1; j++)
                {
                    var e = w.History.At(j);
                    if (Ordinary(e.Kind)) continue;
                    byte wt = Weigh(w, in e);
                    if (wt >= 2) great.Add(e);
                    else if (Folded(e.Kind)) folded.Add(e);
                    else if (wt == 1) notable.Add(e);
                }
                Best(great, 7);
                if (great.Count > 0)
                {
                    ch.Heading("The great events");
                    foreach (var e in great) ch.Line(w, e);
                }
                var notes = AgeNotes(c, folded);
                Best(notable, notes.Count > 0 ? 5 : 8);
                if (notes.Count > 0 || notable.Count > 0)
                {
                    ch.Heading("Also in these years");
                    foreach (var n in notes) ch.Note(n);
                    foreach (var e in notable) ch.Line(w, e);
                }
                if (tally != null) ch.Fact(tally);
                part.Chapters.Add(ch);
            }
            return part;
        }

        /// <summary>The records told together in an age's notes rather than one by one: the many like records of a busy age.</summary>
        static bool Folded(HistoryKind k)
        {
            switch (k)
            {
                case HistoryKind.TributeAgreed: case HistoryKind.TributeEnded: case HistoryKind.TributeDemanded: case HistoryKind.SettlementFounded: case HistoryKind.PartySetOut:
                case HistoryKind.WorkBuilt: case HistoryKind.WorkBegun: case HistoryKind.Raid: case HistoryKind.BandRaised: case HistoryKind.Famine: case HistoryKind.Hunger:
                case HistoryKind.Rationing: case HistoryKind.EpidemicArrived: case HistoryKind.EpidemicEnded: case HistoryKind.HeadmanRose: case HistoryKind.WallBuilt:
                case HistoryKind.WallPlanned: case HistoryKind.VillageSplit: case HistoryKind.BoatBuilt: case HistoryKind.BoatToCross: case HistoryKind.WayWorn:
                case HistoryKind.ReliefSent: case HistoryKind.HouseholdMoved: case HistoryKind.Sworn: case HistoryKind.OreFound: case HistoryKind.StoneFound:
                case HistoryKind.CallAnswered: case HistoryKind.TechniqueKnown: case HistoryKind.Taken: case HistoryKind.TakingSeen: case HistoryKind.Struck:
                case HistoryKind.ClaimRefused: case HistoryKind.AmendsMade: case HistoryKind.Wildfire: case HistoryKind.FireStarted: case HistoryKind.HarvestFailed:
                case HistoryKind.WildFood: case HistoryKind.BuildingBurned: case HistoryKind.FieldsBurned:
                    return true;
                default: return false;
            }
        }

        /// <summary>The first finding of each craft in the world (its TechniqueFound record), by craft; the crafts found before the first record are not.</summary>
        static Dictionary<int, HistoryEvent> FirstFindings(Ctx c)
        {
            var w = c.W;
            var first = new Dictionary<int, HistoryEvent>();
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.TechniqueFound && !first.ContainsKey(e.ArgA)) first[e.ArgA] = e;
            }
            return first;
        }

        /// <summary>An age's story in a few sentences, every name from the ledger: the realms that rose, reached their height or ended in it, the wars
        /// begun, its worst sickness, the crafts first found, the first walls of stone, your hand. "" when the age holds none of these.</summary>
        static string AgeStory(Ctx c, in Era era, int j0, int j1, List<War> wars, Dictionary<int, HistoryEvent> found)
        {
            var w = c.W;
            int y0 = era.From, y1 = era.To - 1;
            var sentences = new List<string>();
            // the realms
            {
                var rose = new List<KeyValuePair<int, string>>(); var height = new List<string>(); var ended = new List<string>();
                int k = 0;
                foreach (int seat in c.RealmOrder)
                {
                    var r = c.Realms[seat];
                    if (r.Peak < 3) continue;
                    string n = Name(w, seat);
                    if (r.From >= y0 && r.From <= y1 && rose.Count < 3) rose.Add(new KeyValuePair<int, string>(r.From, n + " (year " + r.From + ")"));
                    if (k < 6 && r.PeakYear >= y0 && r.PeakYear <= y1 && height.Count < 2)
                        height.Add(height.Count == 0 ? n + "'s realm reached its height in year " + r.PeakYear + ", " + Plural(r.Peak + 1, "village", "villages") : n + "'s in year " + r.PeakYear + ", " + Plural(r.Peak + 1, "village", "villages"));
                    if (r.To >= y0 && r.To <= y1 && r.To < c.Now && ended.Count < 3) ended.Add(n + "'s, once " + Plural(r.Peak + 1, "village", "villages") + " (year " + r.To + ")");
                    k++;
                }
                rose.Sort((p, q) => p.Key != q.Key ? p.Key.CompareTo(q.Key) : string.CompareOrdinal(p.Value, q.Value));
                var roseN = new List<string>(); foreach (var kv in rose) roseN.Add(kv.Value);
                if (roseN.Count > 0) sentences.Add("Lords rose at " + Join(roseN) + ".");
                if (height.Count > 0) sentences.Add(Cap(string.Join("; ", height.ToArray())) + ".");
                if (ended.Count > 0) sentences.Add((ended.Count == 1 ? "A realm came to an end: " : "Realms came to an end: ") + Join(ended) + ".");
            }
            // the wars begun in it, the greatest first
            {
                var begun = new List<War>();
                foreach (var x in wars) if (x.From >= y0 && x.From <= y1 && x.RaidsA + x.RaidsB >= 4) begun.Add(x);
                if (begun.Count > 0)
                {
                    var names = new List<string>();
                    for (int i = 0; i < begun.Count && i < 2; i++) names.Add(Lower(begun[i].Title) + " (" + Plural(begun[i].RaidsA + begun[i].RaidsB, "raid", "raids") + ", " + Years(begun[i].From, begun[i].To) + ")");
                    sentences.Add(Cap(Join(names)) + (names.Count == 1 ? " began." : " began.") + (begun.Count > 2 ? " " + Cap(Plural(begun.Count - 2, "lesser war", "lesser wars")) + " began besides." : ""));
                }
            }
            // its worst sickness, by the dead its outbreaks' ends tell
            {
                var toll = new Dictionary<long, double>();
                var names = new Dictionary<long, string>();
                var places = new Dictionary<long, HashSet<int>>();
                for (int j = j0; j < j1; j++)
                {
                    var e = w.History.At(j);
                    if (e.Kind != HistoryKind.EpidemicEnded) continue;
                    var pr = e.Object;
                    long key = pr.Packed;
                    if (pr.Kind == EntityKind.Pathogen && w.Pathogens.Exists(pr.Id)) key = new EntityRef(EntityKind.Pathogen, w.Pathogens.Get(pr.Id).Family).Packed;
                    toll.TryGetValue(key, out double t); toll[key] = t + e.Magnitude;
                    if (!names.ContainsKey(key)) { names[key] = w.History.NameOf(EntityRef.FromPacked(key)); places[key] = new HashSet<int>(); }
                    if (e.Settlement > 0) places[key].Add(e.Settlement);
                }
                long best = 0; double bt = 0;
                foreach (var kv in toll) if (kv.Value > bt || kv.Value == bt && kv.Key < best) { bt = kv.Value; best = kv.Key; }
                if (bt >= 20)
                {
                    string sick = names[best];
                    sentences.Add(Cap(sick.StartsWith("the ") ? sick : "the " + sick) + " killed " + F(bt) + " in " + Plural(places[best].Count, "village", "villages") + ".");
                }
            }
            // the crafts first found in it
            {
                var crafts = new List<string>();
                var firsts = new List<HistoryEvent>(found.Values);
                firsts.Sort((p, q) => p.Id.CompareTo(q.Id));
                foreach (var fe in firsts)
                {
                    int y = YearOf(fe.Time);
                    if (y < y0 || y > y1 || fe.ArgA < 0 || fe.ArgA >= Knowledge.Tech.Count) continue;
                    crafts.Add(Knowledge.Tech.Laws[fe.ArgA].Name + " (" + (fe.Settlement > 0 ? Name(w, fe.Settlement) + ", " : "") + "year " + y + ")");
                }
                if (crafts.Count > 0) sentences.Add("People first learned to " + Join(crafts) + ".");
            }
            // the first walls of stone, the world's first palisade
            {
                string stone = null, timber = null;
                for (int j = 0; j < j1 && (stone == null || timber == null); j++)
                {
                    var e = w.History.At(j);
                    if (e.Kind != HistoryKind.WallBuilt) continue;
                    bool inAge = j >= j0;
                    if (e.ArgB == 2) { if (stone == null) stone = inAge ? Name(w, e.Settlement) + " (year " + YearOf(e.Time) + ")" : ""; }
                    else if (timber == null) timber = inAge ? Name(w, e.Settlement) + " (year " + YearOf(e.Time) + ")" : "";
                }
                if (!string.IsNullOrEmpty(timber)) sentences.Add("The first palisade in the world was finished at " + timber + ".");
                if (!string.IsNullOrEmpty(stone)) sentences.Add("The first walls of stone in the world rose at " + stone + ".");
            }
            // your hand
            {
                int casts = 0;
                var kinds = new List<string>();
                for (int j = j0; j < j1; j++)
                {
                    var e = w.History.At(j);
                    if (e.Kind != HistoryKind.GodCommand) continue;
                    casts++;
                    string d = HistoryText.Describe(w, in e);
                    if (d.StartsWith("You ", System.StringComparison.Ordinal)) d = d.Substring(4);
                    d = d.TrimEnd('.');
                    if (kinds.Count < 3 && !kinds.Contains(d)) kinds.Add(d);
                }
                if (casts > 0) sentences.Add("You reached into the world " + (casts == 1 ? "once" : Num(casts) + " times") + ": you " + Join(kinds) + (casts > kinds.Count ? ", and more" : "") + ".");
            }
            return string.Join(" ", sentences.ToArray());
        }

        /// <summary>An age's lesser records told together: the bows to lords by lord, the bonds ended, the foundings and the parties that set out, the
        /// works raised by kind, the walls, the raids and bands, the hungry and the sick, new firsts and splits, boats and roads. Each note counts and
        /// names only records of the age.</summary>
        static List<string> AgeNotes(Ctx c, List<HistoryEvent> evs)
        {
            var w = c.W;
            var notes = new List<string>();
            // bows to lords, by lord
            {
                var byLord = new Dictionary<int, List<string>>(); var lords = new List<int>(); int bows = 0, ended = 0, refused = 0;
                foreach (var e in evs)
                {
                    if (e.Kind == HistoryKind.TributeEnded) { ended++; continue; }
                    if (e.Kind == HistoryKind.TributeDemanded) { if (e.ArgA == 0) refused++; continue; }
                    if (e.Kind != HistoryKind.TributeAgreed) continue;
                    bows++;
                    int lord = e.Object.Id;
                    if (!byLord.TryGetValue(lord, out var l)) { byLord[lord] = l = new List<string>(); lords.Add(lord); }
                    string n = w.History.NameOf(e.Subject);
                    if (!l.Contains(n)) l.Add(n);
                }
                if (bows > 0)
                {
                    lords.Sort((p, q) => byLord[q].Count != byLord[p].Count ? byLord[q].Count.CompareTo(byLord[p].Count) : p.CompareTo(q));
                    var parts = new List<string>();
                    for (int i = 0; i < lords.Count && i < 4; i++)
                    {
                        var l = byLord[lords[i]];
                        parts.Add("to " + Name(w, lords[i]) + ", " + (l.Count <= 4 ? Join(l) : string.Join(", ", l.GetRange(0, 3).ToArray()) + " and " + Num(l.Count - 3) + " more"));
                    }
                    notes.Add(Cap(Plural(bows, "bow", "bows")) + " to lords: " + string.Join("; ", parts.ToArray()) + (lords.Count > 4 ? "; and to " + Plural(lords.Count - 4, "lord", "lords") + " more" : "") + "." +
                              (ended > 0 ? " " + Cap(Plural(ended, "bond", "bonds")) + " of tribute ended." : "") + (refused > 0 ? " " + Cap(Plural(refused, "asking was", "askings were")) + " refused." : ""));
                }
                else if (ended > 0) notes.Add(Cap(Plural(ended, "bond", "bonds")) + " of tribute ended.");
            }
            // foundings and parties
            {
                var places = new List<string>(); int parties = 0; long people = 0;
                foreach (var e in evs)
                {
                    if (e.Kind == HistoryKind.SettlementFounded && e.Subject.Kind == EntityKind.Settlement) places.Add(w.History.NameOf(e.Subject) + " (" + YearOf(e.Time) + ")");
                    else if (e.Kind == HistoryKind.PartySetOut) { parties++; people += e.ArgB; }
                }
                if (places.Count > 0) notes.Add(Cap(Plural(places.Count, "village was", "villages were")) + " founded: " + (places.Count <= 8 ? Join(places) : string.Join(", ", places.GetRange(0, 7).ToArray()) + " and " + Num(places.Count - 7) + " more") + "." +
                                                (parties > 0 ? " " + Cap(Plural(parties, "party", "parties")) + " set out to make new homes (" + Plural(people, "person", "people") + ")." : ""));
                else if (parties > 0) notes.Add(Cap(Plural(parties, "party", "parties")) + " set out to make new homes (" + Plural(people, "person", "people") + ").");
            }
            // works raised, by kind
            {
                var byKind = new Dictionary<int, List<string>>(); var kinds = new List<int>();
                foreach (var e in evs)
                {
                    if (e.Kind != HistoryKind.WorkBuilt) continue;
                    if (!byKind.TryGetValue(e.ArgA, out var l)) { byKind[e.ArgA] = l = new List<string>(); kinds.Add(e.ArgA); }
                    string n = w.History.NameOf(e.Subject);
                    if (!l.Contains(n)) l.Add(n);
                }
                if (kinds.Count > 0)
                {
                    kinds.Sort((p, q) => byKind[q].Count != byKind[p].Count ? byKind[q].Count.CompareTo(byKind[p].Count) : p.CompareTo(q));
                    var parts = new List<string>();
                    foreach (int k in kinds)
                    {
                        var l = byKind[k];
                        string name = TC2.Core.Places.Structures.Name((byte)k);
                        parts.Add((l.Count == 1 ? Article(name) + " at " + l[0] : Num(l.Count) + " " + PluralOf(name) + " (" + (l.Count <= 3 ? Join(l) : string.Join(", ", l.GetRange(0, 2).ToArray()) + " and " + Num(l.Count - 2) + " more") + ")"));
                        if (parts.Count >= 6) break;
                    }
                    notes.Add("Works raised: " + string.Join("; ", parts.ToArray()) + (kinds.Count > 6 ? "; and more" : "") + ".");
                }
            }
            // walls
            {
                var palisades = new List<string>(); var begun = 0;
                foreach (var e in evs)
                {
                    if (e.Kind == HistoryKind.WallBuilt && e.ArgB != 2) { string n = Name(w, e.Settlement); if (!palisades.Contains(n)) palisades.Add(n); }
                    else if (e.Kind == HistoryKind.WallPlanned) begun++;
                }
                if (palisades.Count > 0) notes.Add("Palisades were finished at " + (palisades.Count <= 5 ? Join(palisades) : string.Join(", ", palisades.GetRange(0, 4).ToArray()) + " and " + Num(palisades.Count - 4) + " more") + ".");
            }
            // raids and bands
            {
                int raids = 0, bands = 0, called = 0;
                var pairs = new Dictionary<long, int>();
                foreach (var e in evs)
                {
                    if (e.Kind == HistoryKind.BandRaised) bands++;
                    else if (e.Kind == HistoryKind.CallAnswered) called++;
                    else if (e.Kind == HistoryKind.Raid)
                    {
                        raids++;
                        int x = e.Subject.Id, y = e.Object.Id;
                        long key = x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
                        pairs.TryGetValue(key, out int n); pairs[key] = n + 1;
                    }
                }
                int takings = 0, blows = 0, refused = 0, amends = 0;
                foreach (var e in evs)
                {
                    if (e.Kind == HistoryKind.Taken || e.Kind == HistoryKind.TakingSeen) takings++;
                    else if (e.Kind == HistoryKind.Struck) blows++;
                    else if (e.Kind == HistoryKind.ClaimRefused) refused++;
                    else if (e.Kind == HistoryKind.AmendsMade) amends++;
                }
                if (takings + blows + refused + amends > 0)
                {
                    var q = new List<string>();
                    if (takings > 0) q.Add(Plural(takings, "taking", "takings") + " from other villages' stores in the night");
                    if (blows > 0) q.Add(Plural(blows, "blow", "blows") + " struck");
                    if (amends > 0) q.Add(Plural(amends, "wrong", "wrongs") + " amended");
                    if (refused > 0) q.Add(Plural(refused, "claim for amends", "claims for amends") + " refused");
                    notes.Add("Between neighbours: " + Join(q) + ".");
                }
                if (raids > 0 || bands > 0)
                {
                    long top = 0; int tn = 0;
                    foreach (var kv in pairs) if (kv.Value > tn || kv.Value == tn && kv.Key < top) { tn = kv.Value; top = kv.Key; }
                    notes.Add(Cap(Plural(bands, "band was", "bands were")) + " raised and " + Plural(raids, "raid", "raids") + " made" +
                              (tn >= 2 ? ", the most between " + Name(w, (int)(top >> 32)) + " and " + Name(w, (int)(top & 0xFFFFFFFFL)) + " (" + Num(tn) + ")" : "") +
                              (called > 0 ? "; " + Plural(called, "village answered its lord's call", "villages answered their lords' calls") : "") + ".");
                }
            }
            // the hungry and the sick
            {
                var famine = new HashSet<int>(); var rations = new HashSet<int>(); var sick = new HashSet<int>(); var failed = new HashSet<int>(); int relief = 0, fires = 0, burnt = 0;
                foreach (var e in evs)
                {
                    if (e.Kind == HistoryKind.Famine || e.Kind == HistoryKind.Hunger) famine.Add(e.Settlement);
                    else if (e.Kind == HistoryKind.Rationing) rations.Add(e.Settlement);
                    else if (e.Kind == HistoryKind.EpidemicArrived) sick.Add(e.Settlement);
                    else if (e.Kind == HistoryKind.ReliefSent) relief++;
                    else if (e.Kind == HistoryKind.HarvestFailed) failed.Add(e.Settlement);
                    else if (e.Kind == HistoryKind.Wildfire) fires++;
                    else if (e.Kind == HistoryKind.BuildingBurned) burnt++;
                }
                var parts = new List<string>();
                if (failed.Count > 0) parts.Add("the harvest failed in " + Plural(failed.Count, "village", "villages"));
                if (famine.Count > 0) parts.Add("famine came to " + Plural(famine.Count, "village", "villages"));
                if (rations.Count > 0) parts.Add(Plural(rations.Count, "village rationed its grain", "villages rationed their grain"));
                if (sick.Count > 0) parts.Add("sickness reached " + Plural(sick.Count, "village", "villages"));
                if (relief > 0) parts.Add(Plural(relief, "gift", "gifts") + " of grain went to the hungry");
                if (fires > 0) parts.Add(Plural(fires, "wildfire", "wildfires") + " burned in the wild");
                if (burnt > 0) parts.Add(Plural(burnt, "home was", "homes were") + " burned");
                if (parts.Count > 0) notes.Add(Cap(Join(parts)) + ".");
            }
            // who led, who left, boats and roads
            {
                int heads = 0, splits = 0, moved = 0, boats = 0, roads = 0, sworn = 0, ore = 0, stone = 0, known = 0;
                foreach (var e in evs)
                    switch (e.Kind)
                    {
                        case HistoryKind.HeadmanRose: heads++; break;
                        case HistoryKind.VillageSplit: splits++; break;
                        case HistoryKind.HouseholdMoved: moved++; break;
                        case HistoryKind.BoatBuilt: case HistoryKind.BoatToCross: boats++; break;
                        case HistoryKind.WayWorn: if (e.ArgA >= 3) roads++; break;
                        case HistoryKind.Sworn: sworn++; break;
                        case HistoryKind.OreFound: ore++; break;
                        case HistoryKind.StoneFound: stone++; break;
                        case HistoryKind.TechniqueKnown: known++; break;
                    }
                var parts = new List<string>();
                if (splits > 0) parts.Add(Plural(splits, "village split", "villages split") + " over who should lead");
                if (known > 0) parts.Add(Plural(known, "village learned a craft", "villages learned crafts") + " from their neighbours");
                if (boats > 0) parts.Add(Plural(boats, "boat was", "boats were") + " built");
                if (roads > 0) parts.Add(Plural(roads, "way was", "ways were") + " beaten into roads");
                if (sworn > 0) parts.Add(Plural(sworn, "man swore", "men swore") + " to the first of their villages");
                if (ore > 0 || stone > 0) parts.Add((ore > 0 ? Plural(ore, "body of ore", "bodies of ore") : "") + (ore > 0 && stone > 0 ? " and " : "") + (stone > 0 ? Plural(stone, "outcrop of stone", "outcrops of stone") : "") + " found");
                if (moved > 0) parts.Add(Plural(moved, "household moved", "households moved") + " to another village");
                if (parts.Count > 0) notes.Add(Cap(Join(parts)) + ".");
            }
            return notes;
        }

        static string Article(string noun) => (noun.Length > 0 && "aeiou".IndexOf(noun[0]) >= 0 ? "an " : "a ") + noun;
        static string PluralOf(string noun)
        {
            int sp = noun.LastIndexOf(' ');
            string head = sp >= 0 ? noun.Substring(0, sp + 1) : "", last = sp >= 0 ? noun.Substring(sp + 1) : noun;
            if (last.EndsWith("s")) return noun;
            if (last.EndsWith("y") && last.Length > 1 && "aeiou".IndexOf(last[last.Length - 2]) < 0) return head + last.Substring(0, last.Length - 1) + "ies";
            if (last.EndsWith("sh") || last.EndsWith("ch") || last.EndsWith("x")) return head + last + "es";
            return head + last + "s";
        }

        /// <summary>
        /// (session 27) A record's weight in the story: 2 a great event - a new sickness, a craft first found, the end of a place, a bow that made a
        /// realm of ten or more, walls of stone finished, the earth's and the sky's great violence, your hand -, 1 a notable record, 0 a lesser one;
        /// otherwise by the ledger's importance (its kind's base and size, raised by what it led to). Reads only.
        /// </summary>
        public static byte Weigh(World w, in HistoryEvent e)
        {
            switch (e.Kind)
            {
                case HistoryKind.GodCommand: case HistoryKind.PathogenAppeared: case HistoryKind.TechniqueFound: case HistoryKind.MeteorStruck: case HistoryKind.Tsunami:
                case HistoryKind.IceAge: case HistoryKind.IceAgeEnded: case HistoryKind.Extinction: case HistoryKind.Speciation: case HistoryKind.IslandRose:
                case HistoryKind.SettlementAbandoned: case HistoryKind.WorldCreated:
                    return 2;
                case HistoryKind.PeopleArrived: return 1;   // (its place's founding, a moment later, carries the weight)
                case HistoryKind.Eruption: return (byte)(e.Magnitude >= 3f ? 2 : 1);
                case HistoryKind.Quake: return (byte)(e.Magnitude >= 6.5f ? 2 : e.Magnitude >= 5.5f ? 1 : 0);
                case HistoryKind.StormLandfall: case HistoryKind.StormSurge: return (byte)(e.Importance >= 20f ? 2 : 1);
                case HistoryKind.WallBuilt: return (byte)(e.ArgB == 2 ? 2 : 1);
                case HistoryKind.TributeAgreed: return (byte)(e.ArgA >= 10 ? 1 : 0);   // (a realm's rise and height are told in its age's story)
                case HistoryKind.SettlementFounded:
                    {
                        int sid = e.Subject.Kind == EntityKind.Settlement ? e.Subject.Id : 0;
                        return (byte)(sid != 0 && w.Settlements.Exists(sid) && w.Settlements.Get(sid).Inhabited && w.Settlements.Get(sid).Population >= 380 ? 2 : 1);   // (the founding of a great town of today)
                    }
                case HistoryKind.EpidemicEnded: return (byte)(e.Magnitude >= 100f ? 2 : 1);
                case HistoryKind.Famine: case HistoryKind.EpidemicArrived: case HistoryKind.VillageSplit: case HistoryKind.TributeEnded: case HistoryKind.Wildfire: case HistoryKind.HarvestFailed:
                case HistoryKind.HomesDrowned: case HistoryKind.CoastChanged: case HistoryKind.OreFound: case HistoryKind.OreRevealed: case HistoryKind.ArmsMade:
                    return 1;
                case HistoryKind.Raid: case HistoryKind.BandRaised: case HistoryKind.HeadmanRose: case HistoryKind.WorkBuilt: case HistoryKind.PartySetOut: case HistoryKind.WayWorn:
                case HistoryKind.Taken: case HistoryKind.TakingSeen: case HistoryKind.Struck: case HistoryKind.ClaimRefused: case HistoryKind.AmendsMade: case HistoryKind.FireStarted:
                case HistoryKind.PersonDied: case HistoryKind.PersonBorn: case HistoryKind.Marriage: case HistoryKind.HouseholdFormed:   // (lives are told in Lives)
                    return 0;
                default: return (byte)(e.Importance >= 40f ? 1 : 0);
            }
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
            var ch = new BookChapter { Title = "The Realm of " + seat, Sub = Span(r.From, r.To, present) + "; at its height " + Plural(r.Peak + 1, "village", "villages") + " (year " + r.PeakYear + ")", Settlement = r.Seat, MapSeat = r.Seat, MapYear = r.PeakYear };
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
            int made = 0, bread = 0, asked = 0, endedWeak = 0, endedGone = 0, endedDeath = 0, endedEmpty = 0;
            foreach (var b in mine) { made++; if (b.How == 1) bread++; else if (b.How == 2) asked++; if (b.Ended != 0) { if (b.EndHow == 1) endedGone++; else if (b.EndHow == 2) endedDeath++; else if (b.EndHow == 3) endedEmpty++; else endedWeak++; } }
            sb.Append(Cap(Plural(made, "bond", "bonds"))).Append(" were made with ").Append(seat).Append(" itself");
            var hows = new List<string>();
            if (made - bread - asked > 0) hows.Add(Num(made - bread - asked) + " after its bands came");
            if (bread > 0) hows.Add(Num(bread) + " for famine grain");
            if (asked > 0) hows.Add(Num(asked) + " when it asked");
            if (hows.Count > 0) sb.Append(" (").Append(string.Join(", ", hows.ToArray())).Append(')');
            sb.Append("; ").Append(Num(endedWeak)).Append(" ended when it was no longer the stronger").Append(endedDeath > 0 ? ", " + Num(endedDeath) + " lapsed at the deaths of the first among its people" : "").Append(endedGone > 0 ? ", " + Num(endedGone) + " when it was gone" : "").Append(endedEmpty > 0 ? ", " + Num(endedEmpty) + (endedEmpty == 1 ? " when the village paying it was abandoned" : " when the villages paying it were abandoned") : "").Append('.');
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
            // (C10.9) a village whose bond lapsed at a lord's death and that bowed again is told once - its first bow a line, its later bows counted
            // below; the lapses are counted in the paragraph above, not listed
            var firstBow = new HashSet<int>();
            var again = new Dictionary<int, List<int>>();
            var againOrder = new List<int>();
            foreach (var b in mine)
            {
                if (w.History.TryGet(b.Made, out var e1))
                {
                    if (firstBow.Add(b.Payer)) evs.Add(e1);
                    else { if (!again.TryGetValue(b.Payer, out var yl)) { again[b.Payer] = yl = new List<int>(); againOrder.Add(b.Payer); } yl.Add(YearOf(e1.Time)); }
                }
                if (b.Ended != 0 && b.EndHow != 2 && w.History.TryGet(b.Ended, out var e2)) evs.Add(e2);
            }
            // (C10.8) the askings its seat made that were refused - a realm's story is also the tenths it was denied
            if (c.At.TryGetValue(r.Seat, out var _))
                foreach (var kv in c.Refusals) if (kv.Subject.Id == r.Seat) evs.Add(kv);
            Best(evs, 14);
            foreach (var e in evs) ch.Line(w, e);
            if (againOrder.Count > 0)
            {
                againOrder.Sort((a, b) => again[b].Count != again[a].Count ? again[b].Count.CompareTo(again[a].Count) : a.CompareTo(b));
                var parts = new List<string>();
                for (int i = 0; i < againOrder.Count && i < 6; i++)
                {
                    var yl = again[againOrder[i]];
                    parts.Add(Name(w, againOrder[i]) + " " + (yl.Count == 1 ? "once more (year " + yl[0] + ")" : Num(yl.Count) + " times more (" + Years(yl[0], yl[yl.Count - 1]) + ")"));
                }
                ch.Para("Won back after their bonds lapsed, these bowed again to " + seat + ": " + Join(parts) +
                        (againOrder.Count > 6 ? ", and " + Plural(againOrder.Count - 6, "village", "villages") + " more" : "") + ".", r.Seat);
            }
            // (C11.1b) its houses today: how many of the villages paying it build as its seat's first household does - its way taken up, a way at a
            // time, by the first households of the villages that pay it, and from them by their own people
            if (present && !TC2.Core.Places.Architecture.WaysOff && w.Settlements.Exists(r.Seat))
            {
                ref var ss = ref w.Settlements.Get(r.Seat);
                if (ss.HeadHh != 0 && w.Households.Exists(ss.HeadHh))
                {
                    int sway = TC2.Core.Places.Architecture.WayOf(w, ss.HeadHh, in ss);
                    int pay = 0, alike = 0, asFounders = 0;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        if (sid == r.Seat) continue;
                        ref var ts = ref w.Settlements.Get(sid);
                        if (!ts.Inhabited || ts.HeadHh == 0 || !w.Households.Exists(ts.HeadHh) || !w.Conflict.Under(sid, r.Seat)) continue;
                        pay++;
                        int tw = TC2.Core.Places.Architecture.WayOf(w, ts.HeadHh, in ts);
                        if (TC2.Core.Places.Architecture.Alike(tw, sway)) alike++;
                        else if (TC2.Core.Places.Architecture.Alike(tw, ts.Tradition)) asFounders++;
                    }
                    if (pay > 0)
                    {
                        ch.Heading("Its houses");
                        ch.Para("The first house of " + seat + " raises " + TC2.Core.Places.Architecture.RoofName(TC2.Core.Places.Architecture.TraditionRoof(sway)) + " roofs. Of the " +
                                Plural(pay, "village that pays", "villages that pay") + " it today, the first houses of " + Num(alike) + " build as it does" +
                                (asFounders > 0 ? ", " + Num(asFounders) + " still as their own founders did" : "") + ".", r.Seat);
                    }
                }
            }
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
            var peoples = Peoples(c);   // (C11.1: the peoples - the villages of one line, how they speak and build)
            if (peoples != null) part.Chapters.Add(peoples);
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

        /// <summary>
        /// The peoples (C11.1): the villages descended from one first village (their building tradition's line, D2-114), the most first - how many,
        /// from when, the endings their tongue gives their places (each village's own, C11.1a) and the roofs they raise; its lines the foundings of its
        /// villages on record. Null when the world's names come from one speech (`--oldnames`) or no people has founded another village.
        /// </summary>
        static BookChapter Peoples(Ctx c)
        {
            var w = c.W;
            var lines = new Dictionary<int, List<int>>();
            var order = new List<int>();
            bool spoken = false;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (s.Tongue != 0) spoken = true;
                int l = TC2.Core.Places.Architecture.Line(s.Tradition);
                if (!lines.TryGetValue(l, out var ll)) { lines[l] = ll = new List<int>(); order.Add(l); }
                ll.Add(sid);
            }
            if (!spoken) return null;
            order.RemoveAll(l => lines[l].Count < 2);
            if (order.Count == 0) return null;
            order.Sort((a, b) => lines[b].Count != lines[a].Count ? lines[b].Count.CompareTo(lines[a].Count) : a.CompareTo(b));
            var ch = new BookChapter { Title = "The Peoples", Sub = Plural(order.Count, "people of more than one village", "peoples of more than one village") };
            ch.Para("A people here is the villages that descend, founding by founding, from one of the first: their founders carried its way of building and " +
                    "its speech, each now and then a little changed.");
            foreach (int l in order)
            {
                var ll = lines[l];
                int living = 0;
                var ends = new Dictionary<string, int>(); var endOrder = new List<string>();
                var roofs = new int[3];
                int y0 = int.MaxValue, y1 = 0;
                foreach (int sid in ll)
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.Inhabited) living++;
                    int fy = YearOf(s.Founded); y0 = System.Math.Min(y0, fy); y1 = System.Math.Max(y1, fy);
                    // (C11.1b) the roofs its homes stand under today - its households build as they were raised to; before, as their founders did
                    if (TC2.Core.Places.Architecture.WaysOff || !s.Inhabited) roofs[TC2.Core.Places.Architecture.TraditionRoof(s.Tradition)]++;
                    else
                    {
                        var bl = w.BuildingsOf(sid);
                        for (int i = 0; i < bl.Count; i++)
                        {
                            ref var b = ref w.Buildings.Get(bl[i]);
                            if (TC2.Core.Places.Structures.IsHome(b.Kind) && TC2.Core.Places.Architecture.Has(b.Form) && b.Left == 0) roofs[TC2.Core.Places.Architecture.RoofOf(b.Form)]++;
                        }
                    }
                    foreach (var e in TC2.Core.Worldgen.Names.EndsOf(s.Tongue)) { if (!ends.ContainsKey(e)) { ends[e] = 0; endOrder.Add(e); } ends[e]++; }
                }
                endOrder.Sort((a, b) => ends[b] != ends[a] ? ends[b].CompareTo(ends[a]) : string.CompareOrdinal(a, b));
                var shown = new List<string>();
                for (int i = 0; i < endOrder.Count && i < 5; i++) shown.Add("-" + endOrder[i]);
                int roof = roofs[0] >= roofs[1] && roofs[0] >= roofs[2] ? 0 : roofs[1] >= roofs[2] ? 1 : 2;
                string first = Name(w, l);
                ch.Heading("The people of " + first, l);
                ch.Para(Cap(Plural(ll.Count, "village", "villages")) + " founded " + Years(y0, y1) + ", " + Plural(living, "lived in", "lived in") + " today; they name their places " +
                        Join(shown) + ", and most raise " + TC2.Core.Places.Architecture.RoofName(roof) + " roofs.", l);
                // its foundings on record, the earliest first
                var evs = new List<HistoryEvent>();
                foreach (int sid in ll)
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.FoundedEvent != 0 && w.History.TryGet(s.FoundedEvent, out var fe)) evs.Add(fe);
                }
                evs.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Id.CompareTo(b.Id));
                for (int i = 0; i < evs.Count && i < 5; i++) ch.Line(w, evs[i]);
            }
            return ch;
        }

        static string Join(List<string> l) => l.Count == 0 ? "" : l.Count == 1 ? l[0] : string.Join(", ", l.GetRange(0, l.Count - 1).ToArray()) + " and " + l[l.Count - 1];

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
            int famines = 0, epidemics = 0, raidsOn = 0, raidsBy = 0, splits = 0, walled = -1, stoned = -1;
            var all = c.Of(sid);
            foreach (var e in all)
            {
                if (e.Kind == HistoryKind.Famine && e.Settlement == sid) famines++;
                else if (e.Kind == HistoryKind.EpidemicArrived && e.Settlement == sid) epidemics++;
                else if (e.Kind == HistoryKind.Raid) { if (e.Object.Id == sid) raidsOn++; else if (e.Subject.Id == sid) raidsBy++; }
                else if (e.Kind == HistoryKind.WallBuilt && e.Settlement == sid && e.ArgB != 2 && walled < 0) walled = YearOf(e.Time);
                else if (e.Kind == HistoryKind.WallBuilt && e.Settlement == sid && e.ArgB == 2 && stoned < 0) stoned = YearOf(e.Time);   // (C7.2)
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
            if (stoned >= 0) sb.Append("Its walls were finished in stone in year ").Append(stoned).Append(". ");
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
                case HistoryKind.WallPlanned: case HistoryKind.WallBuilt: return (e.ArgB == 2 ? "the stone walls of " : "the palisade of ") + at;
                case HistoryKind.WorkBuilt: case HistoryKind.WorkBegun: return "a work at " + at;
                case HistoryKind.Sworn: return "men sworn at " + at;
                case HistoryKind.PersonDied: return "the death of " + w.History.NameOf(e.Subject);
                case HistoryKind.HouseholdMoved: return "the " + House(w, e.Subject) + "' leaving";
                default: return "year " + YearOf(e.Time) + (at.Length > 0 ? " at " + at : "");
            }
        }

        // ================================================================== IX. your actions (F9.2)
        /// <summary>
        /// (F9.2) What you did and what came of it: each of your casts (its `GodCommand` record), newest first, and the records whose causes run back to it
        /// (the ledger's `Root`) - counted by what they were (the dead, the fires, homes and fields lost, the earth's and the sky's work) and the weightiest
        /// told in order. Nothing here that the ledger does not hold.
        /// </summary>
        static BookPart YourActions(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "Your Actions", Intro = "What you did to the world, and what came of it: every record whose causes run back to your hand." };
            int n = w.History.Count;
            var casts = new List<int>();
            var byRoot = new Dictionary<long, List<int>>();
            for (int i = 0; i < n; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand) { casts.Add(i); continue; }
                if (e.RootKind != RootKind.You || e.Root == 0) continue;
                if (!byRoot.TryGetValue(e.Root, out var l)) byRoot[e.Root] = l = new List<int>();
                l.Add(i);
            }
            if (casts.Count == 0)
            {
                var none = new BookChapter { Title = "Nothing yet" };
                none.Para("You have not reached into the world. Its history so far is its own.");
                part.Chapters.Add(none);
                return part;
            }
            var counts = new Dictionary<HistoryKind, int>();
            var order = new List<HistoryKind>();
            var best = new List<int>();
            for (int k = casts.Count - 1; k >= 0 && part.Chapters.Count < 40; k--)
            {
                var ce = w.History.At(casts[k]);
                string what = HistoryText.Describe(w, in ce);
                if (what.StartsWith("You ", System.StringComparison.Ordinal)) what = char.ToUpperInvariant(what[4]) + what.Substring(5);
                var ch = new BookChapter { Title = what.TrimEnd('.'), Sub = "year " + YearOf(ce.Time), Settlement = ce.Settlement };
                ch.Line(w, in ce);
                byRoot.TryGetValue(ce.Id, out var fol);
                if (fol == null || fol.Count == 0) { ch.Para("Nothing has come of it that the world remembers."); part.Chapters.Add(ch); continue; }
                counts.Clear(); order.Clear();
                for (int j = 0; j < fol.Count; j++) { var e = w.History.At(fol[j]); if (!counts.TryGetValue(e.Kind, out int m)) order.Add(e.Kind); counts[e.Kind] = m + 1; }
                var parts = new List<string>();
                foreach (var kind in order) { string s = Followed(kind, counts[kind]); if (s != null) parts.Add(s); }
                int last = YearOf(w.History.At(fol[fol.Count - 1]).Time);
                ch.Para("What came of it" + (last > YearOf(ce.Time) ? ", to year " + last : "") + ": " + (parts.Count > 0 ? Join(parts) : Plural(fol.Count, "record", "records")) + ".", ce.Settlement);
                // the weightiest of what followed, told in order
                best.Clear(); best.AddRange(fol);
                best.Sort((p, q) => { int x = w.History.At(q).Importance.CompareTo(w.History.At(p).Importance); return x != 0 ? x : p.CompareTo(q); });
                if (best.Count > 12) best.RemoveRange(12, best.Count - 12);
                best.Sort();
                for (int j = 0; j < best.Count; j++) ch.Line(w, w.History.At(best[j]));
                if (fol.Count > best.Count) ch.Fact("... and " + Plural(fol.Count - best.Count, "record", "records") + " more.");
                part.Chapters.Add(ch);
            }
            return part;
        }

        /// <summary>
        /// (session 27: the powers as play) What has come so far of one of your casts, in a few words - the records since it whose causes run back to it,
        /// counted by what they were as Your Actions counts them ("2 fires, 1 home burned"); "" while nothing has. For the HUD's "your hand", read
        /// once a second: the ledger from the cast on (it is kept in time order), never written.
        /// </summary>
        public static string CameOf(World w, long castId, out int count)
        {
            count = 0;
            var h = w.History;
            if (!h.TryGet(castId, out var cast)) return "";
            int lo = 0, hi = h.Count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (h.At(mid).Time < cast.Time) lo = mid + 1; else hi = mid; }
            var counts = new Dictionary<HistoryKind, int>();
            var order = new List<HistoryKind>();
            for (int i = lo; i < h.Count; i++)
            {
                var e = h.At(i);
                if (e.RootKind != RootKind.You || e.Root != castId || e.Id == castId) continue;
                count++;
                if (!counts.TryGetValue(e.Kind, out int m)) order.Add(e.Kind);
                counts[e.Kind] = m + 1;
            }
            var parts = new List<string>();
            foreach (var kind in order) { string s = Followed(kind, counts[kind]); if (s != null) parts.Add(s); }
            return parts.Count > 0 ? Join(parts) : count > 0 ? Plural(count, "record", "records") : "";
        }

        /// <summary>What a kind of record is, counted, in "What came of it" (null: not told by count).</summary>
        static string Followed(HistoryKind k, int n)
        {
            switch (k)
            {
                case HistoryKind.PersonDied: return Plural(n, "death", "deaths");
                case HistoryKind.FireStarted: return Plural(n, "fire", "fires");
                case HistoryKind.Wildfire: return Plural(n, "wildfire", "wildfires");
                case HistoryKind.FieldsBurned: return Plural(n, "village's fields burned", "villages' fields burned");
                case HistoryKind.BuildingBurned: return Plural(n, "home burned", "homes burned");
                case HistoryKind.HomesDrowned: return Plural(n, "village's homes drowned", "villages' homes drowned");
                case HistoryKind.CropsFlattened: return Plural(n, "village's crop flattened", "villages' crops flattened");
                case HistoryKind.CropsBlighted: return Plural(n, "village's crop blighted", "villages' crops blighted");
                case HistoryKind.MeteorStruck: return Plural(n, "stone from the sky", "stones from the sky");
                case HistoryKind.Drought: return Plural(n, "drought", "droughts");
                case HistoryKind.RainBrought: return Plural(n, "rain brought", "rains brought");
                case HistoryKind.OreRevealed: return Plural(n, "vein laid bare", "veins laid bare");
                case HistoryKind.SoilRenewed: return Plural(n, "village's fields renewed", "villages' fields renewed");
                case HistoryKind.WarmAir: return Plural(n, "warm summer", "warm summers");
                case HistoryKind.ColdAir: return Plural(n, "cold summer", "cold summers");
                case HistoryKind.BeastsCulled: return Plural(n, "kind of beast culled", "kinds of beast culled");
                case HistoryKind.BeastsBrought: return Plural(n, "kind of beast brought", "kinds of beast brought");
                case HistoryKind.FireOut: return Plural(n, "fire put out", "fires put out");
                case HistoryKind.PathogenAppeared: return Plural(n, "new sickness", "new sicknesses");
                case HistoryKind.EpidemicArrived: return Plural(n, "village struck by sickness", "villages struck by sickness");
                case HistoryKind.StormLandfall: return Plural(n, "landfall", "landfalls");
                case HistoryKind.StormSurge: return Plural(n, "surge from the sea", "surges from the sea");
                case HistoryKind.Quake: return Plural(n, "quake", "quakes");
                case HistoryKind.Landslides: return Plural(n, "landslide", "landslides");
                case HistoryKind.Tsunami: return Plural(n, "great wave", "great waves");
                case HistoryKind.Eruption: return Plural(n, "eruption", "eruptions");
                case HistoryKind.AshFall: return Plural(n, "fall of ash", "falls of ash");
                case HistoryKind.VentBorn: return Plural(n, "new vent", "new vents");
                case HistoryKind.StoresSpoiled: return Plural(n, "store spoiled", "stores spoiled");
                case HistoryKind.HarvestFailed: return Plural(n, "failed harvest", "failed harvests");
                case HistoryKind.Hunger: case HistoryKind.Famine: return Plural(n, "time of hunger", "times of hunger");
                case HistoryKind.SettlementAbandoned: return Plural(n, "village abandoned", "villages abandoned");
                case HistoryKind.PartySetOut: return Plural(n, "party setting out", "parties setting out");
                case HistoryKind.HouseholdMoved: return Plural(n, "household leaving", "households leaving");
                case HistoryKind.SettlementFounded: return Plural(n, "village founded", "villages founded");
                default: return null;
            }
        }

        // ================================================================== IX. discoveries and works (session 27)
        /// <summary>
        /// (session 27) Discoveries and Works: each craft from its first finding - who found it, where, when - to how often it was found again,
        /// how many villages learned it from others and how many know it today; then the world's works - the first of each kind raised, how many
        /// were raised and how many stand - its walls of timber and stone, its boats and its roads. Counts and names from the ledger and the world.
        /// </summary>
        static BookPart Works(Ctx c)
        {
            var w = c.W;
            var part = new BookPart { Title = "Discoveries and Works", Intro = "What people learned to do and what they raised: each craft from its first finding to the villages that know it now, and the world's works - its stores and smithies, kilns and markets, mines, walls, boats and roads." };
            int n = w.History.Count;
            // the crafts
            {
                var ch = new BookChapter { Title = "Crafts Found", Sub = "each craft from its first finding" };
                int crafts = Knowledge.Tech.Count;
                var firstFound = new HistoryEvent[crafts]; var hasFirst = new bool[crafts];
                var foundN = new int[crafts]; var learnedN = new int[crafts]; var lostN = new int[crafts];
                for (int i = 0; i < n; i++)
                {
                    var e = w.History.At(i);
                    int t = e.ArgA;
                    if (t < 0 || t >= crafts) continue;
                    if (e.Kind == HistoryKind.TechniqueFound) { foundN[t]++; if (!hasFirst[t]) { hasFirst[t] = true; firstFound[t] = e; } }
                    else if (e.Kind == HistoryKind.TechniqueKnown && !e.Object.IsNone) learnedN[t]++;
                    else if (e.Kind == HistoryKind.TechniqueLost) lostN[t]++;
                }
                var knowNow = new int[crafts]; int living = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (!s.Inhabited) continue;
                    living++;
                    for (int t = 0; t < crafts; t++) if ((s.TechKnown & (1 << t)) != 0) knowNow[t]++;
                }
                var order = new List<int>();
                for (int t = 0; t < crafts; t++) if (hasFirst[t] || knowNow[t] > 0) order.Add(t);
                order.Sort((p, q) => hasFirst[p] != hasFirst[q] ? (hasFirst[p] ? 1 : -1) : hasFirst[p] ? firstFound[p].Id.CompareTo(firstFound[q].Id) : p.CompareTo(q));
                ch.Para(order.Count == 0 ? "No craft has yet been found that the ledger keeps." :
                        "A craft is found by practice - a fisher who learns to knot a net, a smith who learns to smelt - and spreads as neighbours teach each other. These are the world's crafts: those the first settlers brought, then those found since, in the order they were found; " +
                        Plural(living, "village is", "villages are") + " lived in today.");
                foreach (int t in order)
                {
                    var law = Knowledge.Tech.Laws[t];
                    ch.Heading(Cap(law.Name));
                    var sb = new StringBuilder();
                    if (hasFirst[t])
                    {
                        var fe = firstFound[t];
                        sb.Append("First found in year ").Append(YearOf(fe.Time)).Append(fe.Settlement > 0 ? " at " + Name(w, fe.Settlement) : "").Append(fe.Subject.Kind == EntityKind.Person ? ", by " + w.History.NameOf(fe.Subject) : "").Append('.');
                        if (foundN[t] > 1) sb.Append(" Found again ").Append(foundN[t] - 1 == 1 ? "once" : Num(foundN[t] - 1) + " times").Append(" elsewhere.");
                    }
                    else sb.Append("Brought by the first settlers.");
                    if (learnedN[t] > 0) sb.Append(' ').Append(Cap(Plural(learnedN[t], "village", "villages"))).Append(" learned it from neighbours.");
                    if (lostN[t] > 0) sb.Append(' ').Append(Cap(Plural(lostN[t], "village", "villages"))).Append(" lost it again.");
                    sb.Append(" Today the people of ").Append(knowNow[t] == living && living > 1 ? "every village " : Plural(knowNow[t], "village", "villages") + " of " + F(living) + " ").Append(law.Now).Append('.');
                    ch.Para(sb.ToString(), hasFirst[t] ? firstFound[t].Settlement : 0);
                    if (hasFirst[t]) ch.Line(w, in firstFound[t]);
                }
                part.Chapters.Add(ch);
            }
            // the works: the first of each kind, how many raised, how many stand
            {
                var ch = new BookChapter { Title = "Works Raised", Sub = "the first of each kind, and how many stand" };
                var first = new Dictionary<int, HistoryEvent>(); var raised = new Dictionary<int, int>(); var kinds = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.WorkBuilt) continue;
                    if (!first.ContainsKey(e.ArgA)) { first[e.ArgA] = e; kinds.Add(e.ArgA); }
                    raised.TryGetValue(e.ArgA, out int r); raised[e.ArgA] = r + 1;
                }
                var standing = new Dictionary<int, int>();
                for (int b = w.Buildings.First(); b != 0; b = w.Buildings.Next(b))
                {
                    ref var bb = ref w.Buildings.Get(b);
                    if (bb.Done == 0 || TC2.Core.Places.Structures.IsHome(bb.Kind) || !w.Settlements.Exists(bb.Settlement) || !w.Settlements.Get(bb.Settlement).Inhabited) continue;
                    standing.TryGetValue(bb.Kind, out int s); standing[bb.Kind] = s + 1;
                }
                kinds.Sort((p, q) => first[p].Id.CompareTo(first[q].Id));
                ch.Para(kinds.Count == 0 ? "No work has yet been raised that the ledger keeps." :
                        "A village raises a work when its need is plain - a granary when its grain will not keep, a smithy when its smiths want one, a market where traders come often. The first of each kind, in the order they were raised:");
                foreach (int k in kinds)
                {
                    string name = TC2.Core.Places.Structures.Name((byte)k);
                    standing.TryGetValue(k, out int st);
                    var fe = first[k];
                    ch.Note(Cap(PluralOf(name)) + ": the first at " + Name(w, fe.Settlement) + " in year " + YearOf(fe.Time) + "; " + Plural(raised[k], "raised", "raised") + " in all, " + F(st) + " standing in lived-in places today.", fe.Settlement);
                    ch.Line(w, in fe);
                }
                part.Chapters.Add(ch);
            }
            // the walls of timber and stone
            {
                var ch = new BookChapter { Title = "Walls", Sub = "palisades and walls of stone" };
                HistoryEvent firstP = default, firstS = default, greatest = default; bool hasP = false, hasS = false, hasG = false;
                int palisades = 0, stones = 0, fallen = 0;
                for (int i = 0; i < n; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind == HistoryKind.WallFallen) { fallen++; continue; }
                    if (e.Kind != HistoryKind.WallBuilt) continue;
                    if (e.ArgB == 2) { stones++; if (!hasS) { hasS = true; firstS = e; } if (!hasG || e.ArgA > greatest.ArgA) { hasG = true; greatest = e; } }
                    else { palisades++; if (!hasP) { hasP = true; firstP = e; } }
                }
                int walledNow = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (!s.Inhabited || s.Wall == 0) continue;
                    walledNow++;
                }
                if (!hasP && !hasS) ch.Para("No village has walled itself in.");
                else
                {
                    var sb = new StringBuilder();
                    if (hasP) sb.Append(Cap(Plural(palisades, "palisade was", "palisades were"))).Append(" finished in the world's years, the first at ").Append(Name(w, firstP.Settlement)).Append(" in year ").Append(YearOf(firstP.Time)).Append(". ");
                    if (fallen > 0) sb.Append(Cap(Plural(fallen, "palisade", "palisades"))).Append(" rotted and fell. ");
                    if (hasS) sb.Append(Cap(Plural(stones, "ring was", "rings were"))).Append(" walled in stone, the first at ").Append(Name(w, firstS.Settlement)).Append(" in year ").Append(YearOf(firstS.Time)).Append("; the greatest ").Append(F(greatest.ArgA)).Append(" m round, at ").Append(Name(w, greatest.Settlement)).Append(". ");
                    sb.Append(Cap(Plural(walledNow, "village stands", "villages stand"))).Append(" walled today.");
                    ch.Para(sb.ToString());
                    if (hasP) ch.Line(w, in firstP);
                    if (hasS) ch.Line(w, in firstS);
                    if (hasG && (!hasS || greatest.Id != firstS.Id)) ch.Line(w, in greatest);
                }
                part.Chapters.Add(ch);
            }
            // boats, roads and mines
            {
                var ch = new BookChapter { Title = "Boats, Roads and Mines", Sub = "the ways over land and sea, and the ore" };
                HistoryEvent dug = default, plank = default, road = default, copper = default, iron = default; bool hd = false, hp = false, hr = false, hc = false, hi = false;
                int boats = 0, roads = 0, paths = 0, gaveOut = 0;
                for (int i = 0; i < n; i++)
                {
                    var e = w.History.At(i);
                    switch (e.Kind)
                    {
                        case HistoryKind.BoatBuilt:
                            boats++;
                            if (e.ArgA == TC2.Core.Places.Structures.PlankBoat) { if (!hp) { hp = true; plank = e; } }
                            else if (!hd) { hd = true; dug = e; }
                            break;
                        case HistoryKind.WayWorn: if (e.ArgA >= 3) { roads++; if (!hr) { hr = true; road = e; } } else paths++; break;
                        case HistoryKind.OreFound:
                            if (e.ArgA == TC2.Core.Places.Mining.Iron) { if (!hi) { hi = true; iron = e; } }
                            else if (!hc) { hc = true; copper = e; }
                            break;
                        case HistoryKind.OreGaveOut: gaveOut++; break;
                    }
                }
                var sb = new StringBuilder();
                if (boats > 0) sb.Append(Cap(Plural(boats, "boat was", "boats were"))).Append(" built").Append(hd ? ", the first dugout at " + Name(w, dug.Settlement) + " in year " + YearOf(dug.Time) : "").Append(hp ? ", the first boat of planks at " + Name(w, plank.Settlement) + " in year " + YearOf(plank.Time) : "").Append(". ");
                if (paths + roads > 0) sb.Append(Cap(Plural(paths, "path was", "paths were"))).Append(" worn between villages and ").Append(Plural(roads, "way was", "ways were")).Append(" beaten into roads").Append(hr ? ", the first between " + w.History.NameOf(road.Subject) + " and " + w.History.NameOf(road.Object) + " in year " + YearOf(road.Time) : "").Append(". ");
                if (hc || hi) sb.Append("Ore was first seen ").Append(hc ? "- copper - by the people of " + w.History.NameOf(copper.Subject) + " in year " + YearOf(copper.Time) : "").Append(hc && hi ? "; " : "").Append(hi ? "bog iron by the people of " + w.History.NameOf(iron.Subject) + " in year " + YearOf(iron.Time) : "").Append(gaveOut > 0 ? "; " + Plural(gaveOut, "body was", "bodies were") + " dug out" : "").Append('.');
                ch.Para(sb.Length > 0 ? sb.ToString().TrimEnd() : "No boat, road or mine yet.");
                if (hd) ch.Line(w, in dug);
                if (hp) ch.Line(w, in plank);
                if (hr) ch.Line(w, in road);
                if (hc) ch.Line(w, in copper);
                if (hi) ch.Line(w, in iron);
                part.Chapters.Add(ch);
            }
            return part;
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
                            case BookKind.Note: sb.Append('*').Append(b.Text).Append("*\n\n"); break;
                            case BookKind.Line: sb.Append("- ").Append(b.Weight >= 2 ? "**" + b.Text + "**" : b.Text).Append(b.Weight == 0 ? " _(minor)_" : "").Append(ids ? " `[" + b.Event + "]`" : "").Append('\n'); inList = true; break;
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
