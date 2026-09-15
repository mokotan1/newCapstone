using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings-only Cheshire ask. Uses a private history so game progress is unchanged.
/// </summary>
public sealed class SettingsCheshirePreview : MonoBehaviour, IChatHttpCallbacks
{
    public const string RootName = "SettingsCheshirePreview";

    public TMP_Text StatusLabel;
    public TMP_Text AnswerLabel;
    public TMP_Text FirstMetric;
    public TMP_Text CompleteMetric;
    public TMP_InputField PromptInput;
    public Button AskButton;
    public Button ExampleButton;
    public Image Portrait;

    ChatHttpClient _client;
    ChatHistoryManager _history;
    Coroutine _askRoutine;
    Coroutine _readyRoutine;
    bool _requestInProgress;
    bool _localAiReady = true;
    string _streamBuffer = "";

    public bool IsRequestInProgress
    {
        get => _requestInProgress;
        set => _requestInProgress = value;
    }

    public bool? UseToolsOverrideForNextRequest { get; set; }

    public static SettingsCheshirePreview Ensure(Transform parent)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find(RootName);
        if (existing != null)
            return existing.GetComponent<SettingsCheshirePreview>();

        GameObject root = SettingsShellFactory.CreatePanel(parent, RootName, SettingsWoodPanelSpec.InputBackground);
        var layout = root.AddComponent<LayoutElement>();
        layout.minHeight = 448f;
        layout.preferredHeight = 448f;
        var preview = root.AddComponent<SettingsCheshirePreview>();
        preview.Build(root.transform);
        return preview;
    }

    public void RefreshLabels(string locale)
    {
        if (PromptInput != null && string.IsNullOrWhiteSpace(PromptInput.text))
            PromptInput.text = CheshireUiStrings.Lookup("SettingsExamplePrompt", locale);
        if (AskButton != null)
            SetButtonLabel(AskButton, CheshireUiStrings.Lookup("SettingsAsk", locale));
        if (ExampleButton != null)
            SetButtonLabel(ExampleButton, CheshireUiStrings.Lookup("SettingsExample", locale));
    }

    public void CancelInFlight()
    {
        if (_askRoutine != null)
        {
            StopCoroutine(_askRoutine);
            _askRoutine = null;
        }

        _requestInProgress = false;
    }

    public string BuildAndComposeSystemPrompt(string userMessage)
    {
        EnsureClient();
        string locale = SettingsDisplayPreferences.ResolveLocale();
        string baseSystem = CheshirePromptCatalog.Load("BaseSystem", locale);
        return _history.ComposeSystemPromptWithCommonRules(baseSystem, locale);
    }

    public void AugmentChatPayload(LocalLlamaPayload payload, string userMessage)
    {
    }

    public void OnChatHttpWaitStarted()
    {
    }

    public void OnChatHttpWaitFinished()
    {
    }

    public void OnStreamTextDelta(string delta)
    {
        _streamBuffer += delta ?? "";
        if (AnswerLabel != null)
        {
            AnswerLabel.text = _streamBuffer;
            AnswerLabel.fontSize = SettingsDisplayPreferences.ResolveAnswerFontSize();
        }

        RefreshMetrics();
    }

    public void SayLine(string message, Action onComplete)
    {
        if (AnswerLabel != null)
            AnswerLabel.text = message ?? "";
        onComplete?.Invoke();
    }

    public Coroutine StartHostCoroutine(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }

    public IEnumerator HandleChatbotResponse(string responseMessage, List<FunctionCallData> functionCalls)
    {
        if (AnswerLabel != null)
            AnswerLabel.text = responseMessage ?? "";
        RefreshMetrics();
        yield break;
    }

    void OnEnable()
    {
        if (FirstMetric != null)
            FirstMetric.gameObject.SetActive(false);
        if (CompleteMetric != null)
            CompleteMetric.gameObject.SetActive(false);
        RefreshLabels(SettingsDisplayPreferences.ResolveLocale());
        ApplyAnswerSize();
        EnsureClient();
        if (_readyRoutine != null)
            StopCoroutine(_readyRoutine);
        _readyRoutine = StartCoroutine(CoPollLocalAiReady());
    }

    void OnDisable()
    {
        CancelInFlight();
        if (_readyRoutine != null)
        {
            StopCoroutine(_readyRoutine);
            _readyRoutine = null;
        }
    }

    void Build(Transform root)
    {
        TMP_FontAsset uiFont = SettingsShellFactory.FindUiFont();
        TMP_FontAsset pixelFont = SettingsShellFactory.FindPixelFont();

        StatusLabel = SettingsShellFactory.CreateLabel(
            root, "PreviewStatus", "", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -8f), new Vector2(0f, 28f), uiFont, SettingsWoodPanelSpec.CaptionFontSize,
            SettingsWoodPanelSpec.ReadyStatus, TextAlignmentOptions.MidlineLeft);

        AnswerLabel = SettingsShellFactory.CreateLabel(
            root, "Answer", CheshireUiStrings.Lookup("SettingsSampleReply", SettingsDisplayPreferences.ResolveLocale()),
            new Vector2(0f, 0.35f), new Vector2(1f, 0.92f),
            new Vector2(108f, 8f), new Vector2(-12f, -8f), uiFont,
            SettingsDisplayPreferences.ResolveAnswerFontSize(),
            SettingsWoodPanelSpec.PrimaryText, TextAlignmentOptions.TopLeft);
        AnswerLabel.lineSpacing = 16f;
        AnswerLabel.enableWordWrapping = true;
        AnswerLabel.overflowMode = TextOverflowModes.Overflow;

        Portrait = SettingsShellFactory.CreateImage(root, "Portrait", SettingsWoodPanelSpec.InputBackground);
        RectTransform portraitRect = Portrait.rectTransform;
        portraitRect.anchorMin = new Vector2(0f, 0.55f);
        portraitRect.anchorMax = new Vector2(0f, 0.55f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.sizeDelta = new Vector2(96f, 144f);
        portraitRect.anchoredPosition = new Vector2(8f, 0f);
        Portrait.sprite = SettingsShellFactory.LoadSprite(
            "Assets/mokotan/animation/parrot_wing_peak.png");
        Portrait.preserveAspect = true;

        FirstMetric = SettingsShellFactory.CreateLabel(
            root, "FirstMetric", "—", new Vector2(1f, 0.72f), new Vector2(1f, 0.72f),
            new Vector2(-SettingsWoodPanelSpec.MetricsColumnWidth, 0f),
            new Vector2(-8f, 56f),
            pixelFont, SettingsWoodPanelSpec.MetricPixelFontSize,
            SettingsWoodPanelSpec.Accent, TextAlignmentOptions.MidlineLeft);

        CompleteMetric = SettingsShellFactory.CreateLabel(
            root, "CompleteMetric", "—", new Vector2(1f, 0.48f), new Vector2(1f, 0.48f),
            new Vector2(-SettingsWoodPanelSpec.MetricsColumnWidth, 0f),
            new Vector2(-8f, 56f),
            pixelFont, SettingsWoodPanelSpec.MetricPixelFontSize,
            SettingsWoodPanelSpec.Accent, TextAlignmentOptions.MidlineLeft);
        FirstMetric.gameObject.SetActive(false);
        CompleteMetric.gameObject.SetActive(false);

        GameObject inputObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        inputObject.name = "Prompt";
        inputObject.transform.SetParent(root, false);
        PromptInput = inputObject.GetComponent<TMP_InputField>();
        PromptInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        PromptInput.characterLimit = 300;
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0.28f);
        inputRect.offsetMin = new Vector2(8f, 48f);
        inputRect.offsetMax = new Vector2(-8f, -4f);
        if (PromptInput.textComponent != null)
        {
            PromptInput.textComponent.font = uiFont;
            PromptInput.textComponent.color = SettingsWoodPanelSpec.PrimaryText;
            PromptInput.textComponent.fontSize = SettingsWoodPanelSpec.UiFontSize;
        }

        Image inputImage = inputObject.GetComponent<Image>();
        if (inputImage != null)
            inputImage.color = SettingsWoodPanelSpec.InputBackground;

        ExampleButton = SettingsShellFactory.CreateButton(
            root, "Example", CheshireUiStrings.Lookup("SettingsExample", SettingsDisplayPreferences.ResolveLocale()),
            uiFont, SettingsWoodPanelSpec.ButtonFace);
        PlaceButton(ExampleButton, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(8f, 8f), new Vector2(-4f, 40f));
        ExampleButton.onClick.AddListener(FillExample);

        AskButton = SettingsShellFactory.CreateButton(
            root, "Ask", CheshireUiStrings.Lookup("SettingsAsk", SettingsDisplayPreferences.ResolveLocale()),
            uiFont, SettingsWoodPanelSpec.PrimaryButtonFace);
        PlaceButton(AskButton, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(4f, 8f), new Vector2(-8f, 40f));
        AskButton.onClick.AddListener(Ask);
    }

    public void ApplyAnswerSize()
    {
        if (AnswerLabel != null)
            AnswerLabel.fontSize = SettingsDisplayPreferences.ResolveAnswerFontSize();
    }

    void FillExample()
    {
        if (PromptInput == null)
            return;
        PromptInput.text = CheshireUiStrings.Lookup("SettingsExamplePrompt", SettingsDisplayPreferences.ResolveLocale());
        PromptInput.Select();
    }

    void Ask()
    {
        string locale = SettingsDisplayPreferences.ResolveLocale();
        string prompt = PromptInput != null ? PromptInput.text : "";
        if (!ChatHttpClient.TryNormalizePromptForChatApi(prompt, out _))
        {
            if (StatusLabel != null)
                StatusLabel.text = CheshireUiStrings.Lookup("SettingsEmptyQuestion", locale);
            return;
        }

        string chatUrl = ServerConfig.GetOrCreate().ChatUrl;
        string blocked = SettingsCheshirePreviewGate.BlockedAskKey(
            LocalAiReadiness.IsChatDisabled(),
            LocalAiReadiness.RequiresLoopbackRuntime(chatUrl),
            _localAiReady);
        if (blocked != null)
        {
            string blockedText = CheshireUiStrings.Lookup(blocked, locale);
            if (StatusLabel != null)
                StatusLabel.text = blockedText;
            if (AnswerLabel != null && blocked == SettingsCheshirePreviewGate.StatusDisabled)
                AnswerLabel.text = CheshireUiStrings.LocalAiDisabled(locale);
            return;
        }

        CancelInFlight();
        _streamBuffer = "";
        if (AnswerLabel != null)
            AnswerLabel.text = "";
        EnsureClient();
        _history.Initialize();
        UseToolsOverrideForNextRequest = false;
        _askRoutine = StartCoroutine(_client.GetGPTResponseStreaming(prompt));
    }

    IEnumerator CoPollLocalAiReady()
    {
        string chatUrl = ServerConfig.GetOrCreate().ChatUrl;
        bool requiresLoopback = LocalAiReadiness.RequiresLoopbackRuntime(chatUrl);
        _localAiReady = !requiresLoopback;
        ApplyIdleStatus();
        EnsureClient();
        bool appliedDefaultDevice = false;
        while (SettingsCheshirePreviewGate.ShouldKeepPolling(
            LocalAiReadiness.IsChatDisabled(), requiresLoopback, _localAiReady))
        {
            long statusCode = 0;
            string body = "";
            yield return _client.FetchRootStatus((code, json) =>
            {
                statusCode = code;
                body = json;
            });
            _localAiReady = LocalAiReadiness.IsLocalModelReady(
                body,
                statusCode,
                requireLocalRuntime: true);
            ApplyIdleStatus();
            if (_localAiReady)
                yield break;

            if (SettingsCheshirePreviewGate.ShouldApplyDefaultDevice(
                LocalAiReadiness.IsChatDisabled(),
                requiresLoopback,
                appliedDefaultDevice,
                SettingsCheshirePreviewGate.IsServerReachable(statusCode)))
            {
                long applyCode = 0;
                yield return _client.ApplyDefaultGpuSettings((code, _) => { applyCode = code; });
                if (applyCode == 202)
                    appliedDefaultDevice = true;
            }

            yield return new WaitForSecondsRealtime(2f);
        }

        ApplyIdleStatus();
    }

    void ApplyIdleStatus()
    {
        string locale = SettingsDisplayPreferences.ResolveLocale();
        string chatUrl = ServerConfig.GetOrCreate().ChatUrl;
        string key = SettingsCheshirePreviewGate.IdleStatusKey(
            LocalAiReadiness.IsChatDisabled(),
            LocalAiReadiness.RequiresLoopbackRuntime(chatUrl),
            _localAiReady);
        string text = CheshireUiStrings.Lookup(key, locale);
        if (StatusLabel != null)
            StatusLabel.text = text;
        Transform page = transform.parent;
        LocalAiSettingsPanel strip = page != null
            ? page.GetComponentInChildren<LocalAiSettingsPanel>(true)
            : null;
        strip?.ShowRuntimeStatus(text);
    }

    void EnsureClient()
    {
        if (_history == null)
            _history = new ChatHistoryManager(appendCommonVoice: true);
        if (_client == null)
        {
            _client = new ChatHttpClient(
                () => ServerConfig.GetOrCreate().ChatUrl,
                this,
                _history);
        }
    }

    void RefreshMetrics()
    {
        SetMetric(FirstMetric, null);
        SetMetric(CompleteMetric, null);
    }

    static void SetMetric(TMP_Text label, int? milliseconds)
    {
        if (label == null)
            return;
        label.text = milliseconds.HasValue
            ? (milliseconds.Value / 1000f).ToString("0.0")
            : "—";
    }

    static void PlaceButton(Button button, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
    }
}
