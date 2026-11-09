using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ProjectBuild
{
    [MenuItem("Tools/UI/Rebuild Menu and All HUDs")]
    public static void RebuildUiOnly()
    {
        MainMenuBuilder.BuildFromCommandLine();
        HudBuilder.RebuildAllHuds();
    }

    [MenuItem("Tools/Build/Rebuild UI and WebGL")]
    public static void RebuildUiAndWebGl()
    {
        RebuildUiOnly();
        PlayerSettings.WebGL.template = "PROJECT:Reference";

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "docs",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"WebGL build failed: {report.summary.result} " +
                $"({report.summary.totalErrors} errors).");

        Debug.Log($"WebGL build completed in {report.summary.totalTime}.");
    }
}
