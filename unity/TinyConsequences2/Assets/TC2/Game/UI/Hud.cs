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
    /// numbers; History: the recent feed; Book (B): the History Book - its parts and chapters, every line its record and its causes; World:
    /// saves, absences, new worlds) - H hides everything. Places named on the map (N), a tag on the person or building under the pointer
    /// close in. The catch-up screen and a person inspector with their "why". Reads the world; actions go through the host.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        public WorldObjectsView Objects;
        public static bool PointerOverUi;
        int _selected, _follow;   // the person inspected; the person the camera keeps with (0 none)
        int _building;            // the structure inspected (0 none: C1.1)
        Vector2 _bScroll;         // (session 29: the building page scrolls - its story of why)
        GUIStyle _box, _label, _small, _title, _btn;
        float _scale = 1f;
        Rect _left, _right, _bottom, _toggles, _ticker;
        bool _settings; Rect _settingsRect; float _settingsDirty; bool _lastMuted;   // (the player's settings: F11.2)
        float _fps; int _frames; float _fpsT;
        bool _info, _history, _worldPanel, _hidden, _book;
        // the person page (C12.1): a person's life in sections, from the world's state and its ledger (Core/History/Biography)
        bool _personPage; int _pageTab, _pagePid = -1, _pageTabBuilt = -1; float _pageAt; string _pageText = "", _pageTitle = ""; Vector2 _pageScroll; Rect _pageRect;
        // the settlement page (C12.2): a place's founding, people, food, works, land and making, neighbours and history (Core/History/Gazetteer)
        int _placeSid, _placeTab, _placeBuilt = -1, _placeFor; float _placeAt; string _placeText = "", _placeTitle = "";
        static readonly string[] PlaceTabs = { "Place", "Now", "Why?", "Realm", "Food", "Works", "Land", "Neighbours", "History" };   // (session 29: Now, Why? and Realm - Hud.Place.cs)
        // the History Book (session 25, HistoryBook: a book - its title page and preface, eight parts and their chapters on the left page,
        // a chapter on the right, a line's causes under it; each part written when it is first opened, the book rewritten in a new year)
        int _bookPart = -1, _bookChapter, _bookYear = -1, _openAt = -2;
        HistoryBook.Writer _writer;
        BookPart[] _parts;
        long _line;
        Vector2 _listScroll, _bookScroll, _whyScroll;
        readonly long[] _chain = new long[16];
        GUIStyle _lineStyle, _lineSel, _sheet, _ink, _inkTitle, _inkHead, _inkFact, _inkSub, _inkLine, _inkLineSel, _toc, _tocSel, _tocPart, _inkBtn;
        Rect _bookRect;

        /// <summary>Opens the History Book at an age (the page a tool or a URL asks for; -1 the present age).</summary>
        public void OpenBook(int era) { _book = true; _openAt = era; _line = 0; }

        /// <summary>Opens the History Book at a part and chapter (-1: the preface) - the page a tool asks for.</summary>
        public void OpenBookAt(int part, int chapter) { _book = true; _openPart = part; _openChapter = chapter; _line = 0; }
        public void CloseBook() { _book = false; }
        /// <summary>Closes the book and every page - a person's, a place's, a building's (the tests' way back to the open world).</summary>
        public void ClosePages() { _book = false; _personPage = false; _placeSid = 0; _building = 0; }
        /// <summary>(session 29) Tests: the History panel shown or hidden, as its toggle; a feed line opened as a click on it does (0 the latest).</summary>
        public void HistoryPanel(bool on) { _history = on; }
        public void OpenFeedLine(int back)
        {
            int i = Host.FeedIds.Count - 1 - back;
            if (i >= 0 && Host.World != null && Host.World.History.TryGet(Host.FeedIds[i], out _)) { _book = true; _line = Host.FeedIds[i]; _whyScroll = Vector2.zero; }
        }
        int _openPart = NoRequest, _openChapter;
        const int NoRequest = -99;
        // the Timeline page (session 25): the world's years drawn once into a texture when the book is written (one draw a frame)
        BookTimeline _timeline; Texture2D _timelineTex; int _timelineYear = -1;
        const int TimelinePart = -2;
        static readonly Color[] RealmInk = { new Color(0.66f, 0.17f, 0.12f), new Color(0.17f, 0.32f, 0.60f), new Color(0.22f, 0.48f, 0.22f), new Color(0.80f, 0.58f, 0.14f),
                                             new Color(0.47f, 0.22f, 0.52f), new Color(0.12f, 0.50f, 0.52f), new Color(0.85f, 0.42f, 0.12f), new Color(0.45f, 0.31f, 0.20f) };
        bool _bookAsked;
        // names on the map (session 25: places and people read clearly): each inhabited place's name at its square, what it is and whom
        // it pays under it, the most peopled first and none over another; close in, a tag on the person or building under the pointer
        public static bool Names = true;
        GUIStyle _mapName, _mapTown, _mapSub, _mapTag;
        Texture2D _dot;
        readonly System.Collections.Generic.List<Rect> _placed = new System.Collections.Generic.List<Rect>();
        // (session 29) the panels, tags and bars drawn last frame: no place, work or person is named under them (s29book2: names showed through
        // the translucent hover tag, Your hand and the power bar, over their words)
        readonly System.Collections.Generic.List<Rect> _uiRects = new System.Collections.Generic.List<Rect>();
        Rect _handRect, _tagRect;
        readonly System.Collections.Generic.List<int> _byPeople = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.Dictionary<int, string> _placeLine = new System.Collections.Generic.Dictionary<int, string>();
        string _pageStanding = "";   // (session 27: the person page's standing line)
        readonly System.Collections.Generic.Dictionary<int, int> _sickAt = new System.Collections.Generic.Dictionary<int, int>();   // (people abed sick, by home: the map line)
        float _dateW, _speedsW, _togglesW;   // (the top bars' widths, measured once from their words)
        readonly System.Collections.Generic.HashSet<int> _seats = new System.Collections.Generic.HashSet<int>();   // (places others pay and that pay nobody: realms' seats)
        float _byPeopleAt = -99f;
        int _hoverPid, _hoverBid; float _hoverAt;
        string _news = ""; float _newsAge = 99f; long _newsTotal;

        void Update()
        {
            _frames++; _fpsT += Time.unscaledDeltaTime;
            if (_fpsT > 0.5f) { _fps = _frames / _fpsT; _frames = 0; _fpsT = 0; }
            var w = Host.World;
            LapseHook();   // (session 29: the timelapse - Hud.Lapse.cs)
            if (w != null && Rig != null && Rig.LapseActive) PollHeadlines(w);
            if (w == null || Host.CatchingUp) return;
            if (_titleOn) return;   // (the title holds the world still: F11.1)
            if (!_bookAsked) { _bookAsked = true; if (Browser.Param("book") != null) OpenBook(-1); }   // (?book=1: open at the present age)
            if (Input.GetKeyDown(KeyCode.B)) _book = !_book;
            if (Input.GetKeyDown(KeyCode.Space)) Host.SpeedIndex = Host.SpeedIndex == 0 ? 3 : 0;
            if (Input.GetKeyDown(KeyCode.H)) _hidden = !_hidden;
            if (Input.GetKeyDown(KeyCode.R)) RealmsView.On = !RealmsView.On;   // (the realms map layer, D2-167)
            if (Input.GetKeyDown(KeyCode.N)) Names = !Names;   // (places named on the map)
            if (Host.FeedTotal != _newsTotal) { _newsTotal = Host.FeedTotal; _news = Host.Feed.Count > 0 ? Host.Feed[Host.Feed.Count - 1] : ""; _newsAge = 0f; }   // (session 29: by the lines ever added - the full feed's count stands still)
            _newsAge += Time.unscaledDeltaTime;
            for (int i = 0; i < SimHost.Speeds.Length && i < 8; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Host.SpeedIndex = i; Host.MaxSpeed = false; }
            if (Input.GetKeyDown(KeyCode.Alpha9)) Host.MaxSpeed = !Host.MaxSpeed;
            if (!PowersInput() && Input.GetMouseButtonUp(0) && !PointerOverUi && ((Vector2)Input.mousePosition - _downAt).sqrMagnitude <= 64f) Pick();   // (a click while aiming a power casts it: F9.1; a drag of the camera picks nothing - session 29)
            // following: the camera keeps over the person as they go about their day (its height as the player left it)
            var fw = Host.World;
            if (_follow != 0 && fw != null && !Rig.LapseFlies)   // (while the camera flies a timelapse it keeps the person's place: Hud.Lapse.cs)
            {
                if (!fw.People.Exists(_follow) || !fw.People.Get(_follow).Alive) { NoteDeath(fw, _follow, true); _follow = 0; }   // (session 29: their death told, the kin to go on with offered)
                else
                {
                    Presence.Of(fw, in fw.People.Get(_follow), Host.RenderTime, out var d, out _, out _, out _);   // (where they are drawn)
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
            // (session 29: world -> realm -> settlement) from afar a click on a village opens its page - before, nothing answered a click above 1.3 km
            float altPick = cam.transform.position.magnitude - (float)w.Planet.Radius;
            if (altPick > 1300f) { int far = PlaceUnder(w, mp); if (far != 0) ShowPlace(far, PlaceNowTab); return; }
            float best = 28f * _scale; int bestId = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || !Presence.Of(w, in p, t, out var pd, out _, out _, out _)) continue;   // (as they are drawn: the asleep and those indoors are not)
                Vector3 pos = PlanetGeo.Surface(w.Planet, pd);
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
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8), border = new RectOffset(8, 8, 8, 8) };
            // (session 27: the HUD as a game's) panels of dark glass with a thin gold edge, rounded
            _box.normal.background = Rounded(new Color(0.045f, 0.055f, 0.075f, 0.70f), new Color(0.62f, 0.50f, 0.28f, 0.55f));
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            _label.normal.textColor = new Color(0.92f, 0.93f, 0.95f);
            _small = new GUIStyle(_label) { fontSize = 12 };
            _small.normal.textColor = new Color(0.78f, 0.82f, 0.88f);
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
            // (session 27) buttons of dark glass that warm to gold under the pointer and when on
            // (the date in the UI type, warm gold: IM FELL's old-style figures read "Year o ... oo:44" - session 27, s27web2)
            _title.fontSize = 18; _title.normal.textColor = new Color(0.98f, 0.88f, 0.62f);
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(9, 9, 4, 4), border = new RectOffset(8, 8, 8, 8), margin = new RectOffset(2, 2, 2, 2) };
            var bNormal = Rounded(new Color(0.10f, 0.11f, 0.14f, 0.86f), new Color(0.50f, 0.42f, 0.27f, 0.50f));
            var bHover = Rounded(new Color(0.17f, 0.15f, 0.12f, 0.92f), new Color(0.88f, 0.70f, 0.36f, 0.90f));
            var bActive = Rounded(new Color(0.32f, 0.23f, 0.10f, 0.95f), new Color(0.95f, 0.78f, 0.42f, 1f));
            _btn.normal.background = bNormal; _btn.hover.background = bHover; _btn.active.background = bActive; _btn.focused.background = bNormal;
            _btn.onNormal.background = bActive; _btn.onHover.background = bActive; _btn.onActive.background = bActive; _btn.onFocused.background = bActive;
            _btn.normal.textColor = new Color(0.90f, 0.88f, 0.82f); _btn.hover.textColor = new Color(1f, 0.86f, 0.52f); _btn.active.textColor = new Color(1f, 0.92f, 0.65f);
            _btn.onNormal.textColor = new Color(1f, 0.90f, 0.62f); _btn.onHover.textColor = new Color(1f, 0.92f, 0.68f); _btn.onActive.textColor = new Color(1f, 0.92f, 0.68f);
            _lineStyle = new GUIStyle(_small) { padding = new RectOffset(4, 4, 2, 2) };
            _lineStyle.hover.textColor = new Color(1f, 0.92f, 0.6f);
            _lineSel = new GUIStyle(_lineStyle);
            _lineSel.normal.textColor = new Color(1f, 0.85f, 0.45f);
            var sel = new Texture2D(1, 1); sel.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.08f)); sel.Apply();
            _lineSel.normal.background = sel;
            // the book: dark ink on a warm page
            _sheet = new GUIStyle(GUI.skin.box) { padding = new RectOffset(22, 22, 16, 14) };
            _sheet.normal.background = Solid(new Color(0.925f, 0.885f, 0.80f, 0.985f));
            Color ink = new Color(0.17f, 0.12f, 0.08f), faded = new Color(0.40f, 0.32f, 0.24f), red = new Color(0.52f, 0.16f, 0.10f);
            _ink = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true, padding = new RectOffset(0, 0, 3, 5) };
            _ink.normal.textColor = ink;
            _inkTitle = new GUIStyle(_ink) { fontSize = 21, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 2, 0) };
            _inkTitle.normal.textColor = red;
            _inkSub = new GUIStyle(_ink) { fontSize = 12, fontStyle = FontStyle.Italic, padding = new RectOffset(0, 0, 0, 8) };
            _inkSub.normal.textColor = faded;
            _inkHead = new GUIStyle(_ink) { fontSize = 15, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 10, 2) };
            _inkHead.normal.textColor = red;
            _inkFact = new GUIStyle(_ink) { fontSize = 12, fontStyle = FontStyle.Italic, padding = new RectOffset(16, 8, 2, 6) };
            _inkFact.normal.textColor = faded;
            _inkLine = new GUIStyle(_ink) { fontSize = 13, padding = new RectOffset(10, 4, 2, 2) };
            _inkLine.hover.textColor = red;
            _inkLine.hover.background = Solid(new Color(0.55f, 0.35f, 0.15f, 0.08f));
            _inkLineSel = new GUIStyle(_inkLine);
            _inkLineSel.normal.textColor = red;
            _inkLineSel.normal.background = Solid(new Color(0.55f, 0.35f, 0.15f, 0.16f));
            _toc = new GUIStyle(_ink) { fontSize = 13, padding = new RectOffset(14, 4, 1, 1) };
            _toc.hover.textColor = red;
            _tocSel = new GUIStyle(_toc);
            _tocSel.normal.textColor = red;
            _tocSel.normal.background = Solid(new Color(0.55f, 0.35f, 0.15f, 0.14f));
            _tocPart = new GUIStyle(_ink) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 4, 5, 1) };
            _tocPart.hover.textColor = red;
            // the book's buttons on its page: a darker leaf of the same paper, ink letters (not the HUD's dark buttons)
            _inkBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 4, 4), border = new RectOffset(0, 0, 0, 0) };
            _inkBtn.normal.background = Solid(new Color(0.84f, 0.78f, 0.66f, 1f));
            _inkBtn.hover.background = Solid(new Color(0.78f, 0.70f, 0.56f, 1f));
            _inkBtn.active.background = Solid(new Color(0.70f, 0.60f, 0.46f, 1f));
            _inkBtn.normal.textColor = ink; _inkBtn.hover.textColor = red; _inkBtn.active.textColor = red;
            // names on the map: light letters with a dark edge, readable over sea, snow and field alike
            _mapName = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            _mapName.normal.textColor = new Color(0.98f, 0.96f, 0.90f);
            _mapTown = new GUIStyle(_mapName) { fontSize = 16 };
            _mapSub = new GUIStyle(_mapName) { fontSize = 11, fontStyle = FontStyle.Italic };
            _mapSub.normal.textColor = new Color(0.90f, 0.88f, 0.80f);
            _mapTag = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true, padding = new RectOffset(7, 7, 3, 3) };
            _mapTag.normal.textColor = new Color(0.96f, 0.95f, 0.92f);
            _mapTag.normal.background = Solid(new Color(0.05f, 0.06f, 0.08f, 0.72f));
            _dot = Solid(Color.white);
        }

        /// <summary>A word drawn with a dark edge (four diagonal shadows, then the letters; the HUD draws its labels twice a frame, so it is kept cheap).</summary>
        static void Edged(Rect r, string text, GUIStyle st)
        {
            if (Event.current.type != EventType.Repaint) return;   // (labels take no input: only the repaint draws them)
            var c = st.normal.textColor;
            st.normal.textColor = new Color(0.04f, 0.05f, 0.06f, 0.85f);
            GUI.Label(new Rect(r.x - 1, r.y - 1, r.width, r.height), text, st);
            GUI.Label(new Rect(r.x + 1, r.y - 1, r.width, r.height), text, st);
            GUI.Label(new Rect(r.x - 1, r.y + 1, r.width, r.height), text, st);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
        }

        /// <summary>
        /// Places named on the map: each inhabited place's name over its square - a town larger - and under it what it is ("town of 864")
        /// and its bond ("pays Lounstead", "seat of 14 villages"); the most peopled first, none drawn over another, the far side of the
        /// planet and the places under the camera left out (close in, the village is around you). From orbit only the largest dozen.
        /// </summary>
        void MapNames(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (!Names || Rig == null || Rig.Cam == null) return;
            var cam = Rig.Cam;
            var P = w.Planet;
            Vector3 camPos = cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            if (alt < 420f) return;
            if (Time.unscaledTime - _byPeopleAt > 1f)
            {
                _byPeopleAt = Time.unscaledTime;
                _byPeople.Clear();
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) _byPeople.Add(sid);
                _byPeople.Sort((a, b) => { int c = w.Settlements.Get(b).Population.CompareTo(w.Settlements.Get(a).Population); return c != 0 ? c : a.CompareTo(b); });
                _placeLine.Clear(); _seats.Clear();
                // (session 27) a place's present troubles, from state: how many of its people lie sick (abed, so unseen), and a famine of the last weeks
                _sickAt.Clear();
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var pp = ref w.People.Get(id);
                    if (pp.Alive && pp.SickPhase == TC2.Core.Health.Disease.Infectious) { _sickAt.TryGetValue(pp.Home, out int k); _sickAt[pp.Home] = k + 1; }
                }
                foreach (int sid in _byPeople)
                {
                    ref var s = ref w.Settlements.Get(sid);
                    int paying = 0;
                    for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == sid && os.Inhabited) paying++; }
                    if (paying > 0 && s.Lord == 0) _seats.Add(sid);
                    _placeLine[sid] = Gazetteer.Word(in s).Substring(2) + " of " + s.Population.ToString("N0", CultureInfo.InvariantCulture) +
                                      (s.Lord != 0 && w.Settlements.Exists(s.Lord) ? ", pays " + w.History.NameOf(EntityRef.Settlement(s.Lord)) : paying > 0 ? ", lord of " + paying + (paying == 1 ? " village" : " villages") : "");
                    _sickAt.TryGetValue(sid, out int sick);
                    if (sick >= 3) _placeLine[sid] += " · " + sick + " sick";
                    if (s.FamineAt != 0 && w.Now - s.FamineAt < 24 * SimTime.Day) _placeLine[sid] += " · famine";
                }
            }
            _placed.Clear();
            for (int i = 0; i < _uiRects.Count; i++) if (_uiRects[i].width > 0f) _placed.Add(_uiRects[i]);
            int max = alt > 9000f ? 12 : 48, shown = 0;
            foreach (int sid in _byPeople)
            {
                if (shown >= max) break;
                if (!w.Settlements.Exists(sid)) continue;
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                Vector3 up = PlanetGeo.ToVector(s.Anchor);
                Vector3 at = PlanetGeo.Surface(P, s.Anchor);
                Vector3 toCam = camPos - at;
                if (Vector3.Dot(up, toCam.normalized) < 0.10f) continue;   // (over the horizon)
                Vector3 sp = cam.WorldToScreenPoint(at);
                if (sp.z <= 0f) continue;
                float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
                if (x < -60f || x > sw + 60f || y < -30f || y > sh + 30f) continue;
                if (alt < 1400f && (new Vector2(x - sw * 0.5f, y - sh * 0.5f)).magnitude < 90f) continue;   // (the village the camera is over)
                bool town = s.Population >= 300;
                string name = w.History.NameOf(EntityRef.Settlement(sid));
                var st = town ? _mapTown : _mapName;
                Vector2 ns = st.CalcSize(new GUIContent(name));
                if (!_placeLine.TryGetValue(sid, out string sub)) sub = "";
                bool withSub = alt < 9000f;
                Vector2 ss = withSub ? _mapSub.CalcSize(new GUIContent(sub)) : Vector2.zero;
                float wdt = Mathf.Max(ns.x, ss.x) + 6f, hgt = ns.y + ss.y;
                var box = new Rect(x - wdt * 0.5f, y - hgt - 7f, wdt, hgt);
                if (box.y < _left.yMax + 6f || box.yMax > sh - 46f) continue;   // (clear of the bar at the top and the ticker at the foot)
                // (session 27) a small flag in its polity's colour before its name - a realm's villages fly one colour, as their standards do; only a
                // realm's (a village alone flies none: two unbound hamlets in near pinks read as one realm - s27web2)
                bool inRealm = s.Lord != 0 || _seats.Contains(sid);
                var flag = new Rect(x - ns.x * 0.5f - (town ? 17f : 15f), box.y + ns.y * 0.5f - (town ? 5f : 4.5f), town ? 13f : 11f, town ? 9f : 8f);
                if (!inRealm) flag = new Rect(box.x + 2f, box.y, 0f, 0f);
                var room = new Rect(Mathf.Min(box.x, flag.x - 2f), box.y, box.xMax - Mathf.Min(box.x, flag.x - 2f), box.height);
                bool clash = false;
                foreach (var r in _placed) if (r.Overlaps(room)) { clash = true; break; }
                if (clash) continue;
                _placed.Add(room);
                shown++;
                GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.DrawTexture(new Rect(x - 3f, y - 3f, 6f, 6f), _dot); GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), _dot);
                if (inRealm && Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.85f); GUI.DrawTexture(new Rect(flag.x - 1f, flag.y - 1f, flag.width + 2f, flag.height + 2f), _dot);
                    GUI.color = WorldObjectsView.PolityColour(w, sid); GUI.DrawTexture(flag, _dot);
                    if (_seats.Contains(sid))   // (a realm's seat: a gold band at the flag's head)
                    { GUI.color = new Color(1f, 0.84f, 0.35f); GUI.DrawTexture(new Rect(flag.x, flag.y, flag.width, 2f), _dot); }
                    GUI.color = Color.white;
                }
                Edged(new Rect(box.x, box.y, wdt, ns.y), name, st);
                if (withSub) Edged(new Rect(box.x, box.y + ns.y - 1f, wdt, ss.y), sub, _mapSub);
            }
        }

        /// <summary>
        /// At the village view, the works named where they stand (the well, the granaries, the smithy, the training ground, the market, the
        /// weaver's shed, the kiln, the boat shed, a mine or a quarry): what a place has and where, at a glance. Standing ones only, each kind once a
        /// place (but each granary), none over another, none over a place's name.
        /// </summary>
        void WorkNames(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (!Names || Rig == null || Rig.Cam == null) return;
            var cam = Rig.Cam;
            var P = w.Planet;
            Vector3 camPos = cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            if (alt > 1500f || alt < 60f) return;
            long now = w.Now;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || (PlanetGeo.Surface(P, s.Anchor) - camPos).sqrMagnitude > 2600f * 2600f) continue;
                var bl = w.BuildingsOf(sid);
                int namedKinds = 0;
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (Structures.IsHome(b.Kind) || Boats.IsBoat(b.Kind) || b.Done == 0 || b.Left != 0) continue;
                    if ((namedKinds & (1 << (b.Kind & 31))) != 0 && b.Kind != Structures.Granary) continue;
                    Vector3 at = PlanetGeo.Surface(P, s.LocalToSphere(b.X, b.Y, P.Radius));
                    if (Vector3.Dot(at.normalized, (camPos - at).normalized) < 0.05f) continue;
                    Vector3 sp = cam.WorldToScreenPoint(at);
                    if (sp.z <= 0f) continue;
                    float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
                    if (x < 0f || x > sw || y < 40f || y > sh) continue;
                    string text = Structures.Name(b.Kind);
                    Vector2 ts = _mapSub.CalcSize(new GUIContent(text));
                    var box = new Rect(x - ts.x * 0.5f - 2f, y - ts.y - 4f, ts.x + 4f, ts.y);
                    bool clash = false;
                    foreach (var r in _placed) if (r.Overlaps(box)) { clash = true; break; }
                    if (clash) continue;
                    _placed.Add(box);
                    namedKinds |= 1 << (b.Kind & 31);
                    GUI.color = new Color(0f, 0f, 0f, 0.75f); GUI.DrawTexture(new Rect(x - 2.5f, y - 2.5f, 5f, 5f), _dot); GUI.color = new Color(1f, 0.86f, 0.5f);
                    GUI.DrawTexture(new Rect(x - 1.5f, y - 1.5f, 3f, 3f), _dot); GUI.color = Color.white;
                    Edged(box, text, _mapSub);
                }
            }
        }

        readonly System.Collections.Generic.List<Vector3> _near = new System.Collections.Generic.List<Vector3>();   // (screen x, y, person id)

        /// <summary>
        /// At the closest views (below 140 m), the people nearest the middle of the picture named over their heads - their given name small, so a
        /// street reads as people one knows; at most fourteen, none over another. Those asleep or indoors are not drawn and not named.
        /// </summary>
        void PeopleNames(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (!Names || Rig == null || Rig.Cam == null) return;
            var cam = Rig.Cam;
            Vector3 camPos = cam.transform.position;
            float alt = camPos.magnitude - (float)w.Planet.Radius;
            if (alt > 140f) return;
            double t = Host.RenderTime;
            _near.Clear();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || !Presence.Of(w, in p, t, out var pd, out _, out _, out _)) continue;
                Vector3 at = PlanetGeo.Surface(w.Planet, pd);
                if ((at - camPos).sqrMagnitude > 260f * 260f) continue;
                Vector3 sp = cam.WorldToScreenPoint(at + at.normalized * 2.1f);
                if (sp.z <= 0f) continue;
                float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
                if (x < 0f || x > sw || y < 50f || y > sh - 20f) continue;
                _near.Add(new Vector3(x, y, id));
            }
            _near.Sort((a, b) => { float da = (a.x - sw * 0.5f) * (a.x - sw * 0.5f) + (a.y - sh * 0.5f) * (a.y - sh * 0.5f), db = (b.x - sw * 0.5f) * (b.x - sw * 0.5f) + (b.y - sh * 0.5f) * (b.y - sh * 0.5f); int c = da.CompareTo(db); return c != 0 ? c : a.z.CompareTo(b.z); });
            int shown = 0;
            foreach (var v in _near)
            {
                if (shown >= 14) break;
                string full = w.History.NameOf(EntityRef.Person((int)v.z));
                int sp2 = full.IndexOf(' ');
                string given = sp2 > 0 ? full.Substring(0, sp2) : full;
                Vector2 ts = _mapSub.CalcSize(new GUIContent(given));
                var box = new Rect(v.x - ts.x * 0.5f, v.y - ts.y, ts.x, ts.y);
                bool clash = false;
                foreach (var r in _placed) if (r.Overlaps(box)) { clash = true; break; }
                if (clash) continue;
                _placed.Add(box);
                shown++;
                Edged(box, given, _mapSub);
            }
        }

        /// <summary>Close in, who or what is under the pointer: a person's name, age and doing; a building's kind and whose it is.</summary>
        void HoverTag(TC2.Core.Simulation.World w, float sw, float sh)
        {
            _tagRect = Rect.zero;
            if (Rig == null || Rig.Cam == null || PointerOverUi || _aim >= 0) return;   // (session 29: while a power is aimed its own words are the tag)
            var cam = Rig.Cam;
            float alt = cam.transform.position.magnitude - (float)w.Planet.Radius;
            if (alt > 1300f)
            {
                _hoverPid = 0; _hoverBid = 0;   // (nobody under the pointer from afar: no ring left where it last was)
                // (session 29) from afar, the village under the pointer: what it is, who leads it, what it faces - a click opens its page
                int sid = PlaceUnder(w, Input.mousePosition);
                if (sid == 0) return;
                if (_farTagFor != sid || Time.unscaledTime - _farTagAt > 1f) { _farTagFor = sid; _farTagAt = Time.unscaledTime; _farTag = FarTag(w, sid); }
                var fc = new GUIContent(_farTag);
                Vector2 fs = _mapTag.CalcSize(fc);
                float fx = Input.mousePosition.x / _scale + 14f, fy = (Screen.height - Input.mousePosition.y) / _scale + 10f;
                if (fx + fs.x > sw) fx = sw - fs.x - 4f;
                if (fy + fs.y > sh) fy = sh - fs.y - 4f;
                GUI.Box(new Rect(fx, fy, fs.x, fs.y), fc, _mapTag);
                return;
            }
            Vector2 mp = Input.mousePosition;
            if (Time.unscaledTime - _hoverAt > 0.12f)
            {
                _hoverAt = Time.unscaledTime;
                _hoverPid = 0; _hoverBid = 0;
                double t = Host.RenderTime;
                float best = 16f * _scale;
                // (session 27, context 4: the frame) only those near the ground under the pointer are placed and measured on the screen - every one of a
                // great world's people had been, eight times a second, the far side of the planet too: 11.7 ms of a running village view's 71 (s27prof1_run)
                if (!GroundUnder(mp, out Vec3d under, out _)) return;
                double R = w.Planet.Radius, cosNear = System.Math.Cos(System.Math.Max(80.0, alt * 0.25) / R);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive) continue;
                    Vec3d at = w.Agents.PositionAt(p, t);   // (where the way has them; their drawn place keeps within tens of metres of it)
                    if (at.X * under.X + at.Y * under.Y + at.Z * under.Z < cosNear) continue;
                    if (!Presence.Of(w, in p, t, out var pd, out _, out _, out _)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, pd));
                    if (sp.z < 0) continue;
                    float d = Vector2.Distance(mp, sp);
                    if (d < best) { best = d; _hoverPid = id; }
                }
                if (_hoverPid == 0)
                {
                    float bestB = 22f * _scale;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        ref var s = ref w.Settlements.Get(sid);
                        var bl = w.BuildingsOf(sid);   // (each by its own place: a mine or a quarry may stand kilometres out)
                        for (int i = 0; i < bl.Count; i++)
                        {
                            ref var b = ref w.Buildings.Get(bl[i]);
                            Vec3d bd = s.LocalToSphere(b.X, b.Y, R);
                            if (bd.X * under.X + bd.Y * under.Y + bd.Z * under.Z < cosNear) continue;
                            Vector3 sp = cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, bd));
                            if (sp.z < 0) continue;
                            float d = Vector2.Distance(mp, sp);
                            if (d < bestB) { bestB = d; _hoverBid = bl[i]; }
                        }
                    }
                }
                // (session 29: roads read) nobody and nothing built under the pointer: the worn way under it, if one passes within a few metres
                _hoverWayA = _hoverWayB = 0;
                if (_hoverPid == 0 && _hoverBid == 0) HoverWay(w, under, alt);
            }
            string text = null;
            if (_hoverPid == 0 && _hoverBid == 0 && _hoverWayA != 0 && w.Settlements.Exists(_hoverWayA) && w.Settlements.Exists(_hoverWayB))
            {
                if (Time.unscaledTime - _wayTextAt > 0.5f || _wayTextFor != _hoverWayA * 100003L + _hoverWayB)
                {
                    _wayTextAt = Time.unscaledTime; _wayTextFor = _hoverWayA * 100003L + _hoverWayB;
                    var lines = Whys.Way(w, _hoverWayA, _hoverWayB);
                    _wayText = lines.Count > 0 ? "<b>" + lines[0] + "</b>" : "";
                    for (int i = 1; i < lines.Count; i++) _wayText += "\n" + (i == 1 ? lines[i] : "<color=#e6dfcf>" + lines[i] + "</color>");
                }
                text = _wayText;
            }
            if (_hoverPid != 0 && w.People.Exists(_hoverPid))
            {
                ref var p = ref w.People.Get(_hoverPid);
                string doing = HistoryText.Doing(w, in p), why = "";
                int cut = doing.IndexOf(": ", System.StringComparison.Ordinal); if (cut > 0) { why = doing.Substring(cut + 2); doing = doing.Substring(0, cut); }
                if (doing.Length > 70) doing = doing.Substring(0, 67) + "...";
                if (why.Length > 76) why = why.Substring(0, 73) + "...";   // (session 29: the why under the what - read from the same choice)
                string standing = Biography.Standing(w, _hoverPid);   // (session 27: the first, a lord, the sworn, the first's household)
                text = "<b>" + Biography.Title(w, _hoverPid) + "</b>" + (standing.Length > 0 ? "\n<color=#f2c35f>" + standing + "</color>" : "") + "\n" + doing + (why.Length > 0 ? "\n<i><color=#c9c3b4>why: " + why + "</color></i>" : "") + "\n<size=10><color=#9a9486>click for them</color></size>";
            }
            else if (_hoverBid != 0 && w.Buildings.Exists(_hoverBid))
            {
                ref var b = ref w.Buildings.Get(_hoverBid);
                string place = w.History.NameOf(EntityRef.Settlement(b.Settlement));
                if (Structures.IsHome(b.Kind))
                    text = "<b>" + (b.Kind == Structures.House ? (Architecture.Has(b.Form) ? "A " + Architecture.PlanName(Architecture.PlanOf(b.Form)) : "A house") : "A hut") + "</b> of " + place +
                           (b.Household != 0 && w.Households.Exists(b.Household) ? "\nhome of the " + HistoryText.Plural(w.History.NameOf(EntityRef.Household(b.Household))) + " (" + w.Members(b.Household).Count + ")" +
                            (w.Settlements.Exists(b.Settlement) && w.Settlements.Get(b.Settlement).HeadHh == b.Household ? "\n<color=#f2c35f>the first household of " + place + "</color>" : "") : "\nstanding empty");
                else
                {
                    // (session 29: a village's works named with why) when it was raised, who is at it this moment, and the need its making answered (its
                    // Cause record, as the book tells it) - read every half second
                    if (Time.unscaledTime - _workTextAt > 0.5f || _workTextFor != _hoverBid) { _workTextAt = Time.unscaledTime; _workTextFor = _hoverBid; _workText = WorkTag(w, _hoverBid); }
                    text = "<b>The " + Structures.Of(b.Kind).Name + "</b> of " + place + _workText;
                }
            }
            if (text == null) return;
            var content = new GUIContent(text);
            Vector2 size = _mapTag.CalcSize(content);
            float x = mp.x / _scale + 14f, y = (Screen.height - mp.y) / _scale + 10f;
            if (x + size.x > sw) x = sw - size.x - 4f;
            if (y + size.y > sh) y = sh - size.y - 4f;
            _tagRect = new Rect(x, y, size.x, size.y);
            GUI.Box(_tagRect, content, _mapTag);
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // (session 29: the world now) read every five seconds: the age, the greatest realms, the troubles
        float _worldAt = -99f; string _worldAge = "", _worldRealmLine = "", _worldTrouble = "";
        readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>> _worldRealms = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>();

        void WorldNow(TC2.Core.Simulation.World w)
        {
            if (Time.unscaledTime - _worldAt < 5f) return;
            _worldAt = Time.unscaledTime;
            var eras = Chronicle.Eras(w);
            _worldAge = eras.Count > 0 ? "<i>" + eras[eras.Count - 1].Name + "</i> <size=11>(since year " + eras[eras.Count - 1].From + ")</size>" : "<i>its first years</i>";
            var realms = new System.Collections.Generic.Dictionary<int, int>();
            Headlines.Realms(w, realms);
            _worldRealms.Clear();
            foreach (var kv in realms) _worldRealms.Add(kv);
            _worldRealms.Sort((a, b) => { int c = b.Value.CompareTo(a.Value); return c != 0 ? c : a.Key.CompareTo(b.Key); });
            int alone = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { ref var s = ref w.Settlements.Get(sid); if (s.Inhabited && s.Lord == 0 && !realms.ContainsKey(sid)) alone++; }
            _worldRealmLine = realms.Count == 0 ? "no village pays another" : realms.Count + (realms.Count == 1 ? " realm" : " realms") + ", " + alone + (alone == 1 ? " village alone" : " villages alone") + (realms.Count > 3 ? "; the greatest:" : ":");
            if (_worldRealms.Count > 3) _worldRealms.RemoveRange(3, _worldRealms.Count - 3);
            int famine = 0, sick = 0, hungry = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                if (s.FamineAt != 0 && w.Now - s.FamineAt < 24 * SimTime.Day) famine++;
                else if (s.HungerEvent != 0) hungry++;
                if (s.Epidemic != 0) sick++;
            }
            var parts = new System.Collections.Generic.List<string>();
            if (famine > 0) parts.Add(famine + (famine == 1 ? " village in famine" : " villages in famine"));
            if (hungry > 0) parts.Add(hungry + " going hungry");
            if (sick > 0) parts.Add(sick + (sick == 1 ? " with sickness" : " with sickness"));
            int raids = 0; var an = w.Annals;   // (the annals' own counts: the realms layer counts its spears only while it is on)
            for (int i = System.Math.Max(0, an.Count - 10); i < an.Count; i++) raids += an[i].Raids;
            parts.Add(raids + (raids == 1 ? " raid" : " raids") + " in the last ten years");
            _worldTrouble = "Now: " + string.Join(", ", parts.ToArray()) + ".";
        }

        // (session 29: a village from afar) the inhabited village whose square is nearest the pointer on the screen, within 30 px, on the near side
        int _farTagFor; float _farTagAt = -99f; string _farTag = "";

        int PlaceUnder(TC2.Core.Simulation.World w, Vector2 mp)
        {
            var cam = Rig.Cam; Vector3 camPos = cam.transform.position;
            float best = 30f * _scale; int found = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                Vector3 at = PlanetGeo.Surface(w.Planet, s.Anchor);
                if (Vector3.Dot(at.normalized, (camPos - at).normalized) < 0.10f) continue;   // (over the horizon)
                Vector3 sp = cam.WorldToScreenPoint(at);
                if (sp.z <= 0f) continue;
                float d = Vector2.Distance(mp, sp);
                if (d < best) { best = d; found = sid; }
            }
            return found;
        }

        /// <summary>A village read from afar in four lines: its name and size, its bond and its first, what its people are doing, what it faces.</summary>
        string FarTag(TC2.Core.Simulation.World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            string name = w.History.NameOf(EntityRef.Settlement(sid));
            int paying = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == sid && os.Inhabited) paying++; }
            string bond = s.Lord != 0 && w.Settlements.Exists(s.Lord) ? "pays " + w.History.NameOf(EntityRef.Settlement(s.Lord)) : paying > 0 ? "lord of " + paying + (paying == 1 ? " village" : " villages") : "pays no lord";
            string first = s.Head != 0 && w.People.Exists(s.Head) ? "; first: " + w.History.NameOf(EntityRef.Person(s.Head)) : "";
            // what its people are doing now, the three largest groups
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (!p.Alive) continue; string k = DoingWord(p.Act); counts.TryGetValue(k, out int n); counts[k] = n + 1; }
            var top = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(counts);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var doing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < top.Count && i < 3; i++) doing.Add(top[i].Value + " " + top[i].Key);
            var now = Whys.Now(w, sid);
            return "<b>" + name + "</b>  " + Gazetteer.Word(in s).Substring(2) + " of " + s.Population.ToString("N0", CultureInfo.InvariantCulture) +
                   "\n<color=#f2c35f>" + bond + first + "</color>" + (doing.Count > 0 ? "\nnow: " + string.Join(", ", doing.ToArray()) : "") +
                   (now.Count > 0 ? "\n<color=#e6b8a0>" + now[0] + "</color>" : "") + "\n<size=10><color=#9a9486>click for its page</color></size>";
        }

        float _workTextAt = -99f; int _workTextFor; string _workText = "";

        /// <summary>(session 29) A work's tag under its name: being raised, fallen in, or standing since its year; how many of its village are at
        /// it now (by the work its kind is for: the smithy's smiths at this smithy, the weavers at the shed, the potters at the kiln, those cutting at
        /// the quarry, digging at the mine, burning at the hearth, practising at the ground, threshing at the barn, raising it); and why it was
        /// made - its Cause record's words. Nothing said that the world does not hold.</summary>
        static string WorkTag(TC2.Core.Simulation.World w, int bid)
        {
            ref var b = ref w.Buildings.Get(bid);
            long now = w.Now;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var k = Structures.Of(b.Kind);
            if (b.Done == 0) sb.Append("\nbeing raised: ").Append((100.0 * b.Hours / System.Math.Max(1.0, k.Hours)).ToString("0", inv)).Append(" % of its work done");
            else if (Homes.Fallen(in b, now)) sb.Append("\nfallen in: a ruin");
            else sb.Append("\nraised in year ").Append(SimTime.YearOf(b.Built).ToString(inv)).Append(b.Left != 0 ? ", unkept since year " + SimTime.YearOf(b.Left).ToString(inv) : "");
            ActivityKind act = b.Done == 0 ? ActivityKind.Build : b.Kind == Structures.Smithy ? ActivityKind.Smith : b.Kind == Structures.Workshop ? ActivityKind.Weave : b.Kind == Structures.Kiln ? ActivityKind.Pot
                : b.Kind == Structures.Quarry ? ActivityKind.Quarry : b.Kind == Structures.Mine || b.Kind == Structures.BogPits ? ActivityKind.Mine : b.Kind == Structures.CharHearth ? ActivityKind.Burn
                : b.Kind == Structures.Ground ? ActivityKind.Train : b.Kind == Structures.Barn ? ActivityKind.Thresh : ActivityKind.None;
            if (act != ActivityKind.None && !Homes.Fallen(in b, now) && w.Settlements.Exists(b.Settlement))
            {
                int same = 0;   // (the village's other works of the kind: the count is then the village's at all of them, and says so)
                var bl = w.BuildingsOf(b.Settlement);
                for (int i = 0; i < bl.Count; i++) { ref var o = ref w.Buildings.Get(bl[i]); if (o.Kind == b.Kind && o.Done != 0 && !Homes.Fallen(in o, now)) same++; }
                int n = 0;
                var res = w.Residents(b.Settlement);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var q = ref w.People.Get(res[i]);
                    if (!q.Alive || q.Act != act || q.At != b.Settlement || q.Step == StepKind.Travel) continue;
                    if (act == ActivityKind.Smith && q.WhyArg != 0 && q.WhyArg != bid) continue;   // (at another of its smithies: C1.4b, as the smoke)
                    n++;
                }
                bool all = act != ActivityKind.Smith && act != ActivityKind.Build && same > 1;
                string at = act == ActivityKind.Build ? "raising it" : act == ActivityKind.Thresh ? "threshing" : all ? "at its " + same + " " + k.Name + "s" : "at it";
                sb.Append('\n').Append(n == 0 ? "nobody " + at + " now" : n + " of its people " + at + " now");
            }
            if (b.Cause != 0 && w.History.TryGet(b.Cause, out var cause))
            {
                string why = HistoryText.Describe(w, cause);
                if (why.Length > 96) why = why.Substring(0, 93) + "...";
                sb.Append("\n<i><color=#c9c3b4>why: ").Append(why).Append("</color></i>");
            }
            sb.Append("\n<size=10><color=#9a9486>click for its page</color></size>");
            return sb.ToString();
        }

        // (session 29: roads read) the worn way under the pointer: the ways as they stand (collected every two seconds), each by its route's line
        int _hoverWayA, _hoverWayB; float _waysAt = -99f, _wayTextAt = -99f; long _wayTextFor; string _wayText = "";
        readonly System.Collections.Generic.List<TC2.Core.Agents.Way> _hoverWays = new System.Collections.Generic.List<TC2.Core.Agents.Way>();

        void HoverWay(TC2.Core.Simulation.World w, Vec3d under, float alt)
        {
            if (Time.unscaledTime - _waysAt > 2f) { _waysAt = Time.unscaledTime; w.Ways.Collect(w.Now, _hoverWays); }
            double R = w.Planet.Radius, near = System.Math.Max(6.0, alt * 0.012), best = near;
            for (int i = 0; i < _hoverWays.Count; i++)
            {
                var way = _hoverWays[i];
                if (!w.Settlements.Exists(way.A) || !w.Settlements.Exists(way.B)) continue;
                Vec3d a = w.Settlements.Get(way.A).Anchor, b = w.Settlements.Get(way.B).Anchor;
                double ab = Vec3d.ArcDistance(a, b, R);
                if (Vec3d.ArcDistance(under, a, R) + Vec3d.ArcDistance(under, b, R) > ab * 1.6 + 400.0) continue;   // (far from its ends: not this way)
                var route = w.Routes.Get(w, way.A, way.B);
                if (route == null || route.Pts == null) continue;
                for (int k = 0; k + 1 < route.Pts.Length; k++)
                {
                    Vec3d p0 = route.Pts[k], p1 = route.Pts[k + 1], d = p1 - p0;
                    double len2 = Vec3d.Dot(d, d), t = len2 > 1e-18 ? System.Math.Max(0.0, System.Math.Min(1.0, Vec3d.Dot(under - p0, d) / len2)) : 0.0;
                    double m = (p0 + d * t - under).Length * R;
                    if (m < best) { best = m; _hoverWayA = way.A; _hoverWayB = way.B; }
                }
            }
        }

        static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        /// <summary>(session 27) A rounded rectangle for 9-slicing (border 8): its fill, a one-pixel edge, soft corners.</summary>
        static Texture2D Rounded(Color fill, Color edge)
        {
            const int n = 24; const float rad = 6f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, rad, n - rad), cy = Mathf.Clamp(y + 0.5f, rad, n - rad);
                    float d = rad - Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));   // (inside the corner's arc: > 0)
                    float inside = Mathf.Clamp01(d + 0.5f), rim = Mathf.Clamp01(1.5f - Mathf.Abs(d - 0.5f));
                    var c = Color.Lerp(fill, edge, rim * edge.a);
                    c.a = inside * Mathf.Max(fill.a, rim * edge.a);
                    t.SetPixel(x, y, c);
                }
            t.Apply(false, true);
            return t;
        }

        void OnGUI()
        {
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            float sw = Screen.width / _scale, sh = Screen.height / _scale;
            var w = Host.World;
            if (w == null) { GUI.Label(new Rect(20, 20, 600, 40), Host.StatusLine, _title); return; }
            // (session 29) a catch-up or a skip is watched from orbit, the years in large type and the headlines as they come (Hud.Lapse.cs)
            if (Host.CatchingUp) { LapseOverlay(w, sw, sh, 16f); return; }
            if (Title(sw, sh)) return;   // (the opening: F11.1)

            if (_hidden) { PointerOverUi = false; WorldObjectsView.Chosen = WorldObjectsView.Pointed = WorldObjectsView.ChosenHome = WorldObjectsView.PointedHome = 0; return; }
            // (the names take no input: laid out and drawn on the repaint only - session 27, context 4: every IMGUI event had placed and measured them all
            // again, the map's names 3.6 ms of the region view's frame in s27prof1)
            if (!_book && Event.current.type == EventType.Repaint) { MapNames(w, sw, sh); WorkNames(w, sw, sh); PeopleNames(w, sw, sh); }
            // top-left: a slim bar - the date and the speeds, measured from their words (the date's widest form); on a narrow screen the speeds under the
            // date, so the bar never runs under the panels' toggles at the right (it did at 1280 px: "10 days/s" under Info)
            if (_dateW == 0f)
            {
                _dateW = _title.CalcSize(new GUIContent("Year 0000 · Early Autumn · Day 48 · 00:00")).x + 10f;
                foreach (string s in SimHost.SpeedNames) _speedsW += _btn.CalcSize(new GUIContent(s)).x + _btn.margin.horizontal;
                _speedsW += _btn.CalcSize(new GUIContent("Max")).x + _btn.margin.horizontal;
                _speedsW += _btn.CalcSize(new GUIContent("Skip...")).x + _btn.margin.horizontal + 6f;   // (session 29: the skip ahead)
                _togglesW = 24f;
                foreach (string s in new[] { "Info", "History", "Book", "World", "Realms", "Sound", "Settings" }) _togglesW += _btn.CalcSize(new GUIContent(s)).x + _btn.margin.horizontal;
            }
            float pad = _box.padding.horizontal + 4f;
            bool twoRows = 10f + _dateW + _speedsW + pad + 10f > sw - _togglesW - 10f;
            _left = twoRows ? new Rect(10, 10, Mathf.Max(_dateW, _speedsW) + pad, 70) : new Rect(10, 10, _dateW + _speedsW + pad, 40);
            GUILayout.BeginArea(_left, _box);
            if (!twoRows) GUILayout.BeginHorizontal();
            GUILayout.Label(SimTime.Format(w.Now), _title, GUILayout.Width(_dateW));
            if (twoRows) GUILayout.BeginHorizontal();
            for (int i = 0; i < SimHost.Speeds.Length; i++)
            {
                bool on = !Host.MaxSpeed && Host.SpeedIndex == i;
                if (GUILayout.Toggle(on, SimHost.SpeedNames[i], _btn) && !on) { Host.SpeedIndex = i; Host.MaxSpeed = false; }
            }
            if (GUILayout.Toggle(Host.MaxSpeed, "Max", _btn) != Host.MaxSpeed) Host.MaxSpeed = !Host.MaxSpeed;
            GUILayout.Space(6);
            _skipMenu = GUILayout.Toggle(_skipMenu, "Skip...", _btn);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            _skipRect = Rect.zero;
            SkipMenu(sw);
            // (session 29) a speed too fast to watch from the ground: the camera watches from orbit - the HUD's pages and powers wait, the headlines come
            bool lapse = Rig != null && Rig.LapseActive;
            if (lapse) LapseOverlay(w, sw, sh, _left.yMax + 10f);

            // top-right: toggles for the panels
            _toggles = new Rect(sw - _togglesW - 10f, 10, _togglesW, 40);
            GUILayout.BeginArea(_toggles, _box);
            GUILayout.BeginHorizontal();
            _info = GUILayout.Toggle(_info, "Info", _btn);
            _history = GUILayout.Toggle(_history, "History", _btn);
            _book = GUILayout.Toggle(_book, "Book", _btn);
            _worldPanel = GUILayout.Toggle(_worldPanel, "World", _btn);
            RealmsView.On = GUILayout.Toggle(RealmsView.On, "Realms", _btn);
            AudioView.Muted = !GUILayout.Toggle(!AudioView.Muted, "Sound", _btn);   // (the world's sound: F10.1; M)
            _settings = GUILayout.Toggle(_settings, "Settings", _btn);   // (F11.2)
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            _right = Rect.zero;
            float ry = 56;
            if (_info)
            {
                // (session 29: the world at the top of what can be read - world -> realm -> settlement -> household -> person) the age the world is in, its
                // greatest realms (each opening its page), what troubles it now; the tool's numbers kept small below
                WorldNow(w);
                float wh = 130f + 22f * _worldRealms.Count + 44f;
                _right = new Rect(sw - 380, ry, 370, wh);
                GUILayout.BeginArea(_right, _box);
                GUILayout.Label("<b>The world now</b>   " + _worldAge, _label);
                GUILayout.Label("<b>" + w.LivingPeople().ToString("N0", CultureInfo.InvariantCulture) + "</b> people in <b>" + w.InhabitedSettlements() + "</b> places; " + _worldRealmLine, _small);
                foreach (var kv in _worldRealms)
                    if (GUILayout.Button("   " + w.History.NameOf(EntityRef.Settlement(kv.Key)) + "'s realm - " + kv.Value + " villages  <color=#f2c35f>›</color>", _lineStyle)) ShowPlace(kv.Key, PlaceRealmTab);
                GUILayout.Label(_worldTrouble, _small);
                var P = w.Planet;
                string sea = (P.SeaLevelNow - P.SeaLevel0).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
                GUILayout.Label("climate " + P.TGlobal.ToString("0.0", CultureInfo.InvariantCulture) + " °C   ice " + (P.IceVolume / 1e9).ToString("0.0", CultureInfo.InvariantCulture) + " km³   sea " + sea + " m   tilt " + (P.Tilt * 180 / System.Math.PI).ToString("0.0", CultureInfo.InvariantCulture) + "°" + (P.Glacial == 1 ? "   (an age of ice)" : ""), _small);
                GUILayout.Label("achieved " + FormatSpeed(Host.AchievedSpeed) + "   sim " + Host.LastSimMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms   " + _fps.ToString("0", CultureInfo.InvariantCulture) + " fps   " + SystemInfo.graphicsDeviceType, _small);
                GUILayout.Label("events " + w.Metrics.Events.ToString("N0", CultureInfo.InvariantCulture) + "   history " + w.History.Count + "   drawn " + Objects.PeopleDrawn + " people, " + (Objects.TreesDrawn + TerrainLod.ForestDrawn) + " trees", _small);
                GUILayout.Label("CO2 " + P.CO2.ToString("0", CultureInfo.InvariantCulture) + " ppm   volcanoes " + P.Vents.Count + "   storms " + P.Storms.Count + "   " + Wild(w), _small);
                GUILayout.EndArea();
                ry += wh + 6f;
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
                ry += 102;
            }
            Rect realmsRect = Rect.zero;
            if (RealmsView.On) realmsRect = RealmsLegend(sw, ry, w);

            // what happened lately: the full feed behind a toggle, else a one-line ticker that fades
            _bottom = Rect.zero; _ticker = Rect.zero;
            if (_history)
            {
                _bottom = new Rect(10, sh - 250, 560, 240);
                GUILayout.BeginArea(_bottom, _box);
                // (session 29: the History panel as the book's recent pages) a line opens the book on it - why it happened, back to its root, and the
                // people and places it names; the book itself at the present age
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Recent history</b>  <size=10><color=#9a9486>a line opens why it happened</color></size>", _label);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("the book  ›", _btn)) OpenBook(-1);
                GUILayout.EndHorizontal();
                int from = Mathf.Max(0, Host.Feed.Count - 10);
                for (int i = from; i < Host.Feed.Count; i++)
                {
                    long id = i < Host.FeedIds.Count ? Host.FeedIds[i] : 0;
                    if (GUILayout.Button("<color=#c9a25a>›</color> " + Host.Feed[i], _small) && id != 0 && w.History.TryGet(id, out _)) { _book = true; _line = id; _whyScroll = Vector2.zero; }
                }
                GUILayout.EndArea();
            }
            else if (_news.Length > 0 && _newsAge < 14f && !lapse)
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
            float topY = Mathf.Max(62f, _left.yMax + 8f);   // (below the top bar, two rows of it on a narrow screen)
            if (_book) { float bw = Mathf.Min(1280f, sw - 60f); _bookRect = new Rect(sw * 0.5f - bw * 0.5f, topY, bw, sh - 50f - topY); Book(_bookRect, w); }

            // inspector (session 27, context 4: not over the open book - s27book6 had the chosen person's card over its right-hand page; the book holds
            // the screen while it is open, and a name clicked in it closes it for that page)
            Rect insp = new Rect(sw - 400, 190, 390, 300);
            Rect card = new Rect(sw - 436f, Mathf.Max(ry + 6f + (realmsRect.height > 0f ? realmsRect.height + 6f : 0f), _left.yMax + 10f), 420f, 268f);   // (session 29: the person's card as a small leaf - Hud.Person.cs)
            bool showInsp = !_book && !lapse && !_personPage && _selected != 0 && w.People.Exists(_selected);   // (session 29: the page holds all the card held)
            if (showInsp) PersonCard(card, w);
            _pageRect = Rect.zero;
            float pageH = sh - 136f - topY;   // (ending above the power bar: its foot had run under it - s27web2)
            if (_book || lapse) { /* (the book holds the screen; the timelapse the picture) */ }
            else if (_placeSid != 0 && w.Settlements.Exists(_placeSid)) { float pw = Mathf.Min(760f, sw - 40f); _pageRect = new Rect(sw - 16f - pw, topY - 2f, pw, pageH); PlacePage(_pageRect, w); }
            else if (_personPage && _selected != 0 && w.People.Exists(_selected)) { float pw = Mathf.Min(820f, sw - 40f); _pageRect = new Rect(sw - 16f - pw, topY - 2f, pw, pageH); PersonSheet(_pageRect, w); }   // (session 29: Hud.Person.cs)
            else if (_building != 0 && w.Buildings.Exists(_building)) { showInsp = true; insp = new Rect(sw - 476f, card.y, 460f, Mathf.Min(430f, sh - card.y - 140f)); BuildingPage(insp, w); }   // (session 29: a leaf, larger)
            _powersRect = Rect.zero; _aimRect = Rect.zero;
            if (!_book && !lapse) { PowerBar(w, sw, sh); DrawCastNote(sw); }   // (the god's powers: F9.1; the last cast's word: session 29)
            _handRect = Rect.zero;
            if (!_book && !lapse && _pageRect.width <= 0f) YourHand(w, sw, sh);   // (what came of your casts, live: session 27; not over a person's or a place's page - s27book5)
            if (!_book && !lapse) LapseSummaryLeaf(w, sw, sh); else _summaryRect = Rect.zero;   // (what the years did: Hud.Lapse.cs)
            if (!_book) DrawTip(); else _tip = null;
            _settingsRect = Rect.zero;
            if (_settings) SettingsPanel(sw);
            KeepSettings();
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / _scale;
            bool overLapse = lapse && PointerOverUi;   // (the overlay's own rects, set as it drew)
            PointerOverUi = overLapse || _skipRect.Contains(m) || _summaryRect.Contains(m) || _settingsRect.Contains(m) || _powersRect.Contains(m) || _left.Contains(m) || _toggles.Contains(m) || _right.Contains(m) || worldRect.Contains(m) || realmsRect.Contains(m) || _bottom.Contains(m) || _ticker.Contains(m) || (showInsp && (_building != 0 ? insp.Contains(m) : card.Contains(m))) || (_book && _bookRect.Contains(m)) || _pageRect.Contains(m);
            if (!_book) HoverTag(w, sw, sh);
            _uiRects.Clear();
            _uiRects.Add(_left); _uiRects.Add(_toggles); _uiRects.Add(_right); _uiRects.Add(worldRect); _uiRects.Add(realmsRect); _uiRects.Add(_bottom); _uiRects.Add(_ticker);
            _uiRects.Add(_powersRect); _uiRects.Add(_handRect); _uiRects.Add(_summaryRect); _uiRects.Add(_settingsRect); _uiRects.Add(_pageRect); _uiRects.Add(_tagRect);
            _uiRects.Add(_aimRect); _uiRects.Add(_tipShown);
            if (showInsp) _uiRects.Add(_building != 0 ? insp : card);
            // (session 27) who is ringed on the ground: the person whose page is open, the one under the pointer, the chosen home's household
            WorldObjectsView.Chosen = _personPage ? _selected : 0;
            WorldObjectsView.Pointed = !_book && !PointerOverUi ? _hoverPid : 0;
            WorldObjectsView.ChosenHome = _building;
            // (session 27) the building under the pointer and the one chosen marked on the ground round their footprint, a home in its household's colour
            // (its people ringed with it)
            WorldObjectsView.PointedHome = !_book && !PointerOverUi && _hoverPid == 0 ? _hoverBid : 0;
            if (Event.current.type == EventType.Repaint) { MarkBuilding(w, 0, WorldObjectsView.PointedHome); MarkBuilding(w, 1, _building); }
        }

        static void MarkBuilding(TC2.Core.Simulation.World w, int slot, int bid)
        {
            if (bid == 0 || !w.Buildings.Exists(bid)) return;
            ref var b = ref w.Buildings.Get(bid);
            if (!w.Settlements.Exists(b.Settlement)) return;
            ref var s = ref w.Settlements.Get(b.Settlement);
            bool home = Structures.IsHome(b.Kind);
            double r = (home ? Footprints.Reach(Footprints.PlanOf(in b)) : Footprints.WorkR(b.Kind)) + 0.6;
            Color c = home && b.Household != 0 ? WorldObjectsView.HouseholdColour(w, b.Settlement, b.Household) : new Color(1f, 0.86f, 0.5f);
            AimView.MarkAt(slot, s.LocalToSphere(b.X, b.Y, w.Planet.Radius), r, c);
        }

        /// <summary>
        /// The realms map layer's legend (D2-167): each realm - a lordship's head and the villages that pay it, directly or through their lords - in its
        /// colour on the ground, largest first, with its villages and people; and how many villages pay a lord and how many pay none. Read from the
        /// world (`RealmsView.Legend`, refreshed each second). "Alone": in no realm - paying nobody and paid by nobody.
        /// </summary>
        Rect RealmsLegend(float sw, float ry, TC2.Core.Simulation.World w)
        {
            var legend = RealmsView.Legend;
            int shown = Mathf.Min(legend.Count, 8);
            int paying = 0, free = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                if (s.Lord != 0 && w.Settlements.Exists(s.Lord) && w.Settlements.Get(s.Lord).Inhabited) paying++;   // (as RealmsView reads a bond)
                else if (w.Conflict.PolitySize(sid) == 0) free++;
            }
            bool wars = RealmsView.FeudRaids > 0;
            var r = new Rect(sw - 340, ry, 330, 52 + 20 * Mathf.Max(1, shown) + (legend.Count > shown ? 18 : 0) + (wars ? 36 : 0));
            GUILayout.BeginArea(r, _box);
            GUILayout.Label("<b>Realms</b>   " + paying + (paying == 1 ? " village pays" : " villages pay") + " a lord, " + free + " stand" + (free == 1 ? "s" : "") + " alone", _label);
            if (legend.Count == 0) GUILayout.Label("No village pays tribute to another now.", _small);
            var old = GUI.color;
            for (int i = 0; i < shown; i++)
            {
                var e = legend[i];
                GUILayout.BeginHorizontal();
                Rect sq = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
                sq.y += 3;
                GUI.color = new Color(e.c.r, e.c.g, e.c.b, 1f);
                GUI.DrawTexture(sq, Texture2D.whiteTexture);
                GUI.color = old;
                if (GUILayout.Button(w.History.NameOf(EntityRef.Settlement(e.sid)) + " - " + e.villages + " villages, " + e.people + " people  <color=#f2c35f>›</color>", _lineStyle)) ShowPlace(e.sid, PlaceRealmTab);   // (session 29: the realm's page)
                GUILayout.EndHorizontal();
            }
            if (legend.Count > shown) GUILayout.Label("and " + (legend.Count - shown) + " more", _small);
            // the wars of the last ten years (dark spearheads toward the raided): how many raids, between how many pairs, and how many pairs of one realm
            if (wars)
                GUILayout.Label("Dark spears edged pale: " + RealmsView.FeudRaids + (RealmsView.FeudRaids == 1 ? " raid" : " raids") + " in the last ten years between " + RealmsView.FeudPairs +
                                (RealmsView.FeudPairs == 1 ? " pair of villages" : " pairs of villages") + (RealmsView.FeudsWithin > 0 ? ", " + RealmsView.FeudsWithin + " of one realm" : ", none of one realm"), _small);
            GUILayout.EndArea();
            return r;
        }

        /// <summary>Opens a person's page (tools: shots of the page; a click in the world selects, the button opens it).</summary>
        public void ShowPerson(int pid, int tab = 0) { _book = false; _placeSid = 0; _selected = pid; _building = 0; _personPage = true; _pageTab = tab; _pagePid = -1; }

        /// <summary>Opens a place's page (C12.2): from a person's card, a structure's page, or the tools.</summary>
        public void ShowPlace(int sid, int tab = 0) { _book = false; _personPage = false; _placeSid = sid; _placeTab = tab; _placeBuilt = -1; }

        /// <summary>
        /// The settlement page (C12.2, D2-106): a place a section at a time - its founding and people, its food, its works, what it takes from
        /// the land and makes, its neighbours and the wrongs between them, its history. The text comes from Gazetteer (Core), rebuilt once a
        /// second; nothing here writes the world.
        /// </summary>
        void PlacePage(Rect r, TC2.Core.Simulation.World w)
        {
            if (_placeFor != _placeSid || _placeBuilt != _placeTab || Time.unscaledTime - _placeAt > 1f)
            {
                _placeFor = _placeSid; _placeBuilt = _placeTab; _placeAt = Time.unscaledTime;
                _placeTitle = Gazetteer.Title(w, _placeSid);
                switch (_placeTab)
                {
                    case 0: _placeText = Gazetteer.Overview(w, _placeSid) + "\n" + Gazetteer.Crafts(w, _placeSid); break;
                    case PlaceNowTab: case PlaceWhyTab: case PlaceRealmTab: _placeText = ""; break;
                    case 4: _placeText = Gazetteer.Food(w, _placeSid); break;
                    case 5: _placeText = Gazetteer.Works(w, _placeSid); break;
                    case 6: _placeText = Gazetteer.Materials(w, _placeSid); break;
                    case 7: _placeText = Gazetteer.Neighbours(w, _placeSid); break;
                    default: _placeText = Gazetteer.Life(w, _placeSid, 80); break;
                }
            }
            // (session 27) a leaf of the book
            int sidNow = _placeSid;
            System.Action<float> custom = _placeTab == PlaceNowTab ? (cw => PlaceNow(w, sidNow, cw)) : _placeTab == PlaceWhyTab ? (cw => PlaceWhy(w, sidNow, cw)) : _placeTab == PlaceRealmTab ? (System.Action<float>)(cw => PlaceRealm(w, sidNow, cw)) : null;
            int tab = PaperPage(r, "A Place", _placeTitle, "", PlaceTabs, _placeTab, _placeText, out bool close, custom);
            if (tab != _placeTab) { _placeTab = tab; _pageScroll = Vector2.zero; }
            if (close) _placeSid = 0;
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
                if (b.Household != 0 && w.Households.Exists(b.Household)) sb.Append("home of the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(b.Household)))).Append(" (").Append(w.Members(b.Household).Count).Append(" people)\n");
                else sb.Append("standing empty").Append(b.Last != 0 ? "; the " + TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(b.Last))) + " lived in it last" : "").Append('\n');
                if (b.Style != 0) sb.Append("raised by the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(b.Style)))).Append("\n");
                if (Architecture.Has(b.Form) && b.Kind == Structures.House) sb.Append(Architecture.Describe(b.Form, true)).Append(" (").Append(Architecture.Why(b.Form, true)).Append(")\n");   // (how it was built: D2-114)
                if (Architecture.Has(b.Form) && b.Kind == Structures.House && !Architecture.WaysOff && w.Settlements.Exists(b.Settlement))
                {
                    // (C11.1b) its roof's way: its village's founders', or its builders' own - carried from the house they grew up in or the village they came
                    // from, or taken from the village's first household or a lord's seat (the cold's half-hips are a hipped way under snow)
                    int founders = Architecture.TraditionRoof(w.Settlements.Get(b.Settlement).Tradition), roof = Architecture.RoofOf(b.Form);
                    bool asFounders = roof == founders || (roof == Architecture.HalfHip && founders == Architecture.Hipped);
                    sb.Append(asFounders ? "roofed as its village's founders built\n" : "roofed otherwise than its village's founders built: in its builders' own way\n");
                }
                if (b.Kind == Structures.House) sb.Append(b.Mat >= 1 ? "on stone footings (" + b.Stone.ToString("0", CultureInfo.InvariantCulture) + " loads of stone): it stands twice as long once left\n" : "on earth-fast posts: no stone near enough\n");
            }
            else if (w.Settlements.Exists(b.Settlement))
            {
                ref var s = ref w.Settlements.Get(b.Settlement);
                if (Architecture.Has(b.Form) && (b.Kind == Structures.Barn || b.Kind == Structures.Granary || b.Kind == Structures.Smithy || b.Kind == Structures.Workshop || b.Kind == Structures.Kiln)) sb.Append(Architecture.Describe(b.Form, false)).Append(" (").Append(Architecture.Why(b.Form, false)).Append(")\n");
                if (k.Function == Function.Store)
                {
                    double cap = Structures.StoreCapacity(in s);
                    sb.Append("keeps grain: up to ").Append(k.Capacity.ToString("N0", CultureInfo.InvariantCulture)).Append(" days' food for one; ")
                      .Append(place).Append("'s ").Append(s.Granaries).Append(s.Granaries == 1 ? " granary" : " granaries").Append(s.Barns > 0 ? " and " + s.Barns + (s.Barns == 1 ? " barn" : " barns") : "").Append(" hold ").Append(s.Granary.ToString("N0", CultureInfo.InvariantCulture))
                      .Append(" of ").Append(cap.ToString("N0", CultureInfo.InvariantCulture)).Append(s.Granary > cap ? " - the rest lies in heaps and rots fast" : "").Append('\n');
                    if (s.Sheaves > 0.5) sb.Append(s.Sheaves.ToString("N0", CultureInfo.InvariantCulture)).Append(" of it still in the sheaf, threshed at the floor as its people need it\n");   // (C3.7b)
                }
                else if (b.Kind == Structures.Well) sb.Append("water for ").Append(s.Population).Append(" people, drawn by the pot\n");
                else if (b.Kind == Structures.BoatShed)
                {
                    // the village's boat shed (C9.2b): what is on its stocks, its boatwrights
                    int stocks = Boats.OnTheStocks(w, b.Settlement);
                    sb.Append("plank boats are built here, one at a time");
                    if (stocks != 0) { ref var sb2 = ref w.Buildings.Get(stocks); sb.Append(": on the stocks the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(sb2.Household)))).Append("' (").Append((100.0 * sb2.Hours / Boats.PlankHours).ToString("0", CultureInfo.InvariantCulture)).Append(" % done)"); }
                    sb.Append("; its best boatwright knows the craft at ").Append(s.BestBoat.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
                }
                else if (Boats.IsBoat(b.Kind))
                {
                    // a household's dugout (C9.1) or plank boat (C9.2b): whose, what the sea gives against the shore, how long it will last
                    int owner = b.Household != 0 ? b.Household : b.Last;
                    if (owner != 0) sb.Append(b.Household != 0 ? "the " : "left by the ").Append(TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(owner)))).Append("'").Append(b.Household != 0 ? " boat" : "").Append('\n');
                    if (b.Cond <= 0f) sb.Append("overturned at sea and lost\n");
                    else if (b.Done != 0 && b.Left == 0)
                        sb.Append("seats ").Append(Boats.SeatsOf(b.Kind) == 4 ? "four" : "two").Append("; goes out in winds below ").Append(Boats.MaxOf(b.Kind).ToString("0", CultureInfo.InvariantCulture)).Append(" m/s to the open sea's grounds within ").Append((WildFood.SeaReach / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" km: an hour there is worth ")
                          .Append(Boats.SeaExpect(w, in s, Boats.IsPlank(b.Kind)).ToString("0.0", CultureInfo.InvariantCulture)).Append(" days' food against ").Append(s.Fish.Expect.ToString("0.0", CultureInfo.InvariantCulture))
                          .Append(" on the shore; it will be past mending in about ").Append(System.Math.Max(0.0, Boats.YearsOf(b.Kind) - age).ToString("0", CultureInfo.InvariantCulture)).Append(" years\n");
                }
                else if (b.Kind == Structures.Smithy)
                {
                    double share = TC2.Core.Knowledge.Tech.MetalShare(s.Metal, s.Population), want = Forge.ArmsWant(in s);
                    // its smiths (C1.4b): the village's best, who have its four places - and how practised a hand each has
                    var smiths = new System.Collections.Generic.List<string>(); var res = w.Residents(b.Settlement);
                    for (int i = 0; i < res.Count; i++)
                    {
                        ref var q = ref w.People.Get(res[i]);
                        if (Forge.Maker(in s, in q, w.Now) && Forge.SmithyFor(w, in s, in q) == _building) smiths.Add(w.History.NameOf(EntityRef.Person(res[i])) + (q.SkSmith >= 0.3f ? " (practised)" : " (new to it)"));
                    }
                    sb.Append("the smith's workshop: smelts and forges at twice an open hearth's pace").Append(b.Mat >= 1 ? ", on a hearth of stone" : "").Append("; ")
                      .Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours worked here\n")
                      .Append("its smiths: ").Append(smiths.Count > 0 ? string.Join(", ", smiths) : "nobody").Append('\n')
                      .Append(place).Append("'s tools: ").Append(s.Metal.ToString("0", CultureInfo.InvariantCulture)).Append(" kg of metal (").Append((share * 100).ToString("0", CultureInfo.InvariantCulture)).Append("% of what its people want); arms ")
                      .Append(s.Arms.ToString("0", CultureInfo.InvariantCulture)).Append(want > 0 ? " - it wants " + want.ToString("0", CultureInfo.InvariantCulture) + ": it fears its neighbours" : "").Append('\n');
                }
                else if (b.Kind == Structures.Workshop)
                {
                    // the weaver's shed (C3.1): its makers - the village's best weavers - what they made, what its households took and its traders sold
                    var makers = new System.Collections.Generic.List<string>(); var res = w.Residents(b.Settlement);
                    for (int i = 0; i < res.Count; i++) if (Weaving.Maker(in s, in w.People.Get(res[i]), w.Now) && Weaving.ShedFor(w, in s, in w.People.Get(res[i])) == _building) makers.Add(w.History.NameOf(EntityRef.Person(res[i])));
                    sb.Append("broad looms, a warping frame, a dye vat: ").Append(place).Append("'s best weavers make its wares here, half as fast again as at home and lasting longer than homespun; ")
                      .Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture)).Append(" wares made in ").Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours\n")
                      .Append("its looms: ").Append(makers.Count > 0 ? string.Join(", ", makers) : "nobody").Append('\n')
                      .Append("in store ").Append(s.Wares.ToString("0", CultureInfo.InvariantCulture)).Append(" of the ").Append(Weaving.Target(in s).ToString("0", CultureInfo.InvariantCulture)).Append(" it keeps; its households took ")
                      .Append(s.WaresOut.ToString("0", CultureInfo.InvariantCulture)).Append(" lately and wear wares for ").Append((100.0 * System.Math.Min(1.0, s.WaresHeld / System.Math.Max(1.0, Goods.Target * s.Population))).ToString("0", CultureInfo.InvariantCulture))
                      .Append(" % of its people").Append(s.WaresSold > 0.5f ? "; sold " + s.WaresSold.ToString("0", CultureInfo.InvariantCulture) + " lately" : "").Append("; fibre ").Append(s.Fibre.ToString("0", CultureInfo.InvariantCulture)).Append(" kg in store\n");
                }
                else if (b.Kind == Structures.Kiln)
                {
                    // the potter's kiln (C3.2): its potters - the village's best - what they fired, the jars its granaries hold, the pots its hearths took, sold
                    var makers = new System.Collections.Generic.List<string>(); var res = w.Residents(b.Settlement);
                    for (int i = 0; i < res.Count; i++) if (Pottery.Maker(in s, in w.People.Get(res[i]), w.Now)) makers.Add(w.History.NameOf(EntityRef.Person(res[i])));
                    double held = System.Math.Min(s.Granary, Structures.StoreCapacity(in s));
                    sb.Append("an updraught kiln by ").Append(place).Append("'s clay, ").Append(Pottery.Dist(w, in s, _building).ToString("0", CultureInfo.InvariantCulture)).Append(" m out: its best potters dig the clay, throw and fire jars and pots here on the village's wood; ")
                      .Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture)).Append(" kg fired in ").Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours\n")
                      .Append("its potters: ").Append(makers.Count > 0 ? string.Join(", ", makers) : "nobody").Append('\n')
                      .Append("jars in its granaries: ").Append(s.Jars.ToString("0", CultureInfo.InvariantCulture)).Append(" kg, keeping ").Append((100.0 * Pottery.Sealed(in s)).ToString("0", CultureInfo.InvariantCulture)).Append(" % of the ")
                      .Append(held.ToString("0", CultureInfo.InvariantCulture)).Append(" days' grain there sealed; ware in store ").Append(s.Pots.ToString("0", CultureInfo.InvariantCulture)).Append(" of the ").Append(Pottery.Target(in s).ToString("0", CultureInfo.InvariantCulture)).Append(" kg it keeps")
                      .Append(s.PotsOut > 0.5f ? "; hearths that cannot fire their own took " + s.PotsOut.ToString("0", CultureInfo.InvariantCulture) + " kg lately" : "")
                      .Append(s.PotsSold > 0.5f ? "; sold " + s.PotsSold.ToString("0", CultureInfo.InvariantCulture) + " kg lately" : "").Append('\n');
                }
                else if (b.Kind == Structures.Market)
                    sb.Append("stalls on the green: traders come from up to ").Append((Trade.MarketReach / 1000.0).ToString("0", CultureInfo.InvariantCulture))
                      .Append(" km and deal better here; ").Append(s.TradeIn.ToString("0", CultureInfo.InvariantCulture)).Append(" came to deal this past year (")
                      .Append(b.Stock.ToString("0", CultureInfo.InvariantCulture)).Append(" the year it was laid out)\n");
                else if (b.Kind == Structures.Ground)
                {
                    int trained = 0; var res = w.Residents(b.Settlement);
                    for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).SkFight >= 0.1f) trained++;
                    sb.Append("practice with spear and shield: ").Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours here; ").Append(trained).Append(" of ").Append(place)
                      .Append("'s people have some skill, its best ").Append(s.BestFight.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append(Forge.Threatened(in s) ? " - it fears its neighbours" : " - unused while it fears no one").Append('\n');
                }
                else if (b.Kind == Structures.CharHearth)
                    sb.Append("burns charcoal for ").Append(place).Append("'s smiths from wood felled at its woodlot: ").Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture))
                      .Append(" kg burnt here in ").Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours (a load of wood makes 5 kg); the store holds ")
                      .Append(s.Charcoal.ToString("0", CultureInfo.InvariantCulture)).Append(" kg").Append(s.ClampUntil > w.Now && b.Left == 0 ? "; a clamp is smouldering" : "").Append('\n');
                else if (b.Kind == Structures.BogPits)
                    sb.Append("digs bog iron ore and carries it home to ").Append(place).Append(": ").Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture)).Append(" kg dug in ")
                      .Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours; the store holds ").Append(s.FeOre.ToString("0", CultureInfo.InvariantCulture)).Append(" kg (its smiths want ")
                      .Append(s.FeOreWant.ToString("0", CultureInfo.InvariantCulture)).Append("); iron tools for ").Append((100.0 * TC2.Core.Knowledge.Tech.IronShare(in s)).ToString("0", CultureInfo.InvariantCulture)).Append(" % of its people\n");
                else if (b.Kind == Structures.Mine)
                {
                    // the body it digs (C5.1): what is left in it, or when it gave out
                    sb.Append("digs copper ore and carries it home to ").Append(place).Append(": ").Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture)).Append(" kg dug in ")
                      .Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours; the store holds ").Append(s.Ore.ToString("0", CultureInfo.InvariantCulture)).Append(" kg of ore (its smiths want ")
                      .Append(s.OreWant.ToString("0", CultureInfo.InvariantCulture)).Append(")\n");
                    var cs = w.Planet.Grid.CellOf(s.LocalToSphere(b.X, b.Y, w.Planet.Radius));   // (a mine stands at its body's cell centre)
                    int di = w.Deposits.IndexAt(cs, Mining.Copper);
                    if (di >= 0)
                    {
                        var d = w.Deposits.Get(di);
                        sb.Append(d.GaveOut != 0 ? "its copper gave out in year " + (d.GaveOut / SimTime.Year) + " after " + (d.Taken / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " t of ore"
                                                 : "the body holds about " + (d.Ore / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " t of ore more (" + (d.Taken / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " t dug from it by everyone)").Append('\n');
                    }
                }
                else if (b.Kind == Structures.Quarry)
                    sb.Append("cuts building stone at rock ").Append((s.StoneDist / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" km from ").Append(place).Append(": ")
                      .Append(b.Stock.ToString("N0", CultureInfo.InvariantCulture)).Append(" loads cut and carried home in ").Append(b.Use.ToString("N0", CultureInfo.InvariantCulture)).Append(" hours; the store holds ")
                      .Append(s.Stone.ToString("0", CultureInfo.InvariantCulture)).Append(" loads (it wants ").Append(s.StoneWant.ToString("0", CultureInfo.InvariantCulture)).Append(")\n");
                if (b.Hours > 0) sb.Append("raised with ").Append(b.Hours.ToString("0", CultureInfo.InvariantCulture)).Append(" hours of work and ").Append(b.Wood.ToString("0", CultureInfo.InvariantCulture)).Append(" loads of wood").Append(b.Stone > 0 ? ", " + b.Stone.ToString("0", CultureInfo.InvariantCulture) + " of stone" : "").Append('\n');
                if (b.Leader != 0) sb.Append("the first at the work: ").Append(w.History.NameOf(EntityRef.Person(b.Leader))).Append('\n');
                if (b.Cause != 0 && w.History.TryGet(b.Cause, out var ce)) sb.Append("<b>why</b>: ").Append(HistoryText.Describe(w, ce)).Append('\n');
                else if (b.Hours <= 0) sb.Append("standing when its people came\n");
            }
            var evs = w.History.EventsOf(new EntityRef(EntityKind.Building, _building));
            if (evs != null && evs.Count > 0)
            {
                sb.Append("<b>history</b>\n");
                for (int i = System.Math.Max(0, evs.Count - 6); i < evs.Count; i++)
                    if (w.History.TryGet(evs[i], out var e)) sb.Append(HistoryText.Dated(w, e)).Append('\n');
            }
            // (session 29) a leaf of the book like the person's and the place's: its kind as a kicker, its name, its words; why it was raised as a story
            PersonStyles();
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(r.x - 5f, r.y - 4f, r.width + 10f, r.height + 8f), _leather);
                GUI.DrawTexture(r, _leaf);
            }
            string all = sb.ToString();
            int nl = all.IndexOf('\n');
            string title = (nl > 0 ? all.Substring(0, nl) : all).Replace("<b>", "").Replace("</b>", "");
            string body = nl > 0 ? all.Substring(nl + 1).Replace("<b>why</b>: ", "Why: ").Replace("<b>history</b>\n", "-- its history\n") : "";
            GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 10f, r.width - 28f, r.height - 16f));
            GUILayout.BeginHorizontal();
            Kicker(home ? "A Home" : Boats.IsBoat(b.Kind) ? "A Boat" : "A Work");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("close  ×", _bkNav, GUILayout.Width(84))) _building = 0;
            GUILayout.EndHorizontal();
            GUILayout.Label(title, _pgBig);
            _bScroll = GUILayout.BeginScrollView(_bScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            foreach (string raw in body.Split('\n'))
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("-- ", System.StringComparison.Ordinal)) { GUILayout.Label(Cap(t.Substring(3)), _pgHead); continue; }
                int colon = t.IndexOf(": ", System.StringComparison.Ordinal);
                if (t.StartsWith("Why: ", System.StringComparison.Ordinal)) t = "<color=#8c2614>Why</color>  " + t.Substring(5);
                else if (t.StartsWith("Year ", System.StringComparison.Ordinal) && colon > 0 && colon < 30) t = "<color=#8c2614>" + t.Substring(5, colon - 5) + "</color>  " + t.Substring(colon + 2);
                GUILayout.Label(t, _pgEntry);
            }
            // why it was raised, told as a story from its root (Core/History/Whys)
            if (b.Cause != 0 && w.History.TryGet(b.Cause, out _)) WhyToggle(w, "Why was it raised? The story from its root", b.Cause, r.width - 60f);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("its village  ›", _bkNav)) ShowPlace(b.Settlement);
            if (home && b.Household != 0 && w.Households.Exists(b.Household))
            {
                var mem = w.Members(b.Household);
                int eldest = 0; double eldestAge = -1;
                for (int i = 0; i < mem.Count; i++) { ref var q = ref w.People.Get(mem[i]); if (q.Alive && q.AgeYears(w.Now) > eldestAge) { eldestAge = q.AgeYears(w.Now); eldest = mem[i]; } }
                if (eldest != 0 && GUILayout.Button("its household  ›", _bkNav)) { _building = 0; ShowPerson(eldest, 4); }
            }
            if (b.Leader != 0 && w.People.Exists(b.Leader) && GUILayout.Button("who led it  ›", _bkNav)) { _building = 0; ShowPerson(b.Leader, 5); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// The History Book (D2-077; session 25: a book): a two-page spread - on the left the title, the preface and the contents (eight
        /// parts, the open part's chapters), on the right the chapter: its headings, the historian's prose over the records, the numbers,
        /// and its lines, each the record it stands on; a chosen line's causes back to their root (each a record to follow in turn) and the
        /// place to go to. A part is written when it is first opened (HistoryBook.Writer); a new year rewrites the book. Writes nothing.
        /// </summary>
        void Book(Rect r, TC2.Core.Simulation.World w)
        {
            int year = (int)(w.Now / SimTime.Year);
            if (_writer == null || year != _bookYear)
            {
                _writer = new HistoryBook.Writer(w);
                _mapSeat = -1;   // (a new book: its realm maps drawn anew)
                _parts = new BookPart[HistoryBook.PartTitles.Length];
                _bookYear = year;
            }
            if (_openPart != NoRequest)
            {
                if (_openPart >= 0 && _openPart < _parts.Length) { Part(_openPart); _bookPart = _openPart; _bookChapter = Mathf.Clamp(_openChapter, 0, Mathf.Max(0, _parts[_openPart].Chapters.Count - 1)); }
                else _bookPart = _openPart == TimelinePart ? TimelinePart : -1;
                _openPart = NoRequest; _bookScroll = Vector2.zero;
            }
            if (_openAt != -2)
            {
                // an age asked for (-1 the present): the Ages, at that age's chapter
                Part(0);
                int n = _parts[0].Chapters.Count;
                _bookPart = 0; _bookChapter = _openAt < 0 || _openAt >= n ? n - 1 : _openAt; _openAt = -2;
                _bookScroll = Vector2.zero;
            }
            BookSpread(r, w);   // (session 27: the book as an open book - Hud.Book.cs)
        }

        // (the book's realm maps, 25 §8) a realm's portrait: the land round its seat at its height, its villages in its ink, the rest in grey - the
        // ground drawn once a chapter into a texture, the villages as dots over it
        const int MapW = 256, MapH = 160;
        Texture2D _mapTex; int _mapSeat = -1, _mapYear = -1; double _mapHalf;
        Vec3d _mapAt, _mapE, _mapN;
        readonly System.Collections.Generic.List<int> _mapIn = new System.Collections.Generic.List<int>(), _mapOut = new System.Collections.Generic.List<int>();

        /// <summary>A point on the planet in the map's frame (m east and north of the seat, along the ground: an azimuthal projection).</summary>
        void MapXY(Vec3d p, out double x, out double y)
        {
            double R = Host.World.Planet.Radius, c = System.Math.Max(-1.0, System.Math.Min(1.0, Vec3d.Dot(_mapAt, p)));
            Vec3d d = p - _mapAt * c; double len = d.Length;
            if (len < 1e-12) { x = 0; y = 0; return; }
            double th = System.Math.Acos(c) * R / len;
            x = Vec3d.Dot(d, _mapE) * th; y = Vec3d.Dot(d, _mapN) * th;
        }

        void BuildRealmMap(TC2.Core.Simulation.World w, BookChapter ch)
        {
            _mapSeat = ch.MapSeat; _mapYear = ch.MapYear;
            _writer.RealmAt(ch.MapSeat, ch.MapYear, _mapIn, _mapOut);
            var P = w.Planet; double R = P.Radius;
            _mapAt = w.Settlements.Get(ch.MapSeat).Anchor;
            TC2.Core.Planet.Storms.Frame(_mapAt, out _mapE, out _mapN);
            double far = 0;
            foreach (int sid in _mapIn) far = System.Math.Max(far, Vec3d.ArcDistance(_mapAt, w.Settlements.Get(sid).Anchor, R));
            _mapHalf = System.Math.Min(System.Math.PI * R * 0.8, System.Math.Max(1500.0, far * 1.3 + 400.0));   // (half the map's height, m)
            if (_mapTex == null) { _mapTex = new Texture2D(MapW, MapH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear }; }
            var px = new Color32[MapW * MapH];
            double scale = 2.0 * _mapHalf / MapH;   // m a pixel
            var parchment = new Color(0.86f, 0.79f, 0.63f); var wood = new Color(0.62f, 0.64f, 0.46f); var sea = new Color(0.60f, 0.68f, 0.72f); var ice = new Color(0.92f, 0.93f, 0.94f);
            for (int j = 0; j < MapH; j++)
                for (int i = 0; i < MapW; i++)
                {
                    double x = (i + 0.5 - MapW * 0.5) * scale, y = (j + 0.5 - MapH * 0.5) * scale, rr = System.Math.Sqrt(x * x + y * y);
                    Vec3d p = _mapAt;
                    if (rr > 1e-9) { double th = rr / R; p = (_mapAt * System.Math.Cos(th) + (_mapE * (x / rr) + _mapN * (y / rr)) * System.Math.Sin(th)).Normalized; }
                    int c = P.Grid.CellOf(p);
                    Color col = c < 0 ? sea : !P.IsLand(c) || P.IsLake(c) ? sea : P.Ice[c] > 5f ? ice : Color.Lerp(parchment, wood, 0.7f * P.Forest[c]);
                    px[j * MapW + i] = col;
                }
            _mapTex.SetPixels32(px);
            _mapTex.Apply(false);
        }

        void RealmMap(TC2.Core.Simulation.World w, BookChapter ch, float pageW)
        {
            if (_mapTex == null || _mapSeat != ch.MapSeat || _mapYear != ch.MapYear) BuildRealmMap(w, ch);
            float h = Mathf.Min(180f, pageW * 0.3f), mw = h * MapW / MapH;
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            Rect box = GUILayoutUtility.GetRect(mw, h, GUILayout.Width(mw), GUILayout.Height(h));
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            GUILayout.Label("<size=11>" + w.History.NameOf(EntityRef.Settlement(ch.MapSeat)) + "'s realm in year " + ch.MapYear + ": its villages in its ink, the others grey; " + OneDec(2.0 * _mapHalf / 1000.0) + " km from top to foot</size>", _inkFact);
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(box, _mapTex);
            Color ink = RealmInk[0];
            for (int k = 0; k < 2; k++)
            {
                var list = k == 0 ? _mapOut : _mapIn;
                foreach (int sid in list)
                {
                    MapXY(w.Settlements.Get(sid).Anchor, out double x, out double y);
                    float u = box.x + box.width * (0.5f + (float)(x / (2.0 * _mapHalf * MapW / MapH))), v = box.y + box.height * (0.5f - (float)(y / (2.0 * _mapHalf)));
                    if (u < box.x || u > box.xMax || v < box.y || v > box.yMax) continue;
                    bool seat = sid == ch.MapSeat; float d = seat ? 9f : k == 0 ? 4f : 6f;
                    GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(new Rect(u - d * 0.5f - 1f, v - d * 0.5f - 1f, d + 2f, d + 2f), _dot);
                    GUI.color = k == 0 ? new Color(0.45f, 0.42f, 0.38f) : ink; GUI.DrawTexture(new Rect(u - d * 0.5f, v - d * 0.5f, d, d), _dot);
                }
            }
            GUI.color = Color.white;
        }

        static string OneDec(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>
        /// The Timeline page: the world's years at a glance - its ages as bands across the top, under them each great realm's villages year by year
        /// stacked in its own ink (the height of the whole the world's places), the people as a dark line over them, and its greatest moments.
        /// The chart is drawn once into a texture when the book is written; the page draws it and its words.
        /// </summary>
        void TimelinePage(TC2.Core.Simulation.World w, Rect r)
        {
            if (_timeline == null || _timelineYear != _bookYear)
            {
                var t0 = System.Diagnostics.Stopwatch.StartNew();
                _timeline = HistoryBook.Timeline(w);
                _timelineYear = _bookYear;
                if (_timelineTex != null) Destroy(_timelineTex);
                _timelineTex = DrawTimeline(_timeline, w);
                Debug.Log("[TC2] book: the timeline written in " + t0.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
            }
            var tl = _timeline;
            GUILayout.Label("A Timeline of the World", _inkTitle);
            GUILayout.Label(tl.To > tl.From ? "years " + tl.From + " to " + tl.To + ": its ages, its realms and its people" : "its first days", _inkSub);
            _bookScroll = GUILayout.BeginScrollView(_bookScroll, GUILayout.Height(Mathf.Max(120f, r.height - 210f)));   // (room left for the leaf's foot: it was clipped at 720 px)
            float cw = Mathf.Max(200f, r.width * 0.64f - 100f);   // (the right leaf's text width: Hud.Book.cs)
            float ch = 250f;
            Rect ages = GUILayoutUtility.GetRect(cw, 20f);
            Rect chart = GUILayoutUtility.GetRect(cw, ch);
            Rect axis = GUILayoutUtility.GetRect(cw, 22f);
            if (_inkFactCentre == null) _inkFactCentre = new GUIStyle(_inkFact) { alignment = TextAnchor.UpperCenter, padding = new RectOffset(0, 0, 0, 0) };
            int n = tl.To - tl.From + 1;
            if (Event.current.type == EventType.Repaint && n > 1)
            {
                // the ages: alternate shades, named where there is room
                for (int i = 0; i < tl.Ages.Count; i++)
                {
                    var e = tl.Ages[i];
                    float x0 = chart.x + cw * (e.From - tl.From) / (float)(n - 1), x1 = chart.x + cw * Mathf.Min(n - 1, e.To - tl.From) / (float)(n - 1);
                    GUI.color = i % 2 == 0 ? new Color(0.55f, 0.35f, 0.15f, 0.16f) : new Color(0.55f, 0.35f, 0.15f, 0.07f);
                    GUI.DrawTexture(new Rect(x0, ages.y, Mathf.Max(1f, x1 - x0), ages.height + ch), _dot);
                    GUI.color = Color.white;
                    string name = e.Name.StartsWith("The ") ? e.Name.Substring(4) : e.Name;
                    if (_inkFact.CalcSize(new GUIContent(name)).x < x1 - x0 - 4f) GUI.Label(new Rect(x0 + 2f, ages.y, x1 - x0, ages.height), name, _inkFact);
                }
                GUI.DrawTexture(chart, _timelineTex);
                for (int y = (tl.From / 100 + 1) * 100; y <= tl.To; y += 100)
                {
                    float x = chart.x + cw * (y - tl.From) / (float)(n - 1);
                    GUI.Label(new Rect(x - 30f, axis.y, 60f, axis.height + 6f), y.ToString(CultureInfo.InvariantCulture), _inkFactCentre);
                }
            }
            // (session 27) the ages under the pointer: its band lit, the year marked, the age named with its years and that year's people and greatest
            // realm; a click opens the age's chapter
            Rect span = new Rect(chart.x, ages.y, cw, ages.height + ch);
            Vector2 mp = Event.current.mousePosition;
            if (n > 1 && span.Contains(mp))
            {
                int k = Mathf.Clamp(Mathf.RoundToInt((mp.x - chart.x) / cw * (n - 1)), 0, n - 1), year = tl.From + k;
                int ai = -1;
                for (int i = 0; i < tl.Ages.Count; i++) if (year >= tl.Ages[i].From && (year < tl.Ages[i].To || i == tl.Ages.Count - 1)) { ai = i; break; }
                if (Event.current.type == EventType.Repaint)
                {
                    if (ai >= 0)
                    {
                        var e = tl.Ages[ai];
                        float x0 = chart.x + cw * (e.From - tl.From) / (float)(n - 1), x1 = chart.x + cw * Mathf.Min(n - 1, e.To - tl.From) / (float)(n - 1);
                        GUI.color = new Color(0.55f, 0.15f, 0.08f, 0.13f); GUI.DrawTexture(new Rect(x0, span.y, Mathf.Max(1f, x1 - x0), span.height), _dot);
                    }
                    GUI.color = new Color(0.55f, 0.15f, 0.08f, 0.8f); GUI.DrawTexture(new Rect(mp.x, span.y, 1f, span.height), _dot); GUI.color = Color.white;
                    int bigV = 0; string big = null;
                    for (int ri = 0; ri < tl.Realms.Count; ri++) if (tl.Realms[ri].Villages[k] > bigV) { bigV = tl.Realms[ri].Villages[k]; big = tl.Realms[ri].Name; }
                    string tip = (ai >= 0 ? "<b>" + tl.Ages[ai].Name + "</b>  <size=11>years " + tl.Ages[ai].From + "-" + tl.Ages[ai].To + "</size>\n" : "") +
                                 "Year " + year + ": " + tl.People[k].ToString("N0", CultureInfo.InvariantCulture) + " people" +
                                 (big != null ? "; the greatest realm " + big + "'s, " + bigV + " villages" : "") + (ai >= 0 ? "\n<size=11><i>click to read the age</i></size>" : "");
                    var content = new GUIContent(tip);
                    Vector2 size = _mapTag.CalcSize(content);
                    float tx = Mathf.Min(mp.x + 12f, span.xMax - size.x), ty = span.y + 22f;
                    GUI.Box(new Rect(Mathf.Max(span.x, tx), ty, size.x, size.y), content, _mapTag);
                }
                else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && ai >= 0)
                {
                    Part(0);
                    var chs = _parts[0].Chapters;
                    int at = Mathf.Min(ai, chs.Count - 1);
                    for (int c = 0; c < chs.Count; c++) if (chs[c].Title == tl.Ages[ai].Name) { at = c; break; }
                    _bookPart = 0; _bookChapter = at; _bookScroll = Vector2.zero; _line = 0;
                    Event.current.Use();
                }
            }
            // the realms' inks, the people's line
            GUILayout.Space(6);
            GUILayout.Label("Each band is a realm: the villages of its lord and of every tier under it, year by year; the whole height is the world's places. The dark line is the world's people (at most " +
                            tl.MaxPeople.ToString("N0", CultureInfo.InvariantCulture) + ").", _ink);
            for (int i = 0; i < tl.Realms.Count; i++)
            {
                var rr = tl.Realms[i];
                string legend = rr.Name + " - at its height " + rr.Peak + " villages, year " + rr.PeakYear;
                Rect line = GUILayoutUtility.GetRect(cw, Mathf.Max(18f, _ink.CalcHeight(new GUIContent(legend), cw - 20f)));
                if (Event.current.type == EventType.Repaint) { GUI.color = RealmInk[i % RealmInk.Length]; GUI.DrawTexture(new Rect(line.x, line.y + 6f, 14f, 10f), _dot); GUI.color = Color.white; }
                GUI.Label(new Rect(line.x + 20f, line.y, line.width - 20f, line.height), legend, _ink);
            }
            if (tl.Marks.Count > 0)
            {
                GUILayout.Label("Its greatest moments", _inkHead);
                foreach (var m in tl.Marks) GUILayout.Label("Year " + m.Year + ": " + m.Text + ".", _ink);
            }
            GUILayout.EndScrollView();
        }

        /// <summary>The timeline's chart in pixels: the realms stacked in their inks over the world's places, the people's line, on clear paper.</summary>
        GUIStyle _inkFactCentre;

        Texture2D DrawTimeline(BookTimeline tl, TC2.Core.Simulation.World w)
        {
            const int TW = 720, TH = 250;
            var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tc2-timeline" };
            var px = new Color32[TW * TH];
            int n = tl.To - tl.From + 1;
            if (n > 1)
            {
                // the scale: the most places the world has had (the realms' villages are a share of them)
                int places = 1;
                var a = w.Annals;
                for (int i = 0; i < a.Count; i++) places = Mathf.Max(places, a[i].Places);
                int prevPy = -1;
                for (int x = 0; x < TW; x++)
                {
                    int k = Mathf.Clamp(Mathf.RoundToInt(x * (n - 1) / (float)(TW - 1)), 0, n - 1);
                    int y = 0;
                    for (int ri = 0; ri < tl.Realms.Count; ri++)
                    {
                        int v = tl.Realms[ri].Villages[k];
                        if (v <= 0) continue;
                        int hgt = Mathf.Max(1, Mathf.RoundToInt(v / (float)places * (TH - 8)));
                        Color c = RealmInk[ri % RealmInk.Length];
                        var c32 = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 215);
                        for (int j = y; j < Mathf.Min(TH, y + hgt); j++) px[j * TW + x] = c32;
                        y += hgt;
                    }
                    // the people: a dark line two pixels thick, joined to the column before it (a fall or a rise between two columns is drawn, not a gap)
                    if (tl.MaxPeople > 0 && tl.People[k] > 0)
                    {
                        int py = Mathf.Clamp(Mathf.RoundToInt(tl.People[k] / (float)tl.MaxPeople * (TH - 4)), 0, TH - 2);
                        int lo = py, hi = py + 1;
                        if (prevPy >= 0) { lo = Mathf.Min(lo, prevPy); hi = Mathf.Max(hi, prevPy + 1); }
                        for (int j = lo; j <= hi; j++) px[j * TW + x] = new Color32(40, 28, 18, 235);
                        prevPy = py;
                    }
                }
                // the moments: a short tick at the top
                foreach (var m in tl.Marks)
                {
                    int x = Mathf.Clamp(Mathf.RoundToInt((m.Year - tl.From) / (float)(n - 1) * (TW - 1)), 0, TW - 1);
                    for (int j = TH - 10; j < TH; j++) px[j * TW + x] = new Color32(120, 30, 20, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Writes a part of the book if it is not written yet.</summary>
        void Part(int i)
        {
            if (_parts[i] != null) return;
            _parts[i] = _writer.Part(i);
            Debug.Log("[TC2] book: part " + HistoryBook.Roman(i + 1) + " written in " + _writer.LastMs.ToString("0", CultureInfo.InvariantCulture) + " ms (" + _parts[i].Chapters.Count + " chapters)");
        }

        /// <summary>The next or previous chapter, across parts (the preface before the first).</summary>
        void Turn(int d)
        {
            _line = 0; _bookScroll = Vector2.zero;
            if (_bookPart == -1) { if (d > 0) _bookPart = TimelinePart; return; }
            if (_bookPart == TimelinePart) { if (d > 0) { Part(0); _bookPart = 0; _bookChapter = 0; } else _bookPart = -1; return; }
            int k = _bookChapter + d;
            if (k >= 0 && k < _parts[_bookPart].Chapters.Count) { _bookChapter = k; return; }
            int p = _bookPart + d;
            if (p < 0) { _bookPart = TimelinePart; return; }
            if (p >= _parts.Length) return;
            Part(p);
            _bookPart = p;
            _bookChapter = d > 0 ? 0 : _parts[p].Chapters.Count - 1;
        }

        /// <summary>Where an event happened, for the camera: its village, else its cell.</summary>
        static bool Place(TC2.Core.Simulation.World w, in HistoryEvent e, out double lat, out double lon)
        {
            lat = 0; lon = 0;
            if (e.Settlement > 0 && w.Settlements.Exists(e.Settlement)) { var a = w.Settlements.Get(e.Settlement).Anchor; lat = a.Latitude; lon = a.Longitude; return true; }
            if (e.Cell >= 0 && e.Cell < w.Planet.Cells) { var c = w.Planet.Grid.Center[e.Cell]; lat = c.Latitude; lon = c.Longitude; return true; }
            return false;
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

        /// <summary>(F11.2) The player's settings: the sound and its loudness, the clouds, lighter shadows from high up, the picture's detail - kept
        /// for the next visit. Drawn over the panels at the top right.</summary>
        void SettingsPanel(float sw)
        {
            _settingsRect = new Rect(sw - 350, 56, 340, 226);
            GUILayout.BeginArea(_settingsRect, _box);
            GUILayout.Label("<b>Settings</b>   kept for your next visit", _label);
            bool muted = AudioView.Muted, lite = Sky.ShadowLite; float vol = AudioView.Volume, clouds = Settings.Clouds, detail = Settings.Detail;
            GUILayout.BeginHorizontal();
            AudioView.Muted = !GUILayout.Toggle(!AudioView.Muted, "Sound", _btn, GUILayout.Width(72));
            GUILayout.BeginVertical(); GUILayout.Space(10); AudioView.Volume = GUILayout.HorizontalSlider(AudioView.Volume, 0f, 1f); GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Label("Clouds: " + (Settings.Clouds < 0.05f ? "none" : Settings.Clouds < 0.75f ? "thin" : Settings.Clouds > 1.2f ? "thick" : "as the weather makes them"), _small);
            Settings.Clouds = GUILayout.HorizontalSlider(Settings.Clouds, 0f, 1.5f);
            Sky.ShadowLite = GUILayout.Toggle(Sky.ShadowLite, "Lighter shadows from high up (faster)", _btn);
            GUILayout.Label("Picture detail: " + (Settings.Detail * 100f).ToString("0", CultureInfo.InvariantCulture) + " %" + (Application.isEditor ? " (in the built game)" : ""), _small);
            Settings.Detail = GUILayout.HorizontalSlider(Settings.Detail, 0.5f, 1f);
            GUILayout.EndArea();
            if (muted != AudioView.Muted || lite != Sky.ShadowLite || vol != AudioView.Volume || clouds != Settings.Clouds || detail != Settings.Detail)
            {
                Settings.Apply();
                _settingsDirty = Time.unscaledTime;
            }
        }

        /// <summary>(F11.2) Keeps the settings half a second after they last changed (a slider is not written every frame); the Sound toggle and
        /// the M key change them too. Only in play: shots and batch runs never load or keep them.</summary>
        void KeepSettings()
        {
            if (!Settings.Loaded) return;
            if (AudioView.Muted != _lastMuted) { _lastMuted = AudioView.Muted; if (_settingsDirty == 0f) _settingsDirty = Time.unscaledTime; }
            if (_settingsDirty > 0f && Time.unscaledTime - _settingsDirty > 0.5f) { Settings.Save(); _settingsDirty = 0f; }
        }
    }
}
