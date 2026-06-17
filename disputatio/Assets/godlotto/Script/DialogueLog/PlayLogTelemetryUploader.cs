using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// <see cref="PlayLogRecorder"/>가 버퍼링한 플레이 로그를 주기적으로 서버
/// <c>POST /telemetry</c>로 전송한다. 전송 실패분은 버퍼에 되돌려 재시도한다.
/// 순수 로직(URL·payload·buffer)은 별도 클래스로 분리되어 테스트되며,
/// 이 클래스는 코루틴·HTTP 글루만 담당한다(SRP).
/// </summary>
public sealed class PlayLogTelemetryUploader : MonoBehaviour
{
    const int UploadTimeoutSeconds = 30;

    static PlayLogTelemetryUploader instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (!Application.isPlaying || instance != null)
            return;

        if (!PlayLogRecorder.TelemetryUploadEnabled)
            return;

        var go = new GameObject(nameof(PlayLogTelemetryUploader));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PlayLogTelemetryUploader>();
    }

    IEnumerator Start()
    {
        while (true)
        {
            float interval = PlayLogSettings.GetOrCreate().TelemetryFlushIntervalSeconds;
            yield return new WaitForSecondsRealtime(interval);
            yield return FlushOnce();
        }
    }

    IEnumerator FlushOnce()
    {
        if (PlayLogRecorder.TelemetryPendingCount == 0)
            yield break;

        PlayLogSettings settings = PlayLogSettings.GetOrCreate();
        List<PlayLogEvent> batch = PlayLogRecorder.DrainTelemetryBatch(settings.TelemetryMaxBatch);
        if (batch.Count == 0)
            yield break;

        string url = PlayLogTelemetryUrl.Resolve(
            ServerConfig.GetOrCreate().ChatUrl,
            settings.TelemetryUrlOverride);

        if (string.IsNullOrEmpty(url))
        {
            // 전송 대상이 없으면 버려지지 않도록 되돌린다.
            PlayLogRecorder.RequeueTelemetry(batch);
            yield break;
        }

        string json = PlayLogTelemetryPayload.BuildJson(batch, settings.IncludeMessageContent);

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (ChatHttpClient.TryGetChatApiTokenHeader(
                    ServerConfig.GetOrCreate().ChatApiToken,
                    out string headerName,
                    out string headerValue))
            {
                request.SetRequestHeader(headerName, headerValue);
            }

            ChatHttpClient.AttachCertificateBypass(request);
            request.timeout = UploadTimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                PlayLogRecorder.RequeueTelemetry(batch);
                GameLog.LogWarning("[PlayLogTelemetry] upload failed (" + request.responseCode + "): " + request.error);
            }
        }
    }
}
