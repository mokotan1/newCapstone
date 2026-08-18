using System;
using NUnit.Framework;
using UnityEngine;

public class HintInformationPolicyTests
{
    Func<string, TextAsset> _previousLoader;

    [SetUp]
    public void SetUp()
    {
        _previousLoader = CheshirePromptCatalog.ResourceLoader;
    }

    [TearDown]
    public void TearDown()
    {
        CheshirePromptCatalog.ResourceLoader = _previousLoader;
    }

    [Test]
    public void BuildPromptBlock_Novice_ContainsDirectGuidanceText()
    {
        var profile = new PlayerSkillProfile { level = PlayerSkillLevel.Novice };
        string block = HintInformationPolicy.BuildPromptBlock(profile, CheshireLocaleResolver.Korean);
        StringAssert.Contains("직접적으로", block);
    }

    [Test]
    public void BuildPromptBlock_Expert_ContainsMinimalExposureText()
    {
        var profile = new PlayerSkillProfile { level = PlayerSkillLevel.Expert };
        string block = HintInformationPolicy.BuildPromptBlock(profile, CheshireLocaleResolver.Korean);
        StringAssert.Contains("정보 노출을 최소화", block);
    }

    [Test]
    public void BuildPromptBlock_English_DoesNotContainKoreanPolicyHeader()
    {
        CheshirePromptCatalog.ResourceLoader = path =>
        {
            if (path == $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.English}/HintPolicy_Novice")
                return new TextAsset("[Information budget policy]\n- Point more directly at the next action.");
            if (path == $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.Korean}/HintPolicy_Novice")
                return new TextAsset("[정보량 정책]\n- 직접적으로 제시하세요.");
            return null;
        };

        var profile = new PlayerSkillProfile { level = PlayerSkillLevel.Novice };
        string block = HintInformationPolicy.BuildPromptBlock(profile, CheshireLocaleResolver.English);

        Assert.IsFalse(block.Contains("[정보량 정책]"), block);
        Assert.IsTrue(
            block.Contains("[Information budget policy]") || block.Contains("Information"),
            block);
    }

    [Test]
    public void Compose_EnglishLocale_DoesNotEmbedKoreanPolicyHeader()
    {
        CheshirePromptCatalog.ResourceLoader = path =>
        {
            if (path.EndsWith($"/{CheshireLocaleResolver.English}/HintPolicy_Intermediate"))
                return new TextAsset("[Information budget policy]\n- Keep mid-level hints.");
            if (path.EndsWith($"/{CheshireLocaleResolver.Korean}/HintPolicy_Intermediate"))
                return new TextAsset("[정보량 정책]\n- 중간 힌트.");
            return null;
        };

        // progress/accuracy 0.5 with no stuck → Intermediate skill band
        var input = new HeuristicSignalInput
        {
            roomName = "TestRoom",
            progressScore = 0.5f,
            accuracyScore = 0.5f,
            unsolvedRevisitCount = 0,
            revisitIntervalSeconds = 120f,
            noProgressAfterRevisitCount = 0
        };

        string composed = PromptInfoBudgetComposer.Compose("base-prompt", input, CheshireLocaleResolver.English);

        Assert.IsFalse(composed.Contains("[정보량 정책]"), composed);
        StringAssert.Contains("base-prompt", composed);
        StringAssert.Contains("Information", composed);
    }
}
