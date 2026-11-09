using UnityEngine;
using UnityEngine.UI;

// Crisp clipped-corner panel with a soft, texture-free neon edge.
[RequireComponent(typeof(CanvasRenderer))]
public class NeonPanelGraphic : MaskableGraphic
{
    [SerializeField] private Color fillColor = new Color(0.018f, 0.022f, 0.032f, 0.94f);
    [SerializeField] private Color lineColor = new Color(0.96f, 0.97f, 1f);
    [SerializeField, Min(0f)] private float cornerCut = 18f;
    [SerializeField, Min(1f)] private float strokeWidth = 3f;
    [SerializeField, Min(0f)] private float glowWidth = 10f;

    public void Configure(Color fill, Color line, float cut = 18f,
        float stroke = 3f, float glow = 10f)
    {
        fillColor = fill;
        lineColor = line;
        cornerCut = cut;
        strokeWidth = stroke;
        glowWidth = glow;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * 0.45f);
        Vector2[] points =
        {
            new Vector2(rect.xMin + cut, rect.yMin),
            new Vector2(rect.xMax - cut, rect.yMin),
            new Vector2(rect.xMax, rect.yMin + cut),
            new Vector2(rect.xMax, rect.yMax - cut),
            new Vector2(rect.xMax - cut, rect.yMax),
            new Vector2(rect.xMin + cut, rect.yMax),
            new Vector2(rect.xMin, rect.yMax - cut),
            new Vector2(rect.xMin, rect.yMin + cut),
        };

        Color tint = color;
        AddFan(vh, points, Multiply(fillColor, tint));
        if (glowWidth > 0f)
        {
            AddLoop(vh, points, strokeWidth + glowWidth,
                WithAlpha(Multiply(lineColor, tint), 0.07f));
            AddLoop(vh, points, strokeWidth + glowWidth * 0.45f,
                WithAlpha(Multiply(lineColor, tint), 0.18f));
        }
        AddLoop(vh, points, strokeWidth, Multiply(lineColor, tint));
    }

    private static void AddFan(VertexHelper vh, Vector2[] points, Color color)
    {
        int centerIndex = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        Vector2 center = Vector2.zero;
        foreach (Vector2 point in points)
            center += point;
        center /= points.Length;
        vertex.position = center;
        vh.AddVert(vertex);
        foreach (Vector2 point in points)
        {
            vertex.position = point;
            vh.AddVert(vertex);
        }
        for (int i = 0; i < points.Length; i++)
            vh.AddTriangle(centerIndex, centerIndex + 1 + i,
                centerIndex + 1 + ((i + 1) % points.Length));
    }

    private static void AddLoop(VertexHelper vh, Vector2[] points, float width, Color color)
    {
        for (int i = 0; i < points.Length; i++)
            AddLine(vh, points[i], points[(i + 1) % points.Length], width, color);
    }

    internal static void AddLine(VertexHelper vh, Vector2 from, Vector2 to,
        float width, Color color)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = from - normal; vh.AddVert(vertex);
        vertex.position = from + normal; vh.AddVert(vertex);
        vertex.position = to + normal; vh.AddVert(vertex);
        vertex.position = to - normal; vh.AddVert(vertex);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    internal static Color WithAlpha(Color value, float multiplier)
    {
        value.a *= multiplier;
        return value;
    }

    private static Color Multiply(Color a, Color b) => new Color(
        a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
}
