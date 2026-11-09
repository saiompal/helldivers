using System;
using System.Collections.Generic;
using UnityEngine;

// String-pull the recorded route through visible passages, one pickup leg at a time.
// It describes where the player went rather than reproducing airborne motion.
public static class RunPathSimplifier
{
    public static List<Vector3> Build(IReadOnlyList<Vector3> samples, IReadOnlyList<int> stops,
        Func<Vector3, Vector3, bool> canConnect)
    {
        var result = new List<Vector3>();
        if (samples.Count == 0) return result;
        result.Add(samples[0]);
        int start = 0;
        foreach (int stop in stops)
        {
            if (stop <= start || stop >= samples.Count) continue;
            AppendLeg(samples, start, stop, canConnect, result);
            start = stop;
        }
        if (start < samples.Count - 1) AppendLeg(samples, start, samples.Count - 1, canConnect, result);
        return result;
    }

    private static void AppendLeg(IReadOnlyList<Vector3> samples, int start, int end,
        Func<Vector3, Vector3, bool> canConnect, List<Vector3> result)
    {
        // A forward pass reduces dense recordings without quadratic physics queries.
        var corners = new List<Vector3> { samples[start] };
        int anchor = start;
        while (anchor < end)
        {
            int candidate = anchor + 1;
            while (candidate <= end && canConnect(samples[anchor], samples[candidate])) candidate++;
            // Historical movement can overlap a platform's final position. In that
            // case keep the recorded segment rather than inventing a different route.
            anchor = Mathf.Max(anchor + 1, candidate - 1);
            corners.Add(samples[anchor]);
        }
        // Pull the remaining corners tight; small jumps/loops in an open passage vanish.
        int current = 0;
        while (current < corners.Count - 1)
        {
            int next = corners.Count - 1;
            while (next > current + 1 && !canConnect(corners[current], corners[next])) next--;
            Vector3 point = corners[next];
            if ((point - result[result.Count - 1]).sqrMagnitude > .000001f) result.Add(point);
            current = next;
        }
    }
}
