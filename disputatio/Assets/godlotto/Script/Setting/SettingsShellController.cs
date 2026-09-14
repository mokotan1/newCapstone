using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared settings chrome. Tab pages swap inside Body; sidebar/header/footer stay put.
/// </summary>
public sealed class SettingsShellController : MonoBehaviour
{
    public const string RootName = "SettingsWoodShell";

    public RectTransform Sidebar;
    public RectTransform Header;
    public RectTransform Divider;
    public RectTransform Body;
    public RectTransform Footer;
    public GameObject GeneralPage;
    public GameObject CheshireAiPage;
    public GameObject LanguagePage;
    public Button TabGeneral;
    public Button TabCheshireAi;
    public Button TabLanguage;
    public TMP_Text TabGeneralLabel;
    public TMP_Text TabCheshireAiLabel;
    public TMP_Text TabLanguageLabel;
    public TMP_Text TabGeneralArrow;
    public TMP_Text TabCheshireAiArrow;
    public TMP_Text TabLanguageArrow;
    public TMP_Text Heading;
    public TMP_Text Subtitle;
    public TMP_Text FooterNote;
    public TMP_Text Aside;
    public TMP_Text ReturnLabel;
    public Button ReturnButton;
    public ScrollRect BodyScroll;
    public SettingsCheshirePreview Preview;

    SettingsTabId _tab = SettingsTabId.General;
    bool _inGameContext = true;

    public SettingsTabId CurrentTab => _tab;
    public bool InGameContext => _inGameContext;

    public void BindTabs()
    {
        WireTab(TabGeneral, SettingsTabId.General);
        WireTab(TabCheshireAi, SettingsTabId.CheshireAi);
        WireTab(TabLanguage, SettingsTabId.Language);
        if (ReturnButton == null)
            return;
        ReturnButton.onClick.RemoveListener(HandleReturn);
        ReturnButton.onClick.AddListener(HandleReturn);
    }

    public void BindReturnContext(bool inGame)
    {
        _inGameContext = inGame;
        RefreshLabels();
    }

    public void SelectTab(SettingsTabId tab)
    {
        _tab = tab;
        if (GeneralPage != null)
            GeneralPage.SetActive(tab == SettingsTabId.General);
        if (CheshireAiPage != null)
            CheshireAiPage.SetActive(tab == SettingsTabId.CheshireAi);
        if (LanguagePage != null)
            LanguagePage.SetActive(tab == SettingsTabId.Language);
        if (BodyScroll != null)
            BodyScroll.verticalNormalizedPosition = 1f;
        ApplyTabVisuals();
        RefreshLabels();
    }

    public SettingsChromeSnapshot CaptureChrome()
    {
        return new SettingsChromeSnapshot(
            PositionOf(Sidebar),
            PositionOf(Header),
            PositionOf(Footer),
            SizeOf(Sidebar),
            SizeOf(Header),
            SizeOf(Footer));
    }

    public void RefreshLabels()
    {
        string locale = SettingsDisplayPreferences.ResolveLocale();
        SetText(TabGeneralLabel, Lookup("SettingsTabGeneral", locale));
        SetText(TabCheshireAiLabel, Lookup("SettingsTabAi", locale));
        SetText(TabLanguageLabel, Lookup("SettingsTabLanguage", locale));
        SetText(Subtitle, Lookup("SettingsSubtitle", locale));
        SetText(FooterNote, Lookup("SettingsFooterNote", locale));
        SetText(Aside, Lookup("SettingsAside", locale));
        SetText(ReturnLabel, Lookup(_inGameContext ? "SettingsBackToGame" : "SettingsBackToMenu", locale));

        if (_tab == SettingsTabId.CheshireAi)
            SetText(Heading, Lookup("SettingsTitleAi", locale));
        else if (_tab == SettingsTabId.Language)
            SetText(Heading, Lookup("SettingsTabLanguage", locale));
        else
            SetText(Heading, Lookup("SettingsTabGeneral", locale));

        RefreshRowLabel(GeneralPage, "BgmRow", "SettingsBgm", locale);
        RefreshRowLabel(GeneralPage, "SfxRow", "SettingsSfx", locale);
        RefreshRowLabel(GeneralPage, "ResolutionRow", "SettingsResolution", locale);
        RefreshRowLabel(GeneralPage, "FullscreenRow", "SettingsFullscreen", locale);
        RefreshRowLabel(LanguagePage, "LanguageRow", "SettingsLanguage", locale);
        RefreshRowLabel(LanguagePage, "TextSizeRow", "SettingsTextSize", locale);
        if (LanguagePage != null)
        {
            Transform hint = LanguagePage.transform.Find("LanguageHint");
            SetText(hint != null ? hint.GetComponent<TMP_Text>() : null, Lookup("SettingsLanguageHint", locale));
        }

        if (CheshireAiPage != null)
        {
            LocalAiSettingsPanel localAi = CheshireAiPage.GetComponentInChildren<LocalAiSettingsPanel>(true);
            localAi?.RefreshLocalizedText();
        }

        if (Preview != null)
            Preview.RefreshLabels(locale);
    }

    static void RefreshRowLabel(GameObject page, string rowName, string key, string locale)
    {
        if (page == null)
            return;
        Transform row = page.transform.Find(rowName);
        Transform label = row != null ? row.Find("Label") : null;
        SetText(label != null ? label.GetComponent<TMP_Text>() : null, Lookup(key, locale));
    }

    void HandleReturn()
    {
        InGameSettingsPanel inGame = GetComponentInParent<InGameSettingsPanel>();
        if (inGame != null)
        {
            inGame.ReturnToGame();
            return;
        }

        IntegratedSettingUI integrated = GetComponentInParent<IntegratedSettingUI>();
        if (integrated == null)
            return;

        if (integrated.uiMode == IntegratedSettingUI.UIMode.StandaloneScene)
            integrated.BackToMainMenu();
        else
            integrated.ReturnToGame();
    }

    void WireTab(Button button, SettingsTabId tab)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectTab(tab));
    }

    void ApplyTabVisuals()
    {
        StyleTab(TabGeneral, TabGeneralArrow, _tab == SettingsTabId.General);
        StyleTab(TabCheshireAi, TabCheshireAiArrow, _tab == SettingsTabId.CheshireAi);
        StyleTab(TabLanguage, TabLanguageArrow, _tab == SettingsTabId.Language);
    }

    static void StyleTab(Button button, TMP_Text arrow, bool selected)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected
                ? SettingsWoodPanelSpec.SelectedBackground
                : SettingsWoodPanelSpec.ButtonFace;
        }

        if (arrow != null)
            arrow.text = "";

        Image marker = button.transform.Find("Marker")?.GetComponent<Image>();
        if (marker != null)
            marker.enabled = selected;

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
            outline.effectColor = selected ? SettingsWoodPanelSpec.SelectedBorder : SettingsWoodPanelSpec.Border;

        TMP_Text index = button.transform.Find("Index")?.GetComponent<TMP_Text>();
        if (index != null)
            index.color = selected ? SettingsWoodPanelSpec.Accent : SettingsWoodPanelSpec.SecondaryText;
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label != null)
            label.text = value ?? "";
    }

    static string Lookup(string key, string locale)
    {
        return CheshireUiStrings.Lookup(key, locale);
    }

    static Vector2 PositionOf(RectTransform rect)
    {
        return rect != null ? rect.anchoredPosition : Vector2.zero;
    }

    static Vector2 SizeOf(RectTransform rect)
    {
        return rect != null ? rect.rect.size : Vector2.zero;
    }
}
