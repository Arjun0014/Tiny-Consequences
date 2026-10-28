using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 29: the long skip as a timelapse) The HUD's part of it: the Skip menu on the bar (a season, a year, ten years, a century - lived as an
    /// absence's catch-up is, then the player's speed again); while the camera watches from orbit, the years running in large type with the span's
    /// progress, and a few great records coming up one at a time as they happen (Core/History/Headlines - a century reads as a dozen lines); when it
    /// ends, a leaf of what the years did - the world's counts, the realms risen and fallen, the headlines to follow to their causes - and what became
    /// of what the player was watching: the person followed or chosen if they died (their completed life, and the kin to follow on), the place whose page
    /// was open if it was abandoned. Reads the world; writes nothing in it (a skip is the host's, a logged plan of resolutions).
    /// </summary>
    public sealed partial class Hud
    {
        Headlines _heads;
        readonly List<Headlines.Item> _headNew = new List<Headlines.Item>();
        readonly List<Headlines.Item> _headQueue = new List<Headlines.Item>();
        readonly List<float> _headQueuedAt = new List<float>();
        readonly List<Headlines.Item> _headShown = new List<Headlines.Item>();
        readonly List<float> _headShownAt = new List<float>();
        readonly List<Headlines.Item> _headAll = new List<Headlines.Item>();   // (every headline of the span, for its summary)
        float _headNext;
        bool _lapseHooked, _skipMenu;
        // what the years did, shown when the camera is back (null: nothing to show)
        List<string> _lapseSummary; string _lapseTitle = ""; float _lapseSummaryAt; Rect _summaryRect; Vector2 _summaryScroll;
        // what became of what the player watched
        string _lostNote; int _lostPid, _lostKin, _lostSid;
        int _watchPid, _watchSid;
        GUIStyle _lpDate, _lpSub, _lpKick, _lpText, _lpCard;

        static readonly long[] SkipSeconds = { 12 * SimTime.Day, SimTime.Year, 10 * SimTime.Year, 100 * SimTime.Year };
        static readonly string[] SkipNames = { "a season", "a year", "ten years", "a century" };

        /// <summary>Follows a person as their card's Follow does (the bridge's way in: tests).</summary>
        public void Follow(int pid) { _book = false; _selected = pid; _follow = pid; _building = 0; }

        /// <summary>Skips ahead as the bar's menu does (the tests' and the bridge's way in): years of the world.</summary>
        public void Skip(double years) { if (years > 0) Host.BeginSkip((long)(years * SimTime.Year)); }

        void LapseHook()
        {
            if (_lapseHooked || Rig == null) return;
            _lapseHooked = true;
            Rig.LapseBegan += OnLapseBegan;
            Rig.LapseEnded += OnLapseEnded;
            Rig.FollowLocator = LocateFollowed;
        }

        /// <summary>Where the person followed is drawn now (the camera comes back to them after a lapse); false when nobody is followed or they died.</summary>
        bool LocateFollowed(out double lat, out double lon)
        {
            lat = lon = 0;
            var w = Host.World;
            if (_follow == 0 || w == null || !w.People.Exists(_follow) || !w.People.Get(_follow).Alive) return false;
            Presence.Of(w, in w.People.Get(_follow), Host.RenderTime, out var d, out _, out _, out _);
            lat = d.Latitude; lon = d.Longitude;
            return true;
        }

        void OnLapseBegan()
        {
            var w = Host.World;
            if (w == null) return;
            _heads = new Headlines(w);
            _headNew.Clear(); _headQueue.Clear(); _headQueuedAt.Clear(); _headShown.Clear(); _headShownAt.Clear(); _headAll.Clear();
            _headNext = Time.unscaledTime + 1.2f;
            _lapseSummary = null; _lostNote = null; _skipMenu = false;
            // (what the player watched: the person followed, else the one chosen; the place whose page is open)
            _watchPid = _follow != 0 ? _follow : _selected;
            _watchSid = _placeSid;
            if (_watchPid != 0 && (!w.People.Exists(_watchPid) || !w.People.Get(_watchPid).Alive)) _watchPid = 0;
            if (_watchSid != 0 && (!w.Settlements.Exists(_watchSid) || !w.Settlements.Get(_watchSid).Inhabited)) _watchSid = 0;
        }

        void OnLapseEnded(bool cameBack)
        {
            var w = Host.World;
            if (w == null || _heads == null) return;
            PollHeadlines(w);
            long span = w.Now - _heads.FromTime;
            // (a short run at speed - days - needs no leaf; a season or more does, and any span with a headline)
            if (span >= 12 * SimTime.Day || _headAll.Count > 0)
            {
                _lapseSummary = _heads.Summary(w);
                _lapseTitle = SimTime.YearOf(_heads.FromTime) == SimTime.YearOf(w.Now) ? "Year " + SimTime.YearOf(w.Now) : "Year " + SimTime.YearOf(_heads.FromTime) + " to year " + SimTime.YearOf(w.Now);
                _lapseSummaryAt = Time.unscaledTime; _summaryScroll = Vector2.zero;
            }
            // what became of what the player watched
            _lostNote = null; _lostPid = 0; _lostKin = 0; _lostSid = 0;
            if (_watchPid != 0 && w.People.Exists(_watchPid) && !w.People.Get(_watchPid).Alive)
            {
                ref var p = ref w.People.Get(_watchPid);
                string how = p.DeathEvent != 0 && w.History.TryGet(p.DeathEvent, out var de) ? HistoryText.Describe(w, de) : "";
                _lostPid = _watchPid;
                _lostKin = Heir(w, _watchPid);
                _lostNote = w.History.NameOf(EntityRef.Person(_watchPid)) + (_follow == _watchPid ? ", whom you followed," : ", whom you were watching,") + " died in year " + SimTime.YearOf(p.Died) +
                            ", at " + ((int)p.AgeYears(p.Died)).ToString(CultureInfo.InvariantCulture) + (how.Length > 0 ? ": " + how : ".");
                if (_follow == _watchPid) _follow = 0;
            }
            else if (_watchSid != 0 && w.Settlements.Exists(_watchSid) && !w.Settlements.Get(_watchSid).Inhabited)
            {
                ref var s = ref w.Settlements.Get(_watchSid);
                _lostSid = _watchSid;
                _lostNote = w.History.NameOf(EntityRef.Settlement(_watchSid)) + ", whose page you had open, was abandoned" + (s.Abandoned != 0 ? " in year " + SimTime.YearOf(s.Abandoned) : "") + ".";
                if (_lapseSummary == null) { _lapseSummary = new List<string>(); _lapseTitle = "Year " + SimTime.YearOf(w.Now); _lapseSummaryAt = Time.unscaledTime; }
            }
            if (_lostNote != null && _lapseSummary == null) { _lapseSummary = new List<string>(); _lapseTitle = "Year " + SimTime.YearOf(w.Now); _lapseSummaryAt = Time.unscaledTime; }
            _heads = null;
        }

        /// <summary>The one to follow on after a death: their eldest living child, else their partner, else the eldest of their household still living.</summary>
        static int Heir(TC2.Core.Simulation.World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            int best = 0; double bestAge = -1;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var q = ref w.People.Get(id);
                if (!q.Alive || (q.Mother != pid && q.Father != pid)) continue;
                double a = q.AgeYears(w.Now);
                if (a > bestAge) { bestAge = a; best = id; }
            }
            if (best != 0) return best;
            if (p.Partner != 0 && w.People.Exists(p.Partner) && w.People.Get(p.Partner).Alive) return p.Partner;
            if (p.Household != 0 && w.Households.Exists(p.Household))
            {
                var mem = w.Members(p.Household);
                for (int i = 0; i < mem.Count; i++) { ref var q = ref w.People.Get(mem[i]); if (q.Alive && q.AgeYears(w.Now) > bestAge) { bestAge = q.AgeYears(w.Now); best = mem[i]; } }
            }
            return best;
        }

        /// <summary>The span's new records read for headlines; the queue fed; one shown at a time, the greatest waiting first, every 1.6 s at most.</summary>
        void PollHeadlines(TC2.Core.Simulation.World w)
        {
            if (_heads == null) return;
            _headNew.Clear();
            _heads.Poll(w, _headNew);
            float now = Time.unscaledTime;
            foreach (var it in _headNew) { _headQueue.Add(it); _headQueuedAt.Add(now); _headAll.Add(it); }
            // (a notable record waits a while, then gives way: the years have moved on; the great and the player's hand wait their turn)
            for (int i = _headQueue.Count - 1; i >= 0; i--)
                if (_headQueue[i].Rank <= 1 && now - _headQueuedAt[i] > 9f) { _headQueue.RemoveAt(i); _headQueuedAt.RemoveAt(i); }
            while (_headQueue.Count > 24) { _headQueue.RemoveAt(0); _headQueuedAt.RemoveAt(0); }
            if (now >= _headNext && _headQueue.Count > 0)
            {
                int pick = 0;
                for (int i = 1; i < _headQueue.Count; i++) if (_headQueue[i].Rank > _headQueue[pick].Rank) pick = i;
                _headShown.Add(_headQueue[pick]); _headShownAt.Add(now);
                _headQueue.RemoveAt(pick); _headQueuedAt.RemoveAt(pick);
                _headNext = now + 1.6f;
            }
            for (int i = _headShown.Count - 1; i >= 0; i--) if (now - _headShownAt[i] > 9f) { _headShown.RemoveAt(i); _headShownAt.RemoveAt(i); }
            while (_headShown.Count > 4) { _headShown.RemoveAt(0); _headShownAt.RemoveAt(0); }
        }

        void LapseStyles()
        {
            BookStyles();
            if (_lpDate != null) return;
            GUIStyle S(Font f, int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
            {
                var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                st.normal.textColor = c;
                return st;
            }
            _lpDate = S(_display, 46, new Color(0.99f, 0.90f, 0.66f), a: TextAnchor.UpperCenter);
            _lpSub = S(_serif, 17, new Color(0.90f, 0.88f, 0.82f), FontStyle.Italic, TextAnchor.UpperCenter);
            _lpKick = S(_display, 14, new Color(0.96f, 0.76f, 0.40f));
            _lpText = S(_serif, 16, new Color(0.95f, 0.94f, 0.90f));
            _lpCard = new GUIStyle(_box) { padding = new RectOffset(14, 14, 9, 10) };
        }

        /// <summary>The overlay while the camera watches from orbit: the years in large type, the span's bar for a catch-up or a skip, the headlines.</summary>
        void LapseOverlay(TC2.Core.Simulation.World w, float sw, float sh, float topY)
        {
            LapseStyles();
            bool span = Host.CatchingUp;
            float cw = Mathf.Min(620f, sw - 40f), cx = sw * 0.5f - cw * 0.5f;
            Rect top = new Rect(cx, topY, cw, span ? 132f : 92f);
            // (no box behind the date: the planet is the picture - the letters carry their own dark edge)
            Edged(new Rect(top.x, top.y, top.width, 56f), "Year " + SimTime.YearOf(w.Now), _lpDate);
            string season = SimTime.FormatDate(w.Now);
            int comma = season.IndexOf(',');
            season = comma > 0 ? season.Substring(comma + 1).Trim() : "";
            string pace = Host.AchievedSpeed > SimTime.Day ? FormatSpeed(Host.AchievedSpeed) : "";
            string what = span ? (Host.Skipping ? "Skipping ahead" : "While you were away") : "The days run too fast to watch from the ground";
            Edged(new Rect(top.x, top.y + 56f, top.width, 24f), what + (season.Length > 0 ? "  ·  " + season : "") + (pace.Length > 0 ? "  ·  " + pace : ""), _lpSub);
            Rect stopRect = Rect.zero;
            if (span)
            {
                float done = Mathf.Clamp01((float)(w.Now - Host.CatchUpFrom) / Mathf.Max(1f, (float)(Host.CatchUpTarget - Host.CatchUpFrom)));
                var bar = new Rect(top.x + 60f, top.y + 90f, top.width - 120f, 5f);
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), _dot);
                    GUI.color = new Color(0.98f, 0.82f, 0.45f, 0.95f); GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * done, bar.height), _dot);
                    GUI.color = Color.white;
                }
                Edged(new Rect(bar.x - 60f, bar.y - 8f, 56f, 20f), "" + SimTime.YearOf(Host.CatchUpFrom), _mapSub);
                Edged(new Rect(bar.xMax + 6f, bar.y - 8f, 56f, 20f), "" + SimTime.YearOf(Host.CatchUpTarget), _mapSub);
                Edged(new Rect(top.x, bar.y + 10f, top.width, 20f), w.LivingPeople().ToString("N0", CultureInfo.InvariantCulture) + " people in " + w.InhabitedSettlements() + " places", _mapSub);
                if (Host.Skipping)
                {
                    stopRect = new Rect(sw * 0.5f - 60f, bar.y + 32f, 120f, 26f);
                    if (GUI.Button(stopRect, "Stop here", _btn)) Host.StopSkip();
                }
            }
            // the headlines: one at a time, the newest at the foot, each fading after a while
            float y = sh - 70f, now = Time.unscaledTime, hw = Mathf.Min(520f, sw - 40f);
            Rect heads = Rect.zero;
            for (int i = _headShown.Count - 1; i >= 0; i--)
            {
                var it = _headShown[i];
                float age = now - _headShownAt[i];
                float a = Mathf.Clamp01(age / 0.5f) * Mathf.Clamp01((9f - age) / 1.6f);
                string text = it.Text;
                float th = _lpText.CalcHeight(new GUIContent(text), hw - 28f);
                var r = new Rect(20f, y - th - 30f, hw, th + 30f);
                y = r.y - 8f;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Box(r, GUIContent.none, _lpCard);
                string kick = it.Kicker.ToUpperInvariant() + "  ·  YEAR " + SimTime.YearOf(it.Time);
                GUI.Label(new Rect(r.x + 14f, r.y + 7f, r.width - 28f, 18f), it.Rank >= 3 ? "<color=#ffb070>" + kick + "</color>" : kick, _lpKick);
                GUI.Label(new Rect(r.x + 14f, r.y + 25f, r.width - 28f, th + 2f), text, _lpText);
                GUI.color = Color.white;
                heads = heads.width == 0f ? r : Rect.MinMaxRect(Mathf.Min(heads.xMin, r.xMin), Mathf.Min(heads.yMin, r.yMin), Mathf.Max(heads.xMax, r.xMax), Mathf.Max(heads.yMax, r.yMax));
            }
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / _scale;
            PointerOverUi = stopRect.Contains(m) || heads.Contains(m);
        }

        /// <summary>The leaf of what the years did, at the left when the camera is back: kept until closed (or a minute).</summary>
        void LapseSummaryLeaf(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (_lapseSummary == null) { _summaryRect = Rect.zero; return; }
            if (Time.unscaledTime - _lapseSummaryAt > 60f) { _lapseSummary = null; _lostNote = null; _summaryRect = Rect.zero; return; }
            BookStyles();
            float lw = Mathf.Min(470f, sw - 40f);
            float lh = Mathf.Min(sh - 200f, 120f + 30f * _lapseSummary.Count + (_lostNote != null ? 110f : 0f) + 26f * Mathf.Min(6, _headAll.Count));
            var r = new Rect(16f, Mathf.Max(62f, _left.yMax + 10f), lw, lh);
            _summaryRect = r;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(r.x - 5f, r.y - 4f, r.width + 10f, r.height + 8f), _leather);
                GUI.DrawTexture(r, _leaf);
            }
            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 12f, r.width - 34f, r.height - 18f));
            GUILayout.BeginHorizontal();
            Kicker("The years passed");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("close  ×", _bkNav, GUILayout.Width(84))) { _lapseSummary = null; _lostNote = null; }
            GUILayout.EndHorizontal();
            if (_lapseSummary == null) { GUILayout.EndArea(); return; }
            GUILayout.Label(_lapseTitle, _pgTitle);
            Rule(0f, 1f);
            _summaryScroll = GUILayout.BeginScrollView(_summaryScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (_lostNote != null)
            {
                GUILayout.Label(_lostNote, _pgBody);
                GUILayout.BeginHorizontal();
                if (_lostPid != 0 && GUILayout.Button("their life", _bkNav)) { ShowPerson(_lostPid, 5); }
                if (_lostKin != 0 && w.People.Exists(_lostKin) && GUILayout.Button("follow " + Given(w, _lostKin) + Kinship(w, _lostPid, _lostKin), _bkNav)) { _selected = _lostKin; _follow = _lostKin; _personPage = false; _lapseSummary = null; _lostNote = null; }
                if (_lostSid != 0 && GUILayout.Button("its page", _bkNav)) ShowPlace(_lostSid, 5);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                Rule(0f, 0.5f);
            }
            if (_lapseSummary != null)
            {
                foreach (string line in _lapseSummary)
                {
                    int colon = line.IndexOf(": ", System.StringComparison.Ordinal);
                    string t = colon > 0 && colon < 14 ? "<color=#8c2614>" + line.Substring(0, colon) + "</color>  " + line.Substring(colon + 2) : line;
                    GUILayout.Label(t, _pgLine);
                }
                if (_headAll.Count > 0)
                {
                    GUILayout.Label("Of note", _pgHead);
                    // (the greatest of the span first, then by time; each opens its causes in the book)
                    var top = new List<Headlines.Item>(_headAll);
                    top.Sort((a, b) => { int c = b.Rank.CompareTo(a.Rank); return c != 0 ? c : a.Time.CompareTo(b.Time); });
                    for (int i = 0; i < top.Count && i < 6; i++)
                        if (GUILayout.Button("<color=#8c2614>" + SimTime.YearOf(top[i].Time) + "</color>  " + top[i].Text, _bkWhy)) { _book = true; _line = top[i].Event; _whyScroll = Vector2.zero; }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("read these years in the book", _bkNav)) { OpenBook(-1); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string Given(TC2.Core.Simulation.World w, int pid)
        {
            string full = w.History.NameOf(EntityRef.Person(pid));
            int sp = full.IndexOf(' ');
            return sp > 0 ? full.Substring(0, sp) : full;
        }

        /// <summary>" (their son)", " (their partner)" ... - how the one followed on was kin to the one who died.</summary>
        static string Kinship(TC2.Core.Simulation.World w, int dead, int kin)
        {
            ref var k = ref w.People.Get(kin);
            if (k.Mother == dead || k.Father == dead) return k.IsFemale ? ", their daughter" : ", their son";
            if (w.People.Get(dead).Partner == kin) return ", their partner";
            return ", of their household";
        }

        /// <summary>The bar's Skip menu, under the bar at its right end.</summary>
        void SkipMenu(float sw)
        {
            if (!_skipMenu) return;
            var r = new Rect(_left.xMax - 170f, _left.yMax + 4f, 170f, 30f + 28f * SkipNames.Length);
            GUILayout.BeginArea(r, _box);
            GUILayout.Label("Skip ahead, watched from orbit", _small);
            for (int i = 0; i < SkipNames.Length; i++) if (GUILayout.Button(SkipNames[i], _btn)) { _skipMenu = false; Host.BeginSkip(SkipSeconds[i]); }
            GUILayout.EndArea();
            _skipRect = r;
        }
        Rect _skipRect;
    }
}
