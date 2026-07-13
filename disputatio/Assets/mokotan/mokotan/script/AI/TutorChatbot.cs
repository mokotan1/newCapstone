using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Newtonsoft.Json;
using TMPro;

[Serializable]
internal class TutorGradeResponseDto
{
    [JsonProperty("is_correct")]
    public bool is_correct;

    [JsonProperty("question_id")]
    public string question_id;

    [JsonProperty("reference_snippet")]
    public string reference_snippet;

    [JsonProperty("quiz_complete_after")]
    public bool quiz_complete_after;

    [JsonProperty("unknown_question")]
    public bool unknown_question;
}

public class TutorChatbot : BaseChatbot, IGraderHost, IChesterParrotHost
{
    [Header("TutorBot Settings")]
    [SerializeField] public Flowchart flowchart;
    [Tooltip("비워 두면 씬에서 QuizInputHandler를 찾습니다. 전송 Button이 TutorChatbot.OnSendButtonClick에만 연결돼 있을 때 실제 입력은 여기 필드를 읽습니다.")]
    [SerializeField] private QuizInputHandler quizInputHandler;
    [Tooltip("한 줄에 하나씩 question_id (quiz_bank.csv와 동일). CorrectAnswerCount번째 줄이 현재 출제/채점 ID.")]
    [SerializeField] private TextAsset tutorQuestionOrderAsset;

    [Header("Tutor 채점 API")]
    [Tooltip("비우면 BaseChatbot의 chat URL에서 /chat → /tutor/grade 로 바꿉니다. EC2 등에 아직 /tutor/grade가 없으면 404 — 서버 배포 후 사용하거나, 여기에 전체 URL을 직접 넣으세요.")]
    [SerializeField] private string tutorGradeUrlOverride = "";

    [Header("UX — 느린 응답·패널 버튼")]
    [SerializeField] private float thinkingHoldDelaySeconds = 3f;
    [Tooltip("비우면 Fungus 언어(ko/ja/en) 기본 문구를 사용합니다.")]
    [SerializeField] [TextArea(1, 3)] private string thinkingHoldSayMessage = "";

    [Header("Events")]
    public UnityEvent OnAIReponseComplete;
    public UnityEvent OnQuizCompletedEvent;

    [Header("Debug")]
    [Tooltip("CorrectAnswerCount·채점 경로 등 퀴즈 진행 로그.")]
    [SerializeField] private bool debugQuizProgress = true;

    [Header("Chester 창 → 앵무 플로우")]
    [Tooltip("앵무 오브젝트의 Clickable2D. 비워도 씬에서 이름이 \"Parret\"인 Clickable2D를 찾습니다. WindowClicked인 동안 빈 /chat은 앵무 클릭(또는 이미 연 패널·책상 경로) 전까지 막습니다.")]
    [SerializeField] private Clickable2D chesterParrotClickable;

    // ---- helpers (created in Start) ----
    private TutorQuizStateTracker _quizState;
    private TutorQuizGrader _grader;
    private ChesterParrotFlow _chesterFlow;

    private Coroutine _thinkingHoldCoroutine;
    /// <summary><see cref="BaseChatbot.OnChatHttpWaitStarted"/>~<see cref="BaseChatbot.OnChatHttpWaitFinished"/> 구간만 true — Say 대기와 구분.</summary>
    private bool _tutorWaitingOnHttp;

    /// <summary>미션 완료 판정·UI 잠금의 단일 기준(Fungus 정답 누적).</summary>
    public const int TutorQuizTargetCorrectCount = 5;

    /// <summary>Fungus Writer 기본 writingSpeed(~60)는 글자 단위로 밀어 넣어 한글도 한 글자씩 들어가는 것처럼 보입니다.</summary>
    private const int TutorWriterCharsPerSecond = 4800;

    // ---- public properties (API unchanged) ----

    /// <summary>패널과 SayDialog 동기화(<see cref="TutorPanelSayDialogSync"/>)용.</summary>
    public SayDialog TutorSayDialogForPanelSync
    {
        get
        {
            EnsureDedicatedChatSayDialog();
            return chatSayDialog;
        }
    }

    /// <summary>서버 응답 처리 및 Say가 끝날 때까지 true — 이 동안 추가 제출은 막습니다.</summary>
    public bool IsAiResponseInFlight => isRequestInProgress;

    /// <summary>Fungus <c>CorrectAnswerCount</c>가 목표에 도달했거나 완료 이벤트가 이미 발생했습니다.</summary>
    public bool IsTutorQuizFinished => _quizState?.IsTutorQuizFinished ?? false;

    /// <summary>Enter 제출은 <see cref="QuizInputHandler"/>만 처리 — BaseChatbot과 이중 등록 방지.</summary>
    protected override bool RegisterInputFieldSubmitListener => false;

    /// <summary>퀴즈 중 대사창이 꺼지면 입력·진행이 끊기므로 Say 완료 후에도 SayDialog를 켜 둡니다.</summary>
    protected override bool DeactivateSayDialogWhenLineCompletes => false;

    /// <summary>인풋과 TutorChatbot이 같은 오브젝트를 쓰면 Say 때 SetActive(false)가 IME·여러 단어 입력을 망가뜨립니다.</summary>
    protected override bool HideInputFieldDuringSay => false;

    // ==================================================================
    //  Lifecycle
    // ==================================================================

    protected override void Start()
    {
        base.Start();

        _quizState = new TutorQuizStateTracker(
            flowchart,
            tutorQuestionOrderAsset,
            debugQuizProgress,
            OnQuizCompletedEvent,
            LockQuizInputAfterSessionComplete);

        _grader = new TutorQuizGrader(
            tutorGradeUrlOverride,
            debugQuizProgress,
            _quizState);
        _grader.SetChatUrl(localServerUrl);

        _chesterFlow = new ChesterParrotFlow(
            flowchart,
            chesterParrotClickable,
            _quizState,
            this,
            debugQuizProgress);

        if (quizInputHandler == null)
        {
            quizInputHandler = FindFirstObjectByType<QuizInputHandler>(FindObjectsInactive.Include);
            if (quizInputHandler != null)
                GameLog.LogWarning($"[{nameof(TutorChatbot)}] quizInputHandler resolved via FindFirstObjectByType — assign in Inspector for faster startup.");
        }
        ConfigureTutorSayDialogInput();

        if (_quizState.ReadCorrectAnswerCount() >= TutorQuizStateTracker.TutorQuizTargetCorrectCount)
            LockQuizInputAfterSessionComplete();
        else
            _quizState.ExpectingQuizAnswer = false;

        _chesterFlow.TryResolveClickable();
        _chesterFlow.Subscribe();
        _chesterFlow.InitializePrevWindowClicked();
        StartCoroutine(CoDeferredChesterParrotSubscribe());
    }

    private IEnumerator CoDeferredChesterParrotSubscribe()
    {
        yield return null;
        _chesterFlow.TryResolveClickable();
        _chesterFlow.Subscribe();
    }

    private void OnEnable()
    {
        if (_chesterFlow == null)
            return;
        _chesterFlow.TryResolveClickable();
        _chesterFlow.Subscribe();
    }

    private void OnDisable()
    {
        _chesterFlow?.Unsubscribe();
    }

    private void LateUpdate()
    {
        _chesterFlow?.Tick();
    }

    // ==================================================================
    //  IGraderHost (explicit — keeps public API untouched)
    // ==================================================================

    void IGraderHost.SayLine(string message, Action onComplete) => Say(message, onComplete);
    IEnumerator IGraderHost.GetGPTResponse(string userMessage) => GetGPTResponse(userMessage);
    IEnumerator IGraderHost.CoThinkingHoldIfSlow() => CoThinkingHoldIfSlow();
    Coroutine IGraderHost.StartHostCoroutine(IEnumerator routine) => StartCoroutine(routine);
    void IGraderHost.StopHostCoroutine(Coroutine coroutine) => StopCoroutine(coroutine);
    void IGraderHost.NotifyHttpWaitStarted() => OnChatHttpWaitStarted();
    void IGraderHost.NotifyHttpWaitFinished() => OnChatHttpWaitFinished();
    void IGraderHost.AttachCertificateBypass(UnityWebRequest request) => AttachCertificateBypass(request);
    void IGraderHost.HideTutorQuizUiAfterSessionComplete() => HideTutorQuizUiAfterSessionComplete();

    bool? IGraderHost.UseToolsOverrideForNextRequest
    {
        get => useToolsOverrideForNextRequest;
        set => useToolsOverrideForNextRequest = value;
    }

    // ==================================================================
    //  IChesterParrotHost (explicit)
    // ==================================================================

    bool IChesterParrotHost.IsRequestInProgress => isRequestInProgress;

    void IChesterParrotHost.ActivateQuizInputField()
    {
        if (quizInputHandler != null)
            quizInputHandler.ActivateQuizInputField();
    }

    bool IChesterParrotHost.TryStartQuizTurn(string message) => TryAddPlayerMessageAndGetResponse(message);

    // ==================================================================
    //  SayDialog / input helpers
    // ==================================================================

    /// <summary>
    /// Fungus 기본 SayDialog는 ClickAnywhere + Space로 대사 진행 — 한글 띄어쓰기·IME와 충돌. 패널 클릭으로만 진행.
    /// </summary>
    private void ConfigureTutorSayDialogInput()
    {
        if (chatSayDialog == null)
            return;
        var di = chatSayDialog.GetComponentInChildren<DialogInput>(true);
        if (di != null && di.clickMode == ClickMode.ClickAnywhere)
            di.clickMode = ClickMode.ClickOnDialog;
    }

    private static bool PointerHitContainsTMPInputField()
    {
        var es = EventSystem.current;
        if (es == null)
            return false;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponentInParent<TMP_InputField>() != null)
                return true;
        }
        return false;
    }

    protected override bool AllowLegacyInputToAdvanceSayDialog(bool mouseButtonDown, bool advanceKeyDown)
    {
        var es = EventSystem.current;
        if (es == null)
            return true;

        GameObject sel = es.currentSelectedGameObject;
        if (sel != null && sel.GetComponentInParent<TMP_InputField>() != null)
            return false;

        if (mouseButtonDown && PointerHitContainsTMPInputField())
            return false;

        return true;
    }

    protected override void Say(string message, Action onComplete = null)
    {
        if (!string.IsNullOrEmpty(message))
            message = "{s=" + TutorWriterCharsPerSecond + "}" + message + "{/s}";
        base.Say(message, onComplete);
    }

    // ==================================================================
    //  Public entry points (API unchanged)
    // ==================================================================

    /// <summary>Button이 BaseChatbot 쪽만 바인딩된 경우에도 Quiz 입력 필드로 전송되게 합니다.</summary>
    public override void OnSendButtonClick()
    {
        if (IsTutorQuizFinished)
            return;

        if (quizInputHandler != null)
        {
            quizInputHandler.SubmitQuizAnswerFromUI();
            return;
        }

        base.OnSendButtonClick();
    }

    public void AddPlayerMessageAndGetResponse(string playerMessage)
    {
        TryAddPlayerMessageAndGetResponse(playerMessage);
    }

    private bool TryAddPlayerMessageAndGetResponse(string playerMessage)
    {
        if (IsTutorQuizFinished)
            return false;

        if (isRequestInProgress)
        {
            GameLog.LogWarning("이미 AI 응답 요청이 진행 중입니다. 새로운 요청을 무시합니다.");
            return false;
        }

        StartCoroutine(CoTutorPlayerTurn(playerMessage));
        GameLog.Log($"플레이어 답변 전송 및 GPT 응답 요청: {playerMessage}");
        return true;
    }

    /// <summary>GameTimer 등 — 퀴즈와 무관한 짧은 안내용.</summary>
    public void TriggerAIResponseByFlag()
    {
        if (isRequestInProgress)
            return;
        if (IsTutorQuizFinished)
            return;
        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        StartCoroutine(CoTutorPlayerTurn(CheshireUiStrings.TimerLowTimePrompt(locale)));
    }

    /// <summary>답 입력 없이 패널 버튼만 눌렀을 때 — 정답 직후 또는 건너뛰기 직후에만 다음 AI 턴으로 넘깁니다.</summary>
    public bool TryHandleEmptyPanelSubmit()
    {
        if (_quizState == null || IsTutorQuizFinished)
            return false;

        if (debugQuizProgress)
            GameLog.Log(
                $"[TutorQuiz] TryHandleEmptyPanelSubmit: awaiting={_quizState.AwaitingQuestionAdvance}, " +
                $"isRequestInProgress={isRequestInProgress}, lastQuizComplete={_quizState.LastEmbeddedQuizComplete}, " +
                $"CorrectAnswerCount={_quizState.ReadCorrectAnswerCount()}");

        if (!_quizState.AwaitingQuestionAdvance || isRequestInProgress)
            return false;
        if (_quizState.LastEmbeddedQuizComplete)
            return false;

        _quizState.AwaitingQuestionAdvance = false;

        if (!_quizState.LastGradedWasCorrect)
            _quizState.SkipOrderOffset++;

        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        string msg = _quizState.LastGradedWasCorrect
            ? CheshireUiStrings.EmptyPanelAdvancePrompt(locale)
            : CheshireUiStrings.EmptyPanelSkipPrompt(locale);

        if (debugQuizProgress)
            GameLog.Log(
                $"[TutorQuiz] 빈 패널 제출 수락 → 다음 AI 턴 (직전 채점 정답={_quizState.LastGradedWasCorrect}), " +
                $"_skipOrderOffset={_quizState.SkipOrderOffset}, CorrectAnswerCount={_quizState.ReadCorrectAnswerCount()}");

        StartCoroutine(CoTutorPlayerTurn(msg));
        return true;
    }

    // ==================================================================
    //  Core orchestration coroutines
    // ==================================================================

    private IEnumerator CoTutorPlayerTurn(string playerMessage)
    {
        if (ShouldRunDeterministicGrading(playerMessage))
        {
            yield return StartCoroutine(_grader.CoGradeThenReact(playerMessage, this));
            yield break;
        }

        string llmUserTurn = playerMessage;

        if (string.IsNullOrWhiteSpace(llmUserTurn)
            && flowchart != null
            && flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked)
            && !IsTutorQuizFinished
            && !_chesterFlow.QuizStarted
            && !(quizInputHandler != null && quizInputHandler.IsQuizInputPanelActive))
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(llmUserTurn)
            && flowchart != null
            && flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked)
            && !IsTutorQuizFinished)
        {
            string locale = CheshireLocaleResolver.ResolveCurrentLocale();
            llmUserTurn = _chesterFlow.ImmediateQuestionTurnPending
                ? CheshireUiStrings.UserPromptChesterParrotAskQuestionNow(locale)
                : CheshireUiStrings.UserPromptChesterWindowOpen(locale);
        }

        bool immediateParrotQuestion = _chesterFlow.ImmediateQuestionTurnPending;
        if (immediateParrotQuestion)
            _chesterFlow.ImmediateQuestionTurnPending = false;

        if (immediateParrotQuestion)
            _thinkingHoldCoroutine = null;
        else
            _thinkingHoldCoroutine = StartCoroutine(CoThinkingHoldIfSlow());
        yield return StartCoroutine(GetGPTResponse(llmUserTurn));
        if (_thinkingHoldCoroutine != null)
        {
            StopCoroutine(_thinkingHoldCoroutine);
            _thinkingHoldCoroutine = null;
        }
    }

    private bool ShouldRunDeterministicGrading(string playerMessage)
    {
        if (string.IsNullOrWhiteSpace(playerMessage))
            return false;
        if (playerMessage.TrimStart().StartsWith("[", StringComparison.Ordinal))
            return false;
        if (flowchart == null || !flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked))
            return false;
        if (IsTutorQuizFinished)
            return false;
        return _quizState != null && _quizState.ExpectingQuizAnswer;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage, List<FunctionCallData> functionCalls)
    {
        if (chatSayDialog != null)
        {
            chatSayDialog.Stop();
            chatSayDialog.FadeWhenDone = false;
        }

        string displayMessage = TutorQuizGrader.PrepareTutorDisplayAndQuizMetadata(
            responseMessage,
            out bool hasEmbeddedQuizJson,
            out bool embeddedIsCorrect,
            out bool embeddedQuizComplete);

        if (string.IsNullOrWhiteSpace(displayMessage) && hasEmbeddedQuizJson)
            displayMessage = " ";

        bool isComplete = false;
        Say(displayMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);

        _grader.ProcessTutorNonQuizToolsOnly(functionCalls, HandleEmote);
        if (debugQuizProgress)
        {
            GameLog.Log(
                $"[TutorQuiz] LLM 턴(채점은 /tutor/grade 전용): hasEmbeddedJson={hasEmbeddedQuizJson}, " +
                $"embeddedIsCorrect={embeddedIsCorrect}, CorrectAnswerCount={_quizState.ReadCorrectAnswerCount()}");
        }

        if (flowchart != null && flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked))
        {
            _quizState.ExpectingQuizAnswer = !IsTutorQuizFinished;
            if (hasEmbeddedQuizJson)
                _quizState.AwaitingQuestionAdvance = TutorQuizGrader.ShouldOfferPanelAdvanceAfterTurn(
                    chatHistory, hasEmbeddedQuizJson, embeddedQuizComplete);
            else
                _quizState.AwaitingQuestionAdvance = false;
            _quizState.LastEmbeddedQuizComplete = IsTutorQuizFinished;
        }

        if (_quizState.ExpectingQuizAnswer
            && !IsTutorQuizFinished
            && quizInputHandler != null
            && flowchart != null
            && flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked)
            && quizInputHandler.IsQuizInputPanelActive)
        {
            quizInputHandler.ActivateQuizInputField();
        }

        OnAIReponseComplete?.Invoke();
    }

    // ==================================================================
    //  Prompt building
    // ==================================================================

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string finalSystemPrompt = chatHistory[0].content;

        if (flowchart != null)
        {
            bool windowClicked = flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked);
            if (windowClicked)
            {
                string tutorRoomPrompt = CheshirePromptCatalog.Load("TutorRoomPrompt", locale);
                if (!string.IsNullOrEmpty(tutorRoomPrompt))
                {
                    finalSystemPrompt += "\n\n" + tutorRoomPrompt;
                }
                else
                {
                    Debug.LogError($"TutorRoomPrompt (CheshirePrompts/{locale}) 를 찾을 수 없습니다!");
                    finalSystemPrompt += "\n\n[중요 지시]... (TutorRoomPrompt 내용)...";
                }
            }
        }
        return finalSystemPrompt;
    }

    protected override void AugmentChatPayload(LocalLlamaPayload payload, string userMessage)
    {
        if (flowchart == null || !flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked))
            return;

        payload.rag_profile = "tutor";
        string qid = _quizState.ResolveCurrentQuestionIdFromOrderAsset();
        if (!string.IsNullOrWhiteSpace(qid))
            payload.current_question_id = qid.Trim();
    }

    protected override HeuristicSignalInput BuildHeuristicSignalInput(string userMessage)
    {
        var signal = base.BuildHeuristicSignalInput(userMessage);
        signal.roomName = nameof(TutorChatbot);

        if (flowchart != null)
        {
            int currentCorrectCount = Mathf.Clamp(
                _quizState.ReadCorrectAnswerCount(), 0, TutorQuizTargetCorrectCount);
            float denom = Mathf.Max(1, TutorQuizTargetCorrectCount);
            signal.progressScore = currentCorrectCount / denom;
            signal.accuracyScore = currentCorrectCount / denom;
        }

        return signal;
    }

    // ==================================================================
    //  HTTP wait / thinking hold
    // ==================================================================

    protected override void OnChatHttpWaitStarted()
    {
        _tutorWaitingOnHttp = true;
    }

    protected override void OnChatHttpWaitFinished()
    {
        _tutorWaitingOnHttp = false;
    }

    private IEnumerator CoThinkingHoldIfSlow()
    {
        float delay = Mathf.Max(thinkingHoldDelaySeconds, 0.5f);
        yield return new WaitForSecondsRealtime(delay);
        if (!_tutorWaitingOnHttp)
            yield break;
        if (chatSayDialog != null)
        {
            chatSayDialog.Stop();
            chatSayDialog.FadeWhenDone = false;
        }
        Say(
            CheshireUiStrings.ResolveThinkingHoldMessage(
                thinkingHoldSayMessage,
                CheshireLocaleResolver.ResolveCurrentLocale()),
            null);
    }

    // ==================================================================
    //  Emote / post-session helpers
    // ==================================================================

    private void HandleEmote(Dictionary<string, object> args)
    {
        if (args == null || !args.ContainsKey("emotion")) return;
        string emotion = args["emotion"].ToString();
        GameLog.Log($"Chester emote: {emotion}");
        // TODO: wire to animation controller when Chester animations are ready
    }

    private void LockQuizInputAfterSessionComplete()
    {
        if (quizInputHandler != null)
            quizInputHandler.SetQuizInputLocked(true);
    }

    /// <summary>5문제 완료 후 Parret 패널과 Fungus SayDialog를 끕니다.</summary>
    private void HideTutorQuizUiAfterSessionComplete()
    {
        if (quizInputHandler != null)
            quizInputHandler.DeactivateQuizInputPanel();

        // 퀴즈 블록이 Clickable2D 잠금 경로를 타지 않아 InteractionLock이 남는 경우 방 클릭이 막힐 수 있음
        InteractionLock.ForceUnlock();

        if (chatSayDialog == null)
            return;
        chatSayDialog.Stop();
        chatSayDialog.gameObject.SetActive(false);
    }

    public void IncrementCorrectAnswerCount()
    {
        _quizState?.IncrementCorrectAnswerCount();
    }
}
