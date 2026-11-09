using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared by the editor bakers and runtime. A rebuild now produces precisely
// the same UI as a scene that needs to repair its old hierarchy at startup.
public static class GameUi
{
    public static readonly Color White = new Color(.95f, .95f, .96f);
    public static readonly Color Gold = new Color(1f, .8f, .15f);
    public static readonly Color Dark = new Color(.035f, .038f, .043f, 1f);
    public static TMP_FontAsset Font => Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

    public static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = 5; go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }
    public static void Stretch(RectTransform r, float inset = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.one * inset; r.offsetMax = -Vector2.one * inset;
    }
    public static void ConfigureCanvas(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }
    public static UiShapeGraphic Shape(string name, Transform parent, UiShapeGraphic.Shape kind,
        Vector2 pos, Vector2 size, Color fill, Color stroke, float width = 2.5f, float corner = 20, float glow = 0)
    {
        var r = Rect(name, parent, pos, size);
        var g = r.gameObject.AddComponent<UiShapeGraphic>();
        g.Configure(kind, fill, stroke, width, corner, glow); g.raycastTarget = false; return g;
    }
    public static TMP_Text Text(string name, Transform parent, string value, float size,
        Vector2 pos, Vector2 dimensions, Color? tint = null)
    {
        var label = Rect(name, parent, pos, dimensions).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Font; label.text = value; label.fontSize = size; label.color = tint ?? White;
        label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false; return label;
    }
    public static Button Button(string name, Transform parent, string label, Vector2 pos,
        Vector2 size, bool primary = false, bool bevel = true)
    {
        var graphic = Shape(name, parent, bevel ? UiShapeGraphic.Shape.Bevel : UiShapeGraphic.Shape.Rounded,
            pos, size, primary ? White : Dark, White, 3, bevel ? size.y * .38f : 20);
        graphic.raycastTarget = true;
        var b = graphic.gameObject.AddComponent<Button>(); b.targetGraphic = graphic;
        var colors = b.colors;
        colors.highlightedColor = new Color(.8f, .82f, .86f);
        colors.pressedColor = new Color(.6f, .63f, .7f); colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.9f, .9f, .9f, 1f);
        b.colors = colors;
        var navigation = b.navigation; navigation.mode = Navigation.Mode.Automatic; b.navigation = navigation;
        if (!string.IsNullOrEmpty(label)) Text("Label", b.transform, label, 40, Vector2.zero, size, primary ? Dark : White);
        return b;
    }
    public static void Divider(Transform parent, float y, float width)
    {
        Shape("Divider Left", parent, UiShapeGraphic.Shape.Rounded, new Vector2(-width * .26f, y), new Vector2(width * .44f, 1.4f), White, Color.clear, 0, 0);
        Shape("Divider Right", parent, UiShapeGraphic.Shape.Rounded, new Vector2(width * .26f, y), new Vector2(width * .44f, 1.4f), White, Color.clear, 0, 0);
        Shape("Diamond", parent, UiShapeGraphic.Shape.Spark, new Vector2(0, y), new Vector2(18, 18), White, White, 0);
    }
    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var go = parent.GetChild(i).gameObject; go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }
    }
}
