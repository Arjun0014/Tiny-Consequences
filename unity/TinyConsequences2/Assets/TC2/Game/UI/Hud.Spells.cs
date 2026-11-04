using TC2.Core.Foundation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (session 32: the slow powers read on the world) The words SpellView sets on the land - "Drought - 12 days left" at the heart of a drought, a blight's
    /// share of the crop at its village, the god's copper no one has seen - in the map's italic with a dark edge, in the power's colour, a little under the
    /// point so a village's name keeps its place; none over the horizon, behind the camera or under a panel. Reads the view's list; writes nothing.
    /// </summary>
    public sealed partial class Hud
    {
        GUIStyle _spellWord;
        readonly System.Collections.Generic.List<Rect> _spellPlaced = new System.Collections.Generic.List<Rect>();

        void SpellNames(TC2.Core.Simulation.World w, float sw, float sh)
        {
            var labels = SpellView.Labels;
            if (labels.Count == 0 || Rig == null || Rig.Cam == null) return;
            if (_spellWord == null) { _spellWord = new GUIStyle(_mapSub) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = false }; }
            var cam = Rig.Cam;
            var P = w.Planet;
            Vector3 camPos = cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            _spellPlaced.Clear();
            for (int i = 0; i < _uiRects.Count; i++) if (_uiRects[i].width > 0f) _spellPlaced.Add(_uiRects[i]);
            if (alt >= 420f) for (int i = 0; i < _placed.Count; i++) _spellPlaced.Add(_placed[i]);   // (the places' names this frame: MapNames)
            for (int i = 0; i < labels.Count && i < 12; i++)
            {
                var l = labels[i];
                Vector3 at = PlanetGeo.Surface(P, l.Dir), up = PlanetGeo.ToVector(l.Dir);
                if (Vector3.Dot(up, (camPos - at).normalized) < 0.10f) continue;   // (over the horizon)
                Vector3 sp = cam.WorldToScreenPoint(at);
                if (sp.z <= 0f) continue;
                float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
                if (x < -100f || x > sw + 100f || y < -40f || y > sh + 40f) continue;
                Vector2 size = _spellWord.CalcSize(new GUIContent(l.Text));
                var r = new Rect(x - size.x * 0.5f - 2f, y + 22f, size.x + 4f, size.y);
                // (a little lower while it would lie over a name or another word; under a panel, not at all)
                for (int tries = 0; tries < 3; tries++)
                {
                    bool over = false;
                    for (int k = 0; k < _spellPlaced.Count && !over; k++) if (_spellPlaced[k].Overlaps(r)) over = true;
                    if (!over) break;
                    r.y += size.y + 2f;
                }
                bool hidden = false;
                for (int k = 0; k < _uiRects.Count && !hidden; k++) if (_uiRects[k].width > 0f && _uiRects[k].Overlaps(r)) hidden = true;
                if (hidden) continue;
                _spellPlaced.Add(r);
                _spellWord.normal.textColor = l.Col;
                Edged(r, l.Text, _spellWord);
            }
        }
    }
}
