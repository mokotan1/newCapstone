using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Fungus;

// 타겟 위치와 조절할 대상(SpriteRenderer)을 묶어서 관리합니다.
[System.Serializable]
public class InteractableTarget
{
    [Tooltip("마우스를 가져다 댈 목표 위치(Transform)를 연결하세요.")]
    public Transform targetPosition;
    [Tooltip("알파값이 조절되며 켜질 대상의 SpriteRenderer를 연결하세요.")]
    public SpriteRenderer targetSprite;
}

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

    [Header("상호작용 설정")]
    [Tooltip("마우스가 인식되는 최대 범위(알파값이 0이 되는 지점)를 설정하세요.")]
    public float detectionRadius = 2.0f;
    [Tooltip("알파값이 최대치에 도달하는 내부 범위(완전히 선명해지는 지점)를 설정하세요.")]
    public float maxAlphaRadius = 0.5f;
    [Tooltip("적용될 최대 알파값 (0 ~ 1 사이의 값)")]
    [Range(0f, 1f)]
    public float maxAlphaValue = 1.0f;
    [Tooltip("상호작용할 목표 위치와 대상 스프라이트의 목록을 설정하세요.")]
    public List<InteractableTarget> interactableTargets = new List<InteractableTarget>();

    private bool previousElectricState = false;
    private GameObject flashlightInstance;
    private Camera mainCamera;
    
    private bool isFlashlightOn = false;

    private void Start()
    {
        mainCamera = Camera.main;

        if (flashlightPrefab != null)
        {
            flashlightInstance = Instantiate(flashlightPrefab);
            flashlightInstance.SetActive(isFlashlightOn);
        }

        if (targetFlowchart != null)
        {
            previousElectricState = targetFlowchart.GetBooleanVariable(variableName);
            UpdateEnvironmentState(previousElectricState);
        }

        // 시작 시 모든 타겟을 비활성화하고 알파값을 0으로 초기화합니다.
        foreach (var target in interactableTargets)
        {
            if (target.targetSprite != null)
            {
                SetSpriteAlpha(target.targetSprite, 0f);
                target.targetSprite.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // --- 손전등 켜기/끄기 제어 ---
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }

        // --- 상호작용, 알파값 조절 및 손전등 위치 제어 ---
        if (isFlashlightOn && flashlightInstance != null)
        {
            Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = 0f; 
            flashlightInstance.transform.position = mousePosition;

            foreach (var target in interactableTargets)
            {
                if (target.targetPosition != null && target.targetSprite != null)
                {
                    float distance = Vector2.Distance(mousePosition, target.targetPosition.position);

                    // 인식 범위 밖에 있는 경우
                    if (distance > detectionRadius)
                    {
                        if (target.targetSprite.gameObject.activeSelf)
                        {
                            target.targetSprite.gameObject.SetActive(false);
                        }
                    }
                    // 인식 범위 안에 있는 경우
                    else
                    {
                        if (!target.targetSprite.gameObject.activeSelf)
                        {
                            target.targetSprite.gameObject.SetActive(true);
                        }

                        // 거리에 따른 알파값 계산 (InverseLerp를 사용하여 비율을 구합니다)
                        // distance가 detectionRadius일 때 0, maxAlphaRadius일 때 1을 반환합니다.
                        float alphaRatio = Mathf.InverseLerp(detectionRadius, maxAlphaRadius, distance);
                        float currentAlpha = Mathf.Lerp(0f, maxAlphaValue, alphaRatio);

                        SetSpriteAlpha(target.targetSprite, currentAlpha);
                    }
                }
            }
        }
        else
        {
            // 손전등이 꺼져있다면 모든 타겟 오브젝트를 비활성화합니다.
            foreach (var target in interactableTargets)
            {
                if (target.targetSprite != null && target.targetSprite.gameObject.activeSelf)
                {
                    target.targetSprite.gameObject.SetActive(false);
                }
            }
        }

        // --- 환경 조명 제어 ---
        if (targetFlowchart == null) return;

        bool currentElectricState = targetFlowchart.GetBooleanVariable(variableName);

        if (currentElectricState != previousElectricState)
        {
            UpdateEnvironmentState(currentElectricState);
            previousElectricState = currentElectricState;
        }
    }

    private void UpdateEnvironmentState(bool isElectricOn)
    {
        foreach (Light2D light in targetLights)
        {
            if (light != null) light.enabled = isElectricOn;
        }

        foreach (SpriteRenderer sprite in targetSprites)
        {
            if (sprite != null) sprite.enabled = isElectricOn;
        }
    }

    private void ToggleFlashlight()
    {
        if (flashlightInstance != null)
        {
            isFlashlightOn = !isFlashlightOn;
            flashlightInstance.SetActive(isFlashlightOn);
        }
    }

    // 스프라이트의 알파값을 변경하는 편의 메서드입니다.
    private void SetSpriteAlpha(SpriteRenderer sprite, float alpha)
    {
        Color color = sprite.color;
        color.a = alpha;
        sprite.color = color;
    }

    // 에디터에서 두 가지 인식 범위를 직관적으로 확인할 수 있도록 기즈모를 그립니다.
    private void OnDrawGizmosSelected()
    {
        foreach (var target in interactableTargets)
        {
            if (target != null && target.targetPosition != null)
            {
                // 최대 알파값 범위 (녹색 원)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(target.targetPosition.position, maxAlphaRadius);

                // 전체 인식 범위 (노란색 원)
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(target.targetPosition.position, detectionRadius);
            }
        }
    }
}