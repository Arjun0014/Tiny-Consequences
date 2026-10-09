using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Knowledge;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>
    /// The settlement page (C12.2, D2-106; 20 §7): a place shown as a place - who founded it and why, its people and homes, its food and
    /// stores, what it has built, what it takes from the land (wood, stone, copper, bog ore, charcoal) and what it makes of it, what it knows,
    /// its neighbours (the ways worn to them, trade, tribute, the wrongs it holds and is held to), and its history as the ledger keeps it.
    /// Every line is read from state or a record; words like "hamlet" are read from its size. Shared by the HUD and the headless `place`
    /// command. Reads only.
    /// </summary>
    public static class Gazetteer
    {
        static string F(double v, string fmt = "0") => v.ToString(fmt, CultureInfo.InvariantCulture);
        static string Name(World w, int sid) => w.History.NameOf(EntityRef.Settlement(sid));
        static string Year(long t) => "year " + (t / SimTime.Year);

        /// <summary>What its size makes it: a hamlet (under 50 people), a village, a town (300 and more); abandoned when nobody lives there.</summary>
        public static string Word(in Settlement s) => !s.Inhabited ? "an abandoned place" : s.Population < 50 ? "a hamlet" : s.Population < 300 ? "a village" : "a town";

        public static string Title(World w, int sid)
        {
            if (!w.Settlements.Exists(sid)) return "";
            ref var s = ref w.Settlements.Get(sid);
            return Name(w, sid) + ", " + Word(in s) + (s.Inhabited ? " of " + s.Population + " people" : " since " + Year(s.Abandoned));
        }

        /// <summary>Founded when, by whom and why; its people, households and homes.</summary>
        public static string Overview(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var sb = new StringBuilder();
            sb.Append("Founded in ").Append(Year(s.Founded));
            bool origin = false;
            if (s.FoundedEvent != 0 && w.History.TryGet(s.FoundedEvent, out var fe) && fe.Kind == HistoryKind.SettlementFounded)
            {
                if (fe.Object.Kind == EntityKind.Settlement && fe.Object.Id != 0 && fe.Object.Id != sid) { sb.Append(" by people of ").Append(Name(w, fe.Object.Id)); origin = true; }
                if (fe.Cause1 != 0 && w.History.TryGet(fe.Cause1, out var fc))
                {
                    sb.Append(" (").Append(HistoryText.Describe(w, in fc).TrimEnd('.')).Append(')');
                    if (fc.Cause1 != 0 && w.History.TryGet(fc.Cause1, out var fs) && fs.Kind == HistoryKind.VillageSplit) sb.Append(": ").Append(HistoryText.Describe(w, in fs).TrimEnd('.'));   // (C10.3)
                }
            }
            if (!origin && s.Founded == 0) sb.Append(", among the first places of the land");
            sb.Append(".\n");
            if (!s.Inhabited) return sb.ToString();
            int houses = 0, huts = 0, footed = 0, camps = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Building == 0 || !w.Buildings.Exists(hh.Building)) { camps++; continue; }
                ref var b = ref w.Buildings.Get(hh.Building);
                if (b.Kind == Structures.House) { houses++; if (b.Mat >= 1) footed++; } else huts++;
            }
            sb.Append(s.Population).Append(" people in ").Append(hl.Count).Append(" households: ").Append(houses).Append(houses == 1 ? " house" : " houses")
              .Append(footed > 0 ? " (" + footed + " on stone footings)" : "").Append(", ").Append(huts).Append(huts == 1 ? " hut" : " huts").Append(camps > 0 ? ", " + camps + " in camps" : "").Append(".\n");
            if (!Society.Leaders.Off && s.HeadHh != 0 && w.Households.Exists(s.HeadHh))
            {
                // its first household and its headman (C10.2): who follows them and why
                int f = Society.Leaders.Following(w, sid, s.HeadHh, out int kin, out int owing, out int fed);
                sb.Append("Its first household: the ").Append(HistoryText.Plural(w.History.NameOf(EntityRef.Household(s.HeadHh)))).Append(", followed by ").Append(f).Append(f == 1 ? " household" : " households")
                  .Append(" (").Append(kin).Append(" of kin, ").Append(owing).Append(" owing them grain, ").Append(fed).Append(" fed by them or lent to lately)");
                if (s.Head != 0 && w.People.Exists(s.Head)) sb.Append("; first among its people ").Append(w.History.NameOf(EntityRef.Person(s.Head))).Append(", ").Append(F(w.People.Get(s.Head).AgeYears(w.Now), "0")).Append(", since ").Append(Year(s.HeadSince));
                sb.Append(".\n");
            }
            {
                // its splits (C10.3): factions that would not follow a new first and left to found villages of their own
                var pe = w.History.PlaceEventsOf(sid);
                int splits = 0; HistoryEvent last = default;
                for (int i = 0; pe != null && i < pe.Count; i++) if (w.History.TryGet(pe[i], out var e) && e.Kind == HistoryKind.VillageSplit) { splits++; last = e; }
                if (splits > 0)
                {
                    int to = SplitTo(w, sid, last.Id);
                    sb.Append(splits == 1 ? "Split in " : "Split " + splits + " times, the last in ").Append(Year(last.Time)).Append(": ").Append(HistoryText.Describe(w, in last).TrimEnd('.'))
                      .Append(to != 0 ? " - " + Name(w, to) : "").Append(".\n");
                }
            }
            if (SettlementSystem.MeansCount)
            {
                // its matches waiting for the means of a household (C3.5): the unmarried women of an age to marry whose share of their household's
                // grain could not keep them to the harvest, and what a mouth holds here
                int unwed = 0, waiting = 0, toHarvest = w.SettlementSys.ToHarvest(sid);
                var res = w.Residents(sid);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var q = ref w.People.Get(res[i]);
                    if (!q.Alive || !q.IsFemale || q.Partner != 0 || q.AgeYears(w.Now) < 18 || q.AgeYears(w.Now) > 36) continue;
                    unwed++;
                    if (w.SettlementSys.OwnDays(res[i]) < toHarvest + Holdings.Reserve) waiting++;
                }
                if (waiting > 0)
                {
                    double food = 0, need = 0;
                    var hs = w.HouseholdsOf(sid);
                    for (int i = 0; i < hs.Count; i++) { ref var hh = ref w.Households.Get(hs[i]); food += hh.Grain + hh.Larder; need += w.SettlementSys.HouseholdNeed(hs[i]); }
                    sb.Append("Marriages wait: ").Append(waiting).Append(" of its ").Append(unwed).Append(" unmarried women of an age to marry could not yet keep a household - its households' own grain keeps a mouth ")
                      .Append(HistoryText.Span(need > 0 ? food / need : 0)).Append(" on average, the harvest is ").Append(toHarvest).Append(toHarvest == 1 ? " day off.\n" : " days off.\n");
                }
            }
            if (s.Wall == 2) sb.Append("Walled: a palisade ").Append(F(2.0 * DMath.PI * (double)s.WallR, "0")).Append(" m round").Append(s.OldR > 0f ? ", an older ring inside" : "").Append(".\n");
            else if (s.Wall == 1) sb.Append("Raising a palisade.\n");
            // its streets and their core (C13.1-C13.3): rows of narrow town houses on the divided plots, the market place its traders widened
            if (s.StreetN > 0)
            {
                int rows = 0, fronting = 0;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (!Structures.IsHome(b.Kind) || Homes.Fallen(in b, w.Now)) continue;
                    if (Streets.Facing(in s, b.X, b.Y, out _, out _)) fronting++;
                    if (b.Kind >= Structures.House && Architecture.Has(b.Form) && Architecture.PlanOf(b.Form) == Architecture.Row) rows++;
                }
                sb.Append(s.StreetN).Append(s.StreetN == 1 ? " street" : " streets").Append(", ").Append(fronting).Append(" homes along its streets and lanes");
                if (s.CoreR > 0f) sb.Append("; its frontage divided into narrow plots ").Append(F((double)s.CoreR, "0")).Append(" m out from the green, ").Append(rows).Append(rows == 1 ? " narrow town house" : " narrow town houses");
                sb.Append(".\n");
            }
            if (s.MarketR > 0f) sb.Append("A market place ").Append(F(2.0 * (double)s.MarketR, "0")).Append(" m across, widened by the traders who came.\n");
            return sb.ToString();
        }

        /// <summary>Fields, the store against the harvest, its granaries and barns, the hunger it has known.</summary>
        public static string Food(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var sb = new StringBuilder();
            double days = Trade.DaysOfFood(w, in s, out _);
            sb.Append(F(s.FieldHa, "0")).Append(" ha of fields (").Append(s.Crop.ToString().ToLowerInvariant()).Append("); last harvest ").Append(F(s.LastHarvest, "0")).Append(" days' food.\n");
            sb.Append("The store holds ").Append(F(s.Granary, "0")).Append(" days' food (").Append(F(days, "0")).Append(" days for its people), in ")
              .Append(s.Granaries).Append(s.Granaries == 1 ? " granary" : " granaries").Append(s.Barns > 0 ? " and " + s.Barns + (s.Barns == 1 ? " barn" : " barns") : "")
              .Append(" holding ").Append(F(Structures.StoreCapacity(in s), "0")).Append(s.Ration < 0.999 ? "; meals rationed to " + F(100 * s.Ration, "0") + " %" : "").Append(".\n");
            // its fishing and its boats (C9.1)
            if (s.Fish.A >= 0)
            {
                int boats = 0, planks = 0, making = 0, hulks = 0;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (!Boats.IsBoat(b.Kind)) continue;
                    if (b.Done == 0 && b.Left == 0) making++; else if (Boats.Sound(in b, w.Now)) { if (Boats.IsPlank(b.Kind)) planks++; else boats++; } else if (b.Done != 0) hulks++;
                }
                double ldx = (double)s.LandX - s.SquareX, ldy = (double)s.LandY - s.SquareY;
                string fleet = (boats > 0 ? boats + (boats == 1 ? " dugout" : " dugouts") : "") + (boats > 0 && planks > 0 ? " and " : "") + (planks > 0 ? planks + (planks == 1 ? " plank boat" : " plank boats") : "");
                sb.Append("It fishes from the shore").Append(fleet.Length > 0 ? " and from " + fleet + " at its landing" : "")
                  .Append(s.Shed != 0 ? (fleet.Length > 0 ? ", built in its boat shed" : "; it has a boat shed") : "")
                  .Append(s.LandAt != 0 ? " (the landing " + F(DMath.Sqrt(ldx * ldx + ldy * ldy), "0") + " m from the square, since " + Year(s.LandAt) + ")" : "")
                  .Append(making > 0 ? " (" + making + " being made)" : "").Append(hulks > 0 ? "; " + hulks + " old hulks on the beach" : "")
                  .Append(s.Sea.A >= 0 ? "; an hour at sea is worth " + F(Boats.SeaExpect(w, in s), "0.0") + " days' food against " + F(s.Fish.Expect, "0.0") + " on the shore" : "").Append(".\n");
            }
            if (s.FamineAt != 0) sb.Append("Last famine: ").Append(Year(s.FamineAt)).Append(" (").Append(F(100 * s.Memory, "0")).Append(" % of its grown people lived through it).\n");
            return sb.ToString();
        }

        /// <summary>The structures it has standing, each with the year it was raised; those being raised; those left.</summary>
        public static string Works(World w, int sid)
        {
            var sb = new StringBuilder();
            var bl = w.BuildingsOf(sid);
            int left = 0;
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (Structures.IsHome(b.Kind) || b.Kind == Structures.Boat) continue;   // (boats are the households': Food)
                if (b.Left != 0 || !Structures.Working(in b, w.Now)) { if (b.Done != 0) left++; continue; }
                sb.Append(Structures.Name(b.Kind)).Append(" (").Append(Year(b.Built)).Append(")\n");
            }
            ref var s = ref w.Settlements.Get(sid);
            if (s.Project != 0 && w.Buildings.Exists(s.Project)) sb.Append("being raised: ").Append(HistoryText.WorkWhy(s.ProjectKind, s.Granaries)).Append('\n');
            if (left > 0) sb.Append(left).Append(left == 1 ? " work left or fallen in\n" : " works left or fallen in\n");
            return sb.Length > 0 ? sb.ToString() : "No works yet.\n";
        }

        /// <summary>What it takes from the land and what it makes of it: wood, stone, copper, bog ore, charcoal, tools and arms.</summary>
        public static string Materials(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var sb = new StringBuilder();
            {
                // its carts (C4.2): handcarts and ox carts among its households
                int hand = 0, ox = 0, making = 0; var hc = w.HouseholdsOf(sid);
                for (int i = 0; i < hc.Count; i++)
                {
                    var h = w.Households.Get(hc[i]);
                    if (h.CartState == Carts.Made && !Carts.WornOut(in h, w.Now)) { if (h.CartKind == Carts.Ox) ox++; else hand++; }
                    else if (h.CartState == Carts.Making) making++;
                }
                if (hand + ox + making > 0) sb.Append("Carts: ").Append(hand).Append(hand == 1 ? " handcart" : " handcarts").Append(ox > 0 ? " and " + ox + (ox == 1 ? " ox cart" : " ox carts") : "").Append(" among its households").Append(making > 0 ? ", " + making + " being made" : "").Append(".\n");
            }
            if (!Holdings.Off) sb.Append(Holdings.VillageLine(w, sid));   // (whose grain its store holds, its debts: C3.3)
            sb.Append("Wood: ").Append(F(s.Woodstore, "0")).Append(" loads in store; ").Append(Woods.Lot(in s) >= 0 ? (s.WoodDist < 100f ? "it fells by its homes" : "it fells " + F(s.WoodDist, "0") + " m out") : "no woods left within reach").Append(".\n");
            if (s.StoneCell > 0) sb.Append("Stone: rock known ").Append(F(s.StoneDist, "0")).Append(" m out").Append(Stone.Quarry(w, sid) != 0 ? ", quarried" : "").Append("; ").Append(F(s.Stone, "0")).Append(" loads in store.\n");
            Ore(w, sid, Mining.Copper, sb);
            Ore(w, sid, Mining.Iron, sb);
            if (s.Charcoal > 0 || Charcoal.Site(w, sid) != 0) sb.Append("Charcoal: ").Append(F(s.Charcoal, "0")).Append(" kg in store").Append(Charcoal.Site(w, sid) != 0 ? ", burnt at its hearth by the woods" : "").Append(s.ClampUntil > w.Now ? " (a clamp smouldering)" : "").Append(".\n");
            sb.Append("Tools: metal for ").Append(F(100 * Tech.ToolShare(in s), "0")).Append(" % of its people (").Append(F(s.Metal, "0")).Append(" kg of copper, ").Append(F(s.Iron, "0")).Append(" kg of iron)");
            if (s.Arms > 0.5) sb.Append("; arms ").Append(F(s.Arms, "0")).Append(s.IronArms > 0.5 ? " (" + F(s.IronArms, "0") + " of iron)" : "");
            sb.Append(".\n");
            if (Forge.Smithy(w, sid) != 0)
            {
                // its smithy's smiths (C1.4b): the village's best, who have its four places
                int smiths = 0, practised = 0; var sres = w.Residents(sid);
                for (int i = 0; i < sres.Count; i++) { ref var q = ref w.People.Get(sres[i]); if (Forge.Maker(in s, in q, w.Now)) { smiths++; if (q.SkSmith >= 0.3f) practised++; } }
                double hours = 0; var sbl = w.BuildingsOf(sid);
                for (int i = 0; i < sbl.Count; i++) { ref var sb2 = ref w.Buildings.Get(sbl[i]); if (sb2.Kind == Structures.Smithy && Structures.Working(in sb2, w.Now)) hours += sb2.Use; }
                sb.Append(s.Smithies > 1 ? "Its " + s.Smithies + " smithies: " : "Its smithy: ").Append(smiths).Append(smiths == 1 ? " smith" : " smiths").Append(" (its best; ").Append(practised).Append(" with a practised hand); ")
                  .Append(F(hours, "0")).Append(" hours worked there").Append(s.SmithShort > 0 ? "; its smiths could not keep up on " + s.SmithShort + " days this year" : "").Append(".\n");
            }
            if (s.IronSold > 1f) sb.Append("It sold ").Append(F(s.IronSold, "0")).Append(" kg of iron lately (halving each year).\n");
            // cloth and goods (C3.1): what its households wear, its wares, its weaver's shed and who works there
            sb.Append("Cloth and goods: fibre ").Append(F(s.Fibre, "0")).Append(" kg in store; ");
            if (s.Workshop != 0 || s.Wares > 0.05 || s.WaresHeld > 0.5f)
            {
                sb.Append(F(s.Wares, "0.0")).Append(" wares in store").Append(Weaving.Shed(w, sid) != 0 ? " (its makers keep " + F(Weaving.Target(in s), "0") + ")" : "")
                  .Append(", its households wear wares for ").Append(F(100 * DMath.Clamp01((double)s.WaresHeld / (Goods.Target * System.Math.Max(1, s.Population))), "0")).Append(" % of its people");
                if (s.WaresOut > 0.5f) sb.Append("; they took ").Append(F(s.WaresOut, "0")).Append(" from the store lately");
                if (s.WaresSold > 0.5f) sb.Append("; it sold ").Append(F(s.WaresSold, "0")).Append(" lately");
                sb.Append(" (halving each year).\n");
                if (Weaving.Shed(w, sid) != 0)
                {
                    int makers = 0; var res = w.Residents(sid);
                    for (int i = 0; i < res.Count; i++) if (Weaving.Maker(in s, in w.People.Get(res[i]), w.Now)) makers++;
                    double made = 0, hours = 0; var bl = w.BuildingsOf(sid);
                    for (int i = 0; i < bl.Count; i++) { var b = w.Buildings.Get(bl[i]); if (b.Kind == Structures.Workshop) { made += b.Stock; hours += b.Use; } }
                    sb.Append(s.Sheds > 1 ? "Its " + s.Sheds + " weaver's sheds: " : "Its weaver's shed: ").Append(makers).Append(makers == 1 ? " maker" : " makers").Append(" at its looms (its best weavers); ").Append(F(made, "0")).Append(" wares made there in ").Append(F(hours, "0")).Append(" hours")
                      .Append(s.ShedShort > 0 ? "; its makers fell short " + s.ShedShort + " days this year" : "").Append(".\n");
                }
            }
            else sb.Append("its households make their own.\n");
            // pots and jars (C3.2): the clay within reach, the jars that keep its grain, its hearths' pots, its kiln and who works there
            {
                double held = System.Math.Min(s.Granary, Structures.StoreCapacity(in s));
                bool clay = w.Learning.Clay(s.Cell);
                sb.Append("Pots and jars: ").Append(clay ? "clay within reach" : "no clay within reach").Append("; ").Append(F(s.Jars, "0")).Append(" kg of jars in its granaries keep ")
                  .Append(F(100 * Pottery.Sealed(in s), "0")).Append(" % of the ").Append(F(held, "0")).Append(" days' grain there sealed; ").Append(F(s.Pots, "0")).Append(" kg of ware in store");
                int own = 0, kiln = 0, none = 0; var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    if (Pottery.FiresOwn(w, sid, Pottery.Best(w, hl[i]))) own++;
                    else if ((double)w.Households.Get(hl[i]).Pots > 0.5 * w.Members(hl[i]).Count) kiln++; else none++;
                }
                sb.Append("; its hearths: ").Append(own).Append(" fire their own pots, ").Append(kiln).Append(" cook in kiln pots, ").Append(none).Append(" in none");
                if (s.PotsSold > 0.5f) sb.Append("; it sold ").Append(F(s.PotsSold, "0")).Append(" kg lately (halving each year)");
                sb.Append(".\n");
                if (Pottery.Kiln(w, sid) != 0)
                {
                    int makers = 0; var res = w.Residents(sid);
                    for (int i = 0; i < res.Count; i++) if (Pottery.Maker(in s, in w.People.Get(res[i]), w.Now)) makers++;
                    var kb = w.Buildings.Get(s.Kiln);
                    sb.Append("Its kiln at the clay, ").Append(F(Pottery.Dist(w, in s, s.Kiln), "0")).Append(" m out: ").Append(makers).Append(makers == 1 ? " potter" : " potters").Append(" (its best); ")
                      .Append(F(kb.Stock, "0")).Append(" kg fired there in ").Append(F(kb.Use, "0")).Append(" hours.\n");
                }
            }
            return sb.ToString();
        }

        static void Ore(World w, int sid, byte kind, StringBuilder sb)
        {
            ref var s = ref w.Settlements.Get(sid);
            int cell = Mining.CellOf(in s, kind);
            double store = Mining.OreIn(in s, kind);
            if (cell == 0 && store <= 0) return;
            sb.Append(kind == Mining.Iron ? "Bog ore: " : "Copper: ");
            if (cell != 0)
            {
                int di = w.Deposits.IndexAt(cell - 1, kind);
                int owner = Mining.WorkedBy(w, cell - 1, kind);
                sb.Append("a ").Append(kind == Mining.Iron ? "bed" : "body").Append(" known ").Append(F(Mining.DistOf(in s, kind), "0")).Append(" m out");
                if (di >= 0) sb.Append(" (about ").Append(F(w.Deposits.Get(di).Ore / 1000.0, "0.0")).Append(" t left)");
                if (owner == sid) sb.Append(", worked by its people");
                else if (owner != 0) sb.Append(", worked by ").Append(Name(w, owner));
                sb.Append("; ");
            }
            sb.Append(F(store, "0")).Append(" kg of ore in store.\n");
        }

        /// <summary>The techniques its people carry, by its best practitioner.</summary>
        public static string Crafts(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var sb = new StringBuilder();
            for (int t = 0; t < Tech.Count; t++)
            {
                double best = Learning.Best(in s, t);
                if (best < 0.1) continue;
                sb.Append(sb.Length > 0 ? "; " : "").Append(Tech.Laws[t].Now).Append(" (best ").Append(F(best, "0.00")).Append((s.TechKnown & (1 << t)) != 0 ? "" : ", not yet competent").Append(")");
            }
            return sb.Length > 0 ? "Its people " + sb + ".\n" : "No craft beyond the everyday.\n";
        }

        /// <summary>The ways worn to its neighbours, traders coming, tribute, and the wrongs between it and others still weighed.</summary>
        public static string Neighbours(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var sb = new StringBuilder();
            var ways = new List<Way>();
            w.Ways.Collect(w.Now, ways);
            for (int i = 0; i < ways.Count; i++)
            {
                var wy = ways[i];
                if (wy.A != sid && wy.B != sid) continue;
                byte st = Ways.StateOf(wy.Use);
                if (st < 2) continue;
                sb.Append(st >= 3 ? "A road" : "A path").Append(" to ").Append(Name(w, wy.A == sid ? wy.B : wy.A)).Append(".\n");
            }
            if (s.TradeIn >= 1f) sb.Append("Traders come ").Append(F(s.TradeIn, "0")).Append(" times a year").Append(s.Market != 0 ? " to its market" : "").Append(".\n");
            if (s.Lord != 0) sb.Append("It pays tribute to ").Append(Name(w, s.Lord)).Append(" since ").Append(Year(s.LordSince)).Append(".\n");
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (t.Lord == sid && t.Inhabited) sb.Append(Name(w, o)).Append(" pays it tribute.\n");
                double held = w.DeedBook.Grievance(sid, o), against = w.DeedBook.Grievance(o, sid);
                if (held >= 1.0) sb.Append("It holds wrongs against ").Append(Name(w, o)).Append(" weighing ").Append(F(held, "0")).Append(" days' food.\n");
                if (against >= 1.0) sb.Append(Name(w, o)).Append(" holds wrongs against it weighing ").Append(F(against, "0")).Append(" days' food.\n");
            }
            return sb.Length > 0 ? sb.ToString() : "No worn ways, trade or wrongs with its neighbours.\n";
        }

        /// <summary>Its history as the ledger keeps it: the last `max` records under it, oldest first.</summary>
        public static string Life(World w, int sid, int max = 60)
        {
            var evs = w.History.EventsOf(EntityRef.Settlement(sid));
            var pe = w.History.PlaceEventsOf(sid);   // (its headmen and splits: C10.2, C10.3)
            int ne = evs == null ? 0 : evs.Count, np = pe == null ? 0 : pe.Count;
            if (ne + np == 0) return "Nothing of it is kept in the history.\n";
            // both lists are in id order: merged, the last `max`
            var ids = new System.Collections.Generic.List<long>(ne + np);
            for (int i = 0, j = 0; i < ne || j < np;)
            {
                long id = j >= np || (i < ne && evs[i] <= pe[j]) ? evs[i++] : pe[j++];
                if (ids.Count == 0 || ids[ids.Count - 1] != id) ids.Add(id);
            }
            var sb = new StringBuilder();
            int from = System.Math.Max(0, ids.Count - max);
            if (from > 0) sb.Append("(... ").Append(from).Append(" earlier records)\n");
            for (int i = from; i < ids.Count; i++) if (w.History.TryGet(ids[i], out var e)) sb.Append(HistoryText.Line(w, e)).Append('\n');
            return sb.ToString();
        }

        /// <summary>The village a split founded (0 if none is kept): the party that set out from `sid` for the split, and the village it founded.</summary>
        static int SplitTo(World w, int sid, long split)
        {
            var evs = w.History.EventsOf(EntityRef.Settlement(sid));
            if (evs == null) return 0;
            long setOut = 0;
            for (int i = 0; i < evs.Count; i++)
            {
                if (evs[i] <= split || !w.History.TryGet(evs[i], out var e)) continue;
                if (setOut == 0 && e.Kind == HistoryKind.PartySetOut && e.Cause1 == split) setOut = e.Id;
                else if (setOut != 0 && e.Kind == HistoryKind.SettlementFounded && e.Cause1 == setOut) return e.Settlement;
            }
            return 0;
        }

        /// <summary>The whole page as text (headless `place`, tests).</summary>
        public static string All(World w, int sid)
        {
            if (!w.Settlements.Exists(sid)) return "no such place\n";
            var sb = new StringBuilder();
            sb.Append("== ").Append(Title(w, sid)).Append('\n').Append(Overview(w, sid));
            ref var s = ref w.Settlements.Get(sid);
            if (s.Inhabited)
            {
                sb.Append("-- food\n").Append(Food(w, sid));
                sb.Append("-- works\n").Append(Works(w, sid));
                sb.Append("-- land and making\n").Append(Materials(w, sid));
                sb.Append("-- knowledge\n").Append(Crafts(w, sid));
                sb.Append("-- neighbours\n").Append(Neighbours(w, sid));
            }
            sb.Append("-- history\n").Append(Life(w, sid, 40));
            return sb.ToString();
        }
    }
}
