using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time migration for removing only the art added by the reference-UI pass.
// Restore individual renderer properties, never entire scenes or level geometry.
public static class OriginalArtRestorer
{
    public static void RestoreAll()
    {
        int restored = 0;
        foreach (string name in new[] { "Tutorial", "Level0", "Level1", "Level2", "Level3", "Level4" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            string original = ReadOriginal(path);
            var names = new Dictionary<string, string>();
            var colors = new Dictionary<string, Color>();
            var blocks = Regex.Matches(original, @"(?ms)^--- !u!(\d+) &(-?\d+)\r?\n(.*?)(?=^--- !u!|\z)");
            foreach (Match block in blocks)
                if (block.Groups[1].Value == "1")
                    names[block.Groups[2].Value] = Regex.Match(block.Groups[3].Value, @"(?m)^  m_Name: (.*)").Groups[1].Value.Trim();
            foreach (Match block in blocks)
            {
                if (block.Groups[1].Value != "212") continue;
                string objectId = Regex.Match(block.Groups[3].Value, @"m_GameObject: \{fileID: (-?\d+)").Groups[1].Value;
                if (names.TryGetValue(objectId, out string objectName))
                    colors[objectName] = ParseColor(Regex.Match(block.Groups[3].Value, @"m_Color: (\{[^}]+\})").Groups[1].Value);
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var floor = GameObject.Find("Floor").GetComponent<SpriteRenderer>();
            Sprite square = floor.sprite;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (renderer == null) continue;
                if (renderer.name.StartsWith("Reference ")) continue;
                Transform surface = renderer.transform.Find("Reference Surface");
                Transform halo = renderer.transform.Find("Reference Pickup Glow");
                bool changed = surface != null || halo != null ||
                    AssetDatabase.GetAssetPath(renderer.sprite) == "Assets/Resources/ReferenceSurface.png";
                if (!changed && renderer != floor) continue;
                if (surface != null) UnityEngine.Object.DestroyImmediate(surface.gameObject);
                if (halo != null) UnityEngine.Object.DestroyImmediate(halo.gameObject);
                if (surface != null || AssetDatabase.GetAssetPath(renderer.sprite) == "Assets/Resources/ReferenceSurface.png") renderer.sprite = square;
                if (colors.TryGetValue(renderer.name, out Color color)) renderer.color = color;
                else
                {
                    Color wall = colors.TryGetValue("Wall", out Color baseline) ? baseline : new Color(.8301887f, .8301887f, .8301887f);
                    renderer.color = renderer.GetComponent<SurfaceEffector2D>() != null
                        ? Color.Lerp(wall, new Color(.56f, .67f, .71f), .45f) : wall;
                }
                restored++;
            }
            var cameraColor = Regex.Match(original, @"m_BackGroundColor: (\{[^}]+\})");
            if (Camera.main != null && cameraColor.Success) Camera.main.backgroundColor = ParseColor(cameraColor.Groups[1].Value);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        UnityEngine.Debug.Log("ORIGINAL_ART_RESTORED: " + restored + " original renderers; generated rims and halos removed.");
        string backup = "UIReview/RevertedArt";
        System.IO.Directory.CreateDirectory(backup);
        foreach (string name in new[] { "ReferenceSurface.png", "ReferenceGlow.png" })
        foreach (string suffix in new[] { "", ".meta" })
        {
            string source = "Assets/Resources/" + name + suffix;
            if (!System.IO.File.Exists(source)) continue;
            string destination = backup + "/" + name + suffix;
            if (System.IO.File.Exists(destination)) destination += "." + Guid.NewGuid().ToString("N");
            System.IO.File.Move(source, destination);
        }
        AssetDatabase.Refresh();
    }
    private static string ReadOriginal(string path)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            Arguments = "-c \"safe.directory=" + System.IO.Directory.GetCurrentDirectory().Replace('\\', '/') + "\" show \"HEAD:" + path + "\""
        };
        using (var process = Process.Start(info))
        {
            string text = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Cannot read original art for " + path + ": " + error);
            return text;
        }
    }
    private static Color ParseColor(string text)
    {
        var parts = Regex.Matches(text, @"[rgba]: ([^,}]+)");
        if (parts.Count != 4) throw new InvalidOperationException("Invalid original color: " + text);
        return new Color(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]), Parse(parts[3]));
    }
    private static float Parse(Match match) => float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
}
