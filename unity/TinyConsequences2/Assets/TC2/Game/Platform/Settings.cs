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
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }
        }

        /// <summary>What the view reads directly is set here; the clouds' thickness is read by <see cref="CloudView"/> each frame.</summary>
        public static void Apply()
        {
#if !UNITY_EDITOR
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = Mathf.Clamp(Detail, 0.5f, 1f);
#endif
        }
    }
}
