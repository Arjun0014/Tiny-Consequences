using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>
    /// (session 29: the person as a person) A life told as it is lived, for the person page's Life, Family and Overview: the life chronicle - their own
    /// records reworded from their side ("Married Odo Brandr in Fostead", "Found how to make fishing nets, the first in Fostead"), their household's (homes
    /// raised, moves, boats, carts), their kin's births and deaths read from the people themselves (a parent, a partner, a child lost; the first grandchild),
    /// and what their village lived through while they lived in it (famines, plagues and how many died, bows and bonds, walls, the earth's violence);
    /// their family as a tree (grandparents, parents, brothers and sisters, partners with the children of each, grandchildren); and their temperament - the
    /// six inborn leanings the simulation decides by (boldness, sociability, diligence, waking hour, pace, alertness), each told with what it does. Every
    /// line is a record or the state of a person; nothing is invented, and what the ledger has let go is simply not told. Reads only.
    /// </summary>
    public static class Lives
    {
        /// <summary>The kinds of a chronicle's entries (for the page's marks and colours).</summary>
        public const byte Birth = 0, Family = 1, Loss = 2, Home = 3, Work = 4, Standing = 5, Fight = 6, Hardship = 7, Death = 8, Other = 9;

        public struct Entry
        {
            public long Time;
            public int Age;
            public byte Kind, Weight;     // Weight: 2 a turn of their life, 1 notable, 0 small
            public string Text;
            public long Event;            // the record it stands on (0 one read from the people themselves)
            public int Person;            // someone it names, to go to (0 none)
            public int Settlement;        // the place it names, to go to (0 none)
        }

        static string F(double v, string fmt = "0") => v.ToString(fmt, CultureInfo.InvariantCulture);
        static string Name(World w, int pid) => w.History.NameOf(EntityRef.Person(pid));
        static string Place(World w, int sid) => sid != 0 ? w.History.NameOf(EntityRef.Settlement(sid)) : "the road";
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static int AgeAt(in Person p, long t) => (int)System.Math.Max(0.0, (t - p.Born) / (double)SimTime.Year);

        /// <summary>"she"/"he" for a person.</summary>
        static string They(World w, int pid) => w.People.Exists(pid) && w.People.Get(pid).IsFemale ? "she" : "he";

        /// <summary>A record's sentence with its subject's name taken off the front and its verb raised ("X raised a band ..." -> "Raised a band ..."); the
        /// whole sentence when it does not begin with the name.</summary>
        static string Own(World w, in HistoryEvent e, int pid)
        {
            string text = HistoryText.Describe(w, e), name = Name(w, pid);
            if (name.Length > 0 && text.StartsWith(name + " ", System.StringComparison.Ordinal))
            {
                string rest = text.Substring(name.Length + 1);
                if (rest.StartsWith("of ", System.StringComparison.Ordinal)) { int sp = rest.IndexOf(' ', 3); string tail = sp > 0 ? rest.Substring(sp + 1) : rest; if (tail.Length > 0 && char.IsLower(tail[0])) return Cap(tail); }
                if (rest.Length > 0 && char.IsLower(rest[0])) return Cap(rest);
            }
            return text;
        }

        /// <summary>
        /// The life, oldest first. Their own records, their household's since they joined it, their closest kin's births and deaths, and what their village
        /// lived through while they lived there (at most a dozen of those, the weightiest).
        /// </summary>
        public static List<Entry> Chronicle(World w, int pid)
        {
            var list = new List<Entry>();
            if (!w.People.Exists(pid)) return list;
            ref var p = ref w.People.Get(pid);
            long end = p.Alive ? w.Now : p.Died;
            var told = new HashSet<long>();          // records told
            var kinBornTold = new HashSet<int>();    // children whose birth a record told
            var spouses = new List<int>();
            long married = 0;
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs != null)
                for (int i = 0; i < evs.Count; i++)
                {
                    if (!w.History.TryGet(evs[i], out var e) || !told.Add(e.Id)) continue;
                    if (e.Kind == HistoryKind.Marriage) { int other = e.Subject.Id == pid ? e.Object.Id : e.Subject.Id; if (other != 0 && !spouses.Contains(other)) spouses.Add(other); if (married == 0) married = e.Time; }
                    if (e.Kind == HistoryKind.PersonBorn && e.Subject.Id != pid) kinBornTold.Add(e.Subject.Id);
                    if (Mine(w, pid, in p, in e, out var en)) list.Add(en);
                }
            // their household's records since they were of it (born into it, or married into it)
            if (p.Household != 0 && w.Households.Exists(p.Household))
            {
                long joined = married != 0 ? married : p.Born;
                var hev = w.History.EventsOf(EntityRef.Household(p.Household));
                if (hev != null)
                    for (int i = 0; i < hev.Count; i++)
                    {
                        if (!w.History.TryGet(hev[i], out var e) || e.Time < joined || e.Time > end || !told.Add(e.Id)) continue;
                        if (Household(w, in p, in e, out var en)) list.Add(en);
                    }
            }
            // the closest kin, from the people themselves: parents, partners and children lost; children whose birth no record kept; the first grandchild
            if (p.Partner != 0 && !spouses.Contains(p.Partner)) spouses.Add(p.Partner);
            var children = new List<int>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var q = ref w.People.Get(id);
                if (id != pid && (q.Mother == pid || q.Father == pid)) children.Add(id);
            }
            foreach (int c in children)
            {
                ref var q = ref w.People.Get(c);
                int other = q.Mother == pid ? q.Father : q.Mother;
                if (other != 0 && !spouses.Contains(other)) spouses.Add(other);
                if (!kinBornTold.Contains(c) && q.Born >= p.Born && q.Born <= end)
                    list.Add(new Entry { Time = q.Born, Age = AgeAt(in p, q.Born), Kind = Family, Weight = 2, Person = c, Text = (q.IsFemale ? "A daughter, " : "A son, ") + Name(w, c) + ", was born" + (other != 0 ? " to " + (p.IsFemale ? "her" : "him") + " and " + Name(w, other) : "") + "." });
            }
            long pBorn = p.Born;
            string his = p.IsFemale ? "her" : "his";
            void Lost(int kin, string who)
            {
                if (kin == 0 || !w.People.Exists(kin)) return;
                ref var k = ref w.People.Get(kin);
                if (k.Alive || k.Died < pBorn || k.Died > end) return;
                string how = "";
                if (k.DeathEvent != 0 && w.History.TryGet(k.DeathEvent, out var de))
                {
                    string d = HistoryText.Describe(w, de), n = Name(w, kin);
                    how = d.StartsWith(n + " ", System.StringComparison.Ordinal) ? ": " + They(w, kin) + " " + d.Substring(n.Length + 1) : ": " + d;
                }
                list.Add(new Entry { Time = k.Died, Age = (int)System.Math.Max(0.0, (k.Died - pBorn) / (double)SimTime.Year), Kind = Loss, Weight = 2, Person = kin, Event = k.DeathEvent, Text = "Lost " + who + " " + Name(w, kin) + (how.Length > 0 ? how : ".") });
            }
            Lost(p.Mother, his + " mother"); Lost(p.Father, his + " father");
            foreach (int s in spouses) Lost(s, his + (w.People.Get(s).IsFemale ? " wife" : " husband"));
            foreach (int c in children) Lost(c, his + (w.People.Get(c).IsFemale ? " daughter" : " son"));
            int firstGrand = 0; long firstGrandAt = long.MaxValue;
            if (children.Count > 0)
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var q = ref w.People.Get(id);
                    if ((children.Contains(q.Mother) || children.Contains(q.Father)) && q.Born < firstGrandAt) { firstGrandAt = q.Born; firstGrand = id; }
                }
            if (firstGrand != 0 && firstGrandAt <= end)
            {
                ref var g = ref w.People.Get(firstGrand);
                int via = children.Contains(g.Mother) ? g.Mother : g.Father;
                list.Add(new Entry { Time = firstGrandAt, Age = AgeAt(in p, firstGrandAt), Kind = Family, Weight = 1, Person = firstGrand, Text = "Became a grandparent: " + Name(w, firstGrand) + " was born to " + (p.IsFemale ? "her" : "his") + (w.People.Get(via).IsFemale ? " daughter " : " son ") + Name(w, via) + "." });
            }
            // what their village lived through while they lived there
            Village(w, pid, in p, end, married, list);
            // their birth and death when no record of them is kept (the first people of the land were born before its history began; the memory pass lets
            // the ordinary go)
            bool born = false, died = false;
            foreach (var en in list) { if (en.Kind == Birth) born = true; if (en.Kind == Death) died = true; }
            if (!born)
                list.Add(new Entry { Time = p.Born, Age = 0, Kind = Birth, Weight = 2, Person = p.Mother,
                                     Text = p.Born < 0 ? "Born " + F(-p.Born / (double)SimTime.Year) + " years before the land's first year, among the first people who came to it." : "Born" + (p.Mother != 0 ? " to " + Name(w, p.Mother) + (p.Father != 0 ? " and " + Name(w, p.Father) : "") : "") + "." });
            if (!died && !p.Alive) list.Add(new Entry { Time = p.Died, Age = AgeAt(in p, p.Died), Kind = Death, Weight = 2, Text = "Died at " + AgeAt(in p, p.Died) + "." });
            list.Sort((a, b) => { int c = a.Time.CompareTo(b.Time); if (c != 0) return c; c = (a.Kind == Birth ? 0 : 1).CompareTo(b.Kind == Birth ? 0 : 1); return c != 0 ? c : a.Event.CompareTo(b.Event); });
            return Gathered(w, list);
        }

        /// <summary>The same told once: a line repeated within a few years (a claim asked again and again) kept once with how often and over which years;
        /// the men who swore to them in a year named in one line.</summary>
        static List<Entry> Gathered(World w, List<Entry> list)
        {
            var outl = new List<Entry>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.Text == null) continue;
                // the men sworn to them in one year
                int cut = e.Text.IndexOf(" swore to ", System.StringComparison.Ordinal);
                if (e.Kind == Standing && e.Weight == 0 && cut > 0 && e.Text.EndsWith(" household's grain.", System.StringComparison.Ordinal))
                {
                    string tail = e.Text.Substring(cut);
                    var names = new List<string> { e.Text.Substring(0, cut) };
                    long year = SimTime.YearOf(e.Time);
                    int j = i + 1;
                    for (; j < list.Count; j++)
                    {
                        var f = list[j];
                        if (f.Kind != Standing || f.Weight != 0 || SimTime.YearOf(f.Time) != year || f.Text == null || !f.Text.EndsWith(tail, System.StringComparison.Ordinal)) continue;
                        names.Add(f.Text.Substring(0, f.Text.Length - tail.Length));
                        list[j] = new Entry { Time = f.Time, Text = null };
                    }
                    if (names.Count > 1) { e.Text = string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1] + tail; e.Person = 0; e.Weight = 1; }
                    outl.Add(e);
                    continue;
                }
                // a line said again within three years
                int times = 1; long last = e.Time;
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[j].Text == null || list[j].Text != e.Text) continue;
                    if (list[j].Time - last > 3 * SimTime.Year) break;
                    last = list[j].Time; times++;
                    list[j] = new Entry { Time = list[j].Time, Text = null };
                }
                if (times > 1) e.Text = e.Text.TrimEnd('.') + " - " + (times == 2 ? "twice" : times == 3 ? "three times" : times + " times") + (SimTime.YearOf(last) != SimTime.YearOf(e.Time) ? ", to year " + SimTime.YearOf(last) : " that year") + ".";
                outl.Add(e);
            }
            return outl;
        }

        /// <summary>A record of theirs as an entry of their life, told from their side; false for what their life does not need to tell.</summary>
        static bool Mine(World w, int pid, in Person p, in HistoryEvent e, out Entry en)
        {
            en = new Entry { Time = e.Time, Age = AgeAt(in p, e.Time), Event = e.Id, Settlement = e.Settlement, Kind = Other, Weight = 0 };
            bool subj = e.Subject.Kind == EntityKind.Person && e.Subject.Id == pid, obj = e.Object.Kind == EntityKind.Person && e.Object.Id == pid, oth = e.Other.Kind == EntityKind.Person && e.Other.Id == pid;
            string place = Place(w, e.Settlement);
            switch (e.Kind)
            {
                case HistoryKind.PersonBorn:
                    if (subj)
                    {
                        en.Kind = Birth; en.Weight = 2;
                        en.Text = "Born in " + place + (e.Object.IsNone ? ", among the first people of the land." : " to " + Name(w, e.Object.Id) + (e.Other.IsNone ? "" : " and " + Name(w, e.Other.Id)) + ".");
                        return true;
                    }
                    {
                        int child = e.Subject.Id, other = obj ? e.Other.Id : e.Object.Id;
                        bool girl = w.People.Exists(child) && w.People.Get(child).IsFemale;
                        en.Kind = Family; en.Weight = 2; en.Person = child;
                        en.Text = (girl ? "A daughter, " : "A son, ") + Name(w, child) + ", was born to " + (p.IsFemale ? "her" : "him") + (other != 0 ? " and " + Name(w, other) : "") + " in " + place + ".";
                        return true;
                    }
                case HistoryKind.PersonDied:
                    if (!subj) return false;
                    en.Kind = Death; en.Weight = 2; en.Text = Own(w, in e, pid);
                    return true;
                case HistoryKind.Marriage:
                    {
                        int other = subj ? e.Object.Id : e.Subject.Id;
                        en.Kind = Family; en.Weight = 2; en.Person = other;
                        en.Text = "Married " + Name(w, other) + " in " + place + (e.ArgA > 0 ? ", setting up their household with " + HistoryText.Bread(e.ArgA) + " bread of their own" : "") + ".";
                        return true;
                    }
                case HistoryKind.TechniqueFound:
                    if (!subj) return false;
                    en.Kind = Work; en.Weight = 2;
                    en.Text = "Found how to " + (e.ArgA >= 0 && e.ArgA < Knowledge.Tech.Count ? Knowledge.Tech.Laws[e.ArgA].Name : "do it") + " - nobody in " + place + " had known it.";
                    return true;
                case HistoryKind.TechniqueKnown:
                    if (!obj) return false;
                    en.Kind = Work; en.Weight = 1;
                    en.Text = "Was the first in " + place + " to master how to " + (e.ArgA >= 0 && e.ArgA < Knowledge.Tech.Count ? Knowledge.Tech.Laws[e.ArgA].Name : "do it") + ".";
                    return true;
                case HistoryKind.WorkBuilt:
                    if (!oth) return false;
                    en.Kind = Work; en.Weight = 2;
                    en.Text = "Led the raising of the " + Structures.Name((byte)e.ArgA) + " of " + place + ".";
                    return true;
                case HistoryKind.BoatBuilt: case HistoryKind.CartMade:
                    if (!oth) return false;
                    en.Kind = Work; en.Weight = 1; en.Text = HistoryText.Describe(w, e);
                    return true;
                case HistoryKind.StoneFound: case HistoryKind.OreFound:
                    if (!oth) return false;
                    en.Kind = Work; en.Weight = 1; en.Text = HistoryText.Describe(w, e);
                    return true;
                case HistoryKind.Sworn:
                    if (subj) { en.Kind = Standing; en.Weight = 2; en.Person = e.Object.Id; en.Text = Own(w, in e, pid); return true; }
                    if (obj) { en.Kind = Standing; en.Weight = 0; en.Person = e.Subject.Id; en.Text = Name(w, e.Subject.Id) + " swore to " + (p.IsFemale ? "her" : "him") + ", fed from " + (p.IsFemale ? "her" : "his") + " household's grain."; return true; }
                    return false;
                case HistoryKind.SwornReleased:
                    if (!subj) return false;
                    en.Kind = Standing; en.Weight = 1; en.Text = Own(w, in e, pid);
                    return true;
                case HistoryKind.HeadmanRose:
                    if (!subj) return false;
                    en.Kind = Standing; en.Weight = 2; en.Text = Own(w, in e, pid);
                    return true;
                case HistoryKind.VillageSplit:
                    en.Kind = subj ? Home : Standing; en.Weight = (byte)(subj ? 2 : 1); en.Text = subj ? Own(w, in e, pid) : HistoryText.Describe(w, e);
                    return subj || oth;
                case HistoryKind.BandRaised: case HistoryKind.Taken: case HistoryKind.TakingSeen:
                    if (!subj) return false;
                    en.Kind = Fight; en.Weight = (byte)(e.Kind == HistoryKind.BandRaised ? 2 : 1); en.Text = Own(w, in e, pid);
                    return true;
                case HistoryKind.Raid:
                    if (!oth) return false;
                    en.Kind = Fight; en.Weight = 2; en.Text = "Led the raid: " + Cap(HistoryText.Describe(w, e));
                    return true;
                case HistoryKind.Struck:
                    en.Kind = Fight; en.Weight = (byte)(obj ? 2 : 1); en.Person = obj ? e.Subject.Id : e.Object.Id; en.Text = HistoryText.Describe(w, e);
                    return subj || obj;
                case HistoryKind.EpidemicArrived:
                    if (!oth) return false;
                    en.Kind = Hardship; en.Weight = 1; en.Text = HistoryText.Describe(w, e);
                    return true;
                case HistoryKind.AmendsMade: case HistoryKind.ClaimRefused:
                    if (!oth) return false;
                    en.Kind = Standing; en.Weight = 1; en.Text = HistoryText.Describe(w, e);
                    return true;
                case HistoryKind.BoatLost:
                    en.Kind = Hardship; en.Weight = 2; en.Text = HistoryText.Describe(w, e);
                    return oth;
                case HistoryKind.JourneyInterrupted: case HistoryKind.FledDanger: case HistoryKind.JourneyAbandoned: case HistoryKind.DangerNoticed:
                    en.Kind = Hardship; en.Weight = 0; en.Text = HistoryText.Describe(w, e);
                    return true;
                default:
                    en.Weight = 0; en.Text = HistoryText.Describe(w, e);
                    return true;
            }
        }

        /// <summary>A record of their household's, as part of their life (the household's homes, moves, keep, boats and carts).</summary>
        static bool Household(World w, in Person p, in HistoryEvent e, out Entry en)
        {
            en = new Entry { Time = e.Time, Age = AgeAt(in p, e.Time), Event = e.Id, Settlement = e.Settlement, Kind = Home, Weight = 1 };
            switch (e.Kind)
            {
                case HistoryKind.HouseholdMoved: en.Weight = 2; en.Text = "Left " + Place(w, e.Settlement) + " with " + (p.IsFemale ? "her" : "his") + " household for " + w.History.NameOf(e.Object) + "."; return true;
                case HistoryKind.HomeBuilt: en.Text = (p.IsFemale ? "Her" : "His") + " household raised its own " + (e.ArgA == 2 ? "house" : "hut") + " in " + Place(w, e.Settlement) + "."; return true;
                case HistoryKind.HomeTaken: en.Text = (p.IsFemale ? "Her" : "His") + " household made its home in a " + (e.ArgA == 2 ? "house" : "hut") + " that stood empty in " + Place(w, e.Settlement) + "."; return true;
                case HistoryKind.HomeRebuilt: case HistoryKind.Retired: case HistoryKind.TakenIn: case HistoryKind.LetGo: case HistoryKind.BoatBuilt: case HistoryKind.CartMade: case HistoryKind.BoatToCross: case HistoryKind.HomesDrowned:
                    en.Text = HistoryText.Describe(w, e); en.Kind = e.Kind == HistoryKind.BoatBuilt || e.Kind == HistoryKind.CartMade || e.Kind == HistoryKind.BoatToCross ? Work : Home;
                    return true;
                case HistoryKind.BoatLost: en.Kind = Hardship; en.Weight = 2; en.Text = HistoryText.Describe(w, e); return true;
                default: return false;
            }
        }

        /// <summary>What their village lived through while they lived in it: famines, plagues that killed, bows to a lord and bonds ended, walls finished,
        /// the earth's and the sea's violence - the weightiest dozen. "While they lived in it": since their birth if born there and never moved, else since
        /// they came (a marriage there, their household's move there).</summary>
        static void Village(World w, int pid, in Person p, long end, long married, List<Entry> into)
        {
            int home = p.Home;
            if (home == 0 || !w.Settlements.Exists(home)) return;
            long since = -1;
            if (Biography.BornAt(w, pid) == home) since = p.Born;
            if (p.Household != 0 && w.Households.Exists(p.Household))
            {
                var hev = w.History.EventsOf(EntityRef.Household(p.Household));
                if (hev != null)
                    for (int i = 0; i < hev.Count; i++)
                        if (w.History.TryGet(hev[i], out var e) && e.Kind == HistoryKind.HouseholdMoved && e.Object.Kind == EntityKind.Settlement && e.Object.Id == home) since = System.Math.Max(since, e.Time);
            }
            if (since < 0 && married != 0) since = married;
            if (since < 0) return;
            var pev = w.History.RecordsAt(home);
            if (pev == null) return;
            var found = new List<Entry>();
            string place = Place(w, home);
            for (int i = 0; i < pev.Count; i++)
            {
                if (!w.History.TryGet(pev[i], out var e) || e.Time < since || e.Time > end) continue;
                string text = null; byte weight = 1;
                switch (e.Kind)
                {
                    case HistoryKind.Famine: text = "Lived through the famine in " + place + "."; break;
                    case HistoryKind.EpidemicEnded: if (e.Magnitude >= 3f) { text = "Lived through " + w.History.NameOf(e.Object) + " in " + place + ": it killed " + F(e.Magnitude) + " there in " + e.ArgA + " days."; weight = (byte)(e.Magnitude >= 30f ? 2 : 1); } break;
                    case HistoryKind.TributeAgreed: if (e.Subject.Kind == EntityKind.Settlement && e.Subject.Id == home) { text = place + " bowed to " + w.History.NameOf(e.Object) + ": a tenth of every harvest went to it from then on."; } break;
                    case HistoryKind.TributeEnded: if (e.Subject.Kind == EntityKind.Settlement && e.Subject.Id == home) text = HistoryText.Describe(w, e); break;
                    case HistoryKind.WallBuilt: text = HistoryText.Describe(w, e); weight = 0; break;
                    case HistoryKind.Raid: if (e.Object.Kind == EntityKind.Settlement && e.Object.Id == home && e.Magnitude >= 20f) { text = HistoryText.Describe(w, e); weight = 1; } break;
                    case HistoryKind.Quake: case HistoryKind.Eruption: case HistoryKind.Tsunami: case HistoryKind.MeteorStruck: case HistoryKind.StormLandfall: case HistoryKind.HomesDrowned: case HistoryKind.HomesShaken:
                        text = HistoryText.Describe(w, e); weight = (byte)(e.Kind == HistoryKind.MeteorStruck || e.Kind == HistoryKind.Tsunami ? 2 : 1); break;
                    case HistoryKind.HeadmanRose: if (!(e.Subject.Kind == EntityKind.Person && e.Subject.Id == pid)) { text = HistoryText.Describe(w, e); weight = 0; } break;
                }
                if (text == null) continue;
                found.Add(new Entry { Time = e.Time, Age = AgeAt(in p, e.Time), Kind = Hardship, Weight = weight, Text = text, Event = e.Id, Settlement = home });
            }
            // (the weightiest dozen: their own life comes first on their page)
            if (found.Count > 12)
            {
                found.Sort((a, b) => { int c = b.Weight.CompareTo(a.Weight); return c != 0 ? c : a.Time.CompareTo(b.Time); });
                found.RemoveRange(12, found.Count - 12);
            }
            into.AddRange(found);
        }

        // ------------------------------------------------------------------ family

        public sealed class Tree
        {
            public int Person;
            public int MotherMother, MotherFather, FatherMother, FatherFather;
            public int Mother, Father;
            public readonly List<int> Siblings = new List<int>();          // full and half, eldest first
            public readonly List<bool> Half = new List<bool>();
            public readonly List<int> Partners = new List<int>();          // every partner: the married, and the other parent of any child
            public readonly List<List<int>> ChildrenOf = new List<List<int>>();   // each partner's children with them, eldest first (index as Partners); last: children of an unknown other
            public readonly List<int> Grandchildren = new List<int>();
            public int Partner;                                             // the partner now (0 none)
        }

        /// <summary>The family round a person, read from the people (their parents, partners and children as each person keeps them).</summary>
        public static Tree KinOf(World w, int pid)
        {
            var t = new Tree { Person = pid };
            if (!w.People.Exists(pid)) return t;
            ref var p = ref w.People.Get(pid);
            t.Mother = p.Mother; t.Father = p.Father; t.Partner = p.Partner;
            if (p.Mother != 0 && w.People.Exists(p.Mother)) { ref var m = ref w.People.Get(p.Mother); t.MotherMother = m.Mother; t.MotherFather = m.Father; }
            if (p.Father != 0 && w.People.Exists(p.Father)) { ref var f = ref w.People.Get(p.Father); t.FatherMother = f.Mother; t.FatherFather = f.Father; }
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs != null)
                for (int i = 0; i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.Marriage) { int o = e.Subject.Id == pid ? e.Object.Id : e.Subject.Id; if (o != 0 && !t.Partners.Contains(o)) t.Partners.Add(o); }
            if (p.Partner != 0 && !t.Partners.Contains(p.Partner)) t.Partners.Add(p.Partner);
            var children = new List<int>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                if (id == pid) continue;
                ref var q = ref w.People.Get(id);
                if (q.Mother == pid || q.Father == pid) children.Add(id);
                else if ((p.Mother != 0 && q.Mother == p.Mother) || (p.Father != 0 && q.Father == p.Father)) { t.Siblings.Add(id); }
            }
            t.Siblings.Sort((a, b) => w.People.Get(a).Born.CompareTo(w.People.Get(b).Born));
            foreach (int s in t.Siblings) { ref var q = ref w.People.Get(s); t.Half.Add(!(q.Mother == p.Mother && q.Father == p.Father)); }
            children.Sort((a, b) => w.People.Get(a).Born.CompareTo(w.People.Get(b).Born));
            foreach (int c in children)
            {
                ref var q = ref w.People.Get(c);
                int other = q.Mother == pid ? q.Father : q.Mother;
                if (other != 0 && !t.Partners.Contains(other)) t.Partners.Add(other);
            }
            for (int i = 0; i <= t.Partners.Count; i++) t.ChildrenOf.Add(new List<int>());
            foreach (int c in children)
            {
                ref var q = ref w.People.Get(c);
                int other = q.Mother == pid ? q.Father : q.Mother;
                int k = other != 0 ? t.Partners.IndexOf(other) : t.Partners.Count;
                t.ChildrenOf[k < 0 ? t.Partners.Count : k].Add(c);
            }
            if (children.Count > 0)
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var q = ref w.People.Get(id);
                    if ((q.Mother != 0 && children.Contains(q.Mother)) || (q.Father != 0 && children.Contains(q.Father))) t.Grandchildren.Add(id);
                }
            t.Grandchildren.Sort((a, b) => w.People.Get(a).Born.CompareTo(w.People.Get(b).Born));
            return t;
        }

        // ------------------------------------------------------------------ following (session 29)

        /// <summary>
        /// Their household among its village's households (C10.2's followings, read as the village's first is weighed - Society.Leaders): whom it follows and
        /// why (kin; grain it owes them; they fed it in hard times) and who follows it, by the same three bonds. Lines of words; empty for no household.
        /// </summary>
        public static List<string> Following(World w, int pid)
        {
            var l = new List<string>();
            if (Society.Leaders.Off || !w.People.Exists(pid)) return l;
            ref var p = ref w.People.Get(pid);
            int hid = p.Household;
            if (hid == 0 || !w.Households.Exists(hid)) return l;
            int sid = w.Households.Get(hid).Settlement;
            if (!w.Settlements.Exists(sid)) return l;
            var all = Society.Leaders.All(w, sid);
            var kin = Holdings.KinMap(w, sid);
            var hs = w.Holdings;
            long since = w.Now - Society.Leaders.FollowYears * SimTime.Year;
            ref var mine = ref w.Households.Get(hid);
            int patron = mine.Patron; long patronAt = mine.PatronAt;
            // (session 32) households of one family name told once ("the Trilouks (kin); the Trilouks (kin); the Trilouks (kin)" had stood): how many, their bonds
            // together and the grain owed them all; a debt under a day's grain said as such ("owing them 0 days' grain" had stood)
            var byName = new SortedDictionary<string, (int n, bool kin, double fu, bool fed)>(System.StringComparer.Ordinal);
            foreach (var kv in all)
            {
                if (kv.Key == hid || !kv.Value.Contains(hid) || !w.Households.Exists(kv.Key)) continue;
                string fam = HistoryText.Plural(w.History.NameOf(EntityRef.Household(kv.Key)));
                byName.TryGetValue(fam, out var b);
                b.n++;
                if (kin.TryGetValue(kv.Key, out var kl) && kl.Contains(hid)) b.kin = true;
                for (int i = 0; i < hs.DebtCount; i++) { var d = hs.DebtAt(i); if (d.Debtor == hid && d.Creditor == kv.Key) { b.fu += d.Fu; break; } }
                if (patron == kv.Key && patronAt >= since) b.fed = true;
                byName[fam] = b;
            }
            var follows = new List<string>();
            foreach (var kv in byName)
            {
                var b = kv.Value;
                var why = new List<string>();
                if (b.kin) why.Add("kin");
                if (b.fu > 0.0) why.Add("owing them " + (b.fu < 1.0 ? "a little grain" : b.fu < 1.5 ? "a day's grain" : F(b.fu) + " days' grain"));
                if (b.fed) why.Add("fed by them in hard times");
                string who = b.n == 1 ? "the " + kv.Key : (b.n == 2 ? "two" : b.n == 3 ? "three" : b.n == 4 ? "four" : b.n.ToString(CultureInfo.InvariantCulture)) + " households of the " + kv.Key;
                follows.Add(who + (why.Count > 0 ? " (" + string.Join(", ", why.ToArray()) + ")" : ""));
            }
            if (follows.Count > 0) l.Add("Their household follows " + string.Join("; ", follows.ToArray()) + ".");
            int n = Society.Leaders.Following(w, sid, hid, out int k, out int owing, out int fed);
            if (n > 0)
            {
                var parts = new List<string>();
                if (k > 0) parts.Add(n == 1 ? "kin" : k + " of kin");   // (session 32: "1 household follows theirs - 1 of kin" had stood)
                if (owing > 0) parts.Add(n == 1 ? "owing it grain" : owing + " owing it grain");
                if (fed > 0) parts.Add(n == 1 ? "fed by it in hard times" : fed + " it fed in hard times");
                l.Add((n == 1 ? "One household follows theirs" : n + " households follow theirs") + (parts.Count > 0 ? " - " + string.Join(", ", parts.ToArray()) : "") +
                      (w.Settlements.Get(sid).HeadHh == hid ? ": the most of any, so theirs is the first household" : "") + ".");
            }
            else l.Add("No household follows theirs.");
            return l;
        }

        // ------------------------------------------------------------------ temperament

        /// <summary>
        /// The leanings a person was born with that the world's choices read (Person: Bold, Social, Diligence, Chrono, Pace, Curious; 0-255, drawn once at
        /// birth): only the marked ones, each as a word and what it does in their days. Empty for one of middling leanings.
        /// </summary>
        public static List<string[]> Temperament(in Person p)
        {
            var l = new List<string[]>();
            const int Hi = 190, Lo = 65;
            if (p.Bold >= Hi) l.Add(new[] { "Bold", "readier than most to take up arms practice when the village fears its neighbours, to carry its goods to trade, to hunt, to leave for new land" });
            else if (p.Bold <= Lo) l.Add(new[] { "Cautious", "slow to take up arms, to trade away from home or to leave for new land" });
            if (p.Social >= Hi) l.Add(new[] { "Sociable", "goes visiting other villages more than most, and lingers in company of an evening" });
            else if (p.Social <= Lo) l.Add(new[] { "Keeps to themselves", "seldom goes visiting, and spends few evenings in company" });
            if (p.Diligence >= Hi) l.Add(new[] { "Hard-working", "takes up what the village needs done - fields, wood, stone, the smithy, meals - more readily than most" });
            else if (p.Diligence <= Lo) l.Add(new[] { "Easy-going", "leaves the village's work to others when they can" });
            if (p.Chrono <= Lo) l.Add(new[] { "An early riser", "wakes and sleeps up to an hour before most" });
            else if (p.Chrono >= Hi) l.Add(new[] { "Late to rise", "wakes and sleeps up to an hour after most" });
            if (p.Pace >= Hi) l.Add(new[] { "Quick on the road", "walks a tenth faster than most" });
            else if (p.Pace <= Lo) l.Add(new[] { "Slow on the road", "walks a tenth slower than most" });
            if (p.Curious >= Hi) l.Add(new[] { "Watchful", "notices danger within a minute or two" });
            else if (p.Curious <= Lo) l.Add(new[] { "Slow to notice danger", "may take some minutes to see fire or a band coming" });
            return l;
        }

        // ------------------------------------------------------------------ skills

        /// <summary>A practised skill: its name, its level (0-1), the word for it and how it is grown.</summary>
        public struct Skill { public string Name, Level, Grown; public float Value; }

        /// <summary>Every skill the world grows by practice, with how far it has gone (those not begun included, at 0).</summary>
        public static List<Skill> Skills(in Person p)
        {
            var l = new List<Skill>();
            void Add(string n, float v, string grown) => l.Add(new Skill { Name = n, Value = v, Level = Word(v), Grown = grown });
            Add("Farming", p.SkFarm, "in the fields: sowing, weeding, reaping");
            Add("Woodwork", p.SkWood, "felling, building, hollowing boats");
            Add("Hunting", p.SkHunt, "in the woods after game");
            Add("Fishing", p.SkFish, "at the shore and from boats");
            Add("Fighting", p.SkFight, "at the training ground and in fights lived through; fades unused");
            Add("Weaving", p.SkCraft, "at the weaver's shed");
            Add("Pottery", p.SkPot, "at the kiln");
            Add("Smithing", p.SkSmith, "at the smithy or an open hearth");
            return l;
        }

        /// <summary>A level of practice in a word: a master, skilled, a fair hand, a beginner, untried.</summary>
        public static string Word(float s) => s >= 0.9f ? "a master" : s >= 0.6f ? "skilled" : s >= 0.3f ? "a fair hand" : s >= 0.1f ? "a beginner" : "untried";
    }
}
