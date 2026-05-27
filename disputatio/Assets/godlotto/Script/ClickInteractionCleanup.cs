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

    public static void ResetAfterUiBoundary(Flowchart preferredFlowchart = null, bool resetWindowClicked = true)
    {
        InteractionLock.ForceUnlock();
        ClearCurrentUiSelection();

        HashSet<Flowchart> resetFlowcharts = new HashSet<Flowchart>();
        ResetFlowchartClickFlags(preferredFlowchart, resetFlowcharts, resetWindowClicked);

        Flowchart globalFlowchart = FlowchartLocator.Find();
        ResetFlowchartClickFlags(globalFlowchart, resetFlowcharts, resetWindowClicked);
        ResetLoadedSceneFlowcharts(resetFlowcharts, resetWindowClicked);
    }

    private static void ClearCurrentUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private static void ResetLoadedSceneFlowcharts(HashSet<Flowchart> resetFlowcharts, bool resetWindowClicked)
    {
        var flowcharts = Resources.FindObjectsOfTypeAll<Flowchart>();
        foreach (var flowchart in flowcharts)
        {
            if (flowchart == null || !flowchart.gameObject.scene.IsValid() || !flowchart.gameObject.scene.isLoaded)
                continue;

            ResetFlowchartClickFlags(flowchart, resetFlowcharts, resetWindowClicked);
        }
    }

    private static void ResetFlowchartClickFlags(Flowchart flowchart, HashSet<Flowchart> resetFlowcharts, bool resetWindowClicked)
    {
        if (flowchart == null)
            return;

        if (resetFlowcharts != null && !resetFlowcharts.Add(flowchart))
            return;

        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsCalled, false);
        SetBooleanIfPresent(flowchart, LegacyUppercaseIsCalled, false);
        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsClicked, false);
        if (resetWindowClicked)
            SetBooleanIfPresent(flowchart, FungusVariableKeys.WindowClicked, false);
    }

    private static void SetBooleanIfPresent(Flowchart flowchart, string variableName, bool value)
    {
        if (flowchart.GetVariable(variableName) is BooleanVariable)
            flowchart.SetBooleanVariable(variableName, value);
    }
}
