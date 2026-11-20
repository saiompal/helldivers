using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PressureButtonSensor : MonoBehaviour
{
    [Header("Button Appearance")]
    [SerializeField] private SpriteRenderer visualRenderer;

    [SerializeField]
    private Color orangeColor =
        new Color(1f, 0.6f, 0f, 1f);

    [SerializeField, Range(0.2f, 1f)]
    private float pressedHeight = 0.6f;

    [SerializeField, Min(0.01f)]
    private float pressDuration = 0.12f;

    [SerializeField, Min(0.1f)]
    private float pulsePeriod = 1.2f;

    [SerializeField, Range(0.1f, 1f)]
    private float minimumBrightness = 0.55f;

    [Header("Release Timing")]
    [SerializeField, Min(0f)]
    private float releaseDelay = 0.15f;

    // 运行时可以在 Inspector 中观察。
    [SerializeField] private bool pressed;

    // 后续门会读取这个状态。
    public bool IsPressed => pressed;

    private Vector3 originalVisualScale;
    private Vector3 originalVisualPosition;

    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;

    private float visualBottom;
    private float pressAmount;
    private float pulseTime;
    private float lastSupportedTime = float.NegativeInfinity;

    private bool initialized;

    private BoxCollider2D buttonCollider;
    private MapFlipController mapFlip;

    private readonly ContactPoint2D[] contacts =
        new ContactPoint2D[32];

    private void Awake()
    {
        buttonCollider = GetComponent<BoxCollider2D>();

        // 保存碰撞体原始形状。
        originalColliderSize = buttonCollider.size;
        originalColliderOffset = buttonCollider.offset;

        mapFlip = FindFirstObjectByType<MapFlipController>();

        if (visualRenderer == null ||
            visualRenderer.transform == transform)
        {
            Debug.LogError(
                "请将 Visual 子对象的 Sprite Renderer " +
                "拖入 Visual Renderer，不要选择按钮父对象。",
                this
            );

            enabled = false;
            return;
        }

        Transform visual = visualRenderer.transform;

        originalVisualScale = visual.localScale;
        originalVisualPosition = visual.localPosition;

        // Sprite 的局部底边。
        visualBottom = visualRenderer.sprite != null
            ? visualRenderer.sprite.bounds.min.y
            : -0.5f;

        pressAmount = 0f;
        pulseTime = 0f;
        lastSupportedTime = float.NegativeInfinity;

        initialized = true;

        SetPressed(false);
        visualRenderer.color = orangeColor;
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        // 翻转期间暂停高度变化，保留之前的按钮状态。
        if (mapFlip != null && mapFlip.IsTurning)
            return;

        bool someoneIsStanding = false;

        int count = buttonCollider.GetContacts(contacts);

        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];

            // 找到接触按钮的另一个碰撞体。
            Collider2D other =
                contact.collider == buttonCollider
                    ? contact.otherCollider
                    : contact.collider;

            if (other == null ||
                !other.enabled ||
                !other.gameObject.activeInHierarchy)
            {
                continue;
            }

            Rigidbody2D body = other.attachedRigidbody;

            if (body == null || !body.simulated)
                continue;

            bool isPlayer =
                body.GetComponent<PlayerMovement>() != null;

            bool isShadow =
                body.GetComponent<ShadowFlipFollower>() != null;

            if (!isPlayer && !isShadow)
                continue;

            // 排除单纯碰到按钮侧面的情况。
            bool horizontalSurface =
                Mathf.Abs(contact.normal.y) > 0.5f;

            // 角色需要在接触点上方。
            bool actorIsAbove =
                other.bounds.center.y > contact.point.y + 0.01f;

            // 向上跳离时不算压住。
            bool notJumpingAway =
                body.linearVelocity.y <= 0.5f;

            if (horizontalSurface &&
                actorIsAbove &&
                notJumpingAway)
            {
                someoneIsStanding = true;
                break;
            }
        }

        if (someoneIsStanding)
        {
            lastSupportedTime = Time.time;
        }

        // 下压后角色需要短暂下落，允许这段时间内失去接触。
        bool keepPressed =
            someoneIsStanding ||
            (pressed &&
             Time.time - lastSupportedTime < releaseDelay);

        SetPressed(keepPressed);

        // 在物理更新中同步调整外观和碰撞体。
        UpdateButtonHeight();
    }

    private void SetPressed(bool value)
    {
        if (value && !pressed)
        {
            pulseTime = 0f;
        }

        pressed = value;
    }

    private void UpdateButtonHeight()
    {
        float target = pressed ? 1f : 0f;

        pressAmount = Mathf.MoveTowards(
            pressAmount,
            target,
            Time.fixedDeltaTime / Mathf.Max(0.01f, pressDuration)
        );

        float eased = Mathf.SmoothStep(0f, 1f, pressAmount);

        float heightFactor =
            Mathf.Lerp(1f, pressedHeight, eased);

        // 外观下压：缩小高度，同时保持底边固定。
        Transform visual = visualRenderer.transform;

        Vector3 scale = originalVisualScale;
        scale.y *= heightFactor;
        visual.localScale = scale;

        Vector3 position = originalVisualPosition;

        position.y += visualBottom *
            (originalVisualScale.y - scale.y);

        visual.localPosition = position;

        // 碰撞体下压：与外观使用相同的高度比例。
        Vector2 size = originalColliderSize;
        size.y *= heightFactor;
        buttonCollider.size = size;

        // 调整中心，使碰撞体底边保持固定。
        Vector2 offset = originalColliderOffset;

        offset.y +=
            (size.y - originalColliderSize.y) * 0.5f;

        buttonCollider.offset = offset;
    }

    private void Update()
    {
        if (!initialized)
            return;

        // 翻转过程中暂停呼吸动画。
        if (mapFlip != null && mapFlip.IsTurning)
            return;

        float brightness = 1f;

        if (pressed)
        {
            pulseTime += Time.deltaTime;

            float wave = 0.5f + 0.5f * Mathf.Cos(
                pulseTime * Mathf.PI * 2f /
                Mathf.Max(0.1f, pulsePeriod)
            );

            brightness = Mathf.Lerp(
                minimumBrightness,
                1f,
                wave
            );
        }

        // 始终保持橙色，只改变亮度，不改变透明度。
        visualRenderer.color = new Color(
            orangeColor.r * brightness,
            orangeColor.g * brightness,
            orangeColor.b * brightness,
            orangeColor.a
        );
    }

    private void OnDisable()
    {
        pressed = false;
        pressAmount = 0f;
        pulseTime = 0f;
        lastSupportedTime = float.NegativeInfinity;

        // 初始化失败时，不使用尚未保存的原始数据。
        if (!initialized)
            return;

        if (buttonCollider != null)
        {
            buttonCollider.size = originalColliderSize;
            buttonCollider.offset = originalColliderOffset;
        }

        if (visualRenderer != null)
        {
            visualRenderer.transform.localScale =
                originalVisualScale;

            visualRenderer.transform.localPosition =
                originalVisualPosition;

            visualRenderer.color = orangeColor;
        }
    }
}