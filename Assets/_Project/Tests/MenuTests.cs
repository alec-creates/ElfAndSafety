using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// The main menu, pause menu and settings. Settings are the developer's real PlayerPrefs, so every test
    /// that changes one puts the old values back.
    /// </summary>
    public class MenuTests
    {
        float sensitivity, fieldOfView, volume;
        bool invertY, showControls, showWishList;

        [SetUp]
        public void RememberSettings()
        {
            sensitivity = GameSettings.MouseSensitivity;
            fieldOfView = GameSettings.FieldOfView;
            volume = GameSettings.MasterVolume;
            invertY = GameSettings.InvertY;
            showControls = GameSettings.ShowControls;
            showWishList = GameSettings.ShowWishList;
        }

        [UnityTearDown]
        public IEnumerator RestoreSettings()
        {
            GameSettings.MouseSensitivity = sensitivity;
            GameSettings.FieldOfView = fieldOfView;
            GameSettings.MasterVolume = volume;
            GameSettings.InvertY = invertY;
            GameSettings.ShowControls = showControls;
            GameSettings.ShowWishList = showWishList;
            Time.timeScale = 1f;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                if (manager.IsListening) manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }
            yield return null;
        }

        [Test]
        public void Settings_AreSavedAndClamped()
        {
            GameSettings.MouseSensitivity = 0.3f;
            GameSettings.FieldOfView = 500f;
            GameSettings.MasterVolume = -1f;
            GameSettings.InvertY = true;

            Assert.AreEqual(0.3f, GameSettings.MouseSensitivity, 0.0001f);
            Assert.AreEqual(GameSettings.MaxFieldOfView, GameSettings.FieldOfView, "Clamped to the slider range");
            Assert.AreEqual(0f, GameSettings.MasterVolume);
            Assert.AreEqual(0f, AudioListener.volume, "Volume is applied straight away");
            Assert.IsTrue(GameSettings.InvertY);
            Assert.AreEqual(0.3f, PlayerPrefs.GetFloat("ElfAndSafety.Settings.MouseSensitivity"), 0.0001f, "Saved to PlayerPrefs");
        }

        [UnityTest]
        public IEnumerator FieldOfView_AppliesToThePlayerCamera()
        {
            yield return SceneManager.LoadSceneAsync(GameLaunch.WorkshopScene);
            Camera view = Object.FindAnyObjectByType<FirstPersonController>().CameraPivot.GetComponentInChildren<Camera>();

            GameSettings.FieldOfView = 90f;
            Assert.AreEqual(90f, view.fieldOfView, 0.01f);
            GameSettings.FieldOfView = 70f;
            Assert.AreEqual(70f, view.fieldOfView, 0.01f);
        }

        [UnityTest]
        public IEnumerator MainMenu_PlaySolo_LoadsASoloWorkshop()
        {
            yield return SceneManager.LoadSceneAsync(GameLaunch.MainMenuScene);
            var menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(menu, "The main menu scene has a menu");

            menu.PlaySolo();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == GameLaunch.WorkshopScene && Object.FindAnyObjectByType<WorkdayManager>() != null);
            yield return null;

            Assert.IsNull(NetworkManager.Singleton, "Solo needs no networking");
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInteractor>().Length);
            Assert.AreEqual(ShiftState.NotStarted, Object.FindAnyObjectByType<WorkdayManager>().State);
        }

        [UnityTest]
        public IEnumerator MainMenu_Host_LoadsTheWorkshopAndHosts()
        {
            yield return SceneManager.LoadSceneAsync(GameLaunch.MainMenuScene);
            Object.FindAnyObjectByType<MainMenu>().HostGame();

            yield return WaitFor(() => NetworkSession.IsServer && NetworkPlayer.Find(NetworkManager.ServerClientId) != null);
            Assert.IsTrue(NetworkSession.IsServer, "Hosting started from the menu");
            Assert.AreEqual(GameLaunch.WorkshopScene, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesSoloPlay_AndResumes()
        {
            yield return SceneManager.LoadSceneAsync(GameLaunch.WorkshopScene);
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            var workday = Object.FindAnyObjectByType<WorkdayManager>();
            Assert.IsTrue(workday.StartShift());

            pause.Open();
            Assert.IsTrue(PauseMenu.IsPaused);
            Assert.AreEqual(0f, Time.timeScale, "Solo play freezes");

            // The time scale applies from the next frame; after that the clock stands still.
            yield return null;
            float remaining = workday.RemainingSeconds;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.AreEqual(remaining, workday.RemainingSeconds, "The shift timer stops while paused");

            pause.Resume();
            Assert.IsFalse(PauseMenu.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Pause_MainMenu_GoesBackToTheTitleScreen()
        {
            yield return SceneManager.LoadSceneAsync(GameLaunch.WorkshopScene);
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            pause.Open();
            pause.GoToMainMenu();

            yield return WaitFor(() => SceneManager.GetActiveScene().name == GameLaunch.MainMenuScene);
            Assert.AreEqual(GameLaunch.MainMenuScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale, "Unpaused on the way out");
            Assert.IsFalse(PauseMenu.IsPaused);
        }

        [UnityTest]
        public IEnumerator Hud_WishListFollowsTheSetting()
        {
            yield return SceneManager.LoadSceneAsync(GameLaunch.WorkshopScene);
            GameObject wishList = GameObject.Find("Wish List");
            Assert.IsNotNull(wishList);

            GameSettings.ShowWishList = false;
            yield return null;
            Assert.IsFalse(wishList.activeSelf);

            GameSettings.ShowWishList = true;
            yield return null;
            Assert.IsTrue(wishList.activeSelf);
            StringAssert.Contains("WISH LIST", wishList.GetComponent<UnityEngine.UI.Text>().text);
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 6f)
        {
            float waited = 0f;
            while (!condition() && waited < timeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), "Timed out waiting");
        }
    }
}
