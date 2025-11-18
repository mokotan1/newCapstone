using UnityEngine;

using System.Collections; // 코루틴 사용을 위해 필요

using UnityEngine.Rendering;

using UnityEngine.Rendering.Universal; // URP의 ColorAdjustments, Vignette 사용을 위해 필요



public class FitchenPostExposureController : MonoBehaviour

{

    // 인스펙터 창에서 'Global Post-process Volume' 게임 오브젝트를 여기에 연결해주세요.

    [SerializeField] private Volume postProcessVolume;



    // Post Processing 효과 변수들

    private ColorAdjustments colorAdjustments;

    private Vignette vignette;



    // 코루틴 추적 변수들 (Vignette 제외)

    private Coroutine postExposureFadeCoroutine;

    private Coroutine contrastFadeCoroutine;



    // 효과 페이드 지속 시간 (인스펙터에서 조절 가능)

    [Tooltip("효과가 원래 값으로 돌아가는 데 걸리는 시간(초)")]

    [SerializeField] private float fadeDuration = 3.0f;



    void Start()

    {

        if (postProcessVolume == null)

        {

            Debug.LogError("Post Process Volume이 Inspector에 연결되지 않았습니다!", this);

            return;

        }



        // Color Adjustments 효과 가져오기

        if (postProcessVolume.profile.TryGet(out colorAdjustments))

        {

            Debug.Log("Color Adjustments 효과를 찾았습니다.");

            colorAdjustments.postExposure.value = -1.5f;

            colorAdjustments.contrast.value = 50f;

        }

        else

        {

            Debug.LogError("지정된 Volume Profile에서 Color Adjustments 효과를 찾을 수 없습니다!", this);

        }



        // Vignette 효과 가져오기 (초기화 로직은 제거, 필요시 직접 설정)

        if (postProcessVolume.profile.TryGet(out vignette))

        {
            vignette.intensity.value = 0.4f;
            Debug.Log("Vignette 효과를 찾았습니다.");

            // vignette.intensity.value = 0f; // 시작 시 초기화 제거 (원래 Profile 값 유지)

        }

        else

        {

            Debug.LogWarning("지정된 Volume Profile에서 Vignette 효과를 찾을 수 없습니다!", this);

        }

    }



    // ======================================================================================

    // 외부에서 호출할 함수들 (Fungus Call Method 사용)

    // ======================================================================================



    /// <summary>

    /// 섬광탄처럼 화면을 하얗게 만들고 지정된 시간 동안 원래대로 페이드 아웃합니다.

    /// </summary>

    public void TriggerFlashEffect(float peakExposure = 6f, float targetExposure = 0f)

    {

        if (colorAdjustments == null) return;



        if (postExposureFadeCoroutine != null) StopCoroutine(postExposureFadeCoroutine);

        postExposureFadeCoroutine = StartCoroutine(FadeEffect(

            (value) => colorAdjustments.postExposure.value = value,

            peakExposure, targetExposure, fadeDuration

        ));

    }



    /// <summary>

    /// 명암비(Contrast)를 조절하여 눈 깜빡임 후 적응 효과를 만듭니다.

    /// </summary>

    public void TriggerBlinkEffect(float peakContrast = 50f, float targetContrast = 0f)

    {

        if (colorAdjustments == null) return;



        if (contrastFadeCoroutine != null) StopCoroutine(contrastFadeCoroutine);

        contrastFadeCoroutine = StartCoroutine(FadeEffect(

            (value) => colorAdjustments.contrast.value = value,

            peakContrast, targetContrast, fadeDuration

        ));

    }



    /// <summary>

    /// Vignette 강도를 즉시 0으로 설정합니다. (사용자 요구사항 반영)

    /// </summary>

    public void SetVignetteToZero(float value) // 원래 함수 이름 유지

    {

        if (vignette != null)

        {

            vignette.intensity.value = value;

            Debug.Log("Vignette Intensity set to 0 (instant)."); // 디버그 메시지 수정

        }

        else

        {

            Debug.LogError("Cannot set Vignette intensity: Vignette effect not found."); // 디버그 메시지 수정

        }

    }



    // ======================================================================================

    // 코루틴 헬퍼 함수 (내부 사용)

    // ======================================================================================



    private IEnumerator FadeEffect(System.Action<float> setter, float startValue, float endValue, float duration)

    {

        setter(startValue);

        Debug.Log($"Effect starting from {startValue}, fading to {endValue} over {duration}s.");



        float elapsedTime = 0f;

        while (elapsedTime < duration)

        {

            float currentValue = Mathf.Lerp(startValue, endValue, elapsedTime / duration);

            setter(currentValue);

            elapsedTime += Time.deltaTime;

            yield return null;

        }



        setter(endValue);

        Debug.Log($"Effect fade complete, set to {endValue}.");

        // 해당 코루틴 변수를 null로 설정 (각 호출 지점에서 필요시)

        if (setter.Target == colorAdjustments && setter.Method.Name.Contains("postExposure")) postExposureFadeCoroutine = null;

        if (setter.Target == colorAdjustments && setter.Method.Name.Contains("contrast")) contrastFadeCoroutine = null;

    }

}