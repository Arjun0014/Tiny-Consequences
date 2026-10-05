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

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                for (int k = 0; k < 4; k++) { N.Add(n); C.Add(col); }
                T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

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
                Quad(a, b, c, d, col * 0.6f); // bottom
                Quad(a, e, f, b, col * 0.9f); // -z
                Quad(c, g, h, d, col * 0.9f); // +z
                Quad(d, h, e, a, col * 0.8f); // -x
                Quad(b, f, g, c, col * 0.8f); // +x
            }

            /// <summary>A cone/pyramid with <paramref name="sides"/> sides along +Y.</summary>
            public void Cone(float y0, float y1, float r, int sides, Color col)
            {
                Vector3 tip = new Vector3(0, y1, 0);
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                    Tri(p0, tip, p1, col * (0.85f + 0.15f * Mathf.Cos(a0)));
                    Tri(p1, new Vector3(0, y0, 0), p0, col * 0.5f);
                }
            }

            public void Prism(float y0, float y1, float r, int sides, Color col)
            {
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                    Vector3 q0 = new Vector3(p0.x, y1, p0.z), q1 = new Vector3(p1.x, y1, p1.z);
                    Quad(p0, q0, q1, p1, col * (0.8f + 0.2f * Mathf.Cos(a0)));
                    Tri(q0, new Vector3(0, y1, 0), q1, col);
                }
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
