using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (F11.1; session 31: rebuilt as the game's opening) The title over the living planet: the camera holds the world turning slowly at the right of the screen under a
    /// composed light (CameraRig.Title), a shade at the left, the logotype and the line that says what this is, and a column of words - Continue or Begin, a new world,
    /// worlds kept, how to play, settings, credits - in the book's display type, lit under the pointer. The world is held still until the player goes in; nothing here
    /// writes it (the speed is the host's, a new world the host's own). A plain address opens here; the tools' and tests' parameters skip it, `?menu=1` asks for it.
    /// </summary>
    public sealed partial class Hud
    {
        bool _titleAsked, _titleOn;
        int _titleSpeed = 3;
        float _titleBorn, _titleLeft = -1f;
        GUIStyle _titleTag, _titleWorld, _menuWord, _menuWordOn, _menuSub, _pageTitle2, _mnText, _mnTextDim, _pageCap, _busyWord;
        int _titleHover = -1;
        readonly float[] _titleLit = new float[8];
        bool _newWorldBusy; int _newWorldFrames; ulong _newWorldSeed;

        static readonly string[] Skips = { "speed", "fresh", "seed", "absence", "view", "book", "test", "menu" };

        /// <summary>Whether to open on the title: a plain address (or `?menu=1`), not the tools' shots.</summary>
        static bool WantsTitle()
        {
            if (Browser.Param("menu") == "1") return true;
            if (ShotRunner.Arg("-tc2shots") != null) return false;
            for (int i = 0; i < Skips.Length; i++) if (Browser.Param(Skips[i]) != null) return false;
            return true;
        }

        /// <summary>Back to the title from the game's menu.</summary>
        void TitleEnter()
        {
            _titleOn = true; _titleBorn = Time.unscaledTime; _titleLeft = -1f; _menuOn = false; _menuPage = MenuMain;
            _titleSpeed = Host.SpeedIndex == 0 ? (_resumeSpeed > 0 ? _resumeSpeed : 3) : Host.SpeedIndex;
            Host.SpeedIndex = 0; Host.MaxSpeed = false;
            if (Rig != null) Rig.TitleBegin();
        }

        /// <summary>Into the world: the speed the player had, the camera freed from the title's composition; the guide for one who has not seen it.</summary>
        void TitleLeave()
        {
            _titleOn = false; _menuPage = MenuMain; _titleLeft = Time.unscaledTime;
            Host.SpeedIndex = _titleSpeed;
            AudioView.Ui("enter");
            if (Rig != null) Rig.TitleEnd();
            if (!Settings.GuideSeen && Browser.Param("menu") != "1") GuideStart();
        }

        void TitleStyles()
        {
            if (_menuWord != null) return;
            GUIStyle L(Font f, int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft, bool wrap = true)
            {
                var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, fontStyle = fs, alignment = a, wordWrap = wrap, richText = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                st.normal.textColor = c;
                return st;
            }
            _titleTag = L(_serif, 21, new Color(0.95f, 0.92f, 0.84f), FontStyle.Italic);
            _titleWorld = L(_serif, 15, new Color(0.82f, 0.80f, 0.74f), FontStyle.Italic);
            _menuWord = L(_display, 31, new Color(0.93f, 0.90f, 0.82f), a: TextAnchor.MiddleLeft, wrap: false);
            _menuWordOn = L(_display, 31, new Color(1f, 0.87f, 0.58f), a: TextAnchor.MiddleLeft, wrap: false);
            _menuSub = L(_serif, 15, new Color(0.80f, 0.77f, 0.70f), FontStyle.Italic, a: TextAnchor.MiddleLeft, wrap: false);
            _pageTitle2 = L(_display, 30, new Color(1f, 0.87f, 0.58f));
            _mnText = L(_serif, 17, new Color(0.94f, 0.91f, 0.84f));
            _mnTextDim = L(_serif, 15, new Color(0.78f, 0.75f, 0.68f), FontStyle.Italic);
            _pageCap = L(_display, 18, new Color(0.97f, 0.82f, 0.50f));
            _busyWord = new GUIStyle(_menuWordOn) { alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>A word of a menu: lit, with a lozenge and a small step to the right, under the pointer; a line under it if given. True when clicked.</summary>
        bool MenuWord(Rect r, int id, string word, string sub = null)
        {
            var e = Event.current;
            bool over = r.Contains(e.mousePosition) && GUI.enabled;
            if (e.type == EventType.Repaint)
            {
                if (id >= 0 && id < _titleLit.Length) _titleLit[id] = Mathf.MoveTowards(_titleLit[id], over ? 1f : 0f, Time.unscaledDeltaTime * 7f);
                float lit = id >= 0 && id < _titleLit.Length ? _titleLit[id] : (over ? 1f : 0f);
                float dx = 18f * lit;
                if (lit > 0.01f)
                {
                    var old = GUI.color;
                    GUI.color = new Color(1f, 0.82f, 0.45f, lit * old.a * 0.9f);
                    float cy = r.y + (sub != null ? 19f : r.height * 0.5f);
                    GUI.DrawTexture(new Rect(r.x - 2f, cy - 7f, 14f, 14f), Ui("thumb"));
                    GUI.color = old;
                }
                var st = over ? _menuWordOn : _menuWord;
                Edged(new Rect(r.x + dx, r.y, r.width - dx, sub != null ? 38f : r.height), word, st);
                if (sub != null) Edged(new Rect(r.x + dx + 1f, r.y + 36f, r.width - dx, 20f), sub, _menuSub);
            }
            if (id != _titleHover && over && id >= 0) { _titleHover = id; AudioView.Ui("tick"); }
            else if (_titleHover == id && !over) _titleHover = -1;
            bool click = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (click) AudioView.Ui("click");
            return click;
        }

        /// <summary>The title (the HUD's OnGUI, after the catch-up screen): true while it covers the world.</summary>
        bool Title(float sw, float sh)
        {
            if (!_titleAsked)
            {
                _titleAsked = true;
                if (WantsTitle()) { _titleSpeed = Host.SpeedIndex == 0 ? 3 : Host.SpeedIndex; _titleOn = true; _titleBorn = Time.unscaledTime; Host.SpeedIndex = 0; if (Rig != null) Rig.TitleBegin(); }
            }
            if (!_titleOn)
            {
                // (the shade lifting as the world comes in)
                if (_titleLeft >= 0f)
                {
                    float k = (Time.unscaledTime - _titleLeft) / 1.1f;
                    if (k >= 1f) _titleLeft = -1f;
                    else if (Event.current.type == EventType.Repaint) { var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, (1f - k) * (1f - k)); GUI.DrawTexture(new Rect(0, 0, sw * 0.8f, sh), Ui("shade_left"), ScaleMode.StretchToFill, true); GUI.color = o; }
                }
                return false;
            }
            TitleStyles();
            var w = Host.World;
            float t = Time.unscaledTime - _titleBorn;
            float a = Mathf.Clamp01((t - 0.15f) / 1.3f);
            // the new world asked for: the words stand for a frame or two before the generator holds the frame
            if (_newWorldBusy && ++_newWorldFrames > 6 && Event.current.type == EventType.Repaint)
            {
                _newWorldBusy = false;
                Host.NewWorld(_newWorldSeed);
                Host.Continued = false;
                _seedText = null;
                TitleLeave();
                return true;
            }
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(0, 0, sw * 0.8f, sh), Ui("shade_left"), ScaleMode.StretchToFill, true);
                var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.55f); GUI.DrawTexture(new Rect(0, sh - 120f, sw, 120f), Ui("shade_foot"), ScaleMode.StretchToFill, true); GUI.color = o;
            }
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            // the logotype, the rule, the line that says what this is
            float colX = Mathf.Max(36f, sw * 0.06f), colW = Mathf.Min(620f, Mathf.Max(380f, sw * 0.44f));
            var logo = Ui("logo");
            float lh = colW * logo.height / Mathf.Max(1f, logo.width);
            float y = Mathf.Max(24f, sh * 0.095f);
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(colX, y, colW, lh), logo, ScaleMode.ScaleToFit, true);
            y += lh + 2f;
            Ornament(new Rect(colX, y, colW * 0.82f, 26f));
            y += 34f;
            Edged(new Rect(colX + 2f, y, colW, 58f), "A tiny living world that lives and ages without you.\nYou may watch it, read its history - and reach in.", _titleTag);
            y += 74f;
            // the words
            float itemW = Mathf.Min(colW, 420f), itemH = 44f;
            string worldLine = w != null ? (Host.Continued ? "Your world, " : "A new world, ") + YearAndSeason(w.Now) + ": " + w.LivingPeople().ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " people in " + w.InhabitedSettlements() + " places" : null;
            bool busy = _newWorldBusy || w == null;
            GUI.enabled = !busy;
            _titleBeginRect = new Rect(colX, y, itemW, 58f);
            if (MenuWord(_titleBeginRect, 0, Host.Continued ? "Continue" : "Begin", worldLine)) TitleLeave();
            y += 64f;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 1, "A New World")) { _menuPage = _menuPage == MenuNew ? MenuMain : MenuNew; if (_seedText == null) _seedText = NextSeed(); _copyNote = null; }
            y += itemH;
            bool anyKept = Settings.AnyKept();
            if (anyKept) { if (MenuWord(new Rect(colX, y, itemW, itemH), 2, "Worlds Kept")) _menuPage = _menuPage == MenuWorlds ? MenuMain : MenuWorlds; y += itemH; }
            if (MenuWord(new Rect(colX, y, itemW, itemH), 3, "How to Play")) _menuPage = _menuPage == MenuHow ? MenuMain : MenuHow;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 4, "Settings")) _menuPage = _menuPage == MenuSettings ? MenuMain : MenuSettings;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 5, "Credits")) _menuPage = _menuPage == MenuCredits ? MenuMain : MenuCredits;
            GUI.enabled = true;
            // the page beside the words, over the planet's side of the screen
            if (_menuPage != MenuMain)
            {
                float pw = Mathf.Min(620f, sw - (colX + itemW + 34f) - 24f);
                float ph = Mathf.Min(sh - 72f, _menuPage == MenuSettings ? 620f : _menuPage == MenuHow ? 640f : _menuPage == MenuCredits ? 560f : 420f);
                if (pw < 360f) pw = Mathf.Min(sw - 40f, 620f);
                MenuPageDraw(new Rect(sw - pw - 28f, Mathf.Max(24f, (sh - ph) * 0.5f), pw, ph), w, true);
            }
            if (_newWorldBusy)
            {
                var o = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, sw, sh), _dot); GUI.color = o;
                Edged(new Rect(0, sh * 0.5f - 24f, sw, 48f), "A world is being made...", _busyWord);
            }
            GUI.color = old;
            PointerOverUi = true;
            return true;
        }

        /// <summary>The moment before there is a world: the logotype on the dark, and what the game is doing.</summary>
        void BootScreen(float sw, float sh)
        {
            TitleStyles();
            var logo = Ui("logo");
            float lw = Mathf.Min(620f, sw * 0.6f), lh = lw * logo.height / Mathf.Max(1f, logo.width);
            float y = sh * 0.5f - lh;
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect((sw - lw) * 0.5f, y, lw, lh), logo, ScaleMode.ScaleToFit, true);
            Ornament(new Rect((sw - lw * 0.8f) * 0.5f, y + lh + 2f, lw * 0.8f, 26f));
            if (_bootLine == null) _bootLine = new GUIStyle(_titleWorld) { alignment = TextAnchor.UpperCenter };
            GUI.Label(new Rect(0f, y + lh + 34f, sw, 26f), Host.StatusLine, _bootLine);
        }
        GUIStyle _bootLine;
        Rect _titleBeginRect;

        static string YearAndSeason(long now)
        {
            string[] p = SimTime.Format(now).Split('·');
            return p.Length >= 2 ? p[0].Trim() + ", " + p[1].Trim().ToLowerInvariant() : "";
        }
    }
}
