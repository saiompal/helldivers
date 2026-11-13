using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public const string LayoutName = "Reference Menu v6";
    private Transform layout, home, map, help;
    private Coroutine helpAnimation;

    private void Awake()
    {
        layout = transform.Find(LayoutName);
        if (layout == null) RebuildUi();
        Bind();
    }
    private void Start() => ShowHome();

    // One source for editor-baked scenes and runtime repair of legacy scenes.
    public void RebuildUi()
    {
        GameUi.ConfigureCanvas(GetComponent<Canvas>());
        GameUi.Clear(transform);
        var bg = GameUi.Shape("Background", transform, UiShapeGraphic.Shape.Backdrop, Vector2.zero, new Vector2(1920, 1080), Color.black, Color.clear);
        GameUi.Stretch(bg.rectTransform);
        layout = GameUi.Rect(LayoutName, transform, Vector2.zero, new Vector2(1680, 1000));
        home = GameUi.Rect("Home", layout, Vector2.zero, new Vector2(1680, 1000));
        BuildHome(home);
        help = home.Find("How To Play");
        help.gameObject.SetActive(false);
        map = GameUi.Rect("Level Map", layout, Vector2.zero, new Vector2(1440, 1000));
        BuildMap(map);
        map.gameObject.SetActive(false);
    }

    private static void BuildHome(Transform root)
    {
        var frame = GameUi.Shape("Title Frame", root, UiShapeGraphic.Shape.Title, new Vector2(0, 245), new Vector2(1390, 175), Color.clear, GameUi.White, 3, 24);
        var title = GameUi.Text("Title", frame.transform, "IN THE DARK", 79, Vector2.zero, new Vector2(1300, 150));
        title.characterSpacing = 13;
        GameUi.Shape("Title Diamond", frame.transform, UiShapeGraphic.Shape.Spark, new Vector2(0, -87.5f), new Vector2(36, 36), GameUi.White, GameUi.White, 0);
        GameUi.Button("Start", root, "Start", Vector2.zero, new Vector2(610, 122), true);
        GameUi.Button("Tutorial", root, "Tutorial", new Vector2(0, -150), new Vector2(610, 122));
        GameUi.Button("Help Button", root, "How to Play", new Vector2(0, -300), new Vector2(610, 122));
        var help = GameUi.Shape("How To Play", root, UiShapeGraphic.Shape.Rounded, new Vector2(0, -110), new Vector2(615, 460), GameUi.Dark, GameUi.White, 2.4f, 28);
        help.raycastTarget = true;
        help.gameObject.AddComponent<CanvasGroup>();
        var close = GameUi.Button("Close", help.transform, "X", new Vector2(265, 191), new Vector2(42, 40), false, false);
        close.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 20;
        GameUi.Text("Heading", help.transform, "How to Play", 37, new Vector2(0, 170), new Vector2(540, 60));
        GameUi.Divider(help.transform, 125, 550);
        Key(help.transform, "A", new Vector2(-220, 48), 62);
        GameUi.Text("Plus", help.transform, "+", 25, new Vector2(-171, 48), new Vector2(30, 45));
        Key(help.transform, "D", new Vector2(-121, 48), 62);
        HelpText(help.transform, "Move Left / Right", 48);
        Key(help.transform, "Space", new Vector2(-170, -33), 164); HelpText(help.transform, "Jump", -33);
        Key(help.transform, "F", new Vector2(-170, -114), 62); HelpText(help.transform, "Use Light", -114);
        Key(help.transform, "R", new Vector2(-170, -195), 62); HelpText(help.transform, "Restart", -195);
        var quit = GameUi.Button("Quit", root, "Quit", new Vector2(0, -445), new Vector2(220, 64), false, false);
        quit.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 26;
    }
    private static void Key(Transform root, string text, Vector2 position, float width)
    {
        var key = GameUi.Shape(text + " Key", root, UiShapeGraphic.Shape.Rounded, position, new Vector2(width, 54), Color.clear, GameUi.White, 2.4f, 8);
        GameUi.Text("Label", key.transform, text, 29, Vector2.zero, new Vector2(width, 54));
    }
    private static void HelpText(Transform root, string text, float y)
    {
        var label = GameUi.Text(text, root, text, 25, new Vector2(120, y), new Vector2(265, 54));
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }
    private static void BuildMap(Transform root)
    {
        var header = GameUi.Shape("Header", root, UiShapeGraphic.Shape.Bevel, new Vector2(0, 430), new Vector2(730, 93), Color.clear, GameUi.White, 2.6f, 34);
        GameUi.Text("Title", header.transform, "Select Level", 50, Vector2.zero, new Vector2(600, 90));
        foreach (float x in new[] { -296f, 296f }) GameUi.Shape("Diamond", header.transform, UiShapeGraphic.Shape.Spark, new Vector2(x, 0), new Vector2(25, 25), GameUi.White, GameUi.White, 0);
        var route = GameUi.Rect("Route", root, Vector2.zero, new Vector2(1440, 1000));
        var path = route.gameObject.AddComponent<LevelPathGraphic>(); path.color = GameUi.White; path.raycastTarget = false;
        Endpoint(root, "Start Route", LevelPathGraphic.StartAnchor, UiShapeGraphic.Shape.Play);
        Endpoint(root, "Finish Route", LevelPathGraphic.FinishAnchor, UiShapeGraphic.Shape.Flag);
        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            var node = GameUi.Button("Level " + i, root, "", Vector2.zero, new Vector2(166, 166), false, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = LevelPathGraphic.NodeAnchors[i];
            (node.targetGraphic as UiShapeGraphic).Configure(UiShapeGraphic.Shape.Circle, GameUi.Dark, GameUi.White, 4);
            GameUi.Text("Number", node.transform, (i + 1).ToString(), 62, new Vector2(0, 22), new Vector2(120, 80));
            GameUi.Shape("Completion Star", node.transform, UiShapeGraphic.Shape.Star,
                new Vector2(0, -39), new Vector2(35, 35), Color.clear, GameUi.White, 2);
        }
        var back = GameUi.Button("Back", root, "Back", new Vector2(-558, -460), new Vector2(250, 70), false, false);
        back.transform.Find("Label").GetComponent<RectTransform>().anchoredPosition = new Vector2(22, 0);
        back.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 32;
        GameUi.Shape("Arrow", back.transform, UiShapeGraphic.Shape.Arrow, new Vector2(-78, 0), new Vector2(38, 38), Color.clear, GameUi.White, 3);
    }
    private static void Endpoint(Transform root, string name, Vector2 anchor, UiShapeGraphic.Shape symbol)
    {
        var p = GameUi.Shape(name, root, UiShapeGraphic.Shape.Rounded, Vector2.zero, new Vector2(104, 94), GameUi.Dark, GameUi.White, 4, 22);
        p.rectTransform.anchorMin = p.rectTransform.anchorMax = anchor;
        GameUi.Shape("Icon", p.transform, symbol, Vector2.zero, new Vector2(55, 55), Color.clear, GameUi.White, 3);
    }
    private void Bind()
    {
        layout = transform.Find(LayoutName); home = layout.Find("Home"); map = layout.Find("Level Map");
        help = home.Find("How To Play");
        home.Find("Start").GetComponent<Button>().onClick.AddListener(ShowLevelMap);
        home.Find("Tutorial").GetComponent<Button>().onClick.AddListener(LoadTutorial);
        home.Find("Help Button").GetComponent<Button>().onClick.AddListener(ShowHelp);
        help.Find("Close").GetComponent<Button>().onClick.AddListener(HideHelp);
        home.Find("Quit").GetComponent<Button>().onClick.AddListener(Quit);
        home.Find("Quit").gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
        map.Find("Back").GetComponent<Button>().onClick.AddListener(ShowHome);
        for (int i = 0; i < LevelCatalog.Count; i++) { int index = i; map.Find("Level " + i).GetComponent<Button>().onClick.AddListener(() => LoadLevel(index)); }
    }
    public void ShowHome() { home.gameObject.SetActive(true); map.gameObject.SetActive(false); HideHelp(); ClearSelection(); }
    public void ShowLevelMap() { HideHelp(); RefreshLevelMap(); home.gameObject.SetActive(false); map.gameObject.SetActive(true); ClearSelection(); }
    public void ShowHelp()
    {
        help.gameObject.SetActive(true);
        if (helpAnimation != null) StopCoroutine(helpAnimation);
        if (Application.isPlaying) helpAnimation = StartCoroutine(OpenHelp());
        else { help.GetComponent<CanvasGroup>().alpha = 1f; help.localScale = Vector3.one; }
        ClearSelection();
    }
    public void HideHelp()
    {
        if (helpAnimation != null) { StopCoroutine(helpAnimation); helpAnimation = null; }
        if (help != null) help.gameObject.SetActive(false);
    }
    private IEnumerator OpenHelp()
    {
        var group = help.GetComponent<CanvasGroup>();
        for (float t = 0; t < .18f; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0, 1, t / .18f);
            group.alpha = progress;
            help.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, progress);
            yield return null;
        }
        group.alpha = 1; help.localScale = Vector3.one; helpAnimation = null;
    }
    public void LoadTutorial() => SceneManager.LoadScene("Tutorial");
    public void LoadLevel(int index) { if (index >= 0 && index < LevelCatalog.Count && PlayerProgress.IsLevelUnlocked(index)) SceneManager.LoadScene(LevelCatalog.GetSceneName(index)); }
    private void RefreshLevelMap()
    {
        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            var node = map.Find("Level " + i); bool unlocked = PlayerProgress.IsLevelUnlocked(i);
            node.GetComponent<Button>().interactable = unlocked;
            Color tint = unlocked ? GameUi.White : new Color(.68f, .69f, .71f);
            node.GetComponent<UiShapeGraphic>().Configure(UiShapeGraphic.Shape.Circle, GameUi.Dark, tint, 4);
            node.Find("Number").GetComponent<TMP_Text>().color = tint;
            bool completed = PlayerProgress.IsLevelCompleted(i);
            node.Find("Completion Star").GetComponent<UiShapeGraphic>().Configure(UiShapeGraphic.Shape.Star,
                completed ? GameUi.Gold : Color.clear, completed ? GameUi.Gold : tint, 2);
        }
    }
    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (help != null && help.gameObject.activeSelf) HideHelp();
        else if (map != null && map.gameObject.activeSelf) ShowHome();
    }
    private static void ClearSelection() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
}
