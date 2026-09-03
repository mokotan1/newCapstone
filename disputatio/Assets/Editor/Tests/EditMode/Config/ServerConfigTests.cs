using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class ServerConfigTests
{
    [TearDown]
    public void TearDown()
    {
        ServerConfig.ResetCacheForTest();
    }

    [Test]
    public void GetOrCreate_ReturnsNonNull()
    {
        if (Resources.Load<ServerConfig>("ServerConfig") == null)
        {
            LogAssert.Expect(
                LogType.Warning,
                "[ServerConfig] Resources/ServerConfig not found — using runtime defaults.");
        }

        ServerConfig config = ServerConfig.GetOrCreate();
        Assert.IsNotNull(config);
    }

    [Test]
    public void FreshInstance_HasDefaultChatUrl()
    {
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        Assert.AreEqual(ServerConfig.LocalLoopbackChatUrl, config.ChatUrl);
    }

    [Test]
    public void FreshInstance_UseLocalLoopback_DefaultsToTrue()
    {
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        Assert.IsTrue(config.UseLocalLoopback);
    }

    [Test]
    public void ChatUrl_UsesSerializedCloudUrl_WhenLoopbackDisabled()
    {
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        config.ApplyChatEndpointForTest(
            useLocalLoopback: false,
            chatUrl: ServerConfig.DefaultCloudChatUrl);

        Assert.AreEqual(ServerConfig.DefaultCloudChatUrl, config.ChatUrl);
        Assert.IsFalse(config.UseLocalLoopback);
    }

    [Test]
    public void FreshInstance_BypassTlsCertificate_DefaultsToTrue()
    {
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        Assert.IsTrue(config.BypassTlsCertificate);
    }

    [Test]
    public void FreshInstance_ChatApiToken_DefaultsToEmpty()
    {
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        Assert.AreEqual("", config.ChatApiToken);
    }

    // ---------------------------------------------------------------
    //  Caching behavior
    // ---------------------------------------------------------------

    [Test]
    public void GetOrCreate_ReturnsSameInstance_OnRepeatedCalls()
    {
        ServerConfig.ResetCacheForTest();

        if (Resources.Load<ServerConfig>("ServerConfig") == null)
        {
            LogAssert.Expect(
                LogType.Warning,
                "[ServerConfig] Resources/ServerConfig not found — using runtime defaults.");
        }

        ServerConfig first = ServerConfig.GetOrCreate();
        ServerConfig second = ServerConfig.GetOrCreate();

        Assert.AreSame(first, second);
    }

    [Test]
    public void ResetCacheForTest_ClearsCachedInstance()
    {
        if (Resources.Load<ServerConfig>("ServerConfig") == null)
        {
            LogAssert.Expect(
                LogType.Warning,
                "[ServerConfig] Resources/ServerConfig not found — using runtime defaults.");
        }

        ServerConfig first = ServerConfig.GetOrCreate();
        ServerConfig.ResetCacheForTest();

        if (Resources.Load<ServerConfig>("ServerConfig") == null)
        {
            LogAssert.Expect(
                LogType.Warning,
                "[ServerConfig] Resources/ServerConfig not found — using runtime defaults.");
        }

        ServerConfig second = ServerConfig.GetOrCreate();

        Assert.IsNotNull(second);
        if (Resources.Load<ServerConfig>("ServerConfig") == null)
            Assert.AreNotSame(first, second);
    }

    // ---------------------------------------------------------------
    //  CreateInstance independence
    // ---------------------------------------------------------------

    [Test]
    public void CreateInstance_ProducesIndependentInstances()
    {
        ServerConfig a = ScriptableObject.CreateInstance<ServerConfig>();
        ServerConfig b = ScriptableObject.CreateInstance<ServerConfig>();

        Assert.AreNotSame(a, b);
        Assert.AreEqual(a.ChatUrl, b.ChatUrl);
        Assert.AreEqual(a.BypassTlsCertificate, b.BypassTlsCertificate);
        Assert.AreEqual(a.ChatApiToken, b.ChatApiToken);
    }

    [Test]
    public void GetOrCreate_FallbackInstance_HasDefaultValues()
    {
        ServerConfig.ResetCacheForTest();

        if (Resources.Load<ServerConfig>("ServerConfig") == null)
        {
            LogAssert.Expect(
                LogType.Warning,
                "[ServerConfig] Resources/ServerConfig not found — using runtime defaults.");
        }

        ServerConfig config = ServerConfig.GetOrCreate();

        Assert.IsNotNull(config.ChatUrl);
        Assert.IsNotEmpty(config.ChatUrl);
    }
}
