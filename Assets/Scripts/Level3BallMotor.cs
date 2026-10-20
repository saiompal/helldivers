using UnityEngine;

// Compatibility component for existing Level 3 scene data. All movement now
// lives in PlayerMovement so current and future levels use the same controller.
[RequireComponent(typeof(Rigidbody2D))]
public sealed class Level3BallMotor : PlayerMovement
{
}
