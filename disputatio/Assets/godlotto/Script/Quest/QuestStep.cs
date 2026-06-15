/// <summary>
/// 퀘스트 트래커의 단일 단계 정의.
/// </summary>
public readonly struct QuestStep
{
    public readonly string Id;
    public readonly string Text;

    public QuestStep(string id, string text)
    {
        Id = id ?? string.Empty;
        Text = text ?? string.Empty;
    }
}
