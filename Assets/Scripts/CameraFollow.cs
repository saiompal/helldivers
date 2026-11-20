using UnityEngine;

// Default: preserve the scene's original overview.
// Enable Auto Fit This Level on individual scene cameras to fit their map.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform target;
    [Tooltip("覆盖完整地图的背景 Sprite，用于计算全景和跟随边界。")]
    [SerializeField] private SpriteRenderer mapBounds;
    [SerializeField] private MapFlipController mapFlip;

    [Header("Level Camera Settings")]
    [Tooltip("仅对当前相机启用自动适配。关闭时沿用场景原有全景位置和大小。")]
    [SerializeField] private bool autoFitThisLevel = false;

    [Header("Zoom")]
    [SerializeField, Min(1f)] private float followSize = 4f;
    [SerializeField, Min(1f)] private float revealSize = 8f;
    [SerializeField, Min(0.05f)] private float revealZoomSeconds = 0.25f;
    [SerializeField, Min(0.05f)] private float zoomSeconds = 1f;
    [SerializeField, Min(0f)] private float followSmoothing = 0.12f;

    [Header("Overview")]
    [Tooltip("自动适配时，地图四周额外预留的世界坐标距离。")]
    [SerializeField, Min(0f)] private float overviewPadding = 0.35f;

    [Header("HUD")]
    [SerializeField, Range(0f, 0.5f)] private float topBarShare;
    [SerializeField, Range(0f, 0.5f)] private float bottomBarShare;

    [Header("Minimum HUD Safe Area")]
    [Tooltip("仅自动适配模式使用：顶部至少预留的屏幕高度比例。")]
    [SerializeField, Range(0f, 0.4f)] private float minimumTopShare = 0.18f;
    [Tooltip("仅自动适配模式使用：底部至少预留的屏幕高度比例。")]
    [SerializeField, Range(0f, 0.4f)] private float minimumBottomShare = 0.16f;

    private Camera view;
    private Vector3 overviewPosition;
    private float overviewSize;
    private float blend;
    private float targetBlend;
    private float currentFollowSize;
    private float targetFollowSize;
    private Vector2 focus;
    private Vector2 focusVelocity;
    private bool fitCompletion;

    private float EffectiveTopShare => autoFitThisLevel
        ? Mathf.Max(topBarShare, minimumTopShare) : topBarShare;

    private float EffectiveBottomShare => autoFitThisLevel
        ? Mathf.Max(bottomBarShare, minimumBottomShare) : bottomBarShare;

    public bool IsOverviewSettled => targetBlend == 0f && blend <= 0.001f;

    private void Awake()
    {
        view = GetComponent<Camera>();
        overviewPosition = transform.position;
        overviewSize = view.orthographicSize;
        currentFollowSize = followSize;
        targetFollowSize = followSize;

        if (target != null)
            focus = target.position;

        if (autoFitThisLevel)
        {
            RefreshOverview();
            view.orthographicSize = overviewSize;
            transform.position = overviewPosition;
        }
    }

    public void FollowPlayer()
    {
        targetBlend = 1f;
    }

    public void ShowOverview(float footerShare = 0f)
    {
        if (footerShare > 0f)
        {
            bottomBarShare = Mathf.Clamp(footerShare, 0f, 0.5f);
            fitCompletion = true;
        }
        targetBlend = 0f;
    }

    public void SetRevealView(bool revealing)
    {
        targetFollowSize = revealing ? revealSize : followSize;
    }

    public void SetHudShares(float topShare, float bottomShare)
    {
        topBarShare = Mathf.Clamp(topShare, 0f, 0.5f);
        bottomBarShare = Mathf.Clamp(bottomShare, 0f, 0.5f);
    }

    private float AvailableHeightShare()
    {
        return Mathf.Max(0.05f, 1f - EffectiveTopShare - EffectiveBottomShare);
    }

    private void RefreshOverview()
    {
        // Preserve the original completion overview behavior in ordinary levels.
        if ((!autoFitThisLevel && !fitCompletion) || view == null || mapBounds == null)
            return;

        Bounds bounds = mapBounds.bounds;
        float padding = autoFitThisLevel ? overviewPadding : 0.35f;
        float sizeForHeight = (bounds.extents.y + padding) / AvailableHeightShare();
        float sizeForWidth = (bounds.extents.x + padding) / Mathf.Max(0.1f, view.aspect);
        overviewSize = Mathf.Max(0.1f, Mathf.Max(sizeForHeight, sizeForWidth));

        float offsetY = overviewSize * (EffectiveTopShare - EffectiveBottomShare);
        overviewPosition = new Vector3(
            bounds.center.x, bounds.center.y + offsetY, transform.position.z);
    }

    private void LateUpdate()
    {
        RefreshOverview();

        if (target == null)
        {
            if (autoFitThisLevel)
            {
                view.orthographicSize = overviewSize;
                transform.position = overviewPosition;
            }
            return;
        }

        blend = Mathf.MoveTowards(
            blend, targetBlend, Time.deltaTime / Mathf.Max(0.05f, zoomSeconds));
        float eased = blend * blend * (3f - 2f * blend);
        float revealZoomSpeed = Mathf.Abs(revealSize - followSize) /
                                Mathf.Max(0.05f, revealZoomSeconds);
        currentFollowSize = Mathf.MoveTowards(
            currentFollowSize, targetFollowSize, revealZoomSpeed * Time.deltaTime);

        bool turning = mapFlip != null && mapFlip.IsTurning;
        if (turning || followSmoothing <= 0f)
        {
            focus = target.position;
            focusVelocity = Vector2.zero;
        }
        else
        {
            focus = Vector2.SmoothDamp(
                focus, target.position, ref focusVelocity, followSmoothing);
        }

        float size = Mathf.Lerp(overviewSize, currentFollowSize, eased);
        Vector3 followPosition = PositionFor(focus, size);
        view.orthographicSize = size;
        transform.position = Vector3.Lerp(overviewPosition, followPosition, eased);
    }

    private Vector3 PositionFor(Vector2 point, float size)
    {
        float halfWidth = size * view.aspect;
        float halfHeight = size * AvailableHeightShare();

        if (mapBounds != null)
        {
            Bounds map = mapBounds.bounds;
            point.x = ClampToRange(point.x, map.min.x + halfWidth, map.max.x - halfWidth);
            point.y = ClampToRange(point.y, map.min.y + halfHeight, map.max.y - halfHeight);
        }

        float offsetY = size * (EffectiveTopShare - EffectiveBottomShare);
        return new Vector3(point.x, point.y + offsetY, transform.position.z);
    }

    private static float ClampToRange(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}