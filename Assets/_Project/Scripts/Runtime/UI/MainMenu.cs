using UnityEngine;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// The title screen: play solo, host or join a co-op game, how to play, settings, quit.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject howToPlayPanel;
        [SerializeField] SettingsMenu settings;
        [SerializeField] InputField joinAddress;
        [SerializeField] Text messageLabel;

        [Header("Buttons")]
        [SerializeField] Button playSoloButton;
        [SerializeField] Button hostButton;
        [SerializeField] Button joinButton;
        [SerializeField] Button howToPlayButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;
        [SerializeField] Button howToPlayBackButton;

        void Awake()
        {
            playSoloButton.onClick.AddListener(PlaySolo);
            hostButton.onClick.AddListener(HostGame);
            joinButton.onClick.AddListener(JoinGame);
            howToPlayButton.onClick.AddListener(() => Show(howToPlayPanel));
            settingsButton.onClick.AddListener(settings.Open);
            quitButton.onClick.AddListener(GameLaunch.Quit);
            howToPlayBackButton.onClick.AddListener(() => Show(mainPanel));
        }

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            joinAddress.text = GameLaunch.JoinAddress;
            string message = GameLaunch.TakeMenuMessage();
            messageLabel.text = message ?? string.Empty;
            Show(mainPanel);
        }

        public void PlaySolo() => GameLaunch.StartGame(LaunchMode.Solo);

        public void HostGame() => GameLaunch.StartGame(LaunchMode.Host);

        public void JoinGame()
        {
            GameLaunch.JoinAddress = joinAddress.text;
            GameLaunch.StartGame(LaunchMode.Join);
        }

        void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            howToPlayPanel.SetActive(panel == howToPlayPanel);
            settings.gameObject.SetActive(false);
        }
    }
}
