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

    [Header("Chat API")]
    [Tooltip("Base chat endpoint (e.g. http://host:port/chat).")]
    [SerializeField] private string chatUrl = "http://15.134.24.132:8000/chat";

    [Header("Security")]
    [Tooltip("When true, TLS certificate validation is skipped (dev/staging only).")]
    [SerializeField] private bool bypassTlsCertificate = true;

    [Tooltip("Optional shared token for the chat API. Leave empty for local development.")]
    [SerializeField] private string chatApiToken = "";

    public string ChatUrl => chatUrl;
    public bool BypassTlsCertificate => bypassTlsCertificate;
    public string ChatApiToken => chatApiToken;

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
