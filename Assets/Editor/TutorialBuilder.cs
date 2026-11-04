using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the short tutorial level from a tiny map and adds the step-by-step
// guide. The scene starts as a copy of Level 0, so the player, camera, HUD
// and rules all match the real levels.
// Run it from the menu: Tools > Tutorial > Build Tutorial.
public static class TutorialBuilder
{
    internal const string ScenePath = "Assets/Scenes/Tutorial.unity";

    // # wall   . open space   S player start   E exit   * star
    // A low block to jump, a star to grab, then a wall with a gap at the
    // top. The gap and the exit hang up high until the map flips and drops
    // them to the floor.
    private static readonly string[] Layout =
    {
        "########################",
        "#..................EE...#",
        "#..................EE...#",
        "#..........#...........#",
        "#..........#...........#",
        "#.......*..#...........#",
        "#....##....#...........#",
        "#SS..##....#...........#",
        "########################",
    };

    // Long preview to memorize the map, and the flip only comes when the guide asks for it.
    private const float PreviewSeconds = 10f;
    private const float NoAutoFlipSeconds = 9999f;

    [MenuItem("Tools/Tutorial/Build Tutorial")]
    private static void BuildMenuItem()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Build();
        }
    }

    internal static bool Build()
    {
        if (!Level0Builder.Build(ScenePath, Layout, PreviewSeconds, NoAutoFlipSeconds))
        {
            return false;
        }

        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager.GetComponent<TutorialGuide>() == null)
        {
            manager.gameObject.AddComponent<TutorialGuide>();
        }

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        Debug.Log($"TutorialBuilder: saved {ScenePath}.");
        return true;
    }
}
