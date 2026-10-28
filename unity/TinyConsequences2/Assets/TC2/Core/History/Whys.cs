using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>
    /// (session 29: "why?") The questions a player can put to a place, a person or a building, each bound to the record the world kept of it - why a village
    /// pays its lord, why it walled itself, why it starved, why someone is first among its people or sworn to them, why they died, why a work was raised -
    /// and the answer told as a story from its root: each record of the chain the ledger keeps (its first cause, and a second where one was recorded), the
    /// link words between them from how each was recorded to follow the one before (caused by, in answer to, made possible by, part of), and where it began
    /// (nature, people's choices, or the player's hand). And a place's last years from its own records - born, died and why, households come and gone - with
    /// what most moved it. Nothing is written for an answer that the ledger does not hold; a question with no record is not asked. Reads only.
    /// </summary>
    public static class Whys
    {
        public struct Ask
        {
            public string Question;
            public long Event;          // the record the answer starts from (its chain walked back)
        }

        public struct Step
        {
            public long Event, Time;
            public string Text;         // the record dated, as the book says it
            public string Link;         // how the next step follows from this one ("which brought about", "in answer to which" ...; "" for the last)
            public bool Side;           // a second cause of the step after it, told beside the line
            public RootKind Root;
        }

        static string Name(World w, int sid) => w.History.NameOf(EntityRef.Settlement(sid));
        static string Person(World w, int pid) => w.History.NameOf(EntityRef.Person(pid));

        /// <summary>How a record follows from its cause, in words placed after the cause ("Famine in X." -> "in answer to which" -> "Y sent grain to X.").</summary>
        public static string LinkWord(CauseRel rel) => rel == CauseRel.InResponseTo ? "in answer to which" : rel == CauseRel.EnabledBy ? "which made possible" : rel == CauseRel.PartOf ? "part of which was" : "which brought about";

        /// <summary>
        /// The story of a record from its root: its first causes walked back (at most <paramref name="max"/>), told oldest first, each with how the next
        /// follows from it; a second recorded cause told beside the step it led to. The last step is the record asked about.
        /// </summary>
        public static List<Step> Story(World w, long ev, int max = 14)
        {
            var chain = new List<HistoryEvent>();
            long cur = ev;
            while (cur != 0 && chain.Count < max && w.History.TryGet(cur, out var e)) { chain.Add(e); cur = e.Cause1; }
            var steps = new List<Step>();
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var e = chain[i];
                // a second cause, beside the step it led to
                if (e.Cause2 != 0 && w.History.TryGet(e.Cause2, out var c2))
                    steps.Add(new Step { Event = c2.Id, Time = c2.Time, Text = HistoryText.Dated(w, c2), Link = "and also, " + LinkWord(e.Rel2), Side = true, Root = c2.RootKind });
                string link = i > 0 ? LinkWord(chain[i - 1].Rel1) : "";
                steps.Add(new Step { Event = e.Id, Time = e.Time, Text = HistoryText.Dated(w, e), Link = link, Root = e.RootKind });
            }
            return steps;
        }

        /// <summary>Where a story began: its oldest record's root, in words.</summary>
        public static string Began(World w, List<Step> story)
        {
            if (story.Count == 0) return "";
            var first = story[0];
            RootKind r = first.Root;
            if (w.History.TryGet(first.Event, out var e) && e.Cause1 != 0 && !w.History.TryGet(e.Cause1, out _)) return "Its earlier causes are no longer remembered.";
            return r == RootKind.You ? "It began with your hand." : r == RootKind.Nature ? "It began with nature." : r == RootKind.People ? "It began with people's own choices." : "";
        }

        /// <summary>What may be asked of a place, each with its record: its founding, its lord, its walls, its wrong, its first, its famine, its land worked
        /// harder or rested, its works' causes.</summary>
        public static List<Ask> OfPlace(World w, int sid)
        {
            var l = new List<Ask>();
            if (!w.Settlements.Exists(sid)) return l;
            ref var s = ref w.Settlements.Get(sid);
            string n = Name(w, sid);
            void Q(string q, long ev) { if (ev != 0 && w.History.TryGet(ev, out _)) l.Add(new Ask { Question = q, Event = ev }); }
            if (s.Lord != 0 && w.Settlements.Exists(s.Lord)) Q("Why does " + n + " pay tribute to " + Name(w, s.Lord) + "?", s.LordEvent);
            int paying = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == sid && os.Inhabited) paying++; }
            if (paying > 0)
            {
                // the first bond of its realm that still holds: why it became a lord
                long first = long.MaxValue; long fev = 0;
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == sid && os.Inhabited && os.LordEvent != 0 && os.LordSince < first) { first = os.LordSince; fev = os.LordEvent; } }
                Q("Why is " + n + " a lord over " + paying + (paying == 1 ? " village?" : " villages?"), fev);
            }
            if (s.StoneWallEvent != 0) Q("Why does " + n + " wall itself in stone?", s.StoneWallEvent);
            else if (s.Wall == 1 || s.Wall == 2) Q("Why does " + n + " have a palisade?", s.WallEvent);
            if (s.Foe != 0 && w.Settlements.Exists(s.Foe)) Q("Why does " + n + " hold a wrong against " + Name(w, s.Foe) + "?", s.FoeEvent);
            if (s.Head != 0 && w.People.Exists(s.Head)) Q("Why is " + Person(w, s.Head) + " first among its people?", s.HeadEvent);
            if (s.FamineEvent != 0) Q("Why did " + n + " starve" + (s.FamineAt != 0 ? " in year " + SimTime.YearOf(s.FamineAt) : "") + "?", s.FamineEvent);
            if (s.HungerEvent != 0) Q("Why is " + n + " hungry now?", s.HungerEvent);
            if (s.Epidemic != 0) Q("Why is there sickness in " + n + "?", s.Epidemic);
            if (s.TilthEvent != 0) Q("Why does " + n + " work its fields harder than before?", s.TilthEvent);
            if (s.RestEvent != 0) Q("Why does " + n + " rest a share of its fields?", s.RestEvent);
            if (s.WatchEvent != 0) Q("Why does " + n + " keep a watch at its store?", s.WatchEvent);
            Q("Why was " + n + " founded here?", s.FoundedEvent);
            return l;
        }

        /// <summary>What may be asked of a person: why they are first, sworn, sick, wounded, why they died, why they left their village.</summary>
        public static List<Ask> OfPerson(World w, int pid)
        {
            var l = new List<Ask>();
            if (!w.People.Exists(pid)) return l;
            ref var p = ref w.People.Get(pid);
            string n = Person(w, pid);
            void Q(string q, long ev) { if (ev != 0 && w.History.TryGet(ev, out _)) l.Add(new Ask { Question = q, Event = ev }); }
            if (!p.Alive) Q("Why did " + n + " die?", p.DeathEvent);
            if (p.Alive && p.Sick != 0) Q("Why is " + n + " sick?", p.SickCause);
            if (p.Alive && w.Settlements.Exists(p.Home))
            {
                ref var s = ref w.Settlements.Get(p.Home);
                if (s.Head == pid) Q("Why is " + n + " first among the people of " + Name(w, p.Home) + "?", s.HeadEvent);
            }
            // the latest of their own turns the ledger keeps: sworn, a band raised, a household moved
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs != null)
            {
                long sworn = 0, band = 0, struck = 0;
                for (int i = evs.Count - 1; i >= 0; i--)
                {
                    if (!w.History.TryGet(evs[i], out var e)) continue;
                    if (sworn == 0 && e.Kind == HistoryKind.Sworn && e.Subject.Id == pid && p.Sworn != 0) sworn = e.Id;
                    if (band == 0 && e.Kind == HistoryKind.BandRaised && e.Subject.Id == pid) band = e.Id;
                    if (struck == 0 && e.Kind == HistoryKind.Struck && e.Object.Id == pid && p.Hurt > 0.01f) struck = e.Id;
                }
                if (sworn != 0) Q("Why is " + n + " sworn to " + Person(w, p.Sworn) + "?", sworn);
                if (struck != 0) Q("Why is " + n + " wounded?", struck);
                if (band != 0) Q("Why did " + n + " raise a band?", band);
            }
            return l;
        }

        /// <summary>The trend of a place over its last years, from its own records: births, deaths by cause, households come and gone; what moved it most.
        /// Lines of words; the worst of its troubles with its record to ask why.</summary>
        public static List<string> Trend(World w, int sid, int years, out long worstEvent)
        {
            worstEvent = 0;
            var lines = new List<string>();
            if (!w.Settlements.Exists(sid)) return lines;
            long from = w.Now - years * SimTime.Year;
            int born = 0, died = 0, hunger = 0, sick = 0, wounds = 0, birth = 0, earth = 0, cold = 0, sea = 0, hhOut = 0, pplOut = 0, hhIn = 0, pplIn = 0;
            var sicknessDead = new Dictionary<long, int>();
            var pev = w.History.RecordsAt(sid);
            if (pev != null)
                for (int i = pev.Count - 1; i >= 0; i--)
                {
                    if (!w.History.TryGet(pev[i], out var e)) continue;
                    if (e.Time < from) break;
                    if (e.Kind == HistoryKind.PersonBorn) born++;
                    else if (e.Kind == HistoryKind.PersonDied)
                    {
                        died++;
                        switch (e.ArgA)
                        {
                            case 2: hunger++; break;
                            case 3: birth++; break;
                            case 9: cold++; break;
                            case 10: sick++; { long k = e.Cause1; sicknessDead.TryGetValue(k, out int c); sicknessDead[k] = c + 1; } break;
                            case 11: wounds++; break;
                            case 12: sea++; break;
                            case 4: case 5: case 6: case 7: case 8: case 14: earth++; break;
                        }
                    }
                    else if ((e.Kind == HistoryKind.HouseholdMoved || e.Kind == HistoryKind.PartySetOut || e.Kind == HistoryKind.VillageSplit) && e.Settlement == sid) { hhOut += System.Math.Max(1, e.ArgA); pplOut += e.ArgB; }
                }
            var sev = w.History.EventsOf(EntityRef.Settlement(sid));
            if (sev != null)
                for (int i = sev.Count - 1; i >= 0; i--)
                {
                    if (!w.History.TryGet(sev[i], out var e)) continue;
                    if (e.Time < from) break;
                    if ((e.Kind == HistoryKind.HouseholdMoved || e.Kind == HistoryKind.PartyJoined) && e.Object.Kind == EntityKind.Settlement && e.Object.Id == sid && e.Settlement != sid) { hhIn += System.Math.Max(1, e.ArgA); pplIn += e.ArgB; }
                }
            int net = born - died + pplIn - pplOut;
            lines.Add("In the last " + years + " years " + born + " were born here and " + died + " died; " + (hhOut > 0 ? hhOut + (hhOut == 1 ? " household" : " households") + " (" + pplOut + " people) left" : "no household left") +
                      (hhIn > 0 ? ", " + hhIn + (hhIn == 1 ? " came" : " came") + " (" + pplIn + " people)" : "") + ".");
            var causes = new List<string>();
            if (hunger > 0) causes.Add(hunger + " of hunger");
            if (sick > 0) causes.Add(sick + " of sickness");
            if (wounds > 0) causes.Add(wounds + " of wounds");
            if (birth > 0) causes.Add(birth + " in childbirth");
            if (cold > 0) causes.Add(cold + " of the cold");
            if (earth > 0) causes.Add(earth + " in the earth's violence");
            if (sea > 0) causes.Add(sea + " at sea");
            if (causes.Count > 0) lines.Add("Of the dead, " + string.Join(", ", causes) + "; the rest of their years.");
            ref var s = ref w.Settlements.Get(sid);
            string trend = net > 2 ? "It is growing" : net < -2 ? "It is shrinking" : "It holds steady";
            string because = "";
            if (net < -2)
            {
                int worst = System.Math.Max(System.Math.Max(hunger, sick), System.Math.Max(wounds, pplOut));
                if (worst == hunger && hunger > 0) { because = ": most of all from hunger"; worstEvent = s.FamineEvent; }
                else if (worst == sick && sick > 0)
                {
                    because = ": most of all from sickness";
                    long top = 0; int most = 0; foreach (var kv in sicknessDead) if (kv.Value > most) { most = kv.Value; top = kv.Key; }
                    worstEvent = top;
                }
                else if (worst == wounds && wounds > 0) because = ": most of all from violence";
                else if (worst == pplOut && pplOut > 0) because = ": most of all from the households that left";
            }
            else if (net > 2) because = born > died ? ": more are born than die" + (pplIn > pplOut ? ", and households come to it" : "") : ": households come to it";
            lines.Add(trend + because + ".");
            return lines;
        }

        /// <summary>What troubles a place now, from its state: hunger and rations, sickness, a famine remembered, a wrong held, cold homes, a store short of the
        /// harvest, land short of its people. And what it is building.</summary>
        public static List<string> Now(World w, int sid)
        {
            var l = new List<string>();
            if (!w.Settlements.Exists(sid)) return l;
            ref var s = ref w.Settlements.Get(sid);
            string n = Name(w, sid);
            if (s.HungerEvent != 0) l.Add("Hungry now: its people are going short.");
            if (s.RationEvent != 0 && s.Ration < 0.999) l.Add("Rationing its grain: " + (s.Ration * 100.0).ToString("0", CultureInfo.InvariantCulture) + " % of full meals until the next harvest.");
            if (s.Epidemic != 0 && s.EpiPath != 0) l.Add(Cap(w.History.NameOf(new EntityRef(EntityKind.Pathogen, s.EpiPath))) + " is in it: " + s.EpiDead + " dead so far.");
            if (s.FamineAt != 0 && w.Now - s.FamineAt < 3 * SimTime.Year) l.Add("A famine struck it in year " + SimTime.YearOf(s.FamineAt) + "; " + (s.Memory * 100.0).ToString("0", CultureInfo.InvariantCulture) + " % of its grown people lived through one.");
            if (s.ColdEvent != 0) l.Add("Its homes are cold.");
            if (s.Foe != 0 && w.Settlements.Exists(s.Foe)) l.Add("It holds a wrong against " + Name(w, s.Foe) + ".");
            if (s.TilthEvent != 0) l.Add("Its land within reach no longer feeds its people at the old work: it ploughs and hoes harder.");
            if (s.Wall == 1) l.Add("Building a palisade.");
            byte next = Construction.NextWork(in s);
            if (next != 0 && s.Wall != 1) l.Add("Building: " + Structures.Name(next) + ".");
            return l;
        }

        /// <summary>(session 29: war scars) The record of the fire that brought a ruin down, or 0: a building that fell at once fell at the moment a band burned
        /// it (`Homes.Burnt`) or a stone from the sky flattened it - its `Left` set back by exactly its fall time and a millisecond - so the record of that
        /// kind at that moment is its cause (looked for at its own place, else by its moment in the whole ledger). The view draws such a ruin charred.</summary>
        public static long FellTo(World w, in Building b)
        {
            if (b.Left == 0 || !Homes.Fallen(in b, w.Now)) return 0;
            long fell = b.Left + DMath.ToLong(Homes.FallYears(in b) * SimTime.Year) + 1;
            var at = w.History.RecordsAt(b.Settlement);
            if (at != null)
                for (int i = at.Count - 1; i >= 0; i--)
                {
                    if (!w.History.TryGet(at[i], out var e)) continue;
                    if (e.Time < fell) break;
                    if (e.Time == fell && (e.Kind == HistoryKind.BuildingBurned || e.Kind == HistoryKind.MeteorStruck)) return e.Id;
                }
            // (a stone's record names the village nearest its fall; the buildings it flattened may be another's) - by its moment, the ledger in time order
            int lo = 0, hi = w.History.Count - 1;
            while (lo < hi) { int mid = (lo + hi) / 2; if (w.History.At(mid).Time < fell) lo = mid + 1; else hi = mid; }
            for (int i = lo; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Time != fell) break;
                if (e.Kind == HistoryKind.MeteorStruck || e.Kind == HistoryKind.BuildingBurned) return e.Id;
            }
            return 0;
        }

        /// <summary>
        /// (session 29: roads read) A way between two villages as it is: a track, a path or a road by how often it is walked now (its wear halves each year
        /// it is not renewed: about 0.7 of it is the walks of a year), when it was first worn into a path or beaten into a road, what is on it at this moment
        /// and why - traders and their loads, carriers of a lord's tenth, gifts of grain, bands, households moving, kin visiting - and the bond between its two
        /// villages (tribute, one realm, a wrong held). The first line is its name. Every word from state and records.
        /// </summary>
        public static List<string> Way(World w, int a, int b)
        {
            var l = new List<string>();
            if (!w.Settlements.Exists(a) || !w.Settlements.Exists(b)) return l;
            double use = w.Ways.UseOf(w.Now, a, b);
            byte st = TC2.Core.Agents.Ways.StateOf(use);
            string na = Name(w, a), nb = Name(w, b);
            l.Add((st == 3 ? "The road" : st == 2 ? "The path" : st == 1 ? "The track" : "The old way") + " between " + na + " and " + nb);
            l.Add("walked about " + (use * 0.69).ToString("0", CultureInfo.InvariantCulture) + " times a year - " + (st == 3 ? "beaten broad by its walkers" : st == 2 ? "worn into a path by its walkers" : st == 1 ? "a faint track" : "greening over"));
            // when it was worn and beaten (the ledger tells each the first time)
            var evs = w.History.EventsOf(EntityRef.Settlement(a));
            string firsts = "";
            if (evs != null)
                for (int i = 0; i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.WayWorn && ((e.Subject.Id == a && e.Object.Id == b) || (e.Subject.Id == b && e.Object.Id == a)))
                        firsts += (firsts.Length > 0 ? "; " : "") + (e.ArgA >= 3 ? "a road since year " : "a path since year ") + SimTime.YearOf(e.Time);
            if (firsts.Length > 0) l.Add(firsts);
            // on it now, and why
            var why = new Dictionary<string, int>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || p.Step != StepKind.Travel || p.Act == ActivityKind.Voyage) continue;
                if (!((p.RouteA == a && p.RouteB == b) || (p.RouteA == b && p.RouteB == a))) continue;
                string k = OnTheRoad(w, in p);
                why.TryGetValue(k, out int n); why[k] = n + 1;
            }
            if (why.Count == 0) l.Add("nobody on it at this moment");
            else
            {
                var parts = new List<KeyValuePair<string, int>>(why);
                parts.Sort((x, y) => { int c = y.Value.CompareTo(x.Value); return c != 0 ? c : string.CompareOrdinal(x.Key, y.Key); });
                var said = new List<string>();
                foreach (var kv in parts) said.Add(kv.Value + " " + kv.Key);
                l.Add("on it now: " + string.Join(", ", said));
            }
            // the bond between its ends
            ref var sa = ref w.Settlements.Get(a); ref var sb = ref w.Settlements.Get(b);
            if (sa.Lord == b) l.Add(na + " pays " + nb + " a tenth of its harvest");
            else if (sb.Lord == a) l.Add(nb + " pays " + na + " a tenth of its harvest");
            else if (w.Conflict.Root(a) == w.Conflict.Root(b) && w.Conflict.PolitySize(w.Conflict.Root(a)) > 0) l.Add("both of " + Name(w, w.Conflict.Root(a)) + "'s realm");
            if (sa.Foe == b) l.Add(na + " holds a wrong against " + nb);
            if (sb.Foe == a) l.Add(nb + " holds a wrong against " + na);
            return l;
        }

        /// <summary>Why someone is on a road, in the words a list can carry (their decision's own reason and their load).</summary>
        static string OnTheRoad(World w, in Person p)
        {
            string load = p.CargoQty > 0f ? (p.Cargo == Trade.Grain ? " grain" : p.Cargo == Trade.Metal ? " copper" : p.Cargo == Trade.Iron ? " iron" : p.Cargo == Trade.Fibre ? " fibre" : p.Cargo == Trade.Wares ? " wares" : p.Cargo == Trade.Pots ? " pots" : " hides") : "";
            if (p.CargoTo == Trade.ToLord || p.CargoTo == Trade.ToTenth || p.CargoTo == Trade.TenthAboard || p.Why == WhyCode.Tribute) return "carrying the lord's tenth";
            if (p.CargoTo == Trade.AsGift) return "carrying grain to a famine";
            if (p.CargoTo == Trade.AsReturn) return "giving back grain once given";
            switch (p.Why)
            {
                case WhyCode.Trade: return load.Length > 0 ? "trading" + load : "going to trade";
                case WhyCode.Raid: return "in a band";
                case WhyCode.Take: return "going to take grain in the dark";
                case WhyCode.Claim: return "asking amends";
                case WhyCode.VisitKin: return "visiting kin";
                case WhyCode.VisitFriend: return "visiting";
                case WhyCode.NewHome: return "moving to a new home";
                case WhyCode.LookForLand: return "looking at the land beyond";
                case WhyCode.GoingHome: return "going home";
                default: return load.Length > 0 ? "carrying" + load : "on errands";
            }
        }

        /// <summary>A realm as it stands (session 29, the place page's Realm): its seat and ruler, every village under it with its depth and its own lord,
        /// its people and the men sworn to its firsts, the tribute the seat took in the last year, its raids with villages outside it in the last ten years,
        /// and the bonds made and broken among its villages (newest first) - from state and records only.</summary>
        public sealed class RealmInfo
        {
            public int Seat, Ruler, People, Sworn;
            public double Tribute;
            public readonly List<int> Villages = new List<int>(), Depth = new List<int>(), LordOf = new List<int>();
            public readonly List<long> Wars = new List<long>(), Bonds = new List<long>();
            public long FirstBond;
        }

        public static RealmInfo Realm(World w, int sid)
        {
            var r = new RealmInfo();
            if (!w.Settlements.Exists(sid)) return r;
            int seat = w.Conflict.Root(sid);
            r.Seat = seat;
            if (w.Settlements.Exists(seat)) r.Ruler = w.Settlements.Get(seat).Head;
            var members = new HashSet<int>();
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                ref var os = ref w.Settlements.Get(o);
                if (!os.Inhabited || w.Conflict.Root(o) != seat) continue;
                int d = 0, cur = o;
                while (cur != seat && d < 16 && w.Settlements.Exists(cur)) { cur = w.Settlements.Get(cur).Lord; d++; if (cur == 0) break; }
                r.Villages.Add(o); r.Depth.Add(d); r.LordOf.Add(os.Lord); members.Add(o);
                r.People += os.Population;
            }
            // (the seat first, then by tier and by people)
            var order = new List<int>();
            for (int i = 0; i < r.Villages.Count; i++) order.Add(i);
            order.Sort((a, b) => { int c = r.Depth[a].CompareTo(r.Depth[b]); if (c != 0) return c; c = w.Settlements.Get(r.Villages[b]).Population.CompareTo(w.Settlements.Get(r.Villages[a]).Population); return c != 0 ? c : r.Villages[a].CompareTo(r.Villages[b]); });
            var v = new List<int>(); var dd = new List<int>(); var lo = new List<int>();
            foreach (int i in order) { v.Add(r.Villages[i]); dd.Add(r.Depth[i]); lo.Add(r.LordOf[i]); }
            r.Villages.Clear(); r.Villages.AddRange(v); r.Depth.Clear(); r.Depth.AddRange(dd); r.LordOf.Clear(); r.LordOf.AddRange(lo);
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (p.Alive && p.Sworn != 0 && members.Contains(p.Home)) r.Sworn++;
            }
            long year = w.Now - SimTime.Year, ten = w.Now - 10 * SimTime.Year;
            var seenWar = new HashSet<long>(); var seenBond = new HashSet<long>();
            long firstAt = long.MaxValue;
            foreach (int m in members)
            {
                var evs = w.History.EventsOf(EntityRef.Settlement(m));
                if (evs == null) continue;
                for (int i = evs.Count - 1; i >= 0; i--)
                {
                    if (!w.History.TryGet(evs[i], out var e)) continue;
                    if (e.Kind == HistoryKind.TributePaid && m == seat && e.Object.Id == seat && e.Time >= year) r.Tribute += e.Magnitude;
                    else if (e.Kind == HistoryKind.Raid && e.Time >= ten)
                    {
                        bool a = members.Contains(e.Subject.Id), b = members.Contains(e.Object.Id);
                        if (a != b && seenWar.Add(e.Id)) r.Wars.Add(e.Id);
                    }
                    else if ((e.Kind == HistoryKind.TributeAgreed || e.Kind == HistoryKind.TributeEnded) && seenBond.Add(e.Id)) r.Bonds.Add(e.Id);
                }
                ref var ms = ref w.Settlements.Get(m);
                if (m != seat && ms.LordEvent != 0 && ms.LordSince < firstAt && w.History.TryGet(ms.LordEvent, out _)) { firstAt = ms.LordSince; r.FirstBond = ms.LordEvent; }
            }
            r.Wars.Sort((a, b) => b.CompareTo(a));
            r.Bonds.Sort((a, b) => b.CompareTo(a));
            if (r.Bonds.Count > 14) r.Bonds.RemoveRange(14, r.Bonds.Count - 14);
            if (r.Wars.Count > 10) r.Wars.RemoveRange(10, r.Wars.Count - 10);
            return r;
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
