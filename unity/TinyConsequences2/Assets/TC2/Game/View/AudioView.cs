using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// (F10.1) The world's sound: an observer that reads state only (never writes the world). Beds of sound - wind from on high, the woods by day and by
    /// night, the shore, a river, rain and storm, a village by day and by night, a fire, a forest burning, a volcano, the open sea, the cold - are mixed by
    /// what the camera looks at: how high it is, what lies under its view (sea or land, woods, water, a village near, the local hour), the weather there
    /// today, the fires and the eruptions near it. Single sounds come from the world's own happenings near where the camera looks (thunder for a lightning
    /// strike, the rumble of a quake felt there, a house falling) and a cue when you cast a power. Library: TC1's generated sounds (CREDITS). M mutes.
    /// (session 32: the audit) Every single sound goes through Shot: fainter and later with distance (thunder after its flash), panned to where it is on the
    /// screen, never piled on itself (a sound asked again too soon waits its turn, twice at most) and never the same take twice running. Each power has
    /// a sound of its own; a quake goes on rumbling, a storm near is heard as wind, the god's rain, cold and storm raise their beds; what later comes of a
    /// cast is heard as a shimmer. The loudest moment is a stone's landing: nothing else is mixed above it.
    /// </summary>
    public sealed class AudioView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        public static bool Muted;
        public static float Volume = 0.8f;
        /// <summary>(session 31) x the world's beds while something else holds the player's attention (the book open, the menu); eased.</summary>
        public static float Duck = 1f;
        float _duckNow = 1f;
        CameraRig.LapseState _lapseWas;

        static readonly string[] Beds = { "amb_wind_high", "amb_wind_winter", "amb_forest_day", "amb_forest_night", "amb_coast", "amb_river", "amb_rain", "amb_storm",
                                          "amb_village_day", "amb_village_night", "amb_fire", "amb_wildfire", "amb_volcano", "amb_sea_open", "amb_herd", "amb_stillness", "amb_river_flood" };
        const int WindHigh = 0, WindCold = 1, ForestDay = 2, ForestNight = 3, Coast = 4, River = 5, Rain = 6, Storm = 7, VillageDay = 8, VillageNight = 9,
                  Fire = 10, Wildfire = 11, Volcano = 12, SeaOpen = 13, Herd = 14, Stillness = 15, RiverFlood = 16;
        AudioSource[] _bed;
        readonly float[] _want = new float[Beds.Length];
        readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        AudioSource[] _shot;
        int _nextShot;
        long _seenId = -1;
        float _lastStory;
        // (session 27) the meteor heard falling: a roar that drops in pitch and swells as the stone comes down (made here, from filtered noise),
        // and its landing - the thunder and the crash of its MeteorStruck record - held until the stone is seen to land (AimView's fall)
        static float _fallAt = -1f, _landAt = -1f;
        AudioClip _fall;
        struct Later { public string Name; public float Volume, At, Pan, Pitch; public int Pick; }
        readonly List<Later> _later = new List<Later>();
        readonly Dictionary<string, float> _lastAt = new Dictionary<string, float>();
        readonly Dictionary<string, int> _lastTake = new Dictionary<string, int>();

        // (session 31: the interface heard) a short sound for a click, a pointer over a word, a leaf turned, a chosen person; quiet, and never when muted
        static readonly List<string> _uiAsked = new List<string>();
        public static void Ui(string what) { if (!Muted && _uiAsked.Count < 8 && !_uiAsked.Contains(what)) _uiAsked.Add(what); }   // (one of each a frame: the tree opened from the impact card asked its page twice)

        /// <summary>A stone thrown from the sky (the meteor's cast): heard falling for <paramref name="seconds"/>, its landing heard then.</summary>
        public static void MeteorFall(float seconds) { _fallAt = Time.unscaledTime; _landAt = Time.unscaledTime + seconds; }

        void Start()
        {
            _bed = new AudioSource[Beds.Length];
            for (int i = 0; i < Beds.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = Resources.Load<AudioClip>("Audio/" + Beds[i]);
                src.loop = true; src.volume = 0f; src.playOnAwake = false; src.spatialBlend = 0f;
                _bed[i] = src;
            }
            _shot = new AudioSource[12];
            for (int i = 0; i < _shot.Length; i++) { _shot[i] = gameObject.AddComponent<AudioSource>(); _shot[i].playOnAwake = false; _shot[i].spatialBlend = 0f; }
            foreach (var n in new[] { "thunder", "quake", "house_fall", "wolf_howl", "shout", "quake_rumble", "wind_gust", "splash", "douse", "tree_fall" }) Load(n, 3);
            foreach (var n in new[] { "ui_power", "ui_story", "ui_you", "ui_click", "ui_chime", "ui_breaking", "rain_onset", "heat_haze", "frost", "wither" }) Load(n, 1);
            foreach (var n in SpotNames) Load(n, 3);
            _fall = MakeFall(AimView.FallTime);
        }

        /// <summary>The stone's roar as it falls (<paramref name="seconds"/> to the ground, then a short tail): white noise through a band that sweeps
        /// down from ~2.6 kHz to ~180 Hz as it nears - the air torn - over a deep rumble, both swelling to the landing.</summary>
        static AudioClip MakeFall(float seconds, int rate = 44100)
        {
            int n = (int)((seconds + 0.12f) * rate);
            var d = new float[n];
            uint s = 0x9E3779B9u;
            float low = 0f, band = 0f, rum = 0f, peak = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, u = Mathf.Clamp01(t / seconds);
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                float x = (s & 0xFFFFFF) / 8388608f - 1f;
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Lerp(2600f, 180f, u * u) / rate);   // (a state-variable filter's band)
                float high = x - low - 0.22f * band; band += f * high; low += f * band;   // (narrow: a falling whistle over the hiss)
                rum += (x - rum) * 0.004f;
                float swell = u * u * (0.25f + 0.75f * u);
                float end = t <= seconds ? 1f : Mathf.Exp(-(t - seconds) * 40f);
                float y = (band * (0.3f + 0.7f * swell) + rum * 18f * swell) * end * Mathf.Clamp01(t * 30f);
                d[i] = y;
                peak = Mathf.Max(peak, Mathf.Abs(y));
            }
            for (int i = 0; i < n; i++) d[i] *= 0.85f / peak;
            var c = AudioClip.Create("meteor_fall", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        void PlayUi()
        {
            for (int i = 0; i < _uiAsked.Count; i++)
            {
                string ask = _uiAsked[i];
                if (_uiLast.TryGetValue(ask, out float was) && Time.unscaledTime - was < 0.07f) continue;   // (asked again a frame or two later: heard once)
                _uiLast[ask] = Time.unscaledTime;
                switch (ask)
                {
                    case "click": PlayOne("ui_click", 0.30f); break;
                    case "tick": PlayOne("ui_tick", 0.16f); break;
                    case "select": PlayOne("ui_select", 0.32f); break;
                    case "page": PlayOne("ui_page", 0.42f); break;
                    case "leaf": PlayOne("ui_leaf", 0.34f); break;
                    case "cast": PlayOne("ui_cast", 0.40f); break;
                    case "enter": PlayOne("ui_enter", 0.48f); break;
                    case "chime": PlayOne("ui_chime", 0.30f); break;
                }
            }
            _uiAsked.Clear();
        }

        void PlayOne(string name, float volume)
        {
            if (_shot == null) return;
            if (!_uiClips.TryGetValue(name, out var c)) { c = Resources.Load<AudioClip>("Audio/" + name); _uiClips[name] = c; }
            if (c == null) return;
            var src = _shot[_nextShot]; _nextShot = (_nextShot + 1) % _shot.Length;
            src.clip = c; src.volume = Mathf.Clamp01(volume) * Volume; src.panStereo = 0f; src.pitch = 1f; src.Play();
        }
        readonly Dictionary<string, AudioClip> _uiClips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _uiLast = new Dictionary<string, float>();

        void Load(string name, int takes)
        {
            var l = new List<AudioClip>();
            var one = Resources.Load<AudioClip>("Audio/" + name);
            if (one != null) l.Add(one);
            for (int k = 1; k <= takes; k++) { var c = Resources.Load<AudioClip>("Audio/" + name + "_" + k); if (c != null) l.Add(c); }
            _clips[name] = l.ToArray();
        }

        /// <summary>(session 32) How soon a sound may be heard again (s): thunder rolls one after another, a quake's rumble is not piled on itself.</summary>
        static float GapOf(string name)
        {
            switch (name)
            {
                case "thunder": return 0.9f;
                case "quake": return 2.5f;
                case "quake_rumble": return 3.5f;
                case "house_fall": return 0.4f;
                case "shout": return 1.5f;
                case "wind_gust": return 2.5f;
                case "tree_fall": return 3.5f;
                case "wolf_howl": return 4f;
                case "douse": return 2.5f;
                case "splash": return 0.6f;
                default: return 0.15f;
            }
        }

        /// <summary>A single sound of the world: now or <paramref name="delay"/> seconds on, panned and pitched; asked again within its gap it waits
        /// its turn (two waiting at most, any more dropped), so a burst of strikes rolls rather than stacks.</summary>
        void Shot(string name, float volume, int pick, float pan = 0f, float pitch = 1f, float delay = 0f)
        {
            if (volume < 0.015f) return;
            float now = Time.unscaledTime, at = now + Mathf.Max(0f, delay);
            if (_lastAt.TryGetValue(name, out float last) && at < last + GapOf(name))
            {
                int waiting = 0;
                for (int i = 0; i < _later.Count; i++) if (_later[i].Name == name) waiting++;
                if (waiting >= 2) return;
                at = last + GapOf(name) + Random.Range(0.05f, 0.3f);
            }
            _lastAt[name] = at;
            if (at > now + 0.01f) { _later.Add(new Later { Name = name, Volume = volume, At = at, Pick = pick, Pan = pan, Pitch = pitch }); return; }
            Sound(name, volume, pick, pan, pitch);
        }

        void Sound(string name, float volume, int pick, float pan, float pitch)
        {
            if (!_clips.TryGetValue(name, out var a)) { Load(name, 3); a = _clips[name]; }
            if (a.Length == 0) return;
            int take = (pick & 0x7fffffff) % a.Length;
            if (a.Length > 1 && _lastTake.TryGetValue(name, out int was) && was == take) take = (take + 1) % a.Length;   // (never the same take twice running)
            _lastTake[name] = take;
            var src = _shot[_nextShot]; _nextShot = (_nextShot + 1) % _shot.Length;
            src.clip = a[take];
            src.volume = Mathf.Clamp01(volume) * Volume;
            src.panStereo = Mathf.Clamp(pan, -0.8f, 0.8f);
            src.pitch = pitch * Random.Range(0.97f, 1.03f);
            src.Play();
        }

        // (session 27, the person's word) the world falls quiet while the game is in the background - another window or tab in front, or the
        // app paused - and plays on when it is in front again; the M key and the settings' Sound stay the player's own (this is not kept)
        bool _unfocused, _paused;
        void OnApplicationFocus(bool focus) { _unfocused = !focus; AudioListener.pause = _unfocused || _paused; }
        void OnApplicationPause(bool paused) { _paused = paused; AudioListener.pause = _unfocused || _paused; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) Muted = !Muted;
            PlayUi();
            var w = Host != null ? Host.World : null;
            bool quiet = w == null || Muted || Rig == null;
            for (int i = 0; i < _want.Length; i++) _want[i] = 0f;
            // (session 31) the planet's quiet on the title and while the years pass from orbit; the ground's own sounds near it
            bool still = !quiet && (Rig.InTitle || Host.CatchingUp || Rig.LapseFlies);
            if (still) { _want[Stillness] = Rig.InTitle ? 0.46f : 0.40f; _want[WindHigh] = 0.20f; }
            else if (!quiet) Listen(w);
            if (!quiet && Rig != null)
            {
                // the camera's flight up to orbit and down heard as wind opening and closing
                var ls = Rig.Lapse;
                if (ls != _lapseWas)
                {
                    if (ls == CameraRig.LapseState.Rising && _lapseWas != CameraRig.LapseState.Orbit) PlayOne("lapse_rise", 0.42f);
                    else if (ls == CameraRig.LapseState.Returning) PlayOne("lapse_fall", 0.42f);
                    _lapseWas = ls;
                }
            }
            _duckNow = Mathf.MoveTowards(_duckNow, Duck, Time.unscaledDeltaTime * 1.6f);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 1.5f);   // (beds drift to their mix over a second or two)
            for (int i = 0; i < _bed.Length; i++)
            {
                var src = _bed[i];
                if (src.clip == null) continue;
                src.volume = Mathf.Lerp(src.volume, _want[i] * Volume * _duckNow, k);
                if (src.volume > 0.005f && !src.isPlaying) { src.time = Random.Range(0f, Mathf.Max(0.01f, src.clip.length - 0.1f)); src.Play(); }
                else if (src.volume <= 0.005f && src.isPlaying && _want[i] == 0f) src.Pause();
            }
            if (quiet || still) { if (w != null) _seenId = w.History.LastId; _fallAt = -1f; _later.Clear(); return; }
            if (_fallAt >= 0f && _fall != null)
            {
                var src = _shot[_nextShot]; _nextShot = (_nextShot + 1) % _shot.Length;
                src.clip = _fall; src.volume = 0.8f * Volume; src.panStereo = 0f; src.pitch = 1f; src.Play();
                _fallAt = -1f;
            }
            for (int i = _later.Count - 1; i >= 0; i--)
                if (_later[i].At <= Time.unscaledTime) { var l = _later[i]; _later.RemoveAt(i); Sound(l.Name, l.Volume, l.Pick, l.Pan, l.Pitch); }
            Happenings(w);
            Spots(w);
            Gusts();
        }

        // (session 31: a place heard as lived) near the ground, now and then, one of the people near where the camera looks is heard at what they are doing this moment -
        // the smith's hammer, an axe in the woods, a hoe, a pick, oars - quiet, panned by where they stand on the screen, never the same kind twice running
        static readonly string[] SpotNames = { "anvil", "axe_chop", "hammer", "hoe", "pick_stone", "scythe", "oars", "baby_cry", "steps_grass" };
        float _spotNext; int _spotLast = -1;
        readonly float[] _spotCool = new float[SpotNames.Length];

        static int SpotOf(TC2.Core.Agents.ActivityKind a, TC2.Core.Agents.WhyCode why)
        {
            switch (a)
            {
                case TC2.Core.Agents.ActivityKind.Smith: return 0;
                case TC2.Core.Agents.ActivityKind.Wood: case TC2.Core.Agents.ActivityKind.Burn: return 1;
                case TC2.Core.Agents.ActivityKind.Build: return 2;
                case TC2.Core.Agents.ActivityKind.Fields: return why == TC2.Core.Agents.WhyCode.Harvest ? 5 : 3;
                case TC2.Core.Agents.ActivityKind.Garden: return 3;
                case TC2.Core.Agents.ActivityKind.Quarry: case TC2.Core.Agents.ActivityKind.Mine: return 4;
                case TC2.Core.Agents.ActivityKind.Sail: case TC2.Core.Agents.ActivityKind.Voyage: return 6;
                case TC2.Core.Agents.ActivityKind.Infant: return 7;
                default: return -1;
            }
        }

        void Spots(TC2.Core.Simulation.World w)
        {
            if (Time.unscaledTime < _spotNext || Rig.Cam == null) return;
            float alt = Mathf.Max(0f, (float)(Rig.Distance - w.Planet.Radius));
            if (alt > 520f || Host.SpeedIndex > 4 || Host.SpeedIndex == 0 || Host.MaxSpeed) { _spotNext = Time.unscaledTime + 0.5f; return; }
            _spotNext = Time.unscaledTime + Random.Range(0.45f, 1.3f);
            var P = w.Planet;
            Vector3 f = Rig.FocusPoint;
            Vec3d dir = new Vec3d(f.x, f.y, f.z).Normalized;
            int near = 0; double best = 900.0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double d = Vec3d.ArcDistance(dir, s.Anchor, P.Radius);
                if (d < best) { best = d; near = sid; }
            }
            if (near == 0) return;
            var res = w.Residents(near);
            if (res.Count == 0) return;
            double t = Host.RenderTime;
            Vector3 cam = Rig.Cam.transform.position;
            for (int tries = 0; tries < 10; tries++)
            {
                int id = res[Random.Range(0, res.Count)];
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                int kind = SpotOf(p.Act, p.Why);
                if (kind < 0 || kind == _spotLast || Time.unscaledTime < _spotCool[kind]) continue;
                bool crying = kind == 7;
                if (crying && Random.value > 0.08f) continue;   // (a baby is rarely heard)
                if (!TC2.Core.Agents.Presence.Of(w, in p, t, out var at, out _, out _, out _) && !crying) continue;
                Vector3 pos = crying ? (Vector3)f : PlanetGeo.Surface(P, at);
                float dist = Vector3.Distance(cam, pos);
                float reach = Mathf.Max(260f, alt * 1.6f);
                float v = Mathf.Clamp01(1f - dist / reach);
                if (v <= 0.05f) continue;
                Vector3 sp = Rig.Cam.WorldToViewportPoint(pos);
                float pan = sp.z > 0f ? Mathf.Clamp((sp.x - 0.5f) * 1.5f, -0.8f, 0.8f) : 0f;
                _spotLast = kind;
                _spotCool[kind] = Time.unscaledTime + Random.Range(2.5f, 6f);
                float sv = (crying ? 0.14f : 0.42f) * v * v;
                if (sv >= 0.015f) Sound(SpotNames[kind], sv, id, pan, 1f);
                return;
            }
            _spotLast = -1;
        }

        /// <summary>A single sound now, or - a meteor's landing while its stone is still seen falling - when it lands (and <paramref name="after"/> on).</summary>
        void Landing(string name, float volume, int pick, float pan, float pitch = 1f, float after = 0f)
        {
            Shot(name, volume, pick, pan, pitch, Mathf.Max(0f, _landAt - Time.unscaledTime) + after);
        }

        // (session 32) a storm near where the camera looks is heard as wind as well as rain: its gusts now and then; a forest burning near, its trees falling
        float _stormNear, _burnNear, _gustNext, _treeNext;

        void Gusts()
        {
            float now = Time.unscaledTime;
            if (_stormNear > 0.25f && now >= _gustNext)
            {
                _gustNext = now + Random.Range(4f, 10f) / Mathf.Max(0.5f, _stormNear);
                Shot("wind_gust", 0.18f + 0.34f * _stormNear, Random.Range(0, 1000), Random.Range(-0.5f, 0.5f), Random.Range(0.92f, 1.08f));
            }
            if (_burnNear > 0.35f && now >= _treeNext)
            {
                _treeNext = now + Random.Range(8f, 18f);
                Shot("tree_fall", 0.12f + 0.2f * _burnNear, Random.Range(0, 1000), Random.Range(-0.6f, 0.6f), Random.Range(0.9f, 1.05f), Random.Range(0f, 2f));
            }
        }

        /// <summary>How near where the camera looks lies a god's spell of a kind (0 drought, 1 rain, 2 warmth, 3 cold) still held: 1 within most of its reach.</summary>
        static float SpellNear(TC2.Core.Simulation.World w, Vec3d dir, byte kind)
        {
            float best = 0f;
            for (int i = 0; i < w.DrySpells.Count; i++)
            {
                var sp = w.DrySpells[i];
                if (sp.Kind != kind || w.Now >= sp.Until) continue;
                double d = Vec3d.ArcDistance(dir, w.Planet.Grid.Center[sp.Cell], w.Planet.Radius);
                best = Mathf.Max(best, Mathf.Clamp01(1.4f * (1f - (float)(d / sp.Reach))));
            }
            return best;
        }

        /// <summary>What the camera hears: the beds' mix from where it looks and how high it is.</summary>
        void Listen(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            Vector3 f = Rig.FocusPoint;
            if (f.sqrMagnitude < 1f) return;
            Vec3d dir = new Vec3d(f.x, f.y, f.z).Normalized;
            int cell = P.Grid.CellOf(dir);
            if (cell < 0) return;
            float alt = Mathf.Max(0f, (float)(Rig.Distance - P.Radius));
            float near = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(400f, 4000f, alt));   // (near the ground: its sounds)
            float high = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1500f, 14000f, alt));      // (from on high: the wind)
            // the local hour where it looks (the sun's: noon at the longitude facing it)
            long local = SimTime.FloorMod(w.Now + PlanetState.LocalOffset(dir), SimTime.Day);
            float hour = local / (float)SimTime.Hour;
            float day = Mathf.Clamp01(Mathf.Min(Mathf.InverseLerp(5f, 7f, hour), Mathf.InverseLerp(21f, 19f, hour)));
            bool land = P.IsLand(cell);
            bool shore = false; for (int j = 0; j < 8; j++) if (P.IsLand(P.Grid.Nbr8[cell * 8 + j]) != land) shore = true;
            float wood = land ? Mathf.Clamp01(P.Forest[cell] * 1.3f) : 0f;
            float river = land && (P.IsStream(cell) || P.IsRiver(cell)) ? (P.IsRiver(cell) ? 1f : 0.6f) : 0f;
            float rain = Mathf.Clamp01(P.RainToday[cell] / 1.2f);
            float cold = Mathf.Clamp01((2f - (float)P.Temp[cell]) / 8f);
            // (session 32) the god's rain and cold heard where they are held from the moment they are cast (the day's weather catches up later)
            // (session 32: the sky's last cast rules - a rain under a drought cast after it is not heard; today's rain is set at the cast now, as it is seen)
            if (w.SkyAt(cell) >= 0) rain = Mathf.Max(rain, 0.9f * SpellNear(w, dir, 1)); else rain = 0f;
            cold = Mathf.Max(cold, 0.7f * SpellNear(w, dir, 3));
            // a storm near: by its distance and its strength, whether its rain has fallen here yet or not (before: any storm on the planet, with rain here)
            float stormNear = 0f;
            for (int i = 0; i < P.Storms.Count; i++)
            {
                var st = P.Storms[i];
                double d = Vec3d.ArcDistance(dir, st.At, PlanetState.GeoRadius);
                stormNear = Mathf.Max(stormNear, Mathf.Clamp01(1.3f * (1f - (float)(d / (st.Radius * 1.4f)))) * Mathf.Clamp01(0.35f + st.Wind / 45f));
            }
            _stormNear = stormNear;
            // a village near where it looks (its people's sounds), its herds
            float village = 0f;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Population <= 0) continue;
                double d = Vec3d.ArcDistance(dir, s.Anchor, P.Radius);
                if (d < 900.0) village = Mathf.Max(village, (1f - (float)(d / 900.0)) * Mathf.Clamp01(0.4f + s.Population / 200f));
            }
            // fires and eruptions near it
            float fire = 0f; int burning = 0;
            for (int i = 0; i < w.Fire.Active.Count; i++)
            {
                double d = Vec3d.ArcDistance(dir, P.Grid.Center[w.Fire.Active[i]], P.Radius);
                if (d < 2500.0) { burning++; fire = Mathf.Max(fire, 1f - (float)(d / 2500.0)); }
            }
            float volcano = 0f;
            for (int v = 0; v < P.Vents.Count; v++)
            {
                var vt = P.Vents[v];
                if (vt.Eruptions == 0 || w.Now - vt.LastEruption > 4 * SimTime.Day) continue;
                double d = Vec3d.ArcDistance(dir, vt.At, PlanetState.GeoRadius);
                if (d < 12000.0) volcano = Mathf.Max(volcano, 1f - (float)(d / 12000.0));
            }
            _burnNear = fire * near;
            float flood = river > 0f ? Mathf.Clamp01((rain - 0.5f) / 0.4f) : 0f;   // (a river in spate after heavy rain)
            _want[WindHigh] = 0.15f + 0.55f * high;
            _want[Stillness] = 0.30f * high;
            _want[WindCold] = cold * (0.25f + 0.35f * near);
            _want[ForestDay] = near * wood * day * 0.7f;
            _want[ForestNight] = near * wood * (1f - day) * 0.6f;
            _want[Coast] = shore ? 0.25f + 0.45f * near : 0f;
            _want[River] = near * river * 0.55f * (1f - flood);
            _want[RiverFlood] = near * river * 0.6f * flood;
            _want[Rain] = rain * (1f - 0.6f * stormNear) * 0.75f * (0.4f + 0.6f * near);
            _want[Storm] = stormNear * (0.45f + 0.35f * Mathf.Max(rain, 0.5f)) * (0.35f + 0.65f * near);   // (from on high its wind more than its rain on the roofs)
            _want[VillageDay] = near * village * day * 0.75f;
            _want[VillageNight] = near * village * (1f - day) * 0.6f;
            _want[Herd] = near * village * day * 0.25f;
            _want[Fire] = fire * near * 0.8f;
            _want[Wildfire] = Mathf.Clamp01(burning / 12f) * (0.3f + 0.4f * (1f - near));
            _want[Volcano] = volcano * 0.8f;
            _want[SeaOpen] = !land && !shore ? 0.2f + 0.4f * near : 0f;
        }

        float _lastCry = -99f;

        /// <summary>The world's happenings since the last frame, near where the camera looks: thunder, a quake, a house falling, your casts.</summary>
        void Happenings(TC2.Core.Simulation.World w)
        {
            var H = w.History;
            if (_seenId < 0) { _seenId = H.LastId; return; }
            if (H.LastId == _seenId) return;
            var P = w.Planet;
            Vector3 f = Rig.FocusPoint;
            Vec3d dir = new Vec3d(f.x, f.y, f.z).Normalized;
            float alt = Mathf.Max(0f, (float)(Rig.Distance - P.Radius));
            float hear = 3000f + 2f * alt;   // (how far it hears: farther from on high, more faintly)
            int told = 0;
            for (int i = H.FirstIndexAfter(_seenId); i < H.Count && told < 6; i++)
            {
                var e = H.At(i);
                if (e.Kind == HistoryKind.GodCommand) { CastHeard(e); told++; continue; }
                // (session 32) what later comes of a cast this session - a fire spreading, the first sick, a harvest failing - heard as a shimmer, now and then
                if (e.Root != 0 && e.Root != e.Id && _casts.TryGetValue(e.Root, out long castAt) && e.Time > castAt && e.Kind != HistoryKind.GodCommand && Time.unscaledTime - _youAt > 12f)
                {
                    _youAt = Time.unscaledTime;
                    PlayOne("ui_you", 0.22f);
                }
                if (e.Tier >= Tier.Historical && Time.unscaledTime - _lastStory > 4f) { _lastStory = Time.unscaledTime; PlayOne("ui_story", 0.25f); }
                if (e.Cell < 0 || !Sounding(e.Kind)) continue;
                double d = Vec3d.ArcDistance(dir, P.Grid.Center[e.Cell], P.Radius);
                float v = Mathf.Clamp01(1f - (float)(d / hear));
                float pan = PanOf(P, P.Grid.Center[e.Cell]);
                int id = (int)e.Id;
                switch (e.Kind)
                {
                    case HistoryKind.LightningStrike:
                        // fainter, deeper and later the farther it falls (the flash first); the god's own bolt, struck where the player looks, close and at once
                        if (e.RootKind == RootKind.You) { Shot("thunder", 0.8f * Mathf.Max(v, 0.6f), id, pan); told++; }
                        else if (v > 0f) { Shot("thunder", 0.8f * Mathf.Pow(v, 1.3f), id, pan, 1f - 0.1f * (1f - v), Mathf.Min(2.2f, (float)d / 1400f)); told++; }
                        break;
                    case HistoryKind.Quake:
                        {
                            // the ground's own rumble at once and going on (12 s, two aftershocks), the houses' creak and fall over it; by its size and its distance
                            float near = Mathf.Max(v, e.Magnitude > 6.5f ? 0.3f : 0f);
                            if (near <= 0f) break;
                            float size = Mathf.Clamp01((e.Magnitude - 4f) / 3.5f);
                            Shot("quake_rumble", 0.6f * (0.45f + 0.55f * size) * Mathf.Pow(near, 0.8f), id, pan * 0.5f);
                            Shot("quake", 0.45f * near, id, pan, 1f, 0.15f);
                            told++;
                            break;
                        }
                    case HistoryKind.Eruption:
                        {
                            // a mountain splitting: a blast as deep as thunder can be made, the ground rumbling after (the volcano's bed comes up under it)
                            float near = Mathf.Max(v, 0.35f);
                            Shot("thunder", 0.75f * near, id, pan, 0.55f);
                            Shot("quake_rumble", 0.55f * near, id + 1, pan * 0.5f, 0.9f, 0.25f);
                            told++;
                            break;
                        }
                    case HistoryKind.MeteorStruck:
                        // the loudest moment of the game: the blast, the buildings it flattens and the ground shaken, when the stone is seen to land
                        Landing("thunder", 0.9f, id, pan); Landing("house_fall", 0.5f + 0.35f * v, id + 1, pan, 1f, 0.12f); Landing("quake_rumble", 0.55f, id + 2, pan * 0.5f, 1f, 0.05f);
                        told++;
                        break;
                    case HistoryKind.BuildingBurned:
                        // (ArgA 2) a roof struck by lightning takes fire and falls in a few seconds after the bolt; a home burned in a raid falls as it is fired
                        if (e.ArgA == 2) { if (v > 0.1f) { Shot("house_fall", 0.25f + 0.4f * v, id, pan, 1.08f, Random.Range(2.5f, 4f)); told++; } }
                        else if (v > 0.2f) { Shot("house_fall", 0.3f + 0.5f * v, id, pan); told++; }
                        break;
                    case HistoryKind.HomesDrowned:
                        if (v > 0.2f) { Shot("splash", 0.3f + 0.4f * v, id, pan, 0.6f); Shot("house_fall", 0.25f + 0.35f * v, id, pan, 1f, 0.5f); told++; }
                        break;
                    case HistoryKind.HomesShaken:
                        // (session 32: a quake's fallen buildings) brought down through the shaking, not all in one instant
                        if (v > 0.1f) { Shot("house_fall", 0.3f + 0.35f * v, id, pan, 1f, Random.Range(0.3f, 1.8f)); told++; }
                        break;
                    case HistoryKind.Tsunami: case HistoryKind.StormSurge:
                        if (v > 0f) { Shot("splash", 0.3f + 0.45f * v, id, pan, 0.5f); Shot(e.Kind == HistoryKind.Tsunami ? "quake_rumble" : "wind_gust", 0.3f * v, id, pan, 0.8f, 0.3f); told++; }
                        break;
                    case HistoryKind.StormLandfall:
                        if (v > 0f) { Shot("wind_gust", 0.25f + 0.4f * v, id, pan); told++; }
                        break;
                    case HistoryKind.FireOut:
                        // a fire put out by the rain: its hiss, if it was near
                        if (v > 0.4f && e.Cell < P.RainToday.Length && P.RainToday[e.Cell] > 0.3f) Shot("douse", 0.15f + 0.25f * v, id, pan);
                        break;
                    case HistoryKind.PersonDied: if (v > 0.5f && (e.ArgA == 14 || e.ArgA == 5) && Time.unscaledTime - _lastCry > 3f) { _lastCry = Time.unscaledTime; Shot("shout", 0.25f + 0.35f * v, id, pan, 1f, Random.Range(0.4f, 1.5f)); } break;   // (a stone's or a quake's dead: a cry, now and then)
                    case HistoryKind.Raid: if (v > 0.3f) { Shot("shout", 0.3f + 0.5f * v, id, pan); told++; } break;
                }
            }
            _seenId = H.LastId;
        }

        /// <summary>The records that are heard where they happen (the rest are only measured for the story cue).</summary>
        static bool Sounding(HistoryKind k) =>
            k == HistoryKind.LightningStrike || k == HistoryKind.Quake || k == HistoryKind.Eruption || k == HistoryKind.MeteorStruck || k == HistoryKind.BuildingBurned
            || k == HistoryKind.HomesDrowned || k == HistoryKind.HomesShaken || k == HistoryKind.Tsunami || k == HistoryKind.StormSurge || k == HistoryKind.StormLandfall
            || k == HistoryKind.FireOut || k == HistoryKind.PersonDied || k == HistoryKind.Raid;

        readonly Dictionary<long, long> _casts = new Dictionary<long, long>();   // (the casts heard this session: their records and when)
        float _youAt = -99f;

        /// <summary>Where on the screen a place lies, as a pan (-0.7 left .. 0.7 right; 0 behind the camera).</summary>
        float PanOf(PlanetState P, Vec3d at)
        {
            if (Rig == null || Rig.Cam == null) return 0f;
            Vector3 sp = Rig.Cam.WorldToViewportPoint(PlanetGeo.Surface(P, at));
            return sp.z > 0f ? Mathf.Clamp((sp.x - 0.5f) * 1.4f, -0.7f, 0.7f) : 0f;
        }

        /// <summary>
        /// (session 32) A power heard as it is cast (the HUD's own cue sounds with the click): each with a sound of its own. Lightning, a quake, an eruption and
        /// a stone are heard by what they do (their strike, rupture, blast and landing); the others under the common deep cue - the rain coming on, warm air with
        /// a dry rustle, frost forming, a storm's first gust and its far thunder, the fields withering, a sickness's dull drum, the land's bell, the hunters'
        /// last howl (at sea a great splash), game come running, a pick on new stone.
        /// </summary>
        void CastHeard(in HistoryEvent e)
        {
            if (_casts.Count > 12) _casts.Clear();
            _casts[e.Id] = e.Time;
            var ck = (TC2.Core.Simulation.CommandKind)e.ArgA;
            int id = (int)e.Id;
            bool sea = e.ArgB == 1;
            switch (ck)
            {
                case TC2.Core.Simulation.CommandKind.GodLightning:
                case TC2.Core.Simulation.CommandKind.GodQuake:
                case TC2.Core.Simulation.CommandKind.GodEruption:
                case TC2.Core.Simulation.CommandKind.GodMeteor:
                    return;   // (their own records bring their sound; the stone's fall is heard from the click)
                case TC2.Core.Simulation.CommandKind.GodStorm:
                    Shot("wind_gust", 0.9f, id); Shot("thunder", 0.5f, id, 0f, 0.85f, 1.3f);
                    return;
            }
            Shot("ui_power", 0.42f, id);
            switch (ck)
            {
                case TC2.Core.Simulation.CommandKind.GodRain: Shot("rain_onset", 0.6f, id, 0f, 1f, 0.3f); break;
                case TC2.Core.Simulation.CommandKind.GodDrought: Shot("heat_haze", 0.4f, id, 0f, 0.9f, 0.2f); break;
                case TC2.Core.Simulation.CommandKind.GodWarm: Shot("heat_haze", 0.45f, id, 0f, 1f, 0.2f); break;
                case TC2.Core.Simulation.CommandKind.GodCold: Shot("frost", 0.55f, id, 0f, 1f, 0.2f); Shot("wind_gust", 0.3f, id, 0f, 1.15f, 0.1f); break;
                case TC2.Core.Simulation.CommandKind.GodPlague: PlayOne("ui_breaking", 0.34f); break;
                case TC2.Core.Simulation.CommandKind.GodBlight: Shot("wither", 0.6f, id, 0f, 1f, 0.15f); break;
                case TC2.Core.Simulation.CommandKind.GodBounty: PlayOne("ui_chime", 0.42f); break;
                case TC2.Core.Simulation.CommandKind.GodCull: if (sea) Shot("splash", 0.5f, id, 0f, 0.7f); else Shot("wolf_howl", 0.45f, id); break;
                case TC2.Core.Simulation.CommandKind.GodBring: if (sea) Shot("splash", 0.45f, id, 0f, 1.1f); else Shot("steps_grass", 0.5f, id, 0f, 1.15f, 0.2f); break;
                case TC2.Core.Simulation.CommandKind.GodOre: Shot("pick_stone", 0.6f, id, 0f, 1f, 0.2f); break;
            }
        }
    }
}
