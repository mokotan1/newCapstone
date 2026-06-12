using System;
using System.Collections.Generic;

/// <summary>
/// 현재 퀘스트 진행 상태. UI 없이 단계 완료·교체를 관리한다.
/// </summary>
public sealed class QuestTrackerState
{
    readonly Dictionary<string, QuestDefinition> questsById = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);
    readonly HashSet<string> completedStepIds = new HashSet<string>(StringComparer.Ordinal);

    QuestDefinition currentQuest;
    int activeStepIndex = -1;
    bool isCleared;

    public QuestTrackerState(IEnumerable<QuestDefinition> quests)
    {
        if (quests == null)
            return;

        foreach (QuestDefinition quest in quests)
        {
            if (quest == null || !quest.HasValidId)
                continue;

            questsById[quest.Id] = quest;
        }
    }

    public string CurrentQuestId => currentQuest?.Id;

    public QuestDefinition CurrentQuest => currentQuest;

    public int CurrentStepIndex => activeStepIndex;

    public bool IsQuestCleared => isCleared;

    public IReadOnlyCollection<string> CompletedStepIds => completedStepIds;

    public bool TrySetCurrentQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        if (!questsById.TryGetValue(questId, out QuestDefinition quest))
            return false;

        if (!quest.HasPlayableSteps)
            return false;

        currentQuest = quest;
        activeStepIndex = 0;
        isCleared = false;
        completedStepIds.Clear();
        return true;
    }

    public bool AdvanceStep()
    {
        if (currentQuest == null || isCleared)
            return false;

        return CompleteActiveStep();
    }

    public bool CompleteStep(string stepId)
    {
        if (currentQuest == null || isCleared || string.IsNullOrWhiteSpace(stepId))
            return false;

        if (activeStepIndex < 0 || activeStepIndex >= currentQuest.Steps.Count)
            return false;

        QuestStep activeStep = currentQuest.Steps[activeStepIndex];
        if (!string.Equals(activeStep.Id, stepId, StringComparison.Ordinal))
            return false;

        return CompleteActiveStep();
    }

    public bool IsStepCompleted(string stepId)
    {
        return !string.IsNullOrWhiteSpace(stepId) && completedStepIds.Contains(stepId);
    }

    public QuestStepPhase GetStepPhase(int stepIndex)
    {
        if (currentQuest == null)
            return QuestStepPhase.Pending;

        return QuestTrackerLogic.ResolveStepPhase(
            stepIndex,
            activeStepIndex,
            isCleared,
            currentQuest.Steps.Count);
    }

    public int CountActiveSteps()
    {
        if (currentQuest == null)
            return 0;

        return QuestTrackerLogic.CountActiveSteps(
            activeStepIndex,
            isCleared,
            currentQuest.Steps.Count,
            hasQuest: true);
    }

    bool CompleteActiveStep()
    {
        QuestStep activeStep = currentQuest.Steps[activeStepIndex];
        completedStepIds.Add(activeStep.Id);

        int nextIndex = activeStepIndex + 1;
        if (nextIndex >= currentQuest.Steps.Count)
        {
            isCleared = true;
            activeStepIndex = -1;
            return true;
        }

        activeStepIndex = nextIndex;
        return true;
    }
}
