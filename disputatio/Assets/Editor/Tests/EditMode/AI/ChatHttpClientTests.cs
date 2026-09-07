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

    [Test]
    public void ResolveUseTools_CheshireDialogue_AlwaysDisablesGameTools()
    {
        Assert.IsFalse(ChatHttpClient.ResolveUseTools(ragProfile: null, requestedUseTools: true));
        Assert.IsFalse(ChatHttpClient.ResolveUseTools(ragProfile: "", requestedUseTools: true));
        Assert.IsFalse(ChatHttpClient.ResolveUseTools(ragProfile: "cheshire", requestedUseTools: true));
    }

    [Test]
    public void ResolveUseTools_TutorProfile_HonorsRequestedFlag()
    {
        Assert.IsTrue(ChatHttpClient.ResolveUseTools(ragProfile: "tutor", requestedUseTools: true));
        Assert.IsFalse(ChatHttpClient.ResolveUseTools(ragProfile: "tutor", requestedUseTools: false));
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
        string reconnect = CheshireUiStrings.ReconnectRetrying(locale);
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
            empty + conn + reconnect, @"[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7A3]"));
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
        StringAssert.Contains(
            "다시 시도",
            CheshireUiStrings.ReconnectRetrying(CheshireLocaleResolver.Korean));
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
    //  Transport recovery (fake attempt seam)
    // ---------------------------------------------------------------

    [Test]
    public void GetGPTResponse_TimeoutThenSuccess_RetriesOnce_AndClearsInProgress()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
        };

        int attempts = 0;
        client.SimulateNonStreamingAttempt = attempt =>
        {
            attempts++;
            if (attempt == 0)
            {
                return new ChatHttpAttemptOutcome(
                    UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
                    responseCode: 0,
                    error: "Request timeout",
                    body: "");
            }

            return new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "{\"response\":\"ok after retry\",\"function_calls\":[]}");
        };

        DrainEnumerator(client.GetGPTResponse("hello"));

        Assert.AreEqual(2, attempts);
        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
        Assert.IsFalse(host.IsRequestInProgress);
        Assert.AreEqual(1, host.HandledResponses.Count);
        Assert.AreEqual("ok after retry", host.HandledResponses[0]);
        Assert.AreEqual(1, host.SaidLines.Count);
        Assert.AreEqual(
            CheshireUiStrings.ReconnectRetrying(CheshireLocaleResolver.ResolveCurrentLocale()),
            host.SaidLines[0]);
    }

    [Test]
    public void GetGPTResponse_TwoFailures_ShowsLocalizedError_AndClearsInProgress()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
        };

        int attempts = 0;
        client.SimulateNonStreamingAttempt = _ =>
        {
            attempts++;
            return new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
                responseCode: 0,
                error: "Cannot connect",
                body: "");
        };

        DrainEnumerator(client.GetGPTResponse("hello"));

        Assert.AreEqual(2, attempts);
        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
        Assert.IsFalse(host.IsRequestInProgress);
        Assert.AreEqual(1, host.HandledResponses.Count);
        StringAssert.StartsWith(
            CheshireUiStrings.ConnectionErrorPrefix(CheshireLocaleResolver.ResolveCurrentLocale()),
            host.HandledResponses[0]);
        StringAssert.Contains("Cannot connect", host.HandledResponses[0]);
    }

    [Test]
    public void GetGPTResponse_HandlerException_StillClearsInProgressAndWait()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks
        {
            ThrowOnHandleResponse = true,
        };
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
            SimulateNonStreamingAttempt = _ => new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "{\"response\":\"hi\",\"function_calls\":[]}"),
        };

        Assert.Throws<InvalidOperationException>(() => DrainEnumerator(client.GetGPTResponse("hello")));

        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
        Assert.IsFalse(host.IsRequestInProgress);
    }

    [Test]
    public void GetGPTResponseStreaming_TimeoutThenSuccess_RetriesOnce_AndClearsInProgress()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
        };

        int attempts = 0;
        client.SimulateStreamingAttempt = attempt =>
        {
            attempts++;
            if (attempt == 0)
            {
                return new ChatHttpAttemptOutcome(
                    UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
                    responseCode: 0,
                    error: "Request timeout",
                    body: "");
            }

            // Streaming seam treats Body as assembled SSE full text (not ChatResponse JSON).
            return new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "ok after retry");
        };

        DrainEnumerator(client.GetGPTResponseStreaming("hello"));

        Assert.AreEqual(2, attempts);
        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
        Assert.IsFalse(host.IsRequestInProgress);
        Assert.AreEqual(1, host.HandledResponses.Count);
        Assert.AreEqual("ok after retry", host.HandledResponses[0]);
        Assert.AreEqual(1, host.SaidLines.Count);
        Assert.AreEqual(
            CheshireUiStrings.ReconnectRetrying(CheshireLocaleResolver.ResolveCurrentLocale()),
            host.SaidLines[0]);
    }

    [Test]
    public void GetGPTResponseStreaming_TwoFailures_ShowsLocalizedError_AndClearsInProgress()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
        };

        int attempts = 0;
        client.SimulateStreamingAttempt = _ =>
        {
            attempts++;
            return new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
                responseCode: 0,
                error: "Cannot connect",
                body: "");
        };
        client.SimulateNonStreamingAttempt = _ => new ChatHttpAttemptOutcome(
            UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
            responseCode: 0,
            error: "Cannot connect",
            body: "");

        DrainEnumerator(client.GetGPTResponseStreaming("hello"));

        Assert.AreEqual(2, attempts);
        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
        Assert.IsFalse(host.IsRequestInProgress);
        Assert.AreEqual(1, host.HandledResponses.Count);
        StringAssert.StartsWith(
            CheshireUiStrings.ConnectionErrorPrefix(CheshireLocaleResolver.ResolveCurrentLocale()),
            host.HandledResponses[0]);
        StringAssert.Contains("Cannot connect", host.HandledResponses[0]);
    }

    [Test]
    public void FetchRootStatus_UsesSimulateSeam_AndDoesNotMarkRequestInProgress()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();
        var client = new ChatHttpClient(() => "http://127.0.0.1:8000/chat", host, history)
        {
            SimulateRootAttempt = () => new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "{\"status\":\"degraded\",\"local_runtime\":{\"model_available\":false}}"),
        };

        long code = 0;
        string body = null;
        DrainEnumerator(client.FetchRootStatus((statusCode, json) =>
        {
            code = statusCode;
            body = json;
        }));

        Assert.AreEqual(200, code);
        StringAssert.Contains("degraded", body);
        Assert.IsFalse(host.IsRequestInProgress);
        Assert.AreEqual(0, host.WaitStartedCount);
    }

    [Test]
    public void NaiveSseSplit_LosesEventWhenJsonIsSplitAcrossChunks()
    {
        string part1 = "data: {\"type\":\"text_delta\",\"content\":\"He";
        string part2 = "llo\"}\n\n";

        List<SSEEventData> events = ParseSseChunksNaively(part1, part2);

        Assert.AreEqual(
            0,
            events.Count,
            "Split('\\n') on each Unity download-buffer increment drops a JSON event split across reads.");
    }

    [Test]
    public void ChatSseStreamParser_ReassemblesEventSplitAcrossChunks()
    {
        var parser = new ChatSseStreamParser();

        IReadOnlyList<SSEEventData> first = parser.Push(
            "data: {\"type\":\"text_delta\",\"content\":\"He");
        Assert.AreEqual(0, first.Count);

        IReadOnlyList<SSEEventData> second = parser.Push("llo\"}\n\n");
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual("text_delta", second[0].type);
        Assert.AreEqual("Hello", second[0].content);
    }

    [Test]
    public void ChatSseStreamParser_ParsesCrlfAndMultipleCompleteEvents()
    {
        var parser = new ChatSseStreamParser();
        IReadOnlyList<SSEEventData> events = parser.Push(
            "data: {\"type\":\"text_delta\",\"content\":\"A\"}\r\n\r\n" +
            "data: {\"type\":\"done\",\"full_text\":\"A\"}\r\n\r\n");

        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("text_delta", events[0].type);
        Assert.AreEqual("A", events[0].content);
        Assert.AreEqual("done", events[1].type);
        Assert.AreEqual("A", events[1].full_text);
    }

    [Test]
    public void ChatSseStreamParser_FlushIgnoresIncompleteLine()
    {
        var parser = new ChatSseStreamParser();
        parser.Push("data: {\"type\":\"text_delta\",\"content\":\"no-newline");
        IReadOnlyList<SSEEventData> flushed = parser.Flush();
        Assert.AreEqual(0, flushed.Count);
    }

    [Test]
    public void GetGPTResponseStreaming_StreamRetriesExhausted_FallsBackToNonStreaming()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks();
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
        };

        int streamAttempts = 0;
        client.SimulateStreamingAttempt = _ =>
        {
            streamAttempts++;
            return new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
                responseCode: 0,
                error: "Cannot connect",
                body: "");
        };

        int nonStreamAttempts = 0;
        client.SimulateNonStreamingAttempt = _ =>
        {
            nonStreamAttempts++;
            return new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "{\"response\":\"fallback ok\",\"function_calls\":[]}");
        };

        DrainEnumerator(client.GetGPTResponseStreaming("hello"));

        Assert.AreEqual(2, streamAttempts);
        Assert.AreEqual(1, nonStreamAttempts);
        Assert.AreEqual(1, host.HandledResponses.Count);
        Assert.AreEqual("fallback ok", host.HandledResponses[0]);
        Assert.IsFalse(host.IsRequestInProgress);
    }

    [Test]
    public void GetGPTResponseStreaming_HandlerException_StillClearsInProgressAndWait()
    {
        var history = new ChatHistoryManager(appendCommonVoice: false);
        var host = new StubChatHttpCallbacks
        {
            ThrowOnHandleResponse = true,
        };
        var client = new ChatHttpClient(() => "http://test.local/chat", host, history)
        {
            RetryDelaySecondsOverrideForTests = 0f,
            SimulateStreamingAttempt = _ => new ChatHttpAttemptOutcome(
                UnityEngine.Networking.UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "hi"),
        };

        Assert.Throws<InvalidOperationException>(
            () => DrainEnumerator(client.GetGPTResponseStreaming("hello")));

        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
        Assert.IsFalse(host.IsRequestInProgress);
    }

    /// <summary>
    /// Mirrors the pre-fix Unity loop: split each download increment on '\n' with no pending buffer.
    /// </summary>
    private static List<SSEEventData> ParseSseChunksNaively(params string[] chunks)
    {
        var events = new List<SSEEventData>();
        foreach (string newData in chunks)
        {
            foreach (string line in newData.Split('\n'))
            {
                if (!line.StartsWith("data: "))
                    continue;
                string json = line.Substring(6).Trim();
                if (string.IsNullOrEmpty(json))
                    continue;
                try
                {
                    SSEEventData evt = JsonConvert.DeserializeObject<SSEEventData>(json);
                    if (evt != null)
                        events.Add(evt);
                }
                catch
                {
                    // Incomplete JSON in this chunk is dropped, matching the production bug.
                }
            }
        }

        return events;
    }

    private static void DrainEnumerator(IEnumerator routine, int maxSteps = 200)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int steps = 0;
        while (stack.Count > 0)
        {
            if (steps++ > maxSteps)
                throw new InvalidOperationException("Enumerator did not complete within step budget.");

            IEnumerator current = stack.Peek();
            bool moved;
            try
            {
                moved = current.MoveNext();
            }
            catch
            {
                while (stack.Count > 0)
                    (stack.Pop() as IDisposable)?.Dispose();

                throw;
            }

            if (!moved)
            {
                stack.Pop();
                continue;
            }

            if (current.Current is IEnumerator nested)
                stack.Push(nested);
        }
    }

    // ---------------------------------------------------------------
    //  Minimal stub for IChatHttpCallbacks (constructor + transport tests)
    // ---------------------------------------------------------------

    private sealed class StubChatHttpCallbacks : IChatHttpCallbacks
    {
        public bool IsRequestInProgress { get; set; }
        public bool? UseToolsOverrideForNextRequest { get; set; }
        public int WaitStartedCount { get; private set; }
        public int WaitFinishedCount { get; private set; }
        public List<string> SaidLines { get; } = new List<string>();
        public List<string> HandledResponses { get; } = new List<string>();
        public bool ThrowOnHandleResponse { get; set; }

        public string BuildAndComposeSystemPrompt(string userMessage) => "sys";
        public void AugmentChatPayload(LocalLlamaPayload payload, string userMessage) { }

        public void OnChatHttpWaitStarted() => WaitStartedCount++;
        public void OnChatHttpWaitFinished() => WaitFinishedCount++;
        public void OnStreamTextDelta(string delta) { }

        public void SayLine(string message, Action onComplete)
        {
            SaidLines.Add(message ?? "");
            onComplete?.Invoke();
        }

        public UnityEngine.Coroutine StartHostCoroutine(IEnumerator routine)
        {
            if (routine == null)
                return null;

            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }

            return null;
        }

        public IEnumerator HandleChatbotResponse(
            string responseMessage,
            List<FunctionCallData> functionCalls)
        {
            if (ThrowOnHandleResponse)
                throw new InvalidOperationException("handler boom");

            HandledResponses.Add(responseMessage ?? "");
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
