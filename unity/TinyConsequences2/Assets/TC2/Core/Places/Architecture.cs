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
        /// <summary>Plans: a one-room cottage, a cottage, a longhouse with its byre, a two-storey house, a hall-house with its cross wing.</summary>
        public const int Small = 0, Cottage = 1, Long = 2, Two = 3, Hall = 4, Plans = 5;
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
        /// roof form bits 0-1, shade 2-4, its line (the first village it descends from) above.
        /// </summary>
        public static int TraditionAt(World w, int sid, int origin)
        {
            ulong h = KeyedRng.Hash(w.Seed, RngStream.Traits, (ulong)sid, 9001);
            if (origin != 0 && w.Settlements.Exists(origin) && w.Settlements.Get(origin).Tradition != 0)
            {
                int t = w.Settlements.Get(origin).Tradition;
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

        /// <summary>
        /// What a household's means are now: its beasts (a head of cattle 1, the small stock a quarter), its larder against a month of its need,
        /// its clothes and tools against its people - a measure for its house, not a wealth anything else reads.
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
            double larder = hh.Larder / DMath.Max(1.0, n * w.Config.FoodPerAdultDay * 30.0);
            return beasts + DMath.Min(2.0, larder) + DMath.Min(1.0, (double)hh.Gear / n);
        }

        /// <summary>The cattle a household keeps (a longhouse's byre is for them).</summary>
        static int Cattle(World w, int hid)
        {
            int c = 0;
            var bl = w.BeastsOf(hid);
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Beasts.Get(bl[i]); if (b.Alive && b.Kind == Livestock.Cattle) c++; }
            return c;
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
        public static int HomeForm(World w, int sid, int hid, int bid, byte kind)
        {
            ref var s = ref w.Settlements.Get(sid);
            var mem = w.Members(hid);
            int n = mem.Count;
            double means = Means(w, hid);
            int rank = Rank(w, sid, hid, means);
            bool rich = means >= 3.0 && rank <= System.Math.Max(1, w.HouseholdsOf(sid).Count / 10);
            Materials(w, in s, means, rich, out int cover, out int walls, out bool chimney);
            int tradition = s.Tradition != 0 ? s.Tradition : TraditionAt(w, sid, 0);
            int roof = TraditionRoof(tradition);
            if (w.Planet.TMean[s.Cell] < 2.0 && roof == Hipped) roof = HalfHip;   // (snow slides off the steep gables of the cold)
            int plan;
            if (kind < Construction.House) plan = Small;
            else
            {
                // up a storey where lots near the centre are dear: inside a standing ring, or in the inner half of a big village's homes
                bool hemmed = s.Wall == 2 && s.WallR > 0f && Inside(in s, w, hid, s.WallR);
                bool inner = s.Population >= 250 && Inside(in s, w, hid, 0.5 * HomesReach(w, sid));
                if (rich && s.Population >= 120 && rank == 0 && HallRoom(w, sid, bid)) plan = Hall;
                else if ((hemmed && s.Population >= 150 || inner) && means >= 1.5) plan = Two;
                else if (Cattle(w, hid) >= 1 || n >= 8) plan = Long;
                else if (n <= 3 && means < 1.0) plan = Small;
                else plan = Cottage;
                if (plan == Hall || plan == Two) chimney = true;
            }
            int palette = (TraditionPalette(tradition) + (int)(KeyedRng.Mix((ulong)bid * 31UL + 7) % 3UL)) % 8;
            return Pack(plan, roof, cover, walls, chimney, palette);
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
            int tradition = s.Tradition != 0 ? s.Tradition : TraditionAt(w, sid, 0);
            int palette = (TraditionPalette(tradition) + (int)(KeyedRng.Mix((ulong)bid * 31UL + 7) % 3UL)) % 8;
            return Pack(0, TraditionRoof(tradition), cover, walls, false, palette);
        }

        /// <summary>A plan's size against a cottage's (its labour and timber to raise).</summary>
        public static double Size(int plan) => plan == Small ? 0.6 : plan == Cottage ? 1.0 : plan == Long ? 1.4 : plan == Two ? 1.7 : 2.5;

        /// <summary>How much better one form is than another (D2-114: a bigger plan, better walls and roof, a chimney); 0 for no form before.</summary>
        public static double Gain(int now, int before)
        {
            if (!Has(before)) return 0;
            return Worth(now) - Worth(before);
        }

        static double Worth(int f)
        {
            int p = PlanOf(f), c = CoverOf(f), wl = WallsOf(f);
            double v = p == Small ? 0 : p == Cottage ? 1.0 : p == Long ? 1.5 : p == Two ? 2.5 : 3.5;
            v += c == Straw ? 0 : c == Reed ? 0.2 : c == Shingle ? 0.8 : 1.2;
            v += wl == Daub ? 0 : wl == Limewash ? 0.3 : wl == Logs ? 0.6 : wl == Framed ? 0.8 : 1.5;
            return v + (Chimney(f) ? 0.5 : 0);
        }

        public static string PlanName(int plan) => plan == Small ? "one-room cottage" : plan == Cottage ? "cottage" : plan == Long ? "longhouse with its byre" : plan == Two ? "two-storey house" : "hall-house with a cross wing";
        public static string CoverName(int c) => c == Straw ? "straw thatch" : c == Reed ? "reed thatch" : c == Shingle ? "wooden shingles" : c == Tile ? "fired tiles" : "stone slates";
        public static string WallsName(int c) => c == Daub ? "wattle and daub" : c == Limewash ? "limewashed daub" : c == Framed ? "timber framing" : c == Logs ? "logs" : "stone";
        public static string RoofName(int r) => r == Gable ? "gabled" : r == Hipped ? "hipped" : "half-hipped";

        /// <summary>"a two-storey house of timber framing under fired tiles, hipped, with a stone chimney" (the pages).</summary>
        public static string Describe(int form, bool home)
        {
            if (!Has(form)) return "";
            string what = home ? "a " + PlanName(PlanOf(form)) + " of " : "built of ";
            return what + WallsName(WallsOf(form)) + " under " + CoverName(CoverOf(form)) + (home ? ", " + RoofName(RoofOf(form)) : "") + (Chimney(form) ? ", with a stone chimney" : "");
        }
    }
}
