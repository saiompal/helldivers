using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuBuilder
{
    public static void BuildAndCapture()
    {
        BuildFromCommandLine();
        PlayerProgress.UseTestStorage(true);
        PlayerProgress.ResetLocalProgress();
        try
        {
            Object.FindAnyObjectByType<MainMenu>().ShowLevelMap();
            ReferenceUiReview.Capture("flowing-level-map", 1280, 720);
        }
        finally
        {
            PlayerProgress.ResetLocalProgress();
            PlayerProgress.UseTestStorage(false);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
    }

    [MenuItem("Tools/UI/Rebuild Main Menu")]
    public static void BuildFromCommandLine()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        var view = camera.GetComponent<Camera>(); view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = GameUi.Dark;
        var rect = GameUi.Rect("Canvas", null, Vector2.zero, new Vector2(1920, 1080));
        var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rect.gameObject.AddComponent<GraphicRaycaster>();
        rect.gameObject.AddComponent<MainMenu>().RebuildUi();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        var paths = new[] { "MainMenu", "Tutorial" }
            .Concat(Enumerable.Range(0, LevelCatalog.Count).Select(LevelCatalog.GetSceneName));
        EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene("Assets/Scenes/" + p + ".unity", true)).ToArray();
        AssetDatabase.SaveAssets();
    }
}
