using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Everything standing on the planet (F0): houses, granaries and fields of settlements, trees from the forest
    /// field near the camera, fire in burning cells, and people at their analytic positions at render time.
    /// Objects grow with camera distance so towns and woods stay readable from afar (presentation only).
    /// Reads the world only.
    /// </summary>
    public sealed class WorldObjectsView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        Instancer _people, _houses, _granaries, _fields, _conifers, _broadleaf, _flames;
        Material _mat;
        float _treeTimer, _placeTimer;
        int _treeFocusCell = -1;
        public int PeopleDrawn, TreesDrawn;

        static readonly Color[] ActColors =
        {
            new Color(0.8f, 0.8f, 0.8f), new Color(0.62f, 0.55f, 0.48f), new Color(0.25f, 0.25f, 0.45f), new Color(0.85f, 0.6f, 0.3f),
            new Color(0.85f, 0.75f, 0.2f), new Color(0.55f, 0.35f, 0.2f), new Color(0.75f, 0.35f, 0.55f), new Color(0.35f, 0.75f, 0.85f),
            new Color(0.25f, 0.55f, 0.95f), new Color(0.35f, 0.65f, 0.95f), new Color(0.25f, 0.45f, 0.85f), new Color(0.95f, 0.15f, 0.1f),
            new Color(0.95f, 0.45f, 0.15f), new Color(0.95f, 0.85f, 0.75f), new Color(0.3f, 0.6f, 0.85f), new Color(0.45f, 0.7f, 0.3f),
        };

        void Start()
        {
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            _people = new Instancer(ProcMeshes.Person(), _mat, true);
            _houses = new Instancer(ProcMeshes.House(), _mat, true);
            _granaries = new Instancer(ProcMeshes.Granary(), _mat, true);
            _fields = new Instancer(ProcMeshes.Field(), _mat, false);
            _conifers = new Instancer(ProcMeshes.Conifer(), _mat, true);
            _broadleaf = new Instancer(ProcMeshes.Broadleaf(), _mat, true);
            _flames = new Instancer(ProcMeshes.Flame(), _mat, false);
        }

        /// <summary>Presentation scale: real size close in, exaggerated with distance so things stay visible.</summary>
        float Exaggeration(Vector3 at)
        {
            if (Rig == null || Rig.Cam == null) return 1f;
            float d = Vector3.Distance(Rig.Cam.transform.position, at);
            return Mathf.Clamp(d / 260f, 1f, 22f);
        }

        void LateUpdate()
        {
            var w = Host.World;
            if (w == null || Host.CatchingUp) return;
            var P = w.Planet;
            _placeTimer -= Time.unscaledDeltaTime;
            if (_placeTimer <= 0) { _placeTimer = 0.5f; RebuildPlaces(w); }
            _treeTimer -= Time.unscaledDeltaTime;
            if (_treeTimer <= 0) { _treeTimer = 1.5f; RebuildTrees(w); }
            RebuildFire(w);
            RebuildPeople(w);
            var cam = Rig != null ? Rig.Cam : Camera.main;
            _fields.Draw(cam); _houses.Draw(cam); _granaries.Draw(cam); _conifers.Draw(cam); _broadleaf.Draw(cam); _flames.Draw(cam); _people.Draw(cam);
        }

        void RebuildPlaces(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            _houses.Clear(); _granaries.Clear(); _fields.Clear();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                Vector3 center = PlanetGeo.Surface(P, s.Anchor);
                float ex = Mathf.Min(Exaggeration(center), 2.2f);
                var hhs = w.HouseholdsOf(sid);
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var h = ref w.Households.Get(hhs[i]);
                    Vec3d d = s.LocalToSphere(h.HomeX, h.HomeY, P.Radius);
                    Vector3 pos = PlanetGeo.Surface(P, d);
                    float yaw = (hhs[i] * 37) % 360;
                    _houses.Add(0, new Color(1f, 1f, 1f), Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, yaw), Vector3.one * ex));
                }
                Vec3d gd = s.LocalToSphere(s.SquareX + 14, s.SquareY - 10, P.Radius);
                Vector3 gp = PlanetGeo.Surface(P, gd);
                float fill = Mathf.Clamp01((float)(s.Granary / System.Math.Max(1.0, s.Population * 60.0)));
                _granaries.Add(0, Color.white, Matrix4x4.TRS(gp, PlanetGeo.Upright(gp, 0), new Vector3(ex, ex * (0.6f + 0.6f * fill), ex)));
                // fields: a patch on every claimed cell, sized by the share of the cell under the plough
                Color fc = s.Crop == CropStage.Sown ? new Color(0.42f, 0.32f, 0.21f)
                    : s.Crop == CropStage.Growing ? Color.Lerp(new Color(0.46f, 0.42f, 0.24f), new Color(0.40f, 0.56f, 0.22f), (float)s.CropGrowth)
                    : s.Crop == CropStage.Ripe ? new Color(0.80f, 0.68f, 0.30f) : new Color(0.62f, 0.55f, 0.36f);
                if (s.CropBurnt > 0.2) fc = Color.Lerp(fc, new Color(0.12f, 0.1f, 0.08f), (float)s.CropBurnt);
                int group = (int)s.Crop * 100 + (int)(s.CropGrowth * 10) + (s.CropBurnt > 0.2 ? 5000 : 0);
                var cells = w.FieldCellsOf(sid);
                double used = s.ArableHa > 0 ? System.Math.Min(1.0, s.FieldHa / s.ArableHa) : 1.0;
                Color fc2 = Color.Lerp(fc, new Color(0.30f, 0.24f, 0.14f), 0.22f); // every other strip a shade darker
                for (int i = 0; i < cells.Count; i++)
                {
                    int c = cells[i];
                    double ha = P.AreaHa(c) * P.Arability(c, w.PlanetSys.MeanTemp(c)) * used;
                    float side = Mathf.Sqrt((float)ha * 10000f);
                    Vec3d ctr = P.Grid.Center[c];
                    float tilt = (c * 37) % 24 - 12; // a little off the village's grid
                    const int Strips = 5;
                    float sw = side / Strips;
                    for (int k = 0; k < Strips; k++)
                    {
                        double off = (k - (Strips - 1) * 0.5) * sw;
                        double ang = tilt * DMath.PI / 180.0;
                        Vec3d d = (ctr * P.Radius + s.East * (off * DMath.Cos(ang)) + s.North * (off * DMath.Sin(ang))).Normalized;
                        Vector3 fp = PlanetGeo.Surface(P, d, 0.3f);
                        Vector3 upv = fp.normalized, nv = PlanetGeo.ToVector(s.North);
                        Vector3 fwd = (nv - Vector3.Dot(nv, upv) * upv).normalized; // north, laid flat on this cell
                        Quaternion rot = Quaternion.AngleAxis(tilt, upv) * Quaternion.LookRotation(fwd, upv);
                        _fields.Add(group * 2 + (k & 1), (k & 1) == 0 ? fc : fc2, Matrix4x4.TRS(fp, rot, new Vector3(sw * 0.92f, 1, side)));
                    }
                }
            }
        }

        void RebuildTrees(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            if (Rig == null) return;
            int focus = P.Grid.CellOf(PlanetGeo.ToVec(Rig.FocusPoint));
            float camDist = Rig.Cam != null ? Vector3.Distance(Rig.Cam.transform.position, Rig.FocusPoint) : 5000f;
            _conifers.Clear(); _broadleaf.Clear();
            TreesDrawn = 0;
            if (camDist > 9000f) return; // from orbit the forest is the terrain's colour
            _treeFocusCell = focus;
            var cells = new System.Collections.Generic.List<int>();
            w.Hazards.CellsWithin(w, focus, camDist < 2500f ? 1600 : 3200, cells);
            float ex = Mathf.Clamp(camDist / 500f, 1f, 6f);
            int perCell = camDist < 2500f ? 26 : 10;
            foreach (int c in cells)
            {
                if (!P.IsLand(c)) continue;
                float forest = P.Forest[c];
                if (forest < 0.05f) continue;
                if (w.Fire.Intensity[c] > 0.5f) continue;
                int n = Mathf.RoundToInt(forest * perCell);
                Vec3d ctr = P.Grid.Center[c];
                Settlement.Frame(ctr, out Vec3d east, out Vec3d north);
                double half = P.CellSize * 0.5;
                bool cold = P.Temp[c] < 6 || P.Elev[c] > 1200;
                for (int k = 0; k < n; k++)
                {
                    ulong h = KeyedRng.Hash(w.Seed, RngStream.None, (ulong)c, (ulong)k);
                    double ox = (KeyedRng.Unit(h) * 2 - 1) * half, oy = (KeyedRng.Unit(KeyedRng.Mix(h)) * 2 - 1) * half;
                    Vec3d d = (ctr * P.Radius + east * ox + north * oy).Normalized;
                    int under = P.Grid.CellOf(d);
                    if (w.SettlementAtCell(under) != 0 || P.FieldOwner[under] != 0) continue; // fields and houses are cleared ground
                    Vector3 pos = PlanetGeo.Surface(P, d);
                    float sc = ex * (0.7f + 0.6f * (float)KeyedRng.Unit(KeyedRng.Mix(h + 7)));
                    var m = Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, (float)(h % 360)), Vector3.one * sc);
                    bool conifer = cold || (h & 3) == 0;
                    int shade = (int)(h >> 8) % 3;
                    Color col = conifer ? new Color(0.12f + 0.03f * shade, 0.30f + 0.04f * shade, 0.16f) : new Color(0.24f + 0.04f * shade, 0.45f + 0.05f * shade, 0.18f);
                    if (conifer) _conifers.Add(shade, col, m); else _broadleaf.Add(shade, col, m);
                    TreesDrawn++;
                }
            }
        }

        void RebuildFire(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            var fire = w.Fire;
            _flames.Clear();
            float t = Time.time;
            for (int i = 0; i < fire.Active.Count; i++)
            {
                int c = fire.Active[i];
                float inten = fire.Intensity[c];
                Vec3d ctr = P.Grid.Center[c];
                Settlement.Frame(ctr, out Vec3d east, out Vec3d north);
                for (int k = 0; k < 6; k++)
                {
                    ulong h = KeyedRng.Hash(11, RngStream.None, (ulong)c, (ulong)k);
                    double ox = (KeyedRng.Unit(h) * 2 - 1) * P.CellSize * 0.45, oy = (KeyedRng.Unit(KeyedRng.Mix(h)) * 2 - 1) * P.CellSize * 0.45;
                    Vec3d d = (ctr * P.Radius + east * ox + north * oy).Normalized;
                    Vector3 pos = PlanetGeo.Surface(P, d);
                    float flicker = 0.75f + 0.25f * Mathf.Sin(t * 7f + k * 1.7f + c);
                    float hgt = (8f + 22f * inten) * flicker * Mathf.Max(1f, Exaggeration(pos) * 0.5f);
                    _flames.Add(k % 2, k % 2 == 0 ? new Color(1f, 0.45f, 0.08f) : new Color(1f, 0.75f, 0.2f), Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, k * 60), new Vector3(hgt * 0.5f, hgt, hgt * 0.5f)), 2.5f);
                }
            }
        }

        void RebuildPeople(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            _people.Clear();
            PeopleDrawn = 0;
            double t = Host.RenderTime;
            float camDist = Rig != null && Rig.Cam != null ? Vector3.Distance(Rig.Cam.transform.position, Rig.FocusPoint) : 5000f;
            if (camDist > 12000f) return;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                if (p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant) continue; // indoors
                Vec3d d = w.Agents.PositionAt(p, t);
                Vector3 pos = PlanetGeo.Surface(P, d);
                float age = (float)p.AgeYears((long)t);
                float sc = (age < 12 ? 0.6f + age * 0.035f : 1f) * Mathf.Min(Exaggeration(pos), 7f);
                int a = (int)p.Act;
                _people.Add(a, ActColors[a % ActColors.Length], Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, (id * 53) % 360), Vector3.one * sc));
                PeopleDrawn++;
            }
        }
    }
}
