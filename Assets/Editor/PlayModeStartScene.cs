using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

    static PlayModeStartScene()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            Apply();
    }

    private static void Apply()
    {
        SceneAsset menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
        if (menu != null && EditorSceneManager.playModeStartScene != menu)
            EditorSceneManager.playModeStartScene = menu;
    }
}
