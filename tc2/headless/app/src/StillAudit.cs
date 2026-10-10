using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// `place --load SAVE --still [--days N] [--every S] [--from H] [--to H]` (the visual truth audit: people standing still): the saved world lived
        /// in ACTIVITY from its next midnight, every person the view draws (all but the asleep, babes and the sick abed) sampled every S seconds in
        /// their village's daylight hours (local H to H). Each sample is what WorldObjectsView.RebuildPeople draws: walking (a road, or the walk at the
        /// start of a step) or standing at one point, facing one way, without moving. For each activity: its share of the people drawn, how many of
        /// them stand, how long a standing bout lasts (from arriving at the spot to the step's end) and where they stand (inside their own house's
        /// walls - hidden; by their door; elsewhere in the village; out on the land). Reads only.
        /// </summary>
        static void StillAudit(World w, Args a)
        {
            long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day;
            if (w.StepDays > 1) w.RequestStepDays(1, next0);
            if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0);
            w.AdvanceTo(next0 + SimTime.Hour);
            long end = w.Now + SimTime.Days(a.Double("days", 2));
            long every = a.Int("every", 120);
            double h0 = a.Double("from", 7), h1 = a.Double("to", 19);
            const int K = 40;
            var seen = new double[K]; var still = new double[K]; var inside = new double[K]; var door = new double[K]; var village = new double[K]; var land = new double[K];
            var bout = new double[K]; var boutN = new double[K];
            var hid = new double[K]; var statue = new double[K]; var sitting = new double[K];   // (as drawn now - Presence: indoors, neither moving nor at work, sitting)
            double hiddenN = 0, statuesN = 0, talkN = 0; var talk = new double[K];
            var why = new Dictionary<(int act, int why), double>();
            double samples = 0, drawn = 0, standing = 0, walking = 0;
            var lastStep = new Dictionary<int, long>();   // (each standing bout counted once: person -> its step's start)
            while (w.Now < end)
            {
                w.AdvanceTo(Math.Min(end, w.Now + every));
                long t = w.Now;
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive) continue;
                    int home = p.Home;
                    if (home == 0 || !w.Settlements.Exists(home)) continue;
                    ref var hs = ref w.Settlements.Get(home);
                    long lt = t + PlanetState.LocalOffset(hs.Anchor);
                    double hour = SimTime.FloorMod(lt, SimTime.Day) / 3600.0;
                    if (hour < h0 || hour >= h1) continue;
                    samples++;
                    if (p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant || p.Act == ActivityKind.Ill) continue;   // (indoors: not drawn)
                    int k = (int)p.Act; if (k < 0 || k >= K) k = 0;
                    drawn++; seen[k]++;
                    if (!Presence.Of(w, in p, t, out _, out _, out byte pose, out bool mv)) { hid[k]++; hiddenN++; }
                    else if (!mv && !Presence.Busy(pose)) { statue[k]++; statuesN++; if (pose == Presence.Sit) sitting[k]++; }
                    else if (Presence.Talking(pose)) { talk[k]++; talkN++; }
                    bool moving = p.Step == StepKind.Travel ? p.S0 != p.S1 : (p.Step == StepKind.Local && t < p.TA);
                    if (moving) { walking++; continue; }
                    standing++; still[k]++;
                    var key = (k, (int)p.Why);
                    why[key] = (why.TryGetValue(key, out double v) ? v : 0) + 1;
                    if (!lastStep.TryGetValue(id, out long ls) || ls != p.T0) { lastStep[id] = p.T0; bout[k] += (p.T1 - p.TA) / 60.0; boutN[k]++; }
                    if (p.Step == StepKind.Travel) { land[k]++; continue; }
                    // where: inside its own house's walls (hidden by them), by its door (within 8 m), in the village (within 140 m of the square), on the land
                    int sid = p.At != 0 ? p.At : p.Home;
                    ref var s = ref w.Settlements.Get(sid);
                    double x = p.ToX, y = p.ToY;
                    bool hidden = false, byDoor = false;
                    if (w.Households.Exists(p.Household))
                    {
                        ref var hh = ref w.Households.Get(p.Household);
                        if (hh.Settlement == sid)
                        {
                            byDoor = Dist(x - hh.HomeX, y - hh.HomeY) < 8.0;
                            if (hh.Building != 0 && w.Buildings.Exists(hh.Building))
                            {
                                ref var b = ref w.Buildings.Get(hh.Building);
                                if (Structures.IsHome(b.Kind) && !Homes.Fallen(in b, t))
                                {
                                    int plan = Footprints.PlanOf(in b);
                                    double f = Footprints.FaceOf(in s, in b);
                                    for (int r = 0; r < Footprints.Count(plan) && !hidden; r++)
                                    {
                                        Footprints.Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                                        hidden = Footprints.BoxDisc(b.X, b.Y, f, cx, cz, hx - Footprints.Eaves - 0.2, hz - Footprints.Eaves - 0.2, x, y, 0.01, 0.0);
                                    }
                                }
                            }
                        }
                    }
                    if (hidden) inside[k]++;
                    else if (byDoor) door[k]++;
                    else if (Dist(x - s.SquareX, y - s.SquareY) < 140.0) village[k]++;
                    else land[k]++;
                }
            }
            Console.WriteLine("people drawn in daylight (" + h0.ToString("0") + "-" + h1.ToString("0") + " h local), " + a.Double("days", 2).ToString("0") + " days from " + SimTime.FormatShort(next0) + ", sampled every " + every + " s: " +
                              (100.0 * drawn / Math.Max(1, samples)).ToString("0.0") + " % of the living drawn; of them walking " + (100.0 * walking / Math.Max(1, drawn)).ToString("0.0") + " %, standing still " + (100.0 * standing / Math.Max(1, drawn)).ToString("0.0") + " %");
            Console.WriteLine("  as drawn now (Presence): indoors and not drawn " + (100.0 * hiddenN / Math.Max(1, drawn)).ToString("0.0") + " %, neither moving nor at work " + (100.0 * statuesN / Math.Max(1, drawn)).ToString("0.0") + " % of those drawn before; at leisure with others (talking) " + (100.0 * talkN / Math.Max(1, drawn)).ToString("0.0") + " %");
            Console.WriteLine("  activity      drawn%  still%  bout-min   inside-house  by-door  village  land   (shares of the still)   now: indoors% still% (sitting%)   whys");
            var order = new List<int>();
            for (int k = 0; k < K; k++) if (seen[k] > 0) order.Add(k);
            order.Sort((x, y) => seen[y].CompareTo(seen[x]));
            foreach (int k in order)
            {
                double st = Math.Max(1, still[k]);
                var ws = new List<(double n, string w)>();
                foreach (var kv in why) if (kv.Key.act == k) ws.Add((kv.Value, ((WhyCode)kv.Key.why).ToString()));
                ws.Sort((x, y) => y.n.CompareTo(x.n));
                var wsb = new System.Text.StringBuilder();
                for (int i = 0; i < Math.Min(3, ws.Count); i++) wsb.Append(" " + ws[i].w + " " + (100.0 * ws[i].n / st).ToString("0"));
                Console.WriteLine("  " + ((ActivityKind)k).ToString().PadRight(12) + (100.0 * seen[k] / Math.Max(1, drawn)).ToString("0.0").PadLeft(7) + (100.0 * still[k] / seen[k]).ToString("0").PadLeft(8) +
                                  (boutN[k] > 0 ? (bout[k] / boutN[k]).ToString("0") : "-").PadLeft(10) +
                                  (100.0 * inside[k] / st).ToString("0").PadLeft(14) + (100.0 * door[k] / st).ToString("0").PadLeft(9) + (100.0 * village[k] / st).ToString("0").PadLeft(9) + (100.0 * land[k] / st).ToString("0").PadLeft(6) +
                                  (100.0 * hid[k] / seen[k]).ToString("0").PadLeft(12) + (100.0 * statue[k] / seen[k]).ToString("0").PadLeft(7) + (" (" + (100.0 * sitting[k] / seen[k]).ToString("0") + ")").PadLeft(6) + "  " + wsb);
            }
        }
    }
}
