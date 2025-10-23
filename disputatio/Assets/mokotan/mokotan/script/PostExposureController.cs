using UnityEngine;
using UnityEngine.Rendering;          // Volume 사용을 위해 필요
using UnityEngine.Rendering.Universal; // URP의 ColorAdjustments 사용을 위해 필요
using System.Collections;

public class PostExposureController : MonoBehaviour
{
    // 인스펙터 창에서 'Global Post-process Volume' 게임 오브젝트를 여기에 연결해주세요.
    [SerializeField] private Volume postProcessVolume;

    private ColorAdjustments colorAdjustments; // Color Adjustments 효과를 담을 변수
    private Vignette vignette;

    private Coroutine fadeCoroutine; // 현재 실행 중인 코루틴을 저장할 변수
    [Tooltip("효과가 0으로 돌아가는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeDuration = 3.0f;

    void Start()
    {
        if (postProcessVolume != null && postProcessVolume.profile.TryGet(out colorAdjustments))
        {
            Debug.Log("Color Adjustments 효과를 찾았습니다.");
            // 시작 시 노출 값을 0으로 초기화 (선택 사항)
            colorAdjustments.postExposure.value = 0f;
        }
        else
        {
            Debug.LogError("지정된 Volume Profile에서 Color Adjustments 효과를 찾을 수 없습니다!");
        }

        if (postProcessVolume.profile.TryGet(out vignette))
            {
                Debug.Log("Vignette 효과를 찾았습니다.");
                vignette.intensity.value = 0.7f; // 시작 시 초기화 (원하는 경우)
            }
    }

    // Fungus에서 이 함수를 호출합니다.
    public void TriggerFlashEffect()
    {
        // 이미 페이드 효과가 진행 중이라면 중지시킵니다.
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        // 새로운 페이드 효과 코루틴을 시작하고 저장합니다.
        fadeCoroutine = StartCoroutine(FadeExposure(7f, 2f, fadeDuration));
    }

  private IEnumerator FadeExposure(float startValue, float endValue, float duration)
    {
        // 즉시 시작 값(6)으로 설정
        colorAdjustments.postExposure.value = startValue;
        Debug.Log($"Post Exposure set to {startValue}, starting fade to {endValue} over {duration}s.");

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // 경과 시간에 따라 시작 값과 끝 값 사이를 보간(Lerp)합니다.
            float currentValue = Mathf.Lerp(startValue, endValue, elapsedTime / duration);
            colorAdjustments.postExposure.value = currentValue;

            elapsedTime += Time.deltaTime; // 다음 프레임까지 걸린 시간만큼 경과 시간 증가
            yield return null; // 다음 프레임까지 대기
        }

        // 정확히 목표 값(0)으로 설정하여 마무리
        colorAdjustments.postExposure.value = endValue;
        Debug.Log("Post Exposure fade complete, set to 0.");
        fadeCoroutine = null; // 코루틴 완료 표시
    }
    public void SetVignnetToZero()
    {
        if (vignette != null)
        {
            // Post Exposure 값을 6으로 설정
            vignette.intensity.value = 0f;
            Debug.Log("Post Exposure 값이 6으로 설정되었습니다.");
        }
        else
        {
            Debug.LogError("노출 값을 설정할 수 없습니다: Color Adjustments를 찾을 수 없습니다.");
        }
    }

}