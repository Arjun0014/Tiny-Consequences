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

        void Start() { Browser.TC2_RegisterPageEvents(); if (GetComponent<FrameClock>() == null) gameObject.AddComponent<FrameClock>(); }

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
                case "catchupalt": CameraRig.CatchUpAltitude = double.Parse(a[1], CultureInfo.InvariantCulture); break;   // tools: the camera's height while catching up
                case "book": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.OpenBook(a.Length > 1 ? int.Parse(a[1], inv) : -1); break; }   // the History Book (D2-077)
                case "bookat": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.OpenBookAt(int.Parse(a[1], inv), a.Length > 2 ? int.Parse(a[2], inv) : 0); break; }   // (session 25) a part and chapter (-1: the preface)
                case "aim": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.Aim(int.Parse(a[1], inv)); break; }   // tests: aim a power as its button does (F9.1)
                case "closebook": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.CloseBook(); break; }
                case "closepages": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.ClosePages(); break; }   // (the book and every page closed)
                case "names": Hud.Names = a.Length < 2 || a[1] == "on"; break;   // (session 25) places named on the map
                case "setting":   // (F11.2) tests: a setting changed as the panel changes it, and kept
                    {
                        float v = float.Parse(a[2], inv);
                        if (a[1] == "clouds") Settings.Clouds = Mathf.Clamp(v, 0f, 1.5f);
                        else if (a[1] == "volume") AudioView.Volume = Mathf.Clamp01(v);
                        else if (a[1] == "muted") AudioView.Muted = v > 0f;
                        else if (a[1] == "lightshadows") Sky.ShadowLite = v > 0f;
                        else if (a[1] == "detail") Settings.Detail = Mathf.Clamp(v, 0.5f, 1f);
                        Settings.Apply(); Settings.Save();
                        break;
                    }
                case "person":
                    {
                        // the person page (C12.1): "person PID [TAB]", or "person oldest" - the oldest living person
                        var hud = Object.FindFirstObjectByType<Hud>(); var w = Host.World; int pid = 0;
                        if (a.Length > 1 && a[1] != "oldest") pid = int.Parse(a[1], inv);
                        else { double best = -1; for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.AgeYears(w.Now) > best) { best = p.AgeYears(w.Now); pid = id; } } }
                        if (hud != null && pid != 0 && w.People.Exists(pid)) hud.ShowPerson(pid, a.Length > 2 ? int.Parse(a[2], inv) : 0);
                        break;
                    }
                case "place":
                    {
                        // the settlement page (C12.2): "place SID [TAB]", or "place big" - the place with the most people
                        var hud = Object.FindFirstObjectByType<Hud>(); var w = Host.World; int sid = 0;
                        if (a.Length > 1 && a[1] != "big") sid = int.Parse(a[1], inv);
                        else { int most = -1; for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most) { most = s.Population; sid = id; } } }
                        if (hud != null && sid != 0 && w.Settlements.Exists(sid)) hud.ShowPlace(sid, a.Length > 2 ? int.Parse(a[2], inv) : 0);
                        break;
                    }
                case "off": case "on": Toggle(a[1], a[0] == "on"); break;   // tools: what a frame costs without a layer
                case "glshadow": Sky.GlCascades = int.Parse(a[1], inv); Sky.GlShadowReach = float.Parse(a[2], inv); break;   // tools: WebGL2 shadows
                case "shadow": Sky.GpuCascades = int.Parse(a[1], inv); Sky.GpuShadowReach = float.Parse(a[2], inv); break;   // tools: WebGPU shadows
                case "cull": Instancer.Cull = a[1] == "on"; break;          // tools: instanced draws with their own bounds (D2-109)
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
            if (what == "realms") { RealmsView.On = on; return; }   // (the realms map layer, D2-167: as the player's R)
            if (what == "names") { Hud.Names = on; return; }        // (places, works and people named: D2-172)
            if (what == "cover") { TerrainLod.CoverOff = !on; return; }   // (the ground cover near the camera: D2-081)
            if (what == "covershadow") { TerrainLod.CoverShadows = on; return; }   // (shrubs' and boulders' shadows)
            if (what == "peoplecull") { WorldObjectsView.PeopleCull = on; return; }   // (people beyond the limb and outside the view left unposed: 14 §7)
            if (what == "placescull") { WorldObjectsView.PlacesCull = on; return; }   // (villages beyond the limb left unbuilt: 14 §7)
            if (what == "shadowlite") { Sky.ShadowLite = on; return; }   // (above 3 km two cascades over twice the height: 14 §7, measured before chosen)
            if (what == "sortplaces") { WorldObjectsView.SortPlaces = on; return; }   // (villages built neighbours together, so batches cull: 14 §7, measured before chosen)
            int part = what == "trees" ? 1 : what == "people" ? 2 : what == "buildings" ? 4 : 0;
            if (part != 0) { WorldObjectsView.Off = on ? WorldObjectsView.Off & ~part : WorldObjectsView.Off | part; return; }
            string prefix = what == "terrain" ? "Patch" : what == "planet" ? "Face" : what == "sea" ? "Sea" : what == "air" ? "Atmosphere" : what == "clouds" ? "Clouds" : what == "rivers" ? "Rivers" : null;
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
                int casts = 0; for (int ci = 0; ci < w.CommandLog.Count; ci++) { var ck = w.CommandLog[ci].Kind; if (ck != CommandKind.SetResolution && ck != CommandKind.SetStepDays && ck != CommandKind.Note) casts++; }
                sb.Append(",\"casts\":").Append(casts);   // (the god's casts so far: F9.1)
                sb.Append(",\"settings\":\"").Append("clouds=").Append(Settings.Clouds.ToString("0.00", CultureInfo.InvariantCulture)).Append(" volume=").Append(AudioView.Volume.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append(" muted=").Append(AudioView.Muted ? 1 : 0).Append(" lightshadows=").Append(Sky.ShadowLite ? 1 : 0).Append(" detail=").Append(Settings.Detail.ToString("0.00", CultureInfo.InvariantCulture)).Append('"');   // (F11.2)
            }
            sb.Append(",\"speed\":").Append(Host.SpeedIndex);
            sb.Append(",\"max\":").Append(Host.MaxSpeed ? "true" : "false");
            sb.Append(",\"achieved\":").Append(Host.AchievedSpeed.ToString("0.###", inv));
            sb.Append(",\"simMs\":").Append(Host.LastSimMs.ToString("0.###", inv));
            sb.Append(",\"catchingUp\":").Append(Host.CatchingUp ? "true" : "false");
            sb.Append(",\"catchUpTarget\":").Append(Host.CatchUpTarget);
            sb.Append(",\"saveBytes\":").Append(Host.LastSaveBytes);
            sb.Append(",\"saveOk\":").Append(Host.LastSaveOk ? "true" : "false");
            sb.Append(",\"saves\":").Append(Host.SavesStored);   // (saves stored: a tool waits for it to rise after its save)
            sb.Append(",\"gfx\":\"").Append(SystemInfo.graphicsDeviceType).Append('"');
            sb.Append(",\"fps\":").Append((1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0.#", inv));
            sb.Append(",\"cpuMs\":").Append(FrameClock.CpuMs.ToString("0.##", inv));
            sb.Append(",\"draws\":").Append(FrameClock.LastDraws);
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
