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

        public void Write(BinWriter w) { w.Int(Id); w.Int(Settlement); w.Float(X); w.Float(Y); w.Byte(Kind); w.Int(Household); w.Int(Last); w.Int(Style); w.Long(Built); w.Long(Left); w.Float(Cond); w.Byte(Done); w.Float(Hours); w.Float(Wood); w.Float(Stone); w.Byte(Mat); w.Int(Leader); w.Long(Cause); w.Long(Event); w.Float(Stock); w.Float(Use); }
        public static Building Read(BinReader r) => new Building { Id = r.Int(), Settlement = r.Int(), X = r.Float(), Y = r.Float(), Kind = r.Byte(), Household = r.Int(), Last = r.Int(), Style = r.Int(), Built = r.Long(), Left = r.Long(), Cond = r.Float(), Done = r.Byte(), Hours = r.Float(), Wood = r.Float(), Stone = r.Float(), Mat = r.Byte(), Leader = r.Int(), Cause = r.Long(), Event = r.Long(), Stock = r.Float(), Use = r.Float() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Settlement); h.Add(X); h.Add(Y); h.Add(Kind); h.Add(Household); h.Add(Last); h.Add(Style); h.Add(Built); h.Add(Left); h.Add(Cond); h.Add(Done); h.Add(Hours); h.Add(Wood); h.Add(Stone); h.Add(Mat); h.Add(Leader); h.Add(Cause); h.Add(Event); h.Add(Stock); h.Add(Use); }
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
            if (hh.Building != 0 && w.Buildings.Exists(hh.Building)) { w.Buildings.Get(hh.Building).Kind = hh.Shelter; return; }
            int id = w.Ids.Next(EntityKind.Building);
            w.Buildings.Add(id, new Building { Id = id, Settlement = hh.Settlement, X = hh.HomeX, Y = hh.HomeY, Kind = hh.Shelter, Household = hid, Last = hid, Style = hid, Built = w.Now, Cond = 1f, Done = 1 });
            w.BuildingsOf(hh.Settlement).InsertSorted(id);
            hh.Building = id;
        }

        /// <summary>A household leaves its home (it dies out, it moves away): the building stands empty from now; the household is a camp.</summary>
        public static void Leave(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            if (hh.Building != 0 && w.Buildings.Exists(hh.Building))
            {
                ref var b = ref w.Buildings.Get(hh.Building);
                if (b.Household == hid) { b.Household = 0; b.Left = w.Now; }
            }
            hh.Building = 0; hh.Shelter = Construction.Camp; hh.Build = 0f;
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
        /// The lot a household without a home camps on (D2-084, D2-085): the innermost lot of its village's spiral on dry land that no
        /// home, ruin or camp of the village stands on. Before, every new home took the next lot of an ever-longer spiral: over centuries
        /// a village of a few hundred spread its houses 400 m out (and its palisade round them all) while its old lots lay empty when
        /// their ruins had gone into the land; now new homes take those lots and a village keeps its shape. Lots in the sea or a lake are
        /// passed over (on a smaller planet the coast lies nearer the anchor in metres).
        /// </summary>
        public static void FreeLot(World w, int sid, int forHid, out float x, out float y)
        {
            ref var s = ref w.Settlements.Get(sid);
            var P = w.Planet;
            var bl = w.BuildingsOf(sid); var hl = w.HouseholdsOf(sid);
            int limit = s.Lots + 64;
            for (int k = 0; k < limit; k++)
            {
                s.LotPosition(k, out float lx, out float ly);
                if (Taken(w, bl, hl, forHid, lx, ly)) continue;
                if (!Shore.Dry(P, s.LocalToSphere(lx, ly, P.Radius))) continue;   // (the ground as drawn: D2-088)
                if (k >= s.Lots) s.Lots = k + 1;
                x = lx; y = ly;
                return;
            }
            s.LotPosition(s.Lots++, out x, out y);   // (nothing free within reach: the next lot)
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

        /// <summary>A home, a ruin or another household's camp stands within 6 m of a lot (lots lie ~8-9 m apart).</summary>
        static bool Taken(World w, IntList buildings, IntList households, int forHid, float lx, float ly)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                ref var b = ref w.Buildings.Get(buildings[i]);
                double dx = (double)b.X - lx, dy = (double)b.Y - ly, need = Structures.IsHome(b.Kind) ? 6.0 : Structures.Of(b.Kind).Footprint + 6.0;
                if (dx * dx + dy * dy < need * need) return true;
            }
            for (int i = 0; i < households.Count; i++)
            {
                if (households[i] == forHid) continue;
                ref var h = ref w.Households.Get(households[i]);
                if (h.Building != 0) continue;   // (its home is among the buildings)
                double dx = (double)h.HomeX - lx, dy = (double)h.HomeY - ly;
                if (dx * dx + dy * dy < 36.0) return true;
            }
            return false;
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
