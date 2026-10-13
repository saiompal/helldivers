using UnityEngine;
using UnityEngine.InputSystem;

// Side-on platformer movement: run left and right, jump, and fall. Gravity
// comes from the Rigidbody2D's gravity scale and always pulls down the
// screen, so when the map flips the player falls towards what used to be
// the ceiling.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Running")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;

    [Header("Jumping")]
    [SerializeField, Min(0f)] private float jumpSpeed = 14f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 18f;
    [Tooltip("Letting go of jump while rising multiplies the upward speed by this, for short hops.")]
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;
    [Tooltip("A jump still works this long after running off a ledge.")]
    [SerializeField, Min(0f)] private float coyoteSeconds = 0.08f;
    [Tooltip("A jump pressed this long before landing still happens on landing.")]
    [SerializeField, Min(0f)] private float jumpBufferSeconds = 0.12f;
    [Tooltip("How upward a contact must face to count as ground.")]
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormal = 0.65f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private Rigidbody2D body;
    private float horizontal;
    private float jumpRequestedUntil = float.NegativeInfinity;
    private float lastGroundedTime = float.NegativeInfinity;
    private bool jumpReleased;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        bool canMove = GameManager.Instance == null ||
                       GameManager.Instance.CanPlayerMove;
        Keyboard keyboard = Keyboard.current;

        if (!canMove || keyboard == null)
        {
            ClearInput();
            return;
        }

        horizontal = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            horizontal -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            horizontal += 1f;

        // Only a fresh press asks for a jump; holding it never repeats.
        if (keyboard.spaceKey.wasPressedThisFrame ||
            keyboard.wKey.wasPressedThisFrame ||
            keyboard.upArrowKey.wasPressedThisFrame)
        {
            jumpRequestedUntil = Time.time + jumpBufferSeconds;
        }

        bool jumpHeld = keyboard.spaceKey.isPressed ||
                        keyboard.wKey.isPressed ||
                        keyboard.upArrowKey.isPressed;

        if (!jumpHeld && (keyboard.spaceKey.wasReleasedThisFrame ||
                          keyboard.wKey.wasReleasedThisFrame ||
                          keyboard.upArrowKey.wasReleasedThisFrame))
        {
            jumpReleased = true;
        }
    }

    private void FixedUpdate()
    {
        // The map flip pauses physics while it turns.
        if (!body.simulated)
        {
            IsGrounded = false;
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = horizontal * moveSpeed;

        // Ignore the ground contact left over from the step we took off on.
        IsGrounded = velocity.y <= 0.05f && HasGroundContact();
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
        }

        bool canJump = Time.time - lastGroundedTime <= coyoteSeconds;
        if (Time.time <= jumpRequestedUntil && canJump)
        {
            velocity.y = jumpSpeed;
            jumpRequestedUntil = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
        }

        if (jumpReleased)
        {
            if (velocity.y > 0f)
            {
                velocity.y *= jumpCutMultiplier;
            }
            jumpReleased = false;
        }

        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        body.linearVelocity = velocity;
    }

    private bool HasGroundContact()
    {
        int count = body.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= minimumGroundNormal)
            {
                return true;
            }
        }
        return false;
    }

    private void ClearInput()
    {
        horizontal = 0f;
        jumpRequestedUntil = float.NegativeInfinity;
        jumpReleased = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ClearInput();
        }
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}
