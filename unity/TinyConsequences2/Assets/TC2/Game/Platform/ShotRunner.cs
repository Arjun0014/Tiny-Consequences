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

        /// <summary>
        /// Look-dev premise (not a god's power): everyone of a village dies and the world goes on some years - its fields go
        /// back to the land and its houses stand as ruins (D2-052); the world then waits for the same local hour.
        /// </summary>
        /// <summary>Look-dev premise: the first village is walled (D2-074) - a palisade round its homes, its timber this sound.</summary>
        static void Wall(World w, int sid, double cond)
        {
            ref var ws = ref w.Settlements.Get(sid);
            ws.Wall = 2; ws.WallR = (float)w.Conflict.WallRadius(sid); ws.WallCond = (float)cond; ws.WallKeep = 1;
            ws.Works = (byte)(ws.Works | TC2.Core.Places.Construction.Palisade);
            Debug.Log("[TC2] the first village walled: a palisade " + (2 * System.Math.PI * ws.WallR).ToString("0", CultureInfo.InvariantCulture) + " m round");
        }

        static void Abandon(World w, int sid, double years, long local, double hour)
        {
            var res = w.Residents(sid); var dead = new System.Collections.Generic.List<int>();
            for (int i = 0; i < res.Count; i++) dead.Add(res[i]);
            foreach (int pid in dead) { ref var p = ref w.People.Get(pid); w.Agents.Kill(ref p, 0, 1); }
            var ba = Budget.Unlimited;
            w.RequestResolution(TC2.Core.Simulation.Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + DMath.ToLong(years * SimTime.Year), ref ba);
            long loc2 = SimTime.FloorMod(w.Now + local, SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.FloorMod(DMath.ToLong(hour * SimTime.Hour) - loc2, SimTime.Day), ref ba);
            Debug.Log("[TC2] abandoned the first village " + years + " years ago: inhabited " + w.Settlements.Get(sid).Inhabited + ", now " + SimTime.Format(w.Now));
        }

        string _out;   // where this world's shots go

        IEnumerator Run()
        {
            while (!Host.Ready) yield return null;
            // (-tc2load F1;F2;..: worlds saved by the headless runner - `run --snap Y1,Y2` - each shot at its age into DIR/<its name>;
            // else one new world into DIR)
            string loads = Arg("-tc2load");
            string[] files = loads != null ? loads.Split(';') : new string[] { null };
            foreach (string file in files)
            {
                // (named by the save's folder and file - r6000_s42/world_y100 -> r6000_s42_world_y100 - so worlds of several runs never collide)
                _out = files.Length > 1 ? Path.Combine(_dir, Path.GetFileName(Path.GetDirectoryName(file)) + "_" + Path.GetFileNameWithoutExtension(file)) : _dir;
                Directory.CreateDirectory(_out);
                var shoot = Shoot(file);
                while (shoot.MoveNext()) yield return shoot.Current;
            }
            File.WriteAllText(Path.Combine(_dir, "done.txt"), SimTime.Format(Host.World.Now));
            Debug.Log("[TC2] shots done -> " + _dir);
        }

        IEnumerator Shoot(string load)
        {
            ulong seed = 42;
            string s = Arg("-tc2seed");
            if (s != null) ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
            if (load != null) { Host.LoadWorld(File.ReadAllBytes(load)); seed = Host.World.Seed; }
            else Host.NewWorld(seed, Parse(Arg("-tc2radius"), 0));
            Host.SpeedIndex = 0; // paused: the shots show one moment
            var w = Host.World;
            // run to the wanted day and local hour at the first village (the world lives up to that moment)
            double days = Parse(Arg("-tc2days"), 0), hour = Parse(Arg("-tc2hour"), 8.0);
            int sid = w.Settlements.First();
            // the village to frame (C1.1 tools): an id, or "big" - the inhabited village with the most standing works
            string vArg = Arg("-tc2village");
            if (vArg != null)
            {
                int pick = 0;
                if (vArg == "town")
                {
                    // the most peopled place, as the town views frame it (CameraRig "town*"): its own local hour for the shots
                    int bp = -1;
                    for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                        if (w.Settlements.Get(s2).Inhabited && w.Settlements.Get(s2).Population > bp) { bp = w.Settlements.Get(s2).Population; pick = s2; }
                }
                else if (vArg == "big")
                {
                    int most = -1;
                    for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                    {
                        if (!w.Settlements.Get(s2).Inhabited) continue;
                        int n = 0; var bl2 = w.BuildingsOf(s2);
                        for (int i = 0; i < bl2.Count; i++) { var bb = w.Buildings.Get(bl2[i]); if (!TC2.Core.Places.Structures.IsHome(bb.Kind) && TC2.Core.Places.Structures.Working(in bb, w.Now)) n++; }
                        n = n * 1000 + w.Settlements.Get(s2).Population;
                        if (n > most) { most = n; pick = s2; }
                    }
                }
                else if (vArg == "tributary" || vArg == "lord")
                {
                    // (C10.4 shots) the most peopled village paying tribute - for bread before for peace - or the lord of the largest lordship: the standards
                    // in the lord's colour (D2-152)
                    int best = -1;
                    for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                    {
                        if (!w.Settlements.Get(s2).Inhabited) continue;
                        int score;
                        if (vArg == "lord")
                        {
                            int paying = w.Conflict.LordOf(s2) == 0 ? w.Conflict.PolitySize(s2) : 0;
                            if (paying == 0) continue;
                            score = paying * 100000 + w.Settlements.Get(s2).Population;
                        }
                        else
                        {
                            var st = w.Settlements.Get(s2);
                            if (st.Lord == 0) continue;
                            bool bread = st.LordEvent != 0 && w.History.TryGet(st.LordEvent, out var le) && le.ArgB == 1;
                            score = (bread ? 100000 : 0) + st.Population;
                        }
                        if (score > best) { best = score; pick = s2; }
                    }
                }
                else int.TryParse(vArg, out pick);
                if (pick != 0 && w.Settlements.Exists(pick)) { sid = pick; CameraRig.FocusSid = pick; Debug.Log("[TC2] framing village " + pick + " (" + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(pick)) + ", " + w.Settlements.Get(pick).Population + " people)"); }
            }
            long local = sid != 0 ? w.LocalOffsetOf(sid) : 0;
            if (Arg("-tc2hungry") != null && sid != 0)
            {
                // look-dev premise: the first village's stores are bare - its people turn to the woods, the game and the shore
                w.Settlements.Get(sid).Granary = 0;
                var hhs = w.HouseholdsOf(sid);
                for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Larder = 0;
                Debug.Log("[TC2] the first village's stores emptied");
            }
            string wall = Arg("-tc2wall");
            if (wall != null && sid != 0) Wall(w, sid, Parse(wall, 1.0));
            // (session 21) -tc2doy D: on to the framed village's own day D of its year (its calendar: the southern seasons half a year on) - the
            // stubble, the muck, the sowing are days of the village's year, not of the world's
            string doyArg = Arg("-tc2doy");
            if (doyArg != null && sid != 0)
            {
                int want = (int)Parse(doyArg, 0) % SimTime.DaysPerYear, now0 = TC2.Core.Places.SettlementSystem.DayOfYear(in w.Settlements.Get(sid), w.Now);
                days = (want - now0 + SimTime.DaysPerYear) % SimTime.DaysPerYear;
            }
            long target = SimTime.StartOfDay(w.Now + local) - local + DMath.ToLong(days * SimTime.Day) + DMath.ToLong(hour * SimTime.Hour);
            while (target <= w.Now) target += SimTime.Day;
            // (session 17) a loaded world lives its days again only from its next midnight (SimHost.LoadWorld: a save of a DEEP or COARSE run);
            // before that nobody is at work and everyone was drawn at their door - the shot waits for the lived day, three hours into it
            if (load != null && w.Resolution != TC2.Core.Simulation.Resolution.Activity)
            {
                long lived = SimTime.StartOfDay(w.Now) + SimTime.Day + 3 * SimTime.Hour;
                while (target < lived) target += SimTime.Day;
            }
            var b = Budget.Unlimited;
            w.AdvanceTo(target, ref b);
            // (C10.6 shots) -tc2after tribute: on, an hour at a time, until the framed village's tenth - or a tributary's of it - stands set aside for its
            // lord (at the harvest), then to the next morning's hour: its carriers on the way (at most a year on)
            if (Arg("-tc2after") == "tribute" && sid != 0)
            {
                long until = w.Now + SimTime.Year;
                bool due = false;
                while (w.Now < until && !due)
                {
                    w.AdvanceTo(w.Now + SimTime.Hour, ref b);
                    for (int o = w.Settlements.First(); o != 0 && !due; o = w.Settlements.Next(o))
                    {
                        int lordOf = w.Settlements.Get(o).Lord;   // (no ref locals in a coroutine)
                        // (the village's day has set the day's loads: its grown people take them in the morning - a tenth set aside after it waits a day)
                        if (lordOf != 0 && (o == sid || lordOf == sid) && w.Settlements.Get(o).CarryToday > 0) due = true;
                    }
                }
                long loc3 = w.LocalOffsetOf(sid);
                long at3 = SimTime.FloorMod(w.Now + loc3, SimTime.Day);
                long ahead3 = SimTime.FloorMod(DMath.ToLong(hour * SimTime.Hour) - at3, SimTime.Day);   // (the next such hour: the carriers set out from 7)
                w.AdvanceTo(w.Now + ahead3, ref b);
                // then on, ten minutes at a time, to the first carrier out on the way (an hour's walk or two: at the lord's village they are visitors)
                long until4 = w.Now + 5 * SimTime.Hour;
                bool onWay = false;
                while (due && w.Now < until4 && !onWay)
                {
                    for (int id = w.People.First(); id != 0 && !onWay; id = w.People.Next(id))
                    {
                        var pp = w.People.Get(id);   // (a copy: no ref locals in a coroutine)
                        if (pp.Alive && pp.Why == TC2.Core.Agents.WhyCode.Tribute && pp.Act == TC2.Core.Agents.ActivityKind.TripOut) onWay = true;
                    }
                    if (!onWay) w.AdvanceTo(w.Now + 10 * SimTime.Minute, ref b);
                }
                if (onWay) w.AdvanceTo(w.Now + 20 * SimTime.Minute, ref b);   // (out on the way)
                Debug.Log("[TC2] after tribute: " + (onWay ? "a carrier on the way" : "no carrier on the way by " + SimTime.Format(w.Now)));
                Debug.Log("[TC2] after tribute: " + (due ? "a tenth set aside, the next morning" : "no tenth set aside within a year") + " - " + SimTime.Format(w.Now));
            }
            // seals live on a shore anywhere on the planet: when only they are shot, the world goes on to the same hour at the
            // shore they haul out on now (a daylight shot)
            if (Arg("-tc2ground") != null) WorldObjectsView.ShowGround = true;   // (C13.3 tools: the simulation's ground under each home)
            string viewsArg = Arg("-tc2views") ?? "";
            if (viewsArg.Length > 0 && System.Array.TrueForAll(viewsArg.Split(','), v => v.StartsWith("seals")))
            {
                int shore = CameraRig.LivelyShore(w, sid != 0 ? w.Settlements.Get(sid).Anchor : w.Planet.Grid.Center[0], double.MaxValue);
                if (shore >= 0)
                {
                    long off = TC2.Core.Planet.PlanetState.LocalOffset(w.Planet.Grid.Center[shore]);
                    long at = SimTime.FloorMod(w.Now + off, SimTime.Day);
                    w.AdvanceTo(w.Now + SimTime.FloorMod(DMath.ToLong(hour * SimTime.Hour) - at, SimTime.Day), ref b);
                    Debug.Log("[TC2] seal shore cell " + shore + ": its own hour");
                }
            }
            Debug.Log("[TC2] shots at " + SimTime.Format(w.Now) + " seed " + seed);
            if (sid != 0 && w.Settlements.Exists(sid))
            {
                var fs = w.Settlements.Get(sid);   // (the framed village's own season: what its fields and herds should show; a copy - this is a coroutine)
                Debug.Log("[TC2] framed village " + sid + ": day " + TC2.Core.Places.SettlementSystem.DayOfYear(in fs, w.Now) + " of its year, crop " + fs.Crop +
                          ", stubble " + (fs.Stubble / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " t on " + fs.FieldHa.ToString("0", CultureInfo.InvariantCulture) + " ha, muck in the yards " +
                          (fs.Muck / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " t, spread " + (fs.Spread / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " t");
            }
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
            string abandon = Arg("-tc2abandon");
            if (abandon != null && sid != 0) Abandon(w, sid, Parse(abandon, 20), local, hour);
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
            foreach (string spec in views.Split(','))
            {
                // "view+D@H": the world lives on D days, then to the next local hour H at the first village, before this view is shot
                // (seasons and nights in the same round; the world's state goes on as it would)
                string view = spec.Trim();
                int at = view.IndexOf('@');
                double hAt = -1;
                if (at > 0) { hAt = Parse(view.Substring(at + 1), hour); view = view.Substring(0, at); }
                int plus = view.IndexOf('+');
                if (plus > 0)
                {
                    double dAhead = Parse(view.Substring(plus + 1), 0);
                    view = view.Substring(0, plus);
                    if (dAhead > 0) { var bd = Budget.Unlimited; w.AdvanceTo(w.Now + DMath.ToLong(dAhead * SimTime.Day), ref bd); }
                }
                if (hAt >= 0)
                {
                    long locNow = SimTime.FloorMod(w.Now + local, SimTime.Day);
                    long ahead = SimTime.FloorMod(DMath.ToLong(hAt * SimTime.Hour) - locNow, SimTime.Day);
                    if (ahead > 0) { var bh = Budget.Unlimited; w.AdvanceTo(w.Now + ahead, ref bh); }
                }
                if (at > 0 || plus > 0) Debug.Log("[TC2] " + spec + ": the world at " + SimTime.Format(w.Now));
                if (view.StartsWith("voyage"))
                {
                    // (C9.3) the world lives on until it shows what the view frames: a boat halfway along a way over the sea ("voyage",
                    // "voyagefar"), or a crew ashore at the other landing ("voyagestay") - waiting for it, never staging it (12 days at most)
                    // (C9.5a) "voyagetenth...", "voyagerelief...": only a boat carrying a lord's tenth or famine grain
                    int cargo = CameraRig.VoyageCargoFilter(view);
                    string vb = CameraRig.VoyageBase(view);
                    int mode = vb == "voyagestay" ? 2 : vb == "voyagenear" ? 1 : 0;
                    var bv = Budget.Unlimited;
                    long until = w.Now + 12 * SimTime.Day;
                    while (w.Now < until && !VoyageInView(w, mode, cargo)) w.AdvanceTo(w.Now + 5 * SimTime.Minute, ref bv);
                    Debug.Log("[TC2] " + spec + ": " + (VoyageInView(w, mode, cargo) ? "found" : "none") + " at " + SimTime.Format(w.Now) + VoyageNote(w, mode, cargo));
                }
                Rig.SetView(view);
                // terrain patches, trees and people settle (real time: views rebuild on timers; no end-of-frame in batch mode)
                yield return new WaitForSecondsRealtime(5f);
                for (int i = 0; i < 5; i++) yield return null;
                _pending = spec.Trim().Replace('@', '_').Replace('+', 'd').Replace(':', '-'); _pendingW = width; _pendingH = height; _saved = false;   // (':' - "forest:rain", W-FOREST - is no file name)
                while (!_saved) yield return null;
            }
            foreach (var mn in new[] { "person", "home_cottage_g", "tree_broadleaf", "granary", "home_hall_g", "home_row_g", "home_two_g" })
            {
                var mm = Models.Get(mn, ProcMeshes.Person);
                Debug.Log("[TC2] model " + mn + " bounds " + mm.bounds.size.ToString("F2") + " centre " + mm.bounds.center.ToString("F2"));
            }
            var tl = FindAnyObjectByType<TerrainLod>();
            if (tl != null) Debug.Log("[TC2] terrain: drawn " + tl.PatchesDrawn + " built " + tl.PatchesBuilt + " near-village patches " + tl.NearPatches + " village vertices " + tl.VillageVerts);
        }

        void LateUpdate()
        {
            if (_pending == null) return;
            Save(_pending, _pendingW, _pendingH);
            _pending = null; _saved = true;
        }

        /// <summary>C9.3 shots: a boat rowing a way, a third to two-thirds along it (mode 0), within its first or last fifth - near a landing
        /// (1), or a crew ashore at the other landing (2); with a cargo filter (C9.5a) only a boat carrying it (`CameraRig.CrewCarries`).</summary>
        static bool VoyageInView(TC2.Core.Simulation.World w, int mode, int cargo = 0) => VoyagerInView(w, mode, cargo) != 0;

        static int VoyagerInView(TC2.Core.Simulation.World w, int mode, int cargo)
        {
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || p.Voyage == 0) continue;
                if (cargo != 0 && !CameraRig.CrewCarries(w, in p, cargo)) continue;
                if (mode == 2) { if (p.At != 0 && p.At != p.Home && p.Step != TC2.Core.Agents.StepKind.Travel) return id; continue; }
                if (p.Act != TC2.Core.Agents.ActivityKind.Voyage || p.Step != TC2.Core.Agents.StepKind.Travel) continue;
                double f = p.T1 > p.T0 ? (w.Now - p.T0) / (double)(p.T1 - p.T0) : 0;
                if (mode == 0 ? f > 0.33 && f < 0.67 : (f > 0.06 && f < 0.2) || (f > 0.8 && f < 0.94)) return id;
            }
            return 0;
        }

        /// <summary>What the framed crew is doing, as the person page tells its skipper (the shot's log: what the picture shows).</summary>
        static string VoyageNote(TC2.Core.Simulation.World w, int mode, int cargo)
        {
            int id = VoyagerInView(w, mode, cargo);
            if (id == 0) return "";
            var p = w.People.Get(id);   // (copies: this is called from a coroutine)
            int sk = CameraRig.CrewSkipper(w, in p, cargo);
            var q = w.People.Get(sk != 0 ? sk : id);
            return " - a crew of " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(q.Home)) + ": " + TC2.Core.History.HistoryText.Doing(w, in q);
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
            File.WriteAllBytes(Path.Combine(_out ?? _dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release(); Destroy(rt);
            Debug.Log("[TC2] shot " + name);
            var sea = FindAnyObjectByType<SeaLifeView>();
            if (sea != null) Debug.Log("[TC2] sea life: shoals " + sea.Shoals + " drawn " + sea.Drawn + " seals " + sea.SealsDrawn + " | " + sea.Why);
            var wild = FindAnyObjectByType<WildlifeView>();
            if (wild != null) Debug.Log("[TC2] wildlife drawn " + wild.Drawn + " (herds " + wild.HerdsDrawn + ")");
            var objs = FindAnyObjectByType<WorldObjectsView>();
            if (objs != null) Debug.Log("[TC2] people drawn " + objs.PeopleDrawn + " (at work or on the move " + objs.PeopleBusy + "), boats afloat " + objs.BoatsAfloat);
            Debug.Log("[TC2] trees on the drawn patches " + TerrainLod.ForestDrawn + ", stumps " + TerrainLod.StumpsOnPatches + " (drawn near the camera " + TerrainLod.StumpsDrawn + ")");
            Debug.Log("[TC2] muck heaps drawn " + WorldObjectsView.MuckHeaps + " (the largest " + WorldObjectsView.MuckLargestKg.ToString("0", CultureInfo.InvariantCulture) + " kg)");
            var stock = FindAnyObjectByType<LivestockView>();
            if (stock != null) Debug.Log("[TC2] livestock drawn " + stock.Drawn + " |" + stock.Why);
        }
    }
}
