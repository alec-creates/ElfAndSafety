using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// The settings panel, used by both the main menu and the pause menu. Each control writes straight to
    /// <see cref="GameSettings"/>, which saves and applies it.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] Slider sensitivity;
        [SerializeField] Text sensitivityValue;
        [SerializeField] Toggle invertY;
        [SerializeField] Slider fieldOfView;
        [SerializeField] Text fieldOfViewValue;
        [SerializeField] Slider volume;
        [SerializeField] Text volumeValue;
        [SerializeField] Toggle fullscreen;
        [SerializeField] Dropdown resolution;
        [SerializeField] Dropdown quality;
        [SerializeField] Toggle vSync;
        [SerializeField] Toggle showControls;
        [SerializeField] Toggle showWishList;
        [SerializeField] Button resetButton;
        [SerializeField] Button backButton;
        [Tooltip("Shown again when Back is pressed.")]
        [SerializeField] GameObject returnTo;

        bool refreshing;
        readonly List<int> resolutionIndices = new List<int>();

        void Awake()
        {
            sensitivity.minValue = GameSettings.MinSensitivity;
            sensitivity.maxValue = GameSettings.MaxSensitivity;
            fieldOfView.minValue = GameSettings.MinFieldOfView;
            fieldOfView.maxValue = GameSettings.MaxFieldOfView;
            fieldOfView.wholeNumbers = true;
            volume.minValue = 0f;
            volume.maxValue = 1f;

            sensitivity.onValueChanged.AddListener(v => Set(() => GameSettings.MouseSensitivity = v));
            invertY.onValueChanged.AddListener(v => Set(() => GameSettings.InvertY = v));
            fieldOfView.onValueChanged.AddListener(v => Set(() => GameSettings.FieldOfView = v));
            volume.onValueChanged.AddListener(v => Set(() => GameSettings.MasterVolume = v));
            fullscreen.onValueChanged.AddListener(v => Set(() => GameSettings.Fullscreen = v));
            vSync.onValueChanged.AddListener(v => Set(() => GameSettings.VSync = v));
            showControls.onValueChanged.AddListener(v => Set(() => GameSettings.ShowControls = v));
            showWishList.onValueChanged.AddListener(v => Set(() => GameSettings.ShowWishList = v));
            quality.onValueChanged.AddListener(i => Set(() => GameSettings.QualityLevel = i));
            resolution.onValueChanged.AddListener(i => Set(() => GameSettings.ResolutionIndex = i < resolutionIndices.Count ? resolutionIndices[i] : -1));
            resetButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); Refresh(); });
            backButton.onClick.AddListener(Close);
        }

        void OnEnable() => Refresh();

        public void Open()
        {
            gameObject.SetActive(true);
            if (returnTo != null) returnTo.SetActive(false);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (returnTo != null) returnTo.SetActive(true);
        }

        void Set(System.Action change)
        {
            if (refreshing) return;
            change();
            RefreshValueLabels();
        }

        /// <summary>Shows the saved values without writing them back.</summary>
        void Refresh()
        {
            refreshing = true;

            sensitivity.value = GameSettings.MouseSensitivity;
            invertY.isOn = GameSettings.InvertY;
            fieldOfView.value = GameSettings.FieldOfView;
            volume.value = GameSettings.MasterVolume;
            fullscreen.isOn = GameSettings.Fullscreen;
            vSync.isOn = GameSettings.VSync;
            showControls.isOn = GameSettings.ShowControls;
            showWishList.isOn = GameSettings.ShowWishList;

            quality.ClearOptions();
            quality.AddOptions(QualitySettings.names.ToList());
            quality.value = GameSettings.QualityLevel >= 0 ? GameSettings.QualityLevel : QualitySettings.GetQualityLevel();

            // One entry per size (refresh rates collapsed), largest last.
            resolutionIndices.Clear();
            var labels = new List<string>();
            Resolution[] all = Screen.resolutions;
            for (int i = 0; i < all.Length; i++)
            {
                string label = $"{all[i].width} x {all[i].height}";
                if (labels.Contains(label)) continue;
                labels.Add(label);
                resolutionIndices.Add(i);
            }
            if (labels.Count == 0)
            {
                labels.Add($"{Screen.width} x {Screen.height}");
                resolutionIndices.Add(-1);
            }
            resolution.ClearOptions();
            resolution.AddOptions(labels);
            int saved = resolutionIndices.IndexOf(GameSettings.ResolutionIndex);
            resolution.value = saved >= 0 ? saved : labels.Count - 1;

            refreshing = false;
            RefreshValueLabels();
        }

        void RefreshValueLabels()
        {
            sensitivityValue.text = (GameSettings.MouseSensitivity * 10f).ToString("0.0");
            fieldOfViewValue.text = $"{GameSettings.FieldOfView:0}°";
            volumeValue.text = $"{GameSettings.MasterVolume * 100f:0}%";
        }
    }
}
