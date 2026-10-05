using System;
using System.Collections;
using System.Globalization;
using System.IO;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Look-dev screenshots of the real game (13 §4): started by the batch editor in Play Mode with
    /// <c>-tc2shots DIR [-tc2seed N] [-tc2hour H] [-tc2days D] [-tc2views orbit,region,village,close,...] [-tc2size 1600x900]</c>.
    /// Makes a fresh world, runs it to the wanted local hour at the first village, frames each view, lets the views
    /// settle, renders the camera into a texture and writes PNGs, then leaves a "done" file for the editor.
    /// </summary>
    [DefaultExecutionOrder(10000)] // captures after every view has queued this frame's instanced draws
    public sealed class ShotRunner : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        public Light Sun;
        string _dir;
        string _pending;
        int _pendingW, _pendingH;
        bool _saved;

        public static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        void Start()
        {
            _dir = Arg("-tc2shots");
            if (_dir == null) { enabled = false; return; }
            Directory.CreateDirectory(_dir);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (!Host.Ready) yield return null;
            ulong seed = 42;
            string s = Arg("-tc2seed");
            if (s != null) ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
            Host.NewWorld(seed);
            Host.SpeedIndex = 0; // paused: the shots show one moment
            var w = Host.World;
            // run to the wanted day and local hour at the first village (the world lives up to that moment)
            double days = Parse(Arg("-tc2days"), 0), hour = Parse(Arg("-tc2hour"), 8.0);
            int sid = w.Settlements.First();
            long local = sid != 0 ? w.LocalOffsetOf(sid) : 0;
            // seals live on a shore anywhere on the planet: when only they are shot, the hour is the shore's own (a daylight shot)
            string viewsArg = Arg("-tc2views") ?? "";
            if (viewsArg.Length > 0 && System.Array.TrueForAll(viewsArg.Split(','), v => v.StartsWith("seals")))
            {
                int shore = CameraRig.LivelyShore(w, sid != 0 ? w.Settlements.Get(sid).Anchor : w.Planet.Grid.Center[0], double.MaxValue);
                if (shore >= 0) { local = TC2.Core.Planet.PlanetState.LocalOffset(w.Planet.Grid.Center[shore]); Debug.Log("[TC2] seal shore cell " + shore + ": its own hour"); }
            }
            long target = SimTime.StartOfDay(w.Now + local) - local + DMath.ToLong(days * SimTime.Day) + DMath.ToLong(hour * SimTime.Hour);
            while (target <= w.Now) target += SimTime.Day;
            var b = Budget.Unlimited;
            w.AdvanceTo(target, ref b);
            Debug.Log("[TC2] shots at " + SimTime.Format(w.Now) + " seed " + seed);
            string storm = Arg("-tc2storm");
            if (storm != null && sid != 0)
            {
                // the god raises a storm a little offshore of the first village (a logged command)
                var P0 = w.Planet;
                var home = w.Settlements.Get(sid).Anchor;
                int best = w.Settlements.Get(sid).Cell; double bd = double.MaxValue;
                for (int c = 0; c < P0.Cells; c++)
                {
                    if (P0.IsLand(c)) continue;
                    double dd = System.Math.Abs(Vec3d.ArcDistance(P0.Grid.Center[c], home, P0.Radius) - 1500.0);
                    if (dd < bd) { bd = dd; best = c; }
                }
                w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodStorm, Cell = best, Arg = (long)Parse(storm, 55) });
                var bs = Budget.Unlimited;
                w.AdvanceTo(w.Now + 3 * SimTime.Hour, ref bs);
                Debug.Log("[TC2] storm: " + w.Planet.Storms.Count + " storms, landfalls " + w.Metrics.StormLandfalls);
            }
            string quake = Arg("-tc2quake");
            if (quake != null && sid != 0)
            {
                // the god shakes the earth under the first village (a logged command, as the player would give it)
                w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodQuake, Cell = w.Settlements.Get(sid).Cell, Arg = (long)Parse(quake, 7) });
                var bq = Budget.Unlimited;
                w.AdvanceTo(w.Now + 2 * SimTime.Minute, ref bq);
                long q = 0; foreach (long x in w.Metrics.Quakes) q += x;
                Debug.Log("[TC2] quake: " + q + " quakes, " + w.Metrics.Landslides + " landslides, " + w.Metrics.QuakeDeaths + " deaths");
            }
            string erupt = Arg("-tc2erupt");
            if (erupt != null && w.Planet.Vents.Count > 0)
            {
                // the volcano nearest the first village erupts (a god's command, as the player would give it)
                var P = w.Planet;
                Vec3d home = sid != 0 ? w.Settlements.Get(sid).Anchor : P.Vents[0].At;
                int best = 0; double bd = double.MaxValue;
                for (int v = 0; v < P.Vents.Count; v++)
                {
                    double dd = Vec3d.Angle(P.Vents[v].At, home) + (P.IsLand(P.Vents[v].Cell) ? 0 : 1.0);
                    if (dd < bd) { bd = dd; best = v; }
                }
                w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodEruption, Cell = P.Vents[best].Cell, Arg = (long)Parse(erupt, 3) });
                var be = Budget.Unlimited;
                w.AdvanceTo(w.Now + 2 * SimTime.Minute, ref be);
                Debug.Log("[TC2] eruption: " + P.Vents[best].Eruptions + " eruptions at vent " + P.Vents[best].Id + " land " + P.IsLand(P.Vents[best].Cell));
            }
            if (sid != 0)
            {
                long loc = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
                var an = w.Settlements.Get(sid).Anchor;
                yield return null;
                Vector3 toSun = Sun != null ? -Sun.transform.forward : Vector3.zero;
                Debug.Log("[TC2] sun debug: world hour " + (SimTime.SecondOfDay(w.Now) / 3600.0).ToString("0.00", CultureInfo.InvariantCulture) + " render " + Host.RenderTime.ToString("0", CultureInfo.InvariantCulture) + " now " + w.Now
                          + " village lon " + (an.Longitude * 180 / DMath.PI).ToString("0.0", CultureInfo.InvariantCulture) + " toSun " + toSun.ToString("F3") + " sun lon " + (Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg).ToString("0.0", CultureInfo.InvariantCulture)
                          + " decl " + (w.Planet.Declination(w.Now) * 180 / DMath.PI).ToString("0.0", CultureInfo.InvariantCulture) + " offset h " + (w.LocalOffsetOf(sid) / 3600.0).ToString("0.00", CultureInfo.InvariantCulture));
                Debug.Log("[TC2] village local " + (loc / 3600.0).ToString("0.00", CultureInfo.InvariantCulture) + " h, lat " + (an.Latitude * 180 / DMath.PI).ToString("0.0", CultureInfo.InvariantCulture)
                          + ", sun elevation " + (Mathf.Asin(Vector3.Dot(PlanetGeo.ToVector(an), toSun)) * Mathf.Rad2Deg).ToString("0.0", CultureInfo.InvariantCulture) + " deg");
            }
            int width = 1600, height = 900;
            string size = Arg("-tc2size");
            if (size != null) { var p = size.Split('x'); width = int.Parse(p[0], CultureInfo.InvariantCulture); height = int.Parse(p[1], CultureInfo.InvariantCulture); }
            string views = Arg("-tc2views") ?? "orbit,region,village,close";
            foreach (string view in views.Split(','))
            {
                Rig.SetView(view.Trim());
                // terrain patches, trees and people settle (real time: views rebuild on timers; no end-of-frame in batch mode)
                yield return new WaitForSecondsRealtime(5f);
                for (int i = 0; i < 5; i++) yield return null;
                _pending = view.Trim(); _pendingW = width; _pendingH = height; _saved = false;
                while (!_saved) yield return null;
            }
            foreach (var mn in new[] { "person", "house_cottage", "tree_broadleaf", "granary" })
            {
                var mm = Models.Get(mn, ProcMeshes.Person);
                Debug.Log("[TC2] model " + mn + " bounds " + mm.bounds.size.ToString("F2") + " centre " + mm.bounds.center.ToString("F2"));
            }
            var tl = FindAnyObjectByType<TerrainLod>();
            if (tl != null) Debug.Log("[TC2] terrain: drawn " + tl.PatchesDrawn + " built " + tl.PatchesBuilt + " near-village patches " + tl.NearPatches + " village vertices " + tl.VillageVerts);
            File.WriteAllText(Path.Combine(_dir, "done.txt"), SimTime.Format(w.Now));
            Debug.Log("[TC2] shots done -> " + _dir);
        }

        void LateUpdate()
        {
            if (_pending == null) return;
            Save(_pending, _pendingW, _pendingH);
            _pending = null; _saved = true;
        }

        static double Parse(string s, double def) => s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : def;

        void Save(string name, int width, int height)
        {
            var cam = Rig.Cam;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            float aspect = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = width / (float)height;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            cam.aspect = aspect;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release(); Destroy(rt);
            Debug.Log("[TC2] shot " + name);
            var sea = FindAnyObjectByType<SeaLifeView>();
            if (sea != null) Debug.Log("[TC2] sea life: shoals " + sea.Shoals + " drawn " + sea.Drawn + " seals " + sea.SealsDrawn + " | " + sea.Why);
            var wild = FindAnyObjectByType<WildlifeView>();
            if (wild != null) Debug.Log("[TC2] wildlife drawn " + wild.Drawn + " (herds " + wild.HerdsDrawn + ")");
            var stock = FindAnyObjectByType<LivestockView>();
            if (stock != null) Debug.Log("[TC2] livestock drawn " + stock.Drawn + " |" + stock.Why);
        }
    }
}
