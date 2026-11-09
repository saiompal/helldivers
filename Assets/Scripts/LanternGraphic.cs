using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class LanternGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 c = rect.center;
        float unit = Mathf.Min(rect.width, rect.height);
        float stroke = unit * 0.055f;
        Color glow = NeonPanelGraphic.WithAlpha(color, 0.12f);

        Vector2 left = c + new Vector2(-unit * 0.27f, -unit * 0.25f);
        Vector2 right = c + new Vector2(unit * 0.27f, -unit * 0.25f);
        Vector2 topLeft = c + new Vector2(-unit * 0.20f, unit * 0.22f);
        Vector2 topRight = c + new Vector2(unit * 0.20f, unit * 0.22f);
        AddFrame(vh, left, right, topLeft, topRight, stroke * 2.8f, glow);
        AddFrame(vh, left, right, topLeft, topRight, stroke, color);

        AddArc(vh, c + new Vector2(0f, unit * 0.24f), unit * 0.18f,
            0f, Mathf.PI, stroke * 2.5f, glow);
        AddArc(vh, c + new Vector2(0f, unit * 0.24f), unit * 0.18f,
            0f, Mathf.PI, stroke, color);
        NeonPanelGraphic.AddLine(vh,
            c + new Vector2(-unit * 0.31f, -unit * 0.30f),
            c + new Vector2(unit * 0.31f, -unit * 0.30f), stroke * 1.35f, color);

        Color flame = new Color(1f, 0.92f, 0.18f, color.a);
        AddDiamond(vh, c + new Vector2(0f, -unit * 0.03f),
            unit * 0.12f, unit * 0.18f, flame);
    }

    private static void AddFrame(VertexHelper vh, Vector2 left, Vector2 right,
        Vector2 topLeft, Vector2 topRight, float width, Color color)
    {
        NeonPanelGraphic.AddLine(vh, left, topLeft, width, color);
        NeonPanelGraphic.AddLine(vh, topLeft, topRight, width, color);
        NeonPanelGraphic.AddLine(vh, topRight, right, width, color);
        NeonPanelGraphic.AddLine(vh, right, left, width, color);
    }

    private static void AddArc(VertexHelper vh, Vector2 center, float radius,
        float from, float to, float width, Color color)
    {
        const int segments = 18;
        Vector2 previous = center + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = Mathf.Lerp(from, to, i / (float)segments);
            Vector2 current = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
            NeonPanelGraphic.AddLine(vh, previous, current, width, color);
            previous = current;
        }
    }

    private static void AddDiamond(VertexHelper vh, Vector2 center,
        float halfWidth, float halfHeight, Color color)
    {
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center + Vector2.up * halfHeight; vh.AddVert(vertex);
        vertex.position = center + Vector2.right * halfWidth; vh.AddVert(vertex);
        vertex.position = center - Vector2.up * halfHeight; vh.AddVert(vertex);
        vertex.position = center - Vector2.right * halfWidth; vh.AddVert(vertex);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
