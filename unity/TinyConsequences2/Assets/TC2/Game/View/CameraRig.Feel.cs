using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 31: navigation that feels effortless) What the camera does between the player's inputs: a wheel's zoom eased in log height, toward the ground under the
    /// pointer when coming down; a drag's turn carrying a moment after the button is let go; a flight to anywhere ("go there", the guide) up an arc and down, cancelled by
    /// any hand on the camera; the title's composition - the planet turning slowly at the right of the screen, tipped a little, lit from the left with a night crescent
    /// (the picture's only: the world's sun comes back as the player goes in). Presentation only.
    /// </summary>
    public sealed partial class CameraRig
    {
        // the wheel
        bool _zoomOn; double _zoomGoal;
        double _nudgeLat, _nudgeLon;
        // the drag
        double _velLat, _velLon;
        // a flight
        bool _fly; float _flyT, _flySecs;
        double _flyFromLat, _flyFromLon, _flyFromAlt, _flyToLat, _flyToLon, _flyToAlt, _flyPeak;
        // the title
        bool _title; float _titleK;
        double _titleLonDrift;
        const float TitleYaw = 15.5f, TitleRoll = 13f;

        /// <summary>Whether the title holds the camera now.</summary>
        public bool InTitle => _title;

        /// <summary>Whether the camera is on a flight now.</summary>
        public bool Flying => _fly;

        /// <summary>Flies to a place: up an arc (higher the farther it goes), turning the short way round, and down to the height asked for (m above the ground); any input takes it back.</summary>
        public void FlyTo(Vec3d dir, double altitude, float seconds = 2.4f)
        {
            if (Host.World == null) return;
            double R = Host.World.Planet.Radius;
            _fly = true; _flyT = 0f; _flySecs = Mathf.Max(0.2f, seconds);
            _flyFromLat = Lat; _flyFromLon = Lon; _flyFromAlt = System.Math.Max(10.0, Distance - R);
            _flyToLat = dir.Latitude; _flyToLon = dir.Longitude; _flyToAlt = System.Math.Max(10.0, altitude);
            double ang = Vec3d.Angle(Vec3d.FromLatLon(Lat, Lon), dir);
            // (a flight over a distance sees the ground it crosses: its peak about the arc's own length, never under either end)
            _flyPeak = System.Math.Max(System.Math.Max(_flyFromAlt, _flyToAlt), ang * R * 0.55);
            _zoomOn = false; _velLat = _velLon = 0; _nudgeLat = _nudgeLon = 0;
        }

        /// <summary>Goes to a place at the height the camera has (a page's "go there").</summary>
        public void FlyTo(double lat, double lon, float seconds = 2.2f)
        {
            if (Host.World == null) return;
            FlyTo(Vec3d.FromLatLon(lat, lon), Distance - Host.World.Planet.Radius, seconds);
        }

        /// <summary>A flight's target follows someone who is walking.</summary>
        public void FlyRetarget(double lat, double lon) { if (_fly) { _flyToLat = lat; _flyToLon = lon; } }

        /// <summary>One frame of the camera's own easing, before the lapse (<paramref name="handOn"/>: the player's hand this frame cancels a flight and any carrying).</summary>
        void FeelUpdate(TC2.Core.Simulation.World w, bool handOn, float dt)
        {
            double R = w.Planet.Radius;
            if (handOn) _fly = false;
            if (_fly)
            {
                _flyT = Mathf.Min(1f, _flyT + dt / _flySecs);
                float e = Smoother(_flyT);
                Lat = _flyFromLat + (_flyToLat - _flyFromLat) * e;
                Lon = _flyFromLon + Wrap(_flyToLon - _flyFromLon) * e;
                // up the first part and down the rest, in log height, so the ground's speed under the camera stays even
                double alt;
                if (_flyPeak > _flyFromAlt * 1.02 && _flyPeak > _flyToAlt * 1.02)
                {
                    float up = Smoother(_flyT / 0.42f), down = Smoother((_flyT - 0.32f) / 0.68f);
                    alt = _flyT < 0.32f ? LogLerp(_flyFromAlt, _flyPeak, up) : LogLerp(LogLerp(_flyFromAlt, _flyPeak, up), _flyToAlt, down);
                }
                else alt = LogLerp(_flyFromAlt, _flyToAlt, e);
                Distance = R + alt;
                if (_flyT >= 1f) { _fly = false; Lat = _flyToLat; Lon = _flyToLon; Distance = R + _flyToAlt; }
                return;
            }
            // the wheel's zoom, eased
            if (_zoomOn)
            {
                double a = System.Math.Max(1.0, Distance - R);
                double k = 1.0 - System.Math.Exp(-dt / 0.085);
                double next = System.Math.Exp(System.Math.Log(a) + (System.Math.Log(_zoomGoal) - System.Math.Log(a)) * k);
                if (System.Math.Abs(System.Math.Log(next / _zoomGoal)) < 0.0015) { next = _zoomGoal; _zoomOn = false; }
                Distance = R + next;
            }
            if (_nudgeLat != 0 || _nudgeLon != 0)
            {
                double k = 1.0 - System.Math.Exp(-dt / 0.085);
                double dLa = _nudgeLat * k, dLo = _nudgeLon * k;
                Lat += dLa; Lon += dLo; _nudgeLat -= dLa; _nudgeLon -= dLo;
                if (System.Math.Abs(_nudgeLat) + System.Math.Abs(_nudgeLon) < 1e-7) _nudgeLat = _nudgeLon = 0;
            }
            // the drag's carry
            if (!_dragging && (_velLat != 0 || _velLon != 0))
            {
                Lat += _velLat * dt; Lon += _velLon * dt;
                double fade = System.Math.Exp(-dt / 0.16);
                _velLat *= fade; _velLon *= fade;
                if (System.Math.Abs(_velLat) + System.Math.Abs(_velLon) < 1e-6) _velLat = _velLon = 0;
            }
        }

        /// <summary>The wheel: eased, and coming down toward the ground under the pointer.</summary>
        void WheelZoom(TC2.Core.Simulation.World w, float notches)
        {
            double R = w.Planet.Radius;
            double from = _zoomOn ? _zoomGoal : System.Math.Max(1.0, Distance - R);
            double to = from * System.Math.Pow(0.85, notches);
            to = System.Math.Max(25.0, System.Math.Min(R * 3.0, to));
            _zoomGoal = to; _zoomOn = true; _fly = false;
            if (notches <= 0f || from > R * 1.2) return;
            // toward the pointer: the ground under it keeps its place on the screen as the height falls (nearly)
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            double ox = ray.origin.x, oy = ray.origin.y, oz = ray.origin.z, dx = ray.direction.x, dy = ray.direction.y, dz = ray.direction.z;
            double b = ox * dx + oy * dy + oz * dz, c = ox * ox + oy * oy + oz * oz - R * R, disc = b * b - c;
            if (disc <= 0) return;
            double t = -b - System.Math.Sqrt(disc);
            if (t <= 0) return;
            var hit = new Vec3d(ox + t * dx, oy + t * dy, oz + t * dz).Normalized;
            double share = (1.0 - to / from) * 0.75;
            _nudgeLat += (hit.Latitude - Lat) * share;
            _nudgeLon += Wrap(hit.Longitude - Lon) * share;
        }

        /// <summary>The title takes the camera: the planet to the right of the screen, turning slowly under a composed light.</summary>
        public void TitleBegin()
        {
            _title = true; _fly = false; _zoomOn = false; _velLat = _velLon = 0; _nudgeLat = _nudgeLon = 0;
            var w = Host.World;
            if (w != null)
            {
                double R = w.Planet.Radius;
                Distance = R + R * 1.42;
                Lat = System.Math.Max(-0.55, System.Math.Min(0.55, Lat));
            }
        }

        /// <summary>The title lets the camera go (it eases back to the plain view and the world's sun over a moment).</summary>
        public void TitleEnd() { _title = false; }

        /// <summary>The title's slow turn; its composition eased in and out.</summary>
        void TitleUpdate(TC2.Core.Simulation.World w, float dt)
        {
            _titleK = Mathf.MoveTowards(_titleK, _title ? 1f : 0f, dt / 1.4f);
            if (!_title) return;
            if (!_dragging) Lon += 0.016 * dt;   // (one turn in six minutes)
            double R = w.Planet.Radius;
            // (the height kept, whatever came before: a world newly made frames its own first village)
            Distance += (R + R * 1.42 - Distance) * (1.0 - System.Math.Exp(-dt / 0.8));
            Lat += (System.Math.Max(-0.55, System.Math.Min(0.55, Lat)) - Lat) * (1.0 - System.Math.Exp(-dt / 2.0));
        }

        /// <summary>The planet moved off the screen's centre and tipped, as far as the title's composition has come in.</summary>
        void TitleCompose()
        {
            if (_titleK <= 0f) return;
            float s = Smoother(_titleK);
            Cam.transform.Rotate(0f, -TitleYaw * s, 0f, Space.Self);
            Cam.transform.Rotate(0f, 0f, TitleRoll * s, Space.Self);
        }

        /// <summary>The light on the title: from the left and a little above, whatever the hour - the planet three-quarters lit with its night crescent at the right.</summary>
        Vector3 TitleSun(Vector3 shown)
        {
            if (_titleK <= 0f) return shown;
            float s = Smoother(_titleK);
            Vector3 up = Cam.transform.up, right = Cam.transform.right, toCam = -Cam.transform.forward;
            Vector3 steady = (toCam * 0.62f - right * 0.72f + up * 0.32f).normalized;
            return Vector3.Slerp(shown, steady, s).normalized;
        }
    }
}
