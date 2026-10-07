using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Orbit camera from the whole planet down to a village: drag to turn the world, wheel/pinch to zoom; the view
    /// tilts toward the horizon as it comes down. Also drives the sun from world time (day, season, declination).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public SimHost Host;
        public Camera Cam;
        public Light Sun;
        public double Lat = 0.5, Lon = 0.0;   // radians: where the camera looks
        public double Distance = 30000;       // from the planet centre (m)
        public Vector3 FocusPoint;            // surface point under the view's centre
        Vector3 _lastMouse;
        bool _dragging;
        int _framedVersion = -1;

        void Update()
        {
            var w = Host.World;
            if (w == null) return;
            if (_framedVersion != Host.WorldVersion) { FrameFirstSettlement(); _framedVersion = Host.WorldVersion; }
            double R = w.Planet.Radius;
            // input (legacy input manager)
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { _lastMouse = Input.mousePosition; _dragging = !Hud.PointerOverUi; }
            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) _dragging = false;
            if (_dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            {
                Vector3 dm = Input.mousePosition - _lastMouse;
                _lastMouse = Input.mousePosition;
                double altitude = Distance - R;
                double k = altitude / R * 0.0022 + 0.00002;
                Lon -= dm.x * k * (1.0 / System.Math.Max(0.2, System.Math.Cos(Lat)));
                Lat -= dm.y * k;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (Hud.PointerOverUi) wheel = 0;
            if (Mathf.Abs(wheel) > 0.01f) Zoom(wheel);
            if (Input.GetKey(KeyCode.Q)) Zoom(Time.unscaledDeltaTime * 4f);
            if (Input.GetKey(KeyCode.E)) Zoom(-Time.unscaledDeltaTime * 4f);
            double kk = (Distance - R) / R * 0.9 + 0.01;
            if (Input.GetKey(KeyCode.A)) Lon -= Time.unscaledDeltaTime * kk;
            if (Input.GetKey(KeyCode.D)) Lon += Time.unscaledDeltaTime * kk;
            if (Input.GetKey(KeyCode.W)) Lat += Time.unscaledDeltaTime * kk;
            if (Input.GetKey(KeyCode.S)) Lat -= Time.unscaledDeltaTime * kk;
            Lat = System.Math.Max(-1.45, System.Math.Min(1.45, Lat));
            // while the world lives through an absence the camera rises to the planet (12 §5: the catch-up shows the world ageing from
            // above), leaving the frame's time to the simulation rather than to the near views behind the catch-up screen; the view
            // the player left comes back when the world arrives
            if (Host.CatchingUp)
            {
                if (!_holding) { _held = Distance; _holding = true; }
                Distance = R + CatchUpAltitude * w.Planet.GeoScale;
            }
            else if (_holding) { Distance = _held; _holding = false; }
            Apply();
            UpdateSun();
        }

        /// <summary>The camera's height while the world catches up an absence (m on the 10 km world; with the planet's size).</summary>
        public static double CatchUpAltitude = 22000;
        bool _holding;
        double _held;

        public void Zoom(float notches)
        {
            double R = Host.World.Planet.Radius;
            double alt = Distance - R;
            alt *= System.Math.Pow(0.85, notches);
            alt = System.Math.Max(25.0, System.Math.Min(R * 3.0, alt));
            Distance = R + alt;
        }

        /// <summary>A named view of the first village (orbit, region, village, close, low) or "lat,lon,alt" (degrees, m).</summary>
        /// <summary>
        /// The liveliest shore cell within a reach of a point (seals first, then the shoals off a coast; D2-039), or -1. The
        /// shots use it to frame the coast views and to set their clock by its longitude.
        /// </summary>
        public static int LivelyShore(TC2.Core.Simulation.World w, Vec3d near, double reach)
        {
            var P = w.Planet;
            int best = -1; double bv = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c) || P.Species.Count == 0) continue;
                if (reach < double.MaxValue && Vec3d.ArcDistance(P.Grid.Center[c], near, P.Radius) > reach) continue;
                bool shore = false;
                for (int q = 0; q < 4; q++) if (P.IsLand(P.Grid.Nbr4[c * 4 + q])) shore = true;
                if (!shore) continue;
                double v = 0;
                for (int s = 0; s < P.Species.Count; s++)
                {
                    var sp = P.Species[s];
                    if (sp.Habitat != 1 || sp.Kind == 12) continue;
                    if (sp.Plankton > 0) v += P.Abund[s * P.Cells + c] * 0.01;
                }
                v += 1000.0 * SeaLifeView.HaulOut(P, c);   // (seals where the view shows them hauled out)
                if (v > bv) { bv = v; best = c; }
            }
            return best;
        }

        public void SetView(string view)
        {
            var w = Host.World;
            _framedVersion = Host.WorldVersion;
            int sid = w.Settlements.First();
            Vec3d d = sid != 0 ? w.Settlements.Get(sid).Anchor : new Vec3d(0, 0.4, 0.9).Normalized;
            var parts = view.Split(',');
            if (parts.Length == 3)
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                double lat = double.Parse(parts[0], ci) * DMath.Deg2Rad, lon = double.Parse(parts[1], ci) * DMath.Deg2Rad;
                LookAt(Vec3d.FromLatLon(lat, lon), double.Parse(parts[2], ci));
                return;
            }
            if (view.StartsWith("herd") && sid != 0)
            {
                // the first village's herd on the day's ground, with its herders (D2-050)
                long day = SimTime.FloorDiv(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
                int ground = w.Stock.GroundOf(sid, day);
                if (ground >= 0) d = w.Planet.Grid.Center[ground];
                LookAt(d, view == "herdfar" ? 260 : 70);
                return;
            }
            if (view.StartsWith("wild"))
            {
                // the busiest wild ground within 6 km of the first village (no closer than 800 m to any village)
                var P = w.Planet;
                int best = -1; double bv = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c) || P.Species.Count == 0) continue;
                    double dd = Vec3d.ArcDistance(P.Grid.Center[c], d, P.Radius);
                    if (dd > 6000) continue;
                    bool near = false;
                    for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                        if (Vec3d.ArcDistance(w.Settlements.Get(sid2).Anchor, P.Grid.Center[c], P.Radius) < 800) { near = true; break; }
                    if (near) continue;
                    double v = 0;
                    for (int s = 0; s < P.Species.Count; s++) v += P.Abund[s * P.Cells + c] * System.Math.Sqrt(P.Species[s].Mass);
                    if (v > bv) { bv = v; best = c; }
                }
                if (best >= 0) d = P.Grid.Center[best];
                LookAt(d, view == "wildfar" ? 320 : 120);
                return;
            }
            if (view.StartsWith("coast") || view.StartsWith("seals"))
            {
                // the liveliest shore within 8 km of the first village (seals: anywhere on the planet): seals first, then
                // the shoals off a coast (D2-039)
                var P = w.Planet;
                int best = LivelyShore(w, d, view.StartsWith("seals") ? double.MaxValue : 8000);
                if (view.StartsWith("seals") && Application.isBatchMode)
                {
                    double total = 0; int haul = 0, most = 0;
                    for (int s = 0; s < P.Species.Count; s++) if (P.Species[s].Habitat == 1 && P.Species[s].Kind == 12) for (int c = 0; c < P.Cells; c++) total += P.Abund[s * P.Cells + c];
                    for (int c = 0; c < P.Cells; c++) { int h = SeaLifeView.HaulOut(P, c); if (h > 0) { haul++; most = Mathf.Max(most, h); } }
                    Debug.Log("[TC2] seals in the world " + total.ToString("0.0") + ", haul-outs " + haul + " (most " + most + "), framed cell " + best + " showing " + (best >= 0 ? SeaLifeView.HaulOut(P, best) : 0));
                }
                if (best >= 0)
                {
                    // look a little out to sea from that shore: the beach in view, and the water off it
                    int far = best; double fd = -1;
                    for (int q = 0; q < 4; q++)
                    {
                        int nb = P.Grid.Nbr4[best * 4 + q];
                        if (P.IsLand(nb)) continue;
                        double depth = P.SeaLevel - P.Elev[nb];
                        if (depth > fd) { fd = depth; far = nb; }
                    }
                    d = (P.Grid.Center[best] * 0.6 + P.Grid.Center[far] * 0.4).Normalized;
                }
                LookAt(d, view == "coastfar" || view == "sealsfar" ? 300 : 90);
                return;
            }
            if (view.StartsWith("forest"))
            {
                // the most wooded land within 6 km of the first village, no closer than 600 m to any village (D2-079)
                var P = w.Planet;
                int best = -1; double bv = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c) || P.Forest[c] <= bv) continue;
                    if (Vec3d.ArcDistance(P.Grid.Center[c], d, P.Radius) > 6000) continue;
                    bool near = false;
                    for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                        if (Vec3d.ArcDistance(w.Settlements.Get(sid2).Anchor, P.Grid.Center[c], P.Radius) < 600) { near = true; break; }
                    if (near) continue;
                    bv = P.Forest[c]; best = c;
                }
                if (best >= 0) d = P.Grid.Center[best];
                LookAt(d, view == "forestfar" ? 900 : view == "forestlow" ? 95 : 260);
                return;
            }
            if (view.StartsWith("river"))
            {
                // the largest river within 6 km of the first village (D2-084: the acceptance views), seen from 600 m (riverfar: 2.5 km)
                var P = w.Planet;
                int best = -1; double bv = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c) || !P.IsStream(c) || P.RiverFlow[c] <= bv) continue;
                    if (Vec3d.ArcDistance(P.Grid.Center[c], d, P.Radius) > 6000) continue;
                    bv = P.RiverFlow[c]; best = c;
                }
                if (best >= 0) d = P.Grid.Center[best];
                LookAt(d, view == "riverfar" ? 2500 : 600);
                return;
            }
            if (view.StartsWith("town"))
            {
                // the most peopled place on the planet (D2-084), at the village view's height (townclose: 260 m)
                int most = 0, bp = -1;
                for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                {
                    ref var st = ref w.Settlements.Get(s2);
                    if (st.Inhabited && st.Population > bp) { bp = st.Population; most = s2; }
                }
                if (most != 0) d = w.Settlements.Get(most).Anchor;
                LookAt(d, view == "townclose" ? 260 : 900);
                return;
            }
            if (view.StartsWith("scar"))
            {
                // the most recently burnt ground within 10 km of the first village (D2-084: does the land keep its history?)
                var P = w.Planet;
                int best = -1; long bt = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    long t = w.Fire.BurnedAt[c];
                    if (t <= bt || !P.IsLand(c)) continue;
                    if (Vec3d.ArcDistance(P.Grid.Center[c], d, P.Radius) > 10000) continue;
                    bt = t; best = c;
                }
                if (best >= 0) d = P.Grid.Center[best];
                LookAt(d, view == "scarfar" ? 2500 : 700);
                return;
            }
            if (view.StartsWith("volcano"))
            {
                // the volcano nearest the first village (land vents first)
                var P = w.Planet;
                int best = -1; double bd = double.MaxValue;
                for (int v = 0; v < P.Vents.Count; v++)
                {
                    double dd = Vec3d.Angle(P.Vents[v].At, d) + (P.IsLand(P.Vents[v].Cell) ? 0 : 1.0);
                    if (dd < bd) { bd = dd; best = v; }
                }
                if (best >= 0) d = P.Vents[best].At;
                LookAt(d, view == "volcanoclose" ? 650 : view == "volcanofar" ? 5200 : 2200);
                return;
            }
            // (orbit and region frame the planet and a part of it, so they go with its size; the village and nearer views are metres, D2-085)
            double S = Host.World.Planet.GeoScale;
            double alt = view == "street" ? 32 : view == "close" ? 260 : view == "low" ? 95 : view == "village" ? 900 : view == "region" ? 5000 * S : 22000 * S;
            LookAt(d, alt);
        }

        float _shake, _shakeFade = 1f;

        /// <summary>Shake the camera (0..1 strength, seconds): a quake felt where it looks.</summary>
        public void Shake(float strength, float seconds)
        {
            if (strength <= _shake) return;
            _shake = Mathf.Clamp01(strength);
            _shakeFade = _shake / Mathf.Max(0.3f, seconds);
        }

        public void LookAt(Vec3d dir, double altitude)
        {
            Lat = dir.Latitude; Lon = dir.Longitude; Distance = Host.World.Planet.Radius + altitude;
        }

        void FrameFirstSettlement()
        {
            var w = Host.World;
            int sid = w.Settlements.First();
            Vec3d d = sid != 0 ? w.Settlements.Get(sid).Anchor : new Vec3d(0, 0.4, 0.9).Normalized;
            string view = Browser.Param("view");
            double S = w.Planet.GeoScale;
            double alt = view == "close" ? 450 : view == "village" ? 1500 : view == "region" ? 6000 * S : 22000 * S;
            LookAt(d, alt);
        }

        void Apply()
        {
            var P = Host.World.Planet;
            double R = P.Radius;
            Vec3d dir = Vec3d.FromLatLon(Lat, Lon);
            FocusPoint = PlanetGeo.Surface(P, dir);
            double alt = Distance - R;
            // tilt toward the horizon close in: 0 deg from orbit, up to ~62 deg at village height
            float far9 = (float)(9000.0 * P.GeoScale);   // (from orbit straight down, with the planet's size)
            double tilt = Mathf.Lerp(62f, 0f, Mathf.InverseLerp(300f, far9, (float)alt)) * DMath.Deg2Rad;
            Vector3 up = PlanetGeo.ToVector(dir);
            Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up);
            if (north.sqrMagnitude < 1e-6f) north = Vector3.ProjectOnPlane(Vector3.forward, up);
            north.Normalize();
            Vector3 offset = (up * (float)System.Math.Cos(tilt) - north * (float)System.Math.Sin(tilt)) * (float)alt;
            Vector3 camPos = FocusPoint + offset;
            // a quake felt where the camera looks shakes it (presentation only)
            if (_shake > 0f)
            {
                float t = Time.unscaledTime * 23f, a = _shake * (float)System.Math.Max(4.0, alt * 0.004);
                camPos += new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t) - 0.5f, Mathf.PerlinNoise(t, 5.1f) - 0.5f) * (2f * a);
                _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * _shakeFade);
            }
            Cam.transform.position = camPos;
            Cam.transform.rotation = Quaternion.LookRotation((FocusPoint - camPos).normalized, alt < far9 ? north + up * 0.2f : Vector3.up);
            float far = (float)(Distance + R * 1.5);
            Cam.farClipPlane = far;
            Cam.nearClipPlane = Mathf.Clamp((float)alt * 0.02f, 0.3f, 400f);
        }

        void UpdateSun()
        {
            if (Sun == null) return;
            var P = Host.World.Planet;
            double t = Host.RenderTime;
            long tl = (long)t;
            double hour = (SimTime.SecondOfDay(tl) + (t - tl)) / SimTime.Hour;
            double decl = P.Declination(tl);
            double sunLon = DMath.TwoPi * (12.0 - hour) / 24.0;
            Vec3d toSun = Vec3d.FromLatLon(decl, sunLon);
            Sun.transform.rotation = Quaternion.LookRotation(-PlanetGeo.ToVector(toSun), Vector3.up);
        }
    }
}
