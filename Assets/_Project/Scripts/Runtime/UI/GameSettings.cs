using System;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Player settings, saved in PlayerPrefs so they survive restarts. Gameplay code reads them directly
    /// (mouse look, field of view); display and audio settings are applied through <see cref="Apply"/>.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "ElfAndSafety.Settings.";

        public const float MinSensitivity = 0.02f, MaxSensitivity = 0.4f, DefaultSensitivity = 0.12f;
        public const float MinFieldOfView = 60f, MaxFieldOfView = 100f, DefaultFieldOfView = 75f;
        public const float DefaultVolume = 0.8f;

        /// <summary>Raised whenever any setting changes.</summary>
        public static event Action Changed;

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(Prefix + "MouseSensitivity", DefaultSensitivity);
            set => SetFloat("MouseSensitivity", Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        public static bool InvertY
        {
            get => GetBool("InvertY", false);
            set => SetBool("InvertY", value);
        }

        public static float FieldOfView
        {
            get => PlayerPrefs.GetFloat(Prefix + "FieldOfView", DefaultFieldOfView);
            set => SetFloat("FieldOfView", Mathf.Clamp(value, MinFieldOfView, MaxFieldOfView));
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "MasterVolume", DefaultVolume);
            set => SetFloat("MasterVolume", Mathf.Clamp01(value));
        }

        public static bool Fullscreen
        {
            get => GetBool("Fullscreen", true);
            set => SetBool("Fullscreen", value);
        }

        public static bool VSync
        {
            get => GetBool("VSync", true);
            set => SetBool("VSync", value);
        }

        /// <summary>Index into QualitySettings.names; -1 keeps the project default.</summary>
        public static int QualityLevel
        {
            get => PlayerPrefs.GetInt(Prefix + "QualityLevel", -1);
            set => SetInt("QualityLevel", value);
        }

        /// <summary>Index into <see cref="Screen.resolutions"/>; -1 keeps the current resolution.</summary>
        public static int ResolutionIndex
        {
            get => PlayerPrefs.GetInt(Prefix + "ResolutionIndex", -1);
            set => SetInt("ResolutionIndex", value);
        }

        public static bool ShowControls
        {
            get => GetBool("ShowControls", true);
            set => SetBool("ShowControls", value);
        }

        public static bool ShowWishList
        {
            get => GetBool("ShowWishList", true);
            set => SetBool("ShowWishList", value);
        }

        public static void ResetToDefaults()
        {
            foreach (string key in new[] { "MouseSensitivity", "InvertY", "FieldOfView", "MasterVolume", "Fullscreen", "VSync", "QualityLevel", "ResolutionIndex", "ShowControls", "ShowWishList" })
            {
                PlayerPrefs.DeleteKey(Prefix + key);
            }
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Pushes volume, display and quality settings to Unity.</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            int quality = QualityLevel;
            if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(quality, applyExpensiveChanges: true);
            }

            // Display changes only mean something in a player build, not in the editor's game view.
            if (!Application.isEditor)
            {
                Resolution[] resolutions = Screen.resolutions;
                int index = ResolutionIndex;
                FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (index >= 0 && index < resolutions.Length)
                {
                    Screen.SetResolution(resolutions[index].width, resolutions[index].height, mode);
                }
                else
                {
                    Screen.fullScreenMode = mode;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStartup() => Apply();

        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;

        static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            Saved();
        }

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            Saved();
        }

        static void Saved()
        {
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }
    }
}
