using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class QuestTrackerStateTests
{
    static QuestDefinition CreateLightQuest()
    {
        return new QuestDefinition(
            "light_the_manor",
            "저택에 불을 밝혀라",
            "불이 없으면 핏자국이 보이지 않는다…",
            new[]
            {
                new QuestStep("go_kitchen", "주방으로 이동한다"),
                new QuestStep("raise_breaker", "다용도실 차단기를 올린다"),
                new QuestStep("inspect_hall", "불이 켜진 복도를 살핀다"),
            });
    }

    static QuestDefinition CreateBottleQuest()
    {
        return new QuestDefinition(
            "bottle_key",
            "병 속 열쇠를 꺼내라",
            "물이 차오르자 열쇠가 떠오른다.",
            new[]
            {
                new QuestStep("find_bottle", "화분 속 병을 발견한다"),
                new QuestStep("fill_bottle", "싱크대에서 병에 물을 채운다"),
                new QuestStep("take_key", "떠오른 열쇠를 집는다"),
            });
    }

    QuestTrackerState tracker;

    [SetUp]
    public void SetUp()
    {
        tracker = new QuestTrackerState(new[] { CreateLightQuest(), CreateBottleQuest() });
    }

    [Test]
    public void TrySetCurrentQuest_ValidId_ActivatesFirstStepOnly()
    {
        Assert.IsTrue(tracker.TrySetCurrentQuest("light_the_manor"));
        Assert.AreEqual("light_the_manor", tracker.CurrentQuestId);
        Assert.AreEqual(0, tracker.CurrentStepIndex);
        Assert.IsFalse(tracker.IsQuestCleared);
        Assert.AreEqual(1, tracker.CountActiveSteps());
        Assert.AreEqual(QuestStepPhase.Active, tracker.GetStepPhase(0));
        Assert.AreEqual(QuestStepPhase.Pending, tracker.GetStepPhase(1));
        Assert.AreEqual(QuestStepPhase.Pending, tracker.GetStepPhase(2));
    }

    [Test]
    public void TrySetCurrentQuest_InvalidId_ReturnsFalse()
    {
        Assert.IsFalse(tracker.TrySetCurrentQuest("missing_quest"));
        Assert.IsNull(tracker.CurrentQuestId);
        Assert.AreEqual(-1, tracker.CurrentStepIndex);
        Assert.AreEqual(0, tracker.CountActiveSteps());
    }

    [Test]
    public void TrySetCurrentQuest_EmptySteps_ReturnsFalse()
    {
        var emptyQuest = new QuestDefinition("empty", "빈 퀘스트", "힌트", System.Array.Empty<QuestStep>());
        var emptyTracker = new QuestTrackerState(new[] { emptyQuest });

        Assert.IsFalse(emptyTracker.TrySetCurrentQuest("empty"));
        Assert.IsNull(emptyTracker.CurrentQuestId);
    }

    [Test]
    public void AdvanceStep_MovesActiveToNextStep()
    {
        tracker.TrySetCurrentQuest("light_the_manor");

        Assert.IsTrue(tracker.AdvanceStep());
        Assert.AreEqual(1, tracker.CurrentStepIndex);
        Assert.IsTrue(tracker.IsStepCompleted("go_kitchen"));
        Assert.AreEqual(QuestStepPhase.Completed, tracker.GetStepPhase(0));
        Assert.AreEqual(QuestStepPhase.Active, tracker.GetStepPhase(1));
        Assert.AreEqual(1, tracker.CountActiveSteps());
    }

    [Test]
    public void AdvanceStep_OnLastStep_SetsQuestCleared()
    {
        tracker.TrySetCurrentQuest("light_the_manor");
        Assert.IsTrue(tracker.AdvanceStep());
        Assert.IsTrue(tracker.AdvanceStep());
        Assert.IsTrue(tracker.AdvanceStep());

        Assert.IsTrue(tracker.IsQuestCleared);
        Assert.AreEqual(-1, tracker.CurrentStepIndex);
        Assert.AreEqual(0, tracker.CountActiveSteps());
        Assert.AreEqual(QuestStepPhase.Completed, tracker.GetStepPhase(2));
        Assert.IsTrue(tracker.CompletedStepIds.SequenceEqual(new[]
        {
            "go_kitchen",
            "raise_breaker",
            "inspect_hall",
        }));
    }

    [Test]
    public void AdvanceStep_WhenCleared_ReturnsFalse()
    {
        tracker.TrySetCurrentQuest("light_the_manor");
        tracker.AdvanceStep();
        tracker.AdvanceStep();
        tracker.AdvanceStep();

        Assert.IsFalse(tracker.AdvanceStep());
    }

    [Test]
    public void AdvanceStep_WhenNoQuest_ReturnsFalse()
    {
        Assert.IsFalse(tracker.AdvanceStep());
    }

    [Test]
    public void CompleteStep_WithMatchingStepId_Advances()
    {
        tracker.TrySetCurrentQuest("light_the_manor");

        Assert.IsTrue(tracker.CompleteStep("go_kitchen"));
        Assert.AreEqual(1, tracker.CurrentStepIndex);
        Assert.IsTrue(tracker.IsStepCompleted("go_kitchen"));
    }

    [Test]
    public void CompleteStep_WithWrongStepId_ReturnsFalse()
    {
        tracker.TrySetCurrentQuest("light_the_manor");

        Assert.IsFalse(tracker.CompleteStep("raise_breaker"));
        Assert.AreEqual(0, tracker.CurrentStepIndex);
        Assert.IsFalse(tracker.IsStepCompleted("raise_breaker"));
    }

    [Test]
    public void CompleteStep_WithInvalidStepId_ReturnsFalse()
    {
        tracker.TrySetCurrentQuest("light_the_manor");

        Assert.IsFalse(tracker.CompleteStep("not_a_step"));
    }

    [Test]
    public void GetStepPhase_WhenInProgress_HasExactlyOneActive()
    {
        tracker.TrySetCurrentQuest("bottle_key");
        tracker.AdvanceStep();

        int activeCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (tracker.GetStepPhase(i) == QuestStepPhase.Active)
                activeCount++;
        }

        Assert.AreEqual(1, activeCount);
    }

    [Test]
    public void GetStepPhase_WhenCleared_HasNoActive()
    {
        tracker.TrySetCurrentQuest("bottle_key");
        tracker.AdvanceStep();
        tracker.AdvanceStep();
        tracker.AdvanceStep();

        Assert.AreEqual(0, tracker.CountActiveSteps());
    }

    [Test]
    public void TrySetCurrentQuest_AfterCleared_LoadsNextQuestFresh()
    {
        tracker.TrySetCurrentQuest("light_the_manor");
        tracker.AdvanceStep();
        tracker.AdvanceStep();
        tracker.AdvanceStep();

        Assert.IsTrue(tracker.TrySetCurrentQuest("bottle_key"));
        Assert.AreEqual("bottle_key", tracker.CurrentQuestId);
        Assert.AreEqual(0, tracker.CurrentStepIndex);
        Assert.IsFalse(tracker.IsQuestCleared);
        Assert.IsFalse(tracker.IsStepCompleted("go_kitchen"));
        Assert.AreEqual(1, tracker.CountActiveSteps());
    }

    [Test]
    public void TrySetCurrentQuest_ResetsCompletedStepsFromPreviousQuest()
    {
        tracker.TrySetCurrentQuest("light_the_manor");
        tracker.AdvanceStep();

        tracker.TrySetCurrentQuest("bottle_key");

        Assert.IsEmpty(tracker.CompletedStepIds);
    }

    [Test]
    public void GetStepPhase_InvalidIndex_ReturnsPending()
    {
        tracker.TrySetCurrentQuest("light_the_manor");

        Assert.AreEqual(QuestStepPhase.Pending, tracker.GetStepPhase(-1));
        Assert.AreEqual(QuestStepPhase.Pending, tracker.GetStepPhase(99));
    }
}
