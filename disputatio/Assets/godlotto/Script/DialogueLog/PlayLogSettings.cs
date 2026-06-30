using UnityEngine;

/// <summary>
/// 플레이 CSV 로그 옵션. <c>Resources/PlayLogSettings</c> 에셋이 없으면 런타임 기본값을 사용한다.
/// </summary>
[CreateAssetMenu(fileName = "PlayLogSettings", menuName = "Disputatio/Play Log Settings")]
public class PlayLogSettings : ScriptableObject
{
    private const string ResourcePath = "PlayLogSettings";

    [Tooltip("false면 CSV 파일에 행을 쓰지 않습니다.")]
    [SerializeField] private bool enableCsvLogging = true;

    [Tooltip("false면 user_message·bot_response 컬럼을 빈 문자열로 기록합니다(개인정보·본문 비활성화).")]
    [SerializeField] private bool includeMessageContent = true;

    [Tooltip("persistentDataPath 아래 하위 폴더 이름.")]
    [SerializeField] private string logDirectoryName = "PlayLogs";

    [Tooltip("세션당 CSV 파일명. {session_id} 플레이스홀더 지원.")]
    [SerializeField] private string logFileNamePattern = "play_log_{session_id}.csv";

    [Header("서버 텔레메트리 업로드 (POST /telemetry)")]
    [Tooltip("true면 누적 이벤트를 서버 /telemetry 로 주기 전송합니다. 서버 배포 후 켜세요.")]
    [SerializeField] private bool enableTelemetryUpload = false;

    [Tooltip("비우면 ServerConfig.ChatUrl 의 /chat 을 /telemetry 로 바꿔 사용합니다.")]
    [SerializeField] private string telemetryUrlOverride = "";

    [Tooltip("전송 주기(초).")]
    [SerializeField] private float telemetryFlushIntervalSeconds = 15f;

    [Tooltip("한 번에 보낼 최대 이벤트 수(서버 telemetry_max_batch 이하).")]
    [SerializeField] private int telemetryMaxBatch = 100;

    [Tooltip("메모리에 보관할 최대 대기 이벤트 수(초과 시 가장 오래된 것부터 폐기).")]
    [SerializeField] private int telemetryMaxBufferedEvents = 1000;

    public bool EnableCsvLogging => enableCsvLogging;
    public bool IncludeMessageContent => includeMessageContent;
    public string LogDirectoryName => string.IsNullOrWhiteSpace(logDirectoryName) ? "PlayLogs" : logDirectoryName.Trim();

    public string LogFileNamePattern =>
        string.IsNullOrWhiteSpace(logFileNamePattern) ? "play_log_{session_id}.csv" : logFileNamePattern.Trim();

    public bool EnableTelemetryUpload => enableTelemetryUpload;
    public string TelemetryUrlOverride => telemetryUrlOverride ?? string.Empty;
    public float TelemetryFlushIntervalSeconds => telemetryFlushIntervalSeconds > 0f ? telemetryFlushIntervalSeconds : 15f;
    public int TelemetryMaxBatch => telemetryMaxBatch > 0 ? telemetryMaxBatch : 100;
    public int TelemetryMaxBufferedEvents => telemetryMaxBufferedEvents > 0 ? telemetryMaxBufferedEvents : 1000;

    private static PlayLogSettings _cached;

    internal static void ResetCacheForTest() => _cached = null;

    public static PlayLogSettings GetOrCreate()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<PlayLogSettings>(ResourcePath);
        if (_cached == null)
            _cached = CreateInstance<PlayLogSettings>();

        return _cached;
    }
}
