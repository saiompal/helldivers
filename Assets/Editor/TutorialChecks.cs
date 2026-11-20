using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Exercises the tutorial through real keyboard input and physics. Saves and
// analytics are isolated by ReferenceUiSmoke before any gameplay scene loads.
internal static class TutorialChecks
{
    private static int stage;
    private static float since, starAt, plateAt;
    private static Keyboard keyboard;
    private static TMP_Text hint;
    private static InputSettings.BackgroundBehavior backgroundBehavior;
    private static InputSettings.EditorInputBehaviorInPlayMode editorInputBehavior;
    private static bool inputSettingsCaptured;

    internal static void Reset()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (inputSettingsCaptured)
        {
            InputSystem.settings.backgroundBehavior = backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInputBehavior;
            inputSettingsCaptured = false;
        }
        keyboard = null;
        hint = null;
        stage = 0;
        since = starAt = plateAt = -1f;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Tutorial: " + message);
    }

    private static void Keys(params Key[] keys) =>
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

    private static void Next() { stage++; since = Time.time; }
    private static void Capture(string name) => ReferenceUiReview.Capture("tutorial-" + name, 1280, 720);

    private static void SteerTo(float x)
    {
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform;
        float distance = x - player.position.x;
        if (Mathf.Abs(distance) < .25f) Keys();
        else Keys(distance < 0 ? Key.A : Key.D);
    }

    internal static bool Tick()
    {
        var game = GameManager.Instance;
        if (game == null || !game.IsTutorial) return false;
        var flip = game.GetComponent<MapFlipController>();
        if (hint == null)
        {
            var panel = GameObject.Find("Tutorial Hint");
            if (panel == null) return false;
            hint = panel.transform.Find("Text").GetComponent<TMP_Text>();
        }
        switch (stage)
        {
            case 0:
                Require(game.IsPreviewing, "expected a 10-second map preview");
                Require(hint.text.Contains("Memorize the map.") && hint.text.Contains("<b>IN THE DARK</b>"),
                    "intro must emphasize the game name");
                Require(game.ChargesRemaining == 0, "the first star should teach earning a charge");
                Require(UnityEngine.Object.FindObjectsByType<StarCollectible>(FindObjectsSortMode.None).Length == 1,
                    "rebuilding left duplicate world stars");
                Require(flip.CurrentPhase == MapFlipController.Phase.Idle, "timer ran before the first star");
                foreach (Transform wall in GameObject.Find("Walls").transform)
                    if (Mathf.Abs(wall.localPosition.x) < 12f && Mathf.Abs(wall.localPosition.y) < 4.5f)
                        Require(wall.localScale.y <= 3f, "the long internal wall was not removed");
                Require(UnityEngine.Object.FindAnyObjectByType<ExitTrigger>().transform.position.y > 3f,
                    "the exit should start out of reach");
                Capture("01-memorize");
                Next();
                break;
            case 1:
                Require(flip.CurrentPhase == MapFlipController.Phase.Idle, "timer ran during memorization");
                if (!game.IsPlaying || Time.time - since < 11f) return false;
                Require(hint.text.Contains("Move left or right"), "movement instruction missing");
                var arrows = hint.transform.parent.Find("Movement Keys");
                Require(arrows.gameObject.activeSelf && arrows.Find("Left Arrow") != null &&
                    arrows.Find("Right Arrow") != null, "movement key arrows missing");
                Capture("02-movement");
                backgroundBehavior = InputSystem.settings.backgroundBehavior;
                editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                inputSettingsCaptured = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                Keys(Key.D);
                Next();
                break;
            case 2:
                if (Time.time - since > 10f)
                    throw new InvalidOperationException("Tutorial movement input did not advance: position=" +
                        UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform.position +
                        ", keyboard enabled=" + keyboard.enabled + ", D=" + keyboard.dKey.isPressed);
                if (!hint.text.Contains("SPACE")) return false;
                Keys();
                Capture("03-jump");
                Next();
                break;
            case 3:
                if (Time.time - since < 1f) return false;
                Require(hint.text.Contains("SPACE"), "walking alone skipped the jump lesson");
                Keys(Key.D, Key.Space);
                Next();
                break;
            case 4:
                var star = UnityEngine.Object.FindAnyObjectByType<StarCollectible>();
                if (star != null) SteerTo(star.transform.position.x);
                else Keys();
                if (Time.time - since < .1f) return false;
                Require(hint.text.Contains("Collect all") || hint.text.Contains("Stars give"),
                    "a real jump did not advance to star collection");
                if (game.StarsCollected == 0) return false;
                starAt = Time.time;
                Require(game.IsExitUnlocked && game.ChargesRemaining == 1, "star did not unlock exit and earn a charge");
                Require(flip.CurrentPhase == MapFlipController.Phase.Idle, "first star immediately started the timer");
                Keys();
                Next();
                break;
            case 5:
                Require(flip.CurrentPhase == MapFlipController.Phase.Idle, "timer started before the five-second delay");
                if (!hint.text.Contains("Stars give")) return false;
                Capture("04-light-charge");
                Keys(Key.F);
                Next();
                break;
            case 6:
                Keys();
                if (game.RevealsUsed == 0) return false;
                Require(game.RevealsUsed == 1 && game.ChargesRemaining == 0, "F did not spend the earned charge");
                var status = new UnityEditor.SerializedObject(game).FindProperty("statusText").objectReferenceValue as TMP_Text;
                Require(!status.text.Contains("Light on"), "Light on status returned");
                if (Time.time - starAt < 4.4f)
                {
                    Require(flip.CurrentPhase == MapFlipController.Phase.Idle, "timer ran before the grace period elapsed");
                    return false;
                }
                Require(flip.CurrentPhase == MapFlipController.Phase.Idle, "five-second grace period was too short");
                Next();
                break;
            case 7:
                if (flip.CurrentPhase == MapFlipController.Phase.Idle) return false;
                Require(Time.time - starAt >= 4.9f && Time.time - starAt < 6.5f,
                    "timer did not start five seconds after the first star");
                Require(flip.CurrentPhase == MapFlipController.Phase.Counting &&
                    Mathf.Approximately(flip.CycleDuration, 10f), "normal automatic countdown missing");
                Require(hint.text.Contains("hourglass") && hint.text.Contains("flashes red"),
                    "warning explanation missing");
                Capture("05-hourglass");
                Next();
                break;
            case 8:
                if (flip.CurrentPhase != MapFlipController.Phase.Warning) return false;
                Require(flip.FlipCount == 0, "flip happened before its red warning");
                Capture("06-warning");
                Next();
                break;
            case 9:
                if (flip.FlipCount == 0 || flip.IsTurning || !hint.text.Contains("flips on its own")) return false;
                Require(hint.text.Contains("bringing new places within reach"), "agreed final wording missing");
                Require(UnityEngine.Object.FindAnyObjectByType<ExitTrigger>().transform.position.y < -3f,
                    "automatic flip did not bring the exit down");
                Capture("07-automatic-flip");
                Keys(Key.Q); // Deliberately leave this first clone away from the button.
                Next();
                break;
            case 10:
                var button = UnityEngine.Object.FindAnyObjectByType<PressureButtonSensor>();
                SteerTo(button.transform.position.x);
                if (!button.IsPressed) { plateAt = -1; return false; }
                if (plateAt < 0)
                {
                    plateAt = Time.time;
                    var misplaced = UnityEngine.Object.FindAnyObjectByType<ShadowCloneSpawner>().CurrentShadow;
                    Require(misplaced != null, "the first Q press did not create the misplaced-clone fixture");
                    var misplacedBody = misplaced.GetComponent<Rigidbody2D>();
                    Vector2 away = new Vector2(button.transform.position.x + 3, -3.9f);
                    misplaced.transform.position = away;
                    misplacedBody.position = away;
                    misplacedBody.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                }
                Keys();
                if (Time.time - plateAt < 1f) return false;
                Require(hint.text.Contains("Stand on the orange button"),
                    "a misplaced clone incorrectly completed the shadow lesson: " + hint.text);
                Require(!UnityEngine.Object.FindAnyObjectByType<ButtonControlledWall>().GetComponent<Collider2D>().enabled,
                    "standing on the pressure button did not open the gate");
                Keys(Key.Q);
                Next();
                break;
            case 11:
                Keys();
                if (Time.time - since < .5f) return false;
                Require(UnityEngine.Object.FindAnyObjectByType<ShadowCloneSpawner>().CurrentShadow != null,
                    "Q did not create a tutorial shadow");
                Require(UnityEngine.Object.FindAnyObjectByType<PressureButtonSensor>().IsPressed,
                    "shadow did not hold the tutorial button");
                Capture("08-shadow-button");
                Next();
                break;
            case 12:
                if (!game.IsFinished)
                {
                    SteerTo(UnityEngine.Object.FindAnyObjectByType<ExitTrigger>().transform.position.x);
                    return false;
                }
                Keys();
                if (hint.transform.parent.gameObject.activeSelf) return false;
                Require(PlayerProgress.IsTutorialCompleted && PlayerProgress.StartingLightCharges == 1,
                    "finishing the tutorial did not save the first permanent starting charge");
                Require(UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Any(text => text.text.Contains("STARTING LIGHT UNLOCKED")),
                    "tutorial completion did not show its permanent light reward");
                Capture("09-completed");
                game.RestartLevel();
                Next();
                break;
            case 13:
                if (!game.IsPreviewing) return false;
                Require(game.StarsCollected == 0 && game.ChargesRemaining == 0 &&
                    flip.CurrentPhase == MapFlipController.Phase.Idle && !flip.IsFlipped,
                    "replay did not reset the tutorial and delayed timer");
                Reset();
                Debug.Log("TUTORIAL_ACTION_AND_TIMING_CHECKS_SUCCESS");
                return true;
        }
        return false;
    }
}
