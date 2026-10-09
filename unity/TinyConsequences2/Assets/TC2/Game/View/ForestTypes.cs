using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// What kind of wood a point of the woods is (W-FOREST, 18 §8): read from the simulation's own climate and water - the cell's annual mean
    /// temperature at the point's height (the lapse rate), its moisture, its soil water and standing water, a stream's corridor - and nothing
    /// else. Presentation only: it chooses the trees' forms, their make-up and spacing, their heights and colours; the woods' share and where
    /// they stand stay the simulation's (PlanetState.Forest).
    /// Measured first (session 16, `place --woods`, five worlds at year 300): the wooded land is mostly hot (22-30 degC mean: 40-75 % of wooded
    /// cells) or cool (2-10 degC: 25-55 %), little between; moisture 0.3-0.9; true drylands are rare; wet ground is common (8-35 %).
    /// </summary>
    public static class ForestTypes
    {
        /// <summary>Tropical rain forest, seasonally dry tropical woodland, warm temperate evergreen, temperate broadleaf, cool mixed,
        /// boreal, the treeline's krummholz, wet carr or swamp, a river's gallery.</summary>
        public const int Rain = 0, DryTropic = 1, WarmTemperate = 2, Broadleaf = 3, Mixed = 4, Boreal = 5, Treeline = 6, Wet = 7, Gallery = 8, Count = 9;

        public static readonly string[] Names = { "rain", "dry", "warm", "broadleaf", "mixed", "boreal", "treeline", "wet", "gallery" };

        /// <summary>The air's mean temperature at a point: the cell's annual mean less the lapse rate over the point's height above the cell's
        /// own (metres of the simulation's ground; the drawn relief is exaggerated by Ground.VScale).</summary>
        public static float MeanTempAt(float cellMean, float heightOverCell) => cellMean - 0.0065f * heightOverCell;

        /// <summary>
        /// The kind of wood: a stream's corridor or wet ground first (where the climate allows trees), then by the warmth and the moisture.
        /// `riverNear` 0-1 (the terrain's corridor value), `wet` 0-1 (soil water, standing water), `moist` the climate's moisture index.
        /// </summary>
        public static int Classify(float tMean, float moist, float riverNear, float wet)
        {
            if (tMean < -1.5f) return Treeline;
            if (riverNear > 0.55f && tMean > 1f) return Gallery;
            if (wet > 0.8f && tMean > 0f) return Wet;
            if (tMean >= 20f) return moist >= 0.55f ? Rain : DryTropic;
            if (tMean >= 14f) return moist < 0.40f ? DryTropic : WarmTemperate;
            if (tMean >= 8f) return Broadleaf;
            if (tMean >= 3f) return Mixed;
            return Boreal;
        }

        /// <summary>The kind of wood at a cell's own height (camera views, tools).</summary>
        public static int OfCell(TC2.Core.Planet.PlanetState P, int c)
        {
            float wet = Wetness(P, c);
            float river = P.IsStream(c) ? 1f : 0f;
            return Classify(P.TMean[c], P.Moist[c], river, wet);
        }

        /// <summary>How wet a cell's ground lies (0-1): its soil water, and water standing over it.</summary>
        public static float Wetness(TC2.Core.Planet.PlanetState P, int c) => Mathf.Clamp01(Mathf.Max(P.SoilWater[c], P.LakeDepth[c] > 0f ? 1f : 0f));

        /// <summary>A wood's openness: the share of its standing crowns that are left out (open dry woodland, the treeline's scattered krummholz,
        /// a carr's thickets with pools between), 0 for a closed wood.</summary>
        public static float Openness(int type) => type == DryTropic ? 0.45f : type == Treeline ? 0.55f : type == Wet ? 0.20f : 0f;

        /// <summary>The tallest trees against the woods' common height (rain forest emergents tower; the treeline's are knee-high to man-high).</summary>
        public static float Stature(int type) => type == Rain ? 1.45f : type == DryTropic ? 0.85f : type == Treeline ? 0.32f : type == Wet ? 0.80f : type == Gallery ? 1.15f : type == Boreal ? 1.05f : 1f;
    }
}
