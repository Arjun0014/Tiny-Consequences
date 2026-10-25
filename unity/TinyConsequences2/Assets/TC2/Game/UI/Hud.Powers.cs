using System;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (F9.1) The god's powers as play: a bar of the powers the world knows (`CommandKind`: lightning, a storm, a quake, an eruption), each back in game
    /// time after its last use - read from the world's command log, so a save or a day away keeps it -, aimed at the ground under the pointer (a ray to
    /// the planet), cast by a click as a logged command at the moment it is given. The view writes nothing: the world applies the command and records it
    /// as YOUR doing, every consequence rooted in it.
    /// </summary>
    public sealed partial class Hud
    {
        struct Power { public string Name, Doing, Help; public CommandKind Kind; public long Arg, Recharge; public double Reach; }

        static readonly Power[] Powers =
        {
            new Power { Name = "Lightning", Doing = "Strike", Kind = CommandKind.GodLightning, Recharge = SimTime.Day, Reach = 60,
                        Help = "a bolt from the sky: it can kindle the woods, the fields or a roof" },
            new Power { Name = "Storm", Doing = "Raise a storm", Kind = CommandKind.GodStorm, Arg = 50, Recharge = 30 * SimTime.Day, Reach = 2000,
                        Help = "a gale of 50 m/s that lives and wanders as storms do: roofs, crops, boats at sea" },
            new Power { Name = "Quake", Doing = "Shake the ground", Kind = CommandKind.GodQuake, Arg = 7, Recharge = 2 * SimTime.Year, Reach = 3000,
                        Help = "a magnitude-7 quake: homes may fall, slopes slide, the sea may come" },
            new Power { Name = "Eruption", Doing = "Split the earth", Kind = CommandKind.GodEruption, Arg = 3, Recharge = 10 * SimTime.Year, Reach = 2500,
                        Help = "the nearest vent within 2.5 km wakes, or the ground opens a new one" },
            new Power { Name = "Plague", Doing = "Send a sickness", Kind = CommandKind.GodPlague, Recharge = 5 * SimTime.Year, Reach = World.GodReach,
                        Help = "a new pestilence breaks out in the nearest village: it spreads as sicknesses do" },
            new Power { Name = "Blight", Doing = "Blight the fields", Kind = CommandKind.GodBlight, Recharge = SimTime.Year, Reach = World.GodReach,
                        Help = "the standing crop of the villages round it fails (nothing when the fields are bare)" },
            new Power { Name = "Drought", Doing = "Withhold the rain", Kind = CommandKind.GodDrought, Recharge = 2 * SimTime.Year, Reach = World.DroughtReach,
                        Help = "no rain for a summer on the land round it: the soil dries, crops wilt, fire takes more readily" },
            new Power { Name = "Rain", Doing = "Bring rain", Kind = CommandKind.GodRain, Recharge = SimTime.Year / 2, Reach = World.RainReach,
                        Help = "a soaking rain for some days on the land round it: the soil drinks, the crops take it, fires go out" },
            new Power { Name = "Bounty", Doing = "Renew the land", Kind = CommandKind.GodBounty, Recharge = 10 * SimTime.Year, Reach = World.GodReach,
                        Help = "the fields round a place as when first broken: their crops full again until they tire" },
            new Power { Name = "Cull", Doing = "Cull the hunters", Kind = CommandKind.GodCull, Recharge = 5 * SimTime.Year, Reach = World.GodReach,
                        Help = "nine in ten of the wolves, foxes and bears round a place (at sea the seals and hunting fish): herds safer, game thicker - until more wander in" },
            new Power { Name = "Bring", Doing = "Bring game", Kind = CommandKind.GodBring, Recharge = 5 * SimTime.Year, Reach = World.GodReach,
                        Help = "the game the place suits best, as thick as it lives where it does best (at sea a shoal): more to hunt - and more mouths on the grass" },
            new Power { Name = "Ore", Doing = "Lay bare a vein of copper", Kind = CommandKind.GodOre, Recharge = 20 * SimTime.Year, Reach = 120,
                        Help = "rich copper ore in the rock at a place: someone must go there and see it - then a mine, smiths and trade may follow" },
            new Power { Name = "Meteor", Doing = "Throw down a stone", Kind = CommandKind.GodMeteor, Recharge = 20 * SimTime.Year, Reach = World.BlastM,
                        Help = "a stone from the sky: what stands near it falls, those at home there die, it burns and the ground shakes" },
        };

        int _aim = -1;                 // the power being aimed (-1 none)
        Vector2 _downAt;               // where the pointer went down (a cast is a click, not a drag of the camera)
        Rect _powersRect;
        GUIStyle _powerOn, _powerOff, _aimLabel;

        /// <summary>Aims a power as its button does (-1 stops aiming): the tests' way in (F9.1).</summary>
        public void Aim(int k) { _aim = k >= 0 && k < Powers.Length ? k : -1; }

        /// <summary>Game seconds until a power can be used again (0: ready) - from the last command of its kind in the world's log.</summary>
        static long Recharging(World w, int k)
        {
            for (int i = w.CommandLog.Count - 1; i >= 0; i--)
                if (w.CommandLog[i].Kind == Powers[k].Kind) return Math.Max(0L, w.CommandLog[i].Time + Powers[k].Recharge - w.Now);
            return 0;
        }

        static string Wait(long s)
        {
            if (s >= SimTime.Year) return (s / (double)SimTime.Year).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " y";
            if (s >= SimTime.Day) return (s / SimTime.Day + 1) + " d";
            return Math.Max(1L, s / SimTime.Hour + 1) + " h";
        }

        /// <summary>The ground under a screen point: the planet's surface the camera's ray meets first (its direction and cell); false over the sky.</summary>
        bool GroundUnder(Vector2 screen, out Vec3d dir, out int cell)
        {
            dir = default; cell = -1;
            var w = Host.World; var P = w.Planet;
            Ray ray = Rig.Cam.ScreenPointToRay(screen);
            double ox = ray.origin.x, oy = ray.origin.y, oz = ray.origin.z, dx = ray.direction.x, dy = ray.direction.y, dz = ray.direction.z;
            double r = P.Radius + P.SeaLevel;
            for (int pass = 0; pass < 2; pass++)   // (the sea's sphere first, then the ground's height where it meets it)
            {
                double b = ox * dx + oy * dy + oz * dz, c = ox * ox + oy * oy + oz * oz - r * r, disc = b * b - c;
                if (disc < 0) return false;
                double t = -b - Math.Sqrt(disc);
                if (t < 0) return false;
                var hit = new Vec3d(ox + t * dx, oy + t * dy, oz + t * dz);
                dir = hit.Normalized;
                r = PlanetGeo.Surface(P, dir).magnitude;
            }
            cell = P.Grid.CellOf(dir);
            return cell >= 0;
        }

        /// <summary>Casts the power being aimed at the ground under the pointer: a command the world applies at once.</summary>
        void Cast()
        {
            var w = Host.World;
            if (_aim < 0 || w == null) return;
            if (Recharging(w, _aim) > 0) { _aim = -1; return; }
            if (!GroundUnder(Input.mousePosition, out _, out int cell)) return;
            var pw = Powers[_aim];
            w.Issue(new Command { Time = w.Now, Kind = pw.Kind, Cell = cell, Arg = pw.Arg });
            _aim = -1;
        }

        /// <summary>Input for the powers (the HUD's Update): a click casts while aiming (not a drag), Escape or the right button stops aiming.
        /// True when the click was the power's (no picking then).</summary>
        bool PowersInput()
        {
            if (Input.GetMouseButtonDown(0)) _downAt = Input.mousePosition;
            if (_aim < 0) return false;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonUp(1)) { _aim = -1; return true; }
            if (Input.GetMouseButtonUp(0) && !PointerOverUi)
            {
                if (((Vector2)Input.mousePosition - _downAt).sqrMagnitude <= 64f) Cast();
                return true;
            }
            return false;
        }

        /// <summary>The bar of powers (bottom centre) and, while aiming, the ring where the power falls and what it is.</summary>
        void PowerBar(World w, float sw, float sh)
        {
            if (_powerOn == null)
            {
                _powerOn = new GUIStyle(_btn) { fontSize = 13, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 5, 5) };
                _powerOn.normal.textColor = new Color(1f, 0.86f, 0.45f);
                _powerOff = new GUIStyle(_btn) { fontSize = 13, padding = new RectOffset(10, 10, 5, 5) };
                _aimLabel = new GUIStyle(_label) { fontSize = 13, alignment = TextAnchor.UpperLeft };
            }
            float bw = 900f, bh = 40f;
            _powersRect = new Rect((sw - bw) * 0.5f, sh - bh - 46f, bw, bh);
            GUILayout.BeginArea(_powersRect, _box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Powers", _small, GUILayout.Width(52));
            for (int k = 0; k < Powers.Length; k++)
            {
                long wait = Recharging(w, k);
                string text = Powers[k].Name + (wait > 0 ? " (" + Wait(wait) + ")" : "");
                GUI.enabled = wait == 0;
                bool on = _aim == k;
                if (GUILayout.Toggle(on, text, on ? _powerOn : _powerOff) != on) _aim = on ? -1 : k;
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            // (over a page, the book or a bar a click is theirs, not the power's: the ring and its words only over open ground - last frame's PointerOverUi)
            if (_aim < 0 || PointerOverUi || !GroundUnder(Input.mousePosition, out var dir, out int cell)) return;
            // the ring where it falls, on the ground round the point aimed at
            var P = w.Planet; var pw = Powers[_aim];
            Vec3d up = dir;
            Vec3d e = Vec3d.Cross(new Vec3d(0, 1, 0), up); if (e.Length < 1e-6) e = new Vec3d(1, 0, 0); e = e.Normalized;
            Vec3d n = Vec3d.Cross(up, e).Normalized;
            double ang = pw.Reach / P.Radius;
            for (int i = 0; i < 32; i++)
            {
                double a = 2 * Math.PI * i / 32;
                Vec3d p = (up * Math.Cos(ang) + (e * Math.Cos(a) + n * Math.Sin(a)) * Math.Sin(ang)).Normalized;
                Vector3 sp = Rig.Cam.WorldToScreenPoint(PlanetGeo.Surface(P, p, 2f));
                if (sp.z <= 0) continue;
                GUI.color = new Color(1f, 0.85f, 0.4f, 0.9f);
                GUI.DrawTexture(new Rect(sp.x / _scale - 2, (Screen.height - sp.y) / _scale - 2, 4, 4), _dot);
                GUI.color = Color.white;
            }
            Vector2 m = Event.current.mousePosition;
            string where = TC2.Core.History.HistoryText.At(w.PlaceName(cell));
            GUI.Label(new Rect(m.x + 18, m.y + 12, 420, 60), "<b>" + pw.Doing + "</b> " + where + "\n<size=11>" + pw.Help + " - click to cast, right-click to stop</size>", _aimLabel);
        }
    }
}
