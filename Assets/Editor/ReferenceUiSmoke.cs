using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs the real scene lifecycle in batch mode, with isolated progression and
// analytics disabled. It exercises menu clicks, tutorial reveal, flip timing,
// star reward, level completion, replay and every level's HUD binding.
[InitializeOnLoad]
public static class ReferenceUiSmoke
{
    private const string Running = "ReferenceUiSmoke.Running";
    private const string BuildAfterRun = "ReferenceUiSmoke.BuildAfterRun";
    private const string TutorialBuildOnly = "ReferenceUiSmoke.TutorialBuildOnly";
    private static int stage;
    private static double started, stageTime;
    private static bool initialized;
    private static string failure;
    static ReferenceUiSmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    public static void RunAndBuild()
    {
        SessionState.SetBool(BuildAfterRun, true);
        Run();
    }
    public static void RebuildTutorialAndVerify()
    {
        TutorialBuilder.BuildFromCommandLine();
        SessionState.SetBool(TutorialBuildOnly, true);
        RunAndBuild();
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !Application.isBatchMode ||
            !SessionState.GetBool(BuildAfterRun, false)) return;
        SessionState.SetBool(BuildAfterRun, false);
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (SessionState.GetBool(TutorialBuildOnly, false)) ProjectBuild.BuildWebGl();
                else ProjectBuild.RebuildUiAndWebGl();
                File.AppendAllText("UIReview/tutorial-rework-result.txt", "\nPASS: WebGL build completed.\n");
                SessionState.SetBool(TutorialBuildOnly, false);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                File.AppendAllText("UIReview/tutorial-rework-result.txt", "\nBUILD FAILED: " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        };
    }
    public static void Run()
    {
        Directory.CreateDirectory("UIReview");
        stage = 0;
        initialized = false;
        failure = null;
        TutorialChecks.Reset();
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void OnLog(string condition, string stack, LogType type)
    {
        if (SessionState.GetBool(Running, false) && (type == LogType.Exception || type == LogType.Error)) failure = condition;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (!initialized) { initialized = true; started = stageTime = EditorApplication.timeSinceStartup; }
        try
        {
            if (failure != null) throw new Exception(failure);
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 150) throw new Exception("Smoke run timed out at stage " + stage);
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
            var game = GameManager.Instance;
            if (now - stageTime < .5) return;
            switch (stage)
            {
                case 0:
                    Require(menu != null, "Main menu did not load");
                    var root = menu.transform.Find(MainMenu.LayoutName);
                    Require(Mathf.Abs(root.Find("Home/Start").GetComponent<RectTransform>().anchoredPosition.x) < .01f, "Start button should be centered");
                    Require(Mathf.Abs(root.Find("Home/Help Button").GetComponent<RectTransform>().anchoredPosition.x) < .01f, "How to Play button should be centered");
                    var tutorial = root.Find("Home/Tutorial");
                    Require(tutorial != null && tutorial.gameObject.activeInHierarchy && tutorial.GetComponent<Button>().interactable,
                        "Tutorial button should be visible and available");
                    Require(Mathf.Abs(tutorial.GetComponent<RectTransform>().anchoredPosition.x) < .01f,
                        "Tutorial button should be centered");
                    Require(!root.Find("Home/How To Play").gameObject.activeSelf, "Controls should start hidden");
                    root.Find("Home/Help Button").GetComponent<Button>().onClick.Invoke();
                    Require(root.Find("Home/How To Play").gameObject.activeSelf, "Help button did not open controls");
                    root.Find("Home/How To Play/Close").GetComponent<Button>().onClick.Invoke();
                    Require(!root.Find("Home/How To Play").gameObject.activeSelf, "Close did not hide controls");
                    root.Find("Home/Start").GetComponent<Button>().onClick.Invoke();
                    Require(root.Find("Level Map").gameObject.activeSelf, "Start did not open map");
                    Require(root.Find("Level Map/Completion Times") == null, "Time-rating legend should be removed");
                    for (int i = 0; i < LevelCatalog.Count; i++)
                    {
                        var node = root.Find("Level Map/Level " + i);
                        Require(node.Find("Completion Star") != null && node.Find("Rating 0") == null,
                            "Each level should show one completion star");
                    }
                    root.Find("Level Map/Back").GetComponent<Button>().onClick.Invoke();
                    Require(root.Find("Home").gameObject.activeSelf, "Back did not open home");
                    TrajectoryChecks.VerifyRecording();
                    tutorial.GetComponent<Button>().onClick.Invoke(); Next(); break;
                case 1:
                    if (!TutorialChecks.Tick()) return;
                    SceneManager.LoadScene("Level0"); stage = 4; stageTime = now; break;
                case 4:
                    if (game == null || !game.IsPlaying) return;
                    if (now - stageTime < 7.0) return; // Let preview and the follow-zoom transition finish.
                    Require(Camera.main.orthographicSize < GameObject.Find("Floor").GetComponent<SpriteRenderer>().bounds.extents.y,
                        "Gameplay camera did not keep the requested player-follow zoom");
                    var controller = UnityEngine.Object.FindAnyObjectByType<MapFlipController>();
                    float duration = controller.CycleDuration;
                    var artwork = UnityEngine.Object.FindAnyObjectByType<HourglassGraphic>();
                    Require(Mathf.Abs(artwork.Fill01 - controller.CycleProgress01) < .05f && controller.SecondsUntilFlip <= duration, "Sand animation is not bound to the flip cycle");
                    TrajectoryChecks.SeedRenderFixture(game);
                    for (int i = game.StarsCollected; i < 2; i++) Require(game.TryCollectStar(), "Level star count did not update");
                    game.TryExit(); Require(game.IsFinished && PlayerProgress.IsLevelUnlocked(1), "Completion did not unlock next level");
                    Require(PlayerProgress.IsLevelCompleted(0), "Completion star was not saved");
                    Next(); stage = 40; break;
                case 40:
                    if (now - stageTime < 3) return;
                    var trace = game.GetComponent<RunTrajectory>();
                    Require(!trace.IsRecording && trace.RevealComplete, "Winning did not finish the trajectory reveal");
                    var follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                    Require(follow.IsOverviewSettled, "Completion zoom did not settle");
                    TrajectoryChecks.VerifyOverview(follow, 16f / 9f);
                    ReferenceUiReview.Capture("trajectory-completion");
                    TrajectoryChecks.VerifyOverview(follow, 4f / 3f);
                    ReferenceUiReview.Capture("trajectory-completion-narrow", 1280, 960);
                    TrajectoryChecks.VerifyOverview(follow, 9f / 16f);
                    Camera.main.ResetAspect();
                    game.RestartLevel(); stage = 5; stageTime = now; break;
                case 5:
                    if (game == null || !game.IsPlaying) return;
                    Require(game.StarsCollected == 0 && game.ChargesRemaining == 1, "Replay did not reset HUD using saved starting charges");
                    Require(!game.GetComponent<RunTrajectory>().IsVisible, "Replay leaked the previous trajectory");
                    game.FailLevel("Trajectory failure check");
                    Require(!game.GetComponent<RunTrajectory>().IsVisible && !game.GetComponent<RunTrajectory>().IsRecording,
                        "Failed attempts must not reveal a trajectory");
                    SceneManager.LoadScene("MainMenu"); Next(); break;
                case 6:
                    if (menu == null) return;
                    menu.ShowLevelMap();
                    var map = menu.transform.Find(MainMenu.LayoutName + "/Level Map");
                    var completedStar = new SerializedObject(map.Find("Level 0/Completion Star").GetComponent<UiShapeGraphic>());
                    var incompleteStar = new SerializedObject(map.Find("Level 1/Completion Star").GetComponent<UiShapeGraphic>());
                    Require(completedStar.FindProperty("fill").colorValue == GameUi.Gold, "Completed level should have a filled yellow star");
                    Require(incompleteStar.FindProperty("fill").colorValue.a == 0, "Uncompleted level should have an outlined star");
                    ReferenceUiReview.Capture("level-map-completed");
                    SceneManager.LoadScene("Level1"); Next(); break;
                case 7:
                case 8:
                case 9:
                case 10:
                    if (game == null) return;
                    ValidateHud();
                    Require(game.GetComponent<RunTrajectory>() != null, "A level is missing trajectory tracking");
                    if (stage < 10) { SceneManager.LoadScene("Level" + (stage - 5)); Next(); }
                    else Finish();
                    break;
            }
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Running, false);
            SessionState.SetBool(BuildAfterRun, false);
            Debug.LogException(exception);
            File.WriteAllText("UIReview/play-smoke.txt", "FAILED: " + exception);
            File.WriteAllText("UIReview/tutorial-rework-result.txt", "FAILED: " + exception);
            TutorialChecks.Reset();
            PlayerProgress.ResetLocalProgress(); PlayerProgress.UseTestStorage(false);
            EditorApplication.Exit(1);
        }
    }
    private static void ValidateHud()
    {
        foreach (var graphic in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Require(graphic.GetComponent<CanvasRenderer>() != null, "Missing CanvasRenderer on " + graphic.name);
        var data = new SerializedObject(GameManager.Instance);
        Require(data.FindProperty("chargesText").objectReferenceValue != null, "Charges binding missing");
        Require(data.FindProperty("starsText").objectReferenceValue != null, "Stars binding missing");
        var hourglass = UnityEngine.Object.FindAnyObjectByType<HourglassIndicator>();
        Require(hourglass.transform.parent.Find("Countdown") == null, "Numeric flip timer should be removed");
        Require(hourglass.GetComponent<RectTransform>().sizeDelta.y >= 150, "Hourglass was not enlarged");
        Color flipColor = hourglass.GetComponent<HourglassGraphic>().FrameColor;
        Require(flipColor.r > .8f && flipColor.g < .7f, "Flip icon should be red");
        var hud = hourglass.transform.parent.parent;
        Require(hud.Find("Stars").GetComponent<RectTransform>().anchorMin.x == .5f, "Stars should be centered");
        Color starColor = hud.Find("Stars/Star").GetComponent<StarGraphic>().color;
        Require(starColor.r > .8f && starColor.g > .7f && starColor.b < .3f, "Stars should remain yellow");
        Require(hud.Find("Light Charges").GetComponent<RectTransform>().anchorMin.x == 1f, "Light charges should be on the right");
        var flashlight = hud.Find("Light Charges/Flashlight").GetComponent<FlashlightGraphic>();
        Require(flashlight != null && flashlight.color.b > .8f && flashlight.color.r < .65f, "Flashlight should use the new light-blue icon");
        Require(hud.GetComponentInChildren<LanternGraphic>(true) == null, "Old lantern icon should not appear in the HUD");
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            Require(!renderer.name.StartsWith("Reference "), "Added world art was not removed: " + renderer.name);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Next() { stage++; stageTime = EditorApplication.timeSinceStartup; }
    private static void Finish()
    {
        PlayerProgress.ResetLocalProgress(); PlayerProgress.UseTestStorage(false);
        SessionState.SetBool(Running, false);
        File.WriteAllText("UIReview/play-smoke.txt", "PASS: maze-local recording across repeated flips, stationary deduplication, pathway filtering of jumps/local loops, wall-safe shortcutting, preserved pickup detours and exact endpoints, hidden recording during play/reveals, animated win reveal, replay/failure cleanup, completion overview at 16:9/4:3/9:16, menu popup/navigation, single completion stars, light reveal/rewards, red flip animation, original world art and all five HUDs. Completion images use a scripted render fixture.\n");
        Debug.Log("REFERENCE_UI_PLAY_SMOKE_SUCCESS");
        File.WriteAllText("UIReview/tutorial-rework-result.txt",
            "PASS: 10-second tutorial preview; real movement and jump; physical star pickup and light reward; F reveal; five-second grace period; automatic countdown, warning and flip; reachable exit; completion and replay; existing menu and all five level HUD checks.\n");
        if (SessionState.GetBool(BuildAfterRun, false)) EditorApplication.ExitPlaymode();
        else EditorApplication.Exit(0);
    }
}
