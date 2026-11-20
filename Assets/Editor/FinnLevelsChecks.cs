using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Batch entry point: -executeMethod FinnLevelsChecks.Run -uiReviewSmoke
// Uses isolated saves and disabled analytics, like ReferenceUiSmoke.
[InitializeOnLoad]
public static class FinnLevelsChecks
{
    private const string Running = "FinnLevelsChecks.Running";
    private static int stage, level = 1;
    private static double since, started;
    private static string failure;
    private static PressureButtonSensor pressure;
    private static ShadowFlipFollower clone;

    static FinnLevelsChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Running, false) &&
                (type == LogType.Error || type == LogType.Exception)) failure = message;
        };
    }

    public static void Run()
    {
        Require(Environment.GetCommandLineArgs().Contains("-uiReviewSmoke"),
            "Pass -uiReviewSmoke to isolate saves and disable analytics.");
        Directory.CreateDirectory("UIReview");
        foreach (string name in new[] { "ShadowLevel", "ShadowLevel 2" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                        name + " has a missing script on " + child.name);
            Require(UnityEngine.Object.FindObjectsByType<ShadowCloneSpawner>(FindObjectsSortMode.None).Length == 1,
                name + " needs exactly one clone spawner.");
            Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scene.path),
                name + " is absent from the build.");
        }
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private static void Next() { stage++; since = EditorApplication.timeSinceStartup; }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) started = since = now;
        try
        {
            if (failure != null) throw new Exception(failure);
            Require(now - started < 100, "Finn levels smoke timed out at stage " + stage);
            if (now - since < .2) return;
            var game = GameManager.Instance;
            switch (stage)
            {
                case 0:
                    var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
                    Require(menu != null, "Main menu did not load.");
                    menu.ShowLevelMap();
                    for (int i = 0; i < LevelCatalog.Count; i++)
                    {
                        var button = menu.transform.Find(MainMenu.LayoutName + "/Level Map/Level " + i)?.GetComponent<Button>();
                        Require(button != null && button.interactable == (i == 0), "Incorrect fresh-save level button " + i);
                    }
                    ReferenceUiReview.Capture("finn-level-map", 1280, 720);
                    PlayerProgress.CompleteLevel(0);
                    menu.ShowLevelMap();
                    menu.transform.Find(MainMenu.LayoutName + "/Level Map/Level 1").GetComponent<Button>().onClick.Invoke();
                    Next();
                    break;
                case 1:
                    Require(game != null && game.gameObject.scene.name == LevelCatalog.GetSceneName(level), "Wrong shadow scene loaded.");
                    Require(game.IsPreviewing, "Expected the map preview.");
                    UnityEngine.Object.FindAnyObjectByType<ShadowCloneSpawner>().RequestShadow();
                    Require(UnityEngine.Object.FindAnyObjectByType<ShadowFlipFollower>() == null, "Spawned during preview.");
                    Require(UnityEngine.Object.FindObjectsByType<TutorialGuide>(FindObjectsSortMode.None).All(g => !g.enabled),
                        "The original tutorial guide should not run in a shadow level.");
                    ReferenceUiReview.Capture("finn-" + level + "-preview", 1280, 720);
                    Next();
                    break;
                case 2:
                    if (!game.CanPlayerMove) return;
                    pressure = UnityEngine.Object.FindObjectsByType<PressureButtonSensor>(FindObjectsSortMode.None)[0];
                    // Some authored buttons start on the ceiling. Turn the
                    // actual map so this fixture can stand on their top face.
                    var mapFlip = UnityEngine.Object.FindAnyObjectByType<MapFlipController>();
                    if (pressure.transform.up.y < 0)
                    {
                        Invoke(mapFlip, "BeginTurn");
                        mapFlip.Stop();
                    }
                    var spawner = UnityEngine.Object.FindAnyObjectByType<ShadowCloneSpawner>();
                    spawner.RequestShadow();
                    clone = UnityEngine.Object.FindAnyObjectByType<ShadowFlipFollower>();
                    Require(clone != null, "Clone did not spawn.");
                    Require(Physics2D.GetIgnoreCollision(clone.GetComponent<Collider2D>(), spawner.GetComponent<Collider2D>()),
                        "Player/clone collisions were not ignored.");
                    var body = clone.GetComponent<Rigidbody2D>();
                    Bounds pad = pressure.GetComponent<Collider2D>().bounds;
                    body.position = new Vector2(pad.center.x, pad.max.y + clone.GetComponent<Collider2D>().bounds.extents.y + .02f);
                    body.linearVelocity = Vector2.zero;
                    Next();
                    break;
                case 3:
                    if (now - since < 1) return;
                    Require(pressure.IsPressed, "Clone did not hold a pressure button in " + game.gameObject.scene.name +
                        "; clone=" + clone.transform.position + "; pad=" + pressure.GetComponent<Collider2D>().bounds);
                    var walls = UnityEngine.Object.FindObjectsByType<ButtonControlledWall>(FindObjectsSortMode.None)
                        .Where(w => new SerializedObject(w).FindProperty("button").objectReferenceValue == pressure).ToArray();
                    Require(walls.Length > 0 && walls.All(w => !w.GetComponent<Collider2D>().enabled), "Linked walls did not open.");
                    var flip = UnityEngine.Object.FindAnyObjectByType<MapFlipController>();
                    Vector3 before = clone.transform.position;
                    Invoke(flip, "BeginTurn");
                    Require(!clone.GetComponent<Rigidbody2D>().simulated, "Clone physics was not paused during the flip.");
                    Invoke(flip, "ApplyTurn", .5f);
                    Vector3 expected = flip.MapRoot.position + Quaternion.Euler(0, 0, 90) * (before - flip.MapRoot.position);
                    Require(Vector3.Distance(clone.transform.position, expected) < .002f, "Clone followed the wrong flip arc.");
                    flip.Stop();
                    expected = 2 * flip.MapRoot.position - before;
                    Require(clone.GetComponent<Rigidbody2D>().simulated && Vector2.Distance(clone.transform.position, expected) < .002f,
                        "Clone did not resume at the flip endpoint.");
                    spawner = UnityEngine.Object.FindAnyObjectByType<ShadowCloneSpawner>();
                    spawner.RequestShadow();
                    Next();
                    break;
                case 4:
                    Require(UnityEngine.Object.FindObjectsByType<ShadowFlipFollower>(FindObjectsSortMode.None).Length == 1,
                        "Replacing a clone left duplicates.");
                    foreach (var star in UnityEngine.Object.FindObjectsByType<StarCollectible>(FindObjectsSortMode.None))
                        Require(game.TryCollectStar(), "Star collection failed.");
                    game.TryExit();
                    Require(game.IsFinished && PlayerProgress.IsLevelCompleted(level), "Shadow completion was not saved.");
                    ReferenceUiReview.Capture("finn-" + level + "-complete", 1280, 720);
                    if (level == 1)
                    {
                        Require(PlayerProgress.IsLevelUnlocked(2), "Small shadow level did not unlock level 3.");
                        for (int i = 2; i < 5; i++) PlayerProgress.CompleteLevel(i);
                        level = 5;
                        SceneManager.LoadScene(LevelCatalog.GetSceneName(level));
                        stage = 1;
                        since = now;
                    }
                    else Finish(null);
                    break;
            }
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static void Finish(string error)
    {
        SessionState.SetBool(Running, false);
        File.WriteAllText("UIReview/finn-levels-result.txt", error ??
            "PASS: six-level menu; both Finn scenes; preview input gate; clone spawning/replacement; pressure walls; flip arc and physics; completion and progression.\n");
        if (error != null) Debug.LogError(error);
        else Debug.Log("FINN_LEVELS_CHECKS_SUCCESS");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
