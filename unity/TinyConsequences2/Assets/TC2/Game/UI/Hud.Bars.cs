using System.Globalization;
using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 31) The bars over the world, kept small and made of the kit: the time at the top left - the year and the season, pause, a ladder of speeds with their
    /// meaning in words, the skip ahead - and the seals at the top right: the History Book first, the realms, the world now, recent history, the sound, the menu.
    /// Each says what it is under the pointer. Writes nothing in the world: the speed is the host's.
    /// </summary>
    public sealed partial class Hud
    {
        static readonly string[] SpeedWords = { "held still", "as it is lived", "ten times as fast", "a minute a second", "ten minutes a second", "an hour a second", "a day a second", "ten days a second" };
        static readonly string[] SpeedTips = { "", "Time as it is lived", "Ten times as fast", "A minute of the world each second", "Ten minutes of the world each second", "An hour of the world each second", "A day each second: too fast to watch from the ground, so the camera rises to orbit", "Ten days each second, watched from orbit" };
        Rect _speedRect, _pauseRect, _skipBtn, _bookBtn, _realmsBtn, _infoBtn, _feedBtn, _soundBtn, _menuBtn;

        /// <summary>The time's bar, top left.</summary>
        void TimeBar(TC2.Core.Simulation.World w, float sw)
        {
            string[] parts = SimTime.Format(w.Now).Split('·');
            string year = parts.Length > 0 ? parts[0].Trim() : "", season = parts.Length > 1 ? parts[1].Trim() : "", day = parts.Length > 2 ? parts[2].Trim() : "", clock = parts.Length > 3 ? parts[3].Trim() : "";
            bool narrow = sw < 760f;
            float dateW = 164f, ladderW = 8f * 11f, wordW = narrow ? 0f : 118f, skipW = 76f, gap = 12f;
            float bw = 14f + dateW + gap + 34f + gap + ladderW + (wordW > 0f ? 8f + wordW : 0f) + gap + skipW + 14f;
            _left = new Rect(10f, 10f, bw, 58f);
            GUI.Box(_left, GUIContent.none, _box);
            float x = _left.x + 14f, y = _left.y;
            // the year and the season; a small sun or moon for the hour at the world's clock
            int h = SimTime.HourOfDay(w.Now);
            Icon(new Rect(x, y + 10f, 18f, 18f), h >= 6 && h < 19 ? "sun" : "moon", new Color(0.97f, 0.86f, 0.60f));
            GUI.Label(new Rect(x + 24f, y + 6f, dateW - 24f, 26f), year, _dateBig);
            GUI.Label(new Rect(x, y + 31f, dateW, 20f), season + "  ·  " + day.Replace("Day ", "day ") + "  ·  " + clock, _dateSub);
            x += dateW + gap;
            // pause / go on
            bool paused = !Host.MaxSpeed && Host.SpeedIndex == 0;
            _pauseRect = new Rect(x, y + 12f, 34f, 34f);
            if (!paused && Host.SpeedIndex > 0) _resumeSpeed = Host.SpeedIndex;
            if (Seal(_pauseRect, paused ? "play" : "pause", paused, false, new Color(0.97f, 0.90f, 0.72f), out bool hp)) { Host.MaxSpeed = false; Host.SpeedIndex = paused ? (_resumeSpeed > 0 ? _resumeSpeed : 3) : 0; }
            if (hp) TipAt(_pauseRect, paused ? "Let the world go on  (Space)" : "Hold the world still  (Space)", false);
            x += 34f + gap;
            // the ladder: seven speeds and as fast as it goes, each a bar taller than the last; lit up to the speed chosen
            _speedRect = new Rect(x, y + 8f, ladderW, 42f);
            int cur = Host.MaxSpeed ? 8 : Host.SpeedIndex;
            for (int i = 1; i <= 8; i++)
            {
                float bh = 9f + (i - 1) * 4.2f;
                var hit = new Rect(x + (i - 1) * 11f, y + 8f, 11f, 42f);
                var bar = new Rect(hit.x + 2f, y + 49f - bh, 7f, bh);
                bool over = hit.Contains(Event.current.mousePosition), lit = i <= cur;
                if (Event.current.type == EventType.Repaint)
                {
                    var c = lit ? (i == 8 ? new Color(1f, 0.66f, 0.32f) : new Color(0.97f, 0.82f, 0.46f)) : new Color(0.60f, 0.54f, 0.42f, over ? 0.9f : 0.42f);
                    if (over && lit) c = Color.Lerp(c, Color.white, 0.25f);
                    var old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * old.a); GUI.DrawTexture(bar, _dot); GUI.color = old;
                }
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                {
                    AudioView.Ui("click");
                    if (i == 8) Host.MaxSpeed = !Host.MaxSpeed;
                    else { Host.SpeedIndex = i; Host.MaxSpeed = false; }
                }
                if (over) TipAt(_speedRect, i == 8 ? "As fast as the world can go, watched from orbit  (9)" : SpeedTips[i] + "  (" + (i + 1) + ")", false);
            }
            x += ladderW;
            if (wordW > 0f)
            {
                x += 8f;
                string word = Host.MaxSpeed ? "as fast as it goes" : SpeedWords[Mathf.Clamp(Host.SpeedIndex, 0, SpeedWords.Length - 1)];
                _speedWord.wordWrap = true;
                GUI.Label(new Rect(x, y + 11f, wordW, 38f), word, _speedWord);
                x += wordW;
            }
            x += gap;
            // the skip ahead
            _skipBtn = new Rect(x, y + 13f, skipW, 32f);
            var skipC = new GUIContent("  Skip", Ui("i_hourglass"));
            if (_skipStyle == null) { _skipStyle = new GUIStyle(_btn) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(7, 8, 5, 5), imagePosition = ImagePosition.ImageLeft }; }
            bool sk = GUI.Toggle(_skipBtn, _skipMenu, skipC, _skipStyle);
            if (sk != _skipMenu) { _skipMenu = sk; AudioView.Ui("click"); }
            if (_skipBtn.Contains(Event.current.mousePosition) && !_skipMenu) TipAt(_skipBtn, "Leap years ahead and watch the planet age from orbit", false);
        }
        int _resumeSpeed = 3;
        GUIStyle _skipStyle;

        /// <summary>The seals at the top right: the History Book, the realms, the world now, recent history, the sound, the menu.</summary>
        void SealBar(float sw)
        {
            const float size = 42f, step = 46f, bookSize = 52f;
            float total = bookSize + 5f * step + 8f;
            float x = sw - 12f - total, y = 10f;
            _toggles = new Rect(x, y, total, bookSize);
            var cream = new Color(0.97f, 0.90f, 0.72f);
            _bookBtn = new Rect(x, y, bookSize, bookSize);
            if (Seal(_bookBtn, "book", _book, false, new Color(1f, 0.92f, 0.70f), out bool hb, 0.60f)) { _book = !_book; AudioView.Ui(_book ? "page" : "click"); }
            if (hb) TipAt(_bookBtn, "<b>The History Book</b>  (B)\nEverything that has happened, and why", false);
            x += bookSize + 8f;
            float ry = y + (bookSize - size) * 0.5f;
            _realmsBtn = new Rect(x, ry, size, size);
            if (Seal(_realmsBtn, "banner", RealmsView.On, false, cream, out bool hr)) RealmsView.On = !RealmsView.On;
            if (hr) TipAt(_realmsBtn, "<b>The realms</b>  (R)\nWho pays whom, on the land itself; wars as dark spears", false);
            x += step;
            _infoBtn = new Rect(x, ry, size, size);
            if (Seal(_infoBtn, "globe", _info, false, cream, out bool hi)) _info = !_info;
            if (hi) TipAt(_infoBtn, "<b>The world now</b>\nIts age, its greatest realms, what troubles it", false);
            x += step;
            _feedBtn = new Rect(x, ry, size, size);
            if (Seal(_feedBtn, "scroll", _history, false, cream, out bool hf)) _history = !_history;
            if (hf) TipAt(_feedBtn, "<b>Recent history</b>\nWhat has just happened; a line opens why", false);
            x += step;
            _soundBtn = new Rect(x, ry, size, size);
            if (Seal(_soundBtn, AudioView.Muted ? "soundoff" : "sound", false, AudioView.Muted, cream, out bool hs)) AudioView.Muted = !AudioView.Muted;
            if (hs) TipAt(_soundBtn, AudioView.Muted ? "The world's sound is off  (M)" : "The world's sound  (M)", false);
            x += step;
            _menuBtn = new Rect(x, ry, size, size);
            if (Seal(_menuBtn, "menu", _menuOn, false, cream, out bool hm)) { _menuOn = !_menuOn; _menuPage = MenuMain; }
            if (hm) TipAt(_menuBtn, "<b>Menu</b>  (Esc)\nSettings, how to play, save, a new world", false);
        }

        /// <summary>The news line: over the power bar, in the book's italic with no box - a line that comes and fades; a click opens its why in the book.</summary>
        void NewsLine(TC2.Core.Simulation.World w, float sw, float sh, bool lapse)
        {
            _ticker = Rect.zero;
            if (_news.Length == 0 || _newsAge >= 12f || lapse || _book) return;
            float left = Mathf.Max(8f, Mathf.Max(_handRect.xMax, _bottom.xMax) + 8f), right = (_pageRect.width > 0f ? _pageRect.x : sw) - 8f;
            float width = Mathf.Min(760f, right - left);
            if (width < 200f) return;
            if (_tickLine == null)
            {
                _tickLine = new GUIStyle(_mapSub) { font = _serif, fontSize = 15, fontStyle = FontStyle.Italic, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleCenter };
                _tickLine.normal.textColor = new Color(0.98f, 0.95f, 0.86f);
            }
            if (!ReferenceEquals(_tickFor, _news) || _tickW != width) { _tickFor = _news; _tickW = width; _tickText = OneLine(_news, _tickLine, width - 8f); }
            float cx = Mathf.Clamp(sw * 0.5f, left + width * 0.5f, right - width * 0.5f);
            float floor = _powersRect.width > 0f ? _powersRect.y - 4f : sh - 12f;
            _ticker = new Rect(cx - width * 0.5f, floor - 24f, width, 24f);
            var old = GUI.color;
            float a = Mathf.Clamp01(_newsAge / 0.5f) * Mathf.Clamp01((12f - _newsAge) / 2.5f);
            GUI.color = new Color(old.r, old.g, old.b, a);
            Edged(_ticker, _tickText, _tickLine);
            if (GUI.Button(_ticker, GUIContent.none, GUIStyle.none) && _newsId != 0 && w.History.TryGet(_newsId, out _)) { _book = true; _line = _newsId; _whyScroll = Vector2.zero; AudioView.Ui("page"); }
            GUI.color = old;
        }
    }
}
