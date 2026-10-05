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
                        string how = e.ArgA == 2 ? " of hunger" : (e.ArgA == 3 ? " in childbirth" : "");
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
