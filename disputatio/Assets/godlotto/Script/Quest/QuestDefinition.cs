using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 메타데이터와 단계 목록.
/// </summary>
public sealed class QuestDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Hint { get; }
    public IReadOnlyList<QuestStep> Steps { get; }

    public QuestDefinition(string id, string title, string hint, IReadOnlyList<QuestStep> steps)
    {
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Hint = hint ?? string.Empty;
        Steps = steps ?? Array.Empty<QuestStep>();
    }

    public bool HasValidId => !string.IsNullOrWhiteSpace(Id);

    public bool HasPlayableSteps => Steps != null && Steps.Count > 0;
}
