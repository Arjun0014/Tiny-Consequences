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

        // ------------------------------------------------------------------ stands and glades (D2-084)
        /// <summary>The woods' stands and glades: a value noise of the ground's position whose features are some 150-300 m across.</summary>
        public const float StandScale = 210f;
        /// <summary>The noise's cumulative distribution in 256 steps as the shader's texture keeps it (8 bits): its rank of a point is
        /// spread evenly over 0-1, so the stands standing where the share is above the rank cover the share of the ground.</summary>
        public static readonly float[] StandCdf = FitStands();

        static uint Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = ((uint)x * 73856093u) ^ ((uint)y * 19349663u) ^ ((uint)z * 83492791u);
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return h;
            }
        }

        static float Lat(int x, int y, int z) => (Hash(x, y, z) & 0xFFFFFFu) / 16777216f;

        /// <summary>Value noise (0-1) on the integer lattice, smoothly interpolated (the shader's StandV: the same arithmetic).</summary>
        static float ValueNoise(float x, float y, float z)
        {
            float fx = Mathf.Floor(x), fy = Mathf.Floor(y), fz = Mathf.Floor(z);
            int ix = (int)fx + 1024, iy = (int)fy + 1024, iz = (int)fz + 1024;   // (all positive: the casts agree on every runtime)
            float tx = x - fx, ty = y - fy, tz = z - fz;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty); tz = tz * tz * (3f - 2f * tz);
            float a = Lat(ix, iy, iz), b = Lat(ix + 1, iy, iz), c = Lat(ix, iy + 1, iz), d = Lat(ix + 1, iy + 1, iz);
            float e = Lat(ix, iy, iz + 1), f = Lat(ix + 1, iy, iz + 1), g = Lat(ix, iy + 1, iz + 1), h = Lat(ix + 1, iy + 1, iz + 1);
            float ab = a + tx * (b - a), cd = c + tx * (d - c), ef = e + tx * (f - e), gh = g + tx * (h - g);
            float lo = ab + ty * (cd - ab), hi = ef + ty * (gh - ef);
            return lo + tz * (hi - lo);
        }

        /// <summary>The stands' noise at a point of the ground (scene metres, the planet centred at the origin): three octaves.</summary>
        public static float StandNoise(Vector3 p)
        {
            float x = p.x / StandScale, y = p.y / StandScale, z = p.z / StandScale;
            return 0.58f * ValueNoise(x, y, z) + 0.29f * ValueNoise(x * 2.07f + 17.1f, y * 2.07f + 3.3f, z * 2.07f - 9.4f)
                 + 0.13f * ValueNoise(x * 4.31f - 4.7f, y * 4.31f + 12.9f, z * 4.31f + 6.1f);
        }

        static float[] FitStands()
        {
            // the noise's distribution, measured over points spread on a planet's ground (any radius of the scale: the lattice is the
            // same everywhere), kept as the 8-bit texture keeps it
            const int n = 16384;
            var v = new float[n];
            var rng = new System.Random(20261007);
            for (int k = 0; k < n; k++)
            {
                float zc = 2f * (float)rng.NextDouble() - 1f, phi = 6.2831853f * (float)rng.NextDouble(), rr = Mathf.Sqrt(1f - zc * zc);
                v[k] = StandNoise(new Vector3(rr * Mathf.Cos(phi), rr * Mathf.Sin(phi), zc) * 10000f);
            }
            System.Array.Sort(v);
            var cdf = new float[256];
            int at = 0;
            for (int b = 0; b < 256; b++)
            {
                float x = (b + 0.5f) / 256f;   // (the texel's centre)
                while (at < n && v[at] < x) at++;
                cdf[b] = Mathf.Round(at / (float)n * 255f) / 255f;
            }
            return cdf;
        }

        /// <summary>A point's rank in the stands' noise (0-1, spread evenly): the CDF texture read with linear filtering, as the shader.</summary>
        public static float StandRank(Vector3 p)
        {
            float x = StandNoise(p) * 256f - 0.5f;
            if (x <= 0f) return StandCdf[0];
            if (x >= 255f) return StandCdf[255];
            int i = (int)x;
            float t = x - i;
            return StandCdf[i] + t * (StandCdf[i + 1] - StandCdf[i]);
        }

        /// <summary>
        /// The woods' share as stands and glades at a point (D2-084, the shader's rule): where the land is wooded its share is drawn as
        /// stands of closed canopy with edges, with glades and meadows between them - a stand where the share is above the point's rank,
        /// so the stands cover the share of the ground; thin woods (savanna, parkland: a share under ~0.12) stay scattered trees. Near a
        /// river's water (<paramref name="river"/>, its corridor 0-1) the rank is lowered: the woods keep to the banks.
        /// </summary>
        public static float Stands(float share, Vector3 p, float river = 0f)
        {
            float w = SmoothEdge(0.12f, 0.35f, share);
            if (w <= 0f) return share;
            float u = StandRank(p) - 0.30f * river;
            return share + w * (SmoothEdge(u - 0.06f, u + 0.06f, share) - share);
        }

        static float SmoothEdge(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        /// <summary>The stands' CDF as the shader reads it (256 x 1, 8 bits, linear, clamped).</summary>
        public static Texture2D MakeStandCdf()
        {
            var tex = new Texture2D(256, 1, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-stand-cdf" };
            var px = new byte[256];
            for (int b = 0; b < 256; b++) px[b] = (byte)Mathf.RoundToInt(StandCdf[b] * 255f);
            tex.SetPixelData(px, 0);
            tex.Apply(false, true);
            return tex;
        }

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
