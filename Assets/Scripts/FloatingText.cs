using TMPro;
using UnityEngine;

// A short message that pops up in the world, drifts upward and fades.
// It ignores the 2D lights, so it's readable even in the dark.
public class FloatingText : MonoBehaviour
{
    private const float Duration = 1.1f;
    private const float RiseDistance = 1.4f;

    private TextMeshPro label;
    private Vector3 start;
    private float age;

    public static void Spawn(Vector3 position, string message)
    {
        FloatingText text = new GameObject("Floating Text").AddComponent<FloatingText>();
        text.transform.position = position;
        text.Build(message);
    }

    private void Build(string message)
    {
        start = transform.position;

        label = gameObject.AddComponent<TextMeshPro>();
        label.text = message;
        label.fontSize = 6f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.84f, 0.29f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(30, 20, 0, 255);
        label.GetComponent<MeshRenderer>().sortingOrder = 30;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);
        float easeOut = 1f - (1f - t) * (1f - t);

        transform.position = start + Vector3.up * (RiseDistance * easeOut);

        // Pops in slightly oversized, settles, then fades over the last half.
        float pop = 1f + 0.4f * Mathf.Max(0f, 1f - age / 0.18f);
        transform.localScale = Vector3.one * pop;
        label.alpha = Mathf.Clamp01((1f - t) * 2f);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
