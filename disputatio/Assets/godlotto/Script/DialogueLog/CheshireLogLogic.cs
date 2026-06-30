using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 체셔 로그 적재·중복 제거·UI 변환. Fungus·TMP 비의존 순수 로직.
/// </summary>
public static class CheshireLogLogic
{
    /// <summary>
    /// 동일 화자·본문이 이미 목록에 있는지 검사한다. timestamp·sceneName은 비교하지 않는다.
    /// </summary>
    public static bool HasSameContent(CheshireLogEntry entry, string speaker, string text) =>
        entry.Speaker == (speaker ?? string.Empty) && entry.Text == (text ?? string.Empty);

    /// <summary>기존 체셔 로그 목록 전체에서 동일 화자·본문이 이미 있는지 검사한다.</summary>
    public static bool ContainsDuplicate(IReadOnlyList<CheshireLogEntry> entries, string speaker, string text)
    {
        if (entries == null || entries.Count == 0)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (HasSameContent(entries[i], speaker, text))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 체셔 로그 한 줄을 추가한다. timestamp·sceneName·turnIndex는 이 메서드에서 채운다.
    /// </summary>
    public static bool TryAppend(List<CheshireLogEntry> entries, string speaker, string text)
    {
        if (!DialogueLogLogic.IsEligibleLine(text))
            return false;

        if (ContainsDuplicate(entries, speaker, text))
            return false;

        entries.Add(CreateEntry(speaker, text, entries.Count));
        return true;
    }

    /// <summary>테스트·고정 시각 주입용.</summary>
    internal static bool TryAppend(
        List<CheshireLogEntry> entries,
        string speaker,
        string text,
        DateTimeOffset timestamp,
        string sceneName)
    {
        if (!DialogueLogLogic.IsEligibleLine(text))
            return false;

        if (ContainsDuplicate(entries, speaker, text))
            return false;

        entries.Add(new CheshireLogEntry(timestamp, sceneName, speaker, text, entries.Count));
        return true;
    }

    static CheshireLogEntry CreateEntry(string speaker, string text, int turnIndex)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return new CheshireLogEntry(DateTimeOffset.UtcNow, sceneName, speaker, text, turnIndex);
    }

    /// <summary>체셔 탭 UI(<see cref="DialogueLogEntryView"/>) 표시용 변환.</summary>
    public static DialogueLogEntry ToDialogueLogEntry(CheshireLogEntry entry) =>
        new DialogueLogEntry(entry.Speaker, entry.Text);
}
