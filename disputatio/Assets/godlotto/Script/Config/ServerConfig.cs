using UnityEngine;

/// <summary>
/// Runtime-editable server configuration.
/// Place a single asset at <c>Resources/ServerConfig</c> so it is loadable via
/// <see cref="GetOrCreate"/>. Inspector overrides per-chatbot remain possible via
/// <see cref="BaseChatbot"/>'s serialized URL field.
/// </summary>
[CreateAssetMenu(fileName = "ServerConfig", menuName = "Disputatio/Server Config")]
public class ServerConfig : ScriptableObject
{
    private const string ResourcePath = "ServerConfig";

    public const string LocalLoopbackChatUrl = "http://127.0.0.1:8000/chat";
    public const string DefaultCloudChatUrl = "http://54.156.51.119:8000/chat";

    [Header("Chat API")]
    [Tooltip("When enabled, ChatUrl is always the local FastAPI loopback endpoint.")]
    [SerializeField] private bool useLocalLoopback = true;

    [Tooltip("Cloud/remote chat endpoint used when Use Local Loopback is off.")]
    [SerializeField] private string chatUrl = DefaultCloudChatUrl;

    [Header("Security")]
    [Tooltip("When true, TLS certificate validation is skipped (dev/staging only).")]
    [SerializeField] private bool bypassTlsCertificate = true;

    [Tooltip("Optional shared token for the chat API. Leave empty for local development.")]
    [SerializeField] private string chatApiToken = "";

    public bool UseLocalLoopback => useLocalLoopback;
    public string ChatUrl => useLocalLoopback ? LocalLoopbackChatUrl : chatUrl;
    public bool BypassTlsCertificate => bypassTlsCertificate;
    public string ChatApiToken => chatApiToken;

    internal void ApplyChatEndpointForTest(bool useLocalLoopback, string chatUrl)
    {
        this.useLocalLoopback = useLocalLoopback;
        this.chatUrl = chatUrl;
    }

    private static ServerConfig _cached;

    internal static void ResetCacheForTest() => _cached = null;

    public static ServerConfig GetOrCreate()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<ServerConfig>(ResourcePath);

        if (_cached == null)
        {
            _cached = CreateInstance<ServerConfig>();
            GameLog.LogWarning($"[ServerConfig] Resources/{ResourcePath} not found — using runtime defaults.");
        }

        return _cached;
    }
}
