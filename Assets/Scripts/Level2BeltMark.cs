using UnityEngine;

// Visual tread motion follows the existing conveyor effector without new physics.
public sealed class Level2BeltMark : MonoBehaviour
{
    private SurfaceEffector2D belt;
    private MapFlipController flip;
    private void Start()
    {
        belt = GetComponentInParent<SurfaceEffector2D>();
        flip = FindFirstObjectByType<MapFlipController>();
    }
    private void Update()
    {
        if (belt == null || (flip != null && flip.IsTurning)) return;
        Vector3 p = transform.localPosition;
        p.x = Mathf.Repeat(p.x + .5f + belt.speed * Time.deltaTime /
            Mathf.Max(.01f, transform.parent.localScale.x), 1f) - .5f;
        transform.localPosition = p;
    }
}
