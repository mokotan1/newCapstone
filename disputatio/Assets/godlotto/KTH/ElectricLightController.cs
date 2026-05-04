using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Fungus;

public class ElectricLightController : MonoBehaviour
{
    [Header("펑거스 연결")]
    [Tooltip("ElectricOn 변수를 관리하는 플로우차트 오브젝트를 연결하세요.")]
    public Flowchart targetFlowchart;
    [Tooltip("감지할 펑거스 변수 이름")]
    public string variableName = "ElectricOn";

    [Header("다중 조명 설정")]
    [Tooltip("ElectricOn 상태에 따라 동시에 켜고 꺼질 2D 조명 오브젝트들을 모두 연결하세요.")]
    public List<Light2D> targetLights = new List<Light2D>();

    [Header("다중 스프라이트 설정")]
    [Tooltip("ElectricOn 상태에 따라 동시에 표시하거나 숨길 스프라이트 렌더러들을 모두 연결하세요.")]
    public List<SpriteRenderer> targetSprites = new List<SpriteRenderer>();

    [Header("손전등 설정")]
    [Tooltip("마우스를 따라다닐 손전등(Spot Light 2D) 프리팹을 연결하세요.")]
    public GameObject flashlightPrefab;

    private bool previousElectricState = false;
    private GameObject flashlightInstance;
    private Camera mainCamera;
    
    // 손전등의 현재 켜짐/꺼짐 상태를 저장하는 변수입니다.
    private bool isFlashlightOn = false;

    private void Start()
    {
        // 마우스 위치 좌표 변환을 위해 메인 카메라를 캐싱합니다.
        mainCamera = Camera.main;

        // 시작할 때 손전등 프리팹을 씬에 생성하고, 초기 상태(꺼짐)를 적용합니다.
        if (flashlightPrefab != null)
        {
            flashlightInstance = Instantiate(flashlightPrefab);
            flashlightInstance.SetActive(isFlashlightOn);
        }

        if (targetFlowchart != null)
        {
            // 씬 시작 시 펑거스 변수의 초기 상태를 읽어옵니다.
            previousElectricState = targetFlowchart.GetBooleanVariable(variableName);
            
            // 초기 상태에 맞게 환경 조명을 설정합니다.
            UpdateEnvironmentState(previousElectricState);
        }
    }

    private void Update()
    {
        // --- 손전등 제어 로직 ---
        // 키보드 F키를 누를 때마다 손전등을 켜거나 끕니다.
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }

        // 손전등이 켜져있고 인스턴스가 존재할 때만 마우스를 따라가게 합니다.
        if (isFlashlightOn && flashlightInstance != null)
        {
            // 화면상의 마우스 좌표를 게임 월드 좌표로 변환합니다.
            Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            // 2D 환경이므로 z축 좌표는 0으로 고정합니다.
            mousePosition.z = 0f; 
            
            flashlightInstance.transform.position = mousePosition;
        }

        // --- 환경 조명 제어 로직 ---
        // 연결이 누락되었으면 아래 로직은 작동하지 않습니다.
        if (targetFlowchart == null) return;

        // 매 프레임 펑거스 변수의 현재 상태를 읽어옵니다.
        bool currentElectricState = targetFlowchart.GetBooleanVariable(variableName);

        // 현재 상태가 이전 상태와 다를 때만 실행합니다.
        if (currentElectricState != previousElectricState)
        {
            UpdateEnvironmentState(currentElectricState);
            previousElectricState = currentElectricState;
        }
    }

    // 조명과 스프라이트의 활성화 상태를 일괄적으로 처리하는 메서드입니다.
    private void UpdateEnvironmentState(bool isElectricOn)
    {
        foreach (Light2D light in targetLights)
        {
            if (light != null)
            {
                light.enabled = isElectricOn;
            }
        }

        foreach (SpriteRenderer sprite in targetSprites)
        {
            if (sprite != null)
            {
                sprite.enabled = isElectricOn;
            }
        }
    }

    // 손전등의 상태를 반전시키는 메서드입니다.
    private void ToggleFlashlight()
    {
        if (flashlightInstance != null)
        {
            isFlashlightOn = !isFlashlightOn;
            flashlightInstance.SetActive(isFlashlightOn);
        }
    }
}