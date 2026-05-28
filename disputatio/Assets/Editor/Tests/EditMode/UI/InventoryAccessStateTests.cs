using NUnit.Framework;

public class InventoryAccessStateTests
{
    [Test]
    public void ShouldUnlockAfterRetry_ReturnsTrue_ForHallPlayableRetryAfterDeath()
    {
        Assert.IsTrue(InventoryAccessState.ShouldUnlockAfterRetry(SceneNames.HallPlayable, true));
    }

    [Test]
    public void ShouldUnlockAfterRetry_ReturnsTrue_WhenRetryTargetIsHallPlayable()
    {
        Assert.IsTrue(InventoryAccessState.ShouldUnlockAfterRetry("Kitchen", "Hall_playerble", true));
    }

    [Test]
    public void ShouldUnlockAfterRetry_ReturnsTrue_ForCorrectedHallPlayableSpelling()
    {
        Assert.IsTrue(InventoryAccessState.ShouldUnlockAfterRetry("Kitchen", "Hall_playable", true));
    }

    [Test]
    public void ShouldUnlockAfterRetry_ReturnsFalse_ForHallPlayableRetryBeforeDeath()
    {
        Assert.IsFalse(InventoryAccessState.ShouldUnlockAfterRetry(SceneNames.HallPlayable, false));
    }

    [Test]
    public void ShouldUnlockAfterRetry_ReturnsFalse_ForOtherSceneRetry()
    {
        Assert.IsFalse(InventoryAccessState.ShouldUnlockAfterRetry(SceneNames.Kitchen, true));
        Assert.IsFalse(InventoryAccessState.ShouldUnlockAfterRetry(SceneNames.Kitchen, SceneNames.Kitchen, true));
    }

    [Test]
    public void ShouldAllowInventoryInput_ReturnsOnlyWhenUnlocked()
    {
        Assert.IsFalse(InventoryAccessState.ShouldAllowInventoryInput(false));
        Assert.IsTrue(InventoryAccessState.ShouldAllowInventoryInput(true));
    }
}
