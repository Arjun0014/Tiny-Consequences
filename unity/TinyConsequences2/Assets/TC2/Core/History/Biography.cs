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

        /// <summary>
        /// (session 27: people recognisable) A person's standing in a few words, for the pointer's tag and the page: the first among a village's people
        /// (a lord when other villages pay it, with how many; whom it pays), a man sworn to a first, one of a first's household; "" for everyone else.
        /// Reads only.
        /// </summary>
        public static string Standing(World w, int pid)
        {
            if (Leaders.Off || !w.People.Exists(pid)) return "";
            ref var p = ref w.People.Get(pid);
            if (!p.Alive || !w.Settlements.Exists(p.Home)) return "";
            ref var s = ref w.Settlements.Get(p.Home);
            string place = Place(w, p.Home);
            if (s.Head == pid)
            {
                int held = 0;
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == p.Home && os.Inhabited) held++; }
                return "first of " + place + (held > 0 ? ", lord of " + held + (held == 1 ? " village" : " villages") : "") +
                       (s.Lord != 0 && w.Settlements.Exists(s.Lord) ? ", paying " + Place(w, s.Lord) : "");
            }
            if (p.Sworn != 0 && p.Sworn == s.Head && w.People.Exists(p.Sworn)) return "sworn to " + Name(w, p.Sworn) + ", first of " + place;
            if (p.Household != 0 && p.Household == s.HeadHh && s.Head != 0 && w.People.Exists(s.Head)) return "of the household of " + Name(w, s.Head) + ", first of " + place;
            return "";
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

        static readonly string[] AtHomeVerbs = { "resting", "asleep", "eating", "abed" };

        /// <summary>Where they are, what they do and why (the decision's own reason), how they are in body.</summary>
        public static string Now(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            if (!p.Alive) return "";
            var sb = new StringBuilder();
            // (session 27, context 4) at home the doing says so once: "At home in Kriford, resting" - not "At home in Kriford, resting at home" (s27book6)
            string doing = HistoryText.Doing(w, in p);
            bool home = p.At != 0 && p.At == p.Home;
            if (home) foreach (string verb in AtHomeVerbs) if (doing.StartsWith(verb + " at home", System.StringComparison.Ordinal)) { doing = verb + doing.Substring(verb.Length + 8); break; }
            sb.Append(p.At == 0 ? "On the road" : (home ? "At home in " + Place(w, p.At) : "Visiting " + Place(w, p.At))).Append(", ")
              .Append(doing).Append(p.T1 > w.Now ? ", until " + Until(w.Now, p.T1) : "").Append(".\n");
            // (session 27: the page a leaf of the book) how they fare in words, as the inspector's card says it - "Health 1.00, fed 1.00, 42 days of reserve"
            // had read as a tool's output
            bool ill = p.Sick != 0 && p.SickPhase != Health.Disease.Latent;
            string sickness = p.Sick != 0 ? w.History.NameOf(new EntityRef(EntityKind.Pathogen, p.Sick)) : "";
            string fares = ill ? "sick with " + sickness : p.Health >= 0.9f ? "in good health" : p.Health >= 0.6f ? "unwell" : p.Health >= 0.3f ? "ailing" : "near death";
            sb.Append(char.ToUpperInvariant(fares[0])).Append(fares.Substring(1)).Append(" and ").Append(p.Nutrition >= 0.8f ? "well fed" : p.Nutrition >= 0.5f ? "hungry" : "starving")
              .Append(", with ").Append(p.Reserve >= 30f ? "flesh to spare for a month or more" : p.Reserve >= 14f ? "some weeks' flesh to spare" : p.Reserve >= 5f ? "a few days' flesh to spare" : "nothing to spare in the body");
            if (p.Hurt > 0.01f) sb.Append(p.Hurt >= 0.4f ? ", badly wounded and abed" : p.Hurt >= 0.15f ? ", wounded" : ", lightly hurt");
            if (p.Sick != 0 && !ill) sb.Append(", carrying ").Append(sickness).Append(" unknowing");
            if (p.PregDue != 0) sb.Append(", with child (due ").Append(p.PregDue > w.Now ? Until(w.Now, p.PregDue) : "now").Append(")");
            sb.Append(".\n");
            if (p.CargoQty > 0f) sb.Append("Carrying ").Append(F(p.CargoQty, "0.0")).Append(p.Cargo == Trade.Metal ? " kg of copper" : p.Cargo == Trade.Iron ? " kg of iron" : p.Cargo == Trade.Grain ? " days' grain" : p.Cargo == Trade.Fibre ? " kg of fibre" : p.Cargo == Trade.Wares ? " wares (a person's cloth and goods each)" : p.Cargo == Trade.Pots ? " kg of pots and jars" : " hides").Append(Trade.KgOf(p.Cargo) * p.CargoQty > Trade.LoadKg + 1e-6 && Carts.Usable(w, p.Household) != Carts.None ? " on the household's " + Carts.NameOf(Carts.Usable(w, p.Household)) : "").Append(".\n");
            return sb.ToString();
        }

        /// <summary>(session 27) When something ends, as people say it: "08:34" the same day, "tomorrow at 06:00", "in 3 days" (not "Y0 d1 08:34").</summary>
        static string Until(long now, long t)
        {
            long dNow = SimTime.FloorDiv(now, SimTime.Day), dT = SimTime.FloorDiv(t, SimTime.Day);
            int h = SimTime.HourOfDay(t), m = SimTime.MinuteOfHour(t);
            string clock = (h < 10 ? "0" : "") + h.ToString(CultureInfo.InvariantCulture) + ":" + (m < 10 ? "0" : "") + m.ToString(CultureInfo.InvariantCulture);
            return dT == dNow ? clock : dT == dNow + 1 ? "tomorrow at " + clock : "in " + (dT - dNow).ToString(CultureInfo.InvariantCulture) + " days";
        }

        /// <summary>Parents, partner, children (living and dead), brothers and sisters, grandchildren.</summary>
        public static string Family(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var sb = new StringBuilder();
            sb.Append("Mother: ").Append(p.Mother != 0 ? Kin(w, p.Mother) : "unknown (among the first people)").Append('\n');
            sb.Append("Father: ").Append(p.Father != 0 ? Kin(w, p.Father) : "unknown (among the first people)").Append('\n');
            if (p.Partner != 0) sb.Append("Partner: ").Append(Kin(w, p.Partner)).Append('\n');
            else if (p.Alive && Places.SettlementSystem.MeansCount && p.AgeYears(w.Now) >= 18 && p.AgeYears(w.Now) <= (p.IsFemale ? 36 : 48) && w.Households.Exists(p.Household))
            {
                // unmarried, of an age to marry: whether a household of their own could be kept now (C3.5)
                int at = w.Households.Get(p.Household).Settlement, toHarvest = w.SettlementSys.ToHarvest(at);
                double own = w.SettlementSys.OwnDays(pid);
                sb.Append("Unmarried. ").Append(p.IsFemale ? "Her" : "His").Append(" share of the household's grain would keep ").Append(p.IsFemale ? "her " : "him ").Append(HistoryText.Span(own))
                  .Append("; the harvest is ").Append(toHarvest).Append(toHarvest == 1 ? " day" : " days").Append(" off")
                  .Append(own < toHarvest + Places.Holdings.Reserve ? " - a match waits for the means of a household (a couple marries when what they bring keeps them to the harvest).\n" : ".\n");
            }
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
            // (session 27) skills and crafts in words, by how far practice has taken them ("farming 1.00, woodcraft 0.32" had read as a tool's output)
            var levels = new List<string>[4];
            for (int k = 0; k < 4; k++) levels[k] = new List<string>();
            void Skill(string what, float s) { int k = Level(s); if (k >= 0) levels[k].Add(what); }
            Skill("farming", p.SkFarm); Skill("woodwork", p.SkWood); Skill("hunting", p.SkHunt); Skill("fishing", p.SkFish);
            var said = new List<string>();
            for (int k = 0; k < 4; k++) if (levels[k].Count > 0) said.Add((k == 0 ? "a master of " : k == 1 ? "skilled at " : k == 2 ? "a fair hand at " : "a beginner at ") + And(levels[k]));
            sb.Append(said.Count > 0 ? "Skills: " + string.Join("; ", said) + " - each grown by practice.\n" : "No skill yet beyond the everyday.\n");
            string roles = Roles(w, in p);
            if (roles.Length > 0) sb.Append("Known for: ").Append(roles).Append(".\n");
            var knows = new List<string>(); var learning = new List<string>();
            void Craft(string what, float k) { if (k >= 0.5f) knows.Add(what); else if (k >= 0.1f) learning.Add(what); }
            Craft("nets", p.KNets); Craft("the loom", p.KLoom); Craft("querns", p.KQuern); Craft("pots", p.KPottery);
            Craft("the ard", p.KArd); Craft("copper", p.KMetal); Craft("the bloomery (iron)", p.KIron); Craft("boats of planks", p.KBoat);
            if (knows.Count + learning.Count == 0) sb.Append("No craft beyond the everyday.\n");
            else sb.Append("Crafts they carry: ").Append(knows.Count > 0 ? And(knows) : "none yet").Append(learning.Count > 0 ? "; learning " + And(learning) : "").Append(".\n");
            // works they were first at (history keeps the leader as Other)
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            if (evs != null)
                for (int i = 0; i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.WorkBuilt && e.Other.Id == pid)
                        sb.Append("Led the raising of the ").Append(Structures.Name((byte)e.ArgA)).Append(" of ").Append(Place(w, e.Settlement)).Append(" (").Append(Year(e.Time)).Append(").\n");
            return sb.ToString();
        }

        /// <summary>How far practice has taken a skill (0-1): 0 a master, 1 skilled, 2 a fair hand, 3 a beginner, -1 not yet.</summary>
        static int Level(float s) => s >= 0.9f ? 0 : s >= 0.6f ? 1 : s >= 0.3f ? 2 : s >= 0.1f ? 3 : -1;

        /// <summary>"a", "a and b", "a, b and c".</summary>
        static string And(List<string> l) => l.Count == 1 ? l[0] : string.Join(", ", l.GetRange(0, l.Count - 1)) + " and " + l[l.Count - 1];

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
            if (Society.Retinue.SwornHere(w, in p)) r.Add("sworn to " + w.History.NameOf(EntityRef.Person(p.Sworn)) + ", first among the people of " + Place(w, p.Home) + ", whose household feeds him");   // (C10.7b)
            if (p.SkFight >= 0.3f) r.Add(p.Fights > 0 ? "a fighter who has fought" : "a trained fighter");
            return string.Join(", ", r);
        }

        /// <summary>What they have done to others (deeds their villages still weigh), what they can do in a fight, the band they are with.</summary>
        public static string Deeds(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            var sb = new StringBuilder();
            // (session 27) in words: how practised in a fight, how hard they would strike now (their health, years, wounds, their village's arms, their practice -
            // 1 a grown body in health, unarmed and untrained)
            int fk = Level(p.SkFight);
            sb.Append("In a fight: ").Append(fk == 0 ? "a master" : fk == 1 ? "skilled" : fk == 2 ? "a fair hand" : fk == 3 ? "a beginner" : "untrained")
              .Append(p.Fights > 0 ? ", " + p.Fights + (p.Fights == 1 ? " fight" : " fights") + " lived through" : ", never yet fought");
            if (p.AgeYears(w.Now) < 16) sb.Append("; too young to come out to a fight.\n");
            else if (!Conflict.Fit(in p, w.Now)) sb.Append("; not fit to come out to a fight now.\n");
            else
            {
                double st = w.Conflict.Strength(in p);
                sb.Append(st >= 1.8 ? "; formidable now - armed and practised" : st >= 1.3 ? "; strikes harder than most" : st >= 0.95 ? "; strikes as hard as most" : st >= 0.6 ? "; no great strength" : "; weak").Append(".\n");
            }
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
            if (hs.Foe != 0) sb.Append(Place(w, p.Home)).Append(" holds a wrong against ").Append(Place(w, hs.Foe)).Append(", weighed at ").Append(F(hs.FoeWorth, "0")).Append(" days' food.\n");
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
            // (session 27) in words ("Larder 1 days' food, 0.0 made ready; water 24 l; clothes and tools 1.25 a head" had read as a tool's output)
            int heads = System.Math.Max(1, w.Members(p.Household).Count);
            double gear = (double)hh.Gear / heads, wares = hh.Gear > 0.05f ? (double)hh.Wares / (double)hh.Gear : 0.0;
            sb.Append(". ").Append(hh.Larder < 1.0 ? "Little food at home" : hh.Larder < 2.0 ? "Food at home for a day" : "Food at home for " + F(hh.Larder, "0") + " days")
              .Append(hh.Ready > 0.05f ? ", some of it made ready" : "").Append("; ").Append(F(hh.Water, "0")).Append(" litres of water; ")
              .Append(hh.Indoor < Hearth.Comfort - 0.5 ? (hh.Indoor < Hearth.ChillLine ? "cold indoors (" : "cool indoors (") + F(hh.Indoor, "0") + " C)" : "warm enough indoors")
              .Append("; ").Append(gear >= 1.0 ? "clothes and tools enough for all" : gear >= 0.6 ? "clothes and tools worn thin" : "short of clothes and tools")
              .Append(wares >= 0.5 ? ", most of them wares from a weaver's shed" : wares > 0.05 ? ", some of them wares from a weaver's shed" : "").Append(".\n");
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
            // (session 27) each record as the book dates it ("Year 12, late summer: ..."), not the feed's "Y12 d44 18:59 [Personal/People]"
            for (int i = from; i < evs.Count; i++) if (w.History.TryGet(evs[i], out var e)) sb.Append(HistoryText.Dated(w, e)).Append('\n');
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
