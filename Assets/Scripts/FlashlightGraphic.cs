using UnityEngine;
using UnityEngine.UI;

// Handheld flashlight: grip, power switch, flared lens and three beam strokes.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FlashlightGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float unit = Mathf.Min(r.width, r.height);
        Vector2 center = r.center;
        Vector2 Point(float x, float y)
        {
            const float angle = 35f * Mathf.Deg2Rad;
            return center + new Vector2(x * Mathf.Cos(angle) - y * Mathf.Sin(angle), x * Mathf.Sin(angle) + y * Mathf.Cos(angle)) * unit;
        }
        Vector2[] body = { Point(-.39f, -.095f), Point(.02f, -.095f), Point(.02f, .095f), Point(-.39f, .095f) };
        HudMesh.Fill(vh, body, new Color(.07f, .20f, .29f));
        HudMesh.Stroke(vh, body, 2.2f, color, true);
        Vector2[] head = { Point(-.01f, -.105f), Point(.17f, -.205f), Point(.25f, -.205f), Point(.25f, .205f), Point(.17f, .205f), Point(-.01f, .105f) };
        HudMesh.Fill(vh, head, new Color(.10f, .32f, .45f));
        HudMesh.Stroke(vh, head, 2.2f, color, true);
        HudMesh.Stroke(vh, new[] { Point(.22f, -.17f), Point(.22f, .17f) }, 3f, new Color(.8f, .95f, 1));
        HudMesh.Stroke(vh, new[] { Point(-.27f, -.08f), Point(-.27f, .08f) }, 1.4f, color);
        HudMesh.Stroke(vh, new[] { Point(-.17f, .025f), Point(-.09f, .025f) }, 3f, new Color(.8f, .95f, 1));
        HudMesh.Stroke(vh, new[] { Point(.33f, 0), Point(.48f, 0) }, 2f, color);
        HudMesh.Stroke(vh, new[] { Point(.32f, .17f), Point(.44f, .24f) }, 2f, color);
        HudMesh.Stroke(vh, new[] { Point(.32f, -.17f), Point(.44f, -.24f) }, 2f, color);
    }
}
