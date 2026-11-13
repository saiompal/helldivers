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
    // A low block to jump, a star on the far side, and an elevated exit.
    // The automatic flip brings the exit down to the floor. The rest of
    // the route is open so the lesson focuses on movement and flipping.
    private static readonly string[] Layout =
    {
        "############################",
        "#......................EE..#",
        "#......................EE..#",
        "#..........................#",
        "#..........................#",
        "#..........................#",
        "#..........................#",
        "#..........................#",
        "#.....##...................#",
        "#.SS..##...*...............#",
        "############################",
    };

    // The guide starts this normal automatic cycle five seconds after the first star.
    private const float PreviewSeconds = 10f;
    private const float SecondsBetweenFlips = 8f;

    public static void BuildFromCommandLine()
    {
        if (!Build()) throw new System.InvalidOperationException("Could not rebuild the tutorial.");
    }

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
        if (!Level0Builder.Build(ScenePath, Layout, PreviewSeconds, SecondsBetweenFlips))
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
