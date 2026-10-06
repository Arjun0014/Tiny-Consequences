using System.Globalization;
using System.Text;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The test bridge (15 §1): publishes the world's state to window.__TC2 twice a second and takes commands
    /// from window.__TC2_CMDS (speed N | max on|off | save | away HOURS | look LAT LON ALT | hashtest | new SEED).
    /// "hashtest" builds the reference small world and posts its state hashes, so the browser can be compared
    /// with the headless runtime (D2-003).
    /// </summary>
    public sealed class WebBridge : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        float _t;
        string _hashTest = "";
        BenchRunner _bench;
        string _lastCmd = "";

        void Start() { Browser.TC2_RegisterPageEvents(); }

        void Update()
        {
            for (int k = 0; k < 4; k++)
            {
                string cmd = Browser.TC2_PollCommand();
                if (string.IsNullOrEmpty(cmd)) break;
                Run(cmd);
            }
            _t += Time.unscaledDeltaTime;
            if (_t < 0.5f) return;
            _t = 0;
            Post();
        }

        void Run(string cmd)
        {
            _lastCmd = cmd;
            var a = cmd.Split(' ');
            var inv = CultureInfo.InvariantCulture;
            switch (a[0])
            {
                case "speed": Host.SpeedIndex = int.Parse(a[1], inv); Host.MaxSpeed = false; break;
                case "max": Host.MaxSpeed = a.Length < 2 || a[1] == "on"; break;
                case "save": Host.SaveNow(); break;
                case "away": Host.ReloadAsIfAway((long)(double.Parse(a[1], inv) * 3600000.0)); break;
                case "look":
                    Rig.Lat = double.Parse(a[1], inv) * DMath.Deg2Rad; Rig.Lon = double.Parse(a[2], inv) * DMath.Deg2Rad;
                    Rig.Distance = Host.World.Planet.Radius + double.Parse(a[3], inv);
                    break;
                case "village":
                    {
                        var w = Host.World; int sid = w.Settlements.First();
                        int k = a.Length > 1 ? int.Parse(a[1], inv) : 0;
                        for (int i = 0; i < k && sid != 0; i++) sid = w.Settlements.Next(sid);
                        if (sid != 0) Rig.LookAt(w.Settlements.Get(sid).Anchor, a.Length > 2 ? double.Parse(a[2], inv) : 900);
                        break;
                    }
                case "new": Host.NewWorld(ulong.Parse(a[1], inv)); break;
                case "view": Rig.SetView(a[1]); break;   // the camera's named views (forest, forestlow, coast, volcano...: D2-079)
                case "book": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.OpenBook(a.Length > 1 ? int.Parse(a[1], inv) : -1); break; }   // the History Book (D2-077)
                case "off": case "on": Toggle(a[1], a[0] == "on"); break;   // tools: what a frame costs without a layer
                case "glshadow": Sky.GlCascades = int.Parse(a[1], inv); Sky.GlShadowReach = float.Parse(a[2], inv); break;   // tools: WebGL2 shadows
                case "hashtest": _hashTest = HashTest(); break;
                case "bench":
                    {
                        // bench SETTLEMENTS FOUNDERS YEARS activity|coarse
                        if (_bench == null) _bench = gameObject.AddComponent<BenchRunner>();
                        Host.SpeedIndex = 0; // the player's world waits while the bench runs
                        _bench.Begin(int.Parse(a[1], inv), int.Parse(a[2], inv), double.Parse(a[3], inv), a.Length > 4 && a[4] == "coarse");
                        break;
                    }
            }
        }

        public static string HashTest() => TC2.Core.Diagnostics.CrossRuntime.Probe();

        LightShadows _sunShadows = LightShadows.Soft;

        /// <summary>
        /// Tools (webperf): a drawn layer off or on again - shadows, tilt-shift, the meshes of the terrain, the planet's faces,
        /// the sea, the air or the rivers by name, or any view component by its type name (WorldObjectsView, CloudView, ...).
        /// </summary>
        void Toggle(string what, bool on)
        {
            if (what == "shadows")
            {
                var sun = FindAnyObjectByType<Light>();
                if (sun != null) { if (!on && sun.shadows != LightShadows.None) _sunShadows = sun.shadows; sun.shadows = on ? _sunShadows : LightShadows.None; }
                return;
            }
            if (what == "tilt") { TiltShiftFeature.Off = !on; return; }
            int part = what == "trees" ? 1 : what == "people" ? 2 : what == "buildings" ? 4 : 0;
            if (part != 0) { WorldObjectsView.Off = on ? WorldObjectsView.Off & ~part : WorldObjectsView.Off | part; return; }
            string prefix = what == "terrain" ? "Patch" : what == "planet" ? "Face" : what == "sea" ? "Sea" : what == "air" ? "Atmosphere" : what == "rivers" ? "Rivers" : null;
            if (what == "terrain") foreach (var t in FindObjectsByType<TerrainLod>(FindObjectsSortMode.None)) t.enabled = on;
            if (prefix != null)
            {
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (r.gameObject.name.StartsWith(prefix) || (what == "rivers" && r.transform.parent != null && r.transform.parent.name == "Rivers")) r.enabled = on;
                return;
            }
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (string.Equals(mb.GetType().Name, what, System.StringComparison.OrdinalIgnoreCase)) mb.enabled = on;
        }

        void Post()
        {
            var w = Host.World;
            var sb = new StringBuilder(512);
            var inv = CultureInfo.InvariantCulture;
            sb.Append('{');
            sb.Append("\"ready\":").Append(Host.Ready ? "true" : "false");
            if (w != null)
            {
                sb.Append(",\"seed\":").Append(w.Seed);
                sb.Append(",\"now\":").Append(w.Now);
                sb.Append(",\"date\":\"").Append(SimTime.Format(w.Now).Replace("\"", "'")).Append('"');
                sb.Append(",\"year\":").Append(SimTime.YearOf(w.Now));
                sb.Append(",\"people\":").Append(w.LivingPeople());
                sb.Append(",\"places\":").Append(w.InhabitedSettlements());
                sb.Append(",\"events\":").Append(w.Metrics.Events);
                sb.Append(",\"history\":").Append(w.History.Count);
                sb.Append(",\"fires\":").Append(w.Fire.Active.Count);
                sb.Append(",\"interrupted\":").Append(w.Metrics.JourneysInterrupted);
            }
            sb.Append(",\"speed\":").Append(Host.SpeedIndex);
            sb.Append(",\"max\":").Append(Host.MaxSpeed ? "true" : "false");
            sb.Append(",\"achieved\":").Append(Host.AchievedSpeed.ToString("0.###", inv));
            sb.Append(",\"simMs\":").Append(Host.LastSimMs.ToString("0.###", inv));
            sb.Append(",\"catchingUp\":").Append(Host.CatchingUp ? "true" : "false");
            sb.Append(",\"catchUpTarget\":").Append(Host.CatchUpTarget);
            sb.Append(",\"saveBytes\":").Append(Host.LastSaveBytes);
            sb.Append(",\"saveOk\":").Append(Host.LastSaveOk ? "true" : "false");
            sb.Append(",\"gfx\":\"").Append(SystemInfo.graphicsDeviceType).Append('"');
            sb.Append(",\"fps\":").Append((1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0.#", inv));
            sb.Append(",\"hashtest\":\"").Append(_hashTest).Append('"');
            sb.Append(",\"bench\":\"").Append(_bench != null ? _bench.Result : "").Append('"');
            sb.Append(",\"lastCmd\":\"").Append(_lastCmd.Replace("\"", "'")).Append('"');
            sb.Append(",\"feed\":[");
            int from = Mathf.Max(0, Host.Feed.Count - 8);
            for (int i = from; i < Host.Feed.Count; i++) { if (i > from) sb.Append(','); sb.Append('"').Append(Host.Feed[i].Replace("\\", "/").Replace("\"", "'")).Append('"'); }
            sb.Append("]}");
            Browser.TC2_PostState(sb.ToString());
        }
    }
}
