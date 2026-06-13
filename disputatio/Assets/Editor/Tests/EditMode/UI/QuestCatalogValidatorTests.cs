using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class QuestCatalogValidatorTests
{
    [Test]
    public void TryValidate_RejectsEmptyCatalog()
    {
        Assert.IsFalse(QuestCatalogValidator.TryValidate(new List<QuestDefinition>(), out string error));
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void TryValidate_RejectsDuplicateQuestIds()
    {
        var quests = new List<QuestDefinition>
        {
            new QuestDefinition("dup", "A", "hint", new[] { new QuestStep("a1", "step") }),
            new QuestDefinition("dup", "B", "hint", new[] { new QuestStep("b1", "step") }),
        };

        Assert.IsFalse(QuestCatalogValidator.TryValidate(quests, out string error));
        StringAssert.Contains("Duplicate quest id", error);
    }

    [Test]
    public void TryValidate_RejectsDuplicateStepIds()
    {
        var quests = new List<QuestDefinition>
        {
            new QuestDefinition("q1", "A", "hint", new[] { new QuestStep("shared", "step") }),
            new QuestDefinition("q2", "B", "hint", new[] { new QuestStep("shared", "step") }),
        };

        Assert.IsFalse(QuestCatalogValidator.TryValidate(quests, out string error));
        StringAssert.Contains("Duplicate step id", error);
    }

    [Test]
    public void TryValidate_AcceptsValidCatalog()
    {
        var quests = new List<QuestDefinition>
        {
            new QuestDefinition("q1", "A", "hint", new[] { new QuestStep("s1", "step") }),
            new QuestDefinition("q2", "B", "hint", new[] { new QuestStep("s2", "step") }),
        };

        Assert.IsTrue(QuestCatalogValidator.TryValidate(quests, out string error), error);
    }
}
