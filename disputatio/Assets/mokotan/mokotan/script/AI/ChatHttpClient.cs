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
    private const string AnonymousUserIdPrefsKey = "ChatHttpClient.AnonymousUserId";

    private readonly Func<string> _resolveServerUrl;
    private readonly IChatHttpCallbacks _host;
    private readonly ChatHistoryManager _history;

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

        if (!TryNormalizePromptForChatApi(userMessage, out string promptForApi))
        {
            _host.IsRequestInProgress = false;
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

        using (var request = new UnityWebRequest(ResolvedServerUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            AttachChatApiToken(request);
            AttachCertificateBypass(request);
            request.timeout = NonStreamingTimeoutSeconds;

            _host.OnChatHttpWaitStarted();
            try
            {
                yield return request.SendWebRequest();
            }
            finally
            {
                _host.OnChatHttpWaitFinished();
            }

            string chatbotResponse;
            var functionCalls = new List<FunctionCallData>();

            if (request.result != UnityWebRequest.Result.Success)
            {
                chatbotResponse = CheshireUiStrings.ConnectionErrorPrefix(locale) + request.error;
            }
            else
            {
                string rawJson = request.downloadHandler.text;
                try
                {
                    var parsed = JsonConvert.DeserializeObject<ChatResponseData>(rawJson);
                    chatbotResponse = ChatResponseDisplayText.StripInlineFunctionTags(parsed.response ?? "");
                    if (parsed.function_calls != null)
                        functionCalls = parsed.function_calls;
                }
                catch (Exception e)
                {
                    Debug.LogError("Response parse error: " + e.Message);
                    chatbotResponse = ChatResponseDisplayText.StripInlineFunctionTags(rawJson);
                }
                _history.AddMessage("assistant", chatbotResponse);
            }

            yield return _host.StartHostCoroutine(
                _host.HandleChatbotResponse(chatbotResponse, functionCalls));
            _host.IsRequestInProgress = false;
        }
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

        if (!TryNormalizePromptForChatApi(userMessage, out string promptForApi))
        {
            _host.IsRequestInProgress = false;
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

        using (var request = new UnityWebRequest(streamUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "text/event-stream");
            AttachChatApiToken(request);
            AttachCertificateBypass(request);
            request.timeout = StreamingTimeoutSeconds;

            var fullText = new StringBuilder();
            var functionCalls = new List<FunctionCallData>();

            _host.OnChatHttpWaitStarted();
            try
            {
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
            }
            finally
            {
                _host.OnChatHttpWaitFinished();
            }

            string responseText = ChatResponseDisplayText.StripInlineFunctionTags(fullText.ToString());
            _history.AddMessage("assistant", responseText);

            yield return _host.StartHostCoroutine(
                _host.HandleChatbotResponse(responseText, functionCalls));
            _host.IsRequestInProgress = false;
        }
    }

    private static void AttachChatApiToken(UnityWebRequest request)
    {
        if (TryGetChatApiTokenHeader(ServerConfig.GetOrCreate().ChatApiToken, out string headerName, out string headerValue))
            request.SetRequestHeader(headerName, headerValue);
    }
}
