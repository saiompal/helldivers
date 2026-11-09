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
        LevelCompletionResult first = PlayerProgress.CompleteLevel(0);
        Assert.That(first.FirstCompletion, Is.True);
        Assert.That(first.UnlockedNextLevel, Is.True);
        Assert.That(first.UnlockedStartingCharge, Is.True);
        Assert.That(first.StartingLightCharges, Is.EqualTo(1));
        Assert.That(PlayerProgress.IsLevelUnlocked(1), Is.True);

        LevelCompletionResult replay = PlayerProgress.CompleteLevel(0);
        Assert.That(replay.FirstCompletion, Is.False);
        Assert.That(replay.UnlockedNextLevel, Is.False);
        Assert.That(replay.UnlockedStartingCharge, Is.False);
        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(1));
    }

    [Test]
    public void LevelsUnlockSequentiallyThroughTheFiveLevelCatalog()
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
    public void DisplayedLevelsOneThreeAndFive_GrantStartingCharges()
    {
        for (int i = 0; i < LevelCatalog.Count; i++)
            PlayerProgress.CompleteLevel(i);

        Assert.That(PlayerProgress.StartingLightCharges, Is.EqualTo(3));
        Assert.That(PlayerProgress.MaxStoredStartingCharges, Is.EqualTo(5));
    }

    [Test]
    public void StoredStartingChargesAreClampedToFive()
    {
        Assert.That(LightChargeRules.CapStoredStartingCharges(-2), Is.Zero);
        Assert.That(LightChargeRules.CapStoredStartingCharges(3), Is.EqualTo(3));
        Assert.That(LightChargeRules.CapStoredStartingCharges(5), Is.EqualTo(5));
        Assert.That(LightChargeRules.CapStoredStartingCharges(9), Is.EqualTo(5));
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
        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            Assert.That(LevelCatalog.GetSceneName(i), Is.EqualTo($"Level{i}"));
            Assert.That(LevelCatalog.GetDisplayNumber(i), Is.EqualTo(i + 1));
            Assert.That(LevelCatalog.TryGetLevelIndex($"Level{i}", out int found), Is.True);
            Assert.That(found, Is.EqualTo(i));
        }
    }

    [Test]
    public void StarRewardsCanExceedFiveLiveCharges()
    {
        Assert.That(LightChargeRules.AddStarReward(5), Is.EqualTo(6));
        Assert.That(LightChargeRules.AddStarReward(9), Is.EqualTo(10));
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
