using Fungus;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 패널 종료, 지도 이동, 씬 뒤로가기처럼 클릭 흐름이 끊기는 경계에서 남은 입력 잠금을 정리합니다.
/// </summary>
public static class ClickInteractionCleanup
{
    private const string LegacyUppercaseIsCalled = "IsCalled";

    public static void ResetAfterUiBoundary(Flowchart preferredFlowchart = null)
    {
        InteractionLock.ForceUnlock();
        ClearCurrentUiSelection();

        HashSet<Flowchart> resetFlowcharts = new HashSet<Flowchart>();
        ResetFlowchartClickFlags(preferredFlowchart, resetFlowcharts);

        Flowchart globalFlowchart = FlowchartLocator.Find();
        ResetFlowchartClickFlags(globalFlowchart, resetFlowcharts);
        ResetLoadedSceneFlowcharts(resetFlowcharts);
    }

    private static void ClearCurrentUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private static void ResetLoadedSceneFlowcharts(HashSet<Flowchart> resetFlowcharts)
    {
        var flowcharts = Resources.FindObjectsOfTypeAll<Flowchart>();
        foreach (var flowchart in flowcharts)
        {
            if (flowchart == null || !flowchart.gameObject.scene.IsValid() || !flowchart.gameObject.scene.isLoaded)
                continue;

            ResetFlowchartClickFlags(flowchart, resetFlowcharts);
        }
    }

    private static void ResetFlowchartClickFlags(Flowchart flowchart, HashSet<Flowchart> resetFlowcharts)
    {
        if (flowchart == null)
            return;

        if (resetFlowcharts != null && !resetFlowcharts.Add(flowchart))
            return;

        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsCalled, false);
        SetBooleanIfPresent(flowchart, LegacyUppercaseIsCalled, false);
        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsClicked, false);
        SetBooleanIfPresent(flowchart, FungusVariableKeys.WindowClicked, false);
    }

    private static void SetBooleanIfPresent(Flowchart flowchart, string variableName, bool value)
    {
        if (flowchart.GetVariable(variableName) is BooleanVariable)
            flowchart.SetBooleanVariable(variableName, value);
    }
}
