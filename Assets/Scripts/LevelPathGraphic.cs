using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class LevelPathGraphic : MaskableGraphic
{
    public static readonly Vector2 StartAnchor = new Vector2(.09f, .725f);
    public static readonly Vector2 FinishAnchor = new Vector2(.92f, .245f);
    public static readonly Vector2[] NodeAnchors = { new Vector2(.34f, .745f), new Vector2(.76f, .66f), new Vector2(.32f, .485f), new Vector2(.735f, .42f), new Vector2(.50f, .27f) };
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Curve(vh, StartAnchor, new Vector2(.22f, .72f), new Vector2(.23f, .75f), NodeAnchors[0]);
        Curve(vh, NodeAnchors[0], new Vector2(.62f, .78f), new Vector2(.52f, .66f), NodeAnchors[1]);
        Curve(vh, NodeAnchors[1], new Vector2(1.10f, .68f), new Vector2(.98f, .48f), new Vector2(.66f, .54f));
        Curve(vh, new Vector2(.66f, .54f), new Vector2(.44f, .59f), new Vector2(.40f, .55f), NodeAnchors[2]);
        Curve(vh, NodeAnchors[2], new Vector2(.44f, .37f), new Vector2(.58f, .46f), NodeAnchors[3]);
        Curve(vh, NodeAnchors[3], new Vector2(1.0f, .43f), new Vector2(.93f, .30f), new Vector2(.72f, .30f));
        Curve(vh, new Vector2(.72f, .30f), new Vector2(.61f, .30f), new Vector2(.57f, .29f), NodeAnchors[4]);
        Curve(vh, NodeAnchors[4], new Vector2(.65f, .18f), new Vector2(.79f, .22f), FinishAnchor);
    }
    private void Curve(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Rect r = GetPixelAdjustedRect(); Vector2 previous = Point(r, a);
        for (int i = 1; i <= 60; i++)
        {
            float t = i / 60f, u = 1 - t;
            Vector2 p = Point(r, u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
            NeonPanelGraphic.AddLine(vh, previous, p, 5, color); previous = p;
        }
    }
    private static Vector2 Point(Rect r, Vector2 p) => new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height);
}
