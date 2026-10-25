using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (F11.1) The opening: a title over the world when a player opens the game (a plain address - the tools' and tests' parameters skip it), the world
    /// held still beneath it until they go in; continue the world (or begin it), begin a new world (asked twice: it replaces the one kept), and how to watch
    /// and reach in. The view writes nothing: the speed is the host's, a new world the host's own.
    /// </summary>
    public sealed partial class Hud
    {
        bool _titleAsked, _titleOn, _titleGuide, _titleNew;
        int _titleSpeed = 3;
        GUIStyle _titleBig, _titleSubtitle, _titleText, _titleBtn;

        static readonly string[] Skips = { "speed", "fresh", "seed", "absence", "view", "book", "test", "menu" };

        /// <summary>Whether to open on the title: a plain address (or `?menu=1`), not the tools' shots.</summary>
        static bool WantsTitle()
        {
            if (Browser.Param("menu") == "1") return true;
            if (ShotRunner.Arg("-tc2shots") != null) return false;
            for (int i = 0; i < Skips.Length; i++) if (Browser.Param(Skips[i]) != null) return false;
            return true;
        }

        /// <summary>The title (the HUD's OnGUI, after the catch-up screen): true while it covers the world.</summary>
        bool Title(float sw, float sh)
        {
            if (!_titleAsked) { _titleAsked = true; _titleOn = WantsTitle(); if (_titleOn) { _titleSpeed = Host.SpeedIndex == 0 ? 3 : Host.SpeedIndex; Host.SpeedIndex = 0; } }
            if (!_titleOn) return false;
            if (_titleBig == null)
            {
                _titleBig = new GUIStyle(_label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                _titleBig.normal.textColor = new Color(0.97f, 0.93f, 0.82f);
                _titleSubtitle = new GUIStyle(_label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                _titleSubtitle.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
                _titleText = new GUIStyle(_label) { fontSize = 14 };
                _titleBtn = new GUIStyle(_btn) { fontSize = 16, padding = new RectOffset(18, 18, 8, 8) };
            }
            var w = Host.World;
            float bw = Mathf.Min(620f, sw - 40f), bh = _titleGuide ? Mathf.Min(560f, sh - 40f) : 300f;
            var r = new Rect((sw - bw) * 0.5f, (sh - bh) * 0.5f, bw, bh);
            GUILayout.BeginArea(r, _page);
            GUILayout.Label("Tiny Consequences", _titleBig, GUILayout.Height(60));
            GUILayout.Label("A living miniature world. Its people farm, build, trade, fight and remember on their own; you may watch, read its history, or reach in.", _titleSubtitle);
            GUILayout.Space(8);
            if (w != null)
                GUILayout.Label((Host.Continued ? "Your world, " : "A new world, ") + SimTime.Format(w.Now) + ": " + w.LivingPeople() + " people in " + w.InhabitedSettlements() + " places.", _titleSubtitle);
            GUILayout.Space(14);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Host.Continued ? "Continue" : "Begin", _titleBtn)) { _titleOn = false; Host.SpeedIndex = _titleSpeed; }
            if (!_titleNew) { if (GUILayout.Button("A new world", _titleBtn)) _titleNew = true; }
            else if (GUILayout.Button("Yes - the world kept now will be lost", _titleBtn)) { _titleNew = false; Host.NewWorld(w != null ? w.Seed + 1 : 42UL); Host.Continued = false; }
            if (GUILayout.Button(_titleGuide ? "Hide the guide" : "How to watch", _titleBtn)) _titleGuide = !_titleGuide;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (_titleGuide)
            {
                GUILayout.Space(10);
                GUILayout.Label("<b>Look</b>: drag to turn the planet, the wheel to come down to a village or rise to the whole world. <b>Time</b>: 1-8 or the bar at the top set the speed; Space holds the world still; 9 lets it run as fast as it can. The world lives on while you are away and catches up when you come back.", _titleText);
                GUILayout.Label("<b>Its people</b>: click a person or a house to see who they are and follow a life; a village's name opens its page. <b>B</b> opens the History Book - every line in it happened, and each tells what caused it. <b>R</b> shows the realms, <b>N</b> the names, <b>H</b> hides all of this.", _titleText);
                GUILayout.Label("<b>Reach in</b>: the powers at the bottom - lightning, a storm, a quake, an eruption, a sickness, a blight. Choose one, then click the ground; the right button stops. Each comes back in the world's time. The world does not exist to answer you: what follows is its own doing, and the book's last part, Your Actions, tells what came of yours.", _titleText);
            }
            GUILayout.EndArea();
            PointerOverUi = true;
            return true;
        }
    }
}
