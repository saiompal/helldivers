using UnityEditor;
using UnityEngine;

public static class ProgressionEditor
{
    [MenuItem("Tools/Progression/Reset Local Progress")]
    private static void ResetLocalProgress()
    {
        PlayerProgress.ResetLocalProgress();
        Debug.Log("Player progression reset on this machine.");
    }
}
