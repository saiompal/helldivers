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
    [Tooltip("Optional independently rotating sections, used by the linked-map level.")]
    [SerializeField] private Transform[] sectionRoots = Array.Empty<Transform>();
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
    public Transform MapRoot => mapRoot;
    public Transform[] SectionRoots => sectionRoots;
    public Transform PlayerSection => IsTurning ? turnSection : GetSectionAt(player.position);
    public Transform GetSectionAt(Vector3 position)
    {
        Transform nearest = mapRoot;
        float distance = float.PositiveInfinity;
        foreach (Transform section in sectionRoots)
        {
            if (section == null) continue;
            float next = (position - section.position).sqrMagnitude;
            if (next < distance) { nearest = section; distance = next; }
        }
        return nearest;
    }
    public Transform PlayerTransform => player != null ? player.transform : null;

    public float CycleDuration => secondsBetweenFlips + warningSeconds;
    public float CycleProgress01 => CurrentPhase switch
    {
        Phase.Counting => Mathf.Clamp01(elapsed / CycleDuration),
        Phase.Warning => Mathf.Clamp01((secondsBetweenFlips + elapsed) / CycleDuration),
        Phase.Turning => 1f,
        _ => 0f
    };
    public float TurnProgress01 => CurrentPhase == Phase.Turning
        ? visualTurnProgress
        : 0f;

    // Seconds until the turn starts, counting through the warning.
    public float SecondsUntilFlip => CurrentPhase switch
    {
        Phase.Counting => secondsBetweenFlips - elapsed + warningSeconds,
        Phase.Warning => warningSeconds - elapsed,
        _ => 0f
    };

    public event Action Flipped;
    public event Action TurnStarted;
    public event Action<Vector3, Quaternion> TurnPoseApplied;
    public event Action TurnFinished;

    private Vector3 pivot;
    private Quaternion turnStartRotation;
    private Quaternion[] sectionStartRotations;
    private Transform turnSection;
    private Vector2 turnStartPosition;
    private RigidbodyInterpolation2D normalInterpolation;
    private Color visionColor;
    private float elapsed;
    private float visualTurnProgress;

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
        visualTurnProgress = 0f;
        CurrentPhase = Phase.Counting;
    }

    // Skips the wait and starts the red warning immediately.
    public void FlipSoon()
    {
        if (enabled && CurrentPhase == Phase.Counting)
        {
            CurrentPhase = Phase.Warning;
            elapsed = 0f;
        }
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
        turnSection = GetSectionAt(player.position);
        pivot = turnSection.position;
        turnStartRotation = mapRoot.rotation;
        sectionStartRotations = new Quaternion[sectionRoots.Length];
        for (int i = 0; i < sectionRoots.Length; i++) sectionStartRotations[i] = sectionRoots[i].rotation;
        CurrentPhase = Phase.Turning;
        elapsed = 0f;
        TurnStarted?.Invoke();
    }

    private void AnimateTurn()
    {
        float t = Mathf.Clamp01(elapsed / turnSeconds);
        visualTurnProgress = t * t * (3f - 2f * t);
        ApplyTurn(visualTurnProgress);

        if (t >= 1f)
        {
            FinishTurn();
        }
    }

    private void ApplyTurn(float progress)
    {
        Quaternion delta = Quaternion.Euler(0f, 0f, 180f * progress);
        if (sectionRoots.Length == 0) mapRoot.rotation = delta * turnStartRotation;
        else for (int i = 0; i < sectionRoots.Length; i++)
            sectionRoots[i].rotation = delta * sectionStartRotations[i];

        // The player moves with the maze but stays upright.
        Vector3 offset = (Vector3)turnStartPosition - pivot;
        offset.z = 0f;
        Vector3 position = pivot + delta * offset;
        player.transform.position = new Vector3(
            position.x, position.y, player.transform.position.z
        );
        TurnPoseApplied?.Invoke(pivot, delta);
    }

    private void FinishTurn()
    {
        IsFlipped = !IsFlipped;

        // Snap to an exact half turn so rounding never builds up.
        Quaternion endRotation = Quaternion.Euler(0f, 0f, IsFlipped ? 180f : 0f);
        if (sectionRoots.Length == 0) mapRoot.rotation = endRotation;
        else foreach (Transform section in sectionRoots) section.rotation = endRotation;
        Vector2 end = 2f * (Vector2)pivot - turnStartPosition;
        player.transform.position = new Vector3(end.x, end.y, player.transform.position.z);
        player.position = end;
        player.simulated = true;
        player.interpolation = normalInterpolation;
        // Carry shadow clones to the exact endpoint before resuming physics.
        TurnPoseApplied?.Invoke(pivot, Quaternion.Euler(0f, 0f, 180f));
        TurnFinished?.Invoke();
        Physics2D.SyncTransforms();

        FlipCount++;
        visualTurnProgress = 0f;
        CurrentPhase = Phase.Counting;
        elapsed = 0f;
        Flipped?.Invoke();
    }
}
