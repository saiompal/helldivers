using UnityEngine;
using UnityEngine.Rendering.Universal;

// A soft light that pulses slowly so an object catches the player's eye
// in the dark.
public class PulsingGlow : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField, Min(0.1f)] private float glowRadius = 1.4f;
    [SerializeField, Min(0f)] private float minGlow = 0.35f;
    [SerializeField, Min(0f)] private float maxGlow = 0.8f;
    [SerializeField, Min(0.1f)] private float pulseSpeed = 3f;

    private Light2D glow;

    public Color GlowColor
    {
        get => glowColor;
        set
        {
            glowColor = value;
            if (glow != null)
            {
                glow.color = value;
            }
        }
    }

    // Called by the level builder.
    public void Configure(Color color, float radius, float min, float max)
    {
        glowColor = color;
        glowRadius = radius;
        minGlow = min;
        maxGlow = max;
    }

    private void Awake()
    {
        GameObject glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(transform, false);
        glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = glowColor;
        glow.pointLightInnerRadius = 0f;
        glow.pointLightOuterRadius = glowRadius;

        // Cancel the object's scale so the radius is in world units.
        Vector3 scale = transform.lossyScale;
        glowObject.transform.localScale = new Vector3(
            1f / Mathf.Max(scale.x, 0.01f), 1f / Mathf.Max(scale.y, 0.01f), 1f
        );
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        glow.intensity = Mathf.Lerp(minGlow, maxGlow, t);
    }
}
