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
                          Spar = 12, Run = 13, Fish = 14, SitWork = 15, SitTalk = 16, Flail = 17;

        /// <summary>Whether a pose shows the body at work or moving (else it stands or sits still: true of leisure and waiting).</summary>
        public static bool Busy(byte pose) => pose != Stand && pose != Sit;

        /// <summary>Whether a pose is at leisure with others (talking, standing or sitting): neither still nor at work.</summary>
        public static bool Talking(byte pose) => pose == Talk || pose == SitTalk;

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
                pose = !moving ? Stand : carrying ? Carry : Walk;
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
                pose = carrying ? Carry : p.Act == ActivityKind.Play || p.Act == ActivityKind.Flee ? Run : Walk;
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
                    pose = p.Act == ActivityKind.Cook ? SitWork : Sit;
                    break;
                case ActivityKind.Idle:
                    // idle at home: sitting by the door in the day, facing out - turned to the neighbours idle at their doors near by, talking
                    Door(w, in p, sid, u0, ref x, ref y, ref fx, ref fy);
                    if (GroupCentre(w, in p, sid, t, out double mx, out double my) && (mx - x) * (mx - x) + (my - y) * (my - y) > 0.25)
                    { fx = mx - x; fy = my - y; pose = u1 < 0.75 ? SitTalk : Talk; }
                    else pose = u1 < 0.75 ? Sit : Stand;
                    break;
                case ActivityKind.Garden:
                {
                    // in the household's croft behind its house: along a bed a couple of metres to and fro - the spade and the hoe while the crops grow,
                    // stooped to the gathering from the ripening
                    double run = Tri(since * 0.04 / 3.0 + u0) * 3.0 - 1.5, a = u1 * DMath.TwoPi;
                    x += run * DMath.Cos(a); y += run * DMath.Sin(a);
                    fx = DMath.Cos(a) * TriSlope(since * 0.04 / 3.0 + u0); fy = DMath.Sin(a) * TriSlope(since * 0.04 / 3.0 + u0);
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
                    pose = Cycle(since + 150.0 * u0, 150.0, 125.0) ? Flail : Stand;
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
                    double speed = st == CropStage.Sown ? 0.28 : st == CropStage.Ripe ? 0.04 : 0.05, span = st == CropStage.Sown ? 22.0 : 9.0;
                    double run = Tri(since * speed / span + u0) * span - 0.5 * span;
                    double dirSign = TriSlope(since * speed / span + u0);
                    bool rows = u1 < 0.5;   // (half work along the rows, half across)
                    if (rows) { x += run; fx = dirSign; } else { y += run; fy = dirSign; }
                    pose = st == CropStage.Sown ? Walk : st == CropStage.Ripe ? Reap : Hoe;
                    moving = true;
                    break;
                }
                case ActivityKind.Gather:
                case ActivityKind.Forage:
                {
                    double run = Tri(since * 0.03 / 6.0 + u0) * 6.0 - 3.0, a = u1 * DMath.TwoPi;
                    x += run * DMath.Cos(a); y += run * DMath.Sin(a);
                    fx = DMath.Cos(a) * TriSlope(since * 0.03 / 6.0 + u0); fy = DMath.Sin(a) * TriSlope(since * 0.03 / 6.0 + u0);
                    pose = Reap; moving = true;
                    break;
                }
                case ActivityKind.Wood:
                    // at a tree: swinging, a breather now and then (a minute and a half of work, twenty seconds' rest)
                    pose = Cycle(since + 110.0 * u0, 110.0, 90.0) ? Chop : Stand;
                    fx = DMath.Cos(u1 * DMath.TwoPi); fy = DMath.Sin(u1 * DMath.TwoPi);
                    break;
                case ActivityKind.Quarry:
                case ActivityKind.Mine:
                    // cutting at the face (or digging the bog's pits), then carrying what was cut to the pile a few metres off
                    Toward(w, p.Act == ActivityKind.Quarry ? s.Quarry : p.Why == WhyCode.BogOre ? s.FeMine : s.Mine, x, y, ref fx, ref fy);
                    if (Cycle(since + 100.0 * u0, 100.0, 80.0)) pose = p.Why == WhyCode.BogOre ? Hoe : Chop;
                    else Fetch(since + 100.0 * u0, 100.0, 80.0, 4.0, ref x, ref y, ref fx, ref fy, out pose, out moving);
                    break;
                case ActivityKind.Burn:
                    pose = Cycle(since + 120.0 * u0, 120.0, 70.0) ? Hoe : Stand;   // (raking and tending the clamp)
                    Toward(w, s.CharHearth, x, y, ref fx, ref fy);
                    break;
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
                    double lap = 240.0, k = since / lap + u0 - DMath.Floor(since / lap + u0);
                    if (k < 0.6)
                    {
                        double a = (since / lap + u0) * DMath.TwoPi, r = 6.0 + 6.0 * u1;
                        x += r * DMath.Cos(a); y += r * DMath.Sin(a);
                        fx = -DMath.Sin(a); fy = DMath.Cos(a);
                        pose = Walk; moving = true;
                    }
                    else
                    {
                        double a = DMath.Floor(since / lap + u0) * DMath.TwoPi + 0.6 * DMath.TwoPi, r = 6.0 + 6.0 * u1;
                        x += r * DMath.Cos(a); y += r * DMath.Sin(a);
                        fx = -DMath.Cos(a); fy = -DMath.Sin(a);   // (watching the herd)
                        pose = Stand;
                    }
                    break;
                }
                case ActivityKind.Hunt:
                {
                    double run = Tri(since * 0.25 / 40.0 + u0) * 40.0 - 20.0, a = u1 * DMath.TwoPi, sl = TriSlope(since * 0.25 / 40.0 + u0);
                    bool still = Cycle(since + 90.0 * u2, 90.0, 30.0);   // (a stalk, then a wait)
                    x += run * DMath.Cos(a); y += run * DMath.Sin(a);
                    fx = DMath.Cos(a) * sl; fy = DMath.Sin(a) * sl;
                    pose = still ? Stand : Walk; moving = !still;
                    break;
                }
                case ActivityKind.Fish:
                    pose = Fish;
                    fx = x - s.SquareX; fy = y - s.SquareY;   // (out from the village: toward the water's side)
                    break;
                case ActivityKind.Haul:
                    pose = Carry;
                    break;
                case ActivityKind.Play:
                {
                    // children at play: running about together
                    double a = since * (0.35 + 0.25 * u1) + u0 * DMath.TwoPi, r = 2.0 + 2.5 * u2;
                    x += r * DMath.Cos(a); y += r * DMath.Sin(a);
                    fx = -DMath.Sin(a); fy = DMath.Cos(a);
                    pose = Cycle(since + 60.0 * u0, 60.0, 45.0) ? Run : Stand; moving = pose == Run;
                    break;
                }
                case ActivityKind.Train:
                    pose = Spar;
                    Group(w, in p, sid, t, ref x, ref y, ref fx, ref fy, 1.6);
                    break;
                case ActivityKind.Social:
                case ActivityKind.Visit:
                    // at leisure on the green, or visiting: those near each other turn to each other and talk; some sit on the grass
                    Group(w, in p, sid, t, ref x, ref y, ref fx, ref fy, 1.1);
                    pose = u1 < 0.3 ? Sit : Talk;
                    // (session 32, Arjun: the reactions read cheap - crowds idling every way) kept together by a shock felt: on their feet, turned to
                    // where it struck, talking it over with a hand toward it - read from the alarm they hold (its record's place); no state written
                    if (p.Why == WhyCode.Shock && p.Alarm != 0 && w.History.TryGet(p.Alarm, out var shock) && shock.Cell >= 0 && shock.Cell < w.Planet.Cells)
                    {
                        s.SphereToLocal(w.Planet.Grid.Center[shock.Cell], w.Planet.Radius, out double sx, out double sy);
                        if ((sx - x) * (sx - x) + (sy - y) * (sy - y) > 1.0) { fx = sx - x; fy = sy - y; }
                        pose = u1 < 0.6 ? Talk : Stand;
                    }
                    break;
                case ActivityKind.Watch:
                    pose = Stand;
                    fx = x - s.SquareX; fy = y - s.SquareY;   // (watching outward from the store)
                    break;
                case ActivityKind.Sail:
                case ActivityKind.Voyage:
                    pose = Row;
                    break;
                default:
                    pose = Stand;
                    break;
            }
            if (fx == 0 && fy == 0) { double a = u2 * DMath.TwoPi; fx = DMath.Cos(a); fy = DMath.Sin(a); }
            dir = s.LocalToSphere(x, y, w.Planet.Radius);
            face = (s.East * fx + s.North * fy).Normalized;
            return true;
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

        static void Group(World w, in Person p, int sid, double t, ref double x, ref double y, ref double fx, ref double fy, double spacing, bool place = true)
        {
            if (!ReferenceEquals(_gw, w) || _gt != t)
            {
                _gw = w; _gt = t; _groups.Clear(); _rank.Clear();
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var q = ref w.People.Get(id);
                    if (!q.Alive || q.Step != StepKind.Local || t < q.TA) continue;
                    if (q.Act != ActivityKind.Social && q.Act != ActivityKind.Visit && q.Act != ActivityKind.Train && q.Act != ActivityKind.Idle) continue;
                    int qs = q.At != 0 ? q.At : q.Home;
                    long key = GroupKey(qs, (int)q.Act == (int)ActivityKind.Visit ? (int)ActivityKind.Social : (int)q.Act, q.ToX, q.ToY);
                    (double x, double y, int n) g = _groups.TryGetValue(key, out var gg) ? gg : (0.0, 0.0, 0);
                    _rank[id] = g.n;
                    _groups[key] = (g.x + q.ToX, g.y + q.ToY, g.n + 1);
                }
            }
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
