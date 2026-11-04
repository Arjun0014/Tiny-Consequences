using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Agents
{
    /// <summary>
    /// How a person is seen (the visual truth audit, session 17): where they are drawn, which way they face and what their body is doing, read
    /// from the simulation's own state at a moment - the activity, its step, its place and its kind of work. Presentation elaborates a real activity
    /// and never invents one: a field hand hoes along the row the village sows, a woodcutter swings at the woods, a smith at the anvil, a herder walks
    /// with the herd, neighbours at leisure on the green turn to each other, someone idle at home sits by the door, and those whose work is
    /// indoors (eating, cooking at the hearth) are not drawn where a roof stands over them. The simulation's point (`AgentSystem.PositionAt`) stays
    /// the person's place; the drawn point keeps within a few metres of it (within the work's own ground). Reads only; one answer for the view, the
    /// picking and the headless audit (`place --still`). Before, every person after their step's walk stood at one point, facing one way, for the
    /// whole two to five hours of their work.
    /// </summary>
    public static class Presence
    {
        /// <summary>The body's poses (the person shader's codes, `_Anim`): standing, walking, bent hoeing, bent reaping or gathering, swinging an axe
        /// or a pick, hammering, sitting, talking, walking with a load, rowing, working with the hands standing (a loom), drawing water, sparring,
        /// running (children at play), fishing from the shore, sitting at work with the hands (spinning, mending, throwing pots), sitting and talking.</summary>
        public const byte Stand = 0, Walk = 1, Hoe = 2, Reap = 3, Chop = 4, Hammer = 5, Sit = 6, Talk = 7, Carry = 8, Row = 9, Loom = 10, Draw = 11,
                          Spar = 12, Run = 13, Fish = 14, SitWork = 15, SitTalk = 16, Flail = 17,
                          // (session 32, Arjun: "most people I'm looking at are standing still and doing nothing") the body astir in place at what they
                          // really do: standing at leisure (the weight shifted, a look round, a hand to the hip or the head), sitting at leisure (leaning
                          // back, hugging the knees, a look round), pointing (an arm out toward what they face: where a shock struck, a fire), looking out
                          // (a hand to the brow, the trunk turning: a watch, a herder, a hunter waiting), stoking (bent to a fire or a clamp with a pole),
                          // running in alarm (hard, the arms pumping, now and then a hand flung up), a child hopping and clapping at play
                          Loiter = 18, SitIdle = 19, Point = 20, Look = 21, Stoke = 22, Scared = 23, Hop = 24;

        /// <summary>Whether a pose shows the body at work or moving (else it stands or sits still: true of leisure and waiting).</summary>
        public static bool Busy(byte pose) => pose != Stand && pose != Sit;

        /// <summary>Whether a pose is at leisure with others (talking, standing or sitting): neither still nor at work.</summary>
        public static bool Talking(byte pose) => pose == Talk || pose == SitTalk;

        /// <summary>Whether a pose is astir in place, not at work: standing or sitting at leisure with the body moving, looking out, pointing, hopping.</summary>
        public static bool Astir(byte pose) => pose == Loiter || pose == SitIdle || pose == Point || pose == Look || pose == Hop;

        /// <summary>
        /// Where and how person `p` is drawn at time t (seconds, fractional): the direction from the planet's centre, the way they face (a unit
        /// tangent; zero when the caller should face them along their motion) and their pose; `moving` when the drawn point moves. False when they
        /// are indoors and not drawn (asleep, a babe, abed, eating or cooking under their own roof).
        /// </summary>
        public static bool Of(World w, in Person p, double t, out Vec3d dir, out Vec3d face, out byte pose, out bool moving)
        {
            face = default; pose = Stand; moving = false;
            dir = w.Agents.PositionAt(in p, t);
            if (!p.Alive || p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant || p.Act == ActivityKind.Ill) return false;
            bool carrying = p.Act == ActivityKind.Haul || p.Why == WhyCode.BringHome || (p.Cargo != Trade.None && p.CargoQty > 0f);
            if (p.Step == StepKind.Travel)
            {
                moving = p.S0 != p.S1;
                pose = !moving ? Loiter : carrying ? Carry : p.Act == ActivityKind.Flee ? Scared : Walk;
                // on the way as it is drawn: in a village along the street it leaves by (WayLines), facing the way they go
                var r = w.Agents.RouteOf(in p);
                if (r != null && p.Act != ActivityKind.Voyage && p.Act != ActivityKind.Sail)
                {
                    dir = WayLines.PointAt(w, r, AgentSystem.RouteSAt(p, t));
                    if (moving) face = Tangent(dir, WayLines.PointAt(w, r, AgentSystem.RouteSAt(p, t + 20.0)));
                }
                return true;
            }
            int sid = p.At != 0 ? p.At : p.Home;
            if (!w.Settlements.Exists(sid)) return true;
            ref var s = ref w.Settlements.Get(sid);
            if (p.Step == StepKind.Local && t < p.TA)
            {
                moving = true;
                pose = carrying ? Carry : p.Act == ActivityKind.Flee ? Scared : p.Act == ActivityKind.Play ? Run : Walk;
                double wx = (double)p.ToX - p.FromX, wy = (double)p.ToY - p.FromY;
                if (p.TBend > p.T0 && p.TBend < p.TA)   // (round the water: along the leg they are on - D2-156)
                {
                    if (t < p.TBend) { wx = (double)p.BendX - p.FromX; wy = (double)p.BendY - p.FromY; }
                    else { wx = (double)p.ToX - p.BendX; wy = (double)p.ToY - p.BendY; }
                }
                if (wx * wx + wy * wy > 1e-6) face = (s.East * wx + s.North * wy).Normalized;
                return true;
            }
            double x = p.ToX, y = p.ToY;
            ulong h = KeyedRng.Mix((ulong)p.Id * 0x9E3779B97F4A7C15UL ^ 0x5EE5);
            double u0 = KeyedRng.Unit(h), u1 = KeyedRng.Unit(KeyedRng.Mix(h + 1)), u2 = KeyedRng.Unit(KeyedRng.Mix(h + 2));
            double since = t - p.TA;   // (seconds at the spot)
            double fx = 0, fy = 0;      // the way they face, in the village's frame (east, north); 0, 0: a bearing by the hash
            switch (p.Act)
            {
                case ActivityKind.Eat:
                case ActivityKind.Cook:
                    // at the hearth, under their own roof: not seen (the chimney's smoke shows the cooking); at a camp, by its fire
                    if (Roofed(w, in p, sid)) return false;
                    Door(w, in p, sid, u0, ref x, ref y, ref fx, ref fy);
                    pose = p.Act == ActivityKind.Cook ? SitWork : SitIdle;
                    break;
                case ActivityKind.Idle:
                {
                    // (session 32) in from a gale: keeping in at home while it blows (AgentSystem.ShockStep) - under their roof, not seen
                    if (p.Why == WhyCode.Shock && sid == p.Home && Roofed(w, in p, sid)) return false;
                    // idle at home: by the door in the day, facing out - turned to the neighbours idle at their doors near by, talking. Their own
                    // round of it (session 32: sat still the whole hour before): sat a while, up on their feet a while, now and then a few steps out
                    // before the door to look down the street and back
                    Door(w, in p, sid, u0, ref x, ref y, ref fx, ref fy);
                    bool grp = GroupCentre(w, in p, sid, t, out double mx, out double my) && (mx - x) * (mx - x) + (my - y) * (my - y) > 0.25;
                    if (grp) { fx = mx - x; fy = my - y; }
                    double per = 300.0, v = since + per * u0, f = (v - DMath.Floor(v / per) * per) / per;
                    long slot = DMath.ToLong(DMath.Floor(v / per));
                    bool speaks = Roll(p.Id, DMath.ToLong(DMath.Floor((since + 40.0 * u2) / 40.0)), 0x7A1) < 0.6;
                    double sitShare = 0.35 + 0.35 * u1;   // (some sit most of their leisure, some stand)
                    if (f < sitShare) pose = grp && speaks ? SitTalk : SitIdle;
                    else if (f < 0.75 || p.Why == WhyCode.Shock) pose = grp && speaks ? Talk : Loiter;
                    else
                    {
                        // a few steps out (5 m) and back, then stood there; the way out turned a little each time
                        double g = (f - 0.75) / 0.25;   // (75 s: 9 s out at a stroll, a look, 9 s back)
                        double a = (Roll(p.Id, slot, 0x57E) - 0.5) * 1.4, l = DMath.Sqrt(fx * fx + fy * fy);
                        double ox = l > 1e-9 ? fx / l : 1.0, oy = l > 1e-9 ? fy / l : 0.0;
                        double bx = ox * DMath.Cos(a) - oy * DMath.Sin(a), by = ox * DMath.Sin(a) + oy * DMath.Cos(a);
                        double outM = g < 0.12 ? g / 0.12 : g < 0.88 ? 1.0 : (1.0 - g) / 0.12;
                        double reach = grp ? DMath.Min(5.0, 0.6 * DMath.Sqrt((mx - x) * (mx - x) + (my - y) * (my - y))) : 5.0;   // (over toward the neighbours)
                        x += bx * reach * outM; y += by * reach * outM;
                        if (g < 0.12) { fx = bx; fy = by; pose = Walk; moving = true; }
                        else if (g >= 0.88) { fx = -bx; fy = -by; pose = Walk; moving = true; }
                        else { fx = bx; fy = by; pose = grp ? (speaks ? Talk : Loiter) : Look; }
                    }
                    break;
                }
                case ActivityKind.Garden:
                {
                    // in the household's croft behind its house: along a bed a couple of metres to and fro - the spade and the hoe while the crops grow,
                    // stooped to the gathering from the ripening
                    double run = Tri(since * 0.08 / 3.0 + u0) * 3.0 - 1.5, a = u1 * DMath.TwoPi;
                    x += run * DMath.Cos(a); y += run * DMath.Sin(a);
                    fx = DMath.Cos(a) * TriSlope(since * 0.08 / 3.0 + u0); fy = DMath.Sin(a) * TriSlope(since * 0.08 / 3.0 + u0);
                    pose = s.Crop == CropStage.Ripe || s.Crop == CropStage.Harvested ? Reap : Hoe;
                    moving = true;
                    break;
                }
                case ActivityKind.Muck:
                    // on the fields with the muck: forking it off the cart's or the basket's load and spreading it, a few metres to and fro
                    if (Cycle(since + 90.0 * u0, 90.0, 60.0)) { pose = Hoe; fx = DMath.Cos(u1 * DMath.TwoPi); fy = DMath.Sin(u1 * DMath.TwoPi); }
                    else Fetch(since + 90.0 * u0, 90.0, 60.0, 5.0, ref x, ref y, ref fx, ref fy, out pose, out moving);
                    break;
                case ActivityKind.Thresh:
                {
                    // at the village's threshing floor (C3.7b): in a ring round the sheaves laid out on it, facing its middle, the flails falling to a
                    // beat - a breather now and then, the straw turned and the grain swept up
                    double a = Threshing.RingAngle(p.Id);
                    fx = -DMath.Cos(a); fy = -DMath.Sin(a);
                    pose = Cycle(since + 150.0 * u0, 150.0, 125.0) ? Flail : Loiter;   // (the breather: the weight shifted, a word, the brow wiped)
                    break;
                }
                case ActivityKind.Craft:
                    // making and mending the household's clothes and tools: at the doorstep in the light, the work in the lap
                    Door(w, in p, sid, 0.5 + 0.5 * u0, ref x, ref y, ref fx, ref fy);
                    pose = SitWork;
                    break;
                case ActivityKind.Fields:
                {
                    // along the rows of the village's plots (drawn in its own grid: rows along its east), a few metres to and fro - sowing walks the
                    // furrows broadcasting, the growing crop is hoed, the ripe one reaped, at their own slow pace
                    CropStage st = s.Crop;
                    // (session 32: at 0.05 m/s a field hand read as a statue from the village view) up a row at the work's pace - broadcasting
                    // 0.5 m/s, hoeing 0.12, reaping 0.08 - then back down the headland at a walk to the next row, the rows taken in turn across
                    // eight of them and back
                    double speed = st == CropStage.Sown ? 0.5 : st == CropStage.Ripe ? 0.08 : 0.12, span = st == CropStage.Sown ? 22.0 : 9.0, back = 1.1;
                    double tw = span / speed, per = tw + span / back, v = since + per * u0;
                    long row = DMath.ToLong(DMath.Floor(v / per));
                    double f = v - DMath.Floor(v / per) * per, along, across;
                    double r0 = (Tri(row / 16.0) * 8.0 - 4.0) * 0.9, r1 = (Tri((row + 1) / 16.0) * 8.0 - 4.0) * 0.9;
                    bool working = f < tw;
                    if (working) { along = f * speed; across = r0; }
                    else { double g = (f - tw) / (per - tw); along = span * (1.0 - g); across = r0 + (r1 - r0) * g; }
                    along -= 0.5 * span;
                    double dirSign = working ? 1.0 : -1.0;
                    bool rows = u1 < 0.5;   // (half work along the rows, half across)
                    if (rows) { x += along; y += across; fx = dirSign; } else { y += along; x += across; fy = dirSign; }
                    pose = !working || st == CropStage.Sown ? Walk : st == CropStage.Ripe ? Reap : Hoe;
                    moving = true;
                    break;
                }
                case ActivityKind.Gather:
                case ActivityKind.Forage:
                {
                    // stooped at a bush or a patch for most of a minute, then a few steps on to the next, along a dozen metres and back
                    double per = 60.0, v = since + per * u0, f = v - DMath.Floor(v / per) * per, a = u1 * DMath.TwoPi;
                    long i = DMath.ToLong(DMath.Floor(v / per));
                    double p0 = Tri(i / 6.0) * 12.0 - 6.0, p1 = Tri((i + 1) / 6.0) * 12.0 - 6.0, run;
                    if (f < 46.0) { run = p0; pose = Reap; }
                    else { run = p0 + (p1 - p0) * (f - 46.0) / 14.0; pose = Walk; }
                    double sl = p1 >= p0 ? 1.0 : -1.0;
                    x += run * DMath.Cos(a); y += run * DMath.Sin(a);
                    fx = DMath.Cos(a) * sl; fy = DMath.Sin(a) * sl;
                    moving = true;
                    break;
                }
                case ActivityKind.Wood:
                    // at a tree: swinging, a breather now and then (a minute and a half of work, twenty seconds' rest)
                    // (session 32) the breather spent carrying what was cut to the stack a few metres off and back, not stood
                    fx = DMath.Cos(u1 * DMath.TwoPi); fy = DMath.Sin(u1 * DMath.TwoPi);
                    if (Cycle(since + 110.0 * u0, 110.0, 90.0)) pose = Chop;
                    else Fetch(since + 110.0 * u0, 110.0, 90.0, 4.0, ref x, ref y, ref fx, ref fy, out pose, out moving);
                    break;
                case ActivityKind.Quarry:
                case ActivityKind.Mine:
                    // cutting at the face (or digging the bog's pits), then carrying what was cut to the pile a few metres off
                    Toward(w, p.Act == ActivityKind.Quarry ? s.Quarry : p.Why == WhyCode.BogOre ? s.FeMine : s.Mine, x, y, ref fx, ref fy);
                    if (Cycle(since + 100.0 * u0, 100.0, 80.0)) pose = p.Why == WhyCode.BogOre ? Hoe : Chop;
                    else Fetch(since + 100.0 * u0, 100.0, 80.0, 4.0, ref x, ref y, ref fx, ref fy, out pose, out moving);
                    break;
                case ActivityKind.Burn:
                {
                    // raking and tending the clamp: raking a while, then round it a quarter of the way to its next side and stoking there (a pole
                    // worked into the vents) - taken round the clamp side by side (session 32: stood a breather of 50 s in every two minutes)
                    double per = 120.0, v = since + per * u0, f = v - DMath.Floor(v / per) * per;
                    long lap = DMath.ToLong(DMath.Floor(v / per));
                    if (s.CharHearth != 0 && w.Buildings.Exists(s.CharHearth))
                    {
                        ref var ch = ref w.Buildings.Get(s.CharHearth);
                        double dx = x - ch.X, dy = y - ch.Y, r = DMath.Max(2.5, DMath.Sqrt(dx * dx + dy * dy)), a0 = DMath.Atan2(dy, dx);
                        double q = f < 70.0 ? 0.0 : f < 88.0 ? (f - 70.0) / 18.0 : 1.0;
                        double a = a0 + ((lap & 3) + q) * 0.5 * DMath.PI;
                        x = ch.X + r * DMath.Cos(a); y = ch.Y + r * DMath.Sin(a);
                        if (f >= 70.0 && f < 88.0) { fx = -DMath.Sin(a); fy = DMath.Cos(a); pose = Walk; moving = true; }
                        else { fx = ch.X - x; fy = ch.Y - y; pose = f < 70.0 ? Hoe : Stoke; }
                    }
                    else { pose = f < 70.0 ? Hoe : Stoke; }
                    break;
                }
                case ActivityKind.Smith:
                    pose = Hammer;
                    OutOf(w, sid, p.WhyArg, ref x, ref y);   // (at the smithy's open front, not inside its walls)
                    Toward(w, p.WhyArg, x, y, ref fx, ref fy);
                    break;
                case ActivityKind.Weave:
                    pose = Loom;
                    OutOf(w, sid, p.WhyArg, ref x, ref y);
                    Toward(w, p.WhyArg, x, y, ref fx, ref fy);
                    break;
                case ActivityKind.Pot:
                    OutOf(w, sid, p.WhyArg, ref x, ref y);
                    Toward(w, p.WhyArg, x, y, ref fx, ref fy);
                    if (Cycle(since + 300.0 * u0, 300.0, 220.0)) pose = SitWork;
                    else Fetch(since + 300.0 * u0, 300.0, 220.0, 5.0, ref x, ref y, ref fx, ref fy, out pose, out moving);   // (clay and ware carried)
                    break;
                case ActivityKind.Build:
                    // at the walls they raise or mend: outside them (the simulation's spot is within a few metres of the house's middle)
                    OutOf(w, sid, Nearest(w, sid, x, y), ref x, ref y);
                    NearestBuilding(w, sid, x, y, ref fx, ref fy);
                    if (Cycle(since + 140.0 * u0, 140.0, 100.0)) pose = Hammer;
                    else Fetch(since + 140.0 * u0, 140.0, 100.0, 5.0, ref x, ref y, ref fx, ref fy, out pose, out moving);   // (timber and daub carried up)
                    break;
                case ActivityKind.Water:
                    pose = Draw;
                    fx = s.WellX - x; fy = s.WellY - y;
                    break;
                case ActivityKind.Tend:
                {
                    // with the herd on its ground: walking round it at a herder's pace, then standing to watch it
                    // (session 32) the walk most of the lap, the whole way round, then a look over the herd where the lap began (before: stood 40 %)
                    double lap = 240.0, k = since / lap + u0 - DMath.Floor(since / lap + u0), lap0 = DMath.Floor(since / lap + u0);
                    if (k < 0.82)
                    {
                        double a = (lap0 + k / 0.82) * DMath.TwoPi, r = 6.0 + 6.0 * u1;
                        x += r * DMath.Cos(a); y += r * DMath.Sin(a);
                        fx = -DMath.Sin(a); fy = DMath.Cos(a);
                        pose = Walk; moving = true;
                    }
                    else
                    {
                        double a = (lap0 + 1.0) * DMath.TwoPi, r = 6.0 + 6.0 * u1;
                        x += r * DMath.Cos(a); y += r * DMath.Sin(a);
                        fx = -DMath.Cos(a); fy = -DMath.Sin(a);   // (watching the herd)
                        pose = Look;
                    }
                    break;
                }
                case ActivityKind.Hunt:
                {
                    double run = Tri(since * 0.25 / 40.0 + u0) * 40.0 - 20.0, a = u1 * DMath.TwoPi, sl = TriSlope(since * 0.25 / 40.0 + u0);
                    bool still = Cycle(since + 90.0 * u2, 90.0, 15.0);   // (a stalk, then a short wait, looking out)
                    x += run * DMath.Cos(a); y += run * DMath.Sin(a);
                    fx = DMath.Cos(a) * sl; fy = DMath.Sin(a) * sl;
                    pose = still ? Look : Walk; moving = !still;
                    break;
                }
                case ActivityKind.Fish:
                    pose = Fish;
                    fx = x - s.SquareX; fy = y - s.SquareY;   // (out from the village: toward the water's side)
                    break;
                case ActivityKind.Haul:
                    // (session 32: walked on the spot before) at the load: stooped to it, then carrying it the few metres to where it goes and back;
                    // bringing it home, at their own door - out to the load and in with it
                    fx = DMath.Cos(u1 * DMath.TwoPi); fy = DMath.Sin(u1 * DMath.TwoPi);
                    if (sid == p.Home && Roofed(w, in p, sid)) { Door(w, in p, sid, u0, ref x, ref y, ref fx, ref fy); fx = -fx; fy = -fy; }
                    if (Cycle(since + 80.0 * u0, 80.0, 25.0)) pose = Reap;
                    else Fetch(since + 80.0 * u0, 80.0, 25.0, 5.0, ref x, ref y, ref fx, ref fy, out pose, out moving);
                    break;
                case ActivityKind.Play:
                {
                    // children at play: running about together
                    double a = since * (0.35 + 0.25 * u1) + u0 * DMath.TwoPi, r = 2.0 + 2.5 * u2;
                    x += r * DMath.Cos(a); y += r * DMath.Sin(a);
                    fx = -DMath.Sin(a); fy = DMath.Cos(a);
                    pose = Cycle(since + 60.0 * u0, 60.0, 52.0) ? Run : Hop; moving = pose == Run;   // (a breath between, hopping and clapping)
                    break;
                }
                case ActivityKind.Train:
                    pose = Spar;
                    Group(w, in p, sid, t, ref x, ref y, ref fx, ref fy, 1.6);
                    break;
                case ActivityKind.Social:
                case ActivityKind.Visit:
                    // at leisure on the green, or visiting: those near each other turn to each other and talk; some sit on the grass
                    // (session 32, Arjun: the reactions read cheap - crowds idling every way) kept together by a shock felt: closed in round the
                    // refuge they ran to, on their feet, turned to where it struck - talking it over, an arm out toward it, looking; a child beside
                    // a grown-up of its household kept together there - read from the alarm they hold (its record's place); no state written
                    if (p.Why == WhyCode.Shock && p.Alarm != 0 && w.History.TryGet(p.Alarm, out var shock) && shock.Cell >= 0 && shock.Cell < w.Planet.Cells)
                    {
                        s.SphereToLocal(w.Planet.Grid.Center[shock.Cell], w.Planet.Radius, out double sx, out double sy);
                        bool child = p.AgeYears((long)t) < 12.0;
                        Crowd(w, in p, sid, t, since, child, sx, sy, ref x, ref y, out bool closing);
                        if ((sx - x) * (sx - x) + (sy - y) * (sy - y) > 1.0) { fx = sx - x; fy = sy - y; }
                        double r = Roll(p.Id, DMath.ToLong(DMath.Floor((since + 25.0 * u2) / 25.0)), 0x5C0C);
                        if (closing) { pose = Walk; moving = true; }
                        else pose = child ? (r < 0.75 ? Look : Point) : r < 0.3 ? Point : r < 0.72 ? Talk : Look;
                        break;
                    }
                    // at leisure: a sitting or a standing spell of some minutes (a third of the time sat), speaking or listening by turns
                    Group(w, in p, sid, t, ref x, ref y, ref fx, ref fy, 1.1);
                    {
                        bool sat = Roll(p.Id, DMath.ToLong(DMath.Floor((since + 300.0 * u0) / 300.0)), 0x5175) < 0.3;
                        bool speaks = Roll(p.Id, DMath.ToLong(DMath.Floor((since + 40.0 * u2) / 40.0)), 0x7A1) < 0.55;
                        pose = sat ? (speaks ? SitTalk : SitIdle) : (speaks ? Talk : Loiter);
                    }
                    break;
                case ActivityKind.Watch:
                {
                    // watching outward from the store, looking round; now and then pacing the beat before it (7 m along and back)
                    fx = x - s.SquareX; fy = y - s.SquareY;
                    double per = 200.0, v = since + per * u0, f = (v - DMath.Floor(v / per) * per) / per;
                    if (f < 0.78) pose = Look;
                    else
                    {
                        double g = (f - 0.78) / 0.22, l = DMath.Sqrt(fx * fx + fy * fy);
                        double bx = l > 1e-9 ? -fy / l : 1.0, by = l > 1e-9 ? fx / l : 0.0, m = g < 0.5 ? 2.0 * g : 2.0 - 2.0 * g;
                        x += bx * 7.0 * m; y += by * 7.0 * m;
                        if (g < 0.5) { fx = bx; fy = by; } else { fx = -bx; fy = -by; }
                        pose = Walk; moving = true;
                    }
                    break;
                }
                case ActivityKind.Sail:
                case ActivityKind.Voyage:
                    pose = Row;
                    break;
                case ActivityKind.Flee:
                case ActivityKind.Wait:
                {
                    // (session 32) run clear, or kept back by a fire: turned to the danger they fled - where the shock struck, the nearest fire burning -
                    // looking at it, an arm out toward it by turns; sheltering from a fire at home, at their own door
                    bool fire = p.Why == WhyCode.FireNear || p.Why == WhyCode.FireAhead || p.Why == WhyCode.FireBehind || p.Why == WhyCode.WaitingForFire;
                    if (fire && sid == p.Home) Door(w, in p, sid, u0, ref x, ref y, ref fx, ref fy);
                    bool seen = false;
                    if (p.Why == WhyCode.Shock && p.Alarm != 0 && w.History.TryGet(p.Alarm, out var fled) && fled.Cell >= 0 && fled.Cell < w.Planet.Cells)
                    {
                        s.SphereToLocal(w.Planet.Grid.Center[fled.Cell], w.Planet.Radius, out double sx, out double sy);
                        if ((sx - x) * (sx - x) + (sy - y) * (sy - y) > 1.0) { fx = sx - x; fy = sy - y; seen = true; }
                    }
                    else if (fire)
                    {
                        int fc = FireCell(w, sid);
                        if (fc >= 0)
                        {
                            s.SphereToLocal(w.Planet.Grid.Center[fc], w.Planet.Radius, out double sx, out double sy);
                            if ((sx - x) * (sx - x) + (sy - y) * (sy - y) > 1.0) { fx = sx - x; fy = sy - y; seen = true; }
                        }
                    }
                    double r = Roll(p.Id, DMath.ToLong(DMath.Floor((since + 20.0 * u2) / 20.0)), 0xF1EE);
                    pose = seen ? (r < 0.35 ? Point : Look) : Loiter;
                    break;
                }
                default:
                    pose = Loiter;
                    break;
            }
            if (fx == 0 && fy == 0) { double a = u2 * DMath.TwoPi; fx = DMath.Cos(a); fy = DMath.Sin(a); }
            dir = s.LocalToSphere(x, y, w.Planet.Radius);
            face = (s.East * fx + s.North * fy).Normalized;
            return true;
        }

        /// <summary>A person's own draw for a slot of time (which way a spell goes: sat or stood, speaking or listening) - a hash, no state.</summary>
        static double Roll(int id, long slot, ulong salt) => KeyedRng.Unit(KeyedRng.Mix((ulong)id * 0x9E3779B97F4A7C15UL ^ (ulong)slot * 0xBF58476D1CE4E5B9UL ^ salt));

        /// <summary>The burning cell nearest a village now (-1 none) - read once a moment for each village asked.</summary>
        static int FireCell(World w, int sid)
        {
            if (w.Fire == null || w.Fire.Active.Count == 0) return -1;
            if (_fireAt.TryGetValue(sid, out int c)) return c;
            ref var s = ref w.Settlements.Get(sid);
            double best = double.MaxValue; c = -1;
            for (int i = 0; i < w.Fire.Active.Count; i++)
            {
                int cell = w.Fire.Active[i];
                double d = Vec3d.ArcDistance(w.Planet.Grid.Center[cell], s.Anchor, w.Planet.Radius);
                if (d < best) { best = d; c = cell; }
            }
            _fireAt[sid] = c;
            return c;
        }

        /// <summary>
        /// Those kept together by a shock (session 32), closed in round the refuge they ran to (AgentSystem.ShockStep's: the green, or 220 m past the
        /// point it struck when that was near the green): each their own spot of the 30 m patch drawn in to a third (as tight as a body to 1.2 m2 allows), a child beside the first grown-up
        /// of its household kept together there - closing in over their first 25 s at it (`closing`, a walk). The refuge is reckoned once a moment for
        /// each shock and village; nothing written.
        /// </summary>
        static void Crowd(World w, in Person p, int sid, double t, double since, bool child, double sx, double sy, ref double x, ref double y, out bool closing)
        {
            closing = false;
            Moment(w, t);
            var key = (sid, p.Alarm);
            if (!_refuge.TryGetValue(key, out var rf))
            {
                ref var s = ref w.Settlements.Get(sid);
                double gx = s.SquareX, gy = s.SquareY;
                Boats.OutOfTheWater(w, in s, ref gx, ref gy);
                double dx = gx - sx, dy = gy - sy, d = DMath.Sqrt(dx * dx + dy * dy);
                rf = (gx, gy);
                if (d < 180.0) { double ux = d > 1.0 ? dx / d : 1.0, uy = d > 1.0 ? dy / d : 0.0; rf = (sx + ux * 220.0, sy + uy * 220.0); }
                _refuge[key] = rf;
            }
            // (drawn in to a third - but never packed tighter than a body to 1.2 m2: a great village's crowd fills its 30 m patch)
            int crowd = _crowdN.TryGetValue(key, out int cn) ? cn : 1;
            double In = DMath.Min(1.0, DMath.Max(0.33, DMath.Sqrt(crowd / 750.0)));
            double tx = rf.x + (p.ToX - rf.x) * In, ty = rf.y + (p.ToY - rf.y) * In;
            if (child && p.Household != 0 && _crowdAdult.TryGetValue((sid, p.Alarm, p.Household), out int ad) && ad != p.Id && w.People.Exists(ad))
            {
                ref var a = ref w.People.Get(ad);
                double ax = rf.x + (a.ToX - rf.x) * In, ay = rf.y + (a.ToY - rf.y) * In;
                double fx = sx - ax, fy = sy - ay, l = DMath.Sqrt(fx * fx + fy * fy), side = (p.Id & 1) == 0 ? 0.6 : -0.6;
                if (l > 1e-6) { tx = ax - fy / l * side; ty = ay + fx / l * side; } else { tx = ax + side; ty = ay; }
            }
            double c = since / 25.0;
            if (c < 1.0)
            {
                closing = (tx - x) * (tx - x) + (ty - y) * (ty - y) > 0.25;
                x += (tx - x) * c; y += (ty - y) * c;
            }
            else { x = tx; y = ty; }
        }

        /// <summary>The unit tangent at a from a toward b (zero when they coincide).</summary>
        static Vec3d Tangent(Vec3d a, Vec3d b)
        {
            Vec3d d = b - a;
            d = d - a * Vec3d.Dot(d, a);
            double l = d.Length;
            return l > 1e-12 ? d * (1.0 / l) : default;
        }

        /// <summary>A triangle wave in [0, 1] (to and fro along a row) and the sign of its slope (which way along it).</summary>
        static double Tri(double v) { double f = v - DMath.Floor(v); return f < 0.5 ? 2.0 * f : 2.0 - 2.0 * f; }
        static double TriSlope(double v) { double f = v - DMath.Floor(v); return f < 0.5 ? 1.0 : -1.0; }

        /// <summary>Whether a moment falls in the working part of a cycle (of `period` seconds, the first `work` of them).</summary>
        static bool Cycle(double v, double period, double work) => v - DMath.Floor(v / period) * period < work;

        /// <summary>The carrying part of a work's cycle: from the work spot `dist` m back the way they face and back again, a load on the way in.</summary>
        static void Fetch(double v, double period, double work, double dist, ref double x, ref double y, ref double fx, ref double fy, out byte pose, out bool moving)
        {
            double f = (v - DMath.Floor(v / period) * period - work) / (period - work);   // (0 to 1 over the carrying part)
            double l = DMath.Sqrt(fx * fx + fy * fy);
            double bx = l > 1e-9 ? -fx / l : 1.0, by = l > 1e-9 ? -fy / l : 0.0;
            double out1 = f < 0.5 ? 2.0 * f : 2.0 - 2.0 * f;
            x += bx * dist * out1; y += by * dist * out1;
            if (f < 0.5) { fx = bx; fy = by; pose = Walk; } else { fx = -bx; fy = -by; pose = Carry; }
            moving = true;
        }

        /// <summary>Faces a building of the village (by id) from (x, y).</summary>
        static void Toward(World w, int bid, double x, double y, ref double fx, ref double fy)
        {
            if (bid == 0 || !w.Buildings.Exists(bid)) return;
            ref var b = ref w.Buildings.Get(bid);
            double dx = b.X - x, dy = b.Y - y;
            if (dx * dx + dy * dy > 0.01) { fx = dx; fy = dy; }
        }

        /// <summary>The building whose middle is nearest (x, y) within 12 m (0 none).</summary>
        static int Nearest(World w, int sid, double x, double y)
        {
            var bl = w.BuildingsOf(sid);
            double best = 144.0; int id = 0;
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                double dx = b.X - x, dy = b.Y - y, d2 = dx * dx + dy * dy;
                if (d2 < best) { best = d2; id = bl[i]; }
            }
            return id;
        }

        /// <summary>
        /// A point inside a building's ground (a home's walls, a work's disc) moved out to just beyond it, the shortest way (a home: through its
        /// nearest wall; a point at the middle: out of the front). Nothing where the point is outside or the building is gone.
        /// </summary>
        static void OutOf(World w, int sid, int bid, ref double x, ref double y)
        {
            if (bid == 0 || !w.Buildings.Exists(bid)) return;
            ref var b = ref w.Buildings.Get(bid);
            if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind)) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!Structures.IsHome(b.Kind))
            {
                double r = Footprints.WorkR(b.Kind) + 0.5, dx = x - b.X, dy = y - b.Y, d = DMath.Sqrt(dx * dx + dy * dy);
                if (d >= r) return;
                double f0 = Footprints.FaceOf(in s, in b);
                if (d < 1e-6) { dx = DMath.Cos(f0); dy = DMath.Sin(f0); d = 1.0; }
                x = b.X + dx / d * r; y = b.Y + dy / d * r;
                return;
            }
            int plan = Footprints.PlanOf(in b);
            double face = Footprints.FaceOf(in s, in b), fx = DMath.Cos(face), fy = DMath.Sin(face), ux = -fy, uy = fx;
            for (int k = 0; k < Footprints.Count(plan); k++)
            {
                Footprints.Rect(plan, k, out double cx, out double cz, out double hx, out double hz);
                double ox = b.X + ux * cx + fx * cz, oy = b.Y + uy * cx + fy * cz;
                double lu = (x - ox) * ux + (y - oy) * uy, lf = (x - ox) * fx + (y - oy) * fy;
                if (DMath.Abs(lu) >= hx || DMath.Abs(lf) >= hz) continue;
                // out through the nearest side, 0.5 m beyond the eaves
                double du = hx - DMath.Abs(lu), df = hz - DMath.Abs(lf);
                if (df <= du) lf = (lf >= 0 ? 1 : -1) * (hz + 0.5); else lu = (lu >= 0 ? 1 : -1) * (hx + 0.5);
                x = ox + ux * lu + fx * lf; y = oy + uy * lu + fy * lf;
                return;
            }
        }

        /// <summary>Faces the nearest building within 10 m (a house being raised, a work).</summary>
        static void NearestBuilding(World w, int sid, double x, double y, ref double fx, ref double fy)
        {
            var bl = w.BuildingsOf(sid);
            double best = 100.0;
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                double dx = b.X - x, dy = b.Y - y, d2 = dx * dx + dy * dy;
                if (d2 < best && d2 > 0.01) { best = d2; fx = dx; fy = dy; }
            }
        }

        /// <summary>Whether the person's household has a standing roof in this village (a house or a hut, not fallen).</summary>
        static bool Roofed(World w, in Person p, int sid)
        {
            if (!w.Households.Exists(p.Household)) return false;
            ref var hh = ref w.Households.Get(p.Household);
            if (hh.Settlement != sid || hh.Building == 0 || !w.Buildings.Exists(hh.Building)) return false;
            ref var b = ref w.Buildings.Get(hh.Building);
            return Structures.IsHome(b.Kind) && b.Left == 0;
        }

        /// <summary>
        /// The household's doorstep: before the front of its home (its walls' front and 0.8 m), along it by `along` (0 to 1 of its front), facing
        /// out; a camp's by its tent. Unchanged when they are not at their own home's village.
        /// </summary>
        static void Door(World w, in Person p, int sid, double along, ref double x, ref double y, ref double fx, ref double fy)
        {
            if (!w.Households.Exists(p.Household)) return;
            ref var hh = ref w.Households.Get(p.Household);
            if (hh.Settlement != sid) return;
            ref var s = ref w.Settlements.Get(sid);
            double cx = hh.HomeX, cy = hh.HomeY, face, front, half;
            if (hh.Building != 0 && w.Buildings.Exists(hh.Building))
            {
                ref var b = ref w.Buildings.Get(hh.Building);
                cx = b.X; cy = b.Y;
                face = Footprints.FaceOf(in s, in b);
                int plan = Footprints.PlanOf(in b);
                Footprints.Rect(plan, 0, out double rcx, out double rcz, out double hx, out double hz);
                front = rcz + hz - Footprints.Eaves + 0.8; half = DMath.Max(0.6, hx - Footprints.Eaves - 0.8);
            }
            else { face = Footprints.FaceAt(in s, cx, cy, p.Household); front = 2.4; half = 1.0; }
            double ffx = DMath.Cos(face), ffy = DMath.Sin(face), ux = -ffy, uy = ffx;
            double side = (2.0 * along - 1.0) * half;
            x = cx + ffx * front + ux * side; y = cy + ffy * front + uy * side;
            fx = ffx; fy = ffy;
        }

        // ---------------------------------------------------------------- groups: people together at leisure (or practice) turn to each other
        static World _gw; static double _gt = double.NaN;
        static readonly Dictionary<long, (double x, double y, int n)> _groups = new Dictionary<long, (double, double, int)>();
        static readonly Dictionary<int, int> _rank = new Dictionary<int, int>();
        static readonly Dictionary<(int sid, long alarm), (double x, double y)> _refuge = new Dictionary<(int, long), (double, double)>();
        static readonly Dictionary<(int sid, long alarm, int hh), int> _crowdAdult = new Dictionary<(int, long, int), int>();
        static readonly Dictionary<int, int> _fireAt = new Dictionary<int, int>();
        static readonly Dictionary<(int sid, long alarm), int> _crowdN = new Dictionary<(int, long), int>();
        const double Cell = 9.0;

        static long GroupKey(int sid, int act, double x, double y) => ((long)sid * 64 + act) * 4194304L + ((long)DMath.Floor(x / Cell) + 1024) * 2048 + ((long)DMath.Floor(y / Cell) + 1024);

        /// <summary>
        /// A ring for those at the same leisure (or practice) within a few metres (a 9 m patch of the village's ground): each stands on a ring round
        /// the group's middle facing it; one alone faces where they were. The groups are read once for a moment (derived, never stored).
        /// </summary>
        /// <summary>The middle of the group a person at leisure is in (people idle at their doors in the same patch of the village), when there are two
        /// or more (read from the same groups).</summary>
        static bool GroupCentre(World w, in Person p, int sid, double t, out double mx, out double my)
        {
            double x = p.ToX, y = p.ToY, fx = 0, fy = 0;
            mx = my = 0;
            Group(w, in p, sid, t, ref x, ref y, ref fx, ref fy, 0.0, false);
            if (fx == 0 && fy == 0) return false;
            mx = _cx; my = _cy;
            return true;
        }
        static double _cx, _cy;

        /// <summary>The groups at leisure (and those kept together by a shock, and their households' grown-ups) read once for a moment.</summary>
        static void Moment(World w, double t)
        {
            if (ReferenceEquals(_gw, w) && _gt == t) return;
            _gw = w; _gt = t; _groups.Clear(); _rank.Clear(); _refuge.Clear(); _crowdAdult.Clear(); _fireAt.Clear(); _crowdN.Clear();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var q = ref w.People.Get(id);
                if (!q.Alive || q.Step != StepKind.Local || t < q.TA) continue;
                if (q.Act != ActivityKind.Social && q.Act != ActivityKind.Visit && q.Act != ActivityKind.Train && q.Act != ActivityKind.Idle) continue;
                int qs = q.At != 0 ? q.At : q.Home;
                if (q.Act == ActivityKind.Social && q.Why == WhyCode.Shock && q.Alarm != 0)
                {
                    var ck = (qs, q.Alarm);
                    _crowdN[ck] = (_crowdN.TryGetValue(ck, out int cn) ? cn : 0) + 1;
                }
                if (q.Act == ActivityKind.Social && q.Why == WhyCode.Shock && q.Alarm != 0 && q.Household != 0 && q.AgeYears((long)t) >= 16.0)
                {
                    var ak = (qs, q.Alarm, q.Household);
                    if (!_crowdAdult.ContainsKey(ak)) _crowdAdult[ak] = id;
                }
                long key = GroupKey(qs, (int)q.Act == (int)ActivityKind.Visit ? (int)ActivityKind.Social : (int)q.Act, q.ToX, q.ToY);
                (double x, double y, int n) g = _groups.TryGetValue(key, out var gg) ? gg : (0.0, 0.0, 0);
                _rank[id] = g.n;
                _groups[key] = (g.x + q.ToX, g.y + q.ToY, g.n + 1);
            }
        }

        static void Group(World w, in Person p, int sid, double t, ref double x, ref double y, ref double fx, ref double fy, double spacing, bool place = true)
        {
            Moment(w, t);
            long k = GroupKey(sid, p.Act == ActivityKind.Visit ? (int)ActivityKind.Social : (int)p.Act, p.ToX, p.ToY);
            if (!_groups.TryGetValue(k, out var grp) || grp.n < 2 || !_rank.TryGetValue(p.Id, out int rank)) return;
            double mx = grp.x / grp.n, my = grp.y / grp.n;
            if (!place) { _cx = mx; _cy = my; fx = 1; return; }
            double r = DMath.Max(0.7, spacing * grp.n / DMath.TwoPi * 1.4);
            double a = DMath.TwoPi * rank / grp.n + (k & 255) * 0.1;
            x = mx + r * DMath.Cos(a); y = my + r * DMath.Sin(a);
            fx = mx - x; fy = my - y;
        }
    }
}
