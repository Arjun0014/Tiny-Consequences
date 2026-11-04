using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 31) The game's menu, on Esc or the menu seal: the world held still, a dimmed picture, the words as on the title (back to the world, save, worlds kept, how
    /// to play, settings, credits, the title) and the same pages the title opens - how to play, settings (sound, picture, camera, interface, the world while away, the
    /// guide), a new world, worlds kept (three copies in the browser's own store), credits. The view writes nothing in the world.
    /// </summary>
    public sealed partial class Hud
    {
        const int MenuMain = 0, MenuHow = 1, MenuSettings = 2, MenuCredits = 3, MenuNew = 4, MenuWorlds = 5;
        bool _menuOn, _menuHeld; int _menuPage, _menuSpeedWas; bool _menuMaxWas;
        string _menuNote; float _menuNoteAt; bool _menuNoteOk;
        string _seedText, _copyNote;
        Vector2 _menuScroll;
        Rect _menuRect;

        /// <summary>Tools and tests (firstrun.mjs plays by them): the state of the opening, and where its parts stand on the screen, in pixels from the top left.</summary>
        public void ToolsState(System.Text.StringBuilder sb)
        {
            string R(Rect r) => "[" + (r.x * _scale).ToString("0", CultureInfo.InvariantCulture) + "," + (r.y * _scale).ToString("0", CultureInfo.InvariantCulture) + "," + (r.width * _scale).ToString("0", CultureInfo.InvariantCulture) + "," + (r.height * _scale).ToString("0", CultureInfo.InvariantCulture) + "]";
            sb.Append(",\"title\":").Append(_titleOn ? "true" : "false").Append(",\"continued\":").Append(Host.Continued ? "true" : "false").Append(",\"guide\":").Append(_guide);
            sb.Append(",\"begin\":").Append(R(_titleBeginRect)).Append(",\"gt\":").Append(R(_guideTarget)).Append(",\"gn\":").Append(R(_guideNextRect)).Append(",\"gskip\":").Append(R(_guideSkipYearRect)).Append(",\"power0\":").Append(R(_power0Rect));
        }

        /// <summary>Tools and tests: the menu open at a page (-1 closed).</summary>
        public void ToolsMenu(int page) { _menuOn = page >= 0; _menuPage = Mathf.Max(0, page); }
        /// <summary>Tools and tests: the title over the world (true), or the way into the world from it, as its first word is.</summary>
        public void ToolsTitle(bool on, int page = 0) { if (on) { if (!_titleOn) TitleEnter(); _menuPage = page; } else if (_titleOn) TitleLeave(); }
        /// <summary>Tools and tests: the guide begun at a step (-1 ended).</summary>
        public void ToolsGuide(int step) { if (step < 0) { if (_guide >= 0) GuideEnd(true); } else { if (_guide < 0) GuideStart(); _guide = step; } }

        static string NextSeed() => (Browser.NowUtcMs() % 1000000L).ToString(CultureInfo.InvariantCulture);

        void MenuNote(string text, bool ok) { _menuNote = text; _menuNoteOk = ok; _menuNoteAt = Time.unscaledTime; }

        /// <summary>The menu's hold on the world (Update): while it is open the world waits, and goes on at the speed it had.</summary>
        void MenuHold()
        {
            if (_menuOn && !_menuHeld) { _menuHeld = true; _menuSpeedWas = Host.SpeedIndex; _menuMaxWas = Host.MaxSpeed; Host.SpeedIndex = 0; Host.MaxSpeed = false; }
            else if (!_menuOn && _menuHeld) { _menuHeld = false; if (!_titleOn) { Host.SpeedIndex = _menuSpeedWas; Host.MaxSpeed = _menuMaxWas; } }
        }

        /// <summary>The menu over the game.</summary>
        void GameMenu(TC2.Core.Simulation.World w, float sw, float sh)
        {
            TitleStyles();
            _menuRect = new Rect(0, 0, sw, sh);
            if (Event.current.type == EventType.Repaint)
            {
                var o = GUI.color; GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.62f); GUI.DrawTexture(_menuRect, _dot); GUI.color = o;
                GUI.DrawTexture(new Rect(0, 0, sw * 0.7f, sh), Ui("shade_left"), ScaleMode.StretchToFill, true);
            }
            float colX = Mathf.Max(36f, sw * 0.06f), itemW = Mathf.Min(420f, sw * 0.34f), itemH = 42f;
            float y = Mathf.Max(30f, sh * 0.5f - 230f);
            var logo = Ui("logo");
            float lw = Mathf.Min(360f, itemW);
            float lh = lw * logo.height / Mathf.Max(1f, logo.width);
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(colX, y, lw, lh), logo, ScaleMode.ScaleToFit, true);
            y += lh + 2f;
            Ornament(new Rect(colX, y, lw, 20f));
            y += 26f;
            Edged(new Rect(colX, y, itemW, 22f), YearAndSeason(w.Now) + " - the world waits", _titleWorld);
            y += 30f;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 0, "Back to the World")) { _menuOn = false; _menuPage = MenuMain; }
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 1, "Save Now"))
            {
                MenuNote("Saving...", true);
                Host.SaveNow(ok => MenuNote(ok ? "Saved. It lives in this browser, and it goes on while you are away." : "It could not be saved: the browser refused.", ok));
            }
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 2, "Worlds Kept", open: _menuPage == MenuWorlds)) _menuPage = _menuPage == MenuWorlds ? MenuMain : MenuWorlds;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 3, "How to Play", open: _menuPage == MenuHow)) _menuPage = _menuPage == MenuHow ? MenuMain : MenuHow;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 4, "Settings", open: _menuPage == MenuSettings)) _menuPage = _menuPage == MenuSettings ? MenuMain : MenuSettings;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 5, "Credits", open: _menuPage == MenuCredits)) _menuPage = _menuPage == MenuCredits ? MenuMain : MenuCredits;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 6, "To the Title")) { Host.SaveNow(); _menuHeld = false; TitleEnter(); }
            y += itemH + 8f;
            if (_menuNote != null && Time.unscaledTime - _menuNoteAt < 7f) Edged(new Rect(colX, y, itemW + 200f, 22f), _menuNote, _titleWorld);
            if (_menuPage != MenuMain)
            {
                float pw = Mathf.Min(620f, sw - (colX + itemW + 34f) - 24f);
                if (pw < 360f) pw = Mathf.Min(sw - 40f, 620f);
                float ph = Mathf.Min(sh - 56f, MenuPageHeight());
                MenuPageDraw(new Rect(sw - pw - 28f, Mathf.Max(20f, (sh - ph) * 0.5f), pw, ph), w, false);
            }
            else _menuScrollPage = -1;
            PointerOverUi = true;
        }

        float MenuPageHeight() => _menuPage == MenuSettings ? 620f : _menuPage == MenuHow ? 760f : _menuPage == MenuCredits ? 720f : 440f;
        int _menuScrollPage = -1;

        /// <summary>A page of the menu in its panel (read from its top each time it is opened).</summary>
        void MenuPageDraw(Rect r, TC2.Core.Simulation.World w, bool onTitle)
        {
            if (_menuScrollPage != _menuPage) { _menuScrollPage = _menuPage; _menuScroll = Vector2.zero; }
            GUI.Box(r, GUIContent.none, _box);
            var area = new Rect(r.x + 24f, r.y + 18f, r.width - 44f, r.height - 32f);
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            string title = _menuPage == MenuHow ? "How to Play" : _menuPage == MenuSettings ? "Settings" : _menuPage == MenuCredits ? "Credits" : _menuPage == MenuNew ? "A New World" : "Worlds Kept";
            GUILayout.Label(title, _pageTitle2);
            GUILayout.FlexibleSpace();
            Rect cr = GUILayoutUtility.GetRect(26f, 26f, GUILayout.Width(26f), GUILayout.Height(26f));
            if (Seal(cr, "close", false, false, new Color(0.97f, 0.90f, 0.72f), out _, 0.46f)) _menuPage = MenuMain;
            GUILayout.EndHorizontal();
            Rect rule = GUILayoutUtility.GetRect(10f, 14f, GUILayout.ExpandWidth(true));
            Ornament(new Rect(rule.x, rule.y - 2f, rule.width, 18f), 0.9f);
            GUILayout.Space(4);
            _menuScroll = GUILayout.BeginScrollView(_menuScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            switch (_menuPage)
            {
                case MenuHow: HowToPage(area.width - 16f); break;
                case MenuSettings: SettingsPage(area.width - 16f); break;
                case MenuCredits: CreditsPage(area.width - 16f); break;
                case MenuNew: NewWorldPage(w, onTitle); break;
                case MenuWorlds: WorldsPage(w, onTitle); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void HowRow(string icon, string head, string text, float width)
        {
            GUILayout.BeginHorizontal();
            Rect ic = GUILayoutUtility.GetRect(42f, 42f, GUILayout.Width(42f), GUILayout.Height(42f));
            if (Event.current.type == EventType.Repaint) { GUI.DrawTexture(ic, Ui("seal")); Icon(Inset(ic, 10f), icon, new Color(0.97f, 0.88f, 0.66f)); }
            GUILayout.Space(14);
            GUILayout.BeginVertical(GUILayout.Width(width - 60f));
            GUILayout.Label(head, _pageCap);
            GUILayout.Space(1);
            GUILayout.Label(text, _howText);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
        }

        /// <summary>A line of the controls: the keys in the display type at the left, what they do beside them.</summary>
        void KeyRow(string keys, string what, float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 24f, GUILayout.Height(24f));
            if (Event.current.type != EventType.Repaint) return;
            GUI.Label(new Rect(r.x + 56f, r.y, 150f, r.height), keys, _keyCap);
            GUI.Label(new Rect(r.x + 214f, r.y, r.width - 214f, r.height), what, _howText);
        }

        void HowToPage(float width)
        {
            CreditStyles();
            GUILayout.Label("You do not command this world. You watch it, read it - and now and then, reach in.", _mnTextDim);
            GUILayout.Space(12);
            HowRow("globe", "The world lives without you", "Its people farm, build, marry, trade and quarrel for their own reasons. It goes on whether you watch or not - even while you are away.", width);
            HowRow("person", "Watch and follow", "Click a person: what they are doing, and why. Their life holds their family, work and story; follow keeps the camera with them. Click a village for its page - Why? traces how it came to be.", width);
            HowRow("book", "The History Book", "B opens it. Every line really happened: click one to see what caused it.", width);
            HowRow("hourglass", "Time", "Space pauses, and 1-8 set the pace. Skip leaps years ahead, watched from orbit, then brings you back to read what changed.", width);
            HowRow("lightning", "Your powers", "Choose a power at the foot of the screen, then click the ground (right-click puts it down). Your hand, at the lower left, shows what came of it - open it to follow the consequences.", width);
            GUILayout.Label("Controls", _pageCap);
            GUILayout.Space(4);
            KeyRow("Drag", "turn the world", width);
            KeyRow("Wheel", "come down toward the pointer, or rise", width);
            KeyRow("Double-click", "fly down to the ground", width);
            KeyRow("W A S D  ·  Q E", "move  ·  come down, rise", width);
            KeyRow("Space  ·  1-8  ·  9", "pause  ·  the pace  ·  as fast as it goes", width);
            KeyRow("B  ·  R  ·  N", "the History Book  ·  realms  ·  names", width);
            KeyRow("M  ·  H  ·  Esc", "sound  ·  hide the interface  ·  the menu", width);
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Walk me through it", _btn, GUILayout.Height(34f)))
            {
                _menuOn = false; _menuPage = MenuMain;
                if (_titleOn) TitleLeave();
                GuideStart();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        static bool Choice(bool on, string word, GUIStyle st) { bool c = GUILayout.Toggle(on, word, st, GUILayout.Height(30f)); if (c && !on) AudioView.Ui("click"); return c && !on; }

        void SettingRow(string name, float width, System.Action body)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _mnText, GUILayout.Width(150f), GUILayout.Height(30f));
            GUILayout.BeginVertical(GUILayout.Width(width - 160f));
            body();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        void SettingsPage(float width)
        {
            bool muted = AudioView.Muted, lite = Sky.ShadowLite; float vol = AudioView.Volume, clouds = Settings.Clouds, detail = Settings.Detail, cam = Settings.CamSpeed, shake = Settings.Shake, ui = Settings.UiScale; bool names = Hud.Names; var offline = Host.Offline;
            GUILayout.Label("Sound", _pageCap);
            SettingRow("The world's sound", width, () =>
            {
                GUILayout.BeginHorizontal();
                if (Choice(!AudioView.Muted, "On", _btn)) AudioView.Muted = false;
                if (Choice(AudioView.Muted, "Off", _btn)) AudioView.Muted = true;
                GUILayout.EndHorizontal();
            });
            SettingRow("Loudness", width, () => { GUILayout.Space(10); AudioView.Volume = GUILayout.HorizontalSlider(AudioView.Volume, 0f, 1f); });
            GUILayout.Space(6);
            GUILayout.Label("Picture", _pageCap);
            SettingRow("Quality", width, () =>
            {
                GUILayout.BeginHorizontal();
                int q = Settings.Detail >= 0.99f && !Sky.ShadowLite ? 2 : Settings.Detail >= 0.82f ? 1 : 0;
                if (Choice(q == 0, "Light", _btn)) { Settings.Detail = 0.7f; Sky.ShadowLite = true; }
                if (Choice(q == 1, "Balanced", _btn)) { Settings.Detail = 0.85f; Sky.ShadowLite = true; }
                if (Choice(q == 2, "Beautiful", _btn)) { Settings.Detail = 1f; Sky.ShadowLite = false; }
                GUILayout.EndHorizontal();
            });
            SettingRow("Clouds", width, () =>
            {
                GUILayout.Space(10);
                Settings.Clouds = GUILayout.HorizontalSlider(Settings.Clouds, 0f, 1.5f);
                GUILayout.Label(Settings.Clouds < 0.05f ? "none" : Settings.Clouds < 0.75f ? "thin" : Settings.Clouds > 1.2f ? "thick" : "as the weather makes them", _mnTextDim);
            });
            SettingRow("Names on the map", width, () =>
            {
                GUILayout.BeginHorizontal();
                if (Choice(Hud.Names, "Shown", _btn)) Hud.Names = true;
                if (Choice(!Hud.Names, "Hidden", _btn)) Hud.Names = false;
                GUILayout.EndHorizontal();
            });
            GUILayout.Space(6);
            GUILayout.Label("Camera and interface", _pageCap);
            SettingRow("Turning speed", width, () => { GUILayout.Space(10); Settings.CamSpeed = GUILayout.HorizontalSlider(Settings.CamSpeed, 0.5f, 2f); });
            SettingRow("Shaking", width, () =>
            {
                GUILayout.BeginHorizontal();
                if (Choice(Settings.Shake >= 0.9f, "Full", _btn)) Settings.Shake = 1f;
                if (Choice(Settings.Shake > 0.05f && Settings.Shake < 0.9f, "Gentle", _btn)) Settings.Shake = 0.35f;
                if (Choice(Settings.Shake <= 0.05f, "None", _btn)) Settings.Shake = 0f;
                GUILayout.EndHorizontal();
            });
            SettingRow("Interface size", width, () =>
            {
                GUILayout.BeginHorizontal();
                if (Choice(Settings.UiScale < 1.07f, "Standard", _btn)) Settings.UiScale = 1f;
                if (Choice(Settings.UiScale >= 1.07f && Settings.UiScale < 1.22f, "Large", _btn)) Settings.UiScale = 1.15f;
                if (Choice(Settings.UiScale >= 1.22f, "Larger", _btn)) Settings.UiScale = 1.3f;
                GUILayout.EndHorizontal();
            });
            GUILayout.Space(6);
            GUILayout.Label("The world while you are away", _pageCap);
            SettingRow("A day away is", width, () =>
            {
                GUILayout.BeginHorizontal();
                if (Choice(Host.Offline == OfflineMode.Paused, "Nothing", _btn)) Host.Offline = OfflineMode.Paused;
                if (Choice(Host.Offline == OfflineMode.Gentle, "24 years", _btn)) Host.Offline = OfflineMode.Gentle;
                if (Choice(Host.Offline == OfflineMode.Living, "A century", _btn)) Host.Offline = OfflineMode.Living;
                if (Choice(Host.Offline == OfflineMode.Ancient, "600 years", _btn)) Host.Offline = OfflineMode.Ancient;
                GUILayout.EndHorizontal();
            });
            GUILayout.Space(6);
            GUILayout.Label("The guide", _pageCap);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Walk me through it again", _btn, GUILayout.Height(30f)))
            {
                Settings.ClearHints(); _menuOn = false; _menuPage = MenuMain;
                if (_titleOn) TitleLeave();
                GuideStart();
            }
            if (GUILayout.Button("Show the small hints again", _btn, GUILayout.Height(30f))) { Settings.ClearHints(); MenuNote("The small hints will show again.", true); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (muted != AudioView.Muted || lite != Sky.ShadowLite || vol != AudioView.Volume || clouds != Settings.Clouds || detail != Settings.Detail || cam != Settings.CamSpeed || shake != Settings.Shake || ui != Settings.UiScale || names != Hud.Names || offline != Host.Offline)
            {
                Settings.Apply();
                _settingsDirty = Time.unscaledTime;
            }
        }

        GUIStyle _crHead, _crText, _crDim, _crName, _crNameC, _crRole, _howText, _keyCap;

        void CreditStyles()
        {
            if (_crHead != null) return;
            _crHead = new GUIStyle(_pageCap) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            _crText = new GUIStyle(_mnText) { alignment = TextAnchor.UpperCenter, fontSize = 16, wordWrap = true };
            _crDim = new GUIStyle(_mnTextDim) { alignment = TextAnchor.UpperCenter, fontSize = 14, wordWrap = true };
            _crName = new GUIStyle(_pageCap) { fontSize = 20, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _crName.normal.textColor = new Color(0.96f, 0.93f, 0.85f);
            _crNameC = new GUIStyle(_crName) { alignment = TextAnchor.MiddleCenter };
            _crRole = new GUIStyle(_mnTextDim) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _howText = new GUIStyle(_mnText) { fontSize = 16, wordWrap = true };
            _keyCap = new GUIStyle(_pageCap) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }

        /// <summary>The small rule between the credits' parts.</summary>
        void CreditRule(float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 30f, GUILayout.Height(30f));
            Ornament(new Rect(r.center.x - 120f, r.y + 6f, 240f, 18f), 0.6f);
        }

        void CreditHead(string text) { GUILayout.Label(text, _crHead); GUILayout.Space(2); }

        /// <summary>A name and what they did, centred on one line (two when it is too long for the page).</summary>
        void CreditPair(string name, string role, float width)
        {
            float wn = _crName.CalcSize(new GUIContent(name)).x, wr = _crRole.CalcSize(new GUIContent(role)).x;
            if (wn + 14f + wr > width) { GUILayout.Label(name, _crNameC); GUILayout.Label(role, _crDim); return; }
            Rect r = GUILayoutUtility.GetRect(width, 28f, GUILayout.Height(28f));
            if (Event.current.type != EventType.Repaint) return;
            float x = r.x + (r.width - wn - 14f - wr) * 0.5f;
            GUI.Label(new Rect(x, r.y, wn, r.height), name, _crName);
            GUI.Label(new Rect(x + wn + 14f, r.y + 2f, wr, r.height), role, _crRole);
        }

        /// <summary>The credits, from the project's own record of them (Assets/TC2/CREDITS.md).</summary>
        void CreditsPage(float width)
        {
            CreditStyles();
            var logo = Ui("logo");
            float lw = Mathf.Min(width * 0.72f, 420f), lh = lw * logo.height / Mathf.Max(1f, logo.width);
            Rect lr = GUILayoutUtility.GetRect(width, lh + 8f, GUILayout.Height(lh + 8f));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(lr.center.x - lw * 0.5f, lr.y + 4f, lw, lh), logo, ScaleMode.ScaleToFit, true);
            GUILayout.Label("A world lived person by person. Nothing in it is scripted, and every story in its book really happened.", _crDim);
            CreditRule(width);
            CreditHead("Made by");
            CreditPair("Arjun", "direction", width);
            CreditPair("Claude, by Anthropic", "engineering and art", width);
            CreditRule(width);
            CreditHead("Art");
            GUILayout.Label("Every model, texture, icon and shader is made by the project's own code: the planet in Unity, the models in Blender from a Python script, the interface's pictures and its logotype in Python.", _crText);
            CreditRule(width);
            CreditHead("Type");
            CreditPair("EB Garamond", "Copyright 2017 The EB Garamond Project Authors", width);
            CreditPair("IM FELL English", "Copyright 2010 Igino Marini", width);
            GUILayout.Label("Both under the SIL Open Font License 1.1", _crDim);
            CreditRule(width);
            CreditHead("Sound");
            GUILayout.Label("The world's sound was generated for this game with ElevenLabs' sound effects model and levelled with ffmpeg. The stillness and most of the interface's cues are synthesised by the project's own code.", _crText);
            CreditRule(width);
            CreditHead("Code");
            GUILayout.Label("Deterministic maths after fdlibm (Copyright 1993 Sun Microsystems). SplitMix64 mixing after Steele, Lea and Flood (2014), from Sebastiano Vigna's public-domain reference.", _crText);
            CreditRule(width);
            CreditHead("Built with");
            GUILayout.Label("Unity 6.6  ·  Blender  ·  Python  ·  ffmpeg", _crText);
            GUILayout.Label("One deterministic core: the same world, bit for bit, in every browser.", _crDim);
            GUILayout.Space(8);
        }

        void NewWorldPage(TC2.Core.Simulation.World w, bool onTitle)
        {
            if (_seedText == null) _seedText = NextSeed();
            GUILayout.Label("A new planet with its own seas, its own climate and its own first peoples. Who settles where, who rules, and what becomes of them is theirs to decide.", _mnText);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Its seed", _mnText, GUILayout.Width(90f), GUILayout.Height(34f));
            _seedText = GUILayout.TextField(_seedText ?? "", 12, GUILayout.Width(170f), GUILayout.Height(34f));
            if (GUILayout.Button("Another", _btn, GUILayout.Height(34f))) _seedText = NextSeed();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("The same seed always makes the same world.", _mnTextDim);
            GUILayout.Space(10);
            bool hasWorld = w != null && Host.Continued;
            if (hasWorld)
            {
                GUILayout.Label("The world you have now - " + YearAndSeason(w.Now) + ", " + w.LivingPeople().ToString("N0", CultureInfo.InvariantCulture) + " people - will be lost unless you keep a copy of it first (Worlds Kept).", _mnText);
                GUILayout.Space(10);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(hasWorld ? "End this world and begin the new one" : "Begin this world", _btn, GUILayout.Height(36f)))
            {
                if (!ulong.TryParse(_seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed)) { seed = 0; foreach (char c in _seedText ?? "") seed = seed * 131UL + c; }
                _newWorldSeed = seed; _newWorldFrames = 0; _newWorldBusy = true;
                if (!onTitle) { _menuHeld = false; TitleEnter(); }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void WorldsPage(TC2.Core.Simulation.World w, bool onTitle)
        {
            GUILayout.Label("Three copies can be kept in this browser beside the world you are living in. A copy does not go on while you are away.", _mnTextDim);
            GUILayout.Space(10);
            for (int i = 0; i < Settings.KeptSlots; i++)
            {
                string label = Settings.KeptLabel(i);
                GUILayout.Label((i + 1).ToString(CultureInfo.InvariantCulture) + ".  " + (label.Length > 0 ? label : "<i>empty</i>"), _mnText);
                GUILayout.BeginHorizontal();
                int slot = i;
                if (w != null && GUILayout.Button(label.Length > 0 ? "Keep this world here instead" : "Keep this world here", _btn, GUILayout.Height(30f)))
                {
                    string now = YearAndSeason(w.Now) + ", " + w.LivingPeople().ToString("N0", CultureInfo.InvariantCulture) + " people in " + w.InhabitedSettlements() + " places (seed " + w.Seed + ")";
                    Host.SaveCopy("kept" + slot, ok => { if (ok) { Settings.SetKept(slot, now); _copyNote = "Kept."; } else _copyNote = "The browser would not keep it."; });
                }
                if (label.Length > 0 && GUILayout.Button("Go back to it", _btn, GUILayout.Height(30f)))
                {
                    Host.LoadCopy("kept" + slot, ok =>
                    {
                        _copyNote = ok ? "Back in that world." : "That copy cannot be opened by this version of the game.";
                        if (ok) { _menuPage = MenuMain; _menuOn = false; if (_titleOn) TitleLeave(); }
                    });
                }
                if (label.Length > 0 && GUILayout.Button("Let it go", _btn, GUILayout.Height(30f))) { Host.Store.Delete("kept" + slot); Settings.SetKept(slot, ""); }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
            }
            if (_copyNote != null) GUILayout.Label(_copyNote, _mnTextDim);
        }
    }
}
