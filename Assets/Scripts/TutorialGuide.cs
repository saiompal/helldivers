using TMPro;
using UnityEngine;

// Teaches one action at a time, while the first star starts a delayed,
// automatic flip cycle independently of how quickly the hints are read.
public class TutorialGuide : MonoBehaviour
{
    private enum Step { Intro, Move, Jump, Star, Light, Flip, Exit, Done }

    [SerializeField, Min(0f)] private float firstStarFlipDelay = 5f;

    private Step step;
    private TMP_Text label, heading;
    private RectTransform box, movementKeys;
    private Canvas canvas;
    private Camera view;
    private Rigidbody2D player;
    private PlayerMovement movement;
    private MapFlipController mapFlip;
    private float startX, stepStartedAt;
    private float firstStarAt = -1f;
    private int revealsAtFirstStar;
    private bool hasJumped, flipTimerStarted;
    private Vector2 layoutScreen;
    private float layoutScale;

    private void Start()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        mapFlip = GetComponent<MapFlipController>();
        canvas = FindAnyObjectByType<Canvas>();
        view = Camera.main;
        if (movement == null || canvas == null || view == null || mapFlip == null)
        {
            enabled = false;
            return;
        }

        player = movement.GetComponent<Rigidbody2D>();
        startX = player.position.x;
        movement.Jumped += OnJumped;
        canvas = canvas.rootCanvas;
        CreateHint();
        SetStep(Step.Intro);
    }

    private void OnDestroy()
    {
        if (movement != null) movement.Jumped -= OnJumped;
    }

    private void OnJumped() => hasJumped = true;

    private void Update()
    {
        var game = GameManager.Instance;
        if (game == null || player == null || box == null) return;
        if (game.IsFinished)
        {
            SetStep(Step.Done);
            return;
        }

        // A fast pickup, early F press or slow reader must never prevent
        // or restart the automatic flip timer.
        if (firstStarAt < 0f && game.StarsCollected > 0)
        {
            firstStarAt = Time.time;
            revealsAtFirstStar = game.RevealsUsed;
        }
        if (!flipTimerStarted && firstStarAt >= 0f &&
            Time.time - firstStarAt >= firstStarFlipDelay)
        {
            mapFlip.Begin();
            flipTimerStarted = true;
        }

        float hintAge = Time.time - stepStartedAt;
        switch (step)
        {
            case Step.Intro:
                if (game.IsPlaying) SetStep(Step.Move);
                break;
            case Step.Move:
                if (Mathf.Abs(player.position.x - startX) > 1.5f && hintAge >= .75f)
                    SetStep(Step.Jump);
                break;
            case Step.Jump:
                if (hasJumped) SetStep(Step.Star);
                break;
            case Step.Star:
                if (game.StarsCollected > 0 && hintAge >= 1.5f) SetStep(Step.Light);
                break;
            case Step.Light:
                // Allow time to read the charge lesson without making the
                // map's automatic behaviour depend on pressing F.
                if (hintAge >= 2f && (game.RevealsUsed > revealsAtFirstStar || flipTimerStarted))
                    SetStep(Step.Flip);
                break;
            case Step.Flip:
                if (mapFlip.FlipCount > 0 && hintAge >= 2f) SetStep(Step.Exit);
                break;
        }
    }

    private void SetStep(Step next)
    {
        if (next == step && label != null && !string.IsNullOrEmpty(label.text)) return;
        step = next;
        stepStartedAt = Time.time;
        box.gameObject.SetActive(next != Step.Done);
        if (next == Step.Done) return;
        // The intro shares the central countdown, leaving the map's floor,
        // star and block visible throughout the memorization phase.
        box.GetComponent<UiShapeGraphic>().enabled = next != Step.Intro;
        heading.enabled = label.enabled = next != Step.Intro;

        bool mobile = DeviceVisibility.IsMobileDevice;
        heading.text = $"TUTORIAL   {(int)next + 1} / 7";
        label.text = next switch
        {
            Step.Intro => "Memorize the map. Soon, you'll be...\n<size=140%><b>IN THE DARK</b></size>",
            Step.Move => mobile ? "Tilt your device to move left or right." : "Move left or right.",
            Step.Jump => mobile ? "Tap the screen to jump over the block." : "Press <b>SPACE</b> to jump over the block.",
            Step.Star => "Collect all the <color=#FFD54A>stars</color> to unlock the exit.",
            Step.Light => mobile
                ? "Stars give you light charges.\nTap <b>LIGHT</b> to briefly reveal the map."
                : "Stars give you light charges.\nPress <b>F</b> to briefly reveal the map.",
            Step.Flip => "Watch the <color=#FF5A4F>hourglass</color>. Your light flashes red\nbefore the map flips.",
            Step.Exit => "The map flips on its own,\nbringing new places within reach.",
            _ => string.Empty
        };
        movementKeys.gameObject.SetActive(next == Step.Move && !mobile);
        RefreshLayout();
    }

    private void LateUpdate()
    {
        if (box == null || !box.gameObject.activeSelf) return;
        if (layoutScreen != new Vector2(Screen.width, Screen.height) ||
            !Mathf.Approximately(layoutScale, canvas.scaleFactor)) RefreshLayout();

        float scale = Mathf.Max(canvas.scaleFactor, .001f);
        Rect safe = Screen.safeArea;
        float margin = 16f / scale;
        Vector2 size = box.sizeDelta;
        Vector2 screen = view.WorldToScreenPoint(player.position) / scale;
        float minX = safe.xMin / scale + size.x * .5f + margin;
        float maxX = safe.xMax / scale - size.x * .5f - margin;
        float minY = safe.yMin / scale + margin;
        float maxY = safe.yMax / scale - Mathf.Max(130f / scale, 195f) - size.y;
        float x = step == Step.Intro ? safe.center.x / scale : screen.x;
        float y = step == Step.Intro ? minY : screen.y + 48f / scale;
        box.anchoredPosition = new Vector2(Mathf.Clamp(x, minX, Mathf.Max(minX, maxX)),
            Mathf.Clamp(y, minY, Mathf.Max(minY, maxY)));
    }

    private void RefreshLayout()
    {
        float scale = Mathf.Max(canvas.scaleFactor, .001f);
        layoutScale = scale;
        layoutScreen = new Vector2(Screen.width, Screen.height);
        float padding = 16f / scale;
        float gap = 8f / scale;
        float width = Mathf.Min(Screen.safeArea.width - 32f, 760f) / scale;
        label.fontSize = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * .034f, 18f, 28f) / scale;
        heading.fontSize = 12f / scale;
        float headerHeight = 18f / scale;
        float textHeight = label.GetPreferredValues(label.text, width - padding * 2, float.PositiveInfinity).y;
        label.rectTransform.sizeDelta = new Vector2(width - padding * 2, textHeight);
        label.rectTransform.anchoredPosition = new Vector2(0, -padding - headerHeight - gap);
        heading.rectTransform.sizeDelta = new Vector2(width - padding * 2, headerHeight);
        heading.rectTransform.anchoredPosition = new Vector2(0, -padding);
        float keyScale = label.fontSize / 36f;
        movementKeys.localScale = Vector3.one * keyScale;
        float keyHeight = movementKeys.gameObject.activeSelf ? 64f * keyScale + gap : 0f;
        movementKeys.anchoredPosition = new Vector2(0, -padding - headerHeight - gap - textHeight - gap - 32f * keyScale);
        box.sizeDelta = new Vector2(width, padding * 2 + headerHeight + gap + textHeight + keyHeight);
    }

    private void CreateHint()
    {
        var panel = GameUi.Shape("Tutorial Hint", canvas.transform, UiShapeGraphic.Shape.Rounded,
            Vector2.zero, Vector2.one, new Color(.032f, .042f, .058f, .96f),
            new Color(.65f, .71f, .78f, .8f), 1.5f, 18);
        box = panel.rectTransform;
        box.anchorMin = box.anchorMax = Vector2.zero;
        box.pivot = new Vector2(.5f, 0);
        heading = GameUi.Text("Progress", box, "", 18, Vector2.zero, Vector2.one,
            new Color(.65f, .71f, .78f));
        label = GameUi.Text("Text", box, "", 40, Vector2.zero, Vector2.one);
        foreach (var text in new[] { heading, label })
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1);
            text.rectTransform.pivot = new Vector2(.5f, 1);
        }
        label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.Normal;
        movementKeys = GameUi.Rect("Movement Keys", box, Vector2.zero, new Vector2(320, 64));
        movementKeys.anchorMin = movementKeys.anchorMax = new Vector2(.5f, 1);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var key = GameUi.Shape(i == 0 ? "A Key" : "D Key", movementKeys, UiShapeGraphic.Shape.Rounded,
                new Vector2(side * 55, 0), new Vector2(58, 54), Color.clear, GameUi.White, 2, 8);
            GameUi.Text("Label", key.transform, i == 0 ? "A" : "D", 36, Vector2.zero, new Vector2(58, 54));
            var arrow = GameUi.Shape(i == 0 ? "Left Arrow" : "Right Arrow", movementKeys,
                UiShapeGraphic.Shape.Arrow, new Vector2(side * 120, 0), new Vector2(44, 44),
                Color.clear, GameUi.White, 2);
            if (i == 1) arrow.transform.localRotation = Quaternion.Euler(0, 0, 180);
        }
    }
}
