using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 31: the first minutes; session 32: the whole game taught) A walk through the living world, shown rather than told, each step ringing the real
    /// thing it speaks of and moved on by the player's own doing - Next always moves on, Back goes back, Skip guide at any time (again from the menu): the world
    /// that does not wait; a real village; the camera; a real person pointed at and clicked, their card, their life and following them; a place's page and its
    /// Why?; the History Book and the roots of every chain (nature, people, you); the realms on the map and the seals beside them; the powers' three groups;
    /// aiming and casting; the impact card; time and its speeds; a year skipped from orbit and the leaf of what it did; Your hand and the tree of what came of the
    /// cast, link by link across the year; and the world going on. A line or two at a time in a small card, over the tree too (which holds the screen). And the
    /// small hints after it, each said once. Reads the world and moves the camera; writes nothing in the world (the speed is the host's).
    /// </summary>
    public sealed partial class Hud
    {
        // the steps, in order (tc2/scripts/firstrun.mjs walks them by these numbers)
        const int GWorld = 0, GVillage = 1, GEye = 2, GPoint = 3, GClick = 4, GCard = 5, GLife = 6, GPlace = 7, GWhy = 8, GBook = 9, GBookOpen = 10, GRealms = 11,
                  GSky = 12, GGround = 13, GLand = 14, GCast = 15, GImpact = 16, GTime = 17, GSkip = 18, GYears = 19, GHand = 20, GTree = 21, GEnd = 22;
        const int GuideLast = GEnd;
        /// <summary>The guide's pace: the world as it is lived (one bar). Faster only while a step waits for time to pass (the night), and back after.</summary>
        const int GuidePace = 1;

        int _guide = -1;              // the step (-1: not walking)
        int _guideEntered = -2;
        int _guideSid, _guidePid;     // the village and the person it shows (0 none)
        Rect _guideCard, _guideTarget, _lifeBtnRect, _whyTabRect, _followBtnRect, _handFirstRect;
        bool _guideWaitsLapse, _guideRaised, _guideCast, _guideAiming, _guideRealmsHeld, _guideRealmsWas, _guideSawLeaf, _guideHandHad;
        int _guideAfterTree = GEnd, _guideTreeFrom = GHand, _guideFollowWas;
        float _guideAt, _guideFollowAt = -99f;
        string _guideTitleText = "", _guideText = "";
        GUIStyle _guideHead, _guideBody, _guideStep;
        Rect _guideNextRect, _guideSkipYearRect, _power0Rect;

        /// <summary>Begins the guide (the first time a world is entered, or asked for in the menu): at the world's own pace.</summary>
        public void GuideStart()
        {
            _guide = 0; _guideEntered = -2; _guidePid = 0; _guideSid = 0; _guideAt = Time.unscaledTime; _guideCast = false; _guideAfterTree = GEnd; _guideTreeFrom = GHand;
            Host.MaxSpeed = false; Host.SpeedIndex = GuidePace;
        }

        void GuideEnd(bool finished)
        {
            if (_guideRaised) { Host.SpeedIndex = GuidePace; _guideRaised = false; }
            GuideRealmsBack();
            _guide = -1; _guideTarget = Rect.zero; _guideCard = Rect.zero; _guidePid = 0; _guideSkipYearRect = Rect.zero;
            Settings.GuideSeen = true; Settings.Save();
            if (!finished) { AudioView.Ui("click"); }
            else foreach (string k in GuideTaught) if (!Settings.HintSaid(k)) Settings.SayHint(k);   // (walked to its end: the small hints it taught are not said again)
        }

        /// <summary>The small hints (HintsTick) whose lessons the whole guide gives.</summary>
        static readonly string[] GuideTaught = { "zoom", "person", "why", "book", "hand", "skip", "power" };

        /// <summary>The realms layer as it was before the guide's realms step (the step shows it; the powers want the plain land).</summary>
        void GuideRealmsBack() { if (_guideRealmsHeld) { RealmsView.On = _guideRealmsWas; _guideRealmsHeld = false; } }

        /// <summary>A step that wants the ground (the powers, the leaf, Your hand): a pace watched from orbit comes back to the guide's.</summary>
        void GuideGround() { if (Host.MaxSpeed || Host.SpeedIndex >= 6) { Host.MaxSpeed = false; Host.SpeedIndex = GuidePace; } }

        /// <summary>Your hand's leaf is open this frame (not only its seal).</summary>
        bool GuideHandOpen() => _handRect.width > 0f && _handOpen == _handRect;

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

        /// <summary>Where a group of the power bar stands (its seals and its name above them), from the bar and its first seal as drawn: 0 the sky, 1 the ground,
        /// 2 life and land.</summary>
        Rect PowerGroupRect(int g)
        {
            if (_powersRect.width <= 0f || _power0Rect.width <= 0f || g < 0 || g >= PowerGroupEnds.Length) return Rect.zero;
            float step = _power0Rect.width + 4f;
            float gap = PowerGroupEnds.Length > 1 ? Mathf.Max(0f, (_powersRect.width - 8f - Powers.Length * step) / (PowerGroupEnds.Length - 1)) : 0f;
            int first = g == 0 ? 0 : PowerGroupEnds[g - 1], end = PowerGroupEnds[g];
            return new Rect(_power0Rect.x + first * step + g * gap, _powersRect.y, (end - first) * step - 4f, _powersRect.height);
        }

        /// <summary>The words of the casting step: choosing a power, or (while one is aimed) its ring on the ground.</summary>
        void GuideCastWords(TC2.Core.Simulation.World w, bool force)
        {
            bool aiming = _aim >= 0;
            if (!force && aiming == _guideAiming) return;
            _guideAiming = aiming;
            if (aiming)
            {
                _guideTitleText = "Where it reaches";
                _guideText = "The ring on the ground is its reach, and the words beside it name the villages inside. Click the ground to cast; right-click puts it down.";
                return;
            }
            int ready = -1;
            for (int k = 0; k < Powers.Length && ready < 0; k++) if (Recharging(w, k) == 0) ready = k;
            _guideTitleText = "Reach in";
            _guideText = ready < 0 ? "Every power is still coming back: each returns in the world's own time, its seal filling as it does. Next goes on."
                       : "Point at a power to read what it does, then click it" + (ready == 0 ? " - Lightning, the first, is a good start." : " - " + Powers[ready].Name + " is ready.") +
                         " Once used, each comes back in the world's own time.";
        }

        /// <summary>Entering a step: its words, and where the camera goes.</summary>
        void GuideEnter(TC2.Core.Simulation.World w, int step)
        {
            _guideAt = Time.unscaledTime;
            _guideTarget = Rect.zero;
            if (step > 0) AudioView.Ui("leaf");   // (session 32: each step heard as a leaf turned, by Next or by the player's own doing)
            if (step != GRealms) GuideRealmsBack();
            switch (step)
            {
                case GWorld:
                    _book = false; _personPage = false; _placeSid = 0; _building = 0; _selected = 0;
                    _guideSid = GreatestPlace(w);
                    Rig.FlyTo(_guideSid != 0 ? PlaceCentres.Of(w, _guideSid, Host.WorldVersion) : Vec3d.FromLatLon(Rig.Lat, Rig.Lon), w.Planet.Radius * 2.2, 2.8f);
                    _guideTitleText = "This world does not wait for you";
                    _guideText = "Its people farm, build, marry and quarrel for their own reasons, whether you watch or not. Nobody gives them orders - not even you.";
                    break;
                case GVillage:
                    _guideSid = GreatestPlace(w);
                    string name = _guideSid != 0 ? w.History.NameOf(EntityRef.Settlement(_guideSid)) : "A village";
                    if (_guideSid != 0) Rig.FlyTo(PlaceCentres.Of(w, _guideSid, Host.WorldVersion), 900.0, 3.6f);
                    _guideTitleText = name;
                    _guideText = "Nobody placed " + name + ". Its people chose this ground, and raised every house, field and road you see for reasons of their own.";
                    break;
                case GEye:
                    _placeSid = 0; _personPage = false; _selected = 0; _guidePid = 0; _book = false;   // (come back to from a page: the land again)
                    _guideTitleText = "Your eye on the world";
                    _guideText = "Drag to turn the world under you; the wheel brings you down or lifts you up. Double-click the ground to fly down to it.";
                    break;
                case GPoint:
                    _guidePid = 0;
                    GuidePointWords(step);
                    break;
                case GClick:
                    GuidePointWords(step);
                    break;
                case GCard:
                    _guideTitleText = "A person, not a unit";
                    _guideText = "Their card shows their day and the real reason behind it - the choice the world itself made. Open their life.";
                    break;
                case GLife:
                    _guideFollowWas = _follow;
                    _guideTitleText = "A real life";
                    _guideText = "Family, work, the years behind them: all of it is what the world remembers, nothing written for show. Press follow, and the camera keeps with them through their days.";
                    break;
                case GPlace:
                    {
                        _personPage = false; _selected = 0; _guidePid = 0; _building = 0; _book = false;
                        if (_guideRaised) { Host.SpeedIndex = GuidePace; _guideRaised = false; }   // (run on through a night to find someone: the guide's pace again)
                        int home = _guideSid;
                        if (home != 0 && w.Settlements.Exists(home)) ShowPlace(home, PlaceNowTab);
                        string n = home != 0 ? w.History.NameOf(EntityRef.Settlement(home)) : "";
                        _guideTitleText = home != 0 ? n + "'s page" : "A place's page";
                        _guideText = (home != 0 ? "This is " + n + " as it is now" : "A village's page is the place as it is now") + ": its people, food, works, land and realm. Click any village for its own. Open Why? to ask how it came to be.";
                        break;
                    }
                case GWhy:
                    {
                        // (the page the player has open, at its Why?; else the guide's village)
                        if (_placeSid != 0 && w.Settlements.Exists(_placeSid)) { if (_placeTab != PlaceWhyTab) { _placeTab = PlaceWhyTab; _pageScroll = Vector2.zero; } }
                        else if (_guideSid != 0 && w.Settlements.Exists(_guideSid)) ShowPlace(_guideSid, PlaceWhyTab);
                        _guideTitleText = "Why?";
                        _guideText = "Each answer is a real record and the records it followed from, link by link. Open one: every chain ends where it began - in nature, in people's own choices, or with you.";
                        break;
                    }
                case GBook:
                    _placeSid = 0; _book = false;
                    _guideTitleText = "The History Book";
                    _guideText = "The world writes its own chronicle as it ages: rulers, wars, plagues, great lives. Open it.";
                    break;
                case GBookOpen:
                    _placeSid = 0;
                    if (!_book) { _book = true; AudioView.Ui("page"); }
                    _guideTitleText = "Everything in it happened";
                    _guideText = "Click any line to see why it happened: each cause is a record of its own, back to its root - nature, people, or your hand. The book grows with the years.";
                    break;
                case GRealms:
                    _book = false; _placeSid = 0; _personPage = false;
                    if (!_guideRealmsHeld) { _guideRealmsWas = RealmsView.On; _guideRealmsHeld = true; }
                    if (_guideSid != 0 && w.Settlements.Exists(_guideSid)) Rig.FlyTo(PlaceCentres.Of(w, _guideSid, Host.WorldVersion), 3800.0, 2.6f);   // (high enough to see a realm)
                    _guideTitleText = "The realms";
                    _guideText = "Try this seal (R): it lays the realms on the land - who pays tribute to whom, each realm in its colour, its wars as dark spears. Beside it: the world now, recent history, the sound, the menu.";
                    break;
                case GSky:
                case GGround:
                case GLand:
                    _book = false; _placeSid = 0; _personPage = false; _building = 0;
                    GuideGround();
                    if (step == GSky && Rig.Distance - w.Planet.Radius > 1400.0 && _guideSid != 0 && w.Settlements.Exists(_guideSid)) Rig.FlyTo(PlaceCentres.Of(w, _guideSid, Host.WorldVersion), 900.0, 2.4f);
                    if (step == GSky)
                    {
                        _guideTitleText = "Your powers: the sky";
                        _guideText = "You cannot command anyone - only change the world they live in. The sky's powers: lightning, a storm, rain or drought, a warmer or a colder summer.";
                    }
                    else if (step == GGround)
                    {
                        _guideTitleText = "The ground";
                        _guideText = "A quake, an eruption, a stone thrown down from the sky: the violent powers. Homes fall, slopes slide, the sea may come - and people run for open ground, or lie hurt.";
                    }
                    else
                    {
                        _guideTitleText = "Life and land";
                        _guideText = "A sickness, a blight on the crop, tired fields renewed; the wolves and bears culled, game brought back, a vein of copper laid bare. What comes of these takes seasons and years.";
                    }
                    break;
                case GCast:
                    _book = false; _placeSid = 0; _personPage = false; _building = 0;
                    GuideGround();
                    GuideCastWords(w, true);
                    break;
                case GImpact:
                    _guideTitleText = "The world answers";
                    _guideText = "At the top, the first of it: what your cast did, and the people it touched - running for open ground, keeping together, hurt. Click it to see what came of it.";
                    break;
                case GTime:
                    _guideTitleText = "Time is yours";
                    _guideText = "One bar is the world as it is lived, a second for a second. Each bar more runs it faster, and from a day a second the camera rises to orbit. Space holds it still.";
                    break;
                case GSkip:
                    _guideWaitsLapse = false;
                    GuideGround();
                    _guideTitleText = "Skip the years";
                    _guideText = "Skip leaps a season, a year, ten or a hundred ahead, watched from orbit, then brings you back. Skip a year now, and see what it did - to the world, and to what you set going.";
                    break;
                case GYears:
                    GuideGround();
                    _guideSawLeaf = false;
                    _guideTitleText = "The years passed";
                    _guideText = "This leaf is what the year did, by the world's own count: births and deaths and their causes, places, realms, troubles - and what traces back to you. A line of note opens its why.";
                    break;
                case GHand:
                    GuideGround();
                    _book = false; _placeSid = 0; _personPage = false; _building = 0; _history = false; _lapseSummary = null; _lostNote = null;   // (Your hand is drawn when no page stands over it)
                    _handShowUntil = Mathf.Max(_handShowUntil, Time.unscaledTime + 30f);
                    _guideHandHad = _hand.Count > 0;
                    _guideTitleText = "Your hand";
                    _guideText = GuideHandWords();
                    break;
                case GTree:
                    _guideTitleText = "What came of it";
                    _guideText = "Each card came of the one it hangs from: what your cast did at once, then what followed days and years on. Point at a card for who and what; click one to go there." +
                                 (_guideAfterTree == GTime ? " It grows as the years pass." : "");
                    break;
                default:
                    _guideTitleText = "It goes on without you";
                    _guideText = "What you set going keeps unfolding, and the book keeps every link of it. Come back later - the world lives on while you are away - and see what came of it.";
                    break;
            }
        }

        void GuidePointWords(int step)
        {
            if (step == GPoint) { _guideTitleText = "Everyone here has a life"; _guideText = "Each figure is one person, with a family, a trade and a reason for what they are doing. Point at the one ringed in gold."; }
            else { _guideTitleText = "Now click them"; _guideText = "Pointing shows what they are doing, and why. A click opens who they are."; }
        }

        string GuideHandWords() => _hand.Count > 0
            ? "Your hand, at the lower left, keeps your last casts and what has come of each so far. Click one to open its tree."
            : "Whenever you cast, Your hand at the lower left keeps what came of it. Click a cast there to open its tree.";

        /// <summary>One frame of the guide's logic (Update): what the player has done moves it on.</summary>
        void GuideTick(TC2.Core.Simulation.World w)
        {
            if (_guide < 0 || _titleOn || Rig == null) return;
            if (_guideEntered != _guide) { _guideEntered = _guide; GuideEnter(w, _guide); }
            // a cast's tree opened from anywhere (the impact card, Your hand, the book): the guide speaks over it, and goes on from where it was when it closes
            if (_treeOn && _guide != GTree)
            {
                _guideTreeFrom = _guide;
                _guideAfterTree = _guide == GImpact ? GTime : _guide == GHand ? GEnd : _guide;
                _guide = GTree;
                return;
            }
            switch (_guide)
            {
                case GPoint:
                case GClick:
                    // a person drawn in the open: night or nobody out, the hours run on to morning (below the speed at which the camera rises)
                    if (_guidePid == 0 || !w.People.Exists(_guidePid) || !w.People.Get(_guidePid).Alive || !Presence.Of(w, in w.People.Get(_guidePid), Host.RenderTime, out _, out _, out _, out _))
                    {
                        int found = GuidePerson(w, _guideSid);
                        if (found != 0)
                        {
                            _guidePid = found;
                            if (_guideRaised || Host.SpeedIndex > GuidePace || Host.MaxSpeed) { Host.MaxSpeed = false; Host.SpeedIndex = GuidePace; _guideRaised = false; }
                            Presence.Of(w, in w.People.Get(found), Host.RenderTime, out var d, out _, out _, out _);
                            if (!Rig.Flying) Rig.FlyTo(d, 75.0, 2.6f);
                            if (_guideTitleText == "Night has fallen") GuidePointWords(_guide);   // (morning: the step's own words again)
                        }
                        else if (Host.SpeedIndex < 5)
                        {
                            Host.SpeedIndex = 5; _guideRaised = true;
                            _guideTitleText = "Night has fallen";
                            _guideText = "Its people are asleep. The hours run on to morning.";
                        }
                    }
                    else
                    {
                        if (Host.SpeedIndex > GuidePace) Host.SpeedIndex = GuidePace;   // (slow enough to point at someone walking)
                        // the camera keeps with them
                        if (!Rig.Flying && Presence.Of(w, in w.People.Get(_guidePid), Host.RenderTime, out var d, out _, out _, out _)) { Rig.Lat = d.Latitude; Rig.Lon = d.Longitude; }
                    }
                    if (_guide == GPoint && _hoverPid != 0 && _guidePid != 0) _guide = GClick;
                    if (_selected != 0) _guide = GCard;
                    break;
                case GCard:
                    if (_selected == 0) _guide = GClick;
                    else if (_personPage) _guide = GLife;
                    break;
                case GLife:
                    // (they are followed: a moment to see the camera take them up, then on to their village)
                    if (_follow != 0 && _follow != _guideFollowWas) { if (_guideFollowAt < _guideAt) _guideFollowAt = Time.unscaledTime; else if (Time.unscaledTime - _guideFollowAt > 1.6f) _guide = GPlace; }
                    break;
                case GPlace:
                    if (_placeSid != 0 && _placeTab == PlaceWhyTab) _guide = GWhy;
                    break;
                case GBook:
                    if (_book) _guide = GBookOpen;
                    break;
                case GSky:
                case GGround:
                case GLand:
                case GCast:
                    if (_castOk && _castNoteAt > _guideAt) { _guideCast = true; _guide = GImpact; break; }
                    if (_guide == GCast) GuideCastWords(w, false);
                    break;
                case GImpact:
                    if (_impactPower >= 0 && Time.unscaledTime - _impactAt > ImpactShow - 3f) _impactAt = Time.unscaledTime - ImpactShow + 3f;   // (kept while the step speaks of it)
                    break;
                case GSkip:
                    // (the skip from the card or from the bar's own menu: on when the camera is back, a leaf of what the years did made)
                    if (!Rig.LapseActive && !Host.CatchingUp && (_guideWaitsLapse || (_lapseSummary != null && _lapseSummary.Count > 0 && _lapseSummaryAt >= _guideAt))) _guide = GYears;
                    break;
                case GYears:
                    if (_lapseSummary != null) { _guideSawLeaf = true; if (Time.unscaledTime - _lapseSummaryAt > 45f) _lapseSummaryAt = Time.unscaledTime - 45f; }   // (kept while read)
                    else if (_guideSawLeaf) _guide = GHand;   // (closed by the player)
                    break;
                case GHand:
                    _handShowUntil = Mathf.Max(_handShowUntil, Time.unscaledTime + 2f);   // (its leaf open while the step speaks of it)
                    if ((_hand.Count > 0) != _guideHandHad) { _guideHandHad = _hand.Count > 0; _guideText = GuideHandWords(); }
                    break;
                case GTree:
                    if (!_treeOn) _guide = _guideAfterTree;
                    break;
            }
        }

        /// <summary>Next: the step after this one (some steps show what they spoke of on the way).</summary>
        void GuideNext()
        {
            var w = Host.World;
            switch (_guide)
            {
                case GPoint:
                case GClick:
                    _personPage = false; _selected = 0; _guide = GPlace; break;
                case GCard:
                    if (_selected != 0 && w != null && w.People.Exists(_selected)) { _personPage = true; _pageTab = 0; _pgBack.Clear(); _placeSid = 0; AudioView.Ui("page"); }
                    _guide = GLife; break;
                case GCast:
                    _guide = _guideCast ? GImpact : GTime; break;
                case GImpact:
                    _guide = GTime; break;
                case GHand:
                    if (_hand.Count > 0) { _guideTreeFrom = GHand; _guideAfterTree = GEnd; _guide = GTree; OpenConsequences(_hand[0].id); }   // (shown: the newest cast's tree)
                    else _guide = GEnd;
                    break;
                case GTree:
                    CloseConsequences(); _guide = _guideAfterTree; break;
                default:
                    _guide = Mathf.Min(GuideLast, _guide + 1); break;
            }
        }

        /// <summary>The step Back goes to (-1: none - the steps that hang on someone chosen).</summary>
        int GuideBack()
        {
            switch (_guide)
            {
                case GWorld: case GClick: case GCard: case GLife: return -1;
                case GPlace: return GEye;
                case GImpact: case GTime: return GCast;
                case GHand: return GSkip;
                case GTree: return _guideTreeFrom;
                case GEnd: return GHand;
                default: return _guide - 1;
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

        /// <summary>The guide's card and ring (the HUD's OnGUI, over everything but the tag; over the tree while it holds the screen - its clicks taken before the
        /// tree's, its picture after: Hud.cs).</summary>
        void GuideDraw(TC2.Core.Simulation.World w, float sw, float sh)
        {
            if (_guide < 0) { _guideCard = Rect.zero; return; }
            GuideStyles();
            bool inTree = _treeOn && !_book && !_menuOn;
            // what it points at
            Rect target = Rect.zero; bool paper = false;
            if (!inTree)
                switch (_guide)
                {
                    case GCard: target = _selected != 0 && !_personPage ? _lifeBtnRect : Rect.zero; paper = true; break;
                    case GLife: target = _personPage && _pageRect.width > 0f ? _followBtnRect : Rect.zero; paper = true; break;
                    case GPlace: target = _placeSid != 0 && _pageRect.width > 0f ? _whyTabRect : Rect.zero; paper = true; break;
                    case GBook: target = _bookBtn; break;
                    case GRealms: target = _realmsBtn; break;
                    case GSky: case GGround: case GLand: target = PowerGroupRect(_guide - GSky); break;
                    case GCast: target = _aim < 0 ? _powersRect : Rect.zero; break;   // (while aiming, the ring on the ground is the mark)
                    case GImpact: target = _impactRect; break;
                    case GTime: target = _left.width > 0f ? Rect.MinMaxRect(_pauseRect.x, _left.y + 4f, _speedRect.xMax + 110f, _left.yMax - 4f) : Rect.zero; break;
                    case GSkip: target = _skipMenu ? _skipRect : _skipBtn; break;
                    case GYears: target = _summaryRect; paper = true; break;
                    case GHand: target = GuideHandOpen() && _handFirstRect.width > 0f && _handRect.Overlaps(_handFirstRect) ? _handFirstRect : _handRect; break;
                }
            if (!inTree && (_guide == GPoint || _guide == GClick) && _guidePid != 0 && w.People.Exists(_guidePid) && Rig != null && Presence.Of(w, in w.People.Get(_guidePid), Host.RenderTime, out var d, out _, out _, out _))
            {
                Vector3 sp = Rig.Cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, d));
                if (sp.z > 0f) { float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale; target = new Rect(x - 13f, y - 25f, 26f, 32f); }
            }
            _guideTarget = target;
            if (target.width > 0f) Halo(target, paper);
            // the card: lower left, clear of the power bar; beside a page, the years' leaf or Your hand's leaf; low while the book is open; over the tree, at its
            // lower right
            float cw = Mathf.Min(430f, sw - 24f);
            float inner = cw - 44f;
            bool action = _guide == GSkip && !Host.CatchingUp && !(Rig != null && Rig.LapseActive);   // (a row of its own over the foot: "Skip a year")
            float h1 = _guideHead.CalcHeight(new GUIContent(_guideTitleText), inner), h2 = _guideBody.CalcHeight(new GUIContent(_guideText), inner);
            float ch = 14f + h1 + 6f + h2 + 12f + (action ? 38f : 0f) + 34f + 16f;
            float cx, cy;
            if (inTree) { cx = Mathf.Max(6f, sw - cw - 48f); cy = sh - ch - 62f; }
            else
            {
                cy = (_powersRect.width > 0f ? _powersRect.y : sh) - ch - 14f;
                if (_book) cy = sh - ch - 12f;
                cx = 14f;
                if (_pageRect.width > 0f && _pageRect.x < cx + cw + 8f) cx = Mathf.Max(6f, _pageRect.x - cw - 8f);
                if (_summaryRect.width > 0f && !_book) cx = Mathf.Min(_summaryRect.xMax + 14f, sw - cw - 8f);
                if (GuideHandOpen() && !_book) cx = Mathf.Max(cx, Mathf.Min(_handRect.xMax + 14f, sw - cw - 8f));
            }
            _guideCard = new Rect(cx, Mathf.Max(70f, cy), cw, ch);
            GUI.Box(_guideCard, GUIContent.none, _box);
            GUI.Label(new Rect(_guideCard.x + 22f, _guideCard.y + 14f, inner, h1), _guideTitleText, _guideHead);
            GUI.Label(new Rect(_guideCard.x + 22f, _guideCard.y + 20f + h1, inner, h2), _guideText, _guideBody);
            float by = _guideCard.yMax - 44f;
            // in the skip's step: a year, from the card itself as well
            _guideSkipYearRect = action ? new Rect(_guideCard.x + 22f, by - 38f, 124f, 30f) : Rect.zero;
            if (action && GUI.Button(_guideSkipYearRect, "Skip a year", _btn)) { AudioView.Ui("click"); Host.BeginSkip(SimTime.Year); _guideWaitsLapse = Host.CatchingUp; }
            // its foot: the steps, Skip, Back, Next
            GUI.Label(new Rect(_guideCard.x + 22f, by + 2f, 70f, 28f), (_guide + 1) + " of " + (GuideLast + 1), _guideStep);
            if (GUI.Button(new Rect(_guideCard.x + 92f, by, 92f, 30f), "Skip guide", _btn)) { GuideEnd(false); return; }
            float rx = _guideCard.xMax - 22f;
            string next = _guide >= GuideLast ? "Done" : _guide == GPoint || _guide == GClick ? "Skip this" : "Next";
            float nw = _guide >= GuideLast ? 80f : 86f;
            _guideNextRect = new Rect(rx - nw, by, nw, 30f);
            int back = GuideBack();
            if (GUI.Button(_guideNextRect, next, _btn))
            {
                if (_guide >= GuideLast) { AudioView.Ui("chime"); GuideEnd(true); return; }
                GuideNext();
            }
            else if (back >= 0 && GUI.Button(new Rect(rx - nw - 68f, by, 62f, 30f), "Back", _btn)) { if (_guide == GTree) CloseConsequences(); _guide = back; }
            // over the tree the card is its own: a click or a drag on it is not the tree's
            if (inTree)
            {
                var ev = Event.current;
                if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseUp || ev.type == EventType.MouseDrag || ev.type == EventType.ScrollWheel) && _guideCard.Contains(ev.mousePosition)) ev.Use();
            }
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
            Hint("why", _placeSid != 0 && _placeTab != PlaceWhyTab, 6f, "Open Why? to trace how this place came to be.");
            Hint("book", _lapseSummary != null && _lapseSummary.Count > 0, 4f, "The years you skipped are written down: open the History Book.");
            Hint("hand", _hand.Count > 0, 3f, "Your hand shows what came of what you did - open it to follow it.");
            Hint("skip", _playedFor > 150f && !_everSkipped, 0f, "Skip decades, then read what changed.");
            Hint("power", _playedFor > 240f && !_everCast, 0f, "Choose a power below, then click the ground - and see what the world does.");
        }

        /// <summary>The hint now said: a line in the book's italic above the power bar, with a small gold mark.</summary>
        void HintDraw(float sw, float sh)
        {
            _hintRect = Rect.zero;
            if (_hintShown == null) return;
            if (_book || _guide >= 0) return;
            if (_hintStyle == null) { _hintStyle = new GUIStyle(_mapSub) { fontSize = 17, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = false }; _hintStyle.normal.textColor = new Color(1f, 0.90f, 0.66f); }
            float floor = _powersRect.width > 0f ? _powersRect.y : sh;
            float a = Mathf.Clamp01((Time.unscaledTime - _hintAt) / 0.5f) * Mathf.Clamp01((9f - (Time.unscaledTime - _hintAt)) / 1.5f);
            var old = GUI.color; GUI.color = new Color(old.r, old.g, old.b, a);
            float tw = _hintStyle.CalcSize(new GUIContent(_hintShown)).x + 34f;
            // (centred in the room an open page leaves, or not said over it: s31b extra_far_click)
            float room = _pageRect.width > 0f ? _pageRect.x : sw;
            if (tw > room - 16f) { GUI.color = old; return; }
            _hintRect = new Rect((room - tw) * 0.5f, floor - 72f, tw, 38f);
            GUI.Box(_hintRect, GUIContent.none, _panelSoft);
            GUI.Label(_hintRect, _hintShown, _hintStyle);
            GUI.color = old;
        }
        GUIStyle _hintStyle; Rect _hintRect;
    }
}
