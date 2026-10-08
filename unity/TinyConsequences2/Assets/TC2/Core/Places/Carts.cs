using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Carts (C4.2, D2-122; `24` §2). A person carries a back's load (`Trade.LoadKg`, 25 kg) - and measured, 83 % of trading loads set out were
    /// bound by it: more was spared at home and wanted at the other end. A household makes a **cart** when one of its traders came home from such a
    /// trip along a way worn to a path or a road (where wheels can go) and one of its people works wood well enough to make one: a **handcart**
    /// (two wheels, shafts, a bed; pushed or pulled by a person - `HandKg`) or, where the household keeps a grown ox, an **ox cart** (`OxKg`,
    /// drawn by the ox). It is made in the household's free hours like a boat (C9.1), from the village's wood, and kept by the house until it is
    /// worn out. A trader of a household with a cart takes it on a path or a road: a cart's load out and back, at the cart's pace. One law at both
    /// resolutions (the load is set when the trader loads, `Trade.Load`). The cart is the household's - nothing is built on the village's ground.
    /// </summary>
    public static class Carts
    {
        public const byte None = 0, Hand = 1, Ox = 2;
        /// <summary>The state of a household's cart: none, wanted (a trader came home wishing for one), being made, made.</summary>
        public const byte NoCart = 0, Wanted = 1, Making = 2, Made = 3;
        /// <summary>Kg a handcart carries on a path or road (a barrow-cart pushed by one person: a bed of planks on two wheels).</summary>
        public const double HandKg = 120.0;
        /// <summary>Kg an ox cart carries (`24` §2: an ox's cart 500-700 kg; one ox here, the lower end).</summary>
        public const double OxKg = 500.0;
        /// <summary>The pace a loaded cart goes at against a walker's.</summary>
        public const double HandPace = 0.85, OxPace = 0.7;
        /// <summary>Labour-hours and loads of wood to make one: wheels and an axle shaped, a bed of planks, shafts (an ox cart's yoke and pole).</summary>
        public const double HandHours = 30.0, OxHours = 80.0, HandWood = 3.0, OxWood = 10.0;
        /// <summary>Years a cart lasts before its wheels and axle are past mending.</summary>
        public const double HandYears = 12.0, OxYears = 20.0;
        /// <summary>The woodworking skill that can shape a wheel and an axle (`Person.SkWood`).</summary>
        public const double Wright = 0.4;
        /// <summary>Trading trips a household's traders carried a back's load where more was spared and wanted, none longer apart than `NeedYears`,
        /// before it wants a cart.</summary>
        public const int NeedTrips = 3;
        public const double NeedYears = 2.0;
        /// <summary>A cart not made two years after it was begun is given up (no hands came to it).</summary>
        public const double PatienceYears = 2.0;
        /// <summary>Hours a day a grown member gives the household's cart in a COARSE day before the village's building (their own small job).</summary>
        public const double HoursADay = 2.0;

        /// <summary>Tools: no carts (the same binary as before C4.2, for A/B runs); false in the game.</summary>
        public static bool Off;

        public static double KgOf(byte kind) => kind == Ox ? OxKg : kind == Hand ? HandKg : Trade.LoadKg;
        public static double PaceOf(byte kind) => kind == Ox ? OxPace : kind == Hand ? HandPace : 1.0;
        public static double HoursOf(byte kind) => kind == Ox ? OxHours : HandHours;
        public static double WoodOf(byte kind) => kind == Ox ? OxWood : HandWood;
        public static double YearsOf(byte kind) => kind == Ox ? OxYears : HandYears;
        public static string NameOf(byte kind) => kind == Ox ? "ox cart" : "handcart";

        /// <summary>The cart the household can take out now (its kind; None): made, not worn out, and an ox cart only while it keeps a grown ox.</summary>
        public static byte Usable(World w, int hid)
        {
            if (Off || hid == 0 || !w.Households.Exists(hid)) return None;
            ref var hh = ref w.Households.Get(hid);
            if (hh.CartState != Made) return None;
            if (WornOut(in hh, w.Now)) return None;   // (left at the day's planning: reads never change the world)
            if (hh.CartKind == Ox && !w.Stock.HasOx(hid)) return None;   // (the ox died or was sold: the cart stands in the yard)
            return hh.CartKind;
        }

        /// <summary>A made cart past its years.</summary>
        public static bool WornOut(in Household hh, long now) => hh.CartState == Made && now - hh.CartAt >= (long)(YearsOf(hh.CartKind) * SimTime.Year);

        /// <summary>A way wheels can go: worn to a path or a road between the two villages (C4.1).</summary>
        public static bool Wheeled(World w, int from, int to) => from != to && w.Ways.State(w.Now, from, to) >= 2;

        /// <summary>The load (kg) a trader of this household can take between these villages now: a cart's on a worn way, else a back's.</summary>
        public static double LoadFor(World w, int hid, int from, int to)
        {
            byte k = Usable(w, hid);
            return k != None && Wheeled(w, from, to) ? KgOf(k) : Trade.LoadKg;
        }

        /// <summary>The pace a traveller goes at: a cart's when they carry more than a back's load.</summary>
        public static double Pace(World w, in Person p)
        {
            if (p.Cargo == Trade.None || p.CargoQty <= 0f || Trade.KgOf(p.Cargo) * p.CargoQty <= Trade.LoadKg + 1e-6) return 1.0;
            byte k = Usable(w, p.Household);
            return k != None ? PaceOf(k) : 1.0;
        }

        /// <summary>Loads (of 25 kg) a trip home the household's cart carries from the woods or the quarry, and its pace (one at a walk with none).</summary>
        public static void Haul(World w, int hid, out double trip, out double pace)
        {
            byte k = Usable(w, hid);
            trip = k == None ? 1.0 : KgOf(k) / Trade.LoadKg; pace = PaceOf(k);
        }

        /// <summary>m from which carrying wood or stone home on the back is most of a stint's work: a cart is wanted for it.</summary>
        public const double HaulFrom = 400.0;

        /// <summary>A stint of the household's at woods or a quarry this far off, carried home on the back: a need for a cart (`Want`).</summary>
        public static void WantHaul(World w, int hid, double dist)
        {
            if (dist >= HaulFrom) Need(w, hid);
        }

        /// <summary>
        /// A trader of the household carried a back's load where more was spared and wanted, along a way wheels can go: a need for a cart.
        /// </summary>
        public static void Want(World w, int hid, int from, int to)
        {
            if (Wheeled(w, from, to)) Need(w, hid);
        }

        /// <summary>
        /// The household's backs were too small again: it wants a cart once its needs come `NeedTrips` times, none `NeedYears` apart, if one of its
        /// people can make it (`Wright`) and it has none.
        /// </summary>
        static void Need(World w, int hid)
        {
            if (Off || hid == 0 || !w.Households.Exists(hid)) return;
            ref var hh = ref w.Households.Get(hid);
            if (hh.CartState != NoCart || hh.Building == 0) return;   // (a household in a camp has no yard to keep a cart in)
            // (wanted where its traders keep finding their backs too small - `NeedTrips` such trips, none `NeedYears` apart: one trip made a cart
            // that then stood in the yard)
            hh.CartNeed = w.Now - hh.CartAt > (long)(NeedYears * SimTime.Year) ? (byte)1 : (byte)System.Math.Min(255, hh.CartNeed + 1);
            hh.CartAt = w.Now;
            if (hh.CartNeed < NeedTrips || !HasWright(w, hid)) return;
            hh.CartState = Wanted; hh.CartNeed = 0;
        }

        /// <summary>One of the household's grown, able people (16-59, well) can shape a wheel and an axle.</summary>
        public static bool HasWright(World w, int hid)
        {
            var mem = w.Members(hid);
            for (int i = 0; i < mem.Count; i++)
            {
                ref var q = ref w.People.Get(mem[i]);
                double age = q.AgeYears(w.Now);
                if (q.Alive && q.SkWood >= Wright && age >= 16 && age < 60 && q.Health > 0.6) return true;   // (an able wright: one of 65 wanted carts nobody made)
            }
            return false;
        }

        /// <summary>
        /// A village's day of carts (both resolutions): households that want a cart begin it as far as the store's wood goes (an ox cart where they
        /// keep a grown ox); carts begun a year ago and not made are given up; worn carts are left.
        /// </summary>
        public static void Plan(World w, int sid)
        {
            if (Off) return;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                int hid = hl[i];
                ref var hh = ref w.Households.Get(hid);
                if (hh.CartState == Made) { if (WornOut(in hh, w.Now)) Worn(w, hid); continue; }   // (worn ones left)
                if (hh.CartState == Making && w.Now - hh.CartAt > (long)(PatienceYears * SimTime.Year)) { hh.CartState = NoCart; hh.CartHours = 0f; w.Metrics.CartsGivenUp++; continue; }
                if (hh.CartState != Wanted) continue;
                if (!HasWright(w, hid)) { hh.CartState = NoCart; continue; }   // (its wright died or grew old while the wood was short)
                byte kind = w.Stock.HasOx(hid) ? Ox : Hand;
                ref var s = ref w.Settlements.Get(sid);
                double wood = WoodOf(kind);
                if (s.Woodstore < wood + 2.0 * s.FuelDay) continue;   // (the hearths' wood first)
                s.Woodstore -= wood;
                ref var h2 = ref w.Households.Get(hid);
                h2.CartKind = kind; h2.CartState = Making; h2.CartHours = 0f; h2.CartAt = w.Now;
                w.Metrics.CartsBegun++;
            }
        }

        /// <summary>The household has a cart to make.</summary>
        public static bool HasWork(World w, int hid) => !Off && hid != 0 && w.Households.Exists(hid) && w.Households.Get(hid).CartState == Making;

        /// <summary>
        /// Hours into the household's cart by one of its people (`rate`: their building pace, `worker` the maker); when its hours are in it is made -
        /// history tells it. Returns the hours that found work.
        /// </summary>
        public static double Work(World w, int hid, double hours, double rate, int worker)
        {
            if (!HasWork(w, hid) || hours <= 0 || rate <= 0) return 0;
            ref var hh = ref w.Households.Get(hid);
            double need = HoursOf(hh.CartKind), doable = DMath.Min(hours * rate, need - hh.CartHours);
            if (doable <= 0) return 0;
            hh.CartHours = (float)(hh.CartHours + doable);
            if (worker != 0 && w.People.Exists(worker)) { ref var q = ref w.People.Get(worker); q.SkWood = (float)DMath.Min(1.0, q.SkWood + 0.0015 * doable / rate); }
            if (hh.CartHours >= need - Construction.Done)
            {
                hh.CartState = Made; hh.CartAt = w.Now;
                int sid = hh.Settlement;
                ref var s = ref w.Settlements.Get(sid);
                w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.CartMade, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(hid),
                    Other = worker != 0 ? EntityRef.Person(worker) : default, ArgA = hh.CartKind, RootKind = RootKind.People,
                });
                w.Metrics.CartsMade++;
            }
            return doable / rate;
        }

        /// <summary>A cart past its years: left in the yard to rot; the household may want another.</summary>
        static void Worn(World w, int hid)
        {
            ref var hh = ref w.Households.Get(hid);
            hh.CartState = NoCart; hh.CartHours = 0f;
            w.Metrics.CartsWorn++;
        }
    }
}
