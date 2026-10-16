using UnityEngine;
using UnityEngine.InputSystem;

// Manual roll/jump controller for Level 2 only. Level 1's sprite and light
// remain on the player. No automatic hopping and no attachment power.
[RequireComponent(typeof(Rigidbody2D))]
public sealed class Level2BallMotor : MonoBehaviour
{
    [SerializeField, Min(0f)] private float groundAcceleration = 38f;
    [SerializeField, Min(0f)] private float airAcceleration = 12f;
    [SerializeField, Min(0f)] private float maximumHorizontalSpeed = 7.5f;
    [SerializeField, Min(0f)] private float flatGroundBraking = 13f;
    [SerializeField, Min(0f)] private float jumpSpeed = 13.2f;
    [SerializeField, Min(0f)] private float coyoteTime = .10f;
    [SerializeField, Min(0f)] private float jumpBuffer = .12f;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[24];
    private Rigidbody2D body;
    private float steer;
    private float jumpUntil = -100f, groundedUntil = -100f;
    private Vector2 lastSupportVelocity;

    private bool Active => GameManager.Instance == null || GameManager.Instance.CanPlayerMove;
    private void Awake() { body = GetComponent<Rigidbody2D>(); }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null || !Active) { ClearInput(); return; }
        steer = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f)
              - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
        if (keys.spaceKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame)
            jumpUntil = Time.time + jumpBuffer;
    }

    private void FixedUpdate()
    {
        if (!body.simulated) { ClearInput(); groundedUntil = -100f; return; }
        if (!Active)
        {
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            groundedUntil = -100f;
            return;
        }
        body.gravityScale = 3f;
        Vector2 normal = Vector2.up, supportVelocity = Vector2.zero;
        bool grounded = false;
        SurfaceEffector2D groundBelt = null;
        int count = body.GetContacts(contacts);
        for (int i = 0; i < count; ++i)
        {
            ContactPoint2D contact = contacts[i];
            if (contact.normal.y <= .45f || (grounded && contact.normal.y <= normal.y)) continue;
            Collider2D surface = contact.collider;
            if (surface != null && surface.attachedRigidbody == body) surface = contact.otherCollider;
            Rigidbody2D support = surface != null ? surface.attachedRigidbody : null;
            Vector2 velocity = support != null && support != body ? support.GetPointVelocity(contact.point) : Vector2.zero;
            // Uphill rolling has positive world Y while still touching the
            // slope. Only motion AWAY FROM the surface means taking off.
            if (Vector2.Dot(body.linearVelocity - velocity, contact.normal) > .3f) continue;
            normal = contact.normal;
            supportVelocity = velocity;
            groundBelt = surface != null ? surface.GetComponent<SurfaceEffector2D>() : null;
            grounded = true;
        }
        if (grounded)
        {
            groundedUntil = Time.time + coyoteTime;
            lastSupportVelocity = supportVelocity;
        }
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        bool onBelt = grounded && groundBelt != null;
        if (onBelt)
        {
            // The controller owns tangential drive. This avoids a second
            // full-strength effector fighting steering or holding a jump.
            // Belt direction follows the rotating map, including its underside.
            Vector2 velocity = body.linearVelocity;
            float along = Vector2.Dot(velocity - supportVelocity, tangent);
            Vector2 beltVelocity = (Vector2)groundBelt.transform.right * groundBelt.speed;
            float target = Vector2.Dot(beltVelocity, tangent) + steer * maximumHorizontalSpeed;
            float acceleration = Mathf.Abs(steer) > .01f ? groundAcceleration : 12f;
            float driven = Mathf.MoveTowards(along, target, acceleration * Time.fixedDeltaTime);
            // Adjust only tangent velocity: gravity/contact normals and lift
            // velocity remain intact. Jump below remains under player control.
            body.linearVelocity = velocity + tangent * (driven - along);
        }
        else
        {
            body.AddForce((grounded ? tangent : Vector2.right) *
                (steer * (grounded ? groundAcceleration : airAcceleration) * body.mass));
        }

        // Brake relative to the supporting body. Never cancel belt motion,
        // downhill acceleration or a lift's vertical carrying velocity.
        if (grounded && !onBelt && Mathf.Abs(normal.x) < .08f && Mathf.Abs(steer) < .01f)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, supportVelocity.x,
                flatGroundBraking * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
            body.angularVelocity = Mathf.MoveTowards(body.angularVelocity, 0f,
                flatGroundBraking * 50f * Time.fixedDeltaTime);
        }
        if (Time.time <= jumpUntil && Time.time <= groundedUntil)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.y = jumpSpeed + lastSupportVelocity.y;
            body.linearVelocity = velocity;
            jumpUntil = groundedUntil = -100f;
        }
        Vector2 limited = body.linearVelocity;
        limited.x = Mathf.Clamp(limited.x, -maximumHorizontalSpeed, maximumHorizontalSpeed);
        limited.y = Mathf.Max(limited.y, -22f);
        body.linearVelocity = limited;
    }

    private void ClearInput() { steer = 0f; jumpUntil = -100f; }
    private void OnApplicationFocus(bool focus) { if (!focus) ClearInput(); }
}
