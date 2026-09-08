using NUnit.Framework;

[TestFixture]
public class LocalAiControlApiTests
{
    [Test]
    public void DefaultRequestedMode_IsGpuForCudaPath()
    {
        Assert.AreEqual("gpu", LocalAiControlApi.DefaultRequestedMode);
        Assert.AreEqual("medium", LocalAiControlApi.DefaultGpuOffload);
    }

    [Test]
    public void BuildSettingsBody_RequestsGpuByDefault()
    {
        string body = LocalAiControlApi.BuildSettingsBody();
        Assert.IsTrue(body.Contains("\"mode\":\"gpu\""));
        Assert.IsTrue(body.Contains("\"gpu_offload\":\"medium\""));
    }

    [Test]
    public void StatusAndSettingsUrls_AreUnderLoopbackRoot()
    {
        const string chatUrl = "http://127.0.0.1:8000/chat/stream";
        Assert.AreEqual("http://127.0.0.1:8000/local-ai/status", LocalAiControlApi.StatusUrl(chatUrl));
        Assert.AreEqual("http://127.0.0.1:8000/local-ai/settings", LocalAiControlApi.SettingsUrl(chatUrl));
    }

    [Test]
    public void ShouldControlLocalRuntime_OnlyOnLoopback()
    {
        Assert.IsTrue(LocalAiControlApi.ShouldControlLocalRuntime("http://127.0.0.1:8000/chat"));
        Assert.IsFalse(LocalAiControlApi.ShouldControlLocalRuntime("http://54.156.51.119:8000/chat"));
    }

    [Test]
    public void TryReadControlToken_RejectsEmpty()
    {
        Assert.IsFalse(LocalAiControlApi.TryReadControlToken("  \n", out string token));
        Assert.AreEqual("", token);
    }

    [Test]
    public void TryReadControlToken_TrimsFileText()
    {
        Assert.IsTrue(LocalAiControlApi.TryReadControlToken("  secret-control  \n", out string token));
        Assert.AreEqual("secret-control", token);
    }

    [Test]
    public void DefaultControlTokenPath_IsGameLocalAiDir()
    {
        string path = LocalAiControlApi.DefaultControlTokenPath();
        StringAssert.Contains("Disputatio", path);
        StringAssert.Contains("local-ai", path);
        StringAssert.EndsWith("control.token", path);
    }

    [Test]
    public void TryParseStatus_ReadsRequestedAndEffective()
    {
        const string json =
            "{\"requested_mode\":\"gpu\",\"effective_backend\":\"cpu\",\"state\":\"ready\"," +
            "\"inference_ready\":true,\"fallback_reason\":\"gpu_runtime_not_available\"}";
        Assert.IsTrue(LocalAiControlApi.TryParseStatus(json, out LocalAiRuntimeStatus status));
        Assert.AreEqual("gpu", status.RequestedMode);
        Assert.AreEqual("cpu", status.EffectiveBackend);
        Assert.AreEqual("ready", status.State);
        Assert.IsTrue(status.InferenceReady);
        Assert.AreEqual("gpu_runtime_not_available", status.FallbackReason);
    }

    [Test]
    public void TryParseStatus_RejectsEmpty()
    {
        Assert.IsFalse(LocalAiControlApi.TryParseStatus("", out _));
    }
}
