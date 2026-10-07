using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>A storm: a moving whirl of wind and rain with its own life.</summary>
    public struct Storm
    {
        public int Id;
        public Vec3d At;            // centre (unit vector)
        public Vec3d Heading;       // direction of travel (unit, tangent)
        public float Speed;         // m/s of the centre
        public float Wind;          // strongest sustained wind (m/s)
        public float Radius;        // outer radius of its winds and rain (m)
        public byte Kind;           // 0 a tropical cyclone, 1 a winter storm
        public byte Flags;          // 1 over land at the last step, 2 a landfall was recorded, 4 its peak was recorded
        public float Peak;          // strongest wind it has had
        public long Born;
        public long Event;          // its history event (0 until it matters)
        public long Cause;          // what made it, when not the weather (a god's command)

        public void Write(BinWriter w)
        {
            w.Int(Id); w.Double(At.X); w.Double(At.Y); w.Double(At.Z); w.Double(Heading.X); w.Double(Heading.Y); w.Double(Heading.Z);
            w.Float(Speed); w.Float(Wind); w.Float(Radius); w.Byte(Kind); w.Byte(Flags); w.Float(Peak); w.Long(Born); w.Long(Event); w.Long(Cause);
        }

        public static Storm Read(BinReader r) => new Storm
        {
            Id = r.Int(), At = new Vec3d(r.Double(), r.Double(), r.Double()), Heading = new Vec3d(r.Double(), r.Double(), r.Double()),
            Speed = r.Float(), Wind = r.Float(), Radius = r.Float(), Kind = r.Byte(), Flags = r.Byte(), Peak = r.Float(), Born = r.Long(), Event = r.Long(), Cause = r.Long(),
        };

        public void Hash(ref StateHasher h)
        {
            h.Add(Id); h.Add(At.X); h.Add(At.Y); h.Add(At.Z); h.Add(Heading.X); h.Add(Heading.Y); h.Add(Heading.Z);
            h.Add(Speed); h.Add(Wind); h.Add(Radius); h.Add(Kind); h.Add(Flags); h.Add(Peak); h.Add(Born); h.Add(Event); h.Add(Cause);
        }
    }

    /// <summary>
    /// Storms (05 §3, D2-032): tropical cyclones gather over warm seas in late summer, drift west and curve poleward,
    /// strengthen toward what the sea's warmth allows and die over land or cool water; winter storms form on the polar
    /// front in the cold half of the year and run east. Each storm steps every three hours: its winds (a Rankine vortex
    /// with a calm eye), its rain (added to the next day's weather, so rivers flood), its blowdowns in woods, the crops
    /// it flattens, its surge over low coasts where it comes ashore, and people caught out in the worst of it - each
    /// consequence caused by the storm's own history event (recorded once it becomes a great storm or does harm).
    /// A warmer world breeds more and stronger cyclones.
    /// </summary>
    public static class Storms
    {
        public const long StepSeconds = 3 * SimTime.Hour;
        public const double TropicalPerDay = 0.08;   // per hemisphere at the season's peak
        public const double WinterPerDay = 0.07;
        public const double GreatWind = 33.0;        // m/s: a great storm (hurricane strength) enters the history

        /// <summary>The day's new storms (called by the weather each day; keyed by the day and the hemisphere).</summary>
        public static void Spawn(Simulation.World w, long day, double yearFraction)
        {
            var P = w.Planet;
            bool had = P.Storms.Count > 0;
            for (int hemi = 0; hemi < 2; hemi++)
            {
                double sgn = hemi == 0 ? 1.0 : -1.0;
                // the season: late summer for cyclones, winter for the front (the south has the opposite year)
                double summer = sgn * DMath.Sin(DMath.TwoPi * (yearFraction - 0.205));
                double warmth = DMath.Exp(0.3 * (P.TGlobal - P.TGlobalRef));
                Try(w, day, hemi, 0, TropicalPerDay * DMath.Max(0.0, summer) * warmth, 8, 20, sgn);
                Try(w, day, hemi, 1, WinterPerDay * DMath.Max(0.0, -summer), 38, 56, sgn);
            }
            if (!had && P.Storms.Count > 0) w.Queue.Push(w.Now + StepSeconds, Scheduling.Phase.Process, Scheduling.EventKind.StormStep, 0);
        }

        static void Try(Simulation.World w, long day, int hemi, byte kind, double rate, double latLo, double latHi, double sgn)
        {
            var P = w.Planet;
            ulong key = (ulong)(day * 4 + hemi * 2 + kind);
            if (KeyedRng.Unit(w.Seed, RngStream.Weather, key, 500) >= rate) return;
            double lat = (latLo + (latHi - latLo) * KeyedRng.Unit(w.Seed, RngStream.Weather, key, 501)) * DMath.Deg2Rad * sgn;
            double lon = DMath.TwoPi * KeyedRng.Unit(w.Seed, RngStream.Weather, key, 502);
            Vec3d at = Vec3d.FromLatLon(lat, lon);
            int c = P.Grid.CellOf(at);
            if (kind == 0 && (P.IsLand(c) || SeaWarmth(P, c, w.Now) < 26.0)) return; // cyclones need a warm sea
            Frame(at, out Vec3d east, out Vec3d north);
            Vec3d pole = north * sgn;
            Vec3d heading = kind == 0 ? (east * -0.95 + pole * 0.3).Normalized : (east * 0.95 + pole * 0.2).Normalized;
            w.Metrics.Storms++;
            P.Storms.Add(new Storm
            {
                Id = w.Ids.Next(EntityKind.Process), At = at, Heading = heading, Speed = kind == 0 ? 0.05f : 0.075f,
                Wind = kind == 0 ? 18f : 20f, Radius = kind == 0 ? 1800f : 2600f, Kind = kind, Born = w.Now, Peak = kind == 0 ? 18f : 20f,
            });
        }

        /// <summary>The sea's warmth under a storm today (annual mean and the season).</summary>
        public static double SeaWarmth(PlanetState P, int c, long now)
        {
            double season = DMath.Sin(DMath.TwoPi * (SimTime.YearFraction(now) - 0.25)) * (P.SinLat[c] >= 0 ? 1 : -1);
            return P.TMean[c] + 0.5 * P.TAmp[c] * season; // the sea lags and damps the year
        }

        public static void Frame(Vec3d up, out Vec3d east, out Vec3d north)
        {
            Vec3d e = Vec3d.Cross(Vec3d.UnitY, up);
            if (e.LengthSq < 1e-10) e = Vec3d.Cross(Vec3d.UnitX, up);
            east = e.Normalized;
            north = Vec3d.Cross(up, east).Normalized;
        }

        /// <summary>Wind (m/s) at a distance from the centre: calm eye, strongest at the eyewall, falling off beyond.</summary>
        public static double WindAt(in Storm s, double r)
        {
            double rm = 0.2 * s.Radius;
            if (r >= s.Radius) return 0;
            if (r < rm) return s.Wind * r / rm;                                   // the eye is calm, the eyewall fiercest
            return s.Wind * DMath.Exp(0.6 * DMath.Log(rm / r)) * (1.0 - DMath.Max(0.0, (r - 0.8 * s.Radius) / (0.2 * s.Radius)));
        }

        /// <summary>Move a storm and let the sea feed it or the land and the cold starve it (dt seconds).</summary>
        public static void Move(PlanetState P, ref Storm s, double dt, long now)
        {
            Frame(s.At, out Vec3d east, out Vec3d north);
            double sgn = s.At.Y >= 0 ? 1.0 : -1.0;
            double lat = DMath.Abs(s.At.Latitude) * 180.0 / DMath.PI;
            // steering: cyclones curve poleward and, past the subtropics, east with the westerlies; fronts run east
            Vec3d steer = s.Kind == 0
                ? (lat < 24 ? east * -0.9 + north * (0.35 * sgn) : east * 0.8 + north * (0.45 * sgn))
                : east * 0.95 + north * (0.15 * sgn);
            Vec3d h = s.Heading + steer.Normalized * (dt / (18.0 * SimTime.Hour));
            h = h - s.At * Vec3d.Dot(h, s.At);
            s.Heading = h.Normalized;
            double a = s.Speed * dt / PlanetState.GeoRadius;
            Vec3d axis = Vec3d.Cross(s.At, s.Heading).Normalized;
            s.At = Tectonics.Rotate(s.At, axis, DMath.Cos(a), DMath.Sin(a));
            s.Heading = Tectonics.Rotate(s.Heading, axis, DMath.Cos(a), DMath.Sin(a));
            int c = P.Grid.CellOf(s.At);
            double days = dt / SimTime.Day;
            if (s.Kind == 0)
            {
                if (P.IsLand(c)) s.Wind = (float)(s.Wind * DMath.Exp(-days / 0.6));
                else
                {
                    double pot = DMath.Clamp((SeaWarmth(P, c, now) - 24.5) * 14.0 + 20.0, 0.0, 78.0); // ~41 m/s at 26 C, ~69 at 28 C
                    double tau = pot > s.Wind ? 1.4 : 1.0;
                    s.Wind = (float)(s.Wind + (pot - s.Wind) * (1.0 - DMath.Exp(-days / tau)));
                }
            }
            else
            {
                double age = (now - s.Born) / (double)SimTime.Day;
                double target = age < 1.2 ? 34.0 : 12.0;
                s.Wind = (float)(s.Wind + (target - s.Wind) * (1.0 - DMath.Exp(-days / 1.3)));
                s.Radius = (float)DMath.Min(3400.0, s.Radius + 300.0 * days);
            }
            if (s.Wind > s.Peak) s.Peak = s.Wind;
        }

        /// <summary>Has the storm blown itself out?</summary>
        public static bool Spent(in Storm s, long now) => s.Wind < 14f || now - s.Born > 10 * SimTime.Day;
    }
}
