using UnityEngine;

public class DontDestroy : MonoBehaviour
{
    void Awake()
    {
        // 이 스크립트가 붙어있는 게임 오브젝트를
        // 씬이 로드될 때 파괴하지 말라고 Unity에 알려줍니다.
        DontDestroyOnLoad(this.gameObject);

        // 스크립트가 실행되었는지 확인하기 위한 메시지
        Debug.Log(this.gameObject.name + " 오브젝트에 DontDestroy 스크립트가 실행되었습니다.");
    }
}