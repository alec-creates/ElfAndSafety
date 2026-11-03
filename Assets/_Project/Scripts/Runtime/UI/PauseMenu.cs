using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// Esc in the Workshop: Resume, Settings, Main Menu, Quit. Solo play freezes while paused (the shift timer
    /// included); a co-op game can't be paused for everyone, so there it only frees the mouse and stops this
    /// player's input.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] SettingsMenu settings;
        [SerializeField] WorkdayManager workday;
        [SerializeField] Text noteLabel;

        [Header("Buttons")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] Button quitButton;

        /// <summary>True when a pause menu is in the scene: Esc opens it instead of just releasing the mouse.</summary>
        public static bool HandlesEscape { get; private set; }

        public static bool IsPaused { get; private set; }

        void Awake()
        {
            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(settings.Open);
            mainMenuButton.onClick.AddListener(GoToMainMenu);
            quitButton.onClick.AddListener(GameLaunch.Quit);
            panel.SetActive(false);
            settings.gameObject.SetActive(false);
        }

        void OnEnable() => HandlesEscape = true;

        void OnDisable()
        {
            HandlesEscape = false;
            if (IsPaused)
            {
                IsPaused = false;
                Time.timeScale = 1f;
            }
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (settings.gameObject.activeSelf) settings.Close();
            else if (IsPaused) Resume();
            else if (workday == null || !workday.VerdictVisible) Open();
        }

        public void Open()
        {
            if (IsPaused) return;

            IsPaused = true;
            panel.SetActive(true);
            if (!NetworkSession.IsOnline) Time.timeScale = 0f;
            if (noteLabel != null) noteLabel.text = NetworkSession.IsOnline ? "Co-op keeps running while you're paused" : string.Empty;

            foreach (var input in FindObjectsByType<PlayerInputReader>())
            {
                input.AllowCursorCapture = false;
                input.LockCursor(false);
            }
        }

        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            panel.SetActive(false);
            settings.gameObject.SetActive(false);
            Time.timeScale = 1f;

            // Back to the game, unless Santa's verdict is waiting for a click.
            if (workday != null && workday.VerdictVisible) return;
            foreach (var input in FindObjectsByType<PlayerInputReader>())
            {
                if (!input.enabled) continue;
                input.AllowCursorCapture = true;
                input.LockCursor(true);
            }
        }

        public void GoToMainMenu()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            GameLaunch.ReturnToMainMenu();
        }
    }
}
