using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Player-facing / synthetic Cheshire strings must not leak Hangul for en/ja.
/// </summary>
public class CheshireUiStringsTests
{
    static readonly Regex Hangul = new Regex(
        @"[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7A3]",
        RegexOptions.Compiled);

    static void AssertNoHangul(string text, string label)
    {
        Assert.IsFalse(
            Hangul.IsMatch(text ?? ""),
            $"{label} must not contain Hangul for non-ko locale. Got: {text}");
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void EmptyInputPlease_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(CheshireUiStrings.EmptyInputPlease(locale), nameof(CheshireUiStrings.EmptyInputPlease));
    }

    [Test]
    public void EmptyInputPlease_Korean_KeepsKnownPhrase()
    {
        StringAssert.Contains("내용을 입력해 주세요", CheshireUiStrings.EmptyInputPlease(CheshireLocaleResolver.Korean));
    }

    [Test]
    public void EmptyInputPlease_English_ReturnsCsvEnglish()
    {
        Assert.AreEqual("Please enter a message.", CheshireUiStrings.EmptyInputPlease(CheshireLocaleResolver.English));
    }

    [Test]
    public void Lookup_UsesInjectedCsvOverride()
    {
        try
        {
            CheshireUiStrings.SetCsvTextOverrideForTests(
                "string_id,ko,en,ja\nEmptyInputPlease,ko-fallback,en-from-csv,ja-from-csv\n");
            Assert.AreEqual("en-from-csv", CheshireUiStrings.EmptyInputPlease(CheshireLocaleResolver.English));
            Assert.AreEqual("ja-from-csv", CheshireUiStrings.EmptyInputPlease(CheshireLocaleResolver.Japanese));
            Assert.AreEqual("ko-fallback", CheshireUiStrings.EmptyInputPlease(CheshireLocaleResolver.Korean));
        }
        finally
        {
            CheshireUiStrings.ClearCsvOverrideForTests();
        }
    }

    [Test]
    public void WrongAnswerWithHint_FormatsPlaceholderFromCsvTemplate()
    {
        try
        {
            CheshireUiStrings.SetCsvTextOverrideForTests(
                "string_id,ko,en,ja\nWrongAnswerWithHint,힌트: {0},Hint: {0},ヒント: {0}\n");
            Assert.AreEqual("Hint: Goliath", CheshireUiStrings.WrongAnswerWithHint(CheshireLocaleResolver.English, "Goliath"));
        }
        finally
        {
            CheshireUiStrings.ClearCsvOverrideForTests();
        }
    }

    [Test]
    public void ResourcesCsv_LoadsEmptyInputPleaseEnglish()
    {
        UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Scenario/cheshire_ui_strings");
        Assert.That(asset, Is.Not.Null, "Expected Resources/Scenario/cheshire_ui_strings.csv");

        ScenarioLocalizationTable table = ScenarioLocalizationTable.FromCsv(asset.text, "en", "string_id");
        Assert.AreEqual("Please enter a message.", table.Get("EmptyInputPlease"));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void LocalAiNotReady_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(CheshireUiStrings.LocalAiNotReady(locale), nameof(CheshireUiStrings.LocalAiNotReady));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void LocalAiDisabled_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(CheshireUiStrings.LocalAiDisabled(locale), nameof(CheshireUiStrings.LocalAiDisabled));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void ConnectionErrorPrefix_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(CheshireUiStrings.ConnectionErrorPrefix(locale), nameof(CheshireUiStrings.ConnectionErrorPrefix));
    }

    [Test]
    public void ConnectionErrorPrefix_Korean_KeepsKnownPhrase()
    {
        StringAssert.Contains("연결 오류", CheshireUiStrings.ConnectionErrorPrefix(CheshireLocaleResolver.Korean));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void WrongAnswerRetry_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(CheshireUiStrings.WrongAnswerRetry(locale), nameof(CheshireUiStrings.WrongAnswerRetry));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void WrongAnswerWithHint_NonKorean_HasNoHangul(string locale)
    {
        string text = CheshireUiStrings.WrongAnswerWithHint(locale, "Goliath");
        AssertNoHangul(text, nameof(CheshireUiStrings.WrongAnswerWithHint));
        StringAssert.Contains("Goliath", text);
    }

    [Test]
    public void WrongAnswerRetry_Korean_KeepsKnownPhrase()
    {
        StringAssert.Contains("아직 정답이 아니야", CheshireUiStrings.WrongAnswerRetry(CheshireLocaleResolver.Korean));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void UserPromptAfterCorrectAnswer_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.UserPromptAfterCorrectAnswer(locale),
            nameof(CheshireUiStrings.UserPromptAfterCorrectAnswer));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void UserPromptMissionComplete_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.UserPromptMissionComplete(locale),
            nameof(CheshireUiStrings.UserPromptMissionComplete));
    }

    [Test]
    public void UserPromptAfterCorrectAnswer_Korean_KeepsKnownSystemMarker()
    {
        StringAssert.Contains(
            "정답으로 확정",
            CheshireUiStrings.UserPromptAfterCorrectAnswer(CheshireLocaleResolver.Korean));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void UserPromptChesterWindowOpen_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.UserPromptChesterWindowOpen(locale),
            nameof(CheshireUiStrings.UserPromptChesterWindowOpen));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void UserPromptChesterParrotAskQuestionNow_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.UserPromptChesterParrotAskQuestionNow(locale),
            nameof(CheshireUiStrings.UserPromptChesterParrotAskQuestionNow));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void TimerPrompt_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(CheshireUiStrings.TimerLowTimePrompt(locale), nameof(CheshireUiStrings.TimerLowTimePrompt));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void EmptyPanelAdvanceAndSkip_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.EmptyPanelAdvancePrompt(locale),
            nameof(CheshireUiStrings.EmptyPanelAdvancePrompt));
        AssertNoHangul(
            CheshireUiStrings.EmptyPanelSkipPrompt(locale),
            nameof(CheshireUiStrings.EmptyPanelSkipPrompt));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void ThinkingHoldDefault_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.ThinkingHoldDefault(locale),
            nameof(CheshireUiStrings.ThinkingHoldDefault));
    }

    [Test]
    public void ResolveThinkingHoldMessage_InspectorEmpty_UsesLocaleDefault()
    {
        string en = CheshireUiStrings.ResolveThinkingHoldMessage("  ", CheshireLocaleResolver.English);
        Assert.AreEqual(CheshireUiStrings.ThinkingHoldDefault(CheshireLocaleResolver.English), en);
        AssertNoHangul(en, "thinking hold en");
    }

    [Test]
    public void ResolveThinkingHoldMessage_InspectorOverride_Wins()
    {
        const string custom = "Custom hold text";
        Assert.AreEqual(
            custom,
            CheshireUiStrings.ResolveThinkingHoldMessage(custom, CheshireLocaleResolver.Japanese));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void ProgressChrome_NonKorean_HasNoHangul(string locale)
    {
        AssertNoHangul(
            CheshireUiStrings.ProgressEmptySection(locale),
            nameof(CheshireUiStrings.ProgressEmptySection));
        AssertNoHangul(
            CheshireUiStrings.ProgressAcquiredHeader(locale),
            nameof(CheshireUiStrings.ProgressAcquiredHeader));
        AssertNoHangul(
            CheshireUiStrings.ProgressGuideFooter(locale),
            nameof(CheshireUiStrings.ProgressGuideFooter));
    }

    [Test]
    public void ProgressChrome_Korean_KeepsKnownLabels()
    {
        StringAssert.Contains("[진행]", CheshireUiStrings.ProgressEmptySection(CheshireLocaleResolver.Korean));
        StringAssert.Contains("[진행 안내]", CheshireUiStrings.ProgressGuideFooter(CheshireLocaleResolver.Korean));
    }
}
