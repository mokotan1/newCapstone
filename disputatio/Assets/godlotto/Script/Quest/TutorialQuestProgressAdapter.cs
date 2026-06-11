using System;
using System.Collections.Generic;
using Godlotto.Interaction;

/// <summary>
/// 게임 월드 신호·스냅샷을 튜토리얼 퀘스트 단계 id로 매핑하는 순수 로직.
/// </summary>
public static class TutorialQuestProgressAdapter
{
    public static string ResolveInitialQuestId(TutorialQuestWorldFlags flags)
    {
        if (IsTutorialFullyComplete(flags))
            return null;

        if (flags.BottleDragged || flags.FaucetClicked || flags.GetBottle)
            return TutorialQuestIds.BottleKey;

        return TutorialQuestIds.LightTheManor;
    }

    public static string GetNextQuestId(string clearedQuestId)
    {
        if (string.Equals(clearedQuestId, TutorialQuestIds.LightTheManor, StringComparison.Ordinal))
            return TutorialQuestIds.BottleKey;

        return null;
    }

    public static bool IsTutorialFullyComplete(TutorialQuestWorldFlags flags)
    {
        return flags.BottleDragged;
    }

    public static bool TryMapSceneEntryToStepId(
        string questId,
        string sceneName,
        TutorialQuestWorldFlags flags,
        out string stepId)
    {
        stepId = null;
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (string.Equals(questId, TutorialQuestIds.LightTheManor, StringComparison.Ordinal))
        {
            if (TutorialQuestWorldScenes.IsKitchenScene(sceneName))
            {
                stepId = TutorialQuestIds.LightTheManorSteps.GoKitchen;
                return true;
            }

            if (TutorialQuestWorldScenes.IsInspectableLitHallScene(sceneName, flags.ElectricOn))
            {
                stepId = TutorialQuestIds.LightTheManorSteps.InspectHall;
                return true;
            }

            return false;
        }

        return false;
    }

    public static bool TryMapFungusFlagEdgeToStepId(
        string questId,
        string fungusKey,
        bool isEnabled,
        out string stepId)
    {
        stepId = null;
        if (!isEnabled || string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(fungusKey))
            return false;

        if (string.Equals(questId, TutorialQuestIds.LightTheManor, StringComparison.Ordinal)
            && string.Equals(fungusKey, FungusVariableKeys.ElectricOn, StringComparison.Ordinal))
        {
            stepId = TutorialQuestIds.LightTheManorSteps.RaiseBreaker;
            return true;
        }

        if (string.Equals(questId, TutorialQuestIds.BottleKey, StringComparison.Ordinal)
            && string.Equals(fungusKey, FungusVariableKeys.GetBottle, StringComparison.Ordinal))
        {
            stepId = TutorialQuestIds.BottleKeySteps.FindBottle;
            return true;
        }

        return false;
    }

    public static bool TryMapKitchenBlockToStepId(string blockName, out string stepId)
    {
        stepId = null;
        if (string.IsNullOrWhiteSpace(blockName))
            return false;

        if (string.Equals(blockName, KitchenSinkInteractionGate.FaucetBlockName, StringComparison.Ordinal))
        {
            stepId = TutorialQuestIds.BottleKeySteps.FillBottle;
            return true;
        }

        if (string.Equals(blockName, KitchenSinkInteractionGate.BottleDraggedBlockName, StringComparison.Ordinal))
        {
            stepId = TutorialQuestIds.BottleKeySteps.TakeKey;
            return true;
        }

        return false;
    }

    public static bool IsTrackedKitchenBlock(string blockName)
    {
        return TryMapKitchenBlockToStepId(blockName, out _);
    }

    public static bool IsStepSatisfiedByWorld(
        string questId,
        string stepId,
        TutorialQuestWorldFlags flags)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(stepId))
            return false;

        if (string.Equals(questId, TutorialQuestIds.LightTheManor, StringComparison.Ordinal))
        {
            if (string.Equals(stepId, TutorialQuestIds.LightTheManorSteps.GoKitchen, StringComparison.Ordinal))
                return TutorialQuestWorldScenes.IsKitchenScene(flags.ActiveSceneName) || flags.ElectricOn || flags.GetBottle;

            if (string.Equals(stepId, TutorialQuestIds.LightTheManorSteps.RaiseBreaker, StringComparison.Ordinal))
                return flags.ElectricOn;

            if (string.Equals(stepId, TutorialQuestIds.LightTheManorSteps.InspectHall, StringComparison.Ordinal))
                return TutorialQuestWorldScenes.IsInspectableLitHallScene(flags.ActiveSceneName, flags.ElectricOn)
                    || flags.GetBottle
                    || flags.FaucetClicked
                    || flags.BottleDragged;
        }

        if (string.Equals(questId, TutorialQuestIds.BottleKey, StringComparison.Ordinal))
        {
            if (string.Equals(stepId, TutorialQuestIds.BottleKeySteps.FindBottle, StringComparison.Ordinal))
                return flags.GetBottle;

            if (string.Equals(stepId, TutorialQuestIds.BottleKeySteps.FillBottle, StringComparison.Ordinal))
                return flags.FaucetClicked;

            if (string.Equals(stepId, TutorialQuestIds.BottleKeySteps.TakeKey, StringComparison.Ordinal))
                return flags.BottleDragged;
        }

        return false;
    }

    public static IReadOnlyList<string> ResolveCatchUpStepIds(
        QuestTrackerState state,
        TutorialQuestWorldFlags flags)
    {
        var completed = new List<string>();
        if (state == null)
            return completed;

        while (state.CurrentQuest != null && !state.IsQuestCleared)
        {
            int index = state.CurrentStepIndex;
            if (index < 0 || index >= state.CurrentQuest.Steps.Count)
                break;

            string activeStepId = state.CurrentQuest.Steps[index].Id;
            if (!IsStepSatisfiedByWorld(state.CurrentQuestId, activeStepId, flags))
                break;

            if (!state.CompleteStep(activeStepId))
                break;

            completed.Add(activeStepId);
        }

        return completed;
    }
}
