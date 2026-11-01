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
        // (session 31) each its seal's picture, in the bar's order; the bar's three groups end after these places
        static readonly string[] PowerIcons = { "lightning", "storm", "rain", "drought", "warmth", "cold", "quake", "eruption", "meteor", "plague", "blight", "bounty", "cull", "bring", "ore" };
        static readonly int[] PowerGroupEnds = { 6, 9, 15 };
        static readonly string[] PowerGroupNames = { "The sky", "The ground", "Life and land" };

        int _aim = -1;                 // the power being aimed (-1 none)
        Vector2 _downAt;               // where the pointer went down (a cast is a click, not a drag of the camera)
        Rect _powersRect;
        GUIStyle _coolWord;

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
            // (session 29) no uncertainty whether a click worked: a cast refused says why, a cast made says what and where
            long back = Recharging(w, _aim);
            if (back > 0) { CastNote(Powers[_aim].Name + " is not back yet - in " + Wait(back), false); _aim = -1; return; }
            if (!GroundUnder(Input.mousePosition, out var aimDir, out int cell)) { CastNote("Aim at the ground - the sky has nothing to strike", false); return; }
            var pw = Powers[_aim];
            // (session 31) a cast that could touch nothing is not spent: it says why
            string refused = Refused(w, aimDir, pw);
            if (refused != null) { CastNote(refused, false); AudioView.Ui("tick"); return; }
            w.Issue(new Command { Time = w.Now, Kind = pw.Kind, Cell = cell, Arg = pw.Arg });
            CastNote(pw.Doing + " " + TC2.Core.History.HistoryText.At(w.PlaceName(cell)) + " - what comes of it will show in Your hand", true);
            // (session 27: the powers as play) the hand seen: a burst where it fell, the ground shaken by the heaviest
            // (the meteor: the stone seen falling to where the world strikes - the cell's heart, where its fires and its scar will be - and the burst and the
            // shaking when it lands)
            AudioView.Ui("cast");
            float shake = pw.Kind == CommandKind.GodMeteor ? 1f : pw.Kind == CommandKind.GodQuake ? 0.85f : pw.Kind == CommandKind.GodEruption ? 0.7f : pw.Kind == CommandKind.GodStorm ? 0.3f : pw.Kind == CommandKind.GodLightning ? 0.25f : 0.1f;
            bool meteor = pw.Kind == CommandKind.GodMeteor;
            if (meteor) { AimView.Cast(w.Planet.Grid.Center[cell], pw.Reach, pw.Col, shake, true); AudioView.MeteorFall(AimView.FallTime); }
            else if (GroundUnder(Input.mousePosition, out var at, out _)) AimView.Cast(at, pw.Reach, pw.Col, shake);
            _aim = -1;
        }

        string _castNote; float _castNoteAt = -99f; bool _castOk;
        void CastNote(string text, bool ok) { _castNote = text; _castNoteAt = Time.unscaledTime; _castOk = ok; }

        /// <summary>(session 29) The last cast's word over the bar: made (gold) or refused (pale), fading after four seconds. Drawn after Your hand,
        /// between what stands over the bar's ends - Your hand or the History panel on its left, a page's foot on its right - else above them (s29book5:
        /// centred over the bar, it ran under Your hand, drawn after it).</summary>
        void DrawCastNote(float sw)
        {
            _noteRect = Rect.zero;
            if (_castNote == null || Time.unscaledTime - _castNoteAt > 4f || _powersRect.width <= 0f) return;
            float a = Mathf.Clamp01((4f - (Time.unscaledTime - _castNoteAt)) / 1.2f);
            if (_tipStyle == null) _tipStyle = new GUIStyle(_mapTag) { wordWrap = true, padding = new RectOffset(9, 9, 6, 7) };
            var c = new GUIContent((_castOk ? "<color=#f2c35f><b>" : "<color=#d8d4c8><b>") + _castNote + "</b></color>");
            float left = Mathf.Max(4f, Mathf.Max(_handRect.xMax, _bottom.xMax) + 8f), right = (_pageRect.width > 0f ? _pageRect.x : sw) - 8f;
            float tw = Mathf.Min(560f, sw - 20f), x = sw * 0.5f - tw * 0.5f, floor = _powersRect.y;
            if (right - left >= Mathf.Min(tw, 320f)) { tw = Mathf.Min(tw, right - left); x = Mathf.Clamp(x, left, right - tw); }
            else floor = Mathf.Min(floor, Mathf.Min(_handRect.width > 0f ? _handRect.y : floor, _bottom.width > 0f ? _bottom.y : floor));   // (no room beside them)
            float th = _tipStyle.CalcHeight(c, tw);
            _noteRect = new Rect(x, floor - th - 6f, tw, th);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Box(_noteRect, c, _tipStyle);
            GUI.color = Color.white;
        }

        /// <summary>Input for the powers (the HUD's Update): a click casts while aiming (not a drag), Escape or the right button stops aiming.
        /// True when the click was the power's (no picking then).</summary>
        bool PowersInput()
        {
            if (Input.GetMouseButtonDown(0)) _downAt = Input.mousePosition;
            if (_aim < 0) return false;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonUp(1)) { _aim = -1; AudioView.Ui("tick"); return true; }
            if (Input.GetMouseButtonUp(0) && !PointerOverUi)
            {
                if (((Vector2)Input.mousePosition - _downAt).sqrMagnitude <= 64f) Cast();
                return true;
            }
            return false;
        }

        // (session 27: the powers as play) your hand: the last casts and what has come of each so far, read from the ledger once a second
        readonly System.Collections.Generic.List<(long id, string what, long time, string came, int n)> _hand = new System.Collections.Generic.List<(long, string, long, string, int)>();
        readonly System.Collections.Generic.List<string> _handThread = new System.Collections.Generic.List<string>();   // (session 29: each cast's longest thread so far)
        float _handAt;
        GUIStyle _handLine, _tipStyle;
        string[] _handText = new string[3];
        GUIContent _tip;
        Rect _tipRect, _tipShown, _aimRect, _noteRect;   // (session 29: the tip, the aim's words and the cast's word as drawn this frame - no map name under them)
        GUIStyle _aimBox;

        /// <summary>The pending tooltip of the power under the pointer, drawn over everything else at the end of the HUD's frame.</summary>
        void DrawTip() { _tipShown = _tip != null ? _tipRect : Rect.zero; if (_tip != null && Event.current.type == EventType.Repaint) GUI.Box(_tipRect, _tip, _tipStyle); _tip = null; }

        void YourHand(World w, float sw, float sh)
        {
            if (Time.unscaledTime - _handAt > 1f)
            {
                _handAt = Time.unscaledTime;
                _hand.Clear(); _handThread.Clear();
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
                    string thread = TC2.Core.History.HistoryBook.Thread(w, e.Id, out int ty, out int depth);
                    if (thread.Length > 150) thread = thread.Substring(0, 147) + "...";
                    _handThread.Add(thread.Length > 0 ? "<size=13><color=#f2c35f>Its longest thread, " + depth + " links" + (ty > 0 ? ", " + ty + (ty == 1 ? " year" : " years") + " on" : "") + ":</color> " + thread + "</size>" : "");
                }
            }
            if (_hand.Count == 0) return;
            if (_handLine == null) { _handLine = new GUIStyle(_small) { fontSize = 14, wordWrap = true, richText = true, padding = new RectOffset(4, 4, 2, 3) }; _handLine.hover.textColor = new Color(1f, 0.86f, 0.45f); }
            // (session 31: progressive disclosure) a seal at the left foot while nothing has just come of it; under the pointer, or for a while after a cast, the leaf with what came
            float now = Time.unscaledTime;
            var seal = new Rect(14f, sh - 60f, 46f, 46f);
            Vector2 mp = Event.current.mousePosition;
            bool overLeaf = _handOpen.width > 0f && _handOpen.Contains(mp);
            long newest = _hand[0].id;
            if (newest != _handNewest) { _handNewest = newest; _handShowUntil = now + 14f; }
            bool show = now < _handShowUntil || overLeaf || seal.Contains(mp);
            if (show && overLeaf) _handShowUntil = Mathf.Max(_handShowUntil, now + 1.5f);
            if (Seal(seal, "why", show, false, new Color(1f, 0.92f, 0.70f), out bool overHand)) _handShowUntil = show ? 0f : now + 30f;
            _handRect = seal;
            if (!show) { _handOpen = Rect.zero; return; }
            float hw = Mathf.Min(Mathf.Max(300f, _powersRect.x - 24f), 420f), inner = hw - _box.padding.horizontal;
            const string head = "<b>Your hand</b>  <i>what has come of it so far</i>";
            float hh = _small.CalcHeight(new GUIContent(head), inner) + _box.padding.vertical + 4f;
            if (_handText.Length < _hand.Count) _handText = new string[3];
            for (int k = 0; k < _hand.Count; k++)
            {
                var x = _hand[k];
                long ago = w.Now - x.time;
                long days = ago / SimTime.Day;   // ("(1 days ago)": s30pow1)
                string when = ago < SimTime.Day ? "today" : ago < SimTime.Year ? (days == 1 ? "yesterday" : days + " days ago") : (ago / (double)SimTime.Year).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + (ago < 2 * SimTime.Year && ago / (double)SimTime.Year < 1.05 ? " year ago" : " years ago");
                _handText[k] = "<b>" + x.what + "</b>  <i>" + when + "</i>\n" + (x.came.Length > 0 ? x.came : "<i>nothing yet that the world remembers</i>") + (k < _handThread.Count && _handThread[k].Length > 0 ? "\n" + _handThread[k] : "");
                hh += _handLine.CalcHeight(new GUIContent(_handText[k]), inner) + _handLine.margin.vertical;
            }
            float bottom = seal.x + hw + 8f <= _powersRect.x ? seal.y - 6f : Mathf.Min(seal.y - 6f, _powersRect.y - 6f);
            var r = new Rect(14f, bottom - hh, hw, hh);
            _handRect = Rect.MinMaxRect(r.xMin, r.yMin, Mathf.Max(r.xMax, seal.xMax), seal.yMax);
            _handOpen = _handRect;
            GUILayout.BeginArea(r, _box);
            GUILayout.Label(head, _small);
            for (int k = 0; k < _hand.Count; k++)
                if (GUILayout.Button(_handText[k], _handLine)) { OpenBookAt(TC2.Core.History.HistoryBook.YourActionsPart, k); AudioView.Ui("page"); }
            GUILayout.EndArea();
        }
        Rect _handOpen; long _handNewest; float _handShowUntil;

        /// <summary>(session 31) Why a power aimed here would touch nothing, by the world's own measure (null: it would): a sickness or a blight wants a village in reach
        /// (a blight a standing crop); bounty, tired fields. The aim shows it in grey and its words say so.</summary>
        static string Refused(World w, Vec3d dir, in Power pw)
        {
            if (pw.Kind != CommandKind.GodPlague && pw.Kind != CommandKind.GodBlight && pw.Kind != CommandKind.GodBounty) return null;
            bool any = false, crop = false, tired = false;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || Vec3d.ArcDistance(dir, s.Anchor, w.Planet.Radius) > World.GodReach) continue;
                any = true;
                if (s.Crop == TC2.Core.Places.CropStage.Sown || s.Crop == TC2.Core.Places.CropStage.Growing || s.Crop == TC2.Core.Places.CropStage.Ripe) crop = true;
                if (s.Tired >= 0.02) tired = true;
            }
            if (!any) return "No village within its reach here: it would touch nobody";
            if (pw.Kind == CommandKind.GodBlight && !crop) return "No crop stands within its reach now: the fields are bare";
            if (pw.Kind == CommandKind.GodBounty && !tired) return "The fields within its reach are not tired: there is nothing to renew";
            return null;
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
                if (d > pw.Reach + 450.0) continue;
                if (d > pw.Reach)
                {
                    // (session 27) a village is within reach where its buildings are, not only its square (a lightning aimed inside a town had said "no
                    // village within its reach")
                    var bl = w.BuildingsOf(sid);
                    for (int i = 0; i < bl.Count && d > pw.Reach; i++)
                    {
                        ref var b = ref w.Buildings.Get(bl[i]);
                        d = System.Math.Min(d, Vec3d.ArcDistance(dir, s.LocalToSphere(b.X, b.Y, w.Planet.Radius), w.Planet.Radius));
                    }
                }
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
            // (session 31) a row of seals in three groups with no panel behind them - the world stays the picture; lit under the pointer, on the aim; while one comes
            // back, its seal dull with its colour rising from the foot and how long it has to go
            float size = Mathf.Clamp((sw - 60f) / 17.4f, 34f, 50f), step = size + 4f, groupGap = 18f;
            float bw = Powers.Length * step + (PowerGroupEnds.Length - 1) * groupGap + 8f, bh = size + 26f;
            _powersRect = new Rect((sw - bw) * 0.5f, sh - bh - 8f, bw, bh);
            if (Event.current.type == EventType.Repaint) { var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.8f); GUI.DrawTexture(new Rect(0f, sh - bh - 46f, sw, bh + 46f), Ui("shade_foot"), ScaleMode.StretchToFill, true); GUI.color = o; }
            int hovered = -1; Rect hoveredRect = Rect.zero;
            float x = _powersRect.x + 4f, sealY = _powersRect.y + 20f;
            int group = 0;
            for (int k = 0; k < Powers.Length; k++)
            {
                if (k == 0 || k == PowerGroupEnds[group])
                {
                    if (k > 0) { group++; x += groupGap; }
                    int end = PowerGroupEnds[group], first = group == 0 ? 0 : PowerGroupEnds[group - 1];
                    var cap = new Rect(x, _powersRect.y + 1f, (end - first) * step - 4f, 18f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        Vector2 ts = _capLabel.CalcSize(new GUIContent(PowerGroupNames[group]));
                        var old = GUI.color; GUI.color = new Color(0.72f, 0.60f, 0.38f, 0.75f * old.a);
                        float mid = cap.y + 9f, lx0 = cap.center.x - ts.x * 0.5f - 6f, lx1 = cap.center.x + ts.x * 0.5f + 6f;
                        GUI.DrawTexture(new Rect(cap.x + 4f, mid, Mathf.Max(0f, lx0 - cap.x - 4f), 1f), _dot);
                        GUI.DrawTexture(new Rect(lx1, mid, Mathf.Max(0f, cap.xMax - 4f - lx1), 1f), _dot);
                        GUI.color = old;
                    }
                    Edged(cap, PowerGroupNames[group], _capLabel);
                }
                long wait = Recharging(w, k);
                var pw = Powers[k];
                bool on = _aim == k;
                var br = new Rect(x, sealY, size, size);
                if (k == 0) _power0Rect = br;
                if (Seal(br, PowerIcons[k], on, wait > 0, Color.Lerp(pw.Col, new Color(1f, 0.97f, 0.88f), 0.55f), out bool over))
                {
                    if (wait > 0) CastNote(pw.Name + " is not back yet - in " + Wait(wait), false);
                    else _aim = on ? -1 : k;
                }
                if (over) { hovered = k; hoveredRect = br; }
                if (wait > 0 && Event.current.type == EventType.Repaint)
                {
                    // how far it has come back: its colour rising inside the seal; the wait in figures
                    float done = 1f - Mathf.Clamp01(wait / (float)pw.Recharge);
                    var inner = Inset(br, size * 0.09f);
                    var old = GUI.color;
                    GUI.color = new Color(pw.Col.r, pw.Col.g, pw.Col.b, 0.34f * old.a);
                    float fh = inner.height * done;
                    GUI.BeginGroup(new Rect(inner.x, inner.yMax - fh, inner.width, fh));
                    GUI.DrawTexture(new Rect(0f, fh - inner.height, inner.width, inner.height), Ui("disc"));
                    GUI.EndGroup();
                    GUI.color = old;
                    if (_coolWord == null) { _coolWord = new GUIStyle(_mapSub) { fontSize = 12, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, wordWrap = false }; }
                    Edged(new Rect(br.x - 6f, br.yMax - 15f, br.width + 12f, 16f), Wait(wait), _coolWord);
                }
                x += step;
            }
            // (session 27) the power under the pointer told above the bar: what it does, and when it is back
            if (hovered >= 0 && _aim < 0 && Event.current.type == EventType.Repaint)
            {
                var hp = Powers[hovered];
                long hw = Recharging(w, hovered);
                if (_tipStyle == null) _tipStyle = new GUIStyle(_mapTag) { wordWrap = true };
                var tc = new GUIContent("<size=17><b>" + hp.Name + "</b></size>\n" + Cap(hp.Help) + "." + (hw > 0 ? "\n<color=#f2c35f>Back in " + Wait(hw) + ".</color>" : "\n<i><color=#c9c3b4>Click it, then click the ground where it falls.</color></i>"));
                float tw = 380f, th = _tipStyle.CalcHeight(tc, tw);
                _tip = tc; _tipRect = new Rect(Mathf.Clamp(hoveredRect.center.x - tw * 0.5f, 4f, sw - tw - 4f), _powersRect.y - th - 6f, tw, th);   // (drawn last: over "Your hand")
            }
            // (over a page, the book or a bar a click is theirs, not the power's: the mark and its words only over open ground - last frame's PointerOverUi)
            if (_aim < 0 || PointerOverUi || !GroundUnder(Input.mousePosition, out var dir, out int cell)) return;
            // (session 27) its reach laid on the ground (AimView), and the villages it would reach named
            var P = w.Planet; var pwr = Powers[_aim];
            string nothing = Refused(w, dir, pwr);
            AimView.Aim(dir, pwr.Reach, nothing != null ? new Color(0.62f, 0.60f, 0.58f) : pwr.Col);   // (grey: it would touch nothing here)
            string reach = nothing != null ? "<color=#e6b8a0>" + nothing + "</color>" : InReach(w, dir, pwr);
            Vector2 m = Event.current.mousePosition;
            string where = TC2.Core.History.HistoryText.At(w.PlaceName(cell));
            // (session 29) in a box sized to its words and kept on the screen, as the hover tag - a bare label had run over the map's names (s29book2)
            if (_aimBox == null) _aimBox = new GUIStyle(_mapTag) { wordWrap = true, richText = true, fontSize = 13, padding = new RectOffset(9, 9, 6, 7) };
            var ac = new GUIContent("<b>" + pwr.Doing + "</b> " + where + (reach.Length > 0 ? "\n" + reach : "") + "\n<size=13><i><color=#c9c3b4>" + (nothing != null ? "Right-click to stop" : "Click to cast, right-click to stop") + "</color></i></size>");
            float aw = Mathf.Min(470f, sw - 8f), ah = _aimBox.CalcHeight(ac, aw);
            float ax = m.x + 18f, ay = m.y + 12f;
            if (ax + aw > sw - 4f) ax = Mathf.Max(4f, m.x - 18f - aw);
            if (ay + ah > _powersRect.y - 4f) ay = Mathf.Max(4f, m.y - 12f - ah);
            _aimRect = new Rect(ax, ay, aw, ah);
            GUI.Box(_aimRect, ac, _aimBox);
        }
    }
}
