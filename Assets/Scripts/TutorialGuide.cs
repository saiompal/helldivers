using TMPro;
using UnityEngine.UI;
using UnityEngine;

// Walks the player through the tutorial level one short hint at a time:
// move, jump, light, star, map flip, exit. Each hint goes away the moment
// the player does the thing it asks for. Added to the Tutorial scene by
// TutorialBuilder.
public class TutorialGuide : MonoBehaviour
{
    private enum Step { Intro, Move, Jump, Light, Star, Flip, Exit, Done }

    private const float AboveHead = 90f;
    private const float EdgeMargin = 20f;
    private const float TopBarHeight = 90f;
    private const float BottomBarHeight = 140f;

    private Step step;
    private TextMeshProUGUI label;
    private RectTransform box;
    private Canvas canvas;
    private Camera view;
    private Rigidbody2D player;
    private MapFlipController mapFlip;
    private float startX;
    private bool flipRequested;

    private void Start()
    {
        PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
        mapFlip = GetComponent<MapFlipController>();
        canvas = FindAnyObjectByType<Canvas>();
        view = Camera.main;

        if (movement == null || canvas == null || view == null)
        {
            enabled = false;
            return;
        }

        player = movement.GetComponent<Rigidbody2D>();
        startX = player.position.x;
        canvas = canvas.rootCanvas;
        label = CreateLabel(canvas.transform, out box);
        SetStep(Step.Intro);
    }

    private void LateUpdate()
    {
        if (!enabled || !box.gameObject.activeInHierarchy)
        {
            return;
        }

        // The hint rides above the player, kept inside the space between
        // the HUD bars.
        float scale = canvas.scaleFactor;
        Vector2 screen = view.WorldToScreenPoint(player.position) / scale;
        Vector2 size = box.sizeDelta;
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        float x = Mathf.Clamp(screen.x, size.x * 0.5f + EdgeMargin, width - size.x * 0.5f - EdgeMargin);
        float y = Mathf.Clamp(screen.y + AboveHead, BottomBarHeight, height - TopBarHeight - size.y);
        box.anchoredPosition = new Vector2(x, y);
    }

    private void Update()
    {
        GameManager game = GameManager.Instance;
        if (game == null)
        {
            return;
        }

        if (game.IsFinished)
        {
            SetStep(Step.Done);
            return;
        }

        float moved = player.position.x - startX;

        switch (step)
        {
            case Step.Intro:
                if (game.IsPlaying) SetStep(Step.Move);
                break;
            case Step.Move:
                // Held as a progress bar of sorts: any real movement counts.
                if (Mathf.Abs(moved) > 1.5f) SetStep(Step.Jump);
                break;
            case Step.Jump:
                if (moved > 5.5f) SetStep(Step.Light);
                break;
            case Step.Light:
                if (game.RevealsUsed > 0) SetStep(Step.Star);
                break;
            case Step.Star:
                if (game.StarsCollected > 0) SetStep(Step.Flip);
                break;
            case Step.Flip:
                if (!flipRequested && mapFlip != null)
                {
                    flipRequested = true;
                    mapFlip.FlipSoon();
                }
                if (mapFlip == null || mapFlip.FlipCount > 0) SetStep(Step.Exit);
                break;
        }
    }

    private void SetStep(Step next)
    {
        step = next;
        box.gameObject.SetActive(next != Step.Done);
        label.text = next switch
        {
            Step.Intro => "Memorize the map: exit, star, wall. Lights out soon",
            Step.Move => "<b>A / D</b>  to move",
            Step.Jump => "<b>SPACE</b>  to jump over the block",
            Step.Light => "Press <b>F</b> to light up the map",
            Step.Star => "Grab the <color=#FFD54A>star</color> to open the exit",
            Step.Flip => "<color=#FF5A4F>Your light pulses red, then the map FLIPS</color>",
            Step.Exit => "Gravity stays down. Reach the <color=#5CE68A>exit</color>",
            _ => string.Empty
        };
    }

    private static TextMeshProUGUI CreateLabel(Transform canvas, out RectTransform box)
    {
        GameObject panel = new GameObject("Tutorial Hint", typeof(RectTransform));
        panel.transform.SetParent(canvas, false);

        box = (RectTransform)panel.transform;
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.zero;
        box.pivot = new Vector2(0.5f, 0f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        background.raycastTarget = false;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 16, 18);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 44f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
