using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 29: the long skip as a timelapse) When the world runs so fast that its days would flash past - a catch-up of an absence, the player's skip
    /// ahead, or a speed of a day a second or more - the camera keeps the player's place (where it looked, how high, whom it followed), rises smoothly to
    /// the whole planet, turns slowly round it under a steady light while the years pass, and comes back down to that place when the world slows again
    /// (to the person followed where they are then). The steady light is the picture's only: the sun stands beside the camera, east of it, its height the
    /// seasons' mean, so the land passes under a calm terminator and the night side's lights instead of day and night strobing; the world's own sun is
    /// taken back as the camera comes down. Why a day a second: 1 h/s's day lasts 24 s - dawns and dusks to watch -, at 1 day/s the whole picture went
    /// light and dark once a second, at 10 days/s ten times. Watching changes nothing: the world advances exactly as it would.
    /// </summary>
    public sealed partial class CameraRig
    {
        public enum LapseState : byte { Off = 0, Rising = 1, Orbit = 2, Returning = 3 }

        /// <summary>A day in under four of the player's seconds: from 1 day/s up (the speeds' steps go from 1 h/s, a day in 24 s, to 1 day/s).</summary>
        public const double LapseEnterDaysPerSecond = 0.25;
        /// <summary>Below this the lapse ends (half the entry: Max's achieved speed wanders about its mean).</summary>
        public const double LapseLeaveDaysPerSecond = 0.12;
        const float RiseSeconds = 3.0f, ReturnSeconds = 3.6f, LightInSeconds = 1.1f, LightOutSeconds = 2.2f;
        /// <summary>One turn round the planet in this many of the player's seconds while the years pass.</summary>
        const float TurnSeconds = 120f;
        /// <summary>The steady sun's longitude east of the camera's (radians): the planet three-quarters lit, its night crescent with its lights at the limb.</summary>
        const double SunEast = 0.95;

        public LapseState Lapse { get; private set; }
        public bool LapseActive => Lapse != LapseState.Off;
        /// <summary>Whether the lapse moves the camera now (rising, watching from orbit, coming back) - not once the player has taken it in a speed's lapse.</summary>
        public bool LapseFlies => Lapse != LapseState.Off && _lapseCam;
        /// <summary>How far the steady light has taken over from the world's sun (0..1).</summary>
        public float LapseLight => _lightK;
        /// <summary>While a span is lived as fast as it goes (a catch-up or a skip) the camera stays at the planet: it may be turned, not brought down - nobody
        /// is drawn while the world lives at speed.</summary>
        bool LapseHoldsCamera => Host.CatchingUp && Lapse != LapseState.Off && Lapse != LapseState.Returning;
        /// <summary>Where the player was when the lapse began (radians; the camera's distance from the planet's centre, m).</summary>
        public double ContextLat, ContextLon, ContextDistance;
        /// <summary>Set by the HUD: where the person it follows is drawn now (false: nobody followed, or they no longer live) - the camera comes back to them.</summary>
        public delegate bool Locate(out double lat, out double lon);
        public Locate FollowLocator;
        /// <summary>A lapse began (the HUD keeps what the player watched and the world as it was) and ended (true: the camera came back to the player's place;
        /// false: the player had taken it).</summary>
        public event System.Action LapseBegan;
        public event System.Action<bool> LapseEnded;

        bool _lapseCam = true;
        float _lapseT, _lightK, _turnPause, _wantFor, _calmFor, _turnK;
        double _fromLat, _fromLon, _fromDist, _orbitLat;
        Vector3 _sunSnap, _sunShown = Vector3.up;
        float _declShown; bool _declSeen;

        /// <summary>The world's days a second of the player's as asked now (a catch-up or a skip: as fast as it goes).</summary>
        double DayRate()
        {
            if (Host.CatchingUp) return double.MaxValue;
            return Host.MaxSpeed ? System.Math.Max(0.0, Host.AchievedSpeed) / SimTime.Day : SimHost.Speeds[Host.SpeedIndex] / (double)SimTime.Day;
        }

        static float Smoother(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6f - 15f) + 10f); }
        static double LogLerp(double a, double b, float t) => System.Math.Exp(System.Math.Log(System.Math.Max(1.0, a)) + (System.Math.Log(System.Math.Max(1.0, b)) - System.Math.Log(System.Math.Max(1.0, a))) * t);

        /// <summary>One frame of the lapse, after the player's input (<paramref name="dLat"/>.. the input's own change this frame).</summary>
        void LapseUpdate(TC2.Core.Simulation.World w, double dLat, double dLon, double dDist)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            double R = w.Planet.Radius, orbit = R + CatchUpAltitude * w.Planet.GeoScale;
            bool moved = dLat != 0 || dLon != 0 || dDist != 0;
            double rate = DayRate();
            bool held = Lapse == LapseState.Rising || Lapse == LapseState.Orbit;
            bool want = Host.CatchingUp || rate >= (held ? LapseLeaveDaysPerSecond : LapseEnterDaysPerSecond);
            // (a moment's dwell before a speed alone sends the camera up or down - keys pressed through 7 to 3 do not fly it; a skip or a catch-up goes at once)
            if (want) { _wantFor += dt; _calmFor = 0f; } else { _calmFor += dt; _wantFor = 0f; }
            bool go = want && (Host.CatchingUp || _wantFor > 0.35f), stop = !want && _calmFor > 0.5f;
            if (moved) _turnPause = 3f;
            _turnPause = Mathf.Max(0f, _turnPause - dt);
            switch (Lapse)
            {
                case LapseState.Off:
                    if (!go) { Host.FrameBudgetOverride = -1f; return; }
                    ContextLat = Lat; ContextLon = Lon; ContextDistance = Distance;
                    BeginRise(R);
                    LapseBegan?.Invoke();
                    break;
                case LapseState.Returning:
                    if (go) { BeginRise(R); break; }   // (faster again on the way down: back up, the player's place kept as it was)
                    break;
                default:
                    if (stop) { BeginReturn(); break; }
                    break;
            }
            if (Lapse == LapseState.Rising || Lapse == LapseState.Orbit)
            {
                // the player's own turning of the planet is kept (added to the flight); in a speed's lapse, coming down or turning away takes the camera back
                if (moved && !LapseHoldsCamera) _lapseCam = false;
                if (moved && _lapseCam) { _fromLat += dLat; _fromLon += dLon; _orbitLat = System.Math.Max(-1.2, System.Math.Min(1.2, _orbitLat + dLat)); }
                _lightK = Mathf.Min(1f, _lightK + dt / LightInSeconds);
                // the slow turn round the planet: eased in, paused a while after the player turns it, never a jolt
                _turnK = Mathf.MoveTowards(_turnK, _turnPause > 0f ? 0f : 1f, dt * 0.5f);
                double turn = DMath.TwoPi / TurnSeconds * dt * _turnK;
                if (Lapse == LapseState.Rising)
                {
                    _lapseT = Mathf.Min(1f, _lapseT + dt / RiseSeconds);
                    if (_lapseCam)
                    {
                        _fromLon += turn * Smoother(_lapseT);
                        float eAlt = Smoother(_lapseT / 0.85f), eLat = Smoother((_lapseT - 0.15f) / 0.85f);
                        Distance = R + LogLerp(_fromDist - R, orbit - R, eAlt);
                        Lat = _fromLat + (_orbitLat - _fromLat) * eLat;
                        Lon = _fromLon;
                    }
                    if (_lapseT >= 1f) Lapse = LapseState.Orbit;
                }
                else if (_lapseCam)
                {
                    Lon += turn;
                    Distance += (orbit - Distance) * (1.0 - System.Math.Exp(-dt / 0.8));
                    if (_turnPause <= 0f) Lat += (_orbitLat - Lat) * (1.0 - System.Math.Exp(-dt / 6.0));
                }
            }
            else if (Lapse == LapseState.Returning)
            {
                _lapseT = Mathf.Min(1f, _lapseT + dt / (_lapseCam ? ReturnSeconds : LightOutSeconds));
                _lightK = 1f - Smoother(_lapseT);
                if (_lapseCam)
                {
                    // back to the player's place - the person followed where they are now - turning the short way round, then down
                    double tLat = ContextLat, tLon = ContextLon;
                    if (FollowLocator != null && FollowLocator(out double fLat, out double fLon)) { tLat = fLat; tLon = fLon; }
                    float eAng = Smoother(_lapseT / 0.72f), eAlt = Smoother((_lapseT - 0.22f) / 0.78f);
                    Lat = _fromLat + (tLat - _fromLat) * eAng;
                    Lon = _fromLon + Wrap(tLon - _fromLon) * eAng;
                    Distance = R + LogLerp(_fromDist - R, ContextDistance - R, eAlt);
                }
                if (_lapseT >= 1f)
                {
                    Lapse = LapseState.Off; _lightK = 0f;
                    Host.FrameBudgetOverride = -1f;
                    LapseEnded?.Invoke(_lapseCam);
                    return;
                }
            }
            // the world's share of each frame: little while the camera flies (smooth frames), more while it watches from orbit, where the picture is cheap
            Host.FrameBudgetOverride = Lapse == LapseState.Orbit ? (Host.CatchingUp ? OrbitCatchUpMs : OrbitWatchMs) : FlightMs;
        }

        /// <summary>The world's ms of a frame while the camera flies up or down, while a catch-up or skip is watched from orbit, and while a speed is.</summary>
        public static float FlightMs = 8f, OrbitCatchUpMs = 55f, OrbitWatchMs = 30f;

        void BeginRise(double R)
        {
            bool fromOff = Lapse == LapseState.Off;
            Lapse = LapseState.Rising; _lapseT = 0f; _lapseCam = true;
            _fromLat = Lat; _fromLon = Lon; _fromDist = System.Math.Max(Distance, R + 30.0);
            // (the orbit over the player's latitude, kept off the poles: the inhabited land turning under the camera)
            _orbitLat = System.Math.Max(-0.62, System.Math.Min(0.62, ContextLat));
            // (the light eased from what it shows now - the world's sun, or wherever it had got to on the way down)
            _sunSnap = _sunShown; _lightK = 0f;
            if (fromOff) _turnK = 0f;
        }

        void BeginReturn()
        {
            Lapse = LapseState.Returning; _lapseT = 0f;
            _fromLat = Lat; _fromLon = Lon; _fromDist = Distance;
        }

        /// <summary>The light shown: the world's sun, or (in a lapse) the steady sun beside the camera - eased in from the sun as it was when the lapse
        /// began, eased out to the world's own as the camera comes down.</summary>
        Vector3 LapseSun(Vector3 real, float decl)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!_declSeen || _lightK <= 0f) { _declShown = decl; _declSeen = true; }
            else _declShown += (decl - _declShown) * (1f - Mathf.Exp(-dt / 2.5f));   // (the seasons' mean: their swing at years a minute is a slow sway)
            Vector3 shown = real;
            if (_lightK > 0f)
            {
                Vector3 steady = PlanetGeo.ToVector(Vec3d.FromLatLon(_declShown, Lon + SunEast));
                Vector3 from = Lapse == LapseState.Returning ? real : _sunSnap;
                if (from.sqrMagnitude < 0.5f) from = steady;
                shown = Vector3.Slerp(from, steady, Smoother(_lightK)).normalized;
            }
            _sunShown = shown;
            return shown;
        }
    }
}
