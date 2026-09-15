/// <summary>
/// Concatenate Cheshire SSE <c>text_delta</c> chunks for live SayDialog text.
/// Final <c>done.full_text</c> may still replace this if the server guard sanitizes.
/// </summary>
public static class CheshireLiveStreamDisplay
{
    public static string Append(string current, string delta)
    {
        if (string.IsNullOrEmpty(delta))
            return current ?? string.Empty;
        return (current ?? string.Empty) + delta;
    }
}
