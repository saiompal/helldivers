using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// One attempt, in maze coordinates. The line stays invisible until a win.
[DefaultExecutionOrder(100)]
public sealed class RunTrajectory : MonoBehaviour
{
    private const float SampleSeconds = .05f;
    private const float RevealSeconds = 1.2f;
    private readonly List<Vector3> samples = new List<Vector3>(2048);
    private readonly List<int> waypoints = new List<int>();
    private readonly HashSet<Collider2D> walls = new HashSet<Collider2D>();
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
    private Transform player, maze;
    private MapFlipController flip;
    private CameraFollow overview;
    private LineRenderer line;
    private Material material;
    private bool recording;
    private float nextSample;
    private Coroutine reveal;
    private readonly List<RunTrajectory> sectionTrails = new List<RunTrajectory>();
    private bool restrictToSection, revealComplete;

    public int SampleCount => samples.Count + sectionTrails.Sum(trail => trail.SampleCount);
    public bool IsRecording => recording;
    public bool IsVisible => (line != null && line.enabled) || sectionTrails.Any(trail => trail.IsVisible);
    public bool RevealComplete => sectionTrails.Count > 0 ? sectionTrails.All(trail => trail.RevealComplete) : revealComplete;

    public bool Configure(Transform ball, Transform map, MapFlipController controller, CameraFollow cameraFollow, bool sectionOnly = false)
    {
        player = ball; maze = map; flip = controller; overview = cameraFollow;
        restrictToSection = sectionOnly;
        if (player == null || maze == null)
        {
            Debug.LogWarning("Trajectory needs a player and maze; level completion remains available.", this);
            return false;
        }
        if (!sectionOnly && controller != null && controller.SectionRoots.Length > 0)
        {
            foreach (Transform section in controller.SectionRoots)
            {
                var child = new GameObject("Route recorder - " + section.name);
                child.transform.SetParent(transform, false);
                var trail = child.AddComponent<RunTrajectory>();
                if (!trail.Configure(ball, section, controller, cameraFollow, true)) return false;
                sectionTrails.Add(trail);
            }
            return true;
        }
        var shader = Resources.Load<Shader>("TrajectoryLine");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("Trajectory shader is unavailable; level completion remains available.", this);
            return false;
        }
        material = new Material(shader);
        var sprite = player.GetComponentInChildren<SpriteRenderer>();
        material.color = sprite != null ? sprite.color : new Color(.56f, .84f, .93f);
        var go = new GameObject("Completed Run Trajectory");
        go.transform.SetParent(maze, false);
        line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 6; line.numCornerVertices = 6;
        line.sortingLayerID = sprite != null ? sprite.sortingLayerID : 0;
        line.sortingOrder = 9;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return true;
    }

    public void BeginRecording()
    {
        Clear();
        if (sectionTrails.Count > 0)
        {
            foreach (var trail in sectionTrails) trail.BeginRecording();
            recording = true;
            return;
        }
        if (line == null) return;
        recording = true;
        CapturePosition();
        nextSample = Time.time + SampleSeconds;
    }

    private void LateUpdate()
    {
        if (sectionTrails.Count > 0) return;
        if (recording && Time.time >= nextSample)
        {
            nextSample = Time.time + SampleSeconds;
            if (flip == null || !flip.IsTurning) CapturePosition();
        }
        if (IsVisible)
        {
            Camera camera = Camera.main;
            if (camera != null)
                line.widthMultiplier = 4f * camera.orthographicSize / Mathf.Max(1, camera.pixelHeight);
        }
    }

    private void CapturePosition()
    {
        if (player == null || maze == null) return;
        if (restrictToSection && flip.GetSectionAt(player.position) != maze) return;
        Vector3 point = maze.InverseTransformPoint(player.position);
        point.z = 0;
        if (samples.Count == 0 || (point - samples[samples.Count - 1]).sqrMagnitude > .000001f)
            samples.Add(point);
    }

    public void MarkWaypoint()
    {
        if (!recording) return;
        if (sectionTrails.Count > 0)
        {
            foreach (var trail in sectionTrails) trail.MarkWaypoint();
            return;
        }
        CapturePosition();
        int index = samples.Count - 1;
        if (index >= 0 && (waypoints.Count == 0 || waypoints[waypoints.Count - 1] != index)) waypoints.Add(index);
    }

    private List<Vector3> BuildPath()
    {
        Physics2D.SyncTransforms();
        walls.Clear();
        foreach (var collider in maze.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || collider.isTrigger || collider.transform.IsChildOf(player)) continue;
            // Moving objects can occupy different places at the end of a run.
            // Only permanent maze geometry constrains the corridor summary.
            if (collider.attachedRigidbody != null && collider.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;
            walls.Add(collider);
        }
        return RunPathSimplifier.Build(samples, waypoints, CanConnect);
    }

    private bool CanConnect(Vector3 a, Vector3 b)
    {
        Vector2 start = maze.TransformPoint(a), delta = maze.TransformPoint(b) - maze.TransformPoint(a);
        float distance = delta.magnitude;
        if (distance < .001f || walls.Count == 0) return true;
        hits.Clear();
        Physics2D.CircleCast(start, .06f, delta / distance,
            new ContactFilter2D { useTriggers = false }, hits, distance);
        foreach (var hit in hits) if (walls.Contains(hit.collider)) return false;
        return true;
    }

    // Called after MapFlipController.Stop, which settles any partial turn.
    public void ShowCompletedRoute()
    {
        if (!recording) return;
        if (sectionTrails.Count > 0)
        {
            foreach (var trail in sectionTrails) trail.ShowCompletedRoute();
            recording = false;
            return;
        }
        CapturePosition();
        recording = false;
        if (samples.Count < 2) { revealComplete = true; return; }
        var points = BuildPath();
        if (points.Count < 2) { revealComplete = true; return; }
        reveal = StartCoroutine(Reveal(points));
    }

    private IEnumerator Reveal(List<Vector3> points)
    {
        float waited = 0;
        while (overview != null && !overview.IsOverviewSettled && waited < 1.5f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        var lengths = new float[points.Count];
        for (int i = 1; i < points.Count; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        line.enabled = true;
        int head = 0;
        for (float elapsed = 0; elapsed < RevealSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float distance = lengths[lengths.Length - 1] * elapsed / RevealSeconds;
            while (head + 1 < points.Count - 1 && lengths[head + 1] <= distance) head++;
            line.positionCount = head + 2;
            for (int i = 0; i <= head; i++) line.SetPosition(i, points[i]);
            float fraction = Mathf.InverseLerp(lengths[head], lengths[head + 1], distance);
            line.SetPosition(head + 1, Vector3.Lerp(points[head], points[head + 1], fraction));
            yield return null;
        }
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        revealComplete = true;
        reveal = null;
    }

    public void Clear()
    {
        recording = false;
        foreach (var trail in sectionTrails) trail.Clear();
        if (reveal != null) { StopCoroutine(reveal); reveal = null; }
        samples.Clear(); waypoints.Clear(); walls.Clear(); hits.Clear(); revealComplete = false;
        if (line != null) { line.enabled = false; line.positionCount = 0; }
    }

    private void OnDestroy()
    {
        if (line != null) Destroy(line.gameObject);
        if (material != null) Destroy(material);
    }
}
