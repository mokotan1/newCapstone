/// <summary>
/// 퀘스트 트래커의 Fungus·UI 비의존 순수 로직.
/// </summary>
public static class QuestTrackerLogic
{
    public static QuestStepPhase ResolveStepPhase(int stepIndex, int activeStepIndex, bool isCleared, int stepCount)
    {
        if (stepCount <= 0 || stepIndex < 0 || stepIndex >= stepCount)
            return QuestStepPhase.Pending;

        if (isCleared)
            return QuestStepPhase.Completed;

        if (activeStepIndex < 0)
            return QuestStepPhase.Pending;

        if (stepIndex < activeStepIndex)
            return QuestStepPhase.Completed;

        if (stepIndex == activeStepIndex)
            return QuestStepPhase.Active;

        return QuestStepPhase.Pending;
    }

    public static int CountActiveSteps(int activeStepIndex, bool isCleared, int stepCount, bool hasQuest)
    {
        if (!hasQuest || isCleared || stepCount <= 0 || activeStepIndex < 0)
            return 0;

        return 1;
    }
}
