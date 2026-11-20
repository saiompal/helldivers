using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// The original Level1 and Level3 assets remain source maps. The playable fourth
// level is baked into its own scene, preserving the three unchanged levels.
public static class CampaignBuilder
{
    public const string LinkedScene = "Assets/Scenes/LinkedMaps.unity";
    public const float UpperOffset = 24f; // Two 19-unit maps separated by 5 units.

    [MenuItem("Tools/Campaign/Build Six-Level Campaign")]
    public static void BuildAll()
    {
        BuildLinkedMaps();
        TutorialBuilder.BuildFromCommandLine();
        MainMenuBuilder.BuildFromCommandLine();
        AssetDatabase.DeleteAsset("Assets/Scenes/ShadowTutorial.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("SIX_LEVEL_CAMPAIGN_BUILD_SUCCESS");
    }

    public static void BuildLinkedMaps()
    {
        Scene source = EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity");
        if (!EditorSceneManager.SaveScene(source, LinkedScene, true)) throw new InvalidOperationException("Cannot copy lower source map.");
        Scene scene = EditorSceneManager.OpenScene(LinkedScene);
        Transform lower = GameObject.Find("Map").transform;
        lower.name = "Lower Map";
        var combined = new GameObject("Map").transform;
        lower.SetParent(combined, true);
        KeepStars(lower, new Vector2(-9.94f, -4.9f), new Vector2(4.07f, -.11f));
        var bottomPortals = lower.GetComponentsInChildren<PortalController>();
        var departure = bottomPortals.OrderBy(p => Vector2.Distance(p.transform.position, new Vector2(3.9f, -4.1f))).First();
        var arrival = bottomPortals.First(p => p != departure);

        SetGlobalLights(scene, false);
        Scene upperSource = EditorSceneManager.OpenScene("Assets/Scenes/Level3.unity", OpenSceneMode.Additive);
        GameObject upperCopy = Object.Instantiate(upperSource.GetRootGameObjects().First(go => go.name == "Map"));
        SceneManager.MoveGameObjectToScene(upperCopy, scene);
        Transform upper = upperCopy.transform;
        upper.name = "Upper Map";
        upper.SetParent(combined, false);
        upper.localPosition = new Vector3(0, UpperOffset, 0);
        foreach (var exit in upper.GetComponentsInChildren<ExitTrigger>()) Object.DestroyImmediate(exit.gameObject);
        KeepStars(upper, new Vector2(-7.6f, -7.86f + UpperOffset),
            new Vector2(-4.59f, -.05f + UpperOffset), new Vector2(14.95f, 3.49f + UpperOffset));
        EditorSceneManager.CloseScene(upperSource, true);
        SetGlobalLights(scene, true);
        SceneManager.SetActiveScene(scene);

        arrival.transform.SetParent(upper, true);
        arrival.transform.SetPositionAndRotation(new Vector3(-14, UpperOffset - 7.7f, 0), Quaternion.identity);
        departure.name = "Portal to Upper Map";
        arrival.name = "Portal to Lower Map";
        departure.destination = arrival.transform;
        arrival.destination = departure.transform;

        var manager = Object.FindAnyObjectByType<GameManager>();
        var flip = manager.GetComponent<MapFlipController>();
        var flipData = new SerializedObject(flip);
        flipData.FindProperty("mapRoot").objectReferenceValue = combined;
        SetArray(flipData, "sectionRoots", lower, upper);
        flipData.ApplyModifiedPropertiesWithoutUndo();
        var gameData = new SerializedObject(manager);
        gameData.FindProperty("previewSeconds").floatValue = 12;
        gameData.ApplyModifiedPropertiesWithoutUndo();

        var lowerFloor = lower.Find("Floor").GetComponent<SpriteRenderer>();
        var upperFloor = upper.Find("Floor").GetComponent<SpriteRenderer>();
        var bounds = new GameObject("Overview Bounds", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        bounds.transform.SetParent(combined, false);
        bounds.transform.localPosition = new Vector3(0, UpperOffset / 2, 0);
        bounds.transform.localScale = new Vector3(34, 19 + UpperOffset, 1);
        bounds.sprite = lowerFloor.sprite;
        bounds.sharedMaterial = lowerFloor.sharedMaterial;
        bounds.color = Color.clear;
        var camera = Camera.main;
        var cameraData = new SerializedObject(camera.GetComponent<CameraFollow>());
        cameraData.FindProperty("mapBounds").objectReferenceValue = bounds;
        SetArray(cameraData, "sectionBounds", lowerFloor, upperFloor);
        cameraData.FindProperty("autoFitThisLevel").boolValue = true;
        cameraData.FindProperty("overviewPadding").floatValue = .8f;
        cameraData.ApplyModifiedPropertiesWithoutUndo();
        camera.orthographicSize = 25.4f;
        camera.transform.position = new Vector3(0, 15.05f, -10);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LINKED_MAPS_BUILD_SUCCESS: original lower spawn/exit; two lower stars; three upper stars; one return portal pair.");
    }

    private static void KeepStars(Transform map, params Vector2[] positions)
    {
        var all = map.GetComponentsInChildren<StarCollectible>();
        var retained = positions.Select(position => all.OrderBy(star => Vector2.Distance(star.transform.position, position)).First()).ToArray();
        if (retained.Distinct().Count() != positions.Length) throw new InvalidOperationException("Star selection was ambiguous.");
        foreach (var star in all) if (!retained.Contains(star)) Object.DestroyImmediate(star.gameObject);
    }

    internal static void AddShadowLesson(Scene scene)
    {
        foreach (var sensor in Object.FindObjectsByType<PressureButtonSensor>(FindObjectsSortMode.None)) Object.DestroyImmediate(sensor.gameObject);
        foreach (var wall in Object.FindObjectsByType<ButtonControlledWall>(FindObjectsSortMode.None)) Object.DestroyImmediate(wall.gameObject);
        Transform map = GameObject.Find("Map").transform;
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        var spawner = player.GetComponent<ShadowCloneSpawner>();
        if (spawner == null) spawner = player.gameObject.AddComponent<ShadowCloneSpawner>();
        var data = new SerializedObject(spawner);
        data.FindProperty("shadowPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/shadow.prefab");
        data.ApplyModifiedPropertiesWithoutUndo();

        SetGlobalLights(scene, false);
        Scene reference = EditorSceneManager.OpenScene("Assets/Scenes/ShadowLevel.unity", OpenSceneMode.Additive);
        var referenceButton = reference.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PressureButtonSensor>()).First();
        var referenceWall = reference.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ButtonControlledWall>()).First();
        GameObject button = Object.Instantiate(referenceButton.gameObject);
        GameObject gate = Object.Instantiate(referenceWall.gameObject);
        SceneManager.MoveGameObjectToScene(button, scene);
        SceneManager.MoveGameObjectToScene(gate, scene);
        button.name = "Tutorial Shadow Button";
        button.transform.SetParent(map, false);
        button.transform.localPosition = new Vector3(0, 4.35f, 0);
        button.transform.localRotation = Quaternion.Euler(0, 0, 180);
        button.transform.localScale = new Vector3(.8f, .22f, 1);
        gate.name = "Tutorial Shadow Gate";
        gate.transform.SetParent(map, false);
        gate.transform.localPosition = new Vector3(6, 0, 0);
        gate.transform.localScale = new Vector3(.5f, 9, 1);
        var gateData = new SerializedObject(gate.GetComponent<ButtonControlledWall>());
        gateData.FindProperty("button").objectReferenceValue = button.GetComponent<PressureButtonSensor>();
        gateData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.CloseScene(reference, true);
        SetGlobalLights(scene, true);
        SceneManager.SetActiveScene(scene);
    }

    private static void SetGlobalLights(Scene scene, bool enabled)
    {
        foreach (var light in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Light2D>()))
            if (light.lightType == Light2D.LightType.Global) light.enabled = enabled;
    }

    private static void SetArray(SerializedObject data, string name, params Object[] objects)
    {
        var array = data.FindProperty(name);
        array.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }
}
