using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class StarGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 c = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .41f;
        var points = new Vector2[10];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.PI * .5f + i * Mathf.PI * 2 / points.Length;
            points[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1f : .46f);
        }
        var v = UIVertex.simpleVert;
        v.position = c; v.color = Color.Lerp(color, Color.white, .28f); vh.AddVert(v);
        for (int i = 0; i < points.Length; i++)
        {
            v.position = points[i];
            v.color = i % 2 == 0 ? color : Color.Lerp(color, new Color(.85f, .5f, .05f), .22f);
            vh.AddVert(v);
        }
        for (int i = 0; i < points.Length; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % points.Length);
        HudMesh.Stroke(vh, points, 1f, new Color(1f, .88f, .48f), true);
    }
}
