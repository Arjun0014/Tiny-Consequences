using System.Globalization;

namespace TC2.Game
{
    /// <summary>
    /// The planet's look from afar (13 §2; session 25): the air's scattering and the clouds' forms, tunables read every frame by
    /// <see cref="Sky"/>, <see cref="PlanetView"/> and <see cref="CloudView"/>. Presentation only. A shot may set any of them for its
    /// view (`ShotRunner`: "orbit~AtmoSun=8~CloudCu=1.2"), so variants are judged side by side in one run.
    /// </summary>
    public static class Look
    {
        // the air: Rayleigh scattering in an exponential atmosphere, its scale height a fraction of the planet's radius (Earth's 8 km of
        // 6,371 is 0.13 %; a miniature's limb must read at the orbit view, so the air is drawn thicker, its colour Earth's)
        public static float AtmoH = 0.016f;      // Rayleigh scale height / radius
        public static float AtmoTop = 7.5f;      // the drawn shell's top, in scale heights
        public static float AtmoTau = 1.0f;      // x the vertical optical depths (0.040, 0.095, 0.230) at 680/550/440 nm (Earth's ~0.046/0.097/0.24)
        public static float AtmoMie = 0.025f;    // the haze's vertical optical depth (grey), its scale height a third of the air's
        public static float AtmoSun = 7.0f;      // the sunlight the air scatters (x the main light's colour)
        public static float AtmoGround = 0.75f;  // how much of the in-scattered light lies over the ground (1 = all of it; the limb keeps its own; session 27: 0.75 - the region view's colours clearer)
        // the clouds: the weather's cover (where it rains today; storms) as decks with ragged edges, the moist climate's air as fields of
        // fair-weather cumulus, both drawn out along the latitude bands' winds (D2-027's cover, its form elaborated)
        public static float CloudDeck = 1.0f;    // x the decks' density where it rains
        public static float CloudCu = 1.6f;      // x the cumulus fields' cover where the air is moist
        public static float CloudStretch = 1.4f; // the forms drawn out east-west, along the bands' winds (1 = round)
        public static float CloudShade = 1.0f;   // their relief from the sun's side (0 = flat)
        public static float CloudTile = 3600f;   // (session 27) the forms' noise tile (m): its large shapes ~ a quarter of it, the cumulus' puffs ~ a thirtieth
        public static float CloudShadow = 0.6f;  // (session 27) the share of the sun a thick cloud takes from the ground, the sea and what stands there
        public static float NightFar = 0.16f;    // (session 27) the night's sky and moon light on the ground seen from orbit (x the near view's)
        // (session 31: the world look) the picture's grade and the land's broad variation; a shot may set them for its view
        public static float GradeSat = 9f;       // saturation over all (was +14: fields read lime)
        public static float GradeContrast = 14f;   // (session 32: a little more life from orbit, s32look1 o4)
        public static float GradeExposure = 0.06f;
        public static float GradeGreen = 0.34f;  // how far greens' saturation is taken down (0.5 none): the living green of leaves, not a screen's
        public static float LandVary = 1f;       // x the land's broad warm and cool drift (0 none)
        public static float StandFar = 8f;       // (session 32: 45 before - s32look2: stands from orbit read as camouflage, at 8 the woods a living green and the boreal snow clean) the metres a pixel spans where the woods' stands and glades begin to give way to their share (gone at x2.4): from orbit they read as camouflage
        public static float StandGrain = 0.35f;  // (session 32: 1 before) x the far woods' stand grain (older darker, younger lighter, ~1.7 km)
        public static float SeaTrue = 1f;        // (session 30) the sea drawn where its true sphere is (0: on its mesh's faces, as before - a shot's "before")

        static readonly float[] Defaults = { AtmoH, AtmoTop, AtmoTau, AtmoMie, AtmoSun, AtmoGround, CloudDeck, CloudCu, CloudStretch, CloudShade, CloudTile, CloudShadow, NightFar, SeaTrue };

        /// <summary>Every tunable back to its default (a shot's next view).</summary>
        public static void Reset()
        {
            AtmoH = Defaults[0]; AtmoTop = Defaults[1]; AtmoTau = Defaults[2]; AtmoMie = Defaults[3]; AtmoSun = Defaults[4]; AtmoGround = Defaults[5];
            CloudDeck = Defaults[6]; CloudCu = Defaults[7]; CloudStretch = Defaults[8]; CloudShade = Defaults[9]; CloudTile = Defaults[10]; CloudShadow = Defaults[11]; NightFar = Defaults[12]; SeaTrue = Defaults[13];
            Sky.ShadowLite = true;   // (session 27: the light shadows are the game's default - shots show what a player sees)
        }

        /// <summary>Sets a tunable by name (a shot's "~Name=value"); false when there is none of that name.</summary>
        public static bool Set(string name, string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return false;
            switch (name)
            {
                case "AtmoH": AtmoH = v; return true;
                case "AtmoTop": AtmoTop = v; return true;
                case "AtmoTau": AtmoTau = v; return true;
                case "AtmoMie": AtmoMie = v; return true;
                case "AtmoSun": AtmoSun = v; return true;
                case "AtmoGround": AtmoGround = v; return true;
                case "CloudDeck": CloudDeck = v; return true;
                case "CloudCu": CloudCu = v; return true;
                case "CloudStretch": CloudStretch = v; return true;
                case "CloudShade": CloudShade = v; return true;
                case "CloudTile": CloudTile = v; return true;
                case "CloudShadow": CloudShadow = v; return true;
                case "NightFar": NightFar = v; return true;
                case "SeaTrue": SeaTrue = v; return true;
                case "GradeSat": GradeSat = v; return true;   // (session 32: the grade and the land judged live in the Web build - bridge "tune")
                case "GradeContrast": GradeContrast = v; return true;
                case "GradeExposure": GradeExposure = v; return true;
                case "GradeGreen": GradeGreen = v; return true;
                case "LandVary": LandVary = v; return true;
                case "StandFar": StandFar = v; return true;
                case "StandGrain": StandGrain = v; return true;
                case "ShadowLite": Sky.ShadowLite = v > 0f; return true;   // (the frame pass's shadow cascades, 14 §7: judged in real shots before chosen)
            }
            return false;
        }

        /// <summary>The air's drawn top above the planet's centre (m), for a ground radius.</summary>
        public static float AirTop(float groundRadius) => groundRadius * (1f + AtmoH * AtmoTop);
    }
}
