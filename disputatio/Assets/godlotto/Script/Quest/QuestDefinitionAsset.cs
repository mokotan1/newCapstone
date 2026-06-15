using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단일 퀘스트 ScriptableObject. 디자이너가 제목·단계·힌트를 편집한다.
/// </summary>
[CreateAssetMenu(fileName = "QuestDefinition", menuName = "Disputatio/Quest Definition")]
public class QuestDefinitionAsset : ScriptableObject
{
    [SerializeField] private string questId = string.Empty;
    [SerializeField] private string title = string.Empty;
    [TextArea]
    [SerializeField] private string hint = string.Empty;
    [SerializeField] private List<QuestStepData> steps = new List<QuestStepData>();

    public string QuestId => questId ?? string.Empty;
    public string Title => title ?? string.Empty;
    public string Hint => hint ?? string.Empty;
    public IReadOnlyList<QuestStepData> Steps => steps ?? (IReadOnlyList<QuestStepData>)Array.Empty<QuestStepData>();

#if UNITY_EDITOR
    public void SetContentForEditor(string id, string questTitle, string questHint, IReadOnlyList<QuestStepData> questSteps)
    {
        questId = id ?? string.Empty;
        title = questTitle ?? string.Empty;
        hint = questHint ?? string.Empty;
        steps = new List<QuestStepData>();
        if (questSteps != null)
        {
            for (int i = 0; i < questSteps.Count; i++)
            {
                if (questSteps[i] != null)
                    steps.Add(questSteps[i]);
            }
        }
    }
#endif

    public QuestDefinition ToDefinition()
    {
        var runtimeSteps = new List<QuestStep>(Steps.Count);
        for (int i = 0; i < Steps.Count; i++)
        {
            QuestStepData step = Steps[i];
            if (step != null)
                runtimeSteps.Add(step.ToQuestStep());
        }

        return new QuestDefinition(QuestId, Title, Hint, runtimeSteps);
    }
}
