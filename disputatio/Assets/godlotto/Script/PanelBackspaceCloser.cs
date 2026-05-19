using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Backspace 버튼 클릭 시 지정한 패널을 닫고, 패널 상호작용 중 남은 클릭 잠금 상태를 정리합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class PanelBackspaceCloser : MonoBehaviour
{
    [Tooltip("닫을 패널입니다. 비워 두면 버튼의 부모 오브젝트를 닫습니다.")]
    [SerializeField] private GameObject targetPanel;

    [Tooltip("기존 Fungus 닫기 블록이 있으면 패널 닫기 후 함께 실행합니다.")]
    [SerializeField] private string executeBlockName;

    [Tooltip("비워 두면 현재 씬 Flowchart를 우선 찾고, 없으면 Variablemanager를 사용합니다.")]
    [SerializeField] private Flowchart targetFlowchart;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ClosePanel);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(ClosePanel);
    }

    public void ClosePanel()
    {
        GameObject panel = targetPanel != null
            ? targetPanel
            : transform.parent != null
                ? transform.parent.gameObject
                : null;

        if (panel != null)
            panel.SetActive(false);

        ClearCurrentUiSelection();
        ResetClickLockVariables();
        ExecuteCloseBlock();
    }

    private static void ClearCurrentUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void ResetClickLockVariables()
    {
        Flowchart flowchart = ResolveFlowchart();
        if (flowchart == null)
            return;

        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsCalled, false);
        SetBooleanIfPresent(flowchart, FungusVariableKeys.IsClicked, false);
    }

    private void ExecuteCloseBlock()
    {
        if (string.IsNullOrWhiteSpace(executeBlockName))
            return;

        Flowchart flowchart = ResolveFlowchart();
        if (flowchart == null)
            return;

        flowchart.ExecuteBlock(executeBlockName);
    }

    private Flowchart ResolveFlowchart()
    {
        if (targetFlowchart != null)
            return targetFlowchart;

        Flowchart sceneFlowchart = FindActiveSceneFlowchart();
        return sceneFlowchart != null ? sceneFlowchart : FlowchartLocator.Find();
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
