using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 27: the planet from orbit) The clouds' forms: a tileable 3-D noise made once at start, four kinds in its channels -
    /// R the large shapes (gradient-noise fbm from 4 cells a tile), G billows (1 - the distance to the nearest of a jittered lattice's points,
    /// three octaves from 4 cells: heaped, cauliflower forms), B single puffs (the distance to the nearest point of an 8-cell lattice) and A the
    /// fine grain (gradient-noise fbm from 8 cells). Sampled on the cloud shell and by the ground's cloud shadows (TC2CloudForm.hlsl), so a
    /// shadow falls where its cloud is. Presentation only: what the weather covers is the world's (<see cref="CloudView"/>), these are its forms.
    /// </summary>
    public static class CloudNoise
    {
        static readonly Vector3[] Grad =
        {
            new Vector3(1, 1, 0), new Vector3(-1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, -1, 0),
            new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1),
            new Vector3(0, 1, 1), new Vector3(0, -1, 1), new Vector3(0, 1, -1), new Vector3(0, -1, -1),
        };

        public static Texture3D Make(int n)
        {
            var rng = new System.Random(20261026);
            var perm = new int[512];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--) { int j = rng.Next(i + 1); int t = perm[i]; perm[i] = perm[j]; perm[j] = t; }
            for (int i = 0; i < 256; i++) perm[256 + i] = perm[i];
            // one jittered point a cell for each lattice the billows and puffs use (4, 8, 16 cells a tile)
            var pts4 = Points(4, rng); var pts8 = Points(8, rng); var pts16 = Points(16, rng); var puff8 = Points(8, rng);
            var px = new Color32[n * n * n];
            float inv = 1f / n;
            for (int z = 0; z < n; z++)
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) * inv, v = (y + 0.5f) * inv, w = (z + 0.5f) * inv;
                        // R: large shapes - fbm of 4 octaves from 4 cells a tile
                        float r = 0f, amp = 0.5f, tot = 0f;
                        for (int o = 0, f = 4; o < 4; o++, f *= 2) { r += amp * Perlin(u * f, v * f, w * f, f, perm); tot += amp; amp *= 0.5f; }
                        r = Mathf.Clamp01(0.5f + 0.75f * r / tot);
                        // G: billows - inverted cellular distance, three octaves
                        float g = 0.55f * (1f - Worley(u, v, w, 4, pts4)) + 0.30f * (1f - Worley(u, v, w, 8, pts8)) + 0.15f * (1f - Worley(u, v, w, 16, pts16));
                        g = Mathf.Clamp01((g - 0.25f) / 0.70f);
                        // B: puffs - the distance to the nearest point of an 8-cell lattice (0 at a puff's heart)
                        float b = Mathf.Clamp01(Worley(u, v, w, 8, puff8));
                        // A: the fine grain - fbm of 3 octaves from 8 cells
                        float a = 0f; amp = 0.5f; tot = 0f;
                        for (int o = 0, f = 8; o < 3; o++, f *= 2) { a += amp * Perlin(u * f, v * f, w * f, f, perm); tot += amp; amp *= 0.5f; }
                        a = Mathf.Clamp01(0.5f + 0.75f * a / tot);
                        px[(z * n + y) * n + x] = new Color32((byte)(r * 255f + 0.5f), (byte)(g * 255f + 0.5f), (byte)(b * 255f + 0.5f), (byte)(a * 255f + 0.5f));
                    }
            var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "tc2-cloudnoise" };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static Vector3[] Points(int cells, System.Random rng)
        {
            var p = new Vector3[cells * cells * cells];
            for (int k = 0; k < p.Length; k++) p[k] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
            return p;
        }

        /// <summary>The distance (in cell widths, ~0..1) to the nearest point of a jittered lattice of <paramref name="cells"/> a tile, wrapping.</summary>
        static float Worley(float u, float v, float w, int cells, Vector3[] pts)
        {
            float fx = u * cells, fy = v * cells, fz = w * cells;
            int cx = (int)fx, cy = (int)fy, cz = (int)fz;
            float best = 9f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int gx = cx + dx, gy = cy + dy, gz = cz + dz;
                        int ix = (gx % cells + cells) % cells, iy = (gy % cells + cells) % cells, iz = (gz % cells + cells) % cells;
                        var pt = pts[(iz * cells + iy) * cells + ix];
                        float ex = gx + pt.x - fx, ey = gy + pt.y - fy, ez = gz + pt.z - fz;
                        float d = ex * ex + ey * ey + ez * ez;
                        if (d < best) best = d;
                    }
            return Mathf.Sqrt(best);
        }

        /// <summary>Gradient noise (Perlin 2002) of period <paramref name="period"/> cells in each axis (~-1..1).</summary>
        static float Perlin(float x, float y, float z, int period, int[] perm)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float xf = x - xi, yf = y - yi, zf = z - zi;
            float u = Fade(xf), v = Fade(yf), w = Fade(zf);
            int x0 = Wrap(xi, period), x1 = Wrap(xi + 1, period), y0 = Wrap(yi, period), y1 = Wrap(yi + 1, period), z0 = Wrap(zi, period), z1 = Wrap(zi + 1, period);
            float n000 = Dot(Hash(x0, y0, z0, perm), xf, yf, zf), n100 = Dot(Hash(x1, y0, z0, perm), xf - 1, yf, zf);
            float n010 = Dot(Hash(x0, y1, z0, perm), xf, yf - 1, zf), n110 = Dot(Hash(x1, y1, z0, perm), xf - 1, yf - 1, zf);
            float n001 = Dot(Hash(x0, y0, z1, perm), xf, yf, zf - 1), n101 = Dot(Hash(x1, y0, z1, perm), xf - 1, yf, zf - 1);
            float n011 = Dot(Hash(x0, y1, z1, perm), xf, yf - 1, zf - 1), n111 = Dot(Hash(x1, y1, z1, perm), xf - 1, yf - 1, zf - 1);
            float a = Mathf.Lerp(Mathf.Lerp(n000, n100, u), Mathf.Lerp(n010, n110, u), v);
            float b = Mathf.Lerp(Mathf.Lerp(n001, n101, u), Mathf.Lerp(n011, n111, u), v);
            return Mathf.Lerp(a, b, w);
        }

        static int Wrap(int i, int p) => ((i % p) + p) % p;
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static int Hash(int x, int y, int z, int[] perm) => perm[perm[perm[x & 255] + (y & 255)] + (z & 255)] % 12;
        static float Dot(int g, float x, float y, float z) => Grad[g].x * x + Grad[g].y * y + Grad[g].z * z;
    }
}
