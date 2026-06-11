using NUnit.Framework;

[TestFixture]
public class QuestTrackerLogicTests
{
    [Test]
    public void ResolveStepPhase_WhenCleared_AllStepsCompleted()
    {
        Assert.AreEqual(QuestStepPhase.Completed, QuestTrackerLogic.ResolveStepPhase(0, -1, true, 3));
        Assert.AreEqual(QuestStepPhase.Completed, QuestTrackerLogic.ResolveStepPhase(2, -1, true, 3));
    }

    [Test]
    public void ResolveStepPhase_WhenInProgress_MarksSingleActiveStep()
    {
        Assert.AreEqual(QuestStepPhase.Completed, QuestTrackerLogic.ResolveStepPhase(0, 1, false, 3));
        Assert.AreEqual(QuestStepPhase.Active, QuestTrackerLogic.ResolveStepPhase(1, 1, false, 3));
        Assert.AreEqual(QuestStepPhase.Pending, QuestTrackerLogic.ResolveStepPhase(2, 1, false, 3));
    }

    [Test]
    public void CountActiveSteps_WhenCleared_ReturnsZero()
    {
        Assert.AreEqual(0, QuestTrackerLogic.CountActiveSteps(-1, true, 3, hasQuest: true));
    }

    [Test]
    public void CountActiveSteps_WhenInProgress_ReturnsOne()
    {
        Assert.AreEqual(1, QuestTrackerLogic.CountActiveSteps(0, false, 3, hasQuest: true));
    }

    [Test]
    public void CountActiveSteps_WhenNoQuest_ReturnsZero()
    {
        Assert.AreEqual(0, QuestTrackerLogic.CountActiveSteps(0, false, 3, hasQuest: false));
    }
}
