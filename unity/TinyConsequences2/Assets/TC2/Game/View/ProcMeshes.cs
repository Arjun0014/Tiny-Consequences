using System.Collections.Generic;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>Procedural placeholder meshes (F0): people, trees, houses, stores, fields, fire. Vertex colours tint parts.</summary>
    public static class ProcMeshes
    {
        sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();
            /// <summary>A far form (W-FOREST): crowns of fewer, plain lumps - the silhouette without the detail.</summary>
            public bool Far;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                for (int k = 0; k < 4; k++) { N.Add(n); C.Add(col); }
                T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            /// <summary>A colour darkened for a face's own shading, its alpha kept: the alpha marks what the draw tints (`Color * k` scaled it too -
            /// conifers' needles, at alpha 0.7-1, were drawn half untinted: pale grey spires; the cones' undersides at 0.5 counted as windows).</summary>
            public static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                for (int k = 0; k < 3; k++) { N.Add(n); C.Add(col); }
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public void Box(Vector3 min, Vector3 max, Color col)
            {
                Vector3 a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z), c = new Vector3(max.x, min.y, max.z), d = new Vector3(min.x, min.y, max.z);
                Vector3 e = new Vector3(min.x, max.y, min.z), f = new Vector3(max.x, max.y, min.z), g = new Vector3(max.x, max.y, max.z), h = new Vector3(min.x, max.y, max.z);
                Quad(e, h, g, f, col); // top
                Quad(a, b, c, d, Dim(col, 0.6f)); // bottom
                Quad(a, e, f, b, Dim(col, 0.9f)); // -z
                Quad(c, g, h, d, Dim(col, 0.9f)); // +z
                Quad(d, h, e, a, Dim(col, 0.8f)); // -x
                Quad(b, f, g, c, Dim(col, 0.8f)); // +x
            }

            /// <summary>A cone/pyramid with <paramref name="sides"/> sides along +Y.</summary>
            public void Cone(float y0, float y1, float r, int sides, Color col)
            {
                Vector3 tip = new Vector3(0, y1, 0);
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                    Tri(p0, tip, p1, Dim(col, 0.85f + 0.15f * Mathf.Cos(a0)));
                    Tri(p1, new Vector3(0, y0, 0), p0, Dim(col, 0.5f));
                }
            }

            public void Prism(float y0, float y1, float r, int sides, Color col)
            {
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                    Vector3 q0 = new Vector3(p0.x, y1, p0.z), q1 = new Vector3(p1.x, y1, p1.z);
                    Quad(p0, q0, q1, p1, Dim(col, 0.8f + 0.2f * Mathf.Cos(a0)));
                    Tri(q0, new Vector3(0, y1, 0), q1, col);
                }
            }

            /// <summary>A quad whose face looks along <paramref name="outward"/> (the winding is chosen to match).</summary>
            public void QuadOut(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color col)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) Quad(d, c, b, a, col); else Quad(a, b, c, d, col);
            }

            /// <summary>A branch: a tapering prism from a to b.</summary>
            public void Stick(Vector3 a, Vector3 b, float r0, float r1, int sides, Color col)
            {
                Vector3 dir = (b - a).normalized;
                Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(dir, u);
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                    Vector3 o0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), o1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                    QuadOut(a + o0 * r0, b + o0 * r1, b + o1 * r1, a + o1 * r0, o0 + o1, col);
                }
            }

            /// <summary>
            /// A lump of leaves (D2-079): an icosphere subdivided once (80 faces), smooth-shaded, its points a little in or out,
            /// squashed; white above and darker below (the draw colours it; the dark is the crown's own shade).
            /// </summary>
            public void Blob(Vector3 c, float r, float squash, float jitter, int seed, bool low = false) => Blob(c, r, squash, jitter, seed, Color.white, low);

            /// <summary>The same lump with its own tint (multiplied into the draw's colour: a crown's lumps a little lighter or darker,
            /// warmer or cooler, D2-084).</summary>
            public void Blob(Vector3 c, float r, float squash, float jitter, int seed, Color tint, bool low)
            {
                low |= Far;
                var rnd = new System.Random(seed);
                int b0 = V.Count;
                int points = low ? 12 : IcoPoints.Length;   // (low: the icosahedron itself, 20 faces)
                var faces = low ? IcoFaces0 : IcoFaces;
                for (int k = 0; k < points; k++)
                {
                    Vector3 d = IcoPoints[k];
                    float s = r * (1f + jitter * ((float)rnd.NextDouble() * 2f - 1f));
                    V.Add(c + new Vector3(d.x * s, d.y * s * squash, d.z * s));
                    N.Add(new Vector3(d.x, d.y / squash, d.z).normalized);
                    float ao = 0.52f + 0.48f * Mathf.Clamp01(d.y * 0.5f + 0.55f);
                    C.Add(new Color(ao * tint.r, ao * tint.g, ao * tint.b, 1f));
                }
                for (int f = 0; f < faces.Length; f += 3) { T.Add(b0 + faces[f]); T.Add(b0 + faces[f + 1]); T.Add(b0 + faces[f + 2]); }
            }

            /// <summary>
            /// A crown of lumps of leaves (D2-084): <paramref name="n"/> lumps spread over an ellipsoid about (0, cy, 0) of radii rx, ry
            /// (a golden-angle spiral from the top down, jittered), each its own size and its own tint - the upper ones lighter, the lower
            /// and inner ones in shade, a few warmer or cooler - so one crown is never one smooth ball.
            /// </summary>
            public void Crown(System.Random rnd, int n, float cy, float rx, float ry, float lumpMin, float lumpMax, float squash, float droop = 0f)
            {
                if (Far) { int m = System.Math.Max(4, (n * 2 + 2) / 3); lumpMin *= (float)n / m * 0.75f; lumpMax *= (float)n / m * 0.75f; lumpMin = System.Math.Min(lumpMin, 0.7f); lumpMax = System.Math.Min(lumpMax, 0.8f); n = m; }
                for (int k = 0; k < n; k++)
                {
                    float u = (k + 0.5f) / n;
                    float yy = 1f - 1.7f * u;
                    float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - yy * yy));
                    float ang = k * 2.39996f + (float)rnd.NextDouble() * 0.9f;
                    float out_ = 0.70f + 0.30f * (float)rnd.NextDouble();
                    var c = new Vector3(Mathf.Cos(ang) * rr * rx * out_, cy + yy * ry - droop * rr * rr, Mathf.Sin(ang) * rr * rx * out_);
                    float s = Mathf.Lerp(lumpMin, lumpMax, (float)rnd.NextDouble());
                    float shade = 0.80f + 0.22f * (float)rnd.NextDouble() + 0.12f * yy;
                    var tint = new Color(shade * (0.96f + 0.08f * (float)rnd.NextDouble()), shade, shade * (0.90f + 0.12f * (float)rnd.NextDouble()), 1f);
                    Blob(c, s, squash, 0.18f, rnd.Next(), tint, s < 0.40f);   // (the small lumps as plain icosahedra: ~300 triangles a crown)
                }
            }

            /// <summary>A triangle facing up (the winding chosen so its normal points up): leaves seen from above.</summary>
            public void TriUp(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                if (Vector3.Cross(b - a, c - a).y < 0f) Tri(a, c, b, col); else Tri(a, b, c, col);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Color Lin(float r, float g, float b) => new Color(r, g, b).linear;

        // an icosahedron subdivided once, its faces wound to look outward (its first twelve points and IcoFaces0: the icosahedron)
        static readonly Vector3[] IcoPoints;
        static readonly int[] IcoFaces, IcoFaces0;

        static ProcMeshes()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var pts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < pts.Count; i++) pts[i] = pts[i].normalized;
            int[] f0 = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                         3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                pts.Add(((pts[a] + pts[b]) * 0.5f).normalized);
                mid[key] = pts.Count - 1;
                return pts.Count - 1;
            }
            var faces = new List<int>();
            void Face(int a, int b, int c)
            {
                // (front faces look outward: the normal the builder computes, cross(b - a, c - a), points away from the centre)
                if (Vector3.Dot(Vector3.Cross(pts[b] - pts[a], pts[c] - pts[a]), pts[a] + pts[b] + pts[c]) < 0) { int x = b; b = c; c = x; }
                faces.Add(a); faces.Add(b); faces.Add(c);
            }
            for (int k = 0; k < f0.Length; k += 3) Face(f0[k], f0[k + 1], f0[k + 2]);
            IcoFaces0 = faces.ToArray();
            faces.Clear();
            for (int k = 0; k < f0.Length; k += 3)
            {
                int a = f0[k], b = f0[k + 1], c = f0[k + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                Face(a, ab, ca); Face(b, bc, ab); Face(c, ca, bc); Face(ab, bc, ca);
            }
            IcoPoints = pts.ToArray();
            IcoFaces = faces.ToArray();
        }

        /// <summary>
        /// A broadleaf of the woods (D2-079) with a crown of radius 1 (the draw scales it to its crown): a trunk and three lumps of
        /// leaves near the camera (~250 triangles), one further off (~90). The leaves are white and take the draw's colour (the
        /// season's); the bark keeps its own.
        /// </summary>
        public static Mesh ForestBroad(int lod)
        {
            var b = new Builder();
            Color bark = Lin(0.34f, 0.25f, 0.17f); bark.a = 0f;
            b.Prism(0f, 1.25f, 0.085f, lod == 0 ? 6 : 4, bark);
            if (lod == 0)
            {
                b.Blob(new Vector3(0f, 1.95f, 0f), 0.80f, 0.82f, 0.16f, 11);
                b.Blob(new Vector3(0.46f, 1.62f, 0.16f), 0.58f, 0.86f, 0.16f, 12);
                b.Blob(new Vector3(-0.38f, 1.70f, -0.28f), 0.60f, 0.86f, 0.16f, 13);
            }
            else b.Blob(new Vector3(0f, 1.85f, 0f), 0.98f, 0.80f, 0.10f, 14);
            return b.Build(lod == 0 ? "tc2-forest-broad" : "tc2-forest-broad-far");
        }

        /// <summary>
        /// The woods' other broadleaves near the camera (D2-081), crowns of radius 1 like <see cref="ForestBroad"/>: 1 a tall, narrow
        /// crown on a longer bole (beech, lime, poplar), 2 a broad, spreading crown low on a stout trunk (oak, chestnut), 3 a birch -
        /// a slender pale trunk under a small loose crown. Leaves white (the draw colours them); bark keeps its own.
        /// </summary>
        public static Mesh ForestBroadKind(int kind)
        {
            var b = new Builder();
            if (kind == 1)
            {
                Color bark = Lin(0.40f, 0.36f, 0.30f); bark.a = 0f;
                b.Prism(0f, 1.6f, 0.075f, 6, bark);
                b.Blob(new Vector3(0f, 2.05f, 0f), 0.66f, 1.05f, 0.14f, 51);
                b.Blob(new Vector3(0.10f, 2.85f, -0.05f), 0.52f, 1.10f, 0.14f, 52);
                b.Blob(new Vector3(-0.30f, 1.80f, 0.22f), 0.42f, 0.95f, 0.14f, 53);
            }
            else if (kind == 2)
            {
                Color bark = Lin(0.30f, 0.23f, 0.16f); bark.a = 0f;
                b.Prism(0f, 0.95f, 0.11f, 6, bark);
                b.Stick(new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 1.30f, 0.10f), 0.07f, 0.04f, 4, bark);
                b.Stick(new Vector3(0f, 0.85f, 0f), new Vector3(-0.40f, 1.25f, -0.18f), 0.07f, 0.04f, 4, bark);
                b.Blob(new Vector3(0f, 1.55f, 0f), 0.70f, 0.62f, 0.16f, 54);
                b.Blob(new Vector3(0.58f, 1.40f, 0.12f), 0.52f, 0.66f, 0.16f, 55);
                b.Blob(new Vector3(-0.52f, 1.38f, -0.20f), 0.54f, 0.66f, 0.16f, 56);
                b.Blob(new Vector3(0.05f, 1.42f, -0.58f), 0.46f, 0.66f, 0.16f, 57);
            }
            else
            {
                Color birch = Lin(0.86f, 0.84f, 0.78f); birch.a = 0f;
                b.Prism(0f, 2.2f, 0.055f, 5, birch);
                b.Blob(new Vector3(0f, 2.55f, 0f), 0.50f, 1.25f, 0.20f, 58);
                b.Blob(new Vector3(0.22f, 2.05f, 0.10f), 0.36f, 1.15f, 0.20f, 59);
            }
            return b.Build("tc2-forest-broad-" + kind);
        }

        /// <summary>The kinds of the woods' trees near the camera (D2-084), each in <see cref="TreeVariants"/> variants.</summary>
        public const int TreeRound = 0, TreeTall = 1, TreeWide = 2, TreeBirch = 3, TreeWillow = 4, TreeSpruce = 5, TreePine = 6, TreeKinds = 18, TreeVariants = 3;
        /// <summary>The forms of W-FOREST (18 §8): firs (a narrow drooping spire), the treeline's krummholz (a low wind-shorn mat), the rain
        /// forest's emergents (a tall pale bole under a broad flat crown) and its canopy trees (a dense dome on a straight bole), palms, umbrella
        /// trees of the dry tropics (a forked trunk under a flat crown), alders of the carr (several stems, a low bushy crown), poplars of the
        /// river galleries (a column), mangroves (stilt roots under a low dense crown) and snags (a dead trunk, bare and broken).</summary>
        public const int TreeFir = 7, TreeKrumm = 8, TreeEmergent = 9, TreeRainCanopy = 10, TreePalm = 11, TreeUmbrella = 12, TreeAlder = 13, TreePoplar = 14, TreeMangrove = 15, TreeSnag = 16, TreeStump = 17;
        /// <summary>Whether a form's leaves are needles (drawn with the conifers' colours and their far form).</summary>
        public static bool Needles(int kind) => kind == TreeSpruce || kind == TreePine || kind == TreeFir || kind == TreeKrumm;

        /// <summary>
        /// A tree of the woods near the camera (D2-084), its crown of radius ~1 (the draw scales it to its crown): round broadleaves
        /// (beech, lime), tall ones (poplar, hornbeam), wide spreading ones low on a stout forked trunk (oak), birches (a slender pale
        /// trunk, a small loose crown), willows and alders by the water (a leaning trunk, a broad drooping crown), spruces (tiers to a
        /// spire) and pines (a tall bare bole under a flat, broken crown). Each kind in a few variants so no two neighbours are the same
        /// tree; every crown is lumps of different sizes and shades, never one smooth ball. Leaves and needles white (the draw colours
        /// them by the season and the land's dryness); bark keeps its own.
        /// </summary>
        public static Mesh ForestTree(int kind, int variant, bool far = false)
        {
            var b = new Builder { Far = far };
            var rnd = new System.Random(7919 * (kind + 1) + 104729 * (variant + 1));
            float J(float lo, float hi) => lo + (hi - lo) * (float)rnd.NextDouble();
            Color bark = Lin(0.34f, 0.25f, 0.17f); bark.a = 0f;
            switch (kind)
            {
                case TreeRound:
                {
                    float tr = J(1.0f, 1.3f);
                    b.Prism(0f, tr, 0.085f, 6, bark);
                    for (int k = 0; k < 2; k++) { float a = J(0f, 6.28f); b.Stick(new Vector3(0f, tr * 0.85f, 0f), new Vector3(Mathf.Cos(a) * 0.45f, tr + 0.45f, Mathf.Sin(a) * 0.45f), 0.05f, 0.025f, 4, bark); }
                    b.Crown(rnd, 7, tr + J(0.65f, 0.8f), J(0.70f, 0.82f), J(0.50f, 0.62f), 0.34f, 0.62f, 0.86f);
                    break;
                }
                case TreeTall:
                {
                    float tr = J(1.4f, 1.8f);
                    Color pale = Lin(0.42f, 0.38f, 0.32f); pale.a = 0f;
                    b.Prism(0f, tr, 0.075f, 6, pale);
                    b.Crown(rnd, 7, tr + J(0.85f, 1.05f), J(0.46f, 0.56f), J(0.95f, 1.15f), 0.30f, 0.50f, 1.08f);
                    break;
                }
                case TreeWide:
                {
                    float tr = J(0.80f, 1.0f);
                    b.Prism(0f, tr, 0.12f, 6, bark);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.09f + J(-0.4f, 0.4f);
                        b.Stick(new Vector3(0f, tr * 0.9f, 0f), new Vector3(Mathf.Cos(a) * 0.55f, tr + J(0.35f, 0.55f), Mathf.Sin(a) * 0.55f), 0.075f, 0.04f, 5, bark);
                    }
                    b.Crown(rnd, 9, tr + J(0.55f, 0.7f), J(0.92f, 1.05f), J(0.36f, 0.46f), 0.32f, 0.56f, 0.70f);
                    break;
                }
                case TreeBirch:
                {
                    Color white = Lin(0.86f, 0.84f, 0.78f); white.a = 0f;
                    float tr = J(1.9f, 2.4f);
                    b.Prism(0f, tr, 0.05f, 5, white);
                    b.Crown(rnd, 6, tr + J(0.35f, 0.5f), J(0.40f, 0.50f), J(0.62f, 0.78f), 0.20f, 0.36f, 1.20f);
                    break;
                }
                case TreeWillow:
                {
                    float tr = J(0.85f, 1.1f), lean = J(0.15f, 0.3f), la = J(0f, 6.28f);
                    var top = new Vector3(Mathf.Cos(la) * lean, tr, Mathf.Sin(la) * lean);
                    b.Stick(Vector3.zero, top, 0.11f, 0.08f, 6, bark);
                    var rc = new System.Random(rnd.Next());
                    var c0 = b.V.Count;
                    b.Crown(rc, 8, tr + 0.5f, J(0.90f, 1.0f), J(0.42f, 0.5f), 0.34f, 0.54f, 0.78f, 0.35f);
                    for (int k = c0; k < b.V.Count; k++) b.V[k] += new Vector3(top.x, 0f, top.z);
                    break;
                }
                case TreeSpruce:
                {
                    b.Prism(0f, 0.7f, 0.075f, 5, bark);
                    int tiers = 4 + (variant % 2);
                    float y = J(0.45f, 0.6f), r = J(0.95f, 1.05f), top = J(4.0f, 4.6f);
                    float step = (top - y) / (tiers + 0.6f);
                    for (int k = 0; k < tiers; k++)
                    {
                        float shade = 0.62f + 0.38f * k / (tiers - 1f) + J(-0.05f, 0.05f);
                        float y1 = y + step * 1.75f;
                        b.Cone(y, k == tiers - 1 ? top : y1, r * J(0.92f, 1.06f), 7, new Color(shade, shade, shade, 1f));
                        y += step; r *= J(0.70f, 0.80f);
                    }
                    break;
                }
                case TreeFir:
                {
                    // a narrow spire of many drooping tiers, darker below (the boreal and montane woods)
                    b.Prism(0f, 0.6f, 0.06f, 5, bark);
                    int tiers = 6 + (variant % 2);
                    float y = J(0.35f, 0.5f), r = J(0.70f, 0.78f), top = J(4.8f, 5.4f);
                    float step = (top - y) / (tiers + 0.4f);
                    for (int k = 0; k < tiers; k++)
                    {
                        float shade = 0.56f + 0.44f * k / (tiers - 1f) + J(-0.05f, 0.05f);
                        b.Cone(y, k == tiers - 1 ? top : y + step * 1.55f, r * J(0.94f, 1.04f), 6, new Color(shade, shade, shade, 1f));
                        y += step; r *= J(0.80f, 0.86f);
                    }
                    break;
                }
                case TreeKrumm:
                {
                    // the treeline's krummholz: a low mat of needles shorn by the wind to one side, a crooked stem
                    float lean = J(0f, 6.28f);
                    var dir = new Vector3(Mathf.Cos(lean), 0f, Mathf.Sin(lean));
                    b.Stick(Vector3.zero, dir * 0.35f + Vector3.up * 0.28f, 0.06f, 0.04f, 4, bark);
                    for (int k = 0; k < 4; k++)
                    {
                        var c = dir * J(-0.2f, 0.75f) + new Vector3(J(-0.35f, 0.35f), J(0.20f, 0.38f), J(-0.35f, 0.35f));
                        float sh = J(0.62f, 0.95f);
                        b.Blob(c, J(0.38f, 0.55f), 0.45f, 0.22f, rnd.Next(), new Color(sh, sh, sh, 1f), false);
                    }
                    break;
                }
                case TreeEmergent:
                {
                    // a rain forest emergent: a tall pale bole on buttresses, limbs spreading at the top under a broad flat crown
                    Color pale = Lin(0.58f, 0.53f, 0.44f); pale.a = 0f;
                    float tr = J(3.4f, 4.0f);
                    b.Prism(0f, tr, 0.075f, 6, pale);
                    for (int k = 0; k < 4; k++) { float a = k * 1.57f + J(-0.3f, 0.3f); b.Stick(new Vector3(Mathf.Cos(a) * 0.26f, 0f, Mathf.Sin(a) * 0.26f), new Vector3(0f, 0.5f, 0f), 0.06f, 0.02f, 3, pale); }
                    for (int k = 0; k < 4; k++) { float a = k * 1.57f + J(-0.4f, 0.4f); b.Stick(new Vector3(0f, tr * 0.9f, 0f), new Vector3(Mathf.Cos(a) * 0.80f, tr + J(0.25f, 0.45f), Mathf.Sin(a) * 0.80f), 0.05f, 0.025f, 4, pale); }
                    b.Crown(rnd, 13, tr + J(0.40f, 0.50f), J(1.40f, 1.60f), J(0.20f, 0.26f), 0.30f, 0.52f, 0.42f);   // (wider and flatter than the canopy below it)
                    break;
                }
                case TreeRainCanopy:
                {
                    // the rain forest's canopy: a dense, many-lumped dome on a straight pale bole
                    Color pale = Lin(0.52f, 0.47f, 0.38f); pale.a = 0f;
                    float tr = J(1.9f, 2.3f);
                    b.Prism(0f, tr, 0.07f, 6, pale);
                    for (int k = 0; k < 2; k++) { float a = J(0f, 6.28f); b.Stick(new Vector3(0f, tr * 0.85f, 0f), new Vector3(Mathf.Cos(a) * 0.5f, tr + 0.4f, Mathf.Sin(a) * 0.5f), 0.045f, 0.022f, 4, pale); }
                    b.Crown(rnd, 14, tr + J(0.45f, 0.55f), J(1.10f, 1.22f), J(0.30f, 0.36f), 0.30f, 0.50f, 0.52f);   // (a broad, flat-topped cauliflower: never a ball)
                    break;
                }
                case TreePalm:
                {
                    // a palm: a slender, curving ringed trunk, a head of long fronds arching out and down
                    Color palmbark = Lin(0.54f, 0.48f, 0.38f); palmbark.a = 0f;
                    float h = J(3.0f, 3.8f), lean = J(0.15f, 0.45f), la = J(0f, 6.28f);
                    var mid = new Vector3(Mathf.Cos(la) * lean * 0.35f, h * 0.5f, Mathf.Sin(la) * lean * 0.35f);
                    var top = new Vector3(Mathf.Cos(la) * lean, h, Mathf.Sin(la) * lean);
                    b.Stick(Vector3.zero, mid, 0.075f, 0.06f, 5, palmbark);
                    b.Stick(mid, top, 0.06f, 0.05f, 5, palmbark);
                    int fronds = 8 + (variant % 2);
                    for (int k = 0; k < fronds; k++)
                    {
                        float a = k * 6.2832f / fronds + J(-0.2f, 0.2f);
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var side = new Vector3(-dir.z, 0f, dir.x) * J(0.16f, 0.22f);
                        float reach = J(1.1f, 1.45f);
                        Vector3 p1 = top + dir * (reach * 0.5f) + Vector3.up * J(0.18f, 0.32f), p2 = top + dir * reach + Vector3.down * J(0.25f, 0.55f);
                        float sh = J(0.75f, 1.0f);
                        var c = new Color(sh, sh, sh, 1f); var c2 = new Color(sh * 0.9f, sh * 0.9f, sh * 0.9f, 1f);
                        b.TriUp(top, p1 + side, p1, c); b.TriUp(top, p1, p1 - side, c);
                        b.TriUp(p1, p1 + side, p2, c2); b.TriUp(p1, p2, p1 - side, c2);
                    }
                    b.Blob(top + Vector3.down * 0.05f, 0.16f, 0.9f, 0.1f, rnd.Next(), new Color(0.7f, 0.7f, 0.7f, 1f), true);
                    break;
                }
                case TreeUmbrella:
                {
                    // an umbrella tree of the dry tropics: a slender trunk forking high into limbs that hold a broad, flat, thin crown
                    float tr = J(1.7f, 2.1f);
                    b.Prism(0f, tr * 0.55f, 0.06f, 5, bark);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.09f + J(-0.5f, 0.5f);
                        b.Stick(new Vector3(0f, tr * 0.5f, 0f), new Vector3(Mathf.Cos(a) * 0.75f, tr + J(0.0f, 0.15f), Mathf.Sin(a) * 0.75f), 0.045f, 0.022f, 4, bark);
                    }
                    b.Crown(rnd, 10, tr + J(0.22f, 0.28f), J(1.30f, 1.45f), J(0.11f, 0.15f), 0.28f, 0.46f, 0.30f);
                    break;
                }
                case TreeAlder:
                {
                    // an alder or willow of the carr: several stems from one stool, a low bushy crown
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.09f + J(-0.4f, 0.4f);
                        b.Stick(new Vector3(Mathf.Cos(a) * 0.08f, 0f, Mathf.Sin(a) * 0.08f), new Vector3(Mathf.Cos(a) * J(0.25f, 0.4f), J(1.0f, 1.3f), Mathf.Sin(a) * J(0.25f, 0.4f)), 0.05f, 0.03f, 4, bark);
                    }
                    b.Crown(rnd, 8, J(1.45f, 1.6f), J(0.85f, 0.95f), J(0.55f, 0.65f), 0.32f, 0.5f, 0.85f);
                    break;
                }
                case TreePoplar:
                {
                    // a poplar of the river's gallery: a narrow column of leaves on a pale trunk
                    Color pale = Lin(0.48f, 0.44f, 0.38f); pale.a = 0f;
                    float tr = J(0.8f, 1.0f);
                    b.Prism(0f, tr + 1.0f, 0.07f, 5, pale);
                    b.Crown(rnd, 8, tr + J(1.55f, 1.75f), J(0.36f, 0.42f), J(1.35f, 1.55f), 0.26f, 0.42f, 1.25f);
                    break;
                }
                case TreeMangrove:
                {
                    // a mangrove of the warm coasts' and swamps' wet ground: arching stilt roots under a low dense crown
                    float tr = J(0.9f, 1.1f);
                    b.Prism(0.35f, tr, 0.07f, 5, bark);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 1.047f + J(-0.3f, 0.3f);
                        b.Stick(new Vector3(0f, 0.6f, 0f), new Vector3(Mathf.Cos(a) * J(0.45f, 0.6f), -0.05f, Mathf.Sin(a) * J(0.45f, 0.6f)), 0.035f, 0.025f, 3, bark);
                    }
                    b.Crown(rnd, 9, tr + J(0.5f, 0.6f), J(0.95f, 1.08f), J(0.42f, 0.5f), 0.32f, 0.52f, 0.7f);
                    break;
                }
                case TreeStump:
                {
                    // a stump (W-FELL): where a tree was felled, a short cut trunk, its sawn face pale, a few shoots from the stool on some (it keeps its
                    // own colours; drawn at the felled crown's radius - a unit here is the crown's)
                    Color face = Lin(0.70f, 0.60f, 0.44f); face.a = 0f;
                    Color shoot = Lin(0.40f, 0.52f, 0.24f); shoot.a = 0f;
                    float h = J(0.08f, 0.14f), r = J(0.085f, 0.11f);
                    b.Prism(0f, h, r, 7, bark);
                    b.Prism(h, h + 0.004f, r * 0.94f, 7, face);
                    if (variant > 0)
                        for (int k = 0; k < 2 + variant; k++)
                        {
                            float a = J(0f, 6.28f);
                            b.Stick(new Vector3(Mathf.Cos(a) * r * 0.8f, h * 0.7f, Mathf.Sin(a) * r * 0.8f), new Vector3(Mathf.Cos(a) * r * 1.6f, h + J(0.10f, 0.22f), Mathf.Sin(a) * r * 1.6f), 0.012f, 0.005f, 3, shoot);
                        }
                    break;
                }
                case TreeSnag:
                {
                    // a snag: a dead trunk standing grey and broken, a few bare stubs (it keeps its own colour)
                    Color dead = Lin(0.60f, 0.57f, 0.52f); dead.a = 0f;
                    float tr = J(2.2f, 3.4f);
                    b.Prism(0f, tr, 0.075f, 5, dead);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = J(0f, 6.28f), y = tr * J(0.45f, 0.85f);
                        b.Stick(new Vector3(0f, y, 0f), new Vector3(Mathf.Cos(a) * J(0.3f, 0.55f), y + J(0.1f, 0.35f), Mathf.Sin(a) * J(0.3f, 0.55f)), 0.035f, 0.012f, 3, dead);
                    }
                    break;
                }
                default:   // TreePine
                {
                    float tr = J(2.4f, 3.0f);
                    Color pinebark = Lin(0.45f, 0.30f, 0.20f); pinebark.a = 0f;
                    b.Prism(0f, tr, 0.07f, 5, pinebark);
                    b.Stick(new Vector3(0f, tr * 0.8f, 0f), new Vector3(J(-0.4f, 0.4f), tr + 0.35f, J(-0.4f, 0.4f)), 0.045f, 0.02f, 4, pinebark);
                    b.Crown(rnd, 5, tr + 0.55f, J(0.65f, 0.80f), J(0.28f, 0.36f), 0.30f, 0.48f, 0.55f);
                    break;
                }
            }
            return b.Build("tc2-tree-" + kind + "-" + variant + (far ? "-far" : ""));
        }

        /// <summary>A broadleaf in winter (D2-079): trunk, boughs and twigs, bare (it takes the draw's colour: bark, or snow on it).</summary>
        public static Mesh ForestBare()
        {
            var b = new Builder();
            var col = new Color(0.92f, 0.92f, 0.92f, 1f);
            b.Prism(0f, 1.3f, 0.085f, 5, col);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.047f + 0.35f * (k % 2);
                var from = new Vector3(0f, 1.0f + 0.07f * k, 0f);
                var mid = new Vector3(Mathf.Cos(a) * 0.55f, 1.75f + 0.1f * (k % 3), Mathf.Sin(a) * 0.55f);
                var tip = new Vector3(Mathf.Cos(a + 0.25f) * 0.95f, 2.35f + 0.15f * (k % 2), Mathf.Sin(a + 0.25f) * 0.95f);
                b.Stick(from, mid, 0.05f, 0.032f, 4, col);
                b.Stick(mid, tip, 0.03f, 0.012f, 3, Builder.Dim(col, 0.95f));
            }
            b.Stick(new Vector3(0f, 1.25f, 0f), new Vector3(0.05f, 2.6f, -0.04f), 0.06f, 0.015f, 4, col);
            return b.Build("tc2-forest-bare");
        }

        /// <summary>A conifer of the woods (D2-079) with a crown of radius 1 at its foot: tiers of needles near the camera, one
        /// cone further off. The needles are white and take the draw's colour.</summary>
        public static Mesh ForestConifer(int lod)
        {
            var b = new Builder();
            Color bark = Lin(0.32f, 0.23f, 0.15f); bark.a = 0f;
            b.Prism(0f, 0.8f, 0.08f, lod == 0 ? 5 : 4, bark);
            if (lod == 0)
            {
                b.Cone(0.55f, 2.45f, 1.00f, 7, new Color(0.62f, 0.62f, 0.62f, 1f));
                b.Cone(1.45f, 3.35f, 0.78f, 7, new Color(0.80f, 0.80f, 0.80f, 1f));
                b.Cone(2.35f, 4.25f, 0.52f, 7, Color.white);
            }
            else b.Cone(0.55f, 4.25f, 1.0f, 6, new Color(0.78f, 0.78f, 0.78f, 1f));
            return b.Build(lod == 0 ? "tc2-forest-conifer" : "tc2-forest-conifer-far");
        }

        /// <summary>A shrub (D2-081): three low lumps of leaves, ~1 m tall and ~1 m round at scale 1 (60 triangles); takes the draw's colour.</summary>
        public static Mesh Shrub()
        {
            var b = new Builder();
            b.Blob(new Vector3(0f, 0.48f, 0f), 0.60f, 0.78f, 0.20f, 21, true);
            b.Blob(new Vector3(0.46f, 0.32f, 0.20f), 0.44f, 0.78f, 0.20f, 22, true);
            b.Blob(new Vector3(-0.40f, 0.34f, -0.22f), 0.47f, 0.78f, 0.20f, 23, true);
            return b.Build("tc2-shrub");
        }

        /// <summary>A fern (D2-081): seven fronds arching out from the crown, ~0.5 m tall and ~1 m round at scale 1; takes the draw's colour.</summary>
        public static Mesh Fern()
        {
            var b = new Builder();
            for (int k = 0; k < 7; k++)
            {
                float a = k * 0.8976f + 0.25f * (k % 2);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x) * 0.13f;
                float reach = 0.85f + 0.15f * ((k * 5) % 3) / 2f;
                Vector3 root = Vector3.up * 0.04f, mid = dir * (0.45f * reach) + Vector3.up * 0.44f, tip = dir * reach + Vector3.up * 0.16f;
                Color c = new Color(0.86f + 0.02f * (k % 3), 0.86f + 0.02f * (k % 3), 0.86f, 1f);
                b.TriUp(root, mid + side, mid, c * 0.85f); b.TriUp(root, mid, mid - side, c * 0.85f);
                b.TriUp(mid, mid + side, tip, c); b.TriUp(mid, tip, mid - side, c);
            }
            return b.Build("tc2-fern");
        }

        /// <summary>A low cushion of heather, crowberry or moss campion (D2-081): two flattened lumps a third of a metre high; takes the draw's colour.</summary>
        public static Mesh Cushion()
        {
            var b = new Builder();
            b.Blob(new Vector3(0f, 0.14f, 0f), 0.62f, 0.42f, 0.25f, 31, true);
            b.Blob(new Vector3(0.42f, 0.10f, 0.25f), 0.40f, 0.45f, 0.25f, 32, true);
            return b.Build("tc2-cushion");
        }

        /// <summary>A stone (D2-081): a rough, half-sunk lump half a metre round at scale 1; takes the draw's colour.</summary>
        public static Mesh Stone()
        {
            var b = new Builder();
            b.Blob(new Vector3(0f, 0.08f, 0f), 0.5f, 0.55f, 0.32f, 41, true);
            return b.Build("tc2-stone");
        }

        /// <summary>
        /// A woodpile (D2-081): split logs stacked in three courses, 1.8 m long and ~1 m high, the cut ends pale; its draw scales it
        /// to the wood the village holds.
        /// </summary>
        public static Mesh Woodpile()
        {
            var b = new Builder();
            Color bark = Lin(0.36f, 0.27f, 0.19f), ends = Lin(0.74f, 0.62f, 0.44f);
            bark.a = 0f; ends.a = 0f;
            for (int row = 0; row < 3; row++)
            {
                int logs = 4 - row;
                for (int k = 0; k < logs; k++)
                {
                    float x = (k - (logs - 1) * 0.5f) * 0.36f, y = 0.17f + row * 0.29f;
                    float r = 0.16f + 0.015f * ((k + row) % 3);
                    var a = new Vector3(x, y, -0.9f + 0.04f * ((k * 7 + row) % 3));
                    var e = new Vector3(x, y, 0.9f - 0.04f * ((k * 5 + row) % 3));
                    b.Stick(a, e, r, r, 6, bark);
                    // the cut ends
                    for (int s = 0; s < 6; s++)
                    {
                        float a0 = s * Mathf.PI / 3f, a1 = (s + 1) * Mathf.PI / 3f;
                        var o0 = new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f);
                        var o1 = new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
                        b.Tri(a, a + o1, a + o0, ends);
                        b.Tri(e, e + o0, e + o1, ends);
                    }
                }
            }
            return b.Build("tc2-woodpile");
        }

        /// <summary>
        /// A threshing floor in use (C3.7b): the sheaves spread on the beaten floor inside the threshers' ring (1.6 m), loose straw over it, a few
        /// sheaves still bound lying ears inward, and the threshed grain swept into a small heap in the middle - drawn only while people thresh there.
        /// </summary>
        public static Mesh ThreshingFloor()
        {
            var b = new Builder();
            Color straw = Lin(0.80f, 0.68f, 0.40f), loose = Lin(0.86f, 0.76f, 0.47f), ears = Lin(0.70f, 0.56f, 0.28f), grain = Lin(0.62f, 0.48f, 0.26f), band = Lin(0.45f, 0.36f, 0.20f);
            straw.a = 0f; loose.a = 0f; ears.a = 0f; grain.a = 0f; band.a = 0f;
            b.Prism(0f, 0.05f, 1.35f, 11, straw);
            b.Prism(0.05f, 0.08f, 0.95f, 9, loose);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.047f + 0.3f * (k & 1), c = Mathf.Cos(a), s = Mathf.Sin(a);
                var foot = new Vector3(c * 1.25f, 0.12f, s * 1.25f);
                var head = new Vector3(c * 0.45f, 0.13f, s * 0.45f);
                b.Stick(foot, head, 0.11f, 0.14f, 6, straw);                       // (a sheaf, its ears inward)
                b.Stick(head, head + (head - foot).normalized * 0.22f, 0.15f, 0.10f, 6, ears);
                var mid = Vector3.Lerp(foot, head, 0.45f);
                b.Stick(mid - (head - foot).normalized * 0.03f, mid + (head - foot).normalized * 0.03f, 0.125f, 0.125f, 6, band);   // (its band)
            }
            b.Cone(0.08f, 0.30f, 0.32f, 8, grain);   // (the threshed grain swept up)
            return b.Build("tc2-threshing-floor");
        }

        /// <summary>A person 1.7 m tall: body and head (colour comes from the draw).</summary>
        public static Mesh Person()
        {
            var b = new Builder();
            b.Prism(0f, 1.15f, 0.22f, 6, Color.white);
            b.Prism(1.15f, 1.45f, 0.14f, 6, Lin(0.95f, 0.80f, 0.68f));
            b.Cone(1.45f, 1.72f, 0.16f, 6, Lin(0.95f, 0.80f, 0.68f));
            return b.Build("tc2-person");
        }

        public static Mesh Conifer()
        {
            var b = new Builder();
            b.Prism(0f, 2.0f, 0.25f, 5, Lin(0.40f, 0.28f, 0.18f));
            b.Cone(1.5f, 7.0f, 2.2f, 7, Color.white);
            b.Cone(4.0f, 10.0f, 1.6f, 7, Color.white * 0.92f);
            return b.Build("tc2-conifer");
        }

        public static Mesh Broadleaf()
        {
            var b = new Builder();
            b.Prism(0f, 3.0f, 0.3f, 5, Lin(0.42f, 0.30f, 0.20f));
            b.Cone(2.2f, 5.0f, 3.0f, 8, Color.white);
            b.Cone(5.0f, 3.6f, 3.0f, 8, Color.white * 0.85f); // underside of the crown (inverted)
            b.Cone(4.2f, 8.0f, 2.2f, 8, Color.white * 0.95f);
            return b.Build("tc2-broadleaf");
        }

        public static Mesh House()
        {
            var b = new Builder();
            b.Box(new Vector3(-3f, 0f, -2.2f), new Vector3(3f, 2.6f, 2.2f), Lin(0.86f, 0.78f, 0.64f));
            Color roof = Lin(0.55f, 0.30f, 0.20f);
            Vector3 l0 = new Vector3(-3.4f, 2.5f, -2.6f), l1 = new Vector3(3.4f, 2.5f, -2.6f), r0 = new Vector3(-3.4f, 2.5f, 2.6f), r1 = new Vector3(3.4f, 2.5f, 2.6f);
            Vector3 ridge0 = new Vector3(-3.4f, 4.4f, 0f), ridge1 = new Vector3(3.4f, 4.4f, 0f);
            b.Quad(l0, ridge0, ridge1, l1, roof);
            b.Quad(r1, ridge1, ridge0, r0, roof * 0.85f);
            b.Tri(l0, r0, ridge0, Lin(0.80f, 0.72f, 0.58f));
            b.Tri(r1, l1, ridge1, Lin(0.80f, 0.72f, 0.58f));
            b.Box(new Vector3(-0.5f, 0f, -2.25f), new Vector3(0.5f, 1.8f, -2.15f), Lin(0.35f, 0.22f, 0.14f));
            return b.Build("tc2-house");
        }

        /// <summary>
        /// A stretch of palisade 2 m long (D2-074): seven sharpened stakes set close along X over 2.1 m (the stretches, set every 2 m round the ring,
        /// overlap on its curve), two rails binding them on the inner face. Drawn in a ring round a walled village, the instance colour greying the timber
        /// as it rots. (Session 27: five posts in 1.86 m of darker wood had read from the village view as a row of separate dark slabs - s27d4.)
        /// </summary>
        public static Mesh Palisade()
        {
            var b = new Builder();
            Color wood = Lin(0.66f, 0.54f, 0.40f), tip = Lin(0.58f, 0.47f, 0.34f), rail = Lin(0.52f, 0.42f, 0.30f);
            for (int k = 0; k < 7; k++)
            {
                float x = -0.9f + 0.3f * k, h = 2.15f + 0.11f * ((k * 7) % 4), r = 0.14f;   // (stakes of a little different height, nearly touching)
                b.Box(new Vector3(x - r, 0f, -r), new Vector3(x + r, h, r), wood * (0.94f + 0.03f * ((k * 5) % 3)));
                Vector3 apex = new Vector3(x, h + 0.38f, 0f);
                Vector3 p0 = new Vector3(x - r, h, -r), p1 = new Vector3(x + r, h, -r), p2 = new Vector3(x + r, h, r), p3 = new Vector3(x - r, h, r);
                b.Tri(p0, apex, p1, tip); b.Tri(p1, apex, p2, tip * 0.85f); b.Tri(p2, apex, p3, tip); b.Tri(p3, apex, p0, tip * 0.85f);
            }
            // two rails on the inner face (+z faces the village's square)
            b.Box(new Vector3(-1.05f, 0.55f, 0.14f), new Vector3(1.05f, 0.70f, 0.24f), rail);
            b.Box(new Vector3(-1.05f, 1.55f, 0.14f), new Vector3(1.05f, 1.70f, 0.24f), rail);
            return b.Build("tc2-palisade");
        }

        /// <summary>
        /// A stretch of dry-stone curtain 2 m long (C7.2): rubble walling ~1.9 m high and a metre thick on a broader footing course, a darker coping, and
        /// two merlons on its outer face (-z: the stretch's +z faces the village's square, as a palisade's does) - drawn in a ring where its people have laid
        /// their palisade in stone, the old stone rings inside a grown town too.
        /// </summary>
        public static Mesh StoneCurtain()
        {
            var b = new Builder();
            Color stone = Lin(0.60f, 0.58f, 0.54f), foot = Lin(0.48f, 0.46f, 0.43f), cope = Lin(0.68f, 0.66f, 0.62f);
            b.Box(new Vector3(-1.04f, 0f, -0.62f), new Vector3(1.04f, 0.45f, 0.62f), foot);
            b.Box(new Vector3(-1.04f, 0.45f, -0.5f), new Vector3(1.04f, 1.85f, 0.5f), stone);
            b.Box(new Vector3(-1.04f, 1.85f, -0.56f), new Vector3(1.04f, 2.0f, 0.56f), cope);
            b.Box(new Vector3(-0.85f, 2.0f, -0.56f), new Vector3(-0.25f, 2.55f, -0.18f), stone * 0.95f);
            b.Box(new Vector3(0.25f, 2.0f, -0.56f), new Vector3(0.85f, 2.55f, -0.18f), stone * 0.95f);
            return b.Build("tc2-stone-curtain");
        }

        /// <summary>
        /// A wall tower of dry stone (C7.2): a square tower 3 m a side and ~4.6 m high, crenellated, its door at the foot of its inner face - drawn either side
        /// of a stone ring's gates and along a curtain every ~45 m.
        /// </summary>
        public static Mesh WallTower()
        {
            var b = new Builder();
            Color stone = Lin(0.58f, 0.56f, 0.52f), foot = Lin(0.47f, 0.45f, 0.42f), cope = Lin(0.67f, 0.65f, 0.61f);
            b.Box(new Vector3(-1.7f, 0f, -1.7f), new Vector3(1.7f, 0.6f, 1.7f), foot);
            b.Box(new Vector3(-1.5f, 0.6f, -1.5f), new Vector3(1.5f, 4.2f, 1.5f), stone);
            b.Box(new Vector3(-1.6f, 4.2f, -1.6f), new Vector3(1.6f, 4.4f, 1.6f), cope);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                b.Box(new Vector3(sx > 0 ? 0.85f : -1.6f, 4.4f, sz > 0 ? 0.85f : -1.6f), new Vector3(sx > 0 ? 1.6f : -0.85f, 5.0f, sz > 0 ? 1.6f : -0.85f), stone * 0.95f);
            }
            b.Box(new Vector3(-0.45f, 0.6f, 1.5f), new Vector3(0.45f, 2.3f, 1.56f), Lin(0.30f, 0.22f, 0.15f));
            return b.Build("tc2-wall-tower");
        }

        public static Mesh Granary()
        {
            var b = new Builder();
            b.Prism(0f, 3.5f, 3.0f, 10, Lin(0.75f, 0.62f, 0.42f));
            b.Cone(3.5f, 6.5f, 3.5f, 10, Lin(0.62f, 0.48f, 0.26f));
            return b.Build("tc2-granary");
        }

        /// <summary>A flat field patch 1 x 1 m (scaled per settlement), slightly raised.</summary>
        public static Mesh Field()
        {
            var b = new Builder();
            b.Quad(new Vector3(-0.5f, 0.15f, -0.5f), new Vector3(-0.5f, 0.15f, 0.5f), new Vector3(0.5f, 0.15f, 0.5f), new Vector3(0.5f, 0.15f, -0.5f), Color.white);
            return b.Build("tc2-field");
        }

        public static Mesh Flame()
        {
            var b = new Builder();
            b.Cone(0f, 1f, 0.45f, 5, Color.white);
            return b.Build("tc2-flame");
        }

        public static Mesh Marker()
        {
            var b = new Builder();
            b.Prism(0f, 1f, 0.5f, 6, Color.white);
            return b.Build("tc2-marker");
        }

        /// <summary>A sphere (UV) for the sea and the atmosphere.</summary>
        public static Mesh Sphere(float radius, int lat, int lon)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = 2 * Mathf.PI * j / lon;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(d * radius); n.Add(d);
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j, b2 = a + lon + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b2); t.Add(b2); t.Add(a + 1); t.Add(b2 + 1);
                }
            var m = new Mesh { name = "tc2-sphere", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
