using System;

public static class LevelCatalog
{
    private static readonly string[] SceneNames =
    {
        "Level0", "ShadowLevel", "Level2", "LinkedMaps", "Level4", "ShadowLevel 2",
    };

    public static int Count => SceneNames.Length;

    public static string GetSceneName(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(levelIndex));
        return SceneNames[levelIndex];
    }

    public static int GetDisplayNumber(int levelIndex) => levelIndex + 1;

    public static bool IsChargeMilestone(int levelIndex) =>
        levelIndex == 0 || levelIndex == 2;

    public static bool TryGetLevelIndex(string sceneName, out int levelIndex)
    {
        for (int i = 0; i < SceneNames.Length; i++)
        {
            if (string.Equals(SceneNames[i], sceneName, StringComparison.Ordinal))
            {
                levelIndex = i;
                return true;
            }
        }
        levelIndex = -1;
        return false;
    }
}
