using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsShellFactoryTests
{
    GameObject _panelRoot;
    string _previousLanguage;

    [SetUp]
    public void SetUp()
    {
        _previousLanguage = Fungus.SetLanguage.mostRecentLanguage;
        _panelRoot = new GameObject("SettingPanel", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        Fungus.SetLanguage.mostRecentLanguage = _previousLanguage;
        if (_panelRoot != null)
            Object.DestroyImmediate(_panelRoot);
    }

    [Test]
    public void Ensure_BuildsThreeTabsAndFixedChrome()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);

        Assert.That(shell, Is.Not.Null);
        Assert.That(shell.Sidebar, Is.Not.Null);
        Assert.That(shell.Header, Is.Not.Null);
        Assert.That(shell.Footer, Is.Not.Null);
        Assert.That(shell.GeneralPage, Is.Not.Null);
        Assert.That(shell.CheshireAiPage, Is.Not.Null);
        Assert.That(shell.LanguagePage, Is.Not.Null);
        Assert.That(_panelRoot.transform.Find(SettingsShellController.RootName), Is.Not.Null);
    }

    [Test]
    public void Ensure_IsIdempotent()
    {
        SettingsShellController first = SettingsShellFactory.Ensure(_panelRoot.transform);
        int childCount = _panelRoot.transform.childCount;

        SettingsShellController second = SettingsShellFactory.Ensure(_panelRoot.transform);

        Assert.That(second, Is.SameAs(first));
        Assert.That(_panelRoot.transform.childCount, Is.EqualTo(childCount));
    }

    [Test]
    public void SelectTab_DoesNotMoveChrome()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        SettingsChromeSnapshot before = shell.CaptureChrome();

        shell.SelectTab(SettingsTabId.CheshireAi);
        SettingsChromeSnapshot afterAi = shell.CaptureChrome();
        shell.SelectTab(SettingsTabId.Language);
        SettingsChromeSnapshot afterLanguage = shell.CaptureChrome();
        shell.SelectTab(SettingsTabId.General);
        SettingsChromeSnapshot afterGeneral = shell.CaptureChrome();

        Assert.That(afterAi, Is.EqualTo(before));
        Assert.That(afterLanguage, Is.EqualTo(before));
        Assert.That(afterGeneral, Is.EqualTo(before));
        Assert.That(shell.GeneralPage.activeSelf, Is.True);
        Assert.That(shell.CheshireAiPage.activeSelf, Is.False);
        Assert.That(shell.LanguagePage.activeSelf, Is.False);
    }

    [Test]
    public void SelectTab_KeepsSelectedTabTextFromShifting()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform generalLabel = shell.TabGeneral.transform.Find("Label") as RectTransform;
        Assert.That(generalLabel, Is.Not.Null);
        Vector2 unselected = generalLabel.anchoredPosition;

        shell.SelectTab(SettingsTabId.General);
        Vector2 selected = generalLabel.anchoredPosition;

        Assert.AreEqual(unselected, selected);
    }

    [Test]
    public void DisplayControls_LandOnGeneralPageInsideShell()
    {
        TMPro.TMP_Dropdown dropdown = null;
        Toggle toggle = null;

        SettingDisplayControlsFactory.EnsureDisplayControls(_panelRoot.transform, ref dropdown, ref toggle);

        Assert.That(dropdown, Is.Not.Null);
        Assert.That(toggle, Is.Not.Null);
        Assert.That(dropdown.transform.IsChildOf(SettingsShellFactory.TryGetGeneralPage(_panelRoot.transform)), Is.True);
        Assert.That(
            _panelRoot.GetComponentInChildren<LocalAiSettingsPanel>(true).transform.IsChildOf(
                SettingsShellFactory.TryGetCheshireAiPage(_panelRoot.transform)),
            Is.True);
        SettingsCheshirePreview preview = _panelRoot.GetComponentInChildren<SettingsCheshirePreview>(true);
        Assert.That(preview, Is.Not.Null);
        Assert.That(preview.AskButton, Is.Not.Null);
        Assert.That(preview.AskButton.gameObject.activeSelf, Is.True);
        Assert.That(preview.PromptInput, Is.Not.Null);
        Assert.That(preview.FirstMetric == null || !preview.FirstMetric.gameObject.activeSelf, Is.True);
        Assert.That(preview.CompleteMetric == null || !preview.CompleteMetric.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void Body_UsesRectMaskInsteadOfZeroAlphaMask()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);

        Assert.That(shell.Body.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(shell.Body.GetComponent<Mask>(), Is.Null);
        Assert.That(shell.Body.GetComponent<Image>().color.a, Is.GreaterThan(0.5f));
    }

    [Test]
    public void WoodFrame_UsesUntintedSpriteAndInnerInset()
    {
        SettingsShellFactory.Ensure(_panelRoot.transform);
        Transform root = _panelRoot.transform.Find(SettingsShellController.RootName);
        Image frame = root.Find("WoodFrame").GetComponent<Image>();
        RectTransform inner = root.Find("WoodFrame").Find("Inner") as RectTransform;

        Assert.That(frame.color, Is.EqualTo(Color.white));
        Assert.That(inner.offsetMin.x, Is.EqualTo(SettingsWoodPanelSpec.FramePadding));
        Assert.That(inner.offsetMax.x, Is.EqualTo(-SettingsWoodPanelSpec.FramePadding));
        Assert.That(frame.sprite, Is.Not.Null);
        Assert.That(frame.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(frame.rectTransform.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(new Vector2(
            SettingsWoodPanelSpec.FrameWidth,
            SettingsWoodPanelSpec.FrameHeight)));
    }

    [Test]
    public void GeneralPage_HasLabeledRowsWithControls()
    {
        TMP_Dropdown dropdown = null;
        Toggle toggle = null;
        SettingDisplayControlsFactory.EnsureDisplayControls(_panelRoot.transform, ref dropdown, ref toggle);

        Transform general = SettingsShellFactory.TryGetGeneralPage(_panelRoot.transform);
        Assert.That(general.Find("BgmRow").GetComponentInChildren<Slider>(true), Is.Not.Null);
        Assert.That(general.Find("SfxRow").GetComponentInChildren<Slider>(true), Is.Not.Null);
        Assert.That(general.Find("ResolutionRow").GetComponentInChildren<TMP_Dropdown>(true), Is.Not.Null);
        Assert.That(general.Find("FullscreenRow").GetComponentInChildren<Toggle>(true), Is.Not.Null);
        Assert.That(general.Find("BgmRow").Find("Label").GetComponent<TMP_Text>().text, Is.Not.Empty);
        Assert.That(general.Find("SfxRow").Find("Label").GetComponent<TMP_Text>().text, Is.Not.Empty);
    }

    [Test]
    public void Tabs_ShowNumericIndexWithoutPixelArrowGlyph()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);

        Assert.That(shell.TabGeneral.transform.Find("Index").GetComponent<TMP_Text>().text, Is.EqualTo("01"));
        Assert.That(shell.TabCheshireAi.transform.Find("Index").GetComponent<TMP_Text>().text, Is.EqualTo("02"));
        Assert.That(shell.TabLanguage.transform.Find("Index").GetComponent<TMP_Text>().text, Is.EqualTo("03"));
        TMP_FontAsset arrowFont = shell.TabGeneralArrow.font;
        if (arrowFont != null)
        {
            Assert.That(
                arrowFont.name.IndexOf("Fantasy", System.StringComparison.OrdinalIgnoreCase),
                Is.LessThan(0));
        }
    }

    [Test]
    public void PageHost_PinsContentToTopOfBody()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform pageHost = shell.Body.Find("PageHost") as RectTransform;

        Assert.That(pageHost, Is.Not.Null);
        Assert.That(pageHost.pivot.y, Is.EqualTo(1f));
        Assert.That(pageHost.anchorMin.y, Is.EqualTo(1f));
        Assert.That(pageHost.anchorMax.y, Is.EqualTo(1f));
    }

    [Test]
    public void GeneralControls_MatchSpecSizeAndInputColors()
    {
        TMP_Dropdown dropdown = null;
        Toggle toggle = null;
        SettingDisplayControlsFactory.EnsureDisplayControls(_panelRoot.transform, ref dropdown, ref toggle);

        Transform general = SettingsShellFactory.TryGetGeneralPage(_panelRoot.transform);
        Slider bgm = general.Find("BgmRow").GetComponentInChildren<Slider>(true);
        SettingsShellFactory.StyleWoodSlider(bgm);
        LayoutElement sliderLayout = bgm.GetComponent<LayoutElement>();
        Assert.That(sliderLayout.preferredWidth, Is.EqualTo(SettingsWoodPanelSpec.ControlWidth));
        Assert.That(sliderLayout.preferredHeight, Is.EqualTo(SettingsWoodPanelSpec.ControlHeight));
        Assert.That(sliderLayout.flexibleWidth, Is.EqualTo(0f));
        Image sliderTrack = bgm.transform.Find("Background")?.GetComponent<Image>();
        Assert.That(sliderTrack, Is.Not.Null);
        Assert.That(sliderTrack.rectTransform.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.SliderTrackHeight));
        RectTransform handle = FindHandle(bgm);
        Assert.That(handle, Is.Not.Null);
        Assert.That(handle.anchorMin, Is.EqualTo(handle.anchorMax), "handle must not stretch");
        Assert.That(handle.sizeDelta.x, Is.EqualTo(SettingsWoodPanelSpec.SliderHandleWidth).Within(0.1f));
        Assert.That(handle.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.SliderHandleHeight).Within(0.1f));

        Image dropdownFace = dropdown.GetComponent<Image>();
        Assert.That(dropdownFace.color, Is.EqualTo(SettingsWoodPanelSpec.InputBackground));
        Assert.That(dropdown.captionText.color, Is.EqualTo(SettingsWoodPanelSpec.PrimaryText));
        Assert.That(dropdown.captionText.enableAutoSizing, Is.False);
        Assert.That(dropdown.captionText.enableWordWrapping, Is.False);
        Assert.That(dropdown.captionText.fontSize, Is.EqualTo(SettingsWoodPanelSpec.UiFontSize));

        Image toggleBg = toggle.transform.Find("Background")?.GetComponent<Image>();
        Assert.That(toggleBg, Is.Not.Null);
        Assert.That(toggleBg.color, Is.EqualTo(SettingsWoodPanelSpec.InputBackground));
        Transform value = general.Find("FullscreenRow").Find("Value");
        LayoutElement valueLayout = value.GetComponent<LayoutElement>();
        Assert.That(valueLayout.preferredWidth, Is.EqualTo(SettingsWoodPanelSpec.ControlWidth));
        Assert.That(valueLayout.preferredHeight, Is.EqualTo(SettingsWoodPanelSpec.ControlHeight));
        TMP_Text status = toggle.transform.Find("Status")?.GetComponent<TMP_Text>();
        Assert.That(status, Is.Not.Null);
        Assert.That(status.gameObject.activeSelf, Is.True);
        Assert.That(status.text, Is.Not.Empty);
    }

    [Test]
    public void FullscreenToggle_FillsValueHitAreaWithVisibleCheckbox()
    {
        TMP_Dropdown dropdown = null;
        Toggle toggle = null;
        SettingDisplayControlsFactory.EnsureDisplayControls(_panelRoot.transform, ref dropdown, ref toggle);

        LayoutElement layout = toggle.GetComponent<LayoutElement>();
        Assert.That(layout.preferredWidth, Is.EqualTo(SettingsWoodPanelSpec.ControlWidth));
        Assert.That(layout.preferredHeight, Is.EqualTo(SettingsWoodPanelSpec.ControlHeight));
        Assert.That(layout.minWidth, Is.EqualTo(SettingsWoodPanelSpec.ControlWidth));
        Assert.That(layout.minHeight, Is.EqualTo(SettingsWoodPanelSpec.ControlHeight));

        Image hitArea = toggle.targetGraphic as Image;
        Assert.That(hitArea, Is.Not.Null);
        Assert.That(hitArea.raycastTarget, Is.True);
        RectTransform hitRect = hitArea.rectTransform;
        Assert.That(hitRect.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(hitRect.anchorMax, Is.EqualTo(Vector2.one));

        Image box = toggle.transform.Find("Background")?.GetComponent<Image>();
        Assert.That(box, Is.Not.Null);
        Assert.That(box.sprite, Is.Not.Null);
        Assert.That(box.rectTransform.sizeDelta.x, Is.EqualTo(SettingsWoodPanelSpec.CheckboxSize));
        Assert.That(box.rectTransform.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.CheckboxSize));
        Assert.That(box.rectTransform.anchorMin, Is.EqualTo(box.rectTransform.anchorMax), "checkbox graphic must not stretch");
        Outline outline = box.GetComponent<Outline>();
        Assert.That(outline, Is.Not.Null);
        Assert.That(Mathf.Abs(outline.effectDistance.x), Is.GreaterThanOrEqualTo(2f));
        Assert.That(outline.effectColor, Is.EqualTo(SettingsWoodPanelSpec.Border));

        TMP_Text status = toggle.transform.Find("Status")?.GetComponent<TMP_Text>();
        Assert.That(status, Is.Not.Null);
        Assert.That(status.raycastTarget, Is.False);
        Assert.That(toggle.transform.parent.Find("Status"), Is.Null);
    }

    [Test]
    public void Divider_IsThinRuleNotFilledBand()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        Image band = shell.Divider.GetComponent<Image>();
        RectTransform line = shell.Divider.Find("Line") as RectTransform;

        Assert.That(band.color.a, Is.EqualTo(0f));
        Assert.That(line.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.RuleThickness));
    }

    [Test]
    public void SelectedTab_UsesColorMarkerInsteadOfMissingGlyph()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        shell.SelectTab(SettingsTabId.General);

        Assert.That(shell.TabGeneralArrow.text, Is.Empty);
        Image marker = shell.TabGeneral.transform.Find("Marker")?.GetComponent<Image>();
        Assert.That(marker, Is.Not.Null);
        Assert.That(marker.enabled, Is.True);
        Assert.That(marker.color, Is.EqualTo(SettingsWoodPanelSpec.Accent));
        Image unselected = shell.TabCheshireAi.transform.Find("Marker")?.GetComponent<Image>();
        Assert.That(unselected.enabled, Is.False);
    }

    [Test]
    public void WoodFrame_UsesSlicedSpriteSoOrnamentsStayInCorners()
    {
        SettingsShellFactory.Ensure(_panelRoot.transform);
        Image frame = _panelRoot.transform.Find(SettingsShellController.RootName).Find("WoodFrame").GetComponent<Image>();
        Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(frame.sprite, Is.Not.Null);
        Assert.That(frame.color, Is.EqualTo(Color.white));
    }

    [Test]
    public void SettingsRow_UsesSpecVerticalPadding()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        HorizontalLayoutGroup row = shell.GeneralPage.transform.Find("BgmRow").GetComponent<HorizontalLayoutGroup>();
        LayoutElement rowElement = shell.GeneralPage.transform.Find("BgmRow").GetComponent<LayoutElement>();
        LayoutElement label = shell.GeneralPage.transform.Find("BgmRow").Find("Label").GetComponent<LayoutElement>();
        LayoutElement value = shell.GeneralPage.transform.Find("BgmRow").Find("Value").GetComponent<LayoutElement>();
        Assert.That(rowElement.minHeight, Is.EqualTo(SettingsWoodPanelSpec.RowHeight));
        Assert.That(row.spacing, Is.EqualTo(SettingsWoodPanelSpec.ColumnGap));
        Assert.That(label.preferredWidth, Is.EqualTo(SettingsWoodPanelSpec.LabelColumnWidth));
        Assert.That(label.flexibleWidth, Is.EqualTo(0f));
        Assert.That(value.preferredWidth, Is.EqualTo(SettingsWoodPanelSpec.ControlWidth));
        Assert.That(value.flexibleWidth, Is.EqualTo(0f));
    }

    [Test]
    public void WoodFrame_DoesNotStretchOnUltrawideParent()
    {
        RectTransform panel = _panelRoot.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(3440f, 1440f);
        SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform frame = _panelRoot.transform
            .Find(SettingsShellController.RootName)
            .Find("WoodFrame") as RectTransform;

        Assert.That(frame.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(frame.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(frame.sizeDelta.x, Is.EqualTo(SettingsWoodPanelSpec.FrameWidth));
        Assert.That(frame.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.FrameHeight));
    }

    [Test]
    public void BrandAndEdition_StayInsideSidebarBounds()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform brand = shell.Sidebar.Find("Brand") as RectTransform;
        RectTransform edition = shell.Sidebar.Find("Edition") as RectTransform;

        Assert.That(brand.sizeDelta, Is.EqualTo(new Vector2(224f, 48f)));
        Assert.That(edition.sizeDelta, Is.EqualTo(new Vector2(224f, 24f)));
        Assert.That(brand.anchoredPosition, Is.EqualTo(new Vector2(16f, -16f)));
        Assert.That(edition.anchoredPosition, Is.EqualTo(new Vector2(16f, -72f)));
        Assert.That(brand.sizeDelta.y, Is.GreaterThan(0f));
        Assert.That(edition.anchoredPosition.y, Is.LessThan(brand.anchoredPosition.y));
    }

    [Test]
    public void DropdownTemplate_UsesWoodListContract()
    {
        TMP_Dropdown dropdown = null;
        Toggle toggle = null;
        SettingDisplayControlsFactory.EnsureDisplayControls(_panelRoot.transform, ref dropdown, ref toggle);

        Assert.That(dropdown.captionText.enableWordWrapping, Is.False);
        Assert.That(dropdown.itemText.enableWordWrapping, Is.False);
        Assert.That(dropdown.captionText.fontSize, Is.EqualTo(24f));
        Assert.That(dropdown.itemText.fontSize, Is.EqualTo(24f));
        Assert.That(dropdown.captionText.margin.x, Is.EqualTo(SettingsWoodPanelSpec.DropdownCaptionPaddingLeft));
        Assert.That(dropdown.captionText.margin.z, Is.EqualTo(SettingsWoodPanelSpec.DropdownCaptionPaddingRight));

        RectTransform template = dropdown.template;
        Assert.That(template, Is.Not.Null);
        Assert.That(template.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.DropdownListMaxHeight));
        Image templateImage = template.GetComponent<Image>();
        Assert.That(templateImage.color, Is.EqualTo(SettingsWoodPanelSpec.InputBackground));
        Canvas overlay = template.GetComponent<Canvas>();
        Assert.That(overlay, Is.Not.Null);
        Assert.That(template.sizeDelta.y, Is.EqualTo(SettingsWoodPanelSpec.DropdownListMaxHeight));

        Transform item = template.Find("Viewport/Content/Item");
        if (item == null)
        {
            Transform content = template.Find("Viewport/Content");
            if (content != null && content.childCount > 0)
                item = content.GetChild(0);
        }

        Assert.That(item, Is.Not.Null);
        LayoutElement itemLayout = item.GetComponent<LayoutElement>();
        Assert.That(itemLayout.minHeight, Is.EqualTo(SettingsWoodPanelSpec.DropdownItemHeight));
        Image itemBackground = item.Find("Item Background")?.GetComponent<Image>()
            ?? item.Find("Background")?.GetComponent<Image>()
            ?? item.GetComponent<Image>();
        Assert.That(itemBackground, Is.Not.Null);
        Assert.That(itemBackground.color, Is.Not.EqualTo(Color.white));
        AssertUprightDropdownItem(dropdown);
    }

    [Test]
    public void LanguageAndTextSizeDropdowns_KeepUprightItemLabels()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        shell.SelectTab(SettingsTabId.Language);

        TMP_Dropdown language = shell.LanguagePage.transform
            .Find("LanguageRow/Value/LanguageDropdown")
            ?.GetComponent<TMP_Dropdown>();
        TMP_Dropdown textSize = shell.LanguagePage.transform
            .Find("TextSizeRow/Value/TextSizeDropdown")
            ?.GetComponent<TMP_Dropdown>();

        Assert.That(language, Is.Not.Null);
        Assert.That(textSize, Is.Not.Null);
        AssertUprightDropdownItem(language);
        AssertUprightDropdownItem(textSize);
        Assert.That(language.options.Count, Is.EqualTo(3));
        Assert.That(textSize.options.Count, Is.EqualTo(3));
        Assert.That(language.options.ConvertAll(option => option.text), Is.EqualTo(
            new[] { "한국어", "English", "日本語" }));
    }

    [Test]
    public void RefreshLabels_UpdatesAsideWhenLocaleChanges()
    {
        SettingsDisplayPreferences.ApplyLocale(CheshireLocaleResolver.Korean);
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        TMP_Text aside = shell.Sidebar.Find("Aside")?.GetComponent<TMP_Text>();
        Assert.That(aside, Is.Not.Null);
        Assert.That(aside.text, Is.EqualTo(CheshireUiStrings.Lookup("SettingsAside", CheshireLocaleResolver.Korean)));

        SettingsDisplayPreferences.ApplyLocale(CheshireLocaleResolver.English);
        shell.RefreshLabels();
        Assert.That(aside.text, Is.EqualTo(CheshireUiStrings.Lookup("SettingsAside", CheshireLocaleResolver.English)));
        Assert.That(aside.text, Does.Not.Contain("저택"));

        SettingsDisplayPreferences.ApplyLocale(CheshireLocaleResolver.Japanese);
        shell.RefreshLabels();
        Assert.That(aside.text, Is.EqualTo(CheshireUiStrings.Lookup("SettingsAside", CheshireLocaleResolver.Japanese)));
        Assert.That(aside.text, Does.Contain("屋敷"));
        Assert.That(shell.Heading.text, Is.EqualTo(
            CheshireUiStrings.Lookup("SettingsTabGeneral", CheshireLocaleResolver.Japanese)));
    }

    [Test]
    public void CheshirePreview_UsesNonEmptyLocalizedAskExampleAndSampleReply()
    {
        SettingsDisplayPreferences.ApplyLocale(CheshireLocaleResolver.English);
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        shell.SelectTab(SettingsTabId.CheshireAi);

        Assert.That(shell.Preview, Is.Not.Null);
        Assert.That(shell.Preview.AnswerLabel.text, Is.Not.Empty);
        Assert.That(ButtonLabel(shell.Preview.AskButton), Is.Not.Empty);
        Assert.That(ButtonLabel(shell.Preview.ExampleButton), Is.Not.Empty);
        Assert.That(shell.Preview.AnswerLabel.text, Does.Not.Match(@"[\uAC00-\uD7A3]"));
        Assert.That(ButtonLabel(shell.Preview.AskButton), Does.Not.Match(@"[\uAC00-\uD7A3]"));
    }

    [Test]
    public void CheshirePreview_PromptAndAskUseFullPreviewWidth()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform input = shell.Preview.PromptInput.GetComponent<RectTransform>();
        RectTransform ask = shell.Preview.AskButton.GetComponent<RectTransform>();
        RectTransform example = shell.Preview.ExampleButton.GetComponent<RectTransform>();

        Assert.That(input.anchorMax.x, Is.EqualTo(1f));
        Assert.That(ask.anchorMax.x, Is.EqualTo(1f));
        Assert.That(example.anchorMin.x, Is.EqualTo(0f));
        Assert.That(example.anchorMax.x, Is.EqualTo(0.5f));
        Assert.That(ask.anchorMin.x, Is.EqualTo(0.5f));
    }

    [Test]
    public void CheshirePreview_MetricsStayInsidePreview()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform first = shell.Preview.FirstMetric.rectTransform;
        RectTransform complete = shell.Preview.CompleteMetric.rectTransform;

        Assert.That(first.offsetMax.x, Is.LessThanOrEqualTo(0f));
        Assert.That(complete.offsetMax.x, Is.LessThanOrEqualTo(0f));
        Assert.That(first.offsetMin.x, Is.LessThan(0f));
        Assert.That(complete.offsetMin.x, Is.LessThan(0f));
    }

    static string ButtonLabel(Button button)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        return label != null ? label.text : "";
    }

    static void AssertUprightDropdownItem(TMP_Dropdown dropdown)
    {
        Assert.That(dropdown.itemText, Is.Not.Null);
        RectTransform label = dropdown.itemText.rectTransform;
        Assert.That(label.localScale, Is.EqualTo(Vector3.one));
        Assert.That(label.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(label.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(label.offsetMin.x, Is.GreaterThanOrEqualTo(0f));
        Assert.That(label.offsetMax.x, Is.LessThanOrEqualTo(0f));
        Assert.That(dropdown.itemText.isRightToLeftText, Is.False);
        Assert.That(dropdown.itemText.margin.x + dropdown.itemText.margin.z, Is.LessThanOrEqualTo(8f));

        Transform item = label.parent;
        Assert.That(item.localScale, Is.EqualTo(Vector3.one));
        LayoutElement itemLayout = item.GetComponent<LayoutElement>();
        Assert.That(itemLayout, Is.Not.Null);
        Assert.That(itemLayout.preferredHeight, Is.EqualTo(SettingsWoodPanelSpec.DropdownItemHeight));

        Transform check = item.Find("Item Checkmark") ?? item.Find("Checkmark");
        Assert.That(check, Is.Not.Null);
        RectTransform checkRect = check as RectTransform;
        Assert.That(checkRect.anchorMin, Is.EqualTo(checkRect.anchorMax), "checkmark must not stretch");
        Assert.That(checkRect.sizeDelta.x, Is.LessThanOrEqualTo(20f));
        Assert.That(checkRect.localScale, Is.EqualTo(Vector3.one));
        Image checkImage = check.GetComponent<Image>();
        if (checkImage != null && checkImage.sprite == null)
            Assert.That(checkImage.enabled, Is.False);
    }

    [Test]
    public void ParentCanvas_UsesExpandScalerAt1080p()
    {
        _panelRoot.AddComponent<Canvas>();
        CanvasScaler scaler = _panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        SettingsShellFactory.Ensure(_panelRoot.transform);

        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
    }

    [Test]
    public void ReturnButton_MatchesSpecSize()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform rect = shell.ReturnButton.GetComponent<RectTransform>();
        Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(
            SettingsWoodPanelSpec.ReturnButtonWidth,
            SettingsWoodPanelSpec.ReturnButtonHeight)));
    }

    [Test]
    public void GeneralHeading_UsesBasicSettingsLocaleKey()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        shell.SelectTab(SettingsTabId.General);
        string locale = SettingsDisplayPreferences.ResolveLocale();
        Assert.That(shell.Heading.text, Is.EqualTo(CheshireUiStrings.Lookup("SettingsTabGeneral", locale)));
        Assert.That(shell.Heading.text, Is.Not.EqualTo(CheshireUiStrings.Lookup("SettingsSound", locale)));
    }

    [Test]
    public void Tabs_KeepReservedMarkerSlotAtFixedSize()
    {
        SettingsShellController shell = SettingsShellFactory.Ensure(_panelRoot.transform);
        RectTransform general = shell.TabGeneral.GetComponent<RectTransform>();
        Assert.That(general.sizeDelta, Is.EqualTo(new Vector2(
            SettingsWoodPanelSpec.TabWidth,
            SettingsWoodPanelSpec.TabHeight)));
        Vector2 first = general.anchoredPosition;
        shell.SelectTab(SettingsTabId.Language);
        Assert.That(general.anchoredPosition, Is.EqualTo(first));
        Assert.That(shell.TabLanguage.GetComponent<RectTransform>().anchoredPosition.y, Is.EqualTo(
            first.y - 2f * (SettingsWoodPanelSpec.TabHeight + SettingsWoodPanelSpec.TabGap)));
    }

    static RectTransform FindHandle(Slider slider)
    {
        Transform[] all = slider.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != slider.transform && all[i].name == "Handle")
                return all[i] as RectTransform;
        }

        return slider.handleRect;
    }
}
