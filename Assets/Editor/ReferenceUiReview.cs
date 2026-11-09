using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Reproducible render review: Unity.exe -batchmode -projectPath ...
// -executeMethod ReferenceUiReview.RebuildAndCapture -quit (graphics enabled).
public static class ReferenceUiReview
{
    public static void RefreshUiAndVerify()
    {
        RebuildAndCapture();
        ReferenceUiSmoke.RunAndBuild();
    }
    public static void VerifyWithoutBuild()
    {
        RebuildAndCapture();
        ReferenceUiSmoke.Run();
    }
    public static void ReviseUiAndVerify()
    {
        OriginalArtRestorer.RestoreAll();
        RebuildAndCapture();
        ReferenceUiSmoke.RunAndBuild();
    }
    public static void BuildAndCaptureWebGl()
    {
        RebuildAndCapture();
        ProjectBuild.RebuildUiAndWebGl();
    }

    public static void RebuildAndCapture()
    {
        Directory.CreateDirectory("UIReview");
        MainMenuBuilder.BuildFromCommandLine();
        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
        ValidateGraphics();
        Capture("menu-home");
        menu.ShowHelp();
        Capture("menu-help");
        menu.HideHelp();
        menu.ShowLevelMap();
        Capture("level-map");
        menu.ShowHome();
        HudBuilder.RebuildAllHuds();
        EditorSceneManager.OpenScene("Assets/Scenes/Level0.unity");
        ValidateGraphics();
        var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var data = new SerializedObject(manager);
        (data.FindProperty("statusText").objectReferenceValue as TMP_Text).text = string.Empty;
        var hud = canvas.transform.Find(GameplayHudRuntime.RootName);
        var count = hud.Find("Light Charges/Count").GetComponent<TMP_Text>();
        GameplayHudRuntime.UpdateCharges(count, 2);
        var icon = hud.Find("Flip Timer/Hourglass").GetComponent<HourglassGraphic>();
        icon.SetState(.2f, 0);
        Capture("level-preview");
        GameplayHudRuntime.UpdateCharges(count, 7); Capture("hud-overflow");
        GameplayHudRuntime.UpdateCharges(count, 0);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        File.WriteAllText("UIReview/review.txt", "Rebuilt menu + six gameplay HUDs. All active graphics have CanvasRenderer components. Rendered home, map, Level0 preview and charge overflow.\n");
        Debug.Log("REFERENCE_UI_REVIEW_SUCCESS");
    }
    private static void ValidateGraphics()
    {
        foreach (var g in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g.GetComponent<CanvasRenderer>() == null) throw new InvalidOperationException("Missing renderer: " + g.name);
    }
    internal static void Capture(string name, int width = 1920, int height = 1080)
    {
        Camera camera = Camera.main;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var target = new RenderTexture(width, height, 24) { antiAliasing = 8 };
        target.Create(); camera.targetTexture = target;
        if (Application.isPlaying)
            foreach (var trace in UnityEngine.Object.FindObjectsByType<RunTrajectory>(FindObjectsSortMode.None))
                trace.SendMessage("LateUpdate"); // Match screen-pixel width to this capture's resolution.
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
        Canvas.ForceUpdateCanvases();
        foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) label.ForceMeshUpdate();
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes("UIReview/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }

}
