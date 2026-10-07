using System.Globalization;
using System.Text;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.History
{
    /// <summary>
    /// Readable lines for recorded events (an observer: reads the ledger and registry, writes nothing).
    /// Used by the catch-up feed, debug dumps and, later, the History Book's prose layer.
    /// </summary>
    public static class HistoryText
    {
        static string F(double v, string fmt = "0.#") => v.ToString(fmt, CultureInfo.InvariantCulture);

        public static string Describe(World w, in HistoryEvent e)
        {
            var H = w.History;
            string subj = H.NameOf(e.Subject), obj = H.NameOf(e.Object), other = H.NameOf(e.Other);
            string place = e.Settlement != 0 ? H.NameOf(EntityRef.Settlement(e.Settlement)) : (e.Cell >= 0 ? w.PlaceName(e.Cell) : "the world");
            switch (e.Kind)
            {
                case HistoryKind.WorldCreated: return "The world took shape.";
                case HistoryKind.PeopleArrived: return "People came to the place that would be " + subj + ".";
                case HistoryKind.SettlementFounded: return subj + " was founded.";
                case HistoryKind.SettlementAbandoned: return subj + " was abandoned.";
                case HistoryKind.PersonBorn: return subj + " was born to " + obj + (e.Other.IsNone ? "" : " and " + other) + " in " + place + ".";
                case HistoryKind.PersonDied:
                    {
                        string how = e.ArgA == 2 ? " of hunger" : (e.ArgA == 3 ? " in childbirth" : (e.ArgA == 4 ? " in the eruption" : (e.ArgA == 5 ? " when the earthquake brought the house down" : (e.ArgA == 6 ? " in the great wave" : (e.ArgA == 7 ? " in the storm" : (e.ArgA == 8 ? " when the storm drove the sea ashore" : (e.ArgA == 9 ? " of the cold" : (e.ArgA == 10 ? " of " + Sickness(w, e.Cause1) : (e.ArgA == 11 ? " of the wounds of a fight in the night" : "")))))))));
                        return subj + " died" + how + " in " + place + ", aged " + F(e.Magnitude, "0") + ".";
                    }
                case HistoryKind.Marriage: return subj + " and " + obj + " married in " + place + ".";
                case HistoryKind.HouseholdFormed: return "The household of the " + subj + "s was formed in " + place + ".";
                case HistoryKind.LightningStrike: return (e.RootKind == RootKind.You ? "Your lightning" : "Lightning") + " struck near " + place + ".";
                case HistoryKind.FireStarted: return "A fire started near " + place + ".";
                case HistoryKind.Wildfire: return "A wildfire spread near " + place + " (" + F(e.Magnitude, "0") + " cells burnt so far).";
                case HistoryKind.FireOut: return "The fire near " + place + " burnt out after " + e.ArgA + " hours (" + F(e.Magnitude, "0") + " cells).";
                case HistoryKind.FieldsBurned: return "The fields of " + subj + " burned.";
                case HistoryKind.JourneyInterrupted:
                    {
                        string what = e.ArgA == JourneyOutcome.TurnedBack ? "turned back" : e.ArgA == JourneyOutcome.Waited ? "stopped on the road to wait" :
                                      e.ArgA == JourneyOutcome.Resumed ? "went on" : e.ArgA == JourneyOutcome.Continued ? "kept walking" : "gave up";
                        return subj + " saw the fire and " + what + (e.Object.IsNone ? "" : " (on the way to " + obj + ")") + ".";
                    }
                case HistoryKind.CoastChanged:
                    {
                        int emerged = e.ArgB / 1000, villages = e.ArgB % 1000;
                        string v = villages > 0 ? " The water reached " + villages + " village cell" + (villages > 1 ? "s" : "") + "." : "";
                        return e.Magnitude > 0
                            ? "The sea rose " + F(e.Magnitude, "0.0") + " m: " + e.ArgA + " cells of shore were drowned." + v
                            : "The sea fell " + F(-e.Magnitude, "0.0") + " m: " + emerged + " cells of new shore emerged.";
                    }
                case HistoryKind.Eruption:
                    return subj + " erupted near " + place + " (VEI " + F(e.Magnitude, "0") + ")" + (e.Magnitude >= 6 ? ": the sky darkened and the summers turned cold." : (e.Magnitude >= 4 ? ": fire ran down its flanks and ash fell far around." : "."));
                case HistoryKind.VentBorn: return "A volcano, " + subj + ", " + (e.ArgB == 1 ? "broke out over the deep fire" : "opened") + " near " + place + ".";
                case HistoryKind.IslandRose: return "A new island rose from the sea: " + subj + ", near " + place + ".";
                case HistoryKind.VentExtinct: return subj + ", near " + place + ", has fallen silent (" + e.ArgB + " eruptions).";
                case HistoryKind.Speciation:
                    return "The " + obj + " of the land near " + place + ", long cut off from the rest of their kind, have become a kind of their own: the " + subj + ".";
                case HistoryKind.Extinction: return "The last of the " + subj + " are gone: the kind has died out.";
                case HistoryKind.Storm:
                    return (e.ArgA == 0 ? "A great storm gathered over the sea" : "A winter storm grew") + " near " + place + " (winds " + F(e.Magnitude, "0") + " m/s).";
                case HistoryKind.StormLandfall: return "The storm came ashore near " + place + " (winds " + F(e.Magnitude, "0") + " m/s).";
                case HistoryKind.CropsFlattened: return "The storm flattened the crops of " + subj + " (" + F(e.Magnitude * 100.0, "0") + "% lost).";
                case HistoryKind.StormSurge: return "The storm drove the sea " + F(e.Magnitude, "0") + " m over the coast near " + place + ", flooding " + e.ArgA + " stretches of shore.";
                case HistoryKind.Quake:
                    return (e.Magnitude >= 7.5 ? "A great earthquake" : "An earthquake") + " (M " + F(e.Magnitude, "0.0") + ") shook the land near " + place + ".";
                case HistoryKind.Landslides: return "Landslides scarred " + e.ArgA + (e.ArgA == 1 ? " slope" : " slopes") + " near " + place + ".";
                case HistoryKind.StoresSpoiled: return "The earthquake spilled and spoiled " + F(e.Magnitude * 100.0, "0") + "% of the stores of " + subj + ".";
                case HistoryKind.Tsunami: return "A great wave (" + F(e.Magnitude, "0") + " m) came over the coast near " + place + ", flooding " + e.ArgA + " stretches of shore.";
                case HistoryKind.FieldsFlooded: return "The sea came over the fields of " + subj + " (" + F(e.Magnitude * 100.0, "0") + "% of the crop lost).";
                case HistoryKind.AshFall: return "Ash fell on the fields of " + subj + " (" + F(e.Magnitude * 100.0, "0") + "% of the crop lost).";
                case HistoryKind.IceAge: return "An age of ice began: the summers grew too cool to melt the snow, and ice spread over the land.";
                case HistoryKind.IceAgeEnded: return "The age of ice ended: warmer summers are melting the ice sheets back.";
                case HistoryKind.Harvest: return subj + " brought in its harvest (" + F(e.Magnitude, "0") + " food).";
                case HistoryKind.HarvestFailed: return "The harvest failed at " + subj + " (" + F(e.Magnitude * 100.0, "0") + "% short).";
                case HistoryKind.Hunger: return "Hunger came to " + subj + ": the granary is empty.";
                case HistoryKind.Famine: return "Famine in " + subj + ": people are dying of hunger.";
                case HistoryKind.Rationing: return subj + " began to ration its grain (" + F(e.Magnitude * 100.0, "0") + "% portions) to last until the harvest.";
                case HistoryKind.WildFood: return "The people of " + subj + " turned to the woods and the shore for food.";
                case HistoryKind.HomeBuilt: return "The " + subj + "s raised " + (e.ArgA >= 2 ? "a house" : "a hut") + " in " + place + ".";
                case HistoryKind.HomesDrowned:
                    return "The sea took " + (e.ArgA == 1 ? "a home" : e.ArgA + " homes") + " of " + place + ": their people made new homes on higher ground.";
                case HistoryKind.HomeTaken:
                    return "The " + subj + "s made their home in " + (e.Object.IsNone ? (e.ArgA >= 2 ? "an empty house" : "an empty hut") : "the " + (e.ArgA >= 2 ? "house" : "hut") + " the " + obj + "s had left") + " in " + place + ".";
                case HistoryKind.WorkBuilt:
                    if (e.ArgA == TC2.Core.Places.Structures.Well) return "The people of " + subj + " dug a well" + (e.Other.IsNone ? "." : ", " + other + " the first at the digging.");
                    if (e.ArgA == TC2.Core.Places.Structures.Granary && e.ArgB > 1) return "The people of " + subj + " built another granary (" + e.ArgB + " now)" + (e.Other.IsNone ? "." : ", " + other + " the first at the work.");
                    return "The people of " + subj + " built a " + TC2.Core.Places.Structures.Name((byte)e.ArgA) + (e.Other.IsNone ? "." : ", " + other + " the first at the work.");
                case HistoryKind.ArmsMade:
                    return subj + " began to forge arms at the smithy of " + obj + (e.ArgA == 1 ? " from the ore they smelt" : ", reforging its tools") + ": " + obj + " fears its neighbours.";
                case HistoryKind.StoneFound:
                    return (e.Other.IsNone ? "The people of " + subj : other + " of " + subj) + " saw rock fit for building " + F(e.Magnitude / 1000.0, "0.0") + " km from " + subj + ".";
                case HistoryKind.WoodsReceded:
                    return e.ArgA >= 4 ? "The people of " + subj + " have cut the woods within reach: they have no more wood to fetch."
                                       : "The woods near " + subj + " are cut back: its people now fetch their wood from " + F(e.Magnitude / 1000.0, "0.0") + " km away.";
                case HistoryKind.WorkBegun: return "The people of " + subj + " began " + WorkWhy((byte)e.ArgA, e.ArgB) + ".";
                case HistoryKind.PartySetOut: return e.ArgA + " households of " + subj + " (" + e.ArgB + " people) set out to make a new home.";
                case HistoryKind.Scouted: return "Someone of " + subj + " saw good land near " + place + ".";
                case HistoryKind.HouseholdMoved: return "The " + subj + "s left " + place + " for " + obj + ".";
                case HistoryKind.HerdHuntedOut: return "The " + obj + " herd near " + place + " was hunted out (" + F(e.Magnitude, "0") + " at its most).";
                case HistoryKind.ReliefSent: return "In the famine " + subj + " sent grain to " + obj + " (" + F(e.Magnitude, "0") + " days' food for one).";
                case HistoryKind.Taken: return subj + " of " + other + " took " + F(e.Magnitude, "0") + " days' grain from the store of " + obj + " in the night.";
                case HistoryKind.TakingSeen: return subj + " of " + other + " was seen at the store of " + obj + " in the night and ran, leaving the grain.";
                case HistoryKind.Struck:
                    return e.ArgA == 1 ? obj + " was caught at the store of " + place + " in the night and " + (e.Magnitude >= 0.6f ? "badly " : "") + "beaten by " + subj + "."
                                       : subj + ", caught at the store of " + place + " in the night, " + (e.Magnitude >= 0.6f ? "struck down " : "struck ") + obj + ".";
                case HistoryKind.WatchSet: return "The people of " + subj + " began to keep a watch at their store at night: grain has been taken from it.";
                case HistoryKind.AmendsMade:
                    return "The people of " + subj + " made amends to " + obj + " for " + (e.ArgA == 5 ? "a death" : "a wounding") + " (" + F(e.Magnitude, "0") + " days' grain)" +
                           (e.Other.IsNone ? "" : ", asked by " + other) + ".";
                case HistoryKind.ClaimRefused:
                    return "The people of " + subj + (e.ArgA == 1 ? " said they had no part in " : " would make no amends to " + obj + " for ") + (e.ArgB == 5 ? "a death" : "a wounding") +
                           (e.ArgA == 1 ? " that " + obj + " laid at their door" : "") + (e.Other.IsNone ? "" : " when " + other + " came to ask") + ".";
                case HistoryKind.BandRaised:
                    return subj + " raised a band of " + e.ArgA + " in " + place + " to fall on " + obj + (e.ArgB == 1 ? ": they hold a heavy wrong against it." : ": they are starving and its store is full.");
                case HistoryKind.Raid:
                    {
                        int outcome = e.ArgB / 100, blows = e.ArgB % 100;
                        string band = "A band of " + e.ArgA + " from " + subj + (e.Other.IsNone ? "" : ", led by " + other + ",") + " fell on the store of " + obj + " in the night";
                        if (outcome == 0) return band + " and carried off " + F(e.Magnitude, "0") + " days' grain unseen.";
                        if (outcome == 1) return band + ", fought off its people (" + blows + (blows == 1 ? " blow" : " blows") + ") and carried off " + F(e.Magnitude, "0") + " days' grain.";
                        return band + " but was driven off (" + blows + (blows == 1 ? " blow" : " blows") + ").";
                    }
                case HistoryKind.WallPlanned: return "The people of " + subj + " began a palisade round their homes: other villages' people have cost them " + F(e.Magnitude, "0") + " days' food a year.";
                case HistoryKind.WallBuilt: return "The people of " + subj + " finished their palisade (" + e.ArgA + " m round): " + subj + " is a walled place.";
                case HistoryKind.WallFallen: return "The palisade of " + subj + " has rotted and fallen.";
                case HistoryKind.TributeAgreed:
                    return "The people of " + subj + " bowed to " + obj + " and agreed to pay it a tenth of every harvest: they could neither stand against its bands nor go elsewhere" +
                           (e.ArgA > 1 ? " (" + obj + " now has " + e.ArgA + " villages paying)." : ".");
                case HistoryKind.TributePaid: return subj + " paid " + obj + " its tribute (" + F(e.Magnitude, "0") + " days' grain).";
                case HistoryKind.TributeEnded: return e.ArgA == 1 ? subj + " pays tribute no more: " + obj + " is gone." : subj + " would pay " + obj + " tribute no more: " + obj + " is no longer the stronger.";
                case HistoryKind.PathogenAppeared: return "A new sickness, " + subj + ", appeared in " + place + (e.ArgA == 1 ? ", caught from its animals" : "") + ".";
                case HistoryKind.EpidemicArrived:
                    return e.ArgA == 1 ? Cap(obj) + " broke out in " + place + (e.Other.IsNone ? "" : ", " + other + " the first to fall ill") + "."
                                       : Cap(obj) + " came to " + place + (e.Other.IsNone ? "" : " with " + other) + ".";
                case HistoryKind.EpidemicEnded: return Cap(obj) + " left " + place + " after " + e.ArgA + " days (" + F(e.Magnitude, "0") + " dead).";
                case HistoryKind.TechniqueFound: return subj + " of " + place + " found how to " + TechName(e.ArgA) + ".";
                case HistoryKind.TechniqueKnown: return "The people of " + place + " now " + (e.ArgA >= 0 && e.ArgA < Knowledge.Tech.Count ? Knowledge.Tech.Laws[e.ArgA].Now : "know it") + (e.Object.IsNone ? "" : " (taught by " + obj + ")") + ".";
                case HistoryKind.TechniqueLost: return "The people of " + place + " no longer know how to " + TechName(e.ArgA) + ".";
                case HistoryKind.ColdHomes:
                    return "The homes of " + subj + " were cold (" + F(e.Magnitude, "0") + (e.Magnitude >= 1.5 ? " households" : " household") + ", " + e.ArgB + " C outside)" +
                           (e.ArgA == 1 ? ": the woodstore ran short." : ": camps and huts in the frost.");
                case HistoryKind.GodCommand: return "You reached down near " + place + ".";
                default: return e.Kind + " at " + place;
            }
        }

        /// <summary>
        /// What a person is doing and why, as a sentence (the inspector's "why", section 8): the activity, the reason it won
        /// (the WhyCode the decision recorded) and its place - read from state, nothing invented.
        /// </summary>
        public static string Doing(World w, in Person p)
        {
            var H = w.History;
            string home = H.NameOf(EntityRef.Settlement(p.Home));
            string at = p.At != 0 ? H.NameOf(EntityRef.Settlement(p.At)) : "";
            string dest = p.Dest != 0 ? H.NameOf(EntityRef.Settlement(p.Dest)) : "";
            string ground = p.Ground >= 0 ? " near " + w.PlaceName(p.Ground) : "";
            if (!p.Alive) return "dead";
            switch (p.Act)
            {
                case ActivityKind.Sleep: return "asleep at home";
                case ActivityKind.Infant: return "a baby, kept at home and fed there";
                case ActivityKind.Eat: return "eating at home";
                case ActivityKind.Play: return "playing near home: a child";
                case ActivityKind.Fields: return p.Why == WhyCode.Harvest ? "bringing in the harvest of " + home : "working the fields of " + home + ": the crop needs hands";
                case ActivityKind.Wood: return "cutting wood: " + home + "'s woodstore is low";
                case ActivityKind.Water: return "fetching water " + ((w.Settlements.Exists(p.Home) && (w.Settlements.Get(p.Home).Works & 1) != 0) ? "from the well" : "from the stream") + ": the household's pots are low";
                case ActivityKind.Ill:
                    if (p.Why == WhyCode.Hurt) return "abed at home with wounds from a fight in the night: they must heal";
                    return "abed at home with " + w.History.NameOf(new EntityRef(EntityKind.Pathogen, p.Sick)) + ": it must run its course";
                case ActivityKind.Watch: return "keeping watch at " + home + "'s store through the night: grain has been taken from it";
                case ActivityKind.Craft: return "making and mending at home - spinning, weaving, sewing, tools: the household's clothes and tools are wearing thin";
                case ActivityKind.Cook: return p.Why == WhyCode.Hungry ? "making a meal: hungry, and nothing is cooked at home" : "grinding grain and cooking for the household: the next meals are not made";
                case ActivityKind.Social: return "on the square with the neighbours, the day's work done";
                case ActivityKind.Idle:
                    {
                        // rest has its why too: a long day's work behind them, or nothing pressing in the fields or at home
                        if (p.Worked >= 6f) return "resting at home after a long day's work";
                        if (p.Why != WhyCode.Leisure || !w.Settlements.Exists(p.Home)) return "resting at home";
                        var crop = w.Settlements.Get(p.Home).Crop;
                        string fields = crop == TC2.Core.Places.CropStage.Harvested || crop == TC2.Core.Places.CropStage.Fallow ? "the harvest is in" :
                                        crop == TC2.Core.Places.CropStage.Growing ? "the crop is weeded and growing" :
                                        crop == TC2.Core.Places.CropStage.Sown ? "the day's sowing is done" : "the fields need no more hands today";
                        return "resting at home: " + fields + " and nothing at home presses";
                    }
                case ActivityKind.TripOut:
                    if (p.Why == WhyCode.Trade) return "walking to " + (dest.Length > 0 ? dest : "a neighbouring village") + " with " + Load(p.Cargo, p.CargoQty) + " to trade: " + home + " spares it and they want it";
                    if (p.Why == WhyCode.Take)
                        return "walking to " + (dest.Length > 0 ? dest : "a neighbouring village") + " in the dark to take grain from its store: " +
                               (p.Nutrition < 0.5f ? "there is nothing left to eat at home, and its store is full" : "to take back for the wrong " + dest + " did " + home);
                    if (p.Why == WhyCode.Raid) return "walking with a band to fall on " + (dest.Length > 0 ? dest : "a neighbouring village") + " in the night: " + home + " holds a wrong against it, or hunger drives it";
                    if (p.Why == WhyCode.Claim) return "walking to " + (dest.Length > 0 ? dest : "a neighbouring village") + " to ask amends for blood its people shed: " + home + " holds them to it";
                    return "walking to " + (dest.Length > 0 ? dest : "a neighbouring village") + (p.Why == WhyCode.VisitKin ? " to see kin" : " to see a friend");
                case ActivityKind.TripHome:
                    if (p.Why == WhyCode.Raid) return "coming home to " + home + " from a raid in the night" + (p.CargoQty > 0f ? " with " + Load(p.Cargo, p.CargoQty) : "") + (p.Hurt > 0f ? ", hurt" : "");
                    if (p.Why == WhyCode.Take) return p.CargoQty > 0f ? "hurrying home to " + home + " in the dark with " + Load(p.Cargo, p.CargoQty) + " taken from a neighbour's store" : "running home to " + home + " in the dark: seen at the store, they left the grain";
                    return "walking home to " + home + (p.CargoQty > 0f ? " with " + Load(p.Cargo, p.CargoQty) : "");
                case ActivityKind.Visit: return "visiting " + at;
                case ActivityKind.Flee: return "keeping to shelter: a fire is near";
                case ActivityKind.Wait: return "waiting on the road for the fire ahead to burn out";
                case ActivityKind.Hunt:
                case ActivityKind.Fish:
                case ActivityKind.Gather:
                    {
                        string what = p.Act == ActivityKind.Hunt ? "hunting" : p.Act == ActivityKind.Fish ? "fishing" : "gathering nuts, roots and berries";
                        string why = p.Why == WhyCode.NoFood ? ": nothing is left to eat at home" : p.Why == WhyCode.FoodShort ? ": rations are short in " + home : ": food for the table";
                        return what + ground + why;
                    }
                case ActivityKind.Haul: return "carrying the catch home";
                case ActivityKind.Build:
                    if (p.Why == WhyCode.NoRoof) return "raising a roof for the household: they live in a camp or a hut";
                    if (w.Settlements.Exists(p.Home) && TC2.Core.Places.Construction.NextWork(w.Settlements.Get(p.Home)) == TC2.Core.Places.Construction.Structure && w.Buildings.Exists(w.Settlements.Get(p.Home).Project))
                    {
                        ref var pb = ref w.Buildings.Get(w.Settlements.Get(p.Home).Project);
                        return "building for " + home + ": " + WorkWhy(pb.Kind, w.Settlements.Get(p.Home).Granaries);
                    }
                    if (w.Settlements.Exists(p.Home) && (w.Settlements.Get(p.Home).Wall == 1 || (w.Settlements.Get(p.Home).Wall == 2 && TC2.Core.Places.Construction.NextWork(w.Settlements.Get(p.Home)) == TC2.Core.Places.Construction.Mend)))
                        return "setting posts in " + home + "'s palisade: other villages' people have cost it dear";
                    return "building for " + home + ": the village needs a well or a granary";
                case ActivityKind.Smith:
                    return "at " + home + "'s smithy, " + (w.Settlements.Exists(p.Home) && w.Settlements.Get(p.Home).Arms < TC2.Core.Places.Forge.ArmsWant(w.Settlements.Get(p.Home)) ? "forging arms: " + home + " fears its neighbours" : "smelting and working metal for the village's tools");
                case ActivityKind.Quarry: return "cutting stone at " + home + "'s quarry and carrying it home: the village wants stone for its houses' footings";
                case ActivityKind.Scout: return "gone to look at the land beyond: " + home + " is crowded or hungry and knows no good place";
                case ActivityKind.Move: return "walking with the household to a new home" + (dest.Length > 0 ? " at " + dest : "");
                case ActivityKind.Tend: return "watching " + home + "'s herd on the pasture" + ground + ": the animals must graze, and the wolves keep off a watched herd";
                default: return p.Act + " (" + p.Why + ")";
            }
        }

        /// <summary>A village work and why it was wanted (C1.1): "a granary: their harvest needs a store".</summary>
        public static string WorkWhy(byte kind, int granaries)
        {
            if (kind == TC2.Core.Places.Structures.Well) return "a well: water near the homes";
            if (kind == TC2.Core.Places.Structures.Granary)
                return granaries > 0 ? "another granary: their harvest overfilled the store" : "a granary: their harvest needs a store";
            if (kind == TC2.Core.Places.Structures.Barn) return "a barn: their harvest overfilled the granaries";
            if (kind == TC2.Core.Places.Structures.Smithy) return "a smithy: they have a smith and use for metal";
            if (kind == TC2.Core.Places.Structures.Quarry) return "a quarry at the rock they found: stone for their houses' footings";
            return "a " + TC2.Core.Places.Structures.Name(kind);
        }

        static string Load(byte g, float q) =>
            g == 1 ? F(q, "0") + " days' grain" : g == 2 ? F(q, "0.0") + " kg of copper" : g == 3 ? F(q, "0") + " kg of fibre" : g == 4 ? F(q, "0") + " hides" : "a load";

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>The sickness an epidemic event names (its pathogen), for a death it caused.</summary>
        static string Sickness(World w, long cause)
        {
            if (cause != 0 && w.History.TryGet(cause, out var c))
            {
                if (c.Object.Kind == EntityKind.Pathogen) return w.History.NameOf(c.Object);
                if (c.Subject.Kind == EntityKind.Pathogen) return w.History.NameOf(c.Subject);
            }
            return "a sickness";
        }

        static string TechName(int t) => t >= 0 && t < Knowledge.Tech.Count ? Knowledge.Tech.Laws[t].Name : "do it";

        public static string Line(World w, in HistoryEvent e)
        {
            return SimTime.FormatShort(e.Time) + "  [" + e.Tier + "/" + e.RootKind + "] " + Describe(w, e);
        }

        /// <summary>A line of the History Book: the record's own words under its date ("Year 41, late summer: the harvest failed
        /// at Gouthorpe (100% short).") - the ledger's tier and root tags are for tools (D2-077).</summary>
        public static string Dated(World w, in HistoryEvent e)
        {
            return SimTime.FormatDate(e.Time) + ": " + Describe(w, e);
        }

        /// <summary>The chain of causes of an event, effect first, as text lines.</summary>
        public static string Why(World w, long eventId)
        {
            var sb = new StringBuilder();
            var buf = new long[16];
            int n = w.History.CausalChain(eventId, buf);
            for (int i = 0; i < n; i++)
            {
                if (!w.History.TryGet(buf[i], out var e)) { sb.Append("  <- (forgotten event ").Append(buf[i]).Append(")\n"); break; }
                sb.Append(i == 0 ? "  " : "  <- ").Append(Line(w, e)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
