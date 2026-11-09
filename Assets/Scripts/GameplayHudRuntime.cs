using TMPro;
using UnityEngine;

public static class GameplayHudRuntime
{
    public const string RootName = "Gameplay HUD v4";
    public static readonly Color LightBlue = new Color(.46f, .83f, 1f);
    private static readonly Color Panel = new Color(.032f, .042f, .058f, .98f);

    public static void ShowCompletion(TMP_Text status, string summary, string shortcuts)
    {
        var footer = GameUi.Shape("Completion Footer", status.canvas.rootCanvas.transform,
            UiShapeGraphic.Shape.Rounded, new Vector2(0, 80), new Vector2(1920, 160),
            new Color(.018f, .023f, .032f), Color.clear, 0, 0);
        var r = footer.rectTransform;
        r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.sizeDelta = new Vector2(0, 160);
        status.transform.SetParent(footer.transform, false);
        var sr = status.rectTransform;
        sr.anchorMin = new Vector2(0, .5f); sr.anchorMax = new Vector2(1, .5f);
        sr.anchoredPosition = new Vector2(0, 44); sr.sizeDelta = new Vector2(-48, 48);
        status.fontSize = 34; status.text = "LEVEL COMPLETE"; status.color = new Color(.36f, .90f, .54f);
        var detail = GameUi.Text("Summary", footer.transform, summary, 21, Vector2.zero, Vector2.zero);
        detail.rectTransform.anchorMin = new Vector2(0, .5f); detail.rectTransform.anchorMax = new Vector2(1, .5f);
        detail.rectTransform.sizeDelta = new Vector2(-48, 54); detail.rectTransform.anchoredPosition = Vector2.zero;
        detail.textWrappingMode = TextWrappingModes.Normal;
        detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 21;
        var actions = GameUi.Text("Shortcuts", footer.transform, shortcuts, 22, new Vector2(0, -48), Vector2.zero);
        actions.rectTransform.anchorMin = new Vector2(0, .5f); actions.rectTransform.anchorMax = new Vector2(1, .5f);
        actions.rectTransform.sizeDelta = new Vector2(-48, 36);
        actions.enableAutoSizing = true; actions.fontSizeMin = 14; actions.fontSizeMax = 22;
    }

    public static void Ensure(TMP_Text statusText, MapFlipController mapFlip, ref TMP_Text chargesText, ref TMP_Text starsText)
    {
        if (statusText == null || statusText.canvas == null) return;
        var root = statusText.canvas.rootCanvas.transform.Find(RootName);
        if (root == null) Build(statusText, mapFlip, out chargesText, out starsText);
        else
        {
            chargesText = root.Find("Light Charges/Count").GetComponent<TMP_Text>();
            starsText = root.Find("Stars/Count").GetComponent<TMP_Text>();
        }
    }

    public static void Build(TMP_Text status, MapFlipController flip, out TMP_Text charges, out TMP_Text stars)
    {
        Canvas canvas = status.canvas.rootCanvas;
        GameUi.ConfigureCanvas(canvas);
        status.transform.SetParent(canvas.transform, false);
        foreach (string name in new[] { "HUD Top Bar", "HUD Bottom Bar", "Gameplay HUD", "Gameplay HUD v2", "Gameplay HUD v3", RootName })
        {
            var old = canvas.transform.Find(name); if (old == null) continue;
            old.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(old.gameObject); else Object.DestroyImmediate(old.gameObject);
        }
        var sr = status.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(.5f, .5f);
        sr.anchoredPosition = Vector2.zero; sr.sizeDelta = new Vector2(1180, 340); sr.localScale = Vector3.one;
        status.fontSize = 37; status.color = GameUi.White; status.alignment = TextAlignmentOptions.Center;
        status.text = string.Empty; status.raycastTarget = false;
        var root = GameUi.Rect(RootName, canvas.transform, Vector2.zero, new Vector2(1920, 1080)); GameUi.Stretch(root);
        var bar = GameUi.Shape("Top Background", root, UiShapeGraphic.Shape.Rounded, new Vector2(0, -64), new Vector2(1920, 128), new Color(.018f, .023f, .032f), Color.clear, 0, 0);
        bar.rectTransform.anchorMin = new Vector2(0, 1); bar.rectTransform.anchorMax = new Vector2(1, 1);
        bar.rectTransform.sizeDelta = new Vector2(0, 128);

        var timer = GameUi.Rect("Flip Timer", root, new Vector2(110, -100), new Vector2(170, 190));
        timer.anchorMin = timer.anchorMax = new Vector2(0, 1);
        var hourglass = GameUi.Rect("Hourglass", timer, Vector2.zero, new Vector2(140, 160));
        var graphic = hourglass.gameObject.AddComponent<HourglassGraphic>(); graphic.raycastTarget = false;
        hourglass.gameObject.AddComponent<HourglassIndicator>().Configure(flip, graphic);

        var starPanel = Group("Stars", root, new Vector2(.5f, 1), new Vector2(0, -64), new Vector2(244, 92), GameUi.Gold);
        var star = GameUi.Rect("Star", starPanel, new Vector2(-77, 0), new Vector2(65, 65));
        var sg = star.gameObject.AddComponent<StarGraphic>(); sg.color = new Color(1f, .81f, .22f); sg.raycastTarget = false;
        Label("Label", starPanel, "STARS", new Vector2(34, 21), GameUi.Gold);
        stars = GameUi.Text("Count", starPanel, "0 / 2", 34, new Vector2(34, -13), new Vector2(130, 46));

        var light = Group("Light Charges", root, new Vector2(1, 1), new Vector2(-36, -64), new Vector2(320, 92), LightBlue);
        var torch = GameUi.Rect("Flashlight", light, new Vector2(-112, 0), new Vector2(76, 76));
        var fg = torch.gameObject.AddComponent<FlashlightGraphic>(); fg.color = LightBlue; fg.raycastTarget = false;
        Label("Label", light, "LIGHT", new Vector2(10, 21), LightBlue);
        for (int i = 0; i < 5; i++)
            GameUi.Shape("Pip " + i, light, UiShapeGraphic.Shape.Rounded, Vector2.zero, new Vector2(20, 27), Panel, new Color(.25f, .36f, .44f), 1.2f, 5);
        charges = GameUi.Text("Count", light, "", 22, new Vector2(113, -12), new Vector2(72, 35), LightBlue);
        charges.enableAutoSizing = true; charges.fontSizeMin = 12; charges.fontSizeMax = 22;
        UpdateCharges(charges, 0);
    }

    private static void Label(string name, Transform parent, string text, Vector2 position, Color tint)
    {
        var label = GameUi.Text(name, parent, text, 14, position, new Vector2(130, 22), tint);
        label.characterSpacing = 3;
    }

    private static RectTransform Group(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color accent)
    {
        var stroke = new Color(accent.r, accent.g, accent.b, .5f);
        var shape = GameUi.Shape(name, parent, UiShapeGraphic.Shape.Rounded, position, size, Panel, stroke, 1.5f, 18, 0);
        var r = shape.rectTransform; r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(anchor.x, .5f); return r;
    }

    public static void UpdateCharges(TMP_Text count, int value)
    {
        int visible = Mathf.Clamp(Mathf.Max(3, value), 3, 5);
        float width = 320 + (visible - 3) * 30;
        var root = count.transform.parent;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 92);
        float left = -width * .5f;
        root.Find("Flashlight").GetComponent<RectTransform>().anchoredPosition = new Vector2(left + 48, 0);
        root.Find("Label").GetComponent<RectTransform>().anchoredPosition = new Vector2(left + 170, 21);
        for (int i = 0; i < 5; i++)
        {
            var pip = root.Find("Pip " + i); pip.gameObject.SetActive(i < visible);
            var shape = pip.GetComponent<UiShapeGraphic>();
            shape.rectTransform.anchoredPosition = new Vector2(left + 130 + i * 30, -12);
            shape.Configure(UiShapeGraphic.Shape.Rounded, i < value ? LightBlue : new Color(.08f, .12f, .16f), i < value ? new Color(.7f, .93f, 1f) : new Color(.25f, .36f, .44f), 1.2f, 5, 0);
        }
        count.text = value > 5 ? "+" + (value - 5) : "";
        count.rectTransform.anchoredPosition = new Vector2(width * .5f - 47, -12);
    }
}
