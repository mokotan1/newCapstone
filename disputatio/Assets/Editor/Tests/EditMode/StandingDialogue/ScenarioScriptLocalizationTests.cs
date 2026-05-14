using NUnit.Framework;

[TestFixture]
public class ScenarioScriptLocalizationTests
{
    private const string ScenarioJson = @"{
        ""schema_version"": 1,
        ""blocks"": [
            {
                ""block_id"": ""opening_office_start"",
                ""commands"": [
                    {
                        ""command"": ""talk_standing"",
                        ""line_id"": ""OPENING_OFFICE_START_001"",
                        ""speaker_id"": ""assistant"",
                        ""side"": ""left""
                    },
                    {
                        ""command"": ""talk_standing"",
                        ""line_id"": ""OPENING_OFFICE_START_002"",
                        ""speaker_id"": ""player"",
                        ""side"": ""right""
                    }
                ]
            }
        ]
    }";

    [Test]
    public void FromJson_FindsCommandsByBlockId()
    {
        ScenarioScript script = ScenarioScript.FromJson(ScenarioJson);

        Assert.That(script.TryGetBlock("opening_office_start", out ScenarioBlock block), Is.True);
        Assert.That(block.commands.Length, Is.EqualTo(2));
        Assert.That(block.commands[0].line_id, Is.EqualTo("OPENING_OFFICE_START_001"));
    }

    [Test]
    public void LocalizationTable_ResolvesRequestedLanguageAndFallsBackToKorean()
    {
        string csv = "line_id,ko,en\n"
            + "OPENING_OFFICE_START_001,\"요새 흉흉하네요.\",\"Things have been grim lately.\"\n"
            + "OPENING_OFFICE_START_002,\"남들이 실종된다는데 그게 할 소리냐?\",\n";

        ScenarioLocalizationTable table = ScenarioLocalizationTable.FromCsv(csv, "en", "line_id");

        Assert.That(table.Get("OPENING_OFFICE_START_001"), Is.EqualTo("Things have been grim lately."));
        Assert.That(table.Get("OPENING_OFFICE_START_002"), Is.EqualTo("남들이 실종된다는데 그게 할 소리냐?"));
    }

    [Test]
    public void BuildTalkLines_CombinesScenarioCommandsWithLocalizedTextAndSpeakerName()
    {
        ScenarioScript script = ScenarioScript.FromJson(ScenarioJson);
        ScenarioLocalizationTable lines = ScenarioLocalizationTable.FromCsv(
            "line_id,ko,en\nOPENING_OFFICE_START_001,\"요새 흉흉하네요.\",\"Things have been grim lately.\"\n",
            "en",
            "line_id");
        ScenarioLocalizationTable speakers = ScenarioLocalizationTable.FromCsv(
            "speaker_id,ko,en\nassistant,조수,Assistant\nplayer,주인공,Detective\n",
            "en",
            "speaker_id");

        ScenarioTalkLine[] talkLines = ScenarioBlockResolver.BuildTalkLines(
            script,
            "opening_office_start",
            lines,
            speakers);

        Assert.That(talkLines.Length, Is.EqualTo(2));
        Assert.That(talkLines[0].speakerName, Is.EqualTo("Assistant"));
        Assert.That(talkLines[0].text, Is.EqualTo("Things have been grim lately."));
        Assert.That(talkLines[0].side, Is.EqualTo(ScenarioSpeakerSide.Left));
        Assert.That(talkLines[1].speakerName, Is.EqualTo("Detective"));
        Assert.That(talkLines[1].text, Is.EqualTo("OPENING_OFFICE_START_002"));
    }
}
