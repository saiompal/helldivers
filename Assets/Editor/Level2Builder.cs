using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Always derive the level rig and appearance from Level 1. The original scene
// on disk is never saved or modified by this builder.
public static class Level2Builder
{
    private const string Reference = "Assets/Scenes/Level1.unity";
    private const string Level2 = "Assets/Scenes/Level2.unity";

    [MenuItem("Tools/Level 2/Build or Refresh Ring Exchange")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene reference = EditorSceneManager.OpenScene(Reference, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(reference, Level2, true))
        {
            Debug.LogError("Cannot copy the Level 1 rig into Level 2.");
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(Level2, OpenSceneMode.Single);
        GameManager manager = Object.FindFirstObjectByType<GameManager>();
        if (manager == null) { Debug.LogError("Level 1 rig requires GameManager."); return; }
        Level2SceneSetup setup = manager.GetComponent<Level2SceneSetup>();
        if (setup == null) setup = manager.gameObject.AddComponent<Level2SceneSetup>();
        setup.Rebuild();
        if (!HudBuilder.BuildHud()) Debug.LogWarning("HUD refresh failed; inspect original bindings.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Ring Exchange saved to Assets/Scenes/Level2.unity. Press Play to test.");
    }
}
