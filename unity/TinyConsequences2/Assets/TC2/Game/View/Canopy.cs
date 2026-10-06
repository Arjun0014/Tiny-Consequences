using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The woods' crowns (D2-079, D2-081): one layout of tree crowns shared by the terrain shader's canopy and the trees drawn near
    /// the camera, so that a tree stands where the canopy shows its crown. Crowns lie on a jittered grid, 24 a side over a tile of
    /// 156 m of ground, each with its radius (most of middle size, a few old and broad) and its number; a crown stands where its
    /// number is below the threshold of the forest share there (then the standing crowns cover that share of the ground), and is a
    /// conifer where its kind number is below the conifers' share. The numbers are plain chance: the tile repeats, so nothing in it
    /// may make a pattern - the woods' clumps, glades and stands come from the share itself, which varies without repeating
    /// (TerrainLod.Colourise). Presentation only: the forest share is the simulation's (PlanetState.Forest).
    /// </summary>
    public static class Canopy
    {
        public const int Side = 24;
        public const float TileMetres = 156f;
        public const float Spacing = TileMetres / Side;
        /// <summary>Per crown (row-major, Side x Side): centre within the tile (in crown spacings), radius (spacings), number.</summary>
        public static readonly float[] X = new float[Side * Side], Y = new float[Side * Side], Radius = new float[Side * Side], Num = new float[Side * Side];
        /// <summary>The crown thresholds whose standing crowns the cover texture keeps in its four channels (the shader's own).</summary>
        public static readonly float[] CoverAt = { 0.18f, 0.38f, 0.62f, 1.0f };
        /// <summary>The share of the tile the standing crowns cover at each of those thresholds (the cover texture's mean).</summary>
        public static readonly Vector4 CoverMean;
        /// <summary>The share-to-threshold factor: a share s stands the crowns numbered below -ln(1 - s) x K (fitted to the layout).</summary>
        public static readonly float K;

        static Canopy()
        {
            var rng = new System.Random(20261006);
            for (int j = 0; j < Side; j++)
                for (int i = 0; i < Side; i++)
                {
                    int k = j * Side + i;
                    X[k] = i + 0.1f + 0.8f * (float)rng.NextDouble();
                    Y[k] = j + 0.1f + 0.8f * (float)rng.NextDouble();
                    Radius[k] = 0.52f + 0.42f * Mathf.Pow((float)rng.NextDouble(), 1.6f);
                    // (kept as the texture keeps it, so the trees and the canopy decide alike)
                    Num[k] = Mathf.Round((0.004f + 0.992f * (float)rng.NextDouble()) * 255f) / 255f;
                }
            K = Fit(96, out CoverMean);
        }

        /// <summary>The number of the lowest-numbered crown covering a point of the tile (u, v in crown spacings), 1 if none.</summary>
        static float Lowest(float u, float v, out float dist)
        {
            int n = Side;
            int ci = (int)u, cj = (int)v;
            float d1 = 1f, n1 = 1f;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int gi = (ci + di + n) % n, gj = (cj + dj + n) % n;
                    int k = gj * n + gi;
                    float ox = X[k] + (ci + di - gi), oy = Y[k] + (cj + dj - gj);
                    float d = Mathf.Sqrt((u - ox) * (u - ox) + (v - oy) * (v - oy)) / Radius[k];
                    if (d >= 1f) continue;
                    if (Num[k] < n1) { d1 = d; n1 = Num[k]; }
                }
            dist = d1;
            return n1;
        }

        /// <summary>
        /// The factor K for which the crowns standing at a share s cover s of the ground: the share covered at each threshold is
        /// measured on the layout, and K is chosen so that half the ground is covered at s = 0.5. Also the share covered at the cover
        /// texture's thresholds (its mean, which the far woods use once the tile itself would read as a pattern).
        /// </summary>
        static float Fit(int res, out Vector4 mean)
        {
            var lowest = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    lowest[y * res + x] = Lowest((x + 0.5f) * Side / res, (y + 0.5f) * Side / res, out _);
            float Covered(float t) { int c = 0; for (int q = 0; q < lowest.Length; q++) if (lowest[q] < t) c++; return c / (float)lowest.Length; }
            mean = new Vector4(Covered(CoverAt[0]), Covered(CoverAt[1]), Covered(CoverAt[2]), Covered(CoverAt[3]));
            // the threshold covering half the ground, by bisection
            float lo = 0f, hi = 1f;
            for (int it = 0; it < 24; it++) { float mid = 0.5f * (lo + hi); if (Covered(mid) < 0.5f) lo = mid; else hi = mid; }
            return 0.5f * (lo + hi) / Mathf.Log(2f);
        }

        /// <summary>The crown numbers that stand at a forest share (the shader's rule): their crowns then cover that share.</summary>
        public static float Threshold(float share) => Mathf.Clamp01(-Mathf.Log(1f - Mathf.Min(share, 0.97f)) * K);

        /// <summary>A crown is a conifer where its kind number is below the conifers' share there.</summary>
        public static bool Conifer(float num, float coniferShare) => Frac(num * 13.73f) < coniferShare;

        /// <summary>A crown's height against the tallest (0.64-1).</summary>
        public static float Top(float num) => 0.64f + 0.36f * Frac(num * 7.31f);

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>
        /// The canopy texture: each texel keeps the two crowns of lowest number that cover it - r, b its distance from their
        /// centres over their radius, g, a their numbers - so the shader can stand any crown whose number is below the share
        /// and draw it whole (the taller of the two that stand).
        /// </summary>
        public static Texture2D MakeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "tc2-canopy" };
            int n = Side;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) * n / size, v = (y + 0.5f) * n / size;
                    int ci = (int)u, cj = (int)v;
                    float d1 = 1f, n1 = 1f, d2 = 1f, n2 = 1f;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int gi = (ci + di + n) % n, gj = (cj + dj + n) % n;
                            int k = gj * n + gi;
                            float ox = X[k] + (ci + di - gi), oy = Y[k] + (cj + dj - gj);
                            float d = Mathf.Sqrt((u - ox) * (u - ox) + (v - oy) * (v - oy)) / Radius[k];
                            if (d >= 1f) continue;
                            if (Num[k] < n1) { d2 = d1; n2 = n1; d1 = d; n1 = Num[k]; }
                            else if (Num[k] < n2) { d2 = d; n2 = Num[k]; }
                        }
                    px[y * size + x] = new Color32((byte)Mathf.RoundToInt(d1 * 255f), (byte)Mathf.RoundToInt(n1 * 255f), (byte)Mathf.RoundToInt(d2 * 255f), (byte)Mathf.RoundToInt(n2 * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>
        /// The cover texture (D2-081): whether a standing crown covers each texel at the four thresholds of <see cref="CoverAt"/>
        /// (one per channel). Its mips are the share of ground the standing crowns cover round a point, so the far woods - where a
        /// crown is smaller than a few pixels - are drawn as the average of the near canopy: the same clumps and glades, no shimmer.
        /// </summary>
        public static Texture2D MakeCover(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "tc2-canopy-cover" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n1 = Lowest((x + 0.5f) * Side / size, (y + 0.5f) * Side / size, out _);
                    px[y * size + x] = new Color32((byte)(n1 < CoverAt[0] ? 255 : 0), (byte)(n1 < CoverAt[1] ? 255 : 0), (byte)(n1 < CoverAt[2] ? 255 : 0), (byte)(n1 < CoverAt[3] ? 255 : 0));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
