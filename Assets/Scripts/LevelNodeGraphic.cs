using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class LevelNodeGraphic : MaskableGraphic
{
    [SerializeField] private Color fillColor = new Color(0.025f, 0.03f, 0.045f, 0.98f);
    [SerializeField] private Color ringColor = new Color(0.96f, 0.97f, 1f);

    public void SetState(Color fill, Color ring)
    {
        fillColor = fill;
        ringColor = ring;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.44f;
        AddDisc(vh, center, radius, fillColor);
        AddRing(vh, center, radius + 5f, 15f,
            NeonPanelGraphic.WithAlpha(ringColor, 0.08f));
        AddRing(vh, center, radius, 4f, ringColor);
    }

    private static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        const int segments = 64;
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vh.AddVert(vertex);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(vertex);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + ((i + 1) % segments));
    }

    private static void AddRing(VertexHelper vh, Vector2 center, float radius,
        float width, Color color)
    {
        const int segments = 64;
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 current = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            NeonPanelGraphic.AddLine(vh, previous, current, width, color);
            previous = current;
        }
    }
}
