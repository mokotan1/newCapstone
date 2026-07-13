using System;
using System.Collections;
using System.Collections.Generic;
using Godlotto.Interaction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class ChatHttpClientTests
{
    [TearDown]
    public void TearDown()
    {
        InteractionInputGate.ResetForTests();
    }

    // ---------------------------------------------------------------
    //  TryNormalizePromptForChatApi
    // ---------------------------------------------------------------

    [Test]
    public void TryNormalizePrompt_NullInput_ReturnsFalse()
    {
        bool result = ChatHttpClient.TryNormalizePromptForChatApi(null, out string normalized);

        Assert.IsFalse(result);
        Assert.AreEqual("", normalized);
    }

    [Test]
    public void TryNormalizePrompt_EmptyString_ReturnsFalse()
    {
        bool result = ChatHttpClient.TryNormalizePromptForChatApi("", out string normalized);

        Assert.IsFalse(result);
        Assert.AreEqual("", normalized);
    }

    [Test]
    public void TryNormalizePrompt_WhitespaceOnly_ReturnsFalse()
    {
        bool result = ChatHttpClient.TryNormalizePromptForChatApi("   \t\n  ", out string normalized);

        Assert.IsFalse(result);
        Assert.AreEqual("", normalized);
    }

    [Test]
    public void TryNormalizePrompt_NormalMessage_ReturnsTrueAndTrimmed()
    {
        bool result = ChatHttpClient.TryNormalizePromptForChatApi("  hello world  ", out string normalized);

        Assert.IsTrue(result);
        Assert.AreEqual("hello world", normalized);
    }

    [Test]
    public void TryNormalizePrompt_ExactlyMaxLength_ReturnsUnchanged()
    {
        string input = new string('A', 2000);

        bool result = ChatHttpClient.TryNormalizePromptForChatApi(input, out string normalized);

        Assert.IsTrue(result);
        Assert.AreEqual(2000, normalized.Length);
        Assert.AreEqual(input, normalized);
    }

    [Test]
    public void TryNormalizePrompt_ExceedsMaxLength_TruncatesTo2000()
    {
        string input = new string('B', 3000);

        bool result = ChatHttpClient.TryNormalizePromptForChatApi(input, out string normalized);

        Assert.IsTrue(result);
        Assert.AreEqual(2000, normalized.Length);
        Assert.IsTrue(normalized.StartsWith("BBB"));
    }

    [Test]
    public void TryNormalizePrompt_OneChar_ReturnsTrue()
    {
        bool result = ChatHttpClient.TryNormalizePromptForChatApi("X", out string normalized);

        Assert.IsTrue(result);
        Assert.AreEqual("X", normalized);
    }

    // ---------------------------------------------------------------
    //  ResolveChatClientUserId
    // ---------------------------------------------------------------

    [Test]
    public void ResolveChatClientUserId_ReturnsNonNullNonEmpty()
    {
        string userId = ChatHttpClient.ResolveChatClientUserId();

        Assert.IsNotNull(userId);
        Assert.IsNotEmpty(userId);
    }

    [Test]
    public void ResolveChatClientUserId_ReturnsAnonymousStableId()
    {
        ChatHttpClient.ResetAnonymousUserIdForTest();

        string first = ChatHttpClient.ResolveChatClientUserId();
        string second = ChatHttpClient.ResolveChatClientUserId();

        Assert.IsTrue(first.StartsWith("anon-"));
        Assert.AreEqual(first, second);
        Assert.AreEqual(41, first.Length);
    }

    [Test]
    public void GetChatApiTokenHeader_EmptyToken_ReturnsFalse()
    {
        bool hasHeader = ChatHttpClient.TryGetChatApiTokenHeader("", out string headerName, out string headerValue);

        Assert.IsFalse(hasHeader);
        Assert.AreEqual("", headerName);
        Assert.AreEqual("", headerValue);
    }

    [Test]
    public void GetChatApiTokenHeader_Token_ReturnsAuthorizationBearer()
    {
        bool hasHeader = ChatHttpClient.TryGetChatApiTokenHeader("  secret-token  ", out string headerName, out string headerValue);

        Assert.IsTrue(hasHeader);
        Assert.AreEqual("Authorization", headerName);
        Assert.AreEqual("Bearer secret-token", headerValue);
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Test]
    public void Constructor_NullResolveServerUrl_ThrowsArgumentNullException()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();

        Assert.Throws<ArgumentNullException>(() =>
            new ChatHttpClient(null, host, history));
    }

    [Test]
    public void Constructor_NullHost_ThrowsArgumentNullException()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);

        Assert.Throws<ArgumentNullException>(() =>
            new ChatHttpClient(() => "http://localhost", null, history));
    }

    [Test]
    public void Constructor_NullHistory_ThrowsArgumentNullException()
    {
        var host = new StubChatHttpCallbacks();

        Assert.Throws<ArgumentNullException>(() =>
            new ChatHttpClient(() => "http://localhost", host, null));
    }

    [Test]
    public void ResolvedServerUrl_DelegatesToFunc()
    {
        const string expectedUrl = "http://test-server:9999/chat";
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();

        var client = new ChatHttpClient(() => expectedUrl, host, history);

        Assert.AreEqual(expectedUrl, client.ResolvedServerUrl);
    }

    // ---------------------------------------------------------------
    //  LocalLlamaPayload.locale serialization
    // ---------------------------------------------------------------

    [Test]
    public void LocalLlamaPayload_SerializeObject_IncludesLocaleWhenSet()
    {
        var payload = new LocalLlamaPayload
        {
            prompt = "hi",
            message = "hi",
            system = "sys",
            use_tools = true,
            user_id = "anon-test",
            locale = "en",
        };

        string json = JsonConvert.SerializeObject(payload);
        JObject obj = JObject.Parse(json);

        Assert.AreEqual("en", (string)obj["locale"]);
        StringAssert.Contains("\"locale\":\"en\"", json.Replace(" ", ""));
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void EmptyInputAndConnectionError_NonKorean_HaveNoHangul(string locale)
    {
        string empty = CheshireUiStrings.EmptyInputPlease(locale);
        string conn = CheshireUiStrings.ConnectionErrorPrefix(locale);
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
            empty + conn, @"[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7A3]"));
    }

    [Test]
    public void EmptyInputAndConnectionError_Korean_KeepKnownPhrases()
    {
        StringAssert.Contains(
            "내용을 입력해 주세요",
            CheshireUiStrings.EmptyInputPlease(CheshireLocaleResolver.Korean));
        StringAssert.Contains(
            "연결 오류",
            CheshireUiStrings.ConnectionErrorPrefix(CheshireLocaleResolver.Korean));
    }

    [Test]
    public void BaseChatbotRequestInProgress_BlocksAndUnblocksSceneInteractions()
    {
        var go = new UnityEngine.GameObject("TestChatbot");

        try
        {
            var chatbot = go.AddComponent<TestChatbot>();
            var callbacks = (IChatHttpCallbacks)chatbot;

            callbacks.IsRequestInProgress = true;

            Assert.IsTrue(InteractionInputGate.IsBlocked);
            Assert.IsFalse(SceneInteractionController.TryInteract("kitchen_parret"));

            callbacks.IsRequestInProgress = false;

            Assert.IsFalse(InteractionInputGate.IsBlocked);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // ---------------------------------------------------------------
    //  Minimal stub for IChatHttpCallbacks (constructor tests only)
    // ---------------------------------------------------------------

    private sealed class StubChatHttpCallbacks : IChatHttpCallbacks
    {
        public bool IsRequestInProgress { get; set; }
        public bool? UseToolsOverrideForNextRequest { get; set; }

        public string BuildAndComposeSystemPrompt(string userMessage) => "";
        public void AugmentChatPayload(LocalLlamaPayload payload, string userMessage) { }
        public void OnChatHttpWaitStarted() { }
        public void OnChatHttpWaitFinished() { }
        public void OnStreamTextDelta(string delta) { }
        public void SayLine(string message, Action onComplete) => onComplete?.Invoke();
        public UnityEngine.Coroutine StartHostCoroutine(System.Collections.IEnumerator routine) => null;
        public System.Collections.IEnumerator HandleChatbotResponse(
            string responseMessage,
            System.Collections.Generic.List<FunctionCallData> functionCalls)
        {
            yield break;
        }
    }

    private sealed class TestChatbot : BaseChatbot
    {
        protected override string BuildFinalSystemPrompt(string locale) => "";

        protected override IEnumerator HandleChatbotResponse(
            string responseMessage,
            List<FunctionCallData> functionCalls)
        {
            yield break;
        }
    }
}
