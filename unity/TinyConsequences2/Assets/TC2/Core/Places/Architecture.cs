using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// How a building is built (D2-114): a home's plan, its roof's form and cover, its walls and chimney, read from the world when its
    /// builders raise it - the village's building tradition (carried by its founders from the village they came from), the household's size
    /// and means, the village's size and whether its ring hems the homes in, what the ground and the village's knowledge give (stone, lime
    /// and clay, timber of the woods it fells, reed or straw thatch, shingles split with iron tools, tiles fired where pottery and clay are
    /// known), and the climate. Kept on the building (`Building.Form`): a house raised early keeps its early form while the village around it
    /// changes - the town's history stands in its houses. A village work takes the cover and walls of its village at its raising. The view
    /// draws it and the pages tell it; nothing in the simulation reads it yet (warmth, fire and decay by material are later).
    /// </summary>
    public static class Architecture
    {
        /// <summary>Plans: a one-room cottage, a cottage, a longhouse with its byre, a two-storey house, a hall-house with its cross wing, and the
        /// narrow town house of a divided plot in a town's core - tall, gable to the street (C13.3, D2-118).</summary>
        public const int Small = 0, Cottage = 1, Long = 2, Two = 3, Hall = 4, Row = 5, Plans = 6;
        /// <summary>Roof forms.</summary>
        public const int Gable = 0, Hipped = 1, HalfHip = 2;
        /// <summary>Roof covers.</summary>
        public const int Straw = 0, Reed = 1, Shingle = 2, Tile = 3, Slate = 4;
        /// <summary>Walls.</summary>
        public const int Daub = 0, Limewash = 1, Framed = 2, Logs = 3, Stone = 4;

        // Form: plan bits 0-2, roof form 3-4, cover 5-7, walls 8-10, chimney 11, palette 12-14, chosen 15
        const int Chosen = 1 << 15;
        public static bool Has(int form) => (form & Chosen) != 0;
        public static int PlanOf(int form) => form & 7;
        public static int RoofOf(int form) => (form >> 3) & 3;
        public static int CoverOf(int form) => (form >> 5) & 7;
        public static int WallsOf(int form) => (form >> 8) & 7;
        public static bool Chimney(int form) => (form & (1 << 11)) != 0;
        public static int PaletteOf(int form) => (form >> 12) & 7;

        public static int Pack(int plan, int roof, int cover, int walls, bool chimney, int palette) =>
            Chosen | (plan & 7) | ((roof & 3) << 3) | ((cover & 7) << 5) | ((walls & 7) << 8) | (chimney ? 1 << 11 : 0) | ((palette & 7) << 12);

        /// <summary>
        /// A village's building tradition at its founding (C11's first trait, D2-114): the way its founders carried from the village they came
        /// from - and as a few people carry a way of building somewhere new, now and then they build it a little differently (a founder
        /// effect: one trait, the roof's form or the walls' shade, changes with a chance `Drift`); the first peoples each their own. Packed:
        /// roof form bits 0-1, shade 2-4, its line (the first village it descends from) above. Since C11.1b the way they carry is their own: the
        /// way the party's first household builds (`from`), where it has one; else their village's.
        /// </summary>
        public static int TraditionAt(World w, int sid, int origin, int from = 0)
        {
            ulong h = KeyedRng.Hash(w.Seed, RngStream.Traits, (ulong)sid, 9001);
            if (from != 0 || (origin != 0 && w.Settlements.Exists(origin) && w.Settlements.Get(origin).Tradition != 0))
            {
                int t = from != 0 ? from : w.Settlements.Get(origin).Tradition;
                if (KeyedRng.Unit(w.Seed, RngStream.Traits, (ulong)sid, 9002) >= Drift) return t;
                if ((h & 1UL) == 0) return (t & ~3) | (int)((((ulong)(t & 3)) + 1UL + ((h >> 1) & 1UL)) % 3UL);   // (another roof form)
                return (t & ~(7 << 2)) | (int)((((ulong)((t >> 2) & 7)) + 1UL + ((h >> 1) % 5UL)) % 6UL) << 2;   // (another shade)
            }
            return (int)((h >> 8) % 3UL) | ((int)((h >> 16) % 6UL) << 2) | (sid << 5);
        }

        /// <summary>The chance a founding's builders change one trait of the tradition they bring.</summary>
        public const double Drift = 0.3;

        /// <summary>A tradition's roof form and shade (its people's way of roofing and colouring a house).</summary>
        public static int TraditionRoof(int tradition) => (tradition & 3) % 3;
        public static int TraditionPalette(int tradition) => ((tradition >> 2) & 7) % 6;
        /// <summary>The first village a tradition descends from.</summary>
        public static int Line(int tradition) => tradition >> 5;

        // ---- a people's ways carried and copied (C11.1b)
        /// <summary>`--oldhouseways` (`--oldways` is D2-160's): every house built in its village's way (D2-114) - no household carries one of its own - on the same binary.</summary>
        public static bool WaysOff;
        /// <summary>
        /// At a marriage, the chance the new household builds as its village builds - its first household's way, roof and shade: houses are raised
        /// with the village's hands, from its materials, in its custom, and the young of a household come from elsewhere take it up (`Conform`);
        /// and the chance a bride from another village brings one of her household's ways with her (`BrideCopy`). A first form had the young take one
        /// way of the first household at a time (0.3) and every bride one of hers (0.15): by year 300 only 15 % of households built as their village's
        /// founders and 21 % as its first household, 62 of 63 villages a mosaic - no village had a way of its own (seed 4, `place --ways`).
        /// </summary>
        public const double Conform = 0.6, BrideCopy = 0.1;
        /// <summary>A year's chance the first household of a tributary takes one way of its lord's first household where they differ (the lord's
        /// seat imitated by the households that lead the villages paying it).</summary>
        public const double LordCopy = 0.1;

        /// <summary>The way a household builds in (C11.1b): the one it carries - from the house its people grew up in, the village its founders came
        /// from or the one it came from itself - else (none recorded; `--oldhouseways`) its village's.</summary>
        public static int WayOf(World w, int hid, in Settlement s)
        {
            if (!WaysOff && hid != 0 && w.Households.Exists(hid))
            {
                int way = w.Households.Get(hid).Way;
                if (way != 0) return way;
            }
            return s.Tradition != 0 ? s.Tradition : TraditionAt(w, s.Id, 0);
        }

        /// <summary>A village's way as its works are raised (C11.1b): its first household's, whose lead its people build them under; else its founders'.</summary>
        public static int VillageWay(World w, in Settlement s) => !WaysOff && s.HeadHh != 0 ? WayOf(w, s.HeadHh, in s) : (s.Tradition != 0 ? s.Tradition : TraditionAt(w, s.Id, 0));

        /// <summary>Whether two ways build alike: the same roof form and shade.</summary>
        public static bool Alike(int a, int b) => TraditionRoof(a) == TraditionRoof(b) && TraditionPalette(a) == TraditionPalette(b);

        /// <summary>One way of `from` that `to` builds otherwise, taken by `to` (its roof form or its shade - which, by a keyed draw, when both differ);
        /// the line `to` descends from is kept.</summary>
        public static int Take(int to, int from, ulong h)
        {
            bool roof = TraditionRoof(to) != TraditionRoof(from), shade = TraditionPalette(to) != TraditionPalette(from);
            if (!roof && !shade) return to;
            if (roof && (!shade || (h & 1UL) == 0)) return (to & ~3) | (from & 3);
            return (to & ~(7 << 2)) | (from & (7 << 2));
        }

        /// <summary>A way built with `from`'s roof form and shade, the line `to` descends from kept.</summary>
        public static int Adopt(int to, int from) => (to & ~0x1F) | (from & 0x1F);

        /// <summary>
        /// The way a couple's new household builds in (C11.1b): the way of the house the groom grew up in (they make their home in his village, under
        /// his family's name) - as its village builds, its first household's way, more often than not (`Conform`); and now and then one of the ways
        /// of the bride's own household, where she came from another village (`BrideCopy`).
        /// </summary>
        public static int WayAtMarriage(World w, int sid, int hid, int groomHh, int brideHh)
        {
            if (WaysOff) return 0;
            ref var s = ref w.Settlements.Get(sid);
            int way = WayOf(w, groomHh, in s);
            int head = s.HeadHh;
            if (head != 0 && head != groomHh && w.Households.Exists(head) && KeyedRng.Unit(w.Seed, RngStream.Traits, (ulong)hid, 9101) < Conform)
                way = Adopt(way, WayOf(w, head, in s));
            if (brideHh != 0 && brideHh != groomHh && w.Households.Exists(brideHh) && w.Households.Get(brideHh).Settlement != sid &&
                KeyedRng.Unit(w.Seed, RngStream.Traits, (ulong)hid, 9103) < BrideCopy)
            {
                int bs = w.Households.Get(brideHh).Settlement;
                if (w.Settlements.Exists(bs)) way = Take(way, WayOf(w, brideHh, in w.Settlements.Get(bs)), KeyedRng.Hash(w.Seed, RngStream.Traits, (ulong)hid, 9104));
            }
            return way;
        }

        /// <summary>
        /// A village's year of ways (C11.1b; at the day households weigh leaving): the first household of a tributary may take one way of its lord's
        /// first household where they differ (`LordCopy`) - so over generations the houses of a realm come to be built as its lord's seat builds,
        /// and the realms' lands look different where their peoples' descent alone made them alike.
        /// </summary>
        public static void Year(World w, int sid)
        {
            if (WaysOff) return;
            ref var s = ref w.Settlements.Get(sid);
            if (s.Lord == 0 || s.HeadHh == 0 || !w.Households.Exists(s.HeadHh) || !w.Settlements.Exists(s.Lord)) return;
            ref var l = ref w.Settlements.Get(s.Lord);
            if (l.HeadHh == 0 || !w.Households.Exists(l.HeadHh)) return;
            int mine = WayOf(w, s.HeadHh, in s), theirs = WayOf(w, l.HeadHh, in l);
            if (Alike(mine, theirs)) return;
            if (KeyedRng.Unit(w.Seed, RngStream.Traits, (ulong)s.HeadHh, (ulong)w.Now, 9105) >= LordCopy) return;
            w.Households.Get(s.HeadHh).Way = Take(mine, theirs, KeyedRng.Hash(w.Seed, RngStream.Traits, (ulong)s.HeadHh, (ulong)w.Now, 9106));
        }

        /// <summary>
        /// What a household could put into a house now - a measure for its house, not a wealth anything else reads: its beasts (a head of cattle 1,
        /// the small stock a quarter, four at most), its boat (a plank boat 0.75, a dugout 0.25), the clothes, tools and vessels it holds beyond what
        /// it wears out (inherited, made over: two at most), its working hands beyond the first (0.3 each: the labour to raise and keep a larger
        /// house), and the years of food in its village's granary (one and a half at most: a village with grain in hand frees its people's hours).
        /// Session 9's first measure (beasts, a larder against a month, clothes against people) put nearly every household of a world between 1.0
        /// and 1.5 - a household's larder holds days (the grain is the village's), clothes were capped at its need and herds are rare late - so every
        /// house was the same cottage. Early villages with grain and herds in hand build better than the crowded ones of later centuries. Since C3.3
        /// (D2-126) the grain is the household's own in the village's store, on the same scale (FU against 365 a member: D2-114 read days of a 365-day
        /// year where TC2's year is 48 - kept, so the plans keep their calibration; it rarely passes 0.3).
        /// </summary>
        public static double Means(World w, int hid)
        {
            if (!w.Households.Exists(hid)) return 0;
            ref var hh = ref w.Households.Get(hid);
            var mem = w.Members(hid);
            int n = mem.Count > 0 ? mem.Count : 1;
            double beasts = 0;
            var bl = w.BeastsOf(hid);
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Beasts.Get(bl[i]); if (b.Alive) beasts += b.Kind == Livestock.Cattle ? 1.0 : 0.25; }
            double boat = 0;
            if (hh.Boat != 0 && w.Buildings.Exists(hh.Boat)) boat = Boats.IsPlank(w.Buildings.Get(hh.Boat).Kind) ? 0.75 : 0.25;
            double goods = DMath.Clamp((double)hh.Gear / n - Goods.Target, 0.0, 2.0);
            int hands = 0;
            for (int i = 0; i < mem.Count; i++) { double age = w.People.Get(mem[i]).AgeYears(w.Now); if (age >= 15.0 && age < 60.0) hands++; }
            double labour = DMath.Min(0.9, 0.3 * DMath.Max(0.0, hands - 1.0));
            double store = 0;
            if (!Holdings.Off && !Holdings.MeansOff) store = DMath.Min(1.5, hh.Grain / DMath.Max(1.0, n * w.Config.FoodPerAdultDay * 365.0));   // (its own grain since C3.3, on D2-114's scale)
            else if (w.Settlements.Exists(hh.Settlement))
            {
                ref var s = ref w.Settlements.Get(hh.Settlement);
                store = DMath.Min(1.5, s.Granary / DMath.Max(1.0, s.Population * w.Config.FoodPerAdultDay * 365.0));
            }
            return DMath.Min(4.0, beasts) + boat + goods + labour + store;
        }

        /// <summary>The beasts a household keeps: its cattle and its small stock.</summary>
        static void Beasts(World w, int hid, out int cattle, out int small)
        {
            cattle = 0; small = 0;
            var bl = w.BeastsOf(hid);
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Beasts.Get(bl[i]); if (!b.Alive) continue; if (b.Kind == Livestock.Cattle) cattle++; else small++; }
        }

        /// <summary>The household's place among its village's by means (0 the best off; how many are better off).</summary>
        static int Rank(World w, int sid, int hid, double mine)
        {
            int better = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (hl[i] != hid && Means(w, hl[i]) > mine) better++;
            return better;
        }

        /// <summary>The cover and walls a village's ground and knowledge give a building of its people, and whether it would have a chimney.</summary>
        static void Materials(World w, in Settlement s, double means, bool rich, out int cover, out int walls, out bool chimney)
        {
            var P = w.Planet;
            int cell = s.Cell;
            double tmean = P.TMean[cell];
            bool cold = tmean < 4.0;
            bool rock = w.Learning.Rock(cell), clay = w.Learning.Clay(cell);
            bool pottery = (s.TechKnown & (1 << Knowledge.Tech.Pottery)) != 0;
            double iron = Knowledge.Tech.IronShare(in s), tools = Knowledge.Tech.ToolShare(in s);
            bool stoneAtHand = rock && (s.Quarry != 0 || s.Stone > 20.0);
            // the woods its people fell: conifers in the cold, broadleaf woods elsewhere (as the view's woods are)
            int wc = P.Grid.CellOf(s.LocalToSphere(s.WoodX, s.WoodY, P.Radius));
            double woods = P.Forest[wc];
            bool conifer = P.TMean[wc] < 6.0;
            bool wet = P.Moist[cell] > 0.72 || P.IsStream(cell) || P.LakeDepth[cell] > 0.3f;
            // the roof's cover
            if (pottery && clay && s.Population >= 90 && means >= 1.5) cover = Tile;
            else if (stoneAtHand && cold && means >= 1.5) cover = Slate;
            else if (woods >= 0.25 && iron >= 0.3 && (conifer || means >= 2.0)) cover = Shingle;
            else if (wet) cover = Reed;
            else cover = Straw;
            // the walls
            if (stoneAtHand && rich && s.Population >= 80) walls = Stone;
            else if (conifer && woods >= 0.2) walls = Logs;
            else if (!conifer && tools >= 0.3 && means >= 1.2 && woods >= 0.1) walls = Framed;
            else if (rock && clay) walls = Limewash;
            else walls = Daub;
            chimney = (rock && means >= 1.2 && s.Population >= 60) || walls == Stone;
        }

        /// <summary>
        /// The form of a home a household raises now (Homes.Raised): its plan by its size and means and the village's, its roof by its tradition
        /// (steeper half-hips under snow), its materials by the ground and the village's knowledge, a palette shade of its tradition.
        /// </summary>
        public static int HomeForm(World w, int sid, int hid, int bid, byte kind) => HomeForm(w, sid, hid, bid, kind, out _, out _);

        /// <summary>The same, with the bearing it is laid out facing (see the full form).</summary>
        public static int HomeForm(World w, int sid, int hid, int bid, byte kind, out double face) => HomeForm(w, sid, hid, bid, kind, out face, out _);

        /// <summary>
        /// The same, with the bearing it is laid out facing (C13.3, D2-118): its plan is the one its household would build that fits its lot - on a
        /// divided plot of a town's core a narrow house (`Row`, a one-room cottage for the poorest), elsewhere the wanted plan or the next smaller
        /// that stands clear of its neighbours, the streets, the green and the ring (`Footprints.HomeFits`).
        /// </summary>
        public static int HomeForm(World w, int sid, int hid, int bid, byte kind, out double face, out bool fits)
        {
            fits = true;
            ref var s = ref w.Settlements.Get(sid);
            var mem = w.Members(hid);
            int n = mem.Count;
            double means = Means(w, hid);
            int rank = Rank(w, sid, hid, means);
            bool rich = means >= RichMeans && rank <= System.Math.Max(1, w.HouseholdsOf(sid).Count / 10);
            Materials(w, in s, means, rich, out int cover, out int walls, out bool chimney);
            int tradition = WayOf(w, hid, in s);   // (the household's own way: C11.1b)
            int roof = TraditionRoof(tradition);
            if (w.Planet.TMean[s.Cell] < 2.0 && roof == Hipped) roof = HalfHip;   // (snow slides off the steep gables of the cold)
            int plan;
            if (kind < Construction.House)
            {
                plan = Small;   // (a hut: round, on the ground its lot keeps)
                face = w.Buildings.Exists(bid) ? Footprints.FaceAt(in s, w.Buildings.Get(bid).X, w.Buildings.Get(bid).Y, w.Buildings.Get(bid).Style) : 0.0;
            }
            else
            {
                // up a storey where lots near the centre are dear: inside a standing ring, or in the inner half of a big village's homes
                bool hemmed = s.Wall == 2 && s.WallR > 0f && Inside(in s, w, hid, s.WallR);
                bool inner = s.Population >= 250 && Inside(in s, w, hid, 0.5 * HomesReach(w, sid));
                Beasts(w, hid, out int cattle, out int small);
                bool core = w.Buildings.Exists(bid) && Streets.CorePlot(in s, w.Buildings.Get(bid).X, w.Buildings.Get(bid).Y);
                bool poor = n <= 2 && means < 1.0 || n <= 3 && means < 0.6;
                if (core) plan = poor ? Small : Row;   // (a divided plot in a town's core: a narrow house, built up - C13.3)
                else if (rich && s.Population >= 120 && rank == 0 && HallRoom(w, sid, bid)) plan = Hall;
                else if ((hemmed && s.Population >= 200 || inner) && (means >= 0.5 || n >= 3)) plan = Two;   // (a town's core builds up, all but the poorest)
                else if (cattle >= 1 || small >= 4 || n >= 7) plan = Long;   // (a byre for its beasts under the one roof; a big family's long hall)
                else if (poor) plan = Small;
                else plan = Cottage;
                plan = Fit(w, sid, hid, bid, plan, core, out face, out fits);
                if (plan == Hall || plan == Two || plan == Row) chimney = true;
            }
            int palette = (TraditionPalette(tradition) + (int)(KeyedRng.Mix((ulong)bid * 31UL + 7) % 3UL)) % 8;
            return Pack(plan, roof, cover, walls, chimney, palette);
        }

        /// <summary>
        /// The plan a house is built to on its lot (C13.3): the wanted one if it stands clear there as it would face, else the next smaller of its
        /// kind (a hall a two-storey house, a longhouse a cottage, ...) - on a divided plot only one that keeps within the plot's width. A lot keeps
        /// the ground of a one-room cottage, so that one stands at the least.
        /// </summary>
        static int Fit(World w, int sid, int hid, int bid, int want, bool core, out double face, out bool fits)
        {
            ref var s = ref w.Settlements.Get(sid);
            fits = true;
            if (!w.Buildings.Exists(bid)) { face = 0.0; return want; }
            ref var b = ref w.Buildings.Get(bid);
            double x = b.X, y = b.Y;
            // facing as it would be laid out now (its street or the square); else as it stands (a street laid since may turn a lot the other way)
            for (int turn = 0; turn < 2; turn++)
            {
                if (turn == 1 && b.Faced == 0) break;
                face = turn == 0 ? Footprints.FaceAt(in s, x, y, b.Style) : b.Face;
                for (int i = 0; i < 5; i++)
                {
                    int p = Fallback(want, i);
                    if (p < 0) break;
                    if (core && !Footprints.Narrow(p)) continue;
                    if (Footprints.HomeFits(w, sid, x, y, face, p, bid, hid)) return p;
                }
            }
            // nothing fits (a camp's lot is checked before its home is raised, so this is a house to be rebuilt): it stays as it stands
            fits = false;
            face = b.Faced != 0 ? b.Face : Footprints.FaceAt(in s, x, y, b.Style);
            return b.Kind >= Construction.House && Has(b.Form) ? PlanOf(b.Form) : Small;
        }

        /// <summary>The i-th plan to try for a wanted plan: itself, then smaller ones as its builders would settle for.</summary>
        static int Fallback(int want, int i)
        {
            switch (want)
            {
                case Hall: return i == 0 ? Hall : i == 1 ? Two : i == 2 ? Row : i == 3 ? Cottage : Small;
                case Long: return i == 0 ? Long : i == 1 ? Cottage : i == 2 ? Row : i == 3 ? Small : -1;
                case Two: return i == 0 ? Two : i == 1 ? Row : i == 2 ? Cottage : i == 3 ? Small : -1;
                case Cottage: return i == 0 ? Cottage : i == 1 ? Row : i == 2 ? Small : -1;
                case Row: return i == 0 ? Row : i == 1 ? Small : -1;
                default: return i == 0 ? Small : -1;
            }
        }

        /// <summary>
        /// Whether a village has room for another hall-house: a hall is its leading household's, so a village keeps one lived-in hall, a town one
        /// for each `PeoplePerHall` of its people (the session 9 runs, before: each year's best-off household that rebuilt raised its own hall -
        /// 54 to 129 in a world). Halls standing empty are taken by whoever needs a home and are not counted; `bid` (the hall being rebuilt) is not.
        /// </summary>
        public static bool HallRoom(World w, int sid, int bid)
        {
            int halls = 0, room = System.Math.Max(1, w.Settlements.Get(sid).Population / PeoplePerHall);
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                if (bl[i] == bid) continue;
                ref var b = ref w.Buildings.Get(bl[i]);
                if (b.Kind != Construction.House || b.Household == 0 || !Has(b.Form) || PlanOf(b.Form) != Hall) continue;
                if (++halls >= room) return false;
            }
            return true;
        }

        /// <summary>Means at which a household among its village's best off (the top tenth) builds as the rich do: stone walls, a hall when first.</summary>
        public const double RichMeans = 2.0;

        /// <summary>People a village has for each hall-house it keeps (one at least).</summary>
        public const int PeoplePerHall = 150;

        /// <summary>Whether a household's lot lies within r (m) of the square.</summary>
        static bool Inside(in Settlement s, World w, int hid, double r)
        {
            if (!w.Households.Exists(hid)) return false;
            ref var hh = ref w.Households.Get(hid);
            double dx = (double)hh.HomeX - s.SquareX, dy = (double)hh.HomeY - s.SquareY;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>How far from the square the village's homes reach now (its farthest lived-in home).</summary>
        static double HomesReach(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            double far = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                double dx = (double)hh.HomeX - s.SquareX, dy = (double)hh.HomeY - s.SquareY;
                double d = DMath.Sqrt(dx * dx + dy * dy);
                if (d > far) far = d;
            }
            return far;
        }

        /// <summary>The form of a village work raised now: the cover and walls its people build with (no plan: its kind is its shape).</summary>
        public static int WorkForm(World w, int sid, int bid)
        {
            ref var s = ref w.Settlements.Get(sid);
            Materials(w, in s, 1.5, false, out int cover, out int walls, out _);
            int tradition = VillageWay(w, in s);   // (as its first household builds: C11.1b)
            int palette = (TraditionPalette(tradition) + (int)(KeyedRng.Mix((ulong)bid * 31UL + 7) % 3UL)) % 8;
            return Pack(0, TraditionRoof(tradition), cover, walls, false, palette);
        }

        /// <summary>A plan's size against a cottage's (its labour and timber to raise).</summary>
        public static double Size(int plan) => plan == Small ? 0.6 : plan == Cottage ? 1.0 : plan == Long ? 1.4 : plan == Two ? 1.7 : plan == Row ? 1.6 : 2.5;

        /// <summary>How much better one form is than another (D2-114: a bigger plan, better walls and roof, a chimney); 0 for no form before.</summary>
        public static double Gain(int now, int before)
        {
            if (!Has(before)) return 0;
            return Worth(now) - Worth(before);
        }

        static double Worth(int f)
        {
            int p = PlanOf(f), c = CoverOf(f), wl = WallsOf(f);
            double v = p == Small ? 0 : p == Cottage ? 1.0 : p == Long ? 1.5 : p == Two || p == Row ? 2.5 : 3.5;
            v += c == Straw ? 0 : c == Reed ? 0.2 : c == Shingle ? 0.8 : 1.2;
            v += wl == Daub ? 0 : wl == Limewash ? 0.3 : wl == Logs ? 0.6 : wl == Framed ? 0.8 : 1.5;
            return v + (Chimney(f) ? 0.5 : 0);
        }

        public static string PlanName(int plan) => plan == Small ? "one-room cottage" : plan == Cottage ? "cottage" : plan == Long ? "longhouse with its byre" : plan == Two ? "two-storey house" : plan == Row ? "narrow town house" : "hall-house with a cross wing";
        public static string CoverName(int c) => c == Straw ? "straw thatch" : c == Reed ? "reed thatch" : c == Shingle ? "wooden shingles" : c == Tile ? "fired tiles" : "stone slates";
        public static string WallsName(int c) => c == Daub ? "wattle and daub" : c == Limewash ? "limewashed daub" : c == Framed ? "timber framing" : c == Logs ? "logs" : "stone";
        public static string RoofName(int r) => r == Gable ? "gabled" : r == Hipped ? "hipped" : "half-hipped";

        /// <summary>"two-storey house of timber framing under fired tiles" (the person page's home).</summary>
        public static string Short(int form)
        {
            if (!Has(form)) return "house";
            int p = PlanOf(form);
            string plan = p == Small ? "one-room cottage" : p == Cottage ? "cottage" : p == Long ? "longhouse" : p == Two ? "two-storey house" : p == Row ? "narrow town house" : "hall-house";
            return plan + " of " + WallsName(WallsOf(form)) + " under " + CoverName(CoverOf(form));
        }

        /// <summary>
        /// Why it was built so (the building page): each part's reason is the condition the rule that chose it required when it was raised - the
        /// household and the village then, what the ground, the woods and the village's knowledge gave.
        /// </summary>
        public static string Why(int form, bool home)
        {
            if (!Has(form)) return "";
            var sb = new System.Text.StringBuilder();
            if (home)
            {
                int p = PlanOf(form);
                sb.Append(p == Small ? "one room: a household of few and little in hand" : p == Cottage ? "a cottage of two rooms" : p == Long ? "a longhouse: its beasts stalled at one end under the one roof, or a big household"
                    : p == Two ? "built up a storey: lots near the middle of a town are dear" : p == Row ? "a narrow town house, tall and gable to the street: the plots along a town's streets near its green are divided"
                    : "a hall-house: the house of its village's best-off household").Append("; ");
            }
            int c = CoverOf(form), wl = WallsOf(form);
            sb.Append(c == Straw ? "thatched with straw from the fields" : c == Reed ? "thatched with reed from the wet ground near" : c == Shingle ? "shingles split from the woods with iron tools"
                : c == Tile ? "tiles fired from clay by a village that knew pottery" : "slates split from rock at hand, against the cold").Append("; ");
            sb.Append(wl == Daub ? "walls of wattle and daub" : wl == Limewash ? "daub washed with lime burnt from the rock" : wl == Framed ? "a frame of broadleaf timber cut with metal tools"
                : wl == Logs ? "logs of the conifer woods" : "walls of stone from the village's store");
            if (Chimney(form)) sb.Append("; a stone chimney");
            return sb.ToString();
        }

        /// <summary>"a two-storey house of timber framing under fired tiles, hipped, with a stone chimney" (the pages).</summary>
        public static string Describe(int form, bool home)
        {
            if (!Has(form)) return "";
            string what = home ? "a " + PlanName(PlanOf(form)) + " of " : "built of ";
            return what + WallsName(WallsOf(form)) + " under " + CoverName(CoverOf(form)) + (home ? ", " + RoofName(RoofOf(form)) : "") + (Chimney(form) ? ", with a stone chimney" : "");
        }
    }
}
