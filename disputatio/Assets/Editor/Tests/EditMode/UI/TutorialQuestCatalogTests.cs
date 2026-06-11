using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class TutorialQuestCatalogTests
{
    TutorialQuestCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        TutorialQuestCatalog.ResetCacheForTest();
        catalog = TutorialQuestCatalog.GetOrCreate();
    }

    [Test]
    public void Catalog_IsNotEmpty()
    {
        Assert.IsNotNull(catalog, "Resources/TutorialQuestCatalog.asset is missing. Run Disputatio/Quest/Build Tutorial Quest Catalog.");
        Assert.Greater(catalog.Count, 0);
        Assert.Greater(catalog.ToDefinitions().Count, 0);
    }

    [Test]
    public void EachQuest_HasAtLeastOneStep()
    {
        Assert.IsNotNull(catalog);

        foreach (QuestDefinition quest in catalog.ToDefinitions())
        {
            Assert.IsTrue(quest.HasPlayableSteps, $"Quest '{quest.Id}' must have at least one step.");
        }
    }

    [Test]
    public void Catalog_PassesQuestAndStepIdValidation()
    {
        Assert.IsNotNull(catalog);
        Assert.IsTrue(
            QuestCatalogValidator.TryValidate(catalog, out string error),
            error);
    }

    [Test]
    public void BuiltInTutorialQuests_ContainExpectedTitlesAndStepTexts()
    {
        Assert.IsNotNull(catalog);

        QuestDefinition lightQuest = catalog.ToDefinitions()
            .FirstOrDefault(quest => quest.Id == TutorialQuestIds.LightTheManor);
        QuestDefinition bottleQuest = catalog.ToDefinitions()
            .FirstOrDefault(quest => quest.Id == TutorialQuestIds.BottleKey);

        Assert.IsNotNull(lightQuest);
        Assert.AreEqual("저택에 불을 밝혀라", lightQuest.Title);
        Assert.AreEqual("불이 없으면 핏자국이 보이지 않는다…", lightQuest.Hint);
        Assert.AreEqual(3, lightQuest.Steps.Count);
        Assert.AreEqual("주방으로 이동한다", lightQuest.Steps[0].Text);
        Assert.AreEqual("다용도실 차단기를 올린다", lightQuest.Steps[1].Text);
        Assert.AreEqual("불 켜진 복도를 살핀다", lightQuest.Steps[2].Text);

        Assert.IsNotNull(bottleQuest);
        Assert.AreEqual("병 속 열쇠를 꺼내라", bottleQuest.Title);
        Assert.AreEqual("물이 차오르자 열쇠가 떠오른다.", bottleQuest.Hint);
        Assert.AreEqual(3, bottleQuest.Steps.Count);
        Assert.AreEqual("화분 속 병을 발견한다", bottleQuest.Steps[0].Text);
        Assert.AreEqual("싱크대에서 병에 물을 채운다", bottleQuest.Steps[1].Text);
        Assert.AreEqual("떠오른 열쇠를 집는다", bottleQuest.Steps[2].Text);
    }

    [Test]
    public void CatalogDefinitions_CanInitializeQuestTrackerState()
    {
        Assert.IsNotNull(catalog);

        var tracker = new QuestTrackerState(catalog.ToDefinitions());
        Assert.IsTrue(tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor));
        Assert.AreEqual(0, tracker.CurrentStepIndex);
        Assert.IsTrue(tracker.CompleteStep(TutorialQuestIds.LightTheManorSteps.GoKitchen));
    }
}
