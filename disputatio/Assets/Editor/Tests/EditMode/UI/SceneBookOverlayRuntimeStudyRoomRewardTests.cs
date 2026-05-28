using NUnit.Framework;

public class SceneBookOverlayRuntimeStudyRoomRewardTests
{
    [Test]
    public void ShouldUseAlreadySolvedStudyRoomBody_WhenDiarySolved()
    {
        Assert.IsTrue(SceneBookOverlayRuntime.ShouldUseAlreadySolvedStudyRoomBody(
            "StudyRoom",
            "ACT2_DIARY_OWNER_001",
            diarySolved: true,
            hasTutorKey: false));
    }

    [Test]
    public void BuildStudyRoomAlreadySolvedBody_ReplacesRewardPagesWithAlreadySolvedMessage()
    {
        string body = SceneBookOverlayRuntime.BuildStudyRoomAlreadySolvedBody("첫 장");

        StringAssert.Contains("첫 장", body);
        StringAssert.Contains("나는 이미 문제를 풀었어", body);
        StringAssert.DoesNotContain("작은 열쇠", body);
    }
}
