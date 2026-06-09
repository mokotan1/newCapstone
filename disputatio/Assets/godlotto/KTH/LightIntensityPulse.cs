using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class LightIntensityPulse : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("If empty, this script uses the Light2D on the same GameObject.")]
    [SerializeField] private Light2D targetLight;

    [Header("Intensity")]
    [Tooltip("Lowest light intensity.")]
    [Min(0f)]
    [SerializeField] private float minIntensity = 0.5f;

    [Tooltip("Highest light intensity.")]
    [Min(0f)]
    [SerializeField] private float maxIntensity = 2f;

    [Header("Timing")]
    [Tooltip("Seconds for one full brighten-and-dim cycle.")]
    [Min(0.01f)]
    [SerializeField] private float cycleDuration = 2f;

    [Tooltip("Delay before the pulse starts.")]
    [Min(0f)]
    [SerializeField] private float startDelay = 0f;

    [Tooltip("Randomizes the starting point so several lights do not pulse identically.")]
    [SerializeField] private bool randomStartOffset = false;

    [Header("Shape")]
    [Tooltip("Controls the pulse shape. 0 is dim, 1 is bright.")]
    [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f));

    private float timeOffset;

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light2D>();
        }

        if (randomStartOffset)
        {
            timeOffset = Random.Range(0f, cycleDuration);
        }
    }

    private void Update()
    {
        if (targetLight == null || Time.time < startDelay)
        {
            return;
        }

        float elapsed = Time.time - startDelay + timeOffset;
        float cycleTime = Mathf.Repeat(elapsed / cycleDuration, 1f);
        float curveValue = Mathf.Clamp01(pulseCurve.Evaluate(cycleTime));

        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, curveValue);
    }

    private void OnValidate()
    {
        cycleDuration = Mathf.Max(0.01f, cycleDuration);
        minIntensity = Mathf.Max(0f, minIntensity);
        maxIntensity = Mathf.Max(0f, maxIntensity);

        if (maxIntensity < minIntensity)
        {
            maxIntensity = minIntensity;
        }
    }
}
