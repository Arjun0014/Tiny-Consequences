using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Core.History
{
    /// <summary>
    /// The person page (C12.1, D2-099; 07 §9 - non-negotiable): a person shown as a person, never "Villager #183" - who they are, what they
    /// are doing now and why, their family, their work and what they know, what they have done to others and what they can do in a fight, their
    /// home and what it holds, and their life as the ledger keeps it. Every line is read from state or a history record; nothing is invented.
    /// Shared by the HUD's page and the headless `person` command. Reads only.
    /// </summary>
    public static class Biography
    {
        static string F(double v, string fmt = "0") => v.ToString(fmt, CultureInfo.InvariantCulture);
        static string Name(World w, int pid) => w.History.NameOf(EntityRef.Person(pid));
        static string Place(World w, int sid) => sid != 0 ? w.History.NameOf(EntityRef.Settlement(sid)) : "the road";
        static string Year(long t) => "year " + (t / SimTime.Year);

        static string Age(World w, in Person p) => F(p.AgeYears(p.Alive ? w.Now : p.Died), "0");

        /// <summary>"Breista Rindra, a woman of 34" (or "who died at 61").</summary>
        public static string Title(World w, int pid)
        {
            if (!w.People.Exists(pid)) return "";
            ref var p = ref w.People.Get(pid);
            string who = p.IsFemale ? (p.AgeYears(w.Now) < 16 ? "a girl" : "a woman") : (p.AgeYears(w.Now) < 16 ? "a boy" : "a man");
            return Name(w, pid) + ", " + who + (p.Alive ? " of " + Age(w, in p) : " who died at " + Age(w, in p));
        }

        /// <summary>Born when, where and to whom; where they live, in whose home.</summary>
        public static string Identity(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var sb = new StringBuilder();
            int born = BornAt(w, pid);
            sb.Append(p.Born < 0 ? "Born " + (-p.Born / SimTime.Year) + " years before the land's first year" : "Born in " + Year(p.Born)).Append(born != 0 ? " in " + Place(w, born) : "")
              .Append(p.Mother != 0 ? " to " + Name(w, p.Mother) + (p.Father != 0 ? " and " + Name(w, p.Father) : "") : ", among the first people of the land").Append(".\n");
            if (!p.Alive) { sb.Append("Died in ").Append(Year(p.Died)).Append(p.DeathEvent != 0 && w.History.TryGet(p.DeathEvent, out var de) ? ": " + HistoryText.Line(w, de) : ".").Append('\n'); return sb.ToString(); }
            sb.Append("Lives in ").Append(Place(w, p.Home));
            if (w.Households.Exists(p.Household))
            {
                ref var hh = ref w.Households.Get(p.Household);
                sb.Append(", in the ").Append(HomeWord(w, in hh)).Append(" of the ").Append(HistoryText.Plural(w.History.NameOf(EntityRef.Household(p.Household)))).Append(" (")
                  .Append(w.Members(p.Household).Count).Append(" people)");
            }
            sb.Append(".\n");
            if (w.Settlements.Exists(p.Home) && w.Settlements.Get(p.Home).Head == pid && !Society.Leaders.Off)
                sb.Append("First among the people of ").Append(Place(w, p.Home)).Append(" since ").Append(Year(w.Settlements.Get(p.Home).HeadSince)).Append(" (theirs is the household most of the village follows: its kin, those who owe it grain, those it fed or lent to lately).\n");
            return sb.ToString();
        }

        static string HomeWord(World w, in Household hh)
        {
            if (hh.Building == 0 || !w.Buildings.Exists(hh.Building)) return "camp";
            ref var b = ref w.Buildings.Get(hh.Building);
            string house = Architecture.Has(b.Form) ? Architecture.Short(b.Form) : "house";   // (how it was built: D2-114)
            return b.Kind == Structures.House ? (b.Mat >= 1 ? house + " on stone footings" : house) : "hut";
        }

        /// <summary>The village they were born in (their birth's record), or 0.</summary>
        public static int BornAt(World w, int pid)
        {
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs == null) return 0;
            for (int i = 0; i < evs.Count; i++)
                if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.PersonBorn && e.Subject.Id == pid && e.Subject.Kind == EntityKind.Person) return e.Settlement;
            return 0;
        }

        /// <summary>Where they are, what they do and why (the decision's own reason), how they are in body.</summary>
        public static string Now(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            if (!p.Alive) return "";
            var sb = new StringBuilder();
            sb.Append(p.At == 0 ? "On the road" : (p.At == p.Home ? "At home in " + Place(w, p.At) : "Visiting " + Place(w, p.At))).Append(": ")
              .Append(HistoryText.Doing(w, in p)).Append(p.T1 > w.Now ? ", until " + SimTime.FormatShort(p.T1) : "").Append(".\n");
            sb.Append("Health ").Append(F(p.Health, "0.00")).Append(", fed ").Append(F(p.Nutrition, "0.00")).Append(", ").Append(F(p.Reserve, "0")).Append(" days of reserve in the body");
            if (p.Hurt > 0.01f) sb.Append(", wounded (").Append(F(p.Hurt, "0.00")).Append(p.Hurt >= 0.4f ? ", abed" : "").Append(")");
            if (p.Sick != 0) sb.Append(p.SickPhase == Health.Disease.Latent ? ", carrying " : ", sick with ").Append(w.History.NameOf(new EntityRef(EntityKind.Pathogen, p.Sick)));
            if (p.PregDue != 0) sb.Append(", with child (due ").Append(SimTime.FormatShort(p.PregDue)).Append(")");
            sb.Append(".\n");
            if (p.CargoQty > 0f) sb.Append("Carrying ").Append(F(p.CargoQty, "0.0")).Append(p.Cargo == Trade.Metal ? " kg of copper" : p.Cargo == Trade.Iron ? " kg of iron" : p.Cargo == Trade.Grain ? " days' grain" : p.Cargo == Trade.Fibre ? " kg of fibre" : p.Cargo == Trade.Wares ? " wares (a person's cloth and goods each)" : p.Cargo == Trade.Pots ? " kg of pots and jars" : " hides").Append(Trade.KgOf(p.Cargo) * p.CargoQty > Trade.LoadKg + 1e-6 && Carts.Usable(w, p.Household) != Carts.None ? " on the household's " + Carts.NameOf(Carts.Usable(w, p.Household)) : "").Append(".\n");
            return sb.ToString();
        }

        /// <summary>Parents, partner, children (living and dead), brothers and sisters, grandchildren.</summary>
        public static string Family(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var sb = new StringBuilder();
            sb.Append("Mother: ").Append(p.Mother != 0 ? Kin(w, p.Mother) : "unknown (among the first people)").Append('\n');
            sb.Append("Father: ").Append(p.Father != 0 ? Kin(w, p.Father) : "unknown (among the first people)").Append('\n');
            if (p.Partner != 0) sb.Append("Partner: ").Append(Kin(w, p.Partner)).Append('\n');
            var children = new List<int>(); var sibs = new List<int>(); int grand = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                if (id == pid) continue;
                ref var q = ref w.People.Get(id);
                if (q.Mother == pid || q.Father == pid) children.Add(id);
                else if ((p.Mother != 0 && q.Mother == p.Mother) || (p.Father != 0 && q.Father == p.Father)) sibs.Add(id);
            }
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var q = ref w.People.Get(id);
                if ((q.Mother != 0 && children.Contains(q.Mother)) || (q.Father != 0 && children.Contains(q.Father))) grand++;
            }
            if (children.Count > 0)
            {
                sb.Append("Children (").Append(children.Count).Append("): ");
                for (int i = 0; i < children.Count; i++) sb.Append(i > 0 ? "; " : "").Append(Kin(w, children[i]));
                sb.Append('\n');
            }
            else sb.Append("No children.\n");
            if (sibs.Count > 0)
            {
                sb.Append("Brothers and sisters (").Append(sibs.Count).Append("): ");
                for (int i = 0; i < sibs.Count && i < 10; i++) { ref var q = ref w.People.Get(sibs[i]); sb.Append(i > 0 ? "; " : "").Append(Kin(w, sibs[i])).Append(q.Mother == p.Mother && q.Father == p.Father ? "" : " (half)"); }
                if (sibs.Count > 10) sb.Append("; ...");
                sb.Append('\n');
            }
            if (grand > 0) sb.Append("Grandchildren: ").Append(grand).Append('\n');
            return sb.ToString();
        }

        static string Kin(World w, int id)
        {
            if (!w.People.Exists(id)) return Name(w, id) + " (long dead)";
            ref var q = ref w.People.Get(id);
            return Name(w, id) + (q.Alive ? " (" + Age(w, in q) + (q.Home != 0 ? ", " + Place(w, q.Home) : "") + ")" : " (died at " + Age(w, in q) + ")");
        }

        /// <summary>What they are good at (skills grow with practice), the crafts they carry, the works they led, their household's goods.</summary>
        public static string Work(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var sb = new StringBuilder();
            sb.Append("Skills (a knack of their own, grown by practice): farming ").Append(F(p.SkFarm, "0.00")).Append(", woodcraft ").Append(F(p.SkWood, "0.00"))
              .Append(", hunting ").Append(F(p.SkHunt, "0.00")).Append(", fishing ").Append(F(p.SkFish, "0.00")).Append(".\n");
            string roles = Roles(w, in p);
            if (roles.Length > 0) sb.Append("Known for: ").Append(roles).Append(".\n");
            var crafts = new StringBuilder();
            Craft(crafts, "nets", p.KNets); Craft(crafts, "the loom", p.KLoom); Craft(crafts, "querns", p.KQuern); Craft(crafts, "pots", p.KPottery);
            Craft(crafts, "the ard", p.KArd); Craft(crafts, "copper", p.KMetal); Craft(crafts, "the bloomery (iron)", p.KIron); Craft(crafts, "boats of planks", p.KBoat);
            sb.Append(crafts.Length > 0 ? "Crafts they carry: " + crafts + ".\n" : "No craft beyond the everyday.\n");
            // works they were first at (history keeps the leader as Other)
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs != null)
                for (int i = 0; i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.WorkBuilt && e.Other.Id == pid)
                        sb.Append("Led the raising of the ").Append(Structures.Name((byte)e.ArgA)).Append(" of ").Append(Place(w, e.Settlement)).Append(" (").Append(Year(e.Time)).Append(").\n");
            return sb.ToString();
        }

        static void Craft(StringBuilder sb, string what, float k)
        {
            if (k < 0.1f) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(what).Append(' ').Append(F(k, "0.00")).Append(k >= 0.5f ? " (competent)" : "");
        }

        /// <summary>The plank boats a person led the building of (history keeps the leader of a boat's building as its record's Other).</summary>
        public static int PlankBoats(World w, int pid)
        {
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs == null) return 0;
            long me = EntityRef.Person(pid).Packed;
            int n = 0;
            for (int i = 0; i < evs.Count; i++)
                if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.BoatBuilt && e.Other.Packed == me && e.ArgA == Structures.PlankBoat) n++;
            return n;
        }

        /// <summary>Roles read from what they have practised (skills and crafts at their village's workplaces) - never assigned.</summary>
        static string Roles(World w, in Person p)
        {
            var r = new List<string>();
            int home = p.Home;
            // (a smith from their hours at the smithy or the open hearth, a boatwright from the plank boats they led the building of - before, everyone
            // who could work copper or knew the planks' craft: 5,730 "smiths" and "boatwrights" in a year-300 world where 175 worked at a smithy
            // and 10 built a plank boat: C1.4b, D2-125)
            if (p.SkSmith >= 0.3f)
                r.Add((p.KIron >= 0.5f ? "an ironsmith" : "a smith") + (home != 0 && w.Settlements.Exists(home) && Forge.Smithy(w, home) != 0 && Forge.Maker(in w.Settlements.Get(home), in p, w.Now) ? " at the smithy" : ""));
            int planks = PlankBoats(w, p.Id);
            if (planks > 0) r.Add((home != 0 && w.Settlements.Exists(home) && w.Settlements.Get(home).Shed != 0 ? "a boatwright at the boat shed" : "a boatwright") + " (" + planks + (planks == 1 ? " plank boat" : " plank boats") + ")");
            if (p.SkFarm >= 0.6f) r.Add("a seasoned farmer");
            if (p.SkHunt >= 0.5f) r.Add("a hunter");
            if (p.SkFish >= 0.5f) r.Add("a fisher");
            if (p.SkWood >= 0.6f) r.Add("a woodcutter and builder");
            if (p.SkPot >= 0.3f) r.Add(home != 0 && w.Settlements.Exists(home) && Pottery.Kiln(w, home) != 0 && Pottery.Maker(in w.Settlements.Get(home), in p, w.Now) ? "a potter at the kiln" : "a potter");   // (from their hours at a kiln - before, everyone who could fire a pot: C3.2)
            if (p.SkCraft >= 0.3f) r.Add(home != 0 && w.Settlements.Exists(home) && Weaving.Shed(w, home) != 0 && Weaving.Maker(in w.Settlements.Get(home), in p, w.Now) ? "a weaver at the weaver's shed" : "a weaver");   // (from their hours at a shed: C3.1)
            if (p.SkFight >= 0.3f) r.Add(p.Fights > 0 ? "a fighter who has fought" : "a trained fighter");
            return string.Join(", ", r);
        }

        /// <summary>What they have done to others (deeds their villages still weigh), what they can do in a fight, the band they are with.</summary>
        public static string Deeds(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var sb = new StringBuilder();
            sb.Append("Fighting skill ").Append(F(p.SkFight, "0.00")).Append(p.Fights > 0 ? ", " + p.Fights + (p.Fights == 1 ? " fight" : " fights") + " lived through" : ", never in a fight")
              .Append(p.AgeYears(w.Now) < 16 ? "; too young to come out to a fight.\n" : !Conflict.Fit(in p, w.Now) ? "; not fit to come out to a fight now.\n"
                  : "; strikes at " + F(w.Conflict.Strength(in p), "0.00") + " (health, years, wounds, arms, skill).\n");
            if (p.Band != 0 && w.Bands.Exists(p.Band)) { var b = w.Bands.Get(p.Band); sb.Append("With a band of ").Append(b.Size).Append(" from ").Append(Place(w, b.Origin)).Append(" against ").Append(Place(w, b.Target)).Append(".\n"); }
            int n = 0;
            for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id))
            {
                var d = w.Deeds.Get(id);
                if (d.By != pid) continue;
                if (n == 0) sb.Append("Deeds still weighed by the villages:\n");
                if (n++ >= 12) continue;
                sb.Append("  ").Append(Year(d.Time)).Append(": ").Append(DeedWord(d.Kind, d.Good)).Append(' ').Append(Place(w, d.ToS))
                  .Append(d.Worth > 0f ? " (" + F(d.Worth, "0") + " days' food in weight)" : "").Append(d.Answer != 0 ? (DeedKind.Harm(d.Kind) ? ", answered" : "") : "").Append('\n');
            }
            if (n == 0) sb.Append("No deed of theirs is still weighed by any village (their life below keeps what they did).\n");
            int hsid = p.Home != 0 ? p.Home : p.At;
            if (hsid == 0 || !w.Settlements.Exists(hsid)) return sb.ToString();
            ref var hs = ref w.Settlements.Get(hsid);
            if (hs.Lord != 0) sb.Append(Place(w, p.Home)).Append(" pays tribute to ").Append(Place(w, hs.Lord)).Append(".\n");
            if (hs.Foe != 0) sb.Append(Place(w, p.Home)).Append(" holds a wrong against ").Append(Place(w, hs.Foe)).Append(" (").Append(F(hs.FoeWorth, "0")).Append(").\n");
            return sb.ToString();
        }

        static string DeedWord(byte k, byte good) => k == DeedKind.Take ? (good == Trade.Metal || good == Trade.Iron ? "dug ore at the works of" : "took grain from the store of") : k == DeedKind.TriedToTake ? "was seen taking at the store of" :
            k == DeedKind.Wound ? "wounded someone of" : k == DeedKind.Kill ? "killed someone of" : k == DeedKind.Raid ? "led a band on" :
            k == DeedKind.Relief ? "gave grain to" : k == DeedKind.Amends ? "made amends to" : "refused amends to";

        /// <summary>The household's home, store, animals and goods.</summary>
        public static string Home(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            if (!w.Households.Exists(p.Household)) return "No household.\n";
            ref var hh = ref w.Households.Get(p.Household);
            var sb = new StringBuilder();
            sb.Append("The ").Append(HistoryText.Plural(w.History.NameOf(EntityRef.Household(p.Household)))).Append(" live in a ").Append(HomeWord(w, in hh));
            if (hh.Building != 0 && w.Buildings.Exists(hh.Building)) sb.Append(" raised in ").Append(Year(w.Buildings.Get(hh.Building).Built));
            sb.Append(". Larder ").Append(F(hh.Larder, "0")).Append(" days' food, ").Append(F(hh.Ready, "0.0")).Append(" made ready; water ").Append(F(hh.Water, "0")).Append(" l; ")
              .Append(hh.Indoor < Hearth.Comfort - 0.5 ? F(hh.Indoor, "0") + " C indoors" + (hh.Indoor < Hearth.ChillLine ? ", cold" : "") : "warm enough indoors").Append("; clothes and tools ").Append(F((double)hh.Gear / System.Math.Max(1, w.Members(p.Household).Count), "0.00")).Append(" a head").Append(hh.Wares > 0.05f ? " (" + F((double)hh.Wares / System.Math.Max(1, w.Members(p.Household).Count), "0.00") + " of it wares from a weaver's shed)" : "").Append(".\n");
            // its grain in the village's granary and what it owes or is owed (C3.3)
            if (!Holdings.Off) sb.Append(Holdings.Line(w, p.Household));
            // its pots (C3.2): fired by its own hands where it has clay, from a kiln, or none - and the wood its cooking burns for it
            {
                bool own = Pottery.FiresOwn(w, hh.Settlement, Pottery.Best(w, p.Household));
                sb.Append("It cooks ").Append(own ? "in pots it fires itself" : hh.Pots > 0.05f ? "in " + F(hh.Pots, "0.0") + " kg of kiln pots" : "without fired pots - " + (w.Settlements.Exists(hh.Settlement) && w.Learning.Clay(w.Settlements.Get(hh.Settlement).Cell) ? "nobody in it can fire them" : "no clay within reach and none from a kiln"))
                  .Append(" (its cooking burns ").Append(F(100 * Pottery.CookFactor(w, p.Household), "0")).Append(" % of the wood it would without them).\n");
            }
            // its cart (C4.2): kept, being made, or wanted
            if (hh.CartState == Carts.Made)
                sb.Append(Carts.WornOut(in hh, w.Now) ? "Its " + Carts.NameOf(hh.CartKind) + " is worn past mending.\n"
                    : "It keeps " + (hh.CartKind == Carts.Ox ? "an ox cart" : "a handcart") + ", made in " + Year(hh.CartAt) + (hh.CartKind == Carts.Ox && !w.Stock.HasOx(p.Household) ? " - no grown ox to draw it now" : "") + ".\n");
            else if (hh.CartState == Carts.Making) sb.Append("It is making " + (hh.CartKind == Carts.Ox ? "an ox cart" : "a handcart") + " (" + F(hh.CartHours, "0") + " of " + F(Carts.HoursOf(hh.CartKind), "0") + " hours).\n");
            else if (hh.CartState == Carts.Wanted) sb.Append("It wants a cart: what it carries is more than its backs can take.\n");
            int cattle = 0, goats = 0, pigs = 0; var beasts = w.BeastsOf(p.Household);
            for (int i = 0; i < beasts.Count; i++) { byte k = w.Beasts.Get(beasts[i]).Kind; if (k == Livestock.Cattle) cattle++; else if (k == Livestock.Goat) goats++; else pigs++; }
            var an = new List<string>();
            if (cattle > 0) an.Add(cattle + " cattle");
            if (goats > 0) an.Add(goats + (goats == 1 ? " goat" : " goats"));
            if (pigs > 0) an.Add(pigs + (pigs == 1 ? " pig" : " pigs"));
            sb.Append(an.Count > 0 ? "Animals: " + string.Join(", ", an) + ".\n" : "No animals.\n");
            // the household's boat (C9.1)
            if (hh.Boat != 0 && w.Buildings.Exists(hh.Boat))
            {
                ref var bt = ref w.Buildings.Get(hh.Boat);
                bool plank = Boats.IsPlank(bt.Kind);
                if (bt.Done == 0) sb.Append(plank ? "They are building a boat of planks in the boat shed (" : "They are hollowing a dugout at the landing (").Append(F(100.0 * bt.Hours / Boats.HoursOf(bt.Kind), "0")).Append(" % done).\n");
                else sb.Append(plank ? "A plank boat of theirs lies at the landing, built in " : "A dugout of theirs lies at the landing, made in ").Append(Year(bt.Built)).Append(Boats.Sound(in bt, w.Now) ? "" : ", split with age").Append(".\n");
            }
            return sb.ToString();
        }

        /// <summary>Their life as the ledger keeps it: every record they are part of, oldest first (the last `max`).</summary>
        public static string Life(World w, int pid, int max = 40)
        {
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            var sb = new StringBuilder();
            if (evs == null || evs.Count == 0) return "Nothing of their life is kept in the history yet.\n";
            int from = System.Math.Max(0, evs.Count - max);
            if (from > 0) sb.Append("(... ").Append(from).Append(" earlier records)\n");
            for (int i = from; i < evs.Count; i++) if (w.History.TryGet(evs[i], out var e)) sb.Append(HistoryText.Line(w, e)).Append('\n');
            return sb.ToString();
        }

        /// <summary>The whole page as text (headless `person`, tests).</summary>
        public static string All(World w, int pid)
        {
            if (!w.People.Exists(pid)) return "no such person\n";
            var sb = new StringBuilder();
            sb.Append("== ").Append(Title(w, pid)).Append('\n').Append(Identity(w, pid));
            ref var p = ref w.People.Get(pid);
            if (p.Alive) sb.Append("-- now\n").Append(Now(w, pid));
            sb.Append("-- family\n").Append(Family(w, pid));
            sb.Append("-- work and knowledge\n").Append(Work(w, pid));
            sb.Append("-- deeds and force\n").Append(Deeds(w, pid));
            sb.Append("-- home\n").Append(Home(w, pid));
            sb.Append("-- life\n").Append(Life(w, pid));
            return sb.ToString();
        }
    }
}
