using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TC2.Game
{
    /// <summary>
    /// (F11.2) The player's settings: the world's sound and how loud it is, how thick the clouds are drawn, lighter shadows from high up (two
    /// cascades above 3 km: 14 §7), the picture's detail (its render scale) - kept between visits (PlayerPrefs: on the Web the browser's own store)
    /// and applied to the view's tunables. The view only: nothing here touches the world. Shots, benches and tests never load them, so their
    /// pictures and numbers stay their own.
    /// </summary>
    public static class Settings
    {
        /// <summary>x the clouds the weather makes (0 none, 1 as they are, 1.5 thicker).</summary>
        public static float Clouds = 1f;
        /// <summary>The picture's render scale (1 full; lower is lighter for slow machines). Applied in players only: the editor keeps its asset.</summary>
        public static float Detail = 1f;
        /// <summary>(session 31) x the camera's turning by a drag.</summary>
        public static float CamSpeed = 1f;
        /// <summary>(session 31) x the camera's shaking (0 none).</summary>
        public static float Shake = 1f;
        /// <summary>(session 31) x the interface's size.</summary>
        public static float UiScale = 1f;
        /// <summary>(session 31) Whether the guide has been walked or skipped.</summary>
        public static bool GuideSeen;
        public static bool Loaded { get; private set; }

        const string Prefix = "tc2.";

        /// <summary>Reads the kept settings (defaults where none are kept, or the store cannot be read) and applies them.</summary>
        public static void Load()
        {
            try
            {
                AudioView.Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", AudioView.Volume));
                AudioView.Muted = PlayerPrefs.GetInt(Prefix + "muted", AudioView.Muted ? 1 : 0) == 1;
                Clouds = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "clouds", Clouds), 0f, 1.5f);
                Sky.ShadowLite = PlayerPrefs.GetInt(Prefix + "lightshadows", Sky.ShadowLite ? 1 : 0) == 1;
                Detail = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "detail", Detail), 0.5f, 1f);
                CamSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "camspeed", CamSpeed), 0.5f, 2f);
                Shake = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "shake", Shake));
                UiScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "uiscale", UiScale), 1f, 1.3f);
                GuideSeen = PlayerPrefs.GetInt(Prefix + "guide", 0) == 1;
                Hud.Names = PlayerPrefs.GetInt(Prefix + "names", 1) == 1;
            }
            catch (System.Exception) { }
            Loaded = true;
            Apply();
        }

        /// <summary>Keeps the settings as they are now (the sound's toggle and the M key change them too).</summary>
        public static void Save()
        {
            try
            {
                PlayerPrefs.SetFloat(Prefix + "volume", AudioView.Volume);
                PlayerPrefs.SetInt(Prefix + "muted", AudioView.Muted ? 1 : 0);
                PlayerPrefs.SetFloat(Prefix + "clouds", Clouds);
                PlayerPrefs.SetInt(Prefix + "lightshadows", Sky.ShadowLite ? 1 : 0);
                PlayerPrefs.SetFloat(Prefix + "detail", Detail);
                PlayerPrefs.SetFloat(Prefix + "camspeed", CamSpeed);
                PlayerPrefs.SetFloat(Prefix + "shake", Shake);
                PlayerPrefs.SetFloat(Prefix + "uiscale", UiScale);
                PlayerPrefs.SetInt(Prefix + "guide", GuideSeen ? 1 : 0);
                PlayerPrefs.SetInt(Prefix + "names", Hud.Names ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }
        }

        /// <summary>(session 31) The worlds kept: three copies in the browser's store, each with the line the menu shows.</summary>
        public const int KeptSlots = 3;
        public static string KeptLabel(int i) { try { return PlayerPrefs.GetString(Prefix + "kept" + i, ""); } catch (System.Exception) { return ""; } }
        public static void SetKept(int i, string label) { try { PlayerPrefs.SetString(Prefix + "kept" + i, label); PlayerPrefs.Save(); } catch (System.Exception) { } }
        public static bool AnyKept() { for (int i = 0; i < KeptSlots; i++) if (KeptLabel(i).Length > 0) return true; return false; }

        // (session 31) the small hints, each said once
        static readonly System.Collections.Generic.HashSet<string> Said = new System.Collections.Generic.HashSet<string>();
        static bool _saidRead;
        static void ReadSaid() { if (_saidRead) return; _saidRead = true; try { foreach (var k in PlayerPrefs.GetString(Prefix + "hints", "").Split(',')) if (k.Length > 0) Said.Add(k); } catch (System.Exception) { } }
        public static bool HintSaid(string key) { ReadSaid(); return Said.Contains(key); }
        public static void SayHint(string key) { ReadSaid(); Said.Add(key); try { PlayerPrefs.SetString(Prefix + "hints", string.Join(",", Said)); PlayerPrefs.Save(); } catch (System.Exception) { } }
        public static void ClearHints() { Said.Clear(); _saidRead = true; try { PlayerPrefs.DeleteKey(Prefix + "hints"); } catch (System.Exception) { } }

        /// <summary>What the view reads directly is set here; the clouds' thickness is read by <see cref="CloudView"/> each frame.</summary>
        public static void Apply()
        {
#if !UNITY_EDITOR
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = Mathf.Clamp(Detail, 0.5f, 1f);
#endif
        }
    }
}
