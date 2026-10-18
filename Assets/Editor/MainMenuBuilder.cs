using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates the MainMenu scene and puts it first in the build settings.
// Run it from the menu: Tools > Main Menu > Build Main Menu Scene,
// or Tools > Build Alpha to rebuild the menu and Level 0 together.
public static class MainMenuBuilder
{
    private const string GameTitle = "IN THE DARK";
    private const string Tagline = "Your light is small. The world won't stay still.";
    private const string BuildLabel = "ALPHA PLAYTEST";

    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    // The menu shows one matching button for each level.
    private static readonly string[] LevelPaths =
    {
        Level0Builder.ScenePath,
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level2.unity",
        "Assets/Scenes/Level3.unity",
    };

    private static readonly string[] HowToPlay =
    {
        "Memorize the level before the lights go out",
        "In the dark you only see a small circle around you",
        "<b>F</b> lights the whole level for a moment, but charges are limited",
        "The world <b>flips</b> every few seconds. Your light pulses red before it does",
        "Gravity stays down, so a wall you can't climb may be one you can walk under",
        "The <color=#5CE68A>exit</color> stays locked until you collect every <color=#FFD54A>star</color>",
    };

    private static readonly Color BackgroundColor = new Color(0.03f, 0.03f, 0.04f);
    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.1f, 0.92f);
    private static readonly Color TitleColor = new Color(0.95f, 0.95f, 0.97f);
    private static readonly Color MutedColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color AccentColor = new Color(1f, 0.84f, 0.29f);
    private static readonly Color ButtonColor = new Color(0.92f, 0.93f, 0.95f);
    private static readonly Color ButtonTextColor = new Color(0.1f, 0.1f, 0.13f);

    private static Sprite roundedSprite;

    // Register all existing level scenes when this replacement is imported.
    // Preserve the project's other scene entries and input configuration.
    [InitializeOnLoadMethod]
    private static void RegisterLevelScenes()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                File.Exists(MenuScenePath) && LevelPaths.All(File.Exists))
                SetBuildOrder();
        };
    }

    [MenuItem("Tools/Build Alpha (Menu + Level 0)")]
    private static void BuildAlpha()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (Level0Builder.Build())
        {
            BuildMenu();
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("Tools/Main Menu/Build Main Menu Scene")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (File.Exists(MenuScenePath) &&
            !EditorUtility.DisplayDialog(
                "Rebuild Main Menu?",
                $"{MenuScenePath} already exists. Replace it with a fresh menu?",
                "Replace", "Cancel"))
        {
            return;
        }

        BuildMenu();
    }

    private static void BuildMenu()
    {
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.AddComponent<AudioListener>();

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(HudBuilder.ReferenceWidth, HudBuilder.ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        VerticalLayoutGroup layout = AddColumn(canvasObject, 28f);
        layout.childAlignment = TextAnchor.MiddleCenter;

        MainMenu menu = canvasObject.AddComponent<MainMenu>();

        TextMeshProUGUI title = AddText(canvasObject.transform, "Title", GameTitle, 112f, TitleColor);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;

        AddText(canvasObject.transform, "Tagline", Tagline, 32f, MutedColor);

        BuildHowToPlay(canvasObject.transform);

        GameObject levels = new GameObject("Levels", typeof(RectTransform));
        levels.transform.SetParent(canvasObject.transform, false);
        HorizontalLayoutGroup levelRow = levels.AddComponent<HorizontalLayoutGroup>();
        levelRow.spacing = 24f;
        levelRow.childAlignment = TextAnchor.MiddleCenter;
        levelRow.childControlWidth = true;
        levelRow.childControlHeight = true;
        levelRow.childForceExpandWidth = false;
        levelRow.childForceExpandHeight = false;
        Button play = null;
        for (int i = 0; i < LevelPaths.Length; i++)
        {
            Button level = CreateButton(levels.transform, $"Level {i}");
            UnityEventTools.AddStringPersistentListener(
                level.onClick, menu.LoadLevel,
                Path.GetFileNameWithoutExtension(LevelPaths[i]));
            if (play == null) play = level;
        }

        Button quit = CreateButton(canvasObject.transform, "Quit");
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        SerializedObject menuData = new SerializedObject(menu);
        menuData.FindProperty("quitButton").objectReferenceValue = quit.gameObject;
        menuData.ApplyModifiedPropertiesWithoutUndo();

        TextMeshProUGUI label = AddText(canvasObject.transform, "Build Label", BuildLabel, 18f, MutedColor);
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 8f;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>().firstSelectedGameObject = play.gameObject;
        eventSystem.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        SetBuildOrder();

        Debug.Log($"MainMenuBuilder: created {MenuScenePath} and set the build order.");
    }

    // Entry point for headless validation/regeneration after scene remapping.
    public static void BuildFromCommandLine()
    {
        BuildMenu();
    }

    private static void BuildHowToPlay(Transform parent)
    {
        GameObject panel = new GameObject("How To Play", typeof(RectTransform));
        panel.transform.SetParent(parent, false);

        Image background = panel.AddComponent<Image>();
        background.sprite = roundedSprite;
        background.type = Image.Type.Sliced;
        background.color = PanelColor;
        background.raycastTarget = false;

        VerticalLayoutGroup column = AddColumn(panel, 10f);
        column.padding = new RectOffset(48, 48, 28, 32);
        column.childAlignment = TextAnchor.UpperLeft;

        TextMeshProUGUI header = AddText(panel.transform, "Header", "HOW TO PLAY", 18f, AccentColor);
        header.fontStyle = FontStyles.Bold;
        header.characterSpacing = 8f;
        header.alignment = TextAlignmentOptions.Left;

        foreach (string line in HowToPlay)
        {
            TextMeshProUGUI text = AddText(panel.transform, "Line", $"\u2022  {line}", 26f, TitleColor);
            text.alignment = TextAlignmentOptions.Left;
        }

        TextMeshProUGUI controls = AddText(
            panel.transform, "Controls",
            "A / D  move   \u00B7   SPACE  jump   \u00B7   F  light   \u00B7   R  restart   \u00B7   ESC  menu",
            20f, MutedColor);
        controls.alignment = TextAlignmentOptions.Left;
    }

    private static VerticalLayoutGroup AddColumn(GameObject go, float spacing)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static TextMeshProUGUI AddText(
        Transform parent, string name, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    // A standard Unity UI button, styled like the HUD key caps.
    private static Button CreateButton(Transform parent, string label)
    {
        TMP_DefaultControls.Resources resources = new TMP_DefaultControls.Resources
        {
            standard = roundedSprite
        };

        GameObject buttonObject = TMP_DefaultControls.CreateButton(resources);
        buttonObject.name = $"{label} Button";
        buttonObject.transform.SetParent(parent, false);

        LayoutElement size = buttonObject.AddComponent<LayoutElement>();
        size.preferredWidth = 320f;
        size.preferredHeight = 70f;

        buttonObject.GetComponent<Image>().color = ButtonColor;

        // The selected button gets a gold tint so keyboard players can see it.
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = AccentColor;
        colors.selectedColor = AccentColor;
        button.colors = colors;

        TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 32f;
        text.fontStyle = FontStyles.Bold;
        text.color = ButtonTextColor;

        return button;
    }

    // MainMenu first, then the levels in order, then any other scenes
    // already listed. The empty SampleScene template and scenes that no
    // longer exist are left out, so N after the last level doesn't load a
    // blank or missing scene.
    private static void SetBuildOrder()
    {
        string[] pinned = new[] { MenuScenePath }.Concat(LevelPaths).ToArray();

        List<EditorBuildSettingsScene> scenes = pinned
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToList();

        scenes.AddRange(EditorBuildSettings.scenes.Where(s =>
            !pinned.Contains(s.path) &&
            File.Exists(s.path) &&
            !s.path.EndsWith("/SampleScene.unity")));

        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        bool changed = current.Length != scenes.Count ||
            scenes.Where((s, i) => i >= current.Length ||
                s.path != current[i].path || s.enabled != current[i].enabled).Any();
        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
