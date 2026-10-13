using TC2.Core.Foundation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The herds on the stubble and the fallow (C3.7d, `26` §2k): after the harvest the village's reaped fields are thrown open to its beasts until
    /// the sowing ploughs them in - the high stubble, its weeds and the spilt ears (`PerHa` a reaped hectare, eaten down over the stage); the arable it
    /// rests (`Settlement.Rest`, D2-139) is grazed all its year, its sward the field cells' own grass. The herders take the herds onto their own
    /// fields first, the waste after (`Livestock.Day`); what the beasts eat there drops its day's dung on the fields themselves - spread with no
    /// carting (the night's goes to the yards as everywhere: C3.7c). The record's main way of putting dung where the corn would grow, and the autumn's
    /// feed when the waste's grass grows lean.
    /// </summary>
    public static class Stubble
    {
        /// <summary>Tools: the herds keep off the fields as before (`--nostubble`: the worlds before C3.7d), on the same binary.</summary>
        public static bool Off;
        /// <summary>kg (dry) of forage a reaped hectare leaves the herds: the high stubble's weeds and green shoots, the spilt ears (a modern wheat
        /// stubble is grazed of ~70 kg of grain and ~150 kg of leaf a hectare; medieval reaping cut high and left more).</summary>
        public const double PerHa = 300.0;
        /// <summary>The share of what is left on the stubble the herds find in a day (it is eaten down over the stage: the best first).</summary>
        public const double DayShare = 0.25;
        /// <summary>Tools (null in the game): kg eaten on the stubble, on the fallow, day dung dropped on the fields (kg of muck). Never read by the simulation.</summary>
        public static double[] Tally;

        /// <summary>The harvest's end (SettlementSystem): the reaped fields' stubble is the herds' until the sowing.</summary>
        public static void Harvested(ref Settlement s) { if (!Off) s.Stubble = s.FieldHa * PerHa; }

        /// <summary>The sowing's first day ploughs the stubble in.</summary>
        public static void Ploughed(ref Settlement s) { s.Stubble = 0; }

        /// <summary>The herds' day dung dropped on the village's own fields (`grassKg` eaten there over the step): spread with no carting, counted at the
        /// next harvest as the yards' muck is (C3.7c's calibration: the day's share stands for as much as the night's).</summary>
        public static void Dung(ref Settlement s, double grassKg)
        {
            if (Off || grassKg <= 0) return;
            double kg = grassKg * Muck.PerDm * (1.0 - Muck.NightShare);
            if (!Muck.Off) s.Spread += kg;
            if (Tally != null) Tally[2] += kg;
        }
    }
}
