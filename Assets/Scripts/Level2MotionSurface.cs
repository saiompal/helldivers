using UnityEngine;

// Lifts use map-local coordinates and keep their phase across the original
// world rotation. They never animate during the initial map preview.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class Level2MotionSurface : MonoBehaviour
{
    [SerializeField] private Vector2 localTravel = new Vector2(0f, 2f);
    [SerializeField, Min(.1f)] private float cycleSeconds = 4f;
    [SerializeField] private float phaseOffset;
    private Rigidbody2D body;
    private MapFlipController flip;
    private Vector3 localOrigin;
    private float elapsed;
    private bool suspended;

    public void SetShuttle(Vector2 travel, float seconds, float phase = 0f)
    {
        localTravel = travel;
        cycleSeconds = Mathf.Max(.1f, seconds);
        phaseOffset = phase;
        elapsed = 0f;
        SnapToPath();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.None;
        localOrigin = transform.localPosition;
        flip = FindFirstObjectByType<MapFlipController>();
    }

    private Vector3 WorldTarget()
    {
        float cycle = Mathf.Sin(elapsed * Mathf.PI * 2f / cycleSeconds + phaseOffset);
        return transform.parent.TransformPoint(localOrigin + (Vector3)localTravel * cycle);
    }

    private void SnapToPath()
    {
        if (body == null) return;
        Vector3 target = WorldTarget();
        body.position = target;
        body.rotation = transform.parent.eulerAngles.z;
        transform.position = target;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void LateUpdate()
    {
        // MapFlipController rotates Map in Update, before this method. Disable
        // lift contacts for the turn just as the source does for the player.
        if (flip != null && flip.IsTurning)
        {
            if (!suspended) { body.simulated = false; suspended = true; }
            SnapToPath();
        }
        else if (suspended)
        {
            SnapToPath();
            body.simulated = true;
            suspended = false;
            Physics2D.SyncTransforms();
        }
    }

    private void FixedUpdate()
    {
        if (suspended || (flip != null && flip.IsTurning)) return;
        if (GameManager.Instance != null && !GameManager.Instance.CanPlayerMove)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }
        elapsed += Time.fixedDeltaTime;
        body.MovePosition(WorldTarget());
    }
}
