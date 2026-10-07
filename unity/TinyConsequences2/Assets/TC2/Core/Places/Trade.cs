using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Trade by want (D2-067; 08 §2, F5): what one village has to spare and another lacks is carried between them by people. Each
    /// village values each good by its own want - grain by how long its store will last against the harvest, metal by the share of
    /// its people its tools leave unequipped, fibre and hides by its store a head - and a trader takes out what their village
    /// values less than the other does and brings back what the other values less than theirs does, at the middle of the two
    /// villages' values: both gain or nobody goes. A village with grain to spare sends some to a neighbour in famine as a gift
    /// (relief, history). Traders are visitors: they catch what their hosts have and carry it home. A COARSE day runs the same
    /// deals as trips at the lived rate.
    /// </summary>
    public sealed class Trade
    {
        World _w;
        public void Bind(World w) { _w = w; }

        public const byte None = 0, Grain = 1, Metal = 2, Fibre = 3, Hides = 4, Iron = 5;   // (Metal is copper; iron since C5.3b)
        public const int Goods = 6;
        /// <summary>A trader's load (kg) and each good's weight a unit (a FU of grain, a kg of metal or fibre, a hide).</summary>
        public const double LoadKg = 25.0;
        static double Kg(byte g) => g == Grain ? 0.75 : g == Hides ? 3.0 : 1.0;
        /// <summary>Trading trips a healthy grown person makes a day when a deal is to be had (COARSE: fitted to lived days).</summary>
        public const double TripsPerAdultDay = 0.01;
        /// <summary>The least gain (in the villages' own values) worth a day's walk.</summary>
        public const double WorthAWalk = 4.0;
        /// <summary>Where a load goes at home: the village's store, or the household's larder (what was taken: D2-069).</summary>
        public const byte ToStore = 0, ToLarder = 1;
        /// <summary>The known harm (FU, by its weight now) past which a village neither trades with nor feeds another (D2-069).</summary>
        public const double Grudge = 10.0;

        /// <summary>Whether either village holds a grudge against the other's people (known harms: D2-069).</summary>
        public bool Estranged(int a, int b) => _w.DeedBook.Grievance(a, b) > Grudge || _w.DeedBook.Grievance(b, a) > Grudge;

        // ------------------------------------------------------------------ wants and spares (pure, from state)
        public static double DaysOfFood(World w, in Settlement s, out double daily)
        {
            daily = DMath.Max(1.0, s.Population * w.Config.FoodPerAdultDay * 0.8);
            return s.StoreSeen(w.Now) / daily;   // (the store as days lived hold it: F8)
        }

        /// <summary>What a unit of a good is worth to a village now (its want: scarcer, dearer).</summary>
        public double Value(int sid, byte g)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            int pop = s.Population > 1 ? s.Population : 1;
            switch (g)
            {
                case Grain: { double d = DaysOfFood(w, in s, out _); return 1.0 + 3.0 * DMath.Clamp01(1.0 - d / 60.0); }
                case Metal: return 6.0 * (1.0 - Knowledge.Tech.ToolShare(in s)) + 0.5;   // (copper, worth less where iron serves: C5.2b)
                case Fibre: return 0.5 + 2.0 * DMath.Clamp01(1.0 - s.Fibre / (2.0 * pop));
                case Hides: return 1.0 + 3.0 * DMath.Clamp01(1.0 - s.Hides / (1.0 * pop));
                case Iron: return 8.0 * (1.0 - Knowledge.Tech.IronShare(in s)) + 0.5;   // (iron tools, by the share of its people they leave unequipped: C5.3b)
            }
            return 0;
        }

        /// <summary>How much of a good a village can spare (keeping two months of grain, full toolkits, a store a head of the rest).</summary>
        public double Spare(int sid, byte g)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            switch (g)
            {
                case Grain: { DaysOfFood(w, in s, out double daily); return DMath.Max(0.0, s.StoreSeen(w.Now) - 60.0 * daily); }
                case Metal: return DMath.Max(0.0, s.Metal - Knowledge.Tech.MetalPerHead * s.Population);
                case Fibre: return DMath.Max(0.0, s.Fibre - 2.0 * s.Population);
                case Hides: return DMath.Max(0.0, s.Hides - 1.0 * s.Population);
                case Iron: return DMath.Max(0.0, s.Iron - Knowledge.Tech.IronPerHead * s.Population);
            }
            return 0;
        }

        static double Stock(ref Settlement s, byte g) => g == Grain ? s.Granary : g == Metal ? s.Metal : g == Fibre ? s.Fibre : g == Iron ? s.Iron : s.Hides;
        static void Add(ref Settlement s, byte g, double q)
        {
            if (g == Grain) s.Granary += q; else if (g == Metal) s.Metal += q; else if (g == Fibre) s.Fibre += q; else if (g == Hides) s.Hides += q;
            else if (g == Iron) { s.Iron += q; if (q < 0) s.IronSold = (float)((double)s.IronSold - q); }   // (iron parted with: its smiths make that much more for the next buyers, C5.3b)
        }

        /// <summary>
        /// The best deal a village's trader could make with a neighbour now: the good to take out (and how much a load holds, as
        /// far as it is spared) and the gain to both, valued at the middle of the two villages' values. 0 when none is worth a walk.
        /// </summary>
        public double Plan(int from, int to, out byte give, out double qty, out byte take, double loadKg = LoadKg)
        {
            give = None; take = None; qty = 0;
            double best = 0;
            if (Estranged(from, to)) return 0.0;   // (nobody deals with those who wronged them)
            for (byte g = 1; g < Goods; g++)
            {
                double spare = Spare(from, g);
                if (spare <= 0) continue;
                double vf = Value(from, g), vt = Value(to, g);
                if (vt <= vf) continue;   // they would not give more for it than it is worth at home
                double q = DMath.Min(spare, loadKg / Kg(g));
                // what comes back: a good the other spares and this village wants more than they do
                for (byte h = 1; h < Goods; h++)
                {
                    if (h == g) continue;
                    double spareH = Spare(to, h);
                    double vfh = Value(from, h), vth = Value(to, h);
                    if (spareH <= 0 || vfh <= vth) continue;
                    double price = (vf + vt) / (vfh + vth);   // units of h a unit of g fetches, at the middle of both values
                    double qh = DMath.Min(DMath.Min(q * price, spareH), loadKg / Kg(h));
                    double qg = qh / price;
                    double gain = (vt - vf) * qg + (vfh - vth) * qh;
                    if (gain > best) { best = gain; give = g; qty = qg; take = h; }
                }
            }
            return best >= WorthAWalk ? best : 0.0;
        }

        /// <summary>The neighbour within a day's walk (6 km) with the best deal for a village now, or 0.</summary>
        public int BestPartner(int sid, out double gain)
        {
            var w = _w;
            gain = 0; int bestTo = 0;
            ref var s = ref w.Settlements.Get(sid);
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited) continue;
                // (a market draws traders from farther - a long day's walk - and they deal better there: many buyers and sellers in one
                // place; C1.6, a central place made by its traders)
                bool market = t.Market != 0 && w.Buildings.Exists(t.Market) && Structures.Working(in w.Buildings.Get(t.Market), w.Now);
                if (Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > (market ? MarketReach : 6000)) continue;
                double g = Plan(sid, o, out _, out _, out _) * (market ? MarketGain : 1.0);
                if (g > gain) { gain = g; bestTo = o; }
            }
            return bestTo;
        }

        /// <summary>m: how far traders walk to a market (a day's walk is 6 km).</summary>
        public const double MarketReach = 9000.0;
        /// <summary>How much better a deal at a market is weighed (more buyers and sellers to match a load with).</summary>
        public const double MarketGain = 1.2;
        /// <summary>Traders arriving a year (the halving count) from which a village lays out a market.</summary>
        public const double MarketFrom = 10.0;

        // ------------------------------------------------------------------ a trader's trip (ACTIVITY) - the same steps a COARSE trip takes at once
        /// <summary>At departure: the load out of the village's store onto the trader's back.</summary>
        public void Load(ref Person p, int from, int to, double loadKg = LoadKg)
        {
            var w = _w;
            if (p.CargoQty > 0f && w.Settlements.Exists(p.Home)) Unload(ref p, p.Home);   // (whatever they still carry is home first: goods are never lost)
            if (Plan(from, to, out byte give, out double qty, out _, loadKg) <= 0 || give == None) return;
            ref var s = ref w.Settlements.Get(from);
            qty = DMath.Min(qty, Stock(ref s, give));
            if (qty <= 0) return;
            Add(ref s, give, -qty);
            p.Cargo = give; p.CargoQty = (float)qty; p.CargoTo = ToStore;
        }

        /// <summary>At the other village: the load into its store, a return load out of it (as the deal stands now), both stores gaining.</summary>
        public void Exchange(ref Person p, int at, double loadKg = LoadKg)
        {
            var w = _w;
            if (p.Cargo == None || p.CargoQty <= 0) return;
            int home = p.Home;
            byte g = p.Cargo; double qg = p.CargoQty;
            ref var t = ref w.Settlements.Get(at);
            // what they give back for it, valued at the middle (from the trader's village's side: what it wants that this one spares)
            byte take = None; double qh = 0, bestGain = 0;
            double vf = Value(home, g), vt = Value(at, g);
            for (byte h = 1; h < Goods; h++)
            {
                if (h == g) continue;
                double spareH = Spare(at, h), vfh = Value(home, h), vth = Value(at, h);
                if (spareH <= 0 || vfh <= vth) continue;
                double price = (vf + vt) / (vfh + vth);
                double q = DMath.Min(DMath.Min(qg * price, spareH), loadKg / Kg(h));
                double gain = (vfh - vth) * q;
                if (gain > bestGain) { bestGain = gain; take = h; qh = q; }
            }
            if (take == None) return;   // nothing they want for it now: the load goes home again
            Add(ref t, g, qg);
            Add(ref t, take, -qh);
            p.Cargo = take; p.CargoQty = (float)qh;
            w.Metrics.TradeTrips++;
            t.TradeIn = (float)((double)t.TradeIn + 1.0);   // (one more trader came to deal here: C1.6)
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Trade, home, at);
            w.Metrics.TradeValue += (vt - vf) * qg + bestGain;
            if (g == Metal) w.Metrics.MetalTraded += qg;
            if (take == Metal) w.Metrics.MetalTraded += qh;
            if (g == Iron) w.Metrics.IronTraded += qg;
            if (take == Iron) w.Metrics.IronTraded += qh;
        }

        /// <summary>Home again: the load into the village's store.</summary>
        public void Unload(ref Person p, int home)
        {
            var w = _w;
            if (p.Cargo == None || p.CargoQty <= 0) { p.Cargo = None; p.CargoQty = 0f; p.CargoTo = ToStore; return; }
            if (p.CargoTo == ToLarder && p.Cargo == Grain && w.Households.Exists(p.Household))
            {
                ref var hh = ref w.Households.Get(p.Household);
                hh.Larder += p.CargoQty;
            }
            else
            {
                ref var s = ref w.Settlements.Get(home);
                Add(ref s, p.Cargo, p.CargoQty);
            }
            p.Cargo = None; p.CargoQty = 0f; p.CargoTo = ToStore;
        }

        // ------------------------------------------------------------------ relief and the COARSE day
        /// <summary>
        /// A village's day of trade and relief at COARSE resolution (and relief at both): its traders' trips at the lived rate, each
        /// the whole deal at once (the trader a visitor who may catch what the other village has); grain to a neighbour in famine.
        /// </summary>
        public void Day(int sid, int step = 1)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            Relief(sid, step);
            Voyages(sid, step);   // (by sea, at both resolutions - abstract, as relief is: C9.3)
            if (w.Resolution != Resolution.Coarse) return;
            int to = BestPartner(sid, out double gain);
            if (to == 0) return;
            int adults = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); double age = p.AgeYears(now); if (p.Alive && p.Sick == 0 && age >= 18 && age < 56 && p.Health > 0.7) adults++; }
            int trips = Poisson(adults * TripsPerAdultDay * step, KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, 501));   // (DEEP: the step's trips)
            for (int k = 0; k < trips && res.Count > 0; k++)
            {
                if (Plan(sid, to, out _, out _, out _) <= 0) break;
                // the trader: a healthy grown person of the village, by a keyed pick
                int pick = res[(int)KeyedRng.Below(KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, (ulong)(510 + k)), res.Count)];
                ref var p = ref w.People.Get(pick);
                double age = p.AgeYears(now);
                if (!p.Alive || p.Sick != 0 || age < 18 || age >= 56 || p.Health <= 0.7) continue;
                Load(ref p, sid, to);
                Exchange(ref p, to);
                w.Ways.Walk(w, sid, to, 2.0);   // (the trader walked there and back: C4.1)
                w.Disease.Arrive(ref p, to, SimTime.Hours(VisitHoursCoarse));   // (what they may catch there and carry home)
                w.DeedBook.Meet(sid, to);                                        // (and the news: D2-072)
                Unload(ref p, sid);
            }
        }

        // ------------------------------------------------------------------ by sea (C9.3)
        /// <summary>kg a plank boat carries besides its crew (24 §5: a plank fishing boat, ~500 kg - twenty walkers' loads).</summary>
        public const double BoatLoadKg = 500.0;
        /// <summary>The least gain (in the villages' own values) worth a boat's crew's day at sea (three walks).</summary>
        public const double WorthAVoyage = 12.0;
        /// <summary>Voyages a village's plank boat makes a day while a deal by sea is to be had (COARSE: about one a month).</summary>
        public const double VoyagesPerBoatDay = 0.04;

        /// <summary>
        /// The coastal village with the best deal for a boat's load from this one now, reached over the sea from landing to landing (its way:
        /// `SeaRoutes`), or 0 - a market there deals better, as on land; none worth a crew's day at sea.
        /// </summary>
        public int BestSeaPartner(int sid, out double gain, out Agents.Route way)
        {
            var w = _w;
            gain = 0; way = null; int best = 0;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited || s.LandAt == 0) return 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || t.LandAt == 0) continue;
                if (Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > SeaRoutes.Most) continue;
                bool market = t.Market != 0 && w.Buildings.Exists(t.Market) && Structures.Working(in w.Buildings.Get(t.Market), w.Now);
                double g = Plan(sid, o, out _, out _, out _, BoatLoadKg) * (market ? MarketGain : 1.0);
                if (g <= gain) continue;
                var r = w.SeaRoutes.Get(w, sid, o);
                if (r == null) continue;
                gain = g; best = o; way = r;
            }
            return gain >= WorthAVoyage ? best : 0;
        }

        /// <summary>
        /// A village's day of voyages (COARSE, DEEP: a step's): its plank boats lying at the landing put out to the coastal village with the
        /// best deal for a boat's load, at the lived rate - each with a crew of its household, the whole voyage at once: the sea's chance on
        /// the way (the wind over its middle; nobody puts out in a wind the boat would not fish in), the load out and the return load home,
        /// the crew visitors there (what they may catch, the news). A boat overturned is lost with its load.
        /// </summary>
        void Voyages(int sid, int step)
        {
            var w = _w;
            long now = w.Now;
            int to = BestSeaPartner(sid, out _, out var way);
            if (to == 0) return;
            var bl = w.BuildingsOf(sid);
            int boats = 0;
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); if (Boats.IsPlank(b.Kind) && Boats.Sound(in b, now) && b.Household != 0) boats++; }
            if (boats == 0) return;
            int n = System.Math.Min(boats, Poisson(boats * VoyagesPerBoatDay * step, KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, 601)));
            int k = 0;
            for (int i = 0; i < bl.Count && k < n; i++)
            {
                int bid = bl[i];
                ref var b = ref w.Buildings.Get(bid);
                if (!Boats.IsPlank(b.Kind) || !Boats.Sound(in b, now) || b.Household == 0) continue;
                k++;
                int hid = b.Household;
                byte kind = b.Kind;
                // its crew: the household's healthy grown people, a seat each
                var crew = _crew; crew.Clear();
                var mem = w.Members(hid);
                for (int j = 0; j < mem.Count && crew.Count < Boats.SeatsOf(kind); j++)
                {
                    ref var q = ref w.People.Get(mem[j]);
                    double age = q.AgeYears(now);
                    if (q.Alive && q.Sick == 0 && age >= 16 && age < 56 && q.Health > 0.7 && q.At == sid) crew.Add(mem[j]);
                }
                if (crew.Count < 2) continue;   // (a boat's load wants two at the oars at least)
                if (Plan(sid, to, out _, out _, out _, BoatLoadKg) < WorthAVoyage) break;   // (the deal as it stands after the voyages before)
                double wind = Boats.WindAt(w, way.PointAt(0.5 * way.Length));
                if (wind >= Boats.MaxOf(kind)) continue;
                int outcome = Boats.Trip(w, sid, hid, bid, crew[0], wind, out long lost, 1, true);
                if (outcome != 0)
                {
                    for (int j = 1; j < crew.Count; j++) Boats.Overturned(w, sid, hid, bid, crew[j], wind, out _, true);   // (all of them in the water)
                    continue;
                }
                ref var p = ref w.People.Get(crew[0]);
                Load(ref p, sid, to, BoatLoadKg);
                if (p.Cargo == None) continue;
                double outKg = p.CargoQty * Kg(p.Cargo);
                Exchange(ref p, to, BoatLoadKg);
                double backKg = p.Cargo != None ? p.CargoQty * Kg(p.Cargo) : 0.0;
                for (int j = 0; j < crew.Count; j++) w.Disease.Arrive(ref w.People.Get(crew[j]), to, SimTime.Hours(VisitHoursCoarse));
                w.DeedBook.Meet(sid, to);
                Unload(ref w.People.Get(crew[0]), sid);
                w.Metrics.Voyages++; w.Metrics.SeaCargoKg += outKg + backKg;
            }
        }

        readonly System.Collections.Generic.List<int> _crew = new System.Collections.Generic.List<int>();

        /// <summary>The hours a COARSE trader spends in the other village (a lived trader's stay).</summary>
        public const double VisitHoursCoarse = 3.0;

        /// <summary>
        /// Relief: a village with grain to spare sends a share of it to each neighbour within a day's walk whose people are dying
        /// of hunger (a famine this year), carried by its people as gifts - history tells it (D2-067; F5's famine coping).
        /// </summary>
        void Relief(int sid, int step)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            double spare = Spare(sid, Grain);
            if (spare <= 0) return;
            int boats = -1;   // (its plank boats that can carry a load: counted when a famine beyond a day's walk calls for them, C9.3)
            for (int o = w.Settlements.First(); o != 0 && spare > 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || t.FamineEvent == 0 || w.Now - t.FamineAt > SimTime.Year / 4 + (step - 1) * SimTime.Day) continue;   // (DEEP: a step that began in the window)
                if (w.DeedBook.Grievance(sid, o) > Grudge) continue;   // (no grain for those who took from it: D2-069)
                double far = Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius);
                // a twentieth of what can be spared a day, as much as feeds them three days (what its people can carry over), while
                // their store holds less than a month: relief feeds the hungry, it does not fill a granary (nor reach a village whose
                // dead starved beside a full one). Beyond a day's walk, by sea: a boat's load a day for each of its plank boats, along
                // the coast to the famine's landing, in a calm sea (C9.3)
                double carry;
                bool bySea = false;
                if (far <= 6000) carry = 3.0 * need0(w, in t) * step;
                else
                {
                    if (far > SeaRoutes.Most || t.LandAt == 0) continue;
                    if (boats < 0) boats = LoadBoats(sid);
                    if (boats == 0) continue;
                    var way = w.SeaRoutes.Get(w, sid, o);
                    if (way == null || Boats.WindAt(w, way.PointAt(0.5 * way.Length)) > Boats.Calm) continue;
                    carry = boats * BoatLoadKg / Kg(Grain) * step;
                    bySea = true;
                }
                double days = DaysOfFood(w, in t, out double need);
                if (days >= ReliefUpToDays) continue;
                double gift = DMath.Min(DMath.Min(0.05 * spare * step, carry), (ReliefUpToDays - days) * need);   // (DEEP: the step's days of it)
                if (gift < 1.0) continue;
                if (bySea) { w.Metrics.ReliefBySeaFu += gift; w.Metrics.Voyages++; w.Metrics.SeaCargoKg += gift * Kg(Grain); }
                ref var s2 = ref w.Settlements.Get(sid);
                s2.Granary -= gift; spare -= gift;
                ref var t2 = ref w.Settlements.Get(o);
                t2.Granary += gift;
                w.Metrics.ReliefFu += gift;
                Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Relief, sid, o);
                long told = 0;
                if (t2.ReliefAt != t2.FamineEvent)
                {
                    // (told once a famine: the first neighbour to send grain)
                    t2.ReliefFrom = sid; t2.ReliefAt = t2.FamineEvent;
                    told = w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.ReliefSent, Time = w.Now, Cell = t2.Cell, Settlement = o, Subject = EntityRef.Settlement(sid),
                        Object = EntityRef.Settlement(o), Cause1 = t2.FamineEvent, Rel1 = CauseRel.InResponseTo, Magnitude = (float)gift, RootKind = RootKind.People,
                        ArgA = bySea ? 1 : 0,
                    });
                }
                // the help is a deed both villages know: one deed a giver and a famine, its worth what was given (D2-069)
                int deed = w.DeedBook.HelpSince(sid, o, t2.FamineAt);
                if (deed != 0) w.DeedBook.AddWorth(deed, gift, gift);
                else
                {
                    deed = w.DeedBook.Record(TC2.Core.Society.DeedKind.Relief, 0, sid, 0, o, Grain, gift, gift, told);
                    w.DeedBook.Learn(o, deed, TC2.Core.Society.KnownHow.Saw);
                    w.DeedBook.Learn(sid, deed, TC2.Core.Society.KnownHow.Own);
                }
            }
        }

        static double need0(World w, in Settlement t) { DaysOfFood(w, in t, out double need); return need; }

        /// <summary>The village's plank boats that can carry a load now: sound, at its landing, of a household (C9.3).</summary>
        int LoadBoats(int sid)
        {
            var w = _w;
            var bl = w.BuildingsOf(sid);
            int n = 0;
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); if (Boats.IsPlank(b.Kind) && Boats.Sound(in b, w.Now) && b.Household != 0) n++; }
            return n;
        }

        /// <summary>Relief goes to a famine while the receiver's store holds less than this many days of its need.</summary>
        public const double ReliefUpToDays = 30.0;

        static int Poisson(double mean, ulong key)
        {
            if (mean <= 0) return 0;
            var rs = new RandomSequence(key);
            double l = DMath.Exp(-DMath.Min(mean, 30.0)), p = 1.0;
            int k = 0;
            do { k++; p *= rs.NextUnit(); } while (p > l && k < 60);
            return k - 1;
        }
    }
}
