using UnityEngine;
using UnityEngine.UI;

// Crisp UI geometry with a one-pixel alpha fringe instead of stacked glow.
internal static class HudMesh
{
    public static void Fill(VertexHelper vh, Vector2[] points, Color tint)
    {
        UiShapeGraphic.Fan(vh, points, tint);
        Stroke(vh, points, .6f, tint, true);
    }

    public static void Stroke(VertexHelper vh, Vector2[] points, float width, Color tint, bool closed = false)
    {
        int start = vh.currentVertCount;
        const float feather = .8f;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 before = (points[i] - points[i == 0 ? (closed ? points.Length - 1 : 0) : i - 1]).normalized;
            Vector2 after = (points[i == points.Length - 1 ? (closed ? 0 : i) : i + 1] - points[i]).normalized;
            if (before == Vector2.zero) before = after;
            if (after == Vector2.zero) after = before;
            Vector2 normal = new Vector2(-before.y - after.y, before.x + after.x).normalized;
            float miter = Mathf.Max(.35f, Vector2.Dot(normal, new Vector2(-after.y, after.x)));
            for (int band = 0; band < 4; band++)
            {
                float distance = band == 0 ? width * .5f + feather : band == 1 ? width * .5f : band == 2 ? -width * .5f : -width * .5f - feather;
                var vertex = UIVertex.simpleVert;
                vertex.position = points[i] + normal * (distance / miter);
                vertex.color = band == 0 || band == 3 ? new Color(tint.r, tint.g, tint.b, 0) : tint;
                vh.AddVert(vertex);
            }
        }
        for (int i = 0; i < points.Length - (closed ? 0 : 1); i++)
            for (int band = 0; band < 3; band++)
            {
                int a = start + i * 4 + band;
                int b = start + ((i + 1) % points.Length) * 4 + band;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
    }

    public static Vector2[] RoundRect(Rect rect, float radius)
    {
        var points = new Vector2[36];
        Vector2[] centers = { new Vector2(rect.xMax - radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMin + radius), new Vector2(rect.xMax - radius, rect.yMin + radius) };
        for (int corner = 0; corner < 4; corner++)
            for (int step = 0; step < 9; step++)
            {
                float angle = (corner * 90f + step * 90f / 8f) * Mathf.Deg2Rad;
                points[corner * 9 + step] = centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        return points;
    }

    public static Vector2[] Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var points = new Vector2[33];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1), u = 1f - t;
            points[i] = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }
        return points;
    }
}
