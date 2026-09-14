using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the wood settings shell used by SettingScene and the in-game panel.
/// </summary>
public static class SettingsShellFactory
{
    const string UiFontNameHint = "NanumGothic Bold";
    const string PixelFontNameHint = "Fantasypixelfont";
    const string FrameSpritePath = "Assets/godlotto/KTH/메인패널2.png";
    const string LibrarySpritePath = "Assets/mokotan/cutSceneSp/library_Front 1.png";

    public static SettingsShellController Ensure(Transform panelRoot)
    {
        if (panelRoot == null)
            return null;

        Transform existing = panelRoot.Find(SettingsShellController.RootName);
        if (existing != null)
            return existing.GetComponent<SettingsShellController>();

        return Build(panelRoot);
    }

    public static Transform TryGetGeneralPage(Transform panelRoot)
    {
        SettingsShellController shell = FindShell(panelRoot);
        return shell != null && shell.GeneralPage != null ? shell.GeneralPage.transform : null;
    }

    public static Transform TryGetCheshireAiPage(Transform panelRoot)
    {
        SettingsShellController shell = FindShell(panelRoot);
        return shell != null && shell.CheshireAiPage != null ? shell.CheshireAiPage.transform : null;
    }

    public static Transform TryGetLanguagePage(Transform panelRoot)
    {
        SettingsShellController shell = FindShell(panelRoot);
        return shell != null && shell.LanguagePage != null ? shell.LanguagePage.transform : null;
    }

    static SettingsShellController FindShell(Transform panelRoot)
    {
        if (panelRoot == null)
            return null;
        Transform existing = panelRoot.Find(SettingsShellController.RootName);
        return existing != null ? existing.GetComponent<SettingsShellController>() : null;
    }

    static SettingsShellController Build(Transform panelRoot)
    {
        TMP_FontAsset uiFont = FindUiFont();
        TMP_FontAsset pixelFont = FindPixelFont();
        bool inGame = ResolveInGameContext(panelRoot);

        ApplyParentCanvasScaler(panelRoot);

        GameObject root = CreatePanel(panelRoot, SettingsShellController.RootName, Color.clear);
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsLastSibling();

        if (!inGame)
        {
            Image library = CreateImage(root.transform, "LibraryBackground", SettingsWoodPanelSpec.OverlayDim);
            Stretch(library.rectTransform);
            library.sprite = LoadSprite(LibrarySpritePath);
            library.preserveAspect = false;
            library.color = Color.white;
        }
        else
        {
            Image dimmer = CreateImage(root.transform, "Dimmer", SettingsWoodPanelSpec.OverlayDim);
            Stretch(dimmer.rectTransform);
        }

        Image frame = CreateImage(root.transform, "WoodFrame", Color.white);
        RectTransform frameRect = frame.rectTransform;
        CenterFixed(frameRect, SettingsWoodPanelSpec.FrameWidth, SettingsWoodPanelSpec.FrameHeight);
        frame.sprite = LoadSprite(FrameSpritePath);
        frame.type = Image.Type.Sliced;
        frame.preserveAspect = false;

        GameObject inner = CreatePanel(frame.transform, "Inner", Color.clear);
        Stretch(inner.GetComponent<RectTransform>(), SettingsWoodPanelSpec.FramePadding);

        Image sidebarImage = CreateImage(inner.transform, "Sidebar", SettingsWoodPanelSpec.SidebarBackground);
        RectTransform sidebar = sidebarImage.rectTransform;
        sidebar.anchorMin = new Vector2(0f, 0f);
        sidebar.anchorMax = new Vector2(0f, 1f);
        sidebar.pivot = new Vector2(0f, 0.5f);
        sidebar.sizeDelta = new Vector2(SettingsWoodPanelSpec.SidebarWidth, 0f);
        sidebar.anchoredPosition = Vector2.zero;

        Image contentImage = CreateImage(inner.transform, "Content", SettingsWoodPanelSpec.ContentBackground);
        RectTransform content = contentImage.rectTransform;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(
            SettingsWoodPanelSpec.SidebarWidth + SettingsWoodPanelSpec.ColumnGap, 0f);
        content.offsetMax = Vector2.zero;

        TMP_Text brand = CreateAnchoredLabel(
            sidebar, "Brand", "CHESHIRE", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -16f), new Vector2(224f, 48f), pixelFont,
            SettingsWoodPanelSpec.BrandPixelFontSize, SettingsWoodPanelSpec.Accent,
            TextAlignmentOptions.BottomLeft);

        CreateAnchoredLabel(
            sidebar, "Edition", "DISPUTATIO / SETTINGS", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -72f), new Vector2(224f, 24f), pixelFont,
            SettingsWoodPanelSpec.CaptionFontSize, SettingsWoodPanelSpec.SecondaryText,
            TextAlignmentOptions.TopLeft);

        Button tabGeneral = CreateTabButton(sidebar, "TabGeneral", uiFont, pixelFont, 0);
        Button tabAi = CreateTabButton(sidebar, "TabCheshireAi", uiFont, pixelFont, 1);
        Button tabLanguage = CreateTabButton(sidebar, "TabLanguage", uiFont, pixelFont, 2);

        TMP_Text aside = CreateLabel(
            sidebar, "Aside", CheshireUiStrings.Lookup("SettingsAside", SettingsDisplayPreferences.ResolveLocale()),
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(22f, 18f), new Vector2(-22f, 90f), uiFont,
            SettingsWoodPanelSpec.CaptionFontSize, SettingsWoodPanelSpec.SecondaryText,
            TextAlignmentOptions.BottomLeft);

        RectTransform header = CreateStretchChild(content, "Header");
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, SettingsWoodPanelSpec.HeaderHeight);
        header.anchoredPosition = Vector2.zero;

        TMP_Text heading = CreateAnchoredLabel(
            header, "Heading", "", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -8f), new Vector2(SettingsWoodPanelSpec.ContentWidth, 48f),
            uiFont, SettingsWoodPanelSpec.TitleFontSize, SettingsWoodPanelSpec.PrimaryText,
            TextAlignmentOptions.BottomLeft);

        TMP_Text subtitle = CreateAnchoredLabel(
            header, "Subtitle", "", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -64f), new Vector2(SettingsWoodPanelSpec.ContentWidth, 28f),
            uiFont, SettingsWoodPanelSpec.CaptionFontSize, SettingsWoodPanelSpec.SecondaryText,
            TextAlignmentOptions.TopLeft);

        RectTransform divider = CreateStretchChild(content, "Divider");
        divider.anchorMin = new Vector2(0f, 1f);
        divider.anchorMax = Vector2.one;
        divider.pivot = new Vector2(0.5f, 1f);
        divider.sizeDelta = new Vector2(0f, SettingsWoodPanelSpec.RuleThickness);
        divider.anchoredPosition = new Vector2(0f, -SettingsWoodPanelSpec.HeaderHeight);
        Image dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = Color.clear;
        dividerImage.raycastTarget = false;
        RectTransform dividerLine = CreateStretchChild(divider, "Line");
        dividerLine.anchorMin = new Vector2(0f, 0.5f);
        dividerLine.anchorMax = new Vector2(1f, 0.5f);
        dividerLine.pivot = new Vector2(0.5f, 0.5f);
        dividerLine.sizeDelta = new Vector2(-SettingsWoodPanelSpec.ContentPaddingX * 2f, SettingsWoodPanelSpec.RuleThickness);
        dividerLine.anchoredPosition = Vector2.zero;
        Image dividerLineImage = dividerLine.gameObject.AddComponent<Image>();
        dividerLineImage.color = SettingsWoodPanelSpec.Border;

        RectTransform footer = CreateStretchChild(content, "Footer");
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, SettingsWoodPanelSpec.FooterHeight);
        footer.anchoredPosition = Vector2.zero;
        Image footerRule = CreateImage(footer, "FooterRule", SettingsWoodPanelSpec.Border);
        footerRule.rectTransform.anchorMin = new Vector2(0f, 1f);
        footerRule.rectTransform.anchorMax = Vector2.one;
        footerRule.rectTransform.sizeDelta = new Vector2(0f, SettingsWoodPanelSpec.RuleThickness);
        footerRule.rectTransform.anchoredPosition = Vector2.zero;

        TMP_Text footerNote = CreateAnchoredLabel(
            footer, "FooterNote", "", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -8f), new Vector2(1040f, 72f), uiFont,
            SettingsWoodPanelSpec.CaptionFontSize, SettingsWoodPanelSpec.SecondaryText,
            TextAlignmentOptions.TopLeft);

        Button returnButton = CreateButton(
            footer, "Return", "", uiFont, SettingsWoodPanelSpec.ButtonFace);
        RectTransform returnRect = (RectTransform)returnButton.transform;
        returnRect.anchorMin = new Vector2(1f, 1f);
        returnRect.anchorMax = new Vector2(1f, 1f);
        returnRect.pivot = new Vector2(1f, 1f);
        returnRect.sizeDelta = new Vector2(
            SettingsWoodPanelSpec.ReturnButtonWidth,
            SettingsWoodPanelSpec.ReturnButtonHeight);
        returnRect.anchoredPosition = new Vector2(0f, -8f);

        RectTransform body = CreateStretchChild(content, "Body");
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(0f, SettingsWoodPanelSpec.FooterHeight + SettingsWoodPanelSpec.SectionGap);
        body.offsetMax = new Vector2(0f, -(SettingsWoodPanelSpec.HeaderHeight + SettingsWoodPanelSpec.SectionGap));

        ScrollRect scroll = body.gameObject.AddComponent<ScrollRect>();
        Image bodyImage = body.gameObject.AddComponent<Image>();
        bodyImage.color = SettingsWoodPanelSpec.ContentBackground;
        bodyImage.raycastTarget = true;
        body.gameObject.AddComponent<RectMask2D>();

        RectTransform pageHost = CreateStretchChild(body, "PageHost");
        pageHost.anchorMin = new Vector2(0f, 1f);
        pageHost.anchorMax = new Vector2(1f, 1f);
        pageHost.pivot = new Vector2(0.5f, 1f);
        pageHost.anchoredPosition = Vector2.zero;
        pageHost.sizeDelta = Vector2.zero;
        var pageHostFitter = pageHost.gameObject.AddComponent<ContentSizeFitter>();
        pageHostFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        pageHostFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var pageHostLayout = pageHost.gameObject.AddComponent<VerticalLayoutGroup>();
        pageHostLayout.childControlHeight = true;
        pageHostLayout.childControlWidth = true;
        pageHostLayout.childForceExpandHeight = false;
        pageHostLayout.childForceExpandWidth = true;
        pageHostLayout.padding = new RectOffset(0, 0, 0, 8);
        pageHostLayout.spacing = 0f;

        scroll.content = pageHost;
        scroll.viewport = body;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject generalPage = CreatePage(pageHost, "GeneralPage");
        GameObject aiPage = CreatePage(pageHost, "CheshireAiPage");
        GameObject languagePage = CreatePage(pageHost, "LanguagePage");

        BuildGeneralRows(generalPage.transform, uiFont, panelRoot);
        LocalAiSettingsPanel.EnsureEmbedded(aiPage.transform);
        SettingsCheshirePreview preview = SettingsCheshirePreview.Ensure(aiPage.transform);
        BuildLanguageRows(languagePage.transform, uiFont);

        var shell = root.AddComponent<SettingsShellController>();
        shell.Sidebar = sidebar;
        shell.Header = header;
        shell.Divider = divider;
        shell.Body = body;
        shell.Footer = footer;
        shell.GeneralPage = generalPage;
        shell.CheshireAiPage = aiPage;
        shell.LanguagePage = languagePage;
        shell.TabGeneral = tabGeneral;
        shell.TabCheshireAi = tabAi;
        shell.TabLanguage = tabLanguage;
        shell.TabGeneralLabel = tabGeneral.transform.Find("Label")?.GetComponent<TMP_Text>();
        shell.TabCheshireAiLabel = tabAi.transform.Find("Label")?.GetComponent<TMP_Text>();
        shell.TabLanguageLabel = tabLanguage.transform.Find("Label")?.GetComponent<TMP_Text>();
        shell.TabGeneralArrow = tabGeneral.transform.Find("Arrow")?.GetComponent<TMP_Text>();
        shell.TabCheshireAiArrow = tabAi.transform.Find("Arrow")?.GetComponent<TMP_Text>();
        shell.TabLanguageArrow = tabLanguage.transform.Find("Arrow")?.GetComponent<TMP_Text>();
        shell.Heading = heading;
        shell.Subtitle = subtitle;
        shell.FooterNote = footerNote;
        shell.Aside = aside;
        shell.ReturnLabel = returnButton.GetComponentInChildren<TMP_Text>(true);
        shell.ReturnButton = returnButton;
        shell.BodyScroll = scroll;
        shell.Preview = preview;
        shell.BindTabs();
        shell.BindReturnContext(inGame);
        shell.SelectTab(SettingsTabId.General);

        HideLegacyLabels(panelRoot);
        _ = brand;
        return shell;
    }

    static bool ResolveInGameContext(Transform panelRoot)
    {
        if (panelRoot.GetComponentInParent<InGameSettingsPanel>() != null)
            return true;
        IntegratedSettingUI integrated = panelRoot.GetComponentInParent<IntegratedSettingUI>();
        if (integrated != null)
            return integrated.uiMode == IntegratedSettingUI.UIMode.PopupPanel;
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName != SceneNames.SettingScene && sceneName != SceneNames.MainMenu;
    }

    static void BuildGeneralRows(Transform page, TMP_FontAsset uiFont, Transform searchRoot)
    {
        string locale = SettingsDisplayPreferences.ResolveLocale();
        AdoptSliderRow(page, searchRoot, uiFont, "BgmRow", "SettingsBgm", new[] { "BGM Slider", "BgmSlider" });
        AdoptSliderRow(page, searchRoot, uiFont, "SfxRow", "SettingsSfx", new[] { "SFX Slider", "SfxSlider" });
        CreateSettingsRow(page, "ResolutionRow", CheshireUiStrings.Lookup("SettingsResolution", locale), uiFont);
        CreateSettingsRow(page, "FullscreenRow", CheshireUiStrings.Lookup("SettingsFullscreen", locale), uiFont);
    }

    static void AdoptSliderRow(
        Transform page,
        Transform searchRoot,
        TMP_FontAsset uiFont,
        string rowName,
        string labelKey,
        string[] sliderNames)
    {
        RectTransform row = CreateSettingsRow(page, rowName, CheshireUiStrings.Lookup(labelKey, SettingsDisplayPreferences.ResolveLocale()), uiFont);
        Transform valueSlot = row.Find("Value");
        Slider slider = FindNamedComponent<Slider>(searchRoot, sliderNames);
        if (valueSlot == null)
            return;
        if (slider == null)
            slider = CreateSlider(valueSlot, sliderNames[0]);
        else
            slider.transform.SetParent(valueSlot, false);

        StyleWoodSlider(slider);
    }

    static void BuildLanguageRows(Transform page, TMP_FontAsset uiFont)
    {
        string locale = SettingsDisplayPreferences.ResolveLocale();
        RectTransform languageRow = CreateSettingsRow(
            page, "LanguageRow", CheshireUiStrings.Lookup("SettingsLanguage", locale), uiFont);
        TMP_Dropdown languageDropdown = CreateSimpleDropdown(languageRow.Find("Value"), "LanguageDropdown", uiFont);
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "한국어", "English", "日本語" });
        languageDropdown.value = LocaleToIndex(locale);
        languageDropdown.onValueChanged.AddListener(index =>
        {
            string next = IndexToLocale(index);
            SettingsDisplayPreferences.ApplyLocale(next);
            SettingsCheshirePreview preview = page.GetComponentInParent<SettingsShellController>()?.Preview;
            preview?.CancelInFlight();
            SettingsShellController shell = page.GetComponentInParent<SettingsShellController>();
            shell?.RefreshLabels();
            BuildLanguageHint(page, uiFont, next);
        });

        RectTransform sizeRow = CreateSettingsRow(
            page, "TextSizeRow", CheshireUiStrings.Lookup("SettingsTextSize", locale), uiFont);
        TMP_Dropdown sizeDropdown = CreateSimpleDropdown(sizeRow.Find("Value"), "TextSizeDropdown", uiFont);
        sizeDropdown.ClearOptions();
        sizeDropdown.AddOptions(new System.Collections.Generic.List<string> { "100%", "120%", "140%" });
        sizeDropdown.value = ScaleToIndex(SettingsDisplayPreferences.GetAnswerTextScale());
        sizeDropdown.onValueChanged.AddListener(index =>
        {
            SettingsDisplayPreferences.SetAnswerTextScale(IndexToScale(index));
            SettingsShellController shell = page.GetComponentInParent<SettingsShellController>();
            shell?.Preview?.ApplyAnswerSize();
        });

        BuildLanguageHint(page, uiFont, locale);
    }

    static void BuildLanguageHint(Transform page, TMP_FontAsset uiFont, string locale)
    {
        Transform existing = page.Find("LanguageHint");
        TMP_Text hint;
        if (existing == null)
        {
            hint = CreateLabel(
                page, "LanguageHint", "", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 48f), uiFont,
                SettingsWoodPanelSpec.CaptionFontSize, SettingsWoodPanelSpec.SecondaryText,
                TextAlignmentOptions.TopLeft);
            var le = hint.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 48f;
            le.preferredHeight = 48f;
        }
        else
        {
            hint = existing.GetComponent<TMP_Text>();
        }

        if (hint != null)
            hint.text = CheshireUiStrings.Lookup("SettingsLanguageHint", locale);
    }

    static RectTransform CreateSettingsRow(Transform page, string name, string label, TMP_FontAsset uiFont)
    {
        GameObject row = CreatePanel(page, name, Color.clear);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = SettingsWoodPanelSpec.ColumnGap;
        layout.padding = new RectOffset(0, 0, 0, 0);
        var element = row.AddComponent<LayoutElement>();
        element.minHeight = SettingsWoodPanelSpec.RowHeight;
        element.preferredHeight = SettingsWoodPanelSpec.RowHeight;
        element.flexibleWidth = 0f;
        element.minWidth = SettingsWoodPanelSpec.ContentWidth;
        element.preferredWidth = SettingsWoodPanelSpec.ContentWidth;

        TMP_Text text = CreateLabel(
            row.transform, "Label", label, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, Vector2.zero, uiFont, SettingsWoodPanelSpec.UiFontSize,
            SettingsWoodPanelSpec.PrimaryText, TextAlignmentOptions.MidlineLeft);
        text.enableWordWrapping = true;
        text.maxVisibleLines = 2;
        text.overflowMode = TextOverflowModes.Overflow;
        var labelElement = text.gameObject.AddComponent<LayoutElement>();
        labelElement.flexibleWidth = 0f;
        labelElement.minWidth = SettingsWoodPanelSpec.LabelColumnWidth;
        labelElement.preferredWidth = SettingsWoodPanelSpec.LabelColumnWidth;
        labelElement.minHeight = SettingsWoodPanelSpec.LabelColumnHeight;
        labelElement.preferredHeight = SettingsWoodPanelSpec.LabelColumnHeight;

        GameObject value = CreatePanel(row.transform, "Value", Color.clear);
        var valueLayout = value.AddComponent<HorizontalLayoutGroup>();
        valueLayout.childAlignment = TextAnchor.MiddleLeft;
        valueLayout.childControlHeight = true;
        valueLayout.childControlWidth = true;
        valueLayout.childForceExpandHeight = false;
        valueLayout.childForceExpandWidth = false;
        valueLayout.spacing = SettingsWoodPanelSpec.CheckboxStatusGap;
        var valueElement = value.AddComponent<LayoutElement>();
        valueElement.flexibleWidth = 0f;
        valueElement.preferredWidth = SettingsWoodPanelSpec.ControlWidth;
        valueElement.minWidth = SettingsWoodPanelSpec.ControlWidth;
        valueElement.minHeight = SettingsWoodPanelSpec.ControlHeight;
        valueElement.preferredHeight = SettingsWoodPanelSpec.ControlHeight;
        Image hairline = CreateImage(row.transform, "Hairline", SettingsWoodPanelSpec.Border);
        var hairlineLayout = hairline.gameObject.AddComponent<LayoutElement>();
        hairlineLayout.ignoreLayout = true;
        RectTransform hairlineRect = hairline.rectTransform;
        hairlineRect.anchorMin = Vector2.zero;
        hairlineRect.anchorMax = new Vector2(1f, 0f);
        hairlineRect.pivot = new Vector2(0.5f, 0f);
        hairlineRect.sizeDelta = new Vector2(0f, 2f);
        hairlineRect.anchoredPosition = Vector2.zero;
        return row.GetComponent<RectTransform>();
    }

    static GameObject CreatePage(Transform host, string name)
    {
        GameObject page = CreatePanel(host, name, Color.clear);
        var element = page.AddComponent<LayoutElement>();
        element.minHeight = 0f;
        element.preferredHeight = -1f;
        element.flexibleHeight = 0f;
        var layout = page.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 8);
        page.SetActive(name == "GeneralPage");
        return page;
    }

    static Button CreateTabButton(Transform sidebar, string name, TMP_FontAsset uiFont, TMP_FontAsset pixelFont, int index)
    {
        Button button = CreateButton(sidebar, name, "", uiFont, SettingsWoodPanelSpec.ButtonFace);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(SettingsWoodPanelSpec.TabWidth, SettingsWoodPanelSpec.TabHeight);
        rect.anchoredPosition = new Vector2(
            0f,
            -(SettingsWoodPanelSpec.TabStartY + index * (SettingsWoodPanelSpec.TabHeight + SettingsWoodPanelSpec.TabGap)));

        float markerX = SettingsWoodPanelSpec.TabInnerPadding + SettingsWoodPanelSpec.TabMarkerSlot * 0.5f;
        Image marker = CreateImage(button.transform, "Marker", SettingsWoodPanelSpec.Accent);
        RectTransform markerRect = marker.rectTransform;
        markerRect.anchorMin = new Vector2(0f, 0.5f);
        markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(SettingsWoodPanelSpec.TabMarkerSize, 14f);
        markerRect.anchoredPosition = new Vector2(markerX, 0f);
        marker.enabled = false;
        marker.raycastTarget = false;

        TMP_Text arrow = CreateLabel(
            button.transform, "Arrow", "", new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(SettingsWoodPanelSpec.TabInnerPadding, 0f),
            new Vector2(SettingsWoodPanelSpec.TabMarkerSlot, 0f),
            uiFont, 16f, SettingsWoodPanelSpec.Accent, TextAlignmentOptions.Midline);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(0f, 0f);
        arrowRect.anchorMax = new Vector2(0f, 1f);
        arrowRect.pivot = new Vector2(0f, 0.5f);
        arrowRect.sizeDelta = new Vector2(SettingsWoodPanelSpec.TabMarkerSlot, 0f);
        arrowRect.anchoredPosition = new Vector2(SettingsWoodPanelSpec.TabInnerPadding, 0f);
        arrow.gameObject.SetActive(false);

        float indexX = SettingsWoodPanelSpec.TabInnerPadding + SettingsWoodPanelSpec.TabMarkerSlot;
        TMP_Text indexLabel = CreateLabel(
            button.transform, "Index", (index + 1).ToString("00"),
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(indexX, 0f),
            new Vector2(SettingsWoodPanelSpec.TabIndexSlot, 0f),
            pixelFont, SettingsWoodPanelSpec.UiFontSize, SettingsWoodPanelSpec.SecondaryText,
            TextAlignmentOptions.MidlineLeft);
        RectTransform indexRect = indexLabel.rectTransform;
        indexRect.anchorMin = new Vector2(0f, 0f);
        indexRect.anchorMax = new Vector2(0f, 1f);
        indexRect.pivot = new Vector2(0f, 0.5f);
        indexRect.sizeDelta = new Vector2(SettingsWoodPanelSpec.TabIndexSlot, 0f);
        indexRect.anchoredPosition = new Vector2(indexX, 0f);

        TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            RectTransform labelRect = label.rectTransform;
            float labelLeft = SettingsWoodPanelSpec.TabInnerPadding
                + SettingsWoodPanelSpec.TabMarkerSlot
                + SettingsWoodPanelSpec.TabIndexSlot
                + SettingsWoodPanelSpec.TabIndexGap;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(labelLeft, 8f);
            labelRect.offsetMax = new Vector2(-SettingsWoodPanelSpec.TabInnerPadding, -8f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = true;
            label.maxVisibleLines = 2;
            label.lineSpacing = -20f;
            label.fontSize = SettingsWoodPanelSpec.UiFontSize;
        }

        return button;
    }

    static TMP_Dropdown CreateSimpleDropdown(Transform parent, string name, TMP_FontAsset uiFont)
    {
        if (parent == null)
            return null;
        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        dropdownObject.name = name;
        dropdownObject.transform.SetParent(parent, false);
        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        StyleWoodDropdown(dropdown, uiFont);
        return dropdown;
    }

    static Slider CreateSlider(Transform parent, string name)
    {
        GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.name = name;
        sliderObject.transform.SetParent(parent, false);
        sliderObject.layer = parent.gameObject.layer;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        var layout = sliderObject.AddComponent<LayoutElement>();
        layout.minHeight = SettingsWoodPanelSpec.ControlHeight;
        layout.preferredHeight = SettingsWoodPanelSpec.ControlHeight;
        layout.minWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.preferredWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.flexibleWidth = 0f;
        StyleWoodSlider(slider);
        return slider;
    }

    public static void StyleWoodSlider(Slider slider)
    {
        if (slider == null)
            return;
        LayoutElement layout = slider.GetComponent<LayoutElement>() ?? slider.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.preferredWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.flexibleWidth = 0f;
        layout.minHeight = SettingsWoodPanelSpec.ControlHeight;
        layout.preferredHeight = SettingsWoodPanelSpec.ControlHeight;

        Image background = slider.transform.Find("Background")?.GetComponent<Image>();
        if (background != null)
        {
            background.color = SettingsWoodPanelSpec.InputBackground;
            SizeTrack(background.rectTransform, SettingsWoodPanelSpec.ControlWidth - SettingsWoodPanelSpec.SliderTrackWidth);
            ApplyBorder(background.gameObject, SettingsWoodPanelSpec.Border, new Vector2(1f, -1f));
        }

        RectTransform fillArea = slider.transform.Find("Fill Area") as RectTransform;
        if (fillArea != null)
            SizeTrack(fillArea, SettingsWoodPanelSpec.ControlWidth - SettingsWoodPanelSpec.SliderTrackWidth);

        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
            fill.color = SettingsWoodPanelSpec.Accent;

        Transform handleTransform = FindNamedChild(slider.transform, "Handle");
        StyleSliderHandle(handleTransform as RectTransform);
        StyleSliderHandle(slider.handleRect);
        if (slider.handleRect == null)
        {
            Transform found = FindNamedChild(slider.transform, "Handle");
            if (found != null)
                slider.handleRect = found as RectTransform;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(slider.GetComponent<RectTransform>());
        StyleSliderHandle(FindNamedChild(slider.transform, "Handle") as RectTransform);
        StyleSliderHandle(slider.handleRect);
    }

    static void StyleSliderHandle(RectTransform handle)
    {
        if (handle == null)
            return;
        handle.anchorMin = new Vector2(0.5f, 0.5f);
        handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(
            SettingsWoodPanelSpec.SliderHandleWidth,
            SettingsWoodPanelSpec.SliderHandleHeight);
        handle.localScale = Vector3.one;
        Image handleImage = handle.GetComponent<Image>();
        if (handleImage != null)
        {
            handleImage.color = SettingsWoodPanelSpec.Accent;
            handleImage.type = Image.Type.Simple;
            handleImage.preserveAspect = true;
        }
    }

    static void SizeTrack(RectTransform rect, float horizontalInset)
    {
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-horizontalInset, SettingsWoodPanelSpec.SliderTrackHeight);
        rect.anchoredPosition = Vector2.zero;
    }

    static void ApplyBorder(GameObject target, Color color, Vector2 distance)
    {
        if (target == null)
            return;
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    public static void StyleWoodDropdown(TMP_Dropdown dropdown, TMP_FontAsset uiFont = null)
    {
        if (dropdown == null)
            return;
        LayoutElement layout = dropdown.GetComponent<LayoutElement>() ?? dropdown.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.preferredWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.flexibleWidth = 0f;
        layout.minHeight = SettingsWoodPanelSpec.ControlHeight;
        layout.preferredHeight = SettingsWoodPanelSpec.ControlHeight;

        Image image = dropdown.GetComponent<Image>();
        if (image != null)
            image.color = SettingsWoodPanelSpec.InputBackground;
        ApplyBorder(dropdown.gameObject, SettingsWoodPanelSpec.Border, new Vector2(1f, -1f));

        Transform arrow = dropdown.transform.Find("Arrow");
        if (arrow != null)
        {
            RectTransform arrowRect = arrow as RectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(16f, 10f);
            arrowRect.anchoredPosition = new Vector2(-20f, 0f);
            Image arrowImage = arrow.GetComponent<Image>();
            if (arrowImage != null)
                arrowImage.color = SettingsWoodPanelSpec.Accent;
        }

        Transform template = dropdown.template != null ? dropdown.template : dropdown.transform.Find("Template");
        if (template != null)
        {
            dropdown.template = template as RectTransform;
            RectTransform templateRect = template as RectTransform;
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0f, SettingsWoodPanelSpec.DropdownListMaxHeight);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.localScale = Vector3.one;
            Image templateImage = template.GetComponent<Image>();
            if (templateImage != null)
                templateImage.color = SettingsWoodPanelSpec.InputBackground;
            ApplyBorder(template.gameObject, SettingsWoodPanelSpec.Border, new Vector2(1f, -1f));

            Canvas overlay = template.GetComponent<Canvas>();
            if (overlay == null)
                overlay = template.gameObject.AddComponent<Canvas>();
            overlay.enabled = true;
            overlay.overrideSorting = true;
            overlay.sortingOrder = 32000;

            Transform viewport = template.Find("Viewport");
            if (viewport != null)
            {
                viewport.localScale = Vector3.one;
                Stretch(viewport as RectTransform);
            }

            Transform content = template.Find("Viewport/Content");
            if (content != null)
            {
                content.localScale = Vector3.one;
                VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>()
                    ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                group.childAlignment = TextAnchor.UpperCenter;
                group.childControlWidth = true;
                group.childControlHeight = true;
                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;
                group.spacing = 0f;
                group.padding = new RectOffset(
                    0,
                    0,
                    (int)SettingsWoodPanelSpec.DropdownListPaddingY,
                    (int)SettingsWoodPanelSpec.DropdownListPaddingY);
            }

            Transform item = template.Find("Viewport/Content/Item");
            if (item == null && content != null && content.childCount > 0)
                item = content.GetChild(0);

            if (item != null)
            {
                LayoutElement itemLayout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
                itemLayout.minHeight = SettingsWoodPanelSpec.DropdownItemHeight;
                itemLayout.preferredHeight = SettingsWoodPanelSpec.DropdownItemHeight;
                Image itemBackground = item.Find("Item Background")?.GetComponent<Image>()
                    ?? item.Find("Background")?.GetComponent<Image>()
                    ?? item.GetComponent<Image>();
                if (itemBackground != null)
                    itemBackground.color = SettingsWoodPanelSpec.InputBackground;
                StyleDropdownItem(item, dropdown.itemText);
            }
        }

        StyleDropdownText(dropdown.captionText, uiFont, true);
        StyleDropdownText(dropdown.itemText, uiFont, false);
        if (dropdown.itemText != null)
            StyleDropdownItem(dropdown.itemText.transform.parent, dropdown.itemText);

        ColorBlock colors = dropdown.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = SettingsWoodPanelSpec.SelectedBorder;
        colors.selectedColor = SettingsWoodPanelSpec.SelectedBackground;
        colors.pressedColor = SettingsWoodPanelSpec.ButtonFace;
        dropdown.colors = colors;
    }

    public static void StyleWoodToggle(Toggle toggle)
    {
        if (toggle == null)
            return;
        RectTransform rect = toggle.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(SettingsWoodPanelSpec.ControlWidth, SettingsWoodPanelSpec.ControlHeight);
        LayoutElement layout = toggle.GetComponent<LayoutElement>() ?? toggle.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.preferredWidth = SettingsWoodPanelSpec.ControlWidth;
        layout.minHeight = SettingsWoodPanelSpec.ControlHeight;
        layout.preferredHeight = SettingsWoodPanelSpec.ControlHeight;
        layout.flexibleWidth = 0f;

        Image background = toggle.transform.Find("Background")?.GetComponent<Image>();
        if (background != null)
        {
            if (background.sprite == null)
                background.sprite = LoadBuiltinUiSprite("UISprite.psd");
            background.color = SettingsWoodPanelSpec.InputBackground;
            background.raycastTarget = false;
            RectTransform bgRect = background.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.sizeDelta = new Vector2(SettingsWoodPanelSpec.CheckboxSize, SettingsWoodPanelSpec.CheckboxSize);
            bgRect.anchoredPosition = Vector2.zero;
        }

        Transform check = toggle.transform.Find("Background/Checkmark");
        if (check == null)
            check = toggle.transform.Find("Checkmark");
        Image checkmark = check != null ? check.GetComponent<Image>() : null;
        if (checkmark != null)
        {
            if (checkmark.sprite == null)
                checkmark.sprite = LoadBuiltinUiSprite("Checkmark.psd");
            checkmark.color = SettingsWoodPanelSpec.Accent;
            checkmark.raycastTarget = false;
            RectTransform checkRect = checkmark.rectTransform;
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(
                SettingsWoodPanelSpec.CheckboxMarkSize,
                SettingsWoodPanelSpec.CheckboxMarkSize);
            checkRect.anchoredPosition = Vector2.zero;
        }

        Transform labelTransform = toggle.transform.Find("Label");
        if (labelTransform != null)
            labelTransform.gameObject.SetActive(false);

        EnsureToggleHitArea(toggle, background);
        EnsureFullscreenStatus(toggle);

        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = Color.white;
        toggle.colors = colors;
        if (background != null)
            ApplyBorder(background.gameObject, SettingsWoodPanelSpec.Border, new Vector2(2f, -2f));
    }

    static void HideLegacyLabels(Transform panelRoot)
    {
        string[] names =
        {
            "BGM Text", "SFX Text", "Resolution Text", "Resolution Text ", "Fullscreen Text"
        };
        for (int i = 0; i < names.Length; i++)
        {
            Transform label = FindNamedTransform(panelRoot, names[i]);
            if (label != null && label.GetComponentInParent<SettingsShellController>() == null)
                label.gameObject.SetActive(false);
        }
    }

    static int LocaleToIndex(string locale)
    {
        if (locale == CheshireLocaleResolver.English)
            return 1;
        if (locale == CheshireLocaleResolver.Japanese)
            return 2;
        return 0;
    }

    static string IndexToLocale(int index)
    {
        if (index == 1)
            return CheshireLocaleResolver.English;
        if (index == 2)
            return CheshireLocaleResolver.Japanese;
        return CheshireLocaleResolver.Korean;
    }

    static int ScaleToIndex(int scale)
    {
        if (scale == 120)
            return 1;
        if (scale == 140)
            return 2;
        return 0;
    }

    static int IndexToScale(int index)
    {
        if (index == 1)
            return 120;
        if (index == 2)
            return 140;
        return 100;
    }

    public static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = name == SettingsShellController.RootName || name == "Dimmer";
        return go;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = CreatePanel(parent, name, color);
        return go.GetComponent<Image>();
    }

    public static Button CreateButton(Transform parent, string name, string text, TMP_FontAsset font, Color face)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        Image image = go.GetComponent<Image>();
        image.color = face;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = SettingsWoodPanelSpec.SelectedBorder;
        colors.selectedColor = SettingsWoodPanelSpec.SelectedBackground;
        button.colors = colors;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = SettingsWoodPanelSpec.Border;
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text label = CreateLabel(
            go.transform, "Label", text, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, font, SettingsWoodPanelSpec.UiFontSize,
            SettingsWoodPanelSpec.PrimaryText, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    public static TMP_Text CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text ?? "";
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        if (font != null)
        {
            label.font = font;
            label.fontSharedMaterial = font.material;
        }

        return label;
    }

    public static void Stretch(RectTransform rect, float padding = 0f, float verticalPad = -1f)
    {
        if (rect == null)
            return;
        float v = verticalPad >= 0f ? verticalPad : padding;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, v);
        rect.offsetMax = new Vector2(-padding, -v);
    }

    static void CenterFixed(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
    }

    static void ApplyParentCanvasScaler(Transform panelRoot)
    {
        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = panelRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (panelRoot.GetComponent<GraphicRaycaster>() == null && canvas.gameObject == panelRoot.gameObject)
            panelRoot.gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        SettingsWoodPanelSpec.ApplyReferenceScaler(scaler);
    }

    public static TMP_Text CreateAnchoredLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        TMP_Text label = CreateLabel(
            parent, name, text, anchorMin, anchorMax, Vector2.zero, Vector2.zero,
            font, fontSize, color, alignment);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    static void StyleDropdownText(TMP_Text text, TMP_FontAsset uiFont, bool caption)
    {
        if (text == null)
            return;
        if (uiFont != null)
            text.font = uiFont;
        text.color = SettingsWoodPanelSpec.PrimaryText;
        text.fontSize = SettingsWoodPanelSpec.UiFontSize;
        text.enableAutoSizing = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.isRightToLeftText = false;
        text.margin = caption
            ? new Vector4(
                SettingsWoodPanelSpec.DropdownCaptionPaddingLeft,
                0f,
                SettingsWoodPanelSpec.DropdownCaptionPaddingRight,
                0f)
            : Vector4.zero;
    }

    static void StyleDropdownItem(Transform item, TMP_Text itemText)
    {
        if (item == null)
            return;

        item.localScale = Vector3.one;
        RectTransform itemRect = item as RectTransform;
        if (itemRect != null)
        {
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(0f, SettingsWoodPanelSpec.DropdownItemHeight);
            itemRect.localScale = Vector3.one;
        }

        LayoutElement itemLayout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
        itemLayout.minHeight = SettingsWoodPanelSpec.DropdownItemHeight;
        itemLayout.preferredHeight = SettingsWoodPanelSpec.DropdownItemHeight;
        itemLayout.minWidth = SettingsWoodPanelSpec.ControlWidth;
        itemLayout.flexibleWidth = 1f;

        Transform check = item.Find("Item Checkmark") ?? item.Find("Checkmark");
        if (check != null)
        {
            RectTransform checkRect = check as RectTransform;
            checkRect.localScale = Vector3.one;
            checkRect.anchorMin = new Vector2(1f, 0.5f);
            checkRect.anchorMax = new Vector2(1f, 0.5f);
            checkRect.pivot = new Vector2(1f, 0.5f);
            checkRect.sizeDelta = new Vector2(16f, 16f);
            checkRect.anchoredPosition = new Vector2(-12f, 0f);
            Image checkImage = check.GetComponent<Image>();
            if (checkImage != null)
            {
                checkImage.color = SettingsWoodPanelSpec.Accent;
                checkImage.raycastTarget = false;
                if (checkImage.sprite == null)
                    checkImage.enabled = false;
            }
        }

        TMP_Text label = itemText != null ? itemText : item.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        RectTransform labelRect = label.rectTransform;
        labelRect.localScale = Vector3.one;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(SettingsWoodPanelSpec.DropdownCaptionPaddingLeft, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
        label.isRightToLeftText = false;
        label.margin = Vector4.zero;
    }

    static readonly System.Collections.Generic.HashSet<int> BoundFullscreenToggles =
        new System.Collections.Generic.HashSet<int>();

    static Image EnsureToggleHitArea(Toggle toggle, Image checkboxGraphic)
    {
        Transform existing = toggle.transform.Find("HitArea");
        Image hit;
        if (existing == null)
        {
            var go = new GameObject("HitArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(toggle.transform, false);
            go.layer = toggle.gameObject.layer;
            go.transform.SetAsFirstSibling();
            hit = go.GetComponent<Image>();
        }
        else
        {
            hit = existing.GetComponent<Image>();
        }

        Stretch(hit.rectTransform);
        hit.color = Color.clear;
        hit.raycastTarget = true;
        if (hit.sprite == null)
        {
            hit.sprite = checkboxGraphic != null && checkboxGraphic.sprite != null
                ? checkboxGraphic.sprite
                : LoadBuiltinUiSprite("UISprite.psd");
        }

        toggle.targetGraphic = hit;
        return hit;
    }

    static Sprite LoadBuiltinUiSprite(string fileName)
    {
        if (fileName == "Checkmark.psd")
            return FallbackSprite(ref _checkmarkSprite);
        return FallbackSprite(ref _uiSprite);
    }

    static Sprite _uiSprite;
    static Sprite _checkmarkSprite;

    static Sprite FallbackSprite(ref Sprite cached)
    {
        if (cached != null)
            return cached;
        Texture2D texture = Texture2D.whiteTexture;
        cached = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return cached;
    }

    static void EnsureFullscreenStatus(Toggle toggle)
    {
        Transform strayParent = toggle.transform.parent;
        if (strayParent != null)
        {
            Transform stray = strayParent.Find("Status");
            if (stray != null && stray.parent != toggle.transform)
                stray.SetParent(toggle.transform, false);
        }

        Transform existing = toggle.transform.Find("Status");
        TMP_Text status;
        if (existing == null)
        {
            status = CreateLabel(
                toggle.transform, "Status", "", new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(
                    SettingsWoodPanelSpec.CheckboxSize + SettingsWoodPanelSpec.CheckboxStatusGap,
                    0f),
                Vector2.zero, FindUiFont(),
                SettingsWoodPanelSpec.UiFontSize, SettingsWoodPanelSpec.PrimaryText,
                TextAlignmentOptions.MidlineLeft);
        }
        else
        {
            status = existing.GetComponent<TMP_Text>();
            if (status != null)
            {
                RectTransform statusRect = status.rectTransform;
                statusRect.anchorMin = Vector2.zero;
                statusRect.anchorMax = Vector2.one;
                statusRect.offsetMin = new Vector2(
                    SettingsWoodPanelSpec.CheckboxSize + SettingsWoodPanelSpec.CheckboxStatusGap,
                    0f);
                statusRect.offsetMax = Vector2.zero;
            }
        }

        if (status == null)
            return;

        status.raycastTarget = false;
        LayoutElement element = status.GetComponent<LayoutElement>();
        if (element != null)
            Object.DestroyImmediate(element);

        if (BoundFullscreenToggles.Add(toggle.GetInstanceID()))
        {
            toggle.onValueChanged.AddListener(isOn =>
            {
                Transform nested = toggle.transform.Find("Status");
                TMP_Text label = nested != null ? nested.GetComponent<TMP_Text>() : null;
                if (label != null)
                    ApplyFullscreenStatusText(label, isOn);
            });
        }

        ApplyFullscreenStatusText(status, toggle.isOn);
        status.gameObject.SetActive(true);
    }

    static void OnFullscreenStatusChanged(bool isOn)
    {
    }

    static void ApplyFullscreenStatusText(TMP_Text status, bool isOn)
    {
        string locale = SettingsDisplayPreferences.ResolveLocale();
        string key = isOn ? "SettingsFullscreenOn" : "SettingsFullscreenOff";
        string text = CheshireUiStrings.Lookup(key, locale);
        if (string.IsNullOrEmpty(text))
            text = isOn ? "On" : "Off";
        status.text = text;
    }

    static RectTransform CreateStretchChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go.GetComponent<RectTransform>();
    }

    public static TMP_FontAsset FindUiFont()
    {
        return FindFont(UiFontNameHint) ?? FindFont("NanumGothic");
    }

    public static TMP_FontAsset FindPixelFont()
    {
        return FindFont(PixelFontNameHint);
    }

    static TMP_FontAsset FindFont(string nameHint)
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name.IndexOf(nameHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return fonts[i];
        }

        return null;
    }

    public static Sprite LoadSprite(string assetPath)
    {
#if UNITY_EDITOR
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }
#endif
        return null;
    }

    static T FindNamedComponent<T>(Transform root, string[] names) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].GetComponentInParent<SettingsShellController>() != null
                && components[i].transform != root)
            {
                // allow already-adopted controls
            }

            for (int j = 0; j < names.Length; j++)
            {
                if (components[i].name == names[j])
                    return components[i];
            }
        }

        return null;
    }

    static Transform FindNamedChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != root && all[i].name == name)
                return all[i];
        }

        return null;
    }

    static Transform FindNamedTransform(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
                return all[i];
        }

        return null;
    }
}
