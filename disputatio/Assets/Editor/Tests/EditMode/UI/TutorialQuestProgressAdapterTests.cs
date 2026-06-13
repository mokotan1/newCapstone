using System.Linq;
using Godlotto.Interaction;
using NUnit.Framework;

[TestFixture]
public class TutorialQuestProgressAdapterTests
{
    static QuestDefinition CreateLightQuest()
    {
        return new QuestDefinition(
            TutorialQuestIds.LightTheManor,
            "저택에 불을 밝혀라",
            "hint",
            new[]
            {
                new QuestStep(TutorialQuestIds.LightTheManorSteps.GoKitchen, "주방"),
                new QuestStep(TutorialQuestIds.LightTheManorSteps.RaiseBreaker, "차단기"),
                new QuestStep(TutorialQuestIds.LightTheManorSteps.InspectHall, "복도"),
            });
    }

    static QuestDefinition CreateBottleQuest()
    {
        return new QuestDefinition(
            TutorialQuestIds.BottleKey,
            "병 속 열쇠",
            "hint",
            new[]
            {
                new QuestStep(TutorialQuestIds.BottleKeySteps.FindBottle, "병"),
                new QuestStep(TutorialQuestIds.BottleKeySteps.FillBottle, "물"),
                new QuestStep(TutorialQuestIds.BottleKeySteps.TakeKey, "열쇠"),
            });
    }

    QuestTrackerState tracker;

    [SetUp]
    public void SetUp()
    {
        tracker = new QuestTrackerState(new[] { CreateLightQuest(), CreateBottleQuest() });
    }

    [Test]
    public void ResolveInitialQuestId_ReturnsLightQuestOnFreshRun()
    {
        var flags = new TutorialQuestWorldFlags(SceneNames.OpeningOffice, false, false, false, false);

        Assert.AreEqual(TutorialQuestIds.LightTheManor, TutorialQuestProgressAdapter.ResolveInitialQuestId(flags));
    }

    [Test]
    public void ResolveInitialQuestId_ReturnsBottleQuestWhenBottleProgressExists()
    {
        var flags = new TutorialQuestWorldFlags(SceneNames.Kitchen, true, true, false, false);

        Assert.AreEqual(TutorialQuestIds.BottleKey, TutorialQuestProgressAdapter.ResolveInitialQuestId(flags));
    }

    [Test]
    public void TryMapSceneEntryToStepId_MapsKitchenForLightQuest()
    {
        var flags = new TutorialQuestWorldFlags(SceneNames.Kitchen, false, false, false, false);

        Assert.IsTrue(TutorialQuestProgressAdapter.TryMapSceneEntryToStepId(
            TutorialQuestIds.LightTheManor,
            SceneNames.Kitchen,
            flags,
            out string stepId));
        Assert.AreEqual(TutorialQuestIds.LightTheManorSteps.GoKitchen, stepId);
    }

    [Test]
    public void TryMapFungusFlagEdgeToStepId_MapsElectricOnForLightQuest()
    {
        Assert.IsTrue(TutorialQuestProgressAdapter.TryMapFungusFlagEdgeToStepId(
            TutorialQuestIds.LightTheManor,
            FungusVariableKeys.ElectricOn,
            true,
            out string stepId));
        Assert.AreEqual(TutorialQuestIds.LightTheManorSteps.RaiseBreaker, stepId);
    }

    [Test]
    public void TryMapKitchenBlockToStepId_MapsFaucetAndDraggedBlocks()
    {
        Assert.IsTrue(TutorialQuestProgressAdapter.TryMapKitchenBlockToStepId(
            KitchenSinkInteractionGate.FaucetBlockName,
            out string faucetStep));
        Assert.AreEqual(TutorialQuestIds.BottleKeySteps.FillBottle, faucetStep);

        Assert.IsTrue(TutorialQuestProgressAdapter.TryMapKitchenBlockToStepId(
            KitchenSinkInteractionGate.BottleDraggedBlockName,
            out string dragStep));
        Assert.AreEqual(TutorialQuestIds.BottleKeySteps.TakeKey, dragStep);
    }

    [Test]
    public void ResolveCatchUpStepIds_CompletesAlreadySatisfiedSteps()
    {
        tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor);
        var flags = new TutorialQuestWorldFlags(SceneNames.Kitchen, true, false, false, false);

        var completed = TutorialQuestProgressAdapter.ResolveCatchUpStepIds(tracker, flags);

        CollectionAssert.AreEqual(
            new[]
            {
                TutorialQuestIds.LightTheManorSteps.GoKitchen,
                TutorialQuestIds.LightTheManorSteps.RaiseBreaker,
            },
            completed.ToArray());
        Assert.AreEqual(TutorialQuestIds.LightTheManorSteps.InspectHall, tracker.CurrentQuest.Steps[tracker.CurrentStepIndex].Id);
    }

    [Test]
    public void GetNextQuestId_ReturnsBottleQuestAfterLightQuest()
    {
        Assert.AreEqual(TutorialQuestIds.BottleKey, TutorialQuestProgressAdapter.GetNextQuestId(TutorialQuestIds.LightTheManor));
    }

    [Test]
    public void IsTutorialFullyComplete_WhenBottleDragged()
    {
        var flags = new TutorialQuestWorldFlags(SceneNames.Kitchen, true, true, true, true);

        Assert.IsTrue(TutorialQuestProgressAdapter.IsTutorialFullyComplete(flags));
    }
}
