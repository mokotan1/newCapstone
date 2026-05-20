using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// UI 패널 종료, 지도 이동, 씬 뒤로가기처럼 클릭 흐름이 끊기는 경계에서 남은 입력 잠금을 정리합니다.
/// </summary>
public static class ClickInteractionCleanup
{
    public static void ResetAfterUiBoundary(Flowchart preferredFlowchart = null)
    {
        InteractionLock.ForceUnlock();
        ClearCurrentUiSelection();

        ResetFlowchartClickFlags(preferredFlowchart);
        Flowchart activeSceneFlowchart = FindActiveSceneFlowchart();
        if (activeSceneFlowchart != preferredFlowchart)
            ResetFlowchartClickFlags(activeSceneFlowchart);

        Flowchart globalFlowchart = FlowchartLocator.Find();
        if (globalFlowchart != preferredFlowchart && globalFlowchart != activeSceneFlowchart)
            ResetFlowchartClickFlags(globalFlowchart);
    }

    private static void ClearCurrentUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private static void ResetFlowchartClickFlags(Flowchart flowchart)
    {
        if (flowchart == null)
            return;

        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsCalled, false);
        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsClicked, false);
        SetBooleanIfPresent(flowchart, FungusVariableKeys.WindowClicked, false);
    }

    private static Flowchart FindActiveSceneFlowchart()
    {
        var activeScene = SceneManager.GetActiveScene();
        var flowcharts = Resources.FindObjectsOfTypeAll<Flowchart>();
        foreach (var flowchart in flowcharts)
        {
            if (flowchart != null && flowchart.gameObject.scene == activeScene)
                return flowchart;
        }

        return null;
    }

    private static void SetBooleanIfPresent(Flowchart flowchart, string variableName, bool value)
    {
        if (flowchart.GetVariable(variableName) is BooleanVariable)
            flowchart.SetBooleanVariable(variableName, value);
    }
}
