using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 31: the first minutes) A short walk through the living world, shown rather than told: the whole planet; down to a real village; a real person, drawn where
    /// they are, to point at and click; their life; why their place is as it is; the book; time and the skip from orbit; the powers last. It never stops the world, a line
    /// or two at a time in a small card, and whatever it points at is ringed; what the player does moves it on, and Next always does. Skip at any time, again from the menu.
    /// And the small hints after it, each said once: "Click a person to follow their life", "Open Why? to trace how this happened", "Skip decades and read what changed".
    /// Reads the world and moves the camera; writes nothing in the world.
    /// </summary>
    public sealed partial class Hud
    {
        int _guide = -1;              // the step (-1: not walking)
        int _guideEntered = -2;
        int _guideSid, _guidePid;     // the village and the person it shows (0 none)
        Rect _guideCard, _guideTarget, _lifeBtnRect, _whyTabRect;
        bool _guideWaitsLapse;
        float _guideAt;
        string _guideTitleText = "", _guideText = "";
        GUIStyle _guideHead, _guideBody, _guideStep;
        const int GuideLast = 12;

        /// <summary>Begins the guide (the first time a world is entered, or asked for in the menu).</summary>
        public void GuideStart()
        {
            _guide = 0; _guideEntered = -2; _guidePid = 0; _guideSid = 0; _guideAt = Time.unscaledTime;
            if (Host.SpeedIndex == 0 || Host.SpeedIndex > 3) Host.SpeedIndex = 3;
        }

        void GuideEnd(bool finished)
        {
            _guide = -1; _guideTarget = Rect.zero; _guideCard = Rect.zero; _guidePid = 0; _guideGround = false;
            Settings.GuideSeen = true; Settings.Save();
            if (!finished) { AudioView.Ui("click"); }
        }

        bool _guideGround;
        Rect _guideNextRect, _guideSkipYearRect, _power0Rect;

        /// <summary>The greatest inhabited village.</summary>
        static int GreatestPlace(TC2.Core.Simulation.World w)
        {
            int sid = 0, most = -1;
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most) { most = s.Population; sid = id; } }
            return sid;
        }

        /// <summary>A grown person of a place drawn in the open now, doing something (not asleep, not indoors): the one nearest its square. 0 if none (it is night).</summary>
        int GuidePerson(TC2.Core.Simulation.World w, int sid)
        {
            if (sid == 0 || !w.Settlements.Exists(sid)) return 0;
            Vec3d at = PlaceCentres.Of(w, sid, Host.WorldVersion);
            double t = Host.RenderTime, best = double.MaxValue; int found = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive || p.AgeYears(w.Now) < 14 || p.At != sid) continue;
                if (!Presence.Of(w, in p, t, out var d, out _, out _, out _)) continue;
                double dist = Vec3d.ArcDistance(d, at, w.Planet.Radius);
                if (dist < best) { best = dist; found = res[i]; }
            }
            return found;
        }

        /// <summary>Entering a step: its words, and where the camera goes.</summary>
        void GuideEnter(TC2.Core.Simulation.World w, int step)
        {
            _guideAt = Time.unscaledTime;
            _guideTarget = Rect.zero;
            _guideGround = false;
            switch (step)
            {
                case 0:
                    _book = false; _personPage = false; _placeSid = 0; _building = 0; _selected = 0;
                    _guideSid = GreatestPlace(w);
                    Rig.FlyTo(_guideSid != 0 ? PlaceCentres.Of(w, _guideSid, Host.WorldVersion) : Vec3d.FromLatLon(Rig.Lat, Rig.Lon), w.Planet.Radius * 2.2, 2.8f);
                    _guideTitleText = "This world is not waiting for you";
                    _guideText = "Its weather, its woods, its beasts and its people all live by the same rules, and go on whether you watch or not. Nobody gives them orders - not even you.";
                    break;
                case 1:
                    _guideSid = GreatestPlace(w);
                    string name = _guideSid != 0 ? w.History.NameOf(EntityRef.Settlement(_guideSid)) : "A village";
                    if (_guideSid != 0) Rig.FlyTo(PlaceCentres.Of(w, _guideSid, Host.WorldVersion), 900.0, 3.6f);
                    _guideTitleText = name;
                    _guideText = "Nobody placed " + name + ". People chose this ground, and every field, road and house you see was raised by someone, for a reason.";
                    break;
                case 2:
                    _guidePid = 0;
                    _guideTitleText = "Everyone down there has a life";
                    _guideText = "Each figure is one person, with a family, a trade and their own reasons for what they are doing today. Point at the one ringed in gold.";
                    _guideGround = true;
                    break;
                case 3:
                    _guideTitleText = "Now click them";
                    _guideText = "Pointing tells you what they are doing and why. Clicking opens who they are.";
                    _guideGround = true;
                    break;
                case 4:
                    _guideTitleText = "A person, not a unit";
                    _guideText = "Their card says what they are doing, and why - from the same choice the world itself made. Open their life.";
                    break;
                case 5:
                    _guideTitleText = "Their family, their work, their life";
                    _guideText = "Nothing on this page was written for show: it is what the world remembers of them. Every name opens that person. Try Family, or Life.";
                    break;
                case 6:
                    {
                        _personPage = false; _selected = 0; _guidePid = 0;
                        int home = _guideSid;
                        if (home != 0) ShowPlace(home, PlaceWhyTab);
                        _guideTitleText = "Why?";
                        _guideText = "Places, works and people can all say why they are as they are. Each answer is a chain of real records, back to where it began: nature, people's choices - or you.";
                        break;
                    }
                case 7:
                    _placeSid = 0;
                    _guideTitleText = "The History Book";
                    _guideText = "The world writes its own chronicle as it ages: ages, rulers, wars, plagues, great lives. Open it.";
                    break;
                case 8:
                    _guideTitleText = "Everything in it happened";
                    _guideText = "Click any line and the book tells what caused it. Its pages grow as the years pass, and they are worth reading after a long skip.";
                    break;
                case 9:
                    _book = false;
                    _guideTitleText = "Time is yours";
                    _guideText = "Hold the world still, or let it run. At a day a second and faster the camera rises to orbit, because the days pass too fast to watch from the ground.";
                    break;
                case 10:
                    _guideWaitsLapse = false;
                    _guideTitleText = "Watch the years go by";
                    _guideText = "Skip leaps ahead, watched from orbit, and brings you back to where you were. Try a year now: when you come back, a leaf tells what the year did.";
                    break;
                case 11:
                    _guideTitleText = "And you may reach in";
                    _guideText = "You cannot command anyone. You can only change the world they live in, and see what they do about it. Choose a power, then click the ground.";
                    break;
                default:
                    _guideTitleText = "The rest is theirs";
                    _guideText = "What follows from your hand is their own doing. Your Actions, the book's last part, traces each thing you do across the decades. That is all: the world is yours to watch.";
                    break;
            }
        }

        /// <summary>One frame of the guide's logic (Update): what the player has done moves it on.</summary>
        void GuideTick(TC2.Core.Simulation.World w)
        {
            if (_guide < 0 || _titleOn || Rig == null) return;
            if (_guideEntered != _guide) { _guideEntered = _guide; GuideEnter(w, _guide); }
            switch (_guide)
            {
                case 2:
                case 3:
                    // a person drawn in the open: night or nobody out, the hours run on to morning (below the speed at which the camera rises)
                    if (_guidePid == 0 || !w.People.Exists(_guidePid) || !w.People.Get(_guidePid).Alive || !Presence.Of(w, in w.People.Get(_guidePid), Host.RenderTime, out _, out _, out _, out _))
                    {
                        int found = GuidePerson(w, _guideSid);
                        if (found != 0)
                        {
                            _guidePid = found;
                            if (Host.SpeedIndex > 3) Host.SpeedIndex = 2;
                            Presence.Of(w, in w.People.Get(found), Host.RenderTime, out var d, out _, out _, out _);
                            if (!Rig.Flying) Rig.FlyTo(d, 75.0, 2.6f);
                        }
                        else if (Host.SpeedIndex < 5)
                        {
                            Host.SpeedIndex = 5;
                            _guideTitleText = "Night has fallen";
                            _guideText = "Its people are abed. The hours run on to morning.";
                        }
                    }
                    else
                    {
                        if (Host.SpeedIndex > 2) Host.SpeedIndex = 1;   // (slow enough to point at someone walking)
                        // the camera keeps with them
                        if (!Rig.Flying && Presence.Of(w, in w.People.Get(_guidePid), Host.RenderTime, out var d, out _, out _, out _)) { Rig.Lat = d.Latitude; Rig.Lon = d.Longitude; }
                    }
                    if (_guide == 2 && _hoverPid != 0 && _guidePid != 0) _guide = 3;
                    if (_selected != 0) _guide = 4;
                    break;
                case 4:
                    if (_selected == 0) _guide = 3;
                    else if (_personPage) _guide = 5;
                    break;
                case 7:
                    if (_book) _guide = 8;
                    break;
                case 10:
                    if (_guideWaitsLapse && Rig != null && !Rig.LapseActive && !Host.CatchingUp) _guide = 11;
                    break;
                case 11:
                    if (_castNoteAt > _guideAt && _castOk) _guide = 12;
                    break;
            }
        }

        void GuideStyles()
        {
            TitleStyles();
            if (_guideHead != null) return;
            _guideHead = new GUIStyle(_pageTitle2) { fontSize = 24, wordWrap = true };
            _guideBody = new GUIStyle(_mnText) { fontSize = 17, wordWrap = true };
            _guideStep = new GUIStyle(_mnTextDim) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }

        /// <summary>The guide's card and ring (the HUD's OnGUI, over everything but the tag).</summary>
        void GuideDraw(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (_guide < 0) { _guideCard = Rect.zero; return; }
            GuideStyles();
            // what it points at
            Rect target = Rect.zero;
            switch (_guide)
            {
                case 4: target = _lifeBtnRect; break;
                case 6: target = _whyTabRect; break;
                case 7: target = _bookBtn; break;
                case 9: target = Rect.MinMaxRect(_pauseRect.x, _left.y + 4f, _speedRect.xMax + 110f, _left.yMax - 4f); break;
                case 10: target = _skipMenu ? _skipRect : _skipBtn; break;
                case 11: target = _powersRect; break;
            }
            if ((_guide == 2 || _guide == 3) && _guidePid != 0 && w.People.Exists(_guidePid) && Rig != null && Presence.Of(w, in w.People.Get(_guidePid), Host.RenderTime, out var d, out _, out _, out _))
            {
                Vector3 sp = Rig.Cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, d));
                if (sp.z > 0f) { float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale; target = new Rect(x - 13f, y - 25f, 26f, 32f); }
            }
            _guideTarget = target;
            if (target.width > 0f) Halo(target);
            // the card: lower left, clear of the power bar; high up while a page or the book covers that corner's words
            float cw = Mathf.Min(430f, sw - 24f);
            float inner = cw - 44f;
            float h1 = _guideHead.CalcHeight(new GUIContent(_guideTitleText), inner), h2 = _guideBody.CalcHeight(new GUIContent(_guideText), inner);
            float ch = 14f + h1 + 6f + h2 + 12f + 34f + 16f;
            float cy = (_powersRect.width > 0f ? _powersRect.y : sh) - ch - 14f;
            if (_book) cy = sh - ch - 12f;
            float cx = 14f;
            if (_pageRect.width > 0f && _pageRect.x < cx + cw + 8f) cx = Mathf.Max(6f, _pageRect.x - cw - 8f);
            if (_summaryRect.width > 0f && !_book) cx = Mathf.Min(_summaryRect.xMax + 14f, sw - cw - 8f);
            _guideCard = new Rect(cx, Mathf.Max(70f, cy), cw, ch);
            GUI.Box(_guideCard, GUIContent.none, _box);
            GUI.Label(new Rect(_guideCard.x + 22f, _guideCard.y + 14f, inner, h1), _guideTitleText, _guideHead);
            GUI.Label(new Rect(_guideCard.x + 22f, _guideCard.y + 20f + h1, inner, h2), _guideText, _guideBody);
            float by = _guideCard.yMax - 44f;
            // its foot: the steps, Skip, Back, Next
            GUI.Label(new Rect(_guideCard.x + 22f, by + 2f, 70f, 28f), (_guide + 1) + " of " + (GuideLast + 1), _guideStep);
            if (GUI.Button(new Rect(_guideCard.x + 92f, by, 92f, 30f), "Skip guide", _btn)) { GuideEnd(false); return; }
            float rx = _guideCard.xMax - 22f;
            string next = _guide >= GuideLast ? "Done" : _guide == 2 || _guide == 3 ? "Skip this" : "Next";
            float nw = _guide >= GuideLast ? 80f : 86f;
            _guideNextRect = new Rect(rx - nw, by, nw, 30f);
            _guideSkipYearRect = _guide == 10 ? new Rect(rx - nw - 68f - 104f, by, 98f, 30f) : Rect.zero;
            if (GUI.Button(_guideNextRect, next, _btn))
            {
                AudioView.Ui("click");
                if (_guide >= GuideLast) { GuideEnd(true); return; }
                if (_guide == 2 || _guide == 3 || _guide == 4 || _guide == 5) { _personPage = false; _selected = 0; _guide = 6; }
                else _guide++;
            }
            if (_guide >= 6 && GUI.Button(new Rect(rx - nw - 68f, by, 62f, 30f), "Back", _btn)) { AudioView.Ui("click"); _guide = Mathf.Max(0, _guide == 6 ? 1 : _guide - 1); }
            else if (_guide == 1 && GUI.Button(new Rect(rx - nw - 68f, by, 62f, 30f), "Back", _btn)) { AudioView.Ui("click"); _guide = 0; }
            // in the skip's step: a year, from the card itself as well
            if (_guide == 10 && !Host.CatchingUp && !(Rig != null && Rig.LapseActive) && GUI.Button(new Rect(rx - nw - 68f - 104f, by, 98f, 30f), "Skip a year", _btn)) { AudioView.Ui("click"); Host.BeginSkip(SimTime.Year); _guideWaitsLapse = true; }
        }

        // ---- the small hints, each once

        readonly System.Collections.Generic.Dictionary<string, float> _hintSince = new System.Collections.Generic.Dictionary<string, float>();
        string _hintShown, _hintKey; float _hintAt;
        float _playedFor;
        bool _everZoomed, _everPicked, _everCast, _everSkipped;

        /// <summary>A hint's condition held for a while and not yet said: it is said, once.</summary>
        void Hint(string key, bool holds, float after, string text)
        {
            if (_hintShown != null || Settings.HintSaid(key)) return;
            if (!holds) { _hintSince.Remove(key); return; }
            if (!_hintSince.TryGetValue(key, out float since)) { _hintSince[key] = Time.unscaledTime; return; }
            if (Time.unscaledTime - since < after) return;
            _hintShown = text; _hintKey = key; _hintAt = Time.unscaledTime; Settings.SayHint(key);
        }

        /// <summary>Which small hint is due (Update, when no guide runs).</summary>
        void HintsTick(TC2.Core.Simulation.World w)
        {
            if (_titleOn || _guide >= 0 || _menuOn || Host.CatchingUp || Rig == null) return;
            _playedFor += Time.unscaledDeltaTime;
            if (_hintShown != null && Time.unscaledTime - _hintAt > 9f) _hintShown = null;
            double alt = Rig.Distance - w.Planet.Radius;
            if (alt < 2500.0) _everZoomed = true;
            if (_selected != 0) _everPicked = true;
            if (_castOk && _castNoteAt > 0f) _everCast = true;
            if (Host.Skipping) _everSkipped = true;
            Hint("zoom", !_everZoomed, 10f, "Scroll to come down from orbit to a village.");
            Hint("person", alt < 400.0 && !_everPicked, 8f, "Click a person to follow their life.");
            Hint("why", _placeSid != 0 && _placeTab != PlaceWhyTab, 6f, "Open Why? to trace how this came about.");
            Hint("book", _lapseSummary != null && _lapseSummary.Count > 0, 4f, "The years you skipped are written down: open the History Book.");
            Hint("hand", _hand.Count > 0, 3f, "Your hand shows what came of what you did. Give it time: the book's Your Actions follows it for decades.");
            Hint("skip", _playedFor > 150f && !_everSkipped, 0f, "Skip decades, then read what changed.");
            Hint("power", _playedFor > 240f && !_everCast, 0f, "Choose a power below, then click the ground. See what the world does about it.");
        }

        /// <summary>The hint now said: a line in the book's italic above the power bar, with a small gold mark.</summary>
        void HintDraw(float sw, float sh)
        {
            if (_hintShown == null) return;
            if (_book || _guide >= 0) return;
            if (_hintStyle == null) { _hintStyle = new GUIStyle(_mapSub) { fontSize = 17, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = false }; _hintStyle.normal.textColor = new Color(1f, 0.90f, 0.66f); }
            float floor = _powersRect.width > 0f ? _powersRect.y : sh;
            float a = Mathf.Clamp01((Time.unscaledTime - _hintAt) / 0.5f) * Mathf.Clamp01((9f - (Time.unscaledTime - _hintAt)) / 1.5f);
            var old = GUI.color; GUI.color = new Color(old.r, old.g, old.b, a);
            Edged(new Rect(0f, floor - 58f, sw, 26f), _hintShown, _hintStyle);
            GUI.color = old;
        }
        GUIStyle _hintStyle;
    }
}
