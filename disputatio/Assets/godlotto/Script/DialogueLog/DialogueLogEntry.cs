/// <summary>
/// 대사 로그(백로그) 한 줄을 표현하는 불변 데이터.
/// 화자 이름과 대사 본문만 보관한다(세션 메모리 전용).
/// </summary>
public readonly struct DialogueLogEntry
{
    /// <summary>화자 이름. 나레이션 등 이름이 없으면 빈 문자열.</summary>
    public readonly string Speaker;

    /// <summary>대사 본문(완성된 전체 텍스트).</summary>
    public readonly string Text;

    public DialogueLogEntry(string speaker, string text)
    {
        Speaker = speaker ?? string.Empty;
        Text = text ?? string.Empty;
    }
}
