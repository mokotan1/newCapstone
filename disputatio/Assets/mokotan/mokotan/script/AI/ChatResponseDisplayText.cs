using System.Text.RegularExpressions;

public static class ChatResponseDisplayText
{
    private static readonly Regex InlineFunctionTagRegex =
        new Regex(@"<function=[^>]*>.*?</function>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static string StripInlineFunctionTags(string response)
    {
        if (string.IsNullOrEmpty(response))
            return response;

        string cleaned = InlineFunctionTagRegex.Replace(response, "");
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
        cleaned = Regex.Replace(cleaned, @"\s+\n", "\n");
        cleaned = Regex.Replace(cleaned, @"\n\s+", "\n");
        return cleaned.Trim();
    }
}
