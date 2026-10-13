using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the in-game HUD for whichever level is open: a status banner at
// the top, and a bottom bar with the controls, the star count and a legend
// of the shapes in the maze. Every level gets the same controls; the
// legend lists only the objects that level has.
// Level builders call it; run it by hand from the menu: Tools > Build HUD.
public static class HudBuilder
{
    // The canvas scales with screen height, so these are pixels at 1080p.
    internal const float ReferenceWidth = 1920f;
    internal const float ReferenceHeight = 1080f;
    internal const float TopBarHeight = 80f;
    internal const float BottomBarHeight = 130f;

    private const float ScreenMargin = 0.3f;
    private const float TargetAspect = 16f / 9f;

    private const string TopBarName = "HUD Top Bar";
    private const string BottomBarName = "HUD Bottom Bar";

    private static readonly Color BarColor = new Color(0.06f, 0.07f, 0.1f, 0.92f);
    private static readonly Color BarEdgeColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color HeaderColor = new Color(1f, 1f, 1f, 0.45f);
    private static readonly Color LabelColor = new Color(0.95f, 0.95f, 0.97f);
    private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color KeyCapColor = new Color(0.92f, 0.93f, 0.95f);
    private static readonly Color KeyLabelColor = new Color(0.1f, 0.1f, 0.13f);

    private static TMP_FontAsset font;
    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    [MenuItem("Tools/Build HUD")]
    private static void Build()
    {
        if (BuildHud())
        {
            Debug.Log("HudBuilder: HUD built. Save the scene to keep it.");
        }
    }

    internal static bool BuildHud()
    {
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        Camera camera = Camera.main;

        if (gameManager == null || camera == null)
        {
            Debug.LogError("HudBuilder: open a level scene first.");
            return false;
        }

        SerializedObject managerData = new SerializedObject(gameManager);
        TMP_Text statusText =
            managerData.FindProperty("statusText").objectReferenceValue as TMP_Text;

        if (statusText == null || statusText.canvas == null)
        {
            Debug.LogError("HudBuilder: GameManager needs its Status Text assigned.");
            return false;
        }

        font = statusText.font;
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        Canvas canvas = statusText.canvas.rootCanvas;

        Undo.SetCurrentGroupName("Build HUD");
        int undoGroup = Undo.GetCurrentGroup();

        ConfigureCanvasScaler(canvas);
        RemoveOldBars(canvas.transform, statusText);

        RectTransform topBar = BuildTopBar(canvas.transform, statusText);
        RectTransform bottomBar = BuildBottomBar(
            canvas.transform, out TMP_Text chargesText, out TMP_Text starsText);
        Undo.RegisterCreatedObjectUndo(bottomBar.gameObject, "Create HUD Bottom Bar");

        managerData.FindProperty("chargesText").objectReferenceValue = chargesText;
        managerData.FindProperty("starsText").objectReferenceValue = starsText;
        managerData.ApplyModifiedProperties();

        FrameCamera(camera);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = topBar.gameObject;
        return true;
    }

    private static void ConfigureCanvasScaler(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        }

        Undo.RecordObject(scaler, "Configure Canvas Scaler");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
    }

    private static void RemoveOldBars(Transform canvas, TMP_Text statusText)
    {
        Transform oldTop = canvas.Find(TopBarName);
        Transform oldBottom = canvas.Find(BottomBarName);

        // The status text lives inside the top bar, so rescue it first.
        if (oldTop != null && statusText.transform.IsChildOf(oldTop))
        {
            Undo.SetTransformParent(statusText.transform, canvas, "Move Status Text");
        }

        if (oldTop != null)
        {
            Undo.DestroyObjectImmediate(oldTop.gameObject);
        }

        if (oldBottom != null)
        {
            Undo.DestroyObjectImmediate(oldBottom.gameObject);
        }
    }

    private static RectTransform BuildTopBar(Transform canvas, TMP_Text statusText)
    {
        RectTransform bar = CreateBar(TopBarName, canvas, top: true);
        Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create HUD Top Bar");

        Undo.SetTransformParent(statusText.transform, bar, "Move Status Text");
        Undo.RecordObject(statusText, "Style Status Text");
        Undo.RecordObject(statusText.rectTransform, "Style Status Text");

        RectTransform textRect = statusText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(40f, 0f);
        textRect.offsetMax = new Vector2(-40f, 0f);
        textRect.localScale = Vector3.one;

        statusText.fontSize = 34f;
        statusText.color = LabelColor;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.richText = true;
        return bar;
    }

    private static RectTransform BuildBottomBar(
        Transform canvas, out TMP_Text chargesText, out TMP_Text starsText)
    {
        RectTransform bar = CreateBar(BottomBarName, canvas, top: false);

        HorizontalLayoutGroup layout = AddRow(bar, 56f);
        layout.padding = new RectOffset(56, 56, 14, 14);

        // Controls
        RectTransform controls = CreateSection(bar, "CONTROLS");
        RectTransform controlRow = CreateUi("Items", controls);
        AddRow(controlRow, 40f);

        RectTransform move = CreateUi("Move", controlRow);
        AddRow(move, 14f);
        RectTransform moveKeys = CreateUi("AD", move);
        AddRow(moveKeys, 4f);
        CreateKeyCap(moveKeys, "A", 40f, 40f);
        CreateKeyCap(moveKeys, "D", 40f, 40f);
        BuildLabelColumn(move, "Move", "or ← →");

        RectTransform jump = CreateUi("Jump", controlRow);
        AddRow(jump, 14f);
        CreateKeyCap(jump, "SPACE", 112f, 40f);
        BuildLabelColumn(jump, "Jump", "or W / ↑");

        RectTransform reveal = CreateUi("Light", controlRow);
        AddRow(reveal, 14f);
        CreateKeyCap(reveal, "F", 40f, 40f);
        chargesText = BuildLabelColumn(reveal, "Light", "●●●");
        chargesText.fontSize = 22f;
        chargesText.characterSpacing = 12f;
        chargesText.color = Color.white;

        RectTransform restart = CreateUi("Restart", controlRow);
        AddRow(restart, 14f);
        CreateKeyCap(restart, "R", 40f, 40f);
        BuildLabelColumn(restart, "Restart", null);

        RectTransform menu = CreateUi("Menu", controlRow);
        AddRow(menu, 14f);
        CreateKeyCap(menu, "ESC", 56f, 40f);
        BuildLabelColumn(menu, "Menu", null);

        // Pushes the stars and legend to the right edge.
        RectTransform spacer = CreateUi("Spacer", bar);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        SpriteRenderer player = FindRenderer("Player");
        SpriteRenderer exit = FindRenderer<ExitTrigger>();
        SpriteRenderer star = FindRenderer<StarCollectible>();
        SpriteRenderer portal = FindRenderer("Portals");
        SpriteRenderer spike = FindRenderer<Hazard>();

        // Stars
        starsText = null;
        if (star != null)
        {
            RectTransform stars = CreateSection(bar, "STARS");
            RectTransform starRow = CreateUi("Items", stars);
            AddRow(starRow, 14f);
            CreateIcon(starRow, star.sprite, star.color, 30f, 0f);
            starsText = AddText(CreateUi("Count", starRow), "0 / 0", 28f, LabelColor);
            starsText.fontStyle = FontStyles.Bold;
        }

        // Legend
        RectTransform legend = CreateSection(bar, "LEGEND");
        RectTransform grid = CreateUi("Items", legend);
        GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(190f, 34f);
        gridLayout.spacing = new Vector2(20f, 8f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.childAlignment = TextAnchor.MiddleLeft;

        Sprite square = exit != null ? exit.sprite : null;

        CreateLegendItem(grid, "You",
            player != null ? player.sprite : circleSprite,
            player != null ? player.color : Color.cyan, 28f, 0f);
        CreateLegendItem(grid, "Exit",
            square, exit != null ? exit.color : Color.green, 24f, 0f);
        int items = 2;

        if (star != null)
        {
            CreateLegendItem(grid, "Star", star.sprite, star.color, 28f, 0f);
            items++;
        }

        if (spike != null)
        {
            CreateLegendItem(grid, "Spike trap", square, spike.color, 20f, 0f);
            items++;
        }

        if (portal != null)
        {
            CreateLegendItem(grid, "Portal", portal.sprite, portal.color, 28f, 0f);
            items++;
        }

        // Two rows fit in the bar, so add a column when there are more items.
        gridLayout.constraintCount = items <= 4 ? 2 : 3;

        return bar;
    }

    private static RectTransform CreateBar(string name, Transform canvas, bool top)
    {
        RectTransform bar = CreateUi(name, canvas);
        float edgeY = top ? 1f : 0f;
        bar.anchorMin = new Vector2(0f, edgeY);
        bar.anchorMax = new Vector2(1f, edgeY);
        bar.pivot = new Vector2(0.5f, edgeY);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0f, top ? TopBarHeight : BottomBarHeight);
        AddImage(bar, null, BarColor);

        // A thin line on the edge facing the maze.
        RectTransform edge = CreateUi("Edge", bar);
        edge.anchorMin = new Vector2(0f, 1f - edgeY);
        edge.anchorMax = new Vector2(1f, 1f - edgeY);
        edge.pivot = new Vector2(0.5f, 1f - edgeY);
        edge.sizeDelta = new Vector2(0f, 2f);
        AddImage(edge, null, BarEdgeColor);
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        return bar;
    }

    private static RectTransform CreateSection(Transform parent, string title)
    {
        RectTransform section = CreateUi(title, parent);
        VerticalLayoutGroup layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI header = AddText(CreateUi("Header", section), title, 16f, HeaderColor);
        header.fontStyle = FontStyles.Bold;
        header.characterSpacing = 8f;
        return section;
    }

    private static TextMeshProUGUI BuildLabelColumn(Transform parent, string label, string hint)
    {
        RectTransform column = CreateUi("Label", parent);
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        AddText(CreateUi("Text", column), label, 24f, LabelColor);

        if (hint == null)
        {
            return null;
        }

        return AddText(CreateUi("Hint", column), hint, 18f, HintColor);
    }

    private static void CreateKeyCap(Transform parent, string key, float width, float height)
    {
        RectTransform cap = CreateUi($"Key {key}", parent);
        Image image = AddImage(cap, roundedSprite, KeyCapColor);
        image.type = Image.Type.Sliced;

        // A darker lower edge makes it read as a physical key.
        Shadow shadow = cap.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -3f);

        LayoutElement size = cap.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.preferredHeight = height;

        RectTransform labelRect = CreateUi("Label", cap);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = AddText(labelRect, key, height * 0.5f, KeyLabelColor);
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateLegendItem(
        Transform parent, string label, Sprite sprite, Color color,
        float iconSize, float rotation)
    {
        RectTransform item = CreateUi(label, parent);
        AddRow(item, 14f);
        CreateIcon(item, sprite, color, iconSize, rotation);
        AddText(CreateUi("Text", item), label, 22f, LabelColor);
    }

    private static void CreateIcon(
        Transform parent, Sprite sprite, Color color, float iconSize, float rotation)
    {
        // A fixed-size slot keeps the labels lined up whatever the icon shape.
        RectTransform slot = CreateUi("Icon", parent);
        LayoutElement slotSize = slot.gameObject.AddComponent<LayoutElement>();
        slotSize.preferredWidth = 30f;
        slotSize.preferredHeight = 30f;

        RectTransform icon = CreateUi("Shape", slot);
        icon.sizeDelta = new Vector2(iconSize, iconSize);
        icon.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image image = AddImage(icon, sprite, color);
        image.preserveAspect = true;
    }

    // Looks in children too: the portal sprites sit under a Portals parent.
    private static SpriteRenderer FindRenderer(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponentInChildren<SpriteRenderer>(true) : null;
    }

    // By component rather than name, since legend items share those names.
    private static SpriteRenderer FindRenderer<T>() where T : Component
    {
        T found = Object.FindAnyObjectByType<T>();
        return found != null ? found.GetComponent<SpriteRenderer>() : null;
    }

    // Fits the maze into the screen area between the HUD bars. The HUD
    // scales with screen height, so the bars are a fixed share of it.
    // The floor covers the whole maze, so its bounds give the maze's size.
    internal static void FrameCamera(Camera camera)
    {
        GameObject floor = GameObject.Find("Floor");
        SpriteRenderer floorRenderer = floor != null ? floor.GetComponent<SpriteRenderer>() : null;

        if (floorRenderer == null)
        {
            Debug.LogWarning("HudBuilder: no Floor sprite found, so the camera wasn't framed.");
            return;
        }

        Bounds maze = floorRenderer.bounds;
        float topShare = TopBarHeight / ReferenceHeight;
        float bottomShare = BottomBarHeight / ReferenceHeight;

        float sizeForHeight =
            (maze.extents.y + ScreenMargin) / (1f - topShare - bottomShare);
        float sizeForWidth = (maze.extents.x + ScreenMargin) / TargetAspect;
        float size = Mathf.Max(sizeForHeight, sizeForWidth);

        Undo.RecordObject(camera, "Frame Camera");
        Undo.RecordObject(camera.transform, "Frame Camera");
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.orthographicSize = size;

        // Shift the camera so the maze is centred in the space between the bars.
        float offsetY = size * (topShare - bottomShare);
        camera.transform.position = new Vector3(
            maze.center.x, maze.center.y + offsetY, camera.transform.position.z
        );
    }

    private static RectTransform CreateUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static HorizontalLayoutGroup AddRow(RectTransform rect, float spacing)
    {
        HorizontalLayoutGroup row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = spacing;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return row;
    }

    private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI AddText(RectTransform rect, string text, float size, Color color)
    {
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        return tmp;
    }
}
