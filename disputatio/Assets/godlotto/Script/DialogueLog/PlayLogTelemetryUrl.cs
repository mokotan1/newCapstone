using System;

/// <summary>
/// 채팅 URL에서 텔레메트리 수집 엔드포인트(/telemetry) URL을 유도한다.
/// 파일·네트워크 비의존 순수 로직.
/// </summary>
public static class PlayLogTelemetryUrl
{
    const string ChatSuffix = "/chat";
    const string TelemetrySuffix = "/telemetry";

    /// <summary>
    /// <paramref name="overrideUrl"/>이 있으면 그대로, 없으면 <paramref name="chatUrl"/>의
    /// 끝 <c>/chat</c>을 <c>/telemetry</c>로 바꾼다(접미사가 없으면 덧붙인다).
    /// chatUrl이 비면 빈 문자열을 반환한다(호출 측에서 전송 생략).
    /// </summary>
    public static string Resolve(string chatUrl, string overrideUrl = null)
    {
        string ov = (overrideUrl ?? string.Empty).Trim();
        if (ov.Length > 0)
            return ov;

        string baseUrl = (chatUrl ?? string.Empty).Trim();
        if (baseUrl.Length == 0)
            return string.Empty;

        baseUrl = baseUrl.TrimEnd('/');
        if (baseUrl.EndsWith(ChatSuffix, StringComparison.Ordinal))
            return baseUrl.Substring(0, baseUrl.Length - ChatSuffix.Length) + TelemetrySuffix;

        return baseUrl + TelemetrySuffix;
    }
}
