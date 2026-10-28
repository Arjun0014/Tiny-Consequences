using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TC2.Game
{
    /// <summary>
    /// Owns the World and decides how much of it happens each frame (03 §9, 04 §6): real time x speed,
    /// sliced by a wall-clock budget, never skipping events. Saves, loads and runs offline catch-up
    /// (12 §5) with the same AdvanceTo - the catch-up is the simulation, never a summary.
    /// </summary>
    public sealed class SimHost : MonoBehaviour
    {
        public static readonly long[] Speeds = { 0, 1, 10, 60, 600, 3600, 86400, 864000 };
        public static readonly string[] SpeedNames = { "Pause", "1x", "10x", "1 min/s", "10 min/s", "1 h/s", "1 day/s", "10 days/s" };

        public World World { get; private set; }
        public int WorldVersion { get; private set; }
        public int SpeedIndex = 3;
        public bool MaxSpeed;
        public float WatchBudgetMs = 6f;
        public float CatchUpBudgetMs = 90f;   // (the catch-up screen needs ~10 frames a second: the rest of each is the world's, D2-082)
        public OfflineMode Offline = OfflineMode.Living;
        public ISaveStore Store;
        public string StatusLine = "";
        public double AchievedSpeed;
        public float LastSimMs;
        public bool Ready;
        /// <summary>Whether the world was continued from a save (the title's "Continue": F11.1), not begun.</summary>
        public bool Continued;

        // catch-up
        public bool CatchingUp;
        public long CatchUpFrom, CatchUpTarget;
        public bool CatchUpCoarse;          // the span is lived at COARSE resolution (shown on the catch-up screen)
        public long CatchUpRealStartMs;
        public readonly List<string> CatchUpFeed = new List<string>();
        long _catchUpCursor;
        /// <summary>(session 29) The catch-up is the player's own skip ahead (the bar's Skip), not an absence: it ends at its target and gives back the speed
        /// the player had. Lived exactly as a catch-up (its plan of resolutions a logged command), so a skip changes history only as any span lived COARSE does.</summary>
        public bool Skipping;
        int _skipSpeed; bool _skipMax;
        /// <summary>(session 29: the timelapse) The ms of each frame the world may take, set by the camera while it flies and while it watches from orbit
        /// (a flight wants smooth frames; the orbit's picture is cheap, so the world may take more): &lt; 0 the host's own budgets.</summary>
        public float FrameBudgetOverride = -1f;

        // the recent history feed (historical and local events)
        public readonly List<string> Feed = new List<string>();
        long _feedCursor;

        double _carry;
        float _autosave;
        long _lastSaveRealMs;
        readonly Stopwatch _sw = new Stopwatch();
        double _speedWorld, _speedReal;

        public double RenderTime => World == null ? 0 : World.Now + _carry;

        void Start()
        {
            Store = SaveStores.Create();
            StartCoroutine(Boot());
        }

        IEnumerator Boot()
        {
            StatusLine = "Looking for your world...";
            byte[] data = null; bool done = false;
            if (Browser.Param("fresh") == null) Store.Load("world", b => { data = b; done = true; });
            else done = true;
            float t0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            if (data != null)
            {
                try
                {
                    var w = SaveFile.Load(data, out var hdr);
                    _saveScratch = SaveFile.Scratch.For(hdr);
                    SetWorld(w);
                    Continued = true;
                    Offline = hdr.Offline == OfflineMode.Paused && hdr.SavedRealUtcMs == 0 ? OfflineMode.Living : hdr.Offline;
                    long now = Browser.NowUtcMs();
                    string absence = Browser.Param("absence"); // test/debug: "26h", "3d"
                    long extraMs = ParseAbsenceMs(absence);
                    long elapsed = Math.Max(0, now - hdr.SavedRealUtcMs) + extraMs;
                    long target = hdr.CatchUpTarget > w.Now ? hdr.CatchUpTarget : w.Now + OfflineProgress.WorldSecondsFor(elapsed, Offline);
                    if (target > w.Now) BeginCatchUp(target, hdr.CatchUpTarget > w.Now);
                    Debug.Log("[TC2] loaded world seed " + w.Seed + " at " + SimTime.Format(w.Now) + "; away " + (elapsed / 3600000.0).ToString("0.0", CultureInfo.InvariantCulture) + " h");
                }
                catch (Exception e)
                {
                    Debug.LogError("[TC2] could not load the saved world (" + e.Message + "); starting a new one");
                    data = null;
                }
            }
            if (data == null)
            {
                ulong seed = 42;
                string s = Browser.Param("seed");
                if (s != null) ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
                double radius = 0;
                string rs = Browser.Param("radius");
                if (rs != null) double.TryParse(rs, NumberStyles.Float, CultureInfo.InvariantCulture, out radius);
                NewWorld(seed, radius);
            }
            string sp = Browser.Param("speed");
            if (sp != null && int.TryParse(sp, NumberStyles.Integer, CultureInfo.InvariantCulture, out int si)) SpeedIndex = Mathf.Clamp(si, 0, Speeds.Length - 1);
            Ready = true;
            StatusLine = "";
        }

        public static long ParseAbsenceMs(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            double mult = 3600000.0;
            char unit = s[s.Length - 1];
            string num = s;
            if (unit == 'h' || unit == 'd' || unit == 'm') { num = s.Substring(0, s.Length - 1); mult = unit == 'd' ? 86400000.0 : (unit == 'm' ? 60000.0 : 3600000.0); }
            return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? (long)(v * mult) : 0;
        }

        public void NewWorld(ulong seed, double radius = 0)
        {
            var sw = Stopwatch.StartNew();
            var cfg = new WorldConfig { Seed = seed };
            if (radius > 0) cfg.Radius = radius;   // (tools and ?radius=: the planet's size, D2-085)
            var w = WorldGenerator.Create(cfg);
            CatchingUp = false; // a new world owes no absence
            _saveScratch = new SaveFile.Scratch();
            SetWorld(w);
            Debug.Log("[TC2] new world seed " + seed + " generated in " + sw.ElapsedMilliseconds + " ms: " + w.LivingPeople() + " people in " + w.InhabitedSettlements() + " places");
            SaveNow();
        }

        /// <summary>Tools (shots of one world at several ages): a saved world, as it was saved - no absence to catch up.</summary>
        public void LoadWorld(byte[] data)
        {
            var w = SaveFile.Load(data, out var hdr);
            _saveScratch = SaveFile.Scratch.For(hdr);
            CatchingUp = false;
            // (a world saved by a COARSE or DEEP run goes back to lived days at its next midnight, as a catch-up ends)
            long next = SimTime.StartOfDay(w.Now) + SimTime.Day;
            if (w.StepDays > 1) w.RequestStepDays(1, next);
            if (w.Resolution != TC2.Core.Simulation.Resolution.Activity) w.RequestResolution(TC2.Core.Simulation.Resolution.Activity, next);
            SetWorld(w);
            Debug.Log("[TC2] loaded world seed " + w.Seed + " at " + SimTime.Format(w.Now) + ": " + w.LivingPeople() + " people in " + w.InhabitedSettlements() + " places");
        }

        void SetWorld(World w)
        {
            World = w;
            WorldVersion++;
            Ground.ScaleTo(w.Planet);
            _carry = 0;
            Feed.Clear();
            _feedCursor = 0;
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Tier >= Tier.Historical) Feed.Add(HistoryText.Dated(w, e));
            }
            if (Feed.Count > 60) Feed.RemoveRange(0, Feed.Count - 60);
            _feedCursor = w.History.LastId;
        }

        /// <summary>
        /// (session 29) The player's skip ahead: the world lives <paramref name="seconds"/> more as fast as it can - a span holding many lives at COARSE, its
        /// last two days in full, as an absence's catch-up is (OfflineProgress.PlanCatchUp, Living's plan - never DEEP) - and the player's speed comes back
        /// at its end. The camera watches it from orbit (CameraRig's timelapse); nothing the camera does changes what is lived.
        /// </summary>
        public void BeginSkip(long seconds)
        {
            if (World == null || CatchingUp || seconds <= 0) return;
            _skipSpeed = SpeedIndex; _skipMax = MaxSpeed;
            long target = World.Now + seconds;
            bool coarse = OfflineProgress.PlanCatchUp(World, target, OfflineMode.Living);
            Skipping = true;
            CatchUpCoarse = coarse || World.Resolution == TC2.Core.Simulation.Resolution.Coarse;
            CatchingUp = true;
            CatchUpFrom = World.Now;
            CatchUpTarget = target;
            CatchUpRealStartMs = Browser.NowUtcMs();
            CatchUpFeed.Clear();
            _catchUpCursor = World.History.LastId;
            Debug.Log("[TC2] skipping from " + SimTime.Format(CatchUpFrom) + " to " + SimTime.Format(target) + (coarse ? " (COARSE)" : ""));
        }

        /// <summary>
        /// (session 29) Stops a skip early: lived days come back at the next midnight (a logged command, as the plan's own return is) and the skip ends there.
        /// A catch-up of an absence cannot be stopped - the absence is owed.
        /// </summary>
        public void StopSkip()
        {
            if (!CatchingUp || !Skipping) return;
            var w = World;
            if (!CatchUpCoarse) CatchUpTarget = w.Now;   // (lived day by day all along: it ends now)
            else
            {
                // (COARSE now, or due at the next midnight by the plan: lived days asked back for that midnight - after the plan's own switch at the same
                // moment, so a skip stopped before its COARSE began never lives a COARSE day; the plan's later return is then a no-op)
                long next = SimTime.StartOfDay(w.Now) + SimTime.Day;
                if (next + SimTime.Hour < CatchUpTarget)
                {
                    w.RequestResolution(TC2.Core.Simulation.Resolution.Activity, next);
                    CatchUpTarget = next + SimTime.Hour;
                }
            }
            Debug.Log("[TC2] skip stopped: it ends at " + SimTime.Format(CatchUpTarget));
        }

        public void BeginCatchUp(long target, bool resumed = false)
        {
            Skipping = false;
            bool coarse = !resumed && OfflineProgress.PlanCatchUp(World, target, Offline);   // (Ancient and Deep time: DEEP, F8)
            CatchUpCoarse = coarse || World.Resolution == TC2.Core.Simulation.Resolution.Coarse;
            CatchingUp = true;
            CatchUpFrom = World.Now;
            CatchUpTarget = target;
            CatchUpRealStartMs = Browser.NowUtcMs();
            CatchUpFeed.Clear();
            _catchUpCursor = World.History.LastId;
            Debug.Log("[TC2] catching up from " + SimTime.Format(CatchUpFrom) + " to " + SimTime.Format(target));
        }

        void Update()
        {
            if (World == null) return;
            _sw.Restart();
            long before = World.Now;
            if (CatchingUp)
            {
                float catchBudget = FrameBudgetOverride >= 0f ? FrameBudgetOverride : CatchUpBudgetMs;
                var budget = new Budget { MaxEvents = long.MaxValue, ShouldYield = () => _sw.Elapsed.TotalMilliseconds > catchBudget };
                bool reached = World.AdvanceTo(CatchUpTarget, ref budget);
                PollCatchUpFeed();
                if (reached)
                {
                    CatchingUp = false;
                    Debug.Log("[TC2] " + (Skipping ? "skipped" : "caught up") + " to " + SimTime.Format(World.Now) + " in " + ((Browser.NowUtcMs() - CatchUpRealStartMs) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s");
                    if (Skipping) { Skipping = false; SpeedIndex = _skipSpeed; MaxSpeed = _skipMax; _carry = 0; }   // (the player's speed again)
                    SaveNow();
                }
                else if (Browser.NowUtcMs() - _lastSaveRealMs > 30000) SaveNow(); // an interrupted catch-up resumes the same span
            }
            else
            {
                double dt = Math.Min(Time.unscaledDeltaTime, 0.25);
                double speed = MaxSpeed ? 1e9 : Speeds[SpeedIndex];
                _carry += dt * speed;
                long whole = (long)Math.Floor(_carry);
                if (whole > 0)
                {
                    float watchBudget = FrameBudgetOverride >= 0f ? FrameBudgetOverride : WatchBudgetMs;
                    var budget = new Budget { MaxEvents = long.MaxValue, ShouldYield = () => _sw.Elapsed.TotalMilliseconds > watchBudget };
                    bool reached = World.AdvanceTo(World.Now + whole, ref budget);
                    if (reached) _carry -= whole; else _carry = 0; // the clock waits for the world, never the reverse
                }
                _autosave += Time.unscaledDeltaTime;
                float every = Speeds[SpeedIndex] >= 86400 || MaxSpeed ? 20f : 60f;
                if (_autosave > every) { _autosave = 0; SaveNow(); }
            }
            LastSimMs = (float)_sw.Elapsed.TotalMilliseconds;
            _speedWorld = _speedWorld * 0.95 + (World.Now - before) * 0.05;
            _speedReal = _speedReal * 0.95 + Time.unscaledDeltaTime * 0.05;
            AchievedSpeed = _speedReal > 0 ? _speedWorld / _speedReal : 0;
            PollFeed();
        }

        void PollFeed()
        {
            var H = World.History;
            int from = H.FirstIndexAfter(_feedCursor);
            for (int i = from; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Tier >= Tier.Local || e.Kind == HistoryKind.JourneyInterrupted || e.RootKind == RootKind.You)
                {
                    Feed.Add(HistoryText.Dated(World, e));
                    if (Feed.Count > 60) Feed.RemoveAt(0);
                }
            }
            _feedCursor = H.LastId;
        }

        void PollCatchUpFeed()
        {
            var H = World.History;
            int from = H.FirstIndexAfter(_catchUpCursor);
            for (int i = from; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Tier >= Tier.Historical || e.Kind == HistoryKind.HarvestFailed || e.Kind == HistoryKind.Hunger)
                {
                    CatchUpFeed.Add(HistoryText.Dated(World, e));
                    if (CatchUpFeed.Count > 200) CatchUpFeed.RemoveAt(0);
                }
            }
            _catchUpCursor = H.LastId;
        }

        public void SaveNow(Action<bool> done = null)
        {
            if (World == null) return;
            var sw = Stopwatch.StartNew();
            long now = Browser.NowUtcMs();
            // (into buffers kept from save to save: a great world's save otherwise made ~460 MB of short-lived buffers a minute, and the browser's heap
            // only grows - SaveFile.Scratch)
            int length = SaveFile.Save(World, _saveScratch, now, Offline, CatchingUp ? CatchUpTarget : 0, CatchingUp ? CatchUpRealStartMs : 0, Feed.Count > 0 ? Feed[Feed.Count - 1] : "");
            _lastSaveRealMs = now;
            LastSaveBytes = length;
            LastSaveMs = (float)sw.Elapsed.TotalMilliseconds;
            // (the flag tells whether the latest save is in the store: unset while it is written, and set by that save's own answer only - an earlier
            // save still being written answered for it, and a page reloaded on that lost the latest: the Web tests of s25web3 and s25web4)
            LastSaveOk = false;
            int seq = ++_saveSeq;
            Store.Save("world", _saveScratch.Out.Buffer, length, ok => { if (seq == _saveSeq) { LastSaveOk = ok; if (ok) SavesStored++; } done?.Invoke(ok); });
        }

        public int LastSaveBytes; public float LastSaveMs; public bool LastSaveOk;
        int _saveSeq;
        /// <summary>Saves stored, each the latest when its answer came (the tools wait for it to rise: a command given now is done at the next frame, and the
        /// state published before it still shows the save before - the Web test reloaded on that and lost its save, s25web3-5).</summary>
        public int SavesStored;
        SaveFile.Scratch _saveScratch = new SaveFile.Scratch();   // (sized for the world at each load: its first save grows nothing by doubling)

        /// <summary>Reloads the saved world as if returning after <paramref name="absenceMs"/> (debug and tests).</summary>
        public void ReloadAsIfAway(long absenceMs)
        {
            SaveNow(ok =>
            {
                Store.Load("world", data =>
                {
                    if (data == null) return;
                    var w = SaveFile.Load(data, out _);   // (the world just saved: the save buffers already fit it)
                    SetWorld(w);
                    long target = w.Now + OfflineProgress.WorldSecondsFor(absenceMs, Offline);
                    if (target > w.Now) BeginCatchUp(target);
                });
            });
        }

        void OnApplicationQuit() { if (!Browser.IsWeb) SaveNow(); }
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }

        // ---- messages from tc2.jslib (SendMessage to the "TC2" object)
        public void OnVisibility(string state) { if (state == "hidden") SaveNow(); }
        public void OnIdbSaved(string msg) => WebSaveStore.OnSaved(msg);
        public void OnIdbLoaded(string msg) => WebSaveStore.OnLoaded(msg);
    }
}
