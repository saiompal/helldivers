using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ButtonControlledWall : MonoBehaviour
{
    [Header("Button Reference")]
    [SerializeField] private PressureButtonSensor button;

    [Header("Appearance")]
    [SerializeField]
    private Color wallColor =
        new Color(1f, 0.6f, 0f, 1f);

    [SerializeField, Range(0f, 1f)]
    private float openAlpha = 0.25f;

    [Header("Runtime State")]
    [SerializeField] private bool isOpen;

    private BoxCollider2D wallCollider;
    private SpriteRenderer wallRenderer;
    private MapFlipController mapFlip;

    private void Awake()
    {
        wallCollider = GetComponent<BoxCollider2D>();
        wallRenderer = GetComponent<SpriteRenderer>();

        mapFlip = FindFirstObjectByType<MapFlipController>();

        ApplyState(false);

        if (button == null)
        {
            Debug.LogWarning(
                "请在 Button 字段中拖入对应的 PressureButton。",
                this
            );
        }
    }

    private void FixedUpdate()
    {
        // 翻转期间保留墙的状态。
        if (mapFlip != null && mapFlip.IsTurning)
            return;

        bool shouldOpen =
            button != null &&
            button.isActiveAndEnabled &&
            button.IsPressed;

        // 按钮松开时，如果角色还在墙里面，
        // 等角色离开后再恢复碰撞，避免将角色卡在墙内。
        if (!shouldOpen && isOpen && HasActorInside())
            return;

        ApplyState(shouldOpen);
    }

    private void ApplyState(bool open)
    {
        isOpen = open;

        // 开门时禁用碰撞体，不需要改成 Trigger。
        wallCollider.enabled = !open;

        Color color = wallColor;
        color.a = open ? openAlpha : 1f;

        wallRenderer.color = color;
    }

    private bool HasActorInside()
    {
        // 碰撞体禁用时，使用它的 Size 和 Offset
        // 计算检测区域，而不是读取它的 bounds。
        Vector2 center =
            transform.TransformPoint(wallCollider.offset);

        Vector3 scale = transform.lossyScale;

        Vector2 size = new Vector2(
            wallCollider.size.x * Mathf.Abs(scale.x),
            wallCollider.size.y * Mathf.Abs(scale.y)
        );

        // 略微扩大检测区域，避免贴边时夹住角色。
        size += Vector2.one * 0.04f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            center,
            size,
            transform.eulerAngles.z
        );

        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger)
                continue;

            Rigidbody2D body = hit.attachedRigidbody;

            if (body == null)
                continue;

            bool isPlayer =
                body.GetComponent<PlayerMovement>() != null;

            bool isShadow =
                body.GetComponent<ShadowFlipFollower>() != null;

            if (isPlayer || isShadow)
                return true;
        }

        return false;
    }
}