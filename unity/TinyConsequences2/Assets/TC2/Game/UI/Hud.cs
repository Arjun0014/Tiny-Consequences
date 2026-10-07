using System.Globalization;
using System.Text;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Foundation HUD (IMGUI: no assets, works on every platform), kept out of the way of the world: a slim bar with
    /// the date and the speeds, a one-line ticker of what just happened, and panels behind toggles (Info: the world in
    /// numbers; History: the recent feed; Book (B): the History Book - ages, places, every line its record and its causes; World:
    /// saves, absences, new worlds) - H hides everything. The catch-up screen and a person inspector with their "why". Reads the
    /// world; actions go through the host.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        public WorldObjectsView Objects;
        public static bool PointerOverUi;
        int _selected, _follow;   // the person inspected; the person the camera keeps with (0 none)
        int _building;            // the structure inspected (0 none: C1.1)
        GUIStyle _box, _label, _small, _title, _btn;
        float _scale = 1f;
        Rect _left, _right, _bottom, _toggles, _ticker;
        float _fps; int _frames; float _fpsT;
        bool _info, _history, _worldPanel, _hidden, _book;
        // the History Book (D2-077): ages or places on the left, a chapter's lines on the right, a line's causes under it
        int _bookTab, _era = -1, _place, _bookKey = -1, _bookYear = -1;
        long _line;
        Vector2 _listScroll, _bookScroll, _whyScroll;
        System.Collections.Generic.List<Era> _eras;
        System.Collections.Generic.List<BookLine> _lines;
        readonly long[] _chain = new long[16];
        GUIStyle _lineStyle, _lineSel, _page;
        Rect _bookRect;

        /// <summary>Opens the History Book at an age (the page a tool or a URL asks for).</summary>
        public void OpenBook(int era) { _book = true; _bookTab = 0; _era = era; _bookKey = -1; _line = 0; _eras = null; }
        bool _bookAsked;
        string _news = ""; float _newsAge = 99f; int _newsCount;

        void Update()
        {
            _frames++; _fpsT += Time.unscaledDeltaTime;
            if (_fpsT > 0.5f) { _fps = _frames / _fpsT; _frames = 0; _fpsT = 0; }
            var w = Host.World;
            if (w == null || Host.CatchingUp) return;
            if (!_bookAsked) { _bookAsked = true; if (Browser.Param("book") != null) OpenBook(-1); }   // (?book=1: open at the present age)
            if (Input.GetKeyDown(KeyCode.B)) _book = !_book;
            if (Input.GetKeyDown(KeyCode.Space)) Host.SpeedIndex = Host.SpeedIndex == 0 ? 3 : 0;
            if (Input.GetKeyDown(KeyCode.H)) _hidden = !_hidden;
            if (Host.Feed.Count != _newsCount) { _newsCount = Host.Feed.Count; _news = _newsCount > 0 ? Host.Feed[_newsCount - 1] : ""; _newsAge = 0f; }
            _newsAge += Time.unscaledDeltaTime;
            for (int i = 0; i < SimHost.Speeds.Length && i < 8; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Host.SpeedIndex = i; Host.MaxSpeed = false; }
            if (Input.GetKeyDown(KeyCode.Alpha9)) Host.MaxSpeed = !Host.MaxSpeed;
            if (Input.GetMouseButtonUp(0) && !PointerOverUi) Pick();
            // following: the camera keeps over the person as they go about their day (its height as the player left it)
            var fw = Host.World;
            if (_follow != 0 && fw != null)
            {
                if (!fw.People.Exists(_follow) || !fw.People.Get(_follow).Alive) _follow = 0;
                else
                {
                    var d = fw.Agents.PositionAt(fw.People.Get(_follow), Host.RenderTime);
                    Rig.Lat = d.Latitude; Rig.Lon = d.Longitude;
                }
            }
        }

        void Pick()
        {
            var w = Host.World;
            var cam = Rig.Cam;
            Vector2 mp = Input.mousePosition;
            double t = Host.RenderTime;
            float best = 28f * _scale; int bestId = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant) continue;
                Vector3 pos = PlanetGeo.Surface(w.Planet, w.Agents.PositionAt(p, t));
                Vector3 sp = cam.WorldToScreenPoint(pos);
                if (sp.z < 0) continue;
                float d = Vector2.Distance(mp, sp);
                if (d < best) { best = d; bestId = id; }
            }
            if (bestId != 0) { _selected = bestId; _building = 0; return; }
            // no one there: the structure under the pointer (C1.1) - a home, a well, a granary
            var P = w.Planet;
            float bestB = 30f * _scale; int bestBid = 0;
            Vector3 camPos = cam.transform.position;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                Vector3 c = PlanetGeo.Surface(P, s.Anchor);
                if ((c - camPos).sqrMagnitude > 3000f * 3000f) continue;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    Vector3 sp = cam.WorldToScreenPoint(PlanetGeo.Surface(P, s.LocalToSphere(b.X, b.Y, P.Radius)));
                    if (sp.z < 0) continue;
                    float d = Vector2.Distance(mp, sp);
                    if (d < bestB) { bestB = d; bestBid = bl[i]; }
                }
            }
            if (bestBid != 0) { _building = bestBid; _selected = 0; }
        }

        void Styles()
        {
            _scale = Mathf.Max(1f, Screen.height / 900f);
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.1f, 0.55f)); bg.Apply();
            _box.normal.background = bg;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            _label.normal.textColor = new Color(0.92f, 0.93f, 0.95f);
            _small = new GUIStyle(_label) { fontSize = 12 };
            _small.normal.textColor = new Color(0.78f, 0.82f, 0.88f);
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(7, 7, 3, 3) };
            _page = new GUIStyle(_box) { padding = new RectOffset(14, 14, 10, 10) };   // (the book reads on a page, not over the world)
            var page = new Texture2D(1, 1); page.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.09f, 0.92f)); page.Apply();
            _page.normal.background = page;
            _lineStyle = new GUIStyle(_small) { padding = new RectOffset(4, 4, 2, 2) };
            _lineStyle.hover.textColor = new Color(1f, 0.92f, 0.6f);
            _lineSel = new GUIStyle(_lineStyle);
            _lineSel.normal.textColor = new Color(1f, 0.85f, 0.45f);
            var sel = new Texture2D(1, 1); sel.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.08f)); sel.Apply();
            _lineSel.normal.background = sel;
        }

        void OnGUI()
        {
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            float sw = Screen.width / _scale, sh = Screen.height / _scale;
            var w = Host.World;
            if (w == null) { GUI.Label(new Rect(20, 20, 600, 40), Host.StatusLine, _title); return; }
            if (Host.CatchingUp) { CatchUpScreen(sw, sh); PointerOverUi = true; return; }

            if (_hidden) { PointerOverUi = false; return; }
            // top-left: a slim bar - the date and the speeds
            _left = new Rect(10, 10, Mathf.Min(sw - 270, 900), 40);
            GUILayout.BeginArea(_left, _box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(SimTime.Format(w.Now), _title, GUILayout.Width(318));
            for (int i = 0; i < SimHost.Speeds.Length; i++)
            {
                bool on = !Host.MaxSpeed && Host.SpeedIndex == i;
                if (GUILayout.Toggle(on, SimHost.SpeedNames[i], _btn) && !on) { Host.SpeedIndex = i; Host.MaxSpeed = false; }
            }
            if (GUILayout.Toggle(Host.MaxSpeed, "Max", _btn) != Host.MaxSpeed) Host.MaxSpeed = !Host.MaxSpeed;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // top-right: toggles for the panels
            _toggles = new Rect(sw - 310, 10, 300, 40);
            GUILayout.BeginArea(_toggles, _box);
            GUILayout.BeginHorizontal();
            _info = GUILayout.Toggle(_info, "Info", _btn);
            _history = GUILayout.Toggle(_history, "History", _btn);
            _book = GUILayout.Toggle(_book, "Book", _btn);
            _worldPanel = GUILayout.Toggle(_worldPanel, "World", _btn);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            _right = Rect.zero;
            float ry = 56;
            if (_info)
            {
                _right = new Rect(sw - 340, ry, 330, 130);
                GUILayout.BeginArea(_right, _box);
                GUILayout.Label("<b>" + w.LivingPeople() + "</b> people in <b>" + w.InhabitedSettlements() + "</b> places   fires " + w.Fire.Active.Count, _label);
                var P = w.Planet;
                string sea = (P.SeaLevelNow - P.SeaLevel0).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
                GUILayout.Label("climate " + P.TGlobal.ToString("0.0", CultureInfo.InvariantCulture) + " °C   ice " + (P.IceVolume / 1e9).ToString("0.0", CultureInfo.InvariantCulture) + " km³   sea " + sea + " m   tilt " + (P.Tilt * 180 / System.Math.PI).ToString("0.0", CultureInfo.InvariantCulture) + "°" + (P.Glacial == 1 ? "   (an age of ice)" : ""), _small);
                GUILayout.Label("achieved " + FormatSpeed(Host.AchievedSpeed) + "   sim " + Host.LastSimMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms   " + _fps.ToString("0", CultureInfo.InvariantCulture) + " fps   " + SystemInfo.graphicsDeviceType, _small);
                GUILayout.Label("events " + w.Metrics.Events.ToString("N0", CultureInfo.InvariantCulture) + "   history " + w.History.Count + "   drawn " + Objects.PeopleDrawn + " people, " + (Objects.TreesDrawn + TerrainLod.ForestDrawn) + " trees", _small);
                GUILayout.Label("CO2 " + P.CO2.ToString("0", CultureInfo.InvariantCulture) + " ppm   volcanoes " + P.Vents.Count + "   storms " + P.Storms.Count + "   " + Wild(w), _small);
                GUILayout.EndArea();
                ry += 136;
            }
            Rect worldRect = Rect.zero;
            if (_worldPanel)
            {
                worldRect = new Rect(sw - 340, ry, 330, 96);
                GUILayout.BeginArea(worldRect, _box);
                GUILayout.Label("save " + (Host.LastSaveBytes / 1024f).ToString("0", CultureInfo.InvariantCulture) + " KB in " + Host.LastSaveMs.ToString("0", CultureInfo.InvariantCulture) + " ms (" + (Host.LastSaveOk ? "ok" : "-") + ")  offline: " + Host.Offline, _small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save", _btn)) Host.SaveNow();
                if (GUILayout.Button("Away 1 h", _btn)) Host.ReloadAsIfAway(3600000L);
                if (GUILayout.Button("Away 1 day", _btn)) Host.ReloadAsIfAway(86400000L);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("New world", _btn)) Host.NewWorld(w.Seed + 1);
                if (GUILayout.Button("Offline: " + Host.Offline, _btn)) Host.Offline = (OfflineMode)(((int)Host.Offline + 1) % 3);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            // what happened lately: the full feed behind a toggle, else a one-line ticker that fades
            _bottom = Rect.zero; _ticker = Rect.zero;
            if (_history)
            {
                _bottom = new Rect(10, sh - 250, 560, 240);
                GUILayout.BeginArea(_bottom, _box);
                GUILayout.Label("<b>Recent history</b>", _label);
                int from = Mathf.Max(0, Host.Feed.Count - 11);
                for (int i = from; i < Host.Feed.Count; i++) GUILayout.Label(Host.Feed[i], _small);
                GUILayout.EndArea();
            }
            else if (_news.Length > 0 && _newsAge < 14f)
            {
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(1.5f - _newsAge / 10f));
                _ticker = new Rect(sw * 0.5f - 330, sh - 46, 660, 34);
                GUILayout.BeginArea(_ticker, _box);
                GUILayout.Label(_news, _small);
                GUILayout.EndArea();
                GUI.color = old;
            }

            // the History Book
            _bookRect = Rect.zero;
            if (_book) { _bookRect = new Rect(sw * 0.5f - Mathf.Min(480, sw * 0.5f - 20), 60, Mathf.Min(960, sw - 40), sh - 130); Book(_bookRect, w); }

            // inspector
            Rect insp = new Rect(sw - 400, 190, 390, 300);
            bool showInsp = _selected != 0 && w.People.Exists(_selected);
            if (showInsp) Inspector(insp, w);
            else if (_building != 0 && w.Buildings.Exists(_building)) { showInsp = true; BuildingPage(insp, w); }
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / _scale;
            PointerOverUi = _left.Contains(m) || _toggles.Contains(m) || _right.Contains(m) || worldRect.Contains(m) || _bottom.Contains(m) || _ticker.Contains(m) || (showInsp && insp.Contains(m)) || _bookRect.Contains(m);
        }

        void Inspector(Rect r, TC2.Core.Simulation.World w)
        {
            ref var p = ref w.People.Get(_selected);
            var sb = new StringBuilder();
            string name = w.History.NameOf(EntityRef.Person(p.Id));
            sb.Append("<b>").Append(name).Append("</b>, ").Append(p.AgeYears(w.Now).ToString("0", CultureInfo.InvariantCulture)).Append(p.Alive ? "" : " (died)").Append('\n');
            sb.Append("home ").Append(w.History.NameOf(EntityRef.Settlement(p.Home))).Append(p.At == 0 ? ", on the road" : (p.At != p.Home ? ", visiting " + w.History.NameOf(EntityRef.Settlement(p.At)) : "")).Append('\n');
            sb.Append("<b>").Append(HistoryText.Doing(w, in p)).Append("</b>\n");
            sb.Append("until ").Append(SimTime.FormatShort(p.T1)).Append("   health ").Append(p.Health.ToString("0.00", CultureInfo.InvariantCulture)).Append("   fed ").Append(p.Nutrition.ToString("0.00", CultureInfo.InvariantCulture)).Append("   reserve ").Append(p.Reserve.ToString("0", CultureInfo.InvariantCulture)).Append(" days\n");
            if (p.Partner != 0) sb.Append("partner ").Append(w.History.NameOf(EntityRef.Person(p.Partner))).Append('\n');
            if (w.Households.Exists(p.Household))
            {
                var hh = w.Households.Get(p.Household);
                sb.Append("household: ").Append(w.Members(p.Household).Count).Append(" people, ").Append(hh.Shelter == Construction.House ? "a house" : hh.Shelter == Construction.Hut ? "a hut" : "a camp");
                int cattle = 0, goats = 0, pigs = 0; var beasts = w.BeastsOf(p.Household);
                for (int i = 0; i < beasts.Count; i++) { byte k = w.Beasts.Get(beasts[i]).Kind; if (k == Livestock.Cattle) cattle++; else if (k == Livestock.Goat) goats++; else pigs++; }
                if (cattle + goats + pigs > 0) sb.Append(", ").Append(cattle).Append(" cattle, ").Append(goats).Append(" goats, ").Append(pigs).Append(" pigs");
                sb.Append(", larder ").Append(hh.Larder.ToString("0", CultureInfo.InvariantCulture))
                  .Append(", cooked ").Append(hh.Ready.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append(", water ").Append(hh.Water.ToString("0", CultureInfo.InvariantCulture)).Append(" l")
                  .Append(", ").Append(hh.Indoor.ToString("0", CultureInfo.InvariantCulture)).Append(" C indoors")
                  .Append(", goods ").Append((hh.Gear / System.Math.Max(1, w.Members(p.Household).Count)).ToString("0.00", CultureInfo.InvariantCulture)).Append(" a head").Append('\n');
            }
            if (p.Sick != 0)
                sb.Append(p.SickPhase == TC2.Core.Health.Disease.Latent ? "carrying " : "sick with ").Append(w.History.NameOf(new EntityRef(EntityKind.Pathogen, p.Sick))).Append('\n');
            if (p.KNets >= 0.1f || p.KLoom >= 0.1f || p.KQuern >= 0.1f || p.KPottery >= 0.1f || p.KArd >= 0.1f || p.KMetal >= 0.1f)
            {
                sb.Append("knows:");
                if (p.KNets >= 0.1f) sb.Append(" nets ").Append(p.KNets.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KLoom >= 0.1f) sb.Append(" loom ").Append(p.KLoom.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KQuern >= 0.1f) sb.Append(" quern ").Append(p.KQuern.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KPottery >= 0.1f) sb.Append(" pots ").Append(p.KPottery.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KArd >= 0.1f) sb.Append(" ard ").Append(p.KArd.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KMetal >= 0.1f) sb.Append(" copper ").Append(p.KMetal.ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append('\n');
            }
            sb.Append("skills: farming ").Append(p.SkFarm.ToString("0.00", CultureInfo.InvariantCulture)).Append(", wood ").Append(p.SkWood.ToString("0.00", CultureInfo.InvariantCulture)).Append(", hunting ").Append(p.SkHunt.ToString("0.00", CultureInfo.InvariantCulture)).Append(", fishing ").Append(p.SkFish.ToString("0.00", CultureInfo.InvariantCulture)).Append("   trips ").Append(p.Visits).Append('\n');
            var evs = w.History.EventsOf(EntityRef.Person(p.Id));
            if (evs != null)
            {
                sb.Append("<b>life</b>\n");
                for (int i = System.Math.Max(0, evs.Count - 5); i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e)) sb.Append(HistoryText.Line(w, e)).Append('\n');
                for (int i = evs.Count - 1; i >= 0; i--)
                    if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.JourneyInterrupted) { sb.Append("<b>why?</b>\n").Append(HistoryText.Why(w, e.Id)); break; }
            }
            GUILayout.BeginArea(r, _box);
            GUILayout.Label(sb.ToString(), _small);
            if (GUILayout.Button(_follow == _selected ? "Stop following" : "Follow", _btn)) _follow = _follow == _selected ? 0 : _selected;
            GUILayout.EndArea();
        }

        /// <summary>
        /// A structure's page (C1.1, 21 §7): what it is and is for, how sound it is, who raised it and why, what it holds, and its history -
        /// read from the building's state and the ledger; writes nothing.
        /// </summary>
        void BuildingPage(Rect r, TC2.Core.Simulation.World w)
        {
            ref var b = ref w.Buildings.Get(_building);
            var sb = new StringBuilder();
            string place = w.History.NameOf(EntityRef.Settlement(b.Settlement));
            bool home = Structures.IsHome(b.Kind);
            var k = Structures.Of(b.Kind);
            long now = w.Now;
            double age = (now - b.Built) / (double)SimTime.Year;
            sb.Append("<b>").Append(home ? (b.Kind == Structures.House ? "A house" : "A hut") : "The " + k.Name).Append(" of ").Append(place).Append("</b>\n");
            bool fallen = Homes.Fallen(in b, now);
            double cond = Structures.Condition(in b, now);
            if (b.Done == 0)
                sb.Append("being raised: ").Append(b.Hours.ToString("0", CultureInfo.InvariantCulture)).Append(" of ").Append(k.Hours.ToString("0", CultureInfo.InvariantCulture)).Append(" hours of work, ")
                  .Append(b.Wood.ToString("0", CultureInfo.InvariantCulture)).Append(" of ").Append(k.Wood.ToString("0", CultureInfo.InvariantCulture)).Append(" loads of wood; laid out ").Append(age.ToString("0.0", CultureInfo.InvariantCulture)).Append(" years ago\n");
            else if (fallen) sb.Append("fallen in: a ruin (").Append((Homes.RuinLeft(in b, now) * 100).ToString("0", CultureInfo.InvariantCulture)).Append("% of it still stands out of the ground)\n");
            else sb.Append("standing ").Append(age.ToString("0", CultureInfo.InvariantCulture)).Append(" years   condition ").Append(cond.ToString("0.00", CultureInfo.InvariantCulture)).Append(b.Left != 0 ? "   unkept since " + SimTime.FormatShort(b.Left) : "").Append('\n');
            // what it is for
            if (home)
            {
                if (b.Household != 0 && w.Households.Exists(b.Household)) sb.Append("home of the ").Append(w.History.NameOf(EntityRef.Household(b.Household))).Append("s (").Append(w.Members(b.Household).Count).Append(" people)\n");
                else sb.Append("standing empty").Append(b.Last != 0 ? "; the " + w.History.NameOf(EntityRef.Household(b.Last)) + "s lived in it last" : "").Append('\n');
                if (b.Style != 0) sb.Append("raised by the ").Append(w.History.NameOf(EntityRef.Household(b.Style))).Append("s\n");
                if (b.Kind == Structures.House) sb.Append(b.Mat >= 1 ? "on stone footings (" + b.Stone.ToString("0", CultureInfo.InvariantCulture) + " loads of stone): it stands twice as long once left\n" : "on earth-fast posts: no stone near enough\n");
            }
            else if (w.Settlements.Exists(b.Settlement))
            {
                ref var s = ref w.Settlements.Get(b.Settlement);
                if (k.Function == Function.Store)
                {
                    double cap = Structures.StoreCapacity(in s);
                    sb.Append("keeps grain: up to ").Append(k.Capacity.ToString("N0", CultureInfo.InvariantCulture)).Append(" days' food for one; ")
                      .Append(place).Append("'s ").Append(s.Granaries).Append(s.Granaries == 1 ? " granary" : " granaries").Append(s.Barns > 0 ? " and " + s.Barns + (s.Barns == 1 ? " barn" : " barns") : "").Append(" hold ").Append(s.Granary.ToString("N0", CultureInfo.InvariantCulture))
                      .Append(" of ").Append(cap.ToString("N0", CultureInfo.InvariantCulture)).Append(s.Granary > cap ? " - the rest lies in heaps and rots fast" : "").Append('\n');
                }
                else if (b.Kind == Structures.Well) sb.Append("water for ").Append(s.Population).Append(" people, drawn by the pot\n");
                else if (b.Kind == Structures.Quarry)
                    sb.Append("cuts building stone at rock ").Append((s.StoneDist / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" km from ").Append(place).Append(": ")
                      .Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture)).Append(" loads cut and carried home in ").Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours; the store holds ")
                      .Append(s.Stone.ToString("0", CultureInfo.InvariantCulture)).Append(" loads (it wants ").Append(s.StoneWant.ToString("0", CultureInfo.InvariantCulture)).Append(")\n");
                if (b.Hours > 0) sb.Append("raised with ").Append(b.Hours.ToString("0", CultureInfo.InvariantCulture)).Append(" hours of work and ").Append(b.Wood.ToString("0", CultureInfo.InvariantCulture)).Append(" loads of wood").Append(b.Stone > 0 ? ", " + b.Stone.ToString("0", CultureInfo.InvariantCulture) + " of stone" : "").Append('\n');
                if (b.Leader != 0) sb.Append("the first at the work: ").Append(w.History.NameOf(EntityRef.Person(b.Leader))).Append('\n');
                if (b.Cause != 0 && w.History.TryGet(b.Cause, out var ce)) sb.Append("<b>why</b>: ").Append(HistoryText.Line(w, ce)).Append('\n');
                else if (b.Hours <= 0) sb.Append("standing when its people came\n");
            }
            var evs = w.History.EventsOf(new EntityRef(EntityKind.Building, _building));
            if (evs != null && evs.Count > 0)
            {
                sb.Append("<b>history</b>\n");
                for (int i = System.Math.Max(0, evs.Count - 6); i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e)) sb.Append(HistoryText.Line(w, e)).Append('\n');
            }
            GUILayout.BeginArea(r, _box);
            GUILayout.Label(sb.ToString(), _small);
            if (GUILayout.Button("Close", _btn)) _building = 0;
            GUILayout.EndArea();
        }

        /// <summary>
        /// The History Book (D2-077; F7): the ages of the world found after the fact, or its places; a chapter's numbers from the
        /// annals and its events, each line its record; a chosen line's causes back to their root, each a record to follow in turn,
        /// and the place it happened to go to. Reads the ledger and the annals; writes nothing.
        /// </summary>
        void Book(Rect r, TC2.Core.Simulation.World w)
        {
            int year = (int)(w.Now / SimTime.Year);
            if (_eras == null || year != _bookYear) { _eras = Chronicle.Eras(w); _bookYear = year; _bookKey = -1; if (_era < 0 || _era >= _eras.Count) _era = _eras.Count - 1; }
            GUILayout.BeginArea(r, _page);
            GUILayout.BeginHorizontal();
            // the ages and the places
            GUILayout.BeginVertical(GUILayout.Width(240));
            GUILayout.Label("<b>The History Book</b>", _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_bookTab == 0, "Ages", _btn) && _bookTab != 0) { _bookTab = 0; _bookKey = -1; _line = 0; }
            if (GUILayout.Toggle(_bookTab == 1, "Places", _btn) && _bookTab != 1) { _bookTab = 1; _bookKey = -1; _line = 0; }
            GUILayout.EndHorizontal();
            _listScroll = GUILayout.BeginScrollView(_listScroll);
            if (_bookTab == 0)
                for (int i = 0; i < _eras.Count; i++)
                {
                    var e = _eras[i];
                    string label = e.Name + "\n<size=11>years " + e.From + " to " + (e.Present ? "this day" : e.To.ToString(CultureInfo.InvariantCulture)) + "</size>";
                    if (GUILayout.Toggle(_era == i, label, _btn) && _era != i) { _era = i; _bookKey = -1; _line = 0; _bookScroll = Vector2.zero; }
                }
            else
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var st = ref w.Settlements.Get(sid);
                    string label = w.History.NameOf(EntityRef.Settlement(sid)) + (st.Inhabited ? "" : " (gone)") + (st.Wall == 2 ? " (walled)" : "") + (st.Lord != 0 ? " (pays " + w.History.NameOf(EntityRef.Settlement(st.Lord)) + ")" : "");
                    if (GUILayout.Toggle(_place == sid, label, _btn) && _place != sid) { _place = sid; _bookKey = -1; _line = 0; _bookScroll = Vector2.zero; }
                }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            // the chapter, each line its record; the chosen line's causes
            GUILayout.BeginVertical();
            int key = _bookTab == 0 ? _era : 1000000 + _place;
            if (key != _bookKey)
            {
                _lines = _bookTab == 0 ? (_era >= 0 && _era < _eras.Count ? Chronicle.Chapter(w, _eras[_era], 14) : null) : (_place > 0 && w.Settlements.Exists(_place) ? Chronicle.Place(w, _place, 24) : null);
                _bookKey = key;
            }
            if (_bookTab == 0 && _era >= 0 && _era < _eras.Count) GUILayout.Label("<b>" + _eras[_era].Name + "</b>   <size=11>years " + _eras[_era].From + " to " + (_eras[_era].Present ? "this day" : _eras[_era].To.ToString(CultureInfo.InvariantCulture)) + "</size>", _label);
            else if (_bookTab == 1 && _place > 0) GUILayout.Label("<b>" + w.History.NameOf(EntityRef.Settlement(_place)) + "</b>", _label);
            _bookScroll = GUILayout.BeginScrollView(_bookScroll, GUILayout.Height(r.height * 0.55f));
            if (_lines != null)
                foreach (var l in _lines)
                {
                    if (l.Event == 0) { GUILayout.Label("<i>" + l.Text + "</i>  <size=10>(the annals)</size>", _small); continue; }
                    if (GUILayout.Button(l.Text, _line == l.Event ? _lineSel : _lineStyle)) { _line = l.Event; _whyScroll = Vector2.zero; }
                }
            GUILayout.EndScrollView();
            if (_line != 0 && w.History.TryGet(_line, out var chosen))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Why?</b>  <size=11>each cause is a record of its own; its root is " + chosen.RootKind + "</size>", _small);
                if (Place(w, chosen, out double lat, out double lon) && GUILayout.Button("Go there", _btn, GUILayout.Width(80))) { Rig.Lat = lat; Rig.Lon = lon; }
                GUILayout.EndHorizontal();
                _whyScroll = GUILayout.BeginScrollView(_whyScroll);
                int n = w.History.CausalChain(_line, _chain);
                for (int i = 0; i < n; i++)
                {
                    if (!w.History.TryGet(_chain[i], out var e)) { GUILayout.Label("<- (a record forgotten)", _small); break; }
                    string row = (i == 0 ? "" : "<- ") + HistoryText.Dated(w, e);
                    if (GUILayout.Button(row, _chain[i] == _line ? _lineSel : _lineStyle) && _chain[i] != _line) { _line = _chain[i]; break; }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Where an event happened, for the camera: its village, else its cell.</summary>
        static bool Place(TC2.Core.Simulation.World w, in HistoryEvent e, out double lat, out double lon)
        {
            lat = 0; lon = 0;
            if (e.Settlement > 0 && w.Settlements.Exists(e.Settlement)) { var a = w.Settlements.Get(e.Settlement).Anchor; lat = a.Latitude; lon = a.Longitude; return true; }
            if (e.Cell >= 0 && e.Cell < w.Planet.Cells) { var c = w.Planet.Grid.Center[e.Cell]; lat = c.Latitude; lon = c.Longitude; return true; }
            return false;
        }

        void CatchUpScreen(float sw, float sh)
        {
            var w = Host.World;
            GUI.Box(new Rect(0, 0, sw, sh), GUIContent.none, _box);
            float span = Mathf.Max(1f, (float)(Host.CatchUpTarget - Host.CatchUpFrom));
            float done = Mathf.Clamp01((float)(w.Now - Host.CatchUpFrom) / span);
            GUI.Label(new Rect(sw * 0.5f - 300, 60, 600, 40), "While you were away...", _title);
            GUI.Label(new Rect(sw * 0.5f - 300, 100, 600, 30), SimTime.Format(Host.CatchUpFrom) + "  ->  " + SimTime.Format(Host.CatchUpTarget), _label);
            GUI.Box(new Rect(sw * 0.5f - 300, 140, 600, 18), GUIContent.none);
            GUI.Box(new Rect(sw * 0.5f - 300, 140, 600 * done, 18), GUIContent.none, _box);
            GUI.Label(new Rect(sw * 0.5f - 300, 165, 600, 30), "now " + SimTime.Format(w.Now) + "   " + w.LivingPeople() + " people in " + w.InhabitedSettlements() + " places   " + FormatSpeed(Host.AchievedSpeed), _small);
            float y = 200;
            int from = Mathf.Max(0, Host.CatchUpFeed.Count - 22);
            for (int i = from; i < Host.CatchUpFeed.Count; i++) { GUI.Label(new Rect(sw * 0.5f - 300, y, 600, 22), Host.CatchUpFeed[i], _small); y += 20; }
        }

        string _wild = ""; float _wildAt = -99f;

        /// <summary>The wild animals on the planet, a few kinds in round numbers (summed every few seconds).</summary>
        string Wild(TC2.Core.Simulation.World w)
        {
            if (Time.unscaledTime - _wildAt < 3f) return _wild;
            _wildAt = Time.unscaledTime;
            var P = w.Planet;
            var sb = new System.Text.StringBuilder();
            for (int habitat = 0; habitat < 2; habitat++)
            {
                bool first = true;
                for (int s = 0; s < P.Species.Count; s++)
                {
                    var sp = P.Species[s];
                    if (sp.Habitat != habitat) continue;
                    double t = TC2.Core.Planet.Ecology.Total(P, s);
                    if (t < 1) continue;
                    if (first) { if (sb.Length > 0) sb.Append(" | sea: "); first = false; }
                    else sb.Append(", ");
                    string name = w.History.NameOf(new TC2.Core.Foundation.EntityRef(TC2.Core.Foundation.EntityKind.Species, sp.Id));
                    if (string.IsNullOrEmpty(name)) name = TC2.Core.Planet.Ecology.KindName(sp.Kind);
                    sb.Append(name).Append(' ').Append(t >= 1e6 ? (t / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M"
                        : t >= 1000 ? (t / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k" : t.ToString("0", CultureInfo.InvariantCulture));
                }
            }
            _wild = sb.ToString();
            return _wild;
        }

        static string FormatSpeed(double s)
        {
            if (s >= SimTime.Year * 0.5) return (s / SimTime.Year).ToString("0.0", CultureInfo.InvariantCulture) + " years/s";
            if (s >= SimTime.Day) return (s / SimTime.Day).ToString("0.0", CultureInfo.InvariantCulture) + " days/s";
            if (s >= SimTime.Hour) return (s / SimTime.Hour).ToString("0.0", CultureInfo.InvariantCulture) + " h/s";
            return s.ToString("0", CultureInfo.InvariantCulture) + "x";
        }
    }
}
