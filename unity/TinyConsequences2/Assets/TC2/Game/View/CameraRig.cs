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
            Apply();
            UpdateSun();
        }

        public void Zoom(float notches)
        {
            double R = Host.World.Planet.Radius;
            double alt = Distance - R;
            alt *= System.Math.Pow(0.85, notches);
            alt = System.Math.Max(25.0, System.Math.Min(R * 3.0, alt));
            Distance = R + alt;
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
            double alt = view == "close" ? 450 : view == "village" ? 1500 : view == "region" ? 6000 : 22000;
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
            double tilt = Mathf.Lerp(62f, 0f, Mathf.InverseLerp(300f, 9000f, (float)alt)) * DMath.Deg2Rad;
            Vector3 up = PlanetGeo.ToVector(dir);
            Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up);
            if (north.sqrMagnitude < 1e-6f) north = Vector3.ProjectOnPlane(Vector3.forward, up);
            north.Normalize();
            Vector3 offset = (up * (float)System.Math.Cos(tilt) - north * (float)System.Math.Sin(tilt)) * (float)alt;
            Vector3 camPos = FocusPoint + offset;
            Cam.transform.position = camPos;
            Cam.transform.rotation = Quaternion.LookRotation((FocusPoint - camPos).normalized, alt < 9000 ? north + up * 0.2f : Vector3.up);
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
