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

        public const byte None = 0, Grain = 1, Metal = 2, Fibre = 3, Hides = 4;
        public const int Goods = 5;
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
            return s.Granary / daily;
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
                case Metal: return 6.0 * (1.0 - Knowledge.Tech.MetalShare(s.Metal, s.Population)) + 0.5;
                case Fibre: return 0.5 + 2.0 * DMath.Clamp01(1.0 - s.Fibre / (2.0 * pop));
                case Hides: return 1.0 + 3.0 * DMath.Clamp01(1.0 - s.Hides / (1.0 * pop));
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
                case Grain: { DaysOfFood(w, in s, out double daily); return DMath.Max(0.0, s.Granary - 60.0 * daily); }
                case Metal: return DMath.Max(0.0, s.Metal - Knowledge.Tech.MetalPerHead * s.Population);
                case Fibre: return DMath.Max(0.0, s.Fibre - 2.0 * s.Population);
                case Hides: return DMath.Max(0.0, s.Hides - 1.0 * s.Population);
            }
            return 0;
        }

        static double Stock(ref Settlement s, byte g) => g == Grain ? s.Granary : g == Metal ? s.Metal : g == Fibre ? s.Fibre : s.Hides;
        static void Add(ref Settlement s, byte g, double q) { if (g == Grain) s.Granary += q; else if (g == Metal) s.Metal += q; else if (g == Fibre) s.Fibre += q; else if (g == Hides) s.Hides += q; }

        /// <summary>
        /// The best deal a village's trader could make with a neighbour now: the good to take out (and how much a load holds, as
        /// far as it is spared) and the gain to both, valued at the middle of the two villages' values. 0 when none is worth a walk.
        /// </summary>
        public double Plan(int from, int to, out byte give, out double qty, out byte take)
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
                double q = DMath.Min(spare, LoadKg / Kg(g));
                // what comes back: a good the other spares and this village wants more than they do
                for (byte h = 1; h < Goods; h++)
                {
                    if (h == g) continue;
                    double spareH = Spare(to, h);
                    double vfh = Value(from, h), vth = Value(to, h);
                    if (spareH <= 0 || vfh <= vth) continue;
                    double price = (vf + vt) / (vfh + vth);   // units of h a unit of g fetches, at the middle of both values
                    double qh = DMath.Min(DMath.Min(q * price, spareH), LoadKg / Kg(h));
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
                if (!t.Inhabited || Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > 6000) continue;
                double g = Plan(sid, o, out _, out _, out _);
                if (g > gain) { gain = g; bestTo = o; }
            }
            return bestTo;
        }

        // ------------------------------------------------------------------ a trader's trip (ACTIVITY) - the same steps a COARSE trip takes at once
        /// <summary>At departure: the load out of the village's store onto the trader's back.</summary>
        public void Load(ref Person p, int from, int to)
        {
            var w = _w;
            if (p.CargoQty > 0f && w.Settlements.Exists(p.Home)) Unload(ref p, p.Home);   // (whatever they still carry is home first: goods are never lost)
            if (Plan(from, to, out byte give, out double qty, out _) <= 0 || give == None) return;
            ref var s = ref w.Settlements.Get(from);
            qty = DMath.Min(qty, Stock(ref s, give));
            if (qty <= 0) return;
            Add(ref s, give, -qty);
            p.Cargo = give; p.CargoQty = (float)qty; p.CargoTo = ToStore;
        }

        /// <summary>At the other village: the load into its store, a return load out of it (as the deal stands now), both stores gaining.</summary>
        public void Exchange(ref Person p, int at)
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
                double q = DMath.Min(DMath.Min(qg * price, spareH), LoadKg / Kg(h));
                double gain = (vfh - vth) * q;
                if (gain > bestGain) { bestGain = gain; take = h; qh = q; }
            }
            if (take == None) return;   // nothing they want for it now: the load goes home again
            Add(ref t, g, qg);
            Add(ref t, take, -qh);
            p.Cargo = take; p.CargoQty = (float)qh;
            w.Metrics.TradeTrips++;
            w.Metrics.TradeValue += (vt - vf) * qg + bestGain;
            if (g == Metal) w.Metrics.MetalTraded += qg;
            if (take == Metal) w.Metrics.MetalTraded += qh;
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
        public void Day(int sid)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            Relief(sid);
            if (w.Resolution != Resolution.Coarse) return;
            int to = BestPartner(sid, out double gain);
            if (to == 0) return;
            int adults = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); double age = p.AgeYears(now); if (p.Alive && p.Sick == 0 && age >= 18 && age < 56 && p.Health > 0.7) adults++; }
            int trips = Poisson(adults * TripsPerAdultDay, KeyedRng.Hash(w.Seed, RngStream.Travel, (ulong)sid, (ulong)now, 501));
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
                w.Disease.Arrive(ref p, to, SimTime.Hours(VisitHoursCoarse));   // (what they may catch there and carry home)
                w.DeedBook.Meet(sid, to);                                        // (and the news: D2-072)
                Unload(ref p, sid);
            }
        }

        /// <summary>The hours a COARSE trader spends in the other village (a lived trader's stay).</summary>
        public const double VisitHoursCoarse = 3.0;

        /// <summary>
        /// Relief: a village with grain to spare sends a share of it to each neighbour within a day's walk whose people are dying
        /// of hunger (a famine this year), carried by its people as gifts - history tells it (D2-067; F5's famine coping).
        /// </summary>
        void Relief(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            double spare = Spare(sid, Grain);
            if (spare <= 0) return;
            for (int o = w.Settlements.First(); o != 0 && spare > 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || t.FamineEvent == 0 || w.Now - t.FamineAt > SimTime.Year / 4) continue;
                if (Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius) > 6000) continue;
                if (w.DeedBook.Grievance(sid, o) > Grudge) continue;   // (no grain for those who took from it: D2-069)
                // a twentieth of what can be spared a day, as much as feeds them three days (what its people can carry over), while
                // their store holds less than a month: relief feeds the hungry, it does not fill a granary (nor reach a village whose
                // dead starved beside a full one)
                double days = DaysOfFood(w, in t, out double need);
                if (days >= ReliefUpToDays) continue;
                double gift = DMath.Min(DMath.Min(0.05 * spare, 3.0 * need), (ReliefUpToDays - days) * need);
                if (gift < 1.0) continue;
                ref var s2 = ref w.Settlements.Get(sid);
                s2.Granary -= gift; spare -= gift;
                ref var t2 = ref w.Settlements.Get(o);
                t2.Granary += gift;
                w.Metrics.ReliefFu += gift;
                long told = 0;
                if (t2.ReliefAt != t2.FamineEvent)
                {
                    // (told once a famine: the first neighbour to send grain)
                    t2.ReliefFrom = sid; t2.ReliefAt = t2.FamineEvent;
                    told = w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.ReliefSent, Time = w.Now, Cell = t2.Cell, Settlement = o, Subject = EntityRef.Settlement(sid),
                        Object = EntityRef.Settlement(o), Cause1 = t2.FamineEvent, Rel1 = CauseRel.InResponseTo, Magnitude = (float)gift, RootKind = RootKind.People,
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
