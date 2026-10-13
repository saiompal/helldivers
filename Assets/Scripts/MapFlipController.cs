using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Every so often the whole maze turns 180 degrees around its centre and
// carries the player with it. The player keeps their place in the maze,
// but the layout they memorised is now upside down and the controls feel
// reversed. GameManager starts the cycle when play begins and stops it
// when the level ends.
public class MapFlipController : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        Counting,
        Warning,
        Turning
    }

    [Header("Scene References")]
    [Tooltip("Parent of everything that flips: floor, walls, stars and exit.")]
    [SerializeField] private Transform mapRoot;
    [SerializeField] private Rigidbody2D player;
    [Tooltip("Optional. Pulses this light during the warning.")]
    [SerializeField] private Light2D visionLight;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float secondsBetweenFlips = 12f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 2f;
    [SerializeField, Min(0.05f)] private float turnSeconds = 0.6f;

    [Header("Warning")]
    [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField, Min(0.1f)] private float warningPulseSpeed = 12f;

    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    public bool IsTurning => CurrentPhase == Phase.Turning;
    public bool IsFlipped { get; private set; }
    public int FlipCount { get; private set; }

    // Seconds until the turn starts, counting through the warning.
    public float SecondsUntilFlip => CurrentPhase switch
    {
        Phase.Counting => secondsBetweenFlips - elapsed + warningSeconds,
        Phase.Warning => warningSeconds - elapsed,
        _ => 0f
    };

    public event Action Flipped;

    private Vector3 pivot;
    private Quaternion turnStartRotation;
    private Vector2 turnStartPosition;
    private RigidbodyInterpolation2D normalInterpolation;
    private Color visionColor;
    private float elapsed;

    private void Awake()
    {
        if (mapRoot == null || player == null)
        {
            Debug.LogError("MapFlipController needs Map Root and Player references.", this);
            enabled = false;
            return;
        }

        // The maze is centred on the map root, so turning around it keeps
        // the maze in the same spot on screen.
        pivot = mapRoot.position;

        if (visionLight != null)
        {
            visionColor = visionLight.color;
        }
    }

    public void Begin()
    {
        if (!enabled)
        {
            return;
        }

        elapsed = 0f;
        CurrentPhase = Phase.Counting;
    }

    // Finishes any turn in progress so the player is never left frozen.
    public void Stop()
    {
        if (CurrentPhase == Phase.Turning)
        {
            FinishTurn();
        }

        ResetVisionColor();
        CurrentPhase = Phase.Idle;
    }

    private void Update()
    {
        if (CurrentPhase == Phase.Idle)
        {
            return;
        }

        elapsed += Time.deltaTime;

        switch (CurrentPhase)
        {
            case Phase.Counting:
                if (elapsed >= secondsBetweenFlips)
                {
                    CurrentPhase = Phase.Warning;
                    elapsed = 0f;
                }
                break;

            case Phase.Warning:
                PulseVisionLight();
                if (elapsed >= warningSeconds)
                {
                    BeginTurn();
                }
                break;

            case Phase.Turning:
                AnimateTurn();
                break;
        }
    }

    private void PulseVisionLight()
    {
        if (visionLight == null)
        {
            return;
        }

        float t = (Mathf.Sin(elapsed * warningPulseSpeed) + 1f) * 0.5f;
        visionLight.color = Color.Lerp(visionColor, warningColor, t);
    }

    private void ResetVisionColor()
    {
        if (visionLight != null)
        {
            visionLight.color = visionColor;
        }
    }

    private void BeginTurn()
    {
        ResetVisionColor();

        // Physics is paused for the turn so the walls can't shove the
        // player while they sweep past.
        player.linearVelocity = Vector2.zero;
        player.angularVelocity = 0f;
        player.simulated = false;

        // Interpolation would blend from the pre-turn spot once physics resumes.
        normalInterpolation = player.interpolation;
        player.interpolation = RigidbodyInterpolation2D.None;

        turnStartPosition = player.position;
        turnStartRotation = mapRoot.rotation;
        CurrentPhase = Phase.Turning;
        elapsed = 0f;
    }

    private void AnimateTurn()
    {
        float t = Mathf.Clamp01(elapsed / turnSeconds);
        float eased = t * t * (3f - 2f * t);
        ApplyTurn(eased);

        if (t >= 1f)
        {
            FinishTurn();
        }
    }

    private void ApplyTurn(float progress)
    {
        Quaternion delta = Quaternion.Euler(0f, 0f, 180f * progress);
        mapRoot.rotation = delta * turnStartRotation;

        // The player moves with the maze but stays upright.
        Vector3 offset = (Vector3)turnStartPosition - pivot;
        offset.z = 0f;
        Vector3 position = pivot + delta * offset;
        player.transform.position = new Vector3(
            position.x, position.y, player.transform.position.z
        );
    }

    private void FinishTurn()
    {
        IsFlipped = !IsFlipped;

        // Snap to an exact half turn so rounding never builds up.
        mapRoot.rotation = Quaternion.Euler(0f, 0f, IsFlipped ? 180f : 0f);
        Vector2 end = 2f * (Vector2)pivot - turnStartPosition;
        player.transform.position = new Vector3(end.x, end.y, player.transform.position.z);
        player.position = end;
        player.simulated = true;
        player.interpolation = normalInterpolation;
        Physics2D.SyncTransforms();

        FlipCount++;
        CurrentPhase = Phase.Counting;
        elapsed = 0f;
        Flipped?.Invoke();
    }
}
