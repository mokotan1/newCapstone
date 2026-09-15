using NUnit.Framework;

[TestFixture]
public class LocalAiReadinessTests
{
    [Test]
    public void RequiresLoopbackRuntime_LoopbackChatUrl_ReturnsTrue()
    {
        Assert.IsTrue(LocalAiReadiness.RequiresLoopbackRuntime("http://127.0.0.1:8000/chat"));
        Assert.IsTrue(LocalAiReadiness.RequiresLoopbackRuntime("http://localhost:8000/chat"));
    }

    [Test]
    public void RequiresLoopbackRuntime_RemoteChatUrl_ReturnsFalse()
    {
        Assert.IsFalse(LocalAiReadiness.RequiresLoopbackRuntime("http://54.156.51.119:8000/chat"));
    }

    [Test]
    public void ResolveRootUrl_StripsChatPath()
    {
        Assert.AreEqual("http://127.0.0.1:8000/", LocalAiReadiness.ResolveRootUrl("http://127.0.0.1:8000/chat"));
        Assert.AreEqual("http://127.0.0.1:8000/", LocalAiReadiness.ResolveRootUrl("http://127.0.0.1:8000/chat/stream"));
    }

    [Test]
    public void CanSendChat_PlayerDisabled_BlocksEvenWhenReady()
    {
        Assert.IsFalse(LocalAiReadiness.CanSendChat(true, true, true));
    }

    [Test]
    public void CanSendChat_RemoteUrl_DoesNotWaitForLocalModel()
    {
        Assert.IsTrue(LocalAiReadiness.CanSendChat(false, false, false));
    }

    [Test]
    public void IsLocalModelReady_DegradedWithoutModel_ReturnsFalse()
    {
        const string json =
            "{\"status\":\"degraded\",\"local_runtime\":{\"available\":true,\"model_available\":false,\"error\":\"missing\"}}";
        Assert.IsFalse(LocalAiReadiness.IsLocalModelReady(json, 200, requireLocalRuntime: true));
    }

    [Test]
    public void IsLocalModelReady_LocalModelAvailable_ReturnsTrue()
    {
        const string json =
            "{\"status\":\"online\",\"local_runtime\":{\"available\":true,\"model_available\":true,\"error\":null}}";
        Assert.IsTrue(LocalAiReadiness.IsLocalModelReady(json, 200, requireLocalRuntime: true));
    }

    [Test]
    public void IsLocalModelReady_LoopbackWithoutRuntimePayload_ReturnsFalse()
    {
        Assert.IsFalse(LocalAiReadiness.IsLocalModelReady("{\"status\":\"online\"}", 200, requireLocalRuntime: true));
    }

    [Test]
    public void IsLocalModelReady_HttpError_ReturnsFalse()
    {
        Assert.IsFalse(LocalAiReadiness.IsLocalModelReady("{\"status\":\"online\"}", 503, requireLocalRuntime: false));
    }
}
