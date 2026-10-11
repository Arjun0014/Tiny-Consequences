using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// A structure on a village's ground (D2-059 homes; C1.1 every kind, `Structures`): raised by people's labour and material, lived in or
    /// worked at, left, fallen in, gone into the land - with what went into it, who began it and why.
    /// </summary>
    public struct Building
    {
        public int Id;
        public int Settlement;
        public float X, Y;           // local metres: its lot
        public byte Kind;            // Structures: Hut, House (homes) or a work (Well, Granary, ...)
        public int Household;        // who lives in it (0 none)
        public int Last;             // the household that last lived in it
        public int Style;            // how it looks (the view's house kinds): the household that raised it
        public long Built;           // when it was first raised (a work: when it was laid out)
        public long Left;            // when it was last left empty or unkept (0 while lived in or kept)
        public float Cond;           // a work's soundness (1 sound; damage lowers it); homes read theirs from Left (C1.1)
        public byte Done;            // 1 standing; 0 being raised
        public float Hours, Wood, Stone;   // labour-hours and 25 kg loads put into raising it
        public byte Mat;             // its form: 0 timber, 1 on stone footings, 2 stone (C1.3)
        public int Leader;           // the person who began its raising (the first to work on it; 0 for homes and the first villages' works)
        public long Cause;           // the event that moved its making (the need)
        public long Event;           // its completion in history (0 while being raised)
        public float Stock;          // what it holds (a work's own goods; the village's grain stays in Settlement.Granary)
        public float Use;            // hours worked at it
        public int Form;             // how it was built: plan, roof, cover, walls, chimney, shade (Architecture, D2-114; 0 before)
        public float Face;           // the bearing it faces, radians in its village's frame (its front's direction: C13.3, D2-118)
        public byte Faced;           // 1 when laid out with its bearing (else read from where it stands: Footprints.FaceOf)

        public void Write(BinWriter w) { w.Int(Id); w.Int(Settlement); w.Float(X); w.Float(Y); w.Byte(Kind); w.Int(Household); w.Int(Last); w.Int(Style); w.Long(Built); w.Long(Left); w.Float(Cond); w.Byte(Done); w.Float(Hours); w.Float(Wood); w.Float(Stone); w.Byte(Mat); w.Int(Leader); w.Long(Cause); w.Long(Event); w.Float(Stock); w.Float(Use); w.Int(Form); w.Float(Face); w.Byte(Faced); }
        public static Building Read(BinReader r) => new Building { Id = r.Int(), Settlement = r.Int(), X = r.Float(), Y = r.Float(), Kind = r.Byte(), Household = r.Int(), Last = r.Int(), Style = r.Int(), Built = r.Long(), Left = r.Long(), Cond = r.Float(), Done = r.Byte(), Hours = r.Float(), Wood = r.Float(), Stone = r.Float(), Mat = r.Byte(), Leader = r.Int(), Cause = r.Long(), Event = r.Long(), Stock = r.Float(), Use = r.Float(), Form = r.Int(), Face = r.Float(), Faced = r.Byte() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Settlement); h.Add(X); h.Add(Y); h.Add(Kind); h.Add(Household); h.Add(Last); h.Add(Style); h.Add(Built); h.Add(Left); h.Add(Cond); h.Add(Done); h.Add(Hours); h.Add(Wood); h.Add(Stone); h.Add(Mat); h.Add(Leader); h.Add(Cause); h.Add(Event); h.Add(Stock); h.Add(Use); h.Add(Form); h.Add(Face); h.Add(Faced); }
    }

    /// <summary>
    /// Homes stand on their own (D2-059). A household's hut or house is a building on its lot that outlives its tenancy: when the
    /// household dies out or moves away the building stands empty and decays - an empty hut falls in after ~20 years, a house
    /// after ~60 (thatch and wattle go first) - and its ruin then fades into the land (a hut's in ~80 years, a house's in ~300:
    /// D2-052). A household without a home - a married couple, families arriving, a camp in the village - first takes the best
    /// standing empty home of its village (mended as they move in); only where none stands does it camp on a new lot and raise its
    /// own. A building lived in is kept sound by its people. Decay is read from the time a building was left, so an abandoned
    /// village's homes fall in with no step to run.
    /// </summary>
    public static class Homes
    {
        public const double HutFallYears = 20, HouseFallYears = 60;
        public const double HutRuinYears = 80, HouseRuinYears = 300;
        /// <summary>Soundness below which nobody moves in (the roof gone: re-roofing a ruin is not yet a work).</summary>
        public const double Habitable = 0.35;

        /// <summary>Years an unkept building stands: by its kind (homes 20 and 60, works by the table: C1.1), twice that on stone footings (C1.3).</summary>
        public static double FallYears(in Building b) => Structures.Of(b.Kind).FallYears * (b.Mat >= 1 ? 2.0 : 1.0);
        /// <summary>Years its ruin lasts (stone footings outlast the timber by half again).</summary>
        public static double RuinYears(in Building b) => Structures.Of(b.Kind).RuinYears * (b.Mat >= 1 ? 1.5 : 1.0);

        /// <summary>How sound a building is at time t: 1 while lived in, down to 0 over its fall years once left empty.</summary>
        public static double Condition(in Building b, long t) => b.Left == 0 ? 1.0 : DMath.Clamp01(1.0 - (t - b.Left) / (FallYears(in b) * SimTime.Year));

        /// <summary>How much of a fallen building's ruin is left at time t (1 as it falls in, 0 gone into the land; 1 while it stands).</summary>
        public static double RuinLeft(in Building b, long t)
        {
            if (b.Left == 0) return 1.0;
            double fallen = (t - b.Left) / (double)SimTime.Year - FallYears(in b);
            return fallen <= 0 ? 1.0 : DMath.Clamp01(1.0 - fallen / RuinYears(in b));
        }

        public static bool Fallen(in Building b, long t) => b.Left != 0 && (t - b.Left) >= FallYears(in b) * SimTime.Year;

        /// <summary>A household raised its shelter a stage (Construction, worldgen): its first building on its lot, or its hut made a house.</summary>
        public static void Raised(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            if (hh.Building != 0 && w.Buildings.Exists(hh.Building))
            {
                // its hut made a house: rebuilt in the form its people would build now (D2-114)
                ref var hb = ref w.Buildings.Get(hh.Building);
                hb.Kind = hh.Shelter;
                int f = Architecture.HomeForm(w, hh.Settlement, hid, hh.Building, hh.Shelter, out double face);
                ref var hb2 = ref w.Buildings.Get(hh.Building);
                hb2.Form = f; hb2.Face = (float)face; hb2.Faced = 1;   // (laid out facing its street or the square: C13.3)
                if (hh.Shelter >= Construction.House && Architecture.PlanOf(f) == Architecture.Row) w.Metrics.TownHouses++;
                return;
            }
            // (C13.3) raised only where its lot is still free - a camp kept since the lot was taken (a party that never left, a lot built over meanwhile)
            // moves to a free one first
            {
                ref var sv = ref w.Settlements.Get(hh.Settlement);
                if (!Footprints.HomeFits(w, hh.Settlement, hh.HomeX, hh.HomeY, Footprints.FaceAt(in sv, hh.HomeX, hh.HomeY, hid), Footprints.LotPlan(in sv, hh.HomeX, hh.HomeY), 0, hid))
                {
                    FreeLot(w, hh.Settlement, hid, out float lx, out float ly);
                    ref var hm = ref w.Households.Get(hid);
                    hm.HomeX = lx; hm.HomeY = ly;
                }
            }
            ref var hh1 = ref w.Households.Get(hid);
            int id = w.Ids.Next(EntityKind.Building);
            w.Buildings.Add(id, new Building { Id = id, Settlement = hh1.Settlement, X = hh1.HomeX, Y = hh1.HomeY, Kind = hh1.Shelter, Household = hid, Last = hid, Style = hid, Built = w.Now, Cond = 1f, Done = 1 });
            w.BuildingsOf(hh1.Settlement).InsertSorted(id);
            hh1.Building = id;
            int form = Architecture.HomeForm(w, hh1.Settlement, hid, id, hh1.Shelter, out double fc);   // (how its builders built it: D2-114)
            ref var nb = ref w.Buildings.Get(id);
            nb.Form = form; nb.Face = (float)fc; nb.Faced = 1;
            if (nb.Kind >= Construction.House && Architecture.PlanOf(form) == Architecture.Row) w.Metrics.TownHouses++;
        }

        /// <summary>
        /// The year's weighing of houses (D2-114; the day households weigh leaving): a household rebuilds its house when what it would build now
        /// stands well above what it lives in (a bigger plan, better walls and roof, a chimney: `Architecture.Gain`) and the house is twenty years
        /// old, or when it is old (`RenewYears`) - if it is of some means, housed, not leaving, its village not short and holding the wood, and
        /// no more than a tenth of the village rebuilding at once. The labour and wood are its own building's (Construction.OwnStage), after the
        /// village's works.
        /// </summary>
        public static void WeighRebuilding(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Ration < 1.0 || s.Wall == 1) return;
            var hl = w.HouseholdsOf(sid);
            int busy = 0;
            for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).Rebuild != 0) busy++;
            int most = System.Math.Max(1, hl.Count / 10);
            for (int i = 0; i < hl.Count && busy < most; i++)
            {
                int hid = hl[i];
                ref var hh = ref w.Households.Get(hid);
                if (hh.Rebuild != 0 || hh.Party != 0 || hh.Shelter < Construction.House || hh.Building == 0 || !w.Buildings.Exists(hh.Building)) continue;
                ref var b = ref w.Buildings.Get(hh.Building);
                if (b.Household != hid) continue;
                if (InOpenOrWet(w, hh.Building) != 0) continue;   // (never renewed where a way, the market place, a ring or a river now runs: C13.3)
                double years = (w.Now - b.Built) / (double)SimTime.Year;
                if (years < 20.0 || Architecture.Means(w, hid) < 1.5) continue;
                int now = Architecture.HomeForm(w, sid, hid, hh.Building, Construction.House, out _, out bool fits);
                if (!fits) continue;   // (no house it would build stands clear on its lot: C13.3)
                double gain = Architecture.Gain(now, b.Form);
                if (!(gain >= 1.0 || years >= RenewYears)) continue;
                Construction.NextShelter(Construction.Hut, out _, out double wood0);
                if (s.Woodstore < wood0 * Architecture.Size(Architecture.PlanOf(now))) continue;
                hh.Rebuild = (byte)(1 + Architecture.PlanOf(now)); hh.Build = 0f;
                busy++;
            }
        }

        /// <summary>Years after which a house is rebuilt whatever its people would build now (timber and thatch do not last for ever).</summary>
        public const double RenewYears = 70.0;

        /// <summary>
        /// A household's house rebuilt (D2-114): in the form its people build now - the plan it set out to build, the walls and roof the village's
        /// ground and knowledge give (stone walls take their stone from the store) -, its years counted again from now; history tells it.
        /// </summary>
        public static void Rebuilt(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            int plan = hh.Rebuild - 1;
            hh.Rebuild = 0; hh.Build = 0f;
            if (hh.Building == 0 || !w.Buildings.Exists(hh.Building)) return;
            int sid = hh.Settlement, bid = hh.Building;
            int before = w.Buildings.Get(bid).Form;
            if (plan == Architecture.Hall && !Architecture.HallRoom(w, sid, bid)) plan = Architecture.Two;   // (another raised its hall meanwhile)
            int form = Architecture.HomeForm(w, sid, hid, bid, Construction.House, out double face, out bool fits);
            if (!fits) return;   // (a neighbour built meanwhile and no house stands clear on its lot now: it keeps the one it has - C13.3)
            // the plan it set out to build, if it still stands clear on its lot (C13.3: a neighbour may have built meanwhile) - else what fits now
            ref var s0 = ref w.Settlements.Get(sid);
            ref var b0 = ref w.Buildings.Get(bid);
            if (plan != Architecture.PlanOf(form) && (Streets.CorePlot(in s0, b0.X, b0.Y) && !Footprints.Narrow(plan) || !Footprints.HomeFits(w, sid, b0.X, b0.Y, face, plan, bid, hid)))
                plan = Architecture.PlanOf(form);
            form = Architecture.Pack(plan, Architecture.RoofOf(form), Architecture.CoverOf(form), Architecture.WallsOf(form), Architecture.Chimney(form) || plan >= Architecture.Two, Architecture.PaletteOf(form));
            ref var s = ref w.Settlements.Get(sid);
            if (Architecture.WallsOf(form) == Architecture.Stone)
            {
                // stone walls are the store's stone, carried and laid
                if (s.Stone >= StoneWallLoads) s.Stone -= StoneWallLoads;
                else form = Architecture.Pack(plan, Architecture.RoofOf(form), Architecture.CoverOf(form), Architecture.Limewash, Architecture.Chimney(form), Architecture.PaletteOf(form));
            }
            ref var b = ref w.Buildings.Get(bid);
            b.Form = form; b.Built = w.Now; b.Face = (float)face; b.Faced = 1;   // (laid out anew on its lot, facing its street or the square: C13.3)
            if (plan == Architecture.Row) w.Metrics.TownHouses++;
            Stone.Footings(w, sid, bid);   // (on stone footings, when the store has them: C1.3)
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.HomeRebuilt, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(hid),
                Object = new EntityRef(EntityKind.Building, bid), ArgA = plan, ArgB = Architecture.PlanOf(before), RootKind = RootKind.People,
            });
            w.Metrics.HomesRebuilt++;
        }

        /// <summary>Loads of stone a house of stone walls takes from the store.</summary>
        public const double StoneWallLoads = 30.0;

        /// <summary>A household leaves its home (it dies out, it moves away): the building stands empty from now; the household is a camp.</summary>
        public static void Leave(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            int bid = hh.Building;
            if (bid != 0 && w.Buildings.Exists(bid))
            {
                ref var b = ref w.Buildings.Get(bid);
                if (b.Household == hid) { b.Household = 0; b.Left = w.Now; }
            }
            hh.Building = 0; hh.Shelter = Construction.Camp; hh.Build = 0f; hh.Rebuild = 0;
            if (bid != 0 && w.Buildings.Exists(bid) && w.Buildings.Get(bid).Household == 0) Clear(w, bid, hid, false);   // (one left in a street, the market place or a ring: taken down - C13.3)
        }

        /// <summary>Why a standing home is in the open (C13.3): 0 it is not, 1 a street or lane runs through it, 2 the green or market place, 3 a ring.</summary>
        public static int InOpen(World w, int bid)
        {
            ref var b = ref w.Buildings.Get(bid);
            if (!w.Settlements.Exists(b.Settlement) || !Structures.IsHome(b.Kind)) return 0;
            ref var s = ref w.Settlements.Get(b.Settlement);
            int plan = Footprints.PlanOf(in b);
            double face = Footprints.FaceOf(in s, in b);
            if (Footprints.Open(in s, b.X, b.Y, face, plan)) return 0;
            for (int r = 0; r < Footprints.Count(plan); r++)
            {
                Footprints.Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                if (Footprints.BoxDisc(b.X, b.Y, face, cx, cz, hx, hz, s.SquareX, s.SquareY, Footprints.Green(in s), 0.0)) return 2;
            }
            double dx = (double)b.X - s.SquareX, dy = (double)b.Y - s.SquareY, d = DMath.Sqrt(dx * dx + dy * dy), reach = Footprints.Reach(plan) + 1.5;
            if (s.OldR > 0f && DMath.Abs(d - (double)s.OldR) < reach || (s.Wall == 1 || s.Wall == 2) && DMath.Abs(d - (double)s.WallR) < reach) return 3;
            return 1;
        }

        /// <summary>Why a standing home is in the open or the water (C13.3): `InOpen`'s reasons, and 4 a river that runs through its ground.</summary>
        public static int InOpenOrWet(World w, int bid)
        {
            int why = InOpen(w, bid);
            if (why != 0) return why;
            ref var b = ref w.Buildings.Get(bid);
            if (!w.Settlements.Exists(b.Settlement) || !Structures.IsHome(b.Kind)) return 0;
            ref var s = ref w.Settlements.Get(b.Settlement);
            return Footprints.Wet(w, in s, b.X, b.Y, Footprints.FaceOf(in s, in b), Footprints.PlanOf(in b)) ? 4 : 0;
        }

        /// <summary>
        /// A home standing empty in the open - a street or lane laid since it was built, the market place widened over it, a ring raised across it - is
        /// taken down (C13.3, D2-118): gone at once (its ground free; the table drops it at the year's end), half its timber back in the village's
        /// woodstore; history tells it. So the ways and the market place open as the households in them leave or die out.
        /// </summary>
        public static bool Clear(World w, int bid, int hid, bool moved)
        {
            int why = InOpenOrWet(w, bid);
            if (why == 0) return false;
            ref var b = ref w.Buildings.Get(bid);
            int sid = b.Settlement;
            b.Household = 0;
            b.Left = w.Now - DMath.ToLong((FallYears(in b) + RuinYears(in b)) * SimTime.Year) - 1;   // (gone: RuinLeft 0)
            ref var s = ref w.Settlements.Get(sid);
            double wood = b.Kind >= Construction.House ? Construction.HouseWood * Architecture.Size(Footprints.PlanOf(in b) == Footprints.HutPlan ? Architecture.Small : Architecture.PlanOf(b.Form)) : Construction.HutWood;
            s.Woodstore += 0.5 * wood;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.HomeCleared, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = hid != 0 ? EntityRef.Household(hid) : default,
                Object = new EntityRef(EntityKind.Building, bid), ArgA = why, ArgB = moved ? 1 : 0, RootKind = RootKind.People,
            });
            w.Metrics.HomesCleared++;
            return true;
        }

        /// <summary>
        /// A household without a home takes the best standing empty home of its village - a house before a hut, the soundest
        /// first, ties by id - and mends it as it moves in. True if it found one; history tells whose it was.
        /// </summary>
        public static bool TakeEmpty(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            var list = w.BuildingsOf(hh.Settlement);
            int best = 0; double bv = -1;
            for (int i = 0; i < list.Count; i++)
            {
                ref var b = ref w.Buildings.Get(list[i]);
                if (b.Household != 0 || !Structures.IsHome(b.Kind)) continue;   // (a home: not the village's well or granary, C1.1)
                if (InOpenOrWet(w, list[i]) != 0) continue;   // (one standing in the market place, a street, a ring or a river is left to come down: C13.3)
                double c = Condition(in b, w.Now);
                if (c < Habitable) continue;
                double v = b.Kind + c;
                if (v > bv) { bv = v; best = list[i]; }
            }
            if (best == 0) return false;
            ref var t = ref w.Buildings.Get(best);
            int last = t.Last;
            t.Household = hid; t.Last = hid; t.Left = 0;
            hh.Building = best; hh.Shelter = t.Kind; hh.Build = 0f; hh.HomeX = t.X; hh.HomeY = t.Y;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.HomeTaken, Time = w.Now, Cell = w.Settlements.Get(hh.Settlement).Cell, Settlement = hh.Settlement,
                Subject = EntityRef.Household(hid), Object = last != 0 && last != hid ? EntityRef.Household(last) : default, ArgA = t.Kind,
                RootKind = RootKind.People,
            });
            w.Metrics.HomesTaken++;
            return true;
        }

        /// <summary>
        /// The lot a household without a home camps on (D2-084, D2-085): the innermost lot of its village's spiral on dry land where the ground a lot
        /// keeps is clear. Before, every new home took the next lot of an ever-longer spiral: over centuries a village of a few hundred spread its
        /// houses 400 m out (and its palisade round them all) while its old lots lay empty when their ruins had gone into the land; now new homes
        /// take those lots and a village keeps its shape. Lots in the sea or a lake are passed over (on a smaller planet the coast lies nearer the
        /// anchor in metres). Where the village has streets (C13.1, D2-116), the nearest free frontage lot along them is taken when it lies within
        /// `Streets.Lean` of the spiral's free lot's distance - frontage is sought, so a village with roads grows along them; in a town's core the
        /// street frontage is divided into plots half as wide (C13.3, D2-118). A lot is free where the ground it keeps meets no building, ruin, camp,
        /// street, lane, ring, green or market place (`Footprints.HomeFits`: D2-116's 6 m between lots let houses stand in each other).
        /// </summary>
        public static void FreeLot(World w, int sid, int forHid, out float x, out float y)
        {
            Streets.Refresh(w, sid);   // (its ways worn to paths laid as streets)
            Streets.OpenLanes(w, sid);   // (and lanes opened where nothing stands in them now: C13.3)
            Streets.Grow(ref w.Settlements.Get(sid));   // (a town's divided core reaches as far as it has grown: C13.3)
            ref var s = ref w.Settlements.Get(sid);
            var P = w.Planet;
            int limit = s.Lots + 64;
            int spiral = -1; float sx = 0f, sy = 0f; double sr = double.MaxValue;
            for (int k = 0; k < limit; k++)
            {
                s.LotPosition(k, out float lx, out float ly);
                if (!Free(w, sid, forHid, lx, ly)) continue;
                spiral = k; sx = lx; sy = ly; sr = 15.0 + 9.5 * DMath.Sqrt(k + 1.0);
                break;
            }
            // the frontage along its streets (C13.1) and their back lanes (C13.2): the nearest free lot of each line, weighed against the spiral's -
            // a street's frontage in a town's core in plots half as wide (C13.3); where none of the spiral's lots is free (the sea over a village's
            // every lot), no further out than the spiral looked - before, the walk along a street whose every lot was drowned never ended (D2-147)
            ref var s1 = ref w.Settlements.Get(sid);
            bool front = false; double fb = spiral >= 0 ? sr : 15.0 + 9.5 * DMath.Sqrt(limit + 1.0); float fx = 0f, fy = 0f;
            int lines = Streets.Lines(in s1);
            for (int i = 0; i < lines; i++)
            {
                bool lane = Streets.IsLane(i);
                if (Streets.Shut(in s1, i)) continue;   // (a lane not yet open: C13.3)
                double lean = lane ? Streets.LaneLean : Streets.Lean;
                double first = lane ? Streets.LaneStart : Streets.Start;
                double core = lane ? 0.0 : (double)s1.CoreR;
                for (double j = 0; lean * (first + j * Streets.Spacing) < fb; j = first + (j + 0.5) * Streets.Spacing <= core + 1e-9 ? j + 0.5 : DMath.Floor(j) + 1.0)
                {
                    bool got = false;
                    for (int side = -1; side <= 1 && !got; side += 2)
                    {
                        Streets.Lot(in s1, i, j, side, out double qx, out double qy);
                        double score = Streets.Weigh(in s1, i, j, qx, qy);
                        if (score >= fb) continue;
                        float lx = (float)qx, ly = (float)qy;
                        if (!Free(w, sid, forHid, lx, ly)) continue;
                        fb = score; fx = lx; fy = ly; front = true; got = true;
                    }
                    if (got) break;
                }
            }
            ref var s2 = ref w.Settlements.Get(sid);
            if (front) { x = fx; y = fy; return; }
            if (spiral >= 0)
            {
                if (spiral >= s2.Lots) s2.Lots = spiral + 1;
                x = sx; y = sy;
                return;
            }
            s2.LotPosition(s2.Lots++, out x, out y);   // (nothing free within reach: the next lot)
        }

        /// <summary>Whether a lot is free for a household to camp on (C13.3): on dry land, its ground clear (`Footprints.HomeFits` of the ground a lot
        /// keeps, as it would face).</summary>
        static bool Free(World w, int sid, int forHid, float lx, float ly)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (!Shore.Dry(w.Planet, s.LocalToSphere(lx, ly, w.Planet.Radius))) return false;   // (the ground as drawn: D2-088)
            int plan = Footprints.LotPlan(in s, lx, ly);
            return Footprints.HomeFits(w, sid, lx, ly, Footprints.FaceAt(in s, lx, ly, forHid), plan, 0, forHid);
        }

        /// <summary>
        /// The sea rose over homes' lots (D2-087, a coast's change): each such home is lost - left at once as a drowned ruin that fades -
        /// and its household takes an empty home of the village or the innermost free lot; camps on drowned lots move too. One history
        /// line a village, caused by the coast's change (0 when the plates or the waves moved it). Before, households lived on in houses
        /// standing in the sea.
        /// </summary>
        public static void SeaTook(World w, long cause)
        {
            var P = w.Planet;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                int lost = 0;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (Fallen(in b, w.Now)) continue;   // (a ruin already)
                    if (Shore.Dry(P, s.LocalToSphere(b.X, b.Y, P.Radius))) continue;
                    if (Boats.IsBoat(b.Kind)) { Boats.Landing(w, sid, out float lx, out float ly, bl[i]); ref var bb = ref w.Buildings.Get(bl[i]); bb.X = lx; bb.Y = ly; continue; }   // (a boat floats: drawn up the new shore, C9.1)
                    int hid = b.Household;
                    b.Household = 0; b.Left = w.Now - DMath.ToLong(FallYears(in b) * SimTime.Year) - 1;   // (drowned: fallen at once)
                    if (!Structures.IsHome(b.Kind)) { Structures.Lost(w, bl[i]); continue; }   // (a work the sea took: its use is gone, C1.1)
                    lost++;
                    if (hid == 0 || !w.Households.Exists(hid)) continue;
                    ref var hh = ref w.Households.Get(hid);
                    hh.Building = 0; hh.Shelter = Construction.Camp; hh.Build = 0f;
                    if (!TakeEmpty(w, hid)) { FreeLot(w, sid, hid, out float x, out float y); ref var h2 = ref w.Households.Get(hid); h2.HomeX = x; h2.HomeY = y; }
                }
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    ref var hh = ref w.Households.Get(hl[i]);
                    if (hh.Building != 0) continue;
                    if (Shore.Dry(P, s.LocalToSphere(hh.HomeX, hh.HomeY, P.Radius))) continue;
                    FreeLot(w, sid, hl[i], out float x, out float y);
                    ref var h2 = ref w.Households.Get(hl[i]); h2.HomeX = x; h2.HomeY = y;
                }
                if (lost > 0)
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.HomesDrowned, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                        ArgA = lost, Cause1 = cause, Rel1 = CauseRel.CausedBy, RootKind = cause == 0 ? RootKind.Nature : RootKind.Unknown,
                    });
            }
        }

        static readonly System.Collections.Generic.List<int> _gone = new System.Collections.Generic.List<int>();

        /// <summary>Ruins gone into the land leave the table (yearly housekeeping).</summary>
        public static void Prune(World w)
        {
            _gone.Clear();
            for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id)) if (RuinLeft(in w.Buildings.Get(id), w.Now) <= 0) _gone.Add(id);
            for (int i = 0; i < _gone.Count; i++)
            {
                int id = _gone[i];
                w.BuildingsOf(w.Buildings.Get(id).Settlement).Remove(id);
                w.Buildings.Remove(id);
            }
        }
    }
}
