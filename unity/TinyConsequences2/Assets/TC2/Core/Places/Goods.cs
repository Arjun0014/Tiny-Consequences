using TC2.Core.Foundation;

namespace TC2.Core.Places
{
    /// <summary>
    /// What a household makes and mends (D2-058): clothes, tools, vessels, baskets and nets, kept as person-equivalents of goods
    /// (`Household.Gear`: 1 is one person clothed and equipped). Each member wears through their goods in about 300 days of use;
    /// making one person's goods takes ~250 hours of spinning, weaving and sewing, hafting and sharpening, potting and plaiting -
    /// some 0.8 hours a person-day to keep up, as textiles kept farming households busy every day. Worn goods let the cold bite
    /// sooner (the chill line rises as the clothes go) and slow the work of hands (worn tools, torn nets). The materials are taken
    /// as found with the work (fibre, hides, clay, wood); F4's chains will ask for them. Shared by ACTIVITY and COARSE.
    /// </summary>
    public static class Goods
    {
        /// <summary>A person's goods worn through in a day of use.</summary>
        public const double WearPerDay = 1.0 / 300.0;
        /// <summary>Hours of a grown person's work to make one person's goods.</summary>
        public const double MakeHours = 250.0;
        /// <summary>What a household keeps a member in when it can: a change of clothes, a spare blade.</summary>
        public const double Target = 1.2;
        /// <summary>Degrees the chill line rises for a household with nothing left to wear.</summary>
        public const double RaggedChill = 6.0;

        /// <summary>How well its members are clothed and equipped (0 ragged, 1 fully).</summary>
        public static double Condition(double gear, int members) => members > 0 ? DMath.Clamp01(gear / members) : 1.0;

        /// <summary>The pace of hands with the household's tools (worn tools, torn nets: down to 0.7).</summary>
        public static double WorkFactor(double condition) => 0.7 + 0.3 * condition;

        /// <summary>Indoors (C) below which the cold takes health, for a household clothed this well.</summary>
        public static double ChillLine(double condition) => Hearth.ChillLine + RaggedChill * (1.0 - condition);

        /// <summary>Materials one person's goods take (D2-063): kg of fibre, a hide counting as two.</summary>
        public const double MaterialPerGood = 2.5;
        /// <summary>The pace of making with nothing in store to make it from (bark, grass, reeds: improvised).</summary>
        public const double Improvised = 0.4;

        /// <summary>
        /// Goods made draw their materials from the village's store - fibre first, then hides; what the store cannot give is made
        /// from what is found at hand, at the improvised pace. Returns the goods made after that (D2-063).
        /// </summary>
        public static double Materials(ref Settlement s, double made)
        {
            if (made <= 0) return made;
            double need = made * MaterialPerGood;
            double have = s.Fibre + 2.0 * s.Hides;
            if (have >= need)
            {
                double f = DMath.Min(need, s.Fibre); s.Fibre -= f; need -= f;
                if (need > 0) s.Hides = DMath.Max(0.0, s.Hides - need / 2.0);
                return made;
            }
            double share = have / need;
            s.Fibre = 0; s.Hides = 0;
            return made * (share + (1.0 - share) * Improvised);
        }

        /// <summary>Person-equivalents of goods an hour of making brings (a child of 8-11 half as much).</summary>
        public static double MakePerHour(double age, double health) => (age < 12 ? 0.5 : 1.0) * (0.5 + 0.5 * health) / MakeHours;
    }
}
