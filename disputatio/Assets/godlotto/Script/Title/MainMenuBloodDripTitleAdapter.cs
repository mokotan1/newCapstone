using TMPro;
using UnityEngine;

/// <summary>
/// Wires the blood-drip renderer to an existing main-menu TMP title without replacing its text.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuBloodDripTitleAdapter : MonoBehaviour
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] BloodDripTitleRenderer renderer;
    [SerializeField] bool loadVisualParamsFromMock = true;
    [SerializeField] bool applyOnStart = true;

    void Awake()
    {
        if (titleText == null)
            titleText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (renderer == null)
            renderer = GetComponent<BloodDripTitleRenderer>();

        if (renderer != null && titleText != null)
            renderer.SetTitleTextForRuntime(titleText);
    }

    void Start()
    {
        if (!applyOnStart || renderer == null || titleText == null)
            return;

        TitleStylePayload payload = loadVisualParamsFromMock
            ? TitleStyleService.LoadMockPayload()
            : TitleStylePayload.CreateDefault();

        renderer.ApplyEffectToExistingTitle(payload);
        HorrorTitleTypography.ApplyToMainMenu(titleText, payload);
        renderer.RefreshGlyphAnchors();
        renderer.RestartDrips();
    }

#if UNITY_EDITOR
    public void ConfigureForTests(
        TMP_Text text,
        BloodDripTitleRenderer dripRenderer,
        bool useMockVisualParams,
        bool runOnStart)
    {
        titleText = text;
        renderer = dripRenderer;
        loadVisualParamsFromMock = useMockVisualParams;
        applyOnStart = runOnStart;
    }
#endif
}
