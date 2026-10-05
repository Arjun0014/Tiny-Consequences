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
                        string how = e.ArgA == 2 ? " of hunger" : (e.ArgA == 3 ? " in childbirth" : (e.ArgA == 4 ? " in the eruption" : (e.ArgA == 5 ? " when the earthquake brought the house down" : (e.ArgA == 6 ? " in the great wave" : ""))));
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
                case HistoryKind.Quake:
                    return (e.Magnitude >= 7.5 ? "A great earthquake" : "An earthquake") + " (M " + F(e.Magnitude, "0.0") + ") shook the land near " + place + ".";
                case HistoryKind.Landslides: return "Landslides scarred " + e.ArgA + (e.ArgA == 1 ? " slope" : " slopes") + " near " + place + ".";
                case HistoryKind.StoresSpoiled: return "The earthquake spilled and spoiled " + F(e.Magnitude * 100, "0") + "% of the stores of " + subj + ".";
                case HistoryKind.Tsunami: return "A great wave (" + F(e.Magnitude, "0") + " m) came over the coast near " + place + ", flooding " + e.ArgA + " stretches of shore.";
                case HistoryKind.FieldsFlooded: return "The sea came over the fields of " + subj + " (" + F(e.Magnitude * 100, "0") + "% of the crop lost).";
                case HistoryKind.AshFall: return "Ash fell on the fields of " + subj + " (" + F(e.Magnitude * 100, "0") + "% of the crop lost).";
                case HistoryKind.IceAge: return "An age of ice began: the summers grew too cool to melt the snow, and ice spread over the land.";
                case HistoryKind.IceAgeEnded: return "The age of ice ended: warmer summers are melting the ice sheets back.";
                case HistoryKind.Harvest: return subj + " brought in its harvest (" + F(e.Magnitude, "0") + " food).";
                case HistoryKind.HarvestFailed: return "The harvest failed at " + subj + " (" + F(e.Magnitude * 100, "0") + "% short).";
                case HistoryKind.Hunger: return "Hunger came to " + subj + ": the granary is empty.";
                case HistoryKind.Famine: return "Famine in " + subj + ".";
                case HistoryKind.GodCommand: return "You reached down near " + place + ".";
                default: return e.Kind + " at " + place;
            }
        }

        public static string Line(World w, in HistoryEvent e)
        {
            return SimTime.FormatShort(e.Time) + "  [" + e.Tier + "/" + e.RootKind + "] " + Describe(w, e);
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
