using UnityEngine;
using UnityEngine.SceneManagement;
using Fungus;

public class ClickedBubble : MonoBehaviour
{
    [SerializeField] public GameObject penel; 
    private Transform originalParent;
    
    // 씬이 바뀌면 이전 Flowchart는 파괴되므로, 
    // 매번 새로 찾을 수 있도록 로직을 변경합니다.
    private Flowchart flowchart;

    void Awake()
    {
        // 1. 시작할 때 패널의 원래 부모(지도 프리펩 내부)를 저장합니다.
        if (penel != null)
        {
            originalParent = penel.transform.parent;
        }
    }

    // 다음 씬에서 지도를 다시 쓸 수 있게 만드는 핵심 로직
    private void SafeLoadScene(string sceneName)
    {
        // 2. 씬 이동 전 패널 회수 (DontDestroyOnLoad로 같이 넘어가기 위함)
        if (penel != null && originalParent != null)
        {
            penel.SetActive(false);
            penel.transform.SetParent(originalParent, false);
        }

        // 3. Variablemanager 글로벌 Flowchart에서 isCalled 해제 (임의 씬 Flowchart가 아님)
        flowchart = FlowchartLocator.Find();
        if (flowchart != null)
            flowchart.SetBooleanVariable(FungusVariableKeys.IsCalled, false);
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart);

        // 4. 씬 로드
        SceneManager.LoadScene(sceneName);
        
        // 중요: 씬 로드 후에는 flowchart를 null로 만들어 
        // 다음번 버튼 클릭 시 새로운 씬의 Flowchart를 찾게 유도합니다.
        flowchart = null; 
    }

    // 각 버튼 연결 함수들 (변화 없음)
    public void ClickedKitchen() => SafeLoadScene("Kitchen");
    public void ClickedMade()    => SafeLoadScene("MaidRoom");
    public void ClickedLib()     => SafeLoadScene("StudyRoom");
    public void ClickedTutor()   => SafeLoadScene("TutorRoom");
    public void ClickedChild()   => SafeLoadScene("ChildRoom");
    public void ClickedWife()    => SafeLoadScene("WifeRoom");
    public void ClickedBed()     => SafeLoadScene("BedRoom");
}
