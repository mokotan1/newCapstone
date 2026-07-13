using Fungus;
using NUnit.Framework;

[TestFixture]
public class PlayScenarioBlockLocaleTests
{
    private string _previousLanguage;

    [SetUp]
    public void SetUp()
    {
        _previousLanguage = SetLanguage.mostRecentLanguage;
    }

    [TearDown]
    public void TearDown()
    {
        SetLanguage.mostRecentLanguage = _previousLanguage;
    }

    [Test]
    public void ResolveLanguageCode_UsesCheshireLocaleResolver_WhenOverrideDisabled()
    {
        SetLanguage.mostRecentLanguage = "en";

        string resolved = PlayScenarioBlockCommand.ResolveLanguageCode(
            useInspectorLanguageOverride: false,
            inspectorLanguageCode: "ko");

        Assert.That(resolved, Is.EqualTo(CheshireLocaleResolver.English));
    }

    [Test]
    public void ResolveLanguageCode_UsesInspectorCode_WhenOverrideEnabled()
    {
        SetLanguage.mostRecentLanguage = "en";

        string resolved = PlayScenarioBlockCommand.ResolveLanguageCode(
            useInspectorLanguageOverride: true,
            inspectorLanguageCode: "ja");

        Assert.That(resolved, Is.EqualTo(CheshireLocaleResolver.Japanese));
    }

    [Test]
    public void ResolveLanguageCode_NormalizesInspectorAlias()
    {
        string resolved = PlayScenarioBlockCommand.ResolveLanguageCode(
            useInspectorLanguageOverride: true,
            inspectorLanguageCode: "en-US");

        Assert.That(resolved, Is.EqualTo(CheshireLocaleResolver.English));
    }

    [Test]
    public void LocalizationTable_UsesEnglish_WhenResolvedLocaleIsEn()
    {
        SetLanguage.mostRecentLanguage = "en";
        string language = PlayScenarioBlockCommand.ResolveLanguageCode(false, "ko");

        ScenarioLocalizationTable table = ScenarioLocalizationTable.FromCsv(
            "line_id,ko,en\nOPENING_OFFICE_START_001,\"요새 흉흉하네요.\",\"Things have been grim lately.\"\n",
            language,
            "line_id");

        Assert.That(table.Get("OPENING_OFFICE_START_001"), Is.EqualTo("Things have been grim lately."));
    }

    [Test]
    public void ResourcesDialogueCsv_OpeningOfficeStart001_EnglishCellIsNonEmpty()
    {
        UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(
            "Scenario/the_unholy_dialogue");
        Assert.That(asset, Is.Not.Null, "Expected Resources/Scenario/the_unholy_dialogue.csv");

        ScenarioLocalizationTable en = ScenarioLocalizationTable.FromCsv(asset.text, "en", "line_id");
        string english = en.Get("OPENING_OFFICE_START_001");

        Assert.That(english, Is.Not.Null.And.Not.Empty);
        Assert.That(english, Is.Not.EqualTo("OPENING_OFFICE_START_001"));
        Assert.That(english, Does.Not.Contain("요새"));
    }
}
