using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared settings view. Runtime state is owned by the loopback backend.</summary>
public sealed class LocalAiSettingsPanel : MonoBehaviour
{
    const string PanelName = "LocalAiSettingsPanel";
    const float DisabledIdleAlpha = 0.45f;
    static readonly string[] ModeKeys = { "cpu", "gpu", "auto" };
    TMP_Text statusLabel;
    Button[] modeButtons;
    Button closeButton;
    Button openButton;
    bool applying;
    bool isEmbedded;

    internal bool OwnsIndependentStatusPoll =>
        SettingsCheshirePreviewGate.PanelOwnsIndependentStatusPoll(isEmbedded);

    static string Text(string key) => CheshireUiStrings.Lookup(key, CheshireLocaleResolver.ResolveCurrentLocale());

    public static void Ensure(Transform parent)
    {
        if (parent == null || parent.Find(PanelName) != null)
            return;
        TMP_FontAsset font = parent.GetComponentInChildren<TMP_Text>(true)?.font;
        var root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        root.SetActive(false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(35, 35);
        rect.offsetMax = new Vector2(-35, -35);
        root.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.99f);
        var panel = root.AddComponent<LocalAiSettingsPanel>();
        Label(root.transform, "Title", Text("AiSettingsTitle"), new Vector2(0, 205), new Vector2(580, 55), font, 32);
        panel.statusLabel = Label(root.transform, "Status", "", new Vector2(0, -35), new Vector2(590, 250), font, 23);
        panel.modeButtons = new Button[ModeKeys.Length];
        for (int i = 0; i < ModeKeys.Length; i++)
        {
            string mode = ModeKeys[i];
            Button button = MakeButton(root.transform, mode.ToUpperInvariant(),
                new Vector2((i - 1) * 190, 115), font);
            button.onClick.AddListener(() => panel.Apply(mode));
            panel.modeButtons[i] = button;
        }
        panel.HighlightRequestedMode(LocalAiControlApi.DefaultRequestedMode);
        panel.closeButton = MakeButton(root.transform, Text("AiSettingsBack"), new Vector2(0, -220), font);
        panel.closeButton.onClick.AddListener(panel.Close);
        panel.openButton = MakeButton(parent, Text("AiSettingsTitle"), Vector2.zero, font);
        panel.openButton.name = "LocalAiSettingsButton";
        RectTransform openRect = (RectTransform)panel.openButton.transform;
        openRect.anchorMin = openRect.anchorMax = new Vector2(1, 0);
        openRect.anchoredPosition = new Vector2(-145, 60);
        panel.openButton.onClick.AddListener(() =>
        {
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            panel.closeButton.Select();
        });
    }

    public static void EnsureEmbedded(Transform parent)
    {
        if (parent == null || parent.Find(PanelName) != null)
            return;

        TMP_FontAsset font = parent.GetComponentInChildren<TMP_Text>(true)?.font ?? SettingsShellFactory.FindUiFont();
        var root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        root.SetActive(false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 168f);
        rect.anchoredPosition = Vector2.zero;
        root.GetComponent<Image>().color = Color.clear;
        var layout = root.AddComponent<LayoutElement>();
        layout.minHeight = 168f;
        layout.preferredHeight = 168f;

        var panel = root.AddComponent<LocalAiSettingsPanel>();
        panel.isEmbedded = true;
        root.SetActive(true);
        panel.statusLabel = Label(root.transform, "Status", "", new Vector2(0, 36), new Vector2(620, 70), font, 16);
        panel.statusLabel.color = SettingsWoodPanelSpec.PrimaryText;
        panel.modeButtons = new Button[ModeKeys.Length];
        for (int i = 0; i < ModeKeys.Length; i++)
        {
            string mode = ModeKeys[i];
            Button button = MakeButton(root.transform, mode.ToUpperInvariant(),
                new Vector2((i - 1) * 170, -40), font);
            button.onClick.AddListener(() => panel.Apply(mode));
            panel.modeButtons[i] = button;
        }
        panel.HighlightRequestedMode(LocalAiControlApi.DefaultRequestedMode);
    }

    public static bool HandleModalInput(GameObject settingsRoot)
    {
        if (settingsRoot == null)
            return false;
        LocalAiSettingsPanel[] panels = settingsRoot.GetComponentsInChildren<LocalAiSettingsPanel>(true);
        for (int i = 0; i < panels.Length; i++)
        {
            LocalAiSettingsPanel panel = panels[i];
            if (panel == null || panel.isEmbedded || !panel.gameObject.activeInHierarchy)
                continue;
            if (Input.GetKeyDown(KeyCode.Escape))
                panel.Close();
            return true;
        }

        return false;
    }

    void OnEnable()
    {
        if (statusLabel == null)
            return;
        applying = false;
        if (OwnsIndependentStatusPoll)
        {
            SetButtons(false);
            statusLabel.text = Text("AiSettingsConnecting");
            StartCoroutine(Poll());
            return;
        }

        SetButtons(true);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        applying = false;
    }

    public void ShowRuntimeStatus(string text)
    {
        if (statusLabel == null)
            return;
        statusLabel.text = text ?? "";
    }

    public void HighlightRequestedMode(string requestedMode)
    {
        if (modeButtons == null)
            return;
        string normalized = string.IsNullOrWhiteSpace(requestedMode)
            ? ""
            : requestedMode.Trim().ToLowerInvariant();
        for (int i = 0; i < modeButtons.Length; i++)
        {
            bool selected = i < ModeKeys.Length && ModeKeys[i] == normalized;
            ApplyModeButtonVisual(modeButtons[i], selected);
        }
    }

    public void NotifyApplyCompleted()
    {
        applying = false;
        if (!OwnsIndependentStatusPoll)
            SetButtons(true);
    }

    public void RefreshLocalizedText()
    {
        if (isEmbedded || statusLabel == null || applying)
            return;
        statusLabel.text = Text("AiSettingsUnavailable");
    }

    void Close()
    {
        gameObject.SetActive(false);
        if (openButton != null && openButton.gameObject.activeInHierarchy)
            openButton.Select();
    }

    void SetButtons(bool enabled)
    {
        if (modeButtons == null)
            return;
        foreach (Button button in modeButtons)
        {
            if (button != null)
                button.interactable = enabled;
        }
    }

    IEnumerator Poll()
    {
        while (true)
        {
            if (!applying)
            {
                yield return ChatHttpClient.LocalAiControlRequest(ServerConfig.GetOrCreate().ChatUrl, null, (code, json) =>
                {
                    if (applying)
                        return;
                    if (code != 200 || !LocalAiControlApi.TryParseStatus(json, out LocalAiRuntimeStatus status))
                    {
                        SetButtons(false);
                        statusLabel.text = Text("AiSettingsUnavailable");
                        return;
                    }
                    SetButtons(status.CanApply);
                    HighlightRequestedMode(status.RequestedMode);
                    string gpu = status.GpuUtilization.HasValue
                        ? string.Format(Text("AiSettingsGpuUsage"), status.GpuName, status.GpuUtilization.Value,
                            status.GpuMemoryUsedMiB ?? 0, status.GpuMemoryTotalMiB ?? 0)
                        : Text("AiSettingsGpuUnknown");
                    string state = Text("AiState_" + status.State);
                    string note = status.State == "externally_managed" ? Text("AiSettingsExternal")
                        : string.IsNullOrEmpty(status.FallbackReason) ? "" : Text("AiSettingsFallback");
                    statusLabel.text = string.Format(Text("AiSettingsStatus"),
                        status.RequestedMode.ToUpperInvariant(), status.EffectiveBackend.ToUpperInvariant(), state)
                        + "\n\n" + gpu + "\n" + note;
                });
            }
            yield return new WaitForSecondsRealtime(2);
        }
    }

    void Apply(string mode)
    {
        if (applying)
            return;
        HighlightRequestedMode(mode);
        applying = true;
        SetButtons(false);
        statusLabel.text = Text("AiSettingsApplying");
        StartCoroutine(ApplyRequest(mode));
    }

    IEnumerator ApplyRequest(string mode)
    {
        yield return ChatHttpClient.LocalAiControlRequest(ServerConfig.GetOrCreate().ChatUrl, mode, (code, _) =>
        {
            if (code != 202)
                statusLabel.text = Text(code == 409 ? "AiSettingsBusy" : "AiSettingsUnavailable");
        });
        NotifyApplyCompleted();
    }

    static TMP_Text Label(Transform parent, string name, string text, Vector2 position,
        Vector2 size, TMP_FontAsset font, int fontSize)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = obj.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    static Button MakeButton(Transform parent, string title, Vector2 position, TMP_FontAsset font)
    {
        var obj = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.sizeDelta = new Vector2(170, 48);
        rect.anchoredPosition = position;
        Image image = obj.GetComponent<Image>();
        var button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        Outline outline = obj.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);
        ApplyModeButtonVisual(button, false);
        Label(obj.transform, "Label", title, Vector2.zero, rect.sizeDelta, font, 24);
        return button;
    }

    static void ApplyModeButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Graphic graphic = button.targetGraphic ?? button.GetComponent<Image>();
        if (graphic != null)
            graphic.color = Color.white;

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
            outline.effectColor = selected
                ? SettingsWoodPanelSpec.SelectedBorder
                : SettingsWoodPanelSpec.Border;

        Color idleDisabled = SettingsWoodPanelSpec.ButtonFace;
        idleDisabled.a = DisabledIdleAlpha;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = selected
            ? SettingsWoodPanelSpec.SelectedBackground
            : SettingsWoodPanelSpec.ButtonFace;
        colors.highlightedColor = SettingsWoodPanelSpec.SelectedBorder;
        colors.pressedColor = SettingsWoodPanelSpec.PrimaryButtonFace;
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = selected
            ? SettingsWoodPanelSpec.SelectedBackground
            : idleDisabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
    }
}
