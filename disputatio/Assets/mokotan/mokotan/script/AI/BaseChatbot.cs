using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using TMPro;

public abstract class BaseChatbot : MonoBehaviour, IChatHttpCallbacks
{
    [Header("Server Settings")]
    [Tooltip("비워 두면 ServerConfig 에셋의 ChatUrl을 사용합니다.")]
    [SerializeField] protected string localServerUrl;
    protected bool isRequestInProgress;

    /// <summary>다음 /chat 요청에만 적용 후 자동 해제. null이면 기본 true.</summary>
    protected bool? useToolsOverrideForNextRequest;

    /// <summary>TMP onSubmit + IME(한글 등)에서 Enter 직후 중복 재시도 방지.</summary>
    private bool _sendImeRetryPending;

    /// <summary>
    /// false면 Resources/ChesterVoiceCommon을 시스템 프롬프트에 붙이지 않음.
    /// </summary>
    protected virtual bool AppendCommonChesterVoiceBlock => true;

    [Header("Base UI Settings")]
    [SerializeField] protected SayDialog chatSayDialog;
    [SerializeField] private SayDialog chatSayDialogPrefab;
    [SerializeField] private string chatSayDialogObjectName = "SayDialogChatbot";
    [SerializeField] private KeyCode dialogAdvanceKey = KeyCode.Space;

    [Header("Chat Input")]
    [SerializeField] private TMP_InputField userInputField;

    private DialogInput chatDialogInput;
    private ChatHttpClient _httpClient;
    private ChatHistoryManager _historyManager;

    // ---------------------------------------------------------------
    //  Protected helper access for subclasses
    // ---------------------------------------------------------------

    protected ChatHttpClient HttpClient => _httpClient;
    protected ChatHistoryManager HistoryManager => _historyManager;
    protected List<OpenAIMessage> chatHistory => _historyManager.History;
    protected string ResolvedServerUrl => _httpClient.ResolvedServerUrl;

    /// <summary>
    /// false면 TMP onSubmit에 BaseChatbot을 붙이지 않습니다.
    /// </summary>
    protected virtual bool RegisterInputFieldSubmitListener => true;

    /// <summary>
    /// false면 Say 한 줄이 끝난 뒤 SayDialog GameObject를 비활성화하지 않습니다.
    /// </summary>
    protected virtual bool DeactivateSayDialogWhenLineCompletes => true;

    /// <summary>
    /// false면 Say 중에도 인풋 필드 GameObject를 끄지 않습니다.
    /// </summary>
    protected virtual bool HideInputFieldDuringSay => true;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    protected virtual void Start()
    {
        _historyManager = new ChatHistoryManager(AppendCommonChesterVoiceBlock);
        _httpClient = new ChatHttpClient(
            () => !string.IsNullOrEmpty(localServerUrl)
                ? localServerUrl
                : ServerConfig.GetOrCreate().ChatUrl,
            this,
            _historyManager);

        InitializeChatHistory();
        _ = SceneRevisitTracker.Instance;
        EnsureDedicatedChatSayDialog();
        CacheDialogInput();
        if (userInputField != null && RegisterInputFieldSubmitListener)
            userInputField.onSubmit.AddListener(OnInputFieldSubmit);
    }

    protected virtual void Update()
    {
        if (chatSayDialog == null || !chatSayDialog.gameObject.activeInHierarchy)
            return;

        bool mouse = Input.GetMouseButtonDown(0);
        bool key = Input.GetKeyDown(dialogAdvanceKey);
        if (!mouse && !key)
            return;

        if (!AllowLegacyInputToAdvanceSayDialog(mouse, key))
            return;

        TryAdvanceChatDialog();
    }

    protected virtual void OnDestroy()
    {
        if (userInputField != null && RegisterInputFieldSubmitListener)
            userInputField.onSubmit.RemoveListener(OnInputFieldSubmit);
    }

    public void ConfigureSharedBindings(
        SayDialog sharedChatSayDialog,
        TMP_InputField sharedUserInputField,
        string serverUrlOverride = null)
    {
        chatSayDialog = sharedChatSayDialog;
        userInputField = sharedUserInputField;
        if (!string.IsNullOrWhiteSpace(serverUrlOverride))
            localServerUrl = serverUrlOverride;
        CacheDialogInput();
    }

    // ---------------------------------------------------------------
    //  Input handling
    // ---------------------------------------------------------------

    private void OnInputFieldSubmit(string _)
    {
        OnSendButtonClick();
    }

    public virtual void OnSendButtonClick()
    {
        if (userInputField == null)
        {
            GameLog.LogWarning($"{GetType().Name}: userInputField가 연결되지 않았습니다.");
            return;
        }

        string message = userInputField.text.Trim();

        if (string.IsNullOrEmpty(message))
        {
            if (!_sendImeRetryPending)
                StartCoroutine(CoTrySendAfterImeFrame());
            return;
        }

        if (isRequestInProgress)
        {
            GameLog.LogWarning("현재 이미 요청 중입니다.");
            return;
        }

        StartCoroutine(GetGPTResponse(message));
        userInputField.text = "";
    }

    private IEnumerator CoTrySendAfterImeFrame()
    {
        _sendImeRetryPending = true;
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(0.05f);

        _sendImeRetryPending = false;

        if (userInputField == null)
            yield break;

        string message = userInputField.text.Trim();
        if (string.IsNullOrEmpty(message))
        {
            GameLog.LogWarning("입력값이 비어있습니다.");
            yield break;
        }

        if (isRequestInProgress)
        {
            GameLog.LogWarning("현재 이미 요청 중입니다.");
            yield break;
        }

        StartCoroutine(GetGPTResponse(message));
        userInputField.text = "";
    }

    // ---------------------------------------------------------------
    //  SayDialog helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Space/마우스로 Say 대사를 진행해도 되는지.
    /// </summary>
    protected virtual bool AllowLegacyInputToAdvanceSayDialog(bool mouseButtonDown, bool advanceKeyDown)
    {
        return true;
    }

    private void CacheDialogInput()
    {
        if (chatSayDialog == null) return;
        chatDialogInput = chatSayDialog.GetComponentInChildren<DialogInput>(true);
    }

    protected void EnsureDedicatedChatSayDialog()
    {
        if (chatSayDialog != null
            && (string.IsNullOrWhiteSpace(chatSayDialogObjectName)
                || chatSayDialog.gameObject.name == chatSayDialogObjectName))
            return;

        chatSayDialog = ChatSayDialogResolver.ResolveExistingOrInstantiate(
            chatSayDialogObjectName,
            chatSayDialogPrefab);
    }

    private void TryAdvanceChatDialog()
    {
        if (chatDialogInput == null)
            CacheDialogInput();

        if (chatDialogInput != null)
        {
            chatDialogInput.SetNextLineFlag();
            return;
        }
        GameLog.LogWarning($"{GetType().Name}: SayDialog의 DialogInput을 찾지 못했습니다.");
    }

    protected virtual void Say(string message, Action onComplete = null)
    {
        bool restoreInput = userInputField != null && userInputField.gameObject.activeSelf;
        if (userInputField != null && HideInputFieldDuringSay)
            userInputField.gameObject.SetActive(false);

        void Done()
        {
            if (DeactivateSayDialogWhenLineCompletes
                && chatSayDialog != null
                && chatSayDialog.gameObject.activeInHierarchy)
                chatSayDialog.SetActive(false);
            if (userInputField != null && restoreInput && HideInputFieldDuringSay)
                userInputField.gameObject.SetActive(true);
            onComplete?.Invoke();
        }

        if (chatSayDialog != null)
        {
            if (!chatSayDialog.gameObject.activeInHierarchy)
                EnsureSayDialogActiveInHierarchy(chatSayDialog);

            if (chatSayDialog.gameObject.activeInHierarchy)
            {
                chatSayDialog.FadeWhenDone = false;
                chatSayDialog.Say(message, true, true, false, true, true, null, Done);
            }
            else
            {
                // 부모가 비활성이거나 씬 인스턴스가 아닌 경우: Fungus DoSay를 BaseChatbot에서 시작합니다.
                // DoSay 내부(Fungus 코드)에서 gameObject.SetActive(true)를 직접 호출하므로
                // BaseChatbot(항상 활성)에서 코루틴을 소유하면 초기 StartCoroutine이 성공합니다.
                GameLog.LogWarning($"[BaseChatbot] '{chatSayDialog.gameObject.name}' activeInHierarchy=false — DoSay를 BaseChatbot에서 시작합니다. scene.IsValid={chatSayDialog.gameObject.scene.IsValid()}");
                StartCoroutine(chatSayDialog.DoSay(message, true, true, false, true, true, null, Done));
            }
        }
        else
        {
            GameLog.LogWarning("Inspector에서 Chat Say Dialog를 연결해주세요!");
            Done();
        }
    }

    /// <summary>
    /// SayDialog 및 비활성화된 모든 상위 게임 오브젝트를 활성화합니다.
    /// </summary>
    private static void EnsureSayDialogActiveInHierarchy(SayDialog sayDialog)
    {
        for (var t = sayDialog.transform; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
        if (!sayDialog.gameObject.activeInHierarchy)
            GameLog.LogWarning($"[BaseChatbot] '{sayDialog.gameObject.name}' 계층 활성화 실패. scene.IsValid={sayDialog.gameObject.scene.IsValid()}");
    }

    // ---------------------------------------------------------------
    //  Forwarding methods (backward-compatible protected API)
    // ---------------------------------------------------------------

    protected virtual void InitializeChatHistory() => _historyManager.Initialize();

    protected IEnumerator GetGPTResponse(string userMessage)
        => _httpClient.GetGPTResponse(userMessage);

    protected IEnumerator GetGPTResponseStreaming(string userMessage)
        => _httpClient.GetGPTResponseStreaming(userMessage);

    protected string ComposeSystemPromptWithCommonRules(string roomSpecificPrompt)
        => _historyManager.ComposeSystemPromptWithCommonRules(roomSpecificPrompt);

    protected string ComposeSystemPromptWithHeuristics(string basePrompt, string userMessage)
    {
        HeuristicSignalInput signal = BuildHeuristicSignalInput(userMessage);
        return _historyManager.ComposeSystemPromptWithHeuristics(basePrompt, signal);
    }

    protected static bool TryNormalizePromptForChatApi(string userMessage, out string normalized)
        => ChatHttpClient.TryNormalizePromptForChatApi(userMessage, out normalized);

    protected static string ResolveChatClientUserId()
        => ChatHttpClient.ResolveChatClientUserId();

    protected static void AttachCertificateBypass(UnityWebRequest request)
        => ChatHttpClient.AttachCertificateBypass(request);

    // ---------------------------------------------------------------
    //  Virtual hooks
    // ---------------------------------------------------------------

    /// <summary>
    /// <c>SendWebRequest</c> 직전·직후. <see cref="isRequestInProgress"/>는 Say까지 true이므로
    /// "서버 대기" 전용 UI는 여기서 구분합니다.
    /// </summary>
    protected virtual void OnChatHttpWaitStarted() { }
    protected virtual void OnChatHttpWaitFinished() { }

    protected virtual void OnStreamTextDelta(string delta) { }

    protected virtual HeuristicSignalInput BuildHeuristicSignalInput(string userMessage)
    {
        return new HeuristicSignalInput
        {
            roomName = GetType().Name,
            progressScore = 0.5f,
            accuracyScore = 0.5f
        };
    }

    /// <summary>튜터 RAG 등 — /chat JSON에 선택 필드를 붙일 때 오버라이드합니다.</summary>
    protected virtual void AugmentChatPayload(LocalLlamaPayload payload, string userMessage) { }

    // ---------------------------------------------------------------
    //  Abstract hooks (subclasses must implement)
    // ---------------------------------------------------------------

    protected abstract string BuildFinalSystemPrompt();
    protected abstract IEnumerator HandleChatbotResponse(
        string responseMessage, List<FunctionCallData> functionCalls);

    // ---------------------------------------------------------------
    //  IChatHttpCallbacks (explicit — keeps public API untouched)
    // ---------------------------------------------------------------

    bool IChatHttpCallbacks.IsRequestInProgress
    {
        get => isRequestInProgress;
        set => isRequestInProgress = value;
    }

    bool? IChatHttpCallbacks.UseToolsOverrideForNextRequest
    {
        get => useToolsOverrideForNextRequest;
        set => useToolsOverrideForNextRequest = value;
    }

    string IChatHttpCallbacks.BuildAndComposeSystemPrompt(string userMessage)
    {
        string prompt = ComposeSystemPromptWithCommonRules(BuildFinalSystemPrompt());
        return ComposeSystemPromptWithHeuristics(prompt, userMessage);
    }

    void IChatHttpCallbacks.AugmentChatPayload(LocalLlamaPayload payload, string userMessage)
        => AugmentChatPayload(payload, userMessage);

    void IChatHttpCallbacks.OnChatHttpWaitStarted() => OnChatHttpWaitStarted();
    void IChatHttpCallbacks.OnChatHttpWaitFinished() => OnChatHttpWaitFinished();
    void IChatHttpCallbacks.OnStreamTextDelta(string delta) => OnStreamTextDelta(delta);
    void IChatHttpCallbacks.SayLine(string message, Action onComplete) => Say(message, onComplete);
    Coroutine IChatHttpCallbacks.StartHostCoroutine(IEnumerator routine) => StartCoroutine(routine);

    IEnumerator IChatHttpCallbacks.HandleChatbotResponse(
        string responseMessage, List<FunctionCallData> functionCalls)
        => HandleChatbotResponse(responseMessage, functionCalls);
}
