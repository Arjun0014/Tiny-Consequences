using TC2.Core.Foundation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The household's own work and warmth (D2-056; 07 §3, the Household and Body generators). Food is made ready before it is
    /// eaten: grain ground on a quern and cooked, meat and fish cooked - about an hour of a person's work for a day's food. Water
    /// is carried from the well or the stream. The hearth burns the village's wood for cooking and against the cold, and a roof
    /// and a fire stand between the weather and the body: a cold home takes health, most from infants and the old. One set of
    /// laws for ACTIVITY (people's steps) and COARSE (shares of a day). A wood unit is a load of ~25 kg, cut and carried in
    /// about an hour (Construction's units).
    /// </summary>
    public static class Hearth
    {
        /// <summary>FU made ready in an hour by a grown person (a saddle quern grinds ~1 kg of meal an hour, then the cooking); a child of 8-11 half.</summary>
        public const double PrepRate = 1.0;
        /// <summary>Wood units it takes to cook a FU of food (~1.2 kg of wood).</summary>
        public const double CookFuel = 0.05;
        /// <summary>Wood units a hearth burns in a day for each degree of warmth it gives the home (~1 kg).</summary>
        public const double WarmFuel = 0.04;
        /// <summary>The warmth a household keeps its home at when it can (C).</summary>
        public const double Comfort = 14.0;
        /// <summary>Indoors (C) below which the cold takes health, by the degree.</summary>
        public const double ChillLine = 8.0;
        /// <summary>Health a day for each degree indoors below the chill line, for a grown body.</summary>
        public const double ChillPerDegree = 0.012;
        /// <summary>Litres a trip to the well: a grown person's pot, a child's (6-11).</summary>
        public const double CarryAdult = 15.0, CarryChild = 6.0;
        /// <summary>Hours a trip to the well takes in a COARSE day (the walk both ways and the filling, as a lived trip).</summary>
        public const double TripHours = 0.2;

        /// <summary>Litres a day a body uses at home: drink, cooking and washing.</summary>
        public static double WaterNeed(double age) => age < 2 ? 1.5 : age < 12 ? 5.0 : 8.0;

        public static double Carry(double age) => age < 12 ? CarryChild : CarryAdult;

        /// <summary>Degrees a shelter keeps over the open air: a hide tent, a hut of wattle and thatch, a timber house.</summary>
        public static double Roof(byte shelter) => shelter >= Construction.House ? 9.0 : shelter == Construction.Hut ? 7.0 : 4.0;

        /// <summary>Degrees a well-fed hearth can add (a camp's fire burns in the open and gives less).</summary>
        public static double FireMax(byte shelter) => shelter == Construction.Camp ? 8.0 : 14.0;

        /// <summary>Degrees of fire a household wants on a day whose mean outdoor temperature is tOut.</summary>
        public static double Wanted(double tOut, byte shelter) => DMath.Clamp(Comfort - (tOut + Roof(shelter)), 0.0, FireMax(shelter));

        public static double Indoors(double tOut, byte shelter, double fire) => tOut + Roof(shelter) + fire;

        /// <summary>How much more the cold takes from a body by its age: infants most, then the old and small children.</summary>
        public static double Frailty(double age) => age < 2 ? 2.5 : age < 5 ? 1.5 : age > 65 ? 1.8 : 1.0;

        /// <summary>Health the day's cold takes from a body at home (0 at or above the chill line, which rises as clothes wear: D2-058).</summary>
        public static double Chill(double indoors, double age, double clothed = 1.0)
        {
            double line = Goods.ChillLine(clothed);
            return indoors >= line ? 0.0 : (line - indoors) * ChillPerDegree * Frailty(age);
        }

        /// <summary>FU an hour of preparing makes, by the cook's age and health, and half as much with no wood to cook on.</summary>
        public static double PrepPerHour(double age, double health, bool wood) => PrepRate * (age < 12 ? 0.5 : 1.0) * (0.5 + 0.5 * health) * (wood ? 1.0 : 0.5);
    }
}
