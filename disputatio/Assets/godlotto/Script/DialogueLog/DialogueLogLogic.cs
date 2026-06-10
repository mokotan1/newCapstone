using System.Collections.Generic;

using UnityEngine;



/// <summary>

/// 대사 로그의 Fungus 비의존 순수 로직(중복 skip, 포맷, 적재).

/// </summary>

public static class DialogueLogLogic

{

    /// <summary>NanumGothic SDF에서 안전한 화자 접두사.</summary>

    public const string ParchmentSpeakerPrefix = "> ";



    /// <summary>① 양피지 패널 제목. TMP 미지원 특수문자(❧ 등) 사용 금지.</summary>

    public const string ParchmentTitleText = ":: 대 사 기 록 ::";



    /// <summary>⑤ 다크 패널 제목.</summary>

    public const string DarkConfessionTitleText = "L O G";



    /// <summary>기존 다크 노트북 제목.</summary>

    public const string LegacyNotebookTitleText = "대사 기록";



    /// <summary>TMP 폰트에서 □로 깨질 수 있는 코드포인트(장식용 Unicode).</summary>

    static readonly int[] RiskyOrnamentCodePoints = { 0x2767, 0x2766, 0x2726, 0x2731 };



    public static bool IsEligibleLine(string text) => !string.IsNullOrWhiteSpace(text);



    public static bool IsNarration(DialogueLogEntry entry) => string.IsNullOrEmpty(entry.Speaker);



    public static bool IsNarration(string speaker) => string.IsNullOrEmpty(speaker);



    /// <summary>
    /// 로그 항목의 핵심 식별값(화자·본문)이 동일한지 판별한다. timestamp 등 메타는 없으므로 제외.
    /// </summary>
    public static bool HasSameContent(DialogueLogEntry entry, string speaker, string text)

    {

        var candidate = new DialogueLogEntry(speaker, text);

        return entry.Speaker == candidate.Speaker && entry.Text == candidate.Text;

    }



    /// <summary>연속 중복(직전 항목) 여부. <see cref="ContainsDuplicate"/>의 단일 항목 편의 API.</summary>
    public static bool ShouldSkipDuplicate(DialogueLogEntry last, string speaker, string text) =>

        HasSameContent(last, speaker, text);



    /// <summary>기존 로그 목록 전체에서 동일 화자·본문이 이미 있는지 검사한다.</summary>
    public static bool ContainsDuplicate(IReadOnlyList<DialogueLogEntry> entries, string speaker, string text)

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



    public static bool TryAppend(List<DialogueLogEntry> entries, string speaker, string text)

    {

        if (!IsEligibleLine(text))

            return false;



        if (ContainsDuplicate(entries, speaker, text))

            return false;



        entries.Add(new DialogueLogEntry(speaker, text));

        return true;

    }



    /// <summary>LegacyNotebook 등 단일 TMP 리치텍스트용. 기존 테스트·프리팹 호환.</summary>

    public static string FormatEntry(DialogueLogEntry entry) =>

        IsNarration(entry)

            ? entry.Text

            : $"<b>{entry.Speaker}</b>\n{entry.Text}";



    public static string FormatPanelTitle(DialogueLogVisualStyle style) =>

        style switch

        {

            DialogueLogVisualStyle.ParchmentCodex => ParchmentTitleText,

            DialogueLogVisualStyle.DarkConfession => DarkConfessionTitleText,

            _ => LegacyNotebookTitleText,

        };



    /// <summary>스타일별 화자 한 줄(장식 기호 포함). EntryView·에디터 프리뷰 공용.</summary>

    public static string FormatSpeakerLine(string speaker, DialogueLogVisualStyle style)

    {

        if (string.IsNullOrEmpty(speaker))

            return string.Empty;



        switch (style)

        {

            case DialogueLogVisualStyle.ParchmentCodex:

                return $"{ParchmentSpeakerPrefix}{speaker}";

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

        string prefixHex = ColorUtility.ToHtmlStringRGB(palette.SpeakerOrnamentColor);

        return $"<color=#{prefixHex}>{ParchmentSpeakerPrefix.TrimEnd()}</color> {speaker}";

    }



    /// <summary>표시 문자열에 TMP에서 깨질 위험이 있는 장식 코드포인트가 없는지 검사.</summary>

    public static bool ContainsRiskyOrnamentCharacters(string text)

    {

        if (string.IsNullOrEmpty(text))

            return false;



        for (int i = 0; i < text.Length; i++)

        {

            int codePoint = char.ConvertToUtf32(text, i);

            for (int j = 0; j < RiskyOrnamentCodePoints.Length; j++)

            {

                if (codePoint == RiskyOrnamentCodePoints[j])

                    return true;

            }



            if (codePoint > 0xFFFF)

                i++;

        }



        return false;

    }

}

