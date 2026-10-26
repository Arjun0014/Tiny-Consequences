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
    /// </summary>
    public sealed class AudioView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        public static bool Muted;
        public static float Volume = 0.8f;

        static readonly string[] Beds = { "amb_wind_high", "amb_wind_winter", "amb_forest_day", "amb_forest_night", "amb_coast", "amb_river", "amb_rain", "amb_storm",
                                          "amb_village_day", "amb_village_night", "amb_fire", "amb_wildfire", "amb_volcano", "amb_sea_open", "amb_herd" };
        const int WindHigh = 0, WindCold = 1, ForestDay = 2, ForestNight = 3, Coast = 4, River = 5, Rain = 6, Storm = 7, VillageDay = 8, VillageNight = 9,
                  Fire = 10, Wildfire = 11, Volcano = 12, SeaOpen = 13, Herd = 14;
        AudioSource[] _bed;
        readonly float[] _want = new float[Beds.Length];
        readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        AudioSource[] _shot;
        int _nextShot;
        long _seenId = -1;
        float _lastStory;

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
            _shot = new AudioSource[8];
            for (int i = 0; i < _shot.Length; i++) { _shot[i] = gameObject.AddComponent<AudioSource>(); _shot[i].playOnAwake = false; _shot[i].spatialBlend = 0f; }
            foreach (var n in new[] { "thunder", "quake", "house_fall", "wolf_howl", "dog_bark", "shout" }) Load(n, 3);
            foreach (var n in new[] { "ui_power", "ui_story", "ui_you", "ui_click" }) Load(n, 1);
        }

        void Load(string name, int takes)
        {
            var l = new List<AudioClip>();
            var one = Resources.Load<AudioClip>("Audio/" + name);
            if (one != null) l.Add(one);
            for (int k = 1; k <= takes; k++) { var c = Resources.Load<AudioClip>("Audio/" + name + "_" + k); if (c != null) l.Add(c); }
            _clips[name] = l.ToArray();
        }

        void Play(string name, float volume, int pick)
        {
            if (volume < 0.02f || !_clips.TryGetValue(name, out var a) || a.Length == 0) return;
            var src = _shot[_nextShot]; _nextShot = (_nextShot + 1) % _shot.Length;
            src.clip = a[(pick & 0x7fffffff) % a.Length];
            src.volume = Mathf.Clamp01(volume) * Volume;
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
            var w = Host != null ? Host.World : null;
            bool quiet = w == null || Host.CatchingUp || Muted || Rig == null;
            for (int i = 0; i < _want.Length; i++) _want[i] = 0f;
            if (!quiet) Listen(w);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 1.5f);   // (beds drift to their mix over a second or two)
            for (int i = 0; i < _bed.Length; i++)
            {
                var src = _bed[i];
                if (src.clip == null) continue;
                src.volume = Mathf.Lerp(src.volume, _want[i] * Volume, k);
                if (src.volume > 0.005f && !src.isPlaying) { src.time = Random.Range(0f, Mathf.Max(0.01f, src.clip.length - 0.1f)); src.Play(); }
                else if (src.volume <= 0.005f && src.isPlaying && _want[i] == 0f) src.Pause();
            }
            if (quiet) { if (w != null) _seenId = w.History.LastId; return; }
            Happenings(w);
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
            bool storm = P.Storms.Count > 0 && rain > 0.5f;
            _want[WindHigh] = 0.15f + 0.55f * high;
            _want[WindCold] = cold * (0.25f + 0.35f * near);
            _want[ForestDay] = near * wood * day * 0.7f;
            _want[ForestNight] = near * wood * (1f - day) * 0.6f;
            _want[Coast] = shore ? 0.25f + 0.45f * near : 0f;
            _want[River] = near * river * 0.55f;
            _want[Rain] = rain * (storm ? 0.3f : 0.75f) * (0.4f + 0.6f * near);
            _want[Storm] = storm ? rain * 0.8f : 0f;
            _want[VillageDay] = near * village * day * 0.75f;
            _want[VillageNight] = near * village * (1f - day) * 0.6f;
            _want[Herd] = near * village * day * 0.25f;
            _want[Fire] = fire * near * 0.8f;
            _want[Wildfire] = Mathf.Clamp01(burning / 12f) * (0.3f + 0.4f * (1f - near));
            _want[Volcano] = volcano * 0.8f;
            _want[SeaOpen] = !land && !shore ? 0.2f + 0.4f * near : 0f;
        }

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
                if (e.Kind == HistoryKind.GodCommand) { Play("ui_power", 0.9f, (int)e.Id); told++; continue; }
                if (e.Cell < 0) continue;
                double d = Vec3d.ArcDistance(dir, P.Grid.Center[e.Cell], P.Radius);
                float v = Mathf.Clamp01(1f - (float)(d / hear));
                switch (e.Kind)
                {
                    case HistoryKind.LightningStrike: if (v > 0f) { Play("thunder", 0.3f + 0.7f * v, (int)e.Id); told++; } break;
                    case HistoryKind.Quake: if (v > 0f || e.Magnitude > 6.5f) { Play("quake", 0.4f + 0.6f * Mathf.Max(v, 0.3f), (int)e.Id); told++; } break;
                    case HistoryKind.MeteorStruck: { Play("thunder", 1f, (int)e.Id); Play("house_fall", 0.6f + 0.4f * v, (int)e.Id + 1); told++; break; }
                    case HistoryKind.BuildingBurned: case HistoryKind.HomesDrowned: if (v > 0.2f) { Play("house_fall", 0.4f + 0.6f * v, (int)e.Id); told++; } break;
                    case HistoryKind.Raid: if (v > 0.3f) { Play("shout", 0.3f + 0.5f * v, (int)e.Id); told++; } break;
                }
                if (e.Tier >= Tier.Historical && Time.unscaledTime - _lastStory > 4f && e.Kind != HistoryKind.GodCommand) { _lastStory = Time.unscaledTime; Play("ui_story", 0.25f, (int)e.Id); }
            }
            _seenId = H.LastId;
        }
    }
}
