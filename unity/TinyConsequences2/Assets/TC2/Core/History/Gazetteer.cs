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
        static string And(List<string> l) => l.Count == 1 ? l[0] : string.Join(", ", l.GetRange(0, l.Count - 1).ToArray()) + " and " + l[l.Count - 1];

        /// <summary>What its size makes it: a hamlet (under 50 people), a village, a town (300 and more); abandoned when nobody lives there.</summary>
        /// <summary>(session 32) Its tenth set aside, in days of its people's food ("0 days' food" had stood for less than one).</summary>
        static string TenthDays(World w, in Settlement s)
        {
            Places.Trade.DaysOfFood(w, in s, out double perDay);
            double d = s.TributeDue / System.Math.Max(1e-6, perDay);
            return d < 1.0 ? "less than a day's food" : d < 1.5 ? "a day's food" : F(d, "0") + " days' food";
        }

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
                HistoryEvent fc = default;
                bool cause = fe.Cause1 != 0 && w.History.TryGet(fe.Cause1, out fc), party = cause && fc.Kind == HistoryKind.PartySetOut;
                // (session 27, context 4) the party that founded it told in the sentence - "by 29 people of Dreford, 8 households who had set out to make a
                // new home": it had read "by people of Dreford (8 households of Dreford (29 people) set out to make a new home)"
                if (fe.Object.Kind == EntityKind.Settlement && fe.Object.Id != 0 && fe.Object.Id != sid) { sb.Append(" by ").Append(party ? fc.ArgB + (fc.ArgB == 1 ? " person" : " people") : "people").Append(" of ").Append(Name(w, fe.Object.Id)); if (fe.ArgA == 1) sb.Append(" on the margins of its land"); origin = true; }   // (a daughter village: C3.6g)
                if (cause)
                {
                    if (party && origin) sb.Append(", ").Append(fc.ArgA).Append(fc.ArgA == 1 ? " household that had" : " households who had").Append(" set out to make a new home");
                    else if (fc.Kind == HistoryKind.PeopleArrived) { sb.Append(" by the first people to come to the land"); origin = true; }   // ("(People came to the place that would be X)" had stood)
                    else sb.Append(" (").Append(HistoryText.Describe(w, in fc).TrimEnd('.')).Append(')');
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
            // (session 27, context 4) where its households live, in words: "189 households: 177 in houses (all on stone footings) and 12 in camps" - it had
            // read "177 houses (177 on stone footings), 12 in camps"
            var homes = new List<string>();
            if (houses > 0) homes.Add(houses + (houses == 1 ? " in a house" + (footed > 0 ? " on stone footings" : "") : " in houses" + (footed == houses ? " (all on stone footings)" : footed > 0 ? " (" + footed + " of them on stone footings)" : "")));
            if (huts > 0) homes.Add(huts + (huts == 1 ? " in a hut" : " in huts"));
            if (camps > 0) homes.Add(camps + (camps == 1 ? " in a camp" : " in camps"));
            sb.Append(s.Population).Append(s.Population == 1 ? " person in " : " people in ").Append(hl.Count == 1 ? "one household" : hl.Count + " households");
            if (homes.Count == 1) { string only = homes[0].Substring(homes[0].IndexOf(' ')); sb.Append(hl.Count == 1 ? "," + only : ", all" + only); }
            else if (homes.Count > 1) sb.Append(": ").Append(And(homes));
            sb.Append(".\n");
            if (!Society.Leaders.Off && s.HeadHh != 0 && w.Households.Exists(s.HeadHh))
            {
                // its first household and its headman (C10.2): who follows them and why
                int f = Society.Leaders.Following(w, sid, s.HeadHh, out int kin, out int owing, out int fed);
                // (session 27, context 4) only the bonds there are - "followed by 0 households (0 of kin, 0 owing them grain, 0 fed ...)" had been read out
                var why = new List<string>();
                if (kin > 0) why.Add(kin + " of kin");
                if (owing > 0) why.Add(owing + " owing them grain");
                if (fed > 0) why.Add(fed + " fed by them or lent to lately");
                sb.Append("Its first household: the ").Append(HistoryText.Plural(w.History.NameOf(EntityRef.Household(s.HeadHh))))
                  .Append(f == 0 ? ", followed by no other household yet" : ", followed by " + f + (f == 1 ? " household" : " households") + (why.Count > 0 ? " (" + string.Join(", ", why.ToArray()) + ")" : ""));
                if (s.Head != 0 && w.People.Exists(s.Head)) sb.Append("; first among its people ").Append(w.History.NameOf(EntityRef.Person(s.Head))).Append(", ").Append(F(w.People.Get(s.Head).AgeYears(w.Now), "0")).Append(", since ").Append(Year(s.HeadSince));
                sb.Append(".\n");
                // its sworn men (C10.7b): fed from the first household's grain - a lord's from its tribute
                int sworn = Society.Retinue.Count(w, sid);
                if (sworn > 0 && s.Head != 0)
                    sb.Append(sworn).Append(sworn == 1 ? " man is" : " men are").Append(" sworn to ").Append(w.History.NameOf(EntityRef.Person(s.Head))).Append(", fed from the ")
                      .Append(HistoryText.Plural(w.History.NameOf(EntityRef.Household(s.HeadHh)))).Append("' grain").Append(w.Conflict.PolitySize(sid) > 0 ? " - the tribute of the villages that pay it" : "")
                      .Append(": they practise arms at its ground every day and go with its bands.\n");
                // its first household's dependants (C10.11): landless households kept from its grain - a lord's from its realm too
                if (s.HeadHh != 0 && !Society.Hall.Off)
                {
                    int kept = 0, came = 0;
                    var keptHl = w.HouseholdsOf(sid);
                    for (int i = 0; i < keptHl.Count; i++) { ref var kh = ref w.Households.Get(keptHl[i]); if (kh.Keeper != s.HeadHh || kh.Dissolved != 0) continue; kept++; }
                    for (int hid = w.Households.First(); hid != 0 && w.Conflict.PolitySize(sid) > 0; hid = w.Households.Next(hid)) { ref var kh = ref w.Households.Get(hid); if (kh.Keeper == s.HeadHh && kh.Dissolved == 0 && kh.Settlement != sid) came++; }
                    if (kept + came > 0)
                        sb.Append(kept + came).Append(kept + came == 1 ? " landless household lives" : " landless households live").Append(" in the keep of the ").Append(HistoryText.Plural(w.History.NameOf(EntityRef.Household(s.HeadHh))))
                          .Append(", fed from their grain").Append(came > 0 ? " (" + came + " on their way from the villages that pay it)" : "").Append(".\n");
                }
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
            // (C7.2) a ring of stone, a palisade being laid in stone, the old rings inside
            string older = s.OldR > 0f ? (s.OldStone >= 1f ? ", an older ring of stone inside" : s.OldStone > 0f ? ", an older ring inside, part of it stone" : ", an older ring inside") : "";
            if (s.Wall == 2 && s.StoneWall >= 1f) sb.Append("Walled in stone: a dry-stone curtain ").Append(F(2.0 * DMath.PI * (double)s.WallR, "0")).Append(" m round").Append(older).Append(".\n");
            else if (s.Wall == 2 && s.StoneWall > 0f) sb.Append("Walled: a palisade ").Append(F(2.0 * DMath.PI * (double)s.WallR, "0")).Append(" m round, ").Append(F(100.0 * s.StoneWall, "0")).Append(" % of it laid in stone").Append(TC2.Core.Places.Construction.Laying(in s) ? " and the rest being laid" : "").Append(older).Append(".\n");
            else if (s.Wall == 2) sb.Append("Walled: a palisade ").Append(F(2.0 * DMath.PI * (double)s.WallR, "0")).Append(" m round").Append(TC2.Core.Places.Construction.Laying(in s) ? ", its people beginning to lay it in stone" : "").Append(older).Append(".\n");
            else if (s.Wall == 1) sb.Append("Raising a palisade").Append(older).Append(".\n");
            else if (s.OldR > 0f && s.OldStone > 0f) sb.Append("An old ring of stone stands about it, its timber fallen.\n");
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
                sb.Append(s.StreetN).Append(s.StreetN == 1 ? " street" : " streets").Append(", ").Append(fronting == 0 ? "no homes along them yet" : fronting == 1 ? "one home along " + (s.StreetN == 1 ? "it" : "its streets and lanes") : fronting + " homes along " + (s.StreetN == 1 ? "it" : "its streets and lanes"));   // ("0 homes along" had stood)
                if (s.CoreR > 0f) sb.Append("; its frontage divided into narrow plots ").Append(F((double)s.CoreR, "0")).Append(" m out from the green, ").Append(rows == 0 ? "no narrow town house on them yet" : rows == 1 ? "one narrow town house" : rows + " narrow town houses");
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
            if (s.Crop == CropStage.Ripe && s.CropYield > 0)
                sb.Append("The harvest is coming in: ").Append(F(s.Reaped, "0")).Append(" days' food reaped of the ").Append(F(s.CropYield * (1.0 - s.CropBurnt), "0")).Append(" its fields bear.\n");
            if (s.Tilth > 0)
            {
                // working the land harder (C3.6): the land within reach against what its people want, the extra hours and what they gave
                double want = s.Population * Places.SettlementSystem.HaPerHead(w, in s) * (1.0 + 0.5 * s.Memory);
                sb.Append("Its arable within reach (").Append(F(s.ArableHa, "0")).Append(" ha) is short of the ").Append(F(want, "0")).Append(" ha its people would sow: they plough and hoe each hectare harder this year, ")
                  .Append(F(100.0 * s.Tilth, "0")).Append(" % more hours").Append(s.TilthGain > 0f ? " (the last harvest " + F(100.0 * s.TilthGain, "0") + " % larger for it)" : "").Append(".\n");
            }
            else if (s.TilthGain > 0f) sb.Append("Its last harvest was ").Append(F(100.0 * s.TilthGain, "0")).Append(" % larger for the extra ploughing and hoeing.\n");
            // the fields' heart and their rest (C3.6f)
            double heart = Places.SettlementSystem.Heart(in s);
            if (!Places.SettlementSystem.SoilOff && s.ArableHa > 0 && (heart < 0.95 || s.Rest > 0.01f))
                sb.Append("Its fields are ").Append(heart < 0.6 ? "worn" : heart < 0.85 ? "tired" : "a little tired").Append(": a sown hectare bears ").Append(F(100.0 * heart, "0"))
                  .Append(" % of what new land would").Append(s.Rest > 0.01f ? "; " + F(100.0 * s.Rest, "0") + " % of its arable rests this year" : "").Append(".\n");
            // (session 32: "in 0 granaries holding 0" and "in 0 granaries and 1 barn holding 40000" had stood)
            string stores = (s.Granaries == 0 ? "" : s.Granaries == 1 ? "a granary" : s.Granaries + " granaries") + (s.Granaries > 0 && s.Barns > 0 ? " and " : "") + (s.Barns == 0 ? "" : s.Barns == 1 ? "a barn" : s.Barns + " barns");
            sb.Append("The store holds ").Append(F(s.Granary, "N0")).Append(" days' food (").Append(F(days, "0")).Append(" days for its people), ")
              .Append(stores.Length == 0 ? "kept in its homes: it has no granary yet" : "in " + stores + " with room for " + F(Structures.StoreCapacity(in s), "N0"))
              .Append(s.Ration < 0.999 ? "; meals rationed to " + F(100 * s.Ration, "0") + " %" : "").Append(".\n");
            if (!Places.Muck.Off && (s.Muck > 500.0 || s.Spread > 500.0))   // (C3.7c: its yards' muck)
                sb.Append("Its yards hold ").Append(F(s.Muck / 1000.0, "0.0")).Append(" t of muck").Append(s.Spread > 500.0 ? "; " + F(s.Spread / 1000.0, "0.0") + " t spread on its fields since the harvest - carted out, and dropped by its herds on the stubble and the fallow (" + F(Places.Muck.TonnesPerHa(in s), "0.0") + " t a hectare sown)" : "").Append(".\n");
            if (!Places.Stubble.Off && s.Crop == Places.CropStage.Harvested && s.Stubble > 100.0)   // (C3.7d: its herds on its stubble)
                sb.Append("Its herds graze its reaped fields until the sowing: ").Append(F(s.Stubble / 1000.0, "0.0")).Append(" t of the stubble's forage left.\n");
            if (s.BurialCount > 0)   // (D2-151: its burial ground)
            {
                Places.Burials.Read(in s, w.Now, out double marked, out double fresh);
                sb.Append("Its burial ground: ").Append(s.BurialCount).Append(s.BurialCount == 1 ? " burial" : " burials").Append(" since it was founded, some ").Append(F(System.Math.Min(Places.Burials.Slots, marked), "0"))
                  .Append(" graves still marked").Append(fresh >= 0.5 ? ", " + F(fresh, "0") + " of them fresh earth" : "").Append(".\n");
            }
            if (s.Sheaves > 0.5)   // (C3.7b: the harvest stands in the sheaf until its people thresh it)
                sb.Append("Of it ").Append(F(s.Sheaves, "0")).Append(" stands in the sheaf").Append(s.Sheaves > s.Barns * Places.Threshing.BarnSheaves + 0.15 * Places.Threshing.RickFu ? (s.Barns > 0 ? " - in the barns' bays and in ricks in the stackyard" : " in ricks in the stackyard") : " in the barns' bays")
                  .Append("; ").Append(F(Places.Threshing.Threshed(in s), "0")).Append(" is threshed (").Append(F(Places.Threshing.Threshed(in s) / System.Math.Max(1.0, Places.Threshing.DayNeed(w, in s)), "0")).Append(" days for its people).\n");
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
            if (s.FamineAt != 0) sb.Append("Last famine: ").Append(Year(s.FamineAt)).Append(" (").Append(F(100 * s.Memory, "0")).Append(" % of its grown people have known famine).\n");
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
            sb.Append(Holdings.LandLine(w, sid));   // (how its fields are held: C3.6b)
            sb.Append("Wood: ").Append(F(s.Woodstore, "0")).Append(" loads in store; ").Append(Woods.Lot(in s) >= 0 ? (s.WoodDist < 100f ? "it fells by its homes" : "it fells " + F(s.WoodDist, "0") + " m out") : "no woods left within reach").Append(".\n");
            if (s.StoneCell > 0) sb.Append("Stone: rock known ").Append(F(s.StoneDist, "0")).Append(" m out").Append(Stone.Quarry(w, sid) != 0 ? ", quarried" : "").Append("; ").Append(F(s.Stone, "0")).Append(" loads in store.\n");
            Ore(w, sid, Mining.Copper, sb);
            Ore(w, sid, Mining.Iron, sb);
            if (s.Charcoal > 0 || Charcoal.Site(w, sid) != 0) sb.Append("Charcoal: ").Append(F(s.Charcoal, "0")).Append(" kg in store").Append(Charcoal.Site(w, sid) != 0 ? ", burnt at its hearth by the woods" : "").Append(s.ClampUntil > w.Now ? " (a clamp smouldering)" : "").Append(".\n");
            // (session 27: the page a leaf of the book - no "0 kg of copper, 0 kg of iron")
            double share = Tech.ToolShare(in s);
            if (share < 0.005 && s.Metal < 0.5 && s.Iron < 0.5) sb.Append("Tools: of wood and stone");
            else sb.Append("Tools: metal for ").Append(F(100 * share, "0")).Append(" % of its people (").Append(s.Metal >= 0.5 ? F(s.Metal, "0") + " kg of copper" : "no copper").Append(s.Iron >= 0.5 ? ", " + F(s.Iron, "0") + " kg of iron" : "").Append(")");
            if (s.Arms > 0.5) sb.Append("; arms for ").Append(F(s.Arms, "0")).Append(s.IronArms > 0.5 ? " (" + F(s.IronArms, "0") + " of iron)" : "");
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
                sb.Append("Pots and jars: ").Append(clay ? "clay within reach" : "no clay within reach");
                if (s.Jars >= 0.5 && held < 0.5) sb.Append("; ").Append(F(s.Jars, "0")).Append(" kg of jars in its granaries, empty");   // ("keep 100 % of the 0 days' grain" had stood)
                else if (s.Jars >= 0.5) sb.Append("; ").Append(F(s.Jars, "0")).Append(" kg of jars in its granaries keep ").Append(F(100 * Pottery.Sealed(in s), "0")).Append(" % of the ").Append(F(held, "0")).Append(" days' grain there sealed");
                else sb.Append("; no jars in its granaries");
                if (s.Pots >= 0.5) sb.Append("; ").Append(F(s.Pots, "0")).Append(" kg of ware in store");
                int own = 0, kiln = 0, none = 0; var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    if (Pottery.FiresOwn(w, sid, Pottery.Best(w, hl[i]))) own++;
                    else if ((double)w.Households.Get(hl[i]).Pots > 0.5 * w.Members(hl[i]).Count) kiln++; else none++;
                }
                // (session 27) only the hearths there are: no "0 cook in kiln pots, 0 in none"
                var hearths = new System.Collections.Generic.List<string>();
                if (own > 0) hearths.Add(own + " fire their own pots");
                if (kiln > 0) hearths.Add(kiln + " cook in kiln pots");
                if (none > 0) hearths.Add(none + " cook without fired pots");
                if (hearths.Count > 0) sb.Append("; its hearths: ").Append(string.Join(", ", hearths));
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
            sb.Append(store < 0.5 ? "no ore in store.\n" : F(store, "0") + " kg of ore in store.\n");
        }

        /// <summary>The techniques its people carry, by its best practitioner.</summary>
        public static string Crafts(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            // (session 27: the page a leaf of the book) by its best hand in words, not "(best 0.87)"; (context 4) the crafts gathered by that hand - "make
            // fishing nets, weave on the loom and smelt iron, masters among them at each" - not "(masters among them)" after every one (s27book6)
            var byHand = new List<string>[4];
            for (int t = 0; t < Tech.Count; t++)
            {
                double best = Learning.Best(in s, t);
                if (best < 0.1) continue;
                int hand = (s.TechKnown & (1 << t)) == 0 ? 3 : best >= 0.9 ? 0 : best >= 0.6 ? 1 : 2;
                (byHand[hand] ??= new List<string>()).Add(Tech.Laws[t].Now);
            }
            string[] among = { "masters among them", "skilled hands among them", "fair hands among them", "none of them yet competent" };
            var groups = new List<int>();
            for (int h = 0; h < 4; h++) if (byHand[h] != null) groups.Add(h);
            if (groups.Count == 0) return "No craft beyond the everyday.\n";
            var sb = new StringBuilder("Its people ");
            if (groups.Count == 1)
            {
                var l = byHand[groups[0]];
                sb.Append(And(l)).Append(l.Count == 1 ? " (" + among[groups[0]] + ")" : groups[0] == 3 ? " - none of them yet competent at any" : ", " + among[groups[0]] + " at each");
            }
            else
                for (int g = 0; g < groups.Count; g++) sb.Append(g > 0 ? "; " : "").Append(And(byHand[groups[g]])).Append(" (").Append(among[groups[g]]).Append(')');
            return sb.Append(".\n").ToString();
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
            if (s.TradeIn >= 1f) sb.Append("Traders come ").Append(s.TradeIn < 1.5f ? "once" : s.TradeIn < 2.5f ? "twice" : F(s.TradeIn, "0") + " times").Append(" a year").Append(s.Market != 0 ? " to its market" : "").Append(".\n");   // ("1 times a year" had stood)
            if (s.Lord != 0)
            {
                // why: its raiders' bands (D2-075), the grain of its famines it could not give back (C10.4), or the lord's asking (C10.8)
                int why = s.LordEvent != 0 && w.History.TryGet(s.LordEvent, out var le) && le.Kind == HistoryKind.TributeAgreed ? le.ArgB : 0;
                sb.Append("It has paid tribute to ").Append(Name(w, s.Lord)).Append(" since ").Append(Year(s.LordSince))
                  .Append(why == 1 ? ": its grain kept it through its famines, and it could not give it back.\n"
                        : why == 2 ? ": when it asked a tenth, its people could neither stand against its men nor go elsewhere.\n"
                        : ": it could neither stand against its bands nor go elsewhere.\n");
                // (C9.5a) a way by sea between their landings, and a boat of planks of its own to row it over or of its lord's to come for it
                bool seaWay = !Places.Trade.AcrossOff && !Places.Trade.NoSea && s.LandAt != 0 && w.Settlements.Exists(s.Lord) && w.Settlements.Get(s.Lord).LandAt != 0 && w.SeaRoutes.Get(w, sid, s.Lord) != null;
                double farL = seaWay ? Vec3d.ArcDistance(s.Anchor, w.Settlements.Get(s.Lord).Anchor, w.Planet.Radius) : 0;   // (dugouts across a short water too: C9.5b)
                bool ownBoats = seaWay && w.Conflict.CrossBoats(sid, farL) > 0, lordBoats = seaWay && w.Conflict.CrossBoats(s.Lord, farL) > 0;
                if (!TC2.Core.Society.Conflict.CarryOff && w.Routes.Get(w, sid, s.Lord) == null && !ownBoats && !lordBoats)   // (C10.6: across water and no boat to cross it - nothing is set aside)
                    sb.Append("No way on foot leads from it to ").Append(Name(w, s.Lord)).Append(": its people cannot carry the tenth there, and it stays in its store.\n");
                else if (!TC2.Core.Society.Conflict.CarryOff && w.Routes.Get(w, sid, s.Lord) == null)   // (C9.5a: by boat)
                    sb.Append("No way on foot leads from it to ").Append(Name(w, s.Lord))
                      .Append(ownBoats ? ": its tenth goes over the sea in its own boats" + (lordBoats ? ", or " + Name(w, s.Lord) + "'s come for it" : "") : ": " + Name(w, s.Lord) + "'s boats come over the sea for its tenth")
                      .Append(s.TributeDue >= 1.0 ? " - " + TenthDays(w, in s) + " of it still set aside" : "").Append(".\n");   // (session 32: "FU", the tool's unit, had stood)
                else if (s.TributeDue >= 1.0)   // (C10.6: set aside at its harvest, carried by its people)
                {
                    string td = TenthDays(w, in s);
                    sb.Append(char.ToUpperInvariant(td[0]) + td.Substring(1)).Append(" of its tenth stands set aside for ").Append(Name(w, s.Lord))
                      .Append(", its people carrying it there load by load").Append(s.CarryToday > 0 ? " (" + s.CarryToday + " loads today)" : "").Append(".\n");
                }
                // why it still pays (C10.5): the weighing at its last harvest, as history keeps it
                var evs = w.History.EventsOf(EntityRef.Settlement(sid));
                for (int i = evs == null ? -1 : evs.Count - 1; i >= 0; i--)
                {
                    if (!w.History.TryGet(evs[i], out var pe) || pe.Kind != HistoryKind.TributePaid || pe.Subject.Id != sid) continue;
                    if (pe.Object.Id == s.Lord && pe.ArgA > 0)
                    {
                        if (TC2.Core.Society.Conflict.MightArmsOff)   // (C10.5's heads)
                            sb.Append("At its last harvest ").Append(Name(w, s.Lord)).Append(" could bring ").Append(pe.ArgA).Append(" against it (its own people and those its call brings); it had ")
                              .Append(pe.ArgB).Append(" of fighting age.\n");
                        else   // (C10.7a: by their lasting strength)
                            sb.Append("At its last harvest ").Append(Name(w, s.Lord)).Append(" could bring a strength of ").Append(pe.ArgA)
                              .Append(" against it - its own people and those its call brings, each by their years, arms and fighting skill (an untrained grown person in their prime with no arms counts one); its own came to ")
                              .Append(pe.ArgB).Append(".\n");
                    }
                    break;
                }
            }
            Places.Trade.DaysOfFood(w, in s, out double daily);
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (t.Lord == sid && t.Inhabited)
                {
                    sb.Append(Name(w, o)).Append(" pays it tribute");
                    bool seaWay2 = !Places.Trade.AcrossOff && !Places.Trade.NoSea && t.LandAt != 0 && s.LandAt != 0 && w.SeaRoutes.Get(w, o, sid) != null;
                    double farT = seaWay2 ? Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) : 0;
                    bool itsBoats = seaWay2 && w.Conflict.CrossBoats(o, farT) > 0, ourBoats = seaWay2 && w.Conflict.CrossBoats(sid, farT) > 0;
                    if (!TC2.Core.Society.Conflict.CarryOff && w.Routes.Get(w, o, sid) == null)   // (C10.6; by boat, C9.5a)
                        sb.Append(itsBoats ? " - over the sea, in its boats" : ourBoats ? " - over the sea: boats from here go for its tenth" : " - but no way on foot leads from it here, and its tenth stays there");
                    else if (t.TributeDue >= 1.0) sb.Append(": its people are carrying in this year's tenth, ").Append(F(t.TributeDue / TC2.Core.Society.Conflict.LoadFu, "0")).Append(" loads still to come");   // (C10.6)
                    sb.Append(".\n");
                }
                double owes = w.DeedBook.Owed(sid, o) / daily;   // (the grain given it in its famines, not yet given back: D2-113)
                if (owes >= 1.0) sb.Append("It owes ").Append(Name(w, o)).Append(" ").Append(F(owes, "0")).Append(" days' food for the grain it gave it in its famines.\n");
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
            for (int i = from; i < ids.Count; i++) if (w.History.TryGet(ids[i], out var e)) sb.Append(HistoryText.Dated(w, e)).Append('\n');   // (session 27: as the book dates them)
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
