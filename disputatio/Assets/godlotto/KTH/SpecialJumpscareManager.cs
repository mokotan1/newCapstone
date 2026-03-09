using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 입력 잠금을 위해 필요

public class SpecialJumpscareManager : MonoBehaviour
{
    private static bool hasVisitedSpecialScene = false;

    [Header("설정")]
    public float spawnChance = 100f;
    public string retrySceneName = "MainScene";

    [Header("오브젝트 연결 (계층 구조)")]
    public GameObject parrotObject;
    public GameObject enemyParent;      // ENEMY (Capsule Collider 2D 부착)
    public GameObject enemyPanel;       // PANEL
    public RawImage jumpscareRawImage;  // RAWIMAGE (Shader 및 Animator 부착)

    [Header("셰이더 및 입력 설정")]
    public Material jumpscareShaderMaterial; // 실행할 셰이더 그래프가 적용된 재질
    public GameObject gameOverPanel;
    public Button retryButton;

    private bool hasTriggered = false;

    void Start()
    {
        // 초기 상태: 적 관련 오브젝트 모두 비활성화
        enemyParent.SetActive(false);
        gameOverPanel.SetActive(false);

        if (!hasVisitedSpecialScene)
        {
            if (Random.Range(0f, 100f) <= spawnChance)
            {
                hasVisitedSpecialScene = true;
                ShowEnemy();
            }
            else
            {
                ShowParrotOnly();
            }
        }
        else
        {
            ShowParrotOnly();
        }
    }

    private void ShowEnemy()
    {
        if (parrotObject != null) parrotObject.SetActive(false);
        
        enemyParent.SetActive(true); // ENEMY 활성화
        enemyPanel.SetActive(true);  // PANEL 활성화 (필요 시)
        jumpscareRawImage.gameObject.SetActive(true);

        // 버튼 컴포넌트가 RawImage에 있다고 가정
        Button btn = jumpscareRawImage.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ExecuteJumpscare);
        }
    }

    private void ShowParrotOnly()
    {
        if (parrotObject != null) parrotObject.SetActive(true);
        enemyParent.SetActive(false);
    }

    public void ExecuteJumpscare()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        // 1. 모든 클릭 및 버튼 입력 잠금
        if (EventSystem.current != null)
        {
            EventSystem.current.enabled = false;
        }

        // 2. 셰이더 그래프 실행 (셰이더 내의 'Trigger'나 'StartTime' 파라미터 조절)
        // 예: 셰이더에 가동 시간을 전달하여 애니메이션 시작
        if (jumpscareShaderMaterial != null)
        {
            jumpscareShaderMaterial.SetFloat("_StartTime", Time.time); 
            // 셰이더 속성 이름은 실제 셰이더 그래프 내 설정에 맞춰 수정하세요.
        }

        // 3. 애니메이션 재생
        Animator anim = jumpscareRawImage.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetTrigger("Scare");
        }
    }

    // 애니메이션 이벤트에서 호출
    public void OnJumpscareFinished()
    {
        // 4. 입력 잠금 해제 (게임오버 UI는 눌러야 하므로)
        if (EventSystem.current != null)
        {
            EventSystem.current.enabled = true;
        }

        enemyParent.SetActive(false);
        gameOverPanel.SetActive(true);
        
        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() => SceneManager.LoadScene(retrySceneName));
    }
}