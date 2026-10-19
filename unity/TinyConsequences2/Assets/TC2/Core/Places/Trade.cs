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

        /// <summary>Tools: grain given by village (sid * 4: sent by land, sent by sea, received by land, received by sea; FU); null in the game.</summary>
        public static double[] Diag;
        /// <summary>Tools: no voyages and no relief by sea (C9.3 against C9.2b in one binary); false in the game.</summary>
        public static bool NoSea;
        /// <summary>Tools (C3.1): what wares were traded for - wares given, grain (FU), fibre (kg), hides, metal and iron (kg) taken for them, and the
        /// wares the shed villages' traders gave (to a village without a shed); null in the game.</summary>
        public static double[] WaresFor;
        /// <summary>Tools (C3.2): kg of ware traded; what came back for it - grain (FU), fibre (kg), hides, metal and iron (kg), wares; kg from a kiln village to one
        /// without; kg to villages with no clay within reach; null in the game.</summary>
        public static double[] PotsFor;
        /// <summary>Tools (C4.2 survey): trading loads set out, of them bound by what one person carries (the spare was more), return loads so bound, and kg
        /// carried out; null in the game.</summary>
        public static double[] LoadTally;
        /// <summary>Tools (C9.4 survey): FU of relief givers would have sent by sea beyond their free boats' room, the gifts so bound, and all gifts by sea; null in the game.</summary>
        public static double[] SeaShort;
        /// <summary>Tools (C9.5 survey, `--acrosstally`): grain moved between villages within a day's walk that no way on foot joins (across water) - [0] relief gifts,
        /// [1] their FU, [2] gifts given back, [3] their FU; [4] bows for bread to a giver no way on foot leads to (`Conflict.Indebted`); null in the game.</summary>
        public static double[] AcrossTally;

        public const byte None = 0, Grain = 1, Metal = 2, Fibre = 3, Hides = 4, Iron = 5, Wares = 6, Pots = 7;   // (Metal is copper; iron since C5.3b; wares since C3.1; pots and jars, kg of fired ware, since C3.2)
        public const int Goods = 8;
        /// <summary>A trader's load (kg) and each good's weight a unit (a FU of grain, a kg of metal or fibre, a hide).</summary>
        public const double LoadKg = 25.0;
        static double Kg(byte g) => g == Grain ? 0.75 : g == Hides ? 3.0 : g == Wares ? 3.0 : 1.0;   // (a ware: a person's cloth and goods, ~3 kg)
        /// <summary>Kg a unit of a good weighs.</summary>
        public static double KgOf(byte g) => Kg(g);
        /// <summary>Trading trips a healthy grown person makes a day when a deal is to be had (COARSE: fitted to lived days).</summary>
        public const double TripsPerAdultDay = 0.01;
        /// <summary>The least gain (in the villages' own values) worth a day's walk.</summary>
        public const double WorthAWalk = 4.0;
        /// <summary>Where a load goes at home: the village's store, or the household's larder (what was taken: D2-069).</summary>
        public const byte ToStore = 0, ToLarder = 1;
        /// <summary>A load of the lord's tenth on its way to the lord's store (C10.6; brought back to the village's store if the bond ends on the way).</summary>
        public const byte ToLord = 5;
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
                case Wares: return 0.5 + Weaving.Worth * (1.0 - Weaving.Share(in s));   // (the hours wares save, for the share of its people in none yet: C3.1)
                case Pots: return Pottery.Off ? 0.0 : Pottery.Worth(w, in s);   // (the grain jars would keep, the wood pots would spare its hearths: C3.2)
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
                case Wares: return DMath.Max(0.0, s.Wares - Weaving.Kept * s.Population);
                case Pots: return Pottery.Off ? 0.0 : Pottery.Spare(in s);
            }
            return 0;
        }

        static double Stock(ref Settlement s, byte g) => g == Grain ? s.Granary : g == Metal ? s.Metal : g == Fibre ? s.Fibre : g == Iron ? s.Iron : g == Wares ? s.Wares : g == Pots ? s.Pots : s.Hides;
        void Add(ref Settlement s, byte g, double q)
        {
            if (q < 0 && (g == Wares || g == Pots)) _w.Holdings.Out(s.Id, 0, g == Wares ? Holdings.Wares : Holdings.Pots, -q);   // (sold: the village pays its makers: C3.3b)
            if (g == Grain) s.Granary += q; else if (g == Metal) s.Metal += q; else if (g == Fibre) s.Fibre += q; else if (g == Hides) s.Hides += q;
            else if (g == Iron) { s.Iron += q; if (q < 0) s.IronSold = (float)((double)s.IronSold - q); }   // (iron parted with: its smiths make that much more for the next buyers, C5.3b)
            else if (g == Wares) { s.Wares += q; if (q < 0) s.WaresSold = (float)((double)s.WaresSold - q); }   // (wares parted with: its makers make that much more, C3.1)
            else if (g == Pots) { s.Pots += q; if (q < 0) s.PotsSold = (float)((double)s.PotsSold - q); }   // (ware parted with: its potters make that much more, C3.2)
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
            bool land = loadKg == LoadKg;
            if (land) loadKg = Carts.LoadFor(w, p.Household, from, to);   // (over land: the household's cart on a worn way, C4.2)
            if (Plan(from, to, out byte give, out double qty, out _, loadKg) <= 0 || give == None) return;
            ref var s = ref w.Settlements.Get(from);
            qty = DMath.Min(qty, Stock(ref s, give));
            if (qty <= 0) return;
            bool bound = Spare(from, give) > loadKg / Kg(give) + 1e-9;   // (the load, not the spare, bound it)
            if (LoadTally != null) { LoadTally[0]++; LoadTally[3] += qty * Kg(give); if (bound) LoadTally[1]++; }
            if (land && loadKg > LoadKg) { w.Metrics.CartTrips++; w.Metrics.CartKg += qty * Kg(give); }
            else if (land && bound) Carts.Want(w, p.Household, from, to);   // (a back's load where more was spared and wanted: a cart, C4.2)
            Add(ref s, give, -qty);
            p.Cargo = give; p.CargoQty = (float)qty; p.CargoTo = ToStore;
        }

        /// <summary>At the other village: the load into its store, a return load out of it (as the deal stands now), both stores gaining.</summary>
        public void Exchange(ref Person p, int at, double loadKg = LoadKg)
        {
            var w = _w;
            if (p.Cargo == None || p.CargoQty <= 0) return;
            int home = p.Home;
            if (loadKg == LoadKg) loadKg = Carts.LoadFor(w, p.Household, home, at);   // (the cart they came with takes the return load: C4.2)
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
            if (LoadTally != null && Spare(at, take) > loadKg / Kg(take) + 1e-9 && qg * (vf + vt) / (Value(home, take) + Value(at, take)) > loadKg / Kg(take) + 1e-9) LoadTally[2]++;   // (and the return load)
            Add(ref t, g, qg);
            Add(ref t, take, -qh);
            p.Cargo = take; p.CargoQty = (float)qh;
            w.Metrics.TradeTrips++;
            t.TradeIn = (float)((double)t.TradeIn + 1.0);   // (one more trader came to deal here: C1.6)
            Structures.MarketDealt(w, at);   // (and to its market: its market place widens - C13.3)
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Trade, home, at);
            w.Metrics.TradeValue += (vt - vf) * qg + bestGain;
            if (g == Metal) w.Metrics.MetalTraded += qg;
            if (take == Metal) w.Metrics.MetalTraded += qh;
            if (g == Iron) w.Metrics.IronTraded += qg;
            if (take == Iron) w.Metrics.IronTraded += qh;
            if (g == Wares) w.Metrics.WaresTraded += qg;
            if (WaresFor != null && g == Wares)
            {
                WaresFor[0] += qg;
                WaresFor[take == Grain ? 1 : take == Fibre ? 2 : take == Hides ? 3 : 4] += qh;
                if (Weaving.Shed(w, home) != 0 && Weaving.Shed(w, at) == 0) WaresFor[5] += qg;
            }
            if (take == Wares) w.Metrics.WaresTraded += qh;
            if (g == Pots) w.Metrics.PotsTraded += qg;
            if (take == Pots) w.Metrics.PotsTraded += qh;
            if (PotsFor != null && (g == Pots || take == Pots))
            {
                // (C3.2: what ware went for, and from villages with a kiln to villages without)
                bool give = g == Pots; byte other = give ? take : g; double q = give ? qg : qh, qo = give ? qh : qg;
                int seller = give ? home : at, buyer = give ? at : home;
                PotsFor[0] += q;
                PotsFor[other == Grain ? 1 : other == Fibre ? 2 : other == Hides ? 3 : other == Wares ? 5 : 4] += qo;
                if (Pottery.Kiln(w, seller) != 0 && Pottery.Kiln(w, buyer) == 0) PotsFor[6] += q;
                if (!w.Learning.Clay(w.Settlements.Get(buyer).Cell)) PotsFor[7] += q;
            }
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
            else if (p.CargoTo == ToLord && p.Cargo == Grain && home == p.Home && w.Settlements.Get(home).Lord != 0)
            {
                // a load of the lord's tenth home again before it reached the lord (everyone home at once in COARSE) while the village still pays:
                // set aside with the rest, to be carried again (C10.6); a bond ended on the way, it is the village's (the store, below)
                w.Settlements.Get(home).TributeDue += p.CargoQty;
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
            Lapse(sid);   // (yesterday's voyages nobody sailed: C9.3)
            Relief(sid, step);
            GiveBack(sid, step);   // (what it was given in its famines, given back when it can spare it: D2-113)
            if (!NoSea) Voyages(sid, step);   // (by sea: COARSE at once, lived booked for a crew to sail - C9.3)
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
        /// A village's day of voyages: its plank boats lying free at the landing put out to the coastal village with the best deal for a boat's
        /// load, at the lived rate. COARSE (DEEP: a step's) takes each whole at once - a crew of its household, the sea's chance as they put out
        /// (the wind over the way's middle; nobody puts out in a wind the boat would not fish in), the load out and the return load home, the crew
        /// visitors there (what they may catch, the news); a boat overturned is lost with its load. Lived, the village books the voyage on the
        /// boat and its household sails it in the morning (`AgentSystem.StartVoyage`): the crew row the way, deal there and row home.
        /// </summary>
        void Voyages(int sid, int step)
        {
            var w = _w;
            long now = w.Now;
            int to = BestSeaPartner(sid, out _, out var way);
            if (to == 0) return;
            var bl = w.BuildingsOf(sid);
            int boats = 0;
            for (int i = 0; i < bl.Count; i++) if (Free(bl[i])) boats++;
            if (boats == 0) return;
            int n = System.Math.Min(boats, Poisson(boats * VoyagesPerBoatDay * step, KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, 601)));
            bool lived = w.Resolution != Resolution.Coarse;
            int k = 0;
            for (int i = 0; i < bl.Count && k < n; i++)
            {
                int bid = bl[i];
                if (!Free(bid)) continue;
                k++;
                ref var b = ref w.Buildings.Get(bid);
                int hid = b.Household;
                byte kind = b.Kind;
                if (lived) { Book(bid, hid, sid, to, ForTrade, 0.0); continue; }   // (its household sails it in the morning)
                // its crew: the household's healthy grown people, a seat each
                var crew = _crew; crew.Clear();
                var mem = w.Members(hid);
                for (int j = 0; j < mem.Count && crew.Count < Boats.SeatsOf(kind); j++)
                {
                    ref var q = ref w.People.Get(mem[j]);
                    if (CanSail(in q, sid, now)) crew.Add(mem[j]);
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
                w.Metrics.Voyages++; w.Metrics.VoyageSeats += crew.Count; w.Metrics.SeaCargoKg += outKg + backKg;
            }
        }

        /// <summary>
        /// Whether someone can take an oar on a voyage now (both resolutions): alive, healthy and grown (16-55), at home, not hurt, not with a
        /// band, not already at sea, not near their time nor nursing a baby under half a year.
        /// </summary>
        public static bool CanSail(in Person q, int sid, long now)
        {
            double age = q.AgeYears(now);
            return q.Alive && q.Sick == 0 && age >= 16 && age < 56 && q.Health > 0.7 && q.At == sid && q.Home == sid && q.Band == 0 &&
                   q.Hurt < TC2.Core.Society.Conflict.RoadHurt && q.Voyage == 0 && q.Act != ActivityKind.Sail && q.Step != StepKind.Travel &&
                   !(q.PregDue != 0 && q.PregDue - now < 10 * SimTime.Day) && !(q.NursingUntil != 0 && q.NursingUntil - now > SimTime.Year / 2);
        }

        readonly System.Collections.Generic.List<int> _crew = new System.Collections.Generic.List<int>();

        /// <summary>The hours a COARSE trader spends in the other village (a lived trader's stay).</summary>
        public const double VisitHoursCoarse = 3.0;

        /// <summary>
        /// Relief: a village with grain to spare sends a share of it to each neighbour within a day's walk whose people are dying
        /// of hunger (a famine this year), carried by its people as gifts - history tells it (D2-067; F5's famine coping). A gift is
        /// owed back (D2-113, social storage - "bad year economics": food given in another's bad year is a claim on theirs): a giver
        /// gives the less the more of what it gave the other still stands unreturned, against two months of their need, so a village
        /// whose land cannot feed it is not fed by its neighbours for ever; what is given back (`GiveBack`) opens their hands again.
        /// Beyond a day's walk, grain goes by sea (C9.3): COARSE at once; lived, booked on its boats for their crews to carry.
        /// </summary>
        void Relief(int sid, int step)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            double spare = Spare(sid, Grain);
            if (spare <= 0) return;
            int boats = -1;   // (its plank boats free to carry a load: counted when a famine beyond a day's walk calls for them, C9.3)
            bool lived = w.Resolution != Resolution.Coarse;
            for (int o = w.Settlements.First(); o != 0 && spare > 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || t.FamineEvent == 0 || w.Now - t.FamineAt > SimTime.Year / 4 + (step - 1) * SimTime.Day) continue;   // (DEEP: a step that began in the window)
                if (w.DeedBook.Grievance(sid, o) > Grudge) continue;   // (no grain for those who took from it: D2-069)
                double far = Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius);
                // a twentieth of what can be spared a day, as much as feeds them three days (what its people can carry over), while
                // their store holds less than a month: relief feeds the hungry, it does not fill a granary (nor reach a village whose
                // dead starved beside a full one). Beyond a day's walk, by sea: a boat's load a day for each of its free plank boats,
                // along the coast to the famine's landing, in a calm sea (C9.3)
                double carry;
                bool bySea = false;
                if (far <= 6000) carry = 3.0 * need0(w, in t) * step;
                else
                {
                    if (NoSea || far > SeaRoutes.Most || t.LandAt == 0) continue;
                    if (boats < 0) boats = LoadBoats(sid);
                    if (boats == 0) continue;
                    var way = w.SeaRoutes.Get(w, sid, o);
                    if (way == null || Boats.WindAt(w, way.PointAt(0.5 * way.Length)) > Boats.Calm) continue;
                    carry = boats * BoatLoadKg / Kg(Grain) * step;
                    bySea = true;
                }
                double days = DaysOfFood(w, in t, out double need);
                if (days >= ReliefUpToDays) continue;
                // what it gave them before and has not had back weighs against giving more (D2-113) - but a lord feeds its own (C10.4: its tribute
                // gives the grain back)
                double open = !TC2.Core.Society.Conflict.BreadOff && t.Lord == sid ? 1.0 : DMath.Clamp01(1.0 - w.DeedBook.Owed(o, sid) / (CreditDays * need));
                double gift = DMath.Min(DMath.Min(0.05 * spare * step, carry), (ReliefUpToDays - days) * need) * open;   // (DEEP: the step's days of it)
                if (SeaShort != null && bySea)
                {
                    double would = DMath.Min(0.05 * spare * step, (ReliefUpToDays - days) * need) * open;
                    SeaShort[2] += 1;
                    if (would > carry * open + 1e-9) { SeaShort[0] += would - carry * open; SeaShort[1] += 1; }
                }
                if (gift < (bySea ? MinSeaLoadFu : 1.0)) continue;   // (nobody rows a boat over the sea for a few baskets)
                if (bySea && lived)
                {
                    // lived: a boatload booked on each free boat it takes, for their households to row over in the morning - given when landed
                    double left = gift;
                    var bl = w.BuildingsOf(sid);
                    for (int i = 0; i < bl.Count && left >= 1.0; i++)
                    {
                        if (!Free(bl[i])) continue;
                        double q = DMath.Min(left, BoatLoadKg / Kg(Grain));
                        Book(bl[i], w.Buildings.Get(bl[i]).Household, sid, o, ForRelief, q);
                        left -= q;
                    }
                    spare -= gift - left;
                    boats = -1;
                    continue;
                }
                ref var s2 = ref w.Settlements.Get(sid);
                s2.Granary -= gift; spare -= gift;
                if (bySea) { w.Metrics.Voyages++; w.Metrics.SeaCargoKg += gift * Kg(Grain); }
                Given(sid, o, gift, bySea);
                if (AcrossTally != null && !bySea && w.Routes.Get(w, sid, o) == null) { AcrossTally[0] += 1; AcrossTally[1] += gift; }   // (tools: across water at once)
            }
        }

        /// <summary>
        /// Grain given to a famine (relief; the grain itself moved by the caller): the receiver's store takes it, the deed both villages know
        /// (one a giver and a famine, its worth what was given: D2-069) and its telling - once a giver and a famine, each gift owed back on
        /// its own (D2-113).
        /// </summary>
        void Given(int sid, int o, double gift, bool bySea)
        {
            var w = _w;
            if (bySea) w.Metrics.ReliefBySeaFu += gift;
            if (Diag != null && sid < Diag.Length / 4 && o < Diag.Length / 4) { Diag[sid * 4 + (bySea ? 1 : 0)] += gift; Diag[o * 4 + (bySea ? 3 : 2)] += gift; }
            ref var t2 = ref w.Settlements.Get(o);
            t2.Granary += gift;
            w.Metrics.ReliefFu += gift;
            Diagnostics.ScaleTally.Pair(w, Diagnostics.ScaleTally.Relief, sid, o);
            if (t2.ReliefAt != t2.FamineEvent) { t2.ReliefFrom = sid; t2.ReliefAt = t2.FamineEvent; }   // (the first neighbour to send grain)
            int deed = w.DeedBook.HelpSince(sid, o, t2.FamineAt);
            if (deed != 0) { w.DeedBook.AddWorth(deed, gift, gift); return; }
            long told = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.ReliefSent, Time = w.Now, Cell = t2.Cell, Settlement = o, Subject = EntityRef.Settlement(sid),
                Object = EntityRef.Settlement(o), Cause1 = t2.FamineEvent, Rel1 = CauseRel.InResponseTo, Magnitude = (float)gift, RootKind = RootKind.People,
                ArgA = bySea ? 1 : 0,
            });
            deed = w.DeedBook.Record(TC2.Core.Society.DeedKind.Relief, 0, sid, 0, o, Grain, gift, gift, told);
            w.DeedBook.Learn(o, deed, TC2.Core.Society.KnownHow.Saw);
            w.DeedBook.Learn(sid, deed, TC2.Core.Society.KnownHow.Own);
        }

        /// <summary>FU of grain worth a boat's voyage (a fifth of its load): less goes by land or waits (C9.3).</summary>
        public const double MinSeaLoadFu = 0.2 * BoatLoadKg / 0.75;

        /// <summary>Days of a famine village's need what a giver has given it and not had back may reach before it gives no more (D2-113).</summary>
        public const double CreditDays = 60.0;

        /// <summary>
        /// Gifts given back (D2-113, social storage): a village with grain to spare carries back what it was given in its famines - to each
        /// giver, the oldest gift first, a twentieth of its spare a day and what its people (or boats, along the coast in a calm sea: its own,
        /// or the giver's come for it) can carry - so its givers' hands open again. A gift weighs less with the years (help's half-life): what
        /// is owed fades as it is remembered. Lived, by sea is booked on a boat as relief is.
        /// </summary>
        void GiveBack(int sid, int step)
        {
            var w = _w;
            double spare = Spare(sid, Grain);
            if (spare <= 0 || !w.DeedBook.OwesAny(sid, 1.0)) return;
            var list = w.DeedBook.KnownBy(sid);
            if (list == null) return;
            long now = w.Now, since = now - TC2.Core.Society.DeedBook.OwedYears * SimTime.Year;
            // its givers, by their oldest gift still weighed, and what it owes each
            _givers.Clear(); _owes.Clear();
            int i0 = list.Count;
            while (i0 > 0 && w.Deeds.Get(list[i0 - 1].Deed).Time >= since) i0--;
            for (int i = i0; i < list.Count; i++)
            {
                if (list[i].How != TC2.Core.Society.KnownHow.Saw) continue;
                ref var d = ref w.Deeds.Get(list[i].Deed);
                if (d.Kind != TC2.Core.Society.DeedKind.Relief || d.ToS != sid) continue;
                double owe = w.DeedBook.Weight(in d, now);
                int k = _givers.IndexOf(d.ByS);
                if (k < 0) { _givers.Add(d.ByS); _owes.Add(owe); } else _owes[k] += owe;
            }
            bool lived = w.Resolution != Resolution.Coarse;
            int boats = -1;
            for (int k = 0; k < _givers.Count && spare > 0; k++)
            {
                int giver = _givers[k];
                double owe = _owes[k];
                if (owe < 1.0 || !w.Settlements.Exists(giver) || !w.Settlements.Get(giver).Inhabited || Estranged(sid, giver)) continue;
                ref var s = ref w.Settlements.Get(sid);
                ref var g = ref w.Settlements.Get(giver);
                double far = Vec3d.ArcDistance(s.Anchor, g.Anchor, w.Planet.Radius);
                double carry;
                bool bySea = false;
                int theirs = 0;
                if (far <= 6000) carry = 3.0 * need0(w, in s) * step;
                else
                {
                    if (NoSea || far > SeaRoutes.Most || s.LandAt == 0 || g.LandAt == 0) continue;
                    if (boats < 0) boats = LoadBoats(sid);
                    theirs = LoadBoats(giver);   // (or the giver's come for it)
                    if (boats + theirs == 0) continue;
                    var way = w.SeaRoutes.Get(w, sid, giver);
                    if (way == null || Boats.WindAt(w, way.PointAt(0.5 * way.Length)) > Boats.Calm) continue;
                    carry = (boats + theirs) * BoatLoadKg / Kg(Grain) * step;
                    bySea = true;
                }
                double back = DMath.Min(DMath.Min(0.05 * spare * step, carry), owe);
                if (back < (bySea ? MinSeaLoadFu : 1.0)) continue;
                if (bySea && lived)
                {
                    // lived: a boatload booked on its own free boat to carry it over, or on the giver's to come and fetch it
                    back = DMath.Min(back, BoatLoadKg / Kg(Grain));
                    int bid = boats > 0 ? FirstFree(sid) : 0;
                    if (bid != 0) { Book(bid, w.Buildings.Get(bid).Household, sid, giver, ForReturn, back); boats = -1; }
                    else
                    {
                        bid = FirstFree(giver);
                        if (bid == 0) continue;
                        Book(bid, w.Buildings.Get(bid).Household, giver, sid, ForFetch, back);
                    }
                    spare -= back;
                    continue;
                }
                ref var s2 = ref w.Settlements.Get(sid);
                s2.Granary -= back; spare -= back;
                ref var g2 = ref w.Settlements.Get(giver);
                g2.Granary += back;
                if (bySea) { w.Metrics.Voyages++; w.Metrics.SeaCargoKg += back * Kg(Grain); }
                Returned(sid, giver, back, bySea);
                if (AcrossTally != null && !bySea && w.Routes.Get(w, sid, giver) == null) { AcrossTally[2] += 1; AcrossTally[3] += back; }   // (tools: across water at once)
            }
        }

        readonly System.Collections.Generic.List<int> _givers = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.List<double> _owes = new System.Collections.Generic.List<double>();

        /// <summary>
        /// Grain given back for gifts (D2-113; the grain itself moved by the caller): the giver's gifts to it weigh that much less, oldest first;
        /// the first grain back for a gift is told, caused by the gift.
        /// </summary>
        void Returned(int sid, int giver, double qty, bool bySea)
        {
            var w = _w;
            w.Metrics.ReliefReturnedFu += qty;
            var list = w.DeedBook.KnownBy(sid);
            if (list == null) return;
            long now = w.Now, since = now - TC2.Core.Society.DeedBook.OwedYears * SimTime.Year;
            int i0 = list.Count;
            while (i0 > 0 && w.Deeds.Get(list[i0 - 1].Deed).Time >= since) i0--;
            double left = qty;
            for (int i = i0; i < list.Count && left > 1e-9; i++)
            {
                if (list[i].How != TC2.Core.Society.KnownHow.Saw) continue;
                int did = list[i].Deed;
                ref var d = ref w.Deeds.Get(did);
                if (d.Kind != TC2.Core.Society.DeedKind.Relief || d.ToS != sid || d.ByS != giver) continue;
                double owe = w.DeedBook.Weight(in d, now);
                if (owe <= 0) continue;
                double back = DMath.Min(left, owe);
                bool first = (d.Flags & TC2.Core.Society.DeedKind.Returned) == 0;
                long cause = d.Event;
                w.DeedBook.GiveBack(did, back);
                left -= back;
                if (!first) continue;
                ref var s = ref w.Settlements.Get(sid);
                w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.ReliefReturned, Time = now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Object = EntityRef.Settlement(giver), Cause1 = cause, Rel1 = CauseRel.InResponseTo, Magnitude = (float)back, RootKind = RootKind.People,
                    ArgA = bySea ? 1 : 0,
                });
            }
        }

        /// <summary>
        /// Grain a tributary's tribute gives back to its lord for the relief the lord gave it (C10.4, D2-157; the grain itself moved by the caller): the
        /// lord's gifts to it weigh that much less, oldest first - told as tribute paid, not as gifts returned.
        /// </summary>
        public void Repaid(int sid, int giver, double qty)
        {
            var w = _w;
            var list = w.DeedBook.KnownBy(sid);
            if (list == null || qty <= 0) return;
            long now = w.Now, since = now - TC2.Core.Society.DeedBook.OwedYears * SimTime.Year;
            int i0 = list.Count;
            while (i0 > 0 && w.Deeds.Get(list[i0 - 1].Deed).Time >= since) i0--;
            double left = qty;
            for (int i = i0; i < list.Count && left > 1e-9; i++)
            {
                if (list[i].How != TC2.Core.Society.KnownHow.Saw) continue;
                int did = list[i].Deed;
                ref var d = ref w.Deeds.Get(did);
                if (d.Kind != TC2.Core.Society.DeedKind.Relief || d.ToS != sid || d.ByS != giver) continue;
                double owe = w.DeedBook.Weight(in d, now);
                if (owe <= 0) continue;
                double back = DMath.Min(left, owe);
                w.DeedBook.GiveBack(did, back);
                left -= back;
            }
        }

        /// <summary>
        /// The neighbour a village owes most for relief (C10.4, D2-157): of the gifts it saw given within `DeedBook.OwedYears`, weighed now, the giver
        /// whose sum is largest (ties: the giver it first owed) - that sum, and the event of its weightiest gift from that giver (the cause a bond is
        /// told by). 0 none. Reads only.
        /// </summary>
        public int MainCreditor(int sid, out double owed, out long gift)
        {
            var w = _w;
            owed = 0; gift = 0;
            if (!w.DeedBook.OwesAny(sid, 1.0)) return 0;
            var list = w.DeedBook.KnownBy(sid);
            if (list == null) return 0;
            long now = w.Now, since = now - TC2.Core.Society.DeedBook.OwedYears * SimTime.Year;
            _givers.Clear(); _owes.Clear();
            int i0 = list.Count;
            while (i0 > 0 && w.Deeds.Get(list[i0 - 1].Deed).Time >= since) i0--;
            for (int i = i0; i < list.Count; i++)
            {
                if (list[i].How != TC2.Core.Society.KnownHow.Saw) continue;
                ref var d = ref w.Deeds.Get(list[i].Deed);
                if (d.Kind != TC2.Core.Society.DeedKind.Relief || d.ToS != sid) continue;
                double owe = w.DeedBook.Weight(in d, now);
                int k = _givers.IndexOf(d.ByS);
                if (k < 0) { _givers.Add(d.ByS); _owes.Add(owe); } else _owes[k] += owe;
            }
            int best = 0;
            for (int k = 0; k < _givers.Count; k++) if (_owes[k] > owed) { owed = _owes[k]; best = _givers[k]; }
            if (best == 0) return 0;
            double top = 0;
            for (int i = i0; i < list.Count; i++)
            {
                if (list[i].How != TC2.Core.Society.KnownHow.Saw) continue;
                ref var d = ref w.Deeds.Get(list[i].Deed);
                if (d.Kind != TC2.Core.Society.DeedKind.Relief || d.ToS != sid || d.ByS != best) continue;
                double owe = w.DeedBook.Weight(in d, now);
                if (owe > top) { top = owe; gift = d.Event; }
            }
            return best;
        }

        static double need0(World w, in Settlement t) { DaysOfFood(w, in t, out double need); return need; }

        // ------------------------------------------------------------------ lived voyages (C9.3)
        /// <summary>What a booked voyage is for: a load to trade, grain to a famine, grain given back, grain fetched back from those who owe it.</summary>
        public const byte ForTrade = 1, ForRelief = 2, ForReturn = 3, ForFetch = 4;
        /// <summary>Where a boat's load goes (`Person.CargoTo` beside ToStore and ToLarder): grain for a famine, grain given back, grain to fetch.</summary>
        public const byte AsGift = 2, AsReturn = 3, ToFetch = 4;

        /// <summary>A voyage its village decided on (lived, C9.3): booked on one of its plank boats for the household to sail in the morning.</summary>
        public struct Booking
        {
            public int Boat, Hh, From, To;
            public byte Kind;
            public double Qty;   // FU of grain (relief, given back, fetched); a trade's load is made at the landing
            public long At;      // when it was booked (the village's day)
            public void Write(BinWriter w) { w.Int(Boat); w.Int(Hh); w.Int(From); w.Int(To); w.Byte(Kind); w.Double(Qty); w.Long(At); }
            public static Booking Read(BinReader r) => new Booking { Boat = r.Int(), Hh = r.Int(), From = r.Int(), To = r.Int(), Kind = r.Byte(), Qty = r.Double(), At = r.Long() };
            public void Hash(ref StateHasher h) { h.Add(Boat); h.Add(Hh); h.Add(From); h.Add(To); h.Add(Kind); h.Add(Qty); h.Add(At); }
        }

        readonly System.Collections.Generic.List<Booking> _booked = new System.Collections.Generic.List<Booking>();

        public int Bookings => _booked.Count;
        public Booking BookingAt(int i) => _booked[i];
        /// <summary>The booking on a household's boat (its index), or -1.</summary>
        public int BookingOf(int hid) { for (int i = 0; i < _booked.Count; i++) if (_booked[i].Hh == hid) return i; return -1; }
        public bool IsBooked(int bid) { for (int i = 0; i < _booked.Count; i++) if (_booked[i].Boat == bid) return true; return false; }
        public void Unbook(int i) => _booked.RemoveAt(i);
        /// <summary>Everyone home at once (COARSE): nobody will sail what was booked.</summary>
        public void ClearBookings() => _booked.Clear();

        public void Book(int bid, int hid, int from, int to, byte kind, double qty) =>
            _booked.Add(new Booking { Boat = bid, Hh = hid, From = from, To = to, Kind = kind, Qty = qty, At = _w.Now });

        /// <summary>A village's bookings from its last day that nobody sailed (no hands, no weather) lapse: its day books afresh.</summary>
        void Lapse(int sid)
        {
            var w = _w;
            for (int i = _booked.Count - 1; i >= 0; i--)
                if (_booked[i].From == sid && w.Now - _booked[i].At >= SimTime.Day / 2) { _booked.RemoveAt(i); w.Metrics.VoyagesLapsed++; }
        }

        /// <summary>
        /// Whether a plank boat can carry a load now: sound, of a household, at its landing (nobody out in it), not booked, and its household has
        /// two who can take an oar (C9.3: the same at both resolutions - grain by sea goes in a boat somebody rows).
        /// </summary>
        bool Free(int bid)
        {
            var w = _w;
            ref var b = ref w.Buildings.Get(bid);
            return Boats.IsPlank(b.Kind) && Boats.Sound(in b, w.Now) && b.Household != 0 && !IsBooked(bid) && Boats.Aboard(w, b.Household, bid) == 0 && Crewed(b.Household, b.Settlement);
        }

        /// <summary>Whether a household has two who can take an oar now (a boat's load wants two at the oars at least).</summary>
        public bool Crewed(int hid, int sid)
        {
            var w = _w;
            var mem = w.Members(hid);
            int n = 0;
            for (int i = 0; i < mem.Count && n < 2; i++) if (CanSail(in w.People.Get(mem[i]), sid, w.Now)) n++;
            return n >= 2;
        }

        /// <summary>The village's plank boats free to carry a load now (C9.3).</summary>
        int LoadBoats(int sid)
        {
            var bl = _w.BuildingsOf(sid);
            int n = 0;
            for (int i = 0; i < bl.Count; i++) if (Free(bl[i])) n++;
            return n;
        }

        int FirstFree(int sid)
        {
            var bl = _w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) if (Free(bl[i])) return bl[i];
            return 0;
        }

        /// <summary>
        /// A boat's skipper at the other landing (lived voyages, C9.3): a load to trade is dealt for the return load, grain for a famine is
        /// given, grain given back goes into the giver's store, grain to fetch back is taken aboard from those who owe it.
        /// </summary>
        public void Deliver(ref Person p, int at)
        {
            var w = _w;
            int home = p.Home;
            if (!w.Settlements.Exists(at)) return;
            switch (p.CargoTo)
            {
                case AsGift:
                    if (p.Cargo == Grain && p.CargoQty > 0f) Given(home, at, p.CargoQty, true);
                    p.Cargo = None; p.CargoQty = 0f; p.CargoTo = ToStore;
                    return;
                case AsReturn:
                    if (p.Cargo == Grain && p.CargoQty > 0f) { w.Settlements.Get(at).Granary += p.CargoQty; Returned(home, at, p.CargoQty, true); }
                    p.Cargo = None; p.CargoQty = 0f; p.CargoTo = ToStore;
                    return;
                case ToFetch:
                {
                    // what they owe, as far as they still spare it, aboard for home
                    double q = DMath.Min(p.CargoQty, Spare(at, Grain));
                    p.Cargo = None; p.CargoQty = 0f; p.CargoTo = ToStore;
                    if (q < 1.0) return;
                    w.Settlements.Get(at).Granary -= q;
                    p.Cargo = Grain; p.CargoQty = (float)q;
                    Returned(at, home, q, true);
                    w.Metrics.SeaCargoKg += q * Kg(Grain);
                    return;
                }
                default:
                    if (p.Cargo == None || p.CargoQty <= 0f) return;
                    Exchange(ref p, at, BoatLoadKg);
                    if (p.Cargo != None && p.CargoQty > 0f) w.Metrics.SeaCargoKg += p.CargoQty * Kg(p.Cargo);
                    return;
            }
        }

        /// <summary>A booking's load put aboard at the landing as the boat sets out (lived): its weight (kg), or 0 when there is nothing to carry.</summary>
        public double LoadAboard(ref Person p, in Booking bk)
        {
            var w = _w;
            int sid = bk.From;
            switch (bk.Kind)
            {
                case ForTrade:
                    if (Plan(sid, bk.To, out _, out _, out _, BoatLoadKg) < WorthAVoyage) return 0;
                    Load(ref p, sid, bk.To, BoatLoadKg);
                    return p.Cargo != None ? p.CargoQty * Kg(p.Cargo) : 0;
                case ForRelief:
                case ForReturn:
                {
                    ref var s = ref w.Settlements.Get(sid);
                    double q = DMath.Min(bk.Qty, s.Granary);
                    if (q < 1.0) return 0;
                    if (p.CargoQty > 0f && w.Settlements.Exists(p.Home)) Unload(ref p, p.Home);
                    s.Granary -= q;
                    p.Cargo = Grain; p.CargoQty = (float)q; p.CargoTo = bk.Kind == ForRelief ? AsGift : AsReturn;
                    return q * Kg(Grain);
                }
                case ForFetch:
                    if (p.CargoQty > 0f && w.Settlements.Exists(p.Home)) Unload(ref p, p.Home);
                    p.Cargo = None; p.CargoQty = (float)bk.Qty; p.CargoTo = ToFetch;
                    return 1;   // (nothing aboard going out)
            }
            return 0;
        }

        public void Write(BinWriter w)
        {
            w.Int(_booked.Count);
            for (int i = 0; i < _booked.Count; i++) _booked[i].Write(w);
        }

        public void Read(BinReader r)
        {
            _booked.Clear();
            int n = r.Int();
            for (int i = 0; i < n; i++) _booked.Add(Booking.Read(r));
        }

        public void Hash(ref StateHasher h)
        {
            h.Add(_booked.Count);
            for (int i = 0; i < _booked.Count; i++) _booked[i].Hash(ref h);
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
