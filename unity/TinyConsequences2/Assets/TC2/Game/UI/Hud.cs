using System.Globalization;
using System.Text;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Foundation HUD (IMGUI: no assets, works on every platform), kept out of the way of the world: a slim bar with
    /// the date and the speeds, a one-line ticker of what just happened, and panels behind toggles (Info: the world in
    /// numbers; History: the recent feed; World: saves, absences, new worlds) - H hides everything. The catch-up screen
    /// and a person inspector with their "why". Reads the world; actions go through the host. F7 replaces this.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        public WorldObjectsView Objects;
        public static bool PointerOverUi;
        int _selected;
        GUIStyle _box, _label, _small, _title, _btn;
        float _scale = 1f;
        Rect _left, _right, _bottom, _toggles, _ticker;
        float _fps; int _frames; float _fpsT;
        bool _info, _history, _worldPanel, _hidden;
        string _news = ""; float _newsAge = 99f; int _newsCount;

        void Update()
        {
            _frames++; _fpsT += Time.unscaledDeltaTime;
            if (_fpsT > 0.5f) { _fps = _frames / _fpsT; _frames = 0; _fpsT = 0; }
            var w = Host.World;
            if (w == null || Host.CatchingUp) return;
            if (Input.GetKeyDown(KeyCode.Space)) Host.SpeedIndex = Host.SpeedIndex == 0 ? 3 : 0;
            if (Input.GetKeyDown(KeyCode.H)) _hidden = !_hidden;
            if (Host.Feed.Count != _newsCount) { _newsCount = Host.Feed.Count; _news = _newsCount > 0 ? Host.Feed[_newsCount - 1] : ""; _newsAge = 0f; }
            _newsAge += Time.unscaledDeltaTime;
            for (int i = 0; i < SimHost.Speeds.Length && i < 8; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Host.SpeedIndex = i; Host.MaxSpeed = false; }
            if (Input.GetKeyDown(KeyCode.Alpha9)) Host.MaxSpeed = !Host.MaxSpeed;
            if (Input.GetMouseButtonUp(0) && !PointerOverUi) Pick();
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
                if (!p.Alive || p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant) continue;
                Vector3 pos = PlanetGeo.Surface(w.Planet, w.Agents.PositionAt(p, t));
                Vector3 sp = cam.WorldToScreenPoint(pos);
                if (sp.z < 0) continue;
                float d = Vector2.Distance(mp, sp);
                if (d < best) { best = d; bestId = id; }
            }
            if (bestId != 0) _selected = bestId;
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
        }

        void OnGUI()
        {
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            float sw = Screen.width / _scale, sh = Screen.height / _scale;
            var w = Host.World;
            if (w == null) { GUI.Label(new Rect(20, 20, 600, 40), Host.StatusLine, _title); return; }
            if (Host.CatchingUp) { CatchUpScreen(sw, sh); PointerOverUi = true; return; }

            if (_hidden) { PointerOverUi = false; return; }
            // top-left: a slim bar - the date and the speeds
            _left = new Rect(10, 10, Mathf.Min(sw - 270, 900), 40);
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
            _toggles = new Rect(sw - 250, 10, 240, 40);
            GUILayout.BeginArea(_toggles, _box);
            GUILayout.BeginHorizontal();
            _info = GUILayout.Toggle(_info, "Info", _btn);
            _history = GUILayout.Toggle(_history, "History", _btn);
            _worldPanel = GUILayout.Toggle(_worldPanel, "World", _btn);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            _right = Rect.zero;
            float ry = 56;
            if (_info)
            {
                _right = new Rect(sw - 340, ry, 330, 110);
                GUILayout.BeginArea(_right, _box);
                GUILayout.Label("<b>" + w.LivingPeople() + "</b> people in <b>" + w.InhabitedSettlements() + "</b> places   fires " + w.Fire.Active.Count, _label);
                var P = w.Planet;
                string sea = (P.SeaLevelNow - P.SeaLevel0).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
                GUILayout.Label("climate " + P.TGlobal.ToString("0.0", CultureInfo.InvariantCulture) + " °C   ice " + (P.IceVolume / 1e9).ToString("0.0", CultureInfo.InvariantCulture) + " km³   sea " + sea + " m   tilt " + (P.Tilt * 180 / System.Math.PI).ToString("0.0", CultureInfo.InvariantCulture) + "°" + (P.Glacial == 1 ? "   (an age of ice)" : ""), _small);
                GUILayout.Label("achieved " + FormatSpeed(Host.AchievedSpeed) + "   sim " + Host.LastSimMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms   " + _fps.ToString("0", CultureInfo.InvariantCulture) + " fps   " + SystemInfo.graphicsDeviceType, _small);
                GUILayout.Label("events " + w.Metrics.Events.ToString("N0", CultureInfo.InvariantCulture) + "   history " + w.History.Count + "   drawn " + Objects.PeopleDrawn + " people, " + Objects.TreesDrawn + " trees", _small);
                GUILayout.EndArea();
                ry += 116;
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
            }

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

            // inspector
            Rect insp = new Rect(sw - 400, 190, 390, 300);
            bool showInsp = _selected != 0 && w.People.Exists(_selected);
            if (showInsp) Inspector(insp, w);
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / _scale;
            PointerOverUi = _left.Contains(m) || _toggles.Contains(m) || _right.Contains(m) || worldRect.Contains(m) || _bottom.Contains(m) || _ticker.Contains(m) || (showInsp && insp.Contains(m));
        }

        void Inspector(Rect r, TC2.Core.Simulation.World w)
        {
            ref var p = ref w.People.Get(_selected);
            var sb = new StringBuilder();
            string name = w.History.NameOf(EntityRef.Person(p.Id));
            sb.Append("<b>").Append(name).Append("</b>, ").Append(p.AgeYears(w.Now).ToString("0", CultureInfo.InvariantCulture)).Append(p.Alive ? "" : " (died)").Append('\n');
            sb.Append("home ").Append(w.History.NameOf(EntityRef.Settlement(p.Home))).Append(p.At == 0 ? ", on the road" : (p.At != p.Home ? ", visiting " + w.History.NameOf(EntityRef.Settlement(p.At)) : "")).Append('\n');
            sb.Append("doing <b>").Append(p.Act).Append("</b> because ").Append(p.Why).Append('\n');
            sb.Append("until ").Append(SimTime.FormatShort(p.T1)).Append("   health ").Append(p.Health.ToString("0.00", CultureInfo.InvariantCulture)).Append("   fed ").Append(p.Nutrition.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            if (p.Partner != 0) sb.Append("partner ").Append(w.History.NameOf(EntityRef.Person(p.Partner))).Append('\n');
            sb.Append("skills: farming ").Append(p.SkFarm.ToString("0.00", CultureInfo.InvariantCulture)).Append(", wood ").Append(p.SkWood.ToString("0.00", CultureInfo.InvariantCulture)).Append("   trips ").Append(p.Visits).Append('\n');
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
            if (GUILayout.Button("Follow", _btn)) Rig.LookAt(w.Agents.PositionAt(p, Host.RenderTime), 260);
            GUILayout.EndArea();
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

        static string FormatSpeed(double s)
        {
            if (s >= SimTime.Year * 0.5) return (s / SimTime.Year).ToString("0.0", CultureInfo.InvariantCulture) + " years/s";
            if (s >= SimTime.Day) return (s / SimTime.Day).ToString("0.0", CultureInfo.InvariantCulture) + " days/s";
            if (s >= SimTime.Hour) return (s / SimTime.Hour).ToString("0.0", CultureInfo.InvariantCulture) + " h/s";
            return s.ToString("0", CultureInfo.InvariantCulture) + "x";
        }
    }
}
