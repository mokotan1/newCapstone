using UnityEngine;
using Fungus;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public GameObject settingPanel;
    public Flowchart flowchart;
    public static bool isPaused = false;
    
    // 이 '깃발'은 다음 입력이 있을 때까지 입력을 막습니다.
    private bool blockNextInput = false;

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
        settingPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        settingPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // 게임을 재개할 때, '입력 무시' 깃발을 세운다
        blockNextInput = true;
    }
}