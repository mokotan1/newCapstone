using NUnit.Framework;

[TestFixture]
public class LocalAiEndpointResolverTests
{
    [Test]
    public void ResolveChatUrl_UsesSessionWhenUsable()
    {
        var session = new LocalAiSessionSnapshot(
            "1", "sess-1", "inst-1", 10, "t0", "disputatio",
            "http://127.0.0.1:8123/", "secret");
        string url = LocalAiEndpointResolver.ResolveChatUrl(
            ServerConfig.DefaultCloudChatUrl, session);
        Assert.AreEqual("http://127.0.0.1:8123/chat", url);
    }

    [Test]
    public void ResolveChatUrl_IgnoresRemoteConfiguredUrlWithoutSession()
    {
        string url = LocalAiEndpointResolver.ResolveChatUrl(
            ServerConfig.DefaultCloudChatUrl, null);
        Assert.AreEqual(LocalAiEndpointResolver.LoopbackChatUrl, url);
        Assert.IsFalse(url.Contains("54.156"));
    }

    [Test]
    public void ResolveChatUrl_KeepsLoopbackConfiguredUrlWithoutSession()
    {
        string url = LocalAiEndpointResolver.ResolveChatUrl(
            "http://127.0.0.1:8000/chat", null);
        Assert.AreEqual("http://127.0.0.1:8000/chat", url);
    }

    [Test]
    public void StreamAndGradeShareTheSameRoot()
    {
        const string chat = "http://127.0.0.1:8123/chat";
        Assert.AreEqual("http://127.0.0.1:8123/chat/stream", LocalAiEndpointResolver.ResolveStreamUrl(chat));
        Assert.AreEqual("http://127.0.0.1:8123/tutor/grade", LocalAiEndpointResolver.ResolveGradeUrl(chat));
        Assert.AreEqual("http://127.0.0.1:8123/", LocalAiEndpointResolver.ResolveStatusUrl(chat));
    }

    [Test]
    public void CanReconnect_RejectsPortOnlyMatch()
    {
        var live = new LocalAiSessionSnapshot(
            "1", "sess-1", "inst-1", 10, "t0", "disputatio",
            "http://127.0.0.1:8123/", "");
        var claimed = new LocalAiSessionSnapshot(
            "1", "other", "other", 1, "no", "disputatio",
            "http://127.0.0.1:8123/", "");
        Assert.IsFalse(LocalAiSessionSnapshot.CanReconnect(claimed, live));
    }

    [Test]
    public void ShouldAutoStart_SkipsBatchmodeAndManualStop()
    {
        Assert.IsFalse(LocalAiEndpointResolver.ShouldAutoStart(true, true, false));
        Assert.IsFalse(LocalAiEndpointResolver.ShouldAutoStart(false, true, true));
        Assert.IsFalse(LocalAiEndpointResolver.ShouldAutoStart(false, false, false));
        Assert.IsTrue(LocalAiEndpointResolver.ShouldAutoStart(false, true, false));
    }

    [Test]
    public void TryParse_RejectsMissingIdentity()
    {
        Assert.IsFalse(LocalAiSessionSnapshot.TryParse("{}", "", out _));
        Assert.IsTrue(LocalAiSessionSnapshot.TryParse(
            "{\"protocol_version\":\"1\",\"session_id\":\"s\",\"instance_id\":\"i\","
            + "\"parent_pid\":1,\"parent_start\":\"t\",\"project_id\":\"disputatio\","
            + "\"base_url\":\"http://127.0.0.1:9/\"}",
            "tok",
            out LocalAiSessionSnapshot snapshot));
        Assert.AreEqual("s", snapshot.SessionId);
        Assert.AreEqual("tok", snapshot.Token);
    }
}
