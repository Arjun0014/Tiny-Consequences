namespace TC2.Core.Foundation
{
    /// <summary>
    /// World time is a count of whole seconds since world creation (long). These are the fixed calendar
    /// laws (01_WORLD_LAWS.md §1, D2-004): 24-hour days, 12-day seasons, 48-day years.
    /// Only integer arithmetic here: calendar results are identical on every runtime.
    /// </summary>
    public static class SimTime
    {
        public const long Second = 1;
        public const long Minute = 60;
        public const long Hour = 3600;
        public const long Day = 86400;
        public const int DaysPerSeason = 12;
        public const int SeasonsPerYear = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear; // 48
        public const long Season = Day * DaysPerSeason;
        public const long Year = Day * DaysPerYear; // 4,147,200 s

        public static long Years(double y) => (long)System.Math.Floor(y * Year + 0.5);
        public static long Days(double d) => (long)System.Math.Floor(d * Day + 0.5);
        public static long Hours(double h) => (long)System.Math.Floor(h * Hour + 0.5);
        public static long Minutes(double m) => (long)System.Math.Floor(m * Minute + 0.5);

        /// <summary>Floor division that is correct for negative values too.</summary>
        public static long FloorDiv(long a, long b) { long q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q--; return q; }
        public static long FloorMod(long a, long b) { long m = a % b; if (m != 0 && ((m < 0) != (b < 0))) m += b; return m; }

        public static long YearOf(long t) => FloorDiv(t, Year);
        public static int DayOfYear(long t) => (int)(FloorMod(t, Year) / Day);          // 0..47
        public static int SeasonOf(long t) => DayOfYear(t) / DaysPerSeason;             // 0 spring .. 3 winter (north)
        public static int DayOfSeason(long t) => DayOfYear(t) % DaysPerSeason;          // 0..11
        public static long SecondOfDay(long t) => FloorMod(t, Day);                     // 0..86399
        public static int HourOfDay(long t) => (int)(SecondOfDay(t) / Hour);
        public static int MinuteOfHour(long t) => (int)(FloorMod(t, Hour) / Minute);
        public static long StartOfDay(long t) => t - SecondOfDay(t);
        public static long StartOfYear(long t) => YearOf(t) * Year;
        /// <summary>The next time strictly after t whose second-of-day equals sod.</summary>
        public static long NextTimeOfDay(long t, long sod)
        {
            long start = StartOfDay(t) + sod;
            return start > t ? start : start + Day;
        }

        /// <summary>Fraction of the year in [0,1) with integer-exact numerator.</summary>
        public static double YearFraction(long t) => (double)FloorMod(t, Year) / Year;

        static readonly string[] SeasonNames = { "Spring", "Summer", "Autumn", "Winter" };
        static readonly string[] PartNames = { "Early", "Mid", "Late" };

        public static string SeasonName(int season) => SeasonNames[((season % 4) + 4) % 4];

        /// <summary>"Year 183 · Late Summer · Day 31 · 14:05" (day of year is 1-based for people).</summary>
        public static string Format(long t)
        {
            long y = YearOf(t);
            int doy = DayOfYear(t);
            int season = doy / DaysPerSeason;
            int part = (doy % DaysPerSeason) / 4; // 4 days each: early, mid, late
            int h = HourOfDay(t), m = MinuteOfHour(t);
            return "Year " + y + " · " + PartNames[part] + " " + SeasonNames[season] + " · Day " + (doy + 1) + " · " + Two(h) + ":" + Two(m);
        }

        /// <summary>"Year 183, late summer" - a date as a reader of the History Book wants it (D2-077).</summary>
        public static string FormatDate(long t)
        {
            int doy = DayOfYear(t);
            return "Year " + YearOf(t) + ", " + PartNames[(doy % DaysPerSeason) / 4].ToLowerInvariant() + " " + SeasonNames[doy / DaysPerSeason].ToLowerInvariant();
        }

        public static string FormatShort(long t)
        {
            long y = YearOf(t);
            int doy = DayOfYear(t);
            return "Y" + y + " d" + (doy + 1) + " " + Two(HourOfDay(t)) + ":" + Two(MinuteOfHour(t));
        }

        public static string FormatDuration(long s)
        {
            if (s < 0) return "-" + FormatDuration(-s);
            if (s >= Year) return (s / (double)Year).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " y";
            if (s >= Day) return (s / (double)Day).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " d";
            if (s >= Hour) return (s / (double)Hour).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " h";
            if (s >= Minute) return (s / (double)Minute).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " min";
            return s + " s";
        }

        static string Two(int v) => v < 10 ? "0" + v : v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
