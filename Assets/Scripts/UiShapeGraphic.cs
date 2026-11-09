using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Small vector primitives used by the reference UI. No font glyphs or image
// downloads are needed for decorative marks, key caps, ratings, or controls.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UiShapeGraphic : MaskableGraphic
{
    public enum Shape { Rounded, Bevel, Title, Circle, Star, Spark, Play, Flag, Arrow, Clock, Backdrop, LessEqual }
    [SerializeField] private Shape shape;
    [SerializeField] private Color fill = Color.clear;
    [SerializeField] private Color stroke = Color.white;
    [SerializeField] private float lineWidth = 2.5f;
    [SerializeField] private float radius = 20f;
    [SerializeField] private float glow;

    public void Configure(Shape kind, Color inside, Color edge, float width = 2.5f,
        float corner = 20f, float halo = 0f)
    {
        shape = kind; fill = inside; stroke = edge; lineWidth = width;
        radius = corner; glow = halo; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float u = Mathf.Min(r.width, r.height);
        if (shape == Shape.Backdrop)
        {
            // Near-black, subtly mottled background, like the supplied menu.
            const int nx = 48, ny = 28;
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                float px = (x + .5f) / nx, py = (y + .5f) / ny;
                float vignette = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(px, py), new Vector2(.5f, .5f)));
                float v = .022f + .023f * vignette + .008f * Mathf.PerlinNoise(x * .6f, y * .6f);
                Vector2 a = new Vector2(Mathf.Lerp(r.xMin, r.xMax, x / (float)nx), Mathf.Lerp(r.yMin, r.yMax, y / (float)ny));
                Vector2 b = a + new Vector2(r.width / nx + .1f, r.height / ny + .1f);
                Fan(vh, new[] { a, new Vector2(b.x, a.y), b, new Vector2(a.x, b.y) }, new Color(v, v * 1.04f, v * 1.08f, 1));
            }
            return;
        }
        if (shape == Shape.Play)
        {
            Fan(vh, new[] { c + new Vector2(-u * .22f, u * .32f), c + new Vector2(u * .34f, 0), c + new Vector2(-u * .22f, -u * .32f) }, stroke);
            return;
        }
        if (shape == Shape.LessEqual)
        {
            Line(vh, c + new Vector2(.3f, .32f) * u, c + new Vector2(-.3f, .08f) * u, lineWidth, stroke);
            Line(vh, c + new Vector2(-.3f, .08f) * u, c + new Vector2(.3f, -.12f) * u, lineWidth, stroke);
            Line(vh, c + new Vector2(-.3f, -.34f) * u, c + new Vector2(.3f, -.34f) * u, lineWidth, stroke);
            return;
        }
        if (shape == Shape.Arrow)
        {
            Line(vh, c + Vector2.right * u * .38f, c - Vector2.right * u * .34f, lineWidth * 2, stroke);
            Line(vh, c - Vector2.right * u * .34f, c + new Vector2(-.02f, .28f) * u, lineWidth * 2, stroke);
            Line(vh, c - Vector2.right * u * .34f, c + new Vector2(-.02f, -.28f) * u, lineWidth * 2, stroke);
            return;
        }
        if (shape == Shape.Flag)
        {
            Line(vh, c + new Vector2(-.25f, -.4f) * u, c + new Vector2(-.25f, .4f) * u, lineWidth * 1.5f, stroke);
            Fan(vh, new[] { c + new Vector2(-.2f, .34f) * u, c + new Vector2(.34f, .29f) * u, c + new Vector2(.18f, .12f) * u, c + new Vector2(.34f, -.02f) * u, c + new Vector2(-.2f, .02f) * u }, stroke);
            return;
        }
        if (shape == Shape.Clock)
        {
            Loop(vh, Circle(c, u * .34f), stroke, true);
            Line(vh, c, c + Vector2.up * u * .25f, lineWidth, stroke);
            Line(vh, c, c + new Vector2(.15f, -.1f) * u, lineWidth, stroke);
            Line(vh, c + new Vector2(-.12f, .46f) * u, c + new Vector2(.12f, .46f) * u, lineWidth, stroke);
            return;
        }
        Vector2[] points;
        if (shape == Shape.Star || shape == Shape.Spark)
        {
            int n = shape == Shape.Star ? 10 : 8;
            points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float angle = Mathf.PI * .5f + i * 2f * Mathf.PI / n;
                float rr = u * .43f * (i % 2 == 0 ? 1 : shape == Shape.Star ? .46f : .3f);
                points[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rr;
            }
        }
        else if (shape == Shape.Circle) points = Circle(c, u * .46f);
        else if (shape == Shape.Bevel || shape == Shape.Title)
        {
            float k = Mathf.Min(radius, u * .48f);
            points = new[] { new Vector2(r.xMin + k, r.yMin), new Vector2(r.xMax - k, r.yMin), new Vector2(r.xMax, r.yMin + k), new Vector2(r.xMax, r.yMax - k), new Vector2(r.xMax - k, r.yMax), new Vector2(r.xMin + k, r.yMax), new Vector2(r.xMin, r.yMax - k), new Vector2(r.xMin, r.yMin + k) };
        }
        else
        {
            var p = new List<Vector2>();
            float k = Mathf.Min(radius, u * .5f - lineWidth);
            Vector2[] centers = { new Vector2(r.xMax - k, r.yMax - k), new Vector2(r.xMin + k, r.yMax - k), new Vector2(r.xMin + k, r.yMin + k), new Vector2(r.xMax - k, r.yMin + k) };
            for (int j = 0; j < 4; j++) for (int i = 0; i <= 12; i++)
            {
                float a = (j * 90f + i * 90f / 12f) * Mathf.Deg2Rad;
                p.Add(centers[j] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * k);
            }
            points = p.ToArray();
        }
        if (fill.a > 0) Fan(vh, points, fill * color);
        if (shape == Shape.Title)
        {
            // Open lower corners and a break for the centered diamond.
            for (int i = 2; i < 7; i++) Line(vh, points[i], points[i + 1], lineWidth, stroke);
            Line(vh, points[0], new Vector2(c.x - 35, r.yMin), lineWidth, stroke);
            Line(vh, new Vector2(c.x + 35, r.yMin), points[1], lineWidth, stroke);
            return;
        }
        if (lineWidth > 0 && stroke.a > 0) Loop(vh, points, stroke * color, true);
    }

    private void Loop(VertexHelper vh, Vector2[] p, Color tint, bool closed)
    {
        if (glow > 0)
        {
            for (int layer = 12; layer >= 1; layer--)
                Stroke(vh, p, lineWidth + glow * layer * .25f, NeonPanelGraphic.WithAlpha(tint, .025f * (1 - layer / 14f)), closed);
        }
        Stroke(vh, p, lineWidth, tint, closed);
    }
    internal static void Stroke(VertexHelper vh, Vector2[] p, float width, Color tint, bool closed)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = (p[i] - p[i == 0 ? (closed ? p.Length - 1 : 0) : i - 1]).normalized;
            Vector2 b = (p[i == p.Length - 1 ? (closed ? 0 : i) : i + 1] - p[i]).normalized;
            if (a == Vector2.zero) a = b; if (b == Vector2.zero) b = a;
            Vector2 normal = new Vector2(-a.y - b.y, a.x + b.x).normalized;
            float miter = Mathf.Max(.25f, Vector2.Dot(normal, new Vector2(-b.y, b.x)));
            Vector2 offset = normal * (width * .5f / miter);
            var v = UIVertex.simpleVert; v.color = tint;
            v.position = p[i] - offset; vh.AddVert(v); v.position = p[i] + offset; vh.AddVert(v);
        }
        for (int i = 0; i < p.Length - (closed ? 0 : 1); i++)
        {
            int a = start + i * 2, b = start + ((i + 1) % p.Length) * 2;
            vh.AddTriangle(a, a + 1, b + 1); vh.AddTriangle(a, b + 1, b);
        }
    }
    private static Vector2[] Circle(Vector2 c, float radius)
    {
        var p = new Vector2[80];
        for (int i = 0; i < p.Length; i++) { float a = i * Mathf.PI * 2 / p.Length; p[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius; }
        return p;
    }
    internal static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint) => NeonPanelGraphic.AddLine(vh, a, b, width, tint);
    internal static void Fan(VertexHelper vh, Vector2[] p, Color tint)
    {
        int start = vh.currentVertCount;
        var v = UIVertex.simpleVert; v.color = tint;
        Vector2 c = Vector2.zero; foreach (var point in p) c += point; c /= p.Length;
        v.position = c; vh.AddVert(v);
        foreach (var point in p) { v.position = point; vh.AddVert(v); }
        for (int i = 0; i < p.Length; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % p.Length);
    }
}
