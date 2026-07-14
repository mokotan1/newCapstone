using UnityEngine.Networking;

/// <summary>
/// Outcome of one chat HTTP attempt (real <see cref="UnityWebRequest"/> or EditMode seam).
/// </summary>
public readonly struct ChatHttpAttemptOutcome
{
    public ChatHttpAttemptOutcome(
        UnityWebRequest.Result result,
        long responseCode,
        string error,
        string body)
    {
        Result = result;
        ResponseCode = responseCode;
        Error = error ?? "";
        Body = body ?? "";
    }

    public UnityWebRequest.Result Result { get; }
    public long ResponseCode { get; }
    public string Error { get; }
    public string Body { get; }
}
