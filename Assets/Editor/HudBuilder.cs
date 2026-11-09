using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HudBuilder
{
    internal const float ReferenceWidth = 1920, ReferenceHeight = 1080;
    [MenuItem("Tools/UI/Rebuild Current Level HUD")]
    private static void BuildCurrent() => BuildHud();

    internal static bool BuildHud()
    {
        var manager = Object.FindAnyObjectByType<GameManager>();
        if (manager == null) return false;
        var data = new SerializedObject(manager);
        var status = data.FindProperty("statusText").objectReferenceValue as TMP_Text;
        if (status == null || status.canvas == null) return false;
        GameplayHudRuntime.Build(status, Object.FindAnyObjectByType<MapFlipController>(), out TMP_Text charges, out TMP_Text stars);
        data.FindProperty("chargesText").objectReferenceValue = charges;
        data.FindProperty("starsText").objectReferenceValue = stars;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (Camera.main != null) FrameCamera(Camera.main);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return true;
    }
    [MenuItem("Tools/UI/Rebuild All Level HUDs")]
    public static void RebuildAllHuds()
    {
        string original = SceneManager.GetActiveScene().path;
        foreach (string name in new[] { "Tutorial", "Level0", "Level1", "Level2", "Level3", "Level4" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!BuildHud()) throw new System.InvalidOperationException("Cannot build HUD: " + path);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
    }
    public static void RebuildAllFromCommandLine() => RebuildAllHuds();
    internal static void FrameCamera(Camera camera)
    {
        var floor = GameObject.Find("Floor");
        var renderer = floor != null ? floor.GetComponent<SpriteRenderer>() : null;
        if (renderer == null) return;
        Bounds bounds = renderer.bounds;
        const float share = .12f;
        float size = Mathf.Max((bounds.extents.y + 1.1f) / (1 - share), (bounds.extents.x + .6f) / (16f / 9f));
        camera.rect = new Rect(0, 0, 1, 1); camera.orthographicSize = size;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + size * share, -10);
    }
}
