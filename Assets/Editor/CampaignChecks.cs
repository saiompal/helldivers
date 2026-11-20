using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises real portal/star/exit trigger callbacks. Teleporting the test fixture
// between landmarks is not a claim to have played a continuous platforming route.
[InitializeOnLoad]
public static class CampaignChecks
{
    private const string Running = "CampaignChecks.Running";
    private static int stage = -1, expectedStars;
    private static double since, started;
    private static string failure;
    private static Transform lower, upper;
    private static Rigidbody2D body;

    static CampaignChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception)) failure = message;
        };
    }

    public static void Run()
    {
        Require(Environment.GetCommandLineArgs().Contains("-uiReviewSmoke"), "Use -uiReviewSmoke to isolate saves and analytics.");
        Directory.CreateDirectory("UIReview");
        Require(!File.Exists("Assets/Scenes/ShadowTutorial.unity"), "Finn's tutorial was not removed.");
        Require(EditorBuildSettings.scenes.Count(scene => scene.enabled) == 8, "Expected menu, tutorial, and six levels.");
        EditorSceneManager.OpenScene(CampaignBuilder.LinkedScene);
        var map = GameObject.Find("Map").transform;
        var bottom = map.Find("Lower Map");
        var top = map.Find("Upper Map");
        Require(bottom.GetComponentsInChildren<StarCollectible>().Length == 2 && top.GetComponentsInChildren<StarCollectible>().Length == 3,
            "Incorrect 2/3 star split.");
        float gap = top.Find("Floor").GetComponent<SpriteRenderer>().bounds.min.y - bottom.Find("Floor").GetComponent<SpriteRenderer>().bounds.max.y;
        Require(Mathf.Abs(gap - 5) < .01f, "The two maps need a five-unit vertical gap.");
        Require(Vector2.Distance(UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform.position, new Vector2(-15, -8)) < .01f,
            "The lower source-map spawn changed.");
        Require(bottom.GetComponentsInChildren<ExitTrigger>().Length == 1 && top.GetComponentsInChildren<ExitTrigger>().Length == 0,
            "The exit must be in the lower map only.");
        foreach (var star in map.GetComponentsInChildren<StarCollectible>())
        {
            // Bounds-based clearance catches stars embedded in solid source walls.
            foreach (var wall in map.GetComponentsInChildren<BoxCollider2D>())
            {
                if (wall.isTrigger || wall.GetComponent<PortalController>() != null) continue;
                Require(!wall.bounds.Contains(star.transform.position), "Star embedded in " + wall.name);
            }
        }
        ReferenceUiReview.Capture("campaign-linked-layout", 1280, 720);
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
    private static void Place(Vector3 position)
    {
        body.transform.position = position;
        body.position = position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) started = since = now;
        try
        {
            if (failure != null) throw new Exception(failure);
            Require(now - started < 100, "Campaign smoke timed out at stage " + stage);
            if (now - since < .25) return;
            var game = GameManager.Instance;
            switch (stage)
            {
                case -1:
                    UnityEngine.Object.FindAnyObjectByType<MainMenu>().ShowHelp();
                    Next();
                    break;
                case 0:
                    var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
                    Require(menu != null, "Menu did not load.");
                    ReferenceUiReview.Capture("campaign-controls", 1280, 720);
                    menu.ShowLevelMap(); ReferenceUiReview.Capture("campaign-level-map", 1280, 720);
                    for (int i = 0; i < 3; i++) PlayerProgress.CompleteLevel(i);
                    menu.LoadLevel(3);
                    Next();
                    break;
                case 1:
                    Require(game != null && game.gameObject.scene.name == "LinkedMaps" && game.IsPreviewing, "Linked map preview missing.");
                    var flip = game.GetComponent<MapFlipController>();
                    lower = flip.SectionRoots[0]; upper = flip.SectionRoots[1];
                    body = flip.PlayerTransform.GetComponent<Rigidbody2D>();
                    body.interpolation = RigidbodyInterpolation2D.None;
                    Vector2 before = body.position;
                    Invoke(lower.GetComponentInChildren<PortalController>(), "OnTriggerEnter2D", body.GetComponent<Collider2D>());
                    Require(body.position == before, "Portal operated during memorization.");
                    game.TryExit(); Require(!game.IsFinished, "Exit opened before five stars.");
                    ReferenceUiReview.Capture("campaign-linked-preview", 1280, 720);
                    Next();
                    break;
                case 2:
                    if (!game.CanPlayerMove) return;
                    game.GetComponent<MapFlipController>().Stop();
                    Place(lower.GetComponentInChildren<PortalController>().transform.position);
                    Next();
                    break;
                case 3:
                    Require(game.GetComponent<MapFlipController>().GetSectionAt(body.position) == upper, "Lower portal did not transport the player upward, or bounced back.");
                    Require(game.StarsCollected == 0, "Teleporting changed the star counter.");
                    Next();
                    break;
                case 4:
                    var topStar = upper.GetComponentInChildren<StarCollectible>();
                    if (topStar != null)
                    {
                        expectedStars = game.StarsCollected + 1;
                        Place(topStar.transform.position);
                        Next();
                    }
                    else
                    {
                        Require(game.StarsCollected == 3 && !game.IsExitUnlocked, "Three upper stars should leave the exit locked.");
                        var controller = game.GetComponent<MapFlipController>();
                        Vector3 oldPosition = body.position;
                        Invoke(controller, "BeginTurn");
                        Require(!body.simulated && controller.PlayerSection == upper, "Wrong player flip section.");
                        Invoke(controller, "ApplyTurn", .5f);
                        Require(lower.position == Vector3.zero && Vector3.Distance(upper.position, new Vector3(0, 24)) < .001f,
                            "The two sections changed sides while turning.");
                        controller.Stop();
                        Require(body.simulated && Vector2.Distance(body.position, 2 * (Vector2)upper.position - (Vector2)oldPosition) < .002f,
                            "The upper-map flip used the wrong pivot.");
                        Place(upper.GetComponentInChildren<PortalController>().transform.position);
                        stage = 6; since = now;
                    }
                    break;
                case 5:
                    Require(game.StarsCollected >= expectedStars, "Physical upper star pickup failed.");
                    stage = 4; since = now;
                    break;
                case 6:
                    Require(game.GetComponent<MapFlipController>().GetSectionAt(body.position) == lower, "Return portal did not bring the player back to the lower map.");
                    Require(game.StarsCollected == 3, "Returning lost upper-map stars.");
                    Next();
                    break;
                case 7:
                    var bottomStar = lower.GetComponentInChildren<StarCollectible>();
                    if (bottomStar != null)
                    {
                        expectedStars = game.StarsCollected + 1;
                        Place(bottomStar.transform.position);
                        Next();
                    }
                    else
                    {
                        Require(game.StarsCollected == 5 && game.IsExitUnlocked, "Five stars did not unlock the lower exit.");
                        Place(lower.GetComponentInChildren<ExitTrigger>().transform.position);
                        stage = 9; since = now;
                    }
                    break;
                case 8:
                    Require(game.StarsCollected >= expectedStars, "Physical lower star pickup failed.");
                    stage = 7; since = now;
                    break;
                case 9:
                    Require(game.IsFinished && PlayerProgress.IsLevelCompleted(3), "Physical lower exit did not complete displayed level 4.");
                    if (now - since < 3) return;
                    var route = game.GetComponent<RunTrajectory>();
                    Require(route.RevealComplete, "The two-map route reveal did not finish.");
                    Require(lower.GetComponentInChildren<LineRenderer>() != null && upper.GetComponentInChildren<LineRenderer>() != null,
                        "Each section needs its own trajectory, without a line across the teleport gap.");
                    ReferenceUiReview.Capture("campaign-linked-complete", 1280, 720);
                    game.RestartLevel();
                    Next();
                    break;
                case 10:
                    Require(game != null && game.IsPreviewing && game.StarsCollected == 0 &&
                        UnityEngine.Object.FindObjectsByType<StarCollectible>(FindObjectsSortMode.None).Length == 5,
                        "Replay did not restore all five stars.");
                    foreach (var section in game.GetComponent<MapFlipController>().SectionRoots)
                        Require(Quaternion.Angle(section.rotation, Quaternion.identity) < .01f, "Replay did not reset section rotation.");
                    Finish(null);
                    break;
            }
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static void Finish(string error)
    {
        SessionState.SetBool(Running, false);
        File.WriteAllText("UIReview/campaign-checks.txt", error ??
            "PASS: six-level build/menu; removed shadow tutorial; 5-unit map gap; 2/3 stars; lower spawn/exit; real two-way portal and pickup triggers; independent flip pivots; persistent shared stars; locked/unlocked lower exit; separate route lines; replay reset.\n");
        if (error != null) Debug.LogError(error); else Debug.Log("CAMPAIGN_CHECKS_SUCCESS");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
