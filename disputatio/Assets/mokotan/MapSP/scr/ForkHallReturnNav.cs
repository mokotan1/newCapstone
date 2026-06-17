using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 1층 우측 갈래(서재·가정부 문 앞 등): PrevScene이 아니라
/// 중앙 홀 쪽으로 이어지는 갈림 씬으로만 이동합니다.
/// </summary>
public class ForkHallReturnNav : MonoBehaviour
{
    [SerializeField] string forkHallSceneName = "Hall_RightCross";

    public void GoToForkTowardCentralHall()
    {
        // 모달이 열려 HUD 입력을 막는 동안에는 갈림길 복귀 내비게이션을 무시합니다(fail-safe).
        if (ModalInputGate.IsBlockingHudInput)
            return;

        if (string.IsNullOrEmpty(forkHallSceneName))
            return;
        SceneManager.LoadScene(forkHallSceneName);
    }
}
