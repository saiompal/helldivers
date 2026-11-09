using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class HourglassGraphic : MaskableGraphic
{
    public float Fill01 => fill01;
    public Color FrameColor => frameColor;
    [SerializeField, Range(0, 1)] private float fill01;
    [SerializeField] private Color frameColor = new Color(1f, .32f, .40f);
    [SerializeField] private Color sandColor = new Color(1f, .67f, .72f);

    public void SetState(float fill, float warningPulse)
    {
        fill01 = Mathf.Clamp01(fill);
        frameColor = Color.Lerp(new Color(1f, .32f, .40f), new Color(1f, .62f, .66f), warningPulse);
        sandColor = Color.Lerp(new Color(1f, .67f, .72f), new Color(1f, .86f, .88f), warningPulse);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 c = rect.center;
        float w = rect.width * .30f, h = rect.height * .37f;
        float line = Mathf.Min(rect.width, rect.height) * .024f;
        var left = HudMesh.Curve(c + new Vector2(-w, h), c + new Vector2(-w, h * .38f), c + new Vector2(-line, h * .19f), c + new Vector2(-line, 0));
        var right = HudMesh.Curve(c + new Vector2(w, h), c + new Vector2(w, h * .38f), c + new Vector2(line, h * .19f), c + new Vector2(line, 0));
        HudMesh.Stroke(vh, left, line, frameColor);
        HudMesh.Stroke(vh, right, line, frameColor);
        for (int i = 0; i < left.Length; i++) { left[i].y = 2 * c.y - left[i].y; right[i].y = 2 * c.y - right[i].y; }
        HudMesh.Stroke(vh, left, line, frameColor);
        HudMesh.Stroke(vh, right, line, frameColor);

        float cap = rect.height * .045f;
        HudMesh.Fill(vh, HudMesh.RoundRect(new Rect(c.x - w - line * 2, c.y + h, w * 2 + line * 4, cap), cap * .45f), frameColor);
        HudMesh.Fill(vh, HudMesh.RoundRect(new Rect(c.x - w - line * 2, c.y - h - cap, w * 2 + line * 4, cap), cap * .45f), frameColor);

        float innerWidth = w - line * 2.5f, innerHeight = h - line * 2;
        float remaining = Mathf.Sqrt(1 - fill01), filled = Mathf.Sqrt(fill01);
        HudMesh.Fill(vh, new[] { c + new Vector2(0, line), c + new Vector2(-innerWidth * remaining, innerHeight * remaining), c + new Vector2(innerWidth * remaining, innerHeight * remaining) }, sandColor);
        HudMesh.Fill(vh, new[] { c + new Vector2(0, -innerHeight + innerHeight * filled), c + new Vector2(-innerWidth * filled, -innerHeight), c + new Vector2(innerWidth * filled, -innerHeight) }, sandColor);
        if (fill01 > .005f && fill01 < .995f)
            HudMesh.Stroke(vh, new[] { c + Vector2.up * line, c - Vector2.up * innerHeight * (1 - filled) }, 1.5f, sandColor);
    }
}
