using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared settings view. Runtime state is owned by the loopback backend.</summary>
public sealed class LocalAiSettingsPanel : MonoBehaviour
{
    const string PanelName = "LocalAiSettingsPanel";
    TMP_Text statusLabel;
    Button[] modeButtons;
    Button closeButton;
    Button openButton;
    bool applying;

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
        string[] modes = { "cpu", "gpu", "auto" };
        panel.modeButtons = new Button[3];
        for (int i = 0; i < modes.Length; i++)
        {
            string mode = modes[i];
            Button button = MakeButton(root.transform, mode.ToUpperInvariant(),
                new Vector2((i - 1) * 190, 115), font);
            button.onClick.AddListener(() => panel.Apply(mode));
            panel.modeButtons[i] = button;
        }
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

    public static bool HandleModalInput(GameObject settingsRoot)
    {
        if (settingsRoot == null)
            return false;
        Transform child = settingsRoot.transform.Find(PanelName);
        if (child == null || !child.gameObject.activeInHierarchy)
            return false;
        if (Input.GetKeyDown(KeyCode.Escape))
            child.GetComponent<LocalAiSettingsPanel>().Close();
        return true;
    }

    void OnEnable()
    {
        if (statusLabel == null)
            return;
        applying = false;
        SetButtons(false);
        statusLabel.text = Text("AiSettingsConnecting");
        StartCoroutine(Poll());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        applying = false;
    }

    void Close()
    {
        gameObject.SetActive(false);
        if (openButton != null && openButton.gameObject.activeInHierarchy)
            openButton.Select();
    }

    void SetButtons(bool enabled)
    {
        foreach (Button button in modeButtons)
            button.interactable = enabled;
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
        applying = false;
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
        obj.GetComponent<Image>().color = new Color(0.38f, 0.20f, 0.04f);
        var button = obj.GetComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        Label(obj.transform, "Label", title, Vector2.zero, rect.sizeDelta, font, 24);
        return button;
    }
}
