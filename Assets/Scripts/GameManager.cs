using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Runs a level: a short preview of the whole map with the lights on, then
// the camera zooms in on the player and the lights go out, leaving only a
// small circle around them. F spends a charge to
// light the whole map for a moment, the map flips every so often, and the
// exit only opens once every star is collected.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsExitUnlocked => starsCollected >= starsTotal;
    public bool IsPreviewing => phase == Phase.Preview;
    public bool IsLevel3 => gameObject.scene.name == "Level3";

    // Fires once, when the last star is collected.
    public event Action ExitUnlocked;

    public bool CanPlayerMove =>
        IsActive && (mapFlip == null || !mapFlip.IsTurning);

    [Header("Level Rules")]
    [SerializeField, Min(0f)] private float previewSeconds = 5f;
    [SerializeField, Min(0.1f)] private float revealSeconds = 2f;
    [SerializeField, Min(0)] private int revealCharges = 3;
    [Tooltip("Global light level while dark. The player's own light still shows a circle.")]
    [SerializeField, Range(0f, 1f)] private float darkIntensity = 0f;
    [Tooltip("Each collected star adds this share of the light's original radius.")]
    [SerializeField, Min(0f)] private float lightRadiusGrowthPerStar = 0.1f;

    [Header("Scenes")]
    [SerializeField] private string menuScene = "MainMenu";

    [Header("Scene References")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Optional. Turns the maze every so often.")]
    [SerializeField] private MapFlipController mapFlip;
    [Tooltip("Optional. Zooms in on the player when play starts.")]
    [SerializeField] private CameraFollow cameraFollow;
    [Tooltip("Optional. Shows the remaining reveal-charge count.")]
    [SerializeField] private TMP_Text chargesText;
    [Tooltip("Optional. Shows stars collected out of the total.")]
    [SerializeField] private TMP_Text starsText;

    private enum Phase
    {
        Preview,
        Playing,
        Revealing,
        Won,
        Failed
    }

    private const string LanternColor = "#FFD54A";
    private const string DangerColor = "#FF5A4F";
    private const string SuccessColor = "#5CE68A";
    private const float ToastSeconds = 1.5f;

    private Phase phase;
    private int chargesRemaining;
    private int starsCollected;
    private int starsTotal;
    private float runStartTime;
    private string toast;
    private float toastUntil;
    private Light2D playerVisionLight;
    private float initialVisionInnerRadius;
    private float initialVisionOuterRadius;

    private bool IsActive =>
        phase == Phase.Playing ||
        phase == Phase.Revealing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Counted here rather than in Start so the exit can read the lock
        // state from its own Start.
        starsTotal = FindObjectsByType<StarCollectible>(FindObjectsSortMode.None).Length;
        CachePlayerVisionLight();
    }

    private void Start()
    {
        if (globalLight == null || statusText == null)
        {
            Debug.LogError(
                "GameManager needs Global Light and Status Text references."
            );
            enabled = false;
            return;
        }

        chargesRemaining = revealCharges;
        UpdateChargesText();
        UpdateStarsText();
        StartCoroutine(BeginLevel());
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            HandleInput(keyboard);
        }

        if (IsActive)
        {
            UpdateStatusText();
        }
    }

    private void HandleInput(Keyboard keyboard)
    {
        if (keyboard.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }

        if (keyboard.escapeKey.wasPressedThisFrame &&
            Application.CanStreamedLevelBeLoaded(menuScene))
        {
            SceneManager.LoadScene(menuScene);
        }

        if (phase == Phase.Playing &&
            keyboard.fKey.wasPressedThisFrame &&
            chargesRemaining > 0)
        {
            StartCoroutine(TemporaryReveal());
        }

        if (phase == Phase.Won && keyboard.nKey.wasPressedThisFrame)
        {
            LoadNextLevel();
        }
    }

    private IEnumerator BeginLevel()
    {
        phase = Phase.Preview;
        globalLight.intensity = 1f;

        float timeRemaining = previewSeconds;

        while (timeRemaining > 0f)
        {
            statusText.text =
                $"Memorize the level   <b>{Mathf.CeilToInt(timeRemaining)}</b>";

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        globalLight.intensity = darkIntensity;
        phase = Phase.Playing;
        runStartTime = Time.time;

        if (cameraFollow != null)
        {
            cameraFollow.FollowPlayer();
        }

        if (mapFlip != null)
        {
            mapFlip.Begin();
        }
    }

    private IEnumerator TemporaryReveal()
    {
        phase = Phase.Revealing;
        chargesRemaining--;
        globalLight.intensity = 1f;
        if (cameraFollow != null)
        {
            cameraFollow.SetRevealView(true);
        }
        UpdateChargesText();

        yield return new WaitForSeconds(revealSeconds);

        globalLight.intensity = darkIntensity;
        if (cameraFollow != null)
        {
            cameraFollow.SetRevealView(false);
        }
        phase = Phase.Playing;
    }

    public bool TryCollectStar()
    {
        if (!IsActive)
        {
            return false;
        }

        starsCollected++;
        UpdatePlayerVisionRadius();
        UpdateStarsText();

        // Every star in every level earns one stored F reveal. Earned charges
        // may exceed the three starting charges.
        chargesRemaining++;
        UpdateChargesText();

        const string reward = "  \u00B7  +1 light charge";

        if (IsExitUnlocked)
        {
            ShowToast($"<color={SuccessColor}>All stars found. The exit is open{reward}</color>");
            ExitUnlocked?.Invoke();
        }
        else
        {
            ShowToast(
                $"<color={LanternColor}>Star collected  {starsCollected} / {starsTotal}{reward}</color>"
            );
        }

        return true;
    }

    // Called when the player touches the exit.
    public void TryExit()
    {
        if (!IsActive)
        {
            return;
        }

        if (IsExitUnlocked)
        {
            WinLevel();
            return;
        }

        int remaining = starsTotal - starsCollected;
        string stars = remaining == 1 ? "1 more star" : $"{remaining} more stars";
        ShowToast($"<color={DangerColor}>Locked. Find {stars}</color>");
    }

    private void ShowToast(string message)
    {
        toast = message;
        toastUntil = Time.time + ToastSeconds;
    }

    private void UpdateStatusText()
    {
        string state = phase == Phase.Revealing
            ? $"<color={LanternColor}>Light on</color>"
            : IsExitUnlocked ? "Reach the exit" : "Find the stars in the dark";

        if (mapFlip != null)
        {
            switch (mapFlip.CurrentPhase)
            {
                case MapFlipController.Phase.Warning:
                    state = $"<color={DangerColor}><b>MAP FLIPPING   " +
                            $"{Mathf.CeilToInt(mapFlip.SecondsUntilFlip)}</b></color>";
                    break;

                case MapFlipController.Phase.Turning:
                    state = $"<color={DangerColor}><b>FLIP!</b></color>";
                    break;

                case MapFlipController.Phase.Counting:
                    state += $"   \u00B7   Flip in <b>{Mathf.CeilToInt(mapFlip.SecondsUntilFlip)}</b>";
                    break;
            }
        }

        if (Time.time < toastUntil)
        {
            state = $"{toast}   \u00B7   {state}";
        }

        statusText.text = state;
    }

    private void UpdateChargesText()
    {
        if (chargesText == null)
        {
            return;
        }

        // A compact count remains readable when star rewards take the total
        // above the three starting charges.
        chargesText.text =
            $"<color={LanternColor}>\u25CF \u00D7 {chargesRemaining}</color>";
    }

    private void UpdateStarsText()
    {
        if (starsText != null)
        {
            starsText.text = $"{starsCollected} / {starsTotal}";
        }
    }

    private void CachePlayerVisionLight()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            return;
        }

        Light2D[] lights = player.GetComponentsInChildren<Light2D>(true);
        foreach (Light2D light in lights)
        {
            if (light.gameObject.name != "Vision Light")
            {
                continue;
            }

            playerVisionLight = light;
            initialVisionInnerRadius = light.pointLightInnerRadius;
            initialVisionOuterRadius = light.pointLightOuterRadius;
            return;
        }
    }

    private void UpdatePlayerVisionRadius()
    {
        if (playerVisionLight == null)
        {
            CachePlayerVisionLight();
        }

        if (playerVisionLight == null)
        {
            return;
        }

        // Always grow from the starting radii: 110%, 120%, 130%, ...
        // This deliberately does not compound the previous star's increase.
        float scale = 1f + starsCollected * lightRadiusGrowthPerStar;
        playerVisionLight.pointLightInnerRadius = initialVisionInnerRadius * scale;
        playerVisionLight.pointLightOuterRadius = initialVisionOuterRadius * scale;
    }

    private void WinLevel()
    {
        if (!IsActive)
        {
            return;
        }

        EndRun(Phase.Won);

        float runSeconds = Time.time - runStartTime;
        string next = HasNextLevel()
            ? "N  next level   \u00B7   R  replay"
            : "R  replay   \u00B7   Esc  menu";
        statusText.text =
            $"<color={SuccessColor}>Escaped in {runSeconds:0.0}s</color>   " +
            $"<color={LanternColor}>Stars {starsCollected} / {starsTotal}</color>   {next}";
    }

    public void FailLevel(string reason)
    {
        if (!IsActive)
        {
            return;
        }

        EndRun(Phase.Failed);
        statusText.text =
            $"<color={DangerColor}>{reason}</color>   Press R to try again";
    }

    private void EndRun(Phase outcome)
    {
        StopAllCoroutines();

        if (mapFlip != null)
        {
            mapFlip.Stop();
        }

        phase = outcome;
        globalLight.intensity = 1f;

        // Pull back to show the whole route.
        if (cameraFollow != null)
        {
            cameraFollow.ShowOverview();
        }
    }

    public void RestartLevel()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private static bool HasNextLevel()
    {
        return SceneManager.GetActiveScene().buildIndex + 1 <
               SceneManager.sceneCountInBuildSettings;
    }

    private void LoadNextLevel()
    {
        if (HasNextLevel())
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
