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
        struct Power { public string Name, Doing, Help; public CommandKind Kind; public long Arg, Recharge; public double Reach; public Color Col; }

        // two rows: the sky and the ground's powers, then life's and the land's (Lightning stays first: the Web test aims power 0)
        static readonly Power[] Powers =
        {
            new Power { Name = "Lightning", Col = new Color(1.00f, 0.93f, 0.55f), Doing = "Strike", Kind = CommandKind.GodLightning, Recharge = SimTime.Day, Reach = 60,
                        Help = "a bolt from the sky: it can kindle the woods, the fields or a roof" },
            new Power { Name = "Storm", Col = new Color(0.55f, 0.66f, 0.85f), Doing = "Raise a storm", Kind = CommandKind.GodStorm, Arg = 50, Recharge = 30 * SimTime.Day, Reach = 2000,
                        Help = "a gale of 50 m/s that lives and wanders as storms do: roofs, crops, boats at sea" },
            new Power { Name = "Rain", Col = new Color(0.40f, 0.68f, 1.00f), Doing = "Bring rain", Kind = CommandKind.GodRain, Recharge = SimTime.Year / 2, Reach = World.RainReach,
                        Help = "a soaking rain for some days on the land round it: the soil drinks, the crops take it, fires go out" },
            new Power { Name = "Drought", Col = new Color(0.95f, 0.70f, 0.30f), Doing = "Withhold the rain", Kind = CommandKind.GodDrought, Recharge = 2 * SimTime.Year, Reach = World.DroughtReach,
                        Help = "no rain for a summer on the land round it: the soil dries, crops wilt, fire takes more readily" },
            new Power { Name = "Warmth", Col = new Color(1.00f, 0.55f, 0.25f), Doing = "Warm the air", Kind = CommandKind.GodWarm, Recharge = 2 * SimTime.Year, Reach = World.AirReach,
                        Help = "a summer 5 C warmer on the land round it: crops grow where it is cold (and wilt where it is hot), the soil dries, homes burn less wood" },
            new Power { Name = "Cold", Col = new Color(0.70f, 0.90f, 1.00f), Doing = "Chill the air", Kind = CommandKind.GodCold, Recharge = 2 * SimTime.Year, Reach = World.AirReach,
                        Help = "a summer 5 C colder on the land round it: crops grow slowly or fail where it is cool, homes burn more wood, frost keeps the herds in" },
            new Power { Name = "Quake", Col = new Color(0.80f, 0.58f, 0.36f), Doing = "Shake the ground", Kind = CommandKind.GodQuake, Arg = 7, Recharge = 2 * SimTime.Year, Reach = 3000,
                        Help = "a magnitude-7 quake: homes may fall, slopes slide, the sea may come" },
            new Power { Name = "Eruption", Col = new Color(1.00f, 0.38f, 0.15f), Doing = "Split the earth", Kind = CommandKind.GodEruption, Arg = 3, Recharge = 10 * SimTime.Year, Reach = 2500,
                        Help = "the nearest vent within 2.5 km wakes, or the ground opens a new one" },
            new Power { Name = "Meteor", Col = new Color(1.00f, 0.62f, 0.22f), Doing = "Throw down a stone", Kind = CommandKind.GodMeteor, Recharge = 20 * SimTime.Year, Reach = World.BlastM,
                        Help = "a stone from the sky: what stands near it falls, those at home there die, it burns and the ground shakes" },
            new Power { Name = "Plague", Col = new Color(0.66f, 0.85f, 0.30f), Doing = "Send a sickness", Kind = CommandKind.GodPlague, Recharge = 5 * SimTime.Year, Reach = World.GodReach,
                        Help = "a new pestilence breaks out in the nearest village: it spreads as sicknesses do" },
            new Power { Name = "Blight", Col = new Color(0.70f, 0.66f, 0.30f), Doing = "Blight the fields", Kind = CommandKind.GodBlight, Recharge = SimTime.Year, Reach = World.GodReach,
                        Help = "the standing crop of the villages round it fails (nothing when the fields are bare)" },
            new Power { Name = "Bounty", Col = new Color(0.85f, 0.90f, 0.40f), Doing = "Renew the land", Kind = CommandKind.GodBounty, Recharge = 10 * SimTime.Year, Reach = World.GodReach,
                        Help = "the fields round a place as when first broken: their crops full again until they tire" },
            new Power { Name = "Cull", Col = new Color(0.85f, 0.35f, 0.30f), Doing = "Cull the hunters", Kind = CommandKind.GodCull, Recharge = 5 * SimTime.Year, Reach = World.GodReach,
                        Help = "nine in ten of the wolves, foxes and bears round a place (at sea the seals and hunting fish): herds safer, game thicker - until more wander in" },
            new Power { Name = "Bring", Col = new Color(0.50f, 0.88f, 0.55f), Doing = "Bring game", Kind = CommandKind.GodBring, Recharge = 5 * SimTime.Year, Reach = World.GodReach,
                        Help = "the game the land round it lacks most - a herd hunted out comes back - as thick as it lives over its range (at sea a shoal): more to hunt, and more mouths on the grass" },
            new Power { Name = "Ore", Col = new Color(0.40f, 0.85f, 0.70f), Doing = "Lay bare a vein of copper", Kind = CommandKind.GodOre, Recharge = 20 * SimTime.Year, Reach = 120,
                        Help = "rich copper ore in the rock at a place: someone must go there and see it - then a mine, smiths and trade may follow" },
        };
        const int FirstRow = 9;

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
            // (session 27: the powers as play) the hand seen: a burst where it fell, the ground shaken by the heaviest
            // (the meteor: the stone seen falling to where the world strikes - the cell's heart, where its fires and its scar will be - and the burst and the
            // shaking when it lands)
            float shake = pw.Kind == CommandKind.GodMeteor ? 1f : pw.Kind == CommandKind.GodQuake ? 0.85f : pw.Kind == CommandKind.GodEruption ? 0.7f : pw.Kind == CommandKind.GodStorm ? 0.3f : pw.Kind == CommandKind.GodLightning ? 0.25f : 0.1f;
            bool meteor = pw.Kind == CommandKind.GodMeteor;
            if (meteor) AimView.Cast(w.Planet.Grid.Center[cell], pw.Reach, pw.Col, shake, true);
            else if (GroundUnder(Input.mousePosition, out var at, out _)) AimView.Cast(at, pw.Reach, pw.Col, shake);
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

        // (session 27: the powers as play) your hand: the last casts and what has come of each so far, read from the ledger once a second
        readonly System.Collections.Generic.List<(long id, string what, long time, string came, int n)> _hand = new System.Collections.Generic.List<(long, string, long, string, int)>();
        float _handAt;
        GUIStyle _handLine, _tipStyle;
        GUIContent _tip;
        Rect _tipRect;

        /// <summary>The pending tooltip of the power under the pointer, drawn over everything else at the end of the HUD's frame.</summary>
        void DrawTip() { if (_tip != null && Event.current.type == EventType.Repaint) GUI.Box(_tipRect, _tip, _tipStyle); _tip = null; }

        void YourHand(World w, float sw, float sh)
        {
            if (Time.unscaledTime - _handAt > 1f)
            {
                _handAt = Time.unscaledTime;
                _hand.Clear();
                var h = w.History;
                for (int i = h.Count - 1, seen = 0; i >= 0 && seen < 40000 && _hand.Count < 3; i--, seen++)
                {
                    var e = h.At(i);
                    if (w.Now - e.Time > 10 * SimTime.Year) break;
                    if (e.Kind != TC2.Core.History.HistoryKind.GodCommand) continue;
                    string what = TC2.Core.History.HistoryText.Describe(w, in e).TrimEnd('.');
                    if (what.StartsWith("You ", StringComparison.Ordinal)) what = char.ToUpperInvariant(what[4]) + what.Substring(5);
                    string came = TC2.Core.History.HistoryBook.CameOf(w, e.Id, out int n);
                    _hand.Add((e.Id, what, e.Time, came, n));
                }
            }
            if (_hand.Count == 0) return;
            if (_handLine == null) { _handLine = new GUIStyle(_small) { fontSize = 12, wordWrap = true, richText = true, padding = new RectOffset(4, 4, 2, 3) }; _handLine.hover.textColor = new Color(1f, 0.86f, 0.45f); }
            float hw = 420f, lineH = 34f;
            var r = new Rect(_powersRect.x, _powersRect.y - 10f - (lineH * _hand.Count + 22f), hw, lineH * _hand.Count + 22f);
            GUILayout.BeginArea(r, _box);
            GUILayout.Label("<b>Your hand</b>  <size=11>what has come of it so far - the book's last part tells all</size>", _small);
            for (int k = 0; k < _hand.Count; k++)
            {
                var x = _hand[k];
                long ago = w.Now - x.time;
                string when = ago < SimTime.Day ? "today" : ago < SimTime.Year ? (ago / SimTime.Day) + " days ago" : (ago / (double)SimTime.Year).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " years ago";
                string line = "<b>" + x.what + "</b> <size=11>(" + when + ")</size>\n" + (x.came.Length > 0 ? x.came : "<i>nothing yet that the world remembers</i>");
                if (GUILayout.Button(line, _handLine)) OpenBookAt(TC2.Core.History.HistoryBook.YourActionsPart, k);
            }
            GUILayout.EndArea();
        }

        /// <summary>(session 27) The places a power aimed here would reach, by the world's own measure: the villages within its reach (the nearest
        /// within it for a sickness), with their people - what the player is about to touch.</summary>
        static string InReach(World w, Vec3d dir, in Power pw)
        {
            var near = new System.Collections.Generic.List<(double d, int sid)>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double d = Vec3d.ArcDistance(dir, s.Anchor, w.Planet.Radius);
                if (d <= pw.Reach) near.Add((d, sid));
            }
            if (near.Count == 0) return "no village within its reach";
            near.Sort((a, b) => a.d.CompareTo(b.d));
            if (pw.Kind == CommandKind.GodPlague) near.RemoveRange(1, near.Count - 1);
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < near.Count && i < 4; i++)
                names.Add(w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(near[i].sid)) + " (" + w.Settlements.Get(near[i].sid).Population + ")");
            return (pw.Kind == CommandKind.GodPlague ? "it would break out in " : "within its reach: ") + string.Join(", ", names.ToArray()) + (near.Count > 4 ? " and " + (near.Count - 4) + " more" : "");
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
            float bw = 960f, bh = 78f;
            _powersRect = new Rect((sw - bw) * 0.5f, sh - bh - 44f, bw, bh);
            GUI.Box(_powersRect, GUIContent.none, _box);
            var area = new Rect(_powersRect.x + 8f, _powersRect.y + 6f, _powersRect.width - 16f, _powersRect.height - 10f);
            GUILayout.BeginArea(area);
            int hovered = -1; Rect hoveredRect = Rect.zero;
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(row == 0 ? "<b>Powers</b>" : "", _small, GUILayout.Width(70));   // (the casts' consequences are "Your hand", above the bar)
                for (int k = row == 0 ? 0 : FirstRow; k < (row == 0 ? FirstRow : Powers.Length); k++)
                {
                    long wait = Recharging(w, k);
                    var pw = Powers[k];
                    bool on = _aim == k;
                    var content = new GUIContent(pw.Name, pw.Help + (wait > 0 ? " - back in " + Wait(wait) : ""));
                    Rect br = GUILayoutUtility.GetRect(content, on ? _powerOn : _powerOff, GUILayout.MinWidth(72));
                    GUI.enabled = wait == 0;
                    if (GUI.Toggle(br, on, content, on ? _powerOn : _powerOff) != on) _aim = on ? -1 : k;
                    GUI.enabled = true;
                    if (br.Contains(Event.current.mousePosition)) { hovered = k; hoveredRect = br; }
                    if (Event.current.type == EventType.Repaint)
                    {
                        // its colour on its left edge; while it comes back, how far it has come, along its foot
                        GUI.color = wait > 0 ? new Color(pw.Col.r, pw.Col.g, pw.Col.b, 0.35f) : pw.Col;
                        GUI.DrawTexture(new Rect(br.x + 1f, br.y + 3f, 3f, br.height - 6f), _dot);
                        if (wait > 0)
                        {
                            float done = 1f - Mathf.Clamp01(wait / (float)pw.Recharge);
                            GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(br.x + 4f, br.yMax - 4f, br.width - 8f, 2f), _dot);
                            GUI.color = pw.Col; GUI.DrawTexture(new Rect(br.x + 4f, br.yMax - 4f, (br.width - 8f) * done, 2f), _dot);
                        }
                        GUI.color = Color.white;
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
            // (session 27) the power under the pointer told above the bar: what it does, and when it is back (its tooltip was never drawn)
            if (hovered >= 0 && _aim < 0 && Event.current.type == EventType.Repaint)
            {
                var hp = Powers[hovered];
                long hw = Recharging(w, hovered);
                if (_tipStyle == null) _tipStyle = new GUIStyle(_mapTag) { wordWrap = true, padding = new RectOffset(9, 9, 6, 7) };
                var tc = new GUIContent("<b>" + hp.Doing + "</b>: " + hp.Help + (hw > 0 ? "\n<color=#f2c35f>back in " + Wait(hw) + "</color>" : "") + "\n<size=11><i>click, then click the ground where it falls</i></size>");
                float tw = 400f, th = _tipStyle.CalcHeight(tc, tw);
                float cx = area.x + hoveredRect.center.x;
                _tip = tc; _tipRect = new Rect(Mathf.Clamp(cx - tw * 0.5f, 4f, sw - tw - 4f), _powersRect.y - th - 8f, tw, th);   // (drawn last: over "Your hand")
            }
            // (over a page, the book or a bar a click is theirs, not the power's: the mark and its words only over open ground - last frame's PointerOverUi)
            if (_aim < 0 || PointerOverUi || !GroundUnder(Input.mousePosition, out var dir, out int cell)) return;
            // (session 27) its reach laid on the ground (AimView), and the villages it would reach named
            var P = w.Planet; var pwr = Powers[_aim];
            AimView.Aim(dir, pwr.Reach, pwr.Col);
            string reach = InReach(w, dir, pwr);
            Vector2 m = Event.current.mousePosition;
            string where = TC2.Core.History.HistoryText.At(w.PlaceName(cell));
            GUI.Label(new Rect(m.x + 18, m.y + 12, 460, 90), "<b>" + pwr.Doing + "</b> " + where + (reach.Length > 0 ? "\n<size=12>" + reach + "</size>" : "") + "\n<size=11>" + pwr.Help + " - click to cast, right-click to stop</size>", _aimLabel);
        }
    }
}
