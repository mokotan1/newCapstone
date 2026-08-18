using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public class TutorQuizGraderTests
{
    static readonly Regex Hangul = new Regex(
        @"[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7A3]",
        RegexOptions.Compiled);

    [Test]
    public void BuildGradeRequestPayload_IncludesNormalizedLocale()
    {
        Dictionary<string, object> payload = TutorQuizGrader.BuildGradeRequestPayload(
            "Q001",
            "Goliath",
            1,
            TutorQuizStateTracker.TutorQuizTargetCorrectCount,
            "en-US");

        Assert.AreEqual("Q001", payload["question_id"]);
        Assert.AreEqual("Goliath", payload["user_answer"]);
        Assert.AreEqual(1, payload["correct_count_before"]);
        Assert.AreEqual(TutorQuizStateTracker.TutorQuizTargetCorrectCount, payload["quiz_target"]);
        Assert.AreEqual(CheshireLocaleResolver.English, payload["locale"]);

        string json = JsonConvert.SerializeObject(payload);
        JObject obj = JObject.Parse(json);
        Assert.AreEqual("en", (string)obj["locale"]);
        StringAssert.Contains("\"locale\":\"en\"", json.Replace(" ", ""));
    }

    [Test]
    public void BuildGradeRequestPayload_DefaultsUnknownLocaleToKo()
    {
        Dictionary<string, object> payload = TutorQuizGrader.BuildGradeRequestPayload(
            "Q002",
            "x",
            0,
            5,
            "zz");

        Assert.AreEqual(CheshireLocaleResolver.Korean, payload["locale"]);
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void WrongAnswerTemplates_NonKorean_HaveNoHangul(string locale)
    {
        Assert.IsFalse(Hangul.IsMatch(CheshireUiStrings.WrongAnswerRetry(locale)));
        Assert.IsFalse(Hangul.IsMatch(CheshireUiStrings.WrongAnswerWithHint(locale, "hint")));
    }

    [Test]
    public void WrongAnswerRetry_Korean_KeepsKnownPhrase()
    {
        StringAssert.Contains(
            "아직 정답이 아니야",
            CheshireUiStrings.WrongAnswerRetry(CheshireLocaleResolver.Korean));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void SyntheticCorrectAnswerPrompts_NonKorean_HaveNoHangul(string locale)
    {
        Assert.IsFalse(Hangul.IsMatch(CheshireUiStrings.UserPromptAfterCorrectAnswer(locale)));
        Assert.IsFalse(Hangul.IsMatch(CheshireUiStrings.UserPromptMissionComplete(locale)));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void TutorInsufficientQuestions_NonKorean_HaveNoHangul(string locale)
    {
        Assert.IsFalse(Hangul.IsMatch(CheshireUiStrings.TutorInsufficientQuestions(locale)));
    }

    [Test]
    public void TutorInsufficientQuestions_Korean_KeepsKnownPhrase()
    {
        StringAssert.Contains(
            "문제를 준비할 수 없어",
            CheshireUiStrings.TutorInsufficientQuestions(CheshireLocaleResolver.Korean));
    }
}
