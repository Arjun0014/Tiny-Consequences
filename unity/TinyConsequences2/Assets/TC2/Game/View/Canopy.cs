using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The woods' crowns (D2-079): one layout of tree crowns shared by the terrain shader's canopy and the trees drawn near the
    /// camera, so that a tree stands where the canopy shows its crown. Crowns lie on a jittered grid, 24 a side over a tile of
    /// 156 m of ground, each with its radius and its number; a crown stands where its number is below the threshold of the
    /// simulation's forest share there (then the standing discs cover that share of the ground), and is a conifer where its kind
    /// number is below the conifers' share. Presentation only: the forest share is the simulation's (PlanetState.Forest).
    /// </summary>
    public static class Canopy
    {
        public const int Side = 24;
        public const float TileMetres = 156f;
        public const float Spacing = TileMetres / Side;
        /// <summary>Per crown (row-major, Side x Side): centre within the tile (in crown spacings), radius (spacings), number.</summary>
        public static readonly float[] X = new float[Side * Side], Y = new float[Side * Side], Radius = new float[Side * Side], Num = new float[Side * Side];

        static Canopy()
        {
            var rng = new System.Random(20261006);
            for (int j = 0; j < Side; j++)
                for (int i = 0; i < Side; i++)
                {
                    int k = j * Side + i;
                    X[k] = i + 0.1f + 0.8f * (float)rng.NextDouble();
                    Y[k] = j + 0.1f + 0.8f * (float)rng.NextDouble();
                    Radius[k] = 0.60f + 0.22f * (float)rng.NextDouble();
                    // (kept as the texture keeps it, so the trees and the canopy decide alike)
                    Num[k] = Mathf.Round((0.004f + 0.992f * (float)rng.NextDouble()) * 255f) / 255f;
                }
        }

        /// <summary>The crown numbers that stand at a forest share (the shader's rule): their discs then cover that share.</summary>
        public static float Threshold(float share) => Mathf.Clamp01(-Mathf.Log(1f - Mathf.Min(share, 0.97f)) * 0.62f);

        /// <summary>A crown is a conifer where its kind number is below the conifers' share there.</summary>
        public static bool Conifer(float num, float coniferShare) => Frac(num * 13.73f) < coniferShare;

        /// <summary>A crown's height against the tallest (0.72-1).</summary>
        public static float Top(float num) => 0.72f + 0.28f * Frac(num * 7.31f);

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
    }
}
