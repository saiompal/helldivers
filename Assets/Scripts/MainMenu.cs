using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private const int LevelCount = 5;

    [Tooltip("Hidden in web builds, where the browser tab can't be closed.")]
    [SerializeField] private GameObject quitButton;

    private const string TutorialScene = "Tutorial";
    private const string TutorialButtonName = "Tutorial Button";
    private const float RowButtonWidth = 260f;

    private GameObject howToPlayPanel;
    private readonly List<GameObject> hiddenByHelp = new List<GameObject>();
    private int helpButtonIndex;

    private void Start()
    {
        EnsureLevelButtons();
        EnsureTutorialAndHelp();
        ClearPreselection();

        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            quitButton.SetActive(false);
        }
    }

    // Keeps older checked-in menu scenes usable immediately while the editor
    // builder creates the same level-button layout for future rebuilds.
    private void EnsureLevelButtons()
    {
        Transform levels = transform.Find("Levels");
        if (levels == null)
        {
            Debug.LogError("MainMenu needs a Levels container.", this);
            return;
        }

        List<Button> buttons = new List<Button>();
        for (int i = 0; i < levels.childCount; i++)
        {
            Button button = levels.GetChild(i).GetComponent<Button>();
            if (button != null && button.name != TutorialButtonName)
            {
                buttons.Add(button);
            }
        }

        if (buttons.Count == 0)
        {
            Debug.LogError("MainMenu needs at least one level button template.", this);
            return;
        }

        while (buttons.Count < LevelCount)
        {
            Button clone = Instantiate(buttons[0], levels);
            buttons.Add(clone);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            bool active = i < LevelCount;
            buttons[i].gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            string levelName = $"Level{i}";
            buttons[i].gameObject.name = $"Level {i} Button";
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = $"Level {i}";
            }

            // Replace the event object so cloned buttons do not retain the
            // template's serialized scene target.
            buttons[i].onClick = new Button.ButtonClickedEvent();
            buttons[i].onClick.AddListener(() => LoadLevel(levelName));
        }
    }

    // Older checked-in menu scenes only have the level row and a permanent
    // how-to-play panel. Hide the panel behind a button and add the tutorial
    // button, cloning the Quit button for the style. A rebuilt menu already
    // has both and is left alone.
    private void EnsureTutorialAndHelp()
    {
        Transform panel = transform.Find("How To Play");
        howToPlayPanel = panel != null ? panel.gameObject : null;
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
        }

        if (quitButton == null)
        {
            return;
        }

        if (transform.Find("How To Play Button") == null)
        {
            Button help = CloneQuitButton("How To Play Button", "How To Play", 0);
            help.onClick.AddListener(ToggleHowToPlay);
        }

        // The Tutorial button leads the level row. Six buttons at the default
        // width would be wider than the screen, so the row buttons are narrower.
        Transform levels = transform.Find("Levels");
        if (levels == null)
        {
            return;
        }

        Transform tutorialButton = transform.Find(TutorialButtonName);
        if (tutorialButton == null)
        {
            tutorialButton = levels.Find(TutorialButtonName);
        }

        if (tutorialButton == null)
        {
            Button tutorial = CloneQuitButton(TutorialButtonName, "Tutorial", 0);
            tutorial.onClick.AddListener(() => LoadLevel(TutorialScene));
            tutorialButton = tutorial.transform;
        }

        tutorialButton.SetParent(levels, false);
        tutorialButton.SetAsFirstSibling();

        foreach (Transform child in levels)
        {
            LayoutElement size = child.GetComponent<LayoutElement>();
            if (size != null)
            {
                size.preferredWidth = RowButtonWidth;
            }
        }

        // How To Play stays on its own just above the row.
        Transform helpButton = transform.Find("How To Play Button");
        helpButton.SetSiblingIndex(Mathf.Max(0, levels.GetSiblingIndex() - 1));
    }

    private Button CloneQuitButton(string objectName, string text, int siblingIndex)
    {
        GameObject clone = Instantiate(quitButton, transform);
        clone.name = objectName;
        clone.SetActive(true);
        clone.transform.SetSiblingIndex(siblingIndex);

        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        TMP_Text label = clone.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = text;
        }
        return button;
    }

    // No button starts highlighted. The event system selects its first
    // button when it activates, which happens after Start, so clear both.
    private static void ClearPreselection()
    {
        EventSystem system = EventSystem.current;
        if (system != null)
        {
            system.firstSelectedGameObject = null;
            system.SetSelectedGameObject(null);
        }
    }

    // Opening How To Play clears the menu and shows only the instructions
    // with a Back button; closing restores everything as it was.
    public void ToggleHowToPlay()
    {
        if (howToPlayPanel == null)
        {
            Transform panel = transform.Find("How To Play");
            howToPlayPanel = panel != null ? panel.gameObject : null;
        }

        Transform helpButton = transform.Find("How To Play Button");
        if (howToPlayPanel == null || helpButton == null)
        {
            return;
        }

        bool open = !howToPlayPanel.activeSelf;
        TMP_Text label = helpButton.GetComponentInChildren<TMP_Text>();

        if (open)
        {
            hiddenByHelp.Clear();
            helpButtonIndex = helpButton.GetSiblingIndex();
            foreach (Transform child in transform)
            {
                if (child != helpButton && child.gameObject != howToPlayPanel &&
                    child.gameObject.activeSelf)
                {
                    hiddenByHelp.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }
            }

            helpButton.SetAsLastSibling();
        }
        else
        {
            foreach (GameObject hidden in hiddenByHelp)
            {
                hidden.SetActive(true);
            }

            hiddenByHelp.Clear();
            helpButton.SetSiblingIndex(helpButtonIndex);
        }

        if (open)
        {
            BuildHowToPlayContent(howToPlayPanel.transform);
        }

        howToPlayPanel.SetActive(open);
        if (label != null)
        {
            label.text = open ? "Back" : "How To Play";
        }
    }

    private static readonly (string Heading, string Body, string Color)[] Rules =
    {
        ("MEMORIZE", "Study the map before the lights go out.", "#FFD54A"),
        ("LIGHT", "In the dark you only see a small circle. Press <b>F</b> to light the whole map. Charges are limited.", "#FFD54A"),
        ("FLIP", "Every few seconds the map flips upside down. Your light pulses red just before.", "#FF5A4F"),
        ("GRAVITY", "Gravity always pulls down, so a wall you can't climb may be one you can walk under.", "#FF5A4F"),
        ("STARS", "Collect every star to unlock the exit. Each star also gives you +1 light charge.", "#FFD54A"),
        ("EXIT", "Reach the exit to escape.", "#5CE68A"),
    };

    private const string ControlsLine =
        "<b>A / D</b> move        <b>SPACE</b> jump        <b>F</b> light        <b>R</b> restart        <b>ESC</b> menu";

    // Rebuilt every time the panel opens, so it looks the same in old and
    // new menu scenes: a big title, one row per rule and a controls line.
    private static void BuildHowToPlayContent(Transform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Destroy(panel.GetChild(i).gameObject);
        }

        VerticalLayoutGroup column = panel.GetComponent<VerticalLayoutGroup>();
        if (column == null)
        {
            column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        column.spacing = 26f;
        column.padding = new RectOffset(70, 70, 50, 50);
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;

        TMP_Text title = AddText(panel, "Header", "HOW TO PLAY", 72f,
            new Color(1f, 0.84f, 0.29f), 1200f);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 10f;
        title.margin = new Vector4(0f, 0f, 0f, 14f);

        foreach (var rule in Rules)
        {
            GameObject row = new GameObject("Rule", typeof(RectTransform));
            row.transform.SetParent(panel, false);
            HorizontalLayoutGroup line = row.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 40f;
            line.childAlignment = TextAnchor.UpperLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;

            TMP_Text heading = AddText(row.transform, "Heading", rule.Heading, 40f, Color.white, 230f);
            heading.text = $"<color={rule.Color}>{rule.Heading}</color>";
            heading.fontStyle = FontStyles.Bold;
            heading.characterSpacing = 4f;

            AddText(row.transform, "Body", rule.Body, 38f, new Color(0.93f, 0.94f, 0.96f), 1120f);
        }

        // A real spacer: text margins don't reliably count towards layout height.
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(panel, false);
        spacer.AddComponent<LayoutElement>().preferredHeight = 30f;

        TMP_Text controls = AddText(panel, "Controls", ControlsLine, 36f,
            new Color(1f, 1f, 1f, 0.75f), 1390f);
        controls.alignment = TextAlignmentOptions.Center;
        controls.margin = new Vector4(0f, 40f, 0f, 0f);
    }

    private static TMP_Text AddText(Transform parent, string objectName, string text,
        float size, Color color, float width)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;

        LayoutElement element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.minWidth = width;
        return tmp;
    }

    private void Update()
    {
        if (howToPlayPanel != null && howToPlayPanel.activeSelf &&
            UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleHowToPlay();
        }
    }

    // Each level button passes its scene name.
    public void LoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
