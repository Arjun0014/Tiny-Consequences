using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The muck (C3.7c, `26` §2j): beasts make no fertility - they carry the waste's grass onto the fields as dung (D2-139), and in this world they
    /// graze the waste by day and are penned in their households' yards at night, so the night's dung piles up in the yards as muck (with the
    /// bedding: `PerDm` kg of muck for each kg of grass the herds ate) and reaches the fields only when it is carted out and spread: after the
    /// harvest and through the sowing, before the ploughing (`Window`), at a cart's pace or a back's. What is spread gives the soil back what the
    /// herds' dung gave before (`SettlementSystem.SoilYear` reads the grass it stands for); what is never carted gives nothing and leaches away in
    /// its heap (`Leach` a year); the day's dung stays on the waste. One law for lived days, COARSE days and DEEP steps.
    /// </summary>
    public static class Muck
    {
        /// <summary>Tools: the herds' dung reaches the fields as before, carted or not (`--nomuck`: the worlds before C3.7c).</summary>
        public static bool Off;
        /// <summary>The share of the herds' dung dropped in their pens at night (the rest on the waste by day); all of it on a day of hard frost.</summary>
        public const double NightShare = 0.5;
        /// <summary>kg of muck in the yard - dung and the bedding trodden into it - for each kg of grass (dry) the herds ate.</summary>
        public const double PerDm = 3.0;
        /// <summary>The rate a heap's goodness is lost a year while it waits uncarted, leached and rotted down (e-folding: about a quarter gone in a year).</summary>
        public const double Leach = 0.3;
        /// <summary>Hours a tonne of muck takes to load, cart out to the fields and spread: by the back in baskets, with a handcart, with an ox cart.</summary>
        public const double BackHPerT = 5.0, HandHPerT = 1.6, OxHPerT = 0.9;
        /// <summary>Tools (null in the game): kg of muck made in the yards, carted out, leached; hours carting. Never read by the simulation.</summary>
        public static double[] Tally;

        /// <summary>Whether the village carts its muck now: from the harvest's end through the sowing, before the crop is up (its own calendar).</summary>
        public static bool Window(in Settlement s) => !Off && (s.Crop == CropStage.Harvested || s.Crop == CropStage.Sown);

        /// <summary>The herds' night in the yards (Livestock.Day): `grassKg` of grass they ate today (the step's days) adds its muck.</summary>
        public static void Make(ref Settlement s, double grassKg, bool penned)
        {
            if (Off || grassKg <= 0) return;
            double kg = grassKg * PerDm * (penned ? 1.0 : NightShare);
            s.Muck += kg;
            if (Tally != null) Tally[0] += kg;
        }

        /// <summary>A heap's year: what waits uncarted leaches (a step of days).</summary>
        public static void Age(ref Settlement s, int days)
        {
            if (s.Muck <= 0) return;
            double keep = SettlementSystem.Keep(Leach / SimTime.DaysPerYear, days);
            if (Tally != null) Tally[2] += s.Muck * (1.0 - keep);
            s.Muck *= keep;
        }

        /// <summary>Hours a tonne takes for a carter of this household (its cart if it can take one out).</summary>
        public static double HoursPerTonne(World w, int hid)
        {
            byte k = Carts.Usable(w, hid);
            return k == Carts.Ox ? OxHPerT : k == Carts.Hand ? HandHPerT : BackHPerT;
        }

        /// <summary>Carts out `hours` of a carter's work (at `hPerT`, their strength `pace`): muck from the yards onto the fields. Returns the hours used.</summary>
        public static double Cart(World w, ref Settlement s, double hours, double hPerT, double pace)
        {
            if (hours <= 0 || s.Muck <= 1e-6 || hPerT <= 0) return 0.0;
            double kgPerH = 1000.0 / hPerT * pace;
            double kg = DMath.Min(s.Muck, hours * kgPerH);
            double used = kg / kgPerH;
            s.Muck -= kg; s.Spread += kg;
            if (s.Muck < 1e-6) s.Muck = 0;
            w.Metrics.MuckHours += used; w.Metrics.MuckKg += kg;
            if (Tally != null) { Tally[1] += kg; Tally[3] += used; }
            return used;
        }

        /// <summary>The grass (kg, dry) the muck spread on the fields since the harvest stands for: what the herds' dung gave the soil before C3.7c
        /// for the grass they ate (D2-139's `Grazed`) - all of it carted, the same.</summary>
        public static double GrassOf(double spreadKg) => spreadKg / (PerDm * NightShare);

        /// <summary>The fields' muck this season, t a hectare sown (the view darkens the ploughed plots by it).</summary>
        public static double TonnesPerHa(in Settlement s) => s.FieldHa > 0 ? s.Spread / 1000.0 / s.FieldHa : 0.0;
    }
}
