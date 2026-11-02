using UnityEditor;
using UnityEditor.SceneManagement;

namespace ElfAndSafety.EditorTools
{
    /// <summary>
    /// Makes the Play button always start in the Workshop scene, whatever scene is open for editing.
    /// Toggle with Elf & Safety > Always Play From Workshop.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromWorkshop
    {
        const string MenuPath = "Elf & Safety/Always Play From Workshop";
        const string PrefKey = "ElfAndSafety.PlayFromWorkshop";
        const string ScenePath = "Assets/_Project/Scenes/Workshop.unity";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromWorkshop()
        {
            // Delayed so the asset database is ready when the editor first loads.
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        }
    }
}
