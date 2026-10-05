using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// The planet (13 §2, F0 version): six cube-face meshes at 3x the surface grid's resolution, displaced by
    /// the simulated relief and coloured by biome, vegetation and fire; a sea sphere; an atmosphere rim.
    /// Reads the world only. Recolours every couple of seconds of real time.
    /// </summary>
    public sealed class PlanetView : MonoBehaviour
    {
        public SimHost Host;
        public int Detail = 3;

        int _built = -1;
        readonly Mesh[] _faces = new Mesh[6];
        readonly Color[][] _colors = new Color[6][];
        Vector3[][] _dirs = new Vector3[6][];
        int _res;
        float _recolor, _sinceBuild;
        int _coast = -1;
        double _builtIce;
        Material _terrain, _water, _atmo;
        GameObject _sea, _air;

        void Start()
        {
            _terrain = Materials.Load("Terrain");
            _water = Materials.Load("Water");
            // the look is defined here (a material asset keeps whatever values it was first saved with)
            _water.SetFloat("_Alpha", 0.3f);   // the sea floor carries the water's colour by depth (Terrain.shader, D2-040)
            _water.SetColor("_Shallow", new Color(0.10f, 0.46f, 0.56f));
            _water.SetColor("_Deep", new Color(0.02f, 0.10f, 0.24f));
            _water.SetColor("_Sky", new Color(0.62f, 0.75f, 0.90f));
            _atmo = Materials.Load("Atmosphere");
        }

        void Update()
        {
            var w = Host.World;
            if (w == null) return;
            var P = w.Planet;
            _sinceBuild += Time.unscaledDeltaTime;
            bool iceMoved = System.Math.Abs(P.IceVolume - _builtIce) > 0.04 * System.Math.Max(_builtIce, 1e9) && _sinceBuild > 10f;
            if (_built != Host.WorldVersion || _coast != P.CoastVersion) BuildShell(); // the ground itself is TerrainLod
        }

        void BuildShell()
        {
            var P = Host.World.Planet;
            _built = Host.WorldVersion;
            _coast = P.CoastVersion;
            if (_sea == null)
            {
                _sea = new GameObject("Sea");
                _sea.transform.SetParent(transform, false);
                _sea.AddComponent<MeshFilter>().sharedMesh = ProcMeshes.Sphere(1f, 192, 384);
                var mr = _sea.AddComponent<MeshRenderer>(); mr.sharedMaterial = _water; mr.shadowCastingMode = ShadowCastingMode.Off;
                _air = new GameObject("Atmosphere");
                _air.transform.SetParent(transform, false);
                _air.AddComponent<MeshFilter>().sharedMesh = ProcMeshes.Sphere(1f, 64, 128);
                var ar = _air.AddComponent<MeshRenderer>(); ar.sharedMaterial = _atmo; ar.shadowCastingMode = ShadowCastingMode.Off;
            }
            _sea.transform.localScale = Vector3.one * (float)(P.Radius + Ground.WaterHeight(P, P.SeaLevel));
            _air.transform.localScale = Vector3.one * (float)(P.Radius * 1.07);
        }

        void Build()
        {
            var P = Host.World.Planet;
            _built = Host.WorldVersion;
            _coast = P.CoastVersion;
            _builtIce = P.IceVolume;
            _sinceBuild = 0;
            _res = P.Grid.N * Detail;
            int vn = (_res + 1) * (_res + 1);
            for (int f = 0; f < 6; f++)
            {
                var verts = new Vector3[vn];
                _dirs[f] = new Vector3[vn];
                _colors[f] = new Color[vn];
                double step = DMath.HalfPi / _res;
                for (int j = 0; j <= _res; j++)
                    for (int i = 0; i <= _res; i++)
                    {
                        int k = j * (_res + 1) + i;
                        double a = -DMath.PI / 4 + i * step, b = -DMath.PI / 4 + j * step;
                        Vec3d d = CubeSphere.FacePoint(f, a, b);
                        double e = PlanetGeo.ElevationFace(P, f, a, b);
                        double r = P.Radius + e * PlanetGeo.VScale;
                        verts[k] = new Vector3((float)(d.X * r), (float)(d.Y * r), (float)(d.Z * r));
                        _dirs[f][k] = new Vector3((float)d.X, (float)d.Y, (float)d.Z);
                    }
                var tris = new int[_res * _res * 6];
                int t = 0;
                for (int j = 0; j < _res; j++)
                    for (int i = 0; i < _res; i++)
                    {
                        int a0 = j * (_res + 1) + i, a1 = a0 + 1, b0 = a0 + _res + 1, b1 = b0 + 1;
                        tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                        tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
                    }
                if (_faces[f] == null)
                {
                    _faces[f] = new Mesh { name = "tc2-face" + f, indexFormat = vn > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    var go = new GameObject("Face" + f);
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = _faces[f];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = _terrain;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
                var m = _faces[f];
                m.Clear();
                m.vertices = verts;
                m.triangles = tris;
                // winding check: normals must point outward
                m.RecalculateNormals();
                var nrm = m.normals;
                if (Vector3.Dot(nrm[vn / 2], verts[vn / 2]) < 0)
                {
                    for (int q = 0; q < tris.Length; q += 3) { int tmp = tris[q + 1]; tris[q + 1] = tris[q + 2]; tris[q + 2] = tmp; }
                    m.triangles = tris;
                    m.RecalculateNormals();
                }
                m.RecalculateBounds();
            }
            Recolor();
            if (_sea == null)
            {
                _sea = new GameObject("Sea");
                _sea.transform.SetParent(transform, false);
                _sea.AddComponent<MeshFilter>().sharedMesh = ProcMeshes.Sphere(1f, 128, 256);
                var mr = _sea.AddComponent<MeshRenderer>(); mr.sharedMaterial = _water; mr.shadowCastingMode = ShadowCastingMode.Off;
                _air = new GameObject("Atmosphere");
                _air.transform.SetParent(transform, false);
                _air.AddComponent<MeshFilter>().sharedMesh = ProcMeshes.Sphere(1f, 64, 128);
                var ar = _air.AddComponent<MeshRenderer>(); ar.sharedMaterial = _atmo; ar.shadowCastingMode = ShadowCastingMode.Off;
            }
            float sea = (float)(P.Radius + P.SeaLevel * PlanetGeo.VScale);
            _sea.transform.localScale = Vector3.one * sea;
            _air.transform.localScale = Vector3.one * (float)(P.Radius * 1.07);
        }

        void Recolor()
        {
            var w = Host.World;
            var P = w.Planet;
            var fire = w.Fire;
            int n = P.Grid.N;
            for (int f = 0; f < 6; f++)
            {
                var cols = _colors[f];
                if (cols == null) continue;
                double step = DMath.HalfPi / _res;
                for (int j = 0; j <= _res; j++)
                    for (int i = 0; i <= _res; i++)
                    {
                        int k = j * (_res + 1) + i;
                        // bilinear blend of the four cell centres around this vertex (no blocky cells)
                        double u = (i / (double)Detail) - 0.5, v = (j / (double)Detail) - 0.5;
                        int i0 = (int)System.Math.Floor(u), j0 = (int)System.Math.Floor(v);
                        float fu = (float)(u - i0), fv = (float)(v - j0);
                        if (i0 < 0) { i0 = 0; fu = 0; } else if (i0 >= n - 1) { i0 = n - 2; fu = 1; }
                        if (j0 < 0) { j0 = 0; fv = 0; } else if (j0 >= n - 1) { j0 = n - 2; fv = 1; }
                        int c00 = f * n * n + j0 * n + i0;
                        Color col = Color.Lerp(Color.Lerp(CellColor(w, c00), CellColor(w, c00 + 1), fu), Color.Lerp(CellColor(w, c00 + n), CellColor(w, c00 + n + 1), fu), fv);
                        int c = (fu < 0.5f ? 0 : 1) + (fv < 0.5f ? 0 : n) + c00;
                        if (P.IsLand(c))
                        {
                            // a little variation so cells do not read as tiles
                            float jitter = ((KeyedRng.Hash(7, RngStream.None, (ulong)(f * 1000003 + k)) & 255) / 255f - 0.5f) * 0.05f;
                            col = new Color(col.r + jitter, col.g + jitter, col.b + jitter * 0.5f);
                        }
                        cols[k] = col.linear;
                    }
                _faces[f].colors = cols;
            }
        }

        /// <summary>A cell's colour: biome, vegetation density, fire and scars, fields (sRGB).</summary>
        static Color CellColor(TC2.Core.Simulation.World w, int c)
        {
            var P = w.Planet;
            var fire = w.Fire;
            Color col = PlanetGeo.BiomeColor((Biome)P.Biome[c]);
            if (!P.IsLand(c)) return col;
            float forest = P.Forest[c], grass = P.Grass[c];
            col = Color.Lerp(col, new Color(0.16f, 0.40f, 0.15f), Mathf.Clamp01(forest - 0.3f) * 0.5f);
            col = Color.Lerp(col, new Color(0.70f, 0.62f, 0.42f), Mathf.Clamp01(0.25f - grass - forest) * 2f);
            if (P.FieldOwner[c] != 0) col = Color.Lerp(col, new Color(0.78f, 0.70f, 0.40f), 0.45f);
            if (fire.Intensity[c] > 0) col = Color.Lerp(col, new Color(1f, 0.35f, 0.05f), 0.4f + 0.6f * fire.Intensity[c]);
            else if (fire.BurnedAt[c] != 0 && w.Now - fire.BurnedAt[c] < 2 * SimTime.Year)
            {
                float age = (float)((w.Now - fire.BurnedAt[c]) / (double)(2 * SimTime.Year));
                col = Color.Lerp(new Color(0.16f, 0.13f, 0.11f), col, age);
            }
            return col;
        }
    }
}
