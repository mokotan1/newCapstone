using System;
using UnityEngine;

/// <summary>
/// Inspector·ScriptableObject 직렬화용 단계 데이터.
/// 런타임 <see cref="QuestStep"/>으로 변환한다.
/// </summary>
[Serializable]
public class QuestStepData
{
    [SerializeField] private string stepId = string.Empty;
    [SerializeField] private string text = string.Empty;

    public string StepId => stepId ?? string.Empty;
    public string Text => text ?? string.Empty;

    public QuestStepData()
    {
    }

    public QuestStepData(string id, string stepText)
    {
        stepId = id ?? string.Empty;
        text = stepText ?? string.Empty;
    }

    public QuestStep ToQuestStep() => new QuestStep(StepId, Text);
}
