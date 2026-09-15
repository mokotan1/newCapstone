using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 패널/설정 씬용 해상도 드롭다운·전체화면 토글 UI를 생성합니다.
/// </summary>
public static class SettingDisplayControlsFactory
{
    static readonly string[] ResolutionDropdownNames = { "Resolution_Dropdown", "ResolutionDropdown" };
    static readonly string[] FullscreenToggleNames = { "Fullscreen Toggle", "FullscreenToggle" };
    static readonly string[] ResolutionLabelNames = { "Resolution Text", "Resolution Text ", "ResolutionText" };
    static readonly string[] FullscreenLabelNames = { "Fullscreen Text", "FullscreenText" };

    static readonly Color LabelColor = Color.white;
    static readonly Color DropdownBackgroundColor = new Color(0.3773585f, 0.19897084f, 0f, 1f);

    public static void EnsureDisplayControls(Transform panelRoot, ref TMP_Dropdown resolutionDropdown, ref Toggle fullscreenToggle)
    {
        if (panelRoot == null)
            return;

        SettingsShellController shell = SettingsShellFactory.Ensure(panelRoot);
        Transform generalPage = SettingsShellFactory.TryGetGeneralPage(panelRoot) ?? panelRoot;
        Transform aiPage = SettingsShellFactory.TryGetCheshireAiPage(panelRoot);
        if (aiPage != null)
            LocalAiSettingsPanel.EnsureEmbedded(aiPage);
        else
            LocalAiSettingsPanel.Ensure(panelRoot);

        if (resolutionDropdown == null)
            resolutionDropdown = FindNamedComponent<TMP_Dropdown>(panelRoot, ResolutionDropdownNames);

        if (fullscreenToggle == null)
            fullscreenToggle = FindNamedComponent<Toggle>(panelRoot, FullscreenToggleNames);

        if (resolutionDropdown != null && fullscreenToggle != null)
        {
            if (shell != null)
                AdoptIntoGeneralRow(generalPage, resolutionDropdown.transform, fullscreenToggle.transform);
            return;
        }

        TMP_FontAsset labelFont = FindPanelFont(generalPage) ?? FindPanelFont(panelRoot);

        if (resolutionDropdown == null)
        {
            EnsureLabel(generalPage, ResolutionLabelNames, CheshireUiStrings.Lookup("SettingsResolution", SettingsDisplayPreferences.ResolveLocale()), new Vector2(-200f, -100f), new Vector2(300f, 100f), labelFont);
            resolutionDropdown = CreateResolutionDropdown(generalPage, labelFont);
        }

        if (fullscreenToggle == null)
        {
            EnsureLabel(generalPage, FullscreenLabelNames, CheshireUiStrings.Lookup("SettingsFullscreen", SettingsDisplayPreferences.ResolveLocale()), new Vector2(-100f, -250f), new Vector2(400f, 100f), labelFont);
            fullscreenToggle = CreateFullscreenToggle(generalPage);
        }

        if (shell != null)
            AdoptIntoGeneralRow(generalPage, resolutionDropdown != null ? resolutionDropdown.transform : null, fullscreenToggle != null ? fullscreenToggle.transform : null);
    }

    public static Slider FindSlider(Transform root, params string[] names)
    {
        return FindNamedComponent<Slider>(root, names);
    }

    static void AdoptIntoGeneralRow(Transform generalPage, Transform dropdown, Transform toggle)
    {
        if (generalPage == null)
            return;

        if (dropdown != null)
            PlaceInRow(generalPage, "ResolutionRow", "SettingsResolution", dropdown);
        if (toggle != null)
            PlaceInRow(generalPage, "FullscreenRow", "SettingsFullscreen", toggle);
    }

    static void PlaceInRow(Transform generalPage, string rowName, string labelKey, Transform control)
    {
        Transform row = generalPage.Find(rowName);
        if (row == null)
        {
            GameObject created = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            created.transform.SetParent(generalPage, false);
            var layout = created.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = SettingsWoodPanelSpec.ColumnGap;
            layout.padding = new RectOffset(0, 0, 0, 0);
            created.GetComponent<LayoutElement>().minHeight = SettingsWoodPanelSpec.RowHeight;
            created.GetComponent<LayoutElement>().preferredHeight = SettingsWoodPanelSpec.RowHeight;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(created.transform, false);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = CheshireUiStrings.Lookup(labelKey, SettingsDisplayPreferences.ResolveLocale());
            label.fontSize = SettingsWoodPanelSpec.UiFontSize;
            label.color = SettingsWoodPanelSpec.PrimaryText;
            TMP_FontAsset font = FindPanelFont(generalPage);
            if (font != null)
                label.font = font;

            var value = new GameObject("Value", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            value.transform.SetParent(created.transform, false);
            var valueLayout = value.GetComponent<HorizontalLayoutGroup>();
            valueLayout.childAlignment = TextAnchor.MiddleRight;
            valueLayout.childControlHeight = true;
            valueLayout.childControlWidth = true;
            valueLayout.childForceExpandHeight = false;
            valueLayout.childForceExpandWidth = false;
            var valueElement = value.GetComponent<LayoutElement>();
            valueElement.flexibleWidth = 0f;
            valueElement.preferredWidth = SettingsWoodPanelSpec.ControlWidth;
            valueElement.minWidth = SettingsWoodPanelSpec.ControlWidth;
            valueElement.minHeight = SettingsWoodPanelSpec.ControlHeight;
            valueElement.preferredHeight = SettingsWoodPanelSpec.ControlHeight;
            row = created.transform;
        }

        Transform valueSlot = row.Find("Value");
        if (valueSlot == null || control == null)
            return;
        control.SetParent(valueSlot, false);
        Slider slider = control.GetComponent<Slider>();
        if (slider != null)
            SettingsShellFactory.StyleWoodSlider(slider);
        TMP_Dropdown dropdown = control.GetComponent<TMP_Dropdown>();
        if (dropdown != null)
            SettingsShellFactory.StyleWoodDropdown(dropdown, FindPanelFont(generalPage));
        Toggle toggle = control.GetComponent<Toggle>();
        if (toggle != null)
            SettingsShellFactory.StyleWoodToggle(toggle);
    }

    static TMP_Dropdown CreateResolutionDropdown(Transform panelRoot, TMP_FontAsset labelFont)
    {
        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        dropdownObject.name = ResolutionDropdownNames[0];
        dropdownObject.transform.SetParent(panelRoot, false);
        dropdownObject.layer = panelRoot.gameObject.layer;
        ConfigureControlRect(dropdownObject.GetComponent<RectTransform>(), new Vector2(100f, -90f), new Vector2(400f, 50f));

        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        ApplyDropdownFont(dropdown, labelFont);
        ApplyDropdownColors(dropdown);
        return dropdown;
    }

    static Toggle CreateFullscreenToggle(Transform panelRoot)
    {
        GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
        toggleObject.name = FullscreenToggleNames[0];
        toggleObject.transform.SetParent(panelRoot, false);
        toggleObject.layer = panelRoot.gameObject.layer;
        ConfigureControlRect(toggleObject.GetComponent<RectTransform>(), new Vector2(100f, -240f), new Vector2(300f, 100f));

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        ApplyToggleColors(toggle);

        Transform labelTransform = toggleObject.transform.Find("Label");
        if (labelTransform != null)
        {
            Text legacyLabel = labelTransform.GetComponent<Text>();
            if (legacyLabel != null)
                legacyLabel.text = string.Empty;
        }

        return toggle;
    }

    static void EnsureLabel(
        Transform panelRoot,
        string[] existingNames,
        string text,
        Vector2 anchoredPosition,
        Vector2 size,
        TMP_FontAsset labelFont)
    {
        if (FindNamedTransform(panelRoot, existingNames) != null)
            return;

        CreateLabel(panelRoot, existingNames[0], text, anchoredPosition, size, labelFont);
    }

    static void CreateLabel(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, TMP_FontAsset font)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        labelObject.layer = parent.gameObject.layer;

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 36f;
        label.color = LabelColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (font != null)
        {
            label.font = font;
            label.fontSharedMaterial = font.material;
        }
    }

    static void ConfigureControlRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    static void ApplyDropdownFont(TMP_Dropdown dropdown, TMP_FontAsset font)
    {
        if (dropdown == null || font == null)
            return;

        if (dropdown.captionText != null)
        {
            dropdown.captionText.font = font;
            dropdown.captionText.fontSharedMaterial = font.material;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.font = font;
            dropdown.itemText.fontSharedMaterial = font.material;
        }
    }

    static void ApplyDropdownColors(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        if (dropdown.targetGraphic is Image targetGraphic)
            targetGraphic.color = DropdownBackgroundColor;

        ColorBlock colors = dropdown.colors;
        colors.highlightedColor = Color.red;
        colors.selectedColor = Color.red;
        dropdown.colors = colors;
    }

    static void ApplyToggleColors(Toggle toggle)
    {
        if (toggle == null)
            return;

        ColorBlock colors = toggle.colors;
        colors.highlightedColor = Color.red;
        colors.selectedColor = Color.red;
        toggle.colors = colors;
    }

    static TMP_FontAsset FindPanelFont(Transform panelRoot)
    {
        TextMeshProUGUI[] labels = panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].font != null)
                return labels[i].font;
        }

        return null;
    }

    static T FindNamedComponent<T>(Transform root, string[] names) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (components[i].name == names[j])
                    return components[i];
            }
        }

        return null;
    }

    static Transform FindNamedTransform(Transform root, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = root.Find(names[i]);
            if (child != null)
                return child;
        }

        return null;
    }
}
