using UnityEngine;
using UnityEngine.InputSystem;

// Standard side-on controller used by every level. Horizontal movement uses
// acceleration instead of assigning velocity directly, so slopes, moving
// platforms, conveyors, and momentum all behave consistently.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Running")]
    [Tooltip("Maximum horizontal world speed.")]
    [SerializeField, Min(0f)] private float moveSpeed = 7.5f;
    [SerializeField, Min(0f)] private float groundAcceleration = 38f;
    [SerializeField, Min(0f)] private float airAcceleration = 12f;
    [SerializeField, Min(0f)] private float flatGroundBraking = 13f;

    [Header("Jumping")]
    [SerializeField, Min(0f)] private float jumpSpeed = 13.2f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 22f;
    [Tooltip("A jump still works this long after running off a ledge.")]
    [SerializeField, Min(0f)] private float coyoteSeconds = 0.1f;
    [Tooltip("A jump pressed this long before landing still happens on landing.")]
    [SerializeField, Min(0f)] private float jumpBufferSeconds = 0.12f;
    [Tooltip("How upward a contact must face to count as ground.")]
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormal = 0.45f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[24];
    private Rigidbody2D body;
    private float steer;
    private float jumpRequestedUntil = float.NegativeInfinity;
    private float groundedUntil = float.NegativeInfinity;
    private float normalGravityScale;
    private Vector2 lastSupportVelocity;

    public bool IsGrounded { get; private set; }

    private bool Active =>
        GameManager.Instance == null || GameManager.Instance.CanPlayerMove;

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        normalGravityScale = body.gravityScale;
    }

    protected virtual void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !Active)
        {
            ClearInput();
            return;
        }

        steer = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
              - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);

        if (keyboard.spaceKey.wasPressedThisFrame ||
            keyboard.wKey.wasPressedThisFrame ||
            keyboard.upArrowKey.wasPressedThisFrame)
        {
            jumpRequestedUntil = Time.time + jumpBufferSeconds;
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!body.simulated)
        {
            ClearInput();
            IsGrounded = false;
            groundedUntil = float.NegativeInfinity;
            return;
        }

        if (!Active)
        {
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            IsGrounded = false;
            groundedUntil = float.NegativeInfinity;
            return;
        }

        body.gravityScale = normalGravityScale;

        Vector2 groundNormal = Vector2.up;
        Vector2 supportVelocity = Vector2.zero;
        SurfaceEffector2D groundBelt = null;
        bool grounded = FindGround(
            ref groundNormal,
            ref supportVelocity,
            ref groundBelt
        );
        IsGrounded = grounded;

        if (grounded)
        {
            groundedUntil = Time.time + coyoteSeconds;
            lastSupportVelocity = supportVelocity;
        }

        Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x);
        bool onBelt = grounded && groundBelt != null;

        if (onBelt)
        {
            DriveAlongBelt(tangent, supportVelocity, groundBelt);
        }
        else
        {
            Vector2 direction = grounded ? tangent : Vector2.right;
            float acceleration = grounded ? groundAcceleration : airAcceleration;
            body.AddForce(direction * (steer * acceleration * body.mass));
        }

        // Brake relative to the supporting body. Do not cancel belt motion,
        // downhill acceleration, or a lift's vertical carrying velocity.
        if (grounded && !onBelt && Mathf.Abs(groundNormal.x) < 0.08f &&
            Mathf.Abs(steer) < 0.01f)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(
                velocity.x,
                supportVelocity.x,
                flatGroundBraking * Time.fixedDeltaTime
            );
            body.linearVelocity = velocity;
            body.angularVelocity = Mathf.MoveTowards(
                body.angularVelocity,
                0f,
                flatGroundBraking * 50f * Time.fixedDeltaTime
            );
        }

        if (Time.time <= jumpRequestedUntil && Time.time <= groundedUntil)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.y = jumpSpeed + lastSupportVelocity.y;
            body.linearVelocity = velocity;
            jumpRequestedUntil = float.NegativeInfinity;
            groundedUntil = float.NegativeInfinity;
            IsGrounded = false;
        }

        Vector2 limited = body.linearVelocity;
        limited.x = Mathf.Clamp(limited.x, -moveSpeed, moveSpeed);
        limited.y = Mathf.Max(limited.y, -maxFallSpeed);
        body.linearVelocity = limited;
    }

    private bool FindGround(
        ref Vector2 bestNormal,
        ref Vector2 supportVelocity,
        ref SurfaceEffector2D groundBelt)
    {
        bool grounded = false;
        int count = body.GetContacts(contacts);

        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];
            if (contact.normal.y <= minimumGroundNormal ||
                (grounded && contact.normal.y <= bestNormal.y))
            {
                continue;
            }

            Collider2D surface = contact.collider;
            if (surface != null && surface.attachedRigidbody == body)
            {
                surface = contact.otherCollider;
            }

            Rigidbody2D support = surface != null ? surface.attachedRigidbody : null;
            Vector2 velocity = support != null && support != body
                ? support.GetPointVelocity(contact.point)
                : Vector2.zero;

            // Uphill travel has positive world Y while still on the slope.
            // Only velocity away from the surface means the player took off.
            if (Vector2.Dot(body.linearVelocity - velocity, contact.normal) > 0.3f)
            {
                continue;
            }

            bestNormal = contact.normal;
            supportVelocity = velocity;
            groundBelt = surface != null
                ? surface.GetComponent<SurfaceEffector2D>()
                : null;
            grounded = true;
        }

        return grounded;
    }

    private void DriveAlongBelt(
        Vector2 tangent,
        Vector2 supportVelocity,
        SurfaceEffector2D groundBelt)
    {
        Vector2 velocity = body.linearVelocity;
        float along = Vector2.Dot(velocity - supportVelocity, tangent);
        Vector2 beltVelocity = (Vector2)groundBelt.transform.right * groundBelt.speed;
        float target = Vector2.Dot(beltVelocity, tangent) + steer * moveSpeed;
        float acceleration = Mathf.Abs(steer) > 0.01f ? groundAcceleration : 12f;
        float driven = Mathf.MoveTowards(
            along,
            target,
            acceleration * Time.fixedDeltaTime
        );

        // Change only tangent velocity, preserving gravity/contact response and
        // vertical lift velocity. Jumping remains under player control.
        body.linearVelocity = velocity + tangent * (driven - along);
    }

    private void ClearInput()
    {
        steer = 0f;
        jumpRequestedUntil = float.NegativeInfinity;
    }

    protected virtual void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ClearInput();
        }
    }
}
