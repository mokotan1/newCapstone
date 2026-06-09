using Mokotan.StandingDialogue;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class IntroStandingDialogueOffsetPolicyTests
{
    [Test]
    public void UsesFixedOffset_ReturnsTrue_ForIntroScenes()
    {
        Assert.IsTrue(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(SceneNames.IntroScene));
        Assert.IsTrue(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(SceneNames.OpeningOffice));
        Assert.IsTrue(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(SceneNames.OpeningMention));
        Assert.IsTrue(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(SceneNames.OpeningMentionOpen));
    }

    [Test]
    public void UsesFixedOffset_ReturnsFalse_ForNonIntroScenes()
    {
        Assert.IsFalse(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(SceneNames.HallPlayable));
        Assert.IsFalse(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(SceneNames.Kitchen));
        Assert.IsFalse(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(null));
        Assert.IsFalse(IntroStandingDialogueOffsetPolicy.UsesFixedOffset(""));
    }

    [Test]
    public void ResolveForScene_ReturnsFixedOffset_InIntroScene()
    {
        var configured = new Vector2(120f, -50f);

        Vector2 resolved = IntroStandingDialogueOffsetPolicy.ResolveForScene(
            configured,
            SceneNames.OpeningOffice);

        Assert.AreEqual(IntroStandingDialogueOffsetPolicy.FixedOffset, resolved);
    }

    [Test]
    public void ResolveForScene_PreservesConfiguredOffset_OutsideIntroScene()
    {
        var configured = new Vector2(120f, -50f);

        Vector2 resolved = IntroStandingDialogueOffsetPolicy.ResolveForScene(
            configured,
            SceneNames.HallPlayable);

        Assert.AreEqual(configured, resolved);
    }

    [Test]
    public void BackdropAlpha_IsWithinNaturalDimRange()
    {
        Assert.GreaterOrEqual(IntroStandingDialogueOffsetPolicy.BackdropAlpha, 0.4f);
        Assert.LessOrEqual(IntroStandingDialogueOffsetPolicy.BackdropAlpha, 0.6f);
    }
}
