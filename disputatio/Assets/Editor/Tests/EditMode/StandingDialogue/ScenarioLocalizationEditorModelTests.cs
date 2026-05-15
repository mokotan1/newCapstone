using NUnit.Framework;

[TestFixture]
public class ScenarioLocalizationEditorModelTests
{
    private const string ScenarioJson = @"{
        ""schema_version"": 1,
        ""blocks"": [
            {
                ""block_id"": ""opening_office_start"",
                ""commands"": [
                    { ""command"": ""talk_standing"", ""line_id"": ""LINE_001"", ""speaker_id"": ""assistant"", ""side"": ""left"" },
                    { ""command"": ""talk_standing"", ""line_id"": ""LINE_002"", ""speaker_id"": ""player"", ""side"": ""right"" }
                ]
            },
            {
                ""block_id"": ""mansion_entry_start"",
                ""commands"": [
                    { ""command"": ""talk_standing"", ""line_id"": ""LINE_003"", ""speaker_id"": ""player"", ""side"": ""right"" }
                ]
            }
        ]
    }";

    [Test]
    public void CsvDocument_AddsMissingLanguageAndSavesEditedTranslation()
    {
        ScenarioLocalizationCsvDocument document = ScenarioLocalizationCsvDocument.FromCsv(
            "line_id,ko\nLINE_001,\"원문, 쉼표 포함\"\n",
            "line_id");

        document.SetValue("LINE_001", "en", "Translated, with comma");

        string saved = document.ToCsv();

        StringAssert.Contains("line_id,ko,en", saved);
        StringAssert.Contains("\"Translated, with comma\"", saved);
    }

    [Test]
    public void BuildRows_ReturnsOnlyLinesForSelectedBlockInCommandOrder()
    {
        ScenarioScript script = ScenarioScript.FromJson(ScenarioJson);
        ScenarioLocalizationCsvDocument dialogue = ScenarioLocalizationCsvDocument.FromCsv(
            "line_id,ko,en\nLINE_001,안녕,Hello\nLINE_002,가자,\nLINE_003,저택,Mansion\n",
            "line_id");
        ScenarioLocalizationCsvDocument speakers = ScenarioLocalizationCsvDocument.FromCsv(
            "speaker_id,ko,en\nassistant,조수,Assistant\nplayer,주인공,Detective\n",
            "speaker_id");

        ScenarioLocalizationEditorRow[] rows = ScenarioLocalizationEditorModel.BuildRows(
            script,
            "opening_office_start",
            dialogue,
            speakers,
            "en");

        Assert.That(rows.Length, Is.EqualTo(2));
        Assert.That(rows[0].lineId, Is.EqualTo("LINE_001"));
        Assert.That(rows[0].speakerName, Is.EqualTo("Assistant"));
        Assert.That(rows[0].sourceText, Is.EqualTo("안녕"));
        Assert.That(rows[0].translation, Is.EqualTo("Hello"));
        Assert.That(rows[0].isTranslated, Is.True);
        Assert.That(rows[1].lineId, Is.EqualTo("LINE_002"));
        Assert.That(rows[1].isTranslated, Is.False);
    }
}
