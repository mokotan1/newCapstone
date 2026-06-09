/// <summary>
/// 대사 로그 UI 시각 스타일. <c>docs/dialogue-log-mockups.html</c> 견본 번호와 대응한다.
/// </summary>
public enum DialogueLogVisualStyle
{
    /// <summary>기존 단일 TMP 리치텍스트(다크 노트북).</summary>
    LegacyNotebook = 0,

    /// <summary>① 양피지 고문서 — Parchment Codex.</summary>
    ParchmentCodex = 1,

    /// <summary>⑤ 어둠 속 고백록 — Minimal Dark / Horror.</summary>
    DarkConfession = 5,
}

/// <summary>
/// Unity <c>SerializedProperty.enumValueIndex</c>는 enum 정수값이 아니라 선언 순서 인덱스를 쓴다.
/// </summary>
public static class DialogueLogVisualStyleIndex
{
    public static int ToEnumValueIndex(DialogueLogVisualStyle style) =>
        style switch
        {
            DialogueLogVisualStyle.ParchmentCodex => 1,
            DialogueLogVisualStyle.DarkConfession => 2,
            _ => 0,
        };
}
