using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Building by labour and wood (D2-045): nothing stands because a flag was set. A household lives in a camp until its people
    /// raise a hut, and in a hut until they raise a house; a village has a well and a granary only once its people have dug and
    /// built them. Every hour of work is a person's (an activity step in ACTIVITY, a share of the day in COARSE), and the wood
    /// comes out of the village's store as the work goes on - no wood, no progress.
    /// </summary>
    public static class Construction
    {
        public const byte Camp = 0, Hut = 1, House = 2;
        public const byte Well = 1, Granary = 2, Palisade = 4;   // a village's works (bits)
        public const byte Mend = 8;                              // (a work, not a bit: mending the palisade; D2-074)
        public const byte Structure = 16;                        // (a work, not a bit: raising the village's planned structure, C1.1)
        /// <summary>A palisade a metre of its ring: felling, carrying, digging and setting the posts; the timber (D2-074).</summary>
        public const double PalisadeHoursPerM = 2.5, PalisadeWoodPerM = 0.8;

        /// <summary>Where a village's store stands (its granary, or the heap before one is built): beside the square.</summary>
        public static void GranarySpot(in Settlement s, out double x, out double y) { x = s.SquareX + 14.0; y = s.SquareY - 10.0; }

        // labour-hours and wood: a couple raises a hut in a few days and a house over some weeks; a village digs its well and
        // builds its granary together
        public const double HutHours = 30, HutWood = 8;       // a round hut of posts, wattle and thatch
        public const double HouseHours = 240, HouseWood = 35; // a timber house
        public const double WellHours = 60, WellWood = 2;     // a dug well, a little timber for its curb
        public const double GranaryHours = 120, GranaryWood = 25;

        /// <summary>Labour and wood the next stage of a household's shelter asks (0 when it has a house).</summary>
        public static void NextShelter(byte shelter, out double hours, out double wood)
        {
            if (shelter == Camp) { hours = HutHours; wood = HutWood; }
            else if (shelter == Hut) { hours = HouseHours; wood = HouseWood; }
            else { hours = 0; wood = 0; }
        }

        /// <summary>
        /// The village work to do next: the structure its people are raising (planned by need: a well first, then a granary once there is
        /// a harvest to keep, another when the store overfills - `Structures.Plan`, C1.1), then its palisade and its mending, or 0.
        /// </summary>
        public static byte NextWork(in Settlement s)
        {
            // (a palisade being raised goes before every work but water and the first store: C1.3)
            if (s.Project != 0 && (s.Wall != 1 || s.ProjectKind == Structures.Well || (s.ProjectKind == Structures.Granary && s.Granaries == 0))) return Structure;
            if (s.Wall == 1) return Palisade;
            if (s.Project != 0) return Structure;
            if (s.Wall == 2 && s.WallKeep != 0 && s.WallCond < 0.6f) return Mend;
            return 0;
        }

        /// <summary>Labour and wood a village work asks in all (a planned structure: its kind's, C1.1).</summary>
        public static void WorkCost(World w, byte work, in Settlement s, out double hours, out double wood)
        {
            if (work == Structure && s.Project != 0 && w.Buildings.Exists(s.Project))
            {
                var k = Structures.Of(w.Buildings.Get(s.Project).Kind);
                hours = k.Hours; wood = k.Wood;
                return;
            }
            WorkCost(work, in s, out hours, out wood);
        }

        /// <summary>Labour and wood of the ring works (the palisade and its mending, D2-074).</summary>
        public static void WorkCost(byte work, in Settlement s, out double hours, out double wood)
        {
            if (work == Palisade || work == Mend)
            {
                double perim = 2.0 * DMath.PI * DMath.Max(1.0, (double)s.WallR) * (s.WallDry > 0f ? (double)s.WallDry : 1.0);   // (the dry stretches only: C1.6)
                double share = work == Mend ? 0.6 * DMath.Max(0.05, 1.0 - (double)s.WallCond) : 1.0;
                hours = PalisadeHoursPerM * perim * share; wood = PalisadeWoodPerM * perim * share;
            }
            else { hours = 0; wood = 0; }
        }

        /// <summary>Where the village's work goes on: the structure being raised, or a stretch of its palisade's ring (u picks the stretch).</summary>
        public static void WorkSpot(World w, in Settlement s, byte work, double u, out double x, out double y)
        {
            if (work == Structure && s.Project != 0 && w.Buildings.Exists(s.Project)) { ref var b = ref w.Buildings.Get(s.Project); x = b.X; y = b.Y; return; }
            if (work == Palisade || work == Mend)
            {
                double a = 2.0 * DMath.PI * u;
                x = s.SquareX + (double)s.WallR * DMath.Cos(a); y = s.SquareY + (double)s.WallR * DMath.Sin(a);
                return;
            }
            GranarySpot(in s, out x, out y);
        }

        /// <summary>
        /// What a person of a household builds next (D2-045): a roof over a camp first, then the village's well (water before
        /// comfort), then a hut made a house, then the village's granary. True for the household's own shelter.
        /// </summary>
        public static bool OwnFirst(World w, in Settlement s, int hid)
        {
            if (hid == 0 || !w.Households.Exists(hid)) return false;
            ref var hh = ref w.Households.Get(hid);
            if (hh.Settlement != s.Id || hh.Shelter >= House) return false;
            if (hh.Shelter == Camp) return true;
            return (s.Works & Well) != 0;   // a hut waits for the well
        }

        /// <summary>
        /// Hours of building work done (a person's share), on what comes first (OwnFirst): the household's shelter or the
        /// village's next work. Wood comes out of the store in step with the labour; a stage completes when its hours are in
        /// (history). Returns the hours that found work (none when there is nothing to build or no wood).
        /// </summary>
        public static double Work(World w, int sid, int hid, double hours, double rate, int worker = 0)
        {
            if (hours <= 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            if (OwnFirst(w, in s, hid) || NextWork(in s) == 0 && hid != 0 && w.Households.Exists(hid) && w.Households.Get(hid).Shelter < House && w.Households.Get(hid).Settlement == sid)
            {
                ref var hh = ref w.Households.Get(hid);
                {
                    NextShelter(hh.Shelter, out double need, out double wood);
                    double done = Spend(ref s, hours * rate, need - hh.Build, wood / need);
                    hh.Build = (float)(hh.Build + done);
                    if (hh.Build >= need - 1e-6)
                    {
                        hh.Shelter++; hh.Build = 0f;
                        Homes.Raised(w, hid);   // a building on its lot, outliving its tenancy (D2-059)
                        if (hh.Shelter == House) Stone.Footings(w, sid, w.Households.Get(hid).Building);   // on stone footings, when the store has them (C1.3)
                        w.Record(new HistoryEvent
                        {
                            Kind = HistoryKind.HomeBuilt, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(hid),
                            ArgA = hh.Shelter, RootKind = RootKind.People,
                        });
                    }
                    return rate > 0 ? done / rate : 0;
                }
            }
            byte work = NextWork(in s);
            if (work == 0) return 0;
            if (work == Structure)
            {
                // the village's planned structure (C1.1): its hours and wood go into it; it stands when they are in
                int bid = s.Project;
                if (!w.Buildings.Exists(bid)) { s.Project = 0; return 0; }
                ref var b = ref w.Buildings.Get(bid);
                var k = Structures.Of(b.Kind);
                double doneS = Spend(ref s, hours * rate, k.Hours - b.Hours, k.Wood / k.Hours);
                Structures.Raised(w, sid, bid, doneS, doneS * k.Wood / k.Hours, worker);
                return rate > 0 ? doneS / rate : 0;
            }
            WorkCost(work, in s, out double needW, out double woodW);
            double doneW = Spend(ref s, hours * rate, needW - s.WorkBuild, woodW / needW);
            s.WorkBuild = (float)(s.WorkBuild + doneW);
            if (s.WorkBuild >= needW - 1e-6)
            {
                s.WorkBuild = 0f;
                if (work == Mend) { s.WallCond = 1f; return rate > 0 ? doneW / rate : 0; }   // (the rotten posts replaced)
                s.Works |= work;
                if (work == Palisade)
                {
                    // the village walled (D2-074): a fortified place, its cause the losses that moved it
                    s.Wall = 2; s.WallCond = 1f; s.WallKeep = 1;
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.WallBuilt, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                        ArgA = (int)DMath.Floor(2.0 * DMath.PI * (double)s.WallR + 0.5), RootKind = RootKind.People, Cause1 = s.WallEvent, Rel1 = CauseRel.CausedBy,
                    });
                    return rate > 0 ? doneW / rate : 0;
                }
            }
            return rate > 0 ? doneW / rate : 0;
        }

        /// <summary>Work-hours spent, as far as the wood allows (wood per work-hour), never past what is left to do.</summary>
        static double Spend(ref Settlement s, double effort, double left, double woodPerHour)
        {
            double doable = effort < left ? effort : left;
            if (doable <= 0) return 0;
            if (woodPerHour > 0)
            {
                double canWood = s.Woodstore / woodPerHour;
                if (doable > canWood) doable = canWood;
                s.Woodstore -= doable * woodPerHour;
                if (s.Woodstore < 0) s.Woodstore = 0;
            }
            return doable;
        }

        /// <summary>Whether a household or its village has building to do now (and the wood to do it with).</summary>
        public static bool HasWork(World w, in Settlement s, int hid, out bool ownRoof, out bool woodShort)
        {
            ownRoof = false; woodShort = false;
            if (OwnFirst(w, in s, hid) || NextWork(in s) == 0 && hid != 0 && w.Households.Exists(hid) && w.Households.Get(hid).Shelter < House && w.Households.Get(hid).Settlement == s.Id)
            {
                ref var hh = ref w.Households.Get(hid);
                {
                    ownRoof = true;
                    NextShelter(hh.Shelter, out double need, out double wood);
                    woodShort = s.Woodstore < wood / need;
                    return !woodShort;
                }
            }
            byte work = NextWork(in s);
            if (work == 0) return false;
            WorkCost(w, work, in s, out double needW, out double woodW);
            woodShort = s.Woodstore < woodW / needW;
            return !woodShort;
        }
    }
}
