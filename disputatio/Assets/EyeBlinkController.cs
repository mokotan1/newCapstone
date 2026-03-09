using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering; // Volume을 쓰기 위해 필수
using UnityEngine.Rendering.Universal; // URP 효과(DepthOfField)를 쓰기 위해 필수

public class EyeBlinkController : MonoBehaviour
{
    [Header("UI Reference")]
    public Image blinkImage; 

    [Header("Post Processing")]
    public Volume globalVolume; // 인스펙터에서 Global Volume을 넣어주세요.

    [Header("Blink Settings")]
    public float blinkDuration = 0.2f; 
    public float closedDuration = 0.1f; 

    // 0.5 = 눈 뜸(Blur 0), 0 = 눈 감음(Blur 최대)
    private float openValue = 0.5f;
    private float closedValue = 0f;

    // 흐림 강도 설정 (0 = 선명함, 1.5 = 적당히 흐림, 3 = 매우 흐림)
    private float maxBlurAmount = 2.0f;

    private readonly int blinkAmountProp = Shader.PropertyToID("_BlinkAmount");
    private DepthOfField dof; // 피사계 심도 컴포넌트

    void Start()
    {
        // 1. UI 머티리얼 복제
        if (blinkImage != null && blinkImage.material != null)
        {
            blinkImage.material = new Material(blinkImage.material);
            blinkImage.material.SetFloat(blinkAmountProp, openValue);
        }

        // 2. Volume에서 DepthOfField 가져오기
        if (globalVolume != null)
        {
            if (globalVolume.profile.TryGet(out dof))
            {
                // 시작할 때 흐림 효과를 0으로 초기화
                dof.gaussianMaxRadius.value = 0f;
            }
            else
            {
                Debug.LogWarning("Volume 프로필에 'Depth Of Field'가 추가되지 않았습니다!");
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(BlinkRoutine());
        }
    }

    private IEnumerator BlinkRoutine()
    {
        // 눈 감기 (선명 -> 흐림)
        yield return StartCoroutine(AnimateBlink(openValue, closedValue, 0f, maxBlurAmount, blinkDuration));
        
        // 감은 상태 유지
        yield return new WaitForSeconds(closedDuration);
        
        // 눈 뜨기 (흐림 -> 선명)
        yield return StartCoroutine(AnimateBlink(closedValue, openValue, maxBlurAmount, 0f, blinkDuration));
    }

    // 눈 깜박임(Blink)과 흐림(Blur)을 동시에 처리하는 함수
    private IEnumerator AnimateBlink(float blinkStart, float blinkEnd, float blurStart, float blurEnd, float duration)
    {
        float elapsed = 0f;
        Material mat = blinkImage.material;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t); // 부드러운 움직임

            // 1. 눈꺼풀 닫기
            mat.SetFloat(blinkAmountProp, Mathf.Lerp(blinkStart, blinkEnd, smoothT));

            // 2. 화면 흐리게 만들기
            if (dof != null)
            {
                dof.gaussianMaxRadius.value = Mathf.Lerp(blurStart, blurEnd, smoothT);
            }

            yield return null;
        }
        
        // 최종값 고정
        mat.SetFloat(blinkAmountProp, blinkEnd);
        if (dof != null) dof.gaussianMaxRadius.value = blurEnd;
    }
}