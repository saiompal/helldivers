using System;

public static class LevelCatalog
{
    private static readonly string[] SceneNames =
    {
        "Level0", "Level1", "Level2", "Level3", "Level4",
    };

    public const int Count = 5;

    public static string GetSceneName(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(levelIndex));
        return SceneNames[levelIndex];
    }

    public static int GetDisplayNumber(int levelIndex) => levelIndex + 1;

    public static bool IsChargeMilestone(int levelIndex) =>
        levelIndex == 0 || levelIndex == 2 || levelIndex == 4;

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
