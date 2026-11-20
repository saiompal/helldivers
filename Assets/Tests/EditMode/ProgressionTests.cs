using NUnit.Framework;

public class ProgressionTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerProgress.UseTestStorage(true);
        PlayerProgress.ResetLocalProgress();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerProgress.ResetLocalProgress();
        PlayerProgress.UseTestStorage(false);
    }

    [Test]
    public void FreshSave_OnlyFirstLevelIsUnlocked_AndStartsWithNoLight()
    {
        Assert.That(PlayerProgress.StartingLightCharges, Is.Zero);
        Assert.That(PlayerProgress.IsLevelUnlocked(0), Is.True);
        for (int i = 1; i < LevelCatalog.Count; i++)
            Assert.That(PlayerProgress.IsLevelUnlocked(i), Is.False);
    }

    [Test]
    public void CompletingLevel_UnlocksNext_AndReplayDoesNotStackReward()
    {
        PlayerProgress.CompleteTutorial();
        LevelCompletionResult first = PlayerProgress.CompleteLevel(0);
        Assert.That(first.FirstCompletion, Is.True);
        Assert.That(first.UnlockedNextLevel, Is.True);
        Assert.That(first.UnlockedStartingCharge, Is.True);
        Assert.That(first.StartingLightCharges, Is.EqualTo(2));
        Assert.That(PlayerProgress.IsLevelUnlocked(1), Is.True);

        LevelCompletionResult replay = PlayerProgress.CompleteLevel(0);
        Assert.That(replay.FirstCompletion, Is.False);
        Assert.That(replay.UnlockedNextLevel, Is.False);
        Assert.That(replay.UnlockedStartingCharge, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(2));
    }

    [Test]
    public void LevelsUnlockSequentiallyThroughTheEntireCatalog()
    {
        for (int completed = 0; completed < LevelCatalog.Count - 1; completed++)
        {
            for (int locked = completed + 1; locked < LevelCatalog.Count; locked++)
                Assert.That(PlayerProgress.IsLevelUnlocked(locked), Is.False);

            PlayerProgress.CompleteLevel(completed);
            Assert.That(PlayerProgress.IsLevelUnlocked(completed + 1), Is.True);
        }
    }

    [Test]
    public void OnlyTutorialAndDisplayedLevelsOneAndThree_GrantStartingCharges()
    {
        Assert.That(PlayerProgress.CompleteTutorial().StartingLightCharges, Is.EqualTo(1));
        Assert.That(PlayerProgress.CompleteLevel(0).StartingLightCharges, Is.EqualTo(2));
        Assert.That(PlayerProgress.CompleteLevel(1).UnlockedStartingCharge, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(2));
        Assert.That(PlayerProgress.CompleteLevel(2).StartingLightCharges, Is.EqualTo(3));
        for (int i = 3; i < LevelCatalog.Count; i++)
            Assert.That(PlayerProgress.CompleteLevel(i).UnlockedStartingCharge, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(3));
        Assert.That(PlayerProgress.MaxStoredStartingCharges, Is.EqualTo(3));
    }

    [Test]
    public void StoredStartingChargesAreClampedToThree()
    {
        Assert.That(LightChargeRules.CapStoredStartingCharges(-2), Is.Zero);
        Assert.That(LightChargeRules.CapStoredStartingCharges(3), Is.EqualTo(3));
        Assert.That(LightChargeRules.CapStoredStartingCharges(5), Is.EqualTo(3));
        Assert.That(LightChargeRules.CapStoredStartingCharges(9), Is.EqualTo(3));
    }

    [Test]
    public void CompletionPersistsAfterRuntimeCacheReload()
    {
        PlayerProgress.CompleteLevel(0);
        PlayerProgress.ReloadFromPlayerPrefs();

        Assert.That(PlayerProgress.IsLevelCompleted(0), Is.True);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(1));
    }

    [Test]
    public void CatalogMapsScenesToPlayerFacingNumbers()
    {
        string[] scenes = { "Level0", "ShadowLevel", "Level2", "LinkedMaps", "Level4", "ShadowLevel 2" };
        Assert.That(LevelCatalog.Count, Is.EqualTo(scenes.Length));
        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            Assert.That(LevelCatalog.GetSceneName(i), Is.EqualTo(scenes[i]));
            Assert.That(LevelCatalog.GetDisplayNumber(i), Is.EqualTo(i + 1));
            Assert.That(LevelCatalog.TryGetLevelIndex(scenes[i], out int found), Is.True);
            Assert.That(found, Is.EqualTo(i));
        }
    }

    [Test]
    public void CampaignUnlocksLinkedMapsAndFinalShadowInOrder()
    {
        PlayerProgress.CompleteTutorial();
        for (int i = 0; i < 3; i++) PlayerProgress.CompleteLevel(i);
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsLevelUnlocked(3), Is.True);
        Assert.That(PlayerProgress.IsLevelUnlocked(4), Is.False);
        PlayerProgress.CompleteLevel(3);
        PlayerProgress.CompleteLevel(4);
        Assert.That(PlayerProgress.IsLevelUnlocked(5), Is.True);
        Assert.That(PlayerProgress.CompleteLevel(5).UnlockedNextLevel, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(3));
    }

    [Test]
    public void OriginalSaveMigration_PreservesOnlyUnchangedLevelCompletions()
    {
        UnityEngine.PlayerPrefs.SetInt("hat_hop.ui_tests.completed_mask", 31);
        UnityEngine.PlayerPrefs.DeleteKey("hat_hop.ui_tests.catalog_version");
        PlayerProgress.ReloadFromPlayerPrefs();
        for (int i = 0; i < LevelCatalog.Count; i++)
            Assert.That(PlayerProgress.IsLevelCompleted(i), Is.EqualTo(i == 0 || i == 2 || i == 4));
        Assert.That(PlayerProgress.IsTutorialCompleted, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(2));
    }

    [Test]
    public void ImportedFinnSaveMigration_RemapsBothMaps_AndRunsOnlyOnce()
    {
        UnityEngine.PlayerPrefs.SetInt("hat_hop.ui_tests.completed_mask", (1 << 5) | (1 << 6) | (1 << 7));
        UnityEngine.PlayerPrefs.DeleteKey("hat_hop.ui_tests.catalog_version");
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsLevelCompleted(1), Is.True);
        Assert.That(PlayerProgress.IsLevelCompleted(5), Is.True);
        Assert.That(PlayerProgress.IsLevelCompleted(3), Is.False);
        PlayerProgress.CompleteLevel(3);
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsLevelCompleted(3), Is.True);
    }

    [Test]
    public void StarRewardsCanExceedFiveLiveCharges()
    {
        Assert.That(LightChargeRules.AddStarReward(3), Is.EqualTo(4));
        Assert.That(LightChargeRules.AddStarReward(5), Is.EqualTo(6));
        Assert.That(LightChargeRules.AddStarReward(9), Is.EqualTo(10));
    }

    [Test]
    public void TutorialRewardPersists_AndReplayDoesNotGrantAnotherCharge()
    {
        var first = PlayerProgress.CompleteTutorial();
        Assert.That(first.FirstCompletion && first.UnlockedStartingCharge, Is.True);
        Assert.That(first.UnlockedNextLevel, Is.False);
        Assert.That(first.StartingLightCharges, Is.EqualTo(1));
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsTutorialCompleted, Is.True);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(1));
        var replay = PlayerProgress.CompleteTutorial();
        Assert.That(replay.FirstCompletion || replay.UnlockedStartingCharge, Is.False);
        Assert.That(replay.StartingLightCharges, Is.EqualTo(1));
    }

    [Test]
    public void LaterLevelsCannotReplaceTheTutorialReward()
    {
        for (int i = 0; i < LevelCatalog.Count; i++) PlayerProgress.CompleteLevel(i);
        Assert.That(PlayerProgress.IsTutorialCompleted, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(2));
        Assert.That(PlayerProgress.CompleteTutorial().StartingLightCharges, Is.EqualTo(3));
    }

    [Test]
    public void ResetClearsTheTutorialCompletionAndItsStartingCharge()
    {
        PlayerProgress.CompleteTutorial();
        PlayerProgress.ResetLocalProgress();
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsTutorialCompleted, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.Zero);
    }

    [Test]
    public void CompletionIsIndependentOfLegacyTimeRecords()
    {
        UnityEngine.PlayerPrefs.SetFloat("hat_hop.ui_tests.best_seconds.0", 120f);
        UnityEngine.PlayerPrefs.SetFloat("hat_hop.ui_tests.best_seconds.1", 5f);
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsLevelCompleted(0), Is.False);
        Assert.That(PlayerProgress.IsLevelCompleted(1), Is.False);

        PlayerProgress.CompleteLevel(0);
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsLevelCompleted(0), Is.True);
        Assert.That(PlayerProgress.IsLevelCompleted(1), Is.False);
    }

    [Test]
    public void ReplayPreservesCompletionWithoutDuplicatingRewards()
    {
        PlayerProgress.CompleteLevel(0);
        PlayerProgress.CompleteLevel(0);
        PlayerProgress.ReloadFromPlayerPrefs();
        Assert.That(PlayerProgress.IsLevelCompleted(0), Is.True);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(1));
    }
}
