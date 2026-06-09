using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사 로그의 Fungus 비의존 순수 로직(중복 skip, 포맷, 적재).
/// </summary>
public static class DialogueLogLogic
{
    public const char ParchmentSpeakerOrnament = '\u2767';

    public static bool IsEligibleLine(string text) => !string.IsNullOrWhiteSpace(text);

    public static bool IsNarration(DialogueLogEntry entry) => string.IsNullOrEmpty(entry.Speaker);

    public static bool IsNarration(string speaker) => string.IsNullOrEmpty(speaker);

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

    /// <summary>LegacyNotebook 등 단일 TMP 리치텍스트용. 기존 테스트·프리팹 호환.</summary>
    public static string FormatEntry(DialogueLogEntry entry) =>
        IsNarration(entry)
            ? entry.Text
            : $"<b>{entry.Speaker}</b>\n{entry.Text}";

    /// <summary>스타일별 화자 한 줄(장식 기호 포함). EntryView·에디터 프리뷰 공용.</summary>
    public static string FormatSpeakerLine(string speaker, DialogueLogVisualStyle style)
    {
        if (string.IsNullOrEmpty(speaker))
            return string.Empty;

        switch (style)
        {
            case DialogueLogVisualStyle.ParchmentCodex:
                return $"{ParchmentSpeakerOrnament} {speaker}";
            case DialogueLogVisualStyle.DarkConfession:
                return speaker.ToUpperInvariant();
            default:
                return speaker;
        }
    }

    /// <summary>Parchment 등 장식·화자색 분리가 필요한 TMP 리치텍스트.</summary>
    public static string FormatSpeakerRichText(string speaker, DialogueLogVisualStyle style)
    {
        if (string.IsNullOrEmpty(speaker))
            return string.Empty;

        if (style != DialogueLogVisualStyle.ParchmentCodex)
            return FormatSpeakerLine(speaker, style);

        var palette = DialogueLogStylePalette.ParchmentCodex;
        string ornamentHex = ColorUtility.ToHtmlStringRGB(palette.SpeakerOrnamentColor);
        return $"<color=#{ornamentHex}>{ParchmentSpeakerOrnament}</color> {speaker}";
    }
}
