using UnityEngine;

/// <summary>
/// Smoke-test harness for the blood-drip title prefab.
/// Loads local mock JSON on start and exposes English/Korean payload switches.
/// </summary>
public class BloodDripTitleDemo : MonoBehaviour
{
    public const string SmokeTestNote =
        "Open Assets/Scenes/godlotto/BloodDripTitleDemo.unity, press Play, confirm DISPUTATIO renders, " +
        "then use the on-screen Korean button (or context menu Apply Korean Mock) to verify Nanum fallback.";

    [SerializeField] BloodDripTitleRenderer renderer;
    [SerializeField] bool loadMockOnStart = true;
    [SerializeField] KeyCode koreanToggleKey = KeyCode.K;

    void Awake()
    {
        if (renderer == null)
            renderer = GetComponentInChildren<BloodDripTitleRenderer>(true);
    }

    void Start()
    {
        if (loadMockOnStart)
            ApplyEnglishMock();
    }

    void Update()
    {
        if (Input.GetKeyDown(koreanToggleKey))
            ApplyKoreanMock();
    }

    [ContextMenu("Apply English Mock")]
    public void ApplyEnglishMock()
    {
        TitleStyleService.ResetCacheForTests();
        renderer?.ApplyPayload(TitleStyleService.LoadMockPayload());
    }

    [ContextMenu("Apply Korean Mock")]
    public void ApplyKoreanMock()
    {
        TitleStyleService.ResetCacheForTests();
        renderer?.ApplyPayload(TitleStyleService.LoadKoreanMockPayload());
    }
}
