using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Always derive the level rig and appearance from Level 0. The original scene
// on disk is never saved or modified by this builder.
public static class Level3Builder
{
    private const string Reference = "Assets/Scenes/Level0.unity";
    private const string Level3 = "Assets/Scenes/Level3.unity";

    [MenuItem("Tools/Level 3/Build or Refresh Ring Exchange")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene reference = EditorSceneManager.OpenScene(Reference, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(reference, Level3, true))
        {
            Debug.LogError("Cannot copy the Level 0 rig into Level 3.");
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(Level3, OpenSceneMode.Single);
        GameManager manager = Object.FindFirstObjectByType<GameManager>();
        if (manager == null) { Debug.LogError("Level 0 rig requires GameManager."); return; }
        Level3SceneSetup setup = manager.GetComponent<Level3SceneSetup>();
        if (setup == null) setup = manager.gameObject.AddComponent<Level3SceneSetup>();
        setup.Rebuild();
        if (!HudBuilder.BuildHud()) Debug.LogWarning("HUD refresh failed; inspect original bindings.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Ring Exchange saved to Assets/Scenes/Level3.unity. Press Play to test.");
    }
}
