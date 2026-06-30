using UnityEngine;
using UnityEngine.SceneManagement;
using Fungus;

public class BackNavigator : MonoBehaviour
{
    [Header("전역 Flowchart 설정")]
    [SerializeField] string globalFlowchartName = "Variablemanager";
    [SerializeField] string prevVarKey = "PrevScene";
    [SerializeField] string fallbackSceneName = ""; // 비상용 (없으면 생략 가능)

    [Header("고정 복귀 씬 설정")]
    [SerializeField] bool useFixedReturnRoutes = true;

    internal static bool TryResolveFixedReturnScene(string currentSceneName, out string returnSceneName)
    {
        switch (currentSceneName)
        {
            case "MaidRoom":
            case "StudyRoom":
            case "MaidEntrance":
            case "StudyEntrance":
                returnSceneName = "Hallway_Right";
                return true;

            case "PrisonEntrance":
                returnSceneName = "StudyRoom";
                return true;

            case "BedRoom":
            case "WifeRoom":
            case "BedEntrance":
            case "WifeEntrance":
                returnSceneName = "2floorHallway_Right";
                return true;

            case "TutorRoom":
            case "ChildRoom":
            case "TutorEntrance":
            case "ChildEntrance":
                returnSceneName = "2floorHallway_Left";
                return true;

            case "2floorMainHall":
                returnSceneName = "Hall_playerble";
                return true;

            default:
                returnSceneName = string.Empty;
                return false;
        }
    }

    public void GoBack()
    {
        // 모달 UI(설정·로그·체셔/튜터 패널·책·지도 등)가 열려 HUD 입력을 막는 동안에는
        // 뒤로가기 같은 HUD 내비게이션을 무시합니다(패널 뒤 버튼 클릭 방지, fail-safe).
        if (ModalInputGate.IsBlockingHudInput)
        {
            GameLog.Log("[BackNavigator] 모달이 열려 있어 뒤로가기 입력을 무시합니다.");
            return;
        }

        Flowchart global = FlowchartLocator.FindByGameObjectName(globalFlowchartName);
        string fixedReturnScene = ResolveFixedReturnScene();
        if (!string.IsNullOrEmpty(fixedReturnScene))
        {
            GameLog.Log($"[BackNavigator] 고정 복귀 씬으로 이동 중 -> {fixedReturnScene}");
            ClickInteractionCleanup.ResetAfterUiBoundary(global);
            SceneManager.LoadScene(fixedReturnScene);
            return;
        }

        if (global == null)
        {
            GameLog.LogWarning($"전역 Flowchart '{globalFlowchartName}'를 찾지 못했습니다.");
            TryFallback();
            return;
        }

        string prev = global.GetStringVariable(prevVarKey);
        if (string.IsNullOrEmpty(prev))
        {
            GameLog.LogWarning($"전역 변수 '{prevVarKey}'가 비어 있습니다.");
            TryFallback();
            return;
        }

        GameLog.Log($"[BackNavigator] 이전 씬으로 이동 중 → {prev}");
        ClickInteractionCleanup.ResetAfterUiBoundary(global);
        SceneManager.LoadScene(prev);

        Debug.Log("클릭됨");
    }

    private string ResolveFixedReturnScene()
    {
        if (!useFixedReturnRoutes)
            return string.Empty;

        string currentSceneName = SceneManager.GetActiveScene().name;
        return TryResolveFixedReturnScene(currentSceneName, out string returnSceneName)
            ? returnSceneName
            : string.Empty;
    }

    private void TryFallback()
    {
        if (!string.IsNullOrEmpty(fallbackSceneName))
        {
            GameLog.Log($"[BackNavigator] PrevScene이 비어 있어서 '{fallbackSceneName}'로 이동합니다.");
            ClickInteractionCleanup.ResetAfterUiBoundary();
            SceneManager.LoadScene(fallbackSceneName);
        }
        else
        {
            GameLog.LogWarning("[BackNavigator] 이전 씬 정보를 찾지 못했습니다.");
        }
    }
}
