using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData) => true;
}
#endif

[Serializable]
public class FunctionCallData
{
    public string name;
    public Dictionary<string, object> arguments;
}

[Serializable]
public class ChatResponseData
{
    public string response;
    public List<FunctionCallData> function_calls;
}

[Serializable]
public class SSEEventData
{
    public string type;
    public string content;
    public string name;
    public Dictionary<string, object> arguments;
    public string full_text;
}

[Serializable]
public class HintRewritePayload
{
    public string hint_id;
    public string item_id;
    public string hint_target;
    public string hint_level;
    public string base_hint;
    public List<string> required_terms = new List<string>();
    public List<string> forbidden_terms = new List<string>();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string fallback_line;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string narrative_seed;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string interaction_type;

    public bool allow_highlight = true;
}

[Serializable]
public class LocalLlamaPayload
{
    public string prompt;
    public string system;
    public bool use_tools = true;
    /// <summary>Gains 등 일부 서버 필수 필드 호환. <see cref="prompt"/>와 동일한 사용자 턴 텍스트.</summary>
    public string message;
    /// <summary>클라이언트 식별(선택 로그용). Gains 등에서 필수인 경우가 있음.</summary>
    public string user_id;

    /// <summary>Canonical player locale (ko|ja|en). Mirrors CheshireLocaleResolver.</summary>
    public string locale;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string rag_profile;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string rag_query;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string current_question_id;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public HintRewritePayload hint_rewrite;
}

/// <summary>
/// Callback contract between <see cref="ChatHttpClient"/> and its MonoBehaviour host.
/// The host supplies prompt composition, coroutine execution, and UI hooks.
/// </summary>
public interface IChatHttpCallbacks
{
    bool IsRequestInProgress { get; set; }
    bool? UseToolsOverrideForNextRequest { get; set; }
    string BuildAndComposeSystemPrompt(string userMessage);
    void AugmentChatPayload(LocalLlamaPayload payload, string userMessage);
    void OnChatHttpWaitStarted();
    void OnChatHttpWaitFinished();
    void OnStreamTextDelta(string delta);
    void SayLine(string message, Action onComplete);
    Coroutine StartHostCoroutine(IEnumerator routine);
    IEnumerator HandleChatbotResponse(string responseMessage, List<FunctionCallData> functionCalls);
}

/// <summary>
/// Plain C# class that owns all HTTP transport for the chat API.
/// Returns <see cref="IEnumerator"/> coroutines — the caller (<see cref="BaseChatbot"/>)
/// starts them via <c>StartCoroutine</c>.
/// </summary>
public sealed class ChatHttpClient
{
    private const int NonStreamingTimeoutSeconds = 60;
    private const int StreamingTimeoutSeconds = 120;
    private const float DefaultRetryDelaySeconds = 0.5f;
    private const string AnonymousUserIdPrefsKey = "ChatHttpClient.AnonymousUserId";

    private readonly Func<string> _resolveServerUrl;
    private readonly IChatHttpCallbacks _host;
    private readonly ChatHistoryManager _history;

    /// <summary>
    /// EditMode seam: when set, replaces UnityWebRequest for each non-streaming attempt (0-based).
    /// </summary>
    internal Func<int, ChatHttpAttemptOutcome> SimulateNonStreamingAttempt;

    /// <summary>
    /// EditMode seam: when set, replaces UnityWebRequest for each streaming attempt (0-based).
    /// </summary>
    internal Func<int, ChatHttpAttemptOutcome> SimulateStreamingAttempt;

    /// <summary>EditMode: override realtime retry delay (0 skips WaitForSecondsRealtime).</summary>
    internal float? RetryDelaySecondsOverrideForTests;

    public ChatHttpClient(
        Func<string> resolveServerUrl,
        IChatHttpCallbacks host,
        ChatHistoryManager history)
    {
        _resolveServerUrl = resolveServerUrl ?? throw new ArgumentNullException(nameof(resolveServerUrl));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    public string ResolvedServerUrl => _resolveServerUrl();

    private float RetryDelaySeconds =>
        RetryDelaySecondsOverrideForTests ?? DefaultRetryDelaySeconds;

    // ---------------------------------------------------------------
    //  Static helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// 백엔드 <c>ChatRequest.prompt</c> (max 2 000). 공백만이면 false → API 호출하지 않음.
    /// </summary>
    public static bool TryNormalizePromptForChatApi(string userMessage, out string normalized)
    {
        normalized = (userMessage ?? "").Trim();
        if (normalized.Length == 0)
            return false;
        const int MaxChatPromptChars = 2000;
        if (normalized.Length > MaxChatPromptChars)
            normalized = normalized.Substring(0, MaxChatPromptChars);
        return true;
    }

    /// <summary>일부 백엔드(Gains 등)가 요구하는 <c>user_id</c>.</summary>
    public static string ResolveChatClientUserId()
    {
        string existing = PlayerPrefs.GetString(AnonymousUserIdPrefsKey, "");
        if (!string.IsNullOrEmpty(existing))
            return existing;

        string created = "anon-" + Guid.NewGuid().ToString("D");
        PlayerPrefs.SetString(AnonymousUserIdPrefsKey, created);
        PlayerPrefs.Save();
        return created;
    }

    internal static void ResetAnonymousUserIdForTest()
    {
        PlayerPrefs.DeleteKey(AnonymousUserIdPrefsKey);
        PlayerPrefs.Save();
    }

    public static bool TryGetChatApiTokenHeader(string token, out string headerName, out string headerValue)
    {
        string trimmed = (token ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            headerName = "";
            headerValue = "";
            return false;
        }

        headerName = "Authorization";
        headerValue = "Bearer " + trimmed;
        return true;
    }

    public static void AttachCertificateBypass(UnityWebRequest request)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ServerConfig.GetOrCreate().BypassTlsCertificate)
            request.certificateHandler = new BypassCertificate();
#endif
    }

    // ---------------------------------------------------------------
    //  Non-streaming /chat
    // ---------------------------------------------------------------

    public IEnumerator GetGPTResponse(string userMessage)
    {
        if (_host.IsRequestInProgress) yield break;
        _host.IsRequestInProgress = true;

        // One locale snapshot for player-facing errors and the outbound payload.
        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        bool waitStarted = false;

        try
        {
            if (!TryNormalizePromptForChatApi(userMessage, out string promptForApi))
            {
                _host.SayLine(CheshireUiStrings.EmptyInputPlease(locale), null);
                yield break;
            }

            _history.AddMessage("user", promptForApi);
            string finalSystemPrompt = _host.BuildAndComposeSystemPrompt(promptForApi);

            bool useTools = _host.UseToolsOverrideForNextRequest ?? true;
            _host.UseToolsOverrideForNextRequest = null;

            var payload = new LocalLlamaPayload
            {
                prompt = promptForApi,
                message = promptForApi,
                system = finalSystemPrompt,
                use_tools = useTools,
                user_id = ResolveChatClientUserId(),
                locale = locale,
            };
            _host.AugmentChatPayload(payload, promptForApi);
            string payloadJson = JsonConvert.SerializeObject(payload);

            _host.OnChatHttpWaitStarted();
            waitStarted = true;

            string chatbotResponse = null;
            var functionCalls = new List<FunctionCallData>();

            yield return ExecuteNonStreamingWithRetry(
                payloadJson,
                locale,
                (text, calls) =>
                {
                    chatbotResponse = text;
                    functionCalls = calls;
                });

            yield return _host.StartHostCoroutine(
                _host.HandleChatbotResponse(chatbotResponse, functionCalls));
        }
        finally
        {
            if (waitStarted)
                _host.OnChatHttpWaitFinished();
            _host.IsRequestInProgress = false;
        }
    }

    private IEnumerator ExecuteNonStreamingWithRetry(
        string payloadJson,
        string locale,
        Action<string, List<FunctionCallData>> onComplete)
    {
        string chatbotResponse = null;
        var functionCalls = new List<FunctionCallData>();

        for (int attempt = 0; ; attempt++)
        {
            ChatHttpAttemptOutcome outcome = default;
            if (SimulateNonStreamingAttempt != null)
            {
                outcome = SimulateNonStreamingAttempt(attempt);
                yield return null;
            }
            else
            {
                using (var request = CreateJsonPostRequest(ResolvedServerUrl, payloadJson, NonStreamingTimeoutSeconds))
                {
                    yield return request.SendWebRequest();
                    outcome = CaptureOutcome(request);
                }
            }

            if (outcome.Result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<ChatResponseData>(outcome.Body);
                    chatbotResponse = ChatResponseDisplayText.StripInlineFunctionTags(parsed.response ?? "");
                    if (parsed.function_calls != null)
                        functionCalls = parsed.function_calls;
                }
                catch (Exception e)
                {
                    Debug.LogError("Response parse error: " + e.Message);
                    chatbotResponse = ChatResponseDisplayText.StripInlineFunctionTags(outcome.Body);
                }

                _history.AddMessage("assistant", chatbotResponse);
                break;
            }

            if (ChatRequestRecoveryPolicy.ShouldRetry(outcome.Result, outcome.ResponseCode, attempt))
            {
                _host.SayLine(CheshireUiStrings.ReconnectRetrying(locale), null);
                yield return WaitForRetryDelay();
                continue;
            }

            chatbotResponse = CheshireUiStrings.ConnectionErrorPrefix(locale) + outcome.Error;
            break;
        }

        onComplete(chatbotResponse, functionCalls);
    }

    // ---------------------------------------------------------------
    //  SSE streaming /chat/stream
    // ---------------------------------------------------------------

    public IEnumerator GetGPTResponseStreaming(string userMessage)
    {
        if (_host.IsRequestInProgress) yield break;
        _host.IsRequestInProgress = true;

        // One locale snapshot for player-facing errors and the outbound payload.
        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        bool waitStarted = false;

        try
        {
            if (!TryNormalizePromptForChatApi(userMessage, out string promptForApi))
            {
                _host.SayLine(CheshireUiStrings.EmptyInputPlease(locale), null);
                yield break;
            }

            _history.AddMessage("user", promptForApi);
            string finalSystemPrompt = _host.BuildAndComposeSystemPrompt(promptForApi);

            bool useTools = _host.UseToolsOverrideForNextRequest ?? true;
            _host.UseToolsOverrideForNextRequest = null;

            var payload = new LocalLlamaPayload
            {
                prompt = promptForApi,
                message = promptForApi,
                system = finalSystemPrompt,
                use_tools = useTools,
                user_id = ResolveChatClientUserId(),
                locale = locale,
            };
            _host.AugmentChatPayload(payload, promptForApi);
            string payloadJson = JsonConvert.SerializeObject(payload);

            string streamUrl = ResolvedServerUrl.Replace("/chat", "/chat/stream");

            _host.OnChatHttpWaitStarted();
            waitStarted = true;

            string responseText = null;
            var functionCalls = new List<FunctionCallData>();

            yield return ExecuteStreamingWithRetry(
                streamUrl,
                payloadJson,
                locale,
                (text, calls) =>
                {
                    responseText = text;
                    functionCalls = calls;
                });

            yield return _host.StartHostCoroutine(
                _host.HandleChatbotResponse(responseText, functionCalls));
        }
        finally
        {
            if (waitStarted)
                _host.OnChatHttpWaitFinished();
            _host.IsRequestInProgress = false;
        }
    }

    private IEnumerator ExecuteStreamingWithRetry(
        string streamUrl,
        string payloadJson,
        string locale,
        Action<string, List<FunctionCallData>> onComplete)
    {
        string responseText = null;
        var functionCalls = new List<FunctionCallData>();

        for (int attempt = 0; ; attempt++)
        {
            var fullText = new StringBuilder();
            functionCalls = new List<FunctionCallData>();
            bool transportFailed = false;
            string transportError = "";
            UnityWebRequest.Result transportResult = UnityWebRequest.Result.Success;
            long transportCode = 0;

            if (SimulateStreamingAttempt != null)
            {
                ChatHttpAttemptOutcome outcome = SimulateStreamingAttempt(attempt);
                yield return null;
                if (outcome.Result != UnityWebRequest.Result.Success)
                {
                    transportFailed = true;
                    transportResult = outcome.Result;
                    transportCode = outcome.ResponseCode;
                    transportError = outcome.Error;
                }
                else
                {
                    fullText.Append(outcome.Body ?? "");
                }
            }
            else
            {
                using (var request = CreateJsonPostRequest(streamUrl, payloadJson, StreamingTimeoutSeconds))
                {
                    request.SetRequestHeader("Accept", "text/event-stream");

                    var op = request.SendWebRequest();
                    int lastProcessedIndex = 0;
                    bool done = false;

                    while (!done)
                    {
                        yield return null;

                        if (request.downloadHandler != null)
                        {
                            string allData = request.downloadHandler.text;
                            if (allData.Length > lastProcessedIndex)
                            {
                                string newData = allData.Substring(lastProcessedIndex);
                                lastProcessedIndex = allData.Length;

                                string[] lines = newData.Split('\n');
                                foreach (string line in lines)
                                {
                                    if (!line.StartsWith("data: ")) continue;
                                    string json = line.Substring(6).Trim();
                                    if (string.IsNullOrEmpty(json)) continue;

                                    SSEEventData evt = null;
                                    try { evt = JsonConvert.DeserializeObject<SSEEventData>(json); }
                                    catch { continue; }
                                    if (evt == null) continue;

                                    switch (evt.type)
                                    {
                                        case "text_delta":
                                            if (evt.content != null) fullText.Append(evt.content);
                                            _host.OnStreamTextDelta(evt.content);
                                            break;

                                        case "function_call":
                                            functionCalls.Add(new FunctionCallData
                                            {
                                                name = evt.name,
                                                arguments = evt.arguments
                                            });
                                            break;

                                        case "done":
                                            if (!string.IsNullOrEmpty(evt.full_text))
                                                fullText = new StringBuilder(evt.full_text);
                                            done = true;
                                            break;

                                        case "error":
                                            Debug.LogError("SSE error: " + evt.content);
                                            done = true;
                                            break;
                                    }
                                }
                            }
                        }

                        if (op.isDone && !done) done = true;
                    }

                    if (request.result != UnityWebRequest.Result.Success && fullText.Length == 0)
                    {
                        transportFailed = true;
                        transportResult = request.result;
                        transportCode = request.responseCode;
                        transportError = request.error ?? "";
                    }
                }
            }

            if (!transportFailed)
            {
                responseText = ChatResponseDisplayText.StripInlineFunctionTags(fullText.ToString());
                _history.AddMessage("assistant", responseText);
                break;
            }

            if (ChatRequestRecoveryPolicy.ShouldRetry(transportResult, transportCode, attempt))
            {
                _host.SayLine(CheshireUiStrings.ReconnectRetrying(locale), null);
                yield return WaitForRetryDelay();
                continue;
            }

            responseText = CheshireUiStrings.ConnectionErrorPrefix(locale) + transportError;
            break;
        }

        onComplete(responseText, functionCalls);
    }

    private IEnumerator WaitForRetryDelay()
    {
        float delay = RetryDelaySeconds;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return null;
    }

    private UnityWebRequest CreateJsonPostRequest(string url, string payloadJson, int timeoutSeconds)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        AttachChatApiToken(request);
        AttachCertificateBypass(request);
        request.timeout = timeoutSeconds;
        return request;
    }

    private static ChatHttpAttemptOutcome CaptureOutcome(UnityWebRequest request)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : "";
        return new ChatHttpAttemptOutcome(
            request.result,
            request.responseCode,
            request.error ?? "",
            body);
    }

    private static void AttachChatApiToken(UnityWebRequest request)
    {
        if (TryGetChatApiTokenHeader(ServerConfig.GetOrCreate().ChatApiToken, out string headerName, out string headerValue))
            request.SetRequestHeader(headerName, headerValue);
    }
}
