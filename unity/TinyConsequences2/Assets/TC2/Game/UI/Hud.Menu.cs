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
            if (MenuWord(new Rect(colX, y, itemW, itemH), 2, "Worlds Kept")) _menuPage = _menuPage == MenuWorlds ? MenuMain : MenuWorlds;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 3, "How to Play")) _menuPage = _menuPage == MenuHow ? MenuMain : MenuHow;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 4, "Settings")) _menuPage = _menuPage == MenuSettings ? MenuMain : MenuSettings;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 5, "Credits")) _menuPage = _menuPage == MenuCredits ? MenuMain : MenuCredits;
            y += itemH;
            if (MenuWord(new Rect(colX, y, itemW, itemH), 6, "To the Title")) { Host.SaveNow(); _menuHeld = false; TitleEnter(); }
            y += itemH + 8f;
            if (_menuNote != null && Time.unscaledTime - _menuNoteAt < 7f) Edged(new Rect(colX, y, itemW + 200f, 22f), _menuNote, _titleWorld);
            if (_menuPage != MenuMain)
            {
                float pw = Mathf.Min(620f, sw - (colX + itemW + 34f) - 24f);
                if (pw < 360f) pw = Mathf.Min(sw - 40f, 620f);
                float ph = Mathf.Min(sh - 56f, _menuPage == MenuSettings ? 620f : _menuPage == MenuHow ? 640f : _menuPage == MenuCredits ? 560f : 460f);
                MenuPageDraw(new Rect(sw - pw - 28f, Mathf.Max(20f, (sh - ph) * 0.5f), pw, ph), w, false);
            }
            PointerOverUi = true;
        }

        /// <summary>A page of the menu in its panel.</summary>
        void MenuPageDraw(Rect r, TC2.Core.Simulation.World w, bool onTitle)
        {
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
                case MenuCredits: CreditsPage(); break;
                case MenuNew: NewWorldPage(w, onTitle); break;
                case MenuWorlds: WorldsPage(w, onTitle); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void HowRow(string icon, string head, string text, float width)
        {
            GUILayout.BeginHorizontal();
            Rect ic = GUILayoutUtility.GetRect(46f, 46f, GUILayout.Width(46f), GUILayout.Height(46f));
            if (Event.current.type == EventType.Repaint) { GUI.DrawTexture(ic, Ui("seal")); Icon(Inset(ic, 11f), icon, new Color(0.97f, 0.88f, 0.66f)); }
            GUILayout.Space(12);
            GUILayout.BeginVertical(GUILayout.Width(width - 60f));
            GUILayout.Label(head, _pageCap);
            GUILayout.Label(text, _mnText);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        void HowToPage(float width)
        {
            GUILayout.Label("You do not command this world. It lives on its own; you watch it, read it, and now and then reach in.", _mnTextDim);
            GUILayout.Space(10);
            HowRow("eye", "Look", "Drag to turn the planet. The wheel comes down to a village or rises to the whole world. A click on a far village opens its page.", width);
            HowRow("person", "Read the living", "Point at anyone or anything - a person, a house, a work, a road, a field, a ruin - to see what it is and why. A click opens its page, its life and its Why?.", width);
            HowRow("hourglass", "Time", "Space holds the world still; the ladder at the top (or 1-8) sets the speed. Skip leaps years ahead, watched from orbit. The world lives on while you are away.", width);
            HowRow("book", "The History Book", "B opens it. Every line happened and tells what caused it. After a long skip, read what changed.", width);
            HowRow("lightning", "Reach in", "Choose a power at the foot of the screen, then click the ground; right-click stops. Each comes back in the world's time. What follows is the world's own doing - Your Actions, in the book, traces it.", width);
            HowRow("menu", "Keys", "B the book  ·  R the realms  ·  N names  ·  M sound  ·  H hide all this  ·  Esc the menu  ·  W A S D move  ·  Q E rise and come down", width);
            if (GUILayout.Button("Walk me through it", _btn, GUILayout.Height(34f)))
            {
                _menuOn = false; _menuPage = MenuMain;
                if (_titleOn) TitleLeave();
                GuideStart();
            }
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

        void CreditsPage()
        {
            GUILayout.Label("Tiny Consequences II", _pageCap);
            GUILayout.Label("A world simulated person by person: nothing in it is scripted, and every story in its book happened.", _mnText);
            GUILayout.Space(10);
            GUILayout.Label("Made by", _pageCap);
            GUILayout.Label("Arjun, who directs it, with Claude, who writes and draws it. Every mesh, texture, icon and shader is generated by the project's own code.", _mnText);
            GUILayout.Space(10);
            GUILayout.Label("Type", _pageCap);
            GUILayout.Label("EB Garamond (Copyright 2017 The EB Garamond Project Authors) and IM FELL English (Copyright 2010 Igino Marini), both under the SIL Open Font License 1.1.", _mnText);
            GUILayout.Space(10);
            GUILayout.Label("Sound", _pageCap);
            GUILayout.Label("The world's sound was generated for this project with ElevenLabs' sound effects model and levelled with ffmpeg; the interface's and the stillness's were synthesised by the project's own code.", _mnText);
            GUILayout.Space(10);
            GUILayout.Label("Built with", _pageCap);
            GUILayout.Label("Unity 6, and for a planet that keeps its word: one deterministic core that plays the same in every browser, bit for bit.", _mnText);
        }

        void NewWorldPage(TC2.Core.Simulation.World w, bool onTitle)
        {
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
