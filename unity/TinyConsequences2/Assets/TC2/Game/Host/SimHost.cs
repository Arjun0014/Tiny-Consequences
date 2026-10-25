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
                if (e.Tier >= Tier.Historical) Feed.Add(HistoryText.Line(w, e));
            }
            if (Feed.Count > 60) Feed.RemoveRange(0, Feed.Count - 60);
            _feedCursor = w.History.LastId;
        }

        public void BeginCatchUp(long target, bool resumed = false)
        {
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
                var budget = new Budget { MaxEvents = long.MaxValue, ShouldYield = () => _sw.Elapsed.TotalMilliseconds > CatchUpBudgetMs };
                bool reached = World.AdvanceTo(CatchUpTarget, ref budget);
                PollCatchUpFeed();
                if (reached)
                {
                    CatchingUp = false;
                    Debug.Log("[TC2] caught up to " + SimTime.Format(World.Now) + " in " + ((Browser.NowUtcMs() - CatchUpRealStartMs) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s");
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
                    var budget = new Budget { MaxEvents = long.MaxValue, ShouldYield = () => _sw.Elapsed.TotalMilliseconds > WatchBudgetMs };
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
                    Feed.Add(HistoryText.Line(World, e));
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
                    CatchUpFeed.Add(HistoryText.Line(World, e));
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
            Store.Save("world", _saveScratch.Out.Buffer, length, ok => { LastSaveOk = ok; done?.Invoke(ok); });
        }

        public int LastSaveBytes; public float LastSaveMs; public bool LastSaveOk;
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
