using UnityEngine;

// Shows the whole map during memorization, then follows the player in the dark.
// The visible play area accounts for the HUD above it.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform target;
    [Tooltip("A sprite covering the whole map, used to keep the camera inside it.")]
    [SerializeField] private SpriteRenderer mapBounds;
    [Tooltip("Optional. The camera locks onto the player while the map turns.")]
    [SerializeField] private MapFlipController mapFlip;

    [Header("Zoom")]
    [Tooltip("Orthographic size while following. Smaller shows less of the map.")]
    [SerializeField, Min(1f)] private float followSize = 4f;
    [Tooltip("Temporary wider view while an F light reveal is active.")]
    [SerializeField, Min(1f)] private float revealSize = 8f;
    [SerializeField, Min(0.05f)] private float revealZoomSeconds = 0.25f;
    [SerializeField, Min(0.05f)] private float zoomSeconds = 1f;
    [SerializeField, Min(0f)] private float followSmoothing = 0.12f;

    [Header("HUD")]
    [Tooltip("Share of the screen height covered by the top HUD bar.")]
    [SerializeField, Range(0f, 0.5f)] private float topBarShare;
    [Tooltip("Share of the screen height covered by the bottom HUD bar.")]
    [SerializeField, Range(0f, 0.5f)] private float bottomBarShare;

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
    public bool IsOverviewSettled => targetBlend == 0f && blend <= .001f;

    private void Awake()
    {
        view = GetComponent<Camera>();

        // The level builder frames the whole map, so that's the overview.
        overviewPosition = transform.position;
        overviewSize = view.orthographicSize;
        currentFollowSize = followSize;
        targetFollowSize = followSize;

        if (target != null)
        {
            focus = target.position;
        }
    }

    public void FollowPlayer()
    {
        targetBlend = 1f;
    }

    public void ShowOverview(float footerShare = 0f)
    {
        if (footerShare > 0)
        {
            bottomBarShare = Mathf.Clamp(footerShare, 0, .5f);
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

    private void LateUpdate()
    {
        if (fitCompletion && mapBounds != null)
        {
            Bounds bounds = mapBounds.bounds;
            overviewSize = Mathf.Max((bounds.extents.y + .35f) / (1f - topBarShare - bottomBarShare),
                (bounds.extents.x + .35f) / Mathf.Max(.1f, view.aspect));
            overviewPosition = new Vector3(bounds.center.x,
                bounds.center.y + overviewSize * (topBarShare - bottomBarShare), transform.position.z);
        }
        if (target == null)
        {
            return;
        }

        blend = Mathf.MoveTowards(blend, targetBlend, Time.deltaTime / zoomSeconds);
        float eased = blend * blend * (3f - 2f * blend);
        float revealZoomSpeed = Mathf.Abs(revealSize - followSize) /
                                Mathf.Max(0.05f, revealZoomSeconds);
        currentFollowSize = Mathf.MoveTowards(
            currentFollowSize,
            targetFollowSize,
            revealZoomSpeed * Time.deltaTime
        );

        // While the map turns the player swings across it, so follow them
        // exactly; smoothing would sweep the view over the whole map.
        bool turning = mapFlip != null && mapFlip.IsTurning;
        if (turning || followSmoothing <= 0f)
        {
            focus = target.position;
            focusVelocity = Vector2.zero;
        }
        else
        {
            focus = Vector2.SmoothDamp(focus, target.position, ref focusVelocity, followSmoothing);
        }

        float size = Mathf.Lerp(overviewSize, currentFollowSize, eased);
        Vector3 followPosition = PositionFor(focus, size);

        view.orthographicSize = size;
        transform.position = Vector3.Lerp(overviewPosition, followPosition, eased);
    }

    // Where the camera sits to centre this point between the HUD bars at
    // this size, pushed back from the map edges where needed.
    private Vector3 PositionFor(Vector2 point, float size)
    {
        float halfWidth = size * view.aspect;
        float halfHeight = size * (1f - topBarShare - bottomBarShare);

        if (mapBounds != null)
        {
            Bounds map = mapBounds.bounds;
            point.x = ClampToRange(point.x, map.min.x + halfWidth, map.max.x - halfWidth);
            point.y = ClampToRange(point.y, map.min.y + halfHeight, map.max.y - halfHeight);
        }

        float offsetY = size * (topBarShare - bottomBarShare);
        return new Vector3(point.x, point.y + offsetY, transform.position.z);
    }

    // Centres the value if the view is wider than the range.
    private static float ClampToRange(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
