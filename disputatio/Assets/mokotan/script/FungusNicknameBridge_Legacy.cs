using UnityEngine;
using UnityEngine.UI; // Legacy UI를 위해 필요합니다.
using Fungus;
// using TMPro; // 이 줄은 필요 없으므로 삭제하거나 주석 처리합니다.

public class FungusNicknameBridge_Legacy : MonoBehaviour
{
    [Tooltip("연결할 Fungus Flowchart")]
    public Flowchart targetFlowchart;

    [Tooltip("닉네임을 입력받을 Legacy Input Field")]
    public InputField nicknameInput; // TMP_InputField에서 InputField로 변경되었습니다.

    // 버튼을 클릭했을 때 이 함수를 실행할 겁니다.
    public void SetNicknameAndExecuteBlock()
    {
        // InputField의 텍스트를 가져옵니다.
        string UserName = nicknameInput.text;

        // Fungus의 "PlayerName" 변수에 텍스트를 저장합니다.
        targetFlowchart.SetStringVariable("UserName", UserName);

        // "Greeting" Block을 실행합니다.
        targetFlowchart.ExecuteBlock("input_Nicname");
    }
}