using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostExposureController : MonoBehaviour
{
    [SerializeField] private Volume postProcessVolume;

    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private Bloom bloom;

    // 코루틴 관리 변수
    private Coroutine postExposureFadeCoroutine;
    private Coroutine contrastFadeCoroutine;
    private Coroutine eyeBlinkCoroutine;

    [Tooltip("기타 효과 페이드 지속 시간")]
    [SerializeField] private float fadeDuration = 3.0f;

    void Start()
    {
        if (postProcessVolume == null)
        {
            Debug.LogError("Post Process Volume이 Inspector에 연결되지 않았습니다!", this);
            return;
        }

        // 프로필에서 컴포넌트 가져오기
        if (postProcessVolume.profile.TryGet(out colorAdjustments)) GameLog.Log("Color Adjustments 찾음");
        if (postProcessVolume.profile.TryGet(out vignette)) GameLog.Log("Vignette 찾음");
        if (postProcessVolume.profile.TryGet(out bloom)) GameLog.Log("Bloom 찾음");
    }

    // ---------------------------------------------------------
    // ★ Fungus 연동용 핵심 함수들 (눈 깜빡임)
    // ---------------------------------------------------------

    /// <summary>
    /// [즉시 감기] Vignette: 1, Exposure: -3
    /// </summary>
    public void SetEyesClosed()
    {
        ApplyEyeState(1f, -3f);
        GameLog.Log("눈을 즉시 감았습니다.");
    }

    /// <summary>
    /// [즉시 뜨기] Vignette: 0.3, Exposure: 1.5
    /// </summary>
    public void SetEyesOpened()
    {
        ApplyEyeState(0.3f, 1.5f); 
        GameLog.Log("눈을 즉시 떴습니다.");
    }

    /// <summary>
    /// [서서히 뜨기] 감은 상태(-3) -> 뜬 상태(1.5)
    /// Fungus SendMessage로 호출: SetEyesOpenSmoothly (Float 값: 시간)
    /// </summary>
    public void SetEyesOpenSmoothly(float duration)
    {
        if (duration <= 0) { SetEyesOpened(); return; }
        
        // startVig: 1(감음), endVig: 0.3(뜸)
        // startExp: -3(어둠), endExp: 1.5(밝음)
        StartEyeCoroutine(1f, 0.3f, -3f, 1.5f, duration);
    }

    /// <summary>
    /// [서서히 감기] 뜬 상태(1.5) -> 감은 상태(-3)
    /// Fungus SendMessage로 호출: SetEyesClosedSmoothly (Float 값: 시간)
    /// </summary>
    public void SetEyesClosedSmoothly(float duration)
    {
        if (duration <= 0) { SetEyesClosed(); return; }

        // startVig: 0.3(뜸), endVig: 1(감음)
        // startExp: 1.5(밝음), endExp: -3(어둠)
        StartEyeCoroutine(0.3f, 1f, 1.5f, -3f, duration);
    }

    // ---------------------------------------------------------
    // ★ 극적인 엔딩 연출 함수 (체크박스 강제 활성화 포함)
    // ---------------------------------------------------------

    /// <summary>
    /// [극적 연출 서서히 적용] 
    /// Vignette: #762921, Intensity: 1, Smoothness: 1, Exposure: 0.5
    /// Fungus SendMessage로 호출: SetDramaticEndingSmoothly (Float 값: 시간)
    /// </summary>
    public void SetDramaticEndingSmoothly(float duration)
    {
        if (duration <= 0) { SetDramaticEnding(); return; }

        // #762921 색상
        Color targetColor = new Color(0.46f, 0.16f, 0.13f, 1.0f); 

        if (eyeBlinkCoroutine != null) StopCoroutine(eyeBlinkCoroutine);
        eyeBlinkCoroutine = StartCoroutine(SmoothDramaticProcess(duration, targetColor));
    }

    /// <summary>
    /// [극적 연출 즉시 적용]
    /// </summary>
    public void SetDramaticEnding()
    {
        Color targetColor = new Color(0.46f, 0.16f, 0.13f, 1.0f); 
        ApplyDramaticState(1f, 1f, targetColor, 0.5f);
    }

    // ---------------------------------------------------------
    // 내부 로직 (Helper Functions)
    // ---------------------------------------------------------

    // 일반 눈 깜빡임 적용
    private void ApplyEyeState(float vigValue, float expValue)
    {
        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = vigValue;
            
            // 눈 깜빡임 모드일 때는 색상을 검정(기본)으로 강제 복구
            vignette.color.overrideState = true; 
            vignette.color.value = Color.black;
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.active = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = expValue;
        }
    }

    // 극적 연출 적용 (체크박스 강제 켜기)
    private void ApplyDramaticState(float intensity, float smoothness, Color color, float exposure)
    {
        if (vignette != null)
        {
            vignette.active = true;
            
            // 1. Intensity 체크박스 켜기 & 값 설정
            vignette.intensity.overrideState = true; 
            vignette.intensity.value = intensity;

            // 2. Smoothness 체크박스 켜기 & 값 설정
            vignette.smoothness.overrideState = true; 
            vignette.smoothness.value = smoothness;

            // 3. Color 체크박스 켜기 & 값 설정
            vignette.color.overrideState = true; 
            vignette.color.value = color;
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.active = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = exposure;
        }
    }

    private void StartEyeCoroutine(float startVig, float endVig, float startExp, float endExp, float duration)
    {
        if (eyeBlinkCoroutine != null) StopCoroutine(eyeBlinkCoroutine);
        eyeBlinkCoroutine = StartCoroutine(SmoothEyeProcess(startVig, endVig, startExp, endExp, duration));
    }

    private IEnumerator SmoothEyeProcess(float startVig, float endVig, float startExp, float endExp, float duration)
    {
        float time = 0f;
        ApplyEyeState(startVig, startExp); 

        while (time < duration)
        {
            time += Time.deltaTime;
            float tSmooth = Mathf.SmoothStep(0f, 1f, time / duration);

            if (vignette != null) vignette.intensity.value = Mathf.Lerp(startVig, endVig, tSmooth);
            if (colorAdjustments != null) colorAdjustments.postExposure.value = Mathf.Lerp(startExp, endExp, tSmooth);
            yield return null;
        }
        ApplyEyeState(endVig, endExp); 
    }

    private IEnumerator SmoothDramaticProcess(float duration, Color targetColor)
    {
        float time = 0f;
        
        // 현재 값들 가져오기 (꺼져있으면 기본값 사용)
        float startVig = vignette != null ? vignette.intensity.value : 0f;
        float startSmooth = vignette != null ? vignette.smoothness.value : 0.2f;
        Color startColor = vignette != null ? vignette.color.value : Color.black;
        float startExp = colorAdjustments != null ? colorAdjustments.postExposure.value : 0f;

        // 시작하면서 체크박스들 강제로 다 켬
        ApplyDramaticState(startVig, startSmooth, startColor, startExp);

        GameLog.Log($"극적 연출 시작: {duration}초 동안 변환");

        while (time < duration)
        {
            time += Time.deltaTime;
            float tSmooth = Mathf.SmoothStep(0f, 1f, time / duration);

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(startVig, 1f, tSmooth);
                vignette.smoothness.value = Mathf.Lerp(startSmooth, 1f, tSmooth);
                vignette.color.value = Color.Lerp(startColor, targetColor, tSmooth);
            }
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.value = Mathf.Lerp(startExp, 0.5f, tSmooth);
            }
            yield return null;
        }

        // 끝날 때도 확실하게 값 고정
        ApplyDramaticState(1f, 1f, targetColor, 0.5f);
    }
    
    // ---------------------------------------------------------
    // ▼ 레거시 함수들 (완전 복구됨)
    // ---------------------------------------------------------

    public void TriggerFlashEffect(float peakExposure = 6f, float targetExposure = 0f)
    {
        if (colorAdjustments == null) return;

        if (postExposureFadeCoroutine != null) StopCoroutine(postExposureFadeCoroutine);
        postExposureFadeCoroutine = StartCoroutine(FadeEffect(
            (value) => { 
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = value; 
            },
            peakExposure, targetExposure, fadeDuration
        ));
    }

    public void TriggerBlinkEffect(float peakContrast = 50f, float targetContrast = 0f)
    {
        if (colorAdjustments == null) return;

        if (contrastFadeCoroutine != null) StopCoroutine(contrastFadeCoroutine);
        contrastFadeCoroutine = StartCoroutine(FadeEffect(
            (value) => { 
                colorAdjustments.contrast.overrideState = true;
                colorAdjustments.contrast.value = value; 
            },
            peakContrast, targetContrast, fadeDuration
        ));
    }

    public void SetVignetteToZero(float value)
    {
        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = value;
            GameLog.Log($"Vignette Intensity set to {value} (instant).");
        }
        else
        {
            Debug.LogError("Cannot set Vignette intensity: Vignette effect not found.");
        }
    }

    public void SetBloomToZero(float value)
    {
        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = value;
            GameLog.Log($"Bloom Intensity set to {value} (instant).");
        }
        else
        {
            Debug.LogError("Cannot set Bloom intensity: Bloom effect not found.");
        }
    }

    private IEnumerator FadeEffect(System.Action<float> setter, float startValue, float endValue, float duration)
    {
        setter(startValue);
        GameLog.Log($"Effect starting from {startValue}, fading to {endValue} over {duration}s.");

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float currentValue = Mathf.Lerp(startValue, endValue, elapsedTime / duration);
            setter(currentValue);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        setter(endValue);
        GameLog.Log($"Effect fade complete, set to {endValue}.");

        if (setter.Target == colorAdjustments && setter.Method.Name.Contains("postExposure")) postExposureFadeCoroutine = null;
        if (setter.Target == colorAdjustments && setter.Method.Name.Contains("contrast")) contrastFadeCoroutine = null;
    }
}