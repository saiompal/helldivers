using System;
using UnityEngine;

public readonly struct LevelCompletionResult
{
    public bool FirstCompletion { get; }
    public bool UnlockedNextLevel { get; }
    public bool UnlockedStartingCharge { get; }
    public int StartingLightCharges { get; }

    public LevelCompletionResult(bool firstCompletion, bool unlockedNextLevel,
        bool unlockedStartingCharge, int startingLightCharges)
    {
        FirstCompletion = firstCompletion;
        UnlockedNextLevel = unlockedNextLevel;
        UnlockedStartingCharge = unlockedStartingCharge;
        StartingLightCharges = startingLightCharges;
    }
}

public static class PlayerProgress
{
    private static string storagePrefix = "hat_hop.progress.v1.";
    private static string CompletedMaskKey => storagePrefix + "completed_mask";
    public const int MaxStoredStartingCharges = 5;
    private static int? completedMask;

    public static int StartingLightCharges
    {
        get
        {
            int count = 0;
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                if (LevelCatalog.IsChargeMilestone(i) && IsLevelCompleted(i))
                    count++;
            }
            return LightChargeRules.CapStoredStartingCharges(count);
        }
    }

    public static bool IsLevelCompleted(int levelIndex)
    {
        ValidateLevelIndex(levelIndex);
        return (CompletedMask & (1 << levelIndex)) != 0;
    }

    public static bool IsLevelUnlocked(int levelIndex)
    {
        ValidateLevelIndex(levelIndex);
        return levelIndex == 0 || IsLevelCompleted(levelIndex - 1);
    }

    public static LevelCompletionResult CompleteLevel(int levelIndex)
    {
        ValidateLevelIndex(levelIndex);
        int previousCharges = StartingLightCharges;
        bool firstCompletion = !IsLevelCompleted(levelIndex);
        if (firstCompletion)
        {
            CompletedMask |= 1 << levelIndex;
            PlayerPrefs.SetInt(CompletedMaskKey, CompletedMask);
            PlayerPrefs.Save();
        }

        int currentCharges = StartingLightCharges;
        return new LevelCompletionResult(firstCompletion,
            firstCompletion && levelIndex + 1 < LevelCatalog.Count,
            currentCharges > previousCharges, currentCharges);
    }

    public static void ResetLocalProgress()
    {
        completedMask = 0;
        PlayerPrefs.DeleteKey(CompletedMaskKey);
        // Also clean up records from the retired time-rating system on reset.
        for (int i = 0; i < LevelCatalog.Count; i++) PlayerPrefs.DeleteKey(storagePrefix + "best_seconds." + i);
        PlayerPrefs.Save();
    }

    public static void ReloadFromPlayerPrefs()
    {
        completedMask = null;
        _ = CompletedMask;
    }

#if UNITY_EDITOR
    public static void UseTestStorage(bool enabled)
    {
        storagePrefix = enabled ? "hat_hop.ui_tests." : "hat_hop.progress.v1.";
        completedMask = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureBatchReview()
    {
        if (Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(), "-uiReviewSmoke") >= 0)
        {
            UseTestStorage(true);
            ResetLocalProgress();
        }
    }
#endif

    private static int CompletedMask
    {
        get
        {
            if (!completedMask.HasValue)
                completedMask = PlayerPrefs.GetInt(CompletedMaskKey, 0);
            return completedMask.Value;
        }
        set => completedMask = value;
    }

    private static void ValidateLevelIndex(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= LevelCatalog.Count)
            throw new ArgumentOutOfRangeException(nameof(levelIndex));
    }
}
