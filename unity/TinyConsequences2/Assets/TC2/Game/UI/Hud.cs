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
        GUIStyle _box, _label, _small, _title, _btn;
        float _scale = 1f;
        Rect _left, _right, _bottom, _toggles, _ticker;
        float _fps; int _frames; float _fpsT;
        bool _info, _history, _worldPanel, _hidden, _book;
        // the person page (C12.1): a person's life in sections, from the world's state and its ledger (Core/History/Biography)
        bool _personPage; int _pageTab, _pagePid = -1, _pageTabBuilt = -1; float _pageAt; string _pageText = "", _pageTitle = ""; Vector2 _pageScroll; Rect _pageRect;
        static readonly string[] PageTabs = { "Now", "Family", "Work", "Deeds", "Home", "Life" };
        // the settlement page (C12.2): a place's founding, people, food, works, land and making, neighbours and history (Core/History/Gazetteer)
        int _placeSid, _placeTab, _placeBuilt = -1, _placeFor; float _placeAt; string _placeText = "", _placeTitle = "";
        static readonly string[] PlaceTabs = { "Place", "Food", "Works", "Land", "Neighbours", "History" };
        // the History Book (session 25, HistoryBook: a book - its title page and preface, eight parts and their chapters on the left page,
        // a chapter on the right, a line's causes under it; each part written when it is first opened, the book rewritten in a new year)
        int _bookPart = -1, _bookChapter, _bookYear = -1, _openAt = -2;
        HistoryBook.Writer _writer;
        BookPart[] _parts;
        long _line;
        Vector2 _listScroll, _bookScroll, _whyScroll;
        readonly long[] _chain = new long[16];
        GUIStyle _lineStyle, _lineSel, _page, _sheet, _ink, _inkTitle, _inkHead, _inkFact, _inkSub, _inkLine, _inkLineSel, _toc, _tocSel, _tocPart, _inkBtn;
        Rect _bookRect;

        /// <summary>Opens the History Book at an age (the page a tool or a URL asks for; -1 the present age).</summary>
        public void OpenBook(int era) { _book = true; _openAt = era; _line = 0; }

        /// <summary>Opens the History Book at a part and chapter (-1: the preface) - the page a tool asks for.</summary>
        public void OpenBookAt(int part, int chapter) { _book = true; _openPart = part; _openChapter = chapter; _line = 0; }
        public void CloseBook() { _book = false; }
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
        readonly System.Collections.Generic.List<int> _byPeople = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.Dictionary<int, string> _placeLine = new System.Collections.Generic.Dictionary<int, string>();
        float _byPeopleAt = -99f;
        int _hoverPid, _hoverBid; float _hoverAt;
        string _news = ""; float _newsAge = 99f; int _newsCount;

        void Update()
        {
            _frames++; _fpsT += Time.unscaledDeltaTime;
            if (_fpsT > 0.5f) { _fps = _frames / _fpsT; _frames = 0; _fpsT = 0; }
            var w = Host.World;
            if (w == null || Host.CatchingUp) return;
            if (_titleOn) return;   // (the title holds the world still: F11.1)
            if (!_bookAsked) { _bookAsked = true; if (Browser.Param("book") != null) OpenBook(-1); }   // (?book=1: open at the present age)
            if (Input.GetKeyDown(KeyCode.B)) _book = !_book;
            if (Input.GetKeyDown(KeyCode.Space)) Host.SpeedIndex = Host.SpeedIndex == 0 ? 3 : 0;
            if (Input.GetKeyDown(KeyCode.H)) _hidden = !_hidden;
            if (Input.GetKeyDown(KeyCode.R)) RealmsView.On = !RealmsView.On;   // (the realms map layer, D2-167)
            if (Input.GetKeyDown(KeyCode.N)) Names = !Names;   // (places named on the map)
            if (Host.Feed.Count != _newsCount) { _newsCount = Host.Feed.Count; _news = _newsCount > 0 ? Host.Feed[_newsCount - 1] : ""; _newsAge = 0f; }
            _newsAge += Time.unscaledDeltaTime;
            for (int i = 0; i < SimHost.Speeds.Length && i < 8; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Host.SpeedIndex = i; Host.MaxSpeed = false; }
            if (Input.GetKeyDown(KeyCode.Alpha9)) Host.MaxSpeed = !Host.MaxSpeed;
            if (!PowersInput() && Input.GetMouseButtonUp(0) && !PointerOverUi) Pick();   // (a click while aiming a power casts it: F9.1)
            // following: the camera keeps over the person as they go about their day (its height as the player left it)
            var fw = Host.World;
            if (_follow != 0 && fw != null)
            {
                if (!fw.People.Exists(_follow) || !fw.People.Get(_follow).Alive) _follow = 0;
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
                _placeLine.Clear();
                foreach (int sid in _byPeople)
                {
                    ref var s = ref w.Settlements.Get(sid);
                    int paying = 0;
                    for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var os = ref w.Settlements.Get(o); if (os.Lord == sid && os.Inhabited) paying++; }
                    _placeLine[sid] = Gazetteer.Word(in s).Substring(2) + " of " + s.Population.ToString("N0", CultureInfo.InvariantCulture) +
                                      (s.Lord != 0 && w.Settlements.Exists(s.Lord) ? ", pays " + w.History.NameOf(EntityRef.Settlement(s.Lord)) : paying > 0 ? ", lord of " + paying + (paying == 1 ? " village" : " villages") : "");
                }
            }
            _placed.Clear();
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
                if (box.y < 48f || box.yMax > sh - 46f) continue;   // (clear of the bar at the top and the ticker at the foot)
                bool clash = false;
                foreach (var r in _placed) if (r.Overlaps(box)) { clash = true; break; }
                if (clash) continue;
                _placed.Add(box);
                shown++;
                GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.DrawTexture(new Rect(x - 3f, y - 3f, 6f, 6f), _dot); GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), _dot);
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
            if (Rig == null || Rig.Cam == null || PointerOverUi) return;
            var cam = Rig.Cam;
            float alt = cam.transform.position.magnitude - (float)w.Planet.Radius;
            if (alt > 1300f) return;
            Vector2 mp = Input.mousePosition;
            if (Time.unscaledTime - _hoverAt > 0.12f)
            {
                _hoverAt = Time.unscaledTime;
                _hoverPid = 0; _hoverBid = 0;
                double t = Host.RenderTime;
                float best = 16f * _scale;
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive || !Presence.Of(w, in p, t, out var pd, out _, out _, out _)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, pd));
                    if (sp.z < 0) continue;
                    float d = Vector2.Distance(mp, sp);
                    if (d < best) { best = d; _hoverPid = id; }
                }
                if (_hoverPid == 0)
                {
                    float bestB = 22f * _scale;
                    Vector3 camPos = cam.transform.position;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        ref var s = ref w.Settlements.Get(sid);
                        if ((PlanetGeo.Surface(w.Planet, s.Anchor) - camPos).sqrMagnitude > 2500f * 2500f) continue;
                        var bl = w.BuildingsOf(sid);
                        for (int i = 0; i < bl.Count; i++)
                        {
                            ref var b = ref w.Buildings.Get(bl[i]);
                            Vector3 sp = cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, s.LocalToSphere(b.X, b.Y, w.Planet.Radius)));
                            if (sp.z < 0) continue;
                            float d = Vector2.Distance(mp, sp);
                            if (d < bestB) { bestB = d; _hoverBid = bl[i]; }
                        }
                    }
                }
            }
            string text = null;
            if (_hoverPid != 0 && w.People.Exists(_hoverPid))
            {
                ref var p = ref w.People.Get(_hoverPid);
                string doing = HistoryText.Doing(w, in p);
                int cut = doing.IndexOf(':'); if (cut > 0) doing = doing.Substring(0, cut);
                if (doing.Length > 70) doing = doing.Substring(0, 67) + "...";
                text = "<b>" + Biography.Title(w, _hoverPid) + "</b>\n" + doing;
            }
            else if (_hoverBid != 0 && w.Buildings.Exists(_hoverBid))
            {
                ref var b = ref w.Buildings.Get(_hoverBid);
                string place = w.History.NameOf(EntityRef.Settlement(b.Settlement));
                if (Structures.IsHome(b.Kind))
                    text = "<b>" + (b.Kind == Structures.House ? (Architecture.Has(b.Form) ? "A " + Architecture.PlanName(Architecture.PlanOf(b.Form)) : "A house") : "A hut") + "</b> of " + place +
                           (b.Household != 0 && w.Households.Exists(b.Household) ? "\nhome of the " + HistoryText.Plural(w.History.NameOf(EntityRef.Household(b.Household))) + " (" + w.Members(b.Household).Count + ")" : "\nstanding empty");
                else text = "<b>The " + Structures.Of(b.Kind).Name + "</b> of " + place;
            }
            if (text == null) return;
            var content = new GUIContent(text);
            Vector2 size = _mapTag.CalcSize(content);
            float x = mp.x / _scale + 14f, y = (Screen.height - mp.y) / _scale + 10f;
            if (x + size.x > sw) x = sw - size.x - 4f;
            if (y + size.y > sh) y = sh - size.y - 4f;
            GUI.Box(new Rect(x, y, size.x, size.y), content, _mapTag);
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void OnGUI()
        {
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            float sw = Screen.width / _scale, sh = Screen.height / _scale;
            var w = Host.World;
            if (w == null) { GUI.Label(new Rect(20, 20, 600, 40), Host.StatusLine, _title); return; }
            if (Host.CatchingUp) { CatchUpScreen(sw, sh); PointerOverUi = true; return; }
            if (Title(sw, sh)) return;   // (the opening: F11.1)

            if (_hidden) { PointerOverUi = false; return; }
            if (!_book) { MapNames(w, sw, sh); WorkNames(w, sw, sh); PeopleNames(w, sw, sh); }
            // top-left: a slim bar - the date and the speeds
            _left = new Rect(10, 10, Mathf.Min(sw - 450, 900), 40);
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
            _toggles = new Rect(sw - 440, 10, 430, 40);
            GUILayout.BeginArea(_toggles, _box);
            GUILayout.BeginHorizontal();
            _info = GUILayout.Toggle(_info, "Info", _btn);
            _history = GUILayout.Toggle(_history, "History", _btn);
            _book = GUILayout.Toggle(_book, "Book", _btn);
            _worldPanel = GUILayout.Toggle(_worldPanel, "World", _btn);
            RealmsView.On = GUILayout.Toggle(RealmsView.On, "Realms", _btn);
            AudioView.Muted = !GUILayout.Toggle(!AudioView.Muted, "Sound", _btn);   // (the world's sound: F10.1; M)
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
            _pageRect = Rect.zero;
            if (_placeSid != 0 && w.Settlements.Exists(_placeSid)) { float pw = Mathf.Min(600, sw - 430); _pageRect = new Rect(Mathf.Max(10, sw - 410 - pw), 60, pw, sh - 130); PlacePage(_pageRect, w); }
            else if (_personPage && _selected != 0 && w.People.Exists(_selected)) { float pw = Mathf.Min(600, sw - 430); _pageRect = new Rect(Mathf.Max(10, sw - 410 - pw), 60, pw, sh - 130); PersonPage(_pageRect, w); }
            else if (_building != 0 && w.Buildings.Exists(_building)) { showInsp = true; BuildingPage(insp, w); }
            _powersRect = Rect.zero;
            if (!_book) PowerBar(w, sw, sh);   // (the god's powers: F9.1)
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / _scale;
            PointerOverUi = _powersRect.Contains(m) || _left.Contains(m) || _toggles.Contains(m) || _right.Contains(m) || worldRect.Contains(m) || realmsRect.Contains(m) || _bottom.Contains(m) || _ticker.Contains(m) || (showInsp && insp.Contains(m)) || (_book && _bookRect.Contains(m)) || _pageRect.Contains(m);
            if (!_book) HoverTag(w, sw, sh);
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
                GUILayout.Label(w.History.NameOf(EntityRef.Settlement(e.sid)) + " - " + e.villages + " villages, " + e.people + " people", _small);
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
            if (p.KNets >= 0.1f || p.KLoom >= 0.1f || p.KQuern >= 0.1f || p.KPottery >= 0.1f || p.KArd >= 0.1f || p.KMetal >= 0.1f || p.KIron >= 0.1f || p.KBoat >= 0.1f)
            {
                sb.Append("knows:");
                if (p.KNets >= 0.1f) sb.Append(" nets ").Append(p.KNets.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KLoom >= 0.1f) sb.Append(" loom ").Append(p.KLoom.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KQuern >= 0.1f) sb.Append(" quern ").Append(p.KQuern.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KPottery >= 0.1f) sb.Append(" pots ").Append(p.KPottery.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KArd >= 0.1f) sb.Append(" ard ").Append(p.KArd.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KMetal >= 0.1f) sb.Append(" copper ").Append(p.KMetal.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KIron >= 0.1f) sb.Append(" iron ").Append(p.KIron.ToString("0.00", CultureInfo.InvariantCulture));
                if (p.KBoat >= 0.1f) sb.Append(" boats ").Append(p.KBoat.ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append('\n');
            }
            sb.Append("skills: farming ").Append(p.SkFarm.ToString("0.00", CultureInfo.InvariantCulture)).Append(", wood ").Append(p.SkWood.ToString("0.00", CultureInfo.InvariantCulture)).Append(", hunting ").Append(p.SkHunt.ToString("0.00", CultureInfo.InvariantCulture)).Append(", fishing ").Append(p.SkFish.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(p.SkFight > 0.005f ? ", fighting " + p.SkFight.ToString("0.00", CultureInfo.InvariantCulture) : "").Append(p.Fights > 0 ? " (" + p.Fights + (p.Fights == 1 ? " fight" : " fights") + " lived through)" : "")
              .Append("   trips ").Append(p.Visits).Append('\n');
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_follow == _selected ? "Stop following" : "Follow", _btn)) _follow = _follow == _selected ? 0 : _selected;
            if (GUILayout.Button(_personPage ? "Close life page" : "Life page", _btn)) { _personPage = !_personPage; _pagePid = -1; _placeSid = 0; }
            if (p.Home != 0 && GUILayout.Button("Their village", _btn)) ShowPlace(p.Home);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// The person page (C12.1, D2-099; 07 §9): who they are, then a section at a time - what they are doing now and why, their family, their
        /// work and what they know, their deeds and what they can do in a fight, their home, their life as the ledger keeps it. The text comes
        /// from Biography (Core), rebuilt once a second; nothing here writes the world.
        /// </summary>
        void PersonPage(Rect r, TC2.Core.Simulation.World w)
        {
            if (_pagePid != _selected || _pageTabBuilt != _pageTab || Time.unscaledTime - _pageAt > 1f)
            {
                _pagePid = _selected; _pageTabBuilt = _pageTab; _pageAt = Time.unscaledTime;
                _pageTitle = Biography.Title(w, _selected);
                string body;
                switch (_pageTab)
                {
                    case 0: body = Biography.Identity(w, _selected) + "\n" + Biography.Now(w, _selected); break;
                    case 1: body = Biography.Family(w, _selected); break;
                    case 2: body = Biography.Work(w, _selected); break;
                    case 3: body = Biography.Deeds(w, _selected); break;
                    case 4: body = Biography.Home(w, _selected); break;
                    default: body = Biography.Life(w, _selected, 80); break;
                }
                _pageText = body;
            }
            GUILayout.BeginArea(r, _page);
            GUILayout.Label(_pageTitle, _title);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PageTabs.Length; i++) if (GUILayout.Toggle(_pageTab == i, PageTabs[i], _btn) && _pageTab != i) _pageTab = i;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", _btn)) _personPage = false;
            GUILayout.EndHorizontal();
            _pageScroll = GUILayout.BeginScrollView(_pageScroll);
            GUILayout.Label(_pageText, _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
                    case 1: _placeText = Gazetteer.Food(w, _placeSid); break;
                    case 2: _placeText = Gazetteer.Works(w, _placeSid); break;
                    case 3: _placeText = Gazetteer.Materials(w, _placeSid); break;
                    case 4: _placeText = Gazetteer.Neighbours(w, _placeSid); break;
                    default: _placeText = Gazetteer.Life(w, _placeSid, 80); break;
                }
            }
            GUILayout.BeginArea(r, _page);
            GUILayout.Label(_placeTitle, _title);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PlaceTabs.Length; i++) if (GUILayout.Toggle(_placeTab == i, PlaceTabs[i], _btn) && _placeTab != i) _placeTab = i;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", _btn)) _placeSid = 0;
            GUILayout.EndHorizontal();
            _pageScroll = GUILayout.BeginScrollView(_pageScroll);
            GUILayout.Label(_placeText, _small);
            GUILayout.EndScrollView();
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Its village", _btn)) ShowPlace(b.Settlement);
            if (GUILayout.Button("Close", _btn)) _building = 0;
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
            GUILayout.BeginArea(r, _sheet);
            GUILayout.BeginHorizontal();
            // the left page: the title, the contents
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(300f, r.width * 0.32f)));
            GUILayout.Label(_writer.Book.Title, _inkTitle);
            GUILayout.Label(_writer.Book.Sub, _inkSub);
            _listScroll = GUILayout.BeginScrollView(_listScroll);
            if (GUILayout.Button("Preface", _bookPart == -1 ? _tocSel : _tocPart)) { _bookPart = -1; _bookScroll = Vector2.zero; _line = 0; }
            if (GUILayout.Button("A Timeline of the World", _bookPart == TimelinePart ? _tocSel : _tocPart)) { _bookPart = TimelinePart; _bookScroll = Vector2.zero; _line = 0; }
            for (int i = 0; i < HistoryBook.PartTitles.Length; i++)
            {
                if (GUILayout.Button(HistoryBook.Roman(i + 1) + ". " + HistoryBook.PartTitles[i], _tocPart) && _bookPart != i) { Part(i); _bookPart = i; _bookChapter = 0; _bookScroll = Vector2.zero; _line = 0; }
                if (_bookPart != i || _parts[i] == null) continue;
                var chs = _parts[i].Chapters;
                for (int k = 0; k < chs.Count; k++)
                    if (GUILayout.Button(chs[k].Title, k == _bookChapter ? _tocSel : _toc) && k != _bookChapter) { _bookChapter = k; _bookScroll = Vector2.zero; _line = 0; }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(18);
            // the right page: the preface, or the chapter
            GUILayout.BeginVertical();
            BookChapter ch = null;
            if (_bookPart == TimelinePart) TimelinePage(w, r);
            else if (_bookPart < 0)
            {
                GUILayout.Label("Preface", _inkTitle);
                GUILayout.Label("the world in year " + _writer.Book.Year, _inkSub);
                _bookScroll = GUILayout.BeginScrollView(_bookScroll);
                foreach (var para in _writer.Book.Preface) GUILayout.Label(para, _ink);
                GUILayout.EndScrollView();
            }
            else
            {
                var part = _parts[_bookPart];
                if (_bookChapter >= part.Chapters.Count) _bookChapter = part.Chapters.Count - 1;
                if (_bookChapter < 0) { GUILayout.Label(HistoryBook.PartTitles[_bookPart], _inkTitle); GUILayout.Label(part.Intro ?? "", _ink); }
                else
                {
                    ch = part.Chapters[_bookChapter];
                    GUILayout.Label(ch.Title, _inkTitle);
                    if (!string.IsNullOrEmpty(ch.Sub)) GUILayout.Label(ch.Sub, _inkSub);
                    _bookScroll = GUILayout.BeginScrollView(_bookScroll, GUILayout.Height(r.height * (_line != 0 ? 0.52f : 0.80f)));
                    if (_bookChapter == 0 && !string.IsNullOrEmpty(part.Intro)) GUILayout.Label("<i>" + part.Intro + "</i>", _inkFact);
                    foreach (var b in ch.Blocks)
                    {
                        switch (b.Kind)
                        {
                            case BookKind.Heading: GUILayout.Label(b.Text, _inkHead); break;
                            case BookKind.Fact: GUILayout.Label(b.Text, _inkFact); break;
                            case BookKind.Line: if (GUILayout.Button(b.Text, _line == b.Event ? _inkLineSel : _inkLine)) { _line = _line == b.Event ? 0 : b.Event; _whyScroll = Vector2.zero; } break;
                            default: GUILayout.Label(b.Text, _ink); break;
                        }
                    }
                    GUILayout.EndScrollView();
                }
            }
            // a chosen line's causes, back to their root
            if (_line != 0 && w.History.TryGet(_line, out var chosen))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Why?</b>  <size=11>each cause is a record of its own; its root is " + chosen.RootKind + "</size>", _ink);
                if (Place(w, chosen, out double lat, out double lon) && GUILayout.Button("Go there", _inkBtn, GUILayout.Width(80))) { Rig.Lat = lat; Rig.Lon = lon; }
                GUILayout.EndHorizontal();
                _whyScroll = GUILayout.BeginScrollView(_whyScroll);
                int n = w.History.CausalChain(_line, _chain);
                for (int i = 0; i < n; i++)
                {
                    if (!w.History.TryGet(_chain[i], out var e)) { GUILayout.Label("<- (a record forgotten)", _inkFact); break; }
                    string row = (i == 0 ? "" : "<- ") + HistoryText.Dated(w, e);
                    if (GUILayout.Button(row, _chain[i] == _line ? _inkLineSel : _inkLine) && _chain[i] != _line) { _line = _chain[i]; break; }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.FlexibleSpace();
            // turning the pages
            GUILayout.BeginHorizontal();
            if (_bookPart >= 0 && GUILayout.Button("< previous", _inkBtn, GUILayout.Width(90))) Turn(-1);
            GUILayout.FlexibleSpace();
            if (ch != null && ch.Settlement > 0 && w.Settlements.Exists(ch.Settlement) && GUILayout.Button("Go to " + w.History.NameOf(EntityRef.Settlement(ch.Settlement)), _inkBtn))
            { var a = w.Settlements.Get(ch.Settlement).Anchor; Rig.Lat = a.Latitude; Rig.Lon = a.Longitude; }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("next >", _inkBtn, GUILayout.Width(90))) Turn(1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

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
            _bookScroll = GUILayout.BeginScrollView(_bookScroll, GUILayout.Height(r.height * 0.80f));
            float cw = Mathf.Max(200f, r.width - Mathf.Min(300f, r.width * 0.32f) - 90f);
            float ch = 250f;
            Rect ages = GUILayoutUtility.GetRect(cw, 20f);
            Rect chart = GUILayoutUtility.GetRect(cw, ch);
            Rect axis = GUILayoutUtility.GetRect(cw, 16f);
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
                    GUI.Label(new Rect(x - 20f, axis.y, 40f, axis.height), y.ToString(CultureInfo.InvariantCulture), _inkFact);
                }
            }
            // the realms' inks, the people's line
            GUILayout.Space(6);
            GUILayout.Label("Each band is a realm: the villages of its lord and of every tier under it, year by year; the whole height is the world's places. The dark line is the world's people (at most " +
                            tl.MaxPeople.ToString("N0", CultureInfo.InvariantCulture) + ").", _ink);
            for (int i = 0; i < tl.Realms.Count; i++)
            {
                var rr = tl.Realms[i];
                Rect line = GUILayoutUtility.GetRect(cw, 18f);
                if (Event.current.type == EventType.Repaint) { GUI.color = RealmInk[i % RealmInk.Length]; GUI.DrawTexture(new Rect(line.x, line.y + 4f, 14f, 10f), _dot); GUI.color = Color.white; }
                GUI.Label(new Rect(line.x + 20f, line.y, line.width - 20f, line.height), rr.Name + " - at its height " + rr.Peak + " villages, year " + rr.PeakYear, _ink);
            }
            if (tl.Marks.Count > 0)
            {
                GUILayout.Label("Its greatest moments", _inkHead);
                foreach (var m in tl.Marks) GUILayout.Label("Year " + m.Year + ": " + m.Text + ".", _ink);
            }
            GUILayout.EndScrollView();
        }

        /// <summary>The timeline's chart in pixels: the realms stacked in their inks over the world's places, the people's line, on clear paper.</summary>
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
                    // the people: a dark line two pixels thick
                    if (tl.MaxPeople > 0 && tl.People[k] > 0)
                    {
                        int py = Mathf.Clamp(Mathf.RoundToInt(tl.People[k] / (float)tl.MaxPeople * (TH - 4)), 0, TH - 2);
                        px[py * TW + x] = new Color32(40, 28, 18, 235); px[(py + 1) * TW + x] = new Color32(40, 28, 18, 235);
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
