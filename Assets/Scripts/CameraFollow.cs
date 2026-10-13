using UnityEngine;

// Shows the whole map during the preview, then zooms in and follows the
// player so only the area around them is on screen. The camera never
// shows past the edge of the map, and keeps the player centred in the
// space between the HUD bars.
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
    private Vector2 focus;
    private Vector2 focusVelocity;

    private void Awake()
    {
        view = GetComponent<Camera>();

        // The level builder frames the whole map, so that's the overview.
        overviewPosition = transform.position;
        overviewSize = view.orthographicSize;

        if (target != null)
        {
            focus = target.position;
        }
    }

    public void FollowPlayer()
    {
        targetBlend = 1f;
    }

    public void ShowOverview()
    {
        targetBlend = 0f;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        blend = Mathf.MoveTowards(blend, targetBlend, Time.deltaTime / zoomSeconds);
        float eased = blend * blend * (3f - 2f * blend);

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

        float size = Mathf.Lerp(overviewSize, followSize, eased);
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
