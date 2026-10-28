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
        Vec3d _wayProbe; bool _wayProbeOn;   // (session 29: tests - a point on the greatest place's busiest way, to point at it as a player would)

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
                case "home":
                    {
                        // (session 27) tests: "home SID|big [ALT]" - the camera over a place's first household's home (pointed at, the house is marked and
                        // its family ringed)
                        var w = Host.World; int sid = 0;
                        if (a.Length > 1 && a[1] != "big") sid = int.Parse(a[1], inv);
                        else { int most = -1; for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most) { most = s.Population; sid = id; } } }
                        if (sid == 0 || !w.Settlements.Exists(sid)) break;
                        ref var st = ref w.Settlements.Get(sid);
                        int hh = st.HeadHh, bid = hh != 0 && w.Households.Exists(hh) ? w.Households.Get(hh).Building : 0;
                        if (bid != 0 && w.Buildings.Exists(bid)) { ref var b = ref w.Buildings.Get(bid); Rig.LookAt(st.LocalToSphere(b.X, b.Y, w.Planet.Radius), a.Length > 2 ? double.Parse(a[2], inv) : 120); }
                        break;
                    }
                case "new": Host.NewWorld(ulong.Parse(a[1], inv)); break;
                case "view": Rig.SetView(a[1]); break;   // the camera's named views (forest, forestlow, coast, volcano...: D2-079)
                case "skip": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.Skip(double.Parse(a[1], inv)); break; }   // (session 29) "skip YEARS": the bar's skip ahead
                case "stopskip": Host.StopSkip(); break;
                case "catchupalt": CameraRig.CatchUpAltitude = double.Parse(a[1], CultureInfo.InvariantCulture); break;   // tools: the camera's height while catching up
                case "book": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.OpenBook(a.Length > 1 ? int.Parse(a[1], inv) : -1); break; }   // the History Book (D2-077)
                case "bookat": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.OpenBookAt(int.Parse(a[1], inv), a.Length > 2 ? int.Parse(a[2], inv) : 0); break; }   // (session 25) a part and chapter (-1: the preface)
                case "aim": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.Aim(int.Parse(a[1], inv)); break; }   // tests: aim a power as its button does (F9.1)
                case "closebook": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.CloseBook(); break; }
                case "closepages": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.ClosePages(); break; }   // (the book and every page closed)
                case "info": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.Info(a.Length < 2 || a[1] == "on"); break; }   // (session 29) the world now
                case "history": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.HistoryPanel(a.Length < 2 || a[1] == "on"); break; }   // (session 29) the History panel
                case "feedline": { var hud = Object.FindFirstObjectByType<Hud>(); if (hud != null) hud.OpenFeedLine(a.Length > 1 ? int.Parse(a[1], inv) : 0); break; }   // (session 29) a feed line opened in the book
                case "building":
                    {
                        // (session 29) tests: "building SID|big [KIND]" - a work of a place (its smithy, else its first granary, else its first home) as a click on it opens it
                        var hud = Object.FindFirstObjectByType<Hud>(); var w = Host.World; int sid = 0;
                        if (a.Length > 1 && a[1] != "big") sid = int.Parse(a[1], inv);
                        else { int most = -1; for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most) { most = s.Population; sid = id; } } }
                        if (sid == 0 || hud == null) break;
                        int want = a.Length > 2 ? int.Parse(a[2], inv) : TC2.Core.Places.Structures.Smithy, pick = 0;
                        var bl = w.BuildingsOf(sid);
                        for (int pass = 0; pass < 3 && pick == 0; pass++)
                            for (int i = 0; i < bl.Count && pick == 0; i++)
                            {
                                ref var b = ref w.Buildings.Get(bl[i]);
                                if (pass == 0 && b.Kind == want && b.Done != 0) pick = bl[i];
                                else if (pass == 1 && b.Kind == TC2.Core.Places.Structures.Granary && b.Done != 0) pick = bl[i];
                                else if (pass == 2 && TC2.Core.Places.Structures.IsHome(b.Kind) && b.Household != 0) pick = bl[i];
                            }
                        if (pick != 0) { hud.ShowBuilding(pick); ref var pb = ref w.Buildings.Get(pick); ref var ps = ref w.Settlements.Get(sid); Rig.LookAt(ps.LocalToSphere(pb.X, pb.Y, w.Planet.Radius), 140); }
                        break;
                    }
                case "way":
                    {
                        // (session 29) tests: "way big [ALT] [AT]" - the camera over the greatest place's busiest worn way, AT (0.4) of the way out along its
                        // route from the place; the point's screen position in the state (wayX/wayY/wayFront) to point at it (Hud.HoverWay)
                        var w = Host.World; int sid = 0, most = -1;
                        for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most) { most = s.Population; sid = id; } }
                        _wayProbeOn = false;
                        if (sid == 0) break;
                        var ways = new System.Collections.Generic.List<TC2.Core.Agents.Way>();
                        w.Ways.Collect(w.Now, ways);
                        double bestUse = 0; int other = 0;
                        for (int i = 0; i < ways.Count; i++)
                        {
                            var wy = ways[i];
                            if (wy.A != sid && wy.B != sid) continue;
                            int o = wy.A == sid ? wy.B : wy.A;
                            if (!w.Settlements.Exists(o) || w.Routes.Get(w, sid, o) == null) continue;
                            if (wy.Use > bestUse) { bestUse = wy.Use; other = o; }
                        }
                        if (other == 0) break;
                        var route = w.Routes.Get(w, sid, other);
                        double at = a.Length > 3 ? double.Parse(a[3], inv) : 0.4;
                        _wayProbe = route.PointAt(route.A == sid ? route.Length * at : route.Length * (1.0 - at));
                        _wayProbeOn = true;
                        Rig.LookAt(_wayProbe, a.Length > 2 && a[2] != "-" ? double.Parse(a[2], inv) : 450);
                        break;
                    }
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
                        if (a.Length > 1 && a[1] == "head") { int most = -1; for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most && s.Head != 0) { most = s.Population; pid = s.Head; } } }   // (session 29: the first of the greatest place)
                        else if (a.Length > 1 && a[1] != "oldest") pid = int.Parse(a[1], inv);
                        else { double best = -1; for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.AgeYears(w.Now) > best) { best = p.AgeYears(w.Now); pid = id; } } }
                        if (hud != null && pid != 0 && w.People.Exists(pid)) hud.ShowPerson(pid, a.Length > 2 ? int.Parse(a[2], inv) : 0);
                        break;
                    }
                case "select":
                    {
                        // (session 29) tests: "select PID|oldest|head" - a person chosen as a click does (their card), the camera near them; "head": the first of the
                        // greatest place
                        var hud = Object.FindFirstObjectByType<Hud>(); var w = Host.World; int pid = 0;
                        if (a.Length > 1 && a[1] == "head") { int most = -1; for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most && s.Head != 0) { most = s.Population; pid = s.Head; } } }
                        else if (a.Length > 1 && a[1] != "oldest") pid = int.Parse(a[1], inv);
                        else { double best = -1; for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.AgeYears(w.Now) > best) { best = p.AgeYears(w.Now); pid = id; } } }
                        if (hud != null && pid != 0 && w.People.Exists(pid)) { hud.Select(pid); if (TC2.Core.Agents.Presence.Of(w, in w.People.Get(pid), Host.RenderTime, out var pd, out _, out _, out _)) Rig.LookAt(pd, 120); }
                        break;
                    }
                case "follow":
                    {
                        // (session 29) tests: "follow PID|oldest" - the camera keeps with a person, as the card's Follow does
                        var hud = Object.FindFirstObjectByType<Hud>(); var w = Host.World; int pid = 0;
                        if (a.Length > 1 && a[1] != "oldest") pid = int.Parse(a[1], inv);
                        else { double best = -1; for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.AgeYears(w.Now) > best && p.At != 0) { best = p.AgeYears(w.Now); pid = id; } } }
                        if (hud != null && pid != 0 && w.People.Exists(pid)) { hud.Follow(pid); if (TC2.Core.Agents.Presence.Of(w, in w.People.Get(pid), Host.RenderTime, out var pd, out _, out _, out _)) Rig.LookAt(pd, a.Length > 2 ? double.Parse(a[2], inv) : 260); }
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
            if (what == "terrainshadow") { TerrainLod.PatchShadows = on; return; }   // (session 27: the ground's patches casting shadows)
            if (what == "recolour") { TerrainLod.RecolourOff = !on; return; }   // (session 27: the ground's colours refreshed as the world changes)
            if (what == "surfacecache") { Ground.SurfaceCacheOff = !on; return; }   // (session 27, context 4: the ground remembered for the day under what stands on it)
            if (what == "terrainbranch") { Shader.SetGlobalFloat("_TC2Unbranched", on ? 0f : 1f); return; }   // (session 27: off runs every block of Terrain.shader everywhere, as before its branches)
            if (what == "roadcache") { TerrainLod.RoadCache = on; return; }   // (session 29: each patch keeps its vertices' roads until the roads change)
            if (what == "peoplestale") { WorldObjectsView.PeopleSkip = on; return; }   // (session 29: people posed again only when time or the camera moved; every third frame from 2 km)
            if (what == "peoplecull") { WorldObjectsView.PeopleCull = on; return; }   // (people beyond the limb and outside the view left unposed: 14 §7)
            if (what == "placescull") { WorldObjectsView.PlacesCull = on; return; }   // (villages beyond the limb left unbuilt: 14 §7)
            if (what == "shadowlite") { Sky.ShadowLite = on; return; }   // (above 3 km two cascades over twice the height: 14 §7, measured before chosen)
            if (what == "sortplaces") { WorldObjectsView.SortPlaces = on; return; }   // (villages built neighbours together, so batches cull: 14 §7, measured before chosen)
            if (what == "cloudshadow") { Look.CloudShadow = on ? 0.6f : 0f; return; }   // (session 27: the clouds' shadows on the ground, the sea and what stands there)
            if (what == "fardetail") { WorldObjectsView.DetailFar = on ? 2200f : 1e9f; return; }   // (session 27: far villages' homes plain and shadowless; off: every village in full)
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
            sb.Append(",\"skipping\":").Append(Host.Skipping ? "true" : "false");
            if (w != null && Rig != null && Rig.Cam != null)
            {
                // (session 29) tests: the most peopled place's square on the screen (px from the top left), to point at it from afar
                int big = 0, most = -1;
                for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) { ref var s = ref w.Settlements.Get(id); if (s.Inhabited && s.Population > most) { most = s.Population; big = id; } }
                if (big != 0)
                {
                    Vector3 sp = Rig.Cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, w.Settlements.Get(big).Anchor));
                    sb.Append(",\"bigX\":").Append(sp.x.ToString("0", inv)).Append(",\"bigY\":").Append((Screen.height - sp.y).ToString("0", inv)).Append(",\"bigFront\":").Append(sp.z > 0 ? "true" : "false");
                }
                if (_wayProbeOn)
                {
                    Vector3 wp = Rig.Cam.WorldToScreenPoint(PlanetGeo.Surface(w.Planet, _wayProbe));
                    sb.Append(",\"wayX\":").Append(wp.x.ToString("0", inv)).Append(",\"wayY\":").Append((Screen.height - wp.y).ToString("0", inv)).Append(",\"wayFront\":").Append(wp.z > 0 ? "true" : "false");
                }
            }
            if (Rig != null) sb.Append(",\"lapse\":\"").Append(Rig.Lapse).Append("\",\"lapseLight\":").Append(Rig.LapseLight.ToString("0.###", inv)).Append(",\"camAlt\":").Append((Rig.Distance - (w != null ? w.Planet.Radius : 0)).ToString("0", inv));   // (session 29: the timelapse)
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
