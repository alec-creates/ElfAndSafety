using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElfAndSafety
{
    public enum LaunchMode
    {
        Solo,
        Host,
        Join
    }

    /// <summary>
    /// Moves between the main menu and the Workshop. The menu says how to start (solo, host, join); the
    /// Workshop's <see cref="NetworkSession"/> picks that up when it loads. Messages for the menu (such as a
    /// failed join) are carried back the same way.
    /// </summary>
    public static class GameLaunch
    {
        public const string MainMenuScene = "MainMenu";
        public const string WorkshopScene = "Workshop";
        const string AddressKey = "ElfAndSafety.JoinAddress";

        static string menuMessage;

        public static LaunchMode PendingMode { get; private set; } = LaunchMode.Solo;

        /// <summary>The address to join, remembered between sessions.</summary>
        public static string JoinAddress
        {
            get => PlayerPrefs.GetString(AddressKey, "127.0.0.1");
            set
            {
                PlayerPrefs.SetString(AddressKey, string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim());
                PlayerPrefs.Save();
            }
        }

        public static void StartGame(LaunchMode mode)
        {
            PendingMode = mode;
            Time.timeScale = 1f;
            SceneManager.LoadScene(WorkshopScene);
        }

        /// <summary>Workshop side: how this load was meant to start. Resets to solo so it's used once.</summary>
        internal static LaunchMode TakePendingMode()
        {
            LaunchMode mode = PendingMode;
            PendingMode = LaunchMode.Solo;
            return mode;
        }

        /// <summary>
        /// Back to the main menu, leaving a co-op session first if there is one. Without a main menu in the
        /// build (e.g. a scene opened on its own), reloads the Workshop instead.
        /// </summary>
        public static void ReturnToMainMenu(string message = null)
        {
            if (NetworkSession.IsOnline && NetworkSession.Current != null)
            {
                NetworkSession.Current.Leave();
                return;
            }

            LoadMainMenu(message);
        }

        internal static void LoadMainMenu(string message)
        {
            Time.timeScale = 1f;
            if (message != null) menuMessage = message;
            SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded(MainMenuScene) ? MainMenuScene : WorkshopScene);
        }

        /// <summary>The message for the main menu to show, once.</summary>
        public static string TakeMenuMessage()
        {
            string message = menuMessage;
            menuMessage = null;
            return message;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
