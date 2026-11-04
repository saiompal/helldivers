using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    public bool IsPlaying => IsActive;
    public bool IsFinished => phase == Phase.Won || phase == Phase.Failed;
    public int StarsCollected => starsCollected;
    public int RevealsUsed => revealsUsed;
    public bool IsTutorial => gameObject.scene.name == "Tutorial";
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
    [SerializeField, Min(0f)] private float lightRadiusGrowthPerStar = 0.3f;

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
    private float runEndTime;
    private string levelName;
    private int attempt;
    private int revealsUsed;
    private string toast;
    private float toastUntil;
    private Coroutine chargesPulse;
    private float targetVisionScale = 1f;
    private float currentVisionScale = 1f;
    private Light2D playerVisionLight;
    private float initialVisionInnerRadius;
    private float initialVisionOuterRadius;

    private bool IsActive =>
        phase == Phase.Playing ||
        phase == Phase.Revealing;

    private float RunSeconds => phase switch
    {
        Phase.Preview => 0f,
        Phase.Won or Phase.Failed => runEndTime - runStartTime,
        _ => Time.time - runStartTime
    };

    private int FlipCount => mapFlip != null ? mapFlip.FlipCount : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        levelName = gameObject.scene.name;

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
        AddMazeLegend();
        UpdateChargesText();
        UpdateStarsText();

        attempt = GameAnalytics.NextAttempt(levelName);
        GameAnalytics.Record("levelStart",
            ("levelName", levelName),
            ("attempt", attempt));

        StartCoroutine(BeginLevel());
    }

    private void AddMazeLegend()
    {
        PortalController portal = FindAnyObjectByType<PortalController>();
        if (portal != null)
            AddLegendEntry("Portal", portal.GetComponentInChildren<SpriteRenderer>());

        Hazard spike = FindAnyObjectByType<Hazard>();
        if (spike != null)
            AddLegendEntry("Spike", spike.GetComponentInChildren<SpriteRenderer>());
    }

    private void AddLegendEntry(string entryName, SpriteRenderer appearance)
    {
        if (appearance == null || statusText.canvas == null)
            return;

        Transform items = statusText.canvas.rootCanvas.transform.Find("HUD Bottom Bar/LEGEND/Items");
        if (items == null || items.Find(entryName) != null)
            return;

        // Older HUD builds used this name for the spike legend entry.
        if (entryName == "Spike" && items.Find("Spike trap") != null)
            return;

        Transform template = items.Find("Exit");
        if (template == null)
            return;

        // Reuse existing styling when maze objects were added after the HUD was built.
        GameObject entry = Instantiate(template.gameObject, items, false);
        entry.name = entryName;
        TMP_Text label = entry.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = entryName;

        Transform shape = entry.transform.Find("Icon/Shape");
        Image icon = shape != null ? shape.GetComponent<Image>() : null;
        if (icon != null)
        {
            icon.sprite = appearance.sprite;
            icon.color = appearance.color;
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
        }

        GridLayoutGroup grid = items.GetComponent<GridLayoutGroup>();
        if (grid != null)
            grid.constraintCount = items.childCount <= 4 ? 2 : 3;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            HandleInput(keyboard);
        }

        AnimateVisionRadius();

        if (IsActive)
        {
            UpdateStatusText();
        }
    }

    private void AnimateVisionRadius()
    {
        if (playerVisionLight == null ||
            Mathf.Approximately(currentVisionScale, targetVisionScale))
        {
            return;
        }

        currentVisionScale = Mathf.MoveTowards(
            currentVisionScale, targetVisionScale, 1.5f * Time.deltaTime);
        playerVisionLight.pointLightInnerRadius = initialVisionInnerRadius * currentVisionScale;
        playerVisionLight.pointLightOuterRadius = initialVisionOuterRadius * currentVisionScale;
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
            RecordLeftLevel("levelQuit");
            GameAnalytics.Flush();
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

        // The countdown sits in the middle of the screen, where it gets
        // read, instead of in the top bar. It's see-through so the map
        // underneath can still be studied.
        TMP_Text countdown = CreateCountdownText();
        statusText.text = string.Empty;

        while (timeRemaining > 0f)
        {
            if (countdown != null)
            {
                countdown.text =
                    $"<size=40%>MEMORIZE</size>\n<b>{Mathf.CeilToInt(timeRemaining)}</b>";
            }

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        if (countdown != null)
        {
            Destroy(countdown.gameObject);
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

    private TMP_Text CreateCountdownText()
    {
        if (statusText.canvas == null)
        {
            return null;
        }

        GameObject go = new GameObject("Memorize Countdown", typeof(RectTransform));
        go.transform.SetParent(statusText.canvas.rootCanvas.transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(900f, 400f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = 200f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 1f, 1f, 0.6f);
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 200);
        text.raycastTarget = false;
        return text;
    }

    private IEnumerator TemporaryReveal()
    {
        phase = Phase.Revealing;
        chargesRemaining--;
        revealsUsed++;
        globalLight.intensity = 1f;
        if (cameraFollow != null)
        {
            cameraFollow.SetRevealView(true);
        }
        UpdateChargesText();

        GameAnalytics.Record("revealUsed",
            ("levelName", levelName),
            ("runSeconds", RunSeconds),
            ("starsCollected", starsCollected),
            ("chargesRemaining", chargesRemaining));

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
        ShowChargeGained();

        GameAnalytics.Record("starCollected",
            ("levelName", levelName),
            ("runSeconds", RunSeconds),
            ("starsCollected", starsCollected),
            ("starsTotal", starsTotal));

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

    // Tells the player the star gave them a light charge: a "+1 light" that
    // floats up from the player, and a pop on the charge counter in the HUD.
    private void ShowChargeGained()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            FloatingText.Spawn(
                player.transform.position + Vector3.up * 0.8f,
                "+1 light");
        }

        if (chargesText != null)
        {
            if (chargesPulse != null)
            {
                StopCoroutine(chargesPulse);
            }
            chargesPulse = StartCoroutine(PulseChargesText());
        }
    }

    private IEnumerator PulseChargesText()
    {
        const float seconds = 0.5f;
        Transform target = chargesText.transform;

        for (float age = 0f; age < seconds; age += Time.deltaTime)
        {
            // Jumps to 1.8x and eases back down.
            float t = age / seconds;
            target.localScale = Vector3.one * (1f + 0.8f * (1f - t) * (1f - t));
            yield return null;
        }

        target.localScale = Vector3.one;
        chargesPulse = null;
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
        // The light swells to the new size over a moment (see
        // AnimateVisionRadius) so the growth is easy to notice.
        targetVisionScale = 1f + starsCollected * lightRadiusGrowthPerStar;
    }

    private void WinLevel()
    {
        if (!IsActive)
        {
            return;
        }

        EndRun(Phase.Won);

        float runSeconds = RunSeconds;
        GameAnalytics.Record("levelCompleted",
            ("levelName", levelName),
            ("attempt", attempt),
            ("runSeconds", runSeconds),
            ("starsCollected", starsCollected),
            ("starsTotal", starsTotal),
            ("revealsUsed", revealsUsed),
            ("flipCount", FlipCount));
        GameAnalytics.Flush();

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
        GameAnalytics.Record("levelFailed",
            ("levelName", levelName),
            ("attempt", attempt),
            ("failReason", reason),
            ("runSeconds", RunSeconds),
            ("starsCollected", starsCollected),
            ("revealsUsed", revealsUsed),
            ("flipCount", FlipCount));
        GameAnalytics.Flush();

        statusText.text =
            $"<color={DangerColor}>{reason}</color>   Press R to try again";
    }

    private void EndRun(Phase outcome)
    {
        StopAllCoroutines();
        chargesPulse = null;
        if (chargesText != null)
        {
            chargesText.transform.localScale = Vector3.one;
        }

        if (mapFlip != null)
        {
            mapFlip.Stop();
        }

        phase = outcome;
        runEndTime = Time.time;
        globalLight.intensity = 1f;

        // Pull back to show the whole route.
        if (cameraFollow != null)
        {
            cameraFollow.ShowOverview();
        }
    }

    public void RestartLevel()
    {
        RecordLeftLevel("levelRestarted");

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    // Restarting or quitting records how far the run got, including runs
    // that had already been won or failed.
    private void RecordLeftLevel(string eventName)
    {
        GameAnalytics.Record(eventName,
            ("levelName", levelName),
            ("attempt", attempt),
            ("phase", phase.ToString()),
            ("runSeconds", RunSeconds),
            ("starsCollected", starsCollected),
            ("revealsUsed", revealsUsed),
            ("flipCount", FlipCount));
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
