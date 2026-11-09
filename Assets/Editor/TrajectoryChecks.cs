using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

// Deterministic play-mode checks; these do not change scene assets or player saves.
internal static class TrajectoryChecks
{
    private static readonly MethodInfo Capture = typeof(RunTrajectory).GetMethod("CapturePosition", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void VerifyRecording()
    {
        var root = new GameObject("Trajectory check");
        var map = new GameObject("Check maze").transform; map.SetParent(root.transform);
        var ball = new GameObject("Check ball").transform; ball.SetParent(root.transform);
        map.position = new Vector3(12, -7, 0);
        map.rotation = Quaternion.Euler(0, 0, 37);
        ball.position = map.TransformPoint(Vector3.zero);
        var trace = root.AddComponent<RunTrajectory>();
        try
        {
            Require(trace.Configure(ball, map, null, null), "Trajectory shader or setup failed");
            trace.BeginRecording();
            Capture.Invoke(trace, null);
            Require(trace.SampleCount == 1 && !trace.IsVisible, "Stationary movement should not grow or show the trail");
            foreach (Vector3 local in new[] { new Vector3(1, 0), new Vector3(2, 1), new Vector3(3, 0), new Vector3(2, 1), new Vector3(1, 0) })
            {
                ball.position = map.TransformPoint(local);
                Capture.Invoke(trace, null);
            }
            int count = trace.SampleCount;
            foreach (float angle in new[] { 217f, 397f })
            {
                map.rotation = Quaternion.Euler(0, 0, angle);
                ball.position = map.TransformPoint(new Vector3(1, 0));
                Capture.Invoke(trace, null);
                Require(trace.SampleCount == count, "World flips must not add a sweeping movement arc");
            }
            // This final point is deliberately not sampled until completion.
            ball.position = map.TransformPoint(new Vector3(4, -2));
            trace.ShowCompletedRoute();
            Require(!trace.IsRecording && trace.SampleCount == count + 1, "Completion must capture the exact endpoint and freeze the recording");
            var simplified = BuildPath(trace);
            Require(simplified[0].sqrMagnitude < .00001f && Vector3.Distance(simplified[simplified.Count - 1], new Vector3(4, -2)) < .001f,
                "Simplification must retain exact endpoints");
            Require(simplified.Count == 2, "An open passage should collapse jumps and repeated local backtracking");
            trace.Clear();
            Require(trace.SampleCount == 0 && !trace.IsRecording && !trace.IsVisible, "Clearing an attempt must remove its path");
            VerifyPassages(trace, ball, map);
        }
        finally { UnityEngine.Object.Destroy(root); }
        Debug.Log("TRAJECTORY_RECORDING_CHECKS_SUCCESS");
    }

    private static List<Vector3> BuildPath(RunTrajectory trace) =>
        (List<Vector3>)typeof(RunTrajectory).GetMethod("BuildPath", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trace, null);

    private static void VerifyPassages(RunTrajectory trace, Transform ball, Transform map)
    {
        var wall = new GameObject("Permanent wall");
        wall.transform.SetParent(map, false); wall.transform.localPosition = new Vector3(2, 0);
        var collider = wall.AddComponent<BoxCollider2D>(); collider.size = new Vector2(1, 2);
        ball.position = map.TransformPoint(Vector3.zero); trace.BeginRecording();
        foreach (var point in new[] { new Vector3(0, 2), new Vector3(4, 2), new Vector3(4, 0) })
        {
            ball.position = map.TransformPoint(point); Capture.Invoke(trace, null);
        }
        var aroundWall = BuildPath(trace);
        Require(aroundWall.Count >= 3, "A pathway must keep the bend around a permanent wall");
        var canConnect = typeof(RunTrajectory).GetMethod("CanConnect", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 1; i < aroundWall.Count; i++)
            Require((bool)canConnect.Invoke(trace, new object[] { aroundWall[i - 1], aroundWall[i] }), "A pathway segment cuts through a wall");
        collider.enabled = false;

        ball.position = map.TransformPoint(Vector3.zero); trace.BeginRecording();
        ball.position = map.TransformPoint(new Vector3(2, 2)); trace.MarkWaypoint();
        ball.position = map.TransformPoint(new Vector3(4, 0)); Capture.Invoke(trace, null);
        var pickupPath = BuildPath(trace);
        Require(pickupPath.Count == 3 && Vector3.Distance(pickupPath[1], new Vector3(2, 2)) < .001f,
            "A star pickup detour must survive pathway simplification");

        ball.position = map.TransformPoint(Vector3.zero); trace.BeginRecording();
        ball.position = map.TransformPoint(new Vector3(2, 2)); Capture.Invoke(trace, null);
        ball.position = map.TransformPoint(Vector3.zero); trace.ShowCompletedRoute();
        Require(trace.RevealComplete && !trace.IsVisible, "A collapsed local loop must not create a broken or phantom line");
        trace.Clear();
        Debug.Log("PATHWAY_WALL_AND_PICKUP_CHECKS_SUCCESS");
    }

    // A scripted route supplies a repeatable render fixture, not a claimed player run.
    internal static void SeedRenderFixture(GameManager game)
    {
        var flip = UnityEngine.Object.FindAnyObjectByType<MapFlipController>();
        flip.Stop();
        var body = flip.PlayerTransform.GetComponent<Rigidbody2D>();
        body.simulated = false;
        var trace = game.GetComponent<RunTrajectory>();
        trace.BeginRecording();
        Vector3 previous = flip.MapRoot.InverseTransformPoint(flip.PlayerTransform.position);
        Vector3[] anchors = {
            new Vector3(-11.5f, -5.8f), new Vector3(-8, -1.6f), new Vector3(-6, -1.8f),
            new Vector3(-3, 1.1f), new Vector3(-1.6f, 1.5f), new Vector3(-2, 7.2f),
            new Vector3(1, 7.7f), new Vector3(7.7f, 4.5f), new Vector3(12.8f, 4.4f),
            new Vector3(13, .1f), new Vector3(9, -2.8f), new Vector3(7.1f, -2.8f),
            new Vector3(9.8f, -2.7f), flip.MapRoot.InverseTransformPoint(UnityEngine.Object.FindAnyObjectByType<ExitTrigger>().transform.position)
        };
        foreach (Vector3 end in anchors)
        {
            float arc = (end.x == -8 || end.x == -3) ? 3.2f : end.x == 9 ? 2f : .65f;
            for (int i = 1; i <= 30; i++)
            {
                float t = i / 30f;
                Vector3 local = Vector3.Lerp(previous, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arc;
                flip.PlayerTransform.position = flip.MapRoot.TransformPoint(local);
                Capture.Invoke(trace, null);
            }
            previous = end;
            if (end.x == -8 || end.x == 7.7f) trace.MarkWaypoint();
        }
        body.position = flip.PlayerTransform.position;
        foreach (var star in UnityEngine.Object.FindObjectsByType<StarCollectible>(FindObjectsSortMode.None)) star.gameObject.SetActive(false);
    }

    internal static void VerifyOverview(CameraFollow follow, float aspect)
    {
        Camera camera = Camera.main;
        camera.aspect = aspect;
        follow.SendMessage("LateUpdate");
        Bounds bounds = GameObject.Find("Floor").GetComponent<SpriteRenderer>().bounds;
        Vector3 min = camera.WorldToViewportPoint(bounds.min), max = camera.WorldToViewportPoint(bounds.max);
        Require(min.x >= -.001f && max.x <= 1.001f && min.y >= .16f && max.y <= .88f,
            "Completion overview overlaps the HUD/footer or crops the maze at aspect " + aspect);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
