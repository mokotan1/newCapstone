using System.Collections.Generic;

/// <summary>
/// 대사 로그의 Fungus 비의존 순수 로직(중복 skip, 포맷, 적재).
/// </summary>
public static class DialogueLogLogic
{
    public static bool IsEligibleLine(string text) => !string.IsNullOrWhiteSpace(text);

    public static bool ShouldSkipDuplicate(DialogueLogEntry last, string speaker, string text) =>
        last.Speaker == speaker && last.Text == text;

    public static bool TryAppend(List<DialogueLogEntry> entries, string speaker, string text)
    {
        if (!IsEligibleLine(text))
            return false;

        if (entries.Count > 0 && ShouldSkipDuplicate(entries[entries.Count - 1], speaker, text))
            return false;

        entries.Add(new DialogueLogEntry(speaker, text));
        return true;
    }

    public static string FormatEntry(DialogueLogEntry entry) =>
        string.IsNullOrEmpty(entry.Speaker)
            ? entry.Text
            : $"<b>{entry.Speaker}</b>\n{entry.Text}";
}
