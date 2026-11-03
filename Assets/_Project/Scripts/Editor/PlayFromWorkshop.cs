using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ElfAndSafety.EditorTools
{
    /// <summary>
    /// Makes the Play button always start in the Workshop scene, whatever scene is open for editing.
    /// Toggle with Elf & Safety > Always Play From Workshop.
    /// Skipped for Unity Test Runner sessions: forcing a start scene there replaces the runner's own
    /// InitTestScene, so PlayMode tests never start and the run hangs.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromWorkshop
    {
        const string MenuPath = "Elf & Safety/Always Play From Workshop";
        const string PrefKey = "ElfAndSafety.PlayFromWorkshop";
        const string ScenePath = "Assets/_Project/Scenes/Workshop.unity";
        const string TestRunnerScenePrefix = "InitTestScene";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromWorkshop()
        {
            // Decide at the moment Play is pressed, when the scene about to play is known.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool isTestRun = SceneManager.GetActiveScene().name.StartsWith(TestRunnerScenePrefix);
                EditorSceneManager.playModeStartScene = Enabled && !isTestRun ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}
