using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>When a consequence came, from the cast: the cast itself; at once (within a day); the days after (within a season); that year (within a
    /// year); years on (within ten); decades on. In this order: a card never comes in an earlier tier than the card it hangs from.</summary>
    public enum ConsequenceTier : byte { Cast = 0, Immediate = 1, Days = 2, Secondary = 3, LongTerm = 4, Decades = 5 }

    /// <summary>
    /// One card of a cast's tree: the records of one kind (and place, and way of dying) that followed from the same card, told together - "7 people killed
    /// by the stone at Hillford" - with the records it stands for (`Members`, ledger ids), when the first and the last came, and what followed from them.
    /// A "+N more" card stands for the branches folded away (`More`); a detail card tells a count its record holds (the buildings a stone flattened: `Detail`).
    /// </summary>
    public sealed class ConsequenceNode
    {
        public int Index, Depth, Parent = -1;   // in the tree's flat list; the cast is 0
        public HistoryKind Kind;
        public int Sub;                       // a way of dying (PersonDied's ArgA), a work's or a craft's kind; 0 else
        public int Settlement;                // the place they are told at (0 none)
        public int Count;                     // the records it stands for (a detail: the count its record tells; "+N more": the records folded)
        public int Total;                     // the records in it and under it
        public int Below;                     // the records under it not drawn (the tree's depth or room)
        public int SumA, SumB;                // its records' ArgA and ArgB summed (households that set out, buildings flattened ...)
        public long Event;                    // the weightiest of its records
        public long First, Last;              // when the first and the last of them came
        public long Reach;                    // (session 32, final) when the latest record in it or under it came - how far on its thread runs
        public double Weight;                 // the importance of it and what is under it
        public ConsequenceTier Tier;
        public byte Family;                   // what it was, for its colour (Consequences.Families)
        public bool More, Detail;
        public string Title = "", When = "", Place = "";
        public readonly List<long> Members = new List<long>();
        public readonly List<string> Folded = new List<string>();   // ("+N more": the titles of the branches folded)
        public readonly List<int> Children = new List<int>();
    }

    /// <summary>A cast's tree: its cards in a flat list (the cast first, then breadth first), how many records are rooted in it.</summary>
    public sealed class ConsequenceTree
    {
        public long Cast, CastTime, LastTime;
        public int Records;       // records rooted in the cast (scanned)
        public bool Capped;       // more than the scan's cap: the rest not read
        public int MaxDepth;
        public int CastsAfter;    // casts since (its chapter in the book's Your Actions: newest first)
        /// <summary>(session 32) The records rooted in the cast by when they came (indexed by ConsequenceTier; Cast: 0) - every one, drawn or not.</summary>
        public readonly int[] TierRecords = new int[Consequences.Tiers];
        public readonly List<ConsequenceNode> Nodes = new List<ConsequenceNode>();
        public ConsequenceNode Root => Nodes[0];
    }

    /// <summary>
    /// (session 32: what came of it) A cast's consequences as a tree, for the HUD's "What came of it": every record whose root is the cast, each under the
    /// record its first cause names (its second where the first is gone; the cast where both are), the records of one kind under one card told together,
    /// the biggest few branches of each card kept and the rest folded into one. (session 32, final) The cards are opened furthest-reaching first - the card
    /// whose thread runs latest, then the one with the most records under it - so the long threads (a sickness carried village to village for years, a
    /// famine that emptied a place and the village its people founded decades on) are followed to their ends before the room goes to the branches of a
    /// single day; to a depth of 24. An observer: reads the ledger, writes nothing.
    /// </summary>
    public static class Consequences
    {
        public const int ScanCap = 60000, MaxDepth = 24, MaxBranches = 5, MaxNodes = 240, Tiers = 6;

        /// <summary>The families a card's kind falls in (its colour and its word in the view).</summary>
        public const byte FamOther = 0, FamDeath = 1, FamFire = 2, FamEarth = 3, FamSky = 4, FamFood = 5, FamSickness = 6, FamPeople = 7, FamPower = 8, FamWorks = 9, FamCast = 10;

        /// <summary>The latest cast still in the ledger (0 none).</summary>
        public static long LatestCast(World w)
        {
            var h = w.History;
            for (int i = h.Count - 1; i >= 0; i--) if (h.RefAt(i).Kind == HistoryKind.GodCommand) return h.RefAt(i).Id;
            return 0;
        }

        /// <summary>The tree of a cast (null when it is not a cast the ledger keeps).</summary>
        public static ConsequenceTree Build(World w, long castId, int maxNodes = MaxNodes)
        {
            var h = w.History;
            int ci = h.IndexOf(castId);
            if (ci < 0) return null;
            var cast = h.At(ci);
            if (cast.Kind != HistoryKind.GodCommand) return null;
            var t = new ConsequenceTree { Cast = castId, CastTime = cast.Time, LastTime = cast.Time };
            // the records rooted in the cast, in ledger (id) order
            var idx = new List<int>();
            for (int i = ci + 1; i < h.Count; i++)
            {
                ref var e = ref h.RefAt(i);
                if (e.Kind == HistoryKind.GodCommand) t.CastsAfter++;
                if (e.RootKind != RootKind.You || e.Root != castId || t.Capped) continue;
                if (idx.Count >= ScanCap) { t.Capped = true; continue; }
                idx.Add(i);
            }
            int n = idx.Count;
            t.Records = n;
            var ids = new long[n];
            for (int j = 0; j < n; j++) ids[j] = h.RefAt(idx[j]).Id;
            // each under its first cause if that came of the cast too (its second where the first is forgotten; the cast else)
            var parent = new int[n];
            for (int j = 0; j < n; j++)
            {
                ref var e = ref h.RefAt(idx[j]);
                int p = Find(ids, e.Cause1, j);
                if (p == -2 && e.Cause2 != 0) p = Find(ids, e.Cause2, j);
                parent[j] = p < 0 ? -1 : p;
            }
            // children lists (in id order), and what each record carries under it
            var firstKid = new int[n]; var nextSib = new int[n];
            for (int j = 0; j < n; j++) { firstKid[j] = -1; nextSib[j] = -1; }
            int rootFirst = -1;
            for (int j = n - 1; j >= 0; j--)
            {
                if (parent[j] < 0) { nextSib[j] = rootFirst; rootFirst = j; }
                else { nextSib[j] = firstKid[parent[j]]; firstKid[parent[j]] = j; }
            }
            var size = new int[n]; var imp = new double[n]; var reach = new long[n];
            for (int j = n - 1; j >= 0; j--)
            {
                ref var e = ref h.RefAt(idx[j]);
                size[j] += 1; imp[j] += (double)e.Importance;
                if (e.Time > t.LastTime) t.LastTime = e.Time;
                t.TierRecords[(int)TierOf(e.Time - t.CastTime)]++;
                if (e.Time > reach[j]) reach[j] = e.Time;
                if (parent[j] >= 0) { size[parent[j]] += size[j]; imp[parent[j]] += imp[j]; if (reach[j] > reach[parent[j]]) reach[parent[j]] = reach[j]; }
            }
            // the cast's card
            var root = new ConsequenceNode { Index = 0, Depth = 0, Kind = HistoryKind.GodCommand, Settlement = cast.Settlement, Count = 1, Total = n + 1, Event = castId,
                                             First = cast.Time, Last = t.LastTime, Tier = ConsequenceTier.Cast, Family = FamCast, When = "year " + SimTime.YearOf(cast.Time).ToString(CultureInfo.InvariantCulture) };
            root.Members.Add(castId);
            root.Reach = t.LastTime;
            string what = HistoryText.Describe(w, in cast).TrimEnd('.');
            if (what.StartsWith("You ", System.StringComparison.Ordinal)) what = char.ToUpperInvariant(what[4]) + what.Substring(5);
            root.Title = what;
            root.Place = cast.Settlement > 0 ? h.NameOf(EntityRef.Settlement(cast.Settlement)) : cast.Cell >= 0 && cast.Cell < w.Planet.Cells ? w.PlaceName(cast.Cell) : "";
            for (int j = 0; j < n; j++) root.Weight += parent[j] < 0 ? imp[j] : 0.0;
            t.Nodes.Add(root);
            var recs = new List<List<int>> { new List<int> { -1 } };
            // each card's records' children, grouped; the biggest branches kept, the rest folded. The cards opened furthest-reaching first (the latest record
            // under them, then the most records, the earliest card of equals): the room goes to the threads that run years and decades on before the
            // branches of one day (breadth first, a wide first day could spend it all and cut a famine's decades at its first card)
            var kids = new List<int>();
            var groups = new List<ConsequenceNode>(); var groupRecs = new List<List<int>>(); var groupBest = new List<double>();
            var byKey = new Dictionary<long, int>();
            var open = new List<int> { 0 };
            while (open.Count > 0)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++)
                {
                    var na = t.Nodes[open[k]]; var nb = t.Nodes[open[bi]];
                    int x = na.Reach.CompareTo(nb.Reach);
                    if (x == 0) x = (na.Total - na.Count).CompareTo(nb.Total - nb.Count);
                    if (x > 0 || (x == 0 && na.Index < nb.Index)) bi = k;
                }
                int q = open[bi]; open.RemoveAt(bi);
                var node = t.Nodes[q];
                if (node.More || node.Detail) continue;
                int below = node.Total - node.Count;
                bool detail = node.Kind == HistoryKind.MeteorStruck && node.SumA > 0;   // (the buildings a stone flattened: its record's count, not records of their own)
                if (below <= 0 && !detail) continue;
                if (node.Depth >= MaxDepth) { node.Below = below; continue; }
                // the children of its records, in id order
                kids.Clear();
                var mine = recs[q];
                for (int k = 0; k < mine.Count; k++)
                    for (int c = mine[k] < 0 ? rootFirst : firstKid[mine[k]]; c >= 0; c = nextSib[c]) kids.Add(c);
                if (mine.Count > 1) kids.Sort();
                if (kids.Count == 0 && !detail) continue;
                // grouped: of one kind, at one place, one way of dying (or kind of work)
                groups.Clear(); groupRecs.Clear(); groupBest.Clear(); byKey.Clear();
                for (int k = 0; k < kids.Count; k++)
                {
                    int j = kids[k];
                    ref var e = ref h.RefAt(idx[j]);
                    int sub = SubOf(in e), place = PlaceOf(in e);
                    long key = ((long)(int)e.Kind << 44) | ((long)(sub & 0xFFF) << 32) | (uint)place;
                    if (!byKey.TryGetValue(key, out int g))
                    {
                        g = groups.Count; byKey[key] = g;
                        groups.Add(new ConsequenceNode { Kind = e.Kind, Sub = sub, Settlement = place, First = e.Time, Last = e.Time, Event = e.Id, Family = FamilyOf(e.Kind, sub) });
                        groupRecs.Add(new List<int>()); groupBest.Add((double)e.Importance);
                    }
                    var gn = groups[g];
                    groupRecs[g].Add(j);
                    gn.Count++; gn.Total += size[j]; gn.Weight += imp[j]; gn.SumA += e.ArgA; gn.SumB += e.ArgB;
                    if (reach[j] > gn.Reach) gn.Reach = reach[j];
                    gn.Members.Add(e.Id);
                    if (e.Time < gn.First) gn.First = e.Time;
                    if (e.Time > gn.Last) gn.Last = e.Time;
                    if ((double)e.Importance > groupBest[g]) { groupBest[g] = e.Importance; gn.Event = e.Id; }
                }
                // the biggest branches by the records under them, then their weight; the rest one card
                var order = new List<int>(groups.Count);
                for (int g = 0; g < groups.Count; g++) order.Add(g);
                order.Sort((a, b) =>
                {
                    int x = groups[b].Total.CompareTo(groups[a].Total); if (x != 0) return x;
                    x = groups[b].Weight.CompareTo(groups[a].Weight); if (x != 0) return x;
                    return groupRecs[a][0].CompareTo(groupRecs[b][0]);
                });
                int keep = System.Math.Min(MaxBranches, order.Count);
                int want = keep + (order.Count > keep ? 1 : 0) + (detail ? 1 : 0);
                if (t.Nodes.Count + want > maxNodes) { node.Below = below; continue; }
                if (detail)
                {
                    var d = new ConsequenceNode { Kind = HistoryKind.MeteorStruck, Detail = true, Count = node.SumA, Total = 0, Settlement = node.Settlement, Event = node.Event,
                                                  First = node.First, Last = node.First, Family = FamEarth, Place = node.Place };
                    d.Members.AddRange(node.Members);
                    d.Title = node.SumA == 1 ? "A building flattened" : node.SumA.ToString(CultureInfo.InvariantCulture) + " buildings flattened";
                    Add(t, node, d, recs, null);
                }
                var kept = order.GetRange(0, keep);
                kept.Sort((a, b) =>
                {
                    int x = groups[a].First.CompareTo(groups[b].First); if (x != 0) return x;
                    x = groups[b].Total.CompareTo(groups[a].Total); if (x != 0) return x;
                    return groupRecs[a][0].CompareTo(groupRecs[b][0]);
                });
                foreach (int g in kept)
                {
                    var gn = groups[g];
                    h.TryGet(gn.Event, out var rep);
                    gn.Place = gn.Settlement > 0 ? h.NameOf(EntityRef.Settlement(gn.Settlement)) : rep.Cell >= 0 && rep.Cell < w.Planet.Cells ? w.PlaceName(rep.Cell) : "the world";
                    gn.Title = Title(w, gn, in rep);
                    Add(t, node, gn, recs, groupRecs[g]);
                    open.Add(gn.Index);
                }
                if (order.Count > keep)
                {
                    var more = new ConsequenceNode { More = true, Settlement = 0, First = long.MaxValue, Last = long.MinValue, Family = FamOther };
                    for (int k = keep; k < order.Count; k++)
                    {
                        var gn = groups[order[k]];
                        more.Count += gn.Total; more.Total += gn.Total; more.Weight += gn.Weight;
                        more.Members.Add(gn.Event);
                        if (gn.First < more.First) more.First = gn.First;
                        if (gn.Last > more.Last) more.Last = gn.Last;
                        if (more.Folded.Count < 12)
                        {
                            h.TryGet(gn.Event, out var rep);
                            gn.Place = gn.Settlement > 0 ? h.NameOf(EntityRef.Settlement(gn.Settlement)) : rep.Cell >= 0 && rep.Cell < w.Planet.Cells ? w.PlaceName(rep.Cell) : "the world";
                            more.Folded.Add(Title(w, gn, in rep));
                        }
                    }
                    more.Event = more.Members[0];
                    int folded = order.Count - keep;
                    more.Title = "+" + folded.ToString(CultureInfo.InvariantCulture) + (folded == 1 ? " more branch" : " more branches") + " (" + Records(more.Count) + ")";
                    Add(t, node, more, recs, null);
                }
            }
            // times and tiers
            foreach (var nd in t.Nodes)
            {
                if (nd.Index == 0) continue;
                long dt = nd.First - t.CastTime;
                nd.Tier = TierOf(dt);
                nd.When = Offset(dt);
                if (nd.Last - nd.First >= SimTime.Day && !nd.Detail) nd.When += ", over " + Span(nd.Last - nd.First);
                if (nd.Depth > t.MaxDepth) t.MaxDepth = nd.Depth;
            }
            return t;
        }

        static void Add(ConsequenceTree t, ConsequenceNode parent, ConsequenceNode child, List<List<int>> recs, List<int> mine)
        {
            child.Index = t.Nodes.Count; child.Depth = parent.Depth + 1; child.Parent = parent.Index;
            t.Nodes.Add(child);
            recs.Add(mine ?? new List<int>());
            parent.Children.Add(child.Index);
        }

        /// <summary>A record's place in the scanned list by its id (binary search over those before it): -1 the cast, -2 not one of them.</summary>
        static int Find(long[] ids, long id, int before)
        {
            if (id == 0) return -2;
            int lo = 0, hi = before - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (ids[mid] == id) return mid;
                if (ids[mid] < id) lo = mid + 1; else hi = mid - 1;
            }
            return -2;
        }

        static int SubOf(in HistoryEvent e)
        {
            switch (e.Kind)
            {
                case HistoryKind.PersonDied: case HistoryKind.WorkBuilt: case HistoryKind.WorkBegun: case HistoryKind.TechniqueFound: case HistoryKind.TechniqueKnown: return e.ArgA;
                default: return 0;
            }
        }

        static int PlaceOf(in HistoryEvent e)
        {
            if (e.Settlement > 0) return e.Settlement;
            if (e.Subject.Kind == EntityKind.Settlement) return e.Subject.Id;
            return 0;
        }

        /// <summary>When a card came, from the cast: at once (a day), the days after (a season), that year, years on (under ten), decades on.</summary>
        public static ConsequenceTier TierOf(long dt) => dt <= SimTime.Day ? ConsequenceTier.Immediate : dt <= SimTime.Season ? ConsequenceTier.Days : dt <= SimTime.Year ? ConsequenceTier.Secondary
                                                       : dt < 10 * SimTime.Year ? ConsequenceTier.LongTerm : ConsequenceTier.Decades;

        /// <summary>A tier in a few words: "At once", "Days after", "That year", "Years on", "Decades on" ("Your action" the cast's).</summary>
        public static string TierWords(ConsequenceTier k)
        {
            switch (k)
            {
                case ConsequenceTier.Immediate: return "At once";
                case ConsequenceTier.Days: return "Days after";
                case ConsequenceTier.Secondary: return "That year";
                case ConsequenceTier.LongTerm: return "Years on";
                case ConsequenceTier.Decades: return "Decades on";
                default: return "Your action";
            }
        }

        /// <summary>How long after the cast, in words: "the same hour", "5 hours on", "the next day", "12 days on", "a year on", "12 years on".</summary>
        public static string Offset(long dt)
        {
            if (dt < SimTime.Hour) return "the same hour";
            if (dt < SimTime.Day) { long hr = dt / SimTime.Hour; return hr == 1 ? "an hour on" : hr.ToString(CultureInfo.InvariantCulture) + " hours on"; }
            if (dt < SimTime.Year) { long d = dt / SimTime.Day; return d == 1 ? "the next day" : d.ToString(CultureInfo.InvariantCulture) + " days on"; }
            long y = dt / SimTime.Year;
            return y == 1 ? "a year on" : y.ToString(CultureInfo.InvariantCulture) + " years on";
        }

        /// <summary>How long a card's records went on: "5 hours", "12 days", "3 years".</summary>
        public static string Span(long dt)
        {
            if (dt < SimTime.Day) { long hr = System.Math.Max(1L, dt / SimTime.Hour); return hr == 1 ? "an hour" : hr.ToString(CultureInfo.InvariantCulture) + " hours"; }
            if (dt < SimTime.Year) { long d = dt / SimTime.Day; return d == 1 ? "a day" : d.ToString(CultureInfo.InvariantCulture) + " days"; }
            long y = dt / SimTime.Year;
            return y == 1 ? "a year" : y.ToString(CultureInfo.InvariantCulture) + " years";
        }

        static string Records(int n) => n == 1 ? "1 record" : n.ToString(CultureInfo.InvariantCulture) + " records";

        /// <summary>What family of things a kind is (its colour on the card).</summary>
        public static byte FamilyOf(HistoryKind k, int sub = 0)
        {
            switch (k)
            {
                case HistoryKind.PersonDied: return sub == 2 ? FamFood : sub == 10 ? FamSickness : FamDeath;
                case HistoryKind.FireStarted: case HistoryKind.Wildfire: case HistoryKind.FireOut: case HistoryKind.FieldsBurned: case HistoryKind.BuildingBurned:
                case HistoryKind.JourneyInterrupted: case HistoryKind.FledDanger: case HistoryKind.DangerNoticed: return FamFire;
                case HistoryKind.MeteorStruck: case HistoryKind.Quake: case HistoryKind.Landslides: case HistoryKind.Tsunami: case HistoryKind.Eruption: case HistoryKind.AshFall:
                case HistoryKind.VentBorn: case HistoryKind.IslandRose: case HistoryKind.HomesDrowned: case HistoryKind.FieldsFlooded: return FamEarth;
                case HistoryKind.LightningStrike: case HistoryKind.Storm: case HistoryKind.StormLandfall: case HistoryKind.StormSurge: case HistoryKind.CropsFlattened:
                case HistoryKind.Drought: case HistoryKind.RainBrought: case HistoryKind.WarmAir: case HistoryKind.ColdAir: case HistoryKind.ColdHomes: return FamSky;
                case HistoryKind.HarvestFailed: case HistoryKind.Hunger: case HistoryKind.Famine: case HistoryKind.Rationing: case HistoryKind.WildFood: case HistoryKind.StoresSpoiled:
                case HistoryKind.CropsBlighted: case HistoryKind.ReliefSent: case HistoryKind.ReliefReturned: case HistoryKind.SoilRenewed: case HistoryKind.Harvest:
                case HistoryKind.BeastsCulled: case HistoryKind.BeastsBrought: case HistoryKind.FieldsRested: case HistoryKind.FieldsWorkedHarder: return FamFood;
                case HistoryKind.PathogenAppeared: case HistoryKind.EpidemicArrived: case HistoryKind.EpidemicEnded: return FamSickness;
                case HistoryKind.SettlementAbandoned: case HistoryKind.SettlementFounded: case HistoryKind.PartySetOut: case HistoryKind.PartyJoined: case HistoryKind.HouseholdMoved:
                case HistoryKind.HomeBuilt: case HistoryKind.HomeTaken: case HistoryKind.TakenIn: case HistoryKind.LetGo: case HistoryKind.Scouted: case HistoryKind.Retired: return FamPeople;
                case HistoryKind.TributeAgreed: case HistoryKind.TributePaid: case HistoryKind.TributeEnded: case HistoryKind.TributeDemanded: case HistoryKind.Raid: case HistoryKind.BandRaised:
                case HistoryKind.ArmsMade: case HistoryKind.WallPlanned: case HistoryKind.WallBuilt: case HistoryKind.HeadmanRose: case HistoryKind.VillageSplit: case HistoryKind.Taken:
                case HistoryKind.TakingSeen: case HistoryKind.Struck: case HistoryKind.WatchSet: case HistoryKind.AmendsMade: case HistoryKind.ClaimRefused: case HistoryKind.Sworn:
                case HistoryKind.CallAnswered: case HistoryKind.FieldsHarried: return FamPower;
                case HistoryKind.OreRevealed: case HistoryKind.OreFound: case HistoryKind.OreTaken: case HistoryKind.OreGaveOut: case HistoryKind.WorkBegun: case HistoryKind.WorkBuilt:
                case HistoryKind.TechniqueFound: case HistoryKind.TechniqueKnown: case HistoryKind.StoneFound: case HistoryKind.BoatBuilt: case HistoryKind.CartMade: case HistoryKind.HomeRebuilt: return FamWorks;
                default: return FamOther;
            }
        }

        /// <summary>How the dead of a card died, a few words (PersonDied's ArgA).</summary>
        static string Died(int why)
        {
            switch (why)
            {
                case 14: return "killed by the stone";
                case 5: return "killed when the earth shook their homes down";
                case 4: return "killed in the eruption";
                case 6: return "drowned by the great wave";
                case 7: return "killed in the storm";
                case 8: return "drowned when the storm drove the sea ashore";
                case 9: return "died of the cold";
                case 2: return "died of hunger";
                case 10: return "died of sickness";
                case 11: return "died of wounds";
                case 12: return "drowned at sea";
                case 3: return "died in childbirth";
                default: return "died";
            }
        }

        static string Near(string place) => place == "the wilds" ? "in the wilds" : place.StartsWith("the land", System.StringComparison.Ordinal) ? "on " + place : "near " + place;
        static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        /// <summary>A card's title in plain words, from its kind, its count and its weightiest record; the record's own words where no title is written.</summary>
        static string Title(World w, ConsequenceNode n, in HistoryEvent e)
        {
            var H = w.History;
            int c = n.Count;
            string subj = H.NameOf(e.Subject), obj = H.NameOf(e.Object), at = HistoryText.At(n.Place), near = Near(n.Place);
            switch (n.Kind)
            {
                case HistoryKind.PersonDied:
                    return c == 1 ? subj + ", " + N((int)(double)e.Magnitude) + ", " + Died(e.ArgA) : N(c) + " people " + Died(e.ArgA) + " " + at;
                case HistoryKind.MeteorStruck: return c == 1 ? "A stone from the sky fell " + at : N(c) + " stones fell from the sky " + near;
                case HistoryKind.LightningStrike: return c == 1 ? "Lightning struck " + near : N(c) + " lightning strikes " + near;
                case HistoryKind.FireStarted: return c == 1 ? "A fire started " + near : N(c) + " fires started " + near;
                case HistoryKind.Wildfire: return c == 1 ? "The fire spread " + near : N(c) + " wildfires " + near;
                case HistoryKind.FireOut: return c == 1 ? "The fire " + near + " burnt out" : N(c) + " fires burnt out " + near;
                case HistoryKind.FieldsBurned: return c == 1 ? "The fields of " + subj + " burned" : "The fields of " + subj + " burned " + N(c) + " times";
                case HistoryKind.BuildingBurned: return c == 1 ? "A home burned " + at : N(c) + " homes burned " + at;
                case HistoryKind.CropsFlattened: return "The crops of " + subj + " flattened";
                case HistoryKind.CropsBlighted: return "The crop of " + subj + " blighted";
                case HistoryKind.FieldsFlooded: return "The sea over the fields of " + subj;
                case HistoryKind.AshFall: return "Ash on the fields of " + subj;
                case HistoryKind.FieldsHarried: return "The crop of " + subj + " burned by a band";
                case HistoryKind.Drought: return "Drought " + near;
                case HistoryKind.RainBrought: return "Rain " + near;
                case HistoryKind.WarmAir: return "Warm air " + near;
                case HistoryKind.ColdAir: return "Cold air " + near;
                case HistoryKind.ColdHomes: return "Cold homes " + at;
                case HistoryKind.Storm: return "A storm gathered " + near;
                case HistoryKind.StormLandfall: return "The storm came ashore " + near;
                case HistoryKind.StormSurge: return "The sea surged ashore " + near;
                case HistoryKind.Quake: return (c == 1 ? "The ground shook " : N(c) + " quakes ") + near + " (magnitude " + ((double)e.Magnitude).ToString("0.0", CultureInfo.InvariantCulture) + ")";
                case HistoryKind.Landslides: return (n.SumA == 1 ? "A slope slid " : N(n.SumA) + " slopes slid ") + near;
                case HistoryKind.StoresSpoiled: return "The stores of " + subj + " spoiled";
                case HistoryKind.Tsunami: return "A great wave struck " + near;
                case HistoryKind.Eruption: return subj + " erupted " + near;
                case HistoryKind.VentBorn: return "A new volcano opened " + near;
                case HistoryKind.HomesDrowned: return (n.SumA == 1 ? "A home" : N(n.SumA) + " homes") + " drowned " + at;
                case HistoryKind.PathogenAppeared: return "A new sickness, " + subj + ", " + at;
                case HistoryKind.EpidemicArrived: return "Sickness came to " + n.Place + (c > 1 ? " (" + N(c) + " times)" : "");
                case HistoryKind.EpidemicEnded: return "The sickness left " + n.Place;
                case HistoryKind.HarvestFailed: return c == 1 ? "The harvest failed at " + subj : N(c) + " harvests failed at " + subj;
                case HistoryKind.Harvest: return c == 1 ? subj + " brought in its harvest" : N(c) + " harvests at " + subj;
                case HistoryKind.Hunger: return "Hunger in " + subj;
                case HistoryKind.Famine: return "Famine in " + subj;
                case HistoryKind.Rationing: return subj + " rationed its grain";
                case HistoryKind.WildFood: return subj + " turned to the woods for food";
                case HistoryKind.ReliefSent: return c == 1 ? subj + " sent grain to " + obj : subj + " sent grain " + N(c) + " times";
                case HistoryKind.ReliefReturned: return subj + " gave back grain to " + obj;
                case HistoryKind.SoilRenewed: return "The fields of " + subj + " renewed";
                case HistoryKind.BeastsCulled: return "The " + subj + " culled " + near;
                case HistoryKind.BeastsBrought: return "The " + subj + " brought " + near;
                case HistoryKind.OreRevealed: return "Copper laid bare " + near;
                case HistoryKind.SettlementAbandoned: return subj + " was abandoned";
                case HistoryKind.SettlementFounded: return subj + " was founded";
                case HistoryKind.PartySetOut: return (n.SumA == 1 ? "A household" : N(n.SumA) + " households") + " set out from " + subj;
                case HistoryKind.PartyJoined: return "Settlers from " + subj + " joined " + obj;
                case HistoryKind.HouseholdMoved: return c == 1 ? "The " + HistoryText.Plural(subj) + " left " + n.Place + " for " + obj : N(c) + " households left " + n.Place;
                case HistoryKind.HomeBuilt: return c == 1 ? "A home raised again " + at : N(c) + " homes raised " + at;
                case HistoryKind.HomeTaken: return c == 1 ? "A household took an empty home " + at : N(c) + " households took empty homes " + at;
                case HistoryKind.OreFound: return subj + " found the ore";
                case HistoryKind.WorkBegun: return subj + " began a " + TC2.Core.Places.Structures.Name((byte)e.ArgA);
                case HistoryKind.WorkBuilt: return c == 1 ? subj + " built a " + TC2.Core.Places.Structures.Name((byte)e.ArgA) : subj + " built " + N(c) + " of " + TC2.Core.Places.Structures.Name((byte)e.ArgA);
                case HistoryKind.ArmsMade: return obj + " forged arms";
                case HistoryKind.TributeAgreed: return subj + " bowed to " + obj;
                case HistoryKind.TributePaid: return subj + " paid tribute to " + obj + (c > 1 ? " (" + N(c) + " times)" : "");
                case HistoryKind.Raid: return c == 1 ? "A raid on " + obj + " from " + subj : N(c) + " raids on " + obj;
                case HistoryKind.BandRaised: return "A band raised against " + obj;
                case HistoryKind.HeadmanRose: return subj + " rose to lead " + n.Place;
                case HistoryKind.VillageSplit: return n.Place + " split";
                case HistoryKind.JourneyInterrupted: return c == 1 ? subj + " saw the fire and turned" : N(c) + " journeys turned by the fire";
                case HistoryKind.FledDanger: return c == 1 ? subj + " fled" : N(c) + " people fled " + near;
                case HistoryKind.DangerNoticed: return c == 1 ? subj + " saw the danger" : N(c) + " people saw the danger " + near;
            }
            string d = HistoryText.Describe(w, in e).TrimEnd('.');
            if (d.Length > 96) d = d.Substring(0, 93) + "...";
            return c == 1 ? d : d + " (and " + N(c - 1) + " like it)";
        }

        /// <summary>One record of a card in a few words, for the list under the pointer: the dead by name and age, the rest by their own words.</summary>
        public static string MemberLine(World w, long id)
        {
            if (!w.History.TryGet(id, out var e)) return "(a record forgotten)";
            if (e.Kind == HistoryKind.PersonDied) return w.History.NameOf(e.Subject) + ", " + N((int)(double)e.Magnitude);
            string d = HistoryText.Describe(w, in e).TrimEnd('.');
            return d.Length > 110 ? d.Substring(0, 107) + "..." : d;
        }
    }
}
