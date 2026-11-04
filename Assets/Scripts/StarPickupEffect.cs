using UnityEngine;
using UnityEngine.Rendering.Universal;

// A short burst where a star was collected: a warm flash of light that
// swells and fades, a ring that expands, and little stars that fly out.
// It's the visual for the +1 light charge, and the flash is a real light,
// so it shows up even in the dark. Destroys itself when done.
public class StarPickupEffect : MonoBehaviour
{
    private const float Duration = 0.75f;
    private const int SparkCount = 10;
    private const int RingTextureSize = 128;

    private static readonly Color GlowColor = new Color(1f, 0.82f, 0.3f);
    private static Sprite ringSprite;

    private Light2D flash;
    private SpriteRenderer ring;
    private SpriteRenderer[] sparks;
    private Vector2[] velocities;
    private float[] spins;
    private float age;

    public static void Play(Vector3 position, Sprite starSprite, Material material)
    {
        StarPickupEffect effect = new GameObject("Star Pickup Effect").AddComponent<StarPickupEffect>();
        effect.transform.position = position;
        effect.Build(starSprite, material);
    }

    private void Build(Sprite starSprite, Material material)
    {
        flash = gameObject.AddComponent<Light2D>();
        flash.lightType = Light2D.LightType.Point;
        flash.color = GlowColor;
        flash.intensity = 2f;
        flash.pointLightInnerRadius = 0f;
        flash.pointLightOuterRadius = 1f;

        ring = CreateSprite("Ring", GetRingSprite(), material, 9);

        sparks = new SpriteRenderer[SparkCount];
        velocities = new Vector2[SparkCount];
        spins = new float[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            sparks[i] = CreateSprite("Spark", starSprite, material, 10);
            sparks[i].transform.localScale = Vector3.one * 0.25f;

            float angle = (i + Random.value * 0.6f) / SparkCount * Mathf.PI * 2f;
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2.5f, 4f);
            spins[i] = Random.Range(-360f, 360f);
        }
    }

    private SpriteRenderer CreateSprite(string objectName, Sprite sprite, Material material, int order)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
        return renderer;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);
        float easeOut = 1f - (1f - t) * (1f - t);
        float fade = 1f - t;

        flash.pointLightOuterRadius = Mathf.Lerp(1f, 4.5f, easeOut);
        flash.intensity = 2f * fade;

        ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 5f, easeOut);
        ring.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, fade);

        for (int i = 0; i < sparks.Length; i++)
        {
            Transform spark = sparks[i].transform;
            spark.localPosition = velocities[i] * (easeOut * Duration);
            spark.localRotation = Quaternion.Euler(0f, 0f, spins[i] * age);
            spark.localScale = Vector3.one * (0.25f * (1f - t * 0.6f));
            sparks[i].color = new Color(1f, 0.9f, 0.5f, fade);
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    // A soft ring, drawn once and shared. One world unit across at scale 1.
    private static Sprite GetRingSprite()
    {
        if (ringSprite != null)
        {
            return ringSprite;
        }

        Texture2D texture = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        float half = RingTextureSize * 0.5f;
        for (int y = 0; y < RingTextureSize; y++)
        {
            for (int x = 0; x < RingTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.9f) / 0.1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0, 0, RingTextureSize, RingTextureSize),
            new Vector2(0.5f, 0.5f), RingTextureSize);
        return ringSprite;
    }
}
