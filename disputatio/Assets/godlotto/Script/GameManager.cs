using UnityEngine;
using Fungus;
using System.Collections;
using UnityEngine.SceneManagement; // 씬 전환 관련해서 필요할 수 있으니 추가

public class GameManager : MonoBehaviour
{
    // !!! 싱글톤 인스턴스 추가 !!!
    public static GameManager instance; 

    public GameObject settingPanel;
    public Flowchart flowchart;
    public static bool isPaused = false;
    
    // 이 '깃발'은 다음 입력이 있을 때까지 입력을 막습니다.
    private bool blockNextInput = false;

    // !!! Awake() 함수 추가 및 싱글톤 구현 !!!
    void Awake()
    {
        // 싱글톤 패턴: 단 하나의 인스턴스만 유지
        if (instance != null)
        {
            Debug.LogWarning("GameManager 중복 생성 시도. 기존 인스턴스가 있어 자신을 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // 이 GameManager는 씬 전환 시 파괴되지 않도록 설정

        Debug.Log("GameManager Awake: 초기화 완료.");
        // 초기에는 게임이 일시 정지 상태가 아님
        isPaused = false; 
        Time.timeScale = 1f;
        
        // 씬 시작 시 커서는 숨기고 잠금 (게임 플레이를 위해)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // settingPanel은 초기에는 비활성화 상태
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
    }


    void Update()
    {
        // ESC 키 입력 처리
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        // '멈춤' 상태이면 아래 입력을 무시
        if (isPaused)
        {
            return;
        }

        // 대사 넘김 입력 (마우스 클릭 또는 스페이스바)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            // '입력 무시' 깃발이 세워져 있다면, 깃발만 내리고 아무것도 하지 않음
            if (blockNextInput)
            {
                blockNextInput = false;
                return;
            }

            // 깃발이 내려가 있을 때만 대사를 넘김
            if (flowchart != null)
            {
                flowchart.SendFungusMessage("Continue");
            }
        }
    }

    void PauseGame()
    {
        isPaused = true;
        // GameManager 내에서 직접 settingPanel을 제어하는 대신, 
        // SettingManager.instance가 있다면 그를 통해 패널을 여는 것을 권장합니다.
        // 하지만 현재 코드는 settingPanel을 직접 관리하므로 그대로 둡니다.
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // 게임을 재개할 때, '입력 무시' 깃발을 세운다
        blockNextInput = true;
    }
}